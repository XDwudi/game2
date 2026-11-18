# W20X 报告：风格接口补充

| 项 | 内容 |
| --- | --- |
| 任务 | [DEV_PLAN](DEV_PLAN.md) W20X（协调者 2026-11-17，爆彩漫画复核时加） |
| 接口 | [THEME_SPEC](THEME_SPEC.md) §2、§3、§3.1（`8bb0e075`） |
| 输入版本 | `codex/v1.1` 的 `43d90547`；爆彩漫画由 `e23a6209` 并入，Unity .meta 在 `a6c04b00` |
| 实施 | W20X 子 agent；Unity 6000.6.7f1，Windows 10，主检出（与 artgen 批量共用，Unity 运行时它自动暂停） |
| 状态 | VERIFYING，交协调者复核。实施者不签署质量结论 |
| 测试改动 | [W20X_TEST_CHANGES](W20X_TEST_CHANGES.md) |

## 0 摘要

- 第 0 项：窗口测试不再受真实鼠标位置影响。原因是 UI 输入模块的 `Point` 动作带初始状态检查，每次加载场景都把停着的光标变成一个指针。测试夹具在加载前停用平台的真实指针设备并把位置归零，卸载后交还；不移动操作系统光标。两条回归测试用真实 Mouse 上排队的状态事件模拟停着的光标，原先失败的两条测试的主体在其下通过；关闭隔离时两条都按 W14 的样子失败。
- 第 1、2 项：色板键 `buttonStep` / `buttonStepPrimary`、`section`、`caption` 与可选的 `Table/page_scene.png` 已实现；颜色在绘制时读 `UiKit`，桌面图随画面在同一次重画里切换。运行时注入的测试风格证明：选关、商店、商店上的详情、整局结束等用 page_scene，对局与结算用 table_scene，四次商店↔对局切换逐帧无闪烁；台阶、分区、标题条、样例格在指定点“画出的颜色 = 贴图 × 乘色”（差 ≤ 2 级）；爆彩漫画用它自己的取值同样通过。
- 第 3 项：在运行的游戏里走 16 个非对局画面，找到 19 处直接画在桌面上的字（主菜单 2、选关 15、整局结束 2；商店与各模态框里没有），清单 `tools/art_v11/table_text.json`，检查器 `tools/art_v11/table_text_contrast.py`。低于 4.5:1：默认 1 处、瑞士 14 处、唐三彩 0 处（按设计稿映射）；处理办法见第 4 节，未实施。
- 第 4 项：写死颜色 18 个文件 50 处（口径见第 5 节）。可写路径内只有按钮台阶需要并适合改成色板键，已改；两处特效固定色与计分爆点确实不随风格变，但改法会改变现有风格的画面，列为提案。
- 第 5 项：默认、瑞士、唐三彩在 1920×1080、1280×720 各 21 个画面改动前后逐像素比较；所有非零差都落在两次基线之间测到的噪声区里（界面骰子层的 3D 骰子，≤ 2 级）。
- 第 6 项：见第 7 节。
- 重要发现（未改，提案）：风格的 `Table/*.png` 被导入成 2048×1024（`nPOTScale` 为 ToNearest），毡面着色器按导入后 2:1 的比例摆图，把 1920×1080 的构图压到 960 行，上下各露约 60 行“毡色 × 0.24”的暗带。瑞士、唐三彩、爆彩漫画现在都是这样，§3.1 的安静区与界面因此错位（第 9.1 节）。

## 1 提交

| 提交 | 内容 | 文件 |
| --- | --- | --- |
| `a6c04b00` | chore(v1.1): Unity metas for the comic theme | `Resources/Themes/comic.meta` 与 `comic/**` 的 60 个 .meta（合计 61） |
| `b2539e14` | test(v1.1 W20X item 0): windowed PlayMode no longer depends on the OS cursor | `Tests/PlayMode/GameplayPlayModeTests.cs`、`Art07ResultRowsPlayModeTests.cs`、`V13CardTemplatePlayModeTests.cs`、`W20XPointerIsolationPlayModeTests.cs`（+ .meta）、`docs/specs/v11/W20X_TEST_CHANGES.md` |
| `b6aa974a` | test(v1.1 W20X item 5): identity captures of every screen in four styles | `Tests/PlayMode/W20XIdentityCapturePlayModeTests.cs`（+ .meta） |
| `33d6d926` | feat(v1.1 W20X items 1-2): palette keys for step, section, caption; page_scene off the play layout | 见第 1.1 节 |
| `9d82b110` | feat(v1.1 W20X item 3): the texts the non-play screens draw on the table, with a contrast checker | 见第 1.1 节 |
| 本文件所在提交 | docs(v1.1 W20X): report, test changes, THEME_SPEC status, STATE | 本报告、测试改动表、THEME_SPEC（第 8 节）、STATE 的 W20X 段与文件锁行 |

### 1.1 改动的文件

| 文件 | 改动 |
| --- | --- |
| `UI/Theme/ThemePalette.cs` | 新字段 `buttonStep`（缺省 8F453A）、`buttonStepPrimary`（B56B29）、`section`、`caption`（缺省未设置，alpha 0）；`Hexes` 与 `From` 读这四个键 |
| `UI/UiKit.cs` | `ButtonStep`、`ButtonStepPrimary`、`Section(fallback)`、`Caption(fallback)`；`UiKit.Button` 的台阶读 `ButtonStep(Primary)` |
| `UI/GameApp.Shop14.cs` | 分区底板（四个分区与“我的构筑”）乘 `Section(Panel)`，标题条乘 `Caption(PanelLight)` |
| `UI/GameApp.Art073StyleLegend.cs` | 骰子样式页的八个样例格乘 `Section(白)`（未设置时不乘色，与以前相同） |
| `UI/GameApp.Play11.cs` | 骰池标签乘 `Caption(Panel)`；`ApplyTableMood11` 开头调用桌面图切换 |
| `UI/Theme/GameTheme.cs` | 懒加载 `Table/page_scene`（`PageScene`）；测试入口 `CreateForTests`、`RegisterForTests`、`UnregisterForTests` |
| `UI/GameApp.Theme11.cs` | `PlayBackdrop11`（哪些画面是对局版面）、`ApplyTablePicture11`（换风格与每次重画时选 table_scene 或 page_scene）、毡面渲染器缓存 |
| `UI/GameApp.Card11Gallery.cs` | 卡面画廊打开 / 关闭时按非对局画面切换桌面图 |
| `Editor/V11ThemeImport.cs` | **未改**：`Table/` 下除 `felt_tile` 外都走同一条规则，page_scene 已与 table_scene 设置相同（爆彩漫画两份 .meta 除 guid 外逐行相同，EditMode 测试核对） |
| 测试 | `Tests/EditMode/Presentation/ThemeInterfaceW20XTests.cs`，`Tests/PlayMode/W20XThemeInterfacePlayModeTests.cs`、`W20XTableTextPlayModeTests.cs` |
| 工具 | `tools/art_v11/table_text_contrast.py`、`tools/art_v11/table_text.json`、`tests/test_table_text_contrast.py` |

