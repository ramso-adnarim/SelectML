# Guia de Configuração de Dispositivos Seriais (Custom)

Este guia explica como configurar o arquivo `custom_device_config.json` para conectar dispositivos seriais que não possuem drivers nativos no SelectML.

## Localização do Arquivo
O arquivo `custom_device_config.json` deve estar localizado na mesma pasta do executável da aplicação (`SelectML.Client.exe`).
Você pode abri-lo diretamente pelo menu **Conexão > Configurar Conexão... > Editar JSON**.

## Estrutura do Arquivo

O arquivo é dividido em duas seções: `PortConfig` (Hardware) e `DataProtocol` (Lógica de Leitura).

```json
{
    "PortConfig": {
        "BaudRate": 9600,       // Velocidade (Ex: 9600, 19200, 115200)
        "DataBits": 8,          // Bits de dados (Geralmente 8)
        "Parity": "None",       // Paridade (None, Odd, Even, Mark, Space)
        "StopBits": "One"       // Bits de parada (One, Two, OnePointFive)
    },
    "DataProtocol": {
        "Terminator": "\r",     // Caractere que indica fim de linha. Use "\r" (CR) ou "\n" (LF).
        "ExtractionRegex": "([+\\-]?\\d+([.,]\\d+)?)", // Expressão Regular para achar o número
        "TargetFeatureName": "" // (Opcional) Nome fixo da característica. Se vazio, será "Genérico/Editável".
    }
}
```

## Configurando a Extração de Dados (Regex)

A propriedade `"ExtractionRegex"` é a mais importante. Ela define como o sistema encontra o valor numérico em meio ao texto enviado pelo dispositivo.
O sistema captura o conteúdo do **primeiro parênteses** `(...)`.

### O Padrão Numérico Básico
Para ler números inteiros ou decimais (com ponto ou vírgula), use este padrão base:
`([+\\-]?\\d+([.,]\\d+)?)`

Este padrão significa: "Pegue sinal opcional, seguido de dígitos, seguido opcionalmente de ponto/vírgula e mais dígitos".

---

### Cenários Comuns

#### 1. Leitura Simples (Recomendado)
Se o dispositivo envia apenas o número ou o número no meio de um texto simples.
*   **Exemplo:** `"123.45"` ou `"Peso: 123.45kg"` ou `"A 123,456"`
*   **Regex:** `"([+\\-]?\\d+([.,]\\d+)?)"`
*   **Comportamento:** O sistema procura o primeiro número válido na linha inteira.

#### 2. Validar Prefixo (Mais Seguro)
Se você quer ler apenas linhas que começam com um código específico (ex: "A").
*   **Exemplo:** Quero ler `"A 123,456"` mas ignorar `"ID 999"`.
*   **Regex:** `"A\\s*([+\\-]?\\d+([.,]\\d+)?)"`
    *   `A`: Exige a letra "A" no início.
    *   `\\s*`: Aceita qualquer quantidade de espaços após o A (ex: "A " ou "A    ").
    *   `(...)`: Captura o número logo após.

#### 3. Identificar por Rótulo
Se a linha tem vários números e você quer um específico.
*   **Exemplo:** `"ID: 55, Peso: 123,45"`
*   **Regex:** `"Peso:\\s*([+\\-]?\\d+([.,]\\d+)?)"`
    *   Procura a palavra "Peso:", ignora espaços e captura o número seguinte.

---

### Atenção com JSON
No arquivo `.json`, você deve escapar a barra invertida.
*   Se o Regex puro é `\d` (dígito), no JSON escreva `\\d`.
*   Se o Regex puro é `\s` (espaço), no JSON escreva `\\s`.

### Testando
Após salvar o arquivo `custom_device_config.json`:
1. Vá no SelectML.
2. Menu **Conexão > Configurar Conexão**.
3. Selecione "Customizado" novamente (ou reconecte) para que o arquivo seja recarregado.

---

## Estratégia Nativa: Mitutoyo U-WAVE

O SelectML já possui suporte nativo para o sistema **Mitutoyo U-WAVE**. Se você selecionar "Mitutoyo U-WAVE" na tela de configuração serial, ele ignorará o arquivo `.json` e usará a lógica interna otimizada.

**Protocolo Esperado:**
Formato padrão Mitutoyo: `01A+123.456CR`
- `01`: ID do canal (ignorável para canal único)
- `A`: Código de tipo de dado
- `+` ou `-`: Sinal
- `123.456`: Valor Numérico
- `CR`: Carriage Return (Terminador)

**Regras Específicas:**
- O comando de requisição de dados (DREQ) não é enviado pelo PC. O U-WAVE deve estar configurado para enviar dados ao pressionar o botão no paquímetro/micrômetro.
- O SelectML faz o parsing automático e extrai o valor numérico, convertendo para Double independente da cultura (ponto ou vírgula).

