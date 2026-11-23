# W11 测试改写记录（对局画面黄金样例）

| 项 | 内容 |
| --- | --- |
| 任务 | DEV_PLAN §5.2 W11：对局画面改版（[PLAY_LAYOUT](PLAY_LAYOUT.md)） |
| 基线 | `codex/v1.1` 的 `3bc30f0`：PlayMode 1920×1080 共 213 项（`nunit-20261116-201308` 有 4 项旧商店流程失败，已在 `3bc30f0` 修复） |
| 原则 | 不删除测试、不降低阈值。旧断言钉住 v0.73 的坐标、名称或文字时，改成对新布局表达同一意图的断言，并写明理由；新增断言检查玩家实际看到的东西（字形画在框内、没有被遮挡、实际像素） |

## 1 改写的断言

发现依据：改版后第一次全量 PlayMode（1920×1080，`nunit-20261116-223033`）214 项中 39 项失败；其中 37 项由本任务的布局引起，逐条改写如下，另 2 项是基线已有的失败（见第 3 节）。改写都在 `DicePrototype/Assets/Game/Tests/PlayMode/` 内，代码里的对应位置写有 `V1.1 (W11)` 注释。

| 测试（文件） | 旧期望 | 新期望 | 理由 |
| --- | --- | --- | --- |
| `PLAY_CoreLoop_ClickLockReorderAndSubmitMatchesPreview`（GameplayPlayModeTests.cs） | 没掷时 `Submit` 存在且禁用；有选中时 `Roll`、`Submit` 存在且禁用 | 这些按钮不存在（`HasActive` 为假） | PLAY_LAYOUT §2：不可用的动作不显示，不再变暗占位 |
| `PLAY_ART07_DICEINTERACT_LockCommitsOnceThen25msStagger180msAndOnlyThenRevealsFrozenPreview`（Art07DiceInteractionPlayModeTests.cs） | 锁定动画中 Confirm、Submit、Roll 存在且禁用；骰池每颗骰子的文字标签不越过任何骰面（`AssertPoolMotionLabels07`） | 锁定动画中 Confirm、Submit、Roll、Clear 都不显示；骰池的形状角标在骰子移动时不画，落定后全部回来，种类正确，不压其他骰子，也不压自己骰身的核心区 | PLAY_LAYOUT §2；B4 以形状角标代替文字标签；移动中隐藏见 PLAY_LAYOUT §8 第 22 行 |
| `PLAY_ART07_DICEINTERACT_FirstRollDotsNeverLoopAndFastReducedSkipFocusDoNotCommitAgain`（同上） | 首掷按钮写“掷骰 2 [R]”，每个字都画出 | 首掷按钮写“掷骰 [R]”；剩余次数是资源面板“还能掷”的 `RollBudgetValue`，两处每个字都画出 | PLAY_LAYOUT §2 状态表：`Roll`“掷骰 [R]”；次数由资源面板显示 |
| `DiceLabelCheck07`（Art07DiceLabelsPlayModeTests.cs；用于 `PLAY_ART07_DiceLabels…` 三项、`PLAY_ART071_FiveDiceGlassTemporary…`、`PLAY_ART071_TemporaryClock150…`、`PLAY_ART07_DICE_VisibleGrip…`） | 5 颗骰子时点击区 220/148 × 240/200、骰身 1.6/1.35；其余 126(180) × 112(90)、骰身 1.18/1.04/0.72；两行间距 ≥8(6) 像素；每颗骰子有文字标签“● 红 / ◇ 蓝 / 待投掷 · 玻璃 · 临时”，字号 17(14) | 一页最多 10 颗排一行：格宽 = min(272, 托盘宽 ÷ 颗数)，骰身比例 = min(1.75, (格宽 − 30) ÷ 101)，点击区宽 min(格宽 − 6, 比例 × 139 + 8)、高 比例 × 136 + 14，骰身实际缩放等于该比例；同一行中心线相同、点击区互不重叠；颜色（色彩辅助开时）、玻璃、临时是形状角标，种类与顺序正确、画出、≥12 单位、在屏内、不压其他骰子与自己骰身核心区、不压动作按钮与槽位序号和锁标，锁定后在自己的槽内；选中的骰子下有“已选 N”，字都画出、不压任何骰面、字号 ≥19.5 单位；画面上没有“待投掷” | PLAY_LAYOUT §8 第 3 行（一行）与 §4 B4（形状角标，取消“待投掷”）；原意（完整、在屏内、不遮挡、不为躲碰撞缩小骰身）保留 |
| `PLAY_ART071_FiveDiceGlassTemporaryLabelsAndFaceHitsSurviveConfirmThenReroll`（Art071StylePlayModeTests.cs 第 201–203 行） | 选中玻璃临时骰后，它的文字标签含“玻璃”“临时”，选中标签为“01 待锁” | 它的形状角标含玻璃与时钟，选中标签为“已选 1” | B4：属性是形状角标；文字方案术语“选中”（不再写“待锁”） |
| `PLAY_ART07_DICE_VisibleGripUsesExistingHitAndPendingHollowChecksKeepPipsClear`（Art07DiceAffordancePlayModeTests.cs） | 8 颗选中的骰子各有一个空心勾（`Art07HollowCheck`，12 顶点轮廓），其矩形不压任何点数；确认后没有；容量满再选一颗时 1 个 | 8 颗选中的骰子各有金色轮廓环 `PendingRing`（`fillCenter` 为假、不接收点击）和“已选 N”标签；标签矩形不压任何点数；环用实际像素检查：画环与不画环的两帧在每个可见面的点数区域内差异 ≤2 像素；确认后没有环；容量满再选一颗时 1 个 | 选中标记改为环绕骰子的轮廓与标签（PLAY_LAYOUT §4）。环的矩形必然包住骰子，只能比较它的实际笔画；“只画轮廓、不挡点数、不拦点击”不变 |
| `PLAY_ART07_FirstBoundaryHintsOnlyChangeReadPreferencesAndNeverCoverActionsOrReplayAfterDismissal`（Art07PanelsAndHintsPlayModeTests.cs） | 提示关闭按钮不压 Roll、Confirm、Clear、Submit、EndStage（五个都存在） | 不压画面上显示的动作按钮，其中 EndStage 必有 | PLAY_LAYOUT §2 |
| `ToastBounds07`（Art07ToastPlayModeTests.cs；用于 `PLAY_ART07_ToastActualPaperGlyphs…`、`PLAY_ART07_ToastPausePreserves…`） | 通知纸不压五个动作按钮、Pause、Handbook 与 Factor0–5 | 不压显示中的动作按钮（EndStage、Pause、Handbook 必有）与显示中的算式筹码 | PLAY_LAYOUT §2；§8 第 9 行（×1 的项没有筹码） |
| `BreakdownSourcesAtFont07`（Art07BreakdownLayoutPlayModeTests.cs，100/125/150 三项）、`PLAY_ART07_ScoreDrawerThreeFonts…`（Art07ScoreDrawerPlayModeTests.cs） | 点 `Factor4`（卡牌系数）打开明细 | 点 `Factor0`（基础分）打开同一份明细 | ×1 的系数没有筹码，卡牌筹码不总在；每个筹码打开同一份明细，基础分筹码总在 |
| `PLAY_ART07_ScoreLabelsPreviewMixedZeroAndRandomKeepEveryGlyphAfterRedraw`（Art07ScoreLabelsPlayModeTests.cs） | Factor0–5 都在；Hand 与 FinalScore 在同一计分面板内，位于系数按钮下方；Factor4 文字“卡牌 约 ×4.29×10⁹” | 只检查显示中的筹码（Factor0 必有），每个筹码的名称与数值字形都在筹码内；Hand 在提交栏信息面板内，FinalScore 在总分筹码内，二者不压任何筹码；Factor4 名称“卡牌”、数值“约 ×4.29×10⁹” | PLAY_LAYOUT §3（牌型名在提交栏信息面板，总分在算式行末）与 §8 第 9 行 |
| `PLAY_ART07_FINAL_RealRendererNormalFastReducedInstantRetainCommittedScoreAndSources`（Art07FinalResultPlayModeTests.cs） | `StableResultFactors07` 含“×5,040”与“生效 4 张” | 算式行标题为“上一手 · 生效 4 张”；基础分筹码等于已提交的基础分；“×”的个数 = 筹码数 − 1，“=”一个，筹码至少 3 个 | 结果保留为算式行，每个系数一个筹码，总倍率不再是一个数字（PLAY_LAYOUT §3、§8 第 9 行） |
| `PLAY_ART07_FINAL_SkipKeepsZeroScorePostChangesAndReadOnlyDetails`（同上） | `StableResultFactors07` 含“后置”“提交收入”“没有有效5点” | `StableResultFactors07`（算式行右端）含“后置”“提交收入”；“没有有效 5 点”是卡牌筹码 `Factor4` 的数值 | 跳过演出后仍能读到后置变化（PLAY_LAYOUT §8 第 18 行）；零分原因写在卡牌筹码上 |
| `PLAY_ART07_STATUS_ColdLoadDuringSubmissionShowsSavedFinalWithoutReplayingEffects`（Art07StatusRecoveryPlayModeTests.cs） | `RecoveredCaption07` 为“已恢复上一手结果” | 算式行标题 `FormulaCaptionText11` 为“已恢复上一手结果” | 标题并入算式行（PLAY_LAYOUT §3） |
| `PLAY_ART07_STATUS_ExcessPendingMarksOnlyOverflowAndCannotPartiallyLock`（同上） | Confirm 写“可再锁 2 颗” | 写“最多锁 2 颗” | PLAY_LAYOUT §2：“选中超过能锁的数量时……写‘最多锁 N 颗’并禁用” |
| `PLAY_ART07_STATUS_QualifiedRewardsStayVisibleAboveNavigationAtAllTextScales`（同上） | “基础奖励  +5”“剩次奖励  +2”“预计利息  +N” | “过关奖励  +5”“剩余提交奖励  +2”“利息  +N”，并检查标题“现在结束 +N 金币” | 术语表（glossary-baseline：基础奖励 → 过关奖励，剩次奖励 → 剩余提交奖励；文字方案 UI_REWRITES 第 126 行）；面板标题已写“现在结束”，利息行不再写“预计”（PLAY_LAYOUT §8 第 4 行） |
| `PLAY_ART07_GuideLayoutManualReopenAtMaximumCapacityKeepsCompleteInlineHelpAndClose`（Art07GuideLayoutPlayModeTests.cs 第 117 行） | 关闭引导后状态栏 `Stats07` 的品牌纸 `BrandPaper/Brand` 回来 | 关闭引导后本关牌 `StageCard11` 回来 | 新布局里引导纸停靠在本关牌上（它让出位置）；对局画面没有品牌纸 |
| `CardCapture07` / `CardCheck07`（Art07CardReadabilityPlayModeTests.cs；用于 `PLAY_ART07_CardReadability_ThreeFonts…`）与 `PLAY_ART07_GuideLayoutManualReopenAtMaximumCapacity…`（Art07GuideLayoutPlayModeTests.cs） | 桌上每张卡底部都有 State 行，普通卡写“查看条件” | 有状态的卡仍检查 State 行；没有状态的卡不画 State 行（`TableCardFooter11`），改为检查短效果 `ShortText11`：全尺寸卡必须有，且每个字画出、不溢出、不压插画/角标/稀有度；两行小卡放得下时才显示，显示时同样检查 | PLAY_LAYOUT §1 与 §8 第 24 行：卡面直接印短效果，“查看条件”是通用提示而不是状态 |
| `CardState09`（V09PlayModeTests.cs；用于 `PLAY_V10_BossStageDisablesCardsShowsItsModifierAndPaysLootInTheShop`） | 取卡上（含隐藏的）名为 State 的文字，没有即异常 | 只取显示中的 State 行，没有则为空字符串 | 同上，没有状态的卡没有 State 行；本测试断言“不是首领关将失效”与“本关失效”不变 |
| `AssertBossReadoutVisible10` 与 `PLAY_V10_BossStageDisables…`（V10PlayModeTests.cs；用于 `PLAY_V10_EveryBossModifierReadoutIsUncoveredAndFitsItsBoxDuringAStage`） | `BossModifierText` 等于减益的 description | 等于减益的 shortText（39 个减益都有） | PLAY_LAYOUT §1：本关牌写名称与效果短文，完整说明在悬停与周期面板；“完整画出、在框内、不被按钮盖住”的检查不变 |
| `PLAY_ART071_ENCHANT_CommittedNewHandBudgetUsesActualHeaderAndVisibleValue`（Art071EnchantIntegrationPlayModeTests.cs） | 首次达标时 `RollBudgetIcon` 移动 >20 像素（旧左栏被达标奖励替换） | 首次达标时 `RollBudgetIcon` 不动（≤0.5 像素）；新一手的投掷加成仍检查实际图标与数值 | 资源面板位置固定，达标奖励在结算预览面板（PLAY_LAYOUT §8 第 4 行） |
| `PLAY_ART071_ENCHANT_MultiplierArcReachesActualReceiptAreaWithoutMutatingReceipt`（同上） | 附魔弧线落到 `ReceiptFactors`，其文字含“附魔倍率” | 落到算式行的附魔筹码 `Factor5`，筹码名称为“附魔” | 旧收据横幅由算式行代替（PLAY_LAYOUT §3）；实现同时把弧线终点改到该筹码 |
| `PLAY_ART071_MaximumLockedHeadersKeepActualGlyphClearanceAtEveryTextScale`（Art071StylePlayModeTests.cs） | 有 SlotsTitle 与 LockRule；标题字形在托盘与所有槽位上方 ≥3 像素；DieState 10 个 | 没有锁定时没有 LockRule、有锁定时两者都在；标题字形与托盘、槽位框的最近距离 ≥3 像素（标题在托盘左侧的信息面板）；DieState 数等于锁定数 | PLAY_LAYOUT §5（锁定说明只在有锁定时）、§1（标题在提交栏信息面板）、B4（池中骰子没有文字标签） |
| `Temp071ClockFrame`（Art071TemporaryDicePlayModeTests.cs；用于 `PLAY_ART071_TemporaryClock150ActualResolutionTenPoolAndEightLockedRemainVisible`） | 每颗临时骰的文字标签里有时钟（`Art071TemporaryClock`），与文字留 3 像素、不压骰面/锁标/槽框、GPU 有像素；刷新复用时钟节点 | 每颗临时骰有时钟形状角标 `TempBadge11_`（`Play11Badge.Clock`）：GPU 有像素、≥12 单位、与骰子文字留 3 像素、不压锁标与槽框、不压其他骰子与自己骰身核心；普通骰没有；刷新复用角标节点 | B4：临时是形状角标；原检查全部保留在新标记上 |
| `PLAY_ART071_TemporaryClockRepositionsAfterCommittedPermanentLabelRewrite`（Art071TemporaryDicePlayModeTests.cs 第 270 行） | 永久改点文字改写后，文字标签里的时钟（`Art071TemporaryClock`）重新摆放：与所有骰子文字留 3 像素、不压骰面/锁标/槽框、GPU 有像素 | 文字里不再有时钟；这颗骰子的时钟形状角标 `TempBadge11_` 移到槽位右列底部（锁标与其他角标让位给永久文字）：与所有骰子文字和永久前缀留 3 像素、不压其他骰子、不压锁标与槽框、在槽内、GPU 有像素 | B4：临时是形状角标；文字中的时钟在新槽位里会贴近槽位序号（PLAY_LAYOUT §8 第 21 行） |
| `PLAY_ART073_HighScoreBurstStartsOnFinalScoreCountsUpAndLeavesWithoutBlocking`（Art073PlayModeTests.cs） | 有盘子 Plate、光线 Ray0、皇冠、火花；盘子上 Chain“生效 … 16,560”、Formula 至少 2 个 ×；4 个金勾 Scored*；结束后 Plate 消失 | 顶栏与两个托盘边缘亮（TopGlow、TopGlowOuter、TrayGlow、RailGlow），皇冠、火花；算式行至少 2 个“×”，总分筹码旁写“两倍目标！”；4 张参与计分的卡有角标（`ScoreCardBadges11`）；演出后标题“上一手 · 生效 N 张”；结束后 TopGlow 消失；计数单调、最终 596,160、不阻塞输入不变 | PLAY_LAYOUT §3 与 AX-01：不再有盖在骰子上的盘子 |
| `PLAY_ART073_HighScoreFastReducedInstantSkipAndBelowThreshold`（同上） | 减少动态时没有 Ray0 与火花、有皇冠，Plate 不缩放；跳过后 Plate 消失 | 减少动态时没有火花、有皇冠与顶栏光，皇冠不缩放、光不呼吸（不透明度 1）；跳过后 TopGlow 消失 | 同上 |

