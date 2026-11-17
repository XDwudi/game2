# W20 唐三彩陶坊 UI kit

状态：**实现与离线自检完成，提交复核**（2026-11-17，分支 `codex/v1.1-sancai`，起点 `codex/v1.1` `4c0f57ad`）。不是实机或美术 ACCEPTED。输入：`docs/specs/v11/THEME_SPEC.md`、`PLAY_LAYOUT.md` §1/§8 与 W12b 保护区、`tools/art_v11/theme_contract.json`（49 件引用）、瑞士 kit 的结构与检查、`ArtSource/StyleKit/themes/sancai/` 的五张概念图与 `style_kit.json`、检查点 ③“每套风格有自己的形状”。没有启动 Unity，没有用 GPT/Codex 生图，没有改 C#、`.meta`、默认件、瑞士件或 `docs/STATE.md`。

W13 补件（2026-11-17）：合并 `codex/v1.1`（合并提交 `d38d01bb`，契约增至 53 件引用，见 `docs/specs/v11/CARD_TEMPLATE.md` §3、§6）后，补齐卡面模板的稀有度环与三种宝石；同样只用程序绘制，没有启动 Unity，没有改 C#、`.meta`、色板或其他风格的件。结果见“验收条件与结果”末表。

## 范围与产物

生成器 [build_sancai.py](build_sancai.py) 一次写出 57 张 PNG 与 `theme.json`（W20 的 53 张，加 W13 卡面模板的 4 件）。全部在 4 倍画布上用程序绘制（SDF 形状、高度场光照、Worley 开片、fBm 流釉），最后一次浮点 Lanczos 缩小；固定种子，PNG 不带时间戳或元数据。

| 路径 | 内容 |
| --- | --- |
| `DicePrototype/Assets/Resources/Themes/sancai/UI/<name>.png` | 53 张 RGBA，名称与像素尺寸逐项等于默认件 |
| `…/sancai/Table/table_scene.png` | 1920×1080 RGB：纯色深褐陶胎桌面，外沿一圈三彩釉泥条（开片、流釉、高光），四角祥云，顶边几处流釉滴 |
| `…/sancai/Table/felt_tile.png` | 1024² RGB，周期噪声（4 倍画布 + 环绕滤波），均值即 felt 色，无光照渐变 |
| `…/sancai/preview.png` | 480×270：离线对局合成图缩小 4 倍 |
| `…/sancai/theme.json` | id `sancai`、order 2、preview true、24 个色板 token（由生成器的 `PALETTE` 写出，唯一来源） |
| `ArtSource/Art11/sancai/contact_sheet.png` | 53 组默认 / 三彩 /（着色件）乘色对照，离线对局合成，图标在真实尺寸与底色上的小图，普通 / 高压 / 首领三种关卡状态，PPU 200 拉伸样例，桌面与保护区、预览、色板与对比度，五张概念图 |
| `ArtSource/Art11/sancai/card_template_review.png` | W13 卡面模板的离线复核图（`build_sancai.py --card-review`，读已交付的三彩与瑞士件，不属于 build）：按 `DrawCard13` 的几何排出商店 220×330、桌面 216×244、紧凑 280×117 三种尺寸 × 常见 / 进阶 / 稀有 / 商品（Ghost），1080p 与 720p，三彩在左、瑞士在右；下方是三种宝石在卡顶边框上 12–34 px 的实际大小与 2 倍放大。精灵按游戏的导入方式取样（双线性、无 mipmap），线性光乘色与混合；卡名、短文、插画（StyleKit anchors）与价格按钮是替身 |

## 形状与材质

