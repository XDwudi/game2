"""Turn simulator JSON output into the Markdown tables used by docs/specs/v08/BALANCE_PLAN.md.

    python tools/balance_sim/report_tables.py stages <run.json> [<run2.json>]
    python tools/balance_sim/report_tables.py cards <cards_v06.json> <cards_candidate.json> <candidate_config.json>
    python tools/balance_sim/report_tables.py items <run.json>
"""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SHIPPED = ROOT / "tools" / "balance_sim" / "baseline" / "P0Config.p0.6.0.json"  # the p0.6.0 config the proposal was measured against
RARITY = {"common": "普通", "uncommon": "罕见", "rare": "稀有"}
# Changes that live in code, so the simulator could not measure them; values are computed by hand.
CODE_ITEMS = {
    "C53": "罕见 4 | ×2.22 † | ×2.22 †",
    "C47": "稀有 8 | ×1.00 | ×1.50 †",
}


def load(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def stages(paths):
    runs = [load(p) for p in paths]
    for run in runs:
        print(f"\n<!-- {run['runs']} runs, wins {run['wins']} ({100 * run['wins'] / run['runs']:.1f}%) -->")
        print("| 关 | 目标分 | 环比 | 到达率 | 本关通过率 | 用掉提交 | 实力÷目标(中位) | 关始金币 | 本关收入 | 商店支出 | 控制器数 | 骰子数 |")
        print("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |")
        prev = None
        for s in run["stages"]:
            t = float(s["target"])
            growth = "–" if prev is None else f"×{t / prev:.2f}"
            prev = t
            income = s["reward"] + s["unused"] + s["interest"] + s["pig"]
            print(f"| {s['stage']} | {int(t):,} | {growth} | {s['reach']:.0f}% | {s['clear']:.0f}% | {s['subs']:.2f} | {s['power']:.1f} | {s['goldStart']:.0f} | {income:.1f} | {s['spent']:.1f} | {s['ctrl']:.1f} | {s['dice']:.1f} |")
        deaths = {}
        for log in run["logs"]:
            if not log["won"]:
                deaths[log["reached"]] = deaths.get(log["reached"], 0) + 1
        total = sum(deaths.values()) or 1
        bands = [(1, 4), (5, 8), (9, 12), (13, 16), (17, 20), (21, 24)]
        print("\n死亡分布：" + "，".join(f"{a}–{b} 关 {100 * sum(v for k, v in deaths.items() if a <= k <= b) / total:.0f}%" for a, b in bands))
        end_gold = [log["stages"][-1]["goldAfterSettle"] for log in run["logs"] if log["won"]]
        if end_gold:
            end_gold.sort()
            print(f"通关局终局金币中位数：{end_gold[len(end_gold) // 2]}")


def cards(v06_path, cand_path, cand_cfg_path):
    old_cfg = {c["id"]: c for c in load(SHIPPED)["controllers"]}
    new_cfg = {c["id"]: c for c in load(cand_cfg_path)["controllers"]}

    def index(rows):
        out = {}
        for r in rows:
            out.setdefault(r["id"], {})["A" if r["context"].startswith("A") else "B"] = r
        return out

    old, new = index(load(v06_path)), index(load(cand_path))
    print("| 编号 | 名称 | 现稀有度/价 | 现·裸装 | 现·中期 | 触发率 | 新稀有度/价 | 新·裸装 | 新·中期 |")
    print("| --- | --- | --- | --- | --- | --- | --- | --- | --- |")
    for cid in sorted(old_cfg, key=lambda k: -old[k]["A"]["ratio"]):
        o, n = old_cfg[cid], new_cfg[cid]
        moved = any(abs(new[cid][k]["ratio"] - old[cid][k]["ratio"]) > 0.04 for k in "AB")
        changed = o["rarity"] != n["rarity"] or o["buyPrice"] != n["buyPrice"] or o["parameters"] != n["parameters"] or moved
        new_cells = f"{RARITY[n['rarity']]} {n['buyPrice']} | ×{new[cid]['A']['ratio']:.2f} | ×{new[cid]['B']['ratio']:.2f}" if changed else "不变 | – | –"
        new_cells = CODE_ITEMS.get(cid, new_cells)
        print(f"| {cid} | {o['displayName']} | {RARITY[o['rarity']]} {o['buyPrice']} | ×{old[cid]['A']['ratio']:.2f} | ×{old[cid]['B']['ratio']:.2f} | {old[cid]['A']['hit'] * 100:.0f}% | {new_cells} |")


def items(path):
    run = load(path)
    print("| 物品 | 每局购买次数 | 平均购买关卡 | 终局持有率 | 通关局持有率 |")
    print("| --- | --- | --- | --- | --- |")
    for r in run["items"][:40]:
        print(f"| {r['id']} {r['name']} | {r['perRun']:.2f} | {r['avgStage']:.1f} | {r['finalPct']:.0f}% | {r['winPct']:.0f}% |")


def scen(old_path, new_path):
    old, new = load(old_path), load(new_path)
    base_old, base_new = old[0]["mean"], new[0]["mean"]
    print("| 状态 | 现行·期望单手 | 相对裸装 | v0.8·期望单手 | 相对裸装 |")
    print("| --- | --- | --- | --- | --- |")
    for a, b in zip(old, new):
        print(f"| {a['name']} | {a['mean']:,.0f} | ×{a['mean'] / base_old:.1f} | {b['mean']:,.0f} | ×{b['mean'] / base_new:.1f} |")


if __name__ == "__main__":
    mode = sys.argv[1]
    if mode == "scen":
        scen(sys.argv[2], sys.argv[3])
    if mode == "stages":
        stages(sys.argv[2:])
    elif mode == "cards":
        cards(*sys.argv[2:5])
    elif mode == "items":
        items(sys.argv[2])
