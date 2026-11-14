"""Copy the accepted v0.9 illustrations, byte for byte, into the Unity project.

    python tools/art_v09/stage.py            # copy and write the manifest
    python tools/art_v09/stage.py --check    # fail if the project copies differ from the archived originals

Source: ArtSource/Art09/Illustrations/<ID>/original.png (+ record.json written by generate.py)
Target: DicePrototype/Assets/Art/Art071/Illustrations/illustration_<id lower>.png, next to the 71 v0.71 illustrations,
        so the game's lookup by name needs no change. Unity import settings are applied by DiceCraft.Editor.V09Setup.
"""
import argparse
import hashlib
import json
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PLAN = ROOT / "tools/art_v09/prompts.json"
SOURCE = ROOT / "ArtSource/Art09/Illustrations"
TARGET = ROOT / "DicePrototype/Assets/Art/Art071/Illustrations"
MANIFEST = ROOT / "ArtSource/Art09/Illustrations/manifest.json"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    entries = json.loads(PLAN.read_text(encoding="utf-8"))["entries"]
    rows, problems = [], []
    for entry in entries:
        source = SOURCE / entry["id"] / "original.png"
        record = SOURCE / entry["id"] / "record.json"
        target = TARGET / f"illustration_{entry['id'].lower()}.png"
        if not source.exists() or not record.exists():
            problems.append(f"{entry['id']}: no archived original")
            continue
        digest = sha(source)
        if json.loads(record.read_text(encoding="utf-8"))["sha256"] != digest:
            problems.append(f"{entry['id']}: original differs from its record")
            continue
        if args.check:
            if not target.exists() or sha(target) != digest:
                problems.append(f"{entry['id']}: project copy missing or different")
        else:
            shutil.copyfile(source, target)
        rows.append({"id": entry["id"], "name": entry["name"], "source": source.relative_to(ROOT).as_posix(),
                     "target": target.relative_to(ROOT).as_posix(), "sha256": digest})
    if problems:
        raise SystemExit("\n".join(problems))
    if not args.check:
        MANIFEST.write_text(json.dumps({"schema": "v09-illustration-manifest-1", "count": len(rows), "entries": rows},
                                       ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(("checked" if args.check else "staged"), len(rows), "illustrations")


if __name__ == "__main__":
    main()
