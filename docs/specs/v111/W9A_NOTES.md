# W9a 备注：Unity 里的集成与首次实跑

工作包 W9a，交接包 [briefs/W9A_UNITY_INTEGRATION.md](briefs/W9A_UNITY_INTEGRATION.md)。起点提交 `be058fe1`，分支 `codex/v1.1.1`。实施者 Claude Opus 5.5（协调者为主会话的 Claude）。本包期间是 Unity 工程唯一的写入者，同一时间只开一个 Editor。

## 0. 进度

| 项 | 状态 | 提交 |
| --- | --- | --- |
| 1 美术入库 | 完成：入库、导入、绑定、裁切表；`tests/test_art_v11_crops.py` 与 `test_art_v11_install.py` 26 条全过（改前 4 条失败）。新美术的实机画面在第 5 项 | 见提交记录 “art(v1.1.1 W9a)” |
| 2 字体图集重烘 | 完成烘焙；`V21TitleFontAssetTests` 在第 3 项跑 | 见提交记录 “fonts(v1.1.1 W9a)” |
| 3 EditMode 全量 | 第 2 次全量 7146/7146 通过（`b4b7fd42`）；收尾时在最终提交上再跑一次 | 见提交记录 “test(v1.1.1 W9a): three save round-trip tests” |
| 4 PlayMode 1080p、720p 全量 | 未开始 | |
| 5 实看画面 | 未开始 | |
| 6 收尾 | 未开始 | |

## 1. 美术入库

### 1.1 复制与清理（`tools/art_v11/install_art.py`）

扩展了现有安装工具，没有手工复制：

- 新增 `--batch <清单>`（`install_batch`）：读 `install_manifests/w8-install.json`，先整批核对再写。每行插画用原有的 `choose` 按清单的版本核对生成记录（`runValid`、版本、归档路径、`jobPath`、内容 id、原图 SHA-256 等于记录），再要求原图 SHA-256 等于清单；图标核对源文件 SHA-256 等于清单；目标路径必须是该主题该 id 的固定位置（默认插画 `Art071/Illustrations/illustration_<小写 id>.png`，五套 `Resources/Themes/<主题>/Art/illustration_<ID>.png`，图标 `Art/Art11/Icons/<主题>/icon_<ID>.png`）；删除清单只接受已移除 id 的插画或图标 PNG 与 `.meta`。任何一项不符整批拒绝（退出码 2），一个文件也不写。复制先写临时文件、核对哈希再替换；`--check` 只核对。
- 整套安装（原有模式）原来要求 `ArtSource/Art11/<主题>/illustrations/` 下的文件夹与配置 id 完全一致；三张已移除卡的文件夹按 W8 的约定作为历史保留，所以加了 `RETIRED_IDS`（C90、C128、C145）：这些文件夹不装、不进清单，其它不在配置里的文件夹仍然整次拒绝。
- 与 W22i 不同的地方：W22i 只用整套模式（五套主题插画与徽记），默认插画与图标不经安装工具（默认插画早就在库里，图标由 `make_crops.py` 烘焙）。本包的默认插画和 24 个图标是 W8 预先烘好、带哈希的成品，所以用批次模式按清单逐字节复制；之后再跑一次 `make_crops.py` 重生成裁切表与全部图标，核对它烘出的 24 个图标与 W8 的逐字节相同（见 1.3）。

运行结果：`--batch … --check`（装之前）`BATCH_CHECK_FAILED W8 120`（48 个文件未装 + 72 个旧文件还在）；`--batch …` `BATCH_INSTALLED W8 files=48 written=48 retired=72 removed=72`；再次 `--check` `BATCH_CHECK_OK W8 files=48 retired=72`；再装一次 `written=0`。整套模式 `INSTALLED written=0 removed=0`，五套清单（`install_manifests/<主题>.json`）各自换成 185 个 id（去掉三张、加上四张，所选版本与 W8 清单相同）；`--check` `INSTALL_CHECK_OK`。

装之前核对过：48 个源文件的 SHA-256 与 W8 清单一致；24 张插画所选版本都等于“带有效记录的最新版本”；72 个待删文件都存在；48 个目标都是新文件。

### 1.2 Unity 导入与绑定

都用批处理（`-batchmode -quit`），日志只抽汇总行：

| 入口 | 结果 |
| --- | --- |
| `V09Setup.Integrate` | `ART09_CONTENT_INTEGRATED illustrations=185 reimported=4`：四张新默认插画按 v0.71 的默认插画规则（精灵、不压缩、上限 2048、无 mipmap）重导入；`Bootstrap.unity` 与 `ArtStyleLab.unity` 的 `GameApp.contentIcons` 按配置顺序重建为 185 张 |
| `V22iArtSetup.Integrate`（内含 `V13CardSetup.BindCompactIcons`） | `V22I_ART_INTEGRATED themed=990 defaultEmblems=7 reimported=0`（5 套 ×（185 插画 + 7 徽记 + 6 战利品），新图在导入时已经由 `V11ThemeImport` 的主题规则设好）；`V13_COMPACT_ICONS_BOUND comic=185 deco=185 default=185 glass=185 sancai=185 swiss=185 reimported=0`（新图标在导入时已由 `V13IconImport` 设好） |

