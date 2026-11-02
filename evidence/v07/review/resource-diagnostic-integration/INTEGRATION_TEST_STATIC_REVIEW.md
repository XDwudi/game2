# Resource diagnostic integration test static review

Reviewed `/root/v07_run_review`, 2026-11-02. Candidate `Art07ResourceDiagnosticPlayModeTests.cs` SHA `f2a4664837c6e554a7e0b10f3a16bc5e7e2985a0a4687e607653ece92b30d6da` was independently read and copied exactly into this cache. No test or Editor was run by the reviewer.

**Suitable for root's real integration run; no current normal-path static blocker found.** The test loads the real Bootstrap with a fresh save override already installed, calls the diagnostic's Editor-only entry, and leaves focus/input checks active. Its waiting loop only yields, so it does not enumerate, hash or serialize during the measured window.

Assertions cover actual primary font and body mesh identity, nine route snapshots, Shop phase/real command history, unique texture identity and atlas ownership, accounted unknown memory, ten actual body instances distinct from contours, fragment pool rows, actual config/MVID binding, state/pending/isolated-file equality, counter completeness and CSV row count. Positive GC is valid data and is not made a new hard performance failure. Editor results remain explicitly separate from Player acceptance.

The outer teardown first destroys the probe/unloads the actual scene, then restores save/display statics and removes only this diagnostic's known clock keys. It records and restores the 23 explicit preference keys, while treating unexpected changes as failures. Ordinary persistent files are read and compared only; the previously proposed write-back/delete behavior has been removed. A file change remains failure evidence rather than being overwritten.

Failure-path limitation noted to root: if the probe has already destroyed GameApp and fails specifically while deleting a clock key, an early ErrorForTests assertion can precede importing registered keys from its report into the outer teardown. The test still fails rather than falsely passing. If encountered in actual execution, use the preserved report to retry cleanup of that run's known keys. This does not authorize deletion or restoration of ordinary user files.

Compilation, real PlayMode completion, collection values and preference/file after-guards remain pending separate evidence. This static review is not a §31 result or a whole-Player/art acceptance.
