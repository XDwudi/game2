# Resource diagnostic: independent actual Editor integration review

Reviewed by `/root/v07_run_review`, 2026-11-02. **The opt-in resource tool's real Editor integration passed its tested route and is suitable for source freezing and full regression.** No blocking defect was found in that scope. **Standalone Player resource/idle validation and whole-game art/performance acceptance remain unverified.** No Editor or Player was launched by this reviewer.

The test is `PLAY_ART07_ResourceDiagnostic_IsolatedRealRouteInventoryAndTwentySecondIdle`, actual NUnit `nunit-20261102-020427.xml`: **1/1 passed, zero failed/skipped, 67.3391573 seconds**, 02:03:20–02:04:27 UTC. It ran the real Bootstrap scene, real debug commands and UI route, then the diagnostic observer. The candidate test SHA is `f2a4664837c6e554a7e0b10f3a16bc5e7e2985a0a4687e607653ece92b30d6da`; production diagnostic SHA is `17d80bf9c7c0e82bd81c76e6dfafc334fc0e7fec9e43239bc86da14bb448bcfe`.

## Identity and original evidence

The actual start/end bindings contain identical **1,370 source-file records and five loaded assemblies**. All bound source bytes also matched the current frozen tree when reviewed. Runtime MVID `55356d69-1356-441b-8293-a913352d0d25` and test MVID `b56ce6e1-db98-449e-8031-90d3f6474b6e` match the actual resource and outer-guard reports.

The observed report correctly says WindowsEditor, development=true, D3D11, 1920×1080 Windowed, AMD Ryzen 9 5900X and NVIDIA GeForce RTX 3080. It has the Editor's zero build GUID, not a standalone build identity. Config SHA is `747ed1efc7bea15873f66a81eee5c7654971c21031f2a1517ce629adce7b6e6c`.

This targeted run preceded the new source commit. Its embedded BuildIdentity still names historical `4efd2b7`; it must not be presented as a new frozen standalone build. The exact file/assembly evidence binds the reviewed worktree. Root subsequently froze those source changes as `dca7a2610f39d65088b9541549d3465c388fd3e7` and imported a new identity. BuildIdentity itself is excluded from this test source snapshot; no mismatch was silently ignored.

Archive `artifacts/cache/v07-resource-integration-020427` contains one actual test output directory, **seven evidence files / 5,757,402 bytes**. Each archived byte count and SHA matched both the manifest and the original temporary file. NUnit and binding copies also matched the originals. There are **zero PNG files and zero images viewed** in this resource review; it provides no visual-quality approval.

## Independent data checks

The reviewer independently parsed the two CSVs and JSON, rather than relying on passed status alone.

- Idle observation lasted **20.0004271 seconds**. Its **1,200 consecutive rows**, frames 2744–3943, each have one valid GC sample, focus=true, motionIdle=true, pointerUnchanged=true, inputInactive=true and actionSequence=5. The first recorded elapsed time is 0.0001123 seconds and the last 19.9834074 seconds. These are reads of previous completed counter batches, as the tool declares.
- All **1,200 GC samples are nonzero**: mean 155,291.9275 bytes, median 160,857, maximum 168,951. These are Editor observations. They neither prove Player zero-allocation behavior nor identify an allocation's product/Editor source. Positive values are retained, not hidden or converted into a new hard gate.
- The resource CSV contains **3,926 consecutive rows** with monotonic clocks and valid phase indexes. The first row lacks the three counters and remains empty; it was not fabricated as zero. The selected after-idle object snapshot has no subsequent LateUpdate resource row, which is consistent with immediate observer shutdown.
- All nine expected snapshots exist, including actual Shop. Each contains **971 unique loaded texture identities**. Font atlas IDs resolve to font-category texture rows with explicit TMP owners. Every category sum independently matches its own rows and the overall per-object total.
- The final object snapshot totals **336,399,582 native bytes**: font 13,111,858; referenced art 81,531,664; render targets 138,413,878; unknown 103,342,182. The global texture-counter peak happens to equal this total in this Editor run. That numerical equality does not establish independent CPU/GPU residency, and the unknown category prevents treating the referenced-art subtotal as complete art-budget proof. Editor-loaded resources are included.
- The ten standard-material die **Body** instances contribute **1,880 triangles** (188 each), all using the real shared body mesh. Ten outline rows and 60 physical-face rows are separate. Six distinct fragment meshes total **120 triangles**; the 12 pooled instances have **240 triangles** of capacity. All those fragment instances are inactive, so this is not a simultaneous active-fragment/highest-combination demonstration.
- Idle state hash, pending IDs and all isolated save/log file paths, lengths and hashes are identical before and after; the retained files independently match those hashes.

## Guard and fixture review

The integration fixture sets a fresh absolute save override before real Bootstrap Awake. Its observer wait loop only yields; it adds no object enumeration, serialization or file I/O to the measured window. It preserves focus/input checks, and asserts the actual scene font/mesh identities and CSV row count. No ordinary persistent file write-back or deletion remains in the test.

Both the inner diagnostic and outer fixture independently preserve all **23 explicit preference keys**, including original existence and values. GameApp was destroyed before clock cleanup; the one registered new diagnostic clock key is reported removed and was asserted absent. Outer cleanup reports no errors. Preference restoration does not turn an unexpected preference change into a pass.

All **17 ordinary persistent files match within the fixture's before/after snapshots**. At independent review time, 16 still matched; `TestResults.xml` had subsequently changed. The reviewer parsed that current file as the later full EditMode run, **1013/1013 passed, 02:07:24–02:07:42 UTC**, after this resource fixture ended. The changed file and both hashes are retained in `integration-facts.json`. No claim is made that all 17 files remained unchanged through subsequent testing, and the reviewer did not restore or delete any user file.

A failure-path limitation remains documented in the separate static test review: if the probe destroys GameApp and then fails specifically during clock removal, an early test assertion can occur before the outer fixture imports the report's registered keys. That is a failing test, not a false pass. A real occurrence should use the preserved report to complete cleanup of only that diagnostic run's keys. It did not occur in this run.

## Remaining boundary

This closes the real Editor integration requirement for the diagnostic tool, not §31's standalone resource/idle assessment. The real 30-second run-clock save behavior remains enabled; a 20-second window does not prove the entire 30-second cycle. Formal 180-second/P95, maximum-combination presentation, UI latency, native display, M5 and human art experience requirements remain separate.

The actual JSON exposed an integration detail: JsonUtility emits no-error as an empty string. The wrapper was independently corrected in v3 to accept only null or empty string, while retaining nonempty errors and the original raw value. This was an encoding compatibility fix, not an acceptance-threshold change.

Audit-note transparency: the first reviewer harness assumed BuildIdentity was in the source snapshot, then assumed subsequent TestResults.xml remained unchanged. Both assumptions were corrected from actual evidence. These were reviewer harness assertions; the production test/report was not rerun or rewritten to remove a failure.
