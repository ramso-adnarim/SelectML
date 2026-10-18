<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/SelectML-logo-dark.png">
    <source media="(prefers-color-scheme: light)" srcset="docs/SelectML-logo-light.png">
    <img alt="SelectML Logo" src="docs/SelectML-logo-light.png" width="400">
  </picture>
</p>

# Interface de monitoramento e entrega de arquivos de medição

![Version](https://img.shields.io/badge/Version-1.2.9-blue)
![Build](https://img.shields.io/badge/Build-Passing-brightgreen)
![Platform](https://img.shields.io/badge/Platform-Windows_WPF-blueviolet)

> **Versão 1.2.9**
> Novidades:
> - **Integração Avançada de Rastreabilidade MeasurLink:** Catálogo de campos do banco SQL, suporte a itens livres com validação por máscara e listas pré-definidas (`Traceability_Item_Value`), popover dedicado no menu superior, campo fixo de número de série com auto-incremento e avisos preventivos de obrigatoriedade.
> - **Auto-Detecção de Codificação Estendida:** Heurística aprimorada no `EncodingDetector` para detecção de arquivos UTF-16 LE e UTF-16 BE sem BOM, mantendo compatibilidade total com UTF-8 e Latin-1/ANSI.
> - **Otimização de Desempenho e Concorrência:** Debounce reativo de 350ms em `BatchNumber` e `PartName` com cancelamento assíncrono para digitação fluida sem travamento de interface, e restrição de inserção manual a cotas de variáveis (`KnownVariableFeatures`).
> - **Dicionário Centralizado de Palavras-Chave:** Padronização multilíngue via `Keywords.csv` com mapeamento abrangente em todas as telas e recursos.
> 
> <details>
> <summary>Histórico de versões anteriores</summary>
> 
> - **Versão 1.2.8:** Motor Universal de Templates de Texto configurável com destaque sintático e prévia em tempo real, listagem completa da rotina de inspeção com código de cores de tolerância, remodelagem da fila serial concorrente híbrida com desfazer U-WAVE e diálogos modernos Claro/Escuro.
> - **Versão 1.2.7:** Persistência de regras de confirmação no `%AppData%`, menu de contexto no botão Enviar, foco automático/elevação de janela e geração de CSV estritamente baseada na UI.
> - **Versão 1.2.6:** Refinamento da Splash Screen e integridade de inicialização.
> - **Versão 1.2.5:** Modificador dinâmico de nomes e resiliência de catálogo SQL.
> </details>
> 
> [Baixar última versão](https://github.com/ramso-adnarim/SelectML/releases/tag/v1.2.9) 

## 🗺️ Mapa do repositório

Para navegar com eficiência no código e documentação:

- **[🤖 AI Codebase Map](docs/AI_CODEBASE_MAP.md)**: Índice otimizado para Agentes de IA.
- **[🏛️ Arquitetura Técnica](docs/ARCHITECTURE.md)**: Diagramas, Fluxo de Dados e Decisões de Design.
- **[🔌 Guia de Plugins](docs/PLUGIN_GUIDE.md)**: Como criar parsers para novas máquinas.
- **[📟 Configuração Serial](docs/SERIAL_CONFIGURATION_GUIDE.md)**: Ajustes de comunicação com dispositivos seriais.

---

## Visão geral do projeto

O **SelectML** é um **Middleware** desenvolvido em **WPF (.NET 8)**. Ele atua como uma ponte entre máquinas de medição (CMMs, VMMs e etc) e o software MeasurLink da Mitutoyo.

Diferente de um simples "copiador de arquivos", o SelectML oferece uma camada robusta de **governança de dados** e **validação em tempo real**, garantindo que apenas dados limpos e padronizados cheguem ao banco de dados.

**Principais funcionalidades:**
- **Rastreabilidade Integrada (Novo):** Integração com campos do MeasurLink, máscaras de validação e opções pré-cadastradas.
- **Híbrido:** Aceita tanto arquivos de CMMs quanto medição serial manual (Paquímetros).
- **Validação SQL:** Verifica se o Lote e as Características existem no banco de dados antes de processar.
- **Human-in-the-Loop:** Interface para revisão manual dos dados com destaque visual para erros ou features desconhecidas.
- **Ciclo de Vida Seguro:** Backup automático de todos os arquivos brutos (Raw Data).
- **System Tray:** Roda silenciosamente na bandeja do sistema.

---

## Arquitetura Simplificada

O sistema segue o fluxo:
`Máquina/Serial -> Buffer -> Validação -> CSV Padronizado -> MeasurLink`

---

## Configuração (appsettings.json)

A aplicação é configurada através do arquivo `appsettings.json`.

**Exemplo de Configuração:**
```json
{
  "WatchDirectory": "C:\\Medicoes\\Input",
  "LastPluginName": "ViciVision M1",
  "DbServer": "localhost\\MLSQLExpress",
  "DbUser": "sa",
  "DbPassword": "MySecurePassword",
  "DbName": "SelectML",
  "DbUseWindowsAuth": false,
  "DataRetentionDays": 30,
  "IsDarkMode": true
}
```

**Novas Chaves:**
*   `DataRetentionDays`: Define quantos dias os arquivos de backup e logs são mantidos antes da limpeza automática (Padrão: 30).
*   `IsDarkMode`: Persiste a preferência de tema do usuário.
*   `Db*`: Configurações granulares de conexão SQL.

---

## Guia de Desenvolvimento de Plugins

Deseja integrar uma nova máquina (ex: Hexagon, Zeiss, Keyence) com formato de arquivo diferente?
O SelectML utiliza uma arquitetura de plugins aberta.

1.  Crie uma Class Library (.NET 8).
2.  Implemente a interface `IMachineParser`.
3.  Retorne um objeto `MeasurementData`.
4.  Coloque a DLL na pasta `/Plugins`.

👉 **[Leia o guia completo de plugins aqui](docs/PLUGIN_GUIDE.md)**

---

## Instalação e Execução

### Pré-requisitos
*   Windows 10/11
*   .NET 8 Runtime (ou SDK para desenvolvimento)
*   Acesso a uma instância SQL Server (para validação de lotes)

### Compilando
```bash
git clone https://github.com/seu-org/SelectML.git
dotnet build -c Release
```

### Executando
O executável principal é `SelectML.Client.exe`.
Ao iniciar, o ícone aparecerá na bandeja do sistema (próximo ao relógio). Clique duas vezes no ícone ou use o botão direito para interagir.

---

## Estrutura do Repositório

*   `/SelectML.Client`: Aplicação WPF (UI, Serviços, ViewModel).
*   `/SelectML.Core`: Contratos e Modelos compartilhados.
*   `/SelectML.Parsers.*`: Projetos de exemplo de plugins.
*   `/docs`: Documentação técnica detalhada.
