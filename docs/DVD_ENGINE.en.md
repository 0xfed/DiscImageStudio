# DVD NRZI image constraint solver

[简体中文](DVD_ENGINE.md) | [繁體中文](DVD_ENGINE.zh-TW.md) | [English](DVD_ENGINE.en.md) | [日本語](DVD_ENGINE.ja.md) | [Türkçe](DVD_ENGINE.tr.md)

The project treats 16 consecutive DVD data sectors as one ECC Block and searches for readable user data that satisfies a two-dimensional image or specified NRZI pit/land constraints.

Fixed conventions:

- Single-layer Data Zone, with Sector Information `0x00`;
- DVD-R Data Frame RSV consists of six `0x00` bytes;
- Supply only the ECC Block's first LBA; subsequent sectors automatically use `LBA + 1 ... LBA + 15`;
- `PSN = PSN offset + LBA`, with default offset `0x30000`;
- Choose Stream 1 when absolute DSV values tie;
- `pit = 0` (black), `land = 1` (white);
- Each output is 32768 bytes: 16 consecutive 2048-byte payloads.

The encoding chain includes ID, IED, all-zero RSV, EDC, data scrambling, RS-PC PO/PI, PO row interleaving, EFMPlus main/substitution tables, SYNC/DSV control, and NRZI conversion.

The independent [grayscale experiment tool](../tools/DvdGrayProbe/README.en.md) uses `FastDispersionImageWriter.WriteScrambledCodewordPatternStream` to specify scrambled Q directly in the same payload-codeword-center coordinate system, then writes `P = Q XOR mask`. This interface bypasses fixed grayscale bins, allowing experiments with candidate codewords such as `64/E4` and discrete dither atlases without changing the normal image palette. Fast generation neither replays whole-disc ECC/EFM nor predicts optical brightness. Regressions perform complete ECC/EFM replay on small mixed-codeword samples and separately verify actual payload, geometry, parallel consistency, and cancellation.

## Build and selftest

The commands below use the standalone Windows entry point and require the .NET 10 SDK. Running its non-self-contained build directly requires the .NET 10 Windows Desktop Runtime. Linux/macOS can execute DVD commands through the shared CLI; see [README.md](../README.en.md):

```powershell
dotnet build DiscImageStudio.slnx --configuration Release
dotnet run --project src/DvdImageSolver --configuration Release -- selftest
```

Selftest coverage:

- Samples of the ECMA-267 modulation tables;
- ID, IED, RSV, and EDC;
- Scrambler XOR round trips;
- Zero remainders for 208 PI rows and 172 PO columns;
- Count, determinism, and RLL(2,10) data runs of 619008 physical NRZI levels;
- Precomputed grayscale, ending level, and DSV increment of 16-bit codewords starting at pit and land;
- Codeword-replayed grayscale, SYNC selection, and final State/DSV/level of random SYNC Frames match the bitwise encoder;
- Dual-stream EFMPlus prefix states exactly match the bitwise encoder when alternate SYNC replay is unnecessary;
- State-control final boundaries match full DVD forward encoding without materializing the 619008-bit level array during solving;
- Dispersion's `0x92/0xA5` scrambled-domain mapping, descrambled ISO payload, and final boundary match full encoding;
- Minimal ISO9660 descriptors, Path Table, root directory, payload LBA, and template replacement results;
- Radius-calibration PNG previews and continuous raw-image output spanning multiple ECC Blocks;
- Protection separating inner-ring ISO9660/Joliet files, Unicode subdirectories, and outer-ring drawing data.

## Target files

Target files represent physical NRZI levels with characters:

| Symbol | Meaning |
| --- | --- |
| `0` or `P` | pit, black |
| `1` or `L` | land, white |
| `?` | unconstrained |

Whitespace and underscores are ignored. A file must contain either:

- 38688 symbols, constraining one physical sector; `--target-sector 0..15` specifies its position in the ECC Block;
- 619008 symbols, constraining a complete ECC Block.

