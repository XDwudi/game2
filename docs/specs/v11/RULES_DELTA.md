# 骰子构筑 V1.1 规则改动（p1.1.0 相对 p1.0.0）

状态：**REVIEW**（2026-11-16，实施者子 agent R 提交；同日按协调者意见修订第 2 节与第 5 节第 3 条，新增第 10 节；按用户对第 5 节第 1 条的选择 D 新增第 1.1 节第 9 条；独立复核与 Unity 测试未完成）。

- 依据：用户 2026-11-16 指令第 2、4、5 条；[开发方案](DEV_PLAN.md) §4.3（W04）、§4.4（W05）、§4.5（W03 的规则与存档部分）；协调者 2026-11-16 对第 5 节第 3 条的决定；用户 2026-11-16 对第 5 节第 1 条的选择 D（经协调者转达）。
- 基线：[p1.0.0 规则规格](../v10/RULES_SPEC.md)。本文件只写改动；没写到的规则沿用 p1.0.0。
- 范围：`Core/`（规则与会话）、`Runtime/AtomicSaveStore.cs`、EditMode 测试。界面、配置文字、PlayMode 测试不在本次改动内，需要跟进的位置见第 8 节。
- 用词：玩家可见的文字用“卡牌”（决定 D2）。`Core/` 里会显示给玩家的提示已全部改用“卡牌”和“栏位”（第 10 节）；代码标识符不改名。

## 1 同名卡牌可以重复持有（G3）

### 1.1 规则

1. **持有**：同名卡牌可以同时持有多张。每张是独立实例：各占自己的栏位（“不占栏位”附魔照旧不占普通栏位），各自记成长值、基金、周转倍率、加成颜色等状态，各自出售、各自被摧毁。
2. **货架**：不再排除已持有的卡；每次生成或刷新货架时第一张报价的保底池（标价 ≤ 4）也不排除。已持有的卡的附魔与未持有的卡一样，从该卡的常规附魔池抽取（不再是“不同于已有附魔的非空附魔”）。
3. **同一家商店不出现两张同名报价**：货架抽样不放回；刷新、扩容（O05）时也排除店里已有的同名报价（含战利品组与赠礼组里的）。这一条与是否持有无关，沿用 p1.0.0。
4. **首领战利品**：L1“稀有控制器”、L2“附魔控制器”不再排除已持有的卡；仍排除货架上已有的卡。其余四种战利品不变。
5. **购买**：不再替换旧卡，也不再用旧卡卖价抵扣；新卡追加在最右侧。栏位与总张数按“现有装备 + 新卡”检查，不够时拒绝（提示“普通卡牌栏位或总张数已满，可先出售卡牌。”，见第 10 节）。取消“同名控制器只能替换为不同的非空附魔”和“控制器定义重复”两条拒绝。
6. **存档校验**：普通对局允许同名多张；实例编号仍必须唯一。调试对局本来就允许，不变。
7. **效果怎么叠加**：每张都是一个来源，计数方式与百变模仿复制出来的来源完全相同——倍率相乘、加值相加、次数相加；写明“持有时”的开关类效果与同样只看“有没有”的效果只生效一次；带状态的卡每张各自记状态。150 张逐张结论见第 4 节。
8. **不变的地方**：滥竽充数（C112）仍只给“未持有”的普通卡（卡面文字如此，本版没改，见第 5 节第 2 条）；替罪羊（C131）每个首领关只用最左侧一张；祭坛、吹玻璃、昙花一现这类“只由真实实例执行”的效果，每张同名卡都是真实实例，各自执行。
9. **同店买入的卡牌离店前卖价不超过实际支付价格**（用户 2026-11-16 选择第 5 节第 1 条的 D）。砍价高手（C98）照旧按张叠加降价。在当前这家商店的货架上**花钱买下**的卡牌，在离开这家商店之前，卖价 = min(按现有规则算出的卖价, 实际支付的价格)；离开商店后恢复现有规则。
   - 只管花钱的购买。免费领取的战利品、在商店以外得到的卡牌（开局装备、滥竽充数、上一家商店买的等）不受影响：刚领的战利品在这家店里照旧按标价的一半回收（p1.0.0 已是如此）。
   - 回收大王（C140）的“本店取得的仍按半价”照旧，封顶再叠加在它之上：本店买下的卡牌先按半价，再与实际支付价格取小。
   - 商店状态为每张这样的卡牌记下实际支付价格（`ShopState.paidControllerPrices`），卖掉时一并删除；离店时随商店一起清空。读档校验从严：记录只能属于本店买下且仍持有的卡牌，每张一条，价格在 1 到该卡标价（含附魔加价）之间；本店货架上已售出、仍持有的卡牌必须有记录，领取的战利品不能有记录。
   - 本店买下后又被附魔台（O11）附魔的卡牌，离店前仍以买卡时支付的价格封顶（附魔台另行付费，不计入）；离店后按附魔后的标价正常回收。附魔只会从“无附魔”变为有附魔，加价不小于 0，所以这种记录仍能通过读档校验。
   - 结果：任意多张砍价高手都不能在同一家店里买进再卖出赚钱（例：三张砍价高手时，标价 4 的卡 1 金币买下，离店前卖价 1 金币，而不是标价的一半 2 金币）。

### 1.2 连带的规格条文

| p1.0.0 条文 | p1.1.0 |
| --- | --- |
| 4.2 L1“3 张未持有的稀有控制器” | 3 张稀有控制器（可以是已持有的），不与货架重复 |
| 4.2 L2“3 张未持有的控制器” | 3 张控制器（可以是已持有的），不与货架重复 |
| 8 回收大王“影响卖出、以旧换新的抵扣” | 没有以旧换新，只影响卖出与卖价显示 |
| 8 回收大王“买进再卖出不会赚钱（含砍价高手）” | 仍然成立，含多张砍价高手：同店买入的卡牌离店前卖价不超过实际支付价格（第 1.1 节第 9 条） |
| 8 “以旧换新的净价可以为负” | 取消（购买价就是标价） |
| 8 百变模仿“C140、C145 多个来源效果相同；自毁、关卡开始的改骰只由真实实例执行” | 同名多张与之相同：C140、C145 多张只生效一次；C131、C141、C128 每张真实卡各自执行（C131 每关只用最左侧一张） |

### 1.3 给界面的接口变化

| 接口 | p1.1.0 |
| --- | --- |
| `GetOfferTradeIn(offer)` | 恒为 0（保留以免界面编译失败） |
| `GetOfferNetPrice(offer)` | 等于 `GetOfferPrice(offer)` |
| 购买回执的 `ControllerReplaced` 效果 | 不再产生 |
| `ControllerRules.ValidateEquipment` | 普通对局不再拒绝同名定义 |
| `GetControllerSalePrice(instanceId)`、`ControllerSalePriceIn(view, card)` 与出售时实际到账的金币 | 已含第 1.1 节第 9 条的封顶 |
| `ShopState.paidControllerPrices`（`ShopPaidPrice { instanceId, paid }`） | 新字段：本店买下的卡牌的实际支付价格，界面可以据此标注“不超过买价” |
| `ControllerRules.SalePrice(instance, cfg, fullPrice)`（纯函数） | 不含本店规则（本店取得的卡牌按半价、买价封顶），界面显示持有卡牌的卖价时应改用上面两个会话接口 |

## 2 首领战利品先领（G5）

### 2.1 规则

1. 商店仍在“继续”时生成，随机抽取顺序不变（战利品组在其余内容之后、赠礼组之前）。
2. **未领取**：商店里有战利品组（`LOOT`），它没有关闭（折现会关闭整组），并且一件都还没领。
3. 未领取时，商店阶段**只接受**：领取战利品组的报价（含折现）、出售卡牌或道具、调整卡牌顺序。其余一律拒绝，状态不变，提示 **“先领取首领战利品。”**：
   - 购买货架上的任何商品（卡牌、道具、牌型升级、骰面改造、服务、新骰）；
   - 领取赠礼组（杂货郎、铁匠铺）的报价；
   - 刷新货架、自选研习、离开商店。
