# b005e00 Player 与 RC2 封包限域复核

复核者：`/root/project_audit`。**可以继续交付用户已授权的诊断试玩候选；没有本地封包阻断。Windows 性能仍为 FAILED，Mac 为 NOT_RUN_ON_MAC，整版不标 ACCEPTED。** 远端上传、完整下载和下载后还原另待真实 receipt；本报告未联网、未运行 Player/Unity。

## 实际 Windows 结果

只使用 `serial-summary.json` 指定的 `strict-full/player-20261110-104130-079c5f75/player-report.json`，没有拼接旧 deferred/native-only 通过结果。实际进程退出码 3，报告 Completed / budget_exceeded；identity=b005e00，GUID=`75e7f6b0f1c34075a6527abbc6829e8b`，Player Runtime MVID=`5c19060c-301d-4a32-8ed5-6d4ce85722a2`，均与实际构建/PDB 相符。

- 145 条正常回放和 175 条调试回放逐命令与原 trace 核对一致，存档/渲染比较均真。正常回放在第 8 关失败是 trace 的预期终态；24 关调试回放只证明集成，不是正常胜率或难度证据。
- 19 个实际 UI 检查和 50 个动画检查通过；报告无 Unity error。原生显示 11 条 passed、3 条因超出当前工作区 not_supported、2 条主报告 not_executed。实际独占/无边框/窗口、非原生独占 Escape、15 秒倒计时和受支持窗口尺寸通过。物理 Alt-Tab/显示器拔插仍未执行。
- 独立进程恢复另外真实执行：writer PID 17760、reader PID 844，writer 只终止所拥有句柄；pending 原哈希、ready receipt、reader receipt、同构建身份及恢复到 1280×720 Windowed 互相一致，reader 退出 0。主报告原有 not_executed 没有被改写。
- 独立重算原 CSV 的 10139 行（排除首个部分间隔后 10138 行），连续帧 6981–17119，elapsed 与每帧间隔吻合。采样 180.0108082 秒；P95 **16.84699999998429 ms > 16.7 ms**，均值 17.75456 ms，最大 71.46750 ms，保持失败。没有丢弃慢帧或用另一时钟替换原判据。
- GPU 9856/10138=**97.218386%**，达到既有 95% 覆盖标准；282 行 unavailable，0 invalid，不能描述为无缺样。CPU、GC、UI 指标均逐列重算与报告一致。CSV 没有逐行 focus 栏，报告 focusedSampleFrames=10139 只能作为上下文，不能独立重建可见性。
- 实机环境为 Windows 10 64 位、Ryzen 9 5900X、RTX 3080、D3D11、1920×1080、PC quality、非 Editor Development Player。主日志和恢复日志未发现 Exception/error/warning/swapchain/timestamp 字样；这是日志范围检查，不是无缺陷保证。
- 305 个实际构建文件与运行前记录逐 SHA 一致。正常和压力使用隔离路径，正常存档当前 SHA 与压力前报告哈希一致。真实 runner 对普通用户存档前后作比较并记录 true，但本轮没有另存前置哈希明细；因此不能独立重建普通存档前快照。本报告明确保留该证据边界，没有补造。

实际打开四张原 PNG：1080 主菜单、1080 普通选择状态、720 商店、1080 压力末帧。新木沿/缝线/毛毡背景、菜单卡骰构图及被动文字底色可见；所看帧未见本轮新增遮挡、拉伸或输入提示被缝线穿过。只覆盖这些帧，不把 124 张截图全称人工看过，也不代替用户风格/体验判断。

原始重算、回放、恢复、构建/源与四张图 SHA 见 `PLAYER_B005E00.bindings.json`，SHA `f6fe33c0dc52ce9c526297560c17dac3136457c305bbf11d1f731382f27b243c`；脚本 `review_player_b005e00.py` 实际执行成功。严格完整验收 validator 仍拒绝此报告（player_did_not_pass / pressure_gate_not_met）；仅显式诊断候选路径可使用。

## 三个实际 RC2 ZIP

| 成品 | 字节 | 成员 | SHA256 |
|---|---:|---:|---|
| Windows x64 | 200441327 | 311 | `44ef23a3d91be201706b59c5f006886ec7198781f3cf4084c3d13adb93fe656a` |
| macOS Universal | 240794669 | 306 | `90b76360530c53c2fb46ed3e542b691225884c9259d2c5891030b9e81003b913` |
| MacVerificationKit | 241475500 | 17 | `f6687b2e6ff41ae7c3433954239eb676859c2db8b160ddcd16bef407b8c31dd5` |

全部在独立 `Releases/p0.7.1/menu-table-rc2/`。实际读取每个 ZIP 成员到 EOF 检查 CRC，并逐 SHA、大小、mode、完整成员集合与 proof/构建目录核对。tag 均为 `p0.7.1-rc.2`，源 b005e00。Mac 的 6 个原生组件字节同双架构报告，主程序权限 755；没有推定 Mac 解压运行、Metal、正式签名或公证通过。

Kit 嵌入的 Mac ZIP 与独立 Mac 包完全相同，17 个实际成员包含本轮 Windows report、145/175 回放、检查点和原始 `inputs/windows-results/performance-frames.csv`。4 个 Mac 脚本与当前工具源一致；`执行说明.md` 与当前正式文档逐字节相同。两游戏包 `开始游玩.txt` 和 BUILD_IDENTITY 精确匹配当前文档/身份，没有旧 41aface GUID、rc.1 标签或旧 native pending 结论。

三份候选 notice 完全相同，均显著保留 Windows 性能 FAILED、物理事件未测、Mac NOT_RUN、fullReleaseAccepted=false。工具包文档使用 `bash VerifyMac.command`，不依赖脚本直接可执行标志。README 的“普通存档未变”来自真实 runner 布尔比较，其独立证据边界见上文。

脚本 `review_packages_b005e00.py` 实际完成上述读取，未解压或执行包内任何内容。绑定 `PACKAGES_B005E00.bindings.json` SHA `21cdef90fb28578ca9623650eb76b4dafe2d13cd6449d2ef698c60660d102eed`。后续只核实际发行 manifest 与远端下载/还原/公开 receipts，不追加本地全量测试或改验收标准。
