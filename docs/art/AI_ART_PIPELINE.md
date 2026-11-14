# 美术资源标准化生产流程

版本 pipeline-2.1，2026-11-14（独立复核后修订）。本版取代 2026-11-08 的入口文档，旧版把 v0.71 当作现行风格，已过时。“长什么样”看 [风格总纲](STYLE_GUIDE.md)，本文只讲“怎么做、怎么查、谁签”。

当前实现与验收状态以 [STATE](../STATE.md) 为准。

## 0 核心思路

所有资源走同一条链：**立单 → 参考包 → 制作 → 自检 → 实机 → 独立复核 → 人工节点 → 归档**。

风格统一靠四件事：同一组锚点、同一段固定提示词、每次只换主体、和锚点并排对照。多抽图、换模型或堆形容词都不能保证统一。

- 质量证据是实机画面和审查单，不是生成数量。
- 一种新类型的资源，先完整做好一个代表件，再扩大批次。

## 1 流程总览

| 步骤 | 做什么 | 产出 | 通过条件 | 谁 |
| --- | --- | --- | --- | --- |
| S0 定位 | 读 STATE、风格总纲、当前任务；运行 `artgen verify-kit` | `KIT_VERIFIED` | 锚点与样板图的 SHA 全部一致，否则停下查原因 | 实施者 |
| S1 立单 | 每个资源一个 `job.json`，含 ID、类别、主体描述；批次用 [AI_ART_JOB](../../templates/AI_ART_JOB.md) 记录范围与验收 | job.json、批次记录 | 主体取自 P0Config，按总纲 §4.4 的写法；路线按本文 §2 选定 | 实施者 |
| S2 参考包 | 工具自动带上 1 号样板图；需要时加主体参考或编辑目标，并写明角色 | prompt.txt、codex-request.txt | 所有参考存在且记录 SHA（工具强制） | 工具 |
| S3 制作 | GPT 生成用 `artgen generate`；脚本或 Blender 按各自工具执行 | `rNN/original.png` 与 record.json | 运行结果为 VALID：调用核对全部通过，原图逐字节归档 | 实施者 |
| S4 自检 | 看审查单，按 §5 逐项检查；每个 LOOK 指标的原因写进批次记录（AI_ART_JOB 表格） | 结论：选用 / 局部修 / 重做 / 换方法 | 没有未解释的 LOOK，没有 A 类与 B 类缺陷 | 实施者 |
| S5 实机 | 接入工程，在真实页面截图：1080p 与 720p 下的商店卡、详情、紧凑槽，与相邻同类并排 | 实机截图 | 无裁切、无白边，小尺寸可读，与邻卡不跳 | 实施者 |
| S6 独立复核 | 另一个 agent 或人，只读；分别判断规格符合性、制作质量和实际运行 | 复核结论 | 通过、有缺陷、未完成或有理由的不适用 | 复核者 |
| S7 人工节点 | 只在四种情况请用户决定：锚点或 kit 变更、新类型资源的黄金样例、批次开工前的首个代表件、转入量产 | 用户决定记录 | 有来源和日期 | 用户 |
| S8 归档 | 选中的修订复制到生产源目录和运行目录，更新清单与 STATE | manifest、STATE | 源、导出、运行件、证据的哈希一一对应 | 实施者 |

已确认方向内的常规制作不逐张找用户审批；S7 之外的重复劳动由 AI 和独立复核完成。

## 2 按资源类别选路线

| 类别 | 路线 | 源文件 | 交付 | 禁止 |
| --- | --- | --- | --- | --- |
| 内容插画（C/I/O/U） | GPT 图像生成，经 `artgen` | original.png、record.json、prompt.txt | 1254² RGBA 透明 → `illustration_<id>.png` | 把上一张输出当风格参考；在图里放文字或数值 |
| 机制或功能图标 | 脚本矢量（沿 `tools/art_v073/build_art.py` 画法） | .py 参数源 | 96 或 128 px PNG | 用 GPT 直接出成品（线宽和尺寸要精确） |
| 界面表面件与九宫格 | 脚本矢量 | .py | 512² 九宫格，PPU 200 | 生成图截图冒充九宫格 |
| 骰子与材质 | Blender 母模、Unity 着色器 | .blend、build_dice.py、shader | FBX 与材质 | 用生成图冒充游戏内骰子 |
| 特效贴图 | 脚本矢量，白色绘制、代码上色 | .py | 小尺寸 PNG | 烘焙颜色或文字 |
| 页面改版或新方向的概念图 | GPT 衍生：1 号为母版或锚点，2 号为实机截图，只约束功能位置 | 概念原图与记录 | 仅供讨论，放 `ArtSource/AIConcepts/` | 切图当素材；直接导入工程 |

