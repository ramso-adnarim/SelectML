# PLANO DE IMPLEMENTAÇÃO V1.1.0 - FASE 1: ESTAÇÃO HÍBRIDA & CONCORRÊNCIA

**Versão:** 1.1.0 (Revisão 2)
**Status:** Planejamento
**Objetivo:** Transformar o SelectML em uma Estação de Coleta Híbrida (Arquivos + RS232), garantindo integridade de dados via orquestração de buffer e validação de contexto.

---

## 1. Visão Geral da Arquitetura

A versão 1.1.0 introduz uma mudança fundamental: a existência de múltiplas fontes de dados ativas simultaneamente (Plugins de Arquivo e Porta Serial). Para suportar isso sem "Race Conditions" ou travamentos de UI, adotaremos uma arquitetura baseada em **Serviço Singleton de Hardware** desacoplado da UI, intermediado por um **Gerenciador de Estado com Buffer**.

### Diagrama de Conceito
```mermaid
graph TD
    SerialPort[(Porta Serial RS232)] -->|Dados Brutos| SerialService
    ConfigJSON[custom_device_config.json] -.->|Carrega Config| CustomStrategy
    
    SerialService -->|Estratégia (U-WAVE/Genérico/Custom)| ParsedEvent
    
    subgraph "Core Logic (MainViewModel)"
        ParsedEvent --> BufferManager{Em Processamento de Arquivo?}
        BufferManager -->|SIM| MemoryQueue[Buffer (Memória)]
        BufferManager -->|NÃO| UIDispatcher
        
        FilePlugin -->|Dados CSV| UIDispatcher
        
        MemoryQueue -->|Ao Finalizar Arquivo| UIDispatcher
    end
    
    UIDispatcher --> Grid[DataGrid UI]
```

---

## 2. Nova Camada de Infraestrutura: SerialService

Este serviço será a única porta de entrada para dados físicos. Ele deve ser resiliente a desconexões e ruídos e agora suportar reconfiguração dinâmica de parâmetros de porta.

### 2.1 Especificações Técnicas
*   **Namespace:** `SelectML.Core.Services`
*   **Dependência:** `System.IO.Ports`
*   **Padrão:** Singleton (Ciclo de vida único para aplicação inteira).
*   **Requisito Novo:** Método `UpdatePortConfig(int baud, int dataBits, ...)` para suportar mudanças vindas da estratégia Custom.

### 2.2 Strategy Pattern (Interpretação)
O serviço receberá dados brutos e delegará o *parsing* para a estratégia ativa.

#### Interface `ISerialInterfaceStrategy`
```csharp
public interface ISerialInterfaceStrategy {
    string Name { get; }
    // Define configurações preferenciais desta estratégia
    SerialPortConfig GetPortConfig(); 
    // Tenta extrair medição e característica da string recebida
    bool TryParse(string rawData, out double value, out string featureName);
}
```

#### Estratégia A: `UWaveStrategy`
*   **Config:** Baud 57600, 8, N, 1 (Fixo).
*   **Formato Esperado:** `DT10001+00012.34567M`
*   **Característica Fixa:** "Rugosidade (Ra)".

#### Estratégia B: `GenericStrategy`
*   **Config:** Padrão 9600 (mas ajustável via UI no futuro).
*   **Lógica:** Regex simples `[\d\.,]+`.
*   **Característica:** "Pendente de Seleção".

#### Estratégia C: `CustomStrategy` (NOVO)
Estratégia dinâmica baseada em configuração externa.
*   **Arquivo Fonte:** `custom_device_config.json` (Raiz da Aplicação).
*   **Comportamento:** Ao ser instanciada (ou selecionada), lê o JSON e configura o regex de extração e os parâmetros da porta.

**Schema JSON:**
```json
{
  "PortConfig": {
    "BaudRate": 9600,
    "DataBits": 8,
    "Parity": "None", 
    "StopBits": "One"
  },
  "DataProtocol": {
    "Terminator": "\r",
    "ExtractionRegex": "([0-9]+,[0-9]+)", 
    "TargetFeatureName": "Característica Customizada"
  }
}
```

