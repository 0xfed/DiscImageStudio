# Validation notes

[简体中文](VALIDATION.md) | [繁體中文](VALIDATION.zh-TW.md) | [English](VALIDATION.en.md) | [日本語](VALIDATION.ja.md) | [Türkçe](VALIDATION.tr.md)

## Upstream feature sync regressions

Checks for upstream `62efb5c` include `UsableSectorGenerationTests`, `UsableSectorPersistenceTests`, `StreamBurnCapacityTests`, `PresetApplicationTests` and `CalibrationConfirmedFitTests`. Additional coverage retains UDF/VAT updates, timing-result usable limits, visual-selector truncation and old/new recipe replay. The seven-language layout matrix remains in place; these tests provide no new physical-recording validation.

This page lists reproducible checks for the current source. Automated regressions validate algorithms, file layouts, and application workflows; physical burning results still depend on the recorder, firmware, and disc.

## Local validation

The most recent full architecture regression was on 2026-10-28, covering UI fixes, calibration generation optimizations, and the Verbatim BD-R (64099) preset (commits `3088802`, `49a0e4d`, and `46e2c53`), on Windows with .NET SDK 10.0.400 (runtime 10.0.11), in Release configuration. The full solution build (0 warnings, 0 errors) and all architecture regressions passed. Tests run the custom executable harness directly, using no `dotnet test` invocation.

After calibration burning finished, the generation optimizations passed the build, full architecture regression, and DVD engine selftest. After adding the Verbatim preset, the build and full architecture regression passed again, checking consistent GUI/CLI radii, sector counts, and polarity, plus preset merging and command-line overrides. The v1.3.3 version/changelog validation and all 45 release-tooling checks passed; no new performance benchmark or physical-disc burning conclusions were added.

New regressions that passed cover: BD calibration parallelism controls, default sequential callbacks, polarity and drawing range, batch order and cancellation; bit-identical coordinates, interleaving, and byte order for CD calibration four-point sampling, and scalar/SIMD byte equality for inner/outer-ring CD file mode; and coordinate, index, byte-output, and parallel-tail-batch equality between DVD analytical sampling and codeword sampling.

Run from the repository root:

```powershell
dotnet restore DiscImageStudio.slnx
dotnet build DiscImageStudio.slnx --configuration Release --no-restore
dotnet run --project tests/DiscImageStudio.ArchitectureTests --configuration Release --no-build
dotnet run --project src/DvdImageSolver --configuration Release --no-build -- selftest
dotnet run --project src/DiscImageStudio.App --configuration Release --no-build -- selftest
./tests/Test-ReleaseTooling.ps1
```

Architecture regressions cover:

- CD/DVD: black/white mapping, dithering, sRGB gamma, file/stream consistency, and SIMD/scalar sampling consistency.
- CD file mode: independent reading of Chinese files in ISO9660/Joliet, inner/outer-ring placement and project persistence, file-region previews, raw-sector geometry, and identical file/forward-stream output.
- CD data DAO: Mode 1 EDC and all P/Q parity equations, main-channel byte order, lead-in and pregap addresses, data-track Q identifiers and CRC, forward seeks and short reads, and complete inner/outer-ring ISO recovery. The Ritek preset retains 359,845 sectors, and a simulated recorder checks the capacity limit shared with audio tracks. Native IMAPI tests construct only an in-memory image and do not create a recording device.
- BD: 17PP/EDC/Reed-Solomon, 3T/6T mapping and polarity, zeroing outside the drawing range, UDF 2.50, files larger than 4 GiB, sidecar round-trips, and rejection of invalid boundaries.
- DVD/BD file placement: default inner-ring compatibility, independent parsing of outer-ring ISO9660/Joliet and UDF directories, Chinese file contents, full-disc coordinates, identical non-seekable-stream/file output, capacity and cancellation guards, and round-trips of project placement options.
- Three-family model: project schema 2 round-trips and schema 1 upgrades, preset schema 4 merging with older schemas, and BD apply/capture in `ProjectSession`.
- Burning: operation exclusion, progress, sequential writing, timeouts, cancellation, and resource cleanup, all using simulated interfaces rather than real recorders.
- Streaming-burn display: progress advances only on device-confirmed writes; speed-multiple conversions for CD audio/data, DVD, and BD; unknown or invalid rates, 3-second expiry, finalization and end cleanup, transfer-log throttling, and protection against late events.
- Calibration burn sources: CD/DVD/BD labels match saved parameters; BD bit length, polarity, and drawing range; switching disc families, speed queries, and simulated burn requests and confirmation text.
- Calibration: cubic track pitch, BD drawing range and polarity, photo rectification, curve fitting, session save/restore, disc preset synchronization, and parameter precision.
- Previews and windows: CD/DVD/BD projection and readback, multiple monitors, and taskbar boundaries.
- File-region previews: yellow highlighting in inner/outer-ring design projections and artifact readback with sidecars, DVD cubic-pitch boundaries, no highlighting for pure drawing or invalid layout information, invalid-directory/capacity messages, and cleanup on switching/reset. The yellow overlay does not change ISO data.

