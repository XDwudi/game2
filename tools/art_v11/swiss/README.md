# W12 瑞士现代主义 UI kit

状态：**R2 REVIEW，离线规格符合性与制作质量复核通过，2026-11-16**。依据已纳入仓库的五张概念图修订；锁图标修复后的全量回归与独立复核均已完成。供主协调者接入 Unity，不是实机或美术 ACCEPTED。输入为 `docs/specs/v11/DEV_PLAN.md` 的 W12 / §4.6、`THEME_SPEC.md`、`tools/art_v11/theme_contract.json` 及用户 R2 指令。R2 对小切角与骰子硬边投影的具体要求，取代最初文字稿的“只用直角 / 除按钮底座外无投影”。用户限定写入路径，因此任务交接写在本说明中，不修改 `docs/STATE.md`。

## 范围与产物

生成器为 [build_swiss.py](build_swiss.py)。执行一次输出 **53 张 PNG**：

| 路径（相对仓库根目录） | 数量 / 格式 | 用途 |
| --- | --- | --- |
| `DicePrototype/Assets/Resources/Themes/swiss/UI/<name>.png` | 49 张，RGBA | 契约中全部 `referenced: true` 的同名覆盖件；像素尺寸逐项沿用默认件 |
| `DicePrototype/Assets/Resources/Themes/swiss/Table/table_scene.png` | 1 张，1920×1080 RGB | 暖白桌面、下沿朝右上升的红色斜面、右上红色域与黑规则线；中央 x=360–1500、y=330–950 保持纯净纸色 |
| `DicePrototype/Assets/Resources/Themes/swiss/Table/felt_tile.png` | 1 张，1024×1024 RGB | 固定种子的极浅纸纹，周期采样并对齐两侧边缘，可无缝平铺 |
| `DicePrototype/Assets/Resources/Themes/swiss/preview.png` | 1 张，480×270 RGB | 设置选项缩略构图：深色面板、红色按钮、斜向色域与骰子图形 |
| `ArtSource/Art11/swiss/contact_sheet.png` | 1 张，RGB | 全部 49 件默认 / 瑞士并排，附尺寸、边距、PPU、桌面、预览、纸底上的 PPU 200 拉伸、1:1 源像素 tint 示例及五张概念图 |

未改 `.meta`、C#、场景、`theme.json`、3D 骰子资产或默认素材；未提交或推送。其余 37 张未被代码引用的契约项不在本次交付范围。接触表是离线检查图，不能代替 Unity 截图。

## R2 修改内容与原因

本轮已逐张打开 `ArtSource/StyleKit/themes/swiss/concepts/{roll,shop,select,card,triptych}.png`，并读取 [五套风格参考来源与 SHA 表](../../../ArtSource/StyleKit/themes/README.md)。参考图约束造型、色彩、线条与质感；图中的文字、数字及页面布局不作为实现契约。

| R2 修改 | 依据与原因 |
| --- | --- |
| 512² 纸面板统一为 10 px 纯黑轮廓、16 px 单段小切角 | select / card 的硬纸卡框有明确的粗轮廓与切角；R1 在纸底上的框线偏弱。PPU 200 下，10 个源像素对应 5 个参考 UI 像素 |
| 深色板与按钮共用切角语言，卡牌仅在固定角块放小红三角 | roll / shop 的黑面板、红色角部强调与 card 的卡框形成同一体系；九宫格中间保持平坦，不烘焙标题带或跨中心的大楔形 |
| 骰子影恢复宽黑色硬边多边形，alpha 77 / 255（约 30%） | roll / triptych 使用明确的平面接触影。R1 的细定位线不能表达这种脚下重量；R2 明确允许恢复，不增加柔化或渐变 |
| 桌面主斜面改为从下沿向右上升，保留右上红块与黑色分隔线 | 对应 roll / select / triptych 的斜向动势；主边界从 `(0,1140)` 到 `(1920,910)`，在 x=1500 处 y≈960.31，留出中央玩法区而不照搬 roll 穿过骰池的红白交界 |
| 附魔徽章去除整条红顶带，换成切角纸签与小红角；书页加镂空行线，刷新改为圆形回转箭头 | 与 shop / triptych 的粗几何功能图标一致，减少色带装饰对语义的干扰；仍保持 tint 蒙版规范 |
| 勾、锁、皇冠采用黑色主体与小面积红色强调 | 保持图标族一致；锁图标另加浅纸色切角承托底，避免黑色锁体在实际深色禁用标记上消失，需回归 22×24 显示尺寸 |
| 接触表补入全部五张参考与纸底上的 PPU 200 样例 | 让检查同时覆盖概念关系、同色纸面上的框线强度和九宫格拉伸；另一区域保留源像素 1:1 tint 演示，避免混淆两种比例 |

