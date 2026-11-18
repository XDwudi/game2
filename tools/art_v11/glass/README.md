# W20 彩绘玻璃 UI kit

状态：**第一阶段 1b 完成，提交协调者复核**（2026-11-18，分支 `codex/v1.1-glass`，起点 `codex/v1.1` `cd2fedb3`；第一阶段 `20d995da`）。1b 只改方向：桌面与非对局背景、面板与卡牌的框、按钮的形状、待定色板键 `buttonSecondary`、离线示意（含选关页与概念页并排）。图标、徽章、宝石、特效、卡背、接触表、卡面复核图、单测仍在第二阶段，按本轮复核结论完成。不是实机或美术 ACCEPTED。没有启动 Unity，没有用 GPT/Codex 生图，没有改 C#、`.meta`、默认件或其他风格的件。

输入：`docs/specs/v11/THEME_SPEC.md` §2、§3、§3.1、§5，`PLAY_LAYOUT.md` §1/§8，`SHOP_LAYOUT.md` §2，`CARD_TEMPLATE.md` §3/§6，`tools/art_v11/theme_contract.json`（94 张，有引用的 50 张），`ArtSource/StyleKit/themes/glass/` 的五张概念页与 `style_kit.json`，唐三彩、爆彩漫画两套 kit 的做法与检查，协调者 2026-11-18 对第一阶段的复核与 W20Y 的说明。代码依据：`GameApp.Play11.cs`（`L11`、`TopBar11`、`DrawTray11`、`DrawSubmit11`、`DrawActions11`、`SolidButton11`）、`GameApp.Play11Score.cs`、`GameApp.Play11Contrast.cs`（`FrameInsets11`、`LabelOn11`）、`GameApp.Card11.cs`（`PriceButton13`）、`GameApp.Shop14.cs`、`GameApp.V10.cs`（`StagePreview10`、`CycleCard10`）、`UiKit.cs`、`Art07ButtonMotion.cs`。

## 第一阶段的复核与 1b 的决定（协调者 2026-11-18）

复核结论：工程上没有问题（静区、装饰位置、对比度、文字空间、提示框余量、卡框比默认细、路径干净）；外观不对，“一眼看上去是深色石板界面加几道细铜线，看不出彩绘玻璃”，与爆彩漫画第一轮的结论相同。四个问题的决定：

1. 主按钮：钴蓝玻璃（照第一阶段）。
2. caption：羊皮纸（照第一阶段）；商店的颜色由 A 的彩窗提供。
3. 两侧尖拱窗：由 A 取代。
4. panelLight：保持旧铜，可微调（本轮没有调：调亮会让停用按钮的 Muted 字低于 3:1，托盘边框也只亮一点）。

返工：A 桌面与非对局背景铺满铅条彩窗、静区是石板并在外面加金色双线；B 面板与卡牌是金框加一道宝石玻璃条、四角金星；C 按钮是 重掷 的尖头，FrameInsets 不变；D 分区标题条照旧；E 量托盘边框，仍淡就提出办法。另外 W20Y 会加 `buttonSecondary`（见下文）。

## 形状与材质（取自哪张概念页）

