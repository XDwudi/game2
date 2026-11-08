# v0.71 首批主题插画

输入是 `docs/specs/v071/ART_SPEC.md`、同包对局母版与商店衍生图。真实 ID 从 `DicePrototype/Assets/Game/Data/P0Config.json` 解析。未修改规则、数据或生产 Assets。

| 资源 | 内容 | 文件 | 产生方式 |
| --- | --- | --- | --- |
| A08 | C48 有也不交 | illustration_c48.png | image_gen 内建工具，票据护骰 |
| A09 | C29 简单顺子 | illustration_c29.png | 随包透明样件原字节复制 |
| A10 | C03 首当其冲 | illustration_c03.png | image_gen 内建工具，领头红旗 |
| A11 | I07 增加投掷 | illustration_i07.png | image_gen 内建工具，骰杯与掷出骰子 |

四张均为 1254×1254 RGBA，真实透明背景，无卡名、价格、效果数值或卡框。`validation.json` 记录实际尺寸、透明度、可见边界和 SHA；`manifest.json` 记录来源与建议导入参数；`prompts.json` 是三次实际完整提示词。原始 PNG 是可编辑栅格源，未声称有独立绘画图层或矢量源。

已目视检查三张新图：主体轮廓完整、与样件的粗墨线和限色网点一致，外围保留裁切余量。C48 为纸票遮住骰子，C03 为红旗引领骰列，I07 为倾斜骰杯掷出骰子。生成图内骰点仅为装饰，不作为精确牌型图；规则点阵仍由引擎创建。

建议 Sprite/Full Rect、Bilinear、Clamp、sRGB、alphaIsTransparency；所有原图保持字节不变。组件可按可见边界调插画窗口，但不要把完整卡框与文字烘焙进去。Unity 组件实际缩放与透明边缘表现、跨页面一致性及人工风格验收由集成阶段完成；此处状态为素材已准备，非整版 ACCEPTED。
