"""Write the p1.1.1 game configuration.

Input:  tools/balance_sim/baseline/P0Config.p1.1.0.json (frozen copy of the shipped p1.1.0 config)
Spec:   docs/specs/v111/RULES_DELTA.md (rules), docs/specs/v111/BALANCE_PLAN.md (design source)
Output: DicePrototype/Assets/Game/Data/P0Config.json

    python tools/v111_config.py            # write the game config
    python tools/v111_config.py --check    # fail if the game config differs from what this script produces

Units: every key ending in 100 is the real value times 100 (加成 +0.6 -> 60, x2.5 -> 250). Growth cards carry
growthKind/growthStart/growthEvent: "additive" and "multiplier" use hundredths, "base" uses whole points.
"""
import argparse
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "tools" / "balance_sim" / "baseline" / "P0Config.p1.1.0.json"
TARGET = ROOT / "DicePrototype" / "Assets" / "Game" / "Data" / "P0Config.json"

RARITY = {"普通": ("common", 6), "罕见": ("uncommon", 4), "稀有": ("rare", 2)}

# Normal targets per stage. Stages 1-3 are anchored by the exact opening-hand computation
# (tools/balance_sim/opening_dp.py). Stages 4-24 were calibrated in W6 with the simulator (BALANCE_PLAN section 25,
# candidate t3/t4: 300 runs each, evidence/v111/balance/run_p111_t4_300.json).
TARGETS_PROVISIONAL = False
TARGETS = {
    1: 160, 2: 320, 3: 500, 4: 1300, 5: 2500, 6: 4000, 7: 11000, 8: 16000, 9: 20000, 10: 40000, 11: 58000, 12: 78000,
    13: 150000, 14: 200000, 15: 260000, 16: 420000, 17: 560000, 18: 760000, 19: 1400000, 20: 1900000, 21: 3000000,
    22: 4600000, 23: 6200000, 24: 7000000,
}

REMOVED = ["C90", "C128", "C145"]
CULTIVATION = ["C07", "C15", "C26", "C36", "C37", "C38", "C115", "C117", "C118", "C119", "C120", "C121", "C122",
               "C123", "C124", "C126", "C127"]
# Multiplier cards: an owned one is not offered again (RULES_DELTA 9.3).
UNIQUE = ["C02", "C09", "C31", "C33", "C41", "C42", "C53", "C54", "C58", "C71", "C87", "C89", "C100", "C101", "C103",
          "C125", "C130", "C138", "C155"]
RED_REWARDS = [100, 100, 100, 200, 300, 450, 600, 800, 1100]
ENCHANT_CHANCE = [10, 20, 35, 45, 55, 60, 65, 65]


def fmt(units100):
    """250 -> '2.5', 300 -> '3', 125 -> '1.25'."""
    text = f"{units100 / 100:.2f}".rstrip("0").rstrip(".")
    return text


# --- simple "whole hand xN" -> "加成 +X" conversions: only the number and the wording change -------------------------
SIMPLE_ADDITIVE = {
    "C06": 300, "C08": 200, "C11": 800, "C14": 400, "C18": 500, "C28": 400, "C30": 250, "C32": 300, "C40": 450,
    "C46": 200, "C74": 400, "C75": 450, "C77": 250, "C81": 800, "C82": 450, "C83": 600, "C86": 800, "C88": 600,
    "C102": 450, "C104": 450, "C105": 300, "C106": 250, "C144": 400, "C149": 800, "C153": 450,
}
# --- fixed multipliers that stay multiplicative (value in hundredths) --------------------------------------------------
SIMPLE_MULTIPLIER = {"C02": 300, "C09": 250, "C31": 300, "C33": 300, "C42": 400, "C101": 300, "C130": 300, "C138": 250}

