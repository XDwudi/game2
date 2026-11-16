# 文字 P0 改动记录（W09）

| 项 | 内容 |
| --- | --- |
| 任务 | DEV_PLAN §5.2 W09：倍率改小数与统一数字格式、去内部编号和版本号、英文选项改中文、文字方案 §5 的界面侧错误、取消旧存档界面 |
| 基线 | `codex/v1.1` 工作区，HEAD `f12d633f`（含规则组 Core 文字合并 `567ee14a` 与 D5 卖价封顶合并 `a61a762a`）；改动未提交，由协调者审阅后提交 |
| 依据 | “方案” = `docs/proposals/2026-11-16-text-readability.md`；“界面附录” = `docs/proposals/2026-11-16-text/UI_REWRITES.md`；`docs/specs/v11/DEV_PLAN.md`（D2、D5、§4.1、§4.5）；`docs/specs/v11/RULES_DELTA.md` §1.1 第 9 条、§8.3、§10 |
| 改动范围 | `UI/**`（不含 `UI/Theme/*`、`GameApp.Theme11.cs`、`UiKit.cs`）、`Runtime/**`（不含 `AtomicSaveStore.cs`）、`Tests/PlayMode/**`、本文件 |
| 没有改 | Core、EditMode 测试、配置、工具、其他文档、`docs/STATE.md`（按任务的文件所有权） |
| 状态 | 实施完成，等待复核。只做了编译检查；Unity 没有启动，PlayMode 测试**未运行**（见 §10） |

文中行号都是改动后的文件行号；路径省略 `DicePrototype/Assets/Game/`。

## 0 摘要

1. 新增 `Runtime/PlayerNumberFormat.cs`，玩家看到的倍率、分数、金币、概率都经过它。分数形式（702/5、39/10）和 “≈1.23e15” 不再出现。
2. 骰子、骰面、卡牌不再用编号显示，统一为“骰子 3”“第 2 面”“骰子 3 的第 2 面”“第 2 张”，名字放在 `UI/GameApp.Text11.cs`。版本号、构建号、美术方案名和英文选项、英文按键名都去掉了。
3. 文字方案 §5 第 2–7 处在界面侧改完，每处有一个 PlayMode 测试。第 1 处在 Core，规则组已经改好（§8）。
4. 旧存档的菜单按钮、说明弹窗和“保留备份并开始新局”都已删除，界面不再读 `AtomicSaveStore.LastIncompatibleSave`。当前版本的“存档读取说明”和“存档恢复说明”保持原样。
5. 改动过的字符串用新用语：卡牌、栏位、提交栏 N 格、生效 N 张、金币、本局、指定升级。没有做全量术语替换。
6. 用户决定 D5（本店买入的卡离店前卖价不超过买价）带来的两处显示修正：买入后的摘要和“本实例基金”说明（§2a）。

## 1 数字格式（TX-01）

### 1.1 统一函数 `Runtime/PlayerNumberFormat.cs`

| 函数 | 规则 | 例 |
| --- | --- | --- |
| `Ratio(n,d)` | 最多 2 位小数，去掉末尾的 0；四舍五入后不精确时前加“≈”；整数部分四位以上加千分位；负号用“−” | 702/5 → 140.4；39/10 → 3.9；1/3 → ≈0.33；38961/10 → 3,896.1 |
| `Times(n,d)` | 倍率加“×”，“≈”放在最前 | ×140.4、≈×0.33 |
| `Integer`、`Gold` | 整数加千分位 | 1,000,010 |
| 大数 | 整数部分超过 `fullDigits`（默认 15 位）时写成“约 1.23×10¹⁹”（三位有效数字）。指数用 TMP `<sup>`，因为字库 Noto 没有上标 0、5–9 | 10¹⁵ → 约 1×10¹⁵ |
| `Unlimited` | 完整明细不缩写、不用标记，保留全部数位（明细的文本检查逐字比对渲染结果） | 12,345,678,901,234,567,890 |
| `Percent(n,d)` | 概率 1 位小数，去末尾 0 | 16.7%、0%、100% |
| `Exact(s)` | 规则字符串 “n” 或 “n/d” 转成上面的格式；颜色、列表等非数字原样返回 | “red” → “red” |

各处的整数位上限：倍率片 6 位（150% 字号下框内放得下“×”加约 10 个字宽），基础分片和横幅算式 9 位，横幅倍率 12 位，其余 15 位，明细不限。

### 1.2 改动

