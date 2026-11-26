# W4b 备注：C# 计分与独立黄金样例的首次对拍

工作包 W4b（[交接包](briefs/W4B_GOLDEN_GLUE.md)）。依据 [规则增量](RULES_DELTA.md)、[W4 待裁定的读法](W4_OPEN_READINGS.md)、[W3a 备注](W3A_NOTES.md)。基线 `a17a3592`，分支 `codex/v1.1.1`。测试改写登记在 [TEST_CHANGES.md](TEST_CHANGES.md) 的 W4b 一节。

本包没有改 `tools/v111_golden.py`、`Data/GoldenScoresV111.json`、`evidence/v111/rules/golden-provenance.json`、`tests/test_v111_golden.py`，也没有改 `Core/Rules` 的计分逻辑（只删了 `ScoreBreakdown.colorMult100` 这个只读别名）。

## 1. 对拍结果

| 项 | 结果 |
| --- | --- |
| 样例数 | 2152 |
| 首次运行通过 | 2152 |
| 首次运行失败 | 0 |
| (a) C# 违反规格正文 | 0 条，没有改 C# 计分 |
| (b) 规格两种读法 | 0 条（有一处字段形状不同，数值一致，见 3.1） |
| (c) 样例违反规格正文 | 0 条 |

“首次运行”指 `V111GoldenTests` 写完后第一次在规则宿主上执行（提交 `11f8980e` 的内容），之前没有试跑、没有按结果调整过比对代码或归一化。

因为一条都没失败，另做了一次反向检查，确认比对不是摆设：临时把 `ScoreCalculator` 改错五处（连庄换牌型不列 `halve`；见五成群跳过已是 5 的面；倍率附魔加成多 0.01；玻璃名单只列点数大于 3 的骰；`triggeredControllerIds` 倒序），重跑后 2152 条里 196 条失败，差异落在 `growthChanges`、`faceChanges`、`additiveEntries`、`additiveTotal100`、`scoreNumerator/Denominator`、`score`、`triggeredControllerIds` 以及对应的 `handChecks` 上。改动随即还原，没有提交。五处里玻璃那一处没有引出失败：全文件只有 9 条样例带玻璃骰，点数都大于 3（见 4.2）。

此外有一条常驻的自检 `GoldenV111_ChangingAnySingleExpectedFieldIsNoticed`：在四条形状不同的样例上把 33 个期望字段与 19 个计划字段逐个改掉一个值，每次都恰好多报一条差异，说明没有哪个字段是空比。

`handChecks`（263 条新写样例里独立方手算的那部分期望）也逐字段比过，全部一致。

## 2. 字段映射与归一化

### 2.1 读入方式

样例文件有键不固定的对象（`input.bindings`）和有含义的 null，`JsonUtility` 两样都表达不了。测试里写了一个只认整数的小读入器（`V111Json`，在 `V111GoldenTests.cs` 内），把文件读成字典、列表、字符串、64 位整数、布尔和 null，Unity 与 .NET 宿主走同一份代码。小数、指数、重复键、多余逗号一律拒绝。文件里没有小数。

### 2.2 输入 → `SubmissionSnapshot`