## 3 让风格保持统一的七条规则

1. **锚点冻结。** 锚点和样板图的 SHA 记录在 `ArtSource/StyleKit/style_kit.json` 中。每次生成前，工具先核对；不一致就拒绝生成。
2. **样板图每次都传，并且事后核对。** 2026-11-14 的试验中，Codex 代理把附上的参考图静默丢掉，只传了文字提示（见[校准试验](../../ArtSource/AIConcepts/artkit-probe-20261114/README.md)）；同日 v0.9 批次中也有两张出现同样情况。
   所以工具会读 Codex 会话日志，以图像工具自己的生成记录为准，核对模型实际收到的参考图（按 SHA-256）、实际使用的提示词（逐字）和透明背景。任一项不符就记为 INVALID_RUN，图不得使用。
3. **固定段不改，只换主体。** 画法、色彩角色、构图、禁止项由 kit 统一拼接；job 里只写主体。
4. **永远以锚点为参考，不以上一张输出为参考。** 链式参考会累积漂移。编辑模式（`edit`）只用来修同一张图的具体缺陷。
5. **先一个再一批。** 每个批次先让一个代表件完整走到 S5，再做 4–6 张一批。同一批次使用同一 kitVersion。
6. **并排对照。** 每张候选都和 8 个锚点同尺寸并排，再在实机里和相邻卡并排。每个 LOOK 指标都要在批次记录里写明原因：接受，还是修改。record.json 是工具生成的证据，不手工改写。
7. **风格变更走版本。** 改锚点、改固定段或色彩角色，需要用户决定，然后升级 kitVersion，并列出已交付资源中需要复查的部分。升级前，把旧 kit 的原字节复制到 `ArtSource/StyleKit/history/style_kit_<版本>.json`，让旧记录仍能核对。

## 4 怎么调用 GPT 图像生成

### 4.1 前提（2026-11-14 已实测）

- 本机安装了 Codex 桌面版，且已用 ChatGPT 登录。CLI 位于 `%LOCALAPPDATA%\OpenAI\Codex\bin\<版本目录>\codex.exe`，工具会自动找到；也可以设置环境变量 `ARTGEN_CODEX`。
- 检查方法：`codex features list` 里 `image_generation` 为 true，`codex login status` 显示已登录。
- 需要 Python 3.10+、Pillow 和 numpy。本机为 `E:\anaconda3\python.exe`。
- 不需要 `OPENAI_API_KEY`，用的是 ChatGPT 账户的 Codex 额度。

### 4.2 命令

在仓库根目录运行：

```powershell
python tools/art_gen/artgen.py verify-kit
python tools/art_gen/artgen.py prompt   --job ArtSource/AIConcepts/<任务>/<ID>/job.json
python tools/art_gen/artgen.py generate --job ArtSource/AIConcepts/<任务>/<ID>/job.json
python tools/art_gen/artgen.py sheet    --image <某张png>
python tools/art_gen/artgen.py recheck  --record <rNN/record.json>
```

- `verify-kit`：核对锚点、样板图和漂移指标所用的 71 张参考集。
- `prompt`：只打印将要发送的完整请求，不生成。
- `generate`：生成一次，归档为下一个 `rNN`。
- `sheet`：给任意 png 补做审查单。
- `recheck`：用当前的核对规则重新检查一次已归档的运行，复核者可以直接用它。

`job.json` 的写法：

```json
{
  "jobId": "V09-ART-C54",
  "category": "illustration",
  "contentId": "C54",
  "subject": "只写物件、动作、关系，见风格总纲 §4.4",
  "extraReferences": [{"path": "仓库相对路径", "role": "SUBJECT REFERENCE: 只参考某物的形状"}],
  "edit": {"target": ".../r01/original.png", "change": "只改一处，例如把硬币缩小一半"}
}
```

`extraReferences` 和 `edit` 都可以省略。

### 4.3 工具会检查什么、记录什么

每次 `generate` 都在 `rNN/` 下留下：

