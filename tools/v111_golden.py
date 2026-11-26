"""Independent p1.1.1 reference scorer and golden vectors (docs/specs/v111/RULES_DELTA.md).

Written from the rules text and the configuration data only. It never reads or runs the game's C# code,
its tests or the balance simulator. The structural parts that p1.1.1 keeps (hand recognition, seals,
retriggers, mimic resolution, face-change lists) follow tools/v10_golden.py, the previous reference that was
written the same way; everything p1.1.1 changes is re-derived from RULES_DELTA sections 2-7, 10 and 12.

    python tools/v111_golden.py verify   # config contract, pure-function spec examples, build + coverage
    python tools/v111_golden.py write    # verify, then write GoldenScoresV111.json and the provenance file
    python tools/v111_golden.py check    # verify, then fail if the files on disk differ from `write`

The four historical vector files are read for their INPUTS only (frozen submissions). Their expected values
were produced under older rules and are never consulted.

Points where the rules text is silent or ambiguous are listed in docs/specs/v111/W4_OPEN_READINGS.md; the
reading used here is summarised in OPEN_READINGS below and copied into the vector file header.
"""
import hashlib
import json
import sys
from collections import Counter
from fractions import Fraction
from pathlib import Path

sys.dont_write_bytecode = True

ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / "DicePrototype" / "Assets" / "Game" / "Data"
CONFIG = DATA / "P0Config.json"
GOLDEN = DATA / "GoldenScoresV111.json"
EVIDENCE = ROOT / "evidence" / "v111" / "rules" / "golden-provenance.json"
SCHEMA = "v111-golden-1"
CONFIG_VERSION = "p1.1.1"
RULES = [ROOT / p for p in (
    "docs/specs/v111/RULES_DELTA.md", "docs/specs/v11/RULES_DELTA.md", "docs/specs/v10/RULES_SPEC.md",
    "docs/specs/v09/RULES_SPEC.md", "docs/specs/v08/RULES_DELTA.md", "docs/specs/P0_v0.6.md")]
BASELINE = ROOT / "tools" / "balance_sim" / "baseline"
# The four older vector files (inputs only). They were moved out of the game's data folder unchanged.
HISTORICAL = [("L08", BASELINE / "GoldenScores.p0.8.0.json"), ("L09", BASELINE / "GoldenScoresV09.p0.9.0.json"),
              ("L10", BASELINE / "GoldenScoresV10.p1.0.0.json"), ("L11", BASELINE / "GoldenScoresV11.p1.1.0.json")]

LIFETIME_PERMANENT = "permanent"
LIFETIME_TEMPORARY = "stage_temporary"
ITEM_FACTOR_ID = "I09"
GOLD_ABILITY_ID = "U06"
MULT_ENCHANT_ID = "mult2"
BASE_ENCHANT_ID = "base10"
FACE_IDS = [f"F0{i}" for i in range(1, 7)]


def ids(text):
    return set(text.split())


def ordered(values):
    return sorted(values, key=lambda c: int(c[1:]))


# ---- card classes, taken from the card tables of the rules text (RULES_DELTA 4-6, v10 6, v09 4, v0.6 11) ----
REMOVED = ids("C04 C16 C23 C90 C128 C145")
ADD_FIXED = ids("C06 C08 C11 C14 C18 C28 C30 C32 C40 C46 C74 C75 C77 C81 C82 C83 C86 C88 C91 C102 C104 C105 C106 C144 C149 C153 C156")
ADD_COUNTED = ids("C19 C47 C49 C63 C137 C139 C141 C147 C148 C150 C151 C152")
ADD_GROWTH = ids("C51 C56 C57 C60 C61 C129 C132 C134")
MULT_FIXED = ids("C02 C09 C31 C33 C42 C101 C130 C138")
MULT_OTHER = ids("C53 C71 C87 C89 C103 C155")
MULT_GROWTH = ids("C54 C58 C100 C125")
BASE_GROWTH = ids("C55 C59 C62")
PER_DIE = ids("C10 C13 C17 C20 C39 C45 C92 C93 C94 C107")
BASE_HAND = ids("C27 C44 C48 C50 C70 C72 C76 C80 C108 C135")
HAND_ADD = ids("C84 C85 C142")
RETRIGGER = ids("C64 C65 C66 C67 C68 C69 C109 C143")
SWITCHES = ids("C03 C12 C24 C25 C29 C73 C146")
PLAN_ONLY = ids("C01 C07 C15 C26 C36 C37 C38 C43 C117 C118 C119 C122 C123 C124 C126 C127")
FACE_CARDS = ("C117", "C118", "C119", "C122", "C123", "C124", "C126", "C127")
GROWTH = ADD_GROWTH | MULT_GROWTH | BASE_GROWTH
SCORING = (ADD_FIXED | ADD_COUNTED | ADD_GROWTH | MULT_FIXED | MULT_OTHER | MULT_GROWTH | BASE_GROWTH | PER_DIE
           | BASE_HAND | HAND_ADD | RETRIGGER | SWITCHES | PLAN_ONLY | {"C41"})
# Sources that trigger on every valid submission (v0.6 appendix C, v09 4, v10 6; p1.1.1 keeps them).
ALWAYS = (ids("C01 C03 C09 C38 C42 C45 C46 C47 C49 C50 C53 C63 C64 C94 C101 C108 C117 C118 C119 C122 C123 C124 C141")
          | GROWTH)
CONDITIONAL = SCORING - ALWAYS - {"C41"}
# Cards whose numbers or mechanism change in p1.1.1 (RULES_DELTA 4.1-4.5, 5 and 6.1), scoring side only.
CHANGED = (ids("C02 C09 C31 C33 C41 C42 C53 C71 C87 C89 C100 C101 C103 C130 C138") | ADD_FIXED | ADD_COUNTED | GROWTH
           | ids("C10 C13 C17 C20 C27 C39 C44 C48 C50 C70 C72 C76 C80 C92 C93 C107 C108 C135 C29 C43 C84 C85 C143")
           | ids("C155 C36 C37 C117 C119 C122 C123 C124 C126 C127"))
CATEGORY = {"additive": ADD_FIXED | ADD_COUNTED, "multiplier": MULT_FIXED | MULT_OTHER,
            "base": (PER_DIE | BASE_HAND), "growth": GROWTH}

SMALL_HANDS = ("pair", "two_pair", "trips")
SCORING_EFFECTS = ("SealOdd", "SealEven", "SealLow", "SealHigh", "SealRed", "SealBlue", "SealFirst", "SealAlternate",
                   "NoStraight", "SmallHandsOnly", "NoRepeatHand", "HandHalf", "ZeroLevels", "NoRed", "NoBlue",
                   "FirstHalf", "LaterHalf", "PipsOnly", "GrowthHalf", "EnchantSilence")
SESSION_EFFECTS = ("RollsDelta", "RollsFixed", "SubmissionsDelta", "CapacityDelta", "TargetPercent", "MustFill", "NoItems",
                   "LoseHalfGold", "TaxPerDie", "NoReward", "GrowthFreeze", "HandDowngrade", "DisableLeftmost", "DisableRarity")

# Readings chosen where the rules text is silent; the full text is in docs/specs/v111/W4_OPEN_READINGS.md.
OPEN_READINGS = {
    "W4-01": "已裁定并写入 §12.2：multiplierFactors 每条 {emitterId,abilityId,numerator,denominator}；triggeredMultiplierIds 与它同序、逐条列技能编号、不去重。"
             "仍为自定的只有 triggeredControllerIds：触发来源技能编号去重后按 Ordinal 排序。",
    "W4-02": "C122 见五成群对朝上面已是 5 点的骰也列 pipSet=5（按“每颗…生成”的字面；执行后无变化）。",
    "W4-03": "连庄换牌型时即使事件累计为 0 也列一条 halve=true、delta=0 的成长计划。",
    "W4-04": "逐条即时加成卡在数额为 0 时的触发口径沿用 p1.0.0：C147 需有留池骰、C148 需剩余投掷≥1、C151 需有空栏、C152 需至少一张普通卡、"
             "C137 需至少一颗匹配；C63 总是触发，永久骰为 0 时不列加成条目。",
    "W4-05": "C117 已是黄金面、C124 权重已满仍照列（沿用 V1.0 附录 A 的“照列，由会话夹取”）；C123 只列计划值（+1 / −2），下限 3 由会话执行。",
    "W4-06": "基础分附魔的“原计数”字段名在可读规格里没有出现，只输出金额 enchantBaseAdd。",
    "W4-07": "旧输入的成长值按整数原样保留并按新单位解释（乘法 / 加成为百分单位，基础分为整数分），不做事件数换算。",
    "W4-08": "已由规格修正：§5.1 的例子改为深蓝已长 1.2 读 +1.6、连庄已长 0.15 读 ×1.32，样例随之更新。",
    "W4-09": "C127 移花接木不再限本关首手（§6.1“每手至少两颗时”，§12.3 删除 submissionOrdinal）。",
    "W4-10": "C36 / C37 的 growthIndices 为前 min(3, 提交数) 颗；净和为 0 时仍给出下标与来源绑定。",
    "W4-11": "百变模仿带倍率附魔时附魔条目的 stateOwnerId 取被模仿的真实卡；基础技能加成条目同样 emitterId=模仿者、stateOwnerId=真实卡。",
    "W4-12": "C49 无 5 点时的 ×1/2 计入 multiplierFactors 与 controllerMultiplier，abilityId 为 C49，不产生加成条目。",
    "W4-13": "旧输入的改写规则（C137 补 starPip、C53 表外结果改 8、缺上一主牌型补 scatter、p0.9.0 快照补 lifetime / stageHandsUsed / 栏位数、"
             "B29 / B37 失效名单重算、人员流动 turnoverUnits2 换算为 growth）是本文件自定的，逐条记在各样例的 derivedFrom.rewrites。",
    "W4-14": "C92 纯色主义、C93 始终如一读这颗骰冻结的六个物理面，不区分永久骰与临时骰；没有六面数据的合成快照一律不满足。",
    "W4-15": "空值约定：colorId、chosenGroupId、upgradeHandId 无值时写空串；additiveEntries 里无卡来源的 emitterId / stateOwnerId 写 null。",
    "W4-16": "返璞归真减半的范围取 §5 表的 15 张成长卡（含 C51、C100、C125）；C141 的倒计时、C63 不在其内。",
}

