# W20 黑金装饰艺术 UI kit

状态：**第二阶段完成，提交复核**（2026-11-18，分支 `codex/v1.1-deco`，起点 `codex/v1.1` `cd2fedb3`；第一阶段 `f11165e2`）。不是实机或美术 ACCEPTED：结论只覆盖“验收条件与结果”写明的方法，独立复核与实机由协调者安排。没有启动 Unity，没有用 GPT/Codex 生图，没有下载；没有改 C#、`.meta`、默认件、其他风格的件、字体或 `docs/STATE.md`。

输入：`docs/specs/v11/THEME_SPEC.md` §2（含 `onFelt`、`valueBadge`、W20X 的 `buttonStep` / `buttonStepPrimary` / `section` / `caption` 与“乘色只会调暗”）、§3（可选的 `Table/page_scene.png`）、§3.1（桌面构图）；`PLAY_LAYOUT.md` §1、`SHOP_LAYOUT.md` §2、`CARD_TEMPLATE.md` §3、§6；`tools/art_v11/theme_contract.json`（2026-11-18，94 张中有引用的 50 张）；`ArtSource/StyleKit/themes/deco/` 的五张概念图与 `style_kit.json`；STATE 里唐三彩、爆彩漫画、W20X、W20Xb 的记录；代码 `GameApp.Play11`、`Play11Score`、`Play11Contrast`（`FrameInsets11`、`LabelOn11`、`CueOn11`）、`Card11`、`Shop14`、`Art071Screens`、`UiKit`；协调者对第一阶段的审查决定（2026-11-18，见末节）。

## 范围与产物

生成器 [build_deco.py](build_deco.py) 一次写出 55 张 PNG 与 `theme.json`。全部在 4 倍画布上用程序绘制（精确多边形距离场与最近边法线、金属斜面的光照色阶、深红与象牙平涂），最后一次浮点 Lanczos 缩小；固定种子 20261118，PNG 不带时间戳或元数据。写图前检查：四个静区与 felt 色偏差 0、11 个按钮与面板外接矩形里 0 个像素不是 felt、九宫格中心纯色、拉伸边带沿边不变（着色件）或沿边逐像素 ≤ 6 级（烘焙件）、与契约同尺寸；画接触表时有一张引用件落回默认件就报错。着色件 R=G=B 等其余规则由测试检查。

| 路径 | 内容 |
| --- | --- |
| `DicePrototype/Assets/Resources/Themes/deco/UI/<name>.png` | 50 张 RGBA（契约里全部有引用的件），名称与像素尺寸逐项等于默认件 |
| `…/deco/Table/table_scene.png` | 1920×1080 RGB：黑漆（= felt）、屏幕边一圈金色双线框、底边两角深红扇角、动作行空处一把金色扇形 |
| `…/deco/Table/page_scene.png` | 1920×1080 RGB：同一块黑漆与屏幕金框，没有版面形状和大装饰（非对局画面的底） |
| `…/deco/Table/felt_tile.png` | 1024² RGB，周期漆面细纹，均值即 felt 色，无渐变 |
| `…/deco/preview.png` | 480×270：离线对局合成（状态 f，100 % 文字）缩小 4 倍 |
| `…/deco/theme.json` | id `deco`、displayName 黑金装饰艺术、order 4、preview true；24 个色板项加 4 个 W20X 项（生成器的 `PALETTE` 与 `W20X_TOKENS` 是唯一来源）。待定键 `buttonSecondary` 不写入（见下） |
| `ArtSource/Art11/deco/contact_sheet.png` | build 的一部分：50 组默认 / 黑金 / 黑金在桌面上（着色件乘典型色）；四张整屏合成（f 100 %、首领 100 %、高压 100 %、f 150 %）；小图标在真实尺寸与底色上（1 倍与 2 倍）与主菜单字标；桌面（静区、外接矩形、动作行空处标出）与 page_scene；色板与对比度；五张概念图 |
| `ArtSource/Art11/deco/card_template_review.png` | W13 + W14 卡面模板复核图（`--card-review`，不属于 build）：商店 220×348、桌面 216×244、紧凑 280×117 × 常见 / 进阶 / 稀有 / 商品，紧凑卡的四条最短短文（C21、C22、C78、C79），商品行 452×103 / 448×121，构筑小格 100×104，1080p 与 720p，黑金在左、默认在右；三种宝石在卡顶边框上 12–34 px |
| `ArtSource/Art11/deco/review/` | 离线示意（`--review`，不属于 build），见“离线示意” |
| `tools/art_v11/deco/review_deco.py` | 示意的画法：按游戏代码排版（L11、DrawActions11、DrawCard13、Shop14 S14、Menu071），精灵按游戏的导入方式取样（双线性、无 mipmap），线性光乘色与混合 |
| `tests/test_art_v11_deco.py` | 44 项独立检查（见“测试”） |

## 形状与材质（取自哪张概念图）

