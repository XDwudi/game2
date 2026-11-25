"""Independent p1.1.0 duplicate-card vectors, derived from the rules, never C#.

Reuse the unchanged v10 reference arithmetic, but construct new legal submissions
from explicit rule-text recipes. Historical expected values only establish the
coverage inventory and regression oracle; they never supply new expectations.
Run with verify | write | check. Only write changes the v11 artifacts.
"""
import copy
import hashlib
import json
import sys
from collections import Counter
from pathlib import Path

sys.dont_write_bytecode = True
try:
    from tools import v10_golden as ref
except ModuleNotFoundError:
    import v10_golden as ref

ROOT = Path(__file__).resolve().parents[1]
# p1.1.1 (W4b): this generator is frozen. It reads the frozen config of its own version and checks the frozen copy of
# its vectors; the live game config and GoldenScoresV111.json belong to tools/v111_config.py and tools/v111_golden.py.
FROZEN = ROOT / "tools/balance_sim/baseline"
CONFIG = FROZEN / "P0Config.p1.1.0.json"
BASELINE = FROZEN / "P0Config.p1.0.0.json"
GOLDEN = FROZEN / "GoldenScoresV11.p1.1.0.json"
EVIDENCE = ROOT / "evidence/v11/rules/golden-provenance.json"
SCHEMA = "v11-golden-1"
CONFIG_VERSION = "p1.1.0"
RULES = [ROOT / p for p in (
    "docs/specs/v11/RULES_DELTA.md", "docs/specs/v10/RULES_SPEC.md",
    "docs/specs/v09/RULES_SPEC.md", "docs/specs/v08/RULES_DELTA.md", "docs/specs/P0_v0.6.md")]
HISTORICAL = [FROZEN / p for p in ("GoldenScores.p0.8.0.json", "GoldenScoresV09.p0.9.0.json", "GoldenScoresV10.p1.0.0.json")]


def ids(text):
    return text.split()


def ordered(values):
    return sorted(values, key=lambda c: int(c[1:]))


# The exclusions and trigger classes are from the card tables, not the live
# configuration's nonSubmitControllerIds or the implementation's branches.
NON_SCORING = set(ids("C05 C21 C22 C34 C35 C52 C78 C79 C95 C96 C97 C98 C99 C110 C111 C112 C113 C114 C115 C116 C120 C121 C128 C131 C133 C136 C140 C145"))
SCORING = set(f"C{i:02d}" for i in range(1, 154)) - NON_SCORING - {"C04", "C16", "C23"}
ALWAYS = set(ids("C01 C03 C09 C38 C42 C45 C46 C47 C49 C50 C51 C53 C54 C55 C56 C57 C58 C59 C60 C61 C62 C63 C64 C90 C94 C100 C101 C108 C117 C118 C119 C122 C123 C124 C125 C129 C132 C134 C141"))
CONDITIONAL = SCORING - ALWAYS
PURE_MULTIPLIERS = set(ids("C02 C06 C08 C09 C11 C14 C18 C28 C30 C31 C32 C33 C40 C42 C46 C47 C49 C51 C53 C54 C56 C57 C58 C60 C61 C63 C71 C74 C75 C77 C81 C82 C83 C86 C87 C88 C89 C90 C100 C101 C102 C103 C104 C105 C106 C129 C130 C132 C134 C138 C139 C141 C144 C147 C148 C149 C150 C151 C152 C153"))
PER_DIE_ADDITIONS = set(ids("C10 C13 C17 C19 C20 C39 C91 C92 C94 C107 C137"))
TRIPLES = PURE_MULTIPLIERS | PER_DIE_ADDITIONS | set(ids("C45 C64 C65 C66 C67 C68 C69 C84 C85 C93 C109 C142 C143"))
GROWTH = set(ids("C54 C55 C56 C57 C58 C59 C60 C61 C62 C90 C100 C129 C132 C134"))
GROWTH_STEP = {"C54": 3, "C55": 2, "C56": 2, "C57": 3, "C58": 4, "C59": 4,
               "C60": 2, "C61": 2, "C62": 1, "C90": 15, "C100": 2, "C129": 8, "C132": 8, "C134": 2}
SWITCHES = set(ids("C03 C12 C24 C25 C29 C73 C146"))

