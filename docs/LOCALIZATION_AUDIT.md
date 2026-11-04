# Localization audit

[简体中文](LOCALIZATION_AUDIT.zh-CN.md) | [繁體中文](LOCALIZATION_AUDIT.zh-TW.md) | [English](LOCALIZATION_AUDIT.md) | [日本語](LOCALIZATION_AUDIT.ja.md) | [Türkçe](LOCALIZATION_AUDIT.tr.md)

Updated 2026-11-04. This audit covers repository-owned UI, CLI, diagnostics, and documentation in Simplified Chinese, Traditional Chinese, English, Japanese, and Turkish. The identified translation gaps have been filled; Windows runtime and visual verification remain pending. Catalog presence and structural checks alone do not prove semantic accuracy.

## Coverage and review

| Area | Evidence |
| --- | --- |
| Shared resources | 2042 entries have nonempty text in all five languages, without duplicate keys or fields; embedded resources match source |
| Source references | 2076 static/dynamic C# and XAML references resolve, including presets, stages, preview titles, calibration guidance, and dialog filters |
| UI semantics | All 749 legacy UI entries reviewed in five languages: Common 154, Workflows 239, Workspace 151, Xaml 205 |
| CLI and diagnostics | Desktop/portable CLI, standalone DVD engine, Core, CD/DVD/BD, ImageIO, Imaging, ISO/UDF, and burning use shared resources for project-owned help, errors, progress, and results |
| Tools | Three C# tools, six Python tools, and four PowerShell packaging/install scripts support five-language text and language selection |
| Documentation | All 23 source documents have five-language coverage, including README, changelog history, technical guides, policies, and this audit |

The semantic review restored action verbs, qualifiers, recorder-adopted speed settings, reflected brightness, inclusive 60% coverage, calibration-record terminology, selected-point deletion, output-folder scope, and zooming in both directions. Traditional Chinese interleaving terms and language endonyms are consistent. Long labels, titles, and calibration buttons wrap and grow while retaining compact minimum button height; actual Windows layout still needs inspection.

A separate inventory traced unwrapped CJK/English phrase candidates and XAML display attributes to their producers. Core fitter warnings translate at display time while raw classification text stays unchanged. Stored user defaults, fixture names, protocol fields, and device details were distinguished from explanatory prose. The pass found and fixed seven DVD streaming errors and blank disabled-preset contexts. Runtime regressions cover those failures in every language; source guards cover DVD adapter exception literals and desktop/tool XAML display attributes.

Reviewed UI catalog fingerprints use UTF-8 with normalized line endings. They identify the reviewed versions, not automated proof of meaning.

| Catalog | SHA-256 |
| --- | --- |
| `Common.json` | `7c304b1575bf7d3494c0f73032f45524e4439ce43f8bef24225041fabac971bf` |
| `Workflows.json` | `2a27cd6337ff55c4841271ad851da0b7c4bb6abede365d710714a210d9f32112` |
| `Workspace.json` | `b389bbf7a35a094f1b405e6e475b7af953a0ff045d32bfb096d465fc7063ba07` |
| `Xaml.json` | `f3728ada2da11e6be5614e2733e94e9b2e8ae0d448eedd7b6bb5bfae2250ed6f` |

## Preserved behavior

Global language options and the environment preference are shared across command-line entry points. Regional/system fallback, invalid/missing/duplicate options, UTF-8 output, and live UI selection are covered. UI culture changes without changing numeric culture or parameter parsing. Cached module descriptions follow language changes. Startup error resources load independently so failed catalog loading and CLI error wrapping cannot recurse.

Commands, option names, schema keys, technical IDs, choices, default filenames, contributors, user-authored names/descriptions, paths, and saved geometry remain unchanged. Original operating-system/third-party details and official license texts are retained. Initial user preset defaults translate only at display time when family, ID, and unchanged text match. Spanish has 24 dormant partial entries and is outside the five supported languages.

Encoding decisions and binary outputs do not depend on translated text. Burning carries an explicit finalization flag. Tests preserve project data, CD audio/data, DVD EFMPlus/ISO, BD ISO/probe, ISO9660/Joliet/UDF bytes, numerical analysis, and generated C# tables across languages. No physical-disc result is inferred from these tests.

## Documentation gate

