# W20 黑金装饰艺术 UI kit

状态：**第一阶段交付，待协调者审查**（2026-11-18，分支 `codex/v1.1-deco`，起点 `codex/v1.1` `cd2fedb3`）。第一阶段按协调者的分阶段要求只交色板、桌面、代表件与离线示意；其余界面件、测试与验收表在第二阶段补齐。不是实机或美术 ACCEPTED。没有启动 Unity，没有用 GPT/Codex 生图，没有改 C#、`.meta`、默认件、其他风格的件、字体或 `docs/STATE.md`。

输入：`docs/specs/v11/THEME_SPEC.md` §2（含 `onFelt`、`valueBadge`、W20X 的 `buttonStep` / `buttonStepPrimary` / `section` / `caption` 与“乘色只会调暗”）、§3（可选的 `Table/page_scene.png`）、§3.1（桌面构图）；`PLAY_LAYOUT.md` §1、`SHOP_LAYOUT.md` §2、`CARD_TEMPLATE.md` §3、§6；`tools/art_v11/theme_contract.json`（2026-11-18，94 张中有引用的 50 张）；`ArtSource/StyleKit/themes/deco/` 的五张概念图与 `style_kit.json`；STATE 里唐三彩、爆彩漫画、W20X、W20Xb 的记录；代码 `GameApp.Play11`、`Play11Score`、`Play11Contrast`（`FrameInsets11`、`LabelOn11`）、`Card11`、`Shop14`、`UiKit`。

## 第一阶段的产物

| 路径 | 内容 |
| --- | --- |
| `tools/art_v11/deco/build_deco.py` | 生成器：4 倍画布、精确多边形距离场、一次浮点 Lanczos 缩小；固定种子，PNG 不带时间戳与元数据。写出下列件、桌面、`theme.json`；写图前检查四个静区、按钮与面板外接矩形里没有装饰、九宫格中心纯色、拉伸边带沿边不变 |
| `DicePrototype/Assets/Resources/Themes/deco/UI/*.png` | 19 张（代表件 12 张：`ui_paper_panel`、`ui_panel_hud`、`ui_button_bevel`、`ui_chip_formula`、`ui_card_controller`、`ui_section_panel`、`ui_section_header`、`ui_dice_tray`、`ui_ring`、`ui_topbar`、`ui_stage_card`、`ui_wordmark_dicecraft`；同一画法出的变体 7 张：`ui_card_controller_compact`、`ui_card_shop`、`ui_card_item`、`ui_info_inset`、`ui_slot_inventory`、`ui_tab_bottom`、W13 的 `ui_card_frame_rarity`）。名称与像素尺寸等于默认件，RGBA |
| `…/deco/Table/table_scene.png` | 1920×1080：黑漆（= felt）、屏幕边一圈金色双线框、底边两角的深红扇角、动作行空处一把金色扇形 |
| `…/deco/Table/page_scene.png` | 1920×1080：同一块黑漆与屏幕金框，没有版面形状和大装饰 |
| `…/deco/Table/felt_tile.png` | 1024²，周期漆面细纹，均值即 felt 色 |
| `…/deco/theme.json` | id `deco`、order 4、preview true；24 个色板项加 4 个 W20X 项（生成器的 `PALETTE` 与 `W20X_TOKENS` 是唯一来源） |
| `tools/art_v11/deco/review_deco.py` | 离线示意（`build_deco.py --review [状态…]`），读已交付的件；不属于 build |
| `ArtSource/Art11/deco/review/` | `play_states.png` 与 `play_{a,f}_{100,150}.png`（对局，状态 a、f，100 % 与 150 % 文字）、`shop_w14.png`（W14 商店）、`palette.png`（色板与对比度）、`sprites.png`（各件与乘色样例）、`concept_sources.png`（概念图裁切与据此画的件并排）、`alternatives.png`（“需要决定”第 1、2 条的两种改法与现状并排） |

示意图里还没有画的 31 张件用默认件（游戏的回退顺序），每张图的标题里列出。卡面插画是默认风格的插画（W22 黑金重画不在本工作树），3D 骰子是平面替身。

## 形状与材质（取自哪张概念图）

