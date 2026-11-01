# Eight transaction recordings — independent review

Reviewer: `/root/v07_spec_audit`. Read-only production review, 2026-11-01 UTC. Authority: `docs/specs/v07/ART_SPEC.md`, art0.7.0 / p0.6.0 / schema4. **REWORK for visual/specification issues below; not an overall art acceptance.** No Unity execution, production/test/AC edits, or human art approval by this reviewer.

## Evidence boundary

Input: `artifacts/cache/v07-tx-*-01`, eight actual WindowsEditor 1920×1080 captures. Runtime MVID `cf4f28d9-e3b8-4b10-88fe-3bc3d2616409`, core MVID `8f690e0d-a075-4b72-98e4-2cb64ced2fa3`, Unity 6000.6.7f1, Windows 10.0.19045, RTX3080 / D3D11. These are offline 1/60 presentation-clock frames, not real-time frame-rate/performance proof or a frozen Player release. Embedded p0.6 build identity is historical; the working-tree source snapshot and assembly MVID identify this capture.

All eight source-snapshot files independently hash to their recorded `fe46e2e94b239be3964a89b7cb41ca6e9bf49a62be5298b3d9ead04881eda667`. The four reviewed transaction/post-effect/transition C# sources still matched that snapshot at review time. Later fixes require new evidence. Complete bindings and actual viewed PNG SHA256 values: `tx-recordings/bindings.json`.

All **512 PNG hashes match** their JSONL rows; indices are consecutive and video timestamps monotonic. After each commit, frame rows contain one state hash and one save hash through the final hold. Ordinary-save hash stays `6d544a09c187b32a8f6d87e4607aa7920ad008c6f4038ac5b707283ed35d2beb` across all clips. Metadata reports timing/preferences restored and no recording/restore errors. This checks evidence and captured-state consistency, not every possible transaction branch.

Eight `visual-silent.mp4` files were fully decoded by the existing FFmpeg with exit 0 and frame counts matching PNGs (512 total); no audio. Decode logs are `tx-recordings/mp4-decode.json`. The reviewer visually inspected the following **37 distinct actual PNGs**, including consecutive transition/end frames; did not claim live video playback or every frame visually inspected.

| Clip suffix, all `v07-tx-…-01` | Frames / duration | Actual PNG indices viewed |
| --- | --- | --- |
| buycontroller-normal | 67 / 340ms | 14, 15, 25, 36, 37 |
| expandcontrollers-normal | 67 / 340ms | 14, 25, 36 |
| expanditems-normal | 67 / 340ms | 14, 25, 33, 35, 36 |
| glassdie-normal | 72 / 420ms | 14, 15, 16, 28, 40, 41, 42 |
| randomface-normal | 68 / 360ms | 14, 15, 16, 25, 36, 37, 38 |
| randomface-reduced | 54 / 120ms | 14, 19, 23 |
| refreshcontrollers-normal | 63 / 280ms | 14, 15, 23, 32 |
| refreshcontrollers-reduced | 54 / 120ms | 14, 19, 23 |

Files are `frames/frame_000NNN.png` under each folder. Transaction progress is `row.transaction.playing`; the separate `presentationPlaying` is the scoring clock and correctly stays false here.

## Findings requiring rework

| ID / priority | Observed defect and source | Executable repair / recheck |
| --- | --- | --- |
| TX01 / P1 | Old captured card text becomes solid dark quads. Purchase frame15 replaces rarity/name/details/price text; refresh-normal frame15 does this across the entire old shelf. It is not a missing-font final-state issue: both lead and final frames have correct glyphs. `Art07TransactionMotion.Prepare` assigns the TMP material then unconditionally binds `g.mainTexture`. | Preserve the actual TMP atlas/submesh material texture when copying old meshes; keep UI image texture handling separate. Re-record purchase plus normal/reduced refresh and inspect early consecutive frames, fallback glyphs and final state. Parent already has a candidate fix; this review does not validate it. |
| TX02 / P2 | Cash ticket obscures the current wallet. Purchase frame25 covers almost all of `104 金币`; refresh and both upgrade clips show the same collision. `GameApp.Art07Effects.cs` creates a 220×58 ticket at (48,146), on the balance/interest area, then moves it through the balance. | Place the entire ticket trajectory in an unused sidebar rectangle clear of both the balance and interest text, or reserve a dedicated delta line. Recheck positive and negative deltas at 720/1080 and all supported font scales; do not hide the balance to make room. |
| TX03 / P2 | U05 does not visibly start as ceramic and does not communicate a whole three-dimensional glass die. At frame15, recorded `_Glass=0`, but the body is already blue; frames15→40 primarily darken/fade the same face-on blue square. Text correctly states ceramic→glass, while the body disagrees. `BeginUpgradeTransaction07` calls `ShowPreview(newDie)`, then only interpolates `_Glass`; the new body's base tint is already the glass tint. Its rotation faces F02 directly into the camera, removing the other faces' volume cues. ART_SPEC S13 and U05 require the entire ceramic die to transition to stylized glass with opaque pips. | Initialize the visual shell from the before material/tint, interpolate base tint and glass properties to the committed after appearance, and show a stable three-quarter whole-die view for U05. Retain face identities, opaque pips, no future-break cue, and 420ms total. Capture beginning/middle/end plus the whole-die final state. |
| TX04 / P2 | Normal U07 lacks the specified single flip from old dot patch through a question mark to the receipt value. Frame25 simultaneously shows old central red1 and two new red dots (looks transiently like3); frame36 correctly ends at red2. `BeginUpgradeFaces07` uses the general shrink/fade face transition, not a flip/question-mark sequence. ART_SPEC U07 explicitly specifies this motif. | Add one visual-only patch flip bound to the already committed result, with a brief neutral question-mark side; do not spin/re-pick or consume RNG. Keep the 360ms overlapping sequence; reduced mode can retain its short non-rotating transition. Verify only selected F01 changes and final mesh remains receipt value2. |
| TX05 / P2 | Expansion outline remains a narrow vertical strip instead of enclosing the new card. O06 frames33/35 show a full280-wide new card but a roughly48-wide left outline; O05 frame25 has the same issue. `BeginExpansionTransaction07` calls `TrackReveal(...flip:true)` before `ExpansionFrame07` samples world corners. TrackReveal immediately rotates the card −80°, so the outline uses its compressed projection. | Measure final-layout card bounds before starting the flip, or make the outline share the card transform while preserving final dimensions. Recheck both controller and consumable expansion mid/end frames; only new slot should animate. |

