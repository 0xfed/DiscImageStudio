# Shared with the application and Python tools. Compatible with Windows PowerShell 5.1.
# UI language selection deliberately leaves numeric culture and metadata untouched.
$script:DiscToolCatalog = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
$script:DiscToolLanguage = 'en'
$script:DiscToolCatalogDirectory = Join-Path $PSScriptRoot '../src/DiscImageStudio.Localization/Catalogs'

function Get-ToolText {
    param([string]$Key, [object[]]$Values = @())
    $text = $Key
    if ($script:DiscToolCatalog.ContainsKey($Key)) {
        $entry = $script:DiscToolCatalog[$Key]
        $translation = $entry.PSObject.Properties[$script:DiscToolLanguage]
        if ($null -eq $translation) { $translation = $entry.PSObject.Properties['zh-CN'] }
        if ($null -ne $translation) { $text = [string]$translation.Value }
    }
    if ($Values.Count -eq 0) { return $text }
    return [string]::Format([Globalization.CultureInfo]::CurrentCulture, $text, $Values)
}

function Normalize-ToolLanguage {
    param([string]$Language)
    $name = $Language.Trim().ToLowerInvariant().Replace('_', '-')
    if ($name -match '^(en|ja|tr)(-|$)') { return $Matches[1] }
    if ($name -match '^zh-(tw|hk|mo|cht|hant)$' -or $name.StartsWith('zh-hant-')) { return 'zh-TW' }
    if ($name -match '^(zh|zh-cn|zh-sg|zh-hans|zh-chs)$' -or $name.StartsWith('zh-hans-')) { return 'zh-CN' }
    return 'system'
}

function Resolve-ToolLanguage {
    param([string]$Language)
    $normalized = Normalize-ToolLanguage $Language
    if ($normalized -ne 'system') { return $normalized }
    $culture = [Globalization.CultureInfo]::CurrentUICulture
    if ($culture.TwoLetterISOLanguageName -in 'ja', 'tr') { return $culture.TwoLetterISOLanguageName }
    if ($culture.TwoLetterISOLanguageName -eq 'zh') {
        if ((Normalize-ToolLanguage $culture.Name) -eq 'zh-TW') { return 'zh-TW' }
        return 'zh-CN'
    }
    return 'en'
}

function Initialize-ToolLanguage {
    param([string]$Language, [bool]$Explicit = $false)
    $script:DiscToolLanguage = Resolve-ToolLanguage $env:DISC_IMAGE_STUDIO_LANGUAGE
    $script:DiscToolCatalog.Clear()
    $duplicates = [Collections.Generic.List[string]]::new()
    foreach ($file in (Get-ChildItem -LiteralPath $script:DiscToolCatalogDirectory -Filter '*.json' -File | Sort-Object Name)) {
        $entries = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($entry in $entries.PSObject.Properties) {
            if ($script:DiscToolCatalog.ContainsKey($entry.Name)) { $duplicates.Add($entry.Name) }
            else { $script:DiscToolCatalog.Add($entry.Name, $entry.Value) }
        }
    }
    if ($Explicit) {
        $normalized = Normalize-ToolLanguage $Language
        if ([string]::IsNullOrWhiteSpace($Language) -or ($normalized -eq 'system' -and $Language.Trim() -ine 'system')) {
            throw (Get-ToolText 'Cli.UnsupportedLanguage' @($Language))
        }
        $script:DiscToolLanguage = Resolve-ToolLanguage $Language
    }
    if ($duplicates.Count -ne 0) {
        throw (Get-ToolText 'Duplicate localization key: {0}' @($duplicates[0]))
    }
}
