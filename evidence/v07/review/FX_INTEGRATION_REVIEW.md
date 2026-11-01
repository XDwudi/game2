# art0.7.0 表现运行集成复核

状态：**有缺陷 / REWORK**。本次审查者为 v07_presentation，审查 root 编写的 GameApp 集成，未修改生产代码、未启动 Unity。底层 receipt/scheduler 曾由本审查者实施，因此这不是全系统独立规则验收。美术风格、听感、闪烁安全、Mac 与实时性能均未代签。

## 输入和真实证据

- 规格：`docs/specs/v07/ART_SPEC.md` art0.7.0，第18、20、21、23、31章；规则 p0.6.0 / schema4。
- 检查：`GameApp.Art07Effects.cs` SHA256 `6b3f513b01956c81f9861399366170fe0f7834475cd8db7f6ee5c7aaf7e53777`；`GameApp.Art07Presentation.cs` `93c2f72694cbca9df366a74a6c5495877dea5ebdae7c6beb4389ac30f7228854`；`Art07PrefabView.cs` `83c3af339b6462bcc5cad207ed1412c6b18e39809b75a8618952dc106317c4cf`。以下行号指这批检查源；后续修复需要重新核对。
- root 真实 WindowsEditor / Unity6000.6.7f1 / D3D11 / RTX3080 录制：`artifacts/cache/v07-recording-normal-01/`。运行程序集 MVID `641f661f-97bc-4f15-adc2-894c9616dea5`。直接查看 frame000060、000105、000156；不是离线概念图。
- 独立读取录制元数据并逐一核对：157 PNG 的 SHA 均与157条 JSONL一致；索引与 Unity frameCount 均连续；1840ms表现实际111次固定1/60步；正常存档SHA未变、Time与三项偏好恢复。它证明本段录制完整性，**不是实时60FPS性能证明**。该录制的 BuildIdentity 仍带旧 v0.6 sourceCommit，不能据此把当前未提交美术源归到旧提交；当前实际MVID与源快照应另绑定。

## 已确认的规则隔离边界

- `PresentCommitted` 消费已保存成功的真实 CommandResult；duplicate直接返回，适配器拒绝pending/noChange/duplicate。不在视觉事件中再次 `Execute`、`Evaluate` 或调用随机数。
- 精确 score/M/N 与最终档位来自不可变 PresentationReceipt；没有用屏幕字符串或float重新判档。G147录制为967680、26880/1、N4、L5，未出现目标1800000的达标章。
- 单个真实C53及其C41复制来源：适配器按stateOwner生成一次random_reveal，scheduler先揭示再播放两个倍率；当前UI也不重抽。下面列出的多owner缺失是显示覆盖问题，不是二次随机。
- FX池预热且有硬上限，Rent失败不无限创建；归还清除文字、回调和实例绑定，主/辅助租约按receipt释放。根本规则与事务没有因FX重放改变。碎片/标签/连接线在已接入的Reserve路径有限制；所有实际资源的峰值仍需运行计数。

## 发现的问题与修复建议

### FXR01 / P1：关键分数文字缺失、放大后越出屏幕

**实际截图确认。** frame000060 的右上“计算中”不存在；frame000105 的967,680末尾超出1920屏幕。`Art07Presentation.cs:187–189,204` 给所有文本（包括中文状态）换上数字字体，并对top-left pivot的整个630px文本Rect缩放。正常右边界1866，1.12倍时扩大到1941.6，超过屏幕；这不是单纯字重或审美问题。

修复：非最终中文状态使用已验证的中文字体；最终数字在预留容器中采用固定右侧/中心锚的独立文字视觉缩放，保证1.12峰值仍完全在容器内。不要只依赖缩写更短来掩盖。补普通/大整数、1280×720、150%文字和L5峰值截图。数字字体中文fallback是否正确仍需检查真实字体资产，截图不能单独确定字体资产根因。

### FXR02 / P1：最高档效果被计分面板背景遮掉

