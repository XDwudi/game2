# W20 爆彩连环漫画 UI kit

状态：**实现与离线自检完成，提交复核**（2026-11-17，分支 `codex/v1.1-comic`，起点 `codex/v1.1` `dc85193a`）。不是实机或美术 ACCEPTED。输入：`docs/specs/v11/THEME_SPEC.md` §2（含 `valueBadge`）、`PLAY_LAYOUT.md` §1/§8 与 W12b 保护区、`tools/art_v11/theme_contract.json`（53 件引用，含 W13 卡面模板 4 件）、唐三彩与瑞士 kit 的结构与检查、`ArtSource/StyleKit/themes/comic/` 的五张概念图与 `style_kit.json`、检查点 ③“每套风格有自己的形状”。没有启动 Unity，没有用 GPT/Codex 生图，没有改 C#、`.meta`、其他风格的件、字体或 `docs/STATE.md`。

## 范围与产物

生成器 [build_comic.py](build_comic.py) 一次写出 57 张 PNG 与 `theme.json`。全部在 4 倍画布上用程序绘制（精确的多边形与矩形距离场、网点、爆炸星与速度线），最后一次浮点 Lanczos 缩小；固定种子，PNG 不带时间戳或元数据。

| 路径 | 内容 |
| --- | --- |
| `DicePrototype/Assets/Resources/Themes/comic/UI/<name>.png` | 53 张 RGBA，名称与像素尺寸逐项等于默认件 |
| `…/comic/Table/table_scene.png` | 1920×1080 RGB：整片平涂的印刷蓝桌面；屏幕外沿一圈红底网点“页边”和墨线，右栏面板背后的深蓝网点，主按钮右侧的黄色速度线，本关牌右上角背后的黄色爆炸星 |
| `…/comic/Table/felt_tile.png` | 1024² RGB，细的报纸颗粒（4 倍画布 + 环绕滤波），均值即 felt 色，无渐变 |
| `…/comic/preview.png` | 480×270：离线对局合成图缩小 4 倍 |
| `…/comic/theme.json` | id `comic`、order 3、preview true、24 个色板 token（`valueBadge` 不填，沿用 gold），由生成器的 `PALETTE` 写出 |
| `ArtSource/Art11/comic/contact_sheet.png` | 53 组默认 / 漫画 /（着色件）乘色对照，离线对局合成，图标在真实尺寸与底色上的小图，普通 / 高压 / 首领三种关卡状态，PPU 200 拉伸样例，桌面与保护区、预览、色板与对比度，五张概念图 |
| `ArtSource/Art11/comic/card_template_review.png` | W13 卡面模板复核图（`build_comic.py --card-review`，读已交付的漫画与瑞士件，不属于 build）：商店、桌面、紧凑三种尺寸 × 常见 / 进阶 / 稀有 / 商品，1080p 与 720p，漫画在左、瑞士在右，下方是宝石在卡顶 12–34 px 的实际大小与 2 倍放大；按游戏的导入方式取样（双线性、无 mipmap），线性光乘色 |

## 形状与材质

- **形状**取自五张概念图，不用默认的阶梯角、瑞士的四角切角或唐三彩的圆角：
  - 面板、卡、按钮、筹码都有**右下硬投影**（形状本身向右下平移 4–8 px 的纯墨色块）和粗墨线；
  - 面板与框是方角的**漫画分格**：墨线、乳白“格间”带、细墨线，再是平涂的底；
  - 按钮、筹码、标签**斜切**：只切左上与右下两角，同一斜度，读作向右倾的“/”字形说明框；
  - 横向切片的件（分区标题、进度条、锁定格前沿）按显示比例画出真正的平行四边形端头；
  - 网点（Ben-Day dots）、爆炸星、速度线只放在角块、桌面边缘、卡背和较大的图标里。
- **克制**：九宫格中心从边距线起就是一种纯色（或全透明）；所有会拉伸的面（着色件与烘焙件）边带中段沿边**完全不变**，网点只在 40 px 角块里；字下面的底一律平涂。
- **着色件**（`NEUTRAL_NAMES` 25 件）：白面、纯黑墨线与投影，测试要求 R=G=B；有实面的件中心像素纯白，乘色后就是色板值。其余 28 件是烘焙色。

## 概念图特征 → 界面件

