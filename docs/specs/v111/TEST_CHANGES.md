# V1.1.1 规则层旧测试改写登记

工作包 W3a。范围是 `DicePrototype/Assets/Game/Tests/EditMode/Rules/` 里钉住 p1.1.0 数值或公式、与 [规则增量](RULES_DELTA.md) 冲突的旧测试。没有删除任何测试。与黄金样例文件对拍的测试没有改，见文末。

新期望都是按规格手算的，算式写在对应测试的注释里，没有照抄计分器输出。手算用到的公共数值：0 级一对 ×2、两对 ×3、三条 ×4、四条 ×8、顺子 ×10（牌型表未变）；全蓝 0 级加成 +0.25、全红 +0.5，每级 +0.5（§3.1）；红组 0–2 颗 ×1、3 颗 ×2（§3.2）；基础分附魔 +15、倍率附魔加成 +1.5（§2 步骤 7、9）；得分只在最后向下取整一次。

## ControllerScoreTests

`ExplicitControllerOutcomes` 的数据行，测试名前缀 `CONTENT-`。每行单张卡、容量 5（另注除外）、无剩余投掷。

| 测试名 | 旧期望 | 新期望 | 依据 |
| --- | --- | --- | --- |
| C02-hit | 45 | 18（5×1.25×3=18.75） | §3.1 全蓝加成；§4.1 C02 ×3 |
| C02-boundary-single | 18 | 7（2×1.25×3=7.5） | 同上 |
| C03-not-equipped | 48 | 20（8×2×1.25） | §3.1 |
| C03-boundary-single | 4 | 3（全红 2×1.5） | §3.1 |
| C06-hit | 30 | 40（10×(1+3)） | §4.2 C06 加成 +3 |
| C06-boundary-duplicate-with-four-distinct | 78 | 104（13×2×4） | §4.2 C06 |
| C08-hit | 18 | 9（3×3.25=9.75） | §4.2 C08 +2；§3.1 |
| C08-boundary-one-empty-slot | 180 | 97（10×3×3.25=97.5） | 同上 |
| C09-hit | 18 | 6（2×1.25×2.5=6.25） | §4.1 C09 ×2.5；§3.1 |
| C09-not-equipped | 6 | 2（2×1.25=2.5） | §3.1 |
| C09-boundary-full-reduced-capacity | 30 | 25（10×2.5） | §4.1 C09 |
| C10-hit | 22 | 24（(1+15)×1.5） | §4.3 C10 逐骰 +15；§3.1 |
| C10-miss-pip-two | 6 | 2（2×1.25=2.5） | §3.1 |
| C10-boundary-contribution-does-not-change-pair | 44 | 64（32×2） | §4.3 C10 |
| C11-hit | 540 | 277（10×3×9.25=277.5） | §4.2 C11 +8；§3.1 |
| C11-boundary-extra-die-allowed | 198 | 297（11×3×9） | §4.2 C11 |
| C12-miss-even-unchanged | 18 | 7（6×1.25=7.5） | §3.1 |
| C12-boundary-five-to-six | 72 | 30（12×2×1.25） | §3.1 |
| C13-hit | 21 | 13（(3+8)×1.25=13.75） | §4.3 C13 +8；§3.1 |
| C13-miss-even | 12 | 5（4×1.25） | §3.1 |
| C13-boundary-red-group-uses-original-one | 70 | 54（18×2×1.5，红组 2 颗 ×1） | §4.3 C13；§3.1；§3.2 |
| C14-hit | 27 | 15（3×5.25=15.75） | §4.2 C14 +4；§3.1 |
| C14-miss-mixed-parity | 15 | 6（5×1.25=6.25） | §3.1 |
| C14-boundary-single-one | 6 | 5（1×5.5） | §4.2 C14；§3.1 |
| C17-hit | 27 | 15（(3+9)×1.25） | §4.3 C17 +9；§3.1 |
| C17-miss-four | 12 | 5（4×1.25） | §3.1 |
| C17-boundary-all-three-small-values | 24 | 33（10+11+12） | §4.3 C17 |
| C18-hit | 32 | 48（4×2×6） | §4.2 C18 +5 |
| C18-miss-unequal | 42 | 24（6×4，红组 2 颗 ×1） | §3.2 |
| C18-boundary-two-different-pips | 36 | 54（9×6） | §4.2 C18 |
| C19-hit-threshold → **C19-hit-min-one** | 18 | 4（1×(1+0.5+2.5)） | §4.4 C19 换机制；§3.1 |
| C19-miss-above-threshold → **C19-permanent-sum-no-longer-matters** | 2 | 4（同上，六面点数和不再参与） | §4.4 C19 |
| C19-boundary-remote-current-six-permanent-ones → **C19-miss-min-six** | 42 | 7（6×1.25=7.5，最小点 6 无项） | §4.4 C19；§3.1 |
| C20-hit | 31 | 40（1+2+3+4+(6+24)） | §4.3 C20 末颗 +24 |
| C20-boundary-last-occupied-not-capacity-slot | 18 | 7（6×1.25=7.5） | §3.1 |

C19 是换机制的卡，三行夹具原样保留，名字改成它们现在表达的意思（粗体为新名）。其余 `CONTENT-` 行（C02-miss-red、C03-hit、C06-miss-two-distinct、C08-miss-full、C11-miss-one-three、C12-hit、C20-miss-last-five）混色且未涉及改动数值，期望未变。

`SeveralCopiesOfOneCardStackPerCopyOrApplyOnce` 的数据行，测试名前缀 `CONTENT-DUP-`。

| 测试名 | 旧期望 | 新期望 | 依据 |
| --- | --- | --- | --- |
| C02x2-multiplier-per-copy | 135 | 56（5×1.25×3×3=56.25） | §3.1；§4.1 C02 |
| C10x2-die-add-per-copy | 42 | 46（(1+15+15)×1.5=46.5） | §4.3 C10；§3.1 |
| C27x2-base-add-per-copy | 144 | 110（(4+20+20)×2×1.25） | §4.3 C27 +20；§3.1 |
| C64x2-retrigger-per-copy | 54 | 22（18×1.25=22.5） | §3.1 |
| C109x2-middle-retrigger-per-copy | 42 | 17（14×1.25=17.5） | §3.1 |
| C84x2-hand-add-per-copy | 90 | 45（6×(2+2+2)×1.25） | §4.3 C84 牌型倍率 +2；§3.1 |
| C20x2-slot-add-per-copy | 46 | 64（1+2+3+4+(6+24+24)） | §4.3 C20 |
| C47x4-count-multiplier-per-copy | 96 | 21（2×(1+0.25+4×2.4)=21.7） | §4.2 C47 每张 +0.6；§3.1 |
| C146x2-red-bonus-applies-once | 80 | 48（8×2×2×1.5） | §3.2 红组 3 颗 ×2；§3.1 |

`CopiesKeepTheirOwnGrowthColourRandomOutcomeAndGoldBudget`：

| 断言 | 旧期望 | 新期望 | 依据 |
| --- | --- | --- | --- |
| 两张 C54（一张已长 10）得分与因子 | 9；因子 3/2、1/1 | 4（2×1.25×1.35×1.25=4.21875）；因子 27/20、5/4 | §5 C54 起步 ×1.25、百分单位 |
| 两张 C53（×2、×3）得分 | 36 | 15（2×1.25×2×3） | §3.1 |
| 两张 C138 的钱包与扣款 | 钱包 5：18、扣 3；钱包 6：54、扣 6 | 钱包改为 3：6（6.25）、扣 2；钱包改为 4：15（15.625）、扣 4 | §4.1 C138 每次 2 金、×2.5。夹具钱包随成本从 5/6 改为 3/4，保留“只够一张／够两张”的原意 |
| 两张 C55 的成长计划 | 各 +2 | 各 +3 | §5 C55 每次提交 +3 |

## RulesTests

