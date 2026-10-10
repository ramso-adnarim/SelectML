# Plano de Implementação: Listagem e Validação Completa de Características (V1.2.8 - Item 2)

**Versão Alvo:** 1.2.8  
**Tipo:** Nova Funcionalidade / Integração com Banco de Dados / Refinamento de UI  
**Status:** Planejamento Revisado e Alinhado com o Usuário  
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
* A partir do nome da corrida / lote (`RunName` / `BatchNumber`), a aplicação consulta a rotina completa no MeasurLink e exibe **todas as características daquela rotina de inspeção em sua ordem sequencial exata (`OrderNo ASC`)**.
* As cotas que já vieram medidas no arquivo de texto são automaticamente reconhecidas e preenchidas.
* As cotas que ainda não foram medidas permanecem listadas em tela com o indicativo *"Aguardando medição..."*, prontas para preenchimento manual ou via instrumentos seriais (de cima para baixo).
* Características do tipo **Atributo** recebem um controle visual ergonômico e intuitivo (botão toggle horizontal sutil para alternar entre **Aprovado** e **Reprovado**), dispensando caixas de texto numéricas.
* As tolerâncias de engenharia (`Nominal`, `LSL`, `USL`) e quantidade de casas decimais (`DecimalLength`) são extraídas e utilizadas para destacar em **verde** os valores aprovados e em **vermelho** os valores fora de especificação.

---

## 2. Requisitos Detalhados do Item 2

### Requisito 2.1: Menu Superior e Controle de Ativação
* Na barra de menus principal da aplicação (`MainWindow.xaml`), ao lado direito do menu *"Modificador de nomes"*, deve ser inserido um novo menu com rótulo descritivo **"Configuração"**.
* Ao clicar no menu, será exibida a opção checkable **"Listar todas as características"**, funcionando como um toggle (liga/desliga).
* Sem indicador visual de círculo pulsante na barra superior para este menu (mantendo visual limpo).
* O estado deve ser persistido nas configurações locais da aplicação (`appsettings.json`) através do `AppConfig.cs` e `ConfigService.cs` (propriedade `ListAllFeatures`, padrão: `false` para preservar retrocompatibilidade).

### Requisito 2.2: Ciclo de Processamento e Consulta Otimizada de Características
* Quando a opção estiver ativada (`ListAllFeatures == true`), o processamento de arquivo ou entrada manual valida o identificador da corrida.
* **Identificador de Entrada:** O que vem no arquivo de texto ou o que o usuário digita no cabeçalho é **sempre o `RunName`** (número do lote / corrida, ex: `"067931-102026"` ou `"T1"`). O `RunID` é uma chave primária interna do MeasurLink.
* **Consulta SQL Otimizada:** A consulta recebe diretamente `@RunName`, resolve internamente o `RunID` ativo (ou o mais recente de `Run`) e extrai as características ordenadas por `OrderNo ASC` em um único comando de alta performance:
  ```sql
  DECLARE @RunName VARCHAR(100) = @BatchNumber;
  DECLARE @TargetRunID INT;

  -- 1. Prioriza a corrida ativa em ActiveRun
  SELECT TOP 1 @TargetRunID = RunID 
  FROM dbo.ActiveRun WITH (NOLOCK) 
  WHERE RunName = @RunName;

  -- 2. Fallback para histórico de Run (mais recente) caso não esteja em ActiveRun
  IF @TargetRunID IS NULL
  BEGIN
      SELECT TOP 1 @TargetRunID = RunID 
      FROM dbo.Run WITH (NOLOCK) 
      WHERE RunName = @RunName 
      ORDER BY RunID DESC;
  END

  -- 3. Extração completa e ordenada das características com limites e decimais
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
  WHERE fr.RunID = @TargetRunID
  ORDER BY fr.OrderNo ASC;
  ```
