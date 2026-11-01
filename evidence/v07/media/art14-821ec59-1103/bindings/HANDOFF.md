# ART14 临时 GPU 补证与 ART22 既有证据交接

输入：`821ec5992c34b59eddfe2a0ae984fbaca48f41f8`，art0.7.0 / p0.6.0 / schema4。只读生产文件；全部新增文件在本 cache。未操作 Unity、Blender、Player、场景或 Git index；未改制作/验收标准。执行人为 root，最终图审需另一代理或人工；本代理是资源作者，不能独立签自己的制作质量。

## GPU 执行范围

原文 `docs/specs/v07/ART_SPEC.md:853,857,859`：透明边缘三底色；九宫格最小、正常、2倍宽；全卡160×224、紧凑卡208×104；角标32/28/40设计像素。

- 运行目录实际146 PNG：144有透明像素，`bg_felt_tile`、`bg_print_grain` 两张全不透明，不参加透明边缘检查。没有将库存数量当通过。
- 15九宫格×3底×3宽=135；5角标×3底×3指定尺寸=45。
- 同一个临时Canvas补原生尺寸：71内容图标、28公共符号、5角标原图、17骰纹理、7FX、1vignette，129×3=387。
- 合计 **43 PNG / 567实际绘制实例 / 144唯一透明PNG**。15模板一页一个，角标指定尺寸一页，另外27原生尺寸联系表；1024图每底单页。所有坐标/页号/实例ID在 `target-plan.json` 与 `samples.tsv`，无文字标签、无TMP。
- 背景：白`#FFFFFF` = (255,255,255)；规格theme.bgDeep `#102A2D` = (16,42,45)；theme.red `#EC655D` = (236,101,93)。不把纸色当白色。
- 卡框正常尺寸使用规定值；其余模板正常尺寸为原始PNG尺寸。最小宽是实际导入border.left+right，PPU/referencePPU均100；这是本次最小切片伸缩样本的明确解释，并非新增玩家最小点击区标准。
- 3张线性数据蒙版 `dice_gold_edge_mask/dice_rough_mask/dice_smooth_mask` 标记diagnosticOnly；其RawImage图不代替材质用途验收。未对普通贴图强制改变导入/色彩空间。
- `manifest-correspondence.json` 逐PNG对应权威manifest；12张单独红/蓝1–6点图为 `dice_pips_red_blue` 的确定性分面导出，来源 `tools/art_v07/build_dice_textures.py` 与源入口 `ArtSource/Art07/Scripts/generate_dice_pips.py`。其余134张路径精确匹配权威stable ID。

## 运行前置与最短调用

使用既有 `artifacts/cache/record-v07-screens.ps1:32–41` 的隔离启动段，不调用整个六屏录制器。root先保存现有源/PDB/identity、编辑器场景path/dirty、偏好、普通存档字节快照。起点必须为无GameApp的未命名干净空场景，不能打开Bootstrap后才设置路径。

1. 在空场景把GameView设为1920×1080（该既有驱动窗口position为1,31,1920,1101）；进入Play并等状态稳定。
2. 只在空场景Play中设置 `GameApp.SaveDirectoryOverride` 到全新、位于cache且与输出目录分离的隔离保存目录；设置 `GameApp.SuppressStartupDisplay=true`；再 `SceneManager.LoadSceneAsync("Bootstrap", Single)`。因此Bootstrap.Awake不会访问普通Run保存路径。
3. 等 `app.StartupReady07 && app.Session != null && app.Session.State == null && !app.IsBusy`；实际Screen.width/height必须1920/1080。不得在全Play测试、其它Recorder或导入期间执行。
4. 用现有 `Invoke-Unity` 包装器一次执行本cache的 `capture.csx`，只替换 `__OUTPUT__` 为新的兄弟cache目录（不能预建该输出目录）。例如：

