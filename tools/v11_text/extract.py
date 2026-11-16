"""Extract the approved text appendix without changing names or gameplay."""
import argparse
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MARKDOWN = ROOT / "docs/proposals/2026-11-16-text/CONTENT_REWRITES.md"
BASE = ROOT / "tools/balance_sim/baseline/P0Config.p1.0.0.json"
CONTENT = Path(__file__).with_name("content_text.json")
AUDIT = Path(__file__).with_name("extraction_report.json")
CATEGORIES = ("controllers", "consumables", "services", "diceUpgrades", "bossModifiers", "lootKinds")
# Appendix section 5 counts are (fields, covered, rows, problem rows, unchanged rows).
EXPECTED = {
    "controllers": (300, 300, 150, 135, 15),
    "consumables": (24, 24, 12, 11, 1),
    "services": (33, 33, 22, 21, 1),
    "diceUpgrades": (22, 22, 11, 7, 4),
    "bossModifiers": (78, 78, 39, 11, 28),
    "lootKinds": (12, 12, 6, 0, 6),
    "redRewards": (9, 9, 9, 2, 7),
    "enchantments": (6, 6, 6, 4, 2),
    "handDefinitions": (13, 13, 13, 3, 10),
    "colorDefinitions": (2, 2, 2, 0, 2),
}


def render(value):
    return json.dumps(value, ensure_ascii=False, indent=2) + "\n"


def table_rows(markdown):
    rows = []
    section = None
    for line_number, line in enumerate(markdown.splitlines(), 1):
        heading = re.match(r"### 3\.(\d+) ", line)
        if heading:
            section = int(heading[1])
        elif line.startswith("## "):
            section = None
        if section and line.startswith("|"):
            cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
            if cells[0] == "ID" or all(re.fullmatch(r"[-:]+", cell) for cell in cells):
                continue
            if len(cells) != 7:
                raise ValueError(f"Malformed appendix row at line {line_number}")
            rows.append(dict(zip(("id", "name", "current", "problem", "shortText", "details", "notes"), cells),
                             section=section, line=line_number))
    return rows


def normalize(item_id, field, text, substitutions, normalizations):
    rules = (
        ("D2", "控制器", "卡牌", "DEV_PLAN §3 D2"),
        ("D4", r"普通(?=(?:稀有度|控制器|卡牌))", "常见", "DEV_PLAN §3 D4"),
        ("D4", "罕见", "进阶", "DEV_PLAN §3 D4"),
    )
    for decision, pattern, replacement, source in rules:
        after = re.sub(pattern, replacement, text)
        if after != text:
            substitutions.append({"id": item_id, "field": field, "decision": decision,
                                  "before": text, "after": after, "source": source})
            text = after
    if item_id in {"U08", "U09", "U10", "U11"} and "目标面" in text:
        after = text.replace("目标面", "这一面")
        normalizations.append({"id": item_id, "field": field, "before": text, "after": after,
                               "source": "用语规范 0.2 §2.1；内容附录 §4.2",
                               "reason": "无问题行仍用了规范禁用的目标面；仅统一指代，不改规则。"})
        text = after
    return text


