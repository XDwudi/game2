# Player06：release之后的UI工作与呈现节奏

本报告仅分析 `evidence/runs/player/player-20261029-024615-1c6316db`。未修改生产、探针或判据。原检查器按LateUpdate P95给出exit0；root根据并列时钟决定性能仍未接受，这两个结论分别保留。

## 对齐实际数据，而非把周期当计算耗时

10,777完整帧；LateUpdate P95 **16.6751ms**，Main Thread **18.7536ms**，FrameStart **18.7553ms**，PresentCalled **18.7520ms**，FrameComplete **18.7508ms**。三个新增时钟均100%有效、原始timestamp与逐行delta已由独立审核复算。CPU计时器10MHz，FramePacer报告已安装；原生target=-1、vSync0，由普通生产调度器实现60Hz最大节拍。

`analyze_player06.py`保留全部完整帧，另外以>10KB分配行为操作标识做诊断。使用0/1事件序列与各时钟全量交叉相关，观察到相对GC行：Main Thread峰值同一行（r=.7503），Present/Complete延后2行（r=.7518），FrameStart延后4行（r=.7507）。这是本轮实际观察延迟，不宣称Unity所有环境都固定如此；不能把同一CSV行的所有字段当成同一个Native帧。该关联只诊断，不改变验收样本。

| 推断操作 | 事件数 | LateUpdate操作间隔均值 | Present对应间隔均值 / P95 | 下一Present均值 | 两个Present间隔合计均值 |
|---|---:|---:|---:|---:|---:|
| Roll+保存+刷新 |179|17.630ms|19.865 /22.489ms|14.518ms|34.383ms|
| Reorder+保存+刷新 |180|17.540ms|19.845 /22.511ms|14.436ms|34.281ms|
| SkipVisual+刷新 |180|16.671ms|18.725 /19.065ms|14.626ms|33.351ms|
| TogglePending+刷新 |180|16.673ms|18.777 /19.166ms|14.584ms|33.361ms|
| ClearPending+刷新 |180|16.674ms|18.659 /19.036ms|14.698ms|33.356ms|

901个Present>18ms帧中899个和上述操作匹配；全部118个>20ms帧都匹配操作。另2个是采样边界返回的较早Native记录。LateUpdate计时窗后还有FTM管线延迟，不能从边界缺少对应动作推断出新玩法行为。所有原始值都保留。

三类纯刷新无存档且GC仍稳定55–56KB：release处约16.67ms，Present先约18.7ms再约14.6ms，两次合计约33.35ms。这表明**release之后的工作量变化约2ms会平移本帧Present时点**，而不是每个游戏Update都计算了18.7ms。FrameStart、Present、Complete看到类似长短对，LateUpdate看不到它；这正是只凭release附近采样通过仍不足以签署呈现平稳的原因。

Roll/Reorder还有逻辑/保存工作，其LateUpdate操作子集本身就有约20.2ms P95，因此不能把所有偏差归UI。两Present间隔合计约34.3ms也高于纯刷新约33.35ms。CPU工作、磁盘同步等待与GC尚未按函数拆解，不报告未经测量的某个函数耗时。

去掉操作及紧随后一帧的诊断余集Present P95为16.8064ms（仍保留原全量验收）。所以即使去掉主要2ms摆动，也不能提前承诺所有呈现差值≤16.7ms。这个余集不是新的通过判据。

## `Canvas.ForceUpdateCanvases` 是否是合理最小修复

**是合理的生产试验，预期把已经发生的UI变更所需的布局/文字几何工作放入本次逻辑刷新，调度器之后等待剩余预算。** 不应称其让原生后续“只剩提交”或保证完全消除后段成本。

官方说明Canvas通常在帧末、渲染前完成布局和内容更新；ForceUpdateCanvases可在调用处要求完成内容更新。[Unity 6000.6 API](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/canvas/forceupdatecanvases)

本工程锁定的`com.unity.ugui@eb824e41fbc7`本地源码提供了具体链路：

- `CanvasUpdateRegistry.cs:101`注册`Canvas.willRenderCanvases → PerformUpdate`，处理layout/pre-render并清空队列（约175–244行）。
- `TMP_UpdateManager.cs:64`也注册同一事件，`DoRebuilds:170–215`处理并清空TMP的layout/graphic/culling队列。
- `TextMeshProUGUI.cs:213–231`的PreRender调用OnPreRenderCanvas；约2450–2499行在dirty时解析文字、更新字库/几何并清除dirty标记。
- GameApp.Refresh把整页UI禁用再启用并更新文字。池化减少了创建，但这些操作仍可产生待处理内容；当前调度位置在PreLateUpdate首部，早于后续Canvas/TMP队列自然执行点。

因此在**普通GameApp统一Refresh出口**调用一次ForceUpdateCanvases，有真实因果基础：同一帧必要内容先完成，再进入既有节拍等待；内容和规则不变，CPU成本仍计入该帧，超预算仍会晚帧。不能在probe里调用、按压力相位特判、移动probe或只改计时点。root已实施统一`Refresh → RebuildView → ForceUpdateCanvases`，覆盖旧菜单提前return；代码提交`f060098`及20条UI回归结果由root提供，本报告没有代为操作Unity。

## 限制与回归风险

- API更新所有Canvas，而不是单个main面板；现项目只发现UGUI/TMP内部队列，没有自定义willRender/preWillRender/TextChanged业务回调订阅。若未来新增回调，要防重入和重复副作用。
- 已清队列在本帧稍后通常只做空队列/轻量检查，但后续LateUpdate、动画、ScrollRect、字体图集变化仍可能再次置dirty。原生batch、渲染提交和驱动同步也不保证被提前；不能由源码证明它们全部消失。
- 不能在每个UiKit.Text/Button调用后分别Force，或放每帧LateUpdate强制重建，否则会放大重复工作。当前完整刷新末尾一次的范围更小。
- 必须看输入框focus/字符、商店与计分模态、滚动top/middle/bottom、三分辨率、10骰/7槽/10C/5I，以及normal97步的真实截图。重新渲染时间提前不能导致漏字、截断、旧按钮或脏几何。
- 旧`Canvas.BuildBatch`指标只有很小值，并不覆盖完整TMP内容生成；它小不能排除这条路径。若试验不改善Present/Complete，下一步应测`TMP.GenerateText`/Canvas.PerformUpdate等真实标记，或减少整页SetActive/属性重复更新，不能把现假设当已确认原因继续盲改。

## Player07的只读比较计划

root正在实际运行新构建；采样期间本代理停止编译和CPU分析。结束后按同一原始方法比较：原LateUpdate、Main、三个FTM系列全量P95/最大值/覆盖；五类操作对应Present与下一间隔；Roll/Reorder的晚帧和历史增长；截图/正常命令与状态一致性。若只有LateUpdate改善而Present/Complete仍摆动，则该生产修复不算解决呈现问题。既不取最好的时钟，也不抹去Player06的exit0与未接受结论。

可复算产物：`player06-analysis.json`、`player06-operation-events.csv`、`analyze_player06.py`。JSON保存源CSV及当前读取源码/包源码SHA；读取时root可能已有f060098修改，因此这些源码hash是本次静态阅读身份，不冒充Player06构建源码。
