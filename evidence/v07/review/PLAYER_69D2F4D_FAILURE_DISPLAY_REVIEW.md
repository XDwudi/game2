# 69d2f4d Windows Player 失败与独立显示专项复核

复核者 `/root/v07_shop_ui`；2026-11-01，只读查看真实产物，没有运行 Player/Editor、操作窗口或改动生产/测试/标准。本记录承接 [代表图观察](PLAYER_69D2F4D_IMAGE_OBSERVATIONS.md)，关闭其中“等待 runner 最终 summary”的时间边界，保留该早期记录原字节。

**严格 Player 失败；独立显示专项在既有 schema7 范围内通过。两进程不能拼接成完整通过。** 本轮 `strictWindowsAutomatedPassed=false`、`wholeReleaseAccepted=false` 正确。180 秒性能、完整 Debug24、独立进程显示恢复、Mac/M5 与人工体验仍未完成；没有发行放行结论。

## 身份与实际进程

输入为 `evidence/v07/player-69d2f4d-1218/serial-summary.json` 和两个原始报告，Windows 构建报告 `evidence/v07/unity/build-windows-20261101-121617.json`。两报告的完整内嵌 identity 与构建一致：源 `69d2f4dc3afa6387d443e827eb0b96892e9f2cfb`、art0.7.0 / p0.6.0 / schema4，Unity6000.6.7f1，Probe `p07-player-art-ui-20`，Windows GUID `cb6bf4330c9d4fccb72d4339f8cb3ca7`，实际 Player Runtime MVID `68c6be49-da3d-44f9-b85a-9c581262af28`。构建为 0 错误/1 警告；两进程均 WindowsPlayer/X64/isEditor=false，Windows10 19045、Ryzen5900X、RTX3080、D3D11、VG27AQ1A。

| 实际进程 | UTC 起止、结果 | 原始报告 SHA256 |
|---|---|---|
| strict-full，PID34772 | 12:17:44.578470—12:19:32.069991；exit1，failed / V07ControllerDrag | `289cee99f1d18d17ee8220244a64b5df6bf993e724d87a3294786c76960fcf89` |
| native-display-only，PID9820 | 12:19:32.072994—12:19:58.024503；exit0，display_passed / V07NativeDisplay | `eca41475713622f05a7efb6aad29047f61e3b9080c1382b60643a71b70b48e22` |

原始 process JSON 与 summary 逐字段一致；两进程均 `timedOut=false`、`ownedProcessTerminated=false`。raw 日志位于 `artifacts/cache/v07/player-69d2f4d-1218`，未删除失败或重新标记退出结果。显示报告 `unityErrors=[]` 不等于日志零警告：原日志仍保留 URP 不支持/剥离的后处理 shader 提示。

## 严格流程的完成段与失败

正常回放实际完成 **145/145**。本审查读取所有 JSONL，用既有 `compare_replays.py` 检查索引/连续序号、字段、规则 hash 格式、非Debug身份及每行保存/渲染核对标志，再把10个 trace 原有结果字段逐条与固定正常输入比对，差异为0。此处自比较只做记录完整性检查，**不是 Windows/Mac 跨端比较或独立重算 RNG/规则**。

`normalReplay.passed=true`，非Debug终态第8关 `RunFailed`，不是正常24关通关。记录 `rollAnimations=48`、`animationSkipsTotal=16`、`reducedMotionChecks=1`，手册只读20次；手册牌型/颜色、骰子详情、固定货架五页签、32目标、刷新/免费首次刷新、控制器拖拽/取消及冻结顺序等具名检查已记录通过。旧三个 probe 前置问题在本次对应段已跨过，不能据此跳过后续失败。

终止为实际 `ArgumentException: Missing decimal score`，从 `ExactScoreMath.Parse` → `ResultStatisticRows07:39` → `DrawRunResult07` → `ReturnNormalWorkspace` 抛出；报告和 Player 日志均保留异常，`unityErrors` 同时记录真实按钮 `OnPointerClick` 调用链。这不是只在报告状态中出现的失败。已有局部 `controller-drag-and-cancel=passed` 不代表整个 `V07ControllerDrag` 段完成；返回普通终局时发生产品 UI 异常，仍需修复与新同源重验。

`stressCompleted=false`、`performance=null`。runner 因失败早于 native/stress，不执行受限完整续跑和独立恢复，理由保留原文；不能称180秒性能不超标，也不能称恢复通过。报告虽已导出跨端 checkpoint，导出不是 Mac 导入或运行验证。

## 本次独立显示专项

