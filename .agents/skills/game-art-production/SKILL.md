---
name: game-art-production
description: Create, revise, integrate, or review art assets for this DicePrototype project, preserving its approved visual master and quality evidence. Use for concept art, card illustrations, surfaces, dice art, and art batch planning; not for unrelated code or gameplay design.
---

# 项目美术生产

先定位含 `DicePrototype/` 与 `docs/STATE.md` 的仓库根目录。下列 `docs/`、`tools/`、`ArtSource/` 路径均相对该根目录。此技能是项目工作指导，不是安装器或自动验收器。

## 读最少的权威输入

读 `AGENTS.md`、`README.md`、`docs/STATE.md` 和当前美术任务。当前视觉以 `docs/specs/v071/ART_SPEC.md` 为准，实际路径及缺口在同目录 `ASSET_MAP.md`；新版本出现时先沿 STATE 定位，不能继续硬套 v071。`docs/art/AI_ART_PIPELINE.md` 提供生产/复核路由。

从 `DicePrototype/Assets/Game/Data/P0Config.json` 取稳定内容 ID 与真实含义。保持现有玩法、存档、面序、交互、UI 框架与渲染管线。参考图中的数字和文字不是配置。

## 按资产选择方法

| 请求 | 方法 |
|---|---|
| 母版探索、主题插画、织物/纸纹、参考图局部编辑 | 使用会话内置 `imagegen` 技能和真实 image_gen 工具；读 [imagegen.md](references/imagegen.md) |
| 固定九宫格、规则点阵、简单几何状态图标 | 复用现有可编辑矢量/绘制脚本与 UiKit，不重新生图猜尺寸 |
| 陶瓷骰子 | 延续 `tools/art_v071/build_dice.py`、现有 blend/面映射/导出参数，Blender 工具与引擎写锁先核实 |
| Unity UI 接入 | 查真实 uGUI/TMP 与共享组件，使用环境实际提供的 `unity:ui` / `unity:ui-ugui` 等适用技能；技能存在不等于 Editor 已连接 |
| 资源技术检查/视觉审查 | 读 `tools/ART_ASSET_AUDIT.md`，复用当前任务的实际场景、截图、回归和预算 |

## 维持同一视觉依据

每个制作包指向同一母版、相应已有插画或实际组件，记录版本/哈希和角色。当前参考位于 `docs/specs/v071/handoff/references/`、`handoff/assets/`；已接入四图在 `ArtSource/Art071/Illustrations/`。先看图再制作。

v071 是高清限色印刷插画，深青毛毡、暖纸、深墨轮廓、红蓝点数与陶瓷骰子；颜色与材质细节引用 ART_SPEC §05 和工程主题，不另创色板。旧64px像素检查只适用于其旧资产。主体面积、色值与尺寸中的“起点/建议”不能擅自升级为硬阈值。

主题层与规则层分开：插画不烘焙卡名、价格、倍率、徽记、状态、完整按钮或规则示意；精确骰点由引擎产生。固定主标题字标按现有规格例外处理。

先完成一个代表对象的源 → 导出 → 实际组件 → 实机画面闭环，再按已授权批次扩大。沿 v071 的三个对照点：母版/样件、对局截图/短片、对局/商店/手册并排。同类资源共享卡框、字体、光照、材质和状态层。已确认方向内的常规修正自主做，不增加逐图审批；新标杆、风格改变或转入量产由用户决定。

## 输出与检查

- 用 `templates/AI_ART_JOB.md` 记录本批必要信息；简任务可精简，不复制整套策划。
- 原图、完整提示词、输入图角色/哈希、真实工具/模型可见性、修订关系和来源放在任务资产目录。概念稿放 `ArtSource/AIConcepts/<task>/`，不直接混入生产 Assets。后续正式交付沿 ASSET_MAP 的源/引擎路径。
- PNG 是可编辑栅格原图，不等于有手绘图层的 KRA/PSD。需要分层的任务必须交真实可编辑层并重开核验。3D 保留 blend、外部依赖、导出和实际引擎材质。
- 使用 PNG 检查器只判断文件、哈希、声明尺寸、透明度和可选副本；它不验证审美、图中点数、Unity 实际设置、性能或授权真实性。
- 看浅纸/深桌面上的透明边缘、实际显示尺寸和同批并排图；检查白边、灰底、裁切、轮廓/粒度/色彩漂移。发现具体问题做单项修订，保留失败稿及理由，不用无限抽图替代定位。
- 生产集成沿 `docs/tasks/P0-V071-IMPLEMENTATION.md` 的 V71-A～E 和当前验收契约，查看实际引擎截图及交互片段。只改素材时选择受影响验证；不把无关全游戏回归强加给纯概念任务。
- 交付分别写规格符合性、制作质量、实际运行范围和未验项，交实施者之外的人/agent复核；更新 STATE。不得自行把概念、自动检查或新截图写为艺术 ACCEPTED。

## 工具缺失时

参考 `docs/research/AI_ART_TOOLS_RESEARCH.md` 选择候选，而非自动安装。先检查本会话工具 schema、本地版本及读写/输出试验。缺图像工具可继续提示词、素材盘点和确定性资源；缺引擎只阻塞集成结论。不要无声换模型、下载整套节点或把工具列表当接通证据。
