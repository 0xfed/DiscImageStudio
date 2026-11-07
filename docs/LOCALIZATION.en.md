# Internationalization

[简体中文](LOCALIZATION.md) | [繁體中文](LOCALIZATION.zh-TW.md) | [English](LOCALIZATION.en.md) | [日本語](LOCALIZATION.ja.md) | [Türkçe](LOCALIZATION.tr.md)

Click the persistent “gear + Settings” button at the top right, then choose “Follow system”, “简体中文”, “繁體中文”, “English”, “日本語”, or “Türkçe” under “General → Language”. Saving applies the change immediately. Settings is available on the home page and in every workspace; Ctrl+, also opens it. Cancel, Escape, or closing the settings window does not save the language. Switching languages does not restart the app, close the project, or interrupt a task.

The `language` property in `settings.json` accepts `system`, `zh-CN`, `zh-TW`, `en`, `ja`, or `tr`. Language and theme both default to following the system; missing, empty, or unsupported language values also fall back to the system. Valid language and theme choices in existing settings are retained. Following the system uses the system UI language captured at startup: `zh-TW`, `zh-HK`, `zh-MO`, `zh-Hant`, and explicit `zh-Hant-*` select Traditional Chinese; `zh-CN`, `zh-SG`, `zh-Hans`, and other Chinese systems select Simplified Chinese; Japanese systems such as `ja` and `ja-JP` select Japanese; Turkish systems such as `tr` and `tr-TR` select Turkish; English, French, and all other systems select English. Restart the app to pick up system language changes made while it is running. The theme uses the Windows app colour mode to choose dark or light. Language is independent of `.discproj`: project names, user preset names, layer names, file paths, and numeric parsing rules are not translated.

## Adding and maintaining text

Translations live in `src/DiscImageStudio.Localization/Catalogs/*.json` and are embedded in the assembly at build time, with no extra runtime dependency. Each key contains `zh-CN`, `zh-TW`, `en`, `ja`, and `tr` text. Traditional Chinese has separate resources using terms such as “設定”, “專案”, “檔案”, and “燒錄”; user data is not converted at runtime:

```json
{
  "示例 {0}": { "zh-CN": "示例 {0}", "zh-TW": "範例 {0}", "en": "Example {0}", "ja": "例 {0}", "tr": "Örnek {0}" }
}
```

Use `L.T("源文案")` in C# and `L.F($"示例 {value}")` for interpolation; the resource key is the corresponding .NET composite format `示例 {0}`. Use `L.Format(key, arguments)` for an explicit resource key. Translations may reorder arguments but must preserve their indices and format specifiers. XAML uses `xmlns:loc="clr-namespace:DiscImageStudio.Localization"` and `{loc:Text ResourceKey}`. `TextExtension` writes text in the current language on load and rewrites loaded controls and templates in place when the language changes.

Define shared phrases only once; keys must be unique across all resource files. A missing current-language translation falls back to Chinese; a missing key displays the key itself. Initialize the language before creating windows and ViewModels. Set only the UI culture and preserve numeric culture. Do not translate protocol identifiers, commands, paths, or user data, and do not use translated text for logic decisions.

The goal is 1:1 semantic parity across the five supported languages for repository-owned UI, command-line help, diagnostics, and documentation. The shared runtime and resources are in `src/DiscImageStudio.Localization`; desktop and CLI use the same resources. CLI help, module and option descriptions, CD module messages, DVD module status, and command routing errors are connected. Project-owned exceptions in Blu-ray, image loading, Core, CD, and ISO/UDF, plus Blu-ray progress summaries, explanatory JSON, and photo rectification guidance are also translated while preserving machine identifiers, geometry values, and binary output. For coverage and completed/pending verification, see [LOCALIZATION_AUDIT.md](LOCALIZATION_AUDIT.md). Third-party and operating-system exception details and original official licence texts remain unchanged.

## Command-line language

