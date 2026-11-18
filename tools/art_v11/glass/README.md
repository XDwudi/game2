# W20 彩绘玻璃 UI kit

状态：**第一阶段完成，提交协调者复核**（2026-11-18，分支 `codex/v1.1-glass`，起点 `codex/v1.1` `cd2fedb3`）。第一阶段只交方向：色板与对比度、桌面图与非对局背景、代表性界面件（框面一族 24 张与字标）、对局与商店的离线示意、形状与材质的来源。图标、徽章、宝石、特效、卡背、接触表、卡面复核图、单测在第二阶段按复核结论完成。不是实机或美术 ACCEPTED。没有启动 Unity，没有用 GPT/Codex 生图，没有改 C#、`.meta`、默认件或其他风格的件。

输入：`docs/specs/v11/THEME_SPEC.md` §2、§3、§3.1、§5（含 W20X 的 `buttonStep` / `buttonStepPrimary` / `section` / `caption` 与可选的 `Table/page_scene.png`）、`PLAY_LAYOUT.md` §1/§8、`SHOP_LAYOUT.md` §2、`CARD_TEMPLATE.md` §3/§6、`tools/art_v11/theme_contract.json`（2026-11-18 重新生成：94 张，有引用的 50 张）、`ArtSource/StyleKit/themes/glass/` 的五张概念页与 `style_kit.json`，以及唐三彩、爆彩漫画两套 kit 的做法与检查。代码依据：`GameApp.Play11.cs`（`L11`、`TopBar11`、`DrawTray11`、`DrawSubmit11`、`DrawActions11`、`SolidButton11`）、`GameApp.Play11Score.cs`、`GameApp.Play11Contrast.cs`（`FrameInsets11`、`LabelOn11`）、`GameApp.Card11.cs`、`GameApp.Shop14.cs`、`UiKit.cs`、`Art07ButtonMotion.cs`（焦点框外扩 3 单位）。

## 形状与材质（取自哪张概念页）

| 形状 / 材质 | 概念页里的样子 | 用在 |
| --- | --- | --- |
| **双道金边**：外沿铅线、一道受左上光的金线脚、铅线、一道细金线、铅线，共 15 px（7.5 单位） | roll 提交格“01–05”的双金框，roll 左栏“目标分数 / 本关累计 / 金币”的金框黑底板，shop 各分区与卡牌的金框 | 不着色、原样显示的面：`ui_paper_panel`、`ui_panel_hud`、`ui_section_panel`、`ui_tab_bottom`（下沿不封口）、四种卡面；道具卡把细金线换成一道钴蓝玻璃 |
| **45° 切角**：金框 12 px（6 单位），铅条件 8–14 px，托盘 22 px | roll 的提交格、左栏面板、行动行按钮，select 的关卡卡，triptych 的锁签与八角章 | 所有九宫格面 |
| **铅条里的玻璃块**：黑铅条、一道比玻璃暗一档的斜边（朝光的边亮、背光的边暗）、白色平面（=色板色）；四角各一小块三角“角窗”，由一道与切角平行的铅线隔出 | roll 的钴蓝“重掷”玻璃按钮、shop 的彩色标题条、triptych 的蓝玻璃八角钮——都是一块块玻璃嵌在铅条里 | 被代码着色的件：`ui_button_bevel`、`ui_chip_formula`、`ui_topbar`、`ui_stage_card`（带角窗），`ui_dice_tray`、`ui_ring`、`ui_button_ghost`、`ui_card_frame_rarity`（开口，不带角窗） |
| **双线环**：粗线、空隙、细线 | roll 提交格的双金框 | `ui_ring`：锁定格乘金色就是概念页的双金框，空格乘 ghost（暗铜）；选中骰子的金色双线框 |
| **尖头横条**：两端是“<”“>”尖角 | shop 的“消耗品”“控制器卡牌”标题条，roll 的“第 6 关”牌签，select 的“开始挑战”两端 | 只横向切片的件：`ui_section_header`（商店分区标题条）、`ui_progress_fill` / `ui_progress_track`、`ui_tray_front`（锁定骰子下的铜台，顶边金线） |
| **羊皮纸**：卡面、纸面板、标题条 | roll 左栏的羊皮纸标题、“骰池”“有序提交”羊皮纸签，card 页的名牌与文字区 | `paper`、`caption` |
| **石板桌面** | roll / triptych 骰池里的深石板（中位 292831，风格包底色 2B2A33） | `felt`，桌面图与非对局背景的底 |
| **彩窗**：铅线分隔的宝石色玻璃，背光，边缘有灰绘暗边 | 各页四周的红蓝彩窗、尖拱窗 | 桌面两侧窄边各一扇尖拱窗（菱形块，红蓝相间，深蓝底玻璃），行动行常空处一扇玫瑰窗（8 瓣红蓝、琥珀心、金四芒星）；字标左边一扇玫瑰窗 |
| **金色四芒星** | 各页框角、卡顶、按钮两端的金星 | 玫瑰窗中心、字标右上；九宫格面上不放（克制） |
| **标题字** | roll / select 的“骰子构筑”衬线粗体 | `ui_wordmark_dicecraft`：Noto Serif SC Black（CP3-2 批准），铅黑字、金边，左边玫瑰窗与大理石骰 |

