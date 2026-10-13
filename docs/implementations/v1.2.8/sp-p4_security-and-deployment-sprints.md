# Sprint Planning - V1.2.8 Fase Final: Auditoria de Segurança, Migração e Deployment Velopack

Este documento consolida o planejamento, checklist de auditoria de segurança, garantia de compatibilidade retroativa e o procedimento de homologação e deployment da versão **V1.2.8** do SelectML.

---

## 📋 Metas da Fase Final (Deployment V1.2.8)

1. **Atualização Completa da Documentação**:
   - Atualizar a documentação pública e técnica (`ARCHITECTURE.md`, `AI_CODEBASE_MAP.md`, `PLUGIN_GUIDE.md`, `SERIAL_CONFIGURATION_GUIDE.md`).
   - Atualizar o `README.md` destacando as inovações da V1.2.8 e links da release.
2. **Auditoria de Segurança e Prevenção de Falhas (Crash-Resilience)**:
   - Identificar e blindar blocos de código sem tratamento de exceção (notadamente métodos `async void`, processadores de buffers e compilação de expressões regulares de templates).
   - Proteger lookups de recursos estáticos de temas em diálogos e janelas.
3. **Garantia de Transição Limpa e Não-Destrutiva do `appconfig` (V1.2.7 -> V1.2.8)**:
   - Confirmar que clientes atualizando a partir da versão 1.2.7 mantêm 100% de suas configurações salvas em `%APPDATA%\SelectML\appsettings.json`.
   - Assegurar que novas propriedades (`EnqueueSerialInput`, `ListAllFeatures`, `LastTemplateName`) assumem valores padrão não-disruptivos sem corromper as chaves existentes.
4. **Geração de Assets e Estampagem da Splash Screen**:
   - Gerar ícones `.ico` e aplicar a versão `V1.2.8` na imagem de splash screen via `GenerateAssets.ps1`.
5. **Empacotamento e Publicação Velopack**:
   - Compilar solução inteira em Release (win-x64, self-contained).
   - Validar integridade e tamanho da DLL nativa `System.IO.Ports.dll` (~88 KB).
   - Empacotar com `vpk pack` e publicar no repositório GitHub via `vpk upload github`.
6. **Publicação da Release no GitHub**:
   - Criação da release `v1.2.8` com notas detalhadas das três frentes desenvolvidas.

---

## 🛡️ Checklist de Auditoria de Segurança

- [x] **Métodos `async void` Protegidos**:
  - `OnFileCreated`: envolvido em `try-catch` para evitar crash caso ocorra bloqueio de arquivo na fila.
  - `ExecuteSend`: envolvido em `try-catch` com apresentação de mensagem amigável via `ModernDialogWindow`.
  - `ResetUI`: protegido contra falhas durante esvaziamento de buffers.
  - `ProcessFileBuffer`: protegido para falhas no processamento sequencial.
- [x] **Compilação e Execução de Expressões Regulares de Templates**:
  - `TemplateExecutionEngine`: `new Regex` e `regex.Match` envolvidos em blocos `try-catch` individuais para garantir que regex sintaticamente inválida fornecida pelo usuário não derrube a aplicação.
- [x] **Lookup Seguro de Recursos em `ModernDialogWindow`**:
  - Substituição de `FindResource` direto por `TryFindResource` com fallback para `SolidColorBrush` seguro.
- [x] **Tratamento de Banco de Dados**:
  - Queries de rotina (`GetInspectionRoutineFeaturesAsync`) protegidas contra quedas de conexão ou tabelas inexistentes.

---

## 🔄 Validação de Transição de Configuração (V1.2.7 -> V1.2.8)

| Item de Configuração | Estado V1.2.7 | Comportamento na V1.2.8 | Impacto no Usuário |
| :--- | :--- | :--- | :--- |
| **Localização do JSON** | `%APPDATA%\SelectML\appsettings.json` | `%APPDATA%\SelectML\appsettings.json` | **Nenhum**. O arquivo não é sobrescrito pelo Velopack. |
| **Credenciais de Banco** | Servidor, usuário, senha, base | Preservados integralmente | **Nenhum**. Conexão permanece pronta. |
| **Pastas de Trabalho** | Entrada (`WatchDirectory`) e Saída (`OutputDirectory`) | Preservados integralmente | **Nenhum**. Monitoramento contínuo. |
| **EnqueueSerialInput** | Inexistente (Comportamento legado de fila) | Inicializado com `true` (padrão seguro legado) | **Transparente**. Mantém exatamente o mesmo fluxo. |
| **ListAllFeatures** | Inexistente (Apenas cotas recebidas) | Inicializado com `false` (padrão legado) | **Transparente**. Usuário ativa se desejar. |
| **LastTemplateName** | Inexistente | Inicializado com `string.Empty` | **Transparente**. Carrega template padrão se necessário. |
| **Templates do Usuário** | Não existiam | Criado `%APPDATA%\SelectML\Templates\` | **Aditivo**. Templates isolados e seguros. |

---

## 🚀 Roteiro de Execução de Deployment

1. **Atualização da Versão no Projeto**:
   - `SelectML.Client.csproj` atualizado para `1.2.8` e assembly versions `1.2.8.0`.
2. **Execução de `GenerateAssets.ps1`**:
   - Splash screen estampada com `V1.2.8`.
3. **Publicação via `Publish-Release.ps1`**:
   - Compilação limpa da solução e da aplicação WPF.
   - Validação da DLL nativa `System.IO.Ports.dll`.
   - Geração dos pacotes Velopack na pasta `Releases/`.
   - Upload para o GitHub Releases com a tag `v1.2.8`.
4. **Verificação de Release e Link no README**:
   - Garantir que `https://github.com/ramso-adnarim/SelectML/releases/tag/v1.2.8` esteja acessível e com as notas de lançamento.
