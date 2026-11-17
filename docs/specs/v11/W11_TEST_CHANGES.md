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