4. **协调者决定：领取第一件后即解锁商店，其余件数离店前仍可领取。** 可拿多件的战利品（L4 骰面工坊、L6 道具补给，各 3 件）领了第一件或折现后，商店立即恢复正常；剩下的件数在这家商店里仍可领取（领过之后不能再折现，沿用 p1.0.0），离开商店时作废（与以前没领的战利品一样）。道具或卡牌放不下时，可以先出售腾位置，但不必为了拿满而出售。
5. （已取消）初版的“防卡死”补充规则：领过一件后、剩下的报价都无法领取时不再阻挡商店。改为第 4 条后，第一次领取前总能折现，领取后商店即解锁，这条规则不再需要，代码与对应测试已一并去掉。
6. **读档**：未领取的状态完全由存档里的商店数据推出，读档后照样拦截。存档校验不需要新字段。
7. 调试工作区的“应用调试场景”不受拦截（它会整体替换状态）。

### 2.2 给界面的接口

| 接口 | 用途 |
| --- | --- |
| `bool IsLootPending` | 当前状态是否必须先处理战利品 |
| `bool IsLootPendingIn(RunState view)` | 同上，对界面手里的状态副本求值 |
| `PurchaseError(offerId)` | 未领取时对非战利品报价返回“先领取首领战利品。” |

## 3 旧存档（G6 的规则与存档部分）

### 3.1 规则

1. **“其他版本的存档”**：存档信封声明了版本（`schema` > 0 且 `config` 非空），并且结构版本或配置版本与当前不同（更旧或更新都算）。信封读不出来或没有声明版本的文件，按“当前版本的损坏”处理。
2. **读档（`Load`）**先删除：活动存档与备份中属于其他版本的那个（或两个），以及这个存档位旁边所有 `<存档文件名>.legacy-schema*` 旧版保留副本。不提示；`LastIncompatibleSave` 恒为空；被删的文件列在仅供诊断的 `LastDeletedSaveFiles`。
3. **当前版本的文件从不删除**。活动存档与备份里剩下的当前版本文件照旧处理：活动存档有效就读取；无效则读备份，并给出原来的“活动存档不可读，已恢复上一份有效备份。”；都不可读就返回空并给出原来的读取说明。
4. 混合情况：
   - 活动存档是其他版本、备份是当前版本且有效：删除活动存档，恢复备份，并给出上面的备份提示（这是当前版本的恢复，不是旧档提醒）。
   - 活动存档是当前版本但损坏、备份是其他版本：删除备份，保留损坏的活动存档并给出读取说明；下次保存时照旧把它复制为 `.rejected-*` 隔离副本。
5. **写档（`Save`）**：活动存档是其他版本时直接覆盖，不做 `.rejected-*` 隔离副本，也不让它成为备份；不再生成 `.legacy-schema*` 副本。当前版本损坏文件的隔离照旧。
6. 删除是尽力而为：某个文件删不掉时留在原处，读档时忽略它（不读取、不提示），下次读档再删。
7. 调试存档位（`debug-run.json`）同样处理；每个存档位只删自己的文件。`.rejected-*` 隔离副本、设置与其他文件都不动。
8. 前提：版本号要变。配置改为 p1.1.0（或存档结构版本改变）后，p1.0.0 的存档才会被当作其他版本删除；在那之前它们仍是“当前版本”。codex/v1.1 的配置已是 p1.1.0。

## 4 150 张卡牌在多张时的效果

“按张叠加”：每张都是一个来源，倍率相乘、加值相加、次数相加，带状态的卡各自记状态（与百变模仿复制出的来源同样计数）。“只生效一次”：开关类、只看“有没有”的效果，或多张产生的结果完全相同。“其他”：另有说明。统计：按张叠加 128 张，只生效一次 18 张，其他 4 张。代码位置是本分支提交时的行号。