与别的风格的区别：不用默认的阶梯角、唐三彩的圆角釉边；切角取自本风格的概念页，但做法不同于瑞士（2–3 px 的小切角、细黑线）和漫画（15 单位大切角、5 单位墨线、右下硬投影、前倾）：这里是中等切角，加双道金边或铅条斜边与角窗，没有投影，左右对称；只横向切片的条是两端尖角，不是平行四边形。

克制：九宫格中心从边距线起一种纯色；拉伸的边带只随离边距离与边的朝向变化，沿边逐像素不变；角窗、切角与光照差都在 40 px 角块里；九宫格面上不放金星和彩窗；彩窗只在桌面不压任何界面的地方。

## 色板与对比度

WCAG 2 相对亮度（sRGB 线性化）；ΔE 为 CIE76，de00 为 CIEDE2000。生成器 `palette_report()` 打印下列数字（第二阶段的单测独立复算）。

| token | 值 | 理由 | 关键对比 |
| --- | --- | --- | --- |
| felt | 292833 | 概念页骰池的深石板（roll 中位 292831，风格包 2B2A33） | onFelt 10.89，textColor 12.71，paper 11.12，muted 7.10 |
| background | 121118 | 最深的石板 | — |
| panel | 1E2036 | 夜色玻璃：顶栏、深色面板与次要按钮。比概念页的近黑（1A1A22）略偏蓝，好与石板桌面分开（亮度只差 1.10，靠色相与金框） | textColor 13.97，muted 7.81，gold 9.27，teal 7.76，pressure 5.12 |
| panelLight | 6B5632 | 旧铜：普通关牌签与本关牌标题条、框的边线（`UiKit.Frame`）、空道具格、未到的章节点、停用按钮的面 | 字取 textColor 6.12；停用按钮的 Muted 字 3.42 |
| paper / ink | F1DFC2 / 1B1720 | 羊皮纸（概念页 F2D4B2 提亮）与铅黑 | ink/paper 13.51 |
| textColor / muted | F7EFDF / BEB4A6 | 大理石白、旧羊皮纸灰 | 见 panel |
| gold / reward | F0BE52 / FFD567 | 镀金（风格包 amber F8C761）；reward 更亮 | 字都取 ink：10.25 / 12.58 |
| primary | 2152C4 | 钴蓝玻璃：每个阶段唯一的主按钮（roll 的“重掷”、select 的“开始挑战”都是钴蓝玻璃）。金色留给钱与奖励，钴蓝在满屏金框里最醒目 | 字取 textColor 5.99（≥4.95，不加深）；与 gold de00 68.9 |
| teal | 62C894 | 浅翡翠：完成、正面 | — |
| red / blue | EC655D / 55B6D9 | 骰点颜色，各风格固定（CP3-1，THEME_SPEC §2） | primary/blue de00 35.8 |
| violet | 9C74D6 | 紫晶：附魔 | primary/violet de00 22.2 |
| danger / boss | B0281F / A8231B | 红宝石玻璃（概念页首领牌签 A40313） | danger 在 paper 上 5.07；boss 字取 textColor 6.29 |
| pressure | EE6A2C | 朱红玻璃，警示色（饱和度 0.82，色相 19°） | 字取 ink 5.66；panel 上 5.12、计分框上 5.70（顶栏目标数字保持色相）；纸面上 2.39，本关牌目标按规则取 ink；primary/pressure de00 53.1，pressure/boss de00 25.2 |
| shadow | 0B0A0F | — | — |
| ghost | 8E7A5A | 暗铜：空提交格、托盘内的强调线、描边按钮、商品卡环 | felt 上 3.52；与卡框金线 de00 21.5（商品卡环不被金框吞掉） |
| rarityCommon / Advanced / Rare | B7BDC9 / 2E9A66 / B2397F | 白玻璃银、翡翠、玫瑰窗洋红；都离开金框色（de00 31.2 / 37.2 / 55.8）和钴蓝主按钮 | 两两亮度比 1.88 / 2.94 / 1.57（≥1.4）；与 primary ΔE 73.7 / 109.6 / 58.8（≥40），de00 39.3 / 49.6 / 30.2（≥20） |
| onFelt | E9DEC8 | 画在石板上的序号、空格点、乘号 | felt 上 10.89 |
| valueBadge | 不填（= gold） | 数值角标与警示角标都画在 `ui_chip_label` 的羊皮纸面上：E3A63C 对 A62215 | de00 43.6、亮度比 3.42；字 ink 8.22 / textColor 6.42 |
| buttonStep / buttonStepPrimary（W20X） | 8C6B33 / B08A3E | 按钮下的台阶是金色底座：铅条玻璃按钮坐在金框上（概念页按钮四周是金框，台阶只露出下沿） | — |
| section（W20X） | FFFFFF | `ui_section_panel` 是带金框的成品（夜色玻璃面），原样显示；骰子样式页的样例格也一样 | 格中心 1E2036：Muted 7.81，textColor 13.97 |
| caption（W20X） | EBD6B0 | 羊皮纸标题条：商店分区标题条与骰池三个标签（roll 的“骰池”“有序提交”与左栏标题都是羊皮纸） | 字取 ink 12.42 |

