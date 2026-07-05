# Plano de Implementação - Fase 4: Estética e Observabilidade

Este documento descreve a estratégia técnica para a Fase 4 do projeto SelectML, focada na modernização da interface com "Dark Mode" e na implementação de um sistema robusto de logs.

## 1. Sistema de Temas (Dark Mode)

**Requisito:** Implementar um tema escuro completo e um botão de alternância (Toggle).

### Estratégia Técnica (WPF)

*   **ResourceDictionaries:**
    *   Criar arquivos XAML separados em `Themes/`:
        *   `Themes/Light.xaml`
        *   `Themes/Dark.xaml`
    *   Definir chaves de pincel (Brushes) semânticas padrão em ambos os dicionários para garantir a troca consistente. Exemplos:
        *   `AppBackgroundBrush`
        *   `CardBackgroundBrush`
        *   `PrimaryTextBrush`
        *   `SecondaryTextBrush`
        *   `BorderBrush`
        *   `InputBackgroundBrush`

*   **Gerenciador de Temas (ThemeService):**
    *   Implementar uma classe `ThemeService` (ou lógica em `App.xaml.cs`) responsável por:
        *   Carregar a preferência salva na inicialização.
        *   Método `SetTheme(ThemeMode mode)` que:
            1.  Limpa os dicionários de temas antigos de `Application.Current.Resources.MergedDictionaries`.
            2.  Adiciona o `ResourceDictionary` correspondente (Light ou Dark).
            3.  Atualiza o ícone da janela.

*   **Controle de UI:**
    *   Adicionar um botão de Toggle (ou botão simples com ícone de Sol/Lua) na barra de título ou área de configuração da `MainWindow`.
    *   O comando deste botão deve invocar o `ThemeService` para alternar o estado.

*   **Persistência:**
    *   Adicionar uma propriedade `IsDarkMode` (bool) em `Properties.Settings`.
    *   Salvar a escolha do usuário sempre que o tema for alterado.
    *   Ler esta propriedade no `OnStartup` da aplicação para definir o tema inicial.

*   **Ícone da Aplicação:**
    *   A lógica de troca de tema deve também atualizar a propriedade `Icon` da `MainWindow` dinamicamente:
        *   **Light Mode:** `pack://application:,,,/SelectML-logo-short-light.ico`
        *   **Dark Mode:** `pack://application:,,,/SelectML-logo-short-dark.ico`

## 2. Logging Estruturado (Serilog)

**Requisito:** Registrar eventos de operação para auditoria e diagnósticos detalhados.

### Estratégia Técnica

*   **Instalação de Pacotes:**
    *   `Serilog`
    *   `Serilog.Sinks.File`
    *   `Serilog.Sinks.Console` (opcional, útil para debug em desenvolvimento)

*   **Configuração (App.xaml.cs):**
    *   Configurar o `Log.Logger` no método `OnStartup` antes de qualquer outra operação.
    *   **Configurações do Sink de Arquivo:**
        *   Caminho: `logs/log-.txt` (criará logs como `log-20231027.txt`).
        *   Rotação: `RollingInterval.Day`.
        *   Formato (`outputTemplate`): `"{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"`.
    *   Garantir que o Logger seja fechado corretamente no `OnExit` (`Log.CloseAndFlush()`).

*   **Pontos de Instrumentação (Onde logar):**

    1.  **Inicialização da App:**
        *   "Application Starting..."
        *   Configurações carregadas (sem expor senhas).
    2.  **Carregamento de Plugins:**
        *   "Loading plugins from..."
        *   "Plugin loaded: {PluginName}"
        *   "Error loading plugin: {ErrorMessage}"
    3.  **Monitoramento de Arquivos (FileSystemWatcher):**
        *   "File detected: {FileName}"
        *   "File locked, retrying... (Attempt {AttemptNumber})"
    4.  **Processamento (Parser):**
        *   "Parsing started for {FileName} using {ParserName}"
        *   "Parsing success: {MeasurementCount} measurements found."
        *   "Parsing failed for {FileName}: {Reason}"
    5.  **Ação do Usuário:**
        *   "User action: Send clicked."
        *   "User action: Cancel clicked."
        *   "Validation failed: {ValidationErrors}"
    6.  **Integração com Banco de Dados:**
        *   "Database connection successful."
        *   "Querying Station/ActiveRun for Batch: {BatchNumber}"
        *   "Station found: {StationName}, ActiveRun: {RunID}"
        *   "Station not found for IP/Host."
    7.  **Geração do Arquivo Final:**
        *   "CSV generated successfully at: {OutputPath}"
        *   "Error writing CSV: {ExceptionMessage}"

## 3. Checklist de Execução

Esta lista define a ordem recomendada de implementação.

### Logging (Fundação)
- [ ] Instalar pacotes NuGet do Serilog (`Serilog`, `Serilog.Sinks.File`, `Serilog.Sinks.Console`).
- [ ] Configurar `Log.Logger` globalmente em `App.xaml.cs` (OnStartup e OnExit).
- [ ] Instrumentar `MainViewModel` (eventos de UI e Estado).
- [ ] Instrumentar `PluginLoader` (carregamento de DLLs).
- [ ] Instrumentar `FileMonitorService` (detecção de arquivos).
- [ ] Instrumentar `DatabaseService` (erros de SQL e conexões).

### Temas (UI)
- [ ] Criar pasta `SelectML.Client/Themes`.
- [ ] Criar `Themes/Light.xaml` com Brushes padrão.
- [ ] Criar `Themes/Dark.xaml` com Brushes padrão (cores invertidas/escuras).
- [ ] Criar classe `ThemeService` (ou método helper) para gerenciar `MergedDictionaries`.
- [ ] Adicionar configuração `IsDarkMode` em `Properties.Settings`.
- [ ] Implementar lógica de inicialização para carregar tema salvo.
- [ ] Adicionar botão de Toggle na `MainWindow.xaml` (Header).
- [ ] Atualizar todos os controles na `MainWindow.xaml` para usar `DynamicResource` referenciando as chaves dos temas (ex: `{DynamicResource AppBackgroundBrush}`).
- [ ] Testar troca de tema em tempo real e verificação do ícone da janela.

## Critério de Sucesso
*   A aplicação inicia salvando logs em arquivo local rotacionado por dia.
*   É possível alternar entre temas Claro e Escuro sem reiniciar a aplicação.
*   O tema escolhido persiste após fechar e abrir o programa novamente.
*   O ícone da janela na barra de tarefas e cabeçalho reflete o tema atual.
