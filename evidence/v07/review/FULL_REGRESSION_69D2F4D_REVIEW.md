# 检查点 69d2f4d 完整回归证据独立复核

结论：**同源完整 Editor 回归的证据一致性通过，最终核对未发现未解决的差异。** 复核者 `/root/v07_spec_audit` 仅只读磁盘、Git 和既有报告，未运行 Unity、Player 或构建，未写生产、测试、标准、索引或 HEAD，未外传。输入 art0.7.0 / p0.6.0 / schema4，源码 `69d2f4dc3afa6387d443e827eb0b96892e9f2cfb`，Unity 6000.6.7f1。本结论不签 Player、平台、性能、整版艺术或发行。

## 实际完整运行

| NUnit 报告 | 通过/完整发现 | 失败/跳过/不确定 | 耗时 |
| --- | ---: | ---: | ---: |
| nunit-20261101-115359.xml，EditMode | 999/999 | 0/0/0 | 18.3784747 秒 |
| nunit-20261101-121426.xml，PlayMode | 154/154 | 0/0/0 | 1197.4548182 秒 |

全部唯一 fullname 与各自完整 discovery 精确一致，无遗漏、额外、重复或非通过叶节点；相较上一检查点 4041766 的 999/154，两套名称集合均完全不变。XML 中 Edit 实际时间为 11:53:40Z—11:53:58Z，Play 为 11:54:28Z—12:14:26Z。

原专项 `PLAY_ART07_DICEINTERACT_FirstRollDotsNeverLoopAndFastReducedSkipFocusDoNotCommitAgain` 和 `PLAY_BusyRejectsBypassedUiAndSkipPreservesCommittedRandomness` 均纳入完整 Play 并通过，耗时分别为 5.169244、1.287718 秒。它们验证产品的投掷/Busy/跳过等行为，不等于运行 standalone Player revision20 探针。此前 404 Windows 主流程最后的动画覆盖失败仍保留，不被本轮 Editor 通过改判。

另核对保留的十一份历史 NUnit：075917、080637、085726、092559、092846、095947、102406、105539、111438、113731、114938；全部原名仍在当前完整集合，历史非通过用例本轮同名通过。没有删除或覆盖失败报告。

## 源、实际身份与程序集

两份 XML 的 SHA/大小与对应 binding 一致。四个真实源快照如下：

| 时点 | UTC | 源文件数 |
| --- | --- | ---: |
| Edit 开始 | 11:53:40.4465688Z | 1360 |
| Edit 完成 | 11:53:59.5431158Z | 1360 |
| Play 开始 | 11:54:28.7252096Z | 1360 |
| Play 完成 | 12:14:26.8667329Z | 1360 |

四份有序列表完全相同、无重复路径。独立读取 1360 个 HEAD Git blob，与物理文件原字节 SHA 全部匹配；未做换行归一化，也未仅依赖 clean 标志。构建结束后的最终有界复核再次确认四快照与当前全部源文件一致、HEAD 未变、source scopes clean。

`v07-pdb-post-full-69d2f4d-1215.json` 的五个程序集、169 份源文档核对无差异：文档校验值恰好覆盖当前全部 C#，PDB/DLL 对应、文档归属与数量正确。root 在构建前保存的 `compiled-final` 十个 DLL/PDB 文件，逐一 SHA 与该实际证明一致；四个测试起止时点加载的 MVID/DLL SHA 亦完全一致。**程序集比较使用这份冻结副本，没有把后续双构建重编的 Library 文件追认为测试时 DLL。** 缓存审计中沿用的 `Current` 字段名，在程序集项上指该明确指定的冻结目录；源文件项仍核当前物理源。

11:53:37.9679673Z 的实际资源读取成功，调用先执行拒绝 Play/编译的 guard，再同步导入并 Resources.Load。版本、提交、配置与黄金用例 SHA 均正确；Runtime MVID `40bf476e-46fc-401d-99ac-cf636e7371cf` 与测试/PDB 一致。返回未包含 playing 字段或帧号，未补造这些字段。

正式 gate 实际绑定 1368 文件及明确 999/154 计数。12:18:20.611153Z 只读调用现有 `package_v07.validated_gate()` 再次成功，gate SHA 保持 `a97e1be2bdf7b29963637365f44a0bb7a6111b50b4716596b329177955461f8d`；没有重新生成 gate、identity 或测试。

## 构建期间的临时观察

首次完整审计于 12:16:19.663826Z 落盘时，当前物理源与四快照有一项差异：`ProjectSettings/ProjectSettings.asset`，source scopes 当时 dirty。该原始失败观察保存在 `full-121426-independent.json`，未覆盖或算作首次通过。四份历史测试快照彼此始终相同。

root 随后确认 Windows 批处理构建为 12:15:51—12:16:23，macOS 为 12:16:32.951—12:17:18.631，构建过程存在临时 ProjectSettings 切换。复核者等待两次构建完成通知后，仅重做一次最终当前源核对，结果 `findings=[]`，保存在 `full-121426-after-build-independent.json`。这是构建期间当前文件观察及恢复记录，不是测试期间源变更证据，也不构成本报告对两个构建的验收。

## 归档与恢复

`artifacts/cache/v07-full-play-69d2f4d-121426` 的 152 个唯一隔离目录恰好等于 NUnit output 引用集合；2944 个唯一文件、913 张 PNG，共 **1,034,917,894 字节**。逐文件检查归档及原 Temp 的 SHA/大小一致，无遗漏、额外、重复或原件缺失；归档 XML/binding 亦与原件相同。**这是完整性核对，没有实看这 913 张图片，不以数量签艺术质量。**

前置 11:53:37.9679673Z 与后置 12:14:52.8755371Z 的六项整数、五项键绑定和音量，其值及存在标记全部相同；intPreferences/keys/hadVolume/volume 四组字段精确一致，字号 100、guideSeen 1。后置实际 SaveAssets 与 idle guard 成功，场景路径为空、dirty=false。只确认这十二个记录键，不推称所有未记录偏好。

## 限域交接

4041766→69d2f4d 仅 PlayerVerification 一源 revision20 的 reduced-roll 探针计数修复，精确差异与媒体有限复用见 `MEDIA_REUSE_4041766_TO_69D2F4D_REVIEW.md` 和 `PLAYER_REDUCED_ROLL_COVERAGE_REVIEW.md`。旧视觉媒体、ART14 保留原采集身份，不由本报告改标新版本。

新实际 Player 全流程、显示、性能、跨进程恢复和版本截图仍需各自真实证据；历史 404 独立 display-only 的有限通过不自动提升为新检查点主流程通过。AudioMixer 缺口、Mac 实机、人工体验/美术和发行决定保持独立。本轮 Editor 通过不证明历史原生崩溃根因已解决，也不解除源码/证据外传审批拒绝。

原标准、工具门槛与用例未修改。输入/输出及十个冻结程序集 SHA 见同名 bindings，详细只读审计位于 `artifacts/cache/v07-full-regression-69d2f4d-review`。由 root 更新 STATE 与后续实际执行状态。
