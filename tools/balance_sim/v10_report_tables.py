"""Markdown tables for docs/specs/v10/BALANCE_REPORT.md, generated from the simulator's JSON evidence.

Every number in the report's tables comes from one of these files, so the report can be regenerated and checked:

    python tools/balance_sim/v10_report_tables.py cards   evidence/v10/balance/cards_p100_final.json
    python tools/balance_sim/v10_report_tables.py stages  evidence/v10/balance/run_p100_final.json
    python tools/balance_sim/v10_report_tables.py bosses  evidence/v10/balance/run_p100_final.json
    python tools/balance_sim/v10_report_tables.py loot    evidence/v10/balance/run_p100_final.json
    python tools/balance_sim/v10_report_tables.py usage   evidence/v10/balance/run_p100_final.json
    python tools/balance_sim/v10_report_tables.py scen    evidence/v10/balance/scen_p100_final.json
    python tools/balance_sim/v10_report_tables.py compare evidence/v10/balance/run_a.json evidence/v10/balance/run_b.json ...
    python tools/balance_sim/v10_report_tables.py paired_v11 evidence/v10/balance/run_p100_final.json evidence/v11/balance/run_p110_300.json --out evidence/v11/balance/paired_p100_p110.md
    python tools/balance_sim/v10_report_tables.py v11 evidence/v11/balance/run_p110_300.json
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


# ---------------------------------------------------------------- playtest report (several run files pooled)
def runs_of(paths: list) -> list:
    logs = []
    for p in paths:
        logs += load(p)["logs"]
    return logs


def wilson(k: int, n: int) -> str:
    if n == 0:
        return "–"
    z = 1.96
    p = k / n
    centre = (p + z * z / (2 * n)) / (1 + z * z / n)
    half = z * ((p * (1 - p) / n + z * z / (4 * n * n)) ** 0.5) / (1 + z * z / n)
    return f"{100 * p:.1f}%（95% 区间 {100 * (centre - half):.1f}–{100 * (centre + half):.1f}）"


def median(values: list) -> float:
    a = sorted(values)
    return float("nan") if not a else (a[len(a) // 2] if len(a) % 2 else (a[len(a) // 2 - 1] + a[len(a) // 2]) / 2)


def pool(paths: list) -> None:
    logs = runs_of(paths)
    wins = sum(1 for l in logs if l["won"])
    stages = [s for l in logs for s in l["stages"]]
    print(f"合并 {len(paths)} 份整局数据，共 {len(logs)} 局：通关 {wins} 局，通关率 {wilson(wins, len(logs))}。")
    print()
    print("| 关卡类型 | 到达 | 通过率 |")
    print("| --- | --- | --- |")
    for kind in ("normal", "pressure", "boss"):
        at = [s for s in stages if s["kind"] == kind]
        print(f"| {KIND[kind]} | {len(at)} | {wilson(sum(1 for s in at if s['cleared']), len(at))} |")
    final = [s for s in stages if s["stage"] == 24]
    print(f"| 其中第 24 关 | {len(final)} | {wilson(sum(1 for s in final if s['cleared']), len(final))} |")
    deaths = [l["reached"] for l in logs if not l["won"]]
    print()
    print("死在哪一段（占全部失败局的 %）：" + " / ".join(f"第 {b * 4 + 1}–{b * 4 + 4} 关 {100 * sum(1 for d in deaths if b * 4 < d <= b * 4 + 4) / max(1, len(deaths)):.0f}" for b in range(6)))
    print("走进每一段的局里，死在这一段的（%）：" + " / ".join(f"第 {b * 4 + 1}–{b * 4 + 4} 关 {100 * sum(1 for d in deaths if b * 4 < d <= b * 4 + 4) / max(1, sum(1 for l in logs if len(l['stages']) > b * 4)):.0f}" for b in range(6)))
    last = {k: sum(1 for l in logs if not l["won"] and l["stages"][-1]["kind"] == k) for k in ("normal", "pressure", "boss")}
    print("失败局最后一关的类型：" + "，".join(f"{KIND[k]}关 {v} 局（{100 * v / max(1, len(deaths)):.0f}%）" for k, v in last.items()))
    print("逐关通过率：" + " ".join(f"{100 * sum(1 for s in stages if s['stage'] == i and s['cleared']) / max(1, sum(1 for s in stages if s['stage'] == i)):.0f}" for i in range(1, 25)))
    print("打完每个周期后还活着的局（%）：" + " / ".join(f"第 {c} 周期 {100 * sum(1 for l in logs if l['won'] or l['reached'] > 3 * c) / len(logs):.0f}" for c in range(1, 9)))
    won = [l for l in logs if l["won"]]
    if won:
        print(f"通关局一局的提交次数中位 {median([sum(s['subsUsed'] for s in l['stages']) for l in won]):.0f} 次（24 关），每家商店平均买 {sum(len(s['bought']) for l in won for s in l['stages'][:-1]) / max(1, sum(len(l['stages']) - 1 for l in won)):.1f} 件东西。")
    print("失败原因：" + "，".join(f"{k} {v}" for k, v in sorted({r: sum(1 for l in logs if l.get('fail') == r) for r in {l.get('fail') for l in logs if not l['won']}}.items(), key=lambda kv: -kv[1])))


def tension(paths: list) -> None:
    """A stage ends the moment its target is reached, so "score ÷ target" of a cleared stage says little about how close
    it was. What does: how many submissions it took, whether the last one was needed, and the bot's own margin."""
    stages = [s for l in runs_of(paths) for s in l["stages"]]
    print("| 关段 | 一手过关 % | 两手以上过关 % | 其中用到最后一次提交 % | 失败 % | 平均提交次数 | 机器人估算的实力 ÷ 目标（中位） |")
    print("| --- | --- | --- | --- | --- | --- | --- |")

    def row(title, at):
        if not at:
            return
        ok = [s for s in at if s["cleared"]]
        n = len(at)
        margin = [s["power"] / s["target"] for s in at if s["target"] > 0 and s.get("power") is not None]
        print(f"| {title} | {100 * sum(1 for s in ok if s['subsUsed'] == 1) / n:.0f} | {100 * sum(1 for s in ok if s['subsUsed'] > 1) / n:.0f} | "
              f"{100 * sum(1 for s in ok if s['unusedSubs'] == 0) / n:.0f} | {100 * (n - len(ok)) / n:.0f} | {sum(s['subsUsed'] for s in at) / n:.2f} | {median(margin):.1f} |")

    for b in range(6):
        row(f"第 {b * 4 + 1}–{b * 4 + 4} 关", [s for s in stages if b * 4 < s["stage"] <= b * 4 + 4])
    for kind in ("normal", "pressure", "boss"):
        row(KIND[kind] + "关（全部）", [s for s in stages if s["kind"] == kind])
    row("接了高压挑战的关", [s for s in stages if s.get("risk")])
    won = [l for l in runs_of(paths) if l["won"]]
    last = [sum(1 for s in l["stages"] if s["cleared"] and s["unusedSubs"] == 0) for l in won]
    print()
    print(f"“实力”是机器人开打前对这一关全部提交的期望总分的估算（已计入首领减益）。通关局里，靠最后一次提交才过的关平均每局 {sum(last) / max(1, len(last)):.1f} 关（中位 {median(last):.0f} 关）。")


