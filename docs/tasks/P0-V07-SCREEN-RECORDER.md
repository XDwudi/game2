# v0.7 实际界面截图记录器

状态：实现与独立 C# 预编译完成；实际 Unity/Player 执行、截图完整性与视觉审查由 root 完成。没有预填截图结果，不代表22屏全部通过。源码为 `DicePrototype/Assets/Game/Runtime/Verification/Art07ScreenRecorder.cs`，仅显式 Begin 启动，不自动安装或修改启动流程。

## 启动接口

同一工程串行运行，不能同时启动 Art07VisualRecorder。必须在 GameApp.Awake 前设置全新绝对 `GameApp.SaveDirectoryOverride`，位于普通 persistentDataPath 之外；普通与调试存档均须不存在，Session.State 为 null，停留新建主菜单。使用新 GameApp/新隔离目录拍每个配置，不复用正常玩家存档。证据目录位于 Assets 外，并与存档目录互不包含。

主协调者先真实设置 Game view/Player 尺寸，再调用：

```csharp
var capture = DiceCraft.Verification.Art07ScreenRecorder.Begin(
    app,
    @"E:\agent_workspace\gpt\game2\evidence\v07\screens\run-1080-unique",
    DiceCraft.Verification.Art07ScreenPreset.Normal1080);
```

记录器不调用 Screen.SetResolution，也不改变系统全屏状态。启动及执行期间实际 Screen.width/height 必须与请求相符。文字缩放通过设置页真实控件选到目标值；命中检查有真实 EventSystem 首命中验证，滚动到不可见控件后再触发，不绕过遮挡调用后台规则。

| Preset | 必须先设置的实际画面 | 文字 |
|---|---|---|
| Normal1080 | 1920×1080 | 100% |
| Small720Font150 | 1280×720 | 150% |
| Normal1440 | 2560×1440 | 100% |
| Aspect16x10 | 1920×1200 | 100% |
| Ultrawide | 2560×1080 | 100% |

自定义可调用同名重载，传入 `Art07ScreenOptions { name="custom", width=3440, height=1440, fontPercent=125, seed=20261031, settleSeconds=.35f }`。尺寸必须先真实生效；文字仅100/125/150，稳定等待0.2–2秒。它不模拟移动端、不伪造其他系统平台或物理显示尺寸。

返回组件公开 `IsRecording`、`Status`、`Error`、`OutputDirectory`、`Cancel()`。取消或出错保留已经写出的图和报告。每次操作/截图都会刷新 screen-report.json；无进展90秒失败，表现40秒不结束失败。录制依赖真实 Game view 渲染及 WaitForEndOfFrame：切走Game视图、最小化或无渲染批处理不可算成功。结束后销毁此次隔离测试App；它恢复持久偏好键的原有存在性和值与全局FontScale，但不会通过反射改回测试App内部的字体字段。

## 实际操作覆盖

正常部分：空主菜单→新局种子弹窗→真实NewRun→关卡预览→StartStage→首掷前→真实Roll→一颗待选→真实ConfirmLocks→暂停→三个设置页/辅助页底部→牌型与颜色手册→骰子右键详情→逐个选六物理面及概率分组。如果首局引导实际存在，另拍其第一步；已有偏好不强制伪造引导。

辅助设置底部实际输入 `XD is god`，先拍口令输入，再触发真实确认按钮并拍组合试验入口。此时要求仍为普通对局且状态和存档不变。开始录制时入口必须尚未解锁；记录器不调用后台 UnlockDebug 冒充口令流程。

随后以公开 EnterDebugWorkspace、Art07Fixtures.Create 和 ApplyDebugScene 命令进入隔离场景：最大10骰/8容量/10控制器/5道具骰池与已锁桌面、倍率预览、控制器/道具详情。每次应用后都点击真实“重新读取当前”，刷新候选表单后才返回桌面。最大已锁场景明确将 debug target 设为1、初始 score 设为0、两种报价扩容购买计数设为3。分数由真实 Submit 计算；历史明细、EndStage结算和Continue商店均来自该真实提交，**不是正常难度/通关成绩证据**。商店必须真实生成6控制器+5道具报价，五个下方分类分别截图。

