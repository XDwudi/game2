# W20 彩绘玻璃 UI kit

状态：**第二阶段完成，提交复核**（2026-11-18，分支 `codex/v1.1-glass`，起点 `codex/v1.1` `cd2fedb3`；第一阶段 `20d995da`，1b `67f1bfee`、`25d9e180`，协调者已接受 1b）。**合并准备**（2026-11-18）：本分支合并了 `codex/v1.1` `173b2be7`（含 W20Y `05044756`、W20Y2 `1361b9e5`），`buttonSecondary` 已写入 `theme.json`（W20Y 已合入），见“决定记录”末条。**W20Z2**（2026-11-19，在 `codex/v1.1` 上）：`panelLight` 64502F → 483A22，让禁用的二级按钮按画出来的面与可用的分得开（见“次要按钮”与“决定记录”末条）；只改了生成器的色板并重建。不是实机或美术 ACCEPTED：结论只覆盖“验收条件与结果”写明的方法，独立复核与实机由协调者安排。第二阶段与合并准备没有启动 Unity，没有用 GPT/Codex 生图，没有下载；没有改 C#、`.meta`、默认件、其他风格的件、字体或 `docs/STATE.md`（W20Z2 的实机截图见 `docs/specs/v11/W20Z_REPORT.md` 的 W20Z2 一节）。

输入：`docs/specs/v11/THEME_SPEC.md` §2（含 `onFelt`、`valueBadge`、W20X 的 `buttonStep` / `buttonStepPrimary` / `section` / `caption` 与“乘色只会调暗”）、§3（可选的 `Table/page_scene.png`）、§3.1、§5；`PLAY_LAYOUT.md` §1/§8、`SHOP_LAYOUT.md` §2、`CARD_TEMPLATE.md` §3/§6；`tools/art_v11/theme_contract.json`（2026-11-18，94 张中有引用的 50 张）；`ArtSource/StyleKit/themes/glass/` 的五张概念页与 `style_kit.json`；唐三彩、爆彩漫画两套 kit 与黑金 kit（只读）的做法与检查；协调者对第一阶段与 1b 的复核、第二阶段的决定与“黑金的教训”（2026-11-18，见末节“决定记录”）。代码依据：`GameApp.Play11.cs`（`L11`、`TopBar11`、`DrawTray11`、`DrawActions11`、`SolidButton11`、`ApplyTableMood11`）、`GameApp.Play11Score.cs`（`Factors11`、`FormulaChip11`）、`GameApp.Play11Contrast.cs`（`FrameInsets11`、`LabelOn11`）、`GameApp.Card11.cs`、`GameApp.Shop14.cs`（`ShopButton14`）、`GameApp.V10.cs`、`UiKit.cs`、`Art07Felt.shader`（首领关压暗）。

## 范围与产物

生成器 [build_glass.py](build_glass.py) 一次写出 55 张 PNG 与 `theme.json`。全部在 4 倍画布上用程序绘制（精确多边形距离场、凸多边形逐边内缩、背光玻璃与金线脚的光照色阶），界面件最后一次浮点 Lanczos 缩小，桌面图与卡背用 4×4 盒式滤波（无振铃、分块无缝）；固定种子 20261118，PNG 不带时间戳或元数据。写图前检查：四个静区与 felt 色偏差 0、玫瑰窗在行动行常空矩形里且不进 11 个按钮与面板外接矩形、九宫格中心纯色、拉伸边带沿边不变（着色件）或沿边逐像素 ≤ 6 级（烘焙件）、与契约同尺寸；画接触表时有一张引用件落回默认件就报错。

| 路径 | 内容 |
| --- | --- |
| `DicePrototype/Assets/Resources/Themes/glass/UI/<name>.png` | 50 张 RGBA（契约里全部有引用的件），名称与像素尺寸逐项等于默认件 |
| `…/glass/Table/table_scene.png` | 1920×1080 RGB：铅条彩窗铺满，两块 felt 色石板（骰区加算式行、提交说明加提交栏）外一圈金色双线，行动行常空处一扇玫瑰窗（第一阶段 1b，未改） |
| `…/glass/Table/page_scene.png` | 1920×1080 RGB：同一种彩窗铺满，没有石板、金线和玫瑰窗（1b，未改） |
| `…/glass/Table/felt_tile.png` | 1024² RGB，周期石板细纹，均值即 felt 色（1b，未改） |
| `…/glass/preview.png` | 480×270：离线对局合成（状态 f，100 % 文字，buttonSecondary 取 `theme.json` 的值）缩小 4 倍 |
| `…/glass/theme.json` | id `glass`、displayName 彩绘玻璃、order 5、preview true；24 个色板项、4 个 W20X 项加 W20Y 的 `buttonSecondary`（生成器的 `PALETTE`、`W20X_TOKENS` 与 `W20Y_TOKENS` 是唯一来源）。`buttonSecondary` 已写入 `theme.json`（W20Y 已合入，见下） |
| `ArtSource/Art11/glass/contact_sheet.png` | build 的一部分：50 组 默认 / 彩绘玻璃 / 彩绘玻璃在桌面上（着色件乘典型色）；四张整屏合成（f 100 %、首领进行中 100 %、高压 100 %、f 150 %）；小图标在真实尺寸与宿主上（1 倍与 2 倍）；字标在纸面板上；桌面（静区、外接矩形、行动行空处标出）与 page_scene；色板与对比度；五张概念页 |
| `ArtSource/Art11/glass/card_template_review.png` | W13 + W14 卡面模板复核图（`--card-review`，不属于 build）：商店 220×348、桌面 216×244、紧凑 280×117 × 常见 / 进阶 / 稀有 / 商品，紧凑卡的四条最短短文（C21、C22、C78、C79），商品行 452×103 / 448×121，1080p 与 720p，彩绘玻璃在左、默认在右；三种宝石 12–34 单位 |
| `ArtSource/Art11/glass/review/` | 离线示意（`--review`，不属于 build），见“离线示意” |
| `tools/art_v11/glass/review_glass.py` | 示意的画法：按游戏代码排版（L11、DrawActions11、Factors11、DrawCard13、Shop14 S14、StagePreview10），精灵按游戏的导入方式取样（双线性、无 mipmap），线性光乘色与混合 |
| `tests/test_art_v11_glass.py` | 47 项独立检查（见“测试”） |

`ArtSource/Art11/glass/illustrations/` 与 `emblems/` 属于美术批次，本包没有碰。

## 形状与材质（取自哪张概念页）