R1 的说明、证据与产物归档于 `ArtSource/Art11/swiss/history/r1/`，不把旧版通过结论沿用到 R2。23 个 palette token 建议保持不变。

## 造型、尺寸与九宫格

- 全部运行时图形先在 **4 倍**画布上绘制，最后用 Pillow `LANCZOS` 缩小；没有低分辨率放大、模糊、渐变或斜面高光。
- 六种 512² 纸面件（`ui_paper_panel`、四种卡面、`ui_chip_label`）统一使用 **10 px 纯黑轮廓与 16 px 单段小切角**，保持硬边平涂；不是阶梯角或圆角。五种完整九宫格深面板采用同样角形、纯近黑内场；`ui_tray_front` 作为第六种深色件按其特殊边距另作处理。按钮自身不烘焙投影，现有 `UiKit.Button` 负责额外底座。
- 尺寸与九宫格 `(left, bottom, right, top)` 以契约为唯一依据。常见 512² 面板使用四边 40 px；全部框线与固定角标限制在相应边距带内，中心为均匀实色或透明。
- `ui_frame_block` 是实心白色切角块，供 `UiKit.Frame` 叠放外层与内层来得到边框；不能把它当作一个已烘焙边框的纸面板。
- `ui_button_bevel`、`ui_price_chip` 使用同样的 10 px 中性黑框 / 白内场。`ui_ring`、`ui_button_ghost` 为 10 px 白色切角框、透明中心；`ui_frame_enchant` 为向内缩进 14 px 的 6 px 白色小切角框。环与附魔框的调用者还会设置 `fillCenter=false`，不得遮住 3D 骰子或卡面。
- **上下边距为 0** 的 `ui_progress_fill`、`ui_progress_track`、`ui_section_header` 为平涂条；`fx_formula_chip`、`ui_tray_front` 的切角只占左右固定端头，前者另有左端小红三角。它们不画水平顶部 / 底部框线，中央列从上到下一致，避免纵向拉伸改变边线厚度。
- `fx_score_plate` 的四边距为 104 px，使用 10 px 白色边线与 20 px 小切角；深色内场承受调用方的 Blue / Gold 乘色，白边接收该 tint。卡背为 768×1024 原生尺寸，外轮廓 16 px / 切角 24 px；标题纸面载体外轮廓 12 px / 切角 16 px。
- `ui_die_shadow` 为低透明度、宽幅硬边黑影；仅表现接触与重量，没有模糊或渐变。这项以 R2 的恢复投影要求为准。

## 烘焙色与运行时 tint

运行时最终颜色约等于 `PNG RGB × Image.color`。本说明的 **N** 表示中性白 / 灰蒙版，颜色由调用者决定；**A** 表示烘焙色，通常由 `Color.white` 原样显示；**A×** 表示烘焙件仍有乘色，须特别检查。

用户列出的 **17 张 N 件**全部保持无彩色：`ui_button_bevel`、`ui_button_ghost`、`ui_frame_block`、`ui_frame_enchant`、`ui_ring`、`ui_price_chip`、`ui_progress_fill`、`ui_progress_track`、`ui_section_header`、`ui_pip_dot`、`ui_icon_arrow`、`ui_icon_book`、`ui_icon_gear`、`fx_arc_short`、`fx_score_ray`、`fx_score_spark`、`fx_square_chip`。此外，`ui_icon_refresh` 也使用白色剪影，因为它会乘以 Ink；给它烘焙红色会被黑色 Ink 压成黑色。

以下源文件均位于 `DicePrototype/Assets/Game/`；行号指本次工作树的原始调用版本，协调者后续改版时应重新核对。

