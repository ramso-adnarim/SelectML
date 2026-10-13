# Plano de Implementação: Mecanismo de Templates para Arquivos de Texto/CSV (V1.2.8 - Item 3)

**Versão Alvo:** 1.2.8  
**Tipo:** Nova Funcionalidade / Arquitetura de Plugins / Editor Gráfico de Templates / Parser Regex  
**Status:** Planejamento Refinado e Alinhado (Aguardando Aval Final para Execução)  
**Data:** 13/10/2026  
**Documento de Origem:** Solicitação de Usuário (Item 3 - V1.2.8)  
**Branch de Desenvolvimento:** `feature/v1.2.8-template-engine`

---

## 1. Visão Geral e Contexto Operacional

### 1.1 Contexto e Premissa de Plugins Vivos
O **SelectML** foi projetado com uma arquitetura modular baseada em plugins dinâmicos (`IMachineParser`), permitindo a incorporação de leitores especializados para diferentes equipamentos de medição (tais como ViciVision CSV, ViciVision JSON e Zeiss Calypso PDF) através do depósito de arquivos DLL na pasta `Plugins/`.

Contudo, no chão de fábrica e em laboratórios de metrologia, é comum a existência de diversos equipamentos (ex: projetores de perfil, micrômetros laser, rugosímetros, tornos CNC e CMMs) que geram relatórios em arquivos de texto delimitados (CSV, TXT, DAT). Embora cada equipamento possua um leiaute próprio, a grande maioria segue estruturas padrão que podem ser perfeitamente atendidas por um **Template configurável**, dispensando o desenvolvimento e compilação de novas DLLs para cada máquina.

### 1.2 O Mecanismo de Templates (Inspiração MeasurLink)
Para suprir essa necessidade com máxima autonomia e facilidade operacional:
* A aplicação passa a contar com um algoritmo padrão denominado **"Leitor de arquivos de texto"**, distribuído como plugin nativo.
* Ao selecionar essa opção na ComboBox "Equipamento", um botão com ícone de configuração (⚙️) surge diretamente à direita da seleção (à esquerda do botão "✖").
* Ao clicar na engrenagem, abre-se a janela modal **"Configuração de Templates"**, onde o usuário gerencia múltiplos templates através de um editor visual e interativo.
* O operador carrega um arquivo de texto de exemplo via botão **"Procurar"**, seleciona trechos no visualizador com o mouse, clica com o botão direito e atribui os campos:
  1. **Nome da rotina** (`PartName` / Identificação da Peça).
  2. **Nome da corrida (lote)** (`BatchNumber` / Identificação do Lote).
  3. **Nome da característica** (nomes das cotas/dimensões).
  4. **Valor medido** (leituras numéricas correspondentes).
  5. **Separador** (delimitador de tokens: vírgula, ponto e vírgula, TAB, espaço, etc.).
* O sistema colore os trechos selecionados com **cores distintas** (Vermelho, Roxo, Azul, Verde) e gera automaticamente as regras de interpretação e expressões regulares (REGEX).
* **Dupla Estrutura Nativa (Sem Atrito):** O motor interpreta transparentemente tanto arquivos de **estrutura horizontal** (uma linha com cotas e outra com valores medidos) quanto de **estrutura linha por linha** (onde cada linha subsequente traz uma característica e seu respectivo valor), sem que o operador precise selecionar modos ou tipos estruturais.
* **Detecção Automática de Encoding e Separadores Decimais:** O usuário **não** precisa configurar codificação nem regras de cultura/país. O sistema detecta automaticamente se o arquivo é UTF-8 ou ANSI/Latin-1 (preservando símbolos de engenharia como `Ø`, `°`, `µ`) e interpreta inteligentemente se os decimais utilizam ponto ou vírgula.
* Ao confirmar, a ComboBox da tela principal exibe **"Leitor de arquivos de texto - [NOME DO TEMPLATE]"** e os arquivos recebidos no diretório de monitoramento são processados com total fidelidade.

---

## 2. Requisitos Detalhados do Item 3

