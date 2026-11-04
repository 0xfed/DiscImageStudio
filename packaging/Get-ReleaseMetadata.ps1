param(
    [string]$Tag,
    [switch]$RequireReleaseNotes,
    [string]$Language
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot 'Localization.ps1')
Initialize-ToolLanguage -Language $Language -Explicit ($PSBoundParameters.ContainsKey('Language'))
$workspaceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
[xml]$buildProps = Get-Content -LiteralPath (Join-Path $workspaceRoot "Directory.Build.props") -Raw
$baseVersion = [string]$buildProps.Project.PropertyGroup.VersionPrefix
$suffixNode = $buildProps.SelectSingleNode('/Project/PropertyGroup/VersionSuffix')
$configuredSuffix = if ($null -ne $suffixNode) { $suffixNode.InnerText } else { '' }
if ($configuredSuffix -and $configuredSuffix -cnotmatch '^(alpha|beta|rc)\.[1-9][0-9]*$') {
    throw (Get-ToolText 'Packaging.VersionSuffix')
}
$number = '(0|[1-9][0-9]*)'
if ($baseVersion -cnotmatch "^$number\.$number\.$number$") {
    throw (Get-ToolText 'Packaging.VersionPrefix')
}
foreach ($part in $baseVersion.Split('.')) {
    # AssemblyVersion components must be below UInt16.MaxValue.
    if ([decimal]$part -gt 65534) {
        throw (Get-ToolText 'Packaging.VersionRange')
    }
}
if ([string]::IsNullOrEmpty($Tag)) {
    $Tag = "v$baseVersion"
    if ($configuredSuffix) { $Tag += "-$configuredSuffix" }
}
if ($Tag -cnotmatch "^v(?<base>$number\.$number\.$number)(?<suffix>-(alpha|beta|rc)\.[1-9][0-9]*)?$") {
    throw (Get-ToolText 'Packaging.TagFormat')
}
if ($Matches['base'] -cne $baseVersion) {
    throw (Get-ToolText 'Packaging.TagMismatch' @($Tag, $baseVersion))
}
$prerelease = -not [string]::IsNullOrEmpty($Matches['suffix'])
$version = $Tag.Substring(1)
$notes = $null
if ($RequireReleaseNotes) {
    $changelog = Get-Content -LiteralPath (Join-Path $workspaceRoot "CHANGELOG.md") -Raw
    $sectionPattern = '(?ms)^## \[' + [regex]::Escape($version) + '\] - (?<date>\d{4}-\d{2}-\d{2})\r?\n(?<notes>.*?)(?=^## |\z)'
    $sections = [regex]::Matches($changelog, $sectionPattern)
    if ($sections.Count -ne 1) {
        throw (Get-ToolText 'Packaging.NotesSection' @($version))
    }
    $null = [datetime]::ParseExact($sections[0].Groups['date'].Value, 'yyyy-MM-dd', [cultureinfo]::InvariantCulture)
    $notes = $sections[0].Groups['notes'].Value.Trim()
    if ([string]::IsNullOrWhiteSpace($notes)) {
        throw (Get-ToolText 'Packaging.NotesEmpty' @($version))
    }
}

[pscustomobject]@{
    Tag = $Tag
    Version = $version
    BaseVersion = $baseVersion
    FileVersion = "$baseVersion.0"
    Prerelease = $prerelease
    Notes = $notes
}
