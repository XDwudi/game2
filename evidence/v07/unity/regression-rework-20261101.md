# v0.7 full regression rework

Root implementation note, not independent acceptance. Inputs art0.7.0, rules p0.6.0/schema4 unchanged.

- `nunit-20261101-022445.xml`: complete EditMode, 996 passed, 0 failed/skipped.
- `nunit-20261101-023325.xml`: complete PlayMode, 92 total, 74 passed, 18 failed, 0 skipped, 260.47 s. Original report and start/end binding retained. These failures block a final build gate.
- `artifacts/cache/v07-full-play-failures.json` preserves exact failure messages/stack locations. Old UI entry points require migration to the authorized v0.7 screens; the original transaction, RNG, save, pooling and physical-face requirements remain mandatory.

## Physical pip count

The two old component-count assertions count every disconnected mesh surface. v0.7 `DiceTable.MakeFace` draws each pip as a neutral outline plus its rule-color fill, so a plain one-pip face has two components. Gold frames and texture marks add further non-pip geometry. The failure is retained; changing the expected count to two would not test visible pips.

The revised helper counts connected pure-white triangle surfaces, the pip fill geometry authored by `MakeFace`. The vertex-color shader applies the red/blue tint to neutral vertex colors, including these white fills. It does not read mesh names or assume triangles per dot. Original exact pip counts, physical face orientation and remote override/permanent-value separation remain. The six-face test additionally checks all six pip values with and without gold and with each of the three textures. Actual rerun remains required.

## Hover timing

The old test waits a nominal 150 ms and assumes its continuation executes before the 300 ms deadline. Under a delayed rendered frame that assumption is false. The revised test wraps the existing show/hide callbacks, retains their real behavior, and records actual Unity unscaled timestamps. It requires show no earlier than 300 ms and hide no earlier than 60 ms, each on the first rendered frame after its deadline. It retains actual tooltip existence checks and prints measured timing. No production duration was shortened and no timing threshold was relaxed.

## Additional real issues

New 720p/150% screenshots exposed blank controller titles from undersized Noto CJK line boxes; repaired dimensions and actual glyph tests are being verified. The first glyph test run (`023453`) correctly rejected a malformed fixture: unpurchased C45 must have no active color. Fixing that fixture also revealed the real offer label incorrectly defaulted to blue; it now says “购入定色”, while acquired instances retain the actual committed color.

The independent review identified dice pool pagination overlapping die hit areas and the first row's labels covering the next row's pips. These are production layout defects, not reasons to remove overlap assertions. Cache repair and actual projected geometry checks are in progress.
