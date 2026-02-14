param(
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "PdfReaderLite/PdfReaderLite.csproj"
$publishExe = Join-Path $repoRoot "publish/win-x64/PdfReaderLite.exe"
$innoScript = Join-Path $repoRoot "installer/PdfReaderLite.iss"
$isccPath = Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6/ISCC.exe"

function Get-IncrementedVersion([string]$versionText) {
    if ($versionText -notmatch '^(\d+)\.(\d+)\.(\d+)$') {
        throw "Versao invalida '$versionText'. Esperado: major.minor.patch"
    }

    $major = [int]$Matches[1]
    $minor = [int]$Matches[2]
    $patch = [int]$Matches[3] + 1
    return "$major.$minor.$patch"
}

function Get-XmlChildByLocalName([System.Xml.XmlNode]$parent, [string]$nodeName) {
    return $parent.ChildNodes | Where-Object {
        $_.NodeType -eq [System.Xml.XmlNodeType]::Element -and $_.LocalName -eq $nodeName
    } | Select-Object -First 1
}

function Set-XmlChildNodeValue([xml]$xml, [System.Xml.XmlNode]$parent, [string]$nodeName, [string]$value) {
    $node = Get-XmlChildByLocalName -parent $parent -nodeName $nodeName

    if ($null -eq $node) {
        if ([string]::IsNullOrWhiteSpace($parent.NamespaceURI)) {
            $node = $xml.CreateElement($nodeName)
        }
        else {
            $node = $xml.CreateElement($nodeName, $parent.NamespaceURI)
        }

        [void]$parent.AppendChild($node)
    }

    $node.InnerText = $value
}

function Get-IssDefinedVersion([string]$issPath) {
    foreach ($line in Get-Content -Path $issPath) {
        if ($line -match '^#define\s+MyAppVersion\s+"([^"]+)"$') {
            return $Matches[1].Trim()
        }
    }

    return $null
}

function Update-ProjectVersion([string]$csprojPath, [string]$issPath) {
    [xml]$projectXml = Get-Content -Path $csprojPath -Raw
    $projectNode = $projectXml.DocumentElement
    if ($null -eq $projectNode -or $projectNode.LocalName -ne "Project") {
        throw "Arquivo csproj invalido em '$csprojPath'."
    }

    $propertyGroups = @($projectNode.ChildNodes | Where-Object {
            $_.NodeType -eq [System.Xml.XmlNodeType]::Element -and $_.LocalName -eq "PropertyGroup"
        })

    $propertyGroup = $null
    $currentVersionNode = $null
    foreach ($group in $propertyGroups) {
        $versionCandidate = Get-XmlChildByLocalName -parent $group -nodeName "Version"
        if ($versionCandidate -and -not [string]::IsNullOrWhiteSpace($versionCandidate.InnerText)) {
            $propertyGroup = $group
            $currentVersionNode = $versionCandidate
            break
        }
    }

    if ($null -eq $propertyGroup) {
        if ($propertyGroups.Count -gt 0) {
            $propertyGroup = $propertyGroups[0]
            $currentVersionNode = Get-XmlChildByLocalName -parent $propertyGroup -nodeName "Version"
        }
        else {
            if ([string]::IsNullOrWhiteSpace($projectNode.NamespaceURI)) {
                $propertyGroup = $projectXml.CreateElement("PropertyGroup")
            }
            else {
                $propertyGroup = $projectXml.CreateElement("PropertyGroup", $projectNode.NamespaceURI)
            }

            [void]$projectNode.AppendChild($propertyGroup)
        }
    }

    $currentVersion = $null
    if ($currentVersionNode -and -not [string]::IsNullOrWhiteSpace($currentVersionNode.InnerText)) {
        $currentVersion = $currentVersionNode.InnerText.Trim()
    }

    if ([string]::IsNullOrWhiteSpace($currentVersion)) {
        $currentVersion = Get-IssDefinedVersion -issPath $issPath
    }

    if ([string]::IsNullOrWhiteSpace($currentVersion)) {
        $currentVersion = "0.1.0"
    }

    $newVersion = Get-IncrementedVersion $currentVersion
    $assemblyVersion = "$newVersion.0"

    Set-XmlChildNodeValue -xml $projectXml -parent $propertyGroup -nodeName "Version" -value $newVersion
    Set-XmlChildNodeValue -xml $projectXml -parent $propertyGroup -nodeName "AssemblyVersion" -value $assemblyVersion
    Set-XmlChildNodeValue -xml $projectXml -parent $propertyGroup -nodeName "FileVersion" -value $assemblyVersion
    $projectXml.Save($csprojPath)

    $issContent = Get-Content -Path $issPath
    $issVersionUpdated = $false
    $updatedIssContent = $issContent | ForEach-Object {
        if ($_ -match '^#define MyAppVersion "\d+\.\d+\.\d+"$') {
            $issVersionUpdated = $true
            "#define MyAppVersion `"$newVersion`""
        }
        else {
            $_
        }
    }

    if (-not $issVersionUpdated) {
        $updatedIssContent = @("#define MyAppVersion `"$newVersion`"") + $updatedIssContent
    }

    Set-Content -Path $issPath -Value $updatedIssContent -Encoding UTF8

    return $newVersion
}

if (-not (Test-Path -Path $projectPath)) {
    throw "Arquivo de projeto nao encontrado em '$projectPath'."
}

if (-not (Test-Path -Path $innoScript)) {
    throw "Script do Inno Setup nao encontrado em '$innoScript'."
}

if (-not (Test-Path -Path $isccPath)) {
    throw "ISCC.exe nao encontrado. Instale o Inno Setup 6 em '$isccPath'."
}

$newVersion = Update-ProjectVersion -csprojPath $projectPath -issPath $innoScript
Write-Host "Versao incrementada automaticamente para: $newVersion"

if ($SkipPublish) {
    Write-Warning "Com incremento automatico de versao, -SkipPublish foi desabilitado para manter o executavel consistente."
    $SkipPublish = $false
}

if (-not $SkipPublish) {
    Write-Host "Executando publish antes de gerar o instalador..."
    & (Join-Path $PSScriptRoot "publish-win-x64.ps1")
}
elseif (-not (Test-Path $publishExe)) {
    throw "Publish nao encontrado em '$publishExe'. Execute sem -SkipPublish."
}

& $isccPath $innoScript

Write-Host "Instalador gerado em: $(Join-Path $repoRoot 'dist')"
