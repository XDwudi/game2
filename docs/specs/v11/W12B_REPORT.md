# W12b：瑞士主题适配 V1.1 对局画面

状态：**REVIEW**。输入为协调者提供的 `codex/v1.1 8f0433a9`、`PLAY_LAYOUT.md` §1/§8、`THEME_SPEC.md` 与 W11 的 7 张截图。执行日期：2026-11-16。工作目录仅 `E:\agent_workspace\gpt\wt-v11-swiss`；未运行 Unity、未提交。本文取代 W12/R2 README 中关于本轮资源数量、桌面布局和验收结果的旧说明；按本轮边界未修改其他文档、测试、C#、默认件、其他主题、`.meta` 或 `theme.json`。

## 1 改动与调用约定

新增四张 PNG 均为 **512×512 RGBA、PPU 200**，继续由导入器复制默认件边距。源图黑规则统一 **10 px**，对应正常 UI 中 **5 个设计单位**；切角 16 px。全部在 4 倍画布绘制后以 LANCZOS 缩小，没有渐变、纹理、台阶、高光或软阴影。

| 新件 | L/B/R/T 边距 | 内容与着色 |
| --- | --- | --- |
| `ui_dice_tray` | 72/72/72/72 | 外侧 10 px 黑线，内侧 10 px 白色可着色边带；其余完全透明。用于骰池和提交托盘，乘 `Lerp(Panel, PanelLight, .55)`。阶段色仍由代码叠加 `ui_ring`。 |
| `ui_topbar` | 40/40/40/40 | 黑框、纯白不透明中心；乘 `Panel` 得到黑色顶栏。 |
| `ui_chip_formula` | 40/40/40/40 | 黑框、纯白不透明中心；供算式、票签、徽记、页签和次要按钮共用，保留 `Panel`、阶段色与各因素色的实际明度。 |
| `ui_stage_card` | 48/48/48/48 | 黑框、纯白不透明中心；乘 `Paper` 得到暖纸色。标题条、首领框均由代码另画，源图没有内嵌分区。 |

上述判断依据 `GameApp.Play11.cs` 的 `Surface11`、`TopBar11`、`StageCard11`、骰池、提交栏及 `SolidButton11`，以及 `GameApp.Play11Score.cs` 的筹码调用。`Surface11` 直接保留传入 tint，不能给这些源图预先烘焙奶油色。

旧件复核了任务 C 列出的 **14 张**：

- `fx_score_crown`：保留黑冠和红底线，增加浅纸色轮廓，解决 48×32 王冠落在深色顶栏后融入底色的问题。
- `ui_icon_dice_view`：为黑色镜框和把手增加浅纸色外轮廓，保证 40×40 深色按钮中的轮廓连续。
- 其余 12 张保持原字节：`ui_panel_hud`、`ui_ring`、`ui_slot_inventory`、`ui_card_item`、`ui_frame_block`、`ui_tray_front`、`ui_tag_lock`、`ui_icon_coin`、`ui_icon_roll`、`ui_icon_lock`、`ui_pip_dot`、`fx_score_spark`。深面板、纸面黑边和锁标浅底在新布局中仍有效；中性 ring/pip/spark 与实心 `ui_frame_block` 保留调用所需语义；`ui_tray_front` 上下零边距，中央列保持恒定。

新契约共 90 个模板，当前按名引用 49 个。W11 新增 4 个引用，同时停止按名引用 `fx_formula_chip`、`fx_score_plate`、`fx_score_ray`、`ui_badge_check`。生成器明确保留这 4 张 W12 旧件，因此干净重建与已有目录均为 **53 张 UI、57 张交付 PNG**，不依赖目录中的残留文件。

## 2 桌面与坐标映射

`table_scene.png` 保持 **1920×1080 RGB**、纸色 `#F9EADB`。删除旧 x≈330 竖线、卡排下方横线和穿入托盘的红色大斜面。黑线只在顶/底外缘与 x=12–18 左侧外边距；红色斜面位于顶栏及本关牌的不透明覆盖区、右外缘和 y≥1048 的底部外边距。可变卡排的空位、整个骰池以及提交信息区下方均留白，不用桌图划分 UI 栏位。

`felt_tile.png` 不需修改：重建后字节不变，1024² RGB、对边完全一致。设置预览改为宽幅开口托盘、顶栏、纸卡、右侧黑面板与边缘红斜切。接触表更新为 53 组默认件／Swiss 对照。

延续当前桌面 shader 的 **contain 等比居中**，没有拉伸或裁切。以左上角为原点，常规完整 safe area 下：

```text
s = min(W / 1920, H / 1080)
screenX = (W - 1920*s)/2 + designX*s
screenY = (H - 1080*s)/2 + designY*s
```

