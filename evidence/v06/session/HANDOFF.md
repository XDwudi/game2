# v0.6 Session and delivery tooling handoff

Owner: v06_session. Source inputs: archived P0_v0.6.md + raw config, schema4. Core/session source and legacy PlayMode migration are frozen; final source identities are owned by root's real Unity/build gate. No Unity, Blender, GitHub operation or git commit was performed by this agent.

## Session implementation and verification

Session commands own all rule RNG, candidate-state persistence and receipts. Scores/targets are exact decimal strings; wallet operations use checked Int64. Frozen stage order/C41 bindings distinguish emitter from dynamic state owner; C05/C42/C45/C51/C52/C53 and C43 history preserve the specified timing and ownership. Shop refresh/services, dynamic sale and negative-net replacements are atomic and retry the same candidate. Reordering unchanged order does not save, advance sequence or RNG. DebugScene uses a separate workspace/store and canonical instance identities; normal acquisition limits are retained even when the originating scenario is debug.

Public UI contract: `CreateDebugWorkspace`, `ExportDebugScene`, `ApplyDebugScene`, `ReorderControllers`, `RefreshControllers`, `PreviewScoreRange`, dynamic shop price accessors, `AbandonPendingSave`. DTOs and validators are under Core/Session. Hand current colors are physical-face-derived; point overrides use remoteOverride. Tests do not make ordinary purchases inherit debug editor caps.

Managed session run: `tests-07.json` / `tests-07.log`, 240/240. These are .NET hosted NUnit assertion tests, explicitly not Unity evidence. Historical failures remain. Independent reviewer separately tested source owners, debug pure-non-submit enchantments entering shops, save IO and other rule vectors; its final source-bound review is in `evidence/v06/review`.

`normal-run-trace.json`: actual legal ordinary commands, seed20261031, 145 commands/restores/duplicate checks, terminal RunFailed at stage8, debug=false. `normal-replay-report.json` records the replay. This is integration coverage, not a claimed winning build or win-rate measurement.

`debug-24-trace.json`: explicit `initialWorkspace`, then 175 actual commands, 24 settlements/23 shops/8 risks, 175 restores/duplicate checks, terminal RunWon debug=true. It is a transition fixture, not normal difficulty evidence. The built Player must compare every supplied command's state/RNG/hash rather than synthesizing an alternate fixture.

Unity JsonUtility can materialize null strings as empty and absent nested DTOs as default objects. Canonicalization handles only identity-less RunState Shop/Hand/Settlement placeholders and nullable semantic strings; numeric decimal fields remain strictly validated. Root diagnosed the first stale-DLL failure, then reported actual full Unity EditMode776/776. Final actual NUnit + loaded-MVID/PDB/source evidence must be taken from root's newest frozen gate, not this prose. Legacy PlayMode fixtures now enter real independent debug slots through the password/workspace UI, retain their coverage and passed root's actual execution.

## Delivery tools and invocation

Only new v06 tools/docs were written; all v05 scripts, packages and failures remain. Last offline tooling run: `delivery-tools-tests-03.log`, 67/67. Root quality checker unit tests:20/20; Python compile and all CLI `--help` entry points succeeded. These checks are synthetic tooling tests, not native Mac/Windows/Unity/GitHub execution. `delivery-tools-source-manifest.json` binds the final delivery files and test log.

```text
python tools/package_v06.py windows --build-report <actual-windows-build.json>
python tools/package_v06.py macos --build-report <actual-macos-build.json>
python tools/package_v06.py kit --windows-results <actual-player-directory>
python tools/release_v06_manifest.py --windows-results <actual-player-directory> --windows-build <actual-windows-build.json> --macos-build <actual-macos-build.json> --display-recovery <actual-Windows-shared-recovery-directory>
```