导入设置核对（脚本把每个新 `.meta` 去掉 guid、内部 id、名字后与同目录的 `C01` 比）：48 个里 45 个逐行相同；另 3 个只差精灵的宽高，就是三张非正方形原图：swiss C154（1326×1187）、sancai I13 与 glass I13（1312×1199）。它们按 W22i 的做法处理：导入规则不变（上限 1024），裁切表里记 `imageSize`，图标按方框烘焙（swiss C154、sancai I13 用 `pad` 补边）。按 1024 上限缩放后 swiss C154 是 1024×917，边长不是 4 的倍数，Unity 会保持未压缩（RGBA32），与 W22i 发现 1 的 49 张同类；1312×1199 缩成 1024×936，可以压缩。这一点由 EditMode `W22i_EveryInstalledPictureIsImportedLikeTheSwissTrialImages` 逐张核对并在输出里列出。

悬空引用：删除前从 `be058fe1` 取出 36 个旧 `.meta` 的 guid，在 `Assets` 与 `ProjectSettings` 的场景、Prefab、资源、`.meta`、JSON 里搜。绑定前 24 处（`Bootstrap.unity` 的 `contentIcons` 3 处与 `compactIcons13` 18 处，`ArtStyleLab.unity` 的 `contentIcons` 3 处）；两次绑定之后 0 处。

### 1.3 裁切表与溯源表

- W8 的 15 个覆写方框（`w8-install.json` 的 `cropOverrides`）并进 `tools/art_v11/crops/make_crops.py` 的 `OVERRIDES`，数值与理由逐字相同（脚本核对 0 处不同）。每套覆写数：默认 3、瑞士 3、唐三彩 9、漫画 2、黑金 8、彩绘玻璃 3，都在测试要求的 10 个以内。
- 六套按 W22i 的命令重跑 `make_crops.py --theme <主题> --source <插画目录> --out Resources/IconCrops/<主题>.json --icons-dir Art/Art11/Icons`（没有写审查图，W8 已经做过）：每套 185 条。裁切表的差异只有去掉 C90、C128、C145 三行、加上四行新内容；新加的 24 条与 W8 的裁切记录（`artifacts/cache/v111/art/crops/<主题>.json`）逐字段相同。重烘的 1,110 个图标里，原有 1,086 个与提交里的逐字节相同（git 无改动），新的 24 个与 W8 交付的逐字节相同（批次 `--check` 通过）。
- 溯源：三张已移除卡的历史在 `ArtSource/Art11/subjects.json` 的 `notes` 里（W8 已写），本包没有改。

### 1.4 钉着“184”的断言

全仓搜过 `184`（C#、Python）。改写的见 [TEST_CHANGES.md](TEST_CHANGES.md) W9a 一节：两个 Python 测试文件、EditMode `W22iArtImportTests`、PlayMode `GameplayPlayModeTests`、`W22iArtInstallPlayModeTests`、`V13CardTemplatePlayModeTests`（只是信息文字）。现行内容数从配置数得：卡牌 150、道具 13、服务 11、骰面改造 11，共 185，与 `activeContentCount` 和 `ConfigValidation` 的 185 一致。其余搜到的 `184` 是版式坐标或旧版本工具（`tools/v10_config.py`），不是内容数。

## 2. 字体图集重烘

按 W21 的流程用现有入口 `V21TitleFontSetup.BuildAll`（批处理）：它先跑 `V071FontSetup.BuildBold`（按 `bold_chars.txt` 扩展粗体，保留已有字形的排布并核对常规体图集字节不变），再按 `title_chars.txt` 烘四套标题字体（三彩、漫画、黑金、彩绘玻璃；瑞士用默认字体），每套独立烘两次、比对字形表与图集像素一致后才保存（保留 guid 与子资源的 id）。字表是 W7a、W7b 已重生成的现行文件，本包没有改：标题字表 676 字（含 W7a 的“匙反意绝转钥”与 W7b 的“撤自”），粗体字表 1077 字（含 W7a 的“做偿匙反惩管纪罚钥零”与 W7b 的“悔”）。

日志汇总行：

- `V071_FONT_READY created=False bindingChanged=False required=1077 boldCharacters=1082 atlasCount=1 boldEllipsis=True regularAtlasUnchanged=True`
- `W21_SANCAI chars=661`、`W21_COMIC chars=662`、`W21_DECO chars=661`、`W21_GLASS chars=676`，都是一张 2048² 图集（三彩、黑金缺的 15 个符号、漫画缺的 14 个按 W21 的预期回落到粗体；彩绘玻璃不缺）。

