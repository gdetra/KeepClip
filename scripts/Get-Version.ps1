param([string]$OutputPath)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Versioning.ps1')
$productRoot = Split-Path -Parent $PSScriptRoot
Push-Location $productRoot
try {
    $json = & dotnet tool run dotnet-gitversion /output json /nocache
    if ($LASTEXITCODE -ne 0) { throw 'GitVersion non riuscito. Esegui dotnet tool restore e verifica che la cronologia Git sia completa.' }
    $version = ConvertTo-KeepClipPublicVersion -Version (($json -join [Environment]::NewLine) | ConvertFrom-Json)
    if ($OutputPath) {
        $metadataPath = [IO.Path]::GetFullPath($OutputPath)
        New-Item -ItemType Directory -Path (Split-Path -Parent $metadataPath) -Force | Out-Null
        $version | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $metadataPath -Encoding utf8
    }
    return $version
}
finally { Pop-Location }