| 测试名 | 旧期望 | 新期望 | 依据 |
| --- | --- | --- | --- |
| Configuration_ContainsExactly71SupportedItemsAnd13Hands2Colors | 内容数 184 | 185 | §1、§12.3 |
| Score_EqualUpgradedMultipliersPreferHigherStructuralRank | 得分 480 | 200（20×8×1.25） | §3.1 |
| Score_RejectsEmptyDuplicateOverCapacityAndInvalidLevels | 一对 9 级被拒 | 一对 13 级被拒（夹具等级 9 改 13） | §3.1 正常等级 0–12 |

## ScoreExplanationTests

| 测试名 | 旧期望 | 新期望 | 依据 |
| --- | --- | --- | --- |
| Trace_StackedContributionsAddBeforeGlassThenGold → **Trace_StackedContributionsAddBeforeGlassAndGoldMovesToTheAdditiveLine** | 逐骰步骤 C10 +10、C13 +4、C17 +6、C19 +8、玻璃 ×3、黄金 ×2，贡献 174，得分 522 | 逐骰步骤 C10 +15、C13 +8、C17 +9、玻璃 ×3，贡献 99；加成条目 颜色 25、黄金 50、C19 250；得分 420（99×4.25=420.75） | §2 步骤 5、9；§4.3；§4.4 C19；§7.2；§13 |
| Trace_C12EffectivePipsSuppressOldOddAndOnePointAdditions | C17 一步后为 8 | 11（2+9） | §4.3 C17 |
| Trace_GlobalFractionsSurviveEveryControllerAndC20AddsBeforeMultipliers | 步骤 unused_rolls、hand、color、red_group、controller×2、floor；26 → 1501；过早取整对照 1488 | 步骤 unused_rolls、hand、red_group、additive、controller、floor；35 → 385/4 → 385/4 → 1155/2 → 5775/4 → 1443；C18 在加成条目里；过早取整对照 1440 | §2；§3.2；§4.2 C18；§4.3 C20；§13 |

## V06RulesTests（文件 V05RulesTests.cs）

| 测试名 | 旧期望 | 新期望 | 依据 |
| --- | --- | --- | --- |
| AllSubmissionPredicatesHavePositiveAndNegativeEvidence（31 条 `V06-TRIGGER-…-hit`） | 触发时基础分附魔 10 | 15。另删去给 C19 设六面点数和 13 的一行，夹具本身（单颗 1 点）按新规则仍触发 | §2 步骤 7；§4.4 C19 |
| BoundaryTriggersReadFrozenOriginalAndEffectiveLayers | 两个基础分附魔 20；C20 黄金玻璃末骰贡献 126；附魔 10 | 30；90（(6+24)×3，黄金不再翻倍）；15 | §2 步骤 5、7；§4.3 C20；§7.2 |
| BaseEnchantIsOncePerCardAndPairAddIsNotPerDieOrMultipliedByGold | 点数合计 66、对子加值 10、附魔 20、基础分 96、得分 1152 | 48、20、30、98、1078（98×4×2.75） | §2；§3.1；§4.3 C10、C27；§7.2 |
| MultipleMultiplierEnchantmentsUseOneFinalFloor | 基础分 17；分子 204000000、分母 1000000；得分 204 | 基础分 25；分子 425、分母 4（最简）；得分 106（25×4.25=106.25） | §2 步骤 9；§4.3 C10、C39；§12.2 得分分子分母取最简 |
| EquipmentSeparatesFourOrdinarySlotsTenInstancesAndDuplicateIds | 第 6 张（下标 5，C07）改为无附魔后超出 5 栏 | C07 是养成卡，去掉不占栏位附魔后靠养成名额免栏位，不超栏；改用下标 6（C08）构造第 6 张普通卡，其余断言不变 | §6 栏位 |
| HandbookAlwaysContainsFourteenHandAndTwoColorRowsAndSeparatesTemporaryFormationFromUpgradeEligibility | 一对 8 级为满级，倍率 800 | 夹具等级 8 改 12，倍率 1100（2+12×0.75） | §3.1 等级上限 12 |

`enchantMultiplierCount` 的两处断言随字段改名为 `enchantAdditiveCount`，数值未变（§12.2）。`OrdinaryUsed` 的调用补上配置参数（§6）。

## V06SourceBoundaryTests

| 测试名 | 旧期望 | 新期望 | 依据 |
| --- | --- | --- | --- |
| CopiedC45SharesColorButAppliesEachSourceAndPlansOwnerOnce | 附魔 10；得分 168 | 15；90（(18+15)×2.75=90.75） | §2 步骤 7、9；§3.1 |
| C53PreviewUsesSharedUnknownOwnerRangeAndPreservesExplicitBranch | 区间 3–375；明示票 135 | 1–100（5×1.25×0.25=1.5625；5×1.25×16）；56（56.25） | §4.1 C53 新票表；§3.1 |
| ZeroFactorStillTriggersEnchantIncomeAndFrozenPostPlan → **C49HalfFactorStillTriggersEnchantIncomeAndFrozenPostPlan** | 得分 0，乘积分子 0，附魔 10 | 得分 10（17×1.25×0.5=10.625），乘积 1/2，附魔 15 | §4.2 C49 无 5 时 ×0.5 且算触发；§2 步骤 7 |
| ColorSingleDieIndependentAndExcludedFromPrimary | 18；8 级全红 36 | 7（7.5）；33（6×5.5） | §3.1 |
| MultiPairShapesNeverSplitSamePointAndDebugCapsDefinedRedReward | 红组 ×14 | ×11 | §3.2 |
| C44ConvertsWalletBeforeMultiplicationAndExactScoreExceedsInt64 | 钱包加值 9223372036854775807 | 18446744073709551614 | §4.3 C44 每金 +2 |
| AllNewAndReplacementEnchantTicketsMatchIndependentPoolTables | 非提交名单含 C128、C145；计数 17800、844、38900 | 名单去 C128、C145、加 C154；计数 17700（123×100+27×200）、846（123×6+27×4）、38850（123×250+27×300） | §4.5；§12.3 附魔 |
| HandbookUsesMimicAbilitiesAndSeparatesColorFromFourteenHandRows | 全蓝 2 级 `currentMult100` 400 | `currentAdd100` 125（0.25+2×0.5），`currentMult100` 0 | §3.1 |

## 没有改的：与黄金样例对拍的测试

以下测试保持原样，等独立方按 p1.1.1 重生成样例后才会通过。测试文件一个字没动；为了让它们继续编译，`ScoreBreakdown` 保留了一个只读别名 `colorMult100`（= 100 + `colorAdd100`，不序列化），样例重生成时应一并删除。

- `RulesTests.Golden_All21Fields_Plan14_AndNoMutation`（`GOLD-G001…G160`）
- `ScoreExplanationTests.Trace_All160GoldenOutputsAndEveryArithmeticLinkRemainExact`（`TRACE-G001…G160`）
- `V09GoldenTests.GoldenV09_EveryExpectedFieldMatchesTheIndependentReference`（`GOLD09-…`）与 `GoldenV09_FileCoversEveryScoringNewControllerBothTriggeredAndPresent`
- `V10GoldenTests.GoldenV10_EveryExpectedFieldMatchesTheIndependentReference`（`GOLD10-…`）与 `GoldenV10_FileCoversEveryNewScoringControllerAndEveryScoringModifierEffect`
- `V11GoldenTests.GoldenV11_EveryExpectedFieldMatchesTheIndependentReference`（`GOLD11-…`）与四个 `GoldenV11_…` 覆盖测试

失败条数见 [W3a 备注](W3A_NOTES.md)。

后续：W4b 已把这些测试改指 p1.1.1 样例并删除了 `colorMult100` 别名，见文末“W4b”一节。

---

# W3b-1：会话层旧测试改写登记

工作包 W3b-1（[交接包](briefs/W3B1_SESSION_STAGE.md)）。范围是 `DicePrototype/Assets/Game/Tests/EditMode/Session/` 里与 [规则增量](RULES_DELTA.md) 的**关内**规则冲突的旧测试。没有删除任何测试，没有使用 `[Ignore]`：测已移除卡牌（C90、C128、C145）的断言都改成测取代它的新规则。新期望按规格手算，算式写在测试注释里。商店、道具、战利品、存档版本的旧测试没有动，见文末。