| 样例字段 | 快照字段 | 归一化 |
| --- | --- | --- |
| `capacity`、`rollsRemaining`、`rollsUsed`、`submissionsRemainingBefore`、`permanentDiceCount`、`wallet`、`redWave`、`blueWave`、`debug`、`stageKind`、`modifierId`、`inventoryCount`、`ordinaryControllerSlots`、`scoreItemMultiplier`、`disabledControllerIds`、`stageHandsUsed`、`stageScoreBefore`、`stageTarget`、`handLevels`、`colorLevels` | 同名 | 无。`modifierId` 的空串原样传入（计分器把空串当作无减益） |
| `submissionOrdinal` | `submissionOrdinalBefore` | 无 |
| `previousPrimary` | `previousPrimaryHandId` | **空串 → null**（2034 条）。计分器对两者处理相同 |
| `bindings` | `mimicBindings` | **null → null**（2119 条，表示模仿右邻）；对象 → 显式绑定数组，键为模仿者；**值为 null 原样保留**为“无目标”（1 条），指向不存在实例的值也原样传入（2 条） |
| `randomChoices[]` | `resolvedRandomChoices[]` | 取 `ownerInstanceId`、`units2`。`ticket`（票号，独立方自定）不传，计分器只读 `units2` |
| `controllers[]` | `equippedControllers[]` | `instanceId`、`definitionId`、`enchantmentId`、`growth`、`growthCatchup`、`turnoverUnits2`、`starPip` 原样；`acquiredSerial` 按输入顺序从 1 起编（§12.1“无实得序的合成卡列按输入顺序赋序”）；**`activeColor` 空串 → null**（3546 个实例）；`cultivationFacesChanged` 样例没有，取 0 |
| `dice[]`（有六面数据，8326 颗） | `dice[]` | `currentColor`、`isGold` 取朝上那一面的 `color`、`gold`；六面进 `permanentFacesCopy`（`baseWeight` 取 1，计分不读）；`permanentSixFaceSum` 取六面点数和（计分不读，只过合法性检查） |
| `dice[]`（来自 `GoldenScores.json` 的合成快照，`faces` 为空、`faceId` 为空串，527 颗） | `dice[]` | **`faceId` 空串 → `F01`**（计分器要求面号非空且在对面表里）；**不给六面副本**；`permanentSixFaceSum` 取 21；颜色与黄金取 `dice[].color`、`dice[].gold`。没有六面副本时 C92、C93、养成改写一律不成立，与 W4-14 的读法一致 |
| `pool[]` | `poolDice[]` | 无 |

### 2.3 期望 → C# 的量

除下表“对应量”一栏另有说明的以外，都是 `ScoreBreakdown`／`AfterScorePlan` 的同名字段，逐个比成规范文本（字符串原样，整数按十进制，数组按元素顺序，对象数组按固定列顺序）。对象数组的每一行若出现比对表以外的键，或少了键，该字段判为读不出、记为差异。

| 样例字段 | 对应量 | 归一化 |
| --- | --- | --- |
| `score`、`scoreNumerator`、`scoreDenominator`、`baseTotal`、`pipSum`、`pipContributions`、`pipRetriggers`、`sealedDice`、`enchantBaseAdd`、`primaryHandId`、`handMultAdd100`、`handMultBeforeModifier100`、`totalHandMult100`、`colorAdd100`、`redCount`、`redPoolCount`、`redBonusCount`、`redMult100`、`additiveTotal100`、`enchantAdditiveCount`、`controllerMultiplierNumerator/Denominator`、`triggeredSourceIds`、`triggeredControllerIds` | 同名 | 无 |
| `chosenGroupId`、`colorId` | 同名 | **空串与 null 视为相等**（样例写空串，计分器给 null） |
| `controllerBaseAdds[]` | 同名，比 `emitterId`、`abilityId`、`amount` | 计分器多出的 `stateOwnerId` 样例没有，不比 |
| `additiveEntries[]` | 同名，比全部六列 | **`emitterId`、`stateOwnerId` 的 null 与空串视为相等**（在 .NET 宿主上两边对无卡来源都给 null，不放宽也通过；放宽是为了防备 Unity 的 `JsonUtility` 把字符串字段的 null 存成空串，这一点没有在 Unity 里验证）。`dieIndex` 两边都是整数，非黄金为 −1，没有做 null → −1 的映射（样例没有写 null） |
| `multiplierFactors[]`、`globalFactors[]` | 同名，比 `emitterId`、`abilityId`、`numerator`、`denominator` | 计分器多出的 `stateOwnerId` 不比 |
| `triggeredMultiplierIds` | `multiplierFactors[].emitterId` | 形状不同，见 3.1 |
| `redGroupKey` | `redValue` | 无 |
| `redMemberIndices` | 由 `effectivePips`、`effectiveColors`、`redGroupingMode`、`redValue` 推出 | 计分器不输出成员名单，见 3.2 |
| `afterScorePlan` 的 19 个字段 | 同名 | **`upgradeHandId`、`faceChanges[].text` 的空串与 null 视为相等**；`faceChanges[]` 比 `sourceId`、`emitterId`、`stateOwnerId`、`dieId`、`faceId`、`kind`、`amount`、`text`；`growthChanges[]` 比 `ownerInstanceId`、`abilityId`、`delta`、`reset`、`halve`；`cultivationSourceBindings[]` 比三列 |

