# Changelog

[简体中文](CHANGELOG.md) | [繁體中文](CHANGELOG.zh-TW.md) | [English](CHANGELOG.en.md) | [日本語](CHANGELOG.ja.md) | [Türkçe](CHANGELOG.tr.md)

## Unreleased

## [1.6.0] - 2026-11-04

- Add an optional wide-sector calibration target for faint or tightly wound cross lines. Four alternating filled sectors provide continuous radial edges without guide-ring ambiguity; the original fine cross remains the default. Share the saved pattern across preview, PNG export, CD/DVD/BD generation and streaming, show the active target in all five interface languages, and record the pattern in schema 7 while retaining schema 6 fine-cross records. Validate digital contrast under ±0.1 mm inner-radius and ±512-sector errors, coarse-edge fitting, exported pixels and saved burn snapshots. Physical-disc visibility remains unverified; no recording power, preamble or overwrite behavior changes.


## [1.5.0] - 2026-11-04

- Extend UDF 2.50 VAT images and open-disc file updates to CD and DVD data projects, including 4.7 GB M-DISC DVD-R/+R. Add visible Design format selection, a shared Burn reserve/update panel, saved project and sidecar settings, and CLI `--file-system iso9660|udf250`. Use native CD/DVD sessions with DVD+R compatibility finalization disabled, session-relative volume descriptors, CD postgap handling, locked media/capacity checks, cancellation and paired cleanup. Verify and reuse unchanged file extents; append replacements, metadata and linked VATs while preserving prior sectors and drawing. Retain the BD-R path and ISO9660/Joliet defaults. Add independent cross-media traversal, repeated-update and fake-recorder tests. Random-overwrite media, finalized discs and foreign volumes remain unsupported; physical burning requires hardware validation.

## [1.4.0] - 2026-11-04

- Generate UDF 2.50 VAT volumes and support reserving space on the first BD-R burn for later file additions, replacements, and removals. File updates append a new tree and linked VAT under the drive lock, preserve the original drawing, and leave the disc open. Old bytes remain recorded and all selected files are rewritten. Add independent VAT traversal and fake-recorder regressions. Fix SDK bootstrap hash failures by using Microsoft’s direct blob endpoint with unchanged reviewed SHA512 values and explicit partial-download checks.

## [1.3.7] - 2026-11-04

- Fix duplicate-file errors (`NETSDK1152`) when publishing the standalone Windows EXE: exclude duplicate launchers and runtime manifests for the hosted DVD solver while retaining `DvdImageSolver.dll`. CI now builds and smoke-tests the EXE with its bundled runtime.

- Upgrade build provenance to `actions/attest` `4.2.2`, pin CI to CPython `3.14.10` through `.python-version`, and install the SDK into a fresh directory with `packaging/Install-DotNetSdk.py`, verifying downloaded archives against the reviewed SHA512 values in `eng/dependencies/dotnet-sdk.json` before extraction.

- Preserve the selected interface language when generating DVD live previews, including calibration previews, and add regression checks for both the language preference and effective language.

- Replace the obsolete `StbImageWriteSharp` dependency with a PNG writer built on .NET `ZLibStream` and fixed-size buffers, preserving decoded pixels and keeping strict package signature verification enabled on macOS.

- Reject PNG inflation beyond the declared image size and set `RasterCodec` default input limits to `256 MiB` and `64 Mi` pixels; pin and lock reviewed build dependencies, separate artifact inspection from publication write permissions, and configure release SBOMs and build provenance.

- Added an isolated Windows CI layout check covering 325 five-language workspace, preview, calibration, and dialog scenarios, checking text bounds and missing glyphs and saving screenshots/reports even on failure. Fixtures use temporary stores and a recorder that cannot write, checking switched titles and unchanged project data. All 325 scenarios pass on Windows CI without detected text overflow or missing glyphs.


