# P0 Windows 帧调度

状态：build10 / Player07最大场景已完成独立技术验收，本轮Windows P0采用此策略；完整结论见 `../../evidence/review/FINAL_REVIEW.md`。输入为P0 v0.4第30节、AC-PERF和既有播放器探针；没有改变玩法、目标帧率、16.7ms探针门槛、180秒场景或采样覆盖要求。

## 问题和证据

当前Windows显示器为2560×1440/144.006Hz。原生软件60帧限制在空闲菜单的LateUpdate、FrameStart、PresentCalled、FrameComplete四个时间轴P95均约17ms。请求1080p/60Hz独占模式在本机驱动回退成144Hz无边框，BitBlt路径也未落实；两次试验均确认恢复原桌面设置。

Player05最大场景还存在事务与刷新成本。界面池化与显示快照缓存已减少分配；后续存档优化只移除旧档验证时无用的完整恢复克隆，每次仍读取磁盘并执行原有校验。帧调度不能替代这些真实工作优化。

## 实现选择

Windows Player成功安装通用 `FramePacer` 后接管节拍，停用原生软件限速以避免双重等待；未安装时保持Unity的0 VSync / target60回退。退出、移除或宿主销毁恢复原生设置。Editor及其他平台不启用该调度器。

等待点在PreLateUpdate首节点，即游戏Update/普通延迟协程之后、原生动画和LateUpdate相机之前。按Stopwatch单调时钟，每次实际释放后重新安排完整的1/60秒；晚帧不追赶，不通过短补偿帧提高吞吐。先粗睡眠，仅剩余不超过1.5ms时自旋；暂停/失焦用17ms粗睡眠且重置期限，恢复时重新建立节拍。算法不读取验证开关、游戏阶段、命令、随机数或采样边界，不更改Unity时间和规则。

代价是前台每帧可能占用最多约1.5ms尾段忙等，且晚帧重锚会使平均帧率略低于60。等待会影响输入到呈现的延迟；本原型不是低延迟竞技游戏，但仍需真人操作判断，不能以测试数字代替体验。

## 验证边界

`spike-03-deadline`每模式3秒预热/20秒采样。调度模式平均周期约16.671ms；LateUpdate P95 16.6742ms，FrameStart 16.8128ms，PresentCalled 16.8051ms，FrameComplete 16.8281ms。四轴都有改善，仍保留后段抖动；空闲比较不算AC-PERF通过。原始CSV与独立复算见 `evidence/runs/pacing/` 和 `evidence/review/`。

正式验收继续保留原LateUpdate判据，三个新增轴用于交叉检查改善是否真实，不挑选最好指标，也不在事后新增三个同门槛硬判据。PresentCalled是CPU调用，FrameComplete是GPU完成通知，均不等同物理显示扫描；当前未测量物理scanout。实际结果须同时报告正常流程、180秒最大场景、CPU/GPU/GC/UI、所有时间轴与缺失率，最后由独立复核判断。

官方依据：[Unity targetFrameRate](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/application/targetframerate)、[PlayerLoop.SetPlayerLoop](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/lowlevel/playerloop/setplayerloop)、[Windows Sleep](https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-sleep)、[高精度时间戳](https://learn.microsoft.com/en-us/windows/win32/sysinfo/acquiring-high-resolution-time-stamps)。具体取舍及独立审查见 `evidence/review/FRAME_PACING_DECISION_REVIEW.md`。

## 正式场景中的返工

build09 / Player06完成180秒且原LateUpdate判据通过，但Present/Complete P95约18.75ms，协调者拒绝仅据exit0放行。独立分析找到5次/秒刷新后的长短呈现摆动；本机TMP源码确认布局和网格队列在Canvas.willRenderCanvases才处理。

build10 / `f060098`保持调度和探针不变，只让每次GameApp.Refresh统一完成Canvas/TMP更新。Player07相同场景180.0146秒：LateUpdate P95 16.6807ms，FrameStart16.9825、Present16.9369、Complete16.9452ms，三类纯刷新的>18ms呈现摆动已消除。平均周期16.7440ms和最大帧24.5388ms仍如实保留；长历史事务慢帧仍在，不能称所有帧都已稳定在16.7ms以内。最终接受范围以独立 `FINAL_REVIEW.md` 为准，硬件与各项指标见 `../DELIVERY.md`。