公共数值：参照手“五颗红 4、无剩余投掷”= 20 × 五同 12 × 红组五颗 4.5 ×（1 + 全红 0.5）= **1620**（p1.1.0 为 2400）。全蓝 0 级 +0.25。关卡目标（§11）：第 1 关 160、第 2 关 320／高压 480、第 3 关（首领）500，150% 厚墙 750。

## V08BalanceTests

| 测试名 | 旧期望 | 新期望 | 依据 |
| --- | --- | --- | --- |
| V08_SalePriceIsHalfTheListPriceRoundedDownAndNeverBelowOne | 合法（卡，附魔）对数 122×6+28×4=844 | 123×6+27×4=846 | §12.3 附魔资格（去 C128、C145，加 C154） |
| V08_C47CountsHalfPerControllerWithAFloorOfOne → **V08_C47AddsSixTenthsPerHeldController**（7 条） | 控制器乘积 n/2；得分 18、18、27、36、45、72、90 | 乘积恒 1/1，加成条目 60×n；一颗蓝 6：6×(1.25+0.6n) = 11、14、18、21、25、36、43。参数由 4 个减为 2 个 | §4.2 C47 |
| V08_C47CopiedByMimicAppliesTwiceButCountsEachInstanceOnce | 乘积 4/1，72 | 两条加成各 240，乘积 1/1，6×6.05=36 | §4.2 C47 |
| V08_C51GrowsOncePerShopHoweverManyControllersAreSoldAndAgainInTheNextShop | `turnoverUnits2` 3→4→4→4→5 | `growth` 0→80→80→80→160，牌面 +2.6 | §5 C51、§12.1 |
| V08_C53OutcomeTableComesFromConfigurationAndFollowsTheRawTicket | 票表 [2,2,3,4,4,6,8,10,1]，和 40；得分 18×u/2 | [1,2,2,3,4,4,6,6,8]，和 36；得分 floor(15u/4) | §4.1 C53、§3.1 |
| V08_GlassBreaksOnlyBelowTheEightPercentTicket… → **…BelowTheFifteenPercentTicketAfterASubmission…** | 8%，投掷时票 0、7 碎；玻璃 4 得 36 | 15%；投掷四种票都不碎；提交后票 0、14 碎，15、99 不碎；玻璃 4 得 12×1.25=15 | §7.3、§3.1 |
| V08_StageCurveStartsAt300EndsAt1500000… → **V08_StageCurveStartsAt160EndsAt1881334…** | 第 1／11／24 关 300／30000／20000000 | 160／320／500、第 11 关 11510、第 24 关 1881334；每关增长 1.3–2 倍、高压 ×1.5 的断言未变 | §11（临时值，W6 校准后还要再改） |

## V09SessionTests

| 测试名 | 旧期望 | 新期望 | 依据 |
| --- | --- | --- | --- |
| V09_PoolCardsScoreFromTheDiceLeftBehind | 基础 12+18+24，乘积 9/2，1458 | 基础 12+27+36=75，乘积 3/2，75×2×5.25×1.5=1181 | §4.1 C71、§4.2 C74、§4.3 C70／C72 |
| V09_SubmitGrowthUsesTheOldValueThenGrowsAndC54ResetsOnADifferentHand → **…C54HalvesOnADifferentHand** | 2400／3036／390；成长 C55 2、4、6，C54 0、3、0（重置），C56 2，C57 3、6 | 7560／10158／844；C55 3、6、9，C54 0、10、5（减半，回执 `ControllerGrowthHalved` 10→5），C56 40，C57 80、160 | §5 表、§5 C54 |
| V09_MimicUsesTheOwnersGrowthButOnlyTheRealCardGrows | 2400、2880；成长 2、4 | 44×81=3564、50×81=4050；成长 3、6 | §5 C55 |
| V09_C62GrowsOnEveryRollAndC90OnEveryGlassBreak → **V09_C62GrowsOnEveryRollAndTheSubmissionOfThatHandReadsIt** | C62 每掷 +1；C90 每碎一颗玻璃 +15 | C62 部分不变，另加“一手掷三次本手读 +15”；C90 已移除，原 C90 断言改为：带玻璃骰投掷不碎、不消耗效果流、C62 照长 | §5 C62、§4.5、§7.3 |
| V09_StageClearGrowsC58C59PaysC95WidensInterestWithC96AndRaisesResaleWithC99 | C58 4、C59 8；C58 卖价 8/2+1，金币 63 | C58 15、C59 12；卖价 7/2+1=4，金币 62 | §5 C58／C59；C58 改价 7 |
| V09_C60GrowsOnDieUpgradesAndC61OnEveryLevelGained | C60 2；C61 2、4；C43 后 C61 2 | C60 50；C61 50、100；C43 后 50 | §5 C60／C61（只改单位，事件条件未动） |
| V09_C97ComparesAgainstExactlyHalfTheTarget | 目标 300：75 不发，150 发 | 目标 160：一颗蓝 5 带 C50 得 43 不发；红 5 蓝 5（一对、混色）带 C50 得 (10+30)×2=80 恰好一半，发 3 金。夹具由“C46 翻倍”改为“一对” | §11、§4.3 C50 |
| V09_AltarDestroysItsRightNeighbourAtStageStartAndGainsATenthOfItsPrice → **…GainsFifteenHundredthsPerGoldOfItsPrice** | 吃 4 金卡成长 8 | 60 | §5 C100 |
| V09_AnAltarDestroyedByAnotherAltarDoesNotAct | 12 | 90 | 同上 |
| V09_MimicOfTheAltarSharesItsMultiplierButNeverSacrifices | 成长 4，3456 | 成长 30，1620×1.55×1.55=3892 | 同上 |
| V09_RealRollsDecideC75C76C77 | C76 加 20 | 30（说明文字随 C75／C77 改为加成） | §4.3 C76 |
| V09_C68AndC102OnlyActOnTheLastSubmissionOfTheStage | 2400、2400、14400 | 1620、1620、40×12×4.5×6=12960 | §4.2 C102 |

## V10SessionTests

