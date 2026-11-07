# Calibration

[简体中文](CALIBRATION.md) | [繁體中文](CALIBRATION.zh-TW.md) | [English](CALIBRATION.en.md) | [日本語](CALIBRATION.ja.md) | [Türkçe](CALIBRATION.tr.md)

Calibration has three stages: **generate a calibration disc → rectify the photograph → calibrate the curve**, corresponding to the three cards in the right column. The photograph only needs to show the physical outer edge, center hole, and one cross arm or wide-sector boundary clearly. Adjust the lighting to improve visibility; there is no requirement to blacken the disc or position it squarely.

## Calibration pattern and output

Set the drawing inner/outer radii, sector count, linear velocity or bit length in Project settings. Then open “Create calibration disc” in the Calibration workspace and use “Create target from current parameters.” The default fine-cross pattern uses the built-in `target_slim.png`: nine thin concentric circles and a cross. **Scale the entire original image to the generation outer radius first, then clip the burning band between the inner and outer radii**, using the same coordinate mapping as ordinary DVD image generation. The original image's left/right and top/bottom boundaries correspond to the negative and positive generation outer radius. Circle positions and line widths scale proportionally with the outer radius; the inner radius clips the central region. Concentric circles are for observation only; parameter solving uses only the selected curve.

The 120 mm disc background in the interface is for preview, not a fixed physical size of the original image. The 12 cm / 8 cm physical disc size chosen for photo rectification also serves only to recover the photo scale; it does not change the saved pattern generation mapping.

Creating a pattern fixes the generation parameters and CD interleaving setting and clears the previous calibration disc's photograph and inputs. Later parameter edits on the main page do not change the meaning of an existing record. Use the record saved when each disc was generated.

- CD outputs `.wav` (with `.cue`) or `.raw`; DVD and BD output a drawing-only `.iso`. A BD target covers the full single-layer data area and records the drawing start/end sectors, 3T/6T polarity, and ISO timestamp.
- File generation also saves a same-name `.calibration.json`. Streaming burn saves the calibration record first, then opens the Burn workspace with this pattern fixed as the source, where you select a device and confirm.
- Preview, PNG export, file output, and streaming output share the saved pattern and generation coordinates; the fine cross preserves the original grayscale edges. PNG exports the clipped pattern at the original 2400 × 2400 pixels, without preview background margins or rescaling a preview screenshot. For the actual burn, use this page's generation or streaming entry point to keep the pattern consistent with its saved generation parameters.

### Coarse calibration when the cross is faint

In Calibration, expand **Create calibration disc**, select **Use wide sectors for coarse calibration**, then click **Create target from current parameters**. The current-target label identifies the pattern actually saved and used for the next burn; changing the checkbox alone does not replace an existing target.

The coarse target contains four alternating 90-degree black/white sectors with no guide circles. Its four radial boundaries use the same curve model as the fine cross. Broad filled regions retain a much larger local texture duty cycle when geometry error winds the boundaries into spirals. The original fine cross and its nine circles remain the default.

Trace one continuous black/white boundary from inside to outside, with enough intermediate points to follow every turn; do not switch to a neighboring edge. After applying a coarse fit, create a new fine-cross target for refinement. Use this page’s generation or streaming controls so the selected pattern and its saved parameters stay together.

This improves visibility in tested geometry simulations; it does not correct an arbitrary offset, guarantee a straight line, or establish physical-disc contrast. Tests cover ±0.1 mm inner-radius errors, ±512-sector offsets, and averaging over a 0.1 mm radial observation footprint. Physical burning still needs hardware validation.

### Calibration from measured circle diameters

In Calibration, expand Create calibration disc, select **Use concentric circles for diameter calibration**, then create a target from current parameters. The new target contains 7 concentric lines without a cross. Line width is normally 0.15 mm and shrinks for a very narrow drawing band. Rings occupy 6%, 18%, 34%, 50%, 66%, 82%, 94% of the saved drawable recording progress and do not depend on angular phase.

1. Generate or stream-burn from this page and keep the matching `.calibration.json`. You can also reopen a matching original-cross record and select **Use measured ring diameters** to use its existing complete circles without seeing the cross. Wide-sector targets have no circles.
2. Number complete circles from the innermost outward; do not count recording-area edges. Measure diameters through the centre, from line centre to line centre on the opposite side. Generated diameters in the table only help identify circles; enter actual measurements in mm, with a decimal point, and leave unmeasured rows blank.
3. Measure at least 3 circles, including inner and outer circles spanning at least 50% of recording progress. Two known positions determine two endpoints in the ideal model; additional circles check consistency.
4. Click **Calculate radii from circles**. Review the estimated radii, individual diameter residuals, and RMS/maximum residuals. The default maximum allowed diameter residual is 0.10 mm; this is a fit-consistency threshold, not measurement accuracy.
5. Check circle numbers and measured diameters, select the confirmation checkbox, then apply calibration parameters and save the project. Editing a diameter or tolerance invalidates the old result and confirmation; reopening a record also requires a fresh calculation.