空值归一化到此为止，共五种：`previousPrimary`、`activeColor`（输入，空串 → null）；合成骰的 `faceId`（输入，空串 → `F01`）；`chosenGroupId`、`colorId`、`upgradeHandId`、`faceChanges[].text`（期望，空串 ≡ null）；`additiveEntries[]` 的两个实例字段（期望，null ≡ 空串）。没有 −1 与 null 之间的归一化。

## 3. 两边形状不同但数值一致的地方

这两处都不算对不上，记在这里请协调者知悉。

### 3.1 `triggeredMultiplierIds`

- 样例：每个乘法因子的**发出者实例编号**，与 `multiplierFactors` 同序（W4-01）。
- 计分器：每个乘法因子的**技能编号**（`factors.Select(f => f.abilityId)`；`ScoreExplanationTests` 的 `TRACE-` 一组按这个口径拿它与计分说明里控制器步骤的 `sourceId` 比）。
- 规格：§12.2 只写“保留 `triggeredMultiplierIds` 同序”，没有写元素是什么；在 `docs/specs/` 的 v0.6、V0.8、V0.9、V1.0、V1.1 规格里搜不到这个字段名。
- 处理：两边都没改。测试把样例的这一列与计分器 `multiplierFactors[].emitterId` 比（同一个量），技能编号则通过 `multiplierFactors[].abilityId` 比到。2152 条全部一致。
- 待裁定：规格写明这个字段装实例编号还是技能编号，然后改生成器或改计分器其中一边的字段内容。数值不受影响。

### 3.2 `redMemberIndices`

计分器输出红组键（`redValue`）、分组方式（`redGroupingMode`）和各种计数，但不输出被选中红组的成员下标。测试用计分器输出的有效点数、有效颜色、分组方式和键反推成员，再与样例比；提交红骰数为 0 时取空。反推只用了计分器自己的输出与 C24 的两个配置参数。2152 条全部一致。若要计分器直接给出成员名单，需要加字段，属契约变化，没有做。

## 4. 没有比对的内容

### 4.1 字段

| 内容 | 原因 |
| --- | --- |
| 样例期望字段 | **没有不比的**：33 个顶层字段与计划的 19 个字段都在比对表里；`GoldenV111_EveryCaseCarriesTheFullExpectedFieldSet` 断言每条样例的字段集合恰好是这一组 |
| `input.randomChoices[].ticket` | 独立方自定的输入字段。计分器不抽票，只读冻结的 `units2`；票号到结果的映射属会话 |
| 计分器有、样例没有的输出：`effectivePips`、`effectiveColors`、`matchedGroupIds`、`groupMult100`、`straightMult100`、`unusedRollBonus`、`pairKinds`、`pairBaseAdd`、`walletBaseAdd`、`emptySlotBaseAdd`、`steadyBaseAdd`、`enchantBaseCount`、`redGroupingMode`、`redRewardCapped`、`retriggerEntries`，以及因子与基础分加值上的 `stateOwnerId` | 样例没有给期望。其中 `effectivePips`、`effectiveColors`、`redGroupingMode` 间接用于 3.2；基础分的各分项由 `baseTotal` 合计约束；`enchantBaseCount` 即 W4-06 说的“原计数”，独立方没有输出。旧的 `GOLD-` 测试曾逐项比 `pairKinds`、`pairBaseAdd`、`walletBaseAdd`、`emptySlotBaseAdd`、`steadyBaseAdd`，这五项现在没有独立期望 |
| 样例的 `description`、`handChecked`、`derivedFrom.rewrites`，文件头的 `fieldNotes`、`openReadings` | 说明性内容，不是计分结果 |