# id -> fields to overwrite. "set"/"drop" act on parameters. Texts: d = description, s = shortText (<= 28 chars), x = details.
PATCH = {
    # multiplier cards with extra changes
    "C31": dict(d="提交中任一有效点数至少4颗时，整手得分×3。", x=""),
    "C42": dict(x="本次计分和提交后效果都完成后才判定；被百变模仿复制时，模仿卡自己也有15%概率碎掉。"),
    # W6 calibration: rare at 7 (was uncommon at 4), see BALANCE_PLAN 25.
    "C53": dict(rarity="稀有", price=7, set={"factorUnits2": [1, 2, 2, 3, 4, 4, 6, 6, 8]},
                d="提交事务中从0.5、1、1、1.5、2、2、3、3、4等概率抽取整手倍率；每个真实错乱乘积抽一次，其模仿者复用同一结果。",
                s="每次提交得分随机×0.5~×4。",
                x="从×0.5、×1、×1、×1.5、×2、×2、×3、×3、×4里等概率抽一个；被百变模仿复制时两份用同一个结果。"),
    "C89": dict(set={"stepNumerator": 13, "stepDenominator": 10},
                d="每颗黄金面朝上的提交骰，整手得分×1.3。", s="每颗黄金面朝上的提交骰子，得分×1.3。",
                x="多颗相乘（2颗×1.69）；黄金面自带的加成+0.5照常另算。"),
    "C103": dict(set={"scoreMultiplier100": 200, "strictScoreMultiplier100": 300}, drop=["scoreMultiplier", "strictScoreMultiplier"],
                 d="永久骰不超过4颗时整手得分×2，不超过3颗时×3。", s="永久骰≤4颗时得分×2，≤3颗时×3。"),
    "C138": dict(set={"goldCost": 2}, d="提交时钱包至少2金币：整手得分×2.5，计分后失去2金币。",
                 s="提交时有2金币以上：得分×2.5，之后扣2金币。"),
    "C106": dict(s="提交3颗以上且点数左右对称时，加成+2.5。", x="如2-5-2：第1颗与最后一颗相同、第2颗与倒数第2颗相同，依此类推。"),
    # additive cards with their own formulas
    "C47": dict(price=7, trigger="ScoreAdditive", set={"additivePerController100": 60}, drop=["factor", "perControllerUnits2"],
                d="每持有1件卡牌，加成+0.6；包括自身、不占栏位的卡和本关失效的卡，百变模仿只算1件。",
                s="每持有1件卡牌，加成+0.6。", x="包括这张卡自己和不占栏位的卡；百变模仿只算1件；本关失效的卡也计入。"),
    "C49": dict(price=4, trigger="ScoreAdditive", set={"additivePerFive100": 150, "noFiveMultiplier100": 50}, drop=["factor", "allowZero"],
                d="每颗有效5点的提交骰，加成+1.5；提交里没有5点时改为整手得分×0.5。",
                s="每颗5点提交骰加成+1.5；没有5点时得分×0.5。", x="有5点时只给加成，没有惩罚。"),
    "C63": dict(trigger="ScoreAdditive", set={"additivePerPermanentDie100": 50}, drop=["perDieUnits10"],
                d="每颗永久骰，加成+0.5。", s="每有1颗永久骰，加成+0.5。", x="5颗+2.5，8颗+4；临时骰不算。"),
    "C139": dict(trigger="ScoreAdditive", set={"additivePerGoldStep100": 50}, drop=["stepUnits4", "maxSteps"],
                 d="钱包每满10金币，加成+0.5，不设上限。", s="每有10金币，加成+0.5。", x="40金币+2；按提交那一刻的金币算。"),
    "C141": dict(trigger="ScoreAdditive", set={"initialAdditive100": 400, "decayPerClear100": 100, "expireGrowth": 4},
                 drop=["initialMultiplierUnits2"],
                 d="加成+4；每通过一关加成−1；降到0时自毁。", s="加成+4；每通过一关−1，降到0时这张卡消失。",
                 x="也就是通过4关后消失；自毁不算卖出。"),
    "C147": dict(trigger="ScoreAdditive", set={"additivePerPoolDie100": 60}, drop=["stepUnits4"],
                 d="每颗留在骰池里没有提交的骰子，加成+0.6。", s="每颗留池骰，加成+0.6。"),
    "C148": dict(trigger="ScoreAdditive", set={"additivePerUnusedRoll100": 100}, drop=["perRollUnits2"],
                 d="提交时每剩1次未用投掷，加成+1。", s="提交时每剩1次投掷，加成+1。"),
    "C150": dict(trigger="ScoreAdditive", set={"additivePerPreviousSubmit100": 200},
                 d="本关第n次提交，加成+2×(n−1)。", s="本关第2次提交加成+2，第3次+4，依此类推。", x="第1次提交不生效。"),
    "C151": dict(trigger="ScoreAdditive", set={"additivePerEmptyOrdinarySlot100": 125}, drop=["perSlotUnits2"],
                 d="每个空着的普通卡牌栏位，加成+1.25。", s="每个空着的卡牌栏位，加成+1.25。",
                 x="不占栏位的卡不影响空位数。"),
    "C152": dict(trigger="ScoreAdditive", set={"additivePerCommon100": 60}, drop=["stepUnits4"],
                 d="每持有1张普通稀有度的卡牌，加成+0.6。", s="每有1张常见卡牌，加成+0.6。", x="本关失效的卡也计入。"),
    # base-score cards
    "C10": dict(set={"perDieAdd": 15}, d="每颗有效1点的提交骰，点数贡献+15，不改牌型。", s="提交的1点骰子，每颗计分+15。"),
    "C13": dict(set={"oddPerDieAdd": 8}, d="每颗有效奇数的提交骰，点数贡献+8，不改牌型。", s="提交的奇数点骰子，每颗计分+8。"),
    "C17": dict(set={"perDieAdd": 9}, d="每颗有效1、2、3点的提交骰，点数贡献+9。", s="提交的1~3点骰子，每颗计分+9。"),
    "C20": dict(set={"perDieAdd": 24}, d="占满提交栏且最后一格有效点数为6时，该骰点数贡献+24，参与逐骰强化及后续全部倍率。",
                s="放满提交栏且最后一颗是6点时，这颗计分+24。", x="必须放满；这+24和点数一样会被玻璃等倍数放大。"),
    "C27": dict(set={"baseAddPerDistinctPairedPip": 20},
                d="每个出现至少2颗的不同有效点数组，使整手基础分+20；四条只算一组，双三条算两组。",
                s="每有一种点数出现2颗以上，基础分+20。"),
    "C39": dict(set={"perDieAdd": 9}, d="每颗有效蓝色提交骰，点数贡献+9。", s="提交的蓝骰，每颗计分+9。"),
    "C44": dict(set={"baseAddPerFrozenGold": 2}, d="每1金币令整手基础分+2，读取本次提交冻结时的钱包；本次提交新增金币不回算本手。",
                s="你每有1金币，基础分+2。"),
    "C48": dict(set={"baseAddPerEmptySubmitSlot": 15}, d="每空一个提交栏位，整手基础分+15；空栏数为提交栏格数减去实际提交数。",
                s="提交栏每空1格，基础分+15。"),
    "C50": dict(set={"baseAdd": 30}, d="每次有效提交整手基础分+30。", s="每次提交基础分+30。"),
    "C70": dict(set={"baseAddPerPoolDie": 9}, d="每颗留在骰池里没有提交的骰子，整手基础分+9。", s="每颗留池骰，基础分+9。"),
    "C72": dict(set={"poolPipSumMultiplier": 3}, d="留在骰池里的骰子点数之和×3，加入整手基础分。", s="留池骰点数之和×3，加到基础分。"),
    "C76": dict(set={"baseAddPerExtraRoll": 15}, d="本手从第2次投掷起，每多投1次整手基础分+15。",
                s="本手从第2次投掷起，每掷1次基础分+15。", x="掷2次+15，掷3次+30；再来三次、遥控骰子等额外投掷也算。"),
    "C80": dict(set={"baseAddPerPair": 30}, d="提交骰中每有一对有效点数相加为7的骰子（每颗只配一次），整手基础分+30。",
                s="每有一对点数相加为7的提交骰子，基础分+30。"),
    "C92": dict(set={"perDieAdd": 15}, d="提交骰所属的骰子六个面同色时，该骰点数贡献+15。", s="六个面同色的提交骰子，每颗计分+15。"),
    "C93": dict(set={"dieContributionMultiplier": 3}, d="提交骰所属的骰子六个面点数全部相同时，该骰点数贡献×3。",
                s="六个面点数相同的提交骰子，每颗计分×3。"),
    "C107": dict(set={"perDieAdd": 20}, d="每颗有效点数恰好比左边相邻骰大1的提交骰，点数贡献+20。",
                 s="比左边那颗正好大1点的提交骰子，每颗计分+20。"),
    "C108": dict(set={"baseAddPerPip": 6}, d="第一颗提交骰的有效点数×6，加入整手基础分。", s="第一颗提交的骰子点数×6，加到基础分。"),
    "C135": dict(set={"baseAddPerItem": 15}, d="背包里每有1个道具，整手基础分+15。", s="道具栏里每有1个道具，基础分+15。",
                 x="钥匙串不管剩几次都算1个。"),
    # hand rule, hand multiplier adds, retrigger
    "C29": dict(price=3),
    "C43": dict(d="本次与上一有效提交的结构主牌型相同且非散点时，该牌型在本次计分后+1级；每个来源各+1，正常上限12，散点不升级。",
                x="散点不算；本次得分仍按旧等级；可以跨关连续（包括买这张卡之前的最后一次提交）；最高12级；被模仿时每份各升1级。"),
    "C84": dict(set={"handMultAdd100": 200}, d="提交含对子时，牌型倍率+2。", s="有一种点数至少2颗时，牌型倍率+2。"),
    "C85": dict(set={"handMultAdd100": 400}, d="提交含三条时，牌型倍率+4。", s="有一种点数至少3颗时，牌型倍率+4。"),
    "C143": dict(set={"retriggers": 2}, d="有效点数恰好出现2颗的那些提交骰，点数贡献各再计2次。", s="某个点数恰好有2颗时，这2颗各再计2次。"),
    # growth cards (起步 / 每次, no cap; later cycles start higher)
    "C51": dict(trigger="GrowthAdditive", set={"growthKind": "additive", "growthStart": 100, "growthEvent": 80},
                drop=["initialMultiplierUnits2", "addUnits2PerRealControllerSale"],
                d="加成从+1起。每家商店第一次真实卖出一件卡牌后，仍持有的真实人员流动各+0.8，同一家商店之后的卖出不再成长；自毁不是卖出。",
                s="加成从+1起，每家店首次卖出卡牌后+0.8。",
                x="同一家店再卖不再增加；卡牌自毁、被祭坛摧毁不算；被百变模仿复制时共用同一个数值。"),
    "C54": dict(trigger="GrowthMultiply", set={"growthKind": "multiplier", "growthStart": 125, "growthEvent": 10}, drop=["growthPerEvent"],
                d="整手得分×n，n从1.25起。每次有效提交后，主牌型与上一次有效提交相同（散点也算）则n+0.1，不同则已长出的部分减半。",
                s="主牌型与上次相同则倍率+0.1，不同则长出的部分减半。",
                x="得分倍率从×1.25起；散点也算一种牌型；可以跨关连续；本次得分用提交前的倍率。"),
    "C55": dict(set={"growthKind": "base", "growthStart": 12, "growthEvent": 3}, drop=["growthPerEvent"],
                d="整手基础分加上本卡的成长值，从+12起。每次有效提交后成长值+3。", s="基础分+12，每次提交后再+3。"),
    "C56": dict(trigger="GrowthAdditive", set={"growthKind": "additive", "growthStart": 100, "growthEvent": 40}, drop=["growthPerEvent"],
                d="加成从+1起。每次没有有效红面的有效提交后+0.4。", s="加成从+1起，每次提交里没有红骰就+0.4。"),
    "C57": dict(trigger="GrowthAdditive", set={"growthKind": "additive", "growthStart": 100, "growthEvent": 80}, drop=["growthPerEvent"],
                d="加成从+1起。每次红组达到3颗及以上的有效提交后+0.8。", s="加成从+1起，每次提交红组达3颗以上就+0.8。"),
    "C58": dict(price=7, set={"growthKind": "multiplier", "growthStart": 125, "growthEvent": 15}, drop=["growthPerEvent"],
                d="整手得分×n，n从1.25起。每通过一关n+0.15。", s="得分倍率从×1.25起，每通过一关+0.15。"),
    "C59": dict(set={"growthKind": "base", "growthStart": 12, "growthEvent": 3}, drop=["growthPerEvent"],  # W6: 6 -> 3, see BALANCE_PLAN 25
                d="整手基础分加上本卡的成长值，从+12起。每关结算时，每剩1次提交机会成长值+3。",
                s="基础分+12；每关结算时每剩1次提交再+3。"),
    "C60": dict(trigger="GrowthAdditive", set={"growthKind": "additive", "growthStart": 100, "growthEvent": 50}, drop=["growthPerEvent"],
                d="加成从+1起。每做1次骰面改造+0.5（购买、自选、赠礼、战利品都算）。", s="加成从+1起，每做1次骰面改造+0.5。",
                x="货架、自选、战利品、赠礼里的骰面改造都算；一次改两个面只算1次；骰子脆化也算。"),
    "C61": dict(trigger="GrowthAdditive", set={"growthKind": "additive", "growthStart": 100, "growthEvent": 50}, drop=["growthPerEvent"],
                d="加成从+1起。牌型或颜色每升1级+0.5（购买、自选研习、战利品、牌型提升都算）。",
                s="加成从+1起，牌型或颜色每升1级+0.5。"),
    "C62": dict(price=4, set={"growthKind": "base", "growthStart": 12, "growthEvent": 1}, drop=["growthPerEvent"],
                d="整手基础分加上本卡的成长值，从+12起。每投掷1次成长值+1。", s="基础分+12，每投掷1次再+1。"),
    "C100": dict(set={"growthKind": "multiplier", "growthStart": 125, "growthEvent": 15}, drop=["growthPerBuyPrice"],
                 d="整手得分×n，n从1.25起。每关开始时摧毁右侧相邻的卡牌，n增加该卡基础买入价（不含附魔加价）×0.15。放在最右侧则不摧毁。",
                 s="每关开始时摧毁右邻卡牌，倍率+其基础价×0.15。",
                 x="得分倍率从×1.25起；按被摧毁卡的基础价，不含附魔加价；放在最右边时不摧毁、不成长；摧毁不算卖出；新买的卡排在最右，可在商店或关卡预览里拖动调序。"),
    "C129": dict(trigger="GrowthAdditive", set={"growthKind": "additive", "growthStart": 100, "growthEvent": 250}, drop=["growthPerEvent"],
                 d="加成从+1起。每通过一个首领关+2.5。", s="加成从+1起，每通过一个首领关+2.5。"),
    "C132": dict(trigger="GrowthAdditive", set={"growthKind": "additive", "growthStart": 100, "growthEvent": 250}, drop=["growthPerEvent"],
                 d="加成从+1起。每完成一次高压挑战+2.5。", s="加成从+1起，每通过一次高压挑战+2.5。"),
    "C134": dict(trigger="GrowthAdditive", set={"growthKind": "additive", "growthStart": 100, "growthEvent": 40}, drop=["growthPerEvent"],
                 d="加成从+1起。每使用1次道具+0.4。", s="加成从+1起，每使用1次道具+0.4。", x="钥匙串每用1次都算。"),
    # dice cultivation
    "C36": dict(set={"indices": [0, 1, 2]}, d="至少提交2颗且没放满提交栏，提交后前三颗本次物理面原值+1，上限6。",
                s="提交2颗以上且没放满时，前三颗朝上的面永久+1点。"),
    "C37": dict(set={"indices": [0, 1, 2]}, d="至少提交2颗且没放满提交栏，提交后前三颗本次物理面原值−1，下限1。",
                s="提交2颗以上且没放满时，前三颗朝上的面永久−1点。"),
    "C115": dict(set={"facesPerStage": 3, "upgradeIds": ["U03", "U04", "U06", "U08"]},
                 d="每关结算时，随机3个永久骰面各获得一项随机改造（染红、染蓝、黄金、精雕里它还能做的一种）。",
                 s="每关结算时，随机3个骰面各得一项随机改造。",
                 x="只选永久骰；改造从染红、染蓝、黄金、精雕里随机，只抽这一面还能做的；不会落空，除非所有面都改不动了。"),
    "C117": dict(set={"targetIndices": "first_and_last_distinct"}, d="每次有效提交后，第一颗和最后一颗提交骰的朝上面永久镀金。",
                 s="每次提交后，首尾两颗骰子朝上的面永久镀金。", x="只提交1颗时只镀这一颗；新镀金的面权重+1。"),
    "C119": dict(price=6, set={"targetIndices": "first_and_last_distinct", "facesPerDie": 2}, drop=["index"],
                 d="每次有效提交后，第一颗和最后一颗提交骰各有两个点数不同的面，永久变成各自朝上面的点数。",
                 s="每次提交后，首尾两颗各有两面变成朝上的点数。",
                 x="改的是按固定顺序最前面两个与朝上点数不同的面（不是随机）；不足两个就改到没有为止；按骰面本来的点数。"),
    "C120": dict(set={"facesPerDie": 3}, d="每关结算时，每颗永久骰点数最小的三个面各+1，上限6。",
                 s="每关结算时，每颗永久骰最小的三个面各+1。", x="上限6点；点数相同时按固定顺序选。"),
    "C121": dict(set={"facesPerDie": 3}, d="每关结算时，每颗永久骰点数最大的三个面各−1，下限1。",
                 s="每关结算时，每颗永久骰最大的三个面各−1。", x="下限1点；点数相同时按固定顺序选。"),
    "C122": dict(d="每次有效提交后，每颗提交骰的朝上面点数永久变成5点。", s="每次提交后，提交骰子朝上的面永久变成5点。", x=""),
    "C123": dict(set={"lowPipDelta": 1, "highPipDelta": -2, "highPipFloor": 3},
                 d="每次有效提交后，每颗提交骰的朝上面：1点变2点；4、5、6点−2，最低到3点。",
                 s="每次提交后，朝上的面：1点→2点，4~6点−2。", x="永久生效；2、3点不变；减点最低到3点（4→3、5→3、6→4）。"),
    "C124": dict(price=5, set={"delta": 2}, d="每次有效提交后，每颗提交骰朝上面的权重+2（上限5），以后更容易掷出这一面。",
                 s="每次提交后，提交骰子朝上的面权重+2。"),
    "C126": dict(set={"delta": 2}, d="每次有效提交后，每颗留在骰池里的骰子，朝上面点数永久+2，上限6。", s="每次提交后，每颗留池骰朝上的面永久+2点。"),
    "C127": dict(drop=["submissionOrdinal"], d="每次有效提交至少2颗时，把第一颗提交骰朝上面的点数、颜色、纹理、黄金复制到最后一颗提交骰的朝上面。",
                 s="每次提交2颗以上时，第一颗朝上的面复制给最后一颗。"),
    # four reworked cards: same id, name and art, new effect
    "C19": dict(price=3, trigger="ScoreAdditive", set={"minOneAdd100": 250, "minTwoAdd100": 150}, drop=["maxPermanentSixFaceSum", "perDieAdd"],
                d="提交的骰子里最小的有效点数是1时，加成+2.5；是2时，加成+1.5。", s="提交里最小的点数是1：加成+2.5；是2：+1.5。",
                x="只看提交的骰子；两档不叠加。"),
    "C91": dict(trigger="ScoreAdditive", set={"minDice": 3, "additiveBonus100": 250}, drop=["perDieAdd", "textures"],
                d="至少提交3颗，且相邻有效点数一高一低交替排列时，加成+2.5。", s="提交3颗以上且点数一高一低交替时，加成+2.5。",
                x="如6-2-5-1-4或2-6-1；相邻两颗不能相同；从高开始或从低开始都可以。"),
    "C125": dict(rarity="稀有", price=6, trigger="GrowthMultiply", set={"growthKind": "multiplier", "growthStart": 125, "growthEvent": 20},
                 drop=["targetIndices", "textures"],
                 d="整手得分×n，n从1.25起。本关有任何一手刷新本局最高单手得分，过关时n+0.2，每关最多一次。",
                 s="本关刷新本局最高单手得分，过关时倍率+0.2。", x="得分倍率从×1.25起；每关最多加一次；打平不算刷新。"),
    "C137": dict(price=4, trigger="ScoreAdditive", set={"additivePerMatch100": 100}, drop=["perDieAdd"],
                 d="每关开始时随机指定一个明星点数；每颗有效点数等于明星点数的提交骰，加成+1。", s="每颗点数等于本关明星点数的提交骰，加成+1。",
                 x="明星点数每关开始时随机抽一个，显示在卡面上；被百变模仿复制时用同一个点数。"),
}

