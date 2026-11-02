# dca7a26 实际 Windows Player 独立复核

复核者 `/root/v07_run_review`，2026-11-02。输入为 `evidence/v07/player-dca7a26-0234` 及对应原始日志；实际串行运行02:34:36–02:41:47 UTC。本复核未启动Editor/Player、未操作显示、未改生产、验收阈值或原始JSON。大文件/CSV核对在root确认独立资源20秒idle已结束后执行。

**规格符合性：部分流程通过，完整Windows验收未完成。制作质量：有缺陷（正式帧时超预算、独占全屏确认失败）；本次抽看未发现新的确定视觉缺陷，旧TMP尾影字块在本次Player对应截图中未再出现。整版/美术/M5均未ACCEPTED。**

## 构建、源及运行身份

- 冻结源 `dca7a2610f39d65088b9541549d3465c388fd3e7`，p0.7.0/art0.7.0、玩法p0.6.0/schema4、Unity6000.6.7f1。配置与黄金集保持冻结值。
- 实际Windows报告 `build-windows-20261102-023220.json`：Succeeded、0错误/3警告、190,976,889字节，GUID `388c2eee37244ba48bb656114b6320bf`。实际boot.config、EXE/Runtime SHA与串行输入绑定吻合。
- 独立执行只读PDB审计：实际Player Core/Runtime两程序集、93个项目源文档、0差异，全部源checksum与022904冻结测试快照一致。实际Runtime MVID `509a9a1c-df39-43d7-927f-c5f787977a35` 与三个流程及恢复报告一致；不把Editor的Runtime MVID当成Player。
- 真实设备为Windows10 x64、Ryzen9 5900X、RTX3080、D3D11；Development Build，1920×1080、Windowed、PC质量、vSync0/targetFrameRate-1。未从这一结果推定其他设备或Mac。

## 流程分别结论

| 流程 | 实际结果 | 可支持范围 |
|---|---|---|
| strict-full，PID13440 | exit1，V07NativeDisplay失败；普通145/145完成 | 普通回放与前置UI通过，原生mode0不能确认；Debug24/压力没有执行 |
| native-deferred-independent，PID38152 | exit3，Completed/budget_exceeded；普通145/145、Debug175/175 | 完整玩法/UI/Debug24与180秒采样已完成；原生显示明确not_executed，帧时失败 |
| native-display-only，PID45916 | exit1，同一mode0确认失败 | 独立显示诊断失败，无玩法回放/压力结论 |
| separate-process-recovery | 外层与reader通过 | 跨进程pending显示设置恢复的独立限定场景通过；不替代显示矩阵 |

三个主进程均自然退出，无超时或外层强杀。逐项原始process.json与serial-summary相等，报告SHA、身份、trace/config及异常状态一致。strict有17项UI、deferred18项UI通过；deferred其余v0.6兼容检查均通过，unityErrors为空。

独立重读两条普通JSONL，各145索引连续；每步kind、sequence、phase、score、gold、stage、预算与权威trace类型/值相等，savedStateMatches/renderedStateMatches均true。普通轨迹的真实终点是**第8关RunFailed**，不是普通通关24关。deferred Debug轨迹175步完整且到24关完成，校验了真实磁盘/渲染状态；此为调试固定场景，不证明普通构筑可通关或最终平衡。两个主回放的隔离normal-save SHA在压力/调试后仍与报告stress前一致。

## 原始性能复算

CSV路径：`native-deferred-independent/player-20261102-023622-9ef3d5e7/performance-frames.csv`。独立复算连续帧号、单调elapsed及interval；原始10116行明确排除第一行起始partial，剩10115有效区间。采样180.005549秒，最后CSV时刻180.0033521秒。

| 指标 | 独立复算 |
|---|---:|
| LateUpdate区间 mean / P95 / max | 17.7945412 / **16.7929000** / 80.0241 ms |
| 原有预算 | P95≤16.7 ms；**失败** |
| 主线程P95 | 18.7442 ms |
| CPU FrameTiming P95 | 18.7488 ms |
| RenderThread P95 | 0.3415 ms |
| 有效GPU样本 | 9674/10115 = **95.6401384%**，≥95% |
| GPU P95 | 0.320512 ms；不能抵消整帧失败 |
| GPU缺失/无效/重复 | 441 /0 /0；缺失未填0或丢弃整体帧 |
| GC allocated mean / P95 / max | 282624.34 /530080 /12868649 bytes/frame |

原始GPU状态计数为9675个valid+441个zero/unavailable，首个valid属于被排除的partial行，所以有效GPU数9674。八组主统计的samples/expectedFrames/coverage/mean/P50/P95/max均与报告一致；另外三种native时间戳差值逐行按真实10MHz CPU频率重算吻合，作为诊断，未替代LateUpdate验收。

