# Plano de Implementação: Remodelagem do Enfileiramento Serial (V1.2.8 - Item 1)

**Versão Alvo:** 1.2.8  
**Tipo:** Nova Funcionalidade / Melhoria de Concorrência  
**Status:** Planejamento Revisado (Aprovado pelo Usuário com Ajustes)  
**Data da Revisão:** 04/10/2026  
**Documento de Origem:** Solicitação de Usuário (Item 1 - V1.2.8)

---

## 1. Visão Geral e Contexto Operacional

### 1.1 Cenário Atual
Atualmente, o SelectML gerencia a entrada de arquivos de máquinas (via `FileSystemWatcher`) e medições seriais (via `SerialPortService`) de forma isolada através do mecanismo de **Buffer Reverso**:
* Quando um arquivo de texto está em processamento (`_isProcessingFile == true`), qualquer dado recebido pela porta serial é enfileirado em `_serialBuffer`.
* Somente após o envio ou cancelamento do arquivo de texto atual (`ResetUI`), o buffer serial é drenado, gerando uma nova sessão de dados separada exclusivamente para as medições seriais.

### 1.2 Necessidade Operacional (Estação Híbrida Unificada)
Em linhas de produção e laboratórios de metrologia, o processo de medição de uma única peça frequentemente envolve múltiplos instrumentos:
1. Parte das cotas é medida em uma máquina automatizada (ex: CMM Zeiss, ViciVision), gerando um arquivo de relatório.
2. Parte das cotas é medida manualmente pelo operador utilizando instrumentos manuais conectados via RS232/USB (ex: paquímetro digital, micrômetro, rugosímetro Mitutoyo U-WAVE).

Para evitar que o operador precise enviar dois relatórios separados para o banco de dados/CSV de saída para a mesma peça, a versão 1.2.8 introduz a capacidade de **adicionar as medições seriais diretamente ao processamento do arquivo de texto ativo**, consolidando todas as cotas em um único registro de saída.

---

## 2. Requisitos Detalhados

### Requisito 1.1: Interface da Janela de Configuração Serial
* Na janela de configuração de conexão serial (`SerialConfigWindow.xaml`), dentro do grupo **"Configuração do Dispositivo"**, logo abaixo do campo *"Nome padrão de característica"*, deve ser inserido um controle toggle (ativado/desativado) com o rótulo **"Enfileirar dados de entrada"**.
* O estado deste toggle deve ser persistido nas configurações locais da aplicação (`appsettings.json`) através do `AppConfig.cs` e `ConfigService.cs`.
* **Valor Padrão da Aplicação:** **ATIVADO (`true`)**.

### Requisito 1.2: Dualidade de Comportamento e Polaridade do Toggle
A polaridade do toggle segue a semântica direta de sua nomenclatura ("Enfileirar dados de entrada"):

* **Toggle ATIVADO (`true` - Padrão / Legado):**
  * **Haverá enfileiramento de dados.**
  * Se um arquivo de texto estiver em processamento, os dados seriais recebidos são retidos em `_serialBuffer` para processamento posterior em uma sessão separada (comportamento atual).
  * O campo *"Nome padrão de característica"* permanece **habilitado** na interface e seu valor é aplicado às medições seriais quando o buffer for processado.
* **Toggle DESATIVADO (`false` - Modo Direto / Híbrido Unificado):**
  * **Os dados NÃO serão enfileirados em buffer isolado.**
  * Os dados recebidos pela porta serial são adicionados **imediatamente e sequencialmente** à lista de resultados em exibição (`MeasuredResults`), logo abaixo da última linha processada do arquivo de texto.
  * Permite ao operador inspecionar e despachar todas as medições da peça em conjunto em uma única saída CSV.

### Requisito 1.3: Tratamento de Nomes e Validação das Medições Seriais Anexadas
Quando a opção estiver **DESATIVADA** (sem enfileiramento / adição direta à lista):
1. A opção *"Nome padrão de característica"* na janela de configuração deve ser **desativada** (`IsEnabled = false`), impedindo que um nome estático seja aplicado genericamente.
2. Cada medição serial adicionada à lista deve ser inserida com a característica **em branco** (`Characteristic = string.Empty`), com `IsRecognized = false` e `IsEditable = true`.
3. O operador deverá selecionar a característica correspondente através do `ComboBox` de características conhecidas da rotina/lote (carregadas do banco de dados via `KnownFeatures`) ou digitar livremente o nome da cota.
4. A validação visual (destaque em vermelho para não reconhecido, verde para reconhecido, bloqueio em caso de duplicidade) deve ser disparada imediatamente.

---

## 3. Mapeamento de Conflitos e Soluções Refinadas

