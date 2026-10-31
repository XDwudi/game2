# P0 v0.6 诊断测试版交付说明

**三份附件已发布于 [GitHub p0.6.0-rc.1](https://github.com/XDwudi/game2/releases/tag/p0.6.0-rc.1)。** 玩法实现和测试候选双平台构建已经完成；Windows 独占全屏与性能验收未通过，Mac 实机未运行，整版未 ACCEPTED。按用户已授权的 GitHub 三附件渠道提供人工测试包，不把诊断候选称为完整验收通过。最新进度见 [STATE](STATE.md)，规则以 [v0.6 开发规格](specs/P0_v0.6.md) 为准。

| 最终绑定项 | 当前状态 |
|---|---|
| 游戏源码提交 | `08ef933f2fbc3198a5e357ecebaa2758fbee8643`；已推送并从全新网络克隆核验607个构建源文件原始字节 |
| 配置 / 黄金用例 SHA-256 | `747ed1efc7bea15873f66a81eee5c7654971c21031f2a1517ce629adce7b6e6c` / `306cb86f79dafe0e3eb302714abd232af20fe0062a6713f7d713c11e2a6a5718` |
| Unity EditMode / PlayMode | 797 / 30 全部通过，零跳过；独立PDB与真实加载程序集、起止源快照一致 |
| Windows Player 流程 | Player09正常145步、第8关按轨迹失败；Debug175步覆盖24关/23商店；15项UI检查通过，不代表平衡或真人体验通过 |
| Windows 显示与性能 | 独占全屏P22失败；180.004603秒/9103帧，P95 23.0348ms，超过16.7ms门槛；0帧前台焦点，另有1个无效GPU计时值。报告原值保留，完整性能条件未通过 |
| Windows / Mac / M5工具包 | 3/3实际上传和远端完整下载，SHA/大小/CRC全相符，详见下表 |
| GitHub 发行页 | `p0.6.0-rc.1`，release 430037166，draft=false / prerelease=true，绑定真实源码08ef933 |
| Mac M5 / Intel / 真人体验 | M5与真人待执行，Intel运行未验证 |

交付渠道已获用户授权：GitHub Release 的三个完整 ZIP 附件，无需 Git LFS。发布时三包须来自同一冻结版本，并绑定实际构建、测试和下载证据。

| 下载 | 字节数 | SHA-256 |
|---|---:|---|
| [Windows x64](https://github.com/XDwudi/game2/releases/download/p0.6.0-rc.1/DicePrototype-p0.6.0-win-x64.zip) | 86,495,395 | `c809dcc0fdfe07acb4a0fe67d5eca6462f53075f39a2a52b2c8835cc008e6dba` |
| [macOS Universal](https://github.com/XDwudi/game2/releases/download/p0.6.0-rc.1/DicePrototype-p0.6.0-macos-universal.zip) | 126,816,584 | `a8cf1b007fe35c276c2a2ce7fc0ddab676a54ab4c7c5e54cd00a7fbd39e45b88` |
| [M5 验收工具包（内含同版Mac包）](https://github.com/XDwudi/game2/releases/download/p0.6.0-rc.1/DicePrototype-p0.6.0-MacVerificationKit.zip) | 126,918,458 | `013e101a2ed3c0f2307cde56f2a6e367e6bf7a4fd8fed86d69cc775c41505bed` |

完整下载所得Windows包在全新目录实际启动，恢复旧进程检查点后继续提交132分，状态/RNG/再次读取/重复命令一致，原检查点未改，实际1920×1080主菜单截图已独立检查。该限定检查结束后仅终止自己的测试进程；原Player报告仍为running，不冒充第二次完整流程/压力通过。证据 `evidence/v06/delivery/github-release/review-candidate/windows-remote-restore02.json`；独立总审见 `evidence/v06/review/REVIEW_CONCLUSION.md`。

本机为 Ryzen 9 5900X / RTX 3080 / 约64GB、Windows 10 build19045、D3D11。Windows09构建GUID `c137ca83c7ec4fbab3243ff0aa663bfb`，零警告/错误；Mac01 GUID `9c16cd34b32c400eb879daef7dcfc29d`，零错误、71条shader警告，实机影响待验。两包实际程序集均与同一冻结源码匹配，Mac六个原生组件均含arm64+x86_64。

独占模式在独立Builtin空场景的Development/非Development对照也失败；尚未确定底层原因。电脑控制工具能枚举并操作游戏，但截图持续超时，激活窗口后另一次分辨率检查失败。没有把失败解释成已解决，也没有修改驱动、安全设置、帧调度或验收阈值。建议本次试玩先使用窗口模式；新的前台显示/性能结果需要实际设备重验。

## Windows 试玩

最终包名为 `DicePrototype-p0.6.0-win-x64.zip`。下载后完整解压到新目录，运行 `DicePrototype.exe`；保留同目录 Data、DLL、`BUILD_IDENTITY.json`、许可证和“开始游玩.txt”。不要只复制 EXE。版本页应与发行记录中的源码、配置哈希、黄金用例哈希和 schema4 一致。

本版为24关骰子构筑原型，初始金币0。支持新的多对子/双三条、独立全红/全蓝乘区、增强红骰奖励、50个控制器及调整后的商店内容。控制器可在商店和关卡预览拖拽排序；进入关卡后冻结。商店可单独刷新控制器，价格3、5、7……；对应服务使首次免费后继续5、7……。

设置支持分辨率、全屏、无边框和窗口模式。应用显示变更后15秒内确认；超时或Esc恢复。设置中准确输入 `XD is god` 可进入独立调试工作区，编辑控制器/附魔、道具、骰子和等级，最高32容量用于测试。返回普通挑战后原检查点保留；调试通关不代表正常难度已验证。

详细操作、键位、存档隔离和反馈信息见 [试玩说明](PLAYER_README_V06.txt)。旧版本Run保留并隔离，不自动猜测迁移。请记录种子、控制器顺序及附魔、骰面、复现步骤和版本页，便于复查。

## Mac 包与 M5 验收

`DicePrototype-p0.6.0-macos-universal.zip` 包含 arm64 与 x86_64，但 Windows 生成的包仍是传输候选。M5原生运行、Metal、系统安全状态、ditto封装及新目录解压运行需要实际Mac证据；包含Intel slice不代表Intel通过。

M5用户使用完整 `DicePrototype-p0.6.0-MacVerificationKit.zip`。解压后打开终端，输入 `bash `（保留末尾空格），将 `VerifyMac.command` 拖进去并回车。需要 Python3.9+，无需Unity或Blender；工具包已准备携带同版Mac包、两份输入轨迹、Windows真实对照结果及检查点。

保持游戏窗口可见，自动阶段不要操作或最小化。工具执行原生运行、轨迹复现、显示检查、独立进程恢复和至少180秒压力采样。系统拒绝运行时保留提示与失败证据；脚本不删除quarantine、不关闭Gatekeeper、不代签或伪称公证。

完成后回传 `MacVerification-Evidence.zip`、证据哈希、Mac生成的最终ZIP，以及人工检查记录。完整步骤与密码/Debug32/拖拽/显示15秒确认等清单见 [M5验收说明](tasks/P0-V06-MAC-VERIFICATION.md)。自动结果通过后，Mac→Windows检查点恢复和人工输入/多屏/体验仍须分别验收。

## 协调者验证与发布记录

当前完整实机原始记录：`evidence/v06/player/player-20261031-154556-f25586f0`，状态 `budget_exceeded`。独立复算见 `evidence/v06/review/player09-independent01.json`；CUA失败见 `evidence/v06/display-diagnostics/cua-focus-attempts.json`；远程源码字节恢复见 `evidence/v06/delivery/remote-source-restore02.json`。保留此前所有失败，不重写报告状态。正常Run允许明确失败；debug24只作为24关/23商店流程覆盖，不宣称胜率或平衡通过。

Windows独立writer/reader恢复已实际通过：writer32164在pending回执后78ms被本脚本终止，reader33676恢复已确认的窗口1280×720并清除pending，保存和备份一致。见 `evidence/v06/display-recovery/attempt01`。该子场景通过不代表独占全屏或性能通过。重现工具为 `tools/verify_v06_display_recovery.py`，仅终止自己启动的进程，不直接修改设置文件；原始启动日志只保存在忽略的 `artifacts/cache/v06/`。

三包、冻结manifest与发布使用 `package_v06.py`、`release_v06_manifest.py`、`publish_v06_release.py` 的显式诊断路径；正式默认门槛和Mac自身标准不变。冻结manifest位于 `Releases/p0.6.0/review-candidate/review-candidate-manifest.json`，其remoteRestoreStatus是上传前快照，后续真实下载/恢复/发布另存不可变证据，不为更新状态破坏既有哈希。M5和真人验收待回传；Windows前台显示条件恢复后仍需重验P22和性能。整个版本不能标为ACCEPTED。