改动的文件：`Game/Fonts/DiceChineseBold.asset`、`Resources/Themes/{sancai,comic,deco,glass}/Fonts/title.asset`。`DiceChineseFallback.asset`、`TMP Settings.asset` 没有变。`V21TitleFontAssetTests`（含 `FW02`、`Q5`）在第 3 项的 EditMode 全量里跑。

## 3. EditMode

入口 `V072BatchTests.RunEditMode`（批处理）。`TestEvidence` 把报告写死在 `evidence/v11/unity/`（发布门禁与 `v11_delivery.py` 也读那里，切换属于协调者的发布步骤，本包没有改）；每次运行后我把 NUnit 报告与绑定文件原样移到 `evidence/v111/unity/`（字节不变，所以绑定文件里的 `report.path` 仍写着 `evidence/v11/unity/…`，以 SHA-256 对应）。V1.1 的 `evidence/v11/unity/` 本来就不进 git，这里同样没有提交报告本身，只在本节记下文件名、提交号与结果。

| 轮次 | 源提交 | 报告 | 总数 | 通过 | 失败 | 跳过 |
| --- | --- | --- | ---: | ---: | ---: | ---: |
| 第 1 次全量 | `cd8a389d` | `nunit-20261126-174727.xml` | 7146 | 7141 | 5 | 0 |
| 修复后只跑相关的（诊断，不算门禁） | 工作区（`cd8a389d` + 三个测试文件的改写） | `nunit-20261126-175052.xml` | 8 | 8 | 0 | 0 |
| 第 2 次全量 | `b4b7fd42` | `nunit-20261126-175846.xml`（17:53:44Z–17:58:45Z） | 7146 | 7146 | 0 | 0 |

第 2 次全量运行期间我新建了 PlayMode 文件 `W9aReviewCapturePlayModeTests.cs`（不在 EditMode 程序集里），所以它的绑定文件“结束时”的快照比“开始时”多这两个文件，其余文件哈希相同。最后收尾时会在最终提交上再跑一次 EditMode 全量。

第 1 次的 5 条失败与归因：

1. `V09PersistenceTests.SAVE_V09_…RealJsonRoundTrip`（a）：按旧规则写的 C55、C70、C58 数值。按 RULES_DELTA 改写，登记在 TEST_CHANGES W9a。
2. `V10PersistenceTests.SAVE_V10_…RealJsonRoundTrip`（a）：C117 现在镀首末两颗、镀金加权 +1、C124 权重 +2。同上。
3. `V10PersistenceTests.SAVE_V11_SameNameCopies…`（a）：C46 现在是加成卡、C55 起步 12、每次成长 +3。同上。
4–5. `PlayerVerificationBindingTests.VERIFY_RecoveryRoleReportBindsActualConfigWithoutReplayInitialization` 的两个用例（环境）：断言“读到的配置哈希等于构建身份里的哈希”。`DicePrototype/Assets/Resources/BuildIdentity.json` 不进 git（只有 `.meta` 被跟踪），由 `tools/v11_delivery.py identity` 在出包前写；本机这份还是 p1.1.0 的（`contentConfigVersion` p1.1.0、旧哈希）。而 `v11_delivery.py identity` 现在写死要求 p1.1.0，不能用来生成。处理：用一个一次性脚本（不进仓库）只刷新这份本机文件里由源文件推出的字段——`sourceCommit`、`contentConfigVersion`（p1.1.1）、`contentConfigSha256`、`saveSchemaVersion`（8）；发布标签 `releaseVersion`（p1.1.0）、`artVersion`（art1.1.0）原样保留，`goldenCaseSetSha256` 也原样保留（它对应的 `Data/GoldenScores.json` 已经不存在）。原文件备份在本次会话的临时目录。**出包前协调者要用切换后的交付工具重写它**（见第 6 节）。测试与运行时代码都没有改。

## 4. PlayMode

入口 `tools/unity_windowed_tests.ps1`（窗口模式，脚本点掉管理员权限对话框）跑 `V072BatchTests.RunPlayMode -v072GameView <宽>x<高>`。报告同样移到 `evidence/v111/unity/`。

| 轮次 | 窗口 | 源提交 | 报告 | 总数 | 通过 | 失败 | 跳过 |
| --- | --- | --- | --- | ---: | ---: | ---: | ---: |
| 第 1 次（编译失败，没有跑） | 1920×1080 | `67b8a440` | 无：本包新文件 `W9aReviewCapturePlayModeTests.cs` 一个变量重名（CS0136），脚本停掉了 Editor。修正提交 `2d2018cc` | — | — | — | — |
| 第 2 次全量 | 1920×1080 | `2d2018cc` | `nunit-20261126-190559.xml`（18:03:58Z–19:05:59Z） | 378 | 336 | 42 | 0 |
| 第 3 次全量 | 1920×1080 | `5c5002ed` | `nunit-20261126-213732.xml`（20:20:10Z–21:37:32Z） | 378 | 376 | 2 | 0 |

