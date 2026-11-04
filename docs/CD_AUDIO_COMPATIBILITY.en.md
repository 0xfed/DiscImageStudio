# CD audio track formats, byte order, and ghost-image troubleshooting

[简体中文](CD_AUDIO_COMPATIBILITY.md) | [繁體中文](CD_AUDIO_COMPATIBILITY.zh-TW.md) | [English](CD_AUDIO_COMPATIBILITY.en.md) | [日本語](CD_AUDIO_COMPATIBILITY.ja.md) | [Türkçe](CD_AUDIO_COMPATIBILITY.tr.md)

## Disc files and data discs

Selecting a disc-files folder in a CD project's Design or Export page automatically switches to an ISO9660/Joliet data disc and changes the output extension to `.iso`. Clearing the folder path restores WAV audio output. Files can be placed at the inner or outer ring; inner is the default, and placement is saved with the project. Filesystem metadata remains at the disc start. Outer placement puts file contents at the end while drawing retains the original whole-disc coordinates rather than being compressed into the remaining area.

```powershell
DiscImageStudio.exe cd-generate --input picture.png --output cd-image.iso `
  --data-dir "D:\随盘文件" --volume-label CD_IMAGE --file-position outer
```

ISO length is `sectorCount × 2048` (sectorCount is the number of sectors). Burn it as a CD-ROM Mode 1 data disc. In-app on-the-fly burning uses the same ISO data stream; external burning software should use its image-writing function. Audio interleaving and CUE apply only to audio-track mode. Data-disc drawing separately compensates for CD-ROM scrambling and position mapping, while disc files retain their original contents.

On-the-fly CD data burning shares the DAO session and lead-out boundary checks with audio tracks, preserving the ATIP-calibrated sector count and complete file layout. The application encodes ISO payload into raw Mode 1 sectors and sets the data-track indicator. The Ritek Medical Aqua Blue preset still writes 359,845 data sectors; it does not shorten patterns or files to the smaller capacity available through the TAO interface. DAO retains the audio mode's bounded overburn check of at most 3 sectors and reports an error before writing if exceeded.

Data tracks use the IMAPI little-endian main-channel convention. Standard Mode 1 sectors receive EDC, P/Q error correction, and scrambling while preserving the byte order of the sync header `00 FF…FF 00`. The adjacent-byte swap in CD-ROM physical encoding cancels the swap for little-endian conversion. This also agrees with cdrdao's [RAW encoding and device byte-order conversion](https://github.com/cdrdao/cdrdao/blob/master/dao/dao.cc). This transport convention does not change inner/outer ISO file placement or physical drawing-coordinate mapping.

Design projection, the small tone preview, and artifact readback with valid `.iso.json` layout information mark the file area, including the filesystem, in yellow. The matching metadata records geometry, tone, and file placement; move it together with the ISO. Preview displays the drawing-payload mapping; physical results still require a disc test.

## Recommended usage

New generation tasks default to `cd-track.wav` with a matching `cd-track.cue`. WAV is uncompressed PCM: 44.1 kHz, 16-bit, stereo, with little-endian data. Keep both files in the same directory and update the filename inside the CUE when moving or renaming. CUE is optional: clear “Also export CUE file” in Export or pass `--cue false` to write only WAV.

| Burning entry point | Usage |
| --- | --- |
| cdrecord | `cdrecord dev=DEVICE speed=SPEED -audio "cd-track.wav"` (DEVICE is the device, SPEED the speed); do not add `-swab` |
| ImgBurn | Open `cd-track.cue` in “Write image file to disc” |
| In-app streaming burning | Automatically produces the same little-endian PCM without a WAV header or full temporary track |
| Old RAW workflow | `.raw` remains an output choice; use `cdrecord -audio "cd-track.raw"` without `-swab` |

The output extension determines the actual format; only `.wav` and `.raw` are accepted. Renaming old RAW does not convert it to WAV; regenerate it. The WAV companion CUE uses `FILE "cd-track.wav" WAVE`, not `BINARY`. Unicode filenames use UTF-8 CUE with a BOM, supporting spaces and Chinese. ImgBurn's author verified Unicode filename support in this CUE format and noted that related problems in early 2.5.0.0 were fixed in 2.5.1.0. [ImgBurn author's explanation](https://forum.imgburn.com/topic/13526-unicode-filename-problem-in-cue-file/)

Command-line generation example (use your own measured geometry):

```powershell
DiscImageStudio.exe cd-generate --input picture.png --output cd-track.wav --interleave true
```

`--cue` (default `true`) controls whether WAV output also writes a matching CUE; `.raw` output never writes CUE.

WAV file length is `sectorCount × 2352 + 44`; audio length, generation progress, and streaming-burn capacity remain `sectorCount × 2352`. The header occupies no disc audio sectors and does not alter pattern geometry.

## Tone (dithering, sRGB linearization, levels, and brightness)

Each CD audio byte corresponds to about 6.8 µm of disc length. At normal viewing distances, the eye integrates hundreds or thousands of bytes into one tone. Thus “only black/white patterns per byte” does not mean “only black/white in the image”; neighboring bytes' black/white proportions reproduce midtones.

| Parameter | Meaning |
| --- | --- |
| Dithering strength 0–100% | 0% uses a hard threshold of 128 (old behavior); 100% selects proportionally between adjacent levels per byte, producing midtones through spatial proportions of black/white bytes |
| Grayscale levels (CD only) | 2 levels use `0x0D` / `0xAA`; 4 levels use `0x0D` / `0x10` / `0x63` / `0xAA` (`0x0D` is black, `0xAA` white) |
| sRGB linearization (gamma) | Decodes with the sRGB transfer function before dithering; changes black/white dot density, not output levels |
| Brightness | Shifts grayscale globally over −64…+64 (in 0..255 units) |

Command-line options (accepted by both `cd-generate` and `cd-preview-warp`):

```powershell
DiscImageStudio.exe cd-generate --input picture.png --output cd-track.wav `
  --tone-dither 1 --tone-levels 2 --tone-brightness 0 --tone-gamma false
```