These require a real frozen identity/Git source and `evidence/v06/unity/build-gate.json`; no old test counts are filled in. The gate is rechecked against strict NUnit structure, full discovery names, exact five-assembly PDB source coverage, start/end loaded-MVID/source bindings and every frozen byte. Current Editor DLL/PDB are deliberately not compared after platform switching: the frozen actual test evidence is compared. Actual build reports/profile/GUID and Player identities must match. Source tree equality remains strict; root moved generated screenshot artifacts aside instead of weakening the scope.

Kit includes both source traces, actual Windows replay/checkpoint, same Mac ZIP and complete identity. Windows report validation dynamically derives row counts from source inputs, compares per-command fields and complete debug state hashes, and requires the real normal terminal outcome. All output ZIP/manifest/proof writes refuse to overwrite existing evidence.

Publication tool: `tools/publish_v06_release.py draft|upload|download|publish|inspect` (`--name` for upload/download). It is provided for root's authorized execution, not run by this agent. Exactly three v0.6 ZIPs, target tag `p0.6.0-rc.1`; source target comes from the verified BuildIdentity/Git commit. Credentials stay in Git helper/in-memory headers. Auth is stripped on cross-host/port redirects, non-HTTPS/authenticated untrusted URLs are rejected, existing assets are never replaced/deleted. Publish requires all three remote digests plus independently downloaded size/SHA/CRC proofs. Later signing or final Mac changes require explicit new release handling, not silent overwrite.

## Mac workflow and review fixes

`tools/macos/v06` is standalone Python3.9+ and standard system tools. Main probe gets explicit normal/debug traces and Windows checkpoint. It checks Apple Silicon/actualArm64/Metal, complete Universal slices, safe ZIP paths, mode repair only for MH_EXECUTE, actual security outputs and quarantine, then ditto-repacks the complete payload. Returned ZIP retains `.app`, BUILD_IDENTITY.json, README and OFL license; a fresh extraction compares app and metadata bytes/modes.

After the main probe, it runs the production display recovery writer, waits for durable ready receipt, SIGKILLs only that owned PID before15seconds, then starts a fresh reader PID. The reader receipt/report remains separate from the main report's `not_executed` field. Manifest accepts `--display-recovery` and binds the actual Windows reader build GUID. Mac fullscreen N/A is accepted only for `exclusive-non-native-escape` on macOS; all required native mode/timeout/Esc results remain mandatory.

Independent review negative findings DR01 (legitimate Mac N/A rejected), DR02 (self-declared truncated replay counts accepted), and DR03 (ditto app-only lost release metadata) were fixed with regression tests. DR04 was root's temporary screenshot/Git inventory mismatch, resolved by preserving those files outside Assets. Review evidence remains under `evidence/v06/player-support` and `evidence/v06/review`; independent final verdict belongs to reviewers.

No actual package creation, remote upload/download, native Mac run, M5 performance, Mac signature result, two-way checkpoint acceptance or human playtest is claimed here. Root performs actual Windows builds/probes/release delivery; user performs M5 and manual password/debug32/drag/display checks per `docs/tasks/P0-V06-MAC-VERIFICATION.md`. Keep Mac and human acceptance pending until that evidence exists.

Final independent tooling review: `evidence/v06/player-support/delivery-review-final.md` / `.json`, by v06_rules. It reran DR01–03 negative probes, observed67/67 tooling and20/20 quality tests, and successfully consumed the actual source6f9e285 gate (Unity Edit776/Play28). It bound11 delivery source files and concluded offline tool/spec conformity passed; native Mac and real publication remain outside that conclusion. Implementation ownership is released back to root for integration.

Subsequent UI06-14 rework: see `ui-targets/HANDOFF.md`. Two32-target pickers and regression tests are frozen after root's actual targeted2/2 pass. New required Player IDs `remote-target32-ui` / `shop-target32-ui` were added to the comparator and negative fixtures; latest offline tooling68/68 and quality20/20. The prior67-test manifest is preserved as `delivery-tools-source-manifest-before-ui-targets.json`; latest manifest is updated. Earlier full-suite/build evidence does not cover these later UI changes; root is rerunning full integration and rebuilding.
