# Guia de Deploy e Publicação Automatizada (Velopack)

Este documento serve como guia técnico e manual de referência para desenvolvedores e Agentes de IA realizarem o build, empacotamento e deploy automatizado da **SelectML**.

---

## 📋 Checklist Pré-Build
1. **Versão**: Garanta que o `<Version>` e as tags de versão de assembly estejam incrementadas no arquivo [SelectML.Client.csproj](file:///c:/Antigravity/SelectML/SelectML.Client/SelectML.Client.csproj).
2. **Token do GitHub**: O processo de publicação automática depende da variável de ambiente `GH_TOKEN` configurada no escopo do Usuário no Windows.

---

## 🤖 Fluxo de Deploy Automatizado (Para Agentes de IA)

Sempre que uma nova versão precisar ser publicada, o Agente de IA pode executar todos os passos de forma 100% autônoma usando o console do PowerShell. O fluxo consiste em:

### 1. Execução do Script de Preparação de Assets
Antes do build, rode o script PowerShell para gerar os ícones `.ico` e aplicar a marca d'água da versão na Splash Screen (utilizando a fonte *Inter*):
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\GenerateAssets.ps1
```

### 2. Compilação e Publicação
Execute a compilação completa da solução e publique a aplicação WPF principal:
```powershell
# Limpa publicação anterior
if (Test-Path .\publish) { Remove-Item -Recurse -Force .\publish }

# Compila toda a solução em Release (gera os plugins e suas dependências)
dotnet build SelectML.sln -c Release

# Publica a aplicação WPF principal em Release (win-x64, self-contained)
dotnet publish SelectML.Client\SelectML.Client.csproj -c Release --self-contained -r win-x64 -o .\publish

# Copia configurações e os plugins compilados para a pasta de publicação
Copy-Item "SelectML.Client\custom_device_config.json" -Destination ".\publish\" -Force
Copy-Item "SelectML.Client\bin\Release\net8.0-windows\Plugins" -Destination ".\publish\Plugins" -Recurse -Force
```

### 3. Empacotamento com o Velopack
Gere o instalador (`SelectML-win-Setup.exe`), o zip portátil e os manifestos na pasta `Releases` usando o `vpk`:
```powershell
# NOTA: Substitua '1.2.3' pela versão correspondente configurada no .csproj
vpk pack --packId SelectML --packAuthors "Ramso Adnarim" --packTitle "SelectML" --packVersion 1.2.3 --packDir .\publish --mainExe SelectML.Client.exe --icon "SelectML.Client\Resources\SelectML-logo-short-light.ico" --splashImage "SelectML.Client\Resources\SelectML-splash.png" --shortcuts Desktop,StartMenu,Startup
```

### 4. Upload Automático para o GitHub Releases
Para publicar o release e os assets diretamente no GitHub, o agente deve ler o token de acesso (PAT) da variável de ambiente do usuário do Windows e executar o comando de upload:
```powershell
# Carrega o token de usuário do Windows
$token = [Environment]::GetEnvironmentVariable("GH_TOKEN", "User")

# Publica os assets da pasta Releases no GitHub (Cria a tag e publica a release oficial)
# NOTA: Altere a tag e o nome da release para corresponder à versão (ex: tag "1.2.3" e nome "V1.2.3")
vpk upload github --repoUrl "https://github.com/ramso-adnarim/SelectML" --token $token --publish --tag "1.2.3" --releaseName "V1.2.3"
```

---

## 🔑 Gerenciamento do Token do GitHub (`GH_TOKEN`)

A publicação automática de releases depende de um **Personal Access Token (PAT)** com escopo de acesso a repositórios.

### Como o Token é Armazenado
O token é armazenado permanentemente no Windows no escopo de variáveis de ambiente do usuário com o nome **`GH_TOKEN`**. O script/agente o lê em tempo de execução via:
`[Environment]::GetEnvironmentVariable("GH_TOKEN", "User")`

### Como Criar ou Atualizar o Token (Quando Vencer)
Os tokens do GitHub expiram periodicamente por razões de segurança. Se o upload falhar por erro de autenticação (`401 Unauthorized`), siga estes passos:

1. Acesse o GitHub e vá em **Settings > Developer Settings > Personal access tokens > Tokens (classic)**.
2. Clique em **Generate new token > Generate new token (classic)**.
3. Defina um nome descritivo (ex: `SelectML Deploy Token`) e uma data de validade.
4. Selecione o escopo **`repo`** (concede permissão de escrita para criar releases e fazer upload de binários).
5. Clique em **Generate token** e copie a chave gerada (começa com `ghp_`).
6. Abra o console do PowerShell e registre a nova chave permanentemente no sistema executando:
   ```powershell
   [Environment]::SetEnvironmentVariable("GH_TOKEN", "ghp_SUA_NOVA_CHAVE_AQUI", "User")
   ```
7. **Importante**: Se estiver usando uma janela de terminal ou um IDE ativo, reinicie-o para que a nova variável de ambiente seja recarregada e detectada pelos próximos agentes de IA.
