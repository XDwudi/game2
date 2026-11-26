# W7a 备注：现有界面的显示跟上 p1.1.1

工作包 W7a，交接包 [briefs/W7A_UI_DISPLAY.md](briefs/W7A_UI_DISPLAY.md)。基线 `f72f64ba`。本包没有启动 Unity，没有改场景、Prefab、项目设置，没有改 `Core/Rules`、`Core/Session`、`tools/v111_config.py`、`P0Config.json`、黄金样例、`tools/balance_sim/`。会话层没有加查询接口。

## 1. 改了哪些界面（按玩家看到的归类）

### 1.1 得分：筹码、明细、公式串、统计

所有读数都从同一个因子列表取：`Runtime/Presentation/ScoreFactors111.cs`（新文件）。顺序是 基础分、牌型、红组、加成 `1 + A`、卡牌（各乘法卡的乘积）、道具、首领；连乘 = `scoreNumerator / scoreDenominator`。颜色、黄金面、卡牌加成、倍率附魔是“加成”一项的条目，不再各自成项。

| 界面 | 位置 | 现在显示 | 数据来源 |
| --- | --- | --- | --- |
| 对局内筹码行 | `UI/GameApp.Play11Score.cs` `Factors11` | 基础分、牌型、红组、加成 ×(1+A)、卡牌、道具、首领；×1 的项不出筹码（沿用原规则） | `ScoreFactors111.List(b, true)` |
| 同上，演出时点亮 | `ChipLit11` | 加成筹码在有卡牌或附魔加成时随卡牌一拍点亮，否则随牌型、红组一拍点亮 | `additiveEntries[].emitterId` |
| 卡牌上的得分角标 | `CardContribution11` | 卡牌给的加成写“加成+1.5”，不与基础分加值相加 | 回执事件 `controller_additive`、`enchantment_additive` |
| 得分明细（抽屉） | `UI/GameApp.Art07ScoreDrawer.cs` | 顶部公式串按上面的顺序；“整手计算”里“加成”一行始终在（没有加成时 ×1），下面逐条展开来源、数额和累计 | `ScoreExplanation.Build` 的 `additive` 行与 `entries`；`BonusSource111` |
| 逐步明细 | `UI/GameApp.Modals.cs` `ShowBreakdownComplete07` | 同上，加成行下缩进列出每条 | 同上 |
| 高分庆祝的公式串 | `Runtime/Presentation/HighScore073.cs`、`UI/GameApp.Art073HighScore.cs` | “基础 … × 牌型 … × 红组 … × 加成 … × 卡牌 … × 道具／首领” | `ScoreFactors111` |
| 结算页“最高倍率” | `UI/GameApp.Art07Results.cs` | 牌型 × 红组 × (1+A) × 卡牌（不含道具、首领，与原口径相同） | `ScoreFactors111.Multiplier(b, false)` |
| 上一手结果栏“总倍率” | 回执 | 含道具、首领，基础分 × 总倍率 = 取整前得分 | `ScoreFactors111.Multiplier(b, true)` |
| 旧版六筹码轨（v0.73 布局） | `UI/GameApp.Art07.cs` | 基础、牌型、红组、加成、卡牌、道具与首领 | 同上 |
| 调试面板“独立乘区” | `UI/GameApp.Debug.cs` | 基础、牌型、红组、加成及其条目、卡牌、道具、首领 | `BonusBreakdown111` |
| 提交后的提示 | `Runtime/GameApp.cs` | “附魔触发：加成 +1.5 共 N 次” | `enchantRules.scoreAdd100` |

**一处需要协调者定的口径**：交接包写“A 为 0 时仍显示 ×1”。我只在明细的行里这样做（规格 §13 说的是明细那一行）。筹码行和单行公式串沿用原来“×1 的因子不写”的规则，所以没有加成时不出“加成 ×1”筹码。原因是 `HighScore073Tests` 有一条名字就叫“只列改变了得分的因子”的断言（期望只有 `23`），改它等于改掉这条测试的本意。如果希望筹码行也常驻“加成 ×1”，改 `ScoreFactors111.List(b, true)` 的调用处即可，那条测试要一起改。

