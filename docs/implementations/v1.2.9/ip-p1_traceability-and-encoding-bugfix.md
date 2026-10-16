# Plano de Implementação: Mecanismo de Rastreabilidade e Correções (V1.2.9 - Fase 1)

**Versão Alvo:** 1.2.9  
**Tipo:** Correção de Bug Crítico (Encoding UTF-16 sem BOM) & Nova Funcionalidade (Mecanismo de Rastreabilidade)  
**Status:** Planejamento e Arquitetura para Alinhamento  
**Data:** 16/10/2026  
**Documento de Origem:** Solicitação de Usuário (V1.2.9 - Fase 1)  
**Branch de Desenvolvimento:** `feature/v1.2.9-traceability-and-bugfixes` (baseada em `release/v1.2.9`)  

---

## 1. Visão Geral da Fase V1.2.9

A versão **V1.2.9** do SelectML compreende dois pilares fundamentais:
1. **Correção de BUG Crítico no Motor de Templates (V1.2.8):** Eliminar a falha de reconhecimento de campos em arquivos de texto industriais gerados sem Byte Order Mark (BOM) em codificação **UTF-16 LE**, tornando a auto-detecção de encodings resiliente a qualquer formato de entrada das máquinas de medição do cliente.
2. **Nova Funcionalidade - Mecanismo de Rastreabilidade:** Prover um recurso ágil e customizável para que o operador registre informações de rastreabilidade (como Número de Série, Operador, Turno, Linha de Fabricação, etc.) diretamente na interface superior através de um menu dinâmico (estilo Popover/Submenu, conforme leiaute aprovado na *IMG3*), injetando automaticamente esses pares `<nome>;<valor>` na **terceira linha do arquivo CSV final** enviado ao MeasurLink.

---

## 2. PARTE 1: Correção de BUG - Detecção de Encodings e Suporte UTF-16 sem BOM

### 2.1 Diagnóstico Técnico e Análise de Causa Raiz
Na versão V1.2.8, introduzimos o utilitário `EncodingDetector.cs` em `SelectML.Core`, responsável por identificar se os arquivos de amostra e os arquivos monitorados estão codificados em UTF-8 ou Latin-1/ANSI.

A investigação dos arquivos de teste reais fornecidos em `C:\SelectML\`:
* `EFF2103.15A.CSV` (Arquivo original gerado pela máquina que apresentava o bug, tamanho 230 bytes)
* `EFF2103.15A - Copia.CSV` (Arquivo aberto no Bloco de Notas e salvo com CTRL+S, tamanho 232 bytes)

Revelou a assinatura binária exata do problema:
```text
Arquivo Original (EFF2103.15A.CSV):
4E 00 6F 00 2E 00 2C 00 43 00 61 00 72 00 61 00 63 00 74 00 65 00 72 00 ED 00 73 00 74 00 69 00 63 00 61 00 73 00 ...
(Total de 230 bytes, SEM preâmbulo BOM)

