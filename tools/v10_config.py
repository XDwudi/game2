"""Write the p1.0.0 game configuration.

Input:  tools/balance_sim/baseline/P0Config.p0.9.0.json (frozen copy of the shipped p0.9.0 config)
Spec:   docs/specs/v10/RULES_SPEC.md
Output: DicePrototype/Assets/Game/Data/P0Config.json

    python tools/v10_config.py            # write the game config
    python tools/v10_config.py --check    # fail if the game config differs from what this script produces
"""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "tools" / "balance_sim" / "baseline" / "P0Config.p0.9.0.json"
TARGET = ROOT / "DicePrototype" / "Assets" / "Game" / "Data" / "P0Config.json"

RARITY = {"普通": "common", "罕见": "uncommon", "稀有": "rare"}

# Normal targets per stage. Calibrated with the p1.0 simulator (see docs/specs/v10/BALANCE_REPORT.md).
TARGETS = {
    1: 300, 2: 600, 3: 1050, 4: 1800, 5: 2800, 6: 4200, 7: 6200, 8: 8800, 9: 12000, 10: 17000, 11: 24000, 12: 46000,
    13: 82000, 14: 145000, 15: 255000, 16: 440000, 17: 740000, 18: 1200000, 19: 1950000, 20: 3100000, 21: 4800000,
    22: 7200000, 23: 10500000, 24: 15000000,
}
STAGE_KINDS = {1: "normal", 2: "pressure", 3: "boss"}
PRESSURE_TARGET_PERCENT = 150
PRESSURE_REWARD = 10

NON_SUBMIT = ["C110", "C111", "C112", "C113", "C114", "C115", "C116", "C120", "C121", "C128", "C131", "C133", "C136",
              "C140", "C145"]