| 测试名 | 旧期望 | 新期望 | 依据 |
| --- | --- | --- | --- |
| V10_StageKindsFollowTheCycle…、V10_CycleViewShows…、V10_ThePreviewQuotes…、V10_TheScapegoat…、V10_TheTalismanRestores… | 高压 900；首领 1000、厚墙 1500 | 480；500、750 | §11 |
| V10_MustFillRejectsAShortSubmissionAndAcceptsAFullRow | 2400 | 1620 | §2、§3 |
| V10_GrowthFreezeStopsEveryGrowthValue… | 对照组 C55 2、C58 4 | 3、15 | §5 |
| V10_DisabledControllersHaveNoAbilityBudgetOrEnchantButStayInPlace | 2400；模仿 C46 得 9600；2400 | 1620；20×12×4.5×5.5=5940；1620 | §4.2 C46 |
| V10_EnchantSilenceStopsBudgetEnchantsAndScoreEnchants | 4800；万籁俱寂失效 3 张、得 4800 | (20+30)×81=4050；失效 2 张，第三张 C50 照发、其倍率附魔静默：50×12×4.5×3.5=9450 | §10 B37、§4.3 C50 |
| V10_ScoringModifiersReachTheScoreThroughTheSession | 1200、1920、240、1200／2400、2400／1200、2400／200 | 810、1296、240、810／1620、1620／810、1620／135 | §2 步骤 8、9 |
| V10_TheTalismanRestores…（末段） | 9600 | 9450 | §4.2、§4.3 |
| V10_StageStartControllersActLeftToRightAndDisabledOnesDoNot | `C128, C136, C110`：最小号骰变玻璃；`C41, C128` 只变一颗 | C128 已移除。改为 `C137, C136, C110`：明星点数 1–6，随机回执依次 `C137Star`、`C110Item`，没有骰变玻璃；`C41, C137` 只抽一次。其余断言未变 | §4.5、§5.3 |
| V10_StageEndFaceGrowthRaisesTheLowestAndLowersTheHighestFace… → **…RaisesTheThreeLowestAndLowersTheThreeHighestFaces…** | 2,2,3,4,5,6／1,2,3,4,5,5／2,2,3,4,5,5 | 2,3,4,4,5,6／1,2,3,3,4,5／2,3,3,4,4,5 | §6.1 C120／C121 |
| V10_TheFadingCardCountsStagesMultipliesLessEachTimeAndExpires → **…AddsLessEachTime…** | ×2.5／×2／×1.5：6000、4800、3600，第三次过关后移除 | +4／+3／+2／+1：5940、4860、3780、2700；第三次衰减后还在（计数 3），第四次才移除 | §4.2 C141、§5.1 |
| V10_BossAndPressureClearsGrowTheirCardsAndPayTheBounty | C129、C132 各 8 | 各 250 | §5 |
| V10_StageEndGeneratorsGrantACommonCardRandomFaceUpgrades… | C115 两次，含落空 | 三次 `RandomUpgrade`、三个不同的面、没有落空，养成计数 3 | §6.1 C115 |
| V10_GlassFilmLowersTheBreakChanceToTwoPercent → **V10_NoCardLowersTheGlassBreakChanceAnyMore** | 有 C145 时投掷碎裂 2% | C145 已移除。提交后固定 15%：票 1、2、7、14 碎，15 不碎，查询返回 15 | §4.5、§7.3 |
| V10_FaceChangeCardsRewriteThePermanentFacesAfterTheScore | C117 只镀末颗、权重都 2；C119 改一面；C122 一步到 2；C125 改纹理；C126 +1 | C117 镀首末两颗，权重 4,3,3,3,4；C119 改最前两面（首末两颗）；C122 设为 5；C125 不再改面（纹理与养成标记都无变化）；C126 +2 | §6.1、§7.2、§4.4 C125 |
| V10_ThrowingGoldPaysAfterTheScoreAndOnlyWhenTheWalletCoversIt | 每次 3 金 ×3：钱包 5 → 7200、余 2 | 每次 2 金 ×2.5：钱包改 3 → 4050、余 1；第二手 1 金不够得 1620；两个来源只左侧触发 | §4.1 C138 |
| V10_DebugScenesCarryTheModifierAndTheItemEffectsOfTheHand | 2400 | 810×2=1620 | §2 |
| V10R_TheTalismanTakesTheTargetFromTheConfigurationAndRechecksTheQualifiedFlag | 夹具累计分 1200（介于 1000 与 1500） | 600（介于 500 与 750） | §11 |
| V10R_DisabledEconomyCardsPayNothingAndADisabledFilmLeavesGlassAtItsBaseRisk → **…AndTheGlassRiskDoesNotDependOnTheCards** | 失效的 C145 不降风险 | C145 已移除：有卡无卡查询都返回 15 | §4.5、§7.3 |
| V11_TwoCopiesTakeTwoSlotsGrowApartAndSellApartWithoutReplacementOrTradeIn | 3600；基础分条目只列有成长的一张；成长 12、2 | (20+22+12)×81=4374；两张各列一条（22、12）；成长 13、3 | §5 C55 |
| V11_SameNameCopiesInANormalRunSurviveRestoresWithTheirScoreHistory | 两条 C46 乘法因子；C55 成长 2、2 | 两条 C46 加成条目；3、3 | §4.2 C46、§5 |
| V11_SessionEffectsStackPerCopyOrApplyOnceAsTheDeltaTableSays | 两张 C145 只生效一次；昙花一现 9000，成长 2 的那张过关后移除 | C145 已移除，该句改为查询恒 15；昙花一现夹具成长改 3：20×12×4.5×6.5=7020，过关后第四次衰减移除 | §4.5、§5.1 |

## V05SessionTests、V06SessionTests、FlowTests、ItemAndSaveTests

| 测试名 | 旧期望 | 新期望 | 依据 |
| --- | --- | --- | --- |
| V05_AllGlassLostAfterQualificationUsesSingleSettlementAndFinalPriority（2 条） | 投掷时最后一颗玻璃碎，直接结算，剩余提交 2 | 投掷不碎；锁定并提交后才碎，结算一次，剩余提交 1；后继判定未变 | §7.3 |
| V05_C26ThenC38LeavesFiveRedOneBlueAndScoreFrozen | 预览效果 2 条 | 非养成效果 2 条，另有 6 条 `FaceCultivated`（C26 五面、C38 一面） | §6 改写与计数 |
| V05_CopyThenGrowthUsesCopiedTargetFaceValueAndSourceIsIndependent | 效果来源序列 C07、C36、C36、C38、C38 | 去掉 `FaceCultivated` 与 `HandRecordBroken` 后同序；另断言养成记录来自 C07、C36、C38 | §6、§5.2 |
| V06_C53SharesOwnerDrawAndPreviewDoesNotConsumeOverride | 覆盖值 1，得 1 | 覆盖值 4（×2），所有者与模仿各乘一次：2×1.25×4=10 | §4.1 C53 |
| V06_ZeroScoreStillConsumesDrawsIncomeAndConsecutiveGrowth | 两颗 2 带 C49（无 5 归零），C53 覆盖 10 | C49 不再归零、票表无 10。夹具改为红 1 蓝 1 一对，`C41,C49,C01,C43,C53`、覆盖 1：4×½×½×½=0.5→0；其余断言未变 | §4.2 C49、§4.1 C53 |
| V06_MatureFundIsNotTradedInAndASecondFundKeepsItsOwnState | C51 `turnoverUnits2` 3、4 | `growth` 0、80（只改这两处，商店断言未动） | §5 C51 |
| V06_C51CopyPurchaseGrowsNothingAndItemSaleDoesNotGrow | `turnoverUnits2` 20,3,3 → 21,4 | `growth` 170,0,0 → 250,80 | §5 C51 |
| V06_DebugLimits32Dice32Cards32ItemsAndScoreBeyondInt64RoundTrip | 32 张 C46（当时 ×2）使得分超 64 位 | C46 现为加成，改用 32 张 C09（×2.5），得分仍超 64 位；断言未变 | §4.1、§4.2 |
| V06_NormalConsecutiveSourcesCapAtEightAndKeepBothTriggers → **…CapAtTwelve…**（2 条） | (7→8)、(8→8) | (11→12)、(12→12) | §3.1 |
| FLOW_C09_C11_BrokenRemoteTargetsConsumeItemAndNoDiceFails → **FLOW_C09_C11_GlassRemoteTargetsSurviveTheRollAndBreakOnlyAfterTheyAreSubmitted** | 遥控重掷时两颗玻璃碎，道具照耗，无骰失败 | 遥控重掷不碎、不抽玻璃票、道具照耗；锁定提交后两颗各抽一票都碎，未达标且无骰，失败原因 `NoDice`。夹具去掉了不再相关的 C05 | §7.3 |
| TwentyFourStageFlowSupportsEightRiskBranchesAndNoFinalShop | 每关锁全部骰、一手过关 | 这是流程测试。p1.1.1 数值下原构筑不能一手打过每个随种子变化的首领（首领还会缩提交栏、减提交次数），所以每关入关后把累计分预设为目标，只锁提交栏容得下的骰，照常掷、计分、提交；自动结算时不再补发 `EndStage`。流程断言（阶段、战利品门禁、通关、无末关商店）未变 | §10、§11。**请协调者确认这种改法**，见 [W3b-1 备注](W3B1_NOTES.md) |

## 没有改的：属于 W3b-2 的旧会话测试（19 条仍失败）