| 编号 | 名称 | 多张时 | 说明 | 代码位置 |
| --- | --- | --- | --- | --- |
| C01 | 存钱罐 | 按张叠加 | 每张每次有效提交 +1 金币 | ScoreCalculator.cs:299；GameSession.Submission.cs:97 |
| C02 | 蓝色疯狂 | 按张叠加 | 每张 ×3，多张相乘 | ScoreCalculator.cs:11、224 |
| C03 | 首当其冲 | 只生效一次 | 第一颗视为红是开关，多张与一张相同 | ScoreCalculator.cs:39 |
| C05 | 再来三次 | 按张叠加 | 每张每手各给一次 +3 投掷（条件成立时依次触发） | GameSession.cs:504–511 |
| C06 | 数字多多 | 按张叠加 | 每张 ×3，多张相乘 | ScoreCalculator.cs:11、224 |
| C07 | 骰子复制 | 只生效一次 | 复制计划只执行一次（结果相同） | ScoreCalculator.cs:300；GameSession.Submission.cs:142–150 |
| C08 | 少数派 | 按张叠加 | 每张 ×2，多张相乘 | ScoreCalculator.cs:11、224 |
| C09 | 简化提交 | 按张叠加 | 每张容量 −1（最少 1）且 ×3 | ControllerRules.cs:84；ScoreCalculator.cs:11 |
| C10 | 少就是多 | 按张叠加 | 每张对符合条件的骰子各 +10 点数贡献 | ScoreExplanation.cs:36 |
| C11 | 2233 | 按张叠加 | 每张 ×6，多张相乘 | ScoreCalculator.cs:11、224 |
| C12 | 不要奇数 | 只生效一次 | 奇数 +1 只做一次（不会变成 +2） | ScoreCalculator.cs:37–38 |
| C13 | 就要奇数 | 按张叠加 | 每张对符合条件的骰子各 +4 点数贡献 | ScoreExplanation.cs:37 |
| C14 | 只要奇数 | 按张叠加 | 每张 ×3，多张相乘 | ScoreCalculator.cs:11、224 |
| C15 | 红色扩散 | 只生效一次 | 染红计划只执行一次（结果相同） | ScoreCalculator.cs:301；GameSession.Submission.cs:167 |
| C17 | 小鬼当家 | 按张叠加 | 每张对符合条件的骰子各 +6 点数贡献 | ScoreExplanation.cs:38 |
| C18 | 红蓝相当 | 按张叠加 | 每张 ×4，多张相乘 | ScoreCalculator.cs:11、224 |
| C19 | 小头大用 | 按张叠加 | 每张对符合条件的骰子各 +8 点数贡献 | ScoreExplanation.cs:39 |
| C20 | 66大顺 | 按张叠加 | 每张对符合条件的骰子各 +15 点数贡献 | ScoreExplanation.cs:40 |
| C21 | 增加提交 | 按张叠加 | 每张容量 +1（上限 8） | ControllerRules.cs:84 |
| C22 | 大量提交 | 按张叠加 | 每张容量 +2（上限 8） | ControllerRules.cs:84 |
| C24 | 我是状元 | 只生效一次 | 红组分组规则是开关 | ScoreCalculator.cs:62–63 |
| C25 | 红运当头 | 只生效一次 | 红组合并是开关 | ScoreCalculator.cs:62–63 |
| C26 | 红色主义 | 只生效一次 | 染红计划只执行一次（结果相同） | ScoreCalculator.cs:302；GameSession.Submission.cs:151–157 |
| C27 | 对穿肠 | 按张叠加 | 每张每个对子组 +10 基础分 | ScoreCalculator.cs:211 |
| C28 | 对对穿肠 | 按张叠加 | 每张 ×3，多张相乘 | ScoreCalculator.cs:11、224 |
| C29 | 简单顺子 | 只生效一次 | 顺子最短长度改为 4 是开关 | ScoreCalculator.cs:49；GameSession.Shop.cs:18 |
| C30 | 三条开泰 | 按张叠加 | 每张 ×2，多张相乘 | ScoreCalculator.cs:11、224 |
| C31 | 四平八稳 | 按张叠加 | 每张 ×3，多张相乘 | ScoreCalculator.cs:11、224 |
| C32 | 天地大同 | 按张叠加 | 每张 ×2，多张相乘 | ScoreCalculator.cs:11、224 |
| C33 | 大数开路 | 按张叠加 | 每张 ×4，多张相乘 | ScoreCalculator.cs:11、224 |
| C34 | 大数致胜 | 按张叠加 | 每张转移 15 个百分点（与 C35 相抵，按净来源数计，转完为止） | ProbabilityCalculator.cs:42–61；GameSession.cs:486 |
| C35 | 小数出奇 | 按张叠加 | 同 C34，方向相反 | ProbabilityCalculator.cs:42–61；GameSession.cs:486 |
| C36 | 不断成长 | 按张叠加 | 每张让前两颗的朝上面各 +1（夹在 1–6；与 C37 相抵） | ScoreCalculator.cs:303；GameSession.Submission.cs:158–166 |
| C37 | 缩小射线 | 按张叠加 | 每张 −1，规则同 C36 | ScoreCalculator.cs:303；GameSession.Submission.cs:158–166 |
| C38 | 蓝色扩散 | 只生效一次 | 染蓝计划只执行一次（结果相同） | ScoreCalculator.cs:305；GameSession.Submission.cs:168 |
| C39 | 蓝色价值 | 按张叠加 | 每张对符合条件的骰子各 +6 点数贡献 | ScoreExplanation.cs:41 |
| C40 | 不断增加 | 按张叠加 | 每张 ×4，多张相乘 | ScoreCalculator.cs:11、224 |
| C41 | 百变模仿 | 其他 | 每张是独立的模仿，各自复制关卡开始时右邻那张；两张模仿相邻时都复制第二张右边的卡；基金累计记在模仿自己身上 | ControllerRules.cs:15–46 |
| C42 | 玻璃大炮 | 按张叠加 | 每张 ×5，提交后每张各自 15% 损坏 | ScoreCalculator.cs:11、312；GameSession.Submission.cs:128–136 |
| C43 | 牌型提升 | 按张叠加 | 每张 +1 级（上限 8 级），学无止境按实际升的级数成长 | ScoreCalculator.cs:296、306–308；GameSession.Submission.cs:105–111 |
| C44 | 有钱真好 | 按张叠加 | 每张按冻结钱包各加一次基础分 | ScoreCalculator.cs:212 |
| C45 | 飘忽不定 | 按张叠加 | 每张有自己的加成颜色（获得时与每次有效提交后各自重抽），各自让该色骰 ×3；两张同色时该色 ×9 | ScoreExplanation.cs:50；GameSession.Submission.cs:121–127；GameSession.Shop.cs:452–457 |
| C46 | 稳稳乘数 | 按张叠加 | 每张 ×2，多张相乘 | ScoreCalculator.cs:11、224 |
| C47 | 控制欲 | 按张叠加 | 每张 ×(件数÷2)，件数含全部同名卡 | ScoreCalculator.cs:227 |
| C48 | 有也不交 | 按张叠加 | 每张每个空提交栏 +10 基础分 | ScoreCalculator.cs:213 |
| C49 | 千年老二 | 按张叠加 | 每张 ×(有效 5 点颗数)，没有 5 点时每张都是 ×0 | ScoreCalculator.cs:228 |
| C50 | 稳稳分数 | 按张叠加 | 每张 +20 基础分 | ScoreCalculator.cs:214 |
| C51 | 人员流动 | 按张叠加 | 每张有自己的 n；每家商店第一次真实卖出控制器后，买卖前后都在的每张各 +0.5；买入同名卡不算卖出 | ScoreCalculator.cs:229；GameSession.Shop.cs:563–576 |
| C52 | 成长基金 | 按张叠加 | 每张每关各自卖价 +3 | GameSession.Submission.cs:219–226 |
| C53 | 错乱乘积 | 按张叠加 | 每张真实卡各自抽一次倍率（模仿复用被模仿那张的结果） | GameSession.Submission.cs:44–49；ScoreCalculator.cs:245 |
| C54 | 连庄 | 按张叠加 | 每张各自记成长值（主牌型重复时每张各自成长），倍率按各自的值相乘 | ScoreCalculator.cs:231；ControllerRules.cs:50 |
| C55 | 积少成多 | 按张叠加 | 每张各自记成长值、各自加入基础分 | ScoreCalculator.cs:193、262 |
| C56 | 深蓝 | 按张叠加 | 每张各自记成长值（没有红面的提交时每张各自成长），倍率按各自的值相乘 | ScoreCalculator.cs:231；ControllerRules.cs:50 |
| C57 | 红火 | 按张叠加 | 每张各自记成长值（红组 ≥3 的提交时每张各自成长），倍率按各自的值相乘 | ScoreCalculator.cs:231；ControllerRules.cs:50 |
| C58 | 老当益壮 | 按张叠加 | 每张各自记成长值（过关时每张各自成长），倍率按各自的值相乘 | ScoreCalculator.cs:231；ControllerRules.cs:50 |
| C59 | 余勇可贾 | 按张叠加 | 每张各自记成长值（结算时各自成长）、各自加入基础分 | ScoreCalculator.cs:193；GameSession.Submission.cs:229 |
| C60 | 千锤百炼 | 按张叠加 | 每张各自记成长值（在商店取得骰面改造时每张各自成长），倍率按各自的值相乘 | ScoreCalculator.cs:231；ControllerRules.cs:50 |
| C61 | 学无止境 | 按张叠加 | 每张各自记成长值（牌型或颜色升级时每张各自成长），倍率按各自的值相乘 | ScoreCalculator.cs:231；ControllerRules.cs:50 |
| C62 | 熟能生巧 | 按张叠加 | 每张各自记成长值（每次投掷各自 +1）、各自加入基础分 | ScoreCalculator.cs:193；GameSession.cs:474 |
| C63 | 收藏家 | 按张叠加 | 每张 ×(永久骰数×0.3，最低 ×1) | ScoreCalculator.cs:240 |
| C64 | 一马当先 | 按张叠加 | 每张各让目标骰再计 1 次 | ScoreCalculator.cs:174、183 |
| C65 | 压轴好戏 | 按张叠加 | 每张各让目标骰再计 2 次 | ScoreCalculator.cs:175、183 |
| C66 | 小小回声 | 按张叠加 | 每张各让目标骰再计 1 次 | ScoreCalculator.cs:176、183 |
| C67 | 掷地有声 | 按张叠加 | 每张各让目标骰再计 1 次 | ScoreCalculator.cs:176、183 |
| C68 | 谢幕 | 按张叠加 | 每张各让目标骰再计 1 次 | ScoreCalculator.cs:177、183 |
| C69 | 红色共鸣 | 按张叠加 | 每张各让目标骰再计 1 次 | ScoreCalculator.cs:178、183 |
| C70 | 板凳深度 | 按张叠加 | 每张每颗留池骰 +6 基础分 | ScoreCalculator.cs:194 |
| C71 | 幕后推手 | 按张叠加 | 每张 ×1.5^(留池 6 点颗数) | ScoreCalculator.cs:241 |
| C72 | 厚积薄发 | 按张叠加 | 每张加一次留池点数和×2 | ScoreCalculator.cs:195 |
| C73 | 场外红人 | 只生效一次 | 留池红骰并入红组只算一次 | ScoreCalculator.cs:66 |
| C74 | 空城计 | 按张叠加 | 每张 ×3，多张相乘 | ScoreCalculator.cs:11、224 |
| C75 | 一锤定音 | 按张叠加 | 每张 ×3，多张相乘 | ScoreCalculator.cs:11、224 |
| C76 | 越挫越勇 | 按张叠加 | 每张每多投一次 +10 基础分 | ScoreCalculator.cs:196 |
| C77 | 孤注一掷 | 按张叠加 | 每张 ×2，多张相乘 | ScoreCalculator.cs:11、224 |
| C78 | 再给一次 | 按张叠加 | 每张每手投掷 +1 | ControllerRules.cs:93 |
| C79 | 多交一手 | 按张叠加 | 每张每关提交 +1 | ControllerRules.cs:101 |
| C80 | 天生一对 | 按张叠加 | 每张每对和为 7 的骰 +14 基础分 | ScoreCalculator.cs:197 |
| C81 | 散兵游勇 | 按张叠加 | 每张 ×3，多张相乘 | ScoreCalculator.cs:11、224 |
| C82 | 满堂彩 | 按张叠加 | 每张 ×3，多张相乘 | ScoreCalculator.cs:11、224 |
| C83 | 顺水推舟 | 按张叠加 | 每张 ×3，多张相乘 | ScoreCalculator.cs:11、224 |
| C84 | 成双成对 | 按张叠加 | 每张牌型倍率 +1.5 | ScoreCalculator.cs:205 |
| C85 | 三足鼎立 | 按张叠加 | 每张牌型倍率 +3 | ScoreCalculator.cs:205 |
| C86 | 三三得九 | 按张叠加 | 每张 ×4，多张相乘 | ScoreCalculator.cs:11、224 |
| C87 | 得寸进尺 | 按张叠加 | 每张 ×1.5^(同点超过 3 的颗数) | ScoreCalculator.cs:242 |
| C88 | 杂牌军 | 按张叠加 | 每张 ×3，多张相乘 | ScoreCalculator.cs:11、224 |
| C89 | 金玉满堂 | 按张叠加 | 每张 ×1.5^(黄金面颗数) | ScoreCalculator.cs:243 |
| C90 | 玻璃心 | 按张叠加 | 每张各自记成长值（玻璃骰碎裂时每张各自成长），倍率按各自的值相乘 | ScoreCalculator.cs:231；ControllerRules.cs:50 |
| C91 | 纹理大师 | 按张叠加 | 每张对符合条件的骰子各 +8 点数贡献 | ScoreExplanation.cs:42 |
| C92 | 纯色主义 | 按张叠加 | 每张对符合条件的骰子各 +10 点数贡献 | ScoreExplanation.cs:43 |
| C93 | 始终如一 | 按张叠加 | 每张让六面同点的骰子贡献 ×2 | ScoreExplanation.cs:51 |
| C94 | 表里如一 | 按张叠加 | 每张各加一次对面点数 | ScoreExplanation.cs:44 |
| C95 | 铁饭碗 | 按张叠加 | 每张每关结算 +3 金币 | GameSession.Submission.cs:339 |
| C96 | 利滚利 | 按张叠加 | 每张计息本金上限 +25 | GameSession.cs:217 |
| C97 | 以战养战 | 按张叠加 | 每张每次触发 +3 金币；每关最多触发 2 次（次数不随张数增加） | GameSession.Submission.cs:58–66 |
| C98 | 砍价高手 | 按张叠加 | 每张让控制器与骰面改造 −1（最低 1）；本店买下的卡牌离店前卖价不超过实际支付价格，所以不能同店套利（第 1.1 节第 9 条） | GameSession.Shop.cs:57、67–73 |
| C99 | 聚宝盆 | 按张叠加 | 每张每关让持有的每件控制器卖价 +1 | GameSession.Submission.cs:240–245 |
| C100 | 祭坛 | 其他 | 每张真实祭坛从左到右各摧毁自己右邻一件（被摧毁的祭坛不再行动），各自成长 | ControllerRules.cs:64–73；GameSession.cs:358–364 |
| C101 | 双刃剑 | 按张叠加 | 每张 ×4、每关提交 −1（最少 1） | ScoreCalculator.cs:11；ControllerRules.cs:101 |
| C102 | 背水一战 | 按张叠加 | 每张 ×3，多张相乘 | ScoreCalculator.cs:11、224 |
| C103 | 精兵简政 | 按张叠加 | 每张 ×2 或 ×4 | ScoreCalculator.cs:244 |
| C104 | 红蓝相间 | 按张叠加 | 每张 ×3，多张相乘 | ScoreCalculator.cs:11、224 |
| C105 | 泾渭分明 | 按张叠加 | 每张 ×2，多张相乘 | ScoreCalculator.cs:11、224 |
| C106 | 对称美学 | 按张叠加 | 每张 ×2，多张相乘 | ScoreCalculator.cs:11、224 |
| C107 | 顺藤摸瓜 | 按张叠加 | 每张对符合条件的骰子各 +10 点数贡献 | ScoreExplanation.cs:45 |
| C108 | 排头兵 | 按张叠加 | 每张各加一次第一颗点数×4 | ScoreCalculator.cs:198 |
| C109 | 中流砥柱 | 按张叠加 | 每张各让目标骰再计 2 次 | ScoreCalculator.cs:179、183 |
| C110 | 百宝袋 | 按张叠加 | 每张每关各给一个道具（道具栏满时不给） | GameSession.cs:418–428 |
| C111 | 杂货郎 | 按张叠加 | 每张让赠礼组可拿件数 +1（候选仍是 3 个，所以最多 3 件）；仍只有一个赠礼组 | GameSession.Shop.cs:146–155 |
| C112 | 滥竽充数 | 其他 | 按张执行，每张每关各给一张；候选仍按卡面文字排除已持有的普通卡（本版未改），需要空的普通栏位 | GameSession.Submission.cs:294–304 |
| C113 | 铁匠铺 | 按张叠加 | 同 C111，赠礼为骰面改造 | GameSession.Shop.cs:146–155 |
| C114 | 点化 | 按张叠加 | 每张首领通关后各附魔一件没有附魔的其他控制器（可以是另一张同名卡） | GameSession.Submission.cs:305–308；GameSession.Shop.cs:251–258 |
| C115 | 无心插柳 | 按张叠加 | 每张每关各做 2 次 | GameSession.Submission.cs:281–293 |
| C116 | 招兵买马 | 按张叠加 | 每张首领通关后各给一颗新骰（永久骰上限内） | GameSession.Submission.cs:309–317 |
| C117 | 点金手 | 只生效一次 | 每张各产生一条镀金变更，同一面镀两次结果相同 | ScoreCalculator.cs:279 |
| C118 | 表里同色 | 只生效一次 | 每张各产生一条染色变更，结果相同 | ScoreCalculator.cs:280 |
| C119 | 潜移默化 | 只生效一次 | 每张都按冻结面改同一个面，结果相同 | ScoreCalculator.cs:281 |
| C120 | 拔苗助长 | 按张叠加 | 每张每关各做一轮（每轮重新找最小的面） | GameSession.Submission.cs:269–276 |
| C121 | 大事化小 | 按张叠加 | 每张每关各做一轮 | GameSession.Submission.cs:269–276 |
| C122 | 见五成群 | 按张叠加 | 每张各产生 ±1；都按冻结面计算，两张时 4 点会变成 6 点（越过 5）。与 v1.0 模仿复制时相同 | ScoreCalculator.cs:282 |
| C123 | 二三其德 | 按张叠加 | 每张各产生 ±1；两张时 1 点变 3 点，4 点变 2 点。与模仿复制时相同 | ScoreCalculator.cs:283 |
| C124 | 灌铅 | 按张叠加 | 每张权重 +1（上限 5） | ScoreCalculator.cs:284 |
| C125 | 抛光匠 | 只生效一次 | 变光滑的结果相同 | ScoreCalculator.cs:285 |
| C126 | 养精蓄锐 | 按张叠加 | 每张让留池骰朝上面 +1（上限 6） | ScoreCalculator.cs:286 |
| C127 | 移花接木 | 只生效一次 | 每张复制同样的数据，结果相同 | ScoreCalculator.cs:287–293 |
| C128 | 吹玻璃 | 按张叠加 | 每张真实卡每关各把一颗非玻璃永久骰变成玻璃（模仿不执行） | GameSession.cs:400–408 |
| C129 | 屠龙者 | 按张叠加 | 每张各自记成长值（首领通关时每张各自成长），倍率按各自的值相乘 | ScoreCalculator.cs:231；ControllerRules.cs:50 |
| C130 | 逆流而上 | 按张叠加 | 每张 ×3，多张相乘 | ScoreCalculator.cs:11、224 |
| C131 | 替罪羊 | 其他 | 每个首领关只消耗最左侧的一张真实替罪羊来解除减益，其余保留到以后的首领关 | GameSession.cs:201–207、366–375 |
| C132 | 高压锅 | 按张叠加 | 每张各自记成长值（完成高压挑战时每张各自成长），倍率按各自的值相乘 | ScoreCalculator.cs:231；ControllerRules.cs:50 |
| C133 | 赏金猎人 | 按张叠加 | 每张各付一次赏金 | GameSession.Submission.cs:337–339 |
| C134 | 物尽其用 | 按张叠加 | 每张各自记成长值（使用道具时每张各自成长），倍率按各自的值相乘 | ScoreCalculator.cs:231；ControllerRules.cs:50 |
| C135 | 囤积居奇 | 按张叠加 | 每张每个道具 +10 基础分 | ScoreCalculator.cs:199 |
| C136 | 雇佣兵 | 按张叠加 | 每张每关各加一颗临时骰（临时骰 3、活动骰 10 的上限内） | GameSession.cs:409–417 |
| C137 | 客串明星 | 按张叠加 | 每张对符合条件的骰子各 +15 点数贡献 | ScoreExplanation.cs:46 |
| C138 | 一掷千金 | 按张叠加 | 从左到右按冻结钱包的剩余额度判定：够 3 金币的每张 ×3 并扣 3 | ScoreCalculator.cs:149、11；GameSession.Submission.cs:99–104 |
| C139 | 富甲一方 | 按张叠加 | 每张 ×(1+0.25×⌊钱包÷10⌋，最高 ×3) | ScoreCalculator.cs:233 |
| C140 | 回收大王 | 只生效一次 | “持有时”开关：有任意一张就全价回收（本店取得的仍按半价，本店买下的再受买价封顶） | GameSession.Shop.cs:67–73 |
| C141 | 昙花一现 | 按张叠加 | 每张 ×(5−已过关数)÷2，各自倒数、到期各自自毁 | ScoreCalculator.cs:234；GameSession.Submission.cs:234–239 |
| C142 | 一条龙 | 按张叠加 | 每张牌型倍率 +10 | ScoreCalculator.cs:205 |
| C143 | 好事成双 | 按张叠加 | 每张各让目标骰再计 1 次 | ScoreCalculator.cs:180、183 |
| C144 | 偶数为王 | 按张叠加 | 每张 ×3，多张相乘 | ScoreCalculator.cs:11、224 |
| C145 | 防碎膜 | 只生效一次 | “持有时”开关：有任意一张就降为 2% | GameSession.cs:219 |
| C146 | 锦上添红 | 只生效一次 | 红组 +1 不重复（规格原文“多个来源不重复”） | ScoreCalculator.cs:67 |
| C147 | 观众席 | 按张叠加 | 每张 ×(1+0.25×留池骰数) | ScoreCalculator.cs:235 |
| C148 | 见好就收 | 按张叠加 | 每张 ×(1+0.5×剩余投掷) | ScoreCalculator.cs:236 |
| C149 | 单刀赴会 | 按张叠加 | 每张 ×4，多张相乘 | ScoreCalculator.cs:11、224 |
| C150 | 渐入佳境 | 按张叠加 | 每张 ×n | ScoreCalculator.cs:237 |
| C151 | 留白 | 按张叠加 | 每张 ×(1+0.5×空普通栏位)；每张同名卡自己也占栏位 | ScoreCalculator.cs:238 |
| C152 | 草根逆袭 | 按张叠加 | 每张 ×(1+0.25×普通稀有度件数)，件数含全部同名卡 | ScoreCalculator.cs:239、75 |
| C153 | 开门红 | 按张叠加 | 每张 ×3，多张相乘 | ScoreCalculator.cs:11、224 |