- **对称切角八角形**，45° 切角、不前倾：roll、shop、select、card 四页的卡、面板、按钮都是这种轮廓。面板与卡切角 16 px，道具卡、空格与角标小牌 12 px，顶栏 14 px，本关牌与按钮 18 px、筹码 20 px、托盘 23 px、环 24 px。与瑞士的小切角单线、漫画的墨线大切角加前倾和硬投影、三彩的圆弧角都不同：测试对 20 个件拟合角上的轮廓，必须是一条 45° 直线。
- **金色双线**（card、roll 的卡框与左栏信息板）：外圈是抛光金的斜面带（面板 13 px、卡 14 px、道具卡 10 px）：外缘一道亮边、斜面受光一侧的高光线、平面、内缘暗成古铜；然后黑漆间隔（道具卡是深红间隔），面板再有一道细金线。光从左上，但金带沿边一致（拉伸时不变形），明暗只在法线方向。卡上的细金线由 W13 的稀有度环代替。
- **深红楔角**（card 页插画窗四角与 roll 页屏幕四角的深红三角）：暗色面板与纸面板的细金线在四角各封着一个深红直角三角形，斜边是一道金色斜撑。三角只在 40 px 角块里。
- **漆牌**（shop 页的价签与购买钮、roll 页的按钮）：被代码着色的件（按钮、筹码、顶栏、本关牌、标题条、托盘、环、描边钮、进度条）只用灰度画出斜面边和一道刻线，乘上任何色板色都读作一块漆牌或金属牌：乘深红是主按钮，乘金是算式筹码与“已选”签，乘古铜（待定的 `buttonSecondary`）是次要按钮与价格钮。顶栏与本关牌的刻线是透空的，透出底下的黑漆，成为一道黑色嵌线。
- **箭头**（shop 页“控制器卡牌”的深红标签右端尖；进度条两端）：`ui_section_header` 右端是一个对称的尖（中线上），乘 `caption` 深红；`ui_progress_*` 两端都是尖。
- **扇形与阶梯座**（roll 页底部正中、select 页、card 页顶上的金色扇形）：只出现在桌面、卡背和字标上，从不放在会被拉伸的边带里。
- **徽章**（card 页角上的八角金章）：五个附魔章是金边八角章，珐琅底保留默认徽章的含义色（玉绿 +10、紫晶 ×2、宝蓝 +1 掷、黑漆无格、象牙 +1 提交），象牙符号加漆墨描边。
- **字标**（card 页“骰艺工坊”标题牌）：站酷小薇体（CP3-2 批准的黑金标题字）“骰子构筑”，金色渐变字在双线框的黑漆牌上，两侧三级金色阶梯肩，顶上扇形冠，左侧一颗象牙骰（黑、红、蓝点）。在主菜单里放在象牙纸面板（`ui_paper_panel`）上，读作镶在象牙卡上的漆牌（接触表右侧小图）。
- **材质**：黑漆（面板内 100D0A，桌面 15110D）、抛光金（3E2A10 → 7A5420 → B5843A → DDAA52 → F2CC79 → FCE6A8 → FFF6DC 七级色阶）、象牙（F4E3C3）、深红（8E1B20）、古铜（785C3E）；取自 style_kit 的色板（ivory F8E5C6、gold D9A44E、oldGold B7916B、lacquer 15110D、crimson 7A120C、bronze 785C3E）与概念图实测（漆面 110E0A–15110E、深红牌 5F0B11–661315、象牙钮 F1D29F、金牌 F9C366）。

## 概念图特征 → 界面件

| 概念图特征（页） | 界面件 |
| --- | --- |
| 黑漆信息板、金色双线框（roll 左栏、shop 分区） | `ui_panel_hud`、`ui_section_panel`、`ui_tab_bottom`：黑漆面、13 px 金斜面带、黑漆间隔、细金线、四角深红楔角 |
| 象牙卡面、金框、切角（roll、shop、card） | `ui_card_controller`（= `_compact`）、`ui_card_shop`：象牙面、14 px 金斜面带、黑漆间隔；`ui_card_item`：10 px 金带、深红间隔（道具） |
| 卡框里的细金线（card） | W13 `ui_card_frame_rarity`：黑漆细线、着色带、黑漆细线（1.5 + 5 + 1.5 px），切角 9 px 落在象牙面里 |
| 稀有度宝石（card 页卡顶的宝石） | `ui_gem_common` 圆、`_advanced` 菱、`_rare` 六角星：刻面宝石，漆墨外框，浅色刻面与白色台面（着色件） |
| 象牙卡、标题、纸面板（card、select） | `ui_paper_panel`：象牙面配同样的双线与楔角 |
| 象牙与深红的漆牌按钮（roll、shop、select） | `ui_button_bevel`（中性，代码乘色：主按钮深红、纸面钮象牙、禁用古铜）+ W20X 台阶色当金属托座 |
| 价签、购买钮（shop） | `ui_chip_formula`（中性：次要按钮、算式筹码、关卡签、骰池签、价格按钮）；`ui_chip_label`：象牙珐琅小牌、漆墨细边（数值与警示角标、同款签） |
| 描边按钮、细框（select） | `ui_button_ghost`（双线描边、中心透空）、`ui_frame_block`（白色切角块，代码乘色作细边） |
| 深红箭头标签（shop） | `ui_section_header`（中性，乘 `caption`） |
| 切角托盘与双线（triptych） | `ui_dice_tray`（中性，斜面边带，中心透空）+ 代码画在里面的 `ui_ring`（Ghost 金）成为第二道线；`ui_tray_front`：黑漆前沿、金色上唇 |
| 提交格“01–05”的金框（roll） | `ui_ring`：粗线、透空、细线的双线环 |
| 黑漆分数框（triptych 顶部“2400 / 3200”、roll 左栏） | `ui_info_inset`：深黑漆井，旧金细线；`ui_slot_inventory`：黑漆凹格、旧金边 |
| 金币、骰杯、锁、钟（roll、shop） | `ui_icon_coin`（金币，深红菱心）、`ui_icon_roll`（象牙骰杯、金箍、金箭头）、`ui_icon_lock` / `ui_tag_lock`（金锁、漆墨锁孔）、`ui_tag_clock`（八角金钟、象牙盘面）、`ui_icon_dice_view`（两颗象牙骰与金放大镜） |
| 线性图标（shop 顶栏） | `ui_icon_arrow`、`ui_icon_book`、`ui_icon_gear`、`ui_icon_refresh`、`ui_pip_dot`（中性，代码乘字色） |
| 金冠、星芒（card、select） | `fx_score_crown`（金冠、深红宝石）、`fx_score_spark`（四芒星，中性）、`fx_arc_short`（端头菱形的弧，中性）、`fx_square_chip`（中性方片） |
| 卡背（card 页卡牌背面的扇形与章） | `ui_card_back`：黑漆、金色放射扇、中央八角章里一颗象牙骰；`ui_die_shadow`：柔和的椭圆影 |
| 屏幕金框、角上深红扇、底部扇形（roll、select、shop） | `table_scene` 的屏幕双线框、底边两角深红扇角、动作行的扇形 |
| 标题牌（card、select） | `ui_wordmark_dicecraft` |

