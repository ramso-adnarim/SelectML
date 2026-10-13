# Plano de Implementação: Listagem e Validação Completa de Características (V1.2.8 - Item 2)

**Versão Alvo:** 1.2.8  
**Tipo:** Nova Funcionalidade / Integração com Banco de Dados / Refinamento de UI  
**Status:** Planejamento Inicial Concluído (Aguardando Aprovação para Execução)  
**Data:** 10/10/2026  
**Documento de Origem:** Solicitação de Usuário (Item 2 - V1.2.8)  
**Branch de Desenvolvimento:** `feature/v1.2.8-feature-listing`

---

## 1. Visão Geral e Contexto Operacional

### 1.1 Cenário Atual
Na versão atual (V1.2.8 - Fase 1), o SelectML monitora arquivos de medição provenientes de máquinas automatizadas (ex: ViciVision CSV/JSON, Zeiss Calypso PDF) ou medições de instrumentos manuais via porta serial.
* O sistema identifica o número do lote através do campo `BatchNumber` (oriundo do arquivo ou digitado manualmente pelo operador no cabeçalho).
* Em seguida, consulta a tabela `dbo.ActiveRun` no banco de dados do MeasurLink (`GetFeaturesForRunAsync`) apenas para obter a lista de nomes conhecidos (`KnownFeatures`).
* A tabela de resultados na interface exibe **estritamente as características que vieram descritas no arquivo de texto**. Caso a rotina de inspeção contenha outras características no banco de dados que não foram medidas pela máquina naquele arquivo, elas não aparecem na tela a menos que o operador as adicione manualmente linha por linha.

### 1.2 Necessidade Operacional (Inspeção Holística da Rotina)
Em rotinas industriais de controle de qualidade, uma corrida de inspeção (Run) frequentemente contém uma combinação de:
1. **Cotas de Máquinas Automatizadas** (medidas por CMM / projetor ótico e exportadas em arquivo).
2. **Cotas de Instrumentos Manuais** (medidas no paquímetro, micrômetro ou rugosímetro serial).
3. **Inspeções Visuais e por Calibre** (características do tipo **Atributo**, tais como *Passa/Não Passa*, *Aspecto Visual*, *Rosca Conforme*, que exigem apenas aprovação/reprovação booleana).

Com a nova funcionalidade **"Listar todas as características"** ativada:
* A partir do identificador da corrida (`RunID` / `BatchNumber`), a aplicação consulta a rotina completa no MeasurLink e exibe **todas as características daquela rotina de inspeção em sua ordem sequencial exata (`OrderNo ASC`)**.
* As cotas que já vieram medidas no arquivo de texto são automaticamente reconhecidas e preenchidas.
* As cotas que ainda não foram medidas permanecem listadas em tela aguardando preenchimento manual ou via instrumentos seriais (de cima para baixo).
* Características do tipo **Atributo** recebem um controle visual ergonômico e intuitivo (botão toggle horizontal sutil para alternar entre **Aprovado** e **Reprovado**), dispensando caixas de texto numéricas.
* As tolerâncias de engenharia (`Nominal`, `LSL`, `USL`) e quantidade de casas decimais (`DecimalLength`) são extraídas e utilizadas para destacar em **verde** os valores aprovados e em **vermelho** os valores fora de especificação.

---

## 2. Requisitos Detalhados do Item 2

### Requisito 2.1: Menu Superior e Controle de Ativação
* Na barra de menus principal da aplicação (`MainWindow.xaml`), ao lado direito do menu *"Modificador de nomes"*, deve ser inserido um novo menu com rótulo descritivo (ex: **"Configuração"**).
* Ao clicar no menu, será exibida a opção checkable **"Listar todas as características"**, funcionando como um toggle (liga/desliga).
* O estado deve ser persistido nas configurações locais da aplicação (`appsettings.json`) através do `AppConfig.cs` e `ConfigService.cs` (propriedade `ListAllFeatures`, padrão: `false` para preservar retrocompatibilidade).

