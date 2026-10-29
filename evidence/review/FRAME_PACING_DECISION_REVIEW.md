# 精确 60 Hz 调度方法独立复核

spec_audit，2026-10-29 UTC。输入：提交 `27e4b34`、正式 Player05/build06、两份 pacing-route-1 技术试验及当前权威规格。只读生产；未启动 Unity、未改变验收阈值或系统显示设置。本报告取代早期研究中“优先再试独占模式”的下一步建议；该模式在本机的实际回退已经有证据，不重复试验。

## 结论

**允许继续可丢弃的通用 60 Hz 调度试验，不接受当前性能。** Player05 全量帧间隔 P95 18.3322 ms 的失败保持。自有等待可以修复软件限帧抖动，但需要真实提交/完成时间佐证，不能仅令原 LateUpdate 回调的采样值好看。最小试验先用相同空闲菜单比较；正式采用前仍需完整 1080p、180 秒、10骰/7槽/10控制器/5道具连续操作，所有原始数据保留。

## 标准来源与不可混淆之处

`docs/specs/P0_v0.4.md:644` 规定 Windows1080p、60 FPS、“约16.7ms帧预算”，以及指定压力场景连续至少3分钟、记录 CPU/GPU/P95/GC/UI重建。`docs/tasks/P0-IMPLEMENTATION.md:37` 记录同一 60FPS 目标。两处均未指定 LateUpdate 时点，也没有直接写出“LateUpdate P95≤16.7”。

严格的 LateUpdate P95≤16.7 ms 来自冻结探针 `Runtime/Verification/README.md:46,49` 和 `PlayerVerification.cs:30,620,689`，属于已经采用的工程验收解释。**这一区分用于说明测量含义，不用于在失败后撤销门槛。** 本轮继续原采样位置、全量分位数和16.7阈值；新增轴只作交叉证据，不挑最好看的轴替代。

精确60Hz周期为16.666666…ms，距离16.7只有约33.3微秒。Windows调度不是硬实时；目标60FPS与“任一执行阶段每帧都在这33微秒容差内”不是同一句话。不能静态保证有限自旋一定满足当前严格工程判据。

## 两份真实 idle 试验独立复算

运行本目录 `audit_pacing_spikes.py`，从原始 elapsed 与三个 CPU timestamp 重新计算每个相邻间隔、nearest-rank分位数、均值与最大值。46项检查全部一致，输出 `pacing-spikes-independent.json`，绑定两份 CSV/report SHA、构建 GUID 和程序集 MVID。两份各1197行/1196间隔，原始时间轴递增、Unity帧号连续、全程聚焦、无观察缓冲扩容；独占切换失败和恢复确认保留。

| 真实试验 | 原 LateUpdate P95 | frameStart P95 | PresentCalled P95 | FrameComplete P95 |
| --- | ---: | ---: | ---: | ---: |
| spike-01 | 17.0061 | 17.0025 | 17.0178 | 17.0175 |
| spike-02-bitblt | 17.0193 | 17.0173 | 17.0377 | 17.0308 |

