# W3b-2 备注：会话层的商店、道具、解锁命令与存档结构 8

工作包 W3b-2（[交接包](briefs/W3B2_SESSION_SHOP.md)）。依据 [规则增量](RULES_DELTA.md)。基线 `b3e6d145`，分支 `codex/v1.1.1`。旧测试改写见 [TEST_CHANGES.md](TEST_CHANGES.md) 的 W3b-2 一节。

五部分：规格疑点与处理、留给界面工作包的命令与查询、留给模拟器工作包的接口变化、没做完的事、附录（货架向量的独立脚本）。都没有改规格。

## 1. 规格疑点与处理

按重要程度排。每条写明现在按哪种读法实现。

| 编号 | 疑点 | 现在的实现 | 影响 |
| --- | --- | --- | --- |
| Q01 | 骰子脆化 U05 带着面 ID 来买怎么办。§7.1 写“只选一颗非玻璃永久骰，不选面”，例子只说“两个面 ID 不能用来买 U05”；p1.1.0 的代码收到一个面 ID 时照样接受、忽略它 | **带任何面 ID 都拒绝**（一个也拒），理由 `UpgradeTakesWholeDie`。旧的单面字段 `targetFaceId` 按 §12.1 等价于单元素数组，所以同样被拒 | 界面现状是 U05 不传面（`GameApp.ShopFacePick14.cs`、`Art07VisualRecorder.Transactions.cs:73`），不受影响。三条旧测试原先给 U05 也传了面，已改并登记。若协调者认为应当容忍一个多余的面 ID，只需放宽 `CheckUpgradeTargets` 的第一行 |
| Q02 | 钥匙串“余 0 时移除并记耗尽”要不要单独的回执种类 | 不加。耗尽 = 从背包移除并写进已有的 `consumedItemIds`（§12.1）；回执仍然只有那一条 `ItemUsed`，它的 `newValue` 为 0 就是耗尽 | 界面用 `ItemUsed.newValue == 0` 判断槽位腾出 |
| Q03 | 对钥匙串发 `UseItem` 怎么办 | 拒绝（理由 `KeyRingUsesUnlock`），不扣次数。§8 写“道具按钮也调用它（`UnlockDie`）” | 界面的钥匙串按钮必须发 `UnlockDie`。玩家不能指定“这次用钥匙串、留着免费次数”，来源由规则定（免费优先） |
| Q04 | 商店周期要不要存进 `ShopState`。§9.1 写“生成后同店固定 N”，§12.1 的字段表里没有它 | 不存，按已结束的关号现算：`min(8, (stageIndex + 1) / 3 + 1)`。商店开着的时候关号不会变 | 调试场景不能直接造一个“周期与关号不一致”的商店 |
| Q05 | 调试场景怎样“构造自选额度、报价上的追赶值” | 调试场景只能造一个进行中的关（现状如此，`selfStudyUsed` 也不在场景里），带不了商店。做法是把场景的 `stageIndex` 设到目标周期、打进商店：报价的追赶值随该周期生成，自选额度从 0 起算，在调试商店里做一笔自选就是“已用 1”。钥匙串次数与免费解锁标记可以直接写进场景 | 如果界面工作包需要“直接跳进某个商店状态”，得另加调试入口，本包没有做 |
| Q06 | `OwnedItem.usesRemaining` 的缺省值 | C# 字段缺省 1（I01–I12 的初值与存活值都是 1）。规则里取得道具的所有入口都走 `NewOwnedItem`，按配置的 `initialUses` 给（钥匙串 3）。调试场景里次数写 0 或负数表示“按新取得的算”，显式写的值由读档校验卡在 1 到 `initialUses` 之间 | 运行时与界面里直接 `new OwnedItem{…}` 的旧代码不用改就合法；但调试界面这样加的钥匙串只有 1 次，界面要么写 0（得 3 次），要么给个次数编辑框 |
| Q07 | 持有两张去重卡的校验放多严 | 正常局读档：持有两张同编号去重卡即拒绝。调试工作区可以（场景直接构造）。**购买与领取时的再验证不分正常局与调试局**，调试商店里也不能买第二张 | 调试里要测两张同名去重卡的来源，用场景构造，不要走商店 |
| Q08 | 报价上的追赶值读档时查多细 | 正常局：每张卡牌报价的追赶值必须**恰好等于**当前商店周期的值（非成长卡为 0）。调试局只查“非负、只在成长卡上”。已持有的卡仍按 W3b-1 的写法查整步与上限 | 调试里可以给报价写任意非负追赶值 |
| Q09 | 低价池为空时退到全候选，“退补不算保底” | 这时那张卡按普通随机报价抽附魔票（第 1、2 周期也抽）。没有写成测试：低价卡有 82 张，正常途径造不出空池 | 见第 4 节 |
| Q10 | 两个面的合法性“均须在原状态”判断 | 先对两个面都按动手前的状态判，再按面 ID 升序逐面执行。U01–U10 的合法性只看这一个面自己，所以“原状态”与“执行到它时的状态”没有差别；U11 只许一个面 | 无 |
| Q11 | 随机变形两个面时第二个面排除谁的原值 | 各排除自己的原值，各抽一票，按面 ID 升序先后抽（效果流）。回执里两条 `FaceTransform` 的 `sourceId` 是 `骰/面` | 种子复现依赖这个顺序 |
| Q12 | 回心转意“供回执标记的来源取最左” | `DieUnlocked.detail` 写最左边那个 C154 来源的**发出者**实例 ID；最左是百变模仿时写模仿卡的实例 ID，`sourceId` 仍是 `C154` | 界面高亮的是发出者 |
| Q13 | `freeUnlocksPerHand`（配置里是 1）大于 1 怎么办 | 不支持：§12.1 的状态是一个布尔 `unlockCardUsed`，每手只有一次 | 改这个参数不会生效 |
| Q14 | 读档时 `unlockCardUsed` 的相互关系 | 为真必须本手已掷过；正常局还要求当下有生效的 C154 来源（一手之内卡牌不会进出） | 调试局不查后一条 |
| Q15 | 镀金改造的骰子版本号 | `GildFace` 自己加一次，U06 不再重复加：单面 +1，双面 +2（其它改造也是每面 +1） | 只影响界面缓存失效的次数 |
| Q16 | `turnoverUnits2`“恒为配置初值”要不要收紧成恒等于 3（W3b-1 备注建议） | **没有收紧**。调试界面 `UI/GameApp.Debug.cs:280` 还有一个编辑它的输入框，收紧会让调试场景被拒 | 界面工作包删掉那个输入框之后再收紧 |
| Q17 | 钥匙串回收价读哪个字段 | 沿用全局的 `consumableSellPrice`（1），与余次无关；道具定义里的 `sellPrice` 也是 1，没有另读 | 无 |
| Q18 | 自选改造的检查顺序 | 面 ID 两种写法是否一致 → 阶段 → 战利品门禁 → 改造编号 → 本店额度 → 金币 → 目标。拒绝理由按这个顺序给第一条 | 界面提示的优先级 |

