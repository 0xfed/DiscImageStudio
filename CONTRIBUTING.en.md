# Contributing to Disc Image Studio

[简体中文](CONTRIBUTING.md) | [繁體中文](CONTRIBUTING.zh-TW.md) | [English](CONTRIBUTING.en.md) | [日本語](CONTRIBUTING.ja.md) | [Türkçe](CONTRIBUTING.tr.md)

Welcome to Disc Image Studio. Please read this document first; it covers issue reporting, development setup, module boundaries, validation requirements, and the contribution process.

## Reporting issues

Submit ordinary issues through GitHub Issues and include:

- The application version and how you obtained it (source build / MSIX package), plus the Windows version.
- Reliable reproduction steps, expected behavior, and actual behavior.
- Relevant run logs or screenshots.

For burning issues, also include the recorder model, firmware version, and disc brand/model. Visible results and measured calibration depend strongly on this hardware; issues without these details usually cannot be diagnosed.

**Do not** disclose exploitable security issues in public issues. Follow the [security policy](SECURITY.en.md) and use GitHub's private reporting feature.

Feature requests also go through Issues. Describe the problem to solve, the desired behavior, and whether a workable alternative already exists.

## Development environment

- Windows 10 2004+ or Windows 11.
- .NET 10 SDK. The repository pins `10.0.403` through `global.json`; after installation, `dotnet` selects a matching SDK automatically.
- Tests have no third-party framework dependencies; all are console regression programs included in the repository.

```powershell
git clone <your-fork-url>
cd DiscImageStudio
dotnet build DiscImageStudio.slnx --configuration Release
dotnet run --project src/DiscImageStudio.App --configuration Release
```

## Dependency and build-input review

Builds use SDK `10.0.403` and runtime `10.0.14`, with locked restores enabled by default. Reviewed NuGet graphs live under `eng/dependencies/{portable|windows}/{optionalRID}/{ProjectName}.lock.json`. After changing `VersionPrefix`, project references, package versions or the SDK/runtime, regenerate and review the locks in the same change.

After setting up CPython `3.14.10` from `.python-version`, CI runs `python packaging/Install-DotNetSdk.py` to download a fixed official SDK archive, compare its SHA512 with the reviewed value in `eng/dependencies/dotnet-sdk.json` before extraction, and install into a fresh directory. Review all five platform URLs and hashes together with `global.json` and dependency locks when updating the SDK; an existing directory reporting the same version is insufficient for reuse.

Use Python `3.14.10` to refresh the complete wheel-only Python lock for all five CI platforms, and the NuGet updater to refresh every supported target and RID graph. Run from the repository root:

```powershell
python packaging/Update-NuGetLocks.py
python packaging/Update-NuGetLocks.py --check
python packaging/Update-PythonLock.py
python packaging/Update-PythonLock.py --check
```

`NuGet.Config` permits only the official NuGet feed and approved repository signing certificates. Review package-source or certificate rotations explicitly, and retain certificates needed to verify existing locked packages. CI checks the committed locks and must not rewrite them automatically.

## Module boundaries

Read the [architecture guide](docs/ARCHITECTURE.en.md) before changing code. The central design choice is that the shell does not depend on any particular disc encoding; media-specific logic stays in its corresponding module. Reviews focus on these rules:

1. Do not put physical modulation, error correction, sector organization, or filesystem details for CD/DVD/future Blu-ray into `DiscImageStudio.Core` or the shared layer.
2. Modules must not read state from GUI controls; all inputs come from `DiscJobRequest`. Modules must not display windows directly.
3. When an underlying algorithm cannot be cancelled safely, `SupportsCancellation` must be `false`.
4. Module IDs and command names must be globally unique. The catalog rejects conflicts at startup.
5. New commands must be registered in `DiscCommandDescriptor`, with catalog routing tests and a minimal output smoke test.

## Coding conventions

- Follow the naming, structure, and comment density of the surrounding code. Keep builds free of warnings and errors.
- Do not remove or downgrade functionality to make a build pass. Do not add compatibility shims or duplicate implementations on your own initiative.
- Before removing functionality or code that appears intentional, explain the purpose in an Issue or PR description.
- Do not commit build artifacts, binary assets, or any private keys / PFX files.

## Testing and validation

All of the following must pass before a Pull Request:

```powershell
dotnet build DiscImageStudio.slnx --configuration Release
dotnet run --project src/DvdImageSolver --configuration Release -- selftest
dotnet run --project tests/DiscImageStudio.LocalizationTests --configuration Release
dotnet run --project tests/DiscImageStudio.ImageIOTests --configuration Release
dotnet run --project tests/DiscImageStudio.ArchitectureTests --configuration Release
```

Add validation appropriate to your change and report the results in the description:

- For image layout, streaming burns, or disc preset parsing, check the corresponding regression items in [VALIDATION.md](docs/VALIDATION.en.md).
- For GUI changes, inspect offscreen-rendered screenshots of key pages and update `docs/images/` when needed.
- After changes to encoding chains such as delay interleaving, EFMPlus, or ECC, explain why if the coverage of `selftest` is unchanged.

## Commits and Pull Requests

1. Fork a feature branch (or create one from `main`). Keep each branch focused on one issue so the diff remains reviewable.
2. Use English for commit subjects and bodies. Keep subjects short; add behavior changes, validation methods, and large-file implications to the body as needed. Each release's notes include Chinese and English sections.
3. Explain the motivation, scope of change, and validation results in the PR description. For user-visible changes, also update the Unreleased section of `CHANGELOG.md` and the relevant `docs/` files.
4. After submitting, confirm that the GitHub Actions `build` workflow is green.

For large changes (new modules, new UI pages, or changes to burning-layer contracts), it is recommended to open an Issue or Draft PR to discuss the approach before implementation.

## Versions and releases

Use `main` with short-lived feature branches, and semantic versioning that increments according to fixes, features, and incompatible changes. Record ordinary contributions in `CHANGELOG.md` under `Unreleased`. In the same release-preparation PR, maintainers update the version configuration in `Directory.Build.props` and the corresponding version entry. After merging into `main`, GitHub Actions generates the version tag and a draft Release automatically. Ordinary code pushes do not generate Releases. See the [release guide](docs/RELEASING.en.md) for complete trigger conditions, prerelease suffixes, draft review, and retry rules.

## License

The project uses [GNU GPL v3.0 or later](LICENSE) (`GPL-3.0-or-later`). Submitting a contribution means you have the right to submit that content and agree to license it under these terms. When introducing third-party code, fonts, or images, retain the original authors and licenses, confirm compatibility with the project's distribution model, and update [Third-party notices](THIRD-PARTY-NOTICES.txt). See [LICENSE-NOTICE.md](LICENSE-NOTICE.en.md) for third-party licensing boundaries.
