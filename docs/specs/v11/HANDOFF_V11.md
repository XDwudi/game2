# V1.1 剩余开发交接方案

状态：**交接**（2026-11-22）。

- 交出方：Claude（V1.1 协调者）；接收方：GPT（Codex）。
- 用户当天原话：“因为你额度不够了，写一份剩余内容的交接开发方案，我交个gpt进行完成。三份方案都批准。”

本文件按 `templates/HANDOFF.md` 的项目写，是接手后的工作入口。接收者不能依靠先前的聊天记录。本文件、`AGENTS.md` 与 `docs/STATE.md` 之外的事实，以仓库里的权威文件为准。发现矛盾时先停下，列出具体冲突，不要挑最方便的解释（AGENTS.md）。

## 0 先读（按顺序）

1. `AGENTS.md`：项目协作规则。用户的项目指令高于一般习惯。
2. `docs/STATE.md` 的“V1.1 开发”一节：所有已做的决定、验收记录、文件锁、遗留问题。
3. 本文件。
4. [开发方案 DEV_PLAN](DEV_PLAN.md)：目标 G1–G7、§3 用户决定、§4 默认做法、§5 工作包、§6 验收条件。
5. [进度报告 PROGRESS_REPORT_20261121](PROGRESS_REPORT_20261121.md)：已完成与还差的清单。
6. 三份已批准的方案：
   - [FONTS_WRAP](FONTS_WRAP.md)（W21）；
   - [SCREENS_LAYOUT](SCREENS_LAYOUT.md)（W16）；
   - [SETTLEMENT_LAYOUT](SETTLEMENT_LAYOUT.md)（W15）。
7. 现行规格：
   - [THEME_SPEC](THEME_SPEC.md)：风格资源、色板键、LabelOn11、桌面上的字；
   - [CARD_TEMPLATE](CARD_TEMPLATE.md)、[SHOP_LAYOUT](SHOP_LAYOUT.md)、[PLAY_LAYOUT](PLAY_LAYOUT.md)；
   - [RULES_DELTA](RULES_DELTA.md)。
8. 美术：
   - `.agents/skills/game-art-production/SKILL.md`；
   - `docs/art/STYLE_GUIDE.md`、`docs/art/AI_ART_PIPELINE.md`；
   - [W22A_SUBJECTS](W22A_SUBJECTS.md)，尤其 §7 第 7 条：红蓝骰点就是红骰蓝骰；
   - `ArtSource/Art11/subjects.json`。
9. 文字：用户要求照这两份文字稿修改。
   - `docs/proposals/2026-11-16-text-readability.md`；
   - 附录 `docs/proposals/2026-11-16-text/UI_REWRITES.md`、`CONTENT_REWRITES.md`。

## 1 交接时的状态

| 项 | 内容 |
| --- | --- |
| 仓库与分支 | `E:\agent_workspace\gpt\game2`，分支 `codex/v1.1`；BASE 为 `6304eade`（`codex/v1.0`）。交接时的 HEAD 是本文件所在的提交（`git log -1 -- docs/specs/v11/HANDOFF_V11.md`）。分支**只在本机，没有推送**（第 7 节第 1 条） |
| 工作树 | 已跟踪的文件没有未提交的改动，暂存区为空 |
| 未跟踪的内容 | `Releases/`、`evidence/`、`artifacts/cache/`、`ArtSource/AIConcepts/`，以及三个没有 `record.json` 的半截生成：`ArtSource/Art11/comic/illustrations/C67/r01`、`C72/r01`、`sancai/illustrations/C73/r01`。这些**不要提交** |
| 进程与锁 | 没有运行中的 Unity 进程，没有子 agent，文件锁全部释放 |
| 最近全量测试 | W22i，2026-11-19：EditMode 2439/2439（`evidence/v11/unity/nunit-20261119-101858`）；PlayMode 1920×1080 252/252（`-105913`）；1280×720 252/252（`-113804`）；Python 436/436 |
| Codex 周额度 | 最近一次生成后用到 31%。成批生成约 35 张涨 1 个百分点 |
| 已完成 | 计划 26 个工作包里完成 16 个，另有 12 个补充包已完成。<br>五套风格的界面件、桌面、920 张卡牌插画、42 张首领徽记、1,104 张紧凑图标都已装进游戏。<br>对局、卡面、商店三个画面已按新版式完成。<br>明细见进度报告 |

