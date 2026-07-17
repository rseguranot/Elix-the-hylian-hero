param(
    [switch]$SkipAutostart,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot 'HylianHeroCompanion.csproj'
$publishDir = Join-Path $projectRoot 'bin\Release\net10.0-windows\win-x64\publish'
$sourceExe = Join-Path $publishDir 'ElixTheHylianHero.exe'
$installDir = Join-Path $env:LOCALAPPDATA 'ElixTheHylianHero'
$installedExe = Join-Path $installDir 'ElixTheHylianHero.exe'

if (-not $SkipBuild) {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'No se encontró dotnet. Instala el SDK de .NET 10 antes de continuar.'
    }

    & dotnet publish $projectFile `
        -c Release `
        -r win-x64 `
        --self-contained false `
        '-p:PublishSingleFile=true'

    if ($LASTEXITCODE -ne 0) {
        throw "No se pudo publicar Elix (código $LASTEXITCODE)."
    }
}

if (-not (Test-Path -LiteralPath $sourceExe)) {
    throw "No se encontró la publicación de Elix: $sourceExe"
}

$running = Get-Process -Name 'ElixTheHylianHero','HylianHeroCompanion' -ErrorAction SilentlyContinue
if ($running) {
    $running | Stop-Process -Force
    $running | Wait-Process -Timeout 10 -ErrorAction Stop
}

New-Item -ItemType Directory -Path $installDir -Force | Out-Null
Copy-Item -Path (Join-Path $publishDir '*') -Destination $installDir -Recurse -Force

if (-not $SkipAutostart) {
    $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    New-Item -Path $runKey -Force | Out-Null
    Remove-ItemProperty -Path $runKey -Name 'HylianHeroCompanion' -ErrorAction SilentlyContinue
    New-ItemProperty -Path $runKey -Name 'ElixTheHylianHero' -Value ('"' + $installedExe + '"') -PropertyType String -Force | Out-Null
}

Start-Process -FilePath $installedExe -WindowStyle Hidden
Write-Output $installedExe
