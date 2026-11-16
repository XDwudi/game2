# W10 用语检查基线

状态：REVIEW。此报告记录尚未改写的界面文字，不代表 TX-04 通过。

复现：`E:\anaconda3\python.exe tools/v11_glossary_check.py --report-only --baseline docs/specs/v11/glossary-baseline.md`。
机器可读输出：`E:\anaconda3\python.exe tools/v11_glossary_check.py --report-only --json`。

配置版本 `p1.1.0`；35 条规则；扫描 99 个文件、2196 条字符串（配置 760，C# 1436）。共 205 处命中，允许 1 处，失效允许项 0 条。

每次正则匹配计 1 处；同一字符串可命中多条规则。同一概念的重叠别名合成一条规则；不同规则可能分别标出术语和开发信息。

## 扫描范围与限制

- 配置字段清单固定在 `tools/v11_text/glossary_rules.json`：六类名称、短文、细则；服务价格规则；红组称号；附魔、牌型、颜色名称；关卡目标分字符串。开发用 description、ID、枚举、配置版本与调试配置排除。
- C# 检查 UI/ 与 Runtime/ 中含 CJK 的字符串字面量，排除所有 Validation~ 目录及 GameApp.Debug.cs。包括旧界面与验证辅助代码；没有推断调用可达性或是否最终呈现。
- 识别普通、逐字、插值、原始字符串及 Unicode 转义；跳过注释、字符字面量与插值代码。插值内部的字符串另作字面量处理；动态值以 {…} 表示，不将代码标识符误算为玩家文字。
- 不执行 C#、不跟踪变量与拼接：动态生成的编号/版本、纯英文枚举字面量、运行时异常信息仍须 TX-02 回放检查。词义依靠已列上下文，不能替代人工语义审查。
- 普通关、普通栏位、普通挑战、稀有度代码 common/uncommon/rare、其他/其余、生命周期、原值型技术字段均不因无关子串误报。明确引用“其”的代词、牌型升级与骰面改造等有歧义词采用上下文规则；具体依据见每条 rationale。
- allowlist 要求规则、文件、位置、完整原文和理由完全匹配；未使用的条目单独列为失效，不自动扩大豁免。

## 按规则计数

| 规则 | 命中数 |
| --- | ---: |
| core-submit | 1 |
| core-turn | 4 |
| core-chapter | 2 |
| core-roll | 8 |
| core-selection | 14 |
| core-capacity | 10 |
| core-slots | 19 |
| core-items | 3 |
| core-upgrade | 9 |
| core-face | 9 |
| core-active-dice | 5 |
| term-controller | 43 |
| term-rarity | 0 |
| score-die | 1 |
| score-base | 0 |
| score-total | 0 |
| score-growth | 0 |
| score-weight | 0 |
| score-chain | 3 |
| score-unused-reward | 2 |
| score-stage-reward | 6 |
| score-interest | 5 |
| score-money | 18 |
| score-run | 16 |
| style-subject | 1 |
| dev-item-id | 0 |
| dev-die-id | 0 |
| dev-face-id | 6 |
| dev-card-id | 4 |
| dev-variable | 4 |
| dev-enum | 0 |
| dev-version | 2 |
| dev-art-label | 1 |
| dev-engine-label | 9 |
| dev-exception | 0 |

## 按文件计数