## 着色约定

被代码乘色的件（生成器与测试各自列出的 23 张 `NEUTRAL_NAMES`）只用灰度：R=G=B，实面中心纯白，所以画出来的颜色就是色板值，W11b 的“中心像素 × 着色”选字规则看到的也是它；斜面与刻线只比白色暗（“乘色只会调暗”）。顶栏、本关牌、环、托盘的透空线不带颜色。其余件是烘焙色，代码以白色显示（商店分区靠 `section` = FFFFFF 保持烘焙）。

## 色板与对比度

WCAG 2 相对亮度；ΔE 为 CIE76，CIEDE2000 另注。`python tools/art_v11/deco/build_deco.py --report` 打印下列数字（`palette_report()`）。

| token | 值 | 选择理由 | 关键对比 |
| --- | --- | --- | --- |
| felt | 15110D | 黑漆桌面（style_kit lacquer；概念图托盘底 15110E）。静区与托盘底就是它 | onFelt 8.31，textColor 15.64 |
| background | 0D0A08 | 最深的漆色：渐隐与遮罩 | — |
| panel | 332A20 | 烟熏漆：顶栏、对话框 | textColor 11.71，muted 6.38；对桌面 1.34 |
| panelLight | 6E5434 | 古铜：普通关的关卡签与本关牌标题条、未到的章节点、禁用按钮面、托盘边的混色、商店格子 | 字取 textColor 5.87；禁用按钮上的 muted 3.19；与 gold 3.55 |
| paper / ink | F4E3C3 / 18130E | 象牙与漆墨 | ink/paper 14.60 |
| textColor / muted | F7E9CC / C2AC83 | 象牙字、旧金次要字 | 见 panel |
| gold | E2B05A | 抛光金：价格、金币、高亮 | 字取 ink 9.30；gold 在 panel 上 7.10 |
| reward | EDC46A | 亮金：达标环、总分筹码 | — |
| primary | 9E1D22 | 深红：概念图的主动作（重掷、下一关、开始挑战）都是深红漆牌（决定 1 保留） | LabelOn11 取 textColor 6.59（≥4.95，不加深） |
| boss | EB2921 | 朱红：首领关的关卡签、本关牌标题与徽记、托盘与本关牌的双环、算式里的首领因子（决定 1，见“首领色”） | 与 primary CIEDE2000 16.9，牌面 19.98；与 pressure 16.7 |
| pressure | E8742E | 朱橙警示色，色相 22.6°，饱和度 0.80 | 字取 ink 6.12；与 primary CIEDE2000 30.8 |
| danger | A6262B | 深红：错误与警示角标 | paper 上 5.67；与 boss CIEDE2000 15.4 |
| teal | 6DC4A0 | 玉绿：完成、正面数值（还能掷） | panel 上 6.73 |
| violet | A783D9 | 紫晶：附魔 | — |
| red / blue | EC655D / 55B6D9 | 骰点颜色，各风格固定（CP3-1，THEME_SPEC §2） | — |
| ghost | B8955A | 旧金：空提交格、托盘内线、描边按钮、商品的卡环 | felt 上 6.70 |
| shadow | 070605 | 漆影 | — |
| rarityCommon / Advanced / Rare | BDB6A8 / 3D9FB5 / 8D57CB | 白镴、青瓷蓝、紫晶；概念图 card 页“常见”是象牙金、“稀有”是紫 | 两两亮度比 1.53 / 2.39 / 1.57（≥1.4）；与 primary ΔE 69.1 / 92.9 / 85.1（≥40），CIEDE2000 46.4 / 63.6 / 37.2 |
| onFelt | C9A86A | 旧金：直接画在黑漆桌面上的序号、空格点、乘号 | felt 上 8.31 |
| valueBadge | 不填（= gold） | 数值角标用金；警示角标 danger。按 `ui_chip_label` 的象牙面（中心 F4E3C3）乘色：D89C43 对 9F211E | CIEDE2000 42.35、亮度对比 3.23:1；字 ink 7.69 / textColor 6.45 |
| buttonStep / buttonStepPrimary（W20X） | 7A5C34 / B68A44 | 按钮下的台阶当金属托座：象牙钮下古铜，深红主按钮下金（概念图的深红按钮镶在金框里）。不填时代码用 8F453A / B56B29，红褐与橙在黑金里不对 | 对桌面 3.05 / 6.00 |
| section（W20X） | FFFFFF | 商店分区保持烘焙：黑漆面与金色双线（不填时代码会给分区乘色，金框会被压暗） | 分区中心 100D0A：muted 8.78，textColor 16.13 |
| caption（W20X） | 8E1B20 | 深红标题条：商店分区标题与托盘的三个标签（决定 3 接受两处共用） | 字取 textColor 7.51（标签首选的 muted 只有 4.09，代码改用 textColor） |
| buttonSecondary（W20Y，**待定，未写入 theme.json**） | 7A5C30 | 中间调古铜漆牌：每个 SolidButton11 的面（次要按钮、价格钮、查看骰子、商店的刷新 / 手册 / 设置） | 对桌面 3.04、对 paper 4.89；字 textColor 5.14（LabelOn11 0 步） |

### 首领色（决定 1）

主按钮保留深红 9E1D22，首领从猩红 C4372D 改为朱红 EB2921：