第 3 次的 2 条失败：`PLAY_V24_SC16_SE04_Deco`、`PLAY_V24_SC16_SE04_Sancai`，手册“颜色与红组”页滚到底时，全红一行规则文字的第一行正好落在列表上沿，画到了黑金、唐三彩两套较厚的纸面边框上（另外四套边框薄，没有）。先按“行末标点出界”改窄了文字（提交 `35dcf63a`），720p 全量证明不是这个原因，已撤回；(b) 真正的修法：颜色页的列表和“进阶规则”页一样按纸面实际边框留出上下边距（`FrameInsets11`，`GameApp.V05.cs`）。`PLAY_ART07_ResourceDiagnostic_IsolatedRealRouteInventoryAndTwentySecondIdle` 在第 2、第 3 次全量里都通过（没有遇到有人操作电脑的干扰）。

| 轮次 | 窗口 | 源提交 | 报告 | 总数 | 通过 | 失败 | 跳过 |
| --- | --- | --- | --- | ---: | ---: | ---: | ---: |
| 720p 第 1 次全量 | 1280×720 | `35dcf63a` | `nunit-20261126-225514.xml`（21:41:56Z–22:55:14Z） | 378 | 375 | 3 | 0 |

720p 第 1 次的 3 条失败：`PLAY_V24_SC16_SE04_Deco`、`…_Sancai`（同上，修法见上）；`PLAY_W9A_ScoreCardShopAndUnlockScreensOfP111`（本包截图测试：100% 那一轮买下了镀金，150% 那一轮在货架上找不到它；之后又发现选面面板没有用“取消”关掉。改为不买、用 `CancelFacePick14` 关面板）。

看 720p 截图时另发现一处 (b)：150% 字号下撤回的主按钮“撤回 · 钥匙串（剩 3 次）”的字顶到按钮两侧边框（`unlock-by-key-ring-text150.png`），测试没有拦到。改 `UI/GameApp.Interact111.cs`：字比主按钮宽时，按钮向右长进“结束本关”之前的空处，字号不变。

| 诊断轮（不算门禁） | 报告 | 结果 |
| --- | --- | --- |
| 720p：`V24_SC16_SE04_Deco`、`…_Sancai`、`PLAY_W9A`（手册列表修法之后） | `nunit-20261126-230335.xml` | 3 通过、1 失败（`PLAY_W9A_Score…`：选面面板没关，见上） |
| 720p：`PLAY_W9A_Score…`、`PLAY_V111_KeyRing…`、`…FreeUnlock…`、`…BareHands…`（撤回按钮修法与截图测试修法之后） | `nunit-20261126-230833.xml` | 4 通过、0 失败 |

第 2 次的 42 条失败按原因归类（测试名省略前缀 `GameplayPlayModeTests.`）：