Defaults are `--tone-dither 0 --tone-levels 2 --tone-brightness 0 --tone-gamma false`, byte-identical to the old behavior. New projects default to 75% / 2 levels in the Design page's right-hand Tone module. Old projects without a `tone` section load at 0% and write the `tone` section on save. Export retains output options only, with a note in its output card that tone belongs to Design.

Dithering phase is determined by the **logical byte index** (`globalByte`), not screen coordinates. After delay interleaving moves bytes within the file, readback preview reconstructs using the same logical index, so generation, calibration projection, and readback show the same patterns. Calibration patterns (`GeneratePattern`) always use a hard threshold and do not undergo dithering.

The matching `.json` beside the audio track records `toneDitherStrength`, `toneLevels`, `toneBrightness`, and `toneSrgbLinearization`. Loading an external track in Preview synchronizes these values along with geometry and interleaving.

Readback preview defaults to 1600 px and 16 samples per sector, **accumulating and averaging** samples that land in the same pixel. The old fixed 900 px and 2 samples per sector overwrote earlier samples; sparse sampling left white midtone dots resembling newspaper halftones. Higher sampling density lets the same disc show continuous tone. Preview's “Design projection” initially sets measured geometry equal to project geometry, giving a neutral projection on opening a project. Differences appear only after entering measured radii.

## Confirmed issues

A user's physical-disc comparison on 2026-09-12 found that the same existing RAW worked normally with cdrecord's `-audio` and `speed`, while adding `-swab` reproduced ghosting. This confirms that existing RAW should be interpreted as cdrecord's default big-endian audio input.

File export and streaming burning previously shared exactly the same `GenerateToStream` output without byte-order conversion in streaming. However, input conventions differ:

| Input | Audio data requirements |
| --- | --- |
| cdrecord headerless `-audio` | Big-endian by default |
| cdrecord headerless `-audio -swab` | Little-endian; the option declares input order requiring a swap |
| cdrecord WAV | Parses the header and detects audio byte order automatically |
| CUE `FILE ... BINARY` | Little-endian; existing big-endian RAW cannot be used directly in BINARY CUE |
| IMAPI `AddTrack(IMAPI_CD_SECTOR_AUDIO, stream)` | Same sample layout as a 16-bit stereo 44.1 kHz WAV data area: headerless little-endian PCM |

