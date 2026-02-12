$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "PdfReaderLite/PdfReaderLite.csproj"
$publishPath = Join-Path $repoRoot "publish/win-x64"

if (Test-Path $publishPath) {
    Remove-Item $publishPath -Recurse -Force
}

dotnet publish `
    $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $publishPath

Write-Host "Publish concluido em: $publishPath"
