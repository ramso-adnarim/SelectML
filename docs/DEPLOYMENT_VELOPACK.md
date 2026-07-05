# Deploy Cheat Sheet (Velopack)

Guia rápido para gerar releases da **SelectML (v1.2.2+)**.

## 📋 Checklist Pré-Build
- [ ] **Versão**: `Version` incrementada em `SelectML.Client.csproj`.
- [ ] **Ícone**: `Resources\SelectML-logo-short-light.ico` existe.
- [ ] **Plugins**: Pasta `Plugins` populada (se houver parsers externos).
- [ ] **Config**: `custom_device_config.json` atualizado.

## 🚀 Comandos de Release

Abra o terminal na **raiz do repositório** e execute:

### 1. Compilar e Publicar
```powershell
# Remove publish anterior para evitar lixo
if (Test-Path .\publish) { Remove-Item -Recurse -Force .\publish }

# Compila toda a solução em Release para gerar os plugins e copiar suas dependências (como UglyToad.PdfPig)
dotnet build SelectML.sln -c Release

# Publica a aplicação WPF principal em Release (win-x64)
dotnet publish SelectML.Client\SelectML.Client.csproj -c Release --self-contained -r win-x64 -o .\publish
```

### 2. Copiar Ativos (Crucial v1.1.0)
Scripts manuais para copiar arquivos que não vão automaticamente:

```powershell
# Copia configuração de dispositivos customizados
Copy-Item "SelectML.Client\custom_device_config.json" -Destination ".\publish\" -Force

# Copia pasta de Plugins (Certifique-se que ela foi populada no passo anterior)
# NOTA: O build da solução já coloca ZeissPdf, ViciVision e ViciVisionJson com suas DLLs em net8.0-windows\Plugins
$pluginSource = "SelectML.Client\bin\Release\net8.0-windows\Plugins" 

if (Test-Path $pluginSource) {
    Copy-Item $pluginSource -Destination ".\publish\Plugins" -Recurse -Force
} else {
    Write-Warning "Pasta Plugins não encontrada em $pluginSource. Verifique se os Parsers foram compilados."
}
```

### 3. Criar Pacote (Velopack)
Gera o instalador e arquivos de update em `Releases`.

```powershell
# NOTA: Altere "1.2.2" para a versão correspondente do SelectML.Client.csproj
vpk pack --packId SelectML --packAuthors "Ramso Adnarim" --packTitle "SelectML" --packVersion 1.2.3 --packDir .\publish --mainExe SelectML.Client.exe --icon "SelectML.Client\Resources\SelectML-logo-short-light.ico" --splashImage "SelectML.Client\Resources\SelectML-splash.png" --shortcuts Desktop,StartMenu,Startup
```

---

## ☁️ Upload (GitHub Releases)

1. Vá para **GitHub > Releases > Draft a new release**.
2. **Tag**: `v1.2.2` (Use o prefixo 'v' por convenção, correspondendo à versão do release).
3. **Título**: `Versão 1.2.2` (ou a versão correspondente).
4. **Anexar Arquivos**: Arraste os arquivos gerados pelo Velopack dentro da pasta `Releases`:
   - `SelectML-win-Setup.exe` (Instalador completo do Windows)
   - `SelectML-1.2.2-full.nupkg` (Pacote completo da versão atual)
   - `SelectML-1.2.2-delta.nupkg` (Pacote delta/incremental, opcional mas recomendado para updates menores)
   - `releases.win.json` (Manifesto de atualização de ativos do Velopack - **CRUCIAL**: Sobrescrever se já existir!)
5. **Publish**.

> 💡 **Dica**: O arquivo **`releases.win.json`** é o cérebro do atualizador automático no Windows. Ele gerencia as somas de verificação SHA e os metadados dos pacotes. O `vpk pack` atualiza esse arquivo local automaticamente a cada geração de versão, portanto, certifique-se de sempre fazer o upload dele atualizado na release do GitHub.
