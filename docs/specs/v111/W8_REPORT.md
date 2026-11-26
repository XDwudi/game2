# W8 美术制作与交接记录

## 1. 输入与范围

- 工作包 W8，规则 p1.1.1，分支 `codex/v1.1.1`，以用户确认的 D20 与 `P0Config.json` 的 `shortText` 为输入。交付目标为 C154、C155、C156、I13 在 default、swiss、sancai、comic、deco、glass 各一张插画和一个图标。D14 的 C19、C91、C125、C137 沿用既有插画。 [工作包](briefs/W8_ART.md) · [平衡性方案](BALANCE_PLAN.md) · [规则增量](RULES_DELTA.md)
- Codex 负责主体表、源图和生成记录、暂存图标、自检与入库清单。W8 写入范围为 `ArtSource/`、`artifacts/cache/v111/art/`、`tools/art_v11/` 下本批次新增清单与本报告。Claude 协调者持锁办理 Unity 入库、旧资源清理、逐图复核、实机验证及 `docs/STATE.md` 更新。主题风格由用户审定。 [任务契约](../../tasks/P0-V111.md)
- 代表件为 C155 的 default 版本。先核对生成记录、原图、缩略图和图标，再按 C155 → C154 → C156 → I13 的优先级扩展。每次生成前跑默认 `verify-kit`，非默认主题另核对该主题 kit。六套既有 kit 校验均返回 `KIT_VERIFIED`，默认版本是 `style-kit-1.1`，五套主题版本均为各自的 `0.2`。 [制作流程](../../art/AI_ART_PIPELINE.md)

## 2. 验收条件

| ID | 方法与场景 | 期望与证据 | 责任人 |
| --- | --- | --- | --- |
| W8-01 | 对照 p1.1.1 的四项 `shortText`、W22a 与风格总纲 §4.4 | 四条英文主体句与功能提示齐全，最多 37 词，历史三条保留，在既有字段登记移除。证据为主体表与本报告第 3 节 | 实施者自检，协调者复核 |
| W8-02 | 核对六套冻结 kit。每次生成后读生成记录并运行 `artgen recheck` | 生图调用与生成记录各一次，传入参考图与记录 SHA 一致，请求真透明，提示词逐字可核验，原图与提示词归档哈希匹配。证据为每次运行的 `record.json`、`prompt.txt`、`codex-events.jsonl` | 实施者自检，协调者复核 |
| W8-03 | 代表件先行。对照各主题锚点与同主题邻卡，在纸色和深色底上看大图与 64 px，逐张执行流程 §5 的 A1–A5、B1–B5、C1–C3 | 画面主体、功能提示和风格相符，选用稿无 A/B 类缺陷，每个 LOOK 有说明。证据为逐图结论、审查单和邻卡对照 | 实施者自检，协调者独立审图，用户定风格 |
| W8-04 | 复用 W13a 与 W22i 的裁切或缩放路径，在紧凑尺寸下查看图标 | 每张选用插画对应同主题的 160×160 RGBA 图标，32 px 下主要识别特征完整。证据为图标、裁切记录及并排图 | 实施者自检，协调者复核 |
| W8-05 | 核对获选源图、图标的安装映射及旧资源移除清单 | 每个源文件有确切目标，每项旧资源有确切移除路径，后续安装可按清单核验。证据为本报告的入库段和相关清单 | 实施者列清单，协调者持 Unity 写锁执行 |
| W8-06 | 入库后在 1920×1080、1280×720 查看商店卡、详情和紧凑槽，与同主题邻卡核对，执行流程 §5 的 D1、D2 | 各尺寸可读、边缘干净、与邻卡一致。证据为目标构建、配置与实机截图 | 协调者验证，用户定风格 |

## 3. 主体句与历史登记