## 2. 留给界面工作包的命令与查询

### 2.1 命令

`CommandKind` 在末尾追加了两个值（旧值的序号没有动）。

| 命令 | 参数 | 说明 |
| --- | --- | --- |
| `UnlockDie` | `targetDieId` | 撤回一颗已锁骰。来源由规则选：有生效的 C154 且本手没用过 → 免费；否则背包里最早的还有次数的钥匙串；徒手（`NoItems`）只挡钥匙串。不耗投掷、不抽随机数。有未确认的临时选骰时被原门禁拒绝 |
| `CustomDieUpgrade` | `upgradeId`、`targetDieId`、`targetFaceIds[]` | 自选改造。每店 `selfDieUpgradeLimit`（2）笔，价 = `max(1, 基础价 + 1 + 砍价高手来源数 × (−1))` |
| `Buy`（改造报价） | `offerId`、`targetDieId`、`targetFaceIds[]` | 按面的改造一次 1 或 2 个面（同一颗骰、面 ID 不同）；U05 不带面；U11 恰好 1 个面。旧的 `targetFaceId` 仍可用，等价于单元素数组；两者都给且不一致时拒绝 |
| `UseItem` | 不变 | 对钥匙串会被拒绝（走 `UnlockDie`） |

### 2.2 查询（都不改状态、不抽随机数）