### ⚠️ Conflito 1: Colisão com o Modo Automático (`IsAutoMode == true`)
* **Contexto:** No fluxo atual de `ProcessFile()`, se `IsAutoMode` estiver ativo e todas as características do arquivo de texto forem reconhecidas pelo banco de dados, o SelectML executa `GenerateOutputCsv()` e `ResetUI()` imediatamente. Se o enfileiramento estiver desativado (modo direto), o operador não teria tempo de realizar medições seriais complementares.
* **Solução Alinhada:** 
  * Quando o enfileiramento estiver desativado (`EnqueueSerialInput == false`), o auto-envio imediato deve ser **suspenso**, mantendo a interface no estado padrão de aguardo de confirmação do operador (`IsPendingAction = true`).
  * **Mensagem de Status:** A mensagem de status **NÃO** deve ser explícita mencionando espera de dados seriais (pois a medição serial é estritamente opcional). Deve-se manter a mensagem padrão já adotada pelo sistema:
    > *"Dados carregados. Verifique e clique em Enviar."* (ou *"Validação necessária. Verifique e envie."* se houver cotas pendentes).

### ⚠️ Conflito 2: Chegada de Dados Seriais ANTES da Carga do Arquivo de Texto
* **Contexto:** O operador pode realizar primeiro as medições no paquímetro serial e somente depois exportar o arquivo da máquina CMM.
* **Solução Alinhada:**
  * Se o enfileiramento estiver desativado (`EnqueueSerialInput == false`) e medições seriais chegarem sem nenhum arquivo em tela, elas são inseridas normalmente na `MeasuredResults`.
  * Quando o arquivo de texto subsequente for detectado e processado por `ProcessFile()`, em vez de executar um `MeasuredResults.Clear()` destrutivo, o sistema deve **complementar** os dados:
    1. Carrega as características e tolerâncias do arquivo de texto na lista de resultados.
    2. Mantém as medições seriais já realizadas no final da lista.
    3. **Regra de Prevalência de Cabeçalho:** Os dados de cabeçalho do arquivo de texto (`PartName` e `BatchNumber`) devem **vencer** / sobrescrever qualquer valor que o usuário possa ter digitado manualmente nos campos de cabeçalho antes da chegada do arquivo.
    4. O usuário mantém a liberdade de editar esses campos manualmente após o carregamento, caso necessário.

### ⚠️ Conflito 3: Validação de Característica em Branco
* **Contexto:** Se a lista `KnownFeatures` estiver vazia (ex: banco de dados desconectado), o código legado atribuía `item.IsRecognized = true` por flexibilidade, o que permitiria enviar cotas com nome em branco.
* **Solução Alinhada:**
  * Regra universal e irrestrita: uma característica com nome vazio (`string.IsNullOrWhiteSpace(item.Characteristic)`) é **categoricamente inválida (`IsRecognized = false`)** e impede o envio do CSV em **qualquer** modo de processamento (seja arquivo puro, serial puro ou híbrido).

### ⚠️ Conflito 4: Nomes Duplicados entre Arquivo e Instrumento Serial
* **Análise:** O mecanismo de `CheckDuplicates()` e validação em `ExecuteSend()` já cobre satisfatoriamente essa situação, destacando as linhas com nomes repetidos em vermelho e impedindo a emissão de CSVs inconsistentes.

### ⚠️ Conflito 5: Concorrência com Novos Arquivos Chegando na Pasta Monitorada
* **Análise:** Enquanto a tela de medição estiver ativa com a sessão híbrida em composição (`IsPendingAction = true`), novos arquivos detectados pelo `FileSystemWatcher` entrarão normalmente na fila `_fileBuffer`, sem interferir na sessão atual.

---

## 4. Arquitetura da Solução e Componentes Afetados