- **对称切角八角形**，45° 切角、不前倾：roll、shop、select、card 四页的卡、面板、按钮都是这种轮廓。面板与卡切角 14 px（7 单位），按钮 18 px、筹码 20 px、托盘 23 px、环 24 px。与瑞士的小切角单线、漫画的墨线大切角加前倾和硬投影不同：黑金的切角配的是下面的金色双线和金属斜面，左右对称，没有投影。
- **金色双线**（card、roll 的卡框与左栏信息板）：外圈是抛光金的斜面带（10 px，光从左上：上沿与左沿的外半面亮、有一道高光线，下沿与右沿暗成古铜），然后黑漆间隔，再是一道细金线。卡上的细金线由 W13 的稀有度环代替（环在卡面边框内侧，按稀有度着色）。
- **深红楔角**（card 页插画窗四角与 roll 页屏幕四角的深红三角）：暗色面板与纸面板的细金线是一个矩形，它的四角各封着一个深红直角三角形，斜边是一道金色斜撑。三角只在 40 px 角块里（到 34.5 px 为止）。
- **漆牌**（shop 页的价签与购买钮、roll 页的按钮）：被代码着色的件（按钮、筹码、顶栏、本关牌、标题条、托盘、环）只用灰度画出斜面边和一道刻线，所以乘上任何色板色都读作一块漆牌或金属牌：乘深红是主按钮，乘金色是算式筹码与“已选”签。顶栏与本关牌的刻线是透空的，透出底下的黑漆，成为一道黑色嵌线。
- **箭头标题条**（shop 页“控制器卡牌”的深红标签，右端尖）：`ui_section_header` 右端是一个 45° 的尖，沿边一道刻线；乘 `caption` 深红。
- **扇形与阶梯座**（roll 页底部正中、select 页、card 页顶上的金色扇形）：只出现在桌面和字标上，从不放在会被拉伸的边带里。桌面上是动作行空处的一把 13 道光芒的扇形，整圈金线闭合，坐在两级阶梯座上，中心是深红半圆。
- **字标**（card 页“骰艺工坊”标题牌）：站酷小薇体（CP3-2 批准的黑金标题字）“骰子构筑”，金色渐变字在双线框的黑漆牌上，两侧三级金色阶梯肩，顶上扇形冠，左侧一颗象牙骰（黑、红、蓝点）。
- **材质**：黑漆（面板内 100D0A，桌面 15110D）、抛光金（古铜到高光的六级色阶）、象牙（F4E3C3）、深红（8E1B20）；全部取自 style_kit 的色板（ivory F8E5C6、gold D9A44E、oldGold B7916B、lacquer 15110D、crimson 7A120C、bronze 785C3E）与概念图实测（漆面 110E0A–15110E、深红牌 5F0B11–661315、象牙钮 F1D29F、金牌 F9C366）。

## 概念图特征 → 界面件

| 概念图特征（页） | 界面件 |
| --- | --- |
| 黑漆信息板、金色双线框（roll 左栏、shop 分区） | `ui_panel_hud`、`ui_section_panel`、`ui_tab_bottom`：黑漆面、10 px 金斜面带、黑漆间隔、细金线矩形、四角深红楔角 |
| 象牙卡面、金框、切角（roll、shop、card） | `ui_card_controller`（= `_compact`）、`ui_card_shop`：象牙面、金斜面带、黑漆间隔；`ui_card_item`：间隔改深红（道具） |
| 卡框里的细金线（card） | W13 `ui_card_frame_rarity`：黑漆细线、着色带、黑漆细线，共 6.7 px，切角 8 px 落在象牙面里 |
| 象牙卡、标题、纸面板（card、select） | `ui_paper_panel`：象牙面配同样的双线与楔角 |
| 象牙与深红的漆牌按钮（roll、shop、select） | `ui_button_bevel`（中性，代码乘色：主按钮深红、纸面钮象牙、禁用古铜）+ W20X 台阶色当金色托座 |
| 价签、购买钮（shop） | `ui_chip_formula`（中性：次要按钮、算式筹码、关卡签、骰池签、价格按钮） |
| 深红箭头标签（shop） | `ui_section_header`（中性，乘 `caption`） |
| 切角托盘与双线（triptych） | `ui_dice_tray`（中性，斜面边带，中心透空）+ 代码画在里面的 `ui_ring`（Ghost 金）成为第二道线 |
| 提交格“01–05”的金框（roll） | `ui_ring`：粗线、透空、细线的双线环 |
| 黑漆分数框（triptych 顶部“2400 / 3200”、roll 左栏） | `ui_info_inset`：深黑漆井，旧金细线 |
| 屏幕金框、角上深红扇、底部扇形（roll、select、shop） | `table_scene` 的屏幕双线框、底边两角深红扇角、动作行的扇形 |
| 标题牌（card、select） | `ui_wordmark_dicecraft` |