| 量 | 数值 | 要求 |
| --- | --- | --- |
| 与 primary 的 CIEDE2000 | 16.93 | ≥15 |
| LabelOn11 画出的首领牌面（白面 × 首领，向白 3 步成 EE4841）与 primary | 19.98 | ≥15 |
| 与 pressure E8742E | 16.71 | 明显分开（测试按 ≥15） |
| 牌面上的字（ink，LabelOn11 选中后把牌面提亮 3 步） | 4.95 | ≥4.95 |
| 托盘双环与本关牌双环：对桌面 / 对 paper | 4.36 / 3.41 | 非文字 ≥3 |
| 徽记上的 ink 档位条 | 4.28 | 非文字 ≥3 |
| 与红骰点 EC655D | 11.31 | 参考：默认风格的首领 C8463F 与红点 11.1 |
| 与 danger A6262B | 15.38 | — |

搜索记录：要同时离 primary ≥15、离 pressure ≥15、牌面画出来也 ≥15、在桌面上 ≥2.4:1，符合的颜色集中在两处：朱红（CIELCh 色相 30–40°，EB2921 是 37.2°，即本选择）和品红。品红离骰点更远，但在概念图里没有出处；朱红是概念图深红牌与金之间的颜色，取它。代价是与红骰点只差 11.3（与默认风格同级），靠位置（骰点在骰子上，首领色在关卡签、标题条和环上）区分；算式行里手型因子（红）与首领因子相邻，那里只差 5.8，见“已知风险”。示意：`review/boss_stage.png`（终局档首领、算式里的首领因子 ×0.8，100 % 与 150 %）、`play_boss_*`、`play_c_*`（二档首领开始一手）。

### 次要按钮（决定 2，W20Y 待定键）

`buttonSecondary` = 7A5C30（中间调古铜漆牌），对桌面 3.04:1、对 paper 4.89:1，字 textColor 5.14:1（LabelOn11 不调整牌面）。现在的代码给每个 SolidButton11 乘 `lerp(background, panel, .55)` = 221C15，对桌面只有 1.11:1。按协调者的要求：值放在生成器的 `PENDING_TOKENS` 与本文，不写入 `theme.json`（本工作树的 `ThemePalette.cs` 还没有这个键：JsonUtility 会跳过它，结构测试也要求每个色板键都已声明）；W20Y 合并后由协调者加入。加入时请改生成器再重建 `theme.json`（例如 `theme_json()` 把 `PENDING_TOKENS` 一并写入；`PENDING_TOKENS` 本身要留着，`review_deco.py` 与 `palette_report()` 读它）：手改 `theme.json` 会让 `test_shipped_theme_json_matches_regeneration` 失败；交付的 PNG 不受影响（示意本来就按这个值画）。结构测试的可选键集合已含 `buttonSecondary`（与 `valueBadge`、`buttonStep`、`buttonStepPrimary`、`section`、`caption` 同列），必需键在合并前（29 个声明键）与合并后（30 个）都是 24 个，两种情况都通过。所有画到它的示意都按这个值画，说明里写着“simulated buttonSecondary 7A5C30”（`palette.png` 里标为 pending）。

禁用时代码把面混入 25 % panelLight（7A5C30 → 775A31，与可用的面只差 CIEDE2000 1.09）。协调者决定（W20Y2）：禁用的 SolidButton11 字改由 `LabelOn11(face, Muted)` 选（现在的代码直接用 Muted，在 775A31 上只有 2.89:1；`ShopButton14` 已经这样选），示意按这个规则画（`review_deco.py` 的 `Game.solid`）。在这块面上 Muted 不到 4.5:1，LabelOn11 改用 textColor（5.31:1），所以禁用的次要按钮与可用的看起来几乎一样：面差 1.09、字同为 textColor（现在的 221C15 面上禁用面 352A1D、Muted 6.35:1，没有这个问题）。示意里没有禁用的次要按钮（商店的钱够买全部商品），这一点只有数字。禁用态如何区分需要决定，见“需要的决定”。

## 桌面

- 四个静区与 felt 色偏差 0（生成器写图前检查，阈值 0；测试再查）：

  ```text
  DECO_ZONE dice_area rect=52,374,1408,276 max_rgb_delta=0 limit=0 PASS
  DECO_ZONE formula_row rect=52,660,1408,60 max_rgb_delta=0 limit=0 PASS
  DECO_ZONE submission_rail rect=344,740,1136,202 max_rgb_delta=0 limit=0 PASS
  DECO_ZONE submit_info_text150 rect=32,740,360,202 max_rgb_delta=0 limit=0 PASS
  ```
- 11 个按钮与面板的外接矩形（两种字号的 L11、按钮台阶、达标环、卡弹起的 10 单位、托盘标签）：里面 0 个像素不是 felt（生成器与测试各查一次）。
- **屏幕金框**：从离屏幕边 0.3 px 起依次是 4.8 px 斜面金带、1.4 px 黑漆、1.2 px 细金线，几何上止于 7.7 px；缩小滤波后第 9、10 个像素与 felt 还差 14 与 2 级，第 11 个像素起就是 felt。14 px 的切角处最远到离屏幕边第 14 个像素，都在 30 px 的角块里。顶栏从 y 12、x 16 起，商店分区左右留 16、上下 y 12–1072，都在框里。闭合八角轮廓。
- **扇形**：动作行里每个状态都空着的矩形是 x 1276–1492、y 942–1080（`action_row.txt`，按 `GameApp.Play11` 的 L11 与 `DrawActions11`，9 个状态 × 2 种字号）。扇形连轮廓在 x 1291–1477、y 958–1069（像素边界；与 page_scene 不同的像素是 x 1291–1476、y 958–1068），左右各留 15、顶上 16；13 道光芒、深红半圆心，整圈 2A1E10 暗轮廓闭合（6032 个亮金像素离桌面至少 2.0 px），站在两级阶梯座上；阶梯座底边与屏幕金框的细金线之间留 3 px 黑漆（y 1068–1070）。

  | 状态 | 100 %：左侧元素右缘 → 右侧元素左缘 | 150 % |
  | --- | --- | --- |
  | a 已选骰子（锁定 / 取消选择） | 主按钮 1240 → 结束 1496 | 1276 → 1496 |
  | b3 计分中（无主按钮、无次要） | 提示框 544 → 跳过 1496 | 632 → 1496 |
  | c 首领关开始（掷骰） | 1240 → 1496 | 1276 → 1496 |
  | f 已锁定，可提交或重掷 | 1240 → 1496 | 1276 → 1496 |
  | 掷骰中（主按钮停用） | 1240 → 跳过 1496 | 1276 → 1496 |
  | 已达标（结束本关可点，奖励环） | 1240 → 环 1492 | 1276 → 1492 |
  | 首领关（满员提示，两行） | 1240 → 1496 | 1276 → 1496 |
  | 高压关 | 1240 → 1496 | 1276 → 1496 |
  | 首领关进行中（终局档，首领因子） | 1240 → 1496 | 1276 → 1496 |