**实际截图与层级共同确认。** `Art07Effects.cs:43,117` 把combo放在整个main后方的libraryUnder。ScorePanel是不透明Frame/Fill，位于y288–448；爆发范围x1670–1890、y210–430。大部分光环、下划线、焰带和粒子在面板背后被遮，frame000105只剩面板上沿的一截金环。

修复：将计分区分成背景、装饰、关键文字三个层；FX置于背景之上、文字之下，不使用“整个main背后”替代“文字之下”。在实际最高档帧验证完整形状与数字保护区域。

### FXR03 / P1：倍率票据同时出现加号和乘号

**实际frame000060确认“＋ ×4”。** `Art07Effects.cs:73–75` 只绑定数值字符串，`Art07AssetLibrary.cs:300` 的fx_value_ticket永远使用默认plus图标。它误导玩家把整手倍率理解为加分。

修复：按已冻结event.Operation绑定加号、乘号或无运算图标；同一处只表达一次运算。依据事件family区分永久/临时标记。检查×0、×0.5、×2附魔、基础+10和负向变化，不能从最终分数大小猜操作。

### FXR04 / P1：后置永久面展示混用了当前Session与旧投掷覆盖

`Art07Effects.cs:105–107` 在PostScore直接读取 **现在的** `Session.State.dice`，而只读回放可以发生在之后已滚动、使用道具或改过调试骰面的时刻。这会把之后的骰面伪装成旧收据的结果。即使刚提交立即播放，它又将旧 `presentationBefore07.hand.currentResults` 传给table.Display；`DiceTable.cs:179` 会在朝上物理面继续采用旧currentPip/currentColor，因而永久修改到当前面时，真实新点色不会按注释所说显示。

修复：保存与该收据一同冻结的after快照，或以before克隆+收据最终面数据构造纯显示对象；禁止回放临时读取后来Session状态。永久面过渡使用明确的物理面展示，并保留“本手当前/有效覆盖”的区别。补“当前面永久改点/改色”“提交后再改骰面然后回放旧记录”“C38最后覆蓝”的测试，状态/SHA必须不变。

### FXR05 / P1：真实玻璃投掷破碎没有接到破碎表现

`Art07Presentation.cs:73–78` 的Roll（含遥控投掷）路径只刷新after、BeginRoll、播放roll音，没有消费receipt.effects。`BreakDie07` 唯一调用位于PostScore，然而真实 `DieBroken` 在 `Core/Session/GameSession.cs:280–284` 的Roll中产生。结果是骰子从界面直接消失；当前破碎prefab并不会被这条真实事件驱动，也没有相应的破碎声音/目标提示。

修复：从成功Roll收据读取破碎名单，保留before中对应可见骰体的位置，在实际抽面/正常掷骰展示之前播放局部破碎；没有抽到面结果的破碎骰不应进入翻滚。不得再调用Glass随机判定。覆盖普通、Fast、减少动态和遥控命中玻璃分支。

### FXR06 / P1：Fast和非整手倍率来源缺少来源命中提示

`Art07Effects.cs:69` 只在beat.FocusEmitterId非空时命中卡/拉来源线。Fast由scheduler将该phase合并，FocusEmitterId为空，因此所有控制器来源提示都消失；Normal的逐骰基础/全局基础来源（如C27/C44/C48/C50）也没有FocusEmitterId，且BaseAndStructure没有其他源提示分支。多于前四个倍率来源的aggregate同样没有分组亮框。显示“前三个文本”不等于完整聚合提示。

修复：从当前beat.Events的真实计分emitter集合生成有界静态来源勾/分组亮框，缺少独立焦点仍保留归属；不把TriggeredSourceIds全部当计分来源。维护每receipt已完整抬卡集合，基础阶段抬过的卡以后只亮角标；10/32卡用明确“其余n来源”与完整明细。补Fast多来源、基础-only、前四之外的命中显示测试。

### FXR07 / P2：回放忽略当前低画质，重新出现庆祝粒子

