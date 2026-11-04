# Review of the CD / DVD generation physical model

[简体中文](PHYSICAL_MODEL_AUDIT.md) | [繁體中文](PHYSICAL_MODEL_AUDIT.zh-TW.md) | [English](PHYSICAL_MODEL_AUDIT.en.md) | [日本語](PHYSICAL_MODEL_AUDIT.ja.md) | [Türkçe](PHYSICAL_MODEL_AUDIT.tr.md)

Review date: 2026-09-21. The subject was the current `DiscImageStudio-GitHub` workspace, including the uncommitted calibration implementation, based on commit `6c55a18`. This review only compared standards, audited source code, and performed independent numerical checks. It did not change generators, presets, or burning behavior.

## Conclusions

No calculation errors were found in the basic spiral formula or DVD sector length. However, the current model cannot precisely predict angles on a physical disc from nominal standard parameters alone: the default CD parameters have a geometric consistency problem; single-line calibration with fixed linear density absorbs density errors into the radii; and the finite-stream boundaries and reference timing of CD interleaving are absent from the geometry model. A check that the recording medium matches the generation model is also missing.

This audit alone cannot identify the sole cause of curvature on a particular physical disc. Directly changing the DVD bit length to another nominal value should not be treated as a fix.

## Standards and scope

- Official [ECMA-359, first edition, 2004](https://ecma-international.org/wp-content/uploads/ECMA-359_1st_edition_december_2004.pdf): single-layer DVD-R; primarily §§10.7–10.9, 18–23, and 24.2. Original PDF pages 28, 29, 52, and 56 were checked.
- Official [ECMA-267, third edition, 2001](https://ecma-international.org/wp-content/uploads/ECMA-267_3rd_edition_april_2001.pdf): DVD-ROM; primarily §§10.6 and 18–21. It cannot replace all rules for recordable media.
- Official [ECMA-130, second edition, 1996](https://ecma-international.org/wp-content/uploads/ECMA-130_2nd_edition_june_1996.pdf): CD-ROM geometry and a common CIRC encoding reference; §§8.6, 11.3–11.4, 19.4, and Annex C. An audio track is not equivalent to a CD-ROM data track.
- Official [ECMA-394, first edition, 2010](https://ecma-international.org/wp-content/uploads/ECMA-394_1st_edition_december_2010.pdf): CD-R; multispeed recording, pregrooves, and Red Book compatibility; §§1.2 and 2.4–2.5. This document specifies some geometry requirements by reference to the Red Book; it does not directly reproduce the complete CD geometry table.

The key ECMA-359 constraints follow. All are standard requirements, not measurements of a particular disc:

| Item | Constraint | Clause |
|---|---|---|
| Data start radius | 23.9–24.0 mm | 10.7.1.2; the original specifies a 48.0 mm diameter with −0.2/+0.0 mm tolerance |
| Data end radius on a 12 cm disc | At most 58.0 mm | 10.7.1.2 |
| Average track pitch | 0.74 ± 0.01 μm | 10.8 |
| Local track-pitch deviation | At most ±0.03 μm relative to 0.74 μm | 10.8 |
| Average channel bit length | 133.3 ± 1.4 nm | 10.9 |
| First data-area PSN | 0x030000; physical sectors are continuous without gaps | 24.2 |

ECMA-267 has a different lower bound for the data start diameter from DVD-R: §10.6.1 specifies 48.0 mm with −0.4/+0.0 mm tolerance. The start tolerances of the two standards must not be mixed.

## Confirmed correct aspects

DVD has `13 × 2 × (32 + 1456) = 38688` channel bits per sector and 619008 bits per 16-sector ECC block. The value in `DvdEccBlockEncoder.cs:8` is correct; no general length omissions were found in sector offsets, sync frames, or PO-row interleaving.

The current track lengths are:

```text
CD:  L = (N / 75) × v                         [mm]
DVD: L = N × 38688 × channelBitLengthNm × 10^-6 [mm]
```

`CdDiscParameters.cs:59–75` uses a constant-pitch area approximation; `ArchimedeanSpiral.cs:33–70,115–120` uses exact arc length. The approximate relations are `pitch = π(ro²−ri²)/L` and `θ = 2L(r−ri)/(ro²−ri²)`. The whole-disc angular difference between the CD approximation and the exact spiral is about 0.000135°, which cannot explain visibly pronounced curvature. No errors were found in DVD exact-arc-length inversion, nanometer-to-millimeter conversion, or the default image-coordinate direction either.

DVD-R linking applies to particular incremental append situations; §24.2 explicitly requires continuous sectors. There is no basis for uniformly appending a supposed hidden gap to every ECC block.

## Finding 1: the default 80-minute CD preset combines inconsistent parameters

`src/DiscImageStudio.Core/Presets/built-in-disc-presets.json:6–12` combines 359849 sectors, 24.5–56.8 mm, and 1200 mm/s. Inverting the actual generation formula gives:

| Preset | Implied track pitch (μm) |
|---|---:|
| Default 80-minute CD | 1.432856 |
| Default 74-minute CD | 1.548384 |
| Ritek cyan CD preset with measured radii | 1.492598 |
| Verbatim CD preset with measured radii | 1.500257 |
| Default 12 cm DVD | 0.739837 |
| Verbatim DVD-R preset with measured radii | 0.738304 |

ECMA-130 §11.3 specifies a track-pitch range of 1.5–1.7 μm; §11.4 specifies a reference scanning velocity of 1200–1400 mm/s. The default CD80 combination clearly cannot serve as a general precise preset conforming to this geometry range. Its 24.5 mm start also cannot be directly equated with the start of the user data area in §8.6 (24.8–25.0 mm).

This checks a software parameter combination; it does not establish that a user's CD-R is noncompliant. Presets labeled as measured still fix the velocity at 1200 mm/s; that does not mean the linear density was measured. User-measured radii must not be arbitrarily changed merely to fall within a standard range.

## Finding 2: standard tolerances far exceed the cumulative angular accuracy needed for drawing

`ImageTargetMapper.cs:58–77` and `FastDispersionImageWriter.cs:345–365` derive constant pitch from capacity, two radii, and a fixed bit length. The default DVD parameters produce about 45956.06 turns. The standard permits local pitch variation, and 58 mm is an upper bound, so these inputs can only define an initial model.

Numerical sensitivity, comparing models with one input changed and all others fixed:

- A 10 ppm DVD bit-length change alters the whole-disc cumulative angle by about 165.44°.
- Changing DVD bit length from 133.33 to 133.3 nm alters the cumulative angle by about 10.34037 turns.
- Increasing either the inner or outer DVD radius by 0.001 mm alters the cumulative angle by about −201.76°.

These values are model phase differences, not measured physical-disc errors. 133.33 nm lies within the standard tolerance; 133.3 nm is not the exact real bit length of every disc either. Blindly replacing a nominal value may substantially change the generated result.

## Finding 3: a single radial line cannot independently identify radii and linear density

`CalibrationTarget.cs:468–473` fixes linear velocity / bit length; `CalibrationCrossFitter.cs:73` fits only two radii and an overall rotation. The current workflow does not use source-radius correspondences from concentric circles.

Independent counterexample: the generation model is DVD `(ri=24, ro=58, bit=133.33)`, and the assumed real model is `(24,58,133.3)`. Keep the fitting bit length fixed at 133.33, change only the candidate radii to about `(24.00540257,58.01305620)`, and adjust the overall rotation. This reproduces almost exactly the same curved line over 25–57 mm (maximum phase residual about 2.04×10^-10 rad in a 2049-point exact-arc-length calculation). The candidate lies within the default ±0.02 mm search range. This is a forward check of an analytically constructed candidate, not a claim that the current optimizer must return it.

A low residual may therefore merely explain bit-length error as radius error. The feature can obtain useful compensation parameters, but the result should be understood as “equivalent parameters under a fixed-linear-density assumption.” Identifying real physical radii and density requires rings with known source radii, known data positions, or independent boundary measurements. Simply freeing another fit parameter cannot resolve the missing information.

## Finding 4: CD interleaving compensation has finite-stream boundary effects; the physical timing origin remains unverified

The delay table in `CddaInterleaver.cs:8–16` matches the data paths of ECMA-130 Figure C.4; it is not erroneous repeated interleaving. An independent label simulation passes the currently compensated PCM through the 24 data paths of standard forward CIRC: all 20856 steady-state data positions in a 10-sector test match, but the combined result delays the entire target pattern by 111 frames.

Two issues must be distinguished precisely:

- **Confirmed finite-stream behavior:** initialization produces 1332 zero placeholder bytes; 1332 target bytes distributed across the final 108 frames are not emitted. Flush in `CddaInterleaver.cs:46–55` handles only the sector tail and does not drain the ring buffer for whole-sector input. This affects pattern boundaries.
- **Conditional physical offset:** 111 frames is the combined delay under the reference timing of Figure C.4, about 15.102 ms or 18.122 mm of arc length at 1200 mm/s. If the recording chain's timing origin does not cancel it, the angular difference varies with radius and cannot be removed using the start angle alone. Its relation to ATIP, INDEX 01, and the physical-disc data start has not been measured, so it cannot be described directly as a proven physical offset.

`CdTrackGenerator.cs:229–246` samples globalByte without this timing, and geometry calibration likewise excludes this encoding stage. The existing compatibility document also states that preview does not simulate complete CIRC/EFM. End-to-end physical timing verification is needed; agreement between generation and preview based on the same model does not prove that disc positions are correct.

## Finding 5: recording media are not matched to the model

`WindowsImapiBurner.cs:151–166,377–390` checks only IMAPI support, blank status, and capacity; it does not query the actual physical medium type. The generator nevertheless uses a single-layer ID (`DvdDataFrameBuilder.cs:41`) and a single-layer, unidirectional spiral.

This lacks proactive rejection of different physical formats such as dual-layer DVD and DVD-RAM. IMAPI's ability to write a medium does not mean the current drawing model applies; whether writing can actually start still depends on the device and state. Dual-layer layer switching, second-layer direction, and bit length are not modeled.

Microsoft's [CurrentPhysicalMediaType](https://learn.microsoft.com/en-us/windows/win32/api/imapi2/nf-imapi2-idiscformat2data-get_currentphysicalmediatype) and [physical media enumeration](https://learn.microsoft.com/en-us/windows/win32/api/imapi2/ne-imapi2-imapi_media_physical_type) can support a matching check.

## Recommended correction order

1. Add an implied-pitch check to presets, distinguishing standard initial values, measured values, and compensation-derived values; prioritize the default CD80 combination. Specify which data positions the start and end radii represent, and separate the drawing crop range from track anchors.
2. Verify the joint timing of CD interleaving, the track start, and the recording chain; then correct sampling offsets and head/tail handling while keeping the final sector count unchanged.
3. Add independent radial or data-position constraints to calibration. If measured residuals still show systematic local curvature, evaluate variable-pitch / phase-correction models.
4. Before writing, check that the actual media type and capacity match the selected model.

This review performed no physical burning or optical-brightness measurements. Verification included original standard text and pages, formula recalculation, exact-spiral numerical comparisons, a parameter non-identifiability counterexample, and CIRC data-path label simulation. These checks are not claimed to constitute a complete optical simulation or certification of physical-disc accuracy.

Audit reproduction materials are in `tmp/standards-audit/` at the workspace root: `circ-audit.py`, `circ-audit-result.json`, `geometry-audit.py`, and `geometry-audit-result.json`, plus extracted text and key original pages from two user-provided PDFs.