| 位置 | 原文 | 新文 | 依据 |
| --- | --- | --- | --- |
| Runtime/GameApp.cs:384 `Factor` | `(n/100m).ToString("0.##")` | `PlayerNumberFormat.Ratio(n,100)` | 方案 §3 |
| Runtime/GameApp.cs:386 `Big` | 整数加千分位 | 小数，保留全部数位 | 界面附录 §3.1 |
| Runtime/GameApp.cs:388 `CompactScore` | 123456…（20位） | ≤15 位完整加千分位；更长写“约 1.23×10¹⁹” | 方案 §3；界面附录 §3.3；DEV_PLAN §4.1 第 10 项 |
| UI/GameApp.Art07.cs:479 `ScoreFactorShort07` | 702/5；≈1.23e15 | 140.4；整数超过 9 位写“约 1.23×10⁹” | 界面附录 §3.1 |
| Art07.cs:453 卡牌倍率片 | 控制器 ×702/5；控制器 ×≈4.29e9 | 卡牌 ×140.4；卡牌 约 ×4.29×10⁹ | 界面附录 §2.4；D2 |
| Art07.cs:454 基础分片、附魔片 | 基础 ≈1.23e15；附魔 ×2^3 | 基础 1,234,567（超过 9 位用“约”写法）；附魔 ×8（附魔实际倍数） | 界面附录 §2.4 |
| Art07.cs:203；Art07Shop.cs:102 周转（C51） | 当前 ×1.5（`0.#`） | 同样的显示，改走统一函数 | 方案 §3 |
| Art07.cs:492、512–513 错乱乘积预览 | 范围用 `Big`；分支分数原样（无千分位）；q ×1.5 | 范围与分支分数用 `CompactScore`；q ×1.5 | 界面附录 §3.3 |
| Art07Effects.cs:272 `DisplayFraction07` | 只把“/2”改成“.5”，其余保留分数 | 任何分数都改成小数 | 界面附录 §3.1 |
| Art07Effects.cs:274–275 `OperationText07`（新） | ×39/10 | ×3.9；≈×0.33 | 界面附录 §3.1 |
| Art07Effects.cs:135、145、152、161、249 价值票、随机揭示、现金票 | ×39/10；随机 ×3/2 → ×1.5 | 一律小数 | 界面附录 §2.7 |
| Art07Effects.cs:370、393 现金票 | +1234 | +1,234 | 方案 §3 |
| Art07Presentation.cs:169–171 `ReceiptMultiplier07`（新）、258 | 总倍率 ×702/5 | 总倍率 ×140.4 | 界面附录 §2.7 |
| Art07Presentation.cs:200 | 已提交随机结果 ×1.5（经旧 `DisplayFraction07`） | 已提交随机结果 ×1.5（统一函数） | 界面附录 §3.1 |
| Art07Presentation.cs:219–222 `EventText07` | 牌型 ×9/2；蓝色价值 ×39/10 | 牌型 ×4.5；蓝色价值 ×3.9 | 界面附录 §2.7 |
| Art07Presentation.cs:237 | 原始值 | 小数 | 界面附录 §3.1 |
| Art07StableResult.cs:52 | 总倍率 ×702/5 | 总倍率 ×140.4 | 界面附录 §2.4 |
| Art073HighScore.cs:77 横幅倍率 | ×16560；×702/5 | ×16,560；×140.4（12 位以内完整） | 界面附录 §2.7 |
| Art073HighScore.cs:81、118–123；Runtime/Presentation/HighScore073.cs:49–67 横幅算式 | 111 × 8 × 4.5 × 39/10（附魔写 2^3） | 基础 111 × 牌型 8 × 颜色 4.5 × 卡牌 3.9 × 附魔 8 × 灵光一闪 2（×1 的项照旧省略；道具与首领系数跟在附魔后） | 方案 §1 第 1 处；界面附录 §2.7 |
| Art07ScoreDrawer.cs:81 `ScoreFormula07` | 基础 111 × … × 控制器 702/25 × 2^0 × 灵光一闪 2 | 基础 111 × … × 卡牌 28.08 × 附魔 1 × 灵光一闪 2 | 界面附录 §2.7 |
| Art07ScoreDrawer.cs:60、64、83–84；Modals.cs:366–367 计分步骤 | 222 × 9/2 → 999/2；38961/10 | 222 × 4.5 → 499.5；3,896.1 | 界面附录 §2.7 |
| Modals.cs:352 牌型加成注释 | +0.5（`0.##`） | 同样的显示，改走 `Factor` | 方案 §3 |
| Modals.cs:371；Controllers.cs:28、30–31、56 成长倍率 | ×1.25（decimal 格式） | 同样的显示，改走 `Times` | 方案 §3 |
| V10.cs:74 `GlobalFactorText10` | × 首领减益名 1/2 | × 首领减益名 0.5 | 界面附录 §3.1 |
| Art07Results.cs:54 最高倍率 | ×702/5 | ×140.4 | 界面附录 §2.8 |
| Modals.cs:285–286 概率表 | 16.6667% | 16.7% | 方案 §3；界面附录 §3.2 |
| Art073DiceLibrary.cs:68 | 本面概率 16.67% | 本面概率 16.7% | 界面附录 §3.2 |
| V05.cs:165、168 错乱乘积详情 | 1.5；×1.5（`0.#`） | 同样的显示，改走统一函数 | 方案 §3 |
| 金币：Art07.cs:73；Art07Wallet.cs:20；Art07StatusDetails.cs:74、86；Art07Settlement.cs:72、74、76、79、87；Art071Screens.cs:56；Art07NewRun.cs:77；Modals.cs:250、272；Shop.cs:141 | 1234 | 1,234 | 方案 §3；界面附录 §3.3 |
| Art07Wallet.cs:26–33 钱包数字框 | 304 宽，字距最低 −4 | 312 宽，字距最低 −8（150% 字号下十位数加三个逗号） | 为千分位腾位置 |
| Runtime/Verification/PlayerVerification.V06.cs:341–342 | 概率核对用 `0.####` | 用 `PlayerNumberFormat.Percent` | 与界面同一格式 |

## 2 内部编号与开发信息（TX-02）

新增的命名函数在 `UI/GameApp.Text11.cs`：`DieName11`（D006 → 骰子 6）、`FaceName11`（F02 → 第 2 面）、`DieFaceName11`、`CardPosition11`（第 N 张）、`CardLabel11`（卡名；同名有多张时加“（第 N 张）”；已不在手里时写“一张卡牌”）、`MissingIconLabel11`、`SpeedLabel11`、`KeyLabel11`。