## 2 验收逐项

| ID | 方法与场景 | 结果 | 证据 |
| --- | --- | --- | --- |
| W20X-00 | 两条回归测试：真实 Mouse 上每次更新排一个状态事件，停在被悬停的元素上（物品测试另停在第一张卡上，W14 的失败方式），跑原失败测试的主体；正向对照解除隔离时同一事件命中元素；反向对照用 `DICECRAFT_W20X_POINTER_ISOLATION=off` 关闭隔离 | 隔离开启 14/14 通过（含 20 秒空闲资源诊断、注入键盘的四条、自己驱动指针的四条）；关闭时 0/2，画廊重现 W14 的四个失败标签 | `evidence/v11/unity/nunit-20261117-232630`（开启）、`-232035`、`-232310`（关闭）；W20X_TEST_CHANGES §1 |
| W20X-01 | EditMode `ThemeInterfaceW20XTests`：解析、缺省值等于改动前、未知键与无效值忽略、瑞士与唐三彩不含新键 | 5/5 通过（W20X-02 的导入设置核对同在此类） | 第 7 节的 EditMode 全量 |
| W20X-02 | PlayMode `PLAY_W20X_02`：运行时注入红色 table_scene、蓝色 page_scene 的测试风格，逐画面核对毡面贴图与 (6, 540) 处像素；全白、全黑乘色副本对比验证“贴图 × 乘色”；爆彩漫画同样；默认、瑞士、唐三彩的回退；四次商店↔对局切换逐帧 | 通过，1920×1080 与 1280×720（两次全量里都通过） | 报告 `artifacts/cache/v11/w20x/<分辨率>/w20x-02/w20x-02.txt`；数字见第 2.1 节；`nunit-20261118-005050`、`-012448` |
| W20X-03 | PlayMode `PLAY_W20X_03` 找出桌面上的字并写清单；检查器对默认、瑞士、唐三彩（与爆彩漫画）出数 | 清单 19 处（720p 写出的清单与之相同）；检查器在四套风格上都跑通（退出码 0），低于 4.5 的分别 1 / 14 / 0 / 14 处；处理办法见第 4 节 | `tools/art_v11/table_text.json`；`artifacts/cache/v11/w20x/table-text-<风格>-<映射>.json` |
| W20X-04 | 全量搜索写死颜色，逐处处理 | 第 5 节；可写路径内转换 1 组（台阶），其余保持或提案 | 第 5 节 |
| W20X-05 | 改动前两次、改动后一次（720p 两次）截图，逐张比较最大通道差与变化像素数 | 默认、瑞士、唐三彩全部非零差都在两次基线之间的噪声区内 | 第 6 节；`artifacts/cache/v11/w20x/<分辨率>/captures/` |
| W20X-06 | 最终提交上 EditMode、PlayMode 1080p 与 720p、Python 全量；`quality_check.py nunit` | 见第 7 节 | 第 7 节 |

### 2.1 W20X-02 的数字（1920×1080）

桌面（测试风格：table_scene 红 210,40,40，page_scene 蓝 40,60,210；(6, 540) 处的毡面像素，模态框的蒙版下变暗但色相不变）：

| 画面 | 桌面 | 像素 |
| --- | --- | --- |
| 主菜单 | page | 40,60,210 |
| 主菜单上的设置 | page | 31,49,174 |
| 选关 | page | 40,60,210 |
| 对局 | table | 210,40,40 |
| 对局上的卡牌详情 / 手册 | table | 173,33,33 |
| 卡面画廊 | page（画廊的底遮住桌面，只核对贴图） | — |
| 关卡结算 | table | 173,31,31 |
| 商店 | page | 40,60,210 |
| 商店上的卡牌详情 / 手册 / 骰子样式 | page | 31,49,174 |
| 整局结束 | page | 40,60,210 |

乘色（测试风格的乘色；“推算”= 全黑与全白乘色副本两点之间按乘色线性插值，即贴图 × 乘色）：

| 点 | 乘色 | 画出 | 推算 | 差 |
| --- | --- | --- | --- | --- |
| 整局结束 `Pause` 的台阶（buttonStep） | 2E8B57 | 46,139,86 | 46,139,87 | 1 |
| 整局结束 `Again` 的台阶（buttonStepPrimary） | 7A3DB8 | 122,61,184 | 122,61,184 | 0 |
| 骰子样式页 `StyleCell0`（section） | 3A5FB0 | 5,16,28 | 4,16,28 | 1 |
| 对局 `PoolTab11`（caption） | C8A23A | 200,162,59 | 200,162,58 | 1 |
| 商店 `ControllerShelf` / `Build14`（section） | 3A5FB0 | 6,15,67 | 5,16,67 | 1 |
| 商店两个分区标题条（caption） | C8A23A | 200,162,59 | 200,162,58 | 1 |
| 爆彩漫画：分区、样例格（section FFFFFF） | FFFFFF | 4,58,128 | 4,58,128 | 0 |
| 爆彩漫画：标题条、骰池标签（caption FBDC0F） | FBDC0F | 251,220,13 | 251,220,15 | 2 |
| 爆彩漫画：台阶（0A080A） | 0A080A | 0,0,0 | — | 漫画按钮贴图露出的台阶带全是黑色墨线，任何乘色都画成黑色；只核对乘色值 |

