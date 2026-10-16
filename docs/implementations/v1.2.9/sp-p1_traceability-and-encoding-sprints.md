# Planejamento de Sprints: Rastreabilidade e Correções (V1.2.9 - Fase 1)

**Versão Alvo:** 1.2.9  
**Tipo:** Sprint Planning / Roteiro de Execução Técnica  
**Status:** Planejamento e Arquitetura para Alinhamento  
**Data:** 16/10/2026  
**Documento Técnico de Referência:** [ip-p1_traceability-and-encoding-bugfix.md](file:///c:/Antigravity/SelectML/docs/implementations/v1.2.9/ip-p1_traceability-and-encoding-bugfix.md)  
**Branch de Desenvolvimento:** `feature/v1.2.9-traceability-and-bugfixes` (baseada em `release/v1.2.9`)  

---

## 1. Estratégia de Desenvolvimento

Para garantir entregas incrementais, testabilidade independente e risco zero de regressão, o desenvolvimento da **Fase 1 da V1.2.9** está estruturado em **4 Sprints modulares**:

```mermaid
graph LR
    S1[Sprint 1: BUG FIX<br/>Encoding UTF-16 sem BOM] --> S2[Sprint 2: FEATURE CORE<br/>Modelos, Persistência & CSV Linha 3]
    S2 --> S3[Sprint 3: FEATURE UI<br/>Menu & Painel Popover IMG3]
    S3 --> S4[Sprint 4: QA & HOMOLOGAÇÃO<br/>Testes E2E com Arquivos Reais]
```

As sprints estão estritamente segregadas entre a **Correção do Bug** (Sprint 1) e a **Implementação da Nova Funcionalidade** (Sprints 2 e 3), culminando na **Homologação e Não-Regressão** (Sprint 4).

---

## 2. Detalhamento das Sprints

### 🐞 Sprint 1: Correção de Bug - Motor de Detecção de Encodings e Suporte a UTF-16 sem BOM
> **Objetivo:** Reformular o utilitário `EncodingDetector.cs` em `SelectML.Core` para detectar de forma transparente e resiliente arquivos codificados em UTF-16 LE e UTF-16 BE sem Byte Order Mark (BOM), além de proteger contra UTF-32 e higienizar caracteres nulos residuais, permitindo que arquivos de texto gerados por máquinas industriais (como `EFF2103.15A.CSV`) sejam perfeitamente interpretados tanto no editor de templates quanto no motor de execução.

* **Arquivos Afetados:**
  * `SelectML.Core/EncodingDetector.cs`
  * `SelectML.Core/Templates/TemplateExecutionEngine.cs`
* **Tarefas Técnicas:**
  - [ ] Implementar verificação de BOMs de 4 bytes para UTF-32 LE (`FF FE 00 00`) e UTF-32 BE (`00 00 FE FF`) no início de `EncodingDetector.Detect()`.
  - [ ] Implementar algoritmo heurístico estatístico de análise de bytes nulos (`0x00`) para detecção de UTF-16 LE / BE sem preâmbulo BOM:
    - Inspecionar a distribuição de bytes `0x00` em amostra de até 2048 bytes.
    - Se a proporção de `0x00` em bytes ímpares for > 70% e em bytes pares < 5%, identificar confiavelmente como `Encoding.Unicode` (UTF-16 LE).
    - Se a proporção de `0x00` em bytes pares for > 70% e em bytes ímpares < 5%, identificar confiavelmente como `Encoding.BigEndianUnicode` (UTF-16 BE).
  - [ ] Adicionar sanitização defensiva em `DetectAndReadAllText` e `DetectAndReadAllLines` para remover eventuais caracteres nulos residuais (`\0`).
  - [ ] Testar a leitura programática direta do arquivo real `C:\SelectML\EFF2103.15A.CSV` comprovando que o texto lido coincide 100% com `EFF2103.15A - Copia.CSV`.
* **Critério de Aceitação:**
  * O arquivo `C:\SelectML\EFF2103.15A.CSV` é carregado e lido diretamente como UTF-16 LE sem necessidade de salvamento prévio no Bloco de Notas.
  * A tela de Configuração de Templates renderiza os caracteres sem espaçamento anômalo e identifica perfeitamente as cotas e valores.

---

### 📦 Sprint 2: Nova Funcionalidade - Modelos de Rastreabilidade, Persistência e Exportação CSV
> **Objetivo:** Criar os modelos de dados para rastreabilidade em `SelectML.Core`, implementar a persistência de pares chave/valor em `AppConfig.cs` (`appsettings.json`), e atualizar a lógica de geração do arquivo CSV final enviado ao MeasurLink (`GenerateOutputCsv`) para injetar os dados de rastreabilidade na terceira linha.

* **Arquivos Afetados:**
  * `SelectML.Core/Models/TraceabilityItem.cs` (Novo arquivo)
  * `SelectML.Client/Services/AppConfig.cs`
  * `SelectML.Client/ViewModels/MainViewModel.cs`
* **Tarefas Técnicas:**
  - [ ] Criar a classe de modelo `TraceabilityItem` em `SelectML.Core`:
    - Propriedades `Key` (Nome) e `Value` (Valor), com suporte a `INotifyPropertyChanged` para atualização instantânea na UI.
  - [ ] Atualizar `AppConfig.cs`:
    - Adicionar propriedade `public List<TraceabilityItem> Traceabilities { get; set; } = new();`.
    - Garantir compatibilidade retroativa para JSONs legados sem a chave.
  - [ ] Atualizar o gerador de CSV em `MainViewModel.GenerateOutputCsv`:
    - Avaliar se existem rastreabilidades cadastradas com `Key` não vazio.
    - Se existirem, sanitizar delimitadores proibidos (substituir `;` e remover quebras de linha `\r\n`).
    - Injetar a linha de rastreabilidade na **Linha 3**:  
      `<nome>;<valor>;<nome>;<valor>;...`
    - Deslocar a linha de características para a **Linha 4** e a de leituras para a **Linha 5**.
    - Se a lista estiver vazia, manter a estrutura legada de 4 linhas (conforme Decisão D1 do Plano de Implementação).
* **Critério de Aceitação:**
  * Ao processar uma medição com rastreabilidades cadastradas, o CSV gerado contém a linha 3 exatamente no padrão `<nome>;<valor>;...`, sem corrupção de delimitadores.

---

### 🎨 Sprint 3: Nova Funcionalidade - Menu Superior "Rastreabilidade" e Painel Popover
> **Objetivo:** Implementar o menu "Rastreabilidade" na barra superior de `MainWindow.xaml` (à direita de "Modificador de nomes" e antes de "Configuração"), com painel suspenso (Popover) interativo estritamente fiel ao design da *IMG3* (cabeçalho azul, grade com colunas Nome e Valor, botões de exclusão e botão de adição `+`).

* **Arquivos Afetados:**
  * `SelectML.Client/MainWindow.xaml`
  * `SelectML.Client/ViewModels/MainViewModel.cs`
* **Tarefas Técnicas:**
  - [ ] Atualizar a barra de menus em `MainWindow.xaml`:
    - Reposicionar os menus: `Porta Serial`, `Modificador de nomes`, **`Rastreabilidade`**, `Configuração`.
    - Adicionar o indicador luminoso circular verde (`TraceabilityIndicator`) ativado quando houver itens válidos preenchidos.
  - [ ] Desenvolver o componente Popover / Submenu suspenso ancorado:
    - **Banner Superior:** Fundo azul primário (`#2563EB`), cantos superiores arredondados e texto em negrito "Rastreabilidade".
    - **Cabeçalhos de Coluna:** Rótulos "Nome" e "Valor" com tipografia nítida e espaçamento proporcional.
    - **Linhas Dinâmicas:** `ItemsControl` bindado à `ObservableCollection<TraceabilityItem>` do `MainViewModel`.
    - **Campos de Entrada:** `TextBox` estilizador para digitação rápida de Nome e Valor com navegação por tecla `Tab`.
    - **Botão de Exclusão (✕):** Botão discreto à direita de cada linha para remoção do item da coleção.
    - **Botão de Adição (+):** Botão estilizado centralizado no rodapé do painel para inclusão de nova linha de rastreabilidade.
  - [ ] Garantir resiliência de foco:
    - O submenu deve permanecer aberto durante a digitação e cliques nos botões internos (`+` e `✕`), fechando apenas ao clicar fora do componente ou pressionar `Esc`.
  - [ ] Persistência em tempo real:
    - Qualquer inclusão, alteração de texto ou remoção atualiza a coleção e aciona a gravação automática no `appsettings.json`.
* **Critério de Aceitação:**
  * O menu superior exibe "Rastreabilidade" na posição solicitada.
  * O clique abre o painel conforme o leiaute da *IMG3*.
  * O operador adiciona, digita e remove itens de rastreabilidade de forma fluida e sem fechamento prematuro do menu.
  * Ao reiniciar a aplicação, os valores digitados continuam intactos.

---

### 🧪 Sprint 4: Homologação Integrada, Testes com Arquivos Reais e Não-Regressão
> **Objetivo:** Executar a validação completa de ponta a ponta (E2E), incluindo o teste de arquivos reais da máquina cliente (`EFF2103.15A.CSV`), teste de não-regressão das funcionalidades da V1.2.8 (Templates, Serial U-WAVE, Rotinas) e validação da exportação do CSV para o MeasurLink.

* **Tarefas Técnicas:**
  - [ ] **Teste E2E 1 - Bug de Encoding:** Carregar `C:\SelectML\EFF2103.15A.CSV` no visualizador de templates e validar a exibição dos caracteres corretos sem espaços.
  - [ ] **Teste E2E 2 - Extração por Template:** Executar o motor de extração sobre `EFF2103.15A.CSV` e validar o retorno correto de `MeasurementData`.
  - [ ] **Teste E2E 3 - Rastreabilidade UI:** Cadastrar múltiplos pares de rastreabilidade (ex: `Série = Metrologia`, `Operador = João`, `Turno = T1`), fechar e reabrir o SelectML conferindo a restauração.
  - [ ] **Teste E2E 4 - Geração do CSV Final:** Processar arquivo com rastreabilidade ativa e verificar que a Linha 3 contém `<nome>;<valor>;...` e que as linhas de cotas e leituras estão nas posições 4 e 5.
  - [ ] **Teste E2E 5 - Modo Legado sem Rastreabilidade:** Processar arquivo sem rastreabilidade e verificar a preservação do layout legado de 4 linhas.
  - [ ] **Teste E2E 6 - Não Regressão:** Validar funcionamento contínuo do leitor serial U-WAVE, do modificador de nomes e da listagem de características da rotina.
* **Critério de Aceitação:**
  * 100% dos testes aprovados com 0 erros de compilação, 0 exceções não tratadas e conformidade total com os requisitos da V1.2.9.