| 位置 | 原文 | 新文 | 依据 |
| --- | --- | --- | --- |
| Art07.cs:64 左栏副标题 | 复古印刷骰桌（练习时为“调试对局”） | 普通对局留空；练习写“练习模式”；调试工作区仍是“调试对局” | 方案 §1 第 14 处；界面附录 §2.4、§2.15 |
| Art071Screens.cs:63 菜单 | p1.0.0 / art0.7.3⏎构建 c7d93792 | 删除；只在“版本与构建”页显示 | 方案 §1 第 14 处；界面附录 §2.1 |
| Art07Settings.cs:126；Text11.cs:35 演出速度 | Instant / Fast / Normal | 即时 / 快速 / 标准（存的值仍是英文键） | 方案 §1 第 13 处 |
| Art07Settings.cs:82；Text11.cs:38–74 快捷键 | Enter、Backspace、Space | 回车、退格、空格、小键盘键 | 界面附录 §2.13、§3.4 |
| Display.cs:75 | 显示器：\\.\DISPLAY1 · 2560 × 1440 | 显示器 1 · 2560 × 1440 | 界面附录 §2.13 |
| Runtime/DisplaySettings.cs:99、137、144 | 显示设置未应用：`<异常>` 等三处 | 显示设置未应用：无法写入设置文件。／显示已恢复，但设置没能保存（无法写入设置文件）。／确认没能保存（无法写入设置文件）。异常写入日志 | 界面附录 §2.13 |
| Modals.cs:163 骰库按钮 | D001 | 骰子 1 | 界面附录 §2.11 |
| Modals.cs:168 | 活动骰 5 · 永久骰 5⏎物理对面 F01/F06 · F02/F05 · F03/F04 | 骰子 5 颗（永久 5） | 界面附录 §2.11；方案 §2.1 |
| Modals.cs:215 | 对面 F06 当前： | 对面（第 6 面）当前： | 界面附录 §2.11 |
| Modals.cs:219 | F02 · 永久属性 | 第 2 面 · 永久属性 | 界面附录 §2.11 |
| Modals.cs:258 | … · 提交容量保持 K=5 | … · 提交栏仍是 5 格 | 界面附录 §2.11 |
| Modals.cs:260、291 | F01 | 第 1 面 | 界面附录 §2.11 |
| Modals.cs:275–276 `ColorName07` | 红 R／蓝 B | 红／蓝 | 界面附录 §2.11 |
| Modals.cs:314；Art07ScoreDrawer.cs:55–56 | 1. D001/F03 … / 蓝 B | 1. 骰子 1 的第 3 面 … / 蓝 | 界面附录 §2.7 |
| Modals.cs:352–361 计分来源 | [ITEM_00003 → ITEM_00005] | （第 3 张，复制第 4 张） | 界面附录 §2.7 |
| Modals.cs:378–383 永久效果 | X：D002 的六面复制到 D003；X：D001 / F02 永久染蓝；永久点数：D001 / F02 3 → 4；D001 玻璃损坏… | X：骰子 2 的六面复制到骰子 3；X：骰子 1 的第 2 面永久染蓝；永久点数：骰子 1 的第 2 面 3 → 4；骰子 1 玻璃损坏… | 界面附录 §2.7 |
| Modals.cs:387 | 飘忽不定：下一颜色 ITEM_00003:red | 飘忽不定：下一颜色 红 | 界面附录 §2.7 |
| Modals.cs:388 | 玻璃大炮：ITEM_00007 本次计分后自毁 | 玻璃大炮：本次计分后自毁 | 界面附录 §2.7 |
| Modals.cs:398 未知效果 | X · DieDyed（英文类型名） | X：状态已更新 | 方案 §2.3 |
| Modals.cs:438、444 遥控 | D003；目标 1 · D003 | 骰子 3；目标 1 · 骰子 3 | 界面附录 §2.11 |
| Art07ScoreDrawer.cs:18；Modals.cs:328 | 分数来源 · 三列抽屉；返回三列抽屉 | 得分明细；返回得分明细 | 界面附录 §2.7 |
| Art07ScoreDrawer.cs:70、73；Modals.cs:330、333 | 复制精确公式／已复制精确公式；复制精确分数与公式／已复制精确分数与公式 | 复制公式／已复制（复制的就是显示的小数算式，不再“精确”） | 界面附录 §2.7 |
| Art07Presentation.cs:115 | … · 目标 D001、D003 | … · 目标 骰子 1、骰子 3 | 界面附录 §2.7 |
| Art07Effects.cs:98 来源兜底 | 实例 ID | 卡名，或“来源未记录” | 界面附录 §2.7 |
| Art07Effects.cs:485、514 | 已装备 蓝色价值 · ITEM_00012 · … | 已装备 蓝色价值 · … | 界面附录 §2.9 |
| Art07Effects.cs:491、515 | 已获得 红色浪潮 · ITEM_00013 | 已获得 红色浪潮 | 界面附录 §2.9 |
| Art07Effects.cs:496、516 | 已加入永久骰 D006 · 永久骰 5→6 · 提交容量保持 | 已加入骰子 6 · 永久骰 5→6 · 提交栏格数不变 | 界面附录 §2.9 |
| Art07Effects.cs:532 新骰回执 | 实例 ID | 骰子 6；道具不显示这一行 | 界面附录 §2.9 |
| Art07Effects.cs:597、603、632、671、675 骰面改造回执 | D003⏎物理目标 F02；F02 · 永久面；D003 · F02 永久：… | 骰子 3⏎第 2 面；第 2 面 · 永久面；骰子 3 · 第 2 面永久：… | 界面附录 §2.9 |
| Art07Effects.cs:650–654 附魔加次数 | 蓝色价值（ITEM_00003） 提交 +1；…（ITEM_00004） 剩掷 +1 | 蓝色价值：本关提交 +1；…：本手可掷 +1 | 界面附录 §2.6 |
| Art07Settlement.cs:95 基金 | ITEM_00012 · 卖价＋3 · 基金累计 3 | 成长基金 · 卖价＋3 · 基金累计 3 | 界面附录 §2.8 |
| Art07Settlement.cs:99 临时骰 | D006、D007 | 骰子 6、骰子 7 | 界面附录 §2.8 |
| Art07SettlementBackdrop.cs:106、115、118、120、133、138 读档背景 | 记录；记录共 N 件；已保存关末 · 静态桌面；已提交记录 · 本关最后一手；静置；关末静态槽位；记录提交 N / K · 显示前8槽 | （空）；（空）；共 N 张；骰池；（空）；提交栏 N / K | 界面附录 §2.8 |
| Art07.cs:494–502 错乱乘积预览 | 独立 q · ITEM_00004⏎使用此 q：ITEM_00004、ITEM_00007 | 独立 q · 第 2 张 错乱乘积⏎使用此 q：第 2 张 错乱乘积、第 4 张 百变模仿 | 界面附录 §2.5 |
| Art07.cs:518 | 多个真实 C53⏎…每个真实 C53 独立确定 q… | 多张错乱乘积⏎…每张错乱乘积独立确定 q… | 界面附录 §2.5 |
| Art07Guide.cs:221 | 首次提示 · C53 的倍率在提交后揭示… | 首次提示 · 错乱乘积的倍率在提交后揭示… | 界面附录 §2.6 |
| Controllers.cs:49–53 百变模仿状态 | → 蓝色价值 / ITEM_00005；目标已损坏 ITEM_00005 · 复制失效；右邻 ITEM_00005 · 无可解析能力 | → 蓝色价值；目标已损坏 · 复制失效；右邻 蓝色价值 · 无可解析能力 | 界面附录 §2.9 |
| Controllers.cs:115、118 拖动预览 | 允许插入 · 普通占槽 3 / 5 · 容量 K 5 → 6⏎ITEM_00004 → 蓝色价值 / ITEM_00005 | 允许插入 · 栏位 3 / 5 · 提交栏 5 → 6 格⏎第 2 张 百变模仿 → 蓝色价值（第 3 张）；练习时不写上限 | 界面附录 §2.9 |
| Shop.cs:78 缺图兜底 | 内容编号（如 C12） | 名称首字 | 界面附录 §2.9 |
| Shop.cs:138 | 购买成功后，在当前 Run 立即生效。 | 购买后在本局立即生效。 | 界面附录 §2.9 |
| Shop.cs:141 | 价格与余额：价格 N 金币 · 当前余额 G | 价格：价格 N 金币 · 现有 G 金币 | 方案 §2.2（金币） |
| Runtime/GameApp.cs:390 | 本Run持续生效；… | 本局一直有效；… | 界面附录 §2.15 |
| V05.cs:55 | 成长与新机制 | 进阶规则 | 界面附录 §2.12 |
| V05.cs:81 | 最低 K 2 · … | 至少提交 2 颗 · … | 界面附录 §2.12 |
| V05.cs:101 | …全红／全蓝不属于牌型 H。 | …全红／全蓝不属于牌型倍率，另外计算。 | 界面附录 §2.12 |
| V05.cs:145 卡牌详情 | ITEM_00012 | 第 2 张 | 界面附录 §2.9 |
| V05.cs:152 | 实验组合 · 无提交触发⏎保留附魔外观，但本实例当前技能不会产生计分附魔加成。 | 附魔不会触发⏎这张卡不参与计分，所以计分附魔不会触发。 | 界面附录 §2.9 |
| V05.cs:158–161 | 绑定右邻：ITEM_00005 · 蓝色价值⏎…⏎动态状态所有者：ITEM_00005 | 绑定右邻：第 3 张 蓝色价值⏎…⏎成长与状态来自：第 3 张 | 界面附录 §2.9 |
| V05.cs:170 | 当前实例与目标／当前 Run 生效；… | 当前状态／本局生效；… | 界面附录 §2.9 |
| V10.cs:117 | 投掷 3 次 / 手 · 提交 3 次 / 关 · 容量 K 5 | … · 提交栏 5 格 | 界面附录 §2.3 |
| V10.cs:325、329–331、336–337 | X：D001 / F02 …；镜像：D001 / F02 …；吹玻璃：D001 …；获得骰子 D006；无心插柳：D001 / F02 … | 骰子 1 的第 2 面 …；吹玻璃：骰子 1 …；获得骰子 6；… | 界面附录 §2.7 |
| V10.cs:345 `NameOfInstance10` | 卡已不在手里时显示实例 ID | 一张卡牌 | 界面附录 §2.7 |
| Art07Results.cs:52–53 | 调试时长未记录；调试对局 · 不计普通成绩（练习也这样写） | 练习时：练习不计时；练习模式 · 不计成绩 | 界面附录 §2.15 |