READINGS = {
    "D01": "范围由旧三份黄金文件实际触发来源的实体定义及模仿目标并集核对，共122张；包括只产生提交后计划的卡与C41。28张非Submit卡不在F-DUP-03范围。",
    "D02": "v11 §1.1.7：每个真实副本是来源；整手倍率逐来源相乘，加值与再计次数相加；最后只向下取整一次。三张覆盖全部整手乘法卡、逐骰加法卡，另覆盖逐骰乘法与再计。",
    "D03": "P0 v0.6触发表及v09/v10触发表：总是触发不等于有收益。C45无匹配颜色、C49无5点、零成长与已满足目标的养成卡仍触发，不能伪造未触发样例。",
    "D04": "C03/C12/C24/C25/C29/C73/C146只改变一次有效值/分组/阈值；每个来源仍独立触发自身附魔。C12仍只奇数+1，C73留池红骰只并一次，C146只+1。",
    "D05": "v0.6模仿条款、v09 §2.2：C41链追到真实目标，复用目标颜色、成长、周转与随机结果；自身附魔独立。真实重复卡各有状态，成长计划只列真实实例一次。",
    "D06": "v10 §6.9 C138：按来源从左到右预留冻结钱包，每次3金币；2/3个来源全触发分别需6/9。当前提交收入不纳入预算，C44/C139仍读冻结钱包。",
    "D07": "v10 §5.3、6.9：骰面计划基于同一冻结六面数据，按每来源列出。C117/118/119/125/127最终赋值可幂等，但列表不能去重；C119不会让后一个副本另找新面。§4逐卡表不是期望值来源。",
    "D08": "v08 §1.2、v10 §6.9：C47按所有真实装备件数计算每份倍率；C151按占用普通栏位数；C152按常见实体数。副本和C41各占一件，模仿不额外创造实体。",
    "D09": "C09容量减值与C101提交预算按来源累加后夹下限；不是沿用v10仅唯一实体的生成器校验。扩容后的容量5至8与骰子购买/移除状态可达。",
    "D10": "C100在关卡开始依次摧毁右邻；测试提交快照里的两张祭坛可由C100/祭品/C100排列产生，三张可由C100/祭品/C100/祭品/C100产生；存活相邻并不表示开关时原来就相邻。",
    "D11": "旧数值经v08与v09/v10覆盖：如C46×2、C42×5、C13+4、C19+8、C20+15、C44每金币+1。p1.1.0仅允许文字/重复持有字段变化；规范化配置逐字段等于冻结p1.0.0。",
    "D12": "条件卡的two_miss使用自然条件失败，不用禁用卡替代。成长触发与计分触发分开：C54/56/57没有成长时仍是计分来源；养成目标无变化时仍可总是触发。",
    "D13": "afterScorePlan额外保留旧参考输出，验证提交收入、染色、净点数变更、牌型升级、真实状态所有者与玻璃大炮风险来源；不把计划验证声称为会话执行或随机概率验证。",
}


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def relative(path):
    return path.relative_to(ROOT).as_posix()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def normalized_config(cfg):
    cfg = copy.deepcopy(cfg)
    cfg.pop("configVersion")
    # The save structure version (6 -> 7 in p1.1.0) describes saves, not scoring.
    cfg.pop("saveSchemaVersion")
    # Explicitly allow only the documented v11 presentation fields/removals.
    for key in ("controllers", "consumables", "services", "diceUpgrades", "bossModifiers", "lootKinds"):
        for entry in cfg[key]:
            entry.pop("shortText", None)
            entry.pop("details", None)
            if key == "controllers":
                entry.pop("maxOwnedNormal", None)
            if key == "lootKinds":
                entry["displayName"] = entry["displayName"].replace("控制器", "卡牌")
            if key == "services" and entry["id"] == "O07" and entry["priceRule"] == "6，整个Run仅可购买1次":
                entry["priceRule"] = "6（整局限买1次）"
    for key in ("controllersUniqueOwned", "allowDifferentEnchantReplacement", "replacementCredit",
                "replacementCountsAsOneControllerSale", "firstControllerGuaranteeUnownedOnly"):
        cfg["shop"].pop(key, None)
    return cfg


def historical_inventory():
    inventory, per_file = set(), {}
    for path in HISTORICAL:
        content = read(path)
        cases = content if isinstance(content, list) else content["cases"]
        seen = set()
        for case in cases:
            cards = case["input"]["controllers"]
            by_id = {c["instanceId"]: c for c in cards}
            order = [c["instanceId"] for c in cards]
            bindings = case["input"].get("bindings")
            for emitter in case["expected"]["triggeredSourceIds"]:
                card = by_id[emitter]
                seen.add(card["definitionId"])
                visited = set()
                while card["definitionId"] == "C41":
                    owner = card["instanceId"]
                    require(owner not in visited, "historical mimic cycle")
                    visited.add(owner)
                    at = order.index(owner)
                    target = bindings.get(owner) if bindings is not None else (order[at + 1] if at + 1 < len(order) else None)
                    card = by_id[target]
                seen.add(card["definitionId"])
        inventory |= seen
        per_file[relative(path)] = ordered(seen)
    require(inventory == SCORING, f"spec/historical inventory mismatch: {inventory ^ SCORING}")
    return per_file


