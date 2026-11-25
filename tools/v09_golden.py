"""Independent reference scorer and golden vectors for p0.9.0 (docs/specs/v09/RULES_SPEC.md).

Written from the rules text only; it never reads the game's scoring code. The scoring logic of
tools/v08_golden.py (proven against the 160 authoritative vectors) is copied here and extended with the
p0.9.0 mechanisms: growth, retriggers, pool dice, hand-multiplier adds and the 50 new controllers.

    python tools/v09_golden.py verify   # legacy check: 160 vectors under the p0.8.0 and the p0.9.0 config
    python tools/v09_golden.py write    # legacy check, then write GoldenScoresV09.json and the provenance file
    python tools/v09_golden.py check    # fail if the files on disk differ from what `write` produces

Numbers come from the configuration. The only rule constants taken from the spec text are the growth unit
(multiplier = (20 + growth) / 20, section 2.2) and "last submission chance" = submissionsRemainingBefore 1.
"""
import hashlib
import json
import sys
from collections import Counter
from fractions import Fraction
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASELINE_CONFIG = ROOT / "tools" / "balance_sim" / "baseline" / "P0Config.p0.8.0.json"
BASELINE_GOLDEN = ROOT / "tools" / "balance_sim" / "baseline" / "GoldenScores.p0.8.0.json"
# p1.1.1 (W4b): this generator is frozen. It reads the frozen config of its own version and checks the frozen copy of
# its vectors; the live game config and GoldenScoresV111.json belong to tools/v111_config.py and tools/v111_golden.py.
CONFIG = ROOT / "tools" / "balance_sim" / "baseline" / "P0Config.p0.9.0.json"
GOLDEN = ROOT / "tools" / "balance_sim" / "baseline" / "GoldenScoresV09.p0.9.0.json"
EVIDENCE = ROOT / "evidence" / "v09" / "rules" / "golden-provenance.json"
SCHEMA = "v09-golden-1"

FIXED = ["C02", "C06", "C08", "C09", "C11", "C14", "C18", "C28", "C30", "C31", "C32", "C33", "C40", "C42", "C46"]
ALWAYS = {"C01", "C03", "C09", "C38", "C42", "C45", "C46", "C47", "C49", "C50", "C51", "C53"}

GROWTH_DENOMINATOR = 20
LAST_CHANCE = 1
GROWTH_MULT = ("C54", "C56", "C57", "C58", "C60", "C61", "C90", "C100")
GROWTH_BASE = ("C55", "C59", "C62")
FIXED_V09 = ("C74", "C75", "C77", "C81", "C82", "C83", "C86", "C88", "C101", "C102")
ALWAYS_V09 = set(GROWTH_MULT) | set(GROWTH_BASE) | {"C63", "C64", "C94", "C101"}
NON_SCORING_V09 = ("C78", "C79", "C95", "C96", "C97", "C98", "C99")
SCORING_V09 = tuple(f"C{i}" for i in range(54, 104) if f"C{i}" not in NON_SCORING_V09)
REPORTED_BASE_ADDS = ("C55", "C59", "C62", "C70", "C72", "C76", "C80")


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
    growth_of = lambda s: int(s["state"].get("growth") or 0)  # noqa: E731

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
    spread = [i for i in range(1, n - 1) if cols[i] == "blue" and cols[i - 1] == "red" and cols[i + 1] == "red"]
    cap = inp["capacity"]

    # p0.9.0 snapshot fields; absent in the legacy vectors (RULES_SPEC 2.1).
    pool = inp.get("pool") or []
    pool_n = len(pool)
    pool_pips = [d["p"] for d in pool]
    pool_red = sum(1 for d in pool if d["c"] == "red")
    rolls_used = inp.get("rolls_used", 0)
    chances = inp.get("submissions_remaining_before", 0)
    permanent = inp.get("permanent_dice_count", 0)

    # Red group (2.5 step 3): largest group among the submitted dice, ties to the larger key; C73 adds pool reds.
    red_key = [None] * n
    for i in range(n):
        if cols[i] != "red":
            continue
        if has("C25"):
            red_key[i] = 0
        elif has("C24") and pips[i] == par("C24")["redGroupKeyFrom"]:
            red_key[i] = par("C24")["redGroupKeyTo"]
        else:
            red_key[i] = pips[i]
    red_groups = Counter(k for k in red_key if k is not None)
    best_key = max(red_groups, key=lambda k: (red_groups[k], k)) if red_groups else None
    red_members = [i for i in range(n) if red_key[i] is not None and red_key[i] == best_key]
    red_pool = pool_red if has("C73") and red_members else 0
    red_count = len(red_members) + red_pool
    red_table = {r["count"]: r["factor100"] for r in cfg["redRewards"]}
    red_mult = red_table[min(red_count, cfg["debug"]["redGroupCapForExperiments"])]

    def sum_pairs(total):
        pairs = 0
        for v in counts:
            w = total - v
            if v < w:
                pairs += min(counts[v], counts.get(w, 0))
            elif v == w:
                pairs += counts[v] // 2
        return pairs

    def hit(cid):
        p = par(cid)
        if cid in ALWAYS or cid in ALWAYS_V09: return True
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
        # p0.9.0 (RULES_SPEC section 4)
        if cid == "C65": return n == cap
        if cid in ("C66", "C67"): return any(v in p["pips"] for v in pips)
        if cid in ("C68", "C102"): return chances == LAST_CHANCE
        if cid == "C69": return red_n > 0
        if cid in ("C70", "C72"): return pool_n > 0
        if cid == "C71": return p["pip"] in pool_pips
        if cid == "C73": return red_n > 0 and pool_red > 0
        if cid == "C74": return pool_n >= n
        if cid == "C75": return 1 <= rolls_used <= p["maxRollsUsed"]
        if cid == "C76": return rolls_used >= 2
        if cid == "C77": return inp["rolls_remaining"] == 0
        if cid == "C80": return sum_pairs(p["pairSum"]) > 0
        if cid == "C81": return primary == "scatter"
        if cid in ("C82", "C86"): return any(h in matched for h in p["handIds"])
        if cid == "C83": return straight_hit
        if cid in ("C84", "C85", "C87"): return max_same >= p["minSamePip"]
        if cid == "C88": return n >= p["minDice"] and len(counts) == n
        if cid == "C89": return any(d["gold"] for d in dice)
        if cid == "C91": return any(d.get("texture") in p["textures"] for d in dice)
        if cid == "C92": return any(d.get("mono_color") for d in dice)
        if cid == "C93": return any(d.get("mono_pip") for d in dice)
        if cid == "C103": return permanent <= p["maxOwnedPermanentDice"]
        return False

    triggered = [s for s in sources if s["ability"] and hit(s["ability"])]
    hits = Counter(s["ability"] for s in triggered)

    hand_add = sum(par(s["ability"])["handMultAdd100"] for s in triggered if s["ability"] in ("C84", "C85"))
    hand = max(100, group + straight + hand_add)

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
            elif a == "C91" and dice[i].get("texture") in par(a)["textures"]: v += par(a)["perDieAdd"]
            elif a == "C92" and dice[i].get("mono_color"): v += par(a)["perDieAdd"]
            elif a == "C94": v += dice[i].get("opposite", 0)
        for s in sources:
            if s["ability"] == "C45" and s["state"].get("activeColor") == cols[i]:
                v *= par("C45")["matchingDieContributionMultiplier"]
        for s in sources:
            if s["ability"] == "C93" and dice[i].get("mono_pip"):
                v *= par("C93")["dieContributionMultiplier"]
        if dice[i]["glass"]: v *= cfg["glassContributionMultiplier"]
        if dice[i]["gold"]: v *= cfg["goldContributionMultiplier"]
        if v.denominator != 1:
            raise ValueError("non-integer die contribution")
        contributions.append(int(v))

    retriggers = [0] * n
    for s in sources:
        a = s["ability"]
        if a == "C64":
            if par(a)["index"] < n: retriggers[par(a)["index"]] += par(a)["retriggers"]
        elif a == "C65":
            if n == cap: retriggers[n - 1] += par(a)["retriggers"]
        elif a in ("C66", "C67"):
            for i in range(n):
                if pips[i] in par(a)["pips"]: retriggers[i] += par(a)["retriggers"]
        elif a == "C68":
            if chances == LAST_CHANCE:
                for i in range(n): retriggers[i] += par(a)["retriggers"]
        elif a == "C69":
            for i in red_members: retriggers[i] += par(a)["retriggers"]
    pip_sum = sum(contributions[i] * (1 + retriggers[i]) for i in range(n))

    pair_add = hits["C27"] * pair_kinds * par("C27")["baseAddPerDistinctPairedPip"]
    wallet_add = inp["wallet"] * hits["C44"] * par("C44")["baseAddPerFrozenGold"]
    empty_add = hits["C48"] * (cap - n) * par("C48")["baseAddPerEmptySubmitSlot"]
    steady_add = hits["C50"] * par("C50")["baseAdd"]
    base_count = sum(1 for s in triggered if s["enchant"] == "base10")
    mult_count = sum(1 for s in triggered if s["enchant"] == "mult2")
    enchant_add = base_count * cfg["enchantRules"]["baseAdd"]
    new_adds = []
    for s in triggered:
        a, amount = s["ability"], 0
        if a in GROWTH_BASE: amount = growth_of(s)
        elif a == "C70": amount = pool_n * par(a)["baseAddPerPoolDie"]
        elif a == "C72": amount = sum(pool_pips) * par(a)["poolPipSumMultiplier"]
        elif a == "C76": amount = (rolls_used - 1) * par(a)["baseAddPerExtraRoll"]
        elif a == "C80": amount = sum_pairs(par(a)["pairSum"]) * par(a)["baseAddPerPair"]
        if amount:
            new_adds.append({"emitterId": s["emitter"], "abilityId": a, "amount": str(amount)})
    base_total = (pip_sum + cfg["bonusPerUnusedRoll"] * inp["rolls_remaining"] + pair_add + wallet_add + empty_add + steady_add
                  + sum(int(e["amount"]) for e in new_adds) + enchant_add)

    alive = sum(1 for c in instances if c.get("alive", True))
    random_units = inp.get("random_units2") or {}
    x = Fraction(1)
    for s in triggered:
        a = s["ability"]
        if a in FIXED or a in FIXED_V09:
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
        elif a in GROWTH_MULT:
            x *= Fraction(GROWTH_DENOMINATOR + growth_of(s), GROWTH_DENOMINATOR)
        elif a == "C63":
            x *= max(Fraction(1), Fraction(par(a)["perDieUnits10"] * permanent, 10))
        elif a == "C71":
            x *= Fraction(par(a)["stepNumerator"], par(a)["stepDenominator"]) ** pool_pips.count(par(a)["pip"])
        elif a == "C87":
            x *= Fraction(par(a)["stepNumerator"], par(a)["stepDenominator"]) ** (max_same - par(a)["minSamePip"] + 1)
        elif a == "C89":
            x *= Fraction(par(a)["stepNumerator"], par(a)["stepDenominator"]) ** sum(1 for d in dice if d["gold"])
        elif a == "C103":
            x *= par(a)["strictScoreMultiplier"] if permanent <= par(a)["strictMaxOwnedPermanentDice"] else par(a)["scoreMultiplier"]

    # Growth changes planned by this submission (4.1); only real instances grow, mimics never do.
    growth_changes = []
    for c in instances:
        if not c.get("alive", True):
            continue
        d = c["definitionId"]
        if d == "C54":
            same = bool(inp.get("previous_primary")) and primary == inp.get("previous_primary")
            growth_changes.append({"ownerInstanceId": c["instanceId"], "abilityId": d, "delta": par(d)["growthPerEvent"] if same else 0, "reset": not same})
        elif d == "C55" or (d == "C56" and red_n == 0) or (d == "C57" and red_count >= par(d)["minRedGroup"]):
            growth_changes.append({"ownerInstanceId": c["instanceId"], "abilityId": d, "delta": par(d)["growthPerEvent"], "reset": False})

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
        "v09": {
            "pipSum": str(pip_sum),
            "pipRetriggers": retriggers,
            "handMultAdd100": hand_add,
            "redPoolCount": red_pool,
            "controllerBaseAdds": new_adds,
            "growthChanges": growth_changes,
            "sourceAbilities": [[s["emitter"], s["ability"]] for s in sources],
        },
    }