- Localized seven DVD streaming error paths and blank disabled-preset contexts, expanding source guards and five-language regressions. The full build and checks for 2,053 entries and 2,077 references pass. Added the Turkish README, reconciled the other README versions, and completed the five-language DVD engine guide with formulas, commands, and historical validation limits preserved.

- Reviewed all 595 existing Workflow, Workspace, and XAML UI entries in five languages, correcting recorder-adopted speed settings, reflected brightness, coverage boundaries, calibration records, and action instructions. Long labels, titles, and calibration buttons can wrap and grow. Completed the five-language architecture guide; documentation checks allow specified diagram labels to translate while preserving nodes, edges, and numbers.

- Added five-language diagnostics, native language selection, and child-language forwarding to four PowerShell packaging/install scripts while preserving metadata, release-note text, paths, and checksums; preflight regressions build, sign, install, and elevate nothing. Catalog-loading failures and CLI error wrappers translate independently to avoid reloading a corrupt main catalog. Reviewed all common UI resources in five languages, restoring click actions, boundaries, immediate application after saving, path scope, and cache-migration qualifiers, with consistent language endonyms. Completed the five-language release guide.
- Added shared five-language resources to all six Python tools for help, parser errors, progress, analysis explanations, and chart text while preserving numbers, fields, and generated C# tables. Added original-script baselines and synthetic figure regressions for fonts and text bounds. Completed five-language Blu-ray implementation, byte-density analysis, and CD compatibility guides; documentation checks distinguish source prose from generated reports and explicitly register reproducible result links.

- Translated initial user-preset defaults at display time, matching unchanged names and descriptions by media family and ID while preserving user edits and saved data. Completed the editable-preset guide and its JSON example text in five languages, with matching fields, types, contributors, and exact numeric precision.

- Completed five-language CD/DVD/Blu-ray burning errors, progress, operations, cleanup, and device-timing diagnostics. Finalization now uses explicit state instead of Chinese descriptions. Added fake-device regressions without WPF; Linux checks preserve bytes, MMC commands, timeouts, cancellation, and cleanup across five languages, along with original device details and numeric culture.

- Completed Windows extended help, standalone DVD help, DVD engine logs and selftest failure descriptions, and the remaining desktop exception text while preserving commands, parameters, formulas, and defaults. Verified identical EFMPlus, encoding JSON, and ISO bytes across five languages. Added shared language selection and translated explanatory JSON to the DVD grayscale probe, with Windows atlas/preview consistency regressions. Completed five-language physical-model audit documentation, preserving its historical conclusions and numbers.

- Added five-language UI, dialogs, status, and CLI text to the independent dithering tool, using shared language flags and environment preference while preserving mode values, palettes, and default filenames. Translated C# Blu-ray probe progress, errors, and explanatory JSON, and verified its five-language CLI against independent Python 17PP vectors. Added Windows UI/PNG byte-consistency regressions and complete five-language validation documentation.

- Translated module, command, and option metadata plus Core, CD/WAV, ISO/UDF validation and photo rectification guidance in all five languages. Cached descriptions follow language switches while technical parameters and user data remain intact. Added cross-language byte-consistency checks for saved projects and inner/outer ISO9660/Joliet/UDF images. Completed five-language documentation for the independent dithering tool, DVD grayscale probe, and localization maintenance guide.

- Translated Blu-ray and image-loading exceptions, progress, results, and explanatory JSON text in all five languages while preserving technical identifiers and values. Added cross-platform encoding and multilingual byte-consistency checks. Completed five-language calibration, fixed-pattern, Blu-ray probe, and independent 17PP reference documentation, with paragraph, formula, numeric-table, and command parity checks.

- Moved localization into a shared project and added global language flags and an environment preference to the cross-platform CLI and Windows/DVD console entry points. Translated CLI help, routing errors, and CD/DVD module messages in all five supported languages and corrected Turkish dithering terminology. Added complete English, Traditional Chinese, Japanese, and Turkish translations of the privacy policy, security policy, license notice, and contribution guide, with documentation parity checks.

