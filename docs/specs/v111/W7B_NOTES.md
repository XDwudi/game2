# W7b 备注：p1.1.1 的新交互与新页面元素

工作包 W7b，交接包 [briefs/W7B_UI_INTERACTIONS.md](briefs/W7B_UI_INTERACTIONS.md)。基线 `437c5ce9`，分支 `codex/v1.1.1`。本包没有启动 Unity，没有改场景、Prefab、项目设置，没有改 `tools/v111_config.py`、`P0Config.json`、黄金样例、`tools/balance_sim/`。测试改写见 [TEST_CHANGES.md](TEST_CHANGES.md) 的 W7b 一节。

## 0. 进度

| 项 | 状态 | 提交 |
| --- | --- | --- |
| 1 按面改造一次选两个面 | 完成（只编译，未在 Unity 里运行） | 见提交记录 |
| 2 自选改造 | 完成（只编译，未在 Unity 里运行） | 见提交记录 |
| 3 钥匙串与撤回已锁骰子 | 完成（只编译，未在 Unity 里运行） | 见提交记录 |
| 4 纪录 | 完成（存储与文字的纯逻辑在 .NET 宿主跑过；界面只编译） | 见提交记录 |
| 5 得分演出分档 | 完成（分档规则与夹具在 .NET 宿主跑过；画面只编译） | 见提交记录 |
| 6 玻璃提交后碎裂的演出 | 完成（只编译，未在 Unity 里运行） | 见提交记录 |
| 7 W7a 留下的小项 | 完成（节拍顺序在 .NET 宿主跑过；其余只编译） | 见提交记录 |
| 8 回放工具 | 完成（.NET 回放工具跑过含新命令的记录；游戏内回放入口只编译） | 见提交记录 |
| 9 测试与实看材料 | 完成（PlayMode 测试与截图步骤只编译；字表已重生成，Python 测试跑过） | 见提交记录 |

九项都做了，但**没有一项在 Unity 里运行或实看过**：界面部分只通过了编译检查，纯逻辑部分在 .NET 宿主里跑过。哪些跑过、哪些只编译，见第 5 节。

## 1. 每个新交互的操作路径与实现位置

### 1.1 一次选两个面（§7.1）

操作路径：商店里点一件按面的骰面改造（货架、赠礼、战利品都走同一个面板）→ 在“我的骰子”里点一颗骰子 → 点六个面方块里的一个面（到这里与原来相同，面板进入确认步骤）→ 确认步骤里价格行下面多一个按钮“再选一个面（价格不变）”→ 回到六个面方块，已选的面带金圈，点另一个面 → 确认步骤同时列出两个面的“改造前 → 改造后”，按面编号升序 → “确认购买”。

- 只改一个面：不点“再选一个面”，直接确认；或在加选时点底部的“只改一面”（点已选的那个面效果相同）。
- “换一面”清空已选的面回到选面；“换一颗”或在“我的骰子”里点另一颗骰子，已选的面一并清空。
- 六个面方块上，这种改造不能改的面盖一层暗色（`Unavailable14`）。它们仍然可以点：点了之后确认步骤显示原因，“确认购买”不可用。两个面里有一个不合法时，原因前面带上那个面的名字（例如“5 点红面（黄金、权重 2）：这一面已经镀金，换一个面”），另一个面可以保留，用“换一面”重选。
- 骰子脆化（U05）不选面，镜像（U11）只能选一个面（没有“再选一个面”按钮）：上限读 `Session.MaxSelectableFaces`。
- 价格不随面数变化：价格行与确认按钮读的是同一个报价价（`GetOfferNetPrice`，自选改造读 `GetCustomDieUpgradePrice`）。

实现位置：

| 内容 | 位置 |
| --- | --- |
| 选面状态（已选面的列表、是否正在加选、面数上限、是否自选） | `UI/GameApp.ShopFacePick14.cs` `FacePick14` |
| 步骤判断 | `FacePickStep14`：正在加选时回到 `S2` |
| 点面、加选、确认 | `FacePickChooseFace14`、`FacePickAddFace14`、`FacePickConfirm14` |
| 拒绝原因 | `FacePickReason14`：合法的面问 `LegalUpgradeFaces`，整笔再问 `PurchaseErrorForFaces`（自选问 `CustomDieUpgradeError`） |
| 两个面的预览文字 | `FacePickChange14`、`FaceChange14` |
| 面方块的暗色与多个金圈 | `UI/GameApp.Shop14.cs` `FaceStrip14` 的两个新参数 |
| 成交演出（两个面） | `UI/GameApp.Art07Effects.cs` `BeginUpgradeTransaction07`：骰子转到面编号较小的那一面，左右两栏各用两行写一个面；面的来源 `CommandFaces111`（`UI/GameApp.Interact111.cs`） |

发出的命令：一个面仍用旧字段 `targetFaceId`（§12.1：等价于单元素数组），两个面用 `targetFaceIds`（面编号升序）。

测试用的只读入口：`FacePickFaces14`（已选面，升序）、`FacePickFace14`（最后选的那个面，原有）。

### 1.2 自选改造（§9.2）

操作路径：商店“升级与服务”标题栏右侧，“指定升级”左边的按钮“自选改造 · 还剩 N 次”（按钮名 `CustomUpgrade111`）→ 弹窗“自选改造 · 本店还剩 N 次”，列出 11 种改造，每行：名称与效果短句、第二行“选 1 个或 2 个面，价格相同”／“选 1 个面”／“整颗骰子”，右侧价格按钮“N 金币”→ 点价格按钮，弹窗关闭，进入与货架改造相同的选骰选面面板（面板右上角的类别写“自选改造”）→ “确认购买”发 `CustomDieUpgrade`。