以下测试先失败、由修改实现解决（测试没有改）：`PLAY_ART07_QUALIFIED_StripKeepsEveryGlyphClear…` 与 `PLAY_ART07_QUALIFIED_RetargetDoesNotTeleport…`（顶栏得分变化时放大 1.14 倍会压到目标数字：目标改从放大后的宽度之后开始；最后一手交接到结算时得分框从顶栏跳到旧左栏：结算也用顶栏，PLAY_LAYOUT §8 第 26 行）、`PLAY_ART07_POSTREWORK_ThreeFontsNormalAndReduced…`（金币票落在卡牌上：移到顶栏，§8 第 19 行）、`PLAY_ART071_COMBO_RealTiersUseAuthoredEdgeRingArcsAndGoldWithoutCoveringInk`（总分筹码放不下连锁装饰：§8 第 20 行）、`PLAY_BusyRejectsBypassedUiAndSkipPreservesCommittedRandomness` 与 `PLAY_ART07_DICEINTERACT_FirstRollDots…`（掷骰中按钮消失，进度点无处显示：掷骰中按钮留在原位并禁用，§8 第 25 行）。

## 2 新增测试 `Tests/PlayMode/V11PlayScreenPlayModeTests.cs`、`V11PlayScreenAxPlayModeTests.cs`

