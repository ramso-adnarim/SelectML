# Mapa de Navegação da Base de Código (SelectML) - V1.2.9

> **Nota para IAs:** Este documento serve como um índice rápido ("Context Window Efficiency") para navegação no projeto. Consulte este mapa antes de perguntar "Onde está a lógica de X?".

## 📂 Estrutura de Diretórios e Responsabilidades

### `SelectML.Core`
> Biblioteca compartilhada de tipos, contratos e modelos de domínio. Dependência mais baixa do ecossistema.
- **`MeasurementData.cs`**: DTO fundamental. Representa uma corrida/peça medida (contendo PartName, BatchNumber, Results, Traceabilities e o dicionário `Tolerances` com nominais e limites USL/LSL).
- **`IMachineParser.cs`**: Contrato base que todo plugin de máquina implementa (`MachineName`, `CanParse`, `Parse`).
- **`IConfigurableParser.cs`**: Contrato estendido para parsers com parametrização dinâmica via templates (`ActiveTemplateName`, `ActiveTemplate`, `SetActiveTemplate`, `INotifyPropertyChanged`).
- **`IDatabaseService.cs`**: Contrato para acesso ao banco de dados SQL Server (consultas de lotes, estações, características da rotina e catálogo de rastreabilidades).
- **`InspectionFeatureInfo.cs`**: Modelo de dados de características da rotina cadastrada no MeasurLink (`FeatureID`, `FeatureName`, `FeatureType`, `Nominal`, `LSL`, `USL`, `DecimalLength`, `IsAttribute`).
- **`EncodingDetector.cs`**: Utilitário resiliente para detecção automática de codificação de arquivos de texto (UTF-8 com/sem BOM, Latin-1/ANSI e heurística para UTF-16 LE/BE sem BOM).
- **`Traceability/`**: Domínio do motor de rastreabilidade do MeasurLink:
  - **`TraceabilityItem.cs`**: DTO observável para itens de rastreabilidade exibidos na UI (`Key`, `Value`, `Definition`, `PredefinedOptions`, `ItemType`, `IsStrictSelection`, `IsSelectionWithAdditions`, `SampleMask`, `IsMaskInvalid`, `IsUnmappedInRun`, `ValidationToolTip`).
  - **`TraceabilityFieldDefinition.cs`**: Modelo de definição de campo no catálogo do MeasurLink (`TraceabilityListID`, `ListName`, `FolderName`, `ItemType`, `Mask`, `PredefinedOptions`).
  - **`TraceMaskInfo.cs`**: Decodificador binário e validador Regex da máscara do MeasurLink (`Parse()`, `IsEnabled`, `RegexPattern`, `SampleMask`, `Validate()` com timeout resiliente).
- **`Templates/`**: Domínio do motor universal de templates de texto:
  - **`TemplateDefinition.cs`**: Modelo de template (Nome, extensão de arquivo, delimitador padrão, regras de linha e fallbacks).
  - **`TemplateLineRule.cs`**: Regra de extração por linha física ou repetitiva (`Role`, `LineIndex`, `FeatureColumnIndex`, `ValueColumnIndex`, `RegexPattern`, `RegexGroupName`).
  - **`TemplateExecutionEngine.cs`**: Motor de execução determinístico que processa arquivos de medição conforme o template, com analisador numérico tolerante a vírgula/ponto (`SmartNumberParser`).
  - **`TemplateRepository.cs`**: Gerenciador de persistência de templates JSON em `%APPDATA%\SelectML\Templates\*.json`.
  - **`TemplateRuleGenerator.cs`**: Gerador e dedutor de regras a partir de seleções textuais feitas pelo usuário.
  - **`TemplatePreviewItem.cs`**: DTO para visualização instantânea de prévias de medição.

### `SelectML.Client`
> Aplicação WPF Principal (.NET 8).
- **`App.xaml.cs`**: Entry point. Handlers globais de exceção (`DispatcherUnhandledException`, `UnhandledException`, `UnobservedTaskException`), integração Velopack e alternância dinâmica de temas.
- **`MainWindow.xaml` / `MainWindow.xaml.cs`**: View principal:
  - Botão de Rastreabilidade no menu superior com ícone e badge visual.
  - Popover de Rastreabilidade (`TraceabilityPopoverView`) flutuante com suporte a adição dinâmica, opções pré-definidas (`ItemType = 1`), caixas de texto com validação de máscara (`ItemType = 0`), Número de Série dedicado e toggle de filtro.
  - DataGrid com customização de colunas (Valor Numérico vs Atributo Toggle via `ValueColumnTemplateSelector`).
  - Destaque visual de tolerâncias (verde se dentro de limites, vermelho se fora de USL/LSL).
  - Botão de engrenagem ⚙️ para abertura contextual do configurador de templates quando `IConfigurableParser` estiver ativo.
