"""W20X item 3 (THEME_SPEC 3.1): the text that the non-play screens draw straight on the table, against a style's backdrop.

tools/art_v11/table_text.json lists every text that a non-play screen draws with no opaque interface surface behind it.
It is written by the PlayMode test PLAY_W20X_03_TableTextOnNonPlayScreens: the screen, the object path, the ink rectangle
at 1920x1080 in design units (x, y from the top left, width, height), the palette key of the text colour, the colour each
style actually drew it in and any see-through layer (a modal's scrim) between the table and the text.

For one style this checker takes the picture the non-play screens draw on the table (W20X item 2): Table/page_scene.png
when the style ships one, else its Table/table_scene.png. The default style ships neither under Resources; it draws
Assets/Art/Art071/Backgrounds/bg_table_scene.png. It reports the worst WCAG 2 contrast of each text colour over every
pixel of the text's rectangle.

Two mappings of the picture onto the screen:
  authored  (default) the picture laid over the 1920x1080 design, pixel for pixel (a picture of another size is scaled
            to it), which is how THEME_SPEC 3 defines it;
  drawn     what the felt shader (Art07Felt) drew before W20Xb: it fits the picture by the aspect of the imported
            texture, and the theme importer then scaled a 1920x1080 picture to 2048x1024 (nPOTScale ToNearest), so the
            picture was squeezed to 960 rows with a band of felt x 0.24 above and below. Since W20Xb (6904b9c5) theme
            pictures import at their native size and the game draws the authored mapping; drawn is kept only to compare
            with captures taken before that fix.
The felt shader also adds a 0.6 % grain and, without reduced motion, a slow light sweep of at most +4.5 %; neither is
modelled.

Usage:
  python tools/art_v11/table_text_contrast.py <theme> [--mapping authored|drawn] [--strict] [--list PATH] [--json OUT]

Exit code 0 when the check ran; entries under 4.5:1 are printed as BELOW. With --strict the exit code is 1 when any entry
is under 4.5:1. Exit code 2 for a broken list or a missing picture.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "DicePrototype" / "Assets"
THEMES = ASSETS / "Resources" / "Themes"
DEFAULT_TABLE = ASSETS / "Art" / "Art071" / "Backgrounds" / "bg_table_scene.png"
LIST = Path(__file__).resolve().parent / "table_text.json"
SCHEMA = "w20x-table-text-1"
MINIMUM = 4.5
DESIGN = (1920, 1080)
DIM = 0.24  # Art07Felt: outside the fitted picture the felt draws _BaseColor x 0.24 (in linear light)


class ListError(Exception):
    """The table-text list or a style file is unusable."""


def parse_hex(value: str) -> tuple[float, float, float, float]:
    """RRGGBB or RRGGBBAA (an optional leading '#') as sRGB components in 0..1."""
    s = value.strip().lstrip("#")
    if len(s) not in (6, 8):
        raise ListError(f"not a colour: {value!r}")
    try:
        parts = [int(s[i:i + 2], 16) / 255.0 for i in range(0, len(s), 2)]
    except ValueError as e:
        raise ListError(f"not a colour: {value!r}") from e
    if len(parts) == 3:
        parts.append(1.0)
    return tuple(parts)  # type: ignore[return-value]


def _linear(c):
    c = np.asarray(c, dtype=np.float64)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def _srgb(c):
    c = np.asarray(c, dtype=np.float64)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(np.maximum(c, 0), 1 / 2.4) - 0.055)


def luminance(rgb) -> np.ndarray:
    """WCAG 2 relative luminance of sRGB colours in 0..1 (last axis r, g, b)."""
    lin = _linear(rgb)
    return 0.2126 * lin[..., 0] + 0.7152 * lin[..., 1] + 0.0722 * lin[..., 2]


def contrast(a, b) -> np.ndarray:
    """WCAG 2 contrast ratio, 1 to 21, of two sRGB colours (or arrays of them)."""
    la, lb = luminance(a), luminance(b)
    return (np.maximum(la, lb) + 0.05) / (np.minimum(la, lb) + 0.05)


def blend(top, alpha: float, bottom) -> np.ndarray:
    """A see-through sRGB colour over sRGB pixels, mixed in linear light as the canvas of this linear-colour-space
    project does; the result is sRGB again."""
    if alpha >= 1.0:
        return np.broadcast_to(np.asarray(top, dtype=np.float64), np.shape(bottom)).copy()
    return _srgb(alpha * _linear(top) + (1 - alpha) * _linear(bottom))


def backdrop_path(theme: str, themes_root: Path = THEMES, default_table: Path = DEFAULT_TABLE) -> Path:
    """The picture a style's non-play screens draw on the table (GameApp.Theme11, W20X item 2)."""
    if theme == "default":
        return default_table
    folder = Path(themes_root) / theme / "Table"
    for name in ("page_scene.png", "table_scene.png"):
        if (folder / name).exists():
            return folder / name
    raise ListError(f"style {theme} has neither Table/page_scene.png nor Table/table_scene.png")


