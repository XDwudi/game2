# Windows 60 FPS 帧节奏候选研究

spec_audit，2026-10-29 UTC；仅研究、只读生产，不修改设置，不启动 Unity。**先等待 UI 池化的同条件真实结果，不立即实施任何候选。** 原 1080p、180 秒、60 FPS 目标、16.7 ms P95、95% 采集覆盖及全部原始帧均保留。

## 已确认事实

独立复制并运行 Session agent 的 CSV 诊断（输出限于本目录 `player04-diagnosis-independent.json/.log`），重现 899 个操作事件、次帧 GC 相关系数 0.8268，以及排除操作及随后补偿帧后的 8,800 帧 P95 17.0095 ms。动作名称来自节奏推断，不是方法计时；诊断分组没有用于替换全量 23.5467 ms 验收结果。UI 与全量存档仍有真实尖峰，不能全部归因于睡眠。

Player03 日志 `evidence/runs/player-console-03.log:36–37` 记录刷新率时间漂移和失真的 DXGI 帧统计；Player04 同类警告消失。驱动由实际 Player 日志确认 `32.0.15.6094`、D3D11 / RTX3080。它们不能证明 NVIDIA 控制面板是“强制关闭 VSync”，当前未读到该用户设置，也不建议改全局驱动设置去掩盖产品问题。

只读调用 `EnumDisplaySettingsW(NULL, ENUM_CURRENT_SETTINGS)` 成功：当前显示设备 **2560×1440 / 整数144Hz / 32bpp**。枚举 377 模式，1920×1080 有 60Hz（index320）、59/120/144Hz；具体证据 `current-display-mode-readonly.json`、`display-modes-readonly.json`。这是 Win32 当前显示设备查询，不是 Unity 游戏当前实际 presentation cadence，也不提供分数刷新率。查询方法符合[微软 EnumDisplaySettingsW 文档](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumdisplaysettingsw)。WMI 查询被访问权限拒绝，没有据此猜值或改变权限。

Unity 6000.6 明确说明桌面 `targetFrameRate` 是软件调度，存在微卡顿；推荐硬件 VSync。正值 vSync 会使 targetFrameRate 被忽略，输出频率为显示刷新率除以 vSyncCount。因此本机当前 144Hz 下直接用 vSync1/2/3 分别得到144/72/48，均不是60。来源：[Application.targetFrameRate](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/application/targetframerate)、[QualitySettings.vSyncCount](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/qualitysettings/vsynccount)。

## 候选优先级

| 方案 | 评估 |
|---|---|
| 先保留 vSync0 / target60，完成池化再跑 | 当前正确下一步；区分真实 UI 尖峰消失多少，不能预先保证 16.7 ms 通过 |
| 游戏自身 1080p / 60Hz 独占全屏 + vSync1 | 优先小范围原生节奏试验。没有提高帧率；不改系统全局显示/驱动偏好。需实际读取分数刷新率、模式、焦点和原始输出节奏。只能证明该真实运行模式，不能顺带签窗口模式 |
| 产品级 monotonic deadline / Sleep + 短 spin | 可作为窗口模式或 VSync 不可信时的可演进试验；有价值但需比“采样点变整齐”更强的实际输出证据，细则见下 |
| target61/62/120、过滤 idle 或尖峰、改 probe 时间点、修改 P95/180秒 | 不接受；会绕开原问题或改变验收 |

Unity 的刷新率参数只有独占全屏能兑现；请求不受支持的刷新率会退到最高值，分辨率变更直到帧末才应用。因此不能只看到 SetResolution 调用成功就当60Hz已落实，也不能让已有多分辨率截图流程把运行悄悄切回 Windowed。独占是应用运行方式的候选，应作为可恢复的实际产品选项验证，不为验收临时造特殊模式。[Screen.SetResolution](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/screen/setresolution)

## 绝对 deadline 节拍是否是合理修复

**可以是真实节奏修复，但当前没有运行证明，不承诺通过。** 应放在普通 Runtime 的独立帧调度组件，普通玩家与验证模式都走同一代码；不引用 probe、采样开始时间、测试 flag、压力动作相位或报告阈值。