## 2 用户已定的事（照办，不要重新询问）

- **2026-11-16 的 V1.1 指示九条**（原文在 DEV_PLAN §0）与 D1–D5（DEV_PLAN §3）：
  - D1：五套风格每套重画全部插画；
  - D2：“控制器”改名为“卡牌”；
  - D3：保持“提交”；
  - D4：稀有度用“常见 / 进阶 / 稀有”；
  - D5：同店买入的卡牌，离店前卖价不超过实际支付价。
- **检查点**：
  - 检查点 ②（2026-11-17）：对局布局、瑞士方向、默认稀有度三色、“印章”徽记样板；
  - 检查点 ③（CP3-1 至 CP3-3）：各风格只换画法、不换内容（玩家要认得出是同一张卡，功能提示不变）；四款标题字体已批准下载；成批生成用 Codex 额度，不用 API Key。用户设置了环境变量 `OPENAI_API_KEY`，并说明“我不经手明文”：不要读取或使用它。
- **2026-11-22**：W21、W16、W15 三份方案获批。
  - 按“照推荐的答案批准”理解，各稿的决定项全部按“推荐”一栏，新写的文字照稿采用，协调者在审阅时的决定（各稿第 13 节）照办。
  - 其中三条推荐会新增或保留工作：W16 第 7 问选 B（另立 W16b）；W15 第 8 问选 A（首领入关演出并入 W15）；W21 第 5 问选 A（补齐粗体字表）。
- **工作规则（2026-11-16 起）**：先交改动方案，用户批准后才改游戏文件。已批准的三份方案可以直接实施；第 3 节标明“先写方案”的工作，要先交用户。
- **性能**：在用户确认最终版本之前可以放宽（DEV_PLAN §1 非目标）。性能与包体问题记入优化清单（第 3.10 节），不在 rc.1 之前做，除非用户要求。
- **发布（2026-11-14 的长期授权）**：开发完成的版本分支可以直接推送，rc 预发布附件可以直接上传。永远不推送 `main`。
- **美术方向**：不做像素画，保持清晰的高清画面（用户此前否决了像素风改版）。

## 3 剩余工作与顺序

建议顺序：W21 → W16 → W16b → W15。
- 美术件（3.5）可以与代码并行，但生成与 Unity 测试不能同时跑。
- W18 与主菜单主视觉的方案（3.6、3.7）可以先写，交用户批准后再做。
- 3.8、3.9 在最后。

### 3.1 W21 标题字体与卡面断行（已批准）

- **依据**：[FONTS_WRAP](FONTS_WRAP.md) 全文。它的分析产物在 `artifacts/cache/v11/w21/spec/`，不进版本库，若被清掉可按 `scripts/` 重算。
- **做法**：
  - **黄金样例先做唐三彩**：标题字体的角色、静态 SDF 图集、Face Info、回落链。实机截图（1080p、720p，100% 与 150%）交用户看过美术观感后，再推广到漫画、黑金、彩绘玻璃。
  - **卡面断行**：`WrapLines21` 与 `BalancedLines11` 的规则见 §8；词表草稿 `lexicon-draft.txt` 需要人工复核后定稿（R4）。
  - **粗体字表**：补齐 DiceChineseBold（第 5 问 A）。
  - **TMP 默认字体**：换成我们自己的资源，构建里不再带 LiberationSans SDF（第 13 节第 1 条）。
  - **量宽辅助**：修 `PreferredWidth11`、`ShopLabelWidth14`，按实际字体量宽；修 TMP 在行尾留“（”的问题。
  - **theme.json**：各风格的 `fonts` 键写进生成器（`tools/art_v11/<风格>/build_*.py`）。theme.json 有“与重新生成一致”的测试，不能手改。
  - **许可证材料**：`Fonts/fonts_manifest.json`，供 W26 打包用。
