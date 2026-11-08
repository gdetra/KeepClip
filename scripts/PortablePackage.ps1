# Package integrity checks used during portable packaging and verification.
function Get-KeepClipExecutableArchitecture {
    param([Parameter(Mandatory=$true)][string]$Path)
    $reader = [IO.BinaryReader]::new([IO.File]::OpenRead($Path))
    try {
        if ($reader.BaseStream.Length -lt 64 -or $reader.ReadUInt16() -ne 0x5a4d) { throw 'Header DOS non valido.' }
        $reader.BaseStream.Position = 60
        $offset = $reader.ReadInt32()
        if ($offset -lt 64 -or $offset -gt $reader.BaseStream.Length - 6) { throw 'Offset PE non valido.' }
        $reader.BaseStream.Position = $offset
        if ($reader.ReadUInt32() -ne 0x00004550) { throw 'Header PE non valido.' }
        switch ($reader.ReadUInt16()) {
            0x8664 { return 'x64' }
            0xaa64 { return 'arm64' }
            0x014c { return 'x86' }
            default { throw 'Architettura PE non supportata.' }
        }
    }
    finally { $reader.Dispose() }
}

function Write-KeepClipPackageManifest {
    param(
        [Parameter(Mandatory=$true)][string]$Directory,
        [ValidateSet('x64','arm64','x86')][string]$Architecture = 'x64'
    )
    $bundleRoot = (Resolve-Path -LiteralPath $Directory).Path.TrimEnd('\','/')
    $actual = Get-KeepClipExecutableArchitecture -Path (Join-Path $bundleRoot 'KeepClip.exe')
    if ($actual -ne $Architecture) { throw "Architettura eseguibile $actual diversa da quella richiesta: $Architecture." }
    $entries = @(Get-ChildItem -LiteralPath $bundleRoot -File -Recurse | Where-Object { $_.Name -ne 'KeepClip.package.json' } | Sort-Object FullName | ForEach-Object {
        [ordered]@{ Path = $_.FullName.Substring($bundleRoot.Length + 1).Replace('\','/'); Length = $_.Length; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
    [ordered]@{ FormatVersion=1; Architecture=$Architecture; Files=$entries } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $bundleRoot 'KeepClip.package.json') -Encoding UTF8
}

function Test-KeepClipPackage {
    param([Parameter(Mandatory=$true)][string]$Directory)
    $bundleRoot = (Resolve-Path -LiteralPath $Directory).Path.TrimEnd('\','/')
    $manifestPath = Join-Path $bundleRoot 'KeepClip.package.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { return 'Manifest di verifica mancante: estrai nuovamente lo ZIP completo.' }
    try { $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -ErrorAction Stop }
    catch { return 'Manifest di verifica non leggibile.' }
    if ($manifest.FormatVersion -ne 1 -or $manifest.Architecture -notin @('x64','arm64','x86') -or @($manifest.Files).Count -eq 0) { return 'Manifest di verifica non valido.' }
    $requiredFiles = @('KeepClip.exe','KeepClip.dll','KeepClip.pri','KeepClip.runtimeconfig.json','hostfxr.dll','hostpolicy.dll','coreclr.dll','Microsoft.WindowsAppRuntime.dll','Microsoft.Internal.FrameworkUdk.dll','Microsoft.ui.xaml.dll')
    foreach ($name in $requiredFiles) {
        if (@($manifest.Files | Where-Object { $_.Path -eq $name }).Count -ne 1) { Write-Output "Manifest incompleto: $name" }
    }
    foreach ($entry in $manifest.Files) {
        # A manifest can never direct file reads outside the extracted bundle.
        try { $entryPath = [IO.Path]::GetFullPath((Join-Path $bundleRoot $entry.Path)) }
        catch { Write-Output 'Percorso non valido nel manifest.'; continue }
        if (-not $entryPath.StartsWith($bundleRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { Write-Output 'Percorso esterno alla cartella nel manifest.'; continue }
        if (-not (Test-Path -LiteralPath $entryPath -PathType Leaf)) { Write-Output "File mancante: $($entry.Path)"; continue }
        if ((Get-Item -LiteralPath $entryPath).Length -ne $entry.Length -or (Get-FileHash -LiteralPath $entryPath -Algorithm SHA256).Hash -ne $entry.SHA256) { Write-Output "File diverso o danneggiato: $($entry.Path)" }
    }
}