| 测试 | 检查 |
| --- | --- |
| `PLAY_V11_W11_CheckpointScreensDefaultAndSwiss` | 检查点 ② 的截图：默认与瑞士两套风格的 (a) 选骰 (b) 计分中（骰子 / 算式 / 总分 / 计分后）(c) 首领关开局 (d) 高压关 (e) 新一关开局 (f) 10 颗骰子 7 张卡；720p 另截 150% 文字的 (a)(f)。写入 `artifacts/cache/v11/w11/<分辨率>/` |
| `PLAY_V11_AX01_ScoringFramesNeverCoverASubmittedDie` | AX-01：标准与快速两种速度，计分演出每个节拍的开头、中间、结尾各取一帧（含超高分光效与达标印章），比较“有界面”和“关掉界面画布”的两帧：每颗提交骰子的骰身内不得有界面像素（≤2 个噪点像素） |
| `PLAY_V11_AX02_OnePrimaryActionButtonInEveryState` | AX-02：没掷、掷后未选、有选中、可提交、选多了、投掷用完且没锁、计分中，各状态主色按钮恰好一个（或在没有可做的动作时为零），且主按钮位置不变；100% 与 150% 文字 |
| `PLAY_V11_AX05_EachContributingDieAndCardPopsAtItsSource` | AX-05：标准与快速速度下，每颗提交的骰子在它的槽位上方出现“+N”（在骰身之上、不越出槽位），每张参与计分的卡在卡上出现角标 |
| `PLAY_V11_W11_NewTextsAreDrawnCompleteAndReadableAtThreeTextSizes` | 100/125/150% 文字下五种画面：新文字的每个字都画出（字形四边形数等于字数），都在自己的框内，彼此不重叠，隐藏/显示对比时确实改变像素；正文 ≥ 19.5 单位（720p 下 13 px）、关键数字 ≥ 31.5 单位（21 px）；所有按钮与骰子的点击区 ≥ 40 物理像素；新一关显示“本关还没有得分”，没有锁定时不显示锁定说明 |