### Requisito 3.1: Plugin "Leitor de arquivos de texto" Otimizado
* Criar o projeto de plugin `SelectML.Parsers.TextTemplate` em conformidade com o contrato `IMachineParser`.
* O plugin deve ser compilado para a pasta `Plugins/` da aplicação (`Plugins/SelectML.Parsers.TextTemplate.dll`), integrando-se nativamente ao `PluginLoader`.
* Implementação extremamente enxuta, focada em alta performance de I/O e processamento determinístico.

### Requisito 3.2: Botão de Configuração (Engrenagem ⚙️) no Cabeçalho
* Na interface principal (`MainWindow.xaml`), ao lado da ComboBox de Equipamento:
  * Inserir um botão de configuração (⚙️) estilizado como `IconButton`, posicionado **à direita da ComboBox e imediatamente à esquerda do botão de exclusão ("✖")**.
  * **Visibilidade Dinâmica:** O botão deve ficar visível exclusivamente quando o leitor de templates estiver selecionado (`IsTemplateParserSelected == true`).
  * Respeitar o estado do monitoramento (`IsEnabled="{Binding IsConfigEnabled}"`).

### Requisito 3.3: Janela "Configuração de Templates"
* Janela modal moderna com título **"Configuração de Templates"**, integrada ao tema (Dark/Light mode) e design system do SelectML.
* Organização limpa e ergonômica sem campos desnecessários ou sobrecarga visual.

### Requisito 3.4: Visualização de Arquivos com Auto-Detecção de Encoding
* Botão **"Procurar..."** para seleção de arquivos de exemplo (`.csv`, `.txt`, `.dat`, `*.*`).
* **Auto-Detecção de Codificação:** A aplicação identifica automaticamente a codificação do arquivo (UTF-8 com/sem BOM, ISO-8859-1 / Latin-1, ASCII, Windows-1252), garantindo a renderização imediata e perfeita de caracteres industriais (`Ø`, `°`, `µ`) sem exigir que o operador configure menus de codificação.