def verify():
    baseline, live = read(BASELINE), read(CONFIG)
    require(baseline["configVersion"] == "p1.0.0" and live["configVersion"] == CONFIG_VERSION, "config versions")
    require(normalized_config(baseline) == normalized_config(live), "undocumented configuration change")
    results = []
    frozen = read(HISTORICAL[-1])
    require(frozen["schema"] == "v10-golden-1" and frozen["configVersion"] == "p1.0.0", "v10 must remain frozen")
    for path, cfg in ((BASELINE, baseline), (CONFIG, live)):
        bad = []
        for case in frozen["cases"]:
            actual = ref.expected_of(ref.score(ref.adapt(case["input"], cfg), cfg))
            if actual != case["expected"]:
                bad.append({"id": case["id"], "expected": case["expected"], "actual": actual})
        result = {"vectors": relative(HISTORICAL[-1]), "config": relative(path), "cases": len(frozen["cases"]),
                  "exact": len(frozen["cases"]) - len(bad), "all_fields_exact": not bad}
        results.append(result)
        print(f"v10 regression {cfg['configVersion']}: {result['exact']}/{result['cases']} exact")
        require(not bad, f"v10 differences: {bad}")
    for cfg_path in (BASELINE, CONFIG):
        results.extend((ref.verify_legacy(cfg_path, "v11"), ref.verify_v09(cfg_path, "v11")))
    require(all(r["all_fields_exact"] for r in results), "older golden mismatch")
    historical_inventory()
    return results


