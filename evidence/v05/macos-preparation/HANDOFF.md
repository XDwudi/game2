# Mac verification tooling handoff

Task: P0-002 / v0.5; user Mac target macOS 26.6.1 / Apple Silicon M5. Agent v05_play_fixtures wrote only tools/macos/**, docs/tasks/P0-V05-MAC-VERIFICATION.md and evidence/v05/macos-preparation/** for this delegated subtask. No Unity/game/test source changed during Mac preparation.

## Deliverables

- tools/macos/VerifyMac.command: no-argument bundle launcher, or explicit argument forwarding; Python only, no auto-install.
- verify_mac.py: original ZIP/hash preservation, independent directories, safe archive preflight, exact Universal checks, minimal local executable-bit repair, actual codesign/spctl/quarantine records, system ditto package and clean re-extraction comparison, native arm64 Player execution, received Windows checkpoint import, report/identity validation, comparison, final Mac ZIP and evidence ZIP.
- package_checks.py: pure-standard-library FAT32/FAT64/thin Mach-O parsing plus archive path/link validation.
- compare_replays.py: actual Windows/macOS reports and JSONL comparison; same real source/config/golden identity, contiguous sequences, canonical rules hashes, explicit gameplay fields, platform/architecture, normal-save/UI checks, imported checkpoint and complete pressure gate. Debug24 starts at sequence2 because its one initial fixture already executed NewRun.
- test_mac_tools.py: 28 meaningful positive/negative synthetic tests; never counts as real Mac evidence.
- Chinese user/operator guide: docs/tasks/P0-V05-MAC-VERIFICATION.md, including exact bundle input structure, one-command execution, manual tests, return artifacts and root follow-up.

## Verified in Windows preparation environment

- Synthetic tooling: 28/28 pass in synthetic-tests-04.log. Earlier synthetic-tests-01/02 failures retained; Python ZipInfo normalizes host separators on Windows, so checking orig_filename was required to reject identical unsafe archive bytes on Windows and Mac. Run03 also passed; run04 is latest after identity/version/pressure/checkpoint safeguards.
- Existing deterministic-checker regression: 20/20 pass in framework-regression.log.
- Python CLI argument/help path executes successfully on Windows. Real macOS tool execution is deliberately refused on Windows. No macOS hardware/process/package run is claimed.

## Root dependencies and acceptance boundary

Fill the guide's inputs directory only with actual same-source Universal ZIP, BUILD_IDENTITY.json (real sourceCommit/contentConfigSha256/goldenCaseSetSha256), normal trace and successful Windows player output including cross-platform-checkpoint. The current adapter expects ProbeRevision7 fields: environment.processArchitecture, embedded buildIdentity JSON, checkpointExported and importedCheckpointStatus. No placeholder fingerprints or pre-passed Mac output are supplied.

Mac automatic pass is still manual_checks_pending. Root must review Mac screenshots/performance, get actual mouse/trackpad and quit/resume evidence, load the returned Mac checkpoint on Windows, then update final-package hashes and do the joint LFS release/fresh-download checks. Intel runtime stays explicitly not tested. Script does not mutate quarantine, sign, notarize or push Git. A quarantined app rejected by spctl stops with retained evidence for normal system authorization/signing resolution; no unsafe bypass switch exists.

Source binding is source-binding.json. Requires independent code review and actual Mac run before acceptance.

## Independent review repair (2026-10-30)

The independent reviewer supplied `evidence/v05/review/mac-nested-link-negative.fixture.zip` and its original accepted-result JSON. The archive was never extracted. Its `a -> .` ancestor changes the real parent of `a/b/c -> ../../outside`; the previous lexical normalization missed that escape.

Repair: every archive member is rejected if any strict ancestor is a symlink, using NFD-normalized casefold lookup. This applies to directory and symlink entries even without a descendant regular file. Every standalone link target is additionally resolved component-by-component in POSIX order, following a preceding link before applying `..`; null targets, escape and cycles/excessive traversal are rejected. Normal Framework `Current -> A` and `Library -> Versions/Current/Library` links remain supported when actual files are only under the real Versions/A path.

Eight regression tests were added: the exact nested-link example, isolated nested link, case-mixed ancestors, target-only alias/`..` escape, case-mixed cycle, Unicode-normalized ancestor, valid Framework multi-link target, and LF/no-BOM launcher byte validation. Latest run `synthetic-tests-06.log`: 36/36 pass; `framework-regression-02.log`: 20/20 pass. Original reviewer archive hash and rejecting recheck are in `review-negative-retest.json`. Earlier accepted/failed records remain unchanged.

`VerifyMac.command` already had LF and no BOM; exact byte inspection is `launcher-bytecheck.json`, SHA256 `2d15bd17261be0abefc457e6b7c2ea38c4f0407c18f6a66243595b1a38da2fc0`. No bash was found in PATH, the bundled Git bin/usr/bin, or Program Files Git bin/usr/bin; `bash -n` was not run. This missing local shell check is explicitly recorded, not called passed. Root plans a Git eol=lf attribute. Actual macOS execution remains pending.

`source-binding-initial.json` preserves the prior source binding; `source-binding.json` now binds this repaired tooling. Independent re-review is still required.