SYNC, RLL, EDC, and ECC limit achievable patterns, so arbitrary bitwise images usually have no zero-error solution. `full` targets mismatched NRZI bits. Image `state-control` binarizes targets into black or white, first measuring wrong pit/land majority directions and then the distance of actual land counts from pure black 0 or pure white 16. Search objectives use only controllable payload codewords; final visibility counts all channel codewords.

`dispersion` uses EFMPlus transition density for two black/white textures rather than controlling absolute pit/land majorities. Scrambled bytes `Q=0xA5/0x92` have exactly 5/2 transitions respectively in every state representation. Image brightness 0–127 maps to A5 and 128–255 to 92, with preview brightness 0/255. New images no longer use 66 or B9 as separate gray levels. Inverting all NRZI levels does not change transition positions.

For fully opaque, already-dithered grayscale images containing only `0/255` (including all-black or all-white images), generation and calibration previews use nearest-pixel sampling to preserve original levels. Continuous grayscale, color, and transparent images still use bilinear sampling. The main app handles scaling, rotation, and layer composition in Design; generation, streaming burns, and previews share that composite and `ToneProfile`, without another ring-copying or preprocessing step. Binary targets in `full` and `state-control` retain their previous sampling rules.

## Image mapping

PNG, JPEG, BMP, TIFF, or GIF can be supplied directly:

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- solve `
  --image picture.png `
  --total-sectors 2295104 `
  --iso-output solved-disc.iso `
  --output payload.bin `
  --lba 0 `
  --psn-offset 0x30000 `
  --inner-radius-mm 24 `
  --outer-radius-mm 58 `
  --channel-bit-nm 133.3 `
  --start-angle-deg 0 `
  --spiral-direction ccw `
  --image-threshold 128 `
  --alpha-threshold 1 `
  --iterations 10000 `
  --constraint-output mapped-target.txt `
  --channel-output channel.bin
```

`--total-sectors`, also accepted as `--total-blocks`, is the disc-reported total number of 2048-byte data blocks. Together with 38688 channel bits per sector and `--channel-bit-nm`, it determines total track length. The program then solves for track-pitch scale using the inner/outer radii and pitch curve.

### Cubic track pitch varying with radius

DVD generation, streaming output, image previews, ISO readback, and assisted calibration share `DvdTrackGeometry`. For normalized radius `x=(r-ri)/(ro-ri)`, local pitch is:

```text
p(x) = a0 + a1*x + a2*x² + a3*x³                 [mm]
     = p0 * (1 + c1*x + c2*x² + c3*x³)
dr/dθ = p(x)/(2π)
```

The complete cubic polynomial has four coefficients. Inputs `--pitch-linear`, `--pitch-quadratic`, and `--pitch-cubic` are three dimensionless relative coefficients `c1,c2,c3`. The constant `a0=p0` is solved automatically from fixed total track length; the other terms are `ai=p0*ci`. Thus the four terms satisfy the length constraint from capacity and inner/outer radii, rather than being independent. The UI displays all four in μm. Their variable is `x`, not absolute radius in mm.

Track length uses `L=∫sqrt(1+(2πr/p(r))²)dr`, and angle uses `θ(r)=∫2π/p(r)dr`. Variable pitch cannot use the constant-pitch angle formula `(r-ri)/b`. The program verifies strictly positive pitch throughout the generated annulus, including internal polynomial extrema. All three relative terms being zero uses the original constant-pitch model; old parameters and presets default omitted fields to zero.

For example, append `--pitch-linear 0.02 --pitch-quadratic -0.03 --pitch-cubic 0.015` to the original `solve` command. This only demonstrates parameter syntax; it is not a measurement of any disc. `calibrate` uses the same options for the generation curve and `--actual-pitch-linear`, `--actual-pitch-quadratic`, and `--actual-pitch-cubic` for the actual curve; `--generated-pitch-*` aliases are also accepted. In `simulate`, `--pitch-*` describes the actual disc curve.

Image and preview JSON record relative coefficients and base pitch. Calibration sessions save relative coefficients, inner/outer radii, sector count, and bit length, allowing reconstruction of all four terms. Overall photo rotation remains an independently fitted quantity.

Coordinate and sampling conventions:

- The image center is the disc center; the shorter image side corresponds to the outer diameter, with the excess on the longer side retained;
- The track starts at 0° at the inner radius, pointing right in the image; the default mathematical direction is counterclockwise, changed to clockwise with `--spiral-direction cw`;
- Image black maps to pit and white to land; grayscale below `--image-threshold` counts as black;
- Transparent regions with alpha below `--alpha-threshold` impose no constraints;
- `--lba` is zero-based from the track's first data block; only 16 consecutive sectors starting there are mapped;
- Optional `--constraint-output` exports the actual 619008-symbol linear constraint for inspection.

## Solving

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- solve `
  --target target.txt `
  --output payload.bin `
  --lba 0 `
  --psn-offset 0x30000 `
  --initial-state 1 `
  --initial-dsv 0 `
  --initial-level land `
  --target-sector 0 `
  --iterations 10000 `
  --mutation-bytes 4 `
  --seed 1 `
  --channel-output channel.bin
```