- **底边两角**：深红直角三角（腿长 24 px）、金边与三道金线，在提示框（x 从 32、y 到 1046）与结束钮（含环与台阶，x 到 1892、y 到 1044）之外（与 page_scene 不同的像素：左 x 6–35、右 x 1884–1913，y 1044–1073；两个外接矩形里 0 像素；亮金离桌面至少 2.8 px）。顶边两角离顶栏太近，不放。
- **卡牌栏下面**没有任何装饰：0、3、5、7、10 张卡时露出的都是黑漆（`review/card_rows.png`，卡牌栏 32, 86, 1448, 254 里 0 个像素不是 felt）。
- **`page_scene`**：只有黑漆和屏幕金框（与 table_scene 除扇形与扇角外逐像素相同）。非对局画面直接画在桌面上的 19 段字（`tools/art_v11/table_text.json`）：`table_text_contrast.py deco` 最差 8.52:1，0 段低于 4.5:1，`TABLE_TEXT_OK`。

## 文字空间（C# `FrameInsets11` 镜像，sprite px；PPU 200 时 2 px = 1 单位）

全部 24 个有九宫格或横向切片边距的件（左、下、右、上；一个数表示四边相同；第一阶段 `f11165e2` 的值附在括号里）：

| 件 | 黑金 | 默认 | 每边 ≤ 默认 |
| --- | --- | --- | --- |
| `ui_card_controller` / `_compact` | 18（14） | 22, 29, 27, 22 | 是 |
| `ui_card_shop` | 18（14） | 27, 34, 32, 27 | 是 |
| `ui_card_item` | 15（11） | 22, 30, 28, 22 | 是 |
| `ui_stage_card` | 9（9） | 19, 29, 27, 19 | 是 |
| `ui_topbar` | 8（8） | 8, 30, 11, 8 | 是 |
| `ui_chip_label` | 4 | 5 | 是 |
| `ui_slot_inventory` | 8（8） | 40（半透明中心） | 是 |
| `ui_progress_track` | 3, 0, 3, 0 | 6, 0, 6, 0 | 是 |
| W13 `ui_card_frame_rarity`（`RingInsets13`） | 9（7） | 12 | 是 |
| `ui_ring` / `ui_button_ghost` / `ui_dice_tray` / `ui_card_frame_rarity`（`FrameInsets11`） | 40 / 40 / 72 / 40（透空中心，等于切片边距） | 同左 | 相等 |
| `ui_panel_hud` / `ui_section_panel` / `ui_tab_bottom` / `ui_paper_panel` | 21（18） | 10, 8, 6, 10 / 6, 6, 6, 6 / 6, 0, 6, 6 / 9, 18, 16, 9 | 否：四边 |
| `ui_chip_formula` | 10（10） | 6, 16, 10, 6 | 否：左、上 |
| `ui_button_bevel` | 12（12） | 7, 12, 12, 7 | 否：左、上 |
| `ui_info_inset` | 7（7） | 0, 3, 3, 0 | 否：四边 |
| `ui_tray_front` | 8, 0, 8, 0 | 5, 0, 5, 0 | 否：左、右 |
| `ui_progress_fill` | 3, 0, 3, 0 | 0, 0, 6, 0 | 否：左 |
| `ui_section_header` | 11, 0, 19, 0（同） | 0, 0, 32, 0 | 否：左 |
| `ui_frame_block` | 1 | 0 | 否：四边 |

- 按决定 4 的字面（每个边距不超过默认）：卡、道具卡、本关牌、顶栏、数值角标、空格、进度槽与 W13 稀有度环每边都不超过默认（测试逐边核对四种卡面、本关牌、顶栏、数值角标与稀有度环）；面板四件、筹码、按钮、信息井、托盘前沿、进度条填充、分区标题条与框块共 11 件至少有一边厚于默认（上表“否”），见“需要的决定”。面板的金框比默认的细框厚（与唐三彩、爆彩漫画一样），它们的文字空间按用处核对：两行操作提示在 150 % 文字时，第二行字底离提示框内框 7.0 单位（100 % 时 10.3；要求 ≥5；离线估算，行距 1.37 em、字形底在行中线下 0.42 em）；商店分区标题条留 55.5 单位高（要求 ≥50）；34 单位的托盘标签留 24 单位（要求 ≥20）。
- 紧凑卡（280×117）：四条最短短文 C21“提交栏+1格。”、C22“提交栏+2格。”、C78“每手投掷次数+1。”、C79“每关提交次数+1。”在黑金与默认里都显示（1080p 与 720p；黑金字区 57.0×168.0 单位，默认 49.5×162.0）。注意这是离线估算：W20Xb 记录过漫画的框按镜像算也比默认薄，实机里这四条却被隐藏，所以实机前不能当作结论。

## 离线示意（`ArtSource/Art11/deco/review/`）

