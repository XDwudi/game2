# v0.6 限制版人工测试候选工具交接

这是一条显式选择的交付路径，沿用用户已授权的 GitHub 三附件渠道；不是新增用户批准流程。Windows P22 原生显示及相关状态隔离尚未通过、Mac 实机未验、整版本非 ACCEPTED。正式默认门槛未降低。本次没有运行 Unity/Player、创建实际候选 ZIP、访问凭据、上传或发布；真实候选由 root 使用已冻结构建与真实报告生成。

## 报告合同

默认 `validate_report` 与无选项的所有正式命令继续要求 `status=passed`。新增 `validate_windows_review_reference` 仅接收真实的：

- `status=partial_passed_native_display_pending`；
- `verificationScope=full-gameplay-ui-perf_native_display_pending`；
- `nativeDisplayDeferred=true`、实际退出0；
- `native-display-verification-deferred / not_executed` 标记。

所有非显示检查仍必需且通过，包含两项真实P19、32目标选择、拖拽/冻结、独立调试、normal和debug24完整源轨迹、原180秒/16.7ms性能门槛、手册纯读、零Unity错误、检查点。仅允许原生显示未执行与display-state-isolation缺失/明确not_executed。实际失败的原生报告、错误退出、缺少其他UI、少一行轨迹或性能超限均拒绝，原报告从不重写。

## 生成三个具体包

以下占位符必须替换为本轮实际冻结报告，不调用旧版本：

```text
python tools/package_v06.py windows --build-report <Windows构建报告> --review-candidate
python tools/package_v06.py macos --build-report <Mac构建报告> --review-candidate
python tools/package_v06.py kit --windows-results <真实partial的player目录> --review-candidate
python tools/release_v06_manifest.py --windows-results <同目录> --windows-build <Windows报告> --macos-build <Mac报告> --review-candidate
```

三个同名ZIP独立放到 `Releases/p0.6.0/review-candidate/`，不覆盖正式路径。manifest为 `review-candidate-manifest.json`、独立schema `v06-restricted-review-candidate-1`、状态 `RESTRICTED_TEST_CANDIDATE_WINDOWS_NATIVE_PENDING`。对应package proof使用 `*-package-review-candidate.json` 与 `mac-verification-kit-review-candidate.json`。完整源/已加载程序集/NUnit绑定/身份/GUID/包字节/SHA/CRC仍按原合同检查。

两平台包带 `REVIEW_CANDIDATE.json` 和更新的试玩说明；M5工具包带输入notice与其SHA。完整工具包直接执行VerifyMac.command时，会明确提示Windows原生限制并传入review参数。

M5只能把Windows部分报告作为玩法/UI/轨迹/性能参考；Mac实际运行仍不传入defer开关，并完整要求Metal/Arm64/所有原生显示、恢复、轨迹和原性能门槛。其成功摘要为 `mac_automated_checks_passed_windows_native_and_manual_pending`，不表示Windows或整版通过。

## 独立Windows恢复

```text
python tools/verify_v06_display_recovery.py --exe <实际EXE> --trace evidence/v06/session/normal-run-trace.json --debug-trace evidence/v06/session/debug-24-trace.json --identity <实际BuildIdentity> --build-report <Windows报告> --main-results <同一partial目录> --evidence-dir <全新evidence/v06子目录> --review-candidate
```

这里只允许选用partial参考；两进程恢复本身仍要求真实writer回执、仅终止自己启动的进程、新reader、恢复实际值与持久pending清除、GUID/工具身份一致。如果真实通过，可将其共享目录用 `--display-recovery` 传给manifest。限制版manifest可暂不提供，明确记录 `not_executed_native_display_pending`；传入失败或不完整证据则拒绝，不能悄悄改成pending/pass。独立恢复通过也不等于所有Windows原生显示通过。

## 已授权渠道的显式防误发开关

root在实际三包和manifest绑定好后可使用：

```text
python tools/publish_v06_release.py draft --review-candidate
python tools/publish_v06_release.py upload --name <三个合法包名之一> --review-candidate
python tools/publish_v06_release.py download --name <同包名> --review-candidate
python tools/publish_v06_release.py publish --review-candidate --allow-review-candidate
```

双开关用于明确技术范围，不要求用户重复批准已有授权。缺少allow开关会在读取凭据或联网前拒绝publish。仍须三个完整附件全部上传、GitHub digest匹配、实际新目录下载SHA/大小/CRC与manifest一致；不覆盖已有附件。review下载证明和远程记录分开保存到review子目录。正式命令默认拒绝限制版schema和partial状态。

## 已执行检查与写锁

- `review-candidate-tools03.log`：**86/86** 离线合成工具检查；原68项保留，新增18项正反/缺项/性能/原生失败/轨迹截断/包字节/notice/发布开关/恢复范围检查。
- `review-candidate-runner14.log`：原两进程工具14/14 mock回归。
- `review-candidate-framework20.log`：项目确定性检查器20/20。
- Python编译与CLI help成功。以上不产生Mac或实际Player通过证据。

源哈希见 `review-candidate-source-manifest.json`。本工具范围冻结供独立复核；如审查要求修复，会另通知root并重跑受影响检查。正式游戏源码保持root的f2de61a冻结；本轮仅工具、试玩说明和此交接记录。STATE与DELIVERY由root维护。
