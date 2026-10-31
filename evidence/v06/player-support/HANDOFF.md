# P0 v0.6 built Player verifier handoff

Status: managed/API preflight passed; actual Unity/Player execution belongs to root and is not claimed by this handoff.

Inputs: P0 v0.6 implementation contract; current production Core, Runtime, Debug/Display/Controllers UI; normal-run-trace.json (145 steps, terminal failure at stage 8); debug-24-trace.json (175 steps, 24 settlements / 23 shops / 8 risks).

Owned sources: Runtime/Verification/PlayerVerification.cs, PlayerVerification.V06.cs and Validation~. Source hashes and unchanged performance-method comparison: preflight.json. Latest managed run: compile-run09.log, 40 checks passed, including the ACTUAL shared stress factory and 14-C41 broken-chain factory through production session invariants/commands. Earlier failed compilation/harness attempts remain in this folder; run04/05 failures were a missing helper during edit, run06 was a held managed test executable, resolved before run09. No Unity or Player was launched by this agent.

The original RunStress schedule, LateUpdate sampling, performance report, metric coverage, timing validation, and frame preallocation are unchanged. Frame budget remains P95 16.7 ms, warmup 10 s, sampled duration 180 s, load 10 dice / 8 submission slots / 10 controllers (4 ordinary + 6 slotless) / 5 consumables. Stress identity, enchantments and save-slot policy now use v0.6 rules. The same factory is used by the built Player and managed preflight.

## Main probe

Windows executable or macOS app binary arguments:

```
--verify-p0 <normal-run-trace.json> --verify-debug-trace <debug-24-trace.json> --evidence-dir <base-directory>
```

The debug-trace option is optional: default is debug-24-trace.json alongside the normal trace. Each invocation creates a unique player-UTC-guid directory and isolated normal/debug saves. Main report is **player-report.json**, schema **p0-player-verification-6**, revision **p06-player-ui-native-display-9**. Original normalReplay/performance/screenshot fields remain; new fields are v06Checks, displayChecks, debugTraceSha256. Independent progressive files are v06-checks.json, v06-display-checks.json and debug24-replay.jsonl.

The new checks perform actual uGUI events on active/visible controls (input value/submit, button click, BeginDrag/Drag/EndDrag, scroll wheel) and InputSystem Escape events. They do not claim OS mouse/keyboard automation. Debug UI exercises exact password rejection/acceptance, all six enchantment identifiers, new C41/44/45/51/52/53 cards, 32 dice, permanent face pip/color edits, all seven consumables, level 100, large integer random range and 20 pure preview reads. Ordinary checkpoint/state isolation is checked before/after. Handbooks inspect both real tabs. Controller checks include resizing the open panel, real handler reorder, same-slot/no-op, outside and Escape cancellation, focus callback cancellation (explicitly NOT physical Alt-Tab), active-stage frozen order, and long damaged mimic-chain hints.

Display checks use real Screen mode/size readback, all three platform modes via UI buttons, supported non-native exclusive mode with Escape rollback, windowed 720/1080/1440 and 16:10/ultrawide sizes that fit the actual work area, actual 15-second timeout, Escape, and no game-state/save mutation. Unsupported or physically unexecuted scenarios are explicit statuses; they are never converted to passed.

Debug24 replays provided initialWorkspace and every supplied command through GameApp.Send/presentation; every step compares score, target, phase, sequence, wallet, three RNG states, canonical state hash, disk reload and rendered state. JSON explicit null tokens are narrowly preserved when JsonUtility creates placeholder DTOs; absent/invalid values are not replaced by defaults. 24-stage success is debug integration evidence only, not normal balance evidence.

## Real new-process pending recovery

Use two independent invocations with the normal probe arguments above PLUS one role option. Supply a new shared evidence directory that does not contain existing settings:

```
--verify-display-recovery-write <shared-recovery-directory>
--verify-display-recovery-read <same-shared-recovery-directory>
```

The writer changes the actual display from confirmed 1280x720 Windowed to native borderless, durably writes pending preferences and writes **ready-to-terminate.json**. It prints `P06_DISPLAY_RECOVERY_READY <path>` to Unity log and stderr. **The runner must externally terminate that writer process immediately after the receipt appears, before the real 15-second confirmation timeout**; do not let it exit normally. The writer fails if not killed promptly. Then start the reader as a new process. Reader requires a different PID, the exact pending-file hash and pending=true, initializes the production preferences from disk, waits for real native restoration, and writes **recovery-read-receipt.json** with status passed only after mode/size/pending persistence are verified. Its player-report status is recovery_passed; no normal replay/performance result is claimed by these short modes. Main probe separately records crash recovery as not_executed; the two-process receipt is the separate evidence for that condition.

## Cross-platform checkpoint

Main run exports **cross-platform-checkpoint/active-run.json**, **command.json**, **expected.json**. Add `--verify-import-save <checkpoint-directory>` to a main invocation on the other platform. It copies into that invocation's isolated evidence directory, compares exact continuation score and canonical rules/RNG state, reloads and tests duplicate submission; source checkpoint stays unchanged. Export and import directions must each be executed and retained for the M5 claim.

Remaining execution: root formal Unity compile, Player Windows main and two-process recovery; independent reviewer source/evidence binding; user-run macOS M5. Physical focus/monitor removal and unsupported display sizes remain explicitly unexecuted/inapplicable where applicable. No global framework/state document was edited by this agent; root owns task/state integration.