- Completed calibration fit guidance, preview validation, and generated user-preset descriptions in all five UI languages. Preset resource keys now resolve while user edits remain intact. Added cross-platform catalog and source-reference checks to CI.
- Added a built-in Verbatim BD-R (64293) preset with the same parameters as 64099, contributed by bczhc.
- Added a built-in Verbatim DVD+R AZO (43498) preset, contributed by Disappear9.
- Added a built-in Ritek DVD-R E-Series preset, contributed by bczhc.
- Added a built-in Ritek BD-R 12x preset with inner radius 23.98795 mm and outer radius 58.02236 mm, contributed by DeepAQ.
- Added a built-in Ritek DVD+R E-Series preset.
- New projects leave the project name empty instead of filling it from the selected template; the project can be created once a name is entered.
- Added a floppy-disk Save button on the right of the sidebar project header.

## [1.3.6] - 2026-11-02


- **Experimental feature: offline CLI for Linux and macOS.** Generate CD audio tracks (WAV+CUE or RAW), CD data images, and DVD or Blu-ray ISOs from an image. Self-contained x64 and ARM64 archives are available for both systems. Extract the archive and run `./disc-image-studio --help` to list commands.
- The Windows desktop app continues to decode images with WIC. The cross-platform CLI uses StbImageSharp to read images and StbImageWriteSharp to write PNGs, with the same disc encoding algorithms.
- Added a Japanese interface. Choose 日本語 under Settings → General → Language; Japanese system locales (`ja`, `ja-JP`) now use Japanese when following the system. Every translation entry gains `ja` text, and `ui-snapshot --language` accepts `ja`.
- Added a Japanese README (`README.ja.md`) and a 日本語 link to the language switcher at the top of every README.

## [1.3.4] - 2026-11-02


- Added Simplified Chinese, Traditional Chinese, and English interfaces. Choose a language or follow the system under Settings → General → Language; save and restart to apply it, or cancel to leave preferences unchanged. System locales for Taiwan, Hong Kong, Macao, or `zh-Hant` use Traditional Chinese; other Chinese locales use Simplified Chinese, and all others use English. Navigation, workspaces, dialogs, and application status messages share embedded translation resources. Project data and numeric formatting are unchanged; diagnostics returned by lower-level modules and the operating system retain their original language.
- Language and theme now follow the system by default. Missing or invalid preferences fall back to the system, while existing valid saved choices are preserved. Settings are stored in `%LOCALAPPDATA%\DiscImageStudio\settings.json`; corrupt files or files with a newer schema fall back to defaults. Settings are not written to `.discproj` files.
- Added a persistent gear-and-Settings button on the right of the title bar, available on the home page and every workspace, while retaining the Ctrl+, shortcut. Long project names are truncated. The “…” menu provides About; the duplicate Settings menu item and former sidebar About entry were removed.
- Added General and Storage settings categories. Dark, light, and system themes apply immediately to all windows after saving, including the sidebar, segmented controls, disc canvas, and job dock.
- Added default project and cache folder settings, using the system Documents and temporary folders respectively when left empty. View cache usage, clear the app’s cache, or open the application data folder. Cache clearing deletes only allowlisted cached images and sidecars, leaves projects and exported files untouched, and is disabled while a job is running.
- Unified the maximum CD, DVD, and Blu-ray preview resolution at 8192×8192 for design projections, artifact readback, and exported previews. Default preview sizes for each media type remain unchanged.
- Added complete English and Traditional Chinese READMEs, with language links at the top of all three versions, plus documentation for language configuration and translation resource maintenance.
- Extended the developer `ui-snapshot` command with `--language zh-CN|zh-TW|en|system`, `--theme dark|light|system`, `--settings-dialog`, and `--settings-view general|storage` to verify layouts across languages, themes, and settings categories. Settings dialog snapshots default to 760×600 and use a temporary settings file without modifying real preferences.