## 5 需要用户决定

**无限循环检查**：逐项检查了每次提交、每关、每家商店的收入（C01、C95、C97、C99、C52、C133、C96），免费获得再出售（C110、C111、C112、C113、战利品，配合 C140），按事件成长（C05、C62、C76、C43、C61、C51、C90、C128、C100），骰面变更（有上下限），预算与容量（C21、C22 上限 8，C09、C101 最少 1）。都受每手、每关、每家商店的次数或数值上限约束，**没有发现会无限刷钱或无限成长的循环**。下面几条有上限，但与以前的设计说法不一致或者是取舍，请你决定；实施者没有做任何平衡修改。第 1 条用户已选 D、第 3 条协调者已决定，均已实现；其余三条仍待决定。

1. ~~多张砍价高手（C98）可以同店买进再卖出赚钱。~~ **用户 2026-11-16 选择 D：同店买入的卡牌离店前卖价不超过实际支付价格。** 已实现，见第 1.1 节第 9 条；砍价高手仍按张叠加，卡面文字由主 agent 更新。原问题（留档）：p1.0.0 的说法是“买进再卖出不会赚钱（含砍价高手）”，当时最多只有 2 个来源（本体加一张模仿）。现在每张 C98 都 −1：买价 = max(1, 标价 − 来源数)，同店卖价 = ⌊标价 ÷ 2⌋。来源数超过 ⌈标价 ÷ 2⌉ 就有赚头，例如 3 个来源时标价 4 的卡 1 金币买、2 金币卖。当时的选项：A 保持（由 W07 测量）；B 砍价高手改为“持有时”开关，多张只算一次；C 折扣总额封顶（例如 −2）；D 同店取得的卡回收价不超过实际买价。
2. **滥竽充数（C112）仍只给未持有的普通卡。** 卡面写着“未持有”，本版没改。可选：A 保持；B 也可以给已持有的普通卡（那样它也会产生同名卡）。
3. ~~L4、L6 要拿满 3 件才算领完。~~ **协调者决定：领取第一件后即解锁商店，其余件数离店前仍可领取。**（2026-11-16，按选项 B 调整：剩下的件数不在第一件时作废，而是在离开商店时作废。理由：用户要的是首领关后马上做出战利品选择，不是强迫拿满；道具栏只有 2 格时拿满 L6 还得先卖道具。）初版的防卡死规则随之取消，见第 2.1 节第 4、5 条。
4. **见五成群（C122）、二三其德（C123）多张时会越过目标。** 每张都按冻结的面各算 ±1，两张见五成群会把 4 点变成 6 点。这与 v1.0 百变模仿复制它们时的结果相同，本版保持。可选：A 保持；B 多张合计后再夹到目标（5 点、2–3 点）。
5. **旧存档的混合情况**（第 3.1 节第 4 条）：其他版本的活动存档被删后恢复当前版本的备份时，沿用原来的备份提示。可选：A 保持；B 这种情况也不提示。

