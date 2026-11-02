# dca7a26 同源完整 Editor 回归独立复核

复核时间：2026-11-02 02:34–02:37 UTC；独立复核者：`/root/v07_run_review`。源检查点：`dca7a2610f39d65088b9541549d3465c388fd3e7`。本次仅只读核对真实运行及归档；未启动 Unity/Player、操作原生显示、修改生产/标准或复写原始证据。

规格符合性：**通过本次冻结源的完整 Editor 回归与证据绑定检查**。制作质量：**该工程回归和此次实际观看的 TMP 尾影修复场景通过；整版美术、Player 性能、原生显示、Mac/M5及人工体验仍未完成**。不存在把 Editor 全通过提升为整版 ACCEPTED 的结论。

## 真实运行与源绑定

| 项目 | 独立核验结果 |
|---|---|
| EditMode `nunit-20261102-020742.xml` | 1013/1013，零失败/跳过/不确定；18.0938405 秒；02:07:24–02:07:42 UTC |
| PlayMode `nunit-20261102-022904.xml` | 158/158，零失败/跳过/不确定；1266.2926493 秒；02:07:58–02:29:04 UTC |
| 发现清单 | 两模式的所有唯一 fullname 集合与真实 NUnit 叶节点完全相等，无漏项或重复计数 |
| 历史覆盖 | 保留前一轮 1013 Edit +155 Play 全部测试；新增两项尾影 GPU 测试和一项资源诊断真实集成，三项均通过 |
| 起止源 | 四个有序快照各1370文件完全相等，并逐字节匹配冻结 Git blob；不以当前临时构建设置替代测试源 |
| 当前源/gate | 双构建完成后 02:34:41 再核：HEAD正确、三个source scopes clean、1370当前源完全匹配，validated_gate通过1378文件，计数1013/158 |
| 编译证据 | 五程序集、174个项目 C#文档；PDB零差异，文档集合精确等于冻结 C#集合；保存10个DLL/PDB的SHA匹配审计与manifest |
| 实际载入身份 | Runtime `55356d69-1356-441b-8293-a913352d0d25`；PlayModeTests `b56ce6e1-db98-449e-8031-90d3f6474b6e`；四次起止程序集均与PDB/DLL一致 |
| 偏好恢复 | 02:07:12实际import identity至02:30:44 SaveAssets后，12公共偏好存在性/值全部一致，空场景且dirty=false |

当前规则仍为 `p0.6.0/schema4`，美术 `art0.7.0`、发布标识 `p0.7.0`、Unity `6000.6.7f1`；配置与黄金集SHA与冻结 identity一致。相对4efd2b7只有已审九个源文件变更：PanelMotion、两项测试及meta、资源probe及meta、两个既有bootstrap互斥guard。PanelMotion/两个测试/probe与已审候选 SHA 精确相同，详情见 `frozen-audit.json` 的 fixBindings。

资源专项此前实际 020427 的独立静审与真实集成结论见 `resource-diagnostic-static/` 和 `resource-diagnostic-integration/`；本次确认它在冻结完整回归中仍真实通过。该新增工具是 opt-in 观察，建议纹理预算与尽量零GC未被改成新硬门槛，也未修改180秒/P95标准。本报告没有把 Editor 内部资源数据换算成 Player 显存/性能通过。

## 归档与实际观看范围

`artifacts/cache/v07-full-play-dca7a26-022904` 的156个目录与 NUnit output 中实际路径集合完全相等；2994文件、932 PNG、1,050,863,629字节全部逐件核对了manifest、归档与原始Temp的SHA/大小；复制的NUnit和binding也逐SHA一致。该数量只代表归档完整性，不代表932图全看。

本次实际观看12张新图，7个唯一PNG哈希，全部为本次运行02:16:56–02:17:00生成。四组各看 source、tail、tail-in-motion：普通Debug32、减少动态Debug32、主/回退两atlas第一次及第二次真实关闭。4张blank未看，旧green未重复看，余920张归档PNG没有在本次视觉复核中逐图观察。

- 普通/减少动态路径：`DiceCraftTests_831dbab7a5b34073a7005bde3d42508f/art07-modal-exit-gpu/actual-debug32-{normal-23658,reduced-23696}`。
- 两atlas路径：`DiceCraftTests_d3ab020d014a496788c4125502cdb29b/art07-modal-exit-gpu/actual-debug32-two-atlases-{cycle0-23739,cycle1-23775}`。

所有12张图的精确绝对原路径、归档路径、SHA与大小保留在 `frozen-audit.json` 的 visualGroupsBound；每张已匹配最终158项NUnit对应输出目录及实际Runtime/Test MVID。早先 `viewed-images.json` 中的 pending 字段是运行尚未结束时的原记录，未改写；本报告及 finalNUnitArchiveBindingVerified=true 明确完成其后续绑定。

实际画面中中文标题、控制按钮、32骰摘要、提交顺序与底部操作可读，未再见原始P07-MODAL-TMP-01不透明字块；回退字体 `H＋混 78` 也保留正确字形。四个中间帧alpha分别约0.8583/0.7908/0.8639/0.8614，背景随淡出可见属于过渡，未当作稳定重叠缺陷。source为原弹窗；tail为测试暂停Update保存的实GPU快照；中间帧在暂停解除后由原Update生成。不能把这些帧称作无暂停的自然Player录像或全平台交互体验验收。

## 剩余边界与复现

可以基于此回归继续审查同源实际构建和Player。正式Windows180秒帧时/GPU覆盖、原生显示矩阵及跨进程恢复、独立资源/坏参数实际运行、Mac/M5、AudioMixer及人工美术体验必须保留各自真实结果；旧4efd失败不能以本Editor结果覆盖。审查者未写生产、未改旧XML/截图、未降低门槛。

独立复算脚本和结果位于 `artifacts/cache/v07-full-regression-dca7a26-review/{audit-frozen.py,audit-current.py,frozen-audit.json,current-gate-audit.json}`。归档双读已在正式Player采样前完成，后续仅写此报告。首轮review harness曾将真实程序集名写成DiceCraft.Tests而触发StopIteration；已按实际PDB中的DiceCraft.PlayModeTests修正，未改任何游戏测试、阈值或原始结果。