默认、瑞士、唐三彩：分区底板 = Panel、标题条 = PanelLight、台阶 = 8F453A / B56B29、样例格不乘色，商店仍是 table_scene（或默认场景自己的桌面图）。

## 3 背景表

规则（`GameApp.PlayBackdrop11`）：对局画面（`HandReady`、`Selecting`，含练习）与画着对局版面的关卡结算（`StageSettlement`）用 `table_scene`；其余画面用风格的 `page_scene`，没有 `page_scene` 时仍用 `table_scene`（默认、瑞士、唐三彩都没有，所以它们每个画面都和以前一样）。模态框不改桌面，跟随其下的画面：对局上打开的卡牌详情、手册仍是对局桌面，商店上打开的仍是 page_scene。桌面在重画画面的同一次 `Refresh` 里切换（`ApplyTableMood11` 开头），所以与画面同帧。

| 画面 | 桌面 | 核对 |
| --- | --- | --- |
| 启动加载 | page_scene | 代码路径（加载画面在 `Awake` 时按“非对局”设好，未单独截取） |
| 主菜单，及其上的设置、新建挑战、手册 | page_scene | W20X-02：主菜单、主菜单上的设置 |
| 选关（`StagePreview`） | page_scene | W20X-02 |
| 对局（`HandReady` / `Selecting`）、计分演出 | table_scene | W20X-02：对局；逐帧：选关→对局、出牌计分 |
| 对局上的模态框（卡牌详情、手册、暂停与设置、查看骰子、骰子样式、本章、上一手明细） | table_scene | W20X-02：对局上的卡牌详情、手册 |
| 关卡结算（`StageSettlement`，结算背景是对局版面的快照） | table_scene | W20X-02；逐帧：对局→结算 |
| 商店（含桌上选面 S1–S3） | page_scene | W20X-02；逐帧：结算→商店、商店→选关 |
| 商店上的模态框（商品详情、本章、首领战利品 / 赠礼、手册、设置、骰子样式） | page_scene | W20X-02：商店上的卡牌详情、手册、骰子样式 |
| 整局结束（`RunWon` / `RunFailed`）及其上的模态框 | page_scene | W20X-02：整局结束 |
| 卡面画廊（调试页，`ShowCardGallery13`） | page_scene | W20X-02（画廊的不透明底遮住桌面，只核对贴图） |

## 4 直接画在桌面上的字（W20X-03）

找法：`PLAY_W20X_03` 在四套风格里走 16 个非对局画面（主菜单及其上的设置、手册、新建挑战；选关；整局胜利、失败；常见与满货商店；商店上的商品详情、本章、手册、骰子样式；桌上选面 S1–S3）。一段字背后没有不透明的界面件（W11b 的底面规则：先于它画出、含其字形中心且覆盖一半以上字形的不透明图）就算在桌面上；模态框状态只看模态框自己的字。清单记录画面、对象路径、字形墨迹的矩形（1920×1080 的设计单位，左上为原点）、色板键、各风格实际画出的颜色、其间的透明蒙版。

检查器：`python tools/art_v11/table_text_contrast.py <风格> [--mapping authored|drawn] [--strict]`。对每段字，取该风格 page_scene（没有时 table_scene；默认风格是场景自己的 `bg_table_scene.png`，1672×941，按 16:9 拉满）在矩形内的每个像素，算字色对它的 WCAG 对比度，报最差值。`authored` 按设计稿把图铺满 1920×1080（规格的定义）；`drawn` 按毡面着色器现在的实际画法（第 9.1 节的挤压与暗带）。

| # | 画面 | 对象 | 字 | 矩形 x, y, w, h | 色板键 | 默认 | 瑞士 | 唐三彩 | 爆彩漫画（page_scene） |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | menu | `Screen/MenuRules` | 投掷  →  选择  →  锁定  →  提交 | 484.5, 397, 287, 20.5 | paper | 7.10 / 7.07 | **1.00** / **1.00** | 9.56 / 9.56 | **3.31** / **3.31** |
| 2 | menu | `Screen/LockHint` | 确认锁定后，本手不可直接解除。 | 508, 1009, 230, 16 | muted | 7.13 / 7.19 | **1.55** / **1.02** | 6.58 / **1.00** | **2.14** / **2.14** |
| 3 | select | `Screen/ControllerEmpty0/Plus` | + | 52, 211.5, 16, 16.5 | muted | **1.00** / **1.60** | 在底面上 | 在底面上 | 在底面上 |
| 4 | select | `Screen/ControllerEmpty1/Plus` | + | 120, 211.5, 16, 16.5 | muted | 6.34 / 6.40 | 在底面上 | 在底面上 | 在底面上 |
| 5 | select | `Screen/ControllerEmpty2/Plus` | + | 188, 211.5, 16, 16.5 | muted | 5.87 / 5.95 | 在底面上 | 在底面上 | 在底面上 |
| 6 | select | `Screen/ControllerEmpty3/Plus` | + | 256, 211.5, 16, 16.5 | muted | 5.93 / 5.89 | 在底面上 | 在底面上 | 在底面上 |
| 7 | select | `Screen/ControllerEmpty4/Plus` | + | 324, 211.5, 16, 16.5 | muted | 6.14 / 6.22 | 在底面上 | 在底面上 | 在底面上 |
| 8 | select | `Screen/StageChapter0` | 第 1 章 | 276.5, 352.5, 51, 17 | gold | 5.67 / 5.84 | **3.96** / **3.96** | 6.11 / 6.11 | **2.73** / **2.73** |
| 9 | select | `Screen/StageChapter1` | 第 2 章 | 465, 352.5, 50.5, 17 | muted | 5.43 / 5.38 | **1.55** / **1.55** | 6.58 / 6.58 | **2.14** / **2.14** |
| 10 | select | `Screen/StageChapter2` | 第 3 章 | 653, 352.5, 50.5, 17 | muted | 5.42 / 5.40 | **1.55** / **1.55** | 6.58 / 6.58 | **2.14** / **2.14** |
| 11 | select | `Screen/StageChapter3` | 第 4 章 | 841, 352.5, 50.5, 17 | muted | 5.34 / 5.36 | **1.55** / **1.55** | 6.58 / 6.58 | **2.14** / **2.14** |
| 12 | select | `Screen/StageChapter4` | 第 5 章 | 1029, 352.5, 50.5, 17 | muted | 5.41 / 5.42 | **1.55** / **1.55** | 6.58 / 6.58 | **2.14** / **2.14** |
| 13 | select | `Screen/StageChapter5` | 第 6 章 | 1217, 352.5, 50.5, 17 | muted | 5.42 / 5.53 | **1.55** / **1.55** | 6.58 / 6.58 | **2.14** / **2.14** |
| 14 | select | `Screen/StageChapter6` | 第 7 章 | 1405, 352.5, 50.5, 17 | muted | 5.80 / 5.69 | **1.55** / **1.55** | 6.58 / 6.58 | **2.14** / **2.14** |
| 15 | select | `Screen/StageChapter7` | 第 8 章 | 1593, 352.5, 50.5, 17 | muted | 6.58 / 6.52 | **1.55** / **1.55** | 6.58 / 6.58 | **2.14** / **2.14** |
| 16 | select | `Screen/PreviewBudgets` | 投掷 3 次 / 手     提交 3 次 / 关     提交栏 5 格 | 256, 829.5, 492, 28 | muted | 5.60 / 5.52 | **1.55** / **1.55** | 6.58 / 6.58 | **2.14** / **2.14** |
| 17 | select | `Screen/ControllerOrderWarning` | 入关后，控制器顺序固定。 | 256.5, 875.5, 227, 20 | gold | 6.06 / 6.03 | **3.96** / **3.96** | 6.11 / 6.11 | **2.73** / **2.73** |
| 18 | run-won | `Screen/LastDiceTitle` | 最终骰组 · 5 颗 | 1510.5, 177.5, 187, 25.5 | muted | 7.03 / 6.87 | **1.01** / **1.04** | 6.58 / 6.58 | **2.14** / **2.14** |
| 19 | run-failed | `Screen/LastDiceTitle` | 最后有效提交 | 1510.5, 177.5, 155, 25.5 | muted | 7.03 / 6.87 | **1.01** / **1.04** | 6.58 / 6.58 | **2.14** / **2.14** |

