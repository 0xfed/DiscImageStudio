# Black-and-white dithering tool

[简体中文](README.md) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [日本語](README.ja.md) | [Türkçe](README.tr.md)

An independent Windows image-processing application that converts an original image into a two-level black-and-white PNG containing only **0 and 255**. DVD and CD both use these levels, preserving tone through spatial mixtures of black and white pixels. The executable remains named `FourLevelDither.exe` to keep existing entry points compatible.

## Window controls

1. Run `FourLevelDither.exe` and click “Open image” or drag an image into the window.
2. Select “DVD two-level” or “CD two-level” to view the corresponding codeword explanation; both types output the same black-and-white palette. Enable “100% pixel view” to inspect dithering detail.
3. “sRGB gamma correction” is off by default, preserving existing output. Enabling it automatically reprocesses the image, and the window shows the current setting.
4. Click “Save black-and-white PNG” to save the processed image separately. With correction off, the default filename ends in `-黑白抖动.png`; with it on, it ends in `-黑白抖动-sRGB.png`, making comparison easier. The program refuses to overwrite the current original image path.

Changing the disc type or gamma option recalculates from the cached original, rather than processing the previous dithering result again. When moving from an earlier three- or four-level scheme to two levels, reopen and process the undithered original to retain the fullest tonal information.

Supports PNG, JPEG, BMP, TIFF, GIF, and other formats supported by the system image decoders. Export is lossless Gray8 PNG, retaining the original pixel dimensions and valid DPI; invalid DPI falls back to 96. Preview zoom does not change the saved pixel dimensions.

## Processing rules and disc mapping

By default, colors are converted to luminance in encoded RGB space using Rec.709 weights, with transparency first composited over white. **Serpentine Floyd–Steinberg error diffusion** then scans adjacent rows in alternating directions, chooses the nearest black or white for luminance including propagated error, and distributes the luminance difference to subsequent pixels.

| Main application's two-level input range | PNG / preview grayscale | DVD scrambled-domain Q byte | CD byte |
| --- | ---: | --- | --- |
| 0–127 | 0 | `0xA5` | `0x0D` |
| 128–255 | 255 | `0x92` | `0xAA` |

With gamma correction off by default, a large original grayscale-128 region produces approximately `128 / 255 = 50.2%` white pixels and black pixels for the remainder. Propagated error is added during dithering; output files contain only 0 and 255 whether gamma correction is enabled or not.

### sRGB gamma correction

When enabled, the program first decodes each RGB channel through the standard sRGB transfer function into linear light. It then computes luminance using Rec.709 weights, composites over white in linear space, and diffuses error. This changes the ratio of black to white pixels, not disc codewords or output grayscale levels.

For example, original grayscale 128 decodes to approximately 21.6% linear luminance and therefore produces approximately 21.6% white pixels. Midtones in the same image are usually darker than with correction off. Save both versions to compare physical-disc results. Disabling the option retains the original algorithm and unchanged default behavior.

The main application manages tone in the Design workspace's right column; generation, streaming burns, and design projection share the selected tone settings. No prior PNG export from this independent tool is needed. New projects default to 75% dithering, 2 levels, brightness 0, and sRGB linearization off. CD supports 2/4 levels; DVD/BD use 2. Edit generation geometry in Project settings.

The main application now composes designs from layers and no longer offers the old CD/DVD ring-copy option. This independent tool processes one image and does not manage project layer layout.

The processing core `DiscImageStudio.Imaging.GrayscaleDither` still supports any ordered grayscale palette. The old `FourLevelDither` API retains compatibility only for tools such as existing codeword probes; the independent window and CLI both use two-level black and white.

## Command line

The window and command line support Simplified Chinese, Traditional Chinese, English, Japanese, and Turkish; language is selected at startup. Choose explicitly with `--language zh-CN|zh-TW|en|ja|tr|system`; otherwise the `DISC_IMAGE_STUDIO_LANGUAGE` environment variable is used, falling back to the system UI language. Language selection does not change mode values, filenames, or pixel output.

```powershell
# Convert without a window; DVD two-level by default. Output must be a .png that does not already exist.
.\FourLevelDither.exe --input original.jpg --output result.png

# Select DVD or CD explicitly; both output two-level black/white 0/255.
.\FourLevelDither.exe --input original.jpg --output dvd-result.png --mode dvd
.\FourLevelDither.exe --input original.jpg --output cd-result.png --mode cd

# Compare sRGB gamma correction off and on; omitting --gamma is equivalent to none.
.\FourLevelDither.exe --input original.jpg --output result-none.png --gamma none
.\FourLevelDither.exe --input original.jpg --output result-srgb.png --gamma srgb

# Open a window with an image.
.\FourLevelDither.exe original.jpg

# Show help (--help is also supported).
.\FourLevelDither.exe -h

# Open the window in Japanese
.\FourLevelDither.exe --language ja original.jpg

# Convert without a window in Turkish
.\FourLevelDither.exe --language tr --input original.jpg --output result-tr.png
```

## Run and publish from source

Use the .NET 10 SDK from the repository root:

```powershell
dotnet run --project tools/FourLevelDither --configuration Release

dotnet publish tools/FourLevelDither/FourLevelDither.csproj --configuration Release --runtime win-x64 --self-contained true --output artifacts/FourLevelDither/win-x64
```

The self-contained `FourLevelDither.exe` includes the .NET 10 runtime. Running a non-self-contained build requires installing the .NET 10 Windows Desktop Runtime.