| ID | 主体与功能提示 |
| --- | --- |
| C154 回心转意 | 一只手将一颗骰子从锁定托盘移向骰池。开着的挂锁留在空托盘上。骰子归池的动作把本卡与 I13 的钥匙串区分开。 |
| C155 状元插金花 | 宽翼状元帽的帽冠上插两朵金花。浅碗里的骰子分别呈现单颗红点和方形红点组合。按 §4.4 省去精确计分手牌与固定六骰数量。红点和金色花用于识别博饼奖项，计分按有效点数判定。 |
| C156 绝地反击 | 骰子从岩石突缘向上反弹。用实体测量管取代进度条，管内液面低于中点刻口，保留「不到一半」的关系。 |
| I13 钥匙串 | 正好三把钥匙在一个钥匙圈上扇开，旁边是一把开着的挂锁。英文主体用 `three` 指定物件数量，三次使用由独立可数的钥匙轮廓表达。 |

四条主体分别为 23、34、21、13 词，只写物件、动作、关系及有功能的颜色。C90、C128、C145 在 `subjects-1` 既有 `notes` 字段追加 V1.1.1 移除说明，保留原主体、原图路径和 SHA。当前 schema 的移除记录采用文本说明。 [主体表](../../../ArtSource/Art11/subjects.json) · [主体句规范](../../art/STYLE_GUIDE.md#44-主体描述的写法) · [V1.1 主体表做法](../v11/W22A_SUBJECTS.md)

## 4. 制作入口与环境

Windows 上使用 Python 3.11.4、Pillow 9.4.0、NumPy 1.24.3、SciPy 1.10.1。V1.1 原生路径由 `artgen generate` 通过本机 Codex CLI 调用内置 `image_gen.imagegen`。当前沙箱尝试沿用该路径时，Codex CLI 0.171.0-alpha.13.2 在嵌套进程报 `Error finding codex home: Could not find home directory` 并以 1 退出。代表件首次运行记录为 `INVALID_RUN`，`artgen` 记录的生图调用数和生成记录数均为零。 [失败记录](../../../ArtSource/Art11/default/illustrations/C155/r01/record.json) · [原始错误输出](../../../ArtSource/Art11/default/illustrations/C155/r01/codex-stderr.txt)

恢复时沿用 V1.1 的内置图像生成入口，在当前 Codex 会话发出调用。每次传入 `artgen prompt` 组合的原文、按顺序列出的锁定参考图路径和透明请求。默认主题用既有风格板，五套重绘用各自风格板和同卡已自检的默认图，后一张只约束物件、构图及功能性色彩。归档从本机会话提取的原始调用与生成事件，并由仓库现有的 `artgen` 核对实际输入 SHA、提示词、透明参数和原图，保留各次修订。已实测代表件归档可用 `artgen recheck` 重验。图像生成工具和项目脚本沿用仓库现有版本。 [交互调用流程](../../../.agents/skills/game-art-production/references/imagegen.md) · [代表件生成记录](../../../ArtSource/Art11/default/illustrations/C155/r02/record.json)

## 5. 交付与代表件

**REVIEW**。24 张插画和 24 个 160×160 RGBA 紧凑图标均已完成离线自检。开工 HEAD 为 `b35f6ea3551726fe57a642fba8e7d1c67b98b4ef`。所有生成记录绑定的 `P0Config.json` SHA-256 为 `5ba97c8158062e355796d888dbdb581710351cdff15cd6b3e3fbf49b394b777b`。W8-01 至 W8-05 离线通过。W8-06 的 D1、D2 由协调者入库后验证。

C155 default 为先做的代表件。r01 在嵌套 CLI 启动时失败。r02 改由当前会话调用同一内置工具，提示词及实际收到的风格板均通过核对。原图 1254×1254 RGBA，主体宽 94.58%，金色占 5.24%。纸色与深毡上边缘干净，64 px 可辨双金花、帽翼和骰碗。画中出现单颗红点及方形红点群，五颗可见骰体现点色和文化主题。图标裁框保留构图，置信度 0.6679，原因是裁框接近全图且墨线细节较多。32 px 无 mip 预览仍能和 C24 区分，红点群宜在 720p 紧凑槽复看。代表件通过 A1–C3 自检和离线独立复核后才扩展。 [审查单](../../../ArtSource/Art11/default/illustrations/C155/r02/review_sheet.png) · [邻卡对照](../../../artifacts/cache/v111/art/comparisons/default/C155-r02.png)

## 6. 逐图自检

每张选用稿通过 A1–A5、B1–B5、C1–C3。检查覆盖版面、透明、主体留边、64 px 辨识、单图、主题描画及色彩、体积、骰点、禁用效果、功能提示、规则抽象和邻卡辨识。五套重绘的 B 项对照实际使用的本主题锚点。每张图的提示词与实际输入参考都可用记录重验。 [全套总览](../../../artifacts/cache/v111/art/comparisons/overview.png) · [逐次复核](../../../artifacts/cache/v111/art/verification.json)

| 内容 | 主题 | 选用 | 原图尺寸 | 周已用前后 | LOOK | 通过说明 | 同主题邻卡 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| C155 | default | [r02](../../../ArtSource/Art11/default/illustrations/C155/r02/record.json) | 1254×1254 | 90% → 90% | — | 双金花和两类红骰点清楚。与同为宽翼帽的 C24 用青帽、双花、圆骰碗区分 | [对照](../../../artifacts/cache/v111/art/comparisons/default/C155-r02.png) |
| C155 | swiss | [r02](../../../ArtSource/Art11/swiss/illustrations/C155/r02/record.json) | 1254×1254 | 91% → 91% | 深色、其他色、透明占比、主体宽、主体高 | 上方雾边清除，帽翼完整。钴蓝动作线沿用同主题邻卡惯例 | [对照](../../../artifacts/cache/v111/art/comparisons/swiss/C155-r02.png) |
| C155 | sancai | [r01](../../../ArtSource/Art11/sancai/illustrations/C155/r01/record.json) | 1254×1254 | 90% → 90% | 其他色、透明占比、主体宽、主体高、浅缘 | 琥珀双花、铁红骰点清楚，釉面高光在轮廓内 | [对照](../../../artifacts/cache/v111/art/comparisons/sancai/C155-r01.png) |
| C155 | comic | [r01](../../../ArtSource/Art11/comic/illustrations/C155/r01/record.json) | 1254×1254 | 90% → 90% | 深色、透明占比、主体宽、主体高 | 粗墨线和阴影网点与邻卡相符，红点与帽碗轮廓清楚 | [对照](../../../artifacts/cache/v111/art/comparisons/comic/C155-r01.png) |
| C155 | deco | [r01](../../../ArtSource/Art11/deco/illustrations/C155/r01/record.json) | 1254×1254 | 90% → 90% | 深色、金、其他色、透明占比、主体宽、主体高、浅缘 | 两金花、深青珐琅及红点清楚，亮金集中在花、包边和压纹 | [对照](../../../artifacts/cache/v111/art/comparisons/deco/C155-r01.png) |
| C155 | glass | [r01](../../../ArtSource/Art11/glass/illustrations/C155/r01/record.json) | 1254×1254 | 90% → 90% | 其他色、透明占比、主体宽、主体高 | 铅线分开帽与骰碗，琥珀双花与红宝石骰点清楚 | [对照](../../../artifacts/cache/v111/art/comparisons/glass/C155-r01.png) |
| C154 | default | [r01](../../../ArtSource/Art11/default/illustrations/C154/r01/record.json) | 1254×1254 | 91% → 91% | — | 打开的锁留在空托盘，手持单骰，青色动作线指向骰池 | [对照](../../../artifacts/cache/v111/art/comparisons/default/C154-r01.png) |
| C154 | swiss | [r01](../../../ArtSource/Art11/swiss/illustrations/C154/r01/record.json) | 1326×1187 | 91% → 91% | 深色、其他色、透明占比、主体宽、主体高 | 开锁与回池方向明确，补边裁框保留完整横构图 | [对照](../../../artifacts/cache/v111/art/comparisons/swiss/C154-r01.png) |
| C154 | sancai | [r01](../../../ArtSource/Art11/sancai/illustrations/C154/r01/record.json) | 1254×1254 | 91% → 91% | 其他色、透明占比、主体宽、主体高、浅缘 | 陶釉表现一致，覆写裁框后完整显示托盘、取骰和回池 | [对照](../../../artifacts/cache/v111/art/comparisons/sancai/C154-r01.png) |
| C154 | comic | [r01](../../../ArtSource/Art11/comic/illustrations/C154/r01/record.json) | 1254×1254 | 91% → 91% | 深色、象牙、其他色、透明占比、主体宽、主体高 | 黑色中性点、黄色动作线与打开的锁口清楚 | [对照](../../../artifacts/cache/v111/art/comparisons/comic/C154-r01.png) |
| C154 | deco | [r01](../../../ArtSource/Art11/deco/illustrations/C154/r01/record.json) | 1254×1254 | 91% → 91% | 深色、金、透明占比、主体宽、主体高、浅缘 | 金边托盘、开锁及象牙骰分层清楚，扩大裁框保留托盘边 | [对照](../../../artifacts/cache/v111/art/comparisons/deco/C154-r01.png) |
| C154 | glass | [r01](../../../ArtSource/Art11/glass/illustrations/C154/r01/record.json) | 1254×1254 | 91% → 91% | 深色、透明占比、主体宽、主体高 | 蓝色动作线指向骰池，开锁和托盘边完整，骰点为墨色 | [对照](../../../artifacts/cache/v111/art/comparisons/glass/C154-r01.png) |
| C156 | default | [r01](../../../ArtSource/Art11/default/illustrations/C156/r01/record.json) | 1254×1254 | 91% → 91% | — | 青色弧线连接碎岩与上弹骰，液面低于中央长刻口 | [对照](../../../artifacts/cache/v111/art/comparisons/default/C156-r01.png) |
| C156 | swiss | [r01](../../../ArtSource/Art11/swiss/illustrations/C156/r01/record.json) | 1254×1254 | 91% → 91% | 深色、其他色、透明占比、主体宽、主体高 | 实体量管中液面在下段，32 px 可辨低水位轮廓 | [对照](../../../artifacts/cache/v111/art/comparisons/swiss/C156-r01.png) |
| C156 | sancai | [r01](../../../ArtSource/Art11/sancai/illustrations/C156/r01/record.json) | 1254×1254 | 91% → 91% | 红、其他色、透明占比、主体宽、主体高、浅缘 | 釉岩、上弹弧线和低液面清楚，图标保留量管底座 | [对照](../../../artifacts/cache/v111/art/comparisons/sancai/C156-r01.png) |
| C156 | comic | [r02](../../../ArtSource/Art11/comic/illustrations/C156/r02/record.json) | 1254×1254 | 91% → 91% | 深色、象牙、透明占比、主体宽、主体高 | 内侧青线改为平涂尖尾，保留回弹方向和量管低液面 | [对照](../../../artifacts/cache/v111/art/comparisons/comic/C156-r02.png) |
| C156 | deco | [r01](../../../ArtSource/Art11/deco/illustrations/C156/r01/record.json) | 1254×1254 | 91% → 91% | 深色、象牙、金、透明占比、主体宽、主体高、浅缘 | 骰子沿金青弧线向上，水位在中央刻口下，量管完整 | [对照](../../../artifacts/cache/v111/art/comparisons/deco/C156-r01.png) |
| C156 | glass | [r01](../../../ArtSource/Art11/glass/illustrations/C156/r01/record.json) | 1254×1254 | 91% → 91% | 深色、透明占比、主体宽、主体高 | 铅条分清液面和圆柱，反弹方向及低液面关系清楚 | [对照](../../../artifacts/cache/v111/art/comparisons/glass/C156-r01.png) |
| I13 | default | [r01](../../../ArtSource/Art11/default/illustrations/I13/r01/record.json) | 1254×1254 | 91% → 91% | — | 三个匙柄与开锁均可辨，32 px 优先读钥匙和锁梁 | [对照](../../../artifacts/cache/v111/art/comparisons/default/I13-r01.png) |
| I13 | swiss | [r01](../../../ArtSource/Art11/swiss/illustrations/I13/r01/record.json) | 1254×1254 | 91% → 91% | 深色、其他色、透明占比、主体宽、主体高 | 三个匙杆可数、锁梁脱开，扩大裁框后右下硬影保留 | [对照](../../../artifacts/cache/v111/art/comparisons/swiss/I13-r01.png) |
| I13 | sancai | [r01](../../../ArtSource/Art11/sancai/illustrations/I13/r01/record.json) | 1312×1199 | 91% → 91% | 其他色、透明占比、主体宽、主体高、浅缘 | 中间钥匙穿环点部分被挡，三把匙柄和匙齿可辨，补边保留外匙尖 | [对照](../../../artifacts/cache/v111/art/comparisons/sancai/I13-r01.png) |
| I13 | comic | [r01](../../../ArtSource/Art11/comic/illustrations/I13/r01/record.json) | 1254×1254 | 91% → 91% | 深色、象牙、透明占比、主体宽、主体高 | 三把钥匙头形与匙齿各自分清，锁梁有间隙 | [对照](../../../artifacts/cache/v111/art/comparisons/comic/I13-r01.png) |
| I13 | deco | [r01](../../../ArtSource/Art11/deco/illustrations/I13/r01/record.json) | 1254×1254 | 91% → 91% | 深色、金、其他色、透明占比、主体宽、主体高、浅缘 | 三把钥匙以不同纹样区分，中间钥匙通过连接环挂在主圈，扩框保留匙尖 | [对照](../../../artifacts/cache/v111/art/comparisons/deco/I13-r01.png) |
| I13 | glass | [r01](../../../ArtSource/Art11/glass/illustrations/I13/r01/record.json) | 1312×1199 | 91% → 91% | 深色、透明占比、主体宽、主体高 | 三把钥匙与开锁用铅线分开，裁切按原始非方尺寸计算 | [对照](../../../artifacts/cache/v111/art/comparisons/glass/I13-r01.png) |

五套重绘的透明占比、主体宽和主体高偏离来自满幅背景锚图与透明交付之间的差异，逐张用实际轮廓和留白判定。瑞士的米纸色、钴蓝与黑硬影占比，漫画的象牙与原色平涂比例，唐三彩的釉绿和铁红，黑金的包边、金花与珐琅，彩绘玻璃的钴蓝和宝石色均已与同主题邻图复看。唐三彩和黑金的浅缘标签多落在釉边或金属薄边，两种底色合成未见持续白边。瑞士 C155 r01 则有可见边雾，已作为返工样本保留。

三张原图采用模型实际输出的比例。swiss C154 是 1326×1187，sancai I13 和 glass I13 均为 1312×1199。按 W22i 的原图逐字节入库先例保留这三张，图标按各自的 `imageSize` 裁切。协调者入库后核对实际导入和压缩格式。 [V1.1 安装与导入记录](../v11/W22I_REPORT.md)

| 额外修订 | 运行核对 | 视觉结果与处理 |
| --- | --- | --- |
| C155 default r01 | `INVALID_RUN`，零输出 | CLI 用户目录解析失败。无对照图。 [运行记录](../../../ArtSource/Art11/default/illustrations/C155/r01/record.json) |
| C155 swiss r01 | 调用有效 | A2、B5 返工。上方半透明灰蓝雾，双底可见。r02 局部编辑清雾后复查。 [运行记录](../../../ArtSource/Art11/swiss/illustrations/C155/r01/record.json) · [邻卡对照](../../../artifacts/cache/v111/art/comparisons/swiss/C155-r01.png) |
| C156 comic r01 | 调用有效 | B5 返工。内侧青色动作线末端有软雾。r02 局部编辑为平涂尖尾后复查。 [运行记录](../../../ArtSource/Art11/comic/illustrations/C156/r01/record.json) · [邻卡对照](../../../artifacts/cache/v111/art/comparisons/comic/C156-r01.png) |

## 7. 紧凑图标

采用 W13a 的 `alpha-core-1`、3% 边距与预乘 alpha Lanczos。现有批量命令要求输入包含该主题全部历史覆写 ID，本批在隔离目录调用原脚本的 `crop_one`、`bake_icon` 和 `table_text`。覆写了 15 个裁框。C154 六套保留托盘、手持骰和池中骰，C156 六套保留碎岩、反弹骰与完整量管，I13 的瑞士、唐三彩、黑金保留锁影或外侧匙尖。全部烘焙图标在主题底和深毡下核过 32 px，图标外缘均无 alpha 大于 128 的像素。独立只读复核用原方法重烘了 24 个图标，PNG 逐字节一致。24 个裁框均保留原图 alpha 不低于 128 的主体像素。 [裁切表和覆写](../../../artifacts/cache/v111/art/crops/) · [全套总览](../../../artifacts/cache/v111/art/comparisons/overview.png)

## 8. 周额度

开工前 22:54:22 UTC 的周额度已用 90%、余 10%。最后一次生成完成后 23:13:09 UTC 已用 91%、余 9%。23:15:21 UTC 复查仍为 91%。记录的重置时间为 2026-11-30 09:16:26 UTC。26 张实际生成图包含两张返工前候选，另有一次 CLI 启动失败，未产生图。按显示差值计算，观测平均为 1 个百分点 ÷ 26，约 0.038 个百分点/生成张。额度是整数显示的共享账户周读数，单张的实际耗用无法从该精度拆出。 [逐次复核](../../../artifacts/cache/v111/art/verification.json)

## 9. 入库清单

清单记录每个源文件及目标、SHA-256、图像尺寸、生成记录、裁切表、对照图及全部 72 条旧路径。独立核对确认 48 个唯一来源、48 个唯一目标、72 个现存待清理文件，且新旧路径无交叉。 [入库清单](../../../tools/art_v11/install_manifests/w8-install.json)

| 内容 · 主题 | 插画源 → 目标 | 图标源 → 目标 |
| --- | --- | --- |
| C155 · default | `ArtSource/Art11/default/illustrations/C155/r02/original.png` → `DicePrototype/Assets/Art/Art071/Illustrations/illustration_c155.png` | `ArtSource/Art11/default/icons/icon_C155.png` → `DicePrototype/Assets/Art/Art11/Icons/default/icon_C155.png` |
| C155 · swiss | `ArtSource/Art11/swiss/illustrations/C155/r02/original.png` → `DicePrototype/Assets/Resources/Themes/swiss/Art/illustration_C155.png` | `ArtSource/Art11/swiss/icons/icon_C155.png` → `DicePrototype/Assets/Art/Art11/Icons/swiss/icon_C155.png` |
| C155 · sancai | `ArtSource/Art11/sancai/illustrations/C155/r01/original.png` → `DicePrototype/Assets/Resources/Themes/sancai/Art/illustration_C155.png` | `ArtSource/Art11/sancai/icons/icon_C155.png` → `DicePrototype/Assets/Art/Art11/Icons/sancai/icon_C155.png` |
| C155 · comic | `ArtSource/Art11/comic/illustrations/C155/r01/original.png` → `DicePrototype/Assets/Resources/Themes/comic/Art/illustration_C155.png` | `ArtSource/Art11/comic/icons/icon_C155.png` → `DicePrototype/Assets/Art/Art11/Icons/comic/icon_C155.png` |
| C155 · deco | `ArtSource/Art11/deco/illustrations/C155/r01/original.png` → `DicePrototype/Assets/Resources/Themes/deco/Art/illustration_C155.png` | `ArtSource/Art11/deco/icons/icon_C155.png` → `DicePrototype/Assets/Art/Art11/Icons/deco/icon_C155.png` |
| C155 · glass | `ArtSource/Art11/glass/illustrations/C155/r01/original.png` → `DicePrototype/Assets/Resources/Themes/glass/Art/illustration_C155.png` | `ArtSource/Art11/glass/icons/icon_C155.png` → `DicePrototype/Assets/Art/Art11/Icons/glass/icon_C155.png` |
| C154 · default | `ArtSource/Art11/default/illustrations/C154/r01/original.png` → `DicePrototype/Assets/Art/Art071/Illustrations/illustration_c154.png` | `ArtSource/Art11/default/icons/icon_C154.png` → `DicePrototype/Assets/Art/Art11/Icons/default/icon_C154.png` |
| C154 · swiss | `ArtSource/Art11/swiss/illustrations/C154/r01/original.png` → `DicePrototype/Assets/Resources/Themes/swiss/Art/illustration_C154.png` | `ArtSource/Art11/swiss/icons/icon_C154.png` → `DicePrototype/Assets/Art/Art11/Icons/swiss/icon_C154.png` |
| C154 · sancai | `ArtSource/Art11/sancai/illustrations/C154/r01/original.png` → `DicePrototype/Assets/Resources/Themes/sancai/Art/illustration_C154.png` | `ArtSource/Art11/sancai/icons/icon_C154.png` → `DicePrototype/Assets/Art/Art11/Icons/sancai/icon_C154.png` |
| C154 · comic | `ArtSource/Art11/comic/illustrations/C154/r01/original.png` → `DicePrototype/Assets/Resources/Themes/comic/Art/illustration_C154.png` | `ArtSource/Art11/comic/icons/icon_C154.png` → `DicePrototype/Assets/Art/Art11/Icons/comic/icon_C154.png` |
| C154 · deco | `ArtSource/Art11/deco/illustrations/C154/r01/original.png` → `DicePrototype/Assets/Resources/Themes/deco/Art/illustration_C154.png` | `ArtSource/Art11/deco/icons/icon_C154.png` → `DicePrototype/Assets/Art/Art11/Icons/deco/icon_C154.png` |
| C154 · glass | `ArtSource/Art11/glass/illustrations/C154/r01/original.png` → `DicePrototype/Assets/Resources/Themes/glass/Art/illustration_C154.png` | `ArtSource/Art11/glass/icons/icon_C154.png` → `DicePrototype/Assets/Art/Art11/Icons/glass/icon_C154.png` |
| C156 · default | `ArtSource/Art11/default/illustrations/C156/r01/original.png` → `DicePrototype/Assets/Art/Art071/Illustrations/illustration_c156.png` | `ArtSource/Art11/default/icons/icon_C156.png` → `DicePrototype/Assets/Art/Art11/Icons/default/icon_C156.png` |
| C156 · swiss | `ArtSource/Art11/swiss/illustrations/C156/r01/original.png` → `DicePrototype/Assets/Resources/Themes/swiss/Art/illustration_C156.png` | `ArtSource/Art11/swiss/icons/icon_C156.png` → `DicePrototype/Assets/Art/Art11/Icons/swiss/icon_C156.png` |
| C156 · sancai | `ArtSource/Art11/sancai/illustrations/C156/r01/original.png` → `DicePrototype/Assets/Resources/Themes/sancai/Art/illustration_C156.png` | `ArtSource/Art11/sancai/icons/icon_C156.png` → `DicePrototype/Assets/Art/Art11/Icons/sancai/icon_C156.png` |
| C156 · comic | `ArtSource/Art11/comic/illustrations/C156/r02/original.png` → `DicePrototype/Assets/Resources/Themes/comic/Art/illustration_C156.png` | `ArtSource/Art11/comic/icons/icon_C156.png` → `DicePrototype/Assets/Art/Art11/Icons/comic/icon_C156.png` |
| C156 · deco | `ArtSource/Art11/deco/illustrations/C156/r01/original.png` → `DicePrototype/Assets/Resources/Themes/deco/Art/illustration_C156.png` | `ArtSource/Art11/deco/icons/icon_C156.png` → `DicePrototype/Assets/Art/Art11/Icons/deco/icon_C156.png` |
| C156 · glass | `ArtSource/Art11/glass/illustrations/C156/r01/original.png` → `DicePrototype/Assets/Resources/Themes/glass/Art/illustration_C156.png` | `ArtSource/Art11/glass/icons/icon_C156.png` → `DicePrototype/Assets/Art/Art11/Icons/glass/icon_C156.png` |
| I13 · default | `ArtSource/Art11/default/illustrations/I13/r01/original.png` → `DicePrototype/Assets/Art/Art071/Illustrations/illustration_i13.png` | `ArtSource/Art11/default/icons/icon_I13.png` → `DicePrototype/Assets/Art/Art11/Icons/default/icon_I13.png` |
| I13 · swiss | `ArtSource/Art11/swiss/illustrations/I13/r01/original.png` → `DicePrototype/Assets/Resources/Themes/swiss/Art/illustration_I13.png` | `ArtSource/Art11/swiss/icons/icon_I13.png` → `DicePrototype/Assets/Art/Art11/Icons/swiss/icon_I13.png` |
| I13 · sancai | `ArtSource/Art11/sancai/illustrations/I13/r01/original.png` → `DicePrototype/Assets/Resources/Themes/sancai/Art/illustration_I13.png` | `ArtSource/Art11/sancai/icons/icon_I13.png` → `DicePrototype/Assets/Art/Art11/Icons/sancai/icon_I13.png` |
| I13 · comic | `ArtSource/Art11/comic/illustrations/I13/r01/original.png` → `DicePrototype/Assets/Resources/Themes/comic/Art/illustration_I13.png` | `ArtSource/Art11/comic/icons/icon_I13.png` → `DicePrototype/Assets/Art/Art11/Icons/comic/icon_I13.png` |
| I13 · deco | `ArtSource/Art11/deco/illustrations/I13/r01/original.png` → `DicePrototype/Assets/Resources/Themes/deco/Art/illustration_I13.png` | `ArtSource/Art11/deco/icons/icon_I13.png` → `DicePrototype/Assets/Art/Art11/Icons/deco/icon_I13.png` |
| I13 · glass | `ArtSource/Art11/glass/illustrations/I13/r01/original.png` → `DicePrototype/Assets/Resources/Themes/glass/Art/illustration_I13.png` | `ArtSource/Art11/glass/icons/icon_I13.png` → `DicePrototype/Assets/Art/Art11/Icons/glass/icon_I13.png` |

C90、C128、C145 的清理范围如下。每条 PNG 和对应的 `.png.meta` 在清单中逐路径列出。

| 范围 | 旧资源路径 |
| --- | --- |
| 默认插画 | `DicePrototype/Assets/Art/Art071/Illustrations/illustration_{c90,c128,c145}.png[.meta]` |
| 其余主题插画 | `DicePrototype/Assets/Resources/Themes/{swiss,sancai,comic,deco,glass}/Art/illustration_{C90,C128,C145}.png[.meta]` |
| 六套图标 | `DicePrototype/Assets/Art/Art11/Icons/{default,swiss,sancai,comic,deco,glass}/icon_{C90,C128,C145}.png[.meta]` |

现有 `install_art.py` 面向整套主题，会要求完整的 P0Config ID 集合并清理计划外插画。其 default 分支只处理徽记，紧凑图标也需单独入库。协调者持 Unity 写锁按清单集成。默认图与图标可分别沿用 `V09Setup.Integrate` 及 `V13CardSetup.BindCompactIcons` 更新引用，主题插画由 `V11ThemeImport` 导入。若更新整套 `IconCrops` 溯源表，可按本批裁切记录增补并清理三项历史条目。 [安装先例](../v11/W22I_REPORT.md) · [裁切约定](../v11/W13A_CROPS.md)

## 10. 待协调者与用户

协调者逐张审图、入库、处理旧件，然后在 1080p 和 720p 双窗验证商店卡、详情及紧凑槽。优先查看 C155 与 C24 的紧凑辨识、C155 的红点群、C156 的低液面和三张非方图的导入结果。当前 schema 通过历史条目的 `notes` 记录移除。如后续确需程序化读取生命周期状态，由协调者另行决定 schema。W8 状态和 Unity 写锁由协调者汇总到 `docs/STATE.md`。用户审定风格。
