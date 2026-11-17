"""Write the artgen jobs for the V1.1 batch redraw of the card illustrations (W22).

Every job uses the checked subject table (ArtSource/Art11/subjects.json, W22a) and passes the shipped default
illustration as the SUBJECT REFERENCE, so each style changes only the rendering (user decision CP3-1). The functional
cues are appended to the subject as "Must keep: ..." so the generator sees them. Existing job files are never
overwritten unless --replace names the id (a changed subject then makes the next generation a new revision).

usage: python tools/art_v11/make_redraw_jobs.py --themes swiss sancai ... [--ids C01 C02 ...] [--replace ID ... | --replace-all] --list OUT.txt
The list file has one "kitPath|jobPath" line per job, in theme order, for the batch runner.
"""
import argparse
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
# 2026-11-17: the first ROLE told the generator to ignore the reference's colours completely, and the Swiss batch then
# drew neutral dark pips in cobalt and lost a glass die (C122, C128). Pip colours are game information (red and blue
# dice; STYLE_GUIDE section 4: pips are ink-coloured unless the card's mechanic involves red or blue), so the colours
# that carry information are now kept and only the rendering changes.
ROLE = ("SUBJECT REFERENCE: the same card drawn in the game's default style. Keep its objects, their arrangement, the "
        "idea, and every colour that carries game information: dice pips keep the reference's colours (dark pips stay "
        "dark, in the darkest ink of reference image 1; red pips stay red; blue pips stay blue), coloured die faces keep "
        "their colour, gold stays gold, and glass or transparent parts stay transparent. Never colour neutral dark pips "
        "red or blue. Ignore everything else about its rendering (outlines, shading, halftone, exact shades) and draw it "
        "in the style of reference image 1.")
PIP_CUE = "dice pips coloured exactly as in the subject reference (dark pips stay dark, never recoloured red or blue)"
DICE_WORDS = re.compile(r"\b(die|dice|pips?)\b", re.IGNORECASE)  # whole words: "pipe" and "diet" must not match


def subject_text(entry: dict) -> str:
    text = entry["subject"].rstrip(". ")
    cues = [c.strip().rstrip(".") for c in entry.get("cues", []) if c.strip()]
    mentions_dice = bool(DICE_WORDS.search(text + " " + " ".join(cues)))
    names_pip_colour = any(re.search(r"\bpips?\b", c, re.IGNORECASE) and re.search(r"\b(red|blue)\b", c, re.IGNORECASE)
                           for c in cues)
    if mentions_dice and not names_pip_colour:
        cues.append(PIP_CUE)
    return text + (". Must keep: " + "; ".join(cues) + "." if cues else "")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--themes", nargs="+", required=True)
    ap.add_argument("--ids", nargs="*")
    ap.add_argument("--replace", nargs="*", default=[])
    ap.add_argument("--replace-all", action="store_true", help="rewrite every listed job (a prompt rule changed)")
    ap.add_argument("--keep", nargs="*", default=[],
                    help="theme/ID jobs never rewritten, e.g. a job that is generating right now (artgen hashes the "
                         "job file when the run ends)")
    ap.add_argument("--list", required=True)
    args = ap.parse_args()
    table = json.loads((ROOT / "ArtSource/Art11/subjects.json").read_text(encoding="utf-8"))["subjects"]
    ids = [i.upper() for i in args.ids] if args.ids else sorted(table)
    replace = set(ids) if args.replace_all else {r.upper() for r in args.replace}
    keep = {k.lower().replace("\\", "/") for k in args.keep}
    lines, written, kept = [], 0, 0
    for theme in args.themes:
        kit = f"ArtSource/StyleKit/themes/{theme}/style_kit.json"
        if not (ROOT / kit).is_file():
            print(f"no kit for {theme}", file=sys.stderr)
            return 2
        for cid in ids:
            entry = table[cid]
            job_path = ROOT / "ArtSource/Art11" / theme / "illustrations" / cid / "job.json"
            if job_path.exists() and (cid not in replace or f"{theme}/{cid}".lower() in keep):
                kept += 1
            else:
                job = {"jobId": f"V11-{theme.upper()}-{cid}", "category": "illustration", "contentId": cid,
                       "subject": subject_text(entry),
                       "extraReferences": [{"path": entry["picture"], "role": ROLE}]}
                job_path.parent.mkdir(parents=True, exist_ok=True)
                job_path.write_text(json.dumps(job, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
                written += 1
            lines.append(f"{kit}|{job_path.relative_to(ROOT).as_posix()}")
    Path(args.list).write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"jobs listed {len(lines)}, written {written}, existing kept {kept}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