### Requisito 2.2: Ciclo de Processamento e Consulta Otimizada de Características
* Quando a opção estiver ativada (`ListAllFeatures == true`), o processamento de arquivo ou entrada manual valida o identificador da corrida.
* **Validação do Identificador:** O sistema utiliza o campo `BatchNumber` (que pode conter diretamente o `RunID` numérico ou o `RunName` alfanumérico) e resolve o `RunID` correspondente no banco MeasurLink.
* **Consulta SQL Otimizada:** Executa a consulta de alta performance no banco de dados MeasurLink utilizando diretivas `WITH (NOLOCK)` e ordenação nativa por índice `OrderNo ASC`:
  ```sql
  DECLARE @RunID INT = @ResolvedRunID;

  SELECT 
      fr.OrderNo,
      f.FeatureID,
      f.FeatureName,
      f.FeatureType,
      CASE 
          WHEN f.FeatureType = 1 THEN 'Variável'
          WHEN f.FeatureType IN (2, 4) THEN 'Atributo'
          WHEN f.FeatureType = 3 THEN 'Posição'
          ELSE 'Outro'
      END AS TipoDescricao,
      fp.Target AS Nominal,
      fp.LowerToleranceLimit AS LSL,
      fp.UpperToleranceLimit AS USL,
      fp.DecimalLength
  FROM FeatureRun fr WITH (NOLOCK)
  INNER JOIN Feature f WITH (NOLOCK) 
      ON fr.FeatureID = f.FeatureID
  LEFT JOIN FeatureProperties fp WITH (NOLOCK) 
      ON fp.FeatureID = f.FeatureID 
     AND fp.FeaturePropID = COALESCE(fr.FeaturePropID, f.FeaturePropID)
  WHERE fr.RunID = @RunID
  ORDER BY fr.OrderNo ASC;
  ```
* **Mesclagem Inteligente de Dados:**
  1. A lista final em tela respeitará rigorosamente a ordem `OrderNo ASC` retornada pelo banco.
  2. Características presentes tanto no banco quanto no arquivo de texto de entrada são preenchidas com o valor medido do arquivo, marcadas como reconhecidas (`IsRecognized = true`) e associadas aos limites de tolerância.
  3. Características presentes no banco mas **ausentes** no arquivo de texto são inseridas na lista com seu respectivo nome, limites e tipo, com o campo de valor no estado **Aguardando Inserção** (sem duplicidade).
  4. Eventuais características lidas do arquivo que não constem na rotina do banco são mantidas no final da lista, marcadas como não reconhecidas (`IsRecognized = false`) para revisão do operador.

### Requisito 2.3: Tratamento de Características do Tipo Atributo (Toggle Aprovado/Reprovado)
* A consulta identifica o tipo da característica através do campo `FeatureType`:
  * `FeatureType = 1`: **Variável** (aguarda valor numérico).
  * `FeatureType IN (2, 4)`: **Atributo** (aguarda estado discreto de conformidade).
  * `FeatureType = 3`: **Posição** (geometria).
* Para características identificadas como **Atributo**:
  * Na coluna *"Valor"* da tabela de resultados, **não deve ser exibida** uma caixa de texto numérica (`TextBox`).
  * Em seu lugar, deve ser renderizado um controle **Toggle sutil e horizontal** no padrão de design system da aplicação.
  * O operador pode clicar para alternar o estado entre **"Aprovado"** e **"Reprovado"**.
  * Valor padrão ao carregar: **"Aprovado"** (conforme convenção de chão de fábrica).

### Requisito 2.4: Destacar Valores Conformes e Não Conformes com Cores (Tolerâncias)
* A consulta extrai `Nominal`, `LSL` (Limite Inferior), `USL` (Limite Superior) e `DecimalLength`.
* **Regra de Coloração Visual:**
  * **Dentro das Tolerâncias:** O texto da linha/valor é exibido na cor **Verde** (`Brush.Status.Success`).
  * **Fora das Tolerâncias:** Se o valor medido for inferior ao `LSL` ou superior ao `USL`, o texto é destacado em **Vermelho** (`Brush.Status.Error`).
  * **Atributos:** Se "Aprovado" -> Verde; se "Reprovado" -> Vermelho.
  * **Cotas Sem Tolerância Cadastrada:** Exibidas na cor padrão neutra (`Brush.Text.Primary` / `Brush.Action.Primary`).
  * **Cotas Aguardando Valor:** Exibidas em estado neutro/placeholder suave (não devem ser marcadas como erro antes de serem medidas).
* **Comportamento Não Bloqueante:** A indicação em vermelho serve como alerta visual imediato ao inspetor da qualidade, não impedindo o envio caso a empresa permita arquivamento de peças reprovadas.

### Requisito 2.5: Preenchimento Sequencial via Comunicação Serial (Top-Down)
* Quando instrumentos seriais enviarem medições para uma lista com cotas da rotina pendentes:
  * A medição serial preenche sequencialmente a **primeira característica do tipo Variável da rotina que ainda não possua valor medido**, de cima para baixo.
  * O valor inserido é formatado, validado e tem sua cor atualizada instantaneamente.
