"""Build a stage-target candidate from the shipped P0Config.json (rules p1.1.1).

All 24 stages can be set. Only `normalTarget` changes, plus `riskTarget` = normalTarget x 3/2 on the pressure stages
(RULES_DELTA section 11). Everything else is copied unchanged. The output is a simulation input for
`BalanceSim run --config`; it is not loaded by the game.

    python tools/balance_sim/make_targets.py --name v111_t1 --targets 160,320,500,...       (24 numbers: stages 1..24)
    python tools/balance_sim/make_targets.py --name v111_t1 --from 4 --targets 740,...       (21 numbers: stages 4..24)
    python tools/balance_sim/make_targets.py --name v111_t1 --from 4 --targets ... --keep-anchors 160,320,500

Every target must be even (section 11 and the game's own validation), which also keeps the pressure target (x 3/2)
and the TargetPercent boss modifiers (x 150%, x 200%) whole; the simulator's relaxed validation checks the same.
The source must be a p1.1.1 configuration: an older file is refused rather than silently converted.
--keep-anchors fails when the first stages of the result differ from the listed values (the three fixed opening
targets of p1.1.1 are 160,320,500).
"""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "DicePrototype" / "Assets" / "Game" / "Data" / "P0Config.json"
RISK_NUM, RISK_DEN = 3, 2


EXPECTED_VERSION = "p1.1.1"


def build(targets, first_stage, anchors=()):
    cfg = json.loads(SOURCE.read_text(encoding="utf-8-sig"))
    if cfg.get("configVersion") != EXPECTED_VERSION:
        raise SystemExit(f"{SOURCE} is {cfg.get('configVersion')}, this script writes {EXPECTED_VERSION} candidates")
    stages = cfg["stages"]
    for number, stage in enumerate(stages, 1):
        # p1.1.1: only the second stage of a cycle is a pressure stage, and only it carries a riskTarget.
        if bool(stage["riskEnabled"]) != (stage.get("stageKind") == "pressure"):
            raise SystemExit(f"stage {number}: riskEnabled does not match stageKind {stage.get('stageKind')}")
    if first_stage < 1 or len(targets) != len(stages) - first_stage + 1:
        raise SystemExit(f"need {len(stages) - first_stage + 1} targets for stages {first_stage}..{len(stages)}, got {len(targets)}")
    percents = [m["amount"] for m in cfg.get("bossModifiers", []) if "TargetPercent" in m["effects"]]
    for offset, value in enumerate(targets):
        number = first_stage + offset
        stage = stages[number - 1]
        if value <= 0:
            raise SystemExit(f"stage {number}: target must be positive")
        if value % 2:
            raise SystemExit(f"stage {number}: {value} is odd; p1.1.1 targets must be even")
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
    for number, stage in enumerate(stages, 1):
        # The untouched stages must satisfy the same rules (they come from the shipped file).
        value = int(stage["normalTarget"])
        if value % 2 or (stage["riskEnabled"] and int(stage["riskTarget"]) * RISK_DEN != value * RISK_NUM):
            raise SystemExit(f"stage {number}: target {value} / pressure target {stage.get('riskTarget')} break the p1.1.1 target rules")
    for number, want in enumerate(anchors, 1):
        if all_targets[number - 1] != want:
            raise SystemExit(f"stage {number}: target {all_targets[number - 1]} differs from the anchor {want}")
    return cfg


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--name", required=True, help="file name without extension, written to tools/balance_sim/candidates/")
    ap.add_argument("--targets", required=True, help="comma separated targets, one per stage from --from to the last stage")
    ap.add_argument("--from", dest="first", type=int, default=1, help="first stage the list applies to (default 1 = all stages)")
    ap.add_argument("--keep-anchors", default="", help="comma separated targets the first stages must still have, e.g. 160,320,500")
    args = ap.parse_args()
    targets = [int(x) for x in args.targets.replace("_", "").split(",")]
    anchors = [int(x) for x in args.keep_anchors.replace("_", "").split(",")] if args.keep_anchors else []
    cfg = build(targets, args.first, anchors)
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
