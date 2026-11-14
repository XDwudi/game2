"""Stage-target calibration helper for `BalanceSim run` output (p0.9.0, stages 12-24).

    python tools/balance_sim/calibrate.py summary <run.json> [<run2.json> ...] [--md]
        Headline numbers of each run: win rate, death share per four-stage block, median power/target at the last
        stage, clear rate of stages 13-24, step factors of the targets.

    python tools/balance_sim/calibrate.py predict --fit <run.json>[,<run2.json>...] --base <run.json> --targets t12,...,t24
        Planning aid only (never evidence): estimates what a different set of targets for stages 12-24 would do,
        from runs that were already simulated.
          1. A logistic model P(clear | ln(mean power / target), ln(safe power / target)) is fitted on every
             stage >= 12 that any of the --fit runs reached.
          2. Every seed of --base that reached stage 12 is replayed on paper: its logged power at each stage is
             compared with the new target; stages it never reached are extrapolated with the median stage-to-stage
             power growth of the seeds that did reach them.
          3. Reach, clear and death shares are the sums of those probabilities.
        It ignores that the bot shops differently when targets move, so every candidate is then simulated for real.

    python tools/balance_sim/calibrate.py solve --fit ... --base ... --clear 0.95,0.90 [--round]
        Chooses targets for stages 12-24 one stage at a time so that the predicted clear rate follows --clear
        (one number, or "first,last" interpolated linearly from stage 12 to the last stage), keeping every step
        factor inside [1.3, 2.0].
"""
import argparse
import json
import math
from pathlib import Path

FIRST = 12


