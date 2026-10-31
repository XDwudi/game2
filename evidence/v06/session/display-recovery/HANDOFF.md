# Windows v0.6 独立进程显示恢复运行器交接

状态：工具离线验证完成，等待主协调者执行真实 Player。未启动 Unity、Player、Blender，未修改生产设置、门禁、Mac 验收规则或 GitHub。`docs/DELIVERY_V06.md` 仍是明确标注待实际结果的交付草稿。

## 文件与接口

- `tools/verify_v06_display_recovery.py`：Windows 专用包装器，只使用真实 Runtime writer/reader 协议。先验证成功的 Windows 主 Player、实际构建 GUID、BuildIdentity 和两份轨迹；主 Player 未通过时拒绝启动。
- `test_windows_recovery_runner.py`：14项离线测试，全部子进程为 mock，临时合成数据不作为游戏证据。
- `docs/DELIVERY_V06.md`：Windows/Mac/M5 工具包步骤；最终 source、NUnit、Player、ZIP 哈希与发行链接留给主协调者从实际结果填入。

参数示例（替换占位路径，不要照抄不存在的目录）：

```powershell
python tools/verify_v06_display_recovery.py `
  --exe <实际Windows包目录/DicePrototype.exe> `
  --trace evidence/v06/session/normal-run-trace.json `
  --debug-trace evidence/v06/session/debug-24-trace.json `
  --identity <同版BuildIdentity.json> `
  --build-report <实际Windows构建报告.json> `
  --main-results <已通过的player-UTC-guid目录> `
  --evidence-dir evidence/v06/player-display-recovery/attempt-01
```

`--evidence-dir` 必须是 `evidence/v06` 下全新目录，不覆盖或复用。可选 `--raw-dir` 必须是 `artifacts/cache/v06` 下全新目录；默认生成唯一缓存目录。`--timeout` 默认120秒，限制为30–600秒，只调整进程启动/退出等待，不改变真实15秒确认计时。

## 真实执行合同

1. 显式 `STARTF_USESHOWWINDOW / SW_SHOWNORMAL` 启动游戏 GUI，不创建额外 console。参数为数组、`shell=False`，没有命令拼接。
2. writer 产生完整 `ready-to-terminate.json` 后，核对自己启动的 PID 和隔离 settings 路径，立即调用原 `Popen` 句柄的 kill/wait。目标小于15秒，记录实际观察至退出耗时；不按名称枚举进程、不终止别的 Player。
3. 退出后验证 pending 文件仍为真实原始 SHA、pending=true 和正确身份。不写设置文件、ready 或 reader 回执。
4. 启动新 reader；要求不同的真实 PID、退出0、`recovery_passed` 报告、实际窗口尺寸/模式恢复、pending=false，并绑定成功主报告的 GUID、probeRevision、MVID。
5. 任意失败只清理自己持有的尚未退出进程，保留全部已有证据。退出码0表示该独立恢复检查通过，1失败，2启动前校验拒绝，130中断。主报告中的 `not_executed` 保持原样，本工具不代替主回放、180秒压力或 Mac 结果。

最终目录包含原始 runtime 回执、reader Player 报告，以及安全的 `writer-process.json`、`reader-process.json`、`runner-summary.json`。原始 Unity/stdout/异常命令行只写到缓存；最终摘要仅含失败类型，不复制异常参数。失败目录也应保留，重试使用新的目录。

供 manifest 使用的 `--display-recovery` 指向上述共享目录；不用指向 reader 内层的 player 目录。

## 已运行检查

- `offline-tests-02.log`：14/14，mock 进程；包含句柄归属、PID/path 拒绝、半写回执重试、设置字节不改、超时/非0清理、输入错误、窗口显式显示以及异常启动参数不泄漏。
- `quality-framework-tests.log`：20/20，项目确定性检查器回归。
- `python -m py_compile` 与 `--help`：成功。
- 独立复核 `evidence/v06/review/recovery-runner-review02.json/.log`：17/17（原14 + 审查者3负例），四份源文件前后SHA稳定；仍仅离线 mock。

`source-manifest.json` 绑定本交付工具、草稿、测试、只读复用依赖与上述证据。此清单不是游戏构建门禁，不宣称 native recovery 已通过。工具/测试写锁释放给 root；docs/STATE.md 由 root 合并最新真实状态，避免并发写全局状态。后续如改工具，应重跑受影响离线检查并重新绑定清单。
