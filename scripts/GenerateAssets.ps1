# GenerateAssets.ps1
# Automação para geração de ícones (.ico), cópia de logos e estampagem da Splash Screen

Add-Type -AssemblyName System.Drawing

function Convert-PngToIco {
    param(
        [string]$pngPath,
        [string]$icoPath
    )
    Write-Host "Convertendo PNG para ICO: $pngPath -> $icoPath"
    
    $sizes = @(16, 24, 32, 48, 64, 128, 256)
    $original = [System.Drawing.Bitmap]::FromFile($pngPath)

    $pngStreams = @()
    foreach ($size in $sizes) {
        $resized = New-Object System.Drawing.Bitmap($size, $size)
        $g = [System.Drawing.Graphics]::FromImage($resized)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.DrawImage($original, 0, 0, $size, $size)
        $g.Dispose()

        $ms = New-Object System.IO.MemoryStream
        $resized.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $resized.Dispose()
        $pngStreams += ,$ms
    }
    $original.Dispose()

    $fs = New-Object System.IO.FileStream($icoPath, [System.IO.FileMode]::Create)
    $w = New-Object System.IO.BinaryWriter($fs)

    # Header
    $w.Write([UInt16]0)
    $w.Write([UInt16]1)
    $w.Write([UInt16]$sizes.Count)

    # Offset
    $offset = 6 + ($sizes.Count * 16)
    
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $size = $sizes[$i]
        $stream = $pngStreams[$i]
        $length = $stream.Length

        $val = if ($size -eq 256) { 0 } else { $size }
        $w.Write([byte]$val)
        $w.Write([byte]$val)
        $w.Write([byte]0)
        $w.Write([byte]0)
        $w.Write([UInt16]1)
        $w.Write([UInt16]32)
        $w.Write([UInt32]$length)
        $w.Write([UInt32]$offset)

        $offset += $length
    }

    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $stream = $pngStreams[$i]
        $bytes = $stream.ToArray()
        $w.Write($bytes)
        $stream.Dispose()
    }

    $w.Close()
    $fs.Close()
}

function New-VersionSplash {
    param(
        [string]$baseSplashPath,
        [string]$targetSplashPath,
        [string]$version
    )
    Write-Host "Estampando versão V$version na Splash Screen: $baseSplashPath -> $targetSplashPath"

    $img = [System.Drawing.Bitmap]::FromFile($baseSplashPath)
    $g = [System.Drawing.Graphics]::FromImage($img)
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    # Configuração de Fonte dinâmica baseada no tamanho da imagem
    $fontSize = [int]($img.Height * 0.025) # 2.5% da altura da imagem
    $font = New-Object System.Drawing.Font("Inter", $fontSize, [System.Drawing.FontStyle]::Bold)
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(180, 255, 255, 255)) # Branco semi-transparente

    $text = "V$version"
    $textSize = $g.MeasureString($text, $font)

    # Centralizado horizontalmente, localizado a 78% do topo
    $x = ($img.Width - $textSize.Width) / 2
    $y = ($img.Height * 0.78)

    $g.DrawString($text, $font, $brush, $x, $y)

    $g.Dispose()
    $font.Dispose()
    $brush.Dispose()

    # Garantir que a pasta de destino exista
    $targetDir = Split-Path $targetSplashPath
    if (-not (Test-Path $targetDir)) {
        New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    }

    $img.Save($targetSplashPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $img.Dispose()
}

# 1. Obter a versão a partir do csproj
$csprojPath = "c:\Antigravity\SelectML\SelectML.Client\SelectML.Client.csproj"
[xml]$xml = Get-Content $csprojPath
$version = $xml.Project.PropertyGroup.Version
Write-Host "Versão detectada do projeto: $version"

# 2. Gerar Ícones (.ico) para a aplicação
Convert-PngToIco "C:\Antigravity\SelectML-icon-dark.png" "c:\Antigravity\SelectML\SelectML.Client\Resources\SelectML-logo-short-dark.ico"
Convert-PngToIco "C:\Antigravity\SelectML-icon-light.png" "c:\Antigravity\SelectML\SelectML.Client\Resources\SelectML-logo-short-light.ico"

# 3. Copiar Logos para documentação
Copy-Item "C:\Antigravity\SelectML-logo-dark.png" "c:\Antigravity\SelectML\docs\SelectML-logo-dark.png" -Force
Copy-Item "C:\Antigravity\SelectML-logo-light.png" "c:\Antigravity\SelectML\docs\SelectML-logo-light.png" -Force

# 4. Gerar Splash Screen com versão
New-VersionSplash "C:\Antigravity\SelectML-splash-dark.png" "c:\Antigravity\SelectML\SelectML.Client\Resources\SelectML-splash.png" "$version"

Write-Host "Processo concluído com sucesso!"
