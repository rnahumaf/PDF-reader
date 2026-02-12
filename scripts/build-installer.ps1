param(
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishExe = Join-Path $repoRoot "publish/win-x64/PdfReaderLite.exe"
$innoScript = Join-Path $repoRoot "installer/PdfReaderLite.iss"
$isccPath = Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6/ISCC.exe"

if (-not $SkipPublish) {
    Write-Host "Executando publish antes de gerar o instalador..."
    & (Join-Path $PSScriptRoot "publish-win-x64.ps1")
}
elseif (-not (Test-Path $publishExe)) {
    throw "Publish nao encontrado em '$publishExe'. Execute sem -SkipPublish."
}

if (-not (Test-Path $isccPath)) {
    throw "ISCC.exe nao encontrado. Instale o Inno Setup 6 em '$isccPath'."
}

& $isccPath $innoScript

Write-Host "Instalador gerado em: $(Join-Path $repoRoot 'dist')"