| 文件 | 扫描字符串 | 命中数 |
| --- | ---: | ---: |
| `DicePrototype/Assets/Game/Data/P0Config.json` | 760 | 2 |
| `DicePrototype/Assets/Game/Runtime/Art07StartupLoad.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/AtomicSaveStore.cs` | 7 | 1 |
| `DicePrototype/Assets/Game/Runtime/DiceTable.Interaction07.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/DiceTable.Style073.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/DiceTable.Temporary071.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/DiceTable.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/DisplaySettings.cs` | 13 | 0 |
| `DicePrototype/Assets/Game/Runtime/FramePacer.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/GameApp.cs` | 134 | 28 |
| `DicePrototype/Assets/Game/Runtime/Presentation/Art071ComboEdgeAccents.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/Presentation/Art071EnchantCue.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/Presentation/Art07FaceTransition.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/Presentation/Art07PrefabView.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/Presentation/Art07QualifiedStamp.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/Presentation/Art07ScoreBurstLayout.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/Presentation/Art07TransactionMotion.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/Presentation/Art07UpgradeSurface.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/Presentation/AudioCueMap.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/Presentation/HighScore073.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/Presentation/PresentationAudio.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/Presentation/PresentationEffectBudget.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/Presentation/PresentationModels.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/Presentation/PresentationReceiptAdapter.cs` | 3 | 0 |
| `DicePrototype/Assets/Game/Runtime/Presentation/PresentationSequence.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/Verification/Art071LabRecording.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/Verification/Art071StyleLab.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/Verification/Art072CaptureTour.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/Verification/Art073CaptureTour.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/Verification/Art07AudioDiagnostic.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/Verification/Art07Fixtures.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/Verification/Art07ResourceDiagnostic.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/Verification/Art07ScreenRecorder.cs` | 3 | 1 |
| `DicePrototype/Assets/Game/Runtime/Verification/Art07VisualRecorder.Transactions.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/Verification/Art07VisualRecorder.cs` | 2 | 0 |
| `DicePrototype/Assets/Game/Runtime/Verification/FramePacingExperiment.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/Runtime/Verification/PlayerVerification.V06.cs` | 8 | 3 |
| `DicePrototype/Assets/Game/Runtime/Verification/PlayerVerification.cs` | 1 | 0 |
| `DicePrototype/Assets/Game/UI/Art071TemporaryClock.cs` | 1 | 0 |
| `DicePrototype/Assets/Game/UI/Art072IdleFloat.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/UI/Art072Motion.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/UI/Art073DiceLayer.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/UI/Art073DieView.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/UI/Art07ButtonMotion.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/UI/Art07DieHover.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/UI/Art07HollowCheck.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/UI/Art07MenuReveal.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/UI/Art07PanelMotion.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/UI/Art07PoolLabelFollower.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/UI/Art07RollButtonDots.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/UI/Art07ScoreDrawerMotion.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/UI/Art07ScreenReveal.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/UI/Art07ScrollIndicator.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/UI/Art07SettlementBackdrop.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/UI/Art07SettlementReveal.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/UI/Art07ShopEntrance.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/UI/Art07StageCardsMotion.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/UI/GameApp.Art07.cs` | 122 | 18 |
| `DicePrototype/Assets/Game/UI/GameApp.Art071Enchants.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/UI/GameApp.Art071FontMaterials.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/UI/GameApp.Art071Screens.cs` | 19 | 3 |
| `DicePrototype/Assets/Game/UI/GameApp.Art071TemporaryDice.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/UI/GameApp.Art072Score.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/UI/GameApp.Art073Dice.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/UI/GameApp.Art073DiceLibrary.cs` | 12 | 0 |
| `DicePrototype/Assets/Game/UI/GameApp.Art073HighScore.cs` | 6 | 1 |
| `DicePrototype/Assets/Game/UI/GameApp.Art073Shop.cs` | 55 | 10 |
| `DicePrototype/Assets/Game/UI/GameApp.Art073StyleLegend.cs` | 14 | 0 |
| `DicePrototype/Assets/Game/UI/GameApp.Art07DiceAffordances.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/UI/GameApp.Art07DiceInteraction.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/UI/GameApp.Art07Effects.cs` | 115 | 10 |
| `DicePrototype/Assets/Game/UI/GameApp.Art07Guide.cs` | 14 | 5 |
| `DicePrototype/Assets/Game/UI/GameApp.Art07GuideDock.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/UI/GameApp.Art07Interactions.cs` | 11 | 2 |
| `DicePrototype/Assets/Game/UI/GameApp.Art07LegacySave.cs` | 24 | 1 |
| `DicePrototype/Assets/Game/UI/GameApp.Art07Loading.cs` | 3 | 0 |
| `DicePrototype/Assets/Game/UI/GameApp.Art07NewRun.cs` | 15 | 1 |
| `DicePrototype/Assets/Game/UI/GameApp.Art07PostEffects.cs` | 7 | 0 |
| `DicePrototype/Assets/Game/UI/GameApp.Art07Presentation.cs` | 92 | 11 |
| `DicePrototype/Assets/Game/UI/GameApp.Art07Results.cs` | 32 | 6 |
| `DicePrototype/Assets/Game/UI/GameApp.Art07ScoreDrawer.cs` | 21 | 2 |
| `DicePrototype/Assets/Game/UI/GameApp.Art07Settings.cs` | 64 | 2 |
| `DicePrototype/Assets/Game/UI/GameApp.Art07Settlement.cs` | 27 | 10 |
| `DicePrototype/Assets/Game/UI/GameApp.Art07SettlementBackdrop.cs` | 11 | 2 |
| `DicePrototype/Assets/Game/UI/GameApp.Art07Shop.cs` | 48 | 5 |
| `DicePrototype/Assets/Game/UI/GameApp.Art07StableResult.cs` | 6 | 1 |
| `DicePrototype/Assets/Game/UI/GameApp.Art07StageMotion.cs` | 1 | 0 |
| `DicePrototype/Assets/Game/UI/GameApp.Art07StatusDetails.cs` | 17 | 3 |
| `DicePrototype/Assets/Game/UI/GameApp.Art07Toasts.cs` | 8 | 1 |
| `DicePrototype/Assets/Game/UI/GameApp.Art07UpgradePreview.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/UI/GameApp.Art07Wallet.cs` | 2 | 0 |
| `DicePrototype/Assets/Game/UI/GameApp.Controllers.cs` | 25 | 4 |
| `DicePrototype/Assets/Game/UI/GameApp.Display.cs` | 11 | 0 |
| `DicePrototype/Assets/Game/UI/GameApp.Modals.cs` | 176 | 26 |
| `DicePrototype/Assets/Game/UI/GameApp.Shop.cs` | 56 | 16 |
| `DicePrototype/Assets/Game/UI/GameApp.V05.cs` | 100 | 19 |
| `DicePrototype/Assets/Game/UI/GameApp.V10.cs` | 150 | 11 |
| `DicePrototype/Assets/Game/UI/UiElementPool.cs` | 0 | 0 |
| `DicePrototype/Assets/Game/UI/UiKit.cs` | 0 | 0 |

## 主要例子（每条规则最多 3 条）

