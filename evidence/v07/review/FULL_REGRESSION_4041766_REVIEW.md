# 检查点 4041766 完整回归证据独立复核

结论：**同源完整 Editor 回归的证据一致性通过，本次核对未发现缺陷。** 复核者 `/root/v07_spec_audit` 仅只读磁盘、Git 与既有报告，未运行 Unity/Player，未写生产、测试、标准、索引或 HEAD，未外传。输入 art0.7.0 / p0.6.0 / schema4，源码 `404176661240a5d9834895416ed3719d6493e253`，Unity 6000.6.7f1。本报告不签 Player、Mac、性能、整版艺术或发行。

## 实际完整运行

| NUnit 报告 | 通过/完整发现 | 失败/跳过/不确定 | 耗时 |
| --- | ---: | ---: | ---: |
| nunit-20261101-111708.xml，EditMode | 999/999 | 0/0/0 | 17.5225219 秒 |
| nunit-20261101-113731.xml，PlayMode | 154/154 | 0/0/0 | 1195.5413346 秒 |

全部唯一 fullname 与各自完整 discovery 精确一致，无遗漏、额外、重复或非通过叶节点。Edit 实际 XML 时间为 11:16:50Z—11:17:08Z；Play 为 11:17:35Z—11:37:31Z。与上一轮 821ec59 的 999/154 相比，两套用例名称集合均完全不变。

原商店专项 `PLAY_ART07_ShopFirstEntryUses240msShelf30msCardStagger12pxRiseAndReducedFade` 与 `PLAY_ART07_ShopTabRedrawSettlesWithoutReplayingAndRefreshKeepsItsOwnTransaction` 均纳入本轮完整 Play 并通过，耗时分别 1.694072、1.015369 秒。它们覆盖产品商店行为，不等于执行新 standalone Player 的 revision19 探针；此前 821ec59 Windows 商店位置断言失败仍为真实失败，不被本轮 Editor 结果改判。

保留核对九份历史 NUnit（075917、080637、085726、092559、092846、095947、102406、105539、111438）；原名均在本轮集合中，旧非通过用例本轮同名通过。历史失败、取消及不完整批次没有被删除或重写。

## 源与程序集绑定

两份实际 XML 的 SHA 与对应 binding 一致。四个真实快照如下：

| 时点 | UTC | 源文件数 |
| --- | --- | ---: |
| Edit 开始 | 11:16:50.5925903Z | 1360 |
| Edit 完成 | 11:17:08.8272126Z | 1360 |
| Play 开始 | 11:17:35.7382909Z | 1360 |
| Play 完成 | 11:37:31.9900522Z | 1360 |

四份有序清单完全相同且无重复路径，原字节 SHA 与核对时当前文件全部一致。另读取 HEAD 的 1360 个真实 Git blob 逐一比较原字节，全部匹配；没有换行归一化，没有仅依赖 clean 标志。核对时 source scopes clean、HEAD 保持 4041766。

`v07-pdb-post-full-4041766-1138.json` 的 5 个程序集与 169 份文档核对无差异：实际 DLL/PDB SHA、标识及全部 PDB 源校验值一致，169 份源恰好覆盖当前全部 C#。两报告四个起止时点的加载 MVID/DLL SHA 与这份证明及审查时磁盘文件一致。此检查在 root 后续构建重编 Library 前已完成并落盘；后续构建 DLL 不会被当作本次 Editor 运行的 DLL。

11:16:48.1607214Z 的实际资源读取有显式拒绝 Play/编译的 guard，再同步导入并 Resources.Load，调用成功。版本、提交、配置/黄金用例 SHA 均正确；Runtime MVID 为 `b37c1374-4c4e-44b5-9ff1-85e239f457fc`，与实际测试/PDB/磁盘一致。该返回没有 playing 字段或帧号，未补造；idle 由成功执行的 guard 举证。

正式 `build-gate.json` 实际绑定 1368 文件及明确 999/154 计数。只读调用现有 `package_v07.validated_gate()` 成功，没有重新生成 gate、identity 或测试；仅说明该组源和证据满足既有构建前一致性检查。

## 归档与恢复

`artifacts/cache/v07-full-play-4041766-113731` 的 152 个唯一隔离目录恰好等于 NUnit output 引用集合；2944 个唯一文件、913 张 PNG，共 **1,035,142,541 字节**。逐文件检查归档和原 Temp 的 SHA/大小一致，无遗漏、额外、重复或原件缺失，归档 XML/binding 与原件相同。**这是完整性核对，未实看这 913 张图片，不按文件数签艺术质量。**

前置 11:16:48.1607214Z 与后置 11:38:07.750911Z 的 6 项整数、5 项键绑定和音量，其值及存在标记全部相同；intPreferences/keys/hadVolume/volume 四组字段完全一致，字号 100、guideSeen 1。后置真实 SaveAssets/idle guard 成功，场景路径为空、dirty=false。仅确认这 12 个已记录键，未推称所有未记录偏好。

## 限域交接

本检查点只改 Player 商店探针的入场等待及 revision19，产品 UI、资产、规则未因该修复改变；精确前置静审见 `artifacts/cache/v07-player-motion-preconditions-review/REVIEW.md`。旧艺术媒体与 ART14 的已有有限复用继续保留原采集身份，不由本报告改标新版本，也不机械补录未受影响媒体。

新 Windows/macOS 构建、实际 Player 全流程/显示/性能/恢复及版本截图必须使用各自真实证据。旧 Windows 主流程与 mode0 失败仍保留；AudioMixer 缺口、Mac 实机、人工体验/美术和发行决定仍按 STATE 处理。完整 Editor 通过不证明原生崩溃根因已经解决，也不解除源码/证据外传的自动审批拒绝。本代理没有执行外传。

正式工具、验收门槛和原用例未修改。首次辅助审计因新 cache 输出父目录尚不存在，在写结果时抛 FileNotFoundError；仅创建输出目录后用原脚本重跑成功，失败说明保留，没有将首次调用算作成功。详细输入/输出 SHA 见同名 bindings，逐项审计在 `artifacts/cache/v07-full-regression-4041766-review`。由 root 更新 STATE 与后续实际执行状态。
