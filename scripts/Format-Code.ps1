param([switch]$Check)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repositoryRoot
try {
    $files = @(& git ls-files --cached --others --exclude-standard -- '*.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate C# files.' }
    $projects = @(& git ls-files --cached --others --exclude-standard -- '*.csproj')
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate C# projects.' }
    # Git still lists tracked deletions until they are staged; only format existing files.
    $files = @($files | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
    $projects = @($projects | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
    [string[]]$checkArguments = if ($Check) { @('--verify-no-changes') } else { @() }
    [string[]]$signatureArguments = if ($Check) { @('--check') } else { @() }
    $signatureProject = Join-Path $repositoryRoot 'tools/KeepClip.CodeStyle/KeepClip.CodeStyle.csproj'
    # Build the syntax checker before project-aware style fixes inspect its project.
    & dotnet build $signatureProject -c Release --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'Cannot build the method signature formatter.' }

    # Linked source files have different compilation contexts in app and test projects.
    # Format their owning project only: solution-wide style fixes can produce merge markers.
    foreach ($project in $projects) {
        $directory = ($project -replace '/[^/]+$', '') + '/'
        $ownedFiles = @($files | Where-Object { $_.StartsWith($directory, [StringComparison]::Ordinal) })
        if ($ownedFiles.Count -eq 0) { continue }
        & dotnet format style $project --no-restore --severity info --diagnostics IDE0003 IDE0005 IDE0011 IDE0040 IDE1006 --include @ownedFiles @checkArguments
        if ($LASTEXITCODE -ne 0) { throw "C# style check failed: $project" }
    }

    & dotnet run --project $signatureProject -c Release --no-build -- @signatureArguments @files
    if ($LASTEXITCODE -ne 0) { throw 'C# method signature check failed.' }
    & dotnet format whitespace . --folder --include @files @checkArguments
    if ($LASTEXITCODE -ne 0) { throw 'C# whitespace check failed.' }
} finally {
    Pop-Location
}