全部是 Python 按游戏代码的排版模拟，不是 Unity 截图；卡名、短文、插画（默认风格的插画，W22 黑金重画不在本工作树）与价格是替身，3D 骰子是平面替身（骰子下的影子是一块硬边的暗矩形，替身画法，不是 `ui_die_shadow`）；画到 buttonSecondary 的图，说明里都写着 “simulated buttonSecondary 7A5C30”（`palette.png` 标为 pending）。

| 文件 | 内容 |
| --- | --- |
| `play_<状态>_<100|150>.png`、`play_states.png` | 9 个状态 × 两种文字大小：a、b3、c、f、rolling（掷骰中）、qualified（已达标）、boss（满员提示）、pressure、boss_play（终局首领进行中）；首领关按代码压暗桌面（`_Dim11` .26，近似） |
| `card_rows.png` | 卡牌栏 0、3、5、7、10 张（10 张为两排紧凑卡） |
| `action_row.png`、`action_row.txt` | 动作行在全部状态与两种字号下的矩形、扇形外框是否在空处、提示字底的余量 |
| `boss_stage.png` | 首领关整屏，100 % 与 150 %，附首领色的数字 |
| `shop_w14.png` | W14 商店（`Shop14` S14，100 % 文字）画在 page_scene 上，附分区、标题条、按钮的对比度 |
| `palette.png`、`concept_sources.png` | 色板与对比度；概念图裁切与据此画的件并排 |

第一阶段的 `alternatives.png`（决定 1、2 的备选）与 `sprites.png`（19 件的样例）已被决定与接触表取代，从目录里删掉（在 `f11165e2` 里可取回）。

## 重建与验证

```powershell
& E:\anaconda3\python.exe -B tools/art_v11/deco/build_deco.py                  # 全部交付件与接触表，约 3 分钟
& E:\anaconda3\python.exe -B tools/art_v11/deco/build_deco.py --report         # 色板对比度
& E:\anaconda3\python.exe -B tools/art_v11/deco/build_deco.py --review         # 离线示意（读已交付的件）
& E:\anaconda3\python.exe -B tools/art_v11/deco/build_deco.py --card-review    # 卡面模板复核图
& E:\anaconda3\python.exe -B tools/art_v11/theme_contract.py audit deco --strict
& E:\anaconda3\python.exe -B tools/art_v11/table_text_contrast.py deco
& E:\anaconda3\python.exe -m unittest tests.test_art_v11_deco -v
& E:\anaconda3\python.exe -m unittest discover -s tests -v
```

依赖：Python 3.11、Pillow 9.4、numpy 1.24、scipy 1.10；字体只用仓库内的 Noto Sans SC Bold、Noto Sans CJK SC、Liberation Sans 与站酷小薇体（`Fonts/Themes/deco/`，OFL）。

## 测试（`tests/test_art_v11_deco.py`，44 项）

清单（50 件、55 张 PNG）与尺寸、RGBA、不全透明；着色件 R=G=B、白色实面、开口件中心全透明；九宫格中心从边距线起纯色（20 件）、横向切片中段逐列相同（4 件）；拉伸边带（9 个着色件沿边不变，11 个烘焙件沿边逐像素 ≤6 级）；C# 框厚镜像与文字空间（卡、本关牌、顶栏、角标不比默认厚，其余件的上限），150 % 两行提示的余量 ≥5 单位；20 个件的角是 45° 直切角；金框是有高光与暗边的金属（亮度 ≥0.70 与 <0.12 同在）；面板四角有深红楔角而拉伸段没有；标题条与进度条的尖在中线上；内层件不出宿主轮廓（稀有度环、本关牌双环、托盘环、商店标题条）；带字面板的对比度；锁与小图标在真实尺寸与底色上（含待定的 buttonSecondary 面）；稀有度环带宽不超过默认、两侧漆线；宝石轮廓点数、小尺寸可分、墨线与浅色刻面；桌面尺寸、静区、外接矩形内全是 felt、装饰只有屏幕框、扇角与一把扇形、装饰有闭合暗轮廓、屏幕框是闭合金色双线；page_scene 只有黑漆与同一屏幕框；felt_tile 无缝均匀；色板规则（AX-08、onFelt、pressure 警示色、稀有度、主按钮）；首领色（决定 1）；待定的 buttonSecondary 不在 `theme.json`、对比度达标（决定 2）；数值与警示角标；W20X 四个键；`theme.json` 结构（键名从 `ThemePalette.cs` 读取）与生成一致；概念图 SHA 与来源表一致；字标用批准的标题字；两次完整重建逐字节一致且与交付件一致。

## 验收条件与结果

实施者自测；独立复核与实机由协调者安排。