- 剩余次数读 `CustomDieUpgradesRemainingIn`；用完后入口文字变成“本店自选改造已用完”并不可点。战利品未领时入口不可点（与“指定升级”相同）。
- 列表每行的价格、能不能买、为什么不能买都来自 `CustomDieUpgradeOptions()`。不能买的行价格按钮不可点，第二行改成“现在不能买：原因”。原因的写法：额度用完“本店自选改造已用完”、金币不够“还差 N 金币”、没有目标“没有能改造的骰面／骰子”、战利品未领“先领取首领战利品”。
- 选面面板里的价格与确认按钮读 `GetCustomDieUpgradePrice`，拒绝原因读 `CustomDieUpgradeError`。
- 成交演出与货架改造相同（没有货架卡片可以“飞走”，只有骰面变化的那一段）；成交后的一行摘要是“自选改造：黄金电镀（−4 金币，本店还剩 1 次） · …各面的变化”。
- 被会话拒绝时（例如额度刚好用完），提示走原来的商店拒绝提示（`QueueCommandRejection16` 认识了这个命令）。

入口的文字没有照交接包写成“自选改造 剩 N 次”，而是跟旁边“指定升级 · 还剩 N 次”同一个写法。**需要协调者定**：是否要改回交接包的原话（改 `CustomUpgradeEntryText111` 一处即可）。

实现位置：入口在 `UI/GameApp.Shop14.cs` `ShopGoods14`；列表、入口文字、原因、摘要在 `UI/GameApp.Interact111.cs`（`ShowCustomUpgrades111`、`CustomUpgradeEntryText111`、`CustomUpgradeReason111`、`CustomUpgradeSummary111`）；选面流程的自选分支在 `UI/GameApp.ShopFacePick14.cs`（`BeginCustomFacePick14`、`FacePickPrice14`、`FacePickConfirm14`）。

没有做的：列表里没有画每种改造的图标（“指定升级”的列表也没有）。以后可以配图标的位置：列表每行名称左侧。

### 1.3 钥匙串与撤回已锁骰子（§8）

操作路径（撤回）：对局里掷骰并锁定骰子之后，只要持有钥匙串或本手有回心转意在生效，点提交栏里一颗已锁定的骰子 → 这颗骰子的格子出现金圈（只是选中，没有发命令）→ 底部操作行变成：主按钮“撤回 · 回心转意（本手免费）”或“撤回 · 钥匙串（剩 N 次）”，次按钮“取消选择”，提示栏写明会发生什么（“撤回骰子 3：用掉钥匙串 1 次（现在剩 3 次），不能反悔。骰子回到骰池，可以重掷或再锁定。”）→ 点主按钮才发 `UnlockDie(dieId)`。再点一次同一颗骰子，或点“取消选择”，取消选中。

- 用哪个来源完全由 `UnlockQuoteIn(ViewState, dieId)` 的 `sourceId` 决定，界面只把它写在按钮上，没有选择控件。
- 不能撤回时主按钮写“撤回”且不可点，提示栏写“不能撤回骰子 3：原因”。原因来自 `UnlockView.error`：回心转意本手已用且没有钥匙串、首领减益“徒手”挡住钥匙串、还没掷骰、有选中但没锁定的骰子。
- 既没有钥匙串、本手也没有回心转意在生效的玩家：点已锁定的骰子仍然是打开骰子详情，和原来一样（持有来源时看骰子详情请用“查看骰子”）。
- 提交栏上方那一行：有骰子可以撤回时写“已锁定 · 点一颗骰子可以撤回”，否则仍是“已锁定 · 本手不可移回骰池”。
- 没有二次确认弹窗；任何被接受的命令（掷骰、提交等）都会清掉选中状态。

操作路径（钥匙串）：道具栏的钥匙串图标右下角有剩余次数（1–3）。点图标 → 详情里多一段“用法”，“当前状态”读会话的撤回查询；按钮写“去选要撤回的骰子”（不能撤回时是“当前不能使用”）→ 点它不发命令，关闭详情，提示栏写“点一颗已锁定的骰子来撤回。”，每颗可以撤回的已锁骰子格子上出现细金框和“可撤回”小标签 → 点其中一颗，接上面的撤回路径。

- 钥匙串用到 0 次：会话把它移出道具栏，图标随重画消失、格子空出；提示里有一行“钥匙串用完了，道具栏空出 1 格”。
- 撤回之后：界面按会话状态重画，撤回的骰子回到骰池里按创建顺序的位置，其余已锁骰子顺序不变（位置移动用骰桌已有的布局过渡，没有另写演出）。提示栏与“刚才的变化”里，`DieUnlocked` 一行（“已撤回骰子 3（钥匙串）”）、`ItemUsed` 一行（“钥匙串还剩 2 次（3 → 2）”）；免费撤回只有前一行。
- 回心转意（C154）的卡面状态：对局中显示“本手免费撤回可用”或“本手免费撤回已用”（`UnlockView.freeAvailable`、`hand.unlockCardUsed`）。

实现位置：全部在 `UI/GameApp.Interact111.cs` 的“the key ring and taking a locked die back”一段（`UnlockClick111`、`UnlockActions111`、`SendUnlock111`、`ArmKeyRing111`、`UnlockHint111`、`DrawUnlockMark111`、`ItemUses111`、`KeyRingUsage111`、`KeyRingState111`、`UnlockReason111`、`UnlockSummary111`）。接入点：`UI/GameApp.Play11.cs`（道具图标、锁定说明行、已锁骰子的点击、格子标记、操作行；都在第 164 行的术语豁免之后，豁免行没有动）、`UI/GameApp.Shop.cs` `ShowOwned`、`UI/GameApp.Shop14.cs`（商店里道具图标的次数）、`UI/GameApp.V10.cs` `ItemUnavailable10`、`UI/GameApp.Display111.cs` `CardValue111`、`UI/GameApp.Art07Effects.cs`（摘要）、`UI/GameApp.Art07Presentation.cs`（命令被接受后清掉选中）。