| 查询 | 用途 |
| --- | --- |
| `UnlockQuote(targetDieId = null)` → `UnlockView` | 当前能否解锁（`available`、`error`）、会用哪个来源（`sourceId` = `C154`／`I13`、`sourceInstanceId`）、免费次数还在不在（`freeAvailable`）、钥匙串是否被徒手挡住（`keyRingBlocked`）、下一把要用的钥匙串剩余次数与全部钥匙串合计（`keyRingUses`、`keyRingUsesTotal`）。不给骰子 ID 时问的是“有没有哪颗已锁骰能撤” |
| `UnlockQuoteIn(view, targetDieId)` | 同上，对一份脱离的状态（不看临时选骰） |
| `OwnedItem.usesRemaining` | 钥匙串图标上的 1–3 |
| `CustomDieUpgradeOptions()` → `DieUpgradeOption[]` | 自选清单：11 种改造按编号排，各带实时价 `price`、`maxSelectableFaces`（0／1／2）、有没有合法目标 `hasTarget`、此刻整体买不了的原因 `error`（战利品未领、额度用完、金币不足、没有目标；能买为 null）。不在商店里时为空 |
| `CustomDieUpgradesRemaining`、`CustomDieUpgradesRemainingIn(view)` | 本店自选改造剩余笔数 |
| `GetCustomDieUpgradePrice(upgradeId)` | 某种自选改造的实时价 |
| `CustomDieUpgradeError(upgradeId, dieId, faceIds)` | 这一笔自选会不会被拒、为什么 |
| `PurchaseErrorForFaces(offerId, dieId, faceIds)` | 货架、赠礼、战利品改造报价配一组面会不会被拒。旧的 `PurchaseError(offerId, dieId, faceId)` 保留 |
| `LegalUpgradeFaces(upgradeId, dieId)` | 这种改造在这颗骰子上哪些面合法（面 ID 升序）。U05 返回空，改问 `IsUpgradeTargetLegal("U05", dieId, null)` |
| `MaxSelectableFaces(upgradeId)` | 0／1／2 |
| `IsUniqueControllerHeld(id)`、`IsUniqueControllerHeldIn(view, id)` | 这张去重卡是否因为已持有而被货架与战利品过滤、活报价被拒（图鉴或提示用）。去重名单在 `cfg.shop.uniqueControllerIds` |
| `GetHandUpgradePrice(handId, selfStudy)` | 牌型或颜色的升级价（货架价或自选研习价）。`GetStudyPrice`、`GetOfferPrice` 照旧 |
| `ShopCycleIn(view)` | 当前（或本关之后）商店的周期 1–8 |
| 报价的追赶值 | `ShopOffer.controller.state.growthCatchup`；牌面起步值用 `ControllerRules.GrowthValue(cfg, id, offer.controller.state)` |

拒绝理由的常量（`GameSession.*`）：`UpgradeTargetRejected`、`UpgradeFaceCountRejected`、`UpgradeSingleFaceOnly`、`UpgradeTakesWholeDie`、`UpgradeFacesInconsistent`、`SelfDieUpgradeLimitReached`、`UniqueControllerOwned`、`UnlockNeedsResults`、`UnlockTargetInvalid`、`UnlockNoSource`、`ItemsForbidden`、`KeyRingUsesUnlock`。

### 2.3 回执 `effects` 的变化

| kind | 字段 | 说明 |
| --- | --- | --- |
| `DieUnlocked`（新） | `sourceId` = `C154` 或 `I13`，`dieId`，`detail` = 来源实例 ID（发出者或钥匙串） | 每次解锁先记这一条 |
| `ItemUsed` | 多了 `oldValue`／`newValue` = 扣前、扣后次数 | 所有道具都有；普通道具是 1→0。钥匙串解锁时紧跟在 `DieUnlocked` 之后，再后面是每张 C134 的 `ControllerGrown`。免费解锁没有这一条 |
| `CustomDieUpgraded`（新） | `sourceId` = 改造编号，`dieId`，`detail` = 面 ID 升序逗号串（U05 为空），`oldValue` = 实付价，`newValue` = 本店已用笔数 | 自选改造成功 |
| `DieUpgraded` | 不变 | 双面改造时每个面一条，按面 ID 升序 |
| `FaceChanged`（`gold`、`weightDelta`） | 同 W3b-1 | 现在商店的 U06 与点金笔 I11 也产生（权重真的变了才有 `weightDelta`） |
| 随机回执 `ControllerEnchantment`（商店流） | `range` 现在是 100×W（5000 或 10000） | 第 1、2 周期的低价保底卡没有这一条；L2、O11、C114 仍是原来的范围 |