| 概念图特征（页） | 界面件 |
| --- | --- |
| 粗墨线的乳白面板、蓝色面板配乳白格间带、右下硬投影（roll、shop、select） | `ui_paper_panel`；`ui_panel_hud`、`ui_section_panel`、`ui_tab_bottom`（深蓝底、乳白格间、细墨线、右上角浅蓝网点）；`ui_info_inset`（深井，左上内投影）；`ui_slot_inventory` |
| 黄框卡牌、角上网点（card、shop） | 四种卡面：黄框（控制器、商店）、红框（道具），格间墨线，框带两角红 / 蓝网点，卡面平涂乳白 |
| 斜切的标签、按钮与价签（roll “重掷”、shop 价签、select 标签） | `ui_chip_formula`、`ui_button_bevel`、`ui_button_ghost`、`ui_price_chip`、`ui_chip_label`（斜切 + 右下投影，由代码着色或乳白） |
| 方角漫画分格框与格间（triptych、roll 提交格） | `ui_ring`、`ui_dice_tray`（宽乳白格间带 + 墨线 + 硬投影，中心全透明）、`ui_frame_enchant`、`ui_topbar`、`ui_stage_card`、`ui_frame_block` |
| 斜切分区标题（shop “控制器卡牌”“消耗品”） | `ui_section_header`：右端“/”斜切 + 投影 |
| 黄色主按钮与速度线（roll “重掷”、shop “下一关”、card “选择一张卡牌”） | `primary` = 漫画黄；桌面在主按钮右侧画速度线 |
| 目标分数背后的爆炸星（roll、select） | 桌面在本关牌右上角背后画爆炸星；`fx_score_spark` 八角星；卡背、字标、锁签与时钟签的爆炸星 |
| 红底网点页面（roll、shop、card） | 桌面外沿红底网点页边与墨线；卡背红底网点 + 黄色放射线 |
| 黄色硬币、蓝色骰台、骰杯、放大镜（shop、roll） | `ui_icon_coin`、`ui_pedestal`、`ui_icon_roll`、`ui_icon_dice_view` |
| 蓝色锯齿徽章（roll、triptych 左上的星章） | 五个 `badge_enchant_*`（锯齿章，各自平涂色，乳白符号描墨线） |
| 标题字（roll、card） | `ui_wordmark_dicecraft`：站酷快乐体（CP3-2 批准的漫画标题字）墨色字、乳白描边、红色错位影，斜切黄色横幅 + 红色爆炸星里的骰子 |
| 卡顶稀有度章（card、shop） | W13：`ui_card_frame_rarity` 方角细环（墨线、着色带、墨线，宽 10 px = 5 单位，默认 12 px）；`ui_gem_common` / `ui_gem_advanced` / `ui_gem_rare` 圆、菱、六角星：粗墨线、平涂两档明暗（左上亮、右下暗）、一块白高光 |

## 色板与对比度

WCAG 2 相对亮度（sRGB 线性化）；ΔE 为 CIE76，CIEDE2000 另注。生成器 `palette_report()` 打印下列数字，测试独立复算。

| token | 值 | 选择理由 | 关键对比 |
| --- | --- | --- | --- |
| felt | 0250C6 | 概念图骰池的印刷蓝（style_kit `blue`）；整片平涂，四个保护区就是它 | onFelt 6.28，textColor 6.68（旧页面直接写在桌布上） |
| background | 021A44 | 最深的海军蓝页面底 | — |
| panel | 032F7A | 海军蓝（style_kit `navy`，概念图的蓝色面板）；代码在 panel 上写浅色字 | textColor 11.71（≥4.5），muted 7.11（≥3） |
| panelLight | 0D56CC | 稍亮的蓝：普通关标签、分隔、托盘边混色 | 字自动取 textColor 6.15（≥4.95，不触发加深） |
| paper / ink | F9F1D9 / 0A080A | 报纸米白与墨黑（style_kit `cream` / `ink`） | ink/paper 17.69 |
| textColor / muted | FFF8E4 / A9C6F0 | 浅米白、浅蓝 | 见 panel |
| gold / primary | FBDC0F | 漫画黄（style_kit `yellow`）：价格、金币、高亮；主按钮同色（与默认风格一样 primary = gold），概念图的主按钮“重掷”“下一关”“选择一张卡牌”都是黄的 | 主按钮的字取 ink 14.59（≥4.95）；gold 在 panel 上 9.07 |
| reward | FFB21A | 琥珀黄：奖励时刻 | — |
| teal | 33C25A | 波普绿（概念图“已通过”）：完成、正面 | panel 上 5.32 |
| red / blue | EC655D / 55B6D9 | 骰点颜色，各风格固定（THEME_SPEC §2） | — |
| violet | 9D4EDD | 波普紫：附魔；与默认风格一样和稀有同色 | — |
| danger | A80F1A | 深红：错误、首领警示 | paper 上 6.77，textColor 7.20 |
| boss / pressure | C8102E / F26B1D | 绯红 / 漫画橙（色相 22°，饱和度 0.88，警示色）；两者 CIEDE2000 25.7 | 字：textColor 5.55 / ink 6.56；主按钮与 pressure CIEDE2000 39.3（≥20），不会把唯一的主按钮读成警示 |
| shadow | 0A080A | 硬投影就是墨色 | — |
| ghost | 7FCFF5 | 浅青（style_kit `cyan` 提亮）：描边按钮、空提交格、商品的卡环；与卡面米白和黄色卡框都分得开 | felt 上 4.10 |
| rarityCommon / Advanced / Rare | C8CBD0 / 0250C6 / 9D4EDD | 报纸灰、印刷蓝、波普紫，按亮度拉开；都不是黄色，所以黄色卡框吞不掉稀有度环 | 两两亮度比 4.35 / 2.83 / 1.54（≥1.4）；与 primary 的 ΔE 89.6 / 164.5 / 164.4（≥40），CIEDE2000 33.9 / 80.6 / 80.7（≥20） |
| onFelt | F9F1D9 | 米白：直接画在蓝桌布上的序号与点 | felt 上 6.28（≥4.5） |
| valueBadge | 不填（= gold） | 数值角标沿用漫画黄 | 画在 `ui_chip_label` 米白面上为 F5D00B / 警示 A40D14：CIEDE2000 61.4（≥20），亮度对比 5.26:1（≥1.5），字 ink 13.22 / textColor 7.48 |