测试用的只读入口：`UnlockPick111`（选中的已锁骰子）、`UnlockArmed111`（钥匙串“使用”之后的指向状态）。按钮名：`UnlockDie111`、`UnlockCancel111`；标记名：`UnlockPick111_<骰子>`、`UnlockTarget111_<骰子>`、道具图标上的 `Uses111`。

以后可以配图标的位置：“可撤回”小标签现在是文字；“撤回”按钮没有图标。

**需要协调者知道的取舍**：持有来源时，点已锁骰子不再直接打开骰子详情（改成选中）。如果希望两者都留，可以改成“第二次点击打开详情”，改 `UnlockClick111` 一处。

### 1.4 纪录（§13 第四行、§12.1）

| 屏 | 显示 | 数据来源 | 位置 |
| --- | --- | --- | --- |
| 关末结算页 | “明细”按钮右侧两行，右对齐：“本局最高单手 12,345”“最高过关倍数 ×3.2”；本关刷新了哪一项，那一行加粗并带“ · 本关新纪录” | `RunState.highestHandScore`；`highestClearRatioNumerator/Denominator`；标记读 `stage.handRecordBroken` 与本次结算回执里的 `ClearRatioRecord` | `UI/GameApp.Records111.cs` `DrawSettlementRecords111`，从 `UI/GameApp.Art07Settlement.cs` 调用 |
| 整局结果页（通关与失败） | 统计格从四格变五格：总得分、**本局最高单手**（原“最大单手”，现在读会话的纪录）、**最高过关倍数**（新，`EndClearRatio111`）、最高倍率、用时。100% 字号五格一行；125%、150% 三格加两格两行 | 同上 | `UI/GameApp.Art07Results.cs` |
| 主菜单 | 菜单纸面上“退出游戏”下面一行“历史最高单手 12,345”；还没有记录时“历史最高单手 暂无记录” | 局外正式记录 | `DrawMenuRecord111`，从 `UI/GameApp.Art071Screens.cs` 调用（加在第 60 行之后，第 55 行的术语豁免没有动） |

过关倍数的写法：一位小数、总是写出小数位、向下取（`RunRecords111.ClearRatio`，`Runtime/Presentation/RunRecords111.cs`）。还没有过关时（0/1）显示破折号“—”。

整局结果页原来的“最大单手”是从提交回执里现算的最大值；现在显示 `RunState.highestHandScore`。正式局里两者相等；调试工作区从正式局克隆时带着原来的纪录，可能比调试里提交过的最大值高（W3b-1 备注 S08）。

**局外正式记录**：

- 文件：存档目录下的 `formal-records.json`（与 `active-run.json`、`display-settings.json` 同一个目录；测试里跟随 `GameApp.SaveDirectoryOverride`）。内容 `{"schema":1,"historicalHighestHandScore":"12345"}`，分数是规范十进制字符串（可以超过 64 位）。
- 类：`Runtime/FormalRecordStore.cs`，不依赖引擎类型（所以能在 .NET 宿主里测）。写入办法照 `DisplaySettings.Persist`：写完整的临时文件、刷盘、再替换，原文件留作 `.backup`。
- 兼容：没有文件 → 0；文件是 JSON 对象但没有这个字段（旧记录文件）→ 0，写入时只补这个字段、其它字段原样保留；文件损坏（不是对象、字段不是规范十进制）→ 不抛错，用完好的备份，备份也没有就按 0；损坏的主文件在下一次写入时被替换，但不会被轮换成备份。写入失败不抛错，内存里的值保留，下一次记录时重试。
- 什么时候更新：每条被接受的命令之后（`PresentCommitted`），以及启动时读到存档之后（`TickStartupLoad07`），把当前局的 `highestHandScore` 与记录取最大值；只有变大才写文件，所以重复记同一局不改文件。存档还处于“待重试”时不记。调试局、调试工作区、练习（`isDebug`、`debugWorkspace`、`tutorial`）一律忽略，规则写在 `FormalRecordStore.NoteRun` 里。
- 测试用入口：`GameApp.HistoricalHighestHandScore111`、`GameApp.FormalRecordLocation111`。

以后可以配图标的位置：结算页两行前面、主菜单那一行前面（现在都是纯文字）。

### 1.5 得分演出分档（§13 第五行）

在原有的高分庆祝（`HighScore073`，三档）上改，没有另起一套：

| 档 | 条件（S = 本手最终整数分，T = 本手冻结的有效目标） | 档位文字 | 画面（沿用原来三档的样子，顺序不变） |
| --- | --- | --- | --- |
| 无 | S < T | 无 | 无 |
| 一 | T ≤ S < 3T | “一手达标！” | 顶栏与两个托盘边缘的蓝色光圈 |
| 二 | 3T ≤ S < 10T | “三倍目标！” | 金色光圈、顶栏外圈、10 颗火花 |
| 三 | S ≥ 10T | “十倍目标！” | 再加皇冠、16 颗火花 |