格中为“设计稿映射 / 现在实际画法”的最差对比度，粗体低于 4.5:1。“在底面上”：该风格里这段字背后有不透明界面件（瑞士、唐三彩、漫画的空卡位贴图中心是实心的）。爆彩漫画用它的 page_scene。

检查器在默认、瑞士、唐三彩上都正常运行（退出码 0）；`--strict` 时默认（1 处）与瑞士（14 处）返回 1，唐三彩返回 0。

### 4.1 低于 4.5:1 的处理建议（未实施）

| 条目 | 风格 | 建议 |
| --- | --- | --- |
| 3（选关最左空卡位的“+”） | 默认 | 默认的 `ui_slot_inventory` 中心透明，“+”落在桌面图边框的高光上（其余四个 5.9–6.3）。空卡位保留 `Frame` 的填充色作底（垫底板），或把“+”当作桌面标记改用 `onFelt` 色 |
| 1、2（主菜单的规则行与锁定提示） | 瑞士、漫画 | 垫底板：两行放到一条底板（`ui_chip_label` 或分区底板）上。瑞士桌面是白纸，漫画是红色网点页，单靠改字色只能救瑞士（深色字 15:1），救不了漫画 |
| 8–15（选关的“第 N 章”一行） | 瑞士、漫画 | 垫底板：整行章节标签放到一条标题条上（可用 `caption`） |
| 16、17（选关的投掷 / 提交次数与顺序提示） | 瑞士、漫画 | 垫底板：放进选关已有的面板里 |
| 18、19（整局结束的“最终骰组 / 最后有效提交”标题） | 瑞士、漫画 | 垫底板：标题放在一条标题条上，与下面的骰子列成组 |
| 2（主菜单锁定提示） | 唐三彩（只在实际画法下 1.00） | 按设计稿 6.58；实际画法里桌面图被压，底边的亮框线移到这行字下。修好导入（第 9.1 节）即可 |

另一个选择是新增“桌面上的字”色键（如 `onTable`，瑞士填 151618）：对瑞士足够，对漫画的网点页不够。THEME_SPEC §5 原计划“让这些字都落在面板上”，建议统一垫底板，交协调者定。

## 5 写死颜色排查

搜索口径：`DicePrototype/Assets/Game` 下除 `Tests/`、`Editor/`、`Core/` 与 `Runtime/Verification/Validation~/`（不进构建的验证宿主）之外的全部 C#，找 `Hex("…")`、`new Color32(…)` 与分量为常量的 `new Color(…)`（色板颜色换透明度的 `new Color(c.r,c.g,c.b,a)` 不算）。`UI/Theme/ThemePalette.cs` 是色板本身（默认值与“未设置”记号），不列为写死。结果：改动前 18 个文件 50 处（UI 12 个文件 29 处，Runtime 6 个文件 21 处）。协调者 2026-11-17 的计数“13 个文件 33 处”与此不同，差在口径：UI 的 29 处加上 `Art071EnchantCue` 3 处、`Art071ComboEdgeAccents` 1 处正好 33 处（14 个文件）；本表另含 3D 骰子与骰面材质（`DiceTable*.cs` 12 处）、`Art07ScoreBurstLayout` 4 处、`GameApp.cs` 1 处。命名常量 `Color.white / clear / black` 另列在附录 A。

行号为本提交（W20X 改动后）的行号。“可写”指在本任务的可写路径内。

