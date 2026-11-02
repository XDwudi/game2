# 终局时钟首绘修复独立复核

结论：**RC02 在本次候选及真实专项范围内关闭；代码、红绿功能证据与三张新实际画面符合预期，未发现新的阻断。** 前项 RR01 按 Submit 类型统计的修复也在同一测试中保持通过。此结论允许进入新源码冻结/完整回归，不代表完整 Editor 套件、新 Player、Mac、性能或整版艺术/发行通过。

复核者 `/root/v07_spec_audit` 只读源码、报告、PDB 证明和图像，未运行 Unity/Player，未修改生产、测试、标准或 HEAD，未外传。输入是基于 `4f1a25cbd7ed885bd863c24411f1075d7888a18f` 的两文件工作树候选，art0.7.0 / p0.6.0 / schema4；实际图保持这一采集身份，不改标后续检查点。

## 实现与边界

`GameApp.Art07Results.cs` 的 `SyncRunClock07()` 提取了原 Tick 中已有的 Run ID 切换逻辑；Tick 原菜单/模态早退、终局不增时、正常局按 unscaledDeltaTime 累计、30 秒保存均保持。`DrawRunResult07` 在原 Save/读取统计之前调用该 helper，使调试返回和冷 Continue 的第一次 Refresh 就能取得对应时长。

- ViewState 为空或 ID 相同即返回，同局尚未保存的内存时长不会被旧偏好覆盖。
- ID 变化先保存旧 ID 的计时，再读取新 ID 的 HasKey/GetFloat；不会把 debug 时长写给普通局。
- 没有调用 StartRunClock07，没有清零、额外加时或更改计时键名。新局明确写入 0 的已有逻辑未变；是否有记录由 HasKey 判断，不将数值 0 当成缺失。
- 缺少旧 key 时保留 known=false；后续 Save 不创建 key，旧档/调试缺记录的明确文案未变。Draw 重复调用不重新载入同局，不额外累计。
- 计分、Submit 类型筛选、Core、存档 schema、规则与结果布局未更改。此次仅时钟绑定发生在首绘之前。

生产最终字节与 cache 候选一致，SHA `0e1838c81273137f9326b52481c37ddfef9730aa96c36e3032e17ed076f2b681`；测试 SHA `203b16c30551836fce8247279888ce741a61a3baf57c6c2bd6d9fcd6bf952648`。红绿实际测试均使用这份测试字节，没有上轮换行身份差异。

## 有效的真实回归

既有单项 `PLAY_ART07_ResultReloadIgnoresNonSubmitPlaceholdersAndPreservesExactStatistics` 保留三手普通终局、真实 AtomicSaveStore 往返、空占位存在、普通状态/序号/文件字节不变、精确总分/最大分/倍率、坏真实 Submit 必须拒绝等断言。本次只扩展时长边界：

1. 先证明真实 NewRun 已建立普通计时 key；进入调试后将这个既有 key 设置为确定的历史值 **3661.25 秒**。
2. ReturnNormalWorkspace 和冷 Bootstrap → Continue 的断言都紧跟调用、发生在 yield/后续 Update 之前，要求第一帧文本为 `01:01:01`，并要求偏好仍精确为 3661.25。
3. 再在切离普通局后删除该 key，明确模拟无计时记录的旧档；返回、Refresh、后续帧与冷 Continue 必须显示“旧存档未记录”，不能创建新 key。普通存档与精确统计仍不变。
4. finally 只清理该隔离测试创建的 GUID 时钟键，并在所有产品行为断言之后使测试对象的 known=false，避免 OnDestroy 重建已清理键；未用该清理步骤满足产品断言。

3661.25 是测试提供的恢复数据，**不是实际游玩一小时的性能或时长证据**。

| 实际报告 | 结果 | 总耗时 |
| --- | --- | ---: |
| nunit-20261101-123635.xml | 0/1，1 失败，0 跳过 | 1.5475726 秒 |
| nunit-20261101-123717.xml | 2/2，0 失败/跳过 | 5.1922213 秒 |

红批准确失败于首个返回结果帧：期望 `用时  01:01:01`，实际仍为 `用时  旧存档未记录`；分数已是正确的 14/6/×3，不是旧空分数崩溃。绿批新回归 3.105162 秒通过，既有四类终局用例 2.040031 秒通过。

两份原 XML 的 path/SHA 与 binding 一致；各自开始/结束的 1360 源文件及加载程序集完全相同。相对基线，红批只有测试变化；绿批只有测试及该结果文件变化。红 Runtime 为 `ee32969a-b944-411a-940b-c039b8a15ecf`，绿 Runtime 为 `e2f02f1c-e73f-4b56-8fdb-5696bc606495`。红批生产 SHA 为 `f740fb4e…`，绿批为上述 `0e1838c8…`，未改写旧报告。

`v07-result-clock-pdb-1238.json` 五程序集/169 文档/零差异已逐文档校验，DLL/PDB SHA 与审查时磁盘文件、绿批加载记录一致。后置 12:37:54.0321075Z SaveAssets 成功、空场景、dirty=false；六整数、五键绑定与音量的值及存在标记与 4f1 身份快照一致。只确认这十二个已记录公共键；新测试的时钟键清理由测试自身负责。

## 三张实际画面

已实际打开查看 1920×1080、100% 字号的三张原始 PNG，随后核对持久化副本逐字节相同。正式目录：`evidence/v07/media/result-reload-123717/`。

| 图像 | 实际帧 | 所见时长 |
| --- | ---: | --- |
| S18-normal-terminal-return.png | 33 | 01:01:01 |
| S18-normal-terminal-cold-reload.png | 52 | 01:01:01 |
| S18-normal-terminal-no-clock.png | 70 | 旧存档未记录 |

三图 Runtime MVID 与绿批一致，状态哈希相同、均为普通 RunFailed；都完整显示总得分 14、最大单手 6、最高倍率 ×3、种子 70991、最后有效提交骰及结果按钮。时长与统计文字清晰，未观察到裁切/重叠；冷恢复通知不盖主要统计或按钮。缺 key 图保留未知提示是正确行为，区别于上一批有记录却错误显示未知。

红归档 1 目录/3 文件/239,710 字节；绿归档 2 目录/22 文件/7 PNG/8,285,953 字节，目录与报告引用、各复制文件 SHA/大小、归档 XML/binding 均核对一致。**本轮只实看上述三张，其余四张只核完整性，不扩大视觉结论。** 三 PNG/三 sidecar 的持久化 manifest 保留实际工作树和两源 SHA，没有借新检查点重新标注。

## 限域关闭与下一步

本报告以新的实际证据限域关闭 `RESULT_RELOAD_FIX_REVIEW.md` 中 RC02，同时确认其 RR01 防回归保持。旧红图、失败 Player、取消的 4f1 完整 Play 均保留原结论；未完成运行不能作为通过。当前可继续新冻结后的完整 999 Edit/155 Play、双构建和实际 Player 验证；本报告不预填这些结果，不重审未改变的旧媒体或放宽标准。AudioMixer、人工/M5、外传审批等独立限制保持。

输入、图像及审计 SHA 见同名 bindings；逐项核对见 `artifacts/cache/v07-result-clock-review/red-green-and-images-audit.json`。由 root 更新 STATE。