- **形状**取自概念图，不用默认的阶梯角和瑞士的切角：面板、卡、按钮、筹码是圆润软棱的釉面陶板（圆角 + 高度场釉边）；盛骰子的东西（提交格 `ui_ring`、空槽 `ui_slot_inventory`、托盘 `ui_dice_tray`、附魔线 `ui_frame_enchant`）用海棠委角（内凹圆角，尖端再倒圆）；徽章与小标签用四瓣海棠形；装饰用祥云（两旋一尾）；W13 稀有度环随卡面用圆角，三种稀有度宝石是圆、菱、六角星釉珠（尖角都倒圆）。测试对 12 个代表件做圆弧拟合，确认角是一段圆弧（凸的圆角或凹的委角）。
- **材质**：琥珀、铜绿、乳白三色铅釉，钴蓝作第四色（色板里只给稀有，协调者 2026-11-17），铁红用于红点与首领，深褐陶胎作描边与最深的影；釉边有高光、细开片、角部积釉与流釉。
- **克制**：九宫格中心从边距线起就是一种纯色（或全透明）。会被大幅拉伸的面板，边带中段只允许沿边方向的长而软的色带（沿边逐像素变化 ≤ 6 级），开片、条纹、积釉与祥云只在角块里；被代码着色的件边带中段沿边完全不变。卡面（最多约 1.5 倍拉伸）整圈带三彩流釉与开片。
- **文字边距**：按 C# `FrameInsets11` 的算法量出的框厚，卡 12.5–14.5 单位（默认 11–14.5），筹码 4.5–6.5 单位（默认 3–8），商店分区顶边 5 单位（让出代码画的标题条）。

## 概念图特征 → 界面件

| 概念图特征（页） | 界面件 |
| --- | --- |
| 乳白开片釉面、琥珀铜绿厚釉边、顶角积绿釉（card、shop、select） | 四种卡面（整圈三彩流釉、开片、顶边流釉滴、底角祥云；道具卡用铁红釉边，商店卡细琥珀边），`ui_paper_panel`、`ui_chip_label` |
| 深铜绿釉条“当前持有”、深褐信息行（shop） | `ui_panel_hud`、`ui_section_panel`、`ui_tab_bottom`：铜绿深釉面 + 琥珀乳白釉边，沿边长流釉，角部积釉、开片、祥云；`panel` 色 |
| 提交格的海棠委角、底边祥云（roll） | `ui_ring`、`ui_slot_inventory`（下陷暗槽 + 乳白细边）、`ui_dice_tray`（委角釉边 + 边上浅浮雕祥云）、`ui_frame_enchant` |
| 圆润按钮：乳白面、厚釉边、内圈细凸线（roll、select） | `ui_button_bevel`、`ui_chip_formula`、`ui_stage_card`、`ui_price_chip`（中性浮雕，由代码着色） |
| 钴蓝“稀有”釉章（card） | `rarityRare` = 钴蓝 2C4E9A（协调者 2026-11-17：钴蓝是三彩里最稀少的釉，给稀有）；主按钮改为琥珀金，`primary` = `gold` = EBA53F，与默认风格的金色主按钮一致。roll 页的钴蓝“重掷”不再沿用 |
| 琥珀价签、云纹钱与方孔钱（shop、select） | `ui_icon_coin`（琥珀釉方孔钱，深褐外圈，落在琥珀价签上仍可辨） |
| 四瓣海棠釉章（triptych） | 五个 `badge_enchant_*`、`ui_tag_lock`、`ui_tag_clock` |
| 绿玉釉骰台（shop） | `ui_pedestal` |
| 嵌在深槽里的琥珀亮釉进度条（triptych） | `ui_progress_fill`（圆柱高光）、`ui_progress_track`（下陷槽） |
| 铁红釉分区标题条（shop） | `ui_section_header`：釉条右端卷成卷轴（代码只按高度压扁它，图按显示比例预先拉长） |
| 三彩泥条外框、角上祥云、流釉（roll、card） | 桌面外沿泥条、四角祥云、顶边流釉滴 |
| 乳白开片釉骰、钴蓝 / 铁红釉点（card、triptych） | `ui_icon_dice_view`、`ui_card_back`、字标里的釉骰；`red` / `blue` |
| 琥珀环形刷新箭头、齿轮、手册（shop） | `ui_icon_refresh`、`ui_icon_gear`、`ui_icon_book`、`ui_icon_arrow`（中性，由 Ink 着色） |
| 标题字（roll、select） | `ui_wordmark_dicecraft`：Ma Shan Zheng（CP3-2 批准的三彩标题字）凸起深褐釉字、琥珀边 + 祥云上的釉骰 |
| 卡框里一圈凸起的细釉线、卡顶跨在边框上的釉章（card、shop） | W13 卡面模板（CARD_TEMPLATE §3、§6），四件都是中性灰、由稀有度色（商品为 Ghost）着色：`ui_card_frame_rarity` 稀有度环，圆角凸起釉线，两侧深色釉沟，紧贴卡面乳白唇线以内，环宽 11 px（5.5 单位，默认环 12 px），各边截面沿边不变、中心全透明；`ui_gem_common` / `ui_gem_advanced` / `ui_gem_rare` 圆、菱、六角星三种釉珠：墨线外框，软圆鼓面，左上一块边缘清楚的高光，右下釉面压暗；形状本身区分稀有度，不靠颜色 |

