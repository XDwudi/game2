# ProfilerRecorder 持续采样研究

只读研究，spec_audit，2026-10-29。未运行 Unity 原生 API，不声称采集修复已通过。

本机 `E:/unity/6000.6.7f1/Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.xml` 68874–68890 支持 `CopyTo(List<ProfilerRecorderSample>, bool reset)`；69002–69009 明确 Reset 清除样本且停止采集，必须随后 Start 才恢复。[Unity 6000.6 Reset 官方文档](https://docs.unity.com/en-us/engine/6000.6/script-reference/unity/profiling/profilerrecorder/reset) 同时说明 Count归零、WrappedAround清除，Reset之后不能把LastValue当成此前帧结果。[CopyTo 文档](https://docs.unity.com/en-us/engine/6000.6/script-reference/unity/profiling/profilerrecorder/copyto) 的reset选项只承诺重置，未保证继续运行。

建议每帧复用预分配 List，记录 WrappedAround 后调用 CopyTo(list, reset:true)，若 !IsRunning 就 Start，并验证下一帧采集继续。只统计复制出来的样本，禁止复用旧LastValue。相较分开的CopyTo(false)+Reset，单次复制清除减少两个调用之间丢样的窗口；是否原子、是否保留running属于原生实现细节，公开C#绑定不能证明，必须在当前Player短测再正式跑。[Unity 官方 C# 绑定](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Profiler/ScriptBindings/ProfilerRecorder.bindings.cs) 的 CopyTo 只是将reset传入原生方法，该master不是本机补丁的严格源码版本。

保留 SumAllSamplesInFrame；UI每帧合计Value与Count同时记录。不每帧重新创建Recorder，不在帧内新建数组/List。采样起点先排空warmup；持续逐帧读取理论上每次0或1个完成样本。若一次>1，应记录批次/错失帧，不能将多帧sum伪称一个普通帧。只有Valid、IsRunning且无wrap的空采样可视为本帧未调用0；失效、停止、wrap为缺测。Stop/Start本身不清除旧数据，因此不能替代Reset或CopyTo(reset:true)。

原95%覆盖与16.7ms不变。Player02的286832帧/180.008秒、UI覆盖29.79%、GPU最大1.31637e13ms证明原证据不完整；可见窗口新跑不能复用后台结果。异常GPU值应原样留在raw CSV和异常计数中，并强制gaps；不能偷偷删掉后称完整通过。建议在正式180秒前验证UI稀疏调用→空帧→再次调用序列，确认没有旧值复用，也不会Reset后永远停采。
