# 4041766 Windows Player 显示专项独立复核

复核者：`/root/v07_shop_ui`。2026-11-01，只读复核，未启动 Unity/Player、未修改报告、探针、产品或门槛。

**本次 `native-display-only` 的限定结果通过；同批 strict 主流程仍失败。** 没有把显示专项提升为完整回放、独立进程恢复、180 秒性能或发行通过，也没有根据这一次成功推断此前同机显示失败的原因已经查明/解决。

## 身份与实际执行

- 证据根：`evidence/v07/player-4041766-1141`。
- 源：`404176661240a5d9834895416ed3719d6493e253`；`art0.7.0` / `p0.6.0` / schema4；Player 报告 schema `p0-player-verification-7`，Probe `p07-player-art-ui-19`。
- Windows 构建报告 `evidence/v07/unity/build-windows-20261101-113937.json`：Unity6000.6.7f1、Windows_x64、Succeeded、0错误/1警告，GUID **`bece24301d3647638e538bd931e59699`**。两个 Player 的内嵌 identity、环境 GUID 与该报告及 serial summary 相同。
- 两进程实际报告均为 WindowsPlayer / X64 / isEditor=false；Player Runtime MVID 均为 **`b35721f6-fca5-484c-9022-6ec29d3538e9`**。这是实际 Player 自报的已加载程序集 MVID，不将 Editor MVID 误当相同构建物。
- native-only PID **50416**，11:42:26.697523Z–11:42:52.208783Z，exit0、未超时/未强杀，`display_passed`、`display_only_no_replay_or_stress`。raw process JSON、Player 日志结束标记与报告一致。
- strict PID **3208**，11:41:23.606974Z–11:42:26.694521Z，exit1、未超时/未强杀，原失败报告保留。
- 本复核于 **11:48:07Z** 重算 summary 所列 **305 个 Windows 构建文件**，全部 SHA 相同；EXE、Runtime、build report、normal trace SHA 均相符。此核对只绑定这个时点的该构建，后续新源码/构建须另验。

## 显示检查与边界

检查了报告中 16 条 displayChecks、15 组 native transition 逐帧记录、两组真实 InputSystem 诊断、旁文件一致性，并实际打开全部 **10 张**显示专项 PNG。图片用于确认实际界面与尺寸；模式成功依赖原生读回及确认流程，而非仅看截图。

| 检查 | 实际结果 |
|---|---|
| 独占全屏 | `mode-ExclusiveFullScreen=passed`，2560×1440；实际 Mode0 / ApplyDisplay uGUI 操作后，transition 末帧为该模式、pending=true、canConfirm=true、transitioning=false；真实 ConfirmDisplay 后记录通过。 |
| 无边框 | `mode-FullScreenWindow=passed`，2560×1440；同样有真实 UI 前置、可确认读回和保存确认。 |
| 窗口 | `mode-Windowed=passed`，1920×1080；确认流程与模式读回一致。 |
| 非原生独占模式与 Esc | 1280×720 ExclusiveFullScreen 可确认，随后 InputSystem Escape 回到原 1920×1080 Windowed，pending=false、transitioning=false。 |
| 六个窗口尺寸 | **1280×720、1920×1080、2560×1440、1600×1000、1920×800、1366×768 全部 passed**，各有精确 Windowed 读回、可确认状态和对应原生尺寸 PNG。 |
| 2560×1600、3440×1440 | **not_supported，不是 passed。** 记录原因为请求客户区超过当前显示器工作区；当时显示器为 VG27AQ1A、原生2560×1440。探针按真实 workArea 判断，没有执行这两种尺寸的切换，未伪造对应大尺寸 PNG。此结果沿用既有校验器允许“明确不支持+原因”的边界，不能声称这两种尺寸在本机已成功。 |
| 15 秒未确认恢复 | `timeout-candidate` 成功到1280×720 Windowed。`timeout-revert` 有 **903 条帧记录**，realtime 8.97646550362812→24.0095124036281，共 **15.0330469秒**；前14秒观察均 pending，终态恢复原已确认1366×768 Windowed，pending=false、transitioning=false。未改计时器或使用短路模拟。该 transition 的通用 status 仍是 `requested`，最终结论来自真实终帧、调用者严格断言及独立 `15-second-timeout-revert=passed`，不把通用标签当额外失败/通过。 |
| 普通窗口 Esc 恢复 | 1280×720候选→1366×768 Windowed；终态无 pending/transition。注入 Keyboard 事件经实际 Dynamic update 和 GameApp.Update，dynamicPressedUpdates=2、pressedEdge=1、currentDeviceEdge=1；不是真人按键或物理失焦证据。 |
| 崩溃 pending 恢复 | **not_executed**，没有独立 writer/reader 进程证据，不以本次同进程回退替代。 |
| 物理 Alt-Tab / 拔显示器 | **not_executed**，不外推硬件/焦点恢复。 |

上述“原生模式读回”按现有探针的 Unity Screen / DisplayPreferences.ReadActual 合同解释；不额外声称取得 OS 合成器、扫描输出或硬件独占的独立取证。

10 张图均可读出实际确认层的模式/尺寸及“15秒内确认”文字，保留/恢复按钮可见。旁文件 `v06-display-checks.json`、`v06-native-display-transitions.json`、`v06-input-diagnostics.json`、`v06-resolution-transitions.json` 与报告内记录完全一致。文件中的 v06 名称是既有兼容命名，条目实际绑定 art0.7.0/revision19。

`display-state-isolation=passed`，两条 Escape 记录均 stateUnchanged/diskUnchanged、原输入设备恢复、全局输入设置不变。serial summary 的普通存档与 build 守卫均为 true；**普通存档前后哈希映射未在 summary 持久化，本复核没有声称独立重算不存在的前快照**。报告 unityErrors=[]；原 Player 日志仍保留四条 URP 未支持/被裁剪的后处理 shader 警告，未把“零错误”写成“零日志警告”。

## strict 失败与未完成范围

`strict-full/player-20261101-114126-2332a3de/player-report.json` 在 **NormalReplay** 结束的覆盖断言失败：`Full animation, reduced-motion and skip paths were not all exercised.`

已独立读取 `normal-replay.jsonl` 全部145行，与冻结 normal trace 的145步逐条比较原字段（命令/序号/阶段/提交分/累计分/目标/金币/关卡/投掷与提交预算）相同；记录自身的连续序号、规则hash格式、savedStateMatches/renderedStateMatches 亦符合既有工具检查。终态为非Debug的 **第8关 RunFailed**。这只能证明已经记录的命令段一致：报告仍为 `normalReplay.passed=false`，计数为 rollAnimations=1、animationSkipsTotal=0、reducedMotionChecks=0，不能据145行齐全提升成完整正常流程通过。

handbookPureReadChecks=20，但 Debug24、stress、performance、独立进程显示恢复均未完成；两个进程均 `stressCompleted=false`、`performance=null`。runner 没有继续受限完整续跑或恢复，`strictMainPassed=false`、`strictWindowsAutomatedPassed=false`、`wholeReleaseAccepted=false` 均保持。后续 probe 计数修正不能覆盖此原失败记录，也不能继承本次显示证据为新构建已验。

逐文件 SHA、报告/日志/真实 PNG、逐组显示末帧、timeout 和命令段比较结果冻结于 `artifacts/cache/v07-player-404-display-review/bindings.json`。只读审计脚本与404检查点原探针字节副本一并保存；没有重写正式校验器或执行新的实际测试。