### 4.2 覆盖上的薄处（不是差异）

- 三张 C91（纹理大师）都触发的样例在新文件里没有：旧的 `L11-C91-three_hit` 输入（点数 2、4、5）不满足 §4.4 的新条件，矩阵只做到两张。`GoldenV11_MultiplierAndPerDieAdditionCardsHaveThreeTriggeringCopies` 对 C91 因此退为两张。
- 带玻璃骰的样例只有 9 条，且玻璃骰点数都大于 3；`glassRiskDieIds` 对低点玻璃骰、封印的玻璃骰没有样例。
- 旧文件的 8 条输入没有进新文件（N038、N039、N040、P214、`C90-two_hit`、`C90-three_hit`、`C90-mimic_hit`、`C90-separate_state_and_mimic`），原因都是只含已移除的 C90 或 C128，见来源记录的 `excluded_historical_inputs`。

## 5. 需要独立方处理的地方

1. **`tools/v111_golden.py` 第 37–38 行**（`HISTORICAL`）仍从 `DicePrototype/Assets/Game/Data/` 读四个旧文件。本包按交接包把旧文件移走后，这两行要改成：

   | 前缀 | 新位置 |
   | --- | --- |
   | `L08` | `tools/balance_sim/baseline/GoldenScores.p0.8.0.json` |
   | `L09` | `tools/balance_sim/baseline/GoldenScoresV09.p0.9.0.json` |
   | `L10` | `tools/balance_sim/baseline/GoldenScoresV10.p1.0.0.json` |
   | `L11` | `tools/balance_sim/baseline/GoldenScoresV11.p1.1.0.json` |

   在改之前，`tools/v111_golden.py` 的三种模式都会在读旧输入时报找不到文件，`tests/test_v111_golden.py` 的 `setUpClass` 也因此出错，19 条测试都跑不起来（本包之前 18 条通过、1 条失败，见下一条）。连带要注意：
   - 生成器把文件名写进每条旧样例的 `source`（`path.name`）和 `derivedFrom.file`（相对路径），也写进来源记录的 `input_sha256`、`historical_inputs` 的键。路径一改，样例文件与来源记录都要重生成，两个文件的哈希会变。四个文件的内容没有变（见第 7 节的哈希），所以重算的期望值不会变。
   - `tests/test_v111_golden.py` 第 54–57 行按旧文件名查 `by_source` 和被排除的输入；若 `source` 跟着新文件名变，这里要一起改。
   - 文件头 `fieldNotes.input` 里“来自 GoldenScores.json 的合成快照”一句。
   - C# 一侧不依赖这些文件名：旧输入按编号前缀 `L08-`…`L11-` 识别，只要求 `derivedFrom.file` 里含 `GoldenScores`／`GoldenScoresV09`／`GoldenScoresV10`，新旧文件名都满足；`source` 只认 `new` 与 `matrix` 两个值。
2. **来源记录已经过期（本包之前就是这样）**：`evidence/v111/rules/golden-provenance.json` 记的 `docs/specs/v111/RULES_DELTA.md` 哈希是 `687b99da…`，现在的文件是 `c8f41d00…`。规格在样例生成之后被提交 `a17a3592`（“reachable example for GrowthHalf”）改过。`tests/test_v111_golden.py` 的 `test_check_files_match_the_generator` 在本包动手之前的基线里就因此失败（样例文件本身与生成器一致，只是来源记录不一致）。重生成来源记录即可；是否要按改过的 §5.1 例子调整那条样例，由独立方判断。
3. 建议补的样例（不影响本次结论）：三张 C91 都触发的一条；低点数或被封印的玻璃骰各一条。
4. 3.1 裁定后，若定为技能编号，改生成器的 `triggeredMultiplierIds`。

## 6. 三套测试的数字

