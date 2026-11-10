# 主菜单 / 桌面接入：独立生产代码审查

2026-11-10，审查者 `/root/project_audit`。审查 root 本轮生产差异；未修改 Assets、场景、导入配置、测试标准，未调用 Unity。审查者是本轮新增测试候选作者，故不将其自编译结论当独立测试验收。本文件只签静态范围；实际1080/720/16:10结果尚待提供。审查期间root根据首轮实际失败修了菜单；下面明确区分初版预警与绑定时修后源码，不把修后状态归给旧截图。

**规格符合性：静态满足本轮表现层边界。工程质量：未发现需要在执行专项前阻断的确定代码错误；保留下列实机风险。没有签新画面、GPU或跨平台通过。**

## 已核对的实现

- `GameApp.Art071Screens.cs`仅重排Menu071：左侧原生字标/共享两张卡面与两卡背/三颗真实1、3、5点骰，右侧纸面菜单。五主入口名称及回调、无存档Continue禁用、旧版本存档分支、保存摘要和BuildIdentity来源全部保留。新建与取消、普通/调试存档逻辑没有移到装饰代码中；没有把Image2概念的示例数值烘焙成规则或改Core。
- Title/卡背装饰仍不接受raycast，新增PaperFrame默认raycast=false；真实按钮仍用原UiKit/事件，主次强调依hasSave决定。BuildVersion仍单独按钮，Version仍显示配置/美术/实际提交或Editor“开发中”。右侧普通按钮改为有厚度纸按钮是本次概念采用的外观选择，未改交互语义。
- `Art07Felt.shader`仍为同一Opaque URP世界Plane材质，真实骰子在它上方。新纹理不进入Overlay Canvas，因此不会按Canvas绘制顺序盖住全部世界骰子。`GetMainLight(TransformWorldToShadowCoord(v.world))`及原 `lerp(.7,1,shadowAttenuation)`保留，并放在新/旧背景分支之后；没有去掉实际阴影或增加虚假烘焙骰影。
- 新图通过独立`_TableMap`采样，不走旧_BaseMap的24×24 Repeat。fit的宽高比推导符合contain：宽画幅裁定左右为room，高画幅裁定上下为room；图不拉伸。真实素材1672×941略小于16:9，1080p两侧约0.51px余区，16:10/1920×1200上下约59.71px，和现UI1920×1080居中60px差不到1px。
- UV采用项目当前URP包`ShaderVariablesFunctions.hlsl`的`GetNormalizedScreenSpaceUV`，其中包含`TransformNormalizedScreenUV`和`_ScaleBiasRt`的平台RenderTexture方向处理，未另手写一套固定Y翻转。**这只证明API选择合理，实际D3D/RT方向仍由新GPU原图对照验证；Metal未因此通过。**
- `_Grain`只对启用新桌图后的颜色施加±0.6%微细变化，不再控制桌子主体是否存在。Reduced时既有`_Grain=0`/扫描线关及menu breath归零仍生效，桌面构图保留。旧材质`_UseTableMap=0`保留旧路径；暗room不增加Collider/输入层。
- `V071TableSetup`将新图设为Default+sRGB、Clamp、Bilinear、无mip、non-readable、无alpha、最大2048、CompressedHQ；当前真实.meta与这些字段一致，平台override=0，没有让Standalone旧默认块覆盖HQ设置。实际GPU压缩外观未静态代签。
- `BindMaterial`检查Texture及兼容_TableMap、写正式共享felt材质，并保存。`Apply`拒绝Play/脏场景；没有修改场景布局或游戏状态。Bootstrap和ArtStyleLab均通过相同GUID `cfb94f3758bca38438ce8842cc667d98` 引用该材质，新TableMap GUID与.meta一致。
- `V071FullSetup`与旧`V071Setup`导入循环均先识别新桌图并走专用导入，防止被Repeat或Sprite通配规则覆盖；全量接入末尾还重新BindMaterial，确保正常重跑保持新图。历史V071Setup不是本轮完整全量重建入口，未将它误当新批次的唯一配置来源。
- 素材record保留母版/v0.7桌面参考、原图/提示词/工具路径、原字节SHA与不透明PNG性质，标明概念数值不是运行数据。旧127清单被作为历史保留，本轮record是新增一件运行背景；没有重签历史gate/发行包。

## 必须由本轮运行图解决的风险

1. **菜单骰底与LockHint。** 初版中间骰下移至y924、scale2.05不变，LockHint在y1010–1052，本审查已向root预警。root随后报告真实图确认穿骰/边线，首轮失败保留于`runs/1080-initial`。绑定时源码已将扇卡上移30，骰中心改为880/894/882并缩至1.65，提示改y992/48高/字号16。这些是明确的生产修正，不是降低测试阈值；**修后实际外观尚未由本审查者看图确认关闭**。现有菜单glyph检查只管文字间重叠，仍需看文本与3D骰体。
2. **150%真实glyph。** 初版Subtitle52高及Version68高，root报告实际150%TMPoverflow；绑定时Subtitle已改74高（字号25不变）、Version96高（字号16不变）。静态方向对症，修后待重跑/实看。SavedSeed仍为20×1.5两行/80高，保留实际完整字形核验；不能仅用fontSize推定通过。
3. **构图细纹/room接缝。** 新图自身已含毛毡与光照；加0.6%颗粒与已有扫描线需实际720/1080看纹理是否影响骰点/文字，contain内外硬边界需16:10实看。没有因静态公式正确而关闭这些视觉项。
4. **完整接入证据。** root已告知Apply实际成功/无Shader错误，但本审查尚未读到绑定到这些文件SHA的运行原件；正式结论等当前专项报告/PNG/GPU JSON补充。旧41aface通过不能覆盖本次Shader/menu差异。

## 限域结论

生产变更保持UI框架、相机、骰子世界定位/面序、规则、存档与按钮命令，解决了此前审计中的Overlay遮骰、纹理平铺和Reduced隐藏桌图三个接入风险。可继续当前受影响专项；目前仍是待实机复核状态。既有性能/native显示失败、Mac和人工未验没有在本文件关闭。

逐源SHA与参考路径见同目录 `PRODUCTION_STATIC_REVIEW.bindings.json`。不会修改原验收条件让本轮通过。
