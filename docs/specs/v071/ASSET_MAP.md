# v0.71 全量素材路径表

2026-11-09 UTC，全量资源已接入，实际表现专项验证中。71项活跃内容全部绑定高清主题插画，旧小图标不再由Bootstrap/Lab引用；下方首批记录保留为历史。全量技术验证不替代人工风格验收。

| 范围 | 可编辑源 / 运行件 | 当前状态 |
| --- | --- | --- |
| 71项插画 | `ArtSource/Art071/IllustrationsFull/<ID>/` 原图、完整prompt、record；C03/C48/I07复用 `Illustrations/` 原稿 → `DicePrototype/Assets/Art/Art071/Illustrations/illustration_<id>.png` | 50控制器、7道具、7服务、7强化全量。C29新修订，C45/revision-01、O05/revision-02为最终选图。全部原稿保留。 |
| A01/A02 桌布/光照 | `tools/art_v071/build_full_background.py` → `ArtSource/Art071/Full/Backgrounds/` → `Assets/Art/Art071/Full/Backgrounds/`；Art07Felt.shader | 新1024²无缝Repeat纹理；中心受光与边缘压暗在桌面Shader，不覆盖文字/骰点。 |
| A03—A06 共享纸面/按钮/页签 | 既有 `build_surfaces.py` 的5表面与UiKit，新增 `build_full_surfaces.py` 的12表面 → `Full/Surfaces/` | 清楚的信息/内容/动作层；库存内容纸卡去厚按钮侧边，实际命中保留。页签与精确线条继续代码绘制。 |
| A07/A18 卡背/字标 | `tools/art_v071/build_full_ui.py` → `ArtSource/Art071/Full/UI/` SVG+PNG → `Assets/Art/Art071/Full/UI/` | 768×1024卡背、1440×400标题字标接入菜单，正文仍TMP。 |
| A08—A11 代表插画 | C48/C29/C03/I07路径同上 | 复用3件并重新修订C29，已做全量Unity并排。 |
| A12 附魔/状态 | `build_full_ui.py` → `Full/Symbols/`（5附魔与28状态符号，实际路径以manifest为准） | 5附魔徽记、28公共符号，256²高清，含实际代码颜色/状态。5类表现按提交事件与真实实例绑定。 |
| A13—A16 骰子/点阵/材质/阴影 | `ArtSource/Art071/Dice/dice_master.blend`、face_map/export_preset；现有 `build_dice.py`；`Assets/Art/Art071/Dice/`；DiceTable/Art07Ceramic/Art07Felt | 陶瓷母模型复用；点阵按真实配置生成，浅点窝/玻璃/金属/光滑/纹理层保留。新增临时小钟与减轻投影已完成真实GPU及720/1080锁槽专项，独立限域复核通过。 |
| A17 FX | `build_full_ui.py` → `ArtSource/Art071/Full/FX/` 8张SVG+PNG → `Assets/Art/Art071/Full/FX/` | 含新短弧、环、星、方片、火焰。新高档环/短弧与五附魔已通过实际接线及720/1080专项；完整冻结回归仍按STATE推进。 |
| 实际共享Prefab/材质 | `Assets/Art/Art071/Full/Prefabs/`、`Full/Materials/` | 26个Prefab重绑新素材；功能轮盘仍由引擎显示9个真实分区与结果，不用主题插画代替随机结果。 |

完整逐件源/运行SHA以 `DicePrototype/Assets/Art/Art071/FullSourceManifest.json` 为准（127件=71插画+43公共+12表面+1背景）。可重跑接入入口为 `tools/art_v071/stage_full_art.py --complete` 和 `Assets/Game/Editor/V071FullSetup.cs`。生成PNG是可编辑栅格原稿，不冒称有手绘图层的KRA/PSD；公共SVG、脚本与Blender源另保留。

新实机证据：`nunit-20261109-035235`图册/三字体卡片/强化交易专项3/3通过；修正图册共12页，每项有大卡、176/64深浅底共5真实实例。71项独立逐图限域审查已完成。新增临时钟旧失败保留于034902/040031，修后040217/040605通过；高档Combo041014/041301通过。源码41aface完整Edit1018通过、Play182运行中，双平台最终构建、Windows运行与发布仍待，最终以STATE为准。