# id, name, rarity, price, trigger label, parameters, description
CARDS = [
    # 6.1 submission order
    ("C104", "红蓝相间", "罕见", 4, "ScoreMultiply", {"scoreMultiplier": 3, "minDice": 3},
     "至少提交3颗，且每两颗相邻提交骰的有效颜色都不同时，整手得分×3。"),
    ("C105", "泾渭分明", "罕见", 4, "ScoreMultiply", {"scoreMultiplier": 2},
     "提交骰里红蓝都有，且所有红骰都排在所有蓝骰左侧时，整手得分×2。"),
    ("C106", "对称美学", "罕见", 5, "ScoreMultiply", {"scoreMultiplier": 2, "minDice": 3},
     "至少提交3颗，且有效点数左右对称时，整手得分×2。"),
    ("C107", "顺藤摸瓜", "普通", 3, "PipContribution", {"perDieAdd": 10},
     "每颗有效点数恰好比左边相邻骰大1的提交骰，点数贡献+10。"),
    ("C108", "排头兵", "普通", 3, "BaseAdd", {"baseAddPerPip": 5, "index": 0},
     "第一颗提交骰的有效点数×5，加入整手基础分。"),
    ("C109", "中流砥柱", "罕见", 4, "Retrigger", {"retriggers": 2, "minDice": 3},
     "提交奇数颗且至少3颗时，正中间那颗提交骰的点数贡献再计2次。"),
    # 6.2 random generation
    ("C110", "百宝袋", "罕见", 5, "StageStart", {"itemsPerStage": 1},
     "每关开始时，道具栏有空位则获得1个随机道具。"),
    ("C111", "杂货郎", "罕见", 4, "ShopGift", {"giftChoices": 3, "giftPicks": 1},
     "每家商店附赠一次道具三选一（免费，需要道具栏空位）。"),
    ("C112", "滥竽充数", "罕见", 4, "StageFinalize", {"rarity": "common"},
     "每关结算时，若有空的普通栏位，获得1张随机的未持有普通控制器。"),
    ("C113", "铁匠铺", "稀有", 6, "ShopGift", {"giftChoices": 3, "giftPicks": 1},
     "每家商店附赠一次骰面改造三选一（免费）。"),
    ("C114", "点化", "稀有", 7, "BossClear", {"enchantsPerBoss": 1},
     "每通过一个首领关，随机1件没有附魔的其他控制器获得一个随机附魔。"),
    ("C115", "无心插柳", "普通", 3, "StageFinalize", {"facesPerStage": 2, "upgradeIds": ["U01", "U02", "U03", "U04", "U06", "U07"]},
     "每关结算时，随机2个永久骰面各获得一种随机的骰面改造（光滑、粗糙、染红、染蓝、黄金、随机变形）。"),
    ("C116", "招兵买马", "罕见", 5, "BossClear", {"dicePerBoss": 1},
     "每通过一个首领关，若永久骰未满，获得1颗随机新骰。"),
    # 6.3 die-face growth
    ("C117", "点金手", "稀有", 6, "AfterScore", {"targetIndices": "last"},
     "每次有效提交后，最后一颗提交骰的朝上面永久镀金。"),
    ("C118", "表里同色", "罕见", 4, "AfterScore", {"targetIndices": "all_opposite"},
     "每次有效提交后，每颗提交骰朝上面的对面，永久染成与朝上面相同的颜色。"),
    ("C119", "潜移默化", "稀有", 7, "AfterScore", {"index": 0},
     "每次有效提交后，第一颗提交骰上一个点数不同的面，永久变成朝上面的点数。"),
    ("C120", "拔苗助长", "稀有", 7, "StageFinalize", {"delta": 1},
     "每关结算时，每颗永久骰点数最小的面+1，上限6。"),
    ("C121", "大事化小", "罕见", 5, "StageFinalize", {"delta": -1},
     "每关结算时，每颗永久骰点数最大的面−1，下限1。"),
    ("C122", "见五成群", "罕见", 4, "AfterScore", {"pip": 5},
     "每次有效提交后，每颗提交骰的朝上面点数永久向5靠近1点。"),
    ("C123", "二三其德", "罕见", 4, "AfterScore", {"pips": [2, 3]},
     "每次有效提交后，每颗提交骰的朝上面：1点变2点；4、5、6点−1。"),
    ("C124", "灌铅", "稀有", 6, "AfterScore", {"delta": 1},
     "每次有效提交后，每颗提交骰朝上面的权重+1（上限5），以后更容易掷出这一面。"),
    ("C125", "抛光匠", "普通", 3, "AfterScore", {"targetIndices": "first_and_last_distinct", "textures": ["smooth"]},
     "每次有效提交后，第一颗和最后一颗提交骰的朝上面若没有纹理，变为光滑。"),
    ("C126", "养精蓄锐", "罕见", 4, "AfterScore", {"delta": 1},
     "每次有效提交后，每颗留在骰池里的骰子，朝上面点数永久+1，上限6。"),
    ("C127", "移花接木", "罕见", 5, "AfterScore", {"submissionOrdinal": 0, "minDice": 2, "sourceIndex": 0},
     "本关第一次有效提交且至少2颗时，把第一颗提交骰朝上面的点数、颜色、纹理、黄金复制到最后一颗提交骰的朝上面。"),
    ("C128", "吹玻璃", "罕见", 4, "StageStart", {"dicePerStage": 1},
     "每关开始时，编号最小的非玻璃永久骰变为玻璃骰。"),
    # 6.4 boss and pressure
    ("C129", "屠龙者", "罕见", 5, "GrowthMultiply", {"growthPerEvent": 10},
     "整手得分×n，n从1起。每通过一个首领关n+0.5。"),
    ("C130", "逆流而上", "稀有", 6, "ScoreMultiply", {"scoreMultiplier": 3},
     "首领关里整手得分×3。"),
    ("C131", "替罪羊", "罕见", 4, "StageStart", {},
     "首领关开始时自毁，本关的首领减益解除。"),
    ("C132", "高压锅", "罕见", 4, "GrowthMultiply", {"growthPerEvent": 12},
     "整手得分×n，n从1起。每完成一次高压挑战n+0.6。"),
    ("C133", "赏金猎人", "普通", 4, "StageFinalize", {"goldPerBoss": 5, "goldPerPressure": 3},
     "通过首领关结算额外+5金币；完成高压挑战结算额外+3金币。"),
    # 6.5 items and temporary dice
    ("C134", "物尽其用", "罕见", 4, "GrowthMultiply", {"growthPerEvent": 3},
     "整手得分×n，n从1起。每使用1个道具n+0.15。"),
    ("C135", "囤积居奇", "普通", 3, "BaseAdd", {"baseAddPerItem": 12},
     "背包里每有1个道具，整手基础分+12。"),
    ("C136", "雇佣兵", "罕见", 5, "StageStart", {"dicePerStage": 1},
     "每关开始时获得1颗标准临时骰（临时骰最多3颗、活动骰最多10颗）。"),
    ("C137", "客串明星", "普通", 3, "PipContribution", {"perDieAdd": 15},
     "每颗提交的临时骰，点数贡献+15。"),
    # 6.6 gold
    ("C138", "一掷千金", "稀有", 6, "ScoreMultiply", {"scoreMultiplier": 3, "goldCost": 3},
     "提交时钱包至少3金币：整手得分×3，计分后失去3金币。"),
    ("C139", "富甲一方", "罕见", 5, "ScoreMultiply", {"goldPerStep": 10, "stepUnits4": 1, "maxSteps": 8},
     "钱包每满10金币，整手得分的倍率+0.25（40金币×2），最高×3。"),
    ("C140", "回收大王", "罕见", 4, "ShopPrice", {},
     "控制器按标价（买入价加附魔加价）全额回收。"),
    ("C141", "昙花一现", "普通", 3, "ScoreMultiply", {"initialMultiplierUnits2": 6, "expireGrowth": 4},
     "整手得分×3；每通过一关倍率−0.5；降到×1时自毁。"),
    # 6.7 hands and archetypes
    ("C142", "一条龙", "罕见", 4, "HandMultiplierAdd", {"handMultAdd100": 500},
     "顺子成立时，牌型倍率+5。"),
    ("C143", "好事成双", "罕见", 4, "Retrigger", {"retriggers": 1, "requiredDice": 2},
     "有效点数恰好出现2颗的那些提交骰，点数贡献各再计1次。"),
    ("C144", "偶数为王", "罕见", 4, "ScoreMultiply", {"scoreMultiplier": 3},
     "提交的有效点数全为偶数时，整手得分×3。"),
    ("C145", "防碎膜", "罕见", 5, "RollRisk", {"glassBreakPercent": 2},
     "玻璃骰每次投掷的碎裂概率由8%降为2%。"),
    ("C146", "锦上添红", "罕见", 5, "RedGrouping", {"idempotent": True},
     "提交骰里有红组时，红组数量按多1颗计算。"),
    ("C147", "观众席", "罕见", 5, "ScoreMultiply", {"stepUnits4": 1},
     "每颗留在骰池里没有提交的骰子，整手得分的倍率+0.25。"),
    ("C148", "见好就收", "罕见", 4, "ScoreMultiply", {"perRollUnits2": 1},
     "提交时每剩1次未用投掷，整手得分的倍率+0.5。"),
    ("C149", "单刀赴会", "罕见", 4, "ScoreMultiply", {"scoreMultiplier": 4, "requiredDice": 1},
     "恰好提交1颗骰时，整手得分×4。"),
    ("C150", "渐入佳境", "罕见", 5, "ScoreMultiply", {},
     "本关第n次提交，整手得分×n。"),
    # 6.8 build
    ("C151", "留白", "罕见", 4, "ScoreMultiply", {"perSlotUnits2": 1},
     "每个空着的普通控制器栏位，整手得分的倍率+0.5。"),
    ("C152", "草根逆袭", "罕见", 5, "ScoreMultiply", {"rarity": "common", "stepUnits4": 1},
     "每持有1张普通稀有度的控制器，整手得分的倍率+0.25。"),
    ("C153", "开门红", "罕见", 4, "ScoreMultiply", {"scoreMultiplier": 3, "submissionOrdinal": 0},
     "本关第一次提交，整手得分×3。"),
]

