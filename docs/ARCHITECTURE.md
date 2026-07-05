# Documentação de Arquitetura SelectML

Este documento serve como a "Fonte da Verdade" técnica para o projeto SelectML (rebrand Protequality). Destina-se à equipe de engenharia e manutenção, detalhando decisões críticas de arquitetura, fluxo de dados e integrações na versão V1.2.2.

## 1. Diagrama de Componentes (Híbrido)

A arquitetura V1.2.2 suporta entrada dupla (Arquivos via Watcher e Serial via PortService), com plugins para formatos CSV, JSON e PDF.

```mermaid
graph TD
    subgraph "SelectML.Client (WPF)"
        View[MainWindow_View] -->|DataBinding| VM[MainViewModel_ViewModel]
        VM -->|Gerencia| FSW[FileSystemWatcher_Service]
        VM -->|Assina Eventos| SS[SerialPortService]
        VM -->|Utiliza| PL[PluginLoader_Service]
        VM -->|Persistência Config| CS[ConfigService]
        VM -->|Coordena| FLS[FileLifecycleService]
        VM -->|Consome| DB[DatabaseService]
        
        SS -->|Usa| STR[ISerialDeviceStrategy]
    end

    subgraph "SelectML.Core (Shared Contracts)"
        IMP[<< Interface >>\nIMachineParser]
        DTO[MeasurementData_DTO]
    end

    subgraph "Hardware & IO"
        RS232[Dispositivo Serial] -- Cabo --> SS
        CMM[CMM/Vici/Zeiss] -- Arquivo --> DISK[Pasta Monitorada]
    end

    FSW -.->|Detecta| DISK
    SS -.->|Lê| RS232
```

## 2. Fluxo de Dados (Data Flow)

O SelectML opera em modo **Híbrido**, processando dados de duas fontes distintas com estratégias de buffer e processamento adequadas.

### 2.1 Fluxo de Arquivo (Máquinas Automáticas)
1.  **Entrada**: `FileSystemWatcher` detecta o arquivo (CSV, JSON ou PDF) no diretório monitorado.
2.  **Parsing**: O plugin selecionado converte o arquivo para `MeasurementData`:
    - **CSV (ViciVision)**: Lê a primeira e última linhas.
    - **JSON (ViciVision X5)**: Lê a estrutura JSON do ciclo de medição e extrai limites de tolerância.
    - **PDF (Zeiss Calypso)**: Executa análise espacial via `PdfPig` para agrupar palavras em linhas físicas (evitando quebras do layout PDF), limpa caracteres LaTeX e detecta o número de casas decimais dinamicamente.
3.  **Modificação de Nome**: Se o `NameModifierMode` estiver em "Default", o sistema mescla os nomes das características com o nominal e tolerâncias (ex: `Ø10.00 ±0.05`).
4.  **Validação**: Consulta o SQL Server (`DatabaseService`) para associar a corrida/peça a uma estação e rotina. Se a estação for desconhecida, a UI exibe o modal de Seleção de Estação.
5.  **Roteamento**: Se `UseOutputDirectory` estiver ativo, o arquivo CSV final gerado é salvo na pasta `OutputDirectory`. Caso contrário, é gerado no destino padrão.
6.  **Ciclo de Vida**: O arquivo original é movido para o diretório de Backup.

### 2.2 Fluxo Serial (Paquímetros/Micrômetros) - "Buffer Reverso"
No fluxo serial, os dados chegam de forma incremental (medida por medida) e muitas vezes antes do operador selecionar a peça na UI.

1.  **Entrada**: `SerialPortService` recebe os bytes do hardware serial.
2.  **Parsing Imediato**: `ISerialDeviceStrategy` converte string bruta em valor numérico.
3.  **Buffer de Espera**:
    - Se o usuário **JÁ** selecionou uma peça na UI: A medida é associada à característica atual.
    - Se **NÃO** há peça selecionada: O valor entra no **"Buffer Reverso"** (fila em memória).
4.  **Flush**: Quando a peça é selecionada, o buffer reverso preenche as características na ordem de chegada.

## 3. Componentes Chave V1.2.2

### DatabaseService (SQL Server)
- **Conectividade Resiliente**: A string de conexão usa `Encrypt=false` por padrão para evitar falhas de handshake TLS em servidores locais mais antigos.
- **Consultas Otimizadas**: Busca de metadados da estação e rotinas baseada no `DbName` persistido, evitando varreduras globais no banco `master`.
- **Log de Consultas**: Logs estruturados para depuração das requisições de consulta de características e validação de estações.

### SerialPort & Strategies
- **SerialPortService**: Gerencia a comunicação física. Mantém portas seriais e buffers.
- **Name Modifier**: Lógica integrada na `MainViewModel` para formatar e padronizar o nome de exibição das características conforme as tolerâncias de engenharia.

### Design System
- Temas claro/escuro persistidos no `appsettings.json`.
- Melhorias na UI: Remoção de barras de rolagem desnecessárias em submenus e modal de seleção de estação otimizado.

## 4. Governança de Dados
- **Backup First**: Arquivos são copiados para a pasta `/Backup` antes de qualquer manipulação.
- **Limpeza Automática**: Configuração `DataRetentionDays` (default 30 dias) gerencia a retenção de backups antigos e logs.

## 5. Estratégia de Codificação (Encoding)
- **Serial/Arquivo Bruto**: Carregados usando `Encoding.Latin1` para garantir a integridade de caracteres como `Ø` e `µ`.
- **Saída CSV**: Gravação com `UTF8Encoding(true)` (com BOM) para compatibilidade imediata com o Microsoft Excel.

## 6. Extensibilidade (Plugins)
- Carregamento sob demanda via Reflection de arquivos `.dll` no diretório `/Plugins`.
- Contrato baseado na interface `IMachineParser` do `SelectML.Core`.
