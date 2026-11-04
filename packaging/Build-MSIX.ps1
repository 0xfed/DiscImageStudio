param(
    [Parameter(Mandatory = $true)]
    [string]$IdentityName,

    [Parameter(Mandatory = $true)]
    [string]$Publisher,

    [string]$PublisherDisplayName = "Disc Image Studio",
    [string]$DisplayName = "Disc Image Studio 光盘绘图工坊",
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string]$Version,
    [string]$CertificateThumbprint,
    [switch]$UnsignedDevelopmentPackage,
    [string]$Language
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot 'Localization.ps1')
Initialize-ToolLanguage -Language $Language -Explicit ($PSBoundParameters.ContainsKey('Language'))

if ([string]::IsNullOrEmpty($Version)) {
    $metadata = & (Join-Path $PSScriptRoot "Get-ReleaseMetadata.ps1") -Language $script:DiscToolLanguage
    if ($metadata.Prerelease) {
        throw (Get-ToolText 'Packaging.StableRequired')
    }
    $Version = $metadata.FileVersion
}

if ($UnsignedDevelopmentPackage -and -not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    throw (Get-ToolText 'Packaging.SigningConflict')
}

$versionParts = $Version.Split('.') | ForEach-Object { [int]$_ }
$invalidVersionPart = @($versionParts | Where-Object { $_ -lt 0 -or $_ -gt 65535 }).Count -ne 0
if (($versionParts.Count -ne 4) -or
    ($versionParts[0] -eq 0) -or
    $invalidVersionPart -or
    ($versionParts[3] -ne 0)) {
    throw (Get-ToolText 'Packaging.StoreVersion')
}

$workspaceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $workspaceRoot "artifacts\msix"))
if (-not $artifactRoot.StartsWith($workspaceRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw (Get-ToolText 'Packaging.PackageOutside')
}

$publishDirectory = Join-Path $artifactRoot "publish"
$stagingDirectory = Join-Path $artifactRoot "staging"
$installDirectory = Join-Path $stagingDirectory "VFS\ProgramFilesX64\DiscImageStudio"
$projectPath = Join-Path $workspaceRoot "src\DiscImageStudio.App\DiscImageStudio.App.csproj"
$assetSource = Join-Path $workspaceRoot "src\DiscImageStudio.App\Assets"
$assetDestination = Join-Path $stagingDirectory "Assets"
$manifestTemplate = Join-Path $PSScriptRoot "AppxManifest.xml.template"
$packageFileName = if ($UnsignedDevelopmentPackage) {
    "DiscImageStudio_{0}_x64_unsigned-dev.msix" -f $Version
}
else {
    "DiscImageStudio_{0}_x64.msix" -f $Version
}
$packagePath = Join-Path $artifactRoot $packageFileName

function Get-SourceSnapshot {
    $sourceFiles = Get-ChildItem -LiteralPath (Join-Path $workspaceRoot "src") -Recurse -File |
        Where-Object {
            $_.Extension -in ".cs", ".xaml", ".csproj", ".json" -and
            $_.FullName -notmatch '[\\/](bin|obj)[\\/]'
        } |
        Sort-Object FullName
    return ($sourceFiles | ForEach-Object {
        "{0}|{1}" -f $_.FullName, (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }) -join "`n"
}

$sourceSnapshotBeforePublish = Get-SourceSnapshot

if (Test-Path -LiteralPath $artifactRoot) {
    Remove-Item -LiteralPath $artifactRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDirectory, $installDirectory, $assetDestination -Force | Out-Null

& dotnet restore $projectPath --runtime win-x64
if ($LASTEXITCODE -ne 0) {
    throw (Get-ToolText 'Packaging.CommandFailed' @('dotnet restore', $LASTEXITCODE))
}

& dotnet clean $projectPath `
    --configuration Release `
    --runtime win-x64
if ($LASTEXITCODE -ne 0) {
    throw (Get-ToolText 'Packaging.CommandFailed' @('dotnet clean', $LASTEXITCODE))
}

& dotnet publish $projectPath `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publishDirectory `
    -p:Version="$($versionParts[0]).$($versionParts[1]).$($versionParts[2])" `
    -p:AssemblyVersion=$Version `
    -p:FileVersion=$Version `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true
if ($LASTEXITCODE -ne 0) {
    throw (Get-ToolText 'Packaging.CommandFailed' @('dotnet publish', $LASTEXITCODE))
}

if ((Get-SourceSnapshot) -ne $sourceSnapshotBeforePublish) {
    throw (Get-ToolText 'Packaging.SourceChanged')
}

$publishedExecutable = Join-Path $publishDirectory "DiscImageStudio.exe"
if ([Diagnostics.FileVersionInfo]::GetVersionInfo($publishedExecutable).FileVersion -ne $Version) {
    throw (Get-ToolText 'Packaging.ManifestVersion')
}
& $publishedExecutable burn-build-info --language $script:DiscToolLanguage
if ($LASTEXITCODE -ne 0) {
    throw (Get-ToolText 'Packaging.LegacySetter')
}

Get-ChildItem -LiteralPath $publishDirectory -Filter "*.pdb" -File | Remove-Item -Force
$referencedRuntimeConfig = Join-Path $publishDirectory "DvdImageSolver.runtimeconfig.json"
if (Test-Path -LiteralPath $referencedRuntimeConfig) {
    Remove-Item -LiteralPath $referencedRuntimeConfig -Force
}

Copy-Item -Path (Join-Path $publishDirectory "*") -Destination $installDirectory -Recurse -Force
Copy-Item -Path (Join-Path $assetSource "*.png") -Destination $assetDestination -Force

function Escape-Xml([string]$Value) {
    return [Security.SecurityElement]::Escape($Value)
}

$effectivePublisher = $Publisher
if ($UnsignedDevelopmentPackage) {
    if ($Publisher -match '(^|,\s*)OID\.2\.25\.\d+=1($|,)') {
        throw (Get-ToolText 'Packaging.PublisherOid')
    }

    # Windows recognizes this exact, fixed marker as the unsigned publisher
    # namespace. It must be the final field in the Publisher distinguished name.
    $unsignedPublisherMarker = "OID.2.25.311729368913984317654407730594956997722=1"
    $effectivePublisher = "{0}, {1}" -f $Publisher, $unsignedPublisherMarker
}

$manifest = Get-Content -LiteralPath $manifestTemplate -Raw
$manifest = $manifest.Replace("__IDENTITY_NAME__", (Escape-Xml $IdentityName))
$manifest = $manifest.Replace("__PUBLISHER__", (Escape-Xml $effectivePublisher))
$manifest = $manifest.Replace("__PUBLISHER_DISPLAY_NAME__", (Escape-Xml $PublisherDisplayName))
$manifest = $manifest.Replace("__DISPLAY_NAME__", (Escape-Xml $DisplayName))
$manifest = $manifest.Replace("__VERSION__", $Version)
$manifestPath = Join-Path $stagingDirectory "AppxManifest.xml"
[IO.File]::WriteAllText($manifestPath, $manifest, [Text.UTF8Encoding]::new($false))

$windowsKitsBin = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
$makeAppx = Get-ChildItem -LiteralPath $windowsKitsBin -Filter "makeappx.exe" -Recurse -File |
    Where-Object { $_.DirectoryName -match '\\x64$' } |
    Sort-Object FullName -Descending |
    Select-Object -First 1
if ($null -eq $makeAppx) {
    throw (Get-ToolText 'Packaging.MakeAppxMissing')
}

& $makeAppx.FullName pack /d $stagingDirectory /p $packagePath /o
if ($LASTEXITCODE -ne 0) {
    throw (Get-ToolText 'Packaging.CommandFailed' @('MakeAppx', $LASTEXITCODE))
}

if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    $signTool = Get-ChildItem -LiteralPath $windowsKitsBin -Filter "signtool.exe" -Recurse -File |
        Where-Object { $_.DirectoryName -match '\\x64$' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if ($null -eq $signTool) {
        throw (Get-ToolText 'Packaging.SignToolMissing')
    }

    & $signTool.FullName sign /sha1 $CertificateThumbprint /fd SHA256 $packagePath
    if ($LASTEXITCODE -ne 0) {
        throw (Get-ToolText 'Packaging.CommandFailed' @('SignTool', $LASTEXITCODE))
    }
}

$hash = Get-FileHash -LiteralPath $packagePath -Algorithm SHA256
Write-Host (Get-ToolText 'Packaging.MsixReady' @($packagePath))
Write-Host "SHA-256: $($hash.Hash)"
if ([string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    if ($UnsignedDevelopmentPackage) {
        Write-Host (Get-ToolText 'Packaging.UnsignedReady')
        Write-Host (Get-ToolText 'Packaging.DevelopmentPublisher' @($effectivePublisher))
    }
    else {
        Write-Host (Get-ToolText 'Packaging.StoreUnsigned')
    }
}