def theme_palette(theme: str, defaults: dict[str, str], themes_root: Path = THEMES) -> dict[str, str]:
    """The style's palette: the default values (ThemePalette) with the style's theme.json overrides."""
    palette = dict(defaults)
    if theme == "default":
        return palette
    path = Path(themes_root) / theme / "theme.json"
    if not path.exists():
        raise ListError(f"style {theme} has no theme.json")
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    for key, value in (data.get("palette") or {}).items():
        if key in palette and isinstance(value, str):
            parse_hex(value)
            palette[key] = value
    return palette


def load_list(path: Path = LIST) -> dict:
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    if data.get("schema") != SCHEMA:
        raise ListError(f"{path}: schema {data.get('schema')!r}, expected {SCHEMA!r}")
    if not isinstance(data.get("palette"), dict) or not data["palette"]:
        raise ListError(f"{path}: no default palette")
    entries = data.get("entries")
    if not isinstance(entries, list):
        raise ListError(f"{path}: no entries")
    for i, e in enumerate(entries):
        for field in ("screen", "name", "rect", "colorKey"):
            if field not in e:
                raise ListError(f"{path}: entry {i} has no {field}")
        r = e["rect"]
        if len(r) != 4 or r[2] <= 0 or r[3] <= 0:
            raise ListError(f"{path}: entry {i} rect {r} is not x, y, width, height")
    return data


def text_colour(entry: dict, theme: str, palette: dict[str, str]) -> tuple[tuple[float, float, float, float], str]:
    """The colour the style drew the text in when the list recorded it, else the palette key's value."""
    recorded = (entry.get("colors") or {}).get(theme)
    if recorded:
        return parse_hex(recorded), "drawn"
    key = entry["colorKey"]
    if key not in palette:
        raise ListError(f"{entry['screen']} {entry['name']}: colour key {key!r} is not a palette key and {theme} was not recorded")
    return parse_hex(palette[key]), "palette"


def imported_size(png: Path) -> tuple[int, int]:
    """The texture size after Unity's import: the picture's own size, or powers of two when its .meta scales
    non-power-of-two textures (nPOTScale 1 nearest, 2 larger, 3 smaller)."""
    with Image.open(png) as im:
        w, h = im.size
    meta = png.with_name(png.name + ".meta")
    mode = 0
    if meta.exists():
        m = re.search(r"^\s*nPOTScale:\s*(\d+)\s*$", meta.read_text(encoding="utf-8", errors="replace"), re.M)
        mode = int(m.group(1)) if m else 0

    def pot(n: int) -> int:
        lo = 1 << (n.bit_length() - 1)
        hi = lo if lo == n else lo << 1
        return {1: lo if n - lo < hi - n else hi, 2: hi, 3: lo}.get(mode, n)

    return (pot(w), pot(h)) if mode in (1, 2, 3) else (w, h)


def _bilinear(rgb: np.ndarray, x: np.ndarray, y: np.ndarray) -> np.ndarray:
    """Bilinear samples of an image (clamped edges) at pixel-centre coordinates x, y."""
    h, w = rgb.shape[:2]
    x = np.clip(x, 0, w - 1)
    y = np.clip(y, 0, h - 1)
    x0, y0 = np.floor(x).astype(int), np.floor(y).astype(int)
    x1, y1 = np.minimum(x0 + 1, w - 1), np.minimum(y0 + 1, h - 1)
    fx, fy = (x - x0)[..., None], (y - y0)[..., None]
    top = rgb[y0, x0] * (1 - fx) + rgb[y0, x1] * fx
    bottom = rgb[y1, x0] * (1 - fx) + rgb[y1, x1] * fx
    return top * (1 - fy) + bottom * fy


def load_backdrop(path: Path, mapping: str = "authored", felt: str = "173E40") -> np.ndarray:
    """The table as the 1920x1080 screen shows it (sRGB in 0..1, rows from the top)."""
    with Image.open(path) as im:
        src = np.asarray(im.convert("RGB"), dtype=np.float64) / 255.0
    sh, sw = src.shape[:2]
    if mapping == "authored":
        if (sw, sh) == DESIGN:
            return src
        if abs(sw / sh - DESIGN[0] / DESIGN[1]) > 0.01:
            raise ListError(f"{path}: {sw}x{sh} is not 16:9")
        ys, xs = np.mgrid[0:DESIGN[1], 0:DESIGN[0]]
        return _bilinear(src, (xs + 0.5) * sw / DESIGN[0] - 0.5, (ys + 0.5) * sh / DESIGN[1] - 0.5)
    if mapping != "drawn":
        raise ListError(f"unknown mapping {mapping!r}")
    # Art07Felt: tableUV = (uv - 0.5) * fit + 0.5 with fit from the screen's and the imported texture's aspects.
    iw, ih = imported_size(path)
    aspect, authored = DESIGN[0] / DESIGN[1], iw / ih
    fx, fy = max(1.0, aspect / authored), max(1.0, authored / aspect)
    ys, xs = np.mgrid[0:DESIGN[1], 0:DESIGN[0]]
    u = ((xs + 0.5) / DESIGN[0] - 0.5) * fx + 0.5
    v = ((ys + 0.5) / DESIGN[1] - 0.5) * fy + 0.5  # from the top; the shader's v runs from the bottom, symmetric here
    inside = (u >= 0) & (u <= 1) & (v >= 0) & (v <= 1)
    out = _bilinear(src, u * sw - 0.5, v * sh - 0.5)
    dim = _srgb(_linear(np.asarray(parse_hex(felt)[:3])) * DIM)
    out[~inside] = dim
    return out


