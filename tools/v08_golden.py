"""Regenerate GoldenScores.json for p0.8.0 without using the game's scoring code.

The 160 golden vectors of p0.6.0 came verbatim from the user's specification (tools/import_v06_rules.py).
p0.8.0 changes balance numbers, so their expected values change. This script holds an independent reference
scorer written from the rules. It is trusted only because step 1 below proves that, fed the frozen p0.6.0
configuration, it reproduces every expected field of all 160 authoritative vectors exactly.

    python tools/v08_golden.py verify     # step 1 only: reference vs the p0.6.0 vectors
    python tools/v08_golden.py write      # step 1, then write the p0.8.0 vectors
    python tools/v08_golden.py check      # fail if GoldenScores.json differs from what `write` produces

Input adaptation (recorded in evidence/v08/rules/golden-provenance.json): vectors that fix a C53 outcome keep the
same raw ticket, so the old outcome is mapped to the new table by position (x8 -> x5 and so on).
"""
import hashlib
import json
import sys
from collections import Counter
from fractions import Fraction
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASELINE_CONFIG = ROOT / "tools" / "balance_sim" / "baseline" / "P0Config.p0.6.0.json"
BASELINE_GOLDEN = ROOT / "tools" / "balance_sim" / "baseline" / "GoldenScores.p0.6.0.json"
CONFIG = ROOT / "DicePrototype" / "Assets" / "Game" / "Data" / "P0Config.json"
GOLDEN = ROOT / "DicePrototype" / "Assets" / "Game" / "Data" / "GoldenScores.json"
EVIDENCE = ROOT / "evidence" / "v08" / "rules" / "golden-provenance.json"

GROUP_ORDER = ["pair", "two_pair", "trips", "full_house", "three_pair", "quads", "two_trips", "five_kind", "four_pair", "six_kind", "seven_kind", "eight_kind"]
FIXED = ["C02", "C06", "C08", "C09", "C11", "C14", "C18", "C28", "C30", "C31", "C32", "C33", "C40", "C42", "C46"]
ALWAYS = {"C01", "C03", "C09", "C38", "C42", "C45", "C46", "C47", "C49", "C50", "C51", "C53"}


def has_straight(values, length):
    run, last = 0, None
    for v in sorted(set(values)):
        run = run + 1 if last is not None and v == last + 1 else 1
        if run >= length:
            return True
        last = v
    return False


