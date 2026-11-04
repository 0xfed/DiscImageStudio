# Single-layer BD-R: byte and bit-period transition-density analysis

[简体中文](BLURAY_BYTE_DENSITY.md) | [繁體中文](BLURAY_BYTE_DENSITY.zh-TW.md) | [English](BLURAY_BYTE_DENSITY.en.md) | [日本語](BLURAY_BYTE_DENSITY.ja.md) | [Türkçe](BLURAY_BYTE_DENSITY.tr.md)

2026-10-24. Numerical experiments based on the supplied ISO/IEC 30190:2021, especially 13.15–13.18 and Figure 40/41, using this project's EDC, scrambling, LDC/BIS, DC, and 17PP encoding chain. Single-layer discs only.

**When targeting total transition density, bit-constructed patterns are worth using.** The best repeated-byte baseline has full-cluster densities of about **19.20% / 38.61%**; accounting for physical interleaving and DC-segment boundaries, the tested bit-period schemes reach **15.95% / 43.37%**. The endpoint gap rises from 19.41 to 27.42 percentage points, about 41%. These are digital encoding results; physical reflected brightness and black/white direction remain undetermined. Current drawing follows the user-selected **3T / 6T count bias** below, rather than these density extremes.

<a id="按-3t--6t-数量接入绘图"></a>

## Integrating 3T / 6T count mapping into drawing

Use full clusters of the same 958,272 channel bits and count the **absolute number** of complete 3T or 6T intervals between adjacent modulation ONEs. Both marks and spaces count. This is neither NRZI-level duty cycle nor a ranking by target run count / total run count.

| Default image endpoint | Scrambled-domain Q | Baseline cluster target run count | Target run fraction | Target count range over four addresses |
| --- | --- | ---: | ---: | ---: |
| Dark (0–127), favors 3T | `AA` | 265,136 | 85.4164% | 264,946–265,186 |
| Light (128–255), favors 6T | `44` | 116,575 | 62.6862% | 116,470–116,659 |

These two bytes achieve the highest respective target counts among all 256 repeated bytes at baseline PSN `0x100000`. `22` has the highest 6T fraction, 62.7744%, but only 116,402 occurrences of 6T, fewer than `44`, so count mode chooses `44`. Across `0x100000`, `0x234560`, `0x6D3A00`, and `0xCA73E0`, the mean 6T count for `44` is 116,559.5, slightly above `11` at 116,526.5 and `22` at 116,457. This does not imply it is optimal at every address.

Production `BluRayRunLengthMapping` selects `AA` / `44` at threshold 128 and allows black/white inversion. `BluRayImagePayloadMapping` samples image positions, and the streaming generator cancels scrambling to output host payload. Image edges, EDC, ECC, BIS, DC, and sync introduce other run lengths, so “biased toward 3T / 6T” does not mean pure 3T / 6T. The default dark/light assignment is an invertible image convention, not a calibration of reflected brightness on a physical disc.

These values use the current greedy DC diagnostic model; raw counts are in `Cluster.RunCounts` in `all-bytes.json`. Architecture regressions use full RUB encoding to verify count bias at both the beginning and end of the disc and check scrambling and ECC positions for mixed images. See the [Blu-ray guide](ADDING_BLURAY.en.md) for commands and usage.

## Measurement definitions

- Transition density = modulation ONE count / channel-bit count. ONE denotes an NRZI transition; the fraction of ONEs in NRZI levels cannot substitute for it.
- Q is the **target payload after scrambling**. Write `Q XOR scramblingMask` as host payload; simply repeating raw host bytes loses the target pattern.
- Pure 17PP results exclude DC, ECC, BIS, and sync; they use an independent standard table and exact finite-state cycle calculation.
- Full clusters are generated from 65,536 host bytes and count `496 × 1932 = 958272` channel bits, including EDC, LDC parity, BIS, DC, and sync. They exclude run-in/out/Guard3 but use the level and DSV at the end of run-in.
- Default PSN is `0x100000`; address flags and user control data are zero. Candidates are also checked at `0x234560`, `0x6D3A00`, and `0xCA73E0`.
- Main results use the current greedy DC diagnostic algorithm, with future control bits temporarily zero. It does not guarantee the same decisions as a recorder. All-0, all-1, and fixed-seed random DC are additional sensitivity tests, not prescriptions for physical recording.

