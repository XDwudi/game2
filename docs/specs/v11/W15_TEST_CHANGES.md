# W15 测试与文字迁移

依据 `SETTLEMENT_LAYOUT.md` §2、3、6、10 的批准版。以下为关末结算子项。验证以实际 Unity 的 NUnit、截图和逐项测量报告为准。

| 测试 | 迁移内容 |
| --- | --- |
| `PLAY_ART07_SETTLE_PaperCashStaggerPauseRedrawSkipAndFontsUseOneCommittedReceipt` | 100% 的纸为 880×620，125% 与 150% 为 1120×780。按芯片根节点查淡入、逐字检查各芯片边界；保留老测试的存档字节、金币、重复播放与暂停断言。 |
| `PLAY_ART07_SETTLE_BACKDROP_CommitAndColdRecoveryShowReadonlyBoardWithoutCommandsOrReplay` | 增加捕获根名单检查；实时与冷启动必须含 W11 托盘、本关牌与提交栏，排除行动与下一步金币预览。保留已提交骰面点数和无碰撞体断言。 |
| `PLAY_ART07_SettlementUsesActualIncomeFundAndTemporaryCleanupReceipts` | 打开明细核对“本关提交时已得”、“卖价 +3（现价 N，卖出时才得到）”和移除颗数，现价读会话。汇总的新增金币和骰子耗尽说明分别断言。 |
| `PLAY_V09_SettlementListsStageIncomeFromControllersAndTheWiderInterestCap` | 分查芯片金额、汇总金额、钱包始末、明细的利息上限与卡牌成长。40→58、奖励分拆和成长值仍取同一记录。 |
| `V21TitleFontPlayModeTests` | W15 删除“本关奖励已处理。”，正文角色取同页的金币始末行。 |
| `GameplayPlayModeTests.Click`、`Art07ShopInterestPlayModeTests` | 原先直接点“继续”的导航场景若仍在演出则先操作实际“跳过演出”。独立时钟测试验证继续按钮的出现时机。 |
| `PLAY_V15_SE_OrdinaryReceiptSixStylesAtTwoTextScales` | 普通关 6 套风格 × 2 个字号，检查纸面几何、奖励滚动、唯一的继续按钮用色、正文与合计字号、字形边界、对比、明细切换和存档不变；各拍演出中、结束、明细三帧。 |
| `PLAY_V15_SE_BossStampFlipFinalOrExhaustedAndSavedWallet` | 同一真实命令链验证首领标准、快速、即时、减少动态、终局和骰子耗尽。核对章、翻面、钱包、配置短文、按钮和回执。首领图缺失时的截图文件名带 placeholder。 |
| `PLAY_V15_SE_EscapeFreezesAndEachEnterAdvancesExactlyOneStep` | 通过真实 Input System 键盘按 Esc 暂停和恢复；小键盘 Enter 跳过，主键盘 Enter 继续。只产生一次 Continue。 |
| `V15ReceiptTermsTests` | 四组卡牌收入检查是否重复计入赏金；五组存钱罐、利滚利、理财顾问检查基础上限与加成上限的文字和数值。 |

## 整合时补充的回归

W24 与 W25 在整合时增加了以下路径。验证结果记录在 STATE 与对应 NUnit 报告。

| 用例 | 补充检查 |
| --- | --- |
| `PLAY_V15_SE05_ClaimInFlightAndRetryKeepEnterShopDisabled` | 多选奖励已领一件，再次领取遇到写盘失败时，进店按钮须锁住。重试后复核库存、存盘与动作序号仅增加一次。 |
| `PLAY_V15_SE09_FullInventorySaleMakesTheSamePendingOfferClaimable` | 满栏时卖出一件，重新检查同一待领取奖励的可用性。 |
| `PLAY_V15_SE10_FaceAndWholeDieClaimsCancelAndPersistForBothSources` | 免费选面及整骰改造涵盖取消、确认、读档和两种来源。 |
| `PLAY_V15_SE13_ThemeDefaultAndMissingUseTheRenderedFallbacks` | 逐级检查主题图、默认图和缺图表现。 |
| `PLAY_V15_SE01_SE07_LootForegroundLayoutAndInk` | 六主题、两档字号核对实际前景、边界、对比与真实点击。 |
| `PLAY_V25_C114_` 三例 | 真实点化、出售、同名卡位置变化和冷读档后，上一次提交能按当时顺序和名称显示受影响的卡。 |

