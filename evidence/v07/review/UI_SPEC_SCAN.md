# art0.7.0 独立 UI 规格扫描

本轮结论：**有缺陷 / 仍在 IMPLEMENTING**。22 个屏幕都有代码入口或组成部分，但“入口存在”不等于该屏全部状态、动效、文字与分辨率验收通过。本审查由 v07_spec_audit 完成，只读生产；没有启动 Unity、修改规则或修改验收标准。以下是当前开发遗漏清单，不是冻结版本最终审签。

## 输入与证据边界

- 权威输入：原样 `docs/specs/v07/ART_SPEC.md` / manifest，art0.7.0，玩法 p0.6.0 / schema4。
- 源码、71 导出图标、6 张预览及测试报告共 102 文件绑定在 `ui-scan/source-bindings.json`；工作树未提交且正在变化。审查副本在 `ui-scan/source/`，仅供本机复核，不放入正式构建。
- 直接读取真实 `evidence/v07/unity/nunit-20261031-175033.xml`：18/18 Passed、0 Failed、0 Skipped。逐项包含真实 26 prefab 实例、full/compact 复用、骰物理面/玻璃 GPU、200 次 renderer、三速状态和存档不变、两项音频回归；**不覆盖下列所有新 UI 路径**。
- 新 Modals / Guide / PanelMotion、ResultStats 的后续改动未在本轮独立启动引擎复验。原有 G147 157 帧录像与 FX 缺陷详见 `FX_INTEGRATION_REVIEW.md`；此处不重复将其当新发现。
- 32/64 图标联系表和原始 C07/C42 PNG 本轮直接查看；审查时图像副本在 `ui-scan/images/`。C35 方向修复已经目视复核。图标审查为离线规格与辨识度检查，不能代签用户艺术方向或游戏镜头验收。
- 全部 67 标准的静态覆盖、局部实测证据和缺口见 `UI_ACCEPTANCE_COVERAGE.json`。该文件是审查映射，不改写原验收登记或将未测项标为通过。

## 优先返工项

### UIS01 / P1：I06 的使用结果显示成“剩掷 +0”

`GameApp.Art07Effects.cs / PresentTransactionEffects07` 在 UseItem 分支中只专门处理 I01、I02、I04、I05，其余统一读取 `after.hand.rollsRemaining-before.hand.rollsRemaining` 并标“剩掷”。I06 实际增加提交预算，因此展示错误的计数类别和增量；规则本身没有因此改错。

修法：按已提交道具 ID 和对应 before/after 预算差展示“提交 +n”或“投掷 +n”，不在视觉中另发命令。验收 I06/I07 各一次、减少动态、取消/保存失败、重复回放，分数和钱包保持真实收据。

### UIS02 / P2：关末结算缺少已入账收入、基金和清理信息

`GameApp.Art07.cs / Settlement` 只有基础奖励、余次奖励、利息、奖励前后余额。S09 / UI17 要求本关提交收入明确“已入账”、基金增长写“卖价 +3”、临时骰清理；成功但无永久骰的结算原因也没有显示。本关成功标题和自动后继按钮已有，不能删除成功状态来补失败说明。

修法：从本关已提交收据和 `lastStageSettlement` 生成只读结算行，区分现金净增与非现金卖价；显示实际清理骰和 `settlementContinuation` 原因。不能再次执行结算。普通多次 C01、C52+C41、临时骰耗尽、第24关、成功但 NoPermanentDice 各核验一次，并重进同结算页验证不增长。

### UIS03 / P2：预览没有完整解释实际计分结构与随机来源

`ScorePanel` 只用 `chosenGroupId`，没有同时呈现 `primaryHandId`；预览数字使用正式提交的金色。C49 无5只出现一个 ×0 数值，没有“没有有效5点”的原因。C53 虽显示 min–max，标题仍为普通“预计得分”，没有 S07 的“随机结果范围 / 查看9种结果”入口或 C41 共用 q 标记。手册已经分别标记结构和计分，不能据此认为主 HUD 已满足同一说明要求。

