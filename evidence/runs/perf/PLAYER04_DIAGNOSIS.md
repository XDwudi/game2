# Player04 性能诊断（只读）

Player04 的性能结论仍为 **budget_exceeded**。真实 180.003 秒、10,598 个完整帧的间隔 P95 为 **23.5467 ms**；采集覆盖有效、记录缓冲未扩容。本诊断没有重算或降低验收门槛，没有修改原 CSV、报告、玩法或存档规则。最值得先处理的两条路径是整页 UI 刷新，以及随历史回执增长的完整状态复制/序列化/旧档验证。

## 输入与复现

- 实测目录：`evidence/runs/player/player-20261029-012720-f50658f6`。
- 实测 Windows Player build GUID：`1ece7f2b268b4656b12e498d74f0841c`；Unity `6000.6.7f1`；Ryzen 9 5900X / RTX 3080；1920×1080；vSync 0 / targetFrameRate 60。
- 实测 probe：`p0-player-preallocated-capture-4`。原始文件 SHA、程序集 MVID、读到的源码 SHA 均在 `player04-analysis.json`。
- 执行 `E:/anaconda3/python.exe evidence/runs/perf/analyze_player04.py` 生成 JSON 和逐事件 CSV。需要 NumPy；只读取实测证据。
- root 已在诊断期间修改 GameApp/UiKit 对象复用；本报告读取源码的 hash 不冒充 Player04 的构建源码快照。探针随后获准加“两帧排空”，也属于 Player04 之后的变更。

## 实测关联：不是从总体 P95 猜热点

Probe 的每秒固定节奏为 Roll 0.00、SkipVisual 0.25、Reorder 0.50、TogglePending+Refresh 0.70、ClearPending+Refresh 0.85。原 CSV 不含方法起止时间，以下动作标签是按节奏推断，未伪装为方法级 Profiler 标记。

`GC Allocated In Frame` 对应完成帧，出现在长帧的下一行。将 interval[t] 与 GC[t+1] 配对，Pearson r = **0.8268**；错误地使用同一行则 r = -0.5700。其他 ±4 帧偏移约 -0.014。原始逐行例：frame 2373 为35.816 ms / GC0，下一帧2374为3.731 ms / GC1,763,013B。这是长刷新与随后短间隔，不应把后一行的高 GC 归给3.7ms的“快帧”。

共有900个 >100KB 分配行。第一个含预分配边界，单列保留；其余899个可与上一帧关联，全部距最近计划动作≤24.129ms。分组得到179次Roll、其余四类各180次，符合首个Roll在采样打开前已经执行的事实。

| 推断动作 | 事件数 | 动作帧间隔均值 / P95 (ms) | 分配均值 (MB，十进制) | 头30秒 → 尾30秒每次分配 (MB) |
| --- | ---: | ---: | ---: | ---: |
| Roll + 保存 + Refresh |179|37.522 / 41.128|3.442|2.036 → 4.840|
| SkipVisual + Refresh |180|23.080 / 24.401|1.240|1.181 → 1.299|
| Reorder + 保存 + Refresh |180|37.634 / 41.268|3.433|2.027 → 4.839|
| TogglePending + Refresh |180|23.315 / 24.524|1.236|1.177 → 1.295|
| ClearPending + Refresh |180|22.947 / 24.280|1.240|1.180 → 1.299|

- 900个 >20ms 帧中899个属于上述动作；所有529个高于总体P95、所有364个 >25ms、所有359个 >30ms 帧均属于这些动作。
- 仅刷新、不提交事务的三组约23ms；Roll/Reorder约37.6ms。这支持同时处理UI和事务路径，不能将所有成本归为骰子动画或计分。
- GPU P95 0.232448ms、CPU Render Thread P95 0.3123ms。`Canvas.BuildBatch` P95 0.0065ms仅测该标记，**不覆盖** GameObject/TMP/Button 创建、销毁、字符串、状态拷贝或 `GameApp.Refresh` 的全部托管开销，不能据此断言UI无成本。
- 前30秒总分配234.900MB，尾30秒407.165MB；整窗1,911.984MB（包含下面记录的首边界）。同样容量/同样动作的增长与回执历史增长相吻合。