The calculation holds the saved sector count, linear density and pitch shape fixed, fitting only the inner and outer radii; it does not fit angle or a new pitch shape. Constant pitch approximately satisfies `r(q)^2 = ri^2 + q*(ro^2-ri^2)`; the program refines the result using the exact saved track model. Synthetic checks cover a 2 mm inner-radius error, a 1 mm outer-radius error and diameter readings rounded to 0.01 mm; they cannot guarantee physical-disc accuracy. Instrument error, line width, mistaken circle identities and unknown pitch variation affect results.

### Visual selection without measuring diameters (experimental, DVD)

Choose **Visual selector (experimental, DVD)** under Create calibration disc, choose a test range, and create a new target. Generate its ISO or use streaming burn from this page. The target records the current inner/outer radii, sector count and test range, using constant pitch for this coarse search. It does not change the project until you apply an estimate.

The disc has two groups of 21 circular test bands. Each band tests a different local radius correction. In each group, look for the black/white boundary with the least winding. Count bands from inside to outside: **I1–I21** in the inner group and **O1–O21** in the outer group. A wider gap separates the groups; additional spacing around bands 5, 10, 15 and 20 helps counting. The numbers on the app preview are guides only and are not burned onto the disc.

Select the two band numbers and calculate the estimate. If a choice is unclear, do not guess. An end band (1 or 21) prevents applying the result because the match may lie outside the tested range; create a new target with a wider range. Otherwise, **Apply as starting values** writes estimated inner/outer radii and resets the DVD pitch corrections to zero. Create a narrower-range selector or a wide-sector target from these parameters for refinement. The range describes local-radius corrections, not a guaranteed error bound on either endpoint. Existing burned discs must always be interpreted using their original saved target and range.

This is an experimental visual comparison, not a precise physical measurement. Two selections give two local-radius hypotheses under a constant-pitch model. Adjacent bands can look similar, and unknown pitch variation, physical bit length, drive offsets and limited optical contrast affect the estimate. Neither the preview nor the numerical result establishes naked-eye readability or final alignment. No successful burn or fixed number of attempts is guaranteed.

Both ISO output and streaming burn sample the selector directly at DVD payload codeword positions, including the intervening headers, ECC and synchronization fields in the recording position. A normal raster image can alias its phase, so PNG export is unavailable for this pattern. The preview is filtered for display and includes labels; it is never used as burn input. Saved sessions retain both band choices but require a fresh calculation after reopening. Changing either choice, pattern or range invalidates the previous estimate.

## Step one: photo rectification

1. Import a photograph or scan and select a standard 12 cm disc or an 8 cm mini disc. The image is reduced to a size suitable for interaction and stored in the record.
2. Drag the two groups of control points to the **physical outer edge of the disc** and the **center-hole edge**. Initial contours are starting points for adjustment, not a claim of accurate detection. Do not use the recording-area boundary, a transparent plastic ring, or a shadow as the physical edge.
3. Click “Calculate rectification” and inspect the front-on preview. To make corrections, turn off the preview and keep dragging the boundaries. Confirm when satisfied to enter curve calibration.

The program jointly fits the projections of two coplanar concentric circles to recover the disc center, scale, and perspective. Scale comes from the selected disc size and the standard center-hole size; manual measurement is unnecessary. The geometric centers of the two ellipses cannot directly serve as the disc center, and making just one ellipse circular does not fully remove perspective distortion.

This step does not constrain the cross to be straight or perpendicular, so it preserves the recorded curvature that needs calibration. Current rectification corrects perspective caused by the camera angle and does not separately estimate radial lens distortion. Severe wide-angle distortion, occlusion, or incorrect boundary selection reduces reliability.

## Step two: calibration from one curve