* Caso o usuário continue enviando medições seriais após **todas** as características da rotina já estarem preenchidas:
  * O sistema cria características adicionais no final da tabela (comportamento da Fase 1).

### Requisito 2.6: Desfazer Inteligente (Serial Undo / U-WAVE)
* Ao receber o comando de desfazer serial (hardware trigger U-WAVE ou botão de desfazer):
  1. Se houver características adicionais (além das pertencentes à rotina do banco de dados), a linha adicional mais recente é **excluída** da tabela.
  2. Se o desfazer alcançar características que fazem parte da **rotina original do banco de dados**, a linha **NÃO deve ser removida**. O sistema deve apenas **limpar o valor medido** (`Value` resetado / estado aguardando medição), mantendo a característica visível e disponível para nova medição.

---

## 3. Mapeamento de Conflitos, Perguntas de Projeto e Soluções Técnicas

### ⚠️ P1: Identificação de Lote: `RunID` vs `BatchNumber` vs `RunName`
* **Contexto Analisado:**
  * No SelectML, a propriedade utilizada tanto no ViewModel quanto nos Parsers de arquivo é `BatchNumber`.
  * No MeasurLink:
    * `RunID`: Chave primária inteira (ex: `705`).
    * `RunName`: Nome do lote / corrida cadastrado no sistema (ex: `"067931-102026"` ou `"T2"`).
  * Arquivos de máquinas diferentes podem gerar o nome da corrida ou o ID numérico.
* **Solução Técnica Adotada:**
  * Criação do método `ResolveRunIdAsync(string identifier)` em `DatabaseService`:
    * Se `int.TryParse(identifier, out int id)`, consulta primeiramente por `RunID = @id` em `ActiveRun` e `Run`.
    * Se não for puramente numérico ou não encontrar por ID, consulta por `RunName = @identifier`.
  * Isso confere robustez total: aceita tanto o ID direto quanto o nome do lote digitado pelo operador ou lido do arquivo.

### ⚠️ P2: Representação e Exportação de Características do Tipo Atributo no CSV
* **Contexto Analisado:**
  * O arquivo CSV final do SelectML é importado pelo MeasurLink através do template `SELECTML.itpl` (`[A<;>]***` = Actuals / Leituras).
  * No banco de dados do MeasurLink (`AttFeatureRunData`), atributos são registrados com o campo `DefectCount` (contagem de defeitos):
    * `0` = Peça Conforme / Sem Defeito (**Aprovado**).
    * `1` = Peça Não Conforme / Defeito Encontrado (**Reprovado**).
  * No modelo `MeasurementData`, o dicionário de resultados atualmente é tipado como `Dictionary<string, double>`.
* **Solução Técnica Adotada:**
  * Mapeamento numérico subjacente:
    * **Aprovado** = `0.0`
    * **Reprovado** = `1.0`
  * Na interface gráfica, o usuário vê apenas o toggle ergonômico com os rótulos **"Aprovado"** e **"Reprovado"** com suas respectivas cores.
  * O CSV gerado grava os valores `0` ou `1`, garantindo compatibilidade imediata com o interpretador do MeasurLink e com o dicionário `Results` existente.

### ⚠️ P3: Estado "Aguardando Valor" e Bloqueio de Envio
* **Contexto Analisado:**
  * Se a rotina listar 15 cotas e o arquivo trouxer 5, as outras 10 estarão listadas sem valor medido.
  * O tipo `double` padrão do C# inicializa como `0.0`. Se não houver tratamento, a cota exibiria `0.000` e poderia ser avaliada erroneamente em relação à tolerância ou enviada ao banco como medição real zero.
* **Solução Técnica Adotada:**
  * No `ResultItem`, introduzir uma propriedade de controle de estado:
    * `public double? Value { get; set; }` (ou `public bool HasValue { get; set; }`).
  * Enquanto `HasValue == false`:
    * Não avalia limites de tolerância (cor neutra de espera).
    * Na UI, exibe máscara vazia ou texto indicativo *"Aguardando medição..."*.
  * No envio (`ExecuteSend`):
    * O sistema verifica se há itens sem valor preenchido.
    * Se houver itens vazios, aciona diálogo ou respeita a política configurada de confirmação (`ConfirmationAction`), prevenindo o envio acidental de dados incompletos sem ciência do operador.