| 形状 / 材质 | 概念页里的样子 | 用在 |
| --- | --- | --- |
| **铅条彩窗**：Voronoi 拼块（种子网格 40 px、抖动），钴蓝 44 %、宝石红 31 %、金 15 %、绿与紫各 5 %；每块背光、边上灰绘压暗、带一道斜纹；铅线 2.6 px；整体亮度 0.6 | 各页四周的红蓝彩窗 | `table_scene` 静区以外的全部、`page_scene` 全部 |
| **金色双线框住的石板**：静区是 felt 色石板，外面一圈方角金线（铅 1、金线脚 3、铅 1、细金 1.5、铅 1 px），线从静区边缘往外画，不进静区 | roll / triptych 骰池的石板与金框 | 骰区与算式行（连同两者之间 10 单位的缝）一块，提交说明与提交栏（两者相叠）一块 |
| **金框加宝石玻璃条**：铅、受左上光的金线脚、铅、一道背光的钴蓝或宝石红玻璃条（沿边不变）、铅、细金线、铅，共 17 px（8.5 单位）；45° 切角 | roll 左栏面板与提交格的金框，card 页卡框里的彩色玻璃，shop 分区 | `ui_panel_hud`、`ui_section_panel`、`ui_tab_bottom`（钴蓝）、`ui_paper_panel`（宝石红）、四种卡面（钴蓝；道具卡宝石红） |
| **金色四芒星**：八个切面左上亮、右下暗，铅线描边，坐在切角上 | 各页框角与按钮两端的金星 | 上一行所有框的四个角（角块内）；标签页只放上面两个 |
| **铅条里的玻璃块**：黑铅条、比玻璃暗一档的斜边、白色平面（=色板色） | roll 的钴蓝“重掷”、shop 的彩色标题条、triptych 的蓝玻璃钮 | 被代码着色的件 |
| **重掷 的尖头**：两端各两道斜边填满角块（按钮横 28 / 竖 34 px，筹码 22 / 30 px） | roll 的“重掷”、“第 6 关”牌签，select 的“开始挑战” | `ui_button_bevel`（`UiKit.Button`）、`ui_chip_formula`（`SolidButton11`、算式筹码、关卡牌签、骰池标签、已选标签、价格按钮） |
| **切角加铅条角窗** | roll 的面板与提交格 | `ui_topbar`、`ui_stage_card` |
| **双线环**：粗线、空隙、细线 | roll 提交格的双金框 | `ui_ring`（锁定格乘金色就是双金框，空格乘 ghost） |
| **尖头横条** | shop 的标题条 | `ui_section_header`（羊皮纸，照旧）、进度条、锁定骰子的铜台 |
| **玫瑰窗** | 圆形彩窗与金四芒星 | 行动行常空处一扇；字标左边一扇 |
| **标题字** | roll / select 的衬线粗体 | `ui_wordmark_dicecraft`：Noto Serif SC Black（CP3-2），铅黑字、金边 |

**按钮的金边（C）做不到写在贴图里**：`ui_button_bevel`、`ui_chip_formula` 是被代码着色的件，Unity 在线性光里把贴图乘上着色色。贴图边上画金色，在钴蓝主按钮上会变成约 1C3335 的暗青（钴蓝的线性红只有 0.015），在今天的次要按钮（191929）上几乎是黑的。所以按钮贴图保持中性（这也是协调者的着色规则），本轮给的是：

- 重掷 的尖头形状（角块内两道斜边；按钮侧边中段是被拉伸的切片，只能是直的：60 / 72 / 80 单位高的按钮直段 13 / 25 / 33 单位，40 单位以下的标签与牌签两端是真尖角）；
- 金色来自 `UiKit.Button` 下面的台阶（buttonStepPrimary B08A3E、buttonStep 8C6B33），沿下沿与下面两道斜边露出来；
- W20Y 之后，次要按钮本身就是琥珀金玻璃（`buttonSecondary`）。

要在钴蓝面周围画一圈真正的金边，需要代码在按钮上再画一层不着色的框（例如可选模板 `ui_button_frame` 盖在面上，`SolidButton11` 同理），见“需要决定”。

FrameInsets 在中行、中列量，尖头只在角块里，所以文字空间不变：`ui_button_bevel` 改前 10, 10, 10, 10 px，改后 10, 10, 10, 10；`ui_chip_formula` 改前 8, 8, 8, 8，改后 8, 8, 8, 8。

## buttonSecondary（待定键，W20Y）

W20Y 给 `SolidButton11` 的面加色板键 `buttonSecondary`（次要动作、跳过演出、查看骰子、上一手明细、计分明细、商店按钮、卡牌与商品行的价格按钮），今天的后备是 lerp(background, panel, .55) = 191929。本 worktree 的 `ThemePalette.cs` 还没有这个键，所以 theme.json 不写；生成器里记为 `PENDING_TOKENS`，离线示意按它画并在图题里写“simulated buttonSecondary”。合并时由协调者写进 theme.json。

取值 **AC8442**（琥珀金玻璃）：

| 量 | 值 |
| --- | --- |
| 字（LabelOn11，偏好 textColor 不到 4.5 时取最好的） | ink 5.15（≥4.95，面不加深） |
| 面对 felt（桌面与石板） | 4.24 |
| 面对 panel（深色面板上的按钮） | 4.66 |
| 面对 paper（卡面与商品行上的价格按钮） | 2.62：不到 3:1；按钮自带黑铅条外框，铅条对羊皮纸约 16:1，边界靠它 |
| 停用的面（往 panelLight 四分之一，9C793E） | Muted 字 1.97（代码给停用按钮写 Muted，停用控件可豁免）；价格按钮的原因字走 LabelOn11，取 ink 4.38，代码会把面调亮一两档到 4.95 |

