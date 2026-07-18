# Implementação do Plugin Zeiss Calypso PDF (SelectML.Parsers.ZeissPdf)

Este plano detalha a construção do driver para extração de dados não-estruturados de relatórios em PDF gerados por máquinas CMM Zeiss Calypso, mantendo conformidade com os contratos do `SelectML.Core`.

## User Review Required
> [!IMPORTANT]
> Aprovação do plano e da divisão das Sprints abaixo para darmos início à **Sprint 1**.
> Por favor, confira se as expressões regulares propostas no POC atendem a todas as variações conhecidas dos PDFs da Zeiss, e confirme se podemos prosseguir com a criação da branch para a Sprint 1.

## Abordagem Técnica e Arquitetura

O plugin `SelectML.Parsers.ZeissPdf` será construído como uma Class Library (.NET 8) dependente do pacote NuGet `UglyToad.PdfPig` (Licença MIT).
Devido à natureza dos PDFs da Zeiss, a extração ocorrerá em duas frentes de processamento:
1. **Extração Espacial/Cabeçalhos:** Varredura estruturada nas primeiras linhas/blocos para obter "Part name", "Run" (BatchNumber), "Modelo MMC", e "Nº MMC".
2. **Extração de Tabela de Características:** Identificação do cabeçalho da tabela, captura inteligente das linhas baseada nas palavras-chave aglutinadas ("Measured value Nominal value") e sanitização de dados complexos com Regex (remoção de marcações LaTeX e separação de números).

---

## Divisão de Sprints

### Sprint 1: Infraestrutura e Leitura PDF
- Criar novo projeto `SelectML.Parsers.ZeissPdf` (Class Library .NET 8).
- Referenciar o projeto core `SelectML.Core`.
- Adicionar o pacote NuGet `UglyToad.PdfPig`.
- Implementar classe `ZeissPdfParser : IMachineParser`.
- Implementar o método `CanParse` (verificando a extensão `.pdf`).
- Lógica básica em `Parse` para abrir o documento via `PdfDocument.Open()` e extrair as palavras e blocos da primeira página.

### Sprint 2: Extrator de Cabeçalhos
- Criar a máquina de estados para varrer o topo da página do PDF (Bounding Boxes ou Leitura Textual Lógica).
- Lógica para buscar os rótulos e pareá-los com seus valores adjacentes:
  - **PartName:** Identificar "Part name" e capturar o texto adjacente (ex: `11_30 graus_comprimento_10mm_REV_03` ou lidar com quebras de linha).
  - **BatchNumber:** Identificar "Run" e capturar (ex: `Todas Caracteristicas`).
  - Mapear "Modelo MMC" e "Nº MMC" como metadados adicionais, caso requisitados em regras futuras.

### Sprint 3: Extrator de Tabela e Sanitização Complexa
- Identificar as âncoras da tabela de medição (`"Name"`, `"Measured value Nominal value"`, etc).
- Iterar nas linhas da tabela ignorando texto redundante.
- Desenvolver as Expressões Regulares (Regex) reais para limpar os valores aglutinados em colunas.
- Criar testes locais / rotinas para validar cenários híbridos onde uma característica é Graus e outra é Linear.

### Sprint 4: Conversão de Valores e Integração
- Implementar a rotina de conversão Graus/Minutos/Segundos -> Grau Decimal.
- Implementar conversão segura de decimais separados por vírgula utilizando `CultureInfo("pt-BR")`.
- Realizar a injeção dos resultados tratados (`Dictionary<string, double>`) no retorno estruturado `MeasurementData`.
- Empacotamento para teste e injeção do arquivo `.dll` na aplicação Client.

---

## POC (Proof of Concept) - Expressões Regulares e Sanitização

### 1. Limpeza e Isolamento de Graus/Minutos/Segundos (LaTeX)
**Cenário Real:** `$12^{\circ}13^{\prime}5^{\prime\prime}$ $12^{\circ}0^{\prime}0^{\prime\prime}$`

```csharp
// Expressão regular para capturar Grau, Minuto e Segundo ignorando a sujeira do LaTeX
// Captura 3 blocos numéricos para cada medição
var regexGMS = new Regex(@"(-?\d+)\^\{\\circ\}(\d+)\^\{\\prime\}(\d+)\^\{\\prime\\prime\}");
var matches = regexGMS.Matches(inputString);

// matches[0] conterá os grupos do "Measured Value"
// matches[1] conterá os grupos do "Nominal Value"

if (matches.Count >= 1) {
    double graus = double.Parse(matches[0].Groups[1].Value);
    double minutos = double.Parse(matches[0].Groups[2].Value);
    double segundos = double.Parse(matches[0].Groups[3].Value);
    
    // Matemática da conversão para Grau Decimal (preservando o sinal de números negativos)
    double sinal = graus < 0 ? -1 : 1;
    double grauDecimal = (Math.Abs(graus) + (minutos / 60.0) + (segundos / 3600.0)) * sinal;
    
    // Resultado injetado no dicionário Results.
}
```

### 2. Separação de Decimais Aglutinados
**Cenário Real:** `2,5200 2,5280 mm` ou até `2,5012 mm 2,5000` devido à desformatação.

```csharp
// Captura números com vírgulas separados por espaço, lidando com sinais e sufixos ignorados
var regexDecimais = new Regex(@"(-?\d+,\d+)\s*(?:mm)?\s+(-?\d+,\d+)");
var match = regexDecimais.Match(inputString);

if (match.Success) {
    string measuredStr = match.Groups[1].Value;
    string nominalStr = match.Groups[2].Value;
    
    // Conversão vital com Cultura Português-BR devido à separação por vírgula
    double measured = double.Parse(measuredStr, new CultureInfo("pt-BR"));
    double nominal = double.Parse(nominalStr, new CultureInfo("pt-BR"));
}
```