- T 的来源：提交快照里冻结的 `stageTarget`（会话在提交时取 `stage.target`，已含高压与首领对目标的调整）。表演回执新增 `PresentationReceipt.HandTarget`，适配器从快照取；快照没有这个值时（不是提交，或旧的合成快照）退回提交后的关卡目标。
- 位置：`Runtime/Presentation/HighScore073.cs` `Tier`（阈值常量 `SuperTargets = 3`、`BurstTargets = 10`）；`Runtime/Presentation/PresentationModels.cs`、`PresentationReceiptAdapter.cs`；画面 `UI/GameApp.Art073HighScore.cs`；档位文字 `UI/GameApp.Play11Score.cs` `TierWord11`。
- 原来的第一档“过半！”（达到目标一半）没有了：规格写“小于 T 没有这三档”。
- **没有动的另一套档位**：`ScorePresentationTier`（按总倍率 8、32、128、1024、8192 分 0–5 档）仍然决定计分节拍的时长、粒子数和“爆发”音效。它是按倍率分的，不是按固定分数，p0.9.0 起已经不参与庆祝档位，本包没有改。**需要协调者定**：§13 的“三档”是否也要接管这套节拍时长与音效；如果要，改 `PresentationReceipt.ComboTier` 的来源即可，但 `PresentationTests.FX01`、夹具的 `ComboTier` 断言要一起改。
- 夹具 `Burst09` 的目标从 250000 改成 50000，让它仍然是最高档（见 TEST_CHANGES）。

需要重烘的字：档位文字里新出现“三”“十”“倍”三个字的组合（“倍”“十”“三”在其它界面文字里都已经有；常驻的字形保持文本同步改成了“一手达标！三倍十倍目标计分中…”）。是否要重烘以字表重生成后的差异为准，见第 4 节。

### 1.6 玻璃提交后碎裂的演出（§7.3）

- 时机：提交演出的“结算后”节拍（`PostScore`，标准速度 480 毫秒，快速 160 毫秒）。这一拍的事件里有回执的 `DieBroken`，演出从这里读。在这之前（计分、总分）玻璃骰都还好好地在格子里。
- 做法：沿用 v0.7 投掷碎裂的那一套。这一拍开始时，先记下每颗碎骰在格子里的真实姿态（位置、朝向、大小），把它从骰桌上拿掉；本次回执里按提交顺序的前两颗在原地播碎裂特效（`BreakDie07`，每颗 6 片，共 12 片上限，与被摧毁的卡牌共用“风险”这一组的碎片额度，卡牌先取）；第 3 颗起留下轮廓和一个叉（`ShowBrokenOutline07`）。减少动效或低画质时没有碎片，所有碎骰都留轮廓（与原来投掷碎裂时相同）。即时速度没有演出。
- 声音与文字是 W7a 已有的：这一拍的事件自带碎裂音效；提示栏摘要“N 颗玻璃骰碎了（提交栏格数不变）”；计分记录逐条一行。
- 演出期间界面显示的是提交前的状态，所以碎掉的骰子要记下来（`shatteredDice111`），之后任何重画都不再把它放回格子（`DrawSlotDie11` 里跳过）。演出结束或跳过后界面换成提交后的状态，骰子本来就不在了。重播上一手时先清空这份记录，骰子会重新出现再碎一次。
- 位置：`UI/GameApp.Glass111.cs`（`PresentSubmissionBreaks111`、`ShatteredDice111`、常量 `ShatteredDiceLimit111 = 2`、`FragmentsPerDie111 = 6`）；`UI/GameApp.Art07Effects.cs` `PresentPostEffects07` 里调用；`UI/GameApp.Art07PostEffects.cs` 把轮廓的画法拆成 `ShowBrokenOutline07`；`Runtime/DiceTable.Break111.cs` 新增 `HideDie111`（只拿掉一颗骰子）。
- 投掷时的碎裂代码（`PresentRollEffects07` 里的那一段）还留着，规则不再在投掷时产生 `DieBroken`，它不会被触发。

没有做的：碎骰所在的格子在这一拍之后是空的，格子上的“+N”分数条、锁标仍然留到演出结束（它们属于提交前的画面）。是否要在碎裂后把格子标成“已碎”，属于版式问题，留给实看之后定。

### 1.7 W7a 留下的小项

| 小项 | 做法 | 位置 |
| --- | --- | --- |
| 养成卡“不占栏位”的标识：卡面 | 不占栏位的那一张养成卡（最左边的一张，规则的 `FreeCultivationInstanceId`），如果它自己没有别的读数，牌桌上卡面左上的读数角标写“不占栏位”。用的是已有的读数角标，没有加新的版式元素；这张卡有别的读数（失效警告等）时仍显示那个读数 | `UI/GameApp.Art07.cs` `CardState07`；`UI/GameApp.Display111.cs` `FreeCultivation111` |
| 养成卡“不占栏位”的标识：栏位计数行 | 商店“我的构筑”的计数行后面加“ · 养成卡“拔苗助长”不占栏位”（练习与调试的计数行同样加） | `UI/GameApp.Shop14.cs` `ShopBuild14`；`FreeCultivationNote111` |
| 加成明细的收起／展开 | 得分明细（抽屉）里“加成”一行的条目超过 4 条时默认收起，行下面一个按钮“展开加成的 N 项”／“收起加成的 N 项”（`BonusToggle111`）；展开状态只对当前这一手有效。“加成（共 N 项）… ×(1+A)”这一行始终在。“逐步明细”仍然全部列出 | `UI/GameApp.Art07ScoreDrawer.cs`（常量 `BonusEntriesShown111 = 4`） |
| 演出里“加成”的先后 | 卡牌这一段的节拍改成跟公式一致：先是给加成的卡（各自一拍），再是一拍合并的“加成”（没有卡牌来源的加成、超出四个单独聚焦之外的卡牌加成，最后是“加成 ×(1+A)”这一行），然后才是做乘法的卡，最后是其余卡的合并一拍。单独聚焦仍然最多 4 张；没有任何卡牌加成的一手与原来完全相同。一张卡既有加成附魔又做乘法时，会先后出现在两拍里 | `Runtime/Presentation/PresentationSequence.cs`（`IsBonusEvent`） |
| 调试面板加道具 | 加钥匙串时直接是 3 次（按配置的 `initialUses`）；道具列表里多次数的道具旁边多一个按钮“剩 N 次”，点一下减 1，到 1 之后回到 3 | `UI/GameApp.Debug.cs` |