| Sprite | 用途与本套造型 | tint 约定 / 调用证据 |
| --- | --- | --- |
| `badge_enchant_base10` | 基础分附魔；切角纸白签、小红角与粗加号 | A；白色显示，`UI/GameApp.V05.cs:122–125` |
| `badge_enchant_mult2` | 倍率附魔；切角纸白签、小红角与粗几何乘号 | A；同上 |
| `badge_enchant_roll1` | 投掷附魔；切角纸白签、小红角与骰杯 | A；同上 |
| `badge_enchant_slotless` | 免槽附魔；切角纸白签，四个直角括号围出空槽 | A；同上 |
| `badge_enchant_submit1` | 提交附魔；切角纸白签，清单与红色确认勾 | A；同上；附魔演出也使用此图 |
| `fx_arc_short` | 倍率触发的短运动轨迹；折线弧段 | N；附魔演出固定紫色，`Runtime/Presentation/Art071EnchantCue.cs` |
| `fx_formula_chip` | 高分牌下方算式条；黑色切角端头、左上小红三角，无水平边线 | A；白色显示，文字为 TextColor，`UI/GameApp.Art073HighScore.cs:83–86` |
| `fx_score_crown` | 最高档高分演出冠标；黑色几何冠、红色底条 | A；白色显示，`UI/GameApp.Art073HighScore.cs:91–92` |
| `fx_score_plate` | 高分横牌；深色中心、10 px 白边与 20 px 小切角 | A×；整图乘 Blue / Gold，正文为 TextColor 或固定 FFF1CF，`UI/GameApp.Art073HighScore.cs:52,62–79` |
| `fx_score_ray` | 高分横牌两侧的短放射条 | N；乘 Gold，`UI/GameApp.Art073HighScore.cs:57–58` |
| `fx_score_spark` | 高分火花；几何十字 | N；乘 Red / Blue / Gold，`UI/GameApp.Art073HighScore.cs:98–99` |
| `fx_square_chip` | 纸屑 / 计分粒子；实心方片 | N；预制件使用 Paper 或 Red / Blue，`Editor/Art07AssetLibrary.cs:324,336` |
| `ui_badge_check` | 已贡献计分卡牌的勾标；黑色小切角块、浅色勾与小红角 | A；白色显示，`UI/GameApp.Art073HighScore.cs:107–108` |
| `ui_button_bevel` | 通用按钮面与独立底座；白底、10 px 黑框、16 px 切角，名称保留但没有 bevel | N；面乘 Gold / Paper / PanelLight / 动作色，底座目前另乘固定棕色；`UI/UiKit.cs:115–127,163–167` |
| `ui_button_ghost` | 次级按钮；透明中心、10 px 白色小切角框 | N；乘 Ghost，禁用时降低 alpha；`UI/UiKit.cs:145–150` |
| `ui_card_back` | 主菜单卡背；16 px 黑色切角外框，纸白、黑与红色斜向构图 | A；白色显示，`UI/GameApp.Art071Screens.cs:33–35` |
| `ui_card_controller` | 完整卡牌底板；纸白、10 px 黑色切角框、右下小红三角 | A；`CardSurface` 强制白色显示，`UI/UiKit.cs:130–136` |
| `ui_card_controller_compact` | 紧凑卡牌底板；沿用同一纸面与角标 | A；同上 |
| `ui_card_item` | 道具卡底板；纸白、10 px 黑色切角框、右下小红三角 | A；在 CardSurface 后替换 sprite，保持白色，`UI/GameApp.Art07.cs:222` |
| `ui_card_shop` | 商品卡底板；纸白、10 px 黑色切角框、右下小红三角 | A；白色显示，`UI/GameApp.Art073Shop.cs:74–77` |
| `ui_chip_label` | 骰子样式名称签；浅纸白底、10 px 黑色切角框 | A；白色显示、Ink 字，`UI/GameApp.Art073StyleLegend.cs:46–47` |
| `ui_die_shadow` | 界面骰子的宽幅接触影；纯黑硬边多边形，alpha 77（约 30%） | A；白色显示，`UI/GameApp.Art073Dice.cs:53–57`；R2 恢复平面投影，桌面 shader 的实际骰影机制不变 |
| `ui_frame_block` | 通用框 / 选中标签的基础实心小切角块 | N；外层乘 edge、内层乘 fill；`UI/UiKit.cs:145,174–178` |
| `ui_frame_enchant` | 卡牌附魔细框；透明中心、缩进 14 px 的 6 px 白色小切角框 | N；乘附魔颜色，`UI/GameApp.Art073Shop.cs:20–31` |
| `ui_icon_arrow` | 下一关按钮的箭头 | N；乘 Ink，`UI/GameApp.Art073Shop.cs:93` |
| `ui_icon_book` | 手册按钮图标；两页几何书与镂空行线 | N；NavIcon 乘 Ink，`UI/GameApp.Art07.cs:40–52` |
| `ui_icon_coin` | 价格与奖励的硬币标记；浅色币面、黑边、红条 | A；白色或 alpha 0.6，须在红价签上可见；`UI/GameApp.Art073Shop.cs:53` |
| `ui_icon_dice_view` | 查看骰子 / 新骰提示的几何骰面与放大镜 | A；白色显示，`UI/GameApp.Art073DiceLibrary.cs:107–108` |
| `ui_icon_gear` | 设置按钮的齿轮 | N；NavIcon 乘 Ink，`UI/GameApp.Art07.cs:40–52` |
| `ui_icon_lock` | 道具卡锁定标记；浅纸色切角承托底、黑色锁梁 / 锁体、红色锁孔 | A；白色显示，实际外层 UnavailableLock 为 Background，须核验 22×24 px 深底识别；`UI/GameApp.Art07.cs:227–228` |
| `ui_icon_refresh` | 刷新商店 / 随机种子；白色圆形回转箭头 | N（额外识别）；乘 Ink，`UI/GameApp.Art073Shop.cs:127`、`UI/GameApp.Art07NewRun.cs:48–49` |
| `ui_icon_roll` | 投掷次数与附魔演出的骰杯 | A；白色显示，次数耗尽时 alpha 0.38；`UI/GameApp.Art07.cs:125` |
| `ui_info_inset` | 分数读数内板；黑色小切角块与纯近黑内场 | A；白色显示，承载 Muted 和语义色数字；`UI/UiKit.cs:190–191`、`UI/GameApp.Art07.cs:82–90` |
| `ui_panel_hud` | 状态 / 分数主面板；黑色小切角块、右下小红三角 | A；Surface 默认白色，承载亮色文字；`UI/GameApp.Art07.cs:60,427` |
| `ui_paper_panel` | 纸面弹窗、卡片与主菜单面板；纸白、10 px 黑框、16 px 小切角 | A；PaperFrame 忽略 fill 颜色参数，强制白色显示；`UI/UiKit.cs:155–160` |
| `ui_pedestal` | 商品骰子底台；浅顶面、黑正面、红线 | A；白色显示，`UI/GameApp.Art073Shop.cs:188` |
| `ui_pip_dot` | 界面精确点阵的单个点 | N；按实际骰面色乘色，`UI/GameApp.Art073DiceLibrary.cs:87–88`、`UI/GameApp.Art07Shop.cs:197` |
| `ui_price_chip` | 价签；白底、10 px 黑色小切角框，运行时变为红块 | N；乘 Gold 或 Gold / Paper 混合，价格字使用 Ink；`UI/GameApp.Art073Shop.cs:47–55` |
| `ui_progress_fill` | 分数 / 进度条填充 | N；乘调用者 fill，`UI/UiKit.cs:210–213` |
| `ui_progress_track` | 进度条轨道 | N；当前乘固定 07191C，`UI/UiKit.cs:206–208` |
| `ui_ring` | 骰子槽、锁定托盘、选中框；透明中心、10 px 白色小切角框 | N；乘调用者颜色，`fillCenter=false`；`UI/UiKit.cs:197–201` |
| `ui_section_header` | 商店分区标题带；实心直角条 | N；当前三个分区仍乘固定 B8433F / 2F8F86 / 7B5FB5，`UI/GameApp.Art073Shop.cs:15,40–43` |
| `ui_section_panel` | 商店与骰子样式分区底板；黑色小切角块、纯近黑内场 | A；白色显示，承载 TextColor / Muted；`UI/GameApp.Art073Shop.cs:36–43`、`UI/GameApp.Art073StyleLegend.cs:34,48` |
| `ui_slot_inventory` | 空商品 / 卡牌槽位；黑色小切角块，不烘焙额外定位标 | A；白色显示，空位文案使用 Muted；`UI/GameApp.Art073Shop.cs:114–117` |
| `ui_tab_bottom` | 操作区底部解释条；黑色小切角块，去掉纸色顶线 | A；调用方显式恢复白色，文字为 Muted；`UI/GameApp.Art07.cs:312–315` |
| `ui_tag_clock` | 本关临时骰标记；切角纸签、方形钟面与红色指针 | A；白色显示，`UI/GameApp.Art073StyleLegend.cs:44` |
| `ui_tag_lock` | 已锁定骰标记；切角纸签、黑锁与红色锁孔 | A；白色显示，`UI/GameApp.Art073StyleLegend.cs:44` |
| `ui_tray_front` | 已锁骰的托盘前沿；纯黑色场，切角仅在固定左右端头，无水平边框 | A；白色显示，样式页还有 Muted 编号；`UI/GameApp.Art07.cs:285–286`、`UI/GameApp.Art073StyleLegend.cs:38–39` |
| `ui_wordmark_dicecraft` | 主菜单标题字标；12 px 黑色切角纸面外框、中文粗黑字与抽象红方块标志 | A；默认白色 Image，位于纸面上，`UI/GameApp.Art071Screens.cs:14–19` |

