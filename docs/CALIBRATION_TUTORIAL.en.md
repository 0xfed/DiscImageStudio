# Calibration after the upstream integration

[简体中文](CALIBRATION_TUTORIAL.md) | [繁體中文](CALIBRATION_TUTORIAL.zh-TW.md) | [English](CALIBRATION_TUTORIAL.en.md) | [日本語](CALIBRATION_TUTORIAL.ja.md) | [Türkçe](CALIBRATION_TUTORIAL.tr.md)

Assisted calibration now has coarse, boundary and fine stages and can run independently of a drawing project. Confirm the disc capacity, then create the matching target for each blank disc. Analyze every recorded disc with its own saved generation record and photograph.

Measured diameters, wide sectors and the DVD visual selector remain available. New records explicitly identify their artwork lineage. This fork’s schema 7–9 records without an artwork field retain their original patterns. Do not convert old upstream records by manually changing version numbers.

Coarse calibration uses three wide bands and angular marks to estimate radii for the next disc. Boundary calibration traces separate, clear black-white edges; fine calibration traces one cross arm and can fit cubic pitch. Check the predicted curve before applying each result and save the record.

Rectify photographs against the physical disc edge and center hole. A 12 cm disc has a physical outer radius of 60 mm; the recording radius is a different measurement. Use a new blank disc for each iteration. Recalculating the same photo does not create another physical measurement.

## Verbatim AZO DVD-R example records

These records contain photographs, rectification parameters and measured points. They illustrate one physical-disc workflow and do not establish accuracy for other media, drives or batches.

- [AngleSweepBands](examples/verbatim-azo-20261104/calibration-4f0228e6.calibration.json)
- [WavyBoundary](examples/verbatim-azo-20261104/calibration-3f2ce8c2.calibration.json)
- [ReferenceCross](examples/verbatim-azo-20261104/calibration-14580aac.calibration.json)

[The complete upstream tutorial in Chinese and its historical measurements are available at this fixed revision.](https://github.com/JiaFeiMiao-K-Cat/DiscImageStudio/blob/c720fd45b76dc4d9257316474dd646a91589ffdb/docs/CALIBRATION_TUTORIAL.md)
