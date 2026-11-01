# art0.7.0 显式逐帧录制工具

`Art07VisualRecorder` 是检查真实游戏表现的离线录制工具，默认不创建、不启动，也不会自动加载调试场景。输出固定 **60 帧/秒视频时间**的完整 Game View PNG；实际渲染、读回、压缩和写盘可以比 60 FPS 慢。**这不是实时性能采样，不可用它宣称 60 FPS 达标。**

## 集成和隔离

root 在 `GameApp.TickPresentation07` 的纯表现调用中使用：

```csharp
presentationPlayer07.Tick(DiceCraft.Verification.Art07VisualRecorder
    .PresentationDeltaTime(this, Time.unscaledDeltaTime));
```

未显式录制时，它原样返回传入时间；只在该 GameApp 的录制期间返回 `1f / 60f`，并记录每个实际 Unity 帧的调用次数。禁止将这个接口接到规则、RNG、存档或业务计时。`Time.captureFramerate = 60` 本身不能固定现有 unscaled 表现时钟，因此录制器还会检查 hook 确实被调用、每帧最多一次、实际步数与声明时间一致。依据：[Unity 6000.6 captureDeltaTime](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/time/capturedeltatime)。

必须先在独立临时存档目录中加载真实 Bootstrap。`GameApp.SaveDirectoryOverride` 要在 `GameApp.Awake` 前设置为唯一绝对路径，并保持到本段录制结束。当前存档路径必须位于 override 下，并且不在 `Application.persistentDataPath` 下。录制器不会替换现有 GameSession 或反射写入私有规则/UI 字段；没有隔离条件就拒绝启动。

现有 Play 模式可先设置 override，然后使用公开 `SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single)` 重新加载。场景加载完成后，通过 `UnlockDebug("XD is god")` 和 `EnterDebugWorkspace()` 打开调试工作区，再启动录制。已有 `Art07Fixtures.Apply` 同样可以完成这一公开入口。普通用户存档不参与录制；隔离目录中的普通检查点也会逐帧验证字节不变。

录制期间不要人工操作该 GameApp、切换分辨率、关闭 Game View、改变设置或同时运行另一场景写入工具。结束后退出这个隔离 Play 会话，再恢复调用方设置的 `SaveDirectoryOverride` / `SuppressStartupDisplay`；这两个隔离入口不是录制器修改的参数。

## 调用

每次调用使用新的空输出目录，禁止写入 Assets。以下路径是本工作区可用的绝对 artifact 示例：

```csharp
var app = UnityEngine.Object.FindFirstObjectByType<DiceCraft.GameApp>();
var recorder = DiceCraft.Verification.Art07VisualRecorder.Begin(
    app,
    @"E:\agent_workspace\gpt\game2\artifacts\cache\v07-recording-normal-01",
    DiceCraft.Verification.Art07RecordingMode.NormalHighest);
```

一次只录一段。检查 `recorder.Status` 或输出的 `recording.json` 结束后再启动下一段。

| mode | 实际输入与行为 |
| --- | --- |
| NormalHighest | G147，Normal，实际 Submit 一次 |
| FastHighest | G147，Fast，实际 Submit 一次 |
| ReducedHighest | G147，Normal + 减少动态，实际 Submit 一次 |
| ContinuousHighest | G147，第一次 Submit；其后同收据只读 Replay，默认共 3 次，可指定 2–10 次 |
| NormalSample | 黄金样板 C02/C41/C45/C52，Normal，实际 Submit 一次 |

速度与减少动态通过现有设置 Button 的公开 onClick 回调选择。其余样板通过 `Art07Fixtures.Apply`，计分通过 `Art07Fixtures.Submit`，连续段后续调用 `GameApp.ReplayLastPresentation07()`。没有手工制作收据，没有重新 Evaluate，也没有规则回调。G147 的最终得分 **967680**，精确倍率 **26880/1**，连锁 **4**，L5，目标1800000未达标。

每段包含 15 帧前导和 30 帧最终静帧。连续最高档两次演出之间保留 6 帧（100ms）安静间隔，所有声明的表现帧完整播放，不调用 Skip 跳过录制。只有取消、超时或失败清理时，才通过公开 SkipVisual 结束正在播放的纯表现。

## 证据文件与结果含义

- `recording.json`：录制方式、规则/美术版本、Unity/平台/GPU、运行与 Core 程序集 MVID、配置 SHA、可读取时的录制器源码 SHA、构建标识、尺寸、每个循环起止帧/时间步/状态与存档 SHA、设置恢复状态。
- `frames/frame_000000.png` 起：实际 `ScreenCapture.CaptureScreenshotAsTexture`，在 `WaitForEndOfFrame` 后读取，包含真实相机与 Canvas。没有用重绘 UI 或独立 RenderTexture 镜头冒充 Game View。
- `frames.jsonl`：每帧连续索引、真实 Unity frameCount、视频时间、实际墙钟时间、实际 unscaledDelta、表现时间步、得分/N/M/档位、收据 ID、游戏状态 SHA、调试存档 SHA、普通存档 SHA、PNG SHA 和采样图像统计。
- `fixture.json`、`state-before-fixture.json`、`state-before-submit.json`、`state-committed.json`：真实公开命令前后的独立调试状态，用于核对来源和计分。它们只来自当前隔离工作区。

`captured_pending_visual_review` 只表示录制完整性检查完成，**不是美术、闪烁安全、音频听感或平台验收通过**。这些 PNG 不包含声音；声音验收另行采集。root 还应把当前工作树源码快照绑定到该录制；程序集 MVID 与一份源码 SHA 不等于全工程来源绑定。Mac 需要在同版本 Mac Player 上独立执行。

灰度变化统计按每16像素网格计算：`highContrastAreaRatio` 是相邻帧归一化亮度差大于0.2的采样面积比例；`redBlueSwapAreaRatio` 是红蓝通道优势超过64并翻转的采样比例。这些是帮助找可疑帧的保守观察值，不是 WCAG 的完整闪光阈值实现，不输出“安全通过”。逐帧检查原PNG，必要时去除局部闪光并重新录制。

## 失败、恢复和批处理限制

缺少 hook、重复/跳帧、尺寸变化、空图、写盘/读回失败、状态或存档发生意外变化、声明时间内不结束，都会保留已经写入的证据并记录 `failed`。从不覆盖失败段或用后来的有效帧替代缺帧。

运行中的 Update 看门狗在 **12秒没有完整截图**时报告失败；完整任务墙钟上限15分钟。Batch Editor 中 `WaitForEndOfFrame` 可能不推进：此时失败报告会明确指出环境限制，改用可见 Game View 或真实 Player 运行，不能将0帧或只包含首帧的目录判为录制完成。若 Editor 整个主循环停住，内部看门狗也无法推进，调用方还必须保留外部进程/任务超时。

正常完成、`Cancel()`、禁用组件、销毁组件和捕获到的异常都会尝试恢复原 `Time.captureDeltaTime`（也恢复其对应 captureFramerate）、原三项速度/减少动态偏好及它们原来是否存在，并关闭本工具的固定表现时间覆盖。报告分别记录恢复结果；恢复失败不得算完成。不会保存一个替换过的正常游戏状态。

源级预编译仅检查接口和语法；真实截图、EndOfFrame推进、固定时间步和设置恢复，仍须由 root 在实际 Unity/Player 运行验证。