| 形状 / 材质 | 概念页里的样子 | 用在 |
| --- | --- | --- |
| **铅条彩窗**：Voronoi 拼块（种子网格 40 px、抖动），钴蓝 44 %、宝石红 31 %、金 15 %、绿与紫各 5 %；每块背光、边上压暗、一道斜纹；铅线 2.6 px；整体亮度 0.6 | 各页四周的红蓝彩窗 | `table_scene` 静区以外、`page_scene` 全部、卡背的底（1b） |
| **金色双线框住的石板** | roll / triptych 骰池的石板与金框 | 两块石板，线从静区边缘往外画（1b） |
| **金框加宝石玻璃条**：铅、受左上光的金线脚、铅、背光的钴蓝或宝石红玻璃条、铅、细金线、铅，共 17 px；45° 切角；角上金色四芒星 | card 页的卡框 | 四种卡面（钴蓝；道具卡宝石红）：FrameInsets11 18 px，默认 22–34 |
| **细金框加宝石玻璃角片**（第二阶段）：铅 0.75、金线脚 3.75、夜色铅 1.5 px（暗面板；夜色铅与面板面色差 36，FrameInsets11 不计）；羊皮纸面板为铅 1、金线脚 4.5、铅 1.5 px。宝石玻璃从拉伸边移进角块：每个切角里一片背光的钴蓝（羊皮纸面板为宝石红）玻璃、铅线封口，上面一颗金四芒星 | roll 左栏面板的细金框与框角金星（面板的金框没有玻璃条） | `ui_panel_hud`、`ui_section_panel`、`ui_tab_bottom`、`ui_paper_panel` |
| **铅条里的玻璃块**：黑铅条、比玻璃暗一档的斜边、白色平面（= 色板色）；**受光一侧铅条细**（第二阶段）：上、左两边的铅条与斜边比下、右两边薄，像光从左上来，也让文字空间不超过默认 | roll 的钴蓝“重掷”、shop 彩色标题条、triptych 的蓝玻璃钮 | 着色件：`ui_button_bevel`、`ui_chip_formula`、`ui_topbar`（另有铅条角窗）；`ui_stage_card` 仍是四边一样的铅条（12 px，默认 19–29） |
| **重掷 的尖头**：两端各两道斜边填满角块（按钮横 28 / 竖 34 px，筹码 22 / 30 px） | roll 的“重掷”、“第 6 关”牌签，select 的“开始挑战” | `ui_button_bevel`、`ui_chip_formula` |
| **双线环**、**尖头横条**、**玫瑰窗**、**标题字** | roll 提交格双金框、shop 标题条、圆形彩窗、roll / select 的衬线粗体 | `ui_ring`、进度条与锁定骰的铜台、行动行与字标的玫瑰窗、`ui_wordmark_dicecraft`（Noto Serif SC Black，CP3-2）（1b） |
| **标题条从分区框里伸出**（第二阶段）：左端是开口（面一直到贴图左边，左上角按分区的切角斜切），右端尖头 | shop 的“控制器卡牌”“骰子柜台”标题条 | `ui_section_header`（乘 `caption` 羊皮纸） |
| **八角彩窗章**：铅外框、受光的金色斜面边、铅、八块放射状的背光玻璃、大理石白的符号描铅 | triptych 的钴蓝骰子章、金色勾章、宝石红箭头章；card 页卡顶的章 | 五枚附魔章、`ui_tag_lock`、`ui_tag_clock` |
| **切面玻璃宝石**：铅色墨线外框，按扇区受光的灰色切面，白色台面 | card 页卡顶的宝石 | `ui_gem_common`（圆形明亮式 8 面）、`_advanced`（菱形 4 尖）、`_rare`（六角星） |
| **金器与大理石**：金色色阶（FFEDB0 → DDA94A → BE8A35 → 7E5420 → 4E3214）、大理石白（FCF7EE → EEE5D4 → CDBFA9）、铅黑 16131B | 各页的金框、金币、金锁、大理石骰 | 金币、金锁、骰杯的金箍、放大镜、金冠、卡背 |

**按钮的金边**：照协调者的决定，按钮是中性尖头件，金色来自 `UiKit.Button` 下面的台阶（buttonStepPrimary B08A3E、buttonStep 8C6B33），不另开代码任务加金色外框。

## 概念页特征 → 界面件

| 概念页特征（页） | 界面件 |
| --- | --- |
| 铅条彩窗（各页四周） | `table_scene` 静区以外、`page_scene`、`ui_card_back` 的底 |
| 骰池的石板与金框（roll、triptych） | `table_scene` 的两块石板与金色双线 |
| 卡框：金线脚、宝石玻璃条、框角金星（card） | `ui_card_controller` = `_compact`、`ui_card_shop`（钴蓝条）、`ui_card_item`（宝石红条） |
| 左栏面板的细金框与框角金星（roll） | `ui_panel_hud`、`ui_section_panel`、`ui_tab_bottom`（钴蓝角片）、`ui_paper_panel`（宝石红角片） |
| 分数框、预算框（triptych 顶部、roll 左栏） | `ui_info_inset`：铅框、受光的金下唇 |
| 空卡槽、局内道具空格（roll） | `ui_slot_inventory`：石板凹格、细金线 |
| 铅条里的玻璃钮与牌签（roll、shop、triptych） | 着色件 `ui_button_bevel`、`ui_chip_formula`、`ui_topbar`、`ui_stage_card`、`ui_frame_block`、`ui_button_ghost` |
| 提交格的双金框（roll） | `ui_ring` |
| 托盘与锁定骰的台（triptych） | `ui_dice_tray`（铅、斜面、铅，透空）、`ui_tray_front`（铜台金沿、尖头） |
| 分区标题条（shop） | `ui_section_header` |
| 进度条（triptych 顶部） | `ui_progress_fill` / `_track`（尖头） |
| 卡框里的彩色玻璃（card） | W13 `ui_card_frame_rarity`：铅、着色玻璃带、铅（10 px，默认 12） |
| 卡顶的宝石（card） | `ui_gem_common` / `_advanced` / `_rare` |
| 八角章（triptych；card 页卡顶的章） | `badge_enchant_base10`（翠绿玻璃、+）、`_mult2`（紫晶、×）、`_roll1`（天青钴蓝、骰杯）、`_slotless`（铅灰玻璃、四角括）、`_submit1`（琥珀金、勾，即 triptych 的金色勾章）；`ui_tag_clock`（钴蓝章、大理石钟面） |
| 锁定骰下的锁牌（triptych） | `ui_tag_lock`（琥珀章里的黑锁）、`ui_icon_lock`（金锁、铅锁孔） |
| 金币（shop 价签） | `ui_icon_coin`：金币、铅圈、深金四芒星 |
| “投掷”（roll 左栏） | `ui_icon_roll`：钴蓝玻璃骰杯、金箍、一颗大理石骰从杯口翻出 |
| “查看骰子”（roll 右上） | `ui_icon_dice_view`：大理石骰与金放大镜、浅蓝镜片 |
| 金冠、星芒（select 首领章、shop 贿赂首领） | `fx_score_crown`：金冠，五个尖头金星，箍上宝石红与钴蓝玻璃；`fx_score_spark`：四芒星（中性，两档切面） |
| 设置 / 手册 / 刷新（shop） | `ui_icon_gear`、`ui_icon_book`、`ui_icon_refresh`、`ui_icon_arrow`：白色形状，阴面一道浅灰斜面（中性，代码乘字色） |
| — | `ui_pip_dot`（白圆）、`fx_square_chip`（玻璃碎片：灰边、白面、一道浅灰反光）、`fx_arc_short`（渐粗的弧，末端四芒星）、`ui_die_shadow`（柔和的铅色椭圆影） |
| 卡背（概念页没有） | `ui_card_back`：卡框（金线脚、钴蓝条、金星）、彩窗底、中央玫瑰窗与大理石骰（红蓝点）、上下两颗金星 |
| 标题字（roll、select） | `ui_wordmark_dicecraft`（1b） |