## 结算文字对照

| 玩家位置 | 已实施的文字与数源 |
| --- | --- |
| 顶部 | 第 N 关完成、配置对应的关卡类型、本关得分 N（目标 N）。 |
| 汇总 | 共获得、+N 金币、金币 A → B，均取不可变结算记录。 |
| 芯片 | 过关奖励 +N、剩余提交 N 次 +N、利息 +N。铁饭碗和赏金猎人仅在各自收入大于 0 时出现。 |
| 明细 | 利息每 N 金币 +M 与基础上限、加成上限，正数提交收入、基金卖价、到期临时骰，其他变化复用计分后事件的玩家文字。 |
| 说明 | 奖励作废时说明首领减益，骰子耗尽时说明整局结束。 |
| 首领 | 配置首领名、击败、首领战利品、配置显示名及短文，终局写“终局首领 · 通关即胜利”。 |
| 操作 | 明细／收起明细、跳过演出、进入商店／选择战利品／查看整局结果／查看结果。 |

## 战利品、整局结果与首领入关

| 用例 | 验证路径 |
| --- | --- |
| `PLAY_V15_SE08_SixLootKindsUseTheirRealOffersAndClaimExactlyOnce` | 用真实命令分别打到六种首领奖励，领取并进入商店。核对每次领取只递增一次动作序号，免费改造保留选面步骤，多选奖励在进店后可继续领取。 |
| `PLAY_V15_SE09_P0AndP1ResumeWithThePersistedPickCount` | 在一次未领和已经领过一件的状态分别读档，核对落点、剩余件数以及战利品与两类赠礼之间的切换。 |
| `PLAY_V15_SE15_NewPageOnlyRejectsEarlyPointerClicks` | 走实际指针事件，测折现和进店后的 0.3 秒保护，核对金币、动作序号和存档字节。 |
| `PLAY_V15_SE11_LootEntranceSkipsWithoutReplayingOrMutatingTheSave` | 标准、快速、减少动态和即时四种模式分别进入领奖页。逐一核对时长、跳过、重绘与磁盘状态。 |
| `PLAY_V17_DefaultLootIllustrationsInReceiptAndPicker` | 在首领结算和领奖页读取实际绑定的精灵、尺寸，并截取六种奖励在 100% 与 150% 字号下的完整画面。 |
| `PLAY_V15_SE_ResultsSixThemesAndTerminalCases` | 用已完成的命令触发通关、提交耗尽、临时骰耗尽、玻璃骰碎尽四种结果，逐套检查六种风格、两档字号、字形和存档。 |
| `PLAY_V15_SE11_BossEntranceRealStartAndSpeeds` | 从选关进入首领关，在真实场景里检查演出切换、速度与最终对局。 |

两种窗口分别取证；实测结果和已知缺口记在 [STATE](../../STATE.md)。

六套 `PLAY_V15_SE01_SE07_LootForegroundLayoutAndInk` 在操作检查中增加 L5 候选骰四角和相邻领取按钮的真实射线命中。预览的透明点击区沿用商店 60 设计单位的下限，在 720p 至少为 40 像素。另测从预览边缘进入详情、回退、点击领取后的动作及存档。[首次实测与截图](../../../artifacts/cache/v11/w24-loot/hit720/REVIEW.md)

`PLAY_V25_InstalledBossEmblemsOnFourRealScreens` 覆盖六套、七族和两档字号。对局中逐一核对族图、档边和实际精灵，另在商店顶栏、首领结算与整局通关页验证对应图像及标签，覆盖 22、60／72 和 176 的实际显示尺寸。