### Requisito 3.5: Gestão Centralizada de Múltiplos Templates
* A janela oferece criação (com nome customizado), renomeação, duplicação e exclusão de múltiplos templates.
* **Armazenamento Centralizado e Unificado:** Todos os templates são salvos como arquivos JSON individuais em `%APPDATA%\SelectML\Templates\`, mesmo diretório raiz das demais configurações da aplicação (`appsettings.json`). Isso evita a criação de diretórios dispersos e facilita o acesso, backup e administração pelo usuário.

### Requisito 3.6: Marcação Visual, Cores e Interpretação Automática (Horizontal e Linha a Linha)
* O usuário seleciona trechos do texto no visualizador e clica com o botão direito para definir:
  * 📋 **Nome da rotina** -> Destaque em **Vermelho**
  * 🏷️ **Nome da corrida (lote)** -> Destaque em **Roxo**
  * 📐 **Nome da característica** -> Destaque em **Azul**
  * 📊 **Valor medido** -> Destaque em **Verde**
  * ⚙️ **Definir separador** (ponto e vírgula `;`, vírgula `,`, TAB `\t`, espaço)
  * ❌ **Remover marcação**
* **Interpretação Transparente de Leiautes:**
  * **Leiaute Horizontal (Matriz):** Quando o usuário marca uma linha inteira como Características e outra como Valores com delimitador.
  * **Leiaute Linha a Linha:** Quando o usuário marca linhas onde cada registro traz a característica e o valor medido correspondente.
  * O motor deduz a estrutura automaticamente a partir das marcações visuais, sem exigir toggles adicionais do usuário.

### Requisito 3.7: Validação Obrigatória e Fallback Inteligente de Arquivo
* O template só pode ser salvo se possuir regras definidas para **Características** e **Valores Medidos** (ambos estritamente obrigatórios).
* **Fallback Automático:** Caso o usuário **não** selecione trechos no texto para "Nome da rotina" ou "Nome da corrida", a aplicação automaticamente define que esses campos utilizarão o **Nome do arquivo processado** (sem extensão). A validação de preenchimento completo é atendida com sucesso sem bloquear o operador.

### Requisito 3.8: Atualização Dinâmica do Rótulo na ComboBox
* Ao salvar ou selecionar um template e confirmar (OK), a ComboBox de equipamentos atualiza imediatamente seu rótulo para:
  `"Leitor de arquivos de texto - [NOME DO TEMPLATE]"`
* As propriedades `LastPluginName` e `LastTemplateName` são salvas no `appsettings.json`, sendo restauradas perfeitamente ao reiniciar o SelectML.

### Requisito 3.9: Leitura em Tempo Real e Auto-Interpretação Decimal
* Quando arquivos chegam no diretório de monitoramento, o `TextTemplateParser` executa o arquivo com o template ativo.
* **Auto-Interpretação Decimal:** O algoritmo identifica de forma resiliente o separador decimal do arquivo (distinguindo se o ponto `.` ou a vírgula `,` representa a fração numérica, considerando o separador de colunas utilizado), convertendo os valores medidos em `double` de forma consistente.
* Retorna `MeasurementData` estruturado, acionando normalmente o grid de resultados, tolerâncias (Item 2), integração serial (Item 1) e gravação no banco de dados.

---

## 3. Diretrizes Técnicas e Resolução de Decisões de Projeto

### 🎯 D1: Arquitetura Híbrida Enxuta (Plugin DLL + UI no Client)
* **Decisão:** Aprovada. O projeto `SelectML.Parsers.TextTemplate` contém estritamente o motor de leitura (`TemplateExecutionEngine`) e implementa `IMachineParser`. A interface gráfica `TemplateConfigWindow` reside no `SelectML.Client`.
* **Otimização:** Menor volume de código, zero dependências extras na pasta de plugins, aproveitamento nativo do Design System do Client e compilação ágil.

### 🎯 D2: Rótulo Dinâmico no ComboBox e Persistência
* **Decisão:** Aprovada. O `TextTemplateParser` implementa `INotifyPropertyChanged` para `MachineName`. Ao selecionar/salvar um template, o nome dinâmico é atualizado instantaneamente em tela e salvo em `appsettings.json`.

### 🎯 D3: Dupla Estrutura Transparente (Horizontal e Linha a Linha)
* **Decisão:** O motor suporta ambos os formatos de forma automática:
  1. *Formato Horizontal:* Identificado quando existem regras com papel `CharacteristicNames` e `MeasuredValues` associadas a linhas distintas com separadores de lista.
  2. *Formato Linha por Linha:* Identificado quando as regras associam características e valores na mesma linha ou em linhas consecutivas repetitivas.
  * O usuário não precisa configurar nem indicar o tipo de formato; o motor infere a estratégia diretamente a partir da distribuição das regras criadas.

### 🎯 D4: Localização Centralizada de Dados
* **Decisão:** Concentrar o armazenamento na pasta `%APPDATA%\SelectML\Templates\`, mantendo um único ponto de administração de dados do usuário ao lado de `appsettings.json`. Evita proliferação de subpastas ou pastas restritas no sistema operacional.

### 🎯 D5: Auto-Detecção de Codificação e Separadores Decimais
* **Decisão:** Nenhuma seleção manual de codificação ou cultura será exposta ao operador.
  1. **Detecção de Encoding:** Algoritmo utilitário que analisa o preâmbulo do arquivo (BOM) e inspeciona bytes estendidos. Se houver sequências UTF-8 válidas, lê como UTF-8; caso contrário, decodifica automaticamente como ISO-8859-1 (Latin-1/ANSI), garantindo a preservação imediata de `Ø`, `°`, `µ` e acentuações.
  2. **Interpretação Decimal Inteligente:** Analisador numérico que verifica a presença de vírgula ou ponto no token de leitura em relação ao delimitador de campo, convertendo com segurança para `double` invariante.

---

## 4. Arquitetura da Solução e Diagramas

```mermaid
graph TD
    subgraph "Interface Principal (MainWindow)"
        ComboEquip["ComboBox Equipamento"] -->|Seleciona| TextPlugin["Leitor de arquivos de texto"]
        TextPlugin -->|Ativa| BtnConfig["Botão Engrenagem ⚙️<br/>(Visível & Habilitado)"]
        BtnConfig -->|Clique| OpenWindow["Abre TemplateConfigWindow"]
    end

    subgraph "Janela de Configuração (TemplateConfigWindow)"
        OpenWindow --> LeftPanel["Painel Esquerdo:<br/>Lista de Templates (Novo, Duplicar, Excluir)"]
        OpenWindow --> TopPanel["Painel Superior:<br/>Nome do Template, Extensão & Tabela de Regras"]
        OpenWindow --> BottomPanel["Visualizador de Arquivo de Amostra:<br/>Botão Procurar + Prévia Monoespaçada"]
        
        BottomPanel --> AutoDetect["Auto-Detecção Transparente:<br/>Encoding (UTF-8/Latin-1)"]
        AutoDetect --> TextPreview["Renderização do Texto com Símbolos"]
        
        TextPreview --> ContextMenu["Menu de Contexto (Botão Direito):<br/>- Nome da rotina (Vermelho)<br/>- Nome da corrida (Roxo)<br/>- Características (Azul)<br/>- Valores Medidos (Verde)<br/>- Separador"]
        
        ContextMenu --> Highlighter["Marcação por Cores"]
        ContextMenu --> RuleGen["Gera Regras / Regex<br/>(Horizontal ou Linha a Linha)"]
        
        RuleGen --> Validate{"Validação:<br/>Características e Valores presentes?"}
        Validate -->|Sim (com Fallback automático de Arquivo)| SaveTemplate["Salva em %APPDATA%/SelectML/Templates/{Nome}.json"]
        SaveTemplate --> UpdateCombo["Atualiza ComboBox:<br/>'Leitor de arquivos de texto - [Nome]'"]
    end

    subgraph "Execução em Tempo Real (FileWatcher)"
        NewFile["Arquivo CSV/TXT Detectado"] --> CanParse{"TextTemplateParser.CanParse?"}
        CanParse -->|Sim| Engine["TemplateExecutionEngine.Execute()"]
        Engine --> SmartEncoding["Lê com Auto-Encoding (UTF-8/Latin-1)"]
        SmartEncoding --> ExtractMetadata["Extrai PartName e BatchNumber<br/>(Regra ou Nome do Arquivo)"]
        ExtractMetadata --> ExtractValues["Extrai Características e Valores<br/>(Auto-Parsing Decimal Ponto/Vírgula)"]
        ExtractValues --> MeasData["Retorna MeasurementData Válido"]
        MeasData --> Flow["Segue Fluxo: Tolerâncias, Serial, Envio"]
    end
