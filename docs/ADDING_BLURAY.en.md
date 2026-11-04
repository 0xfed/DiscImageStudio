# Blu-ray mode (single-layer BD-R)

[简体中文](ADDING_BLURAY.md) | [繁體中文](ADDING_BLURAY.zh-TW.md) | [English](ADDING_BLURAY.en.md) | [日本語](ADDING_BLURAY.ja.md) | [Türkçe](ADDING_BLURAY.tr.md)

The Blu-ray implementation is in `src/DiscImageStudio.BluRay`. It references the media-independent Core and `src/DiscImageStudio.Iso` and does not depend on the CD/DVD encoders. Based on ISO/IEC 30190:2021, its current scope is **120 mm, single-layer 25 GB BD-R**; dual-layer discs, BDXL, BD-RE, and Blu-ray video authoring are unsupported.

## Place in the application

Blu-ray is the project's third disc type (CD / DVD / BD), rather than a sixth workspace. When a BD project is open:

- **Design**: the canvas and composite use the BD generation outer radius. “Disc files” in the left panel selects a folder and inner/outer placement, synchronized with Export. The tone panel on the right supports 2 levels, sRGB linearization, brightness, and 3T/6T polarity.
- **Preview**: design projection renders a 3T/6T category map using generation/measured geometry, tone, and polarity. Artifact readback directly classifies AA/44 payload bytes from the ISO without applying polarity; it shows categories rather than reflected brightness.
- **Export**: generates a complete BD ISO (drawing only or UDF 2.01 files). After selecting a folder, files can be placed at the inner or outer ring; inner is the default. Placement is saved with the project and recorded in the adjacent `<iso>.json` sidecar.
- **Burn**: feeds the same ISO stream directly into MMC sequential writing without saving a full image. The job dock provides progress and cancellation.
- **Calibration**: BD targets cover the entire data area and save sectors, radii, polarity, drawing boundaries, and the ISO timestamp. Fitting uses constant track pitch and applies the result to BD parameters.

BD presets are stored alongside CD/DVD in `bluRayPresets` in `disc-presets.json` (schema 4). Built-ins are “Standard 25GB single-layer BD”, “Ritek BD-R 10x”, “Ritek BD-R 12x”, “Verbatim BD-R (64099)”, and “Verbatim BD-R (64293)”. User presets can be saved at the bottom of Project Settings.

## Capacity and geometry

- Single-layer capacity is 12,219,392 user sectors of 2048 bytes, or 25,025,314,816 bytes. A cluster contains 32 sectors and 65,536 bytes.
- The project's sector count must be a positive multiple of 32 within the single-layer capacity; the outer radius must not exceed 60 mm.
- Generation and burning share the total sector count. Burning always declares the full disc capacity; unused sectors outside files and metadata contain zero payload.
- Nominal track pitch is 0.320 µm, channel bit length 74.5 nm, and start angle 0°.

## Generation and ISO

`BluRayIsoGenerator` plans UDF 2.01 directories and file extents before allocating a drawing range aligned to 32 sectors. Inner placement puts the file partition at the front; outer placement puts it before the reserved disc-end area. Front recognition information, anchors, and trailing backup information remain in their prescribed positions. Output length is fixed at `TotalSectors × 2048`.

- No folder selected: the complete data area is drawing only, without a filesystem. This `.iso` can be read back by this application, but ordinary filesystem tools cannot browse it.
- Folder selected: writes UDF 2.01 and supports individual files larger than 4 GiB. `--file-position inner|outer` selects placement, defaulting to `inner`; `outer` requires `--data-dir`. Files, padding, patterns, and trailing metadata are written in one forward stream.
- The output path must be outside the source folder and must not overwrite the source image. Cancellation or failure preserves an existing ISO.
- Successful generation writes `<iso>.json` (`format: bd-r-image-v1`): total sectors, inner/outer radii, drawing boundaries and radii, 3T/6T polarity, `ToneProfile`, volume label, UDF timestamp, source image, and generation time. Selecting this ISO in Preview restores these parameters automatically; an external ISO without a sidecar can still be read back with the current project geometry.

Files use ordinary user bytes and must not undergo pattern descrambling. Patterns retain the original whole-disc sector addresses and geometry; the remaining drawing area is not stretched to fill the disc. The UDF partition covers only the filesystem area, and patterns are not marked as free filesystem space. File occupancy and the trailing backup area together determine the actual drawable area. GUI design projection and artifact readback with a sidecar show the file area, including the filesystem, in yellow with a legend. File ranges are not inferred for external ISOs without layout information.

