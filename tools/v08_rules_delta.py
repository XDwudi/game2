"""Print the p0.6.0 -> p0.8.0 configuration differences as Markdown (used to build docs/specs/v08/RULES_DELTA.md).

Reads only the frozen p0.6.0 baseline and the current game config; it never edits either.
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OLD = json.loads((ROOT / "tools/balance_sim/baseline/P0Config.p0.6.0.json").read_text(encoding="utf-8"))
NEW = json.loads((ROOT / "DicePrototype/Assets/Game/Data/P0Config.json").read_text(encoding="utf-8"))
RARITY = {"common": "普通", "uncommon": "罕见", "rare": "稀有"}


def flat(value, prefix=""):
    if isinstance(value, dict):
        for k, v in value.items():
            yield from flat(v, prefix + ("." if prefix else "") + k)
    else:
        yield prefix, value


def show(v):
    return json.dumps(v, ensure_ascii=False) if not isinstance(v, str) else v


print("### 顶层与经济\n\n| 字段 | p0.6.0 | p0.8.0 |\n| --- | --- | --- |")
for key in NEW:
    if isinstance(NEW[key], (dict, list)):
        continue
    if key not in OLD:
        print(f"| `{key}`（新增） | – | {show(NEW[key])} |")
    elif OLD[key] != NEW[key]:
        print(f"| `{key}` | {show(OLD[key])} | {show(NEW[key])} |")
for section in ("shop", "enchantRules"):
    a, b = dict(flat(OLD[section])), dict(flat(NEW[section]))
    for k in b:
        if a.get(k) != b[k]:
            print(f"| `{section}.{k}` | {show(a.get(k, '–'))} | {show(b[k])} |")

print("\n### 附魔\n\n| 附魔 | 出现权重 | 加价 p0.6.0 | 加价 p0.8.0 |\n| --- | --- | --- | --- |")
for a, b in zip(OLD["enchantments"], NEW["enchantments"]):
    print(f"| {b['displayName']} | {b['weight']}（不变） | {a['buyPremium']} | {b['buyPremium']} |")

print("\n### 牌型与骰面改造\n\n| 项 | 字段 | p0.6.0 | p0.8.0 |\n| --- | --- | --- | --- |")
for name, key in (("handDefinitions", "id"), ("colorDefinitions", "id"), ("diceUpgrades", "id"), ("consumables", "id"), ("services", "id")):
    for a, b in zip(OLD[name], NEW[name]):
        for k in b:
            if k != "description" and a.get(k) != b[k]:
                print(f"| {b.get('displayName', b[key])} | `{k}` | {show(a.get(k))} | {show(b[k])} |")

print("\n### 控制器\n\n| 编号 | 名称 | 稀有度 | 价格 | 参数变化 |\n| --- | --- | --- | --- | --- |")
unchanged = []
for a, b in zip(OLD["controllers"], NEW["controllers"]):
    pa, pb = dict(flat(a["parameters"])), dict(flat(b["parameters"]))
    params = [f"`{k}` {show(pa.get(k, '无'))} → {show(pb[k])}" for k in pb if pa.get(k) != pb[k]]
    rarity = RARITY[b["rarity"]] if a["rarity"] == b["rarity"] else f"{RARITY[a['rarity']]} → {RARITY[b['rarity']]}"
    price = str(b["buyPrice"]) if a["buyPrice"] == b["buyPrice"] else f"{a['buyPrice']} → {b['buyPrice']}"
    if not params and a["rarity"] == b["rarity"] and a["buyPrice"] == b["buyPrice"]:
        unchanged.append(b["id"])
        continue
    print(f"| {b['id']} | {b['displayName']} | {rarity} | {price} | {'；'.join(params) or '–'} |")
print(f"\n数值未变的 {len(unchanged)} 张：{'、'.join(unchanged)}。出现权重随稀有度由 6 / 3 / 1 变为 6 / 4 / 2，所有卡都受影响。")

print("\n### 关卡\n\n| 关 | 目标 p0.6.0 | 目标 p0.8.0 | 高压目标 p0.6.0 | 高压目标 p0.8.0 | 奖励 |\n| --- | --- | --- | --- | --- | --- |")
for a, b in zip(OLD["stages"], NEW["stages"]):
    risk_a = f"{int(a['riskTarget']):,}" if a["riskEnabled"] else "–"
    risk_b = f"{int(b['riskTarget']):,}" if b["riskEnabled"] else "–"
    reward = f"{b['normalReward']}" + (f"（高压 {b['riskReward']}）" if b["riskEnabled"] else "")
    print(f"| {int(b['stageId'][-2:])} | {int(a['normalTarget']):,} | {int(b['normalTarget']):,} | {risk_a} | {risk_b} | {reward} |")

changed_text = [b["id"] for a, b in zip(OLD["controllers"], NEW["controllers"]) if a["description"] != b["description"]]
changed_text += [b["id"] for a, b in zip(OLD["diceUpgrades"], NEW["diceUpgrades"]) if a["description"] != b["description"]]
print(f"\n说明文字随数值更新的条目：{'、'.join(changed_text)}。")
