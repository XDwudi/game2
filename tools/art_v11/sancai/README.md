# W20 唐三彩陶坊 UI kit

状态：**实现与离线自检完成，提交复核**（2026-11-17，分支 `codex/v1.1-sancai`，起点 `codex/v1.1` `4c0f57ad`）。不是实机或美术 ACCEPTED。输入：`docs/specs/v11/THEME_SPEC.md`、`PLAY_LAYOUT.md` §1/§8 与 W12b 保护区、`tools/art_v11/theme_contract.json`（49 件引用）、瑞士 kit 的结构与检查、`ArtSource/StyleKit/themes/sancai/` 的五张概念图与 `style_kit.json`、检查点 ③“每套风格有自己的形状”。没有启动 Unity，没有用 GPT/Codex 生图，没有改 C#、`.meta`、默认件、瑞士件或 `docs/STATE.md`。

## 范围与产物

生成器 [build_sancai.py](build_sancai.py) 一次写出 53 张 PNG 与 `theme.json`。全部在 4 倍画布上用程序绘制（SDF 形状、高度场光照、Worley 开片、fBm 流釉），最后一次浮点 Lanczos 缩小；固定种子，PNG 不带时间戳或元数据。

| 路径 | 内容 |
| --- | --- |
| `DicePrototype/Assets/Resources/Themes/sancai/UI/<name>.png` | 49 张 RGBA，名称与像素尺寸逐项等于默认件 |
| `…/sancai/Table/table_scene.png` | 1920×1080 RGB：纯色深褐陶胎桌面，外沿一圈三彩釉泥条（开片、流釉、高光），四角祥云，顶边几处流釉滴 |
| `…/sancai/Table/felt_tile.png` | 1024² RGB，周期噪声（4 倍画布 + 环绕滤波），均值即 felt 色，无光照渐变 |
| `…/sancai/preview.png` | 480×270：离线对局合成图缩小 4 倍 |
| `…/sancai/theme.json` | id `sancai`、order 2、preview true、24 个色板 token（由生成器的 `PALETTE` 写出，唯一来源） |
| `ArtSource/Art11/sancai/contact_sheet.png` | 49 组默认 / 三彩 /（着色件）乘色对照，离线对局合成，图标在真实尺寸与底色上的小图，普通 / 高压 / 首领三种关卡状态，PPU 200 拉伸样例，桌面与保护区、预览、色板与对比度，五张概念图 |

## 形状与材质

- **形状**取自概念图，不用默认的阶梯角和瑞士的切角：面板、卡、按钮、筹码是圆润软棱的釉面陶板（圆角 + 高度场釉边）；盛骰子的东西（提交格 `ui_ring`、空槽 `ui_slot_inventory`、托盘 `ui_dice_tray`、附魔线 `ui_frame_enchant`）用海棠委角（内凹圆角，尖端再倒圆）；徽章与小标签用四瓣海棠形；装饰用祥云（两旋一尾）。测试对 11 个代表件做圆弧拟合，确认角是一段圆弧（凸的圆角或凹的委角）。
- **材质**：琥珀、铜绿、乳白三色铅釉，钴蓝作第四色，铁红用于红点与首领，深褐陶胎作描边与最深的影；釉边有高光、细开片、角部积釉与流釉。
- **克制**：九宫格中心从边距线起就是一种纯色（或全透明）。会被大幅拉伸的面板，边带中段只允许沿边方向的长而软的色带（沿边逐像素变化 ≤ 6 级），开片、条纹、积釉与祥云只在角块里；被代码着色的件边带中段沿边完全不变。卡面（最多约 1.5 倍拉伸）整圈带三彩流釉与开片。
- **文字边距**：按 C# `FrameInsets11` 的算法量出的框厚，卡 12.5–14.5 单位（默认 11–14.5），筹码 4.5–6.5 单位（默认 3–8），商店分区顶边 5 单位（让出代码画的标题条）。

## 概念图特征 → 界面件

