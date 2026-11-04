[CmdletBinding()]
param(
    [string]$Tag,
    [string]$Language
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Localization.ps1')
Initialize-ToolLanguage -Language $Language -Explicit ($PSBoundParameters.ContainsKey('Language'))

if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw (Get-ToolText 'Packaging.WindowsRequired')
}

$metadata = & (Join-Path $PSScriptRoot 'Get-ReleaseMetadata.ps1') -Tag $Tag -Language $script:DiscToolLanguage
$workspaceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workspacePrefix = $workspaceRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$releaseDirectory = [IO.Path]::GetFullPath((Join-Path $workspaceRoot "artifacts\release\$($metadata.Tag)"))

function Assert-SafeReleasePath([string]$Path) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($workspacePrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw (Get-ToolText 'Packaging.ReleaseOutside' @($fullPath))
    }

    # A junction or symbolic link could make a lexically safe path leave the workspace.
    $currentPath = $fullPath
    while ($currentPath.Length -ge $workspaceRoot.Length) {
        if (Test-Path -LiteralPath $currentPath) {
            $item = Get-Item -LiteralPath $currentPath -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw (Get-ToolText 'Packaging.ReleaseLink' @($currentPath))
            }
        }
        if ($currentPath.Equals($workspaceRoot, [StringComparison]::OrdinalIgnoreCase)) {
            break
        }
        $currentPath = [IO.Path]::GetDirectoryName($currentPath)
    }
}

Assert-SafeReleasePath $releaseDirectory
if (Test-Path -LiteralPath $releaseDirectory) {
    $linkedItems = @(Get-ChildItem -LiteralPath $releaseDirectory -Force -Recurse |
        Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 })
    if ($linkedItems.Count -ne 0) {
        throw (Get-ToolText 'Packaging.RefuseClearLinks' @($releaseDirectory))
    }
    # Only this validated tag's output is replaced; other release versions are retained.
    Remove-Item -LiteralPath $releaseDirectory -Recurse -Force
}

$publishDirectory = Join-Path $releaseDirectory 'publish'
$smokeDirectory = Join-Path $releaseDirectory 'smoke'
$isolatedAppDirectory = Join-Path $smokeDirectory 'app'
$projectPath = Join-Path $workspaceRoot 'src\DiscImageStudio.App\DiscImageStudio.App.csproj'
New-Item -ItemType Directory -Path $publishDirectory, $isolatedAppDirectory -Force | Out-Null

# WPF's temporary project and the app can build the DVD executable with different
# global properties but the same runtimeconfig output. Serialize this publish graph.
& dotnet publish $projectPath `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publishDirectory `
    -maxcpucount:1 `
    -p:BuildInParallel=false `
    "-p:Version=$($metadata.Version)" `
    "-p:AssemblyVersion=$($metadata.FileVersion)" `
    "-p:FileVersion=$($metadata.FileVersion)" `
    -p:RestoreLockedMode=true
if ($LASTEXITCODE -ne 0) {
    throw (Get-ToolText 'Packaging.CommandFailed' @('dotnet publish', $LASTEXITCODE))
}

$publishedExecutable = Join-Path $publishDirectory 'DiscImageStudio.exe'
if (-not (Test-Path -LiteralPath $publishedExecutable -PathType Leaf)) {
    throw (Get-ToolText 'Packaging.ExecutableMissing' @($publishedExecutable))
}
$fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($publishedExecutable).FileVersion
if ($fileVersion -ne $metadata.FileVersion) {
    throw (Get-ToolText 'Packaging.ExecutableVersion' @($fileVersion, $metadata.FileVersion))
}

Get-ChildItem -LiteralPath $publishDirectory -Filter '*.pdb' -File -Recurse -Force |
    Remove-Item -Force
$referencedRuntimeConfig = Join-Path $publishDirectory 'DvdImageSolver.runtimeconfig.json'
if (Test-Path -LiteralPath $referencedRuntimeConfig) {
    Remove-Item -LiteralPath $referencedRuntimeConfig -Force
}

$packageFileName = "DiscImageStudio-$($metadata.Tag)-win-x64.exe"
$packagePath = Join-Path $releaseDirectory $packageFileName
Copy-Item -LiteralPath $publishedExecutable -Destination $packagePath
$checkExecutable = Join-Path $isolatedAppDirectory $packageFileName
Copy-Item -LiteralPath $packagePath -Destination $checkExecutable

function ConvertTo-WindowsArgument([string]$Value) {
    # Start-Process joins ArgumentList into a command line. Quote each argument using
    # Windows' backslash/quote rules so paths containing spaces arrive intact.
    $escaped = [regex]::Replace($Value, '(\\*)"', '$1$1\"')
    $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
    return '"' + $escaped + '"'
}

function Invoke-PublishedCheck([string]$Name, [string[]]$Arguments) {
    Write-Host (Get-ToolText 'Packaging.CheckingPackage' @($Name))
    $commandLine = (@($Arguments) + @('--language', $script:DiscToolLanguage) | ForEach-Object { ConvertTo-WindowsArgument $_ }) -join ' '
    $process = Start-Process -FilePath $checkExecutable `
        -ArgumentList $commandLine `
        -WorkingDirectory $isolatedAppDirectory `
        -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $smokeDirectory "$Name.stdout.log") `
        -RedirectStandardError (Join-Path $smokeDirectory "$Name.stderr.log") `
        -Wait -PassThru
    $exitCode = $process.ExitCode
    $process.Dispose()
    if ($exitCode -ne 0) {
        throw (Get-ToolText 'Packaging.PackageCheckFailed' @($Name, $exitCode, $smokeDirectory))
    }
}