| 原因 | 测试 | 归因与处理 |
| --- | --- | --- |
| 玻璃改在提交后碎（RULES_DELTA §7.3），“没有骰子”的结束夹具还在投掷时碎 | `PLAY_ART07_ResultScreensUseRealTerminalCommandsAndPreserveExactReceipts`、`PLAY_V15_SE_ResultsSixThemesAndTerminalCases`、`PLAY_V24_SC16_SE04_{Default,Swiss,Sancai,Comic,Deco,Glass}`、`PLAY_W20X_03_TableTextOnNonPlayScreens`（共 9 条，都走 `TerminalFixture15` 或同样的夹具） | (a)。夹具改成：最后一颗是锁定的玻璃骰，提交（没到目标、还有提交次数）后碎，会话以 `NoDice` 结束（`GameSession.Submission.cs` 第 90–95 行）。新函数 `NoDiceScene111`，两处共用 |
| 重新读档后打开得分明细抛 `Frozen additive mismatch` | `PLAY_ART07_STATUS_ColdLoadDuringSubmissionShowsSavedFinalWithoutReplayingEffects`、`PLAY_V25_C114_PreviousHandRetainsSameNameCardPositionAfterReload`、`PLAY_V25_C114_PreviousHandRetainsSoldCardAfterReload` | (b) 运行时存档适配器的缺陷，玩家可见（读档后点“明细”／上一手报错）。颜色、黄金面的加成条目没有来源卡（`emitterId`、`stateOwnerId` 为 null），规则层解释冻结的一手时核对这一点；`JsonUtility` 把 null 字符串写成 `""`，读回来就对不上。修在 `Runtime/AtomicSaveStore.cs`：读档后把这两个字段的 `""` 还原成 null（与同处已有的“Unity 把 null 的嵌套类写成默认对象”的还原同一做法）。规则层、会话层没有改。另见第 6 节 |
| W7a、W7b 给明细加的“加成”分项行 | `PLAY_ART07_BreakdownEverySourceFitsAndWheelRevealsRealGlyphsFont{100,125,150}`、`PLAY_ART07_ScoreDrawerC53BranchAndHistoricalRowsKeepFrozenExactValuesAndCopy`、`PLAY_ART07_ScoreDrawerThreeFontsMapEverySourceAndWheelReachEveryGlyph` | (a)。期望列表加上 `GlobalStep<i>Bonus<j>`／`ScoreRow_Global<i>Bonus<j>`（抽屉里超过 4 条时默认收起，不加） |
| 新的牌面、数值、目标（p1.1.1） | `PLAY_V09_HandScreenShowsGrowthPoolSummaryAndTheNewHandbookPage`（C70 +9）、`PLAY_V10_BossStageDisablesCardsShowsItsModifierAndPaysLootInTheShop`（2106）、`PLAY_V10_ShopButtonAndStageSelectShowTheThreeStagesOfTheCycle`（目标 320／480）、`PLAY_IndependentTutorialCompletesRealPurchaseAndPreservesFormalRun`（两颗红是二举 ×1，得分 50）、`PLAY_V13_CT04_TenCardRowAndPlayScreenStatesInBothThemes`（角标“基础分 +36”）、`PLAY_TX07_StageEndReceiptsNameExpiredCardAndGrantedCardInTheCountingRecord`（“加成耗尽”）、`PLAY_ART07_POST_NetZeroUsesThinConfirmationWithoutWipeOrMeshSwap`（C36／C37 作用前三颗）、`PLAY_ART07_FX_MultipleRandomOwnersAndSharedCopyUseCompleteCommittedList`（C53 的倍率表里已没有 ×5，改用 ×4）、`PLAY_ART071_COMBO_RealTiersUseAuthoredEdgeRingArcsAndGoldWithoutCoveringInk`（C02×C31 只到第 3 档，第 4 档用 G147 整套 ×2205）、`PLAY_V11_TEXT_HandbookScoringOrderIncludesItemAndBossFactors`（手册里附魔已并入“加成”一步）、`PLAY_V24_TX06_ShopOffersAndOwnedDetailsDisplayApprovedContent` 与 `…DiceShopAndRewardDetailsUseTheirOwnTerms`（C51、U01 的 p1.1.1 文字） | (a)。逐条见 TEST_CHANGES W9a |
| 玻璃不在投掷时碎 | `PLAY_RealKeyboardTenDiceMappingModalSeedFocusAndHeldKeys` | (a)。投掷后 D005 仍在，第三颗可见骰是 D005 |
| 改写规则后会顺带记录逐面的变化 | `PLAY_PreviousSubmissionRetainsOriginalScoreAndPermanentEffectsAfterNextRollAndReload` | (a)。预览里不再只有一条效果（C07 复制骰子时还有逐面的记录）；改为“恰好一条主效果，所有效果都来自这张卡、落在第二颗骰子上”，后面“预览与提交后的效果逐字段相同”的检查不变 |
| 这一手不再超过 3,000 | `PLAY_V11_W11b_LabelsOnFilledSurfacesReachContrastInDefaultAndSwiss`、`PLAY_V11_W11b_SettlePanelTurnsToReachedTheMomentTheTotalReachesTheTarget` | (a)。目标改成这一手实际得分（会话预览）的三分之一，保持“提交前 0、演出中途越过目标” |
| 颜色并入加成、红色一对 ×1，最长的筹码行少了两个筹码 | `PLAY_V11_W11b_FormulaChipRowFitsWithTheLongestNamesAt150` | (a)。把夹具里的加成卡 C30 换成老当益壮 C58（恒定 ×1.25），筹码回到 6 个（基础、牌型、加成、卡牌、道具、首领）；“至少 6 个”的要求没有降 |
| 首领计划随规则改了 | `PLAY_V11_RewardHintsMatchTheActiveStageAndItsCardIncome` | (a)。种子 5000 第 3 关现在是 B01。改成从给定种子往后找第一个真实计划里这一关是所需减益的种子（计划仍由会话抽，不由测试写） |
| p1.1.0 的录制与回放 | `PLAY_V15_SE14_NormalMenuContinuesLiveOrdinaryAndBossReceipts(100)`、`(150)`、`PLAY_V15_SE15_OrdinaryReceiptBlocksSecondPointerAtSamePosition`、`PLAY_V26_ReleaseNavigationSmokeToFirstCommittedMultiplier` | (a)。前三条读 `evidence/v11/balance/trace_p110_5000.json`（p1.1.0），现在读用同一条模拟器命令在 p1.1.1 上生成的 `evidence/v111/balance/trace_p111_5000.json`（`balance_sim trace --seed 5000 --steps 80`：81 步、回放全部接受、得分不符 0；模拟器没有改）。V26 的前九条命令与新录制相同，版本改 p1.1.1，期望的手与得分改由同样九条命令在一个新的 Core 会话上重放得出（原来写死 H0001、×3、198） |
| 一批货架里重复的商品 | `PLAY_V111_TwoFaceUpgradeCostsOnePriceAndAnIllegalFaceBlocksThePair` | (a) W7b 测试的夹具错：同一批里放了两件 U06，会话拒绝（“同批商品定义重复”）。第二件换成货架上没有的另一种按面改造（它只用来验证换骰子会清空已选面） |
| 150% 字号下“再选一个面（价格不变）”压在“确认购买”上 | `PLAY_V14_SH08_LongestChangeTextsFitWithEnlargedText` | (b) W7b 的版式缺陷。改 `UI/GameApp.ShopFacePick14.cs`：150% 字号时这个按钮放到骰子旁边、骰子名字下面（S2 放提示的位置），骰子这一行按需加高，放不下长字时用短字“再选一个面”；100%、125% 仍在价格行下面（原位置）。先试过“压到底部按钮行时移到价格行右端或底部行左端”，在最挤的“光滑骰面”一例里两处都放不下，改成现在的做法 |
| 本包的截图测试 | `PLAY_W9A_NewArtInThePlayRowTheShopAndTheDetailOfEveryStyle` | 本包错误：场景里的 C45 需要已设的颜色（照 `W13TenCards` 补上）；同时给两个截图测试的货架夹具去掉与新增商品重复的生成商品 |

