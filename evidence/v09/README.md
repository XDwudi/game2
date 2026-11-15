# v0.9（p0.9.0）证据

冻结源 `7f56dc2cfaa964457274cd31659d0f6e7d7eb509`，分支 `codex/v0.9`。逐条验收结论见 `docs/tasks/P0-V09.md` §6，失败记录见 §7，独立复核见 §8。

| 目录 | 内容 |
| --- | --- |
| `rules/` | `golden-provenance.json`：260 条新卡黄金样例由独立参考实现生成的来历，以及旧 160 条在新旧配置下的复现结果 |
| `unity/` | NUnit 报告与绑定（含全部失败与作废轮次）、测试发现清单、PDB 核对、构建门禁、两个平台的构建记录。最终三份：EditMode `nunit-20261114-235246`，PlayMode 1080p `nunit-20261115-001844`，PlayMode 720p `nunit-20261115-004506`。`nunit-20261114-225037`、`…231603`、`…234251` 是上一个冻结提交 `b3fcc2f` 的全通过报告，因门禁拒绝重名用例而作废 |
| `packages/` | 两个发行包的成员清单与 SHA-256。包本身在 `Releases/p0.9.0/`，不入库 |
| `screens/` | 打包后的 Windows 版回放截图节选：`tour-5001`（机器人种子 5001 的前 150 步，回放脚本 `tour-trace-5001.json`）、`fixture-Burst09`（最高一档高分动画） |
| `balance/` | 模拟器对拍、逐卡强度、校准前后两组 300 局与最终复跑，说明见其 README |
| `art/` | `run-audit.json`：51 张插画的生成运行核对（模型是否收到参考图、提示词是否逐字、是否请求透明） |
| `delivery/` | 发行收据 |

没有入库：Unity 编辑器与构建日志（`unity/*.log`）、完整的 146 张回放截图（本机 `artifacts/cache/v09/screens-full`）。

这些证据不包含：真人试玩、macOS 实机启动、Windows 帧时间与独占全屏专项、卡面的独立美术复核。