The cdrecord behavior and BINARY definition above come from the [official cdrecord manual](https://cdrtools.sourceforge.net/private/man/cdrecord/cdrecord.1.html); IMAPI's audio sample layout comes from [Microsoft's AUDIO sector definition](https://learn.microsoft.com/en-us/windows/win32/api/imapi2/ne-imapi2-imapi_cd_sector_type). ImgBurn's author also provided a successful `FILE ... WAVE` CUE burning example. [ImgBurn WAVE CUE example](https://forum.imgburn.com/topic/25173-extract-only-session-two-from-audio-cd/page/2/)

Here `AudioSectorType = 0` is the IMAPI enum's AUDIO value, not physical Mode 0 recording. `IRawCDImageCreator::AddTrack` currently supports only AUDIO. [Microsoft AddTrack documentation](https://learn.microsoft.com/en-us/windows/win32/api/imapi2/nf-imapi2-irawcdimagecreator-addtrack)

## Why two outlines appear

The existing `CddaInterleaver` performs delay compensation for target bytes. Swapping the two bytes of each final 16-bit sample crosses bytes from different delay positions into the encoding chain. This cannot be understood as an ordinary audio-value change or canceled by swapping once before image sampling.

An integer-label permutation experiment with the existing delay table, excluding leading/trailing padding, shows that the extra sample-byte swap shifts even positions after inverse mapping by `+73` target bytes and odd positions by `-73`. This explains the double-outline mechanism. It is derived from the application's delay-permutation model, not a complete drive CIRC model or a physical-disc measurement.

The fix preserves sampling and the delay table and selects byte order at the 2352-byte sector-output boundary after compensation: old RAW is big-endian; WAV and IMAPI are little-endian. The WAV header lets external tools identify format and byte order; the streaming interface still receives only the data area under its own convention.

## Readback must undo delay interleaving

Delay compensation moves logical byte i to file index `i + offset(i % 24)`. The table stores 28×D (D=4) frames of delay lines, with 24 bytes per frame, so offsets range from 72 to 2592 (frame delay plus within-frame column rotation). Placing bytes on the spiral in file order rotates and shears the pattern. In a comparison using the same image at 60000 sectors and 900 px, about 17.5% of drawn pixels differ from the source, appearing as “slight distortion”.

Therefore `cd-preview-track` defaults to inverse mapping (`--deinterleave`, default `true`): logical byte i reads file index `i + CddaInterleaveTable.FileOffsetFor(i % 24)`, requiring a 2592-byte look-ahead window. Forward delay and `CddaInterleaver` share `CddaInterleaveTable`, preventing the two sides from drifting independently.

The trailing cost: the delay lines still hold the last few frames at file end, so these logical bytes are not written (1318–1332 bytes, less than one sector) and must be left blank on readback. This lies at the track's outermost ring, and its fraction of the full track decreases with sector count.

`cd-generate` now writes `<track>.json` beside the track (`format: cd-da-track-v1`, containing r0/r1/sector count/linear speed/start angle/image outer radius/interleaving state). Preview reads it when switching tracks and loads calibrated geometry and the interleaving flag, so tracks imported from other machines can be read back with the correct model.

## Calibration projection: source image and two geometries

`cd-preview-warp` is a quick comparison for the readback path and is the command used by Preview's “Calibration preview” mode. Its input is the source image rather than the track, with two geometries: generation geometry determines where byte i samples the image, and measured geometry determines where it is drawn on the disc (`--actual-r0`, `--actual-r1`, defaults 24.3 / 56.6 mm). Equal geometries reproduce the original image; differing ones shift the pattern according to the measured disc, exactly what radius calibration must show. Both geometries share sector count, linear speed, start angle, and image outer radius; the last three are fixed at 1200 mm/s, 0°, and 57.5 mm.

Because it reads only the source image rather than scanning the whole track, refresh cost after radius or geometry changes scales with image size instead of track size (about 845 MB for an 80-minute disc). It is suitable for trying calibration-preview parameters before confirming the final disc through readback simulation.

## Validation and limits

- RAW and WAV: for an asymmetric pattern, with interleaving both on and off, WAV data is exactly the old RAW byte sequence with each 16-bit pair swapped; length is unchanged.
- Streaming output: little-endian audio generated into a forward stream that forbids Seek/Position equals the WAV data byte for byte and contains no header.
- WAV preview: parses RIFF `fmt ` / `data`, skips unknown chunks and odd-length padding, bounds reads, and normalizes byte order. Rejects non-PCM, non-44.1 kHz, non-16-bit, or non-stereo data. Matches the corresponding RAW preview at different sampling steps. Preview still maps file bytes without simulating full physical CIRC/EFM and cannot replace a physical-disc assessment.
- Readback consistency: generate interleaved and non-interleaved tracks from the same source, then compare interleaved readback with `--deinterleave true` against file-order readback of the non-interleaved track. Pixels match except within one trailing sector of delay-line output (900 px asymmetric test; differences only in the trailing projection, while a mutation changing frame delay by 24 bytes reports 245 mismatches).
- Calibration projection consistency: generate a track with geometry `(24.5, 56.8)`, then compare `cd-preview-warp` projections (measured=generated and measured=`24.3/56.6`) pixel by pixel with `cd-preview-track` readback under the corresponding geometries. Thresholded results match exactly at 900 px. Reducing measured radii reduces the projection's centroid radius, so the two geometries cannot be accidentally swapped.
- Windows memory experiment: feed the existing `MsftRawCDImageCreator` 300 asymmetric audio sectors and scan each complete 2448-byte RAW result sector. All audio bytes are preserved unchanged; the generator does not swap on the caller's behalf. This test creates no recorder device and writes no disc.
- On 2026-09-12, a user tested a physical disc after this fix and reported no ghosting. This is a user-supplied physical-disc result; separate measured details for streaming, cdrecord, and ImgBurn were not recorded.

Microsoft's `WriteMedia` documentation does not describe its internal byte-swap implementation. These conclusions do not equate the memory experiment with observing swaps during device transfer. [Microsoft WriteMedia documentation](https://learn.microsoft.com/en-us/windows/win32/api/imapi2/nf-imapi2-idiscformat2rawcd-writemedia)