## Baseline for all 256 bytes

Continuously repeating one Q byte yields only four steady-state densities without DC/ECC:

| Pure 17PP density | Byte count |
| --- | ---: |
| 1/6 = 16.6667% | 28 |
| 1/4 = 25% | 92 |
| 1/3 = 33.3333% | 100 |
| 5/12 = 41.6667% | 36 |

Of these, **80 bytes change density class after an odd-bit offset**. DC insertion changes the two-bit parsing phase, so this bare encoding table alone cannot rank candidates.

| Q | Pure 17PP | Full-cluster greedy | Full-cluster range over four PSNs | Test use |
| --- | ---: | ---: | ---: | --- |
| `09` | 16.6667% | 19.1997% | 19.1962–19.2080% | Lowest repeated byte in the scan |
| `11` | 16.6667% | 19.4820% | 19.4447–19.4820% | Pure encoding is all 6T; long-run control |
| `22` | 16.6667% | 19.3505% | 19.3242–19.3510% | Also in the robust low-density group |
| `3D` | 41.6667% | 38.4602% | 38.4428–38.4739% | Robust high-density byte |
| `7A` | 41.6667% | 38.5486% | 38.5409–38.5525% | Robust high-density byte |
| `9E` | 41.6667% | 38.6143% | 38.6014–38.6260% | Highest repeated byte in the scan |
| `00` | 33.3333% | 32.8615% | 32.8412–32.8668% | Common-input control |
| `FF` | 33.3333% | 32.5332% | 32.5332–32.5639% | No large density gap from `00` |

`55/AA` have full-cluster densities of about 32.30% / 32.39%, also unsuitable as density endpoints. Without canceling scrambling, all 14 tested constant-host candidates fall around 29.37–29.67%, largely erasing the original differences.

Complete results: [CSV](../artifacts/bluray-byte-density/all-bytes.csv), [readable table](../artifacts/bluray-byte-density/all-bytes.md), [256-byte density plot](../artifacts/bluray-byte-density/all-bytes-density.png).

## Finer-grained attainable patterns

17PP encodes variable-length groups of 2/4/6/8 input bits, with a preceding modulation bit and special substitution rules. Bytes are containers and cannot be treated as independent codewords.

| Raw periodic bit string | Period length | Byte-packing example | Pure 17PP density | Steady-state runs |
| --- | ---: | --- | ---: | --- |
| `0001` | 4 | `11` | 16.6667% | All 6T |
| `00111101` | 8 | `3D` | 41.6667% | 3 × 2T, 2 × 3T |
| `001111` | 6 | `3C F3 CF` | 44.4444% | 3 × 2T, 1 × 3T |
| `0000101101110010` | 16 | `0B 72` | **12.5%** | All 8T |
| `0111011110` | 10 | `77 9D E7 79 DE` | **46.6667%** | 6 × 2T, 1 × 3T |

Ordinary 17PP data runs range from 2T–8T, with at most six consecutive 2T runs. Thus long data segments have a density lower bound of `1/8` and upper bound of `7/(6×2+3)=7/15`; the 16-bit and 10-bit patterns above attain them. Sync contains 9T, so these bounds do not apply directly to arbitrary short windows containing sync.

The two extreme periods degrade to 29.1667% / 33.3333% at odd-bit phases. Both phases of 6-bit `001111` give 44.4444%, but bare NRZI duty cycle is 4/9 or its complement, requiring DC balancing. In the search through periods up to 16 bits, it is the highest-density pattern robust to both even and odd phases.

Packing examples only show how to represent a period. **Do not simply repeat these host bytes, or repeat Q in original payload order while ignoring interleaving.** For example, filling Q with the 6-bit period in payload order yields only about 33.43–34.39% in full clusters; arranging it in physical ECC order reaches about 40.64–40.75%.

## DC compensation and full-cluster results

Each frame has 155 ECC bytes, totaling 1240 raw bits. There are first 25 data bits, then a DC bit is inserted every 45 data bits, 28 in total. Let `j=8×EccColumn+bitWithinByte`, with bit 0 as MSB:

```text
DCbefore(j) = j < 25 ? 0 : 1 + floor((j - 25) / 45)
```

Reserve DC slots:

```text
index = RecordingFrame × 1268 + j + DCbefore(j)
Qbit = pattern[(index + phase) % period]
```