## [1.3.3] - 2026-10-28

- Added the built-in Verbatim BD-R (64099) preset using user-saved measured radii, sector count, and mapping polarity, available in both GUI and CLI.
- Fixed the Blu-ray calibration streaming-burn source summary incorrectly showing CD; it now displays Blu-ray bit length, 3T/6T polarity, and drawing range from the calibration record.
- Streaming burns keep device-written/total amounts, live rate, and corresponding speed multiplier at the bottom; preparation, transfer, finalization, and diagnostics enter the log. Transfer amounts no longer overwrite actual write progress; finalization or prolonged inactivity clears stale rates.
- BD calibration-range mapping now uses normal image generation's bounded parallel batches and four-point geometry sampling, preserving original calibration callbacks, drawing range, polarity, and ordered output. Custom mappings still require explicit parallel permission.
- CD calibration reuses image generation's four-point geometry sampling. DVD calibration and codeword callbacks reuse the variable-pitch SIMD path while preserving per-point scalar trigonometry, callback order, and audio-track interleaving. Constant-pitch DVD retains its scalar fallback.
- Completed four-point sampling in CD file mode using each byte's individual physical position after swapping and CIRC mapping, preserving file regions, palette indices, scrambling, and disc-end boundary handling.

## [1.3.2] - 2026-10-27

- Project Settings can directly switch to another preset of the current disc type. Confirmation precedes overwriting; Cancel preserves parameters and selection.
- Unified theme blue as dark `#3385FF` and light accent `#74C0FF`. Segmented selections use theme fill and input-matched height; the selector's right expansion arrow is compact.
- Right-click a Custom preset in New Project or Project Settings to delete it; deletion also clears its recent-template entry.
- Design's zoom bar stays at the lower right, adds “−”/“+” buttons, and expands zoom to 25%–1200% (previously zoom-in only). Fit to Window uses a refresh icon. Ctrl/Alt+wheel still zooms; middle-button dragging pans.
- Home template cards show disc type (“CD”, “DVD”, “Blu-ray”) in their gray descriptions.
- Fixed ordinary keys inadvertently triggering preset-switch confirmation, Save as Preset retaining old polarity after a Blu-ray switch, and mismatched disc type/next selection after deleting a recent preset.

## [1.3.1] - 2026-10-26

- Added files-on-disc mode for CD: without a folder, output remains WAV audio; selecting a folder produces an ISO9660/Joliet Mode 1 data disc. Files can occupy the inner or outer ring; project saving, ISO export, and streaming burns share the layout.
- CD design projection and artifact readback with layout information highlight file regions in yellow while preserving full-disc coordinates. Chinese paths and files are supported, with included content unchanged byte for byte.
- CD data streaming burns now share the audio DAO session and capacity-boundary handling, fixing incorrect insufficient-capacity rejection of the Ritek medical aqua-blue preset. ATIP-calibrated 359,845 sectors and existing end limits are retained.
- Added regressions for Mode 1 checksums, DAO data-track identifiers, forward streams, and complete inner/outer ISO recovery. CD data DAO passes software and native memory-image validation; physical burning is still unverified. Live burning remains experimental.

## [1.3.0] - 2026-10-26

- Upgraded all applications, disc modules, and tools to .NET 10. CI, EXE, and MSIX packaging use the .NET 10 SDK; released self-contained EXEs still need no separate runtime.
- Added Files on disc to DVD/Blu-ray Design, with direct folder and inner/outer placement selection shared with Export.
- Added inner/outer file placement to mixed DVD/Blu-ray files-and-patterns mode, defaulting to inner. Project saving, ISO export, and streaming burns share one layout. Outer files retain required filesystem information; drawing automatically avoids file regions.
- DVD/Blu-ray design projection and layout-aware artifact readback show file regions, including the filesystem, in yellow with a legend. Color is preview-only. External images without layout metadata do not infer file extents.