| | 本包之前 | 本包之后 | 说明 |
| --- | --- | --- | --- |
| 规则宿主 | 1825 条，691 通过，1134 失败 | 3977 条，3977 通过，0 失败 | 条数变化见 TEST_CHANGES 的 W4b 一节 |
| 会话宿主 | 516 条，497 通过，19 失败 | 516 条，497 通过，19 失败 | 失败的 19 条逐名相同，属 W3b-2，没有动 |
| Python 套件 | 跑了 506 条，失败 16、出错 12 | 跑了 494 条，失败 14、出错 12 | 条数少 12：`test_v111_golden` 的 19 条没能跑，`test_v11_golden` 的 7 条恢复运行。逐条见下 |

Python 套件两次都用 `E:\anaconda3\python.exe -m unittest discover -s tests` 在同一环境下整套运行（约 33 分钟）。逐条对照：

| 测试 | 本包之前 | 本包之后 | 归属 |
| --- | --- | --- | --- |
| `test_opening_dp.GateBX03Tests.test_colour_blind_at_least_85pct_of_optimal` | 失败（84.68 < 85） | 失败，没有动 | BX-03，待用户决定 |
| `test_v11_config.V11ConfigTests.test_generated_files_are_reproducible_utf8_lf` | 失败（拿 p1.1.0 生成结果比现行 p1.1.1 配置） | **通过** | 本包：`v11_config.TARGET` 改为冻结副本，测试文件没改 |
| `test_v11_golden.V11GoldenContractTests`（7 条） | `setUpClass` 出错，7 条都没跑（旧参考实现读现行配置，找不到 `scoreMultiplier`） | **7 条都通过** | 本包：`v11_golden.py`、`v10_golden.py` 改读冻结配置与冻结样例，测试文件没改 |
| `test_v111_golden.V111GoldenTests`（19 条） | 18 条通过，`test_check_files_match_the_generator` 失败（来源记录里的规格哈希过期，见第 5 节第 2 条） | **`setUpClass` 出错，19 条都没跑**（生成器在游戏目录里找不到旧文件） | 本包移走旧文件造成，等独立方改 `tools/v111_golden.py` 第 37–38 行（第 5 节第 1 条）。这是本包之后唯一新增的失败 |
| `test_art_v11_crops.CropTableTests.test_ids_match_illustrations_and_config` | 失败（裁切表的默认项里还有已移除的 C90、C128、C145） | 失败，没有动 | 美术（W8） |
| `test_art_v11_install.InstalledArtTests` 的 3 条（11 个子项） | 失败 | 失败，没有动 | 美术（W8） |
| `test_v21_title_chars.TitleFontInputsTests.test_versioned_character_files_match_game_inputs` | 失败（游戏文字变了，字符表文件与现行文字不一致） | 失败，没有动 | 界面文字 |
| `test_v21_linebreak_words.LineBreakWordsTests.test_every_protected_word_is_used_in_the_target_text` | 出错（保护词“件数”在现行文字里已不出现） | 出错，没有动 | 界面文字 |
| `test_package_v11.Publication11Tests` 的 10 条 | 出错（运行环境的 PATH 里没有 `git`） | 出错，没有动 | 环境。把 git 加进 PATH 单独跑这个文件，本包前后都是 23 条全部通过 |

后五行的失败都不是本包造成的，也不该靠改成“对冻结基线成立”来消掉：它们检查的是现行内容与现行配置是否配套（插画、字符表、换行词），需要对应的工作包重生成产物。本包没有改 `tests/` 下任何文件。
旧生成器的自检（本包之后，命令行直接运行）：

