# NativeMinimal 可丢弃隔离试验

输入版本：Unity **6000.6.7f1 (b22767f04f04)**。目的仅是将同机原生独占切换与 GameApp、FramePacer、URP 分离，不能替代 v0.6 正式显示门禁。当前仅准备文本，**未编译、未运行**；主协调者负责实际结果。

源副本和逐文件 SHA 见 [source-manifest.json](source-manifest.json)。还原时将此目录下的 Assets、Packages、ProjectSettings 三个目录复制至工作区 `artifacts/cache/v06/native-minimal-project`。不要复制到 DicePrototype；不要把生成的 Library/Temp/构建内容加入 Git。本机可直接打开已准备好的同名项目，缺少的默认 ProjectSettings 与场景由独立 Editor 初始化/构建产生。

## 构建入口

- `BuildNativeDisplayProbe.BuildWindows`：Development，输出 `artifacts/cache/v06/native-minimal-build/NativeMinimal.exe`。
- `BuildNativeDisplayProbe.BuildNonDevelopmentWindows`：BuildOptions.None，输出 `artifacts/cache/v06/NativeMinimalRelease/NativeMinimal.exe`。

两个入口使用相同的空场景、运行脚本、Mono、Builtin 与 D3D11。除 BuildOptions、输出目录和报告名称外没有实验参数差异。每次报告独立写到 `evidence/v06/display-minimal/build-development-UTC.json` 或 `build-non-development-UTC.json`，记录真实 BuildSummary、GUID、输入 SHA 和错误；不是正式 source gate。

主协调者可用其现有 Unity6000.6.7f1 执行对应静态方法。命令形状如下，Editor 路径由实际安装确定：

```text
<Unity.exe> -batchmode -quit -projectPath <workspace>/artifacts/cache/v06/native-minimal-project -buildTarget Win64 -executeMethod BuildNativeDisplayProbe.BuildWindows -logFile <workspace>/artifacts/cache/v06/native-minimal-dev-editor.log
```

这份最小工程不需要 pipeline 插件或其他网络包。Packages/manifest.json 仅声明内建 JsonSerialize 模块；本轮明确要求直接准备独立项目文本，因此没有复制正式工程包清单或启用技能默认的 URP 模板。

## 运行入口与记录

显式传入以下参数才执行诊断（不要使用 -batchmode/-nographics 启动 Player）：

```text
NativeMinimal.exe --native-output <workspace>/evidence/v06/display-minimal -logFile <workspace>/artifacts/cache/v06/native-minimal-dev-player.log
```

输出必须是当前工作区内的绝对路径；程序沿构建目录上级查找 AGENTS.md 与 artifacts 来限定工作区。每次在该输出基目录下新建 `native-UTC-PID`，不复用旧结果。没有 `--native-output` 时仅显示动态纯色 Camera，不发起分辨率请求或写诊断。游戏窗口应正常可见；root 保持仅操作其启动的进程。

运行设置为 runInBackground=true、vSyncCount=0、Application.targetFrameRate=60，使用 Unity 自身帧率限制。没有 GameApp/Core、UI、Input System、FramePacer、渲染管线包、几何体或物理组件。

序列为：

1. 请求1920×1080 Windowed，等待实际尺寸/模式连续三帧一致，10秒超时会记录失败。
2. 读取当前窗口所在显示器的原生尺寸，以及 Screen.resolutions。在相同尺寸的枚举项中选择最接近当前显示器刷新率的真实 RefreshRate 比值，调用 ExclusiveFullScreen 四参数重载。
3. 观察至少5秒，每帧保存实际宽高/模式、focus、当前显示器尺寸/刷新率与 currentResolution；完整分辨率列表记录在初始及每个试验臂前。恢复1920×1080 Windowed并等待三稳定帧。
4. 调用不指定刷新率的三参数 ExclusiveFullScreen 重载，再观察5秒，然后再次恢复并验证三稳定帧。

每次请求前落盘报告；`frames.jsonl`、`log-events.jsonl` 每行 flush，报告定期更新。原生崩溃可能来不及输出最终报告，因此保留最后的 observing 状态和 root 指定的原始缓存日志。错误也保留，不把 native 错误计数变成虚假的通过。

每个完成观察的臂为 `observed_not_acceptance`；序列跑完为 `diagnostic_completed`，退出0仅说明观察流程结束。窗口基线/恢复或托管错误使用 `diagnostic_failed`；正常中断为 `diagnostic_interrupted`。这里没有 passed 字段，也不声明正式游戏、原生显示、性能或发布验收通过。

源准备者没有启动 Unity、Player 或修改正式工程。后续如改源，重新保存源副本/哈希；保留已产生的失败构建与运行证据。
