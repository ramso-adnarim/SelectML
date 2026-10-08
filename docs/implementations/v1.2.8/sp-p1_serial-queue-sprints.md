# Planejamento de Sprints: Remodelagem do Enfileiramento Serial (V1.2.8 - Item 1)

**Versão Alvo:** 1.2.8  
**Tipo:** Sprint Planning / Roteiro de Execução Técnica  
**Status:** Pronto para Execução (Aguardando Sinal Verde)  
**Data:** 04/10/2026  
**Documento Técnico de Referência:** [ip-p1_serial-queue-remodeling.md](file:///c:/Antigravity/SelectML/docs/implementations/v1.2.8/ip-p1_serial-queue-remodeling.md)

---

## 1. Estratégia de Desenvolvimento

Para garantir a máxima eficiência técnica, menor risco de regressão e permitir testes incrementais, a implementação do **Item 1 da V1.2.8** foi dividida em **5 Sprints modulares**:

```mermaid
graph LR
    S1[Sprint 1:<br/>Modelo & Config] --> S2[Sprint 2:<br/>UI Serial & Toggle]
    S2 --> S3[Sprint 3:<br/>Validação Global]
    S3 --> S4[Sprint 4:<br/>Orquestração Híbrida]
    S4 --> S5[Sprint 5:<br/>Homologação & QA]
```

Cada sprint possui escopo delimitado, critérios de aceitação objetivos e validação independente.

---

## 2. Detalhamento das Sprints

### 🏁 Sprint 1: Infraestrutura de Configuração e Persistência
> **Objetivo:** Adicionar o suporte a persistência da nova opção `EnqueueSerialInput` no arquivo de configuração do usuário (`appsettings.json`), garantindo compatibilidade e migração transparente.

* **Arquivos Afetados:**
  * `SelectML.Client/Services/AppConfig.cs`
* **Tarefas Técnicas:**
  - [x] Adicionar propriedade `public bool EnqueueSerialInput { get; set; } = true;` à classe `AppConfig`.
  - [x] Garantir que o valor padrão seja rigorosamente `true` (mantendo o comportamento padrão/enfileirado por omissão).
  - [x] Verificar a rotina `ConfigService.Load()` e `ConfigService.Save()` para assegurar que instalações existentes recebam o valor padrão sem corrupção do JSON.
* **Critério de Aceitação:**
  * Instâncias sem a chave `EnqueueSerialInput` no `appsettings.json` iniciam com valor `true`.
  * Ao chamar `_configService.Save(config)`, a chave é persistida corretamente formatada no JSON em `%AppData%/SelectML/appsettings.json`.

---

### 🎨 Sprint 2: Interface Gráfica da Conexão Serial (`SerialConfigWindow`)
> **Objetivo:** Implementar o controle Toggle na janela de configuração serial, realizar o binding com o ViewModel e aplicar a regra de desativação reativa do campo *"Nome padrão de característica"*.

* **Arquivos Afetados:**
  * `SelectML.Client/ViewModels/SerialConfigViewModel.cs`
  * `SelectML.Client/Views/SerialConfigWindow.xaml`
* **Tarefas Técnicas:**
  - [x] No `SerialConfigViewModel.cs`:
    - [x] Adicionar a propriedade `EnqueueSerialInput` com notificação de property change para si e para `IsDefaultFeatureNameEnabled`.
    - [x] Implementar a propriedade computada `public bool IsDefaultFeatureNameEnabled => IsDisconnected && EnqueueSerialInput;`.
    - [x] No método `ExecuteConnect()`, salvar `config.EnqueueSerialInput = EnqueueSerialInput;` no `appsettings.json`.
    - [x] No construtor, carregar o valor persistido de `config.EnqueueSerialInput`.
  - [x] No `SerialConfigWindow.xaml`:
    - [x] Aumentar a propriedade `Height` da janela (ajustada para `395` x `340`) para evitar compressão dos botões de rodapé.
    - [x] Inserir o container com o controle ToggleSwitch / ToggleButton abaixo de *"Nome padrão de característica"*.
    - [x] Vincular `IsChecked` bidirecionalmente a `EnqueueSerialInput`.
    - [x] Atualizar o campo `TextBox` de `DefaultFeatureName` para utilizar `IsEnabled="{Binding IsDefaultFeatureNameEnabled}"`.
* **Critério de Aceitação:**
  * Ao abrir a janela, o toggle inicia **Ativado** por padrão e o campo de texto do nome padrão está editável.
  * Ao desativar o toggle, o campo de texto do nome padrão fica cinza/desabilitado instantaneamente.
  * O estado do toggle é restaurado ao fechar e reabrir a janela ou reiniciar a aplicação.

---

### 🛡️ Sprint 3: Blindagem de Validação Global (Cota em Branco e Duplicidades)
> **Objetivo:** Blindar o núcleo de validação da aplicação contra características vazias em qualquer modo de processamento (resolução do conflito C3).

* **Arquivos Afetados:**
  * `SelectML.Client/ViewModels/MainViewModel.cs`
* **Tarefas Técnicas:**
  - [x] Refatorar o método `ValidateItem(ResultItem item)`:
    - [x] Adicionar cláusula de barreira: se `string.IsNullOrWhiteSpace(item.Characteristic)`, atribuir **obrigatoriamente** `item.IsRecognized = false`, mesmo que o banco de dados não esteja conectado ou `KnownFeatures` esteja vazio.
  - [x] No método `CanExecuteAction()` e `ExecuteSend()`:
    - [x] Garantir validação explícita impedindo envio caso qualquer item possua nome em branco ou nulo.
  - [x] No método `BuildMeasurementDataFromUI()`:
    - [x] Garantir que chaves vazias ou apenas com espaços não sejam inseridas no dicionário final de saída.
* **Critério de Aceitação:**
  * Linhas com campo de característica vazio são destacadas como inválidas na interface (borda vermelha/alerta).
  * O botão "Enviar" é barrado caso haja ao menos uma característica sem preenchimento, exibindo mensagem amigável ao usuário.

---

### ⚙️ Sprint 4: Orquestração de Concorrência e Modo Híbrido (`MainViewModel`)
> **Objetivo:** Implementar o fluxo de inserção direta de cotas seriais na lista ativa (`MeasuredResults`), o tratamento do Modo Automático (C1) e a mesclagem quando medições seriais chegarem antes do arquivo (C2).

* **Arquivos Afetados:**
  * `SelectML.Client/ViewModels/MainViewModel.cs`
* **Tarefas Técnicas:**
  - [x] Adicionar campo/propriedade local `_enqueueSerialInput` sincronizado com `AppConfig`.
  - [x] Refatorar o método `OnSerialMeasurementReceived`:
    - [x] **Ramo `!EnqueueSerialInput` (Modo Direto/Híbrido):**
      - [x] Criar `ResultItem` com `Characteristic = string.Empty`, `Value = e.Value`, `IsRecognized = false`, `IsEditable = true`.
      - [x] Assinar evento `newItem.PropertyChanged += ResultItem_PropertyChanged`.
      - [x] Adicionar sequencialmente à `MeasuredResults`.
      - [x] Executar `TriggerValidation()`.
      - [x] Ativar `IsPendingAction = true` para reter o foco na UI e sinalizar estado ocupado.
    - [x] **Ramo `EnqueueSerialInput == true` (Modo Enfileirado):**
      - [x] Manter o fluxo legado intacto (`_serialBuffer.Enqueue(e)` se arquivo ativo; sessão serial direta se ocioso).
  - [x] **Tratamento do Conflito C1 (Modo Automático):**
    - [x] Em `ProcessFile()`, se `!_enqueueSerialInput`, inibir o envio automático assíncrono imediato de `IsAutoMode`, exibindo a mensagem de status usual: *"Dados carregados. Verifique e clique em Enviar."*.
  - [x] **Tratamento do Conflito C2 (Serial Pré-Arquivo):**
    - [x] Em `ProcessFile()`, verificar se já existem itens na `MeasuredResults` provenientes da serial:
      - [x] Em vez de `MeasuredResults.Clear()`, preservar as medições seriais existentes e inserir os itens do arquivo de texto acima delas.
      - [x] Fazer com que o cabeçalho do arquivo (`data.PartName` e `data.BatchNumber`) sobrescreva quaisquer valores prévios na UI.
  - [x] **Mecanismo de "Desfazer" Serial (Comando U-WAVE):**
    - [x] Identificar comando de cancelamento Mitutoyo U-WAVE (`ST1...41899`) em `UWaveStrategy.IsUndoCommand()`.
    - [x] Disparar evento `UndoRequested` em `SerialPortService`.
    - [x] Executar exclusão sequencial do item serial mais recente (`IsFromSerial == true`), sem jamais afetar cotas de arquivo de texto.
* **Critério de Aceitação:**
  * Com o toggle desativado, dados seriais entram como novas linhas no final da tabela.
  * No Modo Automático, a tela não fecha sozinha, aguardando intervenção do usuário.
  * Medições seriais tomadas antes da chegada do arquivo são preservadas e complementadas pelos dados do arquivo.
  * O envio de string de cancelamento U-WAVE exclui do mais recente ao mais antigo exclusivamente as medições seriais.

---

### 🧪 Sprint 5: Homologação Integrada, Testes de Concorrência e Validação Final
> **Objetivo:** Executar a bateria de testes operacionais e de regressão para assegurar robustez, estabilidade e conformidade com as diretrizes do projeto.

* **Tarefas Técnicas:**
  - [x] **Bateria de Regressão:**
    - [x] Testar fluxo normal de arquivos CSV/PDF/JSON com toggle ativado.
    - [x] Testar buffer serial clássico (chegada serial durante arquivo -> dreno posterior em nova tela).
  - [x] **Bateria do Novo Modo Híbrido:**
    - [x] Testar arquivo com cotas + medições seriais adicionadas -> seleção de nomes no ComboBox -> geração do CSV unificado.
    - [x] Testar chegada serial anterior ao arquivo de texto (C2) com prevalência de cabeçalho do arquivo.
    - [x] Testar barreira de validação para cotas vazias ou duplicadas (C3/C4).
  - [x] **Mecanismo de Desfazer Serial (U-WAVE):**
    - [x] Enviar medições seriais e disparar comando de cancelamento U-WAVE -> conferir remoção LIFO apenas das cotas seriais.
  - [x] **Verificação de Compilação e Empacotamento:**
    - [x] Compilar em modo `Release` (garantindo que o target `EnsureWindowsSerialPortDll` copie a DLL nativa Win32 de 88 KB).
* **Critério de Aceitação:**
  * 100% dos testes manuais e de regressão executados com sucesso sem crashes ou exceções não tratadas nos logs.

---

## 3. Matriz de Dependência e Esforço Estimado

| Sprint | Foco Principal | Risco de Regressão | Complexidade Técnica |
| :--- | :--- | :--- | :--- |
| **Sprint 1** | Persistência / `AppConfig` | Muito Baixo | Baixa |
| **Sprint 2** | UI / `SerialConfigWindow` | Baixo | Média |
| **Sprint 3** | Validação / `MainViewModel` | Baixo | Baixa |
| **Sprint 4** | Concorrência / `MainViewModel` | Médio | Alta |
| **Sprint 5** | Homologação / Regressão | Nulo (Validação) | Média |

---

## 4. Estado Atual e Próximas Ações
* **Status:** O planejamento e a decomposição em Sprints estão concluídos e documentados.
* **Próxima Ação:** Aguardar o envio dos próximos itens/funcionalidades da versão **1.2.8** pelo usuário antes de iniciar a execução da **Sprint 1**.
