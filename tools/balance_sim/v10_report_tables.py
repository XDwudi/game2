"""Markdown tables for docs/specs/v10/BALANCE_REPORT.md, generated from the simulator's JSON evidence.

Every number in the report's tables comes from one of these files, so the report can be regenerated and checked:

    python tools/balance_sim/v10_report_tables.py cards   evidence/v10/balance/cards_p100_final.json
    python tools/balance_sim/v10_report_tables.py stages  evidence/v10/balance/run_p100_final.json
    python tools/balance_sim/v10_report_tables.py bosses  evidence/v10/balance/run_p100_final.json
    python tools/balance_sim/v10_report_tables.py loot    evidence/v10/balance/run_p100_final.json
    python tools/balance_sim/v10_report_tables.py usage   evidence/v10/balance/run_p100_final.json
    python tools/balance_sim/v10_report_tables.py scen    evidence/v10/balance/scen_p100_final.json
    python tools/balance_sim/v10_report_tables.py compare evidence/v10/balance/run_a.json evidence/v10/balance/run_b.json ...
    python tools/balance_sim/v10_report_tables.py fill docs/specs/v10/BALANCE_REPORT.md     # regenerate the tables in place
    python tools/balance_sim/v10_report_tables.py fill docs/specs/v10/BALANCE_REPORT.md --check

The first six modes only read and print to stdout (UTF-8). `fill` rewrites what stands between the marker pairs
`<!-- BEGIN:<mode> <json path> [<json path> ...] -->` and `<!-- END:<mode> -->` of the report with the output of that mode;
`--check` fails if the report on disk differs from what `fill` would write.
"""
import contextlib
import io
import json
import re
import sys
from pathlib import Path

RARITY = {"common": "普通", "uncommon": "罕见", "rare": "稀有"}
KIND = {"normal": "普通", "pressure": "高压", "boss": "首领"}


def load(path: str) -> dict:
    return json.loads(Path(path).read_text(encoding="utf-8"))


def x(value) -> str:
    return "–" if value is None else f"×{value:.2f}"


def cards(path: str) -> None:
    data = load(path)
    rows = [r for r in data["rows"] if r["id"].startswith("C") and int(r["id"][1:]) >= 104]
    contexts = sorted({r["context"] for r in rows})
    by_id = {}
    for r in rows:
        by_id.setdefault(r["id"], []).append(r)
    print(f"两个参照构筑：{' ／ '.join(contexts)}。每格 = 有这张卡的期望整关得分 ÷ 没有它（{data['hands']} 手）。")
    print()
    print("| 卡 | 稀有度 / 价 | 买来时 A | 买来时 B | 触发率 A / B | 成型后（注明状态）A | 成型后 B |")
    print("| --- | --- | --- | --- | --- | --- | --- |")
    for cid in sorted(by_id, key=lambda i: int(i[1:])):
        group = by_id[cid]
        first = group[0]

        def pick(ctx_index: int, grown: bool):
            ctx = contexts[ctx_index]
            cand = [r for r in group if r["context"] == ctx and (bool(r["growth"]) or bool(r["stagesPlayed"])) == grown]
            if not cand:
                return None
            return max(cand, key=lambda r: (r["growth"], r["stagesPlayed"]))

        a0, b0, a1, b1 = pick(0, False), pick(1, False), pick(0, True), pick(1, True)

        def label(r):
            if r is None:
                return "–"
            note = f"成长 {r['growth']}" if r["growth"] else f"打 {r['stagesPlayed']} 关后"
            return f"{x(r['ratio'])}（{note}）"

        situation = f"［{first['situation']}］" if first.get("situation") else ""
        hit = "–" if a0 is None or b0 is None else f"{a0['hit'] * 100:.0f}% / {b0['hit'] * 100:.0f}%"
        print(f"| {cid} {first['name']}{situation} | {RARITY[first['rarity']]} {first['price']} | {x(a0['ratio']) if a0 else '–'} | "
              f"{x(b0['ratio']) if b0 else '–'} | {hit} | {label(a1)} | {label(b1) if b1 else '–'} |")


