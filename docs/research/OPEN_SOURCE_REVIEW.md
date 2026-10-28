# AI 游戏开发方法源码研究与取舍

研究日期为 2026-10-27。目标是减少需求误解、管线失配和后期返工，保持人工创意权，并支持未来 PC、iOS、Android。选择方法时以实际源码和适用条件为依据，不把角色数量、宣传描述或 GitHub 热度当作品质证明。

## 研究范围与证据层级

已下载并阅读 Claude Code Game Studios、BMad Game Dev Studio、Superpowers 的固定版本，原始文件与许可在 [参考资料目录](../../references/README.md)。本次检查了工作流、任务模板、审查逻辑、钩子和部分测试辅助代码；未执行这些仓库的安装器或上游工作流，也未用它们实际制作完整游戏。

以下区分三种结论：源码明确写出的规则；由静态逻辑得出的风险；本项目提出的适配方案。没有可据此承诺的返工减少百分比，效果要在本项目的代表性样板中验证。

| 来源 | 固定提交 | 主要研究用途 |
| --- | --- | --- |
| Claude Code Game Studios | `05ad7c8a5b8aa2aa86bbd3439ae7cf7295cac418` | 游戏工作室分工、设计到实现的追溯、开工准备与资产规格 |
| BMad Game Dev Studio | `ff8870955203e1665f4bf75a64049078e6d4080c` | 需求覆盖、任务依赖、故事上下文和评审分支 |
| Superpowers | `2ce8aa15d05bd57213e6f746f953aafb7ded7a8b` | 实施与评审分离、固定版本审查包、根因分析和实际验证 |

## Claude Code Game Studios 的真实结构

固定快照中直接计数为 49 个 agent 定义、74 个 skill、62 个顶层文档模板、18 个 shell hook 文件和 14 个路径规则。hook 文件包含辅助脚本，不能把 18 当作 18 个独立质量检查。

其主要结构是：

```text
CLAUDE.md 与 project.yaml           总入口与配置
.claude/agents/                    领导、部门负责人、专业执行角色
.claude/skills/                    按任务调用的操作流程
.claude/docs/workflow-catalog.yaml  七阶段路线与精简分支
.claude/docs/templates/            策划、架构、任务、证据和生产模板
.claude/rules/ 与 hooks/           路径规则和 Claude Code 事件脚本
design/ 与 docs/architecture/      策划、共享事实、技术需求与架构
production/                       任务、生产记录与会话状态
studio-tests/                     框架自身的审查规范
```

