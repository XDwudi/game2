"""Check W21's reviewed lexicon, or list every non-punctuation break in the FW-11 UTF-8 layout report."""
import argparse
import csv
import json
import re
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
WORDS = ROOT / "DicePrototype/Assets/Resources/Data/LineBreakWords.json"
CONFIG = ROOT / "DicePrototype/Assets/Game/Data/P0Config.json"
MATRIX = ROOT / "DicePrototype/Assets/Game/Tests/EditMode/V21WrapMatrix.cs"
CATEGORIES = ("controllers", "consumables", "services", "diceUpgrades", "bossModifiers", "lootKinds")


def corpus():
    config = json.loads(CONFIG.read_text(encoding="utf-8"))
    texts = [row["shortText"] for category in CATEGORIES for row in config[category]]
    # The EditMode matrix uses the same strings for the selected face, prompts, notes and prices.
    matrix = MATRIX.read_text(encoding="utf-8")
    samples = matrix.split("string[] FaceLines = {", 1)[1].split("};", 1)[0]
    return texts + re.findall(r'"([^"\n]+)"', samples)


def check():
    data = json.loads(WORDS.read_text(encoding="utf-8"))
    words = data["words"]
    if data["version"] != 1 or not words or words != sorted(set(words)):
        raise ValueError("LineBreakWords must be version 1 with sorted, unique words")
    texts = corpus()
    for word in words:
        if len(word) < 2 or not any(word in text for text in texts):
            raise ValueError(f"Dead or invalid line-break word: {word}")
    return len(words), len(texts)


def report_candidates(source, output):
    """Preserve all source contexts including theme, actual width, fitted size and line cap."""
    groups = defaultdict(list)
    layouts = 0
    with source.open(encoding="utf-8-sig", newline="") as src:
        for row in csv.DictReader(src, delimiter="\t"):
            layouts += 1
            lines = row["lines"].split(" / ")
            for left, right in zip(lines, lines[1:]):
                if left[-1] in "，。；：、！？）”》】」’…" or right[0] in "（“《【「‘":
                    continue
                context = left[-4:] + "|" + right[:4]
                groups[left[-1] + "|" + right[0]].append((context, row))
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("w", encoding="utf-8", newline="") as dst:
        writer = csv.writer(dst, delimiter="\t", lineterminator="\n")
        writer.writerow(("boundary", "context", "id", "theme", "scenario", "width", "font", "cap"))
        for pair, values in sorted(groups.items()):
            for context, row in values:
                writer.writerow((pair, context, *(row[key] for key in ("id", "theme", "scenario", "width", "font", "cap"))))
    return layouts, len(groups)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--layouts", type=Path, help="FW-11 layouts.tsv produced by V21WrapTests")
    parser.add_argument("--out", type=Path, default=ROOT / "artifacts/cache/v11/w21/wrap/non-punctuation.tsv")
    args = parser.parse_args()
    count, texts = check()
    print(f"Verified {count} words against {texts} short texts and face-panel samples")
    if args.layouts:
        layouts, pairs = report_candidates(args.layouts, args.out)
        print(f"{layouts} layouts, {pairs} distinct non-punctuation break pairs written to {args.out}")