NEW_CARDS = [
    dict(id="C154", name="回心转意", rarity="罕见", price=5, trigger="UnlockDie", eligible=False, params={"freeUnlocksPerHand": 1},
         d="每手一次，可以把一颗已锁定的骰子放回骰池。", s="每手一次，可以把一颗已锁定的骰子放回骰池。",
         x="放回的骰子保持朝上的面；还有投掷次数时可以重掷；多张也只有每手一次。"),
    dict(id="C155", name="状元插金花", rarity="稀有", price=7, trigger="ScoreMultiply", eligible=True,
         params={"minFours": 4, "minOnes": 2, "scoreMultiplier100": 200, "enhancedScoreMultiplier100": 400},
         d="提交含至少4颗有效4点时，整手得分×2；同时含至少2颗有效1点时改为×4。", s="含四颗4点得分×2；再含两颗1点改为×4。",
         x="完整牌型要6格提交栏；不看颜色；可以同时提交其他骰子。"),
    dict(id="C156", name="绝地反击", rarity="罕见", price=4, trigger="ScoreAdditive", eligible=True,
         params={"targetDivisor": 2, "additiveBonus100": 500},
         d="提交时本关累计得分不到目标的一半时，加成+5。", s="本关累计得分不到目标一半时，加成+5。",
         x="按提交前的累计得分算，这一手的得分不计入；目标包括高压挑战和首领减益的调整。"),
]

