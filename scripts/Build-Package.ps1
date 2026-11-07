param(
    [switch]$SkipBuild,
    [string]$CertificateThumbprint,
    [string]$PublishDirectory,
    [ValidateSet('x64','arm64','x86')][string]$Architecture = 'x64',
    [string]$OutputDirectory,
    [string]$StoreVersion
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Versioning.ps1')
if ($PSBoundParameters.ContainsKey('StoreVersion')) { $StoreVersion = Assert-KeepClipStoreVersion -Version $StoreVersion }
$productRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $productRoot 'artifacts' }
$publishRoot = Join-Path $productRoot "artifacts/publish-$Architecture"
if ($PublishDirectory) { $publishRoot = [System.IO.Path]::GetFullPath($PublishDirectory) }
New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null
if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'Publish-App.ps1') -Architecture $Architecture -PublishDirectory $publishRoot
}
if (-not (Test-Path -LiteralPath (Join-Path $publishRoot 'KeepClip.exe'))) { throw 'Eseguibile pubblicato non disponibile.' }
$version = Get-KeepClipPublishedVersion -Directory $publishRoot
. (Join-Path $PSScriptRoot 'PortablePackage.ps1')
$actual = Get-KeepClipExecutableArchitecture -Path (Join-Path $publishRoot 'KeepClip.exe')
if ($actual -ne $Architecture) { throw "Architettura eseguibile $actual diversa da quella richiesta: $Architecture." }
[xml]$manifest = Get-Content -LiteralPath (Join-Path $productRoot 'packaging/AppxManifest.xml') -Raw
$manifest.Package.Identity.ProcessorArchitecture = $Architecture
$manifest.Package.Identity.Version = if ($StoreVersion) { $StoreVersion } else { $version.AssemblySemFileVer }
$manifest.Save((Join-Path $publishRoot 'AppxManifest.xml'))
$assetRoot = Join-Path $publishRoot 'Assets'
New-Item -ItemType Directory -Path $assetRoot -Force | Out-Null
foreach ($asset in @('StoreLogo.png', 'Square44x44Logo.png', 'Square150x150Logo.png', 'KeepClip.ico')) {
    if (-not (Test-Path -LiteralPath (Join-Path $assetRoot $asset))) {
        throw "Asset mancante nel publish: $asset. Genera gli asset con scripts/Generate-AppIcons.ps1 e ripeti il publish."
    }
}
$sdkBinRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
$sdk = Get-ChildItem -LiteralPath $sdkBinRoot -Directory | Where-Object { $_.Name -match '^10\.0\.\d+\.0$' -and (Test-Path -LiteralPath (Join-Path $_.FullName 'x64/makeappx.exe')) } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
if (-not $sdk) { throw 'MakeAppx non trovato. Installa Windows SDK tramite Visual Studio.' }
New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
$packageName = if ($StoreVersion) { "KeepClip-$StoreVersion-store-windows-$Architecture.msix" } else { "KeepClip-$($version.SemVer)-windows-$Architecture.msix" }
$packagePath = Join-Path $artifactRoot $packageName
& (Join-Path $sdk.FullName 'x64/makeappx.exe') pack /d $publishRoot /p $packagePath /o
if ($LASTEXITCODE -ne 0) { throw 'Creazione MSIX non riuscita.' }
if ($CertificateThumbprint) {
    # Sign with an existing certificate in CurrentUser/My; no private key or password is stored here.
    & (Join-Path $sdk.FullName 'x64/signtool.exe') sign /fd SHA256 /sha1 $CertificateThumbprint $packagePath
    if ($LASTEXITCODE -ne 0) { throw "Firma non riuscita. Il subject del certificato deve corrispondere al Publisher del manifest: $($manifest.Package.Identity.Publisher)." }
}
Write-Output "Pacchetto: $packagePath"
if ($StoreVersion) { Write-Output "Versione pacchetto Store: $StoreVersion. La versione dell'eseguibile resta $($version.AssemblySemFileVer) (GitVersion)." }
if (-not $CertificateThumbprint) {
    if ($StoreVersion) { Write-Output 'MSIX non firmato per invio al Microsoft Store; Microsoft gestisce la firma dopo la certificazione.' }
    else { Write-Output 'Pacchetto non firmato: occorre firmarlo prima di installarlo.' }
}
