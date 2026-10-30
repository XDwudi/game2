# 静态字体、PlayMode03与Mac工具复核

审查者 `/root/v05_spec_audit`，2026-10-30。本次继续只读生产；没有启动Unity或执行Mac应用。

## 字体与原生回归

采用已读的 `unity:optimize-text-mesh-pro` 技能规范检查主静态字体/动态回退。Editor退出后独立解析实际资产：主字体Static、2048×2048、48pt、padding7、scale1，639个请求字符全部存在；43份配置/游戏UI/Core源文本零缺字符。动态fallback引用GUID正确，1024×1024配置、相同48pt/padding7/scale1、Clear Dynamic Data On Build开启，初始字符表为空、序列化空atlas为1×1。此配置避免主字体在退出/构建时清空，fallback可以从打包源字体补充非预定字符。证据 `font-static-fallback-after-quit.json` 绑定实际资产SHA。

原生PlayMode03为24/24 Passed，NUnit `nunit-20261030-051921.xml` 与 `playmode-03.json`逐叶子名称、状态、计数一致，0失败/跳过/未定；24项与PlayMode02相同。`unity-edit02-play03-report-audit.json`保留对照。EditMode02覆盖输入未变，因此仍可复用552/552；没有因字体变更修改核心计分或持久化规则。

`unity-prebuild-pdb02-static-font.json`核对5程序集47项目源文档，DLL与PDB GUID一致、源checksum全部匹配。它包括新的V05Setup静态字体准备逻辑。字体资源布局效果与最终包退出续玩仍需实际Player；静态字符表存在不等于视觉可读性已经验收。

## Mac工具复核进展

已阅读VerifyMac.command、verify_mac.py、compare_replays.py、package_checks.py及其合成测试，独立在Windows重跑 **28/28通过**；运行前后脚本SHA一致，证据 `mac-tools-independent-run01.json/.log`。这些测试没有执行macOS原生命令，不能当作M5设备验收。

实际脚本调用参数为argv数组，无shell拼接；操作在新建输出目录内，对原包只读取/复制并保留哈希。解压与执行位修复针对复制品，仅为Mach-O执行文件补owner execute，未对整个目录递归chmod。没有删除quarantine、改签名、公证或推送Git行为；超时只终止自己启动的Player进程。平台、配置/黄金/源码身份、连续命令、状态hash、Windows存档导入、完整压力和arm64身份均参与结果检查；Mac产物仍明确manual_checks_pending，逆向存档恢复与真人输入另验。

独立额外发现 **MAC-R01：归档中声明的symlink之下再放成员可绕过链接解析**。纯检查复现为`a -> '.'`、`a/b/`、`a/b/c -> '../../outside'`、`a/b/c/file.txt`。检查器旧版本接受，但实际第二链接父路径经a解析后是b，目标成为`../outside`。审查者只生成合成ZIP并运行预检查，未解压、未创建symlink、未执行内容。失败保留 `mac-nested-link-negative.json` 和 `.fixture.zip`。

该问题已交root修复，建议直接拒绝任何位于已声明symlink严格子路径下的成员（casefold比较），同时继续允许常规Framework `Current -> A`链接本身和真实Versions/A目录。**截至此记录，Mac工具制作质量仍为REWORK，须对新增负例修复重验；Windows构建与字体不受该缺陷影响。**

## MAC-R01 修复后复核

后续修复采用 NFD/casefold 路径比较，拒绝声明的 symlink 之下的归档成员，并按 POSIX 分量逐步跟随 link target，而不是先简化目标路径再解析。独立重跑扩展后的 **36/36** 合成测试通过，旧失败 ZIP 现在以 `Archive member has a symlink ancestor: a/b` 拒绝；合法 Framework 链测试仍通过。运行前后全部脚本哈希一致，证据 `mac-tools-independent-run02.json/.log`。MAC-R01 关闭，保留上节原失败结论和输入；工具审查通过不代表 macOS 原生执行通过。