```powershell
$body = [IO.File]::ReadAllText('E:\agent_workspace\gpt\game2\artifacts\cache\v07-art14-resource-check\capture.csx')
$body = $body.Replace('__OUTPUT__','E:\agent_workspace\gpt\game2\artifacts\cache\v07-art14-gpu-NEWBATCH')
Invoke-Unity $body
```

`Invoke-Unity`沿现有形式调用 `unity.exe command eval --code $body --project-path E:\agent_workspace\gpt\game2\DicePrototype --caller plugin --skill unity-cli --format json`；解析顶层及data.result.success。脚本拒绝既有输出、普通保存目录、任何已有Run、未Ready/Busy/pending、非1920尺寸、其它Recorder、清单或资产meta SHA偏差。

5. 读取新输出 `capture-report.json` 直到status不再running，超时建议120秒；预计约130实际帧加43张PNG编码，数秒到一分钟。它记录source identity SHA、Runtime/Core MVID、设备/API、每张PNG SHA、每个实例实际矩形/mesh/shader/alpha/cull、Sprite packed状态、原图与meta SHA以及只读Session/保存检查。
6. 正常终态只能是 `captured_pending_independent_visual_review`，不是ART14通过。finally先停用再Destroy整个临时Canvas；再等一帧确认对象不存在。root退出Play，核原空场景path/dirty、全物理源快照、普通保存文件、完整偏好前后一致。脚本不写任何偏好，内部只额外观察既有6个UI整数键及FontScale；**外层完整偏好/场景守卫不能由这个局部观察代替**。原先静态override/StartupDisplay值由root在空场景返回后恢复。
7. 失败保留capture-report/已生成PNG与异常，不宣布全覆盖；不要自动重做资产/改meta/打包Atlas以使检查通过。报告packed=false应如实解释，不能由一个Atlas名称推定实际已打包。

纯临时表现对象：无GraphicRaycaster、全部raycastTarget=false；不触发按钮/规则/RNG/存档、不调用UiKit、无TMP字形、无AssetDatabase写/Refresh/Save/Pack、无场景保存。只AssetDatabase.Load已有Sprite/Texture。没有创建材质或改共享资产。

离线编译：`CompileOnly.csproj` 对当前Unity6000.6.7f1实际程序集成功，0警告0错误，见 `compile.log`。这仅证明C# API/类型兼容，不冒充Unity执行。最初GetMesh重载预编译失败已在本次缓存准备中修正为实际API `GetMesh()`，没有运行失败资产版本。

## 图审交付

独立审查者按每页三底、每实例指定矩形放大原像素观察：意外白/黑晕、切片接缝、杂alpha、角标缩小轮廓；保留像素插画有意墨色轮廓，不能将其自动判成黑晕。直接使用capture-report的实例ID/坐标绑定原PNG；允许派生ROI但不能把ROI处理结果冒充GPU原图。原生贴图边缘、卡框伸缩、角标三个尺寸分别下结论。现有字体/交互/动画/平台测试不重复扩大。

## ART22 已有证据（不是统一待验）

