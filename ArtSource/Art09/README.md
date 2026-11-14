# v0.9 内容插画（51 张）

50 张新控制器（C54–C103）与新服务“扩充栏位”（O08）的卡面插画。

| 项 | 内容 |
| --- | --- |
| 依据 | 用户 2026-11-14：“你需要什么图像生成工具可以和我提出，现在电脑里也有 gpt 可以供你直接调用它的 image2 模型。” |
| 工具 | 本机 Codex CLI 0.167.0-alpha.3.2 的内置 `image_gen`，每张一次调用。模型快照工具不暴露。为了能在命令行里调用，把已安装的 Codex 应用里的 `codex.exe`、`codex-code-mode-host.exe`、`codex-command-runner.exe`、`rg.exe` 复制到了 `artifacts/cache/codex-bin/`（不入库）；没有读取或改动 Codex 的凭据与配置 |
| 风格依据 | 与 v0.71 的 71 张插画完全相同的公共提示段（取自 `ArtSource/Art071/IllustrationsFull/C33/prompt.txt` 第 1 行）和同样的两张参考图：`docs/specs/v071/handoff/references/battle_master_v071.png`（风格母版）、`ArtSource/Art071/Illustrations/illustration_c29.png`（单张风格锚点） |
| 主体描述 | `tools/art_v09/prompts.json`，每张一句主体与一句轮廓，由实施者撰写 |
| 生成 | `python tools/art_v09/generate.py --codex artifacts/cache/codex-bin/codex.exe --workers 2` |
| 归档 | `Illustrations/<ID>/original.png`（工具输出的原始字节，未做任何像素处理）、`prompt.txt`、`record.json`（哈希、尺寸、透明度统计、参考图哈希） |
| 接入 | `python tools/art_v09/stage.py` 逐字节复制到 `DicePrototype/Assets/Art/Art071/Illustrations/illustration_<id>.png`；`DiceCraft.Editor.V09Setup.Integrate` 按 v0.71 插画的导入设置导入，并只重建两个场景里的 `contentIcons` |

## 过程与结论

- 先做了一张样张（C54），与既有的 C33 并排确认线条、配色、半调网点、透明底一致后才启动批量。51 张全部是 1254×1254 RGBA，带真实透明通道。
- **参考图核对。** 批量进行期间，另一项任务（ART-PIPELINE-2，`docs/art/AI_ART_PIPELINE.md`）发现只用 `-i` 附参考图时，Codex 可能不把参考图传给图像工具。
  用该流程自带的核对规则（`tools/art_gen/artgen.py` 的 `check_run`）回放这 51 次运行的会话日志（`tools/art_v09/audit_runs.py`）：
  45 张模型确实收到了两张参考图（SHA-256 一致、提示词逐字一致、请求了透明背景）；**6 张没有收到**：C59、C67、C71、C78、C80、C90。
  这 6 张已重做：请求里写明两张参考图的路径并要求在工具调用里传入（`generate.py` 已按此修改）。重做后 51 张全部核对通过，结果在每张的 `record.json` 的 `runCheck` 和 `evidence/v09/art/run-audit.json`。
  没收到参考图的 6 张旧图保留为各自目录下的 `rejected-1.png`，没有进入工程。并排比较见下一条。
- 重做前后并排看，带参考图的新图红色更克制、象牙与石油青的比例更接近既有插画（例如 C78 的骰子不再是红蓝两色）。
- 实施者逐张看过拼版总览（深色毛毡底与暖纸底交替）：没有文字、数字、边框或裁切；红蓝黄三色的使用与既有插画一致。

## 与标准流程（pipeline-2.1）的差异

这批图开工时标准流程还不存在，下面几点与它不同，如实列出：

| 项 | 标准流程 | 这批图 |
| --- | --- | --- |
| 风格参考 | `ArtSource/StyleKit/style_kit.json` 的锚点样板图（8 张锚点，状态为“待用户确认”） | v0.71 那 71 张插画用过的同一对参考图。为了 51 张之间、以及与旧 71 张之间保持同一依据，重做的 6 张也沿用这一对 |
| 生成工具 | `artgen generate`，每次运行自动核对并出审查单 | `tools/art_v09/generate.py`；核对是事后用同一套规则补做的 |
| 审查单与 LOOK 指标 | 每张一份 | 没有逐张生成；只有拼版总览的人工目检 |
| 独立复核（S6） | 另一个 agent 或人 | 没有做 |
| 实机截图（S5） | 商店卡、详情、紧凑槽，1080p 与 720p | 见 `evidence/v09/screens/`（打包后的 Windows 版回放截图） |

是否保留这批图、是否在锚点确认后按标准流程整批重做，由用户决定。重做某一张：`generate.py --redo <ID>`（旧图会改名为 `rejected-N.png` 保留）。