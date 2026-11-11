# P0-V072-ART：v0.72 高清简约美术与动画

状态：IMPLEMENTING（2026-11-11 开工）
规格：[v0.72 开发方案与美术规格](../specs/v072/ART_SPEC.md)
输入版本：源 `944adf6`（分支 `codex/v0.71`），玩法 p0.6.0 / schema4，Unity 6000.6.7f1。
工作分支：`codex/v0.72`。发行身份 p0.7.2 / art0.7.2。

## 目标

按用户 2026-11-11 指令优化表现层并交付试玩版。像素方向已在同日被用户否决（“完全是糊了”），
本任务按高清、锐利、简约方向实施。

## 范围

- 新增 `tools/art_v072/`、`DicePrototype/Assets/Art/Art072/`。
- 修改表现层：`Assets/Game/UI/`、`Runtime/Shaders/Art07Felt.shader`、`Editor/`、Bootstrap / ArtStyleLab 的资源绑定。
- 版本身份、交付工具与文档适配 v072。

## 非目标

- 不改 `Core/Rules`、`Core/Session`、`P0Config.json`、`GoldenScores.json`、存档结构、布局矩形与热区。
- 不重画 71 张插画、符号与特效贴图；不改骰子模型（Blender 未调用）。
- 不引入需要下载的第三方资源；不做移动端验证。
- 不处理历史遗留的独占全屏、帧时间、AudioMixer、Mac 实机缺口（保留为已知问题）。

## 验收条件

| ID | 条件 | 方法 | 期望 | 证据 | 责任 |
| --- | --- | --- | --- | --- | --- |
| V72-A | 素材可复现 | 重跑 `tools/art_v072/build_art.py` 并比对 SHA | 输出与已提交文件逐字节一致；规格第 3 节名称齐全 | `evidence/v072/assets/verify.json` | 主 agent |
| V72-B | 实机画面锐利、风格一致 | 开发构建截图巡览，目检主菜单 / 对局 / 商店 / 结算 | 无模糊缩放件；同类组件一致 | `evidence/v072/screens/` | 主 agent；风格由用户判定 |
| V72-C | 玩法未变 | 完整 EditMode | 0 失败；Config / Golden SHA 与 `944adf6` 相同 | `evidence/v072/unity/` | 主 agent |
| V72-D | 界面可用 | 完整 PlayMode | 0 失败；因美术变更而修改的断言逐条列入“断言变更” | `evidence/v072/unity/`、本文件 | 主 agent |
| V72-E | 动画可关闭 | 巡览的减少动态运行 | 减少动态时卡牌与字标静止 | `evidence/v072/screens/reduced/` | 主 agent |
| V72-F | 可试玩构建 | 门禁后的 Windows x64 构建 + 新目录启动 | 构建成功；启动进入主菜单并能进行对局 | `evidence/v072/unity/build-*.json`、`evidence/v072/player/` | 主 agent |
| V72-G | 交付 | 推送 + 发行附件 + 远端回读 | 分支与标签远端可见；附件 SHA 与本地一致 | `evidence/v072/delivery/` | 主 agent |

本任务的独立复核与人工风格 / 体验判断尚未进行；交付时状态为 REVIEW，不是 ACCEPTED。

## 断言变更

（实施中逐条填写：测试名、原断言、新断言、对应规格条目。）

## 记录

- 2026-11-11：建立任务。环境：Unity 6000.6.7f1 在 `E:\unity\6000.6.7f1`，Blender 5.2 在 `E:\blender`，
  git 2.56（Codex 运行时自带，不在 PATH），Python 3 + Pillow 9.4 / numpy 1.24。无图像生成工具。
- 2026-11-11：按初始指令做了像素方向（DC-32 色板、64×64 插画转换、1/3 分辨率世界渲染），
  合成原型图与一轮实机截图后，用户否决该方向。相关素材与渲染代码已全部删除。
- 2026-11-11：改为高清简约方向；首轮实机截图（主菜单 / 对局 / 商店）已发给用户。
  据截图取消桌面缝线（压到实时文字），并把骰面预览改为圆角骰面。