### ⚠️ P4: Conflito com o Modo Automático (`IsAutoMode`)
* **Contexto Analisado:**
  * Com o modo automático ligado, se todas as cotas lidas do arquivo fossem reconhecidas, o SelectML despachava o CSV imediatamente.
  * Se "Listar todas as características" estiver ativado e a rotina contiver cotas não medidas pelo arquivo, o envio automático fecharia a tela antes do operador medir no paquímetro serial ou avaliar os atributos visuais.
* **Solução Técnica Adotada:**
  * Quando `ListAllFeatures == true` e houver características pendentes de valor na rotina:
    * O auto-envio é **obrigatoriamente suspenso**, mantendo a interface ativa (`IsPendingAction = true`) com status:
      > *"Rotina carregada. Preencha as características pendentes e clique em Enviar."*

---

## 4. Arquitetura da Solução e Diagramas

```mermaid
graph TD
    subgraph "Interface e Configuração"
        MenuCheck["Menu: Rotina de Inspeção<br/>[x] Listar todas as características"]
        MenuCheck --> AppConfig["appsettings.json (ListAllFeatures)"]
    end

    subgraph "Entrada de Dados (Arquivo ou Manual)"
        InputSource["Arquivo (CSV/JSON/PDF) ou Digitação"] --> ExtractBatch["Identifica BatchNumber"]
        ExtractBatch --> ResolveRun["ResolveRunIdAsync(BatchNumber)"]
    end

    subgraph "DatabaseService (MeasurLink)"
        ResolveRun --> SQLQuery["Query Otimizada FeatureRun + FeatureProperties<br/>(OrderNo ASC, FeatureType, Tolerâncias, Decimais)"]
        SQLQuery --> FeatureList["Lista Completa da Rotina (InspectionFeatureInfo)"]
    end

    subgraph "Mesclagem em MainViewModel"
        FeatureList --> MergeProcess{"ListAllFeatures Ativo?"}
        MergeProcess -->|NÃO (Modo Legado)| FileOnly["Exibe apenas cotas do arquivo"]
        MergeProcess -->|SIM| OrderedMerge["Monta MeasuredResults na ordem OrderNo ASC:<br/>1. Cotas do arquivo: Valor preenchido<br/>2. Cotas do banco ausentes: Aguardando valor<br/>3. Cotas do tipo Atributo: Toggle Aprovado/Reprovado"]
    end

    subgraph "Entradas Complementares (Serial / UI)"
        SerialData["Medição Serial Recebida"] --> FindPending{"Há cota da rotina sem valor?"}
        FindPending -->|SIM| FillPending["Preenche a próxima cota da rotina (Top-Down)"]
        FindPending -->|NÃO| AppendExtra["Adiciona como cota adicional no final"]
        
        UndoCmd["Comando Desfazer (U-WAVE)"] --> CheckExtra{"É cota adicional?"}
        CheckExtra -->|SIM| RemoveRow["Remove linha da tabela"]
        CheckExtra -->|NÃO (Cota do Banco)| ClearValueOnly["Limpa apenas o valor medido (Mantém a linha)"]
    end

    subgraph "Exibição e Validação"
        OrderedMerge --> GridDisplay["DataGrid com Cores de Tolerância:<br/>Verde = Conforme | Vermelho = Não Conforme"]
        FillPending --> GridDisplay
    end
```

---

## 5. Estrutura de Modelos e Modificações Previstas

### 5.1 Novo Modelo: `InspectionFeatureInfo.cs`
Localização: `SelectML.Core/InspectionFeatureInfo.cs`
```csharp
namespace SelectML.Core
{
    public class InspectionFeatureInfo
    {
        public int OrderNo { get; set; }
        public int FeatureID { get; set; }
        public string FeatureName { get; set; } = string.Empty;
        public short FeatureType { get; set; }
        public string TipoDescricao { get; set; } = string.Empty;
        public double? Nominal { get; set; }
        public double? LSL { get; set; }
        public double? USL { get; set; }
        public int? DecimalLength { get; set; }

        public bool IsAttribute => FeatureType == 2 || FeatureType == 4;
    }
}
```