### 必须保留的边界

首个计入GC行 frame2344 / elapsed0.00615s为8,946,498B。采样前记录数组payload为7,200,000B；完成帧计数延迟把该预分配带入首行，余量量级与一次初始Roll相符。故8.9MB最大值不能全部归游戏，也不能用它指称采样中扩容。没有从原验收数据删除该行；它不解释P95或后续持续增长。root明确授权的修复只是在数组分配后等待两帧，再建立采样时间/GC基线；下一轮实测需要确认。

排除动作及紧随其后的补偿帧后，8800个其余帧的P95仍为17.0095ms，中位16.6683ms。60FPS限速的睡眠/帧调度边界也有抖动；`Main Thread` 包含等待，不能视为纯游戏CPU执行时间。UI池有充分依据，但不能预先保证池化后总体16.7ms P95通过；必须同预算重跑，必要时进一步记录等待/主线程实际工作的标记，不改变门槛去取得通过。

## 源码路径：复制次数和增长来源

`GameSession.State`（GameSession.cs:19）每次都返回完整深拷贝。`RunState.Clone`（SessionModels.cs:153–165）复制所有骰面、控制器、库存、牌型等级、手牌、回执、effects及上一提交。回执再复制命令数组和effects（SessionModels.cs:128）。这正确隔离事务和外部调用者，但多个只要几个标量的调用者不需要整份DTO。

Player04对应的优化前普通 Roll/Reorder 成功路径（本轮首次读取时确认）每次有 **6次生产代码全状态Clone**：

1. `GameApp.Send` 只为 `expectedActionSequence` 读取 `Session.State`（GameApp.cs:108）。
2. `GameSession.Execute` 创建事务候选（GameSession.cs:61）。
3. `SavePending` 给存档器的隔离副本（GameSession.cs:109）。
4. `AtomicSaveStore.Save` 为保护备份读旧档；`TryRead` 最后构造临时 `GameSession`，构造器除完整Config/State验证还 `restored.Clone()`（AtomicSaveStore.cs:37、79；GameSession.cs:24–29）。这份恢复副本立即被丢弃。
5. `GameApp.LogCommand` 只需要seed/runId/isDebug/phase却读取完整 `State`（GameApp.cs:282）。
6. `GameApp.Refresh` 再取完整 `State`（GameApp.cs:62）。

压力驱动每个Roll还在 `AssertStressCapacity` 读一次State，每个Reorder读一次State取有序ID。因此实测每次事务是7次复制；三次纯Refresh各1次。合计约17次完整复制/秒，其中2次来自观察者。真实玩家对应生产路径约15次/同样操作秒；不能把全部复制成本归生产。

每次保存还执行：整个RunState→JSON；payload哈希UTF8；包络JSON及转义；包络UTF8；FileStream写入及`Flush(true)`；读取旧包络文本、反序列化包络、payload哈希UTF8、反序列化旧RunState、完整Config/State校验、恢复Clone，然后`File.Replace`。CSV没有这些方法的单独计时，**不能声称某一种复制或同步Flush占了具体多少ms**。

初始Debug state为2条回执，最终382条；最终活跃存档196,714B，其中payload169,101个字符。按相位拟合，纯刷新每次分配随时间增加约792B/秒，恰为每秒新增2条回执在一次完整State复制中的规模；事务帧每次分配增长约18,768B/秒，为纯刷新的约23.7倍。7次复制只解释其中部分增长，余下与全量JSON/旧档反序列化路径一致；这是有源码支持的定位线索，并非方法级归因结论。

## 建议顺序与保护条件

