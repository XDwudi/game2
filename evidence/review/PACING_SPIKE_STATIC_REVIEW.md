# 帧节奏技术试验静态复核

spec_audit，2026-10-29 UTC。`FramePacingExperiment.cs` SHA-256 `a27040f1b01d4c7e9b939aadc768778ce2fd9d7feb19dda86216f274ba6b9aff`；只读，未启动 Unity，尚无实际试验通过结论。

仅显式 `--pacing-experiment <dir>` 启动，BeforeSceneLoad 用随机新目录隔离存档，拒绝与正式验证同开；新组件不改正式PlayerVerification采样。两模式相同空闲主菜单，3秒预热、各20秒，显式记录Windowed vsync0 target60与requested Exclusive1080/60Hz vsync1的实际模式/像素/刷新分数，成功和异常都恢复Windowed并核对原桌面Hz后退出。此运行是技术定位，报告明确不是180秒最大容量AC-PERF。

原始行保留Unity帧号、elapsed、LateUpdate间隔、frameStart/PresentCalled/FrameComplete原始值、CPU频率、status与focus。四种序列独立输出；首次部分Late间隔留空但行保留。无效/缺失/重复/逆序时清差值基线；某axis非递增也清其baseline。未见丢弃慢帧、缩短全量验收或选最好指标的逻辑。异常带阶段和可用记录，原模式恢复失败不会返回试验成功。静态未发现阻断此次小试验的问题。

判读限制：GetLatestTimings(1) 的相邻独特记录不保证原生帧相邻，可能跳过未取得的timing。代码处理显式gap，却无法凭timestamp独特性证明零隐性漏帧；之后需对实际帧数、elapsed、三时点跨度、缺失比例和分布交叉核查。PresentCalled是CPU调用Present，FrameComplete是GPU完成通知，均不单独等同物理scanout。不能由20秒空闲最低P95外推正式180秒压力通过；不能为通过调整原probe顺序/时点/阈值。

依据及替代候选见 `FRAME_PACING_RESEARCH.md`。实际结果到达后另作动态复核，当前仅静态可试验。
