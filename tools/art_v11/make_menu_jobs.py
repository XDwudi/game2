"""Prepare W23's five menu tray jobs from the W17 r02 silhouette, without generating images."""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ART = Path("ArtSource/Art11")
KITS = Path("ArtSource/StyleKit/themes")
DEFAULT_TRAY = ART / "default/menu/r02/original.png"

# MENU_KEY_ART §3 defines the material treatment and where ornament may appear.
MATERIALS = {
    "swiss": "A folded warm-paper tray with restrained black rules along its lip and one small spot of printed colour on the edge. Keep the central bed plain and the black rules below the cards. Limit any red to a tiny edge accent.",
    "sancai": "A low ceramic tray with a thin layer of glaze visible on the outer sides. Use a quiet, evenly lit inner bed and keep glaze highlights away from the foreground dice positions.",
    "comic": "A tray with a stamp-printed contour. Confine the halftone to the narrow rim shadow. Keep the entire inner bed free of dots, radiating lines, starbursts and speech bubbles.",
    "deco": "A black lacquer tray with a narrow gold lip and small symmetrical corner details. Separate the silhouette from a dark table with a restrained rim. Keep gold off the central bed.",
    "glass": "A dark supporting bed with small pieces of coloured glass and lead seams confined to the outer rim. Leave a continuous quiet pane throughout the inner bed. All seams end at the lip.",
}

SUBJECT = (
    "One empty wide shallow tabletop tray, seen nearly overhead. Match the projected silhouette, "
    "angle and proportions of reference 2. Use reference 1 alone for the rendering style. "
    "The entire back rim is visible, the two back corners taper slightly inward, "
    "and the low near lip casts a narrow contact shadow. Light from upper left. "
    "The central bed is a single uninterrupted resting surface for the runtime foreground. "
    "Show the complete tray alone on transparent ground with space around all four edges. "
    "No cards, dice, pips, letters, numbers, coins, people, emblems, captions, buttons, or artwork outside the silhouette."
)
REFERENCE_ROLE = (
    "SUBJECT AND SILHOUETTE REFERENCE ONLY. Match the same tray, outline, projection and rim placement. "
    "Reference 1 is the only style reference for colour, material treatment, shading and linework."
)


def menu_job(theme: str) -> dict:
    return {
        "jobId": f"V11-{theme.upper()}-MENU-TRAY",
        "category": "illustration",
        "subject": f"{SUBJECT} {MATERIALS[theme]}",
        "extraReferences": [{"path": DEFAULT_TRAY.as_posix(), "role": REFERENCE_ROLE}],
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--list", type=Path, help="write kitPath|jobPath lines for run_batch.ps1")
    parser.add_argument("--replace", action="store_true", help="replace differing job definitions")
    args = parser.parse_args()
    if not (ROOT / DEFAULT_TRAY).is_file():
        parser.error(f"missing subject reference: {DEFAULT_TRAY}")
    updates = {}
    lines = []
    for theme in MATERIALS:
        kit = KITS / theme / "style_kit.json"
        if not (ROOT / kit).is_file():
            parser.error(f"missing style kit: {kit}")
        path = ART / theme / "menu/job.json"
        lines.append(f"{kit.as_posix()}|{path.as_posix()}")
        target = ROOT / path
        job = menu_job(theme)
        if target.exists():
            if json.loads(target.read_text(encoding="utf-8")) == job:
                continue
            if not args.replace:
                parser.error(f"{path} differs. Review the change before using --replace.")
        updates[target] = job
    for target, job in updates.items():
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(json.dumps(job, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    if args.list:
        args.list.parent.mkdir(parents=True, exist_ok=True)
        args.list.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    print(f"{len(lines)} menu jobs, {len(updates)} written")


if __name__ == "__main__":
    main()
