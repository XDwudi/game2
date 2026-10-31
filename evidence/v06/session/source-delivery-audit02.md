# v0.6 final source-delivery audit 02

Snapshot: 2026-10-31T15:44:31.915980+00:00 to 2026-10-31T15:44:42.229792+00:00. HEAD stayed `08ef933f2fbc3198a5e357ecebaa2758fbee8643`. Read-only source inspection; no staging, commits, Unity/Player launch, or source/evidence removal.

Candidate files after excluding derived caches and ZIPs: **2,362 files, 174,809,584 bytes** (2,361 untracked, 1 modified tracked, 0 staged). Exact paths, sizes and SHA-256 values are in [the JSON inventory](source-delivery-audit02.json).

Largest candidate: `evidence/v06/review/review-vectors.json`, **2,925,384 bytes**.

## Recommended source/evidence scope

- Current docs and tools sources/tests, including explicitly limited review-candidate notices; existing draft or pending statuses remain unchanged.
- All v06 real results, failed attempts, input data, reproducible harness csproj/source files, screenshots, persisted states, independent review, and archived minimal Builtin experiment text inputs.
- Previously archived canonical-state diagnostics under artifacts/v06/hash-diagnostic.
- Synthetic malformed C#/XML/JSON fixtures under review/gate-run* and gate-consumer-run* are negative-test evidence, not compiler caches. Preserve them.

## Do not stage

- 1,095 ignored derived files remain excluded. Directory groups for bin/obj/Library/Temp/.dotnet/.nuget/__pycache__ are listed in JSON. Do not force-add them.
- All artifacts/cache/ contents: raw logs, disposable native-minimal working project, Editor caches and generated builds. Only explicitly archived text inputs/reports under evidence belong in Git.
- All 3 discovered untracked/changed ZIPs are excluded from source staging, retained unchanged for their release workflow.
  - `Releases/p0.5.0/DicePrototype-p0.5.0-MacVerificationKit.zip`
  - `Releases/p0.5.0/DicePrototype-p0.5.0-macos-universal.zip`
  - `Releases/p0.5.0/DicePrototype-p0.5.0-win-x64.zip`

## Credential marker scan (paths only)

Candidate credential-marker matches: **0**. Explicit raw-cache log matches: **10**. No matching text, token values or credentials are printed. Authorized external input references and absolute paths are allowed.
- Keep cached: `artifacts/cache/v06/build-windows01.log`
- Keep cached: `artifacts/cache/v06/build-windows02.log`
- Keep cached: `artifacts/cache/v06/build-windows03.log`
- Keep cached: `artifacts/cache/v06/build-windows04.log`
- Keep cached: `artifacts/cache/v06/build-windows05.log`
- Keep cached: `artifacts/cache/v06/build-windows06.log`
- Keep cached: `artifacts/cache/v06/build-windows07.log`
- Keep cached: `artifacts/cache/v06/build-windows08.log`
- Keep cached: `artifacts/cache/v06/native-minimal-dev-editor.log`
- Keep cached: `artifacts/cache/v06/native-minimal-release-editor.log`

Binary files without text/OCR inspection: 448. Read errors: 0; files changed during read: 0; added during read: 1; removed during read: 0.
- Added after initial inventory (not covered by this snapshot): `evidence/v06/review/unity-evidence-review12.json`

The two audit02 outputs themselves are excluded from their own input inventory. Later native-run outputs need the same narrow exclusion/credential check before staging. No failed evidence was removed, no result was rewritten, and this audit does not establish release or whole-game acceptance.
