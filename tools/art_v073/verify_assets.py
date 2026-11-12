"""V73-J: the v0.73 art set is reproducible and complete. Read-only except for its report.

Rebuilds every generated file into a temporary directory and compares it byte for byte with the
committed export, then checks the expected names, sizes and nine-slice borders against
docs/specs/v073/ART_PLAN.md section 3. A pass says nothing about taste or about how the art looks
in the engine.
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
COMMITTED = ROOT / "DicePrototype" / "Assets" / "Art" / "Art073"
SQUARE = ("ui_paper_panel", "ui_card_controller", "ui_card_controller_compact", "ui_card_item", "ui_card_upgrade", "ui_card_shop",
          "ui_panel_modal", "ui_tooltip_frame", "ui_button_bevel", "ui_price_chip", "ui_info_inset", "ui_panel_hud", "ui_section_panel",
          "ui_button_ghost", "ui_ring", "ui_frame_enchant", "ui_tab_bottom", "ui_panel_plain", "ui_chip_label", "ui_slot_dice",
          "ui_slot_inventory", "ui_focus_frame", "ui_scroll_handle", "ui_frame_block")
EXPECTED = {
    "Surfaces": {**{name: (512, 512) for name in SQUARE}, "ui_section_header": (512, 128), "ui_tray_front": (512, 160),
                 "fx_score_plate": (768, 384), "fx_formula_chip": (512, 128)},
    "UI": {"ui_pedestal": (384, 192), "ui_die_shadow": (256, 128), "ui_tag_lock": (128, 128), "ui_tag_clock": (128, 128),
           "ui_badge_check": (96, 96), "ui_icon_coin": (96, 96), "ui_icon_dice_view": (128, 128), "ui_icon_gear": (96, 96),
           "ui_icon_book": (96, 96), "ui_icon_arrow": (96, 96)},
    "FX": {"fx_score_ray": (384, 96), "fx_score_crown": (192, 128), "fx_score_spark": (64, 64)},
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
                size, mode = list(image.size), image.mode
            if mode != "RGBA":
                failures.append(name + " has no alpha channel")
            items.append({"path": name, "size": size, "sha256": sha(committed[name]), "reproduced": same})
    expected_count = 0
    for group, names in EXPECTED.items():
        for name, size in names.items():
            expected_count += 1
            match = next((i for i in items if i["path"] == f"{group}/{name}.png"), None)
            if match is None:
                failures.append(f"missing {group}/{name}.png")
            elif tuple(match["size"]) != size:
                failures.append(f"{group}/{name}.png is {match['size']}, expected {list(size)}")
    if len(items) != expected_count:
        failures.append(f"{len(items)} files on disk, {expected_count} in the plan")
    for name, (left, bottom, right, top) in build_art.BORDERS.items():
        size = EXPECTED["Surfaces"].get(name)
        if size is None or left + right >= size[0] or bottom + top >= size[1]:
            failures.append("nine-slice border does not fit " + name)
    report = {"schema": "art073-verify-1", "utc": datetime.now(timezone.utc).isoformat(), "items": len(items), "failures": failures,
              "result": "CHECKS_PASSED" if not failures else "CHECKS_FAILED", "assets": items}
    out = ROOT / "evidence" / "v073" / "assets" / "verify.json"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(report["result"], "items=%d failures=%d" % (len(items), len(failures)))
    for failure in failures[:20]:
        print(" -", failure)
    return 0 if not failures else 1


if __name__ == "__main__":
    sys.exit(main())