| ID | 方法与期望 | 证据 | 结果 |
| --- | --- | --- | --- |
| W20-01 | 50 件齐全、RGBA、与默认件同尺寸；strict 审计 0 缺件 | `theme_contract.py audit deco --strict`、单测 | 通过：`50 sprites shipped, 0 referenced defaults not overridden`，`OPTIONAL Table/page_scene 1920x1080`，`AUDIT_OK` |
| W20-02 | 九宫格中心纯色；横向切片中段逐列相同；拉伸边带规则；框厚（C# 算法）在边距内且留出文字空间；卡框不比默认厚 | 单测 | 通过：20 个四边九宫格、4 个横向切片；着色件沿边不变；烘焙件沿边逐像素最大 0 级（规则 ≤6）；卡 18 / 道具 15 px ≤ 默认 |
| W20-03 | 着色件 R=G=B；有实面的着色件中心纯白；开口件中心全透明 | 单测 | 通过：23 / 5 / 4 件 |
| W20-04 | 四个静区偏差 0；外接矩形内无装饰；动作行装饰只在全状态空处、闭合；卡牌栏下无装饰；page_scene 平 | 生成器检查、单测、`action_row.txt`、`card_rows.png` | 通过：4/4 偏差 0；11 个矩形 0 像素；扇形在 x 1291–1477（空处 1276–1492），18 个状态 × 字号都在空处；卡牌栏 0 / 3 / 5 / 7 / 10 张下 0 像素 |
| W20-05 | 色板：红蓝原值、AX-08、onFelt、pressure 警示色、稀有度亮度与离 primary 的距离、主按钮字 ≥4.95、角标、W20X 键；`theme.json` 结构 | 单测、`palette_report()` | 通过，数字见色板表 |
| W20-06 | 首领离 primary ≥15 CIEDE2000（牌面也是）、离 pressure 明显、字 ≥4.95、环与徽记可读 | 单测、`boss_stage.png` | 通过：16.93 / 19.98 / 16.71；字 4.95；环 4.36 / 3.41，档位条 4.28。另见风险：算式行里首领因子与手型因子只差 5.8 |
| W20-07 | buttonSecondary：对桌面、对 paper 各 ≥3:1，字 ≥4.95；不写入 `theme.json`；示意里注明模拟；结构测试合并前后都通过 | 单测、示意 | 通过：3.04 / 4.89，字 5.14；`theme.json` 无此键；可选键集合含它。另见风险：禁用态与可用态只差 1.09 |
| W20-08 | 两行提示 150 % 时字底余量 ≥5 单位 | 单测（离线估算）、`action_row.txt` | 通过：7.0 单位（100 % 10.3） |
| W20-09 | 小图标在真实尺寸与宿主上可辨；锁 20×22 在 background 上 | 单测、接触表小图 | 通过（离线合成） |
| W20-10 | 字标用站酷小薇体 | 单测、接触表 | 通过 |
| W20-11 | 非对局画面桌面上的字 ≥4.5:1 | `table_text_contrast.py deco` | 通过：19 段，最差 8.52:1，`TABLE_TEXT_OK` |
| W20-12 | 两次完整重建逐字节一致，且与交付件一致 | 单测（setUpClass 两次 build）；另做两次完整 build 到工作树内的临时目录（低优先级，各 186 / 168 秒）；`--review` 与 `--card-review` 重画到临时目录 | 通过：两次 build 的 56 个文件（55 张 PNG 与 `theme.json`）彼此逐字节相同，且与交付件相同，交付目录里没有 build 不写的文件；示意 26 个文件（25 张图与 `action_row.txt`）和 `card_template_review.png` 重画后与交付件逐字节相同 |
| W20-13 | 黑金单测、全量 `unittest discover -s tests -v` | 本机日志，BelowNormal 优先级；每次开跑前确认没有命令行含 `V072BatchTests.Run` 的 Unity 在跑（三次都遇到，等它结束才开始） | 通过：黑金 `Ran 44 tests in 300.575s`，`OK`；全量 `Ran 378 tests in 889.186s`，`OK`（0 失败、0 错误、0 跳过） |
| W20-14 | 只改本包路径；其他风格与默认件逐字节不变 | `git diff --stat cd2fedb3`（排除本包四处路径后为空）、`git ls-files --others` | 通过：改动只在 `tools/art_v11/deco/`、`tests/test_art_v11_deco.py`、`DicePrototype/Assets/Resources/Themes/deco/`、`ArtSource/Art11/deco/`（`illustrations/` 未动）；没有 `.meta`；其他风格、默认件、C# 0 改动 |
| W20-15 | 风格忠于概念图、纹理克制、示意在全部状态可读 | 实施者对照五张概念图、接触表、全部示意自查 | 自查完成，待独立复核与用户判断 |
| W20-16 | 决定 4：金色更接近概念图；每个边距不超过默认；150 % 两行提示下 ≥5 单位 | “文字空间”表（C# 镜像）、单测 | 部分：金色加强（中线上暖色像素 12→15 / 9→13 / 6→10，最亮相对亮度 0.54→0.77 / 0.52→0.82 / 0.53→0.75），提示余 7.0 单位；边距按字面不满足，11 件有边厚于默认（面板 21 对 0–18 px），待决定 |

## 没能检查的

- 引擎里的样子：Unity 导入（Table 为 CompressedHQ + mipmap）、桌面 shader 的光池、颗粒、首领压暗与暗角叠在黑漆与金框上的效果、真实 TMP 字在面板上的观感（字形、行高、自动缩字）、按钮悬停 / 按下的乘色、720p 与超宽屏。所有示意都是 Python 模拟（线性光乘色、九宫格按 PPU 展开、TMP 排版按 1.45 em 行高估算），不是截图。
- 紧凑卡的四条短文：离线估算显示，实机未验（见“文字空间”）。
- 结算、主菜单整屏、骰子样式页、对话框没有合成；主菜单只看了字标所在的纸面板。
- `buttonSecondary` 只在示意里模拟；W20Y 的代码（含禁用态的字色）未在本工作树。
- 附魔徽章：瑞士 README 记录过 `enchantIcons` 可能直接取默认图、绕过主题，这五张在当前代码里可能不生效（未改代码）。
- 代码里的固定色不随风格：进度槽 07191C；首领徽记是占位（稀有度色方块上画 Ink 档位条）。

## 已知风险

