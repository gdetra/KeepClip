param([string]$FixturesDirectory = './artifacts/release-tests')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ReleaseChecks.ps1')
$fixture = Join-Path ([IO.Path]::GetFullPath($FixturesDirectory)) ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
function Invoke-FixtureGit([string[]]$Arguments) {
    $result = & git -C $fixture -c user.name=KeepClip-tests -c user.email=tests@example.invalid -c commit.gpgsign=false @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Fixture Git failed: $Arguments" }
    return $result
}
function Get-FixtureRelease([string]$Tag, [string]$Channel = 'store') {
    & (Join-Path $PSScriptRoot 'Get-ReleaseVersion.ps1') -Tag $Tag -Channel $Channel -Repository $fixture -MainRef refs/heads/main
}
function Assert-Rejected([scriptblock]$Action) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw 'Unsafe release input was accepted.' }
}
Invoke-FixtureGit @('init','-b','main') | Out-Null
Set-Content -LiteralPath (Join-Path $fixture 'sample.txt') -Value 'Initial source'
Invoke-FixtureGit @('add','sample.txt') | Out-Null
Invoke-FixtureGit @('commit','-m','Initial fixture') | Out-Null
$sha = Invoke-FixtureGit @('rev-parse','HEAD')
Invoke-FixtureGit @('tag','-a','v1.2.3','-m','Release fixture') | Out-Null
$release = Get-FixtureRelease 'v1.2.3'
if ($release.Sha -ne $sha -or $release.SemVer -ne '1.2.3' -or $release.StoreVersion -ne '1.2.3.0') { throw 'Annotated release metadata mismatch.' }
Invoke-FixtureGit @('tag','v1.2.4') | Out-Null
if ((Get-FixtureRelease 'v1.2.4').Sha -ne $sha) { throw 'Lightweight release tag rejected.' }
foreach ($tag in @('main','refs/heads/main','V1.2.3','v01.2.3','v1.2.3-beta.1','v1.2.3.0','v1.2.3;exit','v65536.0.0','v9.9.9')) {
    Assert-Rejected { Get-FixtureRelease $tag }
}
Invoke-FixtureGit @('tag','v0.2.0') | Out-Null
Assert-Rejected { Get-FixtureRelease 'v0.2.0' }
if ((Get-FixtureRelease 'v0.2.0' 'portable').SemVer -ne '0.2.0') { throw 'Portable major-zero release rejected.' }
$blob = Invoke-FixtureGit @('rev-parse','HEAD:sample.txt')
Invoke-FixtureGit @('tag','v1.2.5',$blob) | Out-Null
Assert-Rejected { Get-FixtureRelease 'v1.2.5' }
Invoke-FixtureGit @('switch','-c','feature/unmerged') | Out-Null
Invoke-FixtureGit @('commit','--allow-empty','-m','Unmerged source') | Out-Null
Invoke-FixtureGit @('tag','v1.3.0') | Out-Null
Assert-Rejected { Get-FixtureRelease 'v1.3.0' }
Invoke-FixtureGit @('switch','main') | Out-Null
Invoke-FixtureGit @('commit','--allow-empty','-m','Newer main source') | Out-Null
if ((Get-FixtureRelease 'v1.2.3').Sha -ne $sha) { throw 'Release drifted to newer main.' }

$good = [pscustomobject]@{ id=10; head_sha=$sha; head_branch='main'; event='push'; status='completed'; conclusion='success'; html_url='https://example.invalid/runs/10' }
if ((Assert-KeepClipReleaseCI -Runs @($good) -Sha $sha).id -ne 10) { throw 'Successful main CI rejected.' }
foreach ($change in @(
    @{head_sha=('0' * 40)}, @{head_branch='feature/example'}, @{event='pull_request'},
    @{status='in_progress'}, @{conclusion='failure'}, @{conclusion='cancelled'}
)) {
    $bad = $good | ConvertTo-Json | ConvertFrom-Json
    foreach ($key in $change.Keys) { $bad.$key = $change[$key] }
    Assert-Rejected { Assert-KeepClipReleaseCI -Runs @($bad) -Sha $sha }
}
Assert-Rejected { Assert-KeepClipReleaseCI -Runs @() -Sha $sha }
$newer = $good | ConvertTo-Json | ConvertFrom-Json
$newer.id = 11
$newer.status = 'queued'
$newer.conclusion = $null
Assert-Rejected { Assert-KeepClipReleaseCI -Runs @($good,$newer) -Sha $sha }
$manual = $good | ConvertTo-Json | ConvertFrom-Json
$manual.event = 'workflow_dispatch'
if ((Assert-KeepClipReleaseCI -Runs @($manual) -Sha $sha).id -ne 10) { throw 'Manual CI on main rejected.' }
Write-Output 'Release checks passed: tags, main ancestry, frozen SHA, Store versions and exact-commit CI gates.'
