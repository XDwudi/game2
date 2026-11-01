# v0.7 表现收尾复核与缓存修复交接

日期：2026-11-01。输入：`docs/specs/v07/ART_SPEC.md` / art0.7.0；玩法 p0.6.0、schema4。
复核者：v07_presentation。本人此前参与本模块实现，本记录是实施者自查及定向缺陷复核，**不代替独立美术验收**。

本轮只读生产源，未启动 Unity，未写 Assets/Editor。修复候选全部留在 `artifacts/cache/fx-final-fix/`。根协调者负责串行集成、真实 PlayMode、截图/录像与独立复核。

## 已核对的真实证据

- 读取 `evidence/v07/unity/nunit-20261101-020116.xml`：交易专项 **6/6 Passed、0 skipped、6.9547409s**。Unity fake-null 修复已存在，不再列为问题。
- 实际查看 `artifacts/cache/v07-recording-normal-03/frames/frame_000110.png` 和 `frame_000145.png`。110 分数完整、字体及层级已修；145 计分区被空预览替换。110 的金色亮环穿过同色数字，底部焰带像一堆三角形，散屑因非等比父缩放近似细线。这些是本轮实图发现；不重复先前 G147 裁切、缺字或 CanvasGroup 问题。
- normal-03 元数据为 Unity 6000.6.7f1 / WindowsEditor / D3D11 / RTX3080；Runtime MVID `5946399c-f9e2-4250-9ddf-f22d0daf6b6f`。捕获状态 `captured_pending_visual_review`，不是最终美术通过。
- 候选独立 `dotnet build artifacts/cache/fx-final-fix/FinalCompile.csproj --nologo --verbosity quiet`：**0 error、0 warning**。包含实际 Runtime/UI、原 Gameplay/FX/TX tests 与新增六项 final tests；这是编译检查，**新增六项尚未执行**。

## 具体缺陷

行号绑定修复前源。缓存 `manifest.json` 包含逐文件原始与候选 SHA256，避免根协调者覆盖同期修改。

### FFG01 / P1：Instant、跳过及正常结束立即抹去最终得分

- `Runtime/Presentation/PresentationSequence.cs:104,134`：Instant 直接 Finish，不派发 BeatStarted；FinalStateRequested 有事件但 GameApp 未订阅。
- `UI/GameApp.Art07Presentation.cs:39`：Completed 清空所有效果并 Refresh。
- `UI/GameApp.Art07.cs:299–301`：不在播放就只画下一手预览；新一手没有锁骰时成为空态。
- 真实 normal-03/145 复现；Instant 连单帧最终计分区也没有。来源勾和后置摘要同时消失，只有侧栏旧得分明细可查。
- 对应 S07/S08、20/21章、FX08/09/10。**缓存已修，待根协调者真实验证。**

### FFG02 / P2：最终分数与金环局部同色叠压，方屑被压扁

- `UI/GameApp.Art07Effects.cs:161–170` 把原192×192 prefab 非等比放大为600×160，再放到分数背后。
- `Runtime/Presentation/Art07PrefabView.cs:198–205` 的粒子距离/初始形状仍在原正方形空间内，继承父变形。
- 真实 normal-03/110：数字虽然位于上层，亮边仍穿过金色笔画；短焰像三角堆，散屑变细线。
- 缓存用现有金色与现有短焰/散屑纹理，改为面板边缘的阶梯细线、底边短焰带、等比方屑，数字中央留透明空间。没有重做源资产；需要重新实录确认观感。对应21章、FX10。**缓存已修，待实图复核。**
- 层级没有删减：规格740行 L0 保留下划线；741行 L1保留4火花和数字1.04；742行 L2一圈细金边及8方屑；743行 L3单边环＋横向扫光和16方屑；744行 L4双层边环和24方屑；745行 L5双层阶梯边环＋短焰带和36方屑。数字比例仍由原 ScorePresentationTier 控制。边环在自身阶段内单调向外扩3参考像素一次，保持面板边缘范围；Reduced完全固定。变化是将原圆形发光贴图在数字后面的非等比椭圆，替换为透明中心、围绕整个计分区域的边环；规格没有要求L2/L4必须是圆形。Reduced保留对应线粗与静态星数，不展示飞行粒子。是否达到艺术强度仍必须实图验收。

