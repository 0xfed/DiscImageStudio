# Fixed calibration reference image

[简体中文](README.md) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [日本語](README.ja.md) | [Türkçe](README.tr.md)

`target_slim.png` is an unchanged copy of the image specified by the user, 2400×2400 RGB. File SHA-256:

```text
c98ffe87db7cf9c3f132352814e1ce611c44f704de5b7212da4083188257c829
```

The original image's three color channels are identical pixel by pixel. `target_slim.gray.gz` stores its unbinarized 8-bit grayscale pixels in row order, compressed with GZip. After decompression it is 5,760,000 bytes, with SHA-256:

```text
3d351bdfb817b943c1d1b4aedc97fc4f0a111e7e1537748f70cb5b180ba96852
```

The image is scaled to the generation outer radius, using the same pixel coordinate conventions as the ordinary image encoders: the DVD pixel-center reference is `1199.5`, and the CD reference is `1200`. Coordinates relative to the disc center are
`((x-centre)*outerRadius/centre, (y-centre)*outerRadius/centre)` mm, with Y pointing down. The original reference radii of the 9 circles are
490, 570, 650, 730, 810, 890, 970, 1050, and 1130 pixels. Line width is approximately 10 pixels; actual dimensions in millimeters scale proportionally with the generation outer radius.

Encoding, preview, and PNG export share this image: scale to the outer radius first, then clip the drawing area to the inner and outer radii. The preview's 120 mm disc background does not determine the image encoding scale. The image has not been parsed and redrawn. `CalibrationEncoderTests` checks the original PNG hash, every grayscale pixel, native previews and exports, and consistency between CD/DVD files and streams.
