# Correção de Bug: Crash Silencioso ao Acessar Conexão Serial (PlatformNotSupportedException)

**Versão Afetada:** 1.2.7  
**Tipo:** Correção de Bug (Bugfix)  
**Status:** Concluído  
**Data:** 03/10/2026  

---

## 1. Descrição do Problema e Sintomas

Ao executar a versão do **SelectML V1.2.7** instalada pelo release oficial do GitHub em ambiente Windows, a aplicação fechava instantaneamente (crash to desktop) de forma silenciosa sempre que o usuário tentava acessar a opção de menu **"Conexão Serial"**.

### Comportamento Observado:
* Nenhuma mensagem de erro ou aviso em tela era exibido para o usuário.
* O arquivo de log local (`logs/log-*.txt`) não continha o registro da falha, pois o processo era finalizado abruptamente pelo runtime do .NET antes de liberar o buffer do Serilog.
* No **Visualizador de Eventos (Event Viewer)** do Windows, dois eventos eram registrados sob o log de *Aplicativo* (`Application`):
  * **Evento 1026 (.NET Runtime):**
    ```text
    Application: SelectML.Client.exe
    CoreCLR Version: 8.0.2526.11203
    .NET Version: 8.0.25
    Description: The process was terminated due to an unhandled exception.
    Exception Info: System.PlatformNotSupportedException: System.IO.Ports is currently only supported on Windows.
       at System.IO.Ports.SerialPort.GetPortNames()
       at SelectML.Client.Services.Serial.SerialPortService.GetAvailablePorts()
       at SelectML.Client.ViewModels.SerialConfigViewModel.LoadPorts()
       at SelectML.Client.ViewModels.SerialConfigViewModel..ctor()
       at SelectML.Client.ViewModels.MainViewModel.ExecuteOpenSerialConfig(Object obj)
    ```
  * **Evento 1000 (Application Error):**
    Código de exceção `0xe0434352` (código de falha fatal por exceção CLR não capturada) no módulo `KERNELBASE.dll`.

---

## 2. Investigação e Causa Raiz

### 2.1 O Paradoxo da `PlatformNotSupportedException` no Windows
A mensagem `System.IO.Ports is currently only supported on Windows` parecia contraditória, visto que o software estava rodando exatamente no Windows 11 x64.

A investigação nos pacotes NuGet do .NET revelou a arquitetura do pacote `System.IO.Ports`:
1. **DLL de Referência / Placeholder (Any-CPU / Stub):** Localizada no pacote em `lib/net8.0/System.IO.Ports.dll` (~**36 KB**). Essa biblioteca não contém código nativo para comunicação com portas; todos os seus métodos públicos contêm apenas o lançamento estático de `PlatformNotSupportedException`.
2. **DLL de Implementação Real para Windows:** Localizada em `runtimes/win/lib/net8.0/System.IO.Ports.dll` (~**88 KB**). Esta sim contém o código Win32 para abrir handles `CreateFile` em portas COM e consultar o Registro do Windows (`HARDWARE\DEVICEMAP\SERIALCOMM`).

### 2.2 Falha no Empacotamento do Release V1.2.7
Durante o processo de build/publicação que antecedeu a criação do pacote pelo **Velopack** (`vpk pack`):
* A pasta `publish` (ou a saída da compilação) foi gerada sem o isolamento correto de RID (`-r win-x64`), ou herdou o binário de uma publicação portável/Any-CPU.
* Isso fez com que a DLL stub de **36 KB** fosse copiada para a raiz da aplicação instalada (`%LocalAppData%\SelectML\current\System.IO.Ports.dll`), enquanto a DLL funcional de 88 KB ficou aninhada em `runtimes\win\lib\net8.0\`.
* Pela regra padrão de sondagem de assemblies do CoreCLR, o .NET verifica primeiramente a pasta base do executável (`AppContext.BaseDirectory`). Encontrando o `System.IO.Ports.dll` de 36 KB ali, ele a carregou e ignorou a pasta `runtimes`.

### 2.3 Ausência de Captura Global no WPF Dispatcher
* No arquivo `App.xaml.cs`, não havia inscrição nos eventos `DispatcherUnhandledException` e `AppDomain.CurrentDomain.UnhandledException`.
* Com isso, qualquer exceção disparada a partir de comandos da interface gráfica (como o clique no MenuItem que acionou `OpenSerialConfigCommand`) resultava em finalização imediata do processo pelo sistema operacional.

---

## 3. Modificações e Soluções Implementadas

### 3.1 Garantia no Build do MSBuild ([SelectML.Client.csproj](file:///c:/Antigravity/SelectML/SelectML.Client/SelectML.Client.csproj))
Para impedir que qualquer compilação futura (seja Debug, Release ou Publish) coloque o stub de 36 KB na saída:
* Foi habilitado o atributo `GeneratePathProperty="true"` na referência do pacote `System.IO.Ports`.
* Foi criado um Target dedicado `EnsureWindowsSerialPortDll` executado após `Build` e `Publish`, que copia de forma mandatória a DLL de **88 KB** (`$(PkgSystem_IO_Ports)\runtimes\win\lib\net8.0\System.IO.Ports.dll`) sobre a raiz do diretório de saída:

```xml
<PackageReference Include="System.IO.Ports" Version="10.0.2" GeneratePathProperty="true" />

