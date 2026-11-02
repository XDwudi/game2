# dca7a26 实际Player资源诊断与启动参数拒绝独立复核

2026-11-02，独立审查者 `/root/v07_run_review`。本次只读原始文件、复算CSV、检查源与实际构建绑定；没有启动Editor/Player、读取实时Registry、操作界面或修改生产/门槛。作者分析只作消息背景，以下由正式归档和原始cache独立重算。

**规格符合性：本次独立诊断的真实执行、观察条件、隔离与四项明确参数拒绝通过。制作质量：局部几何和所采资源观察有证据；绘制开销目标在结算场景未达，全部资源目标及整版验收未完成。** 不将exit0或诊断完成写成最高特效、UI延迟、物理显存、M5或整版通过，也不把建议预算改成新的硬门槛。

## 归档、源与范围

| 实际产物 | 独立核对 |
|---|---|
| `evidence/v07/resources/v07-resource-player-dca7a26-0243` | 12原始文件、1,451,441字节，另附archive.json |
| `evidence/v07/resources/v07-resource-negative-dca7a26-0242` | 28原始文件、857,512字节，另附archive.json |

40文件的原cache、正式副本、archive清单SHA/大小三方完全一致，未改写嵌入的origin绝对路径。两个archive的sourceCommit均为 `dca7a2610f39d65088b9541549d3465c388fd3e7`。实际Windows GUID `388c2eee37244ba48bb656114b6320bf`、Runtime MVID `509a9a1c-df39-43d7-927f-c5f787977a35`、玩法p0.6.0/schema4、art0.7.0、配置SHA与已独立审查的正式Player/实际DLL一致。EXE、Runtime、boot.config和build-report SHA另核对一致。

已审运行脚本v3 SHA859333e3…、负例driver5433d2a…与正式tools/diagnostics/v07字节相同；资源probe17d80bf9…、两个既有入口互斥guard与022904冻结源一致。实际Player PDB93文档检查沿用紧邻的[正式Player独立复核](PLAYER_DCA7A26_REVIEW.md)，不把Editor程序集当成Player。

正例PID38216自然exit0，02:43:16–02:44:26，无超时、强杀或残留进程；报告为diagnostic_completed_not_acceptance，wholeReleaseAccepted/formalPerformanceAcceptance均false，error为空且外层errors为空。环境为真实WindowsPlayer、D3D11、RTX3080、Ryzen5900X、Development Build、1920×1080 Windowed。

## 实际路线和观察覆盖

源码通过既有UI按钮进入手册结构/颜色页、设置三页及完成；通过真实debug工作区与Apply/Submit/EndStage/Continue事务进入商店，再应用十普通骰场景。18段phase、9次实际对象快照顺序与之吻合。**商店只查看保存的当前tab（本次shopTab=3），没有遍历全部商店分区。** 不把已加载贴图清单等同所有内容已实际显示。

保存的debug-run envelope SHA有效，最终Selecting/actionSequence5，10骰、8提交容量、10控制器、5道具；所有骰非玻璃、骰面非镀金且texture=none。实际Body renderer清单与此一致。idle前预热至少3秒，观察数组/边界哈希在窗口外准备，窗口内无probe事务、枚举、截图或文件编码；真实游戏自己的计时行为未禁用。

这两个归档没有PNG，本次实际观看资源诊断截图为**0张**。GUI路线结论来自真实phase/状态/按钮源合同，不是额外人工视觉验收；此前9张正式Player图保持其独立范围。

## 20秒稳定闲置逐行复算

`idle-frames.csv`：1200行，Unity帧2732–3931连续、elapsed严格递增，实际窗口20.0021907秒。每行counterBatch=1且gcAvailable、focused、motionIdle、pointerUnchanged、inputInactive均true，动作序号始终5；没有把缺样本填成0。

- 1199行GC=0；第2889帧唯一非零为132 B。总132 B、均值0.11 B/frame；**不是1200帧全部零分配**，没有证据将132 B归因到某个具体系统。
- idle前后Session.State SHA、pending和三份隔离存档/动作日志的SHA与字节数完全相同，归档文件亦匹配。没有将普通用户存档当作调试输出。
- 内层23项明确偏好before/after/restored完全相同；App先销毁，本次唯一登记的clock key实际列入removed。外围游戏偏好完整守卫同时支持没有遗留本次新key。
- idle对应的1200条资源行Draw Calls均96。原始全局Triangles Count为13146，其中包含UI等；它不能替代下述普通Body几何统计。

“尽量0 B/frame”保留为规格原有目标：此次已经量出单个非零样本，诊断观察完整通过并不把该样本隐藏，也不新增零分配硬失败标准。

## 纹理与几何

9次快照均为94个不同entityId的Texture对象，未把同一原生对象按引用重复计数。字体集合直接匹配5个实际TMP font asset的atlas ID（含DiceChinese、DiceChineseFallback、font_numbers_tmp），font拥有者引用可追踪；art有真实引用owner，RenderTexture单列，unknown保留，不从总数中删除。