演出回执（`PresentationReceiptAdapter`）：加成行拆成每条加成一个事件（`color`、`gold`、`controller_additive`、`enchantment_additive`，都是在加成合计上做“+”），最后一个 `additive` 事件乘 `1 + A`。卡牌和附魔给的加成计入“生效 N 张”，卡牌会被点亮。原来的 `enchantment_multiplier` 事件改名 `enchantment_additive`，附魔的弧线仍落在 `Factor5`，这个筹码现在是“加成”。

### 1.2 卡牌的当前值

统一入口 `UI/GameApp.Display111.cs` `CardValue111`，文字是“加成 +X”“得分 ×N”“基础分 +N”。

| 内容 | 现在显示 | 数据来源 |
| --- | --- | --- |
| 成长卡（15 张，含人员流动） | 牌桌角标与状态行“当前 加成 +1.4”等；首领“返璞归真”生效时按减半后的值，详情里加一句说明 | `ControllerRules.GrowthValue(cfg, id, state, halved)`，`halved` 取自 `Session.CurrentModifierId` 是否带 `GrowthHalf` |
| 商店货架、战利品里的成长卡 | 角标是买入时的值（含追赶值）；详情“买入时 加成 +3（含起步补偿 +2）” | 报价的 `controller.state.growthCatchup` |
| 昙花一现 | “加成 +4”随过关递减 | `initialAdditive100 − growth × decayPerClear100` |
| 收藏家 | “加成 +永久骰数 × 0.5” | `additivePerPermanentDie100` |
| 客串明星 | “本关明星点数 N”；没有值时“明星点数待入关”；模仿读被模仿卡的值 | `state.starPip`（来源的状态） |
| 绝地反击 | 关内显示“加成 +5 生效中”或“累计已过半，不生效” | 本关累计分、目标、`targetDivisor` |
| 成长与衰减的记录文字 | “深蓝：成长 加成 +1 → 加成 +1.4”“连庄：牌型换了，长出的部分减半，…”“昙花一现：已过 N 关，加成 +3 → +2” | 回执的成长值 + 卡牌的追赶值（卡已不在时按 0） |
| 养成卡 | 卡牌详情“当前状态”里：养成卡、是否就是不占栏位的那一张、已改写面数、卖价加成；顺序面板里“养成卡 · 不占栏位”；改写够 4 面后状态行带实际售价 | `isCultivation`、`FreeCultivationInstanceId`、`cultivationFacesChanged`、`ControllerSalePriceIn` |
| 被养成卡改写过的面 | “我的骰子”选中面的说明里加“被养成卡改写过” | `FaceState.cultivated` |

卡牌详情与卖出确认里的卖价一直读 `Session.GetControllerSalePrice`，已经含养成加价，没有改。

### 1.3 颜色、附魔、黄金、玻璃

| 界面 | 现在显示 | 数据来源 |
| --- | --- | --- |
| 商店货架的颜色升级、指定升级、战利品的颜色升级 | “加成 +0.75 → +1.25”（牌型升级仍是“×… → ×…”） | `Color.baseAdd100`、`addPerLevel100` |
| 手册“颜色与红组”页 | “加成 +X（下一级 +Y）”，规则说明里写 0 级值、每级增量、最高级 | `HandbookRow.currentAdd100`、`baseAdd100`、`addPerLevel100` |
| 手册“进阶规则”页 | 计分顺序、成长值、再计一次、牌型倍率加值、栏位各行按 p1.1.1 改写；末尾新增三行：加成、养成卡、黄金面与玻璃骰（加在最后，原有行号不变） | 配置 |
| 附魔名与说明 | “基础分 +15”“加成 +1.5”（原“基础 +10”“得分 ×2”） | `enchantRules.baseAdd`、`scoreAdd100` |
| 镀金（改造预览） | 预览的面带上新权重；提示“朝上提交时加成 +0.5；权重 +1（上限 5）”，权重已满时写“权重已到上限 5” | `goldAdd100`、`gildedFaceWeightDelta`、`maxFaceWeight` |
| 玻璃（改造预览两处） | “计分 ×3；提交后每颗有 15% 概率碎裂，留在骰池里不会碎” | `Session.GlassBreakPercentIn` |
| 提交前的预计牌型 | 锁了玻璃骰时第二行加“玻璃 N 颗，提交后每颗有 15% 概率碎裂” | `PreviewScore().afterScorePlan.glassRiskDieIds` |
| 钥匙串详情 | “现在可以使用（还剩 N 次，用完才消失）” | `OwnedItem.usesRemaining` |