* **Mesclagem Inteligente de Dados:**
  1. A lista final em tela respeitará rigorosamente a ordem `OrderNo ASC` retornada pelo banco.
  2. Características presentes tanto no banco quanto no arquivo de texto de entrada são preenchidas com o valor medido do arquivo, marcadas como reconhecidas (`IsRecognized = true`) e associadas aos limites de tolerância.
  3. Características presentes no banco mas **ausentes** no arquivo de texto são inseridas na lista com seu respectivo nome, limites e tipo, com o campo de valor no estado **Aguardando Inserção** (`HasValue = false`), exibindo o placeholder *"Aguardando medição..."*.
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
  * Na geração do CSV de saída para o MeasurLink (`SELECTML.itpl`), grava **`0` para Aprovado** (0 defeitos) e **`1` para Reprovado** (1 defeito).

### Requisito 2.4: Destacar Valores Conformes e Não Conformes com Cores (Tolerâncias)
* A consulta extrai `Nominal`, `LSL` (Limite Inferior), `USL` (Limite Superior) e `DecimalLength`.
* **Regra de Coloração Visual:**
  * **Dentro das Tolerâncias:** O texto da linha/valor é exibido na cor **Verde** (`Brush.Status.Success`).
  * **Fora das Tolerâncias:** Se o valor medido for inferior ao `LSL` ou superior ao `USL`, o texto é destacado em **Vermelho** (`Brush.Status.Error`).
  * **Atributos:** Se "Aprovado" -> Verde; se "Reprovado" -> Vermelho.
  * **Cotas Sem Tolerância Cadastrada:** Exibidas na cor padrão neutra (`Brush.Text.Primary` / `Brush.Action.Primary`).
  * **Cotas Aguardando Valor:** Exibidas em estado neutro/placeholder suave (não devem ser marcadas como erro antes de serem medidas).
* **Comportamento Não Bloqueante:** A indicação em vermelho serve como alerta visual imediato ao inspetor da qualidade, não impedindo o envio caso a empresa permita arquivamento de peças reprovadas.

### Requisito 2.5: Digitação Manual, Watermark e Ressincronização Segura
* **Placeholder "Aguardando medição...":** Na coluna Valor de cotas variáveis vazias, exibe um texto indicativo estilo watermark em fonte suave/itálica. Ao focar ou clicar na célula, o texto desaparece imediatamente permitindo digitação direta.
* **Exclusão de Linha Mantida:** Se o operador não desejar propositalmente medir uma cota da rotina, ele pode clicar com o botão direito e selecionar *"Excluir valor"*, removendo a linha da visualização atual.
* **Ressincronização com Preservação de Valores:**
  * A lista de cotas da rotina só é recarregada do banco se o operador alterar o campo de lote (`BatchNumber`).
  * Caso o operador modifique o número do lote (mesmo por engano), ao consultar e reconstruir a rotina para o novo lote, o sistema **preserva todos os valores já medidos, digitados ou recebidos via serial** que coincidam com características da nova rotina, evitando qualquer retrabalho ou perda de dados.

### Requisito 2.6: Preenchimento Sequencial via Comunicação Serial (Top-Down)
* Quando instrumentos seriais enviarem medições para uma lista com cotas da rotina pendentes:
  * A medição serial preenche sequencialmente a **primeira característica do tipo Variável da rotina que ainda não possua valor medido**, de cima para baixo.
  * O valor inserido é formatado, validado e tem sua cor atualizada instantaneamente.
* Caso o usuário continue enviando medições seriais após **todas** as características da rotina já estarem preenchidas:
  * O sistema cria características adicionais no final da tabela (comportamento da Fase 1).

### Requisito 2.7: Desfazer Inteligente (Serial Undo / U-WAVE)
* Ao receber o comando de desfazer serial (hardware trigger U-WAVE ou botão de desfazer):
  1. Se houver características adicionais (além das pertencentes à rotina do banco de dados), a linha adicional mais recente é **excluída** da tabela.
  2. Se o desfazer alcançar características que fazem parte da **rotina original do banco de dados**, a linha **NÃO deve ser removida**. O sistema deve apenas **limpar o valor medido** (`HasValue = false`, `Value = null`, voltando ao estado de placeholder *"Aguardando medição..."*), mantendo a característica visível e disponível para nova medição.

---

## 3. Mapeamento de Decisões Técnicas Alinhadas com o Usuário