- **验收**：FW-01 至 FW-18（§10.1）。测试的增改见 §10.2。

### 3.2 W16 主菜单、新局、选关、手册、设置、暂停、弹窗、引导（已批准）

- **依据**：[SCREENS_LAYOUT](SCREENS_LAYOUT.md) 全文。验收 SC-01 至 SC-16。
- **注意**：
  - **字体**：用 W21 的角色接口画新标题与主按钮。
  - **设置页**：“画面风格”页做成六格预览（第 6 问 A）；没有开放的风格仍按预览标记隐藏，见 3.8。
  - **彩绘玻璃的选中页签**：对比度只有 1.70:1，要核对选中态另有可辨的提示（STATE 中 W20Z2 的决定）。
  - **用语与旧代码**：
    - 界面里剩下的“控制器”改为“卡牌”（第 10 节）；
    - 手册颜色行的内部编号“C43”要去掉（RULES_DELTA 第 389 行）；
    - 不再调用的旧界面代码里有 30 多条用语检查命中：删掉或改写，在报告里说明。
  - **桌面上的字**：每套每处至少 4.5:1，用 `tools/art_v11/table_text_contrast.py` 与 `table_text.json` 验收。

### 3.3 W16b 对局里打开的弹窗的文字 P1（W16 第 7 问 B）

- **范围**：得分明细与逐步明细、计分记录、骰库（查看骰子、赠礼的骰面改造选目标）、遥控骰子、错乱乘积预览、卡牌详情与确认出售、卡牌顺序面板。
- **依据**：UI_REWRITES §2.5、§2.7、§2.9、§2.11，以及 SCREENS_LAYOUT §1.2 第二行。
- **做法**：先列一张改动表，格式同 W16 的表；附录没有原文的句子标“新写，待用户确认”，交用户确认。然后实施，用对局夹具测。

### 3.4 W15 结算、首领战利品页、整局结果、演出（已批准）

- **依据**：[SETTLEMENT_LAYOUT](SETTLEMENT_LAYOUT.md) 全文，包括第 13 节的六条协调者决定。验收 SE-01 至 SE-17。
- **注意**：
  - **徽记接入**：本关牌、商店顶栏小图、首领关结算、整局通关都接上 42 张已导入的首领徽记，共用 §8.5 的族表。默认风格的 7 张在 `Assets/Art/Art11/Emblems/`，要另设字段绑定：`contentIcons` 必须正好是 184 张插画，`PlayerVerification.V06:246` 与两条 PlayMode 测试会检查。
  - **框角问题**（第 13 节第 6 条）：
    - 首领徽记与标题条贴着内环（漫画、彩绘玻璃）；
    - “我的构筑”第一格压在框角装饰上（彩绘玻璃左下角的金星、黑金的楔角）。
  - **交互与配色**：防双击 0.3 秒，只限指针点击；回车键跳过或继续；失败标签用色板键 `Danger`。
  - **美术件没到位时**：用明确可见的占位，缺件报告逐项列出，不算通过。交用户的截图等战利品图到位，或注明“战利品图为占位”。

### 3.5 美术件（约 60 件）

| 项 | 数量 | 规格 | 做法 |
| --- | --- | --- | --- |
| 战利品图 L1–L6 | 36（六套 × 6） | SETTLEMENT_LAYOUT §8.1、§8.2：1:1 透明，1254² RGBA；画“这一类奖励”；不画红蓝骰、金面、玻璃骰与文字 | 第一步：先写主体表，同 W22a，把每种的主体与功能提示写进 `subjects.json` 或单独的表。<br>第二步：默认风格先做 6 张作黄金样例（新的资源类型，AGENTS.md 要求先做黄金样例），实机截图交用户。<br>第三步：再按五套风格成批生成，经只读复核、协调者核对后提交。<br>第四步：用 `install_art.py`（需扩展）装进 `Resources/Themes/<风格>/Art/illustration_L*.png`；默认风格的绑定由 W17 定 |
| 档位边框 | 4（默认；五套可选） | SETTLEMENT_LAYOUT §8.3：白色，按档位乘色；`UI/ui_emblem_tier1…4.png` | 用脚本画矢量（`tools/art_v11/default/`）。加进 UI 贴图后重新生成 `tools/art_v11/theme_contract.json`，五套生成器的测试要跟着更新 |
| 资源图标 | W15 先用 3 个（W17 一族约 12 个） | SETTLEMENT_LAYOUT §8.4：`ui_icon_stage`、`ui_icon_submit`、`ui_icon_interest`，单色白 | 同上 |
| 主菜单主视觉 | 6（默认 W17，五套 W23） | 先写方案（见 3.7） | 用户批准后经 artgen 生成，交用户签署美术标杆 |