### FFG03 / P2：暂停交易中开启减少动态，返回后仍继续翻面

- `UI/GameApp.Art07Presentation.cs:50–51` 原本只结束计分播放器或 `visual` 协程；新交易不使用该协程。
- `Runtime/Presentation/Art07TransactionMotion.cs:66–68,97–98` 在 Begin 捕获 reduced，继续用旧值驱动旋转/位移/缩放。
- 场景：Normal 刷新→暂停→设置减少动态为开→返回。交易的旧运动没有立即取消。
- 缓存 ApplyArtPreferences 对所有当前 Busy 表现调用 SkipVisual，保留新设置，直接落到已提交最终UI；不再发命令。对应S16/S17/20、FX08/09。**缓存已修，新增真实回归待运行。**

### FFG04 / P2：失焦仅取消计分，投掷和交易继续运行

- `UI/GameApp.Controllers.cs:129` 原来检查 `IsPresenting07`，未包括 Roll 的 `visual` 和 TransactionMotion。
- `Runtime/GameApp.cs:363` 的 OnApplicationPause 已覆盖 Busy，不能据此推定桌面失焦也覆盖。
- 缓存失焦改为 `if(IsBusy)SkipVisual()`；拖拽取消保持，纯表现取消不追加收据。对应23章、FX08。
- **缓存已修，新增真实回归待运行。** 测试是实际组件生命周期回调，不冒充 OS 前台切换。

### FFG05 / P2：永久改造先瞬间写到显示，通用擦拭不区分零变化

- `UI/GameApp.Art07Effects.cs:181–203`：PostScore 开始即 `table.Display` 最终永久数据；随后全部目标（包括 before==after）播放 `fx_face_change`。
- 不会篡改规则，也正确使用冻结 after，但普通 C36/C37 的旧点阵没有淡出/新点阵没有淡入；已经蓝色的 C38 仍出现完整擦拭，不是规格要求的细确认。多个净抵消的事件摘要只有“永久改点 n 面”，需开完整记录才见真实变化0。
- `UI/GameApp.Art07Effects.cs:550–555`：S13 染色/玻璃数值确实跨60–300ms插值；纹理、黄金、点阵则在180ms切换整份显示，没有对应材质交叉淡入。交易前后文字与永久数据正确；6项交易测试验证的是最终值、预算、门禁和状态，不证明这些连续过渡完成。
- 对应12/13/17/20章、FX05/10。**仍待修；本轮缓存未扩到永久面动画。** 建议独立只读面显示层按目标 faceId 从冻结 before 到 after 过渡；真实净变化0只出确认线；先合并每物理面净结果，阶段总长仍≤480ms。

### FFG06 / P2：Fast 的共享随机盘仍在约100ms内转整圈

- `Runtime/Presentation/PresentationSequence.cs:61–66` 将 ControllerAndEnchant 合成100ms。
- `UI/GameApp.Art07Effects.cs:143–148` Fast 与 Normal 都创建 Reveal prefab。
- `Runtime/Presentation/Art07PrefabView.cs:197` 只按 ReducedMotion 关闭旋转，没有 Fast 判断；结果是 Fast 整圈旋转压到100ms，不符合12章“Fast仅结果淡入80ms”。
- 共享 owner 归组、q值来源与各 emitter 记录当前正确，不应再改随机或拆成多个盘。**仍待修**：仅改 Fast 的 view motion/淡入，不重算 q；验证 C41+C53 q=0.5 一次揭示、两来源合计0.25。

### FFG07 / P2：达标章没有规定的480ms落章→缩小标收尾

- `UI/GameApp.Art07Presentation.cs:149–153` 只创建静态 Frame。
- `Runtime/Presentation/PresentationSequence.cs:26` 的 `QualifiedNonblockingTailMs=200` 没有消费者。
- 目标状态最多280ms后 Refresh 清除，稳定HUD另有小标，条件判断正确但缺实际落章/缩小过渡。对应S08/21章、FX06/10。**仍待修**，不得增加3.2秒门禁；后200ms应非阻塞且可取消。

### FFG08 / P2：超过两颗破碎骰缺其余短轮廓退场

