"""Independent Appendix D shop-ticket oracle, never imports the C# implementation."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
cfg = json.loads((ROOT / "DicePrototype/Assets/Game/Data/P0Config.json").read_text(encoding="utf-8"))

class Rng:
    def __init__(self, state): self.state = state or 0x9E3779B9
    def index(self, n):
        limit = (0xFFFFFFFF // n) * n
        while True:
            x = self.state
            x ^= (x << 13) & 0xFFFFFFFF
            x ^= x >> 17
            x ^= (x << 5) & 0xFFFFFFFF
            self.state = x & 0xFFFFFFFF
            u = self.state - 1
            if u < limit: return u % n

def pick(rng, choices, weight):
    ticket = rng.index(sum(weight(x) for x in choices))
    for choice in choices:
        ticket -= weight(choice)
        if ticket < 0: return choice
    raise AssertionError("Uncovered ticket")

def controllers(seed):
    rng = Rng(seed ^ cfg["rng"]["shopSeedXor"])
    candidates = sorted(cfg["controllers"], key=lambda c: c["id"])
    result = []
    for i in range(3):
        pool = [c for c in candidates if c["buyPrice"] <= 3] if i == 0 else candidates
        c = pick(rng, pool or candidates, lambda x: x["offerWeight"])
        weights = cfg["enchantRules"]["nonSubmitDrawWeights"] if c["id"] in cfg["enchantRules"]["nonSubmitControllerIds"] else {e["id"]: e["weight"] for e in cfg["enchantments"]}
        enchant = pick(rng, [e for e in cfg["enchantRules"]["rawDrawOrder"] if weights.get(e, 0)], lambda e: weights[e])
        result.append(c["id"] + ":" + enchant)
        candidates.remove(c)
    return {"seed": seed, "controllers": ",".join(result), "rngAfterControllers": rng.state}

if __name__ == "__main__":
    result = [controllers(s) for s in [1, 0, 20261030, 4294967295]]
    out = ROOT / "evidence/v06/session/independent-shop-vectors.json"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result, indent=2))
