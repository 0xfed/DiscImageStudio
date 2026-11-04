# Exercises preflight paths only: no package building, signing, installation, or elevation.
$ErrorActionPreference = 'Stop'
$workspaceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $workspaceRoot 'packaging/Localization.ps1')
$originalLanguage = $env:DISC_IMAGE_STUDIO_LANGUAGE
$numericCulture = [Globalization.CultureInfo]::CurrentCulture
$uiCulture = [Globalization.CultureInfo]::CurrentUICulture
$fixtureRoot = Join-Path $workspaceRoot ('artifacts/tests/packaging-localization/' + [guid]::NewGuid().ToString('N'))
$languages = @('zh-CN', 'zh-TW', 'en', 'ja', 'tr')
$count = 0

function Assert-Equal($Actual, $Expected, [string]$Description) {
    if ($Actual -cne $Expected) { throw "$Description`: expected '$Expected', received '$Actual'." }
}

function Assert-Failure([scriptblock]$Action, [string]$Expected) {
    $failure = $null
    try { $null = & $Action }
    catch { $failure = $_ }
    if ($null -eq $failure -or $failure.Exception.Message -cne $Expected) {
        throw "Unexpected preflight result; expected '$Expected', received '$failure'."
    }
    $script:count++
}

# Fail closed if a preflight regression reaches a side effect or external command.
function dotnet { throw 'Unexpected build invocation during preflight.' }
function Start-Process { throw 'Unexpected elevation/process invocation during preflight.' }
function Import-Certificate { throw 'Unexpected certificate import during preflight.' }
function Add-AppxPackage { throw 'Unexpected package installation during preflight.' }
function Get-AppxPackage { throw 'Unexpected package query during preflight.' }