Arquivo Salvo pelo Bloco de Notas (EFF2103.15A - Copia.CSV):
FF FE 4E 00 6F 00 2E 00 2C 00 43 00 61 00 72 00 61 00 63 00 74 00 65 00 72 00 ED 00 ...
(Total de 232 bytes, COM preâmbulo BOM UTF-16 LE: 0xFF 0xFE)
```

#### O Mecanismo da Falha:
1. O arquivo original é emitido pela máquina em **UTF-16 LE (Unicode 16-bit Little Endian)** sem incluir os 2 bytes de preâmbulo BOM (`0xFF 0xFE`).
2. O `EncodingDetector.cs` verificava exclusivamente a presença de BOM explícito para UTF-16:
   ```csharp
   if (bytesRead >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE) return Encoding.Unicode;
   ```
3. Sem o BOM, o detector entrava na checagem de sequências UTF-8 vs Latin-1. Como os bytes pares continham os caracteres ASCII (`4E`, `6F`, `2E`...) e os bytes ímpares continham `0x00`, ao analisar o byte `0xED` (letra 'í' em `Característica`), o detector avaliava o próximo byte (`0x00`), identificava que não era uma sequência UTF-8 válida e retornava erroneamente `Encoding.Latin1` (ou `Encoding.UTF8`).
4. Ao decodificar bytes de 16 bits usando uma tabela de 8 bits (Latin-1/UTF-8), cada byte `0x00` foi interpretado como um caractere nulo literal (`\0`).
5. No WPF e no editor de templates (*IMG1*), cada caractere nulo foi renderizado como um espaço em branco visual:  
   `N o . , , C a r a c t e r í s t i c a s , C o e f , N o m i n a l...`
6. Como consequência direta, os delimitadores de coluna (vírgulas e pontos e vírgulas) continham caracteres nulos adjacentes (`,\0` ou `\0,`), os splits de string e as expressões regulares falharam, e o SelectML não encontrou os campos básicos do arquivo.
7. Quando o usuário abriu no Bloco de Notas e salvou, a heurística do Windows adicionou os bytes `FF FE` no início. Ao reabrir, o `EncodingDetector` reconheceu o BOM e tudo funcionou perfeitamente (*IMG2*).

### 2.2 Solução Técnica e Arquitetura de Proteção
Para solucionar o problema de forma definitiva e imune a quaisquer variações de arquivos industriais:

1. **Heurística Estatística de Bytes Nulos para UTF-16 LE / BE sem BOM:**
   * Textos industriais contendo palavras ocidentais, números, pontuação e símbolos em UTF-16 utilizam o padrão de 2 bytes por caractere.
   * Em **UTF-16 LE sem BOM**: bytes ímpares (índices 1, 3, 5, 7...) contêm altíssima concentração de bytes `0x00` (> 70%), enquanto bytes pares (0, 2, 4, 6...) quase nunca são `0x00`.
   * Em **UTF-16 BE sem BOM**: bytes pares contêm alta concentração de bytes `0x00`, enquanto bytes ímpares quase nunca são `0x00`.
   * Em arquivos de texto normais de 8 bits (UTF-8, Latin-1, ANSI, ASCII), o byte `0x00` **nunca** ocorre em fluxos de texto válidos.
   * Portanto, a presença consistente de bytes nulos em posições alternadas é uma prova determinística de UTF-16.
2. **Suporte Completo a BOMs de 4 Bytes:**
   * Inclusão prévia de checagem de UTF-32 LE (`FF FE 00 00`) e UTF-32 BE (`00 00 FE FF`) antes de avaliar UTF-16 para prevenir falsos positivos.
3. **Higienização Defensiva de Strings:**
   * Em `EncodingDetector.DetectAndReadAllText` e `DetectAndReadAllLines`, incluir proteção contra eventuais caracteres nulos isolados residuais (`\0`), garantindo que nenhuma quebra de regex ou tokenização ocorra mesmo em arquivos corrompidos por equipamentos legados.

---

## 3. PARTE 2: Nova Funcionalidade - Mecanismo de Rastreabilidade

### 3.1 Requisitos Operacionais e de Negócio
1. **Menu Superior "Rastreabilidade":**
   * Posicionado na barra de menus principal imediatamente à direita de *"Modificador de nomes"* e antes de *"Configuração"*:
     * Ordem: `Porta Serial` | `Modificador de nomes` | **`Rastreabilidade`** | `Configuração` | `Tema`.
2. **Interface Direta por Submenu Dinâmico (Sem Abertura de Janela Separada):**
   * Conforme especificado pelo usuário e exemplificado na *IMG3*, o clique no menu não abre uma janela modal intermediária.
   * Abre diretamente um painel suspenso ancorado (Submenu / Popover) que permite rápida visualização e preenchimento direto.
3. **Estrutura Visual do Painel Conforme IMG3:**
   * **Cabeçalho Azul Superior:** Banner estilizado com bordas arredondadas no topo, fundo azul primário (`Brush.Action.Primary` / `#2563EB`) e texto branco em negrito centralizado: **"Rastreabilidade"**.
   * **Corpo Escuro com Lista Flexível de Pares:**
     * Colunas: **"Nome"** e **"Valor"**.
     * Linhas com campos de entrada (`TextBox`) padronizados, com visual moderno e tema responsivo.
     * Botão de exclusão (`✕` ou 🗑️) em cada linha para remoção limpa de entradas individuais.
     * Botão inferior centralizado **`+`** para inclusão dinâmica de novas linhas.
