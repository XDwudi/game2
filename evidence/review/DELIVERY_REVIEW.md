# Windows交付包独立复核

`/root/spec_audit`，2026-10-29 UTC。**本地交付包完整性与已验收构建身份通过**；本次没有重新启动游戏，没有重复功能/性能验收。详细逐文件结果见`DELIVERY_REVIEW.json`，13项实际检查全部通过。

- 包：`Builds/DiceCraft-P0-Windows.zip`，**84,571,347 bytes**。
- SHA256：**`d0bf25b18326a9a02f28326be1151895768bce22d5ad1c9ffa9e9eddbaa0cbde`**。
- 生产源为`f060098533d26e99e7eba7d0360ecfea78cbe517`，打包清单提交为`821e8631a9e7bde1c68837aedfabd5a2eab1ffa0`。独立只读Git差异核查确认两者之间Assets/Packages/ProjectSettings/art无变化，这些路径当前无未提交修改，清单`sourceStatus`为空。

实际读取并解压校验ZIP数据流：317个条目恰为313个运行文件和4个交接文件。313项的解压后SHA256与字节数均同时等于实际`Builds/Windows`文件及manifest，完整ZIP CRC检查通过。EXE、Core及Runtime DLL分别匹配此前独立验收的build10，Runtime SHA256仍为`346b4c1ec85e75ad4f7f15d10f2968c97b1d836e235474878718fab886b43d59`。

四个交接文件为内嵌`build-manifest.json`、UTF-8中文`开始游玩.txt`及两份Noto字体许可/来源文件。内嵌清单与外部清单逐字节相同，字体文件与工程记录逐字节相同。已阅读启动说明，包含完整解压、操作、保存路径、失败重试、原型限制和仓库入口。中文文件名编码正确。

所有条目均位于单一`DiceCraft-P0/`目录；没有绝对路径、`..`、反斜杠穿越、驱动器冒号、Windows大小写碰撞或符号链接。未混入个人/验证存档、备份档、临时文件、Git或Unity缓存。实际Build目录唯一未打包文件为Unity明确标记不要随游戏交付目录中的`x86_64/lib_burst_generated.txt`，排除正确。

据此，`FINAL_REVIEW.md`中AC-SOURCE的**本地包交接部分关闭**。原技术结论及真人体验/风格待验、移动端N/A保持不变。最终清单和本报告的远端提交由root随后推送；本报告此时不宣称该最终指针已发布，待收到后只核对指针与文件，不重新扩大验收范围。