这同时适用于 PNG 像素和 UI 设计单位。代码依据：`GameApp.Display.cs:26,34–39` 将 Canvas 设为 ConstantPixelSize 并居中；`Art07Felt.shader:37–44` 将屏幕 UV 逆变换到 16:9 桌图。3D 骰子以同一个 `designRoot` 映射。

| 屏幕 | 桌图/UI 内容区 | 额外空间 |
| --- | --- | --- |
| 1920×1080 | 1920×1080，原点 0,0 | 无 |
| 1280×720 | 1280×720，原点 0,0，缩放 2/3 | 无 |
| 1920×1200，16:10 | 1920×1080，原点 0,60 | 上下各 60 px |
| 2520×1080，精确 21:9 | 1920×1080，原点 300,0 | 左右各 300 px |
| 2560×1080 | 1920×1080，原点 320,0 | 左右各 320 px |
| 3440×1440 | 2560×1440，原点 440,0 | 左右各 440 px |

图外由现有 shader 显示 `_BaseColor * .24` 的房间底色。开启 TableMap 时，图内直接采样桌图，不再乘 `felt`；首领压暗、暗角、微小动态与骰子阴影仍由 shader 添加。本次没有改变这些代码。非全屏或偏心 safe area 时，UI 以 safe area 居中而 shader 以整个屏幕居中，这是既有集成限制，不能将离线宽高比合成当作此场景的实机验收。

## 3 确定性保护区检查

生成器在写出任何 PNG 之前调用 `check_felt_zones()`。度量为区域内每像素每个 RGB 通道相对 `(249,234,219)` 的绝对差的最大值，阈值 **0/255**：必须完全同色。检查整条常规提交 rail，并将信息区扩大至 360 宽；大字体下 rail 为 x=404–1480，包含在已检查区域内。输出：

```text
SWISS_ZONE dice_area rect=52,374,1408,276 max_rgb_delta=0 limit=0 PASS
SWISS_ZONE formula_row rect=52,660,1408,60 max_rgb_delta=0 limit=0 PASS
SWISS_ZONE submission_rail rect=344,740,1136,202 max_rgb_delta=0 limit=0 PASS
SWISS_ZONE submit_info_text150 rect=32,740,360,202 max_rgb_delta=0 limit=0 PASS
SWISS_BUILT 53 UI sprites (49 referenced, 4 legacy) / 57 PNGs total / supersampling 4x
```

检查器的反例验证：分别改动 4 个矩形左上／右下像素的一个通道，仅差 1 也会报错，**8/8 拒绝**。阈值检查的是导出源图，不包括 shader 光照、压暗和 UI 层。

## 4 验证与复核

环境：Windows、Python 3.11.4、Pillow 9.4.0、NumPy 1.24.3。重建命令：

```powershell
E:\anaconda3\python.exe -B tools/art_v11/swiss/build_swiss.py
E:\anaconda3\python.exe -B tools/art_v11/swiss/review_w12b.py
E:\anaconda3\python.exe -B tools/art_v11/theme_contract.py audit swiss --strict
```

严格审计原始输出：

```text
theme swiss: 53 sprites shipped, 0 referenced defaults not overridden
AUDIT_OK
```

| 检查 | 结果 |
| --- | --- |
| 重复生成 PNG SHA-256 | 57/57 一致；6/6 布局审查图也一致 |
| UI 尺寸、RGBA、非全透明 | 53/53 通过 |
| 中性件平均可见像素饱和度 <8 | 22/22 通过，均为 0；含 21 张显式中性件及 refresh |
| 新增 4 件边距、PPU、平整中心 | 4/4 通过；托盘中心 RGBA=0，其余中心 RGBA=255 |
| 完整九宫格中心／仅水平切片中央列 | 独立技术复核分别 22/22、5/5 恒定 |
| 桌图保护区 | 4/4 最大偏差为 0；8/8 单通道污染反例被拒绝 |
| felt 对边及格式 | 1024² RGB，对边一致 |
| 全部 Python unittest | **222 项，220 通过、2 失败**，见下述旧断言问题 |

完整测试通过现有 `tools/art_v11/swiss/run_tests.py` 运行未改动的 `unittest discover -s tests -v`；包装器只将临时写入限制到 Swiss 可写目录。两个失败均在旧 `test_art_v11_swiss.py`：

1. `test_exact_referenced_inventory` 要求交付集合与当前按名引用集合完全相等。现交付包含上述 4 张保留旧件，因此该断言失败；主题严格审计允许这些有默认模板的保留件。
2. `test_neutral_sprites_remain_tintable` 首先断言旧中性清单全部仍被当前代码按名引用；`fx_score_ray` 已不再被 W11 引用，使前置断言失败。它自身仍是零饱和度中性图，独立像素检查通过。