4. **Indicador Visual de Atividade:**
   * Ponto circular luminoso (verde) ao lado do título *"Rastreabilidade"* no menu principal, ativo quando houver ao menos um campo preenchido.
5. **Persistência das Rastreabilidades:**
   * Os dados preenchidos pelo operador são salvos automaticamente no `appsettings.json` na propriedade `Traceabilities`.
   * São mantidos entre reinicializações da aplicação e aplicados para todos os processamentos de novos arquivos recebidos.
6. **Formato do Arquivo CSV Exportado para o MeasurLink:**
   * Os dados de rastreabilidade cadastrados são injetados na **terceira linha do arquivo CSV gerado**:
     * **Linha 1:** Identificação da Rotina (`data.PartName`)
     * **Linha 2:** Identificação da Corrida / Lote (`data.BatchNumber`)
     * **Linha 3:** Rastreabilidades: `<nome>;<valor>;<nome>;<valor>;...`
     * **Linha 4:** Nomes das Características medidas (`Cota1;Cota2;...`)
     * **Linha 5:** Leituras / Valores medidos (`Val1;Val2;...`)

---

## 4. Análise de Conflitos, Trade-Offs e Guia de Decisões de Projeto

Abaixo estão os pontos de atenção e escolhas arquiteturais identificadas durante a elaboração deste plano, com a nossa recomendação técnica e o guia para direcionamento:

### ⚖️ Decisão 1 (D1): Comportamento do CSV quando a lista de rastreabilidades estiver vazia
* **Contexto:** Na versão atual, o CSV gerado possui 4 linhas (Linha 1: Rotina, Linha 2: Corrida, Linha 3: Características, Linha 4: Leituras). O template de importação padrão do MeasurLink (`SELECTML.itpl`) espera `[C<;>]***` na Linha 3 e `[A<;>]***` na Linha 4.
* **Opção 1A (Recomendada): Injeção Condicional:**
  * Se o usuário **não** cadastrou rastreabilidades (ou todos os campos estiverem em branco), a Linha 3 de rastreabilidade é omitida e o arquivo mantém as 4 linhas legadas.
  * Se o usuário cadastrou ao menos uma rastreabilidade válida, a linha `<nome>;<valor>;...` é injetada na Linha 3, empurrando as características para a Linha 4 e leituras para a Linha 5.
* **Opção 1B: Linha 3 Fixa Sempre Presente:**
  * Mesmo sem rastreabilidades, gera uma Linha 3 vazia (apenas `\r\n`).
  * *Risco:* Pode quebrar templates do MeasurLink que esperam cotas na Linha 3 quando o usuário não estiver usando rastreabilidade.
* **Recomendação:** **Opção 1A**, acompanhada de documentação de configuração do template `.itpl` do MeasurLink para receber a linha de rastreabilidade `[T<;>]***`.

### ⚖️ Decisão 2 (D2): Comportamento do Submenu / Popover do WPF durante a digitação
* **Contexto:** Menus nativos do WPF (`MenuItem`) tendem a fechar automaticamente quando o foco se desloca ou quando o usuário clica dentro de controles filhos como `TextBox` e `Button`.
* **Abordagem Técnica Recomendada:**
  * Implementar o painel suspenso utilizando um controle estilizado acoplado ao menu com `Popup` (`StaysOpen="False"` ou manipulador de cliques fora da área), permitindo:
    * Digitação fluida nos TextBoxes sem fechamento prematuro.
    * Navegação com tecla `Tab` entre campo *Nome* e campo *Valor*.
    * Adição contínua via botão `+` mantendo o painel aberto e visível.
    * Fechamento automático natural apenas ao clicar fora do painel ou teclar `Esc`.

