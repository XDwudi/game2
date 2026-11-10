# RC2 发行工具：独立限域复核

结论：当前三工具增量未见阻断项，可用于后续真实 RC2 封包/发行流程；**未执行封包、网络或发布，不签其实际结果。** 复核人 `/root/project_audit`，只读 `dice_sample` 的工具与测试改动。独立实际运行 `python -X utf8 -B -m unittest discover -s tests -v`，61/61，exit0；4 个源文件运行前后 SHA 完全相同，详见 `release-tools-test-binding.json`。测试使用合成包/Mock HTTP，不是真实 GitHub 证明。

原 `validated_gate`、NUnit完整发现集/逐叶/首尾源绑定、PDB、构建 StrictMode 报告、Player真实轨迹和失败性能 CSV 校验未删改。rc.1 默认调用兼容保留；新 tag 只允许 `p0.7.1-rc.N`，必须显式新 package/proof 目录和 manifest，旧两个包/proof目录及子目录拒绝复用，输出仍 x 模式拒绝覆盖。

tag 贯穿 CLI→manifest→三个 package proof→本地及远端 ref→精确 draft匹配→POST/PATCH→独立 receipt/download目录→三下载证明。显式rc.2与rc.1 manifest/proof串线先拒绝；下载证明还对anchor。新 draft 与最终 publish 都显式 tag_name/target_commitish，避免上轮省略tag引发alias。没有 DELETE、覆盖旧附件或修改已公开发行的路径；mock HTTP 实际走 main，断言 rc.1 ID 不被访问写入。

`validate_menu_table` 另绑定两张生成原图/提示词、记录、运行背景与当前 gate 内 shader/material/Importer/menu源；保留旧127资源catalog，不以改旧资源数量伪造新输入。新增发行标题/正文只在有menuTable时标注界面/桌面更新，人工美术仍 false。3完整ZIP/download门禁没有放宽。

保留边界：这些工具只发布 manifest 中三个具名成品。当前 b005e00源已推送，通常无需旧 `--existing-release-anchor-commit` 模式。真实 draft/asset/完整下载/公开地址仍应逐证据核验；外部API失败不是通过。旧下载版诊断 cache adapter必须显式传新tag及调用新的receipt/download目录函数，原默认调用只适用rc.1，详见 `BUILD_PACKAGE_ROUTE.md`。

核对 SHA：package `ea3edd156c2bd256d3d35405ab7af070d9d26693a2a82f4659db10d4adb5068e`；manifest `49fcbcc37db242c98337da4611cb516955a02756353bd30bd2eae7d927694ed7`；publisher `382aec5a5bf1c691ce03c5fd920f51ce819bc99d478edf7bf38d9a53d5374c76`；测试 `a36615c8694a557eaa89b0e20ca40abbe4e77bcf1d1eb3bda4f0eb77d04e676c`。之后若改变这些字节，按差异复核，不套用此版本结论。