def stages(path: str) -> None:
    data = load(path)
    print(f"{data['runs']} 局，种子 {data['firstSeed']}–{data['firstSeed'] + data['seeds'] - 1}，通关 {data['wins']} 局（{data['summary']['winRatePct']:.1f}%）。")
    print()
    print("| 关 | 类型 | 目标 | 到达 % | 通过 % | 平均提交 | 实力 ÷ 目标（中位） | 关始金币 | 离店金币 | 控制器 | 接高压 % |")
    print("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |")
    for s in data["stages"]:
        risk = f"{s['risk']:.0f}" if s["kind"] == "pressure" else "–"
        print(f"| {s['stage']} | {KIND[s['kind']]} | {int(s['target']):,} | {s['reach']:.0f} | {s['clear']:.0f} | {s['subs']:.2f} | {s['power']:.2f} | "
              f"{s['goldStart']:.0f} | {s['goldLeave']:.0f} | {s['ctrl']:.1f} | {risk} |")
    summary = data["summary"]
    print()
    print("死亡分布（占全部死亡的 %，每四关一段）：" + " / ".join(f"{v:.0f}" for v in summary["deathShareByBlockPct"]))
    print("按关卡类型的死亡数：" + "，".join(f"{KIND.get(k, k)} {v}" for k, v in summary["deathsByStageKind"].items()))
    for k in data["stageKinds"]:
        print(f"{KIND[k['kind']]}关：到达 {k['reached']}，通过 {k['cleared']}（{k['clearPct']:.1f}%）")
    p = data["pressure"]
    print(f"高压挑战：出现 {p['offered']} 次，接受 {p['acceptedPct']:.1f}%，接受后通过 {p['clearedWhenAcceptedPct']:.1f}%，不接受时通过 {p['clearedWhenDeclinedPct']:.1f}%")
    print(f"最后一关 实力 ÷ 目标（中位）：减益前 {summary['lastStagePowerOverTargetMedian']:.2f}，带减益 {summary['lastStagePowerOverTargetWithModifierMedian']:.2f}")


def bosses(path: str) -> None:
    data = load(path)
    print("| 减益 | 档 | 遇到 | 被解除 | 通过 % | 实力 ÷ 目标（带减益） | 不带减益 | 实力损失 % |")
    print("| --- | --- | --- | --- | --- | --- | --- | --- |")
    for m in data["bossModifiers"]:
        tier = "终局" if m["tier"] == 4 else str(m["tier"])
        power = "–" if m["powerOverTarget"] is None else f"{m['powerOverTarget']:.2f}"
        raw = "–" if m["rawPowerOverTarget"] is None else f"{m['rawPowerOverTarget']:.2f}"
        loss = "–" if m["lossPct"] is None else f"{m['lossPct']:.0f}"
        clear = "–" if m["clearPct"] is None else f"{m['clearPct']:.0f}"
        print(f"| {m['id']} {m['name']} | {tier} | {m['faced']} | {m['lifted']} | {clear} | {power} | {raw} | {loss} |")


def loot(path: str) -> None:
    data = load(path)
    print("| 战利品 | 出现 | 领了东西 % | 折现 % | 领得最多的 |")
    print("| --- | --- | --- | --- | --- |")
    for k in data["loot"]:
        offered = max(1, k["offered"])
        top = "、".join(f"{i}×{n}" for i, n in list(k["picks"].items())[:5])
        print(f"| {k['id']} {k['name']} | {k['offered']} | {k['tookOffers'] * 100 / offered:.0f} | {k['tookGold'] * 100 / offered:.0f} | {top} |")
    bought = {i["id"]: i["perRun"] for i in data["items"]}
    held = {c["id"]: c["perRun"] for c in data["cards"]}
    print()
    print("每局购买次数：" + "，".join(f"{name} {bought.get(i, 0):.2f}" for i, name in (
        ("O09", "贿赂首领"), ("O10", "理财顾问"), ("O11", "附魔台"), ("O08", "扩充栏位"), ("I08", "破咒符"), ("I09", "灵光一闪"), ("I11", "点金笔"), ("I12", "扩容符")))
          + f"，替罪羊 {held.get('C131', 0):.2f}")
    print("每局使用的道具：" + "，".join(f"{i} {n:.2f}" for i, n in data["itemsUsedPerRun"].items()))
    print("第 9 关起关始金币：" + " ".join(f"{s['goldStart']:.0f}" for s in data["stages"][8:]) + f"；通关局终局金币见最后一关的离店金币 {data['stages'][-1]['goldLeave']:.0f}")


