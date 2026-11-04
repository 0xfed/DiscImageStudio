[CmdletBinding()]
param(
    [string]$PackagePath,
    [string]$CertificatePath,
    [string]$Language
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot 'Localization.ps1')
Initialize-ToolLanguage -Language $Language -Explicit ($PSBoundParameters.ContainsKey('Language'))

$workspaceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $packageDirectory = Join-Path $workspaceRoot "artifacts\msix"
    $packages = @(
        Get-ChildItem -LiteralPath $packageDirectory -Filter "DiscImageStudio_*_x64.msix" -File |
            Sort-Object LastWriteTime -Descending
    )
    if ($packages.Count -ne 1) {
        throw (Get-ToolText 'Packaging.PackageCount' @($packageDirectory, $packages.Count))
    }

    $PackagePath = $packages[0].FullName
}
if ([string]::IsNullOrWhiteSpace($CertificatePath)) {
    $CertificatePath = Join-Path $workspaceRoot "artifacts\certificates\DiscImageStudio-Test.cer"
}

$PackagePath = [IO.Path]::GetFullPath($PackagePath)
$CertificatePath = [IO.Path]::GetFullPath($CertificatePath)

if (-not (Test-Path -LiteralPath $PackagePath -PathType Leaf)) {
    throw (Get-ToolText 'Packaging.TestPackageMissing' @($PackagePath))
}
if (-not (Test-Path -LiteralPath $CertificatePath -PathType Leaf)) {
    throw (Get-ToolText 'Packaging.CertificateMissing' @($CertificatePath))
}

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
$isAdministrator = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdministrator) {
    $arguments = @(
        "-NoProfile"
        "-ExecutionPolicy Bypass"
        "-File `"$PSCommandPath`""
        "-PackagePath `"$PackagePath`""
        "-CertificatePath `"$CertificatePath`""
        "-Language $script:DiscToolLanguage"
    ) -join " "

    $process = Start-Process powershell.exe -Verb RunAs -ArgumentList $arguments -Wait -PassThru
    exit $process.ExitCode
}

$certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($CertificatePath)
$trustedCertificatePath = "Cert:\LocalMachine\TrustedPeople\$($certificate.Thumbprint)"

if (-not (Test-Path -LiteralPath $trustedCertificatePath)) {
    Import-Certificate `
        -FilePath $CertificatePath `
        -CertStoreLocation "Cert:\LocalMachine\TrustedPeople" | Out-Null
}

Add-AppxPackage -Path $PackagePath

$installedPackage = Get-AppxPackage -Name "12345.DiscImageStudioArchitectureTest"
if ($null -eq $installedPackage) {
    throw (Get-ToolText 'Packaging.InstallUnreported')
}

Write-Host ""
Write-Host (Get-ToolText 'Packaging.Installed') -ForegroundColor Green
Write-Host (Get-ToolText 'Packaging.StartMenu')
Write-Host (Get-ToolText 'Packaging.CloseWindow')
[void](Read-Host)
