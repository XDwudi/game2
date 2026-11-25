# W3a 备注：规则层实现 p1.1.1 计分

工作包 W3a（[交接包](briefs/W3A_RULES_CORE.md)）。依据 [规则增量](RULES_DELTA.md) 与 `tools/v111_config.py`。基线 `a17f30fa`，分支 `codex/v1.1.1`。旧测试改写见 [TEST_CHANGES.md](TEST_CHANGES.md)。

本文三部分：规格疑点与处理、编译适配清单（W3b 待实现）、没做完的事。

## 1. 规格疑点与处理

按重要程度排列。每条写明我按哪种读法实现；都没有改规格。

### 1.1 需要协调者或 W3b 决定的

| 编号 | 疑点 | 现在的实现 | 影响 |
| --- | --- | --- | --- |
| N01 | 规格 §12.1 要求“有有效 C156 来源的快照必须明示真实分数和目标”，但会话层现在构造快照时不填 `stageScoreBefore`、`stageTarget`（那是 W3b 的事） | 计分器按规格执行：有有效 C156 来源而两个字段任一为空时抛 `ArgumentException`。无 C156 时缺省 0 和 1 | **在 W3b 填这两个字段之前，持有有效绝地反击的局在预览和提交时会抛错。** W3b 第一步应在会话的快照构造处填上冻结累计分与有效目标 |
| N02 | 黄金对拍测试要求“保持原样”，而规格把 `ScoreBreakdown.colorMult100` 换成了 `colorAdd100`，三个黄金测试文件直接读 `actual.colorMult100`，不改就编不过 | 没有动黄金测试文件。`ScoreBreakdown` 留了一个只读属性别名 `colorMult100 => 100 + colorAdd100`（不是字段，不序列化），只为让它们编译 | 独立方重生成样例并改测试 DTO 时应删掉这个别名。界面里还有几处在读它（见 2.3） |
| N03 | 规格 §12.2 写 `dieIndex` 非黄金为 null；`JsonUtility` 存不了可空整数 | `AdditiveEntry.dieIndex` 是 `int`，非黄金为 −1，黄金为零基下标 | 独立参考实现的样例如果写 null，对拍时要把 null 映射成 −1，或协调者改规格 |
| N04 | 规格 §12.2 的 `amount100`、`additiveTotal100` 只说是整数，没说宽度。C139 无上限，钱包取 64 位上限时加成超过 64 位 | 两个字段是 `long`，内部用大整数累加后做带检查的转换；溢出时抛 `OverflowException`。调试钱包上限 10 亿时最大约 5×10⁹，`long` 足够，`int` 不够 | 正常局不会碰到。若要完全不抛错，需要把这两个字段改成十进制字符串（与 `baseTotal` 同类），这是契约变化，没有擅自做 |
| N05 | 倍率附魔加成条目的 `stateOwnerId` 规格没写。附魔属于发出者自己，但条目是因某次技能触发而产生的 | 条目的 `emitterId`、`stateOwnerId` 照抄触发它的那个来源（模仿者带附魔时，`stateOwnerId` 是被模仿的卡），与 `multiplierFactors`、`retriggerEntries` 的写法一致 | 独立参考实现需要同一口径 |
| N06 | C122 规格写“每颗有物理面数据的朝上面生成 `pipSet=5`”，p1.1.0 对已是 5 的面不生成计划 | 按字面：每颗都生成，包括已经是 5 的面。是否算“成功改写”由会话执行时比较前后决定 | 计划条数与 p1.1.0 不同；W3b 执行时注意“没变化不计数” |
| N07 | C54 换牌型时事件累计已是 0，是否还列 `halve` 计划 | 列。条件只看“有上一主牌型且不同”，不看当前累计 | 回执里会多一条无实际效果的计划 |
| N08 | C43 的钳制“按永久实际等级”。p1.1.0 代码在返璞归真（按 0 级计分）时用的是 0 级 | 按规格改为用快照里的真实等级：真实 12 级时即使按 0 级计分也不升 | 行为与 p1.1.0 代码不同，属规格明示 |
| N09 | C63“每颗冻结时的存活永久骰 +0.5”，0 颗永久骰时算不算触发 | 沿用 p1.1.0：C63 恒触发；加成为 0 时不列条目（规格“零加成不列”），但仍会触发它的附魔 | 只在合成夹具里出现（正式局永久骰不为 0） |
| N10 | C123 的减点下限 3 在执行时生效；计划里只有 `pipDelta=-2` | 计分器只按冻结点数出计划（1 → +1，4–6 → −2）。下限由会话执行时读 `highPipFloor`（生成器加的参数，值 3）| W3b：执行 `sourceId=C123` 的负 `pipDelta` 时用下限 3，不是通用下限 1 |
| N11 | `turnoverUnits2`“恒为配置初值”，但配置里已无 `initialMultiplierUnits2` | 字段保留，默认值 3，规则层不再读它；实例校验仍要求它 ≥ 3（与 p1.1.0 相同） | 无 |

