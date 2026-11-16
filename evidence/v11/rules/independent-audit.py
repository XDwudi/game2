"""Read-only, rules-derived arithmetic audit of the v11 duplicate vectors.

This reviewer script reads only JSON, never imports a reference scorer or reads
C#. Formulas come from P0_v0.6 sections 7/11/12 and Appendix C, v08 RULES_DELTA
section 2.4, v09 RULES_SPEC sections 2/4, v10 RULES_SPEC sections 5/6, and v11
RULES_DELTA section 1.1 item 7. The implementation-derived section 4 is not an
oracle. Run from any directory; results and input hashes are printed as JSON.
"""
import hashlib
import json
from collections import Counter
from fractions import Fraction as F
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
DATA = ROOT / "DicePrototype/Assets/Game/Data"
GOLDEN = DATA / "GoldenScoresV11.json"
CONFIG = DATA / "P0Config.json"
FAMILIES = {"two_hit", "three_hit", "mimic_hit", "two_miss"}
FIXED = set("C02 C06 C08 C09 C11 C14 C18 C28 C30 C31 C32 C33 C40 C42 C46 C74 C75 C77 C81 C82 C83 C86 C88 C101 C102 C104 C105 C106 C130 C138 C144 C149 C153".split())
GROWTH_MULT = set("C54 C56 C57 C58 C60 C61 C90 C100 C129 C132 C134".split())
DYNAMIC = set("C47 C49 C51 C53 C63 C71 C87 C89 C103 C139 C141 C147 C148 C150 C151 C152".split()) | GROWTH_MULT
ADDS = set("C10 C13 C17 C19 C20 C39 C91 C92 C94 C107 C137".split())
GROWTH_STEPS = {"C54": 3, "C55": 2, "C56": 2, "C57": 3, "C58": 4,
                "C59": 4, "C60": 2, "C61": 2, "C62": 1, "C90": 15,
                "C100": 2, "C129": 8, "C132": 8, "C134": 2}


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def current_faces(dice):
    return [next(f for f in d["faces"] if f["faceId"] == d["faceId"]) for d in dice]


def owners(cards):
    # Frozen adjacency is the binding convention of these vectors. Copies of a
    # mimic chain reuse the ultimate real target; each emitter remains a source.
    for i, card in enumerate(cards):
        while card["definitionId"] == "C41":
            i += 1
            require(i < len(cards), "audit fixture has an unbound mimic")
            card = cards[i]
        yield card


def multiplier(case):
    e = case["expected"]
    return F(int(e["controllerMultiplierNumerator"]), int(e["controllerMultiplierDenominator"]))