Debug32场景通过同一公开调试命令构造32骰、32控制器、32道具、32容量，再打开真实七个导航位置。重新打开编辑器后再点“重新读取当前”，验证实际控制器/骰子/道具数量文字与当前物理面、点数，拒绝拍摄旧候选表单。每屏 `debugCandidateIndicators` 记录这些真实 UI 值，与右栏已应用数量分开。截图覆盖各页顶部/所选锚点，不冒称32行全部逐项验收。返回普通工作区后原普通存档SHA必须与进入Debug前一致；最后拍有继续对局的主菜单。

S01、S02、S03、S04、S05、S06、S07、S08、S09、S10、S12、S14、S15、S16、S17、S20、S21仅记 `partial_capture_pending_review`；S19仅实际出现时增加。每个标记带确切截图ID，不能把部分状态映射当作整个S屏通过。S11替换/出售/刷新动效、S13目标选择、S18终局、S22错误恢复本批明确 `not_captured`，其余页面未走的风险、排序、耗尽、显示回退等分支仍未覆盖。保存失败不人为制造；真实写入失败则停止并保留错误。

## 证据内容

- `screen-report.json`：schema art07-screen-recording-1，实际Unity/平台/GPU/API、BuildIdentity原文、运行程序集MVID、配置/记录器源SHA、请求尺寸与实际环境、动作及22屏覆盖表。最终最多为 `captured_pending_visual_review`，不写accepted/pass。
- `screens/NNN-id.png` 及 `.png.json`：WaitForEndOfFrame后抓取真实合成帧，校验实际纹理大小/PNG字节与SHA，保存非黑/亮像素及量化颜色数。每屏绑定Unity/resolution/fontScale/canvasScale/显示模式/quality/reduced、StateHash前后、存档SHA前后、普通存档SHA、真实数量和phase。
- 每屏 controls：全部激活 Selectable 的层级路径、屏幕像素边界、RectMask/Mask裁切后可见区域、是否可交互/首命中、是否达到40物理像素、TMP文本/容器尺寸/首选尺寸/overflow。滚动区屏外行标为不可见；不将其自动误判为缺陷，也不把边界存在当作可读性通过。
- 每屏 textRows（2026-11-01起的新录制）：额外记录所有激活TMP标签的字符布局数、非空白字形数、已渲染字形数、完全落在容器/屏幕/父级遮罩内的字形数，以及overflow/truncated/cull。用于发现仅检查字符串会漏掉的整行空白或裁切；屏外滚动内容和被弹窗覆盖的背景不自动判错，最终仍核对实际PNG。旧截图没有此字段，不补造旧记录。
- `fixture-*.json` 与 `*-applied.json`：真正送入调试命令的参数及已应用状态，明确场景来源。浏览/详情操作必须保持完整GameSession.StateHash与存档字节；合法NewRun、Roll、Confirm、Submit、结算与fixture命令记录其前后差异。

捕获像素完整性失败、按钮遮挡、命令拒绝、实际保存失败均停止并保留现场；不会忽略后续未到达页面或补造截图。该工具不是性能测试，不更改帧率或演出固定时钟；连续60fps动效证据由 Art07VisualRecorder 独立提供。Root逐屏实看截图、检查溢出/交互，并交独立审查；人工风格与体验仍由用户决定。

## 已做与未做

对真实 Unity 6000.6.7f1 模块、当前 Core/Runtime程序集的独立 dotnet Library 预编译通过。未执行编译产物，未启动Unity/Player，未生成任何运行截图或宣称界面通过。Root负责首批1080运行及后续配置；生产UI未在本任务中改动。
