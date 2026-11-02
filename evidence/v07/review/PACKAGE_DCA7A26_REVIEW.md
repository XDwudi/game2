# dca7a26 三包独立封包复核

复核者：`/root/v07_perf`，非三包打包实施者。范围为本地最终压缩包、manifest、对应原始报告与文档绑定；未联网、未启动游戏、未修改包或生产内容。复核时间见相邻 bindings。

**结论：本地诊断候选的封包规格符合性与封包完整性通过。Windows 性能及完整原生显示仍未通过；Mac 实机、远端全量下载及新解压运行、人工美术/声音/体验均未据本报告放行。整版不是 ACCEPTED。**

## 身份与完整性

三个包位于 `Releases/p0.7.0/review-candidate/`。独立逐字节计算外包 SHA-256，并对每个 ZIP 项完整读取到 EOF（触发实际 CRC 检验），计算逐项 SHA、大小、Unix mode，与对应 `evidence/v07/packages/*review-candidate.json` 全项比较。

| 包 | 字节 | ZIP 项数 | SHA-256 |
|---|---:|---:|---|
| DicePrototype-p0.7.0-win-x64.zip | 87,218,149 | 309 | `c823056517851a786cfe8a185c43ad6829e3bdf2f5d8686eb9b17ff6a39169b1` |
| DicePrototype-p0.7.0-macos-universal.zip | 127,598,578 | 304 | `437985740644806f220da8217aaf187a19c816421a20dd917e5578f2190641b6` |
| DicePrototype-p0.7.0-MacVerificationKit.zip | 127,707,336 | 16 | `e3807850f72fa0de3c41f1bed1364254b530356e0086ae78f926d70c8e9e23c6` |

全部 CRC、SHA、大小、proof 清单匹配；无重复路径、NFC/大小写碰撞、绝对路径、盘符、反斜杠路径、`..` 越界或特殊设备项。本次包中没有符号链接。未执行任何归档内代码。

包、proof、两构建报告、manifest、Windows 报告和恢复收据的共同身份相同：源码 `dca7a2610f39d65088b9541549d3465c388fd3e7`，发行 `p0.7.0`，美术 `art0.7.0`，规则 `p0.6.0`，schema4，Unity `6000.6.7f1`/Mono；配置与黄金集 SHA 未变。

- Windows 构建 GUID `388c2eee37244ba48bb656114b6320bf`，实际报告成功、0 错误、3 警告。
- Mac 构建 GUID `b37777db216941e2b54f110d241df237`，实际报告成功、0 错误、77 警告。不能把交叉构建成功当作运行或着色器实机验收。
- 真实 NUnit XML 分别为 Edit 1013/1013、Play 158/158，零失败/跳过；两份运行前后 binding 的 1370 个源文件及五程序集均无漂移，与 build-gate 的对应项相同。gate 额外八项仅为身份与测试/发现/PDB证据，均核验实际 SHA；不把 1378 门禁文件误称为测试源数量。

最终 manifest SHA：`c1e2b0f886b62fc8aef65bbdc9c8bd5c533099f476da26d822a3b09f42bbe13c`。与保留原件 `a810be1e96d696073e2557a23b7c92df8b325f832de00238aa1066fbd32fe9ac` 比较，唯一 JSON 变化为 `/platforms/0/packagingStatus`，明确改为远端新解压/运行 **pending separate verification**；三个 ZIP 字节不变。生成器 `tools/release_v07_manifest.py` 当前 SHA 为 `1f5b43064d0aa1d2a4ea7eff7a5b8f1096c0a6b6ec6162a2506b89bfd77da19d`。

## Mac 实际结构

独立解析 fat 架构表及每个 slice 内的 64 位 Mach-O header，检查 CPU 一致、边界、对齐、无重叠。以下六个组件均含且仅含 `x86_64` 与 `arm64`：四个 Frameworks dylib、主程序、`PlugIns/lib_burst_generated.bundle`。

Info.plist 指定的主程序为 `DicePrototype.app/Contents/MacOS/DiceCraft P0`，实际存在、mode `0100755`，两 slice 均为 `MH_EXECUTE`。其余五组件为动态库类型，mode `0100644`；没有将动态库的非执行位误判为主程序不可运行。

包内存在 `_CodeSignature/CodeResources`，这里只确认文件存在，没有评估签名有效性、公证、Gatekeeper、quarantine 或 macOS 解压后的权限行为。M5/Metal、Intel 运行及 Mac 原生 ditto 封装仍待实际执行。

## 工具包与随包文档

