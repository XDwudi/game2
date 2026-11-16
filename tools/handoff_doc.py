"""Generate docs/GAME_CONTENT_REFERENCE.md: every piece of game content and every tunable number, straight from
DicePrototype/Assets/Game/Data/P0Config.json. The narrative overview (docs/GAME_OVERVIEW.md) is written by hand and
links here, so numbers are never retyped.

    python tools/handoff_doc.py            # write the reference
    python tools/handoff_doc.py --check    # fail if the file on disk differs from what would be written
"""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CONFIG = ROOT / "DicePrototype" / "Assets" / "Game" / "Data" / "P0Config.json"
OUT = ROOT / "docs" / "GAME_CONTENT_REFERENCE.md"

RARITY = {"common": "普通", "uncommon": "罕见", "rare": "稀有"}
KIND = {"normal": "普通", "pressure": "高压", "boss": "首领"}
WINDOW = {"Selecting": "选择阶段（已投掷）", "HandReady": "本手首掷之前", "HandReadyOrSelecting": "首掷前或选择阶段"}
TIER = {1: "一档（第 1 周期起）", 2: "二档（第 2 周期起）", 3: "三档（第 4 周期起）", 4: "终局（第 24 关）"}


def cell(text) -> str:
    return str(text if text is not None else "").replace("|", "／").replace("\n", " ")


def table(header, rows) -> list:
    lines = ["| " + " | ".join(header) + " |", "|" + " --- |" * len(header)]
    lines += ["| " + " | ".join(cell(c) for c in row) + " |" for row in rows]
    return lines + [""]


def mult(value100) -> str:
    return "×" + (f"{value100 / 100:.2f}".rstrip("0").rstrip("."))