<Target Name="EnsureWindowsSerialPortDll" AfterTargets="Build;Publish">
  <Copy SourceFiles="$(PkgSystem_IO_Ports)\runtimes\win\lib\net8.0\System.IO.Ports.dll"
        DestinationFolder="$(TargetDir)"
        Condition="Exists('$(PkgSystem_IO_Ports)\runtimes\win\lib\net8.0\System.IO.Ports.dll')"
        OverwriteReadOnlyFiles="true" />
  <Copy SourceFiles="$(PkgSystem_IO_Ports)\runtimes\win\lib\net8.0\System.IO.Ports.dll"
        DestinationFolder="$(PublishDir)"
        Condition="'$(PublishDir)' != '' and Exists('$(PkgSystem_IO_Ports)\runtimes\win\lib\net8.0\System.IO.Ports.dll')"
        OverwriteReadOnlyFiles="true" />
</Target>
```

### 3.2 Resiliência com Fallback pelo Registro ([SerialPortService.cs](file:///c:/Antigravity/SelectML/SelectML.Client/Services/Serial/SerialPortService.cs))
* O método `GetAvailablePorts()` foi encapsulado em `try/catch`.
* Caso a API do `SerialPort.GetPortNames()` falhe por qualquer razão de driver ou plataforma, o serviço executa um **fallback nativo consultando diretamente o Registro do Windows** em `HKLM\HARDWARE\DEVICEMAP\SERIALCOMM`.
* No método `Connect()`, adicionou-se limpeza garantida (`Disconnect()`) no bloco `catch` para evitar instâncias presas em caso de erro na abertura.
* No método `Disconnect()`, desinscreve-se o manipulador de eventos `DataReceived` antes do descarte do objeto.

### 3.3 Proteção na Camada de VisualModels ([SerialConfigViewModel.cs](file:///c:/Antigravity/SelectML/SelectML.Client/ViewModels/SerialConfigViewModel.cs) e [MainViewModel.cs](file:///c:/Antigravity/SelectML/SelectML.Client/ViewModels/MainViewModel.cs))
* Em `SerialConfigViewModel.LoadPorts()`: Tratamento com `try/catch` para que qualquer anomalia ao listar portas seriais não derrube a tela.
* Em `SerialConfigViewModel.ExecuteConnect()`: A persistência do `AutoStartSerial` foi movida para executar **apenas se a conexão física for bem-sucedida**, impedindo que um estado corrompido seja salvo.
* Em `MainViewModel.ExecuteOpenSerialConfig()`: Tratamento de exceção com `Log.Error` e exibição de diálogo amigável (`MessageBox`) caso a janela falhe ao ser instanciada.

### 3.4 Captura Global de Falhas no [App.xaml.cs](file:///c:/Antigravity/SelectML/SelectML.Client/App.xaml.cs)
Foram adicionados os tratadores globais no ciclo de vida da aplicação:
```csharp
DispatcherUnhandledException += (s, args) =>
{
    Log.Fatal(args.Exception, "Exceção não tratada capturada na thread de UI (Dispatcher)");
    MessageBox.Show(
        $"Ocorreu um erro inesperado na aplicação:\n\n{args.Exception.Message}\n\nConsulte o arquivo de log para mais detalhes.",
        "Erro Inesperado - SelectML",
        MessageBoxButton.OK,
        MessageBoxImage.Error);
    args.Handled = true;
};

AppDomain.CurrentDomain.UnhandledException += (s, args) =>
{
    if (args.ExceptionObject is Exception ex)
    {
        Log.Fatal(ex, "Exceção fatal não tratada no AppDomain");
    }
};

TaskScheduler.UnobservedTaskException += (s, args) =>
{
    Log.Error(args.Exception, "Exceção não observada em tarefa assíncrona (TaskScheduler)");
    args.SetObserved();
};
```

---

## 4. Testes e Validações Realizadas

1. **Substituição no Ambiente Instalado:**
   * A DLL `System.IO.Ports.dll` presente em `%LocalAppData%\SelectML\current\` foi substituída pela versão Windows de 88.328 bytes.
2. **Teste de Consulta de Portas:**
   * Executou-se a listagem via PowerShell / .NET na máquina, que identificou e listou com sucesso as 10 portas seriais disponíveis no sistema (`COM3`, `COM4`, `COM5`, `COM6`, `COM8`, `COM9`, `COM20`, `COM21`, `COM22`, `COM23`).
3. **Teste de Compilação Automatizada:**
   * Executou-se `dotnet build SelectML.sln` e `dotnet publish` em diretório de teste.
   * Em ambos os testes, o Target `EnsureWindowsSerialPortDll` garantiu o arquivo final com exatos **88.328 bytes** na pasta raiz de saída.