## 色板与 23 个 token 建议

图片实际使用 `F9EADB` 纸白、`FCF1E0` 浅纸白、`151618` 近黑、`000000` 纯黑、`E80E16` 红、`96948E` 暖灰、`C8BEB5` 浅褐灰及中性蒙版白。红是烘焙 UI 的唯一彩色强调。生成器另保留 `D60D0E` 深红常量，供调色时使用；当前成品主要红为 `E80E16`。

以下仅为 `theme.json` 的 `palette` 建议，**本任务不写入配置**：

```json
{
  "background": "151618",
  "felt": "FFFFFF",
  "panel": "151618",
  "panelLight": "4E4E4E",
  "paper": "F9EADB",
  "ink": "000000",
  "textColor": "FCF1E0",
  "muted": "C8BEB5",
  "gold": "E80E16",
  "teal": "C8BEB5",
  "red": "EC655D",
  "blue": "55B6D9",
  "violet": "96948E",
  "danger": "D60D0E",
  "shadow": "000000",
  "ghost": "96948E",
  "primary": "E80E16",
  "reward": "E80E16",
  "boss": "E80E16",
  "pressure": "96948E",
  "rarityCommon": "96948E",
  "rarityAdvanced": "4E4E4E",
  "rarityRare": "E80E16"
}
```