FIELD_NOTES = {
    "input": "沿用 v10-golden-1 的提交快照结构。新增：controllers[].growthCatchup / starPip（§12.1）、stageScoreBefore / stageTarget（§12.1，十进制整数串）、"
             "bindings（null=冻结时的右邻；对象=显式绑定，值为 null 或不存在的实例表示无目标）、debug（调试上限）。"
             "randomChoices[].units2 是 C53 的二倍整数结果，新样例另带 ticket（票号，0–8）。"
             "来自 GoldenScores.p0.8.0.json 的合成快照没有六面数据：dice[].faces 为空数组、faceId 为空串，朝上面的颜色与黄金写在 dice[].color / dice[].gold。",
    "expected": "计分明细。§12.2 的字段：colorAdd100、additiveEntries、additiveTotal100、enchantAdditiveCount、multiplierFactors、"
                "controllerMultiplierNumerator/Denominator、scoreNumerator/Denominator、score；其余沿用 V1.0 附录 A。",
    "expected.afterScorePlan": "计分后计划里由冻结快照就能确定的部分：growthChanges（含 halve）、glassRiskDieIds、faceChanges（每条含 stateOwnerId）、"
                               "cultivationSourceBindings、goldSpent，以及沿用的 earnedGold、copyFirstToSecond、dyeAllFacesRed、redSpreadIndices、growthNetDelta、growthIndices、"
                               "blueSpreadIndices、upgradeHandId（无升级时为空串）/ upgradeRequested / upgradeApplied、nextPreviousPrimary、nextColorOwnerIds、randomFactorOwnerIds、cannonRiskEmitterIds。",
    "selfNamed": "规格没有给名字或形状、由本文件自定的字段：expected.redGroupKey（被选中红组的分组键，无红组或红运当头时为 0）、"
                 "expected.redMemberIndices（被选中红组的提交下标）、expected.chosenGroupId（计分选中的同点牌型，无则空串）、expected.enchantBaseAdd（基础分附魔金额）、"
                 "expected.triggeredSourceIds（触发来源的发出者实例，卡列顺序）、expected.triggeredControllerIds、"
                 "input.bindings、input.debug、input.randomChoices[].ticket。",
    "units": "后缀 100 的字段是百分单位整数；growth / growthCatchup 的单位随卡：加成与乘法成长为百分单位，基础分成长为整数分，C141 的 growth 是已衰减次数。",
    "derivedFrom": "旧样例输入在 p1.1.1 下重算时的来源记录：file、id 与 rewrites（为成为合法 p1.1.1 输入所做的改写；空数组表示只补了缺省新字段）。",
    "handChecked": "true 表示生成器里写有手算期望值并已与参考实现核对一致。",
}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def load(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def rel(path):
    return Path(path).resolve().relative_to(ROOT).as_posix()


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


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
    found = [m for m in cfg["bossModifiers"] if m["id"] == modifier_id]
    require(len(found) == 1, f"unknown boss modifier {modifier_id!r}")
    unknown = set(found[0]["effects"]) - set(SCORING_EFFECTS) - set(SESSION_EFFECTS)
    require(not unknown, f"{modifier_id}: effect codes not covered by the rules text: {sorted(unknown)}")
    return found[0]


def cycle_of_shop(finished_stage):
    """RULES_DELTA 9.1: the shop after stage j belongs to cycle min(8, floor(j / 3) + 1)."""
    return min(8, finished_stage // 3 + 1)


def catchup_for(cfg, card_id, cycle):
    """RULES_DELTA 5: fixed catch-up of a growth card offered in a shop of the given cycle."""
    kind = {c["id"]: c for c in cfg["controllers"]}[card_id]["parameters"]["growthKind"]
    return (cycle - 1) * cfg["growthCatchupPerCycle"][kind]


def final_score(base_total, hand_mult100, red_mult100, additive_total100, controller=Fraction(1), outside=Fraction(1)):
    """RULES_DELTA 2: floor(B x H x R x (1 + A) x M x G), one floor at the very end. Returns (score, exact fraction)."""
    exact = (Fraction(base_total) * Fraction(hand_mult100, 100) * Fraction(red_mult100, 100)
             * Fraction(100 + additive_total100, 100) * Fraction(controller) * Fraction(outside))
    return exact.numerator // exact.denominator, exact


def halve_hand_mult(hand_mult100):
    """RULES_DELTA 2 step 8: HandHalf divides the percent-unit multiplier with integer division, floor x1."""
    return max(100, hand_mult100 // 2)


def resolve_sources(instances, bindings, disabled):
    """Step 4: one source per alive instance, left to right. A mimic follows its frozen binding (the right
    neighbour when bindings is None) to the real owner; a disabled mimic or a disabled target has no ability."""
    by_id = {c["instanceId"]: c for c in instances}
    order = [c["instanceId"] for c in instances]

    def target_of(instance_id):
        if bindings is not None:
            return bindings.get(instance_id)
        at = order.index(instance_id)
        return order[at + 1] if at + 1 < len(order) else None

    sources = []
    for emitter in instances:
        owner, seen = emitter, set()
        dead = emitter["instanceId"] in disabled
        while owner is not None and owner["definitionId"] == "C41":
            require(owner["instanceId"] not in seen, "mimic cycle")
            seen.add(owner["instanceId"])
            owner = by_id.get(target_of(owner["instanceId"]))
            if owner is not None and owner["instanceId"] in disabled:
                dead = True
        if dead:
            owner = None
        sources.append({"emitter": emitter["instanceId"], "ability": owner["definitionId"] if owner else None,
                        "owner": owner["instanceId"] if owner else None, "state": owner or {},
                        "enchant": emitter["enchantmentId"]})
    return sources


def score(inp, cfg, drop_effect=None):
    """Score one frozen submission (public v111-golden-1 input). Returns (expected, info).

    `drop_effect` removes one effect code of the modifier; it exists for coverage analysis only."""
    ctl = {c["id"]: c for c in cfg["controllers"]}
    par = lambda cid: ctl[cid]["parameters"]  # noqa: E731
    hands = {h["id"]: h for h in cfg["handDefinitions"]}
    colors_def = {c["id"]: c for c in cfg["colorDefinitions"]}
    instances = inp["controllers"]
    modifier_id = inp["modifierId"] or ""
    modifier = modifier_of(cfg, modifier_id)
    fx = set(modifier["effects"]) if modifier else set()
    fx.discard(drop_effect)
    disabled = set(inp["disabledControllerIds"])
    item_mult = inp["scoreItemMultiplier"]
    require(item_mult in (0, 2), "scoreItemMultiplier must be 0 or 2")
    require(not (inp["redWave"] and inp["blueWave"]), "red wave and blue wave cannot both be active")

    sources = resolve_sources(instances, inp.get("bindings"), disabled)
    count = Counter(s["ability"] for s in sources if s["ability"])
    has = lambda cid: count[cid] > 0  # noqa: E731

    def growth_value(s):
        """RULES_DELTA 5 / 5.1: start + catch-up + event growth; GrowthHalf halves only the event part."""
        grown = int(s["state"]["growth"])
        if "GrowthHalf" in fx:
            grown //= 2
        return par(s["ability"])["growthStart"] + int(s["state"]["growthCatchup"]) + grown

    # Submitted dice. A synthetic snapshot without six-face data carries the colour and gold of the face that is up.
    opposite = cfg["faceOpposites"]
    dice = []
    for d in inp["dice"]:
        faces = {f["faceId"]: f for f in d["faces"]}
        up = faces.get(d["faceId"])
        require(d["lifetime"] in (LIFETIME_PERMANENT, LIFETIME_TEMPORARY), f"unknown die lifetime {d['lifetime']!r}")
        dice.append({"p": d["pip"], "c": up["color"] if up else d["color"], "gold": up["gold"] if up else d["gold"],
                     "glass": d["glass"], "mono_color": bool(faces) and len({f["color"] for f in d["faces"]}) == 1,
                     "mono_pip": bool(faces) and len({f["pip"] for f in d["faces"]}) == 1,
                     "opposite": faces[opposite[d["faceId"]]]["pip"] if up else 0,
                     "die_id": d["dieId"], "face_id": d["faceId"], "faces": d["faces"], "up": up})
    n = len(dice)
    require(n >= 1, "empty submission")

    # Step 1: effective pips and colours.
    pips = [d["p"] for d in dice]
    cols = [d["c"] for d in dice]
    changed_odd = has("C12") and any(p % 2 for p in pips)
    if has("C12"):
        pips = [p + par("C12")["oddToEvenAdd"] if p % 2 else p for p in pips]
    if has("C03"):
        cols[par("C03")["index"]] = "red"
    if inp["redWave"]:
        cols = ["red"] * n
    if inp["blueWave"]:
        cols = ["blue"] * n
    red_n = cols.count("red")

    # Step 2: same-pip hands, straight, structural primary hand, colour.
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
    levels = {e["handId"]: e["level"] for e in inp["handLevels"]}
    color_levels = {e["handId"]: e["level"] for e in inp["colorLevels"]}
    zero = "ZeroLevels" in fx
    level = lambda hid: 0 if zero else levels.get(hid, 0)  # noqa: E731
    mult = lambda hid: hands[hid]["baseMult100"] + hands[hid]["incrementPerLevel100"] * level(hid)  # noqa: E731
    chosen = max(matched, key=lambda h: (mult(h), hands[h]["structuralPriority"])) if matched else None
    group = mult(chosen) if chosen else 0
    need = par("C29")["minimumStraightLength"] if has("C29") else cfg["minStraightLength"]
    straight_hit = has_straight(pips, need) and "NoStraight" not in fx
    straight = mult("straight") if straight_hit else 0
    structural = matched + (["straight"] if straight_hit else [])
    primary = max(structural, key=lambda h: hands[h]["structuralPriority"]) if structural else "scatter"
    color_id = "all_red" if red_n == n else "all_blue" if red_n == 0 else None
    color_add = 0
    if color_id:
        color_add = colors_def[color_id]["baseAdd100"] + colors_def[color_id]["addPerLevel100"] * (0 if zero else color_levels.get(color_id, 0))
    if (color_id == "all_red" and "NoRed" in fx) or (color_id == "all_blue" and "NoBlue" in fx):
        color_add = 0
    spread = [i for i in range(1, n - 1) if cols[i] == "blue" and cols[i - 1] == "red" and cols[i + 1] == "red"]

    cap = inp["capacity"]
    pool = inp["pool"]
    pool_n = len(pool)
    pool_pips = [d["pip"] for d in pool]
    pool_red = sum(1 for d in pool if d["color"] == "red")
    rolls_used, rolls_left = inp["rollsUsed"], inp["rollsRemaining"]
    chances = inp["submissionsRemainingBefore"]
    permanent = inp["permanentDiceCount"]
    ordinal = inp["submissionOrdinal"]
    wallet = inp["wallet"]
    previous = inp["previousPrimary"] or None
    inventory = inp["inventoryCount"]
    stage_kind = inp["stageKind"] or "normal"
    score_before, target = int(inp["stageScoreBefore"]), int(inp["stageTarget"])
    require(target > 0 and score_before >= 0, "stage target / score before")

    # Step 3 (RULES_DELTA 3.2): the red group. Members are the submitted effective reds of the chosen key.
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
    red_mult = 100 if "NoRed" in fx else red_table[min(red_count, max(red_table))]

    # Seals: only the per-die contribution is zeroed.
    sealed = [("SealOdd" in fx and pips[i] % 2 == 1) or ("SealEven" in fx and pips[i] % 2 == 0)
              or ("SealLow" in fx and pips[i] <= 3) or ("SealHigh" in fx and pips[i] >= 4)
              or ("SealRed" in fx and cols[i] == "red") or ("SealBlue" in fx and cols[i] == "blue")
              or ("SealFirst" in fx and i == 0) or ("SealAlternate" in fx and i % 2 == 0) for i in range(n)]

    def sum_pairs(total):
        pairs = 0
        for v in counts:
            w = total - v
            if v < w:
                pairs += min(counts[v], counts.get(w, 0))
            elif v == w:
                pairs += counts[v] // 2
        return pairs

    # Held cards (RULES_DELTA 4.2 C47 / C151 / C152, 6 "栏位"). Every instance in the snapshot is alive.
    held = len(instances)
    slotless = sum(1 for c in instances if c["enchantmentId"] == "slotless")
    free_cultivation = any(ctl[c["definitionId"]]["isCultivation"] and c["enchantmentId"] != "slotless" for c in instances)
    occupied = held - slotless - (1 if free_cultivation else 0)
    empty_slots = max(0, inp["ordinaryControllerSlots"] - occupied)
    common_n = sum(1 for c in instances if ctl[c["definitionId"]]["rarity"] == par("C152")["rarity"])
    fives = counts.get(5, 0)
    gold_dice = [i for i in range(n) if dice[i]["gold"]]
    zigzag = n >= par("C91")["minDice"] and all(pips[i] != pips[i + 1] for i in range(n - 1)) and all(
        (pips[i + 1] > pips[i]) != (pips[i + 2] > pips[i + 1]) for i in range(n - 2))

    def star_matches(s):
        star = int(s["state"].get("starPip") or 0)
        return sum(1 for v in pips if v == star)

    # C138: sources are judged left to right against the frozen wallet, each hit reserves the cost.
    gold_hit, budget = set(), wallet
    for s in sources:
        if s["ability"] == "C138" and budget >= par("C138")["goldCost"]:
            gold_hit.add(s["emitter"])
            budget -= par("C138")["goldCost"]

    def hit(s):
        cid = s["ability"]
        p = par(cid)
        if cid in ALWAYS: return True
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
        if cid == "C19": return min(pips) in (1, 2)
        if cid == "C20": return n == cap and pips[n - 1] == p["lastActualCapacitySlotPip"]
        if cid == "C24": return any(cols[i] == "red" and pips[i] == p["redGroupKeyFrom"] for i in range(n))
        if cid == "C25": return red_n > 0
        if cid == "C26": return n == p["requiredDice"]
        if cid == "C27": return pair_kinds > 0
        if cid == "C28": return pair_kinds >= p["minDistinctPairs"]
        if cid == "C29": return has_straight(pips, p["minimumStraightLength"]) and "NoStraight" not in fx
        if cid in ("C30", "C31"): return max_same >= p["minSamePip"]
        if cid == "C32": return len(counts) == p["distinctPips"]
        if cid == "C33": return n >= p["minDice"] and pips[0] - pips[1] >= p["firstMinusSecondAtLeast"]
        if cid in ("C36", "C37"): return p["minDice"] <= n < cap
        if cid == "C39": return p["effectiveColor"] in cols
        if cid == "C40": return n >= 3 and pips[1] > pips[0] and pips[2] - pips[1] == pips[1] - pips[0]
        if cid == "C43": return primary != "scatter" and primary == previous
        if cid == "C44": return wallet > 0
        if cid == "C65": return n == cap
        if cid in ("C66", "C67"): return any(v in p["pips"] for v in pips)
        if cid in ("C68", "C102"): return chances == 1
        if cid == "C69": return red_n > 0
        if cid in ("C70", "C72", "C126", "C147"): return pool_n > 0
        if cid == "C71": return p["pip"] in pool_pips
        if cid == "C73": return red_n > 0 and pool_red > 0
        if cid == "C74": return pool_n >= n
        if cid == "C75": return rolls_used == p["maxRollsUsed"]
        if cid == "C76": return rolls_used >= 2
        if cid == "C77": return rolls_left == 0
        if cid == "C80": return sum_pairs(p["pairSum"]) > 0
        if cid == "C81": return primary == "scatter"
        if cid in ("C82", "C86"): return any(h in matched for h in p["handIds"])
        if cid in ("C83", "C142"): return straight_hit
        if cid in ("C84", "C85", "C87"): return max_same >= p["minSamePip"]
        if cid == "C88": return n >= p["minDice"] and len(counts) == n
        if cid == "C89": return bool(gold_dice)
        if cid == "C91": return zigzag
        if cid == "C92": return any(d["mono_color"] for d in dice)
        if cid == "C93": return any(d["mono_pip"] for d in dice)
        if cid == "C103": return permanent <= p["maxOwnedPermanentDice"]
        if cid == "C104": return n >= p["minDice"] and all(cols[i] != cols[i + 1] for i in range(n - 1))
        if cid == "C105": return 0 < red_n < n and all(c == "red" for c in cols[:red_n])
        if cid == "C106": return n >= p["minDice"] and pips == pips[::-1]
        if cid == "C107": return any(pips[i] == pips[i - 1] + 1 for i in range(1, n))
        if cid == "C109": return n % 2 == 1 and n >= p["minDice"]
        if cid == "C127": return n >= p["minDice"]
        if cid == "C130": return stage_kind == "boss"
        if cid == "C135": return inventory > 0
        if cid == "C137": return star_matches(s) > 0
        if cid == "C138": return s["emitter"] in gold_hit
        if cid == "C139": return wallet >= p["goldPerStep"]
        if cid == "C143": return any(v == p["requiredDice"] for v in counts.values())
        if cid == "C144": return all(v % 2 == 0 for v in pips)
        if cid == "C146": return bool(red_members)
        if cid == "C148": return rolls_left >= 1
        if cid == "C149": return n == p["requiredDice"]
        if cid == "C150": return ordinal >= 1
        if cid == "C151": return empty_slots > 0
        if cid == "C152": return common_n >= 1
        if cid == "C153": return ordinal == p["submissionOrdinal"]
        if cid == "C155": return counts.get(4, 0) >= p["minFours"]
        if cid == "C156": return p["targetDivisor"] * score_before < target
        require(cid not in SCORING, f"{cid}: scoring card without a trigger rule")
        return False

    # Step 4: which sources trigger. EnchantSilence does not change this.
    triggered = [s for s in sources if s["ability"] and hit(s)]
    hits = Counter(s["ability"] for s in triggered)
    pips_only = "PipsOnly" in fx
    silent = "EnchantSilence" in fx

    # Step 5: per-die contribution = effective pip -> per-die adds -> C45 x3 -> C93 x3 per source -> glass x3.
    contributions = []
    for i in range(n):
        v = pips[i]
        if not pips_only:
            for s in sources:
                a = s["ability"]
                if a == "C10" and pips[i] == par(a)["pip"]: v += par(a)["perDieAdd"]
                elif a == "C13" and pips[i] % 2: v += par(a)["oddPerDieAdd"]
                elif a == "C17" and pips[i] in par(a)["pips"]: v += par(a)["perDieAdd"]
                elif a == "C20" and n == cap and i == n - 1 and pips[i] == par(a)["lastActualCapacitySlotPip"]: v += par(a)["perDieAdd"]
                elif a == "C39" and cols[i] == par(a)["effectiveColor"]: v += par(a)["perDieAdd"]
                elif a == "C92" and dice[i]["mono_color"]: v += par(a)["perDieAdd"]
                elif a == "C94": v += dice[i]["opposite"]
                elif a == "C107" and i >= 1 and pips[i] == pips[i - 1] + 1: v += par(a)["perDieAdd"]
            for s in sources:
                if s["ability"] == "C45" and s["state"].get("activeColor") == cols[i]:
                    v *= par("C45")["matchingDieContributionMultiplier"]
            for s in sources:
                if s["ability"] == "C93" and dice[i]["mono_pip"]:
                    v *= par("C93")["dieContributionMultiplier"]
            if dice[i]["glass"]:
                v *= cfg["glassContributionMultiplier"]
        contributions.append(0 if sealed[i] else v)

    # Step 6: retriggers. A sealed die still records its count.
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
            if chances == 1:
                for i in range(n): retriggers[i] += par(a)["retriggers"]
        elif a == "C69":
            for i in red_members: retriggers[i] += par(a)["retriggers"]
        elif a == "C109":
            if n % 2 == 1 and n >= par(a)["minDice"]: retriggers[(n - 1) // 2] += par(a)["retriggers"]
        elif a == "C143":
            for i in range(n):
                if counts[pips[i]] == par(a)["requiredDice"]: retriggers[i] += par(a)["retriggers"]
    pip_sum = sum(contributions[i] * (1 + retriggers[i]) for i in range(n))

    # Step 7: base total. PipsOnly keeps pips, retriggers and the unused-roll bonus only.
    whole_adds = 0
    base_adds = []
    if not pips_only:
        whole_adds += hits["C27"] * pair_kinds * par("C27")["baseAddPerDistinctPairedPip"]
        whole_adds += hits["C44"] * wallet * par("C44")["baseAddPerFrozenGold"]
        whole_adds += hits["C48"] * (cap - n) * par("C48")["baseAddPerEmptySubmitSlot"]
        whole_adds += hits["C50"] * par("C50")["baseAdd"]
        for s in triggered:
            a, amount = s["ability"], 0
            if a in BASE_GROWTH: amount = growth_value(s)
            elif a == "C70": amount = pool_n * par(a)["baseAddPerPoolDie"]
            elif a == "C72": amount = sum(pool_pips) * par(a)["poolPipSumMultiplier"]
            elif a == "C76": amount = (rolls_used - 1) * par(a)["baseAddPerExtraRoll"]
            elif a == "C80": amount = sum_pairs(par(a)["pairSum"]) * par(a)["baseAddPerPair"]
            elif a == "C108": amount = pips[par(a)["index"]] * par(a)["baseAddPerPip"]
            elif a == "C135": amount = inventory * par(a)["baseAddPerItem"]
            if amount:
                base_adds.append({"emitterId": s["emitter"], "abilityId": a, "amount": str(amount)})
    base_enchants = 0 if silent or pips_only else sum(1 for s in triggered if s["enchant"] == BASE_ENCHANT_ID)
    enchant_base_add = base_enchants * cfg["enchantRules"]["baseAdd"]
    base_total = (pip_sum + cfg["bonusPerUnusedRoll"] * rolls_left + whole_adds
                  + sum(int(e["amount"]) for e in base_adds) + enchant_base_add)

    # Step 8: hand multiplier, then NoRepeatHand, then HandHalf (integer division of the percent units).
    hand_add = sum(par(s["ability"])["handMultAdd100"] for s in triggered if s["ability"] in HAND_ADD)
    hand_before = max(100, group + straight + hand_add)
    hand = hand_before
    if "NoRepeatHand" in fx and primary != "scatter" and primary in inp["stageHandsUsed"]:
        hand = 100
    if "HandHalf" in fx:
        hand = halve_hand_mult(hand)

    # Step 9: the additive zone. Colour, each submitted gold face, base abilities, multiplier enchantments.
    def additive_amount(s):
        a = s["ability"]
        p = par(a)
        if a in ADD_FIXED: return p["additiveBonus100"]
        if a in ADD_GROWTH: return growth_value(s)
        if a == "C19": return p["minOneAdd100"] if min(pips) == 1 else p["minTwoAdd100"]
        if a == "C47": return held * p["additivePerController100"]
        if a == "C49": return fives * p["additivePerFive100"]
        if a == "C63": return permanent * p["additivePerPermanentDie100"]
        if a == "C137": return star_matches(s) * p["additivePerMatch100"]
        if a == "C139": return (wallet // p["goldPerStep"]) * p["additivePerGoldStep100"]
        if a == "C141": return p["initialAdditive100"] - p["decayPerClear100"] * int(s["state"]["growth"])
        if a == "C147": return pool_n * p["additivePerPoolDie100"]
        if a == "C148": return rolls_left * p["additivePerUnusedRoll100"]
        if a == "C150": return ordinal * p["additivePerPreviousSubmit100"]
        if a == "C151": return empty_slots * p["additivePerEmptyOrdinarySlot100"]
        if a == "C152": return common_n * p["additivePerCommon100"]
        return 0

    entry = lambda kind, ability, emitter, owner, die_index, amount: {  # noqa: E731
        "kind": kind, "abilityId": ability, "emitterId": emitter, "stateOwnerId": owner, "dieIndex": die_index, "amount100": amount}
    entries = []
    if color_add:
        entries.append(entry("color", color_id, None, None, -1, color_add))
    for i in gold_dice:
        entries.append(entry("gold", GOLD_ABILITY_ID, None, None, i, cfg["goldAdd100"]))
    for s in triggered:
        amount = additive_amount(s)
        require(amount >= 0, f"negative additive from {s['ability']}")
        if amount:
            entries.append(entry("controller", s["ability"], s["emitter"], s["owner"], -1, amount))
    enchant_adds = 0
    for s in triggered:
        if s["enchant"] == MULT_ENCHANT_ID and not silent:
            entries.append(entry("enchantment", MULT_ENCHANT_ID, s["emitter"], s["owner"], -1, cfg["enchantRules"]["scoreAdd100"]))
            enchant_adds += 1
    additive_total = sum(e["amount100"] for e in entries)

    # Controller-side factors, left to right.
    random_units = {e["ownerInstanceId"]: e["units2"] for e in inp["randomChoices"]}

    def factor_of(s):
        a = s["ability"]
        p = par(a)
        if a in MULT_FIXED: return Fraction(p["scoreMultiplier100"], 100)
        if a in MULT_GROWTH: return Fraction(growth_value(s), 100)
        if a == "C49": return None if fives else Fraction(p["noFiveMultiplier100"], 100)
        if a == "C53": return Fraction(random_units[s["owner"]], 2)
        if a == "C71": return Fraction(p["stepNumerator"], p["stepDenominator"]) ** pool_pips.count(p["pip"])
        if a == "C87": return Fraction(p["stepNumerator"], p["stepDenominator"]) ** (max_same - p["minSamePip"] + 1)
        if a == "C89": return Fraction(p["stepNumerator"], p["stepDenominator"]) ** len(gold_dice)
        if a == "C103": return Fraction(p["strictScoreMultiplier100"] if permanent <= p["strictMaxOwnedPermanentDice"] else p["scoreMultiplier100"], 100)
        if a == "C155": return Fraction(p["enhancedScoreMultiplier100"] if counts.get(1, 0) >= p["minOnes"] else p["scoreMultiplier100"], 100)
        return None

    factors = []
    x = Fraction(1)
    for s in triggered:
        f = factor_of(s)
        if f is not None:
            factors.append({"emitterId": s["emitter"], "abilityId": s["ability"], "numerator": str(f.numerator), "denominator": str(f.denominator)})
            x *= f

    # Factors outside the cards: the item, then the boss halving.
    global_factors = []
    if item_mult:
        global_factors.append({"emitterId": "ITEM", "abilityId": ITEM_FACTOR_ID, "numerator": str(item_mult), "denominator": "1"})
    if ("FirstHalf" in fx and ordinal == 0) or ("LaterHalf" in fx and ordinal >= 1):
        global_factors.append({"emitterId": "BOSS", "abilityId": modifier_id, "numerator": "1", "denominator": "2"})
    g = Fraction(1)
    for f in global_factors:
        g *= Fraction(int(f["numerator"]), int(f["denominator"]))
    total, exact = final_score(base_total, hand, red_mult, additive_total, x, g)

    # Step 10: the part of the after-score plan that the frozen snapshot already decides.
    growth_changes = []
    for c in instances:
        if c["instanceId"] in disabled:
            continue
        d = c["definitionId"]
        change = lambda delta, halve=False: growth_changes.append(  # noqa: E731
            {"ownerInstanceId": c["instanceId"], "abilityId": d, "delta": delta, "reset": False, "halve": halve})
        if d == "C54" and previous:
            if primary == previous: change(par(d)["growthEvent"])
            else: change(0, True)
        elif d == "C55" or (d == "C56" and red_n == 0) or (d == "C57" and red_count >= par(d)["minRedGroup"]):
            change(par(d)["growthEvent"])

    face_changes = []
    if all(d["up"] for d in dice):
        up = [d["up"] for d in dice]
        ends = [0] if n == 1 else [0, n - 1]

        def change_face(s, die_id, face_id, kind, amount=0, text=""):
            face_changes.append({"sourceId": s["ability"], "emitterId": s["emitter"], "stateOwnerId": s["owner"], "dieId": die_id,
                                 "faceId": face_id, "kind": kind, "amount": amount, "text": text})

        for s in triggered:
            a = s["ability"]
            if a not in FACE_CARDS:
                continue
            p = par(a)
            if a == "C117":
                for i in ends:
                    change_face(s, dice[i]["die_id"], up[i]["faceId"], "gold", 1)
            elif a == "C118":
                for i in range(n):
                    change_face(s, dice[i]["die_id"], opposite[up[i]["faceId"]], "color", 0, up[i]["color"])
            elif a == "C119":
                for i in ends:
                    others = [f for f in sorted(dice[i]["faces"], key=lambda f: f["faceId"]) if f["pip"] != up[i]["pip"]]
                    for f in others[:p["facesPerDie"]]:
                        change_face(s, dice[i]["die_id"], f["faceId"], "pipSet", up[i]["pip"])
            elif a == "C122":
                for i in range(n):
                    change_face(s, dice[i]["die_id"], up[i]["faceId"], "pipSet", p["pip"])
            elif a == "C123":
                for i in range(n):
                    if up[i]["pip"] < min(p["pips"]):
                        change_face(s, dice[i]["die_id"], up[i]["faceId"], "pipDelta", p["lowPipDelta"])
                    elif up[i]["pip"] > max(p["pips"]):
                        change_face(s, dice[i]["die_id"], up[i]["faceId"], "pipDelta", p["highPipDelta"])
            elif a == "C124":
                for i in range(n):
                    change_face(s, dice[i]["die_id"], up[i]["faceId"], "weightDelta", p["delta"])
            elif a == "C126":
                for d in pool:
                    change_face(s, d["dieId"], d["faceId"], "pipDelta", p["delta"])
            elif a == "C127":
                src, die_id, face_id = up[p["sourceIndex"]], dice[n - 1]["die_id"], up[n - 1]["faceId"]
                change_face(s, die_id, face_id, "pipSet", src["pip"])
                change_face(s, die_id, face_id, "color", 0, src["color"])
                change_face(s, die_id, face_id, "texture", 0, src["texture"])
                change_face(s, die_id, face_id, "gold", 1 if src["gold"] else 0)

    # RULES_DELTA 12.2 cultivationSourceBindings: old permanent-rewrite order C07 -> C26 -> C36/C37 -> C15 -> C38.
    binding = lambda s: {"abilityId": s["ability"], "emitterId": s["emitter"], "stateOwnerId": s["owner"]}  # noqa: E731
    cultivation = []
    for group_ids, every in ((("C07",), False), (("C26",), False), (("C36", "C37"), True), (("C15",), False), (("C38",), False)):
        found = [s for s in triggered if s["ability"] in group_ids]
        cultivation.extend(binding(s) for s in (found if every else found[:1]))

    requested = hits["C43"] * par("C43")["levelAddPerSource"]
    level_cap = cfg["debug"]["maxHandLevel"] if inp.get("debug") else cfg["maxHandLevel"]
    real = lambda d: [c["instanceId"] for c in instances if c["instanceId"] not in disabled and c["definitionId"] == d]  # noqa: E731
    grow_indices = [i for i in par("C36")["indices"] if i < n]
    plan = {
        "earnedGold": hits["C01"] * par("C01")["goldPerValidSubmission"],
        "goldSpent": par("C138")["goldCost"] * len(gold_hit),
        "copyFirstToSecond": hits["C07"] > 0,
        "dyeAllFacesRed": hits["C26"] > 0,
        "growthNetDelta": hits["C36"] * par("C36")["delta"] + hits["C37"] * par("C37")["delta"],
        "growthIndices": grow_indices if hits["C36"] + hits["C37"] > 0 else [],
        "redSpreadIndices": spread if hits["C15"] > 0 else [],
        "blueSpreadIndices": ([0] if n == 1 else [0, n - 1]) if hits["C38"] > 0 else [],
        "cultivationSourceBindings": cultivation,
        "faceChanges": face_changes,
        "growthChanges": growth_changes,
        "upgradeHandId": primary if requested > 0 else "",
        "upgradeRequested": requested,
        "upgradeApplied": max(0, min(requested, level_cap - levels.get(primary, 0))) if requested > 0 else 0,
        "nextPreviousPrimary": primary,
        "nextColorOwnerIds": real("C45"),
        "randomFactorOwnerIds": real("C53"),
        "cannonRiskEmitterIds": [s["emitter"] for s in triggered if s["ability"] == "C42"],
        "glassRiskDieIds": [d["die_id"] for d in dice if d["glass"]],
    }
    expected = {
        "score": str(total),
        "scoreNumerator": str(exact.numerator),
        "scoreDenominator": str(exact.denominator),
        "baseTotal": str(base_total),
        "pipSum": str(pip_sum),
        "pipContributions": contributions,
        "pipRetriggers": retriggers,
        "sealedDice": sealed,
        "controllerBaseAdds": base_adds,
        "enchantBaseAdd": enchant_base_add,
        "primaryHandId": primary,
        "chosenGroupId": chosen or "",
        "handMultAdd100": hand_add,
        "handMultBeforeModifier100": hand_before,
        "totalHandMult100": hand,
        "colorId": color_id or "",
        "colorAdd100": color_add,
        "redGroupKey": best_key or 0,
        "redMemberIndices": red_members,
        "redCount": red_count,
        "redPoolCount": red_pool,
        "redBonusCount": red_bonus,
        "redMult100": red_mult,
        "additiveEntries": entries,
        "additiveTotal100": additive_total,
        "enchantAdditiveCount": enchant_adds,
        "multiplierFactors": factors,
        "triggeredMultiplierIds": [f["abilityId"] for f in factors],
        "controllerMultiplierNumerator": str(x.numerator),
        "controllerMultiplierDenominator": str(x.denominator),
        "globalFactors": global_factors,
        "triggeredSourceIds": [s["emitter"] for s in triggered],
        "triggeredControllerIds": sorted({s["ability"] for s in triggered}),
        "afterScorePlan": plan,
    }
    info = {"sources": [[s["emitter"], s["ability"]] for s in sources], "triggered": [s["emitter"] for s in triggered]}
    return expected, info


def apply_growth_plan(controllers, growth_changes):
    """What the session does with growthChanges when nothing blocks it (RULES_DELTA 5, 12.2): halve first
    (integer division of the event growth), then add the event delta. Returns new controller entries."""
    out = [dict(c) for c in controllers]
    by_id = {c["instanceId"]: c for c in out}
    for change in growth_changes:
        card = by_id[change["ownerInstanceId"]]
        if change["halve"]:
            card["growth"] //= 2
        card["growth"] += change["delta"]
    return out


# ---- legality of a frozen submission -------------------------------------------------------------------------------

def disabled_by_modifier(cfg, controllers, modifier_id):
    """RULES_DELTA 10: DisableLeftmost takes the leftmost `amount` cards; DisableRarity the leftmost `amount`
    cards of that rarity (a mimic is judged by its own rarity)."""
    modifier = modifier_of(cfg, modifier_id)
    if modifier is None:
        return []
    rarity = {c["id"]: c["rarity"] for c in cfg["controllers"]}
    out = set()
    if "DisableLeftmost" in modifier["effects"]:
        out |= {c["instanceId"] for c in controllers[:modifier["amount"]]}
    if "DisableRarity" in modifier["effects"]:
        out |= {c["instanceId"] for c in [c for c in controllers if rarity[c["definitionId"]] == modifier["filter"]][:modifier["amount"]]}
    return [c["instanceId"] for c in controllers if c["instanceId"] in out]


def stage_budgets(cfg, controllers, bindings, modifier_id, disabled):
    """Session rules that decide which snapshots are legal: rolls per hand, submissions per stage, the submit
    capacity and whether a card or the modifier pins that capacity. Every enabled source counts, mimics included."""
    par = {c["id"]: c["parameters"] for c in cfg["controllers"]}
    modifier = modifier_of(cfg, modifier_id)
    fx = set(modifier["effects"]) if modifier else set()
    amount = modifier["amount"] if modifier else 0
    abilities = Counter(s["ability"] for s in resolve_sources(controllers, bindings, set(disabled)) if s["ability"])
    live = [c for c in controllers if c["instanceId"] not in disabled]
    enchants = lambda e: 0 if "EnchantSilence" in fx else sum(1 for c in live if c["enchantmentId"] == e)  # noqa: E731
    if "RollsFixed" in fx:
        rolls = amount
    else:
        rolls = max(1, cfg["totalRollsPerHand"] + enchants("roll1") + abilities["C78"] + (amount if "RollsDelta" in fx else 0))
    chances = max(1, cfg["submissionsPerStage"] + enchants("submit1") + abilities["C79"] - abilities["C101"]
                  + (amount if "SubmissionsDelta" in fx else 0))
    delta = sum(abilities[d] * par[d]["capacityDelta"] for d in ("C09", "C21", "C22"))
    capacity = min(cfg["maxSubmitCapacity"], max(1, cfg["initialSubmitCapacity"] + delta + (amount if "CapacityDelta" in fx else 0)))
    pinned = any(abilities[d] for d in ("C09", "C21", "C22")) or "CapacityDelta" in fx
    return rolls, chances, capacity, pinned


INPUT_KEYS = {"dice", "pool", "capacity", "rollsRemaining", "rollsUsed", "submissionsRemainingBefore", "permanentDiceCount",
              "submissionOrdinal", "wallet", "redWave", "blueWave", "previousPrimary", "controllers", "handLevels", "colorLevels",
              "randomChoices", "stageKind", "modifierId", "disabledControllerIds", "stageHandsUsed", "inventoryCount",
              "ordinaryControllerSlots", "scoreItemMultiplier", "stageScoreBefore", "stageTarget", "bindings", "debug"}
CONTROLLER_KEYS = {"instanceId", "definitionId", "enchantmentId", "growth", "growthCatchup", "turnoverUnits2", "activeColor", "starPip"}


def validate(case_id, inp, cfg, strict=True, synthetic_disabled=False, remote_allowed=False, relax_slots=False):
    """A legal p1.1.1 frozen submission. `strict` adds the session budget consistency that only snapshots built
    by a real stage satisfy; the synthetic legacy snapshots (no six faces, debug sizes) are checked structurally."""
    def need(cond, why):
        if not cond:
            raise ValueError(f"{case_id}: illegal case: {why}")
    definitions = {c["id"]: c for c in cfg["controllers"]}
    non_submit = set(cfg["enchantRules"]["nonSubmitControllerIds"])
    hand_ids = {h["id"] for h in cfg["handDefinitions"]}
    color_ids = {c["id"] for c in cfg["colorDefinitions"]}
    debug = inp["debug"]
    need(set(inp) == INPUT_KEYS, f"input fields {sorted(set(inp) ^ INPUT_KEYS)}")
    need(isinstance(debug, bool), "debug")
    n, pool_n = len(inp["dice"]), len(inp["pool"])
    max_cap = cfg["debug"]["maxSubmitCapacity"] if debug else cfg["maxSubmitCapacity"]
    need(1 <= inp["capacity"] <= max_cap and 1 <= n <= inp["capacity"], "dice count / capacity")
    die_ids = [d["dieId"] for d in inp["dice"]] + [d["dieId"] for d in inp["pool"]]
    need(len(die_ids) == len(set(die_ids)), "die ids")
    for d in inp["dice"]:
        need(1 <= d["pip"] <= 6 and isinstance(d["glass"], bool) and d["lifetime"] in (LIFETIME_PERMANENT, LIFETIME_TEMPORARY), "die pip / lifetime")
        if d["faces"]:
            need(set(d) == {"dieId", "faceId", "pip", "glass", "lifetime", "faces"}, "die fields")
            need([f["faceId"] for f in d["faces"]] == FACE_IDS and d["faceId"] in FACE_IDS, "faces")
            need(all(set(f) == {"faceId", "pip", "color", "texture", "gold"} for f in d["faces"]), "face fields")
            need(all(1 <= f["pip"] <= 6 and f["color"] in ("red", "blue") and f["texture"] in ("none", "smooth", "rough")
                     and isinstance(f["gold"], bool) for f in d["faces"]), "face data")
            need(remote_allowed or d["pip"] == d["faces"][int(d["faceId"][2]) - 1]["pip"], "current pip differs from the face pip")
        else:
            need(not strict, "a real snapshot carries six faces")
            need(set(d) == {"dieId", "faceId", "pip", "glass", "lifetime", "faces", "color", "gold"} and d["faceId"] == ""
                 and d["color"] in ("red", "blue") and isinstance(d["gold"], bool), "synthetic die fields")
    for d in inp["pool"]:
        need(set(d) == {"dieId", "faceId", "pip", "color"} and 1 <= d["pip"] <= 6 and d["color"] in ("red", "blue") and d["faceId"] in FACE_IDS, "pool die")
    ctl = inp["controllers"]
    need(len(ctl) <= (cfg["debug"]["maxControllerInstances"] if debug else cfg["maxControllerInstances"]), "controller count")
    need(len({c["instanceId"] for c in ctl}) == len(ctl), "duplicate controller instance")
    for c in ctl:
        d = c["definitionId"]
        need(set(c) == CONTROLLER_KEYS, "controller fields")
        need(d in definitions, f"unknown or removed controller {d}")
        need(c["enchantmentId"] in ("none", "slotless", "base10", "mult2", "submit1", "roll1"), "enchantment")
        need(not (c["enchantmentId"] in ("base10", "mult2") and d in non_submit), "score enchant on a non-scoring controller")
        need(isinstance(c["growth"], int) and c["growth"] >= 0 and isinstance(c["growthCatchup"], int) and c["growthCatchup"] >= 0, "growth state")
        need(c["growthCatchup"] == 0 or d in GROWTH, "catch-up on a card that is not a growth card")
        need(c["growth"] == 0 or d in GROWTH or d == "C141", "growth on a card without growth")
        need(d != "C141" or c["growth"] < definitions["C141"]["parameters"]["expireGrowth"], "C141 is removed at this count")
        need(c["turnoverUnits2"] == 3, "turnoverUnits2 is no longer read and stays at its initial value")
        need(c["activeColor"] in (("red", "blue") if d == "C45" else ("",)), "activeColor")
        need(c["starPip"] in (range(1, 7) if d == "C137" else (0,)), "starPip")
    bindings = inp["bindings"]
    need(bindings is None or set(bindings) == {c["instanceId"] for c in ctl if c["definitionId"] == "C41"}, "bindings cover exactly the mimics")
    need(inp["stageKind"] in ("normal", "pressure", "boss"), "stageKind")
    modifier = modifier_of(cfg, inp["modifierId"])
    fx = set(modifier["effects"]) if modifier else set()
    need(modifier is None or inp["stageKind"] == "boss", "a modifier needs a boss stage")
    disabled = inp["disabledControllerIds"]
    need(disabled == [c["instanceId"] for c in ctl if c["instanceId"] in set(disabled)], "disabled ids must be existing instances in controller order")
    need(synthetic_disabled or disabled == disabled_by_modifier(cfg, ctl, inp["modifierId"]), "disabled controllers vs modifier")
    need(isinstance(inp["redWave"], bool) and isinstance(inp["blueWave"], bool) and not (inp["redWave"] and inp["blueWave"]), "waves")
    need(inp["scoreItemMultiplier"] in (0, 2), "scoreItemMultiplier")
    need(inp["wallet"] >= 0 and inp["previousPrimary"] in ({""} | hand_ids | {"scatter"}), "wallet / previousPrimary")
    need(isinstance(inp["stageScoreBefore"], str) and isinstance(inp["stageTarget"], str)
         and int(inp["stageScoreBefore"]) >= 0 and int(inp["stageTarget"]) > 0, "stage score / target")
    need(str(int(inp["stageScoreBefore"])) == inp["stageScoreBefore"] and str(int(inp["stageTarget"])) == inp["stageTarget"], "canonical decimal strings")
    max_level = cfg["debug"]["maxHandLevel"] if debug else cfg["maxHandLevel"]
    max_color = cfg["debug"]["maxColorLevel"] if debug else cfg["maxColorLevel"]
    need(all(e["handId"] in hand_ids and 1 <= e["level"] <= max_level for e in inp["handLevels"]), "hand levels")
    need(all(e["handId"] in color_ids and 1 <= e["level"] <= max_color for e in inp["colorLevels"]), "colour levels")
    # Step 4: one fixed result per real, enabled C53; a disabled C53 draws nothing.
    owners = sorted(c["instanceId"] for c in ctl if c["definitionId"] == "C53" and c["instanceId"] not in set(disabled))
    table = definitions["C53"]["parameters"]["factorUnits2"]
    need(sorted(e["ownerInstanceId"] for e in inp["randomChoices"]) == owners, "one random choice per real enabled C53")
    for e in inp["randomChoices"]:
        need(e["units2"] in table and ("ticket" not in e or table[e["ticket"]] == e["units2"]), "random choice outside the ticket table")
    used, ordinal = inp["stageHandsUsed"], inp["submissionOrdinal"]
    need(len(set(used)) == len(used) and set(used) <= hand_ids and len(used) <= ordinal, "stageHandsUsed")
    need(inp["rollsUsed"] >= 1 and inp["rollsRemaining"] >= 0 and inp["submissionsRemainingBefore"] >= 1 and ordinal >= 0, "budgets")
    need(inp["permanentDiceCount"] >= 0 and inp["inventoryCount"] >= 0, "counts")
    if not strict:
        return
    permanent = inp["permanentDiceCount"]
    temporary = n + pool_n - permanent
    submitted_temporary = sum(1 for d in inp["dice"] if d["lifetime"] == LIFETIME_TEMPORARY)
    need(0 <= permanent <= cfg["maxPermanentDice"] and n + pool_n <= cfg["maxActiveDice"], "dice totals")
    need(0 <= temporary <= cfg["maxTemporaryDice"], "pool must hold every alive die that is not submitted")
    need(submitted_temporary <= temporary and temporary - submitted_temporary <= pool_n, "temporary dice vs permanentDiceCount")
    rolls, chances, capacity, pinned = stage_budgets(cfg, ctl, bindings, inp["modifierId"], set(disabled))
    need(not pinned or inp["capacity"] == capacity, "capacity vs capacity cards / modifier")
    need(inp["rollsUsed"] + inp["rollsRemaining"] >= rolls, "roll budget")
    need(inp["submissionsRemainingBefore"] <= chances and ordinal == chances - inp["submissionsRemainingBefore"], "submission budget")
    live = [c["definitionId"] for c in ctl if c["instanceId"] not in set(disabled)]
    need("C131" not in live or inp["stageKind"] != "boss", "a real C131 destroys itself when a boss stage starts")
    need("C136" not in live or temporary >= 1, "C136 grants a temporary die when the stage starts")
    need("MustFill" not in fx or n == min(inp["capacity"], n + pool_n), "MustFill")
    need("NoItems" not in fx or not (inp["redWave"] or inp["blueWave"] or inp["scoreItemMultiplier"]), "NoItems")
    need(inp["inventoryCount"] <= cfg["maxConsumableSlots"], "inventoryCount")
    slotless = sum(1 for c in ctl if c["enchantmentId"] == "slotless")
    free = any(definitions[c["definitionId"]]["isCultivation"] and c["enchantmentId"] != "slotless" for c in ctl)
    need(cfg["initialControllerSlots"] <= inp["ordinaryControllerSlots"] and len(ctl) - slotless - (1 if free else 0) <= inp["ordinaryControllerSlots"]
         and (relax_slots or inp["ordinaryControllerSlots"] <= cfg["maxOrdinaryControllerSlots"]), "ordinary slots")
    need(ordinal == 0 or inp["previousPrimary"] == "scatter" or inp["previousPrimary"] in used, "previousPrimary vs stageHandsUsed")


def occupied_slots(cfg, controllers):
    """RULES_DELTA 6 "栏位": alive cards - slotless enchantments - one free slot for the leftmost cultivation card."""
    definitions = {c["id"]: c for c in cfg["controllers"]}
    slotless = sum(1 for c in controllers if c["enchantmentId"] == "slotless")
    free = any(definitions[c["definitionId"]]["isCultivation"] and c["enchantmentId"] != "slotless" for c in controllers)
    return len(controllers) - slotless - (1 if free else 0)


# ---- historical inputs recomputed under p1.1.1 ---------------------------------------------------------------------

class Excluded(Exception):
    """A historical input that cannot be turned into a legal p1.1.1 submission."""


def pick_star(dice):
    """Star pip for a migrated C137 (its p1.0.0 rule read temporary dice): the current pip of the first submitted
    temporary die when there is one, otherwise a pip that no submitted die shows (also not after C12's +1)."""
    temporary = [d["pip"] for d in dice if d.get("lifetime") == LIFETIME_TEMPORARY]
    if temporary:
        return temporary[0]
    present = {d["pip"] for d in dice} | {d["pip"] + 1 for d in dice}
    return next((v for v in range(6, 0, -1) if v not in present), 6)


def legacy_to_snapshot(case, cfg, rewrites):
    """GoldenScores.json (p0.8.0 synthetic format, no six faces) -> the v10-style field names."""
    i = case["input"]
    cards = []
    for c in i["controllers"]:
        state = c.get("state") or {}
        if not c.get("alive", True):
            rewrites.append(f"已销毁的实例 {c['instanceId']} 不再出现在卡列里（绑定仍指向它，等同目标不存在）")
            continue
        if "fundAccrual" in state:
            rewrites.append("fundAccrual 与计分无关，已去掉")
        cards.append({"instanceId": c["instanceId"], "definitionId": c["definitionId"], "enchantmentId": c["enchantmentId"], "growth": 0,
                      "turnoverUnits2": state.get("turnoverUnits2", 3), "activeColor": state.get("activeColor") or ""})
    mimics = [c["instanceId"] for c in cards if c["definitionId"] == "C41"]
    rewrites.append("合成快照：补上 dieId、lifetime 与 p0.9.0 / p1.0.0 起新增的快照字段的缺省值；sum6 已没有规则读取，去掉")
    return {
        "dice": [{"dieId": f"D{k + 1:03d}", "faceId": "", "pip": d["p"], "glass": d["glass"], "lifetime": LIFETIME_PERMANENT, "faces": [],
                  "color": d["c"], "gold": d["gold"]} for k, d in enumerate(i["dice"])],
        "pool": [], "capacity": i["capacity"], "rollsRemaining": i["rolls_remaining"],
        "rollsUsed": max(1, cfg["totalRollsPerHand"] - i["rolls_remaining"]),
        "submissionsRemainingBefore": max(1, cfg["submissionsPerStage"] - i["submission_ordinal"]),
        "permanentDiceCount": len(i["dice"]), "submissionOrdinal": i["submission_ordinal"], "wallet": i["wallet"],
        "redWave": i["red_wave"], "previousPrimary": i["previous_primary"] or "", "controllers": cards,
        "handLevels": [{"handId": k, "level": v} for k, v in i["levels"].items() if v],
        "colorLevels": [{"handId": k, "level": v} for k, v in i["color_levels"].items() if v],
        "randomChoices": [{"ownerInstanceId": k, "units2": v} for k, v in i["random_units2"].items()],
        "bindings": {m: i["bindings"].get(m) for m in mimics} if mimics else None, "debug": bool(i["debug"]),
    }


def modernize(inp, cfg, rewrites, focus_card=None):
    """A v09 / v10-style input -> a legal v111-golden-1 input. Returns (input, validation flags)."""
    definitions = {c["id"]: c for c in cfg["controllers"]}
    par = lambda cid: definitions[cid]["parameters"]  # noqa: E731
    cards = [dict(c) for c in inp["controllers"]]
    bindings = inp.get("bindings")
    debug = bool(inp.get("debug", False))
    modifier_id = inp.get("modifierId") or ""
    disabled = list(inp.get("disabledControllerIds") or [])
    dice = [dict(d, lifetime=d.get("lifetime", LIFETIME_PERMANENT)) for d in inp["dice"]]
    before_p10 = "stageKind" not in inp  # a p0.9.0 snapshot: no lifetime, stage history or slot count yet
    unlabeled = len(dice) + len(inp["pool"]) - inp["permanentDiceCount"] - len(inp["pool"])
    if before_p10 and unlabeled > 0:
        for d in dice[len(dice) - unlabeled:]:
            d["lifetime"] = LIFETIME_TEMPORARY
        rewrites.append(f"p0.9.0 快照没有 lifetime：permanentDiceCount 表明至少 {unlabeled} 颗提交骰是临时骰，把最后 {unlabeled} 颗标为 stage_temporary")
    used, previous = list(inp.get("stageHandsUsed") or []), inp["previousPrimary"]
    if before_p10 and inp["submissionOrdinal"] > 0 and previous == "":
        previous = "scatter"
        rewrites.append("本关已有过提交却没有上一主牌型：补 previousPrimary=scatter（本关此前的提交必然留下记录）")
    if before_p10 and inp["submissionOrdinal"] > 0 and previous != "scatter":
        used = [previous]
        rewrites.append("p0.9.0 快照没有 stageHandsUsed：本关已有提交且上一主牌型非散点，补为上一主牌型")

    gone = sorted({c["definitionId"] for c in cards if c["definitionId"] in REMOVED}, key=lambda c: int(c[1:]))
    if gone:
        names = "、".join(gone)
        if focus_card in REMOVED:
            raise Excluded(f"样例专为已移除的 {focus_card} 而设，p1.1.1 没有对应的卡")
        rest = [c for c in cards if c["definitionId"] not in REMOVED]
        if not [c for c in rest if c["definitionId"] != "C41"]:
            raise Excluded(f"输入的卡牌只有已移除的 {names}（及复制它的百变模仿），去掉后没有可重算的内容")
        kind = {c["instanceId"]: c["definitionId"] for c in cards}
        for s in resolve_sources([dict(c, enchantmentId=c["enchantmentId"]) for c in cards], bindings, set()):
            if s["ability"] in REMOVED and kind[s["emitter"]] == "C41":
                raise Excluded(f"百变模仿复制的是已移除的 {s['ability']}，去掉目标会改变绑定")
        cards = rest
        alive = {c["instanceId"] for c in cards}
        disabled = [i for i in disabled if i in alive]
        rewrites.append(f"去掉已移除的卡 {names} 的实例（配置与实例校验不再接受）")

    out_cards = []
    for c in cards:
        d = c["definitionId"]
        if d not in definitions:
            raise Excluded(f"卡牌 {d} 不在 p1.1.1 的配置里")
        growth, star = c["growth"], 0
        if d == "C51" and c["turnoverUnits2"] != 3:
            growth = (c["turnoverUnits2"] - 3) * par("C51")["growthEvent"]
            rewrites.append(f"人员流动改读 growth：旧 turnoverUnits2={c['turnoverUnits2']}（{c['turnoverUnits2'] - 3} 次卖卡事件）改写为 growth={growth}，turnoverUnits2 回到初值 3")
        elif c["turnoverUnits2"] != 3:
            raise Excluded(f"{d} 带有非初值的 turnoverUnits2")
        if d == "C137":
            star = pick_star(dice)
            rewrites.append(f"客串明星换机制：补 starPip={star}（有提交的临时骰时取第一颗临时骰的当前点数，否则取提交骰里没有的点数）")
        out_cards.append({"instanceId": c["instanceId"], "definitionId": d, "enchantmentId": c["enchantmentId"], "growth": growth,
                          "growthCatchup": 0, "turnoverUnits2": 3, "activeColor": c["activeColor"], "starPip": star})

    modifier = modifier_of(cfg, modifier_id)
    disabling = modifier is not None and bool({"DisableLeftmost", "DisableRarity"} & set(modifier["effects"]))
    if disabling and (modifier_id in ("B29", "B37") or gone):
        fresh = disabled_by_modifier(cfg, out_cards, modifier_id)
        if fresh != disabled:
            rewrites.append(f"{modifier_id} 的失效名单按 p1.1.1 重算：{disabled} 改为 {fresh}")
            disabled = fresh
    synthetic = disabled != disabled_by_modifier(cfg, out_cards, modifier_id)

    table = par("C53")["factorUnits2"]
    owners = [c["instanceId"] for c in out_cards if c["definitionId"] == "C53" and c["instanceId"] not in set(disabled)]
    old_choices = {e["ownerInstanceId"]: e["units2"] for e in inp["randomChoices"]}
    choices = []
    for owner in owners:
        units = old_choices.get(owner)
        if units is None:
            units = 2
            rewrites.append(f"{owner} 的错乱乘积在新失效名单下有效，补随机结果 units2=2（×1）")
        elif units not in table:
            rewrites.append(f"{owner} 的错乱乘积结果 units2={units} 不在新票表里，改为表内最大值 8（×4）")
            units = max(table)
        choices.append({"ownerInstanceId": owner, "units2": units})
    if set(old_choices) - set(owners):
        rewrites.append("去掉失效或已不存在的错乱乘积的随机结果")

    occupied = occupied_slots(cfg, out_cards)
    slots = inp.get("ordinaryControllerSlots") or max(cfg["initialControllerSlots"], occupied)
    relax_slots = slots > cfg["maxOrdinaryControllerSlots"]
    if relax_slots and not debug:
        rewrites.append(f"旧快照没有栏位字段：按占用数补 ordinaryControllerSlots={slots}，超过正式上限 {cfg['maxOrdinaryControllerSlots']}，属合成快照")
    remote = any(d["faces"] and d["pip"] != next(f["pip"] for f in d["faces"] if f["faceId"] == d["faceId"]) for d in dice)
    out = {
        "dice": dice, "pool": inp["pool"], "capacity": inp["capacity"], "rollsRemaining": inp["rollsRemaining"], "rollsUsed": inp["rollsUsed"],
        "submissionsRemainingBefore": inp["submissionsRemainingBefore"], "permanentDiceCount": inp["permanentDiceCount"],
        "submissionOrdinal": inp["submissionOrdinal"], "wallet": inp["wallet"], "redWave": inp["redWave"], "blueWave": bool(inp.get("blueWave", False)),
        "previousPrimary": previous, "controllers": out_cards, "handLevels": inp["handLevels"], "colorLevels": inp["colorLevels"],
        "randomChoices": choices, "stageKind": inp.get("stageKind") or "normal", "modifierId": modifier_id, "disabledControllerIds": disabled,
        "stageHandsUsed": used, "inventoryCount": inp.get("inventoryCount", 0), "ordinaryControllerSlots": slots,
        "scoreItemMultiplier": inp.get("scoreItemMultiplier", 0), "stageScoreBefore": "0", "stageTarget": "1", "bindings": bindings, "debug": debug,
    }
    return out, {"synthetic_disabled": synthetic, "remote_allowed": remote, "relax_slots": relax_slots}


def historical_cases(cfg):
    """Every input of the four older vector files, recomputed. Only `input`, `id` and `description` are read."""
    cases, infos, excluded, seen = [], [], [], {}
    for prefix, path in HISTORICAL:
        content = load(path)
        old_cases = content if isinstance(content, list) else content["cases"]
        seen[rel(path)] = len(old_cases)
        for old in old_cases:
            rewrites = []
            try:
                snapshot = legacy_to_snapshot(old, cfg, rewrites) if prefix == "L08" else old["input"]
                inp, flags = modernize(snapshot, cfg, rewrites, old.get("cardId"))
                case_id = f"{prefix}-{old['id']}"
                try:
                    validate(case_id, inp, cfg, strict=prefix != "L08", **flags)
                except ValueError as error:
                    raise Excluded(f"改写后仍不是合法的 p1.1.1 提交：{str(error).split('illegal case: ')[-1]}")
            except Excluded as reason:
                excluded.append({"file": rel(path), "id": old["id"], "description": old["description"], "reason": str(reason)})
                continue
            expected, info = score(inp, cfg)
            cases.append({"id": case_id, "description": f"旧样例 {old['id']} 的输入按 p1.1.1 重算（原说明按旧规则写成：{old['description']}）",
                          "source": path.name, "derivedFrom": {"file": rel(path), "id": old["id"], "rewrites": rewrites},
                          "handChecked": False, "input": inp, "expected": expected})
            infos.append(info)
    return cases, infos, excluded, seen


# ---- new cases -----------------------------------------------------------------------------------------------------

# Matrix recipes: explicit examples of each card's trigger text. The default hand is PAIR (3b 3b 5b 4r 2b: 17 pips,
# one pair, mixed colours, one red, capacity 5 filled, 3 rolls used, first submission of the stage).
PAIR = "3b 3b 5b 4r 2b"
HIT = {
    "C02": {"d": "3b 3b 5b 6b 2b"}, "C07": {"d": "2b 5r"}, "C08": {"d": "2b 5r"}, "C09": {"d": "2b"}, "C10": {"d": "1r 2b"},
    "C11": {"d": "2b 2r 3b 3r"}, "C14": {"d": "1r 3b 5b"}, "C15": {"d": "2r 3b 4r"}, "C18": {"d": "2r 4b"},
    "C20": {"d": "2r 3b 4r 5b 6b"}, "C24": {"d": "1r 4r 2b"}, "C25": {"d": "1r 4r 2b"}, "C26": {"d": "2b"},
    "C28": {"d": "2b 2r 3b 3r"}, "C29": {"d": "1r 2b 3b 4r"}, "C30": {"d": "3b 3b 3r"}, "C31": {"d": "4b 4r 4b 4b"},
    "C32": {"d": "5b 5r"}, "C33": {"d": "6b 3r"}, "C36": {"d": "2b 4r"}, "C37": {"d": "2b 4r"}, "C40": {"d": "1r 3b 5b"},
    "C43": {"prev": "pair"}, "C44": {"wallet": 7}, "C48": {"d": "2b 4r"}, "C49": {"d": "5b 5r 2b"}, "C54": {"prev": "pair"},
    "C56": {"d": "3b 3b 5b 6b 2b"}, "C57": {"d": "4r 4r 4r 2b"}, "C66": {"d": "1r 2b 3b"}, "C67": {"d": "6r 2b"},
    "C68": {"srb": 1}, "C69": {"d": "4r 4r 2b"}, "C70": {"d": "2b 4r", "p": "1b 6r"}, "C71": {"d": "2b 4r", "p": "6b 6r"},
    "C72": {"d": "2b 4r", "p": "1b 6r"}, "C73": {"d": "4r 4r 2b", "p": "1r 6r"}, "C74": {"d": "2b 4r", "p": "1b 6r"},
    "C75": {"ru": 1, "rr": 2}, "C80": {"d": "1r 6b 2b 5r"}, "C81": {"d": "1r 3b 5b"}, "C82": {"d": "3b 3r 3b 5b 5r"},
    "C83": {"d": "1r 2b 3b 4r 5b"}, "C85": {"d": "3b 3r 3b"}, "C86": {"d": "1r 1b 2b 2r 3b 3r", "cap": 6},
    "C87": {"d": "4b 4r 4b 4b"}, "C88": {"d": "1r 2b 4r 6b"}, "C89": {"d": "2b:gold 4r"}, "C91": {"d": "6b 2b 5b 1r 4r"},
    "C92": {"d": "2b:mono 4r"}, "C93": {"d": "2b:same 4r"}, "C102": {"srb": 1}, "C103": {"d": "2b 4r 5b"},
    "C104": {"d": "2r 3b 4r"}, "C105": {"d": "2r 3r 4b"}, "C106": {"d": "2r 3b 2b"}, "C107": {"d": "2r 3b 4r"},
    "C109": {"d": "2r 3b 4r"}, "C126": {"d": "2r 3b", "p": "1b 6r"}, "C130": {"kind": "boss"}, "C135": {"inv": 2},
    "C137": {"star": 3}, "C138": {"wallet": 9}, "C139": {"wallet": 10}, "C142": {"d": "1r 2b 3b 4r 5b"},
    "C144": {"d": "2r 4b 6b"}, "C146": {"d": "4r 4r 2b"}, "C147": {"d": "2r 3b", "p": "1b 6r"}, "C148": {"ru": 1, "rr": 2},
    "C149": {"d": "2b"}, "C150": {"ordinal": 1}, "C152": {"sup": "C95"}, "C155": {"d": "4r 4r 4r 4r 1r"},
}
MISS = {
    "C02": {}, "C06": {"d": "2r 2b"}, "C07": {"d": "2r 2b"}, "C08": {}, "C10": {}, "C11": {}, "C12": {"d": "2r 4b 6b"},
    "C13": {"d": "2r 4b"}, "C14": {}, "C15": {"d": "2r 3b 4b 5r"}, "C17": {"d": "4r 5b 6b"}, "C18": {},
    "C19": {"d": "3b 3b 5b 4r"}, "C20": {"d": "2r 3b 6b"}, "C24": {}, "C25": {"d": "1b 4b 2b"}, "C26": {},
    "C27": {"d": "1r 3b 5b"}, "C28": {}, "C29": {"d": "1r 2b 3b 5r"}, "C30": {}, "C31": {}, "C32": {}, "C33": {"d": "5b 4r"},
    "C36": {}, "C37": {}, "C39": {"d": "2r 4r"}, "C40": {"d": "5r 3b 1b"}, "C43": {}, "C44": {}, "C48": {},
    "C65": {"d": "2b 4r"}, "C66": {"d": "4r 5b"}, "C67": {}, "C68": {}, "C69": {"d": "2b 4b"}, "C70": {},
    "C71": {"p": "2b"}, "C72": {}, "C73": {"p": "2b"}, "C74": {}, "C75": {}, "C76": {"ru": 1, "rr": 2},
    "C77": {"ru": 2, "rr": 1}, "C80": {"d": "1r 2b 3b"}, "C81": {}, "C82": {}, "C83": {}, "C84": {"d": "1r 3b 5b"},
    "C85": {}, "C86": {}, "C87": {}, "C88": {}, "C89": {}, "C91": {}, "C92": {}, "C93": {}, "C102": {}, "C103": {},
    "C104": {}, "C105": {}, "C106": {"d": "2r 3b 4b"}, "C107": {"d": "5r 3b 1b"}, "C109": {"d": "2r 3b"}, "C126": {},
    "C127": {"d": "6b"}, "C130": {}, "C135": {}, "C137": {"star": 6}, "C138": {"wallet": 1}, "C139": {"wallet": 9},
    "C142": {}, "C143": {"d": "3r 3b 3b"}, "C144": {}, "C146": {"d": "2b 4b"}, "C147": {}, "C148": {}, "C149": {},
    "C150": {}, "C151": {"sup": "C96 C96 C96 C96"}, "C152": {}, "C153": {"ordinal": 1}, "C155": {},
    "C156": {"before": "80"},
}
# The coverage items of RULES_DELTA 14 (rows "十步计分" and "随机和成长") that are pure scoring; every tag needs a case.
SPEC14_TAGS = (
    ["无卡", "混色", "全蓝0级", "全蓝12级", "全红0级", "全红12级"] + [f"红组{k}" for k in ("0", "1", "2", "3", "4", "5", "6", "7", "8", "9+")]
    + ["C24", "C25", "C73", "C146", "C69", "点数有效化", "封印", "基础分附魔", "倍率附魔", "黄金", "玻璃", "再计", "精确减半", "末尾取整",
       "顺子与同点同时成立", "实际12级表观0级", "C49两个同名惩罚", "C138预算先后"]
    + [f"C53票{k}" for k in range(9)] + ["C53模仿共用", "C53失效不抽", "C137明星", "C137失效", "连庄两次减半", "周期追赶", "停滞", "返璞归真",
                                        "C125读值"] + [f"C141衰减{k}" for k in range(4)] + [f"成长读值{c}" for c in ordered(GROWTH)])


def build_new_cases(cfg):
    """Hand-written cases for the rules text examples and the section 14 list, then the per-card matrix."""
    std = cfg["diceDefinition"]["faces"]
    names = {"r": "red", "b": "blue"}
    definitions = {c["id"]: c for c in cfg["controllers"]}
    table53 = definitions["C53"]["parameters"]["factorUnits2"]
    cases, infos, mismatches, tags = [], [], [], {}
    counter = [0]

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
            require(set(flags) <= {"smooth", "rough", "gold", "glass", "mono", "same", "temp"}, token)
            out.append(die(pip, color, face=pip, pips=[pip] * 6 if "same" in flags else None, colors=color * 6 if "mono" in flags else None,
                           tex={pip: "smooth"} if "smooth" in flags else {pip: "rough"} if "rough" in flags else None,
                           gold=(pip,) if "gold" in flags else (), glass="glass" in flags, temp="temp" in flags))
        return out

    def cards(text):
        out = []
        for i, token in enumerate(text.split()):
            head, *mods = token.split(":")
            e = {"instanceId": f"ctl_{i + 1:02d}", "definitionId": head, "enchantmentId": "none", "growth": 0, "growthCatchup": 0,
                 "turnoverUnits2": 3, "activeColor": "", "starPip": 0}
            for m in mods:
                if m.startswith("g="): e["growth"] = int(m[2:])
                elif m.startswith("k="): e["growthCatchup"] = int(m[2:])
                elif m.startswith("s="): e["starPip"] = int(m[2:])
                elif m in ("red", "blue"): e["activeColor"] = m
                else: e["enchantmentId"] = m
            out.append(e)
        return out

    def add(desc, d, c="", p="", s=None, chk=None, plan=None, tag=(), cid=None, meta=None, cap=None, rr=0, ru=None, srb=None, ordinal=None,
            perm=None, wallet=0, wave=False, bwave=False, prev=None, lv=None, clv=None, rnd=(), kind=None, mod="", dis=None, used=None,
            inv=0, slots=None, item=0, before="0", target="160", debug=False, bind=None):
        d = dice(d) if isinstance(d, str) else d
        controllers = cards(c)
        pool = [{"pip": int(t[0]), "color": names[t[1]]} for t in p.split()]
        n = len(d)
        counter[0] += 0 if cid else 1
        case_id = cid or f"Q{counter[0]:03d}"
        disabled = list(dis) if dis is not None else disabled_by_modifier(cfg, controllers, mod)
        rolls, chances, auto_cap, _ = stage_budgets(cfg, controllers, bind, mod, set(disabled))
        cap = auto_cap if cap is None else cap
        if ordinal is not None:
            srb = chances - ordinal
        srb = chances if srb is None else srb
        ordinal = chances - srb
        if prev is None:
            prev = "" if ordinal == 0 else "scatter"
        if used is None:
            used = [] if ordinal == 0 or prev in ("", "scatter") else [prev]
        owners = [x["instanceId"] for x in controllers if x["definitionId"] == "C53" and x["instanceId"] not in set(disabled)]
        require(len(rnd) == len(owners), f"{case_id}: one C53 ticket per real enabled C53")
        occupied = occupied_slots(cfg, controllers)
        inp = {
            "dice": [{"dieId": f"D{i + 1:03d}", "faceId": x["faceId"], "pip": x["pip"], "glass": x["glass"],
                      "lifetime": LIFETIME_TEMPORARY if x["temp"] else LIFETIME_PERMANENT, "faces": x["faces"]} for i, x in enumerate(d)],
            "pool": [{"dieId": f"D{n + i + 1:03d}", "faceId": f"F0{x['pip']}", "pip": x["pip"], "color": x["color"]} for i, x in enumerate(pool)],
            "capacity": cap, "rollsRemaining": rr, "rollsUsed": rolls - rr if ru is None else ru,
            "submissionsRemainingBefore": srb,
            "permanentDiceCount": min(n + len(pool) - sum(1 for x in d if x["temp"]), cfg["maxPermanentDice"]) if perm is None else perm,
            "submissionOrdinal": ordinal, "wallet": wallet, "redWave": wave, "blueWave": bwave, "previousPrimary": prev,
            "controllers": controllers,
            "handLevels": [{"handId": k, "level": v} for k, v in (lv or {}).items()],
            "colorLevels": [{"handId": k, "level": v} for k, v in (clv or {}).items()],
            "randomChoices": [{"ownerInstanceId": o, "units2": table53[t], "ticket": t} for o, t in zip(owners, rnd)],
            "stageKind": kind or ("boss" if mod else "normal"), "modifierId": mod, "disabledControllerIds": disabled,
            "stageHandsUsed": list(used), "inventoryCount": inv,
            "ordinaryControllerSlots": max(cfg["initialControllerSlots"], min(cfg["maxOrdinaryControllerSlots"], occupied)) if slots is None else slots,
            "scoreItemMultiplier": item, "stageScoreBefore": before, "stageTarget": target, "bindings": bind, "debug": debug,
        }
        validate(case_id, inp, cfg, strict=True, synthetic_disabled=dis is not None, remote_allowed=any(x["remote"] for x in d))
        expected, info = score(inp, cfg)
        pins = dict(chk or {}, **({"score": s} if s is not None else {}))
        for key, value in pins.items():
            if expected[key] != value:
                mismatches.append(f"{case_id} {desc}: hand-computed {key}={value!r}, reference gives {expected[key]!r}")
        for key, value in (plan or {}).items():
            if expected["afterScorePlan"][key] != value:
                mismatches.append(f"{case_id} {desc}: hand-computed plan {key}={value!r}, reference gives {expected['afterScorePlan'][key]!r}")
        case = {"id": case_id, "description": desc, "source": "matrix" if meta else "new"}
        case.update(meta or {})
        case.update({"handChecked": bool(pins or plan), "input": inp, "expected": expected})
        if pins or plan:
            case["handChecks"] = {"expected": pins, "afterScorePlan": plan or {}}
        for t in ([tag] if isinstance(tag, str) else tag):
            tags.setdefault(t, []).append(case_id)
        cases.append(case)
        infos.append(info)
        return case

    BLUE = "3b 3b 5b 6b 2b"    # 19 pips, pair, all blue (+0.25)        -> 47
    SCAT = "1r 3b 5b 6b"       # 15 pips, scatter, one red              -> 15
    RED2 = "4r 4r 2b 3b 6b"    # 19 pips, pair, red group 2 (x1)        -> 38
    RED3 = "4r 4r 4r 2b 6b"    # 20 pips, trips, red group 3 (x2)       -> 160
    STR = "1r 2b 3b 4r 5b"     # 15 pips, straight x10                  -> 150
    FULL = "3b 3b 3b 5b 5b"    # 19 pips, full house, all blue          -> 142
    QUAD = "5b 5b 5b 5b 2b"    # 22 pips, quads, all blue               -> 220
    ALLR = "4r 4r 1r"          # 9 pips, pair, all red (+0.5), group 2  -> 27
    one = lambda owner, ability, delta, halve=False: [{"ownerInstanceId": owner, "abilityId": ability, "delta": delta, "reset": False, "halve": halve}]  # noqa: E731
    fc = lambda source, emitter, die_id, face_id, kind, amount=0, text="", owner=None: {  # noqa: E731
        "sourceId": source, "emitterId": emitter, "stateOwnerId": owner or emitter, "dieId": die_id, "faceId": face_id, "kind": kind, "amount": amount, "text": text}
    ent = lambda kind, ability, emitter, owner, index, amount: {  # noqa: E731
        "kind": kind, "abilityId": ability, "emitterId": emitter, "stateOwnerId": owner, "dieIndex": index, "amount100": amount}
    bnd = lambda ability, emitter, owner=None: {"abilityId": ability, "emitterId": emitter, "stateOwnerId": owner or emitter}  # noqa: E731

    # ---- RULES_DELTA 2: order, zones, the worked examples ----
    add("无卡 混色一对：17×2", PAIR, s="34", chk={"additiveEntries": [], "additiveTotal100": 0, "colorId": "", "multiplierFactors": []}, tag=("无卡", "混色", "红组1"))
    add("无卡 散点带一颗红：15", SCAT, s="15", chk={"primaryHandId": "scatter", "redCount": 1})
    add("无卡 全蓝 0 级 +0.25：19×2×1.25=47.5", BLUE, s="47", chk={"colorAdd100": 25, "additiveTotal100": 25, "scoreNumerator": "95", "scoreDenominator": "2",
        "additiveEntries": [ent("color", "all_blue", None, None, -1, 25)], "redCount": 0, "redGroupKey": 0}, tag=("全蓝0级", "红组0", "末尾取整"))
    add("无卡 全红 0 级 +0.5，红组二举 ×1：9×2×1.5", ALLR, s="27", chk={"colorAdd100": 50, "redCount": 2, "redMult100": 100}, tag=("全红0级", "红组2"))
    add("§3.1 例 3 级全蓝 +1.75：19×2×2.75=104.5", BLUE, clv={"all_blue": 3}, s="104", chk={"colorAdd100": 175})
    add("§3.1 例 12 级全红 +6.5：9×2×7.5", ALLR, clv={"all_red": 12}, s="135", chk={"colorAdd100": 650}, tag="全红12级")
    add("12 级全蓝 +6.25：19×2×7.25=275.5", BLUE, clv={"all_blue": 12}, s="275", chk={"colorAdd100": 625}, tag="全蓝12级")
    add("§2 例 基础分20、两对×3、红组×2、+2、+3、黄金+0.5、倍率附魔+1.5、×2.5、首领×1/2 = 1200", "4r:gold 6r 6r 4b", "C25 C46:mult2 C105 C09", mod="B26", s="1200",
        chk={"baseTotal": "20", "totalHandMult100": 300, "redMult100": 200, "additiveTotal100": 700, "enchantAdditiveCount": 1,
             "controllerMultiplierNumerator": "5", "controllerMultiplierDenominator": "2",
             "additiveEntries": [ent("gold", "U06", None, None, 0, 50), ent("controller", "C46", "ctl_02", "ctl_02", -1, 200),
                                 ent("controller", "C105", "ctl_03", "ctl_03", -1, 300), ent("enchantment", "mult2", "ctl_02", "ctl_02", -1, 150)],
             "globalFactors": [{"emitterId": "BOSS", "abilityId": "B26", "numerator": "1", "denominator": "2"}]}, tag=("黄金", "倍率附魔"))
    add("§2 例 续：黄金骰再计一次只改基础分 20→24，加成合计仍是 7：24×3×2×8×2.5×0.5", "4r:gold 6r 6r 4b", "C25 C46:mult2 C105 C09 C64", mod="B26", s="1440",
        chk={"baseTotal": "24", "additiveTotal100": 700, "pipRetriggers": [1, 0, 0, 0]}, tag="再计")
    add("§2 例 1 级一对 ×2.75 被半吊子减半为 ×1.37，基础分 200：274", "3b 3b 4r", "C50 C44", wallet=80, lv={"pair": 1}, mod="B19", s="274",
        chk={"baseTotal": "200", "handMultBeforeModifier100": 275, "totalHandMult100": 137}, tag="精确减半")
    add("§14 手算例 4、4、4、4、1 全红、两颗黄金、C155 ×2、C149 未触发、C53 票 3 ×1.5：17×8×3×2.5×2×1.5", "4r:gold 4r:gold 4r 4r 1r", "C155 C149 C53", rnd=(3,), s="3060",
        chk={"baseTotal": "17", "totalHandMult100": 800, "redCount": 4, "redMult100": 300, "additiveTotal100": 150, "triggeredSourceIds": ["ctl_01", "ctl_03"],
             "controllerMultiplierNumerator": "3", "controllerMultiplierDenominator": "1"}, tag=("红组4", "C53票3"))
    add("§14 手算例 续：首领整手减半（慢热首手）得 1530", "4r:gold 4r:gold 4r 4r 1r", "C155 C149 C53", rnd=(3,), mod="B26", s="1530")
    add("灵光一闪 ×2 列在卡牌之外", PAIR, item=2, s="68", chk={"globalFactors": [{"emitterId": "ITEM", "abilityId": "I09", "numerator": "2", "denominator": "1"}]})
    add("末尾只取整一次：19×0.5=9.5 → 慢热后 19", RED2, mod="B26", s="19")
    add("末尾只取整一次：散点 15 减半得 7", SCAT, mod="B26", s="7", chk={"scoreNumerator": "15", "scoreDenominator": "2"}, tag="末尾取整")
    add("半吊子 1 级一对：17×1.37=23.29", PAIR, lv={"pair": 1}, mod="B19", s="23", chk={"totalHandMult100": 137}, tag="精确减半")
    add("顺子与一对同时成立：20×(2+10)", "1r 2b 3b 4r 5b 5b", cap=6, s="240", chk={"primaryHandId": "straight", "chosenGroupId": "pair", "totalHandMult100": 1200}, tag="顺子与同点同时成立")
    add("一对 12 级：17×(2+0.75×12)", PAIR, lv={"pair": 12}, s="187", chk={"totalHandMult100": 1100})
    add("实际 12 级的一对在打回原形下按 0 级计分，C43 触发但不升级", PAIR, "C43", lv={"pair": 12}, prev="pair", mod="B28", s="34",
        chk={"totalHandMult100": 200, "triggeredSourceIds": ["ctl_01"]}, plan={"upgradeHandId": "pair", "upgradeRequested": 1, "upgradeApplied": 0}, tag="实际12级表观0级")
    add("§3.1 例 11 级有两次 C43 请求时实升 1 级：17×10.25=174.25", PAIR, "C41 C43", lv={"pair": 11}, prev="pair", s="174",
        plan={"upgradeHandId": "pair", "upgradeRequested": 2, "upgradeApplied": 1})
    add("§4.3 C43 例 两次一对、11 级后升至 12", PAIR, "C43", lv={"pair": 11}, prev="pair", s="174", plan={"upgradeRequested": 1, "upgradeApplied": 1})

    # ---- RULES_DELTA 3.2: the red group ----
    add("红组 2 颗（二举）×1", RED2, s="38", chk={"redCount": 2, "redMult100": 100, "redGroupKey": 4, "redMemberIndices": [0, 1]}, tag="红组2")
    add("红组 3 颗 ×2", RED3, s="160", chk={"redCount": 3, "redMult100": 200}, tag="红组3")
    add("红组 4 颗 ×3：18×8×3", "4r 4r 4r 4r 2b", s="432", chk={"redMult100": 300}, tag="红组4")
    add("红组 5 颗 ×4.5：20×12×4.5×1.5", "4r 4r 4r 4r 4r", s="1620", chk={"redMult100": 450}, tag="红组5")
    add("红组 6 颗 ×6：24×18×6×1.5", "4r 4r 4r 4r 4r 4r", cap=6, s="3888", chk={"redMult100": 600}, tag="红组6")
    add("红组 7 颗 ×8：28×27×8×1.5", "4r 4r 4r 4r 4r 4r 4r", cap=7, s="9072", chk={"redMult100": 800}, tag="红组7")
    add("红组 8 颗 ×11：32×40×11×1.5", "4r 4r 4r 4r 4r 4r 4r 4r", cap=8, s="21120", chk={"redMult100": 1100}, tag="红组8")
    add("红组 5+3+1=9：数量记 9，倍率按 8 取 ×11", "4r 4r 4r 4r 4r", "C73 C146", p="1r 4r 1r", s="3960", chk={"redCount": 9, "redPoolCount": 3, "redBonusCount": 1, "redMult100": 1100}, tag="红组9+")
    add("§3.2 例 提交红1、红4、蓝6，留池红2，无卡：同数取键 4", "1r 4r 6b", p="2r", s="11", chk={"redGroupKey": 4, "redCount": 1, "redMemberIndices": [1], "colorId": ""})
    add("§3.2 例 C24 把红 1 并入键 4：2 颗", "1r 4r 6b", "C24", p="2r", s="11", chk={"redGroupKey": 4, "redCount": 2, "redMemberIndices": [0, 1]}, tag="C24")
    add("§3.2 例 C24 + C73：留池红骰并入，3 颗 ×2", "1r 4r 6b", "C24 C73", p="2r", s="22", chk={"redCount": 3, "redPoolCount": 1}, tag="C73")
    add("§3.2 例 C24 + C73 + C146：4 颗 ×3", "1r 4r 6b", "C24 C73 C146", p="2r", s="33", chk={"redCount": 4, "redBonusCount": 1, "redMult100": 300}, tag="C146")
    add("§3.2 例 再加 C25：组键为 0，仍是 4 颗", "1r 4r 6b", "C24 C73 C146 C25", p="2r", s="33", chk={"redGroupKey": 0, "redCount": 4, "triggeredSourceIds": ["ctl_01", "ctl_02", "ctl_03", "ctl_04"]}, tag="C25")
    add("§3.2 例 C69 只为红 1、红 4 各再计一次：(11+5)×3", "1r 4r 6b", "C24 C73 C146 C25 C69", p="2r", s="48", chk={"pipRetriggers": [1, 1, 0], "pipSum": "16", "colorId": ""}, tag="C69")
    add("§3.2 例 提交红 4、4 且没有追加来源：二举 ×1", "4r 4r", s="24", chk={"redCount": 2, "redMult100": 100})
    add("C73 没有提交红骰时不追加", BLUE, "C73 C146", p="1r 4r", s="47", chk={"redCount": 0, "redPoolCount": 0, "redBonusCount": 0, "triggeredSourceIds": []})
    add("褪红：红组 ×1、全红加成清零，数量与 C57 成长照常", "4r 4r 4r", "C57", mod="B22", s="96", chk={"redCount": 3, "redMult100": 100, "colorAdd100": 0, "colorId": "all_red"},
        plan={"growthChanges": one("ctl_01", "C57", 80)})
    add("褪蓝：全蓝加成清零，黄金面仍 +0.5：12×2×1.5", "6b:gold 3b 3b", mod="B23", s="36", chk={"colorAdd100": 0, "additiveTotal100": 50})

    # ---- effective pips, seals, enchantments, gold, glass ----
    add("不要奇数：1、3、5 按有效 2、4、6", "1r 3b 5b", "C12", s="12", chk={"pipContributions": [2, 4, 6], "triggeredSourceIds": ["ctl_01"]}, tag="点数有效化")
    add("首当其冲使首颗视红，红色浪潮最后覆盖为全红：19×2×1.5", BLUE, "C03", wave=True, s="57", chk={"colorId": "all_red", "redCount": 2})
    add("出头鸟封印黄金首骰：贡献 0，黄金 +0.5 保留：6×2×1.75", "6b:gold 3b 3b", mod="B05", s="21", chk={"pipContributions": [0, 3, 3], "sealedDice": [True, False, False], "additiveTotal100": 75}, tag="封印")
    add("封印骰照记再计次数", "6b 3b 3b", "C64", mod="B05", s="15", chk={"pipRetriggers": [1, 0, 0], "pipSum": "6"})
    add("基础分附魔触发 +15：(17+30+15)×2", PAIR, "C50:base10", s="124", chk={"enchantBaseAdd": 15, "baseTotal": "62"}, tag="基础分附魔")
    add("倍率附魔触发 +1.5 进加成区：47×2×2.5", PAIR, "C50:mult2", s="235", chk={"enchantAdditiveCount": 1, "additiveTotal100": 150,
        "additiveEntries": [ent("enchantment", "mult2", "ctl_01", "ctl_01", -1, 150)], "multiplierFactors": []}, tag="倍率附魔")
    add("倍率附魔在未触发的卡上：不发分", PAIR, "C149:mult2", s="34", chk={"enchantAdditiveCount": 0, "additiveEntries": []})
    add("沉默：倍率附魔不给 +1.5，基础技能照常", PAIR, "C50:mult2", mod="B06", s="94", chk={"enchantAdditiveCount": 0, "triggeredSourceIds": ["ctl_01"]})
    add("沉默：基础分附魔不给 +15", PAIR, "C50:base10", mod="B06", s="94", chk={"enchantBaseAdd": 0})
    add("带倍率附魔的百变模仿复制稳稳乘数：+2、+2、+1.5，附魔条目的状态所有者是被模仿的卡", PAIR, "C41:mult2 C46", s="221",
        chk={"additiveEntries": [ent("controller", "C46", "ctl_01", "ctl_02", -1, 200), ent("controller", "C46", "ctl_02", "ctl_02", -1, 200),
                                 ent("enchantment", "mult2", "ctl_01", "ctl_02", -1, 150)]})
    add("黄金面朝上并提交 +0.5，点数不翻倍：12×2×1.75", "6b:gold 3b 3b", s="42", chk={"pipContributions": [6, 3, 3], "additiveEntries": [
        ent("color", "all_blue", None, None, -1, 25), ent("gold", "U06", None, None, 0, 50)]}, tag="黄金")
    add("黄金骰再计不增加黄金项：18×2×1.75", "6b:gold 3b 3b", "C64", s="63", chk={"additiveTotal100": 75, "pipSum": "18"})
    add("§7.2 例 两颗黄金面 +1，带 C89 再 ×1.69：12×2×2.25×1.69=91.26", "6b:gold 3b:gold 3b", "C89", s="91",
        chk={"additiveTotal100": 125, "controllerMultiplierNumerator": "169", "controllerMultiplierDenominator": "100"}, tag="黄金")
    add("玻璃骰贡献 ×3 并列入碎裂风险：24×2×1.25", "6b:glass 3b 3b", s="60", chk={"pipContributions": [18, 3, 3]}, plan={"glassRiskDieIds": ["D001"]}, tag="玻璃")
    add("§7.3 例 提交两颗冻结玻璃：两颗都在风险名单，按提交顺序", "5b:glass 5b:glass", s="75", plan={"glassRiskDieIds": ["D001", "D002"]}, tag="玻璃")
    add("玻璃骰被封印仍列入风险名单", "6b:glass 3b 3b", mod="B05", s="15", chk={"pipContributions": [0, 3, 3]}, plan={"glassRiskDieIds": ["D001"]})
    add("釜底抽薪：玻璃与逐骰加值不计，黄金 +0.5 保留：12×2×1.75", "6b:gold:glass 3b 3b", "C39", mod="B39", s="42", chk={"pipContributions": [6, 3, 3], "additiveTotal100": 75})
    add("釜底抽薪：整手基础分不计，加成照常：17×2×3", PAIR, "C46 C50", mod="B39", s="102", chk={"baseTotal": "17", "triggeredSourceIds": ["ctl_01", "ctl_02"]})

    # ---- RULES_DELTA 4.1: multiplier cards ----
    add("§4.1 C02 全蓝一颗也 ×3：6×1.25×3=22.5", "6b", "C02", s="22", chk={"multiplierFactors": [{"emitterId": "ctl_01", "abilityId": "C02", "numerator": "3", "denominator": "1"}]})
    add("§4.1 C09 有效提交 ×2.5，容量 5 变 4", "3b 3b 5b 4r", "C09", s="75", chk={"controllerMultiplierNumerator": "5", "controllerMultiplierDenominator": "2"})
    add("§4.1 C31 五颗 3 也 ×3：15×12×1.25×3", "3b 3b 3b 3b 3b", "C31", s="675")
    add("§4.1 C33 5、2 满足：7×1.25×3=26.25", "5b 2b", "C33", s="26", chk={"triggeredSourceIds": ["ctl_01"]})
    add("§4.1 C33 2、5 不满足", "2b 5b", "C33", s="8", chk={"triggeredSourceIds": []})
    add("§4.1 C33 差恰为 3：6、3 满足", "6b 3b", "C33", s="33")
    add("§4.1 C33 差为 2：5、3 不满足", "5b 3b", "C33", s="10", chk={"triggeredSourceIds": []})
    add("§4.1 C41 复制 ×3 时贡献 ×3：6×1.25×9=67.5", "6b", "C41 C02", s="67", chk={"controllerMultiplierNumerator": "9"})
    add("§4.1 C41 复制 +3 时进加成区：6×(1+3+3)", "1r 2b 3b", "C41 C06", s="42", chk={"additiveTotal100": 600, "multiplierFactors": []})
    add("§4.1 C42 有效提交 ×4，发出者列入自毁风险", PAIR, "C42", s="136", plan={"cannonRiskEmitterIds": ["ctl_01"]})
    add("§4.1 C42 被复制：两个发出者各自承担风险，×16", PAIR, "C41 C42", s="544", plan={"cannonRiskEmitterIds": ["ctl_01", "ctl_02"]})
    for ticket, total in enumerate(("17", "34", "34", "51", "68", "68", "102", "102", "136")):
        add(f"§4.1 C53 票 {ticket} 为 ×{Fraction(table53[ticket], 2)}", PAIR, "C53", rnd=(ticket,), s=total,
            chk={"controllerMultiplierNumerator": str(Fraction(table53[ticket], 2).numerator)}, plan={"randomFactorOwnerIds": ["ctl_01"]}, tag=f"C53票{ticket}")
    add("C53 模仿共用真实所有者的票 0：34×0.25=8.5", PAIR, "C41 C53", rnd=(0,), s="8", chk={"controllerMultiplierNumerator": "1", "controllerMultiplierDenominator": "4"},
        plan={"randomFactorOwnerIds": ["ctl_02"]}, tag="C53模仿共用")
    add("两张真实 C53 各抽各的：票 0 与票 8，×0.5×4", PAIR, "C53 C53", rnd=(0, 8), s="68", plan={"randomFactorOwnerIds": ["ctl_01", "ctl_02"]})
    add("失效的 C53 不抽票、不发技能", PAIR, "C53", mod="B25", s="34", chk={"triggeredSourceIds": []}, plan={"randomFactorOwnerIds": []}, tag="C53失效不抽")
    add("§4.1 C71 两颗留池 6 点 ×9/4：34×2.25=76.5", PAIR, "C71", p="6b 6r", s="76", chk={"controllerMultiplierNumerator": "9", "controllerMultiplierDenominator": "4"})
    add("§4.1 C87 五同 ×9/4：15×12×1.25×2.25=506.25", "3b 3b 3b 3b 3b", "C87", s="506")
    add("§4.1 C87 四同 ×3/2", QUAD, "C87", s="330")
    add("§4.1 C100 吃掉基础价 6 的卡后 ×2.15：34×2.15=73.1", PAIR, "C100:g=90", s="73", chk={"controllerMultiplierNumerator": "43", "controllerMultiplierDenominator": "20"}, tag="成长读值C100")
    add("§4.1 C101 有效提交 ×3（本关提交 3 次变 2 次）", PAIR, "C101", s="102", chk={"triggeredSourceIds": ["ctl_01"]})
    add("§4.1 C103 3 永久 + 2 临时仍 ×3", "3b 3b 5b 4r:temp 2b:temp", "C103", s="102")
    add("§4.1 C103 恰为 4 颗永久 ×2", "3b 3b 5b 4r", "C103", s="60")
    add("§4.1 C103 5 颗永久不触发", PAIR, "C103", s="34", chk={"triggeredSourceIds": []})
    add("§4.1 C130 首领关 ×3，减益已解除也触发", PAIR, "C130", kind="boss", s="102")
    add("§4.1 C130 高压关无因子", PAIR, "C130", kind="pressure", s="34", chk={"triggeredSourceIds": []})
    add("§4.1 C138 3 金、两个来源时仅左侧触发，待扣 2", PAIR, "C138 C138", wallet=3, s="85", chk={"triggeredSourceIds": ["ctl_01"]}, plan={"goldSpent": 2}, tag="C138预算先后")
    add("C138 4 金两个来源都触发：34×6.25=212.5，待扣 4", PAIR, "C138 C138", wallet=4, s="212", plan={"goldSpent": 4}, tag="C138预算先后")
    add("C138 模仿在左：3 金只够模仿来源", PAIR, "C41 C138", wallet=3, s="85", chk={"triggeredSourceIds": ["ctl_01"]}, plan={"goldSpent": 2})
    add("C138 1 金不触发；存钱罐本手收入不能垫付", PAIR, "C01 C138", wallet=1, s="34", chk={"triggeredSourceIds": ["ctl_01"]}, plan={"goldSpent": 0, "earnedGold": 1})
    add("C138 与 C44、C139 都读冻结钱包 20：(17+40)×2×(1+1)×2.5", PAIR, "C138 C44 C139", wallet=20, s="570", plan={"goldSpent": 2})

    # ---- RULES_DELTA 4.2: immediate additive cards ----
    add("§4.2 C06 1、2、3 满足 +3", "1r 2b 3b", "C06", s="24")
    add("§4.2 C08 3/5 格满足 +2：11×2×3.25=71.5", "3b 3b 5b", "C08", s="71")
    add("§4.2 C11 2、2、3、3、6 满足 +8：16×3×9.25", "2b 2b 3b 3b 6b", "C11", s="444")
    add("§4.2 C14 1、3 满足 +4", "1r 3b", "C14", s="20")
    add("§4.2 C18 两红两蓝满足 +5", "1r 4r 2b 3b", "C18", s="60")
    add("§4.2 C28 2、2、3、3 满足 +4：10×3×5.25=157.5", "2b 2b 3b 3b", "C28", s="157")
    add("§4.2 C28 四个 2 不满足", "2b 2b 2b 2b", "C28", s="80", chk={"triggeredSourceIds": []})
    add("§4.2 C30 四同也满足 +2.5：8×8×3.75", "2b 2b 2b 2b", "C30", s="240")
    add("§4.2 C32 单骰也满足 +3：6×4.25=25.5", "6b", "C32", s="25")
    add("§4.2 C40 1、3、5、2 满足 +4.5：11×5.5=60.5", "1r 3b 5b 2b", "C40", s="60")
    add("§4.2 C40 1、2、4 相邻差不等，不满足", "1r 2b 4r", "C40", s="7", chk={"triggeredSourceIds": []})
    add("§4.2 C46 无其它项时 ×3", PAIR, "C46", s="102", chk={"additiveTotal100": 200})
    add("§4.2 C47 5 张 +3，含自己与非计分卡", PAIR, "C47 C95 C96 C97 C98", s="136", chk={"additiveTotal100": 300})
    add("§4.2 C47 失效卡仍计入持有数：缴械后第 3 张 C47 按 3 张 +1.8", PAIR, "C50 C46 C47", mod="B25", s="95", chk={"additiveTotal100": 180, "triggeredSourceIds": ["ctl_03"]})
    add("§4.2 C49 两张、两个 5 为 +6：13×2×7.25=188.5", "5b 5b 3b", "C49 C49", s="188", chk={"additiveTotal100": 625, "multiplierFactors": []})
    add("§4.2 C49 两张、零个 5 为 ×1/4，两张都算触发", "3b 3b 2b", "C49 C49", s="5", chk={"triggeredSourceIds": ["ctl_01", "ctl_02"], "controllerMultiplierNumerator": "1",
        "controllerMultiplierDenominator": "4", "additiveTotal100": 25}, tag="C49两个同名惩罚")
    add("C49 无 5 点分支也触发自己的倍率附魔：8×2×2.75×0.5", "3b 3b 2b", "C49:mult2", s="22", chk={"enchantAdditiveCount": 1})
    add("§4.2 C63 5 永久 + 1 临时为 +2.5", PAIR, "C63", p="6b", perm=5, s="119", chk={"additiveTotal100": 250})
    add("§4.2 C74 留 2 交 2 满足 +4：8×5.25", "3b 5b", "C74", p="1r 2b", s="42")
    add("§4.2 C74 留 1 交 2 不满足", "3b 5b", "C74", p="1r", s="10", chk={"triggeredSourceIds": []})
    add("§4.2 C75 已掷 1 次满足 +4.5：(17+6)×2×5.5", PAIR, "C75", ru=1, rr=2, s="253")
    add("§4.2 C77 剩余投掷为 0 满足 +2.5", PAIR, "C77", s="119")
    add("§4.2 C77 还剩 1 次不满足：(17+3)×2", PAIR, "C77", ru=2, rr=1, s="40", chk={"triggeredSourceIds": []})
    add("§4.2 C81 红色散点仍满足 +8：5×9.5=47.5", "1r 4r", "C81", s="47", chk={"colorId": "all_red"})
    add("§4.2 C82 匹配含三条带一对 +4.5：19×6×5.75=655.5", FULL, "C82", s="655")
    add("§4.2 C82 禁大牌型（小打小闹）时该匹配消失", FULL, "C82", mod="B20", s="95", chk={"triggeredSourceIds": [], "primaryHandId": "trips"})
    add("§4.2 C83 有 C29 时四连也可 +6：14×10×7", "2b 3b 4r 5b", "C29 C83", s="980", chk={"triggeredSourceIds": ["ctl_01", "ctl_02"]})
    add("§4.2 C86 三对子满足 +8：12×7×9", "1r 1r 2b 2b 3b 3b", "C86", cap=6, s="756")
    add("§4.2 C86 四对子同时匹配三对子，一来源只加一次：22×12×9", "1r 1r 2b 2b 3b 3b 5b 5b", "C86", cap=8, s="2376", chk={"additiveTotal100": 800})
    add("§4.2 C88 1、2、5、6 满足 +6", "1r 2b 5b 6b", "C88", s="98")
    add("§4.2 C102 破釜沉舟把总次数减为 1 时首手也满足 +4.5", PAIR, "C102", mod="B33", s="187")
    add("§4.2 C104 红蓝红满足 +4.5：7×5.5=38.5", "1r 2b 4r", "C104", s="38")
    add("§4.2 C105 红红蓝满足 +3", "1r 4r 2b", "C105", s="28")
    add("§4.2 C106 2、6、2 满足 +2.5：10×2×3.75", "2b 6b 2b", "C106", s="75")
    add("§4.2 C139 69 金 +3，无上限", PAIR, "C139", wallet=69, s="136", chk={"additiveTotal100": 300})
    add("§4.2 C139 9 金无项", PAIR, "C139", wallet=9, s="34", chk={"triggeredSourceIds": []})
    add("§4.2 C139 200 金 +10，不封顶", PAIR, "C139", wallet=200, s="374", chk={"additiveTotal100": 1000})
    for decay, total in enumerate(("170", "136", "102", "68")):
        add(f"§4.2 / §5.1 C141 已衰减 {decay} 次，当前 +{4 - decay}", PAIR, f"C141:g={decay}", s=total, chk={"additiveTotal100": 400 - 100 * decay}, plan={"growthChanges": []},
            tag=f"C141衰减{decay}")
    add("§4.2 C144 2、4、6 满足 +4", "2b 4r 6b", "C144", s="60")
    add("§4.2 C147 留 3 颗 +1.8：34×2.8=95.2", PAIR, "C147", p="1r 2b 6b", s="95")
    add("§4.2 C148 剩 2 次 +2：(17+6)×2×3", PAIR, "C148", ru=1, rr=2, s="138")
    add("§4.2 C149 单骰 +8：6×9.25=55.5", "6b", "C149", s="55")
    add("§4.2 C150 第 4 手 +6", PAIR, "C150 C79", srb=1, s="238", chk={"additiveTotal100": 600, "triggeredSourceIds": ["ctl_01"]})
    add("§4.2 C151 五栏、占三，+2.5", PAIR, "C151 C95 C96", s="119", chk={"additiveTotal100": 250})
    add("§4.2 C151 最左的养成卡免栏位：五栏、三张里一张养成卡，空 3 个 +3.75", PAIR, "C151 C118 C96", s="161", chk={"additiveTotal100": 375})
    add("§4.2 C151 不占栏位附魔的养成卡不消耗养成名额：C118 靠附魔、C122 靠名额，空 4 个 +5", PAIR, "C151 C118:slotless C122", s="204", chk={"additiveTotal100": 500})
    add("§4.2 C152 两张普通 +1.2：34×2.2=74.8", PAIR, "C152 C95 C133", s="74", chk={"additiveTotal100": 120})
    add("§4.2 C153 本关首次有效提交 +4.5", PAIR, "C153", s="187")
    add("§4.2 C153 即使首手得 0 分，第二手也不触发", PAIR, "C153", ordinal=1, s="34", chk={"triggeredSourceIds": []})

    # ---- RULES_DELTA 4.3: base, hand and retrigger cards ----
    add("§4.3 C10 两颗有效 1 点各 +15", "1r 1r 3b", "C10", s="70", chk={"pipContributions": [16, 16, 3]})
    add("§4.3 C13 1、2、3 共 +16", "1r 2b 3b", "C13", s="22", chk={"pipContributions": [9, 2, 11]})
    add("§4.3 C17 3、4 仅第一颗 +9", "3b 4r", "C17", s="16", chk={"pipContributions": [12, 4]})
    add("§4.3 C20 满五、第五颗 6 时它的贡献 30", "3b 3b 5b 4r 6b", "C20", s="90", chk={"pipContributions": [3, 3, 5, 4, 30]})
    add("§4.3 C27 四同 +20：(8+20)×8×1.25", "2b 2b 2b 2b", "C27", s="280")
    add("§4.3 C27 双三条 +40：(21+40)×10×1.25=762.5", "2b 2b 2b 5b 5b 5b", "C27", cap=6, s="762", chk={"baseTotal": "61"})
    add("§4.3 C39 两蓝一红，共 +18", "2b 3b 4r", "C39", s="27", chk={"pipContributions": [11, 12, 4]})
    add("§4.3 C44 4 金 +8", PAIR, "C44", wallet=4, s="50", chk={"baseTotal": "25"})
    add("§4.3 C48 交 3/5，+30：(11+30)×2×1.25=102.5", "3b 3b 5b", "C48", s="102", chk={"baseTotal": "41"})
    add("§4.3 C50 点数合计 20，单该项得 50", "6b 6b 4r 4r", "C50", s="150", chk={"pipSum": "20", "baseTotal": "50"})
    add("§4.3 C70 留 2 颗 +18", PAIR, "C70", p="1r 2b", s="70", chk={"controllerBaseAdds": [{"emitterId": "ctl_01", "abilityId": "C70", "amount": "18"}]})
    add("§4.3 C72 留 2、6 得 +24", PAIR, "C72", p="2b 6b", s="82", chk={"controllerBaseAdds": [{"emitterId": "ctl_01", "abilityId": "C72", "amount": "24"}]})
    add("§4.3 C76 掷 3 次 +30", PAIR, "C76", s="94", chk={"baseTotal": "47"})
    add("§4.3 C80 1、1、6 只有一对 +30", "1r 1r 6b", "C80", s="76", chk={"baseTotal": "38"})
    add("§4.3 C92 全红永久骰被蓝色浪潮覆盖仍 +15：19×1.25=23.75", "4r:mono", "C92", bwave=True, s="23", chk={"pipContributions": [19], "colorId": "all_blue"})
    add("§4.3 C93 贡献 5 再 ×3 得 15", "5b:same", "C93", s="18", chk={"pipContributions": [15]})
    add("C93 在逐骰加值之后乘：(5+9)×3", "5b:same", "C39 C93", s="52", chk={"pipContributions": [42]})
    add("§4.3 C107 2、3、4 的后二颗各 +20", "2b 3b 4r", "C107", s="49", chk={"pipContributions": [2, 23, 24]})
    add("§4.3 C108 首颗 4 得 +24", "4r 3b 3b", "C108", s="68", chk={"baseTotal": "34"})
    add("C108 首颗被封印也取有效点数", "4r 3b 3b", "C108", mod="B05", s="60", chk={"baseTotal": "30"})
    add("§4.3 C135 一把满次数钥匙串按一件 +15", PAIR, "C135", inv=1, s="64", chk={"baseTotal": "32"})
    add("§4.3 C29 2、3、4、5 可成顺", "2b 3b 4r 5b", "C29", s="140", chk={"primaryHandId": "straight", "triggeredSourceIds": ["ctl_01"]})
    add("§4.3 C84 三同也使牌型倍率 +2：20×6×2", RED3, "C84", s="240", chk={"handMultAdd100": 200, "totalHandMult100": 600})
    add("§4.3 C85 四同也使牌型倍率 +4：22×12×1.25", QUAD, "C85", s="330", chk={"handMultAdd100": 400, "totalHandMult100": 1200})
    add("§4.3 C143 2、2、3、3、3 中两个 2 各计三遍，三个 3 不再计：21×6×1.25=157.5", "2b 2b 3b 3b 3b", "C143", s="157", chk={"pipRetriggers": [2, 2, 0, 0, 0], "pipSum": "21"}, tag="再计")
    add("一条龙沿用 +10，半吊子先加后减半：(10+10)/2", STR, "C142", mod="B19", s="150", chk={"handMultBeforeModifier100": 2000, "totalHandMult100": 1000})

    # ---- RULES_DELTA 4.4 / 4.5: changed mechanisms and new cards ----
    add("§4.4 C19 1、2、5 最小有效点数为 1，+2.5", "1r 2b 5b", "C19", s="28", chk={"additiveTotal100": 250})
    add("§4.4 C19 不要奇数把 1 变为 2 后给 +1.5：10×2×2.5", "1r 2b 5b", "C12 C19", s="50", chk={"additiveTotal100": 150, "pipContributions": [2, 2, 6]})
    add("§4.4 C19 最小有效点数为 3，无项", "3b 5b", "C19", s="10", chk={"triggeredSourceIds": []})
    add("§4.4 C91 6、2、5、1、4 成立 +2.5", "6b 2b 5b 1r 4r", "C91", s="63")
    add("§4.4 C91 2、6、1 成立（低开头）：9×3.5=31.5", "2b 6b 1r", "C91", s="31")
    add("§4.4 C91 2、2、1 不成立", "2b 2b 1r", "C91", s="10", chk={"triggeredSourceIds": []})
    add("§4.4 C91 1、2、3 方向不反转，不成立", "1r 2b 3b", "C91", s="6", chk={"triggeredSourceIds": []})
    add("§4.4 C91 只有两颗，不成立", "6b 2b", "C91", s="10", chk={"triggeredSourceIds": []})
    add("§4.4 C125 ×1.25 起：34×1.25=42.5", PAIR, "C125", s="42", chk={"controllerMultiplierNumerator": "5", "controllerMultiplierDenominator": "4"}, plan={"faceChanges": [], "growthChanges": []}, tag="C125读值")
    add("§4.4 / §5 C125 长过一次 ×1.45：34×1.45=49.3", PAIR, "C125:g=20", s="49", tag=("C125读值", "成长读值C125"))
    add("§4.4 C137 明星 4、提交 4、4、1，加 +2：9×2×3.5", "4r 4r 1r", "C137:s=4", s="63", chk={"additiveEntries": [
        ent("color", "all_red", None, None, -1, 50), ent("controller", "C137", "ctl_01", "ctl_01", -1, 200)]}, tag="C137明星")
    add("§5.3 例 两张真实卡明星 1、6，提交 1、1、6 时分别 +2、+1", "1r 1r 6b", "C137:s=1 C137:s=6", s="64", chk={"additiveEntries": [
        ent("controller", "C137", "ctl_01", "ctl_01", -1, 200), ent("controller", "C137", "ctl_02", "ctl_02", -1, 100)]}, tag="C137明星")
    add("§5.3 例 复制第一张再加 +2：8×2×6", "1r 1r 6b", "C41 C137:s=1 C137:s=6", s="96", chk={"additiveTotal100": 500})
    add("§5.3 例 第一张失效时该卡和复制均无该加成：8×2×2", "1r 1r 6b", "C41 C137:s=1 C137:s=6", mod="B25", s="32", chk={"triggeredSourceIds": ["ctl_03"], "additiveTotal100": 100}, tag="C137失效")
    add("C137 只读有效点数：不要奇数把 1 变 2，明星 2 命中一颗", "1r 3b", "C12 C137:s=2", s="12", chk={"additiveTotal100": 100})
    add("C137 明星点数没有出现：不触发", PAIR, "C137:s=6", s="34", chk={"triggeredSourceIds": []})
    add("§4.5 C155 4、4、4、4、1、1、6 为 ×4：24×8×3×4", "4r 4r 4r 4r 1r 1r 6b", "C22 C155", s="2304", chk={"controllerMultiplierNumerator": "4", "triggeredSourceIds": ["ctl_02"]})
    add("§4.5 C155 三颗 4 和两颗 1 无因子：14×6×2×1.5", "4r 4r 4r 1r 1r", "C155", s="252", chk={"triggeredSourceIds": []})
    add("§4.5 C155 四颗蓝 4 也 ×2，不要求红色：18×8×1.25×2", "4b 4b 4b 4b 2b", "C155", s="360")
    add("§4.5 C156 有效目标 321、累计 160 触发 +5", PAIR, "C156", before="160", target="321", s="204", chk={"additiveTotal100": 500})
    add("§4.5 C156 目标 321、累计 161 不触发", PAIR, "C156", before="161", target="321", s="34", chk={"triggeredSourceIds": []})
    add("§4.5 C156 目标 320、累计 160 不触发（严格小于）", PAIR, "C156", before="160", target="320", s="34", chk={"triggeredSourceIds": []})
    add("§11 例 厚墙目标 2430、累计 1214 触发绝地反击", PAIR, "C156", before="1214", target="2430", mod="B11", s="204")
    add("§11 例 厚墙目标 2430、累计 1215 不触发", PAIR, "C156", before="1215", target="2430", mod="B11", s="34", chk={"triggeredSourceIds": []})

    # ---- RULES_DELTA 5: growth values, catch-up, the two halvings ----
    add("§5 C51 起步 +1", PAIR, "C51", s="68", chk={"additiveTotal100": 100})
    add("§5 C51 卖过一次杂卡后 +1.8：34×2.8=95.2", PAIR, "C51:g=80", s="95", tag="成长读值C51")
    add("§5 C54 全局首次提交无上次：×1.25，值不变、不列计划", PAIR, "C54", s="42", plan={"growthChanges": []})
    chain = add("§5 C54 例 第 3 周期买入基线 ×1.45、已长 0.3：按 ×1.75 算，换牌型后计划减半", PAIR, "C54:g=30:k=20", prev="trips", s="59",
                chk={"controllerMultiplierNumerator": "7", "controllerMultiplierDenominator": "4"}, plan={"growthChanges": one("ctl_01", "C54", 0, True)},
                tag=("连庄两次减半", "周期追赶", "成长读值C54"), cid="CHAIN-C54-1")
    add("§5 C54 例 续：事件累计 0.15，下手 ×1.60，再换牌型：160×1.6", RED3, "C54:g=15:k=20", prev="pair", s="256",
        chk={"controllerMultiplierNumerator": "8", "controllerMultiplierDenominator": "5"}, plan={"growthChanges": one("ctl_01", "C54", 0, True)}, tag="连庄两次减半", cid="CHAIN-C54-2")
    add("§5 C54 例 续：0.15 减半取 0.07，×1.52：34×1.52=51.68", PAIR, "C54:g=7:k=20", prev="trips", s="51",
        chk={"controllerMultiplierNumerator": "38", "controllerMultiplierDenominator": "25"}, tag="连庄两次减半", cid="CHAIN-C54-3")
    add("§5 C54 与上一手相同：事件累计 +0.1", PAIR, "C54:g=30:k=20", prev="pair", s="59", plan={"growthChanges": one("ctl_01", "C54", 10)})
    add("§5 C54 事件累计为 0 时换牌型：仍列减半计划（减半后还是 0）", PAIR, "C54", prev="trips", s="42", plan={"growthChanges": one("ctl_01", "C54", 0, True)})
    add("§5 C54 上一手是散点、本手也是散点：相同，+0.1", SCAT, "C54", prev="scatter", s="18", plan={"growthChanges": one("ctl_01", "C54", 10)})
    add("§5 C55 首手 +12，计分后 +3", PAIR, "C55", s="58", chk={"controllerBaseAdds": [{"emitterId": "ctl_01", "abilityId": "C55", "amount": "12"}]}, plan={"growthChanges": one("ctl_01", "C55", 3)})
    add("§5 C55 下手 +15", PAIR, "C55:g=3", s="64", tag="成长读值C55")
    add("§5 C56 第 5 周期货架起步 +3，首次满足按 +3 算：19×2×4.25=161.5", BLUE, "C56:k=200", s="161", chk={"additiveTotal100": 325}, plan={"growthChanges": one("ctl_01", "C56", 40)}, tag="周期追赶")
    add("§5 C56 下手 +3.4：19×2×4.65=176.7", BLUE, "C56:g=40:k=200", s="176", tag="成长读值C56")
    add("§5 C56 有有效红面：按 +1 计分，不成长", PAIR, "C56", s="68", plan={"growthChanges": []})
    add("§5 C57 两颗实际成员加 C146 虚拟一颗，计数 3：计分后 +0.8", RED2, "C146 C57", s="152", chk={"redCount": 3}, plan={"growthChanges": one("ctl_02", "C57", 80)})
    add("§5 C57 下一手 +1.8：19×2×2×2.8=212.8", RED2, "C146 C57:g=80", s="212", tag="成长读值C57")
    add("§5 C57 红组只有 2：不成长", RED2, "C57", s="76", plan={"growthChanges": []})
    add("§5 C58 两关后 ×1.55：34×1.55=52.7", PAIR, "C58:g=30", s="52", tag="成长读值C58")
    add("§5 C59 剩 2 次时下关 +24", PAIR, "C59:g=12", s="82", chk={"baseTotal": "41"}, tag="成长读值C59")
    add("§5 C60 自选镀两面后从 +1 到 +1.5", PAIR, "C60:g=50", s="85", tag="成长读值C60")
    add("§5 C61 实升 1 级只 +0.5", PAIR, "C61:g=50", s="85", tag="成长读值C61")
    add("§5 C62 一手掷三次，本手 +15", PAIR, "C62:g=3", s="64", chk={"baseTotal": "32"}, tag="成长读值C62")
    add("§5 C129 一次首领后 +3.5：17×2×4.5", PAIR, "C129:g=250", s="153", tag="成长读值C129")
    add("§5 C132 赢高压后 +3.5", PAIR, "C132:g=250", s="153", tag="成长读值C132")
    add("§5 C134 钥匙串用两次合计成长 +0.8：34×2.8=95.2", PAIR, "C134:g=80", s="95", tag="成长读值C134")
    add("§5 追赶 基础分成长卡第 3 周期 +12：(17+12+12)×2", PAIR, "C55:k=12", s="82", tag="周期追赶")
    add("§5 追赶 乘法成长卡第 8 周期 +0.7：34×1.95=66.3", PAIR, "C58:k=70", s="66", tag="周期追赶")
    add("§5 追赶 加成成长卡第 8 周期 +3.5：17×2×5.5", PAIR, "C129:k=350", s="187", tag="周期追赶")
    add("§5 模仿读取目标的成长值与追赶，不另成长：34×1.75²=104.125", PAIR, "C41 C54:g=30:k=20", prev="pair", s="104", plan={"growthChanges": one("ctl_02", "C54", 10)})
    add("§5 同名两张连庄各自成长：×1.25×1.75", PAIR, "C54 C54:g=30:k=20", prev="pair", s="74",
        plan={"growthChanges": one("ctl_01", "C54", 10) + one("ctl_02", "C54", 10)})
    add("§5.1 / §10 返璞归真 +12 的积少成多已长 3 时本关加 +13", PAIR, "C55:g=3", mod="B35", s="60", chk={"controllerBaseAdds": [{"emitterId": "ctl_01", "abilityId": "C55", "amount": "13"}]},
        plan={"growthChanges": one("ctl_01", "C55", 3)}, tag="返璞归真")
    add("§5.1 返璞归真 深蓝已长 1.2 时本关按 +1.6 读：19×2×2.85=108.3", BLUE, "C56:g=120", mod="B35", s="108", chk={"additiveTotal100": 185}, tag="返璞归真")
    add("§10 返璞归真 3 级全红按 +0.5", ALLR, clv={"all_red": 3}, mod="B35", s="27", chk={"colorAdd100": 50})
    add("§5.1 返璞归真 追赶与起步保留，只减事件累计：×(1.25+0.2+0.15)", PAIR, "C58:g=30:k=20", mod="B35", s="54", tag="返璞归真")
    add("§5.1 返璞归真 连庄读 0.15 的一半 0.07：×1.52", PAIR, "C54:g=15:k=20", mod="B35", s="51")
    add("§5.1 返璞归真 不改昙花一现的倒计时", PAIR, "C141:g=1", mod="B35", s="136")
    add("§5.1 停滞 计分照常，成长计划照产生（由会话决定不执行）", PAIR, "C55:g=3", mod="B07", s="64", plan={"growthChanges": one("ctl_01", "C55", 3)}, tag="停滞")
    add("失效的连庄不参与计分，也不列减半计划", PAIR, "C54:g=30", prev="trips", mod="B25", s="34", plan={"growthChanges": []})

    # ---- RULES_DELTA 6.1: submission rewrites decided by the frozen snapshot ----
    add("§6.1 C07 本关首手恰两骰且有效点不同：计划深拷贝", "2b 5r", "C07", s="7", plan={"copyFirstToSecond": True, "cultivationSourceBindings": [bnd("C07", "ctl_01")]})
    add("§6.1 C07 第二手不触发", "2b 5r", "C07", ordinal=1, s="7", chk={"triggeredSourceIds": []}, plan={"copyFirstToSecond": False, "cultivationSourceBindings": []})
    add("§6.1 C15 红蓝红蓝红最多改 2 面", "1r 2b 4r 3b 1r", "C15", s="22", plan={"redSpreadIndices": [1, 3], "cultivationSourceBindings": [bnd("C15", "ctl_01")]})
    add("§6.1 C26 提交恰一颗：六面染红", "6b", "C26", s="7", plan={"dyeAllFacesRed": True, "cultivationSourceBindings": [bnd("C26", "ctl_01")]})
    add("§6.1 C36 例 容量 5 交四颗，两张 C36：前三颗净 +2，两个来源都列入绑定", "5b 2b 6b 3b", "C36 C36", s="20",
        plan={"growthNetDelta": 2, "growthIndices": [0, 1, 2], "cultivationSourceBindings": [bnd("C36", "ctl_01"), bnd("C36", "ctl_02")]})
    add("§6.1 C36 与 C37 净和为 0，仍给出目标与两个来源", "5b 2b 6b 3b", "C37 C36", s="20",
        plan={"growthNetDelta": 0, "growthIndices": [0, 1, 2], "cultivationSourceBindings": [bnd("C37", "ctl_01"), bnd("C36", "ctl_02")]})
    add("§6.1 C36 只交两颗：超出提交数的下标忽略", "5b 2b", "C36", s="8", plan={"growthNetDelta": 1, "growthIndices": [0, 1]})
    add("§6.1 C36 交满容量不触发", PAIR, "C36", s="34", chk={"triggeredSourceIds": []}, plan={"growthNetDelta": 0, "growthIndices": []})
    add("§6.1 C36 被模仿：绑定里发出者是模仿、所有者是真实卡", "5b 2b 6b 3b", "C41 C36", s="20",
        plan={"growthNetDelta": 2, "cultivationSourceBindings": [bnd("C36", "ctl_01", "ctl_02"), bnd("C36", "ctl_02")]})
    add("§6.1 C38 单颗原为红，改一面", "4r", "C38", s="6", plan={"blueSpreadIndices": [0], "cultivationSourceBindings": [bnd("C38", "ctl_01")]})
    add("§6.1 旧永久改写同场：绑定按 C07、C36/C37、C38 的执行顺序，同类只列首个来源", "2b 5r", "C38 C36 C07 C38", s="7",
        plan={"cultivationSourceBindings": [bnd("C07", "ctl_03"), bnd("C36", "ctl_02"), bnd("C38", "ctl_01")], "blueSpreadIndices": [0, 1], "growthIndices": [0, 1]})
    add("§6.1 C117 单颗非金只加一面金层", "6b", "C117", s="7", plan={"faceChanges": [fc("C117", "ctl_01", "D001", "F06", "gold", 1)]})
    add("§6.1 C117 首末两颗的朝上面镀金", PAIR, "C117", s="34", plan={"faceChanges": [fc("C117", "ctl_01", "D001", "F03", "gold", 1), fc("C117", "ctl_01", "D005", "F02", "gold", 1)]})
    add("§6.1 C118 朝上底红、有效蓝时对面仍染红", "4r", "C118", bwave=True, s="5", plan={"faceChanges": [fc("C118", "ctl_01", "D001", "F03", "color", 0, "red")]})
    add("§6.1 C119 第一颗朝上 6、可选三面，取最前两面改 6", [die(6, face=6, pips=[6, 6, 2, 3, 5, 6])], "C119", s="7",
        plan={"faceChanges": [fc("C119", "ctl_01", "D001", "F03", "pipSet", 6), fc("C119", "ctl_01", "D001", "F04", "pipSet", 6)]})
    add("§6.1 C119 首末两颗各取最前两个异点面", "3b 5b", "C119", s="10",
        plan={"faceChanges": [fc("C119", "ctl_01", "D001", "F01", "pipSet", 3), fc("C119", "ctl_01", "D001", "F02", "pipSet", 3),
                              fc("C119", "ctl_01", "D002", "F01", "pipSet", 5), fc("C119", "ctl_01", "D002", "F02", "pipSet", 5)]})
    add("§6.1 C122 原 1 直接到 5：pipSet=5", "1r", "C122", s="1", plan={"faceChanges": [fc("C122", "ctl_01", "D001", "F01", "pipSet", 5)]})
    add("§6.1 C123 单卡计划：1 点 +1，4、5、6 点 −2，2、3 无计划", "1r 4r 5b 6b 2b", "C123", s="18",
        plan={"faceChanges": [fc("C123", "ctl_01", "D001", "F01", "pipDelta", 1), fc("C123", "ctl_01", "D002", "F04", "pipDelta", -2),
                              fc("C123", "ctl_01", "D003", "F05", "pipDelta", -2), fc("C123", "ctl_01", "D004", "F06", "pipDelta", -2)]})
    add("§6.1 C124 每颗提交骰朝上面权重 +2", "6b", "C124", s="7", plan={"faceChanges": [fc("C124", "ctl_01", "D001", "F06", "weightDelta", 2)]})
    add("§6.1 C126 留池原值 4 的朝上面 +2", "6b", "C126", p="4r", s="7", plan={"faceChanges": [fc("C126", "ctl_01", "D002", "F04", "pipDelta", 2)]})
    add("§6.1 C127 把首颗的点数、底色、纹理、黄金复制到末颗：13×1.5=19.5", "6b:gold:smooth 3b 4r", "C127", s="19",
        plan={"faceChanges": [fc("C127", "ctl_01", "D003", "F04", "pipSet", 6), fc("C127", "ctl_01", "D003", "F04", "color", 0, "blue"),
                              fc("C127", "ctl_01", "D003", "F04", "texture", 0, "smooth"), fc("C127", "ctl_01", "D003", "F04", "gold", 1)]})
    add("§6.1 C127 每手至少两颗即触发，不限首手", "1r 6b", "C127", ordinal=1, s="7", chk={"triggeredSourceIds": ["ctl_01"]})
    add("§6.1 模仿的改写条目：发出者是模仿，状态所有者是被模仿的真实卡", "6b", "C41 C124", s="7",
        plan={"faceChanges": [fc("C124", "ctl_01", "D001", "F06", "weightDelta", 2, owner="ctl_02"), fc("C124", "ctl_02", "D001", "F06", "weightDelta", 2)]})

    # ---- RULES_DELTA 10: disabled lists ----
    add("§10 缴械 A、B、C 入关，A、B 失效", PAIR, "C46 C50 C64", mod="B25", s="40", chk={"triggeredSourceIds": ["ctl_03"]})
    add("§10 凡人 罕见 A、稀有 B、稀有 C，只有 B 失效：17×2×3×4", PAIR, "C46 C130 C42", mod="B29", s="408", chk={"triggeredSourceIds": ["ctl_01", "ctl_03"]})
    add("§10 万籁俱寂 A、B 失效；第三张可发技能，其倍率附魔不给 +1.5：47×2×3", PAIR, "C64 C65 C46:mult2 C50:base10", mod="B37", s="282",
        chk={"triggeredSourceIds": ["ctl_03", "ctl_04"], "enchantAdditiveCount": 0, "enchantBaseAdd": 0})
    add("失效的模仿目标：模仿者没有技能", PAIR, "C41 C46 C64", mod="B25", s="40", chk={"triggeredSourceIds": ["ctl_03"]})
    add("最右侧的百变模仿没有目标，不触发", PAIR, "C46 C41", s="102", chk={"triggeredSourceIds": ["ctl_01"]})

    # ---- added after the first comparison with the game's scorer; appended so that earlier case ids stay put ----
    add("§5.1 例 返璞归真 连庄已长 0.15 时本关按 ×1.32 读（0.15 减半取 0.07）：34×1.32=44.88", PAIR, "C54:g=15", mod="B35", s="44",
        chk={"controllerMultiplierNumerator": "33", "controllerMultiplierDenominator": "25", "triggeredMultiplierIds": ["C54"]}, tag="返璞归真")
    add("§7.3 提交的玻璃骰朝上点数不超过 3 也照列风险名单：贡献 3、9、5", "1r:glass 3b:glass 5b", s="17",
        chk={"pipContributions": [3, 9, 5]}, plan={"glassRiskDieIds": ["D001", "D002"]}, tag="玻璃")
    add("§7.3 封小封印了 2 点玻璃骰：贡献 0，仍按提交顺序列入风险名单：(0+15+5)×2×1.25", "2b:glass 5b:glass 5b", mod="B15", s="50",
        chk={"sealedDice": [True, False, False], "pipContributions": [0, 15, 5]}, plan={"glassRiskDieIds": ["D001", "D002"]}, tag=("玻璃", "封印"))
    add("百变模仿发出的乘法因子：triggeredMultiplierIds 列被模仿卡的技能编号，不去重", PAIR, "C41 C42 C101", s="1632",
        chk={"triggeredMultiplierIds": ["C42", "C42", "C101"], "controllerMultiplierNumerator": "48"})

    if mismatches:
        raise ValueError("hand-computed expectations disagree with the reference scorer:\n  " + "\n  ".join(mismatches))
    require(chain["expected"]["afterScorePlan"]["growthChanges"][0]["halve"], "C54 chain")

    # ---- the per-card matrix ----
    require(set(MISS) == CONDITIONAL, f"miss recipe inventory: {sorted(set(MISS) ^ CONDITIONAL)}")
    require(set(HIT) <= SCORING, "hit recipes")

    def token(card, copy):
        if card in GROWTH:
            p = definitions[card]["parameters"]
            return f"{card}:g={(2 + copy) * p['growthEvent']}:k={(1 - copy % 2) * cfg['growthCatchupPerCycle'][p['growthKind']]}"
        if card == "C141": return f"{card}:g={1 + copy % 2}"
        if card == "C45": return f"{card}:blue"
        return card

    def matrix(card, family, copies, recipe, mimic=False, disabled=False, what=""):
        r = dict(recipe)
        star = r.pop("star", 3)
        tokens = [token(card, k) + (f":s={star}" if card == "C137" else "") for k in range(copies)]
        if mimic:
            tokens.insert(0, "C41")
        tokens += r.pop("sup", "").split()
        kw = {k: r.pop(k) for k in list(r) if k not in ("d", "p")}
        if disabled:
            kw.update(mod="B25", kind="boss")
        real = sum(1 for k, t in enumerate(tokens) if t.split(":")[0] == "C53" and not (disabled and k < 2))
        return add(f"{card} {what}", r.get("d", PAIR), " ".join(tokens), r.get("p", ""), rnd=(3, 6, 8)[:real], cid=f"M-{card}-{family}",
                   meta={"cardId": card, "family": family}, **kw)

    for card in ordered(SCORING - {"C41"}):
        hit = HIT.get(card, {})
        matrix(card, "hit", 1, hit, what="单张：触发")
        if card in CONDITIONAL:
            matrix(card, "miss", 1, MISS[card], what="单张：条件不满足，不触发")
        matrix(card, "disabled", 1, hit, disabled=True, what="单张被缴械失效：不触发，不产生技能、附魔或成长计划")
        matrix(card, "two_hit", 2, hit, what="同名两张：两个真实来源各自触发")
        matrix(card, "mimic_hit", 1, hit, mimic=True, what="百变模仿在左复制它：模仿与真实卡各发一份")
    # Three copies of one additive card: 18 x (1 + 3 x 2.5) = 153.
    three = matrix("C91", "three_hit", 3, HIT["C91"], what="同名三张：三个真实来源各自触发，加成相加")
    require(three["expected"]["score"] == "153" and three["expected"]["additiveTotal100"] == 750
            and three["expected"]["triggeredSourceIds"] == ["ctl_01", "ctl_02", "ctl_03"], "M-C91-three_hit hand calculation")
    return cases, infos, tags


# ---- assembly, coverage, artifacts ---------------------------------------------------------------------------------

def build_all(cfg):
    old_cases, old_infos, excluded, seen = historical_cases(cfg)
    new_cases, new_infos, tags = build_new_cases(cfg)
    cases, infos = new_cases + old_cases, new_infos + old_infos
    require(len({c["id"] for c in cases}) == len(cases), "duplicate case ids")
    return cases, infos, excluded, seen, tags


def coverage(cfg, cases, infos, tags):
    """Per-card trigger coverage and the checks the hand-off asks for. Returns (table, problems)."""
    table = {card: Counter() for card in ordered(SCORING)}
    for case, info in zip(cases, infos):
        inp = case["input"]
        kind = {c["instanceId"]: c["definitionId"] for c in inp["controllers"]}
        disabled, fired = set(inp["disabledControllerIds"]), set(info["triggered"])
        flags = {}
        for emitter, ability in info["sources"]:
            for card in {ability, kind[emitter]} - {None}:
                if card not in table or (card == "C41" and kind[emitter] != "C41"):
                    continue
                f = flags.setdefault(card, Counter())
                if card == "C41" or ability == card:
                    f["hit" if emitter in fired else "miss"] += 1
                    if emitter in fired and kind[emitter] == card: f["real_hit"] += 1
                    if emitter in fired and kind[emitter] == "C41" and card != "C41": f["mimic_hit"] += 1
        for instance in disabled:
            flags.setdefault(kind[instance], Counter())["disabled"] += 1
        for card, f in flags.items():
            if card not in table:
                continue
            row = table[card]
            row["cases"] += 1
            for key in ("hit", "miss", "disabled"):
                row[key] += 1 if f[key] else 0
            row["duplicate"] += 1 if f["real_hit"] >= 2 else 0
            row["mimic"] += 1 if f["mimic_hit"] else 0
            if case["source"] in ("new", "matrix"):
                row["new_cases"] += 1
    problems = []
    category = {card: name for name, members in CATEGORY.items() for card in members}
    for card, row in table.items():
        if not row["hit"]: problems.append(f"{card}: no triggering case")
        if card in CONDITIONAL and not row["miss"]: problems.append(f"{card}: no natural non-triggering case")
        if not (row["miss"] or row["disabled"]): problems.append(f"{card}: no non-triggering case")
        if not row["disabled"]: problems.append(f"{card}: no disabled case")
        if card in category:
            if not row["duplicate"]: problems.append(f"{card} ({category[card]}): no same-name stacking case")
            if not row["mimic"]: problems.append(f"{card} ({category[card]}): no mimic case")
    problems += [f"section 14 item without a case: {t}" for t in SPEC14_TAGS if not tags.get(t)]
    problems += [f"modifier effect {code}: no case where it changes the result" for code, hits in effect_coverage(cfg, cases).items() if not hits]
    names = ("cases", "new_cases", "hit", "miss", "disabled", "duplicate", "mimic")
    return {card: {k: row[k] for k in names} for card, row in table.items()}, problems


def effect_coverage(cfg, cases):
    """For each scoring effect code: in how many cases removing the code changes the result."""
    out = Counter()
    for case in cases:
        modifier = modifier_of(cfg, case["input"]["modifierId"])
        for code in (modifier["effects"] if modifier else []):
            without, _ = score(case["input"], cfg, drop_effect=code)
            changed = without != case["expected"]
            require(code in SCORING_EFFECTS or not changed, f"{case['id']}: session-only effect {code} changed the score result")
            if code in SCORING_EFFECTS:
                out[code] += 1 if changed else 0
    return {code: out[code] for code in SCORING_EFFECTS}


# Numbers quoted in the rules text (RULES_DELTA 2-7, 12.3). The configuration must agree before any vector is produced.
SPEC_TOP = {"configVersion": CONFIG_VERSION, "saveSchemaVersion": 8, "maxHandLevel": 12, "maxColorLevel": 12, "goldAdd100": 50,
            "glassContributionMultiplier": 3, "glassBreakPercent": 15, "maxFaceWeight": 5, "bonusPerUnusedRoll": 3}
SPEC_PARAMS = {
    "C02": {"scoreMultiplier100": 300}, "C09": {"scoreMultiplier100": 250, "capacityDelta": -1}, "C31": {"scoreMultiplier100": 300, "minSamePip": 4},
    "C33": {"scoreMultiplier100": 300, "firstMinusSecondAtLeast": 3, "minDice": 2}, "C42": {"scoreMultiplier100": 400, "destroyChancePercentAfterValidSubmission": 15},
    "C53": {"factorUnits2": [1, 2, 2, 3, 4, 4, 6, 6, 8]}, "C71": {"pip": 6, "stepNumerator": 3, "stepDenominator": 2},
    "C87": {"minSamePip": 4, "stepNumerator": 3, "stepDenominator": 2}, "C89": {"stepNumerator": 13, "stepDenominator": 10},
    "C100": {"growthKind": "multiplier", "growthStart": 125, "growthEvent": 15}, "C101": {"scoreMultiplier100": 300, "submissionsDelta": -1},
    "C103": {"maxOwnedPermanentDice": 4, "strictMaxOwnedPermanentDice": 3, "scoreMultiplier100": 200, "strictScoreMultiplier100": 300},
    "C130": {"scoreMultiplier100": 300}, "C138": {"scoreMultiplier100": 250, "goldCost": 2},
    "C06": {"additiveBonus100": 300, "minDistinct": 3}, "C08": {"additiveBonus100": 200}, "C11": {"additiveBonus100": 800}, "C14": {"additiveBonus100": 400},
    "C18": {"additiveBonus100": 500}, "C28": {"additiveBonus100": 400, "minDistinctPairs": 2}, "C30": {"additiveBonus100": 250, "minSamePip": 3},
    "C32": {"additiveBonus100": 300}, "C40": {"additiveBonus100": 450}, "C46": {"additiveBonus100": 200}, "C47": {"additivePerController100": 60},
    "C49": {"additivePerFive100": 150, "noFiveMultiplier100": 50}, "C63": {"additivePerPermanentDie100": 50}, "C74": {"additiveBonus100": 400},
    "C75": {"additiveBonus100": 450, "maxRollsUsed": 1}, "C77": {"additiveBonus100": 250}, "C81": {"additiveBonus100": 800}, "C82": {"additiveBonus100": 450},
    "C83": {"additiveBonus100": 600}, "C86": {"additiveBonus100": 800}, "C88": {"additiveBonus100": 600, "minDice": 4}, "C102": {"additiveBonus100": 450},
    "C104": {"additiveBonus100": 450, "minDice": 3}, "C105": {"additiveBonus100": 300}, "C106": {"additiveBonus100": 250, "minDice": 3},
    "C139": {"goldPerStep": 10, "additivePerGoldStep100": 50}, "C141": {"initialAdditive100": 400, "decayPerClear100": 100, "expireGrowth": 4},
    "C144": {"additiveBonus100": 400}, "C147": {"additivePerPoolDie100": 60}, "C148": {"additivePerUnusedRoll100": 100},
    "C149": {"additiveBonus100": 800, "requiredDice": 1}, "C150": {"additivePerPreviousSubmit100": 200}, "C151": {"additivePerEmptyOrdinarySlot100": 125},
    "C152": {"additivePerCommon100": 60, "rarity": "common"}, "C153": {"additiveBonus100": 450, "submissionOrdinal": 0},
    "C10": {"pip": 1, "perDieAdd": 15}, "C13": {"oddPerDieAdd": 8}, "C17": {"pips": [1, 2, 3], "perDieAdd": 9}, "C20": {"lastActualCapacitySlotPip": 6, "perDieAdd": 24},
    "C27": {"baseAddPerDistinctPairedPip": 20}, "C39": {"effectiveColor": "blue", "perDieAdd": 9}, "C44": {"baseAddPerFrozenGold": 2},
    "C48": {"baseAddPerEmptySubmitSlot": 15}, "C50": {"baseAdd": 30}, "C70": {"baseAddPerPoolDie": 9}, "C72": {"poolPipSumMultiplier": 3},
    "C76": {"baseAddPerExtraRoll": 15}, "C80": {"pairSum": 7, "baseAddPerPair": 30}, "C92": {"perDieAdd": 15}, "C93": {"dieContributionMultiplier": 3},
    "C107": {"perDieAdd": 20}, "C108": {"baseAddPerPip": 6, "index": 0}, "C135": {"baseAddPerItem": 15}, "C29": {"minimumStraightLength": 4},
    "C43": {"levelAddPerSource": 1}, "C84": {"handMultAdd100": 200, "minSamePip": 2}, "C85": {"handMultAdd100": 400, "minSamePip": 3},
    "C143": {"retriggers": 2, "requiredDice": 2}, "C19": {"minOneAdd100": 250, "minTwoAdd100": 150}, "C91": {"minDice": 3, "additiveBonus100": 250},
    "C125": {"growthKind": "multiplier", "growthStart": 125, "growthEvent": 20}, "C137": {"additivePerMatch100": 100},
    "C155": {"minFours": 4, "minOnes": 2, "scoreMultiplier100": 200, "enhancedScoreMultiplier100": 400}, "C156": {"targetDivisor": 2, "additiveBonus100": 500},
    "C51": {"growthKind": "additive", "growthStart": 100, "growthEvent": 80}, "C54": {"growthKind": "multiplier", "growthStart": 125, "growthEvent": 10},
    "C55": {"growthKind": "base", "growthStart": 12, "growthEvent": 3}, "C56": {"growthKind": "additive", "growthStart": 100, "growthEvent": 40},
    "C57": {"growthKind": "additive", "growthStart": 100, "growthEvent": 80, "minRedGroup": 3}, "C58": {"growthKind": "multiplier", "growthStart": 125, "growthEvent": 15},
    "C59": {"growthKind": "base", "growthStart": 12, "growthEvent": 6}, "C60": {"growthKind": "additive", "growthStart": 100, "growthEvent": 50},
    "C61": {"growthKind": "additive", "growthStart": 100, "growthEvent": 50}, "C62": {"growthKind": "base", "growthStart": 12, "growthEvent": 1},
    "C129": {"growthKind": "additive", "growthStart": 100, "growthEvent": 250}, "C132": {"growthKind": "additive", "growthStart": 100, "growthEvent": 250},
    "C134": {"growthKind": "additive", "growthStart": 100, "growthEvent": 40}, "C36": {"indices": [0, 1, 2], "delta": 1}, "C37": {"indices": [0, 1, 2], "delta": -1},
    "C119": {"facesPerDie": 2}, "C122": {"pip": 5}, "C123": {"pips": [2, 3], "lowPipDelta": 1, "highPipDelta": -2, "highPipFloor": 3},
    "C124": {"delta": 2}, "C126": {"delta": 2}, "C127": {"minDice": 2, "sourceIndex": 0},
}
CULTIVATION = ids("C07 C15 C26 C36 C37 C38 C115 C117 C118 C119 C120 C121 C122 C123 C124 C126 C127")


def check_config(cfg):
    """The configuration contract this reference relies on (RULES_DELTA 12.3). Stops before any vector is built."""
    problems = [f"{key}: config {cfg.get(key)!r}, rules text {value!r}" for key, value in SPEC_TOP.items() if cfg.get(key) != value]
    require(cfg.get("configVersion") == CONFIG_VERSION, f"P0Config.json is {cfg.get('configVersion')!r}, not {CONFIG_VERSION}; stop and report")
    definitions = {c["id"]: c for c in cfg["controllers"]}
    for card, params in SPEC_PARAMS.items():
        for key, value in params.items():
            actual = definitions.get(card, {}).get("parameters", {}).get(key)
            if actual != value:
                problems.append(f"{card}.{key}: config {actual!r}, rules text {value!r}")
    colors = {c["id"]: (c.get("baseAdd100"), c.get("addPerLevel100")) for c in cfg["colorDefinitions"]}
    if colors != {"all_blue": (25, 50), "all_red": (50, 50)}: problems.append(f"colour adds {colors}")
    if [r["factor100"] for r in cfg["redRewards"]] != [100, 100, 100, 200, 300, 450, 600, 800, 1100]: problems.append("red group table")
    rules = cfg["enchantRules"]
    if (rules.get("baseAdd"), rules.get("scoreAdd100")) != (15, 150): problems.append("enchantment numbers")
    if cfg["growthCatchupPerCycle"] != {"additive": 50, "base": 6, "multiplier": 10}: problems.append("growthCatchupPerCycle")
    if set(definitions) & REMOVED: problems.append("removed cards are still defined")
    if set(definitions) - set(rules["nonSubmitControllerIds"]) != SCORING: problems.append("scoring card inventory")
    if {c for c, d in definitions.items() if "growthKind" in d["parameters"]} != GROWTH: problems.append("growth card inventory")
    if {c for c, d in definitions.items() if d["isCultivation"]} != CULTIVATION: problems.append("cultivation card inventory")
    rarities = Counter(d["rarity"] for d in definitions.values())
    if (rarities["common"], rarities["uncommon"], rarities["rare"]) != (29, 88, 33): problems.append(f"rarity counts {dict(rarities)}")
    by_id = {m["id"]: m for m in cfg["bossModifiers"]}
    if (by_id["B25"]["effects"], by_id["B25"]["amount"]) != (["DisableLeftmost"], 2): problems.append("B25")
    if (by_id["B29"]["effects"], by_id["B29"]["amount"], by_id["B29"]["filter"]) != (["DisableRarity"], 1, "rare"): problems.append("B29")
    if (sorted(by_id["B37"]["effects"]), by_id["B37"]["amount"]) != (["DisableLeftmost", "EnchantSilence"], 2): problems.append("B37")
    if sorted(by_id["B35"]["effects"]) != ["GrowthHalf", "ZeroLevels"]: problems.append("B35")
    require(not problems, "configuration disagrees with the rules text:\n  " + "\n  ".join(problems))


def check_spec_examples(cfg):
    """Worked numbers of the rules text that are pure arithmetic on the formula (no card state needed)."""
    examples = [
        ("§2 1200", final_score(20, 300, 200, 700, Fraction(5, 2), Fraction(1, 2))[0], 1200),
        ("§2 ×2.75 减半", halve_hand_mult(275), 137),
        ("§2 274", final_score(200, halve_hand_mult(275), 100, 0)[0], 274),
        ("§12.2 69", final_score(13, 112, 100, 25 + 50 + 300)[0], 69),
        ("§14 3060", final_score(17, 800, 300, 150, Fraction(2) * Fraction(3, 2))[0], 3060),
        ("§14 1530", final_score(17, 800, 300, 150, Fraction(2) * Fraction(3, 2), Fraction(1, 2))[0], 1530),
        ("§9.1 周期", [cycle_of_shop(j) for j in (2, 3, 23)], [1, 2, 8]),
        ("§5 第 3 周期乘法追赶", catchup_for(cfg, "C54", 3), 20),
        ("§5 第 5 周期加成追赶", catchup_for(cfg, "C56", 5), 200),
    ]
    bad = [f"{name}: {actual!r} != {wanted!r}" for name, actual, wanted in examples if actual != wanted]
    require(not bad, "rules text examples do not follow from the formula:\n  " + "\n  ".join(bad))
    return len(examples)


def render(header, cases):
    dump = lambda o: json.dumps(o, ensure_ascii=False, separators=(",", ":"))  # noqa: E731
    lines = ["{" + ",\n".join(f"{dump(k)}:{dump(v)}" for k, v in header.items()) + ',\n"cases":[']
    for i, c in enumerate(cases):
        meta = {k: v for k, v in c.items() if k not in ("input", "expected")}
        lines.append("{%s,\n \"input\":%s,\n \"expected\":%s}%s" % (dump(meta)[1:-1], dump(c["input"]), dump(c["expected"]), "," if i + 1 < len(cases) else ""))
    lines.append("]}")
    return "\n".join(lines) + "\n"


def artifacts(cfg, built):
    cases, infos, excluded, seen, tags = built
    table, problems = coverage(cfg, cases, infos, tags)
    require(not problems, "coverage gaps:\n  " + "\n  ".join(problems))
    by_source = dict(Counter(c["source"] for c in cases))
    header = {"schema": SCHEMA, "configVersion": cfg["configVersion"], "caseCount": len(cases), "casesBySource": by_source,
              "fieldNotes": FIELD_NOTES, "openReadings": OPEN_READINGS}
    text = render(header, cases)
    rewritten = Counter()
    for c in cases:
        for r in c.get("derivedFrom", {}).get("rewrites", []):
            rewritten[r.split("：")[0].split("（")[0][:28]] += 1
    inputs = [Path(__file__).resolve(), CONFIG, ROOT / "tools" / "v111_config.py"] + RULES + [p for _, p in HISTORICAL]
    provenance = {
        "generator": rel(__file__), "schema": SCHEMA, "configVersion": cfg["configVersion"],
        "derived_from_production_code": False,
        "independence": "期望值只来自规格文本与配置数据；没有读取或运行 Core/、UI/、Runtime/、Tests/、tools/balance_sim/*.cs，也没有读取 W3A_NOTES、W3B1_NOTES、TEST_CHANGES。旧样例文件只取输入。",
        "input_sha256": {rel(p): digest(p) for p in inputs},
        "golden": rel(GOLDEN), "golden_sha256": hashlib.sha256(text.encode("utf-8")).hexdigest(),
        "cases": len(cases), "cases_by_source": by_source,
        "historical_inputs": {name: {"inputs": total, "kept": by_source.get(Path(name).name, 0),
                                     "excluded": sum(1 for e in excluded if e["file"] == name)} for name, total in seen.items()},
        "new_cases": {"hand_written": by_source.get("new", 0), "matrix": by_source.get("matrix", 0)},
        "hand_checked_cases": sum(1 for c in cases if c["handChecked"]),
        "rewritten_historical_inputs": sum(1 for c in cases if c.get("derivedFrom", {}).get("rewrites")),
        "rewrite_kinds": dict(rewritten.most_common()),
        "excluded_historical_inputs": excluded,
        "coverage_by_card": table,
        "changed_cards": ordered(CHANGED), "conditional_cards": ordered(CONDITIONAL),
        "card_categories": {name: ordered(members) for name, members in CATEGORY.items()},
        "section14_tags": {t: tags[t] for t in SPEC14_TAGS},
        "modifier_effect_coverage": effect_coverage(cfg, cases),
        "open_readings": OPEN_READINGS,
        "open_readings_doc": "docs/specs/v111/W4_OPEN_READINGS.md",
        "production_comparison": "Not part of this script. The C# side compares its own results with GoldenScoresV111.json.",
    }
    return text, json.dumps(provenance, ensure_ascii=False, indent=2) + "\n"


def verify():
    cfg = load(CONFIG)
    check_config(cfg)
    examples = check_spec_examples(cfg)
    built = build_all(cfg)
    cases, infos, excluded, seen, tags = built
    _, problems = coverage(cfg, cases, infos, tags)
    require(not problems, "coverage gaps:\n  " + "\n  ".join(problems))
    by_source = Counter(c["source"] for c in cases)
    print(f"config {cfg['configVersion']}: contract ok, {examples} formula examples ok")
    print(f"cases {len(cases)}: " + ", ".join(f"{k} {v}" for k, v in by_source.items()) + f"; excluded historical inputs {len(excluded)}")
    print(f"hand-checked cases {sum(1 for c in cases if c['handChecked'])}; coverage ok for {len(SCORING)} scoring cards and {len(SPEC14_TAGS)} section-14 items")
    return cfg, built


def main():
    mode = sys.argv[1] if len(sys.argv) > 1 else "verify"
    require(mode in ("verify", "write", "check"), "usage: v111_golden.py verify|write|check")
    cfg, built = verify()
    if mode == "verify":
        return
    golden, provenance = artifacts(cfg, built)
    if mode == "write":
        EVIDENCE.parent.mkdir(parents=True, exist_ok=True)
        GOLDEN.write_text(golden, encoding="utf-8", newline="\n")
        EVIDENCE.write_text(provenance, encoding="utf-8", newline="\n")
        print(f"wrote {len(built[0])} cases to {rel(GOLDEN)} and {rel(EVIDENCE)}")
    else:
        for path, wanted in ((GOLDEN, golden), (EVIDENCE, provenance)):
            require(path.exists() and path.read_bytes().decode("utf-8") == wanted, f"{rel(path)} differs from the generator")
        print("golden and provenance match the generator exactly")


if __name__ == "__main__":
    main()