- `UI/GameApp.Art07Effects.cs:271–276` 仅 `.Take(2)` 创建碎片；其余只有总数摘要，没有逐个真实消失位置的短轮廓。
- 规则删除正确，2×6碎片预算正确；第10章要求其余使用短轮廓淡出，不能为了预算直接丢失目标反馈。**仍待修**，可使用已捕获 Pose 对余项做无碎片轮廓，不超过1主3辅助预算。

## 复核中保持正确的边界

- 计分适配器只读成功收据、冻结前后状态；当前永久后置读取 `lastPresentationBefore07/After07`，不读取随后修改的 Session 来伪造旧回放。
- C53 当前按 StateOwner 分组，多个真实owner用完整静态列表；C41共享同owner q，不二次随机。复核未发现已修复的共享来源再次分裂。
- 精确倍率判档使用整数有理数；0分强制L0，连锁N来自计分emitter去重；这些与稳定UI修复相互独立。
- Normal/Fast播放器、事务motion的暂停与跳过不会再次执行规则或保存；本轮修的是未完全接入的生命周期入口与最终可见状态。
- 现有 `PLAY_ART07_RealCommittedReceiptThreeSpeedsAndReducedMotionPreserveStateAndSaveBytes` 在真实收据后实例化独立纯播放器，**不是**四种模式的完整GameApp渲染验证。新增六项中的第一项补真实renderer自然结束/Instant同步结束路径。

## 五种录像模式能与不能证明什么

`Art07VisualRecorder.cs:16,126–177` 当前五模式：G147 Normal/Fast/Reduced、G147连续同收据Replay、C02/C41/C45/C52 Normal样板。固定60Hz PNG是真实合成画面，状态/存档SHA和MVID能绑定收据与运行；不是实时帧率结果，也不包含声音。

这五种模式足以审查当前 G147 基准、最高档连续视觉叠加、部分静态/复制/换色样板；**不足以单独覆盖 FX01–13 或 UI交易验收**：

| 缺口 | 追加最小场景/证据 |
| --- | --- |
| Instant没有录制模式；Recorder还要求Playing=true且duration>0 | Instant成功Submit前后各稳定帧＋完整renderer断言，允许0ms同步完成；不是把其伪装成动画模式 |
| 无C53共享半倍率与多owner | SharedHalf，q=0.5；两个真实owner，Fast静态揭示和Reduced静态表现 |
| G147不含永久改面/自毁/基金后置组合 | C07/C26/C36/C37/C15/C38最大后置合并、零净变化、C42两个独立风险；正常与Reduced短片 |
| 未包含0分及等级边界的实际视觉 | ZeroMultiplier，L0无庆祝、保留收入/成长；精确边界仍以整数测试为准 |
| 三速录像不覆盖中途暂停/失焦/跳过/改设置 | 每种事务至少一条实际回调/操作证据，验证门禁释放、对象复位、最终值与存档一致 |
| 不含S11/S13 | 刷新100+180ms、采购160+180ms、O05/O06新增位、U07与U05物理面/材质过渡短片 |
| 单一窗口/质量 | 720p/150%、1080p/100%、1440p、16:10和超宽；低质量和满10骰/K8/10卡/5道具/6+5报价 |
| 连续模式重复同receipt | 可以审视觉闪烁与复用；不能替代多个新真实提交的永久变化、随机推进与音频重新触发 |
| PNG无声音、固定步长不是性能 | 独立音频录制/听感和真实Player性能；Mac同源Player实测 |

采样亮度/红蓝翻转比例只是定位可疑帧，当前工具没有实现完整闪烁阈值检验，不能报告“安全通过”。

## 缓存候选与集成边界

目录：`artifacts/cache/fx-final-fix/`。`candidate.diff` 给逐行补丁，`manifest.json` 给原始与候选SHA；原始SHA不匹配时必须按diff合并，不直接覆盖。

可按清单SHA整文件复制：