The manifest records canonical source hashes and language destinations. Checks preserve headings, paragraphs, list items, table dimensions and numbers, inline code, commands/formulas, reference targets, and local links. Explicit exceptions permit preset example names/descriptions and two visible Mermaid labels while preserving JSON types/precision/IDs/contributors and diagram nodes/edges/direction/digits. Missing or blank declared labels, duplicate JSON fields, and invalid numbers fail.

Bilingual publishing-source changelog sections are retained. Explicitly declared parallel bullet lists must match item counts and technical tokens; reading copies show each change once. All 117 distinct entries, historical dates, and original validation limits remain. Generated reports are excluded from source inventory; only four explicitly declared, source-referenced outputs beneath ignored artifacts may be absent on a clean checkout. Other local links must exist.

The full documentation gate reports 23/23 and is required in CI. Seven rule regressions protect these constraints. Structural checks complement language review; source changes require renewed review. See [LOCALIZATION.md](LOCALIZATION.en.md) for maintenance and Windows screenshot commands.

## Verification and limits

The Release solution builds with Windows targeting and zero warnings/errors. Shared localization regressions pass, including actual five-language DVD streaming failures, exception types/parameter names, saved data, catalog failure disposal, and numeric-culture preservation. Portable CLI package smoke, DVD selftests, and C# Blu-ray probes pass; the probe matches 256 independent byte vectors and finite patterns. Fake-recorder burning checks preserve bytes, MMC commands/timeouts, progress, cancellation, and cleanup without accessing hardware.

PowerShell passes 45 existing release-tooling and 69 safe localization preflight cases, covering 47 keys, unchanged metadata, Unicode paths, and child-language forwarding. No package is built, signed, installed, or elevated by those checks. Python checks cover 123 keys; all six tools pass integration against original numerical/schema baselines. Reference searches run through period 8, and all three plot types in five languages pass glyph/text-bound checks. Five representative plots were inspected. Fixtures are synthetic, including an intentionally invalid atlas, not physical measurements or a full production probe sweep.

Windows WPF language switching, glyph/layout screenshots, dithering UI/PNG integration, DVD atlas/preview integration, and Windows PowerShell/package installation paths are compiled or configured where applicable but not executed in this Linux workspace. The tests do not establish real recorder performance or optical brightness. These are verification limits, not claims of additional hardware validation.

## Reproduce checks

```sh
dotnet run --project tests/DiscImageStudio.LocalizationTests -c Release
dotnet run --project tests/DiscImageStudio.Burning.LocalizationTests -c Release
python3 tests/test_cli_package.py /absolute/path/to/disc-image-studio
python3 tests/test_probe_localization.py /absolute/path/to/BluRayByteProbe.dll
python3 -m unittest discover -s tests -p test_python_localization.py
python3 tests/test_python_tool_localization.py
python3 -m unittest discover -s tests -p test_documentation_translation_rules.py
python3 tests/test_documentation_translations.py --require-complete
pwsh -NoProfile -File tests/Test-ReleaseTooling.ps1
pwsh -NoProfile -File tests/Test-PackagingLocalization.ps1
```

On Windows, also run the runtime suite and inspect the language screenshots:

```powershell
dotnet run --project tests/DiscImageStudio.ArchitectureTests -c Release
```

A separate Windows CI process checks 325 layout scenarios in five languages: CD/DVD/BD workspaces at 1366×900 with the dark theme and 960×700 with the light theme, expanded preview parameters and readback, calibration panels, settings categories, and new/edit project dialogs. It checks loaded page titles after language changes, text width/height and missing glyphs, while allowing intentional ellipsis. Temporary stores and a recorder that cannot write isolate the fixtures; saved project bytes, geometry, and numeric culture are checked. The report records actual DPI rather than emulating another monitor.

```powershell
$env:DISC_IMAGE_STUDIO_UI_SNAPSHOTS = "artifacts/ci/localization-layout"
dotnet run --project tests/DiscImageStudio.ArchitectureTests -c Release -- --localization-layout
```

The runner compiles in this Linux workspace; its Windows execution remains pending the next repository CI run after a push. CI uploads PNGs and the JSON report even when a check fails. Empty projects are synthetic fixtures; the matrix does not validate physical optical output or every populated workflow state.