## 6 改写的测试

都按新规则改写，没有删除测试、没有降低阈值。方法名描述旧规则的一并改名；参数化测试只改写对应的行。

| 测试 | 原期望 | 新期望 | 原因 |
| --- | --- | --- | --- |
| `V05SessionTests.V05_SlotlessFitsBeyondFourAndReplaceUsesFinalEquipment` → `…AndASameNameCopyNeedsItsOwnSlot` | 卖掉一张后，C06 基础分附魔替换已有的不占栏位 C06：净价 2（6 − 抵扣 4），共 5 件 | 净价 6（不抵扣），共 6 件，两张 C06 都在（不占栏位 + 基础分），普通栏位 5 | 不替换、不抵扣 |
| `V05SessionTests.V05_ReplacingAtTenDoesNotCreateEleventhInstanceAndNewEleventhRejects` → `V05_AtTenInstancesASameNameCardIsAnEleventhAndRejectsLikeAnyNewCard` | 10 件时买同名 C02 是替换，仍为 10 件；新的 C12 被拒 | 同名 C02 是第 11 件，被拒（“已满”）且状态不变；C12 被拒；卖掉一件后 C12 可买，回到 10 件 | 同名卡要自己的位置 |
| `V05SessionTests.V05_ReplacementInsufficientMoneyAndWriteFailureAreAtomic` → `V05_SameNameCopyInsufficientMoneyAndWriteFailureAreAtomic` | 5 金币不够净价 10（13 − 3）；重试保存后 90 金币，只剩新的 C02 | 12 金币不够 13（不抵扣）；重试后 87 金币，两张 C02（无附魔 + 倍率附魔），旧的那张位置不变 | 不替换、不抵扣 |
| `V05SessionTests.V05_ShopExcludesSlotlessNonSubmitOwnedButCanOfferDifferentScoringEnchant` → `V05_ShopOffersOwnedCardsWithTheOrdinaryEnchantmentDraw` | 已持有 C05、C02 时，它们的报价只能是“不同于已有附魔的非空附魔”；至少出现一次同名替换报价 | 已持有的卡照常上架，附魔走该卡的常规池；C05 仍不会有计分附魔；附魔都合法；至少出现一次已持有卡的报价，且出现过以前会被排除的“无附魔 / 不占栏位” | 货架不排除已持有、附魔按未持有抽 |
| `V06SessionTests.V06_MatureFundReplacementCreditsNegativeNetKeepsPositionAndNewState` → `V06_MatureFundIsNotTradedInAndASecondFundKeepsItsOwnState` | 买同名 C52 时旧卡（基金 30）抵扣 32，净价 −23，0 金币变 23；新卡在原位置、基金 0；人员流动 +0.5 | 净价 9，0 金币时被拒；20 金币时追加在最右，剩 11；旧 C52 位置与基金 30 不变，新卡基金 0；人员流动不变（3）；单独卖掉旧卡得 32，人员流动变 4 | 不替换、不抵扣，买入不算卖出 |
| `V06SessionTests.V06_C51SelfReplacementDoesNotGrowNewInstanceAndItemSaleDoesNotGrow` → `V06_C51CopyPurchaseGrowsNothingAndItemSaleDoesNotGrow` | 买同名 C51 替换掉成长到 20 的旧卡，新卡为 3；卖出道具不成长；卖出 C41 后新卡变 4 | 新卡追加，[20, 3(C41), 3]；卖出道具不成长；卖出 C41 后两张 C51 各自成长，为 21 和 4 | 同名卡并存、各自成长 |
| `V09SessionTests.V09_C98LowersControllerAndDieUpgradePricesPerSourceWithAFloorOfOne`（同名） | 同名 C02 倍率附魔：标价 12、抵扣 3、净价 9 | 标价 12、抵扣 0、净价 12；买下后扣 12 金币，持有两张 C02 | 不抵扣 |
| `V09SessionTests.V09_ReplacingAGrownCardTradesInItsBonusAndStartsFromZero` → `V09_ASecondCopyOfAGrownCardStartsFromZeroAndTheFirstKeepsItsGrowthAndBonus` | 旧 C55 抵扣 5（2 + 卖价加成 3），净价 1，剩 99；只剩新卡，成长与加成都为 0 | 净价 6，剩 94；两张：旧卡保留成长 40 与加成 3（单独卖得 5，回到 99），新卡从 0 开始 | 不替换、不抵扣 |
| `V09SessionTests.V09_ANormalSaveEnforcesItsOwnSlotCountAndUniqueControllers` → `…AndAcceptsSameNameCopies` | 普通存档里两张 C55 必须报错（“只允许调试”） | 两张 C55 的普通存档正常读取（不是调试局）；6 张 C55 放 5 个栏位仍报错 | 普通对局允许同名多张 |
| `V10SessionTests.V10_LootCanBeCashedForFiveGoldInsteadAndSurvivesARefresh` → `…SurvivesALaterRefresh` | 未领战利品时刷新货架成功，战利品报价不变 | 未领时刷新被拒（“先领取首领战利品。”，状态不变）；折现后刷新成功，战利品报价不变 | 战利品先领 |
| `V10SessionTests.V10R_TheTalismanTakesTheTargetFromTheConfigurationAndRechecksTheQualifiedFlag`（同名） | 第 3 关后的商店直接离开 | 先折现战利品再离开；破咒符相关的断言不变 | 战利品先领 |
| `V10SessionTests.V10R_TheRecyclerPaysFullPriceOnlyForControllersThatWereNotAcquiredInThisShop`（同名） | 同名 C46 替换：旧卡按全价抵扣，替换后的新卡按半价回收 | 不抵扣，全价支付；两张 C46：之前持有的那张按全价回收（回收大王），本店新买的按半价 | 不替换、不抵扣 |
| `ItemAndSaveTests.TwentyFourStageFlowSupportsEightRiskBranchesAndNoFinalShop`（同名） | 每家商店直接离开 | 7 个首领关后的商店：先离开被拒（“先领取首领战利品。”），折现后再离开；`IsLootPending` 恰好在首领关后为真 | 战利品先领 |
| `V06RulesTests.EquipmentSeparatesFourOrdinarySlotsTenInstancesAndDuplicateIds`（在 `V05RulesTests.cs`，同名） | 普通对局里重复的定义编号报错 | 重复的定义编号合法；重复的实例编号仍报错 | 普通对局允许同名多张 |
| `V06SourceBoundaryTests.StateLegalityDebugDuplicatesAndRetiredIdsAreDistinctBoundaries`（同名） | 两张 C46 在普通装备里报错，调试允许 | 普通与调试都允许；两张共用一个实例编号在两种情况下都报错 | 普通对局允许同名多张 |
| `AtomicSaveStoreTests.SAVE_V05_IsolatesBothLegacySchema2FilesAcrossNewRunBackupRotation` → `SAVE_V11_BothSchema2FilesAreDeletedWithoutNoticeAndNewSavesRotateNormally` | 读档返回空并提示“…开始新挑战”，生成并一直保留 2 个 `.legacy-schema2-*` 副本 | 读档返回空、无提示、`LastIncompatibleSave` 为空；活动存档与备份被删，没有旧版副本与隔离副本；之后的保存照常轮换备份 | 旧档直接删除 |
| `AtomicSaveStoreTests.SAVE_BothFilesCorruptReturnsNullAndNoticeWithoutOverwritingEvidence("schema")` | 结构版本 +1 视为损坏：有提示，两个文件逐字节不变 | 结构版本 +1 是其他版本：两个文件被删，无提示（"hash"、"structure" 两行不变） | 其他版本的存档删除，不再当作损坏 |
| `AtomicSaveStoreTests.SAVE_BackupRecoveryThenSavePreservesLastValidBackup("schema")` | 恢复备份后，下次保存把结构版本 +1 的活动存档复制为隔离副本 | 读档时删除该活动存档（`LastDeletedSaveFiles` 只有它），恢复备份并给出备份提示；下次保存不生成隔离副本，备份不变（另两行不变） | 同上 |
| `AtomicSaveStoreTests.SAVE_DirectWriteWithoutLoadStillPreservesBackupWhenActiveIsCorrupt("schema")` | 不读档直接保存：结构版本 +1 的活动存档被隔离 | 直接覆盖，不隔离；备份不变（另两行不变） | 同上 |
| `AtomicSaveMetadata07Tests.SAVE_ART07_MetadataReportsFirstRealVersionAndPreservesBothLegacyFiles` → `SAVE_V11_OldVersionFilesAreDeletedAndNeitherMetadataNorNoticeIsReported` | `LastIncompatibleSave` 报告 schema 2 / p0.4.0 且已保留；两个文件不变；两个旧版副本 | `LastIncompatibleSave` 为空、无提示；两个文件被删，没有旧版副本；之后的保存照常 | 旧档直接删除，不再提醒 |
| `AtomicSaveMetadata07Tests.SAVE_ART07_ValidActiveIgnoresIncompatibleBackupAndSaveProbeDoesNotSetLoadMetadata` → `SAVE_ART07_ValidActiveLoadsAnOldBackupIsDeletedAndSaveProbeDoesNotSetLoadMetadata` | 有效的活动存档读取成功，旧版本备份原样保留 | 有效的活动存档读取成功且不变，旧版本备份被删；在旧版本活动存档上保存不设元数据、不隔离、不生成备份 | 旧档直接删除 |
| `V10SessionTests.V11_AMultiPickLootHoldsTheShopUntilEveryPickIsTakenOrNothingClaimableIsLeft` → `V11_AMultiPickLootUnlocksTheShopAtTheFirstPickAndKeepsTheRestClaimableUntilLeaving`（本版新增后又改写） | L4 领一件后仍拦截离店，领满 3 件才解锁；剩下的骰面改造都没有合法目标时不再拦截（防卡死） | L4 领第一件后立即解锁：货架可买、可刷新，不能再折现，第二件仍可领，离店后第三件作废；L6 在 2 格道具栏时领两件即可离店，不必卖道具 | 协调者决定（第 5 节第 3 条），防卡死规则取消 |
| `V10SessionTests.V11_TheLootPageCanSellCardsOrItemsToMakeRoomForTheLoot`（本版新增，同名） | 栏位满时领取被拒，只断言提示含“已满” | 断言完整的新提示“普通卡牌栏位或总张数已满，可先出售卡牌。”与“道具栏位已满，可先出售道具。” | 用词改为卡牌、栏位（第 10 节），断言更严 |