1. `GameApp.Art07Presentation.cs` → `Assets/Game/UI/`。
2. `GameApp.Art07Effects.cs` → `Assets/Game/UI/`，只有Combo根布局与Configure调用变化；包含已有交易fake-null正确版本所依赖的调用方式。
3. `GameApp.Controllers.cs` → `Assets/Game/UI/`，仅失焦门禁变化。
4. `Art07PrefabView.cs` → `Assets/Game/Runtime/Presentation/`。
5. 新 `GameApp.Art07StableResult.cs` → `Assets/Game/UI/`。
6. 新 `Art07ScoreBurstLayout.cs` → `Assets/Game/Runtime/Presentation/`。
7. 新 `Art07FinalResultPlayModeTests.cs` → `Assets/Game/Tests/PlayMode/`。

**`GameApp.Art07.cs` 不整文件覆盖。** 仅在 ScorePanel 的 `if(IsPresenting07){DrawReceiptScore07(p,w,h);return;}` 后增加 `if(DrawStablePresentationScore07(p,w,h))return;`，避免盖掉root/商店代理的同期终局/稀有度工作。

稳定结果持续时间不是新玩法：规格没有固定秒数。本轮根协调者明确同意“直到下一次成功真实投掷（含遥控）、成功锁定形成新预览、新提交或新Run/ApplyDebugScene替换”。失败请求、重绘、暂停/关闭只读记录不清除；Normal自然完成、Fast、Reduced、Instant和Skip都使用同一收据。静态来源勾独立于活动FX池，原有“Skip后活动FX=0”断言不降低。

恢复旧存档仍按既有最终RunState重建，不补演出；本轮新静态收据面板在内存内持有，未增加存档字段或改schema。跨进程重开最后提交时，完整历史入口仍由既有 lastSubmission 提供；不声称该静态面板已经跨进程重建。

新增真实测试前缀 **PLAY_ART07_FINAL_**，共六项：四模式renderer保留结果、零分Skip后置明细、失败命令保留/成功Roll清除、失焦Roll与TX收束、暂停TX开启Reduced收束、Combo等比方屑与中心无圆形填充。需root运行；并回归原7FX/6TX及200次完整renderer复用。受影响Normal/Fast/Reduced/G147样板应重录；新layout的艺术质量不由编译或几何断言签字。

紧凑模式新“完整记录”按钮为 y58/h60（720p实际40px），不是旧h40；左侧正文宽度保留独立区域，不穿过按钮。

当前结论：**有缺陷 / REWORK**。FFG01–04已有缓存候选；FFG05–08仍未修。双平台、声音、性能、人工艺术判断不在本次只读复核内冒充通过。

## FFG05–08 后续缓存候选（2026-11-01）

此段优先于前文“仍未修”的实施状态，**不改变前文真实发现和未通过结论**。root已串行集成 FFG01–04，并报告新增FINAL六项真实通过（NUnit022053）；本代理未启动Unity。新的FFG05–08候选位于 `artifacts/cache/fx-post-fix/`，仍未写入Assets。

### 具体实现

- **FFG05**：新增 `Art07FaceTransition`。旧/新面均引用 DiceTable 根据冻结永久数据生成的真实mesh；只在过渡期间使用一份共享透明材质，常态 `VertexFace`、faceId、点色和游戏状态不变。旧点阵只缩中央点位至0.9，黄金边/纹理位置保持；普通点阵320ms、纯染色220ms后永久章收尾至360ms、六面复制/玻璃420ms，均在480ms后置阶段内并行。Fast140ms、Reduced80ms交叉淡入且无缩放/擦拭运动。每面合并最终净变化；0变化只显示细确认线与真实“净变化0”。纯永久来源仍亮边，但不加入连锁N。
- **自动关末清理**：若目标临时骰已从最终RunState删除，以成功收据的原始effects建立显示副本（复制/六面染色/实际点数newValue/面染色）；存活after骰再次覆盖为权威最终数据。这只是显示恢复，未调用规则Evaluate或RNG。冻结副本随旧收据回放，不读后续调试修改。
- **S13**：纹理、黄金、点阵也使用真实旧/新物理面显示层，60–300ms重叠窗口内过渡，不再在180ms硬切。玻璃仍使用已提交old/new属性插值。取消/Skip/完成恢复原Renderer与MaterialPropertyBlock，数据层从来只保存正式结果。
- **FFG06**：Reveal新增“仅结果淡入”开关；Fast/Reduced保持收据q对应固定角度、80ms淡入，不再把整圈旋转压成100ms。共享owner和q的事件映射不改，两个真实owner的静态列表仍保留。
- **FFG07**：新增 `Art07QualifiedStamp`，Normal480ms：前280ms沿用目标阶段，后200ms独立收缩成小标且不持有Busy。Fast/Reduced短静态反馈；暂停冻结、Skip/失焦直接落小标，换场景或不再属于真实达标状态时隐藏。新提交qualifiedBefore为true时不再启动。尾段使用root新增的 `Art07VisualRecorder.NonblockingDeltaTime`，不计入阻塞cycleTicks；Instant不强制tick。
- **FFG08**：前两颗维持2×6碎片；其余按已保存世界Pose投影出原位置细轮廓与消失符号，约320ms局部淡出；不画不存在的点数、不额外生成碎片。低质量/Reduced使用静态轮廓路线，瞬时投掷按既有同步终态合同收束。

