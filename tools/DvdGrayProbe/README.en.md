# DVD 64 / E4 grayscale and mixture tests

[简体中文](README.md) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [日本語](README.ja.md) | [Türkçe](README.tr.md)

This tool creates a comparison test disc for **Verbatim DVD-R AZO 43533**. Using the previously tested `A5 / 66 / B9 / 92` as a control, it compares solid patches, dithered mixtures, and real images when `64` or `E4` replaces `66`, and separately tests mixtures of `64` and `E4`. The tool generates and verifies images; it does not burn discs.

All byte values mean **target Q bytes after DVD scrambling**. The atlas specifies Q directly. Generation reads it with nearest-neighbor sampling, without bilinear interpolation or grayscale requantization.

Help, progress, project-owned errors, and explanatory JSON text support five languages. Append `--language zh-CN|zh-TW|en|ja|tr|system` to a command; the default is `DISC_IMAGE_STUDIO_LANGUAGE`, or the system language when unset. Language selection preserves command names, JSON fields and technical identifiers, parameters, atlas data, and image data.

The independent pixel-occupancy verifier `verify_mixtures.py` requires Python 3 and NumPy and can run on Windows, Linux, or macOS with the prepared files. It also supports `--language`. It reads the atlas and writes measurement JSON/CSV, preserving fields and panel/row identifiers; only help, errors, and the measurement explanation are translated. Results describe pixel occupancy, not optical brightness or encoded ISO samples.

```powershell
python tools/DvdGrayProbe/verify_mixtures.py --prepared "artifacts/dvd-gray-probe-64-e4" --language en
```

## Generation and verification

Requires Windows and the .NET 10 SDK. Supply your own `6000 × 6000` four-level grayscale PNG. The tool reads a fixed `640 × 640` region with top-left coordinate `(2880, 1920)`. After conversion to Gray8, this region may contain only `0 / 85 / 170 / 255`; incorrect dimensions or grayscale levels cause generation to be rejected. The tool does not resize or redither the input, and the repository does not include a sample source image.

Run from the repository root, replacing `four-level-6000.png` with your input path. The output directory for `prepare` must be empty:

```powershell
dotnet run --project tools/DvdGrayProbe --configuration Release -- prepare --source "four-level-6000.png" --output-dir "artifacts/dvd-gray-probe-64-e4"

dotnet run --project tools/DvdGrayProbe --configuration Release -- generate --prepared "artifacts/dvd-gray-probe-64-e4" --output "artifacts/dvd-gray-probe-64-e4/burn.iso" --parallelism 8

dotnet run --project tools/DvdGrayProbe --configuration Release -- verify --prepared "artifacts/dvd-gray-probe-64-e4" --iso "artifacts/dvd-gray-probe-64-e4/burn.iso"
```

Output files:

| File | Purpose |
|---|---|
| `burn.iso` | Full-disc image for the actual burn: 2,297,888 sectors of 2048 bytes, totaling 4,706,074,624 bytes |
| `burn.iso.json` | Image generation record and verification information |
| `probe.json` | Layout, source image, and parameter record |
| `atlas.q8` | Direct Q target atlas, 6000 × 6000 |
| `code-map.png` | Layout guide with colors distinguishing Q bytes; colors do not represent actual disc color or brightness |
| `nominal-preview.png` | Preview drawn with nominal grayscale to identify content; it cannot predict the light/dark result after burning |

`burn.iso` intentionally contains no filesystem, so failure to mount it normally is expected. Select it through the burning software's **“Write image file to disc”** function; do not add it to a data disc as an ordinary file. Use a 43533 disc and the same recorder and speed as the previous test to enable comparison.

**Do not feed the two guide PNGs into the old grayscale-image generation workflow.** That would requantize them and fail to preserve this test disc's specified `64`, `E4`, and mixture ratios.

## Reading the test disc

Starting at 12 o'clock, there are 12 clockwise panels of 30°. The second part of each label is that panel's dark-gray candidate:

```text
1-66  2-64  3-E4  |  4-64  5-E4  6-66  |  7-E4  8-66  9-64  |  10-66  11-E4  12-64
```

