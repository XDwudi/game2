# W3b-1 备注：会话层的关内行为

工作包 W3b-1（[交接包](briefs/W3B1_SESSION_STAGE.md)）。依据 [规则增量](RULES_DELTA.md)。基线 `528fd93a`，分支 `codex/v1.1.1`。旧测试改写见 [TEST_CHANGES.md](TEST_CHANGES.md) 的 W3b-1 一节。

四部分：规格疑点与处理、留给 W3b-2 的接口与待办、留给界面工作包的查询与回执清单、没做完的事。都没有改规格。

## 1. 规格疑点与处理

按重要程度排。每条写明现在按哪种读法实现。

| 编号 | 疑点 | 现在的实现 | 影响 |
| --- | --- | --- | --- |
| S01 | 明星点数什么时候清掉。§12.1 写“入关后为 1–6，未入关为 0”，§5.3 只写“换关重抽” | **成功结算时**把所有卡的 `starPip` 清 0；商店与下一关预览阶段 C137 没有值，下一关入关重抽。失败（`RunFailed`）不清 | 界面在商店里显示“待入关”。读档校验因此要求：不在关内的任何卡 `starPip` 必须是 0 |
| S02 | 调试场景里 C137 没给点数（0）怎么办 | 套用调试场景**从不抽数**；0 表示本关没有值，C137 不触发。给 1–6 就按给的用。正常局关内的真实 C137 必须是 1–6 | 调试界面要让人能填这个值，否则调试关里的 C137 不生效 |
| S03 | “一次技能执行”怎么切分（§6 改写与计数） | C07、C26、C15、C38 各算一次执行，记给各自第一个触发来源；C36／C37 合在一起算一次（净和），一个面变了就给每个触发的 C36、C37 来源各记 1（裁定 A13）；`faceChanges` 里同一个发出者连续的条目算一次执行，执行完比较该发出者计划改的每个面。所以两张同名卡先后改同一面时，后一张只有在它自己又改动了才计（两张 C122：第一张 1→5 计 1，第二张 5→5 不计；两张 C124：1→3、3→5 各计 1） | 独立参考实现要用同一口径 |
| S04 | 卡牌改写临时骰算不算、标不标 | 临时骰照 p1.1.0 **照常被改写**，但不计数、不置 `cultivated`（按“永久骰物理面”读） | 读档校验：正常局的临时骰不能带养成标记 |
| S05 | C115“各一次 `NextIndex`”在候选只有一个时抽不抽 | 照抽：每步固定两票（面一票、改造一票），`NextIndex(1)` 也推进效果流。候选面“当前至少可做一项”用商店的合法性判定（U03 非红、U04 非蓝、U06 非金、U08 点数小于 6）。`upgradeIds` 按 ID 的 Ordinal 升序 | 种子复现依赖这个口径。回执不再有“落空” |
| S06 | 失效的 C53 抽不抽票（裁定 A06 说不抽，但不在交接包十项里） | 已按 A06 实现：失效的真实 C53 不抽票、不进 `resolvedRandomChoices`。归在第 7 项（首领减益的关内部分）下 | p1.1.0 的代码会抽，种子复现以本版为准 |
| S07 | 最后一颗骰因玻璃碎掉时的失败原因 | 未达标且还有提交次数 → `RunFailed`／`NoDice`；提交次数同时用完 → 沿用 `SubmissionsExhausted`；已达标 → 先成功结算，再由结算判后继（末关 `RunWon`，否则无永久骰 `RunFailed`） | 界面的失败文案有两种原因 |
| S08 | 调试局的“本局自己的纪录”从哪里起算 | 调试工作区从正式局克隆时带着该局当时的最高单手与过关倍数，之后只改自己这份；调试场景可以显式写这三个值与本关标记。**正式的历史最高单手 `historicalHighestHandScore` 不在 `RunState` 里，本包没有实现**（见第 4 节） | 主菜单的历史最高要由存储层另做，并忽略调试来源 |
| S09 | 过关倍数用哪个目标 | 结算当时的 `stage.target`（高压、首领百分比都算在内；减益已解除则是恢复后的目标），与结算回执里的 `target` 同一个值 | 无 |
| S10 | 连庄换牌型时事件累计已是 0 | 规则层照列 `halve` 计划（W3a N07）；会话执行后值没变就不写回执，只有真的减了才有 `ControllerGrowthHalved` | 回执比计划少一条 |
| S11 | 停滞与失效对祭坛、抛光匠、昙花一现 | 祭坛在失效名单冻结之前献祭并成长，之后被缴械或停滞都不回退（§5.1“祭坛照先献祭、后确定失效”）；抛光匠、昙花一现走统一的 `Grow`，失效或停滞时不动 | 无 |
| S12 | C62“每次成功投掷后立即 +1” | 在同一个投掷事务里先加再掷，结果等价（本手提交冻结时已生效） | 无 |
| S13 | C07 复制时玻璃材质变了算不算改写 | 不算：只比较五项面属性。复制改成逐面逐字段写入（不再整体替换面数组），这样目标自己的历史标记保留、源的标记不带过来 | 目标骰的面数组顺序保持原样（p1.1.0 是换成源的顺序，两者都是 F01–F06） |
| S14 | 读档校验能钉到多细 | 格式与范围对所有档都查；“相互关系”只对正常局查（调试局可以任意构造）：没有提交过的局不能有纪录；最高单手不低于任何已提交回执的得分；已结算的关不高于最高过关倍数；第一关结算前不能有过关倍数；关内的真实 C137 必须有 1–6；追赶值是该卡成长类型每周期步长的整数倍且不超过 7 步。养成计数与面标记之间没有可查的关系（带标记的面会随骰子被移除，一个面可以被改很多次），没有钉 | 调试场景里不合整步的追赶值是允许的 |
| S15 | 点金笔 I11 的镀金加权重 | 按交接包留给 W3b-2 接入，本包没有接。**在 W3b-2 完成之前，点金笔镀金不加权重**，与规格 §7.2 不一致 | 见第 2 节 |