Key parameters:

- `--lba`: the ECC Block's first LBA, not 16 LBAs;
- `--psn-offset`: decimal or `0x` hexadecimal, default `0x30000`;
- `--initial-state`: EFMPlus state 1 through 4;
- `--initial-dsv`: accumulated DSV before entering the current ECC Block;
- `--initial-level`: physical level `pit` or `land` before entering this block;
- `--seed-payload`: optional 32768-byte initial solution; otherwise a deterministic random initial solution is generated;
- `--initial zero`: use an all-zero initial payload instead;
- `--channel-output`: also output final NRZI levels, packing every 8 levels MSB-first.

Choose exactly one of `--target` and `--image`. Image mode also requires `--total-sectors` or `--total-blocks`.

### State-control codeword grayscale solving

The original algorithm with unrestricted payload positions remains as `--algorithm full`, also accepting `exact`. The old spatial control-byte pool algorithm has been removed; enable the new mode with:

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- solve `
  --image picture.png `
  --total-sectors 2297888 `
  --output payload.bin `
  --algorithm state-control `
  --bridge-bytes 2 `
  --beam-width 8 `
  --retry-beam-width 32 `
  --retry-frame-fraction 0.25 `
  --batch-frames 8 `
  --batch-candidates 4 `
  --search-lanes 1 `
  --iterations 3
