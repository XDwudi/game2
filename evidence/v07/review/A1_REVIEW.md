# A1 独立技术与视觉复核

复核者 `/root/v07_spec_audit`，2026-10-31。范围：四卡 C02/C41/C45/C52 样板、菜单/对局、Presentation 适配器及新 GameApp 集成。只读生产，没有启动 Unity 或修改验收标准。**结论：只读数值适配的托管检查通过；A1 仍需返工和真实完整演出验证，未签署制作质量通过。** 后续22屏、71图标尚未完成的范围不作为本次A1缺陷列报。

基准 HEAD 为 `1fbc3cce9d84237fa98b31d4b73b08e6f5e909ae`，被审代码为未提交工作树。SHA/字节数见 `a1-source-bindings.json`；生产正在并行修改，不能将本结论自动覆盖后续版本。托管测试另外冻结26个C#文件，清单见 `a1-managed/source-snapshot.json`。

## 发现与处置状态

| ID | 优先级 | 发现、依据与影响 | 当前状态 |
| --- | --- | --- | --- |
| A1-01 | P1 | `GameApp.Art07Presentation.cs` 减少动态投掷仍调用 `table.BeginRoll(.08f)`；DiceTable 对任何正时长都会执行完整旋转和位移，因此将900ms动作压缩到80ms，违背 ART17/UI32 的无旋转/短淡入终态要求。中途打开减少动态也需要收尾已在运行的roll。 | 已通知root；绑定时尚未修正，需真实滚动截图/逐帧核查。 |
| A1-02 | P2 | 新 Gameplay/Settlement 原本删除了 Legacy 的上一手明细入口；演出后返回 HandReady，只显示“投掷后选择骰子”，已保存得分/来源从普通UI不可再查。 | root已添加 `PreviousSubmission`，静态复查可见；点击、恢复后入口和实际明细仍待引擎验证。 |
| A1-03 | P2 | 原 `TickPresentation07` 以 `Time.unscaledTime-beatStarted07` 推进数字/粒子，暂停只停 PresentationClock；在最后分数阶段暂停后恢复会跳到运动终点。 | root已改用 `Clock.ElapsedMilliseconds - beat.StartMs`，静态正确；真实暂停前后连续帧待验。 |
| A1-04 | P2 | Awake读取reduced/质量/颗粒等偏好后，没有在初始化完成时调用 `ApplyArtPreferences07`；当前调用只有设置点击回调，重启后的材质、阴影及UiKit状态可能不对应已保存设置。 | 已通知root，需初始化时统一应用及重启场景验证。 |
| A1-05 | 风险，待实机确认 | `PresentationEffects` 根位于 main 之后，ScorePanel 的关键文字仍在main之下；最终36颗Spark在(1560,366)周围生成，覆盖区域和分数末行接近。此层级没有实现规格 UICritical 高于UIEffects的保护。 | 需要真实G147主爆发截图/逐帧确认；若交叠，应给关键数字独立上层或严格排除区域，不能只凭raycast=false认定不遮挡。 |
| A1-06 | 已知视觉返工 | golden-sample03中已锁标签跨到底边，玻璃标签换行；附魔当前退回文本块，C02技能摘要截断到无法直读倍率。 | root已知，报告保留真实观察；未重新把这些重复列作新高优缺陷。 |

## 已完成的技术检查

静态读取了 `PresentationModels`、`PresentationReceiptAdapter`、`PresentationSequence`、`PresentationEffectBudget`、`AudioCueMap`、音频播放器、GameApp新表现partial、UiKit及相关核心解释器。适配器不持有GameSession或RNG，不调用Execute/Evaluate，只有读取冻结快照、精确数学、克隆和只读DTO构建。GameApp依然先执行并保存规则，再进入表现；重复结果不重播，核心层保存失败返回不accepted，UI进入重试流程。