AX-08：textColor/panel 13.97（≥4.5），ink/paper 13.51（≥4.5），muted/panel 7.81（≥3）。

## 桌面

- `table_scene.png`：整张石板（felt 色）。四个保护区与 felt 色偏差 0（生成器写图前检查）：

  ```text
  GLASS_ZONE dice_area rect=52,374,1408,276 max_rgb_delta=0 limit=0 PASS
  GLASS_ZONE formula_row rect=52,660,1408,60 max_rgb_delta=0 limit=0 PASS
  GLASS_ZONE submission_rail rect=344,740,1136,202 max_rgb_delta=0 limit=0 PASS
  GLASS_ZONE submit_info_text150 rect=32,740,360,202 max_rgb_delta=0 limit=0 PASS
  ```

- 装饰只有三处，都是闭合轮廓：
  - 左右窄边各一扇尖拱窗：x 3–25 与 1895–1917，y 94（尖顶）–1066（窗台），金边铅线，内为红蓝相间的菱形块与深蓝底玻璃，颜色压暗，只起框的作用。算上 3 px 的抗锯齿与滤波范围，左窗止于 x 28，第一张卡的焦点框从 x 29 起；右窗从 x 1892 起，结束槽的达标环止于 x 1891；两扇窗都在顶栏（y 84）以下。
  - 行动行的玫瑰窗：中心 (1384, 1012)，半径 54，x 1330–1438、y 958–1066。行动行在每个状态、100 % 与 150 % 字号下都空着的矩形是 x 1280–1488（主按钮在 150 % 时连焦点框到 1279；结束槽的达标环从 1492 起），y 942（提交行下沿）–1080；玫瑰窗四边都在里面并留 4 px 以上。逐状态的矩形与爆彩漫画 README 的表一致（本 kit 另把按钮焦点框的 3 单位算进去），第二阶段出逐状态的图与表。