1. Click evenly along one clear radial curve, from inside to outside, covering the inner, middle, and outer regions. CD or radius-only correction requires at least 6 points; 20 or more are recommended. DVD cubic track-pitch fitting requires at least 12 points at different radii spanning 60% of the generation band's radial width. If points are so far apart that the angular change between neighbors approaches half a turn, the program rejects them and asks for more points. You do not need to match concentric-circle intersections or identify which cross arm it is.
2. Drag existing points to correct them, click blank positions to add points, and press Delete to remove the selected point. All points belong to this one curve. Rings and other cross arms do not participate in the fit; do not switch to another arm partway through.
3. Click “Solve parameters.” The program follows the curve in radial point order, fits the inner and outer radii, and treats the overall disc rotation as an independent quantity. All points confirmed visible in the photograph participate. They are not removed or truncated according to the preset recording band, and endpoints are not forced onto preset boundaries. Input points must still lie on the actual disc surface. The default automatic range starts at ±0.02 mm. If the optimum reaches a range boundary, the search automatically widens to ±0.1, ±0.5 mm, and larger ranges, avoiding treating boundary-clamped parameters as measurements. Manual ranges are under the collapsed “Search range” item and only constrain diagnostics; neither mode changes the curve model.
4. Select a candidate and inspect the entire predicted curve overlaid on the photograph. Cyan points and dashed lines are the photographed inputs; the orange line is the parameter prediction and spans the actual traced range. It should preserve the curvature predicted by the parameters and be compared with the photo points, rather than treating an ideal straight line or an arbitrary spline through the points as a parameter fit. Matching a few points does not prove there is no extra turn between them; add points at visible bends or large gaps. Points clearly deviating from the prediction are marked red and included in the “Large deviation” legend. The status line gives the average deviation, maximum deviation, and automatically expanded range. Candidates with average deviation above 0.35 mm or maximum deviation above 0.8 mm cannot be applied; correct the red points or add points first.
5. Apply the parameters after confirming that the predicted curve matches. Applying updates only the corresponding CD / DVD / BD parameters (BD writes back both radii, total sectors, and polarity), retains the original calibration disc snapshot, and does not write the photo rotation into the burning start angle. After applying, the project header on the left lights up with “Parameters updated,” indicating that this project's geometry has changed.

Wheel zoom and right-button panning only change the view, not the input data. “Burn pattern” is for inspecting the original image; photograph points cannot be entered on that preview.

The generation inner and outer radii are reference parameters of the track model, not hard limits on the observed photo region. Single-curve fitting computes phase continuously over the observed range. If observed points extend slightly outside the preset band, do not delete real points or widen the parameter search merely to make the program accept them. Actual burning still uses the configured inner and outer radii as output boundaries.

DVD supports track pitch varying as a cubic polynomial of normalized radius `x=(r-ri)/(ro-ri)`: `p(x)=p0*(1+c1*x+c2*x²+c3*x³)`. The full model has four coefficients: `p0`, `p0*c1`, `p0*c2`, and `p0*c3`. Once capacity, bit length, and both radii are fixed, the total track-length constraint automatically determines `p0`, so fitting solves the three relative shape coefficients and two radii. Zero coefficients mean constant track pitch. Generation snapshots, prediction overlays, and applied parameters retain these coefficients. BD uses constant track pitch (nominally 0.320 µm) and continuously extends the observed curve at positive radii. Because it has far more turns per millimeter than CD/DVD, a narrow search may yield an interior solution with a high residual. The UI uses a residual threshold to prevent applying it and retains the candidate for inspection.

When the point count or coverage is insufficient, the program preserves the existing pitch shape, fits only radii, and requests more points. Cubic fitting still requires checking the entire predicted curve, especially for an extra turn between points. Obtaining a candidate does not mean the real disc's four physical coefficients have been uniquely measured. At fixed linear density, photo scale, radii, and pitch shape can compensate for one another, and the model may absorb residual lens distortion. The UI retains mismatched candidates for inspection but does not allow clearly high-residual results to be applied.

## Editing and saving

Return to step one to inspect the original photograph. Changing boundaries or disc size invalidates rectification, tracing, and fitting, requiring both steps to be completed again. Changing any curve point or the search range also invalidates the old fit and contour confirmation.

Calibration records save the generation snapshot, reduced original photograph, boundary control points, disc size, and all curve points; they do not depend on the original photo path. Reopening restores rectification from the saved boundaries so you can continue tracing or recompute. Fit candidates must be recalculated.

New records use schema 9 and retain the pattern, measured circle diameters, measurement method, diameter tolerance, and optional visual-selector range and band choices. Schema 8 circle/fine-cross/wide-sector records, schema 7 fine-cross/wide-sector records and schema 6 fine-cross records remain readable. Older comb records are not migrated. Use each burned disc with its own original record.

## Validation scope

Automated checks cover consistency of the built-in original image and pixel sampling; CD/DVD/BD file and streaming output consistency; BD drawing ranges and polarity; recovery of synthetic perspective; degenerate boundaries and cancellation; single-curve solving; full curves beyond the preset band; prediction direction and curvature; interactive coordinate transforms; point dragging and insertion; session restoration and invalidation rules. Burning tests write only memory streams and do not access recorders.

Software and synthetic tests cannot establish real-disc visibility, lens condition, point-placement accuracy, or final physical calibration accuracy. Check the entire predicted curve against your own photograph before applying it.

Assisted calibration now has coarse, boundary and fine stages and can run independently of a drawing project. Confirm the disc capacity, then create the matching target for each blank disc. Analyze every recorded disc with its own saved generation record and photograph. [Calibration after the upstream integration](CALIBRATION_TUTORIAL.en.md).
