# Player05：池化后剩余工作与修复风险

输入由root指定为 `27e4b34` 后的代码与 FramePacingExperiment；实际正式数据来自 `player-20261029-015104-31d8ee23`。源码读取SHA、构建GUID、原始CSV SHA及统计在 `player05-analysis.json`。本轮只读生产/探针；未调整目标帧率、操作、采样口径或16.7ms门槛。

## 数据结论

正式结论仍为 **budget_exceeded**：180.014秒、10,724完整帧，LateUpdate间隔P95 **18.3322ms**，最大40.2854ms；记录缓冲0扩容。GC P95为56,280B，相比Player04的1,265,626B显著改善，总分配由1,911.984MB降为808.744MB。前后源版本不同，因此这是组合优化结果，不能拆称对象池单独贡献了某个百分比。

`analyze_player05.py`保留原验收的完整帧集合，仅为诊断将>10KB分配行关联到上一行，按计划相位（含预采样两帧带来的约30ms偏移）推断动作。901个事件为179 Roll、180 Reorder、其他三种各180，加源代码补足180秒时的2次末尾Refresh。标签不是方法级测时。原始逐帧证据未改。

| 推断动作 | LateUpdate间隔均值 / P95 ms | 下一间隔均值 ms | 两间隔合计均值 ms | GC均值 |
|---|---:|---:|---:|---:|
| Roll+保存+刷新 |32.178 /36.460|3.044|35.222|2.179MB|
| Reorder+保存+刷新 |32.347 /36.826|3.077|35.425|2.158MB|
| SkipVisual+刷新 |18.352 /18.815|15.077|33.429|56.334KB|
| TogglePending+刷新 |18.319 /18.676|15.151|33.470|55.931KB|
| ClearPending+刷新 |18.190 /18.673|15.253|33.443|55.262KB|

所有846个>18ms、536个高于整体P95、362个>20ms、359个>25ms帧都对应操作。原来的纯刷新历史增长约792B/秒，现在约0B/秒；这支持已提交快照缓存真正消除了无提交刷新复制历史的成本。三类纯刷新由Player04约23ms、1.24MB降到约18.3ms、56KB。

LateUpdate长间隔不能直接当成单方法CPU工作时间。纯刷新18.3ms随后15.1ms，两间隔合计约33.45ms；对应完成帧Main Thread均值16.72ms、P95约17.01ms。工作发生在本帧LateUpdate之前，会使测点相对前帧移动；不能将18.3ms全部解释成CPU实际执行了18.3ms。本报告也没有用两帧均值替换原始P95。

但事务后半程确有进一步的帧总耗时增长，不能只修调度：

| 事务 | 头30秒 → 尾30秒每次GC | 同期完成帧Main Thread均值 | 同期两间隔合计均值 |
|---|---:|---:|---:|
| Roll |0.901 →3.468MB|16.823 →21.405ms|33.596 →38.182ms|
| Reorder |0.870 →3.445MB|16.800 →22.238ms|33.532 →38.968ms|

Roll/Reorder分配增速各约17,183B/秒；纯刷新完全平坦。单看Roll已有38帧、Reorder41帧的完成帧Main Thread>20ms。这与增长的回执/全量存档路径吻合，但尚无方法级标记，不能把计算、磁盘同步等待、GC各自占用多少ms说成已测事实。

## 两种问题分别存在

排除动作及随后补偿帧，仅用于诊断的8923个其余帧P95为17.013ms。两个独立20秒窗口idle试验的LateUpdate / FrameStart / PresentCalled / FrameComplete P95分别也约17.0ms；原始数据均完整，独占全屏两次未落实且已恢复。这证明即使没有这些游戏操作，软件60限帧也有节奏抖动。PresentCalled不是scanout，FrameComplete是GPU完成后CPU通知；不选择其中最好看的指标替代正式判据。

同时，历史较长时事务帧增长到21–22ms均值。因此通用调度修复与实际工作优化都值得验证。固定epoch逐周期累加会在晚帧后产生`T−lateness`的短补偿；仅跳过已过期deadline仍不能保证下一次距离满一周期。重锚可避免追赶，但会牺牲平均频率/相位，需要实测，不能通过调整probe执行位置制造通过。

## 现在可做：保持语义的小修改