未删除旧件、改写契约或修改测试以掩盖失败。需由有测试修改权限的协调者同步 W11 的引用范围；本轮不宣称完整测试全绿。日志与逐图哈希在 `artifacts/cache/v11/swiss-w12b/{unittest.log,audit.txt,build-output.txt,verification.json,changed-files.json}`。

实施者已查看 7 张 W11 输入截图、新 1920 清洁合成、1280 注释合成、桌面区域图、预览和接触表中 6 张改动 UI 的原始裁片。独立 `visual_review` agent 对照 selecting/boss/scoring 截图，给出规格符合性（离线）与制作质量均通过；独立 `tests` agent 的技术复核通过。生成源 SHA-256：`b5bd16849e4fef0dc7c8522fcf62733738c28b29564539f9b5454eacd9656736`。均未替代协调者的引擎导入与人工 ACCEPTED。

## 5 onFelt 建议与剩余项

建议新 token **`onFelt: "151618"`**，全不透明。按 WCAG sRGB 线性化相对亮度计算，对原始 Swiss felt `#F9EADB` 的对比度为 **15.364172:1**，超过 4.5:1。它适用于直接落在桌面的槽号、空位点、锁定骰拖动点；不要把它误作桌布的乘色值。`theme.json` 未修改。

本轮没有审美方向异议。为适应透明托盘，红色大斜面收至覆盖区和外缘；画面中的主红色现在来自状态筹码与主按钮。剩余项：协调者需验证实际导入、首领暗角与阴影、720p 150% 字体以及宽高比窗口。深红文字叠红筹码/按钮、首领文本溢出和浅槽号/拖动点均按任务归代码方处理，未通过美术覆盖规避。离线合成的文字仅为尺寸和明暗示例，不模拟 TMP、真实卡面、3D 骰子或计分动画。

## 5a 协调者复核与修改（2026-11-16）

- 结论：规格符合性通过（离线）；制作质量通过：桌面平静，托盘内部是纯色，四个新件与瑞士界面件同一语言。实际导入和实机截图在合并后由 W11 返工的测试重拍。
- 两项 Python 测试失败的根因是 W11 改了契约：四个旧件不再被代码按名引用，而测试要求交付集合与引用集合完全一致。协调者决定保持这条严格要求，不让测试迁就交付：
  - 瑞士风格不再生成这四个旧件，已删除这四张 PNG 与 meta（`LEGACY_NAMES` 置空，绘制分支保留）；
  - 测试的中性件清单去掉 `fx_score_ray`，加入 W11 的四个着色表面。这样检查范围比原来多，没有放宽。
- 重建后：`SWISS_BUILT 49 UI sprites (49 referenced, 0 legacy) / 53 PNGs total`，四个保护区偏差都是 0，严格审计 `49 sprites shipped … AUDIT_OK`，Python 全量 **222/222 通过**（26.4 秒）。本文 §1、§4 写的“53 张 UI、57 张 PNG”与“220/222”是修改前的数字。

## 6 文件清单

修改：

- `tools/art_v11/swiss/build_swiss.py`
- `DicePrototype/Assets/Resources/Themes/swiss/UI/fx_score_crown.png`
- `DicePrototype/Assets/Resources/Themes/swiss/UI/ui_icon_dice_view.png`
- `DicePrototype/Assets/Resources/Themes/swiss/Table/table_scene.png`
- `DicePrototype/Assets/Resources/Themes/swiss/preview.png`
- `ArtSource/Art11/swiss/contact_sheet.png`

新增：

- `tools/art_v11/swiss/review_w12b.py`
- `DicePrototype/Assets/Resources/Themes/swiss/UI/ui_dice_tray.png`
- `DicePrototype/Assets/Resources/Themes/swiss/UI/ui_topbar.png`
- `DicePrototype/Assets/Resources/Themes/swiss/UI/ui_chip_formula.png`
- `DicePrototype/Assets/Resources/Themes/swiss/UI/ui_stage_card.png`
- `docs/specs/v11/W12B_REPORT.md`

审查输出均在 git-ignored 的 `artifacts/cache/v11/swiss-w12b/`：

- `table-zones-1920x1080.png`：桌图及区域矩形。
- `review-1920x1080.png`、`review-1280x720.png`：真实区域尺寸的九宫格与区域矩形；蓝线是审查标记，不在交付贴图中。
- `review-clean-1920x1080.png`：去掉区域线的合成。
- `review-contain-1920x1200.png`、`review-contain-2520x1080.png`：16:10／21:9 等比居中离线示意。
- `contact-sheet-w12b-focus.png`：从完整接触表裁出的 6 张改动 UI 的默认／Swiss 配对。
- 验证日志、哈希记录及 W11 输入截图的缩小副本；交付资源以 `DicePrototype/Assets/Resources/Themes/swiss/` 为准。
