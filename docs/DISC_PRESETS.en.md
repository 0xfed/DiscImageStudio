# Editable disc presets

[简体中文](DISC_PRESETS.md) | [繁體中文](DISC_PRESETS.zh-TW.md) | [English](DISC_PRESETS.en.md) | [日本語](DISC_PRESETS.ja.md) | [Türkçe](DISC_PRESETS.tr.md)

## Usable capacity and applying saved presets

Geometry sectors determine the complete spiral; optional `usableSectors` limits output length, filesystem capacity and the usable outer radius. Explicit values receive no additional reserve. For legacy presets/projects without the field, CD defaults to total minus 2, DVD to total minus 608 rounded down to 16, and BD rounds down to 32. Built-in presets provide explicit usable limits. DVD geometry sectors need not be aligned; output must be aligned.

Saving a preset in an existing project immediately applies it, selects the new preset and synchronizes measured preview radii/pitch. Cancelling the settings dialog afterwards retains the saved parameters but discards later unconfirmed edits. Renaming alone preserves manual measurements. Saving from New Project leaves the current project intact; same-name presets retain distinct IDs.

Disc presets have two layers:

- The built-in layer ships and updates with the application and holds official presets.
- The user layer is stored in the following file and holds only user additions, overrides, and disabled settings:

```text
%LOCALAPPDATA%\DiscImageStudio\disc-presets.json
```

On first launch, the application adds three editable presets to the user layer: “Custom CD parameters,” “Custom DVD parameters,” and “Custom Blu-ray parameters.” Built-in presets are not copied into the user file, so parameter corrections and new presets from application updates take effect automatically.

Add user presets from the project settings dialog: “Save these parameters as a preset” first confirms the name in a separate window, then appends the current parameters to the user layer. The new-project template list shows every non-manual preset in the catalog, grouped under “Recent,” “CD,” “DVD,” and “Blu-ray.”

The project settings dialog also lists presets for the current disc type. Selecting another preset replaces the dialog's geometry parameters, so a confirmation appears before switching; cancelling preserves the original parameters and selection. “Custom” presets in both new-project and project settings can be deleted from their context menu; deletion immediately updates the user layer and refreshes the list. Direct JSON edits, such as additions, renaming, or disabling entries, still require an application restart.

## Merge rules

- A user preset with a new `id` is appended after the built-in presets.
- A user preset with the same `id` as a built-in preset completely replaces it.
- `disabledCdPresetIds`, `disabledDvdPresetIds`, and `disabledBluRayPresetIds` can hide unwanted built-in or user presets.
- When built-in presets change, parameters update automatically unless a user entry with the same ID overrides them.

## Add a DVD preset

Copy an initialized user object in the `dvdPresets` array and change its `id`, name, and parameters. For example:

```json
{
  "id": "verbatim-dvd-r-azo-43533",
  "displayName": "Verbatim DVD-R AZO (43533) (measured)",
  "description": "Verified Verbatim DVD-R AZO 43533 calibration parameters: 23.9914606277–57.9648936579 mm, 2,297,888 sectors; cubic track-pitch distribution.",
  "totalSectors": 2297888,
  "innerRadiusMm": 23.991460627731737,
  "outerRadiusMm": 57.964893657925366,
  "channelBitLengthNm": 133.3,
  "pitchLinear": -3.9256854319227684E-05,
  "pitchQuadratic": 6.964795915429256E-05,
  "pitchCubic": -4.670918950137523E-05
}
```

DVD `totalSectors` must be greater than 0 and a multiple of 16 to satisfy ECC Block and streaming-burn layout requirements. `channelBitLengthNm` is a reserved field; see below.

This Verbatim DVD preset comes from the user-confirmed, verified `calibration-f2f851f7.calibration.json` and retains the full parameter precision in that record.

The optional `contributor` field records the contributor of these measured parameters. When provided, a seedling icon appears at the upper left of the preset card, with an immediate “Contributor: …” tooltip on hover. Omitting the field or leaving it empty hides the icon; whitespace-only values are rejected by validation. For example, the built-in `philips-dvd-plus-r`:

```json
{
  "id": "philips-dvd-plus-r",
  "displayName": "PHILIPS DVD+R (measured)",
  "description": "Measured PHILIPS DVD+R calibration parameters: 24.157724–57.881039 mm, 2,295,104 sectors; cubic track-pitch distribution.",
  "totalSectors": 2295104,
  "innerRadiusMm": 24.157723798864616,
  "outerRadiusMm": 57.881039159257099,
  "channelBitLengthNm": 133.3,
  "pitchLinear": -0.00015487792452410765,
  "pitchQuadratic": 0.0001962455822194293,
  "pitchCubic": -8.6683602888291885E-05,
  "contributor": "DBPolp"
}
```

`pitchLinear`, `pitchQuadratic`, and `pitchCubic` are the three relative shape coefficients of cubic track pitch. They default to 0 for compatibility with older presets. The model is `p(x)=p0*(1+pitchLinear*x+pitchQuadratic*x²+pitchCubic*x³)`, with `x=(r-innerRadiusMm)/(outerRadiusMm-innerRadiusMm)`. The complete polynomial has four terms; `p0` is calculated automatically from total track length, and the interface displays the actual μm values of all four terms. Pitch must be strictly positive throughout `0≤x≤1`; checking only the inner and outer boundaries is insufficient. Changing any coefficient switches to an unsaved custom state; reselecting a built-in constant-pitch preset restores zero coefficients.