## 着色约定

被代码乘色的件（生成器与测试各自列出的 23 张 `NEUTRAL_NAMES`）只用灰度：R=G=B；有实面的 5 张（顶栏、筹码、本关牌、按钮、框块）中心纯白，所以画出来的颜色就是色板值，`LabelOn11` 的“中心像素 × 着色”选字看到的也是它；斜面、铅条、切面只比白色暗（“乘色只会调暗”）。开口件（环、描边钮、托盘、稀有度环）中心全透明。其余件是烘焙色，代码以白色显示（商店分区靠 `section` = FFFFFF 保持烘焙；`ui_chip_label` 被代码乘 valueBadge / danger，羊皮纸面上乘出金色与宝石红）。

## 色板与对比度

WCAG 2 相对亮度；ΔE 为 CIE76，CIEDE2000 另注。`python tools/art_v11/glass/build_glass.py --report` 打印下列数字（`palette_report()`）。第二阶段与 W20Z2 只改了 `panelLight`（见“次要按钮”）；其余 23 个 token 与 W20X 四个键同第一阶段。

| token | 值 | 选择理由 | 关键对比 |
| --- | --- | --- | --- |
| felt | 292833 | 概念页的石板（roll 骰区中位 292831） | onFelt 10.89，textColor 12.71 |
| background | 121118 | 最深的石板：页底 | — |
| panel | 1E2036 | 夜色玻璃：深色面板、顶栏、次要筹码 | textColor 13.97，muted 7.81；对桌面 1.10（靠金框分开） |
| panelLight | **483A22**（W20Z2；第二阶段 64502F，原 6B5632） | 古铜：普通关牌签、未到的章节点、空道具环、停用的面、托盘边的混色、商店格子、选中的平面页签、设置滑条的底槽。W20Z2 在同一色相与饱和度（37.9°、0.53）里加深（L* 35.3 → 25.3），让停用的二级按钮转成象牙字的暗牌（“次要按钮”） | 字取 textColor 9.65（原 6.73）；停用按钮上的 Muted 5.39（原 3.76）；变弱的用处见“已知风险” |
| paper / ink | F1DFC2 / 1B1720 | 羊皮纸与铅黑 | ink/paper 13.51 |
| textColor / muted | F7EFDF / BEB4A6 | 大理石白、旧羊皮纸灰 | 见 panel |
| gold | F0BE52 | 金：价格、金币、高亮 | 字取 ink 10.25；在 panel 上 9.27 |
| reward | FFD567 | 亮金：达标环、总分筹码 | 字取 ink 12.58 |
| primary | 2152C4 | 钴蓝玻璃：每个阶段唯一的主动作（决定 1） | LabelOn11 取 textColor 5.99（≥4.95，不调面） |
| boss | A8231B | 宝石红玻璃（概念页首领签 A40313） | 见“首领色” |
| pressure | EE6A2C | 朱橙玻璃：警示色，色相 19.2°，饱和度 0.82 | 字取 ink 5.66；与 primary CIEDE2000 53.1 |
| danger | B0281F | 宝石红：错误与警示角标 | paper 上 5.07 |
| teal | 62C894 | 浅翠：完成、正面数值 | panel 上 7.76 |
| violet | 9C74D6 | 紫晶：附魔 | — |
| red / blue | EC655D / 55B6D9 | 骰点颜色，各风格固定（CP3-1） | — |
| ghost | 8E7A5A | 暗古铜：空提交格、托盘内线、描边钮、商品的卡环 | felt 上 3.52；与卡框金线 CIEDE2000 21.5 |
| shadow | 0B0A0F | 铅影 | — |
| rarityCommon / Advanced / Rare | B7BDC9 / 2E9A66 / B2397F | 透明银玻璃、翠绿玻璃、玫瑰窗品红 | 两两亮度比 1.88 / 2.94 / 1.57（≥1.4）；与 primary ΔE 73.7 / 109.6 / 58.8（≥40），CIEDE2000 39.3 / 49.6 / 30.2；与卡框金线 CIEDE2000 31.2 / 37.2 / 55.8 |
| onFelt | E9DEC8 | 直接画在石板上的序号、空格点、乘号 | felt 上 10.89 |
| valueBadge | 不填（= gold） | 数值角标用金、警示角标 danger。按 `ui_chip_label` 的羊皮纸面（F1DFC2）乘色：E3A63C 对 A62215 | CIEDE2000 43.64、亮度对比 3.42；字 ink 8.22 / textColor 6.42 |
| buttonStep / buttonStepPrimary（W20X） | 8C6B33 / B08A3E | 按钮下的台阶是金色底座：玻璃块坐在金上 | 与面 CIEDE2000 ≥20，对桌面 ≥2.5 |
| section（W20X） | FFFFFF | 商店分区保持烘焙（夜色面与细金框） | 分区中心 1E2036：muted 7.81，textColor 13.97 |
| caption（W20X） | EBD6B0 | 羊皮纸标题条：商店分区标题与托盘的三个标签（决定 2） | 字取 ink 12.42 |
| buttonSecondary（W20Y，已写入 theme.json：W20Y 已合入） | AC8442 | 琥珀金玻璃：每个 SolidButton11 的面（次要按钮、跳过演出、查看骰子、上一手明细、计分明细、商店按钮、价格按钮） | 见“次要按钮” |

### 首领色（黑金的教训：离 primary、红骰点、pressure 各 ≥15 CIEDE2000）

| 量 | 数值 | 要求 |
| --- | --- | --- |
| boss A8231B 与 primary 2152C4 | 43.07 | ≥15 |
| 与红骰点 EC655D | 22.86 | ≥15 |
| 与 pressure EE6A2C | 25.20 | ≥15 |
| 算式行里画出来的两块：首领因子（白面 × boss，LabelOn11 0 步，A8231B）与手型因子（× red，0 步，EC655D） | 22.86，亮度对比 2.25:1 | ≥15（黑金只有 5.8） |
| 画出来的首领牌面与 primary | 43.07 | ≥15 |
| 牌面上的字（textColor，LabelOn11 0 步） | 6.29 | ≥4.95 |
| 环对 paper / 对桌面 | 5.50 / 2.02 | 非文字 ≥3（paper）；桌面上靠环的铅边分开 |
| 与 danger B0281F | 1.91 | 无规则；两者都是宝石红（见“已知风险”） |

`palette_report()` 的 `boss chip … / red chip … (drawn)` 用与游戏相同的 LabelOn11 步进计算；测试另写一遍（`test_boss_stands_apart_from_primary_the_red_pips_and_pressure`）。示意：`review/boss_stage.png`（首领进行中，算式行“基础分 × 一对 × 满员”里首领因子紧挨着红色手型因子，100 % 与 150 %）、`play_c_*`、`play_boss_*`、`play_boss_play_*`；首领关按 `Art07Felt.shader` 压暗桌面（× 0.74 与暗角，近似）。

### 次要按钮（buttonSecondary，W20Y 键，已写入 theme.json）