## 2026-11-23 全量回归与领奖交接

输入 `9974c97e` 的 1080p 全量有 320 项，301 项通过，19 项失败。下表列出其中 7 项和本轮修改保留的检查。旧结果与失败堆栈可在 [NUnit 原件](../../../evidence/v11/unity/nunit-20261122-200239.xml) 中按用例名检索。

| 用例 | 本轮如何验证原来的行为 |
| --- | --- |
| `ART07_ChallengeStartSaveFault` | 存档写入失败后关闭弹窗，确认选关主按钮禁用，对它发送真实指针点击，再从写入失败弹窗重试。动作序号、原文件、待提交候选和日志逐一比对。 |
| `ART07_FINAL_SkipKeepsZeroScore` | 跳过后用已提交 `SubmissionIncome` 的金额核对“提交得 +N 金币”，继续检查零分原因、只读明细与存档。 |
| `ART07_FX_MultipleRandomOwners` | 从已提交的演出记录找两条随机揭晓，逐条核对卡牌实例、与模仿牌共用的来源、×0.5／×5 和对应明细行。三条乘算记录可以使用相同的卡牌名。 |
| `ART07_UPGRADE_ActualCeramic`、`ART07_UPGRADE_FastReduced` | 升级板的“还没掷”或“本手朝上”由目标骰此手的实际记录决定。保留陶瓷／玻璃、问号翻面、真实像素、物理权重、文字范围和提交次数的原检查。 |
| `ART07_RESULTROWS_RealVictory` | 依原时钟逐行揭示六组结果。每组必须有实际文字，检查它们的字界，并逐个核对构筑卡面印出的名字与持有卡牌。按钮模板自带的空文本不承担卡名。 |
| `V14_SH04_NextStageWithLootLeft` | 领取一件后从领奖页进店，等真实货架入场结束再量“还有 2 件战利品未领，离开后作废”。仍检查首次领取前的禁用、剩余件数、4.5∶1、存档写入中与重试。 |

W15 新增五种实机场景。SE-11 追踪折现按钮到钱包、已提交的骰子／卡牌／道具到目标位置，以及 L4、铁匠赠礼、付费货架的快速改造时钟。SE-14 走正常档的主菜单继续，比对普通与首领收据在两档字号下重启前后的画面和同一存档。SE-15 对普通收据的同一位置再发真实指针点击，验证 0.3 秒保护。

W24 的六主题 `PLAY_V24_SC16_SE04_*` 增加 W16 SC-03 检查。当前弹窗从 `Dialog` 取前景，按实际涂满主色家族的按钮计数；禁用面上的主色也会计入。确认保留显示设置、完整提示、选关忙碌、存档写入中四个状态各自拍照。忙碌状态置位对局使用的 `Session.Busy` 短时标记，写入中使用真实文件替换失败。文字用实际墨迹和隐字底图核对，箭头单独隐去，用精灵不透明核心逐像素量取。保留 4.95∶1 挑色目标和经 W11b 调整后的 4.5∶1 下限。

上述改动已在工作树以 Unity 6000.6.7f1 所用的 Roslyn 编译运行时及完整 PlayMode 测试程序集。新场景与迁移用例的通过状态以随后绑定新输入的窗口 NUnit 为准。[编译记录](../../../artifacts/cache/v11/w24-screens/primary-actions/integrated-compile/DiceCraft.PlayModeTests.log)

SE-17 的双窗口原图索引显示，首领盖章中途、终局首领结算纸、骰子耗尽结算纸各缺六主题与两档字号，共缺 72 格。新增 `PLAY_V15_SE17_BossReceiptScreens` 从真实提交按钮进入三条路径，核对存档与结算去向。首领演出分别记录盖章和战利品翻面，耗尽场景用临时骰到期触发，并点击继续确认失败结果。每档窗口应产生 36 个缺项的原帧与 12 张翻面补图。[逐格原图索引](../../../artifacts/cache/v11/review-20261123/index.html)