Kit 内 `inputs/macos.zip` 与外层 Mac ZIP 完整 SHA 相同。`VerifyMac.command`、`verify_mac.py`、`package_checks.py`、`compare_replays.py` 与当前正式工具逐字节相同；三份 Python 的 Python 3.9 语法解析通过，导入仅标准库及包内工具。此项不是在 Python 3.9/macOS 上的执行证明。

launcher mode 为 `0100666`；说明明确使用 `bash VerifyMac.command`，不要求脚本自身执行位。默认路径指向包内全部输入；诊断选项仅放行明确失败的 Windows 对照，Mac 启动命令仍为 `arch -arm64` 的严格探针，未传递跳过原生显示参数，Mac 比较仍调用严格校验。

| 文档 | 对应正式源 | 冻结 SHA-256 |
|---|---|---|
| 两平台 `开始游玩.txt` | `docs/PLAYER_README_V07.txt` | `d73761fd225cbe7bce1ef7bd2c347fa90e4e844ae64210d8e657f8d22ac54f50` |
| Kit `执行说明.md` | `docs/tasks/P0-V07-MAC-VERIFICATION.md` | `78ecdf4a750dcfe42a0ad71a3015d8d9ac9d08287803c9d3469c857f7a3679a1` |

实际阅读的说明使用 dca7a26、1013+158、P95 16.7929、GPU 95.6401% 等当前值，明确保留 AudioMixer、公共 Krita 原生补证、Mac 与人工待验；候选 notice 的 `fullReleaseAccepted` 为 false。说明没有把修复局部截图扩展为全面美术验收。

Kit 两份输入轨迹、Windows `player-report.json`、两份 replay 和三个跨平台检查点文件，均与指定 `native-deferred-independent/player-20261102-023622-9ef3d5e7` 原件逐字节相同。另逐步核对正常 145 行与 debug 175 行的指令、状态、分数、金币/次数及 debug 规则哈希，全部符合实际输入；正常结局仍为第 8 关 RunFailed，Debug24 仅作流程集成依据。

Kit 未附 `performance-frames.csv`。当前选用的是显式 native-deferred 参考，现行工具/契约只要求完整 strict 性能失败参考随包带 CSV，因此本次不构成缺件；M5 收件人不能仅凭 Kit 独立重算 Windows 帧分布。此次复核另对仓库原始 CSV 进行了复算，并在 bindings 绑定原件；不把未随包 CSV 说成已随包。

## 失败及恢复范围核对

- 本次 dca7a26 strict-full 与 native-display-only 的原始报告均在 `V07NativeDisplay`、mode0 无法进入确认时失败。实际请求 `2560×1440 ExclusiveFullScreen`，结束读回 `1920×1080 Windowed`、focused=true、canConfirm=false；不借旧版本 native 通过记录覆盖本次失败，也不归因于未经证明的远程桌面、锁屏或驱动。
- 选用的独立 deferred 报告为 `budget_exceeded`、`full-gameplay-ui-perf_native_display_pending`。原始 10,116 行含一行初始部分帧；独立复算 10,115 个完整区间 P95 为 **16.792899999984456 ms > 16.7 ms**，180.005549 秒观察完成，整体性能仍失败。
- GPU 完整区间有效 9,674/10,115，覆盖 **0.956401384083045**；原始缺值 441（status2）、无无效值，达到 0.95 覆盖要求。focusedSampleFrames=10,116 是报告上下文，本 CSV 无逐帧焦点列，不据此增加可见性证明。
- 跨进程恢复另有真实 ready/read 收据，writer PID 48388 与 reader PID 26840 不同、pending SHA 一致、读回实际 `1280×720 Windowed` 与原 confirmed 一致，status passed。该证据只关闭未确认设置的独立进程恢复，不拼接为完整三模式、物理显示变化或严格总体通过。

## 保留依赖与证据

本报告允许将这三包作为准确标记的诊断测试候选继续处理；没有执行发布或授权源码同步。远端附件全量下载 SHA/CRC、Windows 新解压运行仍待；Mac 原生运行/签名安全状态/最终封装、Mac→Windows 反向存档、人工声音/美术/体验与其它既有验收缺项保持原状态。三个 ZIP 的本地通过不改变这些条件。

复核原始计算、每项 inventory 与独立脚本在 `artifacts/cache/v07-package-dca7a26-review/`；正式相邻 `PACKAGE_DCA7A26_REVIEW.bindings.json` 绑定本次全部原始输入及分析产物。没有读取无关历史大包或 raw Profiler。