## 着色约定

25 件由代码乘色（`NEUTRAL_NAMES`：按钮、筹码、顶栏、本关牌、托盘、环、进度条、分区标题、点、导航图标、特效，以及 W13 的稀有度环与三种宝石）：只有灰度浮雕，测试要求 R=G=B；有实面的（顶栏、筹码、本关牌、按钮、价签、frame_block）中心像素纯白，所以画出来的颜色就是色板值，W11b 的“中心像素 × 着色”选字规则看到的也是它。稀有度环与宝石最亮处是纯白（即稀有度色本身），其余明暗只在亮度上。其余 28 件是烘焙色，代码以白色显示。

## 色板与对比度

WCAG 2 相对亮度（sRGB 线性化）。生成器 `palette_report()` 打印下列数字，测试独立复算。

| token | 值 | 选择理由 | 关键对比 |
| --- | --- | --- | --- |
| felt / background | 4A2A18 / 24160C | 概念图的深褐皮革桌面（取样 46–4E 21–2A 10–1A）；background 更深 | onFelt 8.80，textColor 10.96 |
| panel | 31441C | 深铜绿釉。代码在 panel 上写浅色字（顶栏、深色筹码、旧版面板），所以必须是深色；绿而不是褐，和陶胎桌面靠色相分开 | textColor 9.08（≥4.5），muted 5.45（≥3） |
| panelLight | 4B6A28 | 中铜绿：普通关的关卡牌签与本关牌标题条，托盘边取 panel 与它的混色 | textColor 5.28（≥4.95，不触发自动加深） |
| paper / ink | F2DCB0 / 3A2211 | 乳白釉与深褐陶胎色 | ink/paper 11.04（≥4.5） |
| textColor / muted | F8ECD2 / CDB78F | 浅乳白与浅陶土色 | 见 panel |
| primary | EBA53F（= gold） | 琥珀金釉：每阶段唯一的主按钮。协调者决定（2026-11-17）：钴蓝让给稀有，主按钮改琥珀 / 金釉，与默认风格“主按钮是金色”一致。取三彩的 gold，不取更深的琥珀 D98E2B：字的对比更高（ink 7.05 对 5.54），离柿橙 pressure 更远（CIEDE2000 18.6 对 14.5），而 D98E2B 正是卡边、泥条的琥珀釉。原值为钴蓝 3060B8 | LabelOn11 选 ink：7.05（≥4.95，按钮面不加深）；与三种稀有度 ΔE 48 / 58 / 112（≥40） |
| rarityCommon / Advanced / Rare | D3C7AA / 4F7D2B / 2C4E9A | 乳白、铜绿、钴蓝。钴蓝取生成器的 `COBALT` 釉色：概念图 card 页“稀有”釉章取样中位 274577、亮处 2B5A94，也是真三彩里最稀少的釉（协调者 2026-11-17）。原主按钮的 3060B8 与铜绿的亮度比只有 1.23，过不了 1.4，所以取更深的这一档。常见保留乳白（与新 primary 的 ΔE 48，过 40）。原稀有为琥珀 D98E2B | 两两亮度比 2.91 / 4.70 / 1.61（≥1.4）；与 primary 的 ΔE 48 / 58 / 112（≥40，不相撞） |
| pressure | F07436 | 柿橙警示色（饱和度 0.78），与首领铁红分得开；与新主按钮琥珀金的 CIEDE2000 18.6（ΔE76 29.5），靠色相与位置区分 | ink 5.12（≥4.95）；在深色底上 3.68，代码的 CueOn11 保持色相提亮 |
| boss / danger | A93F1C / A0301E | 铁红；danger 更深 | textColor 5.25 / 6.10；danger 在 paper 上 5.33 |
| gold / reward | EBA53F / F2B64E | 琥珀釉；reward 更亮。`valueBadge` 不填，卡面数值角标沿用 gold（THEME_SPEC §2，codex/v1.1） | ink/gold 7.05；gold 在 panel 上 5.06（保留金色数字）。数值角标与警示角标画在 `ui_chip_label` 乳白面（F7E9CB）上，线性光乘色得 E49630 / 9B2B15：CIEDE2000 38.4（≥20），亮度对比 3.17:1（≥1.5），字 ink 6.14 / textColor 6.52 |
| teal | 9BC766 | 浅铜绿（完成、正面） | panel 上 5.45 |
| red / blue | EC655D / 55B6D9（与默认相同） | 这两项同时决定 3D 骰点的颜色。骰点颜色是最重要的功能提示，协调者决定各风格都用默认这一对（2026-11-17，CP3-1；THEME_SPEC §2）。原提案为铁红 E66A44 / 薄钴蓝 84AEEE | 与默认相同 |
| violet | B58BC9 | 锰紫釉（概念图“商店服务”） | — |
| ghost | D2AC74 | 浅琥珀：空提交格、托盘内细线 | felt 上 6.05 |
| onFelt | EBD3A4 | 桌面是深色，直接画在桌布上的序号、空格点、乘号用浅乳白 | felt 上 8.80（≥4.5） |
| shadow | 140B05 | 最深陶胎 | — |

