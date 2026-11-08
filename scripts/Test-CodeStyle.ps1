$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot 'tools/KeepClip.CodeStyle/KeepClip.CodeStyle.csproj'
& dotnet build $project -c Release --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Cannot build the code style checker.' }
$fixtures = Join-Path $repositoryRoot ('artifacts/code-style-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtures -Force | Out-Null
$failures = @()

$comment = "/* preserved`r`n              diagram`r`n            */"
$source = "class Fixture`r`n{`r`n    public void Run()`r`n    {`r`n        System.Action action =`r`n            () =>`r`n        {`r`n            $comment`r`n        };`r`n    }`r`n}`r`n"
$commentFile = Join-Path $fixtures 'Comments.cs'
[IO.File]::WriteAllText($commentFile, $source)
& dotnet run --project $project -c Release --no-build -- $commentFile
if ($LASTEXITCODE -ne 0 -or -not [IO.File]::ReadAllText($commentFile).Contains($comment)) {
    $failures += 'Formatting changed multiline comment contents.'
}

$lambdas = foreach ($index in 1..20) { "        System.Action action$index =`r`n            () =>`r`n        {`r`n            System.Console.WriteLine($index);`r`n        };" }
$manyFile = Join-Path $fixtures 'ManyLambdas.cs'
[IO.File]::WriteAllText($manyFile, "class Fixture`r`n{`r`n    public void Run()`r`n    {`r`n$($lambdas -join "`r`n")`r`n    }`r`n}`r`n")
& dotnet run --project $project -c Release --no-build -- $manyFile
if ($LASTEXITCODE -ne 0) { $failures += 'Formatting did not converge for independent lambda blocks.' }

$signatureFile = Join-Path $fixtures 'Signature.cs'
[IO.File]::WriteAllText($signatureFile, "class Fixture`r`n{`r`n    private void Publish(`r`nstring token,`r`n(uint Format, byte[] Bytes)[] content)`r`n    {`r`n    }`r`n}`r`n")
& dotnet run --project $project -c Release --no-build -- $signatureFile
$formatted = [IO.File]::ReadAllText($signatureFile)
if ($LASTEXITCODE -ne 0 -or -not $formatted.Contains("        string token,`r`n        (uint Format, byte[] Bytes)[] content)")) {
    $failures += 'Tuple parameters did not receive a four-space continuation indent.'
}
$multipleTypes = Join-Path $fixtures 'MultipleTypes.cs'
[IO.File]::WriteAllText($multipleTypes, 'class First { } interface ISecond { }')
& dotnet run --project $project -c Release --no-build -- --check $multipleTypes *> (Join-Path $fixtures 'multiple-types.log')
if ($LASTEXITCODE -eq 0) { $failures += 'Multiple top-level types were accepted in one file.' }
& dotnet run --project $project -c Release --no-build -- --check $signatureFile $commentFile $manyFile
if ($LASTEXITCODE -ne 0) { $failures += 'Formatting is not idempotent.' }
if ($failures.Count -gt 0) { throw ($failures -join [Environment]::NewLine) }
Write-Output 'Code style regression checks passed.'