单位ms。这证明空闲的约17ms抖动同时出现在CPU帧开始、提交调用与GPU完成通知，不能仅归咎于LateUpdate位置。20秒空闲不是正式压力验收；Present调用与GPU完成也不是显示器物理扫描时间。[Unity时间戳定义](https://docs.unity.com/en-us/engine/6000.6/manual/analysis/graphics-performance-profiling/profile-rendering/frame-timing-manager/get-timing-data)

## 建议的候选语义

采用同一单调时钟的整数ticks；周期为 `ceil(frequency/60)`，不会通过截短周期提高目标帧率。每次实际 release 后建立下一 deadline；本帧工作已越过 deadline 时立即放行，并以实际放行时刻重锚。失焦/暂停/长停顿后重建时基，不追赶漏掉的帧，不改变规则调用次数、Time.deltaTime、存档、动画或输入单发语义。

“固定epoch按序加周期”和“迟到后绝不补偿”不能同时成立：当前晚5ms时，下个仍未到的旧deadline只剩11.667ms。只跳过已过期deadline仍会形成短补偿帧。实际release重锚避免此问题，代价是超期后相位漂移、平均速率可能低于60；应准确称60Hz目标/上限节拍，不能宣称硬实时精确平均60。早期研究中避免now+period漂移的建议，在本次明确no-catchup约束下应服从这一取舍。

粗Sleep加固定上限尾段spin是可测试方案；当前提议≤1.5ms尾段应记录实际等待/自旋成本，并防止长卡顿后全帧忙等。不要使用实时线程优先级、全局驱动改动或看过P95后缩短目标周期。Sleep到期仅使线程可运行，不保证立刻获得CPU；单调QPC负责计时，不靠墙钟或计时器分辨率制造精度。[微软Sleep](https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-sleep)、[高精度时间戳](https://learn.microsoft.com/en-us/windows/win32/sysinfo/acquiring-high-resolution-time-stamps)

只允许一个有效限帧所有者。若独立调度接管60，停用引擎软件等待需要显式记录，并在安装失败/停用/异常时恢复target60。普通Runtime代码不得引用验证开关、压力操作相位、采样起止或阈值；同一生产路径将来用于普通游戏与测试。当前仅在技术试验接线，尚未默认用于生产。

## 放置阶段与可信测量

root提出插入 `PreLateUpdate` 首个子节点：在 Update/延迟协程之后、原生动画和LateUpdate相机之前统一放行。这个位置有明确产品理由，可作为首个候选。需从当前PlayerLoop插入一次、保留所有原生系统，记录实际树；不重排/删除渲染、输入或原探针。Unity官方支持自定义插入点，但新树中遗漏的系统不会运行，改动下一完整迭代才生效。[自定义PlayerLoop](https://docs.unity.com/en-us/engine/6000.6/manual/scripting/managing-update-order/player-loop-customizing)、[SetPlayerLoop](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/lowlevel/playerloop/setplayerloop)

不能把“所有Update之后”写成“所有工作之后”。原生动画、其他LateUpdate、Canvas重建、渲染与提交仍可能花费不同时间。等待也会增加已有输入到显示的延迟。另一种在帧开头等待的设计更利于更新使用新输入，但不消除本帧工作量差异；不应为了哪一种让原探针更好看而选择阶段。LateUpdate本来只保证每次Update后调用，不能保证已经Present。[LateUpdate](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/monobehaviour/lateupdate)

用 `l_n = s_n + w_n` 表示本帧开始时刻及到LateUpdate的工作量，则原探针测得 `Δl = Δs + w_n − w_(n−1)`。长/短相邻帧可能是工作量位移；真正Present还有后续渲染尾部。反过来，在原探针之前等待可以平整Δl，却不能证明渲染尾部同样平整。候选需同时保留原LateUpdate和FTM原始frameStart/PresentCalled/FrameComplete轴、重复/缺失分类、CPU计时频率、deadline迟到、实际等待和有效帧数；FTM的延迟数据按自身时间戳归属，不按CSV同一行猜成同帧。若声称物理显示节奏，需额外PresentMon/ETW等独立显示路径证据，不能把上述API名称解释成scanout。

## 继续采用的条件

1. 先冻结候选周期、插入阶段、尾spin上限与恢复策略，空闲技术试验不作AC-PERF判决。记录有效目标60与实际计数，不增加吞吐、不靠catchup压分位数。
2. 正常游戏、Debug、失焦返回、重复安装/移除、暂停、场景生命周期及原0/快速/普通动画回归。安装失败不得遗留无限帧率或删掉其他PlayerLoop系统。
3. 同条件完整压力试验，原LateUpdate门槛和所有数据保留，同时核对三个原生轴、UI/GPU覆盖及CPU实际工作。若仅LateUpdate改善而Present/Complete恶化，方法不成立；若工作本身超时，继续优化事务/序列化/刷新，等待不能消除计算成本。

规格符合性：没有建议删减范围或改标准；当前AC-PERF仍有缺陷。制作质量：候选算法与阶段有合理技术依据，需源码复核及成本/生命周期验证。实际运行：两份旧idle CSV独立复算通过，新的调度器尚未实际运行，不能签署通过。真人体验仍由人类验收。
