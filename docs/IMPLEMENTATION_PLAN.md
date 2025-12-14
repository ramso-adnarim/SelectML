# Plano de Implementação - Fase 2: Human-in-the-Loop & Roteamento Inteligente

Este documento descreve o plano técnico para a implementação das funcionalidades da Fase 2 do SelectML. As mudanças visam introduzir validação humana no processo de medição e roteamento dinâmico de arquivos de saída baseado em dados do banco de dados.

## 1. Funcionalidade A: Fluxo de Validação Manual ("Human-in-the-loop")

### Descrição
Atualmente, o sistema processa e salva os arquivos automaticamente. O novo fluxo exige que o operador valide os dados na tela antes de salvar (Enviar) ou descartar (Cancelar).

### Novo Fluxo de Trabalho
1.  **Detecção**: `FileSystemWatcher` detecta novo arquivo.
2.  **Processamento**: Parser lê o arquivo.
3.  **Apresentação**: Dados são exibidos na UI (`MainViewModel`).
4.  **Espera**: O sistema entra em estado "Aguardando Ação" (`IsPendingAction = true`).
    -   Botões "Enviar" e "Cancelar" tornam-se ativos.
    -   Monitoramento de novos arquivos pode ser pausado ou ignorado para evitar sobrescrita acidental (Decisão: Ignorar novos eventos enquanto pendente).
5.  **Ação do Usuário**:
    -   **Enviar**: Gera o CSV final, limpa a tela, retoma monitoramento.
    -   **Cancelar**: Limpa a tela, descarta dados, retoma monitoramento.

### Impacto Técnico

#### `SelectML.Client/ViewModels/MainViewModel.cs`
-   **Propriedades**:
    -   Adicionar `private bool _isPendingAction;` e propriedade pública correspondente.
    -   Vincular a visibilidade/habilitação dos botões "Enviar" e "Cancelar" a esta propriedade.
-   **Métodos**:
    -   `OnFileCreated`:
        -   Adicionar verificação: `if (IsPendingAction) return;` (ignora novos arquivos se ocupado).
        -   Remover chamada direta para `GenerateOutputCsv`.
        -   Definir `IsPendingAction = true` após carregar os dados.
        -   Atualizar `StatusMessage` para "Aguardando verificação do operador...".
    -   `ExecuteSend`:
        -   Chamar `GenerateOutputCsv(_currentData)`.
        -   Limpar dados da UI (`MeasuredResults.Clear()`, `PartName = null`, etc.).
        -   Definir `IsPendingAction = false`.
        -   Atualizar `StatusMessage` para "Monitorando...".
    -   `ExecuteCancel`:
        -   Limpar dados da UI.
        -   Definir `IsPendingAction = false`.
        -   Atualizar `StatusMessage` para "Operação cancelada. Monitorando...".
-   **Estado**: Precisa armazenar o objeto `MeasurementData` atual em um campo privado (`private MeasurementData _currentData;`) para ser usado pelo `ExecuteSend`.

---

## 2. Funcionalidade B: Roteamento Dinâmico via SQL Server

### Descrição
O diretório de saída dos arquivos CSV será determinado dinamicamente consultando um banco de dados SQL Server. O sistema usará o `BatchNumber` (Lote) para encontrar o `StationName` (Nome da Estação).

### Requisitos de Dados
-   **Tabelas**: `dbo.ActiveRun` (contém BatchNumber), `dbo.Station` (contém StationName).
-   **Input**: `BatchNumber` (do parser).
-   **Output**: `StationName`.
-   **Fallback**: Se não encontrado ou erro, usar pasta `Unidentified`.

### Impacto Técnico

#### Dependências
-   Adicionar pacote NuGet `Microsoft.Data.SqlClient` ao projeto `SelectML.Client`.

#### `SelectML.Core`
-   Criar interface `IDatabaseService` (opcional, para abstração) ou definir contrato direto. Para manter simplicidade e estrutura atual, definiremos o contrato.
    ```csharp
    public interface IDatabaseService
    {
        string GetStationNameByBatch(string batchNumber);
    }
    ```

#### `SelectML.Client/Services`
-   **AppConfig.cs**: Adicionar propriedade `public string ConnectionString { get; set; }`.
-   **DatabaseService.cs**: Nova classe implementando a lógica de acesso a dados.
    -   Método `GetStationNameByBatch(string batchNumber)`.
    -   Query SQL (exemplo conceitual):
        ```sql
        SELECT s.StationName
        FROM dbo.ActiveRun r
        JOIN dbo.Station s ON r.StationId = s.Id
        WHERE r.BatchNumber = @BatchNumber
        ```
    -   Tratamento de erros (try-catch, log, retornar null se falhar).

#### `SelectML.Client/ViewModels/MainViewModel.cs`
-   Injetar/Instanciar `DatabaseService`.
-   Atualizar `GenerateOutputCsv`:
    -   Obter `StationName` via `DatabaseService`.
    -   Definir subdiretório: `StationName` (se encontrado) ou `"Unidentified"`.
    -   Caminho final: `Path.Combine(WatchDirectory, subDirectory, fileName)`.
    -   Garantir que o diretório destino exista (`Directory.CreateDirectory`).

---

## 3. Checklist de Execução

Esta lista define a ordem de implementação para garantir estabilidade.

### Preparação e Infraestrutura
- [ ] 1. Instalar pacote NuGet `Microsoft.Data.SqlClient` no projeto `SelectML.Client`.
- [ ] 2. Atualizar `AppConfig` em `SelectML.Client/Services/AppConfig.cs` para incluir `ConnectionString`.
- [ ] 3. Criar `IDatabaseService` em `SelectML.Core` (para manter contratos no Core).
- [ ] 4. Implementar `DatabaseService` em `SelectML.Client/Services` com a consulta SQL.

### Funcionalidade A: Human-in-the-Loop
- [ ] 5. Refatorar `MainViewModel` para armazenar `_currentData` (MeasurementData) em campo privado.
- [ ] 6. Implementar propriedades de estado `IsPendingAction` e comandos `Send`/`Cancel` com lógica de habilitação na UI.
- [ ] 7. Alterar `OnFileCreated` para povoar a UI e pausar (não salvar automaticamente).
- [ ] 8. Implementar lógica de `ExecuteSend` (Salvar e Limpar) e `ExecuteCancel` (Limpar apenas).

### Funcionalidade B: Roteamento Dinâmico
- [ ] 9. Atualizar método `GenerateOutputCsv` em `MainViewModel` para utilizar o `DatabaseService`.
- [ ] 10. Implementar lógica de fallback para pasta `Unidentified` caso o banco não retorne resultados.

### Validação Final
- [ ] 11. Testar fluxo completo: Arquivo -> UI (Espera) -> Consulta SQL (Simulada ou Real) -> Salvar em Pasta Dinâmica.
