# Plano de Implementação - Fase 6: Governança de Arquivos e Manutenção

## Visão Geral
Esta fase visa solucionar o acúmulo de arquivos processados na pasta de monitoramento e garantir a integridade dos dados através de backups. Implementaremos um ciclo de vida rigoroso para os arquivos de entrada e uma rotina de manutenção (housekeeping) para logs e backups antigos.

---

## 1. Componentes Afetados

*   **SelectML.Core/AppConfig.cs**: Adição da propriedade `DataRetentionDays`.
*   **SelectML.Client/Services/FileLifecycleService.cs** (Novo): Responsável por encapsular as operações de IO seguras (Copy, Verify, Delete, Cleanup).
*   **SelectML.Client/ViewModels/MainViewModel.cs**: Integração do novo fluxo de limpeza logo após o parsing e chamada da rotina de manutenção na inicialização.
*   **SelectML.Client/App.xaml.cs**: Configuração e injeção do serviço de limpeza.

---

## 2. Fluxo de Backup e Limpeza de Entrada

**Objetivo:** Garantir que a pasta de monitoramento (`WatchDirectory`) contenha apenas arquivos pendentes, movendo arquivos processados com sucesso para uma pasta de backup segura.

**Lógica (FileLifecycleService):**
O método `ArchiveInputFile(string filePath, string watchDirectory)` executará a seguinte sequência atômica:

1.  Definir diretório de destino: `[watchDirectory]\Backup`.
2.  **Copiar** o arquivo original para o destino (sobrescrevendo se necessário, para garantir a versão mais recente).
3.  **Verificar** se o arquivo de destino existe e possui o mesmo tamanho (Length) do original.
4.  Se a verificação passar: **Deletar** o arquivo original da `WatchDirectory`.
5.  Se falhar: Lançar exceção e **não** deletar o original (o sistema tentará processar novamente ou o usuário intervirá).

**Integração no MainViewModel:**
No método `OnFileCreated`, imediatamente após `SelectedParser.Parse(e.FullPath)` retornar `data.IsValid = true`:
```csharp
if (data.IsValid)
{
   // Novo passo: Arquivamento Seguro
   string newPath = _fileLifecycleService.ArchiveInputFile(e.FullPath, WatchDirectory);

   // Continua o fluxo normal...
}
```

---

## 3. Rotina de Retenção de Dados (Housekeeping)

**Objetivo:** Remover backups e logs antigos para evitar que o disco encha, respeitando a configuração de retenção.

**Configuração (AppConfig):**
*   `int DataRetentionDays { get; set; } = 30;`

**Lógica (FileLifecycleService):**
O método `PerformCleanupAsync(string watchDirectory, int retentionDays)` será executado em uma Task secundária:

1.  Calcular data limite: `DateTime.Now.AddDays(-retentionDays)`.
2.  **Limpeza de Backups:**
    *   Varrer `[watchDirectory]\Backup`.
    *   Para cada arquivo, se `CreationTime < dataLimite`, deletar.
3.  **Limpeza de Logs:**
    *   Identificar pasta de logs (padrão `logs/` relativo à aplicação).
    *   Aplicar a mesma regra de data.

**Execução:**
O `MainViewModel` chamará este método no final de `LoadConfiguration` ou ao iniciar o Watcher, utilizando `Task.Run` para não bloquear a UI.

---

## 4. Estrutura de Diretórios Final

O sistema garantirá a seguinte estrutura:

*   **Entrada (Watch):** `C:\Medicoes\` (Monitorado, deve ficar vazio após processamento).
*   **Backup (Archive):** `C:\Medicoes\Backup\` (Cópia fiel do original).
*   **Saída (Output):** `C:\Medicoes\Output\[NomeEstacao]\` (CSV Final gerado).

---

## 5. Checklist de Execução

1.  [ ] **Atualizar AppConfig**: Adicionar `DataRetentionDays` com valor padrão 30.
2.  [ ] **Criar FileLifecycleService**:
    *   Implementar `ArchiveInputFile` (Copy -> Verify -> Delete).
    *   Implementar `PerformCleanupAsync` (Delete if Old).
3.  [ ] **Integrar no MainViewModel (Fluxo de Parse)**:
    *   Instanciar o serviço.
    *   Chamar `ArchiveInputFile` logo após `Parse()` ter sucesso.
    *   Tratar erros (se falhar o backup, abortar o processo e logar erro).
4.  [ ] **Integrar no MainViewModel (Inicialização)**:
    *   Chamar `PerformCleanupAsync` ao carregar a configuração ou iniciar o monitoramento.
5.  [ ] **Testes Manuais**:
    *   Verificar se o arquivo some da pasta de entrada e aparece na pasta Backup.
    *   Verificar se a UI continua responsiva.
    *   Simular arquivos antigos (alterando data do sistema ou arquivo) e verificar se são deletados na inicialização.
