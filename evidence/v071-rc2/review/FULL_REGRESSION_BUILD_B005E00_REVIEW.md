# b005e00 完整回归与实际构建复核

复核者：`/root/project_audit`。结论：**本报告覆盖的完整 Editor 回归、源码/程序集绑定及双平台交叉构建通过；不代表整版验收。** 当前源码 `b005e0088b227b26e2f8fc44f1c59f68df0c0a35`，Unity `6000.6.7f1` / Mono。

- 实际 `nunit-20261110-101318.xml`：EditMode 1018/1018；`nunit-20261110-103648.xml`：PlayMode 185/185，1377.0017875 秒。逐叶名称无重复，均 Passed，零失败/跳过/不确定，与对应完整 discovery 精确一致。不是把此前 17 次局部检查折算成全量。
- 当前 gate 与冻结副本逐字节一致；1789 个 gate 文件逐 SHA 核对通过。其中 1782 个当前 Assets/Packages/ProjectSettings 文件集合完整匹配，1781 个测试快照行与两个报告首尾绑定一致。游戏源树与该 Git commit 精确一致，生成的 BuildIdentity 单独校验。
- 5 个 Editor 程序集、198 个源码的 Portable PDB 实际读取零差异；符号必须与 DLL 相配。首轮 Library 审计和重新打开冻结副本结果相同，10 个 DLL/PDB SHA 均与测试绑定一致。
- Editor 退出时动态字体缓存确实改变，旧字节及差异保留；恢复后字体 SHA `ad4bd727b9dbbb1cdbe302c41404c2682632a2d93f0c83e7eeaa18ecfa4cffc0` 与测试、gate 和 Git 源完全相同。该恢复没有重新生成哈希来掩盖源码变动。
- 实际 Windows 构建 `build-windows-20261110-103917.json` 成功：GUID `75e7f6b0f1c34075a6527abbc6829e8b`，0 错误、3 警告。实际 Mac 构建 `build-macos-20261110-104006.json` 成功：GUID `b523ca601806410ca9fbb7ff3d943c8c`，0 错误、79 警告。两者分别重新打开 Player Portable PDB，均 2 程序集、105 源码、零差异；当前 DLL/PDB 与报告 SHA 一致。
- Mac 的 6 个实际原生组件均含 x86_64/arm64。筛选后的 105 条 warning 文本与旧 41aface Mac 构建归一分类、次数完全一致，无新增 Felt/桌面导入或 shader 警告。原始 warning 行数与 BuildReport 79 的计数口径不同，不混称相等。

完整可重现核对脚本为 `verify_complete_b005e00.py`，实际执行成功。文件、源码集、完整测试名称和 SHA 见 `FULL_REGRESSION_BUILD_B005E00.bindings.json`（SHA `00568f203e6e089a3d35127edff4e44839a24cc178906b95a392df7acaf42536`）。Portable PDB 四份报告及 Mac native 报告均位于 `evidence/v071-rc2/unity/`。

Windows 实机、性能、回放及封包另见本目录 `PLAYER_AND_PACKAGES_B005E00_REVIEW.md`。本次没有运行 Unity、修改生产源或验收阈值；Mac 实机、签名、公证、人工风格与体验未因此通过。审查者曾参与既有 UI 和本轮测试候选编写，本报告是实际执行/绑定的复核，不是自身制作的独立艺术质量签署。