- 以 `Stopwatch.GetTimestamp()` 和 `Stopwatch.Frequency` 为同一进程单调时基；目标周期精确为 frequency/60，使用整数商+余数累积或固定起点的帧序号，避免每帧整数毫秒舍入和 `now+period` 持续漂移。Windows 上 Stopwatch 的时基是 QPC；不要混入 DateTime/墙钟。[微软高精度时间戳](https://learn.microsoft.com/en-us/windows/win32/sysinfo/acquiring-high-resolution-time-stamps)
- 本帧实际工作先执行；只等待至预定 deadline，工作超期时不再额外等待。记录真实迟到、missedDeadline 和等待耗时，不伪造 Unity deltaTime，不补跑多次规则更新、不跳过提交/保存。遇到暂停、失焦、模式切换或长停顿，重新建立时基，避免恢复时高速追赶多帧；整段等待必须有有限上界。
- 等待可由粗 Sleep 加最后一小段 QPC 自旋组成；尾段预算需用本机实测确定，不在看验收结果后偷偷缩短目标周期。Sleep 超时只是变为可运行，不保证立即调度，所以任何此类方案都没有硬实时承诺；长期忙等一整帧会浪费核心和电力。`timeBeginPeriod` 不提高 QPC 精度，若使用需成对释放并评估功耗；不能用实时线程优先级或全局定时器修改挤占系统。[Sleep](https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-sleep)、[timeBeginPeriod](https://learn.microsoft.com/en-us/windows/win32/api/timeapi/nf-timeapi-timebeginperiod)
- Windows 10 1803+ 有 `CREATE_WAITABLE_TIMER_HIGH_RESOLUTION`，可减少粗等待不确定性；本机系统满足版本。它仍不是精确唤醒保证，P/Invoke、handle释放与回退会增加实现风险，应先比较小型可丢弃调度试验，不急于进正式代码。[CreateWaitableTimerExW](https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-createwaitabletimerexw)
- 不叠两个互相补偿的限帧器。若自有调度确实接管60，才可关闭引擎软件等待（target=-1），且必须在它停用/异常/未初始化时回退 target60，报告明确“有效目标60 / custom pacer”。这不是提高目标帧率，但若帧计数/Present 显示实际超过60、组件退出后失控或靠更高吞吐压低P95，就不接受。保留 target60 与自定义等待叠加的行为也必须实测，不能假设引擎不再额外等待。

### LateUpdate 的位置与测量诚实性

LateUpdate 保证在所有 Update 后，却不保证在所有其他 LateUpdate 后；仍早于后续 UI/渲染工作。可以按产品目的固定在“完成玩法和动画更新，提交本帧渲染前”的阶段，明确与其他生产脚本顺序，增加少量输入到显示延迟是代价。不能根据 probe 是否先执行来反复调整 ScriptExecutionOrder，不能在 probe 中 Sleep、手填 frame interval 或改原采样位置。[LateUpdate 官方语义](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/monobehaviour/lateupdate)

最关键风险：本项目原 interval 来自 probe 的 LateUpdate。若新 pacer 恰在它前面，固定截止点可能让 interval 整齐，但之后的 UI rebuild / 渲染 / Present 仍不稳定。即使这是自然生产顺序，也**必须补充**独立输出证据，不能只凭它认定真实画面更流畅。保留所有现有指标和失败门槛，同时记录 `FrameTiming.cpuTimePresentCalled` 的连续差值、frameStartTimestamp、重复/缺失、实际 frame counts 和 deadline miss；用 GetCpuTimerFrequency 转换，按帧时间戳去重，异常不替换成假值。必要时再用外部 PresentMon/ETW 证实显示提交；这些都是补充，不能选一个最好看的指标替换原P95。Present 调用时间本身也不等于物理扫描显示时间。[Unity FrameTiming 事件时间戳](https://docs.unity.com/en-us/engine/6000.6/manual/analysis/graphics-performance-profiling/profile-rendering/frame-timing-manager/get-timing-data)

## 判断边界与下一步

先跑池化版本的原180秒，保留全部 CSV；若总体尖峰仍高，继续真实方法标记定位 UI/Clone/JSON/Flush，不能用等待掩盖超过16.7ms的实际工作。若操作成本已低但节奏仍抖，优先受控60Hz硬件试验，再考虑独立生产节拍器。候选必须覆盖普通挑战、Debug、失焦/返回、暂停、模式恢复、动画0/0.15/0.9秒、输入单发，且最终同样180秒/10骰/7槽/10C/5I完整采样。严16.7ms对60Hz仅留下约33微秒余量，Windows和显示时钟不能静态保证达到；任何候选失败都应保留并分析，不自动放宽门槛。

研究结论：有两个合理技术方向；当前无可签署的帧节奏修复。没有运行 CPU 自旋基准，避免干扰 root 正在进行的真实 Unity 验证；没有修改生产、游戏设置、桌面设置或验收标准。
