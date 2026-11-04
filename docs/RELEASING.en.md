# Release guide

[简体中文](RELEASING.md) | [繁體中文](RELEASING.zh-TW.md) | [English](RELEASING.en.md) | [日本語](RELEASING.ja.md) | [Türkçe](RELEASING.tr.md)

The four PowerShell packaging and installation scripts support `-Language zh-CN|zh-TW|en|ja|tr|system`. Without an explicit option they use `DISC_IMAGE_STUDIO_LANGUAGE` or the system UI language; the explicit option takes precedence. Status and project-owned diagnostics use shared translations, while versions, identities, paths, release-note text, and checksums remain unchanged. Details returned by third-party tools and Windows remain in their original language. The selected language is passed to child checks and the elevated installer.

The following checks validate only metadata and preflight failure paths. They do not build, sign, or install packages, or request elevation. For Windows PowerShell, replace pwsh with powershell in these commands:

```powershell
pwsh -NoProfile -File tests/Test-ReleaseTooling.ps1
pwsh -NoProfile -File tests/Test-PackagingLocalization.ps1
```

## Branch and version policy

Daily development uses `main` with short-lived feature branches merged through Pull Requests. Patch releases also come from `main`; long-lived release branches are not currently maintained.

Versions are maintained centrally in `Directory.Build.props` at the repository root. Maintainers choose the next version according to the change:

| Change | Version change | Example |
| --- | --- | --- |
| Backward-compatible fixes | patch | `1.0.26` → `1.0.27` |
| Backward-compatible features | minor | `1.0.26` → `1.1.0` |
| Incompatible interface, command, or file-format changes | major | `1.0.26` → `2.0.0` |

For a stable release, put `A.B.C` in `VersionPrefix` and leave `VersionSuffix` empty. For prereleases, put `alpha.N`, `beta.N`, or `rc.N` in `VersionSuffix`, where `N` is a positive integer without leading zeros. For example, `VersionPrefix=1.0.27` and `VersionSuffix=rc.1` produce `1.0.27-rc.1`; do not put the suffix in `VersionPrefix`. Clear `VersionSuffix` when promoting a candidate to stable.

GitHub Actions reads the full configured version and creates the corresponding `vA.B.C`, `vA.B.C-rc.N`, or similar tag. The published EXE's informational version retains the prerelease suffix; file and assembly versions use four numeric components, `A.B.C.0`. Repository numeric components are limited to `0..65534` for .NET assembly compatibility. Microsoft Store submission additionally requires the first component to exceed `0`, with MSIX version `A.B.C.0`.

## Releasing the Windows app and cross-platform CLI through GitHub Actions

### Prepare a release

1. Confirm that the GPL-3.0-or-later license, privacy policy, and support contact satisfy this distribution's requirements. Keep a tag pointing to the build commit and provide the complete corresponding source and build instructions for that version in the same Release, so source is available alongside EXE/MSIX. Verify that GitHub's generated source archive includes all required build files. Update [third-party notices](../THIRD-PARTY-NOTICES.txt) when dependencies or the self-contained .NET runtime change. The project license, third-party notices, and font agreements are embedded and available offline in About. See [LICENSE-NOTICE.md](../LICENSE-NOTICE.en.md) for license boundaries.
2. On a feature branch, change `VersionPrefix` in `Directory.Build.props` (and `VersionSuffix` for prereleases), and move this release's entries from `Unreleased` in `CHANGELOG.md` to the corresponding version section. Keep `## Unreleased` for subsequent development.
3. Version headings must follow `## [1.0.27] - YYYY-MM-DD` or `## [1.0.27-rc.1] - YYYY-MM-DD`, replacing the date placeholder with the actual release date, and sections must be nonempty. Prerelease and stable tags each require an exactly matching section. Starting with this release, new version sections provide both Chinese and English notes as described below.
4. Put the version configuration and release notes in the same release-preparation PR, confirm the `build` workflow passes, and merge into `main`. Automatic release starts only when this push changes both files and actually changes the full configured version.
5. GitHub Actions completes validation, creates the version tag automatically, and generates a Release draft; a maintainer reviews and publishes it manually.

For example, a future patch release sets this in `Directory.Build.props`:

```xml
<VersionPrefix>1.0.27</VersionPrefix>
<VersionSuffix></VersionSuffix>
```

Also add a nonempty `## [1.0.27] - YYYY-MM-DD` section with the actual date to `CHANGELOG.md`. Merging into `main` then creates tag `v1.0.27` automatically. For a release candidate, set `VersionSuffix` to `rc.1` and use `1.0.27-rc.1` as the changelog version.

### Bilingual release notes

Future releases use `### 中文` followed by `### English` within the same dated `## [VERSION] - DATE` section. Both lists describe the same changes, limitations, and validation. English must fully translate the corresponding Chinese, omitting no warnings and adding no unverified capability claims. Keep the top `## Unreleased` after preparing release content for the next development cycle.