try {
    $env:DISC_IMAGE_STUDIO_LANGUAGE = 'en'
    Initialize-ToolLanguage -Language en -Explicit $true
    $scripts = @('Build-Release.ps1', 'Build-MSIX.ps1', 'Get-ReleaseMetadata.ps1', 'Install-TestPackage.ps1', 'Localization.ps1')
    $discovered = @(Get-ChildItem -LiteralPath (Join-Path $workspaceRoot 'packaging') -Filter '*.ps1' -File | ForEach-Object Name)
    Assert-Equal (($discovered | Sort-Object) -join ',') (($scripts | Sort-Object) -join ',') 'Audit new packaging scripts before declaring coverage'
    $references = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($name in $scripts) {
        $path = Join-Path $workspaceRoot "packaging/$name"
        $tokens = $null
        $errors = $null
        $tree = [Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$errors)
        if ($errors.Count -ne 0) { throw "Invalid PowerShell syntax: $name`: $errors" }
        foreach ($call in $tree.FindAll({ param($node)
            $node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -eq 'Get-ToolText'
        }, $true)) {
            $key = $call.CommandElements[1]
            if ($key -isnot [Management.Automation.Language.StringConstantExpressionAst]) { throw "Unaudited dynamic resource key: $name" }
            [void]$references.Add($key.Value)
            if (-not $script:DiscToolCatalog.ContainsKey($key.Value)) { throw "Missing packaging key: $($key.Value)" }
        }
        foreach ($statement in $tree.FindAll({ param($node) $node -is [Management.Automation.Language.ThrowStatementAst] }, $true)) {
            if ($statement.FindAll({ param($node)
                $node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -eq 'Get-ToolText'
            }, $true).Count -eq 0) { throw "Unlocalized packaging exception: $name`: $($statement.Extent.Text)" }
        }
        foreach ($call in $tree.FindAll({ param($node)
            $node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -in 'Write-Host', 'Write-Warning', 'Write-Error'
        }, $true)) {
            if ($call.FindAll({ param($node)
                $node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -eq 'Get-ToolText'
            }, $true).Count -eq 0 -and $call.CommandElements[1].Extent.Text -notmatch '^(""|"SHA-256: \$\(\$hash.Hash\)")$') {
                throw "Unlocalized packaging output: $name`: $($call.Extent.Text)"
            }
        }
    }
    foreach ($key in $references) {
        foreach ($language in $languages) {
            if ([string]::IsNullOrWhiteSpace($script:DiscToolCatalog[$key].PSObject.Properties[$language].Value)) {
                throw "Missing packaging translation: $key/$language"
            }
        }
    }

    $fixturePackaging = Join-Path $fixtureRoot 'packaging'
    $fixtureCatalogs = Join-Path $fixtureRoot 'src/DiscImageStudio.Localization/Catalogs'
    $fixtureMsix = Join-Path $fixtureRoot 'artifacts/msix'
    New-Item -ItemType Directory -Path $fixturePackaging, $fixtureCatalogs, $fixtureMsix -Force | Out-Null
    foreach ($name in $scripts) { Copy-Item -LiteralPath (Join-Path $workspaceRoot "packaging/$name") -Destination $fixturePackaging }
    Copy-Item -Path (Join-Path $workspaceRoot 'src/DiscImageStudio.Localization/Catalogs/*.json') -Destination $fixtureCatalogs
    $props = '<Project><PropertyGroup><VersionPrefix>1.2.3</VersionPrefix><VersionSuffix>rc.2</VersionSuffix></PropertyGroup></Project>'
    [IO.File]::WriteAllText((Join-Path $fixtureRoot 'Directory.Build.props'), $props)
    $notes = "- user text and identifiers remain unchanged.`n`n### English`n`n- matching release text."
    [IO.File]::WriteAllText((Join-Path $fixtureRoot 'CHANGELOG.md'), "## [1.2.3-rc.2] - 2026-11-03`n$notes`n")
    $metadataScript = Join-Path $fixturePackaging 'Get-ReleaseMetadata.ps1'
    $msixScript = Join-Path $fixturePackaging 'Build-MSIX.ps1'
    $installScript = Join-Path $fixturePackaging 'Install-TestPackage.ps1'
    $releaseTree = [Management.Automation.Language.Parser]::ParseFile((Join-Path $fixturePackaging 'Build-Release.ps1'), [ref]$null, [ref]$null)
    $releaseFunctions = $releaseTree.FindAll({ param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst]
    }, $false)
    $baseline = & $metadataScript -Language en -RequireReleaseNotes | ConvertTo-Json -Compress
    $missingPackage = Join-Path $fixtureRoot 'missing-İ-日本語.msix'
    $missingCertificate = Join-Path $fixtureRoot 'missing-İ-日本語.cer'
    $existingPackage = Join-Path $fixtureRoot 'existing-İ-日本語.msix'
    [IO.File]::WriteAllBytes($existingPackage, [byte[]]@(0))
    [Globalization.CultureInfo]::CurrentCulture = [Globalization.CultureInfo]::GetCultureInfo('fr-FR')

    foreach ($language in $languages) {
        Initialize-ToolLanguage -Language $language -Explicit $true
        $actual = & $metadataScript -Language $language -RequireReleaseNotes | ConvertTo-Json -Compress
        Assert-Equal $actual $baseline 'Metadata and release text changed with language'
        Assert-Failure { & $metadataScript -Language $language -Tag invalid } (Get-ToolText 'Packaging.TagFormat')
        Assert-Failure { & $metadataScript -Language $language -Tag v1.2.4 } (Get-ToolText 'Packaging.TagMismatch' @('v1.2.4', '1.2.3'))
        Assert-Failure { & $msixScript -Language $language -IdentityName Test -Publisher 'CN=Test' -Version 1.2.3.0 -UnsignedDevelopmentPackage -CertificateThumbprint test } (Get-ToolText 'Packaging.SigningConflict')
        Assert-Failure { & $msixScript -Language $language -IdentityName Test -Publisher 'CN=Test' -Version 0.2.3.0 } (Get-ToolText 'Packaging.StoreVersion')
        Assert-Failure { & $installScript -Language $language -PackagePath $missingPackage -CertificatePath $missingCertificate } (Get-ToolText 'Packaging.TestPackageMissing' @($missingPackage))
        Assert-Failure { & $installScript -Language $language -PackagePath $existingPackage -CertificatePath $missingCertificate } (Get-ToolText 'Packaging.CertificateMissing' @($missingCertificate))
        Assert-Failure { & $installScript -Language $language } (Get-ToolText 'Packaging.PackageCount' @($fixtureMsix, 0))
        if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
            Assert-Failure { & (Join-Path $fixturePackaging 'Build-Release.ps1') -Language $language } (Get-ToolText 'Packaging.WindowsRequired')
        }
        # Run the actual release-check functions with a fake process, never the build.
        & {
            param($Language, $FixtureRoot, $Definitions)
            $workspaceRoot = $FixtureRoot
            $workspacePrefix = $workspaceRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
            $smokeDirectory = Join-Path $FixtureRoot 'smoke-İ-日本語'
            $checkExecutable = Join-Path $FixtureRoot 'test executable.exe'
            $isolatedAppDirectory = $FixtureRoot
            foreach ($definition in $Definitions) {
                # Keep parameters declared in the function header, outside Body.
                . ([scriptblock]::Create($definition.Extent.Text))
            }
            function Start-Process {
                param($FilePath, $ArgumentList, $WorkingDirectory, $WindowStyle,
                      $RedirectStandardOutput, $RedirectStandardError, [switch]$Wait, [switch]$PassThru)
                Assert-Equal $ArgumentList ('"selftest" "--language" "' + $Language + '"') 'Child check lost selected language'
                $process = [pscustomobject]@{ ExitCode = 37 }
                $process | Add-Member -MemberType ScriptMethod -Name Dispose -Value { }
                return $process
            }
            $outside = [IO.Path]::GetFullPath((Join-Path $FixtureRoot '../outside'))
            Assert-Failure { Assert-SafeReleasePath $outside } (Get-ToolText 'Packaging.ReleaseOutside' @($outside))
            $missing = Join-Path $FixtureRoot 'missing output-İ-日本語.raw'
            Assert-Failure { Assert-FileLength $missing 4 } (Get-ToolText 'Packaging.SmokeMissing' @($missing))
            $output = Join-Path $FixtureRoot 'short output-İ-日本語.raw'
            [IO.File]::WriteAllBytes($output, [byte[]]@(0, 1, 2))
            Assert-Failure { Assert-FileLength $output 4 } (Get-ToolText 'Packaging.SmokeLength' @($output, 3, 4))
            Assert-Failure { Invoke-PublishedCheck 'check-İ-日本語' @('selftest') } (Get-ToolText 'Packaging.PackageCheckFailed' @('check-İ-日本語', 37, $smokeDirectory))
        } $language $fixtureRoot $releaseFunctions
        Assert-Equal ([Globalization.CultureInfo]::CurrentCulture.Name) 'fr-FR' 'Numeric culture changed'
    }

    # Environment selection, explicit precedence, regional tags, and invalid flags.
    $env:DISC_IMAGE_STUDIO_LANGUAGE = 'ja'
    Initialize-ToolLanguage
    Assert-Failure { & $metadataScript -Tag invalid } (Get-ToolText 'Packaging.TagFormat')
    Initialize-ToolLanguage -Language tr -Explicit $true
    Assert-Failure { & $metadataScript -Language tr -Tag invalid } (Get-ToolText 'Packaging.TagFormat')
    foreach ($pair in @(@('zh-Hant-HK', 'zh-TW'), @('ja-JP', 'ja'), @('tr-TR', 'tr'), @('en-US', 'en'))) {
        Initialize-ToolLanguage -Language $pair[1] -Explicit $true
        Assert-Failure { & $metadataScript -Language $pair[0] -Tag invalid } (Get-ToolText 'Packaging.TagFormat')
    }
    Initialize-ToolLanguage -Language ja -Explicit $true
    Assert-Failure { & $metadataScript -Language es } (Get-ToolText 'Cli.UnsupportedLanguage' @('es'))
    Assert-Failure { & $metadataScript -Language '' } (Get-ToolText 'Cli.UnsupportedLanguage' @(''))
    [Globalization.CultureInfo]::CurrentUICulture = [Globalization.CultureInfo]::GetCultureInfo('zh-HK')
    Initialize-ToolLanguage -Language system -Explicit $true
    Assert-Equal $script:DiscToolLanguage 'zh-TW' 'System Traditional Chinese resolution'
    Assert-Failure { & $metadataScript -Language system -Tag invalid } (Get-ToolText 'Packaging.TagFormat')
    Write-Host "packaging localization: $count preflight checks passed; $($references.Count) keys in five languages; metadata, Unicode paths, numeric culture and source diagnostics verified"
}
finally {
    $env:DISC_IMAGE_STUDIO_LANGUAGE = $originalLanguage
    [Globalization.CultureInfo]::CurrentCulture = $numericCulture
    [Globalization.CultureInfo]::CurrentUICulture = $uiCulture
    if (Test-Path -LiteralPath $fixtureRoot) { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force }
}