修法：并列结构/计分（只有不同时需要双列）；预览用纸白并标随机范围；用只读 `ScorePreview` 的已有分支数据生成最多9行，多个真实 owner 只汇总范围与 owner，C41 指向同一 q；显示 ×0 的真实来源，不猜下一次随机。大整数详情应能复制原始精确值，现 `ShowBreakdown` 只有非可选文本，尚无复制入口。

### UIS04 / P2：非法种子关闭面板并丢失原输入

`ShowNewRun07` 点击开始时先 `CloseModal()`，随后 `StartNew(value)` 才验证 uint32；不合法值因此回到菜单，重新打开为空。违反 S02 的字段就地报错和保留输入。新局面板也没有随机生成按钮，已有 Run 只显示泛化覆盖说明，没有将替换的关卡/金币摘要。

修法：在关闭前验证，同一字段下显示原因并保留文本；成功才进入既有 NewRun 事务。随机生成仅填种子字段，不滚动假骰面。已有存档显示具体摘要并把当前开始按钮作为一次覆盖确认，无须叠加第二层重复确认。验证负数、4294967296、空值、新种子及取消，原存档 SHA 不变直到确认成功。

### UIS05 / P2：装备卡缺少稀有度和短说明，角标尺寸不一致

正常 `DrawController07` 没有 1/2/3 稀有度刻度与类别文字，也没有接上商店已有的 `Art07AttachTooltip`；普通卡当前值大多只写“点击查看条件”。C41 只显示目标技能名，无法常驻反映 C45 色、C51 n 或自身基金卖价。`ShowController` 已有完整详情，是可复用数据来源。

实际卡尺寸还存在 UI10 的明确差异：完整商店卡 160×224 却给28角标（应32）；已持有详情给32（应40）。紧凑卡28正确。C09/C46 图标的倍率数字依规范应由 TMP 拼装，当前只有乘号图形且默认当前值没有 ×4/×3，缺少相应信息；不要为修此项把运行倍率烘焙进 PNG。

修法：统一实际 HUD/商店/详情卡的稀有度、角标尺寸和动态摘要，文字与附魔保留独立区域；接入300ms短提示。不同栏目必须以自身 instanceId 解析，C41 不继承目标附魔。验证10卡、150%文字、C41+C45/C51/C52及失效目标。

### UIS06 / P2：普通错误 Toast 持续覆盖锁定骰区域

`DrawNavigation07` 将普通 notice 放在正常 y862–938，覆盖提交栏 y792–916；紧凑版 y840–916 覆盖第二排锁定槽 y813–915。notice 没有两秒淡出时钟，只在后续操作清除。当前是静态坐标交集证据，需在最大场景真实截帧确认遮挡内容，不把它说成已经拍到的失败截图。

修法：放到独立保留的原因区，不遮骰点/动作，最多两条且两秒后淡出；保存失败依旧使用模态，不用 Toast 定时消失掩盖。验证超额待选/不合法动作、K8第二排、150%字体和两条连续错误。

### UIS07 / P2：已锁拖拽和待锁标记的视觉语义未完成

规则路径保留 dieId、已锁不可移回池、Esc/失焦取消，但 `DrawDie` 拖拽只画插入线，未出现鼠标附近的本骰视图或移动抓手；待锁用实心“✓”且无待选顺序小号；槽位只画边线和槽号，未接独立锁标。UI03/UI04/UI07 的部分行为已有历史/局部检查，S05/S06 的完整视觉反馈尚未完成。

修法：增加纯显示拖影、原位占位、空心待锁标记/序号和实际锁标；取消归位不能 Execute 解锁。实际拖出、Esc、失焦、排序跨槽、待选超K验收；不要仅以回执不变代替可读反馈。

### UIS08 / P2：达标后缺少“现在结束”经济说明

达标后继续掷骰和 `CanEndStage` 路径存在，但左栏只有常驻达标文字，没有 S08 的基础/余次/利息预计、单列继续提交可能产生的 C01 收入；结束按钮也没有“本手未提交骰不计分，已用道具与投掷不退”的说明。

