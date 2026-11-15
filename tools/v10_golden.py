"""Independent reference scorer and golden vectors for p1.0.0 (docs/specs/v10/RULES_SPEC.md).

Written from the rules text only; it never reads the game's scoring code. The scoring logic of
tools/v09_golden.py (proven against the 160 + 260 authoritative vectors) is copied here and extended with
the p1.0.0 mechanisms: boss modifiers, disabled controllers, blue wave, the item multiplier, the face-change
list, goldSpent and the scoring controllers among C104-C153.

    python tools/v10_golden.py verify   # legacy checks only: 160 vectors x 3 configs, 260 vectors x 2 configs
    python tools/v10_golden.py write    # legacy checks, then write GoldenScoresV10.json and the provenance file
    python tools/v10_golden.py check    # fail if the files on disk differ from what `write` produces

Numbers come from the configuration. Rule constants taken from the spec text: the growth unit
(multiplier = (20 + growth) / 20), "last submission chance" = submissionsRemainingBefore 1, the hands that
survive SmallHandsOnly (pair, two_pair, trips), the seal ranges (1-3 / 4-6, slots 1/3/5/7), the halving
formulas and the /2 and /4 units of the multiplier cards (their parameters are named ...Units2 / ...Units4).

The readings chosen at the first hand-off (R1-R16) and the later question O1 have since been decided in the rules
text; RESOLVED_READINGS records where, OPEN_READINGS lists what is still undecided (nothing at present). Both are
copied into the provenance file.
"""
import hashlib
import json
import sys
from collections import Counter
from fractions import Fraction
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASELINE = ROOT / "tools" / "balance_sim" / "baseline"
CONFIG_P08 = BASELINE / "P0Config.p0.8.0.json"
CONFIG_P09 = BASELINE / "P0Config.p0.9.0.json"
LEGACY_VECTORS = BASELINE / "GoldenScores.p0.8.0.json"
V09_VECTORS = BASELINE / "GoldenScoresV09.p0.9.0.json"
CONFIG = ROOT / "DicePrototype" / "Assets" / "Game" / "Data" / "P0Config.json"
GOLDEN = ROOT / "DicePrototype" / "Assets" / "Game" / "Data" / "GoldenScoresV10.json"
EVIDENCE = ROOT / "evidence" / "v10" / "rules" / "golden-provenance.json"
RULES = ROOT / "docs" / "specs" / "v10" / "RULES_SPEC.md"
RULES_V09 = ROOT / "docs" / "specs" / "v09" / "RULES_SPEC.md"
SCHEMA = "v10-golden-1"
CONFIG_VERSION = "p1.0.0"

FIXED = ["C02", "C06", "C08", "C09", "C11", "C14", "C18", "C28", "C30", "C31", "C32", "C33", "C40", "C42", "C46"]
ALWAYS = {"C01", "C03", "C09", "C38", "C42", "C45", "C46", "C47", "C49", "C50", "C51", "C53"}

GROWTH_DENOMINATOR = 20
LAST_CHANCE = 1
GROWTH_MULT = ("C54", "C56", "C57", "C58", "C60", "C61", "C90", "C100")
GROWTH_BASE = ("C55", "C59", "C62")
FIXED_V09 = ("C74", "C75", "C77", "C81", "C82", "C83", "C86", "C88", "C101", "C102")
ALWAYS_V09 = set(GROWTH_MULT) | set(GROWTH_BASE) | {"C63", "C64", "C94", "C101"}

# p1.0.0 (RULES_SPEC sections 3, 5, 6)
GROWTH_MULT_V10 = ("C129", "C132", "C134")
FIXED_V10 = ("C104", "C105", "C106", "C130", "C138", "C144", "C149", "C153")
FACE_CARDS = ("C117", "C118", "C119", "C122", "C123", "C124", "C125", "C126", "C127")
ALWAYS_V10 = set(GROWTH_MULT_V10) | {"C108", "C117", "C118", "C119", "C122", "C123", "C124", "C125", "C141"}
NEW_IDS = tuple(f"C{i}" for i in range(104, 154))
SMALL_HANDS = ("pair", "two_pair", "trips")
SCORING_EFFECTS = ("SealOdd", "SealEven", "SealLow", "SealHigh", "SealRed", "SealBlue", "SealFirst", "SealAlternate",
                   "NoStraight", "SmallHandsOnly", "NoRepeatHand", "HandHalf", "ZeroLevels", "NoRed", "NoBlue",
                   "FirstHalf", "LaterHalf", "PipsOnly", "GrowthZero", "EnchantSilence")
SESSION_EFFECTS = ("RollsDelta", "RollsFixed", "SubmissionsDelta", "CapacityDelta", "TargetPercent", "MustFill", "NoItems",
                   "LoseHalfGold", "TaxPerDie", "NoReward", "GrowthFreeze", "HandDowngrade", "DisableLeftmost", "DisableRarity")
# These two always seal at least one die, so "present but changes nothing" can only hold for the score.
ALWAYS_EFFECTIVE = ("SealFirst", "SealAlternate")
ITEM_FACTOR_ID = "I09"

# Per-die lifetime values written to and read from the vectors (RULES_SPEC 5.1; any other value is illegal).
LIFETIME_PERMANENT = "permanent"
LIFETIME_TEMPORARY = "stage_temporary"

# The readings of the first hand-off (R1-R16) and where the clarified rules text now decides each of them.
RESOLVED_READINGS = {
    "R1": "3.1 sealed-dice paragraph: the retrigger count of a sealed die is still recorded (times 0). As implemented.",
    "R2": "3.1 sealed-dice paragraph: C108 reads the first die's effective pip and is not reduced by a seal. As implemented.",
    "R3": "3.1 NoStraight row, decided the other way: C29's trigger is 'the straight holds at its length', so under NoStraight C29 does "
          "not trigger and its enchantment does not fire; C83 and C142 likewise. Reference and vectors changed accordingly.",
    "R4": "Appendix A tail: C118 lists one entry per submitted die, also when the opposite face already has that colour. As implemented.",
    "R5": "Appendix A tail: the C146 +1 counts for the red group size read by C57's growth condition. As implemented.",
    "R6": "5.2 step 4 and Appendix A tail: a disabled controller plans no post-score state change; disabled C54-C57 are not listed in "
          "growthChanges. As implemented (C45 / C53 follow-ups are not golden fields).",
    "R7": "3.1 GrowthZero row: only the value used for scoring changes, growth plans are produced as usual. As implemented.",
    "R8": "3.1 GrowthZero row: growth cards are C54-C62, C90, C100, C129, C132, C134; C51, C63, C141 are not. As implemented.",
    "R9": "Appendix A tail: a disabled controller with the slotless enchantment still does not occupy an ordinary slot. As implemented.",
    "R10": "3.1 sealed-dice paragraph: face changes are produced for sealed dice as usual. As implemented.",
    "R11": "Appendix A tail: goldSpent and faceChanges[].amount are integers, globalFactors numerator / denominator are strings, "
           "faceChanges[].text is the empty string when the kind has no text. As implemented.",
    "R12": "Appendix A tail: C127's texture entry carries the source texture verbatim (none included), gold is 0 when the source is not "
           "gold. As implemented.",
    "R13": "3.1 NoRed / NoBlue paragraph: only the multipliers are fixed; colorId, redCount and the triggers of C02, C25, C69, C73, C146 "
           "are unchanged. As implemented.",
    "R14": "5.1 lifetime note: lifetime is carried by the submitted dice only, pool dice keep the p0.9.0 fields. As implemented.",
    "R15": "Appendix A tail: C130 still triggers in a boss stage whose modifier has been lifted. As implemented.",
    "R16": "Appendix A tail: C124 always lists weightDelta +1 per submitted die, the cap is applied by the session. As implemented.",
    "O1": "5.1 lifetime note, corrected in the rules text: the values are permanent / stage_temporary (the die model's values), the "
          "default is permanent and any other value is illegal. As implemented (LIFETIME_PERMANENT / LIFETIME_TEMPORARY; adapt() and "
          "validate() reject other values); no vector changed.",
}

# Points where the rules text and this reference still disagree or the text is silent. Nothing here is decided by this script.
OPEN_READINGS = {}


def has_straight(values, length):
    run, last = 0, None
    for v in sorted(set(values)):
        run = run + 1 if last is not None and v == last + 1 else 1
        if run >= length:
            return True
        last = v
    return False


def modifier_of(cfg, modifier_id):
    if not modifier_id:
        return None
    found = [m for m in cfg.get("bossModifiers", []) if m["id"] == modifier_id]
    if len(found) != 1:
        raise ValueError(f"unknown boss modifier {modifier_id!r}")
    unknown = set(found[0]["effects"]) - set(SCORING_EFFECTS) - set(SESSION_EFFECTS)
    if unknown:
        raise ValueError(f"{modifier_id}: effect codes not covered by the rules text: {sorted(unknown)}")
    return found[0]