## 3 不是本任务引起、没有改的失败

| 测试 | 失败 | 来源 |
| --- | --- | --- |
| `PLAY_ART07_CardInformationPreservesPricesAndGeometricRarity`（Art07InformationPlayModeTests.cs 第 159 行） | 期望商店报价可用性“总持有已满”，实际“总张数已满” | 基线提交 `3bc30f0`（商店改为普通购买）改了 `GameApp.Art07Shop.cs` 第 129 行的文字，测试未跟着改；与对局画面无关 |
| `PLAY_V09_ShopSellsASlotShowsTheDiscountAndUnblocksTheSixthController`（V09PlayModeTests.cs 第 44、53 行） | 期望“普通槽已满”，实际“栏位已满” | 同上，`GameApp.Art07Shop.cs` 第 130 行（`3bc30f0`） |

这两项的新文字与 RULES_DELTA §10 的用词一致，改测试即可；但改动属于商店任务，留给它的负责人确认。

## 4 运行结果（`b4fbdd69`）

| 套件 | 结果 | 报告 |
| --- | --- | --- |
| EditMode（batch） | 2421 / 2421 | `evidence/v11/unity/nunit-20261117-005721.xml` |
| PlayMode 1920×1080（全量） | 216 / 218，失败的 2 项见第 3 节 | `evidence/v11/unity/nunit-20261117-012224.xml` |
| PlayMode 1280×720（全量） | 216 / 218，失败的同为第 3 节的 2 项 | `evidence/v11/unity/nunit-20261117-014936.xml` |