`felt=FFFFFF` 避免现有材质再给已着色桌面乘上一层青色。`background/panel` 保留近黑，因为当前共享组件的亮色正文依赖深底；桌面图本身仍是暖白。运行时 `ink` 采用纯黑，以满足红色按钮和价签上现有黑字的对比度；R2 的面板外轮廓也统一为 `000000`，深色内场与图标主体仍可使用 `151618`。

按 sRGB 线性化后的相对亮度 `L=0.2126R+0.7152G+0.0722B`，对比度 `(L亮+0.05)/(L暗+0.05)`：

| 文本 / 底色 | 色值 | 比值 | 目标 |
| --- | --- | ---: | ---: |
| textColor / panel | FCF1E0 / 151618 | **16.207:1** | ≥4.5 |
| ink / paper | 000000 / F9EADB | **17.820:1** | ≥4.5 |
| muted / panel | C8BEB5 / 151618 | **9.905:1** | ≥3 |
| 当前按钮 / 价签黑字 | 000000 / E80E16 | **4.506:1** | ≥4.5 |
| Danger 按钮浅色字 | FCF1E0 / D60D0E | **4.791:1** | ≥4.5 |

最后两项是额外静态核对。4.506:1 接近阈值，按钮按下态的额外乘色仍须实机验证；不能把静态 token 比值当作所有状态可读性通过。