界面里已经与规则不符、本包没有动的地方：

- 调试界面加道具时 `new OwnedItem{…}` 不写次数（见 Q06），加出来的钥匙串是 1 次。
- 调试界面的“人员流动本体倍率”输入框还在改 `turnoverUnits2`（见 Q16）。
- 改造的选面界面一次只能选一个面，自选改造没有入口，钥匙串没有按钮路径（推断：这些命令与字段此前不存在，界面代码里搜不到对它们的调用；没有在 Unity 里实看）。

## 3. 留给模拟器工作包的接口变化

- 货架附魔：`AppendControllers` 现在每张卡抽“编号一票 + 附魔一票（范围 100×W）”，第 1、2 周期每批第一张低价保底卡只有编号票。商店流的消耗量因此与 p1.1.0 不同，旧种子的货架全部变了。
- 候选过滤：持有的去重卡不进货架、刷新、扩栏、L1、L2 的候选；购买与领取时再查一次。
- 追赶值在报价生成时写入、取得时继承：模拟器若自己构造卡牌实例，要自己给 `growthCatchup`。
- `OwnedItem` 多了 `usesRemaining`（缺省 1）；钥匙串不走 `UseItem`。
- U05 带面 ID 会被拒（Q01）。
- 两个新命令与 `targetFaceIds`、`upgradeId` 两个命令字段。`Core/Session/Validation~/NormalRunSearch.cs`、`NormalRunReplay.cs` 里各有一份命令字段的镜像，还不认识这两个字段：它们能编译（本包编过），但回放含新命令或双面改造的记录会丢掉这两项。本包没有改这两个工具。
- `Editor/PrototypeBuild.cs:104` 的发布身份检查还钉着 `p1.1.0` 与结构 7，出包时会不通过。不在本包范围内，没有动。

## 4. 没做完的事

- **正式历史最高单手** `historicalHighestHandScore`（§12.1 最后一行）仍然没有做。它在 `RunState` 之外保存，要存储层与主菜单配合，W3b-1 的备注已经写过；本包的十一项里也没有它。
- **旧档门禁**：会话层的门禁就是 `ValidateState` 第一行的版本相等检查，版本号来自配置（现在是 p1.1.1／8），本包没有改它的行为，只加了测试。活动档与备份的优先级、清理、损坏隔离在 `Runtime/AtomicSaveStore.cs`，它读的也是配置里的版本号；它的测试（`Tests/EditMode/Persistence/`）要在 Unity 里跑，本包没有运行。
- 没有启动 Unity。新测试与全部改写只在 .NET 会话宿主上跑过；`V111ShopTests.cs.meta` 是手写的（随机 GUID，格式照同目录）。
- `UnityTestsCompilation.csproj` 编过一次：仍是基线上那 5 个与本包无关的错误（`Art07ResourceDiagnostic` 的 `…ForTests` 成员），没有新增。界面、运行时与 PlayMode 测试没有运行。
- 没有写成测试的规格内容：
  - §9.1“低价池为空才退到全候选，退补不算保底”（见 Q09，造不出空池）。
  - §9.1“随机率统计也排除它及强制附魔”：这是统计口径，没有写统计测试；每张票的区间边界已逐一测过。
  - §8 钥匙串的取得入口里，“赠礼”（C111）没有单独测：它与购买、道具补给走同一个 `NewOwnedItem`，购买、道具补给 L6、百宝袋 C110 三条都测了。
  - §12.1 例子里“I13 余 2、免费已用、自选已用 1”不可能同处一个阶段（免费标记属于一手，自选额度属于商店），拆成了手内与商店两条往返测试。
- 第一个提交 `c3678959` 的提交信息开头带了一个 UTF-8 BOM（PowerShell 5.1 的 `Out-File -Encoding utf8` 写出来的），标题行前面多一个不可见字符。按“不改写历史”没有去修它；后面的提交已改用无 BOM 的写法。推送前如果要整理，改这一条的提交信息即可。
- 没有更新 `docs/STATE.md`，留给协调者。