修法：用当前稳定状态只读计算奖励预估，收入来源分列；结束按钮合法性继续由规则决定，不新增确认或退还资源。测持有已锁骰结束、有待选禁用、C01及利息阈值。

### UIS09 / P2：刷新、扩容、强化等交易尚缺针对性视觉接入

目前成功 Buy 主要显示通用“已获得”；RefreshControllers 主要给全货架边框。S11 的旧报价100ms淡出/新报价180ms翻面、O05/O06仅新增位、S13明确物理目标面前后/360–420ms重叠变化，不能由“26 prefab已生成”推定已接入。预算附魔 StageStart/HandStart 的真实授予反馈也未在当前事务入口找到完整接线。

修法：每类从成功 receipt 做目标实例映射，其他区 sold 状态不得重建为新报价；预算只显示真实一次。以局部录像证明变化区域、来源、目标和时长。该项属于仍在制作的接入缺口，不是规则交易不安全的结论。

### UIS10 / P2：关卡预览选卡直接提交 ChooseChallenge

S03 明确“选择只改变本次预览中的选中框，开始挑战才提交正式命令”；当前 `ChallengeCard07` 每次选卡即 `Send(ChooseChallenge)` 保存操作。既有核心支持该命令，规则结果正确，但 UI 预览合同未实现。

修法：UI暂存候选 risk，开始时顺序提交必要的既有选择命令和 StartStage；每步失败保持检查点且不提前启动关卡，不修改核心协议。取消/退出预览不更改已提交风险选择。此修法要先由root结合既有复用约束判断，不能删除 v0.6 命令或改存档来迎合UI。

### UIS11 / P2：新屏幕关闭与重开必须补运行验证

新 `Art07PanelMotion` 关闭尾帧使用只读 mesh 而非活按钮，原输入关闭同步；该设计避免尾帧触发旧回调。但新 source 未被本轮18报告覆盖，复制TMP多通道UV、滚动裁剪、部分透明与快速再开、preview aperture都需真实验证。`Art07ScreenReveal` 也需检查池复用后CanvasGroup alpha恢复、菜单从对局返回是否误播首次240/420ms动画、结果立即跳过。

这不是已确认崩溃，列为高影响未测项。至少做150%详情滚动→关闭→快速另一详情、骰库旋转→关闭→投掷、首次/返回菜单、结果skip和200次面板开关的对象/mesh基线检查。

## 22 屏覆盖扫描

