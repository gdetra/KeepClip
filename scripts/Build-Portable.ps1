param(
    [switch]$SkipBuild,
    [string]$PublishDirectory,
    [ValidateSet('x64','arm64','x86')][string]$Architecture = 'x64',
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$productRoot = Split-Path -Parent $PSScriptRoot
if (-not $PublishDirectory) { $PublishDirectory = Join-Path $productRoot "artifacts/publish-portable-$Architecture" }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $productRoot 'artifacts' }
$publishRoot = [IO.Path]::GetFullPath($PublishDirectory)
if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'Publish-App.ps1') -Architecture $Architecture -PublishDirectory $publishRoot
}
. (Join-Path $PSScriptRoot 'Versioning.ps1')
$version = Get-KeepClipPublishedVersion -Directory $publishRoot
. (Join-Path $PSScriptRoot 'PortablePackage.ps1')
if (Test-Path -LiteralPath (Join-Path $publishRoot 'AppxManifest.xml')) { throw 'Usa un publish pulito: la cartella contiene gia un manifest MSIX.' }
Write-KeepClipPackageManifest -Directory $publishRoot -Architecture $Architecture
$problems = @(Test-KeepClipPackage -Directory $publishRoot)
if ($problems.Count -gt 0) { throw ($problems -join [Environment]::NewLine) }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$archive = Join-Path $OutputDirectory "KeepClip-$($version.SemVer)-windows-$Architecture.zip"
Compress-Archive -Path (Join-Path $publishRoot '*') -DestinationPath $archive -Force
Write-Output ('ZIP portable: ' + $archive)
