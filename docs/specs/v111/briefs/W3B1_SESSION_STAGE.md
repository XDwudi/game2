# W3b-1 交接包：会话层的关内行为（提交、成长、玻璃、养成、纪录）

你是这个工作包的实施者，协调者是主会话里的 Claude。请完整读完本文件再动手。

## 目标

让 `DicePrototype/Assets/Game/Core/Session/` 实现 p1.1.1 规格里**一关之内**发生的全部新行为。商店、道具、解锁命令、存档版本门禁是下一个工作包 W3b-2，本包不做。

## 输入

| 文件 | 用途 |
| --- | --- |
| `docs/specs/v111/RULES_DELTA.md` | **唯一规则依据**（§15 里“协调者裁定”的条目以裁定为准） |
| `docs/specs/v111/W3A_NOTES.md` | 上一个工作包（规则层）留下的清单：§2.1 列了会话层每一处“只做了编译适配、行为待实现”的位置，以及“编译不报错但已经与规则层脱节”的地方。**先读它** |
| `docs/specs/v111/TEST_CHANGES.md` | 旧测试改写登记表，本包继续往里登记 |
| `tools/v111_config.py`、`DicePrototype/Assets/Game/Data/P0Config.json` | 配置字段名与数值 |
| `docs/specs/v10/RULES_SPEC.md`、`docs/specs/v11/RULES_DELTA.md`、`docs/specs/P0_v0.6.md` 附录 D | 现行的事务顺序、随机流、回执 |

规则层（`Core/Rules`）已经按规格实现并提交：计分器会在 `AfterScorePlan` 里给出 `growthChanges`（含 `halve`）、`faceChanges`（含 `stateOwnerId`）、`glassRiskDieIds`、`cultivationSourceBindings`；`ControllerRules` 里有 `GrowthValue`、`FreeCultivationInstanceId`、`OrdinaryUsed(列表, cfg)`、`IsUniqueController` 等。需要规则层再提供纯函数时可以加，但不要改已有的计分行为；改了规则层就要重跑规则宿主。

## 范围内

1. **提交快照**：构造 `SubmissionSnapshot` 时填 `stageScoreBefore`（提交前本关累计分）与 `stageTarget`（本关实际生效的目标：含高压选择与首领的目标调整，减益解除后恢复）。预览与提交用同一来源（规格 §4.5 C156、§11、§12.1）。
2. **成长的执行**（规格 §5、§5.1）：`halve`（连庄换牌型，整除 2）；失效与停滞时跳过；返璞归真不改保存值；C58、C59、C129、C132（只有选了高压挑战并过关才长）、C55–C57、C62 的时机与单位；祭坛按被摧毁卡的配置基础价 × `growthEvent`；昙花一现用 `growth` 记衰减次数，到 `expireGrowth` 时移除。人员流动（C51）改用 `growth` 的那一处适配请对照规格 §5 表复核全部条件。
3. **本局最高单手与抛光匠**（规格 §5.2）：`RunState.highestHandScore`、`StageState.handRecordBroken`、成功过关时 C125 的成长；过关倍数纪录 `highestClearRatioNumerator/Denominator`（规格 §13“局与关纪录”）。调试局只维护本局自己的纪录。
4. **客串明星**（规格 §5.3）：入关时为每张真实 C137 抽明星点数、写入 `state.starPip`；随机流、顺序、失效与解除、调试构造、换关重抽。
5. **玻璃**（规格 §7.3）：取消投掷时的碎裂判定；有效提交之后按 `glassRiskDieIds` 逐颗用效果流判定 15%；判定在本次提交的其它后置效果之后；碎裂后的后继流程（下一手、结算、失败）。`GlassBreakPercent` 之类给界面的查询同步。
6. **养成的执行**（规格 §6、§6.1、§7.2）：
   - 提交后的全部改写按规格的顺序执行；C123 的减点下限 3；C36/C37 前三颗；C127 每次提交。
   - 关末：C120、C121 每颗骰三个面；C115 的新抽法（规格 §6.1 表里写了候选枚举顺序与两次 `NextIndex`）。
   - **镀金加权重**：任何途径把一个面从非金变成金时权重 +`gildedFaceWeightDelta`（上限 `maxFaceWeight`），做成一个被所有镀金入口共用的函数（本包里的调用者是 C117、C115、C127；商店改造、点金笔、战利品镀金骰在 W3b-2 接入，请把函数留成它们能直接调用的形式）。
   - **成功改写的计数**：按规格 §6“改写与计数”，比较执行前后五项永久属性；`FaceState.cultivated` 置真；参与的每个有效来源的真实所有者 `cultivationFacesChanged` +1（上限 24）；被模仿时记给被模仿的真实卡；回执逐面记录。养成卖价由规则层的 `SalePrice` 读这个计数，已经实现。