Restart at DC data segments:

```text
index = j < 25 ? j : (j - 25) % 45
Qbit = pattern[(index + phase) % period]
```

After DC insertion, each segment starts on an even input bit; restarting the period preserves the two-bit parsing phase. The slot-reservation scheme only reserves positions; the encoder/recorder still chooses DC bit values. EDC, BIS, and parity also remain normally generated rather than arbitrarily controlled.

After constructing Q at these physical positions, scatter it back into the corresponding payload bytes, then cancel scrambling. The existing `Map(luminance, position)` provides RecordingFrame/EccColumn; generate 8 bits individually in each call, without restricting the return value to a fixed byte.

| Recommended bit pattern | Construction | Phase | Full-cluster greedy | Range over four PSNs | Range for three other DC strategies |
| --- | --- | ---: | ---: | ---: | ---: |
| 16-bit `0000101101110010` | Restart at DC segments | 2 | **15.9500%** | 15.9405–15.9500% | 15.9278–17.0720% |
| 6-bit `001111` | Reserved DC slots | 3 | **40.8172%** | 40.8158–40.8315% | 41.0010–41.4603% |
| 10-bit `0111011110` | Restart at DC segments | 4 | **43.3670%** | 43.3662–43.3713% | 42.1870–43.3904% |

Phase starts at 0 and denotes the periodic offset added to the coordinates above. Coordinates restart at the origin for each cluster. These phases apply only to this construction, not to an arbitrary shift of the entire physical signal by the same bit count.

Across all phases, the 6-bit scheme's full-cluster density is 40.7929–40.8172%, so phase has little effect. The extreme 16/10-bit schemes still require the selected phases; a wrong phase can return to about 30%–32%. DC sensitivity intervals cover only these tested strategies and are not guaranteed ranges for arbitrary recorders.

In the recommended low pattern, **52.65% of complete inter-transition runs are 8T**; in the recommended high pattern, **79.14% are 2T**. This provides a more specific physical-disc comparison than mean density alone. Both have about 50% NRZI-level occupancy in the greedy model, so bit values 0/1 cannot be interpreted directly as visual black/white.

![Full-cluster density comparison](../artifacts/bluray-byte-density/fine-pattern-comparison.png)

## First-round physical-disc candidates

Compare these solid patches on the same disc at the same recording speed and viewing conditions, repeating them at inner, middle, and outer radii:

1. Sparse endpoint: 16-bit period, restart at DC segments, phase 2.
2. Dense endpoint: 10-bit period, restart at DC segments, phase 4.
3. Robust high-density control: 6-bit period, reserved DC slots, phase 3.
4. Byte baselines: `09`, `11`, `9E`; `00` can serve as an ordinary-input control.

First compare large patches for actual light/dark direction and stability when rotating the viewing angle, then test regional mixing and image mapping. Alternating the two patterns byte by byte changes encoding context; their density or brightness must not be assumed to equal a linear average of the endpoints. These density-extreme candidates have not been burned to physical discs and are not used for the current default black/white mapping.

## Validation and reproduction

- The independent Python implementation transcribes standard Figure 41 and matches C# bit for bit on 256 four-byte vectors and 298 periodic vectors.
- Exhaustively searched all 131,068 raw bit strings of length 2–16, each with two preceding-bit values, for 262,136 exact cycle calculations; checked 14,694 two-bit-rotation equivalence classes. The phase-robust optimum above is limited to this search scope.
- The full-chain scan covers all 256 bytes. Synthetic frames additionally check 8 DC masks × 31 sync positions × 2 preceding-frame terminations for each byte.
- Fine patterns cover 176 pattern/layout/phase combinations, plus 120 address-check clusters. Every cluster verifies that 65,536 controllable Q bytes match the target at actual ECC positions.
- Cluster length, run-count conservation, and RLL interval checks pass; fast cluster scans agree with the full RUB path. Pure data has no complete runs outside 2T–8T; clusters containing sync allow 9T.

See [BluRayByteProbe](../tools/BluRayByteProbe/README.en.md) for tools and commands. Raw results are in `artifacts/bluray-byte-density/`; the artifacts directory is ignored under repository rules, and scripts can rebuild the results. Production already uses the separate 3T / 6T count mapping listed on this page.