# id, name, price, window, lifetime, description
CONSUMABLES = [
    ("I08", "破咒符", 4, "HandReady", "ThisStage", "首领关第一手首掷之前使用：本关首领减益解除；入关时已失去的金币不返还。"),
    ("I09", "灵光一闪", 3, "HandReadyOrSelecting", "ThisHand", "本手的下一次有效提交得分×2；同一手不叠加。"),
    ("I10", "蓝色浪潮", 2, "Selecting", "ThisHand", "下一次有效提交全部骰子临时视为蓝色；与红色浪潮同一手只能生效一个。"),
    ("I11", "点金笔", 3, "Selecting", "Permanent", "第一颗已锁定骰子的朝上面永久镀金。"),
    ("I12", "扩容符", 2, "HandReadyOrSelecting", "ThisStage", "本关提交栏容量+1，最多到8。"),
]

# id, name, price, description
UPGRADES = [
    ("U08", "精雕", 2, "目标面点数+1，上限6。"),
    ("U09", "削点", 1, "目标面点数−1，下限1。"),
    ("U10", "配重", 2, "目标面权重+1（上限5），更容易掷出这一面。"),
    ("U11", "镜像", 2, "把目标面的点数与颜色复制到它的对面。"),
]

# id, name, base price, price rule, description
SERVICES = [
    ("O09", "贿赂首领", 4, "4+2×本局此前购买次数", "重新抽取本周期首领的减益；受折扣，最低1。"),
    ("O10", "理财顾问", 6, "6+4×此前购买次数，最多2次", "计息本金上限永久+10（每关利息上限+2）；受折扣，最低1。"),
    ("O11", "附魔台", 6, "6+3×本局此前购买次数", "随机1件没有附魔的控制器获得一个随机附魔；受折扣，最低1。"),
]