# Recipes are deliberately explicit examples of the card text. Defaults form a
# mixed-colour, non-straight, single-pair hand (17 pips, score 34 without cards).
PAIR = "3b 3b 5b 4r 2b"
HIT = {
    "C02": {"dice": "3b 3b 5b 6b 2b"}, "C06": {},
    "C07": {"dice": "2b 5r"}, "C08": {"dice": "2b 5r"}, "C09": {"dice": "2b"},
    "C10": {"dice": "1r 2b"}, "C11": {"dice": "2b 2r 3b 3r"}, "C12": {}, "C13": {},
    "C14": {"dice": "1r 3b 5b"}, "C15": {"dice": "2r 3b 4r"}, "C17": {},
    "C18": {"dice": "2r 4b"}, "C19": {"dice": "2b:small 5r"},
    "C20": {"dice": "2r 3b 4r 5b 6b"}, "C24": {"dice": "1r 4r 2b"},
    "C25": {"dice": "1r 4r 2b"}, "C26": {"dice": "2b"}, "C27": {},
    "C28": {"dice": "2b 2r 3b 3r"}, "C29": {"dice": "1r 2b 3b 4r"},
    "C30": {"dice": "3b 3b 3r"}, "C31": {"dice": "4b 4r 4b 4b"},
    "C32": {"dice": "5b 5r"}, "C33": {"dice": "6b 3r"},
    "C36": {"dice": "2b 4r"}, "C37": {"dice": "2b 4r"}, "C39": {},
    "C40": {"dice": "1r 3b 5b"}, "C41": {"support": "C46"},
    "C43": {"previousPrimary": "pair"}, "C44": {"wallet": 7},
    "C47": {"support": "C50"}, "C48": {"dice": "2b 4r"},
    "C49": {"dice": "5b 5r 2b"}, "C54": {"previousPrimary": "pair"},
    "C56": {"dice": "3b 3b 5b 6b 2b"}, "C57": {"dice": "4r 4r 4r 2b"},
    "C65": {}, "C66": {"dice": "1r 2b 3b"}, "C67": {"dice": "6r 2b"},
    "C68": {"last": True}, "C69": {"dice": "4r 4r 2b"},
    "C70": {"dice": "2b 4r", "pool": "1b 6r"},
    "C71": {"dice": "2b 4r", "pool": "6b 6r"},
    "C72": {"dice": "2b 4r", "pool": "1b 6r"},
    "C73": {"dice": "4r 4r 2b", "pool": "1r 6r"},
    "C74": {"dice": "2b 4r", "pool": "1b 6r"},
    "C75": {"rollsUsed": 1, "rollsRemaining": 2}, "C76": {}, "C77": {},
    "C80": {"dice": "1r 6b 2b 5r"}, "C81": {"dice": "1r 3b 5b"},
    "C82": {"dice": "3b 3r 3b 5b 5r"}, "C83": {"dice": "1r 2b 3b 4r 5b"},
    "C84": {}, "C85": {"dice": "3b 3r 3b"},
    "C86": {"dice": "1r 1b 2b 2r 3b 3r", "capacity": 6},
    "C87": {"dice": "4b 4r 4b 4b"}, "C88": {"dice": "1r 2b 4r 6b"},
    "C89": {"dice": "2b:gold 4r"}, "C91": {"dice": "2b:smooth 4r:rough 5b"},
    "C92": {"dice": "2b:mono 4r"}, "C93": {"dice": "2b:same 4r"},
    "C102": {"last": True}, "C103": {"dice": "2b 4r 5b"},
    "C104": {"dice": "2r 3b 4r"}, "C105": {"dice": "2r 3r 4b"},
    "C106": {"dice": "2r 3b 2b"}, "C107": {"dice": "2r 3b 4r"},
    "C109": {"dice": "2r 3b 4r"}, "C126": {"dice": "2r 3b", "pool": "1b 6r"},
    "C127": {"dice": "2r:gold:rough 5b"}, "C130": {"stageKind": "boss"},
    "C135": {"inventoryCount": 2}, "C137": {"dice": "2r:temp 3b"},
    "C138": {"wallet": 9}, "C139": {"wallet": 10},
    "C142": {"dice": "1r 2b 3b 4r 5b"}, "C143": {},
    "C144": {"dice": "2r 4b 6b"}, "C146": {"dice": "4r 4r 2b"},
    "C147": {"dice": "2r 3b", "pool": "1b 6r"},
    "C148": {"rollsRemaining": 2, "rollsUsed": 1},
    "C149": {"dice": "2b"}, "C150": {"submissionOrdinal": 1},
    "C151": {}, "C152": {"support": "C50"}, "C153": {},
}
MISS = {
    "C02": {}, "C06": {"dice": "2r 2b"}, "C07": {"dice": "2r 2b"}, "C08": {},
    "C10": {}, "C11": {}, "C12": {"dice": "2r 4b 6b"}, "C13": {"dice": "2r 4b"},
    "C14": {}, "C15": {"dice": "2r 3b 4b 5r"}, "C17": {"dice": "4r 5b 6b"},
    "C18": {}, "C19": {}, "C20": {"dice": "2r 3b 6b"}, "C24": {},
    "C25": {"dice": "1b 4b 2b"}, "C26": {}, "C27": {"dice": "1r 3b 5b"},
    "C28": {}, "C29": {"dice": "1r 2b 3b 5r"}, "C30": {}, "C31": {}, "C32": {},
    "C33": {"dice": "5b 4r"}, "C36": {}, "C37": {}, "C39": {"dice": "2r 4r"},
    "C40": {"dice": "5r 3b 1b"}, "C41": {"support": "C02"},
    "C43": {}, "C44": {}, "C48": {}, "C65": {"dice": "2b 4r"},
    "C66": {"dice": "4r 5b"}, "C67": {}, "C68": {}, "C69": {"dice": "2b 4b"},
    "C70": {}, "C71": {"pool": "2b"}, "C72": {},
    "C73": {"pool": "2b"}, "C74": {}, "C75": {},
    "C76": {"rollsUsed": 1, "rollsRemaining": 2},
    "C77": {"rollsUsed": 2, "rollsRemaining": 1}, "C80": {"dice": "1r 2b 3b"},
    "C81": {}, "C82": {}, "C83": {}, "C84": {"dice": "1r 3b 5b"},
    "C85": {}, "C86": {}, "C87": {}, "C88": {}, "C89": {},
    "C91": {}, "C92": {}, "C93": {}, "C102": {}, "C103": {},
    "C104": {}, "C105": {}, "C106": {"dice": "2r 3b 4b"},
    "C107": {"dice": "5r 3b 1b"}, "C109": {"dice": "2r 3b"},
    "C126": {}, "C127": {"submissionOrdinal": 1}, "C130": {}, "C135": {}, "C137": {},
    "C138": {"wallet": 2}, "C139": {"wallet": 9}, "C142": {},
    "C143": {"dice": "3r 3b 3b"}, "C144": {}, "C146": {"dice": "2b 4b"},
    "C147": {}, "C148": {}, "C149": {}, "C150": {},
    "C151": {"support": "C46 C46 C46"}, "C152": {}, "C153": {"submissionOrdinal": 1},
}