N 归属覆盖了有效值变换 C03/C12、分组 C24/C25、缩短顺子 C29、逐骰贡献、聚合基础加分、整手倍率及自身计分附魔。适配器将同一emitter去重，区分C41发起者与动态owner；纯后置不计入N，C53=1/C45无匹配不凭trigger列表虚增，×0/×0.5仍计入。此次没有发现新的归属或精度错误。聚合基础分按冻结同技能来源等分，并检查回加总数；若解释不一致，回退最终只读结果，不重新计分。

总倍率使用BigInteger分子/分母，阈值用交叉乘法；分数最终取整仍属于既有规则。`ScorePresentationTier` 的整数判断及零分强制L0没有浮点精度路径。DTO数组克隆为只读集合，后置面数据另行克隆，不把演出改面写回Run。

独立执行证据：

- `a1-managed/result-20261031-170712.json`：冻结源后重跑现有 **199/199** Presentation检查通过。
- `a1-managed/result-20261031-170855.json`：加入独立测试后 **200/200** 通过。新增测试先运行真实GameSession的NewRun→StartStage→Roll→ConfirmLocks→Submit，再对同收据执行 Normal/Fast/Instant×减少动态开关的6种组合，每种200次回放/暂停/跳过/恢复；最终完整before、after、会话状态、原收据和MemorySaveStore序列化字节均未改变。
- 测试源 `a1-managed/IndependentReadOnlyTests.cs`，执行为 `.NET 10.0.403 / NUnit reflection`。Unity API使用该预检原有JsonUtility桩；**不是Unity、实际渲染、音频听感或真实对象池200次验收**。框架池单元测试不能替代GameApp实际池。
- 生产规则与资源配置的原样检查见 input/source-audit.json；本次没有改变它们，也没有使用新增测试修改生产实现。

## 实际图像观察及边界

已直接查看 `DicePrototype/Assets/Temp/v07/menu-first.png`、`battle-first.png`、`golden-sample03.png`。前三者分别是历史首样菜单/对局与最新四卡材料样板；不能把旧图未修正状态视为最新代码失败。最新图已经比前图改善骰体朝向与投影阴影，卡片奶油纸底、青绿背景、红蓝点数与粗轮廓有统一关系。物理面上的颜色仍可与金边、玻璃、临时和锁定标签区分，但标签位置和附魔图标需完成上述返工。

`golden-sample03.json` 的fixture是 **GoldenSample**，预览1380分、5骰/4卡/5道具/K5、已锁3颗，expectedValuesMatched=true。它**不是G147，也没有证明已执行Submit或完整时间轴**。输入G147的967680分/26880倍/N4/L5已在托管层验证，但实际最高演出、零倍率、后置染色与最终显示尚需现场证据。

复核结束时root补充：已通过实时工具观察G147得到967680分、N4、L5和1840ms。此项属于root提供的实时观察，尚缺可供本审查直接复核的对应截图文件与完整证据绑定，故保留为“已有工具观察、未完成独立实机验收”，不写成没有执行，也不将其升为A1完整通过。

不能根据这三张静态图认定投掷落位最后120ms、暂停/跳过、射线隔离、输入门禁、C41动态目标高亮、全阶段3.2秒预算、真实对象/材质稳定或双平台压力通过。当前无完整60fps视频、Windows Player或Mac实际证据。

## A1复核的下一组必要证据

1. 普通手和G147经实际UI提交，保存前后状态/收据、各阶段截图与连续帧；G147必须967680、26880、N4、L5且目标1800000时不达标。
2. 同初态/同指令在六种速度/减少动态组合下，状态哈希和三个RNG一致；减少动态无压缩旋转，Instant直接终态。
3. 最终分数阶段暂停后恢复、跳过、失焦和返回后重播，只读明细入口能访问正确历史收据；不得停留旧手数据或重发副作用。
4. 修正标签和附魔图标后的实际同镜头截图；一次最高爆发确认分数、骰点和按钮没有被效果盖住。

这些是A1已有目标的验证，不新增范围或降低原规格标准。完整ART/UI/FX封版清单继续按67项原登记执行。