The command line supports the global option `--language zh-CN|zh-TW|en|ja|tr|system` before or after the command, as well as `--language=ja`. An explicit option overrides the `DISC_IMAGE_STUDIO_LANGUAGE` environment variable; otherwise the system UI language is used. Unsupported explicit languages, duplicate options, and missing values return exit code 1. The standalone DVD program, cross-platform CLI, and Windows app console entry point share this option. Launching the GUI without a command still uses the saved UI settings.

```sh
disc-image-studio --language ja --help
disc-image-studio cd-generate --language tr --input picture.png --output track.wav --sectors 2
```

Translate display text only; do not translate command names, option names, protocol identifiers, file paths, user input, or JSON field names. Project parameters retain their existing parsing rules; language selection does not change numeric culture. The CLI disables invariant globalization to support named UI cultures.

Calibration curve-fitting guidance is translated when displayed in the UI. Core return values retain their source text so automatic search decisions do not depend on translations. When saving presets, generated geometry descriptions retain portable original text and a format resource key; display uses the current language and numeric culture, while manually edited descriptions remain unchanged.

## Validation

The integrity checks that do not depend on WPF run on Windows, Linux, and macOS, and CI runs them on each platform:

```sh
dotnet run --project tests/DiscImageStudio.LocalizationTests -c Release
```

The check verifies nonempty text in five languages, duplicate keys, composite and numeric/date formats, file-dialog filters, C# literal and interpolated calls, XAML text and template triggers, built-in presets, dynamic calibration guidance, and user preset fallbacks. It also checks that cached module descriptions follow language switches, exception types and parameter names are preserved, and project, ISO9660/Joliet, and UDF output is consistent across languages. It cannot prove complete semantic accuracy or replace runtime switching and layout screenshot checks on Windows.

Check document paragraph structure, lists, command examples, links, and source versions separately:

```sh
python3 tests/test_documentation_translations.py
python3 tests/test_documentation_translations.py --require-complete
```

The document check also preserves paragraphs, table rows and columns, and numeric values, allowing localized decimal and grouping separators. The first command validates only the documents recorded as complete in the manifest and reports pending items; the second checks documentation completeness across the repository and fails if anything remains. See [LOCALIZATION_AUDIT.md](LOCALIZATION_AUDIT.md) for current results and remaining work.

CI requires the full repository completeness check.

The manifest may explicitly permit translating `displayName` and `description` in preset JSON examples; field names, types, numeric precision, IDs, and contributors must still match, and duplicate fields or invalid numbers are rejected.

Generated test reports are excluded from the source-document inventory. The manifest can explicitly register reproducible result links with `generated_artifacts`; only paths already referenced by the source beneath the ignored artifacts directory are allowed. Other local links must still exist.

The manifest may list Mermaid nodes with translatable display text in `translated_mermaid_labels`; node IDs, edges, direction, other labels, and digits within labels must match, and missing or blank labels are rejected.

Publishing-source changelogs retain parallel Chinese and English subsections. The manifest can explicitly declare their two headings with `parallel_language_sections`, allowing each reading copy to show the same changes once. Each parallel version must be a complete bullet list with matching item counts and inline technical identifiers; missing subsections or extra unlisted content are rejected. Historical subheadings and all other prose remain complete.

Burning-layer exceptions, progress, operation descriptions, and device diagnostics also use the shared resources. `OpticalBurnOperation.IsFinalizing` explicitly carries finalization state; never decide whether to finalize by comparing translated text. The following regression needs no WPF and uses the actual burning library with fake devices to check CD/BD bytes, MMC commands and timeouts, progress stages, cancellation tokens, and cleanup behavior in five languages. It neither accesses real recorders nor performs physical burning.

```sh
dotnet run --project tests/DiscImageStudio.Burning.LocalizationTests -c Release
```