## 2a 规则改动带来的显示修正（用户决定 D5）

D5（合并 `a61a762a`，RULES_DELTA §1.1 第 9 条、§8.3）：从本店货架买到的卡牌，离店前卖价不超过实际支付的价格。会话接口 `GetControllerSalePrice`、`ControllerSalePriceIn`（界面封装为 `OwnedSalePrice10`）已经返回封顶后的价格；出售按钮、出售确认和“当前卖价”用的就是它们，不用改。下面两处原来绕过了这些接口：

| 位置 | 原来 | 现在 | 测试 |
| --- | --- | --- | --- |
| Art07Effects.cs:485、514（买入后的摘要）；新函数 Art07Shop.cs:112–118 `AcquiredCardState11` | 成长基金的“卖价”取自 `Art07OfferState`，即静态的 `ControllerRules.SalePrice`（离店后的价格），不受封顶影响 | 用 `Session.ControllerSalePriceIn(after, 卡)`，即 `OwnedSalePrice10` 用的同一个接口，但读成交后的状态 `after`：摘要在界面刷新前生成，这时 `ViewState` 可能还是成交前的状态，里面没有这张卡的买价记录。例：三张砍价高手时 1 金币买下成长基金，摘要写“卖价 1”，不是“卖价 2” | `PLAY_V11_TEXT_SameShopResaleCapIsShownForAJustBoughtFund` |
| V05.cs:172–180（成长基金、百变模仿详情的“本实例基金”） | 只有两种说明：“（回收大王：标价全额）”和“（标价的一半）”；封顶的卡也写“标价的一半” | 加第三种：在商店里、`ViewState.shop.paidControllerPrices` 有这张卡的记录、且买价低于平常卖价时，写“基础卖价 1（本店买入，离店前不超过买价） + 基金累计 0 = 当前卖价 1”。封顶不起作用时照旧写“标价的一半”；离店后恢复平常的写法 | 同上：店内“基础卖价 1（本店买入，离店前不超过买价）”“当前卖价 1”；离店后“基础卖价 2（标价的一半）”“当前卖价 2” |

说明：刚买的卡基金累计和聚宝盆都是 0（两者都在关末增长），所以封顶时“基础卖价 = 当前卖价”，等式仍然成立。商店货架上的成长基金报价（`Art07OfferState`，Art07Shop.cs:103）仍写平常卖价，也就是离店后的价格；按协调者的说明没有改。如果要在报价上提示“本店买下后离店前不超过买价”，需要另定写法。

## 3 文字方案 §5 的错误（TX-08）