| 概念图特征（页） | 界面件 |
| --- | --- |
| 乳白开片釉面、琥珀铜绿厚釉边、顶角积绿釉（card、shop、select） | 四种卡面（整圈三彩流釉、开片、顶边流釉滴、底角祥云；道具卡用铁红釉边，商店卡细琥珀边），`ui_paper_panel`、`ui_chip_label` |
| 深铜绿釉条“当前持有”、深褐信息行（shop） | `ui_panel_hud`、`ui_section_panel`、`ui_tab_bottom`：铜绿深釉面 + 琥珀乳白釉边，沿边长流釉，角部积釉、开片、祥云；`panel` 色 |
| 提交格的海棠委角、底边祥云（roll） | `ui_ring`、`ui_slot_inventory`（下陷暗槽 + 乳白细边）、`ui_dice_tray`（委角釉边 + 边上浅浮雕祥云）、`ui_frame_enchant` |
| 圆润按钮：乳白面、厚釉边、内圈细凸线（roll、select） | `ui_button_bevel`、`ui_chip_formula`、`ui_stage_card`、`ui_price_chip`（中性浮雕，由代码着色） |
| 钴蓝“重掷”（roll） | `primary` = 钴蓝 |
| 琥珀价签、云纹钱与方孔钱（shop、select） | `ui_icon_coin`（琥珀釉方孔钱，深褐外圈，落在琥珀价签上仍可辨） |
| 四瓣海棠釉章（triptych） | 五个 `badge_enchant_*`、`ui_tag_lock`、`ui_tag_clock` |
| 绿玉釉骰台（shop） | `ui_pedestal` |
| 嵌在深槽里的琥珀亮釉进度条（triptych） | `ui_progress_fill`（圆柱高光）、`ui_progress_track`（下陷槽） |
| 铁红釉分区标题条（shop） | `ui_section_header`：釉条右端卷成卷轴（代码只按高度压扁它，图按显示比例预先拉长） |
| 三彩泥条外框、角上祥云、流釉（roll、card） | 桌面外沿泥条、四角祥云、顶边流釉滴 |
| 乳白开片釉骰、钴蓝 / 铁红釉点（card、triptych） | `ui_icon_dice_view`、`ui_card_back`、字标里的釉骰；`red` / `blue` |
| 琥珀环形刷新箭头、齿轮、手册（shop） | `ui_icon_refresh`、`ui_icon_gear`、`ui_icon_book`、`ui_icon_arrow`（中性，由 Ink 着色） |
| 标题字（roll、select） | `ui_wordmark_dicecraft`：Ma Shan Zheng（CP3-2 批准的三彩标题字）凸起深褐釉字、琥珀边 + 祥云上的釉骰 |

## 着色约定

21 件由代码乘色（`NEUTRAL_NAMES`：按钮、筹码、顶栏、本关牌、托盘、环、进度条、分区标题、点、导航图标、特效）：只有灰度浮雕，测试要求 R=G=B；有实面的（顶栏、筹码、本关牌、按钮、价签、frame_block）中心像素纯白，所以画出来的颜色就是色板值，W11b 的“中心像素 × 着色”选字规则看到的也是它。其余 28 件是烘焙色，代码以白色显示。

## 色板与对比度

WCAG 2 相对亮度（sRGB 线性化）。生成器 `palette_report()` 打印下列数字，测试独立复算。