```

State-control treats each 91-byte Sync Frame as one solving segment. EDC, PI, PO, fixed fields, and SYNC are still generated exactly and propagate State, DSV, and pit/land boundaries. They do not contribute to the free-byte search objective but do contribute to final whole-disc visibility scoring. Optimal black/white byte pairs are calculated for `EFMPlus state × initialLevel × currentDsv` (EFMPlus state, incoming level, and current accumulated DSV). At target positions, each active Stream tries only the black or white byte for its exact current state; each prefix keeps only the best `--beam-width` paths. Each free segment containing image targets also uses its first and last `--bridge-bytes` payload bytes as control positions. Those positions choose only from the current DSV byte pair, rather than enumerating all 256 values.

At startup the program preprocesses all 65536 16-bit codewords, starting at both pit and land, for:

- Land count among 16 NRZI positions: 17 gray levels from 0 to 16;
- Ending pit/land level;
- Exact DSV increment;
- Leading and trailing zero counts for RLL boundary checks.

For every possible EFMPlus representation, a byte pair evaluates grayscale error and the `|DSV|` after writing the codeword. The joint cost is `worstGrayError * 16 + worstOutputAbsDsv`: worst grayscale error multiplied by 16 plus worst absolute output DSV. Thus one gray level still outweighs 15 DSV units, while current DSV direction and magnitude can change the choice among bytes with similar grayscale. Byte pairs for common DSV values -256..256 are precomputed; out-of-range incoming or runtime values are calculated on demand using exact DSV, without clamping. These are scrambled recording-frame bytes entering EFMPlus. Original ISO payload is reverse-mapped through scrambler XOR, so its byte values need not be limited to these choices.

Inner candidates are scored one EFMPlus codeword at a time without expanding 16 channel levels. Each beam path keeps both Stream 1 and Stream 2, avoiding premature loss of either. Primary/secondary SYNC, Stream 1 tie-breaking, accumulated DSV, and final boundaries are replayed exactly codeword by codeword under the original encoder's integer rules. Each surviving Sync Frame sequence is also recomputed by the full 91-byte word-level encoder to handle alternate SYNC when DSV exceeds its bounds. Final results undergo only codeword-level majority, grayscale, and boundary checks; bitwise data is expanded only when `--channel-output` is explicitly requested.

State-control uses a strict lexicographic search objective: first minimize free-payload `controlled-majority-error`, compare `controlled-gray-error` only when majority-error counts tie, then compare boundaries and DSV if still tied. Main `majority-error`/`score` in console and JSON describe visible error across all final channel codewords. Each Sync Frame has 2 SYNC and 91 data codewords; for an opaque image, one ECC Block has the fixed denominator `416 × 93 = 38688`. Main `gray-error` also covers these 38688 codewords. Free-byte search scores are stored separately as `controlledScore` and `controlledGrayScore`. Lower is better for every error.

Each outer pass first computes exact controlled majority/gray errors per Sync Frame using the complete current ECC Block, then selects the worst `--retry-frame-fraction` (default 0.25) frames. It first tries those frames as a cross-block warm-start candidate to escape the random initial solution quickly. Regardless of local EFM score, acceptance requires updating EDC, PO, PI and exact replay of the entire block. Candidate ECC updates reuse current recording frames, rebuilding only changed payload sectors, affected PO columns, and PI rows; selftests verify word-for-word equality with full ECC reconstruction. Candidates then form small batches of `--batch-frames` (default 8); each batch generates and checks `--batch-candidates` (default 4) distinct EFM sequences in parallel. Acceptance requires a strict improvement in the complete block's controlled score. Failed batches split into progressively smaller groups down to single frames; failed single frames can retry with `--retry-beam-width` (default 32). Splitting follows only the first successful branch rather than exhaustively traversing the split tree. A pass with no accepted candidates stops early. `--retry-frame-fraction 0` processes every erroneous frame each pass; `--retry-beam-width 0` disables wider-beam single-frame retries.

Larger `--beam-width`, `--retry-beam-width`, and `--batch-candidates` make it easier to find candidates that survive ECC feedback, but runtime grows roughly with candidate-check count. Larger `--batch-frames` gives stronger local improvements at once but is more likely to be rejected after PO/PI feedback. Smaller batches are steadier but need more full reconstructions. `--iterations`, or `--iterations-per-block` in range mode, is the number of closed-loop passes, default 3. The compatibility alias `--algorithm control` remains available. Old `--frame-candidates`, `--parallel-candidates`, `--parallelism`, `--word-candidates`, and `--control-bytes` are rejected. `--mutation-bytes` belongs only to `full` mode.

`--search-lanes 1..8` reduces near-50% errors caused by local minima. Lane 1 uses the supplied initial payload with strictly monotonic acceptance. Other lanes restart from deterministic independent payloads derived from `--seed`, and may temporarily accept worse exact whole-block scores under annealing from `--initial-temperature` to `--final-temperature` to cross PO/PI barriers. Every lane separately retains its historical best after exact full-ECC replay. All lanes run in parallel; final selection compares controlled majority, gray, then final `|DSV|`, so exploration cannot make final output worse than lane 1. Default 1 adds no cost. For quality, first try `--search-lanes 4 --iterations-per-block 10` on a small sector range. Independent restarts change unconstrained payload too; keep 1 when specified payload content must be preserved.

`full/exact` means mutable payload positions are unrestricted; it does not mean exhaustive search over `256^32768` or a global optimum guarantee. Images are first binarized pixel by pixel using `--image-threshold`. State-control then majority-votes black/white image samples within each 16-bit payload codeword. Targets are only pure black 0 or pure white 16; an 8:8 tie uses the codeword-center sample. Solving first minimizes wrong majority directions, then uses land-count distance to 0/16 as grayscale error. It does not match each of the 16 individual bit positions; use `full` when exact channel-bit-position correspondence is required.

### Dispersion single-byte texture mode

`--algorithm dispersion`, alias `byte-pool`, generates payload directly in the scrambled domain without random search:

| Image brightness | Scrambled byte Q | Fixed transitions | Nominal preview brightness |
| --- | --- | --- | --- |
| 0..127 | `0xA5` | 5 | 0 |
| 128..255 | `0x92` | 2 | 255 |

ISO payload is calculated as `P = Q XOR sector scrambler byte`.

Each payload codeword samples the image at the disc-surface center of its 16 channel bits and selects the fixed bin above. Thus `--constraint-step` and `--image-threshold` do not affect this mode; `--image-threshold` controls only binary targets in `full/state-control`. ID, IED, zero RSV, EDC, PI, PO, and SYNC still follow the standard. The program does not expand 619008 NRZI bits, but replays complete recording frames codeword by codeword to propagate exact final EFMPlus state, DSV, and pit/land into the next ECC Block.

#### Tone dithering

The two textures are black and white, but image midtones can mix them proportionally at each payload byte:

```powershell
--tone-dither 1 --tone-levels 2 --tone-brightness 0
```

- `--tone-dither 0..1`: 0 keeps the original `--image-threshold` hard threshold; 1 dithers thresholds per byte to reconstruct grayscale through spatial black/white codeword ratios.
- `--tone-levels`: DVD has only two textures and accepts only 2.
- `--tone-brightness -64..64`: shifts grayscale overall.

Dither phase depends on logical payload byte index, independently of preview sampling. `simulate` classifies ISO bytes only by `0xA5/0x92`, so readback naturally reflects dithering. Omitting these parameters preserves the old output byte for byte.

Add `--fast-output true` when a burnable image is needed quickly without final EFMPlus boundaries. Fast output samples the same payload-codeword centers and produces the same 2048-byte sectors as normal dispersion. It uses a precomputed 32 KiB scrambler mask to derive ISO bytes `P` directly from `Q`, without constructing Data Frames, EDC, PI, PO, or Recording Frames, or replaying EFMPlus, DSV, or pit/land. JSON has `exactEccEfmReplay` set to `false` and `finalBoundary` set to `null`. No required burning data is omitted: the recorder generates these derived fields from the 2048-byte sectors itself.

```powershell
& ".\src\DvdImageSolver\bin\Release\net10.0-windows\DvdImageSolver.exe" solve `
  --image target.png `
  --total-sectors 2297888 `
  --iso-output "R:\full-disc-fast.iso" `
  --fill-sectors 2297888 `
  --lba 0 `
  --psn-offset 0x30000 `
  --inner-radius-mm 24 `
  --outer-radius-mm 58 `
  --channel-bit-nm 133.3 `
  --algorithm dispersion `
  --fast-output true `
  --fast-parallelism 0 `
  --initial zero
```