## 2. 留给 W3b-2 的接口与待办

- **镀金入口**（`GameSession.Submission.cs`）：`GildFace(RunState, DieInstance, FaceState, sourceId)` 把非金面变金并加权重（上限 `maxFaceWeight`），写两条 `FaceChanged` 回执（`gold`、`weightDelta`），返回是否新镀金；`Gild(FaceState)` 只改状态不写回执，给还没进骰池的报价骰用。待接入：`ApplyUpgrade` 的 `U06`（货架、自选、赠礼、战利品改造）、`UseItem` 的 `I11`（现在直接调 `ApplyFaceChange(…"gold"…)`）、`GenerateSpecialDie` 的模板 2（L5 两个黄金面）。
- **养成计数入口**：`Rewrite(RunState, CultivationSourceBinding[], IEnumerable<FaceRef>, Action)` 只给卡牌技能用。商店改造和道具不是卡牌改写，不要走它（不置 `cultivated`、不计数）。
- **追赶值的发放**：没有做。`Buy` 里取得卡牌时 `acquired.state = new ControllerDynamicState()` 会把报价上的 `growthCatchup` 丢掉，W3b-2 要改成继承；货架（`AppendControllers`）与 L1/L2 战利品（`AddLootController`）生成时写入 `(周期 − 1) × 步长`。读档校验已经准备好：报价允许成长卡带非负追赶值，不允许带明星点数与养成计数；持有的卡（正常局）由 `ValidateCatchup` 查整步与上限。报价上的追赶值目前只查“非负、只在成长卡上”，W3b-2 可以对报价也调 `ValidateCatchup`。C112 的直接赠卡没有追赶（现状即如此）。
- **C51**：已对照 §5 复核，条件与 p1.1.0 相同，只是写入 `growth`（每次 +80），回执 `TurnoverGrown` 补了 `oldValue`。`turnoverUnits2` 的校验仍是 p1.1.0 的写法（C51 允许 ≥3），规格说它“恒为配置初值”，可在改存档结构时收紧成恒等于 3。
- **C60、C61、C134**：只是单位随规则层改了。C60“一笔只记一次（自选、双面、镜像、整骰）”、C134 按钥匙串次数、免费解锁不算，都没做。
- **没有加的状态**：`HandState.unlockCardUsed`、`OwnedItem.usesRemaining`、`ShopState.selfDieUpgradeUsed`、`SessionCommand` 的 `UnlockDie`／`CustomDieUpgrade`／`targetFaceIds`。本包新增的 `RunState.highestHandScore`、`highestClearRatioNumerator/Denominator`、`StageState.handRecordBroken` 与调试场景的四个同名字段已经进入克隆、JSON、状态哈希和校验；存档结构号与旧档门禁没有动。
- **仍失败的 19 条旧会话测试**都属 W3b-2，清单与原因在 [TEST_CHANGES.md](TEST_CHANGES.md) 末尾。其中 `V10_EnchantedLoot…` 与 `V10_GiftControllers…` 现在停在成长单位的断言上（C61 4→100、C60 2→50），改完单位后还会碰到 L5 镀金骰与 C60 的新口径。
- 会话测试宿主（`Validation~/Program.cs`）不认 `[Ignore]`，会照跑被忽略的方法。本包没有用 `[Ignore]`，所以没改宿主；以后要退役测试得先让宿主跳过它。