def load(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def blocks(n_stages):
    return [(a, min(a + 3, n_stages)) for a in range(1, n_stages + 1, 4)]


def median(values):
    values = sorted(values)
    return values[len(values) // 2] if values else float("nan")


def headline(run):
    logs = run["logs"]
    targets = [int(t) for t in run["summary"]["targets"]]
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
    return {
        "name": Path(run.get("configPath", "?")).stem, "runs": len(logs), "wins": run["wins"], "win": 100.0 * run["wins"] / len(logs),
        "share": share, "share_runs": share_runs, "power_last": by_stage[n]["power"] if n in by_stage else float("nan"),
        "power": [by_stage[k]["power"] if k in by_stage else float("nan") for k in range(1, n + 1)],
        "clear": clear, "reach": reach, "targets": targets, "deaths": deaths,
    }


def summary(paths, md):
    rows = [headline(load(p)) for p in paths]
    for r, p in zip(rows, paths):
        r["file"] = Path(p).name
    if md:
        print("| run | win % | deaths by block 1-4 / 5-8 / 9-12 / 13-16 / 17-20 / 21-24 (% of deaths) | stage-24 power/target (median) | clear % stages 13-24 | in 85-95 |")
        print("| --- | --- | --- | --- | --- | --- |")
    for r in rows:
        late = r["clear"][12:]
        inside = sum(1 for c in late if 85 <= c <= 95)
        if md:
            print(f"| {r['file']} | {r['win']:.1f} | {' / '.join(f'{s:.0f}' for s in r['share'])} | {r['power_last']:.2f} | {' '.join(f'{c:.0f}' for c in late)} | {inside}/12 |")
            continue
        print(f"\n== {r['file']}  ({r['name']})  runs={r['runs']} wins={r['wins']} ({r['win']:.1f}%)")
        print("targets 12-24: " + ",".join(str(t) for t in r["targets"][FIRST - 1:]))
        print("step factors 12-24: " + " ".join(f"{r['targets'][i] / r['targets'][i - 1]:.2f}" for i in range(FIRST - 1, len(r["targets"]))))
        print("death share by block, % of deaths: " + "  ".join(f"{a}-{b}: {s:.0f}%" for (a, b), s in zip(blocks(len(r["targets"])), r["share"])))
        print("death share by block, % of runs:   " + "  ".join(f"{a}-{b}: {s:.0f}%" for (a, b), s in zip(blocks(len(r["targets"])), r["share_runs"])))
        print(f"median power/target at the last stage: {r['power_last']:.2f}")
        print("median power/target stages 12-24: " + " ".join(f"{x:.1f}" for x in r["power"][FIRST - 1:]))
        print("clear % stages 12-24: " + " ".join(f"{c:.0f}" for c in r["clear"][FIRST - 1:]) + f"   (stages 13-24 inside 85-95: {inside}/12)")
        print("reach % stages 12-24: " + " ".join(f"{c:.0f}" for c in r["reach"][FIRST - 1:]))


# ---------------------------------------------------------------- prediction
def fit_logistic(rows):
    """rows: (x1, x2, y). Newton iterations with a small ridge; returns (b0, b1, b2)."""
    import numpy as np
    X = np.array([[1.0, a, b] for a, b, _ in rows])
    y = np.array([float(c) for _, _, c in rows])
    w = np.zeros(3)
    for _ in range(60):
        p = 1.0 / (1.0 + np.exp(-np.clip(X @ w, -30, 30)))
        grad = X.T @ (y - p) - 1e-3 * w
        hess = (X * (p * (1 - p))[:, None]).T @ X + 1e-3 * np.eye(3)
        step = np.linalg.solve(hess, grad)
        w = w + step
        if float(np.abs(step).max()) < 1e-9:
            break
    return [float(v) for v in w]


def feature_rows(runs):
    rows = []
    for run in runs:
        for log in run["logs"]:
            for s in log["stages"]:
                if s["stage"] >= FIRST and s["power"] > 0:
                    rows.append((math.log(s["power"] / s["target"]), math.log(max(s["safe"], s["power"] * 0.02) / s["target"]), bool(s["cleared"])))
    return rows


class Model:
    def __init__(self, fit_runs, base):
        self.w = fit_logistic(feature_rows(fit_runs))
        self.base = base
        self.n = len(base["summary"]["targets"])
        self.fixed = [int(t) for t in base["summary"]["targets"]]
        # median growth of mean / safe power from stage k-1 to k among seeds that have both
        self.growth = {}
        for k in range(FIRST + 1, self.n + 1):
            gm, gs = [], []
            for log in base["logs"]:
                st = {s["stage"]: s for s in log["stages"]}
                if k in st and k - 1 in st and st[k - 1]["power"] > 0:
                    gm.append(st[k]["power"] / st[k - 1]["power"])
                    gs.append(max(st[k]["safe"], 1e-9) / max(st[k - 1]["safe"], 1e-9))
            self.growth[k] = (median(gm) if gm else 1.3, median(gs) if gs else 1.3)
        self.seeds = []
        for log in base["logs"]:
            st = {s["stage"]: s for s in log["stages"]}
            if FIRST not in st:
                continue
            mean, safe = {}, {}
            for k in range(FIRST, self.n + 1):
                if k in st:
                    mean[k], safe[k] = st[k]["power"], max(st[k]["safe"], st[k]["power"] * 0.02)
                else:
                    mean[k], safe[k] = mean[k - 1] * self.growth[k][0], safe[k - 1] * self.growth[k][1]
            self.seeds.append((mean, safe))

    def p_clear(self, mean, safe, target):
        z = self.w[0] + self.w[1] * math.log(mean / target) + self.w[2] * math.log(safe / target)
        return 1.0 / (1.0 + math.exp(-max(-30.0, min(30.0, z))))

    def walk(self, targets):
        """targets: dict stage -> target for stages FIRST..n. Returns per-stage (reach weight, clear rate) and win weight."""
        weight = [1.0] * len(self.seeds)
        out = {}
        for k in range(FIRST, self.n + 1):
            reach = sum(weight)
            nxt = [w * self.p_clear(m[k], s[k], targets[k]) for w, (m, s) in zip(weight, self.seeds)]
            order = sorted(range(len(self.seeds)), key=lambda i: self.seeds[i][0][k])
            acc, med = 0.0, float("nan")
            for i in order:
                acc += weight[i]
                if acc >= reach / 2:
                    med = self.seeds[i][0][k] / targets[k]
                    break
            out[k] = (reach, sum(nxt) / reach if reach > 0 else float("nan"), med)
            weight = nxt
        return out, sum(weight)

    def report(self, targets):
        total = len(self.base["logs"])
        out, wins = self.walk(targets)
        early = {}
        for log in self.base["logs"]:
            if not log["won"] and log["reached"] < FIRST:
                early[log["reached"]] = early.get(log["reached"], 0) + 1
        deaths = dict(early)
        for k in range(FIRST, self.n + 1):
            deaths[k] = out[k][0] * (1 - out[k][1])
        all_deaths = sum(deaths.values()) or 1
        print(f"logistic fit on stages >= {FIRST}: z = {self.w[0]:.3f} + {self.w[1]:.3f} ln(mean/target) + {self.w[2]:.3f} ln(safe/target)")
        print("stage  target      step  reach%  clear%  power/target(median of reachers)")
        prev = self.fixed[FIRST - 2]
        for k in range(FIRST, self.n + 1):
            reach, clear, med = out[k]
            print(f"{k:>5}  {targets[k]:>10,}  x{targets[k] / prev:.2f}  {100 * reach / total:5.1f}  {100 * clear:5.1f}  {med:6.2f}")
            prev = targets[k]
        print(f"predicted win rate: {100 * wins / total:.1f}%")
        print("predicted death share by block (% of deaths): " + "  ".join(
            f"{a}-{b}: {100 * sum(v for k, v in deaths.items() if a <= k <= b) / all_deaths:.0f}%" for a, b in blocks(self.n)))


def nice(value):
    """Round to two significant digits, preferring steps of 5 in the second digit for large numbers."""
    exp = 10 ** (int(math.floor(math.log10(value))) - 1)
    return int(round(value / exp) * exp)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("mode", choices=["summary", "predict", "solve"])
    ap.add_argument("files", nargs="*")
    ap.add_argument("--md", action="store_true")
    ap.add_argument("--fit")
    ap.add_argument("--base")
    ap.add_argument("--targets")
    ap.add_argument("--clear", default="0.90")
    ap.add_argument("--round", action="store_true")
    args = ap.parse_args()
    if args.mode == "summary":
        summary(args.files, args.md)
        return
    model = Model([load(p) for p in args.fit.split(",")], load(args.base))
    if args.mode == "predict":
        values = [int(x) for x in args.targets.split(",")]
        model.report({FIRST + i: v for i, v in enumerate(values)})
        return
    # solve: stage by stage bisection on the target so that the predicted clear rate equals --clear
    targets = {}
    ends = [float(x) for x in args.clear.split(",")]
    prev = model.fixed[FIRST - 2]
    for k in range(FIRST, model.n + 1):
        want = ends[0] if len(ends) == 1 else ends[0] + (ends[1] - ends[0]) * (k - FIRST) / (model.n - FIRST)
        lo, hi = 1e3, 1e12
        for _ in range(60):
            mid = math.sqrt(lo * hi)
            trial = dict(targets)
            for j in range(k, model.n + 1):
                trial[j] = mid
            out, _ = model.walk(trial)
            if out[k][1] > want:
                lo = mid
            else:
                hi = mid
        value = min(2.0 * prev, max(1.3 * prev, lo))
        targets[k] = nice(value) if args.round else int(value)
        prev = targets[k]
    print("targets: " + ",".join(str(targets[k]) for k in range(FIRST, model.n + 1)))
    model.report(targets)


if __name__ == "__main__":
    main()