## 以下为首批历史记录（已被全量范围覆盖）

# v0.71 首批素材路径表（交付草稿）

2026-11-08，只读盘点。输入是 [ART_SPEC.md §08 / A01—A18](ART_SPEC.md) 与随包母版；本表记录当前源文件、引擎接入和缺口，不把“文件存在”写成最终美术验收。首批只有 C48、C29、C03、I07 四张大插画；其他 67 项保留旧图标并待后续替换。没有扩大量产。

为便于阅读，下表路径前缀均相对仓库根目录：

- `S71` = `ArtSource/Art071/`；`R71` = `DicePrototype/Assets/Art/Art071/`。
- `S07` = `ArtSource/Art07/`；`R07` = `DicePrototype/Assets/Art/Art07/`。
- `Game` = `DicePrototype/Assets/Game/`。

| 编号 / 目标 | 实际源与引擎路径 | 本轮状态与适用边界 |
| --- | --- | --- |
| A01 毛毡纹理 | `S07/Backgrounds/bg_felt_tile.kra` → `R07/Backgrounds/bg_felt_tile.png`；`R07/Backgrounds/felt.mat` | 复用旧 512×512 纹理，Repeat/Bilinear；未制作规格建议的 1024 新纹理。 |
| A02 桌面光照与暗角 | `Game/Runtime/Shaders/Art07Felt.shader`、`R07/Backgrounds/felt.mat`；旧暗角源 `S07/Backgrounds/bg_vignette.kra` → `R07/Backgrounds/bg_vignette.png` | 复用 Felt 材质/真实主光阴影；旧暗角文件存在，不据此宣称新增遮罩已接入。本轮无独立暗角新件。 |
| A03 信息面板边框 | `tools/art_v071/build_surfaces.py` → `S71/UI/ui_info_inset.png`、`ui_paper_panel.png` → `R71/UI/` 同名文件；`Game/UI/UiKit.cs` 的 `Frame/PaperFrame` | 新 512×512 九宫格表面已导入。当前信息层 `Frame` 仍用代码薄边/底色，纸层使用新 paper 表面；`ui_info_inset` 不等于已被所有信息面板采用。 |
| A04 主次按钮 | 同脚本 → `S71/UI/ui_button_bevel.png` → `R71/UI/ui_button_bevel.png`；`Game/UI/UiKit.cs` 的 `Button/ActionButton/FlatButton` | 新 512×512 可着色九宫格，实际宽高由 UI 定义；侧边 `Step` 与主体 `Face` 分离。不是 512×128 成品按钮烘焙图。 |
| A05 页签与活动线 | `Game/UI/UiKit.cs` 的 `FlatButton`；`Game/UI/GameApp.Shop.cs` 的 `ActiveShopTab`；手册在 `Game/UI/GameApp.V05.cs` | 代码生成平面页签/分隔线/活动金线，无独立新 PNG。各页当前调用可查源码，未以文件数量计成一组新插画。 |
| A06 控制器空白卡框 | 同脚本 → `S71/UI/ui_card_controller.png`、`ui_card_controller_compact.png` → `R71/UI/` 同名；`Game/UI/GameApp.Art07Shop.cs` 的 `DrawControllerFullBody07` | 新九宫格可变尺寸纸框，插画、中文和状态分层。两个卡框与 paper panel 共用相同图像字节；不计作三张独立绘制图。源是 512×512 切片而非 768×1024 固定卡图，实际比例由容器指定。 |
| A07 卡背 | `Game/UI/GameApp.Art071Screens.cs` 的 `Menu071`：`MenuCardBack/PrintedBorder/Inset/BackSeal`；共用 A06 纸框 | 当前菜单以代码描边、菱形骰印组成卡背，非独立 768×1024 导出；没有伪称交付分层卡背图。 |
| A08 有也不交 / C48 | `S71/Illustrations/illustration_c48.png` → `R71/Illustrations/illustration_c48.png` | 首批新透明大插画，票据遮住骰子。内建 imagegen，原图字节保留；1254×1254 RGBA。 |
| A09 简单顺子 / C29 | `docs/specs/v071/handoff/assets/controller_stairs_candidate_v071.png` → `S71/Illustrations/illustration_c29.png` → `R71/Illustrations/illustration_c29.png` | 复用附件透明样件，逐字节一致，未重新绘制；1254×1254 RGBA。 |
| A10 首当其冲 / C03 | `S71/Illustrations/illustration_c03.png` → `R71/Illustrations/illustration_c03.png` | 首批新透明大插画，旗帜与领头骰子。内建 imagegen；1254×1254 RGBA。 |
| A11 道具 / I07 增加投掷 | `S71/Illustrations/illustration_i07.png` → `R71/Illustrations/illustration_i07.png` | 首批新透明骰杯插画。内建 imagegen；1254×1254 RGBA。主题取真实配置，不新增道具规则。 |
| A12 状态与附魔标记 | `S07/UI/Badges/badge_enchant_*.kra` → `R07/UI/Badges/badge_enchant_*.png`（base10、mult2、roll1、slotless、submit1）；状态符号在 `S07/UI/Symbols/` → `R07/UI/Symbols/`；`Game/UI/GameApp.V05.cs` 的 `EnchantBadge` | 复用五附魔与既有状态图标；本轮卡面以形、色、文字组合使用。未制作整套新标记。 |
| A13 骰子母模型 | `tools/art_v071/build_dice.py` → `S71/Dice/dice_master.blend`；`S71/Dice/face_map.json`、`export_preset.json`；`R71/Dice/dice_body_bevel.fbx` | 新 Blender 5.2.3 母版，空白骰体 300 三角/152 顶点，保留六面和原层级。源保存重开/FBX 回导记录见 `S71/Dice/Review/roundtrip.json`；引擎外观另验。 |
| A14 六面点阵与遮罩 | `Game/Runtime/DiceTable.cs` 的显示面数据循环与 `MakeFace`；`S71/Dice/face_map.json`；`R07/Dice/Materials/mat_dice_pips.mat` | 运行时按真实点数生成点阵，颜色独立；新浅点窝表现仍是动态网格覆盖层。无生成图点数参与规则。 |
| A15 陶瓷材质与灯光 | `R71/Dice/mat_dice_ceramic.mat`、`mat_dice_glass.mat`、`dice_contour.mat`；`Game/Runtime/Shaders/Art07Ceramic.shader`；`Game/Editor/V071Setup.cs`；`Game/Scenes/Bootstrap.unity`、`ArtStyleLab.unity` | 共享新材质/现有着色器更新及样板场景。当前接入参数由 V071Setup 与场景保存；Blender 离线预览仅作源检查，不替代 Unity。 |
| A16 接触阴影 | `Game/Editor/V071Setup.cs` 的主光软阴影参数；`Game/Runtime/Shaders/Art07Felt.shader` 的 `GetMainLight/TransformWorldToShadowCoord` | 复用真实投影并调整接触参数，不额外叠加 blob。旧 `R07/Dice/Textures/dice_contact_shadow.png` 存在，但不是本轮新增/启用的证据。 |
| A17 三类特效基础件 | `S07/FX/Textures/fx_star_spark.kra`、`fx_line_soft.kra`、`fx_ring_soft.kra` → `R07/FX/Textures/` 同名 PNG；`Game/UI/GameApp.Art07Effects.cs` | 复用旧光点/短线/软环及现有演出。短线不冒称新增短弧线；本轮未另制三张新 FX 贴图。 |
| A18 标题字标 | `Game/UI/GameApp.Art071Screens.cs` 的 `MenuWordmark/Title/RegistrationRule`；共用 A03 纸表面和项目中文 TMP 字体 | 已有代码排版的固定标题视觉，未导出独立矢量或高清标题图片。该资产形式差异保留在表中。 |