`--fast-parallelism 0` generates independent ECC Block payloads in parallel according to logical processor count, then writes them in LBA order. Set `1..64` to limit CPU use. Parallelism changes neither byte choices nor output order.

Fast file and streaming output enable four-point double-precision SIMD sampling by default when hardware acceleration is available. Each group still uses four actual payload-codeword centers; positions across SYNC/ECC boundaries are not treated as equally spaced consecutive samples. Cubic-pitch inversion within one segment shares a segment lookup and vectorizes the original four Newton/Hermite calculations. Cross-segment and boundary cases fall back to scalar geometry. Fully opaque black/white images use vector sine/cosine, with points near half-pixels or image edges recomputed by the original scalar path. Continuous grayscale, color, and transparent images keep scalar trigonometry and interpolation, accelerating only cubic-pitch geometry. Constant pitch, analytic pattern callbacks, and machines without SIMD continue scalar sampling. The constant-pitch four-point path currently regresses in measured performance and remains disabled. All computations retain `double`, without single-precision coordinates or approximate spiral formulas.

Generation APIs can set `MultiBlockSolveOptions.UseSimdSampling = false` for the scalar reference path when comparing performance and output. Default is `true`, requiring no GUI or CLI argument changes. Regressions cover payload/count equality between both paths, cubic-pitch segment boundaries, half-pixel boundaries, alpha thresholds, clockwise/counterclockwise directions, and parallel output order. Measure throughput using actual payload bytes over identical inputs and LBA ranges; isolated trigonometric speedups do not establish whole-disc speedups.

