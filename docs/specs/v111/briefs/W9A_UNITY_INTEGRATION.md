# W9a 交接包：Unity 里的集成与首次实跑（美术入库、字体重烘、EditMode 与 PlayMode）

你是这个工作包的实施者，协调者是主会话里的 Claude。请完整读完本文件再动手。**本包期间你是 Unity 工程唯一的写入者**（场景、Prefab、资源导入、项目设置都由你串行操作），同一时间只开一个 Editor。

## 背景

V1.1.1（规则 p1.1.1）的规则层、会话层、界面代码、配置与目标分都已提交，并在 .NET 宿主上通过（规则 3982、会话 616、表演 2419）。但**到现在为止没有任何一行新代码在 Unity 里运行过**：界面的两个工作包（W7a、W7b）只做了编译检查，PlayMode 测试的改写与新增全部没有跑过，新美术没有入库，字体图集没有重烘。本包把这些在 Unity 里做完并跑通。

## 必读

- `AGENTS.md`（项目规则）与 `docs/art/AI_ART_PIPELINE.md` 的入库与实机检查部分。
- `docs/specs/v111/W8_REPORT.md`（新美术：24 张插画、24 个图标，§9 入库清单与旧资源清理范围）与 `tools/art_v11/install_manifests/w8-install.json`。
- `docs/specs/v11/W22I_REPORT.md`（V1.1 的安装、导入设置、紧凑图标绑定的做法与工具：`tools/art_v11/install_art.py`、`V11ThemeImport`、`V22iArtSetup.Integrate`、`V13CardSetup.BindCompactIcons`、`V09Setup.Integrate`、裁切表）。
- 字体重烘：`docs/specs/v11/` 下 W21 的字体报告（搜 `title_chars`、`V21TitleFont`），`docs/specs/v111/W7A_NOTES.md` §4 与 `W7B_NOTES.md` §4（新增字符清单）。
- `docs/specs/v111/W7A_NOTES.md`、`W7B_NOTES.md` 全文（只编译未运行的测试清单、把握较小的断言、需要实看的画面、需要编辑器操作的事）。
- `docs/specs/v111/TEST_CHANGES.md`（改写登记，继续登记到新开的 W9a 一节）。
- Unity 的运行办法见下面“环境”。

## 要做的事（按顺序，每完成一项就提交并在备注里记进度）

1. **美术入库**（W8-05、W8-06 的工程部分）：
   - 按 `w8-install.json` 把 48 个源文件逐字节复制到目标路径（核对清单里的 SHA-256）；删除清单列的 C90、C128、C145 的 72 个旧文件（PNG 与 `.meta`）。优先扩展现有安装工具的做法，不要手工零散复制；做法与 W22i 不同的地方写进备注。
   - 在 Unity 里导入：新图的导入设置必须与同目录现有同类图片一致（默认插画、五套主题插画、六套紧凑图标各自的规则）；三张非正方形原图（swiss C154、sancai I13、glass I13）按 W22i 对非正方形图的处理。
   - 绑定：默认插画与图标的引用（`V09Setup.Integrate`、`V13CardSetup.BindCompactIcons`／`V22iArtSetup.Integrate` 一类的现有入口），主题插画走 `V11ThemeImport`。场景与资源里不能留下指向已删文件的悬空引用。
   - 裁切表与溯源表：把本批 24 个图标的裁切记录并入现行表，去掉三张已移除卡的条目；`tests/test_art_v11_crops.py`、`tests/test_art_v11_install.py` 回到全过（它们现在因为这三张卡与新内容而失败）。
   - 把钉着“184 张”的断言（W7a 备注 §4 列了 `GameplayPlayModeTests.cs:133–134`、`V13CardTemplatePlayModeTests.cs:441`、`W22iArtInstallPlayModeTests.cs:101`，再全仓搜一遍）按现行内容数改写并登记。现行内容数以配置为准（卡牌 150 张、道具 13 件等，自己数，不要照抄本文件）。
