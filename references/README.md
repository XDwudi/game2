# 开源方法参考与复用记录

这些资料用于研究和选择性改编，不是本项目已经启用的插件或执行规则。项目实际规则以根目录 AGENTS.md 和 docs 中的本地方案为准。不要在参考仓库中启动开发任务或执行其安装器、更新器、hooks。

## 保存的内容

- `upstream/`：从公开 GitHub 仓库下载的完整浅克隆，用于源码核查。含上游配置与嵌套 Git 数据，不纳入未来本项目版本控制。
- `vendor/`：从固定 Git 提交提取的 35 份原始文件，包含三份 LICENSE。它们是部分参考快照，未收齐运行整个上游框架所需的依赖。
- [sources.lock.json](sources.lock.json)：仓库、完整提交 ID、原始路径、本地路径、SHA-256 与许可证位置。复制文件直接取自 Git blob，没有改动内容。
- [源码研究与取舍](../docs/research/OPEN_SOURCE_REVIEW.md)：说明哪些方法被采用、改造或拒绝，以及依据。

## 版本基线

| 来源 | 固定提交 | 原始许可 |
| --- | --- | --- |
| [Claude Code Game Studios](https://github.com/Donchitos/Claude-Code-Game-Studios) | `05ad7c8a5b8aa2aa86bbd3439ae7cf7295cac418` | [MIT](vendor/ccgs/LICENSE) |
| [BMad Game Dev Studio](https://github.com/bmad-code-org/bmad-module-game-dev-studio) | `ff8870955203e1665f4bf75a64049078e6d4080c` | [MIT 及原文件内声明](vendor/bmad-game-dev-studio/LICENSE) |
| [Superpowers](https://github.com/obra/superpowers) | `2ce8aa15d05bd57213e6f746f953aafb7ded7a8b` | [MIT](vendor/superpowers/LICENSE) |

## 实际复用方式

本项目吸收任务准备度、需求追溯、风格规范、接口走查、内容哈希、规格与实现分别评审等结构，形成中文工作规范和模板。原始文件保持不变，许可证一并保留；本地改编的主要差异记录在研究报告中。

没有将上游 `.claude/settings.json`、模型名称、批准流程、安装器或 Bash 钩子放到项目根目录。下载到本地不等于在 Codex 中安装或启用。参考文件即使名为 SKILL.md 或 CLAUDE.md，也只是被研究材料。

## 更新方式

以后升级参考资料时，先比较新旧提交和许可，再选择变更；不要自动拉取最新版本并覆盖本项目规则。更新后重做受影响流程的检查与试运行，最后更新锁文件。副本哈希一致只能证明文件没有偏离记录，不能证明上游软件可靠。

校验命令见 [本地检查工具](../tools/QUALITY_CHECK.md)。原始模板之间可能引用未纳入快照的文件，不能把 `vendor/` 当成完整可运行安装包。
