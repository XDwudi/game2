"""Build the approved W18 default play surface and the default/Swiss chapter tone ramps."""

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "DicePrototype/Assets"
OUTPUT = ASSETS / "Resources/ChapterTables18"
DEFAULT_TABLE = ASSETS / "Art/Art072/Backgrounds/bg_table_scene.png"
SWISS_TABLE = ASSETS / "Resources/Themes/swiss/Table/table_scene.png"
QUIET = ((52, 374, 1408, 276), (52, 660, 1408, 60),
         (344, 740, 1136, 202), (32, 740, 360, 202))
RGB_XYZ = np.array(((.4124564, .3575761, .1804375),
                    (.2126729, .7151522, .0721750),
                    (.0193339, .1191920, .9503041)))
D65 = np.array((.95047, 1.0, 1.08883))


def chapter_colors(color, total_lightness_change):
    rgb = np.array(color) / 255.0
    linear = np.where(rgb <= .04045, rgb / 12.92, ((rgb + .055) / 1.055) ** 2.4)
    xyz = (RGB_XYZ @ linear) / D65
    eps = 6 / 29
    f = np.where(xyz > eps**3, np.cbrt(xyz), xyz / (3 * eps**2) + 4 / 29)
    result = []
    for chapter in range(8):
        fy = f[1] + total_lightness_change * chapter / (7 * 116)
        # Keep hue and a*, b* until the target leaves the display gamut. If it does,
        # keep lightness and hue and choose the largest chroma that still fits.
        def rgb_at_chroma(amount):
            lab_f = fy + (f - f[1]) * amount
            xyz = D65 * np.where(lab_f > eps, lab_f**3, 3 * eps**2 * (lab_f - 4 / 29))
            return np.linalg.solve(RGB_XYZ, xyz)

        target = rgb_at_chroma(1)
        if np.min(target) < -1e-8 or np.max(target) > 1 + 1e-8:
            low, high = 0.0, 1.0
            for _ in range(32):
                mid = (low + high) / 2
                candidate = rgb_at_chroma(mid)
                if np.all((candidate >= 0) & (candidate <= 1)):
                    low = mid
                else:
                    high = mid
            target = rgb_at_chroma(low)
        target = np.clip(target, 0, 1)
        encoded = np.where(target <= .0031308, 12.92 * target, 1.055 * target**(1/2.4) - .055)
        result.append("".join(f"{v:02X}" for v in np.rint(encoded * 255).astype(int)))
    return result


def smoothstep(values):
    t = np.clip(values, 0, 1)
    return t * t * (3 - 2 * t)


def default_play(source, base):
    height, width = source.shape[:2]
    y, x = np.ogrid[:height, :width]
    blend = np.zeros((height, width), dtype=np.float32)
    for left, top, w, h in QUIET:
        dx = np.maximum(0, np.maximum(left-x, x-(left+w-1)))
        dy = np.maximum(0, np.maximum(top-y, y-(top+h-1)))
        blend = np.maximum(blend, 1-smoothstep((np.hypot(dx, dy)-6)/74))
    # The v0.72 generator starts its wood rail 20 px from the edge. Its shadow blends
    # into the felt from there; the first quiet rectangle reaches x=32.
    rim = np.minimum(np.minimum(x, width-1-x), np.minimum(y, height-1-y))
    blend *= smoothstep((rim-20)/12)
    return np.rint(source + (np.asarray(base)-source) * blend[...,None]).astype(np.uint8)


def check_surface(rgb, expected):
    if rgb.shape != (1080, 1920, 3):
        raise ValueError(f"Unexpected table shape {rgb.shape}")
    for left, top, width, height in QUIET:
        region = rgb[top:top+height, left:left+width]
        if not np.all(region == np.array(expected)):
            raise ValueError(f"Nonuniform or incorrect quiet area {left},{top},{width},{height}")


def build(destination):
    original = np.asarray(Image.open(DEFAULT_TABLE).convert("RGB"))
    base = (23, 62, 64)
    swiss = (249, 234, 219)
    surface = default_play(original, base)
    check_surface(surface, base)
    check_surface(np.asarray(Image.open(SWISS_TABLE).convert("RGB")), swiss)
    for old, new in ((original[:21], surface[:21]), (original[-21:], surface[-21:]),
                     (original[:,:21], surface[:,:21]), (original[:,-21:], surface[:,-21:])):
        if not np.array_equal(old, new):
            raise ValueError("The original rail was altered")

    themes = [{"id": name, "colors": chapter_colors(color, delta)}
              for name, color, delta in (("default", base, -7), ("swiss", swiss, -9))]
    for theme in themes:
        if len(set(theme["colors"])) != 8:
            raise ValueError(f'{theme["id"]} has repeated chapter colours')
    destination.mkdir(parents=True, exist_ok=True)
    Image.fromarray(surface).save(destination / "default_play.png", compress_level=9)
    (destination / "tones.json").write_text(json.dumps({"themes": themes}, indent=2) + "\n", encoding="utf-8")
    print(f"W18_DEFAULT_SWISS_BUILT size=1920x1080 quiet=4/4 themes=2 chapters=8 output={destination}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", type=Path, default=OUTPUT)
    build(parser.parse_args().out)