### 1.2 生成器与规格 §12.3 文字不一致（按交接包以生成器为准）

| 编号 | 规格写的 | 生成器写的（已照此实现） |
| --- | --- | --- |
| G01 | 顶层 `growthCatchupPerLateCycle100`，`additive=50,base=600,multiplier=10` | 顶层 `growthCatchupPerCycle`，`additive=50,base=6,multiplier=10`（基础分是整数分，不乘 100，与 §5 的单位一致） |
| G02 | C155 `scoreMultiplier=2,enhancedScoreMultiplier=4` | `scoreMultiplier100=200,enhancedScoreMultiplier100=400` |
| G03 | C154 的 `trigger` 用 `Unlock` | `UnlockDie` |
| G04 | I13 `lifetime=UntilUsesExhausted` | `lifetime=Permanent`。校验没有钉 I13 的 `lifetime`，其余（窗口、价格、次数 3）已钉 |
| G05 | “C09、C138：`scoreMultiplier` → `scoreMultiplier100`” | C02、C09、C31、C33、C42、C101、C130、C138 全部改为 `scoreMultiplier100`；C103 改为 `scoreMultiplier100=200`、`strictScoreMultiplier100=300`。配置里已无任何 `scoreMultiplier` 键 |
| G06 | C123 只列 `lowPipDelta=1,highPipDelta=-2` | 另有 `highPipFloor=3` |

### 1.3 规格例子与数值、文字

- 规格 §2 的例子“牌型倍率 2.25 被减半得 ×1.12、基础分 200 得 224”：现行牌型表凑不出 ×2.25（一对每级 +0.75，其余每级增量也都得不到 2.25）。测试用同一条规则的可达值：1 级一对 ×2.75 减半为 ×1.37，基础分 200 得 274。
- C152 的 `shortText` 是“每有1张常见卡牌，加成+0.6”，`description` 与规格写“普通稀有度”。两处用词不一，没有改文字。
- `Runtime/GameApp.cs:161` 的提示文字仍写“附魔触发：×2”，现在倍率附魔是加成 +1.5。属界面文案，没有改。
- 规格 §12.3 说“普通／罕见／稀有数量为 29／88／33”，生成配置与之一致，已写进校验。

### 1.4 已按规格实现、值得独立方留意的口径

- 得分分子分母（`scoreNumerator`、`scoreDenominator`）现在是最简分数（§12.2），p1.1.0 是未约分的。
- C49 有 5 时只进加成、无 5 时每个触发来源在 `multiplierFactors` 里一条 `1/2`；两支都计入 `triggeredSourceIds`，都能触发附魔。
- C47 数的是存活实例数（模仿卡本身是一张卡，算 1；它复制来的技能不另算）。
- 黄金面加成读 `SubmissionDie.isGold`，封印骰与 `PipsOnly` 下仍列；玻璃风险名单读 `SubmissionDie.isGlass`，封印骰也列。
- 养成免栏位看的是实例自己的定义：百变模仿（C41）不是养成卡，复制养成卡不占用也不享受这个名额。失效的养成卡照样免栏位。
- C137 的明星点数读技能所有者的 `starPip`；为 0（尚未入关）时不触发。`starPip` 只允许出现在 C137 上，`growthCatchup` 只允许出现在成长卡上，`cultivationFacesChanged` 只允许出现在养成卡上且不超过 24，否则实例校验拒绝。
- 成长计划 `growthChanges[].delta` 的单位随卡：C54 为 10、C55 为 3、C56 为 40、C57 为 80。`reset` 不再产生。