`Art07Presentation.cs:93` 构建Replay options未传quality，回到默认Standard；真实首次Submit在68行传了当前quality。`Art07Effects.cs:118` 直接按这些options生成最高档粒子，所以Low档第一次无粒子、同收据Replay又有36粒子。

修复：Submit与Replay统一生成PresentationOptions；低画质继续关闭粒子，减少动态/Instant保持无运动。补低画质实际renderer回放检查，不只测纯scheduler。

### FXR08 / P2：不可见来源被假定位、连接线可能穿过卡面

`Art07Effects.cs:57–61` 查不到instanceId时返回固定(560,260)。Debug超过10卡的原owner、已不可见的目标或缺失目标因此会连到无关位置，像指向另一张卡。`Link07:132` 跨行折线固定outsideX1518，在compact五列卡布局中末列右边界1578，折线进入末列正文；同排也使用卡中心到中心，穿过来源/目标的正文。

修复：TryCardAnchor返回是否有合法可见锚，不可见来源显示instance/name摘要或“目标不在当前视图”，不要编造坐标；锚取卡边，外侧路径依据当次真实卡区域bounds生成。补C41跨行、32卡不可见owner、目标失效三场景。

### FXR09 / P2：多个真实C53只揭示了第一个所有者

`Art07Effects.cs:88` 对random_reveal直接Take(1)。一个C53+C41共用q的情况正确；调试允许多个真实C53时，其余真实owner的抽中值被省略，而后续倍率仍播放。q=1/2也直接显示“×1/2”，未按本版视觉要求转换成清晰的“×0.5”。

修复：按stateOwnerId有界合并已提交的结果，以一份静态owner→q列表呈现多个所有者，不建立指数分支、不重抽；每个owner的C41只连到该owner同一份q。展示格式可把精确1/2转成0.5，数据保持分子分母。给两个真实C53分别0.5/8及各自C41做实际视觉验收。

### FXR10 / P2：140ms换焦点截断了要求的220ms压弹收尾

`BeginBeatEffects07:67` 每beat先ReturnLibraryEffects；`ReturnLibraryEffects07:169` 立即把前卡缩放复位。当前压弹曲线在`TickLibraryEffects07:162`到140ms刚达到1.03，80ms自然回落尚未播放，就被下一来源硬复位。规格明确140ms是焦点间隔，220ms回弹尾帧允许与下一焦点叠加。

修复：分离主焦点切换与卡片尾段任务，给上一卡剩余80ms只读归位；Skip/Cancel/销毁仍全部强制复位。补帧140–220的transform连续性，不能以缩短规格动画来通过。

### FXR11 / P2：三个永久目标会挤掉后续自毁提示；正文条覆盖骰池

`Art07Effects.cs:96–111` 前三个目标各占一个辅助组，随后ControllerDestroyed再申请组会失败并continue，没有合并“已消耗n卡”标记。`Art07Presentation.cs:112–113` 又只取前三条PostScore正文，风险事件很容易同时丢失。后置正文框x584–1664/y475–625位于正常未锁骰池，而非计分区；若仍有未锁骰，会以不透明底遮住它们。G147没有未锁骰或后置变化，当前截图不能覆盖这一风险。

修复：先按重要性分配三个辅助组，把多目标永久变更合并为一组，给自毁/收入/换色保留静态摘要；完整有序记录仍可查。正文条移入不会覆盖骰点/按钮的安全区域。补“至少三骰永久变化 + C42/C41分别自毁 + 尚有未锁骰”的实际帧，不改变风险结果。

### FXR12 / P2：3D碎片调用方姿态会被PrefabView恢复覆盖

`BreakDie07:124` 写入实例transform世界rotation/scale后调用Play；对于没有visualRoot的3D特效，`Art07PrefabView.Play→RestoreVisuals/Evaluate` 会立刻把同一个root恢复为初始化缓存的baseRotation/baseScale，丢弃调用方给定的骰子姿态和尺寸。当前位置被Play捕获，旋转/比例没有。这是源码路径结论；由于FXR05当前真实Roll根本没接此效果，尚无真实破碎录像佐证。