为什么三个目标同时达不到：要对 felt（L 0.022）≥3:1，面的亮度 L ≥ 0.167；对 paper（L 0.754）≥3:1，L ≤ 0.218；字要 4.95:1，用现在的 ink（L 0.0094）要 L ≥ 0.245，用 textColor 要 L ≤ 0.134。没有交集。三个都要的话得同时改 ink 为 0D0B10、paper 为 F5E7CF（更浅的羊皮纸），面取 A57D3D 一带（字 5.2、felt 3.9、paper 3.1）。本轮没有改 ink 与 paper。

## 色板与对比度

24 个 token 与 W20X 的四个键都没改（数字同第一阶段）：AX-08 textColor/panel 13.97、ink/paper 13.51、muted/panel 7.81；onFelt/felt 10.89；骰点 EC655D / 55B6D9；主按钮的字 textColor 5.99；pressure 饱和度 0.82、色相 19°，字 ink 5.66；稀有度两两亮度比 1.88 / 2.94 / 1.57，对主按钮 ΔE 73.7 / 109.6 / 58.8、de00 39.3 / 49.6 / 30.2，对卡框金线 de00 31.2 / 37.2 / 55.8；ghost 对卡框金线 de00 21.5；数值角标对警示角标 de00 43.6、亮度比 3.42，字 ink 8.22 / textColor 6.42；caption 上的字 ink 12.42；section 格中心 Muted 7.81。完整表见第一阶段的提交 `20d995da`，生成器 `palette_report()` 打印全部数字。

**发现（代码，不在本 kit 内）**：选关页的“开始挑战”用 `UiKit.ActionButton(..., UiKit.Primary)` 画，`ActionButton` 把字定为 Ink，`StagePreview10` 没有像 `PrimaryButton11`、`ShopLeave14` 那样再用 `LabelOn11` 选字。钴蓝主按钮上的 Ink 只有 **2.58:1**（LabelOn11 会选 textColor 5.99）。对局与商店的主按钮不受影响。默认、唐三彩、漫画的主按钮是浅色，Ink 合适，所以以前没暴露。

## 桌面

- `table_scene.png`：静区以外是铅条彩窗；两块石板（骰区加算式行、提交说明加提交栏）外面一圈金色双线；行动行常空处的玫瑰窗。桌面图用 4×4 的盒式滤波缩小（没有振铃），所以从静区边缘往外画的线不碰静区里的像素：

  ```text
  GLASS_ZONE dice_area rect=52,374,1408,276 max_rgb_delta=0 limit=0 PASS
  GLASS_ZONE formula_row rect=52,660,1408,60 max_rgb_delta=0 limit=0 PASS
  GLASS_ZONE submission_rail rect=344,740,1136,202 max_rgb_delta=0 limit=0 PASS
  GLASS_ZONE submit_info_text150 rect=32,740,360,202 max_rgb_delta=0 limit=0 PASS
  ```

  两块石板外紧挨着的一行 / 一列像素全部不是 felt 色（金线从那里开始）。
- 玫瑰窗：中心 (1384, 1012)、半径 54，外接框 x 1330–1438、y 958–1066，在行动行每个状态（100 % 与 150 % 字号，连按钮焦点框与达标环）都空着的 x 1280–1488、y 942–1080 里，离它左右各 50 px、上 16 px、下 14 px；生成器另留 3 px 抗锯齿余量，检查它在常空矩形里、不进 11 个按钮 / 面板外接矩形。彩窗是铺满的纹理，按协调者的决定可以在面板下面（与漫画网点页同理）。
- `page_scene.png`：同一种彩窗铺满，没有石板与玫瑰窗。
- 亮度：彩窗 WCAG 亮度均值 0.025、中位 0.020、九成分位 0.047；石板 0.022，panel 0.016，paper 0.754，金框线脚约 0.44。按示意图调：亮度从 0.7 降到 0.6、红蓝为主、金块从 21 % 降到 15 %，让金框、羊皮纸、琥珀与钴蓝按钮比彩窗先入眼。
- 直接画在桌面上的字（`table_text_contrast.py glass`，非对局画面的背景 page_scene）：19 处中 **9 处低于 4.5:1**，最低 2.77（选关的预算行）；其余 10 处 5.66–7.28。按 THEME_SPEC §3.1 的决定，这 19 处由 W16、W15 放到面板或标题条上；第一阶段纯石板时 19 处全部 ≥7.10。

