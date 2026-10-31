# Exact state-sequence coverage supplement

This independent reviewer added a small managed host under `sequences/`, without changing production or Unity tests. Five cases ran through the actual current Core. Each uses one explicit isolated debug starting fixture, then real session commands; none is represented as normal-run balance, Unity serialization, Player, or Mac evidence.

`effect-sequences-run02.json` and its adjacent binding/log record **5/5 passed**, 21 input/config/host hashes stable before and after execution. Each command records its sequence, phase, stage, previous primary, trips level, controller fund and sale values, all three RNG states, and canonical state hash.

| Spec case | Actual sequence and assertions |
|---|---|
| E011 | Three submissions use trips, scatter, trips, with genuine roll/lock/submit commands after the first fixture; previous primary updates each time and trips level stays zero. |
| E012 | C43 is present before the first trips, then EndStage/Continue/LeaveShop/StartStage and another trips; the first matching next-stage hand increments trips once after its score. |
| E013 | First trips occurs without C43. The ordinary shop legally refreshes until offering C43, buys it, then enters the next stage and submits trips. Acquisition preserves history and the new C43 upgrades that first matching hand. |
| E049–E052 | Existing C52 starts with sale 10, C41 starts at sale 1. Real stage completion gives C41 sale 4. Reorder at the shop to copy C46, finish the next stage: still 4. Reorder back to C52, finish the following stage: sale 7. Same instance and accumulated fund persist; pre-existing target fund is never copied. Duplicate finalization commands preserve the complete state hash. |
| E053 | Stage 24 completes into StageSettlement with RunWon successor, then Continue reaches victory. Both fund emitters grow once, no shop exists, a repeated finalization and a rejected later Continue cannot pay again. |

The first run's sole failure was in the review host: it expected RunWon immediately after EndStage and omitted the established StageSettlement→Continue step. That report remains as `effect-sequences-run01.json`. Reading the existing continuation code confirmed the fixture assertion was wrong; the host now checks the intermediate successor explicitly and then performs Continue. No production code, expected payout, or acceptance condition was changed.

This supplements the earlier 2077-case managed review without rerunning that whole suite or starting Unity. The remaining native display failure and all Mac/manual pending conditions are unaffected.