### 新文件与最小合并

`fx-post-fix/manifest.json` 包含每个原始文件与候选SHA；`candidate.diff` 为精确补丁。

修改候选：`GameApp.Art07Effects.cs`、`GameApp.Art07Presentation.cs`、`GameApp.Art07StableResult.cs`、`Art07PrefabView.cs`、`Art07FxIntegrationPlayModeTests.cs`。新候选：`GameApp.Art07PostEffects.cs`、`Art07FaceTransition.cs`、`Art07QualifiedStamp.cs`、`Art07PostEffectsPlayModeTests.cs`、`Art07FaceTransition.shader`。

Shader必须放在 **`DicePrototype/Assets/Game/Resources/Art07FaceTransition.shader`**，由Resources引用确保Player包含，不能只依靠编辑器Shader.Find。它只作用于过渡显示层，现有骰子Shader未修改；需root真实Unity导入/编译以及目标GPU验证。

**不改 Fixtures、VisualRecorder、Editor 或场景。** 编译依赖root已加入production的NonblockingDeltaTime接口；root给出的两个新计数字段继续由root维护。StableResult候选保留root此前非compact详情按钮 y84/h60 修复。

### 验证入口与限制

独立完整C#编译命令：`dotnet build artifacts/cache/fx-post-fix/PostCompile.csproj --nologo --verbosity quiet`。已执行，**0警告/0错误**；shader和真实renderer未由本代理运行，不能据此写通过。

新增真实GameApp测试前缀 **`PLAY_ART07_POST_`，共八项**：

1. C36+C37净0及已蓝C38：细确认、无擦拭、mesh不伪变。
2. 永久点阵：实际GPU采样旧中心点、中间淡化、最终四点；恢复原不透明材质。
3. C07目标同提交到期删除：冻结六面最终状态仍演出，三次只读回放池不增长，完整状态/存档字节保持。
4. Fast C41+C53半倍率：一次owner、固定角度、单次结果淡入。
5. 首次达标480ms：门禁结束时尾段仍存在，暂停冻结、Skip收束，再次真实提交不重盖。
6. 四颗破碎：两次共12片，其余两个真实ID的原位置轮廓；Skip彻底复位。
7. U01/U06：60–300ms内真实双层材质过渡，最终原Renderer/材质恢复，状态/存档一致。
8. 播放中的物理目标先销毁、池禁用、池内闲置子对象销毁及整个宿主销毁：不访问Unity失效引用，原存活面Renderer恢复，坏pair丢弃且后续可复用。

原 `PLAY_ART07_FX_PostScoreThreeTargetsRiskAndFrozenReplayPhysicalPips` 保留≥3目标、风险、Pips4与冻结回放的原断言，改为先确认可见旧Pips3过渡层，过渡完成后检查Pips4；未删除数据断言或降低目标阈值。

以上仍须root实际运行，并复核普通/Reduced最大后置、Fast共享半倍率、Qualified尾段及S13连续录像。GPU测试是实际引擎的局部物理面渲染，不冒充全UI截图；最终艺术观感与多平台结论仍未签署。候选状态 **VERIFYING（缓存交接）**，原始失败和旧证据均保留。


### 本轮冻结记录

