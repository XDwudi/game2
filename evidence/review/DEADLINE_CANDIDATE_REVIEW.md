# 60 Hz 候选与真实空闲对照复核

spec_audit，2026-10-29 UTC。只读生产；实际Unity运行由root完成，本审查独立检查源码、NUnit、程序集PDB和原始CSV，并运行纯托管报告检查。

## 源码结论

`Runtime/FramePacer.cs` SHA256 `e663968dec45150f373a7d753dc45da4b8a5bc67a1e5cfb5cbf550f776d0967e`：未发现阻止opt-in技术试验的实质缺陷。

- `:34–74` 使用ceil(freq/60)周期并在实际release重锚；工作、Sleep或spin超期后都不追赶旧epoch。尾段剩余≤1.5ms才spin，粗等待可超时且真实迟到保留。暂停/失焦`:50–53`独立Sleep17ms且不spin，避免试验native cap=-1时后台无限空转；恢复清空旧deadline。
- `:96–130,159–190` 显式安装/停止，仅Windows standalone可启用。插入当前PlayerLoop的PreLateUpdate首节点，Stop只移除自己的节点，保留其他owner后续修改；重复安装不会添加第二份。旧host延迟Destroy时通过`lifetime==this`判断，不停止新实例。Editor路径无安装。
- 源码不读取probe、动作相位或阈值，不更改Unity时间/玩法/存档；caller负责有效限帧设置。当前是技术候选，不能自动推定为通用生产生命周期完整通过。Windows实际失焦/恢复、异常停用及将来默认启用时的fallback仍需运行证据。

`Verification/FramePacingExperiment.cs` SHA256 `de5362b1ad8a4959017224724d4c5bfa22b453c07e84b79d628c6a86e078c723`：`:139–178`按native60→deadline60顺序，独立3秒预热/20秒采样，默认未指定deadline参数的原独占路线保留。先Install成功才解除native cap；`:233–250`正常/受捕获异常后恢复原pacer和cap并确认窗口/桌面刷新率。四个原始时间轴未移除，首次部分间隔留空，报告始终标注非AC-PERF。

这次短试验允许判断有效限帧/提交节奏，不含deadline实际等待与CPU spin开销分解，不能由策略描述中的“≤1.5ms”推定已实测功耗或CPU时间。在生产采用前应结合正式CPU、GC/UI与多轴数据判断成本。

## 新探针并列时钟

`PlayerVerification.cs` SHA256 `d1a236372197964d5cef1a468c4572e0e174f68d98c73b730132d2a5b26887e5` 静态检查：原LateUpdate时点、16.7ms、180秒、95%原指标覆盖、全量操作与失败判定保持。新增struct时钟链复用同一次GetLatest结果，无新增逐帧反射或采集调用。缺失/重复/倒退/漏观察会断链；Present/Complete单轴异常不抹去另一有效轴，恢复先建新基线；原始值与分类全部保留。诊断轴不替换原判定、不新增硬阈值。FTM最新记录可能跳过底层记录的限制有明确说明。

独立执行 `Validation~/bin/Release/net10.0/ProbeCompile.dll`，28项纯托管检查通过，日志 `probe-independent-clocks-managed.log`；不代表native采集通过。真实build08 Runtime PDB已匹配上述当前源。

## Unity实际回归及构建绑定

本目录 `audit_unity07_10.py` 实际运行通过：EditMode07 JSON/XML逐case一致 **339/339**，含14条pacer、11条AtomicSaveStore、52黄金和57解释trace；PlayMode10 **20/20**，含保存失败重试、池化旧事件/目标、商店边界等。无跳过/失败，旧失败文件仍保留；原配置、黄金与上传规格机械比较未改。

`unity07-10-pdb-independent.json`：Editor5程序集/39份项目源checksum全部匹配。`build08-pdb-independent.json`：Player2程序集/22份项目源全部匹配；实际spike Runtime MVID `7941fe85-19c1-4a9d-89b3-9f4d7d93180a`匹配该Player。build08报告为0error/0warning。旧Atomic复用的真实Unity回归待验项因此关闭；pacer的14条大多是FakeClock/tree与Editor惰性路径，不能替代Windows生命周期测试。

## Spike03真实数据独立复算

输入 `evidence/runs/pacing/spike-03-deadline`，buildGUID `f1f02fa662804c3b9409b602e4a96e9f`。`audit_pacing_deadline.py`从原始elapsed/CPU timestamps重算全部间隔和统计，27项一致；输出 `pacing-deadline-independent.json/.log`绑定CSV/report SHA。两模式均完成20秒、全程聚焦、连续Unity帧号、无显式timing gap、无扩容/模式失配；恢复Windowed1920×1080、桌面144.006Hz、vSync0/target60、pacer=false。

| P95，ms | native60 | deadline60 |
| --- | ---: | ---: |
| 原LateUpdate | 17.0036 | 16.6742 |
| FrameStart | 17.0060 | 16.8128 |
| PresentCalled | 17.0289 | 16.8051 |
| FrameComplete | 17.0150 | 16.8281 |

deadline平均周期约16.6713ms，四轴均改善，未通过更高目标吞吐取得较低分位数。原LateUpdate单轴过16.7；其他轴仍有剩余抖动，数值原样保留。**后三轴是诊断，不能事后新增同样的≤16.7硬门槛，也不能当作物理scanout。** 它们支持改善不只来自原回调位置，但空闲结果不覆盖操作计算/序列化、最大场景或持续180秒。

最终结论：规格范围/标准未变；当前候选制作质量足以进入正式同条件压力试验，尚无新增阻塞缺陷；真实空闲运行与回归证据已复核，AC-PERF仍待正式最大场景结果，沿用Player05失败状态而不提前放行。真人体验未运行。

## 后续源变化边界

上述build08/PDB及spike03源绑定在生产接线前完成。root随后将GameApp.Awake接到FramePacer并由core_rules追加native cap所有权；这些新源不属于build08，不能用build08旧通过签署新源，也不能将预期hash差异误报为原spike证据错误。原manifest与原SHA保持不覆盖。

新FramePacer SHA256 `74970d291d1b3cbde7cb4f421da6decedae912a6f8c1f29003712b524a8ecba0`（仅静态复核）：成功安装末尾取得native cap并设0/-1；首次获取保留原值，重复获取不覆盖；Stop在finally中清调度并恢复native cap；意外销毁host先清lifetime再Stop，避免对正销毁host重复Destroy。GameApp先给0/60 fallback再Install，OnDestroy停止。未改变deadline算法/原探针；未发现阻止正式试验的新增实质缺陷。新增cap ownership测试及实际生产路径等待build09/新Unity结果，不能用本报告旧339/20数量提前关闭。