| 命令 | 结果 |
| --- | --- |
| `tools/v08_config.py --check`、`v09_config.py --check`、`v10_config.py --check`、`v11_config.py --check` | 都通过（各自与冻结副本逐字节一致） |
| `tools/v111_config.py --check` | 通过（没有改它） |
| `tools/v08_golden.py verify`／`check` | 都通过 |
| `tools/v09_golden.py`、`v10_golden.py`、`v11_golden.py` 的 `verify` | 都通过（旧样例在各自冻结配置下逐条重现） |
| `tools/v09_golden.py`、`v10_golden.py`、`v11_golden.py` 的 `check` | 样例文件与生成器一致；**来源记录不一致，退出码非零**。原因是这三份旧来源记录（`evidence/v09`、`v10`、`v11` 下的 `golden-provenance.json`）钉着生成器文件自身的哈希和旧路径，而本包改了生成器里的路径常量。旧来源记录是当时版本的证据，没有重写；是否补发由协调者决定。本包之前这三个 `check` 更早就过不了（现行配置已是 p1.1.1） |

## 7. 旧样例文件的去向

| 游戏目录里的文件 | 处理 | 对应的基线文件 | SHA-256 前 16 位 |
| --- | --- | --- | --- |
| `Data/GoldenScores.json` | 删除（已有同内容副本） | `tools/balance_sim/baseline/GoldenScores.p0.8.0.json` | `f9ffc7640d31d3a1`（两者相同） |
| `Data/GoldenScoresV09.json` | 删除（已有同内容副本） | `tools/balance_sim/baseline/GoldenScoresV09.p0.9.0.json` | `2aff977888f4b6e2`（两者相同） |
| `Data/GoldenScoresV10.json` | `git mv` | `tools/balance_sim/baseline/GoldenScoresV10.p1.0.0.json` | `e9b6d1f41dbf83c3` |
| `Data/GoldenScoresV11.json` | `git mv` | `tools/balance_sim/baseline/GoldenScoresV11.p1.1.0.json` | `13262d1bdfe54884` |

两点与交接包字面不同，请协调者确认：

- **后缀取文件头里的规则版本。** 交接包举的例子是 `GoldenScoresV10.p1.1.0.json`；V10 文件头写的是 `"configVersion":"p1.0.0"`，同目录已有的 `GoldenScoresV09.p0.9.0.json` 也是按生成时的版本命名，`tools/v11_golden.py` 还断言 V10 文件“必须保持 p1.0.0”，所以取了 `p1.0.0`。要改成 `p1.1.0` 只需再 `git mv` 一次并改 `tools/v10_golden.py`、`tools/v11_golden.py` 各一行。
- **四个 `.meta` 没有带过去，直接删了。** `tools/` 不是 Unity 目录，基线目录里的其它文件都没有 `.meta`。

改了路径的引用：

| 文件 | 改动 |
| --- | --- |
| `Tests/EditMode/Rules/RulesTests.cs`、`ScoreExplanationTests.cs`、`V09GoldenTests.cs`、`V10GoldenTests.cs`、`V11GoldenTests.cs`，`Tests/EditMode/Presentation/PresentationTests.cs` | 不再读旧文件，改读 `GoldenScoresV111.json` 的对应一段 |
| `tools/v08_golden.py`、`v09_golden.py`、`v10_golden.py`、`v11_golden.py` | `CONFIG` 改为各自版本的冻结配置（`P0Config.p0.8.0`／`p0.9.0`／`p1.0.0`／`p1.1.0`），`GOLDEN` 与 `v11_golden.py` 的 `HISTORICAL` 改为基线目录里的文件 |
| `tools/v08_config.py`、`v09_config.py`、`v10_config.py`、`v11_config.py` | `TARGET` 改为各自版本的冻结配置。写入模式因此也只写冻结副本，不会再把旧版本写进现行游戏配置 |

没有改、仍写着旧路径的地方（都不在交接包列的范围内，留给对应的工作包）：