另：`ShopTests.CONTENT_ControllerPurchaseDuplicateAndSell` 不需要改写。它名字里的“Duplicate”指重复的命令回执，第二次购买针对的是同一个已售出的报价，新规则下照样通过。

## 7 新增的测试

| 验收条件 | 测试 |
| --- | --- |
| F-DUP-01 | `V10SessionTests.V11_AnOwnedCardCanBeTheFirstShelfOfferBecauseNothingOwnedIsExcluded`（固定票号，按规则文字推出货架第一张就是已持有的 C01）；`V11_OwnedCardsKeepAppearingOnShelvesByWeightAndNoShelfEverHoldsTwoSameNameOffers`（200 个种子：已持有卡出现次数在按权重推算值的 0.5–1.5 倍内；每个货架、刷新后、扩容后都没有同名报价）；`V11_RareAndEnchantedLootOfferOwnedCardsButNeverACardOnTheShelf`（L1 20 个种子、L2 30 个种子） |
| F-DUP-02 | `V11_TwoCopiesTakeTwoSlotsGrowApartAndSellApartWithoutReplacementOrTradeIn`；`V11_SessionEffectsStackPerCopyOrApplyOnceAsTheDeltaTableSays`（C78、C79、C21、C95、C111 按张；C140、C145 一次；C131 只用最左侧一张；两张 C141 各自倒数与到期） |
| F-DUP-04 | `V11_SameNameCopiesInANormalRunSurviveRestoresWithTheirScoreHistory`（普通对局，读档时按冻结快照重算计分）；`V10PersistenceTests.SAVE_V11_SameNameCopiesAndAPendingLootShopSurviveARealJsonRoundTrip`（Unity JsonUtility 实际存读） |
| 计分 | `ControllerScoreTests`：11 条 `CONTENT-DUP-*`（倍率、逐骰加值、末栏加值、基础分、再计、牌型倍率加值、控制欲按件数、三个开关类各一次）与 `CopiesKeepTheirOwnGrowthColourRandomOutcomeAndGoldBudget`（成长、C45 颜色、C53 结果、C138 预算、成长计划）。期望值按规则文字手算 |
| F-LOOT-01 | `V11_PendingBossLootRefusesEveryOtherShopActionButSellingAndReordering`；`V11_AMultiPickLootUnlocksTheShopAtTheFirstPickAndKeepsTheRestClaimableUntilLeaving` |
| F-LOOT-02 | `V11_AnOrdinaryRunRestoredInItsLootShopKeepsTheGateUntilTheLootIsCashed`；`V11_TheLootPageCanSellCardsOrItemsToMakeRoomForTheLoot`；上面的持久化测试 |
| F-SAVE-01 | `AtomicSaveStoreTests.SAVE_V11_AConfigVersionChangeAloneDeletesActiveBackupAndEveryLegacyCopyButNothingElse`、`SAVE_V11_LegacyCopiesWithoutAnyActiveFileArePurgedSilently`、`SAVE_V11_TheDebugSlotDeletesItsOwnOldFilesAndLeavesTheNormalSlotAlone`，以及第 6 节改写的存档测试 |
| F-SAVE-02 | `SAVE_V11_AnOldActiveIsDeletedAndTheCurrentBackupIsStillRestored`、`SAVE_V11_ADamagedActiveOfThisVersionIsKeptAndQuarantinedWhileAnOldBackupIsDeleted`，以及原有的当前版本存档测试（未改动） |
| F-RULE-REG | 原有的 `RulesTests`（160 条）、`V09GoldenTests`（260 条）、`V10GoldenTests`（446 条），计分代码未改动 |
| TX-08（文字方案 §5 第 1 处） | `ItemAndSaveTests.TX08_ASecondUseInOneHandNamesTheItemAlreadyActive`：红色浪潮、大数多来、小数多来、灵光一闪、蓝色浪潮同一手第二次使用时，提示说出的是这件道具本身 |
| 同店卖价封顶（第 1.1 节第 9 条，用户选择 D） | `V10SessionTests.V11_ThreeOrMoreBargainersCanNoLongerBuyAndResellAtAProfitInTheSameShop`（三张砍价高手：标价 4 的卡 1 金币买、卖价 1，买卖后金币不变；五张时标价 8 的卡 3 金币买、卖价 3；没有折扣时封顶不起作用）；`V11_TheResaleCapEndsWhenThePlayerLeavesTheShop`（离店后卖价回到标价的一半 2，下一家店卖得 2）；`V11_AFreeLootCardIsNotCappedByItsZeroPrice`；`V11_TheRecyclerKeepsHalfPriceForCardsBoughtHereAndTheCapAppliesOnTop`（之前持有的卡全价；本店买的半价再封顶为 1；离店后全价 4）；`V11_ThePaidPriceSurvivesARestoreAndTamperedRecordsAreRejected`（读档保留；缺记录、超过标价、为 0、重复、不属于本店买下的卡、空记录、记录表缺失、战利品带记录都被拒绝，且拒绝原因都是“本店买价记录无效。”）；`V10PersistenceTests.SAVE_V11_SameNameCopiesAndAPendingLootShopSurviveARealJsonRoundTrip` 增加买下一张卡后经 JsonUtility 存读、实际支付价格保留。去掉封顶时前四条测试失败（已核对） |

