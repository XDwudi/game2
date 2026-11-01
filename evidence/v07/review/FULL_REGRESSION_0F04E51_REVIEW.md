# 检查点 0f04e51 完整回归证据独立复核

结论：**同源完整 EditMode 与 PlayMode 自动化回归的证据一致性通过。** 本报告不签整版美术、体验、最终媒体、Player、性能、Mac 或发行。复核者 `/root/v07_spec_audit` 仅只读磁盘与 Git；未运行 Unity/Player，未修改生产、测试、标准、索引或 HEAD。输入 art0.7.0 / p0.6.0 / schema4，源码 `0f04e51a2add69ca69f44c2d0c7b8425367e7a89`，Unity 6000.6.7f1。

## 完整实际报告

| NUnit 报告 | 通过/发现 | 失败/跳过/不确定 | 耗时 |
| --- | ---: | ---: | ---: |
| nunit-20261101-093935.xml，EditMode | 999/999 | 0/0/0 | 17.4410057 秒 |
| nunit-20261101-095947.xml，PlayMode | 154/154 | 0/0/0 | 1189.9099368 秒 |

逐一核对全部唯一 `fullname` 与各自完整 Unity discovery：无遗漏、额外、重复或非通过叶节点，报告平台正确。Edit 实际 09:39:18Z—09:39:35Z；Play 实际 09:39:57Z—09:59:47Z。与前一轮 e0439ae 的 999/152 相比，Edit 集合不变，Play 只增加以下两项，原 152 项全部保留：

- `PLAY_ART07_DICE_DebugAppliedTableAllCloseRoutesRestoreGripsWithoutApplyingDraft`
- `PLAY_ART07_DICE_SettingsRebuildAllCloseRoutesRestoreOnlyLegalGrips`

这两项此前分别在 092559、092846 真实失败（应有 8 个抓手，实际 0）；本轮完整套件同名通过。旧 075917、080637 的七项失败也都保留，并在本轮同名通过。旧失败报告及其 SHA 记录在本次 bindings/审计 JSON；没有删失败、减集合或改零失败/跳过/不确定要求。此前 0befca 取消批次仍为未完成，不由本轮改写历史结论。

## 实际源与程序集绑定

两份 XML SHA 与各自 binding 一致。四个快照时点如下：

| 实际 binding 时点 | UTC | 源文件 |
| --- | --- | ---: |
| Edit 开始 | 09:39:18.2822924Z | 1360 |
| Edit 完成 | 09:39:36.4468518Z | 1360 |
| Play 开始 | 09:39:57.4965531Z | 1360 |
| Play 完成 | 09:59:48.0930095Z | 1360 |

四份有序清单完全相同、无重复路径，当前文件 SHA 全部一致。另直接读取 HEAD 的 1360 个 Git blob，与当前文件原字节逐一散列比较，全部相同；不是仅依赖 Git clean 或换行归一化。生产 scopes 在核对时保持 clean。早先缓存候选与集成源码的 CRLF/LF 比较仅用于确认补丁内容，**本次 PDB、快照和 Git 检查没有归一化字节**。

`v07-pdb-post-full-0f04e51-1000.json` 的 5 个程序集、169 份文档全部核对：当前 DLL/PDB SHA 与证明相同，DLL/PDB 标识匹配；169 份 PDB 源校验值与当前 C# 原字节一致，集合恰好覆盖项目全部 C#，零差异。两报告四个起止时点的真实加载 MVID/DLL SHA，均与该证明和当前程序集相同。

09:39:14.7416856Z 的 `checkpoint-resource-identity-0f04e51.json` 实际调用先拒绝 Play/编译状态，再同步导入并 `Resources.Load`。该调用成功；返回 identity 与当前文件一致，版本、提交、配置/黄金用例 SHA 正确。实际 Runtime MVID 为 `e858ca97-2062-4977-801f-579d213d9ff4`，与报告/PDB/磁盘一致。此证据没有返回 `playing` 或帧号，本报告没有补造；idle 来自真实成功执行的前置 guard，discovery 另由实际清单核对。

正式 `build-gate.json` 逐项散列一致，实际绑定 1368 文件及明确 999/154 计数。只读调用现有 `package_v07.validated_gate()` 返回成功，未重新生成 gate、identity 或测试。该结果仅证明这组源/证据可用于后续构建，不是已构建或发布。

## 归档与偏好

独立检查 `artifacts/cache/v07-full-play-0f04e51-095947`：152 个唯一隔离目录恰好等于 NUnit output 引用集合；2944 个唯一文件、913 张 PNG，共 1,034,928,799 字节。逐文件核对归档及原 Temp 的 SHA/大小，全部相同，无遗漏、额外、重复或原件缺失；归档 XML/binding 与原件相同。**这是证据完整性核对，没有实看 913 张图，也没有按数量签艺术通过。** 各模块的实际视觉结论仍引用各自报告。

独立比较 09:39:14.7416856Z 前置快照与 10:00:34.3155613Z 后置读取：6 项整数偏好、5 项按键绑定，以及音量的值和存在标记全部相同；字号 100、guideSeen 1。后置实际执行 `SaveAssets`，且 guard 确认非 Play/编译，返回场景路径为空、dirty=false。仅确认这 12 个明确记录的键，不推称未记录偏好全部恢复。

## 保留边界与交接

本轮关闭的是指定检查点的完整 Editor 自动化回归及绑定核对。正式 AudioMixer、同源最终六配置媒体与九段录像、双平台构建、Windows Player/原生显示/性能、Mac 实机、人工体验仍按 STATE 待验或保留既有失败。Editor 的 154 项通过不能代替这些项，也不改变已记录原生崩溃根因尚未证实解决的事实。

源码或相关派生资源发生变化时，按影响重新核对/验收，不能静默沿用本报告。审计脚本从上一轮只读方法复制到本轮 cache，仅适配明确的 154 项完整 discovery 和本轮实际资源读取结构；正式工具/阈值未修改。原 Edit-only 审查与首次辅助脚本结构误读记录仍保留。输入、脚本和输出 SHA 见同名 `.bindings.json`，详细逐项核对见 `artifacts/cache/v07-next-full-review`。由 root 更新 STATE 与后续执行状态；本代理不写当前冻结源或 HEAD。