- 生成器在导入时检查：装饰外接框外扩 3 px（抗锯齿与 Lanczos 的范围）后，不进 11 个按钮 / 面板外接矩形（含焦点框与达标环），也不进四个保护区。自查（读交付的 PNG）：11 个矩形内与 felt 不同的像素 0。
- 卡牌栏 32,96,1448,244（连卡的焦点框 29,93,1454,250）下面没有任何装饰：矩形内与 felt 不同的像素 0，所以卡多卡少都只露出石板。
- `page_scene.png`：纯石板（与 felt 色偏差 0），没有版面形状，商店、选关等画面用它。非对局画面直接画在桌面上的 19 处字（`table_text_contrast.py glass`）：最低 7.10:1，全部 ≥4.5，`TABLE_TEXT_OK`。
- `felt_tile.png`：1024² 石板细纹，环绕滤波无缝，均值即 felt 色（现在的游戏用 table_scene 取代它）。

## 文字空间（FrameInsets11 镜像）

| 件 | glass（左、下、右、上 px） | 默认 | 说明 |
| --- | --- | --- | --- |
| 卡面四种（controller / compact / shop / item） | 16, 16, 16, 16 | 22, 29, 27, 22（shop 27, 34, 32, 27；item 22, 30, 28, 22） | 每边都比默认细 |
| `ui_panel_hud` | 15, 15, 15, 15 | 10, 8, 6, 10 | 提示框内框 7.5 单位。W20Xb 实测两行提示的字块（150 %）在提示框里 15.7–72.3 单位，由代码决定、与贴图无关；本 kit 第二行离内框底 88 − 7.5 − 72.3 = 8.2 单位（要求 ≥5；漫画 1.6，唐三彩 5.2）。离线示意估计 6.5 |
| `ui_button_bevel` | 10, 10, 10, 10 | 7, 12, 12, 7 | — |
| `ui_chip_formula` | 8, 8, 8, 8 | 6, 16, 10, 6 | 34 单位标签留 26 单位给字 |
| `ui_chip_label` | 7, 7, 7, 7 | 5, 5, 5, 5 | 30 单位数值角标留 23 单位给字 |
| `ui_topbar` | 11, 11, 11, 11 | 8, 30, 11, 8 | — |
| `ui_stage_card` | 12, 12, 12, 12 | 19, 29, 27, 19 | — |
| `ui_section_panel` | 15, 15, 15, 15 | 6, 6, 6, 6 | 标题条高 68 − 7.5 − 2 = 58.5 单位 |
| `ui_card_frame_rarity`（RingInsets13） | 10 | 12 | — |

## 离线示意（Python 模拟游戏的画法，不是 Unity 截图）

`ArtSource/Art11/glass/review/`（`review_glass.py` 读已交付的件；还没画的件按 `UiKit.Template` 退回默认件，每张图的标题里列出）：

- `play_a_100.png`、`play_a_150.png`：状态 a（三张卡，五颗骰子选中两颗，行动行“锁定 2 颗 / 取消选择”）；
- `play_f_100.png`、`play_f_150.png`：状态 f（七张卡、十颗骰子锁定两颗，行动行“提交 2 颗 / 重掷”）；150 % 时七张卡按代码改成两行迷你卡；
- `shop_100.png`：W14 商店（`GameApp.Shop14` 的 S14 矩形），W20X 的乘色：分区 × section、标题条 × caption、“下一关”的台阶 × buttonStepPrimary；
- `notes.txt`：每张图里自动选的字色与对比度、提示框的字块位置。

## 重建与验证

