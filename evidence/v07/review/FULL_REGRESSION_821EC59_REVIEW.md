# 检查点 821ec59 完整回归证据独立复核

结论：**同源完整 EditMode 与 PlayMode 自动化回归的证据一致性通过，未发现本次核对缺陷。** 本报告只签指定检查点的 Editor 回归证据，不签整版美术、体验、Player、性能、Mac 或发行。复核者 `/root/v07_spec_audit` 仅只读磁盘与 Git，未运行 Unity/Player，未修改生产、测试、标准、索引或 HEAD。输入 art0.7.0 / p0.6.0 / schema4，源码 `821ec5992c34b59eddfe2a0ae984fbaca48f41f8`，Unity 6000.6.7f1。

## 完整实际报告

| NUnit 报告 | 通过/发现 | 失败/跳过/不确定 | 耗时 |
| --- | ---: | ---: | ---: |
| nunit-20261101-103514.xml，EditMode | 999/999 | 0/0/0 | 18.0964396 秒 |
| nunit-20261101-105539.xml，PlayMode | 154/154 | 0/0/0 | 1192.6815642 秒 |

全部唯一 `fullname` 与各自完整 Unity discovery 精确一致：无遗漏、额外、重复或非通过叶节点，报告平台正确。Edit 实际 10:34:56Z—10:35:14Z；Play 实际 10:35:46Z—10:55:39Z。与上轮 0f04e51 的完整 999/154 比较，两套 fullname 集合均完全不变，没有减少原用例或跳过检查。

修正手册页签前置后的 `PLAY_V05_HandbookChangesWithCapacitySimpleStraightUpgradeAndReload`、`PLAY_V05_HandbookTwentyReadsPreservePendingSaveAndAllRuleState` 均实际通过（2.599469、1.495947 秒）。这证明其 Editor 用例已纳入完整运行，不能替代新 Windows Player 中的验证探针执行。旧 075917、080637 的七项失败、两个抓手基线 092559/092846，以及其他旧报告仍保留；本轮同名用例通过，不改写历史失败或取消批次。

## 实际源与程序集绑定

XML SHA 与各自 binding 一致。四个真实快照时点为：

| binding 时点 | UTC | 源文件 |
| --- | --- | ---: |
| Edit 开始 | 10:34:55.9854086Z | 1360 |
| Edit 完成 | 10:35:14.8123426Z | 1360 |
| Play 开始 | 10:35:46.9448926Z | 1360 |
| Play 完成 | 10:55:40.3123224Z | 1360 |

四份有序清单完全相同、无重复路径，核对时当前文件 SHA 全部一致。独立读取 HEAD 的 1360 个 Git blob，与当前文件原字节逐一比较，全部相同；没有做换行归一化，也没有仅依赖 Git clean。核对时 source scopes clean、HEAD 保持指定检查点。

`v07-pdb-post-full-821ec59-1056.json` 绑定 5 个程序集、169 份文档：DLL/PDB SHA 与实际文件相同，标识匹配；169 份 PDB 源校验值与当前 C# 原字节一致，且恰好覆盖项目全部 C#，零差异。四个测试起止时点的真实加载 MVID/DLL SHA 均与该证明及实际程序集一致。

10:34:52.4299741Z 的实际资源读取先执行拒绝 Play/编译的 guard，再同步导入并 `Resources.Load`，成功返回版本、提交、配置/黄金用例 SHA。Runtime MVID 为 `dea6b318-5db4-4f86-9b94-c1dad2e85dca`，与报告/PDB/磁盘一致。该结果没有返回 playing 或帧号，本报告不补造这些字段；idle 依据是成功执行的显式 guard。

正式 `build-gate.json` 逐项绑定 1368 个文件及明确 999/154 计数。只读调用现有 `package_v07.validated_gate()` 成功，未重生成 gate、identity 或测试。这表示该组源和证据满足既有构建前一致性检查，不表示已经完成 Player 或发布验收。

## 归档与偏好

`artifacts/cache/v07-full-play-821ec59-105539` 包含 152 个唯一隔离目录，集合恰好等于 NUnit output 引用；2944 个唯一文件、913 张 PNG，共 1,034,624,948 字节。逐文件核对归档和原 Temp 的 SHA/大小，全部一致，无遗漏、额外、重复或原件缺失；归档 XML/binding 与原件相同。**这是证据完整性核对，没有实看 913 张图，不按数量签艺术通过。**

独立比较 10:34:52.4299741Z 前置快照与 10:56:17.8795404Z 后置读取：6 项整数偏好、5 项按键绑定，以及音量的值和存在标记全部相同（字号 100、guideSeen 1）。四组字段 intPreferences/keys/hadVolume/volume 均完全相等。后置真实执行 SaveAssets，显式 idle guard 成功，返回场景路径为空、dirty=false。仅确认这 12 个已记录键，不推称所有未记录偏好。

## 保留边界与交接

0f04e51 → 821ec59 只有 Player 探针选 Hands/Revision18 与旧测试的前置修正；生产 UI、资产和演出未变化，精确两源差异及旧媒体有限复用条件见 `MEDIA_REUSE_0F04E51_TO_821EC59_REVIEW.md`。旧媒体仍绑定 0f04e51，不能改标为本检查点；本轮完整报告不增加视觉签署范围。最终 Player、版本截图、原生显示/性能、Mac 实机仍需各自实际证据；旧 0f04 Player 主流程和 native-only 的失败不被这轮 Editor 结果改判。

AudioMixer 缺口、人工美术/体验和发行决定继续按 STATE 处理；既有原生崩溃根因未证实解决。本代理没有外传任何文件，也没有执行此前被自动审批拒绝的 git push。

使用原只读审计方法，缓存脚本仅替换明确的检查点/证据路径；正式工具和阈值未改。输入、审计脚本及输出 SHA 见同名 bindings，逐项结果在 `artifacts/cache/v07-full-regression-821ec59-review`。后续相关源码或资源变化需按影响重新核对，由 root 更新 STATE 和执行状态。
