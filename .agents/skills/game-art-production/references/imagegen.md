# 内置 Image2 概念与插画调用

核对日期：2026-11-08。通用规则以本会话实际提供的 `imagegen` 技能及工具 schema 为准。这里保存项目用法，不复制整套上游技能或锁死用户机器绝对路径。

## 已验证的入口

当前会话有 `image_gen.imagegen`，通过 `functions.exec` 的 `tools.image_gen__imagegen` 调用。OpenAI 官方 [Codex 图像说明](https://learn.chatgpt.com/docs/image-generation) 将内置能力描述为 `gpt-image-2`；当前工具不暴露 `model` 参数，返回值也不提供可核验的底层模型快照。因此记录“内置 imagegen；官方文档标为 gpt-image-2；实际快照不可见”，不能杜撰 model ID/seed/request ID。

内置路径不需要 `OPENAI_API_KEY`。项目技能只保存调用方法，不把云端模型权重安装进仓库，也不保证每个未来会话都拥有同一工具。

本任务实际例子：`ArtSource/AIConcepts/imagegen-probe-20261108/`。它是 I07 的独立概念候选，未替换生产图。

## 可直接改用的调用

先用 `view_image` 查看所有本地输入图，并给每张图标明角色。把下面示例绝对路径替换为当前仓库根目录；`prompt` 必须读取或使用当前任务完整提示词。此段是工具调用示例，不能当普通 Node/Python 脚本运行。

```javascript
// @exec: {"yield_time_ms": 120000, "max_output_tokens": 1000}
const result = await tools.image_gen__imagegen({
  prompt: "输入1为风格母版，输入2为主体/画法参考。为既有ID制作一张独立插画……明确只改什么、保留什么……",
  referenced_image_paths: ["E:/path/to/game2/docs/specs/v071/handoff/references/battle_master_v071.png", "E:/path/to/game2/ArtSource/Art071/Illustrations/illustration_i07.png"],
  transparent_background: true
});
generatedImage(result);
```

只有真实接口提供相应参数时才使用上述字段。纯新图不带两种引用参数；本地输入均有路径时使用 `referenced_image_paths`。会话附件没有路径时按当前工具要求使用最小 `num_last_images_to_include`；两者不同时传。等待按实际工具返回的运行 ID 和说明进行，不重发请求冒充轮询。

不传未暴露的 model、seed、size、quality、output path 或 input_fidelity。明确请求透明时设 `transparent_background: true`，保存原始 alpha。不要用色键自动抠掉奶油白主体或把棋盘格当透明。

## 项目提示词与保存

复用 ART_SPEC §10，按单一资产裁剪：

1. 用途/ID：概念、独立插画或局部修订；真实主体来自配置和任务。
2. 输入角色：母版只约束风格；已选插画约束描边/色块；目标图是要编辑的文件。实机图只约束功能位置。
3. 保留：参考图的材质、配色、轮廓粗细、构图安全边、主光方向；变量仅本轮主体或一个缺陷。
4. 输出：单件无卡框/文字/数值/状态层；透明背景；不要多物件拼版作为可直接导入资源。

固定提示词和同一参考能减少漂移，不能保证像素级复现。围绕选中原稿做小步编辑，避免只把上一次变体当下一次唯一风格锚点导致累积漂移。

生成后从工具返回的真实保存路径复制原字节到任务目录，保留原文件；没有返回路径时先查工具提供的产物获取方式，不伪造路径。仅展示图片不算项目归档完成。记录完整提示词、参考 SHA、输出 SHA/尺寸/alpha、修订父文件和审查状态；不要记录或索要密钥。外部默认生成路径可以作为来源说明，项目可用文件必须在仓库内。

## API / CLI 是单独的可选路线

用户明确选择 API/CLI 后才按内置 imagegen 技能的 fallback 文档运行其现成 `scripts/image_gen.py`，需要本机环境变量 `OPENAI_API_KEY` 和 API 账户权限；不要另造一次性 SDK runner。内置失败不自动转付费 API，也不自动降到其他模型。

官方 [gpt-image-2 模型页](https://developers.openai.com/api/docs/models/gpt-image-2) 提供生成/编辑与快照信息。API 的具体参数要核对请求的准确模型，不能把新型号示例套到 image2。当前本地 imagegen fallback 说明明确：`gpt-image-2` CLI 不支持 `background=transparent`，高保真输入不传 `input_fidelity`。这与内置工具暴露透明请求是不同接口；不要推导 API 也可传同名参数。本轮未调用 API、未测试 CLI、未验证账户额度。

参考：本机内置 `imagegen/SKILL.md`（在当前技能目录发现并读取），[官方生成指南](https://developers.openai.com/api/docs/guides/image-generation)。未来工具或模型变化时重查；透明失败应记录并在已授权路线修整/重试，不伪称成功。