| 分项 | Native对象字节 | MiB（1024²） |
|---|---:|---:|
| 字体atlas | 12,062,258 | 11.503466 |
| 有实际美术引用 | 40,896,704 | 39.002136 |
| RenderTexture | 78,905,026 | 75.249697 |
| 未分类 | 1,690,406 | 1.612097 |
| 合计 | 133,554,394 | 127.367395 |

独立按对象重新求和与每次快照完全相等。3930条resource CSV中首行三counter空缺保留为空；后续Texture Memory峰值同为133,554,394，未用全局counter减字体推算“剩余显存”。`Profiler.GetRuntimeMemorySizeLong(Texture)`是原生对象内存口径，**不是物理GPU驻留/显存或CPU/GPU独立分摊**。当前实际引用art 39.002 MiB低于128 MiB建议值只是所采集合观察，不能因此签全部场景峰值/最高特效预算通过。

十个真实普通Body各188三角形，总1880≤20000，按实例累计；另有10个outline pass和60个实体骰面，未混进Body分子。碎片6个唯一mesh各20、合120三角形；池内12个实例共240，全部inactive。池容量清单不能证明最高演出时活跃碎片峰值、特效叠加或整体GPU三角预算。

## 必须保留的Draw Calls目标未达

真实ProfilerRecorder的Draw Calls Count逐行重算，原§31起始目标≤180在此次路线中确实被超过：

- `command:EndStage`段223行：2行59，1行202，198行212，4行213，18行214；其中**221行>180**，峰值214。
- `command:Continue`段前两行仍为212，之后转为28、31等；这两行也保留在**总223个超目标样本**中。
- phase标签在命令调用前登记，counter读的是已完成帧；Continue开头的两行可能承接结算画面，不能因其标签变了就删除或强行归因商店稳定绘制。
- 完整CSV3930行、帧2–3931连续；after-idle快照在3932帧完成，结束前没有下一条LateUpdate行，不伪造额外CSV样本。

以上是已有绘制目标的实际未达，不是新设发布硬门槛。也不改变此前180秒P95 16.7929>16.7的正式性能失败，不能写“资源诊断通过所以性能达标”。

## 外围守卫与四项拒绝

正例保留了完整before/after清单：**306构建文件、17普通LocalLow文件、36游戏偏好hash、17引擎Screenmanager hash**均完全相等。构建与普通文件还逐SHA/大小核对了审查时实际文件；这是截至本次审查的范围，不承诺后续启动不会正常写入。306比正式串行305多含一个Burst备份txt，来自本工具不筛除backup/未知文件的完整树合同。

四负例均自然exit1、没有timeout/强杀/遗留进程，各自Player.log含精确预期ArgumentException及Art07ResourceDiagnostic来源。没有旧verify/pacing输出目录或日志标记、没有无关崩溃标记、没有资源report/CSV；非空目录中的57字节sentinel原样保留。

| 案例 | PID | 实际预期拒绝 |
|---|---:|---|
| missing-path | 25072 | requires one fresh absolute output directory. |
| nonempty-path | 13200 | Evidence directory must be fresh or empty. |
| mixed-verify | 26988 | Resource diagnostic cannot run with verification or pacing modes. |
| mixed-pacing | 45040 | 同上 |

逐例/整suite的306构建、17普通文件、36游戏偏好完全一致；相邻after→before链也一致。**第一例改变4项Screenmanager宽高元数据hash**：Resolution Width/Height及Window Width/Height；其余13个engine键不变，后三例17个均不变。suite.engineScreenmanagerUnchanged=false被正确保留，不能写“所有偏好未变”。没有读取这些键的原值或反推未知设置；它们是引擎显示存储，单独记录，不当游戏偏好守卫或原生显示验收。

旧入口冻结代码会在创建host前创建其输出目录，因此正确旧参数下无目录/日志支持互斥guard生效；本审查没有枚举运行时GameObject，不声称直接证明对象从未创建。四项测试仅覆盖这四类参数，不推广到所有非法参数。

## 限定结论

这批实际诊断可用、可追溯，四项拒绝与隔离合同符合预期，没有新增生产代码阻断。应保留结算Draw Calls未达、单个132 B分配以及未遍历场景的限制；最高级特效、UI点击延迟、完整美术资源峰值、物理VRAM、M5/Intel Mac和人工美术仍待各自真实证据。

这里的完整外围guard只覆盖02:42–02:44的负例/资源运行，**不能追溯替代正式02:34–02:41流程缺失的普通存档逐项before清单**。报告不撤销[正式Player复核](PLAYER_DCA7A26_REVIEW.md)的失败与证据限制。

独立复算脚本/事实/输入SHA在 `artifacts/cache/v07-resource-player-dca7a26-review/{audit.py,facts.json,bindings.json}`，正式报告另附同名bindings。未改原始文件、旧失败、阈值或整版接受状态。