## [1.2.0] - 2026-10-26

### Projects and design

- Reorganized the app into Design, Preview, Export, Burn, and Assisted Calibration workspaces; CD/DVD/BD share the current project and disc parameters.
- Added `.discproj` saving/opening, recent-project thumbnails, unsaved-change checks, and Project Settings, plus saving current disc parameters as user presets.
- Added a multilayer canvas for importing multiple images, moving, scaling, rotating, reordering, and toggling visibility. Export and burning share a high-resolution composite. Removed automatic ring-copying; the design canvas controls layout.
- Added a job dock collecting logs, progress, cancellation, and output locations. Export and Burn consistently show the current project source.

### Tone, Blu-ray, and calibration

- Centralized tone in Design: 0–100% dithering, brightness, sRGB linearization, and CD two/four-level grayscale. New projects default to 75% dithering; older projects without tone preserve binary mapping.
- Added optional tone parameters to CD/DVD/BD production commands. CD/DVD use deterministic logical-byte dithering; BD processes at source resolution. Generation, streaming burning, and design projection share settings.
- Added BD ISO JSON sidecars recording geometry, drawing range, polarity, tone, and UDF timestamps; opening an ISO in Preview can restore these parameters.
- Improved assisted fitting with automatic search expansion, continuing wider for Blu-ray when residuals remain large. Added maximum deviations, outlier markers, and error thresholds to avoid applying excessive-error results.

### Compatibility and migration

- Projects and user presets now uniformly support CD/DVD/BD while retaining project schema 1, preset schema 2/3, and existing CD-track reading. Production commands retain existing arguments; all new tone arguments are optional.
- Developer `ui-snapshot` page numbers are now 0 Home, 1 Design, 2 Preview, 3 Export, 4 Burn, 5 Calibration. About uses `--about-dialog`; `--live-processing` is removed.
- Blu-ray and direct burning remain experimental. Hardware coverage is recorded in `docs/VALIDATION.md`; automated tests are not additional physical-disc validation.

## [1.1.0] - 2026-10-25

### Added

- Experimental single-layer 25 GB BD-R mode: source layout, 3T/6T mapping/inversion, parameters/presets, live preview, ISO generation, and assisted calibration in GUI and CLI. Dual-layer discs are unsupported; physical brightness still needs verification.
- Blu-ray inner-ring files support UDF 2.01 and individual files larger than 4 GiB. Generation and streaming burns share full-disc geometry, segmented file layouts, and bounded streams.
- Sequential burning without spare areas for new unformatted BD-R retains all 12,219,392 sectors. Capacity, blank status, and starting address are checked before writing. There is no automatic shrinking or rewrite after failure; chunk timeouts and cleanup diagnostics were added.
- CD/DVD/BD production and live previews can enable black/white dithering and independent sRGB gamma correction, off by default. Files, streaming burns, and source previews share processing options.
- Assisted calibration supports perspective rectification, curve-point fitting, parameter snapshots, and applying parameters. DVD uses cubic pitch, with the verified Verbatim DVD-R AZO (43533) preset, before/after photos, and records.
- Added standalone black/white dithering, DVD grayscale experiments, and Blu-ray byte-analysis tools, with instructions and reproducible validation steps.

### Improvements and fixes

- Live preview supports CD/DVD/BD source projection and artifact readback, configurable resolution, Fit to Window, 1:1 pixels, mouse zoom, and dragging. Fixed preset synchronization, browse commands, and DVD readback sampling positions.
- CD/DVD share two-level black/white mapping. Binary input preserves discrete pixels; scaling ring copies redithers them. Old multilevel artifacts retain recognition of their original gray levels during readback.
- Added SIMD sampling with boundary fallback for CD/DVD, and bounded parallel generation with integer SIMD for Blu-ray. Regressions verify scalar, parallel, and streaming output agreement.
- Burn shows rates based on device-confirmed written data and serializes device queries with writing. Preparation and finalization hide stale rates. Actual burning remains experimental.
- Fixed maximization with multiple monitors, different DPI, and taskbar positions; standardized parameter decimal precision. Home and recent jobs support Blu-ray; CD WAV output can optionally omit CUE generation.
- Calibration records use schema 6 and reject older pattern records. Preset and existing-track/image compatibility is documented separately. Actual geometry and brightness conclusions still require specific hardware/media verification.