修复后只跑相关测试的诊断轮（1920×1080，不算门禁；报告都在 `evidence/v111/unity/`）：

| 报告 | 范围 | 结果 | 新暴露的问题与处理 |
| --- | --- | --- | --- |
| `nunit-20261126-194948.xml` | 第 2 次全量失败的 42 条所在的 32 个名字片段（55 条） | 43 通过、12 失败 | 同一测试里后面的旧断言（键盘测试提交 D006、V10 第 3 关目标 1,000、TX06 红色浪潮 2 金币、C07 复制后 `cultivated` 标记、效果预览不含“本局最高单手”纪录）都是 (a)；另有两处 (b)：**手册“颜色与红组”页 150% 时全蓝／全红的规则文字溢出**（W7a 改长的句子），**得分明细里“展开加成的 N 项”按钮的字在 150% 时被裁**（W7b 的按钮高 56、字行高约 52，且没有关掉自动缩放）；SH08 的第一版修法不够 |
| `nunit-20261126-200248.xml` | 仍失败的 8 个片段（18 条） | 13 通过、5 失败 | 手册红组说明在 150% 时也溢出（改 `GameApp.V05.cs`：颜色两行的行高 150% 时 220 → 264，红组说明按实际行数加高、下面的表随之下移）；按钮修法第二版 |
| `nunit-20261126-201055.xml` | 5 个片段（9 条） | 7 通过、2 失败 | `…ScoreDrawerThreeFonts…` 这次失败是我在运行中改了截图测试文件，触发了“运行期间源文件不变”的检查（不是产品问题）；SH08 第二版（价格行右端／骰子旁／底部行左端）仍放不下，改成上表的第三版 |
| `nunit-20261126-201538.xml` | `ScoreDrawerThreeFonts`、`V14_SH08`、`V111_TwoFace`、`PLAY_W9A_Score`（6 条） | 6 通过、0 失败 | — |

得分明细的按钮（`UI/GameApp.Art07ScoreDrawer.cs`）：关掉自动缩放、字号固定 23×字号倍数、不换行；按钮宽度按字的实际宽度（至少 420），高度 150% 时按行高加高（56 → 63）。

`W7bShot`（`V111InteractionsPlayModeTests.cs` 的截图步骤）：第 2 次全量产出的 `custom-upgrade-list.png` 里没有自选改造列表（弹窗在同一帧打开，截图时还在淡入）。改为有弹窗时不重画、等 0.5 秒再截。这是截图工具的问题，不影响断言。

## 5. 看图结论

截图来源：本包新增的 `Tests/PlayMode/W9aReviewCapturePlayModeTests.cs`（`artifacts/cache/v111/w9a/<宽>x<高>/`，两个测试：六套主题的新美术；默认主题的 p1.1.1 画面，720p 另存 150% 字号），W7b 的 `V111InteractionsPlayModeTests`（`artifacts/cache/v111/w7b/<宽>x<高>/`），以及全量运行里原有测试顺带产出的截图。截图里的局面多数是调试场景（顶栏不显示调试标记），所以“手册最高 100 级”这类调试上限是调试局的正常显示（正常局读配置 12 级，`HandbookRules.cs` 第 39 行），不算缺陷。我逐张用图片查看；为省篇幅，同一区域在多套主题里的对比是把同一块裁下来拼成一张看的（拼图只在本次会话的临时目录，结论里写的是原图路径）。

### 5.1 新美术（W8-06 D1、D2；只做工程检查，风格由用户定）

1920×1080（`artifacts/cache/v111/w9a/1920x1080/`）：