## 桌面保护区

生成器写图前检查（阈值 0）：

```text
SANCAI_ZONE dice_area rect=52,374,1408,276 max_rgb_delta=0 limit=0 PASS
SANCAI_ZONE formula_row rect=52,660,1408,60 max_rgb_delta=0 limit=0 PASS
SANCAI_ZONE submission_rail rect=344,740,1136,202 max_rgb_delta=0 limit=0 PASS
SANCAI_ZONE submit_info_text150 rect=32,740,360,202 max_rgb_delta=0 limit=0 PASS
```

装饰只在外沿：左侧泥条与阴影止于 x≤31（信息区从 32 开始），顶边很薄（商店标题从 y≈18 开始），流釉滴避开商店标题的 x 330–520。

## 重建与验证

```powershell
& E:\anaconda3\python.exe -B tools/art_v11/sancai/build_sancai.py
& E:\anaconda3\python.exe -B tools/art_v11/sancai/build_sancai.py --card-review   # W13 复核图，读已交付的件
& E:\anaconda3\python.exe -B tools/art_v11/theme_contract.py audit sancai --strict
& E:\anaconda3\python.exe -m unittest discover -s tests -v
```

本机结果见下节“验收条件与结果”。依赖：Python 3.11.4、Pillow 9.4.0、numpy 1.24.3、scipy 1.10.1；字体只用仓库内的 Noto Sans SC Bold、Liberation Sans、Ma Shan Zheng（各自许可证在字体目录）。

