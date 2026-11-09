# v0.71 十二种公共表面源

状态：源文件与导出检查完成，等待独立复核、root 导入及真实 Unity 画面检查。只改新脚本与本目录；旧 43 件公共资源保持冻结。不能据此签署最终运行、美术或输入验收。

重跑：`python -B tools/art_v071/build_full_surfaces.py`。依赖 Python 3.11.4、Pillow 9.4.0、numpy 1.24.3；SVG 为可编辑几何源，PNG 是同一参数的三倍抗锯齿导出，未读取或放大旧模板。`manifest.json` 的 assets 为唯一导入清单，勿将预览作为运行纹理。

## 内容与导入

保留全部要求名称：ui_card_item、ui_card_upgrade、ui_chip_label、ui_focus_frame、ui_panel_modal、ui_panel_plain、ui_progress_fill、ui_progress_track、ui_scroll_handle、ui_slot_dice、ui_slot_inventory、ui_tooltip_frame。

- 一般表面为 512 × 512，PPU 200，九宫格 L/B/R/T = 32/32/32/32；相当于 Canvas reference PPU 100 下每边 16 UI 单位。采用 Bilinear、Clamp、无 mipmap、Uncompressed、FullRect、中心 pivot、sRGB、AlphaIsTransparency。
- 两张进度纹理为 768 × 96，PPU 200，L/B/R/T = 48/0/48/0。`ui_progress_fill` 的真实消费者是 Image.Filled/Horizontal；track 是 Image.Simple。二者必须 `preserveAspect=false`，由实际 RectTransform 决定长宽，fillAmount 继续由引擎。旧 Art07AssetLibrary.Picture 默认 preserveAspect=true，不应沿用到新的进度条。其他所有九宫格也使用 preserveAspect=false。
- 卡纸、芯片、槽位与普通面板为定色，Image.color 应为 Color.white。卡纸要保留 fillCenter=true。物品与强化卡复用相同纸张几何，类型信息由插画、标题与现有类别组件表达。
- focus 是纯白 RGB / alpha 轮廓，中心透明，保留 LocalFx 材质与 Graphic 运行时 Tint；不烘焙金色、蓝色或黑描边。fill 为白 RGB / alpha，track 和 scroll 为中性灰 RGB，保留原 Tint 语义。
- 骰子槽、库存槽中心全透明，无容量、序号、选中态烘焙，避免遮住真实骰子镜头。focus/槽位建议 fillCenter=false；池复用时须按当前资产恢复 fillCenter。源中心本身透明，但不能把 false 渗透到之后复用的纸面。
- 信息层没有动作按钮厚侧边；chip 是平纸签，普通面板为低对比深青薄框；卡牌/弹窗/提示框用短软阴影。没有增加交互区域、玩法数值或按钮状态。

## 消费依据与复核范围

实际读取 `Assets/Game/Editor/Art07AssetLibrary.cs` 的 Surface、Picture、InterfaceView、EffectView 与 `Assets/Game/Runtime/Shaders/Art07LocalFx.shader`。卡牌、骰槽、tooltip、modal、票据、概率和选中材质均有直接消费者；plain/scroll/inventory 是旧模板兼容名称，未虚构新的运行消费者。需要 root 统一旧 Prefab 引用重绑定。

`preview-deep-light-scale.png` 是实际 Pillow 九宫格模拟预览，涵盖深青桌面、浅纸、160×224/80×112 卡、320×200/160×64 面板、152×124/76×62 槽、280×64/160×32 纸签、26×160/12×64 滚动条及 320×24/160×16 进度条。槽下对象先绘制、框后合成；可见中心并非画在框上作假。预览不含动态文字，不证明实际 TMP、事件或运行布局通过。

作者实看：纸框与已批准卡纸家族一致；缩小图未见断边或槽中心遮挡；浅底上可辨细描边；普通信息框比卡牌/选中反馈轻。真实镜头的细线强度、Filled 比例、材质 tint 及池复用需 root 集成实看。

`source-export-verification.json`：12/12 请求名称、PNG 解码、SVG XML、SHA 一致；源脚本实际断言外缘 alpha=0、三种槽/框内区 alpha=0、两张 white-alpha 图所有非透明像素 RGB=255。重复完整生成后 26 个 PNG/SVG/manifest 文件逐字节一致。没有执行 Unity 或改生产 Assets。