| # | 位置 | 值 | 用途 | 处理 |
| --- | --- | --- | --- | --- |
| 1 | `UI/UiKit.cs:129`（原 119） | `Hex("8F453A")` / `Hex("B56B29")` | `UiKit.Button` 的台阶（次要 / 主按钮） | **已改为色板键** `buttonStep` / `buttonStepPrimary`，缺省值不变（第 1 项；EditMode 与第 5 项截图核对） |
| 2 | `UI/UiKit.cs:133` | `(1,1,1,.35)` | 按钮面顶上的 1 单位高光线 | 保持：中性白光，叠在任何风格的面色上都表示“上沿受光”，不是风格色 |
| 3 | `UI/UiKit.cs:136` | 悬停 ×1.08，按下 `(.84,.88,.88)` | Button 的状态乘数 | 保持：是乘在面色上的明暗系数，不是颜色。注：按下的系数红通道略低，带一点青，按默认风格调的；某风格看起来偏色时可改为中性 .86（会改变现有风格的按下态，属提案） |
| 4 | `UI/UiKit.cs:161` | `(0,0,0,.08)` | `FlatButton` 在没有 `ui_button_ghost` 贴图时的底 | 保持：只在贴图缺失时出现，现有风格都有这张贴图 |
| 5 | `UI/UiKit.cs:204` | `(0,0,0,.18)` | `Inset` 在没有 `ui_info_inset` 贴图时的底 | 保持：同上，后备路径 |
| 6 | `UI/UiKit.cs:220` | `Hex("07191C")` | 进度条的槽（乘 `ui_progress_track`） | 保持（提案）：值等于默认的 `shadow`。改读 `UiKit.Shadow` 时默认不变，瑞士（000000）、唐三彩（140B05）、爆彩漫画（0A080A）会变；或另设可选键（缺省 07191C）。需要协调者定 |
| 7 | `UI/UiKit.cs:252` | `(0,0,0,.01)` | 滚动视口的遮罩图形（不显示） | 保持：不可见 |
| 8 | `UI/Art073DiceLayer.cs:36` | `(0,0,0,0)` | 界面骰子层相机的清屏色 | 保持：透明，不是颜色 |
| 9 | `UI/GameApp.Art07.cs:128`、`UI/GameApp.Play11.cs:477` | `(1,1,1,.38)` | 掷数为 0 时的空杯图标 | 保持：白色乘色只降透明度（淡出），颜色来自贴图 |
| 10 | `UI/GameApp.Art073DiceLibrary.cs:108`、`UI/GameApp.Play11.cs:523` | `(1,1,1,.5)` | 不可用时的“查看骰子”图标 | 保持：同上，淡出 |
| 11 | `UI/GameApp.Card11.cs:84–85` | `DimFace13 (.62,.62,.62)`、`Dim13(c)=c×.62` | 失效卡、已售商品整张压暗 | 保持：压暗系数（乘数），W13/W14 的测试按 0.62 判定 |
| 12 | `UI/GameApp.Card11.cs:181` | `(.55,.55,.55)` | 失效卡的插画压暗 | 保持：压暗系数 |
| 13 | `UI/GameApp.Card11.cs:492` | `(1,1,1,.6)` | 买不起时价格按钮上的金币图标 | 保持：淡出 |
| 14 | `UI/GameApp.Modals.cs:35、131`、`UI/GameApp.Art07NewRun.cs:20` | `(.015,.03,.035,.35)` | 模态框的蒙版 | 保持：近黑 35% 蒙版，压暗其下任何风格的画面都成立。带一点青；若某风格要有色蒙版，可另设可选键 `scrim`（提案） |
| 15 | `UI/GameApp.Modals.cs:409` | `(.015,.03,.035,.3)` | 远程选骰的蒙版 | 保持：同上 |
| 16 | `UI/GameApp.Art07Settlement.cs:55` | `(0,0,0,.35)` | 结算蒙版 | 保持：中性黑蒙版 |
| 17 | `UI/GameApp.Art07Results.cs:98–99` | `Hex("87535A")` | 失败结局纸面上的“印章”框线与字 | 保持（提案）：印在纸色上，四套纸色上的对比度 5.05 / 5.22 / 4.58 / 5.45（默认 / 瑞士 / 唐三彩 / 漫画），都达 4.5。将来深色纸面的风格需要时，改为可选键（缺省 87535A）或 `danger` |
| 18 | `UI/GameApp.V05.cs:67` | `(.08,.18,.18,.12)` | 手册里选中牌型行的底色（纸上） | 保持（提案）：12% 的墨绿薄涂，在别的风格的纸上带一点青；可改为 `ink` 的 12%（默认会有极小变化） |
| 19 | `UI/GameApp.V05.cs:105` | `(0,0,0,.04)` | 手册红组奖励行的底 | 保持：中性薄涂 |
| 20 | `UI/GameApp.V05.cs:216` | `(.42,.25,.10)`（6B4019） | 手册纸面上的小标题 | 保持（提案）：四套纸色上 7.27 / 7.51 / 6.59 / 7.84；将来需要时改为可选键 |
| 21 | `UI/GameApp.V10.cs:207` | `(0,0,0,.06)` | 章节一览未选中行的底 | 保持：中性薄涂 |
| 22 | `Runtime/GameApp.cs:282` | `(0,0,0,0)` | 骰子的透明点击区 | 保持：不可见（不在可写路径） |
| 23 | `Runtime/DiceTable.cs:439、446、451、478` | `.77` 灰、`shade` 灰、`(.96,.985,1)`、`(.96,.99,.95)` | 3D 骰子点凹槽、点的明暗、高光 | 保持：骰点颜色是功能提示，按 CP3-1 各风格不变（不在可写路径） |
| 24 | `Runtime/DiceTable.Style073.cs:127–159`（8 处） | 光滑面的光泽、粗糙面的斑点、黄金的亮 / 暗、闪点 | 骰面的光滑 / 粗糙 / 黄金表现 | 保持：骰面改造的功能提示，与风格无关（不在可写路径） |
| 25 | `Runtime/Presentation/Art071EnchantCue.cs:19–21` | `static readonly` Mint 79C99C、Violet AD92DB、Paper F4E8CB | 附魔提示的特效 | **提案**（不在可写路径）：三个值就是默认的 teal / violet / paper，但存在静态字段里，换风格不变：瑞士、唐三彩、漫画里附魔特效仍是默认配色（THEME_SPEC §5 的疑问，答案是“没有随风格变”）。改为绘制时读 `UiKit.Teal / Violet / Paper` 会改变这三套风格的特效，需协调者决定 |
| 26 | `Runtime/Presentation/Art071ComboEdgeAccents.cs:19` | `static readonly` Gold F3BA58 | 连锁边缘的点缀 | **提案**（同上）：默认的 gold；改读 `UiKit.Gold`（或 `Reward`）会改变瑞士（E80E16）、唐三彩（EBA53F）、漫画（FBDC0F） |
| 27 | `Runtime/Presentation/Art07ScoreBurstLayout.cs:49、102` | 金 F3BA58（.8 / .42 透明）、红 EC655D、蓝 55B6D9 | 计分爆点 | **提案**（同上）：红蓝两色是每套风格都用的骰子红蓝（THEME_SPEC §2 固定），改读 `UiKit.Red / Blue` 现在不改变任何画面；金色改读 `UiKit.Gold` 会改变三套风格 |

