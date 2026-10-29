# Player 视觉与动画探针扩展复核

独立审查：spec_audit；实施者：session_engine；2026-10-29 UTC。范围为 `Runtime/Verification/PlayerVerification.cs` 与 README；没有操作 Unity 或运行 Player。

结论：**当前冻结源码静态通过，真实运行待验**。SHA-256 `80254a9197e6a926e310e2a105caee8a36eb0e3324824bb5530a48a41af1e2f5`。未发现会使本轮播放器路径必然阻断或把无效视觉/动画证据判为通过的实质问题。

- `SetActualResolution` 使用真正 Screen.SetResolution，等实际宽高和窗口模式连续三帧一致；12 秒超时为失败，截图使用原屏幕像素并记录实际尺寸。每组恢复1080p；截图、编码、窗口改变均由 sampling=false 前置条件隔离。
- 通过真实 ShowDie / ShowBreakdown 创建模态，使用现有 ScrollRect 的位置取得底部截图；没有另造一套展示组件。检查 state、Pending、磁盘存档前后不变。截图本身仍待逐张视觉检查。
- 全10骰未锁夹具使用原 Debug 状态克隆和 MemorySaveStore，只移除副本锁标记；原压力 GameSession 引用在 finally 恢复，并核对序列化状态、存档 hash、memory.Saved==null。它不冒充正常获胜路径或7锁槽压力采样。
- 当前 Guard 的 186–195 行已在嵌套异常时按 LIFO Dispose 迭代器，并合并 cleanup 异常后失败退出；因此子截图/分辨率异常可以执行外层恢复 finally。审查早期沿用历史 Guard 记忆产生的疑虑已撤回，不是当前版本缺陷。`ProbeGuardCleanup` 的纯 C# 对照实验只验证旧模式与当前 LIFO 模式的迭代器语义，未调用 Unity。
- 动画检查读取实际可见 RollPivot，在0.32/0.45秒附近要求两次均位于0.3–0.5秒窗口且确有位移或旋转变化；绑定两段导入clip，未用 Editor 采样代替 Player。每个普通 Roll 完成/跳过后检查 Pivot局部TRS复位和真实faceId法线世界+Y，第二次Roll使用真实 reduced=0 路径。
- 每个普通命令前多让一帧，避免截图编码的长帧直接作为首个动画采样步长。并未修改规则随机、97步轨迹、180秒采样或16.7ms/95%完整性判据。

边界：两次中段TRS变化证明“确实在动”，不证明全片段连贯、首帧姿态连续或艺术品质。正常回放、三分辨率截图逐张检查、正确clip在Windows运行、180秒性能均须由root的新构建运行报告确认。编译成功与静态审查均不能关闭此前Player失败。
