# P0 v0.6 Windows 性能失败诊断候选工具交接

输入仍为用户完整 P0 v0.6；生产源由 root 冻结在 `08ef933`。本轮仅交付工具、离线测试、试玩说明与证据，不修改生产代码、原门槛、原始 Player 报告、STATE 或已授权发布范围；没有运行 Unity/Player、打包、联网、上传或发布。

## 实际失败与选用范围

Player09：`evidence/v06/player/player-20261031-154556-f25586f0`，原状态 **budget_exceeded**、exitCode **3**，原 scope `full-gameplay-ui-perf_native_display_pending`。完成 145 步正常、175 步 debug24、15 项非显示 UI、真实检查点与 180.004603 秒压力；P95 **23.0348 ms > 16.7 ms**，**性能 FAILED**，9103 帧中焦点帧 0、异常 GPU 样本 1。P22 原生独占未通过，Mac 未实测，整个版本未 ACCEPTED。

新增独立 `--allow-failed-windows-performance`，只能与 `--review-candidate` 一起选用。原 `validate_report` 和 `validate_windows_review_reference` 保持严格，仍拒绝本报告。新诊断函数只接收上述失败种类与完成范围，仍检查完整时长、分辨率、负载、15 项实际 UI、全部真实输入轨迹、身份、检查点、零 Unity 错误；不接收短采样、尺寸不符、其他异常退出、缺失/失败玩法或原生检查，也不接受把失败字段手改为 passed。

新 notice/manifest 首屏明确 FAILED、原 P95/焦点/GPU 数值、P22 未通过、Mac 未验和非 ACCEPTED。manifest schema 为 `v06-diagnostic-review-candidate-1`，状态 `DIAGNOSTIC_TEST_CANDIDATE_WINDOWS_PERFORMANCE_FAILED_NATIVE_PENDING`；保留完整原 performance 子树。原 Player 报告按字节复制、SHA 绑定，没有再生成预期值。

## root 实际交付命令

使用实际 Windows/Mac 构建报告路径替换尖括号。诊断候选沿用独立的 `Releases/p0.6.0/review-candidate/` 目录；写入均拒绝覆盖，不能混用没有诊断标记的包或 proof。

```text
python tools/package_v06.py windows --build-report <Windows报告> --windows-results evidence/v06/player/player-20261031-154556-f25586f0 --review-candidate --allow-failed-windows-performance
python tools/package_v06.py macos --build-report <Mac报告> --windows-results evidence/v06/player/player-20261031-154556-f25586f0 --review-candidate --allow-failed-windows-performance
python tools/package_v06.py kit --windows-results evidence/v06/player/player-20261031-154556-f25586f0 --review-candidate --allow-failed-windows-performance
python tools/release_v06_manifest.py --windows-results evidence/v06/player/player-20261031-154556-f25586f0 --windows-build <Windows报告> --macos-build <Mac报告> --review-candidate --allow-failed-windows-performance
```

正式 gate/测试加载程序集/源快照/身份/构建 GUID/整个 ZIP 字节与 CRC/工具包 notice 绑定均保留。两平台包和 kit proof 都记录 `allowFailedWindowsPerformance`，两平台包同时绑定原 Windows 报告 SHA；不能把只含原 review 提示的包接到诊断 manifest。

用户已授权三个 GitHub 发行附件，开关用于明确技术范围，不再次要求批准：

```text
python tools/publish_v06_release.py draft --review-candidate --allow-failed-windows-performance
python tools/publish_v06_release.py upload --name <合法包名> --review-candidate --allow-failed-windows-performance
python tools/publish_v06_release.py download --name <合法包名> --review-candidate --allow-failed-windows-performance
python tools/publish_v06_release.py publish --review-candidate --allow-review-candidate --allow-failed-windows-performance
```

publish 缺任一相关开关即在取凭据/联网前拒绝。三附件实际下载 SHA/大小/CRC、既有附件不得覆盖与跨 host 去凭据规则不变。

## Mac 与独立恢复

M5 的 `VerifyMac.command` 读取工具包明确诊断 notice 后展示失败提示，传入两项范围开关。Windows 仅作玩法/UI/轨迹对照；实际 Mac Player 命令没有 defer 或性能放宽参数，Metal/Arm64/全部原生显示/原 180 秒及 P95/采样完整性/真实恢复均保持原合同。Mac 即使自动检查通过，摘要仍指出 Windows performance FAILED、native/manual pending，不能标整个版本接受。

Windows 恢复运行器同样可追加 `--review-candidate --allow-failed-windows-performance` 使用此参考；自己的 writer/reader 原生恢复检查保持严格，独立恢复通过不会改变 Windows 性能 FAILED，也不改主报告。

## 已执行检查

- `diagnostic-candidate-tools04.log`：100/100 离线工具测试，原 86 项保留，新增 14 项诊断正反向覆盖。
- `diagnostic-candidate-runner14.log`：14/14 mock 进程回归。
- `diagnostic-candidate-framework20.log`：20/20 确定性检查器回归。
- `diagnostic-candidate-actual-reference01.json`：只读真实 Player09 全部输入轨迹与报告校验，原报告前后 SHA 同为 `7c7116714d70b04b6915e700d79a8c75e2844f343d265e5355976f2cb0504f54`；不是新 Player 运行。
- 首轮工具测试的 fixture 参数迁移失败记录 01/02 完整保留；未改测试目标或删用例。

源清单 `diagnostic-candidate-source-manifest.json`。交接供独立 review；正式是否接受仍按原标准，root 负责实际包和发行证据。工具/说明写锁冻结，除独立审查缺陷外不再改写。