def make_input(cfg, card_id, copies, recipe, mimic=False):
    r = dict(recipe)
    tokens = [card_id] * copies
    if mimic:
        tokens.insert(copies - 1, "C41")
    tokens += r.pop("support", "").split()
    cards = []
    for i, cid in enumerate(tokens):
        cards.append({"instanceId": f"ctl_{i + 1:02d}", "definitionId": cid, "enchantmentId": "none",
                      "growth": 4 * GROWTH_STEP[cid] if cid in GROWTH else 0, "turnoverUnits2": 3,
                      "activeColor": "blue" if cid == "C45" else ""})
    dice = []
    names = {"r": "red", "b": "blue"}
    for i, token in enumerate(r.pop("dice", PAIR).split()):
        head, *flags = token.split(":")
        pip, color = int(head[0]), names[head[1]]
        faces = [{"faceId": f["faceId"], "pip": f["pipValue"], "color": f["colorId"],
                  "texture": f["texture"], "gold": f["isGold"]} for f in cfg["diceDefinition"]["faces"]]
        for f in faces:
            if "small" in flags: f["pip"] = 2
            if "same" in flags: f["pip"] = pip
            if "mono" in flags: f["color"] = color
        face = faces[pip - 1]
        face["pip"], face["color"] = pip, color
        face["gold"] = "gold" in flags
        face["texture"] = "rough" if "rough" in flags else "smooth" if "smooth" in flags else "none"
        dice.append({"dieId": f"D{i + 1:03d}", "faceId": face["faceId"], "pip": pip, "glass": "glass" in flags,
                     "lifetime": "stage_temporary" if "temp" in flags else "permanent", "faces": faces})
    pool = [{"dieId": f"D{len(dice) + i + 1:03d}", "faceId": f"F0{t[0]}", "pip": int(t[0]), "color": names[t[1]]}
            for i, t in enumerate(r.pop("pool", "").split())]
    # Resolve the ability chain purely for the frozen session budgets.
    abilities = []
    for i in range(len(cards)):
        at = i
        while at < len(cards) and cards[at]["definitionId"] == "C41": at += 1
        abilities.append(cards[at]["definitionId"] if at < len(cards) else None)
    chances = max(1, cfg["submissionsPerStage"] - abilities.count("C101"))
    ordinal = chances - 1 if r.pop("last", False) else r.pop("submissionOrdinal", 0)
    capacity = max(1, cfg["initialSubmitCapacity"] - abilities.count("C09"))
    previous = r.pop("previousPrimary", "scatter" if ordinal else "")
    result = {"dice": dice, "pool": pool, "controllers": cards,
              "capacity": capacity, "rollsRemaining": 0, "rollsUsed": 3,
              "submissionsRemainingBefore": chances - ordinal, "submissionOrdinal": ordinal,
              "permanentDiceCount": len(dice) + len(pool) - sum(d["lifetime"] == "stage_temporary" for d in dice),
              "wallet": 0, "redWave": False, "blueWave": False, "previousPrimary": previous,
              "handLevels": [], "colorLevels": [],
              "randomChoices": [{"ownerInstanceId": c["instanceId"], "units2": 3} for c in cards if c["definitionId"] == "C53"],
              "stageKind": "normal", "modifierId": "", "disabledControllerIds": [],
              "stageHandsUsed": [previous] if ordinal and previous != "scatter" else [],
              "inventoryCount": 0, "ordinaryControllerSlots": 5, "scoreItemMultiplier": 0}
    result.update(r)
    return result