ITEM_I13 = dict(id="I13", displayName="钥匙串", baseBuyPrice=3, sellPrice=1, window="Selecting", lifetime="Permanent", initialUses=3,
                description="选一颗已锁定的骰子放回骰池；共可使用3次。", shortText="把一颗已锁定的骰子放回骰池，可用3次。",
                details="放回的骰子保持朝上的面，还有投掷次数时可以重掷；不消耗投掷次数；没用完的次数留到下一手、下一关；用完才腾出道具栏。持有“回心转意”时，先用它每手一次的免费次数。")

UPGRADE_TEXT = {
    "U05": ("整骰玻璃，提交时该骰贡献×3；每次被提交计分之后独立判定15%永久损坏，留在骰池里不会碎。",
            "整颗变成玻璃骰：计分×3，每次提交后15%概率碎掉。", "碎掉的骰子永久消失；只有被提交的玻璃骰才判定，留在骰池里不会碎。"),
    "U06": ("目标面增加金层并且权重+1；该面朝上且提交时，加成+0.5，红蓝底色不变。",
            "这一面镀金：朝上提交时加成+0.5，更容易掷出。", "颜色不变；新镀金的面权重+1（上限5）。"),
}
TWO_FACE_UPGRADES = ["U01", "U02", "U03", "U04", "U06", "U07", "U08", "U09", "U10"]

