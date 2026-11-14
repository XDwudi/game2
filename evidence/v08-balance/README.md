# v0.8 平衡性方案的模拟证据

由 `tools/balance_sim` 生成，供 `docs/specs/v08/BALANCE_PLAN.md` 引用。规则来源：分支 `codex/v0.73` 提交 `f53ae72` 的
`DicePrototype/Assets/Game/Core` 与 `Data/P0Config.json`（规则 p0.6.0）。工具：.NET SDK 10.0.403。生成日期 2026-11-12。

| 文件 | 内容 |
| --- | --- |
| `cards_v06.json` / `.log` | 现行数值下每张控制器的等效倍率与触发率（两个参照构筑，各 3000 手） |
| `cards_v08.json` / `.log` | 候选数值下的同一测量 |
| `scen_v06.json` / `.log`、`scen_v08.json` / `.log` | 养成终态、乘区组合、裸装过关概率等场景（各 2000 手） |
| `run_v06.json` / `.log` | 现行数值，机器人整局模拟 300 局（种子 5000–5299），含每局逐关记录 |
| `run_v08.json` / `.log` | 最终候选数值（`tools/balance_sim/candidates/v08_candidate.json`），同一机器人、同一批种子 |
| `run_v06_exactplanner.json` / `.log` | 最早一版机器人（不看风险、不花余钱）的 400 局基线。保留作对照：结论方向相同，通关率更低（19%） |
| `run_cand_a.*`、`run_cand_b.*`、`run_cand_c.*`、`run_cand_d.*` | 校准目标分过程中的中间候选，不是最终方案。A（200 局，通关 60%）后段目标过低；B（200 局，通关 22%）含控制欲，通关局 89% 持有它；C（200 局，11%）移除控制欲后后段过难；D（300 局，18%）曲线形状达标但整体比现行难，最终方案在它的基础上把第 5 关以后的目标下调约 15% |

这些是模拟结果，不是 Unity 测试报告，也不是真人试玩数据。机器人的局限写在 `tools/balance_sim/README.md`。