## 着色约定

被代码乘色的件（生成器 `NEUTRAL_NAMES`）只用灰度：R=G=B，实面中心纯白，所以画出来的颜色就是色板值，W11b 的“中心像素 × 着色”选字规则看到的也是它；斜面与刻线只比白色暗。顶栏、本关牌、环、托盘的透空线不带颜色。其余件是烘焙色，代码以白色显示（商店分区靠 `section` = FFFFFF 保持烘焙）。

## 色板与对比度

WCAG 2 相对亮度；ΔE 为 CIE76（唐三彩用的同一度量），CIEDE2000 另注。`python tools/art_v11/deco/build_deco.py --report` 打印下列数字（`palette_report()`）。

| token | 值 | 选择理由 | 关键对比 |
| --- | --- | --- | --- |
| felt | 15110D | 黑漆桌面（style_kit lacquer；概念图托盘底 15110E）。静区与托盘底就是它 | onFelt 8.31，textColor 15.64 |
| background | 0D0A08 | 最深的漆色：渐隐与遮罩；代码把它混进次要按钮 | — |
| panel | 332A20 | 烟熏漆：顶栏、对话框、次要按钮的底。比桌面亮一档，否则顶栏与次要按钮在黑漆桌面上没有边（见“需要决定”第 2 条） | textColor 11.71，muted 6.38；对桌面 1.34 |
| panelLight | 6E5434 | 古铜：普通关的关卡签与本关牌标题条、未到的章节点、禁用按钮面、托盘边的混色 | 字取 textColor 5.87；禁用按钮上的 muted 3.19；与 gold 3.55（已过与未到的章节点） |
| paper / ink | F4E3C3 / 18130E | 象牙与漆墨 | ink/paper 14.60 |
| textColor / muted | F7E9CC / C2AC83 | 象牙字、旧金次要字 | 见 panel |
| gold | E2B05A | 抛光金：价格、金币、高亮 | 字取 ink 9.30；gold 在 panel 上 7.10 |
| reward | EDC46A | 亮金：达标环、总分筹码 | — |
| primary | 9E1D22 | 深红：概念图的主动作（重掷、下一关、开始挑战）都是深红漆牌。金色在黑金风格里到处都是（每个框），作主按钮不突出 | LabelOn11 取 textColor 6.59（≥4.95，不加深） |
| boss | C4372D | 猩红：首领关的关卡签、本关牌标题、托盘双环 | 字 textColor 4.45，带字的色块由 LabelOn11 加深一档；与 primary CIEDE2000 9.8 |
| pressure | E8742E | 朱橙警示色，色相 22.6°，饱和度 0.80 | 字取 ink 6.12；与 primary CIEDE2000 30.8，与 boss 20.8 |
| danger | A6262B | 深红：错误与警示角标 | paper 上 5.67 |
| teal | 6DC4A0 | 玉绿：完成、正面数值（还能掷） | panel 上 6.73 |
| violet | A783D9 | 紫晶：附魔 | — |
| red / blue | EC655D / 55B6D9 | 骰点颜色，各风格固定（CP3-1，THEME_SPEC §2） | — |
| ghost | B8955A | 旧金：空提交格、托盘内线、描边按钮、商品的卡环 | felt 上 6.70 |
| shadow | 070605 | 漆影 | — |
| rarityCommon / Advanced / Rare | BDB6A8 / 3D9FB5 / 8D57CB | 白镴、青瓷蓝（style_kit teal 提亮）、紫晶；概念图 card 页“常见”是象牙金、“稀有”是紫 | 两两亮度比 1.53 / 2.39 / 1.57（≥1.4）；与 primary ΔE 69.1 / 92.9 / 85.1（≥40），CIEDE2000 46.4 / 63.6 / 37.2 |
| onFelt | C9A86A | 旧金：直接画在黑漆桌面上的序号、空格点、乘号 | felt 上 8.31 |
| valueBadge | 不填（= gold） | 数值角标用金；警示角标 danger。按第二阶段 `ui_chip_label` 的象牙面（F4E3C3）乘色：D6983C 对 A11F1C | CIEDE2000 42.4、亮度对比 3.23:1；字 ink 7.69 / textColor 6.45 |
| buttonStep / buttonStepPrimary（W20X） | 7A5C34 / B68A44 | 按钮下的台阶当金属托座：次要按钮古铜，主按钮金色（概念图的深红按钮镶在金框里） | — |
| section（W20X） | FFFFFF | 商店分区保持烘焙：黑漆面与金色双线 | 分区中心 100D0A：muted 8.78，textColor 16.13 |
| caption（W20X） | 8E1B20 | 深红标题条：商店分区标题与托盘的三个标签（概念图 shop 页的深红标签） | 字取 textColor 7.51（标签首选的 muted 只有 4.09，代码改用 textColor） |

