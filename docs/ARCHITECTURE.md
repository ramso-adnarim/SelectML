# Documentação de Arquitetura SelectML

Este documento serve como a "Fonte da Verdade" técnica para o projeto SelectML. Destina-se à equipe de engenharia e manutenção, detalhando decisões críticas de arquitetura, concorrência e integração.

## 1. Diagrama de Componentes

A arquitetura segue o padrão MVVM, com separação clara entre a camada de apresentação (Client) e o núcleo de domínio (Core), estendida dinamicamente via Plugins.

```mermaid
graph TD
    subgraph "SelectML.Client (WPF)"
        View[MainWindow_View] -->|DataBinding| VM[MainViewModel_ViewModel]
        VM -->|Gerencia| FSW[FileSystemWatcher_Service]
        VM -->|Utiliza| PL[PluginLoader_Service]
        VM -->|Persistência| CS[ConfigService]
    end

    subgraph "SelectML.Core (Shared Contracts)"
        IMP[<< Interface >>\nIMachineParser]
        DTO[MeasurementData_DTO]
    end

    subgraph "Plugins (External Assemblies)"
        PV[SelectML.Parsers.ViciVision.dll] -.->|Implementa| IMP
        PO[Outros Parsers...] -.->|Implementa| IMP
    end

    PL -->|Reflection/Load| PV
    FSW -->|Event:Created| VM
```

## 2. Modelo de Concorrência e Threading

O SelectML opera em um ambiente multi-thread para garantir que a Interface do Usuário (UI) permaneça responsiva enquanto processa I/O de arquivos potencialmente bloqueantes.

### 2.1 FileSystemWatcher e Thread Pool
O `FileSystemWatcher` (instanciado em `MainViewModel`) monitora o diretório de entrada.
- **Evento `Created`**: É disparado em uma thread secundária do *Thread Pool*, **não** na UI Thread.
- **Consequência**: Qualquer tentativa de acessar propriedades vinculadas à UI (como `ObservableCollection`) diretamente dentro deste manipulador causará uma `InvalidOperationException`.

### 2.2 Marshalling para UI Thread
Para atualizar a interface com os resultados do processamento, utilizamos o `Dispatcher`:

```csharp
System.Windows.Application.Current.Dispatcher.Invoke(() =>
{
    // Código executado na UI Thread
    PartName = data.PartName;
    MeasuredResults.Add(...);
});
```

Isso garante que as atualizações visuais ocorram de forma segura e síncrona com o loop de renderização do WPF.

### 2.3 Estratégia de "Retry" (File Locking)
Máquinas industriais frequentemente mantêm o arquivo de saída bloqueado (Lock) enquanto escrevem os dados. Tentar ler o arquivo imediatamente após o evento `Created` resulta em exceção de I/O.

Implementamos um padrão de **Spin-Wait** no método `WaitForFileAccess`:
1.  **Tentativas**: Loop de até 10 tentativas (`timeoutSeconds * 2`).
2.  **Delay**: `Task.Delay(500)` (500ms) entre tentativas.
3.  **Verificação**: Tenta abrir o arquivo com `FileMode.Open` e `FileShare.ReadWrite`.
4.  **Validação**: Verifica se `stream.Length > 0` para evitar leituras de arquivos vazios (comuns durante a criação inicial).

## 3. Estratégia de Codificação (Encoding Strategy)

A correta interpretação de caracteres é crítica neste domínio devido à mistura de hardware legado e requisitos de integração modernos.

### 3.1 Entrada (Parsers de Máquina)
- **Padrão**: `Encoding.Latin1` (ISO-8859-1).
- **Justificativa**: Máquinas de medição legadas e sistemas embarcados frequentemente utilizam tabelas de caracteres ANSI estendidas para representar símbolos de engenharia (ex: Ø para diâmetro, ° para graus, µ para mícrons). O uso de UTF-8 na leitura resultaria em corrupção desses símbolos (mojibake).

### 3.2 Saída (Arquivos CSV Padronizados)
- **Padrão**: `new UTF8Encoding(true)` (UTF-8 com BOM).
- **Justificativa**: O sistema gera arquivos CSV para consumo por terceiros e importação no Excel.
    - O **BOM (Byte Order Mark)** é estritamente necessário para que o Microsoft Excel detecte automaticamente a codificação e exiba corretamente os caracteres especiais sem configuração manual do usuário.

## 4. Mecânica do Sistema de Plugins

A extensibilidade é garantida através de carregamento dinâmico de assemblies, permitindo adicionar suporte a novas máquinas sem recompilar o cliente principal.

### 4.1 Carregamento (PluginLoader)
O serviço `PluginLoader` utiliza Reflection para inspecionar DLLs na pasta `\Plugins`:
1.  **Carregamento**: `Assembly.LoadFrom(dllPath)`.
2.  **Descoberta**: Busca tipos que implementam `SelectML.Core.IMachineParser`, ignorando interfaces e classes abstratas.
3.  **Instanciação**: Utiliza `Activator.CreateInstance(type)` para criar objetos parser.

### 4.2 Isolamento de Falhas
O carregamento de cada DLL é envolvido em um bloco `try-catch`. Se um plugin falhar ao carregar (ex: falta de dependência), ele é logado e ignorado, não impedindo a inicialização da aplicação ou o carregamento de outros plugins válidos.

## 5. Estrutura de Dados e Persistência

### 5.1 DTO Universal (MeasurementData)
A classe `MeasurementData` (no projeto Core) atua como o contrato de dados único entre os plugins e a aplicação.
- Ela normaliza os dados brutos da máquina em uma estrutura previsível (`PartName`, `BatchNumber`, `Dictionary<string, double>`).
- Isso desacopla a lógica de exibição e exportação CSV dos formatos proprietários das máquinas.

### 5.2 Configuração (appsettings.json)
O `ConfigService` gerencia a persistência das configurações do usuário em um arquivo `appsettings.json` local.
- **Esquema**:
  ```json
  {
    "WatchDirectory": "C:\\Caminho\\Monitorado",
    "LastPluginName": "Vici Vision M1"
  }
  ```
- O serviço utiliza `System.Text.Json` para serialização/deserialização. O carregamento é resiliente a falhas (retorna configuração padrão se o arquivo não existir ou estiver corrompido).
