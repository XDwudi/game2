# W7a 交接包：界面显示跟上 p1.1.1（现有界面改对）

你是这个工作包的实施者，协调者是主会话里的 Claude。请完整读完本文件再动手。

## 目标

规则层与会话层已经是 p1.1.1（计分 = `floor(B × H × R × (1 + A) × M × G)`，颜色、黄金面、加成卡、倍率附魔都进“加成”合计）。界面与表现层目前只做了编译适配，**很多地方还按旧公式显示，数字与实际得分对不上**。本包把**现有界面**改对：玩家看到的每个数字、每句说明都与 p1.1.1 规则一致。

新的交互（自选改造面板、一次选两个面、钥匙串与解锁骰子、结算页纪录、主菜单历史最高、得分演出分档）是下一个包（W7b），本包不做，但不要挡它的路。

## 必读

- `docs/specs/v111/RULES_DELTA.md`：§2（公式）、§3（颜色与等级）、§4–§6（卡牌）、§7（骰面与玻璃）、§12.2（计分输出字段）、**§13（玩家界面与回执的契约）**。
- 三份实现备注里留给界面的清单，逐条处理：
  - `docs/specs/v111/W3A_NOTES.md` §2.3（含“编译不报错但显示已经不对”的三条）。
  - `docs/specs/v111/W3B1_NOTES.md` §3（查询、回执变化、“界面里已经与规则不符”的三条）。
  - `docs/specs/v111/W4B_NOTES.md` §8（8 处 `W4B-ADAPT`）。
- `docs/specs/v11/` 下与文字、版式有关的现行约定（卡面文字规范、术语表、两档字号、六套主题），以及 `AGENTS.md` 的协作规则。先搜一下 V1.1 的文字方案与检查脚本（`tools/` 下名字含 `text`、`glossary`、`linebreak`、`title_chars` 的脚本和 `tests/test_v21_*`），弄清楚改了玩家可见文字之后要重生成哪些产物。

## 要做的事

1. **得分明细与筹码**（§13 第一行）：
   - 计分说明 `ScoreExplanation` 已经给出一行 `kind=additive` 的“加成 ×(1+A)”和加成条目。得分抽屉、对局内因子列表、结算公式串、最高分公式串都改为：基础分、牌型、红组、**加成**（一项，可展开看每条来源与数额；A 为 0 时仍显示 ×1）、各乘法卡、道具、首领系数。原“颜色 ×”“附魔 ×”“黄金翻倍”三个独立项移除，并入加成的展开条目。
   - 所有显示的因子连乘必须等于实际得分（取整前）。`W4B-ADAPT` 标记的 8 处与 W3a 备注列的各处都按这个要求改，改完删掉标记。
   - `PresentationReceiptAdapter` 与 `Art07Results` 的总倍率照 W3a 备注的口径核对一遍。
2. **卡面与成长值**（§13 第二行）：
   - 成长卡的当前值统一用 `ControllerRules.GrowthValue(cfg, id, state, halved)`，按 `IsGrowthAdditive`／`IsGrowthMultiplier`／`IsGrowthBase` 分别显示为“加成 +X”“整手得分 ×N”“基础分 +N”。商店报价上的起步值含追赶值（`offer.controller.state.growthCatchup`）。旧的 `(20 + growth)/20` 写法全部去掉。
   - C141、C63、C137（“本关明星点数”，无值时显示待入关；模仿显示复制目标的值）、C156、C155 等动态文字按规则核对。
   - 颜色升级、图鉴颜色行改成“加成 +X → +Y”（读 `baseAdd100`、`addPerLevel100`、`currentAdd100`），不再显示“倍率 ×”。等级上限 12。
   - 倍率附魔的提示从“×2”改为“加成 +1.5”（数值读配置）。
   - 养成卡：养成标签、首张免栏位的标识（`ControllerRules.FreeCultivationInstanceId`）、实际回收价（`GameSession.ControllerSalePriceIn`）、已改面数。
   - 黄金面说明改为“加成 +0.5”（读配置）；镀金说明带上“该面权重 +1（上限 5）”。