卡牌这一段最多从 6 拍变成 7 拍（多出来的是那一拍合并的“加成”），标准速度下整段演出的上限从 3000 毫秒变成 3140 毫秒，仍在 3200 毫秒的预算内；快速模式按阶段合并，时长不变。

没有做的：对局画面没有栏位计数行（空栏位用右侧的“+”条表示），所以对局里只有卡面角标；商店“我的构筑”里的小卡片没有读数角标，靠计数行那一句。**需要协调者定**：是否要给养成卡另配一个专门的角标图（现在是文字，可以配图标的位置：卡面读数角标处、“我的构筑”小卡片的角上）。

### 1.8 回放工具

两个新命令（`UnlockDie`、`CustomDieUpgrade`）在所有记录里都是按名字存的命令种类，原来就能解析；缺的是两个新字段 `upgradeId`、`targetFaceIds`。补的地方：

| 工具 | 改动 | 验证 |
| --- | --- | --- |
| `Core/Session/Validation~/NormalRunSearch.cs` | 记录的每一步多写这两个字段 | 编译；用种子 20261028 跑一局再用下面的回放工具重放，35 步全过（写出的记录里有这两个字段）。跑完已把被改写的 `evidence/v06/session/` 还原 |
| `Core/Session/Validation~/NormalRunReplay.cs` | 读这两个字段；旧记录里没有这两个键时按“没有”处理 | 同上；另外在仓库之外的临时目录里造了一份真实记录（种子 1，正常局打进第一家商店，做一笔两个面的自选改造“骰子染红”），用仓库里编出来的回放程序重放：13 步全过，逐步恢复存档与重复命令检查都过，最终状态一致。这份记录与生成它的脚本是一次性的，没有进仓库 |
| 游戏内回放入口 `Runtime/Verification/Art073CaptureTour.cs`、`Art073CaptureTour.Release11.cs` | 步骤结构多这两个字段，发命令时带上 | 只编译 |
| `Runtime/Verification/PlayerVerification.cs`（玩家端验证的记录重放） | 同上 | 只编译 |
| `tools/replay_v11.py` | 没有改：它自己不解析命令字段，只检查记录的整体形状，再把记录交给游戏内回放入口。生成记录的模拟器（`tools/balance_sim/Runner.cs`，不属于本包）已经在写这两个字段，名字与游戏内读的一致 | 读代码确认，未运行 |

没有动的：`Runtime/Verification/Art072CaptureTour.cs`（v0.72 的旧巡览，连卡牌调序的字段都没有，现行流程不用它）。`tools/replay_v11.py` 第 138 行与 `tools/package_v11.py` 的版本号还是 `p1.1.0`，用 p1.1.1 的记录跑它之前要先改这个版本号（属于出包工具，不在本包范围）。

### 1.9 PlayMode 测试与审阅截图

新文件 `Tests/PlayMode/V111InteractionsPlayModeTests.cs`（9 个测试，都走真实界面：点入口、选骰选面、点确认，再断言会话状态与界面文字）：

| 测试 | 覆盖 |
| --- | --- |
| `PLAY_V111_TwoFaceUpgradeCostsOnePriceAndAnIllegalFaceBlocksThePair` | 不能改的面变暗；选一个面后出现“再选一个面”；一合法一非法：确认不可用、原因带出那个面、状态不变；“换一面”清空；两个合法面：预览两行、价格文字不变、只扣一次钱、回执两条 `DieUpgraded`、命令带两个面；换骰子清空已选面 |
| `PLAY_V111_CustomUpgradeListBuysTwiceThenTheEntryIsGreyed` | 入口文字与剩余次数；列表 11 行的价格都等于会话的实时价；自选一笔双面染红、一笔整骰脆化；剩余次数刷新；两笔之后入口变灰、文字改变、第三笔被会话拒绝且提示是玩家用语 |
| `PLAY_V111_CustomUpgradeListGivesTheReasonWhenGoldIsShort` | 金币为 0 时每一行不可买并写“现在不能买：还差 N 金币” |
| `PLAY_V111_KeyRingTakesThreeLockedDiceBackThenLeavesItsSlot` | 图标上的次数 3→2→1；钥匙串的按钮不发命令、只标出可撤回的骰子；点骰子只是选中；按钮写明来源与次数；三次撤回各自的回执（先 `DieUnlocked` 后 `ItemUsed`、扣前扣后次数）、其余已锁骰子顺序不变、不耗投掷；用到 0 次图标消失、格子空出；撤回的骰子在骰池里按创建顺序排列 |
| `PLAY_V111_FreeUnlockOfTheHandComesBeforeTheKeyRing` | 同时持有时先用回心转意（按钮写“本手免费”）、钥匙串次数不动、卡面从“可用”变“已用”；同一手第二次用钥匙串 |
| `PLAY_V111_BareHandsBlocksTheKeyRingWithItsReasonAndNoSourceOpensTheDie` | 徒手：选中后按钮不可用、提示栏给出原因、硬发命令被拒且次数不变；徒手下回心转意仍可用；免费次数用掉之后的原因；没有任何来源时点已锁骰子仍是打开骰子详情 |
| `PLAY_V111_SettlementShowsTheRunRecordsAndTheMenuTheFormalBest` | 结算页两行纪录的文字与“本关新纪录”；点开明细后仍在；调试局不写正式记录；主菜单“暂无记录”与读到记录文件之后的数字 |
| `PLAY_V111_AFormalRunFeedsTheFormalRecordOnlyWhenItsBestHandRises` | 正式局提交一手之后历史最高更新并落盘在存档目录；重启读同一个目录数值还在、文件不被重写、主菜单显示 |
| `PLAY_V111_ScoreTiersStartAtOneThreeAndTenTargets` | 同一手牌，目标分别设成 得分+1、得分、得分/3、得分/10：无档、一档、二档、三档各触发一次，档位文字与光圈、外圈、皇冠 |