| # | 位置 | 原来 | 现在 | 测试 |
| --- | --- | --- | --- | --- |
| 1 | Core/Session/GameSession.Submission.cs:429 | 本手缩小射线已经生效。 | 规则组已改（§8） | EditMode `TX08_ASecondUseInOneHandNamesTheItemAlreadyActive`（规则组） |
| 2 | Art07Settings.cs:68–69、140–141；Text11.cs:122–142 | 设置页对所有玩家显示“调试口令” | 默认不显示。在设置页按 Ctrl+Shift+D（左右键都算）后显示原来的入口，只显示、不解锁；本次运行已解锁时照常显示 | `PLAY_V11_TEXT_DebugPasswordRowIsHiddenUntilTheSettingsChord` |
| 3 | Shop.cs:155–156；Art073Shop.cs:226–227 | 标题：自选研习 · 本店一次；按钮：自选研习 · 正常价 +1／还可 N 次／本店已研习 | 标题：指定升级 · 本店还剩 N 次（N 来自 `selfStudyLimit`）；按钮：指定升级 · 加价 1／指定升级 · 还剩 N 次／本店指定升级已用完（加价来自配置） | `PLAY_V11_TEXT_SelfUpgradeTitleCountsUsesFromTheConfiguration` |
| 4 | Art07StatusDetails.cs:89–91 | 续交 C01  +0 / 次（没有存钱罐也显示） | 只在有存钱罐（自己的或被复制的）时显示：续交 存钱罐  +1 / 次 | `PLAY_V11_TEXT_ContinueIncomeLineNeedsThePiggyBankAndNamesIt` |
| 5 | Text11.cs:80 `IsPracticeRun11`；Art07.cs:64、83；Art073Shop.cs:325；Controllers.cs:118；Art07Results.cs:52–53；Art07StatusDetails.cs:94；Art07Guide.cs:50、207；Modals.cs:468–469 | 练习整套显示调试标签（调试对局、调试配置 · 持有 N、调试时长未记录、紫色调试条），并关掉新手引导 | 练习模式；练习模式 · 持有 N 张⏎占用栏位 N；练习不计时；练习模式 · 不计成绩；没有调试条；新手引导和首次提示与普通对局相同。练习的会话、存档与规则不变（仍是独立的调试分区会话） | `PLAY_V11_TEXT_PracticeShowsPracticeLabelsAndTheFirstRunGuide` |
| 6 | V05.cs:16–17 | …各控制器倍率和倍率附魔，最后向下取整一次。 | …各卡牌倍率和倍率附魔；用了道具“灵光一闪”时再 ×2，首领减益让得分减半时再 ×0.5；最后向下取整一次。（顺序同 `ScoreCalculator.cs:269–270`） | `PLAY_V11_TEXT_HandbookScoringOrderIncludesItemAndBossFactors` |
| 7 | Art073Shop.cs:389–390；Text11.cs:112–120；V10.cs:159 | 商店左栏的下一关目标用普通目标，不计首领减益 | 与“查看关卡”同一计算：用 `CycleView` 的首领目标，减益已解除时用普通目标 | `PLAY_V11_TEXT_ShopNextTargetMatchesStageIntelWithBossModifier` |

## 4 取消旧存档界面（DEV_PLAN §4.5）

| 位置 | 原来 | 现在 |
| --- | --- | --- |
| Art071Screens.cs:51–56 | 有旧档时菜单按钮写“旧版本存档”，摘要写“旧版本存档 · p0.9.0 / schema 5” | 按钮总是“继续对局”，没有当前存档时不可点；摘要写“暂无存档 · 开始你的第一局” |
| Art07StatusDetails.cs:25（删除） | 点“继续对局”时打开旧档弹窗 | 删除 |
| Art07LegacySave.cs:7–18 | 旧档弹窗：版本对比、保留说明、“保留备份并开始 v1.0 规则的新局”、复制详情带版本 | 删除。只留当前版本的“存档读取说明”和“存档恢复说明”，标题、正文、“复制读取详情”“返回主菜单”与原来一致 |
| Modals.cs:49 | 错误类弹窗标题包含“旧版本存档” | 去掉这一项 |
| 全部 UI | 读取 `AtomicSaveStore.LastIncompatibleSave` | 不再读取。删除旧档本身由 W03 在 `AtomicSaveStore` 实现 |

## 5 改动过的字符串里的新用语

只处理本次改动的字符串，没有做全量替换。

| 旧 | 新 | 依据 |
| --- | --- | --- |
| 控制器 | 卡牌 | DEV_PLAN D2 |
| 普通占槽、槽位 | 栏位 | 方案 §2.1 |
| 容量 K、提交容量保持 | 提交栏 N 格、提交栏格数不变 | 方案 §2.1 |
| 连锁 N | 生效 N 张 | DEV_PLAN §4.1 第 4 项 |
| 余额、现金净增 | 金币、共获得 | 方案 §2.2 |
| 本Run、当前 Run | 本局 | 方案 §2.2 |
| 自选研习、研习 | 指定升级 | 方案 §1 第 11 处、§2.1 |
| 剩掷 | 本手可掷 | 方案 §2.1 |
| 活动骰 | 骰子 N 颗（永久 N） | 方案 §2.1 |
| D001、F01、物理目标、实例 | 骰子 1、第 1 面、（删除） | 方案 §2.1 |
| 成长与新机制、三列抽屉 | 进阶规则、得分明细 | 界面附录 §2.12、§2.7 |

## 6 测试期望的改动

没有删除测试。旧存档测试换成了新测试（第 17 行），原测试里仍然适用的检查都保留了。