720p 的这 2 项失败在改版前就会出现（失败的是商店报价文字，来自 `3bc30f0`，与分辨率和对局画面无关）；V1.1 上此前没有跑过 720p 全量，所以没有更早的 720p 基线可比。

改版后第一次全量（`ebc6b26a`）为 1080p 175 / 214；中间各轮（筛选运行）的报告也在 `evidence/v11/unity/`。

## 5 W11b 复查返工（2026-11-16）

基线：`6ab20715`（含主 agent 的商店修正 `8f0433a9` 与瑞士 W12b 合并）。布局与颜色的改动见 [PLAY_LAYOUT](PLAY_LAYOUT.md) §8 第 30–36 行。

### 5.1 改写或加强的断言

| 测试（文件） | 旧期望 | 新期望 | 理由 |
| --- | --- | --- | --- |
| `W11GlyphCheck` / `W11TextPass`（V11PlayScreenAxPlayModeTests.cs；用于 `PLAY_V11_W11_NewTextsAreDrawnCompleteAndReadableAtThreeTextSizes`） | 每个字形在文字自己的框内 | 另外必须在它所在面的“可见框”内：文字下面最上层的实底面（含字形中心与一半以上字形面积、贴图中心不透明），按贴图沿中线实际画出的边框（透明边，以及在九宫格边界内与填充色不同的像素：框线、斜面、底沿、内框线）向内收，再与画在其上、文字之前的空心框（`ui_ring`）内沿取交。一轮里的越框全部列出后再判失败 | 复查第 2 项：首领本关牌的奖励行画在底框线上，旧检查只看文字框所以通过。改后还查出顶栏章节说明压在默认顶栏的内框线上、“已选 1”与“骰池”标签的字压到标签底沿、7 张卡时卡面短文压到卡面右框（PLAY_LAYOUT §8 第 31a、31b 行与卡面边距） |
| 同上 | — | 算式筹码的名称字号 ≥ 19.5 × 文字大小；`ActionReason` 两行时每行 ≥ 3 个字 | 复查第 5、4 项 |
| `PLAY_ART07_ScoreLabelsPreviewMixedZeroAndRandomKeepEveryGlyphAfterRedraw`（Art07ScoreLabelsPlayModeTests.cs） | 每个显示中的筹码的名称与数值都做 GPU 字形检查 | 数值都检查；名称只检查显示中的（行宽不够时最右边的筹码只写数值），基础分筹码必须有名称 | PLAY_LAYOUT §8 第 34 行：名称不再缩小，放不下时让位 |
| `AssertBossReadoutVisible10`（V10PlayModeTests.cs；用于 `PLAY_V10_EveryBossModifierReadoutIsUncoveredAndFitsItsBoxDuringAStage`） | `BossModifierText` 等于减益的 shortText | 去掉换行符后等于 shortText | 两行的效果短文在最接近中间的标点处换行（PLAY_LAYOUT §8 第 33 行）；“完整画出、在框内”的检查不变 |
| `GripPaperPixels07`（Art07DiceAffordancePlayModeTests.cs；用于两项拖动把手测试） | 把手的点是 Paper 色 | 是 `UiKit.OnFelt(UiKit.Paper)`（默认风格没有 onFelt，仍是 Paper） | 复查第 6 项：桌布上的标记用色板的 onFelt |

