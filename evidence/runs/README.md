# P0 normal-run automatic interaction evidence

Configuration: `p0.4.0`, save schema `2`. Successful seed: **20261032**. All 97 actions were accepted through `GameSession.Execute`; every resulting state had `isDebug == false`.

The playing policy uses only current visible dice, public face probabilities, owned items, and visible shop offers. It estimates a next roll with a separate `System.Random(731)` stream. It never reads the live dice/shop/effect RNG states to select actions and never changes the run's dice, money, targets, offers, configuration, or RNG state. It is a smoke-test policy, not an optimal strategy or a balance simulation.

Five consecutive seeds were tried: 20261028 failed at stage 3; 20261029, 20261030 and 20261031 failed at stage 4; 20261032 won. These searched results must not be reported as a player win rate. Full attempt summary is in `search-statistics.json` and raw output in `search.log`.

| Stage | Target | Submitted scores | Final cumulative score |
|---|---:|---|---:|
| 1 | 180 | 156, 52 | 208 |
| 2 | 300 | 150, 50, 224 | 424 |
| 3 | 500 | 360, 168 | 528 |
| 4 | 800 | 80, 384, 420 | 884 |
| 5 | 1200 | 340, 420, 490 | 1250 |
| 6 | 1800 | 432, 1476 | 1908 |

The final four controllers are C17 (小鬼当家), C02 (蓝色疯狂), C13 (就要奇数), and C14 (只要奇数). The run purchased five controllers and sold one through normal shop operations. No hand upgrades or consumable effects were required. The run finished with 9 gold.

`normal-win.json` is the replay input: `{seed, configVersion, isDebug, steps:[...]}`. Each step names a normal `CommandKind`; `ConfirmLocks.dieIds` lists the dice to add to Pending in that order before issuing ConfirmLocks. The remaining target fields map directly to `SessionCommand`. The other recorded fields are postconditions for verification. The replay constructs fresh commands with the current action sequence; it does not load or force the final-state file.

Independent replay executed all 97 inputs again, serialized/restored the complete state after **every** step, and resent the same command after **every** restore. All 97 restores and 97 duplicate checks passed, and the final full state exactly matched the original result. See `normal-replay-report.json` and `replay.log`.

These runs used the actual C# Core under .NET 10.0.403. They **do not** claim Unity player, UI, animation, physical save-file recovery, performance, or human playtest acceptance. The recorded trajectory is intended for additional replay in the real Unity build.

Reproduce from repository root:

```powershell
$env:DOTNET_CLI_HOME = "$PWD/DicePrototype/Assets/Game/Core/Session/Validation~/.dotnet"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
dotnet run --project 'DicePrototype/Assets/Game/Core/Session/Validation~/NormalRunSearch.csproj' -c Release -- 20261028 100
dotnet run --project 'DicePrototype/Assets/Game/Core/Session/Validation~/NormalRunReplay.csproj' -c Release
```

`source_manifest.json` binds the production source, configuration, search/replay programs, trajectory, and result files by SHA-256. Source changes require re-evaluating this evidence.
