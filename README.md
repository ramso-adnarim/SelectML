# SelectML - Monitoramento de Medições Industriais

## Visão Geral do Projeto

O **SelectML** é uma aplicação Desktop desenvolvida em **WPF (.NET 8)** projetada para o ambiente de Indústria 4.0. Seu objetivo principal é automatizar a coleta e padronização de dados de medição industrial.

O sistema monitora continuamente uma pasta especificada, detecta novos arquivos gerados por máquinas de medição (como ViciVision, Zeiss, etc.), processa esses arquivos através de **Plugins (DLLs)** dedicados e gera um arquivo **CSV padronizado** e limpo, pronto para integração com bancos de dados ou análise em Excel.

**Principais Funcionalidades:**
- **Monitoramento em Tempo Real:** Uso de `FileSystemWatcher` para detecção imediata de novos arquivos.
- **Arquitetura de Plugins:** Extensibilidade total para suportar novas máquinas sem a necessidade de recompilar o núcleo da aplicação.
- **Robustez:** Implementa lógica de "Debounce/Retry" para garantir que arquivos não sejam processados enquanto ainda estão bloqueados pela máquina emissora.
- **Tratamento de Encoding:** Suporte explícito a caracteres especiais de máquinas legadas (Ø, °, µ) e geração de saída compatível com Excel.
- **Interface Reativa:** UI MVVM limpa e responsiva, com validação de estado.

---

## Arquitetura da Solução

A solução segue os princípios de **Clean Architecture** simplificada e o padrão **MVVM (Model-View-ViewModel)**.

### Fluxo de Dados

1.  **Monitoramento:** O `MainViewModel` inicia um serviço de monitoramento (`FileSystemWatcher`) no diretório configurado.
2.  **Detecção de Evento:** Quando um arquivo é criado, o sistema detecta o evento e aguarda o desbloqueio do arquivo (Lógica de Retry).
3.  **Seleção de Plugin:** O arquivo é submetido ao Plugin (`IMachineParser`) selecionado pelo usuário.
4.  **Parsing:** O Plugin lê o arquivo original (respeitando o encoding Latin1) e extrai os dados relevantes para um objeto `MeasurementData`.
5.  **Geração de CSV:** Os dados estruturados são exibidos na interface e salvos em um arquivo CSV padronizado na subpasta `/Output` (encoding UTF8-BOM).

### Diagrama de Fluxo

```mermaid
graph TD
    A[Máquina de Medição] -->|Salva Arquivo| B(Pasta Monitorada)
    B -->|Evento Created| C[FileSystemWatcher]
    C -->|Debounce & Retry| D{Arquivo Liberado?}
    D -- Não --> D
    D -- Sim --> E[Plugin Parser]
    E -->|Lê (Latin1)| F[Objeto MeasurementData]
    F -->|Bind| G[Interface WPF]
    F -->|Escreve (UTF8-BOM)| H[CSV Padronizado]
    H --> I[Integração / Banco de Dados]
```

---

## Estrutura de Pastas e Arquivos

A organização do repositório foca na separação clara de responsabilidades:

*   **/SelectML.Core**
    *   Núcleo da aplicação contendo contratos e modelos.
    *   `IMachineParser.cs`: A interface que define o contrato para todos os plugins.
    *   `MeasurementData.cs`: O modelo de dados canônico usado para trafegar informações.

*   **/SelectML.Client**
    *   Aplicação principal (WPF).
    *   `/ViewModels`: Lógica de apresentação (`MainViewModel.cs`).
    *   `/Services`: Serviços de infraestrutura (`PluginLoader.cs` para carregar DLLs, `ConfigService.cs` para persistência).
    *   `/Views`: Camada visual (XAML).

*   **/SelectML.Parsers.NomeDaMaquina**
    *   Projetos independentes para cada fabricante (ex: `SelectML.Parsers.ViciVision`).
    *   Cada projeto compila uma DLL que deve ser copiada para a pasta de Plugins.

---

## Guia de Desenvolvimento de Plugins

