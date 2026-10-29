# P0-DICE-PRESENTATION：REVIEW

范围：仅 `DicePrototype/Assets/Game/Runtime/DiceTable.cs` 生产代码及本目录证据。父任务输入 v0.4，六面法线与原有 MakeFace 普通点阵几何未改变。

## 变更

- 真正可见层级为 `dieId/RollPivot/FaceOrientation`，六面位于 FaceOrientation 下。`GetFaceOrientation(dieId)` 返回此真实对象，测试没有代理副本。
- 新 `public AnimationClip[] rollClips`。按root在真实Editor读取的绑定，renderer-free sampler提供空根、RollPivot、FaceOrientation、DiceBody与6个face marker；从导入AnimationClip.SampleAnimation采样RollPivot世界运动，去掉起始轴变换，再将delta应用到可见RollPivot。导入动画不能覆盖布局或逻辑面。
- 当前GameApp同步顺序为“成功事务→Refresh/Display→BeginRoll”。Display保留此前实际姿态；BeginRoll在同帧先设置新目标，再以父节点逆补偿保持原世界姿态。0.1s衔接、0.7s导入翻滚、0.1s落定；0.15s快模式缩放时间轴。FinishRoll或时长0明确父节点位置0/旋转identity/scale1，物理面精确朝上。
- 不调用任何随机源。两动画用稳定createdSerial轮流选择；表现只读DieInstance/DieHandState，不改变核心状态。
- 面网格按6点数×2金层×3纹理缓存，最多36个；红蓝通过复用MaterialPropertyBlock独立设置。重复Display不再创建/销毁面网格或属性块。
- 整骰GameObject按实例ID复用；每帧布局结束后回收已隐藏视图，可保留最多10个备用；避免Hide/Display过程中提前复用打断旧姿态。
- 提供 `ShowPreview(die, uiCenter, rotation, size)` 与 `HidePreview()`，通过同一主相机展示独立永久六面，只读且不参加投掷。模态UI需要为该世界坐标区域留透明窗口；其他UI集成不在此切片内。

## 绑定与复核

root在ProjectSetup为table.rollClips绑定Roll_A.fbx/Roll_B.fbx内非`__preview`片段（实际均名Scene，length约0.7）。生产场景应断言ImportedRollClipCount==2；未配置的孤立测试可用确定性几何运动fallback，不能以fallback声称Blender动画集成通过。

core_rules已将旧硬编码路径查询改为GetFaceOrientation，保持所有正常/遥控法线和点数断言。建议实跑补充：旧姿态到BeginRoll同帧无跳转、FinishRoll父TRS归零、0秒立即落定、两骰共享点阵Mesh且多次Refresh缓存不增长、旧场景卸载后无采样rig残留。

## 实际验证

- .NET SDK 10.0.403，target netstandard2.1，C#9，引用本机6000.6.7f1 UnityEngine.CoreModule/AnimationModule及现有DiceCraft程序集，离线编译0警告0错误。项目与日志见 runtime-compile/DicePresentation.csproj、dice_runtime_compile_02.log。首次尝试因为默认CLI_HOME写权限失败，原日志保留，不记作编译通过。
- root首次真实PlayMode发现原实现把MaterialPropertyBlock放在字段初始化中，Unity反序列化阶段禁止原生对象创建，导致全部12项Setup失败（evidence/unity/playmode-01.json）。已修复为DisplayInternal主线程首次调用时惰性创建，并一并移除Shader.PropertyToID静态调用；其他字段仅托管集合/数组和值类型。未修改测试预期、吞异常或删除失败证据，等待root重新导入实跑。
- 本实施者未调用UnityEditor；离线编译不能证明动画实机采样或性能通过。最终以root实际运行与独立测试证据为准。

## Windows Player 差异修复

- root 的首轮 Windows Player 回放在第 2 步投掷失败：非 Legacy 片段在 Player 中要求被采样对象含 Animator，Editor 则允许缺失。失败证据保留在 `evidence/runs/player/player-20261029-003731-7c15217a/player-report.json`，不能由先前 Editor 通过结果抵消。
- `EnsureMotionClips` 创建完整采样层级后，现在添加 Animator，关闭 applyRootMotion，设 AlwaysAnimate，保持无 Controller，由显式 SampleAnimation 控制采样。现有 Generic 导入和片段绑定不需要修改；没有捕获或忽略 Unity 错误，也没有切到占位运动掩盖失败。
- 本机真实 Unity 程序集离线编译 0 警告、0 错误，日志 `dice_runtime_compile_player_animator.log`。尚不能据此宣布 Player 修复通过。
- 必要回归是重新构建 **Windows Player**，加载生产场景及两段非 Legacy 导入片段，按正常时长执行投掷并检查全过程零 Unity error、运动中 RollPivot 的 TRS 实际变化、落地目标面法线朝上且 RollPivot 精确归零；再执行跳过动画和减少动态时长 0。现有 PlayerVerification 的错误收集已捕获此次差异，必须保留真实 Player 运行这一道门禁，不能用 Editor PlayMode 或零时长投掷替代。