`BluRayIsoGenerator.Prepare` first scans the folder and plans UDF 2.01 directories and file extents. Inner placement rounds the end of the files up to a 32-sector boundary; outer placement rounds the file partition start down to a 32-sector boundary. The final 288 sectors are reserved for backup volume descriptors and anchors. This filesystem API retains an empty volume even when no folder is specified. At least one full drawing cluster must remain; the minimum test capacity is 640 sectors.

File and streaming output share a forward-writing path. If source files or directory contents change after planning, the stale layout is rejected. File lengths use 64-bit fields; large files are split into allocation extents no larger than `0x3ffff800` bytes and read sequentially through a 128 KiB buffer. Individual file size is limited by remaining disc capacity rather than 4 GiB. The DVD ISO9660/Joliet path is unchanged.

The UDF implementation follows [ECMA-167](https://ecma-international.org/publications-and-standards/standards/ecma-167/) and [ECMA TR/112-4 (UDF 2.01)](https://ecma-international.org/wp-content/uploads/ECMA_TR-112-4_1st_edition_december_2023.pdf) and writes closed, read-only volumes. It is for data discs and does not provide Blu-ray video authoring.

## Tone and polarity

BD tone processing runs once at source-image resolution: sRGB linearization → brightness offset → byte-level dithering → binarization at threshold 128. Generation, burning, and preview share this binary raster rather than hashing each of roughly 25 billion payload bytes.

`BluRayRunLengthMapping` performs only the final step: dark areas write `AA`, which favors 3T counts; light areas write `44`, which favors 6T counts. `InvertMapping` swaps the endpoints. This is an image-mapping convention, not a promise of pure 3T/6T or measured optical polarity. The actual light/dark direction must be observed with a calibration disc. Polarity is saved separately from `ToneProfile`.

## 3T / 6T count mapping and extension

The image path uses `BluRayImageLoader` to decode and composite transparency onto white, then samples through `BluRayImagePayloadMapping`. Aspect ratio is preserved, the longest side corresponds to the disc's outer diameter, and blank areas are white. Sample positions account for each frame's EDC placeholder, LDC interleaving, BIS insertion, DC bits, sync, and run-in. The 2:3 modulation rate gives nominal byte centers, rather than exact boundaries of variable-length 17PP codewords.

The built-in `BluRayRunLengthMapping` splits at 128 and returns scrambled-domain `AA` or `44`. Selection is based on the number of 3T / 6T intervals between adjacent modulation ONEs in full clusters of the same 958,272 channel bits, counting both marks and spaces; it is not the duty cycle of one level. In the 256 repeated-byte scan at baseline PSN `0x100000`, `AA` has 265,136 occurrences of 3T and `44` has 116,575 of 6T, each the highest count for its target. Checks at four addresses retain a strong bias without guaranteeing first place at every address. See [byte analysis](BLURAY_BYTE_DENSITY.en.md#按-3t--6t-数量接入绘图) for details.

Implement `IBluRayLuminanceMapping.Map` to replace the strategy, returning a chosen scrambled-domain byte for source luminance and nominal byte position. For non-image experiments, implement `IBluRayPayloadMapping.FillScrambledPayload` directly to fill the sector's 2048 user-byte positions, then pass it to `BluRayStreamingGenerator`. The callback receives the nominal sector, PSN, and disc parameters.

`BluRayDiscModule` receives an image-decoding delegate through its constructor to remain independent of WPF. The App CLI uses `BluRayImageLoader.Load`; callers generating images through the module API should supply this delegate too. `BluRayConstantByteMapping` remains limited to explicit digital experiments. Both image and calibration generation use `BluRayIsoGenerator` for file output or burner streaming.

See [byte and bit-period density analysis](BLURAY_BYTE_DENSITY.en.md) for candidate screening. `tools/BluRayByteProbe` reproduces full-cluster statistics for all 256 bytes and fine-grained periods. Periodic bit strings are other experimental strategies; the current count mapping uses repeated bytes. The analysis still does not assign physical black/white polarity.

## Command line

```powershell
# Inspect single-layer disc parameters, or export JSON
dotnet run --project src/DiscImageStudio.App -- bd-info --output bd-parameters.json

# Drawing-only ISO (1024 sectors are for a quick functional check, not a real full-disc configuration)
dotnet run --project src/DiscImageStudio.App -- bd-generate --input image.png --output bd-test.iso --total-sectors 1024 --invert true

# Full-capacity ISO with UDF files at the inner ring
dotnet run --project src/DiscImageStudio.App -- bd-generate --input image.png --output bd-image.iso --data-dir C:\Files --volume-label BD_IMAGE

# Tone: dithering, brightness, sRGB linearization
dotnet run --project src/DiscImageStudio.App -- bd-generate --input image.png --output bd-tone.iso --tone-dither 0.75 --tone-gamma true --tone-brightness -4

# Raw-payload probe and single ECC cluster diagnostics
dotnet run --project src/DiscImageStudio.App -- bd-probe --byte 0xA6 --sectors 32 --output bd-probe.bin
dotnet run --project src/DiscImageStudio.App -- bd-encode --input bd-probe.bin --psn 0x100000 --output bd-ecc.bin
```

`bd-generate` accepts `--preset` values `bd-r-sl-25gb`, `bd-r-ritek-10x`, `bd-r-ritek-12x`, `bd-r-verbatim-64099`, or `bd-r-verbatim-64293`. `--total-sectors`, `--r0`, `--r1`, and `--start-angle` override corresponding values. `--invert` accepts `true`/`false`. `bd-generate` no longer accepts the old `--start-sector` / `--sectors` slice parameters; slicing remains available only in `bd-probe`.

For `bd-probe`, `--byte` accepts 0..255 or `0x00`..`0xFF`, and ranges align to 32. `bd-encode` requires exactly 65,536 input bytes and produces a 76,880-byte ECC cluster without 17PP, DC-control, or a filesystem; it cannot be used as a burnable image.

## Encoding chain

| Component | ISO/IEC 30190:2021 |
| --- | --- |
| 2048-byte payload + 4-byte EDC | §13.2–13.3 |
| 16-bit LFSR, seed derived from PSN bits 19..5; reset per data frame | §13.4 |
| LDC RS(248,216), 304 codewords and interleaving | §13.5–13.8 |
| Address, RS(9,5), BIS RS(62,30), and multiplexing | §13.9–13.14 |
| 17PP, DC-control, and sync frames | §13.15–13.17 |
| Recording unit spacing | §14 |
| Nominal track pitch 0.320 µm | §15.4.3 |
| Single-layer PSN 0x00100000..0x00CA73FF | §17 |
| Single-layer data area: 381,856 clusters | §19 |

Pattern tests write **descrambled user payload**. The actual drive calculates EDC and scrambles each complete 2052-byte data frame. EDC, error correction, addresses, sync, and similar data cannot be arbitrarily overwritten by a selected-byte probe.

`BluRayRecordingFrameEncoder` accepts 28 DC control bits explicitly supplied by the caller. `BluRayRecordingUnitEncoder` additionally provides diagnostic simulation with run-in, 496 frames, run-out, and optional Guard3, representing each modulation bit / NRZI level with one byte. The automatic strategy is explicitly named `BluRayGreedyDcControlEncoder`: it makes look-ahead greedy choices with undecided future bits set to 0. Finalizing later control bits can change earlier local optima; the returned `SuboptimalDcControlDecisions` reports these cases. This experimental strategy guarantees neither the final DC optimum required by the standard nor the drive's actual bit sequence. CLI `bd-encode` exports only the deterministic ECC stage and does not export this diagnostic simulation as a burnable image.

`BluRayTrackGeometry` uses a separate constant-pitch spiral, deriving effective pitch from total track length corresponding to the manual total sector count and from the inner/outer radii. The last sector reaches the configured outer radius. Viewed from the data side, sampling in fixed disc coordinates proceeds clockwise outward (§9.8, §15.3); screen conversion reverses the Y axis exactly once. Each recording unit spans `2760 + 496 × 1932 + 1104 = 962136` channel bits at 74.5 nm per bit. Local `bd-probe` ranges retain full-capacity geometry. Guard3 at the sequence end is excluded from each recording unit's repeated span.

The default 24–58 mm is nominal geometry, not a measurement of a particular disc. Built-in streaming burning matches the `sector → PSN` model by leaving BD-R unformatted and allocating no defect-management spare area. Actual radii, positions, and optical contrast still require physical calibration. External ISO burning software should also use a mode without a spare area.

## Streaming burning without a spare area

For unformatted BD-R, `IDiscFormat2Data` may report 11,826,176 available sectors under the default spare-area scheme, 768 MiB below the raw 12,219,392 sectors. The Blu-ray path sends sequential `WRITE(10)` commands from LBA 0 through `IDiscRecorder2Ex.SendCommandSendDataToDevice`, 32 sectors (64 KiB) per command. Each command has a 200-second timeout to allow first-write initialization. Buffers obey adapter address-alignment and maximum-transfer limits. This path does not call the data-burning interface's `Write`, `FORMAT UNIT`, or spare-area allocation operations.

The public `IWriteEngine2` interface exposes no configurable command-timeout property, so query and cache-flush timeout settings cannot extend the internal wait in `WriteSection`. The current explicit-command path reports failed-command LBA, sector count, elapsed time, HRESULT, and Sense. A timeout can leave the device-side outcome uncertain; the application does not automatically resend that range.

Failure logs retain timing for the most recent 8 successful writes, separating source-stream read wait, device-command wait, and cumulative acknowledged data. Without valid Sense, they explicitly state “No valid device Sense data returned”; an all-zero result does not mean the disc has no error. `0x8007048F` means Windows reports the device disconnected. If a command with a 200-second timeout fails within milliseconds and the device then re-enumerates, investigate the connection, firmware, and USB events rather than treating it solely as insufficient wait time. Device queries and burning within this instance are queued to avoid overlapping an unfinished query with writing.

After exclusively locking the disc, the application uses MMC to read the current profile, disc information, format capacities, and track information. It requires a blank, unformatted, single-layer BD-R SRM with the first track starting at 0 and the full raw writable capacity. Unknown, used, or formatted layouts are rejected without automatically reducing total sectors. The UI's total sector count converts to the same requested length at 2048 bytes per sector, including the UDF prefix, drawing area, and trailing metadata; a smaller manual capacity still writes that same specified number of sectors.

After writing, the application flushes the cache, closes the track, and finalizes the disc with BD-R finalize function 6. Cancellation takes effect after the current device command finishes. Cancellation or write failure only attempts cache flushing and device release; it does not finalize an incomplete image as successful. Each unlock is marked complete only on success, and failed operations are retried once with errors retained in diagnostics; already successful paired count operations are not repeated. Once finalization starts, the command is awaited. Without a spare area, bad-block remapping has no reserve capacity; interrupted discs usually cannot resume writing.

BD live speed is calculated from write commands acknowledged by the recorder and includes device-cache effects. It is not the laser's instantaneous speed on the disc; final success still requires cache flush and finalization. Nominal user-data speed at 4× is 18 MB/s (1× = 4.5 MB/s). The UI uses decimal MB/s; requested speed multipliers cannot substitute for actual throughput. Automatic mode still selects the lowest profile reported for the current disc.

The device-returned configured speed is recorded immediately before writing; configured speed and live throughput are retained separately. A diagnostic window is logged every 10 seconds during writing and after the final block, distinguishing source-stream read time (including waits and copying) from WRITE command time (including transfer and device wait), and recording average acknowledged throughput. These low-frequency logs add no device queries and do not change progress or speed-freshness rules. Source read time is not pure producer-thread computation time, and command time alone cannot separate USB transfer from physical writing.

Image streaming uses at most 8 workers and bounded batches of 16 clusters (1 MiB), writing completed calculations in their original order. Built-in 3T/6T threshold mapping and scrambling XOR use integer SIMD while position calculations retain their precision; validated disc parameters are no longer rechecked per byte. Immutable raster images enable parallelism automatically, while built-in calibration patterns explicitly declare thread-safe sampling. Custom sampling callbacks remain serial by default. Prebuffering only absorbs temporary speed differences; sustained burning still requires production faster than the selected device speed.

Protocol and workflow tests use only fake devices and a 1024-sector (2 MiB) UDF stream. Full 12,219,392-sector capacity is checked only through capacity and geometry metadata, without generating a full-disc ISO. Physical burning results still need disc tests.

Implementation references: [Microsoft data-send and command-timeout interface](https://learn.microsoft.com/en-us/windows/win32/api/imapi2/nf-imapi2-idiscrecorder2ex-sendcommandsenddatatodevice), [libburn MMC implementation](https://dev.lovelyhq.com/libburnia/libburn/src/branch/master/libburn/mmc.c), and [libburn write timeouts](https://dev.lovelyhq.com/libburnia/libburn/src/branch/master/libburn/spc.h).

## Validation scope

Automated checks cover 17PP/EDC/Reed-Solomon, full RUB replay, image mapping, parallel generation, ISO/UDF, files larger than 4 GiB, sequential write commands, drawing ranges, sidecars, and cancellation. On-disk UDF tests use only 1024 sectors, with each ISO at most 2 MiB. Large-file tests first enable and verify sparse-file support, then create a logical source larger than 4 GiB with less than 1 MiB actually allocated, streaming into a sink that validates and discards data rather than writing a large ISO. Real disc capacity is checked mathematically only. Tests access no physical burner and do not establish visible brightness or polarity on an actual disc.
