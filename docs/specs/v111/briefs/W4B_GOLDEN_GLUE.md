# W4b 交接包：C# 计分与独立黄金样例的对拍

你是这个工作包的实施者，协调者是主会话里的 Claude。请完整读完本文件再动手。

## 背景

- 规则层（`Core/Rules`）已按 `docs/specs/v111/RULES_DELTA.md` 实现（W3a）。
- 独立方只按规格文本写了参考实现 `tools/v111_golden.py`，生成了 `DicePrototype/Assets/Game/Data/GoldenScoresV111.json`（2152 条样例：四个旧样例文件的输入在新规则下重算 1317 条，加新写的 835 条）。它的读法登记在 `docs/specs/v111/W4_OPEN_READINGS.md`，协调者已全部采纳（与 `W3A_NOTES.md` 里实现方的读法逐条对过，实质一致）。
- 两边到现在**从没有互相比对过**。本包就是做这次比对。它是检验新计分是否正确的关键一步，所以结论必须如实：对不上的地方不许靠改期望值或放宽比较来抹平。

## 要做的事

1. **新对拍测试** `DicePrototype/Assets/Game/Tests/EditMode/Rules/V111GoldenTests.cs`（连同 `.meta`；给 `GoldenScoresV111.json` 也补 `.meta`，格式照同目录其它 JSON 的 `.meta`，GUID 随机）：
   - 读样例文件，逐条把输入还原成 `SubmissionSnapshot`，调用 `ScoreCalculator.Evaluate`，逐字段比对期望输出。每条样例一个用例，用例名唯一且稳定（用样例编号），不要用数组参数的 `[TestCase]`。
   - 样例文件头的 `fieldNotes` 说明了字段；其中 `selfNamed` 列的是独立方自定名字的字段，C# 有对应量的就比（例如 `enchantBaseAdd`、`triggeredSourceIds`、`triggeredControllerIds`、`multiplierFactors`、红组的键与成员），确实没有对应量的记进备注、不比。除此以外的期望字段必须全部比对，不许悄悄跳过。空值写法的差异（空串、null、−1）在读入时归一化，并在备注里写明每一种归一化。
   - 另加覆盖断言：样例总数下限；每张数值或机制有变化的卡都有触发与不触发样例（规则照样例文件自己的分类）。
2. **旧的对拍测试改指新文件**。`RulesTests` 的 160 条 `GOLD-`、`ScoreExplanationTests` 的 160 条 `TRACE-`、`V09GoldenTests`、`V10GoldenTests`、`V11GoldenTests`（含它们各自的覆盖类断言）现在都对着旧文件、按旧规则断言：
   - 逐条数值比对由新测试覆盖同一批输入（样例编号前缀 `L08-`、`L09-`、`L10-`、`L11-`），旧的比对方法改为从新文件里取对应子集，或在确认新测试已逐条覆盖后改写为对新文件的断言。`TRACE-` 这一组检查的是计分说明与计分结果逐步一致，它要保留，改为在新文件的输入上运行。
   - 覆盖类断言按新规则改写（例如“每张计分卡有两张同名触发的样例”改为对新文件成立的等价断言）。
   - 不删除测试方法；每一处改写登记到 `docs/specs/v111/TEST_CHANGES.md` 新开的 W4b 一节。