## 2. 编译适配清单

规则层删了字段、改了签名，下列调用点只做了让它们继续编译的机械改动，全部带 `W3A-ADAPT` 注释或是纯改名。**没有实现任何新行为。**

### 2.1 会话层（`Core/Session`）

| 位置 | 原来 | 现在 | W3b 要做 |
| --- | --- | --- | --- |
| `GameSession.cs:219` | `GlassBreakPercent` 在有 C145 来源时读 `C145.parameters.glassBreakPercent` | 恒返回 `cfg.glassBreakPercent`（C145 已移除） | 玻璃改为提交后按 `afterScorePlan.glassRiskDieIds` 判定（§7.3），不再在投掷时判 |
| `GameSession.cs:365` | 祭坛成长 `买价 × C100.growthPerBuyPrice` | `买价 × C100.growthEvent`（同义改名，现为每金 0.15 的百分单位） | 复核祭坛按“配置基础价”计（§5）；追赶值的发放 |
| `GameSession.cs:477` | `Grow(C62, growthPerEvent)` | `Grow(C62, growthEvent)` | 无（单位已是整数分） |
| `GameSession.cs:485` | 玻璃碎裂后 `Grow(C90, …)` | 删去这一句（C90 已移除，否则碎裂时会因查不到 C90 抛错） | 同 219 行 |
| `GameSession.Shop.cs:427`、`GameSession.Submission.cs:297` | `OrdinaryUsed(列表)` | `OrdinaryUsed(列表, cfg)` | 养成免栏位已随规则层生效；W3b 复核买入、领取、C112 赠卡处的栏位提示 |
| `GameSession.Shop.cs:474、574` | `C61.growthPerEvent` | `C61.growthEvent` | 无 |
| `GameSession.Shop.cs:519` | `C60.growthPerEvent` | `C60.growthEvent` | 自选、双面、赠礼等“一笔只记一次”（§5、§7） |
| `GameSession.Shop.cs:586` | 卖卡时 `turnoverUnits2 += C51.addUnits2PerRealControllerSale`，回执 `newValue` 为 `turnoverUnits2` | `growth += C51.growthEvent`，回执 `newValue` 为 `growth` | **这一处不只是改名**：参数没了，按规格“人员流动改用 `growth`”换了写入的字段。事件条件（每店额度等）没动。W3b 复核 §5 C51 的全部条件与回执口径 |
| `GameSession.Submission.cs:110、228、229、232、233、459` | `C61/C58/C59/C129/C132/C134.growthPerEvent` | 同名卡的 `growthEvent` | C125 的关末成长、C134 按钥匙串次数、C132 的条件等新行为 |

会话层里**编译不报错但已经与规则层脱节**的地方，W3b 必须处理：

- 快照构造不填 `stageScoreBefore`、`stageTarget`（见 N01）。
- `growthChanges` 的 `halve` 没有执行者：连庄换牌型现在什么都不发生（旧的 `reset` 分支不再被触发）。
- `afterScorePlan.glassRiskDieIds`、`cultivationSourceBindings`、`FaceChange.stateOwnerId` 没有读者；养成计数、`FaceState.cultivated`、镀金加权重、C123 下限 3 都未执行。
- `growthCatchup`、`starPip` 没有写入者（货架与战利品的追赶、入关抽明星点数）。
- `GameSession.Validation.cs:129` 的成长值校验用 `HasGrowthCounter`，名单已随规则层更新（加 C51、C125，去 C90）；新增的三个状态字段只有规则层的范围校验。
- 新字段会进入存档 JSON 与状态哈希；存档结构号、旧档门禁、调试往返都还没动。
- 商店附魔曲线、乘法卡去重过滤、自选改造、解锁命令、道具次数全部未做。`ControllerRules.IsUniqueController`、`OwnsUniqueController`、`GrowthValue`、`FreeCultivationInstanceId` 已备好供调用；按周期抽附魔的函数没有写（交接包列为范围外）。

