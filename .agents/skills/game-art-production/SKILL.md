---
name: game-art-production
description: Create, revise, integrate, or review art assets for this DicePrototype project, preserving its approved visual master and quality evidence. Use for concept art, card illustrations, surfaces, dice art, and art batch planning; not for unrelated code or gameplay design.
---

# 项目美术生产

先定位含 `DicePrototype/` 与 `docs/STATE.md` 的仓库根目录。下列 `docs/`、`tools/`、`ArtSource/` 路径均相对该根目录。此技能是项目工作指导，不是安装器或自动验收器。

## 读最少的权威输入

1. 读 `AGENTS.md`、`README.md`、`docs/STATE.md` 和当前美术任务。
2. **风格**看 `docs/art/STYLE_GUIDE.md`：各层的现行版本、色板、插画规范、锚点集和待决项。不要再把 v071 ART_SPEC 当作全部现行风格。
3. **流程**看 `docs/art/AI_ART_PIPELINE.md`：S0–S8 步骤、路线表、审查清单和签署权。
4. **机器可读的风格基准**是 `ArtSource/StyleKit/style_kit.json`。

内容 ID 和真实含义从 `DicePrototype/Assets/Game/Data/P0Config.json` 读取。保持现有玩法、存档、面序、交互、UI 框架与渲染管线。参考图中的数字和文字不是配置。

## 按资产选择方法

| 请求 | 方法 |
|---|---|
| 内容插画（C/I/O/U）、概念图、参考图局部编辑 | `python tools/art_gen/artgen.py generate --job <job.json>`，经本机 Codex CLI 调用 GPT 内置图像生成；在 Codex 桌面内交互调用时读 [imagegen.md](references/imagegen.md) |
| 九宫格、界面件、功能图标、规则点阵 | 复用现有矢量脚本（`tools/art_v073/build_art.py` 画法）与 UiKit，不用生图猜尺寸 |
| 陶瓷骰子 | 延续 `tools/art_v071/build_dice.py`、现有 blend、面映射与导出参数；先核实 Blender 与引擎写锁 |
| Unity UI 接入 | 查真实 uGUI/TMP 与共享组件；技能存在不等于 Editor 已连接 |
| 资源技术检查、视觉审查 | `artgen` 审查单、`tools/ART_ASSET_AUDIT.md`、当前任务的实机截图与回归 |

## 维持同一视觉依据

- 每次生成之前运行 `artgen verify-kit`。不通过就停下查原因，不能改 SHA 让它通过。
- 插画只在 job 的 `subject` 里改主体，按总纲 §4.4 的写法：只写物件、动作、关系，不写颜色。画法、色彩角色、构图和禁止项由 kit 统一拼接。
- 永远以锚点样板为参考，不用上一张输出当风格参考。`edit` 只修同一张图的具体缺陷。
- 插画不烘焙卡名、价格、倍率、徽记、状态、按钮或规则示意；精确骰点由引擎产生。
- 先让一个代表件完整走完“源 → 导出 → 实际组件 → 实机画面”，再按已授权批次扩大（每批 4–6 张）。
- 已确认方向内的常规修正自主完成，不增加逐图审批。新标杆、锚点或 kit 变更、风格改变、转入量产由用户决定。

## 输出与检查

- 批次信息用 `templates/AI_ART_JOB.md` 记录。单张图的提示词、参考 SHA、调用核对、额度与尺寸由 `artgen` 写进 `rNN/record.json`。
- 记录为 INVALID_RUN 的运行（参考图没传、提示词被改、不透明等）不得使用；文件保留，先读原因再决定，不盲目重跑。
- 逐项按流程 §5 的 A/B/C/D 清单检查。每个 LOOK 指标要写明原因。同一缺陷最多重抽两次，之后改用编辑、重写主体或换路线。
- PNG 是可编辑的栅格原图，不等于有图层的 KRA/PSD。需要分层的任务必须交真实图层并重开核验。3D 保留 blend、依赖、导出和实际引擎材质。
- 生产集成要看实机截图：1080p 与 720p 下的商店卡、详情、紧凑槽，并与相邻卡并排。只改素材时选择受影响的验证，不把无关的全游戏回归强加给纯概念任务。
- 交付时分别写规格符合性、制作质量、实际运行范围和未验项，交给实施者之外的人或 agent 复核，并更新 STATE。不得把概念、自动检查或自评写成艺术 ACCEPTED。

## 工具缺失时

- 先检查本机真实情况：`codex login status`、`codex features list` 中的 `image_generation`、Python 依赖。
- 图像工具不可用时，可以继续写 job 与主体描述、盘点素材、制作确定性资源；缺引擎只阻塞集成结论。
- 不要无声换模型、自动改走付费 API、下载整套节点，也不要把工具列表当作接通证据。可选工具及其条件见流程 §9。
