# Mapa de Navegação da Base de Código (SelectML) - V1.2.8

> **Nota para IAs:** Este documento serve como um índice rápido ("Context Window Efficiency") para navegação no projeto. Consulte este mapa antes de perguntar "Onde está a lógica de X?".

## 📂 Estrutura de Diretórios e Responsabilidades

### `SelectML.Core`
> Biblioteca compartilhada de tipos, contratos e modelos de domínio. Dependência mais baixa do ecossistema.
- **`MeasurementData.cs`**: DTO fundamental. Representa uma corrida/peça medida (contendo PartName, BatchNumber, Results e o dicionário `Tolerances` com nominais e limites USL/LSL).
- **`IMachineParser.cs`**: Contrato base que todo plugin de máquina implementa (`MachineName`, `CanParse`, `Parse`).
- **`IConfigurableParser.cs`**: Contrato estendido para parsers com parametrização dinâmica via templates (`ActiveTemplateName`, `ActiveTemplate`, `SetActiveTemplate`, `INotifyPropertyChanged`).
- **`IDatabaseService.cs`**: Contrato para acesso ao banco de dados SQL Server (consultas de lotes, estações e características da rotina).
- **`InspectionFeatureInfo.cs`**: Modelo de dados de características da rotina cadastrada no MeasurLink (`FeatureID`, `FeatureName`, `FeatureType`, `Nominal`, `LSL`, `USL`, `DecimalLength`, `IsAttribute`).
- **`EncodingDetector.cs`**: Utilitário resiliente para detecção automática de codificação de arquivos de texto (UTF-8 com/sem BOM, Latin-1/ANSI).
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
  - DataGrid com customização de colunas (Valor Numérico vs Atributo Toggle via `ValueColumnTemplateSelector`).
  - Destaque visual de tolerâncias (verde se dentro de limites, vermelho se fora de USL/LSL).
  - Botão de engrenagem ⚙️ para abertura contextual do configurador de templates quando `IConfigurableParser` estiver ativo.
  - Menu Configurações com Toggle para "Listar todas as características da rotina" (`IsListAllFeaturesActive`).
- **`ViewModels/MainViewModel.cs`**: Orquestrador MVVM:
  - **Serial Concurrency & Queue (Item 1)**: Toggle `EnqueueSerialInput`. Suporte a modo híbrido sem travar arquivo e comando de Desfazer (Undo) serial U-WAVE.
  - **Listagem de Características da Rotina (Item 2)**: Consulta metadados da rotina no banco (`GetInspectionRoutineFeaturesAsync`), preenchimento serial de cima para baixo (top-down), auto-merge de características desconhecidas e smart undo.
  - **Universal Template Engine (Item 3)**: Integração bidirecional com `IConfigurableParser`, salvando `LastTemplateName` no `appsettings.json` e atualizando o rótulo da ComboBox dinamicamente.
- **`ViewModels/TemplateConfigViewModel.cs`**: ViewModel da janela de templates. Gerencia lista de templates, regras ativas, carregamento de amostras de arquivo e painel de prévia em tempo real.
- **`Views/TemplateConfigWindow.xaml` / `.cs`**: Janela interativa de configuração de templates com destaque sintático colorido por regras e visualização em tempo real.
- **`Views/ModernDialogWindow.xaml` / `.cs`**: Diálogos modais padronizados, com suporte a temas Claro/Escuro, substituindo caixas de mensagem nativas.
- **`Views/SerialConfigWindow.xaml` / `SerialConfigViewModel.cs`**: Configuração serial, com `ModernToggleSwitch` para enfileiramento e validação de nome padrão.
- **`Services/DatabaseService.cs`**: Conectividade SQL Server com queries de alto desempenho (`GetInspectionRoutineFeaturesAsync`, `GetAllStationsAsync`).
- **`Services/AppConfig.cs` & `ConfigService.cs`**: Gerencia persistência atômica em `%APPDATA%\SelectML\appsettings.json`.

