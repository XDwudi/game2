# RC2 下载与实际启动观察复核

结论：**满足 RC2-05 的远端完整下载校验与下载版实际启动观察范围；可交付已授权的诊断试玩候选。辅助资源/空闲诊断未通过，Windows 正式性能仍 FAILED，Mac 未实机运行，整版不标 ACCEPTED。** 当前源码 b005e00，审查者 `/root/project_audit`，只读核对已有证据，无联网、Unity 或 Player 操作。

实际重读三份下载 ZIP 全部成员到 EOF，311/306/17 成员的 CRC、SHA、大小、mode 与先前独立检查的本地封包逐项相同。服务器 release=440245825，Win/Mac/Kit asset ID 分别 731100890/731104688/731111250；下载 receipt 均 HTTP200、绑定 rc.2、源 b005e00 和 manifest `a3c1dfaf…`。不是只凭 HEAD 推定完整下载。

两个独立新目录 `artifacts/cache/v071-rc2-remote-b005e00-01` 与 `-02` 各 311 个解压文件，当前逐 SHA、大小和完整集合与下载 ZIP 及运行前 inventory 相同。两次报告都由实际 WindowsPlayer 产生，GUID=`75e7f6b0f1c34075a6527abbc6829e8b`、Runtime MVID=`5c19060c-301d-4a32-8ed5-6d4ce85722a2`。两次自然退出，未触发 launcher timeout 或强杀。实际启动与程序执行已有证据，不声称下载版人工玩过或成功截取画面。

- 01 自然 exit3，18 阶段到 after-idle、9 资源快照、4006 resource 行。20.0005557 秒 idle 的 1200 行中，1200 GC 样本有效、0 失焦、0 动效活动、0 指针改变、0 序列改变；**恰有 1 行 inputInactive=false**，依原 `inputRows==0` 标准判 `diagnostic_incomplete`。1199 行 GC=0、1 行 132B 不是改变通过标准的依据。
- 02 自然 exit1，6 阶段、10129 resource 行，在 SettingsTab0 的 `Stable()` 内触发原 180 秒总 deadline。没有进入 idle，idle CSV 为 0 行。`Stable()` 要求连续 3 秒动效闲置、Application.isFocused、InputInactive；任一条件不满足均重置稳定计时。**resource CSV 不含这三个条件，不能从现存数据确定失焦、输入或动效中的具体分支。** 零 idle 行不能算零分配或通过。
- 两次完整 before/after guard 均证明 311 文件、17 个普通目录文件和 48 个游戏偏好键相同。01 Unity Engine Screenmanager 值改变单独保留；02 该组也相同。没有将引擎显示值与游戏偏好合并掩盖差异。

原失败记录、CSV、日志和报告均保留；本复核未改门槛、重跑或用成功的完整 Player 测试替换这两次未完成结果。root 的 `downloaded-launch-evidence.json` 明确 startupObserved=true、supplementaryDiagnosticPassed=false，与实际数据一致。CUA 未取得截图/未成功发送 UI 输入是 root 报告的范围，本报告不把它当新画面证据。

审查过程有一次已公开纠正的范围错误：原“轻量 receipt”辅助函数内部还重算了三 ZIP 的 SHA，约 0.953 秒，结束于 11:10:42.561085Z，处于 02 执行期间。原错误声明与 `REMOTE_RECEIPTS_LIGHT_SCOPE_CORRECTION.json` 都保留。此重叠不能作为无后台 IO 性能证据，也不足以将 02 的具体超时分支归因于该读取。之后 CRC/成员核验在两次诊断均结束后完成。

实际核验脚本 `review_remote_delivery_b005e00.py` 执行成功；绑定 `REMOTE_DELIVERY_B005E00.bindings.json` SHA `d4c9476561d41301517a3f8ead3d99496a2af05d096cc208e7b9be773f2b42bf`。本检查不扩大为全量辅助诊断或 Mac 验收。

## 公开交付闭环

实际发布 receipt 与匿名 proof 交叉核对一致：`2026-11-10T11:22:20Z` 发布，同一 release440245825，draft=false、prerelease=true，canonical URL 为 `https://github.com/XDwudi/game2/releases/tag/p0.7.1-rc.2`。标签指向实际构建源 b005e00；标题、正文与冻结 manifest 生成值逐字符一致，保留性能/物理事件/Mac/人工体验限制。

三附件 ID、服务器 digest、大小与此前全量下载一致。匿名 API 及三个 canonical 下载 HEAD 均 200、Content-Length 匹配；原草稿 alias 没有被冒充公开链接。公开 proof 记录 main/rc.1 保持旧值、旧 release438762609/三附件 ID 保留；审查者没有另发网络请求。

最终绑定 `PUBLIC_FINAL_B005E00.bindings.json` 明确交付完成而辅助诊断未通过。后续可以让用户下载试玩；不需要把已知未完成项改写为通过才能提供这一受限测试版。