The GUI supports off-screen screenshots through the existing commands:

```powershell
dotnet run --project src/DiscImageStudio.App --configuration Release --no-build -- ui-snapshot --output artifacts/validation/home.png --tab 0
dotnet run --project src/DiscImageStudio.App --configuration Release --no-build -- ui-snapshot --output artifacts/validation/export-bd.png --tab 3 --project sample.discproj
dotnet run --project src/DiscImageStudio.App --configuration Release --no-build -- ui-snapshot --output artifacts/validation/preview-bd.png --tab 2 --project sample.discproj --live-input image.png --live-disc bd --live-mode calibrate
dotnet run --project src/DiscImageStudio.App --configuration Release --no-build -- ui-snapshot --output artifacts/validation/burn-bd.png --tab 4 --project sample.discproj --burn-source calibration --burn-progress write
```

`--burn-progress` accepts `write`, `buffer`, and `finalize`, rendering the UI with simulated progress without starting a real burn. On 2026-10-28, BD calibration was checked in wide and narrow 800×650 windows, including preparation and finalization states; the bottom written amount, rate, speed multiple, and progress bar were visible. The DVD engine selftest also passed in that session; no physical disc was burned.

The `build` workflow also runs the module catalog's DVD selftest, a two-sector CD output check, and a home-page screenshot. See the [release guide](RELEASING.en.md) for additional checks on the published single-file EXE.

That session also checked design projections for both file positions on DVD and BD, outer-ring export pages, and outer-ring artifact readback, totaling 8 off-screen screenshots. DVD/application selftests and all 45 release-tooling regressions passed.

The design page's file-selection entry was also checked in 7 UI states: DVD, BD, CD, no folder selected, 18 layers, narrow window, and low window height. In a wide window the layer list scrolls internally and the file-selection area stays reachable; the build had 0 warnings and 0 errors.

v1.3.0 also passed .NET 10 self-contained single-file checks through `Build-Release.ps1`: with only the EXE in an isolated directory, burner-interface build information, the DVD selftest, two-sector RAW/WAV/CUE, CUE disabled, and WPF rendering all passed. The final build embeds .NET / Windows Desktop 10.0.11 and retains the complete upstream licence notices.

v1.3.2 passed the full architecture regression, DVD engine and application module selftests, and all 45 release-tooling checks. New regressions verify that Blu-ray polarity stays consistent between Save As and project confirmation after changing presets, and that cross-family selection and geometry synchronize after deleting the recent preset. Canvas zoom covers the 25%–1200% boundaries, plus/minus buttons, and fit-to-window reset. Four additional off-screen screenshots checked new CD, Blu-ray, and DVD project settings and an 18-layer design page.

## Translation preflight

Packaging translation regressions check metadata, diagnostics, language precedence, Unicode paths, and language forwarding to child checks without building, signing, installing, or elevating; numeric culture stays unchanged. The shared runtime also checks five-language resource-loading failures, exception types, and stream disposal. For Windows PowerShell, replace pwsh below with powershell. Actual Windows installation and packaging still require verification on Windows.

```powershell
pwsh -NoProfile -File tests/Test-ReleaseTooling.ps1
pwsh -NoProfile -File tests/Test-PackagingLocalization.ps1
```

## Limits of physical-disc validation

The file-placement and preview changes in that session were not tested by physically burning a disc. Automated tests do not prove device write strategies, actual reflected brightness, or burning success rates; Blu-ray functionality remains experimental.

The v1.3.1 CD data DAO path has not been physically burned either. It has passed the full architecture regression, standalone single-file EXE startup, audio/CUE checks, and verification of inner/outer-ring ISO file contents and artifact readback.

Recorded DVD cubic-track-pitch photographs and parameters appear in the [README before/after comparison](../README.en.md#辅助标定展示). The historical physical-model audit is in [PHYSICAL_MODEL_AUDIT.md](PHYSICAL_MODEL_AUDIT.en.md); its conclusions retain their stated dates and code baseline and do not replace validation of the current source.

Future physical-disc records should include the version or commit, recorder and firmware, disc model, preset parameters, speed, generation/burning entry point, and original photographs. Avoid generalizing one device's results to all discs.

Assisted calibration now has coarse, boundary and fine stages and can run independently of a drawing project. Confirm the disc capacity, then create the matching target for each blank disc. Analyze every recorded disc with its own saved generation record and photograph. [Calibration after the upstream integration](CALIBRATION_TUTORIAL.en.md).
