# p1.0.0 实测证据（2026-11-16）

对应报告：`docs/specs/v10/PLAYTEST_REPORT.md`。对象是已发布的 p1.0.0-rc.1（构建源 `c7d9379`），配置 `P0Config.json` 没有改动。
没有真人试玩；全部数据来自平衡模拟器（直接调用游戏的规则核心）和打包版的命令回放。

| 文件 | 内容 | 命令 |
| --- | --- | --- |
| `run_base_6000.json` / `.log` | 发布时的机器人，种子 6000–6599，600 局。与 `../balance/run_p100_final.json`（种子 5000–5299，300 局）合并成 900 局基础样本 | `run --first 6000 --seeds 600 --threads 10` |
| `run_lab_5000.json` / `.log` | 减益对照实验：种子 5000–5299，每个首领关开打前把本周期池子里的每个减益套在当时的构筑上（`bossLab`、每关的 `lab` / `labOrdered`）。机器人的决定与 `run_p100_final` 逐局相同（已核对 300 局的通关与到达关数） | `run --first 5000 --seeds 300 --threads 10 --lab` |
| `run_prepared_5000.json` / `.log` | “会准备的机器人”：种子 5000–5299。进首领关前按减益重排控制器；估算减益损失时计入被失效的控制器 | `run --first 5000 --seeds 300 --threads 10 --prepared` |
| `replay-5005/` | 种子 5005 的整局通关在 rc.1 Windows 包（新解压）里的回放：`trace_5005_full.json`（437 条命令）、`tour.json`（387 张截图、状态 completed）、`Player.log`（0 条异常，4 条已知的着色器裁剪告警）、11 张缩到 1280×720 的截图 | `trace --seed 5005 --steps 437`；`DicePrototype.exe --art073-capture … --art073-trace … --art073-steps 437` |
| `run_base_6000_recheck90.json` / `.log` | 用最终版模拟器重跑种子 6000–6089：通关、到达关数、每关得分与 `run_base_6000` 逐局相同（90 / 90） | `run --first 6000 --seeds 90 --threads 10` |
| `verify_p100_after_playtest.json` / `.log` | 模拟器修改后重跑的计分对拍：30 万次随机提交，与游戏计分 0 差异 | `verify --n 300000` |
| `REVIEW_R1.md` | 独立只读复核对报告初稿与总览的意见（6 条主要、19 条次要，无阻断）。处理情况见报告第 12 节；修订后没有复查 | — |

说明：

- `run_lab_5000` 与 `run_prepared_5000` 用的是本次修改后的模拟器（`tools/balance_sim` 当前源码）。`run_base_6000` 是用修改过程中的中间版本跑的
  （已经有 `--lab` / `--prepared` 的代码但没有打开，所以 JSON 里带着没有内容的 `bossLab` 等字段）。不带开关时机器人的行为没有变：
  `--lab` 的 300 局与发布时的 300 局逐局一致，`run_base_6000_recheck90` 用最终版重跑的 90 个种子也与它逐局一致。
- 第一次跑对照实验时，“让控制器失效”的三个减益显示损失为 0，与整局数据矛盾。查出是模拟器估算减益损失时没有带上失效名单
  （`StageCtx.disabled` 的默认值是空数组，`Runner.Effective` 因此从不解析）；同一个遗漏也让“重排顺序”的搜索永远找不到更好的顺序。
  两处都已修正并重跑，被替换掉的第一次结果没有保留。发布时的机器人行为保持不变，以便 `run_p100_final` 可以复现。
- 完整截图（387 张，2560×1440，约 700 MB）在不入库的 `artifacts/cache/v10/tour-5005-full/`。
- 生成报告里的表：`python tools/balance_sim/v10_report_tables.py fill docs/specs/v10/PLAYTEST_REPORT.md`（加 `--check` 复核）。