# id, name, tier, effects, amount, filter, description. tier 4 = final boss pool. minCycle follows the tier.
MIN_CYCLE = {1: 1, 2: 2, 3: 4, 4: 8}
MODIFIERS = [
    ("B01", "满员", 1, ["MustFill"], 0, "", "必须占满提交栏才能提交（骰子不够时提交全部骰子）。"),
    ("B02", "徒手", 1, ["NoItems"], 0, "", "本关不能使用道具。"),
    ("B03", "苛税", 1, ["TaxPerDie"], 1, "", "每次提交后，每提交1颗骰子失去1金币。"),
    ("B04", "白干", 1, ["NoReward"], 0, "", "本关没有过关金币和剩余提交奖励。"),
    ("B05", "出头鸟", 1, ["SealFirst"], 0, "", "第一颗提交骰被封印（点数贡献为0）。"),
    ("B06", "沉默", 1, ["EnchantSilence"], 0, "", "控制器的附魔本关失效（不占栏位除外）。"),
    ("B07", "停滞", 1, ["GrowthFreeze"], 0, "", "本关内控制器的成长值不变。"),
    ("B08", "禁顺", 1, ["NoStraight"], 0, "", "顺子不成立。"),
    ("B09", "急行军", 2, ["RollsDelta"], -1, "", "每手投掷次数−1。"),
    ("B10", "窄门", 2, ["CapacityDelta"], -1, "", "提交栏容量−1。"),
    ("B11", "厚墙", 2, ["TargetPercent"], 150, "", "本关目标×1.5。"),
    ("B12", "过路费", 2, ["LoseHalfGold"], 0, "", "入关时失去一半金币。"),
    ("B13", "封奇", 2, ["SealOdd"], 0, "", "有效奇数点的提交骰被封印（点数贡献为0）。"),
    ("B14", "封偶", 2, ["SealEven"], 0, "", "有效偶数点的提交骰被封印（点数贡献为0）。"),
    ("B15", "封小", 2, ["SealLow"], 0, "", "有效1至3点的提交骰被封印（点数贡献为0）。"),
    ("B16", "封大", 2, ["SealHigh"], 0, "", "有效4至6点的提交骰被封印（点数贡献为0）。"),
    ("B17", "封红", 2, ["SealRed"], 0, "", "有效红色的提交骰被封印（点数贡献为0）。"),
    ("B18", "封蓝", 2, ["SealBlue"], 0, "", "有效蓝色的提交骰被封印（点数贡献为0）。"),
    ("B19", "半吊子", 2, ["HandHalf"], 0, "", "牌型倍率减半，最低×1。"),
    ("B20", "小打小闹", 2, ["SmallHandsOnly"], 0, "", "同点牌型只认一对、两对、三条。"),
    ("B21", "不重样", 2, ["NoRepeatHand"], 0, "", "本关已经打过的主牌型，再打时牌型倍率为×1。"),
    ("B22", "褪红", 2, ["NoRed"], 0, "", "红组倍率与全红倍率固定×1。"),
    ("B23", "褪蓝", 2, ["NoBlue"], 0, "", "全蓝倍率固定×1。"),
    ("B24", "削级", 2, ["HandDowngrade"], 1, "", "每次提交后，本次主牌型永久降1级。"),
    ("B25", "缴械", 2, ["DisableLeftmost"], 2, "", "最左侧的2件控制器本关失效。"),
    ("B26", "慢热", 2, ["FirstHalf"], 0, "", "本关第一次提交的得分减半。"),
    ("B27", "惜字如金", 3, ["SubmissionsDelta"], -1, "", "本关提交次数−1。"),
    ("B28", "打回原形", 3, ["ZeroLevels"], 0, "", "牌型与颜色都按0级计算。"),
    ("B29", "凡人", 3, ["DisableRarity"], 0, "rare", "稀有控制器本关失效。"),
    ("B30", "强弩之末", 3, ["LaterHalf"], 0, "", "本关第二次提交起得分减半。"),
    ("B31", "天堑", 4, ["TargetPercent"], 200, "", "本关目标×2。"),
    ("B32", "孤掷", 4, ["RollsFixed"], 1, "", "每手只有1次投掷（道具和再来三次仍可增加）。"),
    ("B33", "破釜沉舟", 4, ["SubmissionsDelta"], -2, "", "本关提交次数−2，最少1次。"),
    ("B34", "独木桥", 4, ["CapacityDelta"], -2, "", "提交栏容量−2。"),
    ("B35", "返璞归真", 4, ["ZeroLevels", "GrowthZero"], 0, "", "牌型、颜色按0级计算，成长值按0计算。"),
    ("B36", "色盲", 4, ["NoRed", "NoBlue"], 0, "", "颜色倍率与红组倍率全部固定×1。"),
    ("B37", "万籁俱寂", 4, ["DisableLeftmost", "EnchantSilence"], 3, "", "最左侧3件控制器失效，附魔全部失效。"),
    ("B38", "隔行封印", 4, ["SealAlternate"], 0, "", "提交栏第1、3、5、7位的骰子被封印（点数贡献为0）。"),
    ("B39", "釜底抽薪", 4, ["PipsOnly"], 0, "", "基础分只计骰子点数与未用投掷奖励，其余加值与逐骰倍率全部不计。"),
]

