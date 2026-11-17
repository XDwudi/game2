"""Collect the subject text every default illustration was generated from (V1.1 W22 preparation).

The 184 default card illustrations come from three production rounds that recorded their prompts in different places:
  - v0.71: ArtSource/Art071/IllustrationsFull/<ID>/prompt.txt ("Subject:" and "Distinct silhouette:" lines, Chinese)
           and ArtSource/Art071/Illustrations/prompts.json (three hand-written prompts, English)
  - v0.9:  ArtSource/Art09/Illustrations/<ID>/prompt.txt ("Subject:" line, Chinese)
  - v1.0:  ArtSource/AIConcepts/v10-illustrations/<ID>/job.json ("subject", English)
This script only gathers them (with their source path) next to the shipped file each id maps to, so a reviewer can
check every subject against the picture that actually shipped. It writes no game asset.

usage: python tools/art_v11/collect_subjects.py --out <draft.json>
"""
import argparse
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SHIPPED = ROOT / "DicePrototype/Assets/Art/Art071/Illustrations"


def from_prompt(path: Path) -> dict:
    text = path.read_text(encoding="utf-8")
    out = {}
    m = re.search(r"^Subject:\s*(.+)$", text, re.M)
    if m:
        out["subject"] = m.group(1).strip()
    m = re.search(r"^Distinct silhouette:\s*(.+?)(?:\.\s*Change only|$)", text, re.M)
    if m:
        out["silhouette"] = m.group(1).strip()
    if "subject" not in out:
        # Some v0.71 prompts are free-form English with the subject inside; keep the whole prompt for the reviewer.
        out["subject"] = text.strip()
        out["freeForm"] = True
    return out


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    args = ap.parse_args()
    ids = sorted(p.stem.split("_", 1)[1].upper() for p in SHIPPED.glob("illustration_*.png"))
    found = {}
    for d in sorted((ROOT / "ArtSource/Art071/IllustrationsFull").glob("*/prompt.txt")):
        entry = {"round": "v0.71", "source": d.relative_to(ROOT).as_posix(), "lang": "zh", **from_prompt(d)}
        if entry.get("freeForm"):
            entry["lang"] = "en"
        found[d.parent.name.upper()] = entry
    prompts = ROOT / "ArtSource/Art071/Illustrations/prompts.json"
    for key, text in json.loads(prompts.read_text(encoding="utf-8")).items():
        found.setdefault(key.upper(), {"round": "v0.71", "source": prompts.relative_to(ROOT).as_posix() + "#" + key,
                                       "lang": "en", "subject": text})
    for d in sorted((ROOT / "ArtSource/Art09/Illustrations").glob("*/prompt.txt")):
        found[d.parent.name.upper()] = {"round": "v0.9", "source": d.relative_to(ROOT).as_posix(), "lang": "zh", **from_prompt(d)}
    for d in sorted((ROOT / "ArtSource/AIConcepts/v10-illustrations").glob("*/job.json")):
        job = json.loads(d.read_text(encoding="utf-8"))
        cid = (job.get("contentId") or d.parent.name).upper()
        found[cid] = {"round": "v1.0", "source": d.relative_to(ROOT).as_posix(), "lang": "en", "subject": job["subject"]}
    table = {}
    for cid in ids:
        entry = dict(found.get(cid, {"round": None, "source": None, "subject": None}))
        entry["shipped"] = (SHIPPED / f"illustration_{cid.lower()}.png").relative_to(ROOT).as_posix()
        table[cid] = entry
    missing = [c for c, e in table.items() if not e.get("subject")]
    Path(args.out).write_text(json.dumps({"schema": "subjects-draft-1", "count": len(table), "missing": missing,
                                          "subjects": table}, ensure_ascii=False, indent=1, sort_keys=True) + "\n",
                              encoding="utf-8")
    print(f"{len(table)} ids, {len(table) - len(missing)} with a subject, missing: {missing}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