### 1.4 已移除与新增内容

- C90、C128、C145：界面、图鉴、调试候选都按配置列表生成，配置里已经没有它们，没有残留的写死引用（搜过）。对应的回执分支（`DieGlassed`、`RandomUpgradeMissed`）已删。
- C154、C155、C156、I13：文字都从配置读，图标走现有的缺图占位（`MissingIconLabel11`）。没有造图。
- 调试面板去掉了人员流动的“本体倍率”输入框（规则不再读那个字段），成长值输入框的说明改成新单位并显示换算后的值。`PlayerVerification.V06.cs` 里往那个输入框填值的一步改成填成长值。

## 2. 没有专门演出的新回执

| 回执 | 处理 |
| --- | --- |
| `ControllerGrowthHalved` | 演出事件文字“连庄 换了牌型，成长减半 → 得分 ×1.32”，计分记录与结算明细里有同样的一行；适配器把它挂到那张卡上。`ControllerGrowthReset` 的分支已删 |
| `FaceCultivated` | 演出事件文字“养成卡改写骰面”，后置摘要“养成卡改写 N 个骰面”；得分明细底部的“计分后发生了”和结算明细里每张卡合成一行“拔苗助长：改写 9 个骰面，累计 13 个”（逐面的变化已经由 `FaceChanged` 各行写出，不再每面重复一行） |
| `StarPipDrawn` | 没有演出，也不进任何列表（入关回执本来就不列出）；入关后卡面直接显示“本关明星点数 N”。文字函数里备了一行，回执照常进动作日志 |
| 提交回执里的 `DieBroken` | 后置摘要“N 颗玻璃骰碎了（提交栏格数不变）”、计分记录里逐条一行、碎裂音效。**没有碎片演出**，见第 5 节 |
| `HandRecordBroken` | 得分明细底部的“计分后发生了”和计分记录里有一行“本局最高单手：a → b” |
| `ClearRatioRecord` | 文字函数里备了一行；结算明细只列卡牌来源的变化，所以现在不显示。结算页的纪录展示属于 W7b |
| `TurnoverGrown` | 文字函数里备了一行（“人员流动：卖出卡牌，加成 +1 → 加成 +1.8”）；卖卡后牌面的值即时更新 |
| `FaceChanged` 的 `weightDelta` | “权重 a → b”，没变时带“（已到上限）” |
| 抛光匠（C125）关末成长 | 结算明细“结算时的其他变化”原来的来源名单里没有 C125，已加上 |

投掷时的碎裂演出代码（`PresentRollEffects07`、`BreakDie07`、轮廓）没有删：规则不再在投掷时产生 `DieBroken`，这段代码现在不会被触发，留给 W7b 接到提交上。

## 3. 卡面与首领文字的问题（在 `tools/v111_config.py`，我没有改）

