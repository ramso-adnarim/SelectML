# Mapa de Navegação da Base de Código (SelectML) - V1.2.2

> **Nota para IAs:** Este documento serve como um índice rápido ("Context Window Efficiency") para navegação no projeto. Consulte este mapa antes de perguntar "Onde está a lógica de X?".

## 📂 Estrutura de Diretórios e Responsabilidades

### `SelectML.Core`
> Biblioteca compartilhada de tipos e interfaces. Dependência mais baixa.
- **`MeasurementData.cs`**: DTO fundamental. Representa uma linha de medição (contendo características, valores e o dicionário `Tolerances` com nominais e limites superior/inferior de tolerância).
- **`IMachineParser.cs`**: Contrato que todo plugin de máquina deve implementar para parsear arquivos de texto/JSON/PDF.
- **`IDatabaseService.cs`**: Contrato para acesso a bancos de dados externos (ex: SQL Server para consultar lotes, rotinas, estações e características).

### `SelectML.Client`
> Consiste na aplicação WPF Principal (Rebrand parcial para Ramso Adnarim nas tags de publicação/empacotamento).
- **`App.xaml.cs`**: Entry point. Configura DI simples, temas, tratamento global de exceções e Velopack para atualização dinâmica.
- **`MainWindow.xaml`**: View principal. Contém o DataGrid, dashboard de status de conexões, monitoramento de banco de dados e seleção de modo modificador de nomes.
- **`ViewModels/MainViewModel.cs`**: "God Class" (por design MVVM simples). Orquestra:
    - **UI State**: Propriedades bindadas à View.
    - **Lógica de Negócios**: Validação, Buffer Reverso, Filtros, Arredondamento e Roteamento para Diretórios de Saída.
    - **Name Modifier**: Lógica de modificação de nomes de características (`ApplyNameModifier`) com base no nominal e tolerâncias (`NameModifierMode` persistido no arquivo de configuração).
    - **IO**: Gerencia o `FileSystemWatcher` para arquivos e assina eventos do `SerialPortService`.
- **`Services/Serial/SerialPortService.cs`**: Singleton. Gerencia a conexão física `SerialPort`. Lê bytes brutos, converte para string e delega o parsing para a `ISerialDeviceStrategy`.
- **`Services/Serial/Strategies/`**: Implementações de `ISerialDeviceStrategy` (ex: `UWaveStrategy`, `CustomSerialStrategy`). Convertem string bruta em `SerialMeasurement`.
- **`Services/FileLifecycleService.cs`**: Gerencia o ciclo de vida dos arquivos de entrada: Leitura -> Cópia para Backup -> Validação -> Deleção da Origem.
- **`Services/PluginLoader.cs`**: Usa Reflection para carregar DLLs da pasta de execução que implementam `IMachineParser`.
- **`Styles/`**: Dicionários de Recursos XAML (Cores, Fontes, Templates de Controles).

### `SelectML.Parsers.*`
> Projetos de Plugins específicos para máquinas.
- **`SelectML.Parsers.ViciVision`**: Parser para arquivos CSV gerados pelas máquinas ViciVision.
- **`SelectML.Parsers.ViciVisionJson`**: Parser para arquivos JSON gerados pela ViciVision X5 (com suporte a extração automática de tolerâncias).
- **`SelectML.Parsers.ZeissPdf`**: Parser para arquivos PDF da Zeiss Calypso com análise espacial resiliente de caixas de texto e limpeza de LaTeX.

---

## 🌳 Árvore de Dependências (Simplificada)

```mermaid
graph TD
    App --> MainWindow
    MainWindow --> MainViewModel
    MainViewModel --> ConfigService
    MainViewModel --> FileLifecycleService
    MainViewModel --> PluginLoader
    MainViewModel --> SerialPortService
    MainViewModel --> DatabaseService
    
    PluginLoader ..-> IMachineParser (Carrega Dinamicamente)
    
    SerialPortService --> ISerialDeviceStrategy (Selecionada via Config)
    SerialPortService --> SerialPort (System.IO.Ports)
    
    MainViewModel -- Ouve Eventos --> SerialPortService
```

---

## 📚 Glossário Técnico

| Termo | Definição |
| :--- | :--- |
| **Buffer Reverso** | Lógica na `MainViewModel`. Quando medições seriais chegam e o usuário ainda não definiu o "Nome da Peça", elas entram numa fila (`_serialBuffer`). Ao definir o nome, a fila é processada. |
| **Run / Corrida** | Uma sequência completa de medições para uma única peça. Geralmente mapeada para um arquivo de saída CSV único. |
| **U-WAVE** | Sistema de transmissão sem fio da Ramso Adnarim. No modo serial, envia dados no formato `01A+001.234CR`. |
| **Feature / Característica** | Uma única característica medida (ex: "Diâmetro Externo 1"). |
| **Velopack** | Framework usado para empacotamento, instalação e atualização automática da aplicação. |
| **NameModifierMode** | Modo que altera o nome das características adicionando dados geométricos e de tolerância (ex: adicionar sinal de diâmetro `Ø`, nominal e limites). |

---

## ⚡ Regras de Ouro (Golden Rules)

1.  **UI Thread**: O `SerialPort` dispara eventos em threads do ThreadPool. **SEMPRE** use `Application.Current.Dispatcher.Invoke` antes de tocar em qualquer propriedade ObservableCollection na ViewModel.
2.  **Encoding**: Use `System.Text.Encoding.Latin1` (ISO-8859-1) para comunicação serial padrão e leitura de arquivos legados de máquinas, a menos que especificado o contrário. UTF-8 pode quebrar o parsing de símbolos especiais (como `Ø`).
3.  **Configuração Quente**: Alterações no `appsettings.json` ou `custom_device_config.json` devem preferencialmente ser recarregadas sem reinício (Hot Reload), mas conexões seriais exigem reconexão (Disconnect -> Connect).
4.  **Plugins**: Devem ser colocados na pasta `Plugins/` ao lado do executável para serem carregados dinamicamente.
5.  **Output Directory Routing**: Quando habilitado, o sistema roteia os arquivos CSV de saída gerados para uma pasta específica configurada (`OutputDirectory`), em vez de apenas no diretório de destino padrão.
6.  **Precisão de Decimais**: O parser do Zeiss PDF analisa a precisão de decimais presente no próprio PDF e a aplica para o arredondamento GMS.
