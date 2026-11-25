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