```powershell
& E:\anaconda3\python.exe -B tools/art_v11/glass/build_glass.py      # 界面件、桌面图、theme.json（约 2 分钟）
& E:\anaconda3\python.exe -B tools/art_v11/glass/review_glass.py     # 离线示意，读已交付的件
& E:\anaconda3\python.exe -B tools/art_v11/theme_contract.py audit glass
& E:\anaconda3\python.exe -B tools/art_v11/table_text_contrast.py glass
```

## 第一阶段自查

| 项 | 结果 |
| --- | --- |
| 四个保护区与 felt 色偏差 | 0 / 0 / 0 / 0（生成器检查与独立读图） |
| 11 个按钮与面板外接矩形内的装饰像素 | 0 |
| `page_scene` 与 felt 色偏差 | 0 |
| `theme_contract.py audit glass` | `25 sprites shipped`，`OPTIONAL Table/page_scene 1920x1080`，`AUDIT_OK`；其余 25 张为 FALLBACK（第二阶段） |
| `table_text_contrast.py glass` | 19 处全部 ≥4.5（最低 7.10），`TABLE_TEXT_OK` |
| 卡面框厚 ≤ 默认 | 是，每边 16 px 对默认 22–34 px |
| 两行提示（150 %）离内框底 | 8.2 单位（按 W20Xb 实测字块）；离线估计 6.5 |
| 色板规则（AX-08、骰点、pressure、稀有度、主按钮、数值角标） | 全部满足，数字见色板表 |

## 没能检查的

- 引擎里的样子：Unity 导入、毡面着色器的光、首领压暗与暗角叠在彩窗上、TMP 的真实字形与行高、按钮悬停与按下、720p。示意里的骰子是 2D 替身，卡面插画是风格包的锚点裁片。
- 第一阶段只画了框面一族与字标；图标、宝石、徽章、特效、卡背在示意里是默认件。

## 已知风险

- **托盘的边框淡**：代码给托盘乘 lerp(panel, panelLight, .55) = 483E34，在石板上只有 1.39:1；看得见的托盘框是里面那道暗铜双线（ghost 强调环）。把 panelLight 调亮能让托盘框更亮，但停用按钮上的 Muted 字会低于 3:1。
- **停用的“结束本关”是旧铜面**（panelLight），Muted 字 3.42:1，像一块暗金牌，不如默认风格那样一眼看出“停用”；字本身写着“达标后可结束”。
- **紧凑卡的短文**：本 kit 的卡框每边都比默认细，按代码的算法紧凑卡留给短文的宽和高都不少于默认。但 W20Xb 记录爆彩漫画的框也比默认细（11, 21, 21, 11 px），仍在 100 % 的迷你卡上丢了 C21、C22、C78、C79；原因没有查明，要在实机上核对（第二阶段的 10 张卡示意会按代码算一遍）。
- 次要按钮是深色（代码 `SolidButton11` 用 lerp(Background, Panel, .55)），概念页的次要按钮是羊皮纸；这是代码的选择，本 kit 不改。
- 顶栏（panel）与石板桌面的亮度只差 1.10，分开靠色相与铅条。

## 需要决定

1. **主按钮用钴蓝**（2152C4，roll 与 select 概念页）还是金色（shop 的“下一关”与默认、唐三彩、漫画的做法）。本 kit 选钴蓝：金色留给钱与奖励，钴蓝在满屏金框里最醒目。
2. **caption 用羊皮纸**（EBD6B0）还是彩色玻璃。同一个 token 管商店分区标题条和骰池三个标签；shop 概念页的标题条是红、蓝、青、紫玻璃，roll 的骰池签是羊皮纸。彩色 caption 会让骰池标签变成托盘上的一块彩色玻璃，首领关时还会和红色强调环混在一起。
3. **桌面两侧的尖拱窗**留着（彩窗只在窄边，压暗）还是去掉（窄边纯石板，桌面只剩玫瑰窗）。
4. **panelLight 用旧铜**（普通关牌签、本关牌标题条、空道具格带金意；代价是停用按钮像暗金牌、托盘边框淡），还是改成灰调（停用更像停用，金意少）。