Each group of three panels uses the same input, with palettes `A5 / candidate / B9 / 92`. Candidates repeat at different angles and change order to reduce the influence of disc position on comparison. All radii below are measured from the disc center:

| Radius | Test content |
|---|---|
| 24.5–25.6 mm | Inner solid patches: one patch every 15°, with `A5、66、64、E4、B9、92` repeated four times |
| 26.0–28.1 mm | Four-level serpentine Floyd–Steinberg gradient; all panels share the same level map, replacing only the dark-gray byte |
| 28.5–30.5 mm | Direct `64 / E4` mixtures: three clockwise columns per panel containing 25%, 50%, and 75% `64`, with `E4` for the remainder |
| Center radius 37.5 mm | Identical 12.8 mm square image crops |
| 45.8–47.5 mm | Candidate mixed with `A5` |
| 47.8–49.5 mm | Candidate mixed with `B9` |
| 49.8–51.5 mm | Candidate mixed with `92` |
| 52.2–57.3 mm | Outer solid patches in the same order as the inner patches, with byte labels |

Each panel in the last three mixture rows is likewise divided into three clockwise columns: **25%, 50%, and 75% candidate**, with the remaining pixels using that row's specified other byte. All candidates use the same binary FS masks.

Percentages are target pixel proportions, and `0 / 85 / 170 / 255` are nominal grayscale values. Neither represents evenly spaced physical-disc brightness. The image grid is **20 µm**, testing the mixture scale after image dithering rather than alternating individual Q bytes.

The input is already a four-level dithered PNG. The tool takes a `640 × 640` region at top-left coordinate `(2880, 1920)`, without resizing or dithering again. In the original experiment's crop, the four levels occupied approximately `24.13% / 23.90% / 22.74% / 29.24%`. With a different input, proportions vary with the image; among all image panels, only the byte corresponding to the original `85` level differs.

This atlas was independently checked across 144 mixture cells and 4,216,432 valid pixels. Excluding blank areas, the maximum difference between actual and target proportions was **0.501 percentage points**; the `64/E4` direct mixture row's maximum difference was **0.197 percentage points**, with no other Q bytes mixed in. Full records are in `mixture-measurements.csv` and `mixture-measurements.json` in the output directory. The ISO was also checked for length and compared byte by byte with a total of 2,359,296 payload target Q values at 9 radii. These are digital-output checks; actual disc appearance must be observed after burning.

## Recording results

First keep the light source and normal viewing position unchanged and compare each group of three adjacent panels. Then rotate the disc and inspect repetitions of the same candidate elsewhere. Finally, vary the viewing angle slightly to distinguish “stable visibility at normal angles” from “visible only at specific angles.”

Focus on whether a candidate can be distinguished from both `A5` and `B9`; whether increasing mixture proportions produces ordered changes; whether dark image details are recovered; and whether `64 / E4` mixtures create more useful, angle-stable levels than either solid patch. In the control, `B9` should remain clearly distinguishable from `A5` and `92`.

A simple record can use 0 = hard to distinguish, 1 = distinguishable at specific angles, 2 = stably distinguishable at normal angles:

| Candidate | Distinct from A5 | Distinct from B9 | Three mixture levels ordered and distinguishable | Dark image detail | Notes |
|---|---|---|---|---|---|
| 66 | | | | | |
| 64 | | | | | |
| E4 | | | | | |

Also record whether the 25%, 50%, and 75% levels of `64 / E4` can be distinguished from one another and whether they work only at specific angles. Decide whether to adopt a fourth level from these physical-disc results.

## Fixed calibration parameters

This experiment uses the confirmed 43533 calibration parameters:

- Start radius: `23.991460627731737 mm`
- End radius: `57.964893657925366 mm`
- Sector count: `2297888`
- Channel-bit length: `133.3 nm`
- Track-pitch polynomial coefficients: `[-3.9256854319227684E-05, 6.964795915429256E-05, -4.670918950137523E-05]`
- Atlas: `6000 × 6000`, covering `120 × 120 mm`

These parameters control this disc's spatial mapping. The guide images are not a physical brightness model.
