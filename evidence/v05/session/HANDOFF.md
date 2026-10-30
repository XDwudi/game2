# P0 v0.5 Session handoff

Base: 36025ff. Input: docs/specs/P0_v0.5.md, config p0.5.0 / schema 3. Owned production: Core/Session; owned NUnit sources: Tests/EditMode/Session. No Unity Editor or Git writes were executed by this agent.

## Implemented

- ControllerInstance inventory and fixed shop instances; independent base K=5 with C09/C21/C22; four ordinary slots and ten total including slotless; atomic same-ID nonempty-enchantment replacement with one-coin trade-in.
- 24 stages, 8 configured risk branches, 23 shops, latched qualified state, manual EndStage with unsubmitted-hand discard, final-submission and no-dice auto-finalization. One frozen settlement ID and original-balance principal cap 50; PreviewStageSettlement shares the same calculator without writes.
- Frozen trigger driven post-effects in C07, C26, merged C36/C37, C15, C38 order; deep copies and physical face targets; once-only retries. C34/C35 probability vectors flow into actual rolls.
- Strong persisted instance/enchantment/qualification/settlement validation and complete Snapshot/Breakdown clones. I04/I05 now require a future legal roll on a matching unlocked die.

## Actual verification

166/166 NUnit assertion cases executed with the .NET 10.0.403 managed harness (run03.json and run03.log). This is not Unity EditMode. The previous 114 cases were updated for intentional v0.5 changes; 52 cases were added, including all 20 new purchase IDs and transaction boundaries. Source hashes are in source-manifest.json. No failed behavior was removed to force passage. The initial dotnet CLI home sandbox failure is retained separately; the local CLI-home rerun succeeded.

Eight legal-input normal-policy seed trials are recorded in search-statistics.json. Best seed 20261037 reached stage 9 and failed, with 161 actions. The policy uses current visible faces and an independent planning PRNG; it does not read future rule RNG. The chosen trace is normal-run-trace.json. NormalRunReplay executed 161 restore cycles and 161 duplicate-command checks against real core, matching the stored terminal state. This is not a winning-run, balance, or player success-rate claim.

## Outstanding validation

Root must compile and execute real Unity EditMode/PlayMode and built Players. Independent reviewer must inspect the production changes and source-bound evidence. Gameplay experience and platform verification remain separate from these managed results.