| 画面 | 截图 | 结论 |
| --- | --- | --- |
| 对局十张卡的紧凑行（C154、C155、C156 与 C24、C08 等同排）与道具栏（钥匙串 3 次） | `art-play-{default,swiss,sancai,comic,deco,glass}.png` | 通过：六套都显示本主题自己的图标，边缘干净，没有导入瑕疵，主体没有被裁切切掉。观察（版式取舍，留给协调者）：C154 的状态角标“本手免费撤回可用”较长，盖住了图标的大半，只露出托盘下部；C156 的“加成 +5 生效中”同样盖住上半。这是 W7a/W7b 统一的角标位置（积少成多、百变模仿等卡相同），不是新美术的问题 |
| 道具栏的钥匙串图标与次数小框 | 同上（右侧道具栏） | 通过：六套都能看清三把钥匙；次数小框压住图标右下角的挂锁一部分（默认、瑞士、黑金较明显），数字清楚。是否挪动小框属于版式取舍 |
| 商店货架（C155、C156 与同主题的利滚利等旧卡并排） | `art-shop-*.png` | 通过：六套插画清晰、边缘干净、与邻卡同一画风。**有缺陷（文字换行，未修，见 5.4）**：C155 的短文字在瑞士、漫画、黑金、彩绘玻璃四套里断成“……得分×2；再 / 含两颗1点改为×4。”，第二行只剩一个“再”字 |
| 我的构筑的小卡（C154、C24、C155、C39、C156）与道具 | `art-shop-*.png` 底部 | 通过：图标可辨。C155 与 C24（我是状元）在小尺寸下靠“碗”区分，能分开；像不像由用户判断 |
| 卡牌详情（C155，六套；C156、I13 默认） | `art-detail-C155-*.png`、`art-detail-C156-default.png`、`art-detail-I13-default.png` | 通过：大图完整、无裁切、无白边或锯齿 |

1280×720 的同一组截图在第 4 节最后一次 720p 全量里产出，结论见 5.3。

### 5.2 p1.1.1 的画面（默认主题，1920×1080）

