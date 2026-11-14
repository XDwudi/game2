"""Fill the generated tables of docs/specs/v08/BALANCE_PLAN.md from the evidence JSON files.

Only placeholders of the form {{NAME}} are replaced; running it twice is harmless.
"""
import contextlib
import io
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import report_tables as rt  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
EV = ROOT / "evidence" / "v08-balance"
DOC = ROOT / "docs" / "specs" / "v08" / "BALANCE_PLAN.md"
CAND = ROOT / "tools" / "balance_sim" / "candidates" / "v08_candidate.json"


def capture(fn, *args):
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        fn(*args)
    return buf.getvalue().strip()


def stage_table(run):
    out = ["| 关 | 目标分 | 环比 | 到达率 | 本关通过率 | 用掉提交 | 实力÷目标（中位） | 关始金币 | 本关收入 | 商店支出 | 控制器数 |",
           "| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |"]
    prev = None
    for s in run["stages"]:
        t = float(s["target"])
        growth = "–" if prev is None else f"×{t / prev:.2f}"
        prev = t
        income = s["reward"] + s["unused"] + s["interest"] + s["pig"]
        out.append(f"| {s['stage']} | {int(t):,} | {growth} | {s['reach']:.0f}% | {s['clear']:.0f}% | {s['subs']:.2f} | {s['power']:.1f} | {s['goldStart']:.0f} | {income:.1f} | {s['spent']:.1f} | {s['ctrl']:.1f} |")
    return "\n".join(out)


def deaths(run):
    d = {}
    for log in run["logs"]:
        if not log["won"]:
            d[log["reached"]] = d.get(log["reached"], 0) + 1
    total = sum(d.values()) or 1
    bands = [(1, 4), (5, 8), (9, 12), (13, 16), (17, 20), (21, 24)]
    return "、".join(f"{a}–{b} 关 {100 * sum(v for k, v in d.items() if a <= k <= b) / total:.0f}%" for a, b in bands)


def end_stats(run):
    gold = sorted(log["stages"][-1]["goldAfterSettle"] for log in run["logs"] if log["won"])
    ctrl = [log["stages"][-1]["ctrl"] for log in run["logs"] if log["won"]]
    return (gold[len(gold) // 2] if gold else 0), (sum(ctrl) / len(ctrl) if ctrl else 0)


def target_table():
    old = json.loads(rt.SHIPPED.read_text(encoding="utf-8"))["stages"]
    new = json.loads(CAND.read_text(encoding="utf-8"))["stages"]
    out = ["| 关 | 现行目标 | 现行环比 | v0.8 目标 | v0.8 环比 | 相对现行 | v0.8 高压目标 |", "| --- | --- | --- | --- | --- | --- | --- |"]
    po = pn = None
    for o, n in zip(old, new):
        a, b = int(o["normalTarget"]), int(n["normalTarget"])
        go = "–" if po is None else f"×{a / po:.2f}"
        gn = "–" if pn is None else f"×{b / pn:.2f}"
        risk = f"{int(n['riskTarget']):,}" if n["riskEnabled"] else "–"
        out.append(f"| {int(o['stageId'][-2:])} | {a:,} | {go} | {b:,} | {gn} | {100 * b / a - 100:+.0f}% | {risk} |")
        po, pn = a, b
    return "\n".join(out)


def main():
    v06 = rt.load(EV / "run_v06.json")
    v08 = rt.load(EV / "run_v08.json")
    g06, c06 = end_stats(v06)
    g08, c08 = end_stats(v08)
    last06, last08 = v06["stages"][-1], v08["stages"][-1]
    values = {
        "CARD_TABLE": capture(rt.cards, EV / "cards_v06.json", EV / "cards_v08.json", CAND),
        "TARGET_TABLE": target_table(),
        "STAGE_TABLE_V06": stage_table(v06),
        "STAGE_TABLE_V08": stage_table(v08),
        "RUNS_V06": str(v06["runs"]), "RUNS_V08": str(v08["runs"]),
        "WIN_V06": f"{100 * v06['wins'] / v06['runs']:.0f}%", "WIN_V08": f"{100 * v08['wins'] / v08['runs']:.0f}%",
        "DEATH_V06": "死亡分布为 " + deaths(v06), "DEATH_V08": deaths(v08),
        "FINAL_RATIO_V06": f"{last06['power']:.0f}", "FINAL_CLEAR_V06": f"{last06['clear']:.0f}%",
        "END_GOLD_V06": str(g06), "END_CTRL_V06": f"{c06:.1f}", "END_GOLD_V08": str(g08), "END_CTRL_V08": f"{c08:.1f}",
    }
    text = DOC.read_text(encoding="utf-8")
    for key, value in values.items():
        text = text.replace("{{" + key + "}}", value)
    DOC.write_text(text, encoding="utf-8", newline="\n")
    left = sorted(set(part.split("}}")[0] for part in text.split("{{")[1:]))
    print("filled", len(values), "remaining placeholders:", left)
    print("deaths v06:", deaths(v06))
    print("deaths v08:", deaths(v08))
    print("end gold/ctrl v06:", g06, round(c06, 1), " v08:", g08, round(c08, 1))


if __name__ == "__main__":
    main()