---

## 3. Gestão de Metadados e Fluxo de Validação

Diferente dos arquivos, a porta serial é "meta-data blind" (cega para metadados como Nome da Peça ou Lote).

### 3.1 O Problema
Dados chegam "órfãos". O operador deve fornecer o contexto *a posteriori*.

### 3.2 Solução: Trigger de "Enriquecimento"
1.  **Entrada:** Dado serial entra no Grid apenas com o Valor Numérico.
2.  **Ação Humana:** Usuário digita ou scaneia código de barras nos campos `PartName` / `BatchNumber`.
3.  **Trigger:** Evento `LostFocus` ou `TextChanged` (com Debounce de 500ms).
4.  **Query SQL:** `GetFeaturesForRun(PartName)`.

### 3.3 Validação Pós-Query
*   **Caso U-WAVE:** Sistema verifica automaticamente se "Rugosidade (Ra)" existe na lista retornada pelo SQL.
*   **Caso Custom:** Verifica se `TargetFeatureName` do JSON existe na lista.
*   **Caso Genérico:** Sistema popula o `ComboBox`.

---

## 4. Orquestração de Buffer (O "Cérebro")

O `MainViewModel` atua como Máquina de Estados para evitar conflitos de dados.

### 4.1 Cenário A: Conflito (Arquivo Ativo + Serial Recebido)
1.  Usuário carrega CSV (`_isProcessingFile = true`).
2.  **Evento Serial:** Medição chega.
3.  **Ação:** Dado vai para `_serialBuffer`. UI não altera.
4.  **Resolução:** Ao finalizar CSV, buffer é drenado para a UI.

### 4.2 Cenário B: Serial Puro
1.  Estado `Idle`.
2.  **Evento Serial:** Entra direto na `ObservableCollection`.

---

## 5. Alterações na Interface (UX/UI)

### 5.1 Menu "Conexão" Melhorado
*   **Seletor de Dispositivo:** U-WAVE | Genérico | Customizado.
*   **Ação para Customizado:** Habilitar botão "Configurar Comunicação".
    *   *Comportamento:* Abre `custom_device_config.json` com o editor de texto padrão do Windows (`Process.Start`).
*   **Reload:** Ao trocar de dispositivo ou re-selecionar "Customizado", o sistema deve reler o JSON.

### 5.2 DataGrid Polimórfico
Mantém-se a lógica de `FeatureColumnTemplateSelector`.
*   **Custom Strategy:** Usa o `TemplateReadonly` (pois o nome da característica vem fixo do JSON), a menos que `TargetFeatureName` esteja vazio no JSON (neste caso, fallback para ComboBox).

---

## 6. Checklist de Execução

Tarefas técnicas ordenadas para implementação segura:

- [ ] **Infraestrutura Serial Expansível**
    - [ ] Atualizar `SerialPortService` para suportar reconfiguração (DataBits, Parity, etc.).
    - [ ] Implementar `CustomSerialStrategy` com parser JSON (`System.Text.Json`).
    - [ ] Criar arquivo modelo `custom_device_config.json` na raiz.

- [ ] **Lógica de Estado (Backend)**
    - [ ] Refatorar `MainViewModel`: Adicionar `Queue<T>` buffer e flag `IsProcessingFile`.
    - [ ] Implementar lógica de descarte/enfileiramento.

- [ ] **Integração de Metadados**
    - [ ] Criar Command/Trigger para `TextChanged` em Lote/Peça.
    - [ ] Implementar validação cruzada.

- [ ] **Interface de Usuário**
    - [ ] Implementar Menu "Conexão" com lógica de abrir JSON externo.
    - [ ] Implementar `FeatureTemplateSelector`.

---

## Critério de Sucesso
O sistema deve ler corretamente configurações de um arquivo JSON externo para conectar a um dispositivo serial não-padrão. Deve permitir a edição desse arquivo pelo usuário e refletir as mudanças (ex: mudança de BaudRate) sem recompilar, apenas reconectando. A integridade dos dados (Buffer) deve ser mantida.