拖动把手的两项测试（`PLAY_ART07_DICE_VisibleGrip…`、`PLAY_ART07_DICE_DebugAppliedTable…`）没有改：把手移到槽位右列底部后，“在同一点击区内、不压点数、点到把手就是这颗骰子”仍按原样检查。

### 5.2 新增测试 `Tests/PlayMode/V11PlayScreenW11bPlayModeTests.cs`

| 测试 | 检查 |
| --- | --- |
| `PLAY_V11_W11b_LabelsOnFilledSurfacesReachContrastInDefaultAndSwiss` | 默认与瑞士两套风格的四种画面（选骰、首领关开局、计分中停在总分、达标后）：每个画在实底面上的字（判定方法同可见框检查），把字隐藏后截图，取字形下像素的中位色作为面的实际颜色，字色对它的 WCAG 对比度 ≥ 4.5:1（禁用按钮的字除外）；瑞士风格设了 onFelt，桌布上的槽位序号 ≥ 4.5:1、空格的点 ≥ 3:1；默认风格没有 onFelt。逐项写入 `artifacts/cache/v11/w11/<分辨率>/w11b-contrast.txt`（含字形实际画出的像素的对比度，供参考） |
| `PLAY_V11_W11b_SettlePanelTurnsToReachedTheMomentTheTotalReachesTheTarget` | 标准与快速速度，计分演出每个节拍取 8 帧：结算预览面板恰好显示一种状态；显示差距时差距不是“0”且顶栏显示的总分低于目标；显示的总分一到目标就是“现在结束 +N 金币”，之后不再回到差距；标准速度下切换发生在 FinalScore 节拍之内 |
| `PLAY_V11_W11b_HintsBreakAtBalancedPunctuationWithoutOrphans` | 100/125/150% 文字下，提示行可能出现的 19 种文字放进真实的 `ActionReason`：不截断、最多两行、每行 ≥ 3 个字；两行时短的一行至少是长的一行的三分之一，文字中段有标点时在标点处断开。写入 `w11b-hints.txt` |
| `PLAY_V11_W11b_FormulaChipRowFitsWithTheLongestNamesAt150` | 100% 与 150% 文字下四种算式行：最长的一行（十张卡、附魔、全红、灵光一闪与强弩之末两个四字名称）、零倍率（数值最长）、计分后带后置说明、两倍目标的总分（总分筹码加宽）：筹码、乘号与文字都在算式行内且互不重叠，显示的名称 ≥ 19.5 × 文字大小，基础分有名称，名称与数值的字形在筹码的可见框内。写入 `w11b-chip-row.txt` |