Fast constant-pitch inversion caches the fixed inner-radius integral, squared radius, and pitch coefficient after solving, and reuses the same square root within one Newton correction. This preserves original `double` operation order, division, and boundary behavior without approximate integration or reciprocal multiplication. An independent old-formula regression compares radius, angle, and short-spiral boundaries bit for bit. Performance comparisons require independent engines before and after the optimization: both sides of `UseSimdSampling` use this scalar optimization.

`payload-transition-class-error=0` means every controlled payload byte is assigned to the correct binary `0xA5/0x92` class, not that fixed fields, ECC, and SYNC share that transition density. Output JSON `dispersionPool.levels` records both input ranges, Q bytes, transition counts, and nominal preview brightness. Those preview values are not physical-disc reflectance.

Outputs include:

- `payload.bin`: 16 consecutive 2048-byte sectors;
- `payload.bin.json`: PSN, initial/final boundary states, errors, iterations, and elapsed time;
- Optional `channel.bin`: 77376-byte packed NRZI levels.

## Burnable ISO output

`solve` can output only an ISO or also retain the 32768-byte payload:

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- solve `
  --image picture.png `
  --total-sectors 2295104 `
  --iso-output solved-disc.iso `
  --lba 0 `
  --iterations 10000
```

Image mode uses `--total-sectors` directly as the ISO's logical sector count. Text constraints additionally require `--iso-sectors`:

```powershell
--target target.txt --iso-output solved-disc.iso --iso-sectors 2295104
```

New images contain a minimal ISO9660 filesystem and `SOLVED.BIN`. The 16 solved 2048-byte sectors are written at `--lba ... --lba+15`. At LBA 0 this lies in the ISO9660 system area, so a visible `SOLVED.BIN` copy is also placed in the filesystem data area. On NTFS with sparse-file support, new images use sparse storage where possible. Logical length remains `total-sectors × 2048`, and burning software reads holes as zero bytes.

An existing ISO can also serve as a template for replacing 16 consecutive sectors:

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- solve `
  --image picture.png `
  --total-sectors 2295104 `
  --iso-template original.iso `
  --iso-output solved-disc.iso `
  --lba 32 `
  --iterations 10000
```

Template size must be a multiple of 2048; in image mode its sector count must also equal `--total-sectors`. Replacing sectors assigned to directories, files, or UDF/ISO metadata corrupts the original filesystem. Choose reserved space or extents already belonging to the target file. New minimal ISOs reject overwriting fixed ISO9660 volume descriptor sectors 16 and 17.

ISOs store 2048-byte user data rather than a 619008-bit raw channel stream. A standard DVD recorder regenerates ID, EDC, ECC, EFMPlus, and NRZI. Actual patterns therefore also depend on whether the drive makes the same encoding choices, and whether solver initial state, DSV, and pit/land match the real recording boundary.

### Continuous whole-disc or selected-sector generation

When `--fill-sectors` exceeds 16, consecutive ECC Blocks are solved from `--lba`, passing each final state, DSV, and pit/land to the next. Sector count must be a multiple of 16. For a disc with 2297888 sectors, the full-disc command is:

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- solve `
  --image picture.png `
  --total-sectors 2297888 `
  --iso-output full-disc.iso `
  --fill-sectors 2297888 `
  --lba 0 `
  --psn-offset 0x30000 `
  --inner-radius-mm 24 `
  --outer-radius-mm 58 `
  --channel-bit-nm 133.3 `
  --constraint-step 16 `
  --algorithm state-control `
  --bridge-bytes 2 `
  --beam-width 8 `
  --retry-beam-width 32 `
  --retry-frame-fraction 0.25 `
  --batch-frames 8 `
  --batch-candidates 4 `
  --search-lanes 1 `
  --iterations-per-block 3 `
  --initial-state 1 `
  --initial-dsv 0 `
  --initial-level land
```

To generate only the first 1600 sectors, change it to:

