# v0.71 Mac 试玩与验证


这份工具包含主菜单与木沿毛毡桌面更新。开发基线 b005e00；实际构建源 `b005e0088b227b26e2f8fc44f1c59f68df0c0a35`，Mac GUID `b523ca601806410ca9fbb7ff3d943c8c`。macOS Universal 含 arm64 / x86_64，Mac 实机运行、Metal、签名与公证尚未验证；交叉构建成功不等于 Mac 运行通过。

普通试玩可直接使用 macOS 游戏包中的 `DicePrototype.app`。协助验证时，完整解压 `DicePrototype-p0.7.1-MacVerificationKit.zip`；其中已含同版 Mac 游戏，无需重复下载。需要 Python 3.9+，工具不会自动安装。

在终端输入 `bash `，把 `VerifyMac.command` 拖入终端后回车，或在解压目录执行 `bash VerifyMac.command`。运行时保持窗口可见。工具会在新目录解压、隔离存档，并执行回放、显示和压力检查；结束后请回传完整结果目录与日志，包括失败结果。

Windows 对照结果：完整严格流程已执行，正常与调试回放、UI、动画和自动原生显示检查通过，独立进程恢复通过；180.01 秒采样的 P95 为 16.847 ms，超过 16.7 ms，性能 FAILED，作为诊断对照保留原始失败结果。GPU 有效样本覆盖 97.22%；物理焦点/显示器变更仍未测。只使用 `inputs/windows-results/player-report.json` 内本轮真实结果；它不能代替 Mac 实机验证。逐帧 CSV 是否随包，以实际清单为准。Mac 保持原有至少 180 秒、P95 ≤16.7 ms 和显示检查标准，不因 Windows 的失败放宽要求。

若系统阻止启动，请保留原提示。工具不移除隔离属性、关闭 Gatekeeper 或重签；按正常系统授权或正式签名流程解决后，再在新结果目录验证。AudioMixer 仍用分类音量回退，人工画面、声音、体验和实际显示器/焦点变化仍需分别反馈。