### 5.3 运行结果（`23b8dd61`）

| 套件 | 结果 | 报告 |
| --- | --- | --- |
| EditMode（batch） | 2421 / 2421 | `evidence/v11/unity/nunit-20261117-040533.xml` |
| PlayMode 1920×1080（全量） | 222 / 222 | `evidence/v11/unity/nunit-20261117-043523.xml` |
| PlayMode 1280×720（全量） | 222 / 222 | `evidence/v11/unity/nunit-20261117-050557.xml` |

`349af992` 上的第一次 1080p 全量（`nunit-20261117-035332`）为 221 / 222：`PLAY_ART07_POST_PermanentPipTransitionChangesActualGpuPixelsAndRestoresOpaqueFace` 失败。原因是锁定骰子变大后，计分后的永久改点要把骰子退回 0.82 倍，这一步变成了布局移动，移动结束时重新显示了骰面、打断了面的过渡；`23b8dd61` 让这一步立即完成（PLAY_LAYOUT §8 第 36 行），测试没有改。筛选运行的报告也在 `evidence/v11/unity/`。

返工前后的对比图（左为返工前，右为返工后，取自检查点截图）：`artifacts/cache/v11/w11/w11b-evidence/`。

## 6 W11c：瑞士高压关改红（2026-11-17）