### Open source and documentation

- Adopted GPL-3.0-or-later for compatibility with CDImage-derived interleaving, completing original-author copyright and acknowledgment notices.
- Preserved complete licenses for third-party components and HarmonyOS Sans. About displays them offline; single-file EXEs also embed the texts.
- Added standards and official links for ECMA-267, ECMA-359, ISO/IEC 30190:2021, and others to README; organized tool entry points, validation docs, cache exclusions, and personal-path examples.

## [1.0.26] - 2026-09-12

- First GitHub Release: the Windows x64 single-file EXE embeds .NET and runs immediately after download.
- Includes CD WAV + CUE / RAW generation, DVD drawing ISOs and hybrid data discs, plus GUI, CLI, live previews, ring copying, and editable presets.
- Includes byte-order fixes for CD WAV and streamed audio while retaining legacy RAW compatibility; user physical-disc feedback confirmed ghosting was gone after the fix.
- Includes experimental CD/DVD direct burning. Some physical feedback exists; coverage across devices, media, and burn paths remains limited.
- GitHub Actions builds releases and runs module regressions, DVD selftests, final single-file RAW/WAV generation, and GUI snapshot checks. Release notes provide SHA-256 download checksums.
- Unified versioning: packages are generated only when both version and corresponding notes change. Supports prereleases and manual retries while retaining a separate MSIX packaging workflow.

## Development history before the first GitHub release

These are cumulative development records. Validation status reflects each record's stage; current coverage is defined in `docs/VALIDATION.md`.

