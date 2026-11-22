"""Build the seven W17 tintable settlement sprites to artifacts/cache/v11/w17-staging.

Each ordered set of filled/cut polygons is the editable source of both the SVG and PNG.
The default theme's stepped outline comes from the v0.73 vector helper. Threshold or
colour changes during a theme swap must never be needed to read the tier marks.
"""
import importlib.util
import json
import math
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[3]
STAGING = ROOT / "artifacts/cache/v11/w17-staging"
SURFACES = Path("DicePrototype/Assets/Art/Art11/Surfaces")
spec = importlib.util.spec_from_file_location("art073", ROOT / "tools/art_v073/build_art.py")
art073 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(art073)


def tier_frame(tier):
    # Coordinates are screen units at 176. A quiet stepped frame keeps the bottom-right stamp legible.
    shapes = [(True, art073.stair(3, 7, 173, 173, 4)),
              (False, art073.stair(7, 11, 169, 169, 3))]
    if tier == 4:
        # Three broad tines and cut-in valleys read as a crown on the tiny stage card.
        shapes.append((False, [(68, 1), (108, 1), (108, 13), (68, 13)]))
        shapes.append((True, [(68, 1), (80, 7), (88, 0.5), (96, 7), (108, 1),
                              (104, 13), (72, 13)]))
    else:
        # Recessed diagonal marks in one stepped plate; large gaps survive a 60-pixel stage card.
        shapes.append((True, [(64, 7), (68, 7), (68, 3), (108, 3), (108, 7), (112, 7),
                              (112, 11), (108, 11), (108, 14), (68, 14), (68, 11), (64, 11)]))
        for mark in range(tier):
            x = 88 + (mark - (tier - 1) / 2) * 12
            shapes.append((False, [(x - 1, 4), (x + 4, 4), (x, 13), (x - 5, 13)]))
    return shapes


def stage_icon():
    # A planted pennant identifies the cleared stage. Its swallowtail stays open on a tiny chip.
    return [(True, [(17, 11), (26, 11), (26, 15), (83, 15), (70, 31), (83, 47),
                    (26, 47), (26, 76), (44, 76), (44, 85), (8, 85), (8, 76), (17, 76)])]


def submit_icon():
    # A die enters the play tray. A single cut-out pip distinguishes the die from a letter or parcel.
    die = art073.stair(33, 8, 63, 38, 3, n=1)
    return [(True, die), (False, [(44, 19), (52, 19), (52, 27), (44, 27)]),
            (True, [(43, 43), (53, 43), (53, 51), (66, 51), (48, 69), (30, 51), (43, 51)]),
            (True, [(9, 61), (19, 61), (19, 77), (77, 77), (77, 61), (87, 61),
                    (87, 83), (82, 88), (14, 88), (9, 83)])]


def coin_arc(y, first, last):
    return [(37 + 28 * math.cos(math.radians(angle)), y + 10 * math.sin(math.radians(angle)))
            for angle in range(first, last + 1, 10)]


def interest_icon():
    # The separated rims remain visible beside the plus sign after downsampling to 28 pixels.
    shapes = [(True, coin_arc(38, 0, 360))]
    for y in (44, 59):
        shapes.append((True, coin_arc(y, 0, 180) + list(reversed(coin_arc(y + 10, 0, 180)))))
    shapes.append((True, [(69, 7), (79, 7), (79, 17), (89, 17), (89, 27),
                          (79, 27), (79, 37), (69, 37), (69, 27), (59, 27),
                          (59, 17), (69, 17)]))
    return shapes


def write_sprite(output, name, design_size, pixels, shapes):
    # Soft pixels only carry alpha. Hue is supplied by UiKit at draw time.
    scale = pixels * 3 / design_size
    mask = Image.new("L", (pixels * 3, pixels * 3))
    draw = ImageDraw.Draw(mask)
    polygons = []
    for filled, points in shapes:
        draw.polygon([(x * scale, y * scale) for x, y in points], fill=255 if filled else 0)
        points_text = " ".join(f"{x:.4f},{y:.4f}" for x, y in points)
        polygons.append(f'    <polygon fill="{"white" if filled else "black"}" points="{points_text}"/>')
    svg = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{pixels}" height="{pixels}" viewBox="0 0 {design_size} {design_size}">',
           '  <defs><mask id="ink" maskUnits="userSpaceOnUse" x="0" y="0" width="100%" height="100%">',
           *polygons, '  </mask></defs>', '  <rect width="100%" height="100%" fill="white" mask="url(#ink)"/>', '</svg>']
    (output / "svg" / (name + ".svg")).write_text("\n".join(svg) + "\n", encoding="utf-8", newline="\n")
    image = Image.new("RGBA", (pixels, pixels), "white")
    image.putalpha(mask.resize((pixels, pixels), Image.Resampling.LANCZOS))
    image.save(output / "UI" / (name + ".png"))


def build(output=STAGING):
    for directory in ("UI", "svg"):
        (output / directory).mkdir(parents=True, exist_ok=True)
    sprites = [(f"ui_emblem_tier{tier}", 176, 352, tier_frame(tier)) for tier in range(1, 5)]
    sprites += [("ui_icon_stage", 96, 96, stage_icon()), ("ui_icon_submit", 96, 96, submit_icon()),
                ("ui_icon_interest", 96, 96, interest_icon())]
    # Expected subset of theme_contract.py's GENERATED rows after V11Setup.Integrate.
    # The scene binding, actual GUID and runtime referenced flag must come from Unity and production code.
    contract = []
    for name, design, pixels, shapes in sprites:
        write_sprite(output, name, design, pixels, shapes)
        contract.append({"name": name, "default": (SURFACES / (name + ".png")).as_posix(),
                         "size": [pixels, pixels], "border": [0., 0., 0., 0.], "pixelsPerUnit": 200.,
                         "optionalOverride": True})
    (output / "theme_contract.expected.json").write_text(
        json.dumps({"count": len(contract), "sprites": sorted(contract, key=lambda s: s["name"])}, indent=2) + "\n",
        encoding="utf-8", newline="\n")


if __name__ == "__main__":
    build()
    print(f"Staged four tier frames, three icons, editable SVG files and the expected contract rows in {STAGING}")
