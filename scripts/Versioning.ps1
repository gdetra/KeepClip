function Assert-KeepClipStoreVersion {
    param([AllowEmptyString()][string]$Version)
    if ($Version -cnotmatch '\A[1-9][0-9]{0,4}\.(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\.0\z') {
        throw 'Versione Store non valida: usa Major.Minor.Build.0, con Major maggiore di zero (esempio: 1.0.0.0).'
    }
    foreach ($part in $Version.Split('.')) {
        if ([int]$part -gt 65535) { throw 'Versione Store non valida: ogni componente deve essere al massimo 65535.' }
    }
    return $Version
}

function Assert-KeepClipVersion {
    param([Parameter(Mandatory=$true)]$Version)
    if ($Version.SemVer -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$') { throw 'SemVer non valida.' }
    $versionPrefix = ([string]$Version.SemVer).Split('-')[0] + '.'
    foreach ($field in @('AssemblySemVer','AssemblySemFileVer')) {
        $value = [string]$Version.$field
        if ($value -notmatch '^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$') { throw "Versione numerica non valida: $field." }
        if (-not $value.StartsWith($versionPrefix, [StringComparison]::Ordinal)) { throw "Versione numerica diversa dalla SemVer: $field." }
        foreach ($part in $value.Split('.')) {
            if ([decimal]$part -gt 65535) { throw "Componente oltre il limite Windows: $field." }
        }
    }
    if ($Version.InformationalVersion -notmatch '^[0-9A-Za-z.+/_-]+$') { throw 'InformationalVersion non valida.' }
    if ($Version.InformationalVersion -ne $Version.SemVer -and -not ([string]$Version.InformationalVersion).StartsWith($Version.SemVer + '+', [StringComparison]::Ordinal)) { throw 'InformationalVersion diversa dalla SemVer.' }
    if ($Version.Sha -notmatch '^[0-9a-f]{40}$') { throw 'Commit Git non valido.' }
    return $Version
}

function Get-KeepClipPublishedVersion {
    param([Parameter(Mandatory=$true)][string]$Directory)
    $metadataPath = Join-Path $Directory 'KeepClip.version.json'
    if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) { throw 'Metadati di versione mancanti: ripubblica con scripts/Publish-App.ps1.' }
    $version = Assert-KeepClipVersion -Version (Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json)
    $file = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path ([IO.Path]::GetFullPath($Directory)) 'KeepClip.exe'))
    $numeric = '{0}.{1}.{2}.{3}' -f $file.FileMajorPart, $file.FileMinorPart, $file.FileBuildPart, $file.FilePrivatePart
    if ($numeric -ne $version.AssemblySemFileVer -or $file.ProductVersion -ne $version.InformationalVersion) { throw 'Versione eseguibile diversa dai metadati del publish.' }
    return $version
}