`red/blue` 保留默认的 `EC655D/55B6D9` 是**骰子语义例外**：`Runtime/DiceTable.cs:225` 把 `UiKit.Red/Blue` 直接写进 3D 骰面材质；`UI/GameApp.Art07Effects.cs:619,622` 与 `GameApp.Art07PostEffects.cs:51` 的骰面过渡也使用它们。把 Blue 改成暖灰会同时改动真正的骰点，超出本任务边界。当前代码把 UI 动作色与骰点语义色共用，因此应用本建议后，部分 UI 仍会出现蓝色；彻底实现单红 UI 需协调者在 W11 等接口工作中分开这些用途。

## 重建与验证

在本 worktree 根目录执行：

```powershell
& E:\anaconda3\python.exe tools/art_v11/swiss/build_swiss.py
& E:\anaconda3\python.exe tools/art_v11/theme_contract.py audit swiss --strict
& E:\anaconda3\python.exe -m unittest discover -s tests -v
```

本轮因用户的严格写入边界，实际以 `E:\anaconda3\python.exe -B tools/art_v11/swiss/run_tests.py` 运行**同一套完整 discovery**。旧测试显式在 `tools/v11_text` 以及系统临时目录建夹，此启动器仅将 `tempfile.mkdtemp` 位置重定向至 `ArtSource/Art11/swiss/test-temp` 并禁止写入 pycache；测试、断言和发现范围不变，运行后临时目录已清理。

生成器只使用仓库字体，不依赖系统字体回退：

- 中文字标：`DicePrototype/Assets/Game/Fonts/NotoSansSC-Bold.otf`，同目录保留 `LICENSE.txt`、`SOURCE.json`。
- 接触表标签：`DicePrototype/Assets/TextMesh Pro/Fonts/LiberationSans.ttf`，同目录保留 `LiberationSans - OFL.txt`。

依赖为 Python、Pillow 与 numpy；本轮指定环境为 `E:\anaconda3\python.exe`、Pillow 9.4、numpy 1.24。没有 OpenCV 或外部生图调用。噪声种子固定为 11012；PNG 显式使用 `optimize=False, compress_level=9`，没有时间戳、EXIF 或机器相关元数据。确定性承诺指相同源文件、契约、字体和依赖版本下的重复运行。

R2 最终实际结果：Python 3.11.4、Pillow 9.4.0、numpy 1.24.3。全量 **213/213**（旧 197 + 瑞士 16，其中 R2 新增 5），26.322 秒，0 失败 / 0 错误；两次完整重建与交付 PNG 的 SHA-256 全部相同。五张已入库概念图的 SHA 与 `themes/README.md` 来源表 **5/5 一致**。相比 R1，53 张 PNG 中有 **38 张**变化。最终生成器 SHA-256 为 `6721b25daf6962354b8f4b00d4b408832686c3031ed4122186b7f81e1b5e4a7f`。

| 条件 | 方法 | 当前证据状态 |
| --- | --- | --- |
| W12-01：49 件齐全、RGBA、与默认尺寸相同 | 契约严格审计与单元测试 | 49/49；`AUDIT_OK`，0 缺件、0 多余 |
| W12-02：重复生成字节一致 | 新增测试的两次完整生成比对 | 全部 53 张 PNG 字节一致，且与交付件一致 |
| W12-03：中性件平均饱和度 <8、无全透明件 | 新增测试及独立像素检查 | 17 张指定中性件平均饱和度均为 0；49 件均有可见像素 |
| W12-04：框线、九宫格与视觉一致性 | 打开五张概念图与对照图、纸底 PPU 200 宽 / 高拉伸样例；独立像素检查 | 18 张完整九宫格中心平坦，5 张横向九宫格中央列一致；R2 独立离线复核通过 |
| W12-05：桌面与预览尺寸、纸纹平铺 | 新增测试与图像检查 | 尺寸正确、平铺边缘逐像素一致；桌面指定中央区全为 F9EADB，纸纹红通道标准差约 0.995 |
| W12-06：23 项色板与对比度 | 上节静态计算 | 23 项齐全；要求三组比值 16.207 / 17.820 / 9.905 |
| W12-07：全部旧测试与新测试通过 | 同一套完整 unittest discovery，临时写入限定目录 | 213/213 通过，26.322 秒 |
| W12-R2-01：线宽、切角与暗面板不偏离 R2 | 新增纸面黑框与面板切角 / 中央色域检查 | 通过；10 px 黑框在 PPU 200 下的 5 px 参考 UI 效果另做离线视觉核验 |
| W12-R2-02：恢复宽幅平面接触影 | 新增阴影面积、颜色、alpha 检查 | 通过；纯黑多边形、alpha 77，无渐变 |
| W12-R2-03：锁图标在实际深底与紧凑尺寸可辨 | 新增 22×24 px 深底回归，加独立视觉复核 | 通过；浅纸色承托底解决黑锁与 151618 背景混合的问题 |
| F-THEME-02 / AX-08：引擎关键页面、状态与切换 | 协调者 Unity 导入、1080p / 720p 截图 | 本环境未运行，待协调者接入 |

