"""Stage the reviewed v1.0 illustrations (AI_ART_PIPELINE S8).

For every job in tools/art_v10/jobs.py the accepted revision is the newest VALID one, unless
ArtSource/Art10/accepted.json names another revision for that id (written by hand after review).
The original bytes are copied unchanged to

    ArtSource/Art10/IllustrationsFull/<ID>/{original.png, record.json, prompt.txt, review_sheet.png, review_metrics.json}
    DicePrototype/Assets/Art/Art071/Illustrations/illustration_<id>.png

and ArtSource/Art10/manifest.json lists id, revision, sha256, size and the run check.

    python tools/art_v10/stage.py            # stage everything that has a valid revision
    python tools/art_v10/stage.py --check    # verify staged files match the manifest and the archive
    python tools/art_v10/stage.py --contact  # write contact sheets for review to artifacts/cache/v10/art-contact/
"""
import argparse
import hashlib
import json
import shutil
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).resolve().parent))
from jobs import JOBS, SUBJECTS  # noqa: E402

ARCHIVE = ROOT / "ArtSource" / "Art10"
RUNTIME = ROOT / "DicePrototype" / "Assets" / "Art" / "Art071" / "Illustrations"
CONTACT = ROOT / "artifacts" / "cache" / "v10" / "art-contact"
KEEP = ["original.png", "record.json", "prompt.txt", "review_sheet.png", "review_metrics.json"]


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def accepted_revision(content_id: str, overrides: dict):
    wanted = overrides.get(content_id)
    revs = sorted((JOBS / content_id).glob("r[0-9][0-9]"))
    if wanted:
        revs = [r for r in revs if r.name == wanted]
    for rev in reversed(revs):
        record = rev / "record.json"
        if record.exists() and json.loads(record.read_text(encoding="utf-8")).get("runValid"):
            return rev
    return None


def stage(check: bool) -> int:
    overrides_path = ARCHIVE / "accepted.json"
    overrides = json.loads(overrides_path.read_text(encoding="utf-8")) if overrides_path.exists() else {}
    entries, missing, problems = [], [], []
    for content_id, subject in SUBJECTS:
        rev = accepted_revision(content_id, overrides)
        if rev is None:
            missing.append(content_id)
            continue
        record = json.loads((rev / "record.json").read_text(encoding="utf-8"))
        digest = sha(rev / "original.png")
        if record["output"]["sha256"] != digest:
            problems.append(content_id + ": original.png does not match its record")
            continue
        target_dir = ARCHIVE / "IllustrationsFull" / content_id
        runtime = RUNTIME / f"illustration_{content_id.lower()}.png"
        if check:
            if not runtime.exists() or sha(runtime) != digest:
                problems.append(content_id + ": runtime file missing or different")
            if not (target_dir / "original.png").exists() or sha(target_dir / "original.png") != digest:
                problems.append(content_id + ": archive file missing or different")
        else:
            target_dir.mkdir(parents=True, exist_ok=True)
            for name in KEEP:
                if (rev / name).exists():
                    shutil.copyfile(rev / name, target_dir / name)
            shutil.copyfile(rev / "original.png", runtime)
        entries.append({
            "id": content_id, "revision": rev.name, "sha256": digest, "dimensions": record["output"]["dimensions"],
            "subject": subject, "kitVersion": record.get("styleKit", {}).get("kitVersion"), "runValid": record["runValid"],
            "runtime": str(runtime.relative_to(ROOT)).replace("\\", "/"),
        })
    manifest = {"schema": "v10-illustration-manifest-1", "count": len(entries), "missing": missing, "entries": entries}
    text = json.dumps(manifest, ensure_ascii=False, indent=1) + "\n"
    path = ARCHIVE / "manifest.json"
    if check:
        if not path.exists() or path.read_text(encoding="utf-8") != text:
            problems.append("manifest.json differs from the staged state")
    else:
        ARCHIVE.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8", newline="\n")
    print(json.dumps({"staged": len(entries), "missing": missing, "problems": problems}, ensure_ascii=False))
    return 1 if missing or problems else 0


def contact() -> int:
    from PIL import Image, ImageDraw
    CONTACT.mkdir(parents=True, exist_ok=True)
    cell, label, cols, rows = 300, 26, 6, 3
    ids = [i for i, _ in SUBJECTS]
    for page in range(0, len(ids), cols * rows):
        sheet = Image.new("RGB", (cols * cell, rows * (cell + label)), (244, 232, 203))
        draw = ImageDraw.Draw(sheet)
        for n, content_id in enumerate(ids[page:page + cols * rows]):
            rev = accepted_revision(content_id, {})
            x, y = (n % cols) * cell, (n // cols) * (cell + label)
            draw.text((x + 6, y + 4), content_id + ("" if rev is None else " " + rev.name), fill=(22, 38, 41))
            if rev is None:
                continue
            image = Image.open(rev / "original.png").convert("RGBA")
            image.thumbnail((cell - 20, cell - 20), Image.LANCZOS)
            sheet.paste(image, (x + (cell - image.width) // 2, y + label + (cell - image.height) // 2), image)
        out = CONTACT / f"contact_{page // (cols * rows) + 1}.png"
        sheet.save(out)
        print("wrote", out)
    return 0


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--contact", action="store_true")
    args = ap.parse_args()
    return contact() if args.contact else stage(args.check)


if __name__ == "__main__":
    raise SystemExit(main())
