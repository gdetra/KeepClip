$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Versioning.ps1')

# Store versions must be usable by Windows and leave the revision to the Store.
$checks = 0
foreach ($candidate in @('1.0.0.0', '1.0.1.0', '2.12.345.0', '65535.65535.65535.0')) {
    if ((Assert-KeepClipStoreVersion -Version $candidate) -cne $candidate) { throw "Versione Store valida modificata: $candidate" }
    $checks++
}
foreach ($candidate in @('', ' ', '0.2.0.0', '1.0.0.1', '1.0.0.55040', '1.0.0', '1.0.0.0.0', '1.0.0.0-ci.1',
    '65536.0.0.0', '1.65536.0.0', '1.0.65536.0', '01.0.0.0', '1.00.0.0', '-1.0.0.0', '1.0.0.0 ', "1.0.0.0`n", '../outside')) {
    $rejected = $false
    try { Assert-KeepClipStoreVersion -Version $candidate | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw "Versione Store non valida accettata: [$candidate]" }
    $checks++
}
# A bad Store version must fail before publishing or creating output folders.
$unusedRoot = Join-Path ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../artifacts'))) ('invalid-store-' + [Guid]::NewGuid().ToString('N'))
$rejected = $false
try {
    & (Join-Path $PSScriptRoot 'Build-Package.ps1') -StoreVersion '1.0.0.1' -PublishDirectory (Join-Path $unusedRoot 'publish') -OutputDirectory (Join-Path $unusedRoot 'packages') | Out-Null
} catch {
    if ($_.Exception.Message -notlike 'Versione Store non valida:*') { throw }
    $rejected = $true
}
if (-not $rejected -or (Test-Path -LiteralPath $unusedRoot)) { throw 'Versione Store errata non rifiutata prima degli effetti su disco.' }
$checks++
Write-Output "$checks Store version checks passed."
