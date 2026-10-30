# P0-002 macOS 实机验证与最终封装

输入规格：`docs/specs/P0_v0.5.md` 第34–35节、附录E。设备由用户确认：macOS 26.6.1、Apple Silicon M5；脚本仍读取实际系统/CPU/GPU，不把用户提供的环境写成已经实测。本工具准备完成不代表 Mac 已通过。主协调者负责提供同源 Windows/macOS 构建与真实 Windows Player 结果，用户或获准的远程执行者运行 Mac，独立审查者验收回传证据。

## 用户执行

使用完整的 Mac 验证工具包，里面应含以下结构。`inputs` 是主协调者在 Windows 构建/验证完成后填入的真实产物；源码仓库中的脚本不包含预填通过结果。

```text
DicePrototype-MacVerification/
  VerifyMac.command
  verify_mac.py
  package_checks.py
  compare_replays.py
  inputs/
    macos.zip
    BUILD_IDENTITY.json
    normal-run-trace.json
    windows-results/
      player-report.json
      normal-replay.jsonl
      debug24-replay.jsonl
      cross-platform-checkpoint/
        active-run.json
        command.json
        expected.json
```

Mac 需要可用的 Python 3.9+，不需要安装 Unity、Blender、Rosetta、第三方 Python 包或 Xcode 编译工具。缺 Python 时脚本会提示，不自动安装。先打开终端，输入 `bash `（保留末尾空格），把 `VerifyMac.command` 拖入终端后回车；这样也不依赖下载时脚本自身是否保留了执行位。参数齐全的工具包不需要额外输入。

脚本在桌面创建带时间的新目录。自动过程包括正常轨迹、24关流程、三种分辨率截图和180秒压力采样，通常需要数分钟。保持电源接通和游戏窗口可见，不最小化，不在自动阶段操作游戏。不要同时运行另一份测试或高负载程序。原压缩包、现有游戏存档和其他进程不会被覆盖。

如果系统拒绝运行，保留系统提示，回传脚本已生成的证据。脚本不执行 `xattr -d`、不关闭 Gatekeeper、不重签或伪称公证。隔离包未通过系统评估时停止；由 Mac 上的正常系统授权或后续签名流程解决，再重新验证。不要通过删除隔离属性让脚本变绿。

完成后回传输出目录中的：

- `DicePrototype-p0.5.0-macos-universal.zip`：Mac 上用 ditto 生成并从全新目录解压验证的最终候选包。
- `MacVerification-Evidence.zip` 与 `evidence-package-sha256.json`：实际过程、硬件、签名、结构、截图、帧数据、存档和逐步比较结果。若失败也回传，失败不会被改成通过。
- 按下面的人工检查记录得到的结果。日志含本机文件路径；公开提交前由协调者检查，只脱敏身份路径，不改测试数值。

需要自选输入时可以运行：

```bash
bash /绝对路径/VerifyMac.command \
  --package /绝对路径/macos.zip \
  --trace /绝对路径/normal-run-trace.json \
  --windows-results /绝对路径/windows-player结果目录 \
  --identity /绝对路径/BUILD_IDENTITY.json \
  --output /绝对路径/尚不存在的输出目录
```

输出目录必须是新目录；脚本不会删除已有结果。`--timeout` 只控制整个 Player 进程超时，默认1800秒，不改变180秒压力场景和帧时间门槛。

## 自动行为和证据

