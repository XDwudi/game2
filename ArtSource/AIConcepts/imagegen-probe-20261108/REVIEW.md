# I07 内置 imagegen 接通试验

2026-11-08，AI-ART-PIPELINE / ARTPIPE-05。状态：概念候选；生成调用与文件归档完成，未集成、未获艺术认可。

调用：当前会话内置 `image_gen.imagegen`，两张已实际打开的本地参考图，`transparent_background: true`；完整原始提示词见 [prompt.txt](prompt.txt)。工具实际返回图片及其原始保存路径，原字节复制到 [illustration_i07_concept.png](illustration_i07_concept.png)，来源和SHA见 [manifest.json](manifest.json)。

能力结论：成功生成并保存一张 1254×1254 RGBA 图，有真实 alpha；本次没有 API key/API调用，没有得到可核验的底层模型快照、seed或请求ID。官方将内置能力标为 gpt-image-2，具体身份边界见技能调用指南。

## 实施者观察

- 已实际查看输出：骰杯、单颗骰子、青色动作线、暖纸色和墨色轮廓延续现有I07；没有卡框、中文、价格或倍率，构图接近原参考。这是保留样式的局部变体试验，不是新的创意方向或生产替换。
- 文件可解码，原图与归档逐字节相同。1023178像素全透明、547286半透明、2052不透明；半透明中525575像素alpha在240—254。不能仅因“半透明多”判断整图不可用，也不能仅因有alpha判断边缘干净。
- 非零alpha包围盒为 `[0,17,1234,1222]`，而alpha≥16包围盒为 `[158,163,1116,1126]`，说明外围存在低alpha像素；此项是待视觉检查的事实，不通过新阈值自动删除像素。原生产I07也存在类似低alpha外围，本任务未改两者。
- 尚未在纸色/深桌面上完整检查边缘、实际卡面缩放、Unity裁切/压缩和运行效果；不推荐未经这些检查直接替换游戏资源。

实际完整性报告：[imagegen-probe-integrity.json](../../../evidence/ai-art-pipeline/imagegen-probe-integrity.json)。它只证明文件与绑定，不能作为风格统一、原生分层源或生产放行证据。此图是扁平栅格，不声称KRA/PSD图层。

独立复核见 `evidence/ai-art-pipeline/review/`。后续如采纳该候选，沿当前素材任务执行必要修整、集成与视觉复验；本任务保留现有生产I07不变。