## 桌面保护区

生成器写图前检查（阈值 0）：

```text
COMIC_ZONE dice_area rect=52,374,1408,276 max_rgb_delta=0 limit=0 PASS
COMIC_ZONE formula_row rect=52,660,1408,60 max_rgb_delta=0 limit=0 PASS
COMIC_ZONE submission_rail rect=344,740,1136,202 max_rgb_delta=0 limit=0 PASS
COMIC_ZONE submit_info_text150 rect=32,740,360,202 max_rgb_delta=0 limit=0 PASS
```

装饰只在外沿与面板背后：页边左右 14 px、上 10 px（被顶栏盖住）、下 24 px；右栏网点从 x=1484 起（提交栏到 1480 为止）；速度线在 x≥1240、y≥944（主按钮右侧与底边）；爆炸星在本关牌右上角背后。

## 重建与验证

```powershell
& E:\anaconda3\python.exe -B tools/art_v11/comic/build_comic.py
& E:\anaconda3\python.exe -B tools/art_v11/comic/build_comic.py --card-review   # W13 复核图，读已交付的件
& E:\anaconda3\python.exe -B tools/art_v11/theme_contract.py audit comic --strict
& E:\anaconda3\python.exe -m unittest discover -s tests -v
```

`tests/test_art_v11_comic.py`（31 项）：清单与尺寸、RGBA、着色件 R=G=B、白色实面、开口件中心全透明、九宫格中心从边距线起纯色、横向切片中段逐列相同、22 个拉伸件边带沿边完全不变、C# 框厚镜像与文字空间、漫画形状（右下硬投影、按钮与筹码的斜切、面板方角）、带字面板的对比度、小图标在真实底色与尺寸上的可辨度、W13 卡面模板（环宽不超过默认、环在每种卡面上都不被卡框吞掉、宝石轮廓点数 0 / 4 / 6、18 px 与 12 px 下可分、墨线与浅色面）、桌面保护区与外沿装饰、felt_tile 无缝与均匀、色板规则（AX-08、onFelt、骰点色固定、pressure 为警示色、稀有度亮度差、主按钮与稀有度和 pressure 的距离、主按钮的字）、数值角标规则、`theme.json` 结构、概念图 SHA、两次完整重建逐字节一致且与交付件一致。

## 验收条件与结果

实施者自测，独立复核与实机由协调者安排；结论只覆盖下表写明的方法。

