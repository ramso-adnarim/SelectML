# Plano de Documentação e Faxina Geral (V1.1.0)

**Contexto:**
Com a finalização técnica da Versão 1.1.0 (Arquitetura Híbrida, Buffer, UI Renovada), precisamos preparar o terreno para a distribuição e manutenção futura. Este documento detalha a estratégia para elevar o nível da "Developer Experience" (DX) e garantir que a base de código seja facilmente navegável por humanos e agentes de IA.

**Objetivo:**
Garantir que qualquer desenvolvedor ou agente de IA possa compreender, manter e evoluir o SelectML sem fricção, através de documentação atualizada e código limpo/comentado em PT-BR.

---

## 1. Novo Documento: Mapa de Navegação para IA (`docs/AI_CODEBASE_MAP.md`)

**Objetivo:** Criar um índice técnico otimizado para eficiência de janela de contexto de LLMs.

**Conteúdo Planejado:**
- **Mapeamento Arquivo -> Responsabilidade:** Lista concisa descrevendo a responsabilidade única de cada arquivo importante (ex: `SerialService.cs`: Gerencia conexão e buffer de dados seriais).
- **Árvore de Dependências:** Representação visual ou textual de fluxo de chamadas principais (ex: `MainWindow` -> `MainViewModel` -> `SerialService` -> `SerialDeviceStrategy`).
- **Glossário Técnico:** Definição de termos do domínio (ex: "Buffer Reverso", "Feature", "Run", "U-WAVE").
- **Regras de Ouro:** Diretrizes inegociáveis (ex: "UI Thread via Dispatcher", "Encoding.Latin1 para serial").

---

## 2. Atualização de Documentos Existentes

### `docs/ARCHITECTURE.md`
Atualizar para refletir a nova realidade da V1.1.0:
- **Diagrama Híbrido:** Representar o fluxo dual (Serial + Arquivo).
- **SerialService:** Explicar o padrão Singleton e a orquestração.
- **Buffer Reverso:** Detalhar a Máquina de Estados na ViewModel que gerencia a fila de medições.
- **Design System:** Documentar o uso de Temas e Recursos Semânticos introduzidos na renovação de UI.

### `docs/SERIAL_CONFIGURATION_GUIDE.md`
- Explicar a estrutura do JSON de configuração.
- Detalhar como configurar dispositivos customizados.
- Documentar a estratégia específica do U-WAVE e como adaptá-la.

### `docs/PLUGIN_GUIDE.md`
- Revisão de validade para a V1.1.0.
- Confirmar se a interface `IMachineParser` sofreu alterações e atualizar exemplos se necessário.

### `README.md`
- **Cabeçalho:** Adicionar `docs/SelectML-logo-dark.png` centralizado.
- **Badges:** Adicionar status de Build e Versão atual.
- **Mapa do Repositório:** Seção de links rápidos para `docs/AI_CODEBASE_MAP.md`, `ARCHITECTURE.md`, etc.
- **Release V1.1.0:** Destaque para as novidades e link de download (Velopack).

---

## 3. Governança de Código (Code Comment Audit)

**Regra Geral:** Todos os comentários devem estar estritamente em **PT-BR**.

**Estratégia de Execução (Varredura por Namespace):**
1.  **SelectML.Core:**
    - Foco em Interfaces e Modelos de Dados.
    - Adicionar `<summary>` em métodos públicos vitais.
2.  **SelectML.Client:**
    - ViewModels: Explicar a lógica de negócios e fluxo de estado (o "Porquê").
    - Views: Comentar comportamentos complexos de UI (Converters, Triggers) se houver.
3.  **SelectML.Parsers:**
    - Documentar peculiaridades de regex ou lógica de parsing específica de cada máquina.

**Ações Específicas:**
- **Tradução:** Converter comentários legados (EN) para PT-BR.
- **Limpeza:** Remover blocos de código comentado (Dead Code).
- **Enriquecimento:** Adicionar XML Docs (`/// <summary>`) em métodos complexos (ex: `TriggerValidation`, `ArchiveInputFile`), focando na intenção e não na implementação óbvia.

---

## Estrutura de Execução

1.  **Levantamento e Criação do AI Map**
2.  **Atualização da Arquitetura e Guias**
3.  **Refinamento do README**
4.  **Auditoria de Código (Core -> Parsers -> Client)**

## Critério de Sucesso
Um novo desenvolvedor (ou IA) deve ser capaz de criar um novo driver serial ou alterar um comportamento da UI consultando apenas o `README.md` e o `AI_CODEBASE_MAP.md`, sem necessidade de engenharia reversa profunda.