主链条为 Concept、Systems Design、Technical Setup、Pre-Production、Production、Polish、Release，另有 minimal 路线。它通过垂直委派、同级咨询和跨域争议上报组织角色。[工作流原文](https://github.com/Donchitos/Claude-Code-Game-Studios/blob/05ad7c8a5b8aa2aa86bbd3439ae7cf7295cac418/.claude/docs/workflow-catalog.yaml)、[协调规则](https://github.com/Donchitos/Claude-Code-Game-Studios/blob/05ad7c8a5b8aa2aa86bbd3439ae7cf7295cac418/.claude/docs/coordination-rules.md)

最有价值的是一条可追溯的制作链：系统策划 → 稳定技术需求 ID → 架构决定 → 当前任务约束 → story → 实现 → 证据与复核。角色是执行这些职责的手段，不需要在小项目同时启动全部角色。

## 值得采用的机制

| 机制及实际来源 | 解决的返工原因 | 本项目的落地方式 |
| --- | --- | --- |
| CCGS `story-readiness/SKILL.md` | 需求、依赖和验证办法未齐就开工 | [前置质量流程](../QUALITY_FIRST_WORKFLOW.md) 与 [开工审查](../../templates/PREFLIGHT.md) |
| CCGS 系统 GDD 和手感指导 | 只有形容词，状态、反馈和异常路径含糊 | [系统规格](../../templates/SYSTEM_SPEC.md)，人工定意图，AI检查完整性 |
| CCGS 跨 GDD 场景走查 | 各模块分别合理，组合后状态或数值冲突 | 开工前走查实际玩家场景与输入输出契约 |
| CCGS 需求注册与架构追溯；BMad 覆盖验证 | 漏做、重复做、无需求的额外实现 | [追溯表](../../templates/TRACEABILITY.md)，稳定 ID、权威来源、影响分析 |
| CCGS Art Bible 与视觉和技术规格分工 | 风格不一致、资产制作后才发现无法用于游戏 | [美术方向](../../templates/ART_DIRECTION.md)、[黄金样例](../../templates/GOLDEN_SAMPLE.md) |
| CCGS 审查输入哈希 | 改了规范或产物仍沿用旧批准 | 输入及产物快照检查，失效证据必须重新判断 |
| BMad 任务依赖和前序经验 | 任务拆分遗漏接口、重复踩同一问题 | [交接契约](../../templates/HANDOFF.md) 和任务上下文 |
| Superpowers spec 与 quality 双结论 | “做了需求”与“做得可靠”混为一谈 | 验收分别写规格符合性、制作质量和运行验证结论 |
| Superpowers 根因分析和修复复核 | 多处盲改、修完旧问题引入新问题 | 单一假设、最小实验、逐缺陷重验与经验记录 |

这里是机制选择和本地改编，并非直接启用上游命令。具体事实可在已保存的 [固定来源清单](../../references/sources.lock.json) 中定位。

## CCGS 需要加强的实际限制

**多数 hooks 不构成硬阻断。** `validate-write.sh`、`validate-registry.sh` 和 `validate-agent-artifact.sh` 明确以警告为主；`validate-push.sh` 的阻断示例被注释；`validate-commit.sh` 说明只有特定数据目录的 JSON 检查会阻断。文件命名和 JSON 检查不能证明模型、材质、动画或游戏体验合格。[提交检查源码](https://github.com/Donchitos/Claude-Code-Game-Studios/blob/05ad7c8a5b8aa2aa86bbd3439ae7cf7295cac418/.claude/hooks/validate-commit.sh#L471)、[推送检查源码](https://github.com/Donchitos/Claude-Code-Game-Studios/blob/05ad7c8a5b8aa2aa86bbd3439ae7cf7295cac418/.claude/hooks/validate-push.sh#L269)

**阶段状态和实际验证需要区分。** `workflow-catalog.yaml` 明确阶段结论是 advisory；`story.md` 注明逻辑测试在完成检查中可能只核对存在性，运行结果由后续流程确定。我们要求任务接受前，相应验证已经实际执行。

**旧证据可能只被提醒而不阻止关闭。** `story-done/SKILL.md` 第 170–174 行要求报告哈希变化等信息，但规定不改变 verdict。我们采用更严格的规则：受影响证据失效，不能作为当前版本通过的依据。[固定源码](https://github.com/Donchitos/Claude-Code-Game-Studios/blob/05ad7c8a5b8aa2aa86bbd3439ae7cf7295cac418/.claude/skills/story-done/SKILL.md#L170)

**Unity 测试辅助脚本有静态误报风险。** `test-run.sh` 第 397–409 行读取失败数后，存在根据进程退出 0、报告存在、总数大于 0 就宣称全部通过的分支，没有在该分支用失败或跳过数限制。包装命令错误地返回 0 时可能触发；本次没有运行上游脚本复现。我们独立编写结果解析器，并用失败、跳过、零测试和计数冲突样例测试它。[固定源码](https://github.com/Donchitos/Claude-Code-Game-Studios/blob/05ad7c8a5b8aa2aa86bbd3439ae7cf7295cac418/.claude/scripts/test-run.sh#L397)

**美术制作能力不能由角色名称推定。** `asset-spec/SKILL.md` 明确目前主要生成提示词文本；技术美术角色虽含 shader、VFX 和渲染优化职责，但明确不负责创建最终美术资产，不能据此当作完整 Blender 制作系统。我们仍需实际源文件、导出、引擎整合及目标设备样例。[资产规格范围](https://github.com/Donchitos/Claude-Code-Game-Studios/blob/05ad7c8a5b8aa2aa86bbd3439ae7cf7295cac418/.claude/skills/asset-spec/SKILL.md#L25)、[角色边界](https://github.com/Donchitos/Claude-Code-Game-Studios/blob/05ad7c8a5b8aa2aa86bbd3439ae7cf7295cac418/.claude/agents/technical-artist.md#L115)

**框架审查不全是行为执行测试。** `skill-test` 的 spec 模式明确由模型推理检查，而非实际执行整个工作流。可以发现提示词遗漏，但不能证明未来 agent 一定遵循。我们的自动检查负例是真实执行的 Python 测试；真实游戏流程仍需试运行验证。[固定源码](https://github.com/Donchitos/Claude-Code-Game-Studios/blob/05ad7c8a5b8aa2aa86bbd3439ae7cf7295cac418/.claude/skills/skill-test/SKILL.md#L354)

## 不采用的默认策略

- CCGS 的默认 minimal 强度会降低审查并豁免部分测试。我们把文档篇幅、内容规模、agent 数量和验证强度分开，少量内容仍可严格验证。
- 不采用每次写文件都要求批准。你决定体验、风格、范围与质量取舍；已授权实施由 AI 自主推进。
- 不照搬固定一天原型、固定几轮失败就终止、固定几分钟切片等数值。规模由实际验证问题决定。
- 不强制把所有切片代码重写。任务先标记抛弃式试验或可演进样板；后者通过生产标准后可以保留。
- 不把 UI 截图等同于触控、返回、状态切换和生命周期交互通过。
- 不按断言数量、review 评论数或 agent 数量评价质量，不要求评审者为了配额制造问题。
- 不把 Claude Code 的工具名称、模型、Bash 事件 hooks 当成 Codex 已接通能力。

## BMad 与 Superpowers 的补充及限制

BMad 的需求覆盖矩阵、任务依赖和上下文准备很适合减少漏项。需要注意其 story 模板默认 `ready-for-dev` 并称验证可选，而开发完成检查将部分原始证据列为可选输入；正文仍要求测试通过，因此不能概括成“不要求测试”。本项目保持 DRAFT 默认及明确的实际证据要求。[故事模板](https://github.com/bmad-code-org/bmad-module-game-dev-studio/blob/ff8870955203e1665f4bf75a64049078e6d4080c/src/workflows/4-production/gds-create-story/template.md)、[覆盖验证](https://github.com/bmad-code-org/bmad-module-game-dev-studio/blob/ff8870955203e1665f4bf75a64049078e6d4080c/src/workflows/3-technical/gds-check-implementation-readiness/steps/step-03-epic-coverage-validation.md)

BMad 评审汇总文本中存在“失败评审层且零发现时应提示未完成”与“零发现时宣布全部通过”的分支歧义。这是静态提示词审查发现，不是已执行复现的软件故障。我们用通过、有缺陷、未完成、不适用四种互斥结论，未完成永不等同通过。[汇总步骤](https://github.com/bmad-code-org/bmad-module-game-dev-studio/blob/ff8870955203e1665f4bf75a64049078e6d4080c/src/workflows/4-production/gds-code-review/steps/step-03-triage.md#L42)

当前 Superpowers 采用每个任务一名 reviewer 给出 spec 和 quality 两类结论，再做整体评审；不能拿旧版本介绍称它自动提供两名独立审查者。它的固定 BASE/HEAD 审查范围、逐缺陷复核及根因调试值得采用。我们不采用由控制者将未解决硬问题 park 后仍 complete 的策略，也不把执行期间不能询问用户的规则用于游戏创意决定。[任务审查](https://github.com/obra/superpowers/blob/2ce8aa15d05bd57213e6f746f953aafb7ded7a8b/skills/subagent-driven-development/task-reviewer-prompt.md)、[修复循环](https://github.com/obra/superpowers/blob/2ce8aa15d05bd57213e6f746f953aafb7ded7a8b/skills/subagent-driven-development/references/fix-loop.md)

## 本轮实现与待验证范围

本轮新增前置审查、系统和美术规格、追溯、交接与黄金样例模板；升级任务和验收规则；保存 35 份原文件及许可；增加本地来源、快照和 NUnit 结果检查与自动负例测试。

这些工具只检查来源副本、文件内容一致性和测试报告内部结果，不判断审美、乐趣、真正的审核人身份或报告来源是否可信。它们尚未连接 Unity/Blender、CI 或远端分支保护。实际执行方式与验证结果分别见 [检查工具说明](../../tools/QUALITY_CHECK.md) 和 [框架验证记录](../FRAMEWORK_VALIDATION.md)。

下一步应用于一个代表性样板，并记录问题发现阶段、首次验收通过情况、返工原因和返工范围。重点观察是否在正式制作前解决了错误前提，不把“没有问题记录”当成流程成功。
