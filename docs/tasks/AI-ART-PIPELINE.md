# AI 美术生产技能与工具接入

状态：ACCEPTED（仅本工具、指导与能力试验任务）。日期：2026-11-08。任务性质：可演进的项目工具与制作指导；不制作新游戏版本。依据：独立审查者 `/root/art_pipeline_review` 的 [REVIEW](../../evidence/ai-art-pipeline/review/REVIEW.md)，非实施者自签。

## 输入和边界

- 用户要求：分析先进 AI 美术方案和当前项目，查找并加入适用技能/工具，保障质量与统一风格，说明其他工具需求并接入 OpenAI image2 前期概念能力。
- BASE：`246646b0e4fb87595727b0fbf48559f1947230ba`；开始时已有未跟踪发行包、cache、evidence/v07 与 evidence/v071，保留原样。
- 设计唯一来源：[v0.71 ART_SPEC](../specs/v071/ART_SPEC.md)、[ASSET_MAP](../specs/v071/ASSET_MAP.md)；玩法 p0.6.0/schema4、当前生产状态见 [STATE](../STATE.md)。
- 范围：研究、项目级技能、生成调用说明、只读 PNG/来源检查器、一个已有 I07 主题的隔离概念调用试验、独立复核。
- 不改：游戏 Assets/Packages/ProjectSettings、生产素材、玩法、风格、验收预算、旧门禁、旧记录；不启动或写入 Unity/Blender，不安装未经验证的外部节点/服务，不批量生产剩余 67 项。
- 概念试验只证明内置工具能返回并保存图片；不作为新风格标杆或生产替换。人工风格及扩大量产决定仍保持原边界。

## 交付和所有权

| 所有者 | 可写路径 | 交付 |
|---|---|---|
| root | `.agents/skills/game-art-production/`、`docs/art/`、`docs/tasks/AI-ART-PIPELINE*`、`templates/AI_ART_JOB.md`、`ArtSource/AIConcepts/`、`evidence/ai-art-pipeline/`、AGENTS/README/STATE/TOOLCHAIN 的限域导航 | 项目技能、调用方法、流程、概念试验和集成 |
| art_audit | `docs/research/AI_ART_PROJECT_AUDIT.md` | 只读当前美术与实际图像审计 |
| art_research | `docs/research/AI_ART_TOOLS_RESEARCH.md` | 第一方工具/技能研究与采纳理由 |
| art_checker | `tools/art_asset_audit.py`、`tools/ART_ASSET_AUDIT.md`、`tests/test_art_asset_audit.py`、`evidence/ai-art-pipeline/checker/` | 只读检查器、负例和四图实跑 |
| 独立复核者（实施者之外） | `evidence/ai-art-pipeline/review/` | 全任务规格/制作质量与技能情境演练 |

Unity、Blender、场景、Prefab 无本任务写锁；其他任务的历史锁不在此解除。

## 验收条件

| ID | 场景、方法、期望 | 证据 | 责任 |
|---|---|---|---|
| ARTPIPE-01 | 检查当前实际素材、截图与规范，分别列已实现/未验/差距，不用旧版像素规范覆盖 v071 | 项目审计及源路径 | art_audit，独立复核 |
| ARTPIPE-02 | 搜索并打开第一方资料；工具比较含来源、许可边界、接通状态、适配/延后理由 | 工具研究与 imagegen 调用文档 | art_research/root，独立复核 |
| ARTPIPE-03 | 项目技能有合法 frontmatter、路径可解析，正常自动发现布局；真实情境演练能保持参考图、规则和审批边界 | quick_validate、情境演练及文件快照 | root，独立复核 |
| ARTPIPE-04 | 对真实四图运行 PNG/哈希/声明尺寸/透明检查；负例拒绝空清单、越界、损坏和失配；不改输入、不输出审美通过 | checker JSON；`python -m unittest discover -s tests -v` | art_checker，独立复核 |
| ARTPIPE-05 | 调用一次内置 imagegen，保存原图、完整提示词、参考哈希、真实结果和限制；失败如实记录，不要求换服务 | `ArtSource/AIConcepts/imagegen-probe-20261108/` | root，独立复核 |
| ARTPIPE-06 | AGENTS/README/TOOLCHAIN 可导航到技能；STATE 更新；生产文件差异为空，原有未验不关闭 | 任务范围差异、证据快照、独立结论 | root，独立复核 |

验证环境：本地 Windows，现有 Python/Pillow；官方网页和会话内置 image_gen 工具。无 GPU 训练/API 付费批量/新引擎执行。游戏性能、平台构建、Mac 实机本任务不适用：没有生产改动。

准备度见 [AI-ART-PIPELINE-PREFLIGHT](AI-ART-PIPELINE-PREFLIGHT.md)。仅工具/文档可由独立技术复核接受，概念图审美保持待人工。

## 实施结果

ARTPIPE-01～05 已提交实物与运行证据；ARTPIPE-06 导航/STATE已更新，生产路径diff为空。技能位于官方项目发现位置，独立agent已实际发现并用C01完成准备演练。检查器33项测试/四图通过；内置生成一次真实返回候选、归档与1图检查通过。初次技能验证编码失败与概念低alpha残留均保留于 [验证记录](../../evidence/ai-art-pipeline/VALIDATION.md)。

独立结论与逐条验收在 `evidence/ai-art-pipeline/review/`：ARTPIPE-01～06通过，规格符合性与工具/指导制作质量通过；本状态依据该结论转录为 ACCEPTED。技能已实际发现并完成一次准备演练；概念图的生产艺术验收仍未完成，不批准美术标杆或转入67项量产。没有安装第三方服务、改变引擎或发布游戏。任务文件写锁已释放。