- `prompt.txt`：发给图像工具的提示词；
- `codex-request.txt`：发给 Codex 代理的完整请求；
- `original.png`：工具输出的逐字节副本；
- `record.json`；
- `review_sheet.png` 与 `review_metrics.json`；
- Codex 的事件流与错误输出。

record.json 记录以下内容：

- kitVersion、样板图和每张参考图的 SHA；
- P0Config 中的名称、说明和 SHA；
- Codex 版本、线程 ID、会话日志路径；
- 调用核对结果：
  - 图像调用与生成记录各一次；
  - 调用中的参考路径，以及模型实际收到的图片 SHA-256，与锁定的参考逐一相符；
  - 模型实际使用的提示词，以及调用中以字面量传入的提示词，都与组合出的提示词逐字一致；两者至少要有一个能核对，否则判无效；
  - 已请求透明背景；
- token 用量，以及调用前后的周额度百分比；
- 尺寸与 alpha 统计，以及运行结论 VALID 或 INVALID_RUN。

工具不记录实际图像模型快照，因为工具不暴露它。OpenAI 文档称内置工具为 gpt-image-2，record 照此注明，不杜撰版本。

### 4.4 额度与失败处理

- **额度。** 实测一次生成约 80 秒，周额度从 77% 升到 78%（百分比只到整数，仅一次观测）。批量开工前先看剩余额度；50 张以上的批次，按 §6 的建议考虑 API 路线。
- **INVALID_RUN。** 原图和日志都保留，不删除、不覆盖，但不得进入审查或使用。先读 `runProblems` 和错误输出再决定，不盲目重跑。
- **同一缺陷最多重抽两次。** 之后改用编辑模式、重写主体描述或换路线，并在 record 旁写明原因。
- 用户 Codex 配置中有一个本地 MCP 服务（127.0.0.1:8000）未启动，会在错误输出里打印连接错误。这不影响生成。如需安静，可加 `--codex-arg=--ignore-user-config`（必须用等号形式；是否仍能生成未实测）。

在 Codex 桌面里交互式调用 `image_gen.imagegen` 仍然可行（见技能的 [imagegen.md](../../.agents/skills/game-art-production/references/imagegen.md)），但必须手工补齐同样的记录字段，并确认调用里确实带了 `referenced_image_paths`。

## 5 审查清单（S4 自检与 S6 复核共用）

每张候选逐项判断。发现缺陷就做一次有针对性的修改，不用抽更多图来碰运气。

| 编号 | 检查 | 怎么看 |
| --- | --- | --- |
| A1 | 没有文字、数字、卡框或界面元素 | 原图大图 |
| A2 | 真透明；纸色底和毛毡底上都没有白边、灰底或棋盘格 | 审查单第一行两块底 |
| A3 | 主体完整、不贴边，宽度约为画布的 70%–95% | 看审查单大图；指标 subjectWidthShare 只作参考 |
| A4 | 在 64 px 下仍能认出主体 | 审查单的显示尺寸行 |
| A5 | 单张插画，不是多个变体的拼版 | 原图 |
| B1 | 描边粗细、平涂和网点与锚点一致 | 审查单并排行 |
| B2 | 色彩角色：象牙与石油青为主，红、蓝、金只作点缀 | 审查单指标与 LOOK 标记 |
| B3 | 三分之四俯视，光从左上，阴影面为硬边 | 并排行 |
| B4 | 骰子是圆角陶瓷、墨色骰点（机制要求时除外） | 原图 |
| B5 | 没有渐变、发光、写实、像素或做旧 | 原图 |
| C1 | 能联想到 P0Config 里的效果 | record 中的 configDescription |
| C2 | 不充当规则示意：没有精确牌型，没有数字 | 原图 |
| C3 | 不与已有主体撞车；原创，不模仿现成游戏 | 全量总览图 |
| D1 | 商店卡、详情、紧凑槽三处可读 | 实机截图（S5） |
| D2 | 与相邻卡并排时不跳出 | 实机截图（S5） |

结论只有四种：

- **选用**；
- **局部修**：用编辑模式，只改一处；
- **重做**：重写主体描述后重新生成；
- **换方法**：比如改为脚本路线，或请用户决定。

A 类和 B 类缺陷不能带进 S5。审查单的数值指标只用来定位问题，最终判断看图。

## 6 能自动检查的和不能的