**美术流程**（W22 用过、已验证）：

| 环节 | 工具与规则 |
| --- | --- |
| 生成 | `tools/art_gen/artgen.py`（Codex 内置生图，以风格包的锚点为唯一风格参考，带记录）。成批时用 `tools/art_v11/batch/run_batch.ps1 -JobList <清单> -Tag <名>`：<br>- 清单每行“风格包路径\|job 路径”；<br>- 一次一张，Unity 测试运行时自动暂停，额度到 85% 停止；<br>- 已有有效结果的跳过，`-Force` 强制重画；<br>- 日志在 `artifacts/cache/v11/batch-logs/` |
| 写 job | `tools/art_v11/make_redraw_jobs.py`，读 `subjects.json`，含按风格的 `styleCues` |
| 复核 | `tools/art_v11/batch/` 下的 `batch_progress.py`、`list_stale.py`、`pairs_sheet.py`、`redo_pairs.py`，用法见各文件开头 |
| 判图的要点 | - 同一主体，功能提示不变；<br>- 红蓝骰点就是红骰蓝骰：中性骰子是深色点；<br>- 金面、玻璃是机制，不能乱用；<br>- 不能有文字；<br>- 背景完全透明；<br>- 在 160 px 下也看得清；<br>- 每张都在纸面和毡面上看一遍 |
| 生成器的毛病 | 生成器常常不照描述画。改通用描述会让其他风格的 job 变旧；只对一套风格的问题用 `styleCues.<风格>`。每次改动都在 `subjects.json` 的备注里写日期与原因 |

### 3.6 W18 章节色调与三档桌面（**先写方案**）

- **范围**：DEV_PLAN W18 一行，美术方案 B2 与章节部分。W11 已做首领关、高压关的压暗与配色；W15 做首领入关演出。
- **做法**：先写方案给用户批准，包括章节色调怎么随风格变、三档桌面在六套风格里的做法、静区规则（THEME_SPEC 的四个静区必须是纯色），然后实施。

### 3.7 主菜单主视觉（**先写方案**）

- **依赖**：W16 的主菜单版式（第 2 问 A：卡面用 W13 模板）。
- **做法**：先写构图方案，再做默认风格的黄金样例，交用户签署后推广到五套。

### 3.8 正式开放五套风格

- **现状**：五套风格的 `theme.json` 里 `preview: true`，玩家在设置里看不到。
- **时机**：W21、W16（六格预览页）完成并验收后，在各风格的生成器里改成不再是预览，重建，并更新依赖预览开关的测试。
- **检查**：F-THEME-01、F-THEME-02。

### 3.9 阶段 5：W24、W25、W26，然后检查点 ④

- **W24**：测试整理。布局与文字断言逐项改写并记录；新界面的测试要断言可见性，即不被遮挡、字形画在框内，不能只断言文字内容。
- **W25**：独立复核，复核者只读，不是实施者。覆盖规则、文字（可用 Codex 只读）、美术、实机截图。
- **W26**：冻结与门禁；Windows x64 与 macOS Universal 构建；每套风格一轮回放截图；打包；发布 `p1.1.0-rc.1`；更新 README 与 STATE。
  - **交付链**：参照 v1.0 的 `tools/v10_delivery.py`、`package_v10.py`、`publish_v10_release.py` 写 v11 的版本，`tools/v11_delivery.py` 已有。
  - **许可证**：包里按 FONTS_WRAP §6 放 `Licenses/`（macOS 另在 `.app/Contents/Resources/Licenses/`），并核对 FW-10。
  - **包体**：实际大小在这时量（估计每个平台多约 1.2 GB）。
  - **未验证要写明**：macOS 没有实机启动过的，要如实写“未验证”。