def gold(paths: list) -> None:
    logs = runs_of(paths)
    stages = [s for l in logs for s in l["stages"] if s["cleared"]]
    print("| 关段 | 关始金币 | 每关结算收入 | 其中利息 | 利息占 % | 每关净支出 | 每店购买件数 | 什么都没买的店 % | 离店金币（平均） | 离店 ≥ 25 的店 % | 离店 ≥ 40 的店 % |")
    print("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |")
    for b in range(6):
        at = [s for s in stages if b * 4 < s["stage"] <= b * 4 + 4 and s["stage"] < 24]
        if not at:
            continue
        n = len(at)
        avg = lambda key: sum(s[key] for s in at) / n
        income = avg("reward") + avg("unused") + avg("interest") + avg("pig") + avg("ctlIncome")
        print(f"| 第 {b * 4 + 1}–{min(23, b * 4 + 4)} 关 | {avg('goldStart'):.1f} | {income:.1f} | {avg('interest'):.1f} | {100 * avg('interest') / max(1e-9, income):.0f} | {avg('spent'):.1f} | "
              f"{sum(len(s['bought']) for s in at) / n:.2f} | {100 * sum(1 for s in at if not s['bought']) / n:.0f} | {avg('goldAfterShop'):.1f} | "
              f"{100 * sum(1 for s in at if s['goldAfterShop'] >= 25) / n:.0f} | {100 * sum(1 for s in at if s['goldAfterShop'] >= 40) / n:.0f} |")
    won = [l for l in logs if l["won"]]
    total_income = [sum(s["reward"] + s["unused"] + s["interest"] + s["pig"] + s["ctlIncome"] for s in l["stages"]) for l in won]
    total_spent = [sum(s["spent"] for s in l["stages"]) for l in won]
    print()
    print(f"通关局（{len(won)} 局）整局结算收入中位 {median(total_income):.0f} 金币，净支出中位 {median(total_spent):.0f} 金币。"
          "结算收入 = 过关奖励 + 剩余提交 + 利息 + 关内提交时的进出 + 控制器收益；“净支出”已经抵掉卖卡与战利品折现的进账；“购买件数”含研究、刷新和替换。")


