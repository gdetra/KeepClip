param([string]$FixturesDirectory = './artifacts/gitversion-tests')
$ErrorActionPreference = 'Stop'
$productRoot = Split-Path -Parent $PSScriptRoot
$fixtureRoot = Join-Path ([IO.Path]::GetFullPath($FixturesDirectory)) ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
function Invoke-FixtureGit {
    param([string[]]$GitArguments)
    & git -C $fixtureRoot -c user.name=KeepClip-tests -c user.email=tests@example.invalid -c commit.gpgsign=false @GitArguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Fixture Git failed: $GitArguments" }
}
function Read-FixtureVersion {
    $json = & dotnet tool run dotnet-gitversion $fixtureRoot /config (Join-Path $fixtureRoot 'GitVersion.yml') /output json /nocache /nonormalize
    if ($LASTEXITCODE -ne 0) { throw 'GitVersion fixture failed.' }
    return (($json -join [Environment]::NewLine) | ConvertFrom-Json)
}
$oldGitHubActions = $env:GITHUB_ACTIONS
$env:GITHUB_ACTIONS = 'false'
Push-Location $productRoot
try {
    Invoke-FixtureGit -GitArguments @('init','-b','main')
    Copy-Item -LiteralPath (Join-Path $productRoot 'GitVersion.yml') -Destination $fixtureRoot
    Invoke-FixtureGit -GitArguments @('add','GitVersion.yml')
    Invoke-FixtureGit -GitArguments @('commit','-m','Initial fixture')
    $initial = Read-FixtureVersion
    if ($initial.InformationalVersion -ne '1.0.0') { throw 'Executable version must not contain a branch name or prerelease suffix.' }
    if ($initial.SemVer -notmatch '^1\.0\.0-ci\.[0-9]+$') { throw "Unexpected initial version: $($initial.SemVer)" }
    Invoke-FixtureGit -GitArguments @('commit','--allow-empty','-m','Another build')
    $next = Read-FixtureVersion
    if ([version]$next.AssemblySemFileVer -le [version]$initial.AssemblySemFileVer) { throw 'Development file version did not increase.' }
    Invoke-FixtureGit -GitArguments @('tag','v1.0.0')
    $release = Read-FixtureVersion
    if ($release.SemVer -ne '1.0.0' -or $release.AssemblySemFileVer -ne '1.0.0.60000') { throw "Unexpected release version: $($release.SemVer) / $($release.AssemblySemFileVer)" }
    if ([version]$release.AssemblySemFileVer -le [version]$next.AssemblySemFileVer) { throw 'Release would downgrade the development MSIX.' }
    Invoke-FixtureGit -GitArguments @('commit','--allow-empty','-m','fix: correct clipboard behavior')
    $patch = Read-FixtureVersion
    if ($patch.SemVer -notmatch '^1\.0\.1-ci\.[0-9]+$') { throw "Unexpected next patch: $($patch.SemVer)" }
    Invoke-FixtureGit -GitArguments @('switch','-c','feature/example')
    Invoke-FixtureGit -GitArguments @('commit','--allow-empty','-m','Feature work')
    $feature = Read-FixtureVersion
    if ($feature.SemVer -notmatch '-example\.') { throw "Missing feature label: $($feature.SemVer)" }
    Invoke-FixtureGit -GitArguments @('switch','main')
    Invoke-FixtureGit -GitArguments @('commit','--allow-empty','-m','feat(settings): add information page')
    $minor = Read-FixtureVersion
    if ($minor.SemVer -notmatch '^1\.1\.0-ci\.[0-9]+$') { throw "Unexpected minor version: $($minor.SemVer)" }
    Invoke-FixtureGit -GitArguments @('commit','--allow-empty','-m','fix!: change incompatible data format')
    $major = Read-FixtureVersion
    if ($major.SemVer -notmatch '^2\.0\.0-ci\.[0-9]+$') { throw "Unexpected major version: $($major.SemVer)" }
    Invoke-FixtureGit -GitArguments @('tag','v2.0.0')
    Invoke-FixtureGit -GitArguments @('commit','--allow-empty','-m',"refactor: replace storage format`n`nBREAKING CHANGE: existing formats require migration")
    $footer = Read-FixtureVersion
    if ($footer.SemVer -notmatch '^3\.0\.0-ci\.[0-9]+$') { throw "Unexpected breaking footer version: $($footer.SemVer)" }
    Write-Output '8 GitVersion history checks passed: development counter, release tag, patch, feature branch, minor, major and breaking footer.'
}
finally {
    Pop-Location
    $env:GITHUB_ACTIONS = $oldGitHubActions
}