修复：外层placement root固定承载实例位置/旋转/尺寸，内层visualRoot做局部动画；或提供明确的放置参数合同，在Play捕获本次基准且Reset恢复原prefab基准。用不同朝向与0.65/1.18尺寸破碎验证。

## 后续验证建议

先修FXR01–06再扩大录制：G147 Normal/Fast/减少动态/连续最高档、SharedHalf C41+C53、两个真实C53、永久改点改色与玻璃Roll破碎。每段保留score/N/M、state/saveSHA、当前源码/MVID、PNG逐帧证据；覆盖1280×720与150%文本。池验证应同时记录CreatedArtEffects07、ActiveArtEffects07、真实Graphic/Material/AudioSource数量与每类峰值，不能仅因为纯预算类通过就认为接入后所有特效都受控。

本次结论：规则二次执行/随机问题未发现；有明确显示、层级、来源覆盖和事件接线缺陷。已观察的主界面外观有可检查实物，但未达到美术或整版ACCEPTED。

## FXR04–12 实施交接（2026-10-31，本轮未自行签验）

root 转实施授权后修改 `GameApp.Art07Effects.cs`、`GameApp.Art07Presentation.cs`、`Art07PrefabView.cs`，新增 `Art07FxIntegrationPlayModeTests.cs`。冻结 before/after、真实 Roll/remote 的已提交破碎接线、世界 placement 池、Fast/聚合来源标记、多 owner 静态完整记录、5 段跨行路径、220ms 跨 beat 尾帧、3 个语义辅助组、Low 首播/重播一致已落实。保留 root FXR01–03 的修复。新增只读明细与完整面前后值；I06/I07 分别显示真实提交/投掷差，购买骰强显示目标永久变化与当前投出分层，StartStage/新手按冻结 grant source IDs 显示。

准确性更正：FXR04 原审查把 `currentColor` 覆盖物理点色也列为原因，源码实际仅 `currentPip` 覆盖顶面的永久点数；物理颜色来自 `FaceState.baseColor`。真正需修的是旧 currentPip 与读取实时 Session after；两者现均已改。原失败和原审查记录保留。

已执行独立全 Runtime/UI + 原 GameplayPlayModeTests + 新 7 项测试的 C# 编译，0 error。辅助 csproj 为 `artifacts/cache/FxIntegrationTestCompile.csproj`，使用 Unity 6000.6.7f1 本机 DLL；屏蔽既有序列化 DTO CS0649 与旧 API CS0618 警告。此项不是 Unity 测试通过，不是美术验收。

root 接口：Runtime EndVisual 与非 Submit SkipVisual 调用 `FinishTransactionEffects07()`；稳定已提交明细入口使用 `HasCommittedPresentationDetails07(handId)` + `ShowCommittedPresentationDetails07()`。新真实 PlayMode 范围 `PLAY_ART07_FX_` 共 7 项，尚待 root Unity 实跑，原 200 次完整 renderer 回放须重跑。

冻结 SHA256：
- Effects: `5fb01fa2e8afc0d944d07010a7996c40bafde125da39b327b7cc5e7556b487a9`
- Presentation: `839c762f51f7c1505c503c0edfefda3c8707ffc43ce8fc4c990391dc562fc6e5`
- PrefabView: `794e65cda7ca3a5511369cd4631b2570c749d33461f8205b6704422f4c4fd518`
- Tests: `2a054be6f791f8b6ed9333274db4cfb086c128c831016a6498b625f756025f83`

剩余需实测关注：包含未锁骰的 PostScore 和跨行连接真实画面、多个 owner 完整记录在 720p/150% 的滚动可读性、世界碎片与层级，以及 Instant/reduced 投掷结束后的静态破碎警告保留。当前零时长投掷会在 FinishTransactionEffects 后清理动态摘要；必须由稳定操作反馈明确体现，不能仅凭本轮数据/池测试宣称体验满足。
