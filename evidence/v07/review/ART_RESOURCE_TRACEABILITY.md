# ART14 / ART22 资源证据索引

适用 art0.7.0 / p0.6.0 / schema4。资源证据绑定821ec59；4041766仅改变两个Player探针文件，资源、规则、产品UI及录制器不变，续用边界见 [821→404精确差异审查](MEDIA_REUSE_821EC59_TO_4041766_REVIEW.md)。这是已有产物和真实验证的索引，不是整版艺术/发布验收，也没有将inventory或用户manifest改为全verified。

| 可追溯内容 | 可编辑源 / 复现入口 | 已有实际证据与准确范围 |
|---|---|---|
| 133份二维源及导出 | `ArtSource/Art07/{ContentIcons,Backgrounds,UI,Dice,FX}` 的KRA/ORA；逐文件路径由 `samples_manifest.json`、`content_manifest.json`、`dice_texture_manifest.json`、`public_symbols_manifest.json` 给出。脚本入口见[源说明](../../../ArtSource/Art07/README.md)。 | [sample-validation-20261101-022802.json](../../../ArtSource/Art07/Review/sample-validation-20261101-022802.json)：826离线检查通过，当前133对源/导出SHA全部匹配。涵盖尺寸、KRA结构/合成图、透明度、留白/逻辑点阵及红蓝1–6点数；不是Unity实图检查。 |
| 71内容图标真实Krita重开 | `ContentIcons/**/*.kra`；`tools/art_v07/roundtrip_krita.py`，Krita5.3.4。 | [71项重开报告](../../../ArtSource/Art07/Review/krita-roundtrip-20261031-131254/report.json)的69项，加[C07/C42返工报告](../../../ArtSource/Art07/Review/icon-c07-c42-rework-20261031-142245/report.json)的2项，最新源/导出71对SHA匹配。真实CLI重开再导出，visible pixels/alpha一致；返工独立复核见[原报告对应节](INTERACTION_REWORK_REVIEW.md)。 |
| 其余62份公共二维源 | 相同KRA/ORA与脚本体系；[krita_conversion.json](../../../ArtSource/Art07/krita_conversion.json)。 | 已完成结构、图层/合成导出检查；**没有找到全部62项原生Krita重开再导出的完整记录**。不能把ZIP结构读取混称原生roundtrip，也不把已完成检查笼统写为“源文件全待验”。 |
| Blender主源、模型和动作 | [dice_master.blend](../../../ArtSource/Art07/Dice/dice_master.blend)；`ArtSource/Art07/Scripts/export_dice.py` → `tools/art_v07/build_dice.py`。 | [source-check-20261031-130251.json](../../../ArtSource/Art07/Dice/Review/source-check-20261031-130251.json)及同目录log：Blender5.2.3LTS真实重开，41检查通过，当前master SHA匹配。包括188三角主体、1单位、6面锚点、动作和6碎片；Blender预览不冒充Unity图。 |
| 导出与Unity导入预设 | [generation.json](../../../ArtSource/Art07/Dice/Review/generation.json)内FBX参数；`Scripts/generate_dice_pips.py` → `tools/art_v07/build_dice_textures.py`；运行资源在 `DicePrototype/Assets/Art/Art07`，保留`.meta`。 | [library-build-20261101-045656.json](../../../ArtSource/Art07/Review/library-build-20261101-045656.json)记录实际Unity6000.6.7f1库生成；52个资源包括26prefab、7材质、10clip、3atlas。存在/生成本身不等于实机质量。历史source-only字段保持原样，以后续运行证据补充，不追改旧报告。 |
| 3个实际SpriteAtlas | `UI/Atlases/atlas_ui_pixel.spriteatlasv2`、`atlas_content_icons_pixel.spriteatlasv2`；`FX/Atlases/atlas_fx_soft.spriteatlasv2`。原计划路径与真实V2路径见[path_mappings.json](../../../ArtSource/Art07/path_mappings.json)。 | 当前meta：2048、padding4、rotation/tight关闭、alphaDilation开启、不可读/无mip、sRGB；两pixel为Point，soft为Bilinear，无压缩。配置rawSHA在本索引bindings；TMP字体atlas测试没有被当成SpriteAtlas证据。 |
| 26prefab真实导入/加载与复用 | `Game/Tests/PlayMode/Art07AssetLibraryPlayModeTests.cs`；Bootstrap实际序列化库引用。 | [821完整Play报告](../unity/nunit-20261101-105539.xml)中该文件7项全部Passed，逐名见bindings；覆盖缺脚本/有效网格/字体/支持材质、full→compact池复位、L5→L0、连线/暂停、精确数字、骰面/玻璃共享材质复位。独立版本绑定见[FULL_REGRESSION_821EC59_REVIEW](FULL_REGRESSION_821EC59_REVIEW.md)，不借此替代像素边缘实图。 |
| ART14正式持久化GPU图片 | [art14-821ec59-1103目录](../media/art14-821ec59-1103/)；[persisted-manifest.json](../media/art14-821ec59-1103/persisted-manifest.json)、[capture-report.json](../media/art14-821ec59-1103/capture-report.json)。 | 43张原始1920×1080、567实例、144透明PNG；59项持久化文件逐SHA一致，含源/PDB/偏好/清单/临时代码绑定。独立[九宫格/角标000–015复核](ART14_SLICES_REVIEW.md)与[纹理016–042复核](ART14_TEXTURES_REVIEW.md)合并覆盖全部43图。原821身份/MVID/日期不重标404。 |
| 来源、许可和Git追踪 | [ArtSource说明](../../../ArtSource/Art07/README.md)记录原创图形/几何；[Audio recipe](../../../ArtSource/Art07/Audio/recipe.json)记录原创合成配方；字体[来源](../../../DicePrototype/Assets/Game/Fonts/SOURCE.json)及[SIL OFL1.1全文](../../../DicePrototype/Assets/Game/Fonts/LICENSE.txt)。 | 本次已核402个源/导出/meta/主blend/字体来源许可路径全部tracked；Noto字体及LICENSE当前SHA符合SOURCE.json。402是明确抽查集合，不冒称发行ZIP已验。工具安装缓存不属于游戏资产。 |

ART14图片对应白`#FFFFFF`、深青`#102A2D`、红`#EC655D`；九宫格最小/正常/2×宽、全卡160×224、紧凑卡208×104、角标28/32/40均按原规格。133份源图之外，点数atlas另有12张确定性分面PNG；正式manifest stable-ID映射及逐实例坐标保存在持久化`bindings/target-plan.json`、`assets.tsv`、`samples.tsv`。计数证明覆盖，视觉结论来自两个独立实际看图报告。

保留边界：三个线性数据mask只作诊断；柔光原生同心色阶Q-ART14-FX01仍为明确制作观察，不从边缘通过推到最终柔光平滑度。AudioMixer缺口、声音人工验收、整体美术/玩家体验人工签署、Mac实机均未由本索引解除。复现入口和历史实测已给出，但本次没有重跑全体资产生成、Unity或Blender，也不预填404新回归/Player结果。

完整逐源绑定已在 `artifacts/cache/v07-art14-resource-check/existing-evidence-bindings.json`；本索引小型bindings保存这些报告的rawSHA、最新821的7项测试名称、Atlas配置与持久化核对结果，便于最终交付追查。
