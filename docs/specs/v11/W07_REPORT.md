# W07 离线平衡模拟器（p1.1.0）

状态：REVIEW（本轮工程交付与 smoke 已完成；R-03 的正式 300 局测量由协调者排期）。范围按用户本轮 W07 指令：本 worktree 内仅修改模拟器、`evidence/v11/balance/` 与本报告；不修改游戏源码、旧证据、旧候选，不提交 Git。

## 输入与准备度

- 输入：`DEV_PLAN.md` §4.3、§4.4、W07、R-03；`RULES_DELTA.md`（2026-11-16 REVIEW）；当前 p1.1.0 配置与真实 Core/Rules、Core/Session。起点提交 `7f618a95654e9889b10b181635682f4e4c7b1103`，开始时工作树无修改。
- 冲突处理：DEV_PLAN 部分条文仍写“领完”，以用户本轮指令和 RULES_DELTA §2 的“首件或折现解锁、余件离店前可领”为准。C98 本店回收上限由别的任务实施，模拟器不假定套利。
- 文件所有权：fast_parity 负责 Fast.cs 与 Program.Verify；bot_duplicates 负责 Runner.cs；主 agent 负责集成、其余 Program、构建入口、统计脚本、文档与证据。后续复核只读。
- 准备度：目标、接口、重复实例与战利品边界、验证阈值已明确；已实际读取规则与会话。Windows x64、.NET SDK 10.0.403、Python 3.11.4 可用。worktree 缺少原 README 指向的项目文件，补上显式编译真实 Core 源码的 `BalanceSim.csproj`；无需 Unity。
- 场景走查：重复报价 → 边际估值 → 容量检查 → 必要时出售具体实例 → 添加副本；首领商店 → 战利品估值/腾位/首件或折现 → 其他商品 → 剩余领取 → 离店。所有命令由真实 GameSession 判定，拒绝必须入统计。
- 本轮不运行 300 局或 `--lab`；完整测量由协调者安排，避免碰撞 Unity 时序敏感测试。`docs/STATE.md` 不在本轮允许写入范围，交接信息记录在本报告供协调者同步。

## 验收条件

| ID | 方法与场景 | 期望 | 证据 / 责任 |
| --- | --- | --- | --- |
| W07-BUILD | Release 编译真实 Core 与模拟器 | 0 错误，0 警告 | build 日志；实施者执行、独立复核 |
| W07-PARITY | ≥300,000 次随机提交，双持、三持、重复混合 C41，逐实例触发 | 0 差异；报告重复构筑数量 | verify JSON / 日志；实施者执行、独立复核 |
| W07-SMOKE | seeds 5000–5019、4 线程普通 run | 20 局完成；0 计分差异、0 商店拒绝 | smoke JSON / 日志；实施者执行、独立复核 |
| W07-TRACE | 固定种子 trace | 可解析命令序列，战利品顺序正确 | trace JSON / 日志；实施者执行、独立复核 |
| W07-DATA | 对 run 原始日志重算新增统计；测量命令与配对检查 | 副本、终局持有、战利品和拒绝统计可追溯 | 报表与复核；独立复核 |
| R-03-MEASURE | v1.0 既有基线对 p1.1.0 的相同 300 seeds | 本轮未运行，由协调者完成 | 后续测量报告 |

## 结果

### 实现

1. 新增可复现的 `BalanceSim.csproj`，编译当前真实 Rules / Session；仅沿用模拟器自己的 `ConfigValidationRelaxed.cs`。当前这份宽松校验没有引用已删的 `maxOwnedNormal` 或五个 shop 字段，因此无需额外兼容分支。共享编译服务曾返回输出文件写入拒绝，改用进程内独立编译后正常；项目文件已固定 `UseSharedCompilation=false`。
2. 保留原来的按来源叠加，新增逐来源实例的触发比对。能力 ID 的 `HitSet` 只做“至少触发一次”的覆盖汇总，不再被当成副本计数依据。C53 的冻结结果按状态所有者保存，独立卡分开抽样，C41 与目标共享；规划期望按每个 owner 的 E[X^来源数] 再相乘。
3. 机器人对第二、第三张采用相同的“加入前后”边际实力估值；新增卡追加，出售按实例，不再走同名替换或抵扣。经济价值取整套构筑的差，持有开关计一次，并计入赠礼、空栏和骰数上限。多张 C41 都参与局部调序；成长状态独立；养成投影保留只执行一次的面变更及 C122/C123 的冻结步长越过目标行为。
4. 先处理 LOOT，再进行其他商店行为；首件或折现后恢复购物，剩余件数继续按收益领取。满卡栏允许先卖后领。出售 C98 会影响后续价格，因此用隔离的真实会话快照先卖、重新报价，并保留 C51 成长。估值中的本店购入卡卖价不超过实付，不依赖套利。
5. 新增货架 / 战利品重复获得、胜负终局实际副本、逐卡重复排行、战利品首选与件数分布、全部商店拒绝。失败局也保留诊断；错误、计分差异或命令拒绝使 run 非零退出。trace 对运行诊断失败返回非零，并把导出的序列送回真实会话重放。
6. 独立复核发现“同状态同名卡换位但失效目标不同”会碰撞旧实力缓存，已把有序实例 ID 纳入键；定向回归见下。