def score(inp, cfg):
    ctl = {c["id"]: c for c in cfg["controllers"]}
    par = lambda cid: ctl[cid]["parameters"]  # noqa: E731
    hands = {h["id"]: h for h in cfg["handDefinitions"]}
    colors_def = {c["id"]: c for c in cfg["colorDefinitions"]}
    instances = inp["controllers"]
    by_id = {c["instanceId"]: c for c in instances}
    bindings = inp.get("bindings")
    order = [c["instanceId"] for c in instances]

    def target_of(instance_id):
        if bindings is not None:
            return bindings.get(instance_id)
        at = order.index(instance_id)
        return order[at + 1] if at + 1 < len(order) else None

    sources = []
    for emitter in instances:
        if not emitter.get("alive", True):
            continue
        owner, seen = emitter, set()
        while owner is not None and owner["definitionId"] == "C41":
            if owner["instanceId"] in seen:
                raise ValueError("mimic cycle")
            seen.add(owner["instanceId"])
            nxt = by_id.get(target_of(owner["instanceId"]))
            owner = nxt if nxt is not None and nxt.get("alive", True) else None
        sources.append({"emitter": emitter["instanceId"], "ability": owner["definitionId"] if owner else None,
                        "owner": owner["instanceId"] if owner else None, "state": (owner or {}).get("state") or {},
                        "enchant": emitter.get("enchantmentId", "none")})
    count = Counter(s["ability"] for s in sources if s["ability"])
    has = lambda cid: count[cid] > 0  # noqa: E731

    dice = inp["dice"]
    n = len(dice)
    pips = [d["p"] for d in dice]
    cols = [d["c"] for d in dice]
    changed_odd = has("C12") and any(p % 2 for p in pips)
    if has("C12"):
        pips = [p + par("C12")["oddToEvenAdd"] if p % 2 else p for p in pips]
    if has("C03"):
        cols[par("C03")["index"]] = "red"
    if inp["red_wave"]:
        cols = ["red"] * n
    red_n = cols.count("red")
    counts = Counter(pips)
    max_same = max(counts.values())
    pair_kinds = sum(1 for v in counts.values() if v >= 2)
    trip_kinds = sum(1 for v in counts.values() if v >= 3)
    matched = []
    if max_same >= 2: matched.append("pair")
    if pair_kinds >= 2: matched.append("two_pair")
    if max_same >= 3: matched.append("trips")
    if any(a >= 3 and any(kb != ka and b >= 2 for kb, b in counts.items()) for ka, a in counts.items()): matched.append("full_house")
    if pair_kinds >= 3: matched.append("three_pair")
    if max_same >= 4: matched.append("quads")
    if trip_kinds >= 2: matched.append("two_trips")
    if max_same >= 5: matched.append("five_kind")
    if pair_kinds >= 4: matched.append("four_pair")
    if max_same >= 6: matched.append("six_kind")
    if max_same >= 7: matched.append("seven_kind")
    if max_same >= 8: matched.append("eight_kind")
    levels = inp.get("levels") or {}
    color_levels = inp.get("color_levels") or {}
    mult = lambda hid: hands[hid]["baseMult100"] + hands[hid]["incrementPerLevel100"] * levels.get(hid, 0)  # noqa: E731
    chosen = max(matched, key=lambda h: (mult(h), hands[h]["structuralPriority"])) if matched else None
    group = mult(chosen) if chosen else 0
    need = par("C29")["minimumStraightLength"] if has("C29") else cfg["minStraightLength"]
    straight_hit = has_straight(pips, need)
    straight = mult("straight") if straight_hit else 0
    structural = matched + (["straight"] if straight_hit else [])
    primary = max(structural, key=lambda h: hands[h]["structuralPriority"]) if structural else "scatter"
    color_id = "all_red" if red_n == n else "all_blue" if red_n == 0 else None
    color = 100 if color_id is None else colors_def[color_id]["baseMult100"] + colors_def[color_id]["incrementPerLevel100"] * color_levels.get(color_id, 0)
    hand = max(100, group + straight)
    spread = [i for i in range(1, n - 1) if cols[i] == "blue" and cols[i - 1] == "red" and cols[i + 1] == "red"]
    cap = inp["capacity"]

    def hit(cid):
        p = par(cid)
        if cid in ALWAYS: return True
        if cid == "C02": return red_n == 0
        if cid == "C06": return len(counts) >= p["minDistinct"]
        if cid == "C07": return inp["submission_ordinal"] == p["submissionOrdinal"] and n == p["requiredDice"] and pips[p["sourceIndex"]] != pips[p["targetIndex"]]
        if cid in ("C08", "C48"): return n < cap
        if cid == "C10": return p["pip"] in counts
        if cid == "C11": return counts.get(2, 0) >= p["requiredCounts"]["2"] and counts.get(3, 0) >= p["requiredCounts"]["3"]
        if cid == "C12": return changed_odd
        if cid == "C13": return any(v % 2 for v in pips)
        if cid == "C14": return all(v % 2 for v in pips)
        if cid == "C15": return bool(spread)
        if cid == "C17": return any(v in p["pips"] for v in pips)
        if cid == "C18": return red_n == n - red_n
        if cid == "C19": return any(d["sum6"] <= p["maxPermanentSixFaceSum"] for d in dice)
        if cid == "C20": return n == cap and pips[n - 1] == p["lastActualCapacitySlotPip"]
        if cid == "C24": return any(cols[i] == "red" and pips[i] == p["redGroupKeyFrom"] for i in range(n))
        if cid == "C25": return red_n > 0
        if cid == "C26": return n == p["requiredDice"]
        if cid == "C27": return pair_kinds > 0
        if cid == "C28": return pair_kinds >= p["minDistinctPairs"]
        if cid == "C29": return has_straight(pips, p["minimumStraightLength"])
        if cid in ("C30", "C31"): return max_same >= p["minSamePip"]
        if cid == "C32": return len(counts) == p["distinctPips"]
        if cid == "C33": return n >= p["minDice"] and pips[0] - pips[1] >= p["firstMinusSecondAtLeast"]
        if cid in ("C36", "C37"): return n >= p["minDice"] and n < cap
        if cid == "C39": return p["effectiveColor"] in cols
        if cid == "C40": return n >= 3 and pips[1] > pips[0] and pips[2] - pips[1] == pips[1] - pips[0]
        if cid == "C43": return primary != "scatter" and primary == inp.get("previous_primary")
        if cid == "C44": return inp["wallet"] > 0
        return False

    triggered = [s for s in sources if s["ability"] and hit(s["ability"])]
    hits = Counter(s["ability"] for s in triggered)

    contributions = []
    for i in range(n):
        v = Fraction(pips[i])
        for s in sources:
            a = s["ability"]
            if a == "C10" and pips[i] == par(a)["pip"]: v += par(a)["perDieAdd"]
            elif a == "C13" and pips[i] % 2: v += par(a)["oddPerDieAdd"]
            elif a == "C17" and pips[i] in par(a)["pips"]: v += par(a)["perDieAdd"]
            elif a == "C19" and dice[i]["sum6"] <= par(a)["maxPermanentSixFaceSum"]: v += par(a)["perDieAdd"]
            elif a == "C20" and n == cap and i == cap - 1 and pips[i] == par(a)["lastActualCapacitySlotPip"]: v += par(a)["perDieAdd"]
            elif a == "C39" and cols[i] == par(a)["effectiveColor"]: v += par(a)["perDieAdd"]
        for s in sources:
            if s["ability"] == "C45" and s["state"].get("activeColor") == cols[i]:
                v *= par("C45")["matchingDieContributionMultiplier"]
        if dice[i]["glass"]: v *= cfg["glassContributionMultiplier"]
        if dice[i]["gold"]: v *= cfg["goldContributionMultiplier"]
        contributions.append(int(v))

    pair_add = hits["C27"] * pair_kinds * par("C27")["baseAddPerDistinctPairedPip"]
    wallet_add = inp["wallet"] * hits["C44"] * par("C44")["baseAddPerFrozenGold"]
    empty_add = hits["C48"] * (cap - n) * par("C48")["baseAddPerEmptySubmitSlot"]
    steady_add = hits["C50"] * par("C50")["baseAdd"]
    base_count = sum(1 for s in triggered if s["enchant"] == "base10")
    mult_count = sum(1 for s in triggered if s["enchant"] == "mult2")
    enchant_add = base_count * cfg["enchantRules"]["baseAdd"]
    base_total = sum(contributions) + cfg["bonusPerUnusedRoll"] * inp["rolls_remaining"] + pair_add + wallet_add + empty_add + steady_add + enchant_add

    red_keys = []
    for i in range(n):
        if cols[i] != "red":
            continue
        if has("C25"):
            red_keys.append(0)
        elif has("C24") and pips[i] == par("C24")["redGroupKeyFrom"]:
            red_keys.append(par("C24")["redGroupKeyTo"])
        else:
            red_keys.append(pips[i])
    red_count = max(Counter(red_keys).values()) if red_keys else 0
    red_table = {r["count"]: r["factor100"] for r in cfg["redRewards"]}
    red_mult = red_table[min(red_count, cfg["debug"]["redGroupCapForExperiments"])]

    alive = sum(1 for c in instances if c.get("alive", True))
    random_units = inp.get("random_units2") or {}
    x = Fraction(1)
    for s in triggered:
        a = s["ability"]
        if a in FIXED:
            x *= par(a)["scoreMultiplier"]
        elif a == "C47":
            per = par(a).get("perControllerUnits2")
            x *= alive if per is None else Fraction(max(2, alive * per), 2)
        elif a == "C49":
            x *= counts.get(5, 0)
        elif a == "C51":
            x *= Fraction(s["state"].get("turnoverUnits2") or 3, 2)
        elif a == "C53":
            x *= Fraction(random_units[s["owner"]], 2)

    requested = hits["C43"] * par("C43")["levelAddPerSource"]
    max_level = cfg["debug"]["maxHandLevel"] if inp.get("debug") else cfg["maxHandLevel"]
    plan = {
        "earnedGold": hits["C01"] * par("C01")["goldPerValidSubmission"],
        "copyFirstToSecond": hits["C07"] > 0,
        "redSpreadIndices": spread if hits["C15"] > 0 else [],
        "dyeAllFacesRed": hits["C26"] > 0,
        "growthNetDelta": hits["C36"] * par("C36")["delta"] + hits["C37"] * par("C37")["delta"],
        "growthIndices": [0, 1] if hits["C36"] + hits["C37"] > 0 else [],
        "blueSpreadIndices": ([0] if n == 1 else [0, n - 1]) if hits["C38"] > 0 else [],
        "upgradeHandId": primary if requested > 0 else None,
        "upgradeRequested": requested,
        "upgradeApplied": min(requested, max_level - levels.get(primary, 0)) if requested > 0 else 0,
        "nextPreviousPrimary": primary,
        "nextColorOwnerIds": [c["instanceId"] for c in instances if c.get("alive", True) and c["definitionId"] == "C45"],
        "randomFactorOwnerIds": [c["instanceId"] for c in instances if c.get("alive", True) and c["definitionId"] == "C53"],
        "cannonRiskEmitterIds": [s["emitter"] for s in triggered if s["ability"] == "C42"],
    }
    numerator = base_total * hand * color * red_mult * x.numerator * cfg["enchantRules"]["scoreMultiplier"] ** mult_count
    return {
        "score": str(numerator // (1000000 * x.denominator)),
        "baseTotal": str(base_total),
        "pipContributions": contributions,
        "pairKinds": pair_kinds,
        "pairBaseAdd": pair_add,
        "walletBaseAdd": str(wallet_add),
        "emptySlotBaseAdd": empty_add,
        "steadyBaseAdd": steady_add,
        "enchantBaseAdd": enchant_add,
        "chosenGroupId": chosen,
        "primaryHandId": primary,
        "totalHandMult100": hand,
        "colorId": color_id,
        "colorMult100": color,
        "redCount": red_count,
        "redMult100": red_mult,
        "controllerMultiplierNumerator": str(x.numerator),
        "controllerMultiplierDenominator": str(x.denominator),
        "triggeredSourceIds": [s["emitter"] for s in triggered],
        "enchantMultiplierCount": mult_count,
        "afterScorePlan": plan,
    }


def load(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def verify_reference():
    cfg, golden = load(BASELINE_CONFIG), load(BASELINE_GOLDEN)
    bad = []
    for case in golden:
        actual = score(case["input"], cfg)
        expected = case["expected"]
        if set(actual) != set(expected) or set(actual["afterScorePlan"]) != set(expected["afterScorePlan"]):
            bad.append((case["id"], "field set"))
            continue
        for key in expected:
            if actual[key] != expected[key]:
                bad.append((case["id"], key, expected[key], actual[key]))
    print(f"reference vs p0.6.0 vectors: {len(golden)} cases, {len(golden) - len(set(b[0] for b in bad))} exact, {len(bad)} field mismatches")
    for b in bad[:12]:
        print("  ", b)
    return not bad, len(golden)


def build_p080():
    old_cfg, cfg, golden = load(BASELINE_CONFIG), load(CONFIG), load(BASELINE_GOLDEN)
    old_units = next(c for c in old_cfg["controllers"] if c["id"] == "C53")["parameters"]["factorUnits2"]
    new_units = next(c for c in cfg["controllers"] if c["id"] == "C53")["parameters"]["factorUnits2"]
    assert len(old_units) == len(new_units)
    remapped, changed = [], 0
    out = []
    for case in golden:
        item = json.loads(json.dumps(case))
        units = item["input"].get("random_units2") or {}
        for owner, value in list(units.items()):
            units[owner] = new_units[old_units.index(value)]
            if units[owner] != value:
                remapped.append({"id": item["id"], "owner": owner, "from": value, "to": units[owner]})
        new_expected = score(item["input"], cfg)
        if new_expected != case["expected"]:
            changed += 1
        item["expected"] = new_expected
        out.append(item)
    return out, remapped, changed


def render(cases):
    return json.dumps(cases, ensure_ascii=False, indent=2) + "\n"


def main():
    mode = sys.argv[1] if len(sys.argv) > 1 else "verify"
    ok, count = verify_reference()
    if not ok:
        raise SystemExit("reference scorer does not reproduce the authoritative vectors; refusing to continue")
    if mode == "verify":
        return
    cases, remapped, changed = build_p080()
    text = render(cases)
    if mode == "check":
        same = GOLDEN.read_text(encoding="utf-8") == text
        print("golden file matches generator" if same else "golden file DIFFERS from generator")
        raise SystemExit(0 if same else 1)
    GOLDEN.write_text(text, encoding="utf-8", newline="\n")
    EVIDENCE.parent.mkdir(parents=True, exist_ok=True)
    EVIDENCE.write_text(json.dumps({
        "generator": "tools/v08_golden.py",
        "reference_validated_against": {"config": str(BASELINE_CONFIG.relative_to(ROOT)), "vectors": str(BASELINE_GOLDEN.relative_to(ROOT)), "cases": count, "all_fields_exact": True},
        "derived_from_production_code": False,
        "config_sha256": hashlib.sha256(CONFIG.read_bytes()).hexdigest(),
        "golden_sha256": hashlib.sha256(text.encode("utf-8")).hexdigest(),
        "cases": len(cases),
        "cases_with_changed_expected_values": changed,
        "c53_outcomes_remapped_by_table_position": remapped,
    }, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"wrote {GOLDEN.relative_to(ROOT)}: {len(cases)} cases, {changed} with changed expectations, {len(remapped)} C53 inputs remapped")


if __name__ == "__main__":
    main()
