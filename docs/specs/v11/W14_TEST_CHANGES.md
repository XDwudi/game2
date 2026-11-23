# W14 测试改写记录（商店）

| 项 | 内容 |
| --- | --- |
| 任务 | DEV_PLAN §5.2 W14：商店（[SHOP_LAYOUT](SHOP_LAYOUT.md)，协调者 2026-11-17 批准） |
| 基线 | `codex/v1.1` 的 `f4b41d74`（W13 验收、W14 批准之后）；唐三彩由 `dc85193a` 合入 |
| 原则 | 不删除测试、不降低阈值、不缩小范围。旧断言钉住 v0.73 商店的元素（左栏、价签、六个分区、骰库窗口里的选目标）时，改成对新商店表达同一意图的断言，写明理由；代码里的对应位置写有 `V1.1 (W14)` 注释 |

## 1 改写的断言

改写都在 `DicePrototype/Assets/Game/Tests/PlayMode/` 内。

| 测试（文件） | 旧期望 | 新期望 | 理由 |
| --- | --- | --- | --- |
| `W13GalleryChecks`（V13CardTemplatePlayModeTests.cs） | 商品的类别只有道具 / 服务 / 骰面改造 / 新骰子；商品边框正好是 Ghost | 类别多一个“牌型升级”；已售的商品整张压暗时，边框是压暗后的 Ghost（乘 62%，同失效卡）；卡面红色分量低于 0.75 算压暗（LabelOn11 为让字可读，会把压暗的 0.62 卡面按 5% 一步提亮，默认风格的已售货品行读到 0.639；白卡面压深四步仍有 0.81）；失败信息加上实际颜色 | E2：牌型升级成为货品行；已售商品压暗（SHOP_LAYOUT 6） |
| `Art073CheckShopPage`（Art073PlayModeTests.cs；用于 `PLAY_ART073_ShopSinglePage…`、`PLAY_ART073_ShopAtMaximumShelfSizes…`） | 六个分区（`ControllerShelf`、`DiceCounter`、`ConsumableSection`、`ResearchSection`、`UpgradeSection`、`ServiceSection`）与 `OwnedStrip`、`ShopProducts`、`Stats07` 在屏内；价格在 `PriceChip/Price`；分区里多于一行的商品可以滚动，按视口里看得见的部分判断 | 四个分区（`ControllerShelf`、`ShopProducts`＝升级与服务、`DiceCounter`＝骰子与骰面）与 `OwnedStrip`、`Build14`、`Stats07` 在屏内；价格在价格按钮 `Buy_<id>/Price`；页上没有任何滚动视图；150% 时没画出的商品只能是“升级与服务”另一页上的（页数 > 1）；重叠检查加上 `Build14` | SHOP_LAYOUT 2（四区）、CARD_TEMPLATE §5（价格即按钮）、Q3 A（翻页不滚动） |
| `PLAY_ART073_ShopSinglePage…`（同上） | 标题 `ShopTitle` 是“商店” | “商店 · 第 N 关前”（N 为下一关） | Q5 B |
| `PLAY_ART073_ShopAtMaximumShelfSizes…`（同上） | 六张卡分两行；五件道具在“消耗品”分区里滚动（`ConsumableShelf`） | 六张卡一行；100% 时“升级与服务”的商品全部在页上，150% 时第一页 6 件、翻到第二页是其余的，第二页再过一遍整页检查 | SHOP_LAYOUT 2.1、2.2 |
| `PLAY_V11_ShopIconButtonsKeepTheirLabelsThroughHoverPressAndExit`（同上，SH-11） | 刷新、设置、手册三个按钮 | 再加“查看本章”（标签前有本章三点）；阈值不变（0.01 单位） | 新顶栏；SH-11 |
| `PLAY_ART07_ShopFirstEntry…`（Art07ShopEntrancePlayModeTests.cs） | 入场分组 11（6 张卡 + 5 个分区），时长 0.48 秒；第一行卡停在 y −124（未停稳时 −136） | 分组 8（6 张卡 + 两个货品分区），时长按同一公式 7×30 ms + 180 ms = 0.39 秒（上限 0.52 秒不变）；卡停在 y −160（−172） | 四区布局；入场动画的参数（240 ms、30 ms 错开、12 单位上升）不变 |
| `PLAY_ART07_ShopRedrawSettles…`（同上） | “下一关”x 1512；`ShopProducts` 在 (368, −512)；货架卡在 y −124 或 −316（两行） | x 1432；(16, −523)；都在 y −160（一行） | SHOP_LAYOUT 2.1 |
| `AssertTransactionTicketClear07`（Art07TransactionPlayModeTests.cs；两项 TX 测试） | 现金票不能盖住 `Money`、`Interest`、`ControllerShelfTitle` 等字 | 必须存在的字改为 `Money`、`NextStage14`、`ControllerShelfTitle`（利息移到金币悬停里）；遮挡检查不变 | SHOP_LAYOUT 3 |
| `InterestCheck07`（Art07ShopInterestPlayModeTests.cs） | 左栏 `Interest` 两行“每5金币利息 +N⏎计息本金上限 25”，在设置按钮之上 | 金币悬停的说明“利息：每 5 金币 +N，每关最多 +M”，字完整（一或两行），悬停框在屏内；N 仍按当前的 C01 来源（含百变模仿复制）计算，并与真实结算对照（不变） | UI_REWRITES Art07Shop.cs:34；SHOP_LAYOUT 3 |
| `PLAY_ART07_ShopWalletExactDigits…`（Art07WalletPlayModeTests.cs） | 左栏的字标“骰子构筑”、`MoneyUnit`、`Money`（38×字号比例）、两行利息；不压设置与手册 | 字标不再出现；顶栏金币 `Money` 与 `MoneyLabel11`：每一位数字（0、1,000,010、1,000,000,000）、字号 34（放大文字 31.5，关键数字下限），一行、数字墨迹分开；利息在悬停里完整（悬停淡入 80 ms，显示 0.2 秒后再读）；不压设置、手册与查看本章，它们 ≥ 40 px | SHOP_LAYOUT 3（金币进顶栏，字标删除） |
| `PLAY_ART07_CardInformation…`（Art07InformationPlayModeTests.cs） | 满货架时购买按钮写“购买”，价签写价格，可用性行写“卡牌已满 10 张”；道具卡的可用性行写“道具栏已满” | 价格按钮上写价格与原因“卡牌已满 10 张”/“道具栏已满”，且不可点；道具行仍有一个 `Inspect` | CARD_TEMPLATE §5（原因写在按钮上） |
| `AssertBoundaryCardPrice`（ShopBoundaryPlayModeTests.cs；V09、商店边界测试共用） | 价格在 `PriceChip/Price`；购买按钮总写“购买”（不能买的原因另起一行） | 价格在 `Buy_<id>/Price`；按钮上的字（`Buy_<id>/PriceWord13`；第一个字现在是价格）不为空，恰好在按钮可点时是“购买”，否则是原因；具体原因由各测试另行断言（如 V09 的“卡牌栏位已满”） | CARD_TEMPLATE §5（原因写在按钮上） |
| `InspectOfferCard`（FailureAndInputPlayModeTests.cs；所有打开商品详情的测试共用） | 找到 `Product_<id>` 的 `Inspect` 并真实点击 | 先用该分区真实的翻页按钮翻到商品所在页（新的 `PageToOffer14`：先回第一页，再往后翻；卡牌不翻页），再同样点击 | Q3 A：分区翻页、不滚动；“骰子与骰面”在调试工作区多于四行时翻页（SHOP_LAYOUT 2.1） |
| `PLAY_ShopTargetCancellation…`（同上） | 骰库窗口：打开改造时确认按钮禁用；预览写“购买后：2点 · 蓝 · 光滑”“等概率取一个…镀金保持”；“相同属性”“没有合法目标”；用 × 关闭 | 桌上选面：打开改造在 S1，没有确认按钮；`PurchasePreviewBody` 为“2 点蓝面 → 2 点蓝面（光滑）”；U07 为该面当前写法（循环里前面的改造已给它纹理与黄金，如“2 点蓝面（粗糙、黄金）”，测试按状态逐项拼出）加“ → 随机变成 1、3、4、5、6 之一”（其余点数取配置），补充行“颜色、纹理、黄金不变；买下时才抽”；原因“这一面已经是光滑的，换一个面”“至少要留 1 颗骰子”；用“取消” | SHOP_LAYOUT 7；其余断言（不扣钱、不抽随机数、命令的骰子与面、改造结果）不变 |
| `PLAY_ShopCapacityDiscount…`（同上） | 报价详情的价格行以“价格 N 金币”开头 | 以“价格：N 金币”开头 | UI_REWRITES Shop.cs:136 |
| `PLAY_PooledPurchaseAndReadonlyFaces…`（UiPoolingPlayModeTests.cs） | 骰库窗口里整骰改造时同一个面按钮保持停用，直接调用它也不会再打开旧目标；重用的确认按钮只执行最后一次购买 | 桌上选面：整骰改造不画面按钮；直接调用前一流程留下的（隐藏的）面按钮也选不了面，状态不变；再次画出的面按钮（同一个池对象）已去掉前一流程的监听；重用的确认按钮只执行最后一次购买（其后断言不变） | SHOP_LAYOUT 7 |
| `PLAY_ART07_ExtraCapture_S13…`（Art07ExtraCapturePlayModeTests.cs） | 同上一行的骰库窗口流程与文字；`ExtraPurchaseText07` 要求预览在滚动视图里，翻页检查每个字形 | 选面流程与第 7.2 节文字；面板不滚动时，检查变化行与补充行的每个字形都画在面板内、确认按钮可点 | SHOP_LAYOUT 7 |
| `PLAY_V06_ShopThirtyTwoTargets…`（V06TargetSelectionPlayModeTests.cs） | 32 颗骰子的目标列表要滚轮滚到 D031 / D032；选中后仍在视口里 | “我的骰子”在调试工作区 8 颗一页：D031 一开始不在页上，用真实点击翻页（`DicePageNext14`）到它所在页；选中后仍在该页；其余断言（只改选中的骰子、取消不变、删除只删那一颗、提交栏与随机状态不变）不变 | SHOP_LAYOUT 7.3 |
| `PLAY_V09_ShopSellsASlot…`（V09PlayModeTests.cs） | `OfferAvailability_C13`“卡牌栏位已满”，买下扩充栏位后为空 | 价格按钮写“卡牌栏位已满”且不可点；扩充后写“购买”且可点 | CARD_TEMPLATE §5 |
| `PLAY_V10_BossStage…`（V10PlayModeTests.cs） | 入口按钮写“首领战利品”；领取后再点它看到“已领完” | 写“首领战利品（还可领 1 件）”；领取后入口从顶栏消失（断言不存在），对话框直接打开后仍检查“已领完”与其余件不可领 | DEV_PLAN §4.4 第 5 条；SHOP_LAYOUT 3 |
| `PLAY_V11_TEXT_SameShopResaleCap…`（V11TextP0PlayModeTests.cs） | 详情“交易与规则”以“当前卖价 1。”开头 | 以“卖价 1（本店买入，不超过买价 1，离店后恢复）。”开头 | SHOP_LAYOUT 8（C98 封顶要看得见） |
| `PLAY_V11_TEXT_ShopNextTarget…`（同上） | 左栏 `NextStageValue` 等于含首领减益的目标 | 顶栏 `NextStage14` 以“目标 X”结尾，X 为同一个数 | SHOP_LAYOUT 3 |
| `CardCheck07`（Art07CardReadabilityPlayModeTests.cs，`shop-full`、`owned-mini`） | 商店卡与已装备小卡要有稀有度字样和刻度；商店卡的状态行等于报价状态（“技能详情”等） | 两者由卡面模板画，与桌上的卡同样要宝石与边框、不得有字样；商店卡要有完整短文，数值角标为报价状态的数值（“技能详情”“模拟右邻”“购入定色”“随机倍率”没有角标）；图块的附魔角标 24 | CARD_TEMPLATE §3、§4，E3 |