def load(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def verify_legacy(cfg_path, label):
    """Every expected field of the 160 legacy vectors must be reproduced under the given configuration."""
    cfg, golden = load(cfg_path), load(BASELINE_GOLDEN)
    bad = []
    for case in golden:
        actual = score(case["input"], cfg)
        extra = actual.pop("v09")
        expected = case["expected"]
        if set(actual) != set(expected) or set(actual["afterScorePlan"]) != set(expected["afterScorePlan"]):
            bad.append((case["id"], "field set"))
            continue
        for key in expected:
            if actual[key] != expected[key]:
                bad.append((case["id"], key, expected[key], actual[key]))
        if extra["pipSum"] != str(sum(actual["pipContributions"])) or any(extra["pipRetriggers"]) or extra["handMultAdd100"] or extra["redPoolCount"] or extra["controllerBaseAdds"] or extra["growthChanges"]:
            bad.append((case["id"], "p0.9.0 mechanism active on a legacy vector"))
    exact = len(golden) - len(set(b[0] for b in bad))
    print(f"legacy check [{label}] config {cfg['configVersion']}: {len(golden)} cases, {exact} exact, {len(bad)} field mismatches")
    for b in bad[:20]:
        print("  ", b)
    return {"config": str(Path(cfg_path).relative_to(ROOT)).replace("\\", "/"), "configVersion": cfg["configVersion"],
            "vectors": str(BASELINE_GOLDEN.relative_to(ROOT)).replace("\\", "/"), "cases": len(golden), "exact": exact,
            "fieldMismatches": len(bad), "all_fields_exact": not bad}


# ---- p0.9.0 vector format -------------------------------------------------------------------------------------

def adapt(inp, cfg):
    """v09-golden-1 input -> the internal snapshot used by score()."""
    opposite = cfg["faceOpposites"]
    dice = []
    for d in inp["dice"]:
        faces = {f["faceId"]: f for f in d["faces"]}
        up = faces[d["faceId"]]
        dice.append({"p": d["pip"], "c": up["color"], "gold": up["gold"], "glass": d["glass"],
                     "sum6": sum(f["pip"] for f in d["faces"]), "texture": up["texture"],
                     "mono_color": len({f["color"] for f in d["faces"]}) == 1,
                     "mono_pip": len({f["pip"] for f in d["faces"]}) == 1,
                     "opposite": faces[opposite[d["faceId"]]]["pip"]})
    return {
        "dice": dice,
        "pool": [{"p": d["pip"], "c": d["color"]} for d in inp["pool"]],
        "capacity": inp["capacity"], "rolls_remaining": inp["rollsRemaining"], "rolls_used": inp["rollsUsed"],
        "submissions_remaining_before": inp["submissionsRemainingBefore"], "permanent_dice_count": inp["permanentDiceCount"],
        "submission_ordinal": inp["submissionOrdinal"], "wallet": inp["wallet"], "red_wave": inp["redWave"],
        "previous_primary": inp["previousPrimary"] or None,
        "controllers": [{"instanceId": c["instanceId"], "definitionId": c["definitionId"], "enchantmentId": c["enchantmentId"], "alive": True,
                         "state": {"growth": c["growth"], "turnoverUnits2": c["turnoverUnits2"], "activeColor": c["activeColor"] or None}}
                        for c in inp["controllers"]],
        "levels": {e["handId"]: e["level"] for e in inp["handLevels"]},
        "color_levels": {e["handId"]: e["level"] for e in inp["colorLevels"]},
        "random_units2": {e["ownerInstanceId"]: e["units2"] for e in inp["randomChoices"]},
        "bindings": None, "debug": False,
    }


def expected_of(result):
    extra = result["v09"]
    return {
        "score": result["score"], "baseTotal": result["baseTotal"], "pipSum": extra["pipSum"],
        "pipContributions": result["pipContributions"], "pipRetriggers": extra["pipRetriggers"],
        "primaryHandId": result["primaryHandId"], "totalHandMult100": result["totalHandMult100"], "handMultAdd100": extra["handMultAdd100"],
        "colorId": result["colorId"] or "", "colorMult100": result["colorMult100"],
        "redCount": result["redCount"], "redPoolCount": extra["redPoolCount"], "redMult100": result["redMult100"],
        "controllerMultiplierNumerator": result["controllerMultiplierNumerator"],
        "controllerMultiplierDenominator": result["controllerMultiplierDenominator"],
        "triggeredSourceIds": result["triggeredSourceIds"],
        "controllerBaseAdds": extra["controllerBaseAdds"], "growthChanges": extra["growthChanges"],
    }


def validate(case, cfg, remote_allowed):
    """Legal frozen submission (task contract plus RULES_SPEC 2.1 consistency)."""
    inp, cid = case["input"], case["id"]
    def need(cond, why):
        if not cond:
            raise ValueError(f"{cid}: illegal case: {why}")
    known = {c["id"] for c in cfg["controllers"]}
    non_submit = set(cfg["enchantRules"]["nonSubmitControllerIds"])
    hand_ids = {h["id"] for h in cfg["handDefinitions"]}
    color_ids = {c["id"] for c in cfg["colorDefinitions"]}
    n, pool_n = len(inp["dice"]), len(inp["pool"])
    need(1 <= inp["capacity"] <= cfg["maxSubmitCapacity"] and 1 <= n <= inp["capacity"], "dice count / capacity")
    ids = [d["dieId"] for d in inp["dice"]] + [d["dieId"] for d in inp["pool"]]
    need(len(ids) == len(set(ids)), "die ids")
    face_ids = [f"F0{i}" for i in range(1, 7)]
    for d in inp["dice"]:
        need([f["faceId"] for f in d["faces"]] == face_ids and d["faceId"] in face_ids, "faces")
        need(all(1 <= f["pip"] <= 6 and f["color"] in ("red", "blue") and f["texture"] in ("none", "smooth", "rough") and isinstance(f["gold"], bool) for f in d["faces"]), "face data")
        need(1 <= d["pip"] <= 6 and isinstance(d["glass"], bool), "die pip")
        need(remote_allowed or d["pip"] == d["faces"][int(d["faceId"][2]) - 1]["pip"], "current pip differs from the face pip")
    for d in inp["pool"]:
        need(1 <= d["pip"] <= 6 and d["color"] in ("red", "blue") and d["faceId"] in face_ids, "pool die")
    permanent = inp["permanentDiceCount"]
    need(0 <= permanent <= cfg["maxPermanentDice"] and n + pool_n <= cfg["maxActiveDice"], "dice totals")
    need(0 <= n + pool_n - permanent <= cfg["maxTemporaryDice"], "pool must hold every alive die that is not submitted")
    ctl = inp["controllers"]
    need(len(ctl) <= cfg["maxControllerInstances"], "controller count")
    need(len({c["instanceId"] for c in ctl}) == len(ctl) and len({c["definitionId"] for c in ctl}) == len(ctl), "duplicate controller")
    ability = []
    for i, c in enumerate(ctl):
        need(c["definitionId"] in known, "unknown controller")
        need(c["enchantmentId"] in ("none", "slotless", "base10", "mult2", "submit1", "roll1"), "enchantment")
        need(not (c["enchantmentId"] in ("base10", "mult2") and c["definitionId"] in non_submit), "score enchant on a non-scoring controller")
        need(c["growth"] >= 0 and (c["turnoverUnits2"] == 3 or c["definitionId"] == "C51") and c["turnoverUnits2"] >= 3, "dynamic state")
        need(c["activeColor"] in (("red", "blue") if c["definitionId"] == "C45" else ("",)), "activeColor")
        ability.append(c["definitionId"] if c["definitionId"] != "C41" else (ctl[i + 1]["definitionId"] if i + 1 < len(ctl) else None))
    need(not any(c["definitionId"] == "C41" and a in ("C09", "C21", "C22") for c, a in zip(ctl, ability)), "mimic of a capacity card is not covered")
    real = [c["definitionId"] for c in ctl]
    if any(d in real for d in ("C09", "C21", "C22")):
        par = {c["id"]: c["parameters"] for c in cfg["controllers"]}
        need(inp["capacity"] == cfg["initialSubmitCapacity"] + sum(par[d]["capacityDelta"] for d in ("C09", "C21", "C22") if d in real), "capacity vs capacity cards")
    rolls = cfg["totalRollsPerHand"] + sum(1 for c in ctl if c["enchantmentId"] == "roll1") + ability.count("C78")
    need(inp["rollsUsed"] >= 1 and inp["rollsRemaining"] >= 0 and inp["rollsUsed"] + inp["rollsRemaining"] >= rolls, "roll budget")
    chances = max(1, cfg["submissionsPerStage"] + sum(1 for c in ctl if c["enchantmentId"] == "submit1") + ability.count("C79") - ability.count("C101"))
    need(1 <= inp["submissionsRemainingBefore"] <= chances and inp["submissionOrdinal"] == chances - inp["submissionsRemainingBefore"], "submission budget")
    need(inp["wallet"] >= 0 and inp["previousPrimary"] in ({""} | hand_ids | {"scatter"}), "wallet / previousPrimary")
    need(all(e["handId"] in hand_ids and 1 <= e["level"] <= cfg["maxHandLevel"] for e in inp["handLevels"]), "hand levels")
    need(all(e["handId"] in color_ids and 1 <= e["level"] <= cfg["maxColorLevel"] for e in inp["colorLevels"]), "colour levels")
    owners = sorted(c["instanceId"] for c in ctl if c["definitionId"] == "C53")
    table = next(c for c in cfg["controllers"] if c["id"] == "C53")["parameters"]["factorUnits2"]
    need(sorted(e["ownerInstanceId"] for e in inp["randomChoices"]) == owners and all(e["units2"] in table for e in inp["randomChoices"]), "random choices")


def build_cases(cfg):
    std = cfg["diceDefinition"]["faces"]
    par = {c["id"]: c["parameters"] for c in cfg["controllers"]}
    names = {"r": "red", "b": "blue"}
    cases, families, remote_ids = [], [], set()
    family = [""]

    def die(pip, color=None, face=None, pips=None, colors=None, tex=None, gold=(), glass=False, remote=False):
        """One submitted die. `pip` is the current pip; `color` repaints the face that is up."""
        faces = [{"faceId": f["faceId"], "pip": f["pipValue"], "color": f["colorId"], "texture": f["texture"], "gold": f["isGold"]} for f in std]
        for i, v in enumerate(pips or []):
            faces[i]["pip"] = v
        for i, ch in enumerate(colors or ""):
            faces[i]["color"] = names[ch]
        if face is None:
            face = pip if faces[pip - 1]["pip"] == pip else 1 + [f["pip"] for f in faces].index(pip)
        if color:
            faces[face - 1]["color"] = names[color]
        for k, t in (tex or {}).items():
            faces[k - 1]["texture"] = t
        for k in gold:
            faces[k - 1]["gold"] = True
        return {"faceId": f"F0{face}", "pip": pip, "glass": glass, "faces": faces, "remote": remote}

    def dice(text):
        """'6b:gold:glass 3r:mono' -> dice. Flags: smooth, rough, gold, glass, mono (six faces of this colour), same (six faces of this pip)."""
        out = []
        for token in text.split():
            head, *flags = token.split(":")
            pip, color = int(head[0]), head[1]
            assert set(flags) <= {"smooth", "rough", "gold", "glass", "mono", "same"}, token
            out.append(die(pip, color, face=pip, pips=[pip] * 6 if "same" in flags else None, colors=color * 6 if "mono" in flags else None,
                           tex={pip: "smooth"} if "smooth" in flags else {pip: "rough"} if "rough" in flags else None,
                           gold=(pip,) if "gold" in flags else (), glass="glass" in flags))
        return out

    def add(desc, d, c="", p="", cap=None, rr=0, ru=None, srb=None, perm=None, wallet=0, wave=False, prev="", lv=None, clv=None, rnd=None, s=None, chk=None):
        d = dice(d) if isinstance(d, str) else d
        controllers = []
        for i, token in enumerate(c.split()):
            head, *mods = token.split(":")
            item = {"instanceId": f"ctl_{i + 1:02d}", "definitionId": head, "enchantmentId": "none", "growth": 0, "turnoverUnits2": 3, "activeColor": ""}
            for m in mods:
                if m.startswith("g="): item["growth"] = int(m[2:])
                elif m.startswith("t="): item["turnoverUnits2"] = int(m[2:])
                elif m in ("red", "blue"): item["activeColor"] = m
                else: item["enchantmentId"] = m
            controllers.append(item)
        real = [x["definitionId"] for x in controllers]
        ability = [x if x != "C41" else (real[i + 1] if i + 1 < len(real) else None) for i, x in enumerate(real)]
        pool = [{"pip": int(t[0]), "color": names[t[1]]} for t in p.split()]
        n = len(d)
        if cap is None:
            cap = cfg["initialSubmitCapacity"] + sum(par[x]["capacityDelta"] for x in ("C09", "C21", "C22") if x in real)
        rolls = cfg["totalRollsPerHand"] + sum(1 for x in controllers if x["enchantmentId"] == "roll1") + ability.count("C78")
        chances = max(1, cfg["submissionsPerStage"] + sum(1 for x in controllers if x["enchantmentId"] == "submit1") + ability.count("C79") - ability.count("C101"))
        srb = chances if srb is None else srb
        cid = f"N{len(cases) + 1:03d}"
        if any(x["remote"] for x in d):
            remote_ids.add(cid)
        inp = {
            "dice": [{"dieId": f"D{i + 1:03d}", "faceId": x["faceId"], "pip": x["pip"], "glass": x["glass"], "faces": x["faces"]} for i, x in enumerate(d)],
            "pool": [{"dieId": f"D{n + i + 1:03d}", "faceId": f"F0{x['pip']}", "pip": x["pip"], "color": x["color"]} for i, x in enumerate(pool)],
            "capacity": cap, "rollsRemaining": rr, "rollsUsed": rolls - rr if ru is None else ru,
            "submissionsRemainingBefore": srb, "permanentDiceCount": min(n + len(pool), cfg["maxPermanentDice"]) if perm is None else perm,
            "submissionOrdinal": chances - srb, "wallet": wallet, "redWave": wave, "previousPrimary": prev,
            "controllers": controllers,
            "handLevels": [{"handId": k, "level": v} for k, v in (lv or {}).items()],
            "colorLevels": [{"handId": k, "level": v} for k, v in (clv or {}).items()],
            "randomChoices": [{"ownerInstanceId": k, "units2": v} for k, v in (rnd or {}).items()],
        }
        case = {"id": cid, "description": desc, "input": inp}
        validate(case, cfg, cid in remote_ids)
        result = score(adapt(inp, cfg), cfg)
        case["expected"] = expected_of(result)
        # Hand-computed expectations written next to the case; a mismatch means the scorer or the arithmetic is wrong.
        for key, value in dict(chk or {}, **({"score": s} if s is not None else {})).items():
            if case["expected"][key] != value:
                raise ValueError(f"{cid} {desc}: hand-computed {key}={value!r}, reference gives {case['expected'][key]!r}")
        cases.append(case)
        families.append({"id": cid, "family": family[0], "handChecked": s is not None or bool(chk),
                         "sources": result["v09"]["sourceAbilities"], "triggered": result["triggeredSourceIds"]})

    PAIR = "3b 3b 5b 4r 2b"    # 17 pips, pair, mixed colours, one red
    BLUE = "3b 3b 5b 6b 2b"    # 19 pips, pair, all blue
    SCAT = "1r 3b 5b 6b"       # 15 pips, scatter, one red
    RED2 = "4r 4r 2b 3b 6b"    # 19 pips, pair, red group 2
    RED3 = "4r 4r 4r 2b 6b"    # 20 pips, trips, red group 3
    one = lambda owner, ability, delta, reset=False: [{"ownerInstanceId": owner, "abilityId": ability, "delta": delta, "reset": reset}]  # noqa: E731
    adds = lambda *e: [{"emitterId": a, "abilityId": b, "amount": c} for a, b, c in e]  # noqa: E731

    # ---- 4.1 growth ----
    family[0] = "4.1 growth"
    add("C54连庄 成长0且没有上一次：×1，成长归0", PAIR, "C54", s="34", chk={"growthChanges": one("ctl_01", "C54", 0, True), "controllerMultiplierNumerator": "1", "controllerMultiplierDenominator": "1", "triggeredSourceIds": ["ctl_01"]})
    add("C54 成长6、主牌型与上次相同：×1.3，成长+3", PAIR, "C54:g=6", prev="pair", s="44", chk={"growthChanges": one("ctl_01", "C54", 3), "controllerMultiplierNumerator": "13", "controllerMultiplierDenominator": "10"})
    add("C54 成长6、主牌型与上次不同：本次仍×1.3，成长归0", PAIR, "C54:g=6", prev="trips", s="44", chk={"growthChanges": one("ctl_01", "C54", 0, True)})
    add("C54 成长137、散点连庄也算：×7.85，成长+3", "1r 3b 5b", "C54:g=137", prev="scatter", s="70", chk={"growthChanges": one("ctl_01", "C54", 3)})
    add("C41复制C54：共用成长值×1.45两次，只有真实实例成长", PAIR, "C41 C54:g=9", prev="pair", s="71", chk={"growthChanges": one("ctl_02", "C54", 3), "controllerMultiplierNumerator": "841", "controllerMultiplierDenominator": "400", "triggeredSourceIds": ["ctl_01", "ctl_02"]})
    add("C55积少成多 成长0：触发但不加分，成长+2", PAIR, "C55", s="34", chk={"controllerBaseAdds": [], "growthChanges": one("ctl_01", "C55", 2), "triggeredSourceIds": ["ctl_01"]})
    add("C55 成长14：基础分+14", PAIR, "C55:g=14", s="62", chk={"controllerBaseAdds": adds(("ctl_01", "C55", "14")), "baseTotal": "31"})
    add("C55 成长137：基础分+137", PAIR, "C55:g=137", s="308")
    add("C41复制C55：两个来源各+14，只有真实实例成长", PAIR, "C41 C55:g=14", s="90", chk={"controllerBaseAdds": adds(("ctl_01", "C55", "14"), ("ctl_02", "C55", "14")), "growthChanges": one("ctl_02", "C55", 2)})
    add("C56深蓝 全蓝提交：×1.2，成长+2", BLUE, "C56:g=4", s="136", chk={"growthChanges": one("ctl_01", "C56", 2)})
    add("C56 提交含红面：×1.2，不成长", PAIR, "C56:g=4", s="40", chk={"growthChanges": []})
    add("C56 红色浪潮把蓝骰变红：不成长", BLUE, "C56:g=4", wave=True, s="159", chk={"growthChanges": []})
    add("C56 成长0全蓝：×1仍成长", BLUE, "C56", s="114", chk={"growthChanges": one("ctl_01", "C56", 2)})
    add("C56 留池有红骰不影响：仍成长", BLUE, "C56:g=4", p="1r 4r", s="136", chk={"growthChanges": one("ctl_01", "C56", 2)})
    add("C57红火 红组2颗：×1.15，不成长", RED2, "C57:g=3", s="76", chk={"growthChanges": []})
    add("C57 红组恰好3颗：成长+3", RED3, "C57:g=3", s="230", chk={"growthChanges": one("ctl_01", "C57", 3)})
    add("C57 场外红人并入后红组3颗：成长", RED2, "C73 C57:g=3", p="1r", s="109", chk={"growthChanges": one("ctl_02", "C57", 3), "redCount": 3, "redPoolCount": 1})
    add("C57 没有场外红人时留池红骰不算：不成长", RED2, "C57:g=3", p="1r", s="76", chk={"growthChanges": [], "redCount": 2, "redPoolCount": 0})
    add("C57 红运当头把三颗异点红骰并成一组：成长", "1r 4r 6r 2b 3b", "C25 C57:g=3", s="46", chk={"growthChanges": one("ctl_02", "C57", 3)})
    add("C57 成长0红组3：×1并成长", RED3, "C57", s="200", chk={"growthChanges": one("ctl_01", "C57", 3)})
    add("C58老当益壮 成长0：×1", PAIR, "C58", s="34", chk={"growthChanges": []})
    add("C58 成长8：×1.4", PAIR, "C58:g=8", s="47")
    add("C58 成长137：×7.85", PAIR, "C58:g=137", s="266")
    add("C59余勇可贾 成长0", PAIR, "C59", s="34", chk={"controllerBaseAdds": []})
    add("C59 成长12：基础分+12", PAIR, "C59:g=12", s="58", chk={"controllerBaseAdds": adds(("ctl_01", "C59", "12"))})
    add("C60千锤百炼 成长0", PAIR, "C60", s="34")
    add("C60 成长10：×1.5", PAIR, "C60:g=10", s="51")
    add("C61学无止境 成长16：×1.8", PAIR, "C61:g=16", s="61")
    add("C61 成长137", PAIR, "C61:g=137", s="266")
    add("C62熟能生巧 成长0", PAIR, "C62", s="34", chk={"controllerBaseAdds": []})
    add("C62 成长7：基础分+7", PAIR, "C62:g=7", s="48", chk={"controllerBaseAdds": adds(("ctl_01", "C62", "7"))})
    add("C63收藏家 5颗永久骰：×1.5", PAIR, "C63", s="51", chk={"controllerMultiplierNumerator": "3", "controllerMultiplierDenominator": "2"})
    add("C63 8颗永久骰：×2.4", PAIR, "C63", p="1r 2b 6b", s="81", chk={"controllerMultiplierNumerator": "12", "controllerMultiplierDenominator": "5"})
    add("C63 4颗永久骰：×1.2", "3b 3b 5b 4r", "C63", s="36")
    add("C63 3颗永久骰：0.9取最低×1，仍算触发", "3b 3b 5b", "C63", s="66", chk={"controllerMultiplierNumerator": "1", "controllerMultiplierDenominator": "1", "triggeredSourceIds": ["ctl_01"]})
    add("C63 0颗永久骰（全是临时骰）：×1", "3b 3b 5b", "C63", perm=0, s="66")
    add("C41复制C63：×1.5两次", PAIR, "C41 C63", s="76")
    add("C90玻璃心 成长0", PAIR, "C90", s="34")
    add("C90 成长15：×1.75", PAIR, "C90:g=15", s="59")
    add("C90 成长45：×3.25", PAIR, "C90:g=45", s="110")
    add("C100祭坛 成长0", PAIR, "C100", s="34")
    add("C100 成长12：×1.6", PAIR, "C100:g=12", s="54")

    # ---- 4.2 retriggers ----
    family[0] = "4.2 retrigger"
    add("C64一马当先：首骰再计1次", PAIR, "C64", s="40", chk={"pipRetriggers": [1, 0, 0, 0, 0], "pipSum": "20", "pipContributions": [3, 3, 5, 4, 2]})
    add("C64 只提交1颗", "6b", "C64", s="36", chk={"pipRetriggers": [1]})
    add("C41复制C64：首骰再计2次", PAIR, "C41 C64", s="46", chk={"pipRetriggers": [2, 0, 0, 0, 0]})
    add("C65压轴好戏 占满5栏：末骰再计2次", PAIR, "C65", s="42", chk={"pipRetriggers": [0, 0, 0, 0, 2]})
    add("C65 未占满：不触发", "3b 3b 5b 4r", "C65", s="30", chk={"triggeredSourceIds": [], "pipRetriggers": [0, 0, 0, 0]})
    add("C65 简化提交把容量降到4，提交4颗即占满", "3b 3b 5b 4r", "C09 C65", s="138", chk={"pipRetriggers": [0, 0, 0, 2]})
    add("C65 增加提交把容量升到6，提交5颗不算占满", PAIR, "C21 C65", s="34", chk={"triggeredSourceIds": []})
    add("C65 容量6提交6颗", "3b 3b 5b 4r 2b 2b", "C21 C65", s="69", chk={"pipRetriggers": [0, 0, 0, 0, 0, 2]})
    add("C65 大量提交容量7提交7颗（两对加顺子）", "3b 3b 5b 4r 2b 2b 6b", "C22 C65", s="481")
    add("C65 容量8提交8颗（四对子）", "3b 3b 5b 5b 2b 2b 6b 6b", "C21 C22 C65", s="1584", chk={"pipRetriggers": [0, 0, 0, 0, 0, 0, 0, 2]})
    add("C64加C65 容量1提交1颗：同一颗再计3次", "6b", "C64 C65", cap=1, s="72", chk={"pipRetriggers": [3]})
    add("C65加66大顺：末位6点先+15再计3次", "3b 3b 5b 4r 6b", "C20 C65", s="156", chk={"pipContributions": [3, 3, 5, 4, 21]})
    add("C66小小回声：有效1点和2点各再计1次", "1r 2b 3b 5b 6b", "C66", s="20", chk={"pipRetriggers": [1, 1, 0, 0, 0]})
    add("C66 没有1点或2点：不触发", "3b 3b 5b 4r 6b", "C66", s="42", chk={"triggeredSourceIds": []})
    add("C66 只提交1颗1点", "1r", "C66", s="4")
    add("C66加不要奇数：1点变有效2点仍再计，3点变4点不计", "1r 3b 4r 6b", "C12 C66", s="36", chk={"pipRetriggers": [1, 0, 0, 0], "triggeredSourceIds": ["ctl_01", "ctl_02"]})
    add("C67掷地有声：每颗有效6点再计1次", "6b 6b 2b 3b 4r", "C67", s="66", chk={"pipRetriggers": [1, 1, 0, 0, 0]})
    add("C67 没有6点：不触发", PAIR, "C67", s="34", chk={"triggeredSourceIds": []})
    add("C67加不要奇数：5点变有效6点后再计", "5b 5b 2b 3b 4r", "C12 C67", s="102", chk={"pipRetriggers": [1, 1, 0, 0, 0]})
    add("C68谢幕 本关最后一次提交机会：全部再计1次", PAIR, "C68", srb=1, s="68", chk={"pipRetriggers": [1, 1, 1, 1, 1]})
    add("C68 还剩2次提交机会：不触发", PAIR, "C68", srb=2, s="34", chk={"triggeredSourceIds": []})
    add("C41复制C68：全部再计2次", PAIR, "C41 C68", srb=1, s="102", chk={"pipRetriggers": [2, 2, 2, 2, 2]})
    add("C69红色共鸣：最大红组两颗各再计1次", RED2, "C69", s="94", chk={"pipRetriggers": [1, 1, 0, 0, 0]})
    add("C69 没有有效红骰：不触发", BLUE, "C69", s="114", chk={"triggeredSourceIds": []})
    add("C69 只有1颗红骰：1颗也算红组", "4r 2b 2b 5b 6b", "C69", s="46", chk={"pipRetriggers": [1, 0, 0, 0, 0], "redCount": 1})
    add("C69 两个红组数量并列：取分组键大的4点组", "1r 1r 4r 4r 6b", "C69", s="126", chk={"pipRetriggers": [0, 0, 1, 1, 0]})
    add("C69加我是状元：红1并入4点组，三颗都再计", "1r 1r 4r 6b 6b", "C24 C69", s="180", chk={"pipRetriggers": [1, 1, 1, 0, 0], "redCount": 3})
    add("C69加我是状元 单颗红1与单颗红3并列：红1的组键为4胜出", "1r 3r 5b 6b", "C24 C69", s="16", chk={"pipRetriggers": [1, 0, 0, 0]})
    add("C69 单颗红1与单颗红3并列（无我是状元）：3点组胜出", "1r 3r 5b 6b", "C69", s="18", chk={"pipRetriggers": [0, 1, 0, 0]})
    add("C69加红运当头：全部红骰同组都再计", "1r 4r 6r 2b 3b", "C25 C69", s="67", chk={"pipRetriggers": [1, 1, 1, 0, 0]})
    add("C69加红色浪潮：蓝骰视为红，最大组两颗2点再计", "2b 2b 5b 6b", "C69", wave=True, s="133", chk={"pipRetriggers": [1, 1, 0, 0]})
    add("C69加场外红人：红组数量4，但只有提交骰再计", RED2, "C73 C69", p="1r 4r 5b", s="189", chk={"pipRetriggers": [1, 1, 0, 0, 0], "redCount": 4, "redPoolCount": 2})
    add("C41复制C69：红组每颗再计2次", RED2, "C41 C69", s="122", chk={"pipRetriggers": [2, 2, 0, 0, 0]})
    add("五个再计来源叠在同一颗骰上：再计6次", "1r", "C64 C65 C66 C68 C69", cap=1, srb=1, s="14", chk={"pipRetriggers": [6]})
    add("再计6次加少就是多：(1+10)×7", "1r", "C64 C65 C66 C68 C69 C10", cap=1, srb=1, s="154", chk={"pipContributions": [11], "pipSum": "77"})
    add("C64 C65 C67 C68 叠加在不同骰上", "6b 6b 2b 3b 6b", "C64 C65 C67 C68", srb=1, s="984", chk={"pipRetriggers": [3, 2, 1, 1, 4], "pipSum": "82"})
    add("C64加少就是多：加值也被再计", "1r 2b 3b 5b 6b", "C64 C10", s="38", chk={"pipContributions": [11, 2, 3, 5, 6]})
    add("C66加小鬼当家：+6后再计", "1r 2b 3b 5b 6b", "C66 C17", s="50", chk={"pipContributions": [7, 8, 9, 5, 6]})
    add("C67加蓝色价值加就要奇数", "6b 6b 5b 3b 4r", "C67 C39 C13", s="160", chk={"pipContributions": [12, 12, 15, 13, 4]})
    add("C64加飘忽不定蓝色×3：首骰18再计", "6b 3b 4r", "C45:blue C64", s="49", chk={"pipContributions": [18, 9, 4]})
    add("C64加飘忽不定红色×3：首骰是蓝骰不乘", "6b 3b 4r", "C45:red C64", s="27", chk={"pipContributions": [6, 3, 12]})
    add("始终如一×2、玻璃×3、黄金×2后再计1次", "6b:same:glass:gold", "C93 C64", s="432", chk={"pipContributions": [72], "pipSum": "144"})
    add("C91 C92 C94 的加值都被谢幕再计", "3b:smooth 3b:mono 5b 4r 2b", "C91 C92 C94 C68", srb=1, s="212", chk={"pipContributions": [15, 17, 7, 7, 7], "pipSum": "106"})

    # ---- 4.3 pool ----
    family[0] = "4.3 pool"
    add("C70板凳深度：2颗留池骰+12", PAIR, "C70", p="1r 6b", s="58", chk={"controllerBaseAdds": adds(("ctl_01", "C70", "12"))})
    add("C70 没有留池骰：不触发", PAIR, "C70", s="34", chk={"triggeredSourceIds": [], "controllerBaseAdds": []})
    add("C70 留池5颗多于提交1颗：+30", "6b", "C70", p="1r 2b 3b 4r 5b", s="108")
    add("C70 留池里有3颗临时骰也算", PAIR, "C70", p="1r 2b 6b", perm=5, s="70")
    add("C41复制C70：两个来源各+12", PAIR, "C41 C70", p="1r 6b", s="82", chk={"controllerBaseAdds": adds(("ctl_01", "C70", "12"), ("ctl_02", "C70", "12"))})
    add("C71幕后推手：留池2颗6点×2.25", PAIR, "C71", p="6b 6b 2b", s="76", chk={"controllerMultiplierNumerator": "9", "controllerMultiplierDenominator": "4"})
    add("C71 留池没有6点：不触发", PAIR, "C71", p="5b 2b", s="34", chk={"triggeredSourceIds": []})
    add("C71 留池1颗6点：×1.5", PAIR, "C71", p="6b", s="51")
    add("C71 留池5颗6点（含2颗临时骰）：×1.5的5次方", PAIR, "C71", p="6b 6b 6b 6b 6b", s="258", chk={"controllerMultiplierNumerator": "243", "controllerMultiplierDenominator": "32"})
    add("C71 提交骰里的6点不算", "6b 6b 2b 3b 4r", "C71", p="5b", s="42", chk={"triggeredSourceIds": []})
    add("C72厚积薄发：留池点数和7的2倍", PAIR, "C72", p="1r 6b", s="62", chk={"controllerBaseAdds": adds(("ctl_01", "C72", "14"))})
    add("C72 没有留池骰：不触发", PAIR, "C72", s="34", chk={"triggeredSourceIds": []})
    add("C72 留池7颗点数和40：+80", "6b", "C72", p="6b 6b 6b 6b 6b 5b 5b", s="258")
    add("C73场外红人 留池没有红骰：不触发", RED2, "C73", p="5b 6b", s="66", chk={"triggeredSourceIds": [], "redCount": 2, "redPoolCount": 0})
    add("C73 提交骰没有红组：不触发也不并入", BLUE, "C73", p="1r 4r", s="114", chk={"triggeredSourceIds": [], "redCount": 0, "redPoolCount": 0})
    add("C73 单颗红骰加留池2颗红骰：红组3", "4r 2b 2b 5b 6b", "C73", p="1r 1r", s="95", chk={"redCount": 3, "redPoolCount": 2, "redMult100": 250})
    add("C73 留池红骰不论点数都并入", "4r 4r 4r", "C73", p="1r 1r 4r 4r 1r", s="1344", chk={"redCount": 8, "redPoolCount": 5, "redMult100": 1400})
    add("C73 提交5红加留池3红恰好到上限8", "4r 4r 4r 4r 4r", "C73", p="1r 4r 1r", s="6720", chk={"redCount": 8, "redMult100": 1400})
    add("C73 提交3红加留池7红共10颗：数量照实记为10，奖励按上限8", "4r 4r 4r", "C73", p="1r 1r 4r 4r 1r 4r 1r", s="1344", chk={"redCount": 10, "redPoolCount": 7, "redMult100": 1400})
    add("C73加我是状元：红1与红4同组再并入留池", "1r 4r 2b 3b 6b", "C24 C73", p="4r 6b", s="40", chk={"redCount": 3, "redPoolCount": 1})
    add("C73加红运当头：3颗异点红骰加留池2红", "1r 4r 6r 2b 3b", "C25 C73", p="1r 1r", s="80", chk={"redCount": 5, "redPoolCount": 2})
    add("C41复制C73：不重复并入，两个来源都算触发", RED2, "C41 C73", p="1r", s="95", chk={"redCount": 3, "redPoolCount": 1, "triggeredSourceIds": ["ctl_01", "ctl_02"]})
    add("C73加红色浪潮：提交骰全视为红，留池只数物理红面", BLUE, "C73", p="1r 5b 6b", wave=True, s="190", chk={"redCount": 3, "redPoolCount": 1})
    add("C73加首当其冲：首骰变红后并入留池2红", BLUE, "C03 C73", p="1r 4r", s="95", chk={"redCount": 3, "redPoolCount": 2})
    add("C74空城计：留池3颗不少于提交2颗", "6b 5b", "C74", p="1r 2b 3b", s="99")
    add("C74 留池与提交数量相等也触发", "6b 5b 3b", "C74", p="1r 2b 4r", s="126")
    add("C74 留池少于提交：不触发", "6b 5b 3b", "C74", p="1r 2b", s="42", chk={"triggeredSourceIds": []})
    add("C74 提交1颗且没有留池骰：不触发", "6b", "C74", s="18", chk={"triggeredSourceIds": []})
    add("C74 靠3颗临时骰凑够留池数量", "6b 5b 3b 2b", "C74", p="1r 4r 1r 4r", perm=5, s="144")
    add("C70 C71 C72 C74 同时命中：提交1颗留池9颗", "6b", "C70 C71 C72 C74", p="6b 6b 6b 5b 4r 3b 2b 1r 1r", s="3888", chk={"baseTotal": "128"})

    # ---- 4.4 roll phase ----
    family[0] = "4.4 roll"
    add("C75一锤定音 只投1次：×3（另有2次未用投掷奖励）", PAIR, "C75", ru=1, rr=2, s="138")
    add("C75 投了2次：不触发", PAIR, "C75", ru=2, rr=1, s="40", chk={"triggeredSourceIds": []})
    add("C76越挫越勇 只投1次：不触发", PAIR, "C76", ru=1, rr=2, s="46", chk={"triggeredSourceIds": [], "controllerBaseAdds": []})
    add("C76 投2次：+10", PAIR, "C76", ru=2, rr=1, s="60", chk={"controllerBaseAdds": adds(("ctl_01", "C76", "10"))})
    add("C76 投6次：+50", PAIR, "C76", ru=6, s="134", chk={"controllerBaseAdds": adds(("ctl_01", "C76", "50"))})
    add("C77孤注一掷 剩余投掷0：×2", PAIR, "C77", s="68")
    add("C77 剩余投掷1：不触发", PAIR, "C77", ru=2, rr=1, s="40", chk={"triggeredSourceIds": []})
    add("C75 C76 C77 投1剩2：只有一锤定音", PAIR, "C75 C76 C77", ru=1, rr=2, s="138", chk={"triggeredSourceIds": ["ctl_01"]})
    add("C75 C76 C77 加再给一次 投1剩3", PAIR, "C75 C76 C77 C78", ru=1, rr=3, s="156", chk={"triggeredSourceIds": ["ctl_01"]})
    add("C75 C76 C77 投2剩1：只有越挫越勇", PAIR, "C75 C76 C77", ru=2, rr=1, s="60", chk={"triggeredSourceIds": ["ctl_02"]})
    add("C75 C76 C77 投3剩0", PAIR, "C75 C76 C77", s="148", chk={"triggeredSourceIds": ["ctl_02", "ctl_03"]})
    add("C75 C76 C77 投4剩0", PAIR, "C75 C76 C77", ru=4, s="188")
    add("C75 C76 C77 投5剩1", PAIR, "C75 C76 C77", ru=5, rr=1, s="120")
    add("C75 C76 C77 投6剩0", PAIR, "C75 C76 C77", ru=6, s="268")
    add("C75 C76 C77 投3剩3", PAIR, "C75 C76 C77", ru=3, rr=3, s="92")
    add("C80天生一对：1+6与2+5两对，+28", "1r 6b 2b 5b 3b", "C80", s="45", chk={"controllerBaseAdds": adds(("ctl_01", "C80", "28"))})
    add("C80 没有和为7的一对：不触发", "1r 1r 2b 3b 3b", "C80", s="52", chk={"triggeredSourceIds": []})
    add("C80 每颗只配一次：两颗1配一颗6只算一对", "1r 1r 6b 3b 3b", "C80", s="147", chk={"controllerBaseAdds": adds(("ctl_01", "C80", "14"))})
    add("C80 两颗3配三颗4：两对", "3b 3b 4r 4r 4r", "C80", s="690")
    add("C80 8颗骰四对", "1r 6b 2b 5b 3b 4r 1r 6b", "C80", cap=8, s="1911", chk={"controllerBaseAdds": adds(("ctl_01", "C80", "56"))})
    add("C80加不要奇数：有效点数全为偶数，不可能和为7", "1r 6b 2b 5b 3b", "C12 C80", s="60", chk={"triggeredSourceIds": ["ctl_01"]})

    # ---- 4.5 hand specialists ----
    family[0] = "4.5 hand"
    add("C81散兵游勇 散点：×3", SCAT, "C81", s="45")
    add("C81 一对：不触发", PAIR, "C81", s="34", chk={"triggeredSourceIds": []})
    add("C81 只提交1颗是散点", "6b", "C81", s="54")
    add("C81加简单顺子：四连成为顺子，不再是散点", "2b 3b 4r 5b", "C29 C81", s="140", chk={"triggeredSourceIds": ["ctl_01"], "primaryHandId": "straight"})
    add("C82满堂彩 三条带一对：×3", "3b 3b 3b 5b 5b", "C82", s="1026")
    add("C82 只有三条：不触发", "3b 3b 3b 5b 6b", "C82", s="240", chk={"triggeredSourceIds": []})
    add("C82 双三条也含三条带一对", "2b 2b 2b 5b 5b 5b", "C82", cap=6, s="1890", chk={"primaryHandId": "two_trips"})
    add("C82 四条加一对也含三条带一对", "3b 3b 3b 3b 5b 5b", "C82", cap=6, s="1584", chk={"primaryHandId": "quads"})
    add("C82 五个相同不含三条带一对：不触发", "5b 5b 5b 5b 5b", "C82", s="900", chk={"triggeredSourceIds": []})
    add("C83顺水推舟 顺子：×3", "1r 2b 3b 4r 5b", "C83", s="450")
    add("C83 没有顺子：不触发", PAIR, "C83", s="34", chk={"triggeredSourceIds": []})
    add("C83加简单顺子：四连顺子也触发", "2b 3b 4r 5b", "C29 C83", s="420", chk={"triggeredSourceIds": ["ctl_01", "ctl_02"]})
    add("C83 四连在默认规则下不是顺子：不触发", "2b 3b 4r 5b", "C83", s="14", chk={"triggeredSourceIds": []})
    add("C84成双成对 一对：牌型倍率2+1.5", PAIR, "C84", s="59", chk={"totalHandMult100": 350, "handMultAdd100": 150})
    add("C84 散点：不触发", SCAT, "C84", s="15", chk={"triggeredSourceIds": [], "handMultAdd100": 0, "totalHandMult100": 100})
    add("C84 一对2级：3.5+1.5", PAIR, "C84", lv={"pair": 2}, s="85", chk={"totalHandMult100": 500})
    add("C84 三条也含对子：4+1.5", RED3, "C84", s="275", chk={"totalHandMult100": 550})
    add("C41复制C84：加值叠加为+3", PAIR, "C41 C84", s="85", chk={"totalHandMult100": 500, "handMultAdd100": 300})
    add("C85三足鼎立 三条：4+3", RED3, "C85", s="350", chk={"totalHandMult100": 700, "handMultAdd100": 300})
    add("C85 只有一对：不触发", PAIR, "C85", s="34", chk={"triggeredSourceIds": []})
    add("C84加C85 三条：4+1.5+3", RED3, "C84 C85", s="425", chk={"totalHandMult100": 850, "handMultAdd100": 450})
    add("C84加C85 三条加顺子同时成立：4+10+4.5", "1r 2b 3b 4r 5b 5b 5b", "C84 C85", cap=7, s="462", chk={"totalHandMult100": 1850})
    add("C84加C85 三条2级顺子1级：6.5+13+4.5", "1r 2b 3b 4r 5b 5b 5b", "C84 C85", cap=7, lv={"trips": 2, "straight": 1}, s="600", chk={"totalHandMult100": 2400})
    add("C84 一对加顺子：2+10+1.5", "1r 2b 3b 4r 5b 5b", "C84", cap=6, s="270", chk={"totalHandMult100": 1350})
    add("C84加C85 一对8级超过三条：计分选一对8，加值照加", RED3, "C84 C85", lv={"pair": 8}, s="625", chk={"totalHandMult100": 1250, "primaryHandId": "trips"})
    add("C86三三得九 三对子：×4", "1r 1r 2b 2b 3b 3b", "C86", cap=6, s="588")
    add("C86 双三条：×4", "2b 2b 2b 5b 5b 5b", "C86", cap=6, s="2520")
    add("C86 四对子：×4", "1r 1r 2b 2b 3b 3b 5b 5b", "C86", cap=8, s="1848")
    add("C86 只有两对：不触发", "3b 3b 5b 5b 2b", "C86", s="162", chk={"triggeredSourceIds": []})
    add("C86 双三条加一对（8颗）：只乘一次", "2b 2b 2b 5b 5b 5b 3b 3b", "C86", cap=8, s="3240")
    add("C86 六个相同不能拆成双三条：不触发", "5b 5b 5b 5b 5b 5b", "C86", cap=6, s="1620", chk={"triggeredSourceIds": []})
    add("C87得寸进尺 只有三条：不触发", "5b 5b 5b 2b 3b", "C87", s="240", chk={"triggeredSourceIds": []})
    add("C87 四同：×1.5", "5b 5b 5b 5b 2b", "C87", s="792")
    add("C87 五同：×2.25", "5b 5b 5b 5b 5b", "C87", s="2025")
    add("C87 六同：×3.375", "5b 5b 5b 5b 5b 5b", "C87", cap=6, s="5467")
    add("C87 七同：×5.0625", "5b 5b 5b 5b 5b 5b 5b", "C87", cap=7, s="14352")
    add("C87 八同：×7.59375", "5b 5b 5b 5b 5b 5b 5b 5b", "C87", cap=8, s="36450", chk={"controllerMultiplierNumerator": "243", "controllerMultiplierDenominator": "32"})
    add("C88杂牌军 4颗互不相同：×3", "1r 2b 3b 5b", "C88", s="33")
    add("C88 只有3颗互不相同：不触发", "1r 2b 5b", "C88", s="8", chk={"triggeredSourceIds": []})
    add("C88 4颗里有对子：不触发", "1r 1r 2b 3b", "C88", s="24", chk={"triggeredSourceIds": []})
    add("C88 6颗互不相同（顺子）", "1r 2b 3b 4r 5b 6b", "C88", cap=6, s="630")
    add("C88加数字多多 4颗互不相同：×9", "1r 2b 3b 5b", "C88 C06", s="99")
    add("C88加数字多多 3种点数但有对子：只有数字多多", "1r 1r 2b 3b", "C88 C06", s="73", chk={"triggeredSourceIds": ["ctl_02"]})
    add("C88加天地大同加数字多多 4颗相同：只有天地大同", "5b 5b 5b 5b", "C88 C32 C06", s="960", chk={"triggeredSourceIds": ["ctl_02"]})

    # ---- 4.6 face synergy ----
    family[0] = "4.6 face"
    add("C89金玉满堂 1颗黄金面朝上：×1.5", "6b:gold 3b 4r", "C89", s="28", chk={"pipContributions": [12, 3, 4]})
    add("C89 2颗黄金面朝上：×2.25", "6b:gold 3b:gold 4r", "C89", s="49")
    add("C89 没有黄金面：不触发", "6b 3b 4r", "C89", s="13", chk={"triggeredSourceIds": []})
    add("C89 黄金面不朝上：不触发", [die(6, gold=(1,))] + dice("3b 4r"), "C89", s="13", chk={"triggeredSourceIds": []})
    add("C91纹理大师 光滑与粗糙面各+8", "3b:smooth 3b:rough 5b 4r 2b", "C91", s="66", chk={"pipContributions": [11, 11, 5, 4, 2]})
    add("C91 纹理在没朝上的面：不触发", [die(3, tex={4: "smooth", 1: "rough"})] + dice("3b 5b 4r 2b"), "C91", s="34", chk={"triggeredSourceIds": []})
    add("C92纯色主义 整骰同色+10", "3b:mono 3b 5b 4r 2b", "C92", s="54", chk={"pipContributions": [13, 3, 5, 4, 2]})
    add("C92 六面里五面同色：不触发", [die(3, colors="bbbrbb")] + dice("3b 5b 4r 2b"), "C92", s="34", chk={"triggeredSourceIds": []})
    add("C92 红色浪潮不改变物理颜色：不触发", PAIR, "C92", wave=True, s="119", chk={"triggeredSourceIds": []})
    add("C93始终如一 整骰同点×2", "3b:same 3b 5b 4r 2b", "C93", s="40", chk={"pipContributions": [6, 3, 5, 4, 2]})
    add("C93 六面里五面同点：不触发", [die(3, pips=[3, 3, 3, 3, 3, 6])] + dice("3b 5b 4r 2b"), "C93", s="34", chk={"triggeredSourceIds": []})
    add("C41复制C93：×2两次", "3b:same 3b 5b 4r 2b", "C41 C93", s="52", chk={"pipContributions": [12, 3, 5, 4, 2]})
    add("C94表里如一 标准骰每颗贡献都是7", PAIR, "C94", s="70", chk={"pipContributions": [7, 7, 7, 7, 7]})
    add("C94 对面被随机变形成6点：3+6", [die(3, pips=[1, 2, 3, 6, 5, 6])] + dice("3b 5b 4r 2b"), "C94", s="74", chk={"pipContributions": [9, 7, 7, 7, 7]})
    add("C94 遥控骰子把F02的骰定为6点：对面仍按物理面F05的5点", [die(6, face=2, remote=True)] + dice("3b 3b"), "C94", s="150", chk={"pipContributions": [11, 7, 7]})
    add("C94加不要奇数：对面取永久点数，不受影响", "1r 3b 5b", "C12 C94", s="24", chk={"pipContributions": [8, 8, 8]})
    add("C93 遥控把整骰3点的骰定为5点：按5点×2", [die(5, face=3, pips=[3] * 6, remote=True)] + dice("3b 3b"), "C93", s="96", chk={"pipContributions": [10, 3, 3]})
    add("C91 遥控不改变朝上面的纹理", [die(6, face=2, tex={2: "rough"}, remote=True)] + dice("3b 3b"), "C91", s="120", chk={"pipContributions": [14, 3, 3]})
    add("C91 C92 C93 C94 同时作用在一颗全蓝全6粗糙骰上", [die(6, pips=[6] * 6, colors="bbbbbb", tex={6: "rough"})], "C91 C92 C93 C94", s="180", chk={"pipContributions": [60]})

    # ---- 4.8 sacrifice ----
    family[0] = "4.8 sacrifice"
    add("C101双刃剑：×4", PAIR, "C101", s="136")
    add("C41复制C101：×16（每关只剩1次提交）", PAIR, "C41 C101", s="544")
    add("C102背水一战 最后一次提交机会：×3", PAIR, "C102", srb=1, s="102")
    add("C102 还剩2次提交机会：不触发", PAIR, "C102", srb=2, s="34", chk={"triggeredSourceIds": []})
    add("C68加C102 最后一次机会", PAIR, "C68 C102", srb=1, s="204")
    add("C68加C102 剩2次：都不触发", PAIR, "C68 C102", srb=2, s="34", chk={"triggeredSourceIds": []})
    add("C101加C102 双刃剑使本关只有2次机会，第2次即最后一次", PAIR, "C101 C102", srb=1, s="408")
    add("C103精兵简政 3颗永久骰：×4", "3b 3b 5b", "C103", s="264")
    add("C103 4颗永久骰：×2", "3b 3b 5b 4r", "C103", s="60")
    add("C103 5颗永久骰：不触发", PAIR, "C103", s="34", chk={"triggeredSourceIds": []})
    add("C103 3颗永久骰加2颗临时骰提交5颗：×4", PAIR, "C103", perm=3, s="136")
    add("C103 4颗永久骰加1颗临时骰：×2", PAIR, "C103", perm=4, s="68")
    add("C103 0颗永久骰：×4", "3b 3b 5b", "C103", perm=0, s="264")
    add("C41复制C103 4颗永久骰：×4", "3b 3b 5b 4r", "C41 C103", s="120")

    # ---- enchantments on new cards ----
    family[0] = "enchant"
    add("基础分附魔在C65上，触发时+10", PAIR, "C65:base10", s="62", chk={"baseTotal": "31"})
    add("基础分附魔在C65上，未触发不加", "3b 3b 5b 4r", "C65:base10", s="30")
    add("倍率附魔在C75上，触发时×2", PAIR, "C75:mult2", ru=1, rr=2, s="276")
    add("倍率附魔在C75上，未触发不乘", PAIR, "C75:mult2", s="34")
    add("倍率附魔在C54上（总是触发）", PAIR, "C54:g=6:mult2", prev="pair", s="88", chk={"controllerMultiplierNumerator": "13", "controllerMultiplierDenominator": "10"})
    add("倍率附魔在C73上，并入成功时×2", RED2, "C73:mult2", p="1r", s="190")
    add("倍率附魔在C73上，没有留池红骰不乘", RED2, "C73:mult2", s="66")
    add("基础分附魔在C84上，触发时+10", PAIR, "C84:base10", s="94")
    add("基础分附魔在C84上，散点不加", SCAT, "C84:base10", s="15")
    add("倍率附魔在百变模仿上，复制的谢幕触发时×2", PAIR, "C41:mult2 C68", srb=1, s="204")
    add("倍率附魔在百变模仿上，复制的谢幕未触发", PAIR, "C41:mult2 C68", srb=2, s="34")
    add("基础分附魔在C70上，有留池骰", PAIR, "C70:base10", p="1r 6b", s="78")
    add("基础分附魔在C70上，没有留池骰", PAIR, "C70:base10", s="34")
    add("基础分附魔在成长0的C55上：总是触发所以+10，但成长加分为0", PAIR, "C55:base10", s="54", chk={"controllerBaseAdds": []})
    add("基础分附魔在C63上：倍率为×1也算触发", "3b 3b 5b", "C63:base10", s="126")
    add("基础分与倍率附魔同时命中两张新卡", PAIR, "C64:base10 C77:mult2", s="240", chk={"baseTotal": "30"})
    add("免槽、投掷、提交附魔在新计分卡上不改分", PAIR, "C64:slotless C76:roll1 C80:submit1", s="156", chk={"triggeredSourceIds": ["ctl_01", "ctl_02", "ctl_03"], "baseTotal": "78"})

    # ---- non-scoring new cards ----
    family[0] = "non-scoring"
    add("C78 C79 C95 C96 C97 在场：都不触发、不改分", PAIR, "C78 C79 C95 C96 C97", s="34", chk={"triggeredSourceIds": []})
    add("C98 C99 C78 带免槽/提交/投掷附魔，旁边的C64照常", PAIR, "C98:slotless C99:submit1 C78:roll1 C64", s="40", chk={"triggeredSourceIds": ["ctl_04"]})
    add("带基础分附魔的百变模仿复制C95：不触发不加分", PAIR, "C41:base10 C95", s="34", chk={"triggeredSourceIds": []})
    add("百变模仿复制C78：不触发", PAIR, "C41 C78", s="34", chk={"triggeredSourceIds": []})
    add("控制欲把非计分新卡计入件数：5件×2.5", PAIR, "C47 C95 C96 C97 C98", s="85")
    add("C79 C99 与留池卡同场：只有C70触发", PAIR, "C79 C99 C70", p="6b", s="46", chk={"triggeredSourceIds": ["ctl_03"]})

    # ---- C12 / red wave / C03 changing which new cards hit ----
    family[0] = "C12 / red wave"
    add("不要奇数使1、2变成同点：杂牌军不触发", "1r 2b 3b 5b", "C12 C88", s="28", chk={"triggeredSourceIds": ["ctl_01"]})
    add("不要奇数造出对子：散兵游勇不触发", SCAT, "C12 C81", s="36", chk={"triggeredSourceIds": ["ctl_01"]})
    add("不要奇数把5变6凑成四同：得寸进尺与三足鼎立触发", "5b 5b 6b 6b 2b", "C12 C87 C85", s="1287")
    add("不要奇数破坏顺子：顺水推舟不触发", "1r 2b 3b 4r 5b", "C12 C83", s="54", chk={"triggeredSourceIds": ["ctl_01"]})
    add("红色浪潮让全蓝三条成为红组3：红火成长", "2b 2b 2b 5b 6b", "C57:g=12", wave=True, s="544", chk={"growthChanges": one("ctl_01", "C57", 3), "redCount": 3})
    add("首当其冲造出的单颗红骰被红色共鸣再计", BLUE, "C03 C69", s="44", chk={"pipRetriggers": [1, 0, 0, 0, 0]})
    add("红色浪潮下留池蓝骰不算红：场外红人不触发", BLUE, "C73", p="5b 6b", wave=True, s="133", chk={"triggeredSourceIds": [], "redCount": 2})
    add("不要奇数加红色浪潮：C66 C67 C69 再计叠加", "1r 3b 5b 5b 6b", "C12 C66 C67 C69", wave=True, s="1240", chk={"pipRetriggers": [1, 0, 2, 2, 2], "pipSum": "62"})

    # ---- mimic ----
    family[0] = "mimic"
    add("百变模仿在最右：没有能力，不触发", PAIR, "C64 C41", s="40", chk={"triggeredSourceIds": ["ctl_01"]})
    add("C41复制C56：倍率两次，成长只记真实实例", BLUE, "C41 C56:g=4", s="164", chk={"growthChanges": one("ctl_02", "C56", 2)})
    add("C41复制C57：红组3，成长只记一次", RED3, "C41 C57:g=3", s="264", chk={"growthChanges": one("ctl_02", "C57", 3)})
    add("C41复制C71：留池2颗6点，×2.25两次", PAIR, "C41 C71", p="6b 6b", s="172")
    add("C41复制C85：三条+3两次", RED3, "C41 C85", s="500", chk={"handMultAdd100": 600})
    add("C41复制C65 占满：末骰再计4次", PAIR, "C41 C65", s="50", chk={"pipRetriggers": [0, 0, 0, 0, 4]})
    add("C41复制C94：对面点数加两次", PAIR, "C41 C94", s="106", chk={"pipContributions": [11, 11, 9, 10, 12]})
    add("C41复制C102 最后一次机会：×9", PAIR, "C41 C102", srb=1, s="306")

    # ---- large late-game builds ----
    family[0] = "large build"
    add("红色八豹子：C21 C22 C57 C69 C87 C85 C84 C31 C54 C25倍率附魔", "4r 4r 4r 4r 4r 4r 4r 4r",
        "C21 C22 C57:g=30 C69 C87 C85 C84 C31 C54:g=21 C25:mult2", prev="eight_kind", lv={"eight_kind": 5}, clv={"all_red": 8}, s="131182821",
        chk={"totalHandMult100": 10450, "pipSum": "64", "controllerMultiplierNumerator": "29889", "controllerMultiplierDenominator": "256"})
    add("留池流：提交2颗留池6颗，十张卡", "6b 5b", "C74 C70 C72 C71 C63 C08 C48 C55:g=40 C77 C81:mult2", p="6b 6b 1r 4r 2b 3b",
        clv={"all_blue": 6}, s="375580", chk={"baseTotal": "161", "controllerMultiplierNumerator": "972", "controllerMultiplierDenominator": "5"})
    add("顺子流：容量6的1到6，顺子8级", "1r 2b 3b 4r 5b 6b", "C21 C83 C88 C06 C80 C94 C64 C65 C40 C29:base10",
        lv={"straight": 8}, s="422280", chk={"baseTotal": "115", "pipSum": "63", "controllerMultiplierNumerator": "108"})
    add("骰面流：同点、纯色、黄金、玻璃、纹理加飘忽不定与谢幕", "6b:same:gold:glass 6b:mono:smooth 6b:gold 3b:rough 3b:same",
        "C89 C91 C92 C41 C93 C94 C45:blue C68 C102 C90:g=45", srb=1, lv={"full_house": 5}, clv={"all_blue": 7}, s="5010174",
        chk={"pipContributions": [864, 75, 42, 45, 72], "pipSum": "2196"})
    add("成长流：九张成长卡加控制欲，豹子7级全蓝8级", "5b 5b 5b 5b 5b", "C54:g=30 C55:g=60 C56:g=40 C58:g=24 C59:g=36 C60:g=10 C61:g=28 C62:g=45 C100:g=16 C47",
        prev="five_kind", lv={"five_kind": 7}, clv={"all_blue": 8}, s="24848208", chk={"baseTotal": "166"})
    add("模仿错乱乘积加人员流动、双刃剑、精兵简政、三对子6级", "1r 1r 2b 2b 3b 3b", "C41 C53 C51:t=7 C101 C103 C86 C27 C84", cap=6, perm=3,
        lv={"three_pair": 6}, rnd={"ctl_02": 6}, s="3259872", chk={"totalHandMult100": 2200, "controllerMultiplierNumerator": "2016"})
    add("不要奇数加红色浪潮：九张卡里只有部分命中", "1r 3b 5b 5b 6b", "C12 C67 C69 C85 C57:g=12 C13 C80 C81:mult2 C66", wave=True,
        lv={"trips": 5}, clv={"all_red": 5}, s="14787", chk={"pipRetriggers": [1, 0, 2, 2, 2], "triggeredSourceIds": ["ctl_01", "ctl_02", "ctl_03", "ctl_04", "ctl_05", "ctl_09"]})
    add("旧卡为主的两对流：容量4只投1次", "2b 2b 3b 3b", "C09 C02 C11 C28 C84 C55:g=20 C75 C50:base10 C46", ru=1, rr=2,
        lv={"two_pair": 8}, clv={"all_blue": 5}, s="4410450", chk={"baseTotal": "66", "controllerMultiplierNumerator": "972"})
    return cases, families, sorted(remote_ids)


def render(cfg, cases):
    dump = lambda o: json.dumps(o, ensure_ascii=False, separators=(",", ":"))  # noqa: E731
    lines = ['{"schema":%s,"configVersion":%s,"cases":[' % (dump(SCHEMA), dump(cfg["configVersion"]))]
    for i, c in enumerate(cases):
        lines.append('{"id":%s,"description":%s,\n "input":%s,\n "expected":%s}%s' % (
            dump(c["id"]), dump(c["description"]), dump(c["input"]), dump(c["expected"]), "," if i + 1 < len(cases) else ""))
    lines.append("]}")
    return "\n".join(lines) + "\n"


def coverage(cases, families):
    """Per scoring controller: cases where one of its sources triggered / was present without triggering."""
    table = {cid: {"mentions": 0, "hit": 0, "miss": 0} for cid in SCORING_V09 + NON_SCORING_V09}
    mentions = Counter(c["definitionId"] for case in cases for c in case["input"]["controllers"])
    for f in families:
        fired = set(f["triggered"])
        by_ability = {}
        for emitter, ability in f["sources"]:
            if ability in table:
                by_ability.setdefault(ability, []).append(emitter in fired)
        for ability, flags in by_ability.items():
            table[ability]["hit" if any(flags) else "miss"] += 1
    for cid in table:
        table[cid]["mentions"] = mentions[cid]
    problems = []
    for cid in SCORING_V09:
        if table[cid]["hit"] < 1 or (cid not in ALWAYS_V09 and table[cid]["miss"] < 1):
            problems.append(cid)
    for cid in NON_SCORING_V09:
        if table[cid]["hit"] or not table[cid]["mentions"]:
            problems.append(cid)
    return table, dict(sorted(mentions.items(), key=lambda kv: int(kv[0][1:]))), problems


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def main():
    mode = sys.argv[1] if len(sys.argv) > 1 else "verify"
    legacy = [verify_legacy(BASELINE_CONFIG, "1"), verify_legacy(CONFIG, "2")]
    if not all(r["all_fields_exact"] for r in legacy):
        raise SystemExit("legacy check failed; refusing to continue")
    if mode == "verify":
        return
    cfg = load(CONFIG)
    if cfg["configVersion"] != "p0.9.0":
        raise SystemExit(f"unexpected configuration version {cfg['configVersion']}")
    cases, families, remote_ids = build_cases(cfg)
    table, mentions, problems = coverage(cases, families)
    if problems:
        raise SystemExit(f"coverage gap: {problems}")
    text = render(cfg, cases)
    if json.loads(text)["cases"] != cases:
        raise SystemExit("rendered file does not round-trip")
    family_counts = Counter(f["family"] for f in families)
    provenance = json.dumps({
        "generator": "tools/v09_golden.py",
        "schema": SCHEMA,
        "derived_from_production_code": False,
        "rules": "docs/specs/v09/RULES_SPEC.md",
        "generator_sha256": sha256(Path(__file__)),
        "rules_sha256": sha256(ROOT / "docs" / "specs" / "v09" / "RULES_SPEC.md"),
        "baseline_config_sha256": sha256(BASELINE_CONFIG),
        "config_sha256": sha256(CONFIG),
        "legacy_vectors_sha256": sha256(BASELINE_GOLDEN),
        "golden": str(GOLDEN.relative_to(ROOT)).replace("\\", "/"),
        "golden_sha256": hashlib.sha256(text.encode("utf-8")).hexdigest(),
        "cases": len(cases),
        "cases_with_hand_computed_assertions": sum(1 for f in families if f["handChecked"]),
        "legacy_checks": legacy,
        "cases_per_family": dict(family_counts),
        "remote_control_cases": remote_ids,
        "cases_mentioning_controller": mentions,
        "new_controller_coverage": table,
    }, ensure_ascii=False, indent=2) + "\n"
    if mode == "check":
        same = GOLDEN.exists() and GOLDEN.read_text(encoding="utf-8") == text
        same_evidence = EVIDENCE.exists() and EVIDENCE.read_text(encoding="utf-8") == provenance
        print("golden file matches generator" if same else "golden file DIFFERS from generator")
        print("provenance matches generator" if same_evidence else "provenance DIFFERS from generator")
        raise SystemExit(0 if same and same_evidence else 1)
    if mode != "write":
        raise SystemExit("usage: v09_golden.py verify|write|check")
    GOLDEN.write_text(text, encoding="utf-8", newline="\n")
    EVIDENCE.parent.mkdir(parents=True, exist_ok=True)
    EVIDENCE.write_text(provenance, encoding="utf-8", newline="\n")
    print(f"wrote {GOLDEN.relative_to(ROOT)}: {len(cases)} cases, {sum(1 for f in families if f['handChecked'])} with hand-computed assertions")
    print("families:", dict(family_counts))


if __name__ == "__main__":
    main()
