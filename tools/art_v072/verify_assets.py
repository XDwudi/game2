"""V72-A: the v0.72 art set is reproducible and complete. Read-only except for its report.

Rebuilds every generated file into a temporary directory and compares it byte for byte with the
committed export, then checks the expected names and sizes. A pass says nothing about taste or
about how the art looks in the engine.
"""
import hashlib
import json
import sys
import tempfile
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image

import build_art

ROOT = Path(__file__).resolve().parents[2]
COMMITTED = ROOT / "DicePrototype" / "Assets" / "Art" / "Art072"
EXPECTED = {
    "Surfaces": {**{name: (512, 512) for name in (
        "ui_paper_panel", "ui_card_controller", "ui_card_controller_compact", "ui_card_item", "ui_card_upgrade", "ui_panel_modal",
        "ui_tooltip_frame", "ui_button_bevel", "ui_info_inset", "ui_panel_plain", "ui_chip_label", "ui_slot_dice", "ui_slot_inventory",
        "ui_focus_frame", "ui_scroll_handle", "ui_frame_block")}, "ui_progress_track": (768, 96), "ui_progress_fill": (768, 96)},
    "UI": {"ui_wordmark_dicecraft": (1440, 400), "ui_card_back": (768, 1024), "ui_pip_dot": (64, 64)},
    "Backgrounds": {"bg_table_scene": (1920, 1080), "bg_felt_tile": (1024, 1024)},
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    failures, items = [], []
    with tempfile.TemporaryDirectory() as temporary:
        build_art.OUT = Path(temporary)
        build_art.main()
        rebuilt = {p.relative_to(temporary).as_posix(): p for p in Path(temporary).rglob("*.png")}
        committed = {p.relative_to(COMMITTED).as_posix(): p for p in COMMITTED.rglob("*.png")}
        for extra in sorted(set(committed) - set(rebuilt)):
            failures.append("committed PNG is not produced by build_art.py: " + extra)
        for name, path in sorted(rebuilt.items()):
            if name not in committed:
                failures.append("generated file is not committed: " + name)
                continue
            same = sha(path) == sha(committed[name])
            if not same:
                failures.append("committed file differs from a fresh build: " + name)
            with Image.open(committed[name]) as image:
                size = list(image.size)
            items.append({"path": name, "size": size, "sha256": sha(committed[name]), "reproduced": same})
    for group, names in EXPECTED.items():
        for name, size in names.items():
            match = next((i for i in items if i["path"] == f"{group}/{name}.png"), None)
            if match is None:
                failures.append(f"missing {group}/{name}.png")
            elif tuple(match["size"]) != size:
                failures.append(f"{group}/{name}.png is {match['size']}, expected {list(size)}")
    report = {"schema": "art072-verify-2", "utc": datetime.now(timezone.utc).isoformat(), "items": len(items), "failures": failures,
              "result": "CHECKS_PASSED" if not failures else "CHECKS_FAILED", "assets": items}
    out = ROOT / "evidence" / "v072" / "assets" / "verify.json"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(report["result"], "items=%d failures=%d" % (len(items), len(failures)))
    for failure in failures[:20]:
        print(" -", failure)
    return 0 if not failures else 1


if __name__ == "__main__":
    sys.exit(main())
