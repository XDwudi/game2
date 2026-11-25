"""Write the p0.8.0 game configuration.

Input:  tools/balance_sim/baseline/P0Config.p0.6.0.json (frozen copy of the shipped p0.6.0 config)
Tables: tools/balance_sim/make_candidate.py (the approved v0.8 proposal, docs/specs/v08/BALANCE_PLAN.md)
Output: tools/balance_sim/baseline/P0Config.p0.8.0.json (frozen copy; it was the live game config while p0.8.0 was current)

User decision 2026-11-14: every proposed change is adopted except the slotless enchant frequency, so all
enchant appearance weights (including the dedicated pool of the six non-scoring controllers) stay at p0.6.0.

    python tools/v08_config.py            # write the game config
    python tools/v08_config.py --check    # fail if the frozen p0.8.0 config differs from what this script produces
"""
import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools" / "balance_sim"))
import make_candidate as proposal  # noqa: E402

# p1.1.1 (W4b): this generator is frozen. Its output is the frozen p0.8.0 copy, not the live game config
# (DicePrototype/Assets/Game/Data/P0Config.json, which tools/v111_config.py produces).
TARGET = ROOT / "tools" / "balance_sim" / "baseline" / "P0Config.p0.8.0.json"

# Coefficients that needed code support (docs/specs/v08/RULES_DELTA.md).
C53_UNITS2 = [2, 2, 3, 4, 4, 6, 8, 10, 1]  # x1, x1, x1.5, x2, x2, x3, x4, x5, x0.5 ; index = raw ticket, x0.5 stays last

DESCRIPTIONS = {
    "C01": "每次有效提交计分后获得1金币。",
    "C02": "非空提交没有有效红面时，整手得分×3。",
    "C09": "提交容量−1；每次有效提交整手得分×3。",
    "C13": "每颗有效奇数的提交骰，点数贡献+4，不改牌型。",
    "C18": "非空提交中有效红面数等于有效蓝面数时，整手得分×4。",
    "C19": "某提交骰永久六面原值之和≤13时，该骰点数贡献+8。",
    "C20": "占满K栏且第K栏有效点数为6时，该骰点数贡献+15，参与逐骰强化及后续全部倍率。",
    "C28": "至少两个不同有效点数各出现2次时，整手得分×3；四个相同不能拆两对。",
    "C30": "提交中任一有效点数至少3颗时，整手得分×2。",
    "C31": "提交中任一有效点数至少4颗时，整手得分×3；与三条开泰可叠乘。",
    "C32": "非空提交只有一种有效点数时，整手得分×2；单骰也满足。",
    "C33": "至少提交2颗，第一颗有效点数≥第二颗有效点数+3时，整手得分×4。",
    "C40": "至少提交3颗，前三颗有效点数形成公差大于0的等差数列时，整手得分×4。",
    "C42": "每次有效提交整手×5；计分与后置效果后，该效果来源实体独立15%概率永久损坏并消失。",
    "C44": "每1金币令整手基础分+1，读取本次提交冻结时的钱包；本次提交新增金币不回算本手。",
    "C46": "每次有效提交整手得分×2。",
    "C47": "整手得分乘以提交快照中实际持有控制器实例数量的一半（4件×2，8件×4），最低×1；包括自身及免槽卡，不把模仿技能另算一件。",
    "C51": "整手得分×n；真实实例n初始1.5，每家商店第一次真实卖出一件控制器后，仍持有的真实C51各增加0.5，同一家商店之后的卖出不再成长；自毁不是卖出。",
    "C52": "基础购买价固定4，初始卖价为买入价的一半；每次成功关卡Finalize时卖价+3，仅一次；不是每手加3。",
    "C53": "提交事务中从0.5、1、1、1.5、2、2、3、4、5等概率抽取整手倍率；每个真实C53抽一次，其模仿者复用同一结果。",
}
UPGRADE_DESCRIPTIONS = {
    "U05": "整骰玻璃，提交时该骰贡献×3；每次实际投掷先独立判定8%永久损坏。",
}


def insert_after(mapping: dict, anchor: str, key: str, value) -> dict:
    out = {}
    for k, v in mapping.items():
        out[k] = v
        if k == anchor:
            out[key] = value
    if key not in out:
        raise KeyError(anchor)
    return out


def build() -> dict:
    base = json.loads(proposal.SOURCE.read_text(encoding="utf-8"))
    cfg = proposal.build("full")
    cfg["configVersion"] = "p0.8.0"
    # Enchant appearance weights stay at p0.6.0 (user decision); premiums follow the proposal.
    for new, old in zip(cfg["enchantments"], base["enchantments"]):
        assert new["id"] == old["id"]
        new["weight"] = old["weight"]
    cfg["enchantRules"]["nonSubmitDrawWeights"] = base["enchantRules"]["nonSubmitDrawWeights"]
    cfg = insert_after(cfg, "baseControllerSellPrice", "controllerSellDivisor", 2)
    for c in cfg["controllers"]:
        p = c["parameters"]
        if c["id"] == "C47":
            p["perControllerUnits2"] = 1
        elif c["id"] == "C51":
            c["parameters"] = insert_after(p, "addUnits2PerRealControllerSale", "maxGrowthPerShop", 1)
        elif c["id"] == "C53":
            p["factorUnits2"] = list(C53_UNITS2)
        if c["id"] in DESCRIPTIONS:
            c["description"] = DESCRIPTIONS[c["id"]]
    for u in cfg["diceUpgrades"]:
        if u["id"] in UPGRADE_DESCRIPTIONS:
            u["description"] = UPGRADE_DESCRIPTIONS[u["id"]]
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