Para adicionar suporte a uma nova máquina (ex: "Zeiss"), você deve criar um novo projeto **Class Library (.NET 8)** e implementar a interface `IMachineParser`.

### Passo a Passo

1.  **Crie o Projeto:**
    *   Crie um projeto Class Library chamado `SelectML.Parsers.SuaMaquina`.
    *   Adicione uma referência de projeto para `SelectML.Core`.

2.  **Implemente a Interface:**

```csharp
using System.Text;
using SelectML.Core;

namespace SelectML.Parsers.SuaMaquina
{
    public class ZeissParser : IMachineParser
    {
        public string MachineName => "Zeiss CMM";

        public bool CanParse(string filePath)
        {
            // Validação simples (ex: verificar extensão)
            return filePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);
        }

        public MeasurementData Parse(string filePath)
        {
            var data = new MeasurementData();

            // REGRA DE OURO #1: LEITURA EM LATIN1
            // Importante para suportar símbolos como Ø, °, µ
            var lines = File.ReadAllLines(filePath, Encoding.GetEncoding("iso-8859-1"));

            // ... Lógica de parsing (Regex recomendado) ...

            // Exemplo de preenchimento
            data.PartName = "Eixo_Z";
            data.BatchNumber = "Lote_001";
            data.Results.Add("Diametro", 10.05);

            return data;
        }
    }
}
```

### Regras de Ouro

1.  **Encoding de Leitura (CRÍTICO):** Ao ler os arquivos originais da máquina, você **DEVE** usar `Encoding.GetEncoding("iso-8859-1")` (Latin1). A maioria das máquinas industriais utiliza codificações ANSI antigas. Ler em UTF-8 padrão corromperá caracteres especiais.

2.  **Validação de Bloqueio:** Embora o `MainViewModel` possua lógica de retry, seu parser deve estar preparado para capturar exceções de I/O (`IOException`) caso o arquivo ainda esteja sendo escrito ou bloqueado.

---

## Formato de Saída (Output)

O arquivo CSV gerado na pasta `/Output` segue um padrão rígido para facilitar importações automatizadas.

**Nomenclatura do Arquivo:**
`Result_{NomePeca}_{DataHora}.csv`
*Exemplo:* `Result_EixoPrincipal_20231027_143000.csv`

**Estrutura do Conteúdo:**
O arquivo sempre terá 4 linhas e utiliza codificação **UTF-8 com BOM** (byte order mark), garantindo que acentos e símbolos abram corretamente no Excel.

1.  **Linha 1:** Nome da Peça
2.  **Linha 2:** Número do Lote
3.  **Linha 3:** Cabeçalhos (Nomes das características), separados por vírgula.
4.  **Linha 4:** Valores, separados por vírgula (Ponto flutuante com ponto `.` e invariante).

**Exemplo:**
```csv
Eixo_Virabrequim
Lote_2023_A
Diametro_Ponta,Comprimento_Total,Rugosidade_Ra
15.002,120.50,0.85
```

---

## Instalação e Execução

### Pré-requisitos
*   .NET 8 SDK
*   Visual Studio 2022 ou VS Code

### Compilando e Rodando

1.  **Clone o Repositório:**
    ```bash
    git clone https://github.com/seu-org/SelectML.git
    ```

2.  **Compile a Solução:**
    Utilize o Visual Studio ou a linha de comando:
    ```bash
    dotnet build
    ```

3.  **Configuração de Plugins:**
    Certifique-se de que as DLLs dos plugins (ex: `SelectML.Parsers.ViciVision.dll`) estejam na pasta `Plugins` dentro do diretório de saída da aplicação (`/bin/Debug/net8.0-windows/Plugins`).
    *O sistema varre esta pasta automaticamente na inicialização.*

4.  **Executando:**
    *   Inicie o `SelectML.Client.exe`.
    *   Clique em "Editar Configuração" se necessário.
    *   Selecione a pasta a ser monitorada.
    *   Escolha o Plugin correto para a máquina.
    *   Clique em "Salvar e Iniciar".
