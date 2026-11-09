param([string]$FixturesDirectory = './artifacts/version-tests')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Versioning.ps1')
$sha = '0123456789abcdef0123456789abcdef01234567'
$valid = [pscustomobject]@{
    SemVer = '0.2.0-ci.12'
    AssemblySemVer = '0.2.0.0'
    AssemblySemFileVer = '0.2.0.55012'
    InformationalVersion = "0.2.0-ci.12+Branch.main.Sha.$sha"
    Sha = $sha
}
$checked = Assert-KeepClipVersion -Version $valid
if ($checked.SemVer -ne '0.2.0-ci.12' -or $checked.AssemblySemFileVer -ne '0.2.0.55012') { throw 'Valid version metadata changed.' }
$checks = 1
$public = ConvertTo-KeepClipPublicVersion -Version $valid
if ($public.SemVer -ne '0.2.0' -or $public.InformationalVersion -ne '0.2.0' -or $public.AssemblySemFileVer -ne $valid.AssemblySemFileVer -or $public.Sha -ne $sha) {
    throw 'Public versions must be numeric while retaining build and source identity.'
}
$checks++
foreach ($mutation in @(
    @{ Field = 'SemVer'; Value = '../outside' },
    @{ Field = 'SemVer'; Value = '9.9.9' },
    @{ Field = 'AssemblySemFileVer'; Value = '0.2.0' },
    @{ Field = 'AssemblySemFileVer'; Value = '0.2.0.65536' },
    @{ Field = 'AssemblySemFileVer'; Value = '0.3.0.55012' },
    @{ Field = 'AssemblySemVer'; Value = '0.2.0.-1' },
    @{ Field = 'InformationalVersion'; Value = '0.2.0;OtherProperty=bad' },
    @{ Field = 'InformationalVersion'; Value = '0.3.0+another-build' },
    @{ Field = 'Sha'; Value = 'not-a-commit' }
)) {
    $candidate = $valid | ConvertTo-Json | ConvertFrom-Json
    $candidate.($mutation.Field) = $mutation.Value
    $rejected = $false
    try { Assert-KeepClipVersion -Version $candidate | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw "Invalid $($mutation.Field) accepted: $($mutation.Value)" }
    $checks++
}
$fixtureRoot = Join-Path ([IO.Path]::GetFullPath($FixturesDirectory)) ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
$rejected = $false
try { Get-KeepClipPublishedVersion -Directory $fixtureRoot | Out-Null } catch { $rejected = $true }
if (-not $rejected) { throw 'Missing publish metadata accepted.' }
$checks++
$valid | ConvertTo-Json | Set-Content (Join-Path $fixtureRoot 'KeepClip.version.json') -Encoding utf8
# Use an existing system executable as a mismatched payload; never execute it.
Copy-Item -LiteralPath (Join-Path $env:SystemRoot 'System32/cmd.exe') -Destination (Join-Path $fixtureRoot 'KeepClip.exe')
$rejected = $false
try { Get-KeepClipPublishedVersion -Directory $fixtureRoot | Out-Null } catch { $rejected = $true }
if (-not $rejected) { throw 'Payload with a different file version accepted.' }
$checks++
Write-Output "$checks versioning checks passed; no app or clipboard access."