## 3. 留给界面工作包的查询与回执

状态与查询：

| 项 | 读法 |
| --- | --- |
| 本局最高单手、最高过关倍数 | `RunState.highestHandScore`；`highestClearRatioNumerator` / `highestClearRatioDenominator`（已约分，零为 0/1）。本关是否刷新过：`StageState.handRecordBroken` |
| 明星点数 | `ControllerInstance.state.starPip`，0 = 尚无本关值（商店、预览、调试未给）。模仿读它绑定目标的值 |
| 成长值 | `ControllerRules.GrowthValue(cfg, id, state, halved)`；`state.growthCatchup` 是追赶部分。`IsGrowthAdditive`／`IsGrowthMultiplier`／`IsGrowthBase` 区分三类 |
| 养成 | 计数 `state.cultivationFacesChanged`（0–24）；面标记 `FaceState.cultivated`；实际回收价 `GameSession.ControllerSalePriceIn(view, card)` 已含养成加价；免栏位的那张 `ControllerRules.FreeCultivationInstanceId` |
| 玻璃 | `GlassBreakPercentIn(view)` 现在是“提交后碎裂”的概率（15）。本手会判哪些骰：`PreviewScore().afterScorePlan.glassRiskDieIds` |
| 预览 | `PreviewScore()`／`PreviewScoreRange()` 在持有 C156 时不再抛错；`PreviewSubmissionEffects()` 现在还会列出 `FaceCultivated`，不含玻璃判定与纪录 |

回执 `effects` 的变化：

| kind | 字段 | 说明 |
| --- | --- | --- |
| `HandRecordBroken`（新） | `detail` = `旧值:新值` | 提交刷新本局最高单手 |
| `ClearRatioRecord`（新） | `detail` = `旧分子/旧分母:新分子/新分母` | 结算刷新最高过关倍数 |
| `StarPipDrawn`（新） | `sourceId`=C137，`detail`=实例 ID，`newValue`=点数 | 入关抽明星点数；随机回执 kind 为 `C137Star` |
| `ControllerGrowthHalved`（新） | 同 `ControllerGrown` | 连庄换牌型减半。`ControllerGrowthReset` 不再产生 |
| `FaceCultivated`（新） | `sourceId`=技能 ID，`dieId`，`faceId`，`detail`=`发出者实例:真实所有者实例`，`oldValue`／`newValue`=所有者计数前后 | 每个来源、每个成功改写的面一条；到 24 后前后相等 |
| `FaceChanged` | `detail`=`weightDelta` | 卡牌镀金时紧跟在 `gold` 那条之后多一条（权重真的变了才有） |
| `DieBroken` | 不变 | 现在出现在 **提交** 的回执里，不再出现在投掷回执里；随机回执 `Glass` 同理 |
| `RandomUpgrade` | 不变 | 每个 C115 来源至多 3 条；`RandomUpgradeMissed` 不再产生。随机回执改为 `C115Face`（`sourceId`=发出者，值为 `骰/面`）与 `C115Upgrade`，`C115Die` 不再有 |
| `TurnoverGrown` | 多了 `oldValue` | 人员流动 |
| `DieGlassed` | — | 不再产生（C128 已移除） |