MODIFIERS = {
    "B06": dict(tier=2, minCycle=2, details="基础分、加成增加附魔不触发；提交、投掷附魔不加次数。"),
    # Colour is an additive bonus in p1.1.1: these three no longer fix a "colour multiplier".
    "B22": dict(description="红组倍率固定×1，全红加成清零。", shortText="红组倍率固定×1，全红加成清零。"),
    "B23": dict(description="全蓝加成清零。", shortText="全蓝加成清零。"),
    "B36": dict(description="红组倍率固定×1，颜色加成清零。", shortText="红组倍率固定×1，颜色加成清零。"),
    "B25": dict(details="失效：没有技能、附魔，也不成长，但仍占栏位；不占栏位的卡也按位置算。进关前可以拖动卡牌换位置。"),
    "B29": dict(amount=1, description="最左侧的1件稀有卡牌本关失效。", shortText="最左侧的1件稀有卡牌本关失效。",
                details="失效规则同“缴械”；进关前可以拖动卡牌换位置。"),
    "B35": dict(effects=["ZeroLevels", "GrowthHalf"], description="牌型、颜色按0级计算，成长卡长出的部分按一半计算。",
                shortText="牌型、颜色按0级计算，成长值减半。", details="成长卡的起步值不受影响；成长本身照常累积。"),
    "B37": dict(amount=2, description="最左侧2件卡牌失效，附魔全部失效。", shortText="最左侧2件卡牌失效，附魔全部失效。"),
}