For example, a release-preparation structure is shown below. Replace version, date, and entries with the actual release content:

```markdown
## Unreleased

## [1.3.4] - YYYY-MM-DD

### 中文

- 在设置中增加界面语言选择。

### English

- Add interface language selection to Settings.
```

`Get-ReleaseMetadata.ps1` extracts the entire version section, stopping only at the next `## ` heading or end of file, so both `### 中文` and `### English` are included in Release notes. Do not use `## 中文` or `## English` as language headings, as they end the version section prematurely. Current metadata checks verify section uniqueness, a valid date, and nonempty content; release review must still confirm both languages and matching entries.

The workflow appends Chinese and English download/run instructions, SHA-256 verification commands, and corresponding-source and build instructions after the bilingual notes. Both languages must clearly label the Linux and macOS CLI experimental and separately explain the Windows EXE's signing status and the experimental limits of on-the-fly burning.

### Automated checks and drafts

`.github/workflows/release.yml` watches `Directory.Build.props` or `CHANGELOG.md` changes on `main`, but releases only when that push **changes both version configuration and release notes, and changes the full version**. Ordinary code pushes run regular build checks only. Updating just `Unreleased` or configuration without a version change does not generate a Release.

The workflow reads the full configured version, validates its format and matching nonempty, dated Changelog section, confirms the release commit belongs to `origin/main`, and reuses the build and regression checks in `build.yml`. Only after all checks pass does it create the matching tag and draft.

Windows x64 continues to provide a self-contained, single-file EXE. The packaging script runs the final EXE in an isolated directory for selftests, two-sector RAW/WAV generation checks, and a GUI screenshot check.

The experimental offline CLI for Linux and macOS is built on each platform's x64 and Arm64 runner, with the .NET runtime included. Each runner extracts its newly generated tar.gz and runs `--help`, DVD selftests, and CD audio, DVD image, and Blu-ray image generation tests outside the repository. Release gathers the original archives that passed these tests and attaches them to the draft alongside the Windows EXE. For example:

```text
DiscImageStudio-v1.3.6-win-x64.exe
DiscImageStudio-CLI-v1.3.6-linux-x64.tar.gz
DiscImageStudio-CLI-v1.3.6-linux-arm64.tar.gz
DiscImageStudio-CLI-v1.3.6-osx-x64.tar.gz
DiscImageStudio-CLI-v1.3.6-osx-arm64.tar.gz
SHA256SUMS.txt
```

The workflow merges and verifies SHA-256 checksums across platforms, writes them into the Release notes and `SHA256SUMS.txt` attachment, and uses the built-in `GITHUB_TOKEN` to create a **GitHub Release draft**. No personal access token (PAT) is required. Prerelease tags set the prerelease flag automatically; stable tags create stable-release drafts.

A maintainer checks the version, notes, platform attachments, and checksums on GitHub's [Releases page](https://github.com/JiaFeiMiao-K-Cat/DiscImageStudio/releases), downloads and verifies startup, then publishes manually. Calculate the Windows download's SHA-256 with this command and compare it with the Release notes:

```powershell
Get-FileHash .\DiscImageStudio-v1.3.6-win-x64.exe -Algorithm SHA256
```

### Build provenance and SBOMs

The release workflow is configured to generate a CycloneDX SBOM named `<artifact>.cdx.json` for each of the five release binaries/archives and create build-provenance and SBOM attestations through commit-pinned `actions/attest`. Compare the SBOM's package and runtime versions with the committed locks; use `SHA256SUMS.txt` for file integrity and attestations for producer identity.

For a published Release, download the artifact and its SBOM, then verify both files using GitHub CLI. The example trusts this repository's release workflow; forks must replace both repository names with their own owner/repository:

```powershell
gh attestation verify DiscImageStudio-v1.3.6-win-x64.exe --repo JiaFeiMiao-K-Cat/DiscImageStudio --signer-workflow JiaFeiMiao-K-Cat/DiscImageStudio/.github/workflows/release.yml
gh attestation verify DiscImageStudio-v1.3.6-win-x64.exe.cdx.json --repo JiaFeiMiao-K-Cat/DiscImageStudio --signer-workflow JiaFeiMiao-K-Cat/DiscImageStudio/.github/workflows/release.yml
```

A successful local build does not create GitHub attestations; these become available only after the configured GitHub release workflow runs successfully. They do not replace platform code signing: Windows EXEs remain Authenticode-unsigned and macOS packages remain unnotarized until real credentials and signing steps are configured.

### Retry and correction

Rerun a failed Actions run, or manually run the `release` workflow from `main` in GitHub Actions. Manual dispatch has no tag input; it reads current `main` configuration and notes and runs all release checks, so retrying requires no further version-file edit.

