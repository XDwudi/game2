"""Stage-target calibration helper for `BalanceSim run` output (rules p1.1.1: 24 stages, three-stage cycles).

p1.1.1: the fields the three modes read (summary.targets / stageKinds, stages[].clear / reach / power, and per
stage in logs[]: stage, kind, cleared, risk, riskOffered, target, targetBase, power, safe, powerRaw, safeRaw,
powerMod, targetMod) kept their names. `summary` also prints the p1.1.1 per-stage fields when the run has them:
power / target and one mean hand / target per four-stage block, the highest real hand / target, and who died where.

    python tools/balance_sim/calibrate.py summary <run.json> [<run2.json> ...] [--md]
        Headline numbers of each run: win rate, death share per four-stage block, clear rate per stage and per stage
        kind, pressure acceptance, median power/target at the last stage before the final-boss modifier.

    python tools/balance_sim/calibrate.py predict --fit <run.json>[,<run2.json>...] --base <run.json> --targets t1,...,t24 [--from K]
        Planning aid only (never evidence): estimates what a different set of targets would do, from runs that were
        already simulated. --targets lists the stages from --from (default 1) to the last one; earlier stages keep
        the targets of --base.
          1. A logistic model P(clear | ln(mean power / target), ln(safe power / target)) is fitted on every stage
             that any of the --fit runs reached. Power and target are the ones the stage was really played with
             (pressure x1.5 when accepted, the boss modifier in force unless lifted, TargetPercent included).
          2. Every seed of --base is replayed on paper. Its logged power at each stage is compared with the new
             target; stages it never reached are extrapolated with the median stage-to-stage power growth of the
             seeds that did reach them. Stage kinds are respected:
               pressure  the bot's own rule is applied to the new target: it accepts when safe power >= 1.3 x (1.5 x target);
               boss      the seed's logged ratio between "as entered" and "without modifier" (its boss modifier,
                         TargetPercent, a lifted modifier) is kept; unreached boss stages use the stage's median ratio.
          3. Reach, clear and death shares are the sums of those probabilities.
        It ignores that the bot shops differently when targets move, so every candidate is then simulated for real.

    python tools/balance_sim/calibrate.py solve --fit ... --base ... [--from K] [--win 0.25] [--boss-gap 0.03] [--min-clear 0.86]
                                               [--steps 1.2,2.6] [--round] [--last-power 2.5]
        Chooses targets one stage at a time so that the predicted clear rates follow a schedule built from the goals:
        deaths spread evenly over the four-stage blocks for the requested win rate, boss stages --boss-gap harder
        than the other stages of their block, no stage below --min-clear. Step factors stay inside --steps.
        --last-power caps the last target so that the median power/target there (before the final-boss modifier)
        is at least that value. --clear a[,b] instead gives a plain schedule (one value, or first,last interpolated).
"""
import argparse
import json
import math
from pathlib import Path

RISK_FACTOR = 1.5      # pressure target = normal target x 3/2 (p1.0.0 and p1.1.1)
BOT_RISK_MARGIN = 1.3  # BotOptions.riskFactor: the bot accepts when safe power >= margin x pressure target


