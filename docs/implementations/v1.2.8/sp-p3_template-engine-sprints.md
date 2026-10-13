# Planejamento de Sprints: Mecanismo de Templates para Arquivos de Texto/CSV (V1.2.8 - Item 3)

**Versão Alvo:** 1.2.8  
**Tipo:** Sprint Planning / Roteiro de Execução Técnica  
**Status:** Planejamento Inicial Concluído (Aguardando Aprovação para Execução)  
**Data:** 12/10/2026  
**Documento Técnico de Referência:** [ip-p3_template-engine.md](file:///c:/Antigravity/SelectML/docs/implementations/v1.2.8/ip-p3_template-engine.md)  
**Branch de Desenvolvimento:** `feature/v1.2.8-template-engine`

---

## 1. Estratégia de Desenvolvimento

Para viabilizar a entrega progressiva, com testes de isolamento a cada etapa e risco nulo de regressão sobre o ecossistema existente, a implementação do **Mecanismo de Templates (Item 3 da V1.2.8)** está estruturada em **6 Sprints modulares**:

```mermaid
graph LR
    S1[Sprint 1:<br/>Modelos, Engine & Plugin DLL] --> S2[Sprint 2:<br/>UI ComboBox & Botão ⚙️]
    S2 --> S3[Sprint 3:<br/>Janela de Configuração Base]
    S3 --> S4[Sprint 4:<br/>Visualizador, Cores & Regex]
    S4 --> S5[Sprint 5:<br/>Validação, Fallback & Gravação]
    S5 --> S6[Sprint 6:<br/>Homologação E2E & QA]
```

Cada sprint possui escopo delimitado, tarefas rastreáveis por checklist, arquivos afetados e critérios de aceitação objetivos.

---

## 2. Detalhamento das Sprints

### 🏁 Sprint 1: Modelos de Template, Motor de Execução e Plugin DLL
> **Objetivo:** Criar os modelos de dados de template em `SelectML.Core`, implementar a biblioteca de classes do novo plugin `SelectML.Parsers.TextTemplate` que implementa `IMachineParser`, desenvolver o motor de extração determinístico por regex/tokens e o serviço de persistência JSON em disco.

* **Arquivos Afetados:**
  * `SelectML.Core/Templates/TemplateDefinition.cs` (Novo arquivo)
  * `SelectML.Core/Templates/TemplateLineRule.cs` (Novo arquivo)
  * `SelectML.Core/IConfigurableParser.cs` (Novo arquivo)
  * `SelectML.Parsers.TextTemplate/SelectML.Parsers.TextTemplate.csproj` (Novo projeto)
  * `SelectML.Parsers.TextTemplate/TextTemplateParser.cs` (Novo arquivo)
  * `SelectML.Parsers.TextTemplate/TemplateExecutionEngine.cs` (Novo arquivo)
  * `SelectML.Client/Services/TemplateRepository.cs` (Novo arquivo)
  * `SelectML.sln` (Inclusão do novo projeto)
* **Tarefas Técnicas:**
  - [ ] Criar a estrutura de modelos `TemplateDefinition` e `TemplateLineRule` em `SelectML.Core.Templates`.
  - [ ] Criar a interface `IConfigurableParser : IMachineParser` para identificação de parsers parametrizáveis pela UI.
  - [ ] Criar o projeto de biblioteca de classes `.NET 8` `SelectML.Parsers.TextTemplate` configurado com `OutputPath` para `..\SelectML.Client\bin\$(Configuration)\net8.0-windows\Plugins\`.
  - [ ] Implementar `TemplateExecutionEngine` capaz de ler arquivos com encoding flexível e extrair:
    - `PartName` (por regra ou fallback de arquivo)
    - `BatchNumber` (por regra ou fallback de arquivo)
    - Dicionário de características e valores medidos numéricos (`double`).
  - [ ] Implementar `TextTemplateParser : IConfigurableParser, INotifyPropertyChanged` com suporte a template ativo e nome dinâmico.
  - [ ] Implementar `TemplateRepository` em `SelectML.Client` para salvar e carregar arquivos JSON na pasta `%APPDATA%\SelectML\Templates`.
* **Critério de Aceitação:**
  * O projeto compila com sucesso, gera `Plugins\SelectML.Parsers.TextTemplate.dll` e um teste unitário executa o parsing de um CSV de amostra com regras em memória retornando `MeasurementData` válido.

---

### 🎨 Sprint 2: Integração com a Tela Principal (`MainWindow`) e ComboBox "Equipamento"
> **Objetivo:** Integrar o leitor de templates à tela principal, exibir o botão de engrenagem ⚙️ condicionalmente quando o leitor estiver selecionado, atualizar dinamicamente o texto da ComboBox para refletir o template ativo e persistir as escolhas no `appsettings.json`.

* **Arquivos Afetados:**
  * `SelectML.Client/MainWindow.xaml`
  * `SelectML.Client/ViewModels/MainViewModel.cs`
  * `SelectML.Client/Services/AppConfig.cs`
* **Tarefas Técnicas:**
  - [ ] Em `AppConfig.cs`:
    - Adicionar a propriedade `public string LastTemplateName { get; set; } = string.Empty;`.
  - [ ] Em `MainWindow.xaml`:
    - No `Grid` da seção "Equipamento", adicionar uma coluna intermediária para o botão de configuração:
      - Botão com ícone ⚙️, estilo `IconButton`, posicionado à esquerda do botão "✖".
      - Binding de visibilidade atrelado a `IsTemplateParserSelected`.
      - Binding de habilitação atrelado a `IsConfigEnabled`.
      - Comando: `OpenTemplateConfigCommand`.
  - [ ] Em `MainViewModel.cs`:
    - Adicionar propriedade booleana `IsTemplateParserSelected` atualizada ao alterar `SelectedParser`.
    - Ao selecionar `TextTemplateParser`, carregar o último template utilizado a partir de `config.LastTemplateName`.
    - Garantir que o nome exibido no ComboBox reflita `"Leitor de arquivos de texto - [NOME DO TEMPLATE]"`.
    - Salvar `LastTemplateName` no `appsettings.json` quando um template for definido.
* **Critério de Aceitação:**
  * Ao selecionar outros plugins (ex: ViciVision, Zeiss), o botão ⚙️ permanece invisível.
  * Ao selecionar "Leitor de arquivos de texto", o botão ⚙️ surge suavemente entre a ComboBox e o botão "✖".
  * Ao iniciar o monitoramento, o botão ⚙️ fica desabilitado junto com os demais controles de configuração.

---

### 🪟 Sprint 3: Interface Gráfica da Janela de Configuração (`TemplateConfigWindow`)
> **Objetivo:** Desenvolver a estrutura completa da nova janela modal "Configuração de Templates" em WPF MVVM, contendo o gerenciador de biblioteca de templates (painel esquerdo) e cabeçalho de configurações (painel superior direito).

* **Arquivos Afetados:**
  * `SelectML.Client/Views/TemplateConfigWindow.xaml` (Novo arquivo)
  * `SelectML.Client/Views/TemplateConfigWindow.xaml.cs` (Novo arquivo)
  * `SelectML.Client/ViewModels/TemplateConfigViewModel.cs` (Novo arquivo)
* **Tarefas Técnicas:**
  - [ ] Criar `TemplateConfigWindow.xaml` com design escuro/claro nativo, dimensões ergonômicas (mínimo 960x650) e cabeçalho de título "Configuração de Templates".
  - [ ] Implementar o painel esquerdo "Biblioteca de Templates":
    - Barra de ferramentas com botões de ação: Novo Template (➕), Duplicar (📋), Excluir (🗑️).
    - `ListBox` ou `TreeView` estilizada listando templates cadastrados.
  - [ ] Implementar o painel superior direito:
    - Campos de configuração: Nome do Template, Extensão de Arquivo (`.csv`, `.txt`, `.dat`), Codificação (`UTF-8`, `Latin1/ANSI`, `ASCII`), Separador Geral.
    - Tabela resumida das regras de linha cadastradas (`DataGrid` leve com Linha, Função, Padrão/Separador e Status).
  - [ ] Integrar abertura modal pelo `MainViewModel` no comando `OpenTemplateConfigCommand`.
* **Critério de Aceitação:**
  * Clicar no botão ⚙️ na tela principal abre a janela de configuração centralizada e responsiva.
  * Usuário consegue criar novo template com nome personalizado, alternar entre templates salvos e excluir templates existentes com diálogo de confirmação.

---

### ✏️ Sprint 4: Visualizador de Texto Interativo, Marcação por Cores e Geração de Regras
> **Objetivo:** Construir o componente de visualização de texto interativo no painel inferior direito com botão "Procurar...", suporte a seleção de trechos com o mouse, menu de contexto com cores de destaque e motor de geração automática de regras e REGEX.

* **Arquivos Afetados:**
  * `SelectML.Client/Views/TemplateConfigWindow.xaml`
  * `SelectML.Client/ViewModels/TemplateConfigViewModel.cs`
  * `SelectML.Client/Services/TemplateRuleGenerator.cs` (Novo arquivo)
* **Tarefas Técnicas:**
  - [ ] Implementar o visualizador de arquivo de amostra:
    - Campo de caminho com botão "Procurar..." (diálogo de arquivo aberto para `.csv`, `.txt`, `*.*`).
    - Exibição do texto em área estilizada de fonte monoespaçada com numeração lateral de linhas.
    - Respeito à codificação selecionada (UTF-8 / Latin-1) ao carregar a prévia.
  - [ ] Implementar seleção de texto e menu de contexto (botão direito):
    - Opções: "Nome da corrida (lote)", "Nome da rotina", "Nome da característica", "Valor medido", "Definir separador desta linha...", "Remover marcação".
  - [ ] Implementar mecanismo de coloração visual sobre os trechos marcados:
    - Vermelho: Nome da rotina
    - Roxo: Nome da corrida
    - Azul: Nome da característica
    - Verde: Valor medido
  - [ ] Implementar `TemplateRuleGenerator`:
    - Ao selecionar uma linha de características/valores e o separador, gera a regra de tokenização por linha.
    - Ao selecionar trechos de texto fixo, gera expressões regulares com grupos de captura apropriados.
    - Alimenta automaticamente a tabela de regras de linhas do template.
* **Critério de Aceitação:**
  * Ao carregar um arquivo CSV de exemplo, o usuário seleciona uma linha com o mouse, clica com o botão direito e marca como "Nome da característica"; a linha é colorida em azul e a regra correspondente surge instantaneamente na tabela de regras.

---

### 💾 Sprint 5: Validação, Regras de Fallback (Nome do Arquivo) e Gravação
> **Objetivo:** Implementar o validador de integridade do template, a lógica de fallback automático para Nome da Rotina e Nome da Corrida, o diálogo de confirmação e a atualização imediata da seleção na tela principal.

* **Arquivos Afetados:**
  * `SelectML.Core/Templates/TemplateDefinition.cs`
  * `SelectML.Client/ViewModels/TemplateConfigViewModel.cs`
  * `SelectML.Client/ViewModels/MainViewModel.cs`
* **Tarefas Técnicas:**
  - [ ] Implementar método de validação rigoroso `ValidateTemplate()`:
    - Bloquear salvamento se Características ou Valores Medidos não estiverem configurados.
    - Se "Nome da rotina" não for selecionado no texto: ativar automaticamente flag `UseFileNameForPartName = true` e exibir aviso suave na tela ("Usará nome do arquivo").
    - Se "Nome da corrida" não for selecionado no texto: ativar automaticamente flag `UseFileNameForBatchNumber = true` e exibir aviso suave na tela ("Usará nome do arquivo").
  - [ ] Implementar persistência completa:
    - Salvar o template validado via `TemplateRepository`.
    - Ao clicar em "Salvar e Confirmar": fechar a janela, definir o template como ativo no `TextTemplateParser`, atualizar o rótulo do ComboBox no `MainViewModel` para `"Leitor de arquivos de texto - [NOME DO TEMPLATE]"` e gravar no `appsettings.json`.
* **Critério de Aceitação:**
  * Tentativa de salvar sem características exibe diálogo de alerta explicativo sem fechar a janela.
  * Template configurado sem rotina/lote salva com sucesso e confirma fallback para nome do arquivo.
  * Ao fechar a janela com sucesso, a tela principal atualiza imediatamente a ComboBox com o nome do template salvo.

---

### 🧪 Sprint 6: Homologação Integrada, Testes E2E e Não-Regressão
> **Objetivo:** Executar testes ponta a ponta com arquivos reais de medição, validar leitura em modo monitoramento, testar caracteres com acentos e símbolos de engenharia, e assegurar conformidade com a listagem de características (Item 2) e fila serial (Item 1).

* **Arquivos Afetados:**
  * `docs/implementations/v1.2.8/sp-p3_template-engine-sprints.md` (Checklist final)
  * Suíte de testes manuais e integrados
* **Tarefas Técnicas:**
  - [ ] Teste E2E 1: Arquivo CSV clássico (matriz horizontal separada por ponto e vírgula com cabeçalho e leituras).
  - [ ] Teste E2E 2: Arquivo com caracteres especiais (`Ø`, `°`, `µ`) validando codificação Latin1 vs UTF-8.
  - [ ] Teste E2E 3: Arquivo sem lote no texto -> Verificação se o lote assumiu o nome do arquivo conforme fallback.
  - [ ] Teste E2E 4: Monitoramento ativo (`Iniciar`) com recepção de arquivo via `WatchDirectory` -> Ingestão automática, preenchimento do DataGrid, coloração de tolerâncias (Item 2) e envio ao MeasurLink.
  - [ ] Teste E2E 5: Integração com instrumentos seriais (Item 1) completando cotas ausentes no template de texto.
  - [ ] Teste de Não-Regressão: Seleção dos parsers legados (ViciVision CSV, ViciVision JSON, Zeiss PDF) funcionando com 100% de integridade.
* **Critério de Aceitação:**
  * Arquivos CSV processados via template geram resultados idênticos aos plugins nativos específicos, com zero erros de execução e total estabilidade.

---

## 3. Matriz de Rastreabilidade dos Requisitos

| Requisito do Usuário | Sprint Responsável | Componente Principal |
| :--- | :--- | :--- |
| **1 - Plugin "Leitor de arquivos de texto"** | Sprint 1 | `SelectML.Parsers.TextTemplate.dll` |
| **2 - Botão ⚙️ ao lado da ComboBox** | Sprint 2 | `MainWindow.xaml` |
| **3 - Janela de Configuração com botão "Procurar"** | Sprint 3 e 4 | `TemplateConfigWindow.xaml` |
| **4 - Gestão de Múltiplos Templates salvos** | Sprint 1 e 3 | `TemplateRepository.cs` |
| **5 - Opção de nomear novo template** | Sprint 3 | `TemplateConfigViewModel.cs` |
| **6 - Marcação por trechos, cores e REGEX** | Sprint 4 | `TemplateRuleGenerator.cs` |
| **7 - Validação obrigatória e fallback para nome do arquivo** | Sprint 5 | `TemplateDefinition.IsValid()` |
| **8 - Rótulo "Leitor de arquivos de texto - [NOME]" no ComboBox** | Sprint 2 e 5 | `MainViewModel.cs` |
| **9 - Processamento normal no monitoramento de entrada** | Sprint 1 e 6 | `TemplateExecutionEngine.cs` |

---

## 4. Próximos Passos Imediatos

1. Apresentar os planos técnicos ([ip-p3_template-engine.md](file:///c:/Antigravity/SelectML/docs/implementations/v1.2.8/ip-p3_template-engine.md) e [sp-p3_template-engine-sprints.md](file:///c:/Antigravity/SelectML/docs/implementations/v1.2.8/sp-p3_template-engine-sprints.md)) ao usuário;
2. Discutir e alinhar as escolhas arquiteturais destacadas (localização da UI vs DLL, comportamento dinâmico do ComboBox e modelo de geração de regex);
3. Obter a aprovação formal do usuário para dar início à **Sprint 1**.