## 8 验证与交接

### 8.1 已运行（本分支，不含 Unity）

| 项目 | 基线 0110308 | 初版（8a71dde） | 修订（基于 codex/v1.1 的 ab3abbe4，配置 p1.1.0） |
| --- | --- | --- | --- |
| `Session/Validation~/SessionValidation`（`Tests/EditMode/Session`） | 433 / 433 通过 | 443 / 443 通过 | 453 / 453 通过（新增 5 条 TX-08 用例、5 条同店卖价封顶测试） |
| `Rules/Validation~/RulesNUnitValidation`（`Tests/EditMode/Rules`） | 1207 / 1207 通过 | 1219 / 1219 通过（含 `V09GoldenTests` 261、`V10GoldenTests` 448 条记录） | 1219 / 1219 通过 |
| `Rules/Validation~/CoreRulesValidation` | 失败：`C11 required counts did not deserialize.` | 同样失败，原因未变（该宿主没有调用 `PrepareJsonForUnity`，与本次改动无关） | 同样失败，原因未变 |
| `DebugRunTrace` | 失败：序号 22（第 23 关，高压）未达标 | `DEBUG_24_PASSED`：24 关、23 家商店、8 次高压、7 次战利品折现、182 条命令逐条读档与重复命令检查 | 相同 |
| `NormalRunSearch`（种子 20261028 起 8 局） | 种子 20261029 因“满员”减益被拒而中止 | 8 局都正常结束（最好一局到第 11 关，经过 3 家战利品商店） | 相同 |
| `NormalRunReplay`（重放上一项生成的轨迹） | 新生成的轨迹可以重放；仓库里旧的轨迹文件已过期（“Target differs”） | `NORMAL_REPLAY_PASSED`：161 步逐条读档与重复命令检查 | 相同 |
| 持久化测试冒烟（临时 .NET 宿主，用 System.Text.Json 代替 JsonUtility，不是 Unity 结果） | — | 33 / 33；同一批测试换成基线的 `AtomicSaveStore` 时，11 条 v1.1 存档用例失败、其余 22 条通过 | 33 / 33 |
| 对 Unity 6000.6.7f1 程序集的编译检查（Core、Runtime、UI、EditMode、PlayMode） | — | 0 错误 | 0 错误 |

**未运行**：Unity EditMode 与 PlayMode（由主 agent 运行）。机器人运行时的输出写在临时目录，没有改动 `evidence/` 下的旧证据。机器人只折现战利品，不依赖拿满，所以第 2.1 节第 4 条的修改不需要改它们。

### 8.2 机器人的修改

`DebugRunTrace` 与 `NormalRunSearch` 在基线就已失败；`NormalRunReplay` 对仓库里的旧轨迹也失败（轨迹已过期），对新生成的轨迹能通过。为了用它们验证战利品先领，做了最小修改：