LAB_ECONOMIC = {"B03", "B04", "B12"}      # cost gold, not this stage's score
LAB_BLIND = {"B02", "B07", "B24"}         # no items / frozen growth / permanent downgrade: outside a one-stage power estimate


def lab(paths: list) -> None:
    data = load(paths[0])
    meta = {m["id"]: m for m in data["bossModifiers"]}
    values, ordered = {}, {}
    for l in data["logs"]:
        for s in l["stages"]:
            for mid, v in (s.get("lab") or {}).items():
                values.setdefault(mid, []).append((s["stage"], v))
            for mid, v in (s.get("labOrdered") or {}).items():
                ordered.setdefault(mid, []).append(v)

    def q(a, f):
        a = sorted(a)
        return a[min(len(a) - 1, int(f * len(a)))]

    print("| 减益 | 档 | 构筑数 | 实力损失 % 中位 | 四分位区间 | 损失 ≥ 50% 的构筑占 % | 调序后损失 % 中位 |")
    print("| --- | --- | --- | --- | --- | --- | --- |")
    rows = sorted(values.items(), key=lambda kv: (meta[kv[0]]["tier"], -median([v for _, v in kv[1]])))
    for mid, pairs in rows:
        m = meta[mid]
        tier = "终局" if m["tier"] == 4 else str(m["tier"])
        v = [x for _, x in pairs]
        if mid in LAB_ECONOMIC or mid in LAB_BLIND:
            note = "代价是金币，不在当关得分上" if mid in LAB_ECONOMIC else "这种估算量不出（见表下说明）"
            print(f"| {mid} {m['name']} | {tier} | {len(v)} | – | – | – | {note} |")
            continue
        order = f"{100 * median(ordered[mid]):.0f}" if "DisableLeftmost" in m.get("effects", []) or mid in ("B25", "B37") else "–"
        print(f"| {mid} {m['name']} | {tier} | {len(v)} | {100 * median(v):.0f} | {100 * q(v, 0.25):.0f}–{100 * q(v, 0.75):.0f} | "
              f"{100 * sum(1 for x in v if x >= 0.5 - 1e-9) / len(v):.0f} | {order} |")
    if "B06" in values:
        by = {}
        for stage, v in values["B06"]:
            by.setdefault(stage, []).append(v)
        print()
        print("沉默（附魔失效）的损失随进度变化很大，中位数按关：" + " / ".join(f"第 {k} 关 {100 * median(by[k]):.0f}%" for k in sorted(by)) + "。")


def checked_seed_pair(base: dict, current: dict) -> tuple:
    """Reject incomplete or mismatched runs instead of silently pairing only their intersection."""
    indexed = []
    for data in (base, current):
        logs = data["logs"]
        by_seed = {row["seed"]: row for row in logs}
        expected = set(range(data["firstSeed"], data["firstSeed"] + data["seeds"]))
        if data["errors"] or data["runs"] != data["seeds"] or len(logs) != len(by_seed) or set(by_seed) != expected:
            raise ValueError("Paired comparison requires complete, unique, error-free seed logs.")
        if any((row.get("fail") or "").startswith("ERROR") for row in logs):
            raise ValueError("Paired comparison contains failed simulator runs.")
        if data.get("rejectedShopCommands", 0) or data.get("rejectedOptionalCommands", 0) or data.get("quoteMismatches", 0) or data.get("plannerSelfCheck", {}).get("mispredicted", 0):
            raise ValueError("Paired comparison contains failed simulator diagnostics.")
        indexed.append(by_seed)
    if not indexed[0] or set(indexed[0]) != set(indexed[1]):
        raise ValueError("Paired comparison requires exactly the same nonempty seed set.")
    return indexed[0], indexed[1], sorted(indexed[0])