```

---

## 5. Estrutura de Modelos e Modificações Previstas

### 5.1 Novos Modelos em `SelectML.Core/Templates/`

#### `TemplateDefinition.cs` (Modelo Enxuto)
```csharp
namespace SelectML.Core.Templates
{
    public class TemplateDefinition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "Novo Template";
        public string FileExtension { get; set; } = ".csv";
        
        // Fallbacks automáticos para nome do arquivo
        public bool UseFileNameForPartName { get; set; } = true;
        public bool UseFileNameForBatchNumber { get; set; } = true;

        // Regras de extração configuradas
        public List<TemplateLineRule> LineRules { get; set; } = new List<TemplateLineRule>();

        public bool IsValid(out string errorMessage)
        {
            errorMessage = string.Empty;
            bool hasFeatures = LineRules.Any(r => r.Role == LineRuleRole.CharacteristicNames || r.Role == LineRuleRole.RowFeatureAndValue);
            bool hasValues = LineRules.Any(r => r.Role == LineRuleRole.MeasuredValues || r.Role == LineRuleRole.RowFeatureAndValue);

            if (!hasFeatures || !hasValues)
            {
                errorMessage = "O template deve conter o mapeamento de Características e Valores Medidos.";
                return false;
            }

            return true;
        }
    }
}
```

#### `TemplateLineRule.cs`
```csharp
namespace SelectML.Core.Templates
{
    public enum LineRuleRole
    {
        PartName = 1,              // Nome da Rotina
        BatchNumber = 2,           // Nome da Corrida / Lote
        CharacteristicNames = 3,   // Nomes das Características (delimitadas por separador)
        MeasuredValues = 4,        // Valores Medidos (delimitados por separador)
        RowFeatureAndValue = 5     // Linha que contém tanto a Característica quanto o Valor
    }

