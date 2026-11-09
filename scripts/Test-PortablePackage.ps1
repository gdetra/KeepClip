param(
    [Parameter(Mandatory=$true)][string]$FixturesDirectory,
    [ValidateSet('x64','arm64','x86')][string]$Architecture = 'x64'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'PortablePackage.ps1')
$fixtureRoot = Join-Path ([IO.Path]::GetFullPath($FixturesDirectory)) ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
$required = @('KeepClip.exe','KeepClip.dll','KeepClip.pri','KeepClip.runtimeconfig.json','hostfxr.dll','hostpolicy.dll','coreclr.dll','Microsoft.WindowsAppRuntime.dll','Microsoft.Internal.FrameworkUdk.dll','Microsoft.ui.xaml.dll')
foreach ($name in $required) { Set-Content -LiteralPath (Join-Path $fixtureRoot $name) -Value ('fixture ' + $name) -Encoding UTF8 }
# Minimal PE header fixture: never executed, only used to inspect the machine type.
$pe = New-Object byte[] 128
$pe[0] = 0x4d; $pe[1] = 0x5a; $pe[60] = 64; $pe[64] = 0x50; $pe[65] = 0x45
$machine = @{ x64 = 0x8664; arm64 = 0xaa64; x86 = 0x014c }[$Architecture]
[BitConverter]::GetBytes([uint16]$machine).CopyTo($pe, 68)
[IO.File]::WriteAllBytes((Join-Path $fixtureRoot 'KeepClip.exe'), $pe)
Write-KeepClipPackageManifest -Directory $fixtureRoot -Architecture $Architecture
$metadata = Get-Content -LiteralPath (Join-Path $fixtureRoot 'KeepClip.package.json') -Raw | ConvertFrom-Json
if ($metadata.Architecture -ne $Architecture) { throw "FAIL: architettura manifest diversa da $Architecture." }
Write-Output "PASS: manifest $Architecture"
$wrongArchitecture = if ($Architecture -eq 'x64') { 'x86' } else { 'x64' }
$rejected = $false
try { Write-KeepClipPackageManifest -Directory $fixtureRoot -Architecture $wrongArchitecture }
catch { $rejected = $true }
if (-not $rejected) { throw 'FAIL: payload di architettura diversa accettato.' }
Write-Output 'PASS: architettura del payload verificata'
if (@(Test-KeepClipPackage -Directory $fixtureRoot).Count -ne 0) { throw 'FAIL: pacchetto integro rifiutato.' }
Write-Output 'PASS: integrita dei file'
Set-Content -LiteralPath (Join-Path $fixtureRoot 'KeepClip.dll') -Value 'danneggiato' -Encoding UTF8
if (@(Test-KeepClipPackage -Directory $fixtureRoot) -notcontains 'File diverso o danneggiato: KeepClip.dll') { throw 'FAIL: alterazione non rilevata.' }
Write-Output 'PASS: file alterato rilevato'
Set-Content -LiteralPath (Join-Path $fixtureRoot 'KeepClip.dll') -Value 'fixture KeepClip.dll' -Encoding UTF8
Remove-Item -LiteralPath (Join-Path $fixtureRoot 'KeepClip.pri')
if (@(Test-KeepClipPackage -Directory $fixtureRoot) -notcontains 'File mancante: KeepClip.pri') { throw 'FAIL: file mancante non rilevato.' }
Write-Output 'PASS: estrazione incompleta rilevata'
$manifest = Get-Content -LiteralPath (Join-Path $fixtureRoot 'KeepClip.package.json') -Raw | ConvertFrom-Json
$manifest.Files += [pscustomobject]@{ Path='../outside.txt'; Length=1; SHA256='00' }
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $fixtureRoot 'KeepClip.package.json') -Encoding UTF8
if (@(Test-KeepClipPackage -Directory $fixtureRoot) -notcontains 'Percorso esterno alla cartella nel manifest.') { throw 'FAIL: percorso esterno non rifiutato.' }
Write-Output 'PASS: percorso esterno rifiutato'
Set-Content -LiteralPath (Join-Path $fixtureRoot 'KeepClip.package.json') -Value '[' -Encoding UTF8
if (@(Test-KeepClipPackage -Directory $fixtureRoot) -notcontains 'Manifest di verifica non leggibile.') { throw 'FAIL: JSON non valido non rilevato.' }
Write-Output 'PASS: manifest non valido rilevato'
Write-Output "7 verifiche superate ($Architecture); nessun avvio o accesso agli appunti."