def swap_number(text, new, multiply_words=("整手得分×", "整手×", "得分×")):
    """Replace one 'xN' multiplier phrase by an additive phrase."""
    for word in multiply_words:
        pattern = re.escape(word) + r"\d+(?:\.\d+)?"
        if re.search(pattern, text):
            return re.sub(pattern, new, text, count=1)
    raise ValueError("no multiplier phrase in: " + text)


def patch_controller(card, spec):
    if "price" in spec:
        card["buyPrice"] = spec["price"]
    if "rarity" in spec:
        card["rarity"], card["offerWeight"] = RARITY[spec["rarity"]]
    if "trigger" in spec:
        card["trigger"] = spec["trigger"]
    for key in spec.get("drop", []):
        del card["parameters"][key]
    card["parameters"].update(spec.get("set", {}))
    if "d" in spec:
        card["description"] = spec["d"]
    if "s" in spec:
        card["shortText"] = spec["s"]
    if "x" in spec:
        card["details"] = spec["x"]


def build():
    cfg = json.loads(SOURCE.read_text(encoding="utf-8"))
    if cfg["configVersion"] != "p1.1.0" or cfg["saveSchemaVersion"] != 7:
        raise ValueError("Expected the frozen p1.1.0 base")
    cfg["configVersion"] = "p1.1.1"
    cfg["saveSchemaVersion"] = 8
    cfg["removedControllerIds"] = cfg["removedControllerIds"] + REMOVED
    cfg["maxHandLevel"] = cfg["maxColorLevel"] = 12
    cfg["glassBreakPercent"] = 15
    # top-level additions, placed right after the field they replace or relate to
    out = {}
    for key, value in cfg.items():
        if key == "goldContributionMultiplier":
            out["goldAdd100"] = 50
            out["gildedFaceWeightDelta"] = 1
            continue
        out[key] = value
        if key == "maxFaceWeight":
            out["growthCatchupPerCycle"] = {"additive": 50, "base": 6, "multiplier": 10}
            out["cultivationFacesPerSaleBonus"] = 4
            out["cultivationMaxSaleBonus"] = 6
    cfg = out

    for reward in cfg["redRewards"]:
        reward["factor100"] = RED_REWARDS[reward["count"]]
    for hand in cfg["handDefinitions"]:
        hand["maxLevel"] = 12
    colours = []
    for colour in cfg["colorDefinitions"]:
        entry = {}
        for key, value in colour.items():
            if key == "baseMult100":
                entry["baseAdd100"] = {"all_blue": 25, "all_red": 50}[colour["id"]]
            elif key == "incrementPerLevel100":
                entry["addPerLevel100"] = 50
            elif key == "multiplierZone":
                continue
            elif key == "maxLevel":
                entry["maxLevel"] = 12
            else:
                entry[key] = value
        colours.append(entry)
    cfg["colorDefinitions"] = colours

    # enchantments
    for enchant in cfg["enchantments"]:
        if enchant["id"] == "mult2":
            enchant["displayName"] = "加成增加"
            enchant["buyPremium"] = 5
    rules = {}
    for key, value in cfg["enchantRules"].items():
        if key == "scoreMultiplier":
            rules["scoreAdd100"] = 150
            continue
        rules[key] = value
    rules["baseAdd"] = 15
    rules["nonSubmitControllerIds"] = [i for i in rules["nonSubmitControllerIds"] if i not in REMOVED] + ["C154"]
    rules["shopChanceByCyclePercent"] = ENCHANT_CHANCE
    cfg["enchantRules"] = rules

    # controllers
    controllers = [c for c in cfg["controllers"] if c["id"] not in REMOVED]
    by_id = {c["id"]: c for c in controllers}
    for cid, units in SIMPLE_ADDITIVE.items():
        card = by_id[cid]
        del card["parameters"]["scoreMultiplier"]
        card["parameters"]["additiveBonus100"] = units
        card["trigger"] = "ScoreAdditive"
        card["description"] = swap_number(card["description"], "加成+" + fmt(units))
        card["shortText"] = swap_number(card["shortText"], "加成+" + fmt(units))
    for cid, units in SIMPLE_MULTIPLIER.items():
        card = by_id[cid]
        del card["parameters"]["scoreMultiplier"]
        card["parameters"]["scoreMultiplier100"] = units
        card["description"] = swap_number(card["description"], "整手得分×" + fmt(units))
        card["shortText"] = swap_number(card["shortText"], "得分×" + fmt(units))
    for cid, spec in PATCH.items():
        patch_controller(by_id[cid], spec)
    for new in NEW_CARDS:
        rarity, weight = RARITY[new["rarity"]]
        controllers.append({
            "id": new["id"], "displayName": new["name"], "buyPrice": new["price"], "baseSellPrice": 1, "salePriceMode": "fixed",
            "rarity": rarity, "offerWeight": weight, "trigger": new["trigger"], "scoreEnchantEligible": new["eligible"],
            "parameters": new["params"], "description": new["d"], "shortText": new["s"], "details": new["x"],
        })
    for card in controllers:
        entry = {}
        for key, value in card.items():
            entry[key] = value
            if key == "scoreEnchantEligible":
                entry["isCultivation"] = card["id"] in CULTIVATION
        card.clear()
        card.update(entry)
    too_long = [f"{c['id']} ({len(c['shortText'])}): {c['shortText']}" for c in controllers if len(c["shortText"]) > 28]
    if too_long:
        raise ValueError("shortText over 28 characters:\n" + "\n".join(too_long))
    cfg["controllers"] = controllers

    # items
    for item in cfg["consumables"]:
        if item["id"] in ("I01", "I10"):
            item["baseBuyPrice"] = 1
        entry = {}
        for key, value in item.items():
            entry[key] = value
            if key == "lifetime":
                entry["initialUses"] = 1
        item.clear()
        item.update(entry)
    cfg["consumables"].append({k: ITEM_I13[k] for k in ("id", "displayName", "baseBuyPrice", "sellPrice", "window", "lifetime",
                                                         "initialUses", "description", "shortText", "details")})

    # dice upgrades
    for upgrade in cfg["diceUpgrades"]:
        if upgrade["id"] in UPGRADE_TEXT:
            upgrade["description"], upgrade["shortText"], upgrade["details"] = UPGRADE_TEXT[upgrade["id"]]
        if upgrade["id"] in TWO_FACE_UPGRADES:
            note = "一次可以改同一颗骰子的两个面，价格不变；两个面各抽一次。" if upgrade["id"] == "U07" else "一次可以改同一颗骰子的两个面，价格不变。"
            upgrade["details"] = (upgrade["details"] + note) if upgrade["details"].endswith("。") or not upgrade["details"] else (upgrade["details"] + "。" + note)
        entry = {}
        for key, value in upgrade.items():
            entry[key] = value
            if key == "target":
                entry["maxSelectableFaces"] = 2 if upgrade["id"] in TWO_FACE_UPGRADES else (0 if upgrade["id"] == "U05" else 1)
        upgrade.clear()
        upgrade.update(entry)
        if len(upgrade["shortText"]) > 28:
            raise ValueError(f"shortText of {upgrade['id']} too long: {upgrade['shortText']}")

    # boss modifiers
    for modifier in cfg["bossModifiers"]:
        modifier.update(MODIFIERS.get(modifier["id"], {}))
        if len(modifier["shortText"]) > 28:
            raise ValueError(f"shortText of {modifier['id']} too long")

    for loot in cfg["lootKinds"]:
        if loot["id"] == "L3":
            loot["details"] = "不超过12级。"

    # stages
    for index, stage in enumerate(cfg["stages"], start=1):
        stage["normalTarget"] = str(TARGETS[index])
        if stage["riskEnabled"]:
            stage["riskTarget"] = str(TARGETS[index] * 3 // 2)

    # shop
    shop = cfg["shop"]
    shop["upgradePricePerExistingLevel"] = 1
    shop["selfDieUpgradeLimit"] = 2
    shop["selfDieUpgradePremium"] = 1
    shop["uniqueControllerIds"] = UNIQUE

    cfg["activeContentCount"] = len(cfg["controllers"]) + len(cfg["consumables"]) + len(cfg["services"]) + len(cfg["diceUpgrades"])
    cfg["presentation"]["contentIconCount"] = cfg["activeContentCount"]
    if cfg["activeContentCount"] != 185 or len(cfg["controllers"]) != 150:
        raise ValueError("content count")
    for index in TARGETS:
        if TARGETS[index] % 2:
            raise ValueError("targets must be even")
    return cfg


def render(cfg):
    return json.dumps(cfg, ensure_ascii=False, indent=2) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--out", help="write to another path instead of the game config")
    args = parser.parse_args()
    text = render(build())
    target = Path(args.out) if args.out else TARGET
    if args.check:
        same = target.exists() and target.read_bytes() == text.encode("utf-8")
        print("config matches generator" if same else "DIFFERS: " + str(target))
        return 0 if same else 1
    target.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote p1.1.1: {len(text.encode('utf-8'))} bytes" + (" (stage 4-24 targets are provisional)" if TARGETS_PROVISIONAL else ""))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
