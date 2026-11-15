# v1.0 平衡模拟证据

工具：`tools/balance_sim`（说明与机器人的已知盲区见其 README）。机器人、种子（5000–5299，共 300 局）与 v0.8、v0.9 的测试相同。
分析与结论见 [`docs/specs/v10/BALANCE_REPORT.md`](../../../docs/specs/v10/BALANCE_REPORT.md)；那里的表格由 `tools/balance_sim/v10_report_tables.py fill` 从本目录的 JSON 生成。

| 文件 | 配置 | 内容 |
| --- | --- | --- |
| `verify_p100.*` | 数值调整前 | 快速计分模型与真实计分对拍 30 万次：0 差异（模拟器升级完成时） |
| `cards_p100.*`、`scen_p100.*` | 数值调整前 | 逐卡等效倍率、流派场景：数值调整的依据 |
| `run_p100_current.*` | 数值调整前、p0.9.0 的目标 | 300 局：通关 38.3% |
| `core_hashes_run_current.txt` | – | 上一行那次运行时规则核心源文件的哈希 |
| `verify_p100_b.*`、`cards_p100_b.*` | 数值调整后 | 调整后的对拍（0 差异）与逐卡复测 |
| `run_p100_tuned.*` | 数值调整后、p0.9.0 的目标 | 300 局：通关 33.0% |
| `run_v10_tA.*` | 候选 A（`tools/balance_sim/candidates/v10_tA.json`） | 300 局：通关 29.3%。**选用** |
| `run_v10_tB.*` | 候选 B（`candidates/v10_tB.json`） | 300 局：通关 33.7% |
| `verify_p100_final.*`、`cards_p100_final.*`、`scen_p100_final.*`、`run_p100_final.*` | **定稿配置**（目标 = 候选 A） | 在最终源码上的复跑；报告里的表以它们为准 |

命令（从仓库根目录）：

```powershell
dotnet build tools/balance_sim/BalanceSim.csproj -c Release
$sim = "tools/balance_sim/bin/Release/net10.0/BalanceSim.dll"
dotnet $sim verify --n 300000 --out evidence/v10/balance/verify_p100_final.json --log evidence/v10/balance/verify_p100_final.log
dotnet $sim cards --hands 3000 --threads 8 --out evidence/v10/balance/cards_p100_final.json --log evidence/v10/balance/cards_p100_final.log
dotnet $sim scen --hands 2000 --threads 8 --out evidence/v10/balance/scen_p100_final.json --log evidence/v10/balance/scen_p100_final.log
dotnet $sim run --first 5000 --seeds 300 --threads 8 --out evidence/v10/balance/run_p100_final.json --log evidence/v10/balance/run_p100_final.log
python tools/balance_sim/v10_report_tables.py fill docs/specs/v10/BALANCE_REPORT.md --check
```

## 局限

机器人通关率不是玩家通关率，只适合比较两套数值。它不为首领调整控制器顺序，不定向养骰面，不打单骰，很少凑顺子；
非计分类卡的价值只能从整局里的购买与留存看。单个首领减益只遇到 10–60 次，逐个减益的通过率有 ±8 个百分点左右的噪声。
模拟器的升级由一个子任务完成（两次因用量上限中断），收尾的数值调整、候选目标与定稿复跑由主任务完成；没有另请独立方复核模拟器本身，可核对的是它与真实计分的对拍结果。