- `DebugRunTrace`：首领关后的商店先折现战利品再离开，并检查只有首领关后才拦截；固定场景加入第五张卡 C46（与 EditMode 里的同一场景 `TwentyFourStageFlow…` 在 p0.9.0 的修正相同），否则到不了第 24 关。
- `NormalRunSearch`：进商店先折现战利品；遵守“满员”（只考虑占满提交栏的组合）与“徒手”（不用道具）；不买吹玻璃（策略里没有玻璃骰模型，原来遇到玻璃骰会主动报错）。
- `NormalRunReplay`：未改。

### 8.3 需要主 agent 跟进

- 配置（另一位实施者负责的文件）：见第 9 节。
- `Core/` 里的提示已按 D2 改为“卡牌”“栏位”（第 10 节）。PlayMode 里有两处断言旧写法，需要主 agent 改：`FailureAndInputPlayModeTests.cs` 第 108 行断言提示含“控制器”（应改为“卡牌”），第 114 行断言含“槽位”（应改为“栏位”）。
- 同店卖价封顶（第 1.1 节第 9 条）：会话报告的卖价（`GetControllerSalePrice`、`ControllerSalePriceIn`，界面的 `OwnedSalePrice10` 用的是后者）都已封顶，所以出售按钮、出售确认和“当前卖价”是对的。界面有两处自己用纯函数 `ControllerRules.SalePrice` 算卖价文字，需要主 agent 处理：
  1. `UI/GameApp.Art07Shop.cs` 第 103 行 `Art07OfferState` 给成长基金（C52）显示“卖价 = 标价的一半”。用在货架报价上时，这是离店后的卖价，本店买下后离店前可能更低（可加注“本店买下后离店前不超过买价”）；它也用在买下后的交易摘要里（`UI/GameApp.Art07Effects.cs` 第 481、510 行），那里已是持有的卡牌，应改用 `OwnedSalePrice10`。
  2. `UI/GameApp.V05.cs` 第 168 行成长基金、百变模仿的“本实例基金”说明，按“会话卖价是否高于纯函数卖价”标注“（回收大王：标价全额）/（标价的一半）”。被封顶的卡牌会被错标为“标价的一半”（例如标价 4、买价 1 时写“基础卖价 1（标价的一半）”），需要第三种标注，例如“不超过买价”，可读 `ViewState.shop.paidControllerPrices` 判断。
- 界面里按卡名找第一张、替换文案与战利品入口的具体位置，见交接报告。

## 9 配置建议（未改动，`P0Config.json` 与配置校验不归本任务）

| 字段 | 现状 | 建议 |
| --- | --- | --- |
| `configVersion`（或 `saveSchemaVersion`） | codex/v1.1 已改为 p1.1.0（存档结构版本仍为 6） | 已满足：p1.0.0 的存档会被当作其他版本删除。另：第 1.1 节第 9 条给商店状态加了 `paidControllerPrices`，在这之前用 p1.1.0 开发版本留下、停在商店且在本店货架买过卡牌的存档，读档时会被拒绝（“本店买价记录无效。”），不会被当作其他版本删除；如果要让这类开发存档也被自动删除，可把 `saveSchemaVersion` 升到 7（由配置负责人决定） |
| `controllers[].maxOwnedNormal` | 全部为 1，配置校验要求必须为 1，规则代码不读取 | 与新规则矛盾。可删除该字段，或改为 0 表示不限，同时改 `ConfigValidation.cs`（codex/v1.1 第 68 行） |
| `shop.controllersUniqueOwned`、`allowDifferentEnchantReplacement`、`replacementCredit`、`replacementCountsAsOneControllerSale`、`firstControllerGuaranteeUnownedOnly` | 描述旧规则（唯一持有、替换、抵扣）；`replacementCredit` 与 `replacementCountsAsOneControllerSale` 被配置校验固定（codex/v1.1 第 92 行），规则代码不读取 | 改为 false / "none" 或删除，同时改配置校验 |
| `debug.allowDuplicateControllerIds` | true，被配置校验固定 | 可保留；普通对局现在也允许 |
| C112 的描述与细则（“未持有”“只给还没有的常见卡”） | 与保留的规则一致 | 若第 5 节第 2 条选 B，再改文字 |

## 10 Core 提示的用词（D2）与道具提示修正

`Core/` 里会显示给玩家的提示（操作被拒的原因、商店的购买限制、读档失败时“读取详情”里的校验原因、手册行）按决定 D2 与[文字方案](../../proposals/2026-11-16-text-readability.md) §2 的用语改写：“控制器”改为“卡牌”，“槽”改为“栏位”，卡牌的数量用“张”。只改文字，代码标识符不改名。

| 位置 | 原文 | 新文 |
| --- | --- | --- |
| `GameSession.Shop.cs` 刷新货架 | 没有合法控制器候选。 | 没有合法的卡牌候选。 |
| `GameSession.Shop.cs` 购买卡牌 | 控制器报价实例无效。 | 卡牌报价实例无效。 |
| `GameSession.Shop.cs` 购买卡牌 | 普通控制器槽或总件数已满，可先出售控制器。 | 普通卡牌栏位或总张数已满，可先出售卡牌。 |
| `GameSession.Shop.cs` 购买卡牌 | 控制器附魔不合法。 | 卡牌附魔不合法。 |
| `GameSession.Shop.cs` 购买道具 | 道具槽位已满，可先出售道具。 | 道具栏位已满，可先出售道具。 |
| `GameSession.Shop.cs` 附魔台 | 没有可以附魔的控制器。 | 没有可以附魔的卡牌。 |
| `GameSession.cs` 调序预览 | 控制器顺序无效。 | 卡牌顺序无效。 |
| `GameSession.Debug.cs` 卡牌调序 | 控制器调序必须保留同一组实例。 | 卡牌调序必须保留同一组实例。 |
| `GameSession.Validation.cs` 存档校验 | 控制器槽位无效。 | 卡牌栏位无效。 |
| 同上 | 控制器实例身份无效。 | 卡牌实例身份无效。 |
| 同上 | 失效控制器名单无效。 | 失效卡牌名单无效。 |
| 同上 | 失效控制器不在装备中。 | 失效卡牌不在装备中。 |
| 同上 | 结算控制器收益无效。 | 结算卡牌收益无效。 |
| 同上 | 控制器动态状态无效。 | 卡牌动态状态无效。 |
| 同上 | 控制器成长值无效。 | 卡牌成长值无效。 |
| 同上 | 控制器颜色状态无效。 | 卡牌颜色状态无效。 |
| 同上 | 非控制器报价携带了控制器数据。 | 非卡牌报价携带了卡牌数据。 |
| 同上 | 控制器商品实例无效。 | 卡牌商品实例无效。 |
| `HandbookRules.cs` 手册“散点”行 | 不与其他牌型相加；独立颜色、红组和控制器仍可生效 | 不与其他牌型相加；独立颜色、红组和卡牌仍可生效 |
| `GameSession.Submission.cs` 道具“小数多来”同一手第二次使用 | 本手缩小射线已经生效。 | 本手小数多来已经生效。 |

- 最后一行是[文字方案](../../proposals/2026-11-16-text-readability.md) §5 第 1 处错误：缩小射线是卡牌 C37，不是道具。相邻的道具提示（红色浪潮、大数多来、灵光一闪、蓝色浪潮、临时骰子、遥控骰子、破咒符、点金笔）都核对过，名字正确，没有同样的错误；扩容符的提示不提道具名。
- 测试：EditMode 里断言这些提示的只有两处，都只断言“已满”，新文字仍然包含；`V11_TheLootPageCanSell…` 改为断言两条完整的新提示（第 6 节）；新增 `TX08_ASecondUseInOneHandNamesTheItemAlreadyActive`（第 7 节）。PlayMode 里断言旧写法的两处见第 8.3 节。
- 没有改的：手册颜色行的“不计入牌型或C43结构主牌型”仍带内部编号 C43（文字方案 §2.3 不允许内部编号出现在玩家画面上）；英文的内部校验信息（例如 “Invalid controller identities or limits.”）理论上也可能出现在读档失败的“读取详情”里。这两类不属于“控制器→卡牌”的改名，留给文字整理一并处理。