def usage(path: str) -> None:
    data = load(path)
    new = [c for c in data["cards"] if c["generation"] == "p1.0"]
    print("| 新卡 | 稀有度 / 价 | 每局出现 | 每局购买 | 出现后购买 % | 通关构筑里持有 % |")
    print("| --- | --- | --- | --- | --- | --- |")
    for c in sorted(new, key=lambda c: -c["perRun"]):
        print(f"| {c['id']} {c['name']} | {RARITY[c['rarity']]} {c['price']} | {c['shownPerRun']:.2f} | {c['perRun']:.2f} | {c['buyPctOfShown']:.0f} | {c['winPct']:.0f} |")
    print()
    print("通关构筑里出现最多的 12 张：" + "、".join(f"{b['id']} {b['name']} {b['winPct']:.0f}%" for b in data["winningBuilds"][:12]))
    print("从未被购买：" + "、".join(data["neverBought"]))


def compare(paths: list) -> None:
    runs = [(Path(p).stem, load(p)) for p in paths]
    print("| 指标 | " + " | ".join(name for name, _ in runs) + " |")
    print("| --- |" + " --- |" * len(runs))
    def row(title, fn):
        print(f"| {title} | " + " | ".join(fn(d) for _, d in runs) + " |")
    row("通关率", lambda d: f"{d['summary']['winRatePct']:.1f}%")
    row("死亡分布（每四关一段，%）", lambda d: " / ".join(f"{v:.0f}" for v in d["summary"]["deathShareByBlockPct"]))
    for kind in ("normal", "pressure", "boss"):
        row(f"{KIND[kind]}关通过率", lambda d, kind=kind: f"{next(k for k in d['stageKinds'] if k['kind'] == kind)['clearPct']:.1f}%")
    row("高压接受率", lambda d: f"{d['pressure']['acceptedPct']:.0f}%")
    row("最后一关 实力 ÷ 目标（减益前 / 带减益）", lambda d: f"{d['summary']['lastStagePowerOverTargetMedian']:.1f} / {d['summary']['lastStagePowerOverTargetWithModifierMedian']:.1f}")
    row("第 9 关起关始金币", lambda d: f"{min(s['goldStart'] for s in d['stages'][8:]):.0f}–{max(s['goldStart'] for s in d['stages'][8:]):.0f}")
    row("第 24 关目标", lambda d: f"{int(d['stages'][-1]['target']):,}")
    row("逐关通过率", lambda d: " ".join(f"{s['clear']:.0f}" for s in d["stages"]))


def scen(path: str) -> None:
    rows = [r for r in load(path) if r["name"].startswith("[v1.0]")]
    print("| 场景（成型状态） | 每手期望 | 中位 | 第 10 / 90 百分位 | 三手合计 | 够得着第几关的目标 |")
    print("| --- | --- | --- | --- | --- | --- |")
    for r in rows:
        print(f"| {r['name'][7:]} | {r['mean']:,.0f} | {r['median']:,.0f} | {r['p10']:,.0f} / {r['p90']:,.0f} | {r['stageTotal']:,.0f} | {r['reachesStageTarget']} |")


SINGLE = {"cards": cards, "stages": stages, "bosses": bosses, "loot": loot, "usage": usage, "scen": scen}


def render(mode: str, paths: list) -> str:
    out = io.StringIO()
    with contextlib.redirect_stdout(out):
        if mode == "compare":
            compare(paths)
        else:
            SINGLE[mode](paths[0])
    return out.getvalue().rstrip("\n")


def fill(report: str, check: bool) -> int:
    path = Path(report)
    text = path.read_text(encoding="utf-8")
    pattern = re.compile(r"(<!-- BEGIN:(\w+) ([^>]*?) -->\r?\n)(.*?)(<!-- END:\2 -->)", re.S)
    count = 0

    def replace(match):
        nonlocal count
        count += 1
        return match.group(1) + render(match.group(2), match.group(3).split()) + "\n" + match.group(5)

    updated = pattern.sub(replace, text)
    if check:
        ok = updated == text and count > 0
        print(f"report tables match the evidence ({count} tables)" if ok else f"report tables differ from the evidence or no table markers were found ({count} markers)")
        return 0 if ok else 1
    path.write_text(updated, encoding="utf-8", newline="\n")
    print(f"filled {count} tables")
    return 0 if count else 1


def main() -> int:
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    sys.stdout.reconfigure(encoding="utf-8")
    mode = sys.argv[1]
    if mode == "fill":
        return fill(sys.argv[2], "--check" in sys.argv[3:])
    print(render(mode, sys.argv[2:]))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