### `SelectML.Parsers.*`
> Projetos de Plugins específicos para máquinas e formatos de arquivo.
- **`SelectML.Parsers.TextTemplate`**: Novo plugin universal baseado em templates JSON parametrizáveis (`TextTemplateParser`). Implementa `IConfigurableParser`.
- **`SelectML.Parsers.ViciVision`**: Parser para arquivos CSV gerados por máquinas ViciVision.
- **`SelectML.Parsers.ViciVisionJson`**: Parser para arquivos JSON gerados pela ViciVision X5 (com extração de tolerâncias nominais).
- **`SelectML.Parsers.ZeissPdf`**: Parser para relatórios PDF Zeiss Calypso com agrupamento espacial via `PdfPig`.

---

## 🌳 Árvore de Dependências (V1.2.8)

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
    
    MainWindow -.-> TemplateConfigWindow
    TemplateConfigWindow --> TemplateConfigViewModel
    TemplateConfigViewModel --> TemplateRepository
    TemplateConfigViewModel --> TemplateExecutionEngine
    TemplateConfigViewModel --> TemplateRuleGenerator
    
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

## 📚 Glossário Técnico (V1.2.8)

| Termo | Definição |
| :--- | :--- |
| **IConfigurableParser** | Interface para parsers que possuem configurações visuais adicionais por templates (exibe botão ⚙️ na UI principal). |
| **TemplateDefinition** | Modelo serializável em JSON que armazena as regras de extração de cabeçalho, características e valores para arquivos de texto. |
| **Top-Down Serial Filling** | Modo na V1.2.8 em que medições manuais recebidas via Serial preenchem sequencialmente as características pendentes da rotina de cima para baixo. |
| **U-WAVE Undo** | Capacidade de receber o comando de desfazer da botoeira/pedal do U-WAVE e desempilhar a última medição serial registrada. |
| **Inspection Routine Listing** | Exibição de todas as cotas cadastradas na rotina do MeasurLink (`ListAllFeatures`), com limites nominais e cores de tolerância. |
| **Buffer Reverso Híbrido** | Processamento cooperativo entre medidas seriais manuais e arquivos de máquinas sem bloquear ou descartar dados concorrentes. |

---

## ⚡ Regras de Ouro para Agentes de IA (Golden Rules)

1.  **UI Thread Resiliente**: O `SerialPort` e o `FileSystemWatcher` disparam callbacks em threads secundárias do pool. **SEMPRE** utilize `Application.Current.Dispatcher.Invoke` ou `InvokeAsync` antes de manipular coleções observáveis ou abrir janelas WPF.
2.  **Tratamento de Exceções em Métodos `async void`**: Métodos `async void` usados como comandos ou manipuladores de eventos DEVEM conter blocos `try-catch` abrangentes no nível raiz para evitar encerramento forçado do processo.
3.  **Persistência Única em `%APPDATA%\SelectML`**: Todas as configurações do usuário (`appsettings.json`) e templates (`Templates/*.json`) devem residir sob o `%APPDATA%\SelectML`, garantindo que atualizações do instalador Velopack não destruam as configurações do cliente.
4.  **Auto-Detecção de Encoding**: Arquivos de texto externos devem ser lidos preferencialmente usando `EncodingDetector.Detect()`, respeitando arquivos legados em ANSI/Latin-1 e arquivos modernos em UTF-8.
5.  **Robustez de Expressões Regulares**: Expressões regulares definidas em regras de templates devem sempre ser instanciadas e executadas com tratamento de exceções para proteger contra padrões sintáticos inválidos informados pelo operador.
6.  **Compatibilidade Retroativa de Configurações**: Ao introduzir novas propriedades em `AppConfig`, atribua valores padrão diretamente nas propriedades para que JSONs antigos sem essas propriedades sejam desserializados de forma totalmente transparente e segura.