1. **C63 收藏家** 详情“临时骰和本手碎掉的骰子不算”：玻璃改到计分之后才判，本手计分前不会有骰子碎掉，“和本手碎掉的骰子”应删。
2. **B22 褪红**“红组倍率与全红倍率固定×1”、**B23 褪蓝**“全蓝倍率固定×1”、**B36 色盲**“颜色倍率与红组倍率全部固定×1”：颜色现在是加成不是倍率，按 §2 应写成“全红加成清零”之类（红组倍率 ×1 不变）。
3. **B06 沉默** 详情“基础分、倍率附魔不触发”：附魔名已是“加成增加”，“倍率附魔”应改“加成附魔”。
4. 用语：交接包写卡面用“整手得分 ×N”，但术语检查规则 `score-total` 禁止“整手得分 ×”（规定写“得分×N”），配置里的卡面也写“得分×N”。界面按规则与卡面写“得分 ×N”。
5. 短文字最长 27 字（C07、C12、C54），没有超过原有最长值。

## 4. 测试

运行过（.NET 10 宿主）：

| 检查 | 结果 |
| --- | --- |
| 规则宿主 `RulesNUnitValidation.csproj` | 3982 / 3982 |
| 会话宿主 `SessionValidation.csproj` | 616 / 616 |
| 表演宿主 `Runtime/Presentation/Validation~/PresentationValidation.csproj` | 2374 / 2374（其中 `DISPLAY111-` 2157 条、`ART07_RECEIPT_` 160 条）。本包之前这个宿主编不过（缺 `V111Golden`），本包补了编译项 |
| 夹具预检 `FixturePreflight.csproj` | 11 个场景在真实会话上通过。本包之前在 G147 处抛错 |
| `UnityTestsCompilation.csproj` | 5 个错误，与基线相同（`Art07ResourceDiagnosticPlayModeTests`），没有新增 |
| `tests/test_v21_title_chars.py`、`test_v21_linebreak_words.py`、`test_v11_glossary_check.py` | 100 条全过（前两个文件本包之前各有 1 条失败） |
| `tools/v11_glossary_check.py`（全仓扫描） | 违规 0，失效允许项 0 |

规则宿主会改写 `evidence/v05/rules/nunit-managed-report.json`（已 `git checkout` 还原）；会话宿主写出未跟踪的 `evidence/v05/session/report.json`（已删）；表演宿主每跑一次写一个 `managed-<时间>.json`（已删）。表演宿主与夹具预检共用一个 `obj` 目录，换着跑时要先 `dotnet build --no-incremental`，否则会跑到另一个工程的程序。

**只编译过、没有运行的**（都要在 Unity 里跑）：

- 全部 PlayMode 测试；本包改写的清单见 [TEST_CHANGES.md](TEST_CHANGES.md) 的 W7a 一节。
- EditMode 里不在 .NET 宿主的：`V21WrapTests`（条数改成 185）、`V21TitleFontAssetTests`（没有改，见下）、`Tests/EditMode/Presentation` 里其余文件。
- 新文件的 `.meta` 是手写的（随机 GUID）：`ScoreFactors111.cs.meta`、`V111DisplayTests.cs.meta`、`GameApp.Display111.cs.meta`。

**预计在 Unity 里会失败、不属于本包的**：

- `V21TitleFontAssetTests` 的 `FW02`、`Q5`：字表变了（标题字表新增“匙反意绝转钥”，粗体字表新增“做偿匙反惩管纪罚钥零”），四套主题标题字体与粗体的静态图集要在 Unity 里按 W21 的流程重烘。字表文件本包已重生成。
- 图标张数 184 的断言（`GameplayPlayModeTests.cs:133–134`、`V13CardTemplatePlayModeTests.cs:441`、`W22iArtInstallPlayModeTests.cs:101`）：等 W8 新图入库。
- 其它因数值变化而过时的 PlayMode 断言：本包按旧文案、旧数值、旧回执搜索并改写，没有逐个读完全部 PlayMode 文件，不能保证没有漏。

## 5. 留给 W7b 的事

