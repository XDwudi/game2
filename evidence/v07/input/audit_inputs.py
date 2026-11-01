"""Read-only source audit; generated evidence stays beside this script."""
from pathlib import Path
import collections
import datetime
import hashlib
import json
import re
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
SPEC = ROOT / "docs/specs/v07"
SOURCE = Path(r"C:\Users\f\.codex\attachments\e5a137e5-6a3c-46fe-bdfb-05aa9311a3df")
INPUTS = {
    "ART_SPEC.md": "骰子构筑游戏_v0.7_AI美术开发规格.md",
    "ART_DESIGN.docx": "骰子构筑游戏_v0.7_美术界面与动效策划案.docx",
    "dice_art_v0_7_manifest.json": "dice_art_v0_7_manifest.json",
}

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def save(name, value):
    (OUT / name).write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

manifest = json.loads((SPEC / "dice_art_v0_7_manifest.json").read_text("utf-8-sig"))
md = (SPEC / "ART_SPEC.md").read_text("utf-8-sig")
embedded = json.loads(re.findall(r"```json\s*(.*?)```", md, re.S)[0])
with zipfile.ZipFile(SPEC / "ART_DESIGN.docx") as z:
    ns = {"w": "http://schemas.openxmlformats.org/wordprocessingml/2006/main"}
    xml = ET.fromstring(z.read("word/document.xml"))
    paras = ["".join(t.text or "" for t in el.findall(".//w:t", ns)) for el in xml.findall(".//w:p", ns)]
    docx = "\n".join(paras)
    (OUT / "docx-extracted.txt").write_text(docx, encoding="utf-8")
    (OUT / "concepts").mkdir(exist_ok=True)
    concepts = []
    for n, record in zip([x for x in z.namelist() if x.startswith("word/media/")], manifest["conceptArt"]):
        dest = OUT / "concepts" / Path(n).name
        dest.write_bytes(z.read(n))
        concepts.append({"key": record["key"], "title": record["title"], "path": str(dest.relative_to(ROOT)), "sha256": sha(dest), "runtimeImportAllowed": False})

main = md.split("<!-- WORD_MAIN_END -->")[0]
main = re.sub(r"## 04 四张方向效果图.*?(?=## 05)", "", main, flags=re.S)
docx_main = re.sub(r"04 四张方向效果图.*?(?=05 界面美术的制作顺序)", "", docx, flags=re.S)
docx_main = docx_main.replace("风格确认稿  界面与组件  骰子与控制器  计分演出  AI资源生产", "")
main = re.sub(r"\[([^\]]+)\]\([^\)]+\)", r"\1", main)
main = re.sub(r"^\s*\|?\s*:?-{3,}.*$", "", main, flags=re.M)
main = re.sub(r"^#{1,6}\s*", "", main, flags=re.M)
main = re.sub(r"^\s*[-*]\s+", "", main, flags=re.M)
normal = lambda s: re.sub(r"[\s|`*\u200b]", "", s)
same_main = normal(main) == normal(docx_main)

acceptance = []
for number, line in enumerate(md.splitlines(), 1):
    cells = [x.strip() for x in line.strip().strip("|").split("|")]
    if len(cells) == 3 and re.fullmatch(r"(?:ART|UI|FX)\d\d", cells[0]):
        acceptance.append({"id": cells[0], "scenario": cells[1], "expected": cells[2], "sourceLine": number, "runtimeStatus": "not_run"})
assets = manifest["sharedResources"]["assets"]
asset_ids = [a["assetId"] for a in assets]
content = manifest["contentResources"]
config_path = ROOT / "DicePrototype/Assets/Game/Data/P0Config.json"
golden_path = ROOT / "DicePrototype/Assets/Game/Data/GoldenScores.json"
config = json.loads(config_path.read_text("utf-8-sig"))
rules = {x["id"]: x for key in ["controllers", "consumables", "services", "diceUpgrades"] for x in config[key]}
content_diffs = [{"id": a["id"], "artName": a["name"], "ruleName": rules.get(a["id"], {}).get("displayName")} for a in content if a["name"] != rules.get(a["id"], {}).get("displayName")]
trigger_diffs = [a["id"] for a in content if a["trigger"] != rules.get(a["id"], {}).get("description")]
tiers = []
for x in manifest["presentation"]["tierExamples"]:
    score, num, den = (int(x[k]) for k in ["scoreDecimal", "multiplierNumeratorDecimal", "multiplierDenominatorDecimal"])
    tier = 0 if score <= 0 or num <= 0 else sum(num >= threshold * den for threshold in [8, 32, 128, 1024, 8192])
    tiers.append({"id": x["id"], "actual": tier, "expected": x["expectedTier"], "pass": tier == x["expectedTier"]})