1. 保存输入压缩包原始字节、SHA-256、大小和隔离状态；检查 ZIP 路径、大小写冲突及符号链接不越出解压目录。
2. 系统 ditto 解压到新目录，读取 `Info.plist`，检查 `.app` 主程序和每一个 Mach-O 文件均含 arm64、x86_64。纯 Python 解析薄/胖头和实际 slice，不只根据文件名猜测。
3. 只在新解压副本上给缺少执行位的 Mach-O `MH_EXECUTE` 文件补用户执行位，记录逐文件前后 mode；不改内容字节，不修改原包，不给数据文件递归 chmod。
4. 记录 `codesign -d`、`codesign --verify`、`spctl --assess`、quarantine。签名/公证状态以这些原始输出为准，不预标 Developer ID 或已公证。
5. 使用 `/usr/bin/ditto -c -k --keepParent` 封装完整 `.app`；对最终 ZIP 重新计算哈希，再解压到另一个全新目录。逐文件字节、权限、符号链接和原生架构须与封装前一致。
6. 通过 `/usr/bin/arch -arm64` 启动最终包的新解压副本；传入正常轨迹、独立证据目录及 Windows checkpoint。实际 `RuntimeInformation.ProcessArchitecture == Arm64` 为运行架构证据，启动期系统 `sample` 留下辅助原生进程记录；不运行 Rosetta 路线。
7. ProbeRevision7 实际读取 Windows schema3 checkpoint，继续 Submit、写盘重载并验证重复命令幂等；不修改原始 Windows 存档。随后运行完整正常/Debug24/UI/性能探针并导出 Mac checkpoint。
8. 比较 Windows/macOS 的内嵌 sourceCommit、配置/黄金集哈希、配置版本、轨迹哈希，以及每条正常/Debug24 命令的种类、连续序号、分数、货币、阶段、规则 RNG 和 `ruleStateSha256`。忽略跨机 GUID，哈希仍包含完整规范化玩法状态。
9. 生成证据 ZIP；退出0仅表示自动门槛通过，摘要状态仍为 `automated_checks_passed_manual_checks_pending`。Intel 标记始终为“构建含 x86_64，实机运行未验证”。

Apple 官方封装方式：[Packaging Mac software for distribution](https://developer.apple.com/documentation/xcode/packaging-mac-software-for-distribution)。工具实际执行与系统输出保存在证据中；准备阶段没有冒充 Mac 执行。

## Mac 上仍需人工执行

自动探针结束后，在 `final-package-fresh-extract` 内打开最终 `.app`，完成以下检查，记录日期、实际设备、通过/失败及现象。不得预填通过。

| 条件 | 操作与期望 | 记录 |
|---|---|---|
| P04 版本 | 主菜单打开版本页，sourceCommit、p0.5.0/schema3、配置和黄金哈希与提供的身份一致；长哈希可完整读到 | 未执行 |
| P14 鼠标/触控板 | 在正常挑战分别用鼠标、触控板点骰待锁、确认锁定、调序、打开牌型手册；H和Esc正确打开/关闭；无重复动作 | 未执行 |
| P13–14 滚动/模态/字体 | 手册从顶部滚到底，13行完整；滚轮、触控板和拖动滚动条不作用到背景；中文无方框；720p/1080p/1440p截图检查无遮挡 | 未执行 |
| P07 达标 | 达标但剩余提交时保留关内；继续提交和结束本轮均可用，待锁或忙碌时不能结束；收益只支付一次 | 未执行 |
| P16 退出续玩 | 关内锁定后从菜单正常退出程序，重新打开继续；记录前后骰面、顺序、次数、金币、附魔一致。进入商店再退出/恢复，报价与附魔不重抽 | 未执行 |

正常存档的路径由 Unity 的 `Application.persistentDataPath` 决定。不要为了验收删除或覆盖用户已有存档。自动探针已使用输出目录下的隔离存档；人工如已有正式挑战，先正常备份并记录，或使用独立macOS测试用户。

## 回传后的协调者检查

- 独立审查实际截图与性能原始数据，不只看脚本退出码。
- 取 Mac 报告目录中的 `cross-platform-checkpoint`，在同源 Windows 包使用 `--verify-import-save <该目录>` 真实加载并继续，完成反方向兼容性。Mac 自动结果只验证 Windows→Mac。
- 用最终 Mac ZIP 的新 SHA-256/大小更新共同 release manifest；禁止继续沿用 Windows 预包的哈希。若后续签名、公证、附票或重压，重新计算哈希和新解压运行证据。
- 与 Windows 最终包在同一个 Git/LFS 交付提交上传，干净检出/LFS下载后分别检查最终字节与运行。脚本不自动推 Git，也不代签P15。
- 双平台必测项、人工输入/退出续玩和远程恢复未完成时，不标整个版本交付完成。

## 工具自验

Windows 上可运行以下合成测试和离线比较器，不会启动假 Mac 或伪造原生验收：

```powershell
python -m unittest discover -s tools/macos -p 'test_*.py' -v
python tools/macos/compare_replays.py --windows-results <真实Windows目录> --mac-results <真实Mac目录> --output <比较报告.json>
```

当前工具测试与保留的失败记录见 `evidence/v05/macos-preparation/`。这些仅证明解析/拒绝/比较逻辑，不证明 macOS 命令、包结构或 Player 已实机通过。