- **面板金框厚**：面板的框厚 21 px（10.5 单位）超过默认面板的边距（0–18 px，多数 6–10 px）；筹码、按钮、信息井、托盘前沿、进度条填充、分区标题条与框块也各有一边或几边厚于默认（“文字空间”表）。卡、本关牌、顶栏、角标、稀有度环都不比默认厚；面板按用处核对了文字空间（150 % 两行提示余 7.0 单位、商店标题条 55.5 单位）。协调者的“每个边距不超过默认”我按规则 3 理解为卡框（面板与唐三彩、爆彩一样厚于默认），若要求面板也 ≤ 默认，金框只能剩 3–5 px 的细线，概念图的双线金框就没有了，需要决定。
- **首领与红骰点**：首领朱红与红骰点 CIEDE2000 11.3（默认风格 11.1）；首领关时托盘双环、关卡签、标题条、算式首领因子是朱红，骰子上的红点在托盘里。算式行里两者相邻时更近：手型因子（`UiKit.Red`，牌面 EC655D）与首领因子（牌面经 LabelOn11 提亮成 EE4841）只差 CIEDE2000 5.8、亮度比 1.17:1（默认风格同样两块是 ED6D65 与 AB3C36，差 20.0）；`boss_stage.png` 里“一对 ×2”与“满员 ×0.8”几乎同色，只靠字区分。
- **danger 与 primary 同属深红**：A6262B 与 9E1D22 只差 CIEDE2000 2.5，警示角标、封印骰票（`UiKit.Danger`）与主按钮是同一种深红（数值角标对警示角标的规则满足，42.35）。规则里没有这一条，记下供判断。
- **禁用的次要按钮**：按待定的 buttonSecondary 与 W20Y2 的选字，禁用面与可用面只差 CIEDE2000 1.09，字都是 textColor，禁用态看不出来（见“次要按钮”）。
- **次要按钮在顶栏上**：buttonSecondary 7A5C30 对桌面 3.04、对暗色面板的黑漆 100D0A 3.14、对 paper 4.89，但对顶栏（panel 332A20）只有 2.28:1；商店顶栏的查看本章 / 手册 / 设置靠牌子的斜面边与刻线分开（`shop_w14.png`）。
- **商店的空格**：`ui_slot_inventory` 在商店被代码乘 panelLight（我的骰子 8 格、空道具格、空卡位），黑漆凹格的旧金边乘完只有暗古铜（502E11，在分区黑漆 100D0A 上 1.61:1），读作暗格；默认风格的格子同样很淡（半透明深色）。道具格的数量另有“道具 1/3”字样。
- **顶栏与对话框**对桌面 1.34:1，靠金框和斜面边分开。
- **托盘边是古铜**：托盘由代码乘 `lerp(panel, panelLight, .55)` = 53412B（对桌面 1.93:1），亮金来自里面的 Ghost 环（决定 5 接受）。
- 示意里的 SecondaryButton11 字按 TMP 的贪心换行估算；150 % 时“重掷 · 还能掷 2 [R]”在 224 单位宽里可能折成两行，与风格无关。
- 生成一次约 3 分钟（低优先级、与另一个生成同时跑时 168–186 秒），测试会完整生成两次。

## 需要的决定

以下是提案，未经协调者采纳前不改实现：

1. **边距与决定 4 的字面**：11 件有边厚于默认（“文字空间”表），主要是面板金框 21 px 对默认 0–18 px。选项：A 保留现状（文字空间按用处核对，150 % 提示余 7.0 单位）；B 把面板与其余件压到每边 ≤ 默认（面板只剩 3–5 px 细线，概念图的双线金框没有了）。
2. **禁用的次要按钮（W20Y / W20Y2）**：buttonSecondary 7A5C30 下禁用态不可辨（面差 1.09、字同色）。选项：A 禁用面另取一档（例如混入更多 panelLight 或 background，让 Muted 在禁用面上 ≥4.5:1 时 LabelOn11 会保留 Muted）；B 禁用字不走 LabelOn11、保持 Muted（2.89:1，低于 4.5）；C 接受。
3. **算式行的首领因子与手型因子**（CIEDE2000 5.8，默认 20.0）：选项：A 接受（靠字区分）；B 首领色改走品红一侧（“首领色”的搜索记录里的另一处候选，离红点远，但概念图里没有出处）；C 由代码给首领因子另加标记（不属于本包）。

## 决定记录

第一阶段审查（协调者，2026-11-18，`f11165e2`）：

1. 主按钮保留深红；首领离 primary ≥15 CIEDE2000、离 pressure 明显，保留首领的对比规则，补首领关示意。→ 首领 EB2921（见“首领色”），`boss_stage.png`。
2. 新的可选键 `buttonSecondary`（W20Y）设定每个 SolidButton11 的面：中间调金或古铜漆牌，在黑漆桌面、暗色面板与象牙 paper 上都能读出，对桌面与 paper 各 ≥3:1，字 ≥4.95；先不写入 `theme.json`，值放在生成器与 README；示意里模拟并注明。禁用的 SolidButton11 字将由 `LabelOn11(face, Muted)` 选（W20Y2），示意可以按此画。结构测试的可选键集合含 `buttonSecondary`（与 `valueBadge`、`buttonStep`、`buttonStepPrimary`、`section`、`caption` 同列），合并前后都通过。→ 7A5C30（见“次要按钮”）：对桌面 3.04、对 paper 4.89、对 panel 2.28、对分区黑漆 100D0A 3.14，字 textColor 5.14；示意的禁用字按 W20Y2 画；`test_theme_json_schema` 的可选键集合与必需键 24 个在 29 / 30 个声明键时都成立。
3. 商店标题条与托盘标签共用深红 `caption`：接受。→ 8E1B20，标题条与标签的字 textColor 7.51。
4. 金色更接近概念图（高光、亮边），同时边距不超过默认、150 % 提示余量 ≥5。→ 金色阶从六级加到七级（FFF6DC 高光）、斜面外缘亮边与受光高光线；面板金带 10 → 13 px（框厚 18 → 21 px），卡的金带加宽到 14 px（框厚 14 → 18 px，默认 22–34），稀有度环 7 → 9 px（默认 12）；中线上金带的暖色像素与最亮的相对亮度：面板 12 → 15 px、0.54 → 0.77，卡 9 → 13 px、0.52 → 0.82，道具卡 6 → 10 px、0.53 → 0.75；150 % 提示余量 8.5 → 7.0 单位（≥5）。边距按字面只对卡、本关牌、顶栏、角标与稀有度环成立，11 件有边厚于默认（“文字空间”、“需要的决定”第 1 条）。
5. 背景保留；托盘古铜边加 Ghost 环、黑漆 page_scene 加屏幕框：接受。桌面保持平的黑漆，只有屏幕金框、底边两角扇与动作行的扇形。