2. **字体图集重烘**：按 W21 的流程，为字表新增的字符重烘四套主题的标题字体与粗体图集；`V21TitleFontAssetTests`（含 `FW02`、`Q5`）通过。原有字形的字节或像素保持不变这类 W21 的既有检查要继续成立。
3. **EditMode 全量**：`V072BatchTests.RunEditMode`。全部通过。失败逐条归因：(a) 测试期望过时（按规格改写并登记）；(b) 界面或运行时代码的缺陷（修代码）；(c) 规则或会话层的缺陷——**不要改规则与会话的行为**，记进备注交协调者。新文件手写的 `.meta` 有冲突或格式问题的顺手修正。
4. **PlayMode 1920×1080 全量，然后 1280×720 全量**（窗口模式，见“环境”里的坑）。同样逐条归因与修复。W7a、W7b 备注里标了“把握较小”的断言重点看。修代码后受影响的测试必须重跑；两档窗口最后各有一次**完整的、零失败零跳过**的运行（修复之后重新跑的完整一轮，不是拼起来的）。
5. **实看画面**：
   - 以 1080p 和 720p 各跑一次 `V111InteractionsPlayModeTests`，得到 `artifacts/cache/v111/w7b/<宽>x<高>/` 下的截图。
   - 另外按 `W7A_NOTES.md` §6 与 `W7B_NOTES.md` 最后一节的“需要在 Unity 里实看的画面”清单，补齐没有被现有截图覆盖的画面（用 PlayMode 测试里截图的同一办法，或现有的夹具与录制工具），两档窗口、默认主题为主，新美术相关的画面六套主题各一张；字号 100% 与 150% 各一份的只做清单里点名的那些。
   - **你自己逐张看图**（用 Read 工具读 PNG），对照清单里“看什么”逐条写结论：通过 / 有缺陷（描述、位置、截图文件）。看得见的缺陷（重叠、截断、溢出、被遮挡、文字缩得过小、图标缺失、数字与规则不符）要修并重拍；属于审美取舍或交互取舍的记进备注留给协调者和用户，不要自己定。不要只凭“断言通过”判定画面没问题。
   - 新美术在商店卡、卡牌详情、紧凑槽里的实机样子（W8-06 的 D1、D2）：六套主题、两档窗口，与同主题邻卡并排。只做工程检查（可读、边缘干净、没有导入瑕疵、裁切没切掉主体）；风格好不好由用户定。
6. **收尾**：每次窗口模式运行后 `git checkout -- DicePrototype/Assets/Game/Fonts/DiceChineseFallback.asset`；不要留下 `InitTestScene*` 之类的临时文件；`git status` 里不应有本包之外的意外改动（项目设置被 Unity 自动改写的要还原并在备注里写明）。

## 范围外

- 规则与会话层的行为、配置数值、规格、黄金样例、模拟器。
- 发布门禁与交付脚本的版本切换（`Editor/PrototypeBuild.cs:104–106`、`tools/v11_delivery.py` 等）、正式构建、打包、发布：协调者在下一步做。可以用 `V072DevBuild.Build` 出一个不过门禁的开发构建来确认玩家包能启动，但不是必须。
- 新增玩法或界面功能。W7b 备注里列的 11 条“需要决定”的取舍不要自己定，保持现状。

## 环境与已知的坑（照做，能省很多时间）