## 5. 附录：货架向量的独立脚本

`V05_ShopInterleavesIdAndEnchantmentWithFrozenReferenceVectors`（第 1 周期）与 `V111_CycleThreeShelfMatchesTheIndependentReferenceVectors`（第 3 周期）的八条向量由下面的脚本算出，再与游戏输出比对。脚本只读配置文件，按 §9.1 的文字重写了抽法（相对权重与周期概率直接抄自规格，不读配置里的同名字段），流的处理照 `tools/v08_session_vectors.py`。它没有放进 `tools/`（不在本包可写范围内），需要长期保留时可以原样存成 `tools/v111_session_vectors.py`。在仓库根目录运行。

```python
import json
from pathlib import Path
CONFIG = Path("DicePrototype/Assets/Game/Data/P0Config.json")
MASK = 0xFFFFFFFF

class Rng:
    def __init__(self, state):
        self.state = state & MASK or 0x9E3779B9
    def next(self):
        x = self.state
        x ^= (x << 13) & MASK
        x ^= x >> 17
        x ^= (x << 5) & MASK
        self.state = x
        return x
    def index(self, count):
        limit = (MASK // count) * count
        while True:
            u = (self.next() - 1) & MASK
            if u < limit:
                return u % count

ORDER = ["slotless", "base10", "mult2", "submit1", "roll1"]  # section 9.1, after "none"

def enchant(cfg, rng, card_id, cycle):
    rules = cfg["enchantRules"]
    if card_id in rules["nonSubmitControllerIds"]:
        rel = {"slotless": 50, "base10": 0, "mult2": 0, "submit1": 25, "roll1": 25}   # "50, 0, 0, 25, 25"
    else:
        rel = {"slotless": 18, "base10": 10, "mult2": 4, "submit1": 9, "roll1": 9}    # "18, 10, 4, 9, 9"
    w = sum(rel.values())
    p = [10, 20, 35, 45, 55, 60, 65, 65][cycle - 1]                                   # the table of section 9.1
    t = rng.index(100 * w)
    if t < (100 - p) * w:
        return "none"
    t -= (100 - p) * w
    for e in ORDER:
        if t < p * rel[e]:
            return e
        t -= p * rel[e]
    raise AssertionError

def shelf(cfg, seed, cycle):
    rng = Rng(seed ^ cfg["rng"]["shopSeedXor"])
    choices = sorted(cfg["controllers"], key=lambda c: c["id"])
    out = []
    for i in range(cfg["shop"]["initialControllerOffers"]):
        pool, guaranteed = choices, False
        if i == 0:
            cheap = [c for c in choices if c["buyPrice"] <= 4]
            if cheap:
                pool, guaranteed = cheap, True
        ticket = rng.index(sum(c["offerWeight"] for c in pool))
        picked = None
        for option in pool:
            ticket -= option["offerWeight"]
            if ticket < 0:
                picked = option
                break
        e = "none" if guaranteed and cycle <= 2 else enchant(cfg, rng, picked["id"], cycle)
        out.append(picked["id"] + ":" + e)
        choices = [c for c in choices if c is not picked]
    return ",".join(out)

cfg = json.loads(CONFIG.read_text(encoding="utf-8"))
assert cfg["configVersion"] == "p1.1.1" and cfg["rng"]["shopSeedXor"] == 0xC8013EA4
for cycle in (1, 3):
    for seed in (1, 0, 20261030, 4294967295):
        print(cycle, seed, shelf(cfg, seed, cycle))
```

输出：

```text
1 1 C59:none,C105:none,C64:none,C153:none
1 0 C48:none,C65:none,C31:none,C36:slotless
1 20261030 C135:none,C30:submit1,C150:roll1,C74:none
1 4294967295 C142:none,C42:none,C139:none,C13:none
3 1 C59:slotless,C155:none,C05:none,C50:submit1
3 0 C48:none,C126:none,C89:mult2,C49:none
3 20261030 C135:mult2,C08:mult2,C36:none,C115:roll1
3 4294967295 C142:none,C97:slotless,C138:base10,C141:base10
```
