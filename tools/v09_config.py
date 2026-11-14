"""Write the p0.9.0 game configuration.

Input:  tools/balance_sim/baseline/P0Config.p0.8.0.json (frozen copy of the shipped p0.8.0 config)
Spec:   docs/specs/v09/RULES_SPEC.md (50 new controllers, slots, shelf, service O08, stage targets)
Output: DicePrototype/Assets/Game/Data/P0Config.json

    python tools/v09_config.py            # write the game config
    python tools/v09_config.py --check    # fail if the game config differs from what this script produces
"""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "tools" / "balance_sim" / "baseline" / "P0Config.p0.8.0.json"
TARGET = ROOT / "DicePrototype" / "Assets" / "Game" / "Data" / "P0Config.json"

RARITY = {"普通": "common", "罕见": "uncommon", "稀有": "rare"}

# Stages 12-24, calibrated with the upgraded simulator (candidate v09_t1, evidence/v09/balance/run_v09_t1.*):
# 300 bot runs, 26% wins, median power/target 3.1 at stage 24. Stages 1-11 stay at p0.8.0.
LATE_TARGETS = {
    12: 46000, 13: 82000, 14: 145000, 15: 255000, 16: 440000, 17: 740000, 18: 1200000,
    19: 1950000, 20: 3100000, 21: 4800000, 22: 7200000, 23: 10500000, 24: 15000000,}

NON_SUBMIT = ["C78", "C79", "C95", "C96", "C97", "C98", "C99"]

