# v0.71 M5 实机验证执行说明

状态：工具已按 p0.7.1/art0.7.1 作版本适配；本说明撰写时尚无本版最终Mac构建或M5实机结果。规则 p0.6.0 / schema4、Unity6000.6.7f1 / Mono、原平台验收要求保持。执行对象以工具包 inputs/BUILD_IDENTITY.json 为准，不能套用历史v0.7通过结果。

完整解压 `DicePrototype-p0.7.1-MacVerificationKit.zip`。工具包包含 VerifyMac.command、verify_mac.py、compare_replays.py、package_checks.py、Mac原包、两条规则轨迹和本版真实Windows参考证据。缺输入时脚本拒绝运行，不能手填通过结果。需要Python3.9+，脚本不自动安装。

在终端输入 `bash `，拖入 VerifyMac.command 后回车。也可在该目录执行 `bash VerifyMac.command`。完整工具包无需再单独下载Mac包；程序使用全新目录重解压并记录实际系统、架构、Metal、原生显示、隔离存档、回放及180秒压力采样。保持游戏窗口可见，自动阶段不要最小化或启动另一份探针。

既有阈值不变：正式性能P95≤16.7ms，采样至少180秒，完整原生显示和相关恢复检查继续执行。若Windows参考为明确标记的受限/性能失败诊断，脚本只按其真实范围对照；Mac本身仍执行原严格要求。正常回放可能按既定轨迹在第8关失败；Debug24为功能覆盖，不代表玩家正常通关率。

如果系统拒绝启动，保留原提示和生成证据。脚本不移除隔离属性、不关闭Gatekeeper、不重签，也不伪称已公证。通过Mac正常系统授权或正式签名流程解决后，再用新结果目录验证。

结束后回传脚本生成的完整结果目录及日志，尤其是report、逐条回放、性能CSV、显示恢复、截图、存档检查点。失败记录原样保留。跨平台回传存档、真实焦点/显示器切换、人工风格、听审和试玩结论分别记录，不能只凭exit0把整版标为ACCEPTED。

本版继续引用未改变的 `evidence/v07/session/normal-run-trace.json` 和 `debug-24-trace.json` 原输入；由本版Player实际重放并核对SHA。不会把旧版本Player报告作为本版运行证明。