def score(inp, cfg, drop_effect=None):
    """Score one frozen submission. `drop_effect` removes one effect code of the modifier (coverage analysis only)."""
    ctl = {c["id"]: c for c in cfg["controllers"]}
    par = lambda cid: ctl[cid]["parameters"]  # noqa: E731
    hands = {h["id"]: h for h in cfg["handDefinitions"]}
    colors_def = {c["id"]: c for c in cfg["colorDefinitions"]}
    instances = inp["controllers"]
    by_id = {c["instanceId"]: c for c in instances}
    bindings = inp.get("bindings")
    order = [c["instanceId"] for c in instances]

    # p1.0.0 snapshot fields; absent in the older vectors (RULES_SPEC 5.1 defaults).
    modifier_id = inp.get("modifier_id") or ""
    modifier = modifier_of(cfg, modifier_id)
    fx = set(modifier["effects"]) if modifier else set()
    fx.discard(drop_effect)
    disabled = set(inp.get("disabled") or ())
    stage_kind = inp.get("stage_kind") or "normal"
    hands_used = inp.get("hands_used") or []
    inventory = inp.get("inventory", 0)
    slots = inp.get("slots", 0)
    item_mult = inp.get("item_mult", 0)
    if item_mult not in (0, 2):
        raise ValueError("scoreItemMultiplier must be 0 or 2")
    if inp["red_wave"] and inp.get("blue_wave"):
        raise ValueError("red wave and blue wave cannot both be active")

    def target_of(instance_id):
        if bindings is not None:
            return bindings.get(instance_id)
        at = order.index(instance_id)
        return order[at + 1] if at + 1 < len(order) else None

    # Sources. A disabled controller has no ability; a mimic that is disabled, or whose target is disabled, has none either.
    sources = []
    for emitter in instances:
        if not emitter.get("alive", True):
            continue
        owner, seen = emitter, set()
        dead = emitter["instanceId"] in disabled
        while owner is not None and owner["definitionId"] == "C41":
            if owner["instanceId"] in seen:
                raise ValueError("mimic cycle")
            seen.add(owner["instanceId"])
            nxt = by_id.get(target_of(owner["instanceId"]))
            owner = nxt if nxt is not None and nxt.get("alive", True) else None
            if owner is not None and owner["instanceId"] in disabled:
                dead = True
        if dead:
            owner = None
        sources.append({"emitter": emitter["instanceId"], "ability": owner["definitionId"] if owner else None,
                        "owner": owner["instanceId"] if owner else None, "state": (owner or {}).get("state") or {},
                        "enchant": emitter.get("enchantmentId", "none")})
    count = Counter(s["ability"] for s in sources if s["ability"])
    has = lambda cid: count[cid] > 0  # noqa: E731
    growth_of = lambda s: int(s["state"].get("growth") or 0)  # noqa: E731
    scoring_growth = lambda s: 0 if "GrowthZero" in fx else growth_of(s)  # noqa: E731

    # 5.2 step 1: effective pips and colours.
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
    if inp.get("blue_wave"):
        cols = ["blue"] * n
    red_n = cols.count("red")

    # 5.2 step 2: hands, straight, primary hand, colour hand.
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
    if "SmallHandsOnly" in fx:
        matched = [h for h in matched if h in SMALL_HANDS]
    levels = inp.get("levels") or {}
    color_levels = inp.get("color_levels") or {}
    level = (lambda hid: 0) if "ZeroLevels" in fx else (lambda hid: levels.get(hid, 0))  # noqa: E731
    color_level = (lambda cid: 0) if "ZeroLevels" in fx else (lambda cid: color_levels.get(cid, 0))  # noqa: E731
    mult = lambda hid: hands[hid]["baseMult100"] + hands[hid]["incrementPerLevel100"] * level(hid)  # noqa: E731
    chosen = max(matched, key=lambda h: (mult(h), hands[h]["structuralPriority"])) if matched else None
    group = mult(chosen) if chosen else 0
    need = par("C29")["minimumStraightLength"] if has("C29") else cfg["minStraightLength"]
    straight_hit = has_straight(pips, need) and "NoStraight" not in fx
    straight = mult("straight") if straight_hit else 0
    structural = matched + (["straight"] if straight_hit else [])
    primary = max(structural, key=lambda h: hands[h]["structuralPriority"]) if structural else "scatter"
    color_id = "all_red" if red_n == n else "all_blue" if red_n == 0 else None
    color = 100 if color_id is None else colors_def[color_id]["baseMult100"] + colors_def[color_id]["incrementPerLevel100"] * color_level(color_id)
    if (color_id == "all_red" and "NoRed" in fx) or (color_id == "all_blue" and "NoBlue" in fx):
        color = 100
    spread = [i for i in range(1, n - 1) if cols[i] == "blue" and cols[i - 1] == "red" and cols[i + 1] == "red"]
    cap = inp["capacity"]

    # p0.9.0 snapshot fields; absent in the legacy vectors.
    pool = inp.get("pool") or []
    pool_n = len(pool)
    pool_pips = [d["p"] for d in pool]
    pool_red = sum(1 for d in pool if d["c"] == "red")
    rolls_used = inp.get("rolls_used", 0)
    chances = inp.get("submissions_remaining_before", 0)
    permanent = inp.get("permanent_dice_count", 0)
    ordinal = inp["submission_ordinal"]
    wallet = inp["wallet"]

    # 5.2 step 3: red group. Largest group among the submitted dice, ties to the larger key; C73 adds pool reds, C146 adds one.
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
    red_bonus = 1 if has("C146") and red_members else 0
    red_count = len(red_members) + red_pool + red_bonus
    red_table = {r["count"]: r["factor100"] for r in cfg["redRewards"]}
    red_mult = 100 if "NoRed" in fx else red_table[min(red_count, cfg["debug"]["redGroupCapForExperiments"])]

    # Seals (3.1): the die still takes part in hands, colours, red group, counts and positions.
    sealed = []
    for i in range(n):
        sealed.append(("SealOdd" in fx and pips[i] % 2 == 1) or ("SealEven" in fx and pips[i] % 2 == 0)
                      or ("SealLow" in fx and pips[i] <= 3) or ("SealHigh" in fx and pips[i] >= 4)
                      or ("SealRed" in fx and cols[i] == "red") or ("SealBlue" in fx and cols[i] == "blue")
                      or ("SealFirst" in fx and i == 0) or ("SealAlternate" in fx and i % 2 == 0))

    def sum_pairs(total):
        pairs = 0
        for v in counts:
            w = total - v
            if v < w:
                pairs += min(counts[v], counts.get(w, 0))
            elif v == w:
                pairs += counts[v] // 2
        return pairs

    temp_n = sum(1 for d in dice if d.get("temporary"))
    occupying = sum(1 for c in instances if c.get("alive", True) and c.get("enchantmentId", "none") != "slotless")
    empty_slots = max(0, slots - occupying)
    common_n = (sum(1 for c in instances if c.get("alive", True) and ctl[c["definitionId"]]["rarity"] == par("C152")["rarity"])
                if has("C152") else 0)

    # C138 (6.9): sources are judged left to right against the frozen wallet, each hit reserves the cost.
    gold_hit, budget = set(), wallet
    for s in sources:
        if s["ability"] == "C138" and budget >= par("C138")["goldCost"]:
            gold_hit.add(s["emitter"])
            budget -= par("C138")["goldCost"]

    def hit(cid):
        p = par(cid)
        if cid in ALWAYS or cid in ALWAYS_V09 or cid in ALWAYS_V10: return True
        if cid == "C02": return red_n == 0
        if cid == "C06": return len(counts) >= p["minDistinct"]
        if cid == "C07": return ordinal == p["submissionOrdinal"] and n == p["requiredDice"] and pips[p["sourceIndex"]] != pips[p["targetIndex"]]
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
        if cid == "C29": return has_straight(pips, p["minimumStraightLength"]) and "NoStraight" not in fx  # 3.1: "the straight holds at its length"
        if cid in ("C30", "C31"): return max_same >= p["minSamePip"]
        if cid == "C32": return len(counts) == p["distinctPips"]
        if cid == "C33": return n >= p["minDice"] and pips[0] - pips[1] >= p["firstMinusSecondAtLeast"]
        if cid in ("C36", "C37"): return n >= p["minDice"] and n < cap
        if cid == "C39": return p["effectiveColor"] in cols
        if cid == "C40": return n >= 3 and pips[1] > pips[0] and pips[2] - pips[1] == pips[1] - pips[0]
        if cid == "C43": return primary != "scatter" and primary == inp.get("previous_primary")
        if cid == "C44": return wallet > 0
        # p0.9.0 (v09 RULES_SPEC section 4)
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
        # p1.0.0 (RULES_SPEC sections 6 and 6.9)
        if cid == "C104": return n >= p["minDice"] and all(cols[i] != cols[i + 1] for i in range(n - 1))
        if cid == "C105": return 0 < red_n < n and all(c == "red" for c in cols[:red_n])
        if cid == "C106": return n >= p["minDice"] and pips == pips[::-1]
        if cid == "C107": return any(pips[i] == pips[i - 1] + 1 for i in range(1, n))
        if cid == "C109": return n % 2 == 1 and n >= p["minDice"]
        if cid == "C126": return pool_n > 0
        if cid == "C127": return ordinal == p["submissionOrdinal"] and n >= p["minDice"]
        if cid == "C130": return stage_kind == "boss"
        if cid == "C135": return inventory > 0
        if cid == "C137": return temp_n > 0
        if cid == "C139": return wallet >= p["goldPerStep"]
        if cid == "C142": return straight_hit
        if cid == "C143": return any(v == p["requiredDice"] for v in counts.values())
        if cid == "C144": return all(v % 2 == 0 for v in pips)
        if cid == "C146": return bool(red_members)
        if cid == "C147": return pool_n > 0
        if cid == "C148": return inp["rolls_remaining"] >= 1
        if cid == "C149": return n == p["requiredDice"]
        if cid == "C150": return ordinal >= 1
        if cid == "C151": return empty_slots > 0
        if cid == "C152": return common_n >= 1
        if cid == "C153": return ordinal == p["submissionOrdinal"]
        return False

    # 5.2 step 4: which sources trigger. EnchantSilence does not change this.
    triggered = [s for s in sources if s["ability"] and (s["emitter"] in gold_hit if s["ability"] == "C138" else hit(s["ability"]))]
    hits = Counter(s["ability"] for s in triggered)
    pips_only = "PipsOnly" in fx

    hand_add = sum(par(s["ability"])["handMultAdd100"] for s in triggered if s["ability"] in ("C84", "C85", "C142"))
    hand_before = max(100, group + straight + hand_add)
    hand = hand_before
    if "NoRepeatHand" in fx and primary != "scatter" and primary in hands_used:
        hand = 100
    if "HandHalf" in fx:
        hand = max(100, hand // 2)

    # 5.2 step 5: per-die contributions.
    contributions = []
    for i in range(n):
        v = Fraction(pips[i])
        if not pips_only:
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
                elif a == "C107" and i >= 1 and pips[i] == pips[i - 1] + 1: v += par(a)["perDieAdd"]
                elif a == "C137" and dice[i].get("temporary"): v += par(a)["perDieAdd"]
            for s in sources:
                if s["ability"] == "C45" and s["state"].get("activeColor") == cols[i]:
                    v *= par("C45")["matchingDieContributionMultiplier"]
            for s in sources:
                if s["ability"] == "C93" and dice[i].get("mono_pip"):
                    v *= par("C93")["dieContributionMultiplier"]
            if dice[i]["glass"]: v *= cfg["glassContributionMultiplier"]
            if dice[i]["gold"]: v *= cfg["goldContributionMultiplier"]
        if sealed[i]:
            v = Fraction(0)
        if v.denominator != 1:
            raise ValueError("non-integer die contribution")
        contributions.append(int(v))

    # 5.2 step 6: retriggers.
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
        elif a == "C109":
            if n % 2 == 1 and n >= par(a)["minDice"]: retriggers[(n - 1) // 2] += par(a)["retriggers"]
        elif a == "C143":
            for i in range(n):
                if counts[pips[i]] == par(a)["requiredDice"]: retriggers[i] += par(a)["retriggers"]
    pip_sum = sum(contributions[i] * (1 + retriggers[i]) for i in range(n))

    # 5.2 step 7: base total.
    silent = "EnchantSilence" in fx
    pair_add = 0 if pips_only else hits["C27"] * pair_kinds * par("C27")["baseAddPerDistinctPairedPip"]
    wallet_add = 0 if pips_only else wallet * hits["C44"] * par("C44")["baseAddPerFrozenGold"]
    empty_add = 0 if pips_only else hits["C48"] * (cap - n) * par("C48")["baseAddPerEmptySubmitSlot"]
    steady_add = 0 if pips_only else hits["C50"] * par("C50")["baseAdd"]
    base_count = 0 if silent or pips_only else sum(1 for s in triggered if s["enchant"] == "base10")
    mult_count = 0 if silent else sum(1 for s in triggered if s["enchant"] == "mult2")
    enchant_add = base_count * cfg["enchantRules"]["baseAdd"]
    new_adds = []
    for s in triggered:
        a, amount = s["ability"], 0
        if pips_only: break
        if a in GROWTH_BASE: amount = scoring_growth(s)
        elif a == "C70": amount = pool_n * par(a)["baseAddPerPoolDie"]
        elif a == "C72": amount = sum(pool_pips) * par(a)["poolPipSumMultiplier"]
        elif a == "C76": amount = (rolls_used - 1) * par(a)["baseAddPerExtraRoll"]
        elif a == "C80": amount = sum_pairs(par(a)["pairSum"]) * par(a)["baseAddPerPair"]
        elif a == "C108": amount = pips[par(a)["index"]] * par(a)["baseAddPerPip"]
        elif a == "C135": amount = inventory * par(a)["baseAddPerItem"]
        if amount:
            new_adds.append({"emitterId": s["emitter"], "abilityId": a, "amount": str(amount)})
    base_total = (pip_sum + cfg["bonusPerUnusedRoll"] * inp["rolls_remaining"] + pair_add + wallet_add + empty_add + steady_add
                  + sum(int(e["amount"]) for e in new_adds) + enchant_add)

    # 5.2 step 9: controller multipliers, left to right.
    alive = sum(1 for c in instances if c.get("alive", True))
    random_units = inp.get("random_units2") or {}
    x = Fraction(1)
    for s in triggered:
        a = s["ability"]
        if a in FIXED or a in FIXED_V09 or a in FIXED_V10:
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
        elif a in GROWTH_MULT or a in GROWTH_MULT_V10:
            x *= Fraction(GROWTH_DENOMINATOR + scoring_growth(s), GROWTH_DENOMINATOR)
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
        elif a == "C139":
            x *= Fraction(4 + min(par(a)["maxSteps"], wallet // par(a)["goldPerStep"]) * par(a)["stepUnits4"], 4)
        elif a == "C141":
            x *= Fraction(par(a)["initialMultiplierUnits2"] - growth_of(s), 2)
        elif a == "C147":
            x *= Fraction(4 + pool_n * par(a)["stepUnits4"], 4)
        elif a == "C148":
            x *= Fraction(2 + inp["rolls_remaining"] * par(a)["perRollUnits2"], 2)
        elif a == "C150":
            x *= ordinal + 1
        elif a == "C151":
            x *= Fraction(2 + empty_slots * par(a)["perSlotUnits2"], 2)
        elif a == "C152":
            x *= Fraction(4 + common_n * par(a)["stepUnits4"], 4)

    # Factors outside the controllers (appendix A): the item, then the boss halving.
    global_factors = []
    if item_mult:
        global_factors.append({"emitterId": "ITEM", "abilityId": ITEM_FACTOR_ID, "numerator": str(item_mult), "denominator": "1"})
    if ("FirstHalf" in fx and ordinal == 0) or ("LaterHalf" in fx and ordinal >= 1):
        global_factors.append({"emitterId": "BOSS", "abilityId": modifier_id, "numerator": "1", "denominator": "2"})
    g = Fraction(1)
    for f in global_factors:
        g *= Fraction(int(f["numerator"]), int(f["denominator"]))

    # Growth changes planned by this submission; only real, enabled instances grow, mimics never do.
    growth_changes = []
    for c in instances:
        if not c.get("alive", True) or c["instanceId"] in disabled:
            continue
        d = c["definitionId"]
        if d == "C54":
            same = bool(inp.get("previous_primary")) and primary == inp.get("previous_primary")
            growth_changes.append({"ownerInstanceId": c["instanceId"], "abilityId": d, "delta": par(d)["growthPerEvent"] if same else 0, "reset": not same})
        elif d == "C55" or (d == "C56" and red_n == 0) or (d == "C57" and red_count >= par(d)["minRedGroup"]):
            growth_changes.append({"ownerInstanceId": c["instanceId"], "abilityId": d, "delta": par(d)["growthPerEvent"], "reset": False})

    # 5.3 face changes, by triggered source from left to right; all values come from the frozen six-face data.
    face_changes = []
    if n and all("faces" in d for d in dice):
        up = [next(f for f in d["faces"] if f["faceId"] == d["face_id"]) for d in dice]

        def change(s, die_id, face_id, kind, amount=0, text=""):
            face_changes.append({"sourceId": s["ability"], "emitterId": s["emitter"], "dieId": die_id, "faceId": face_id,
                                 "kind": kind, "amount": amount, "text": text})

        for s in triggered:
            a = s["ability"]
            if a not in FACE_CARDS:
                continue
            p = par(a)
            if a == "C117":
                change(s, dice[n - 1]["die_id"], up[n - 1]["faceId"], "gold", 1)
            elif a == "C118":
                for i in range(n):
                    change(s, dice[i]["die_id"], cfg["faceOpposites"][up[i]["faceId"]], "color", 0, up[i]["color"])
            elif a == "C119":
                i = p["index"]
                for f in sorted(dice[i]["faces"], key=lambda f: f["faceId"]):
                    if f["pip"] != up[i]["pip"]:
                        change(s, dice[i]["die_id"], f["faceId"], "pipSet", up[i]["pip"])
                        break
            elif a == "C122":
                for i in range(n):
                    if up[i]["pip"] != p["pip"]:
                        change(s, dice[i]["die_id"], up[i]["faceId"], "pipDelta", 1 if up[i]["pip"] < p["pip"] else -1)
            elif a == "C123":
                for i in range(n):
                    if up[i]["pip"] not in p["pips"]:
                        change(s, dice[i]["die_id"], up[i]["faceId"], "pipDelta", 1 if up[i]["pip"] < min(p["pips"]) else -1)
            elif a == "C124":
                for i in range(n):
                    change(s, dice[i]["die_id"], up[i]["faceId"], "weightDelta", p["delta"])
            elif a == "C125":
                for i in ([0] if n == 1 else [0, n - 1]):
                    if up[i]["texture"] == "none":
                        change(s, dice[i]["die_id"], up[i]["faceId"], "texture", 0, p["textures"][0])
            elif a == "C126":
                for d in pool:
                    change(s, d["die_id"], d["face_id"], "pipDelta", p["delta"])
            elif a == "C127":
                src, die_id, face_id = up[p["sourceIndex"]], dice[n - 1]["die_id"], up[n - 1]["faceId"]
                change(s, die_id, face_id, "pipSet", src["pip"])
                change(s, die_id, face_id, "color", 0, src["color"])
                change(s, die_id, face_id, "texture", 0, src["texture"])
                change(s, die_id, face_id, "gold", 1 if src["gold"] else 0)

    requested = hits["C43"] * par("C43")["levelAddPerSource"]
    max_level = cfg["debug"]["maxHandLevel"] if inp.get("debug") else cfg["maxHandLevel"]
    real = lambda d: [c["instanceId"] for c in instances if c.get("alive", True) and c["instanceId"] not in disabled and c["definitionId"] == d]  # noqa: E731
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
        "nextColorOwnerIds": real("C45"),
        "randomFactorOwnerIds": real("C53"),
        "cannonRiskEmitterIds": [s["emitter"] for s in triggered if s["ability"] == "C42"],
    }
    # 5.2 step 9: one floor at the very end.
    numerator = base_total * hand * color * red_mult * x.numerator * cfg["enchantRules"]["scoreMultiplier"] ** mult_count * g.numerator
    return {
        "score": str(numerator // (1000000 * x.denominator * g.denominator)),
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
        "v10": {
            "sealedDice": sealed,
            "handMultBeforeModifier100": hand_before,
            "redBonusCount": red_bonus,
            "globalFactors": global_factors,
            "goldSpent": par("C138")["goldCost"] * len(gold_hit) if gold_hit else 0,
            "faceChanges": face_changes,
        },
    }


def load(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def rel(path):
    return str(Path(path).relative_to(ROOT)).replace("\\", "/")


def neutral_v10(result):
    """True when every p1.0.0 addition is inactive (the older vectors must not see any of them)."""
    more = result["v10"]
    return (not any(more["sealedDice"]) and more["handMultBeforeModifier100"] == result["totalHandMult100"] and more["redBonusCount"] == 0
            and more["globalFactors"] == [] and more["goldSpent"] == 0 and more["faceChanges"] == [])


def verify_legacy(cfg_path, label):
    """Legacy check A: every expected field of the 160 legacy vectors must be reproduced under the given configuration."""
    cfg, golden = load(cfg_path), load(LEGACY_VECTORS)
    bad = []
    for case in golden:
        actual = score(case["input"], cfg)
        extra = actual.pop("v09")
        more = actual.pop("v10")
        expected = case["expected"]
        if set(actual) != set(expected) or set(actual["afterScorePlan"]) != set(expected["afterScorePlan"]):
            bad.append((case["id"], "field set"))
            continue
        for key in expected:
            if actual[key] != expected[key]:
                bad.append((case["id"], key, expected[key], actual[key]))
        if extra["pipSum"] != str(sum(actual["pipContributions"])) or any(extra["pipRetriggers"]) or extra["handMultAdd100"] or extra["redPoolCount"] or extra["controllerBaseAdds"] or extra["growthChanges"]:
            bad.append((case["id"], "p0.9.0 mechanism active on a legacy vector"))
        if not neutral_v10(dict(actual, v10=more)):
            bad.append((case["id"], "p1.0.0 mechanism active on a legacy vector"))
    exact = len(golden) - len(set(b[0] for b in bad))
    print(f"legacy check A [{label}] config {cfg['configVersion']}: {len(golden)} cases, {exact} exact, {len(bad)} field mismatches")
    for b in bad[:20]:
        print("  ", b)
    return {"check": "A", "config": rel(cfg_path), "configVersion": cfg["configVersion"], "vectors": rel(LEGACY_VECTORS),
            "cases": len(golden), "exact": exact, "fieldMismatches": len(bad), "all_fields_exact": not bad}


def verify_v09(cfg_path, label):
    """Legacy check B: every expected field of the 260 p0.9.0 vectors, with every p1.0.0 addition neutral."""
    cfg, golden = load(cfg_path), load(V09_VECTORS)
    bad = []
    for case in golden["cases"]:
        result = score(adapt(case["input"], cfg), cfg)
        actual, expected = expected_v09(result), case["expected"]
        if set(actual) != set(expected):
            bad.append((case["id"], "field set"))
            continue
        for key in expected:
            if actual[key] != expected[key]:
                bad.append((case["id"], key, expected[key], actual[key]))
        if not neutral_v10(result):
            bad.append((case["id"], "p1.0.0 mechanism active on a p0.9.0 vector"))
    cases = len(golden["cases"])
    exact = cases - len(set(b[0] for b in bad))
    print(f"legacy check B [{label}] config {cfg['configVersion']}: {cases} cases, {exact} exact, {len(bad)} field mismatches")
    for b in bad[:20]:
        print("  ", b)
    return {"check": "B", "config": rel(cfg_path), "configVersion": cfg["configVersion"], "vectors": rel(V09_VECTORS),
            "cases": cases, "exact": exact, "fieldMismatches": len(bad), "all_fields_exact": not bad}


# ---- vector format (v09-golden-1 and v10-golden-1) ---------------------------------------------------------------

def adapt(inp, cfg):
    """v09-golden-1 / v10-golden-1 input -> the internal snapshot used by score(). The p1.0.0 fields default as in 5.1."""
    opposite = cfg["faceOpposites"]
    dice = []
    for d in inp["dice"]:
        faces = {f["faceId"]: f for f in d["faces"]}
        up = faces[d["faceId"]]
        if d.get("lifetime", LIFETIME_PERMANENT) not in (LIFETIME_PERMANENT, LIFETIME_TEMPORARY):
            raise ValueError(f"unknown die lifetime {d['lifetime']!r}")
        dice.append({"p": d["pip"], "c": up["color"], "gold": up["gold"], "glass": d["glass"],
                     "sum6": sum(f["pip"] for f in d["faces"]), "texture": up["texture"],
                     "mono_color": len({f["color"] for f in d["faces"]}) == 1,
                     "mono_pip": len({f["pip"] for f in d["faces"]}) == 1,
                     "opposite": faces[opposite[d["faceId"]]]["pip"],
                     "die_id": d["dieId"], "face_id": d["faceId"], "faces": d["faces"],
                     "temporary": d.get("lifetime", LIFETIME_PERMANENT) == LIFETIME_TEMPORARY})
    return {
        "dice": dice,
        "pool": [{"p": d["pip"], "c": d["color"], "die_id": d["dieId"], "face_id": d["faceId"]} for d in inp["pool"]],
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
        "stage_kind": inp.get("stageKind") or "normal", "modifier_id": inp.get("modifierId") or "",
        "disabled": list(inp.get("disabledControllerIds") or []), "hands_used": list(inp.get("stageHandsUsed") or []),
        "inventory": inp.get("inventoryCount", 0), "slots": inp.get("ordinaryControllerSlots", 0),
        "blue_wave": bool(inp.get("blueWave", False)), "item_mult": inp.get("scoreItemMultiplier", 0),
    }


def expected_v09(result):
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


def expected_of(result):
    more = result["v10"]
    return dict(expected_v09(result), sealedDice=more["sealedDice"], handMultBeforeModifier100=more["handMultBeforeModifier100"],
                redBonusCount=more["redBonusCount"], globalFactors=more["globalFactors"], goldSpent=more["goldSpent"],
                faceChanges=more["faceChanges"])


def disabled_by_modifier(cfg, controllers, modifier_id):
    """2.3 step 3: the controllers a Disable* modifier switches off, in controller order."""
    modifier = modifier_of(cfg, modifier_id)
    if modifier is None:
        return []
    rarity = {c["id"]: c["rarity"] for c in cfg["controllers"]}
    out = set()
    if "DisableLeftmost" in modifier["effects"]:
        out |= {c["instanceId"] for c in controllers[:modifier["amount"]]}
    if "DisableRarity" in modifier["effects"]:
        out |= {c["instanceId"] for c in controllers if rarity[c["definitionId"]] == modifier["filter"]}
    return [c["instanceId"] for c in controllers if c["instanceId"] in out]


def stage_budgets(cfg, controllers, modifier_id, disabled):
    """Session rules that decide which snapshots are legal (2.3 step 7, the session codes of 3.1, section 8 "失效与预算").

    Returns rolls per hand, submissions per stage, the submit capacity and whether the capacity is pinned by a card or modifier.
    """
    par = {c["id"]: c["parameters"] for c in cfg["controllers"]}
    modifier = modifier_of(cfg, modifier_id)
    fx = set(modifier["effects"]) if modifier else set()
    amount = modifier["amount"] if modifier else 0
    live = [c for c in controllers if c["instanceId"] not in disabled]
    ability = []
    for i, c in enumerate(controllers):
        a = c["definitionId"]
        if a == "C41":
            nxt = controllers[i + 1] if i + 1 < len(controllers) else None
            a = nxt["definitionId"] if nxt is not None and nxt["instanceId"] not in disabled else None
        ability.append(None if c["instanceId"] in disabled else a)
    enchants = lambda e: 0 if "EnchantSilence" in fx else sum(1 for c in live if c["enchantmentId"] == e)  # noqa: E731
    if "RollsFixed" in fx:
        rolls = amount
    else:
        rolls = max(1, cfg["totalRollsPerHand"] + enchants("roll1") + ability.count("C78") + (amount if "RollsDelta" in fx else 0))
    chances = max(1, cfg["submissionsPerStage"] + enchants("submit1") + ability.count("C79") - ability.count("C101")
                  + (amount if "SubmissionsDelta" in fx else 0))
    cards = [d for d in ("C09", "C21", "C22") if d in [c["definitionId"] for c in live]]
    capacity = min(cfg["maxSubmitCapacity"], max(1, cfg["initialSubmitCapacity"] + sum(par[d]["capacityDelta"] for d in cards)
                                                 + (amount if "CapacityDelta" in fx else 0)))
    return rolls, chances, capacity, bool(cards) or "CapacityDelta" in fx


def validate(case, cfg, remote_allowed, synthetic_disabled):
    """Legal frozen submission (task contract, v09 RULES_SPEC 2.1 and v10 RULES_SPEC 2.3 / 3.1 / 5.1 consistency)."""
    inp, cid = case["input"], case["id"]
    def need(cond, why):
        if not cond:
            raise ValueError(f"{cid}: illegal case: {why}")
    definitions = {c["id"]: c for c in cfg["controllers"]}
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
        need(all(set(f) == {"faceId", "pip", "color", "texture", "gold"} for f in d["faces"]), "face fields")
        need(all(1 <= f["pip"] <= 6 and f["color"] in ("red", "blue") and f["texture"] in ("none", "smooth", "rough") and isinstance(f["gold"], bool) for f in d["faces"]), "face data")
        need(1 <= d["pip"] <= 6 and isinstance(d["glass"], bool) and d["lifetime"] in (LIFETIME_PERMANENT, LIFETIME_TEMPORARY), "die pip / lifetime")
        need(remote_allowed or d["pip"] == d["faces"][int(d["faceId"][2]) - 1]["pip"], "current pip differs from the face pip")
    for d in inp["pool"]:
        need(1 <= d["pip"] <= 6 and d["color"] in ("red", "blue") and d["faceId"] in face_ids, "pool die")
    permanent = inp["permanentDiceCount"]
    temporary = n + pool_n - permanent
    submitted_temporary = sum(1 for d in inp["dice"] if d["lifetime"] == LIFETIME_TEMPORARY)
    need(0 <= permanent <= cfg["maxPermanentDice"] and n + pool_n <= cfg["maxActiveDice"], "dice totals")
    need(0 <= temporary <= cfg["maxTemporaryDice"], "pool must hold every alive die that is not submitted")
    need(submitted_temporary <= temporary and temporary - submitted_temporary <= pool_n, "temporary dice vs permanentDiceCount")
    ctl = inp["controllers"]
    need(len(ctl) <= cfg["maxControllerInstances"], "controller count")
    need(len({c["instanceId"] for c in ctl}) == len(ctl) and len({c["definitionId"] for c in ctl}) == len(ctl), "duplicate controller")
    for i, c in enumerate(ctl):
        need(c["definitionId"] in definitions, "unknown controller")
        need(c["enchantmentId"] in ("none", "slotless", "base10", "mult2", "submit1", "roll1"), "enchantment")
        need(not (c["enchantmentId"] in ("base10", "mult2") and c["definitionId"] in non_submit), "score enchant on a non-scoring controller")
        need(c["growth"] >= 0 and (c["turnoverUnits2"] == 3 or c["definitionId"] == "C51") and c["turnoverUnits2"] >= 3, "dynamic state")
        need(c["definitionId"] != "C141" or c["growth"] < definitions["C141"]["parameters"]["expireGrowth"], "C141 expires at this growth")
        need(c["activeColor"] in (("red", "blue") if c["definitionId"] == "C45" else ("",)), "activeColor")
        need(not (c["definitionId"] == "C41" and i + 1 < len(ctl) and ctl[i + 1]["definitionId"] in ("C09", "C21", "C22")), "mimic of a capacity card is not covered")
    # p1.0.0 snapshot fields
    need(inp["stageKind"] in ("normal", "pressure", "boss"), "stageKind")
    modifier = modifier_of(cfg, inp["modifierId"])
    fx = set(modifier["effects"]) if modifier else set()
    need(modifier is None or inp["stageKind"] == "boss", "a modifier needs a boss stage")
    disabled = inp["disabledControllerIds"]
    need(disabled == [c["instanceId"] for c in ctl if c["instanceId"] in set(disabled)], "disabled ids must be existing instances in controller order")
    need(synthetic_disabled or disabled == disabled_by_modifier(cfg, ctl, inp["modifierId"]), "disabled controllers vs modifier")
    rolls, chances, capacity, pinned = stage_budgets(cfg, ctl, inp["modifierId"], set(disabled))
    need(not pinned or inp["capacity"] == capacity, "capacity vs capacity cards / modifier")
    need(inp["rollsUsed"] >= 1 and inp["rollsRemaining"] >= 0 and inp["rollsUsed"] + inp["rollsRemaining"] >= rolls, "roll budget")
    need(1 <= inp["submissionsRemainingBefore"] <= chances and inp["submissionOrdinal"] == chances - inp["submissionsRemainingBefore"], "submission budget")
    live = [c["definitionId"] for c in ctl if c["instanceId"] not in set(disabled)]
    need("C131" not in live or inp["stageKind"] != "boss", "a real C131 destroys itself when a boss stage starts")
    need("C136" not in live or temporary >= 1, "C136 grants a temporary die when the stage starts")
    need("MustFill" not in fx or n == min(inp["capacity"], n + pool_n), "MustFill")
    need("NoItems" not in fx or not (inp["redWave"] or inp["blueWave"] or inp["scoreItemMultiplier"]), "NoItems")
    need(isinstance(inp["redWave"], bool) and isinstance(inp["blueWave"], bool) and not (inp["redWave"] and inp["blueWave"]), "waves")
    need(inp["scoreItemMultiplier"] in (0, 2), "scoreItemMultiplier")
    need(0 <= inp["inventoryCount"] <= cfg["maxConsumableSlots"], "inventoryCount")
    occupying = sum(1 for c in ctl if c["enchantmentId"] != "slotless")
    need(cfg["initialControllerSlots"] <= inp["ordinaryControllerSlots"] <= cfg["maxOrdinaryControllerSlots"] and occupying <= inp["ordinaryControllerSlots"], "ordinary slots")
    used, ordinal = inp["stageHandsUsed"], inp["submissionOrdinal"]
    need(len(set(used)) == len(used) and set(used) <= hand_ids and len(used) <= ordinal, "stageHandsUsed")
    need(inp["wallet"] >= 0 and inp["previousPrimary"] in ({""} | hand_ids | {"scatter"}), "wallet / previousPrimary")
    need(ordinal == 0 or inp["previousPrimary"] == "scatter" or inp["previousPrimary"] in used, "previousPrimary vs stageHandsUsed")
    need(all(e["handId"] in hand_ids and 1 <= e["level"] <= cfg["maxHandLevel"] for e in inp["handLevels"]), "hand levels")
    need(all(e["handId"] in color_ids and 1 <= e["level"] <= cfg["maxColorLevel"] for e in inp["colorLevels"]), "colour levels")
    # 5.2 step 4: a disabled C53 draws nothing, so only the enabled ones carry a random choice.
    owners = sorted(c["instanceId"] for c in ctl if c["definitionId"] == "C53" and c["instanceId"] not in set(disabled))
    table = definitions["C53"]["parameters"]["factorUnits2"]
    need(sorted(e["ownerInstanceId"] for e in inp["randomChoices"]) == owners and all(e["units2"] in table for e in inp["randomChoices"]), "random choices")


def build_cases(cfg):
    std = cfg["diceDefinition"]["faces"]
    names = {"r": "red", "b": "blue"}
    cases, families, remote_ids, synthetic_ids, mismatches = [], [], set(), set(), []
    family = [""]

    def die(pip, color=None, face=None, pips=None, colors=None, tex=None, gold=(), glass=False, remote=False, temp=False):
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
        return {"faceId": f"F0{face}", "pip": pip, "glass": glass, "faces": faces, "remote": remote, "temp": temp}

    def dice(text):
        """'6b:gold:glass 3r:mono' -> dice. Flags: smooth, rough, gold, glass, mono (six faces of this colour), same (six faces of this pip), temp."""
        out = []
        for token in text.split():
            head, *flags = token.split(":")
            pip, color = int(head[0]), head[1]
            assert set(flags) <= {"smooth", "rough", "gold", "glass", "mono", "same", "temp"}, token
            out.append(die(pip, color, face=pip, pips=[pip] * 6 if "same" in flags else None, colors=color * 6 if "mono" in flags else None,
                           tex={pip: "smooth"} if "smooth" in flags else {pip: "rough"} if "rough" in flags else None,
                           gold=(pip,) if "gold" in flags else (), glass="glass" in flags, temp="temp" in flags))
        return out

    def add(desc, d, c="", p="", cap=None, rr=0, ru=None, srb=None, perm=None, wallet=0, wave=False, prev=None, lv=None, clv=None, rnd=None,
            kind=None, mod="", dis=None, used=None, inv=0, slots=None, bwave=False, item=0, s=None, chk=None):
        d = dice(d) if isinstance(d, str) else d
        controllers = []
        for i, token in enumerate(c.split()):
            head, *mods = token.split(":")
            entry = {"instanceId": f"ctl_{i + 1:02d}", "definitionId": head, "enchantmentId": "none", "growth": 0, "turnoverUnits2": 3, "activeColor": ""}
            for m in mods:
                if m.startswith("g="): entry["growth"] = int(m[2:])
                elif m.startswith("t="): entry["turnoverUnits2"] = int(m[2:])
                elif m in ("red", "blue"): entry["activeColor"] = m
                else: entry["enchantmentId"] = m
            controllers.append(entry)
        pool = [{"pip": int(t[0]), "color": names[t[1]]} for t in p.split()]
        n = len(d)
        cid = f"P{len(cases) + 1:03d}"
        disabled = list(dis) if dis is not None else disabled_by_modifier(cfg, controllers, mod)
        if dis is not None:
            synthetic_ids.add(cid)
        rolls, chances, auto_cap, _ = stage_budgets(cfg, controllers, mod, set(disabled))
        cap = auto_cap if cap is None else cap
        srb = chances if srb is None else srb
        ordinal = chances - srb
        if prev is None:
            prev = "" if ordinal == 0 else "scatter"
        if used is None:
            used = [] if ordinal == 0 or prev in ("", "scatter") else [prev]
        if any(x["remote"] for x in d):
            remote_ids.add(cid)
        occupying = sum(1 for x in controllers if x["enchantmentId"] != "slotless")
        inp = {
            "dice": [{"dieId": f"D{i + 1:03d}", "faceId": x["faceId"], "pip": x["pip"], "glass": x["glass"],
                      "lifetime": LIFETIME_TEMPORARY if x["temp"] else LIFETIME_PERMANENT, "faces": x["faces"]} for i, x in enumerate(d)],
            "pool": [{"dieId": f"D{n + i + 1:03d}", "faceId": f"F0{x['pip']}", "pip": x["pip"], "color": x["color"]} for i, x in enumerate(pool)],
            "capacity": cap, "rollsRemaining": rr, "rollsUsed": rolls - rr if ru is None else ru,
            "submissionsRemainingBefore": srb,
            "permanentDiceCount": min(n + len(pool) - sum(1 for x in d if x["temp"]), cfg["maxPermanentDice"]) if perm is None else perm,
            "submissionOrdinal": ordinal, "wallet": wallet, "redWave": wave, "previousPrimary": prev,
            "controllers": controllers,
            "handLevels": [{"handId": k, "level": v} for k, v in (lv or {}).items()],
            "colorLevels": [{"handId": k, "level": v} for k, v in (clv or {}).items()],
            "randomChoices": [{"ownerInstanceId": k, "units2": v} for k, v in (rnd or {}).items()],
            "stageKind": kind or ("boss" if mod else "normal"), "modifierId": mod, "disabledControllerIds": disabled,
            "stageHandsUsed": list(used), "inventoryCount": inv,
            "ordinaryControllerSlots": max(cfg["initialControllerSlots"], min(cfg["maxOrdinaryControllerSlots"], occupying)) if slots is None else slots,
            "blueWave": bwave, "scoreItemMultiplier": item,
        }
        case = {"id": cid, "description": desc, "input": inp}
        validate(case, cfg, cid in remote_ids, cid in synthetic_ids)
        snapshot = adapt(inp, cfg)
        result = score(snapshot, cfg)
        case["expected"] = expected_of(result)
        # Hand-computed expectations written next to the case; a mismatch means the scorer or the arithmetic is wrong.
        for key, value in dict(chk or {}, **({"score": s} if s is not None else {})).items():
            if case["expected"][key] != value:
                mismatches.append(f"{cid} {desc}: hand-computed {key}={value!r}, reference gives {case['expected'][key]!r}")
        # What each scoring effect code of the modifier changes in this case: drop the code and compare.
        effects = {}
        modifier = modifier_of(cfg, mod)
        for code in (modifier["effects"] if modifier else []):
            without = expected_of(score(snapshot, cfg, drop_effect=code))
            if code in SESSION_EFFECTS:
                if without != case["expected"]:
                    raise ValueError(f"{cid}: session-only effect {code} changed the score result")
                continue
            effects[code] = "effect" if without != case["expected"] else "neutral"
            if effects[code] == "effect" and without["score"] == case["expected"]["score"]:
                effects[code] = "effect-score-unchanged"
        cases.append(case)
        families.append({"id": cid, "family": family[0], "handChecked": s is not None or bool(chk), "modifier": mod, "effects": effects,
                         "sources": result["v09"]["sourceAbilities"], "triggered": result["triggeredSourceIds"]})

    PAIR = "3b 3b 5b 4r 2b"    # 17 pips, pair, mixed colours, one red            -> 34
    BLUE = "3b 3b 5b 6b 2b"    # 19 pips, pair, all blue                           -> 114
    SCAT = "1r 3b 5b 6b"       # 15 pips, scatter, one red                         -> 15
    RED2 = "4r 4r 2b 3b 6b"    # 19 pips, pair, red group 2                        -> 66
    RED3 = "4r 4r 4r 2b 6b"    # 20 pips, trips, red group 3                       -> 200
    STR = "1r 2b 3b 4r 5b"     # 15 pips, straight, mixed, red group 1             -> 150
    FULL = "3b 3b 3b 5b 5b"    # 19 pips, full house, all blue                     -> 342
    QUAD = "5b 5b 5b 5b 2b"    # 22 pips, quads, all blue                          -> 528
    ALLR = "4r 4r 1r"          # 9 pips, pair, all red, red group 2                -> 63
    STRP = "1r 2b 3b 4r 5b 5b"        # 20 pips, pair + straight (capacity 6)      -> 240
    STRT = "1r 2b 3b 4r 5b 5b 5b"     # 25 pips, trips + straight (capacity 7)     -> 350
    one = lambda owner, ability, delta, reset=False: [{"ownerInstanceId": owner, "abilityId": ability, "delta": delta, "reset": reset}]  # noqa: E731
    adds = lambda *e: [{"emitterId": a, "abilityId": b, "amount": c} for a, b, c in e]  # noqa: E731
    fc = lambda source, emitter, die_id, face_id, kind, amount=0, text="": {  # noqa: E731
        "sourceId": source, "emitterId": emitter, "dieId": die_id, "faceId": face_id, "kind": kind, "amount": amount, "text": text}
    ITEM = {"emitterId": "ITEM", "abilityId": "I09", "numerator": "2", "denominator": "1"}
    half = lambda b: {"emitterId": "BOSS", "abilityId": b, "numerator": "1", "denominator": "2"}  # noqa: E731
    T, F = True, False

    # ---- 6.1 submission order ----
    family[0] = "6.1 order"
    add("C104红蓝相间 红蓝红蓝红5颗：×3", "1r 2b 4r 3b 6r", "C104", s="48", chk={"triggeredSourceIds": ["ctl_01"]})
    add("C104 有两颗相邻同色：不触发", "1r 2b 3b 4r 6r", "C104", s="16", chk={"triggeredSourceIds": []})
    add("C104 只提交2颗：不足3颗不触发", "1r 2b", "C104", s="3", chk={"triggeredSourceIds": []})
    add("C104 恰好3颗交替", "2b 4r 3b", "C104", s="27")
    add("C104加首当其冲：首骰视红后红蓝红交替", "2b 3b 4r", "C03 C104", s="27", chk={"triggeredSourceIds": ["ctl_01", "ctl_02"]})
    add("C104 红色浪潮：全部视红，不触发", "2b 4r 3b", "C104", wave=True, s="18", chk={"triggeredSourceIds": []})
    add("C104 蓝色浪潮：全部视蓝，不触发", "2b 4r 3b", "C104", bwave=True, s="27", chk={"triggeredSourceIds": [], "colorId": "all_blue"})
    add("C41复制C104：×9", "2b 4r 3b", "C41 C104", s="81")
    add("C105泾渭分明 红骰都在蓝骰左侧：×2", "4r 1r 2b 3b 6b", "C105", s="32")
    add("C105 红骰在右侧：不触发", "2b 3b 6b 4r 1r", "C105", s="16", chk={"triggeredSourceIds": []})
    add("C105 全红：不触发", "4r 1r", "C105", s="10", chk={"triggeredSourceIds": []})
    add("C105 全蓝：不触发", BLUE, "C105", s="114", chk={"triggeredSourceIds": []})
    add("C105 红蓝交错：不触发", "4r 2b 1r 3b", "C105", s="10", chk={"triggeredSourceIds": []})
    add("C105加首当其冲：首骰视红后红左蓝右", "2b 3b 6b", "C03 C105", s="22", chk={"triggeredSourceIds": ["ctl_01", "ctl_02"]})
    add("C105 只提交2颗 红在左", "4r 2b", "C105", s="12")
    add("C105 只提交2颗 蓝在左：不触发", "2b 4r", "C105", s="6", chk={"triggeredSourceIds": []})
    add("C106对称美学 3颗 3-5-3：×2", "3b 5b 3b", "C106", s="132")
    add("C106 3-3-5 不对称：不触发", "3b 3b 5b", "C106", s="66", chk={"triggeredSourceIds": []})
    add("C106 2颗同点：不足3颗不触发", "3b 3b", "C106", s="36", chk={"triggeredSourceIds": []})
    add("C106 5颗 2-5-4-5-2", "2b 5b 4r 5b 2b", "C106", s="108")
    add("C106 4颗 6-1-1-6（偶数颗）", "6b 1r 1r 6b", "C106", s="147")
    add("C106加不要奇数：1-6-2 变成有效 2-6-2", "1r 6b 2b", "C12 C106", s="40", chk={"triggeredSourceIds": ["ctl_01", "ctl_02"]})
    add("C106 3颗同点", "5b 5b 5b", "C106", s="360")
    add("C41复制C106：×4", "3b 5b 3b", "C41 C106", s="264")
    add("C107顺藤摸瓜 1-2-3-5-6：三颗各+10", "1r 2b 3b 5b 6b", "C107", s="47", chk={"pipContributions": [1, 12, 13, 5, 16]})
    add("C107 降序：不触发", "6b 5b 3b 2b 1r", "C107", s="17", chk={"triggeredSourceIds": []})
    add("C107 3-3-5-4-2 没有比左邻大1的骰：不触发", PAIR, "C107", s="34", chk={"triggeredSourceIds": []})
    add("C107 顺子1到5：后四颗各+10", STR, "C107", s="550", chk={"pipContributions": [1, 12, 13, 14, 15]})
    add("C107加不要奇数：有效点数全为偶数，不可能相差1", "1r 2b 3b", "C12 C107", s="16", chk={"triggeredSourceIds": ["ctl_01"]})
    add("C41复制C107：同一颗+20", "1r 2b", "C41 C107", s="23", chk={"pipContributions": [1, 22]})
    add("C107 只提交1颗：不触发", "6b", "C107", s="18", chk={"triggeredSourceIds": []})
    add("C107加谢幕：加值也被再计", "5b 6b", "C107 C68", srb=1, s="126", chk={"pipContributions": [5, 16], "pipSum": "42"})
    add("C108排头兵 首骰3点：基础分+12", PAIR, "C108", s="58", chk={"baseTotal": "29", "controllerBaseAdds": adds(("ctl_01", "C108", "12"))})
    add("C108 单颗6点：+24", "6b", "C108", s="90")
    add("C108加不要奇数：首骰1点按有效2点算，+8", "1r 3b 5b", "C12 C108", s="20", chk={"controllerBaseAdds": adds(("ctl_02", "C108", "8"))})
    add("C41复制C108：两个来源各+12", PAIR, "C41 C108", s="82", chk={"controllerBaseAdds": adds(("ctl_01", "C108", "12"), ("ctl_02", "C108", "12"))})
    add("C108 遥控把首骰定为6点：按当前点数6算，+24", [die(6, face=2, remote=True)] + dice("3b 3b"), "C108", s="216")
    add("C109中流砥柱 5颗：正中间那颗再计2次", PAIR, "C109", s="54", chk={"pipRetriggers": [0, 0, 2, 0, 0], "pipSum": "27"})
    add("C109 3颗", "3b 6b 3b", "C109", s="144", chk={"pipRetriggers": [0, 2, 0]})
    add("C109 4颗（偶数）：不触发", "3b 3b 5b 4r", "C109", s="30", chk={"triggeredSourceIds": []})
    add("C109 1颗：不足3颗不触发", "6b", "C109", s="18", chk={"triggeredSourceIds": []})
    add("C109 容量7提交7颗：第4颗再计2次", "2b 2b 3b 6b 3b 5b 5b", "C22 C109", s="798", chk={"pipRetriggers": [0, 0, 0, 2, 0, 0, 0], "triggeredSourceIds": ["ctl_02"]})
    add("C41复制C109：正中间再计4次", PAIR, "C41 C109", s="74", chk={"pipRetriggers": [0, 0, 4, 0, 0]})
    add("C109加少就是多：中间的1点先+10再计3次", "3b 1r 3b", "C109 C10", s="78", chk={"pipContributions": [3, 11, 3], "pipSum": "39"})

    # ---- 6.3 face-change cards ----
    family[0] = "6.3 face change"
    add("C117点金手：最后一颗的朝上面镀金", PAIR, "C117", s="34", chk={"faceChanges": [fc("C117", "ctl_01", "D005", "F02", "gold", 1)], "triggeredSourceIds": ["ctl_01"]})
    add("C117 最后一颗已是黄金面：仍列入变更", "3b 3b 6b:gold", "C117", s="108", chk={"faceChanges": [fc("C117", "ctl_01", "D003", "F06", "gold", 1)]})
    add("C117 只提交1颗", "6b", "C117", s="18", chk={"faceChanges": [fc("C117", "ctl_01", "D001", "F06", "gold", 1)]})
    add("C41复制C117：两个来源各一条", PAIR, "C41 C117", s="34",
        chk={"faceChanges": [fc("C117", "ctl_01", "D005", "F02", "gold", 1), fc("C117", "ctl_02", "D005", "F02", "gold", 1)]})
    add("C118表里同色：每颗提交骰的对面染成朝上面的颜色", "3b 4r", "C118", s="7",
        chk={"faceChanges": [fc("C118", "ctl_01", "D001", "F04", "color", 0, "blue"), fc("C118", "ctl_01", "D002", "F03", "color", 0, "red")]})
    add("C118 对面已是同色：仍列入变更", "6r", "C118", s="12", chk={"faceChanges": [fc("C118", "ctl_01", "D001", "F01", "color", 0, "red")]})
    add("C118加红色浪潮：取永久颜色而不是有效颜色", "3b 5b", "C118", wave=True, s="16",
        chk={"faceChanges": [fc("C118", "ctl_01", "D001", "F04", "color", 0, "blue"), fc("C118", "ctl_01", "D002", "F02", "color", 0, "blue")]})
    add("C118加首当其冲：首骰有效红但永久蓝", "3b 3b", "C03 C118", s="12",
        chk={"faceChanges": [fc("C118", "ctl_02", "D001", "F04", "color", 0, "blue"), fc("C118", "ctl_02", "D002", "F04", "color", 0, "blue")]})
    add("C118 遥控骰：按物理面F02的对面F05", [die(6, face=2, remote=True)], "C118", s="18", chk={"faceChanges": [fc("C118", "ctl_01", "D001", "F05", "color", 0, "blue")]})
    add("C119潜移默化：首骰编号最小的异点面变成朝上面的点数", PAIR, "C119", s="34", chk={"faceChanges": [fc("C119", "ctl_01", "D001", "F01", "pipSet", 3)]})
    add("C119 首骰朝上面是F01：改F02", "1r 3b 5b", "C119", s="9", chk={"faceChanges": [fc("C119", "ctl_01", "D001", "F02", "pipSet", 1)]})
    add("C119 首骰整骰同点：没有可改的面，仍算触发", "3b:same 3b", "C119", s="36", chk={"faceChanges": [], "triggeredSourceIds": ["ctl_01"]})
    add("C119 首骰前三面已同点：改F04", [die(3, pips=[3, 3, 3, 4, 5, 6])] + dice("5b"), "C119", s="24", chk={"faceChanges": [fc("C119", "ctl_01", "D001", "F04", "pipSet", 3)]})
    add("C119 遥控骰：按朝上面的永久点数2，不是遥控值6", [die(6, face=2, remote=True)] + dice("3b 3b"), "C119", s="72",
        chk={"faceChanges": [fc("C119", "ctl_01", "D001", "F01", "pipSet", 2)]})
    add("C41复制C119：两条相同的变更", PAIR, "C41 C119", s="34",
        chk={"faceChanges": [fc("C119", "ctl_01", "D001", "F01", "pipSet", 3), fc("C119", "ctl_02", "D001", "F01", "pipSet", 3)]})
    add("C122见五成群：小于5的+1，6的−1，5不变", "1r 5b 6b 4r 2b", "C122", s="18",
        chk={"faceChanges": [fc("C122", "ctl_01", "D001", "F01", "pipDelta", 1), fc("C122", "ctl_01", "D003", "F06", "pipDelta", -1),
                             fc("C122", "ctl_01", "D004", "F04", "pipDelta", 1), fc("C122", "ctl_01", "D005", "F02", "pipDelta", 1)]})
    add("C122 全是5点：没有变更，仍算触发", "5b 5b 5b", "C122", s="180", chk={"faceChanges": [], "triggeredSourceIds": ["ctl_01"]})
    add("C122 遥控骰：按永久点数2算+1", [die(6, face=2, remote=True)] + dice("3b 3b"), "C122", s="72",
        chk={"faceChanges": [fc("C122", "ctl_01", "D001", "F02", "pipDelta", 1), fc("C122", "ctl_01", "D002", "F03", "pipDelta", 1),
                             fc("C122", "ctl_01", "D003", "F03", "pipDelta", 1)]})
    add("C122加不要奇数：永久5点、有效6点，不变", "5b", "C12 C122", s="18", chk={"faceChanges": [], "triggeredSourceIds": ["ctl_01", "ctl_02"]})
    add("C123二三其德：1点+1，4/5/6点−1，2和3不变", "1r 2b 3b 4r 6b", "C123", s="16",
        chk={"faceChanges": [fc("C123", "ctl_01", "D001", "F01", "pipDelta", 1), fc("C123", "ctl_01", "D004", "F04", "pipDelta", -1),
                             fc("C123", "ctl_01", "D005", "F06", "pipDelta", -1)]})
    add("C123 只有2点和3点：没有变更，仍算触发", "2b 3b 3b", "C123", s="48", chk={"faceChanges": [], "triggeredSourceIds": ["ctl_01"]})
    add("C123 5点−1", "5b", "C123", s="15", chk={"faceChanges": [fc("C123", "ctl_01", "D001", "F05", "pipDelta", -1)]})
    add("C124灌铅：每颗提交骰朝上面权重+1", "3b 3b", "C124", s="36",
        chk={"faceChanges": [fc("C124", "ctl_01", "D001", "F03", "weightDelta", 1), fc("C124", "ctl_01", "D002", "F03", "weightDelta", 1)]})
    add("C41复制C124：同一面两条+1", "6b", "C41 C124", s="18",
        chk={"faceChanges": [fc("C124", "ctl_01", "D001", "F06", "weightDelta", 1), fc("C124", "ctl_02", "D001", "F06", "weightDelta", 1)]})
    add("C125抛光匠：第一颗与最后一颗的朝上面变光滑", PAIR, "C125", s="34",
        chk={"faceChanges": [fc("C125", "ctl_01", "D001", "F03", "texture", 0, "smooth"), fc("C125", "ctl_01", "D005", "F02", "texture", 0, "smooth")]})
    add("C125 只提交1颗：目标去重", "6b", "C125", s="18", chk={"faceChanges": [fc("C125", "ctl_01", "D001", "F06", "texture", 0, "smooth")]})
    add("C125 第一颗已是光滑：只改最后一颗", "3b:smooth 3b 5b", "C125", s="66", chk={"faceChanges": [fc("C125", "ctl_01", "D003", "F05", "texture", 0, "smooth")]})
    add("C125 最后一颗是粗糙：只改第一颗", "3b 3b 5b:rough", "C125", s="66", chk={"faceChanges": [fc("C125", "ctl_01", "D001", "F03", "texture", 0, "smooth")]})
    add("C125 两颗都有纹理：没有变更，仍算触发", "3b:rough 5b:smooth", "C125", s="24", chk={"faceChanges": [], "triggeredSourceIds": ["ctl_01"]})
    add("C125 纹理在没朝上的面：照常变更", [die(3, tex={4: "smooth"})] + dice("5b"), "C125", s="24",
        chk={"faceChanges": [fc("C125", "ctl_01", "D001", "F03", "texture", 0, "smooth"), fc("C125", "ctl_01", "D002", "F05", "texture", 0, "smooth")]})
    add("C126养精蓄锐：每颗留池骰朝上面+1（6点也列入，由会话夹住）", PAIR, "C126", p="1r 6b", s="34",
        chk={"faceChanges": [fc("C126", "ctl_01", "D006", "F01", "pipDelta", 1), fc("C126", "ctl_01", "D007", "F06", "pipDelta", 1)]})
    add("C126 没有留池骰：不触发", PAIR, "C126", s="34", chk={"faceChanges": [], "triggeredSourceIds": []})
    add("C41复制C126：两个来源依次各一份", "6b", "C41 C126", p="2b 5b", s="18",
        chk={"faceChanges": [fc("C126", "ctl_01", "D002", "F02", "pipDelta", 1), fc("C126", "ctl_01", "D003", "F05", "pipDelta", 1),
                             fc("C126", "ctl_02", "D002", "F02", "pipDelta", 1), fc("C126", "ctl_02", "D003", "F05", "pipDelta", 1)]})
    add("C126 留池里有3颗临时骰：同样列入", PAIR, "C126", p="1r 2b 4r", perm=5, s="34",
        chk={"faceChanges": [fc("C126", "ctl_01", "D006", "F01", "pipDelta", 1), fc("C126", "ctl_01", "D007", "F02", "pipDelta", 1),
                             fc("C126", "ctl_01", "D008", "F04", "pipDelta", 1)]})
    add("C127移花接木：首骰的黄金光滑蓝6点复制到末骰", "6b:gold:smooth 3b 4r", "C127", s="19",
        chk={"faceChanges": [fc("C127", "ctl_01", "D003", "F04", "pipSet", 6), fc("C127", "ctl_01", "D003", "F04", "color", 0, "blue"),
                             fc("C127", "ctl_01", "D003", "F04", "texture", 0, "smooth"), fc("C127", "ctl_01", "D003", "F04", "gold", 1)]})
    add("C127 普通首骰：纹理写none、黄金写0", PAIR, "C127", s="34",
        chk={"faceChanges": [fc("C127", "ctl_01", "D005", "F02", "pipSet", 3), fc("C127", "ctl_01", "D005", "F02", "color", 0, "blue"),
                             fc("C127", "ctl_01", "D005", "F02", "texture", 0, "none"), fc("C127", "ctl_01", "D005", "F02", "gold", 0)]})
    add("C127 本关第二次提交：不触发", PAIR, "C127", srb=2, s="34", chk={"faceChanges": [], "triggeredSourceIds": []})
    add("C127 只提交1颗：不触发", "6b", "C127", s="18", chk={"faceChanges": [], "triggeredSourceIds": []})
    add("C127 恰好2颗：红1点复制到末骰", "1r 6b", "C127", s="7",
        chk={"faceChanges": [fc("C127", "ctl_01", "D002", "F06", "pipSet", 1), fc("C127", "ctl_01", "D002", "F06", "color", 0, "red"),
                             fc("C127", "ctl_01", "D002", "F06", "texture", 0, "none"), fc("C127", "ctl_01", "D002", "F06", "gold", 0)]})
    add("C127 遥控首骰：复制永久点数2", [die(6, face=2, remote=True)] + dice("3b"), "C127", s="27",
        chk={"faceChanges": [fc("C127", "ctl_01", "D002", "F03", "pipSet", 2), fc("C127", "ctl_01", "D002", "F03", "color", 0, "blue"),
                             fc("C127", "ctl_01", "D002", "F03", "texture", 0, "none"), fc("C127", "ctl_01", "D002", "F03", "gold", 0)]})
    add("C41复制C127：两个来源各四条", "1r 6b", "C41 C127", s="7",
        chk={"faceChanges": [fc("C127", e, "D002", "F06", k, a, t) for e in ("ctl_01", "ctl_02")
                             for k, a, t in (("pipSet", 1, ""), ("color", 0, "red"), ("texture", 0, "none"), ("gold", 0, ""))]})
    add("多张改骰卡同场：按来源从左到右，各自读取冻结快照", "1r 6b", "C122 C123 C117 C125", s="7",
        chk={"faceChanges": [fc("C122", "ctl_01", "D001", "F01", "pipDelta", 1), fc("C122", "ctl_01", "D002", "F06", "pipDelta", -1),
                             fc("C123", "ctl_02", "D001", "F01", "pipDelta", 1), fc("C123", "ctl_02", "D002", "F06", "pipDelta", -1),
                             fc("C117", "ctl_03", "D002", "F06", "gold", 1),
                             fc("C125", "ctl_04", "D001", "F01", "texture", 0, "smooth"), fc("C125", "ctl_04", "D002", "F06", "texture", 0, "smooth")]})
    add("点金手、百变模仿、灌铅：模仿的那份排在它自己的位置", "6b", "C117 C41 C124", s="18",
        chk={"faceChanges": [fc("C117", "ctl_01", "D001", "F06", "gold", 1), fc("C124", "ctl_02", "D001", "F06", "weightDelta", 1),
                             fc("C124", "ctl_03", "D001", "F06", "weightDelta", 1)]})
    add("七张改骰卡同场", "1r 6b:gold 5b", "C117 C118 C119 C122 C123 C124 C125", s="18",
        chk={"triggeredSourceIds": [f"ctl_0{i}" for i in range(1, 8)],
             "faceChanges": [fc("C117", "ctl_01", "D003", "F05", "gold", 1),
                             fc("C118", "ctl_02", "D001", "F06", "color", 0, "red"), fc("C118", "ctl_02", "D002", "F01", "color", 0, "blue"),
                             fc("C118", "ctl_02", "D003", "F02", "color", 0, "blue"),
                             fc("C119", "ctl_03", "D001", "F02", "pipSet", 1),
                             fc("C122", "ctl_04", "D001", "F01", "pipDelta", 1), fc("C122", "ctl_04", "D002", "F06", "pipDelta", -1),
                             fc("C123", "ctl_05", "D001", "F01", "pipDelta", 1), fc("C123", "ctl_05", "D002", "F06", "pipDelta", -1),
                             fc("C123", "ctl_05", "D003", "F05", "pipDelta", -1),
                             fc("C124", "ctl_06", "D001", "F01", "weightDelta", 1), fc("C124", "ctl_06", "D002", "F06", "weightDelta", 1),
                             fc("C124", "ctl_06", "D003", "F05", "weightDelta", 1),
                             fc("C125", "ctl_07", "D001", "F01", "texture", 0, "smooth"), fc("C125", "ctl_07", "D003", "F05", "texture", 0, "smooth")]})

    # ---- 6.4 boss and pressure ----
    family[0] = "6.4 boss / pressure"
    add("C129屠龙者 成长0：×1仍算触发", PAIR, "C129", s="34", chk={"triggeredSourceIds": ["ctl_01"], "growthChanges": []})
    add("C129 成长10：×1.5", PAIR, "C129:g=10", s="51", chk={"controllerMultiplierNumerator": "3", "controllerMultiplierDenominator": "2"})
    add("C129 成长30：×2.5", PAIR, "C129:g=30", s="85")
    add("C41复制C129 成长10：×2.25，提交不产生成长计划", PAIR, "C41 C129:g=10", s="76", chk={"growthChanges": []})
    add("C130逆流而上 首领关（减益已解除）：×3", PAIR, "C130", kind="boss", s="102")
    add("C130 普通关：不触发", PAIR, "C130", s="34", chk={"triggeredSourceIds": []})
    add("C130 高压关：不触发", PAIR, "C130", kind="pressure", s="34", chk={"triggeredSourceIds": []})
    add("C130 首领关且减益生效中（白干）", PAIR, "C130", mod="B04", s="102")
    add("C41复制C130 首领关：×9", PAIR, "C41 C130", kind="boss", s="306")
    add("C132高压锅 成长12：×1.6", PAIR, "C132:g=12", s="54")
    add("C132 成长0", PAIR, "C132", s="34", chk={"triggeredSourceIds": ["ctl_01"]})
    add("C132 成长24 高压关：×2.2", PAIR, "C132:g=24", kind="pressure", s="74")

    # ---- 6.5 items and temporary dice ----
    family[0] = "6.5 items / temporary dice"
    add("C134物尽其用 成长3：×1.15", PAIR, "C134:g=3", s="39")
    add("C134 成长0", PAIR, "C134", s="34", chk={"triggeredSourceIds": ["ctl_01"]})
    add("C134 成长9：×1.45", PAIR, "C134:g=9", s="49")
    add("C135囤积居奇 背包为空：不触发", PAIR, "C135", s="34", chk={"triggeredSourceIds": [], "controllerBaseAdds": []})
    add("C135 背包1个道具：+10", PAIR, "C135", inv=1, s="54", chk={"controllerBaseAdds": adds(("ctl_01", "C135", "10"))})
    add("C135 背包2个：+20", PAIR, "C135", inv=2, s="74")
    add("C135 背包5个（道具槽上限）：+50", PAIR, "C135", inv=5, s="134")
    add("C41复制C135 背包2个：两个来源各+20", PAIR, "C41 C135", inv=2, s="114")
    add("C137客串明星：提交的临时骰+15", "3b 3b 5b:temp 4r 2b", "C137", s="64", chk={"pipContributions": [3, 3, 20, 4, 2]})
    add("C137 临时骰留在池里：不触发", PAIR, "C137", p="6b", perm=5, s="34", chk={"triggeredSourceIds": []})
    add("C137 两颗临时骰", "3b:temp 3b:temp 5b", "C137", p="1r 2b 4r 6b", s="246", chk={"pipContributions": [18, 18, 5]})
    add("C137加一马当先：临时骰的+15也被再计", "6b:temp", "C137 C64", p="1r 2b 3b 4r 5b", s="126", chk={"pipSum": "42"})
    add("C137 没有临时骰：不触发", PAIR, "C137", s="34", chk={"triggeredSourceIds": []})
    add("C41复制C137：+30", "3b 3b 5b:temp 4r 2b", "C41 C137", s="94")
    add("C137 黄金面的临时骰：先+15再×2", "5b:temp:gold", "C137", p="1r 2b 3b 4r 6b", s="120", chk={"pipContributions": [40]})

    # ---- 6.6 gold ----
    family[0] = "6.6 gold"
    add("C138一掷千金 钱包恰好3：×3，计分后扣3", PAIR, "C138", wallet=3, s="102", chk={"goldSpent": 3})
    add("C138 钱包2：不触发，不扣款", PAIR, "C138", wallet=2, s="34", chk={"goldSpent": 0, "triggeredSourceIds": []})
    add("C138 钱包0", PAIR, "C138", s="34", chk={"goldSpent": 0, "triggeredSourceIds": []})
    add("C138 钱包100：只扣3", PAIR, "C138", wallet=100, s="102", chk={"goldSpent": 3})
    add("C41复制C138 钱包5：只够左边的模仿来源", PAIR, "C41 C138", wallet=5, s="102", chk={"goldSpent": 3, "triggeredSourceIds": ["ctl_01"]})
    add("C41复制C138 钱包6：两个来源都触发，×9扣6", PAIR, "C41 C138", wallet=6, s="306", chk={"goldSpent": 6, "triggeredSourceIds": ["ctl_01", "ctl_02"]})
    add("C41复制C138 钱包2：都不触发", PAIR, "C41 C138", wallet=2, s="34", chk={"goldSpent": 0})
    add("C138加有钱真好 钱包5：都读冻结钱包，扣款不回算本手", PAIR, "C138 C44", wallet=5, s="132", chk={"goldSpent": 3, "baseTotal": "22"})
    add("有钱真好、百变模仿、一掷千金 钱包4：只有模仿来源够付", PAIR, "C44 C41 C138", wallet=4, s="126", chk={"goldSpent": 3, "triggeredSourceIds": ["ctl_01", "ctl_02"]})
    add("存钱罐加一掷千金 钱包2：本手收入不算进冻结钱包", PAIR, "C01 C138", wallet=2, s="34", chk={"goldSpent": 0, "triggeredSourceIds": ["ctl_01"]})
    add("C139富甲一方 钱包9：不触发", PAIR, "C139", wallet=9, s="34", chk={"triggeredSourceIds": []})
    add("C139 钱包10：×1.25", PAIR, "C139", wallet=10, s="42", chk={"controllerMultiplierNumerator": "5", "controllerMultiplierDenominator": "4"})
    add("C139 钱包25：2档×1.5", PAIR, "C139", wallet=25, s="51")
    add("C139 钱包40：×2", PAIR, "C139", wallet=40, s="68")
    add("C139 钱包79：7档×2.75", PAIR, "C139", wallet=79, s="93", chk={"controllerMultiplierNumerator": "11", "controllerMultiplierDenominator": "4"})
    add("C139 钱包80：8档×3", PAIR, "C139", wallet=80, s="102", chk={"controllerMultiplierNumerator": "3", "controllerMultiplierDenominator": "1"})
    add("C139 钱包200：封顶×3", PAIR, "C139", wallet=200, s="102")
    add("C41复制C139 钱包40：×4", PAIR, "C41 C139", wallet=40, s="136")
    add("C138加C139 钱包12：富甲一方读冻结钱包，不受扣款影响", PAIR, "C138 C139", wallet=12, s="127", chk={"goldSpent": 3})
    add("C139加有钱真好 钱包20：两张卡读同一个冻结钱包", PAIR, "C139 C44", wallet=20, s="111", chk={"baseTotal": "37"})
    add("C141昙花一现 已过0关：×2.5", PAIR, "C141", s="85", chk={"growthChanges": [], "controllerMultiplierNumerator": "5", "controllerMultiplierDenominator": "2"})
    add("C141 已过1关：×2", PAIR, "C141:g=1", s="68", chk={"controllerMultiplierNumerator": "2", "controllerMultiplierDenominator": "1"})
    add("C141 已过2关（到期前最后一关）：×1.5", PAIR, "C141:g=2", s="51", chk={"controllerMultiplierNumerator": "3", "controllerMultiplierDenominator": "2"})
    add("C141 已过2关 全蓝手：×1.5", BLUE, "C141:g=2", s="171")
    add("C41复制C141 已过1关：×4", PAIR, "C41 C141:g=1", s="136")

    # ---- 6.7 hands and archetypes ----
    family[0] = "6.7 hands / archetypes"
    add("C142一条龙 顺子：牌型倍率10+10", STR, "C142", s="300", chk={"totalHandMult100": 2000, "handMultAdd100": 1000, "handMultBeforeModifier100": 2000})
    add("C142 没有顺子：不触发", PAIR, "C142", s="34", chk={"triggeredSourceIds": [], "handMultAdd100": 0})
    add("C142加简单顺子：四连顺子也+10", "2b 3b 4r 5b", "C29 C142", s="280", chk={"totalHandMult100": 2000})
    add("C41复制C142：+20", STR, "C41 C142", s="450", chk={"totalHandMult100": 3000, "handMultAdd100": 2000})
    add("C142 一对加顺子：2+10+10", STRP, "C142", cap=6, s="440", chk={"totalHandMult100": 2200})
    add("C142 顺子3级：19+10", STR, "C142", lv={"straight": 3}, s="435", chk={"totalHandMult100": 2900})
    add("C143好事成双 一对：两颗各再计1次", PAIR, "C143", s="46", chk={"pipRetriggers": [1, 1, 0, 0, 0], "pipSum": "23"})
    add("C143 三条不是恰好2颗：不触发", RED3, "C143", s="200", chk={"triggeredSourceIds": []})
    add("C143 两对：四颗各再计", "3b 3b 5b 5b 2b", "C143", s="306", chk={"pipRetriggers": [1, 1, 1, 1, 0]})
    add("C143 三条带一对：只有那一对再计", FULL, "C143", s="522", chk={"pipRetriggers": [0, 0, 0, 1, 1]})
    add("C41复制C143：各再计2次", PAIR, "C41 C143", s="58", chk={"pipRetriggers": [2, 2, 0, 0, 0]})
    add("C143加不要奇数：1和2成为一对有效2点", "1r 2b 5b", "C12 C143", s="28", chk={"pipRetriggers": [1, 1, 0]})
    add("C143 散点：不触发", SCAT, "C143", s="15", chk={"triggeredSourceIds": []})
    add("C143 四对子8颗：全部再计", "1r 1r 2b 2b 3b 3b 5b 5b", "C143", cap=8, s="924", chk={"pipSum": "44"})
    add("C144偶数为王 2-4-6：×3", "2b 4r 6b", "C144", s="36")
    add("C144 有一颗奇数：不触发", "2b 4r 5b", "C144", s="11", chk={"triggeredSourceIds": []})
    add("C144加不要奇数：全部变成有效偶数", PAIR, "C12 C144", s="240", chk={"triggeredSourceIds": ["ctl_01", "ctl_02"]})
    add("C144 单颗6点", "6b", "C144", s="54")
    add("C41复制C144：×9", "2b 4r 6b", "C41 C144", s="108")
    add("C146锦上添红 红组2颗按3颗算", RED2, "C146", s="95", chk={"redCount": 3, "redBonusCount": 1, "redMult100": 250, "redPoolCount": 0})
    add("C146 没有有效红骰：不触发", BLUE, "C146", s="114", chk={"triggeredSourceIds": [], "redBonusCount": 0, "redCount": 0})
    add("C146 单颗红骰按2颗算", PAIR, "C146", s="59", chk={"redCount": 2, "redMult100": 175})
    add("C146加场外红人：2+1+1=4", RED2, "C73 C146", p="1r", s="133", chk={"redCount": 4, "redPoolCount": 1, "redBonusCount": 1})
    add("C146加场外红人 5+3+1=9：数量记9，奖励按上限8", "4r 4r 4r 4r 4r", "C73 C146", p="1r 4r 1r", s="6720", chk={"redCount": 9, "redMult100": 1400})
    add("C41复制C146：不重复加，两个来源都算触发", RED2, "C41 C146", s="95", chk={"redCount": 3, "redBonusCount": 1, "triggeredSourceIds": ["ctl_01", "ctl_02"]})
    add("C146加红火：加成后的红组3颗使红火成长", RED2, "C146 C57:g=3", s="109", chk={"growthChanges": one("ctl_02", "C57", 3)})
    add("C146加红色共鸣：只有真实的两颗红骰再计", RED2, "C146 C69", s="135", chk={"pipRetriggers": [1, 1, 0, 0, 0]})
    add("C146 提交7红：按8颗算", "4r 4r 4r 4r 4r 4r 4r", "C146", cap=7, s="21168", chk={"redCount": 8, "redMult100": 1400})
    add("C146 蓝色浪潮下没有红组：不触发", RED2, "C146", bwave=True, s="114", chk={"triggeredSourceIds": [], "redCount": 0})
    add("C146加红运当头：三颗异点红骰同组再+1", "1r 4r 6r 2b 3b", "C25 C146", s="56", chk={"redCount": 4})
    add("C147观众席 留池2颗：×1.5", PAIR, "C147", p="1r 6b", s="51")
    add("C147 没有留池骰：不触发", PAIR, "C147", s="34", chk={"triggeredSourceIds": []})
    add("C147 留池5颗：×2.25", "6b", "C147", p="1r 2b 3b 4r 5b", s="40")
    add("C147 留池1颗：×1.25", PAIR, "C147", p="6b", s="42")
    add("C41复制C147 留池2颗：×2.25", PAIR, "C41 C147", p="1r 6b", s="76")
    add("C147 留池9颗（含2颗临时骰）：×3.25", "6b", "C147", p="1r 2b 3b 4r 5b 6b 1r 2b 3b", s="58", chk={"controllerMultiplierNumerator": "13", "controllerMultiplierDenominator": "4"})
    add("C148见好就收 剩2次投掷：×2", PAIR, "C148", ru=1, rr=2, s="92")
    add("C148 剩1次：×1.5", PAIR, "C148", ru=2, rr=1, s="60")
    add("C148 剩0次：不触发", PAIR, "C148", s="34", chk={"triggeredSourceIds": []})
    add("C148加再给一次 剩3次：×2.5", PAIR, "C148 C78", ru=1, rr=3, s="130", chk={"triggeredSourceIds": ["ctl_01"]})
    add("C41复制C148 剩2次：×4", PAIR, "C41 C148", ru=1, rr=2, s="184")
    add("C148加一锤定音 只投1次剩2次：×2×3", PAIR, "C148 C75", ru=1, rr=2, s="276")
    add("C149单刀赴会 只提交1颗：×4", "6b", "C149", s="72")
    add("C149 提交2颗：不触发", "6b 5b", "C149", s="33", chk={"triggeredSourceIds": []})
    add("C41复制C149：×16", "6b", "C41 C149", s="288")
    add("C149加空城计：提交1颗留池1颗", "6b", "C149 C74", p="1r", s="216")
    add("C150渐入佳境 本关第1次提交：不触发", PAIR, "C150", s="34", chk={"triggeredSourceIds": []})
    add("C150 第2次：×2", PAIR, "C150", srb=2, s="68")
    add("C150 第3次：×3", PAIR, "C150", srb=1, s="102")
    add("C150加多交一手 第4次：×4", PAIR, "C150 C79", srb=1, s="136", chk={"triggeredSourceIds": ["ctl_01"]})
    add("C41复制C150 第3次：×9", PAIR, "C41 C150", srb=1, s="306")

    # ---- 6.8 build ----
    family[0] = "6.8 build"
    add("C151留白 5个栏位只占1个：空4个×3", PAIR, "C151", s="102", chk={"controllerMultiplierNumerator": "3", "controllerMultiplierDenominator": "1"})
    add("C151 栏位扩到7：空6个×4", PAIR, "C151", slots=7, s="136")
    add("C151 5个栏位全占满：不触发", PAIR, "C151 C95 C96 C97 C98", s="34", chk={"triggeredSourceIds": []})
    add("C151 免槽的控制器不占栏位：空4个", PAIR, "C151 C95:slotless C96:slotless", s="102")
    add("C41复制C151：空3个，×2.5两次", PAIR, "C41 C151", s="212")
    add("C151 空1个：×1.5", PAIR, "C151 C95 C96 C97", s="51")
    add("C151 自身免槽：5个栏位全空，×3.5", PAIR, "C151:slotless", s="119")
    add("C152草根逆袭 没有普通稀有度的控制器：不触发", PAIR, "C152", s="34", chk={"triggeredSourceIds": []})
    add("C152 1张普通：×1.25", PAIR, "C152 C95", s="42", chk={"triggeredSourceIds": ["ctl_01"]})
    add("C152 3张普通：×1.75", PAIR, "C152 C95 C115 C133", s="59")
    add("C41复制C152：百变模仿自己是稀有，不算普通", PAIR, "C41 C152 C95", s="53", chk={"controllerMultiplierNumerator": "25", "controllerMultiplierDenominator": "16"})
    add("C152 罕见与稀有的控制器不算：不触发", PAIR, "C152 C96 C79", s="34", chk={"triggeredSourceIds": []})
    add("C152加两张计分的普通卡：×1.5", PAIR, "C152 C50 C64", s="120", chk={"baseTotal": "40"})
    add("C153开门红 本关第1次提交：×3", PAIR, "C153", s="102")
    add("C153 第2次提交：不触发", PAIR, "C153", srb=2, s="34", chk={"triggeredSourceIds": []})
    add("C41复制C153：×9", PAIR, "C41 C153", s="306")
    add("C150与C153 第1次提交：只有开门红", PAIR, "C150 C153", s="102", chk={"triggeredSourceIds": ["ctl_02"]})
    add("C150与C153 第2次提交：只有渐入佳境", PAIR, "C150 C153", srb=2, s="68", chk={"triggeredSourceIds": ["ctl_01"]})

    # ---- non-scoring new cards ----
    family[0] = "non-scoring"
    add("C110 C111 C112 C113 C114 在场：都不触发、不改分", PAIR, "C110 C111 C112 C113 C114", s="34", chk={"triggeredSourceIds": []})
    add("C115 C116 C120 C121 C128 在场：都不触发、不改分、没有骰面变更", PAIR, "C115 C116 C120 C121 C128", s="34", chk={"triggeredSourceIds": [], "faceChanges": []})
    add("C131 C133 C136 C140 C145 在场（雇佣兵给的临时骰留在池里）：都不触发、不改分", PAIR, "C131 C133 C136 C140 C145", p="6b", perm=5, s="34", chk={"triggeredSourceIds": []})
    add("带基础分附魔的百变模仿复制C133：不触发不加分", PAIR, "C41:base10 C133", s="34", chk={"triggeredSourceIds": []})
    add("百变模仿复制C128：不触发", PAIR, "C41 C128", s="34", chk={"triggeredSourceIds": []})
    add("非计分新卡带免槽/提交/投掷附魔，旁边的排头兵照常", PAIR, "C110:slotless C120:submit1 C128:roll1 C108", s="58", chk={"triggeredSourceIds": ["ctl_04"]})
    add("控制欲把非计分新卡计入件数：5件×2.5", PAIR, "C47 C110 C111 C112 C113", s="85")

    # ---- blue wave ----
    family[0] = "blue wave"
    add("蓝色浪潮：全部视蓝，全蓝×3", PAIR, bwave=True, s="102", chk={"colorId": "all_blue", "colorMult100": 300, "redCount": 0})
    add("蓝色浪潮 三颗红4：红组消失", RED3, bwave=True, s="240", chk={"redCount": 0, "redMult100": 100})
    add("蓝色浪潮加首当其冲：浪潮最后生效，首骰仍视蓝", BLUE, "C03", bwave=True, s="114", chk={"triggeredSourceIds": ["ctl_01"], "colorId": "all_blue"})
    add("蓝色浪潮加蓝色疯狂：没有有效红骰×3", PAIR, "C02", bwave=True, s="306")
    add("蓝色浪潮加蓝色价值：五颗都+6", PAIR, "C39", bwave=True, s="282", chk={"pipContributions": [9, 9, 11, 10, 8]})
    add("蓝色浪潮下红组类卡都不触发", RED2, "C69 C146 C25", bwave=True, s="114", chk={"triggeredSourceIds": []})
    add("蓝色浪潮加场外红人：提交骰没有红组，留池红骰不并入", RED2, "C73", p="1r 4r", bwave=True, s="114", chk={"redCount": 0, "redPoolCount": 0, "triggeredSourceIds": []})
    add("蓝色浪潮加深蓝：没有有效红面，成长", PAIR, "C56:g=4", bwave=True, s="122", chk={"growthChanges": one("ctl_01", "C56", 2)})
    add("蓝色浪潮加飘忽不定蓝色：全部×3", PAIR, "C45:blue", bwave=True, s="306", chk={"pipContributions": [9, 9, 15, 12, 6]})
    add("蓝色浪潮加表里同色：变更仍取永久颜色红", "4r 1r", "C118", bwave=True, s="15",
        chk={"faceChanges": [fc("C118", "ctl_01", "D001", "F03", "color", 0, "red"), fc("C118", "ctl_01", "D002", "F06", "color", 0, "red")]})
    add("蓝色浪潮使红蓝相当失效", "4r 2b", "C18", bwave=True, s="18", chk={"triggeredSourceIds": []})

    # ---- item multiplier ----
    family[0] = "item multiplier"
    add("灵光一闪：得分×2", PAIR, item=2, s="68", chk={"globalFactors": [ITEM], "controllerMultiplierNumerator": "1"})
    add("灵光一闪 乘完再取整：66.5×2=133", RED2, item=2, s="133")
    add("灵光一闪加倍率附魔加稳稳乘数：控制器倍率字段只含×2", PAIR, "C46:mult2", item=2, s="272", chk={"controllerMultiplierNumerator": "2", "controllerMultiplierDenominator": "1"})
    add("灵光一闪加错乱乘积×0.5", PAIR, "C53", rnd={"ctl_01": 1}, item=2, s="34")
    add("灵光一闪加红色浪潮", PAIR, wave=True, item=2, s="238")
    add("灵光一闪加蓝色浪潮", PAIR, bwave=True, item=2, s="204")

    # ---- seals ----
    family[0] = "modifier: seals"
    add("出头鸟：第一颗被封印，贡献0", PAIR, mod="B05", s="28", chk={"sealedDice": [T, F, F, F, F], "pipContributions": [0, 3, 5, 4, 2]})
    add("出头鸟加一马当先：被封印的首骰再计也是0", PAIR, "C64", mod="B05", s="28", chk={"pipRetriggers": [1, 0, 0, 0, 0], "pipSum": "14", "triggeredSourceIds": ["ctl_01"]})
    add("出头鸟加排头兵：整手加值仍按首骰有效点数", PAIR, "C108", mod="B05", s="52", chk={"baseTotal": "26"})
    add("出头鸟：被封印的骰仍参与牌型与红组", RED3, mod="B05", s="160", chk={"redCount": 3, "primaryHandId": "trips"})
    add("出头鸟加少就是多、黄金面：加值与倍率都不产生分数，仍算触发", "1r:gold 3b", "C10", mod="B05", s="3", chk={"pipContributions": [0, 3], "triggeredSourceIds": ["ctl_01"]})
    add("出头鸟加客串明星：被封印的临时骰仍算触发", "5b:temp 3b 3b", "C137", p="1r 2b 6b", mod="B05", s="36", chk={"triggeredSourceIds": ["ctl_01"]})
    add("出头鸟 单骰：基础分只剩未用投掷奖励", "6b", mod="B05", ru=2, rr=1, s="9", chk={"baseTotal": "3"})
    add("出头鸟加千年老二无5点：得分本来就是0", "3b 3b 2b", "C49", mod="B05", s="0")
    add("封奇：有效奇数点被封印", PAIR, mod="B13", s="12", chk={"sealedDice": [T, T, T, F, F]})
    add("封奇 全是偶数：没有骰被封印", "2b 4r 6b", mod="B13", s="12", chk={"sealedDice": [F, F, F]})
    add("封奇加不要奇数：有效点数全为偶数，不封印", PAIR, "C12", mod="B13", s="80", chk={"sealedDice": [F, F, F, F, F]})
    add("封奇加谢幕：未封印的骰照常再计", PAIR, "C68", srb=1, mod="B13", s="24", chk={"pipRetriggers": [1, 1, 1, 1, 1], "pipContributions": [0, 0, 0, 4, 2]})
    add("封偶：有效偶数点被封印", PAIR, mod="B14", s="22", chk={"sealedDice": [F, F, F, T, T]})
    add("封偶加不要奇数：全部变偶数，全部被封印", PAIR, "C12", mod="B14", s="0", chk={"sealedDice": [T, T, T, T, T], "baseTotal": "0"})
    add("封偶 全是奇数：不封印", "1r 3b 5b", mod="B14", s="9")
    add("封小：有效1–3点被封印", PAIR, mod="B15", s="18", chk={"sealedDice": [T, T, F, F, T]})
    add("封小 全是4–6点：不封印", "4r 5b 6b", mod="B15", s="15")
    add("封小加不要奇数：3变4、5变6后都不在1–3", "3b 5b", "C12", mod="B15", s="30", chk={"sealedDice": [F, F]})
    add("封大：有效4–6点被封印", PAIR, mod="B16", s="16", chk={"sealedDice": [F, F, T, T, F]})
    add("封大 全是1–3点：不封印", "1r 2b 3b", mod="B16", s="6")
    add("封大加不要奇数：3变4也被封印", "3b 5b", "C12", mod="B16", s="0", chk={"sealedDice": [T, T]})
    add("封大加66大顺：末位6点被封印，+15不计但仍算触发", "3b 3b 5b 4r 6b", "C20", mod="B16", s="12", chk={"pipContributions": [3, 3, 0, 0, 0], "triggeredSourceIds": ["ctl_01"]})
    add("封红：有效红骰被封印", PAIR, mod="B17", s="26", chk={"sealedDice": [F, F, F, T, F]})
    add("封红加首当其冲：首骰视红后也被封印", PAIR, "C03", mod="B17", s="20", chk={"sealedDice": [T, F, F, T, F]})
    add("封红加红色浪潮：全部被封印", PAIR, wave=True, mod="B17", s="0")
    add("封红 全蓝：不封印", BLUE, mod="B17", s="114")
    add("封红：被封印的红骰仍组成红组", RED2, mod="B17", s="38", chk={"redCount": 2, "redMult100": 175})
    add("封红加蓝色浪潮：全部视蓝，不封印", PAIR, bwave=True, mod="B17", s="102")
    add("封蓝：有效蓝骰被封印", PAIR, mod="B18", s="8", chk={"sealedDice": [T, T, T, F, T]})
    add("封蓝 全红：不封印", "4r 1r", mod="B18", s="10")
    add("封蓝加蓝色浪潮：全部被封印", PAIR, bwave=True, mod="B18", s="0")
    add("封蓝加首当其冲：首骰视红，逃过封印", BLUE, "C03", mod="B18", s="6", chk={"sealedDice": [F, T, T, T, T]})
    add("隔行封印：第1、3、5位被封印，一对×2减半到×1", PAIR, mod="B38", s="7",
        chk={"sealedDice": [T, F, T, F, T], "pipContributions": [0, 3, 0, 4, 0], "handMultBeforeModifier100": 200, "totalHandMult100": 100})
    add("隔行封印 8颗：第1、3、5、7位被封印，四对子×12减半到×6", "3b 3b 5b 5b 2b 2b 6b 6b", cap=8, mod="B38", s="288",
        chk={"sealedDice": [T, F, T, F, T, F, T, F], "handMultBeforeModifier100": 1200, "totalHandMult100": 600})
    add("隔行封印 4颗：第1、3位被封印，一对减半到×1", "3b 3b 5b 4r", mod="B38", s="7")
    add("隔行封印加压轴好戏：末骰被封印，再计无分", PAIR, "C65", mod="B38", s="7", chk={"pipRetriggers": [0, 0, 0, 0, 2]})
    add("隔行封印加好事成双：一对里只有第2位那颗有分", PAIR, "C143", mod="B38", s="10", chk={"pipRetriggers": [1, 1, 0, 0, 0], "pipSum": "10"})
    add("隔行封印 单骰：被封印，散点×1不再减半", "6b", mod="B38", s="0", chk={"totalHandMult100": 100})
    add("隔行封印加点金手：被封印的末骰照常列入骰面变更", PAIR, "C117", mod="B38", s="7", chk={"faceChanges": [fc("C117", "ctl_01", "D005", "F02", "gold", 1)]})
    add("隔行封印加千年老二无5点：得分本来就是0", "3b 3b 2b", "C49", mod="B38", s="0")

    # ---- PipsOnly ----
    family[0] = "modifier: PipsOnly"
    add("釜底抽薪 没有任何加值：结果不变", PAIR, mod="B39", s="34")
    add("釜底抽薪加逐骰加值卡：都算触发但不计加值", "1r 2b 3b 5b 6b", "C10 C13 C17 C39 C94", mod="B39", s="17",
        chk={"pipContributions": [1, 2, 3, 5, 6], "triggeredSourceIds": ["ctl_01", "ctl_02", "ctl_03", "ctl_04", "ctl_05"]})
    add("釜底抽薪加玻璃与黄金：逐骰倍率不计", "6b:gold:glass 3b 3b", mod="B39", s="72", chk={"pipContributions": [6, 3, 3]})
    add("釜底抽薪加始终如一与飘忽不定：逐骰倍率不计", "3b:same 3b 5b", "C93 C45:blue", mod="B39", s="66", chk={"pipContributions": [3, 3, 5]})
    add("釜底抽薪加整手加值卡：对穿肠、有钱真好、有也不交、稳稳分数、排头兵都不计", "3b 3b 5b 4r", "C27 C44 C48 C50 C108", wallet=7, mod="B39", s="30",
        chk={"baseTotal": "15", "controllerBaseAdds": [], "triggeredSourceIds": ["ctl_01", "ctl_02", "ctl_03", "ctl_04", "ctl_05"]})
    add("釜底抽薪加成长加法卡、留池加值卡、越挫越勇、囤积居奇：都不计", PAIR, "C55:g=14 C70 C72 C76 C135", p="1r 6b", inv=2, mod="B39", s="34",
        chk={"controllerBaseAdds": [], "growthChanges": one("ctl_01", "C55", 2), "triggeredSourceIds": ["ctl_01", "ctl_02", "ctl_03", "ctl_04", "ctl_05"]})
    add("釜底抽薪加基础分附魔：+10不计", PAIR, "C46:base10", mod="B39", s="68", chk={"baseTotal": "17"})
    add("釜底抽薪加倍率附魔：照常×2", PAIR, "C46:mult2", mod="B39", s="136")
    add("釜底抽薪加一马当先：有效点数照常再计，少就是多不计", "1r 2b 3b 5b 6b", "C64 C10", mod="B39", s="18", chk={"pipSum": "18"})
    add("釜底抽薪：未用投掷奖励照常", PAIR, ru=1, rr=2, mod="B39", s="46")
    add("釜底抽薪加不要奇数：按有效点数", "1r 3b 5b", "C12", mod="B39", s="12", chk={"pipContributions": [2, 4, 6]})
    add("釜底抽薪：牌型倍率加值与整手倍率照常", RED3, "C85 C30", mod="B39", s="700", chk={"totalHandMult100": 700})
    add("釜底抽薪加顺藤摸瓜与客串明星：加值不计", "1r:temp 2b", "C107 C137", p="3b 4r 5b 6b", mod="B39", s="3", chk={"pipContributions": [1, 2], "triggeredSourceIds": ["ctl_01", "ctl_02"]})
    add("釜底抽薪加纹理大师、纯色主义、66大顺", "3b:smooth 3b:mono 5b 4r 6b", "C91 C92 C20", mod="B39", s="42", chk={"pipContributions": [3, 3, 5, 4, 6]})
    add("釜底抽薪加天生一对：+14不计", "1r 6b", "C80", mod="B39", s="7", chk={"triggeredSourceIds": ["ctl_01"], "controllerBaseAdds": []})

    # ---- EnchantSilence ----
    family[0] = "modifier: EnchantSilence"
    add("沉默：基础分附魔不触发", PAIR, "C46:base10", mod="B06", s="68", chk={"baseTotal": "17", "triggeredSourceIds": ["ctl_01"]})
    add("沉默：倍率附魔不触发", PAIR, "C46:mult2", mod="B06", s="68")
    add("沉默 没有计分附魔：结果不变", PAIR, "C46", mod="B06", s="68")
    add("沉默 带附魔的卡本来就没触发：结果不变", "3b 3b 5b 4r", "C65:base10", mod="B06", s="30")
    add("沉默：投掷、提交附魔不给次数，免槽不受影响", PAIR, "C46:roll1 C50:submit1 C151:slotless", mod="B06", s="370")
    add("沉默加百变模仿：模仿自己的倍率附魔也不触发", PAIR, "C41:mult2 C46", mod="B06", s="136")
    add("万籁俱寂：最左3件失效，其余的附魔失效", PAIR, "C64 C65 C46 C50:base10 C77:mult2", mod="B37", s="148", chk={"triggeredSourceIds": ["ctl_04", "ctl_05"]})
    add("万籁俱寂 只有2件控制器：都失效", PAIR, "C46:mult2 C50:base10", mod="B37", s="34", chk={"triggeredSourceIds": []})
    add("万籁俱寂 剩下的卡没有附魔：沉默不改结果", PAIR, "C64 C65 C46 C50", mod="B37", s="74")

    # ---- GrowthZero / ZeroLevels ----
    family[0] = "modifier: GrowthZero / ZeroLevels"
    add("返璞归真：成长型乘法卡按成长0算，×1仍算触发", PAIR, "C58:g=8", mod="B35", s="34",
        chk={"controllerMultiplierNumerator": "1", "controllerMultiplierDenominator": "1", "triggeredSourceIds": ["ctl_01"]})
    add("返璞归真：成长加法卡+0，不列入加值，成长计划照常", PAIR, "C55:g=14", mod="B35", s="34", chk={"controllerBaseAdds": [], "growthChanges": one("ctl_01", "C55", 2)})
    add("返璞归真：昙花一现不属于成长型，已过1关照常×2", PAIR, "C141:g=1", mod="B35", s="68")
    add("返璞归真：屠龙者、高压锅、物尽其用都×1", PAIR, "C129:g=10 C132:g=12 C134:g=3", mod="B35", s="34", chk={"triggeredSourceIds": ["ctl_01", "ctl_02", "ctl_03"]})
    add("返璞归真加连庄：按成长0计分，成长计划照常+3", PAIR, "C54:g=6", prev="pair", mod="B35", s="34", chk={"growthChanges": one("ctl_01", "C54", 3)})
    add("返璞归真：人员流动的倍率不是成长值，照常×2.5", PAIR, "C51:t=5", mod="B35", s="85")
    add("返璞归真加百变模仿复制成长卡：两个来源都×1", PAIR, "C41 C58:g=8", mod="B35", s="34")
    add("返璞归真：余勇可贾、熟能生巧、千锤百炼、玻璃心、学无止境都按成长0", PAIR, "C59:g=12 C62:g=7 C60:g=10 C90:g=15 C61:g=16", mod="B35", s="34",
        chk={"controllerBaseAdds": [], "controllerMultiplierNumerator": "1"})
    add("返璞归真：祭坛的成长值也按0算", PAIR, "C100:g=12", mod="B35", s="34", chk={"triggeredSourceIds": ["ctl_01"]})
    add("返璞归真：一对2级按0级算", PAIR, lv={"pair": 2}, mod="B35", s="34", chk={"totalHandMult100": 200})
    add("返璞归真：全蓝4级按0级算", BLUE, clv={"all_blue": 4}, mod="B35", s="114")
    add("返璞归真 没有等级也没有成长：结果不变", PAIR, mod="B35", s="34")
    add("打回原形：一对2级按0级", PAIR, lv={"pair": 2}, mod="B28", s="34")
    add("打回原形：全蓝4级按0级", BLUE, clv={"all_blue": 4}, mod="B28", s="114", chk={"colorMult100": 300})
    add("打回原形：顺子3级按0级", STR, lv={"straight": 3}, mod="B28", s="150")
    add("打回原形：一对8级不再压过三条", RED3, lv={"pair": 8}, mod="B28", s="200", chk={"totalHandMult100": 400})
    add("打回原形 没有等级：结果不变", PAIR, mod="B28", s="34")
    add("打回原形加成双成对：加值照加", PAIR, "C84", lv={"pair": 2}, mod="B28", s="59", chk={"totalHandMult100": 350})
    add("打回原形：全红3级按0级", ALLR, clv={"all_red": 3}, mod="B28", s="63")
    add("打回原形：成长值不受影响", PAIR, "C58:g=8", mod="B28", s="47")

    # ---- NoRepeatHand ----
    family[0] = "modifier: NoRepeatHand"
    add("不重样 本关第一次提交：结果不变", PAIR, mod="B21", s="34")
    add("不重样 一对已经打过：牌型倍率×1", PAIR, srb=2, prev="pair", mod="B21", s="17", chk={"handMultBeforeModifier100": 200, "totalHandMult100": 100})
    add("不重样 打过的是三条：一对照常", PAIR, srb=2, prev="trips", mod="B21", s="34")
    add("不重样 散点不受限制", SCAT, srb=2, prev="scatter", mod="B21", s="15")
    add("不重样加成双成对：加值也被压成×1，加值字段照记", PAIR, "C84", srb=2, prev="pair", mod="B21", s="17",
        chk={"handMultAdd100": 150, "handMultBeforeModifier100": 350, "totalHandMult100": 100})
    add("不重样 主牌型是顺子、打过的是三条：不受限", STRT, cap=7, srb=2, prev="trips", mod="B21", s="350", chk={"primaryHandId": "straight", "totalHandMult100": 1400})
    add("不重样 主牌型是顺子、顺子打过：×1", STRT, cap=7, srb=2, prev="straight", mod="B21", s="25", chk={"handMultBeforeModifier100": 1400})
    add("不重样 第三次提交，前两次是一对和三条", PAIR, srb=1, prev="trips", used=["pair", "trips"], mod="B21", s="17")
    add("不重样 上一次是散点，更早打过一对", PAIR, srb=1, prev="scatter", used=["pair"], mod="B21", s="17")
    add("不重样加连庄：牌型倍率×1，连庄照常×1.3并成长", PAIR, "C54:g=6", srb=2, prev="pair", mod="B21", s="22",
        chk={"totalHandMult100": 100, "growthChanges": one("ctl_01", "C54", 3)})
    add("没有不重样减益时，本关打过的牌型照常计分", PAIR, srb=2, prev="pair", s="34", chk={"totalHandMult100": 200, "handMultBeforeModifier100": 200})

    # ---- HandHalf ----
    family[0] = "modifier: HandHalf"
    add("半吊子 一对×2减半到×1", PAIR, mod="B19", s="17", chk={"handMultBeforeModifier100": 200, "totalHandMult100": 100})
    add("半吊子 一对1级×2.75：整数除法得×1.37", PAIR, lv={"pair": 1}, mod="B19", s="23", chk={"totalHandMult100": 137})
    add("半吊子 散点：×1保底，结果不变", SCAT, mod="B19", s="15", chk={"totalHandMult100": 100})
    add("半吊子 三条×4减半到×2", RED3, mod="B19", s="100")
    add("半吊子加成双成对：先加后减半 3.5→1.75", PAIR, "C84", mod="B19", s="29", chk={"handMultBeforeModifier100": 350, "totalHandMult100": 175})
    add("半吊子 一对1级加顺子：12.75→6.37", STRP, cap=6, lv={"pair": 1}, mod="B19", s="127", chk={"handMultBeforeModifier100": 1275, "totalHandMult100": 637})
    add("半吊子加一条龙：20→10", STR, "C142", mod="B19", s="150", chk={"handMultBeforeModifier100": 2000, "totalHandMult100": 1000, "handMultAdd100": 1000})

    # ---- SmallHandsOnly ----
    family[0] = "modifier: SmallHandsOnly"
    add("小打小闹 三条带一对：只认三条", FULL, mod="B20", s="228", chk={"primaryHandId": "trips", "totalHandMult100": 400})
    add("小打小闹 四条：只认三条", QUAD, mod="B20", s="264", chk={"primaryHandId": "trips"})
    add("小打小闹加满堂彩：三条带一对不成立，不触发", FULL, "C82", mod="B20", s="228", chk={"triggeredSourceIds": []})
    add("小打小闹加三三得九：三对子不成立，按两对算", "1r 1r 2b 2b 3b 3b", "C86", cap=6, mod="B20", s="63", chk={"primaryHandId": "two_pair", "triggeredSourceIds": []})
    add("小打小闹加四平八稳：读同点数量，照常触发", QUAD, "C31", mod="B20", s="792", chk={"triggeredSourceIds": ["ctl_01"]})
    add("小打小闹加得寸进尺：读同点数量，照常×1.5", QUAD, "C87", mod="B20", s="396")
    add("小打小闹 豹子加三足鼎立：按三条4+3", "5b 5b 5b 5b 5b", "C85", mod="B20", s="525", chk={"primaryHandId": "trips", "totalHandMult100": 700})
    add("小打小闹 一对：结果不变", PAIR, mod="B20", s="34")
    add("小打小闹 三条：结果不变", RED3, mod="B20", s="200")
    add("小打小闹加对对穿肠：读成对点数种类，照常", FULL, "C28", mod="B20", s="684")
    add("小打小闹加连庄：主牌型成了三条，与上次的三条带一对不同", FULL, "C54:g=6", prev="full_house", mod="B20", s="296", chk={"growthChanges": one("ctl_01", "C54", 0, True)})
    add("小打小闹 两对3级压过三条：计分取两对，主牌型仍是三条", FULL, lv={"two_pair": 3}, mod="B20", s="342", chk={"primaryHandId": "trips", "totalHandMult100": 600})
    add("小打小闹 双三条：只认三条", "2b 2b 2b 5b 5b 5b", cap=6, mod="B20", s="252", chk={"primaryHandId": "trips"})
    add("小打小闹 四对子：只认两对", "1r 1r 2b 2b 3b 3b 5b 5b", cap=8, mod="B20", s="115", chk={"primaryHandId": "two_pair"})

    # ---- NoStraight ----
    family[0] = "modifier: NoStraight"
    add("禁顺 顺子不成立：变成散点", STR, mod="B08", s="15", chk={"primaryHandId": "scatter", "totalHandMult100": 100})
    add("禁顺加顺水推舟：不触发", STR, "C83", mod="B08", s="15", chk={"triggeredSourceIds": []})
    add("禁顺加一条龙：不触发，没有加值", STR, "C142", mod="B08", s="15", chk={"triggeredSourceIds": [], "handMultAdd100": 0})
    add("禁顺加带基础分附魔的简单顺子：顺子不成立，简单顺子不触发，附魔+10也不加", "2b 3b 4r 5b", "C29:base10", mod="B08", s="14",
        chk={"triggeredSourceIds": [], "primaryHandId": "scatter", "baseTotal": "14"})
    add("禁顺加简单顺子、顺水推舟、一条龙：三张都不触发", "2b 3b 4r 5b", "C29 C83 C142", mod="B08", s="14",
        chk={"triggeredSourceIds": [], "handMultAdd100": 0, "primaryHandId": "scatter"})
    add("禁顺 一对加顺子：只剩一对", STRP, cap=6, mod="B08", s="40", chk={"primaryHandId": "pair"})
    add("禁顺 没有顺子：结果不变", PAIR, mod="B08", s="34")
    add("禁顺加散兵游勇：主牌型成了散点，触发", STR, "C81", mod="B08", s="45")
    add("禁顺加杂牌军与顺藤摸瓜：只读点数，不受影响", STR, "C88 C107", mod="B08", s="165", chk={"triggeredSourceIds": ["ctl_01", "ctl_02"]})

    # ---- NoRed / NoBlue ----
    family[0] = "modifier: NoRed / NoBlue"
    add("褪红：红组倍率固定×1，数量照记", RED2, mod="B22", s="38", chk={"redCount": 2, "redMult100": 100})
    add("褪红 全红：颜色倍率×1，颜色牌型照记", ALLR, mod="B22", s="18", chk={"colorId": "all_red", "colorMult100": 100})
    add("褪红 只有1颗红骰且不是全红：结果不变", PAIR, mod="B22", s="34")
    add("褪红 全蓝不受影响", BLUE, mod="B22", s="114")
    add("褪红加场外红人加锦上添红：都算触发，数量5，倍率×1", "4r 4r 4r", "C73 C146", p="1r", mod="B22", s="48",
        chk={"redCount": 5, "redPoolCount": 1, "redBonusCount": 1, "redMult100": 100, "triggeredSourceIds": ["ctl_01", "ctl_02"]})
    add("褪红加红色共鸣：再计照常", RED2, "C69", mod="B22", s="54")
    add("褪红加红火：红组3颗照常成长", RED3, "C57:g=3", mod="B22", s="92", chk={"growthChanges": one("ctl_01", "C57", 3)})
    add("褪红加红色浪潮：全红×1、红组×1", PAIR, wave=True, mod="B22", s="34")
    add("褪蓝：全蓝倍率×1，颜色牌型照记", BLUE, mod="B23", s="38", chk={"colorId": "all_blue", "colorMult100": 100})
    add("褪蓝 不是全蓝：结果不变", PAIR, mod="B23", s="34")
    add("褪蓝 全红不受影响", ALLR, mod="B23", s="63")
    add("褪蓝加蓝色浪潮", PAIR, bwave=True, mod="B23", s="34")
    add("褪蓝加蓝色疯狂：照常触发", BLUE, "C02", mod="B23", s="114")
    add("色盲 全蓝：×1", BLUE, mod="B36", s="38")
    add("色盲 红组2：×1", RED2, mod="B36", s="38")
    add("色盲 全红且红组2：都×1", ALLR, mod="B36", s="18")
    add("色盲 混色且没有红组奖励：结果不变", PAIR, mod="B36", s="34")

    # ---- FirstHalf / LaterHalf ----
    family[0] = "modifier: FirstHalf / LaterHalf"
    add("慢热 第1次提交：得分减半", PAIR, mod="B26", s="17", chk={"globalFactors": [half("B26")]})
    add("慢热 第2次提交：不减半", PAIR, srb=2, mod="B26", s="34", chk={"globalFactors": []})
    add("慢热加开门红：×3再减半，控制器倍率字段不含减半", PAIR, "C153", mod="B26", s="51", chk={"controllerMultiplierNumerator": "3", "controllerMultiplierDenominator": "1"})
    add("慢热加灵光一闪：两个系数按顺序列出", PAIR, item=2, mod="B26", s="34", chk={"globalFactors": [ITEM, half("B26")]})
    add("慢热 奇数分：最后只取整一次 15÷2=7", SCAT, mod="B26", s="7")
    add("慢热 66.5减半", RED2, mod="B26", s="33")
    add("强弩之末 第1次提交：不减半", PAIR, mod="B30", s="34", chk={"globalFactors": []})
    add("强弩之末 第2次提交：减半", PAIR, srb=2, mod="B30", s="17", chk={"globalFactors": [half("B30")]})
    add("强弩之末 第3次提交加渐入佳境：×3再减半", PAIR, "C150", srb=1, mod="B30", s="51")
    add("强弩之末 第3次提交加背水一战", PAIR, "C102", srb=1, mod="B30", s="51")

    # ---- session-only modifiers: nothing changes in scoring ----
    family[0] = "modifier: session-only"
    add("满员（会话类）：计分不变", PAIR, mod="B01", s="34")
    add("徒手（会话类）：囤积居奇照常数背包", PAIR, "C135", inv=2, mod="B02", s="74")
    add("苛税（会话类）：不计入goldSpent，有钱真好读冻结钱包", PAIR, "C44", wallet=10, mod="B03", s="54", chk={"goldSpent": 0})
    add("白干（会话类）", PAIR, mod="B04", s="34")
    add("停滞（会话类）：成长计划照常给出，由会话决定不执行", PAIR, "C55:g=14", mod="B07", s="62", chk={"growthChanges": one("ctl_01", "C55", 2)})
    add("急行军（会话类）：每手2次投掷，剩1次", PAIR, ru=1, rr=1, mod="B09", s="40")
    add("窄门（会话类）：容量4，提交4颗即占满", "3b 3b 5b 4r", "C65", mod="B10", s="46", chk={"pipRetriggers": [0, 0, 0, 2]})
    add("厚墙（会话类）", PAIR, mod="B11", s="34")
    add("过路费（会话类）：有钱真好读扣款后的冻结钱包", PAIR, "C44", wallet=6, mod="B12", s="46")
    add("削级（会话类）：按快照里的等级计分", PAIR, lv={"pair": 2}, mod="B24", s="59")
    add("惜字如金（会话类）：本关2次提交，第2次即最后一次", PAIR, "C102", srb=1, mod="B27", s="102")
    add("天堑（会话类）", PAIR, mod="B31", s="34")
    add("孤掷（会话类）：每手1次投掷，一锤定音触发", PAIR, "C75", mod="B32", s="102")
    add("破釜沉舟（会话类）：本关只有1次提交，既是第一次也是最后一次", PAIR, "C68 C153", mod="B33", s="204")
    add("独木桥（会话类）：容量3，提交3颗不算少数派", "3b 3b 5b", "C08", mod="B34", s="66", chk={"triggeredSourceIds": []})

    # ---- disabled controllers ----
    family[0] = "disabled controllers"
    add("缴械：最左2件失效，只有第3件生效", PAIR, "C46 C50 C64", mod="B25", s="40", chk={"triggeredSourceIds": ["ctl_03"]})
    add("缴械：失效控制器的附魔也不触发", PAIR, "C46:mult2 C50:base10 C64", mod="B25", s="40")
    add("缴械：百变模仿自己失效，它的目标照常", PAIR, "C50 C41 C46", mod="B25", s="68", chk={"triggeredSourceIds": ["ctl_03"]})
    add("缴械：百变模仿与目标都失效", PAIR, "C41 C46 C64", mod="B25", s="40", chk={"triggeredSourceIds": ["ctl_03"]})
    add("缴械加控制欲：失效的仍计入件数，3件×1.5", PAIR, "C50 C46 C47", mod="B25", s="51")
    add("缴械加留白：失效的仍占栏位，空2个×2", PAIR, "C50 C46 C151", mod="B25", s="68")
    add("缴械加留白：失效的免槽卡仍不占栏位，空3个×2.5", PAIR, "C50:slotless C46 C151", mod="B25", s="85")
    add("缴械加草根逆袭：失效的普通卡仍计入，2张×1.5", PAIR, "C50 C64 C152", mod="B25", s="51")
    add("缴械：失效的不要奇数与首当其冲不改有效点数和颜色", "1r 3b 5b", "C12 C03 C13", mod="B25", s="21", chk={"pipContributions": [5, 7, 9]})
    add("缴械：失效的场外红人与锦上添红不加红组", RED2, "C73 C146 C69", p="1r", mod="B25", s="94", chk={"redCount": 2, "redBonusCount": 0, "redPoolCount": 0})
    add("缴械：失效的成长卡不参与计分，也不产生成长计划", BLUE, "C55:g=14 C56:g=4 C64", mod="B25", s="132", chk={"growthChanges": [], "controllerBaseAdds": []})
    add("缴械：失效的一掷千金不扣款，有钱真好不加分", PAIR, "C138 C44 C46", wallet=9, mod="B25", s="68", chk={"goldSpent": 0})
    add("缴械：失效的改骰卡不产生变更", PAIR, "C117 C118 C124", mod="B25", s="34",
        chk={"faceChanges": [fc("C124", "ctl_03", f"D00{i}", f, "weightDelta", 1) for i, f in ((1, "F03"), (2, "F03"), (3, "F05"), (4, "F04"), (5, "F02"))]})
    add("缴械 只有1件控制器", PAIR, "C46", mod="B25", s="34", chk={"triggeredSourceIds": []})
    add("缴械：失效的简化提交不改容量也不乘分", PAIR, "C09 C46 C64", mod="B25", s="40")
    add("缴械加逆流而上：第3件照常×3", PAIR, "C50 C46 C130", mod="B25", s="102")
    add("缴械：失效的简单顺子不放宽顺子，四连不成顺子", "2b 3b 4r 5b", "C29 C46 C83", mod="B25", s="14", chk={"triggeredSourceIds": [], "primaryHandId": "scatter"})
    add("凡人：稀有控制器（蓝色疯狂、百变模仿）失效，其余照常", BLUE, "C02 C46 C41 C64", mod="B29", s="264", chk={"triggeredSourceIds": ["ctl_02", "ctl_04"]})
    add("凡人 没有稀有控制器：没有失效", PAIR, "C46 C64", mod="B29", s="80")
    add("凡人：控制欲自己是稀有，失效", PAIR, "C47 C46", mod="B29", s="68", chk={"triggeredSourceIds": ["ctl_02"]})
    add("凡人加草根逆袭：普通卡照常计入", PAIR, "C152 C50 C02", mod="B29", s="92")
    add("万籁俱寂加控制欲：3件失效仍计入，4件×2", PAIR, "C64 C65 C46 C47", mod="B37", s="68")
    add("合成快照（正常流程不可达）：百变模仿未失效而目标失效，模仿没有技能", PAIR, "C41 C46 C64", mod="B25", dis=["ctl_02"], s="40", chk={"triggeredSourceIds": ["ctl_03"]})

    # ---- enchantments on new cards ----
    family[0] = "enchant"
    add("基础分附魔在C104上，触发时+10", "2b 4r 3b", "C104:base10", s="57")
    add("基础分附魔在C104上，未触发不加", "2b 3b 4r", "C104:base10", s="9")
    add("倍率附魔在C138上，钱包够付", PAIR, "C138:mult2", wallet=3, s="204")
    add("倍率附魔在C138上，钱包不够", PAIR, "C138:mult2", wallet=2, s="34")
    add("一掷千金两个来源只够一个：倍率附魔在没触发的真卡上，不乘", PAIR, "C41 C138:mult2", wallet=4, s="102", chk={"triggeredSourceIds": ["ctl_01"]})
    add("一掷千金两个来源只够一个：倍率附魔在触发的模仿上，×2", PAIR, "C41:mult2 C138", wallet=4, s="204")
    add("基础分附魔在C117上（总是触发）", PAIR, "C117:base10", s="54")
    add("倍率附魔在百变模仿上，复制的开门红触发", PAIR, "C41:mult2 C153", s="612")
    add("倍率附魔在百变模仿上，复制的开门红未触发", PAIR, "C41:mult2 C153", srb=2, s="34")
    add("基础分附魔在C146上，有红组", RED2, "C146:base10", s="145")
    add("基础分附魔在C146上，没有红骰", BLUE, "C146:base10", s="114")
    add("倍率附魔在C141上 已过2关：×1.5×2", PAIR, "C141:g=2:mult2", s="102")
    add("基础分附魔在C126上，没有留池骰", PAIR, "C126:base10", s="34")
    add("基础分附魔在C126上，有留池骰", PAIR, "C126:base10", p="6b", s="54")
    add("倍率附魔在C135上，背包为空", PAIR, "C135:mult2", s="34")
    add("倍率附魔在C135上，背包1个", PAIR, "C135:mult2", inv=1, s="108")
    add("提交附魔在C150上：本关4次提交，第4次×4", PAIR, "C150:submit1", srb=1, s="136")

    # ---- larger builds ----
    family[0] = "large build"
    add("顺序流：对称2-3-4-3-2，六张顺序与再计卡", "2b 3b 4r 3b 2b", "C106 C107 C108 C109 C143 C153", s="1620",
        chk={"pipContributions": [2, 13, 14, 3, 2], "pipRetriggers": [1, 1, 2, 1, 1], "pipSum": "82", "baseTotal": "90", "controllerMultiplierNumerator": "6"})
    add("金币流：钱包47，一掷千金、富甲一方、有钱真好、模仿昙花一现、囤积居奇", RED2, "C138 C139 C44 C41 C141:g=1 C135", wallet=47, inv=3, s="8064",
        chk={"baseTotal": "96", "goldSpent": 3, "controllerMultiplierNumerator": "24", "controllerMultiplierDenominator": "1"})
    add("首领关强弩之末 第3次提交：逆流而上、渐入佳境、背水一战、谢幕、屠龙者", FULL, "C130 C150 C102 C68 C129:g=10", srb=1, prev="pair", mod="B30", s="13851",
        chk={"pipSum": "38", "controllerMultiplierNumerator": "81", "controllerMultiplierDenominator": "2", "globalFactors": [half("B30")]})
    add("终局釜底抽薪：五颗6点，加值全不计，倍率与再计照常", "6b:gold 6b:glass 6b 6b:temp 6b", "C67 C87 C137 C147 C55:g=40 C50:mult2 C31", p="6b", mod="B39", s="36450",
        chk={"baseTotal": "60", "controllerBaseAdds": [], "controllerMultiplierNumerator": "135", "controllerMultiplierDenominator": "16",
             "triggeredSourceIds": [f"ctl_0{i}" for i in range(1, 8)]})
    add("终局万籁俱寂：单骰，三件失效仍计入草根逆袭与留白的栏位", "6b", "C46:mult2 C50 C64 C144:base10 C149 C151:slotless C152", slots=7, mod="B37", s="486",
        chk={"triggeredSourceIds": ["ctl_04", "ctl_05", "ctl_06", "ctl_07"], "controllerMultiplierNumerator": "27"})
    add("红色流：泾渭分明、锦上添红、场外红人、红色共鸣、红火、好事成双、观众席", "4r 4r 4r 2b 2b", "C105 C146 C73 C69 C57:g=12 C143 C147", p="4r 1r 6b", s="7526",
        chk={"redCount": 6, "redPoolCount": 2, "redBonusCount": 1, "pipSum": "32", "controllerMultiplierNumerator": "28", "controllerMultiplierDenominator": "5",
             "growthChanges": one("ctl_05", "C57", 3)})
    if mismatches:
        raise ValueError("hand-computed expectations disagree with the reference scorer:\n  " + "\n  ".join(mismatches))
    return cases, families, sorted(remote_ids), sorted(synthetic_ids)


def render(cfg, cases):
    dump = lambda o: json.dumps(o, ensure_ascii=False, separators=(",", ":"))  # noqa: E731
    lines = ['{"schema":%s,"configVersion":%s,"cases":[' % (dump(SCHEMA), dump(cfg["configVersion"]))]
    for i, c in enumerate(cases):
        lines.append('{"id":%s,"description":%s,\n "input":%s,\n "expected":%s}%s' % (
            dump(c["id"]), dump(c["description"]), dump(c["input"]), dump(c["expected"]), "," if i + 1 < len(cases) else ""))
    lines.append("]}")
    return "\n".join(lines) + "\n"


def coverage(cfg, cases, families):
    """Coverage required by the task contract; returns the tables and the list of gaps."""
    non_submit = set(cfg["enchantRules"]["nonSubmitControllerIds"])
    scoring = [cid for cid in NEW_IDS if cid not in non_submit]
    passive = [cid for cid in NEW_IDS if cid in non_submit]
    table = {cid: {"mentions": 0, "hit": 0, "miss": 0} for cid in NEW_IDS}
    mentions = Counter(c["definitionId"] for case in cases for c in case["input"]["controllers"])
    problems = []
    for f in families:
        fired = set(f["triggered"])
        by_ability = {}
        for emitter, ability in f["sources"]:
            if ability in table:
                by_ability.setdefault(ability, []).append(emitter in fired)
        for ability, flags in by_ability.items():
            table[ability]["hit" if any(flags) else "miss"] += 1
            if ability in ALWAYS_V10 and not all(flags):
                problems.append(f"{f['id']}: {ability} is listed as always triggering but did not")
    for cid in table:
        table[cid]["mentions"] = mentions[cid]
    for cid in scoring:
        if table[cid]["mentions"] < 2 or table[cid]["hit"] < 1 or (cid not in ALWAYS_V10 and table[cid]["miss"] < 1):
            problems.append(cid)
    for cid in passive:
        if table[cid]["hit"] or not table[cid]["mentions"]:
            problems.append(cid)

    effects = {code: {"cases": 0, "effect": 0, "neutral": 0, "scoreNeutral": 0} for code in SCORING_EFFECTS}
    modifiers = {m["id"]: {"effects": m["effects"], "cases": 0,
                           "byEffect": {e: {"effect": 0, "neutral": 0} for e in m["effects"] if e in SCORING_EFFECTS}} for m in cfg["bossModifiers"]}
    for f in families:
        if f["modifier"]:
            modifiers[f["modifier"]]["cases"] += 1
        for code, outcome in f["effects"].items():
            kind = "effect" if outcome.startswith("effect") else "neutral"
            effects[code]["cases"] += 1
            effects[code][kind] += 1
            modifiers[f["modifier"]]["byEffect"][code][kind] += 1
            if outcome != "effect":
                effects[code]["scoreNeutral"] += 1
    for code, row in effects.items():
        quiet = row["scoreNeutral"] if code in ALWAYS_EFFECTIVE else row["neutral"]
        if row["effect"] < 1 or quiet < 1:
            problems.append(code)
    for mid, row in modifiers.items():
        if not row["cases"] and any(e in SCORING_EFFECTS for e in row["effects"]):
            problems.append(mid)
        # A modifier with several scoring effects must have each of them exercised under that modifier.
        problems += [f"{mid}/{code}" for code, seen in row["byEffect"].items() if seen["effect"] < 1]
    used_codes = {e for m in cfg["bossModifiers"] for e in m["effects"]}
    problems += [f"effect code {e} is not used by any modifier" for e in SCORING_EFFECTS if e not in used_codes]
    return table, dict(sorted(mentions.items(), key=lambda kv: int(kv[0][1:]))), effects, modifiers, problems


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def main():
    mode = sys.argv[1] if len(sys.argv) > 1 else "verify"
    if mode not in ("verify", "write", "check"):
        raise SystemExit("usage: v10_golden.py verify|write|check")
    legacy = [verify_legacy(CONFIG_P08, "a"), verify_legacy(CONFIG_P09, "b"), verify_legacy(CONFIG, "c"),
              verify_v09(CONFIG_P09, "a"), verify_v09(CONFIG, "b")]
    if not all(r["all_fields_exact"] for r in legacy):
        raise SystemExit("legacy check failed; refusing to continue")
    if mode == "verify":
        return
    cfg = load(CONFIG)
    if cfg["configVersion"] != CONFIG_VERSION:
        raise SystemExit(f"unexpected configuration version {cfg['configVersion']}")
    cases, families, remote_ids, synthetic_ids = build_cases(cfg)
    table, mentions, effects, modifiers, problems = coverage(cfg, cases, families)
    if problems:
        raise SystemExit(f"coverage gap: {problems}")
    text = render(cfg, cases)
    if json.loads(text)["cases"] != cases:
        raise SystemExit("rendered file does not round-trip")
    family_counts = Counter(f["family"] for f in families)
    provenance = json.dumps({
        "generator": "tools/v10_golden.py",
        "schema": SCHEMA,
        "configVersion": cfg["configVersion"],
        "derived_from_production_code": False,
        "rules": rel(RULES),
        "generator_sha256": sha256(Path(__file__)),
        "rules_sha256": sha256(RULES),
        "rules_v09_sha256": sha256(RULES_V09),
        "config_sha256": sha256(CONFIG),
        "baseline_sha256": {rel(p): sha256(p) for p in (CONFIG_P08, CONFIG_P09, LEGACY_VECTORS, V09_VECTORS)},
        "golden": rel(GOLDEN),
        "golden_sha256": hashlib.sha256(text.encode("utf-8")).hexdigest(),
        "cases": len(cases),
        "cases_with_hand_computed_assertions": sum(1 for f in families if f["handChecked"]),
        "cases_with_modifier": sum(1 for f in families if f["modifier"]),
        "legacy_checks": legacy,
        "cases_per_family": dict(family_counts),
        "remote_control_cases": remote_ids,
        "synthetic_disabled_cases": synthetic_ids,
        "cases_mentioning_controller": mentions,
        "new_controller_coverage": table,
        "always_triggering_new_controllers": sorted(ALWAYS_V10, key=lambda c: int(c[1:])),
        "scoring_effect_coverage": effects,
        "always_effective_codes": list(ALWAYS_EFFECTIVE),
        "modifier_coverage": modifiers,
        "spec_readings_resolved": RESOLVED_READINGS,
        "spec_readings_open": OPEN_READINGS,
    }, ensure_ascii=False, indent=2) + "\n"
    if mode == "check":
        same = GOLDEN.exists() and GOLDEN.read_text(encoding="utf-8") == text
        same_evidence = EVIDENCE.exists() and EVIDENCE.read_text(encoding="utf-8") == provenance
        print("golden file matches generator" if same else "golden file DIFFERS from generator")
        print("provenance matches generator" if same_evidence else "provenance DIFFERS from generator")
        raise SystemExit(0 if same and same_evidence else 1)
    GOLDEN.write_text(text, encoding="utf-8", newline="\n")
    EVIDENCE.parent.mkdir(parents=True, exist_ok=True)
    EVIDENCE.write_text(provenance, encoding="utf-8", newline="\n")
    print(f"wrote {rel(GOLDEN)}: {len(cases)} cases, {sum(1 for f in families if f['handChecked'])} with hand-computed assertions")
    print("families:", dict(family_counts))


if __name__ == "__main__":
    main()
