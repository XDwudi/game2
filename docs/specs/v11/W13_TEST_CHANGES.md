# W13 测试改写记录（卡面模板）

| 项 | 内容 |
| --- | --- |
| 任务 | DEV_PLAN §5.2 W13：卡面模板（[CARD_TEMPLATE](CARD_TEMPLATE.md)） |
| 基线 | `codex/v1.1` 的 `ad3a9dc9`（W11c 验收后）；紧凑图标由 `da22f5a7` 合入 |
| 原则 | 不删除测试、不降低阈值。旧断言钉住 W11 卡面的元素（稀有度字样、刻度、状态行文字）时，改成对模板表达同一意图的断言，并写明理由 |

## 1 改写的断言

改写都在 `DicePrototype/Assets/Game/Tests/PlayMode/` 内，代码里的对应位置写有 `V1.1 (W13)` 注释。

| 测试（文件） | 旧期望 | 新期望 | 理由 |
| --- | --- | --- | --- |
| `CardCheck07`（Art07CardReadabilityPlayModeTests.cs；用于 `PLAY_ART07_CardReadability_ThreeFonts…`、`PLAY_ART07_GuideLayoutManualReopenAtMaximumCapacity…`，以及新增的 `PLAY_V13_CT04_…`） | 桌上的卡（`hud-*`）必须有稀有度字样 `RarityLabel`（常见/进阶/稀有），有几何刻度 `RarityTicks`（1–3 个）；任何文字不压插画 | 桌上的卡不得显示稀有度字样；稀有度由宝石 `RarityGem13`（贴图 `ui_gem_common/advanced/rare` 与稀有度一致、有尺寸）和同色的边框 `RarityFrame13` 表达，宝石代替刻度参加“文字不压稀有度标记”的检查；数值角标的文字 `State` 可以压在插画上（其余文字仍不得压插画）。详情卡、商店卡与已拥有的小卡不变（仍要字样与刻度） | CARD_TEMPLATE §3（字样离开卡面，悬停与详情保留）、§4（数值角标在插画左上）。宝石形状就是新的几何稀有度标记 |
| `TableCardFooter11`（同上） | 桌上卡的状态行文字等于 `CardExpectedFooter07`；“查看条件”时没有状态行 | 期望值改为数值角标的文字：`当前 ×N`→`×N`、`当前 +N`→`+N`、`当前红/当前蓝`→`当前 红/当前 蓝`；“查看条件”和固定特性“随机倍率”没有角标；其余（`本手可用`、`→卡名`、`售价 N`、警示）不变 | CARD_TEMPLATE §4 与第 9 节第 4 行 |
| `PLAY_V09_AltarVictimIsMarkedInTheShopAndNamedBeforeTheStageStarts`、`PLAY_V09_HandScreenShowsGrowthPoolSummaryAndTheNewHandbookPage`（V09PlayModeTests.cs，`CardState09` 的三处期望） | `当前 ×1.4`、`当前 +24`、`当前 ×1.4`、`当前 +26` | `×1.4`、`+24`、`×1.4`、`+26` | 状态行变成插画上的数值角标，去掉“当前 ”（§4 例：`×1.5`、`+12`）；数值本身（祭坛 +0.4、成长 24→26）的断言不变 |
| `PLAY_ART07_CardInformationPreservesPricesAndGeometricRarity`（Art07InformationPlayModeTests.cs 第 132 行） | 桌上卡有 1–3 个 `RarityTicks` | 桌上卡的宝石贴图是该卡稀有度的形状 | 同第一行；同一测试对详情卡刻度的断言（第 150 行）不变 |
| `W11bContrastPass`（V11PlayScreenW11bPlayModeTests.cs） | 测量整个画布 | 增加可选参数 `root`：给出时只测这个节点下的文字；原有调用不传，行为不变 | 画廊盖在对局画面上，CT-02 只测画廊 |

## 2 新增测试 `Tests/PlayMode/V13CardTemplatePlayModeTests.cs`

| 测试 | 内容 |
| --- | --- |
| `PLAY_V13_CT01_CT02_CardGalleryInBothThemesAtTwoTextSizes` | 默认与瑞士 × 100%、150% × 画廊 6 页（商店两页、桌面、两行卡、文字卡、图标审查）：每段文字完整、不截断、在自己的框内、在所在表面的可见边框内，且没有被之后画的实底图片盖住（`W13GlyphCheck`，即 W11GlyphCheck 的检查加遮挡检查，整页收集）；商店与桌面尺寸每张卡显示完整短文；短文与数值角标 ≥19.5 单位；同一张卡的文字两两不重叠、不压附魔角标、不压插画（数值角标除外）；数值角标不压附魔角标；稀有度 = 边框颜色（默认三色为 CP2-3 的 B8C9C5/55B6D9/AD92DB，失效时乘 62%）+ 宝石形状；商品是 Ghost 边框、无宝石、有类别；失效卡压暗并写“本关失效”；价格按钮不是强调色，只在写“购买”时可点。每页截图；CT-02 对比度 ≥4.5:1（`W11bContrastPass` 限定画廊） |
| `PLAY_V13_CT04_TenCardRowAndPlayScreenStatesInBothThemes` | 两套风格：十张卡的两行卡排（每张卡过 `CardCheck07`；成长卡角标为 `+24`）、状态 a、b3、c、f 截图；720p 另有 150% 的十张卡、a、f |
| `PLAY_V13_CompactIconsResolveThemeThenDefaultThenIllustration` | 默认图标 184 张与卡牌、道具、服务、骰面改造一一对应，瑞士 7 张；全部 160×160、有 mipmap、三线性；查找顺序当前风格 → 默认 → 整张插画；按（风格，id）缓存，`ApplyTheme11` 清空；两行卡实际画的是这些图标 |

## 3 Python 测试

| 测试 | 改动 | 理由 |
| --- | --- | --- |
| `tests/test_art_v11_swiss.py` `test_exact_referenced_inventory` | 引用的贴图 49 → 53，整套构建的 PNG 53 → 57 | 风格契约加入 W13 的稀有度边框与三颗宝石（CARD_TEMPLATE §6） |
| 同上 `NEUTRAL_NAMES` | 加入 `ui_card_frame_rarity`、`ui_gem_common/advanced/rare` | 这四张在运行时按稀有度色（或 Ghost）着色，必须是中性色 |
| 同上 `test_shipped_assets_match_regeneration`（`ArtSource/Art11/swiss/contact_sheet.png`） | 未改；在主 agent 重新生成该图之前失败 | 整套构建的审查图现在包含四张新贴图；`ArtSource/` 不在本任务可写范围。重新生成的图见 `artifacts/cache/v11/w13/swiss-contact-sheet-regenerated.png`（整套构建的其余 56 个文件与仓库逐字节相同） |
| 新增 `tests/test_art_v11_default.py` | 默认风格 8 张贴图：重新绘制与仓库像素一致；`BORDERS` 与 `Editor/V11Setup.cs` 的边框一致；新四张是可着色的中性形状（墨线外框除外）；边框中心透明、色带在九宫格边框内；三颗宝石不切片、四角透明、轮廓两两不同 | 默认风格构建脚本此前没有测试；“边框镜像到 Editor 设置”是本任务的要求 |

## 4 运行结果

（全量运行完成后填写。）