| 位置 | 情况 |
| --- | --- |
| `tools/v111_golden.py:37–38` | 见第 5 节，不许本包改 |
| `Editor/PrototypeBuild.cs:106` | 发布门禁把 `Assets/Game/Data/GoldenScores.json` 的哈希与构建身份里的 `goldenCaseSetSha256` 比。这道门禁整体还钉着 p1.1.0（第 104 行要求规则 p1.1.0、存档结构 7），p1.1.1 出包前要连同 `tools/v11_delivery.py` 一起换版本，届时应改为对 `GoldenScoresV111.json` 取哈希。**在那之前用这个入口出包会在这一行因找不到文件而失败** |
| `tools/v05_delivery.py`…`v11_delivery.py`、`tools/package_v06.py`、`package_v07.py`、`package_v071.py`、`tools/import_v05_rules.py`、`import_v06_rules.py`、`tools/macos/` 下的校验脚本 | 各旧版本的打包、导入与校验脚本，按当时的目录结构写成 |
| `Core/Rules/Validation~/Program.cs:24、99`（`CoreRulesValidation.csproj`） | v0.5 时期的独立校验宿主，读 `GoldenScores.json` 与 `evidence/v05/rules/reference-extra.json`，自 v0.5 起没有再维护。不是交接包说的规则宿主（那是 `RulesNUnitValidation.csproj`） |
| `evidence/`、`cache/`、`docs/` 里的历史记录 | 历史证据与文档，不改 |

## 8. 留给界面工作包的位置

`ScoreBreakdown.colorMult100` 已删除。原来读它的地方改成读 `100 + colorAdd100`（与别名的返回值相同），行尾都带 `W4B-ADAPT` 注释。它们仍把“1 + 颜色加成”当作一个独立乘数显示，而新规则里颜色只是加成合计的一项，所以有其它加成时这些地方的数字与得分对不上：

| 位置 | 现状 |
| --- | --- |
| `UI/GameApp.Art07.cs:211`、`:221` | 筹码“颜色 ×”及其是否为中性的判断 |
| `UI/GameApp.Art07ScoreDrawer.cs:93` | 得分抽屉的“颜色”因子 |
| `UI/GameApp.Debug.cs:162` | 调试面板的“独立乘区”文字 |
| `UI/GameApp.Play11Score.cs:37` | 因子列表里的颜色项 |
| `Runtime/Presentation/HighScore073.cs:63` | 最高分公式串的颜色项 |
| `Tests/PlayMode/Art07EdgeScreensPlayModeTests.cs:49`、`:53` | 测试里用颜色乘数估算预期 |

W3a 备注 2.3 列的其它界面事项不变。

## 9. 运行验证过的与没有验证的

运行验证过（.NET 10 宿主与命令行）：

- 规则宿主 3977 条全部通过，其中 `GOLD111-` 2152 条；旧文件移走之后又跑过，结果相同。
- 会话宿主 516 条里 497 通过，失败的 19 条与本包之前逐名相同。
- 反向检查（第 1 节）。
- `UnityTestsCompilation.csproj` 的编译检查：本包前后都是同样的 5 个错误（`Art07ResourceDiagnosticPlayModeTests` 引用的三个 `…ForTests` 成员，与本包无关），没有新增。界面、运行时、PlayMode 与 `PresentationTests.cs` 的改动由此确认能编译。
- 第 6 节列的各条命令。

没有验证：

- **没有启动 Unity。** 新测试只在 .NET 宿主上跑过，那里的 `JsonUtility` 是用 `System.Text.Json` 写的替身；Unity EditMode 下的运行、`.meta` 的导入、`V111Json` 读 9.4 MB 文件的耗时都未验证。`V111GoldenTests.cs.meta` 与 `GoldenScoresV111.json.meta` 是手写的（随机 GUID，格式照同目录文件）。
- `PresentationTests` 的 160 条 `ART07_RECEIPT_` 只做了编译适配，没有运行；它断言回执分数等于样例分数，界面回执适配器若还按旧公式拼装，这些用例会失败，属界面工作包。
- 没有运行平衡模拟器。

推断（没有另行验证）：

- “旧输入重算的期望值不会因路径改动而变”是根据四个文件哈希未变推出的，没有运行改过路径的生成器。
- 4.1 说基础分各分项“由 `baseTotal` 约束”：分项之间互相抵消的错误理论上比不出来；现有 `V111ScoringTests` 与 `ControllerScoreTests` 对这些分项另有手算断言，本包没有逐一核对它们的覆盖面。
