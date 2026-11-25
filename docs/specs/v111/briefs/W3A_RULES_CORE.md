# W3a 交接包：规则层（Core/Rules）实现 p1.1.1 计分

你是这个工作包的实施者，协调者是主会话里的 Claude。请完整读完本文件再动手。

## 目标

让 `DicePrototype/Assets/Game/Core/Rules/` 按 `docs/specs/v111/RULES_DELTA.md` 实现 p1.1.1 的**规则层**：配置模型与校验、计分、计分说明、卡牌规则辅助函数。会话层（`Core/Session`）的行为改动是下一个工作包 W3b，本包只做让它继续编译所必需的机械适配。

## 输入

| 文件 | 用途 |
| --- | --- |
| `docs/specs/v111/RULES_DELTA.md` | **唯一规则依据。** 协调者已审定；§15 里标了“协调者裁定”的条目以裁定为准 |
| `docs/specs/v111/BALANCE_PLAN.md` | 设计背景，只在规格不清楚时参考；数值以规格和生成器为准 |
| `tools/v111_config.py` | 已写好的配置生成器（协调者写的）。它决定了配置里的字段名和单位，C# 模型要与它一致 |
| `docs/specs/v10/RULES_SPEC.md` §5.2 与附录 A、`docs/specs/v11/RULES_DELTA.md` | 现行计分顺序与字段 |
| `DicePrototype/Assets/Game/Core/Rules/*.cs`、`DicePrototype/Assets/Game/Tests/EditMode/Rules/*.cs` | 现行实现与测试 |

基线：分支 `codex/v1.1.1`，开始时的 HEAD 用 `git log -1` 记下来写进你的报告。

## 环境

- Windows PowerShell 5.1（没有 `&&`、`||`）。git 不在 PATH：`C:\Users\f\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe`。Python：`E:\anaconda3\python.exe`。.NET SDK 10 在 PATH。
- 规则测试宿主（几秒到一两分钟）：`dotnet run --project DicePrototype/Assets/Game/Core/Rules/Validation~/RulesNUnitValidation.csproj -c Release`。它编译 `Core/Rules/*.cs` 和 `Tests/EditMode/Rules/*.cs`。它会改写被跟踪的 `evidence/v05/rules/nunit-managed-report.json`，每次提交前用 `git checkout -- evidence/v05/rules/nunit-managed-report.json` 还原。
- 会话测试宿主：`dotnet run --project DicePrototype/Assets/Game/Core/Session/Validation~/SessionValidation.csproj -c Release`。本包只要求它**能编译**（`dotnet build`），不要求它的测试通过。
- 不要启动 Unity；不要运行平衡模拟器。
- 提交信息用文件传：把信息写到系统临时目录下的文本文件（UTF-8 无 BOM），`git commit -F <文件>`。每条提交信息最后一行加 `Co-Authored-By: Claude Fable 5.2 <noreply@anthropic.com>`。

## 范围内

