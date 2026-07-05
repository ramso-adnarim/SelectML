# Plano de Implementação - Fase 2: Refinamento Operacional e Estética

**Autor:** Senior Front-End Engineer & UX Designer
**Data:** 25/05/2024
**Status:** Planejado

Este documento detalha as especificações técnicas para a Fase 2 do projeto SelectML, focando na melhoria da experiência do usuário (UX), prevenção de erros operacionais e persistência de estado da aplicação.

---

## 1. Validação Obrigatória de Metadados (Regra de Negócio)

### Problema
O sistema atual permite o acionamento do comando "Enviar" mesmo se os campos críticos `PartName` e `BatchNumber` estiverem vazios (ex: falha parcial do Parser). Isso pode gerar registros inválidos ou corrompidos no banco de dados/CSV.

### Solução Técnica
Implementar uma validação estrita no `CanExecute` do comando `SendCommand`.

#### Alterações no `MainViewModel.cs`
1.  **Atualizar `CanExecuteAction`**:
    *   A lógica atual verifica apenas `IsPendingAction`.
    *   Nova lógica: `return IsPendingAction && !string.IsNullOrWhiteSpace(PartName) && !string.IsNullOrWhiteSpace(BatchNumber);`.
2.  **Notificação de Propriedade**:
    *   As propriedades `PartName` e `BatchNumber` devem chamar `SendCommand.RaiseCanExecuteChanged()` (ou invalidar o CommandManager) ao serem alteradas.

#### Alterações no `MainWindow.xaml`
1.  **Edição de Campos**:
    *   Remover `IsReadOnly="True"` dos TextBoxes de `PartName` e `BatchNumber`.
    *   Configurar `IsReadOnly` via Binding para garantir que só sejam editáveis durante a revisão.
    *   Binding sugerido: `IsReadOnly="{Binding IsPendingAction, Converter={StaticResource InverseBoolConverter}}"` (necessário criar/verificar o converter).
    *   *Alternativa*: Deixar sempre editável, mas só efetivo no envio. Para melhor UX, o bloqueio visual durante o monitoramento (idle) é preferível.

---

## 2. Comportamento Condicional dos Campos de Conexão SQL

### Contexto
A configuração de credenciais SQL deve seguir uma lógica de bloqueio hierárquica para evitar edições durante o monitoramento e conflitos com a Autenticação Windows.

### Lógica de Bloqueio
A propriedade `IsEnabled` dos campos **Login** (`DbUser`) e **Senha** (`DbPassword`) deve obedecer à regra:
`IsEnabled = (Monitoramento Parado) E (Autenticação Windows Desativada)`

### Implementação

#### Alterações no `MainViewModel.cs`
1.  **Nova Propriedade Calculada**: `IsSqlCredentialsEnabled`.
    ```csharp
    public bool IsSqlCredentialsEnabled => !IsMonitoring && !DbUseWindowsAuth;
    ```
    *Nota: `IsMonitoring` no contexto de configuração é equivalente a `IsConfigLocked`.*
2.  **Atualização de Dependências**:
    *   No `set` de `IsConfigLocked` (ou `IsConfigEnabled`), disparar `OnPropertyChanged(nameof(IsSqlCredentialsEnabled))`.
    *   No `set` de `DbUseWindowsAuth`, disparar `OnPropertyChanged(nameof(IsSqlCredentialsEnabled))`.

#### Alterações no `MainWindow.xaml`
1.  **Bindings**:
    *   Atualizar `IsEnabled` do TextBox `DbUser` e PasswordBox `DbPasswordBox` para `{Binding IsSqlCredentialsEnabled}`.

---

## 3. Feedback Visual "Online" no Painel Recolhido

### Design
Quando o Expander de configuração estiver recolhido, o operador precisa saber se o sistema está "Online" (Monitorando) sem precisar expandir o painel.

### Implementação XAML (`MainWindow.xaml`)

1.  **Customizar Header do Expander**:
    *   Substituir o texto simples "Configuração do Monitoramento" por um Grid contendo o título e o indicador de status.