| 规则 | 文件与位置 | 命中 | 建议 | 原文 |
| --- | --- | --- | --- | --- |
| style-subject | `DicePrototype/Assets/Game/Data/P0Config.json:controllers[C100].shortText` | 其 | 这颗骰子／明确写出对象 | 每关开始时摧毁右边相邻的卡牌，倍率+其价格×0.1。 |
| dev-engine-label | `DicePrototype/Assets/Game/Data/P0Config.json:services[O07].priceRule` | Run | Run 改为“一局”；其他开发标签移至开发界面，练习模式使用玩家标签 | 6，整个Run仅可购买1次 |
| score-run | `DicePrototype/Assets/Game/Runtime/AtomicSaveStore.cs:127:52` | 新挑战 | 一局／对局 | 存档版本不兼容，旧档已保留，请开始新挑战。 |
| dev-version | `DicePrototype/Assets/Game/Runtime/GameApp.cs:113:39` | v0.6 | 仅在“版本与构建”页显示 | P0 v0.6  ·  八章 · 二十四关<br>五颗骰子开始，构筑属于你的得分方式。 |
| score-run | `DicePrototype/Assets/Game/Runtime/GameApp.cs:116:43` | 一场挑战 | 一局／对局 | 开始一场挑战 |
| score-run | `DicePrototype/Assets/Game/Runtime/GameApp.cs:119:41` | 新挑战 | 一局／对局 | 新挑战  → |
| term-controller | `DicePrototype/Assets/Game/Runtime/GameApp.cs:141:83` | 控制器 | 卡牌 | 先完成或取消控制器拖拽。 |
| dev-engine-label | `DicePrototype/Assets/Game/Runtime/GameApp.cs:192:88` | DEBUG | Run 改为“一局”；其他开发标签移至开发界面，练习模式使用玩家标签 | DEBUG · 独立试验工作区 |
| core-capacity | `DicePrototype/Assets/Game/Runtime/GameApp.cs:208:39` | 提交容量 | 提交栏 N 格／提交顺序 | 每手 {…} 次投掷   /   本关 {…} 次提交<br>提交容量 {…}   ·   骰池 {…}<br>每次剩余投掷 +{…} 基础分 |
| score-stage-reward | `DicePrototype/Assets/Game/Runtime/GameApp.cs:209:40` | 基础奖励 | 过关奖励 | 普通挑战<br>{…} 分 · 基础奖励 {…} |
| score-stage-reward | `DicePrototype/Assets/Game/Runtime/GameApp.cs:210:57` | 基础奖励 | 过关奖励 | 高压挑战<br>{…} 分 · 基础奖励 {…} |
| score-interest | `DicePrototype/Assets/Game/Runtime/GameApp.cs:212:42` | 计息本金 | 利息每关最多+N | 进入后无法更改挑战。<br>过关奖励 + 剩余提交 + 旧余额利息（计息本金最多 |
| score-money | `DicePrototype/Assets/Game/Runtime/GameApp.cs:212:42` | 余额 | 金币 | 进入后无法更改挑战。<br>过关奖励 + 剩余提交 + 旧余额利息（计息本金最多 |
| term-controller | `DicePrototype/Assets/Game/Runtime/GameApp.cs:213:52` | 控制器 | 卡牌 | 排列控制器 · 查看状态 |
| core-selection | `DicePrototype/Assets/Game/Runtime/GameApp.cs:230:52` | 待锁 | 取消选择／选中／已选 | 点击待锁  ·  右键查看六面与概率 |
| core-capacity | `DicePrototype/Assets/Game/Runtime/GameApp.cs:241:42` | 有序提交 | 提交栏 N 格／提交顺序 | 有序提交   {…} / {…} |
| core-selection | `DicePrototype/Assets/Game/Runtime/GameApp.cs:259:59` | 待锁 | 取消选择／选中／已选 | 先确认或清除待锁，再投掷、调序或提交。 |
| core-turn | `DicePrototype/Assets/Game/Runtime/GameApp.cs:261:60` | 结束本轮 | 结束本关／本手 | 结束本轮 |
| core-selection | `DicePrototype/Assets/Game/Runtime/GameApp.cs:270:109` | 待锁 | 取消选择／选中／已选 | 练习：点击骰子使其待锁，再按 E 确认。也可以先清除选择。 |
| core-turn | `DicePrototype/Assets/Game/Runtime/GameApp.cs:270:141` | 结束本轮 | 结束本关／本手 | 练习：右侧预览只计算已锁骰；剩余投掷会增加基础分。达标后选择结束本轮，或继续提交。 |
| core-roll | `DicePrototype/Assets/Game/Runtime/GameApp.cs:299:46` | 剩掷 | 首次投掷／还能掷 N 次／掷骰 |   +  剩掷奖励   |
| term-controller | `DicePrototype/Assets/Game/Runtime/GameApp.cs:304:17` | 控制器 | 卡牌 | 控制器 × |
| score-stage-reward | `DicePrototype/Assets/Game/Runtime/GameApp.cs:318:39` | 基础奖励 | 过关奖励 | 奖励前金币                 {…}<br>基础奖励                     +{…}<br>剩余提交                     +{…}<br>利息{…}                     +{…}{…}<br>计息本金 {…} / {…} |
| score-interest | `DicePrototype/Assets/Game/Runtime/GameApp.cs:318:39` | 计息本金 | 利息每关最多+N | 奖励前金币                 {…}<br>基础奖励                     +{…}<br>剩余提交                     +{…}<br>利息{…}                     +{…}{…}<br>计息本金 {…} / {…} |
| score-money | `DicePrototype/Assets/Game/Runtime/GameApp.cs:319:36` | 余额 | 金币 | 余额   |
| dev-engine-label | `DicePrototype/Assets/Game/Runtime/GameApp.cs:386:662` | Run | Run 改为“一局”；其他开发标签移至开发界面，练习模式使用玩家标签 | 本Run持续生效；每级倍率 + |
| core-slots | `DicePrototype/Assets/Game/Runtime/Verification/PlayerVerification.V06.cs:313:37` | 普通槽 | 栏位／道具栏位／不占栏位 | 普通槽  |
| core-face | `DicePrototype/Assets/Game/Runtime/Verification/PlayerVerification.V06.cs:334:118` | 物理面 | 朝上的面／这一面／对面那一面 | 按物理面 |
| dev-art-label | `DicePrototype/Assets/Game/UI/GameApp.Art07.cs:63:68` | 复古印刷骰桌 | 章节信息 | 复古印刷骰桌 |
| core-slots | `DicePrototype/Assets/Game/UI/GameApp.Art07.cs:75:43` | 普通槽 | 栏位／道具栏位／不占栏位 | 普通槽 {…} / {…}<br>总持有 {…} / {…}<br>永久骰 {…} / 8 |
| core-items | `DicePrototype/Assets/Game/UI/GameApp.Art07.cs:216:42` | 局内道具 | 道具 | 局内道具 |
| core-capacity | `DicePrototype/Assets/Game/UI/GameApp.Art07.cs:269:42` | 有序提交 | 提交栏 N 格／提交顺序 | 有序提交  {…} / {…} |
| core-turn | `DicePrototype/Assets/Game/UI/GameApp.Art07.cs:306:81` | 结束本轮 | 结束本关／本手 | 结束本轮 |
| dev-card-id | `DicePrototype/Assets/Game/UI/GameApp.Art07.cs:521:78` | C53 | 卡牌名称 | 多个真实 C53 |
| dev-card-id | `DicePrototype/Assets/Game/UI/GameApp.Art07.cs:521:89` | C53 | 卡牌名称 | 仅列各来源与总范围，不展开组合列表。每个真实 C53 独立确定 q，模仿者与目标共用 q。 |
| core-active-dice | `DicePrototype/Assets/Game/UI/GameApp.Art07.cs:533:91` | 活动骰 | 骰子 N 颗（永久 N） | 活动骰已耗尽。 |
| score-chain | `DicePrototype/Assets/Game/UI/GameApp.Art073HighScore.cs:77:60` | 连锁 | 生效 N 张 | 连锁  |
| core-items | `DicePrototype/Assets/Game/UI/GameApp.Art073Shop.cs:219:59` | 消耗品 | 道具 | 消耗品 |
| core-upgrade | `DicePrototype/Assets/Game/UI/GameApp.Art073Shop.cs:223:57` | 研究强化 | 升级（牌型／颜色）或改造（骰面） | 研究强化 |
| core-upgrade | `DicePrototype/Assets/Game/UI/GameApp.Art073Shop.cs:226:73` | 研习 | 升级（牌型／颜色）或改造（骰面） | 本店已研习 |
| core-upgrade | `DicePrototype/Assets/Game/UI/GameApp.Art073Shop.cs:226:113` | 研习 | 升级（牌型／颜色）或改造（骰面） | 自选研习 · 正常价 +1 |
| core-slots | `DicePrototype/Assets/Game/UI/GameApp.Art073Shop.cs:323:95` | 普通占槽 | 栏位／道具栏位／不占栏位 | <br>普通占槽  |
| core-roll | `DicePrototype/Assets/Game/UI/GameApp.Art07Effects.cs:353:138` | 剩掷 | 首次投掷／还能掷 N 次／掷骰 | 剩掷 + |
| core-face | `DicePrototype/Assets/Game/UI/GameApp.Art07Effects.cs:597:134` | 物理面 | 朝上的面／这一面／对面那一面 | 当前投出：未生成<br>未修改其他物理面 |
| core-roll | `DicePrototype/Assets/Game/UI/GameApp.Art07Effects.cs:648:86` | 剩掷 | 首次投掷／还能掷 N 次／掷骰 |  剩掷 + |
| dev-card-id | `DicePrototype/Assets/Game/UI/GameApp.Art07Guide.cs:219:114` | C53 | 卡牌名称 | C53 的倍率在提交后揭示；预览与取消不会重新抽取。 |
| score-unused-reward | `DicePrototype/Assets/Game/UI/GameApp.Art07Interactions.cs:64:47` | 余次奖励 | 剩余提交奖励／剩余投掷奖励 | <br>余次奖励 + |
| dev-version | `DicePrototype/Assets/Game/UI/GameApp.Art07LegacySave.cs:33:65` | v1.0 | 仅在“版本与构建”页显示 | 保留备份并开始 v1.0 规则的新局 |
| score-chain | `DicePrototype/Assets/Game/UI/GameApp.Art07Presentation.cs:252:53` | 连锁 | 生效 N 张 |   ·  连锁  |
| core-submit | `DicePrototype/Assets/Game/UI/GameApp.Art07Results.cs:63:78` | 有效提交 | 提交 | 最后有效提交 |
| core-active-dice | `DicePrototype/Assets/Game/UI/GameApp.Art07Results.cs:91:62` | 活动骰 | 骰子 N 颗（永久 N） | 活动骰已耗尽。 |
| core-active-dice | `DicePrototype/Assets/Game/UI/GameApp.Art07Results.cs:97:74` | 活动骰 | 骰子 N 颗（永久 N） | 活动骰耗尽 |
| score-money | `DicePrototype/Assets/Game/UI/GameApp.Art07Settlement.cs:77:38` | 现金 | 金币 | 本次现金净增  +{…}<br>奖励前余额 {…} → 结算后余额 {…} |
| score-interest | `DicePrototype/Assets/Game/UI/GameApp.Art07Shop.cs:34:61` | 计息本金上限 | 利息每关最多+N | <br>计息本金上限  |
| score-chain | `DicePrototype/Assets/Game/UI/GameApp.Art07StableResult.cs:52:53` | 连锁 | 生效 N 张 |  · 连锁  |
| score-unused-reward | `DicePrototype/Assets/Game/UI/GameApp.Art07StatusDetails.cs:86:60` | 剩次奖励 | 剩余提交奖励／剩余投掷奖励 | 剩次奖励  + |
| dev-variable | `DicePrototype/Assets/Game/UI/GameApp.Controllers.cs:114:146` | 容量 K | 提交栏 N 格／骰子数量 |  · 容量 K  |
| core-face | `DicePrototype/Assets/Game/UI/GameApp.Modals.cs:154:134` | 物理面 | 朝上的面／这一面／对面那一面 | 仅作用于所选物理面 |
| dev-face-id | `DicePrototype/Assets/Game/UI/GameApp.Modals.cs:167:124` | F01 | 第 N 面 | <br>物理对面 F01/F06 · F02/F05 · F03/F04 |
| dev-face-id | `DicePrototype/Assets/Game/UI/GameApp.Modals.cs:167:124` | F06 | 第 N 面 | <br>物理对面 F01/F06 · F02/F05 · F03/F04 |
| dev-face-id | `DicePrototype/Assets/Game/UI/GameApp.Modals.cs:167:124` | F02 | 第 N 面 | <br>物理对面 F01/F06 · F02/F05 · F03/F04 |
| dev-variable | `DicePrototype/Assets/Game/UI/GameApp.Modals.cs:257:155` | K | 提交栏 N 格／骰子数量 |  · 提交容量保持 K= |
| core-items | `DicePrototype/Assets/Game/UI/GameApp.Shop.cs:25:42` | 局内道具 | 道具 | 局内道具  {…}/{…} |
| dev-variable | `DicePrototype/Assets/Game/UI/GameApp.V05.cs:80:70` | K | 提交栏 N 格／骰子数量 | 最低 K {…} · 永久骰门槛 {…} · {…} |
| core-chapter | `DicePrototype/Assets/Game/UI/GameApp.V10.cs:225:25` | 周期 | 章 | 首领的减益在周期开始时就已确定。可以趁现在调整构筑、控制器顺序，或准备破咒符与替罪羊。 |
| core-chapter | `DicePrototype/Assets/Game/UI/GameApp.V10.cs:351:23` | 周期 | 章 | 三关一周期 |
| score-die | `DicePrototype/Assets/Game/UI/GameApp.V10.cs:353:28` | 点数贡献为 0 | 每颗计分+N／不计分，仍算牌型 | 被封印的提交骰点数贡献为 0，但仍然参与牌型、颜色、红组和位置判定。 |