1. **复用无恢复状态的validator，去掉旧档校验中的恢复Clone。** `AtomicSaveStore.TryRead:78` 当前`new GameSession(config,new MemorySaveStore(),state)`每次重做cfg.Validate，并将已经反序列化的旧档全Clone后立即丢弃。已有`GameSession.ValidateState`是public，构造一次`validator = new GameSession(cfg,new MemorySaveStore())`，以后调用`validator.ValidateState(state)`即可保留全部现有状态条件；无需新增Core API。可以预期每次存档少一次完整历史Clone及一次完整配置验证，但不能承诺具体ms收益。依据之前纯Clone增速推断，尾端省去的回执副本约150KB/事务量级，只占当前3.45MB的一部分，不能先宣布它单独解决问题。当前GameApp配置加载后不变；若未来允许动态改配置，应重新验证配置/重建validator。**root已选择先做此处，缓存另切片。**
2. **按Session引用+actionSequence缓存ScorePanel的PreviewScore结果。** `GameApp.cs:233`仍每次刷新执行`GameSession.PreviewScore`→`FreezeSubmission`（GameSession.Submission.cs:8–23）→Evaluate，复制7颗×6面及牌型数组。Skip/Pending/Clear不会改变已提交得分，缓存可省三次重复计算/秒；Roll减少余掷、Reorder改顺序、道具/提交/买入等序号变化必须失效；独立Session同序号也必须失效。不能缓存真正Submit的计算结果代替规则验算。预期是固定的部分55KB刷新分配，具体大小需要方法级Profiler或独立分配实验，不能预估为全部55KB。
3. **避免重复禁用/启用未变化UI和骰子。** 当前`GameApp.Refresh:67`先UiKit.Clear(main)/table.Hide，`UiKit.Clear:81–85`逐个SetActive(false)，`Rent:31–34`再次清子项、SetAsLastSibling、SetActive(true)。池化省了对象创建，但TMP/Button生命周期与Canvas dirty仍重复触发。可把同页稳定元素改为更新变化属性，或在完整UI构建pass结束时只禁用本轮未使用的旧项；保持新的父级/角色key和监听器去重。预期减少三次纯刷新约1.5–1.7ms的测点位移的一部分，不能承诺全部消失。风险高于前两项：失效视图、旧商店卡片、模态滚动位置、点击监听器、焦点/输入、排序遮挡。必须覆盖场景切换、10骰/7槽/10C/5I、三分辨率、重复模态及销毁释放。
4. **小查询合并。** Gameplay与每颗DrawDie多次访问`PendingIds.ToArray()`（GameApp.cs:172/216），一次Refresh捕获一次pending集合即可；按钮构造反复闭包/UnityAction可在稳定控件上绑定一次分发器。收益小于状态/JSON路径，优先级低，不值得为微分配改动事务逻辑。

## 下一独立切片：确切内容身份缓存旧档完整校验

这项设计可行，但比复用validator增加新不变量，建议在简单修复实测后单独实施。

1. Save候选先通过相同完整State校验，再序列化为将实际写入的确切bytes、写临时文件并Flush(true)。
2. 每次覆盖前仍读取当前活动档全部bytes并核对其内容hash；只有与此前完整校验成功且绑定同一配置内容指纹的bytes完全一致，才能跳过JSON反序列化和完整状态验证。可直接字节比较增强身份判定；不得只看mtime/大小，也不能只绑定configVersion（同版本配置内容也可能变化）。
3. 当前bytes变化时走现有完整包络/校验和/State校验；损坏活动档继续保留rejected副本，禁止覆盖有效backup。外部替换为其他有效存档也必须完整验证。
4. 仅File.Replace/Move成功返回后，把候选确切bytes标为活动档缓存；IO失败不更新缓存、不发布候选、不重抽RNG，Retry保持既有冻结候选语义。缓存只优化“旧档是否足以备份”的判断；Load仍完整解析产生独立DTO最简单。
5. 配置应绑定已验证的不可变内容摘要；若当前配置可变，则内容改变就失效并重新验证。缓存本身不暴露给UI/调用者，不能被外部修改。

预期收益：避免增长旧档的ReadAllText/包络与payload反序列化/重复State检查和实例字符串；仍保留读取、内容校验、候选序列化、durable写入、原子Replace与完整回执。确切bytes缓存约一个当前存档大小，尾端约197KB，内存换取反复分配。现CSV没有这些函数耗时，不能承诺收益百分比。

独立风险：同大小同mtime外部字节改动漏检、缓存提前更新导致失败后误判、配置更改后缓存仍被信任、Load拿到缓存可变对象、有效backup被坏当前档替代。现实现“读取校验→File.Replace”之间原本就存在外部并发写TOCTOU；缓存不解决它，不能声称加强了跨进程并发安全，保持单写者假设并记录边界。

验证应包括有效当前/有效backup、损坏checksum、checksum有效但State非法、无Load直接Save、同大小同mtime变更、外部替换合法档、配置变化、首次Move失败/Replace失败/磁盘写入失败、Retry冻结候选和RNG、duplicate回执恢复。先跑现有11条Persistence与114条事务，再按新增缓存不变量补针对用例，最后同180秒完整Player。

## 不建议的“优化”

截断回执会破坏跨恢复幂等；异步先展示再保存会破坏提交语义；去掉候选/存档器隔离副本会允许外部写入；关闭Flush、减少操作或提高targetFrameRate改变要求。均不用于本修复。若存档仍超预算，可先给Clone、Validate、JSON、TryRead、Flush和Refresh加低开销标记，再按真实函数成本决定后续，不能把CSV相位统计当作这些函数的计时。