def load(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def blocks(n_stages):
    return [(a, min(a + 3, n_stages)) for a in range(1, n_stages + 1, 4)]


def median(values):
    values = sorted(v for v in values if v is not None and not math.isnan(v))
    return values[len(values) // 2] if values else float("nan")


def stage_kinds(run):
    kinds = run["summary"].get("stageKinds")
    if kinds:
        return kinds
    n = len(run["summary"]["targets"])
    return [("normal", "pressure", "boss")[i % 3] for i in range(n)]


def headline(run):
    logs = run["logs"]
    targets = [int(t) for t in run["summary"]["targets"]]
    kinds = stage_kinds(run)
    n = len(targets)
    deaths = {}
    for log in logs:
        if not log["won"]:
            deaths[log["reached"]] = deaths.get(log["reached"], 0) + 1
    total_deaths = sum(deaths.values()) or 1
    share = [100.0 * sum(v for k, v in deaths.items() if a <= k <= b) / total_deaths for a, b in blocks(n)]
    share_runs = [s * total_deaths / len(logs) for s in share]
    by_stage = {s["stage"]: s for s in run["stages"]}
    clear = [by_stage[k]["clear"] if k in by_stage else float("nan") for k in range(1, n + 1)]
    reach = [by_stage[k]["reach"] if k in by_stage else 0.0 for k in range(1, n + 1)]
    stages = [s for log in logs for s in log["stages"]]
    by_kind = {}
    for kind in ("normal", "pressure", "boss"):
        at = [s for s in stages if s.get("kind") == kind]
        by_kind[kind] = 100.0 * sum(1 for s in at if s["cleared"]) / len(at) if at else float("nan")
    offered = [s for s in stages if s.get("riskOffered")]
    accepted = [s for s in offered if s["risk"]]
    last = [s for s in stages if s["stage"] == n]
    # p1.1.1 fields (absent in older runs): the per-hand estimates and the best real hand of each stage.
    has_hand = bool(stages) and "handMeanOverTarget" in stages[0]
    block_rows = []
    for a, b in blocks(n):
        at = [s for s in stages if a <= s["stage"] <= b and s["target"] > 0]
        block_rows.append({
            "block": f"{a}-{b}", "entered": len(at),
            "power": median([s["power"] / s["target"] for s in at]), "safe": median([s["safe"] / s["target"] for s in at]),
            "hand": median([s["handMeanOverTarget"] for s in at]) if has_hand else float("nan"),
            "p25": median([s["handP25OverTarget"] for s in at]) if has_hand else float("nan"),
            "best": median([s["bestHand"] / s["target"] for s in at if s["cleared"]]) if has_hand else float("nan"),
        })
    death_kinds = {}
    for log in logs:
        if not log["won"]:
            kind = kinds[max(1, log["reached"]) - 1]
            death_kinds[kind] = death_kinds.get(kind, 0) + 1
    return {
        "blocks": block_rows, "death_kinds": death_kinds, "seconds": run.get("v111", {}).get("secondsPerRun"),
        "errors": run.get("errors", 0), "rejected": (run.get("rejectedOptionalCommands", 0), run.get("rejectedShopCommands", 0), run.get("quoteMismatches", 0)),
        "name": Path(run.get("configPath", "?")).stem, "runs": len(logs), "wins": run["wins"], "win": 100.0 * run["wins"] / len(logs),
        "share": share, "share_runs": share_runs,
        "power_last": median([s["powerRaw"] / s["targetBase"] for s in last]) if last and "powerRaw" in last[0] else (by_stage[n]["power"] if n in by_stage else float("nan")),
        "power_last_mod": median([s["powerMod"] / s["targetMod"] for s in last if s.get("targetMod")]) if last else float("nan"),
        "power": [by_stage[k]["power"] if k in by_stage else float("nan") for k in range(1, n + 1)],
        "clear": clear, "reach": reach, "targets": targets, "kinds": kinds, "deaths": deaths, "by_kind": by_kind,
        "risk_accept": 100.0 * len(accepted) / len(offered) if offered else float("nan"),
        "risk_clear": 100.0 * sum(1 for s in accepted if s["cleared"]) / len(accepted) if accepted else float("nan"),
        "self_check": run.get("plannerSelfCheck", {}),
    }


def fmt(x, digits=0):
    return "-" if x is None or (isinstance(x, float) and math.isnan(x)) else f"{x:.{digits}f}"


def summary(paths, md):
    rows = [headline(load(p)) for p in paths]
    for r, p in zip(rows, paths):
        r["file"] = Path(p).name
    if md:
        print("| run | win % | deaths by block 1-4 / 5-8 / 9-12 / 13-16 / 17-20 / 21-24 (% of deaths) | clear % normal / pressure / boss | lowest stage clear % | pressure accepted % | stage-24 power/target before modifier (median) |")
        print("| --- | --- | --- | --- | --- | --- | --- |")
    for r in rows:
        valid = [(c, k + 1) for k, c in enumerate(r["clear"]) if not math.isnan(c)]
        low = min(valid) if valid else (float("nan"), 0)
        if md:
            print(f"| {r['file']} | {r['win']:.1f} | {' / '.join(f'{s:.0f}' for s in r['share'])} | {fmt(r['by_kind']['normal'], 1)} / {fmt(r['by_kind']['pressure'], 1)} / {fmt(r['by_kind']['boss'], 1)} | {low[0]:.0f} (stage {low[1]}) | {fmt(r['risk_accept'])} | {fmt(r['power_last'], 2)} |")
            continue
        n = len(r["targets"])
        print(f"\n== {r['file']}  ({r['name']})  runs={r['runs']} wins={r['wins']} ({r['win']:.1f}%)  planner self-check={r['self_check']}")
        print("targets: " + ",".join(str(t) for t in r["targets"]))
        print("step factors 2..%d: %s" % (n, " ".join(f"{r['targets'][i] / r['targets'][i - 1]:.2f}" for i in range(1, n))))
        print("death share by block, % of deaths: " + "  ".join(f"{a}-{b}: {s:.0f}%" for (a, b), s in zip(blocks(n), r["share"])))
        print("death share by block, % of runs:   " + "  ".join(f"{a}-{b}: {s:.0f}%" for (a, b), s in zip(blocks(n), r["share_runs"])))
        print("clear % by stage kind: " + "  ".join(f"{k}: {fmt(v, 1)}" for k, v in r["by_kind"].items()))
        print(f"pressure challenge: accepted {fmt(r['risk_accept'])}% of offers, cleared {fmt(r['risk_clear'])}% of the accepted")
        print(f"median power/target at the last stage: {fmt(r['power_last'], 2)} before the final-boss modifier, {fmt(r['power_last_mod'], 2)} with it")
        print("stage:            " + " ".join(f"{k:>4}" for k in range(1, n + 1)))
        print("kind:             " + " ".join(f"{r['kinds'][k][0].upper():>4}" for k in range(n)))
        print("clear %:          " + " ".join(f"{fmt(c):>4}" for c in r["clear"]))
        print("reach %:          " + " ".join(f"{fmt(c):>4}" for c in r["reach"]))
        print("power/target med: " + " ".join(f"{fmt(x, 1):>4}" for x in r["power"]))
        print(f"lowest stage clear rate: {low[0]:.0f}% at stage {low[1]};  stages below 85%: " + (" ".join(str(k) for c, k in valid if c < 85) or "none"))
        print(f"diagnostics: errors={r['errors']}  refused optional / shop commands={r['rejected'][0]} / {r['rejected'][1]}  quote mismatches={r['rejected'][2]}"
              + (f"  seconds per run={r['seconds']:.1f}" if r["seconds"] is not None else ""))
        print("deaths by stage kind: " + "  ".join(f"{k}: {v}" for k, v in sorted(r["death_kinds"].items())) + "   deaths by stage: " + " ".join(f"{k}:{v}" for k, v in sorted(r["deaths"].items())))
        print("per four-stage block, medians over the stages entered (power = mean hand x submissions, hand = one mean hand, p25 = one 25th-percentile hand, best = highest real hand of a cleared stage; all / target):")
        for b in r["blocks"]:
            print(f"  {b['block']:<6} entered {b['entered']:>5}  power {fmt(b['power'], 2)}  safe {fmt(b['safe'], 2)}  hand {fmt(b['hand'], 2)}  p25 {fmt(b['p25'], 2)}  best {fmt(b['best'], 2)}")


# ---------------------------------------------------------------- prediction
def fit_logistic(rows):
    """rows: (x1, x2, y). Newton iterations with a small ridge; returns (b0, b1, b2)."""
    import numpy as np
    X = np.array([[1.0, a, b] for a, b, _ in rows])
    y = np.array([float(c) for _, _, c in rows])
    w = np.zeros(3)
    for _ in range(80):
        p = 1.0 / (1.0 + np.exp(-np.clip(X @ w, -30, 30)))
        grad = X.T @ (y - p) - 1e-3 * w
        hess = (X * (p * (1 - p))[:, None]).T @ X + 1e-3 * np.eye(3)
        step = np.linalg.solve(hess, grad)
        w = w + step
        if float(np.abs(step).max()) < 1e-9:
            break
    return [float(v) for v in w]


def safe_of(s, key_safe="safe", key_power="power"):
    return max(s[key_safe], s[key_power] * 0.02, 1e-9)


def feature_rows(runs, first):
    rows = []
    for run in runs:
        for log in run["logs"]:
            for s in log["stages"]:
                if s["stage"] >= first and s["power"] > 0 and s["target"] > 0:
                    rows.append((math.log(s["power"] / s["target"]), math.log(safe_of(s) / s["target"]), bool(s["cleared"])))
    return rows


class Model:
    def __init__(self, fit_runs, base, first):
        self.first = first
        self.w = fit_logistic(feature_rows(fit_runs, 1))
        self.base = base
        self.kinds = stage_kinds(base)
        self.n = len(base["summary"]["targets"])
        self.fixed = [int(t) for t in base["summary"]["targets"]]
        logs = base["logs"]
        # Median growth of raw mean / safe power from stage k-1 to k among seeds that have both, and for boss stages
        # the median factors between "as entered" and "without modifier" (in units of power / target).
        self.growth, self.boss_factor = {}, {}
        for k in range(2, self.n + 1):
            gm, gs = [], []
            for log in logs:
                st = {s["stage"]: s for s in log["stages"]}
                if k in st and k - 1 in st and st[k - 1]["powerRaw"] > 0:
                    gm.append(st[k]["powerRaw"] / st[k - 1]["powerRaw"])
                    gs.append(safe_of(st[k], "safeRaw", "powerRaw") / safe_of(st[k - 1], "safeRaw", "powerRaw"))
            self.growth[k] = (median(gm) if gm else 1.3, median(gs) if gs else 1.3)
        for k in range(1, self.n + 1):
            if self.kinds[k - 1] != "boss":
                continue
            fm, fs = [], []
            for log in logs:
                for s in log["stages"]:
                    if s["stage"] == k and s["powerRaw"] > 0:
                        fm.append((s["power"] / s["target"]) / (s["powerRaw"] / s["targetBase"]))
                        fs.append((safe_of(s) / s["target"]) / (safe_of(s, "safeRaw", "powerRaw") / s["targetBase"]))
            self.boss_factor[k] = (median(fm) if fm else 0.7, median(fs) if fs else 0.7)
        # Per seed and stage: raw mean power, raw safe power, boss factors (mean, safe).
        self.seeds = []
        for log in logs:
            st = {s["stage"]: s for s in log["stages"]}
            mean, safe, factor = {}, {}, {}
            for k in range(1, self.n + 1):
                if k in st and st[k]["powerRaw"] > 0:
                    s = st[k]
                    mean[k], safe[k] = s["powerRaw"], safe_of(s, "safeRaw", "powerRaw")
                    if self.kinds[k - 1] == "boss":
                        factor[k] = ((s["power"] / s["target"]) / (mean[k] / s["targetBase"]), (safe_of(s) / s["target"]) / (safe[k] / s["targetBase"]))
                else:
                    mean[k], safe[k] = mean[k - 1] * self.growth[k][0], safe[k - 1] * self.growth[k][1]
                    if self.kinds[k - 1] == "boss":
                        factor[k] = self.boss_factor[k]
            self.seeds.append((mean, safe, factor))

    def p_clear(self, mean_over_target, safe_over_target):
        z = self.w[0] + self.w[1] * math.log(mean_over_target) + self.w[2] * math.log(safe_over_target)
        return 1.0 / (1.0 + math.exp(-max(-30.0, min(30.0, z))))

    def ratios(self, seed, k, target):
        """(mean / target, safe / target, pressure accepted) for one seed at stage k under a normal target."""
        mean, safe, factor = seed
        kind = self.kinds[k - 1]
        if kind == "pressure":
            risk = safe[k] >= BOT_RISK_MARGIN * RISK_FACTOR * target
            t = target * RISK_FACTOR if risk else target
            return mean[k] / t, safe[k] / t, risk
        if kind == "boss":
            return mean[k] / target * factor[k][0], safe[k] / target * factor[k][1], False
        return mean[k] / target, safe[k] / target, False

    def walk(self, targets):
        """targets: dict stage -> normal target for every stage. Returns per-stage (reach weight, clear rate,
        median raw power / target of reachers, pressure acceptance) and the win weight."""
        weight = [1.0] * len(self.seeds)
        out = {}
        for k in range(1, self.n + 1):
            reach = sum(weight)
            nxt, accepted = [], 0.0
            for w, seed in zip(weight, self.seeds):
                m, s, risk = self.ratios(seed, k, targets[k])
                nxt.append(w * self.p_clear(m, s))
                accepted += w if risk else 0.0
            order = sorted(range(len(self.seeds)), key=lambda i: self.seeds[i][0][k])
            acc, med = 0.0, float("nan")
            for i in order:
                acc += weight[i]
                if acc >= reach / 2:
                    med = self.seeds[i][0][k] / targets[k]
                    break
            out[k] = (reach, sum(nxt) / reach if reach > 0 else float("nan"), med, accepted / reach if reach > 0 else 0.0)
            weight = nxt
        return out, sum(weight)

    def full(self, partial):
        targets = {k: self.fixed[k - 1] for k in range(1, self.n + 1)}
        targets.update(partial)
        return targets

    def report(self, partial):
        targets = self.full(partial)
        total = len(self.base["logs"])
        out, wins = self.walk(targets)
        deaths = {k: out[k][0] * (1 - out[k][1]) for k in range(1, self.n + 1)}
        all_deaths = sum(deaths.values()) or 1
        print(f"logistic fit on all stages: z = {self.w[0]:.3f} + {self.w[1]:.3f} ln(mean/target) + {self.w[2]:.3f} ln(safe/target)")
        print("stage kind      target      step  reach%  clear%  power/target(median of reachers, no modifier)  pressure accepted%")
        prev = None
        for k in range(1, self.n + 1):
            reach, clear, med, acc = out[k]
            step = f"x{targets[k] / prev:.2f}" if prev else "     "
            kind = self.kinds[k - 1]
            print(f"{k:>5} {kind:<8} {int(targets[k]):>11,}  {step}  {100 * reach / total:5.1f}  {100 * clear:5.1f}  {med:6.2f}" + (f"  {100 * acc:5.0f}" if kind == "pressure" else ""))
            prev = targets[k]
        print(f"predicted win rate: {100 * wins / total:.1f}%")
        print("predicted death share by block (% of deaths): " + "  ".join(
            f"{a}-{b}: {100 * sum(v for k, v in deaths.items() if a <= k <= b) / all_deaths:.0f}%" for a, b in blocks(self.n)))
        for kind in ("normal", "pressure", "boss"):
            ks = [k for k in range(1, self.n + 1) if self.kinds[k - 1] == kind]
            reach = sum(out[k][0] for k in ks)
            print(f"predicted clear rate, {kind} stages: {100 * sum(out[k][0] * out[k][1] for k in ks) / reach:.1f}%")
        print("targets: " + ",".join(str(int(targets[k])) for k in range(1, self.n + 1)))


def nice(value):
    """Round to two significant digits (three below 1000) and keep the result even."""
    digits = 3 if value < 1000 else 2
    exp = 10 ** (int(math.floor(math.log10(value))) - digits + 1)
    rounded = int(round(value / exp) * exp)
    return rounded + (rounded % 2)


def schedule(model, win, gap, floor):
    """Clear rate wanted at each stage: every block loses the same share of the starting runs, and a boss stage
    is `gap` harder than the other stages of its block (never below `floor`)."""
    want = {}
    n_blocks = len(blocks(model.n))
    for b, (a, z) in enumerate(blocks(model.n)):
        before = 1 - b * (1 - win) / n_blocks
        after = 1 - (b + 1) * (1 - win) / n_blocks
        survive = after / before
        stages = list(range(a, z + 1))
        bosses = [k for k in stages if model.kinds[k - 1] == "boss"]
        lo, hi = floor, 1.0
        for _ in range(60):
            c = (lo + hi) / 2
            product = 1.0
            for k in stages:
                product *= max(floor, c - gap) if k in bosses else c
            if product > survive:
                hi = c
            else:
                lo = c
        for k in stages:
            want[k] = max(floor, lo - gap) if k in bosses else lo
    return want


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("mode", choices=["summary", "predict", "solve"])
    ap.add_argument("files", nargs="*")
    ap.add_argument("--md", action="store_true")
    ap.add_argument("--fit")
    ap.add_argument("--base")
    ap.add_argument("--targets")
    ap.add_argument("--from", dest="first", type=int, default=1)
    ap.add_argument("--clear")
    ap.add_argument("--win", type=float, default=0.25)
    ap.add_argument("--boss-gap", type=float, default=0.03)
    ap.add_argument("--min-clear", type=float, default=0.86)
    ap.add_argument("--steps", default="1.2,2.6")
    ap.add_argument("--last-power", type=float, default=0.0)
    ap.add_argument("--round", action="store_true")
    args = ap.parse_args()
    if args.mode == "summary":
        summary(args.files, args.md)
        return
    model = Model([load(p) for p in args.fit.split(",")], load(args.base), args.first)
    if args.mode == "predict":
        values = [int(x) for x in args.targets.replace("_", "").split(",")]
        if len(values) != model.n - args.first + 1:
            raise SystemExit(f"need {model.n - args.first + 1} targets for stages {args.first}..{model.n}")
        model.report({args.first + i: v for i, v in enumerate(values)})
        return
    # solve: stage by stage bisection on the target so that the predicted clear rate equals the schedule
    if args.clear:
        ends = [float(x) for x in args.clear.split(",")]
        want = {k: ends[0] if len(ends) == 1 else ends[0] + (ends[1] - ends[0]) * (k - args.first) / max(1, model.n - args.first) for k in range(args.first, model.n + 1)}
    else:
        want = schedule(model, args.win, args.boss_gap, args.min_clear)
    step_lo, step_hi = [float(x) for x in args.steps.split(",")]
    targets = {k: model.fixed[k - 1] for k in range(1, args.first)}
    prev = model.fixed[args.first - 2] if args.first > 1 else None
    for k in range(args.first, model.n + 1):
        lo, hi = 10.0, 1e13
        for _ in range(70):
            mid = math.sqrt(lo * hi)
            trial = dict(targets)
            for j in range(k, model.n + 1):
                trial[j] = mid
            out, _ = model.walk(trial)
            if out[k][1] > want[k]:
                lo = mid
            else:
                hi = mid
        value = lo if prev is None else min(step_hi * prev, max(step_lo * prev, lo))
        if k == model.n and args.last_power > 0:
            trial = dict(targets)
            trial[k] = value
            out, _ = model.walk(trial)
            if out[k][2] < args.last_power:      # median power/target would be too low: lower the last target
                value = max(step_lo * prev, value * out[k][2] / args.last_power)
        targets[k] = nice(value) if args.round else int(value) + int(value) % 2
        prev = targets[k]
    print("wanted clear % by stage: " + " ".join(f"{100 * want[k]:.0f}" for k in range(args.first, model.n + 1)))
    model.report(targets)


if __name__ == "__main__":
    main()
