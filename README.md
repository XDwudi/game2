# 人工主导的 AI 游戏开发框架

这套框架用于让人掌握游戏创意与体验决策，让 AI 在明确的任务、专业工具和验收要求下制作。当前在 v0.71 美术规格之上迭代到 v0.73 的骰子构筑原型，玩法保持 p0.6.0/schema4，目标 Windows x64 与 macOS Universal / Unity 6000.6.7f1。长期目标仍包含 PC、iOS、Android，移动端尚未验证。

核心约定：按阶段验证风险，先做可玩的原型，再做达到目标品质的代表性片段，最后扩大生产。AI 交付必须有可检查的成果与证据；质量不由工作时长、token 数或 agent 数量决定。

当前采用质量优先的小批次流程：一次可以只交付很少内容，先把规格、系统交界、专业工具和样例验证清楚，减少可避免返工。参考 CCGS、BMad Game Dev Studio 与 Superpowers 的源码研究及具体取舍已保存；未照搬它们的全部默认规则。

## 当前原型

- **v0.8 平衡性更新方案（提案，待确认）**：对现行数值的卡牌强度、难度曲线、金币曲线的模拟分析与调整方案见 [BALANCE_PLAN](docs/specs/v08/BALANCE_PLAN.md)，工具见 [balance_sim](tools/balance_sim/README.md)。尚未改动游戏工程。
- **v0.73 商店、骰子样式、高分动画与查看骰子（rc.1）**：按 2026-11-12 的四张参考图，商店改为单页分区；骰子区分光滑 / 粗糙 / 黄金 / 玻璃等材质，锁定骰挂锁坐托盘；高分提交弹出三档分数牌；关卡内新增“查看骰子”，所有展示骰面的地方改为可拖动的立体骰子；界面统一为阶梯切角。玩法仍为 p0.6.0/schema4。Windows 与 macOS 试玩包见 [GitHub p0.7.3-rc.1](https://github.com/XDwudi/game2/releases/tag/p0.7.3-rc.1)，源码分支 [codex/v0.73](https://github.com/XDwudi/game2/tree/codex/v0.73)。EditMode 1035/1035，PlayMode 在 1080p 与 720p 下均 192/192；macOS 实机、性能专项、独立复核未做，风格待用户判定。详见 [STATE](docs/STATE.md)、[策划方案](docs/specs/v073/VERSION_PLAN.md)、[美术方案](docs/specs/v073/ART_PLAN.md) 与 [任务](docs/tasks/P0-V073.md)。
- **v0.72 高清简约美术试玩版（rc.2）**：界面件、桌面、字标与卡背重做为边缘干净的高清矢量质感，卡牌有呼吸与倾斜等常驻动画；rc.2 按试玩反馈重做了分数栏与计分板、收窄桌沿、统一圆角并加入读数反馈动画；玩法仍为 p0.6.0/schema4。Windows 与 macOS 试玩包见 [GitHub p0.7.2-rc.2](https://github.com/XDwudi/game2/releases/tag/p0.7.2-rc.2)，源码分支 [codex/v0.72](https://github.com/XDwudi/game2/tree/codex/v0.72)。EditMode 1018/1018，PlayMode 在 1080p 与 720p 下均 185/185；macOS 实机、性能专项、独立复核未做，风格待用户判定。详见 [STATE](docs/STATE.md)、[方案](docs/specs/v072/ART_SPEC.md) 与 [任务](docs/tasks/P0-V072-ART.md)。
- **v0.71 主界面与桌面背景更新**：按 Image2 主界面图重排纸质菜单、卡牌与真实骰子，并接入木沿/深青毛毡桌面。源码交付分支为 [codex/v0.71](https://github.com/XDwudi/game2/tree/codex/v0.71)，生成原图与提示词见 [MenuTable](ArtSource/Art071/MenuTable/record.json)，受影响验证见 [更新任务](docs/tasks/P0-V071-MENU-TABLE.md)。下方旧发行附件仍为41aface构建，不含这次界面更新。
- **v0.71 全量美术测试版已通过 [GitHub发行附件](https://github.com/XDwudi/game2/releases/tag/p0.7.1-rc.1)提供下载。** 冻结源41aface，71项内容插画与公共资源已统一；Edit1018/1018、Play182/182通过，Windows实跑完成，三个附件已完整远端下载校验，下载版新目录启动诊断通过。Windows性能/独占全屏仍有失败，Mac与人工待验；首批249d980三包仅为历史。参见 [STATE](docs/STATE.md)、[素材路径](docs/specs/v071/ASSET_MAP.md) 与 [交付说明](docs/DELIVERY_V071.md)。
- **历史 v0.7 诊断测试候选已发布，整版仍未验收通过。** [GitHub p0.7.0-rc.1](https://github.com/XDwudi/game2/releases/tag/p0.7.0-rc.1)提供Windows、Mac Universal与M5工具包；三附件已完整下载核验，Windows已做新目录隔离启动。1013项Edit、158项Play通过；Windows性能/原生显示、AudioMixer、Mac与人工待验。详见[交付说明](docs/DELIVERY_V07.md)和[STATE](docs/STATE.md)。输入见[美术开发规格](docs/specs/v07/ART_SPEC.md)，以下为历史版本。
- v0.6 诊断测试版已在 [GitHub p0.6.0-rc.1](https://github.com/XDwudi/game2/releases/tag/p0.6.0-rc.1) 提供Windows、Mac和M5验收工具包三个附件；已全量远程下载和新目录启动/存档恢复核验。797项EditMode、30项PlayMode及玩法/UI回放通过；Windows独占全屏、性能未通过，Mac/人工待验，整版未ACCEPTED。详见 [v0.6交付说明](docs/DELIVERY_V06.md)。
- v0.5 请看 [交付说明](docs/DELIVERY_V05.md) 与 [STATE](docs/STATE.md)；Windows、macOS与M5验收工具包已共同上传至 [GitHub预发布附件](https://github.com/XDwudi/game2/releases/tag/p0.5.0-rc.1)，三个完整下载均已核对SHA与ZIP完整性。用户已授权此渠道替代LFS。Windows 技术检查通过，Mac 实机待验。旧版 [交付说明](docs/DELIVERY.md) 和 `Builds/Windows/DiceCraft.exe` 仅为 v0.4 历史产物。
- [运行、开发与复现说明](DicePrototype/README.md)
- [权威 P0 v0.6 规格](docs/specs/P0_v0.6.md)与[实施验收契约](docs/tasks/P0-V06-IMPLEMENTATION.md)；当前表现层按 [v0.71 契约](docs/tasks/P0-V071-IMPLEMENTATION.md) 开发，本次全量范围见 [全量美术任务](docs/tasks/P0-V071-FULL-ART.md)，实际进度以 STATE 最新记录为准。
- [当前状态和剩余工作](docs/STATE.md)
- 本版最终测试与构建证据使用 `evidence/v071/`，全量美术与发行复核使用 `evidence/v071-full/`；开发图片/录制位于 `cache/v071/`、`cache/v071-full/`；历史 evidence/unity、evidence/runs、evidence/review 及分版本目录继续保留。失败记录保留，不能把历史失败或旧版本通过当作当前结论。

## 后续项目与阶段的通用流程

当前P0以已归档的v0.6规格和实施契约为准，不需要重复填写已确认输入。下面是新项目或新增阶段的框架入口。

1. 人工填写 [项目准备表](docs/PROJECT_BRIEF.md) 的首轮必填部分。未知内容保留 TBD。
2. AI 按 [前置质量流程](docs/QUALITY_FIRST_WORKFLOW.md) 整理当前系统规格、美术参照和交界场景；人工确认关键体验与方向。
3. 按 [工具与多端方案](docs/TOOLCHAIN.md) 选择路线，先验证最高风险假设和小规模工具接通，形成实际样例。
4. 用 [任务模板](templates/TASK.md) 定义交付与验收，完成 [开工准备度检查](templates/PREFLIGHT.md) 后制作。探索任务先冻结试验问题，无需提前知道结果。
5. 每轮按 [质量验收规则](docs/QUALITY_GATES.md) 验证、返工和记录，更新 [当前状态](docs/STATE.md)。

## 文件导航

美术制作入口：[AI 美术生产流程](docs/art/AI_ART_PIPELINE.md)与项目技能 [game-art-production](.agents/skills/game-art-production/SKILL.md)。包含 Image2 内置调用、参考图与来源归档、只读素材检查、当前项目审计和候选工具研究；不代替人工风格验收。

| 文件 | 用途 | 主要维护者 |
| --- | --- | --- |
| [AGENTS.md](AGENTS.md) | AI 每次工作需要遵循的项目规则 | AI 维护，受用户意图约束 |
| [开发框架](docs/FRAMEWORK.md) | 阶段、分工、美术流程、多 agent 和返工机制 | 主协调者 |
| [前置质量流程](docs/QUALITY_FIRST_WORKFLOW.md) | 在正式制作前解决歧义、风险与质量参照 | 主协调者与人工 |
| [Agent 执行与交接](docs/AGENT_WORKFLOW.md) | 派工输入输出、依赖、双维度复核与变更追溯 | 主协调者 |
| [项目准备表](docs/PROJECT_BRIEF.md) | 人工填写愿景、体验、范围、平台与品质参照 | 人工主导 |
| [质量验收规则](docs/QUALITY_GATES.md) | 什么叫合格、如何举证、谁能验收 | QA 与人工 |
| [工具与多端方案](docs/TOOLCHAIN.md) | 工具候选、接通试验、跨平台边界 | 技术负责人 |
| [当前状态](docs/STATE.md) | 已知事实、未决定项、下一步与工作锁 | 主协调者 |
| [任务模板](templates/TASK.md) | 一项 AI 工作的执行契约 | 主协调者 |
| [资产模板](templates/ASSET_SPEC.md) | 美术资源规格与交付清单 | 美术负责人 |
| [验收模板](templates/REVIEW.md) | 证据、缺陷、独立复核与结论 | 验收者 |
| [试玩模板](templates/PLAYTEST.md) | 真人游玩问题、观察与设计反馈 | 人工主导 |
| [决策模板](templates/DECISION.md) | 策划确认、重要技术选择和变更记录 | 决策负责人 |

按需要使用的补充模板：[系统规格](templates/SYSTEM_SPEC.md)、[美术方向](templates/ART_DIRECTION.md)、[技术试验](templates/TECHNICAL_SPIKE.md)、[黄金样例](templates/GOLDEN_SAMPLE.md)、[追溯表](templates/TRACEABILITY.md)、[交接包](templates/HANDOFF.md)。这些模板不要求一次全部填写。

研究与实际检查：[开源源码研究](docs/research/OPEN_SOURCE_REVIEW.md)、[固定来源及许可](references/README.md)、[本地检查工具](tools/QUALITY_CHECK.md)、[框架验证记录](docs/FRAMEWORK_VALIDATION.md)。

## 当前可用程度

目前可以使用项目规则、任务模板，原有三个本地检查命令（来源副本、输入与产物快照、NUnit 报告），以及新增的 [PNG 美术完整性检查器](tools/ART_ASSET_AUDIT.md)。检查器有实际运行的负例测试；检查通过只代表覆盖范围内的结构与一致性满足，不代表游戏品质通过。

已下载三个参考仓库，选择性保留 35 份固定提交原文件及许可证。未启用上游插件或 hooks，游戏工程在 DicePrototype/，本项目已建立 Git 并接入用户指定 origin。真实运行证据和未验项以 docs/STATE.md 为准；尚未配置 CI 或执行三端验收，真人试玩需人工完成。

后续代码、资产和测试证据应放进版本控制。AGENTS.md 提供持久项目指导，但不能单独强制工具行为或替代独立验收。[官方说明](https://learn.chatgpt.com/docs/agent-configuration/agents-md)

## 可以直接使用的开工指令

> 阅读 AGENTS.md、docs/STATE.md、已确认的项目准备表及当前任务。先核对任务输入、依赖、验收条件和工具实际可用性。在已授权范围内自主实施，给独立工作分配子 agent，避免共享文件并发写入。依据验收条件提供真实构建、测试、视觉或试玩证据。失败时修复并重验；不能通过降低标准、删除需求或隐藏占位内容交付。涉及体验或风格取舍时，带可检查的方案交给我决定。最后报告通过项、失败项、未验证项和下一步，并更新项目状态。