def build() -> str:
    c = json.loads(CONFIG.read_text(encoding="utf-8"))
    out = [
        "# 骰子构筑 · 内容与数值手册（自动生成）",
        "",
        f"由 `tools/handoff_doc.py` 从 `DicePrototype/Assets/Game/Data/P0Config.json` 生成，规则版本 **{c['configVersion']}** / 存档结构 {c['saveSchemaVersion']}。",
        "不要手改本文件：改数值请改配置生成器（`tools/v10_config.py`），然后重新生成。系统说明与开发约定见 [GAME_OVERVIEW](GAME_OVERVIEW.md)。",
        "",
        f"内容总数 {c['activeContentCount']}：控制器 {len(c['controllers'])}、道具 {len(c['consumables'])}、商店服务 {len(c['services'])}、骰面改造 {len(c['diceUpgrades'])}；"
        f"另有首领减益 {len(c['bossModifiers'])}、战利品 {len(c['lootKinds'])} 种、牌型 {len(c['handDefinitions'])}、颜色牌型 {len(c['colorDefinitions'])}、附魔 {len(c['enchantments']) - 1}。",
        "",
        "## 1. 基础数值",
        "",
    ]
    g = c["generation"]
    s = c["shop"]
    out += table(["项", "数值"], [
        ["开局骰子 / 永久骰上限 / 临时骰上限 / 活动骰上限", f"{c['initialDiceCount']} / {c['maxPermanentDice']} / {c['maxTemporaryDice']} / {c['maxActiveDice']}"],
        ["提交栏容量（开局 / 上限）", f"{c['initialSubmitCapacity']} / {c['maxSubmitCapacity']}"],
        ["每手投掷次数 / 每关提交次数", f"{c['totalRollsPerHand']} / {c['submissionsPerStage']}"],
        ["每剩 1 次未用投掷的基础分奖励", c["bonusPerUnusedRoll"]],
        ["开局金币", c["initialGold"]],
        ["过关金币（普通 / 高压挑战）", f"{c['baseStageReward']} / {c['riskStageReward']}"],
        ["每剩 1 次未用提交的金币", c["goldPerUnusedSubmission"]],
        ["利息", f"每 {c['interestDivisor']} 金币 +1，计息本金上限 {c['maxInterestPrincipal']}（理财顾问每次 +{c['interestPrincipalStep']}，最多 {c['maxInterestPrincipalPurchases']} 次）"],
        ["普通控制器栏位（开局 / 上限）/ 控制器总件数上限", f"{c['initialControllerSlots']} / {c['maxOrdinaryControllerSlots']} / {c['maxControllerInstances']}"],
        ["道具槽（开局 / 上限）", f"{c['initialConsumableSlots']} / {c['maxConsumableSlots']}"],
        ["控制器回收价", f"标价（买入价 + 附魔加价）÷ {c['controllerSellDivisor']}，向下取整，最低 {c['baseControllerSellPrice']}"],
        ["道具回收价", c["consumableSellPrice"]],
        ["牌型 / 颜色等级上限", f"{c['maxHandLevel']} / {c['maxColorLevel']}"],
        ["玻璃骰：贡献倍率 / 每次投掷碎裂概率", f"×{c['glassContributionMultiplier']} / {c['glassBreakPercent']}%"],
        ["黄金面贡献倍率", f"×{c['goldContributionMultiplier']}"],
        ["骰面权重上限", c["maxFaceWeight"]],
        ["货架：控制器（开局 / 上限）、道具（开局 / 上限）", f"{s['initialControllerOffers']} / {s['maxControllerOffers']}、{s['initialConsumableOffers']} / {s['maxConsumableOffers']}"],
        ["货架：牌型升级 / 骰面改造 / 服务 / 新骰", f"{s['upgradeOffers']} / {s['diceUpgradeOffers']} / {s['serviceOffers']} / {s['newDiceOffers']}"],
        ["自选研习", f"每店 {s['selfStudyLimit']} 次，加价 {s['selfStudyPremium']}"],
        ["牌型升级价", f"基础价 + 已有等级 × {s['upgradePricePerExistingLevel']}"],
        ["刷新控制器货架", f"{s['firstRefreshPrice']} 起，每次 +{s['refreshPriceStep']}，每家店重置"],
        ["控制器稀有度出现权重（普通 / 罕见 / 稀有）", f"{s['controllerWeights']['common']} / {s['controllerWeights']['uncommon']} / {s['controllerWeights']['rare']}"],
        ["新骰子", f"基础价 {g['newDieBasePrice']}–{g['maxNewDieBasePrice']}；红面概率 {g['redFaceChancePercent']}%；模板权重 随机 {g['newDieTemplateWeights']['random']} / 小点 {g['newDieTemplateWeights']['low']} / 大点 {g['newDieTemplateWeights']['high']}"],
        ["首领战利品折现 / 牌型精研级数", f"{c['lootGoldValue']} 金币 / +{c['lootHandLevels']} 级"],
    ])
    faces = c["diceDefinition"]["faces"]
    out += ["标准骰：" + "，".join(f"{f['pipValue']} 点{'红' if f['colorId'] == 'red' else '蓝'}" for f in faces)
            + "。对面：" + "、".join(f"{a[1:].lstrip('0')}↔{b[1:].lstrip('0')}" for a, b in c["faceOpposites"].items() if a < b) + "。", ""]

    out += ["## 2. 牌型、颜色与红组", ""]
    out += table(["牌型", "0 级倍率", "每级", "升级基础价", "结构优先级"],
                 [[h["displayName"], mult(h["baseMult100"]), "+" + mult(h["incrementPerLevel100"])[1:], h["baseUpgradePrice"], h["structuralPriority"]]
                  for h in sorted(c["handDefinitions"], key=lambda h: h["structuralPriority"])])
    out += ["同点牌型按结构优先级取最高的一个作为主牌型；顺子与同点牌型可以同时成立，倍率相加。什么都不成立是“散点”，倍率 ×1。", ""]
    out += table(["颜色牌型", "0 级倍率", "每级", "升级基础价"],
                 [[h["displayName"], mult(h["baseMult100"]), "+" + mult(h["incrementPerLevel100"])[1:], h["baseUpgradePrice"]] for h in c["colorDefinitions"]])
    out += table(["提交里的红骰数量", "红组倍率", "称号"], [[r["count"], mult(r["factor100"]), r["title"]] for r in c["redRewards"]])

    out += ["## 3. 附魔", ""]
    e_rules = c["enchantRules"]
    out += table(["附魔", "加价", "出现权重", "效果"], [
        [e["displayName"], e["buyPremium"], e["weight"], {
            "none": "–", "slotless": "不占普通栏位", "base10": f"触发时整手基础分 +{e_rules['baseAdd']}",
            "mult2": f"触发时整手得分 ×{e_rules['scoreMultiplier']}", "submit1": f"每关提交次数 +{e_rules['submissionBonus']}",
            "roll1": f"每手投掷次数 +{e_rules['rollBonus']}"}[e["id"]]] for e in c["enchantments"]])
    out += ["非计分类控制器不会带“基础分增加 / 倍率增加”，它们的附魔权重是 " + "、".join(f"{c_name} {w}" for c_name, w in zip(
        ["无", "不占栏位", "提交增加", "投掷增加"], [e_rules["nonSubmitDrawWeights"][k] for k in ("none", "slotless", "submit1", "roll1")])) + "。", ""]

    out += ["## 4. 关卡", ""]
    out += table(["关", "章", "类型", "目标", "高压目标", "过关金币（普通 / 高压）"], [
        [i + 1, st["chapter"], KIND[st["stageKind"]], f"{int(st['normalTarget']):,}", f"{int(st['riskTarget']):,}" if st.get("riskTarget") else "–",
         f"{st['normalReward']}" + (f" / {st['riskReward']}" if st["riskEnabled"] else "")] for i, st in enumerate(c["stages"])])

    out += ["## 5. 首领减益", ""]
    for tier in (1, 2, 3, 4):
        out += [f"### {TIER[tier]}", ""]
        out += table(["编号", "名称", "效果代码", "说明"], [[m["id"], m["displayName"], " + ".join(m["effects"]) + (f"（{m['amount']}）" if m["amount"] else "") + (f"（{m['filter']}）" if m["filter"] else ""), m["description"]]
                                                 for m in c["bossModifiers"] if m["tier"] == tier])
    out += ["常规减益的抽取权重等于档位（一档 1、二档 2、三档 3），一局内不重复。", ""]
    out += ["## 6. 首领战利品", ""]
    out += table(["编号", "名称", "权重", "可拿件数", "说明"], [[k["id"], k["displayName"], k["weight"], k["picks"], k["description"]] for k in c["lootKinds"]])

    out += ["## 7. 控制器", ""]
    non_submit = set(e_rules["nonSubmitControllerIds"])
    groups = [("7.1 基础 50 张（p0.6 规格，v0.8 调过数值）", lambda n: n <= 53), ("7.2 v0.9 新增 50 张", lambda n: 54 <= n <= 103), ("7.3 v1.0 新增 50 张", lambda n: n >= 104)]
    for title, pick in groups:
        rows = [x for x in c["controllers"] if pick(int(x["id"][1:]))]
        out += [f"### {title}", ""]
        out += table(["编号", "名称", "稀有度", "价", "类别", "说明"], [[x["id"], x["displayName"], RARITY[x["rarity"]], x["buyPrice"], "非计分" if x["id"] in non_submit else "计分", x["description"]] for x in rows])
    out += [f"已移除的编号：{'、'.join(c.get('removedControllerIds') or [])}（保留编号空位，不要复用）。", ""]

    out += ["## 8. 道具", ""]
    out += table(["编号", "名称", "价", "使用时机", "说明"], [[x["id"], x["displayName"], x["baseBuyPrice"], WINDOW.get(x.get("window"), x.get("window", "")), x["description"]] for x in c["consumables"]])
    out += ["## 9. 商店服务", ""]
    out += table(["编号", "名称", "基础价", "价格规则", "说明"], [[x["id"], x["displayName"], x["baseBuyPrice"], x.get("priceRule", ""), x["description"]] for x in c["services"]])
    out += ["## 10. 骰面改造", ""]
    out += table(["编号", "名称", "价", "说明"], [[x["id"], x["displayName"], x["buyPrice"], x["description"]] for x in c["diceUpgrades"]])
    return "\n".join(out).rstrip("\n") + "\n"


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    text = build()
    if args.check:
        same = OUT.exists() and OUT.read_text(encoding="utf-8") == text
        print("content reference matches the configuration" if same else "content reference differs from the configuration")
        return 0 if same else 1
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print("wrote", OUT.relative_to(ROOT), f"({len(text.splitlines())} lines)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