```mermaid
graph TD
    subgraph "SerialConfigView / Configurações"
        Toggle["Toggle: Enfileirar dados de entrada<br/>(Padrão: ATIVADO)"]
        Toggle -->|ATIVADO| EnableDefaultName["Habilita 'Nome padrão de característica'"]
        Toggle -->|DESATIVADO| DisableDefaultName["Desabilita 'Nome padrão de característica'"]
        Toggle -->|Salvar| AppConfig["appsettings.json (EnqueueSerialInput)"]
    end

    subgraph "MainViewModel (Evento Serial)"
        SerialData[Dado Serial Recebido] --> CheckQueueMode{EnqueueSerialInput?}
        
        CheckQueueMode -->|ATIVADO (true - Legado)| IsFileActiveLegacy{Arquivo em Processamento?}
        IsFileActiveLegacy -->|SIM| ReverseBuffer[_serialBuffer.Enqueue]
        IsFileActiveLegacy -->|NÃO| DirectSerialSession[Sessão Serial Isolada]

        CheckQueueMode -->|DESATIVADO (false - Direto/Híbrido)| CheckCurrentSession{Há itens na tela?}
        CheckCurrentSession -->|SIM (Arquivo ou Serial)| AppendDirectly["MeasuredResults.Add(Item)<br/>(Characteristic = Vazio, IsEditable = True)"]
        CheckCurrentSession -->|NÃO| StartHybridSession["Cria Item Inicial na Tela<br/>(Aguardando arquivo complementar)"]
        
        AppendDirectly --> Validate[TriggerValidation]
        Validate --> OperatorAssigns[Operador seleciona Característica no ComboBox]
    end

    subgraph "Chegada Posterior de Arquivo (C2)"
        FileArrives[Arquivo de Texto Detectado] --> HasPriorSerial{Há medições seriais prévias?}
        HasPriorSerial -->|SIM| MergeSession["Adiciona dados do arquivo + preserva seriais<br/>Cabeçalho do arquivo SOBRESCREVE PartName/Batch"]
        HasPriorSerial -->|NÃO| StandardFileLoad[Carga normal de arquivo]
    end

    subgraph "Saída Consolidada"
        OperatorAssigns --> SendAction[Clique em 'Enviar']
        SendAction --> SingleCSV["CSV Único (Arquivo + Serial consolidados)"]
    end
```

### 4.1 Modificações em Arquivos de Código

#### 1. `SelectML.Client/Services/AppConfig.cs`
* Adicionar a propriedade com valor padrão `true`:
  ```csharp
  // Serial Queue Behavior (true = enfileirar em buffer separado; false = anexar diretamente à lista)
  public bool EnqueueSerialInput { get; set; } = true;
  ```

#### 2. `SelectML.Client/ViewModels/SerialConfigViewModel.cs`
* Adicionar propriedade bindável:
  ```csharp
  public bool EnqueueSerialInput
  {
      get => _enqueueSerialInput;
      set
      {
          _enqueueSerialInput = value;
          OnPropertyChanged();
          OnPropertyChanged(nameof(IsDefaultFeatureNameEnabled));
      }
  }
  ```
* Adicionar propriedade computada para habilitar o campo de texto apenas quando o enfileiramento estiver ativo:
  ```csharp
  public bool IsDefaultFeatureNameEnabled => IsDisconnected && EnqueueSerialInput;
  ```
* Na rotina `ExecuteConnect()` e no construtor, persistir e carregar `EnqueueSerialInput`.

#### 3. `SelectML.Client/Views/SerialConfigWindow.xaml`
* Aumentar a altura da janela (`Height="370"`, anteriormente `320`) para acomodar confortavelmente o novo controle sem compressão vertical.
* Inserir o controle Toggle/CheckBox abaixo do bloco de "Nome padrão de característica":
  ```xaml
  <!-- Toggle Enfileirar Dados de Entrada -->
  <Border Margin="0,10,0,15" Padding="10,8" Background="{DynamicResource SubCardBackgroundBrush}" CornerRadius="4">
      <Grid>
          <Grid.ColumnDefinitions>
              <ColumnDefinition Width="*"/>
              <ColumnDefinition Width="Auto"/>
          </Grid.ColumnDefinitions>
          <StackPanel VerticalAlignment="Center">
              <TextBlock Text="Enfileirar dados de entrada" FontWeight="SemiBold" FontSize="12" Foreground="{DynamicResource TextBrush}"/>
              <TextBlock Text="Bufferiza dados seriais para processamento posterior" FontSize="10" Foreground="{DynamicResource TextBrushSecondary}"/>
          </StackPanel>
          <ToggleButton Grid.Column="1" 
                        IsChecked="{Binding EnqueueSerialInput, Mode=TwoWay}"
                        IsEnabled="{Binding IsDisconnected}"
                        Style="{StaticResource ModernToggleSwitchStyle}"
                        VerticalAlignment="Center"/>
      </Grid>
  </Border>
  ```
* Atualizar o `TextBox` de `DefaultFeatureName` para utilizar `IsEnabled="{Binding IsDefaultFeatureNameEnabled}"`.

