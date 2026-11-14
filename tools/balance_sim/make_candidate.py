"""Build a v0.8 balance candidate from the shipped P0Config.json.

Every change is listed in CHANGES below so the proposal document and the simulated config cannot drift apart.
The output is a simulation input only; it is not loaded by the game.

    python tools/balance_sim/make_candidate.py [--variant sim] [--out path]

`--variant sim` additionally removes C53 from the shop (offerWeight 0). Its new outcome table needs two
constants changed in game code, which the simulator cannot do, so leaving it in would overstate its strength.
"""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "tools" / "balance_sim" / "baseline" / "P0Config.p0.6.0.json"  # frozen p0.6.0 input

WEIGHTS = {"common": 6, "uncommon": 4, "rare": 2}

# id: (rarity, buyPrice, {parameter: value})   -- None keeps the shipped value
CONTROLLERS = {
    "C01": (None, 4, {"extraInterestPerTen": 0}),
    "C02": (None, 7, {"scoreMultiplier": 3}),
    "C05": (None, 6, {}),
    "C08": (None, 4, {}),
    "C09": (None, 7, {"scoreMultiplier": 3}),
    "C12": (None, 5, {}),
    "C13": (None, 2, {"oddPerDieAdd": 4}),
    "C15": (None, 2, {}),
    "C18": (None, 3, {"scoreMultiplier": 4}),
    "C19": (None, 2, {"perDieAdd": 8}),
    "C20": (None, 2, {"perDieAdd": 15}),
    "C21": (None, 4, {}),
    "C22": (None, 6, {}),
    "C28": (None, 4, {"scoreMultiplier": 3}),
    "C30": ("uncommon", 4, {"scoreMultiplier": 2}),
    "C31": (None, 6, {"scoreMultiplier": 3}),
    "C32": (None, 4, {"scoreMultiplier": 2}),
    "C33": (None, 7, {"scoreMultiplier": 4, "firstMinusSecondAtLeast": 3}),
    "C40": (None, 4, {"scoreMultiplier": 4}),
    "C41": (None, 8, {}),
    "C42": (None, 6, {"scoreMultiplier": 5}),
    "C43": (None, 6, {}),
    "C44": (None, 6, {"baseAddPerFrozenGold": 1}),
    "C46": ("uncommon", 5, {"scoreMultiplier": 2}),
    "C47": (None, 8, {}),
    "C50": (None, 4, {}),
    "C53": (None, 4, {}),  # new outcome table {0.5,1,1,1.5,2,2,3,4,5} needs two code constants; see --variant
}

HANDS = {"straight": {"baseMult100": 1000, "incrementPerLevel100": 300}}

ENCHANTS = {  # id: (weight, buyPremium)
    "none": (58, 0), "slotless": (6, 5), "base10": (14, 2), "mult2": (4, 6), "submit1": (9, 4), "roll1": (9, 3),
}

TOP = {
    "interestDivisor": 5,
    "maxInterestPrincipal": 25,
    "glassBreakPercent": 8,
    "glassContributionMultiplier": 3,
}

REWARDS = [5] * 24
RISK_BONUS = 3
RISK_FACTOR = 1.25

TARGETS = [
    300, 600, 1050, 1800, 2800, 4200,
    6200, 8800, 12000, 17000, 24000, 34000,
    48000, 66000, 92000, 126000, 170000, 230000,
    315000, 425000, 580000, 780000, 1060000, 1500000,
]

# Cards whose v0.8 change is a coefficient in code, not a config value. The simulator cannot apply those, so the
# `sim` variant takes them out of the shop instead of simulating them at their old strength.
SIM_REMOVED = {"C47", "C53"}
NON_SUBMIT_WEIGHTS = {"none": 100, "slotless": 20, "submit1": 25, "roll1": 25}

SHOP = {"upgradePricePerExistingLevel": 2, "selfStudyLimit": 2}

UPGRADE_PRICES = {"U03": 2, "U04": 1, "U05": 2, "U06": 3}


def build(variant: str, targets=None) -> dict:
    cfg = json.loads(SOURCE.read_text(encoding="utf-8"))
    cfg["shop"]["controllerWeights"] = dict(WEIGHTS)
    cfg["shop"]["firstControllerGuaranteeMaxBasePrice"] = 4
    cfg["shop"].update(SHOP)
    for c in cfg["controllers"]:
        change = CONTROLLERS.get(c["id"])
        if change:
            rarity, price, params = change
            if rarity:
                c["rarity"] = rarity
            if price:
                c["buyPrice"] = price
            for key, value in params.items():
                if key not in c["parameters"]:
                    raise KeyError(f"{c['id']} has no parameter {key}")
                c["parameters"][key] = value
        c["offerWeight"] = WEIGHTS[c["rarity"]]
        if variant == "sim" and c["id"] in SIM_REMOVED:
            c["offerWeight"] = 0
    for h in cfg["handDefinitions"]:
        h.update(HANDS.get(h["id"], {}))
    for e in cfg["enchantments"]:
        e["weight"], e["buyPremium"] = ENCHANTS[e["id"]]
    cfg["enchantRules"]["nonSubmitDrawWeights"] = dict(NON_SUBMIT_WEIGHTS)
    cfg.update(TOP)
    for u in cfg["diceUpgrades"]:
        if u["id"] in UPGRADE_PRICES:
            u["buyPrice"] = UPGRADE_PRICES[u["id"]]
    values = targets or TARGETS
    for i, s in enumerate(cfg["stages"]):
        s["normalTarget"] = str(values[i])
        s["normalReward"] = REWARDS[i]
        if s["riskEnabled"]:
            s["riskTarget"] = str(int(round(values[i] * RISK_FACTOR)))
            s["riskReward"] = REWARDS[i] + RISK_BONUS
    return cfg


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--variant", default="sim", choices=["sim", "full"])
    ap.add_argument("--out", default=str(ROOT / "tools" / "balance_sim" / "candidates" / "v08_candidate.json"))
    ap.add_argument("--targets", default=None, help="comma separated 24 targets to override the table")
    args = ap.parse_args()
    targets = [int(x) for x in args.targets.split(",")] if args.targets else None
    out = Path(args.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(build(args.variant, targets), ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(out)


if __name__ == "__main__":
    main()
