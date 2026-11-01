# S04 / S05 / S10 / S12 / S15 / S18 动画剩余实现短审

输入 art0.7.0 / p0.6.0 / schema4，正文 ART_SPEC。独立只读审查 `/root/v07_art`，2026-11-01 UTC。范围只沿已明确规定的入口到实际运行组件，不重复39类FX，不改变标准。当前源码快照与SHA见相邻 bindings；本次未运行Unity、不以源码证明实际GPU时序。

## 明确未完整实现（REWORK）

| ID | 原规格 | 实际调用链与遗漏 |
|---|---|---|
| AN01 | S04，194：首掷busy按钮短暂机械点阵、不循环 | `GameApp.Art07.cs:300` Roll始终为“掷骰/重掷 n [R]”；`GameApp.Art07Presentation.cs:81–89`只Busy→Refresh→BeginRoll，未接点阵组件/动画。禁用按钮及真实投掷已存在，缺的是要求的短点阵反馈。 |
| AN02 | S05，198：未选悬停纸白轮廓；待锁6px/120ms，取消80ms | `GameApp.Art07.cs:313–317` click直接TogglePending→Refresh；`UiKit.cs:138–145` DiePointer没有enter/exit；无其它Dice hover接入。`DiceTable.cs:157–172`确有6design px，但待锁/取消均用默认LayoutDuration=.18，只有重排140/拖取消120另行覆盖。纸白悬停与120/80时序缺失。空心勾、顺序和金虚线已有，不重判。 |
| AN03 | S05，202：选择顺序锁入尾部，25ms错开、每骰180；完成后才更新预览 | `GameApp.cs:143–152`收集PendingIds并提交；`GameApp.Art07Presentation.cs:91–95` ConfirmLocks走立即Refresh；`GameApp.Art07.cs:257/409`随即用新Session预览；`DiceTable.cs:171/294`所有迁移layoutElapsed从0同时开始，无逐骰delay或锁定迁移完成屏障。单骰180与最终顺序存在，错开与预览门控缺失。规则只执行一次，不建议改Core。 |
| AN04 | S10，248：首次入店货架240ms淡入、各卡30ms错开/上移12px，总≤520 | `GameApp.cs:89–95`→Shop；`GameApp.Shop.cs:11–42`直接建货架/卡/分页，无入店演出。`GameApp.Art07Effects.cs:429–431` TransactionMotion只接受Buy/RefreshControllers，不能覆盖Continue入Shop。240/30/12入场未接。购买old160缩小+new180获得在同文件456–475及TransactionMotion明确已接，不列缺项。 |
| AN05 | S15，288：控制器和道具短悬停300ms+淡入80ms、离开60ms | 已接的控制器与商店持有道具经Art07AttachTooltip→Art07HoverTip，确有300/80/60；但`GameApp.Art07.cs:239–253`对局DrawItems07仅Click→ShowOwned，未调用该tooltip或其它等效路径。因此对局局内道具缺此悬停，而非组件本身缺80ms。 |
| AN06 | S18，312：金章360后分数/用时/最大单手/最高倍率/构筑/种子逐项出现，总≤1.2s | `GameApp.Art07Results.cs:34–46`将得分、用时、最大单手、最高倍率、种子合成一个EndStats字符串；96–106将title/message/整个stats/build四块传给ScreenReveal。故五项统计同一CanvasGroup同时淡入，非逐项；种子先于build。章360、有限1.2s、跳过、Reduced已接，不能将整页认作无动画。 |

## 源码已接，未新增缺项

- S04新手回池180：提交表现结束Refresh后复用同dieId Visual；Hide只关闭active，不丢原姿态，Display变更目标时使用默认180。没有屏外新生路径。本次未新增实测结论。
- S12：ShowDie/商店TargetDie→DrawDiceSheet07→OpenDiceSheet07（Modals:112–144）实际调用FadeModal160/exit100及SetPreviewReveal延迟60、fade100；Art07PanelMotion.Update推进，CloseModal抓只读尾部100并立即逻辑关闭。镜头孔位静止，避免滑出。源码路径完整，既有实图/时序验收仍依原报告。
- S15已接对象：Tooltip组件300延迟→PanelMotion80入；leave60后hide。正文只明确入场淡入，未擅自将“60ms消失”解释为必须额外渐隐。292“卡面**可**在180ms内放大”属可选，不列缺项。
- S18失败有限120淡入，无整屏红闪/持续摇晃；金章正常360与Skip入口存在。仅AN06统计分项遗漏。

## 验证边界

未把录完截图数量视作时序通过。以上缺项来自当前实际路径和缺失的分支/状态字段，并绑定源码快照；六屏旧截图、39FX历史录像不自动覆盖本轮新增源。S01、S02、S03、S09及已知Mixer不在本次审查。修复应单独最小实际时序验证，保持收据/存档/规则不变；本报告不签整版ACCEPTED。