2.  **Indicador Visual**:
    *   Adicionar um elemento (ex: `StackPanel`) alinhado à direita dentro do Header.
    *   **Conteúdo**:
        *   `Ellipse` (Verde, 10x10).
        *   `TextBlock` "Online" (Verde, Margin Left 5).
    *   **Visibilidade**:
        *   Binding: `Visibility="{Binding IsMonitoring, Converter={StaticResource BooleanToVisibilityConverter}}"`.
3.  **Animação ("Heartbeat")**:
    *   Utilizar `Style.Triggers` na `Ellipse`.
    *   Criar um `Storyboard` que altera a `Opacity` de 1.0 para 0.4 em loop (`AutoReverse="True"`, `RepeatBehavior="Forever"`).

---

## 4. Persistência de Estado da Janela (Window Placement)

### Requisito
A aplicação deve restaurar a posição e tamanho da janela entre sessões.

### Técnica: `Properties.Settings`

#### Configuração
1.  **Verificar/Criar `Properties/Settings.settings`**:
    *   Garantir que o arquivo de configurações do usuário exista no projeto `SelectML.Client`.
    *   Adicionar as chaves (Escopo: User):
        *   `WindowTop` (double)
        *   `WindowLeft` (double)
        *   `WindowWidth` (double)
        *   `WindowHeight` (double)
        *   `WindowState` (WindowState - ou int/string)

#### Implementação (`MainWindow.xaml.cs`)
1.  **Evento `Closing`**:
    *   Salvar as propriedades atuais da janela em `Settings.Default`.
    *   Chamar `Settings.Default.Save()`.
    *   *Cuidado*: Se `WindowState` for `Maximized`, salvar as coordenadas de `RestoreBounds`.
2.  **Construtor ou `SourceInitialized`**:
    *   Ler as configurações.
    *   Validar se as coordenadas estão dentro da área visível dos monitores (para evitar janela "perdida" fora da tela).
    *   Aplicar os valores à janela.

---

## Checklist de Execução

### Configuração e Infraestrutura
- [ ] Verificar e criar (se necessário) a estrutura `Properties/Settings.settings` no projeto Client.
- [ ] Criar/Validar `InverseBooleanConverter` e `BooleanToVisibilityConverter` em `Converters/`.

### ViewModel (`MainViewModel.cs`)
- [ ] Implementar propriedade `IsSqlCredentialsEnabled` com a lógica de notificação.
- [ ] Refatorar `CanExecuteAction` para validar `PartName` e `BatchNumber`.
- [ ] Adicionar chamadas de `InvalidateRequerySuggested` nos setters de `PartName` e `BatchNumber`.

### Interface (`MainWindow.xaml`)
- [ ] **SQL Config**: Atualizar bindings de `IsEnabled` nos campos de Login/Senha.
- [ ] **Campos de Metadados**: Remover `IsReadOnly="True"` fixo e aplicar binding condicional (ou lógica de edição).
- [ ] **Expander Header**:
    - [ ] Criar layout do Header com Grid.
    - [ ] Adicionar indicador "Online" (Ellipse + Texto).
    - [ ] Aplicar animação de pulso na Ellipse.
    - [ ] Configurar Visibilidade baseada em `IsMonitoring`.

### Code-Behind (`MainWindow.xaml.cs`)
- [ ] Implementar lógica de `Save` no evento `Closing`.
- [ ] Implementar lógica de `Restore` no construtor/load.

### Testes
- [ ] **Teste de Validação**: Tentar enviar com campos vazios (Botão deve estar desabilitado).
- [ ] **Teste de SQL UI**: Verificar bloqueio de campos ao marcar "Windows Auth" e ao iniciar Monitoramento.
- [ ] **Teste Visual**: Verificar indicador "Online" pulsando quando o monitoramento inicia.
- [ ] **Teste de Persistência**: Mover/Redimensionar janela, fechar e reabrir. Verificar restauração correta.