1. 运行 `E:\anaconda3\python.exe tools/v111_config.py` 生成新的 `DicePrototype/Assets/Game/Data/P0Config.json`。生成器有错（字段缺失、与规格不符）时可以改生成器，但**数值与卡面文字不要改**，发现数值或文字问题记到备注里。
2. `GameConfig.cs`、`ConfigJsonContract.cs`、`ConfigValidation.cs`：新增与删除的字段、严格白名单、所有写死的常量（规格 §12.3 最后一段列了清单）。
3. `Models.cs`：规格 §12.1、§12.2 列出的规则层字段（`SubmissionSnapshot.stageScoreBefore/stageTarget`、`ControllerDynamicState.growthCatchup/starPip/cultivationFacesChanged`、`FaceState.cultivated`、`ScoreBreakdown` 的 `colorAdd100/additiveEntries/additiveTotal100/enchantAdditiveCount`、`GrowthChange.halve`、`AfterScorePlan.glassRiskDieIds` 等），连同克隆。
4. `ScoreCalculator.cs`：规格 §2 的十步计分、§3 颜色与红组、§4 全部卡牌的计分侧规则、§5 成长值的读取（起步 + 追赶 + 事件累计，返璞归真减半）、提交时的成长计划、§6.1 里由计分器产生的改写计划（C36/C37 前三颗、C117、C119、C122、C123、C124、C126、C127）、黄金面进加成区、玻璃风险名单。
5. `ScoreExplanation.cs`：规格 §13 第一行（“加成”一行及其展开，颜色独立倍率行与黄金翻倍行移除），并保持它与计分结果的自检一致。
6. `ControllerRules.cs`：有效编号范围与移除编号；成长卡分类；`OrdinaryUsed` 按规格 §6 的“最左边一张没有不占栏位附魔的养成卡免栏位”（需要配置里的 `isCultivation`，签名变化时同步所有调用点）；`SalePrice` 加养成卖价；`DisabledBy` 的凡人改为最左 1 张稀有；乘法卡去重名单的查询函数；附魔相关常量。
7. `HandbookRules.cs`、`ProbabilityCalculator.cs` 等其余规则层文件里受字段改名影响的地方。
8. 新测试 `DicePrototype/Assets/Game/Tests/EditMode/Rules/V111ScoringTests.cs`：把规格 §2–§7 里凡是属于规则层的“例”逐条写成测试（加成区、黄金面、倍率附魔、千年老二两张、红组新表与四张红组卡、颜色等级、牌型减半取整、成长读取与追赶、返璞归真减半、连庄减半计划、状元插金花、绝地反击边界、小头大用、纹理大师、客串明星、养成免栏位随位置变化、养成卖价、凡人只打最左一张稀有）。测试名用 `V111_` 前缀，`[TestCase]` 的参数不要用数组（门禁会把它们打印成重名），用逗号分隔的字符串。
9. 让会话层与界面层**继续编译**所必需的机械适配（改了签名或字段名的调用点）。只做编译适配，不实现新行为；每一处都记到备注里，注明“W3b 待实现”。

## 范围外（不要做）

- 会话层的新行为：商店附魔曲线、自选改造、双面改造、镀金加权重、解锁命令、道具次数、成长事件的执行、玻璃碎裂判定、明星点数抽取、最高单手记录、养成计数的执行、追赶值的发放、乘法卡货架去重。
- 界面（`UI/`、`Runtime/`）的任何新功能。只做编译适配。
- 黄金样例文件 `Data/GoldenScores*.json` 与参考实现 `tools/v*_golden.py`：**不要改、不要读来对答案**。它们由独立方按规格重做。
- 平衡模拟器 `tools/balance_sim/`。
- 改设计。规格里的数值与规则照做；你认为规格有错、有矛盾或做不到的地方，写进备注，按最保守的读法实现并标出来，不要自行改规格。

## 旧测试的处理

`Tests/EditMode/Rules/` 里的旧测试分三类处理，不删除任何测试：

1. 与新规则无关、仍然通过的：不动。
2. 钉住旧数值或旧公式、与规格冲突的：按规格逐条改写期望值（必须能从规格手算出来，不许照着新实现的输出抄），并登记到 `docs/specs/v111/TEST_CHANGES.md`（表格：测试名、旧期望、新期望、依据的规格小节）。
3. 与黄金样例文件对拍的：保持原样，它们会失败。在报告里列出名字和失败条数，它们要等独立方重生成样例后才会通过。

## 交付与提交

- 分小块提交，每块都能让规则宿主编译（最好也让会话宿主编译）。建议顺序：配置模型与校验 → 模型字段 → 计分器 → 计分说明 → 卡牌规则辅助 → 新测试 → 旧测试改写。
- 只 `git add` 你改过的具体路径，不要 `git add -A`（工作区里有大量与你无关的未跟踪文件）。不要推送、不要切换分支、不要改写历史。
- 备注文件：`docs/specs/v111/W3A_NOTES.md`，记录：规格疑点与你的处理；编译适配清单（文件:行、原来是什么、现在是什么、W3b 要做什么）；没做完的事。

## 最后的报告（中文）

1. 起止提交哈希，提交清单。
2. 规则宿主的最终结果：总数、通过、失败；失败的逐条归类（黄金对拍 / 其他），“其他”类必须为零，否则说明原因。
3. 会话宿主与 `UnityTestsCompilation.csproj` 能否编译（后者依赖已导入的 Unity 工程，编不过就贴最后 20 行并说明是环境原因还是代码原因）。
4. 新测试的条数与覆盖了规格里哪些例子；哪些例子没有写成测试及原因。
5. 规格疑点清单（最重要的放前面）。
6. 分清已经运行验证过的事实和你的推断。