# id, name, weight, picks, description
LOOT = [
    ("L1", "稀有控制器", 30, 1, "3张稀有控制器，免费选1张。"),
    ("L2", "附魔控制器", 20, 1, "3张带附魔的控制器，免费选1张。"),
    ("L3", "牌型精研", 20, 1, "3个牌型或颜色，选1个直接+2级。"),
    ("L4", "骰面工坊", 10, 3, "3项骰面改造，全部免费。"),
    ("L5", "特制骰子", 10, 1, "同点骰、纯色骰、镀金骰，免费选1颗。"),
    ("L6", "道具补给", 10, 3, "3个道具，全部免费（受道具栏限制）。"),
]
LOOT_GOLD = 5
LOOT_HAND_LEVELS = 2
MAX_FACE_WEIGHT = 5
STAGE_SEED_XOR = 1540483477  # 0x5BD1E995, distinct from the dice / shop / effect stream constants
INTEREST_CAP_STEP = 10       # O10
INTEREST_CAP_MAX_PURCHASES = 2


def insert_after(mapping: dict, anchor: str, pairs: list) -> dict:
    out = {}
    for k, v in mapping.items():
        out[k] = v
        if k == anchor:
            for key, value in pairs:
                out[key] = value
    if any(key not in out for key, _ in pairs):
        raise KeyError(anchor)
    return out