`tests/test_art_v11_sancai.py`（29 项）：清单与尺寸、RGBA、着色件 R=G=B、白色实面、开口件中心全透明、九宫格中心从边距线起纯色、横向切片中段逐列相同、拉伸边带（着色件沿边不变，烘焙件只有软色带）、C# 框厚镜像与文字空间、圆弧角、带字面板的对比度、小图标在真实底色与尺寸上的可辨度（锁 20×22 在 background 上等）、桌面保护区与外沿装饰、felt_tile 无缝与均匀、色板规则、`theme.json` 结构（键名从 `ThemePalette.cs` 读取）与生成一致、概念图 SHA 与来源表一致、两次完整重建逐字节一致且与交付件一致。W13 卡面模板另有 4 项：稀有度环带宽（C# `RingInsets13` 镜像）不超过默认环、带深色边与满亮釉；宝石从中心看的轮廓点数（圆 0、菱 4、六角星 6，尖与凹的半径差 ≥20% / ≥30%）；按无 mipmap 的取样在 18 px 与 12 px 下三种轮廓两两不同（异或 ≥ 并集的 20%）；墨线外框（外 3 px 平均灰 ≤100）与浅色釉面（内部平均灰 ≥170，最亮 ≥245）。色板决定（2026-11-17）后：主按钮的字（ink / paper / textColor 取最高）≥4.95；THEME_SPEC §2 的数值角标规则（gold 与 danger 画在 `ui_chip_label` 上，CIEDE2000 ≥20、亮度对比 ≥1.5:1、字 ≥4.95）；`theme.json` 的键允许不写 `valueBadge`（codex/v1.1 新增的可选项，不填即 gold），其余 24 项必须齐全。

## 验收条件与结果

实施者自测，独立复核与实机由协调者安排；结论只覆盖下表写明的方法。

| ID | 方法与期望 | 证据 | 结果 |
| --- | --- | --- | --- |
| W20-01 | 49 件齐全、RGBA、与默认件同尺寸；strict 审计 0 缺件 | `theme_contract.py audit sancai --strict`、单测 | 通过：`49 sprites shipped, 0 referenced defaults not overridden`，`AUDIT_OK` |
| W20-02 | 九宫格中心从边距线起纯色；横向切片中段逐列相同；拉伸边带规则；框厚（C# 算法）在边距内且留出文字空间 | 单测 | 通过：21 个四边九宫格、4 个横向切片；10 个着色件边带沿边不变；烘焙面板沿边逐像素 ≤ 6 级（阈值 10，卡面为 33–101） |
| W20-03 | 着色件 R=G=B；有实面的着色件中心纯白；开口件中心全透明 | 单测 | 通过：21 / 6 / 4 件 |
| W20-04 | 四个保护区与 felt 色偏差 0；felt_tile 无缝、无渐变 | 生成器检查、单测 | 通过：4/4 偏差 0 |
| W20-05 | 色板：AX-08 三项、onFelt、pressure 为警示色、稀有度亮度不同、主按钮不与稀有度撞色；`theme.json` 结构 | 单测、`palette_report()` | 通过，数字见色板表 |
| W20-06 | 锁、钱、骰杯、查看骰子、锁签、王冠、五个附魔章在真实尺寸与底色上可辨 | 单测（对比度 ≥3 的像素 ≥15%；锁两半各 ≥10% 达 4.5:1）、接触表小图 | 通过（离线合成） |
| W20-07 | 两次完整重建逐字节一致，且与交付件一致 | 单测 | 通过：53 张 PNG 与 `theme.json` |
| W20-08 | 全量 `unittest discover -s tests -v` | 本机日志 | 通过：260/260（原 236 + 新 24），331 秒，低优先级运行；生成一次约 100 秒 |
| W20-09 | 风格忠于概念图、纹理克制、图标小尺寸可读 | 实施者对照五张概念图与接触表自查 | 自查完成，待独立复核与用户判断 |
| F-THEME-02 / AX-08（实机） | Unity 导入、1080p / 720p 截图、切换风格 | 本任务不运行 Unity | 未运行，待协调者 |

W13 卡面模板补件（2026-11-17，合并 `codex/v1.1` 后的 `d38d01bb` 起）：