|实际已完成范围|现存证据|本次绑定与边界|
|---|---|---|
|133可编辑二维源与导出，826离线检查|`ArtSource/Art07/Review/sample-validation-20261101-022802.json`；`tools/art_v07/validate_art_samples.py`|本次133/133源SHA与导出SHA均等于当前文件。检查尺寸、KRA结构/合成图、留白/点阵、红蓝1–6连通数等；报告明确unityVerified=false。|
|71图标真实Krita重开导出|`ArtSource/Art07/Review/krita-roundtrip-20261031-131254/report.json`、`full-krita-roundtrip.log`；后续C07/C42用`Review/icon-c07-c42-rework-20261031-142245/report.json`、`source-promotion.json`|69原记录+2后续记录的源/导出71对SHA均匹配。Krita5.3.4，真实重开导出的visible pixels相同。`krita_conversion.json`的133个ZIP结构重开不等于133个Krita原生完整roundtrip，二者未混淆。|
|C07/C42后续独立制作复核|`evidence/v07/review/INTERACTION_REWORK_REVIEW.md` C07/C42节；此前`ICON_CONTACT_SHEET_REVIEW.md`|旧失败不删除，返工后源6/7层、Krita导出/当前PNG绑定已独立记录。人类风格签署仍未替代。|
|Blender真实重开、模型/锚点/动画|`ArtSource/Art07/Dice/Review/source-check-20261031-130251.json`、`source-check.log`、`generation.json`|Blender5.2.3LTS，41检查；骰体188三角、1单位、6物理面锚点、4动作、6碎片。当前master.blend SHA与报告匹配。Blender预览不是Unity实机外观结论。|
|可复现导出及Unity实际导入|`tools/art_v07/build_dice.py`、`build_dice_textures.py`；`ArtSource/Art07/Scripts/export_dice.py`、`generate_dice_pips.py`；`Dice/Review/generation.json`；运行FBX/PNG的`.meta`；`Review/library-build-20261101-045656.json`|源脚本/导出预设/实际Import meta存在；库构建是52实际资源/26prefab/7material/10clip/3atlas，不能单独推运行通过。|
|26prefab加载、字体材质/池复位真实测试|`DicePrototype/Assets/Game/Tests/PlayMode/Art07AssetLibraryPlayModeTests.cs`；`evidence/v07/unity/nunit-20261101-095947.xml`；`evidence/v07/review/FULL_REGRESSION_0F04E51_REVIEW.md`|该文件7项均在0f04完整Play中Passed：26真实prefab、full/compact、L5/L0、连线池、暂停/恢复、精确数字、骰面/玻璃共享材质复位。准确逐名结果在bindings；本次没有运行测试，不借此宣称全部像素边缘已验。|
|3实际SpriteAtlas配置|`Assets/Art/Art07/UI/Atlases/atlas_ui_pixel.spriteatlasv2.meta`、`atlas_content_icons_pixel.spriteatlasv2.meta`、`Assets/Art/Art07/FX/Atlases/atlas_fx_soft.spriteatlasv2.meta`（均在DicePrototype内）|实际meta为2048、padding4、不旋转、不tight、alphaDilation开、不可读、无mip、sRGB；两pixel Point/soft Bilinear、无压缩。TMP fallback atlas测试不作为SpriteAtlas边缘证据。新GPU记录真实packed属性，未强制重新打包。|
|许可、来源和Git文件|`ArtSource/Art07/README.md`、`Audio/recipe.json`；`DicePrototype/Assets/Game/Fonts/LICENSE.txt`、`SOURCE.json`；`ArtSource/Art07/path_mappings.json`|原创图形/几何/合成声音配方与Noto CJK/OFL1.1来源记录存在；本次检查402个源/导出/meta/主blend/字体来源许可文件全部tracked。这个范围不是新发行ZIP完整性或Mac验证。|

本次绑定详见 `existing-evidence-bindings.json`：133对源导出、71最新Krita记录、master SHA、7真实测试名称/结果、3Atlas当前meta、证据rawSHA与402路径tracked。历史inventory不改verified。

实际看过的旧原图：`Review/icon-c07-c42-rework-20261031-142245/after-three-backgrounds.png`、`Previews/public_symbols.png`、`Previews/four_controller_samples.png`（均在ArtSource/Art07内）。前者实际是felt/paper/dark，不能覆盖本次white/bgDeep/red；后三底/九宫格矩阵此前没有证据，因此新43页是真实补缺，非重复制作。

剩余边界：本次临时GPU尚未执行/独立图审；62非内容公共二维源没有找到全量Krita原生重开导出证据，但其KRA结构/合成导出检查已完成，不应笼统写“全部源未验”。最终Git发行内容及双平台实际外观、AudioMixer、声音人工、人工美术体验与Mac实机仍按主DELIVERY记录，不能由ART22库存证明通过。
