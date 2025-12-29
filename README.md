# SelectML - Monitoramento de Medições Industriais

## Visão Geral do Projeto

O **SelectML** é uma solução de **Middleware Industrial** desenvolvida em **WPF (.NET 8)**. Ele atua como uma ponte inteligente entre máquinas de medição (CMMs, ViciVision, Zeiss) e os sistemas de gestão de qualidade (MES/ERP).

Diferente de um simples "copiador de arquivos", o SelectML oferece uma camada robusta de **Governança de Dados** e **Validação em Tempo Real**, garantindo que apenas dados limpos e padronizados cheguem ao banco de dados corporativo.

**Principais Funcionalidades:**
- **Monitoramento Contínuo:** Detecta arquivos automaticamente assim que são gerados pela máquina.
- **Validação SQL (Early Detection):** Verifica se o Lote e as Características existem no banco de dados antes de processar.
- **Human-in-the-Loop:** Interface para revisão manual dos dados com destaque visual para erros ou features desconhecidas.
- **Ciclo de Vida Seguro:** Backup automático de todos os arquivos brutos (Raw Data) com retenção configurável.
- **System Tray:** Roda silenciosamente na bandeja do sistema, com notificações (Balloon Tips) e restauração automática ("Wake-on-Event").
- **Dark Mode:** Suporte nativo a temas Claro e Escuro para conforto visual no chão de fábrica.

---

## Arquitetura Simplificada

O sistema segue o fluxo:
`Máquina -> Arquivo TXT -> SelectML (Plugin) -> Validação SQL -> CSV Padronizado -> ERP`

Para detalhes técnicos profundos, consulte a [Documentação de Arquitetura](docs/ARCHITECTURE.md).

---

## Configuração (appsettings.json)

A aplicação é configurada através do arquivo `appsettings.json` gerado na primeira execução ou distribuído junto com o binário.

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

Deseja integrar uma nova máquina (ex: Mitutoyo, Zeiss, Keyence)?
O SelectML utiliza uma arquitetura de plugins aberta.

1.  Crie uma Class Library (.NET 8).
2.  Implemente a interface `IMachineParser`.
3.  Retorne um objeto `MeasurementData`.
4.  Coloque a DLL na pasta `/Plugins`.

👉 **[Leia o Guia Completo de Plugins Aqui](docs/PLUGIN_GUIDE.md)**

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