## 2 新增测试

| 测试 | 文件 | 内容 |
| --- | --- | --- |
| `PLAY_V14_E1_ShopCardHeightKeepsEveryShortTextComplete` | PlayMode/V14ShopPlayModeTests.cs | E1 实测：两套风格 × 100%/150% × 全部 150 张卡 × 两种价格按钮，取用卡高下短文完整、100% 卡名不缩；报告最小可行卡高与最小插画（SHOP_LAYOUT 2） |
| `ShopDieTitleTests`（5 项） | EditMode/Presentation/ShopDieTitleTests.cs | SH-07：5.2 的例子、七种红面数的词、重复取较大点数、重名两条规则与补定规则、阈值取自配置 |
| `ShopPaletteSourceTests.SH09_ShopFilesUseOnlyThePalette` | EditMode/Presentation/ShopPaletteSourceTests.cs | SH-09：商店五个文件里没有 `UiKit.Hex`、`Color32` 与常量 `new Color`（只允许“色板颜色换透明度”） |
| `PLAY_V14_SH01_SH06_ShopPageInBothThemesAtTwoTextSizes` | PlayMode/V14ShopPlayModeTests.cs | SH-01（分区矩形、不重叠、不出屏、不滚动、150% 只翻“升级与服务”）、SH-02/03（文字完整、在自己的框里、不被后画的盖住）、SH-04（全屏只有“下一关”是主色）、SH-05（对比度）、SH-06（按钮与骰子 ≥ 60 单位）；常见店与满店 × 默认与瑞士 × 100%/150%，满店 150% 再查第二页；R4：满店 120 帧的帧时间 |
| `PLAY_V14_SH04_NextStageWithLootLeftAndWithAPendingSave` | 同上 | SH-04 与首领战利品：一件未领（规则拦截的防御状态）时“下一关”停用、写“先领取首领战利品”、不是主色；领了一件后可用，第二行“还有 2 件战利品未领，离开后作废”且量了对比度；存档挂起时停用、不写原因 |
| `PLAY_V14_SH08_FacePickOnTheTableStepByStep` | 同上 | SH-08：S1–S3 每一步与每一种取消（两套风格，S1–S3 在 100%/150% 截图并查文字）；取消不扣钱、不存档、不抽随机数；确认只发一条命令；U06（面）、U05（整颗）、O04（删除）三类的文字 |
| `PLAY_V14_SH08_IllegalTargetIsNamedAndBlocksTheConfirmation` | 同上 | SH-08：已镀金的面写“这一面已经镀金，换一个面”，确认不可点；`CancelFacePick14`（Esc）回到 S0、状态不变 |
| `PLAY_V14_SH08_LongestChangeTextsFitWithEnlargedText` | 同上 | SH-08 的最长文字：带纹理、黄金与权重的面，U01 的改动与对面补充行、U11 的不合法原因；两套风格 × 100%/150%：文字完整、不被按钮盖住，按钮都在面板内（`W14ButtonsInside`，`PLAY_V14_SH08_FacePickOnTheTableStepByStep` 的 S1–S3 也加了这项） |
| `PLAY_V14_SH10_SameShopSaleCapIsShownOnTheTileAndInTheDetails` | 同上 | SH-10（C98）：三张砍价高手把 4 金币的卡压到 1；本店买入后卖价 1，图块悬停与详情都写明原因；卖出 +1 |
| `PLAY_V14_SH12_TopBarTargetIncludesTheBossModifier` | 同上 | SH-12（R5）：顶栏标题“商店 · 第 3 关前”，下一关的目标含首领减益，与“查看本章”同一个数 |

