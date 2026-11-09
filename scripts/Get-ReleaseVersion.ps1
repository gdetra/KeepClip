param(
    [Parameter(Mandatory=$true)][string]$Tag,
    [ValidateSet('store','portable')][string]$Channel = 'store',
    [string]$Repository = (Split-Path -Parent $PSScriptRoot),
    [string]$MainRef = 'refs/remotes/origin/main'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Versioning.ps1')
if ($Tag -cnotmatch '\Av(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z') {
    throw 'Usa un tag stabile nel formato v1.2.3, senza suffissi prerelease.'
}
$semVer = $Tag.Substring(1)
foreach ($part in $semVer.Split('.')) {
    if ([decimal]$part -gt 65535) { throw 'I componenti della versione devono essere al massimo 65535.' }
}
$storeVersion = "$semVer.0"
if ($Channel -eq 'store') { $storeVersion = Assert-KeepClipStoreVersion -Version $storeVersion }
$sha = & git -C $Repository rev-parse --verify "refs/tags/$Tag^{commit}" 2>$null
if ($LASTEXITCODE -ne 0 -or $sha -notmatch '\A[0-9a-f]{40}\z') { throw "Tag di rilascio mancante o non associato a un commit: $Tag" }
& git -C $Repository rev-parse --verify "$MainRef^{commit}" 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Riferimento main non disponibile: $MainRef. Scarica la cronologia completa." }
& git -C $Repository merge-base --is-ancestor $sha $MainRef
if ($LASTEXITCODE -ne 0) { throw "Il tag $Tag non appartiene alla storia di main." }
return [pscustomobject]@{ Tag=$Tag; SemVer=$semVer; StoreVersion=$storeVersion; Sha=$sha }
