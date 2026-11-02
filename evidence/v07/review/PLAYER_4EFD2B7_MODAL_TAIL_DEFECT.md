# 4efd2b7 实际 Player：关闭调试面板时尾影字形变成矩形

**结论：REWORK。** 这是本次实际 Player 图中可见的短暂产品视觉缺陷，不应仅通过延迟截图掩盖。复核者 `/root/v07_run_review` 只读分析，未修改产品或测试，未新启动 Player/Unity。缺陷编号 **P07-MODAL-TMP-01，P2**。

输入：源码 `4efd2b7da90f369411ba07c836f345b52c2f4a31`，Windows 构建 `build-windows-20261102-010055`，GUID `3e9ef773caed4efe96fc819ed40c3e5a`，actual Player Runtime MVID `3cb47bf2-d3d6-408d-8b5c-ed0fef87a06c`，probe21。

## 实际表现与范围

真实截图：`evidence/v07/player-4efd2b7-0103/native-deferred-independent/player-20261102-010504-4ac1be25/v06-debug-32dice.png`。复核者实际打开完整 PNG：关闭调试工作区后的半透明退出尾影仍覆盖试验桌；尾影标题、按钮和右侧内容中的文字显示为一排实心矩形，背景桌面的活动文本则正常。后续同批 `v06-remote-target32-bottom-1280x720.png` 字形正常，支持缺陷位于退出过渡而非整个动态字库永久损坏；后续正常图不消除已看到的错误过渡。

截图 metadata：1920×1080，accepted=true、contentValid=true、attempt=1、requiredConsecutiveValidFrames=1，phase=Selecting、actionSequence=1、isDebug=true、modalOpen=false。逻辑模态已关闭与视觉尾影仍存在并不冲突。该 accepted 只验证非空、色彩数量与尺寸；`PlayerVerification.cs` 的 Capture/AnalyzeCapturePixels 明确不是美术/可读性评分。

## 具体代码路径

1. `PlayerVerification.V06.cs:398` 通过真实 RunDebugScene 按钮关闭面板，等待一帧；399 行开始截图。Capture 再等待一帧和 WaitForEndOfFrame，未等待退出尾影完成。
2. `GameApp.Debug.cs:168` 将 RunDebugScene 绑定到 CloseModal；`GameApp.Modals.cs:62` 调用 `modalTail07.CaptureExit`，随后同步清除逻辑模态和原 UI。退出默认约 0.12 秒，截图可以落在该窗口。
3. **`UI/Art07PanelMotion.cs:88-89`** 对 TMP_Text 选取 fontSharedMaterial，但紧接着仍执行 `entry.renderer.SetTexture(graphic.mainTexture)`。TMP 文本继承 Graphic 的纹理读取可返回白纹理，这会覆盖材质实际字形 atlas，令本来表示字形的四边形成为实心矩形；TMP_SubMeshUI 也未单独使用其 sharedMaterial/atlas。
4. 同工程 `Runtime/Presentation/Art07TransactionMotion.cs:56-60` 已对相同类型问题做正确区分：TMP_Text / TMP_SubMeshUI 使用对应字体材质的 mainTexture，其余 Graphic 使用自身 mainTexture。本处 modal 路径尚未纳入该修复。

这是实图表现与静态材质/纹理路径相互支持的定位。未运行新的单点复现实验，不能声称已验证所有面板、字体和缩放都会重现；本截图已足够否定该具体过渡的视觉正确性。

## 修复与回归要求

在保留 UV0.w、顶点色、遮罩裁剪、透明度、原关闭时长、同步逻辑关闭及输入/存档行为的前提下，最小修正尾影 TMP_Text / TMP_SubMeshUI 的材质与 atlas 路由。应在退出尾影仍播放时取得真实渲染证据，检验字形与关闭前匹配，不只检查 tail 数量、非黑像素或等待其消失；覆盖至少本次大工作区文字、实际 fallback 子网格及减少动态路径。关闭后无活跃尾影残留、输入不被尾影阻挡、状态/保存不变仍须保留。

稳定终态截图可以补充，但不能替代修复错误过渡。修复后重新绑定受影响源/程序集，运行真实针对性测试与退出中间帧实图，再由独立审查关闭本缺陷；受影响最终构建/包证据须更新。

适用依据：ART_SPEC 第 03 章中文清晰可读与动态文本要求、界面共用组件与退出动效规格。未新增玩法或改变质量门槛。根任务已确认据此返工；本报告不把过渡截图泛化为整套界面长期损坏，也不把既有 Editor 1013/155 通过等同于本路径的字形通过。截图、报告与源 SHA 见 `PLAYER_4EFD2B7_REVIEW.bindings.json`。
