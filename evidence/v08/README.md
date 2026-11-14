# v0.8（p0.8.0）证据

冻结源 `17e79b6a4eb49d3964f4508437b3549c9e556814`，分支 `codex/v0.8`。逐条验收结论见 `docs/tasks/P0-V08-BALANCE.md` §5，失败记录见 §6。

| 目录 | 内容 |
| --- | --- |
| `rules/` | `golden-provenance.json`：黄金样例由独立参考实现重新生成的来历（先在 p0.6.0 上复现 160 条，再生成 p0.8.0） |
| `unity/` | NUnit 报告与绑定（含全部失败轮次）、测试发现清单、PDB 核对、构建门禁、两个平台的构建记录。最终三份：EditMode `nunit-20261114-095309`，PlayMode 1080p `nunit-20261114-101712`，PlayMode 720p `nunit-20261114-104226` |
| `packages/` | 两个发行包的成员清单与 SHA-256。包本身在 `Releases/p0.8.0/`，不入库 |
| `screens/` | 打包后的 Windows 版回放截图节选：`tour-5007`（机器人种子 5007 的前 70 步，回放脚本 `tour-trace-5007.json`）、`fixture-G147`、`fixture-Burst08`。每个目录的 `tour.json` 是游戏写出的回放报告 |
| `balance/` | 实施后的平衡模拟。`run_p080.*`：游戏实际配置；`run_whatif_late_targets.*`：只上调第 12–24 关目标的备选配置 `whatif_late_targets.json`（**未应用到游戏**） |

没有入库的内容：Unity 编辑器与构建日志（`unity/*.log`，含本机路径）、完整的 72 张回放截图（本机 `artifacts/cache/v08/screens-full`）。

这些证据不包含：真人试玩、macOS 实机启动、Windows 帧时间与独占全屏专项、独立复核。