function Assert-FileLength([string]$Path, [long]$ExpectedLength) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw (Get-ToolText 'Packaging.SmokeMissing' @($Path))
    }
    $actualLength = (Get-Item -LiteralPath $Path).Length
    if ($actualLength -ne $ExpectedLength) {
        throw (Get-ToolText 'Packaging.SmokeLength' @($Path, $actualLength, $ExpectedLength))
    }
}

Invoke-PublishedCheck 'burn-build-info' @('burn-build-info')
Invoke-PublishedCheck 'selftest' @('selftest')

# Spaces in these paths also exercise argument quoting in the packaged WinExe.
$inputImage = Join-Path $smokeDirectory 'input image.png'
Copy-Item -LiteralPath (Join-Path $workspaceRoot 'src\DiscImageStudio.App\Assets\DiscImageStudio-256.png') `
    -Destination $inputImage
$rawPath = Join-Path $smokeDirectory 'two sectors.raw'
Invoke-PublishedCheck 'cd-raw' @('cd-generate', '--input', $inputImage, '--output', $rawPath, '--sectors', '2')
Assert-FileLength $rawPath 4704

$wavePath = Join-Path $smokeDirectory 'two sectors.wav'
Invoke-PublishedCheck 'cd-wav' @('cd-generate', '--input', $inputImage, '--output', $wavePath, '--sectors', '2')
Assert-FileLength $wavePath 4748
$cuePath = [IO.Path]::ChangeExtension($wavePath, '.cue')
if (-not (Test-Path -LiteralPath $cuePath -PathType Leaf) -or (Get-Item -LiteralPath $cuePath).Length -eq 0) {
    throw (Get-ToolText 'Packaging.CueMissing' @($cuePath))
}

# The CUE sheet is opt-out: --cue false must still write the WAV and leave no .cue behind.
$noCuePath = Join-Path $smokeDirectory 'no cue.wav'
Invoke-PublishedCheck 'cd-wav-no-cue' @(
    'cd-generate', '--input', $inputImage, '--output', $noCuePath, '--sectors', '2', '--cue', 'false')
Assert-FileLength $noCuePath 4748
if (Test-Path -LiteralPath ([IO.Path]::ChangeExtension($noCuePath, '.cue'))) {
    throw (Get-ToolText 'Packaging.UnexpectedCue' @($noCuePath))
}

$snapshotPath = Join-Path $smokeDirectory 'ui snapshot.png'
Invoke-PublishedCheck 'ui-snapshot' @('ui-snapshot', '--output', $snapshotPath)
if (-not (Test-Path -LiteralPath $snapshotPath -PathType Leaf) -or (Get-Item -LiteralPath $snapshotPath).Length -eq 0) {
    throw (Get-ToolText 'Packaging.SnapshotMissing' @($snapshotPath))
}

$hash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ((Get-FileHash -LiteralPath $checkExecutable -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash) {
    throw (Get-ToolText 'Packaging.IsolatedMismatch')
}
$checksumPath = Join-Path $releaseDirectory 'SHA256SUMS.txt'
[IO.File]::WriteAllText($checksumPath, "$hash  $packageFileName`n", [Text.UTF8Encoding]::new($false))
Write-Host (Get-ToolText 'Packaging.ReleaseReady' @($packagePath))
Write-Host (Get-ToolText 'Packaging.Checksums' @($checksumPath))
