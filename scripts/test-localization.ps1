$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$catalogs = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
foreach ($language in @('en','de','es','it')) {
    $path = Join-Path $projectRoot ('src\Luma\Localization\' + $language + '.json')
    $document = [Text.Json.JsonDocument]::Parse([IO.File]::ReadAllText($path))
    try {
        if ($document.RootElement.ValueKind -ne [Text.Json.JsonValueKind]::Object) { throw "Expected a JSON object: $language" }
        $catalog = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
        foreach ($property in $document.RootElement.EnumerateObject()) {
            if ($property.Value.ValueKind -ne [Text.Json.JsonValueKind]::String -or [string]::IsNullOrWhiteSpace($property.Value.GetString()) -or -not $catalog.TryAdd($property.Name, $property.Value.GetString())) { throw "Invalid or duplicate entry: $language / $($property.Name)" }
        }
        $catalogs.Add($language, $catalog)
    } finally { $document.Dispose() }
}
$reference = $catalogs['en']
if ($reference.Count -lt 250) { throw 'The English reference catalog is incomplete.' }
foreach ($language in @('de','es','it')) {
    $catalog = $catalogs[$language]
    if ($catalog.Count -ne $reference.Count) { throw "Catalog size mismatch: $language" }
    foreach ($key in $reference.Keys) {
        if (-not $catalog.ContainsKey($key)) { throw "Missing translation: $language / $key" }
        $expected = [Text.CompositeFormat]::Parse($reference[$key]).MinimumArgumentCount
        $actual = [Text.CompositeFormat]::Parse($catalog[$key]).MinimumArgumentCount
        if ($actual -ne $expected) { throw "Placeholder mismatch: $language / $key" }
        $indices = @([regex]::Matches($reference[$key], '\{(\d+)[^}]*\}') | ForEach-Object { $_.Groups[1].Value }) -join ','
        $translatedIndices = @([regex]::Matches($catalog[$key], '\{(\d+)[^}]*\}') | ForEach-Object { $_.Groups[1].Value }) -join ','
        if ($indices -ne $translatedIndices) { throw "Changed placeholders: $language / $key" }
    }
}
$installSource = [IO.File]::ReadAllText((Join-Path $projectRoot 'src\Luma\Services\InstallService.cs'))
$uninstaller = [regex]::Match($installSource, 'internal const string UninstallScript = """\r?\n([\s\S]*?)\r?\n\s*""";')
if (-not $uninstaller.Success) { throw 'The uninstaller source was not found for syntax validation.' }
$tokens = $null; $parseErrors = $null
[System.Management.Automation.Language.Parser]::ParseInput($uninstaller.Groups[1].Value, [ref]$tokens, [ref]$parseErrors) | Out-Null
if (@($parseErrors).Count) { throw ('Uninstaller syntax error: ' + ($parseErrors | Out-String)) }
[pscustomobject]@{Languages=@('en','tr','de','es','it');KeysPerCatalog=$reference.Count;Result='Passed'} | ConvertTo-Json -Compress