十个候选与五个生产基线SHA已逐项匹配；完整C#加全部PlayMode源的独立编译再次通过0警告/0错误，证据为 `artifacts/cache/fx-post-fix/compile-evidence.json`（72个C#输入哈希）。初次因同期新增LegacyClick07测试辅助依赖未纳入的编译失败保留为 `compile-evidence-missing-helper.json`；后续补入依赖，未修改测试标准。新增生命周期修正按root复核意见对所有借用的Transform/Renderer使用Unity失效引用检查，坏pair不再入可复用栈。八项新的真实Unity用例、shader编译和连续实机画面仍为未运行，本编译结果不替代这些验证。

## 025155 实机失败返工（缓存 fx-post-rework）

root 已集成前一候选并真实执行23项，17通过6失败，原始报告保留为 `evidence/v07/unity/nunit-20261101-025155.xml`。本段不把上次独立编译当作本轮Unity通过。新候选只包含 `Art07FaceTransition.cs`、`Art07PostEffectsPlayModeTests.cs`、`Art07FxIntegrationPlayModeTests.cs`，见 `artifacts/cache/fx-post-rework/manifest.json` 与 `candidate.diff`；其余文件不整包覆盖。

1. **真实生命周期错误**：在池宿主的 OnDisable 中把子对象 SetParent 回正在停用的宿主，Unity拒绝该操作。候选改为隐藏双层对象、恢复物理Renderer、清空mesh引用；停用时留在原父物体下，下一次合法Add再重新挂载。OnDestroy显式销毁借用对象，防止它们遗留在仍存活的物理面下。生命周期测试增加宿主单独销毁后物理面无孤儿检查，未添加LogAssert.Expect或忽略日志。
2. **净零场景错误**：五骰满五栏不触发C36/C37，旧All()在空集合上无效通过。改为capacity6，并先断言确有两条 `sourceId=C36+C37` 的oldValue==newValue收据，然后保留三个薄线目标/无改造wipe/无mesh伪变断言。
3. **GPU场景错误**：同样遗漏capacity6，根本没有PostScore阶段，并非已证实的shader冷启动问题。改为capacity6并先断言两条真实永久3→4；原GPU旧/中/新像素差阈值、最终不透明面恢复均保持。
4. **复制场景错误**：C07要求首手提交恰两颗且有效点不同；旧五颗场景未触发。改为首提交锁两颗、capacity3、有效1/6、源物理F06=3，第二颗真实临时骰。先断言DieCopied、目标3→4与TemporaryDieExpired都发生，再验证消失目标的冻结显示与重复回放。
5. **三个目标场景错误**：原R-B-R-B-B中C15只净改第二颗、C38末骰已经蓝，只有两个真实改变目标。改为R-B-R-R-R，实际变化恰首、第二、末三颗；先从正式after验证三个distinct目标，再保留三个改造效果、C42风险、Pips4和冻结回放原断言。无变化不会被硬播为变化。
6. **首项Combo未采到FinalScore尚待诊断**：真实首项耗时3.157秒，可能冷帧跳过，但当前没有足够证据归因。WaitFxPhase新增只读实际PresentationPlayer时钟/阶段/帧间隔/焦点/暂停记录，读取表现实例、不写任何规则或时钟；保留6秒观察上限与所有几何断言。下一次引擎执行将区分不存在阶段、elapsed大步越过、或未到终点的外部Skip。不把尚未验证的猜测写成修复成功。

修正后的四组场景已使用实际Core源码、公开CreateDebugWorkspace/ApplyDebugScene/Submit执行 **4/4**，证据 `fixture-audit.json`。它只验证规则触发合同，不能代替真实UI/Shader/GPU。新C#完整编译0错误0警告，输入哈希见 `compile-evidence.json`；本代理未启动Unity，候选等待root串行集成与实际复测。root拥有的PermanentPost录像场景仍需同步上述三个净变目标配色；本代理未修改Fixtures或Recorder。

## 后续实测与 Burst 制作返工

root报告030128中全部POST八项与FINALCombo通过，含真实GPU像素与生命周期；FX三目标回放最后的物理4检查失败，因为检查位置没有要求仍处于冻结PostScore显示。单文件候选 `fx-replay-observation` 在观察协程内检查仍在PostScore、IsReplay、过渡结束、原Renderer实际可见及Pips4；还检查被冻结的永久显示副本4，结束后明确恢复新live状态1。未改生产时长、规则、缓存或存档；root随后实际030805 **1/1通过**。这证明本例冻结回放与正常恢复，不代表所有跨阶段重绘场景已经验证。