## 首批 4 项与其余 67 项

公共中文字体仍以 `Game/Fonts/DiceChinese.asset` 为主；v0.71 修复粗体省略号时新增 `Game/Fonts/DiceChineseBold.asset`，由主字体 weight7 引用。真实 Noto Sans SC Bold 2.002 源、许可、版权及 SHA 位于 `ArtSource/Art071/Fonts/`，生产导入副本为 `Game/Fonts/NotoSansSC-Bold.otf`。`Game/Editor/V071FontSetup.cs` 按现有48pt、padding7、2048²、SDFAA生成844字符的单张Static图集。原Regular 2.004图集与字表未变；新增字重不冒称与Regular完全相同源版本。两平台包保留两套字体的许可说明。

配置唯一来源为 [P0Config.json](../../../DicePrototype/Assets/Game/Data/P0Config.json)：50 控制器 + 7 道具 + 7 服务 + 7 骰子强化 = 71 项。C48/C29/C03/I07 的真实 ID、名称、提示词、参考路径、原图路径与 SHA 记录于 [首批 manifest](../../../ArtSource/Art071/Illustrations/manifest.json) 和 [prompts](../../../ArtSource/Art071/Illustrations/prompts.json)。图片内装饰骰点不作为玩法示例。

| 内容类别 | 本轮替换 | 仍用旧图标 / 待换 | 保留源与运行路径 |
| --- | --- | --- | --- |
| 控制器 50 | C48、C29、C03，共 3 | 47 | `S07/ContentIcons/Controllers/icon_c*.kra` → `R07/ContentIcons/Controllers/icon_c*.png` |
| 道具 7 | I07，共 1 | 6 | `S07/ContentIcons/Items/icon_i*.kra` → `R07/ContentIcons/Items/icon_i*.png` |
| 服务 7 | 0 | 7 | `S07/ContentIcons/Services/*.kra` → `R07/ContentIcons/Services/*.png` |
| 骰子强化 7 | 0 | 7 | `S07/ContentIcons/Upgrades/*.kra` → `R07/ContentIcons/Upgrades/*.png` |
| 合计 71 | **4** | **67** | 旧 71 张文件完整保留；运行绑定用四张新插画替换对应项。 |

