# Mapa de Navegação da Base de Código (SelectML) - V1.1.0

> **Nota para IAs:** Este documento serve como um índice rápido ("Context Window Efficiency") para navegação no projeto. Consulte este mapa antes de perguntar "Onde está a lógica de X?".

## 📂 Estrutura de Diretórios e Responsabilidades

### `SelectML.Core`
> Biblioteca compartilhada de tipos e interfaces. Dependência mais baixa.
- **`MeasurementData.cs`**: DTO fundamental. Representa uma linha de medição (ex: Característica, Valor, Tolerâncias).
- **`IMachineParser.cs`**: Contrato que todo plugin de máquina deve implementar para parsear arquivos de texto.
- **`IDatabaseService.cs`**: Contrato para acesso a bancos de dados externos (ex: SQL Server para validação de `PartName`).

### `SelectML.Client`
> Aplicação WPF Principal.
- **`App.xaml.cs`**: Entry point. Configura DI simples, temas e tratamento global de exceções.
- **`MainWindow.xaml`**: View principal. Contém o DataGrid, Dashboard de Status e controles manuais.
- **`ViewModels/MainViewModel.cs`**: "God Class" (por design MVVM simples). Orquestra:
    - **UI State**: Propriedades bindadas à View.
    - **Lógica de Negócios**: Validação, Buffer Reverso, Filtros.
    - **IO**: Gerencia o `FileSystemWatcher` para arquivos e assina eventos do `SerialPortService`.
- **`Services/Serial/SerialPortService.cs`**: Singleton. Gerencia a conexão física `SerialPort`. Lê bytes brutos, converte para string e delega o parsing para a `ISerialDeviceStrategy`.
- **`Services/Serial/Strategies/`**: Implementações de `ISerialDeviceStrategy` (ex: `UWaveStrategy`, `CustomSerialStrategy`). Convertem string bruta em `SerialMeasurement`.
- **`Services/FileLifecycleService.cs`**: Gerencia o ciclo de vida dos arquivos de entrada: Leitura -> Cópia para Backup -> Validação -> Deleção da Origem.
- **`Services/PluginLoader.cs`**: Usa Reflection para carregar DLLs da pasta de execução que implementam `IMachineParser`.
- **`Styles/`**: Dicionários de Recursos XAML (Cores, Fontes, Templates de Controles).

### `SelectML.Parsers.*`
> Projetos de Plugins específicos para máquinas.
- **`ViciX5Parser.cs`**: Exemplo de implementação de parser para máquinas ViciVision (formato CSV/TXT complexo).

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
| **Run** | Uma sequência completa de medições para uma única peça. Geralmente mapeada para um arquivo de saída CSV único. |
| **U-WAVE** | Sistema de transmissão sem fio da Protequality. No modo serial, envia dados no formato `01A+001.234CR`. |
| **Feature** | Uma única característica medida (ex: "Diâmetro Externo 1"). |
| **Velopack** | Framework usado para empacotamento, instalação e atualização automática da aplicação. |

---

## ⚡ Regras de Ouro (Golden Rules)

1.  **UI Thread**: O `SerialPort` dispara eventos em threads do ThreadPool. **SEMPRE** use `Application.Current.Dispatcher.Invoke` antes de tocar em qualquer propriedade ObservableCollection na ViewModel.
2.  **Encoding**: Use `System.Text.Encoding.Latin1` (ISO-8859-1) para comunicação serial padrão, a menos que especificado o contrário. UTF-8 pode quebrar protocolos antigos.
3.  **Configuração Quente**: Alterações no `appsettings.json` ou `custom_device_config.json` devem preferencialmente ser recarregadas sem reinício (Hot Reload), mas conexões seriais exigem reconexão (Disconnect -> Connect).
4.  **Plugins**: Devem ser "Side-by-side" (na mesma pasta do executável) para serem carregados.
