# 下一轮性能工作的已有证据与去重清单

只读归纳，2026-11-02；未启动实验、未改生产/阈值，未读取4.6GB Profiler raw。当前冻结游戏源 **dca7a2610f39d65088b9541549d3465c388fd3e7**，Windows GUID **388c2eee37244ba48bb656114b6320bf**，Unity6000.6.7f1/Mono、5900X/RTX3080、D3D11、1080p Windowed。旧实验保留旧身份，不拿旧版本通过放行v0.7。

**结论：普通限帧、独占/VSync路线、BitBlt、通用deadline调度及UI尾部刷新都已有尝试；当前并非忘记打开限帧。GPU约0.32ms与帧间隔P95 16.7929ms不矛盾：60Hz节拍本来约16.667ms，真实同步事务/历史序列化造成大量30–80ms工作帧，再叠普通节拍尾部抖动。下一项有依据的工作是定位/减少命令里的保存、快照与分配，而不是重跑已失败的配置或把目标fps调高。尚无已量测、可保证闭合16.7的低风险下一补丁。**

## 已经做过哪些配置实验

以下四份原始 `pacing-report.json` 已本次实际阅读；各模式为3秒预热/20秒空闲菜单，不能代替180秒压力。

| 实验/身份 | 实际配置与结果 | 不应再次误解为 |
|---|---|---|
| `evidence/runs/pacing/spike-01/`，GUID `2fc258646da4436f8c59632326984bcc` | vSync0/target60窗口模式20秒，LateUpdate P95 **17.0061**；随后请求1080p60Hz独占+vSync1，在切换阶段实际回退2560×1440 FullScreenWindow，实验失败；已恢复144.006Hz桌面 | vSync1模式完整采样过并证明无效 |
| `spike-02-bitblt/`，同GUID | BitBlt路径尝试，实际窗口target60 P95 **17.0193**；独占60Hz请求再次回退并失败，恢复确认 | BitBlt已实现真实60Hz独占或能解决当前保存尖峰 |
| `spike-03-deadline/`，GUID `f1f02fa662804c3b9409b602e4a96e9f` | 同一idle对照：native60 **17.0036** → 自有deadline60 **16.6742**；FrameStart **17.0060→16.8128**、Present **17.0289→16.8051**、Complete **17.0150→16.8281**；全程focused，完成且恢复 | 只平整LateUpdate，或已经覆盖最大场景/输入延迟/功耗 |
| `spike-04-lifecycle/`，GUID `f7f0381585684fde8b60e3a4929dd73a` | native60 **17.0123** → deadline60 **16.6726**；重复安装/停止、同帧重装、实际host销毁与原生cap恢复通过 | 物理OS切焦点/暂停都实测；该部分是向真实组件注入回调 |

独立旧复算：`evidence/review/pacing-spikes-independent.json`、`pacing-deadline-independent.json`、`pacing-lifecycle-independent.json`。解释与取舍：`FRAME_PACING_DECISION_REVIEW.md`、`DEADLINE_CANDIDATE_REVIEW.md`、`docs/decisions/P0-FRAME-PACING.md`。

还有更早的 **vSync1 / target60** 真实报告，不应漏报：

- v0.4 `evidence/runs/player/player-20261029-005142-873bffd3/`，GUID `4f8ffc07be074280b4d4f2e40a53a767`，`measured_with_gaps`：GPU有不可能值，UI采样缺口，不能采信约0.7147ms P95为有效60Hz性能。
- `player-20261029-011604-832cff6f/`，GUID `e92f09b5bef848bda6bc52618d8ee682`，180秒287,961完整帧、约0.7286ms P95；实际未保持60Hz节拍，观察数组扩容三次，正确保持 `measured_with_gaps`。参见 `evidence/review/PLAYER02_VISUAL_REVIEW.md`、`PLAYER03_REVIEW.md`。
- 这些记录不能证明用户驱动面板强制禁用了VSync，不能据此归因远程桌面/锁屏。当前没有完成的、同dca场景的硬件60Hz+VSync1有效对照，也没找到vSync2/3、经确认60Hz桌面或高分辨率waitable timer的新实际采样。这些是**未完成/未见证据**，不是已排除所有常规配置；但旧独占请求反复失败，缺少新条件时不值得盲重试。

## 已经实施的优化，不要重新作为“新方案”