| 测试 | 失败原因 |
| --- | --- |
| ShopTests.SelfStudyAndRandomHandUseCurrentLevelAndEligibility；V08_UpgradePriceRisesByTwoPerExistingLevel（4 条） | 牌型升级价每级 +1（§3.1） |
| V05_EightKindRequiresEightPermanentDiceAndCapacityAndLv8StopsSales | 等级上限 12 与升级价 |
| V05_SameNameCopyInsufficientMoneyAndWriteFailureAreAtomic；V05_SellingOldCardBeforeReplacementPaysFullNewPrice；V09_C98LowersControllerAndDieUpgradePricesPerSourceWithAFloorOfOne；V10_TheRecyclerSellsAtTheFullListPrice；V11_ThreeOrMoreBargainersCanNoLongerBuyAndResellAtAProfitInTheSameShop | 倍率附魔加价 6→5 等商店价格 |
| V05_ShopInterleavesIdAndEnchantmentWithFrozenReferenceVectors（4 条） | 货架生成向量（附魔曲线、内容池变化） |
| V06_InitialZeroWalletAndSchema4SeparateColorLevels | 存档结构号 7→8 |
| V10_EnchantedLootHandLootDiceLootUpgradeLootAndItemLootFollowTheirRules | 战利品测试；现在停在 C61 成长单位（4→100），后面还有 L5 镀金骰 |
| V10_GiftControllersAddAFreeGroupToEveryShopAndAMimicAddsAPick | 赠礼测试；现在停在 C60 成长单位（2→50） |
| V10_NewItemsDoubleTheScoreTurnDiceBlueGildAFaceAndWidenTheRow | 道具测试；得分数值与点金笔加权重 |

后续：这 19 条已由 W3b-2 全部改写，见文末“W3b-2”一节。

---

# W4b：与黄金样例对拍的测试改指 p1.1.1 样例

工作包 W4b（[交接包](briefs/W4B_GOLDEN_GLUE.md)）。范围是 `Tests/EditMode/Rules/` 里与样例文件对拍的测试，以及读同一个旧文件的 `Tests/EditMode/Presentation/PresentationTests.cs`。上文“没有改的：与黄金样例对拍的测试”一节列的测试全部在本包改写。没有删除任何测试方法，没有使用 `[Ignore]`。期望值一律取自独立参考实现生成的 `GoldenScoresV111.json`，本包没有手写或照抄计分器输出的期望值。字段映射、归一化与对拍结果见 [W4b 备注](W4B_NOTES.md)。

## 新增

| 测试 | 内容 |
| --- | --- |
| `V111GoldenTests.GoldenV111_EveryExpectedFieldMatchesTheIndependentReference`（`GOLD111-<样例编号>`，2152 条） | 逐条还原输入、计分、比对样例的全部期望字段（33 个加计划 19 个）与手算核对块 `handChecks`，并断言计分没有改动输入。样例里出现本测试不认识的字段即失败 |
| `V111GoldenTests.GoldenV111_FileIsTheP111SetWithAtLeast2152UniqueCases` | 结构号、规则版本、样例总数下限 2152、编号唯一、各类样例下限（新写 263、矩阵 572、`L08-` 160、`L09-` 257、`L10-` 445、`L11-` 455）、文件头的分类计数与实际一致 |
| `V111GoldenTests.GoldenV111_EveryCaseCarriesTheFullExpectedFieldSet` | 每条样例的期望字段集合恰好是比对表里的那一组，多一个少一个都失败 |
| `V111GoldenTests.GoldenV111_EveryChangedCardHasTriggeredAndUntriggeredCases` | 按样例文件自己的分类（矩阵样例的 `cardId` 与 `family`）：本版改了数值或机制的 100 张卡都在矩阵里；矩阵的 122 张卡每张都有单张触发、失效不触发、两张同名都触发、模仿与真卡同时触发的样例；条件触发的卡另有不靠失效的自然不触发样例，恒触发的卡不许出现 `miss` 样例。触发与否同时看样例期望与游戏实际结果 |
| `V111GoldenTests.GoldenV111_HandCheckedCasesAndEveryBossModifierArePresent` | 带手算核对的样例不少于 263 条；配置里 39 个首领减益都在样例里出现过 |
| `V111GoldenTests.GoldenV111_ReaderKeepsNullEmptyAndNestedValuesApart`、`GoldenV111_ComparisonNoticesAWrongAMissingAndAnUnknownField` | 读入器分得清 null、空串、嵌套空值，拒绝小数、重复键和多余逗号；比对函数对改错一个值、缺一个字段、多一个字段都会报差异 |
| `V111GoldenTests.GoldenV111_ChangingAnySingleExpectedFieldIsNoticed` | 在四条形状不同的样例上，把 33 个期望字段与 19 个计划字段逐个改掉一个值，每次都必须恰好多报一条差异，证明没有哪个字段是空比 |
| `ScoreExplanationTests.Trace_EveryOtherP111GoldenInputAlsoRebuildsStepByStep` | 把 `TRACE-` 那一组的逐步核对用到其余 1992 条输入上（改写的 `L09`–`L11` 输入、新写样例、矩阵），覆盖加成区与新卡 |

## 改写

