# Practical image-generation optimization

Researched 2026-10-04. Scope: OpenAI generation and editing, with particular attention to Phraseback's flat, transparent logo. Documentation review only; no image experiments or paid API requests were performed.

## Conclusion

There are concrete ways to improve acceptance rates. Treat image generation as a measured process: specify the visual contract, test settings, inspect original files, and repair only the defects. Exact production properties need deterministic finishing when generation cannot satisfy them. This is a proposed workflow, not a claim that every output can be made perfect by prompting.

## Verified recommendations

OpenAI recommends describing the subject, composition, medium, and constraints; organizing complex prompts; assigning roles to references; and changing one thing per edit while restating preserved details. Prompt syntax itself is not a special quality mechanism. Its logo example favors simple silhouettes, balanced negative space, flat shapes, and clean alpha edges. Repeated edits can drift; composite changes when untouched regions must remain pixel-identical. Its testing guidance includes repeated requests, complete edit sequences, and cost per accepted result. [Official image-prompting guide](https://developers.openai.com/api/docs/guides/image-prompting)

That guide currently recommends Sunburst for demanding quality and Flare for speed. Both GPT Image 2.5 models expose `low`, `medium`, `high`, `xhigh`, `max`, and `auto`. Higher quality is not guaranteed to improve every prompt. Outputs above 3,686,400 pixels are experimental. [Model and parameter guidance](https://developers.openai.com/api/docs/guides/image-prompting#model-parameters)

The API offers explicit dimensions, quality, format, compression, and background controls. Transparent output requires PNG or WebP. Masks guide edits without exact boundary guarantees. GPT Image 2 always processes references at high fidelity; `input_fidelity` cannot be changed for that model. Older examples should not be copied without checking current model support. [Image-generation API guide](https://developers.openai.com/api/docs/guides/image-generation)

## What this chat can actually control

The built-in `image_gen.imagegen` tool exposed in this session accepts a prompt, reference-image paths or recent conversation images, and `transparent_background`. Its schema does **not** expose model selection, quality, dimensions, output format/compression, masks, seed, sampling steps, or guidance scale. This is a direct observation of the session's tool definition, not a claim about every OpenAI interface.

Consequently, writing “max quality,” “2048 px,” or “high fidelity” in its prompt expresses intent; it does not establish that an API parameter was set. A direct API workflow would make more settings testable. Using it would be a separate implementation and potentially billed action; this research did not do that.

## Proposed optimization process

The following choices are engineering recommendations tailored to this problem. Their numerical thresholds and sample counts have not been validated experimentally.

1. **Define pass/fail before generating.** For a flat logo: correct silhouette; no lettering; approved card count and overlap; intended colors; no texture inside solid regions; fully opaque shape interiors; transparent exterior; readable at 16, 32, and 64 pixels. Edge antialiasing is permitted. Do not require every edge pixel to be opaque.
2. **Make the request unambiguous.** Supply the approved original PNG as the geometry reference. Describe the required visible result, including which holes are transparent and which shapes are opaque. Keep palette and geometry separate. Avoid mixing “flat solid fills” with “premium lighting,” “subtle texture,” or “dimensional material.”
3. **Separate concept search from production refinement.** Explore independent designs first. After selecting a mark, use that one reference and request a narrowly defined correction. A montage introduces unnecessary reference-selection ambiguity; one output per file also simplifies inspection.
4. **Keep a clean baseline.** Save the last acceptable geometry and do not overwrite it. Compare the repaired output against that baseline. If a defect is embedded in the reference, explicitly name it rather than asking for generic enhancement. A fresh generation from the visual contract is a useful comparison, not a guaranteed cure.
5. **Test a small matrix.** If API access is chosen later, hold prompt, reference, dimensions, and format constant while comparing quality settings. Then test a different model independently. Repeat each candidate configuration several times; a single excellent output does not establish reliability. Start with conventional dimensions rather than experimental large canvases. Upscaling cannot verify that missing geometry or texture has been corrected.
6. **Inspect original decoded pixels.** Check at native size, enlarged, and final display sizes on white, black, gray, and the actual theme backgrounds. Compare at least two viewers. Record dimensions, alpha behavior, and the original file hash so preview transformations do not become mistaken for generator output.
7. **Repair according to defect type.** A local visual error may justify a constrained image edit. Flat-color or geometry requirements may justify constructing paths and controlled exports. Exact typography can be typeset separately. Preserve accepted regions by compositing when appropriate. No generative edit can be assumed to preserve all other pixels.
8. **Stop on acceptance, not enthusiasm.** Choose the lowest-cost configuration that meets the visual contract reliably. Report acceptance rate and remaining failure types. Keep the best original and the verified final asset separately.

## Banding and transparency: how to identify the cause

This review found no documented cause for the four bands reported in the Phraseback logo. It cannot identify tiling, internal decoding, watermarking, cross-session contamination, or a GPU fault from their appearance.

For a future investigation, retain the exact original PNG and make a diagnostic copy. Inspect the same marked pixels and region in RGB and alpha separately. This distinguishes several testable cases:

| Observation | Useful next step |
|---|---|
| Band is visible in RGB with fully opaque alpha | Test a fresh generation and a narrowly targeted edit; the saved pixels already contain the tonal variation. |
| Band tracks unintended interior alpha changes | Test explicit opaque-interior constraints and transparent-background handling. |
| Band occurs only after resizing or on one viewer | Investigate display scaling, interpolation, and compositing before regenerating. |
| Band recurs across unrelated original outputs | Save originals, prompts, tool/API settings, timestamps, and reproducible comparisons for a support report. |

These are diagnostic hypotheses, not established explanations of the current artifact. For flat logos, compare interior patches against their intended solid fills while excluding antialiased boundaries. Broad opacity-thresholding or smoothing is not a general repair: it can damage edges, shadows, hair, glass, or real image detail.

### Proposed controlled artifact experiment

Keep prompt, settings, dimensions, and inspection procedure fixed. Generate several independent outputs without a reference as the baseline. Next compare a clean geometry-only reference with the textured generated source; then compare opaque and transparent output requests while holding the selected reference constant. Inspect every original PNG at native size alongside its viewer preview. Record band location, interior RGB variation, interior alpha, and acceptance rate. This could distinguish reference carryover, transparency-related behavior, and display-only artifacts; it would not reveal proprietary model internals. This experiment has not been run. A fresh chat, longer negative prompt, or downsampling should not be described as a proven fix.

## Proposed prompt for the selected Phraseback logo

```text
Input 1 is the approved geometry reference for a text-free Phraseback logo.

Result: one centered upright stack of three rounded screenshot cards with the
approved speech-shaped opening. Preserve silhouette, spacing, corner radii,
card order, opening shape, and proportions from Input 1.

Change only the rendering of the fills: use uniform flat coral, peach, and
golden-yellow regions, with the approved separator color. Shape interiors
must be fully opaque. Exterior space and designated cutouts must be fully
transparent. Smooth antialiasing is allowed only at shape boundaries.

No text, gradients, lighting, bevels, shadows, grain, texture, highlights,
contour stripes, white bands, checkerboard, or backdrop. Keep generous
transparent padding. Do not add, remove, or reposition shapes.
```

This template is a proposed intervention. It is not an API configuration or a guarantee. Confirm the actual shape count and opening geometry against the approved image before using it.

## Evidence limits

No comparative generations were run, so no measured improvement or percentage of “perfection” is claimed. Documentation describes useful controls and workflows; it does not establish that a specific prompt prevents all recurring artifacts. The research supports a controlled experiment and acceptance process, with deterministic finishing for properties requiring exactness.