def paired_v11(paths: list) -> None:
    if len(paths) != 2:
        raise ValueError("paired_v11 requires baseline and current run JSON paths.")
    base, current = map(load, paths)
    b, c, seeds = checked_seed_pair(base, current)
    n = len(seeds)
    print(f"按 seed 严格配对 {n} 局（{seeds[0]}–{seeds[-1]}）；输入：{paths[0]}；{paths[1]}。")
    print("规则与机器人适配一起改变；相同种子不保证后续商店、首领和随机事件一一相同，不能把差异全部归因于重复卡。")
    print()
    print(f"| 指标 | {base['config']} | {current['config']} |")
    print("| --- | --- | --- |")
    for title, metric in (
        ("通关", lambda logs: f"{sum(logs[s]['won'] for s in seeds)}/{n}（{100 * sum(logs[s]['won'] for s in seeds) / n:.2f}%）"),
        ("到达关数均值", lambda logs: f"{sum(logs[s]['reached'] for s in seeds) / n:.3f}"),
        ("首领关通过", lambda logs: (lambda at: f"{sum(x['cleared'] for x in at)}/{len(at)}")([x for s in seeds for x in logs[s]['stages'] if x['kind'] == 'boss'])),
        ("高压挑战接受", lambda logs: (lambda at: f"{sum(x['risk'] for x in at)}/{len(at)}")([x for s in seeds for x in logs[s]['stages'] if x.get('riskOffered')])),
    ):
        print(f"| {title} | {metric(b)} | {metric(c)} |")
    only_base = sum(b[s]["won"] and not c[s]["won"] for s in seeds)
    only_current = sum(c[s]["won"] and not b[s]["won"] for s in seeds)
    both = sum(b[s]["won"] and c[s]["won"] for s in seeds)
    diff = (only_current - only_base) / n
    half = 1.96 * ((only_current + only_base - (only_current - only_base) ** 2 / n) ** 0.5) / n
    print()
    print(f"双方通关 {both}；仅基线通关 {only_base}；仅新版通关 {only_current}；双方失败 {n - both - only_base - only_current}。")
    print(f"新版 − 基线通关率：{100 * diff:+.2f} 个百分点；配对正态近似 95% 区间 {100 * (diff - half):+.2f} 至 {100 * (diff + half):+.2f}（小样本或少量不一致配对时仅供描述）。")
    print(f"同 seed 到达关数差均值：{sum(c[s]['reached'] - b[s]['reached'] for s in seeds) / n:+.3f}。")


def v11(path: str) -> None:
    data = load(path)
    dup = data["duplicates"]
    print(f"{data['config']}：{data['runs']} 局，胜 {data['wins']}；错误 {data['errors']}，商店拒绝 {data['rejectedShopCommands']}。")
    print()
    print("| 获得来源 | 重复副本 | 全部卡牌获得 | 重复占比 % |")
    print("| --- | --- | --- | --- |")
    for key, name in (("shelf", "货架"), ("loot", "首领战利品")):
        row = dup[key]
        print(f"| {name} | {row['duplicateCopies']} | {row['acquiredCopies']} | {row['pct']:.2f} |")
    print()
    print("| 结局 | 局数 | 实际持有卡牌张数 | 其中额外副本 | 持有重复卡的局数 |")
    print("| --- | --- | --- | --- | --- |")
    for key, name in (("atDeath", "死亡"), ("atWin", "通关")):
        row = dup[key]
        print(f"| {name} | {row['runs']} | {row['totalCopies']} | {row['extraCopies']} | {row['runsWithDuplicates']} |")
    print()
    print("| 卡牌 | 货架重复获得 | 战利品重复获得 | 终局额外副本 | 终局同名最多张数 |")
    print("| --- | --- | --- | --- | --- |")
    for row in dup["cards"]:
        if row['shelf'] + row['loot'] + row['terminalExtraCopies']:
            print(f"| {row['id']} {row['name']} | {row['shelf']} | {row['loot']} | {row['terminalExtraCopies']} | {row['maxTerminalCopies']} |")
    print()
    print("| 战利品 | 出现 | 选物品 | 折现 | 未选 | 领取件数 / 总额度 | 每店领取件数分布 |")
    print("| --- | --- | --- | --- | --- | --- | --- |")
    for row in data['loot']:
        hist = '，'.join(f"{k} 件：{v} 店" for k, v in row['pickCountHistogram'].items())
        print(f"| {row['id']} {row['name']} | {row['offered']} | {row['tookOffers']} | {row['tookGold']} | {row['tookNothing']} | {row['picksTaken']} / {row['picksAvailable']} | {hist} |")