- **`ViewModels/MainViewModel.cs`**: Orquestrador MVVM:
  - **Traceability Engine**: Gestão de `Traceabilities`, `AvailableDbTraceabilityFields`, `IsTraceabilityPopoverOpen`, `SendOnlyMappedTraceabilities`, `SerialNumberTraceability`, persistência no `AppConfig` e checagem preventiva de obrigatoriedade ao iniciar corridas.
  - **Debounce de 350ms em `BatchNumber` e `PartName`**: Setters reativos com cancelamento assíncrono via `CancellationTokenSource`, prevenindo concorrência e congelamento de interface durante digitação rápida.
  - **Filtro de Características Variáveis (`KnownVariableFeatures`)**: Expõe apenas características onde `IsAttribute == false` para a ComboBox de inserção manual de cotas.
  - **Serial Concurrency & Queue**: Toggle `EnqueueSerialInput`, modo híbrido concorrente e comando de Desfazer (*Undo*) U-WAVE.
  - **Listagem de Características da Rotina**: Consulta metadados da rotina no banco (`GetInspectionRoutineFeaturesAsync`), preenchimento serial top-down e tolerâncias visuais.
- **`Views/TraceabilityPopoverView.xaml` / `.cs`**: Componente visual do popover de rastreabilidades.
- **`ViewModels/TemplateConfigViewModel.cs`**: ViewModel da janela de templates.
- **`Views/TemplateConfigWindow.xaml` / `.cs`**: Janela interativa de configuração de templates.
- **`Views/ModernDialogWindow.xaml` / `.cs`**: Diálogos modais padronizados Claro/Escuro.
- **`Services/DatabaseService.cs`**: Conectividade SQL Server com queries de alto desempenho (`GetInspectionRoutineFeaturesAsync`, `GetAllStationsAsync`, `GetTraceabilityFieldsAsync`, `GetTraceabilityItemValuesAsync`).
- **`Services/AppConfig.cs` & `ConfigService.cs`**: Gerencia persistência atômica em `%APPDATA%\SelectML\appsettings.json`.

### `SelectML.Parsers.*`
> Projetos de Plugins específicos para máquinas e formatos de arquivo.
- **`SelectML.Parsers.TextTemplate`**: Plugin universal baseado em templates JSON parametrizáveis (`TextTemplateParser`). Implementa `IConfigurableParser`.
- **`SelectML.Parsers.ViciVision`**: Parser para arquivos CSV gerados por máquinas ViciVision.
- **`SelectML.Parsers.ViciVisionJson`**: Parser para arquivos JSON gerados pela ViciVision X5 (com extração de tolerâncias nominais).
- **`SelectML.Parsers.ZeissPdf`**: Parser para relatórios PDF Zeiss Calypso com agrupamento espacial via `PdfPig`.

---

## 🌳 Árvore de Dependências (V1.2.9)

```mermaid
graph TD
    App --> MainWindow
    MainWindow --> MainViewModel
    MainViewModel --> ConfigService
    MainViewModel --> FileLifecycleService
    MainViewModel --> PluginLoader
    MainViewModel --> SerialPortService
    MainViewModel --> DatabaseService
    MainViewModel --> ModernDialogWindow
    
    MainWindow -.-> TraceabilityPopoverView
    MainWindow -.-> TemplateConfigWindow
    TemplateConfigWindow --> TemplateConfigViewModel
    TemplateConfigViewModel --> TemplateRepository
    TemplateConfigViewModel --> TemplateExecutionEngine
    TemplateConfigViewModel --> TemplateRuleGenerator
    
    MainViewModel --> TraceabilityItem
    MainViewModel --> TraceMaskInfo
    DatabaseService --> TraceabilityFieldDefinition
    
    PluginLoader ..-> IMachineParser (Carrega Dinamicamente)
    PluginLoader ..-> IConfigurableParser (Identifica Plugins Configuráveis)
    
    TextTemplateParser -.-> IMachineParser
    TextTemplateParser -.-> IConfigurableParser
    TextTemplateParser --> TemplateExecutionEngine
    TextTemplateParser --> TemplateRepository
    
    SerialPortService --> ISerialDeviceStrategy (UWave / Custom)
    SerialPortService --> SerialPort (System.IO.Ports)
    
    MainViewModel -- Ouve Eventos & Undo --> SerialPortService
```