1. **通用Windows FramePacer已进入普通Runtime。** 当前报告采样前后均installed，vSync0/target=-1是由自有60Hz调度接管，不是无限帧率。以ceil(QPC频率/60)为周期，实际release后重锚，不追赶；粗Sleep+最多1.5ms尾spin，PreLateUpdate首节点，失焦/暂停Sleep17。停止恢复native fallback。`Runtime/FramePacer.cs`与当前report说明一致。
2. **UI池化、显示快照复用、Refresh末尾ForceUpdateCanvases已存在。** v0.4 Player06虽然LateUpdate16.6751通过，但Present/Complete约18.75，因此没有据exit0放行。`f060098`/build10（GUID `ee0c4c6ab89e41008cade950261da73a`）仅把已排队Canvas/TMP工作统一完成，899组刷新长短摆动消失；Player07完整180秒LateUpdate16.6807、Present16.9369、Complete16.9452。详见 `evidence/runs/perf/PLAYER06_VS_07.md`。没有必要重新提出“每次Refresh强刷Canvas”而忽略已经做过。
3. **最近已验证存档字节缓存也早已落地。** v0.5 `PLAYER01_AND_SAVE_CACHE_REVIEW.md`记录：每次完整读取active，字节与已成功写入缓存完全相同时跳过旧档重新解析；外部变化仍完整校验，durable写入/backup/原子替换不变。v0.5最终GUID `182571f77341471d9fd9acd120b05e26`、源 `967f309` 对应Player02 P95 **16.6913**通过旧场景。它不证明新增v0.7内容仍通过。
4. **v0.7保存字节比较已由LINQ改为完整索引比较。** 4f020分段Editor实测12.3104ms→1.7537ms；保留全字节比较、ReadAllBytes、坏档保护与Flush(true)。4efd源实际前台P95从4f020的18.0941改善至16.8835，但前置流程/源不同，不能把整个差额当单一修复的精确收益。
5. **换SHA provider已经测过，不建议重复。** 4f020 cache中的hash-provider实际诊断：SHA256.Create/显式Managed/CryptoServiceProvider/Cng约10.6–10.75ms，输出相同，没有可取性能收益；不是native provider能白省10ms。未把Windows支持推定为Mac支持。

相关原始分段结果：`artifacts/cache/v07-performance-4f020f3-analysis/phase-diagnostics-result.json`、`save-phase-diagnostics-result.json`、`after-comparison-diagnostic-result.json`、`hash-provider-diagnostics-result.json`。数据是代表性长历史状态的**Editor定位**，非当前Player逐方法耗时；该Editor的GC.GetAllocatedBytesForCurrentThread始终0不可用，不能说分段代码零分配。

## 当前dca：为什么GPU很快，整体仍超标

原始引用：`evidence/v07/player-dca7a26-0234/native-deferred-independent/player-20261102-023622-9ef3d5e7/`。报告SHA `25c0df4089b9154b1c0d3f939513c42172afe7082c2eb1b24c7eb5d3062de206`，CSV SHA `ef357b6abf47fc977710745db172cce6402e5e9cc85ec36d30d03c4e23a1d93d`。本次只读轻量复算CSV，不启动新运行。

- 180.005549秒，10,116原始行/10,115完整区间，原P95 **16.7929 >16.7**，平均17.79454，P99 **56.7556**，最大 **80.0241**；651帧>16.7，362帧>20。失败不是只差0.0929ms而没有明显卡顿。
- GPU均值 **0.317251**、P95 **0.320512**；有效9674/10115=**95.6401%**，441缺值保留。Render Thread均值0.264126/P950.3415。它们不支持GPU填充率是30–80ms尖峰主因；GPU计时不是整帧等待/CPU序列化时间。
- 原生FrameStart/Present/Complete P95分别 **18.7488/18.7009/18.6738**，仍需保留，不能只因LateUpdate接近门槛就认定显示节奏已完全平滑；这些时钟也不是物理scanout。
- Canvas.BuildBatch P95仅 **0.023ms**，但这个marker不包括所有C# UI构建、Session.State深克隆、字符串或保存。Main Thread均值17.792ms也含等待，不能等同17.792ms纯CPU业务。
- +1行对齐已完成帧ProfilerRecorder，interval与后继Main/Alloc相关分别 **r=.990870/.989101**；相同行直接配对会错归因。10,114对仅作诊断，正式判定仍是全部10,115区间。