def luminance(hex_color):
    rgb = [int(hex_color[i:i+2], 16) / 255 for i in [1, 3, 5]]
    linear = [v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4 for v in rgb]
    return sum(a*b for a,b in zip(linear, [0.2126,0.7152,0.0722]))

contrast = []
for x in manifest["theme"]["solidColorContrastChecks"]:
    a,b = sorted(luminance(manifest["theme"]["colors"][x[k]]) for k in ["foreground","background"])
    ratio = (b + 0.05) / (a + 0.05)
    contrast.append({**x, "recomputedRatio": ratio, "pass": ratio >= x["target"] and abs(ratio-x["ratio"]) < 0.01})

report = {
    "generatedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
    "scope": "input integrity and independent design-data audit only; no Unity/Blender runtime acceptance",
    "inputs": [{"archivedPath": str((SPEC/name).relative_to(ROOT)), "sha256": sha(SPEC/name), "sizeBytes": (SPEC/name).stat().st_size, "originalPath": str(SOURCE/original), "copyExact": (SPEC/name).read_bytes() == (SOURCE/original).read_bytes()} for name,original in INPUTS.items()],
    "embeddedManifestMatchesStandalone": embedded == manifest,
    "docxMdMainNormalizedExact": same_main,
    "normalizedMainCharacterCount": len(normal(main)),
    "normalization": "Removed Markdown syntax/link destinations, whitespace and DOCX zero-width wrapping hints; excluded DOCX-only subtitle and section04 concept packaging/captions. Section04 four concept descriptions visually/textually reviewed separately, no semantic difference found.",
    "contentIdAndNameDiffsAgainstP06": content_diffs,
    "contentTriggerDescriptionDiffsAgainstP06": trigger_diffs,
    "contentCategoryCounts": dict(collections.Counter(a["category"] for a in content)),
    "contentIdSetMatchesP06": set(a["id"] for a in content) == set(rules),
    "retiredIdsAbsent": not set(config["removedControllerIds"]) & set(a["id"] for a in content),
    "sharedAssetCount": len(assets), "uniqueSharedAssetCount": len(set(asset_ids)),
    "sharedCategoryCountsMatch": dict(collections.Counter(a["category"] for a in assets)) == manifest["sharedResources"]["counts"]["byCategory"],
    "requiredSharedAssetCount": sum(a.get("required", True) for a in assets),
    "optionalSharedAssetIds": [a["assetId"] for a in assets if a.get("required") is False],
    "unresolvedDependencies": [(a["assetId"], d) for a in assets for d in a["dependencies"] if d not in asset_ids],
    "allPlanned": all(a["status"] == "planned" for a in assets + content),
    "acceptanceCount": len(acceptance),
    "acceptanceGroupCounts": dict(collections.Counter(re.sub(r"\d", "",a["id"]) for a in acceptance)),
    "tierChecks": tiers, "contrastChecks": contrast, "concepts": concepts,
    "frozenGameplayInputs": [{"path": str(p.relative_to(ROOT)), "sha256": sha(p)} for p in [config_path, golden_path]],
    "runtimeChecksPerformed": False,
}
save("source-audit.json", report)
save("acceptance-register.json", acceptance)
print(json.dumps({"copyExact": all(x["copyExact"] for x in report["inputs"]), "embeddedManifestMatches": embedded == manifest, "docxMainMatches": same_main, "acceptanceCount": len(acceptance), "tierChecksPassed": sum(x["pass"] for x in tiers), "contrastChecksPassed": sum(x["pass"] for x in contrast), "contentNameDifferences": len(content_diffs)}, ensure_ascii=False))