**Normal04仍有制作缺陷**：实看 `artifacts/cache/v07-recording-normal-04/frames/frame_000095.png`，方屑接近1像素且被金边吞掉，短焰几乎压成底线。源纹理在alpha≥38时，square为64×64中的(19,19)-(45,45)，有效墨26×26；short_flame为256×256中的(20,20)-(236,221)，有效墨216×201。旧4px方屑加0.7缩放只剩1.14px墨；旧238×10火纹实际高7.85px。

新缓存 **`artifacts/cache/fx-burst-legibility/`** 与骰面Shader/回放补丁严格分开，仅两文件：`Art07ScoreBurstLayout.cs`、`Art07FinalResultPlayModeTests.cs`。方屑改为14/16参考像素的等比容器，保留1倍尺寸至淡出，实际源墨5.69/6.5参考像素；按当前真实活跃粒子数均匀分布在卡与计分框之间的两条错列窄带，不与阶梯边线重合。L1改用45度菱形方向；L2–L5保留各自数量与边环，L5仍36上限。短焰改为238×32，位于左中下缘空白，仍只在L5单次动作中出现。未改资产、Shader、核心数值、演出时长或对象池预算。

原FINALCombo中“rect≤7”检查测量的是含大量透明padding的容器，不能证明碎屑可辨。候选将其替换为**真实导入Sprite的GPU alpha读取**：三个局部时刻均验证36粒子、等比、有效墨5–7参考像素及720p至少3实际像素、与实际TMP字形/详情按钮不交叠、独立于金边的窄带；火纹有效墨高24–28参考像素并避开数值和按钮。原圆形填充不得遮数字、32边段对象及Skip复位检查保留。

两个文件C#独立编译0错误0警告，manifest与输入/源纹理哈希齐全。新测试和新画面**尚未由本代理在Unity运行**。尺寸和几何阈值只是制作候选的工程检查，最终应由root重录Normal/Fast/Reduced以及720p图，查看方屑、短焰轮廓、空间和可读性后再判断，不能从源alpha或编译直接宣布艺术通过。


### Atlas / compact gutter / transaction glyph follow-up (cache handoff)

- Preserve actual FINAL 031626 failure (reported ink14.903 versus5–7). `fx-burst-gap` fixes the measurement using checked-in uGUI Image original sprite.rect / DataUtility padding / signed outer UV mapping, and moves unchanged-size/count chips into the single12ref compact10-card gutter. Adds real highest-tier10-controller committed-scene coverage against actual card borders and TMP glyph bounds. Full current C# compile0errors/0warnings (`644517`); actual Unity and new images remain pending. Frozen manifest and handoff under `artifacts/cache/fx-burst-gap`.
- Actual transaction Normal01 frame22 revealed main TMP glyph quads. Installed TMP_Text inherits Graphic.mainTexture white; root TMP renderer relies on its font material atlas, while TMP_SubMeshUI owns an independent sharedMaterial atlas. `fx-tx-atlas` makes that distinction explicit without changing transaction semantics. New real-frame GPU tests compare original/snapshot glyph shapes on actual shop cards and separately forced ASCII/Chinese fallback, in Normal and Reduced. Full C# compile0/0 (`ba5bb8`); GPU execution and re-recorded purchase/refresh media are pending. Frozen manifest and handoff under `artifacts/cache/fx-tx-atlas`.
- No Assets or Editor operations performed by this subagent for these candidates. Root owns integration, real Unity tests, STATE update, and independent art review. No new visual acceptance claimed.


Follow-up cache: actual034631 identified first4 particles as fx_star_spark (.84 alpha), not square (.406). fx-burst-shape now binds the existing square to all dynamic particles, gives L1 gold diamonds/L2+red-blue squares, and sizes against imported sprite padding; independent GPU5–7ref assertions retained, shapes/colors also asserted. Full C#0/0 e9ea0e; actual Unity pending. Separate test-only fx-tx-bounds adds Normal/Reduced cash bounds against actual pre/post glyphs and true unrotated O05/O06 outline geometry. Full C#0/0 3174d0; root production fixes and real tests pending. No Assets/Editor writes or visual acceptance by this subagent.
