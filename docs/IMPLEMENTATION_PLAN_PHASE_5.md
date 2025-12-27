# Plano de Implementação - Fase 5: Validação de Integridade e UX

Este documento detalha a estratégia técnica para as novas funcionalidades de "Detecção Antecipada" e "Validação de Características" no projeto SelectML. O objetivo é fornecer feedback visual imediato ao operador e garantir a integridade dos dados antes do envio.

## 1. Detecção Antecipada de Estação (Early Detection)

**Requisito:** Assim que o Parser identificar o `BatchNumber` (Lote), a aplicação deve consultar o SQL imediatamente para descobrir o `StationName`, sem aguardar o clique no botão "Enviar".

### Alterações na UI (Client)
*   **Novo Controle:** Adicionar um `TextBlock` logo abaixo do campo "Código da Corrida/Lote" na `MainWindow`.
*   **Estilo:**
    *   `FontSize`: 10 ou 11.
    *   `Foreground`: Cinza ou a cor de destaque do tema atual.
    *   `Text`: "Estação Detectada: [Nome da Estação]".
    *   **Binding:** `DetectedStationName`.

### Alterações na ViewModel (Client)
*   **Propriedade:** Adicionar `DetectedStationName` (string) observável em `MainViewModel`.
*   **Fluxo `OnFileCreated`:**
    *   Logo após o parse bem-sucedido (`data.IsValid`), chamar assincronamente `DatabaseService.GetStationNameAsync(data.BatchNumber)`.
    *   Atualizar `DetectedStationName` com o resultado.
    *   Essa chamada deve ser feita antes de liberar a UI para o estado "Pending Action".

## 2. Validação Cruzada de Características (Feature Validation)

**Requisito:** Validar se as características lidas do arquivo (ex: "Diametro A") existem na tabela `dbo.ActiveRun` para aquele Lote específico.

### Lógica de Dados (Core)
*   **Interface `IDatabaseService`:** Adicionar método `Task<List<string>> GetFeaturesForRunAsync(string batchNumber)`.
*   **Implementação `DatabaseService`:** Implementar a consulta SQL que retorna os nomes das características (`FeatureName`) associadas ao `BatchNumber`.

### Lógica de Comparação (Client)
*   **`MainViewModel`:**
    *   Ao processar o arquivo, chamar `GetFeaturesForRunAsync`.
    *   Comparar as chaves do dicionário de resultados (`data.Results.Keys`) com a lista retornada pelo banco.
*   **Classe `ResultItem`:**
    *   Adicionar propriedade `bool IsRecognized` (padrão `true`).
    *   Durante o preenchimento da coleção `MeasuredResults`, definir `IsRecognized = false` se a característica não estiver na lista do banco.

### Feedback Visual (Client)
*   **DataGrid:**
    *   Atualizar o `DataGrid` na `MainWindow` para reagir à propriedade `IsRecognized`.
    *   **Style/DataTrigger:**
        *   Se `IsRecognized == false`:
            *   Alterar `Background` da linha para Vermelho Claro (`#FFCCCC` no tema Light, `#550000` no tema Dark).
            *   Adicionar `ToolTip`: "Característica não cadastrada no SQL".

## 3. Confirmação de Envio com Supressão (User Confirmation)

**Requisito:** Se houver itens não reconhecidos ao clicar em "Enviar", exigir confirmação do usuário, com opção de "Não perguntar novamente".

### Cenário de Uso
1.  Usuário clica em "Enviar".
2.  Sistema verifica se há algum `ResultItem` com `IsRecognized == false`.
3.  Se houver **E** a configuração de supressão não estiver ativa:
    *   Exibir `ConfirmationWindow`.

### UX Avançada (Custom Dialog)
*   **Janela `ConfirmationWindow.xaml`:**
    *   Mensagem: "Existem características não reconhecidas no banco de dados. Deseja enviar mesmo assim?"
    *   Botões: "Enviar", "Cancelar".
    *   **Checkbox:** "Não perguntar novamente para este tipo de aviso".
*   **Persistência:**
    *   Se o usuário marcar o checkbox e confirmar, salvar `Properties.Settings.Default.SuppressFeatureWarning = true`.

### Fluxo de Envio (`ExecuteSend`)
*   Verificar `MeasuredResults.Any(r => !r.IsRecognized)`.
*   Verificar `!Properties.Settings.Default.SuppressFeatureWarning`.
*   Se ambos verdadeiros -> Abrir Dialog.
    *   Se Dialog for confirmado -> Prosseguir com envio.
    *   Se Dialog for cancelado -> Abortar.
*   Se falsos -> Prosseguir com envio direto.

---

## Checklist de Execução

A implementação seguirá a ordem abaixo:

1.  [ ] **Core:** Atualizar `IDatabaseService` com `GetFeaturesForRunAsync`.
2.  [ ] **Client (Services):** Implementar `GetFeaturesForRunAsync` em `DatabaseService`.
3.  [ ] **Client (Model):** Atualizar `ResultItem` com a propriedade `IsRecognized`.
4.  [ ] **Client (ViewModel):** Refatorar `OnFileCreated` em `MainViewModel` para chamar as validações (Estação e Features) e popular `DetectedStationName`.
5.  [ ] **Client (UI):** Adicionar `TextBlock` de estação detectada e Estilos/Triggers no `DataGrid` para linhas inválidas.
6.  [ ] **Client (Dialog):** Criar `ConfirmationWindow` com suporte a checkbox de supressão e persistência em `Settings`.
7.  [ ] **Client (Logic):** Atualizar `ExecuteSend` para contemplar a lógica de confirmação e supressão.