- **检查点 ④**：用户试玩 rc。

### 3.10 优化清单（用户确认最终版本后再做）

- **包体**：新插画、徽记与图标估计使每个平台的包多约 1.2 GB。可选做法：
  - 按显示尺寸调低插画导入上限（如 768 或 512）；
  - 49 张非正方形图缩到 1024 后边长不是 4 的倍数，Unity 不压缩，可在安装时补边，清单记两个哈希；
  - 压缩紧凑图标。
- **内存**：紧凑图标六套随场景常驻，共 143.7 MiB，可改为按风格懒加载。
- **断行**：细则、悬停、手册等长文字仍由 TMP 自动断行，可以用 `WrapLines21` 另立任务。

## 4 工具与命令

| 用途 | 路径或命令 |
| --- | --- |
| Unity 6000.6.7f1 | `E:\unity\6000.6.7f1\Editor\Unity.exe`（PATH 上的 `unity.exe` 是 Hub 的命令行，不是它）；工程是 `DicePrototype/` |
| Python | `E:\anaconda3\python.exe`（Pillow、numpy、fontTools）。读下载件或外部数据时用 `-I` |
| git | `C:\Users\f\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe`。会调用 git 的 Python 工具，要把它所在的文件夹放到 PATH 前面 |
| EditMode（无界面，约 1 分钟） | `Unity.exe -batchmode -projectPath DicePrototype -executeMethod DiceCraft.Editor.V072BatchTests.RunEditMode -logFile <日志>`（不要加 `-quit`：测试在方法返回后才异步运行，运行完由方法自己退出编辑器） |
| PlayMode（必须开窗口，一次约 27 分钟） | `powershell -File tools/unity_windowed_tests.ps1 -GameView 1920x1080 -Log <日志>`，720p 用 `-GameView 1280x720`，可加 `-Filter a,b`。脚本会处理管理员权限对话框，编译错误时会停下 |
| 窗口运行之后 | 一定执行 `git checkout -- DicePrototype/Assets/Game/Fonts/DiceChineseFallback.asset`（编辑器会改写动态字体缓存） |
| NUnit 证据 | 报告在 `evidence/v11/unity/nunit-<UTC 时间>.xml` 与 `.binding.json`，不进版本库。核对：<br>- `python -I tools/verify_nunit_bindings.py <仓库根> <binding.json>`：报告哈希、开始与结束时文件一致、与现在的工作树一致；<br>- `python tools/quality_check.py nunit evidence/v11/unity/<x>.xml --min-tests N`：路径用正斜杠；它只核对完整性，不能代替看通过数 |
| Python 全量 | `python -m unittest discover -s tests -v`（约 10 分钟，低优先级跑，不要与 PlayMode 同时跑） |
| 构建 | `Unity.exe -batchmode -quit -projectPath DicePrototype -activeBuildProfile Assets/Settings/BuildProfiles/Windows_x64.asset -executeMethod DiceCraft.Editor.PrototypeBuild.BuildActiveProfile`；macOS 用 `macOS_Universal.asset`；一个平台一个编辑器进程 |
| 美术 | 第 3.5 节；裁切图标用 `tools/art_v11/crops/make_crops.py --theme <风格>`；安装用 `tools/art_v11/install_art.py`（清单在 `tools/art_v11/install_manifests/`） |
| 风格件 | 各风格生成器在 `tools/art_v11/<风格>/`；严格审计与风格契约见各 README 与 `tools/art_v11/theme_contract.py` |

## 5 工作规则（摘自 AGENTS.md 与 V1.1 的经验）

- **角色分开**：实施者不签自己的最终质量结论。复核者只读，不改验收标准。状态按 DRAFT → READY → IMPLEMENTING → VERIFYING → REVIEW → ACCEPTED 记录。美术标杆、体验与发布由用户决定。
- **Unity 串行**：同一工程的编辑器写操作一次只有一个。PlayMode 运行期间不跑生成、Python 全量或其他吃 CPU 的任务，帧时间测试会受影响。
- **测试**：
  - 不删测试，不降阈值，不缩减范围来掩盖失败；
  - 改了的断言逐条写进该工作包的测试改动表（如 `W14_TEST_CHANGES.md`）；
  - 窗口测试不能依赖真实鼠标的位置。