### 2.2 会话与界面的测试（只为编译）

| 位置 | 原来 | 现在 | 备注 |
| --- | --- | --- | --- |
| `Tests/EditMode/Session/V06SessionTests.cs:318` | `breakdown.enchantMultiplierCount` | `breakdown.enchantAdditiveCount` | 纯改名 |
| `Tests/EditMode/Session/V10SessionTests.cs:724、736` | `C55/C129.growthPerEvent` | `growthEvent` | 纯改名 |
| `Tests/EditMode/Session/V10SessionTests.cs:744、950` | `C145.parameters.glassBreakPercent` | `cfg.glassBreakPercent` | 这两条用例测的是已移除的 C145，运行时仍会因 C145 不存在而失败；W3b 改写或登记退役 |
| `Tests/EditMode/Presentation/HighScore073Tests.cs:58–62` | 初始化 `colorMult100 = 100／500`、`enchantMultiplierCount = 0／2` | `colorAdd100 = 0／400`、`enchantAdditiveCount = 0／2` | 别名只读，改为设置等价的加成值 |
| `Tests/PlayMode/Art07EdgeScreensPlayModeTests.cs:49` | `preview.enchantMultiplierCount` | `preview.enchantAdditiveCount` | 纯改名 |
| `Tests/PlayMode/Presentation07/Art07FixturePlayModeTests.cs:36`、`Tests/PlayMode/V15LootCoveragePlayModeTests.cs:127` | `OrdinaryUsed(列表)` | `OrdinaryUsed(列表, app.Config)` | 签名同步 |

这些测试没有运行过（本包只要求编译）。会话与界面测试里钉住的旧数值预计大量失败，属 W3b 与界面工作包。

### 2.3 界面与运行时（`UI/`、`Runtime/`）

| 位置 | 原来 | 现在 | 之后要做 |
| --- | --- | --- | --- |
| `UI/GameApp.Art07.cs:211、221` | 附魔筹码读 `enchantMultiplierCount` | 读 `enchantAdditiveCount` | 颜色与附魔两个筹码合并成一个“加成”筹码 |
| `UI/GameApp.Art07.cs:240` | `EnchantFactor07 = 附魔倍率 ^ 次数` | 恒为 1（配置已无 `scoreMultiplier`） | 同上 |
| `UI/GameApp.Art07Results.cs:46` | 总倍率 = 牌型 × `colorMult100` × 红组 × 控制器 × 附魔倍率^次数 | 牌型 ×（100 + `additiveTotal100`）× 红组 × 控制器 | 这一处换了因子而不只是改名：只有这样总倍率才仍等于得分／基础分。同行分母未动 |
| `UI/GameApp.Art07ScoreDrawer.cs:96、97`、`UI/GameApp.Play11Score.cs:43`、`UI/GameApp.Debug.cs:162` | 读 `enchantMultiplierCount` | 读 `enchantAdditiveCount` | 得分明细改为一行“加成”可展开（§13） |
| `UI/GameApp.Controllers.cs:30` | C141 显示 `×(initialMultiplierUnits2 − growth)/2` | 显示 `+(initialAdditive100 − growth × decayPerClear100)/100` | 卡面“加成 +X”的统一写法 |
| `UI/GameApp.Controllers.cs:31` | C63 显示 `×max(10, 永久骰数 × perDieUnits10)/10` | 显示 `+永久骰数 × additivePerPermanentDie100/100` | 同上 |
| `UI/GameApp.Controllers.cs:129`、`UI/GameApp.Play11.cs:258`、`UI/GameApp.Shop14.cs:304、540–542`、`UI/GameApp.V05.cs:170`、`Runtime/Verification/PlayerVerification.cs:856`、`Runtime/Verification/PlayerVerification.V06.cs:348`、`Runtime/Verification/Validation~/MetricChecks.cs:119` | `OrdinaryUsed(列表)` | `OrdinaryUsed(列表, Config)` | 首张免位的标识与提示（§13） |
| `UI/GameApp.Loot15.cs:239–240`、`UI/GameApp.Shop.cs:195`、`UI/GameApp.Shop14.cs:383–384` | 颜色升级读 `Color.baseMult100`、`incrementPerLevel100` | 读 `100 + Color.baseAdd100`、`Color.addPerLevel100`，仍按“倍率 ×”显示 | 改成“加成 +X → +Y” |
| `Runtime/GameApp.cs:161` | 读 `enchantMultiplierCount` | 读 `enchantAdditiveCount` | 提示文字仍是“×2” |
| `Runtime/GameApp.cs:241` | 颜色说明读 `incrementPerLevel100` | 读 `addPerLevel100` | 文字仍写“倍率 +” |
| `Runtime/Presentation/HighScore073.cs:66` | 读 `enchantMultiplierCount` | 读 `enchantAdditiveCount` | 公式串合并颜色与附魔 |
| `Runtime/Presentation/PresentationReceiptAdapter.cs:66` | 总倍率含 `colorMult100` 与附魔倍率^次数 | 改用（100 + `additiveTotal100`），去掉附魔项 | 与 `Art07Results.cs:46` 同理 |