def validate(inp, cfg):
    """Validate this generator's bounded, non-disabled, non-remote scenarios.

    Unlike v10.validate, duplicate definitions are legal, while instance IDs must
    remain unique. Session budgets count each resolved source, including mimics.
    """
    cards, dice, pool = inp["controllers"], inp["dice"], inp["pool"]
    require(1 <= len(dice) <= inp["capacity"] <= 8, "submission capacity")
    require(len(cards) <= 10 and len({c["instanceId"] for c in cards}) == len(cards), "controller IDs/count")
    require(5 <= inp["ordinaryControllerSlots"] <= 7 and sum(c["enchantmentId"] != "slotless" for c in cards) <= inp["ordinaryControllerSlots"], "ordinary slots")
    definitions = {c["id"]: c for c in cfg["controllers"]}
    for c in cards:
        cid = c["definitionId"]
        require(cid in definitions and c["growth"] >= 0 and c["turnoverUnits2"] >= 3, "controller state")
        require(c["enchantmentId"] in ("none", "base10", "mult2", "slotless"), "supported enchantment")
        require(cid not in NON_SCORING or c["enchantmentId"] in ("none", "slotless"), "non-submit enchantment")
        require(c["activeColor"] in (("red", "blue") if cid == "C45" else ("",)), "active color")
        require(cid != "C141" or c["growth"] < 3, "expired C141")
    all_dice = dice + pool
    require(len(all_dice) <= 10 and len({d["dieId"] for d in all_dice}) == len(all_dice), "die IDs/count")
    temporary = sum(d["lifetime"] == "stage_temporary" for d in dice)
    require(0 <= temporary <= 3 and 0 <= inp["permanentDiceCount"] <= 8 and inp["permanentDiceCount"] + temporary == len(all_dice), "complete pool / permanent dice")
    for d in dice:
        require(d["lifetime"] in ("permanent", "stage_temporary"), "lifetime")
        require([f["faceId"] for f in d["faces"]] == [f"F0{i}" for i in range(1, 7)], "six faces")
        require(d["pip"] == next(f["pip"] for f in d["faces"] if f["faceId"] == d["faceId"]), "face pip consistency")
        require(all(1 <= f["pip"] <= 6 and f["color"] in ("red", "blue") and f["texture"] in ("none", "smooth", "rough") for f in d["faces"]), "face values")
    require(all(1 <= d["pip"] <= 6 and d["color"] in ("red", "blue") for d in pool), "pool values")
    abilities = []
    for i in range(len(cards)):
        while i < len(cards) and cards[i]["definitionId"] == "C41": i += 1
        abilities.append(cards[i]["definitionId"] if i < len(cards) else None)
    chances = max(1, cfg["submissionsPerStage"] - abilities.count("C101"))
    require(1 <= inp["submissionsRemainingBefore"] <= chances and inp["submissionOrdinal"] == chances - inp["submissionsRemainingBefore"], "submission budget")
    if "C09" in abilities:
        require(inp["capacity"] == max(1, 5 - abilities.count("C09")), "C09 capacity budget")
    require(inp["rollsUsed"] >= 1 and inp["rollsRemaining"] >= 0 and inp["rollsUsed"] + inp["rollsRemaining"] == 3, "roll budget")
    require(inp["wallet"] >= 0 and 0 <= inp["inventoryCount"] <= 5, "wallet/inventory")
    require(not inp["disabledControllerIds"] and not inp["modifierId"], "natural trigger scenarios only")
    require(inp["stageKind"] in ("normal", "pressure", "boss"), "stage kind")
    owners = [c["instanceId"] for c in cards if c["definitionId"] == "C53"]
    require([r["ownerInstanceId"] for r in inp["randomChoices"]] == owners, "one random result per real C53")
    require(all(r["units2"] in definitions["C53"]["parameters"]["factorUnits2"] for r in inp["randomChoices"]), "random result table")