def extract(markdown=None, base=None):
    markdown = MARKDOWN.read_text(encoding="utf-8") if markdown is None else markdown
    base = json.loads(BASE.read_text(encoding="utf-8")) if base is None else base
    if base["configVersion"] != "p1.0.0":
        raise ValueError("Expected frozen p1.0.0 base")
    rows = table_rows(markdown)
    grouped = {key: [] for key in EXPECTED}
    base_by_category = {}
    for key in EXPECTED:
        base_by_category[key] = {}
        for entry in base[key]:
            item_id = f"R{entry['count']}" if key == "redRewards" else entry["id"]
            if item_id in base_by_category[key]:
                raise ValueError(f"Duplicate base ID: {key}/{item_id}")
            base_by_category[key][item_id] = entry
    seen = set()
    for row in rows:
        if row["section"] <= 6:
            category = CATEGORIES[row["section"] - 1]
        else:
            matches = [key for key in tuple(EXPECTED)[6:] if row["id"] in base_by_category[key]]
            if len(matches) != 1:
                raise ValueError(f"Unknown name-only ID: {row['id']}")
            category = matches[0]
        price_row = row["id"].endswith(" 价格")
        item_id = row["id"].removesuffix(" 价格")
        identity = (category, row["id"])
        if identity in seen:
            raise ValueError(f"Duplicate appendix ID: {identity}")
        seen.add(identity)
        if item_id not in base_by_category[category]:
            raise ValueError(f"Unknown appendix ID: {identity}")
        entry = base_by_category[category][item_id]
        field = "priceRule" if price_row else "description" if row["section"] <= 6 else "title" if category == "redRewards" else "displayName"
        if row["current"] != entry[field]:
            raise ValueError(f"Appendix current text differs from frozen base: {identity}/{field}")
        if row["section"] <= 6 and row["name"] != entry["displayName"]:
            raise ValueError(f"Appendix name differs from frozen base: {identity}")
        if not row["shortText"] or len(row["shortText"]) > 28:
            raise ValueError(f"Appendix shortText must contain 1..28 characters: {identity}")
        grouped[category].append(row)

    counts = {}
    for category, expected in EXPECTED.items():
        category_rows = grouped[category]
        expected_ids = set(base_by_category[category])
        if category == "services":
            expected_ids |= {item_id + " 价格" for item_id in expected_ids.copy()}
        if {row["id"] for row in category_rows} != expected_ids:
            raise ValueError(f"Missing appendix IDs: {category}")
        fields = len(base[category]) * (3 if category == "services" else 2 if category in CATEGORIES else 1)
        unchanged = sum(row["problem"] == "无" for row in category_rows)
        actual = (fields, fields, len(category_rows), len(category_rows) - unchanged, unchanged)
        if actual != expected:
            raise ValueError(f"Appendix count mismatch for {category}: {actual} != {expected}")
        summary_lines = [line for line in markdown.split("## 5.", 1)[-1].splitlines()
                         if re.search(rf"\b{category}\b", line)]
        if len(summary_lines) != 1:
            raise ValueError(f"Missing appendix summary: {category}")
        summary = tuple(int(cell.strip()) for cell in summary_lines[0].strip().strip("|").split("|")[1:])
        if summary != expected:
            raise ValueError(f"Appendix section 5 mismatch: {category}")
        counts[category] = dict(zip(("fields", "coveredFields", "rows", "problemRows", "noProblemRows"), actual))
    totals = tuple(sum(values[index] for values in EXPECTED.values()) for index in range(5))
    total_lines = [line for line in markdown.split("## 5.", 1)[-1].splitlines() if line.startswith("| 合计 |")]
    if len(total_lines) != 1 or tuple(int(cell.strip()) for cell in total_lines[0].strip().strip("|").split("|")[1:]) != totals:
        raise ValueError("Appendix section 5 total mismatch")

    content = {key: [] for key in CATEGORIES}
    audit = {"source": MARKDOWN.relative_to(ROOT).as_posix(),
             "sourceSha256": hashlib.sha256(markdown.encode("utf-8")).hexdigest(),
             "counts": counts, "substitutions": [], "normalizations": [],
             "noProblemIds": [], "reusedCurrentTextIds": [], "emptyDetailsIds": [],
             "servicePriceRows": [], "displayNameChanges": []}
    for category in CATEGORIES:
        for row in grouped[category]:
            item_id = row["id"]
            if item_id.endswith(" 价格"):
                audit["servicePriceRows"].append({"id": item_id.removesuffix(" 价格"), "shortText": row["shortText"], "details": row["details"]})
                continue
            short = row["shortText"]
            if row["problem"] == "无":
                audit["noProblemIds"].append(item_id)
                if short != row["current"]:
                    raise ValueError(f"No-problem row changed current text: {item_id}")
                short = row["current"]
                if item_id not in {"U08", "U09", "U10", "U11"}:
                    audit["reusedCurrentTextIds"].append(item_id)
            # Empty appendix details stay empty: the short text already states the whole rule and the UI shows it
            # alone (coordinator decision, docs/specs/v11/W10_REPORT.md section 8). Never invent a rule.
            details = row["details"]
            if not details:
                audit["emptyDetailsIds"].append(item_id)
            entry = {"id": item_id}
            for field, value in (("shortText", short), ("details", details)):
                entry[field] = normalize(item_id, field, value, audit["substitutions"], audit["normalizations"])
            if (entry["details"] and not entry["details"].strip()) or not 1 <= len(entry["shortText"]) <= 28:
                raise ValueError(f"Invalid generated text: {item_id}")
            content[category].append(entry)
        for entry in base[category]:
            before = entry["displayName"]
            if "控制器" in before:
                audit["displayNameChanges"].append({"id": entry["id"], "field": "displayName", "decision": "D2",
                                                     "before": before, "after": before.replace("控制器", "卡牌")})
    audit["entryCount"] = sum(map(len, content.values()))
    audit["textFieldCount"] = audit["entryCount"] * 2
    audit["maxShortTextLength"] = max(len(row["shortText"]) for group in content.values() for row in group)
    return content, audit


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    content, audit = extract()
    outputs = ((CONTENT, render(content)), (AUDIT, render(audit)))
    if args.check:
        different = [str(path.relative_to(ROOT)) for path, text in outputs if not path.exists() or path.read_bytes() != text.encode("utf-8")]
        print("DIFFERS: " + ", ".join(different) if different else "text data matches appendix")
        return int(bool(different))
    for path, text in outputs:
        path.write_text(text, encoding="utf-8", newline="\n")
    print(f"extracted {audit['entryCount']} entries / {audit['textFieldCount']} text fields")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