### Build 与 parity

Release 构建 **0 警告、0 错误**。[构建日志](../../../evidence/v11/balance/build_p110.log)；源文件、配置、真实 Core 与二进制的 SHA-256 见 [manifest](../../../evidence/v11/balance/manifest.json)。

最终对拍：[verify JSON](../../../evidence/v11/balance/verify_p110.json)、[日志](../../../evidence/v11/balance/verify_p110.log)。

| 指标 | 实测 |
| --- | ---: |
| 随机提交 | 300,000 |
| 差异 | 0 |
| 对精确有理数的最大相对误差 | 7.954742471407687E-16（日志 7.95E-016） |
| 含重复定义的构筑 | 212,289 |
| 含恰好两张的定义 | 160,350 |
| 含至少三张的定义 | 102,125 |
| 重复构筑且含 C41 | 110,244 |
| 有效 C41 正在复制重复定义 | 90,282 |
| C53 额外期望夹具 | 3 个构筑、891 个冻结结果、0 差异 |

“恰好两张”与“至少三张”可同时成立（例如三张普通卡加两张 C41），不可相加当总数。121 张计分类卡、计分类首领减益及 20 种计分效果的覆盖均通过；每张计分类卡也要求至少两个真实副本存在并触发。对拍核对精确有理数、向下取整得分、主牌型、能力集合及有序触发实例 ID。

### 20-seed smoke 与 trace

固定 **5000–5019、4 线程、planner**，没有 `--lab` 或 `--prepared`。完整日志与原始逐局记录：[run JSON](../../../evidence/v11/balance/run_p110_smoke.json)、[run 日志](../../../evidence/v11/balance/run_p110_smoke.log)。

| 指标 | 实测 |
| --- | ---: |
| 完成 / 错误 | 20 / 0 |
| 通关 | 6（30.0%） |
| 日志耗时 | 134 秒 |
| 可预测的真实提交（不含 C53） / 差异 | 505 / 0 |
| 可选命令拒绝 / 商店命令拒绝 / 预算预告差异 | 0 / 0 / 0 |
| 货架重复获得 / 全部购卡 | 16 / 269（5.95%） |
| 战利品重复获得 / 全部领卡 | 5 / 34（14.71%） |
| 死亡时：局数 / 持有张数 / 额外副本 / 含重复卡局数 | 14 / 94 / 8 / 6 |
| 通关时：局数 / 持有张数 / 额外副本 / 含重复卡局数 | 6 / 60 / 9 / 5 |
| 高压挑战：出现 / 接受 | 113 / 70（61.9%） |

重复获得最多的是 C06、C101、C28、C33，各 2 次；C06 终局最多持有 3 张。额外副本按每个定义 max(张数−1, 0) 相加，终局取真实存活实例，区别于旧版末关入场快照。完整逐卡表与战利品表由脚本生成：[smoke 表](../../../evidence/v11/balance/smoke_duplicates_loot.md)。

| 战利品 | 出现 | 选物品 | 折现 | 未选 | 领取件数 / 总额度 | 每店领取件数分布 |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| L1 | 27 | 22 | 5 | 0 | 22 / 27 | 0 件：5，1 件：22 |
| L2 | 17 | 12 | 5 | 0 | 12 / 17 | 0 件：5，1 件：12 |
| L3 | 14 | 7 | 7 | 0 | 7 / 14 | 0 件：7，1 件：7 |
| L4 | 8 | 0 | 8 | 0 | 0 / 24 | 0 件：8 |
| L5 | 11 | 3 | 8 | 0 | 3 / 11 | 0 件：8，1 件：3 |
| L6 | 17 | 12 | 5 | 0 | 20 / 51 | 0 件：5，1 件：4，2 件：8 |