AC8442 照协调者的决定保留，已写入 `theme.json`（W20Y 已合入）。第二阶段时它只放在生成器的待定色板 `PENDING_TOKENS` 与本文，不写入 `theme.json`（当时本工作树的 `ThemePalette.cs` 还没有这个键：JsonUtility 会跳过它，结构测试也要求每个色板键都已声明）。合并准备（2026-11-18）把 `codex/v1.1` `173b2be7` 并入本分支（`ThemePalette.Hexes` 声明 30 个键），待定色板改名为生成器的 `W20Y_TOKENS`，由 `theme_json()` 写入 `theme.json`（排在 W20X 四个键之后）；`full_palette()` 去掉 `pending` 开关，`review_glass.palette_of()` 直接用 `theme.json` 的色板，`palette_report()` 用色板里的值（色板缺这个键时取 `W20Y_TOKENS`）。`theme.json` 由生成器重建，没有手改（`test_shipped_theme_json_matches_regeneration`）；交付的 PNG 不变（示意本来就按这个值画）。示意说明里的 “simulated buttonSecondary AC8442 (pending W20Y key, not in theme.json)” 改为 “buttonSecondary AC8442 (W20Y key, set in theme.json)”，接触表的色板不再标 pending。结构测试的可选键集合仍含 `buttonSecondary`（与 `valueBadge`、`buttonStep`、`buttonStepPrimary`、`section`、`caption` 同列），必需键仍是 24 个；`test_theme_json_schema` 现在还要求它已在 `ThemePalette.Hexes` 声明、已写入 `theme.json`。停用的 SolidButton11 字按 W20Y2 的 `LabelOn11(face, Muted)` 画。

| 量 | 值 | 目标 |
| --- | --- | --- |
| 面对 felt 292833（桌面石板；行动行下的彩窗中位亮度 0.020，与石板相近） | 4.24 | ≥3 满足 |
| 面对 panel 1E2036（深色面板、商店分区、顶栏：商店顶栏的查看本章 / 手册 / 设置都画在顶栏上） | 4.66 | ≥3 满足 |
| 面对 background 121118 | 5.48 | ≥3 满足 |
| 面对 paper F1DFC2（卡面与商品行上的价格按钮） | 2.62 | ≥3 **不满足**：筹码自带黑铅条外框，铅条对羊皮纸 16:1，边界靠它（1b 已报告，协调者保留 AC8442） |
| 字（LabelOn11 偏好 textColor，不到 4.5 时取最好的） | ink 5.15，0 步 | ≥4.95 满足 |
| 停用的面 lerp(AC8442, panelLight, .25) = 93723A（W20Z2；第二阶段 9A773D）与可用的面 | CIEDE2000 7.57（第二阶段 5.38） | — |
| 停用的字 LabelOn11(93723A, Muted) | Muted 只有 2.18，不到 4.5；三个候选都不到 4.95，加深（大理石白字）与调浅（铅黑字）都要 3 步，平手时加深：面加深 3 步成 7E6132，字 textColor 5.03（C# 的浮点；生成器按 8 位取整算 7E6232、4.99） | 字由铅黑换成大理石白：**不同字色满足** |
| 实际画出的停用面与可用的面 | CIEDE2000 **15.12**（8 位取整 14.84；第二阶段 3.33，W20Z 实机 3.18） | 按画出来的面 **≥5 满足** |
| 决定步数的对比（浮点，与 C# 一致） | 加深第 3 步 5.026、第 2 步 4.638；调浅第 2 步 4.708 | 离 4.95 都至少 0.07：加深 3 步、调浅至少 3 步，平手加深，分支稳定 |

第二阶段按协调者当时写的条件（lerp 的面差 ≥5，或字色不同）通过的是第一条：5.38。但 LabelOn11 会把那张停用面调亮 2 步，玩家看到的停用面与可用面只差 3.33（W20Z 实机 3.18），字都是 ink，停用态看不出来。W20Z2（协调者 2026-11-19，照黑金 2b 的做法）把条件改为按画出来的面：面差 ≥5，或字色不同；只调生成器的色板。办法与黑金相同：把 `panelLight` 在同一色相与饱和度里加深，让 25 % 混合后的停用面暗到 LabelOn11 走加深一支、改选大理石白字。483A22 是这一色相里决定步数的对比两边都离 4.95 至少 0.07 的最亮值（更亮一档 493B23 的加深第 3 步只有 5.010）；结果两条都满足：面差 15.12、字由铅黑换成大理石白。可用的面、字与 W20Z 量过的 buttonSecondary 对比（桌面 4.24、深色面板与顶栏 4.66、羊皮纸 2.62）都不变；停用面对羊皮纸由 2.68 升到约 4.4。代价是 panelLight 的其他用处变暗，见“已知风险”。示意 `review/secondary_states.png` 已按新值重画。

## 桌面（THEME_SPEC §3.1，第一阶段 1b，第二阶段未改）

- 四个静区与 felt 色偏差 0（生成器写图前检查，阈值 0；测试再查）：

  ```text
  GLASS_ZONE dice_area rect=52,374,1408,276 max_rgb_delta=0 limit=0 PASS
  GLASS_ZONE formula_row rect=52,660,1408,60 max_rgb_delta=0 limit=0 PASS
  GLASS_ZONE submission_rail rect=344,740,1136,202 max_rgb_delta=0 limit=0 PASS
  GLASS_ZONE submit_info_text150 rect=32,740,360,202 max_rgb_delta=0 limit=0 PASS
  ```
- 两块石板（52–1460 × 374–720、32–1480 × 740–942）整块是 felt 色；外面紧挨的一圈像素没有 felt 色，金色双线从那里往外画。
- 静区以外是铅条彩窗（一种铺满的纹理，没有版面形状）；按决定 A，彩窗可以在面板和卡牌栏下面（纹理，不是大装饰）。卡牌栏 0、3、5、7、10 张时露出的都是同一种彩窗（`review/card_rows.png`）。
- **玫瑰窗**：中心 (1384, 1012)、半径 54，外接框 x 1330–1438、y 958–1066；行动行每个状态（9 个状态 × 100 % / 150 % 文字，含按钮台阶、焦点框与达标环）都空着的是 x 1279–1492、y 942–1080（`review/action_row.txt`，`ACTION_ROW_OK`），左右各留 51 / 54 px，上 16 px；外圈是一圈闭合的铅（测试沿 360 个方向检查）。page_scene 与 table_scene 在石板与金线之外只差玫瑰窗。
- 托盘边（代码乘 lerp(panel, panelLight, .55)）：W20Z2 的 panelLight 483A22 下是 352E2B，对 felt 1.09:1（第二阶段 453A32、1.32，原 483E34、1.39）；协调者决定托盘边保持现状，看得见的托盘边仍是静区外的金色双线（对彩窗与石板约 6.8:1）。
- 直接画在桌面上的字（`table_text_contrast.py glass`，非对局画面的背景 page_scene）：19 处中 **9 处低于 4.5:1**，最低 2.77（选关的预算行），`TABLE_TEXT_BELOW`。按协调者的决定，这些字由 W15 / W16 放到面板或标题条上，本包只报告数字。

## 文字空间（C# `FrameInsets11` 镜像，sprite px，左、下、右、上；PPU 200 时 2 px = 1 单位）