```powershell
--fill-sectors 1600
```

This produces 1600 sectors from LBA 0 through 1599. `--total-sectors` still describes full-disc capacity for track geometry. If `--lba` is nonzero, preceding zero sectors are retained, making image length `(lba + fill-sectors) × 2048`.

Range mode overwrites every specified sector, including ISO9660 descriptors. Its output is a directly burnable raw 2048-byte/sector image, not a mountable ISO9660 filesystem. `--constraint-step` is the image sampling interval for free-payload search; state-control majority-votes binary samples inside each payload codeword. Final visibility is not limited by this sparse step: it resamples each 16-bit channel-codeword center, including derived, fixed, and SYNC codewords. For strong contrast in binary images, `--constraint-step 16` gives each payload codeword a target. `32` is faster at half the density; `256` constrains only about 1/16 of payload codewords, so overall majority error tends toward random levels. A full disc has 143618 ECC Blocks; first measure speed and results on 160–1600 sectors.

Dispersion always samples each payload-codeword center and ignores the sparse-search meaning of `--constraint-step`, so that argument can be omitted.

## Mixed files and patterns

`--data-dir` enables hybrid mode, recursively writing the directory and subdirectories as standard ISO9660 + Joliet. `--file-position inner` (default) puts files on the inner ring and the pattern outside them. `--file-position outer` puts file contents at the end, with the pattern after leading metadata and before files. Drawing boundaries align to 16-sector ECC Blocks. Full-disc geometry is retained; the remaining drawing area is not stretched over the entire disc. The final ISO opens as an ordinary data disc on systems such as Windows, and Joliet preserves Chinese filenames.

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- solve `
  --image picture.png `
  --data-dir ".\disc-files" `
  --file-position outer `
  --iso-output hybrid-disc.iso `
  --total-sectors 2297888 `
  --volume-label MY_DISC `
  --inner-radius-mm 24 `
  --outer-radius-mm 58
```

Hybrid mode defaults to `--algorithm dispersion --fast-output true`, suitable for generating the complete outer ring directly. Explicit `--fast-output false` first encodes and replays all inner-ring blocks to find the EFMPlus state, DSV, and pit/land at the outer ring, then draws from exact boundaries. Exact replay takes longer as inner-ring data grows.

The GUI Design page's Files on disc area selects a folder and inner/outer file placement; Export shares those settings. They are saved in the project and also used for streaming burns. Streaming always uses fast dispersion: scan the folder and fix layout, then write metadata, files, and patterns in disc order without a complete temporary ISO. Outer placement writes patterns before files. If source file size changes after planning, streaming refuses to continue. Preview highlights filesystem space in yellow; the color never enters recorded data.

This mode rejects `--lba` and `--fill-sectors`, calculating them from file layout and capacity. `--drawing-start-lba N` can leave extra space beyond the default drawing start but must remain in the drawing area and align to 16 sectors together with `--psn-offset`. `--file-position outer` requires `--data-dir`.

The ISO9660 writer supports recursive directories and Joliet Unicode names. Individual files cannot exceed 4 GiB. To avoid link cycles and accidental references outside the folder, `--data-dir` rejects reparse points/symbolic links.

## Radius-calibration previews

Calibration follows the CD version's forward projection: sample sequential global channel positions, read the target image on the assumed generation spiral, then project the same position onto the guessed actual spiral. Multiple track samples landing in one output pixel are averaged. This preserves true phase accumulated from pitch differences while avoiding bubble-like moiré from finding the nearest track separately for each output pixel.

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- calibrate `
  --image picture.png `
  --output calibration-preview.png `
  --total-sectors 2297888 `
  --generated-inner-radius-mm 24 `
  --generated-outer-radius-mm 58 `
  --actual-inner-radius-mm 24.2 `
  --actual-outer-radius-mm 57.8 `
  --channel-bit-nm 133.3 `
  --spiral-direction ccw `
  --image-threshold 128 `
  --alpha-threshold 1 `
  --preview-size 2048 `
  --samples-per-sector 16
