# 平衡模拟器（balance_sim）

为 v0.8 平衡性方案提供数据的离线工具。它直接编译游戏的规则核心（`DicePrototype/Assets/Game/Core/Rules` 与 `Core/Session`），
不修改游戏工程里的任何文件，也不被游戏加载。

## 它做什么

| 模式 | 用途 |
| --- | --- |
| `verify` | 把模拟器内部的快速计分模型与游戏真实的 `ScoreCalculator.Evaluate` 逐次对拍（随机控制器、骰面、等级、附魔） |
| `cards` | 每张控制器在两个参照构筑里的等效倍率（有它 ÷ 没它的期望单手得分）与触发率 |
| `scen` | 手工搭好的场景：骰面养成的终态、乘区四件套、裸装过关概率等 |
| `run` | 机器人通过真实的 `GameSession` 用合法命令打完整局，输出逐关到达率、通过率、金币收支和购买统计 |
| `trace` | 把机器人某一局开头的若干条命令录成 JSON（`--seed`、`--steps`、`--out`），供打包后的游戏用 `--art073-capture … --art073-trace …` 回放并截图 |

## 运行

需要 .NET SDK 10。从仓库根目录执行：

```powershell
dotnet build tools/balance_sim/BalanceSim.csproj -c Release
dotnet tools/balance_sim/bin/Release/net10.0/BalanceSim.dll verify --n 300000
dotnet tools/balance_sim/bin/Release/net10.0/BalanceSim.dll cards --hands 3000 --out evidence/v08-balance/cards_v06.json
dotnet tools/balance_sim/bin/Release/net10.0/BalanceSim.dll scen --hands 2000
dotnet tools/balance_sim/bin/Release/net10.0/BalanceSim.dll run --first 5000 --seeds 300 --out evidence/v08-balance/run_v06.json
```

所有模式都接受 `--config <路径>`，用来模拟候选数值。候选配置由 `make_candidate.py` 从现行 `P0Config.json` 生成，
每一处改动都写在脚本顶部的表里：

```powershell
python tools/balance_sim/make_candidate.py --out tools/balance_sim/candidates/v08_candidate.json
python tools/balance_sim/report_tables.py stages evidence/v08-balance/run_v06.json
```

## 机器人怎么玩

- 对局：每次投掷后，对“锁哪些骰、是否提交”做抽样前瞻（最多看两次投掷），目标是尽快凑够本关剩余分数。
  它只看当前可见的骰面，不读取任何随机数状态；规划用自己独立的随机流。
- 商店：对每件商品，用同一批模拟手牌估算“买了之后的关卡得分 ÷ 现在的关卡得分”，按每金币收益排序购买。
  估值同时看平均手和较差的一手（第 25 百分位），所以高方差的卡不会按好运气估价。
  下一关看起来危险时不留钱，安全时为利息留钱；超过利息上限的余钱只要有提升就会花掉。

## 已知局限（解读数据时必须考虑）

1. 机器人不会经营“养成型”控制器：C07、C15、C26、C36、C37、C38、C43 这类提交后改骰面或升牌型的卡，它只按当下收益估值，
   因而几乎不买。这些卡的强度在 `scen` 模式里按成型状态单独测量。
2. 机器人不使用：光滑 / 粗糙 / 随机变形骰面，红色浪潮、遥控骰子、大数多来、小数多来，以及道具槽、折扣、免费刷新、删除骰子等服务。
   达标后它立刻结束本关，不会为了养成而多提交。
3. 机器人胜率不是玩家胜率。它适合比较两套数值的相对差异和曲线形状，不能直接当作“玩家有百分之多少能通关”。
4. 候选方案里需要改代码的三项无法在这里模拟：控制器半价回收、C53 的新结果表、C51 的成长上限。
   模拟时卖价仍是固定 1 金币（偏保守），C53 从商店移除。
5. `ConfigValidationRelaxed.cs` 取代了游戏里钉死 p0.6.0 数值的 `Validate()`，只在模拟器里生效。