玻璃提交后碎裂在 `Art07PostEffectsPlayModeTests`（1.6 节），整局结果页在 `Art07ResultRowsPlayModeTests`（1.4 节）。

**审阅截图**：没有往 `Art07VisualRecorder` 里加。它录的是“某个夹具的一段计分演出”的逐帧序列并做帧分析，本包的新画面大多是静止的商店、弹窗、结算页，放不进它的流程。V1.1 的 1080p／720p 审阅图实际是由 PlayMode 测试里的截图步骤产出的（`W11Capture`、`W14Shot`、`W20Xb` 审阅巡览），本包照这个办法：上面每个测试在关键画面调用 `W7bShot(名字)`，隐藏调试标记后截当前分辨率的整屏，写到 `artifacts/cache/v111/w7b/<宽>x<高>/<名字>.png`。分别以 1920×1080 和 1280×720 跑这个文件即可得到两套图。文件名：

`custom-upgrade-list`、`custom-upgrade-two-faces`、`custom-upgrade-used-up`、`custom-upgrade-list-no-gold`、`face-pick-pair-rejected`、`face-pick-adding-second`、`face-pick-two-faces`、`items-key-ring-uses`、`unlock-key-ring-armed`、`unlock-by-key-ring`、`unlock-after-first`、`unlock-key-ring-used-up`、`unlock-by-free-card`、`unlock-blocked-bare-hands`、`settlement-records`、`menu-formal-record`、`score-tier-below`、`score-tier-one`、`score-tier-three`、`score-tier-ten`。

整局结果页的图由原有的 `Art07ResultRowsPlayModeTests`（`S18-row…`）产出，现在是七行。玻璃碎裂的那一拍没有加截图步骤（那个测试在这一拍上做位置断言，中途重画会干扰），需要图时可以用 `Art07VisualRecorder` 录一段带玻璃骰的提交。

这些截图步骤会在演出中途重画一次界面；如果实跑时发现某张图与断言互相干扰，先去掉那一处 `W7bShot`。

## 2. 需要在 Unity 里实看的画面