协调者的规则（黑金的教训）：每个装字的件（按钮、筹码、卡、面板、条）每一边都不超过默认件。第二阶段把 1b 里超出的 10 件改薄，全部 50 张里有切片边距的 24 张：

| 件 | 装字 | 彩绘玻璃 | 默认 | 1b | 结果 |
| --- | --- | --- | --- | --- | --- |
| `ui_card_controller` / `_compact` | 是 | 18, 18, 18, 18 | 22, 29, 27, 22 | 18 | ≤ 默认 |
| `ui_card_shop` | 是 | 18 | 27, 34, 32, 27 | 18 | ≤ 默认 |
| `ui_card_item` | 是 | 18 | 22, 30, 28, 22 | 18 | ≤ 默认 |
| `ui_stage_card` | 是 | 12 | 19, 29, 27, 19 | 12 | ≤ 默认 |
| `ui_panel_hud` | 是 | 5, 5, 5, 5 | 10, 8, 6, 10 | 18 | ≤ 默认（改） |
| `ui_section_panel` | 是 | 5 | 6, 6, 6, 6 | 18 | ≤ 默认（改） |
| `ui_tab_bottom` | 是 | 5, 0, 5, 5 | 6, 0, 6, 6 | 18, 0, 18, 18 | ≤ 默认（改） |
| `ui_paper_panel` | 是 | 8 | 9, 18, 16, 9 | 18 | ≤ 默认（改） |
| `ui_topbar` | 是 | 8, 11, 10, 8 | 8, 30, 11, 8 | 11 | ≤ 默认（改） |
| `ui_button_bevel` | 是 | 7, 11, 11, 7 | 7, 12, 12, 7 | 10 | ≤ 默认（改） |
| `ui_chip_formula` | 是 | 6, 9, 8, 6 | 6, 16, 10, 6 | 8 | ≤ 默认（改） |
| `ui_chip_label` | 是 | 5 | 5, 5, 5, 5 | 7 | ≤ 默认（改） |
| `ui_info_inset` | 是 | 0, 3, 3, 0 | 0, 3, 3, 0 | 0, 5, 5, 0 | ≤ 默认（改：铅与井面色差 19，不计；只有下、右的金唇计入） |
| `ui_section_header` | 是 | 0, 0, 24, 0 | 0, 0, 32, 0 | 10, 0, 24, 0 | ≤ 默认（改：左端开口） |
| `ui_slot_inventory` | 是（空格里的“+”） | 5 | 40（半透明中心） | 5 | ≤ 默认 |
| `ui_tray_front` | 否 | 5, 0, 5, 0 | 5, 0, 5, 0 | 同 | 相等 |
| `ui_progress_fill` | 否 | 15, 0, 15, 0 | 0, 0, 6, 0 | 同 | 厚于默认（不装字，代码不量它；两端尖头在 48 px 边距里） |
| `ui_progress_track` | 否 | 15, 0, 15, 0 | 6, 0, 6, 0 | 同 | 同上 |
| `ui_frame_block` | 否 | 0 | 0 | 0 | 相等 |
| `ui_ring` / `ui_button_ghost` / `ui_dice_tray` / `ui_card_frame_rarity` | 否（透空） | 40 / 40 / 72 / 40 | 同 | 同 | 等于切片边距 |
| W13 `ui_card_frame_rarity`（`RingInsets13`） | 卡内容框 | 10 | 12 | 10 | ≤ 默认 |

- 两行提示在 150 % 文字时：提示框底边框 5 px（2.5 单位）。测试的离线估算（行距 1.37 em、字形底在行中线下 0.42 em）第二行字底离内框 15.0 单位；按 W20Xb 在 Unity 里量的字块（15.7–72.3 单位）是 88 − 2.5 − 72.3 = 13.2 单位（要求 ≥5；1b 是 6.7）。
- 商店分区标题条高 68 − 2.5 − 2 = 63.5 单位（≥50）；34 单位的托盘标签留 34 − (9 + 6) / 2 = 26.5 单位（≥20）；30 单位的数值角标留 25 单位（≥18）。
- 面板变薄的代价：宝石玻璃条从面板的拉伸边上没了（卡上还在），面板读作“细金线、四角宝石玻璃与金星”。对照图 `review/text_room_frames.png`（左：交付的细框；右：1b 的宝石条框，不交付），见“需要的决定”第 1 条。

## 离线示意（`ArtSource/Art11/glass/review/`）

全部是 Python 按游戏代码的排版模拟，不是 Unity 截图；卡名、短文、插画（彩绘玻璃风格包的锚点裁片）与价格是替身，3D 骰子是平面替身；画到 buttonSecondary 的图，说明里都写着 “buttonSecondary AC8442”（`theme.json` 里的值；W20Y 合并前写作 “simulated buttonSecondary AC8442”）。

| 文件 | 内容 |
| --- | --- |
| `play_<状态>_<100|150>.png`、`play_states.png` | 9 个状态 × 两种文字大小：a（已选骰子）、b3（计分中）、c（首领关开始）、f（7 张卡、锁定 2 颗）、rolling（掷骰中）、qualified（已达标）、boss（满员提示，两行）、pressure（高压关）、boss_play（首领进行中，首领因子在算式行）；首领关按代码压暗桌面 |
| `boss_stage.png` | 首领进行中 100 % 与 150 %，附首领色的距离与算式行两块筹码画出来的颜色 |
| `secondary_states.png` | SolidButton11 可用与停用（行标 on / off）：buttonSecondary AC8442 与不设这个键时的 191929，各画在石板、彩窗、深色面板、顶栏与卡面羊皮纸上（停用字按 W20Y2；对局示意里没有停用的次要按钮，这里单独画） |
| `card_rows.png` | 卡牌栏 0、3、5、7、10 张，100 % 与 150 %（多于 7 张或 150 % 多于 5 张时两排紧凑卡） |
| `action_row.txt` | 每个状态与字号下，玫瑰窗左右两侧元素的边与是否空着 |
| `shop_100.png` | W14 商店（`Shop14` S14，100 % 文字）画在 page_scene 上，W20X 乘色 |
| `select_100.png` | 选关页（`StagePreview10`），桌面上的字照代码画在彩窗上；“开始挑战”照代码是 Ink 字（W16 的问题） |
| `beside_roll.png`、`beside_shop.png`、`beside_select.png`、`beside_triptych.png` | 概念页与示意并排 |
| `text_room_frames.png` | 交付的细框与 1b 宝石条框并排（对局右栏 150 % 与商店右上），供决定 |
| `notes.txt` | 每张图自动选的字色与对比度、LabelOn11 调面的步数、提示框的字块位置、动作行表 |

共 31 个文件。

第一阶段 1b 的 `tray_rim_options.png`（托盘边的方案 B）已由协调者的决定（保持现状）取代，从目录里删掉（在 `67f1bfee` 里可取回）。

## 重建与验证

