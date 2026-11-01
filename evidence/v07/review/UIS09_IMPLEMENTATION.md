# UIS09 transaction implementation handoff

Input: art0.7.0 S10/S11/S13 and UI_SPEC_SCAN UIS09. Gameplay remains p0.6.0/schema4. Status: IMPLEMENTED / awaiting real Unity verification and independent visual review.

- Controller refresh captures only old rendered offer/card/price/rarity Graphics before Execute. The snapshot is invisible until success and contains CanvasRenderer meshes, never Buttons or callbacks. Normal: old fade 100ms, new live cards flip 180ms. Non-controller sections are untouched by this animation.
- Purchase: old product 160ms shrink/fade, real acquired instance 180ms reveal. Replacement state comes from the new committed instance. Controller acquisition scrolls the owned list to its actual new slot. Missing visible item slots use an explicitly identified read-only acquisition card. Actual net cash feedback is retained.
- O05/O06: only appended offer IDs or the real empty new slot animate, 160ms opening + 180ms flip. O06 bought on the service tab keeps that tab and displays the actual appended consumable quote at its new-slot location as a read-only preview; it does not change tabs or invent merchandise.
- U01–U07: the lower product area temporarily yields to a real DiceTable permanent-data preview with actual target die/face IDs. Outline 0–100ms, color/glass blend and single committed mesh transition 60–300ms, result label 180–360ms; U05 total420ms, other upgrades360ms. Mesh changes occur once through a wipe; no numeric or random redraw. Permanent-before, permanent-after, and current rolled value remain separate. Preview material property blocks and hidden product alpha restore on finish/skip.
- Normal timings above; Fast compresses to150ms and reduced motion to120ms with fades, no flips/slide/particles. Instant shows the final state synchronously. Success alone owns Session.Busy; paused modal freezes the transaction clock. Skip restores final alpha/transforms and clears old snapshots.

Root integration: call PrepareTransactionVisual07(cmd) after assigning command identity/presentationBefore07 and before Session.Execute; rejected/noChange paths call DiscardPreparedTransactionVisual07. After successful RetrySave and before CloseModal/PresentCommitted, call PrepareTransactionVisual07(r.receipt.command). FinishTransactionEffects07 handles skip. Root RebuildView should use lastPresentationBefore07 during Submit/replay; Effects now uses the same frozen before for PostScore.

Source-only validation: full Runtime/UI plus original Gameplay tests, existing seven FX tests and new six transaction tests compiled against local Unity6000.6.7f1 DLLs with 0 errors. The auxiliary project suppresses known pre-existing DTO CS0649 and obsolete API CS0618 warnings. This is not Unity execution or art acceptance. New `PLAY_ART07_TX_` tests cover refresh 100/180ms and unaffected products, pause and busy rejection, real new instance/skip, O05/O06 appended IDs, real U07/U05 physical-face timing, invalid purchase, and a real locked-save failure/retry using the same candidate. Existing FX three-target fixture now includes legal C15 red-blue-red plus C38, preserving the >=3 FaceDyed assertion.

Remaining actual checks: Unity test execution; screenshot/recording of S11/S13, especially clipped owned lists, 720p/150% text, target outline vs projected physical face, and O06 service-tab preview. Material/shader and snapshot UV handling must be judged in the game camera. No runtime/visual results are claimed here.

Frozen SHA256:
- `DicePrototype/Assets/Game/UI/GameApp.Art07Effects.cs`: `3e9b045a6754d3c8f1594526a68adfd48c694d1f26788cace8a00631a32baf26`
- `DicePrototype/Assets/Game/Runtime/Presentation/Art07TransactionMotion.cs`: `43b33dd11f849ff41bf6967fa3515537df6b7420d4763fe3a433a886c72f89ed`
- `DicePrototype/Assets/Game/Tests/PlayMode/Art07TransactionPlayModeTests.cs`: `9f19906cafc685ac705edb5aa74b08bd03c9da0e69a4b9701bcc1b5058d88ff6`
- `DicePrototype/Assets/Game/Tests/PlayMode/Art07FxIntegrationPlayModeTests.cs`: `6b7c5df38270699e68d1734a9119e9c90f166ca77f657b241f0e38da8442e715`