| ID | 方法与期望 | 证据 | 结果 |
| --- | --- | --- | --- |
| W20C-01 | 53 件齐全、RGBA、与默认件同尺寸；strict 审计 0 缺件 | `theme_contract.py audit comic --strict`、单测 | 通过：`53 sprites shipped, 0 referenced defaults not overridden`，`AUDIT_OK` |
| W20C-02 | 九宫格中心从边距线起纯色；横向切片中段逐列相同；拉伸件边带沿边不变；框厚（C# 算法）在边距内且留出文字空间 | 单测 | 通过：22 个四边九宫格、4 个横向切片、22 个拉伸件边带逐像素不变 |
| W20C-03 | 着色件 R=G=B；有实面的着色件中心纯白；开口件中心全透明 | 单测 | 通过：25 / 6 / 5 件 |
| W20C-04 | 漫画形状：右下硬投影、按钮与筹码斜切、面板方角 | 单测 | 通过：14 / 5 / 15 件 |
| W20C-05 | 四个保护区与 felt 色偏差 0；外沿有装饰；felt_tile 无缝、无渐变 | 生成器检查、单测 | 通过：4/4 偏差 0 |
| W20C-06 | 色板：AX-08、onFelt、骰点色固定、pressure 为警示色、稀有度亮度差、主按钮与稀有度和 pressure 的距离、主按钮的字、数值角标；`theme.json` 结构 | 单测、`palette_report()` | 通过，数字见色板表 |
| W20C-07 | 锁、钱、骰杯、查看骰子、锁签、王冠、五个附魔章在真实尺寸与底色上可辨 | 单测（对比度 ≥3 的像素 ≥15%；锁两半各 ≥10% 达 4.5:1）、接触表小图 | 通过（离线合成） |
| W20C-08 | W13 卡面模板：环宽不超过默认、在两种卡面上都不被卡框吞掉；宝石点数 0 / 4 / 6、18 px 与 12 px 下可分、墨线与浅面 | 单测、`card_template_review.png` | 通过 |
| W20C-09 | 两次完整重建逐字节一致，且与交付件一致 | 单测 | 通过：57 张 PNG 与 `theme.json` |
| W20C-10 | 全量 `unittest discover -s tests -v` | 本机日志 | 通过：302/302（漫画 31 项），506 秒，低优先级运行；生成一次约 85 秒 |
| W20C-11 | 风格忠于概念图、纹理克制、面板与小图标可读 | 实施者对照五张概念图、接触表、对局合成与复核图自查 | 自查完成，待独立复核与用户判断 |
| F-THEME-02 / AX-08（实机） | Unity 导入、1080p / 720p 截图、切换风格 | 本任务不运行 Unity | 未运行，待协调者 |

## 没能检查的

- 引擎里的样子：Unity 导入、桌面 shader 的颗粒、扫光、首领压暗与暗角叠在红色页边上的效果、真实 TMP 字、按钮悬停 / 按下、720p 与超宽屏。接触表里的对局合成和卡面复核图是 Python 模拟（线性光乘色、九宫格按 PPU 200 展开；复核图按无 mipmap 的双线性取样），不是截图。
- 只合成了对局画面、三种关卡状态和卡面模板；商店、结算、主菜单、骰子样式页没有合成。对局合成里顶栏右端（金币、手册、暂停）、主按钮和结束按钮按 `GameApp` 的坐标与 `UiKit.Button` 的画法（米白或黄色面 + 代码固定色台阶），其余位置按 PLAY_LAYOUT 近似。
- 桌面装饰的位置按 PLAY_LAYOUT 的对局版面放：商店、结算等别的界面里，右栏网点、速度线和爆炸星会出现在各自面板的空隙里，位置没有逐页核对。
- 代码里的固定色不随风格：按钮台阶 B56B29 / 8F453A（主按钮下面会多一层棕色“台阶”）、商店分区标题 B8433F / 2F8F86 / 7B5FB5、进度槽 07191C、附魔 submit1 3F8F6B。
- 卡面插画和合成图里的插画用的是 `themes/comic/anchors` 的概念图裁片，不是 W22 的正式重画；漫画风格还没有自己烘焙的紧凑图标，游戏里会退到默认风格的图标。

## 已知风险

- 九宫格的斜切只能放在 40 px 角块里：按钮切口 18×25 px，在 72 单位高的主按钮上只占约三分之一高度，读作“微微右倾”，不如概念图里的说明框斜得明显；34–56 单位高的筹码与价签上倾斜更明显。
- 面板角上的网点很小（深蓝面板上约 10 单位见方），在 1080p 上只是一点纹理；网点、速度线和爆炸星主要靠桌面边缘与卡背、字标承担。
- 进阶（印刷蓝 0250C6）与桌布同色：卡在桌面上时，进阶的蓝色细环与宝石都在米白卡面里面，不受影响。首领占位徽记（`GameApp.Play11` 的 `BossEmblem11`：稀有度色方块上画 Ink 档位条）上的档位条对比：一档灰 12.3、二档蓝 2.8、三档紫 4.3:1；代码不在本包范围，等首领徽记接入。
- 黄色主按钮与黄色卡框、价格、数值角标同色（默认风格也是 primary = gold）；AX-02 的 `W11PrimaryColoured` 只数 Screen 下的按钮，卡面着色是白色，不计入。
- 整片蓝桌面饱和度高；概念图的红色网点页面只留在屏幕外沿一圈。

## 需要决定

1. **桌面主色用蓝还是红。** 概念图的页面底色是红色网点，骰子放在蓝色骰池里。现在桌布取蓝（骰子与保护区所在的颜色），红色只做屏幕外沿的页边；若想更接近概念图的整体红色，可以把桌布改红、保护区平涂红色，但骰子和红色骰点、首领红会挤在一起。
2. **按钮的倾斜程度。** 九宫格只允许在角块里切；要更明显的倾斜，需要代码按显示尺寸画（不在美术范围），或者接受现在的幅度。
