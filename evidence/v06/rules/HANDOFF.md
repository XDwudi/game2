# v0.6 规则模块交接

输入为 docs/specs/P0_v0.6.md（原始附录A/B直接导入；不得由待测代码改写黄金预期）。父任务为 P0-003，BASE 2db287a，旧游戏源967f309。本文是实施自检交接，不签署最终验收。

## 实现

- 原样配置 p0.6.0/schema4，50控制器、7道具、7服务、7骰强，13牌型和2独立颜色。
- BigInteger最终分数与整手贡献字符串、精确有理数倍率和公式步骤；唯一最终floor。
- 按实例来源的C41链与冻结失效绑定，emitter/stateOwner分离；纯AfterScorePlan与不抽随机的Preview范围。
- 6种附魔（含none）两类专用池、替换后条件化；净概率来源转移和零概率。
- Handbook 14手册牌型行含散点与2独立颜色；高阶资格和复制来源。
- acquiredSerial是既有持久键，语义等同规格createdSerial，另有非序列化createdSerial属性映射。
- GameConfig.PrepareJsonForUnity先严格校验原始JSON，然后只转换C11 requiredCounts的数字键为C#合法字段；原始P0Config不改。全部加载入口须使用此方法再JsonUtility.FromJson。

## 真实自检

- .NET 10.0.14 / SDK10.0.403，RulesHarness：配置+160黄金，21顶层字段和14个后置字段全比较；输入不变、全部公式步骤精确、预设分支预览一致，161组通过。
- NUnit托管适配器：498/498通过，包含160黄金和160公式、原有规则断言按v0.6语义迁移、新边界18项；概率729纹理×4道具=2916组合；5600新附魔票、288替换池的12800票逐票核对。
- managed-golden-report.json、nunit-managed-report.json均绑定源文件SHA和实际运行环境。独立review agent另以自己的算法和harness验证；见其报告，不代签。
- 未执行Unity Editor/Player，不将托管适配器当作Unity EditMode。root负责引擎集成与双平台构建。

## 迁移和失败记录

所有旧RuleTests场景保留并更新失效语义，新增三对子/四对子/双三条、独立颜色、五附魔、多来源、资金与纯预览覆盖。ControllerScoreTests固定分数由随附独立Python参考重算，逐例新旧差异见 migrated-controller-expectations.json；没有使用C#生成预期。

第一轮配置校验错误保留 managed-golden-01-config-failure.json：沿用了v0.5的利息时点字符串，按原始v0.6配置修正。初次NUnit编译遇旧NUnit没有Is.AnyOf，改用等价Contains断言，未删除用例。初次dotnet运行因默认用户目录被沙箱限制，改为工作区DOTNET_CLI_HOME，无需额外权限。

## 运行

PowerShell设置DOTNET_CLI_HOME为工作区 evidence/v06/rules/dotnet-home，DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1与DOTNET_NOLOGO=1。

- dotnet run --project evidence/v06/rules/harness/RulesHarness.csproj
- dotnet run --project evidence/v06/rules/harness/RulesNUnit.csproj

新导入可运行 tools/import_v06_rules.py 后 tools/generate_v06_dtos.py。migrate_v06_*_tests.py为本次一次性迁移记录，禁止对已迁移文件重复执行。