## Confirmed limited outcomes

- Purchase visibly updates balance110→104, marks the offer purchased and introduces the new equipped card; no whole-shop shake is visible in the inspected frames. Final names/icons render normally. TX01 still prevents visual passage.
- O05 adds one genuine sixth controller offer (`2233`) and balance110→103; existing five offers remain in place. O06 changes the actual item capacity4→5, adds genuine `I06` and balance110→104; it does not add a backpack slot. The temporary item card correctly says “新增道具位5 / 增加提交 / 2金币·道具页”. Its brief display on the service page then removal at340ms is recorded as current behavior, not proof of a rules failure. Empty-candidate expansion is not covered by these eight recordings.
- U05 committed-state comparison changes only D086 `isGlass` and `dataRevision`; its six face records remain unchanged. U07 normal changes only D081 F01 (1→2) plus dataRevision; reduced changes only D126 F01 similarly. Final mesh and visible right-hand result are2, red. Current unrolled state remains “未生成”; no additional random draw is inferred from the visual transition. The transient three-dot appearance is a presentation ambiguity, not evidence that Core selected3.
- Normal and reduced refresh show balance110→107 and next refresh price3→5, six new controller cards and unchanged other visible product sections. Reduced mode uses fade without the normal rotation. Glyph snapshot issue and cash overlap still apply to the affected source.
- Durations match the recorded contracts: refresh280ms, purchase/expansion340ms, U07 360ms, U05 420ms, reduced120ms. Upgrades expose before/after result labels before completion and then return to the ordinary shop. These clocks do not prove real-time performance or that players have sufficient reading time; no human experience approval is supplied.

## Limits and handoff

This batch does not cover replacement net income, selling, skip/focus-loss interaction, all upgrade types, empty legal-offer pool, 720p, every font scale, audio, macOS or Player runtime. Prior dedicated evidence remains separately scoped. Do not use these eight successful captures as an overall transaction-art pass. Root/presentation owner should fix TX01–TX05 and produce affected fresh recordings; preserved old captures remain failure evidence.

Related score status remains in `SCORE_LABELS_REVIEW.md`: SL02 closed only by the inspected1080p033545 retest; SL01 source-row overlap awaits fresh complete retest/visual review. The later034058 breakdown partial/timeout report was received but has not been visually reviewed in this TX pass.


## Upgrade retest — 720p 040317 (11 actual PNG viewed)

2026-11-01: independent review opened all11 PNG in `artifacts/cache/v07-upgrade-720-040317/art07-upgrade-frames`: U05 progress0/18/48/80/99; U07 progress0/18/48/80/99 plus the question-hidden counterfactual. XML040144 remains3 total/2 passed/1 failed because the GPU helper read vertices of a non-readable imported mesh. XML040317 is1/1 passed after the helper used bounds; logs record U07 stagesOldPips→Question→CommittedPips and angles0/45/90/135/179.8341. Passing that check does not approve the symbol's orientation.

**TX03 limited closure:** U05 starts in warm ivory ceramic, not the former already-cyan appearance. Top/front/right faces remain visible in the same pose; the body gradually becomes blue-grey glass while F02=2, blue pips and identity remain unchanged. This closes the reported wrong starting color and flat one-face pose in this720 capture. It does not approve all glass lighting, other combinations,1080,Player or final animation smoothness.

**TX04 still REWORK; narrowed defect:** U07 old1 pip and committed2 pips are now mutually exclusive, with one distinct intermediate question mark; the former false3-pip overlap is absent in inspected samples. Atp18/p80 the pip patch is visibly narrowed, consistent with a half-turn. However, **U07-p48-33.png displays an upside-down question mark: the isolated dot is above the curved hook.** This is an actual visual defect even though the counterfactual GPU check finds nonzero pixels. Keep the question plane screen-upright using the projected camera-up vector within the target face plane, independently from the turning pip patch; do not rotate the physical face or rewrite its pips. Recheck the actual intermediate frame after correction. Discrete screenshots alone do not establish continuous timing/smoothness.

The top cash ticket is now visibly in the shelf's upper free space and leaves the wallet readable in these samples, but all eight transaction videos have not been rerun/reviewed here; TX01/02/05 are not globally closed by this upgrade-only batch. Exact11 current PNG hashes and both XML hashes are in`tx-recordings/upgrade-040317-review.json`. This folder has no per-frame source/MVID metadata; the binding file explicitly records that limitation. No production/test/acceptance changes by reviewer.