```powershell
& E:\anaconda3\python.exe -B tools/art_v11/glass/build_glass.py                  # 全部交付件与接触表，约 5 分钟（低优先级）
& E:\anaconda3\python.exe -B tools/art_v11/glass/build_glass.py --report         # 色板对比度
& E:\anaconda3\python.exe -B tools/art_v11/glass/build_glass.py --review         # 离线示意（读已交付的件）
& E:\anaconda3\python.exe -B tools/art_v11/glass/build_glass.py --card-review    # 卡面模板复核图
& E:\anaconda3\python.exe -B tools/art_v11/theme_contract.py audit glass --strict
& E:\anaconda3\python.exe -B tools/art_v11/table_text_contrast.py glass
& E:\anaconda3\python.exe -m unittest tests.test_art_v11_glass -v
& E:\anaconda3\python.exe -m unittest discover -s tests -v
```

依赖：Python 3.11、Pillow 9.4、numpy 1.24、scipy 1.10；字体只用仓库内的 Noto Sans SC Bold、Noto Sans CJK SC、Liberation Sans 与 Noto Serif SC Black（`Fonts/Themes/glass/`，OFL）。每次开跑前在同一条命令里确认没有命令行含 `V072BatchTests.Run` 的 Unity 在跑。

## 测试（`tests/test_art_v11_glass.py`，47 项）

清单（50 件、55 张 PNG）与尺寸、RGBA、不全透明；着色件 R=G=B、白色实面、开口件中心全透明；九宫格中心从边距线起纯色（20 件）、横向切片中段逐列相同（4 件）；拉伸边带（9 个着色件沿边不变，11 个烘焙件沿边逐像素 ≤6 级）；**装字的 16 件每一边不厚于默认**（协调者的规则）、框在边距内、分区标题条 / 托盘标签 / 数值角标的字高；150 % 两行提示余量 ≥5 单位；7 件的角是 45° 直切角、8 件框角有金星；按钮与筹码两端是两道斜边的尖头（60 / 72 / 80 单位高时不缩）；卡保留宝石玻璃条、面板的宝石玻璃只在四个角块；金框有高光且受光边更亮；标题条左端开口、右端尖头在中线上，进度条两端尖；内层件不出宿主轮廓（稀有度环、本关牌双环、托盘环、商店标题条）；带字面板的对比度；锁与小图标在真实尺寸与宿主上（含 buttonSecondary 面与乘字色的导航图标）；附魔章保留含义色与浅色符号；稀有度环带宽不超过默认、两侧铅线、对铅线 ≥3；宝石轮廓尖数、小尺寸可分、墨线与浅色切面；桌面尺寸、静区、石板整块 felt 色且外圈是金线、静区外是红蓝为主的彩窗、玫瑰窗是唯一的图形且在空处与外接矩形之外、外圈铅闭合；page_scene 是没有版面形状的同一种彩窗；felt_tile 无缝均匀；色板规则（AX-08、onFelt、pressure 警示色、稀有度、主按钮）；首领色（离 primary、红骰点、pressure ≥15，画出来的筹码也是）；buttonSecondary（已写入 `theme.json`，W20Y 已合入；值、对 felt / panel / background ≥3、字 ≥4.95、铅条对羊皮纸 ≥10）；停用的次要按钮可区分（W20Z2 起按 LabelOn11 调过的画出来的面：面差 ≥5 或字色不同，现在两条都满足、字是大理石白，决定步数的对比按浮点离 4.95 至少 0.05；panelLight 的关卡签字 ≥4.95 不调面、禁用 `UiKit.Button` 的 Muted ≥3）；数值与警示角标；W20X 四个键；`theme.json` 结构（键名从 `ThemePalette.cs` 读取，可选键含 buttonSecondary；W20Y 的 buttonSecondary 须已声明、已写入）与生成一致；概念页 SHA 与来源表一致；字标用批准的标题字；两次完整重建逐字节一致且与交付件一致。

## 验收条件与结果

实施者自测；独立复核与实机由协调者安排。

| ID | 方法与期望 | 证据 | 结果 |
| --- | --- | --- | --- |
| W20-01 | 50 件齐全、RGBA、与默认件同尺寸；strict 审计 0 缺件 | `theme_contract.py audit glass --strict`、单测 | 通过：`50 sprites shipped, 0 referenced defaults not overridden`，`OPTIONAL Table/page_scene 1920x1080`，`AUDIT_OK` |
| W20-02 | 九宫格中心纯色；横向切片中段逐列相同；拉伸边带规则；框在边距内 | 单测、生成器写图前检查 | 通过：20 个四边九宫格、4 个横向切片；着色件沿边不变；烘焙件沿边逐像素 ≤6 级 |
| W20-03 | 着色件 R=G=B；有实面的着色件中心纯白；开口件中心全透明 | 单测 | 通过：23 / 5 / 4 件 |
| W20-04 | 四个静区偏差 0；外接矩形内无大装饰；动作行装饰只在全状态空处、闭合；卡牌栏下无大装饰；page_scene 无版面形状 | 生成器检查、单测、`action_row.txt`、`card_rows.png` | 通过：4/4 偏差 0；玫瑰窗不进 11 个矩形，18 个状态 × 字号都在空处，外圈铅闭合；卡牌栏下只有彩窗纹理 |
| W20-05 | 色板：红蓝原值、AX-08、onFelt、pressure 警示色、稀有度亮度与离 primary 的距离、主按钮字 ≥4.95、角标、W20X 键；`theme.json` 结构 | 单测、`palette_report()` | 通过，数字见色板表 |
| W20-06 | 黑金教训·文字空间：装字的件每边 ≤ 默认，完整表 | 单测、“文字空间”表 | 通过：16 件 0 边超出（1b 有 10 件超出，已改薄）；不装字的进度条两件厚于默认，已列出 |
| W20-07 | 黑金教训·首领色：离 primary、红骰点、pressure 各 ≥15 CIEDE2000（画出来的筹码也是）；首领关示意 | 单测、`boss_stage.png` | 通过：43.07 / 22.86 / 25.20；筹码 22.86 |
| W20-08 | 黑金教训·buttonSecondary：对 felt、paper、深色面板、顶栏的对比；停用面 ≥5 CIEDE2000 或字色不同（W20Z2 起按画出来的面） | 单测、`palette_report()` | 部分：felt 4.24、panel（面板与顶栏）4.66、background 5.48 满足；paper 2.62 不满足（已知，铅条外框 16:1）。停用（W20Z2，panelLight 483A22）：画出来的面加深 3 步成 7E6132，与可用面差 15.12，字换成大理石白 5.03，满足（第二阶段只按 lerp 面差 5.38 满足，画出来只差 3.33、字同为 ink） |
| W20-09 | 两行提示 150 % 时字底余量 ≥5 单位 | 单测（离线估算） | 通过：15.0 单位（按 W20Xb 实测字块 13.2） |
| W20-10 | 小图标在真实尺寸与宿主上可辨；锁 20×22 在 background 上 | 单测、接触表小图 | 通过（离线合成） |
| W20-11 | 字标用 Noto Serif SC Black | 单测、接触表 | 通过 |
| W20-12 | 非对局画面桌面上的字 | `table_text_contrast.py glass` | 只报告：19 处中 9 处低于 4.5:1，最低 2.77（W15 / W16 处理） |
| W20-13 | 两次完整重建逐字节一致，且与交付件一致 | 单测（setUpClass 两次 build 到工作树内的临时目录）；`--review`、`--card-review` 重画后与交付件对比哈希 | 通过：两次 build 的 56 个文件（55 张 PNG 与 `theme.json`）彼此逐字节相同，且与交付件相同；交付的 UI 目录里没有 build 不写的文件。`--review` 与 `--card-review` 连跑两次：32 个文件（示意 31 个与卡面复核图）两次逐字节相同 |
| W20-14 | 玻璃单测、全量 `unittest discover -s tests -v` | 本机日志，BelowNormal 优先级；开跑前同一条命令确认没有 `V072BatchTests.Run` 的 Unity（两次都没有；跑完时也没有） | 通过：玻璃 `Ran 47 tests in 607.954s`，`OK`；全量 `Ran 381 tests in 1192.190s`，`OK`（0 失败、0 错误、0 跳过；全量里玻璃 47 项也全过） |
| W20-15 | 只改本包路径；其他风格与默认件逐字节不变 | `git diff --stat cd2fedb3`（排除本包四处路径后为空）、`git ls-files --others` | 通过：改动只在 `tools/art_v11/glass/`、`tests/test_art_v11_glass.py`、`DicePrototype/Assets/Resources/Themes/glass/`、`ArtSource/Art11/glass/`（`illustrations/`、`emblems/` 未动）；没有 `.meta`；其他风格、默认件、C# 0 改动 |
| W20-16 | 风格忠于概念页、纹理克制、示意在全部状态可读 | 实施者对照五张概念页、接触表、全部示意自查 | 自查完成，待独立复核与用户判断 |