| 屏 | 怎么到达 | 看什么 |
| --- | --- | --- |
| 商店选面，确认步骤（一个面） | 商店 → 点“黄金电镀”等按面改造 → 点一颗骰子 → 点一个面 | 价格行下面的“再选一个面（价格不变）”按钮有没有压到底部的按钮行；125%、150% 字号各看一次 |
| 商店选面，加选 | 上一步点“再选一个面” | 已选面的金圈；不能改的面的暗色是否看得出、点数是否还认得清；提示“再选一个面，价格不变”；底部“只改一面”“取消” |
| 商店选面，确认步骤（两个面） | 加选时点第二个面 | 两行“改造前 → 改造后”、说明、价格是否都在按钮行上方（150% 字号下镀金两面最挤） |
| 两个面的成交演出 | 上一步点“确认购买” | 左栏“改造前”、右栏“改造后”各四行是否放得下；骰子只转到第一个面 |
| 商店“升级与服务”标题栏 | 进任意商店 | “自选改造 · 还剩 2 次”与“指定升级 · 加价 1”并排是否挤到左边的“本店暂无道具”等提示；150% 字号且商品分页时最挤 |
| 自选改造列表 | 点“自选改造 · 还剩 2 次” | 11 行（要滚动）：名称与短句一行是否被截断（过长时以省略号结尾）；不能买的行的原因；价格按钮 |
| 自选改造的选面面板 | 列表里点一个价格按钮 | 右上角类别“自选改造”；价格是否比货架同名改造贵 1；持有“砍价高手”时是否便宜 1 |
| 自选两笔之后 | 连续完成两笔自选改造 | 入口变成“本店自选改造已用完”且不可点；刷新卡牌后仍然如此 |
| 对局道具栏、商店“我的构筑”的道具 | 持有钥匙串（调试面板加，或商店买） | 图标右下角的次数小框是否挡住图标主体、数字是否清楚；六套主题下的对比 |
| 钥匙串详情 | 点钥匙串图标 | “用法”一段的长度；按钮“去选要撤回的骰子” |
| 钥匙串“使用”之后 | 掷骰并锁定两颗以上，点钥匙串 →“去选要撤回的骰子” | 每颗已锁骰子的细金框与“可撤回”标签是否压住骰子、锁标或角标；提示栏文字 |
| 已锁骰子上的“撤回”（钥匙串） | 上一步点一颗骰子 | 选中金圈；主按钮“撤回 · 钥匙串（剩 3 次）”是否放得下（150% 字号最挤）；提示栏三句话是否被自动缩得太小 |
| 已锁骰子上的“撤回”（回心转意） | 装备回心转意，掷骰锁定后点一颗已锁骰子 | 主按钮“撤回 · 回心转意（本手免费）”；撤回后骰子回到骰池的位置与过渡；卡面“本手免费撤回已用” |
| 不能撤回 | 徒手首领关持有钥匙串，或回心转意已用且没有钥匙串时点已锁骰子 | 主按钮灰着的“撤回”；提示栏的原因 |
| 钥匙串用完 | 连续撤回三次 | 次数 3→2→1 后图标消失、格子空出；提示“钥匙串用完了，道具栏空出 1 格” |
| 关末结算页（普通关与首领关） | 过任意一关 | “明细”按钮右边的两行纪录是否与按钮同高、是否压到上面的提示行或纸面下边；刷新纪录时的加粗与“ · 本关新纪录”；点开“明细”后两行仍在；125%、150% 字号 |
| 整局结果页（通关、失败各一次） | 打完或打输一局 | 五个统计格：100% 字号一行五格时大数字是否被缩得太小；125%、150% 时三格加两格是否压到下面的“构筑”；失败且一关未过时“最高过关倍数”显示“—”；逐行出现的节奏（现在七行） |
| 主菜单 | 回主菜单（有存档、没有存档各看一次） | “历史最高单手 …”一行在“退出游戏”与“版本与构建”之间的位置；打过一局正式局之后数字出现；调试局不改变它 |
| 提交带玻璃骰的一手（碎 1 颗、碎 4 颗各一次） | 调试面板把骰子设成玻璃并强制“碎”，锁定后提交；或正常玩到带玻璃骰 | 骰子在总分出来之后才碎，不是一提交就没；碎片是否就在骰子原来的位置和朝向上；第 3、第 4 颗的轮廓与叉；碎掉之后格子里剩下的“+N”与锁标看着是否别扭；快速、减少动效、低画质各看一次 |
| 牌桌上不占栏位的养成卡 | 持有一张养成卡（如拔苗助长）进对局 | 卡面左上的角标“不占栏位”是否放得下（卡多时的小卡、125%／150% 字号）；有两张养成卡时只有最左边那张有；拖动换位后角标跟着换 |
| 商店“我的构筑”计数行 | 持有养成卡进商店 | “卡牌栏位 3/5 · 共 4/10 张 · 养成卡“拔苗助长”不占栏位”是否挤到右边的“拖动调整顺序”与道具栏；150% 字号 |
| 得分明细里收起的加成 | 一手里凑出 5 项以上加成（全蓝、黄金面、几张加成卡），提交后点筹码开明细 | “展开加成的 N 项”按钮的位置与大小；点开、收起时抽屉会重新展开一次的动效是否突兀 |
| 加成卡与乘法卡混排时的演出 | 卡牌顺序排成 乘法、加成、乘法、加成 后提交 | 先亮两张加成卡、再亮“加成”筹码、然后才是乘法卡；节奏是否自然 |
| 调试面板的道具 | 调试面板“道具”页加钥匙串 | 列表里“钥匙串 ×”旁边的“剩 3 次”按钮；点它 3→2→1→3 |
| 得分演出三档 | 调试面板把目标设成一手得分的 1 倍、1/3、1/10 各提交一次（或用截图记录器的三张） | 一档蓝色光圈与“一手达标！”；二档金色光圈、外圈、火花与“三倍目标！”；三档再加皇冠与“十倍目标！”；差 1 分不到目标时什么都没有（以前过半就有蓝圈） |

## 3. 需要编辑器操作的事项

本包没有改场景、Prefab、项目设置，也没有新增资源。需要在 Unity 里做的：

1. 导入：新文件的 `.meta` 都是手写的（随机 GUID，格式照同目录）：`UI/GameApp.Interact111.cs`、`UI/GameApp.Records111.cs`、`UI/GameApp.Glass111.cs`、`Runtime/FormalRecordStore.cs`、`Runtime/DiceTable.Break111.cs`、`Runtime/Presentation/RunRecords111.cs`、`Tests/EditMode/Presentation/V111RecordsTests.cs`、`Tests/PlayMode/V111InteractionsPlayModeTests.cs`。打开工程后确认没有 GUID 冲突的提示。
2. 重烘字体图集（见第 4 节）。
3. 跑 EditMode 与 PlayMode 全部测试；以 1920×1080 和 1280×720 各跑一次 `V111InteractionsPlayModeTests` 取审阅图。
4. W7a 备注里那一项仍然没有动：`Editor/Art07AssetLibrary.cs` 生成的得分 Prefab 字段。

## 4. 字表与需要重烘的字符

改了玩家可见文字之后已用 `tools/v21_title_chars.py --write` 重生成字表（以工作区现行配置为准），`tests/test_v21_title_chars.py`、`tests/test_v21_linebreak_words.py`、`tests/test_v11_glossary_check.py` 共 100 条全过；换行词表检查通过（250 个词对 272 条短文字与选面样本），没有需要增删的词。

| 字表 | 相对本包开始时新增的字 | 来自 |
| --- | --- | --- |
| `Fonts/Themes/title_chars.txt`（标题字体，四套主题） | 撤、自 | “撤回”按钮（主按钮用标题字体）、“自选改造”弹窗标题 |
| `Fonts/Themes/bold_chars.txt`（粗体） | 悔 | 撤回提示里的“不能反悔” |

这三个字要在 Unity 里按 W21 的流程重烘四套主题的标题字体与粗体静态图集；W7a 备注里列的那批（标题“匙反意绝转钥”、粗体“做偿匙反惩管纪罚钥零”）如果还没烘，一起烘。烘之前 `V21TitleFontAssetTests` 的 `FW02`、`Q5` 预计失败。