## 托盘边框（E）

托盘由代码乘 lerp(panel, panelLight, .55) = 483E34。量的是托盘件最亮的边带（L 0.046），对比只取彩窗像素（去掉金线与玫瑰窗）：

| 对比的对象 | 比 |
| --- | --- |
| 托盘外四边的彩窗（中位 L 0.022） | 1.33:1（上、下、左、右 1.34 / 1.33 / 1.35 / 1.28） |
| 托盘边与静区金线之间的彩窗细条（中位 L 0.018） | 1.42:1（左 1.35、右 1.49） |

托盘件本身仍淡。现在看得见的托盘边是静区外的金色双线（L 0.442）：对托盘外的彩窗 6.79:1、对石板 6.81:1。

办法（本 kit 路径内，未采用，等决定）：**方案 B**，石板一直铺到托盘边框里面，金色双线贴着托盘内沿；底边离算式行只有 2 单位，金线放不下，那里石板铺到托盘边、线压在托盘边下面。对照图 `ArtSource/Art11/glass/review/tray_rim_options.png`（左：现状，托盘边与金线之间有一窄条彩窗；右：方案 B，金线贴着托盘边，干净一些）。

## 文字空间（FrameInsets11 镜像，px：左、下、右、上）

| 件 | glass 1b | 第一阶段 | 默认 |
| --- | --- | --- | --- |
| 四种卡面 | 18, 18, 18, 18 | 16 | 22, 29, 27, 22（商店 27, 34, 32, 27；道具 22, 30, 28, 22） |
| `ui_panel_hud` | 18, 18, 18, 18 | 15 | 10, 8, 6, 10 |
| `ui_section_panel` / `ui_paper_panel` | 18 | 15 / 16 | 6 / 9, 18, 16, 9 |
| `ui_button_bevel` | 10, 10, 10, 10 | 10 | 7, 12, 12, 7 |
| `ui_chip_formula` | 8, 8, 8, 8 | 8 | 6, 16, 10, 6 |
| `ui_topbar` / `ui_stage_card` | 11 / 12 | 11 / 12 | 8, 30, 11, 8 / 19, 29, 27, 19 |

- 卡框每边仍比默认细。
- 提示框内框 9 单位：按 W20Xb 在 Unity 里量的两行提示字块（150 %，提示框内 15.7–72.3 单位），第二行离内框底 88 − 9 − 72.3 = **6.7 单位**（要求 ≥5）。
- 分区标题条高 68 − 9 − 2 = 57 单位。

## 离线示意（Python 模拟游戏的画法，不是 Unity 截图）

`ArtSource/Art11/glass/review/`（`review_glass.py`）：

- `play_a_100.png`、`play_a_150.png`、`play_f_100.png`、`play_f_150.png`：对局，状态 a（三张卡，五颗骰子选中两颗）、f（七张卡，十颗骰子锁定两颗）；
- `shop_100.png`：W14 商店，W20X 乘色（section、caption、buttonStepPrimary）；
- `select_100.png`：选关页（`StagePreview10`），桌面上的字照代码画在彩窗上；“开始挑战”照代码是 Ink 字；
- `beside_roll.png`（概念页 roll | 状态 a | 状态 f）、`beside_shop.png`、`beside_select.png`：与概念页并排；
- `tray_rim_options.png`：托盘边框现状与方案 B；
- `notes.txt`：每张图自动选的字色与对比度、提示框的字块位置。

所有示意按 `simulated buttonSecondary AC8442` 画次要按钮与价格按钮；还没画的件（图标、宝石、徽章等）是默认件，图题里列出。

## 重建与验证

```powershell
& E:\anaconda3\python.exe -B tools/art_v11/glass/build_glass.py      # 界面件、桌面图、theme.json（约 3 分钟）
& E:\anaconda3\python.exe -B tools/art_v11/glass/review_glass.py     # 离线示意，读已交付的件（约 30 秒）
& E:\anaconda3\python.exe -B tools/art_v11/theme_contract.py audit glass
& E:\anaconda3\python.exe -B tools/art_v11/table_text_contrast.py glass
```

