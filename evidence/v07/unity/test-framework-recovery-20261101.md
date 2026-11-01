# 测试框架恢复与交易专项返工

2026-11-01，Unity 6000.6.7f1，独占 Editor40096。01:52 批次再次在 PlayModeRunTask:52 转抛异常后退出，CLI 留下 running 状态；没有有效 NUnit，因此不算通过。原始日志保留在 artifacts/cache/unity-v07-restart-1828.log（不发布含启动账户参数的完整日志）。

本次日志可见先以 Reload Domain disabled 进入 Play，随后 PlayModeTests 编译完成并发生域重载，才出现框架异常。取消挂起批次，完成 recompile，使用正常域重载（EditorSettings options None）后成功执行。还确认域重载后 GameView focusedWindow 为空，实际帧递增但 WaitForEndOfFrame 取图等待；通过 EditorWindow.Show/Focus/Repaint 恢复真实 GameView 后继续，没有绕过截图或伪造帧。

有效报告 nunit-20261101-020016.xml：48项，43通过、5失败、0跳过。旧183152四项失败均通过。五项新失败均为交易动效使用 null 合并运算符处理 Unity CanvasGroup 的 fake-null；TrackReveal 与 HideUntilFinished 改用 Unity 的 ==null 判断，再真实重跑六项交易专项。nunit-20261101-020116.xml：6/6通过，包含刷新、购买、扩容、物理强化、失败购买、真实保存失败后同候选重试。

未修改框架包、规则、测试断言或阈值。专项通过不能替代最终全量测试、截图审查、Player性能或人工验收；后续终局/引导源变更仍需受影响回归。