| token | 值 | 选择理由 | 关键对比 |
| --- | --- | --- | --- |
| felt / background | 4A2A18 / 24160C | 概念图的深褐皮革桌面（取样 46–4E 21–2A 10–1A）；background 更深 | onFelt 8.80，textColor 10.96 |
| panel | 31441C | 深铜绿釉。代码在 panel 上写浅色字（顶栏、深色筹码、旧版面板），所以必须是深色；绿而不是褐，和陶胎桌面靠色相分开 | textColor 9.08（≥4.5），muted 5.45（≥3） |
| panelLight | 4B6A28 | 中铜绿：普通关的关卡牌签与本关牌标题条，托盘边取 panel 与它的混色 | textColor 5.28（≥4.95，不触发自动加深） |
| paper / ink | F2DCB0 / 3A2211 | 乳白釉与深褐陶胎色 | ink/paper 11.04（≥4.5） |
| textColor / muted | F8ECD2 / CDB78F | 浅乳白与浅陶土色 | 见 panel |
| primary | 3060B8 | 钴蓝，三彩里最稀少的第四色，只给每阶段唯一的主按钮；概念图 roll 页的主按钮就是钴蓝；对局画面上没有别的大块蓝色 | textColor 5.13（≥4.95） |
| rarityCommon / Advanced / Rare | D3C7AA / 4F7D2B / D98E2B | 三彩本身的三釉：乳白、铜绿、琥珀，按亮度拉开 | 两两亮度比 2.91 / 1.83 / 1.59（≥1.4）；与 primary 的 ΔE 79 / 101 / 114（≥40，不相撞） |
| pressure | F07436 | 柿橙警示色（饱和度 0.78），与首领铁红、主按钮钴蓝都分得开 | ink 5.12（≥4.95）；在深色底上 3.68，代码的 CueOn11 保持色相提亮 |
| boss / danger | A93F1C / A0301E | 铁红；danger 更深 | textColor 5.25 / 6.10；danger 在 paper 上 5.33 |
| gold / reward | EBA53F / F2B64E | 琥珀釉；reward 更亮 | ink/gold 7.05；gold 在 panel 上 5.06（保留金色数字） |
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
& E:\anaconda3\python.exe -B tools/art_v11/theme_contract.py audit sancai --strict
& E:\anaconda3\python.exe -m unittest discover -s tests -v
```

本机结果见下节“验收条件与结果”。依赖：Python 3.11.4、Pillow 9.4.0、numpy 1.24.3、scipy 1.10.1；字体只用仓库内的 Noto Sans SC Bold、Liberation Sans、Ma Shan Zheng（各自许可证在字体目录）。

`tests/test_art_v11_sancai.py`（24 项）：清单与尺寸、RGBA、着色件 R=G=B、白色实面、开口件中心全透明、九宫格中心从边距线起纯色、横向切片中段逐列相同、拉伸边带（着色件沿边不变，烘焙件只有软色带）、C# 框厚镜像与文字空间、圆弧角、带字面板的对比度、小图标在真实底色与尺寸上的可辨度（锁 20×22 在 background 上等）、桌面保护区与外沿装饰、felt_tile 无缝与均匀、色板规则、`theme.json` 结构（键名从 `ThemePalette.cs` 读取）与生成一致、概念图 SHA 与来源表一致、两次完整重建逐字节一致且与交付件一致。

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

## 没能检查的

- 引擎里的样子：Unity 导入（Table 为 CompressedHQ + mipmap）、桌面 shader 的光池、颗粒、首领压暗与暗角叠在泥条上的效果、真实 TMP 字在面板上的观感、按钮悬停 / 按下的乘色、720p 与超宽屏。接触表里的合成图是 Python 模拟（线性空间乘色、九宫格按 PPU 200 展开），不是截图。
- 只合成了对局画面与三种关卡状态；商店、结算、主菜单、骰子样式页没有合成。
- 附魔徽章：瑞士 README 记录过 `enchantIcons` 直接取默认图、绕过主题，这五张在当前代码里可能不生效（未改代码）。
- 代码里的固定色不随风格：按钮台阶 B56B29 / 8F453A、商店分区标题 B8433F / 2F8F86 / 7B5FB5、进度槽 07191C、附魔 submit1 3F8F6B。
- 预览图和合成图里的卡面插画用的是 `themes/sancai/anchors` 的概念图裁片，不是 W22 的正式重画。

## 已知风险

- 常见稀有度的乳白在乳白卡面上几乎看不见（1.2:1，默认是 1.4:1），要靠“常见”文字。
- 深绿面板与深褐桌面亮度接近（1.21:1），分隔靠琥珀釉边；需要实机确认。
- 面板角块只有 20×20 单位，祥云在 1080p 约 12 px 宽、720p 更小，可能只读成一点卷纹。
- 卡面整圈有开片与流釉，商店里最多纵向拉伸约 1.5 倍，纹理会被拉长一些。
- 生成一次约 100 秒（另一 agent 占用 CPU、低优先级），测试会完整生成两次。

## 需要决定

1. **主按钮用钴蓝还是琥珀。** 现为钴蓝（对局画面上唯一的蓝色大块，概念 roll 页同色），稀有度因此用乳白 / 铜绿 / 琥珀。若改回默认的“主按钮 = 金色”，稀有可以改用钴蓝（概念 card 页“稀有”是钴蓝章），常见的乳白就要和琥珀拉开。
2. ~~**红 / 蓝骰的颜色。**~~ 已决定（协调者，2026-11-17）：用默认的 EC655D / 55B6D9，各风格骰色一致。
3. **面板用深绿。** 概念图的面板多为乳白釉、深色字，但代码在 `panel` 上写浅色字，所以深色面板是必须的；深绿和深褐二选一，现选深绿。