## 2026-11-23 货架、详情与骰面改造

三项 `PLAY_V24_TX06_` 从货架悬停、在售详情、持有道具、新骰旋转预览和免费领取入口读取实绘文字，覆盖卡牌、道具、服务、骰面改造、购买骰与赠礼骰。对照批准的短文、非空细则及空细则条目，保留当前价格、牌型等级和倍率。商店新骰的 O03 购入条件只在付费候选详情显示。骰面改造的原回归改为核对“改造前”“改造后”“权重”，继续检查变化的面值、材质、权重以及屏幕上的位置。实机运行结果记入逐项任务表。[内容附录逐项核对](../../../artifacts/cache/v11/w25/tx06-review/逐行核对.md) · [验收记录](../../tasks/P0-V11.md)

0272352e 在 720p 的三项购领文案与两项改造演出均已通过。内容附录另有两条正常路径在此次修复以后补核，分别为章卡战利品的悬停细则和骰面详情中实际参与的计分来源。其他追加回归检查钱包与通用购买收据中的真实金额，以及在两档字号实际悬停 C07 和 O08 能读完长文并继续点击商品。[报告](../../../evidence/v11/unity/nunit-20261123-123259.xml) · [货架长提示依据](../../../artifacts/cache/v11/w25/tx07-tooltip/README.md)

TX-06 另有六项批量用例覆盖附录正常入口。一次窗口内依次显示全部卡牌、道具与服务、改造、首领减益、战利品和保留名称，留下每条原文、屏幕实际字形以及滚动位置的记录。各条对照独立的附录清单。结果随每次实际输入记录，逐次失败时保留已完成的行。[内容清单](../../../artifacts/cache/v11/w25/tx06-review/rows.json)

TX-07 的道具与商店改动使用原附录批准句式。不同界面的简短角标与完整购买状态分别核对；普通购买收据取真实钱包差额。“全部 32 张卡牌”在调试持有区的两项既有用例中继续核查实际按钮、单行和点击区。对局留池统计仍使用真实留池骰，新增计数单位，并按 TMP 的实际宽度保留完整内容。[界面附录台账](../../../artifacts/cache/v11/w25/tx07-review/ledger.md)