### ✅ P1: Resolução de Lote via `RunName` Direto na Consulta SQL
* **Definição:** O arquivo de texto ou o operador sempre fornecerá o `RunName` (lote cadastrado). O `RunID` é chave interna.
* **Implementação:** A consulta SQL otimizada recebe diretamente `@RunName` e resolve o `RunID` correspondente internamente (via `ActiveRun` com fallback para `Run`). Não há necessidade de método intermediário no C#, simplificando o `DatabaseService` para uma única chamada `GetInspectionRoutineFeaturesAsync(string runName)`.

### ✅ P2: Exportação de Características do Tipo Atributo no CSV
* **Definição:** Características do tipo atributo gravam no CSV final `0` para Aprovado (0 defeitos) e `1` para Reprovado (1 defeito), em plena conformidade com o template de importação do MeasurLink (`SELECTML.itpl` e tabela `AttFeatureRunData`).

### ✅ P3: Placeholder "Aguardando medição...", Exclusão Opcional e Preservação de Valores
* **Definição:**
  1. A coluna de valor exibe o placeholder *"Aguardando medição..."* que desaparece ao foco/clique para digitação.
  2. O operador pode excluir linhas da rotina através do menu de contexto (*"Excluir valor"*).
  3. A lista só é ressincronizada se o campo `BatchNumber` for alterado.
  4. Na alteração de lote, os valores já digitados/medidos são preservados nas características correspondentes.

### ✅ P4: Tolerâncias, Cores e Modo Automático
* **Definição:**
  1. Destaque em verde para valores dentro dos limites e vermelho para fora.
  2. O auto-envio do `IsAutoMode` é suspenso se houver cotas pendentes na rotina, permitindo inspeção manual/serial.

---

## 4. Arquitetura da Solução e Diagramas