## 1b 自查

| 项 | 结果 |
| --- | --- |
| 四个静区与 felt 色偏差 | 0 / 0 / 0 / 0（生成器检查与独立读图） |
| 金线只在静区外 | 是：两块石板外紧挨的像素全部不是 felt 色，静区内偏差 0 |
| 玫瑰窗位置 | 在行动行常空矩形内（左右 50、上 16、下 14 px）；不进 11 个按钮与面板外接矩形 |
| 按钮与面板外接矩形内的装饰 | 只有决定 A 要的两样：铺满的彩窗纹理，静区外的金色双线（静区在托盘与提交行里，所以线也在这两个框里）；没有别的成形图案，玫瑰窗不进 |
| `page_scene` 与 felt 色偏差 | 不适用：按决定 A，`page_scene` 是彩窗 |
| `theme_contract.py audit glass` | `25 sprites shipped`，`OPTIONAL Table/page_scene 1920x1080`，`AUDIT_OK`；其余 25 张为 FALLBACK（第二阶段） |
| `table_text_contrast.py glass` | 19 处中 9 处低于 4.5（最低 2.77），`TABLE_TEXT_BELOW`；W16 / W15 处理 |
| 卡框 ≤ 默认 | 是，每边 18 px |
| 按钮与筹码的 FrameInsets | 不变：10 / 8 px |
| 两行提示（150 %）离内框底 | 6.7 单位（按 W20Xb 实测字块） |
| 托盘边框 | 托盘件 1.33–1.42:1（仍淡）；看得见的边是金线 6.79–6.81:1 |
| 色板规则（AX-08、骰点、pressure、稀有度、主按钮、数值角标） | 全部满足，数字未变（见色板一节） |
| buttonSecondary（待定）的目标 | 字 5.15、对 felt 4.24 满足；对 paper 2.62 **不满足** 3:1（见上，需要决定） |
| theme.json 不含待定键 | 是：24 个 token 加 W20X 四个键，与生成器输出逐字节相同 |

## 没能检查的

- 引擎里的样子：Unity 导入、毡面着色器的光与首领压暗叠在彩窗上、TMP 字形与行高、按钮悬停与按下、720p。示意里的骰子是 2D 替身，卡面插画是风格包锚点裁片。
- 只画了框面一族与字标；图标、宝石、徽章、特效、卡背在示意里是默认件。
- 彩窗在 720p 下铅线约 1.7 px，没有实机看过是否发糊。

## 已知风险

- 彩窗铺满后，所有画在桌面上的字（主菜单、选关、整局结束，共 19 处）中 9 处低于 4.5:1，W16 / W15 前这些画面上的字难读。
- 面板（夜色玻璃，L 0.016）比彩窗（中位 0.020）还暗，靠金框与宝石玻璃条从彩窗里分出来。
- 卡面四角的金星：七张卡时一排 28 颗，示意里读作框角的装饰，没有喧宾夺主；实机再看。
- 停用的“结束本关”是旧铜面，Muted 字 3.42:1；停用的次要按钮（琥珀）上 Muted 只有 1.97（代码给停用按钮写 Muted）。
- 紧凑卡的短文：卡框每边仍比默认细，但漫画的框也比默认细，W20Xb 仍记录它丢了 C21、C22、C78、C79，原因未查明，要实机核对。

## 需要决定

1. **托盘边框**：保持现状（金线在静区外，与托盘边之间一窄条彩窗），还是方案 B（金线贴托盘内沿）。
2. **按钮的金边**：接受现在的做法（中性贴图、尖头、金色台阶，W20Y 后次要按钮是琥珀金玻璃），还是开一个代码任务在 `UiKit.Button` 与 `SolidButton11` 上加一层不着色的框（可选模板，例如 `ui_button_frame`），让钴蓝主按钮四周有真正的金边。
3. **选关页的“开始挑战”**：在 W16 里给 `StagePreview10` 的开始按钮加 `LabelOn11` 选字（一行代码；否则钴蓝上是 2.58:1 的 Ink 字），还是改用浅色主按钮。
4. **buttonSecondary**：AC8442 对羊皮纸只有 2.62:1（靠铅条外框分界）；如果要三项都过，需要同时改 ink（0D0B10）与 paper（F5E7CF），面取 A57D3D。