When retrying the same version, an existing tag must point to the same commit being built and an existing Release must still be a draft. Never overwrite or move published Releases, tags, or artifacts. Fix issues on `main` and publish a new version; for prerelease iterations, increment the sequence, for example `rc.1` → `rc.2`. If `main` has advanced and the same-version tag points to an older commit, rerun the original failed run or prepare a new version for the new commit. Even note-only edits must preserve facts and correction history; do not replace published binaries.

Repository administrators are advised to protect `main` in GitHub, requiring Pull Requests and the `build` check, and to set a `v*` tag ruleset allowing the release workflow to create tags while prohibiting updates and deletion. Workflow files do not constitute remote protection settings; administrators must configure those separately.

## Local EXE rehearsal

Run the same packaging script as GitHub Actions from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File packaging/Build-Release.ps1
```

The script uses the version in `Directory.Build.props` automatically. With configuration `1.0.26`, for example, output goes to `artifacts/release/v1.0.26/`. It builds and separately verifies the self-contained `win-x64` EXE and produces `DiscImageStudio-v1.0.26-win-x64.exe` and its `SHA256SUMS.txt`. During a formal release, the workflow appends checksums for the four platform CLI archives.

## Microsoft Store MSIX

MSIX is a separate manual distribution channel. Reserve the product name in Partner Center first, then copy these fields from Product identity:

- Package/Identity/Name
- Package/Identity/Publisher
- Package/Properties/PublisherDisplayName

Run locally:

```powershell
powershell -ExecutionPolicy Bypass -File packaging/Build-MSIX.ps1 `
  -IdentityName "Partner Center Identity Name" `
  -Publisher "CN=Partner Center Publisher" `
  -PublisherDisplayName "Publisher display name" `
  -DisplayName "Reserved product name"
```

Without `-Version`, the stable version in `Directory.Build.props` is converted to `A.B.C.0`. To specify a Store version explicitly, use `-Version "1.0.26.0"`; this override also sets the packaged EXE's version so it matches the manifest. The Microsoft Store channel does not accept prerelease suffixes such as `-rc.1`; use a stable configuration for Store packages.

Alternatively, manually run the `package-msix` workflow in GitHub Actions with the same values; an empty `version` also uses repository configuration. It first reuses CI in `build.yml`, then generates MSIX as an Actions artifact. It neither submits to the Store nor attaches to GitHub Release automatically. Store MSIX may remain unsigned; local sideload tests require signing with a certificate matching Publisher.

## Locally signed test package

Create a code-signing certificate for local testing whose subject exactly matches the test Publisher. Put the public certificate here:

```text
artifacts/certificates/DiscImageStudio-Test.cer
```

Run `Build-MSIX.ps1 -CertificateThumbprint <thumbprint>` with the identity parameters above to generate a signed package. Then double-click `packaging/Install-TestPackage.cmd`, or run:

```powershell
powershell -ExecutionPolicy Bypass -File packaging/Install-TestPackage.ps1
```

The installer requests administrator privileges once, imports the test certificate's public key into Local Machine / Trusted People, and installs the signed MSIX. Do not commit private keys or PFX files to GitHub or attach a local self-signed test certificate to a formal Release.

## Windows 11 unsigned quick test (optional)

An unsigned test package must use a separate OID publisher namespace and must not share the official Store identity. Generate a local test package:

```powershell
powershell -ExecutionPolicy Bypass -File packaging/Build-MSIX.ps1 `
  -IdentityName "12345.DiscImageStudioArchitectureTest" `
  -Publisher "CN=Disc Image Studio Test" `
  -PublisherDisplayName "Disc Image Studio Test" `
  -DisplayName "Disc Image Studio Test" `
  -Version "1.0.26.0" `
  -UnsignedDevelopmentPackage
```

Then install from administrator PowerShell:

```powershell
Add-AppxPackage -Path ".\artifacts\msix\DiscImageStudio_1.0.26.0_x64_unsigned-dev.msix" -AllowUnsigned
```

`-UnsignedDevelopmentPackage` is only for quick tests on Windows 11 systems supporting it. Some Windows versions may reject executable packages even with `-AllowUnsigned`; use the signed test method above in that case. Do not use this option for distribution or Microsoft Store submission; formal packages must use the real Partner Center identity.

Before formal submission:

- Repackage with the real identity; do not submit a test Identity package.
- Test installation, removal, and offline operation on a clean Windows device.
- Run Windows App Certification Kit on the final package.
- Provide at least one real desktop screenshot at 1366×768 or higher.
- Publish `PRIVACY.md` to a public HTTPS page and provide real support contact details.
- State in certification notes that the app generates large files, needs no login or network, and includes experimental direct CD/DVD burning through Windows IMAPI2. Accurately list applicable devices, blank-disc requirements, and current validation scope.