3. **删掉过渡别名**：`ScoreBreakdown.colorMult100`（W3a 为了让旧黄金测试编译留的只读属性）。界面与运行时里读它的地方做编译适配（改读 `colorAdd100`，显示逻辑不要求正确，位置记进备注留给界面工作包）。
4. **旧样例文件的去向**：`Data/GoldenScores.json`、`GoldenScoresV09.json`、`GoldenScoresV10.json`、`GoldenScoresV11.json` 的期望值是旧规则的。把它们（连同 `.meta`）用 `git mv` 移到 `tools/balance_sim/baseline/`，文件名加规则版本后缀（例如 `GoldenScoresV10.p1.1.0.json`；已有同内容的基线副本就不重复放，直接删游戏目录里的那份并在备注里写明对应的基线文件）。然后修正所有引用：`tools/v111_golden.py` 读旧输入的路径**不要由你改**（见下面的约束），在备注里列出需要独立方改的行；其它引用（C# 测试、`tests/*.py`、旧的 `tools/v*_golden.py` 与 `tools/v*_config.py` 的 `--check`）由你改到新位置或冻结的基线配置上。
5. **Python 测试套件**：`E:\anaconda3\python.exe -m unittest discover -s tests`（可能要跑十几分钟，放后台，输出重定向到文件，只看汇总）。先在动手前跑一遍记基线，结束时再跑。因为本版改了游戏配置和样例文件而失败的旧测试，改成对冻结基线（`tools/balance_sim/baseline/P0Config.p1.1.0.json` 等）成立的断言并登记；`tests/test_opening_dp.py` 里那条 BX-03 的失败（84.68% < 85%）是已知的待用户决定事项，**保持失败，不要动**。
6. **对不上的样例怎么处理**（最重要）：
   - 逐条归因，三类：(a) C# 违反规格正文——改 `Core/Rules` 的实现，改完重跑规则宿主与会话宿主，并在备注里写明规格依据；(b) 规格有两种说得通的读法、两边各取了一种——**两边都不改**，记进备注交协调者裁定；(c) 样例违反规格正文——**不改样例、不改生成器**，记进备注，由协调者交回独立方。
   - 你现在同时看得到两边，已经不是独立方。所以 `tools/v111_golden.py`、`Data/GoldenScoresV111.json`、`evidence/v111/rules/golden-provenance.json`、`tests/test_v111_golden.py` 一个字都不许改。
   - 归因必须引用规格的具体小节和句子。拿不准属于 (a) 还是 (b) 时按 (b) 处理。

## 范围外

- 会话层的新行为（商店、道具、解锁、存档版本）；界面新功能；平衡模拟器；Unity 编辑器。
- 改规格、改设计数值、改卡面文字。

## 环境、提交、备注

- Windows PowerShell 5.1（没有 `&&`、`||`）。git：`C:\Users\f\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe`。Python：`E:\anaconda3\python.exe`。.NET SDK 10 在 PATH。
- 规则宿主：`dotnet run --project DicePrototype/Assets/Game/Core/Rules/Validation~/RulesNUnitValidation.csproj -c Release`（会改写被跟踪的 `evidence/v05/rules/nunit-managed-report.json`，提交前还原）。会话宿主：`dotnet run --project DicePrototype/Assets/Game/Core/Session/Validation~/SessionValidation.csproj -c Release`（它会在 `evidence/v05/session/` 下生成未跟踪的 `report.json`，用完删掉）。基线：规则宿主 1825 条里 691 通过；会话宿主 516 条里 497 通过，剩 19 条属于下一个工作包，不要动。
- 样例文件约 9.4 MB：只用程序读，不要把内容打印进对话；看失败时只打印前若干条的编号与差异字段。
- 同一个检出里 Codex 在 `ArtSource/`、`tools/art_v11/`、`artifacts/` 下做美术，不要碰；`ArtSource/Art11/subjects.json` 的未提交改动不是你的，不要提交它。
- 分小块提交，只 `git add` 自己改过的具体路径，提交前 `git status` 确认暂存区干净。提交信息用 `git commit -F`，最后一行加 `Co-Authored-By: Claude Fable 5.2 <noreply@anthropic.com>`。不推送、不切分支。
- 省着用上下文：不要整本重读大文件；宿主输出只看汇总与失败清单。剩余工作量超出一次能做完的范围时，先提交已完成的部分并写好备注。
- 备注文件 `docs/specs/v111/W4B_NOTES.md`：字段映射与归一化；每一条对不上的样例的归因（编号、字段、两边的值、规格依据、处理）；需要独立方改的地方；留给界面工作包的位置。

## 最后的报告（中文）

1. 提交清单。
2. **对拍结果**：2152 条里首次运行通过多少、失败多少；失败按 (a)(b)(c) 各多少条、各涉及哪些卡或规则；你改了哪些 C#，改完后通过多少。仍然失败的逐类列出，等协调者裁定。
3. 规则宿主、会话宿主、Python 套件的基线与最终数字；非预期失败必须为零，否则说明。
4. 没有比对的字段清单及原因。
5. 分清运行验证过的事实与推断。