| 测试（文件:行） | 旧期望 | 新期望 | 理由 |
| --- | --- | --- | --- |
| Art071MenuTablePlayModeTests.cs:118–121 `MenuTableGlyphs071` | 检查 `Version` 标签的字形 | 断言没有 `Version`；其余标签照旧检查 | 方案 §1 第 14 处：版本号只在“版本与构建”页 |
| Art071StylePlayModeTests.cs:373–376 `PLAY_ART071_CompactCardHoverKeepsBottomHitAndClickThenRestoresPooledVisuals` | 详情卡 `Identity` = 实例 ID | = “第 N 张”（这一张的位置） | TX-02。位置唯一，仍能证明打开的是这一张 |
| Art073PlayModeTests.cs:269–270 `PLAY_ART073_HighScoreBurstStartsOnFinalScoreCountsUpAndLeavesWithoutBlocking` | 含“连锁”和“16,560” | 含“生效”和“16,560” | DEV_PLAN §4.1 第 4 项 |
| Art07BreakdownLayoutPlayModeTests.cs:91–93 `BreakdownWalk07` | 1. D001/F03 … / 红 R | 1. 骰子 1 的第 3 面 … / 红 | TX-02 |
| Art07CardReadabilityPlayModeTests.cs:87–89、145、153–155、192–193（`CardWithoutSpace07`、`CardCapture07`、`PLAY_ART07_CardReadability_ThreeFontsAtActual720Or1080`） | `Identity` = 实例 ID；从 `Identity` 读出打开的实例 | `Identity` = “第 N 张”；打开的实例由调用方传入（新参数 `detailInstance`） | TX-02 |
| Art07DiceDetailsPlayModeTests.cs:39–40 `PLAY_Art07_DiceNetSeparatesPermanentRemoteAndEffectiveAndKeepsZeroProbabilityEmpty` | 1 → 3 / 蓝 B（遥控） → 4 / 蓝 B | 1 → 3 / 蓝（遥控） → 4 / 蓝 | TX-02：去掉 R/B |
| Art07EdgeScreensPlayModeTests.cs:199–200 `PLAY_ART07_DebugPasswordEntryDoesNotChangeOrdinaryRunBeforeExplicitCopy` | 直接用口令输入框 | 先断言输入框不存在，再调用 `RevealDebugAccess11()`（与 Ctrl+Shift+D 同一入口） | 方案 §5 第 2 处 |
| Art07FinalResultPlayModeTests.cs:20–21 `FinalOptions07` | 按 Fast 等英文找速度选项 | 先用 `SpeedLabel11` 转成中文 | TX-02 |
| Art07FinalResultPlayModeTests.cs:43–44 `PLAY_ART07_FINAL_RealRendererNormalFastReducedInstantRetainCommittedScoreAndSources` | 含“5040”和“连锁 4” | 含“×5,040”和“生效 4 张” | 方案 §3；DEV_PLAN §4.1 第 4 项 |
| Art07InformationPlayModeTests.cs:81–82 `PLAY_ART07_RandomNineBranchesSharedOwnerAreExactAndReadOnly` | `RandomScore` = 原始分数字符串 | = `PlayerNumberFormat.Exact(分数)` | 方案 §3：千分位，数位不变 |
| Art07InformationPlayModeTests.cs:109–114 `PLAY_ART07_SettlementUsesActualIncomeFundAndTemporaryCleanupReceipts` | 基金行含实例 ID；临时骰行含 D006 | 基金行含卡名、不含实例 ID；临时骰行含“骰子 6”、不含 D006 | TX-02 |
| Art07LoadingThreadSpikePlayModeTests.cs:79–94 `PLAY_ART07_LOADING_SPIKE_RealWorkerReadsDtoAndAtomicActiveBackupLegacy` | 旧档：schema 3、版本 p0.5.0、`legacyPreserved`、两份 `*.legacy-schema*` 副本；六个原文件字节全部不变 | 旧档：没有旧档信息、没有提示、没有副本，旧活动档和备份被删除；当前版本的四个文件字节不变 | DEV_PLAN §4.5（用户决定；删除由 W03 实现） |
| Art07LoadingUiPlayModeTests.cs:120–136 `PLAY_ART07_LOADING_UI_FastActiveBackupLegacyAndWorkerErrorKeepStoragePolicy` | 旧档说明含 p0.5.0 / schema 3；两份副本；旧文件保留；截图 legacy-ready-notice；后面的读取错误段沿用旧档文件 | 没有说明弹窗；旧文件与副本都被删除；菜单写“暂无存档 · 开始你的第一局”；截图 legacy-removed-no-save；读取错误段改用当前版本存档（写入 `valid`），断言它与备份字节不变 | DEV_PLAN §4.5。读取错误段原来借用旧档文件，旧档现在会被删除 |
| Art07ScoreDrawerPlayModeTests.cs:62–63 `PLAY_ART07_ScoreDrawerC53BranchAndHistoricalRowsKeepFrozenExactValuesAndCopy` | 公式含“控制器 n/d” | 公式含“卡牌 <小数，全部数位>” | TX-01；D2 |
| Art07ScoreDrawerPlayModeTests.cs:91–121 `DrawerWalk07`（另加辅助函数 75–78） | 公式“控制器 n/d”；步骤原样分数；来源含实例 ID 和“→ stateOwnerId”；骰子行 1. D001 / F03、红 R | 公式“卡牌 <小数>”；步骤为小数，全部数位、不含“/”；来源含“第 N 张”“复制第 M 张”，不含 ITEM_；骰子行 1. 骰子 1 的第 3 面、红 | TX-01、TX-02 |
| Art07ScoreLabelsPlayModeTests.cs:53–61 `PLAY_ART07_ScoreLabelsPreviewMixedZeroAndRandomKeepEveryGlyphAfterRedraw` | 倍率片“控制器 ×≈4.29e9”；公式含“控制器 n/d” | 倍率片“卡牌 约 ×4.29×10<sup>9</sup>”；公式含“卡牌 4,294,967,296”，不含“/” | TX-01；DEV_PLAN §4.1 第 10 项；D2 |
| Art07SettlementLegacyPlayModeTests.cs:109–139：`PLAY_ART07_LEGACY_ExactVersionReadOnlyDetailsAndNewRunCancellationKeepOldBytes` 换成 `PLAY_V11_OLDSAVE_IncompatibleSaveIsRemovedWithoutReminderAndMenuShowsNoSave` | 旧档弹窗显示两边版本；保留一份副本；复制详情含版本；“保留备份并开始新局”打开新局对话框，取消不写入；菜单按钮“旧版本存档” | 启动时旧活动档、备份和以前的副本都被删除；没有弹窗、没有提示；“继续对局”不可点，摘要“暂无存档 · 开始你的第一局”；100/125/150% 字号下菜单文字完整；取消新局不写入；确认新局正常开始 | DEV_PLAN §4.5。保留了原测试里仍适用的检查：不从旧档恢复、取消不写入、文字完整 |
| Art07TransactionPlayModeTests.cs:140–143 `PLAY_ART07_TX_PurchaseUsesCommittedNewInstanceAndSkipRestoresAllGroups` | 摘要含实例 ID | 摘要含卡名、不含实例 ID；动画目标仍按实例检查 | TX-02 |
| Art07WalletPlayModeTests.cs:40–41 `PLAY_ART07_ShopWalletExactDigitsInterestAndGpuAtThreeFonts` | 金额 `ToString()` | `ToString("N0")` | 方案 §3：千分位 |
| FailureAndInputPlayModeTests.cs:108–109 `PLAY_ShopPurchaseFooterShowsSlotAndDuplicateFailuresBeforeConfirm` | `.And.Contains("控制器")` | `.And.Contains("卡牌")` | Core 提示按 D2 与用语规范改写（RULES_DELTA §10） |
| FailureAndInputPlayModeTests.cs:115（同一测试） | `.And.Contain("槽位")` | `.And.Contain("栏位")` | Core 提示按 D2 与用语规范改写（RULES_DELTA §10） |
| GameplayPlayModeTests.cs:218–220 `PLAY_FastAnimationUsesSameSeedResultsAndDoesNotConsumeExtraRolls` | Normal → Fast | 标准 → 快速 | TX-02 |
| Presentation07/Art07FixturePlayModeTests.cs:64–67 `PLAY_ART07_TwoHundredFullRendererReplaysKeepNodesMaterialsAndCommittedStateStable` | 找“Fast” | 找“快速” | TX-02 |
| ShopBoundaryPlayModeTests.cs:109–110 `PLAY_ShopTargetCancellationAndPermanentDiceBoundariesPreserveOffers` | 购买后：2点 · 蓝 B · 光滑 | 购买后：2点 · 蓝 · 光滑 | TX-02 |
| SubmissionHistoryPlayModeTests.cs:95–99 `PLAY_PreviousSubmissionRetainsOriginalScoreAndPermanentEffectsAfterNextRollAndReload` | 含 D002；D001 的六面复制到 D002；F03 永久染红 | 含“骰子 2”、不含 D002；骰子 1 的六面复制到骰子 2；骰子 2 的第 3 面永久染红 | TX-02 |
| UiBoundaryPlayModeTests.cs:249–250 `PLAY_ExistingRunRollAndSubmitSaveFailureRetriesExactCandidateAndPresentsOnce` | D001 的六面复制到 D002 | 骰子 1 的六面复制到骰子 2，且不含“D00” | TX-02 |
| V06RuntimePlayModeTests.cs:22–23 `PLAY_V06_DebugPasswordAddApplyReloadAndReturnPreserveNormalCheckpoint`；118–119 `PLAY_V06_SettingsPasswordFocusBlocksSubmitAndDisplaySettingsStayOutsideRun` | 直接用口令输入框 | 先断言隐藏（第一个测试），再 `RevealDebugAccess11()` | 方案 §5 第 2 处 |

