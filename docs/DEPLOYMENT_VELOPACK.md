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
vpk pack --packId SelectML --packAuthors "Protequality" --packTitle "SelectML" --packVersion 1.2.2 --packDir .\publish --mainExe SelectML.Client.exe --icon "SelectML.Client\Resources\SelectML-logo-short-light.ico" --splashImage "SelectML.Client\Resources\SelectML-splash.png" --shortcuts Desktop,StartMenu,Startup
```

---

## ☁️ Upload (GitHub Releases)

1. Vá para **GitHub > Releases > Draft a new release**.
2. **Tag**: `v1.2.2` (Use 'v' prefixo por convenção, ou apenas o número).
3. **Título**: `Versão 1.2.2`.
4. **Anexar Arquivos**: Arraste os arquivos da pasta `Releases` gerados pelo Velopack:
   - `SelectML-1.2.2-win-x64-Setup.exe` (Instalador)
   - `SelectML-1.2.2-win-x64-full.nupkg` (Pacote Full)
   - `RELEASES` (Manifesto - **CRUCIAL**: Sobrescrever se já existir)
5. **Publish**.

> 💡 **Dica**: O arquivo `RELEASES` é o cérebro do update. Ele deve conter o hash SHA1 correto dos pacotes. O `vpk pack` atualiza isso automaticamente, então sempre suba a versão mais recente gerada.
