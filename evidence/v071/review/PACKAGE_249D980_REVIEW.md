# 249d980 本地三包独立复核

**结论：通过本地诊断测试包的结构、完整性、构建绑定与失败声明检查；未发现需要返工的封包问题。** Windows 性能及独占全屏仍失败，Mac 尚未实跑，本结论不是整版 ACCEPTED，也不是发布授权。

复核者：`/root/dice_sample`。输入源 `249d980f2ae3615d573df089403f298a6713ae96`；实际证据时间见 `PACKAGE_249D980_REVIEW.bindings.json`。未修改 ZIP、源码、工具或原构建，没有启动额外 Player。只在新的 `cache/v071/package-review-249d980/` 下安全解压，并写本报告及 bindings。

## 三包实际结果

| 包 | 字节数 | 文件条目 | SHA256 |
|---|---:|---:|---|
| `DicePrototype-p0.7.1-win-x64.zip` | 93,949,463 | 311 | `9b72cb68f18ad05cf0fa394b608373e2efadcbe174e4a583686ad818f1c03145` |
| `DicePrototype-p0.7.1-macos-universal.zip` | 134,324,560 | 306 | `e2644cf5818927ede765b672786c81f655234fc4161f3ca3386a3e8dbcb61783` |
| `DicePrototype-p0.7.1-MacVerificationKit.zip` | 134,427,883 | 16 | `6ff75e485cd3931f34639a36afeefd3b46d0475c90c4322400b58932bf6147b2` |

三包均位于 `Releases/p0.7.1/review-candidate/`。整包 SHA/大小与父任务输入及各 package proof 一致；三次实际 ZIP CRC 检查通过。全部 **633** 个条目逐项重算 SHA，再对新解压文件重算 SHA，均与证明一致，无多件/漏件。安全目录检查通过：无路径逃逸、大小写/Unicode 重名、危险链接；本次三个包均无需创建符号链接。解压目标逐个解析并限制在对应新目录内，没有覆盖既有文件。

## 与冻结构建的关系

再次只读验证 gate 与源树，完整绑定仍成立。Windows 包中 **305** 个构建文件、Mac 包中 **300** 个构建文件，与当前实际 `Builds/v071` 文件集、字节数/SHA，以及先前独立 `BUILD_249D980_REVIEW.bindings.json` 三方一致；每包其余 6 项恰为身份、候选声明、README 和 3 份字体许可。

- Windows GUID：`2af8d019326049369f05314a26d58bcc`。
- Mac GUID：`6b7f1b4093024970b2b044cb2e8e26f8`。
- 两包 `BUILD_IDENTITY.json` 均为 `p0.7.1/art0.7.1`、规则 `p0.6.0/schema4`、Unity6000.6.7f1/Mono 与指定源；构建报告的 SHA/GUID 均匹配。
- 选定 Windows Player 报告 GUID 与实际 Windows 包的构建 GUID 一致，未把另一份同源重构建的结果套用到此包。

## Mac 包内结构和权限元数据

从新解压后的 Mac 包实际解析 Info.plist，`DicePrototype.app/Contents/MacOS/DiceCraft P0`、Resources/Data/Managed 与 Runtime DLL 均存在，Bundle 版本 `0.7.1`。所有 **6** 个原生组件重新解析 Mach-O，均且仅含 arm64+x86_64，切片检查通过，与构建 proof 相同：主程序、UnityPlayer、libmono-native、libmonobdwgc、libMonoPosixHelper、Burst bundle。

ZIP 的 Unix 权限元数据实际核对：MH_EXECUTE 主程序为 0755，其他原生 dylib/bundle 为 0644，均为常规文件类型。此处验证的是 ZIP 元数据与 Windows 上的新解压内容，**没有执行 Mac ditto、验证目标 Mac 实际权限、签名/公证或原生运行**。不能把包内 Universal 结构通过写成 Mac 实机通过。

## 字体许可与实际说明

两个 Player 包均完整包含以下文件，字节与实际归档源一致：

- `Licenses/NotoSansCJK-OFL.txt`：来自 `DicePrototype/Assets/Game/Fonts/LICENSE.txt`。
- `Licenses/NotoSansSC-Bold-OFL.txt`：来自 `ArtSource/Art071/Fonts/LICENSE.txt`。
- `Licenses/NotoSansSC-Bold-COPYRIGHT.txt`：来自 `ArtSource/Art071/Fonts/COPYRIGHT.txt`。

两包 `开始游玩.txt` 完全相同并能正确按 UTF-8 阅读，与当前玩家说明一致。实读内容正确写明源码、1013+163 通过、双构建 GUID、普通 145 步与 Debug24、仅首批 4 张插画/其余 67 旧图标，以及以下尚未通过的状态；没有宣称完整验收或线上发布。

三份候选声明均与选定真实参考报告生成的声明逐字段一致：

- 参考目录：`evidence/v071/player-249d980-0112/native-deferred-independent/player-20261109-012013-a25233c9/`。
- report SHA：`d80997d7065ea1ce671b17af7356066036f1076d00975d42159a8f1c5e3316ae`。
- `reviewCandidate=true`、`allowFailedWindowsPerformance=true`；performance `FAILED`。
- 180.0039074 秒采样，P95 **16.9258 ms > 16.7 ms**；GPU 有效覆盖 **91.3728209% < 95%**，覆盖缺口仍明示。
- 原生显示状态 `PENDING_P22_NOT_PASSED`；README 明确严格独占全屏确认失败，独立恢复通过没有合并成 strict 通过。
- Mac `NOT_RUN_ON_MAC`；`fullWindowsPassed=false`、`fullReleaseAccepted=false`。

## M5 工具包

16 项输入全部匹配 proof 的 SHA；内部 `inputs/macos.zip` 与上表 Mac 原 ZIP 的 **134,324,560 字节完全相同**。因此其嵌套 Mac 内容包含同样的 306 项和三份许可，并非仅按文件名推定相同。

- 四脚本与当前 v071 工具源逐字节一致；三个 Python 脚本实际执行了语法编译检查，没有执行脚本或导入其运行流程。`VerifyMac.command` 为正确 bash shebang 和 LF 换行，说明按 `bash VerifyMac.command` 调用，不假定 Windows 写入的脚本执行位。
- 新增的两份 Bold 许可名称已包含在工具包内 `verify_mac.py` 的复解压元数据核验中。
- 内部身份、两条规则轨迹、Windows report、普通/Debug24 逐条回放及三个跨平台检查点，与实际选定源文件的字节一致。
- 对**工具包中新解压的** Windows 参考再次执行既有受限性能失败验证，通过其明确允许的诊断范围；没有修改状态、采样时长或阈值。
- 本次参考是 native-deferred，而非 strict-failed-performance 路径；当前工具约定仅后者要求随 kit 加原始 performance CSV。因此本 kit 没有该 CSV 符合既有分支，不能把它表述为包含全套原始性能数据；完整原始数据仍在项目证据目录。
- 实读 `执行说明.md`，记录了实际失败数值和 GUID，要求 Mac 保持原严格验收标准；没有 Gatekeeper 绕过、重签或放宽 Mac 门槛的步骤。

`PACKAGE_249D980_REVIEW.bindings.json` 保存每个 ZIP/proof/参考的 SHA、633 项清单、解压比对、构建文件关系、Mac 原生结构与模式、工具包输入结果及明确失败范围。后续任何包内容或文档重封使对应 SHA 改变时，需重核该包；本结论不自动转用于新 ZIP。