档位文字“三倍目标！”“十倍目标！”走的是常驻的动态字形（`Score11Glyphs`），不在这两张静态字表里。

## 5. 运行过的与只编译的

运行过（.NET 10 宿主与 Python，结果是本包最后一次运行的数字；运行时工作区里还有协调者未提交的配置与测试改动）：

| 检查 | 结果 |
| --- | --- |
| 规则宿主 `RulesNUnitValidation.csproj` | 3982 / 3982 |
| 会话宿主 `SessionValidation.csproj` | 616 / 616 |
| 表演宿主 `PresentationValidation.csproj` | 2419 / 2419（基线 2374；新增 45 条：`V111RecordsTests` 31、`HighScore073Tests` 净增 13、`PresentationTests` 1） |
| 夹具预检 `FixturePreflight.csproj` | 11 个场景全过（`Burst09` 换了目标之后仍是最高档） |
| `UnityTestsCompilation.csproj` | 5 个错误，与基线相同（`Art07ResourceDiagnosticPlayModeTests`），没有新增 |
| `NormalRunSearch` → `NormalRunReplay` | 35 步重放通过；另有一份含两个面自选改造的真实记录 13 步重放通过（见 1.8） |
| `tools/v11_glossary_check.py` | 违规 0，失效允许项 0 |
| Python 测试三个文件 | 100 条全过 |

宿主写出的证据文件已还原或删除：规则宿主改写的 `evidence/v05/rules/nunit-managed-report.json` 已 `git checkout`，会话宿主的 `evidence/v05/session/report.json` 与表演宿主的 `managed-<时间>.json` 已删，回放工具改写的 `evidence/v06/session/` 已还原。

**只编译、没有运行的**（都要在 Unity 里跑）：

- 新增：`Tests/PlayMode/V111InteractionsPlayModeTests.cs` 的 9 个测试。
- 改写：`Art07ResultRowsPlayModeTests`（七行）、`V21TitleFontPlayModeTests`（期望表多一格）、`Art073PlayModeTests`（档位文字）、`Art07PostEffectsPlayModeTests`（碎裂演出的位置、轮廓、12 片）。
- `V111RecordsTests`、`HighScore073Tests`、`PresentationTests` 在 .NET 宿主跑过，但没有在 Unity 的 EditMode 里跑过。
- 全部界面代码：选面面板、自选改造列表、撤回的选中与按钮、道具次数角标、结算页与主菜单的纪录、整局结果页五格、碎裂演出、调试面板。它们的版式（位置、是否重叠、大字号下是否放得下）都是按代码里的尺寸推算的，没有看过画面。

我对这些测试里把握较小的几处（实跑时如果失败，先看这里是不是测试写错了）：

- `PLAY_V111_KeyRing…` 里“撤回后骰池按创建顺序从左到右排”的断言，假定骰池在 3 颗以内时排成一行。
- 用到 `W7bShot` 的地方会在演出中途重画界面。
- `Art07PostEffectsPlayModeTests` 里碎裂特效的位置容差沿用旧测试的 0.001；如果已锁骰子在计分期间有细微的位移，这个容差会偏紧。
- `PLAY_V111_BareHands…` 用配置里第一个带“徒手”效果的首领减益放在第 9 关的调试场景里，假定调试场景不检查减益的章节档位（其它测试对 `B02` 是这样用的）。

## 6. 需要协调者决定的事

1. 自选改造入口的文字：现在是“自选改造 · 还剩 N 次”（与“指定升级”同一写法），交接包的原话是“自选改造 剩 N 次”。
2. 持有钥匙串或回心转意时，点已锁骰子从“打开骰子详情”变成了“选中以便撤回”。是否保留“再点一次打开详情”之类的入口。
3. `ScorePresentationTier`（按总倍率分的 0–5 档，决定节拍时长、粒子与爆发音效）没有动；§13 的三档是否也要接管它。
4. 整局结果页“本局最高单手”现在读 `RunState.highestHandScore`；调试工作区里它可能高于调试期间提交过的最大值。
5. 养成卡“不占栏位”用的是已有的读数角标（文字）；是否另配角标图。
6. 碎掉的玻璃骰离开之后，那一格在演出结束前是空的而“+N”与锁标还在；是否要补一个“已碎”的格内标记。
7. 得分明细里加成条目超过 4 条默认收起；阈值是否合适。
8. 选面面板没有在第一步就说明“可以选两个面”，要选完第一个面才看到“再选一个面”按钮（自选列表里有一行说明，货架卡片上没有）。是否在选面提示或卡面短句里提前说。
9. `tools/replay_v11.py`、`tools/package_v11.py` 的版本号仍是 `p1.1.0`；W3b-2 备注里提到的 `Editor/PrototypeBuild.cs:104` 发布身份检查也还没改。都不在本包范围。
10. `V21WrapMatrix` 的选面样本没有加本包的新句子（“再选一个面，价格不变”、两个面的几句说明），因为 `V21WrapTests` 对样本条数有断言而本包跑不了它。需要时在 Unity 里加样本并同步条数。

## 7. 卡面与配置文字的问题（在 `tools/v111_config.py`，没有改）

1. 九种按面改造的短句都写“这一面……”（U01–U04、U06–U10），没有一处提到可以一次改两个面。
2. U07 随机变形“这一面的点数随机变成另外5个数之一”：选两个面时是各抽一次，短句没有说。
3. I13 钥匙串“选一颗已锁定的骰子放回骰池；共可使用3次”：没有提到持有回心转意时先用它的免费次数（界面的“用法”一段里写了）。
4. W7a 备注第 3 节列的五条（C63、B22／B23／B36、B06、用语、最长短句）本包没有再核对是否已改。