结论：可写路径内只有第 1 项需要、也适合改成色板键，已改；其余可写路径内的常量是透明度、压暗系数、后备路径或中性蒙版，保持。第 6、17、18、20 项将来可能需要新键，现在每套风格都可读，列为提案。不在可写路径的第 25–27 项（两处特效固定色与计分爆点）确实不随风格变，改法会改变现有三套风格的画面，交协调者决定。

## 6 逐像素一致（W20X-05）

截图测试 `PLAY_W20X_05`（`b6aa974a`，改动游戏代码之前提交并用来拍基线）：每种风格 21 个画面——主菜单、主菜单上的设置（画面页，有“画面风格”一行）、选关、对局三态（选择中、首领关开手、十骰七卡）、对局上的卡牌详情 / 骰子样式页 / 手册、关卡结算、结算后的商店、整局胜利、常见商店、商店上的商品详情、本章、满货商店、桌上选面 S1–S3、卡面画廊两页。减少动态开启（毡面光带、卡片摆动、浮动都停），结束页的“用时”固定为 754 秒，截图只取决于画出的状态。比较脚本 `artifacts/cache/v11/w20x/pixel_diff.py`，不遮罩任何区域。

基线 a、b 是同一份旧代码（`b6aa974a`）的两次独立运行，“改动后”是改动后代码的独立运行（与基线一样只跑这一条测试；在别的测试之后跑时，动态字体图集的状态不同，文字边缘会有 1 级的差，见第 6.2 节）。

### 6.1 结果（默认、瑞士、唐三彩，各 21 张）

“比较”一列为改动后对基线 a / b；“噪声”一列为同一份代码两次运行之间的差（基线 a 对 b；720p 另有改动后两次运行之间的差）。“最大差 / 像素”为最大通道差（0–255）与变化像素数；列出的是所有非零项，其余 21 × 3 张逐像素相同。

**1920×1080**（基线 `b6aa974a` 两次：`captures/baseline-a`、`baseline-b`；改动后 `captures/after`）

| 截图 | 改动后对 a | 改动后对 b | 噪声：a 对 b | 位置 |
| --- | --- | --- | --- | --- |
| default-08-dice-styles | 0 | 1 / 58 | 1 / 58 | 骰子样式页的 3D 骰子（692–767, 692–694） |
| swiss-08-dice-styles | 2 / 65 | 1 / 3 | 2 / 62 | 同上（692–806, 361–694） |
| sancai-08-dice-styles | 1 / 1 | 2 / 65 | 2 / 66 | 同上（692–828, 361–694） |
| default-18-facepick-s2 | 1 / 3 | 1 / 3 | 0（同一位置在漫画 S1、S3 的 a 对 b 里是 1 / 3） | “我的骰子”里一颗 3D 骰子（1630–1632, 764–768） |
| 其余 59 张 | 0 | 0 | 0 | — |

逐像素核对：改动后对 a、对 b 的全部变化像素都落在同一画面在基线 a 对 b 里变化的区域内（四套风格的并集，外扩 3 像素），落在区域外的像素数为 0，最大差都不超过 2，与噪声相同。

**1280×720**（基线 `captures/baseline-a`、`baseline-b`；改动后 `after`、`after-b`）

| 截图 | 改动后对 a | 改动后对 b | 噪声：a 对 b | 噪声：改动后两次 | 位置 |
| --- | --- | --- | --- | --- | --- |
| default-08-dice-styles | 1 / 2 | 1 / 2 | 0 | 1 / 2 | 骰子样式页右上格的 3D 骰子（1009–1012, 217–219） |
| sancai-08-dice-styles | 0（`after-b` 对 a、b：1 / 2） | 0 | 0 | 1 / 2 | 同一位置 |
| 其余 61 张 | 0 | 0 | 0 | 0 | — |

720p 的两次基线恰好在这两个像素上相同，所以又跑了一次改动后的截图：同一份新代码两次运行在默认、唐三彩、漫画的这一页上都在这两个像素上差 1，说明它是 3D 骰子的逐次噪声，与改动无关。

爆彩漫画（设了新键，并有 page_scene）改动后 19 张变化、2 张（卡面画廊两页，不透明的底遮住桌面）不变，变化正是第 1、2 项要的：非对局画面换成 page_scene，标题条变黄、分区显出贴图本来的蓝色，台阶与骰池标签按漫画取值。前后对照在 `captures/baseline-a/comic-*.png` 与 `captures/after/comic-*.png`。

### 6.2 说明

- 噪声来源只有一种：界面骰子层画的 3D 骰子（骰子样式页的八颗样例骰、商店“我的骰子”里的骰子）在不同运行之间有个别像素差 1–2 级。其余画面——菜单、设置、选关、对局三态（3D 骰子由主相机画，不在噪声里）、结算、商店、选面、画廊、整局结束——两次基线完全相同，改动后也完全相同。
- 截图要在单独的运行里拍。改动后第一次是和 W20X-02、W20X-03 一起跑的（`captures/after-1`），先跑的两条测试往动态中文字体图集里加了字形；那次默认、瑞士、唐三彩有 9 张在噪声区之外多出 1–2 级、1–26 个像素的差，都在文字笔画的边上（例如商店右栏“红蓝各半”、画廊标题、设置页的一个字）。同一份代码单独再跑（`captures/after`）时这 9 张与两次基线逐像素相同，所以这些差来自运行顺序（字体图集的状态），不是代码改动；表中用的是单独运行。全量测试里的截图（`captures/suite-*`）同样受字体图集影响，不用于第 5 项的判定。
- 截图测试开启“减少动态”，并把结束页的“用时”固定为 754 秒；这两项在基线与改动后相同，不掩盖任何区域。150% 文字没有拍。

