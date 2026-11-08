param([switch]$Check)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$files = & git -C $repositoryRoot ls-files --cached --others --exclude-standard -- '*.xaml' '*.xml' '*.csproj' '*.props' '*.targets' '*.slnx' '*.manifest' '*.appxmanifest' '*.resw'
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate markup files.' }

$encoding = [System.Text.UTF8Encoding]::new($false)
$different = @()
foreach ($file in $files) {
    $path = Join-Path $repositoryRoot $file
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
    $original = [IO.File]::ReadAllText($path)
    $readerSettings = [Xml.XmlReaderSettings]::new()
    $readerSettings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $reader = [Xml.XmlReader]::Create([IO.StringReader]::new($original), $readerSettings)
    $document = [Xml.XmlDocument]::new()
    $document.XmlResolver = $null
    try { $document.Load($reader) } finally { $reader.Dispose() }

    $settings = [Xml.XmlWriterSettings]::new()
    $settings.Encoding = $encoding
    $settings.Indent = $true
    $settings.IndentChars = '  '
    $settings.NewLineChars = "`r`n"
    $settings.NewLineOnAttributes = $true
    $settings.OmitXmlDeclaration = -not ($document.FirstChild -is [Xml.XmlDeclaration])
    $buffer = [IO.MemoryStream]::new()
    $writer = [Xml.XmlWriter]::Create($buffer, $settings)
    try { $document.Save($writer) } finally { $writer.Dispose() }
    try { $formatted = $encoding.GetString($buffer.ToArray()).TrimEnd("`r", "`n") + "`r`n" }
    finally { $buffer.Dispose() }

    if ($original -cne $formatted) {
        $different += $file
        if (-not $Check) { [IO.File]::WriteAllText($path, $formatted, $encoding) }
    }
}

if ($Check -and $different.Count -gt 0) {
    throw "Markup formatting differs: $($different -join ', '). Run ./scripts/Format-Markup.ps1."
}
Write-Output "Markup formatting: $($files.Count) files checked; $($different.Count) $(if ($Check) { 'differences' } else { 'formatted' })."