驱动界面的验证与录屏脚本也做了同样的调整：`Runtime/Verification/PlayerVerification.V06.cs:268–269`（速度标签）、274–275 与 356–357（先显示调试入口）、341–342（概率 1 位小数）；`Art07ScreenRecorder.cs:161–162`；`Art07VisualRecorder.cs:282–283`。

测试辅助：`UiBoundaryPlayModeTests.cs:324–333` 新增 `RoutedKeyboard.PressChord`，同一次输入事件里按下多个键。

## 7 新增测试 `Tests/PlayMode/V11TextP0PlayModeTests.cs`

| 测试 | 检查 |
| --- | --- |
| `PLAY_V11_TEXT_NumberFormatUsesDecimalsSeparatorsAndShortLargeNumbers` | §1.1 每条规则：140.4、3.9、≈0.33、−0.5、3,896.1、1,000,010、15 位完整、约 1×10¹⁵、约 1.23×10¹⁹、倍率片 约 ×4.29×10⁹、16.7%、非数字原样 |
| `PLAY_V11_TEXT_NamesReplaceIdsAndLabelsAreChinese` | 骰子 6、第 2 面、骰子 3 的第 2 面；菜单没有版本号；对局左栏副标题为空；速度“标准”；快捷键“回车”“退格”“R” |
| `PLAY_V11_TEXT_DebugPasswordRowIsHiddenUntilTheSettingsChord` | §5 第 2 处：菜单里按组合键、单按 D、Ctrl+D 都不显示；设置页 Ctrl+Shift+D 显示但不解锁；错误口令被拒；正确口令照旧解锁 |
| `PLAY_V11_TEXT_SelfUpgradeTitleCountsUsesFromTheConfiguration` | §5 第 3 处：标题与按钮的次数随配置和已用次数变化 |
| `PLAY_V11_TEXT_ContinueIncomeLineNeedsThePiggyBankAndNamesIt` | §5 第 4 处：没有存钱罐时不显示；有时写卡名、不含 C01 |
| `PLAY_V11_TEXT_PracticeShowsPracticeLabelsAndTheFirstRunGuide` | §5 第 5 处：练习模式标签、没有调试条、引导从 1 / 5 开始、商店写“练习模式”不写“调试”、回主菜单后普通存档仍为空 |
| `PLAY_V11_TEXT_HandbookScoringOrderIncludesItemAndBossFactors` | §5 第 6 处：顺序为倍率附魔 → 灵光一闪 ×2 → 减半 ×0.5 → 取整；不出现“控制器” |
| `PLAY_V11_TEXT_ShopNextTargetMatchesStageIntelWithBossModifier` | §5 第 7 处：首领减益“厚墙”（×1.5）下，商店左栏和“查看关卡”都显示 1,500，不是 1,000 |
| `PLAY_V11_TEXT_SameShopResaleCapIsShownForAJustBoughtFund` | §2a（D5）：三张砍价高手时 1 金币买下成长基金；买价记录为 1、平常卖价 2、会话卖价 1；摘要写“卖价 1”；详情写“（本店买入，离店前不超过买价）”和“当前卖价 1”；离店后写“（标价的一半）”和“当前卖价 2” |