## 桌面

- 四个静区与 felt 色偏差 0（生成器写图前检查，阈值 0）：

  ```text
  DECO_ZONE dice_area rect=52,374,1408,276 max_rgb_delta=0 limit=0 PASS
  DECO_ZONE formula_row rect=52,660,1408,60 max_rgb_delta=0 limit=0 PASS
  DECO_ZONE submission_rail rect=344,740,1136,202 max_rgb_delta=0 limit=0 PASS
  DECO_ZONE submit_info_text150 rect=32,740,360,202 max_rgb_delta=0 limit=0 PASS
  ```
- 生成器另查 11 个按钮与面板的外接矩形（两种字号的 L11、按钮台阶、达标环、卡弹起的 10 单位、托盘标签）：里面全是 felt 色，0 个像素不同。
- **屏幕金框**：4 px 斜面金带、1.6 px 黑漆、1.2 px 细金线，都在离屏幕边 7.5 px 以内：顶栏从 y 12、x 16 起，商店的分区左右留 16、上下 y 12–1072，都在框里。是闭合轮廓。
- **扇形**：动作行里每个状态都空着的矩形是 x 1276–1492、y 942–1080（下表，按 `GameApp.Play11` 的 L11 与 `DrawActions11`）。扇形连金线在 x 1295–1473、y 962–1066，四边至少留 19 单位；整圈金线闭合，坐在阶梯座上，没有被直线截断。

  | 状态 | 100 %：左侧元素右缘 → 右侧元素左缘 | 150 % |
  | --- | --- | --- |
  | a 已选骰子（锁定 / 取消选择） | 主按钮 1240 → 结束 1496 | 1276 → 1496 |
  | b3 计分中（无主按钮、无次要） | 提示框 544 → 跳过 1496 | 632 → 1496 |
  | c 首领关开始（掷骰） | 1240 → 1496 | 1276 → 1496 |
  | f 已锁定，可提交或重掷 | 1240 → 1496 | 1276 → 1496 |
  | 掷骰中（主按钮停用） | 1240 → 跳过 1496 | 1276 → 1496 |
  | 已达标（结束本关可点，奖励环） | 1240 → 环 1492 | 1276 → 1492 |
  | 首领关 | 1240 → 1496 | 1276 → 1496 |
  | 高压关 | 1240 → 1496 | 1276 → 1496 |

  （主按钮的台阶比矩形低 2 单位、宽度在矩形内；达标环在结束槽外 4 单位。第二阶段按全部状态出示意并在测试里核对。）
- **底边两角**：深红直角三角（腿长 24 px）、金边与三道金线，在提示框（x 从 32、y 到 1046）与结束钮（含环与台阶，x 到 1892、y 到 1044）之外。顶边两角离顶栏太近，不放。
- **卡牌栏下面**没有任何装饰（0、3、5、7、10 张卡时只露出黑漆，第二阶段出示意）。
- **`page_scene`**：只有黑漆和屏幕金框。非对局画面直接画在桌面上的字（`tools/art_v11/table_text.json`）在黑漆上：textColor 15.64、muted 8.31 以上（第二阶段用 `table_text_contrast.py deco` 核对）。

## 文字空间（C# `FrameInsets11` 镜像，sprite px）

