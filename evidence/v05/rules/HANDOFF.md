# P0 v0.5 纯规则交接

状态：提交 REVIEW，等待独立审查与真实 Unity EditMode。实施者 `/root/v05_core_rules` 不签署自身最终验收。任务基线 `36025ff17235993fa00f92241b0fc59ad6a9dd35`，输入 `docs/specs/P0_v0.5.md`。

## 变更与接口

- 附录 A/B 原样提取为 `Assets/Game/Data/P0Config.json` 与 `GoldenScores.json`；后者是包含100例的数组。`import.json` 绑定源文档和两个输出。没有修改原始预期。
- 配置 DTO/严格 JSON 字段校验完整覆盖58内容、40控制器、12可升级牌型、24关、附魔、独立容量、Lv8、上限50计息本金、双平台字段；旧配置版本不再接受。
- `ControllerInstance` 保存 `instanceId/definitionId/enchantmentId/acquiredSerial`。`contentId` 属性只映射 `definitionId` 供旧通用 UI 源代码使用，不产生第二个 Unity 保存字段。
- `SubmissionSnapshot.equippedControllers` 为运行实例数组；`equippedControllerIds` 仅保留旧纯测试输入兼容。Session 层负责正常装备/来源约束，纯计分允许最多10张的构造测试。
- `ScoreBreakdown` 对齐附录B全部26字段：删除 `finalFlatAdd`；新增 `pairBaseAdd/enchantBaseAdd/baseTotal/redGroupingMode/triggeredControllerIds/enchantBaseCount/enchantMultiplierCount/scoreNumerator/scoreDenominator`。
- C20逐骰贡献加6，C39有效蓝逐骰加2，随后玻璃/金层；C27与附魔+5在整手基础区；所有提交类触发条件独立于最终分数是否变大；后置变更仅返回触发集合，由 Session 执行。
- C24/C25只影响红组，八豹子、强化后最高同点牌型、C29四顺及新增倍数均走同一纯计分器。所有分区末尾以 BigInteger 完成一次取整。
- `ProbabilityCalculator.BuildUnits(faces,largeBuff,smallBuff,cfg,highController=false,lowController=false)` 使用精确有理数转移，按数字种类平分、重复面比例分摊，保留0概率；最后只量化一次。`Sample` 跳过0区间。
- `ControllerRules` 共享容量、预算、普通槽/总件数、附魔合法性、替换池与抽样。`DrawEnchantment(id,ownedEnchant,rng,cfg)` 新ID传null；其余传当前附魔。新ID先抽原80/12/6/2再回退，替换只抽合法剩余池；`EnchantPrice` 返回溢价。
- `HandbookRules.Build(cfg,levels,controllers,capacity,permanentDiceCount,activeDiceCount)` 返回固定13行纯展示数据，C29最低4与六七八永久骰购买门槛分别判断；`HandEvaluator.Multiplier100` 同时供计分器与手册使用。

## 实际执行

1. `python tools/import_v05_rules.py`：两个JSON原样提取，结构可解析。
2. `python tools/v05_reference_vectors.py`：执行已经逐行读过且 SHA 锁定的附录F参考计算核心，生成1024额外计分与1024概率输入。固定测试数据种子50317，与游戏随机流无关；高权重、缺面、重复面、纹理、道具及双控制器组合均包括。
3. `dotnet run --project DicePrototype/Assets/Game/Core/Rules/Validation~/CoreRulesValidation.csproj --no-restore`：**1134验证组通过，0失败**。100权威+1024额外计分各26字段与逐步解释一致；1024概率精确匹配Python Fraction；原5向量、2916纹理/道具组合、RNG前5值及字段缺失拒绝通过。
4. `dotnet run --project DicePrototype/Assets/Game/Core/Rules/Validation~/RulesNUnitValidation.csproj --no-restore`：**356个 NUnit 断言用例通过，0失败**。此运行器使用 .NET 10 反射和明确标识的 System.Text.Json 适配器，不代表 Unity JsonUtility/编辑器已测试。
5. `git diff --check -- DicePrototype/Assets/Game/Core/Rules DicePrototype/Assets/Game/Tests/EditMode/Rules`：通过。源文件统一LF避免无关CRLF噪音。

命令需将 `DOTNET_CLI_HOME` 指向工作区内 `Rules/Validation~/.dotnet`；不写用户全局 .dotnet 目录。`.csproj` 保持无外部 NuGet 依赖，NUnit引用已安装Unity包。

## 回归迁移与失败保留

旧52计分/52解释升级为新100计分/100解释；附录C旧B01–B20只是同数据的重复别名，不继续套用已废弃编号。旧显式控制器正反用例全部保留并按v0.5修改受影响预期，包括C02×3、C06三种、红组1.5与C20基础层。旧“最终+F”测试改为明确断言C20在倍率之前，且整条链只有末尾一次floor。

第一次 managed 测试355通过/1失败是独立运行器没有 NUnit TestContext 输出上下文；已仅将非Unity日志输出改Console，真实Unity仍使用TestContext。保留 `nunit-managed-01-harness-failure.json`。额外参考生成曾因脚本默认编码误读中文正则而失败，改为唯一Python围栏ASCII定位并保持源码SHA保护；保留 `dotnet-02-fixture-generation-failure.json`。这两项均非规则断言失败，未删除用例或修改预期来放行。

## 仍需集成验收

- Unity EditMode 实际执行、JsonUtility 与全工程编译。
- Session 对 qualified/24关/商品生成/后置修改/保存失败事务的真实集成。
- Windows/macOS Player运行、UI可读性与性能；本报告不代替这些条件。
- 新脚本/测试的 `.meta` 由唯一 Unity 写入者导入生成。

生产写入锁已交还协调者。规则如被修改，须重跑受影响检查并更新 `source-manifest.json`。
