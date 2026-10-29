# Windows Player 验收探针独立源码复核

审查者：`/root/spec_audit`；实施者：`/root/session_engine`；2026-10-28。只读检查 `Runtime/Verification/PlayerVerification.cs` 与 README，参照 `docs/tasks/P0-IMPLEMENTATION.md` AC-BUILD / AC-PERF 及规格第 30 节（644 行）。没有运行 Unity 或播放器，未修改实现代码。

## 当前结论

规格符合性：探针的正常通关、Debug 压力隔离、规模和持续时间设计符合本轮输入。制作质量：下面一项采样完整性缺口已修复，修复后静态复核通过。实际运行：独立重跑 7 项纯托管报告检查全部通过；没有运行 Unity 或播放器，不能签署 AC-BUILD / AC-PERF。

## RV-PROBE-01 — 零星有效指标可能被判为完整采样（P2）

审查快照 SHA-256：`c036d579b5a3c1383b9fc40c8a25785510e96ca2247277294e767ef0219b3015`。

`Metric` 以有效值数量大于 0 决定 available；`BuildPerformanceReport` 以 available 决定 gaps。因此 GPU/CPU/UI 只返回一个有效值、此后 180 秒均缺失，仍可能在其他条件满足时报告 within_frame_budget / exit 0。`Marker.ReadNewUiFrame` 容量溢出后设置 unavailableReason 并返回 NaN，但早期值使 Metric.available 保持 true，且原因被清空。同一问题适用于其他中途采样中断。当前 60 FPS 和 100000 UI 样本容量不预期溢出；这里审查的是报告不能把持续缺测解释为完整证据。

可复现的判定输入：某项指标输入 `[1.0, NaN, NaN, ...]`，其余每帧指标有效且帧间隔 P95 在预算内。旧 Metric 返回 available=true、samples=1、unavailableReason=null，旧 gaps 不包含此缺口。

建议：记录采样覆盖率与完整性判据；持续中断保留原因并强制 gaps。覆盖率是证据完整性检查，不应改变原 16.7 ms 帧预算。

关闭证据：修复后源码 SHA-256 `11db2568db5f830b42a20f681e0a05b05b15f0fe299431174e3acd3c4a68e3e2`。Metric 587–600 行记录 expectedFrames / validFrameCoverage；CompleteCoverage 583–586 行要求至少 95% 有效帧；BuildPerformanceReport 553–554 行将 CPU/GPU/GC/UI 覆盖不足及 UI recorder 的中断原因计入 gaps。README / 报告明确 95% 是新增保守证据完整性判据，不是放宽性能阈值。独立执行实际编译的 ProbeCompile.dll，7 项报告测试通过（全覆盖 P95、全缺失 null、1/100 不能认证、95% 边界、94% 不足、中断原因保留、JSON null/转义），见 `probe-metric-independent-rerun.log`；测试源码、被测源码和程序集 hash 见 `probe-followup-source-manifest.json`。测试只调用纯托管报告方法，没有调用 Unity 原生 API。

## 已检查的关键边界

- Bootstrap 在 BeforeSceneLoad 设置全新 evidence 子目录，早于 GameApp.Awake；错误的显式验证参数也进入临时隔离目录后失败，不读取正式存档。普通启动没有验证参数时不创建探针。
- 正常回放使用 StartNew / Send 及真实 Pending 选择；每步核对合法回执、序号、阶段、分数、金币、剩余预算，并通过生产 AtomicSaveStore 重读磁盘与 UI ViewState 比较；只允许首个 NewRun，并要求最终第六关 RunWon 且 isDebug=false。此项验证了命令/呈现路径，不声称等价于人工点击全部 UI。
- 正常通关保存 hash 后，Debug 容量夹具另用独立目录；压力完成后再次验证正常存档 hash。夹具只在采样前注入一次，10 骰、7 槽、10 控制器、5 道具由生产 GameSession 验证。玻璃骰被锁，重复投掷不会令压力容量意外下降。
- 10 秒预热后至少 180 秒连续操作；每秒进行 Roll、Skip、Reorder、Pending、Clear，自动保存与实际 UI 刷新保持启用。最后补足首帧对齐引起的不足区间，没有把采样变为闲置场景。
- Guard 显式推进嵌套迭代器，使验证协程内异常落到失败报告。LateUpdate 采样另有异常守护；Unity Error/Exception/Assert 纳入失败，不把命令拒绝当作完成。
- UI marker 不是 willRenderCanvases；使用实际 BuildBatch/UpdateBatches/PerformUpdate marker。每个采样只消费一次，未调用帧计为零，避免重复读取稀疏 marker 的旧 LastValue。已核对本机 Unity 6000.6.7f1 XML：Recorder 默认按帧合计样本。
- 原始 CSV、marker 名称与单位、设备/API/质量设置、构建 GUID、配置/轨迹 hash 均留存；截屏编码、报告写入、夹具安装不在采样时间窗。功能回放与性能结果分开；缺测为 null，异常退出保留错误。

## 必须留给真实运行的检查

确认完整的 97 步回放与原轨迹一致；实际三个 RNG / 存档未污染；真实构建的指标样本数量、覆盖、CPU/GPU/P95/GC/UI 结果；180 秒压力截图与多层外观；最终 executable/source hash 绑定。源码中的截图调用不能证明文字可读性，也不能替代 720p/1080p/1440p 视觉检查、连续两次 Play 或真人体验测试。