def paired(paths: list) -> None:
    base, prep = load(paths[0]), load(paths[1])
    b = {l["seed"]: l for l in base["logs"]}
    p = {l["seed"]: l for l in prep["logs"]}
    seeds = sorted(set(b) & set(p))
    print(f"同一批 {len(seeds)} 个种子，两种机器人各打一遍。")
    print()
    print("| 指标 | 发布时的机器人 | 会准备的机器人 |")
    print("| --- | --- | --- |")
    for title, fn in (
        ("通关率", lambda logs: wilson(sum(1 for s in seeds if logs[s]["won"]), len(seeds))),
        ("首领关通过率", lambda logs: (lambda at: wilson(sum(1 for x in at if x["cleared"]), len(at)))([x for s in seeds for x in logs[s]["stages"] if x["kind"] == "boss"])),
        ("最后一关通过率", lambda logs: (lambda at: wilson(sum(1 for x in at if x["cleared"]), len(at)))([x for s in seeds for x in logs[s]["stages"] if x["stage"] == 24])),
    ):
        print(f"| {title} | {fn(b)} | {fn(p)} |")
    cells = []
    for logs in (b, p):
        bought = [x for s in seeds for st in logs[s]["stages"] for x in st["bought"]]
        cells.append(" / ".join(str(sum(1 for x in bought if x.startswith(k))) for k in ("C131", "I08", "O09")))
    print(f"| 买替罪羊 / 破咒符 / 贿赂首领（整批次数） | {cells[0]} | {cells[1]} |")
    cells = []
    for logs in (b, p):
        lost = [logs[s] for s in seeds if not logs[s]["won"]]
        three = sum(1 for l in lost if l["stages"][-1].get("modifier") in ("B25", "B29", "B37"))
        cells.append(f"{three} / {len(lost)}（{100 * three / max(1, len(lost)):.0f}%）")
    print(f"| 失败局里死在缴械、凡人、万籁俱寂的 | {cells[0]} | {cells[1]} |")
    only_b = sum(1 for s in seeds if b[s]["won"] and not p[s]["won"])
    only_p = sum(1 for s in seeds if p[s]["won"] and not b[s]["won"])
    n = len(seeds)
    diff = (only_p - only_b) / n
    half = 1.96 * ((only_p + only_b - (only_p - only_b) ** 2 / n) ** 0.5) / n
    print()
    print(f"成对比较：只有会准备的机器人通关的种子 {only_p} 个，只有发布时的机器人通关的 {only_b} 个；通关率之差 {100 * diff:+.1f} 个百分点（95% 区间 {100 * (diff - half):+.1f} 到 {100 * (diff + half):+.1f}）。")
    print()
    print("发布时的机器人遇到三个“失效类”减益的那些关，同一个种子的同一关在会准备的机器人手里怎样了：")
    print()
    print("| 减益 | 发布时的机器人：遇到 → 通过 | 会准备的机器人：走到这一关 → 通过 | 其中把减益取消或贿赂换掉后过的 | 顶着减益打的 |")
    print("| --- | --- | --- | --- | --- |")
    for mod in ("B25", "B29", "B37"):
        name = next(m["name"] for m in base["bossModifiers"] if m["id"] == mod)
        faced = [(s, x) for s in seeds for x in b[s]["stages"] if x.get("modifier") == mod]
        there = [p[s]["stages"][x["stage"] - 1] for s, x in faced if len(p[s]["stages"]) >= x["stage"]]
        dodged = [y for y in there if y.get("modifier") != mod or y.get("nullifiedBy")]
        fought = [y for y in there if y.get("modifier") == mod and not y.get("nullifiedBy")]
        print(f"| {mod} {name} | {len(faced)} → {sum(1 for _, x in faced if x['cleared'])} | {len(there)} → {sum(1 for y in there if y['cleared'])} | "
              f"{sum(1 for y in dodged if y['cleared'])} / {len(dodged)} | {sum(1 for y in fought if y['cleared'])} / {len(fought)} |")