实际接入入口是 [V071Setup.cs](../../../DicePrototype/Assets/Game/Editor/V071Setup.cs)：按新插画 ID 替换同 ID 旧 `contentIcons` 引用；[GameApp.Shop.cs](../../../DicePrototype/Assets/Game/UI/GameApp.Shop.cs) 优先选 `illustration_<id>`，再回退旧 `icon_<id>`。旧资源总表 [art07_resource_map.json](../../../DicePrototype/Assets/Game/Config/Art07/art07_resource_map.json) 保留 v0.7 身份，不把它改写成已生产 71 张 v0.71 插画的证明。

## 来源与当前证据

- 用户附件母版：[对局](handoff/references/battle_master_v071.png)、[商店](handoff/references/shop_derived_v071.png)、[随包任务](handoff/TASKS.md)、[提示词](handoff/PROMPTS.md)。完整输入要求仍以 ART_SPEC 为准。
- 新表面可编辑源是 `tools/art_v071/build_surfaces.py`，导出清单和 SHA 见 [UI manifest](../../../ArtSource/Art071/UI/manifest.json)。新素材原图、源模型和旧资源均保留。
- 当前实际录制：`cache/v071/recording-final/recording.json`，真实13.088857秒/122帧，RGBA无损MOV与明确丢弃720像素alpha的RGB MP4预览分别验证。报告绑定Runtime/Core MVID；此后仅Modals选骰文字两行配色修复，以 `final-special/shop-target-readable-720-150.png`、`shop-target-selected-720-150.png` 与前后完整状态文件补充，不重标旧录像身份。早期recording-01仍保留。
- 当前页面：`cache/v071/screens-final-1080`、`screens-720-150` 各48张；三商品、强化实时骰预览、选骰修复专项在 `final-special`。强化商品同一母版的纸框围绕透明3D窗口，保留真实当前永久骰，未预先伪造购买结果。
- 本表为实施路径草稿；四张大插画的作者没有签署其最终美术通过。素材形式差异、其余 67 项替换和全界面风格验收仍需按当前任务范围处理。