#### 4. `SelectML.Client/ViewModels/MainViewModel.cs`
* Carregar `EnqueueSerialInput` das configurações no startup e ao salvar conexão serial.
* Ajustar o método `OnSerialMeasurementReceived`:
  ```csharp
  if (!EnqueueSerialInput)
  {
      // MODO DIRETO / HÍBRIDO (Sem enfileiramento em buffer isolado)
      var newItem = new ResultItem
      {
          Characteristic = string.Empty, // Nenhum nome pré-atribuído
          Value = e.Value,
          IsRecognized = false,          // Força validação do usuário
          IsEditable = true              // Permite seleção/digitação
      };
      newItem.PropertyChanged += ResultItem_PropertyChanged;
      MeasuredResults.Add(newItem);
      TriggerValidation();

      if (!IsPendingAction)
      {
          IsPendingAction = true;
          StatusMessage = "Recebendo dados seriais...";
          RequestRestoreWindow?.Invoke();
      }
  }
  else
  {
      // MODO ENFILEIRADO (Comportamento legado mantido)
      if (_isProcessingFile)
      {
          _serialBuffer.Enqueue(e);
          StatusMessage = $"Dado serial em buffer ({_serialBuffer.Count})...";
      }
      else
      {
          // Sessão serial individual
          string featureName = !string.IsNullOrWhiteSpace(_currentSerialFeatureName) 
                               ? _currentSerialFeatureName 
                               : e.FeatureName;

          var newItem = new ResultItem 
          { 
              Characteristic = featureName, 
              Value = e.Value, 
              IsRecognized = !e.IsGeneric,
              IsEditable = true 
          };
          newItem.PropertyChanged += ResultItem_PropertyChanged;
          MeasuredResults.Add(newItem);
          TriggerValidation();

          if (!IsPendingAction)
          {
              IsPendingAction = true;
              StatusMessage = "Recebendo dados seriais...";
              RequestRestoreWindow?.Invoke();
          }
      }
  }
  ```
* Ajustar `ValidateItem` para garantir que `Characteristic` vazia ou em branco seja categoricamente inválida (`IsRecognized = false`).
* Em `ProcessFile()`:
  * Caso `EnqueueSerialInput == false`, suspender o envio automático de `IsAutoMode`, exibindo a mensagem padrão `"Dados carregados. Verifique e clique em Enviar."`.
  * Preservar eventuais itens seriais já presentes na lista e sobrescrever `PartName` e `BatchNumber` com os dados do arquivo de texto.

---

## 5. Plano de Testes e Roteiro de Validação

### Teste 1: Interface e Persistência das Configurações
1. Abrir a janela de conexão serial ("Menu -> Conexão Serial").
2. Verificar se o toggle "Enfileirar dados de entrada" é exibido com o estado padrão **ATIVADO**.
3. Verificar que o campo "Nome padrão de característica" está **habilitado**.
4. Desativar o toggle: verificar se o campo "Nome padrão de característica" fica imediatamente **desabilitado**.
5. Fechar e reabrir a janela / aplicação: verificar se o estado desativado foi persistido corretamente em `appsettings.json`.

### Teste 2: Regressão do Modo Enfileirado (Toggle Ativado)
1. Com o toggle **ATIVADO** e nome padrão configurado (ex: "Rugosidade"), conectar à porta serial.
2. Carregar um arquivo de texto.
3. Enviar medições seriais enquanto o arquivo está na tela.
4. **Resultado esperado:** O dado serial é retido em `_serialBuffer`. Ao enviar o arquivo de texto, a tela abre a sessão serial separada com o nome padrão configurado.

### Teste 3: Modo Híbrido com Anexação Sequencial (Toggle Desativado)
1. Com o toggle **DESATIVADO**, conectar à porta serial.
2. Carregar um arquivo de texto com 3 características.
3. Enviar uma medição serial (ex: `12.450`).
4. **Resultado esperado:** 
   * Uma 4ª linha aparece no final de `MeasuredResults`.
   * A cota está em branco e marcada como inválida (vermelho).
   * O valor `12.450` está presente e o campo é editável via ComboBox (`KnownFeatures`).
5. Enviar uma segunda medição serial: uma 5ª linha aparece logo abaixo, também em branco.

### Teste 4: Chegada Serial Anterior ao Arquivo de Texto (C2)
1. Com o toggle **DESATIVADO**, enviar duas medições seriais antes de qualquer arquivo de texto chegar.
2. Digitar temporariamente um nome de peça genérico no cabeçalho.
3. Em seguida, descarregar um arquivo de texto válido na pasta monitorada.
4. **Resultado esperado:**
   * O arquivo de texto é processado.
   * Os campos `PartName` e `BatchNumber` são sobrescritos com as informações contidas no cabeçalho do arquivo.
   * A lista `MeasuredResults` contém as características do arquivo no topo e as duas medições seriais preservadas na parte inferior.

### Teste 5: Bloqueio Universal de Características em Branco
1. Tentar enviar a lista mantendo uma cota serial com o nome em branco (mesmo com o banco de dados desconectado).
2. **Resultado esperado:** O sistema impede o envio e exibe alerta de validação.
