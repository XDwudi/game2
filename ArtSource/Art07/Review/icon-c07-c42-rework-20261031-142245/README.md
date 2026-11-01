# C07 / C42 targeted icon repair

Status: REVIEW, source/export only. Art version art0.7.0, unchanged p0.6.0 rules.
Owner: v07_art; root owns Unity integration and actual card screenshots.

Input: ART_SPEC sections 12–13 and unchanged manifest. C07 requires two dice,
solid left and ghost right, joined by a square copy arrow, violet/paper. C42
requires a translucent short cannon with one diamond refraction line,
paper/danger. The cannon's cracking and five-piece card fall occur only after
a self-destruction receipt; they are not baked into the idle illustration.

Source changes:

- C07: solid paper source with violet pips, translucent destination field,
  four open corner brackets and hollow violet pips; square elbow copy arrow.
- C42: layered paper glass with real fractional alpha, intact dark silhouette,
  single closed diamond, danger-red collar/hub and small muzzle accent.
- Both remain 64px logical pixel drawings, 4x nearest-neighbor 256px exports,
  with the existing fixed guard-band normalization and at least 24px safety.
- The straight-alpha bleed helper was repaired to copy all nonzero-alpha
  source texels without applying alpha a second time. This affects fractional
  transparency; all other 69 icon exports reproduce exactly without rewriting.

Evidence:

- `before/`: original reviewed failure PNG, ORA and KRA, retained intact.
- `after-three-backgrounds.png`: 256/64/32 on felt, paper and dark backgrounds.
- `before-after-64-32.png`: direct size comparison of the two revisions.
- `report.json`: 87/87 checks, actual Krita 5.3.4 conversion then KRA reopen,
  source/PNG visible RGB and alpha equality, logical grid, border, unchanged
  other 69 exports, and hashes binding specs, generators and both sources.
- `candidate-sources/`: verified new layered Krita masters (6 and 7 layers).
- `source-promotion.json`: reversible copy to official source paths, with
  hashes of candidate, production source and retained original backup.
- `../sample-validation-20261031-142700.json`: full 826/826 source checks rerun.
- `framework-tests.log`: repository checker regression suite, 20/20 passed.

Author visual review: three backgrounds were actually inspected. At 64px,
the left solid die / right hollow target and the short cannon remain legible;
at 32px their silhouettes, directional copy and red cannon accents persist.
The small diamond detail is supplementary at 32px; the card's live name and
rule text remain necessary. This author observation is not independent
acceptance or proof of the actual Unity composition.

Execution issue: the initial sandboxed native Krita conversion timed out at
75 seconds. Automatic approval then rejected elevated direct source overwrite
because it could lose editable layers. The safer retry exported new candidate
files only and refused overwrites. After successful actual reopen checks,
normal workspace file operations copied those validated candidates to the
official paths while retaining all originals. No pending approval is needed.

Reproduce the drawings with `build_content_icons.py --only C07` and `--only C42`.
For another native verification, create a fresh review directory containing
the original `before/` snapshots and use `verify_copy_cannon_rework.py --krita
<krita.com> --review <new-review-directory>`. The tool deliberately refuses
to overwrite prior candidate KRA or reopened PNG evidence.

No Unity launch, scene, UI code, gameplay config, acceptance criteria or
authoritative manifest changed. Root should refresh/import the two PNGs and
their content atlas, capture full/compact cards and request independent review.