## 没能检查的

- 引擎里的样子：Unity 导入、毡面着色器的首领压暗与暗角叠在彩窗上（示意是近似）、真实 TMP 字形与行高、按钮悬停与按下的乘色、720p 与超宽屏。示意里的骰子是 2D 替身，卡面插画是风格包锚点裁片。
- 紧凑卡的四条最短短文：只有离线估算（`card_template_review.png` 末尾的行），W20Xb 记录过漫画与唐三彩按镜像算也会在实机隐藏 C21、C22、C78、C79。
- `buttonSecondary` 已写入 `theme.json`（W20Y 已合入），W20Y / W20Y2 的代码（含停用字色）随合并进入本分支；示意仍是 Python 模拟，这个键在 Unity 里的样子没有看过。
- 附魔章：在当前代码里 `EnchantSprite` 先查 `UiKit.Template`，风格的章会生效；但整局结算、附魔提示等处的尺寸与底色没有逐一合成，只在卡面上（28 单位，羊皮纸）和接触表里看过。
- 卡背、骰影、计分碎片（`fx_square_chip`）、弧（`fx_arc_short`）在示意里没有出现的场景（选卡、骰子样式页、连击爆点）没有合成；只在接触表里看过。
- 彩窗在 720p 下铅线约 1.7 px、面板细金线约 1.3 px，没有实机看过是否发糊。

## 已知风险

- **面板变薄**：面板的金框从 1b 的 17 px（含宝石玻璃条）变成 6–7 px 的细金线，宝石玻璃只在四角。商店的分区、对局右栏的面板与提示框因此比 1b 朴素，接近第一阶段被退回时“细铜线”的样子，只是现在有彩窗桌面、宝石角片和金星；卡牌仍是完整的宝石条框（`text_room_frames.png`）。
- **停用的次要按钮**（W20Z2 已改）：第二阶段画出来只比可用的面浅一点（CIEDE2000 3.33，实机 3.18），字同为 ink；panelLight 483A22 后停用面是暗古铜、字是大理石白（15.12，见“次要按钮”）。
- **panelLight 加深后变弱的用处**（W20Z2，64502F → 483A22）：普通关的关卡签与未到的章节点对顶栏、空道具环对深色面板 2.08 → 1.45；托盘边对桌面 1.32 → 1.09；商店空格的金线乘 panelLight 后对分区 1.42 → 1.10；选中的平面页签（手册等的 `ui_frame_block` × panelLight）与设置滑条的底槽对页底 2.44 → 1.70。变强的：关卡签的字 6.73 → 9.65，禁用 `UiKit.Button` 的 Muted 字 3.76 → 5.39，选中页签的金色字 4.47 → 6.40。与黑金 2b 同类的代价，实机截图里再看。
- **danger 与 boss 同属宝石红**：B0281F 与 A8231B 只差 CIEDE2000 1.91（规则里没有这一条；数值角标对警示角标 43.6 满足）。
- **商店分区标题条盖住分区上两角的宝石角片与金星**（W14 的版面让标题条从分区边框内侧开始；1b 也一样）。
- **托盘边与空道具环**：panelLight 加深后托盘边变暗（对 felt 1.39 → 1.32，W20Z2 后 1.09），空道具环（× panelLight）对面板 2.28 → 2.08（W20Z2 后 1.45）；看得见的托盘边仍是金色双线，空环有铅边。
- 面板的夜色面（L 0.016）比彩窗（中位 0.020）还暗，靠细金线和角片从彩窗里分出来；细金线在 720p 只有约 1.3 px。
- 卡面四角的金星：七张卡时一排 28 颗，示意里读作框角装饰；实机再看。
- 生成一次约 5 分钟（低优先级），测试会完整生成两次。

## 需要的决定

以下是提案，未经协调者采纳前不改实现：

1. **面板的框（文字空间规则 vs. 1b 的宝石条）**：现在按规则交付细框（装字的件每边 ≤ 默认）。如果要保留 1b 的宝石玻璃条，暗面板与羊皮纸面板要回到 17 px（FrameInsets11 18 px，默认 6–10），四件超出默认 8–12 px；对照图 `review/text_room_frames.png`。选项：A 保持细框（交付状态）；B 面板恢复宝石条框，作为规则的例外（文字空间按用处核对：150 % 两行提示余 6.7 单位、商店标题条 57 单位）；C 只给对局右栏的 `ui_panel_hud` 例外，商店分区保持细框。
2. ~~**停用的次要按钮**：按所写条件（lerp 面差 ≥5）通过，但画出来只差 3.33、字同色。~~ 已由协调者在 W20Z2 决定（2026-11-19）：照黑金的办法调 panelLight，按画出来的面判断，见“决定记录”末条。
3. **danger 与 boss 同色**（1.91）：A 接受（两者含义相近：错误与首领都是危险）；B danger 改成更暗或更紫的红，与 boss 拉开。

## 决定记录

第一阶段复核（协调者，2026-11-18）：外观不对（“深色石板界面加几道细铜线”）。决定：1 主按钮钴蓝玻璃；2 caption 羊皮纸；3 两侧尖拱窗由 A 取代；4 panelLight 保持旧铜，可微调。返工 A 彩窗桌面与金线石板、B 金框加宝石玻璃条与金星、C 重掷 的尖头、D 标题条照旧、E 量托盘边框。→ 1b（`67f1bfee`、`25d9e180`）。

1b 复核（协调者，2026-11-18，接受）与第二阶段的决定：