PowerShell packaging and installation scripts read shared resources through `packaging/Localization.ps1`, supporting native `-Language` and the environment preference. They pass the selected language to child checks and the elevated installer. Metadata, release-note text, identities, paths, checksums, and numeric culture stay unchanged. These tests exercise only metadata and preflight failure paths with a simulated child process; they do not build, sign, or install packages or request elevation. For Windows PowerShell, replace pwsh with powershell.

```powershell
pwsh -NoProfile -File tests/Test-ReleaseTooling.ps1
pwsh -NoProfile -File tests/Test-PackagingLocalization.ps1
```

C# resources load lazily after language selection. Empty-catalog and duplicate-key diagnostics read a separate embedded error catalog directly, avoiding recursive translation during main catalog loading. Shared-runtime regressions check five-language diagnostics, original resource names, exception types, stream disposal, and numeric culture. The CLI error prefix also reads this independent resource, so entry-point error handlers do not retry failed main-catalog loading. Python error wrappers use the same approach.

Python tools read the same JSON resources through `tools/localization.py`, sharing global language flags and the environment preference. Parser help/errors, analysis explanations, and chart annotations support five languages without changing numeric locale, fields, paths, or algorithms. Standard-library checks audit every Python tool and argparse format arguments. Integration checks require NumPy and matplotlib; they use reference searches with maximum period 8, synthetic figures, and a deliberately invalid atlas to compare original-script numerical/schema baselines, input files, fonts, and figure text bounds, and verify that generated C# table code is language-independent. These fixtures are not physical-disc measurements or a complete probe sweep.

```sh
python3 -m unittest discover -s tests -p test_python_localization.py
python3 tests/test_python_tool_localization.py
```

`LocalizationTests` checks resource language completeness, format arguments, system-language fallback, saving and cancelling settings, and that failed saves do not apply a language. `RelocalizationTests` checks that project geometry, ongoing reads, preview results, and text on open pages remain unchanged or refresh correctly after runtime language switching.

```powershell
dotnet run --project src/DiscImageStudio.App -c Release -- ui-snapshot --settings-dialog --language en --output artifacts/settings-en.png
dotnet run --project src/DiscImageStudio.App -c Release -- ui-snapshot --settings-dialog --language zh-CN --output artifacts/settings-zh.png
dotnet run --project src/DiscImageStudio.App -c Release -- ui-snapshot --settings-dialog --language zh-TW --output artifacts/settings-tw.png
dotnet run --project src/DiscImageStudio.App -c Release -- ui-snapshot --settings-dialog --language ja --output artifacts/settings-ja.png
dotnet run --project src/DiscImageStudio.App -c Release -- ui-snapshot --settings-dialog --language tr --output artifacts/settings-tr.png
```

Screenshots use a disposable settings file without changing user configuration. When adding a language, extend normalization and resolution rules, the settings dropdown, all resources, and tests together.

A separate Windows CI process checks 325 layout scenarios in five languages: CD/DVD/BD workspaces at 1366×900 with the dark theme and 960×700 with the light theme, expanded preview parameters and readback, calibration panels, settings categories, and new/edit project dialogs. It checks loaded page titles after language changes, text width/height and missing glyphs, while allowing intentional ellipsis. Temporary stores and a recorder that cannot write isolate the fixtures; saved project bytes, geometry, and numeric culture are checked. The report records actual DPI rather than emulating another monitor.

```powershell
$env:DISC_IMAGE_STUDIO_UI_SNAPSHOTS = "artifacts/ci/localization-layout"
dotnet run --project tests/DiscImageStudio.ArchitectureTests -c Release -- --localization-layout
```

The runner compiles in this Linux workspace; its Windows execution remains pending the next repository CI run after a push. CI uploads PNGs and the JSON report even when a check fails. Empty projects are synthetic fixtures; the matrix does not validate physical optical output or every populated workflow state.

New upstream device diagnostics may be translated incrementally and fall back to English when the selected language is missing. Completed interface catalogs still require all five languages. Device field names, numeric formats and original operating-system errors remain unchanged.
