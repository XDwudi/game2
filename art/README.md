# P0 骰子与符号资产

这些资产由确定性的几何、SVG/Pillow 与音频合成脚本制作。没有外部图片、模型、音效样本或生成式点数图。适用于 v0.4 原型；美术风格与游戏内可读性仍由实际构建及用户评审决定。

## 编辑和复现

- Blender：5.2.3 LTS，构建 `e13b728c6880`。源文件 `art/blender/Dice_P0.blend`。
- 骰体：`E:/blender/blender.exe --background --factory-startup --python art/scripts/build_dice.py`。
- 重开及端点验证：同上替换为 `art/scripts/verify_dice.py`。
- 六视图：同上替换为 `art/scripts/render_dice_views.py`，使用 Cycles CPU。预览临时添加标准点数，未写回无点数骰体源，不是游戏运行截图。
- 符号/图集/音效：`python art/scripts/build_symbols.py`；使用 Pillow 9.4.0。数字使用 Windows 自带 Segoe UI Bold 栅格化；未分发字体文件，SVG保留可编辑文本。
- 字节级检查：`python art/scripts/validate_assets.py`，实际读取PNG alpha连通域核对1–6点，检查38图标尺寸/透明及10 WAV格式/非静音/无削波。

## Unity 交接

- `Assets/Game/Art/Models/DiceBody.fbx`：1 unit、几何中心原点、300三角、152顶点、无骨骼。层级 `DiceRoot → RollPivot → FaceOrientation → DiceBody`，6个 `Face_F01..F06` Empty 位于物理面中心。
- 源文件还保留 `FaceMarkers_ReferenceOnly` 六面标记 Mesh，隐藏且不导出渲染；导出使用命名 Empty 与 `face_map.json`。
- `Roll_A.fbx`、`Roll_B.fbx`：独立动作文件，同层级；60fps，帧1–43，时长0.70秒。首末帧 RollPivot 位置/旋转均0、scale1。Unity衔接/落定各0.10秒及最终面朝向由表现适配完成；不能把动画碰撞作为规则来源。
- FBX `Forward=-Z, Up=Y`，`use_space_transform=true`，`bake_space_transform=false`，单位米；仅Mesh/Empty、逐帧烘焙、简化0。实际导出参数已从本机RNA检查，见 `evidence/art/fbx_export_preset.json`。
- `face_map.json` 明确Unity的6个法线及对面关系，不依赖三角形顺序。根模型几何对称，但面标记的最终导入坐标仍必须由Unity端核验；源脚本定义Unity=(Blender X, Blender Z, -Blender Y)。
- 普通/不透明风格玻璃材质分别保留在BLEND；Unity需用URP材质重建，不依赖Blender节点导入。
- 用共享Mesh和单骰属性/六面片呈现。当前显示点数、规则颜色、纹理、镀层与玻璃分别读取状态；遥控仅覆盖抽中面当前点数。

## 纹理和符号

- 38个图标在 `Assets/Game/Art/Icons/`，256×256 RGBA，4倍采样抗锯齿；源SVG在 `art/source_icons/`。
- `Assets/Game/Art/asset_map.json` 是内容ID到图标路径的映射；价格/规则仍来自主配置，资产不另存规则。
- `PipsAtlas.png` 是1024×1024，4×4格、每格256；从左上数，红1–6对应index0–5，蓝1–6对应index8–13。Unity底左UV已记录到索引，每格(width,height)=(.25,.25)。未使用格透明。
- `Pip_red_1.png` 等12个单独纹理可直接制作面片。每个点数由确切坐标列表生成，1–6点数经过计数断言。
- 金色边框、光滑反光、粗糙颗粒、玻璃轮廓在 `Art/Textures/Layer_*.png`，不替代红蓝点数。图标颜色只是提示，游戏UI仍需文字/状态形状。
- 3张共用透明卡框 `Frame_Controller/Item/Upgrade.png` 可用于UI。

## 音效

10条短WAV均为44.1kHz、16bit、mono：roll、land、lock、cancel、use、purchase、reject、copy、dye、break。由固定种子与包络合成，不使用第三方录音。音量/静音、触发顺序与是否重复播放由Unity运行适配处理；音频生成成功不等于游戏触发已验证。

## 检查边界

实际完成：本机FBX导出器RNA探测、源制作/保存/重开、骰体预算/尺寸、六标记、两动作首末变换、38图标互不相同、12点数组合计数、PNG透明尺寸、10WAV生成。证据在 `evidence/art/`。

尚需集成验收：Unity模型坐标/法线、动画导入、最终面朝上与UI一致性、URP材质、图标和叠层游戏镜头可读性、音效实际触发、三分辨率与压力场景。离线图不能替代这些检查。