- Unity 6000.6.7f1：`E:\unity\6000.6.7f1\Editor\Unity.exe`（PATH 里的 `unity.exe` 是 Hub，不要用）。工程在 `DicePrototype/`。
- EditMode 可以批处理：`-batchmode -projectPath DicePrototype -executeMethod DiceCraft.Editor.V072BatchTests.RunEditMode`（另有 `Discover`）。先读 `DicePrototype/Assets/Game/Editor/` 下 `V072BatchTests` 的源码确认参数与报告输出位置（NUnit XML 在 `evidence/` 下按时间命名；本版放 `evidence/v111/unity/`，如入口有固定目录参数就照它的）。
- **PlayMode 不能批处理**（`WaitForEndOfFrame` 不触发）：用 `tools/unity_windowed_tests.ps1` 启动窗口模式的 Editor 跑 `V072BatchTests.RunPlayMode -v072GameView 1920x1080`（或 `1280x720`，可加 `-v072TestFilter a,b`）。先读这个脚本。这台机器的终端是管理员权限，Editor 启动时会弹“Administrator Privileges Detected”对话框，脚本会点“Ignore Warning”；编译出错时脚本会停掉 Editor。完整一轮约 30–70 分钟：放后台跑、输出写文件，**后台命令约 60 分钟会被切断，长流程要拆开**。
- PlayMode 运行期间**不要同时跑任何重 CPU 的任务**（不要跑 .NET 宿主、模拟器、另一个 Editor），帧计时类的测试会失败。两次完整运行之间空 3–5 分钟。
- `PLAY_ART07_ResourceDiagnostic_IsolatedRealRouteInventoryAndTwentySecondIdle` 需要 Editor 窗口有焦点且 20 秒内没有鼠标键盘输入；如果它因为有人在用电脑而失败（报告里 `pointerChangedRows`／`inputRows` 大于 0），不是代码问题，重跑。`PLAY_ART07_FX_FastSourceMarksLowQualityReplayAndSkipReset` 对冷启动的帧时间敏感，单独过滤运行必然失败，要在完整一轮里看。
- 被强杀的 Editor 下次启动会弹“Scene Backup Detected”（选 Delete），并可能在 Assets 里留下 `InitTestScene*`。
- 测试会用反射按确切参数个数调用 `GameApp` 的一些私有方法（如 `DrawControllerFullBody07`、`Icon`、`QueueNotice07`），不要给它们加可选参数。
- TMP 的坑：粗体样式、缺字形的字体、每次重播都重建的文字上出现动态回退字形，会让 200 次重播测试失败；`Ellipsis` 文本的矩形矮于一行时什么都不画并报出全屏的文字边界。
- 720p 套件要求点击目标不小于 40 物理像素（60 参考单位）。
- NUnit 报告里不能有重名的完整测试名（数组参数的 `[TestCase]` 会重名），否则之后的发布门禁会拒收。
- .NET 宿主（规则、会话、表演）仍可用来快速验证纯逻辑，命令见 `docs/specs/v111/briefs/W3B1_SESSION_STAGE.md` 与 `W7A_NOTES.md` §4；它们写出的证据文件用完还原或删除。不要在 PlayMode 运行期间跑。
- Windows PowerShell 5.1（没有 `&&`、`||`）。git：`C:\Users\f\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe`。Python：`E:\anaconda3\python.exe`（需要调 git 的 Python 工具要把 git 目录加进 PATH）。
- 提交：分小块、每块自成一体；只 `git add` 自己改过的具体路径（工作区里有大量与你无关的未跟踪文件）；提交信息文件不带 BOM，`git commit -F`，最后一行 `Co-Authored-By: Claude Fable 5.2 <noreply@anthropic.com>`。保持文件原有行尾。不推送、不切分支。
- 省着用上下文：这个包很长。Unity 日志与 NUnit 报告只看汇总与失败项；截图一张一张看、看完就记结论。**每完成一项就提交并写备注**；用量或上下文不够时，如实报告做到哪一步、哪一轮测试的结果是什么，不要用“应该能过”代替实跑。

## 证据与备注

- NUnit 报告、运行绑定信息放 `evidence/v111/unity/`（照 V1.1 在 `evidence/v11/unity/` 的做法，先看那里有什么文件）。证据要能对应到提交号、窗口尺寸与运行时间。
- 备注 `docs/specs/v111/W9A_NOTES.md`：每一步做了什么；每轮测试的总数、通过、失败、跳过与报告文件；每一条失败的归因与处理；逐画面的看图结论与截图路径；留给协调者决定的事；(c) 类问题清单；Unity 自动改写并被还原的文件。

## 最后的报告（中文）

1. 提交清单；六项各自的完成情况。
2. 测试：EditMode、PlayMode 1080p、PlayMode 720p 最后一次**完整运行**的总数、通过、失败、跳过，对应的提交号与报告文件。有任何失败或跳过都要逐条列出，不许概括成“基本通过”。
3. 修了哪些代码缺陷（按画面归类）；改写了哪些测试。
4. 看图结论：有缺陷已修的、留给协调者或用户决定的（附截图路径）。
5. 分清运行验证过的事实与推断。