| 后继完成帧分配 | 区间数 | 均值ms | P95ms | >16.7 | >20 |
|---|---:|---:|---:|---:|---:|
| 0B | 9036 | 16.67095 | **16.6846** | 272 | 0 |
| >0且<1MB | 719 | 16.70095 | 16.6819 | 20 | 3 |
| ≥1MB | 359 | 48.26866 | **67.0979** | 359 | 359 |

这359个大分配工作帧占完整样本约3.55%；加上292个小幅越线区间，把全局95分位推入普通帧的更高尾部。P95对应实际Unity帧11626、elapsed101.337014，本行和后继均0分配，不能说“这0.0929ms就是GC暂停”。分配相关也不证明整个48ms都为GC暂停。

历史增长仍非常明显：≥1MB组前30秒每次平均2.55MB/31.44ms，最后30秒约11.97MB/64.74ms；全程累计分配 **2,858,745,215B**。每秒Roll+Reorder两次真实提交/保存，含预热各190次，与359个采样中重事件相符。207/207/207代际计数不能相加为621次独立GC。

4efd与dca之间16.8835→16.7929并非专门的单因素优化对照，不能精确归因于TMP尾影修复或新资源工具。旧4f020的focused=0 deferred、P9533.5488走后台Sleep路线；当前10,116观测focused，不可拿旧后台数据解释当前前台剩余误差。

## Profiler已证明与尚未证明的部分

只读既有 `evidence/v07/review/RESOURCE_PROFILER_4EFD2B7.md/.json`：原capture为源4efd2b7、GUID3e9ef773caed4efe96fc819ed40c3e5a，15,914帧public event完整提取；其压力区与CSV全部10,097观察对齐，Draw Calls95–97，GPU以外资源数据没有显示这一常规压力区的大量绘制异常。该运行额外启用了Profiler，不能替换正式P95。

现有提取是Texture/Draw/Triangles/GC counter，**没有导出能把当前尖峰精确分给JsonUtility/Hash/Flush/每个Clone的函数级时间树或GC暂停堆栈**。旧Editor分段提示长历史payload JSON约14ms、envelope再约8ms、hash约10ms，AtomicSave约49–54ms；不能把各独立计时相加，也不能直接当当前Player方法耗时。

新dca独立资源路线的结算212稳定/214峰值属于另一个真实制作问题；常规180秒压力没有结算。其最小研究见 `artifacts/cache/v07-drawcall-analysis/README.md`，不能声称修结算材质共享会顺带闭合这里的180秒P95。

## 下一步支持什么、不支持什么

**支持先做一个可测的局部CPU试验：**`GameApp.Art07Presentation.cs:64`已有已提交 `after=Session.State`，成功分支再调用`LogCommand`，后者 `GameApp.cs:369`又完整取一次Session.State，只为日志metadata。显式传已有committed after，保留日志内容、路径、时序与异常处理，可减少一个冗余深克隆。不能拿可能是旧表现状态的ViewState替代。此候选仍未实现/未量到Player收益，旧单clone约1.6ms也不能承诺消除长事务尖峰。

若下一轮进一步定位，优先在独立实际Player诊断给 Execute/clone/payload JSON/envelope/UTF8/hash/旧档验证/durable write/Refresh 加有限、明确阶段标记，保存同样的长历史、命令、存档合同与前后台环境；先找到真实最大成本，再决定是否承担序列化内部优化的兼容风险。高频observer不能额外枚举/落盘，额外Profiler运行不能混作正式180秒验收。若单一局部修复有益，再做同源正式复验。

**当前不支持：**再次单纯打开target60、无新环境变化重复独占/BitBlt、因0.32ms GPU就先减画质、用高targetfps稀释慢帧、改采样点/过滤重帧、盲改pacer睡眠或spin阈值、删历史回执、减操作/存档频次、去掉Flush或把提交先显示后异步落盘。已有byte缓存和索引比较不要重新实现一遍；换SHA provider也没有实测收益依据。

如果以后有新的真实节拍证据（例如完成的60Hz原生模式、明确deadline等待/唤醒开销），再评估pacer，保留四轴、输入/生命周期/功耗边界；现在只凭总体P95略高不能说明主要根因是限帧器。整个判断保持16.7ms/180秒及原工作量，Mac与物理显示延迟仍未由Windows历史证据通过。