### 5.2 Expansão do `ResultItem.cs`
Localização: `SelectML.Client/ResultItem.cs`
Novas propriedades com `INotifyPropertyChanged`:
* `public double? Value`: Valor medido numérico (nullable para representar ausência de medição).
* `public bool HasValue`: Indica se o item já foi preenchido.
* `public short FeatureType`: Tipo da característica (1 = Variável, 2/4 = Atributo, etc.).
* `public bool IsAttribute`: Facilita bindings e DataTemplateSelector.
* `public bool IsApproved`: Estado booleano para atributos (padrão: `true`).
* `public double? Nominal`, `public double? LSL`, `public double? USL`: Limites de tolerância.
* `public int? DecimalLength`: Casas decimais para máscara de formatação.
* `public bool IsFromDbRoutine`: Indica se o item pertence à rotina de inspeção do banco de dados (protegendo a linha contra deleção no Undo).
* `public ToleranceStatus Status`: Propriedade computada (`Conforming`, `OutOfTolerance`, `NotEvaluated`, `Pending`) que dita a cor do texto na interface (Verde/Vermelho/Neutro).

### 5.3 Extensão da Interface e Serviço de Banco de Dados
Localização: `SelectML.Core/IDatabaseService.cs` e `SelectML.Client/Services/DatabaseService.cs`
* `Task<int?> ResolveRunIdAsync(string batchOrRunIdentifier);`
* `Task<List<InspectionFeatureInfo>> GetInspectionRoutineFeaturesAsync(int runId);`

### 5.4 Interface Gráfica (`MainWindow.xaml`)
1. **Menu "Rotina de inspeção":**
   Adicionado imediatamente à direita de *"Modificador de nomes"*, com estilo padronizado e indicador de status.
2. **Coluna "Valor" no DataGrid:**
   Utilização de um `ValueColumnTemplateSelector` (ou `DataTemplate` com triggers):
   * Se `IsAttribute == true`: Exibe botão Toggle horizontal moderno estilizado com as cores do tema:
     * Aprovado: Fundo verde suave, texto *"Aprovado"*.
     * Reprovado: Fundo vermelho suave, texto *"Reprovado"*.
   * Se `IsAttribute == false`: Exibe `TextBox` / `TextBlock` com formatação baseada em `DecimalLength` e cor de texto orientada a `Status` (Verde para dentro dos limites, Vermelho para fora dos limites).

---

## 6. Roteiro de Testes e Validação de QA

### Cenário 1: Ativação da Funcionalidade e Persistência
1. Abrir o SelectML. Clicar no menu *"Rotina de inspeção"*.
2. Marcar *"Listar todas as características"*. Verificar indicador visual ativo.
3. Reiniciar a aplicação.
4. **Critério de Aceitação:** O estado permanece marcado após o reinício.

### Cenário 2: Carga de Arquivo com Subconjunto de Características
1. Configurar lote/RunID existente no banco (ex: `705` ou `704`).
2. Descarregar arquivo contendo apenas 3 características de medição.
3. **Critério de Aceitação:**
   * A tabela exibe todas as características da rotina na ordem exata de `OrderNo`.
   * As 3 características do arquivo são exibidas com seus valores preenchidos.
   * As demais características da rotina aparecem listadas com o campo de valor aguardando inserção.
   * Não ocorrem duplicações de nomes.

### Cenário 3: Exibição de Características do Tipo Atributo
1. Carregar uma rotina que contenha características do tipo Atributo (ex: Run `704`, itens como `PJT //A` ou `MC VIS`).
2. **Critério de Aceitação:**
   * Na linha do atributo, a coluna de valor não exibe TextBox numérica.
   * Exibe um botão toggle sutil com *"Aprovado"* (verde).
   * Ao clicar, o toggle muda para *"Reprovado"* (vermelho).

### Cenário 4: Coloração por Limites de Tolerância
1. Inserir valor dentro de LSL e USL (ex: Nominal 12.4, LSL 12.3, USL 12.5; valor inserido 12.42).
   * **Critério de Aceitação:** Texto da linha/valor fica em **Verde**.
2. Inserir valor fora dos limites (ex: valor 12.60).
   * **Critério de Aceitação:** Texto da linha/valor fica em **Vermelho**.

### Cenário 5: Preenchimento Sequencial Serial Top-Down
1. Com características da rotina pendentes na tela, emitir leituras pela porta serial.
2. **Critério de Aceitação:**
   * O 1º dado preenche a primeira cota vazia de cima para baixo.
   * O 2º dado preenche a próxima cota vazia.
   * Ao preencher todas as cotas da rotina, dados seriais adicionais são anexados no rodapé como novas características.

### Cenário 6: Comando Desfazer via Hardware Serial
1. Emitir comando de desfazer com características adicionais presentes: a última cota adicional é removida da tabela.
2. Emitir comando de desfazer quando restarem apenas características da rotina do banco:
   * **Critério de Aceitação:** A linha da cota **permanece na tabela**, apenas o valor medido é esvaziado.
