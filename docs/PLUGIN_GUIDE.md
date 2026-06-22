# Guia de Desenvolvimento de Plugins SelectML

Este guia detalha como criar drivers (parsers) para integrar novas máquinas de medição ao ecossistema SelectML. O sistema utiliza uma arquitetura de plugins baseada em reflexão, permitindo adicionar suporte a novos hardwares sem recompilar a aplicação principal.

## 1. Visão Geral

Um plugin no SelectML é uma biblioteca de classes (.dll) que implementa a interface `IMachineParser`.
A responsabilidade do plugin é única e exclusiva: **Ler um arquivo de texto bruto (gerado pela máquina) e transformá-lo em um objeto estruturado `MeasurementData`.**

### O Fluxo

1.  A aplicação detecta um novo arquivo.
2.  A aplicação carrega seu plugin dinamicamente.
3.  O método `Parse(filePath)` é chamado.
4.  Seu plugin lê o arquivo, extrai os dados e retorna o objeto.
5.  A aplicação cuida do resto (Backup, Banco de Dados, UI, CSV final).

## 2. Contratos (SelectML.Core)

Todos os plugins devem referenciar o projeto (ou DLL) `SelectML.Core`.

### A Interface `IMachineParser`

```csharp
namespace SelectML.Core
{
    public interface IMachineParser
    {
        // Nome que aparecerá na lista de seleção da UI (ex: "Zeiss Calypso")
        string MachineName { get; }

        // Validação rápida para saber se o arquivo pertence a esta máquina
        // Geralmente verifica extensão ou convenção de nome
        bool CanParse(string filePath);

        // O trabalho pesado: lê o arquivo e retorna os dados
        MeasurementData Parse(string filePath);
    }
}
```

### O Objeto `MeasurementData`

```csharp
public class MeasurementData
{
    public string PartName { get; set; }      // Obrigatório
    public string BatchNumber { get; set; }   // Obrigatório (usado para roteamento)
    public DateTime MeasureDate { get; set; } // Data da medição

    // Dicionário de Características
    // Key: Nome da característica (ex: "Diametro_A")
    // Value: Valor medido (double)
    public Dictionary<string, double> Results { get; set; }

    // Helper para verificar se o parsing foi mínimo
    public bool IsValid => !string.IsNullOrEmpty(PartName) && Results.Count > 0;
}
```

## 3. Passo a Passo: Criando seu Primeiro Plugin

### Passo 1: Criar o Projeto
No Visual Studio, crie um novo projeto do tipo **Class Library** segmentado para **.NET 8**.
Nome sugerido: `SelectML.Parsers.NomeDaMaquina` (ex: `SelectML.Parsers.Protequality`).

### Passo 2: Referências
Adicione uma dependência ao projeto `SelectML.Core`.
Se você estiver na mesma solution, adicione como "Project Reference". Se estiver externo, referencie a `SelectML.Core.dll`.

### Passo 3: Implementação

Crie uma classe pública que implemente `IMachineParser`.

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using SelectML.Core;

namespace SelectML.Parsers.Protequality
{
    public class ProtequalityParser : IMachineParser
    {
        public string MachineName => "Protequality CMM";

        public bool CanParse(string filePath)
        {
            return filePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);
        }

        public MeasurementData Parse(string filePath)
        {
            var data = new MeasurementData();

            // DICA DE OURO 1: Encoding
            // Máquinas antigas não usam UTF-8. Use Latin1 (ISO-8859-1) por padrão.
            var content = File.ReadAllLines(filePath, Encoding.GetEncoding("iso-8859-1"));

            foreach (var line in content)
            {
                // Lógica de parsing (Exemplo simplificado)
                // Use Regex para extrair dados de forma robusta

                if (line.StartsWith("PART:"))
                {
                    data.PartName = line.Substring(5).Trim();
                }
                else if (line.StartsWith("BATCH:"))
                {
                    data.BatchNumber = line.Substring(6).Trim();
                }
                // ... Extração de características
            }

            return data;
        }
    }
}
```

### Passo 4: Compilação e Deploy
1.  Compile o projeto em **Release**.
2.  Copie a DLL gerada (ex: `SelectML.Parsers.Protequality.dll`) para a pasta `Plugins` dentro do diretório da aplicação principal (`SelectML.Client`).
3.  Reinicie o SelectML. O novo parser deve aparecer na lista de configuração.

## 4. Dicas de Ouro e Boas Práticas

### Encoding é Tudo
90% dos problemas de integração ocorrem aqui.
*   **Sintoma**: Símbolos como `Ø` (diâmetro) ou `µ` (mícron) viram caracteres estranhos ( ou Ã¼).
*   **Solução**: Force a leitura em Latin1.
    ```csharp
    Encoding.GetEncoding("iso-8859-1")
    ```
    *Nota: Em .NET Core/.NET 5+, pode ser necessário registrar o `CodePagesEncodingProvider` se a codificação não for encontrada, mas Latin1 geralmente é suportado.*

### Cultura e Números (CultureInfo)
Arquivos de máquina podem vir com ponto (`10.5`) ou vírgula (`10,5`) decimal dependendo da configuração do PC industrial.
*   **Sempre** use `CultureInfo.InvariantCulture` se o arquivo usar ponto.
*   Se o arquivo usar vírgula, use `new CultureInfo("pt-BR")`.
*   **Nunca** confie na cultura padrão do sistema operacional (`double.Parse(valor)`), pois o servidor pode estar em EN-US e a máquina em PT-BR.

```csharp
// Exemplo seguro
double valor = double.Parse("10.50", CultureInfo.InvariantCulture);
```

### Tratamento de Erros
*   Se o arquivo estiver malformado ou incompleto, **não estoure exceções**.
*   Ao invés disso, capture o erro internamente (`try-catch`) e retorne um objeto `MeasurementData` vazio ou com dados insuficientes.
*   O SelectML verificará a propriedade `.IsValid` (que será `false` se `PartName` ou `Results` estiverem vazios) e tratará o caso como um "Arquivo Inválido" de forma graciosa na UI.

### Regex vs Split
Evite usar `string.Split(' ')` se o arquivo tiver espaçamento variável. Prefira Expressões Regulares (Regex) para capturar grupos nomeados. É mais robusto e fácil de manter.

---
**Dúvidas?** Consulte o arquivo `docs/ARCHITECTURE.md` para entender como seu plugin se encaixa no fluxo de dados.