| 屏幕 | 当前实现入口/功能 | 静态结论与实际验证缺口 |
|---|---|---|
| S01 主菜单 | Menu / ShowBuildVersion；三静置骰、五入口、保存摘要 | 存在。旧存档错误页可达；继续摘要与初次/返回动效需重验；缺背景纸卡等艺术完成度不作功能通过 |
| S02 新局与继续 | ShowNewRun07 / StartNew / ContinueRun | UIS04；恢复稳定状态已有，已恢复上一手结果专用摘要与重启后的只读重播入口需补 |
| S03 关卡预览 | StagePreview / ChallengeCard07 / controller drag | UIS10；普通/风险、24刻度和预计K已有；入关锁序与实际动画待验 |
| S04 首掷前 | Gameplay / ScorePanel / DrawDie | 未投提示、无伪预览、按钮门禁存在；道具主栏尚无禁用锁标，详情有合法原因 |
| S05 待选锁定 | DrawDie / ConfirmLocks / DrawGuide07 | UIS07；超K不部分提交、道具使用禁用代码存在；需真实空心勾/顺序与迁移 |
| S06 排序受限 | DrawDie drag / action reason | K满仍允许重掷、零余掷与全锁原因分开；UIS07，真实拖影/取消时序待验 |
| S07 预览详情 | ScorePanel / ShowBreakdown | UIS03；精确文本来自规则，结构差异、随机解释、复制原值未完整 |
| S08 提交达标 | PresentCommitted / PresentationPlayer / Gameplay | 核心收据后播与继续有测试；FX报告返工，UIS08未实现经济提示 |
| S09 关末 | Settlement | UIS02；当前只是静态五现金行，不含完整关末非现金说明和行序动画 |
| S10 商店 | Shop / Art07ShopShelf / Art07ShopOwned | 固定货架、分区数量、售出标记、容量、持有滚动存在；最大报价/150%实机待复验 |
| S11 交易 | Art07Replacement / sale / refresh / PurchaseError | 正负净成本、基金重置、取消无命令存在；UIS09；卡片进入/新增位动效不完整 |
| S12 骰库 | DrawDiceSheet07 / DrawFaceNet07 / DrawProbability07 | 新三列、真实旋转、固定六面net、当前/永久/有效和0条已有；新modals尚待重跑 |
| S13 强化遥控 | DrawDiceSheet07 / RemoteTargets | U07不预抽、真实面目标、两不同骰和全部未锁说明存在；UIS09；小屏遮挡和修改后显示待验 |
| S14 牌型颜色 | ShowHandbook | 14行两列、2独立颜色、红组表、主结构和计分标记、键盘门禁存在；150%可达待验 |
| S15 详情 | ShowController / ShowOwned / Art07AttachTooltip | 真实实例/owner/当前值/价格/实验原因存在；UIS05：关内短提示与卡属性不完整 |
| S16 暂停 | Pause / CloseModal / SkipVisual | 暂停独立表现时钟、跳过最终态，已测局部；新尾帧/UI动效UIS11 |
| S17 设置显示 | ShowSettings07 / DisplayPreferences | 3页、4音量、质量辅助改键、15秒独立回退存在；平台独占旧失败不被新外观覆盖 |
| S18 整局结果 | DrawRunResult07 / ResultStatistics07 | 新总分/最高手/倍率/时间、最后骰、真实失败原因、只读构筑存在；新增统计未实测；旧存档用时明确未知 |
| S19 引导 | DrawGuide07 / DrawBoundaryTip07 | 五步与3边界提示已做；只读正式对局，独立练习明确标示；各步骤实际不遮主动作仍待验 |
| S20 调试入口 | DrawDebugAccess07 / ShowDebugEntry07 | Ordinal口令、三入口、独立副本、原存档保留；纯口令不标记debug；新入口实际待验 |
| S21 组合试验 | ShowDebugPanel / ReflowDebug07 / preview | 7导航、固定执行栏、50ID/5附魔、32集合、逐面/分支/日志存在；最大值实际表格与输入回填需新回归 |
| S22 错误恢复 | SaveFailure / ShowLoadIssue07 / RetrySave | 候选重试、完整放弃和备份信息存在；真实旧局部保存测试通过；UIS06/UIS11及旧schema引导待验 |

## Prefab 接口补充复核

只读检查 `Art07AssetLibrary.cs` / `Art07PrefabView.cs` / `Art07LocalFx.shader`。26分支包含真实 Graphic、TMP、UI按钮、6物理面及6片3D碎片，不是空对象凑数量；未知ID会抛错。View保留exactValue字符串，不算规则分数，不调用GameSession或RNG；SetRevealSlice只接受提交结果索引。Reset清实例、文字、按钮回调、角标、链接、粒子档位、mesh/material和暂停状态；full/compact使用同一BoundInstanceId。3D根姿态恢复问题仍见FXR12，调用方正确放置不能仅由View测试推定。

图集生成按路径将Point像素纹理与`_soft` Bilinear分离，关闭旋转/tight，padding4，资源包构建前调用一致配置；LocalFx有UI stencil/clip且不包含规则调用。真实18项报告对库组件有明确覆盖；**正常GameApp许多卡/面板仍由UiKit动态创建，因此库卡通过不自动保证实际页面包含同样的稀有度、状态字段和布局**。

后续应以UIS01、UIS02、UIS03、UIS04和已有FXR01–06优先返工，再按67ID矩阵采集真实页面/操作证据。Mac、最终美术、性能仍未验；本记录不把任何整版状态升为ACCEPTED。