def cardwin(paths: list) -> None:
    """Win rate of the runs that held a card when they entered stage 10, against all runs that got that far.
    Conditioning on reaching stage 10 removes most of the survivorship effect (any card held late looks good
    simply because the run lived long enough to buy it)."""
    cut = 10
    logs = [l for l in runs_of(paths) if len(l["stages"]) >= cut]
    info = {c["id"]: c for c in load(paths[0])["cards"]}
    overall = sum(1 for l in logs if l["won"]) / len(logs)
    held = {}
    for l in logs:
        for cid in {x.split(":")[0] for x in l["stages"][cut - 1]["controllers"]}:
            a = held.setdefault(cid, [0, 0])
            a[0] += 1
            a[1] += 1 if l["won"] else 0
    rows = [(cid, n, w) for cid, (n, w) in held.items() if n >= 30]
    rows.sort(key=lambda r: -(r[2] / r[1]))
    print(f"走到第 {cut} 关的 {len(logs)} 局里，最终通关 {100 * overall:.1f}%。下表按“进第 {cut} 关时手里有这张卡”分组（只列 ≥ 30 局的卡；最高 12 张与最低 12 张）。")
    print()
    print("| 卡 | 稀有度 / 价 | 局数 | 这些局的通关率 | 相对同期全体 |")
    print("| --- | --- | --- | --- | --- |")
    for k, (cid, n, w) in enumerate(rows[:12] + rows[-12:]):
        if k == 12:
            print("| … | | | | |")
        c = info.get(cid, {"name": "", "rarity": "common", "price": 0})
        print(f"| {cid} {c['name']} | {RARITY[c['rarity']]} {c['price']} | {n} | {100 * w / n:.0f}% | {100 * (w / n - overall):+.0f} |")
    print()
    print(f"达到 30 局的卡共 {len(rows)} 张。30 局的通关率误差约 ±17 个百分点、100 局约 ±9：单张卡 10 个点以内的差别不能当结论。")

def diversity(paths: list) -> None:
    logs = [l for l in runs_of(paths) if l["won"]]
    builds = [[x.split(":")[0] for x in l["stages"][-1]["controllers"]] for l in logs]
    count = {}
    for b in builds:
        for cid in set(b):
            count[cid] = count.get(cid, 0) + 1
    top = sorted(count.items(), key=lambda kv: -kv[1])
    info = {c["id"]: c for c in load(paths[0])["cards"]}
    print(f"通关局 {len(builds)} 局，终局构筑平均 {sum(len(b) for b in builds) / max(1, len(builds)):.1f} 张控制器。")
    print(f"- 至少进入过 1 个通关构筑的卡：{len(count)} / {len(info)} 张；进入过 ≥ 5% 通关构筑的：{sum(1 for _, n in top if n >= 0.05 * len(builds))} 张。")
    print("- 出现最多的 10 张：" + "、".join(f"{cid} {info.get(cid, {}).get('name', '')} {100 * n / len(builds):.0f}%" for cid, n in top[:10]))
    everyone = runs_of(paths)
    held = {}
    for l in everyone:
        for cid in {x.split(":")[0] for s in l["stages"] for x in s["controllers"]}:
            held[cid] = held.get(cid, 0) + 1
    never = sorted(set.intersection(*[set(load(p).get("neverBought") or []) for p in paths]))
    names = "、".join(cid + " " + info[cid]["name"] for cid in never) if never else "无"
    print(f"- 全部 {len(everyone)} 局里，持有过的局不到 40 局的卡有 {sum(1 for cid in info if held.get(cid, 0) < 40)} 张；一次都没被买过的：{names}。")
    new = [cid for cid in count if int(cid[1:]) >= 104]
    print(f"- v1.0 新卡里进入过通关构筑的 {len(new)} / 50 张；通关构筑里平均有 {sum(sum(1 for c in b if int(c[1:]) >= 104) for b in builds) / max(1, len(builds)):.1f} 张 v1.0 新卡、"
          f"{sum(sum(1 for c in b if 54 <= int(c[1:]) <= 103) for b in builds) / max(1, len(builds)):.1f} 张 v0.9 卡、{sum(sum(1 for c in b if int(c[1:]) <= 53) for b in builds) / max(1, len(builds)):.1f} 张基础卡。")


