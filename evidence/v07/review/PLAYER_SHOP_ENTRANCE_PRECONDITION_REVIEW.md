# Windows Player 商店固定货架探针失败：限定静态复核

输入为821ec5992c34b59eddfe2a0ae984fbaca48f41f8 / art0.7.0 / p0.6.0 / schema4。复核者没有执行Unity、Player、修改生产源或验收标准。

**结论：已确认探针缺少“首次商店入场已经自然结束”的前置。应最小修复探针，再以实际运行证明修复；现有证据不构成产品固定货架布局缺陷，也不能把本次实际位移写成12px。**

## 实际失败证据

输入目录：`evidence/v07/player-821ec59-1107/strict-full/player-20261101-110810-22a4994f`。

- `player-report.json`：WindowsPlayer、Unity6000.6.7f1、Direct3D11、1920×1080 Windowed；Probe `p07-player-art-ui-18`；identity绑定821ec59；status=failed / phase=NormalReplay / exitCode=1。异常为 `Switching lower tab moved the fixed controller shelf.`，来自VerifyV07ShopTabs。
- `normal-replay.jsonl` 最后一项：index8 / Continue / actionSequence9 / phase Shop / ordinary run；savedStateMatches与renderedStateMatches均true。完整145步仅完成9步，未计整体通过。
- `v06-target32-raycasts.json` 最后一项：frame427，Tab0，firstHit=Tab0、firstButton=Tab0、firstButtonIsTarget=true、未被遮罩裁切，处于1920×1080。`v06-ui-render-readiness.json` 同次Tab0准备就绪。因此失败已在第一次合法页签点击后的固定货架比较阶段，不是错误控件或未注册图形。
- 报告/旁路JSON**没有保存此时每张卡的基线位置、比较位置、入场Elapsed/IsPlaying**，也没有商店失败现场PNG。不能反推每张卡实际差多少像素或声称直接看到了偏移。12是生产代码及规格定义的入场起始偏移，不是本批实测值。
- 原报告堆栈保留了build符号中的V06.cs:309；当前方法/断言位置按本次raw source绑定记录，不以堆栈行号替代源版本核对。

## 入口到具体组件的因果链

1. `Runtime/Verification/PlayerVerification.cs:463` 在每条规则命令后 `WaitReady()`；650–658该方法只等待app.IsBusy，随后检查pending save。
2. `Runtime/GameApp.cs:41` 的IsBusy仅为Session.Busy。S10入场有意不锁游戏输入；既有 `PLAY_ART07_ShopFirstEntryUses240msShelf30msCardStagger12pxRiseAndReducedFade` 明确要求app/Session均非Busy。
3. 主replay约494行遇首个Shop立即调用VerifyV07ShopTabs。该方法在读取positions前没有yield、也未查询商店入场组件。
4. `UI/GameApp.Art07ShopEntrance.cs:10–29` 把controller Offer/Buy/Availability实际Rect绑定给main的Art07ShopEntrance。`Runtime/GameApp.cs:59` 创建的main是canvas直接子节点Screen；没有必要在全层随意First查一个时钟。
5. `UI/Art07ShopEntrance.cs:22–35` 首次generationId执行Evaluate。`Evaluate`51–61按真实Elapsed将卡片置于origin+Vector2.down×12×(1−ease)，货架240ms、卡片180ms、30ms错位。规范ART_SPEC.md:248规定首入240ms淡入、30ms错位、12px上移、完整≤520ms，此运动是产品要求。
6. `UI/GameApp.Shop.cs:20–21` 页签点击更新shopTab并Refresh；42行再次BindShopEntrance07。同一generationId的Bind在原根已被UiKit重新布局后，选择Complete终态，不重播、不给旧origin写回新页签位置（Art07ShopEntrance:24–34）。因此合法点击可以把入场中间位置立即归终态。
7. 现有探针把未保证完成的入场中间位置当成“固定货架”基线，再与tab重绘终态做严格相等比较；即使产品完全遵守规范，也可产生当前错误。

这个判断不意味着原全部商店布局已通过新Player。它只证明此探针缺少必要且可实际观察的前置，并且现有失败与此直接因果链一致。不存在把该失败强行当普通原生显示mode0失败的理由；原生显示失败/阈值保持独立。

## 最小候选与验证边界

缓存 `minimum-probe-wait.patch` 只在VerifyV07ShopTabs取得positions之前，使用已有 `ActiveControl<Art07ShopEntrance>("Screen")` 取得唯一活跃真正owner，等待其IsPlaying自然false；3秒工具超时与现有真实入场用例采用的上限一致，防挂起，不改变规格520ms验收。绝不调用Complete、Refresh、Skip来绕过动画。

原有以下断言逐字保持：controller货架≤6、每个Offer anchoredPosition相等、五页产品精确类别、完整StateHash与磁盘字节纯读、普通/总容量文字。只修基线采样时点，不改全局WaitReady，不扩大生产Busy，不固定sleep猜某个帧率。

root可在现有商店真实用例中首次SettlementContinue后、IsBusy=false且Art07ShopEntrance.IsPlaying=true时执行此检查器，验证首次基线等待自然完成、五页位置和状态/保存仍严格相等。现有两条S10用例证明入场/中途页签归位契约，但不能代替修后实际Windows Player完整回放。此cache候选仅静态复核，未运行或宣称编译/Player通过。

未发现需要修改产品动画、货架位置或放宽原比较的证据；需要修后实际Player确认。旧失败原件与SHA留存。


## 已集成差异复核（Probe 19，静态通过）

root保存的原两文件位于 `artifacts/cache/v07-player-shop-entrance-rework`，其raw SHA与上述821ec59失败分析时的原源完全一致。逐行/字节比较确认：

- `PlayerVerification.V06.cs` 仅在positions基线之前插入6行（2注释、精确Screen owner、3秒deadline、等待真实IsPlaying、超时后Require）。移除这6个新增行后，与保存原文件逐字节相同；因此原有全部固定位置、类别、StateHash/磁盘、容量断言与其余探针代码均未改动。
- `PlayerVerification.cs` 唯一字节变化为 `p07-player-art-ui-18` → `p07-player-art-ui-19`。性能180秒、16.7ms预算、native/display及其他验收参数没有改变。
- 实际集成版本把超时条件放进while，并在循环后Require(!IsPlaying)，与所提候选在正常完成/超时两条路径等价。它未调用Complete/Skip/Refresh，没有新增规则输入锁，没有改变生产动画，也没有用固定等待掩盖帧率差异。

**实际集成差异静态复核通过，可以进入root串行的既有S10真实用例和新源完整回归/实际Player。** 本条未执行Unity、未声称这两项或后续Player已经通过。原821ec59 Player失败保留。精确新旧raw SHA及单一diff由 `integration-bindings.json` 记录；原静态报告原件保存在 `REVIEW.pre-integration.md`。