def build_cases(cfg):
    require(set(MISS) == CONDITIONAL, f"miss recipe inventory: {set(MISS) ^ CONDITIONAL}")
    require(CONDITIONAL <= set(HIT), f"missing hit recipes: {CONDITIONAL - set(HIT)}")
    cases = []

    def add(card, family, inp, description, checks=None, plan_checks=None):
        validate(inp, cfg)
        result = ref.score(ref.adapt(inp, cfg), cfg)
        case = {"id": f"{card}-{family}", "cardId": card, "family": family, "description": description,
                "input": inp, "expected": ref.expected_of(result), "afterScorePlan": result["afterScorePlan"]}
        for section, pins in (("expected", checks), ("afterScorePlan", plan_checks)):
            for field, value in (pins or {}).items():
                require(case[section][field] == value, f"{case['id']} hand calculation {section}.{field}: {value!r} != {case[section][field]!r}")
        if checks or plan_checks:
            case["handChecks"] = {"expected": checks or {}, "afterScorePlan": plan_checks or {}}
        cases.append(case)
        return case

    for card in ordered(SCORING):
        add(card, "two_hit", make_input(cfg, card, 2, HIT.get(card, {})), f"{card} 双持：两个真实来源同时触发")
        if card in CONDITIONAL:
            add(card, "two_miss", make_input(cfg, card, 2, MISS[card]), f"{card} 双持：自然条件不满足，两个来源均不触发")
        if card in TRIPLES:
            add(card, "three_hit", make_input(cfg, card, 3, HIT.get(card, {})), f"{card} 三持：逐来源相乘或加值/次数相加")
        add(card, "mimic_hit", make_input(cfg, card, 2, HIT.get(card, {}), mimic=True), f"{card} 两张真实卡与中间C41，模仿右侧副本；独立来源")

    def extra(card, name, recipe=None, copies=2, mimic=False, edit=None, checks=None, plan=None):
        inp = make_input(cfg, card, copies, HIT.get(card, {}) if recipe is None else recipe, mimic)
        if edit: edit(inp)
        return add(card, name, inp, f"{card}：{name}", checks, plan)

    extra("C46", "hand_multiplier", {"dice": "2r 3b"}, checks={"score": "20", "controllerMultiplierNumerator": "4"})
    extra("C46", "hand_triple", {"dice": "2r 3b"}, copies=3, checks={"score": "40", "controllerMultiplierNumerator": "8"})
    extra("C10", "hand_addition", {"dice": "1r 2b"}, checks={"score": "23", "pipContributions": [21, 2]})
    extra("C10", "hand_triple", {"dice": "1r 2b"}, copies=3, checks={"score": "33", "pipContributions": [31, 2]})
    extra("C64", "hand_retrigger", {"dice": "2r 3b"}, copies=3, checks={"score": "11", "pipRetriggers": [3, 0], "pipSum": "11"})
    extra("C50", "hand_base", {"dice": "2r 3b"}, checks={"score": "45", "baseTotal": "45"})
    extra("C84", "hand_hand_add", {"dice": "2r 2b"}, checks={"score": "20", "totalHandMult100": 500, "handMultAdd100": 300})
    extra("C47", "hand_count", {"dice": "2r 3b", "support": "C50"}, checks={"score": "56", "controllerMultiplierNumerator": "9", "controllerMultiplierDenominator": "4"})
    extra("C45", "separate_colors", {"dice": "2r 3b"}, edit=lambda x: x["controllers"][0].update(activeColor="red"),
          checks={"pipContributions": [6, 9], "score": "15"}, plan={"nextColorOwnerIds": ["ctl_01", "ctl_02"]})
    extra("C45", "no_benefiting_die", {"dice": "2r 3r"}, checks={"pipContributions": [2, 3], "triggeredSourceIds": ["ctl_01", "ctl_02"]})
    extra("C49", "zero_factor_still_triggers", {"dice": "2r 3b"}, checks={"score": "0", "triggeredSourceIds": ["ctl_01", "ctl_02"]})
    extra("C51", "separate_turnover", {"dice": "2r 3b"}, edit=lambda x: x["controllers"][1].update(turnoverUnits2=5),
          checks={"score": "18", "controllerMultiplierNumerator": "15", "controllerMultiplierDenominator": "4"})
    def separate_random(x):
        x["randomChoices"][0]["units2"] = 1
        x["randomChoices"][1]["units2"] = 3
    extra("C53", "separate_random_and_mimic", {"dice": "2r 3b"}, mimic=True, edit=separate_random,
          checks={"score": "5", "controllerMultiplierNumerator": "9", "controllerMultiplierDenominator": "8"},
          plan={"randomFactorOwnerIds": ["ctl_01", "ctl_03"]})
    for card in ordered(GROWTH | {"C141"}):
        def state(x, card=card):
            x["controllers"][0]["growth"] = 1 if card == "C141" else 3 * GROWTH_STEP[card]
            x["controllers"][-1]["growth"] = 2 if card == "C141" else 4 * GROWTH_STEP[card]
        extra(card, "separate_state_and_mimic", {"dice": "2r 3b"}, mimic=True, edit=state)
    for card in ("C54", "C56", "C57"):
        extra(card, "growth_condition_miss", {"dice": "2r 3b", "previousPrimary": "pair"})
    for budget in (0, 2, 3, 5, 6, 8, 9):
        count = min(3, budget // 3)
        extra("C138", f"budget_{budget}", {"dice": "2r 3b", "wallet": budget}, mimic=True,
              checks={"goldSpent": 3 * count, "controllerMultiplierNumerator": str(3 ** count),
                      "triggeredSourceIds": [f"ctl_{i + 1:02d}" for i in range(count)]})
    for card in ordered(SWITCHES):
        def enchants(x):
            x["controllers"][0]["enchantmentId"] = "base10"
            x["controllers"][1]["enchantmentId"] = "mult2"
        extra(card, "switch_independent_enchants", edit=enchants)
    extra("C01", "income_plan", {"dice": "2r 3b"}, plan={"earnedGold": 2})
    extra("C36", "net_growth_plan", {"dice": "2r 3b", "support": "C37"}, plan={"growthNetDelta": 1, "growthIndices": [0, 1]})
    extra("C43", "upgrade_cap", {"dice": "2r 2b", "previousPrimary": "pair", "handLevels": [{"handId": "pair", "level": 7}]},
          plan={"upgradeRequested": 2, "upgradeApplied": 1})
    return cases


def coverage(cases):
    require(len({c["id"] for c in cases}) == len(cases), "duplicate case IDs")
    table = {}
    for card in ordered(SCORING):
        row = {}
        for family, copies, hit in (("two_hit", 2, True), ("two_miss", 2, False), ("three_hit", 3, True), ("mimic_hit", 2, True)):
            required = family != "two_miss" or card in CONDITIONAL
            required = required and (family != "three_hit" or card in TRIPLES)
            found = [c for c in cases if c["cardId"] == card and c["family"] == family]
            if required: require(len(found) == 1, f"{card}: missing {family}")
            for case in found:
                real = [c["instanceId"] for c in case["input"]["controllers"] if c["definitionId"] == card]
                expected_count = copies + (1 if card == "C41" and family == "mimic_hit" else 0)
                require(len(real) == expected_count, f"{card}: copy count")
                triggers = set(case["expected"]["triggeredSourceIds"])
                require(all((i in triggers) == hit for i in real), f"{card}: {family} does not exercise the claimed trigger")
                if family == "mimic_hit":
                    require(all(c["instanceId"] in triggers for c in case["input"]["controllers"] if c["definitionId"] == "C41"), f"{card}: mimic not triggered")
            row[family] = [c["id"] for c in found]
        table[card] = row
    return table


def artifacts(checks):
    cfg = read(CONFIG)
    cases = build_cases(cfg)
    table = coverage(cases)
    content = {"schema": SCHEMA, "configVersion": CONFIG_VERSION,
               "scoringCards": ordered(SCORING), "conditionalCards": ordered(CONDITIONAL),
               "pureMultiplierCards": ordered(PURE_MULTIPLIERS), "perDieAdditionCards": ordered(PER_DIE_ADDITIONS), "cases": cases}
    text = json.dumps(content, ensure_ascii=False, indent=2) + "\n"
    inputs = [Path(__file__).resolve(), Path(ref.__file__).resolve(), CONFIG, BASELINE] + RULES + HISTORICAL + [ref.LEGACY_VECTORS, ref.V09_VECTORS]
    provenance = {"generator": relative(Path(__file__).resolve()), "schema": SCHEMA, "configVersion": CONFIG_VERSION,
                  "derived_from_production_code": False, "reference_scorer": "tools/v10_golden.py",
                  "input_sha256": {relative(p): digest(p) for p in inputs}, "golden": relative(GOLDEN),
                  "golden_sha256": hashlib.sha256(text.encode("utf-8")).hexdigest(), "cases": len(cases),
                  "scoring_cards": len(SCORING), "conditional_cards": len(CONDITIONAL),
                  "cases_per_family": dict(Counter(c["family"] for c in cases)),
                  "hand_checked_cases": [c["id"] for c in cases if "handChecks" in c],
                  "legacy_checks": checks, "historical_inventory": historical_inventory(),
                  "duplicate_coverage": table, "spec_readings": READINGS,
                  "changed_existing_expectations": [], "production_comparison": "See nunit-managed-report.json and W06_REPORT.md; generation never reads production results."}
    return text, json.dumps(provenance, ensure_ascii=False, indent=2) + "\n"


def main():
    mode = sys.argv[1] if len(sys.argv) > 1 else "verify"
    require(mode in ("verify", "write", "check"), "usage: v11_golden.py verify|write|check")
    checks = verify()
    if mode == "verify": return
    golden, provenance = artifacts(checks)
    if mode == "write":
        EVIDENCE.parent.mkdir(parents=True, exist_ok=True)
        GOLDEN.write_text(golden, encoding="utf-8", newline="\n")
        EVIDENCE.write_text(provenance, encoding="utf-8", newline="\n")
        print(f"wrote {len(json.loads(golden)['cases'])} cases / {len(SCORING)} cards")
    else:
        for path, expected in ((GOLDEN, golden), (EVIDENCE, provenance)):
            require(path.exists() and path.read_text(encoding="utf-8") == expected, f"{relative(path)} differs from generator")
        print("golden and provenance match generator exactly")


if __name__ == "__main__":
    main()