失败原因 `NoDice` 现在来自提交（玻璃全碎），不再来自投掷。

界面里已经与规则不符、本包没有动的地方：

- `UI/GameApp.Modals.cs:215`、`UI/GameApp.ShopFacePick14.cs:202` 的文案仍是“每次掷到它有 X% 概率碎掉，锁定后不会碎”，应改为提交后判定。`Tests/PlayMode/V14ShopPlayModeTests.cs:429` 钉着这句话。
- `Tests/PlayMode/Art07PostEffectsPlayModeTests.cs:168`、`UiBoundaryPlayModeTests.cs:80、228`、`Art07FxIntegrationPlayModeTests.cs:184` 断言投掷时出现 `DieBroken`，按新规则会失败。`Art07PostEffectsPlayModeTests.cs:32` 还接受 `RandomUpgradeMissed`（无害）。
- `UI/GameApp.Art07Presentation.cs:216`、`UI/GameApp.Modals.cs:425`、`Runtime/Presentation/PresentationReceiptAdapter.cs:177` 只认 `ControllerGrowthReset`，不认新的 `ControllerGrowthHalved`；`UI/GameApp.V10.cs:404` 的“落空”分支成了死代码。

## 4. 没做完的事

- **正式历史最高单手** `historicalHighestHandScore`（§12.1 最后一行）没有做。它在正式局之外保存，不属于 `RunState`，需要存储层与主菜单配合，交接包第 3 项也只列了局内的三项纪录。
- **`TwentyFourStageFlowSupportsEightRiskBranchesAndNoFinalShop` 的夹具改法需要协调者确认。** 原夹具靠一套强构筑每关一手过关；p1.1.1 数值下这套构筑按我手算约 35 万分（没有实跑验证），过不了后段目标；基线上它就失败在“提交栏放不下这么多”，我推断是周期计划变了之后抽到了会缩提交栏的首领（没有逐关查证是哪一个）。我把它改成每关入关后把累计分预设为目标、只锁提交栏容得下的骰，流程断言不变。这样它不再证明“这套构筑能通关”，只证明 24 关流程、八个高压分支、战利品门禁和末关无商店。如果希望保留“真打通关”的含义，需要等 W6 校准后重配构筑。
- 没有启动 Unity。新测试与全部改写只在 .NET 会话宿主上跑过；`V111StageTests.cs.meta` 是手写的（随机 GUID，格式照同目录）。
- `UnityTestsCompilation.csproj` 编过一次：仍是基线上那 5 个与本包无关的错误（`Art07ResourceDiagnostic` 的 `…ForTests` 成员），没有新增，说明界面、运行时与 PlayMode 测试对会话层的引用仍然编得过；它们没有运行。
- 规格里属于关内、但本包没有单独写成新测试的例子：§7.2“任一黄金面掷中概率 4/8”（概率计算在规则层，权重变化本身已测）；§10 B25 的例子与 B37 的例子由改写后的旧测试覆盖（`V10_DisabledControllers…`、`V10_EnchantSilence…`），没有再写一份；§5 C51 的例子由 `V08_C51…` 覆盖；§5.3“读档不抽”只间接覆盖（`V111_AnOrdinaryRunWithTheNewStateSurvivesAReloadAndKeepsItsHash`）。属于 W3b-2 的例子（追赶的发放、C60、C61 战利品、C134、钥匙串）没有写。
- 工作区里 `ArtSource/Art11/subjects.json` 显示为已修改，不是本包改的，没有碰，也没有提交。