Built-in measured DVD presets also include `verbatim-dvd-plus-r-azo-43498` (Verbatim DVD+R AZO (43498), contributor `Disappear9`), `ritek-dvd-plus-r-e-series` (Ritek DVD+R E-Series, contributor `EternalEve`), and `ritek-dvd-r-e-series` (Ritek DVD-R E-Series, contributor `bczhc`).

The parameters of `verbatim-dvd-plus-r-azo-43498` are inner radius `23.954070288547207` mm, outer radius `57.981456053451275` mm, `2,295,104` sectors, and cubic shape coefficients `-1.1055153059816393E-05`, `-1.7233535436312236E-06`, `5.55123860136712E-06`. The parameters of `ritek-dvd-r-e-series` are inner radius `24.063109184084034` mm, outer radius `58.23373375457525` mm, `2,298,496` sectors, and cubic shape coefficients `-3.2229430281730407E-06`, `1.0507210681810583E-05`, `-8.562064227346657E-06`. Both retain the full precision of their source values.

## Add a CD preset

Copy an existing object in the `cdPresets` array and change its `id`, name, sector count, and inner/outer radii. For example:

```json
{
  "id": "cd-74",
  "displayName": "Standard 74min CD",
  "description": "ECMA-130 / ISO/IEC 10149 standard velocity 1200 mm/s; 120 mm disc, about 74 minutes, 333,000 sectors (24.5–56.8 mm).",
  "sectors": 333000,
  "innerRadiusMm": 24.5,
  "outerRadiusMm": 56.8,
  "linearVelocityMmPerSecond": 1200
}
```

## Add a Blu-ray preset

Copy an existing object in the `bluRayPresets` array and change its `id`, name, and parameters. For example:

```json
{
  "id": "bd-r-ritek-10x",
  "displayName": "Ritek BD-R 10x (measured)",
  "description": "Measured parameters: inner radius 23.988221 mm, outer radius 58.023235 mm, 12,219,392 sectors.",
  "totalSectors": 12219392,
  "innerRadiusMm": 23.98822105616562,
  "outerRadiusMm": 58.023234731100764,
  "invertMapping": false
}
```

BD `totalSectors` must be greater than 0, aligned to 32, and at most the single-layer capacity of 12,219,392; the outer radius must not exceed 60 mm. `invertMapping` is the preset 3T/6T polarity, which can still be changed in the design page's tone module after applying the preset to a project.

The built-in `bd-r-verbatim-64099` (Verbatim BD-R (64099)) uses the “Verbatim 64099” preset saved in the user's `disc-presets.json`, preserving full precision: inner radius `23.987042157339335` mm, outer radius `57.987960341783236` mm, `12,219,392` sectors, `invertMapping: false`. GUI and CLI use the same parameters.

The built-in `bd-r-verbatim-64293` (Verbatim BD-R (64293)), provided by contributor `bczhc`, has the same parameters as 64099 and preserves the same precision.

The built-in `bd-r-ritek-12x` (Ritek BD-R 12x), provided by contributor `DeepAQ`, has inner radius `23.98795` mm, outer radius `58.02236` mm, `12,219,392` sectors, and `invertMapping: false`. GUI and CLI use the same parameters.

## Fixed parameters and reserved fields

Scanning velocity, start angle, image outer radius, and DVD channel bit length are fixed physical quantities in this approach. The interface provides no corresponding form fields; generation always uses 1200 mm/s (ECMA-130), 0°, 57.5 mm, and 133.3 nm.

These fields remain in JSON because older configuration files required them, and user files written by older versions must remain directly usable. These reserved values do not participate in generation calculations.

## i18n compatibility fields

The desktop application first resolves registered `displayNameResourceKey` and `descriptionResourceKey` values, falling back to existing text if a resource key is absent. Unchanged built-in names and descriptions are translated too, while names and descriptions entered by users retain their original text. Geometry descriptions saved by the application use the `DiscPreset.UserDescription` format key and are translated at display time using the preset's radii and sector count. Descriptions in older presets are also recognized if they exactly match the automatically generated format. Once a description is manually changed, its original text is preserved even if the format key remains. User presets need not specify resource keys; `fallbackLanguage` remains optional source-language metadata and does not change the UI language selection. Initial user presets also translate their default text at display time when the media family and ID match and the text has not been edited.

## Editing rules

- The current top level is `"schemaVersion": 4`, containing `cdPresets`, `dvdPresets`, `bluRayPresets`, three disabled-ID lists, and optional `fallbackLanguage`. The application also accepts `"schemaVersion": 2` and `3`; a missing BD catalog is filled from the built-in BD templates, and saving upgrades it to 4.
- Each preset group's `id` values must be unique. `__manual__` is reserved for the interface's “unsaved custom parameters” state and cannot be written into JSON.
- Only generation parameters listed in the template are allowed. Other fields are rejected to prevent mixing image layout or calibration parameters into disc presets.
- Radii, `linearVelocityMmPerSecond`, and `channelBitLengthNm` must be finite values greater than 0; the generation outer radius must exceed the generation inner radius.
- The file permits `//` or `/* ... */` comments and trailing commas in arrays.
- If the format or parameters are invalid, the application reports the problem and keeps the last successfully loaded presets; it does not automatically overwrite the user file.