1. **完成root正在做的UI对象复用并实测同场景。** 三个不保存动作各约1.24MB是明确对照。复用应避免重新AddComponent、TMP实例、按钮对象、事件监听器累积；测试跨阶段、模态关闭、重复Refresh和重新建局。不能只减少Canvas.BuildBatch而仍在全量构造托管UI。
2. **消除只读元数据触发的完整Clone。** 提供加锁的标量ActionSequence / RunMetadata查询，或让成功提交展示路径捕获一次State并供日志与UI共享其外部副本。日志phase优先用对应receipt，不能使用提交前旧ViewState。保留Execute候选、SavePending隔离和外部不能写内部state的契约。只改这两处可消除每事务2次无用深拷贝。
3. **让无提交的Refresh复用已提交ViewState及计分预览。** Skip/Pending/Clear没有改变已提交状态；缓存以Session实例+已提交actionSequence为键，Busy/Pending仍单独实时读取。不能只用序号，否则新Run或Debug独立Session同序号会错误命中。Roll改变余掷、Reorder改变顺序、遥控/道具/购买/恢复等必须失效；失败保存仍显示旧已提交状态，Retry成功再更新。`ScorePanel`每次调用PreviewScore→FreezeSubmission复制7×6骰面并Evaluate（GameApp.cs:228；GameSession.Submission.cs:8–23），纯Pending不应重复执行。
4. **把“验证存档”与“建立可运行Session”分开。** 先暴露等价的纯验证入口，供AtomicSaveStore.TryRead调用，避免为了验证而Clone旧档/建立Session；配置在加载时验证，状态验证仍保留。更进一步只在旧文件字节与此前已验证字节完全一致时复用验证结论，变化时走完整校验；不可只看文件时间/长度，不能直接删除写前旧档验证。必须重跑旧活动档损坏、错误checksum、错误规则内容、Save未经Load、已有有效backup保护、IO失败/Retry等真实存档测试。
5. **低优先级的小分配。** `Gameplay`一次及每颗 `DrawDie` 都读 `PendingIds.ToArray()`（GameApp.cs:167/211）；同次刷新取一次即可。每颗骰数量最多10，牌型/控制器数量小；当前不能证明反复LINQ查找比整页重建/全量JSON更贵。`PreviewSubmissionEffects`确实会Clone并Submit，但只在打开计分模态时调用；模态截图发生在计时窗外，**不是本轮899尖峰的成因**。

不建议截断回执、跳过自动保存、异步先显示未保存状态、把UI直接指向内部可变state、修改RNG或减小压力容量。这些改变事务/幂等/存档语义，不是本任务的性能修复。

## 后续证据要求

先在同一构建中保留真实180秒10骰/7槽/10控制器/5道具和所有采集覆盖。比较五类动作及头尾30秒分配，而非仅总体平均FPS。若事务仍突出，应给 `Refresh`、`State.Clone`、`ValidateState`、`ToJson`、`TryRead`、`Flush` 分别加低开销ProfilerMarker，以取得真正方法耗时；现CSV不足以排列这些方法的毫秒份额。当前诊断已交root，独立审核和下一次Player结果决定修复是否有效，本文不代签性能通过。

## root 后续优化的只读语义复核

root在收到诊断后新增 `GameSession.ActionSequence`，以Session引用+已提交序号缓存脱离核心的ViewState，Send不再为序号读取完整State，并将LogCommand放在Refresh之后消费ViewState。本代理只读检查，未修改这些生产代码。静态结论：保留语义，未发现阻断缺陷。

- 标量序号在gate中读取已提交state，不暴露pendingSaveState；外部仍不能修改核心数据。
- 无提交的Pending/Clear/Skip依旧实时读pending、Busy，缓存不阻止这些UI变化。
- 成功事务序号递增失效缓存；保存失败序号不变显示旧快照；Retry成功再更新。
- 新挑战、练习恢复、独立Debug Session即使序号相同，也因Session引用不同失效。
- LogCommand位于Refresh之后，其seed/runId/phase来自新已提交ViewState；duplicate仍不追加日志。事务候选、存档器隔离副本和旧档保护校验未删除。
- ViewState仍为可变DTO，UI应继续只读；目前生产调用路径未见对其字段写入。缓存不能扩展成返回核心内部state。

待root真实测试确认：无提交刷新复用同一ViewState、新Session同序号换快照、IO失败后旧快照/Retry新快照、Retry日志phase/seed对应磁盘成功状态。这是源码复核，不能冒充已执行的Unity结果。对应读取SHA见 `cache-semantics-review.json`。