本次证据位于 `ArtSource/Art11/swiss/`：`unittest-r2.log`（最终原始 Python 日志）、`audit-r2.log`（49 件、0 fallback、`AUDIT_OK`）、`review-r2.md`（独立复核：规格符合性 PASS、制作质量 PASS_OFFLINE，未发现剩余可复现缺陷）、`manifest.json`（协调者绑定最终输入、源与 53 张输出的哈希）。R1 的 208 项测试与旧审查记录属于 `history/r1/` 历史，不能代替本次证据。

文件锁已释放；下一步由协调者导入，选择是否采纳色板建议，核验实际 tint / 字体 / 主题旁路，再提供黄金样例截图。没有修改 `theme.json`，因此本目录图片本身不会自动应用本建议色板。

## 参考与接入交接

五张参考现已作为原图逐字节副本纳入 `ArtSource/StyleKit/themes/swiss/concepts/`，对应关系为 `roll→01-roll`、`shop→01-shop`、`select→01-select`、`card→01-card`、`triptych→2026-11-15 的 01`。本轮已实际查看全部五张并核验来源 SHA；接触表也并排列出它们。R1 未取得参考时的记录保留在历史目录，本轮不再把“参考缺失”列为当前限制。所有读写仍限制在当前 worktree，没有访问其他 worker 的工作目录。

协调者接入时需处理或核实以下现有接口限制；W12 没有修改相关代码：

1. **主题读取旁路。** `GameApp.V05.cs:122–125`、`GameApp.Art071Enchants.cs:24,40` 直接从 `enchantIcons` 查附魔徽章，当前 `ThemeRegistry.Activate` 只设置 `UiKit.ThemeTemplate`，未改写该数组。交付五张徽章不等于它们已在当前引擎中替换生效。预制件里的 `fx_square_chip` 也要检查主题切换是否会重新绑定。
2. **固定颜色。** `UiKit.Button` 的 Step 仍使用 `B56B29/8F453A`，Lip 为半透明白条；`Section073` 的三个标题色仍为 `B8433F/2F8F86/7B5FB5`；`Progress` 轨道固定 `07191C`。这些调用无法由中性 PNG 强制变成黑红。附魔 `submit1` 固定 `3F8F6B`，`Art071EnchantCue` 的 Mint / Violet / Paper 也有固定值。
3. **浅桌面上的亮字。** THEME_SPEC §5 已记录部分文字直接落在桌面而使用 TextColor / Muted。桌面按需求保持暖白，没有为了补偿旧布局烘焙假面板。此类文字需协调者通过布局或专用桌面文字色修正。
4. **槽与次级按钮的实际 tint。** PNG 中性切角框最后会变成调用者的 Ghost / Gold 等颜色。接触表额外展示黑框槽是建议构图，不代表当前所有调用者都使用黑色；纸底 PPU 200 样例与源像素 1:1 tint 样例已明确分区。
5. **验收边界。** 实机黄金样例、真实缩放、按钮悬停 / 按下、骰点可读性、主题来回切换、画面风格认可仍由协调者与用户完成。离线尺寸、对比度和单元测试不替代这些结论。