- Added GitHub Actions releases with versions centralized in `Directory.Build.props`. Only merges to `main` that change both version and release notes and change the complete version generate tags, self-contained Windows x64 EXEs, and GitHub Release drafts after CI and final single-file checks. Supports alpha/beta/rc; notes contain SHA-256. Maintainers publish after review, and public artifacts are not overwritten. MSIX remains manual, reusing CI and the same default version configuration.
- Changed default CD files to standard WAV (44.1 kHz, 16-bit stereo PCM), with `FILE ... WAVE` CUE. cdrecord reads WAV directly; ImgBurn opens its CUE. Legacy `.raw` remains big-endian. WAV converts to little-endian after delayed interleaving; streaming explicitly uses the same headerless little-endian PCM, fixing the IMAPI audio-input mismatch. Preview parses WAV RIFF chunks and normalizes byte order. Added asymmetric audio samples and Windows RAW-image byte-preservation regressions.
- Added hybrid-folder DVD streaming: plan ISO9660/Joliet, then write inner files, ECC-aligned padding, and outer patterns through a forward-only stream without a complete temporary ISO.
- Raised ring-copying production intermediates from 2048×2048 to 8192×8192; saved previews use 4096×4096 and live previews 2048×2048. Logs include each copy's actual pixel dimensions to avoid detail loss from low-resolution intermediates.
- Added presets for 80/74-minute CD, 12 cm single-layer DVD, and 8 cm single-layer Mini DVD. Generation and live-preview parameters synchronize both ways; manual changes switch to Custom parameters.
- Added the measured Ritek medical aqua-blue CD preset: 24.911275–57.931155 mm, 359845 sectors.
- Added measured Verbatim CD-R AZO (43438): 24.837775–58.020875 mm, 359848 sectors.
- Added measured Verbatim DVD-R AZO (43533): 23.9968875–57.9779875 mm, 2,297,888 sectors.
- Moved CD/DVD presets to layered JSON. Built-ins update with the app; the initial user layer creates custom CD/DVD parameters and saves only additions, same-ID overrides, and disabled entries. Presets contain capacity and generation geometry, not canvas or measured calibration settings. Default text language and name/description resource keys are reserved for later i18n without breaking user JSON. The UI opens/reloads the user layer, reports invalid edits precisely, and retains the last valid presets.
- Display decimal preset parameters at full actual precision instead of truncating to 3 or 6 places.
- Corrected CD built-in, GUI, and CLI default scanning speed from the old tool's 1100 mm/s to ECMA-130's lower limit of 1200 mm/s, with standard-range regressions.
- Added experimental CD and drawing-only DVD direct streaming through a 64 MiB bounded buffer into Windows IMAPI2 without complete ISO/RAW files. Automated tests pass; physical-disc validation was still pending.
- Fixed `0xC0AA0502` failures on some CD recorders caused by redundant `DoNotFinalizeMedia`. Session closing and underrun protection use safe IMAPI2 defaults; cleanup `ReleaseMedia` errors cannot replace the first true write error.
- Replaced CD streaming Track-at-Once, which required 2 extra sectors and rejected audio tracks, with Raw CD/DAO. Windows RAW Image Builder streams lead-in, Cooked R-W subchannels, and lead-out, fitting the measured 359,845 sectors exactly within the same disc's legal DAO boundary. At most 3 DAO sectors of bounded end overburn remain; larger excess is rejected before writing. When speed is unreported, a hardware-verified stable setting near 16× is used.
- Added querying and selecting current-media write speeds. Automatic mode rereads the actual CD/DVD media before writing and selects the lowest available speed to reduce errors; manual choice stays unchanged. Logs record the setting adopted by the device.
- Reversed dispersion polarity based on physical DVD results: black uses scrambled-domain `0xA5`, white `0x92`, in drawing-only and hybrid streaming burns.
- Raised the bounded burn buffer from 16 MiB to 64 MiB and prefill from 8 MiB to 32 MiB, increasing tolerance for generation jitter during 16× DVD writing.
- MSIX packaging now clears RID caches, checks source changes during publishing, and verifies the packaged burning component has removed old `DoNotFinalizeMedia`, preventing an updated host from embedding an old burning DLL.
- Added the separate `DiscImageStudio.Burning` abstraction, recorder enumeration, blank/capacity checks, second confirmation, progress, and cancellation; the image-file workflow stays unchanged.
- Added Ring Copy (automatic count): duplicates a source around the disc facing outward, with adjustable inner/outer safety boundaries.
- Fixed ring-copy layout: count remains automatic; insufficient space scales every copy uniformly without distortion, cropping, or overlap.
- Added separate `DiscImageStudio.Imaging` preprocessing shared by CD/DVD and reusable by future Blu-ray modules.
- The separate live-preview page supports both CD/DVD with bidirectional parameter synchronization and automatic refresh after 450ms debounce.
- CD geometry preview now uses the same sample-averaged grayscale mode as DVD.
- DVD UI and module fix the fast texture algorithm, fast output, and clockwise (CW) spiral; algorithm and direction selectors are removed.
- Added in-app PNG loading/display and fixed log height being constrained by the global input style.
- Fixed preview loading failure in .NET 9 WPF caused by incorrectly enabling URI cache bypass when loading PNG through `StreamSource`.
- Fixed signed-MSIX CD generation/preview crashes when writing logs due to conflicts between WPF and invariant globalization.
- Added a unified WPF GUI.
- Combined CD-DA and DVD workflows while isolating the DVD engine.
- Added the `DiscImageStudio.Core` disc-module abstraction.
- Added CD/DVD module adapters and extension points for future Blu-ray modules.
- Added MSIX packaging, Store assets, GitHub CI, and release documentation.