def audit():
    data = json.loads(GOLDEN.read_text(encoding="utf-8"))
    cfg = json.loads(CONFIG.read_text(encoding="utf-8"))
    cases = data["cases"]
    definitions = {c["id"]: c for c in cfg["controllers"]}
    counts = Counter()
    checked = set()
    require(data["schema"] == "v11-golden-1" and data["configVersion"] == "p1.1.0", "schema/version")
    require(len({c["id"] for c in cases}) == len(cases), "duplicate case ID")
    require(set(data["pureMultiplierCards"]) == FIXED | DYNAMIC, "global multiplier inventory")
    require(set(data["perDieAdditionCards"]) == ADDS, "per-die addition inventory")

    for case in cases:
        cid, family = case["cardId"], case["family"]
        if cid not in FIXED or family not in FAMILIES:
            continue
        # These recipes isolate the named multiplier, without other multiplier
        # helpers. The miss family naturally fails that card's condition.
        copies = 0 if family == "two_miss" else 2 if family == "two_hit" else 3
        expected = F(definitions[cid]["parameters"]["scoreMultiplier"]) ** copies
        require(expected == multiplier(case), f"{case['id']}: fixed multiplier")
        counts["fixed_global_multiplier_assertions"] += 1
        checked.add(case["id"])

    for case in cases:
        if case["cardId"] not in DYNAMIC or case["family"] not in FAMILIES:
            continue
        inp = case["input"]
        cards, dice, pool = inp["controllers"], inp["dice"], inp["pool"]
        pips, faces = [d["pip"] for d in dice], current_faces(dice)
        result = F(1)
        for card in owners(cards):
            cid, factor = card["definitionId"], F(1)
            require(cid in DYNAMIC | {"C46", "C50"}, f"unsupported helper {cid}")
            if cid in GROWTH_MULT:
                factor = F(20 + card["growth"], 20)
            elif cid == "C47":
                factor = F(len(cards), 2)
            elif cid == "C49":
                factor = F(pips.count(5))
            elif cid == "C51":
                factor = F(card["turnoverUnits2"], 2)
            elif cid == "C53":
                factor = F(next(r["units2"] for r in inp["randomChoices"] if r["ownerInstanceId"] == card["instanceId"]), 2)
            elif cid == "C63":
                factor = max(F(1), F(3 * inp["permanentDiceCount"], 10))
            elif cid == "C71":
                factor = F(3, 2) ** sum(d["pip"] == 6 for d in pool)
            elif cid == "C87":
                factor = F(3, 2) ** max(0, max(Counter(pips).values()) - 3)
            elif cid == "C89":
                factor = F(3, 2) ** sum(f["gold"] for f in faces)
            elif cid == "C103":
                factor = F(4 if inp["permanentDiceCount"] <= 3 else 2 if inp["permanentDiceCount"] <= 4 else 1)
            elif cid == "C139":
                factor = F(4 + min(8, inp["wallet"] // 10), 4)
            elif cid == "C141":
                factor = F(5 - card["growth"], 2)
            elif cid == "C147":
                factor = F(4 + len(pool), 4)
            elif cid == "C148":
                factor = F(2 + inp["rollsRemaining"], 2)
            elif cid == "C150":
                factor = F(inp["submissionOrdinal"] + 1)
            elif cid == "C151":
                factor = F(2 + max(0, inp["ordinaryControllerSlots"] - sum(c["enchantmentId"] != "slotless" for c in cards)), 2)
            elif cid == "C152":
                factor = F(4 + sum(definitions[c["definitionId"]]["rarity"] == "common" for c in cards), 4)
            elif cid == "C46":
                factor = F(2)
            result *= factor
        require(result == multiplier(case), f"{case['id']}: dynamic multiplier")
        counts["dynamic_global_multiplier_assertions"] += 1
        checked.add(case["id"])

    for case in cases:
        cid, family = case["cardId"], case["family"]
        if cid not in ADDS or family not in FAMILIES:
            continue
        inp = case["input"]
        dice = inp["dice"]
        copies = 3 if family in ("three_hit", "mimic_hit") else 2
        result = []
        for i, (die, face) in enumerate(zip(dice, current_faces(dice))):
            pip, addition = die["pip"], 0
            if cid == "C10" and pip == 1: addition = 10
            if cid == "C13" and pip % 2: addition = 4
            if cid == "C17" and pip <= 3: addition = 6
            if cid == "C19" and sum(f["pip"] for f in die["faces"]) <= 13: addition = 8
            if cid == "C20" and i == len(dice) - 1 and len(dice) == inp["capacity"] and pip == 6: addition = 15
            if cid == "C39" and face["color"] == "blue": addition = 6
            if cid == "C91" and face["texture"] in ("smooth", "rough"): addition = 8
            if cid == "C92" and len({f["color"] for f in die["faces"]}) == 1: addition = 10
            # The permanent opposite pairs are F01/F06, F02/F05, F03/F04.
            if cid == "C94": addition = die["faces"][6 - int(die["faceId"][-1])]["pip"]
            if cid == "C107" and i > 0 and pip == dice[i - 1]["pip"] + 1: addition = 10
            if cid == "C137" and die["lifetime"] == "stage_temporary": addition = 15
            result.append(pip + copies * addition)
        require(result == case["expected"]["pipContributions"], f"{case['id']}: per-die additions")
        counts["per_die_addition_assertions"] += 1
        checked.add(case["id"])

    for case in cases:
        if case["cardId"] not in GROWTH_STEPS:
            continue
        result, base_add = F(1), 0
        for card in owners(case["input"]["controllers"]):
            cid = card["definitionId"]
            require(card["growth"] % GROWTH_STEPS[cid] == 0, f"{case['id']}: unreachable growth increment")
            if cid in {"C55", "C59", "C62"}:
                base_add += card["growth"]
            else:
                result *= F(20 + card["growth"], 20)
        require(result == multiplier(case), f"{case['id']}: separate growth owner product")
        if base_add:
            require(int(case["expected"]["baseTotal"]) == sum(d["pip"] for d in case["input"]["dice"]) + base_add,
                    f"{case['id']}: separate growth owner sum")
        counts["growth_fixture_assertions"] += 1
        checked.add(case["id"])

    # Pin nonzero, expected coverage so a skipped family cannot report success.
    expected_counts = {"fixed_global_multiplier_assertions": 128, "dynamic_global_multiplier_assertions": 91,
                       "per_die_addition_assertions": 43, "growth_fixture_assertions": 56}
    require(dict(counts) == expected_counts, f"audit coverage changed: {dict(counts)}")
    return {"result": "PASS", "source": "independent rules formulas; JSON only, no scorer imports",
            "cases_in_file": len(cases), "scoring_cards": len(data["scoringCards"]),
            "assertion_groups": dict(counts), "distinct_cases_audited": len(checked),
            "input_sha256": {p.relative_to(ROOT).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in (GOLDEN, CONFIG)}}


if __name__ == "__main__":
    print(json.dumps(audit(), ensure_ascii=False, indent=2))