```mermaid
graph TD
    subgraph "Interface e Configuração"
        MenuConfig["Menu: Configuração<br/>[x] Listar todas as características"]
        MenuConfig --> AppConfig["appsettings.json (ListAllFeatures)"]
    end

    subgraph "Entrada de Dados"
        InputSource["Arquivo (CSV/JSON/PDF) ou Digitação de Lote"] --> ExtractBatch["BatchNumber (RunName)"]
    end

    subgraph "DatabaseService (MeasurLink)"
        ExtractBatch --> SQLQuery["Query Unificada: Resolve TargetRunID por RunName<br/>+ Extrai FeatureRun, Feature e FeatureProperties"]
        SQLQuery --> FeatureList["Lista da Rotina (InspectionFeatureInfo)"]
    end

    subgraph "Mesclagem e Preservação em MainViewModel"
        FeatureList --> MergeProcess{"ListAllFeatures Ativo?"}
        MergeProcess -->|NÃO| LegacyMode["Modo Legado (Apenas cotas do arquivo)"]
        MergeProcess -->|SIM| OrderedMerge["Monta MeasuredResults (OrderNo ASC):<br/>- Preserva medições já existentes (se houver)<br/>- Preenche cotas lidas do arquivo<br/>- Cotas pendentes: 'Aguardando medição...'<br/>- Atributos: Toggle Aprovado/Reprovado"]
    end

    subgraph "Entradas Complementares (Serial / UI)"
        SerialData["Medição Serial Recebida"] --> FindPending{"Há cota da rotina sem valor?"}
        FindPending -->|SIM| FillPending["Preenche a próxima cota da rotina (Top-Down)"]
        FindPending -->|NÃO| AppendExtra["Adiciona como cota adicional no final"]
        
        UndoCmd["Comando Desfazer (U-WAVE)"] --> CheckExtra{"É cota adicional?"}
        CheckExtra -->|SIM| RemoveRow["Remove linha da tabela"]
        CheckExtra -->|NÃO (Cota do Banco)| ClearValueOnly["Limpa apenas o valor medido (Mantém a linha)"]
    end

    subgraph "Exibição e Saída"
        OrderedMerge --> GridDisplay["DataGrid com Cores:<br/>Verde = Conforme | Vermelho = Não Conforme"]
        GridDisplay --> SendAction["Enviar -> CSV Único (SELECTML.itpl):<br/>Variáveis: Valores | Atributos: 0 ou 1"]
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
* `public double? Value`: Valor medido numérico (nullable para representar ausência de medição).
* `public bool HasValue`: Indica se o item já foi preenchido.
* `public short FeatureType`: Tipo da característica (1 = Variável, 2/4 = Atributo, etc.).
* `public bool IsAttribute`: Identifica itens de atributo para o template de UI.
* `public bool IsApproved`: Estado booleano para atributos (padrão: `true`).
* `public double? Nominal`, `public double? LSL`, `public double? USL`: Limites de tolerância.
* `public int? DecimalLength`: Casas decimais para máscara de formatação.
* `public bool IsFromDbRoutine`: Indica se o item pertence à rotina de inspeção do banco de dados (protegendo a linha contra deleção no Undo).
* `public ToleranceStatus Status`: Propriedade computada (`Conforming`, `OutOfTolerance`, `NotEvaluated`, `Pending`) que dita a cor do texto na interface (Verde/Vermelho/Neutro).

### 5.3 Extensão da Interface e Serviço de Banco de Dados
Localização: `SelectML.Core/IDatabaseService.cs` e `SelectML.Client/Services/DatabaseService.cs`
* `Task<List<InspectionFeatureInfo>> GetInspectionRoutineFeaturesAsync(string runName);`

### 5.4 Interface Gráfica (`MainWindow.xaml`)
1. **Menu "Configuração":**
   Adicionado imediatamente à direita de *"Modificador de nomes"*, com sub-item checkable *"Listar todas as características"*.
2. **Coluna "Valor" no DataGrid:**
   * Se `IsAttribute == true`: Exibe botão Toggle horizontal moderno estilizado (estilo pill switch) alternando entre *"Aprovado"* (verde) e *"Reprovado"* (vermelho) com um clique.
   * Se `IsAttribute == false`: Exibe TextBox com watermark *"Aguardando medição..."* quando `HasValue == false`. Ao receber foco/digitação ou medição serial, exibe o valor formatado com binding de cor de texto para Verde/Vermelho conforme o status de tolerância.

---

## 6. Roteiro de Testes e Validação de QA

### Cenário 1: Ativação da Funcionalidade e Persistência
1. Abrir o SelectML. Clicar no menu *"Configuração"*.
2. Marcar *"Listar todas as características"*.
3. Reiniciar a aplicação.
4. **Critério de Aceitação:** O estado permanece marcado após o reinício.

### Cenário 2: Carga de Arquivo com Subconjunto de Características
1. Configurar lote existente no banco (ex: `"067931-102026"` ou `"067919-102026"`).
2. Descarregar arquivo contendo apenas 3 características de medição.
3. **Critério de Aceitação:**
   * A tabela exibe todas as características da rotina na ordem exata de `OrderNo`.
   * As 3 características do arquivo são exibidas com seus valores preenchidos.
   * As demais características da rotina aparecem listadas com o indicativo *"Aguardando medição..."*.
   * Não ocorrem duplicações de nomes.

### Cenário 3: Exibição de Características do Tipo Atributo
1. Carregar uma rotina que contenha características do tipo Atributo (ex: lote com itens como `PJT //A` ou `MC VIS`).
2. **Critério de Aceitação:**
   * Na linha do atributo, a coluna de valor não exibe TextBox numérica.
   * Exibe um botão toggle sutil com *"Aprovado"* (verde).
   * Ao clicar, o toggle muda para *"Reprovado"* (vermelho).
   * No CSV final gerado, o valor exportado é `0` (Aprovado) ou `1` (Reprovado).

### Cenário 4: Coloração por Limites de Tolerância
1. Inserir valor dentro de LSL e USL:
   * **Critério de Aceitação:** Texto da linha/valor fica em **Verde**.
2. Inserir valor fora dos limites:
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
   * **Critério de Aceitação:** A linha da cota **permanece na tabela**, apenas o valor medido é esvaziado, voltando a exibir *"Aguardando medição..."*.

### Cenário 7: Exclusão Opcional de Linha e Preservação de Valores ao Trocar Lote
1. Excluir uma linha da rotina via clique com botão direito (*"Excluir valor"*).
2. Modificar o número do lote no cabeçalho.
3. **Critério de Aceitação:**
   * A rotina é recarregada para o novo lote.
   * Todas as cotas com medições já existentes têm seus valores preservados.
