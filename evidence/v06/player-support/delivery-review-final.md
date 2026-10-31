# v0.6 delivery tooling independent review

Reviewer: `/root/v06_rules`; implementation owner: `/root/v06_session`. Scope is the three packaging/manifest/publishing tools, Mac v06 tools and instructions. Production source and acceptance criteria were read-only. Source/evidence SHA-256 binding is in `delivery-review-final.json`.

**Specification verdict: passed after rework. Production-quality verdict: offline tooling passed; real archive, remote delivery and Mac execution remain pending.** This review does not sign off the release or claim any Mac execution.

Three findings were fixed by the implementation owner and independently rechecked:

| ID | Original evidence | Final result |
|---|---|---|
| DR01 | The actual Player's Mac `exclusive-non-native-escape/not_applicable` record made an otherwise valid Mac report fail. | Exception is restricted to this exact ID on macOS. Required three modes, timeout and Escape still require passed. Independent original/fixed results retained. |
| DR02 | One normal row and one debug row, self-consistent report counts, and the real 145/175-input SHA hashes were accepted. | Validator reads the source inputs, derives counts, checks each command's values/score/hash, checks report rows and terminal state. The same one-row fixture is now rejected. |
| DR03 | Mac final `ditto --keepParent app` omitted top-level identity, OFL and player instructions. | Full extracted payload is archived; all three metadata files are required and their bytes/modes checked after fresh extraction. Independent missing-file negatives pass; actual ditto is still pending Mac execution. |

An initial self-reference concern about BuildIdentity was corrected during review: the file is explicitly ignored/untracked in this repository, so it was not a current blocker. A narrow explicit exclusion adds compatibility without skipping gameplay source. The temporary screenshot/source-tree mismatch was an environment state issue: root preserved all three generated CLI screenshot files outside Assets and regenerated the gate; no filter was loosened to hide them.

Executed evidence:

- `delivery-review-tests02.log`: **67/67** offline delivery tests passed. Includes partial/failed NUnit, exact discovery/PDB/runtime binding, changed source/hash, wrong score/type/count, ZIP traversal/Unicode/case collisions/symlink escapes, native architecture parsing, wrong-platform/Metal/Rosetta, credential redirect and recovery negatives.
- `delivery-review-framework-tests.log`: **20/20** framework checks passed.
- `delivery-review-probes-result-01.json` retains independent failures; `-02.json` proves fixes with identical input scenarios. Source SHA values were unchanged during each probe. The Python debug enum mapping also matches the actual Core enum.
- `delivery-review-real-gate03.log`: actual frozen gate accepted source **6f9e285432ab92e7304830a42c9e9e4cd74d97c3**, current identity/config/golden hashes, **776 EditMode / 28 PlayMode** cases, exact discovery, source snapshots and frozen loaded assemblies. Counts come from current reports, not old-release constants.
- Per root's additional explicit request, the existing PdbAudit was executed read-only: **5 assemblies / 60 project documents / 0 mismatches** in `unity-pdb-final.json`. No Unity or Player was launched.

Reviewed controls bind both platform package proofs and the M5 kit to one complete identity/source/config/golden set. The kit requires the same Mac ZIP and exact Windows report/traces. Manifest checks actual Windows Player build GUID and the independent display reader's build GUID against the Windows build report. Publishing rechecks the current gate, package SHA/size/CRC/safe inventory, Windows report hash, recovery evidence hashes, repository/tag target, exactly three assets and complete remote download receipts; redirects strip credentials across hosts. Existing assets/proofs are not silently overwritten. Mac results are explicitly pending, Intel has only a slice claim, and normal stage-8 failure/debug24 success retain their distinct meanings.

Limits: all publishing/ZIP production/native calls were inspected but **not run** by this reviewer. Actual final ZIP bytes, fresh remote downloads/extracted Windows execution, real two-process Windows recovery, M5/Metal/ditto/security outputs, reverse checkpoint and human checks still require the root/user evidence. Mac/physical-display unexecuted statuses are preserved; this review is not evidence that those conditions passed.
