# W4 交接包：p1.1.1 独立参考实现与黄金样例

你是独立方。你的参考实现是用来和游戏的 C# 计分互相检验的，所以**独立性是这个工作包存在的全部理由**。协调者是主会话里的 Claude。请完整读完本文件再动手。

## 独立性规则（硬性）

- **不许读** `DicePrototype/Assets/Game/Core/**`、`DicePrototype/Assets/Game/UI/**`、`DicePrototype/Assets/Game/Runtime/**`、`DicePrototype/Assets/Game/Tests/**`、`tools/balance_sim/*.cs`，也不许读 `docs/specs/v111/W3A_NOTES.md`、`docs/specs/v111/TEST_CHANGES.md`。不要用搜索工具在这些路径里查任何东西。
- 期望值只能来自规格文本和配置数据。不许运行游戏代码或它的测试宿主来“看看它算出多少”。
- 规格有歧义或算不下去时：**不要去看实现**。把问题写进 `docs/specs/v111/W4_OPEN_READINGS.md`（问题、你采用的读法、理由、受影响的样例编号），按你认为最贴合规格文字的读法继续。协调者会裁定；规格改了以后你再重生成。

## 输入（可以读）

| 文件 | 用途 |
| --- | --- |
| `docs/specs/v111/RULES_DELTA.md` | p1.1.1 规则增量，本包的主要依据。§14 列了最小覆盖，§15 的“协调者裁定”以裁定为准 |
| `docs/specs/v11/RULES_DELTA.md`、`docs/specs/v10/RULES_SPEC.md`、`docs/specs/v09/RULES_SPEC.md`、`docs/specs/v08/RULES_DELTA.md`、`docs/specs/P0_v0.6.md` | 被沿用的旧规则 |
| `DicePrototype/Assets/Game/Data/P0Config.json`（p1.1.1）与 `tools/v111_config.py` | 数值。配置版本不是 p1.1.1 时停下来报告 |
| `tools/v10_golden.py`、`tools/v11_golden.py`、`tools/v09_golden.py`、`tools/v08_golden.py`（如果有） | 上一代独立参考实现，同样是只按规格写的。可以复制其中的计分逻辑作为起点，再按 p1.1.1 规格改 |
| `DicePrototype/Assets/Game/Data/GoldenScores.json`、`GoldenScoresV09.json`、`GoldenScoresV10.json`、`GoldenScoresV11.json` | 旧样例。**只用它们的输入**（提交快照）来保证覆盖面不缩水；它们的期望值是旧规则算的，不能沿用 |
| `tests/test_v11_golden.py` | 上一代样例的 Python 测试写法 |

## 交付

1. `tools/v111_golden.py`：独立参考计分器加样例生成器，命令 `verify | write | check`（含义同 `tools/v11_golden.py`）。
   - 计分器按规格 §2 的十步实现 p1.1.1 的完整计分：加成区、颜色与红组新表、等级上限 12、全部卡牌的计分侧规则（含四张换机制的卡、三张新卡、成长值读取与追赶、返璞归真减半）、黄金面加成、倍率附魔加成、千年老二惩罚、牌型减半的百分单位整除、最终一次向下取整。
   - 计分后计划里属于“冻结快照就能确定”的部分也要算：成长计划（含连庄的 `halve`）、改写计划（规格 §6.1 中由提交触发的卡）、扣款、玻璃风险名单、牌型升级请求。
2. `DicePrototype/Assets/Game/Data/GoldenScoresV111.json`：样例文件。
   - 旧四个文件的每一个输入都要在新规则下重算并收进来。输入里引用了已移除的卡（C90、C128、C145）、被换机制的卡依赖的旧字段、或旧版才有的字段时：能按规格改写成合法的 p1.1.1 输入就改写并在样例里记下 `derivedFrom`；不能就剔除，并在来源记录里逐条列出剔除原因。
   - 新增样例覆盖规格 §14 表里“十步计分”“随机和成长”两行中属于纯计分函数的全部条目，以及规格 §2–§7 正文里每一个属于计分的“例”。每张数值或机制有变化的卡至少一个触发样例和一个不触发样例；同名叠加、百变模仿、失效各至少覆盖到每一类卡（加成、乘法、基础分、成长）。
   - 每个样例：编号、一句中文说明、完整输入（沿用旧样例的提交快照结构，新增字段按规格 §12.1 的名字）、期望输出（规格 §12.2 列出的字段加沿用的旧字段）。字段名以规格为准；规格没给名字的，自己定一个并写进文件头的 `fieldNotes`。
   - 涉及随机的卡（错乱乘积）用输入里固定好的结果；客串明星用输入里的 `starPip`。
3. `evidence/v111/rules/golden-provenance.json`：规格与配置文件的 SHA-256、样例总数、按来源（旧四个文件各多少、新增多少）与按卡的覆盖计数、被剔除的旧输入清单。
4. `tests/test_v111_golden.py`：`check` 一致性、样例总数下限、覆盖断言（每张变动卡都有触发与不触发样例）、若干条手算断言（规格里的数值例子直接写成断言，至少包含：§2 的 1200 例与 274 例、§3.1 的 3 级全蓝与 12 级全红、§3.2 的红组例、§4 表里十条以上卡牌例、§5 的连庄 ×1.75 → ×1.60 → ×1.52 例与返璞归真 +13 例、§12.2 的 69 例、§14 的 3060 / 1530 例）。
5. 如果规格的某个“例”按规格正文算不出它写的数，这是规格的错误：记进 `W4_OPEN_READINGS.md` 并在报告里置顶，**不要让样例迁就那个数**。

## 不在范围内

- 不改任何游戏代码、C# 测试、旧的四个样例文件、配置、生成器、规格。
- 整局事件类的样例（商店、解锁命令、存档恢复，规格 §14 表的后三行里依赖会话状态机的部分）不在本包，后续另派。

## 环境与提交

- Windows PowerShell 5.1（没有 `&&`、`||`）。Python：`E:\anaconda3\python.exe`。git：`C:\Users\f\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe`。
- 仓库测试：`E:\anaconda3\python.exe -m unittest tests.test_v111_golden -v`。
- 分小块提交，只 `git add` 你自己的四个交付路径和 `W4_OPEN_READINGS.md`；提交信息写到临时文件用 `git commit -F`，最后一行加 `Co-Authored-By: Claude Fable 5.2 <noreply@anthropic.com>`。不推送、不切分支。提交前先 `git status` 确认没有把别人的改动带进去（同一个检出里可能有另一个实施者在改 `Core/` 与 C# 测试）。

## 最后的报告（中文）

1. 提交清单；四个交付各自的规模（代码行数、样例数）。
2. 样例来源统计与剔除清单的摘要。
3. `W4_OPEN_READINGS.md` 的条数，最需要协调者裁定的几条放前面；规格里算不对的“例”单独列出。
4. `verify`、`check` 与 `tests.test_v111_golden` 的实际运行结果。
5. 一句声明：你有没有读过独立性规则禁止的任何路径。如果误读了，说明读了什么、可能污染了哪些样例。