这批样本中 L4 全折现，不能据此声称实际覆盖了 L4 多次领取；L6 有 8 家领取两件且正常离店。仅 smoke，不能用 30% 推断正式平衡变化。

trace：seed 5000，`--steps 80` 输出 **81 条**（含 NewRun），真实会话重放接受 81、拒绝 0；首选战利品 2 次、额外领取 0 次。整局到第 18 关失败，商店拒绝与计分差异均 0。[trace JSON](../../../evidence/v11/balance/trace_p110_5000.json)、[日志](../../../evidence/v11/balance/trace_p110_5000.log)。本轮未启动 Unity 或打包版回放。

第一次 smoke 在缓存修复后主动中断，未形成结果 JSON；仅保留 [中断日志](../../../evidence/v11/balance/run_p110_smoke_interrupted_before_cache_fix.log)。上述数字全部来自修复后的完整运行。

### 补充检查与独立复核

- 项目 Python 检查：`python -m unittest discover -s tests -v`，**222/222 通过**；[日志](../../../evidence/v11/balance/python_unittest.log)。所有临时文件与 Python 缓存限制在本 worktree（关闭字节码缓存）。
- 新报表严格配对测试：**4/4 通过**（按 seed 而非文件顺序；拒绝缺局、重复、错误、诊断失败、不同种子集及空记录）；[日志](../../../evidence/v11/balance/report_tests.log)。
- 缓存定向检查：2 个 `[C41, C53, C53]` 顺序，固定 X2 失效。两个键与实际实力都不同，每个缓存结果与独立重算一致：mean 1743.75 / 5449.21875，safe 840 / 2625。[夹具](../../../tools/balance_sim/review_cache/CacheProbe.cs)、[JSON](../../../evidence/v11/balance/cache_probe.json)、[日志](../../../evidence/v11/balance/cache_probe.log)。
- bot_duplicates 独立只读复核 Fast / Verify；fast_parity 独立复核 Runner、主 agent 的 run 汇总与报表，并执行缓存回归、从 20 局原始日志逐项重算新增统计。两者均未发现剩余阻断项。规格与制作质量、实际运行的独立结论见 [review](../../../evidence/v11/balance/review.md)。

## 协调者后续测量命令（本轮未运行）

先合入当前 worktree 外另一路实现的 C98 正式回收上限，再构建并重新验证受影响检查。当前游戏核心的 `SalePrice` 仍保留多 C98 本店套利行为；模拟器估值已保守封顶，真实结算保持原样。这是用户已告知的待合并事项，**本轮未发现新的游戏规则问题**。

从仓库根目录执行以下命令；缓存环境变量见模拟器 README。基线文件只读，已核对为 p1.0.0 / planner、种子 5000–5299、300 局、0 错误、88 胜。

```powershell
dotnet build tools/balance_sim/BalanceSim.csproj -c Release
$sim = "tools/balance_sim/bin/Release/net10.0/BalanceSim.dll"
dotnet $sim verify --n 300000 --threads 4 --out evidence/v11/balance/verify_p110_merged.json --log evidence/v11/balance/verify_p110_merged.log
dotnet $sim run --first 5000 --seeds 300 --threads 4 --out evidence/v11/balance/run_p110_300.json --log evidence/v11/balance/run_p110_300.log
python tools/balance_sim/v10_report_tables.py paired_v11 evidence/v10/balance/run_p100_final.json evidence/v11/balance/run_p110_300.json --out evidence/v11/balance/paired_p100_p110.md
python tools/balance_sim/v10_report_tables.py v11 evidence/v11/balance/run_p110_300.json --out evidence/v11/balance/duplicates_loot_p110.md
```

`paired_v11` 必须使用完整、无重复且完全相同的非空种子集；运行错误或命令 / 计分诊断失败则拒绝比较。它输出胜负配对格、通关率差的配对近似区间、到达关数差；`v11` 输出新增副本和战利品表。`--out` 直接写 UTF-8，不用 PowerShell 管道截断模拟器。

同一 seed 只表示同源随机起点，后续购买与抽样顺序会随规则 / 决策分叉。本版同时修正 C41+C53 的旧估值及经济边际估值，正式配对结果应标为“规则 + 模拟器适配”的合计影响，不宣称纯规则因果效应。未做任何平衡数值调整。

交接：代码和证据留在工作树，文件锁已释放；README 与本报告可供协调者合并并同步 STATE。R-03 正式测量及 C98 合并后的回归尚未完成，不以本报告替代。
