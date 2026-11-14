"""Build a p0.9.0 stage-target candidate from the shipped P0Config.json.

Only stages 12-24 are touched: `normalTarget`, and `riskTarget` = normalTarget x 5/4 on the stages that offer the
high-pressure challenge. Stages 1-11 and everything else are copied unchanged. The output is a simulation input for
`BalanceSim run --config`; it is not loaded by the game.

    python tools/balance_sim/make_targets.py --name v09_t1 --targets 38000,58000,...   (13 numbers, stages 12..24)
"""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "DicePrototype" / "Assets" / "Game" / "Data" / "P0Config.json"
FIRST_STAGE = 12


def build(targets):
    cfg = json.loads(SOURCE.read_text(encoding="utf-8-sig"))
    stages = cfg["stages"]
    if len(targets) != len(stages) - FIRST_STAGE + 1:
        raise SystemExit(f"need {len(stages) - FIRST_STAGE + 1} targets for stages {FIRST_STAGE}..{len(stages)}, got {len(targets)}")
    for offset, value in enumerate(targets):
        stage = stages[FIRST_STAGE - 1 + offset]
        stage["normalTarget"] = str(value)
        if stage["riskEnabled"]:
            if value * 5 % 4:
                raise SystemExit(f"stage {FIRST_STAGE + offset}: {value} x 5/4 is not an integer")
            stage["riskTarget"] = str(value * 5 // 4)
    return cfg


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--name", required=True, help="file name without extension, written to tools/balance_sim/candidates/")
    ap.add_argument("--targets", required=True, help="13 comma separated targets for stages 12..24")
    args = ap.parse_args()
    targets = [int(x) for x in args.targets.split(",")]
    cfg = build(targets)
    out = ROOT / "tools" / "balance_sim" / "candidates" / (args.name + ".json")
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(cfg, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    all_targets = [int(s["normalTarget"]) for s in cfg["stages"]]
    steps = [all_targets[i] / all_targets[i - 1] for i in range(1, len(all_targets))]
    print(out)
    print("targets 12..24:", ",".join(str(t) for t in all_targets[FIRST_STAGE - 1:]))
    print("step factors 12..24:", " ".join(f"{x:.2f}" for x in steps[FIRST_STAGE - 2:]))


if __name__ == "__main__":
    main()