- 主按钮钴蓝玻璃；caption 羊皮纸；panelLight 古铜。→ 不变（panelLight 在古铜内加深 2.3 CIEDE2000，见下）。
- 静区外与 page_scene 的铅条彩窗保留，静区外金色双线，玫瑰窗在行动行空处。→ 不变。
- 托盘边保持现状。→ 不变（panelLight 的改动让托盘边的混色略暗，见“已知风险”）。
- 按钮：中性尖头件加金色台阶，不开金色外框的代码任务。→ 不变。
- `buttonSecondary` AC8442 是待定键：留在生成器的待定色板与本文，不写入 theme.json；示意注明 simulated；停用的 SolidButton11 字由 LabelOn11(face, Muted)（W20Y2）。→ 照做。
- 结构测试的可选键集合含 `buttonSecondary`。→ 照做（29 / 30 个声明键都通过）。
- 桌面上的字低于 4.5:1 由 W15 / W16 处理，只报告数字；选关页“开始挑战”的字归 W16。→ 照做。
- 黑金的教训：文字空间每边 ≤ 默认（超出先问）→ 10 件改薄，0 件超出，面板的宝石条是否要例外见“需要的决定”第 1 条；首领离 primary、红骰点、pressure ≥15 → 43.07 / 22.86 / 25.20；buttonSecondary 的对比与停用态 → 见“次要按钮”，panelLight 6B5632 → 64502F 使 lerp 面差 4.64 → 5.38。

合并准备（协调者，2026-11-18；协调者告知第二阶段 `e8864baf` 已接受，合并步骤见 `codex/v1.1` 的 `docs/STATE.md`）：

- 在本工作树合并 `codex/v1.1`（W20Y `05044756`、W20Y2 `1361b9e5`、测试与文档）；本包路径之外有冲突就停下报告。→ 合并 `codex/v1.1` `173b2be7`（与黑金同一个点），合并提交 `66846f5c`，无冲突；合并没有改动本包的路径（`ArtSource/Art11/glass/` 下只改了 `illustrations/` 的 job.json，不属于本包）；`tools/art_v11/theme_contract.json` 没有变（仍是起点 `cd2fedb3` 重建的那份）。
- 把 `buttonSecondary` 从待定移入生成器的色板，由 build 写进 `theme.json`（不手改）；示意与报告照样能画，标注从 “simulated” 改为 “buttonSecondary”；README 里待定的说法改为已写入 theme.json（W20Y 已合入）。→ `PENDING_TOKENS` 改名 `W20Y_TOKENS`，`theme_json()` 把它写在 W20X 四个键之后；`full_palette()` 去掉 `pending` 开关；`review_glass.palette_of()` 直接用 `theme.json` 的色板；`palette_report()` 的键去掉 “(pending)”，“(today's label)” 改为 “(the label before W20Y2)”（W20Y2 已合入）；`secondary_states.png` 的行标从 “simulated AC8442 / today's 191929, enabled / disabled” 改为 “buttonSecondary AC8442 / unset 191929, on / off”（写成 enabled 会比 260 px 的行标栏宽），`notes.txt` 里对应的 “sim / today” 改为 “buttonSecondary / unset”；`test_button_secondary` 直接读 `theme.json`，`test_theme_json_schema` 另要求 W20Y 已声明这个键、`theme.json` 已写入（仍是 47 项）。
- 重建并验证（合并准备）。→ `build_glass.py`、`--review`、`--card-review`（BelowNormal；开跑前没有 `V072BatchTests.Run` 的 Unity）。交付件里只有 `theme.json` 变了（多一个键）；50 张件、`table_scene`、`page_scene`、`felt_tile` 与 `preview.png` 逐字节不变。重画的只有说明或标签文字变了的图：接触表、卡面复核图、25 张示意与 `notes.txt`（逐像素对比 HEAD：差异在文字行里；只有接触表的小图标排因为金币那格的标签变短而左移，“gear 34 x label” 移到上一排，图标与底色本身不变）；四张 `beside_*.png` 与 `action_row.txt` 不变。`theme_contract.py audit glass --strict`：`50 sprites shipped, 0 referenced defaults not overridden`，`AUDIT_OK`；玻璃单测 `Ran 47 tests in 606.214s`，`OK`；全量 `Ran 381 tests in 1185.235s`，`OK`（0 失败、0 错误、0 跳过；开跑前与跑完时都没有 `V072BatchTests.Run` 的 Unity）。

W20Z2（协调者 2026-11-19，在 `codex/v1.1` 上；W20Z 的实机截图证实停用的二级按钮画出来只差 3.18 CIEDE2000、字同为铅黑）：

- 照黑金 2b 的办法调色板：按画出来的面，停用面与可用面至少差 5 CIEDE2000，或者字换色；保留 AX-08、关卡签的字与 W20Z 量过的 buttonSecondary 对比；报告变弱的用处；只改生成器的色板并重建，theme.json 由生成器写出。→ `PALETTE["panelLight"]` 64502F → 483A22（同一色相 37.9° 与饱和度 0.53，L* 35.3 → 25.3）。停用面 lerp = 93723A，LabelOn11(face, Muted) 加深 3 步成 7E6132（浮点，与 C# 一致；生成器的 8 位报告 7E6232），字换成大理石白 5.03（8 位 4.99），与可用面差 15.12（8 位 14.84）：两条都满足。决定步数的对比（浮点）：加深第 3 步 5.026、第 2 步 4.638，调浅第 2 步 4.708，离 4.95 都至少 0.07（黑金 2b 的标准）；更亮一档 493B23 的加深第 3 步只有 5.010。AX-08 三项不涉及 panelLight，不变；关卡签的字 textColor 9.65（原 6.73，0 步）；buttonSecondary 的可用面、字与对比（桌面 4.24、面板与顶栏 4.66、羊皮纸 2.62）不变。变弱的用处见“已知风险”。
- 测试：`test_a_disabled_secondary_button_looks_disabled` 改为按 LabelOn11 调过、画出来的面判断（原来读的是调面前的 lerp 面，所以第二阶段的 5.38 能通过），并照黑金加上决定步数的余量（浮点 ≥0.05）、字换成大理石白、关卡签的字 ≥4.95 不调面、禁用 `UiKit.Button` 的 Muted ≥3；没有删测试、没有降阈值。
- 重建：`build_glass.py`、`--review`、`--card-review`（BelowNormal；开跑前同一条命令确认没有 `V072BatchTests.Run` 的 Unity）。交付件里变的只有 `theme.json`（panelLight）与 `preview.png`（普通关牌签、托盘边、空道具环、章节点用 panelLight）；50 张件、`table_scene`、`page_scene`、`felt_tile` 逐字节不变。重画的示意：接触表与 `review/` 下 29 个文件（用到 panelLight 的合成，含 `secondary_states.png`、四张 `beside_*.png` 与 `notes.txt`）；`card_template_review.png` 不变。`theme_contract.py audit glass --strict`：`50 sprites shipped, 0 referenced defaults not overridden`、`AUDIT_OK`；`table_text_contrast.py glass` 与之前相同（19 处中 9 处低于 4.5:1，`TABLE_TEXT_BELOW`，归 W15 / W16）；玻璃单测 `Ran 47 tests in 625.475s`、`OK`。实机截图与全量测试见 `docs/specs/v11/W20Z_REPORT.md` 的 W20Z2 一节。