## 7 全量测试（W20X-06）

源码冻结在 `9d82b110`：此后 `DicePrototype/` 没有再变。之后的提交只动 `DicePrototype/` 之外：协调者的 `df94b0b6`（两条风格清单测试，W20X_TEST_CHANGES §3）、`2cf4fde2`（STATE），以及本任务的文档提交。三份 NUnit 报告的绑定（开始与结束两次快照，各 3242 个文件）与 HEAD `2cf4fde2` 的文件逐个 SHA-256 相同，`DicePrototype/` 相对 HEAD 没有改动（核对脚本 `artifacts/cache/v11/w20x/verify_binding.py`，输出 BINDING_MATCHES_HEAD）。交付前在文档提交 `64024eb3` 上重新核对，结果相同；三份报告的 `quality_check.py nunit` 也重跑，仍是 CHECKS_PASSED。

| 套件 | 结果 | 报告 | `quality_check.py nunit` |
| --- | --- | --- | --- |
| EditMode（headless） | 2432/2432，0 失败 0 跳过 | `evidence/v11/unity/nunit-20261118-002043.xml` | CHECKS_PASSED（`--min-tests 2432`，另要求 5 条 W20X 测试） |
| PlayMode 1920×1080（窗口） | 240/240，0 失败 0 跳过，29.0 分钟 | `evidence/v11/unity/nunit-20261118-005050.xml` | CHECKS_PASSED（`--min-tests 240`，另要求 W20X 的 5 条与原失败的 2 条） |
| PlayMode 1280×720（窗口） | 240/240，0 失败 0 跳过，27.6 分钟 | `evidence/v11/unity/nunit-20261118-012448.xml` | CHECKS_PASSED（同上） |
| Python `unittest discover -s tests` | 334/334，OK（HEAD `2cf4fde2`，626 秒）。在 `9d82b110` 上第一次是 332/334：两条风格清单测试钉住了键数，协调者在 `df94b0b6` 里改了（W20X_TEST_CHANGES §3） | `artifacts/cache/v11/w20x/final-python-2.log` | — |

与 W14 交付时相比：EditMode 2427 → 2432（+5，`ThemeInterfaceW20XTests`）；PlayMode 235 → 240（+5：两条 W20X-00、W20X-02、W20X-03、W20X-05）；Python 319 → 334（+15，`test_table_text_contrast.py`）。没有删除测试、没有降低阈值。

1080p 全量开始时（约 19:22）协调者的 git 自动整理与 `git fsck` 占了约 3 个核心约 1–3 分钟；帧时间相关的测试（如 `PLAY_ART07_FX_FastSourceMarksLowQualityReplayAndSkipReset`）照样通过，未重跑。720p 的 W20X-02 数字与 1080p 相同（`artifacts/cache/v11/w20x/1280x720/w20x-02/w20x-02.txt`）；720p 写出的桌面文字清单与提交的 1080p 清单条目相同、矩形（设计单位）与颜色完全相同。

运行间隔（偏差）：任务说明要求两次全量之间空闲 3–5 分钟。EditMode 全量（19:18:51–19:20:47）结束约 1 分钟后就开始了 1080p 全量（19:21:52–19:50:53），间隔不足；1080p 与 720p（19:57:13–20:24:52）之间约 6 分钟。时间取自三份绑定的开始与结束快照（本机时间 UTC−5；NUnit 文件名用 UTC）。间隔不足与上面的 git 整理一样，只会让与时间相关的测试更容易失败，不会让应当失败的测试通过；1080p 全量 240/240，未重跑。协调者需要时可按规定的间隔重跑。

## 8 THEME_SPEC 的改动

只改了允许的部分（状态行、§2 / §3 的“W20X 起使用”注、§5 中 W20X 解决的条目），共 10 处：

1. 状态行：加“风格接口补充已实现（W20X，2026-11-17，`33d6d926`、`9d82b110`……）”。
2. §2 `buttonStep / buttonStepPrimary` 行的注：“（W20X 起使用，`33d6d926` 实现）”。
3. §2 `section` 行的注：同上，并注明八个样例格实际在骰子样式页（第 9.2 节第 1 条，原措辞未改）。
4. §2 `caption` 行的注：同 2。
5. §2 表后一段：“由代码任务 W20X 实现；实现前写进 theme.json 的这几项不起作用” 改为 “由代码任务 W20X 实现（`33d6d926`，2026-11-17 起生效）”。
6. §3 `Table/page_scene.png` 行的注：加“`33d6d926` 实现；模态框跟随其下的画面，各画面用哪张见 W20X 报告 §3”。
7. §5 第 1 条（字画在桌布上）：补 W20X 的清单与检查器、处理待定。
8. §5 第 2 条（商店分区）：“W20X 已增加 section、caption”。
9. §5 第 3 条（按钮台阶与特效固定色）：台阶已改为键；排查结果与特效色未随风格变、待定。
10. §5 第 4 条（共用对局桌面）：“W20X 已加 page_scene”。

没有新增 §5 条目；第 9.1 节的导入问题请协调者决定是否写入规格。

## 9 偏差、发现与问题

### 9.1 风格桌面图被挤压（发现，未改，提案）

`Editor/V11ThemeImport.cs` 把 `Table/*.png` 导入为 Default 类型并开 mipmap，没有设 `npotScale`，于是用缺省的 ToNearest：1920×1080 的图被缩放成 2048×1024（W20X-02 报告：瑞士、唐三彩、爆彩漫画的 table_scene 与 page_scene 都是 2048×1024）。毡面着色器按 `_TableMap_TexelSize` 的宽高比（2.0）摆图，屏幕是 16:9，于是图被压到 960 行，上下各露约 60 行 `_BaseColor × 0.24`（W20X-05 截图实测：瑞士第 0–60 与 1021–1080 行是 134,134,134；漫画是 1,38,102；画面第 61 行对应图的第 1 行，第 1019 行对应第 1078 行）。默认风格的 `bg_table_scene.png` 由 `V071TableSetup` 导入，`nPOTScale: 0`，不受影响。