| ID | 方法与期望 | 证据 | 结果 |
| --- | --- | --- | --- |
| W20-10 | 契约 53 件齐全；strict 审计 0 缺件 | `theme_contract.py audit sancai --strict` | 通过：`53 sprites shipped, 0 referenced defaults not overridden`，`AUDIT_OK` |
| W20-11 | 只有 4 张新件与接触表变化，其余交付件逐字节不变 | `git status`；53 个已跟踪输出的原始字节 `git hash-object --no-filters` 与 HEAD 的 blob 比对 | 通过：49 张旧件、table_scene、felt_tile、preview、`theme.json` 共 53 个与 HEAD 相同；`contact_sheet.png` 变化（多了 4 格） |
| W20-12 | 4 件为中性灰、可乘色；环在 40 px 边距内、中心全透明、各边截面沿边不变、圆角；环宽 ≤ 默认环；三种宝石形状可分、带墨线与浅釉 | 单测（见上节 4 项新测试与扩充的清单）；全量 `unittest discover -s tests -v` | 通过：三彩 28/28（着色件 25、九宫格 22、开口件 5、拉伸着色件 11、圆弧角 12 件）；环宽 11 px（默认 12）；宝石轮廓点数 0 / 4 / 6，尖凹半径差 0.27 / 0.41，18 px 与 12 px 下两两异或 ≥ 0.25（最小是 12 px 的菱与星）；全量 270/270，331 秒，低优先级运行 |
| W20-13 | 卡面上的读感：三种尺寸 × 常见 / 进阶 / 稀有 / 商品，1080p 与 720p，与瑞士并排 | `ArtSource/Art11/sancai/card_template_review.png`（离线模拟，不是 Unity 截图） | 实施者自查：见下节“已知风险”；待独立复核与实机 |

色板决定（协调者 2026-11-17：稀有改钴蓝，主按钮改琥珀金），起点 `57279a50`：

| ID | 方法与期望 | 证据 | 结果 |
| --- | --- | --- | --- |
| W20-14 | 主按钮的字经 LabelOn11（ink / paper / textColor 取最高）≥4.95:1；AX-08 不变；稀有度两两亮度比 ≥1.4；primary 与三种稀有度 ΔE ≥40（与原来同一度量，CIE76）；数值角标（valueBadge 不填 = gold）与警示角标（danger）按 `ValueBadge13` 画在 `ui_chip_label` 上：CIEDE2000 ≥20、亮度对比 ≥1.5:1 | `palette_report()`；单测 `test_palette_meets_the_theme_rules`、新增 `test_value_and_warning_badges_stay_apart`（CIEDE2000 先对 Sharma 等的参考数据自检） | 通过：ink 7.05；AX-08 9.08 / 11.04 / 5.45（不变）；2.91 / 4.70 / 1.61；ΔE 48.4 / 58.1 / 112.5；E49630 对 9B2B15：CIEDE2000 38.4、3.17:1，字 ink 6.14 / textColor 6.52（不加深） |
| W20-15 | 只有 `theme.json`、接触表、离线合成（`preview.png` 是它缩小 4 倍）与复核图变化，其余逐字节不变 | 已跟踪输出的原始字节 `git hash-object --no-filters` 与 `57279a50` 的 blob 比对 | 通过：55 个相同（53 张界面件、table_scene、felt_tile）；变化的正是这 4 个 |
| W20-16 | 三彩单测、全量单测、strict 审计 | 本机日志 | 通过：三彩 29/29；全量 271/271，329 秒，低优先级运行；`53 sprites shipped, 0 referenced defaults not overridden`，`AUDIT_OK` |

## 没能检查的