界面里**编译不报错但显示已经不对**的地方（没有动）：

- 读 `colorMult100` 别名的：`UI/GameApp.Art07.cs:211、221`、`UI/GameApp.Art07ScoreDrawer.cs:93`、`UI/GameApp.Debug.cs:162`、`UI/GameApp.Play11Score.cs:37`、`Runtime/Presentation/HighScore073.cs:63`、`Tests/PlayMode/Art07EdgeScreensPlayModeTests.cs:49`。别名只含颜色加成，不含卡牌、黄金、附魔的加成，所以有其它加成时这些筹码的乘积不等于得分。
- 成长值显示仍按 p1.1.0 的“(20 + growth)/20”：`UI/GameApp.Controllers.cs:28–29`、`UI/GameApp.Modals.cs:394`；`UI/GameApp.Art07Shop.cs:104–105` 的“从 ×1／+0 成长”；`UI/GameApp.Debug.cs:283` 的字段说明。应改用 `ControllerRules.GrowthValue`，并区分加成、乘法、基础分三类（`IsGrowthAdditive` 是新增的）。
- 图鉴颜色行：`HandbookRow` 的 `baseMult100`、`incrementPerLevel100`、`currentMult100` 对颜色行现在是 0，新值在 `baseAdd100`、`addPerLevel100`、`currentAdd100`。`UI/GameApp.V05.cs:91、122` 仍读旧字段，会显示 ×0。

## 3. 没做完的事

- 黄金样例与参考实现按交接包没有读也没有改。与之对拍的 1134 条测试失败（明细见交付报告）。`GOLD-`、`TRACE-` 两组共 320 条里，308 条在数值断言处失败，12 条前面的字段都对上了，却在比较 `colorMult100` 时抛空引用：这两组用反射按**字段**名取值，而 `colorMult100` 现在是属性、`enchantMultiplierCount` 已不存在。也就是说这两组在测试的字段名单更新之前，数值全对也过不了。
- 会话宿主只做了编译，没有运行它的测试。`UnityTestsCompilation.csproj` 在基线上就有 5 个与本包无关的错误（`Art07ResourceDiagnostic` 的三个 `…ForTests` 成员在这个编译环境里不存在），本包前后错误条数相同，没有新增。
- 没有启动 Unity，新测试只在 .NET 宿主上跑过；Unity EditMode 下的运行、新测试文件的导入都未验证。`V111ScoringTests.cs.meta` 是手写的（随机 GUID，格式照同目录其它 .meta）。
- 规格 §2–§7 里属于会话层的例子（追赶的发放、连庄减半的执行、明星点数抽取、玻璃票 14／15、镀金加权重、无心插柳、养成计数的执行、最高单手）没有写成测试，那是 W3b 的范围。
- 规格 §12.3 列的“运行时实例编号范围、调试候选和存档版本门禁同步变更”里，实例编号范围已改（`ControllerRules`）；调试候选与存档版本门禁在会话层，没有动。