影响：THEME_SPEC §3.1 的四个安静区按 1920×1080 画，实际在屏幕上向中间挤了最多约 60 行，与对局界面错位；page_scene 同样被压，上下有暗带。修法：`V11ThemeImport` 对 `Table/` 设 `npotScale = None`（或着色器改用作者的原始比例）。会改变瑞士、唐三彩、爆彩漫画的画面，按任务约定只提案；建议修好后重拍这三套风格的基线。

### 9.2 与 THEME_SPEC 的偏差与解释

1. §2 的 `section` 说“画面风格页的八个样例格”。代码里八个样例格在**骰子样式页**（`GameApp.Art073StyleLegend.cs`，骰库 → “骰子样式说明”），设置里的“画面风格”只有一行开关。按任务说明的文件实现在骰子样式页；规格的措辞请协调者改正。
2. `caption` 只用于骰池标签（`PoolTab11`）。托盘边上另外两个同样式的标签（留池摘要 `PoolSummaryTab11`、骰池翻页 `PoolPageTab11`）仍乘 Panel，规格没有提到它们（问题 4）。
3. 模态框不单独选桌面，跟随其下的画面（任务说明的“对局上的模态框保持对局桌面”的推广）；主菜单、启动加载、整局结束、卡面画廊算作非对局画面（规格写“如商店、选关、卡牌详情等”）。
4. 爆彩漫画的按钮台阶：漫画 `ui_button_bevel` 露在按钮面下的 9 单位整条是黑色墨线（贴图底边第 2–18 像素），`buttonStep 0A080A` 在画面上看不出来，台阶始终画成黑色。这是漫画贴图的选择，不是接口的问题；如希望台阶有颜色，需要贴图在这条带里留出浅色。
5. 测试入口：为不在 Resources 下放测试文件，`GameTheme` / `ThemeRegistry` 增加了只供测试用的公开方法（W20X_TEST_CHANGES §2.1）。
6. 第 0 项改的是测试夹具，没有改 `tools/unity_windowed_tests.ps1`；反向对照用环境变量，脚本会把它传给 Editor。

### 9.3 问题

1. 是否修第 9.1 节的导入（会改变瑞士、唐三彩、漫画三套风格的画面，需重拍基线）？
2. 桌面上的字用垫底板还是新增字色键（第 4.1 节）？
3. 附魔提示、连锁边缘、计分爆点的固定色（第 5 节第 25–27 项，不在我的可写路径）是否改为绘制时读色板？会改变瑞士、唐三彩、漫画的特效颜色。
4. `caption` 是否也用于托盘上的另外两个标签？
5. 进度条槽、失败印章、手册的选中行与小标题（第 5 节第 6、17、18、20 项）要不要预留可选键？现在四套风格都可读。
6. 选关页的“入关后，控制器顺序固定。”还用旧词“控制器”（D2 已改名“卡牌”），不在本任务范围，转告。

### 9.4 没能验证的

- 启动加载画面的桌面只按代码路径确认（按非对局处理），没有单独截图。
- 逐像素比较只在 100% 文字下做；150% 没有拍。
- macOS 与移动端未运行。

## 附录 A 命名颜色常量

`Color.white`（乘色的单位色：贴图原色）、`Color.clear`（不可见的点击区或“无颜色”）、`Color.black`（`LabelOn11` 加深的方向、`Play11Badge` 的轮廓）不是风格色，保持。逐文件行号（本提交）：

| 文件 | 行 |
| --- | --- |
| `Runtime/DiceTable.cs` | white 430、462–465 |
| `Runtime/Presentation/Art071EnchantCue.cs` | white 181、182 |
| `Runtime/Presentation/Art07ScoreBurstLayout.cs` | white 56 |
| `Runtime/Presentation/Art07UpgradeSurface.cs` | white 113 |
| `UI/Art073DieView.cs` | clear 51 |
| `UI/GameApp.Art07.cs` | clear 90、218、329；white 128、231、291、320、424 |
| `UI/GameApp.Art073Dice.cs` | white 56 |
| `UI/GameApp.Art073DiceLibrary.cs` | white 108 |
| `UI/GameApp.Art073HighScore.cs` | white 63 |
| `UI/GameApp.Art073Shop.cs` | clear 26；white 34 |
| `UI/GameApp.Art073StyleLegend.cs` | white 33、35、39、45、47（35 是 `section` 未设置时的单位色） |
| `UI/GameApp.Art07Effects.cs` | white 532 |
| `UI/GameApp.Art07NewRun.cs` | clear 33 |
| `UI/GameApp.Art07Settings.cs` | clear 157 |
| `UI/GameApp.Art07Settlement.cs` | white 112 |
| `UI/GameApp.Art07Toasts.cs` | clear 68 |
| `UI/GameApp.Card11.cs` | clear 139；white 178、195、212、239、492 |
| `UI/GameApp.Controllers.cs` | clear 76 |
| `UI/GameApp.Modals.cs` | clear 199、267、420 |
| `UI/GameApp.Play11.cs` | clear 411、600；white 158、247、469、477、510、523、532、573、661、742 |
| `UI/GameApp.Play11Contrast.cs` | black 148、152、170；white 51、54、97、148、152、170（LabelOn11 的加深 / 提亮方向与缺省填充） |
| `UI/GameApp.Play11Score.cs` | clear 288 |
| `UI/GameApp.Shop.cs` | clear 76；white 62 |
| `UI/GameApp.Shop14.cs` | white 181、253 |
| `UI/GameApp.ShopFacePick14.cs` | clear 345；white 329 |
| `UI/GameApp.V05.cs` | clear 67、92 |
| `UI/GameApp.V10.cs` | clear 116；white 251 |
| `UI/Play11Badge.cs` | black 17；white 17、23、24 |
| `UI/UiKit.cs` | clear 128、133、165、212；white 136、146、171、198、204 |
| `UI/Theme/ThemePalette.cs` | magenta 72（解析失败时的占位，随即丢弃） |