---

## 📚 Glossário Técnico (V1.2.9)

| Termo | Definição |
| :--- | :--- |
| **Traceability Engine** | Subsistema que integra campos de rastreabilidade cadastrados no MeasurLink com preenchimento na interface e injeção no CSV final. |
| **ItemType 0 vs 1** | No MeasurLink: `0` indica campo de texto livre (validado por máscara); `1` indica seleção restrita a itens pré-cadastrados na tabela `Traceability_Item_Value`. |
| **RequiredFieldMask** | Máscara de bits no MeasurLink que determina se um campo de rastreabilidade é obrigatório para a rotina ou estação atual. |
| **Debounced Setter** | Padrão assíncrono que aguarda uma janela de inatividade do operador (ex: 350ms) antes de disparar queries pesadas no SQL Server. |
| **KnownVariableFeatures** | Coleção filtrada de cotas da rotina contendo exclusivamente características de tipo variável (numéricas), excluindo atributos. |
| **IConfigurableParser** | Interface para parsers que possuem configurações visuais adicionais por templates (exibe botão ⚙️ na UI principal). |
| **U-WAVE Undo** | Capacidade de receber o comando de desfazer da botoeira/pedal do U-WAVE e desempilhar a última medição serial registrada. |
| **Buffer Reverso Híbrido** | Processamento cooperativo entre medidas seriais manuais e arquivos de máquinas sem bloquear ou descartar dados concorrentes. |

---

## ⚡ Regras de Ouro para Agentes de IA (Golden Rules)

1.  **UI Thread Resiliente**: O `SerialPort` e o `FileSystemWatcher` disparam callbacks em threads secundárias do pool. **SEMPRE** utilize `Application.Current.Dispatcher.Invoke` ou `InvokeAsync` antes de manipular coleções observáveis ou abrir janelas WPF.
2.  **Tratamento de Exceções em Métodos `async void`**: Métodos `async void` usados como comandos ou manipuladores de eventos DEVEM conter blocos `try-catch` abrangentes no nível raiz com log estruturado para evitar encerramento forçado do processo.
3.  **Persistência Única em `%APPDATA%\SelectML`**: Todas as configurações do usuário (`appsettings.json`) e templates (`Templates/*.json`) devem residir sob o `%APPDATA%\SelectML`, garantindo que atualizações do instalador Velopack não destruam as configurações do cliente.
4.  **Preservação de Debounce em Setters Reativos**: Ao alterar propriedades que disparam consultas ao banco de dados (como `BatchNumber` e `PartName`), preserve sempre a lógica de debounce com `CancellationTokenSource`, evitando enfileiramento de chamadas concorrentes.
5.  **Gestão de Rastreabilidade e Catálogo**: Ao sincronizar campos do banco de dados com a lista de rastreabilidades do operador, respeite itens já marcados como `IsModifiedByUser` e não apague configurações feitas pelo usuário antes ou depois do clique em "Salvar e Iniciar".
6.  **Validação de Expressões Regulares com Timeout**: Expressões regulares definidas em regras de templates ou geradas pelo `TraceMaskInfo` devem sempre especificar `TimeSpan` de timeout e captura de `RegexMatchTimeoutException` para proteção contra DoS.
7.  **Auto-Detecção de Encoding**: Arquivos de texto externos devem ser lidos preferencialmente usando `EncodingDetector.Detect()`, respeitando arquivos legados em ANSI/Latin-1, UTF-8 e UTF-16 LE/BE sem BOM.
8.  **Compatibilidade Retroativa de Configurações**: Ao introduzir novas propriedades em `AppConfig`, atribua valores padrão diretamente nas propriedades para que JSONs antigos sem essas propriedades sejam desserializados de forma totalmente transparente e segura.
