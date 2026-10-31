# v0.6 构建、测试绑定与交付工具独立复核

审查者 `/root/v06_spec_review`。本文仅记已实际完成的检查；不代替 Player、macOS 或人工体验验收。

## 门禁缺陷与修复证据

初轮 `gate-run01.json` 的 12 个合成用例中，7 个构造证明被错误接受：失败套件、命名空间失败节点、错误 XML 根、混合平台标签、旧的未绑定实际执行报告、重复 Core 程序集冒充五程序集、缺失 Runtime 文档。没有改验收阈值；实施方加入严格 NUnit 解析、全部实际发现的测试名称完全匹配、五个唯一程序集、全部 C# PDB 文档和运行开始/结束实际加载程序集 MVID、DLL SHA、完整源快照及报告哈希绑定。

修复后的 `gate-run03.json` 为 **21/21**，包含缺绑定、开始/结束模块变化、漏掉 Runtime、重复模块、测试期间源变化、未测试新文件、缺失或重复 PDB 文档等负例。`gate-run02.json` 是此前同为 21/21 的中间记录，保留不覆盖。

生产 C# 消费端最初遗漏的报告/运行绑定/PDB 必需引用由 `gate-consumer-run01.json` 复现：9 项中 3 项错误接受。增加显式证据引用并纳入全部文件哈希清单后，`gate-consumer-run02.json` **9/9**。该检验只调用生产 `ValidateGate`，Unity API 为无副作用桩；从未调用构建或以合成数据冒充游戏运行。

`source-close-review01.json` 证明以上 Python 门禁和 C# 消费端与通过负例时的源码一致。实际 `python -m unittest discover -s tests -v` 的 **20/20** 见 `quality-check-unittest.log`。

## 实际 Unity 与产物绑定

独立解析最终真实报告 `nunit-20261031-125401.xml` 和 `nunit-20261031-125432.xml`，完整发现清单逐项一致：**EditMode 776/776、PlayMode 28/28**，无失败、跳过或不确定项。两轮运行 start/end 实际加载的五模块 MVID 与 DLL SHA 完全匹配独立 PE/PDB 检查；60 个项目 C# 源文档无差异。

`unity-evidence-review04.json` 在 Editor 退出且 Windows 构建结束后再次检查全部 601 个源文件，无任何差异；`unity-final-pdb03.json` 为独立 PDB 证据。此前 review01/02 真实发现字体资产等变化，review03 在构建期间发现 InputSystem 临时 preloadedAssets；全部保留，均不能独自作为最终绑定通过。review04 明确确认恢复后的最终状态。

`windows-built-pdb01.json` 进一步直接解析 Windows02 产物的 Core/Runtime DLL 与 PDB，2 个模块覆盖全部 **33 个 Core/Runtime/UI 生产 C# 文件**，0 mismatch。`source-close-review01.json` 校验所有应覆盖生产文件集合、2077 项托管复核的 34 个输入未变，以及九个压力采样/指标方法与原提交 `2db287a` 的规范化方法体一致。这里只证明源与构建关联，尚不代表该 Player 已运行通过。

## 打包与 Mac 工具

已只读复核 package/release/publish 与 Mac 检查脚本。打包消费完整冻结身份、严格 NUnit、全发现清单、实际运行模块/源绑定和 PDB 全覆盖；ZIP 拒绝覆盖既有证据并验证路径、CRC、字节、SHA、符号链接及 Mach-O 架构。发布代码限制仓库与 HTTPS 认证域，跨域重定向剥离凭据；不删除或覆盖已有不同附件，发布需要完整远程下载复验。审查者未执行 GitHub 写操作。

Mac 脚本只在新副本上运行系统 ditto、实际 arm64 Player、Metal、Windows 检查点和独立进程显示恢复。默认不安装软件、不去除 quarantine、不绕过系统安全；Mac/Intel/人工状态保持未验。合法 Mac 非适用的独占非原生分辨率分支被严格限制为该平台的该唯一检查 ID，不接受其他必需检查冒用 not_applicable。

`macos-delivery-tools-test01.log` 保留过渡期间的合成 NUnit fixture 格式错误（57 通过、1 错误）；实施者修复 fixture 后，独立 `macos-delivery-tools-test02.log` **67/67**。这些是工具离线测试，**不是 macOS 实机通过**。最终前后源码绑定的重跑见 `delivery-tools-final01.json/.log`。

另实际只读调用打包工具的 `validated_gate()`，针对当前 `6f9e285` 真实源、两份完整 NUnit、发现清单、PDB 和实际执行绑定验证 **609 个源/证据条目通过**，见 `actual-package-gate01.json`。没有创建归档或执行发布，因此此结果不能提前宣称三个最终 ZIP 已通过。

## 当前结论

测试完整性和冻结绑定检查通过；Windows 构建源关联通过。最终 Windows Player 实际功能、压力数据、截图、双进程恢复、三个 ZIP、远程附件下载复验仍按实际运行证据分别验收。Mac 原生实机、双向检查点后半程、物理鼠标/触控板、多屏拔插、人工体验与 ACCEPTED 不在本工具结果中代签。