- **证据**：绑定源码版本，改动后重跑受影响的检查。不虚构测试、截图、运行或审批。通过、失败、未运行与不适用要分清。
- **提交**：
  - 只提交自己的路径：`git -c maintenance.auto=false -c gc.auto=0 commit -F <说明文件> -- <路径>`。普通提交会触发自动重新打包（约 2 GB，好几分钟），会拖慢 Unity 测试。
  - 说明文件用 UTF-8 无 BOM。
  - 暂存区不能留东西。
  - **边做边提交**：每个提交自己能编译、能通过测试。V1.1 中途三次碰到用量上限，有一次留下约 1000 个没提交的文件。
- **STATE**：结束或交接前更新 `docs/STATE.md`，写明产物、验收结论、剩余问题、文件锁与下一步。日期用本机时间（UTC−5）；NUnit 文件名里的时间是 UTC。
- **文字**：附录里没有原文的新句子，标“新写，待用户确认”。玩家看到的地方用“卡牌”（D2）。
- **要停下来问用户的**：新的设计或美术方向、范围变化、标杆与体验判断、长期授权以外的对外操作（推送 `main`、改已发布的版本、删除数据）。

## 6 已知问题与前序经验

- **只在截图里看得出的缺陷**：v1.0 有过只在截图里看得出的界面缺陷，所有断言文字内容的测试都通过了。新界面要断言可见性。
- **池化对象的状态**：池化的 TMP 对象会带着上一次的自动字号（W20Z2 修过迷你卡）。复用对象时要重置状态。
- **字压到框线**：风格的框比默认厚时，字容易压到框线。用 `FrameInsets11` 与字形框量（W20Z2 第 4 项）；框角装饰要另外量（W22i）。
- **色块上的字**：一律用 LabelOn11 选色，目标 4.95:1（THEME_SPEC §2）。
- **生成器的老问题**：中性骰子画成红点或蓝点；打磨、加工的面画成金色；玻璃画成不透明；雾气铺到画布边；小骰子上的颜色看不清；顺子或对子数错。每张都要按功能提示核对，不能只看风格。
- **加载与包体**：风格的插画按张加载、换风格后释放（W22i）。不要改回整套 `LoadAll`：首次切换会到 552 ms，每套还要常驻约 200 MB。
- **`.git` 里的临时文件**：有一个 672 MB 的半截临时文件 `tmp_pack_nbT18p`，两周后 git 会自动清理。删除前要问用户。

## 7 仍待用户决定的事

1. `codex/v1.1` 要不要先推送到 GitHub 作备份。如果接手的 GPT 在云端、要从 GitHub 拉代码，就必须先推送。
2. ~~已发布的 p0.9、p1.0 预发布包里没有字体许可证~~ 已补（用户 2026-11-22“字体许可证补”）：两个发布各加附件 `FONT-LICENSES.zip` 并在说明里加了一段，游戏包没有改动（STATE）。p1.1.0 起按 FONTS_WRAP §6 把 `Licenses/` 放进包里。
3. ~~`.git` 里 672 MB 的临时文件~~ 已删（用户 2026-11-22“清理git的文件夹”）。约 2.6 GiB 的零散对象等没有 agent 工作时用 `git -c gc.reflogExpire=never -c gc.reflogExpireUnreachable=never gc --prune=never` 打包（不删除对象，不要与 Unity 测试或其他 git 写入同时跑）。测试留下的临时文件夹没有清理，要删前先问用户。

## 8 接手后的第一步

1. 复述关键约束：第 2、5 节，加上 AGENTS.md。发现矛盾先回报。
2. 确认环境：工作树干净，没有 Unity 进程；EditMode 能跑通，作为接手基线。
3. 在 STATE 里写上自己的工作包与文件锁。从 W21 的唐三彩黄金样例开始。
