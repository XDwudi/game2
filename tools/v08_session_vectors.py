"""Independent reference for the first three controller offers of a fresh shop (id + enchant interleaving).

EditMode test V05_ShopInterleavesIdAndEnchantmentWithFrozenReferenceVectors pins four seeds. Its p0.6.0
expectations were produced outside the game code; the generator used then is not in the repository, so this
file re-implements the documented procedure (xorshift32, rejection sampling, weighted id draw, enchant draw)
and proves itself first: with the frozen p0.6.0 configuration it must reproduce the four p0.6.0 vectors.

    python tools/v08_session_vectors.py
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASELINE = ROOT / "tools" / "balance_sim" / "baseline" / "P0Config.p0.6.0.json"
CONFIG = ROOT / "DicePrototype" / "Assets" / "Game" / "Data" / "P0Config.json"
MASK = 0xFFFFFFFF
P060 = {
    1: "C26:none,C13:none,C27:none",
    0: "C48:submit1,C27:none,C11:none",
    20261030: "C50:slotless,C28:none,C19:slotless",
    4294967295: "C34:none,C01:slotless,C15:base10",
}


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


def first_offers(cfg, seed):
    rng = Rng(seed ^ cfg["rng"]["shopSeedXor"])
    rules = cfg["enchantRules"]
    enchants = {e["id"]: e for e in cfg["enchantments"]}
    special = rules["nonSubmitDrawWeights"]
    choices = sorted(cfg["controllers"], key=lambda c: c["id"])
    out = []
    for i in range(cfg["shop"]["initialControllerOffers"]):
        pool = choices
        if i == 0:
            cheap = [c for c in choices if c["buyPrice"] <= cfg["shop"]["firstControllerGuaranteeMaxBasePrice"]]
            pool = cheap or choices
        ticket = rng.index(sum(c["offerWeight"] for c in pool))
        picked = pool[0]
        for option in pool:
            ticket -= option["offerWeight"]
            if ticket < 0:
                picked = option
                break
        non_submit = picked["id"] in rules["nonSubmitControllerIds"]
        weight = (lambda e: special.get(e, 0)) if non_submit else (lambda e: enchants[e]["weight"])
        order = [e for e in rules["rawDrawOrder"] if weight(e) > 0]
        draw = rng.index(sum(weight(e) for e in order))
        enchant = None
        for e in order:
            draw -= weight(e)
            if draw < 0:
                enchant = e
                break
        out.append(picked["id"] + ":" + enchant)
        choices = [c for c in choices if c is not picked]
    return ",".join(out)


def main():
    base = json.loads(BASELINE.read_text(encoding="utf-8"))
    for seed, expected in P060.items():
        actual = first_offers(base, seed)
        if actual != expected:
            raise SystemExit(f"reference does not reproduce p0.6.0 vector for seed {seed}: {actual} != {expected}")
    print("reference reproduces the 4 p0.6.0 vectors")
    cfg = json.loads(CONFIG.read_text(encoding="utf-8"))
    for seed in P060:
        print(f'[TestCase({seed}u, "{first_offers(cfg, seed)}")]')


if __name__ == "__main__":
    main()