## 8 Core 侧：方案 §5 第 1 处

- 位置：`Core/Session/GameSession.Submission.cs:429`。
- 原文：本手缩小射线已经生效。（缩小射线是卡牌 C37；道具 I05 叫“小数多来”）
- 现状：规则组在 `c4707c5c`（合并 `567ee14a`）改成“本手小数多来已经生效。”，与同类提示（本手红色浪潮已经生效。等）一致，并加了 EditMode 测试 `TX08_ASecondUseInOneHandNamesTheItemAlreadyActive`；RULES_DELTA §10 有记录。本任务没有改 Core。
- 界面附录建议的写法是“本手已经用过“小数多来”。”。现在的写法内容正确，句式也与同类提示一致，**不需要再改**；要统一成附录写法的话，应连同其余五条一起改。

## 9 没有做的与待定的

| 项 | 位置 | 原因 / 建议 |
| --- | --- | --- |
| 读档失败提示含异常原文 | Art07Loading.cs:63：恢复失败，原存档保留：`<异常原文>`；Art07LegacySave.cs 的“读取详情”正文 | 界面附录标为 D。任务要求“存档读取说明”保持原样，所以没改。建议协调者决定是否改成“存档读取失败，原文件没有改动。可以复制读取详情。”，原文只放在复制内容里 |
| Core 的异常类提示 | GameSession.cs:51–126；GameSession.Validation.cs | 属于 Core，不在本任务范围 |
| 菜单与新局对话框里的“普通对局 · 种子 N” | Art071Screens.cs:56；Art07NewRun.cs:77；结果页 Art07Results.cs:54 | 种子是玩家能输入、能分享的数，不是内部编号；测试 Art071MenuTablePlayModeTests.cs:92 也依赖它。附录建议删掉，属信息架构（X），留给 W16 |
| 种子范围“0 到 4294967295” | Art07NewRun.cs:57；Runtime/GameApp.cs:135 | 种子是要照着输入的编号，加千分位会让人以为可以输入逗号，所以不加 |
| 完整算式保留 ×1 的项 | Art07ScoreDrawer.cs:81 | 附录建议省略 ×1。得分明细是完整账目，保留“红组 1”“附魔 1”能看出哪项没起作用；横幅的短算式照旧省略 ×1。需要省略的话改一行即可 |
| 对局内文字 | “01 待锁”、“结束本轮”、“已锁定 · 本手不可移回骰池”、底部提示、分数栏状态、[R]/[H] 提示 | W11 |
| 配置里的文字 | 卡牌、道具、牌型等的说明，`upgradeReason` 等 | W10 |
| 默认风格的说明“复古印刷骰桌” | UI/Theme/GameTheme.cs:48（风格选择里显示） | 风格代码不在本任务范围。它是风格名的说明，不是对局左栏 |
| 没改动的旧用语 | 例如：普通槽、普通占槽（Art07.cs:76、Art073Shop.cs:325；V09 测试断言“普通槽已满”）、消耗品、研究强化（Art073Shop.cs:219、223）、提交容量（Art073Shop.cs:393）、本次无控制器触发（Art07ScoreDrawer.cs:65、Modals.cs:325）、控制器顺序（V10.cs:226）、控制器货架（Art07Effects.cs:546）、物理对面（Art073DiceLibrary.cs:96）、余额与现金净增（Art07Settlement.cs:87）、基础奖励与剩次奖励（Art07StatusDetails.cs:86） | 任务不做全量术语替换。留给 W16 和 TX-04 检查脚本 |
| 效果文字里的金币 | V10.cs:318–320、338；Art07Presentation.cs:185–203；Modals.cs:385 | 都是单次金币变化，实际不到 1,000，加不加千分位显示一样。附录 §3.3 列出的金币位置都已改 |
| 错乱乘积预览里的“q” | Art07.cs:502、512 | 附录 §2.5 建议改写整段（J），不是 P0 |
| 结果页“最高倍率”的口径 | Art07Results.cs:42–54 | 只算牌型、颜色、红组、卡牌、附魔，不含灵光一闪和首领减半，与原来一致。是否计入另定 |
| 数字和量词之间的空格 | 骰子 3 的第 2 面、提交栏 5 格、生效 4 张 | 方案 §3 说正文不空格，但方案 §1、§2.1 的示例都带空格；这里按示例写。建议 W16 统一 |

## 10 验证

| 检查 | 结果 |
| --- | --- |
| 编译检查：`dotnet build DicePrototype/Assets/Game/Core/Rules/Validation~/UnityTestsCompilation.csproj -c Release`（编译 Runtime、UI、EditMode、PlayMode 全部源码，不运行 Unity） | 没有新错误。仍有 5 个原有错误，都在 `Tests/PlayMode/Art07ResourceDiagnosticPlayModeTests.cs:99–104`（引用了这个工程看不到的成员），与本任务无关。改动文件没有新警告 |
| 行尾 | 改动过的文件中，没改的行保留原来的 CRLF/LF |
| PlayMode / EditMode 测试 | **未运行**（任务要求不启动 Unity） |
| 依赖 | 旧存档删除相关的断言（Art07LoadingThreadSpike、Art07LoadingUi、`PLAY_V11_OLDSAVE_…`）依赖 W03 对 `AtomicSaveStore` 的改动；W03 没合入时这几项会失败 |

需要在 Unity 里确认的风险：

1. 倍率片和横幅里的“约”与上标 `<sup>` 靠动态字库渲染，静态字库 DiceChinese 没有“约”字。需要实机截图（TX-01）。
2. 钱包数字框 150% 字号下改为 312 宽、字距最低 −8，数字之间要留得出空隙，需由 `Art07WalletPlayModeTests` 实机确认。
3. 倍率片的 6 位上限是按 150% 字号估算的，窄屏布局要看实机截图。
