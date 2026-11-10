# RC2 Mac 构建警告限域核对

当前 Mac `build-macos-20261110-104006.json` 为 Succeeded / 0 errors / 79 warnings。只读过滤任务 `cache/v071-rc2/build/macos-Editor.log`，与旧 41aface 同类构建日志 `artifacts/cache/v071/full-art-build-41aface/macos-editor.log` 比较，没有输出原始启动/账户参数。

两份日志均筛得105条warning文本行（包含重复编译/提示，**不等于 BuildReport 的79个计数口径**），去源码行号后的警告文本及次数完全相同，无新增类别：旧 FindObjects/FindFirstObject API 的 CS0618、Unity/URP包中潜在未初始化变量，以及HDRP/Terrain等非本项目专用shader的subshader移除/pragma提示。未出现本轮 `DiceCraft/Art07/Felt`、新桌图导入或自定义新shader警告。

据实际完整警告集合相同及本次构建0错误，未发现新菜单/桌面引入的构建阻断。不能因此关闭旧交叉编译风险、声称这些警告无害，或签Mac/Metal真实运行通过。安全过滤结果及旧/新类别在 `macos-warning-classification.json`；原日志留本地，未全文转印。
