# GitHub draft URL adapter: independent limited review

Reviewer `/root/v07_run_review`; actual checks completed 2026-11-02 03:41:15 UTC. Writes limited to this review cache; production, standards, ZIPs and original network records were read-only.

**Specification conformity: passed for the fixed Windows asset and recorded draft release. Implementation quality: no blocking issue found.** Root may perform the serial new-directory extraction/diagnostic run. This review itself did not extract, launch Player, access Registry or make network requests. It is not a release acceptance or evidence that the new extracted Player has run.

The frozen adapter is `e0bbcab91e6aa0ded5d32d83d1381fa94ac67606702b2f13d7de6c608b3ca169`, 14,405 bytes. The author's file and independently exercised copy match. This supersedes the f8ba6536 adapter's assumption that a draft browser URL necessarily contains the canonical release tag; the earlier static review remains historical evidence only for its stated version.

## Actual server records and correction

The untouched publisher upload record identifies Windows asset **700392401**, 87,218,149 bytes, SHA-256 `c823056517851a786cfe8a185c43ad6829e3bdf2f5d8686eb9b17ff6a39169b1`. The explicit `release-upload-20261102-033620-435969.json` records release **431044653**, draft and prerelease true, `tag_name=p0.7.0-rc.1`, target commit `dca7a2610f39d65088b9541549d3465c388fd3e7`. Its asset and release HTML URLs use the genuine `untagged-c3095d2fc99df8ae7a31` alias.

The real download receipt timestamp is 03:39:06 UTC, HTTP 200, final host `release-assets.githubusercontent.com`, 309 ZIP entries and the same SHA/size/source. The reviewer read these local raw records and recomputed downloaded archive/member checks through the plan command; the reviewer did not independently contact GitHub or observe the HTTP connection.

The new helper cross-checks the upload, explicit server release and receipt. It binds positive integer asset/release IDs; exact authorized repository API URLs; uploaded state, ZIP content type, name, digest and size; draft/prerelease/tag/target/published state; one matching asset by ID or name; exact browser URL across all three records; and the browser alias against the release HTML URL. Browser scheme/host/repository/path are still constrained. No receipt rewrite, arbitrary-host allowance or substitution of local ZIP bytes occurs.

`--server-release-record` is required only for downloaded mode. The chosen record must reside in the publisher evidence directory with a release JSON filename; no latest-file guessing occurs. Upload and release JSON are each read once and parsed from exactly the bytes whose SHA enters the existing `inputHashes` guard. That guard executes after preflight, after extraction, immediately before launch and after the diagnostic. Original manifest/proof/receipt/ZIP checks, EXE/Runtime/boot bindings, real build report and runner isolation are preserved. The execute path adds only the server facts and their guarded hashes; it does not change gameplay, performance thresholds, display requirements, save behavior or timeout.

## Independently executed checks

- Ran the exact copied adapter in downloaded **plan-only** mode with the real explicit server record. Exit **0**, `status=plan_only`, expected downloaded ZIP and future extracted EXE/isolated diagnostic paths, source dca7a26, asset/release IDs and original draft alias all correct. The command independently ran the existing frozen gate/package validation, complete ZIP SHA/CRC/member proof checks and original build preflight.
- Output directory `artifacts/cache/v07-independent-draft-download-plan` remained absent. `writesOrProcessesStarted=false`, `wholeReleaseAccepted=false`; no `--execute` was used.
- **18/18** independent in-memory helper checks passed: the actual three-record positive case, plus 17 rejection cases covering changed/boolean download asset ID, receipt URL mismatch, upload digest/size/API repository mismatch, release API ID/target/tag/draft mismatch, release asset digest/missing/duplicate, HTML alias mismatch, and coherently changed browser URLs to another repository, another host, or HTTP. Only deep-copied dictionaries were mutated; original evidence remained untouched.
- Compared the current candidate to the previously reviewed f8ba6536 copy. The changes are the constrained draft server-record helper, new explicit argument, initial-byte read/hash binding and reported server facts. All runtime/extraction/guard logic outside that integration remains the prior reviewed path.

## Limits

The downloaded plan success proves that the real downloaded ZIP and three recorded network facts are compatible with the frozen package and source. It does not prove a new extracted process has run. The adapter checks existing local publisher records and cannot independently authenticate them to the server without a new network action. It intentionally uses the recorded draft snapshot; it does not query current publication state.

The earlier fixed-package limitation about unspecified POSIX file type is unchanged and non-blocking: all 309 exact frozen member modes have been checked as regular/unspecified regular, and arbitrary future archives are outside this review. Windows extraction does not claim POSIX permission preservation.

The 180-second performance failure, native exclusive-display failure, settlement draw-call target miss and remaining highest-FX/UI-latency/M5/human checks remain unchanged. Both acceptance fields stay false. New extraction success must retain this diagnostic-only scope.

Evidence in this cache: `run_downloaded_windows.py`, `check_helper.py`, `helper-checks.json`, `helper-checks.log`, `downloaded-plan.log`, `plan-results.json`, `candidate-diff.patch` and `bindings.json`.