| 测试 | 原来 | 现在 | 说明 |
| --- | --- | --- | --- |
| `RulesTests.Golden_All21Fields_Plan14_AndNoMutation`（`GOLD-G001…G160`，160 条） | 读 `GoldenScores.json`，按反射比 21 个字段与计划 | 取新文件 `L08-G001…G160`（同一批输入在 p1.1.1 下的期望），比全部期望字段 | 用例名不变。方法名保留（字段数已不是 21／14，方法上有注释）。旧文件里有、新文件没有的中间量不再比：`pairKinds`、`pairBaseAdd`、`walletBaseAdd`、`emptySlotBaseAdd`、`steadyBaseAdd`（它们的和仍由 `baseTotal` 约束）；`enchantMultiplierCount` 改比 `enchantAdditiveCount`，`colorMult100` 改比 `colorAdd100`。新增比对 `scoreNumerator/Denominator`、`pipSum`、加成条目、乘法因子、红组键与成员等 |
| `RulesTests.GoldenFile_HasExactly160UniqueCases` | 旧文件恰好 G001–G160 | 新文件 `L08-` 一段的来源编号恰好是 G001–G160 | 等价 |
| `RulesTests.GoldenJson_PreservesNullEmptyAndNonemptyGroupIdsExactly` | 旧读入器（`GoldenJsonLoader`，用占位串保住 null） | 同一段 JSON 交给新读入器 `V111Json`，仍断言 null、空串、`pair` 三者分得开 | 旧读入器随旧文件退役 |
| `RulesTests.GoldenJson_AllOriginalNullTokensAreAccountedFor` | 旧文件的 null 记号：`chosenGroupId` 的个数与读入结果一致，另有其它可空字段 | 新文件的 null 只允许出现在三处：`bindings`（右邻绑定）、绑定值（无目标）、无卡来源的加成条目的 `emitterId`／`stateOwnerId`。原文里的 null 记号总数必须等于这三处之和；`chosenGroupId`、`colorId`、`upgradeHandId`、`previousPrimary` 不许为 null（新文件用空串） | 新文件的空值约定见 W4-15 |
| `RulesTests.Score_RejectsEmptyDuplicateOverCapacityAndInvalidLevels` | 用旧文件第一条当模板 | 用 `L08-G001` 当模板，并先断言模板本身合法 | 断言不变 |
| `ScoreExplanationTests.Trace_All160GoldenOutputsAndEveryArithmeticLinkRemainExact`（`TRACE-G001…G160`，160 条） | 在旧文件输入上核对说明与结果逐步一致 | 在 `L08-` 输入上做同样的核对；与样例的比对改为全部期望字段 | 用例名不变。逐骰链的终点由“等于贡献”改为“等于该骰合计（贡献 ×（1 + 再计次数））”，这 160 条没有再计，两者相同 |
| `V09GoldenTests.GoldenV09_EveryExpectedFieldMatchesTheIndependentReference`（`GOLD09-…`） | 读 `GoldenScoresV09.json`，260 条 | 取 `L09-` 一段，257 条，比全部期望字段，并重建计分说明 | 少的 3 条（N038、N039、N040）输入里只有已移除的 C90，独立方没有保留 |
| `V09GoldenTests.GoldenV09_FileCoversEveryScoringNewControllerBothTriggeredAndPresent` | 旧文件 ≥150 条；C54–C103 每张计分卡出现 ≥2 次、有触发、条件卡有不触发；恒触发名单含 C90 | `L09-` 一段 ≥257 条，同样的三条断言；恒触发名单去掉 C90；另断言检查的计分卡恰好 42 张 | C91（纹理大师）的条件在 §4.4 换了，旧输入没有一条满足新条件，所以它的“有触发样例”改在全文件里找（矩阵里有）；并断言旧输入确实触发不了它，哪天能触发了这条例外会报错 |
| `V10GoldenTests.GoldenV10_EveryExpectedFieldMatchesTheIndependentReference`（`GOLD10-…`） | 读 `GoldenScoresV10.json`，446 条 | 取 `L10-` 一段，445 条 | 少的 1 条（P214）输入里只有已移除的 C128 |
| `V10GoldenTests.GoldenV10_FileCoversEveryNewScoringControllerAndEveryScoringModifierEffect` | 旧文件 ≥300 条；C104 起每张计分卡三条断言；每个首领减益出现过 | `L10-` 一段 ≥445 条，同样的断言；另断言检查的计分卡恰好 37 张 | C155、C156 是本版新卡，旧输入里不可能有，改在全文件里数 |
| `V10GoldenTests.V10_DisabledControllersPlanNoGrowthNoColourRotationAndNoRandomRedraw` | 用旧文件 `P001` 当模板 | 用 `L10-P001` 当模板 | 断言不变 |
| `V11GoldenTests.GoldenV11_EveryExpectedFieldMatchesTheIndependentReference`（`GOLD11-…`） | 读 `GoldenScoresV11.json`，459 条 | 取 `L11-` 一段，455 条 | 少的 4 条都是专为 C90 设的 |
| `V11GoldenTests.GoldenV11_CoversEveryHistoricalScoringCardWithTwoTriggeringCopies` | 历史计分卡清单（三个旧文件里触发过的卡）等于文件头的 `scoringCards`；每张有两张同名都触发的 `two_hit` 样例 | 历史清单改由 `L08`–`L10` 三段在新规则下触发过的卡得出（120 张，下限 120）；它加上 C91 恰好等于 `L11-` 里带 `two_hit` 的卡；每张历史卡在 `L11-` 里有两张都触发的样例，C91 用矩阵的 `two_hit` | 新文件没有 `scoringCards` 等清单，样例的归类改从编号（`L11-<卡>-<族>`）与矩阵的 `cardId`／`family` 读 |
| `V11GoldenTests.GoldenV11_ConditionalCardsAlsoHaveTwoUntriggeredCopies` | 条件卡清单等于文件头的 `conditionalCards`；每张有两张都自然不触发的 `two_miss` 样例 | 条件卡 = 历史清单 − 恒触发 + C91，等于 `L11-` 里带 `two_miss` 的卡；每张仍要求两张都自然不触发，C19、C127、C138 除外 | C19（改读最小点数）、C127（不再限首手）、C138（花费 2）的旧 `two_miss` 输入在新规则下会触发，改用矩阵的单张 `miss` 样例；并断言旧输入确实不再是两张都不触发 |
| `V11GoldenTests.GoldenV11_MultiplierAndPerDieAdditionCardsHaveThreeTriggeringCopies` | 两份必备名单是文件头两个清单的子集；清单里每张有三张都触发的 `three_hit` 样例 | 两份必备名单（去掉 C90）是 `L11-` 里带 `three_hit` 的卡的子集；这些卡每张有三张都触发的样例，C91 除外 | **覆盖变弱的一处**：新文件里没有三张 C91 都触发的样例（旧输入不满足新条件，矩阵只到两张），C91 退为要求矩阵的两张都触发。已记入备注，请独立方补一条 |
| `V11GoldenTests.GoldenV11_MixesRealCopiesWithMimicsOfTheSameAbility` | 每张历史卡有 `mimic_hit` 样例；逐条检查模仿与真卡都触发 | 范围扩到 `L11-` 与矩阵两处的 `mimic_hit`（至少 242 条逐条检查）；每张历史卡加 C91 都要有 | `L11-C91-mimic_hit` 的旧输入不满足 C91 的新条件，跳过这一条，C91 由矩阵的那条检查 |
| `PresentationTests.AllFrozenGoldenReceiptsAdaptWithoutMutationOrBudgetOverflow`（`ART07_RECEIPT_G001…G160`） | 读 `GoldenScores.json` | 取 `L08-` 一段，回执分数与新文件的 `score` 比 | 用例名不变。本包只让它编译并改了数据来源，没有在 Unity 里运行；界面测试的其余断言属界面工作包 |

随旧文件一起删掉的只是读旧文件用的数据类（`GoldenFile`、`GoldenCase`、`GoldenInput`、`GoldenDie`、`GoldenLevels`、`GoldenBindings`、`GoldenRandom`、`GoldenJsonLoader`，以及 `V09Golden*`、`V10Golden*`、`V11Golden*` 各组），它们不是测试方法。

## 规则宿主条数

| | 总数 | 通过 | 失败 |
| --- | --- | --- | --- |
| 本包之前 | 1825 | 691 | 1134 |
| 本包之后 | 3977 | 3977 | 0 |

条数变化：新增 2152 + 7 + 1 = 2160 条；`GOLD09-`、`GOLD10-`、`GOLD11-` 因独立方没有保留 8 条只含已移除卡牌的旧输入而少 8 条（1825 + 2160 − 8 = 3977）。

## Python 测试

没有改 `tests/` 下任何文件。`tests/test_v11_config.py`（17 条）与 `tests/test_v11_golden.py`（7 条）原先因为读到 p1.1.1 的现行配置而失败，本包把它们测的旧生成器（`tools/v08…v11_config.py`、`tools/v08…v11_golden.py`）改为读写各自版本的冻结副本后，两个文件不经修改全部通过。其余见 [W4b 备注](W4B_NOTES.md) 第 6 节。

---

# W3b-2：会话层旧测试改写登记（商店、道具、存档）

工作包 W3b-2（[交接包](briefs/W3B2_SESSION_SHOP.md)）。范围是 `DicePrototype/Assets/Game/Tests/EditMode/Session/` 里与 [规则增量](RULES_DELTA.md) 的商店、道具、升级价与存档版本规则冲突的旧测试：W3b-1 留下的 19 条，加上本包实现 §7.1、§7.2 后新冲突的 2 条（`CONTENT_UpgradeAppliesOnlyToTargetAndIsIdempotent` 的 U05、U06）。共改写 14 个测试方法、21 条用例。没有删除任何测试，没有使用 `[Ignore]`，没有降低阈值。新期望按规格手算，算式写在测试注释里；货架向量由独立脚本算出（见下）。

公共数值：倍率附魔加价 5（§9.1，原 6）；升级价 = 基础价 + 当前等级 × 1，自选研习再 +1（§3.1）；等级上限 12；参照手“五颗红 4”= 1620。