- 交接包列的新交互：自选改造面板、一次选两个面、钥匙串与解锁骰子（现在点“使用道具”会不带目标发命令，由会话拒绝）、钥匙串图标上的剩余次数、结算页纪录、主菜单历史最高、得分演出分档。
- **玻璃提交后碎裂的碎片演出**：现在只有文字和音效。把 `BreakDie07` 与轮廓接到提交的后置节拍上之后，[TEST_CHANGES.md](TEST_CHANGES.md) 里“这四处少了的检查”要在提交路径上补回来。
- 演出里加成行的位置：一手里有四张以上有来源的卡时，“加成 ×(1+A)”事件落在最后的合并节拍里，会排在前四张卡的乘法之后。数值不受影响，只是先后顺序；做演出分档时可以一起调。
- 加成行的收起与展开：现在明细里始终展开。条目很多时（十几张加成卡）明细会变长，需要的话加一个开关。
- 养成标签现在在卡牌详情和顺序面板里，卡面本身没有标记；是否要在卡面上加角标是版式问题。
- `Editor/Art07AssetLibrary.cs` 生成的得分 Prefab 字段还是“基础、牌型、颜色、红组、控制器、附魔”六项。运行时没有地方往这几个字段写值（只有一条资源库测试用到），重生成 Prefab 属于编辑器操作，没有动。

## 6. 需要在 Unity 里实看的画面

| 屏 | 看什么 |
| --- | --- |
| 对局，锁好骰子（预览） | 筹码行顺序“基础分 × 牌型 × 红组 × 加成 × 卡牌 = 总分”；全蓝单颗时出现“加成 ×1.25”；筹码多时不重叠、名字不被截 |
| 对局，提交时的演出 | 给加成的卡牌被点亮并出“加成+X”角标；附魔弧线落到“加成”筹码；加成筹码点亮的时机 |
| 点任一筹码 → 得分明细 | “加成（共 N 项）”一行和它下面缩进的各条（来源、“加成 a + b”、累计）；没有加成时是“加成（没有加成）… × 1”；窄屏大字号（1366 宽、150%）下三列不挤 |
| 得分明细 → 逐步明细 | 加成行下的缩进各条 |
| 牌桌上的卡牌 | 成长卡角标“加成 +1.4”“得分 ×1.35”“基础分 +15”是否放得下（比原来的“×1.35”“+24”长）；客串明星“本关明星点数 4”；绝地反击“加成 +5 生效中” |
| 商店货架 | 成长卡角标（含追赶值，第 3 章以后的商店能看到比起步值高）；客串明星“明星点数待入关”；颜色升级行“Lv.0 → Lv.1 · 加成 +0.25 → +0.75”是否一行放得下 |
| 卡牌详情（点一张成长卡、一张养成卡） | “当前状态”里的成长值、起步补偿、养成卡几行；附魔一栏“加成 +1.5：…” |
| 商店改造选面（镀金、脆化） | 预览“… → 5 点红面（黄金、权重 2）”；提示两句的长度，尤其 150% 字号 |
| 手册 | “颜色与红组”页两行的“加成 +X（下一级 +Y）”和说明是否放得下；“进阶规则”页末尾新增的三行 |
| 提交一手带玻璃骰 | 预计牌型第二行“全蓝 · 玻璃 2 颗，提交后每颗有 15% 概率碎裂”有没有被自动缩得太小；碎了之后后置摘要的文字；骰子消失的时机（现在没有碎片演出） |
| 首领“返璞归真”关 | 成长卡的值是否按减半显示，详情里那句说明 |
| 高分庆祝 | 公式串“基础 … × 牌型 … × 加成 … × 卡牌 …” |
| 结算页、整局结果 | “最高倍率”读数；结算明细里成长、减半、养成的各行文字 |
| 图鉴／卡面版式画廊 | 角标样例换成了“得分 ×1.5”“基础分 +12”“加成 +2.5”“基础分 +120”，看最长的一条是否溢出 |
| 调试面板 | “独立乘区”一段；成长值输入框的新说明 |