采样为10骰、8提交栏、10控制器、5道具，10秒warmup+180秒，roll/reorder/pending-clear各190次（含warmup），固定60000容量无采样期扩容。没有改变负载、删慢帧或借其他流程补齐通过。报告focusedSampleFrames为10116/10116，与原始行数一致；**CSV本身没有逐行focus字段，因此全焦点仅能按探针汇总声明，不能声称逐帧独立证明焦点**。计时警告记录为0。

## 原生显示与跨进程恢复

strict/native-only各自12条真实过渡读回均focused=true：Windowed9条、ExclusiveFullScreen1条、FullScreenWindow2条；canConfirm始终false，最后返回1920×1080 Windowed。两个原始Player日志均记录ExclusiveFullscreen切换失败并回退，probe随后在mode0确认断言失败。可确认“未达到稳定可确认的独占模式”；不能写“API从未返回Exclusive”或无证据归因某驱动、远程桌面。后续模式/分辨率/超时Esc矩阵没有执行，deferred也明确未执行。显示专项不能与deferred拼成strict通过。

独立恢复writer PID48388、reader PID26840：writer真实到达pending候选，外层发现ready后约78ms仅终止本次拥有的writer句柄；reader自然exit0。ready/pending备份/receipt SHA与原存储匹配，恢复至VG27AQ1A上的1280×720 Windowed，pending=false。writer/reader当前config SHA均747ed1ef…，GUID/MVID/身份与主构建相等；外层mainReportSha绑定本次deferred。候选mode1→已确认mode3的恢复范围通过，不等于独占全屏已通过。

## 守卫证据边界

305个实际构建文件的当前SHA与serial-summary完整清单逐项相等，EXE/DLL/boot身份另外核对。回放目录内normal-save的前后绑定亦核实。

**普通用户存档不同：本次正式runner只在内存保存before列表，finally实际比较后只持久化ordinarySaveFilesUnchanged=true。没有历史逐项before/after清单，本审查只能接受“已审runner报告比较无变化”的运行声明，无法独立重算那段历史SHA。** 未补造历史清单，未用稍后资源/坏参数流程的完整external guards追溯替代。该证据限制保留，不扩大为全部用户文件/偏好均被本次独立证明未变。

## 实际画面复核

本次共204个PNG，仅实际查看**9张**新Player图，其余195张没有逐图观察；未重复宣称932张Editor归档全看。精确路径/SHA/尺寸/捕获状态及逐图结论见cache `supplemental-facts.json`，并纳入本报告bindings。

| 已观看图片 | 结论与局限 |
|---|---|
| strict与deferred各`v06-debug-32dice.png` | 两张均无旧TMP不透明白色字块，中文/按钮完整；RunDebugScene后modalOpen=false但尾影尚在淡出，不能当稳定叠层布局 |
| deferred `normal-shop-1280x720.png` | 控制器、价格差额、分类、骰面与离店操作可读 |
| `normal-run-failed-stage-8.png` | 失败结果与差额可读；统计/构筑行仍渐显，不能用此帧签稳定最终对比度 |
| `debug24-final.png` | 24关/调试标记/终骰可读；捕获时统计尚未展开，不当稳定全部内容验收 |
| `debug-score-preview-1920x1080.png` | 字形正常但透明重叠的入场外观，不作为稳定布局通过依据 |
| `debug-score-preview-bottom-1280x720.png` | 末尾公式、向下取整、永久效果说明及操作按钮可读 |
| `v07-settings-720p-text150-bottom.png` | 150%文本下口令、快捷键说明及完成按钮可见、可读 |
| `debug-stress-final.png` | 多控制器/提交骰/独立乘区与操作清楚，无旧尾影字块 |

probe的Capture只等待下一帧/end-of-frame并检查尺寸与非黑内容（PlayerVerification.cs:1322附近）；它没有保证每张所有入场/退出动画已经结束。上面明确区分过渡与看起来稳定的截图，未把accepted/contentValid字段等同人工视觉通过，也未用图片推断物理显示模式。旧P07-MODAL-TMP-01修复同时具有此前真实Editor红绿/逐字形GPU检查和本次实际Player两张对应画面的支持；这只关闭具体字块缺陷。

## 结论与后续

可以保留本构建为明确标注FAILED/待验的诊断候选，不能作为完整Windows或发行质量通过。需继续定位帧时、原生独占显示，按独立场景审资源/坏参数实际结果，并保留M5、AudioMixer及人工美术体验验收。完整Editor1013/158通过不会覆盖这里的实际失败。

复算脚本及证据：`artifacts/cache/v07-player-dca7a26-review/{audit.py,player-pdb.json,facts.json,supplemental-facts.json,bindings.json}`。所有原失败、CSV、trace与报告未改写，正式原生/性能阈值保持。