| 测试名 | 旧期望 | 新期望 | 依据 |
| --- | --- | --- | --- |
| ShopTests.CONTENT_UpgradeAppliesOnlyToTargetAndIsIdempotent（U05） | 带着 `targetFaceId=F01` 买 U05 被接受 | U05 不带面（`targetFaceId` 为空）才被接受；其余断言不变 | §7.1：U05 只选骰、不选面。带面的请求现在被拒，这条规则另由 `V111_TheFaceCountOfAnUpgradeFollowsItsDefinition` 钉住 |
| ShopTests.CONTENT_UpgradeAppliesOnlyToTargetAndIsIdempotent（U06） | 只断言镀金 | 另断言权重 +1；骰子版本号仍只 +1 | §7.2 |
| ShopTests.SelfStudyAndRandomHandUseCurrentLevelAndEligibility | 研习价 5、7；金币 93、86 | 4（2+1+1）、5（2+2+1）；金币 94、89 | §3.1、§9.2 |
| V05_EightKindRequiresEightPermanentDiceAndCapacityAndLv8StopsSales → **…AndLv12StopsSales** | 7 级研习价 20，升到 8 级后停售 | 夹具等级 7 改 11：研习价 17（5+11+1），升到 12 级后停售 | §3.1 |
| V05_SellingOldCardBeforeReplacementPaysFullNewPrice | C02+倍率附魔 13，买后金币 90 | 12（7+5），买后 91；另断言卖掉旧 C02 之前这张报价因“已持有同名乘法卡”被拒 | §9.1 加价、§9.3 |
| V05_SameNameCopyInsufficientMoneyAndWriteFailureAreAtomic | 持 C02 再买 C02+倍率附魔（13 金） | C02 现在是去重卡，不能持两张。夹具换成 C06（4+5=9）：8 金不够被拒，100 金买后 91，卡列 `C06:none, C06:mult2`。原子性与写失败重试的断言不变 | §9.3 |
| V05_ShopInterleavesIdAndEnchantmentWithFrozenReferenceVectors（4 条） | p0.9.0 的四条货架向量 | p1.1.1、第 1 周期的四条：`C59:none,C105:none,C64:none,C153:none`；`C48:none,C65:none,C31:none,C36:slotless`；`C135:none,C30:submit1,C150:roll1,C74:none`；`C142:none,C42:none,C139:none,C13:none` | §9.1。向量由独立脚本按规格文字重算（脚本附在 [W3b-2 备注](W3B2_NOTES.md) 末尾，做法同 `tools/v08_session_vectors.py`），再与游戏输出比对，不是照抄游戏输出 |
| V06_InitialZeroWalletAndSchema4SeparateColorLevels | 存档结构 7；第 1 关目标 300 | 结构 8；目标 160 | §12.1、§11 |
| V08_UpgradePriceRisesByTwoPerExistingLevel → **V08_UpgradePriceRisesByOnePerExistingLevel**（4 条） | 0–7 级，每级 +2 | 0–11 级，每级 +1（货架 基础价+等级，研习再 +1） | §3.1 |
| V09_C98LowersControllerAndDieUpgradePricesPerSourceWithAFloorOfOne | 同名卡用 C02：7+6−1=12 | C02 是去重卡，换成 C06：4+5−1=8；买后持两张 C06。其余价格断言不变 | §9.1、§9.3 |
| V10_EnchantedLootHandLootDiceLootUpgradeLootAndItemLootFollowTheirRules | C61 成长 4；镀金骰只断言两个金面；骰面工坊给每种改造都带面 | 100（两级各 +0.5）；两个金面权重各 2、其余 1；U05 不带面 | §5 C61、§7.2 L5、§7.1 |
| V10_GiftControllersAddAFreeGroupToEveryShopAndAMimicAddsAPick | C60 成长 2；赠礼改造一律带面 | 50；U05 不带面 | §5 C60、§7.1 |
| V10_NewItemsDoubleTheScoreTurnDiceBlueGildAFaceAndWidenTheRow | 灵光一闪 4800；蓝色浪潮 720；点金笔 2880；C134 成长 2 | 3240（1620×2）；300（20×12×1.25）；2160（20×12×4.5×(1+0.5+0.5)），另断言点金笔使该面权重变 2；40 | §2、§3.1、§7.2、§5 C134 |
| V10_TheRecyclerSellsAtTheFullListPrice | 13（7+6） | 12（7+5） | §9.1 |
| V11_ThreeOrMoreBargainersCanNoLongerBuyAndResellAtAProfitInTheSameShop | C47 标价 8、五张砍价高手买入 3、卖 3 | C47 标价 7：买入 2、卖 2（半价 3 被实付价封顶） | §4.2 C47 改价 |

测试支持类 `SessionTestSupport.AddItem` 现在按配置的 `initialUses` 给道具次数（钥匙串 3，其余 1），不属于某一条测试的期望变化。

## 会话宿主条数

| | 总数 | 通过 | 失败 |
| --- | --- | --- | --- |
| 本包之前 | 516 | 497 | 19 |
| 本包之后 | 616 | 616 | 0 |

新增 100 条都在 `V111ShopTests.cs`（54 个测试方法）。

# W7a：界面与演出的旧测试改写登记

工作包 W7a（界面显示跟上 p1.1.1）。依据见 [RULES_DELTA.md](RULES_DELTA.md) 各节。没有删除测试，没有降低阈值。下面“未运行”指本包不能启动 Unity：EditMode 里只有 .NET 宿主能编进去的那部分跑过（表演宿主 `Runtime/Presentation/Validation~/PresentationValidation.csproj`，2374 条全过；夹具预检 `FixturePreflight.csproj`，11 个场景全过），PlayMode 全部只编译过。

## 新增

| 文件 | 内容 | 运行 |
| --- | --- | --- |
| `Tests/EditMode/Presentation/V111DisplayTests.cs` | `DisplayV111_ShownFactorsMultiplyToTheScoreBeforeTheFloor`：`GoldenScoresV111.json` 的每个输入一条（2157 条，名字 `DISPLAY111-<id>`），检查四个数据模型乘出来都等于 `scoreNumerator / scoreDenominator`——因子列表 `ScoreFactors111`（全部因子、去掉 ×1 之后的因子）、单行公式串 `HighScore073.Formula` 写出的数、得分明细的全局行（逐行衔接、加成行 = 1 + A、条目之和 = A、没有独立的颜色／黄金／附魔行）、表演回执（总倍率、每条加成一个事件、加成行在条目之后、卡牌给的加成计入生效张数）。`DisplayV111_SpecExampleOpensTheBonusLineIntoItsEntries`：§13 的例子（+2、+1.5、+0.5 展开，总行 ×5；A 为 0 仍是一行 ×1） | .NET 表演宿主跑过，2158 条全过 |

## 改写：EditMode（.NET 表演宿主跑过）

| 测试 | 原期望 | 新期望 | 依据 |
| --- | --- | --- | --- |
| `HighScore073Tests.HighScore_FormulaListsOnlyFactorsThatChangedTheScore` | `36  ×  28  ×  5  ×  192  ×  2^2`（颜色 ×5、附魔 2^2 各自成项） | `36  ×  28  ×  8  ×  192`（颜色 +4 与两次附魔 +1.5 合成一项加成 ×8）；另加一条带标签的断言 `base=36  ×  hand=28  ×  red=3  ×  additive=7/4  ×  controller=192`。“没有改变得分的因子不写”这一条（期望 `23`）没有动 | §2、§13 |
| `PresentationTests.FX06_G147ExactScoreMultiplierFourSourcesNoQualified` | 得分 181440、总倍率 5040 | 79380、2205（36 × 28 × 8.75 × 9；加成 = 全蓝 4 级 +2.25、C30 +2.5、C32 +3），生效 4 张与第 4 档不变；另断言两条卡牌加成事件、加成行 ×35/4 | §2、§3.1、§4 |
| `PresentationTests.FX03_ZeroAndFractionAreNonIdentityScoringSources` | 千年老二无 5 点：总分事件带“×0”说明 | 无 5 点得 1（2 × 1.25 × 1/2 取整），卡牌因子 1/2，总分事件无说明；有 5 点得 13，卡牌加成 +3/2。生效 1 张、第 0 档不变 | §4.2 C49 |
| `PresentationTests.AggregatedBaseContributionsPreserveEachFrozenEmitterAndExactSum` | 每个来源 +9007199254740993 | 每个来源 +18014398509481986（每金 +2） | §4.3 C44 |
| `V21WrapMatrix.FaceLines`（样本，不是断言） | “计分 ×3；每次掷到它有 8% 概率碎掉，锁定后不会碎”“朝上提交时这颗计分 ×2” | 现在界面实际写的三句：玻璃提示、镀金提示（权重 +1）、镀金提示（权重已到上限） | §7.2、§7.3 |
| `V21WrapTests`（短文字条数） | `184 + 39 + 6` | `activeContentCount + 39 + 6`，并断言内容数 185 | §1 |

`PresentationTests` 的 160 条 `ART07_RECEIPT_` 没有改，适配器改对之后全部通过。`V21WrapTests` 需要字体资源，只编译过。

## 改写：表演夹具（`Runtime/Verification/Art07Fixtures.cs`，夹具预检跑过）