3. **玻璃**：文案改为“提交后每颗有 X% 概率碎裂”（X 读 `GlassBreakPercentIn`）；投掷不再碎。提交预览里标出本手会被判定的玻璃骰（`afterScorePlan.glassRiskDieIds`）若现有界面有合适的位置就做，没有就记进备注留给 W7b。
4. **回执与演出适配**：认识 `ControllerGrowthHalved`（连庄换牌型减半，原 `ControllerGrowthReset` 的位置）、`FaceCultivated`、`StarPipDrawn`、提交回执里的 `DieBroken`；清掉已成死代码的分支（`RandomUpgradeMissed`、`DieGlassed`、`C115Die`）。新回执没有专门演出的，至少不报错、不丢后续演出，并在日志或既有的文字提示通道里有一条说明。
5. **已移除与新增内容的收尾**：界面、图鉴、调试候选里不再出现 C90、C128、C145；C154、C155、C156、I13 能在图鉴、商店、背包里正常显示文字（插画与图标未入库前用现有的缺图占位逻辑，不要自己造图）。I13 图标上显示剩余次数（`OwnedItem.usesRemaining`）属于 W7b，但别让它显示出错。
6. **测试**：
   - `Tests/EditMode/Presentation/` 与 `Tests/PlayMode/` 里因规则变化而过时的断言按规格改写（W3b-1 备注列了投掷时断言 `DieBroken` 的几处、钉着旧玻璃文案的一处；`PresentationTests` 的 160 条 `ART07_RECEIPT_`），登记到 `docs/specs/v111/TEST_CHANGES.md` 新开的 W7a 一节。不删测试、不降阈值。
   - 为“显示的因子连乘 = 实际得分”加 EditMode 测试：在 `GoldenScoresV111.json` 的全部输入上，逐条检查得分抽屉／公式串用到的数据模型（不是像素）乘出来等于 `scoreNumerator / scoreDenominator`。
   - 你不能运行 Unity。用 `dotnet build DicePrototype/Assets/Game/Core/Rules/Validation~/UnityTestsCompilation.csproj` 确认界面、运行时与测试都能编译（基线是 5 个与本包无关的既有错误，不能新增）；能放进 .NET 宿主跑的纯逻辑测试尽量放进去跑。哪些测试只编译过、没有运行，在报告里列清楚，协调者会在 Unity 里跑。
7. **文字产物**：改了玩家可见文字后，按现行流程重生成字符表、换行保护词等产物，让 `tests/test_v21_title_chars.py`、`tests/test_v21_linebreak_words.py` 回到通过（它们现在因为文字变化而失败）。卡牌的 `shortText`／详情文字在 `tools/v111_config.py` 里，**你不要改这个文件和 `P0Config.json`**；发现卡面文字有问题（超长、与规则不符、术语不一致）列进备注，由协调者改。

## 范围外

- W7b 的新交互与新页面元素；美术入库；Unity 编辑器操作（场景、Prefab、项目设置一律不动）。
- `Core/Rules`、`Core/Session` 的行为。确实缺查询接口时，可以在会话层加**只读**查询并补会话测试，在备注里写明；不许改规则。
- `tools/v111_config.py`、`P0Config.json`、黄金样例、`tools/balance_sim/`（协调者正在用模拟器校准目标分，期间会改配置里的目标分并重生成 `P0Config.json`，所以测试里不要钉死第 4–24 关的目标分数值）。
- 改规格或设计。

## 环境、提交、备注

- Windows PowerShell 5.1（没有 `&&`、`||`）。git：`C:\Users\f\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe`。Python：`E:\anaconda3\python.exe`。.NET SDK 10 在 PATH。
- 规则宿主与会话宿主的命令、要还原或删除的证据文件见 `docs/specs/v111/briefs/W3B1_SESSION_STAGE.md` 的同名小节。基线：规则宿主 3982 条、会话宿主 616 条全部通过，结束时必须仍然全过。
- 协调者同时在同一个检出里提交（`tools/balance_sim/`、`tools/v111_config.py`、`P0Config.json`、`docs/`、`evidence/v111/balance/`）。你只 `git add` 自己改过的具体路径，提交前 `git status --short -- DicePrototype tools tests docs` 看清楚；遇到 `index.lock` 等几秒重试。提交信息文件不要带 BOM（用 `[IO.File]::WriteAllText(path, text, (New-Object Text.UTF8Encoding($false)))`），`git commit -F`，最后一行 `Co-Authored-By: Claude Fable 5.2 <noreply@anthropic.com>`。不推送、不切分支。
- 省着用上下文：界面文件很多很大，先用搜索定位再分段读。工作量超出一次能做完的范围时，先提交已完成的部分并把进度写进备注再继续。
- 备注文件 `docs/specs/v111/W7A_NOTES.md`：改了哪些界面位置（按玩家看到的界面归类）；每类显示的数据来源；没有专门演出的新回执怎么处理的；卡面文字问题清单；只编译未运行的测试清单；留给 W7b 的事；需要在 Unity 里实看的画面清单（哪一屏、看什么）。

## 最后的报告（中文）

1. 提交清单。
2. 三份清单（W3a §2.3、W3b-1 §3、W4b §8）逐条的处理结果：已改 / 留给 W7b / 不需要改（理由）。
3. 测试：两个宿主与编译检查的数字；新增与改写的测试；只编译未运行的清单。
4. 卡面文字问题与需要协调者决定的事。
5. 分清运行验证过的事实与推断。