# id, name, rarity, price, trigger label, parameters, description
CARDS = [
    ("C54", "连庄", "稀有", 6, "GrowthMultiply", {"growthPerEvent": 3},
     "整手得分×n，n从1起。每次有效提交后，主牌型与上一次有效提交相同（散点也算）则n+0.15，不同则回到1。"),
    ("C55", "积少成多", "普通", 4, "GrowthBase", {"growthPerEvent": 2},
     "整手基础分加上本卡的成长值。每次有效提交后成长值+2。"),
    ("C56", "深蓝", "罕见", 5, "GrowthMultiply", {"growthPerEvent": 2},
     "整手得分×n，n从1起。每次没有有效红面的有效提交后n+0.1。"),
    ("C57", "红火", "罕见", 5, "GrowthMultiply", {"growthPerEvent": 3, "minRedGroup": 3},
     "整手得分×n，n从1起。每次红组达到3颗及以上的有效提交后n+0.15。"),
    ("C58", "老当益壮", "稀有", 8, "GrowthMultiply", {"growthPerEvent": 4},
     "整手得分×n，n从1起。每通过一关n+0.2。"),
    ("C59", "余勇可贾", "普通", 4, "GrowthBase", {"growthPerEvent": 4},
     "整手基础分加上本卡的成长值。每关结算时，每剩1次提交机会成长值+4。"),
    ("C60", "千锤百炼", "罕见", 5, "GrowthMultiply", {"growthPerEvent": 2},
     "整手得分×n，n从1起。每在商店购买1次骰面改造n+0.1。"),
    ("C61", "学无止境", "罕见", 5, "GrowthMultiply", {"growthPerEvent": 2},
     "整手得分×n，n从1起。牌型或颜色每升1级n+0.1（购买、自选研习、牌型提升都算）。"),
    ("C62", "熟能生巧", "普通", 3, "GrowthBase", {"growthPerEvent": 1},
     "整手基础分加上本卡的成长值。每投掷1次成长值+1。"),
    ("C63", "收藏家", "罕见", 5, "ScoreMultiply", {"perDieUnits10": 3},
     "整手得分乘以永久骰数量的0.3倍（5颗×1.5，8颗×2.4），最低×1。"),
    ("C64", "一马当先", "普通", 4, "Retrigger", {"retriggers": 1, "index": 0},
     "第一颗提交骰的点数贡献再计1次。"),
    ("C65", "压轴好戏", "罕见", 5, "Retrigger", {"retriggers": 2, "requireFullCapacity": True},
     "占满提交栏时，最后一颗提交骰的点数贡献再计2次。"),
    ("C66", "小小回声", "罕见", 5, "Retrigger", {"retriggers": 1, "pips": [1, 2]},
     "每颗有效1点或2点的提交骰，点数贡献再计1次。"),
    ("C67", "掷地有声", "罕见", 5, "Retrigger", {"retriggers": 1, "pips": [6]},
     "每颗有效6点的提交骰，点数贡献再计1次。"),
    ("C68", "谢幕", "罕见", 4, "Retrigger", {"retriggers": 1},
     "本关最后一次提交机会时，所有提交骰的点数贡献再计1次。"),
    ("C69", "红色共鸣", "稀有", 7, "Retrigger", {"retriggers": 1},
     "最大红组里的每颗提交骰，点数贡献再计1次。"),
    ("C70", "板凳深度", "普通", 3, "PoolBaseAdd", {"baseAddPerPoolDie": 6},
     "每颗留在骰池里没有提交的骰子，整手基础分+6。"),
    ("C71", "幕后推手", "稀有", 7, "PoolMultiply", {"pip": 6, "stepNumerator": 3, "stepDenominator": 2},
     "每颗留在骰池里的6点骰，整手得分×1.5。"),
    ("C72", "厚积薄发", "罕见", 4, "PoolBaseAdd", {"poolPipSumMultiplier": 2},
     "留在骰池里的骰子点数之和×2，加入整手基础分。"),
    ("C73", "场外红人", "稀有", 7, "RedGrouping", {"idempotent": True},
     "提交骰里有红组时，留在骰池里的红面骰也算进最大的红组。"),
    ("C74", "空城计", "罕见", 5, "ScoreMultiply", {"scoreMultiplier": 3},
     "留在骰池里的骰子数量不少于提交骰数量时，整手得分×3。"),
    ("C75", "一锤定音", "罕见", 5, "ScoreMultiply", {"scoreMultiplier": 3, "maxRollsUsed": 1},
     "本手只投掷1次就提交时，整手得分×3。"),
    ("C76", "越挫越勇", "普通", 3, "BaseAdd", {"baseAddPerExtraRoll": 10},
     "本手从第2次投掷起，每多投1次整手基础分+10。"),
    ("C77", "孤注一掷", "罕见", 5, "ScoreMultiply", {"scoreMultiplier": 2},
     "提交时剩余投掷次数为0，整手得分×2。"),
    ("C78", "再给一次", "罕见", 5, "RollBudget", {"rollsPerHandDelta": 1},
     "每手投掷次数+1。"),
    ("C79", "多交一手", "稀有", 7, "SubmissionBudget", {"submissionsDelta": 1},
     "每关提交次数+1。"),
    ("C80", "天生一对", "罕见", 4, "BaseAdd", {"pairSum": 7, "baseAddPerPair": 14},
     "提交骰中每有一对有效点数相加为7的骰子（每颗只配一次），整手基础分+14。"),
    ("C81", "散兵游勇", "罕见", 4, "ScoreMultiply", {"scoreMultiplier": 3},
     "主牌型为散点时，整手得分×3。"),
    ("C82", "满堂彩", "罕见", 5, "ScoreMultiply", {"scoreMultiplier": 3, "handIds": ["full_house"]},
     "提交含三条带一对时，整手得分×3。"),
    ("C83", "顺水推舟", "罕见", 5, "ScoreMultiply", {"scoreMultiplier": 3},
     "顺子成立时，整手得分×3。"),
    ("C84", "成双成对", "普通", 3, "HandMultiplierAdd", {"handMultAdd100": 150, "minSamePip": 2},
     "提交含对子时，牌型倍率+1.5。"),
    ("C85", "三足鼎立", "普通", 4, "HandMultiplierAdd", {"handMultAdd100": 300, "minSamePip": 3},
     "提交含三条时，牌型倍率+3。"),
    ("C86", "三三得九", "罕见", 5, "ScoreMultiply", {"scoreMultiplier": 4, "handIds": ["three_pair", "two_trips", "four_pair"]},
     "提交含三对子、双三条或四对子时，整手得分×4。"),
    ("C87", "得寸进尺", "稀有", 8, "ScoreMultiply", {"minSamePip": 4, "stepNumerator": 3, "stepDenominator": 2},
     "最大同点数超过3颗后，每多1颗整手得分×1.5（4同×1.5，5同×2.25，8同约×7.6）。"),
    ("C88", "杂牌军", "罕见", 4, "ScoreMultiply", {"scoreMultiplier": 3, "minDice": 4},
     "至少提交4颗且有效点数互不相同时，整手得分×3。"),
    ("C89", "金玉满堂", "稀有", 7, "ScoreMultiply", {"stepNumerator": 3, "stepDenominator": 2},
     "每颗黄金面朝上的提交骰，整手得分×1.5。"),
    ("C90", "玻璃心", "罕见", 4, "GrowthMultiply", {"growthPerEvent": 15},
     "整手得分×n，n从1起。每有1颗玻璃骰碎裂n+0.75。"),
    ("C91", "纹理大师", "普通", 3, "PipContribution", {"perDieAdd": 8, "textures": ["smooth", "rough"]},
     "每颗光滑或粗糙面朝上的提交骰，点数贡献+8。"),
    ("C92", "纯色主义", "罕见", 4, "PipContribution", {"perDieAdd": 10},
     "提交骰所属的骰子六个面同色时，该骰点数贡献+10。"),
    ("C93", "始终如一", "罕见", 5, "PipMultiply", {"dieContributionMultiplier": 2},
     "提交骰所属的骰子六个面点数全部相同时，该骰点数贡献×2。"),
    ("C94", "表里如一", "普通", 3, "PipContribution", {},
     "每颗提交骰对面的永久点数也加入它的点数贡献。"),
    ("C95", "铁饭碗", "普通", 5, "StageFinalize", {"goldPerStage": 3},
     "每关结算额外获得3金币。"),
    ("C96", "利滚利", "罕见", 5, "StageFinalize", {"interestPrincipalAdd": 25},
     "计息本金上限+25（单张时每关利息最多10）。"),
    ("C97", "以战养战", "罕见", 4, "AfterScore", {"goldPerTrigger": 3, "maxTriggersPerStage": 2, "targetDivisor": 2},
     "单次提交得分达到本关目标的一半时获得3金币，每关最多2次。"),
    ("C98", "砍价高手", "罕见", 5, "ShopPrice", {"priceDelta": -1},
     "商店里控制器与骰面改造的价格−1，最低1。"),
    ("C99", "聚宝盆", "罕见", 5, "StageFinalize", {"saleBonusPerStage": 1},
     "每关结算时，持有的每件控制器卖价+1。"),
    ("C100", "祭坛", "稀有", 6, "GrowthMultiply", {"growthPerBuyPrice": 2},
     "整手得分×n，n从1起。每关开始时摧毁右侧相邻的控制器，n增加该卡基础买入价（不含附魔加价）的十分之一。放在最右侧则不摧毁。"),
    ("C101", "双刃剑", "稀有", 6, "ScoreMultiply", {"scoreMultiplier": 4, "submissionsDelta": -1},
     "整手得分×4；每关提交次数−1，最少1次。"),
    ("C102", "背水一战", "罕见", 5, "ScoreMultiply", {"scoreMultiplier": 3},
     "本关最后一次提交机会时，整手得分×3。"),
    ("C103", "精兵简政", "罕见", 5, "ScoreMultiply",
     {"scoreMultiplier": 2, "maxOwnedPermanentDice": 4, "strictScoreMultiplier": 4, "strictMaxOwnedPermanentDice": 3},
     "永久骰不超过4颗时整手得分×2，不超过3颗时×4。"),
]


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
    assert cfg["configVersion"] == "p0.8.0"
    cfg["configVersion"] = "p0.9.0"
    cfg["saveSchemaVersion"] = 5
    cfg["initialControllerSlots"] = 5
    cfg = insert_after(cfg, "initialControllerSlots", [("maxOrdinaryControllerSlots", 7), ("controllerSlotPriceStep", 6)])
    weights = cfg["shop"]["controllerWeights"]
    for card_id, name, rarity, price, trigger, parameters, description in CARDS:
        rarity_id = RARITY[rarity]
        cfg["controllers"].append({
            "id": card_id, "displayName": name, "buyPrice": price, "baseSellPrice": 1, "salePriceMode": "fixed",
            "rarity": rarity_id, "offerWeight": weights[rarity_id], "trigger": trigger, "maxOwnedNormal": 1,
            "scoreEnchantEligible": card_id not in NON_SUBMIT, "parameters": parameters, "description": description,
        })
    cfg["enchantRules"]["nonSubmitControllerIds"] += NON_SUBMIT
    for service in cfg["services"]:
        if service["id"] == "O05":
            service["description"] = "每店控制器报价数量永久+1，从4到最多6；当前店只生成新位置的一件报价，不补其他已售商品。"
            service["priceRule"] = "3+2×此前购买次数，最多2次"
    cfg["services"].append({
        "id": "O08", "displayName": "扩充栏位", "baseBuyPrice": 8, "priceRule": "8+6×此前购买次数，最多2次",
        "description": "普通控制器栏位永久+1，从5到最多7；受折扣，最低1。免槽附魔的控制器不占栏位，总件数上限仍是10。",
    })
    cfg["shop"]["initialControllerOffers"] = 4
    cfg["shop"]["serviceOffers"] = 2
    cfg["activeContentCount"] = len(cfg["controllers"]) + len(cfg["consumables"]) + len(cfg["services"]) + len(cfg["diceUpgrades"])
    cfg["presentation"]["contentIconCount"] = cfg["activeContentCount"]
    for stage in cfg["stages"]:
        index = int(stage["stageId"].split("_")[1])
        if index in LATE_TARGETS:
            target = LATE_TARGETS[index]
            stage["normalTarget"] = str(target)
            if stage["riskEnabled"]:
                stage["riskTarget"] = str(target * 5 // 4)
    assert len(cfg["controllers"]) == 100 and cfg["activeContentCount"] == 122
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
