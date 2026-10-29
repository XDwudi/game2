# Player04 独立技术复核

spec_audit，2026-10-29 UTC。输入 `evidence/runs/player/player-20261029-012720-f50658f6`，对应 build05。审查者没有启动 Unity 或 Player；独立实际运行 `audit_player04.py`，22 组 trace、磁盘、动画、图像完整性和原始 CSV 指标复算全部吻合报告。**这表示报告可信，不能表示性能通过。**

## 分列结论

**规格符合性：核心与本轮功能通过；AC-PERF 失败。** 97 条非 Debug 正常六关命令逐字段等于原 trace，磁盘和渲染状态匹配记录完整，正常档在独立 Debug 压力前后不变。31 次落地记录所有骰子 position/rotation/scale 误差均为 0，结果面法向 +Y；正常、减少动态和跳过路径都有真实记录。未发现新规则偏差。

**制作质量：本轮检查画面通过，性能需返工。** 实际以原始尺寸查看 9 图，清单与 SHA 在 `player04-independent-artifact-audit.json`：正常选择 1080p、十骰 720p、分层骰面详情 1440p、逐骰计分 720p 顶部/中部/底部、商店 720p、最大容量 1080p、180 秒结束画面 1080p。逐步公式可滚动读完，详情三维预览未被遮挡，10 骰/7 槽/10 控制器/5 道具与文字可见。48 PNG 含 2 预热图，46 接受截图尺寸一致且无黑图；没有 Unity error。风格与真人体验不在此技术签署内。

**实际运行：完整采样明确超预算，进入 REWORK。** 1080p 可见 Windows Player，在 Ryzen 9 5900X、RTX 3080、Windows 10 / D3D11、Unity 6000.6.7f1，vSync=0、targetFrameRate=60 下连续 180.0030149 秒；CSV 10,599 行，去首部分帧后 10,598 有效帧。原始十项指标分别重算 samples/coverage/mean/P95/max，与报告一致。

| 指标 | 独立确认结果 |
|---|---|
| 帧间隔 | 平均 16.98349 ms；P95 **23.5467 ms**；最大 48.3832 ms |
| 主线程 | P95 17.0496 ms；最大 34.8659 ms |
| Render thread | P95 0.3123 ms |
| GPU | 有效覆盖 96.622%；P95 0.232448 ms；无非法/重复时间戳 |
| UI rebuild | 有效覆盖 100%；P95 0.0065 ms；每帧批次 0 或 1，无中断 |
| GC alloc | P95 1,265,626 B；最大 8,946,498 B |
| 探针记录缓冲 | 预热后一次预分配 60,000；最终仍 60,000；测量中扩容 **0** |

报告 `status=budget_exceeded / exitCode=3` 正确，不能沿用 Player02/03 的采集缺口解释此次失败，也不能降低 16.7 ms 或 95% 完整性阈值。root 已开始 UI 复用优化；该后续代码使本轮相应 UI/性能证据成为修复前基线，必须重新真实运行。

## 版本绑定与原始证据

- buildGuid `1ece7f2b268b4656b12e498d74f0841c`；Runtime MVID `d7c43013-9527-44a4-b3f4-0f78289a9711`。
- Runtime DLL SHA-256 `35e09ffe3bf3943247259461fecb6f36d9d1c301ef6888487a274c6fef5372d3`；Core `78deceeab59142c51bfb6a5ef86a1a920e6df712104501ad916405314f145fa4`。
- `player04-pdb-independent.json`：2 程序集 20 项目源文档，在池化修改前与当前源码全部匹配。`unity08-pdb-independent.json`：5 程序集 35 文档全部匹配。
- probe SHA `28162622312d1a81c8636a8f411def69bccd0fbd7028363fbfa117c023c717c6`；其预分配修复 18 项托管检查已独立复跑，日志 `probe-preallocation-managed-independent.log`，不替代本次原生采样。
- Player 报告 SHA `df0c23368a967b55f25bf987b1915b9a82fbc53f500e7c1021af377c21cfb9df`；全部复算见 `player04-independent-artifact-audit.json/.log`。
- PlayMode08 18/18、EditMode05 325/325 的实际 NUnit 和对应 PDB 已检查，见 `acceptance-mapping-independent.json`。核心规则源未变，因此核心证据有效；新的 UI 池化影响 PlayMode 与播放器，不能把本次旧运行自动当作池化通过。

失败历史保留。真人 5–8 人各至少 2 局未运行，移动平台本轮不适用。此报告没有整体 ACCEPTED 或发布结论。