7. **首领减益**（规格 §10）里属于关内的部分：失效名单在祭坛与替罪羊之后、其它关初技能之前冻结；破咒符解除后的恢复；`GrowthHalf` 只影响读取。凡人与万籁俱寂的名单由规则层的 `DisabledBy` 给出，核对会话层没有另算一份。
8. **读档校验**（`GameSession.Validation.cs`）里与本包新增状态有关的部分：`highestHandScore`、`handRecordBroken`、过关倍数、`starPip`、`cultivationFacesChanged`、`FaceState.cultivated`、`growthCatchup` 的合法范围与相互关系。存档结构版本号与旧档门禁属 W3b-2，不动。
9. **调试场景**（`DebugScene.cs`、`GameSession.Debug.cs`）：能构造并往返本包新增的状态（明星点数、成长与追赶值、养成计数、纪录）。
10. **测试**：
    - 新文件 `DicePrototype/Assets/Game/Tests/EditMode/Session/V111StageTests.cs`（连同 `.meta`，GUID 随机、格式照同目录其它 `.meta`）。把规格 §5–§7、§10 里属于会话层关内行为的“例”逐条写成测试，另加：C156 的预览与提交不再抛错；连庄 ×1.75 → ×1.60 → ×1.52 的完整执行；返璞归真下保存值不变；C132 普通挑战不长；抛光匠同分不刷新、每关只长一次、失败不长；明星点数的两张卡各抽各的与模仿共用；玻璃票 14 碎 15 不碎、留池不判、封印仍判；镀金从权重 1 到 2、权重 5 不再加；养成计数到 4 卖价 +1、到 24 封顶；C115 空池不抽；C123 的 4→3、5→3、6→4。测试名用 `V111_` 前缀；`[TestCase]` 不要用数组参数。
    - 旧的会话测试（`Tests/EditMode/Session/*.cs`）：与新规则冲突的逐条按规格改写期望值并登记到 `TEST_CHANGES.md`；测已移除卡牌（C90、C128、C145）的用例改写成测对应的新规则或登记为退役（写明原因，保留函数并标 `[Ignore("p1.1.1: …")]` 不算删除，但只允许用于被移除的卡）。不删测试、不降阈值。
    - 依赖黄金样例文件的测试保持原样。
    - 属于 W3b-2（商店、道具、解锁、存档版本）的旧测试如果因为本包以外的原因失败，不要动，在报告里列出。

## 范围外

- 商店附魔曲线、保底卡不带附魔、乘法卡货架去重、追赶值的**发放**（货架与战利品生成时写 `growthCatchup`）、自选改造、双面改造目标、牌型升级价与上限、钥匙串与道具次数、解锁命令、回心转意、存档结构版本号、旧档门禁。
- 界面与表现层的任何新功能（`UI/`、`Runtime/`），只做编译适配。
- 黄金样例文件与 `tools/v*_golden.py`：不读、不改。
- 平衡模拟器。Unity 编辑器。
- 改设计或改规格。规格疑点记进备注，按最保守的读法实现并标出来。

## 环境、提交、备注

- Windows PowerShell 5.1（没有 `&&`、`||`）。git：`C:\Users\f\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe`。.NET SDK 10 在 PATH。
- 会话测试宿主：`dotnet run --project DicePrototype/Assets/Game/Core/Session/Validation~/SessionValidation.csproj -c Release`（先读它的 README 弄清它跑哪些测试、输出什么）。规则宿主：`dotnet run --project DicePrototype/Assets/Game/Core/Rules/Validation~/RulesNUnitValidation.csproj -c Release`，它会改写被跟踪的 `evidence/v05/rules/nunit-managed-report.json`，提交前 `git checkout --` 还原；会话宿主如果也改写了被跟踪的证据文件，同样还原，并在报告里说明是哪些。
- 开工先各跑一遍两个宿主，记下基线（规则宿主现在是 1825 条里 691 通过、1134 条黄金对拍失败；会话宿主的基线你来测）。
- 同一个检出里此刻有另一个代理在写 `tools/v111_golden.py`、`Data/GoldenScoresV111.json`、`tests/test_v111_golden.py`、`evidence/v111/rules/`、`docs/specs/v111/W4_OPEN_READINGS.md`。**不要读、不要碰这些路径**，提交时只 `git add` 你自己改过的具体路径，提交前 `git status` 确认暂存区里没有别人的文件。
- 分小块提交，每块都能让两个宿主编译。提交信息写到临时文件用 `git commit -F`，最后一行加 `Co-Authored-By: Claude Fable 5.2 <noreply@anthropic.com>`。不推送、不切分支、不改写历史。
- 备注文件 `docs/specs/v111/W3B1_NOTES.md`：规格疑点与处理；留给 W3b-2 的接口与待办；留给界面工作包的查询接口清单；没做完的事。

## 最后的报告（中文）

1. 起止提交与提交清单。
2. 两个宿主的基线与最终结果（总数、通过、失败）；失败逐条归类：黄金对拍 / 属 W3b-2 范围 / 其他。“其他”必须为零，否则说明。
3. 新测试条数，覆盖了规格哪些例子，哪些没写及原因。
4. 改写与退役的旧测试条数。
5. 规格疑点（最重要的在前）。
6. 分清运行验证过的事实与推断。
