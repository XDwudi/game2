# art0.7.1 骰子母版交接

状态：源制作和 Blender 重开/FBX 回导通过；Unity 集成、实机视觉与人工风格验收待 root 完成。输入为 `docs/specs/v071/ART_SPEC.md` §07 和随包对局母版；保持 p0.6.0/schema4 的六面与运行时数据所有权。

## 实际产物

- `dice_master.blend`：可编辑的 1m 圆角陶瓷母模型；`DiceRoot → RollPivot → FaceOrientation → DiceBody` 和 `Face_F01..F06` 保持现有层级。
- `face_map.json`：原六面法线、对面、动态点阵位置与集中参数。没有把某个初始点数或红蓝颜色固化进导出骰体。
- `export_preset.json`：沿用 FBX -Z forward/Y up、米制、Mesh/Empty、不导入碰撞和灯光的路线。
- `Review/source_ceramic.png`：真实 Blender Cycles 离线预览，不能称为 Unity 截图。
- `Review/generation.json`、`Review/roundtrip.json`、`build.log`、`verify.log`：原生制作、重开、导出回导记录。
- 待集成模型：`cache/v071/dice/Export/dice_body_bevel.fbx`。
- 待集成文本候选：`cache/v071/dice/Candidates/DiceTable.cs`、`Art07Ceramic.shader` 及各自 unified patch。没有直接改生产 Assets。

## 复现

```powershell
& 'E:/blender/blender.exe' --background --factory-startup --python tools/art_v071/build_dice.py
& 'E:/blender/blender.exe' --background --factory-startup --python tools/art_v071/verify_dice.py
```

实际环境 Blender 5.2.3 LTS / e13b728c6880。骰体 300 三角、152 顶点、尺寸 1×1×1，闭合流形。26 项源与 FBX 回导检查通过，包括与 v07 权威映射逐面比较、六个标记位置、六组点数准确数量、导出仅含空白骰体及原层级。保存时 Blender 系统缩略图缓存有非致命写入提示；主源文件重开、正式 PNG 写出与 FBX 回导均另行实际成功，不以进程退出码独自判定通过。

## 表现参数与边界

母模型从规格推荐的倒角起点向参考图的圆润轮廓调整为 0.10/4 段，几何预算保持现有 1200tri 上限内。Blender 暖白 F4E8CB、非金属、roughness .32。Unity 候选用现有同名 Ceramic shader，连续漫反射与宽高光取代旧三阶明暗；`_Smoothness=.62`、`_Specular=.30` 是引擎调节起点，不把 roughness 直接映射成 smoothness。

运行时点阵仍由 `MakeFace` 按真实面数据生成并缓存。半径 .075，边缘与灰阶内壁形成浅点窝视觉，红蓝仍由现有 MaterialPropertyBlock 乘色，高光和金边独立。此实现是覆盖层的光学点窝，不是固定钻孔；这样才能保留所有可变面值。无新的随机抽样、状态写入、动画或存档逻辑。

源文件中 `Review_ONLY_not_exported` 内有示例点数、桌面、相机与光，只为源预览使用。FBX 实际回导确认只有一个空白 DiceBody 网格，所有例子不进入游戏资源。

## root 引擎接入建议

1. 保持当前模型路径/Guid 或将新 FBX 导入既有加载链，`globalScale=1`、`useFileScale=true`、`bakeAxisConversion=true`、不添加 Collider；继续原有动画。
2. 合并两份候选 patch 后编译，确保两个新增 shader float 写入陶瓷材质。保留玻璃 stencil/opacity 和背面点数剔除逻辑。
3. Felt 已支持真实主光阴影。继续这一条路线，不叠加 blob。可从主光 `shadowStrength=.7`、`shadowBias=.015`、`shadowNormalBias=.035`、Soft 开始，在实际桌布背景上调节；轮廓可由 2.4px 降至 1.0px 以免普通陶瓷像贴纸。
4. 验证六面朝向、任意点数/红蓝替换、普通/黄金单面/玻璃、投掷终点和锁定。现有 `DiceArt07PlayModeTests` 玻璃测试中的白顶点 z=.001 精确断言需要改为有灰阶顶点且全部正深度的非空检查；保留后续真实 GPU 透光/不透光测试。
5. 用同一游戏相机实看主菜单、骰池、锁定与详情预览。离线图和本报告都不能替代引擎与人工外观结论。

未修改运行时动作；Normal/Fast 时长、排序、锁定和 RNG 保持现有实现。运行时浅点窝具体高光和接触阴影仍需实机校准。