```

`--samples-per-sector` controls forward-projection density, range 1–4096, default 16. A DVD sector spans about 5.16 mm of track; 16 gives a sample about every 0.32 mm. Increasing to 32 or 64 reduces empty pixels and improves detail, with approximately linear runtime growth. There is no arbitrary rotation-offset option. Generation and actual spirals share a 0° start; overall rotation does not affect radius calibration. The preview also writes `calibration-preview.png.json`, setting `mappingMode` to `forward-channel-splat-average` and recording density, both pitches, total turns, and track length. It does not include remaining solver error or recorder implementation differences.

Calibration previews the full disc by default. If only part of the image was generated, supply the same `--lba` and `--fill-sectors` as the solve command; unwritten regions remain transparent.

## Readback simulation of generated ISOs

`calibrate` takes the source image and shows the ideal result of writing with generation geometry and reading with measured geometry. It uses generation's binary threshold of 128; `--image-threshold` remains for compatibility but does not alter this preview. `simulate` reads the generated ISO itself: each sampled payload byte is DVD-scrambled back into the `0xA5/0x92` classes, shown as 0/255, then its codeword center is projected onto the disc using measured-radius spirals. For old multilevel images, readback still shows `0x66` as 0 and `0xB9` as 85; those values are not used in new generation. This closes the loop in the same manner as cdimage's calibration dialog: generation geometry is already embedded in the ISO, so simulation needs only measured geometry.

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- simulate `
  --iso drawing.iso `
  --output readback-preview.png `
  --inner-radius-mm 24.2 `
  --outer-radius-mm 57.8 `
  --preview-size 2048 `
  --samples-per-sector 16
```

When `--total-sectors`, `--lba`, and `--fill-sectors` are omitted, they come from solve's `drawing.iso.json` sidecar (drawing start LBA and sector count). Without it, the entire ISO from LBA 0 is used. `--samples-per-sector` controls sampled payload bytes per sector (1–2048, default 16). Scrambled bytes outside the binary table or legacy 66/B9 values are skipped. Filesystem prefixes may accidentally match the table; use the sidecar drawing range to exclude them. The ISO itself contains no PI/PO error-correction codes. `readback-preview.png.json` is written beside output with `mappingMode` set to `payload-scramble-classify-splat`, recording classification hits, pitch, and total turns.

Samples use codeword centers. `FastDispersionImageWriter` places image targets at `PayloadDirectChannelOffset + 8` (half a 16-bit codeword); readback uses the same position. Thus the two projections have no 8-channel-bit offset (about 1 µm of track). Readback and generation also share spirals, direction, and scrambling masks. Matching generation and preview parameters therefore reproduces the generated image sample by sample.

When switching ISOs, Preview reads sidecar `imageMapping.totalSectors`, `innerRadiusMm`, and `outerRadiusMm` to load calibration geometry. Imported images are not squeezed into thin rings by reusing the previous disc dimensions; later user adjustments start from the loaded parameters.

## The two Preview workspace modes

Preview's input switch exposes both paths, sharing debouncing, cancellation, and export actions:

- **Artifact readback** (readback simulation, `simulate`) takes a generated ISO and one set of measured geometry, showing bytes actually written in the image.
- **Design projection** (calibration preview, default, `calibrate`) takes the source image and generation/measured geometry. It does not read the ISO, so parameter changes refresh much faster. The image is the source projected under measured geometry, rather than actual recorded codewords; solver errors and filesystem prefixes do not appear.

Calibration previews take the Design composite directly, using the same source as image generation so both patterns agree.

## Forward encoding only

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- encode `
  --input payload.bin `
  --output channel.bin `
  --lba 0 `
  --psn-offset 0x30000 `
  --initial-state 1 `
  --initial-dsv 0 `
  --initial-level land
```

The first PSN must be a multiple of 16; otherwise encoding is rejected because input does not start at an ECC Block boundary.

## Regenerating modulation tables

`Encoding/EfmPlusTables.Generated.cs` is mechanically extracted from ECMA-267 Annex G. To regenerate it from the specification PDF:

```powershell
python tools/generate_efm_tables.py ECMA-267.pdf src/DvdImageSolver/Encoding/EfmPlusTables.Generated.cs
```