- 引擎里的样子：Unity 导入（Table 为 CompressedHQ + mipmap）、桌面 shader 的光池、颗粒、首领压暗与暗角叠在泥条上的效果、真实 TMP 字在面板上的观感、按钮悬停 / 按下的乘色、720p 与超宽屏。接触表里的合成图是 Python 模拟（线性空间乘色、九宫格按 PPU 200 展开），不是截图。
- 只合成了对局画面与三种关卡状态；商店、结算、主菜单、骰子样式页没有合成。
- 附魔徽章：瑞士 README 记录过 `enchantIcons` 直接取默认图、绕过主题，这五张在当前代码里可能不生效（未改代码）。
- 代码里的固定色不随风格：按钮台阶 B56B29 / 8F453A、商店分区标题 B8433F / 2F8F86 / 7B5FB5、进度槽 07191C、附魔 submit1 3F8F6B。
- 预览图和合成图里的卡面插画用的是 `themes/sancai/anchors` 的概念图裁片，不是 W22 的正式重画。
- W13 卡面模板：复核图是 Python 对 `DrawCard13` 几何的模拟（双线性无 mipmap 取样、线性光乘色），不是 Unity 截图；没有看 TMP 的真实字形、失效卡的 62% 压暗、悬停、150% 文字、高的紧凑卡（宝石 22 单位）。游戏里三彩没有自己烘焙的紧凑图标，会退到默认风格的图标；复核图用的是 anchors 裁片。

## 已知风险

- 常见稀有度的乳白在乳白卡面上几乎看不见（1.2:1，默认是 1.4:1）。W13 卡面不再写“常见”，所以稀有度环两侧有深色釉沟、宝石有墨线：常见的环读作一圈浅灰米色细线，宝石读作带高光的浅灰米色釉珠，在乳白卡面旁偏灰。协调者 2026-11-17 决定保留乳白（与新主按钮的 ΔE 48，过 40）。
- ~~稀有的琥珀环紧挨琥珀卡边，读起来像双层琥珀边。~~ 已解决（协调者 2026-11-17）：稀有改为钴蓝 2C4E9A，深蓝环与六角星在琥珀卡边和乳白卡面上都很醒目（复核图，1080p 与 720p）。剩下的：进阶的深铜绿与稀有的深钴蓝都偏深（亮度比 1.61），靠色相（CIEDE2000 52.7）和宝石形状区分；钴蓝星的墨线与釉面都深，星读作深蓝剪影，内部高光弱；720p 紧凑卡的 12 px 星只剩带尖的小块。
- 主按钮与 gold 同色（默认风格也是 primary = gold）：价格数字、金币、算式里的金色筹码和数值角标与主按钮同一种琥珀金，reward 也只差 CIEDE2000 4.9；AX-02 的 `W11PrimaryColoured` 只数 Screen 下的按钮。主按钮与柿橙 pressure 的 CIEDE2000 18.6、亮度对比 1.38:1（默认风格的金与 E0703A 为 24.6 / 1.83），高压关时同屏，靠色相、大小与位置区分；需要实机确认。
- 首领三档的占位徽记（`GameApp.Play11` 的 `BossEmblem11`：稀有度色方块上画 Ink 档位条，等首领徽记接入前的占位）在钴蓝上只有 1.88:1（原琥珀 5.54:1）；代码不在本包范围。
- 深绿面板与深褐桌面亮度接近（1.21:1），分隔靠琥珀釉边；需要实机确认。
- 面板角块只有 20×20 单位，祥云在 1080p 约 12 px 宽、720p 更小，可能只读成一点卷纹。
- 卡面整圈有开片与流釉，商店里最多纵向拉伸约 1.5 倍，纹理会被拉长一些。
- 生成一次约 100 秒（另一 agent 占用 CPU、低优先级），测试会完整生成两次。

## 需要决定

1. ~~**主按钮用钴蓝还是琥珀。**~~ 已决定（协调者，2026-11-17）：稀有改为钴蓝（概念 card 页的“稀有”釉章，真三彩里最稀少的釉），取生成器的 `COBALT` 釉色 2C4E9A（原主按钮的 3060B8 与铜绿亮度比只有 1.23）；主按钮改琥珀金 EBA53F（= gold，与默认风格的金色主按钮一致）；常见保留乳白，稀有度环维持细环；其余色板不变。
2. ~~**红 / 蓝骰的颜色。**~~ 已决定（协调者，2026-11-17）：用默认的 EC655D / 55B6D9，各风格骰色一致。
3. **面板用深绿。** 概念图的面板多为乳白釉、深色字，但代码在 `panel` 上写浅色字，所以深色面板是必须的；深绿和深褐二选一，现选深绿。