| 层 | 自动检查 | 能证明 / 不能证明 |
| --- | --- | --- |
| 风格基准 | `artgen verify-kit` | 锚点和样板图没被改动；不证明锚点好看或已获认可 |
| 生成调用 | `artgen generate` / `recheck` 读会话日志核对 | 模型确实收到锁定的参考图、逐字提示词与透明请求；不证明结果好 |
| 漂移指标 | 审查单的色彩角色、主体占比、浅色毛边 | 定位与 71 张已交付插画的偏离（超出范围 0.03 以上记 LOOK）；不是通过门槛，不替代看图 |
| 文件完整性 | [art_asset_audit.py](../../tools/ART_ASSET_AUDIT.md) | SHA、尺寸、alpha、运行副本；不证明导入设置或视觉 |
| 引擎集成 | 现有 Unity 测试与截图巡览 | 引用和显示正确；静态图不证明动态效果，Windows 不代表 Mac 或移动端 |
| 风格与可读性 | 无，需要人或独立 agent 看审查单和实机截图 | 自动相似度或 AI 自评不能签署美术认可 |

## 7 文件与命名

- **候选与概念：** `ArtSource/AIConcepts/<任务>/<ID>/job.json`，每次运行生成 `rNN/`。不覆盖，失败的运行也保留。
- **生产源：** `ArtSource/Art<版本>/IllustrationsFull/<ID>/`，放选中的原图、提示词与 record（沿用 v0.71 的约定）。
- **运行件：** `DicePrototype/Assets/Art/Art071/Illustrations/illustration_<小写id>.png`。新版本若另建目录，以对应 ASSET_MAP 为准。
- **风格基准：** `ArtSource/StyleKit/style_kit.json` 与 `plates/`。只能按 §3 第 7 条修改。

## 8 角色与签署

- **AI 实施者**负责 S0–S5 与 S8，写真实记录，不能把自己的产物标为 ACCEPTED。
- **独立复核者**（另一个 agent 或人，只读）负责 S6，不得为了让实现通过而修改验收标准。
- **用户**负责锚点与 kit 变更、新类型资源的黄金样例、批次首件、风格方向和转入量产。

## 9 工具现状与建议

| 工具 | 状态 | 用途 |
| --- | --- | --- |
| Codex CLI 内置图像生成（ChatGPT 登录） | **已接通**，2026-11-14 实测两次 | 主路线：插画、概念图、局部编辑 |
| Python、Pillow、numpy | 已有 | artgen、矢量素材脚本 |
| Blender 5.2 | 已有（`E:\blender`） | 骰子母模 |
| OpenAI API Key（gpt-image 系列） | 未配置 | 可选：大批量时按张付费、可脚本化参数与多候选；不占 ChatGPT 周额度。需用户开通并设置 `OPENAI_API_KEY`。参数需按当时官方文档逐项核对，透明背景等能力不能从内置工具推定 |
| Krita + Krita AI Diffusion | 未安装；本机 RTX 3080 10GB 满足本地运行 | 可选：频繁局部修图（重画一只手、一个边角）时使用；锚点获认可、积累足够成品后，可再评估风格 LoRA |
| ComfyUI、图生 3D、Blender MCP | 未接入 | 当前不需要。理由见 [工具研究](../research/AI_ART_TOOLS_RESEARCH.md) |

## 10 已知局限（2026-11-14）

- 标准流程目前只生成过一张校准图，每种做法一个样本，还不能证明它在批量中稳定地更一致。
- 样板图把 8 个锚点缩到约 336 px 的格子里，网点和线重的传达可能偏弱。“单张样板图”与“直接传 2–3 张全分辨率锚点”哪种更稳，尚未做对比试验。
- 漂移指标是按最近色归类的粗略统计，只用来定位，阈值没有经过校准。
  - 已交付插画里红、蓝、深色、象牙的占比本身跨度很大，这几项指标很少触发；真正有约束力的是金色和浅色毛边。
  - 主体宽度在已交付插画中最高达 98%，所以 A3 的 95% 上限靠看图判断，指标管不到。
- 锚点未获用户认可；独立复核只覆盖工具与文档，未签署任何美术结论。

研究来历与许可见 [工具研究](../research/AI_ART_TOOLS_RESEARCH.md) 和 [项目审计](../research/AI_ART_PROJECT_AUDIT.md)。2026-11-08 的旧入口所记录的 imagegen 首次接通、I07 隔离概念等历史事实，仍保存在 [AI-ART-PIPELINE 任务](../tasks/AI-ART-PIPELINE.md) 中。
