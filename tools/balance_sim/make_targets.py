"""Build a p1.0.0 stage-target candidate from the shipped P0Config.json.

All 24 stages can be set. Only `normalTarget` changes, plus `riskTarget` = normalTarget x 3/2 on the pressure stages
(the p1.0.0 rule; p0.9.0 used x 5/4). Everything else is copied unchanged. The output is a simulation input for
`BalanceSim run --config`; it is not loaded by the game.

    python tools/balance_sim/make_targets.py --name v10_t1 --targets 300,600,...            (24 numbers: stages 1..24)
    python tools/balance_sim/make_targets.py --name v10_t1 --from 12 --targets 46000,...     (13 numbers: stages 12..24)

Every target must be even, so that the pressure target (x 3/2) and the TargetPercent boss modifiers (x 150%, x 200%)
stay whole numbers; the simulator's relaxed validation checks the same thing.
"""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "DicePrototype" / "Assets" / "Game" / "Data" / "P0Config.json"
RISK_NUM, RISK_DEN = 3, 2


def build(targets, first_stage):
    cfg = json.loads(SOURCE.read_text(encoding="utf-8-sig"))
    stages = cfg["stages"]
    if first_stage < 1 or len(targets) != len(stages) - first_stage + 1:
        raise SystemExit(f"need {len(stages) - first_stage + 1} targets for stages {first_stage}..{len(stages)}, got {len(targets)}")
    percents = [m["amount"] for m in cfg.get("bossModifiers", []) if "TargetPercent" in m["effects"]]
    for offset, value in enumerate(targets):
        number = first_stage + offset
        stage = stages[number - 1]
        if value <= 0:
            raise SystemExit(f"stage {number}: target must be positive")
        if any(value * p % 100 for p in percents):
            raise SystemExit(f"stage {number}: {value} does not stay whole under the TargetPercent modifiers {percents}")
        stage["normalTarget"] = str(value)
        if stage["riskEnabled"]:
            if value * RISK_NUM % RISK_DEN:
                raise SystemExit(f"stage {number}: {value} x {RISK_NUM}/{RISK_DEN} is not an integer")
            stage["riskTarget"] = str(value * RISK_NUM // RISK_DEN)
    all_targets = [int(s["normalTarget"]) for s in stages]
    for i in range(1, len(all_targets)):
        if all_targets[i] <= all_targets[i - 1]:
            raise SystemExit(f"stage {i + 1}: target {all_targets[i]} is not above stage {i} ({all_targets[i - 1]})")
    return cfg


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--name", required=True, help="file name without extension, written to tools/balance_sim/candidates/")
    ap.add_argument("--targets", required=True, help="comma separated targets, one per stage from --from to the last stage")
    ap.add_argument("--from", dest="first", type=int, default=1, help="first stage the list applies to (default 1 = all stages)")
    args = ap.parse_args()
    targets = [int(x) for x in args.targets.replace("_", "").split(",")]
    cfg = build(targets, args.first)
    out = ROOT / "tools" / "balance_sim" / "candidates" / (args.name + ".json")
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(cfg, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    all_targets = [int(s["normalTarget"]) for s in cfg["stages"]]
    kinds = [s.get("stageKind", "?") for s in cfg["stages"]]
    steps = [all_targets[i] / all_targets[i - 1] for i in range(1, len(all_targets))]
    print(out)
    print("targets 1..%d: %s" % (len(all_targets), ",".join(str(t) for t in all_targets)))
    print("pressure targets (x1.5): " + " ".join(f"{i + 1}:{s['riskTarget']}" for i, s in enumerate(cfg["stages"]) if s["riskEnabled"]))
    print("step factors 2..%d: %s" % (len(all_targets), " ".join(f"{x:.2f}" for x in steps)))
    print("stage kinds: " + " ".join(k[0].upper() for k in kinds))


if __name__ == "__main__":
    main()