专项 scope 明确是 `display_only_no_replay_or_stress`。四份 sidecar 与报告内 displayChecks / displayTransitions / inputDiagnostics / resolutionDiagnostics 完全相等；模式确认来自真实 Mode/ApplyDisplay uGUI 事件、pending/canConfirm/transitioning 状态和最终读回，不只凭 PNG 尺寸。

| 检查 | 本次实际结果 |
|---|---|
| ExclusiveFullScreen | passed，2560×1440 精确读回 |
| FullScreenWindow | passed，2560×1440 精确读回 |
| Windowed | passed，1920×1080 精确读回 |
| 非原生1280×720独占 → Esc | passed，恢复此前1920×1080 Windowed |
| 窗口1280×720、1920×1080、1600×1000、1920×800、1366×768 | **5项 passed**，均实际精确 Windowed 读回且可确认 |
| 窗口2560×1440、2560×1600、3440×1440 | **3项 not_supported**，原因为请求客户区尺寸超出当时显示工作区；没有对应已执行尺寸 transition，未冒称 passed |
| 15秒超时回退 | passed，903条逐帧记录，从 realtime9.275420105839 到24.364723005839，实测 **15.0893029秒**；最终恢复1366×768 Windowed、pending=false、transitioning=false |
| 窗口 Esc 回退 | passed，恢复1366×768 Windowed |
| crash-pending-recovery、物理失焦/拔屏 | **not_executed**；本专项没有这些证据 |

两次 Esc 都有 normal Dynamic InputSystem 消费记录：2次pressed updates、1次pressed edge、1次current-device edge，随后原设备与global settings恢复，游戏 state/disk 守卫不变。它们是探针专有键盘队列经过 `GameApp.Update` 的真实软件输入路径，**不是物理键盘、Alt-Tab或显示器断连证据**。模式读回也只限现有 Unity/DisplayPreferences 合同，不扩称独立 DWM/scanout 取证。

本次八尺寸为5通过/3不支持，和404历史候选的6/2不同；没有复制历史结果，亦不从本次通过推断此前宿主全屏失败原因。

## 图像、普通存档与构建守卫

此前实际打开16张 strict 原始图，详见代表图观察及其逐图 SHA。手册和骰子详情首帧仍在淡入、720首张3D预览为空的不足保留；后续清晰图不能倒填首图为可读。商店实图为实际3件货架，不冒称6件满货架。本人曾实施部分基础 UI，这部分属于本次 Player 集成观察，不能单独签署本人实现的最终制作质量。

本次新增实际查看3张 native 原图：`v06-display-mode-0.png`、`v06-display-mode-1.png`、`v06-display-1280x720.png`，显示确认面板与操作可见；首个模式图含淡入状态，未用它签静止正文品质。全部 **9张** native PNG 的 SHA、尺寸和报告条目已核，只有上述3张标记 actuallyViewed；未把未打开的其余6张写成人工看过。

独立比较外部 ordinary-before（12:17:05.800758Z）/after（12:21:01.365531Z）中 **10个普通存档文件名→SHA**，集合和哈希完全一致，时间包围两进程。包含 active/backup/legacy/rejected、display-settings/backup 与 normal-actions。该结论基于保存下来的前后快照，不声称重新获得过去的物理文件；也不是所有应用偏好或整个 LocalLow 目录的保证。summary 两个守卫布尔值均 true。

12:29:25 UTC 审查逐一重算已归档 Windows/macOS **606文件、513826869字节** 的SHA与大小，0差异；Windows runner守卫305文件均与保存构建一致，EXE SHA `3726509af94bdacbfe23f85da13109a7e70fb7d61913506e98690bd3208b2fd7`、Runtime DLL SHA `510dc2b44ca53bba0e3f912c405a9a0688d9f33fa65d76a7d706aab9d2067b61` 与summary输入一致。检查对象是失败候选归档，后续可变 Builds目录不用于追认本轮。Mac文件完整性不是Mac运行通过。

## 冻结与结论边界

`artifacts/cache/v07-player-69d2f4d-review/audit_final.py` 是本范围只读复核脚本，不替换正式验收器。其 `final-bindings.json` 记录本次全部输入哈希、原始报告、raw进程/日志、sidecar、三张实际打开/九张元数据图像、10文件守卫、606文件归档结果及该checkpoint原始probe副本；`metadataErrors=[]` 仅表示**失败/显示证据闭环一致**，不是“严格 Player 通过”。

结论：**本轮严格 Player 有确认缺陷，保持 REWORK；独立显示专项限域通过；关键图观察已完成。** 完整Debug24、180秒性能、独立进程恢复、新源修复后的Player、AudioMixer、Mac/M5与人工验收均不能由本记录签过。旧失败、首图不足和外传授权边界保留，没有修改源、标准或执行网络推送。