| 清单项 | 截图 | 结论 |
| --- | --- | --- |
| W7a 1、5：筹码行顺序；成长卡、客串明星、绝地反击、养成卡的角标 | `play-bonus-preview.png` | 通过：“基础分 26 × 豹子 ×12 × 加成 ×12.45 × 卡牌 ×1.45 = 5,632”，顺序与规格一致；角标“加成 +2.2”“加成 +4”“明星点数待入关”（调试场景没有入关，所以没有明星点数）“加成 +5 生效中”“不占栏位”“得分 ×1.45”都放得下、数值与规则相符（1 + 0.25 + 2.2 + 4 + 5 = 12.45） |
| W7a 1：全蓝单颗 | `play-single-blue.png` | 通过：“基础分 10 × 加成 ×1.25 = 12”，出现“加成 ×1.25” |
| W7a 3、4：得分明细、逐步明细 | `score-drawer.png`、`score-breakdown.png` | 公式串通过：“基础 26 × 牌型 12 × 加成 12.45 × 卡牌 1.45 = 5,632（最后去掉小数）”。加成的分项行在列表下方，这两张图没滚到；测试已加“滚到底再截一张”（`score-drawer-end`、`score-breakdown-end`），见 5.3 |
| W7a 7：成长卡、养成卡详情 | `detail-growth-C56.png`、`detail-cultivation-C120.png` | 通过：“当前 加成 +…”、养成卡“最左边的一张养成卡，不占卡牌栏位／已改写 0 个骰面 · 卖价 +0（每改写 4 个面卖价 +1，最多 +6）” |
| W7a 11：返璞归真 | `play-boss-B35.png`、`detail-growth-halved.png` | 通过：深蓝 +2.2 → +1.6、积少成多 +21 → +16、连庄 ×1.45 → ×1.35（成长减半）；详情“首领减益：本关长出的部分只算一半” |
| W7a 10：带玻璃骰提交 | `play-glass-preview.png`、`play-glass-after.png` | 提交后的图通过（碎 1 颗，骰池少一颗）。观察：后置摘要一行先写“本局最高单手：0 → 170”，玻璃那句“1 颗…”被省略号截掉，要点“完整记录”才看到；顺序与一行显示属于取舍，留给协调者。预览图通过：左侧“预计：三条 / 全蓝 · 玻璃 2 颗，提交后每颗有 15% 概率碎裂”分三行，字号没有被缩小；提交栏里两颗玻璃骰呈蓝色半透明并带玻璃小标 |
| W7a 9：手册两页 | `handbook-colors.png`、`handbook-rules.png` | 100% 通过：“加成 +0.25（下一级 +0.75）”与说明一行放得下；进阶规则末尾新增的三行在列表下方，测试已加滚到底的截图。150% 时的溢出由 V24 的检查发现并已修（第 4 节） |
| W7b：按面改造一个面、加选、两个面；脆化整颗 | `facepick-gild-one.png`、`facepick-gild-adding.png`、`facepick-gild-two.png`、`facepick-glass.png` | 100% 通过：“再选一个面（价格不变）”在价格行下、不压底部按钮；加选时已选面金圈、提示“再选一个面，价格不变”、底部“取消”“只改一面”；两个面各一行“改造前 → 改造后”，价格不变（3），买后金币 200 → 197 只扣一次；脆化“计分 ×3；提交后每颗有 15% 概率碎裂，留在骰池里不会碎” |
| W7b：两个面的成交演出 | `upgrade-two-face-transaction.png` | **未覆盖**：0.45 秒后截图时演出已结束（测试环境的演出速度），图里是成交后的商店。顺带确认：标题栏“自选改造 · 还剩 2 次”与“指定升级 · 加价 1”并排不挤；计数行“卡牌栏位 2/5 · 共 3/10 张 · 养成卡“拔苗助长”不占栏位”放得下 |
| W7b：钥匙串详情、撤回按钮 | `keyring-detail.png`、`unlock-by-key-ring.png` | 通过：详情的“用法”一段与按钮“去选要撤回的骰子”放得下（“道具效果”与“用法”内容有重复，属文案取舍）；撤回时提示栏两行“撤回骰子 16：用掉钥匙串 1 次（现在剩 3 次），不能反悔。……”，主按钮“撤回 · 钥匙串（剩 3 次）”、次按钮“取消选择”都放得下。观察：选中的金圈是锁定格外框外再加一圈同色细框，能看出但不醒目（取舍） |
| W7b：自选改造列表（有钱、没钱）、用完后的入口 | `artifacts/cache/v111/w7b/1920x1080/custom-upgrade-list.png`、`custom-upgrade-list-no-gold.png`、`custom-upgrade-used-up.png`（第 3 次全量产出，`W7bShot` 修正之后） | 通过：11 行可滚动，名称与短句一行放得下，第二行“选 1 个或 2 个面，价格相同”／“整颗骰子”，没钱时每行“现在不能买：还差 N 金币”且价格按钮变灰；两笔之后入口变成“本店自选改造已用完”并变灰。第 2 次全量产出的 `custom-upgrade-list.png` 里没有弹窗（截图时弹窗还在淡入），是截图工具的问题，已修（第 4 节） |
| W7b：关末结算页两行纪录、主菜单历史最高 | `settlement-records.png`、`menu-formal-record.png` | 通过：“本局最高单手 192 · 本关新纪录”“最高过关倍数 ×192.0 · 本关新纪录”在“明细”右侧、与按钮同高，不压上面的提示也不出纸面；主菜单“历史最高单手 123,456”在“退出游戏”与“版本与构建”之间 |
| W7b：得分演出三档 | `score-tier-below.png`、`score-tier-one.png`、`score-tier-three.png`、`score-tier-ten.png` | 通过：差 1 分时没有光圈；一档蓝色光圈与“一手达标！”；二档金色光圈与“三倍目标！”；三档多了顶栏皇冠与“十倍目标！”。档位字贴在总分筹码旁，字较小但清楚 |
| W7a 14、15：图鉴第一页、调试面板 | `gallery-p0.png`、`debug-panel.png` | 通过：图鉴角标样例“得分 ×1.5”“基础分 +12”“加成 +2.5”“当前 红”都在框内（只截了第一页，“基础分 +120”那条在后面的页里，未看）；调试面板“独立乘区：基础 34 · 牌型 ×4 · 红组 ×1 · 加成 ×2.85 = 1 + 全蓝 0.25 + 深蓝（第 1 张）1.6 · 卡牌 ×27/20”。成长值输入框不在这一页，未看 |

## 6. 留给协调者决定的事

1. **本机 `Resources/BuildIdentity.json`**（不进 git）：为了让 EditMode 的配置绑定测试在 p1.1.1 下有意义，我只刷新了由源文件推出的字段（提交号、`contentConfigVersion` p1.1.1、配置哈希、`saveSchemaVersion` 8），发布标签 `releaseVersion` p1.1.0、`artVersion` art1.1.0 与 `goldenCaseSetSha256` 原样保留。出包前要用切换到 p1.1.1 的 `tools/v11_delivery.py identity`（或其后继）重写；它现在写死 p1.1.0，而且引用的 `Data/GoldenScores.json` 已不存在。
2. **规则层对“没有来源卡”的判断只认 null**（`Core/Rules/ScoreExplanation.cs` 第 121–122 行 `emitterId == null`）。本包在存档适配器里把读回的 `""` 还原成 null（第 4 节），规则层没有改。是否要让规则层同时接受空字符串（更稳），由协调者定；那属于规则层改动。
3. **`evidence/v111/balance/trace_p111_5000.json`**：V15 两个收据测试与 V26 冒烟用的 p1.1.1 录制，由现有模拟器命令生成（模拟器没有改）。如果发布流程要求录制由协调者生成或另放位置，请指示。

## 7. (c) 类问题（规则或会话层）

## 8. Unity 自动改写并被还原的文件