## 允许项与失效项

- `dev-engine-label` `DicePrototype/Assets/Game/UI/GameApp.V05.cs:212:42`：ShowBuildVersion 的“版本与构建”页，DEV_PLAN §6 TX-02 明确允许开发版本信息；只豁免这条完整文字中的 schema 标签。

## 输入指纹

- 规则 SHA-256：`dc415f23348a70ec3667dc1df7261613f2e8a8381429d7b8c71af43d59ec5c03`
- 允许项 SHA-256：`8baa4df91291d75d00d259f46fdd072e493a12a3cb9563f289396362d574b9d0`

| 文件 | SHA-256 |
| --- | --- |
| `DicePrototype/Assets/Game/Data/P0Config.json` | `2a3035bd2964783cdf9829e7b7ed3f934564d1e27c8adedad3406aea12ab89ba` |
| `DicePrototype/Assets/Game/Runtime/Art07StartupLoad.cs` | `d8bfaa9e2a09fbac3b6817cb0257646e5851b81984fe3a228065cef472190ab6` |
| `DicePrototype/Assets/Game/Runtime/AtomicSaveStore.cs` | `ea6c7c5eafcc639585b14acc6a13c6c31ec83ab6caee02b7e74fe218625caa9c` |
| `DicePrototype/Assets/Game/Runtime/DiceTable.Interaction07.cs` | `7db5432e612006adcaa7d781872d5dcc93e473f6e10b8ed40123eec6dbe3ca8b` |
| `DicePrototype/Assets/Game/Runtime/DiceTable.Style073.cs` | `c75df9b2b55d728d0864b815bde9902e360d9e66f093f4cce884f22717810a3d` |
| `DicePrototype/Assets/Game/Runtime/DiceTable.Temporary071.cs` | `f8fafccb385d55b194aadaee2a32141a6fec255435d670992e78594c0ece3df2` |
| `DicePrototype/Assets/Game/Runtime/DiceTable.cs` | `e228df744cf44a3742bdee59798f8489e444f642f12fde11824698d9e6d2ba8d` |
| `DicePrototype/Assets/Game/Runtime/DisplaySettings.cs` | `6f0ca301e54df5aeec2d87ede1c20af8f2100e368941f37d73ca87c8a99fb998` |
| `DicePrototype/Assets/Game/Runtime/FramePacer.cs` | `74970d291d1b3cbde7cb4f421da6decedae912a6f8c1f29003712b524a8ecba0` |
| `DicePrototype/Assets/Game/Runtime/GameApp.cs` | `bdb2fee7b85ef662a260e0cfd2169b322c6350955ee91c7d8edc202accc4ac6b` |
| `DicePrototype/Assets/Game/Runtime/Presentation/Art071ComboEdgeAccents.cs` | `60ce0de11a55df1651f6f9da6fc1571a721e73845e425d9a0715fcf176491c2d` |
| `DicePrototype/Assets/Game/Runtime/Presentation/Art071EnchantCue.cs` | `cbaf36972795f6800f76cbaa275f15d7b86876e1069f7702c20ef93e8f30881e` |
| `DicePrototype/Assets/Game/Runtime/Presentation/Art07FaceTransition.cs` | `b9182a72b1b0781993643abcf5688ea3aecdacbbcb690e7e98c801f06e10ceb4` |
| `DicePrototype/Assets/Game/Runtime/Presentation/Art07PrefabView.cs` | `7f41892d80e35ded3905e7573486d2d5329092973d5261edb9ec5a06a915ce7a` |
| `DicePrototype/Assets/Game/Runtime/Presentation/Art07QualifiedStamp.cs` | `376a29163da5b57ac512b49c57aa1528ad7006b405b7d926d89a697a98581de1` |
| `DicePrototype/Assets/Game/Runtime/Presentation/Art07ScoreBurstLayout.cs` | `7c09aa24231a37f0d9aab497948fa9c4218828382667e704bd5382ba708dc003` |
| `DicePrototype/Assets/Game/Runtime/Presentation/Art07TransactionMotion.cs` | `289b979bb395b0b2b7fa5652cc8940f6a796d7a8c2364f26e2aea7d7f395d957` |
| `DicePrototype/Assets/Game/Runtime/Presentation/Art07UpgradeSurface.cs` | `fda8f3e1ab6b072f647be2e8a310cd79c6000b0a5f5070a50eeb49be59c22e73` |
| `DicePrototype/Assets/Game/Runtime/Presentation/AudioCueMap.cs` | `1db0ef2664832f257bc40fdb750121b614889fbc6b6610c46a69d2bad2dc1d2b` |
| `DicePrototype/Assets/Game/Runtime/Presentation/HighScore073.cs` | `8fae585f6975f387fc26fabba7a0d7cd983b03c3eb3fbf012bd7f418cc6223fc` |
| `DicePrototype/Assets/Game/Runtime/Presentation/PresentationAudio.cs` | `6380371957b8178aaaff9aa2851c077e60860c795a916dcd928be4857a371fcd` |
| `DicePrototype/Assets/Game/Runtime/Presentation/PresentationEffectBudget.cs` | `0d23e5314762d2ea66efaf0e88e15c71acaef3942049f5336c242016d8ad17c0` |
| `DicePrototype/Assets/Game/Runtime/Presentation/PresentationModels.cs` | `6bc6eab0c33d9813bbf06e6074c4074b6ade286b5c2228df8a9b85494e9d181d` |
| `DicePrototype/Assets/Game/Runtime/Presentation/PresentationReceiptAdapter.cs` | `0d4e428518d0638b7bd5570a1583a8036f3f06d08d6459092469c4399773b764` |
| `DicePrototype/Assets/Game/Runtime/Presentation/PresentationSequence.cs` | `53952fbbb737d66bf650f5c8d9ee9f69191280ad7327beb38c1255b1f742940a` |
| `DicePrototype/Assets/Game/Runtime/Verification/Art071LabRecording.cs` | `ccd750fae605329a5441d01e2d69283a6e9c0ebf24c350812af78ce5e16aee8e` |
| `DicePrototype/Assets/Game/Runtime/Verification/Art071StyleLab.cs` | `4db6d6ab6980364ba9641db0e283b8e16c61c26414f06d48d6e9dc159ff98814` |
| `DicePrototype/Assets/Game/Runtime/Verification/Art072CaptureTour.cs` | `e36161d19a1512250e785530e3e34f4df5ece4cf818b8fcec1e8fe697b6164d4` |
| `DicePrototype/Assets/Game/Runtime/Verification/Art073CaptureTour.cs` | `6e3bc29a0869b791c5764ef8585d357994942a0cf17d027d17f4c8379da520bf` |
| `DicePrototype/Assets/Game/Runtime/Verification/Art07AudioDiagnostic.cs` | `fa8ba953ed04961aae410767ada34f8ee2e893b36337f2bc44dac0c5daee026f` |
| `DicePrototype/Assets/Game/Runtime/Verification/Art07Fixtures.cs` | `85c61d35853978dd997544dc18ebfcb1fb966b2d12496f20223921aaf1ae268f` |
| `DicePrototype/Assets/Game/Runtime/Verification/Art07ResourceDiagnostic.cs` | `17d80bf9c7c0e82bd81c76e6dfafc334fc0e7fec9e43239bc86da14bb448bcfe` |
| `DicePrototype/Assets/Game/Runtime/Verification/Art07ScreenRecorder.cs` | `8c431a9f30c9cc4295f8d9c156cae90a7cde876819990c919d6cd2268326745b` |
| `DicePrototype/Assets/Game/Runtime/Verification/Art07VisualRecorder.Transactions.cs` | `d95ea2fe400669b7e838f5db68ee7776a4da663671d5e4f188d730e7123f7435` |
| `DicePrototype/Assets/Game/Runtime/Verification/Art07VisualRecorder.cs` | `f3bd3bbb9ac3d232396255554576b41b87a071e8caa9974d084ba0b6d509287d` |
| `DicePrototype/Assets/Game/Runtime/Verification/FramePacingExperiment.cs` | `92a86d48dfab91252c1790a0212c242c93219f4a0a6cc9e11ffca4c627964524` |
| `DicePrototype/Assets/Game/Runtime/Verification/PlayerVerification.V06.cs` | `555defee6615311ff7a706fa92aed35c7fd7b8102841b8e83e0ccee3678b8432` |
| `DicePrototype/Assets/Game/Runtime/Verification/PlayerVerification.cs` | `c4a721de21dc936fde64277c33f160483cea3759916074142a930ecd584ca3c0` |
| `DicePrototype/Assets/Game/UI/Art071TemporaryClock.cs` | `eb1391ea133693cafc34e261dd4706870ec0fb226c94973b3def269232654791` |
| `DicePrototype/Assets/Game/UI/Art072IdleFloat.cs` | `5d1173fc0218dcb6a9876c9bbe441aeb5471d9d655a356f725595c191354d222` |
| `DicePrototype/Assets/Game/UI/Art072Motion.cs` | `4aa81072f79635fad33cd3284db5324e9916f09697fddc8f0dd32ff089cc711f` |
| `DicePrototype/Assets/Game/UI/Art073DiceLayer.cs` | `2dc0e5d4595facd2bf844c3891b071cd7498c37a8fd2fe122de5cf6d4200f642` |
| `DicePrototype/Assets/Game/UI/Art073DieView.cs` | `95a50fc45a7252413be13f90ccf5df0f81d1bad4adb33285204a698096e421ba` |
| `DicePrototype/Assets/Game/UI/Art07ButtonMotion.cs` | `d8129e720a2fe74312da87b1dcf4562f00febcff978622b8806f5086009aa8cd` |
| `DicePrototype/Assets/Game/UI/Art07DieHover.cs` | `7457ccf9e4b841d90b001211ecc8cac176cd5cce0257878fc2b6aed682952625` |
| `DicePrototype/Assets/Game/UI/Art07HollowCheck.cs` | `ccb3a0a1f27f243a368e4fdf3a184c2303f3b1e349d9c3beffd99da5ed23eeab` |
| `DicePrototype/Assets/Game/UI/Art07MenuReveal.cs` | `70c773784d421a72b8dc8b1646e37bc5ca0a083f7a2967673f25119f7ed588e5` |
| `DicePrototype/Assets/Game/UI/Art07PanelMotion.cs` | `da1bdd0e6ab6d85f7ebe28fe94620663fbfb30c2b705acaab4496f1a099715ea` |
| `DicePrototype/Assets/Game/UI/Art07PoolLabelFollower.cs` | `2f6ddce192a3216e1138a52a27a0019752cf432d8e8b0db217402e93e7956f41` |
| `DicePrototype/Assets/Game/UI/Art07RollButtonDots.cs` | `c0efb550a24215183168082c7545c435ad904ae27b12f982d97e7e036d6a1cf6` |
| `DicePrototype/Assets/Game/UI/Art07ScoreDrawerMotion.cs` | `180387923b2a1bfea929c1b56be4e72ac64eac996ed7f5eebe51e500a4fb5723` |
| `DicePrototype/Assets/Game/UI/Art07ScreenReveal.cs` | `7ae4b581625d87dd78614310ab698e3c7c65036a6438ea69ea562bc7e96439a1` |
| `DicePrototype/Assets/Game/UI/Art07ScrollIndicator.cs` | `d724947e3b37907084b056921547371c6cf271213132b694aa015cf72c4adf38` |
| `DicePrototype/Assets/Game/UI/Art07SettlementBackdrop.cs` | `b3a6b8670748cbc687b0ca72100314ecf266ab0ea23e2bd6b8ed48cdfcc2d9e8` |
| `DicePrototype/Assets/Game/UI/Art07SettlementReveal.cs` | `98061424de3845b53fde172f8a1505eedf12d19a277867c29aea28c50ba3788c` |
| `DicePrototype/Assets/Game/UI/Art07ShopEntrance.cs` | `5451a1b17ddd3a6f172d14dffc4d0843e432012e18c22ed039fe59732d8d3ebd` |
| `DicePrototype/Assets/Game/UI/Art07StageCardsMotion.cs` | `86bf28ed3a7276197afafb9bd7497c068a32bd467543175e2588bbe9c1335032` |
| `DicePrototype/Assets/Game/UI/GameApp.Art07.cs` | `a79b1a3c5214ede081f642faa242163bcf96d9609efd9342c791df726a9ad49c` |
| `DicePrototype/Assets/Game/UI/GameApp.Art071Enchants.cs` | `b7d933e6378ff190cb97008f18e33d78d1d541d15dcb9cb2114e4a1d77bfae09` |
| `DicePrototype/Assets/Game/UI/GameApp.Art071FontMaterials.cs` | `74f86fd401b4a6c13e5e1705bf74e233004ee0174b9dca5e9978cf362743ff5a` |
| `DicePrototype/Assets/Game/UI/GameApp.Art071Screens.cs` | `89c964c17468af5c3192c4ae7777627da20cf1b5f9284785040690b0a87bdab1` |
| `DicePrototype/Assets/Game/UI/GameApp.Art071TemporaryDice.cs` | `139932eb2580130dfc377088d05219930e9534bb5902878cb55376854c17cb81` |
| `DicePrototype/Assets/Game/UI/GameApp.Art072Score.cs` | `9fe8f64660d538aefbb283f2af2744b97c6b874e681d7d6c097a1196239202be` |
| `DicePrototype/Assets/Game/UI/GameApp.Art073Dice.cs` | `1ef6c19d663907d13ca08d760c5f223eb861e95ce572c44fc283984a22a3ba12` |
| `DicePrototype/Assets/Game/UI/GameApp.Art073DiceLibrary.cs` | `411ef94b75fd939452d5e64a844c86442703c896456bfa8a3ab55eb84a8850b9` |
| `DicePrototype/Assets/Game/UI/GameApp.Art073HighScore.cs` | `06d240fceac08b6a7b5e986b35e1f03f32e54eca2430217b60b2c0a422fbdcf9` |
| `DicePrototype/Assets/Game/UI/GameApp.Art073Shop.cs` | `9ab1dbc7c1c371e406245fdcbf22397b1631e15254463517b7b994a140100d63` |
| `DicePrototype/Assets/Game/UI/GameApp.Art073StyleLegend.cs` | `1faf088049562657eeb8ca824e0b19598bdcaffb84a060bea8b5192ff0c9d87a` |
| `DicePrototype/Assets/Game/UI/GameApp.Art07DiceAffordances.cs` | `eed00d6ad982c2a79555268f2b555b5fc2111d5ffa611193496c7c914eb97144` |
| `DicePrototype/Assets/Game/UI/GameApp.Art07DiceInteraction.cs` | `18cfdbdcf36a875308b0bd2cff21e08ae305439aeb3b344242a4eb299143e4cf` |
| `DicePrototype/Assets/Game/UI/GameApp.Art07Effects.cs` | `3b15936ceac19d64efcacf3c855b37a279d6e883cc61026e4a949f34a7df4113` |
| `DicePrototype/Assets/Game/UI/GameApp.Art07Guide.cs` | `09596343e1f6e2e2f35b99ec7e47925f50465d32d3c3faef56061761f752a2f4` |
| `DicePrototype/Assets/Game/UI/GameApp.Art07GuideDock.cs` | `95dfa5d9a72bf79c316343efac30b5e8cc538e7efba1edc0b131ddfcfc1b88ce` |
| `DicePrototype/Assets/Game/UI/GameApp.Art07Interactions.cs` | `37ceff1c3132bbe5fd688eebaf114dacac862371c0dd83419bbee2636920b797` |
| `DicePrototype/Assets/Game/UI/GameApp.Art07LegacySave.cs` | `837b393aad48d74a2ddd9f22cea6ef137c954d3c696c4af2e25a3be875d9b96e` |
| `DicePrototype/Assets/Game/UI/GameApp.Art07Loading.cs` | `5cafb537accfd32f56c24227475c5fe503237a39f140bbba579a7302cf6fe1f9` |
| `DicePrototype/Assets/Game/UI/GameApp.Art07NewRun.cs` | `44a5853b89a2ba259a4487ad3198b7b309642eff786bee796d3387d8bdf85fe5` |
| `DicePrototype/Assets/Game/UI/GameApp.Art07PostEffects.cs` | `c62f340e75127c70970a7c553d82ff1ad30f0344d766fbef65795033a1dba3a5` |
| `DicePrototype/Assets/Game/UI/GameApp.Art07Presentation.cs` | `5199bc1eab5563973090924ed100645177fa317371de130d78f900c5e73cd757` |
| `DicePrototype/Assets/Game/UI/GameApp.Art07Results.cs` | `8bbb594434a350ce408721f5745cdd980c4be8d360e14535e7f949dc9cb45cff` |
| `DicePrototype/Assets/Game/UI/GameApp.Art07ScoreDrawer.cs` | `aff331cbc76ad921ce9fffad19bfe8483a774e8f1059ca116968db28840a8ce2` |
| `DicePrototype/Assets/Game/UI/GameApp.Art07Settings.cs` | `3bb51090af78cf66b6aaa89b2c61108dfbc2f5f50d5c71fb9e043878be8e618b` |
| `DicePrototype/Assets/Game/UI/GameApp.Art07Settlement.cs` | `3a23147cc1b1bb8a02e8acf27557ff2e3f55fad126af41bc7908c54d6989cbc4` |
| `DicePrototype/Assets/Game/UI/GameApp.Art07SettlementBackdrop.cs` | `c19a0fb484905972fa0b3f1d2af1f0e9ee21ba8a4c6e7b875743b9b726ebcb9f` |
| `DicePrototype/Assets/Game/UI/GameApp.Art07Shop.cs` | `96b6333d74fd0808db7d1767f60b40a197ac1db5d9207ad546e889f5660d1a8d` |
| `DicePrototype/Assets/Game/UI/GameApp.Art07StableResult.cs` | `0deb3974d21f3eaed8fc63263e09acc52f77297c71d41b5bda73a0f3a719ce09` |
| `DicePrototype/Assets/Game/UI/GameApp.Art07StageMotion.cs` | `a537c12571a604930b6be753e7fe0c947f5304ef626b71e8a50c867b09457210` |
| `DicePrototype/Assets/Game/UI/GameApp.Art07StatusDetails.cs` | `be8d7ae816d286e86b6f54d23b316b9fa06cefc46064c5c52ff09794f130d035` |
| `DicePrototype/Assets/Game/UI/GameApp.Art07Toasts.cs` | `8f891cf055c676d310254d6d47a62c9079ec20f33d66dd4baa25100fdf9f61d9` |
| `DicePrototype/Assets/Game/UI/GameApp.Art07UpgradePreview.cs` | `bfaa1d59df365bf5f472215ee63265eec5459e8211c51b451e28c73cc9242563` |
| `DicePrototype/Assets/Game/UI/GameApp.Art07Wallet.cs` | `853d760f7834c0146bfabbb2a135920399fbd4241982f490022e848e4b7608ba` |
| `DicePrototype/Assets/Game/UI/GameApp.Controllers.cs` | `d2f4095949e3ab5bc8dd36354055b2d3f89aad0b546932aaf842f53c673c43a5` |
| `DicePrototype/Assets/Game/UI/GameApp.Display.cs` | `bc930d0875b3f800aa71438c54df169b101e4cf5f047fb5f61da66bec4bd7d39` |
| `DicePrototype/Assets/Game/UI/GameApp.Modals.cs` | `79278967c2e989da4e366bb908f2cdea2432ada09726d95186ce52767d7653fd` |
| `DicePrototype/Assets/Game/UI/GameApp.Shop.cs` | `f80cc8d3241739a4b878436e5570de2258822c465d2889de06a9d546955e3915` |
| `DicePrototype/Assets/Game/UI/GameApp.V05.cs` | `7d18c5d91443dc91a8d6ebad2fc59f42e1640b87b5be5aee3f322bf4d3eeb817` |
| `DicePrototype/Assets/Game/UI/GameApp.V10.cs` | `b475c92879b823b597e424f0fb70041be34697a97e86e179de6d25558bb05152` |
| `DicePrototype/Assets/Game/UI/UiElementPool.cs` | `3c536f3e6dc7eea26f4fc80cac8b50a04b4aa7d5acd0517013aed0e94905d37d` |
| `DicePrototype/Assets/Game/UI/UiKit.cs` | `0605843ae6f1ff2a1ee7780f4aae28a9a4401e7fbfb97205048380a831340709` |