基线：`5717b7f9`。做法见 [PLAY_LAYOUT](PLAY_LAYOUT.md) §8 第 37–39 行。

| 测试（文件） | 旧期望 | 新期望 | 理由 |
| --- | --- | --- | --- |
| `W11PrimaryButtons`（V11PlayScreenAxPlayModeTests.cs；用于 `PLAY_V11_AX02_OnePrimaryActionButtonInEveryState`） | 屏幕上的按钮面色与 `UiKit.Primary` 完全相同才算主色按钮；只在默认风格下跑 | 面色与主色同一色族才算：色相差 ≤ 10°、饱和度差 ≤ 0.2、明度在主色的 60% 到 +0.1 之间；同一套状态在默认与瑞士两套风格各跑一遍 | 第 30 行的选色会把主按钮面加深（瑞士 E80E16 画成 D10D16），完全相等只在默认风格成立；“恰好一个主色按钮、禁用时没有、位置不变”的检查不变 |

新增测试 `Tests/PlayMode/V11PlayScreenW11cPlayModeTests.cs`：

| 测试 | 检查 |
| --- | --- |
| `PLAY_V11_W11c_PressureCuesAreRedInSwissAndUnchangedInDefault` | 默认与瑞士、100% 与 150% 文字下的高压关（检查点场景 d）：切换风格后按贴图测得的缓存为空；顶栏目标与结算差额在默认风格下等于 `UiKit.Pressure`，在瑞士风格下与压力色的色相差 ≤ 12°、饱和度 ≥ 0.5、对字下实际画出的底色 ≥ 4.5:1；本关牌红框、托盘细线、进度条正好是压力色；关卡牌签与本关牌标题条与压力色色相差 ≤ 12°、饱和度 ≥ 0.5，标题字 ≥ 4.5:1。逐项写入 `artifacts/cache/v11/w11/<分辨率>/w11c-pressure.txt` |

运行结果（`b36ed975`，其后只有本文档与 PLAY_LAYOUT 的提交）：

| 套件 | 结果 | 报告 |
| --- | --- | --- |
| EditMode（batch） | 2421 / 2421 | `evidence/v11/unity/nunit-20261117-080725.xml` |
| PlayMode 1920×1080（全量） | 223 / 223 | `evidence/v11/unity/nunit-20261117-083643.xml` |
| PlayMode 1280×720（全量） | 223 / 223 | `evidence/v11/unity/nunit-20261117-094343.xml` |

720p 的第一次全量（`nunit-20261117-090850`）为 222 / 223：`PLAY_ART07_FX_FastSourceMarksLowQualityReplayAndSkipReset` 的计时帧长 356 ms（该测试超过约 340 ms 就会越过它要等的节拍，是机器连续负载下的已知计时问题，与本次改动无关；同一代码的 1080p 全量通过）。间隔 5 分钟后重跑全量，全部通过；测试没有改。

返工前后的对比图（瑞士高压关的顶栏、本关牌、结算面板、托盘，两种分辨率）：`artifacts/cache/v11/w11/w11c-evidence/`，返工前的截图在 `artifacts/cache/v11/w11c-before/`。

## 2026-11-23 计分演出的连续原帧

AX-01 的原用例在普通档和快速档分别抽取每个节拍的 2%、50%、98%，两窗各检查 168 个骰身区域。新增取证沿用实际计分录制器，记录普通、快速及带过关印章的演出，从提交首帧一直记录到播放结束后 30 帧。按 Unity 帧号配对场景相机与叠加界面后的实际画面，对每颗可见提交骰的内部像素逐一比较。启用一次刻意覆盖骰子的前景方块，核验比较器确实能发现遮挡。每段还核对计分回执、命令对应的存档与局面。实际通过数按窗口记入[验收记录](../../tasks/P0-V11.md)。[原抽样与证据缺口](../../../artifacts/cache/v11/w25/ax-review-c6/REVIEW.md)
