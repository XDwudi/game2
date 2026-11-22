"""Write W17's six default loot jobs. Use --list FILE for the kitPath|jobPath batch format.

Artgen's default illustration kit supplies the style. The first default drawings have no
subject-reference images. Reviewed defaults can become subject references for W22 redraws.
"""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TABLE = Path("ArtSource/Art11/loot_subjects.json")
KIT = Path("ArtSource/StyleKit/style_kit.json")
JOBS = Path("ArtSource/Art11/default/loot")


THEMES = ("default","swiss","sancai","comic","deco","glass")


def build_jobs(table: dict, theme="default") -> dict:
    jobs = {}
    for content_id, entry in table["subjects"].items():
        clauses = [entry["subject"].rstrip(".") + ".", *entry["cues"], *table["defaults"]["cues"]]
        clauses.append("Item-specific restrictions: " + "; ".join(entry["avoid"]) + ".")
        job = {
            "jobId": f"V11-{theme.upper()}-{content_id}",
            "category": "illustration",
            "contentId": content_id,
            "subject": " ".join(clauses),
        }
        if theme != "default":
            reference = Path("DicePrototype/Assets/Art/Art11/Loot") / f"illustration_{content_id}.png"
            if not (ROOT / reference).is_file():
                raise FileNotFoundError(reference)
            job["extraReferences"] = [{
                "path": reference.as_posix(),
                "role": "SUBJECT REFERENCE ONLY. Keep the same objects, count, functional symbols and arrangement. "
                        "Neutral dice must have dark pips, and neutral faces stay opaque. "
                        "Use only reference 1 for style, material, shading and colour treatment."
            }]
        jobs[Path("ArtSource/Art11") / theme / "loot" / content_id / "job.json"] = job
    return jobs


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--list", type=Path, help="write the batch runner's job list")
    parser.add_argument("--theme", choices=THEMES, default="default")
    parser.add_argument("--replace", action="store_true", help="rewrite jobs whose subject changed")
    args = parser.parse_args()
    table = json.loads((ROOT / TABLE).read_text(encoding="utf-8"))
    jobs = build_jobs(table,args.theme)
    kit = KIT if args.theme=="default" else Path("ArtSource/StyleKit/themes") / args.theme / "style_kit.json"
    if not (ROOT / kit).is_file():
        parser.error(f"missing style kit {kit}")
    updates = {}
    for relative, job in jobs.items():
        target = ROOT / relative
        if target.is_file():
            if json.loads(target.read_text(encoding="utf-8")) == job:
                continue
            if not args.replace:
                parser.error(f"{relative} differs from the subject table; review and use --replace")
        updates[target] = job
    for target, job in updates.items():
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(json.dumps(job, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    if args.list:
        lines = [f"{kit.as_posix()}|{path.as_posix()}" for path in jobs]
        args.list.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    print(f"{len(jobs)} {args.theme} loot jobs, {len(updates)} written")


if __name__ == "__main__":
    main()