| 件 | 黑金 | 默认 |
| --- | --- | --- |
| `ui_card_controller` / `_compact` | 14, 14, 14, 14 | 22, 29, 27, 22 |
| `ui_card_shop` | 14, 14, 14, 14 | 27, 34, 32, 27 |
| `ui_card_item` | 11, 11, 11, 11 | 22, 30, 28, 22 |
| `ui_panel_hud` / `ui_section_panel` / `ui_tab_bottom` / `ui_paper_panel` | 18, 18, 18, 18 | 10, 8, 6, 10 / 6, 6, 6, 6 / 6, 0, 6, 6 / 9, 18, 16, 9 |
| `ui_chip_formula` | 10, 10, 10, 10 | 6, 16, 10, 6 |
| `ui_button_bevel` | 12, 12, 12, 12 | 7, 12, 12, 7 |
| `ui_topbar` / `ui_stage_card` | 8 / 9 | 8, 30, 11, 8 / 19, 29, 27, 19 |
| W13 `ui_card_frame_rarity`（`RingInsets13`） | 7 | 12 |

卡框每边都不比默认厚，紧凑卡（280×117）的字区因此不比默认小（第二阶段在测试里按 `DrawCompactCard13` 的算式核对，并对照 W20Xb 记录的 C21、C22、C78、C79）。两行操作提示在 150 % 文字时，第二行字底离提示框内框 8.5 单位（100 % 时 11.8；要求 ≥5，离线估算，字形底按 0.42 em）。

## 重建与验证（第一阶段）

```powershell
& E:\anaconda3\python.exe -B tools/art_v11/deco/build_deco.py            # 约 90 秒
& E:\anaconda3\python.exe -B tools/art_v11/deco/build_deco.py --report   # 色板对比度
& E:\anaconda3\python.exe -B tools/art_v11/deco/build_deco.py --review a f   # 离线示意，读已交付的件
```

依赖：Python 3.11、Pillow 9.4、numpy 1.24、scipy 1.10；字体只用仓库内的 Noto Sans SC Bold、Noto Sans CJK SC、Liberation Sans 与站酷小薇体（`Fonts/Themes/deco/`，OFL）。

## 第一阶段没有做的（第二阶段）

- 其余 31 张件（图标、附魔章、特效、宝石、卡背、骰影、标签、进度条、`ui_chip_label`、`ui_frame_block`、`ui_button_ghost`、`ui_tray_front`、`ui_pip_dot`、`ui_icon_*`）；`preview.png`、`contact_sheet.png`、`card_template_review.png`。
- 测试 `tests/test_art_v11_deco.py`、严格审计、全量单测、两次重建逐字节一致。
- 其余状态（b3、c、掷骰中、已达标、首领、高压）与卡牌栏 0 / 3 / 5 / 7 / 10 张的示意。

## 已知风险

- **次要按钮几乎没有边**：代码给次要按钮乘 `lerp(background, panel, .55)` = 221C15，对黑漆桌面只有 1.11:1；按钮的形状靠它比桌面略亮和字（14.04:1）。顶栏 332A20 对桌面 1.34:1。
- **首领与主按钮同一色系**：主按钮深红 9E1D22、首领猩红 C4372D，CIEDE2000 9.8。首领关时托盘双环、关卡签和本关牌标题也是红的。
- **托盘边是古铜**：托盘由代码乘 `lerp(panel, panelLight, .55)` = 53412B（对桌面 1.93:1），亮金只能来自里面的 Ghost 环；做不出概念图那种亮金托盘框。
- 示意是 Python 模拟（线性光乘色、九宫格按 PPU 200 展开），不是截图；桌面 shader 的光池、颗粒、首领压暗叠在黑漆上的效果没有看。

## 需要决定

1. **主按钮用深红还是金色**。现在按概念图用深红（重掷、下一关、开始挑战都是深红漆牌）；其他套件的主按钮都是金或黄。改金色的话，主按钮在满是金框的画面里不突出，首领红与主按钮也就分开了。
2. **深色漆牌的亮度**。`panel` 332A20 让顶栏与对话框比桌面亮一档；次要按钮仍几乎与桌面同色（1.11:1）。可选：把 `background` 提到暗古铜 3A2E22，次要按钮 362C21 对桌面 1.38:1（`alternatives.png` 第三行），代价是渐隐与遮罩（`Art071Screens` 的进入遮罩、`Art07StatusDetails` 的继续遮罩）从暗古铜而不是黑色淡出；或保持现状。
3. **托盘上的三个标签也是深红**（`caption` 同时给商店标题条与托盘标签）。概念图 roll 页的“骰池”标签是黑漆金框；用深红是因为 `caption` 一个值要两处共用，商店标题条在概念图里是深红。