| 场景 | 原来 | 现在 | 依据 |
| --- | --- | --- | --- |
| G147 | 181440、总倍率 5040 | 79380、2205 | 同 FX06 |
| Burst08、Burst09 | 五条 20 级，596160、16560 | 五条 42 级（×180），510300、14175。42 级是让它仍在最高档（总倍率 ≥ 8192）并且仍超过 Burst09 目标 250000 的两倍 | §2；调试等级上限 100 |
| ZeroMultiplier | 千年老二无 5 点得 0 | 场景与名字不变，得 120（16 × 12 × 1.25 × 1/2），并要求有一个 1/2 的千年老二因子；仍是第 0 档 | §4.2 C49 |

## 改写：PlayMode（只编译过，未运行）

| 测试 | 原期望 | 新期望 | 依据 |
| --- | --- | --- | --- |
| `Art07FixturePlayModeTests`（3 处） | 181440、596160、5040 | 79380、510300、2205 | 夹具 |
| `Art07FinalResultPlayModeTests`（G147、ZeroMultiplier、Burst 三处） | 181440；得分 0，卡牌筹码含“没有有效 5 点”；596160 | 79380；得分 120，卡牌筹码 `×0.5`；510300 | 夹具、§4.2 C49 |
| `Art073PlayModeTests`（4 处） | 596,160 | 510,300（仍超过目标两倍，最高档不变）；“零分不庆祝”一句改为“远低于目标一半不庆祝”（120 / 1800000） | 夹具 |
| `Art07InformationPlayModeTests.PLAY_ART07_ScoreStructureChosenItemAndZeroReasonRemainIndependent` | 卡牌筹码含“×0”“没有有效 5 点”，得分 0 | 卡牌筹码含“×0.5”，得分 60（12 × 8 × 1.25 × 1/2，已用真实会话算过） | §4.2 C49 |
| `Art07ScoreLabelsPlayModeTests.PLAY_ART07_ScoreLabelsPreviewMixedZeroAndRandomKeepEveryGlyphAfterRedraw`（Debug32 一段） | 32 张稳稳乘数 = 卡牌 ×2^32，筹码“约 ×4.29×10⁹” | 稳稳乘数现在是加成，不再相乘；本测试把场景里的 32 张卡换成蓝色疯狂（每张 ×3），卡牌 ×3^32 = 1853020188851841，筹码“约 ×1.85×10¹⁵”。“十位以上的卡牌倍率读成科学计数、明细保留全部数字”的检查内容不变 | §4.1 C46 |
| `Art071EnchantIntegrationPlayModeTests.PLAY_ART071_ENCHANT_MultiplierArcReachesActualReceiptAreaWithoutMutatingReceipt` | 事件 `enchantment_multiplier`，筹码名“附魔” | 事件 `enchantment_additive`（+3/2），筹码名“加成” | §2 步骤 9 |
| `Art07EdgeScreensPlayModeTests.PLAY_ART07_…`（统计一段） | 颜色乘数是整数，最高倍率 `×N` | 单颗骰的加成只有颜色 0 级值；最高倍率按百分单位比较（`×1.25`／`×1.5`） | §3.1 |
| `Art07CardReadabilityPlayModeTests`（期望脚注） | 人员流动“当前 ×(turnover/2)” | “当前 加成 +X”（起步 + 补偿 + 成长） | §5 C51 |
| `V09PlayModeTests`（3 个测试 6 处） | 祭坛“×1.4”；积少成多“+24”“+26”、基础加值 24；老当益壮“×1.4”、成长 4 | 祭坛“得分 ×(1.25 + 基础价 × 0.15)”；积少成多“基础分 +36”“基础分 +39”、基础加值 36；老当益壮“得分 ×1.33”、成长 15。数值从配置读 | §5 |
| `V14ShopPlayModeTests`（镀金、玻璃两处） | 预览以“（黄金）”结尾，提示“朝上提交时这颗计分 ×2”；“每次掷到它有 X% 概率碎掉，锁定后不会碎” | 预览以“（黄金、权重 2）”结尾，提示“朝上提交时加成 +0.5；权重 +1（上限 5）”；“提交后每颗有 X% 概率碎裂，留在骰池里不会碎” | §7.2、§7.3 |
| `V15LootCoveragePlayModeTests`（牌型升级战利品） | 文字含“倍率 ×” | 颜色升级含“加成 +”，牌型升级仍含“倍率 ×” | §3.1 |
| `V24AppendixPlayModeTests`（附魔两行） | “基础 +10：这张卡生效时基础分 +10”“得分 ×2：这张卡生效时得分 ×2” | “基础分 +15：…基础分 +15”“加成 +1.5：…加成 +1.5”（数值读配置） | §2 步骤 7、9，§12.3 |
| `V24ContentTextPlayModeTests.PLAY_V24_TX06_HandUpgradePreservesPriceLevelsAndActualMultiplier` | 最高 8 级；2 级一对升级价 6 | 最高 12 级；价 4（基础 2 + 2 级 × 1） | §3.1 |
| `Art07PostEffectsPlayModeTests`（无心插柳一处） | 接受 `RandomUpgrade` 或 `RandomUpgradeMissed` | 只接受 `RandomUpgrade` | §6.1 C115 |

### 玻璃不再在投掷时碎（§7.3）：四个测试

| 测试 | 原来检查的 | 现在检查的 |
| --- | --- | --- |
| `Art07PostEffectsPlayModeTests.PLAY_ART07_POST_ThirdAndFourthBrokenDiceKeepOriginalOutlineAndOnlyTwelveFragments` | 投掷碎 4 颗；第 3、4 颗留轮廓，2 个碎裂特效共 12 片 | 投掷不碎、没有轮廓和碎裂特效；锁定 4 颗玻璃骰提交后，回执按提交顺序记 4 条 `DieBroken`，骰子少 4 颗，计分记录里有 4 行“玻璃骰碎了”；跳过后状态与存档不变 |
| `Art07FxIntegrationPlayModeTests.PLAY_ART07_FX_RealRollAndRemoteBreakUsePlacedWorldPoolAndSkipResets` | 投掷、遥控骰子各碎 1 颗，碎裂特效放在骰子原来的位置、朝向、大小上 | 投掷与遥控骰子都不碎、不抽玻璃票、没有碎裂特效；跳过后特效清零、状态与存档不变 |
| `UiBoundaryPlayModeTests`（遥控骰子，`breakTarget` 两轮） | 抽到碎裂票时目标 D001 消失、提示“1 颗玻璃骰碎了” | 两轮都是 5 颗骰子、D001 仍是玻璃并取到遥控点数、不记碎裂、不抽玻璃票、提示里没有“玻璃骰碎了” |
| `UiBoundaryPlayModeTests.PLAY_ExistingRunRollAndSubmitSaveFailureRetriesExactCandidateAndPresentsOnce`（投掷一轮） | 重试的候选状态里 D001 已碎，提示“1 颗玻璃骰碎了” | 候选状态不记碎裂，D001 仍在并已投掷，提示里没有“玻璃骰碎了” |

**这四处少了的检查**：碎裂特效的位置与朝向、第 3 和第 4 颗的轮廓、12 片碎片的上限。它们检查的是“投掷时碎”的演出，规则改了之后这个演出不再发生；“提交后碎”目前只有文字和音效，碎片演出还没有接到提交上（见 [W7A_NOTES.md](W7A_NOTES.md) 留给 W7b 的事）。接上之后这几条检查应当在提交路径上补回来。

## 没有改的：已知会失败或可能失败的 PlayMode 测试

- 图标张数 184：`GameplayPlayModeTests.cs:133–134`、`V13CardTemplatePlayModeTests.cs:441`、`W22iArtInstallPlayModeTests.cs:101`。p1.1.1 是 185 项，新图入库属于 W8。
- 本包只按搜索到的旧文案、旧数值和旧回执去找过时断言，没有逐个读完 150 个 PlayMode 文件，也没有运行。其余因数值变化而失败的断言要等在 Unity 里跑一遍才能列全。
