# Deploy Cheat Sheet (Velopack)

Guia rápido para gerar releases da **SelectML (v1.2.1+)**.

## 📋 Checklist Pré-Build
- [ ] **Versão**: `Version` incrementada em `SelectML.Client.csproj`.
- [ ] **Ícone**: `Resources\SelectML-logo-short-light.ico` existe.
- [ ] **Plugins**: Pasta `Plugins` populada (se houver parsers externos).
- [ ] **Config**: `custom_device_config.json` atualizado.

## 🚀 Comandos de Release

Abra o terminal na **raiz do repositório** e execute:

### 1. Limpar e Publicar
```powershell
# Remove publish anterior para evitar lixo
if (Test-Path .\publish) { Remove-Item -Recurse -Force .\publish }

# Publica em Release (win-x64)
dotnet publish SelectML.Client\SelectML.Client.csproj -c Release --self-contained -r win-x64 -o .\publish
```

### 2. Copiar Ativos (Crucial v1.1.0)
Scripts manuais para copiar arquivos que não vão automaticamente:

```powershell
# Copia configuração de dispositivos customizados
Copy-Item "SelectML.Client\custom_device_config.json" -Destination ".\publish\" -Force

# Copia pasta de Plugins (Certifique-se que ela existe e contém as DLLs necessárias)
# Exemplo se os plugins estiverem em SelectML.Client\Plugins ou na pasta bin
$pluginSource = "SelectML.Client\bin\Release\net8.0-windows\Plugins" 
# Ou se você mantém uma pasta raiz de plugins: $pluginSource = "SelectML.Client\Plugins"

if (Test-Path $pluginSource) {
    Copy-Item $pluginSource -Destination ".\publish\Plugins" -Recurse -Force
} else {
    Write-Warning "Pasta Plugins não encontrada em $pluginSource. Verifique se os Parsers foram compilados."
}
```

### 3. Criar Pacote (Velopack)
Gera o instalador e arquivos de update em `Releases`.

```powershell
vpk pack --packId SelectML --packAuthors "Protequality" --packTitle "SelectML" --packVersion 1.2.1 --packDir .\publish --mainExe SelectML.Client.exe --icon "SelectML.Client\Resources\SelectML-logo-short-light.ico" --splashImage "SelectML.Client\Resources\SelectML-splash.png" --shortcuts Desktop,StartMenu,Startup
```

---

## ☁️ Upload (GitHub Releases)

1. Vá para **GitHub > Releases > Draft a new release**.
2. **Tag**: `v1.2.1` (Use 'v' prefixo por convenção, ou apenas o número).
3. **Título**: `Versão 1.2.1`.
4. **Anexar Arquivos**: Arraste os arquivos da pasta `Releases` gerados pelo Velopack:
   - `SelectML-1.2.1-win-x64-Setup.exe` (Instalador)
   - `SelectML-1.2.1-win-x64-full.nupkg` (Pacote Full)
   - `RELEASES` (Manifesto - **CRUCIAL**: Sobrescrever se já existir)
5. **Publish**.

> 💡 **Dica**: O arquivo `RELEASES` é o cérebro do update. Ele deve conter o hash SHA1 correto dos pacotes. O `vpk pack` atualiza isso automaticamente, então sempre suba a versão mais recente gerada.