### ⚖️ Decisão 3 (D3): Tratamento de Caracteres Reservados no Nome e Valor
* **Contexto:** O CSV gerado é delimitado por ponto e vírgula (`;`) e quebras de linha (`\r\n`).
* **Tratamento:** Se o usuário digitar acidentalmente um `;` ou der quebra de linha no nome ou valor da rastreabilidade (ex: `Nome = "Lote;Op"`), as colunas do CSV seriam fragmentadas.
* **Solução:** Aplicar sanitização preventiva no momento da exportação:
  * Substituição de `;` por hífen `-` ou espaço.
  * Remoção estrita de quebras de linha (`\r` e `\n`).

### ⚖️ Decisão 4 (D4): Validação de Pares Incompletos
* **Contexto:** Usuário cria uma linha e preenche o Nome (ex: `Operador`), mas deixa o Valor em branco (ou cria a linha e deixa ambos em branco).
* **Solução Recomendada:**
  * Itens com *Nome* e *Valor* totalmente vazios são ignorados na exportação.
  * Itens com *Nome* preenchido e *Valor* vazio são exportados normalmente como `<nome>;` (respeitando o layout do MeasurLink).

---

## 5. Diagrama da Arquitetura da Solução

```mermaid
graph TD
    subgraph "Detecção Robusta de Encoding (SelectML.Core)"
        FileIn["Arquivo de Entrada (Ex: EFF2103.15A.CSV)"] --> CheckBOM{"Possui BOM?"}
        CheckBOM -->|Sim| BOMType["UTF-8, UTF-16 LE/BE, UTF-32"]
        CheckBOM -->|Não| NullHeuristic{"Heurística de Nulos:<br/>Posições alternadas > 70%?"}
        NullHeuristic -->|Sim (Ímpares)| UTF16LE["Decodifica como UTF-16 LE (Unicode)"]
        NullHeuristic -->|Sim (Pares)| UTF16BE["Decodifica como UTF-16 BE"]
        NullHeuristic -->|Não| UTF8Check{"Sequências UTF-8 Válidas?"}
        UTF8Check -->|Sim / ASCII| UTF8["Decodifica como UTF-8"]
        UTF8Check -->|Não| Latin1["Decodifica como ISO-8859-1 (Latin-1)"]
    end

    subgraph "Mecanismo de Rastreabilidade (UI & Persistência)"
        MenuTrace["Menu Superior 'Rastreabilidade'"] --> Dropdown["Painel Suspenso (Conforme IMG3)"]
        Dropdown --> HeaderBlue["Cabeçalho Azul 'Rastreabilidade'"]
        Dropdown --> Rows["Lista Dinâmica (Nome | Valor)"]
        Rows --> BtnAdd["Botão (+) Adicionar Linha"]
        Rows --> BtnDel["Botão (✕) Excluir Linha"]
        Dropdown --> SaveConfig["Auto-Save em %APPDATA%/SelectML/appsettings.json"]
        SaveConfig --> Indicator["Ponto Verde Luminoso no Menu"]
    end

    subgraph "Geração do CSV para MeasurLink (MainViewModel)"
        MeasData["MeasurementData Recebido"] --> HasTrace{"Possui Rastreabilidades Válidas?"}
        HasTrace -->|Sim| BuildCSVWithTrace["Linha 1: PartName<br/>Linha 2: BatchNumber<br/>Linha 3: Nome;Valor;Nome;Valor;...<br/>Linha 4: Características<br/>Linha 5: Leituras"]
        HasTrace -->|Não| BuildCSVLegacy["Linha 1: PartName<br/>Linha 2: BatchNumber<br/>Linha 3: Características<br/>Linha 4: Leituras"]
        BuildCSVWithTrace --> ExportCSV["Grava Arquivo UTF-8 com BOM no Diretório Output"]
        BuildCSVLegacy --> ExportCSV
    end
```