    public class TemplateLineRule
    {
        public int LineIndex { get; set; } // Índice da linha (1-based) ou 0 para padrão repetitivo
        public LineRuleRole Role { get; set; }
        public string Separator { get; set; } = ";";
        public string RegexPattern { get; set; } = string.Empty;
    }
}
```

### 5.2 Novo Projeto de Plugin: `SelectML.Parsers.TextTemplate`
* Localização: `SelectML.Parsers.TextTemplate/SelectML.Parsers.TextTemplate.csproj`
* Configurado com saída direta para `Plugins/SelectML.Parsers.TextTemplate.dll`.
* Contém:
  * `TextTemplateParser.cs`: Implementa `IMachineParser, INotifyPropertyChanged`.
  * `TemplateExecutionEngine.cs`: Executa o algoritmo de extração com auto-detecção de encoding e conversão decimal inteligente.
  * `EncodingDetector.cs`: Utilitário enxuto de detecção de codificação.

### 5.3 Repositório de Templates em `SelectML.Client/Services/TemplateRepository.cs`
* Armazenamento centralizado em:
  `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SelectML", "Templates")`
* Métodos assíncronos e síncronos de carregamento e salvamento de arquivos `.json`.

---

## 6. Design Visual e Experiência do Usuário (UI/UX)

### 6.1 Posicionamento da Engrenagem na Tela Principal
```text
+------------------------------------------------------------------------------------+
| Equipamento:                                                                       |
| [ Leitor de arquivos de texto - Torno CNC  ▼ ] [ ⚙ ] [ ✖ ]                          |
+------------------------------------------------------------------------------------+
```
* **Botão ⚙**: Estilo `IconButton`, 25x30 px, visível exclusivamente quando `SelectedParser` for o leitor de templates.
* Desabilitado durante o monitoramento ativo.

### 6.2 Layout da Janela "Configuração de Templates"
* Janela dimensionável e ergonômica (960x650 px).
* **Painel Esquerdo (Lista de Templates):**
  * Título: "Templates"
  * Ações: Criar (➕), Duplicar (📋), Excluir (🗑️).
  * Lista limpa com destaque visual no template selecionado.
* **Painel Superior Direito (Metadados e Resumo das Regras):**
  * Nome do Template: `[ TextBox ]`
  * Extensão do Arquivo: `[ ComboBox (.csv, .txt, .dat, Todos) ]`
  * Grade resumida com as regras criadas: Linha, Função e Indicador de status.
* **Painel Inferior Direito (Visualizador de Arquivo de Amostra):**
  * Barra de ferramentas: `Arquivo: [ TextBox ] [ Procurar... ] [ Limpar ]`
  * Área de texto com fonte monoespaçada (`Consolas`), numeração lateral de linhas e rolagem suave.
  * Seleção por mouse e botão direito:
    * 🔴 **Nome da rotina** (Vermelho suave)
    * 🟣 **Nome da corrida** (Roxo suave)
    * 🔵 **Características** (Azul suave)
    * 🟢 **Valores Medidos** (Verde suave)
* **Rodapé:**
  * Indicador de validação com mensagens de apoio.
  * Botões `[ Cancelar ]` e `[ Salvar e Aplicar ]`.

---

## 7. Estratégia de Testes e Homologação

1. **Testes de Auto-Detecção de Codificação:**
   * Leitura de arquivos salvos em UTF-8 e ANSI/Latin-1 contendo `Ø`, `°`, `µ` confirmando preservação integral dos caracteres sem configuração manual.
2. **Testes dos Dois Tipos de Estrutura:**
   * Arquivo horizontal (cabeçalho de cotas na linha 3, leituras na linha 4).
   * Arquivo vertical / linha a linha (cada linha trazendo nome da característica e leitura).
3. **Testes de Resolução Decimal Inteligente:**
   * Arquivos com vírgula como decimal (`12,345`) e delimitador `;`.
   * Arquivos com ponto como decimal (`12.345`) e delimitador `,`.
4. **Testes de Validação e Fallback:**
   * Validação de bloqueio caso faltem características ou valores.
   * Validação do fallback para nome do arquivo para rotina/lote.
5. **Testes de Não-Regressão e Pipeline Completo:**
   * Monitoramento contínuo, preenchimento de tolerâncias (Item 2) e fila serial (Item 1).