def build() -> dict:
    cfg = json.loads(SOURCE.read_text(encoding="utf-8"))
    assert cfg["configVersion"] == "p0.9.0"
    cfg["configVersion"] = "p1.0.0"
    cfg["saveSchemaVersion"] = 6
    cfg = insert_after(cfg, "maxInterestPrincipal", [("interestPrincipalStep", INTEREST_CAP_STEP), ("maxInterestPrincipalPurchases", INTEREST_CAP_MAX_PURCHASES)])
    cfg = insert_after(cfg, "goldContributionMultiplier", [("maxFaceWeight", MAX_FACE_WEIGHT)])
    cfg["riskStageReward"] = PRESSURE_REWARD
    weights = cfg["shop"]["controllerWeights"]
    for card_id, name, rarity, price, trigger, parameters, description in CARDS:
        rarity_id = RARITY[rarity]
        cfg["controllers"].append({
            "id": card_id, "displayName": name, "buyPrice": price, "baseSellPrice": 1, "salePriceMode": "fixed",
            "rarity": rarity_id, "offerWeight": weights[rarity_id], "trigger": trigger, "maxOwnedNormal": 1,
            "scoreEnchantEligible": card_id not in NON_SUBMIT, "parameters": parameters, "description": description,
        })
    cfg["enchantRules"]["nonSubmitControllerIds"] += NON_SUBMIT
    for item_id, name, price, window, lifetime, description in CONSUMABLES:
        cfg["consumables"].append({"id": item_id, "displayName": name, "baseBuyPrice": price, "sellPrice": 1,
                                   "window": window, "lifetime": lifetime, "description": description})
    for upgrade_id, name, price, description in UPGRADES:
        cfg["diceUpgrades"].append({"id": upgrade_id, "displayName": name, "buyPrice": price, "target": "Face", "description": description})
    for service_id, name, price, rule, description in SERVICES:
        cfg["services"].append({"id": service_id, "displayName": name, "baseBuyPrice": price, "priceRule": rule, "description": description})
    cfg["activeContentCount"] = len(cfg["controllers"]) + len(cfg["consumables"]) + len(cfg["services"]) + len(cfg["diceUpgrades"])
    cfg["presentation"]["contentIconCount"] = cfg["activeContentCount"]
    stages = []
    for stage in cfg["stages"]:
        index = int(stage["stageId"].split("_")[1])
        kind = STAGE_KINDS[stage["indexInChapter"]]
        target = TARGETS[index]
        risk = kind == "pressure"
        assert target * PRESSURE_TARGET_PERCENT % 100 == 0
        stages.append({
            "stageId": stage["stageId"], "chapter": stage["chapter"], "indexInChapter": stage["indexInChapter"], "stageKind": kind,
            "normalTarget": str(target), "normalReward": stage["normalReward"], "riskEnabled": risk,
            "riskTarget": str(target * PRESSURE_TARGET_PERCENT // 100) if risk else None, "riskReward": PRESSURE_REWARD if risk else 0,
            "shopAfterClear": stage["shopAfterClear"],
        })
    cfg["stages"] = stages
    modifiers = [{"id": m[0], "displayName": m[1], "tier": m[2], "minCycle": MIN_CYCLE[m[2]], "effects": m[3], "amount": m[4],
                  "filter": m[5], "description": m[6]} for m in MODIFIERS]
    loot = [{"id": k[0], "displayName": k[1], "weight": k[2], "picks": k[3], "description": k[4]} for k in LOOT]
    cfg = insert_after(cfg, "stages", [("bossModifiers", modifiers), ("lootKinds", loot), ("lootGoldValue", LOOT_GOLD), ("lootHandLevels", LOOT_HAND_LEVELS)])
    cfg["rng"] = insert_after(cfg["rng"], "effectSeedXor", [("stageSeedXor", STAGE_SEED_XOR)])
    assert len(cfg["controllers"]) == 150 and cfg["activeContentCount"] == 184
    assert len(modifiers) == 39 and sum(1 for m in modifiers if m["tier"] < 4) == 30 and sum(1 for m in modifiers if m["tier"] == 4) == 9
    assert len({c["displayName"] for c in cfg["controllers"]}) == 150, "duplicate controller name"
    return cfg


def render() -> str:
    return json.dumps(build(), ensure_ascii=False, indent=2) + "\n"


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    text = render()
    if args.check:
        ok = TARGET.read_text(encoding="utf-8") == text
        print("config matches generator" if ok else "config DIFFERS from generator")
        raise SystemExit(0 if ok else 1)
    TARGET.write_text(text, encoding="utf-8", newline="\n")
    print("wrote", TARGET.relative_to(ROOT))


if __name__ == "__main__":
    main()