def bosspool(paths: list) -> None:
    logs = runs_of(paths)
    meta = {m["id"]: m for m in load(paths[0])["bossModifiers"]}
    seen = {}
    for l in logs:
        for s in l["stages"]:
            if s.get("modifier"):
                a = seen.setdefault(s["modifier"], [0, 0, 0, [], 0])
                a[0] += 1
                a[1] += 1 if s["cleared"] else 0
                a[2] += 1 if s.get("nullifiedBy") else 0
                a[4] += 1 if s["cleared"] and not s.get("nullifiedBy") else 0
                if s["cleared"] and s["target"] > 0:
                    a[3].append(s["score"] / s["target"])
    print("| 减益 | 档 | 遇到 | 通过率 | 被破解（次） | 没破解、硬打的通过率 | 过关时得分 ÷ 目标（中位） |")
    print("| --- | --- | --- | --- | --- | --- | --- |")
    for mid, (n, ok, nul, ratio, raw) in sorted(seen.items(), key=lambda kv: (meta.get(kv[0], {}).get("tier", 0), kv[1][1] / kv[1][0])):
        m = meta.get(mid, {"name": "", "tier": 0})
        tier = "终局" if m["tier"] == 4 else str(m["tier"])
        print(f"| {mid} {m['name']} | {tier} | {n} | {wilson(ok, n)} | {nul} | {raw} / {n - nul}（{100 * raw / max(1, n - nul):.0f}%） | {median(ratio):.2f} |")
    lost = len([l for l in logs if not l["won"]])
    worst = sorted(seen.items(), key=lambda kv: -(kv[1][0] - kv[1][1]))[:3]
    print()
    print(f"全部 {lost} 局失败里，死在首领关的 {sum(n - ok for n, ok, *_ in seen.values())} 局；其中失败最多的三个减益是 "
          + "、".join(f"{mid} {meta.get(mid, {}).get('name', '')}（{n - ok} 局）" for mid, (n, ok, *_) in worst)
          + f"，合计占全部失败的 {100 * sum(n - ok for _, (n, ok, *_) in worst) / max(1, lost):.0f}%。")

def pressure(paths: list) -> None:
    stages = [s for l in runs_of(paths) for s in l["stages"] if s["kind"] == "pressure"]
    offered = [s for s in stages if s.get("riskOffered")]
    taken = [s for s in offered if s.get("risk")]
    declined = [s for s in offered if not s.get("risk")]
    print(f"高压关 {len(stages)} 关，其中 {len(offered)} 关提供了挑战：接了 {len(taken)} 关（{100 * len(taken) / max(1, len(offered)):.0f}%）。")
    print()
    print("| | 关数 | 通过率 | 过关后的结算收入（平均） |")
    print("| --- | --- | --- | --- |")
    for title, at in (("接了挑战（目标 ×1.5）", taken), ("没接", declined)):
        ok = [s for s in at if s["cleared"]]
        inc = sum(s["reward"] + s["unused"] + s["interest"] + s["pig"] + s["ctlIncome"] for s in ok) / max(1, len(ok))
        print(f"| {title} | {len(at)} | {wilson(len(ok), len(at))} | {inc:.1f} |")
    print()
    print("按关段的接受率：" + " / ".join(f"第 {i} 关 {100 * sum(1 for s in offered if s['stage'] == i and s.get('risk')) / max(1, sum(1 for s in offered if s['stage'] == i)):.0f}%" for i in range(2, 24, 3)))


MULTI = {"compare": None, "bosspool": bosspool, "pressure": pressure, "pool": pool, "tension": tension, "gold": gold, "lab": lab, "paired": paired, "cardwin": cardwin, "diversity": diversity}
SINGLE = {"cards": cards, "stages": stages, "bosses": bosses, "loot": loot, "usage": usage, "scen": scen}


def render(mode: str, paths: list) -> str:
    out = io.StringIO()
    with contextlib.redirect_stdout(out):
        if mode == "paired_v11":
            paired_v11(paths)
        elif mode == "v11":
            v11(paths[0])
        elif mode == "compare":
            compare(paths)
        elif mode in MULTI:
            MULTI[mode](paths)
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
    paths = sys.argv[2:]
    out = None
    if "--out" in paths:
        index = paths.index("--out")
        out = Path(paths[index + 1])
        paths = paths[:index] + paths[index + 2:]
    result = render(mode, paths)
    if out is not None:
        out.write_text(result + "\n", encoding="utf-8")
    print(result)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
