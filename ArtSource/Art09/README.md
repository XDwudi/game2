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

- 先做了一张样张（C54），与既有的 C33 并排确认线条、配色、半调网点、透明底一致后才启动批量。
- 51 张全部一次生成，没有重做；全部是 1254×1254 RGBA，带真实透明通道。
- 实施者逐张看过拼版总览（深色毛毡底与暖纸底交替）：没有文字、数字、边框或裁切；红蓝黄三色的使用与既有插画一致。
- 这只是实施者的目检。没有美术人员复核，没有逐张在 64 px 小尺寸下验收，是否保留、重画由用户决定。重做某一张：`generate.py --redo <ID>`（旧图会改名为 `rejected-N.png` 保留）。