def worst_contrast(image: np.ndarray, rect, colour, veils=()) -> tuple[float, float, int]:
    """Worst contrast of a text colour over every pixel of its rectangle (design units at 1920x1080 = picture pixels).
    See-through layers between the table and the text (a modal's scrim: colour and alpha) are blended over the table
    first, then a see-through text colour over that. Returns (worst, share of pixels under 4.5, pixels)."""
    h, w = image.shape[:2]
    x0, y0 = max(0, int(np.floor(rect[0]))), max(0, int(np.floor(rect[1])))
    x1, y1 = min(w, int(np.ceil(rect[0] + rect[2]))), min(h, int(np.ceil(rect[1] + rect[3])))
    if x1 <= x0 or y1 <= y0:
        raise ListError(f"rect {rect} lies outside the {w}x{h} picture")
    pixels = image[y0:y1, x0:x1, :3].reshape(-1, 3)
    for veil in veils:
        r, g, b, a = parse_hex(veil)
        pixels = blend((r, g, b), a, pixels)
    ink = blend(colour[:3], colour[3], pixels)
    ratios = contrast(ink, pixels)
    return float(ratios.min()), float((ratios < MINIMUM).mean()), int(pixels.shape[0])


def check(theme: str, list_path: Path = LIST, themes_root: Path = THEMES, default_table: Path = DEFAULT_TABLE,
          mapping: str = "authored") -> dict:
    data = load_list(list_path)
    palette = theme_palette(theme, data["palette"], themes_root)
    backdrop = backdrop_path(theme, themes_root, default_table)
    image = load_backdrop(backdrop, mapping, palette.get("felt", "173E40"))
    rows = []
    captured = data.get("themes") or []
    for e in data["entries"]:
        # A text the capture found on a surface in this style (not on the table) is not this style's concern.
        if theme in captured and e.get("themes") and theme not in e["themes"]:
            continue
        colour, source = text_colour(e, theme, palette)
        worst, share, count = worst_contrast(image, e["rect"], colour, e.get("veils") or ())
        rows.append({"screen": e["screen"], "name": e["name"], "text": e.get("text", ""), "rect": e["rect"], "colorKey": e["colorKey"],
                     "colour": "".join(f"{round(c * 255):02X}" for c in colour[:3]), "colourSource": source,
                     "alpha": round(colour[3], 3), "veils": e.get("veils") or [], "worst": round(worst, 2),
                     "shareBelow": round(share, 4), "pixels": count, "below": worst < MINIMUM})
    try:
        shown = backdrop.relative_to(ROOT).as_posix()
    except ValueError:
        shown = str(backdrop)
    return {"theme": theme, "mapping": mapping, "backdrop": shown, "entries": len(rows),
            "below": sum(r["below"] for r in rows), "rows": rows}


def report(result: dict) -> str:
    lines = [f"style {result['theme']}: backdrop {result['backdrop']} ({result['mapping']} mapping), "
             f"{result['entries']} texts on the table, {result['below']} under {MINIMUM}:1",
             "| verdict | worst | under 4.5 | screen | text | rect (x, y, w, h) | key | colour |",
             "| --- | --- | --- | --- | --- | --- | --- | --- |"]
    for r in sorted(result["rows"], key=lambda r: (r["worst"], r["screen"], r["name"])):
        rect = ", ".join(f"{v:g}" for v in r["rect"])
        veil = f" under {', '.join(r['veils'])}" if r["veils"] else ""
        lines.append(f"| {'BELOW' if r['below'] else 'ok'} | {r['worst']:.2f} | {r['shareBelow'] * 100:.1f} % | {r['screen']} | "
                     f"{r['name']} {r['text']!r} | {rect} | {r['colorKey']} | {r['colour']} ({r['colourSource']}){veil} |")
    return "\n".join(lines)


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("theme")
    ap.add_argument("--mapping", choices=("authored", "drawn"), default="authored")
    ap.add_argument("--strict", action="store_true", help="exit 1 when any text is under 4.5:1")
    ap.add_argument("--list", type=Path, default=LIST)
    ap.add_argument("--json", type=Path, help="also write the result as JSON")
    args = ap.parse_args(argv)
    try:
        result = check(args.theme, args.list, THEMES, DEFAULT_TABLE, args.mapping)
    except (ListError, OSError, json.JSONDecodeError) as e:
        print("TABLE_TEXT_ERROR " + str(e))
        return 2
    print(report(result))
    if args.json:
        args.json.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("TABLE_TEXT_BELOW" if result["below"] else "TABLE_TEXT_OK")
    return 1 if args.strict and result["below"] else 0


if __name__ == "__main__":
    sys.exit(main())
