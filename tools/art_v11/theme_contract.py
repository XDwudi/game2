"""V1.1 theme asset contract and audit (docs/specs/v11/THEME_SPEC.md, DEV_PLAN F-THEME-02).

The default theme's UI sprites are the GameApp.art07Templates array serialized in the Bootstrap scene
(UiKit.Template looks names up there, first entry wins). A theme overrides a sprite by shipping a PNG
with the same name under Assets/Resources/Themes/<id>/UI/. The importer (Editor/V11ThemeImport.cs)
copies the default sprite's pixels-per-unit and nine-slice border, so a theme sprite must have the
default sprite's pixel size; its border band must hold the frame and its centre must stretch cleanly.

Usage:
  python tools/art_v11/theme_contract.py contract            # write tools/art_v11/theme_contract.json and print a table
  python tools/art_v11/theme_contract.py audit <theme-id>    # check a theme folder against the contract
  python tools/art_v11/theme_contract.py audit <theme-id> --strict   # also fail on missing referenced sprites

The audit also checks the optional Table/page_scene.png (THEME_SPEC 3, W20X: the non-play screens' backdrop) when a
theme ships it: exactly 1920x1080, RGB(A). Themes without it audit exactly as before.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "DicePrototype" / "Assets"
SCENE = ASSETS / "Game" / "Scenes" / "Bootstrap.unity"
CODE_DIRS = [ASSETS / "Game" / "UI", ASSETS / "Game" / "Runtime"]
THEMES = ASSETS / "Resources" / "Themes"
CONTRACT = Path(__file__).resolve().parent / "theme_contract.json"

GUID_LINE = re.compile(r"^\s*-\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-f]{32}),\s*type:\s*\d+\}")
BORDER = re.compile(r"spriteBorder:\s*\{x:\s*([-\d.]+),\s*y:\s*([-\d.]+),\s*z:\s*([-\d.]+),\s*w:\s*([-\d.]+)\}")
PPU = re.compile(r"spritePixelsToUnits:\s*([-\d.]+)")


def template_guids() -> list[str]:
    lines = SCENE.read_text(encoding="utf-8").splitlines()
    out: list[str] = []
    inside = False
    for line in lines:
        if line.strip() == "art07Templates:":
            inside = True
            continue
        if inside:
            m = GUID_LINE.match(line)
            if not m:
                break
            out.append(m.group(1))
    if not out:
        raise SystemExit("art07Templates not found in " + str(SCENE))
    return out


def guid_index() -> dict[str, Path]:
    index: dict[str, Path] = {}
    for meta in ASSETS.rglob("*.png.meta"):
        try:
            head = meta.read_text(encoding="utf-8", errors="replace").splitlines()[:3]
        except OSError:
            continue
        for line in head:
            if line.startswith("guid:"):
                index[line.split(":", 1)[1].strip()] = meta.with_suffix("")
                break
    return index


def code_text() -> str:
    parts = []
    for folder in CODE_DIRS:
        for path in folder.rglob("*.cs"):
            if "Validation~" in path.parts:
                continue
            parts.append(path.read_text(encoding="utf-8", errors="replace"))
    return "\n".join(parts)


def computed_prefixes(code: str) -> set[str]:
    """Prefixes the code concatenates into sprite names, e.g. "badge_enchant_"+id."""
    return set(re.findall(r'"([a-z0-9]+(?:_[a-z0-9]+)*_)"\s*\+', code))


# SETTLEMENT_LAYOUT §8.1: these seven white masks take the theme palette at runtime.
OPTIONAL_UI_OVERRIDES = {f"ui_emblem_tier{tier}" for tier in range(1, 5)} | {
    "ui_icon_stage", "ui_icon_submit", "ui_icon_interest",
}


def build_contract() -> dict:
    index = guid_index()
    code = code_text()
    prefixes = computed_prefixes(code)
    seen: dict[str, dict] = {}
    for guid in template_guids():
        path = index.get(guid)
        if path is None or not path.exists():
            continue
        name = path.stem
        if name in seen:  # UiKit.Template: the first entry with a name wins
            continue
        meta = path.with_name(path.name + ".meta").read_text(encoding="utf-8", errors="replace")
        b = BORDER.search(meta)
        p = PPU.search(meta)
        with Image.open(path) as im:
            size = list(im.size)
        seen[name] = {
            "name": name,
            "default": path.relative_to(ROOT).as_posix(),
            "size": size,
            "border": [float(x) for x in b.groups()] if b else [0.0, 0.0, 0.0, 0.0],
            "pixelsPerUnit": float(p.group(1)) if p else 100.0,
            "referenced": f'"{name}"' in code or any(name.startswith(p) for p in prefixes),
        }
        if name in OPTIONAL_UI_OVERRIDES:
            seen[name]["optionalOverride"] = True
    return {"scene": SCENE.relative_to(ROOT).as_posix(), "count": len(seen),
            "sprites": sorted(seen.values(), key=lambda s: s["name"])}


def cmd_contract() -> int:
    data = build_contract()
    CONTRACT.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"{data['count']} default UI sprites ({sum(s['referenced'] for s in data['sprites'])} referenced by name in code)")
    print("| name | size | border (l,b,r,t) | ppu | referenced |")
    print("| --- | --- | --- | --- | --- |")
    for s in data["sprites"]:
        l, b, r, t = (int(v) for v in s["border"])
        print(f"| {s['name']} | {s['size'][0]}x{s['size'][1]} | {l},{b},{r},{t} | {int(s['pixelsPerUnit'])} | {'yes' if s['referenced'] else ''} |")
    return 0


# THEME_SPEC 3: optional table files the audit checks when a theme ships them (W20X: the non-play screens' backdrop).
OPTIONAL_TABLE = {"page_scene": (1920, 1080)}


def cmd_audit(theme: str, strict: bool, themes_root: Path = THEMES) -> int:
    data = json.loads(CONTRACT.read_text(encoding="utf-8")) if CONTRACT.exists() else build_contract()
    sprites = {s["name"]: s for s in data["sprites"]}
    folder = Path(themes_root) / theme / "UI"
    shipped = {p.stem: p for p in folder.glob("*.png")} if folder.exists() else {}
    problems: list[str] = []
    for name, path in sorted(shipped.items()):
        if name not in sprites:
            problems.append(f"EXTRA {name}: not a default template name, UiKit will never use it")
            continue
        with Image.open(path) as im:
            if list(im.size) != sprites[name]["size"]:
                problems.append(f"SIZE {name}: {im.size[0]}x{im.size[1]}, default is {sprites[name]['size'][0]}x{sprites[name]['size'][1]}")
            if im.mode not in ("RGBA", "LA"):
                problems.append(f"ALPHA {name}: mode {im.mode}, expected RGBA")
    optional: list[str] = []
    for name, size in OPTIONAL_TABLE.items():
        path = Path(themes_root) / theme / "Table" / (name + ".png")
        if not path.exists():
            continue
        with Image.open(path) as im:
            if im.size != size:
                problems.append(f"SIZE Table/{name}: {im.size[0]}x{im.size[1]}, expected {size[0]}x{size[1]}")
            elif im.mode not in ("RGB", "RGBA"):
                problems.append(f"MODE Table/{name}: mode {im.mode}, expected RGB")
            else:
                optional.append(f"Table/{name} {size[0]}x{size[1]}")
    missing = [n for n, s in sprites.items() if s["referenced"] and not s.get("optionalOverride") and n not in shipped]
    optional_fallback = [n for n, s in sprites.items() if s.get("optionalOverride") and n not in shipped]
    manifest = folder.parent / "theme.json"
    if manifest.exists():
        fonts = json.loads(manifest.read_text(encoding="utf-8")).get("fonts")
        if fonts is not None:
            if not isinstance(fonts, dict) or fonts.get("title") != "Fonts/title":
                problems.append("FONT: expected title resource Fonts/title")
            elif not (folder.parent / "Fonts" / "title.asset").is_file():
                missing.append("Fonts/title")
    print(f"theme {theme}: {len(shipped)} sprites shipped, {len(missing)} referenced defaults not overridden")
    for n in optional_fallback:
        print("  OPTIONAL DEFAULT " + n)
    for item in optional:
        print("  OPTIONAL " + item)
    for p in problems:
        print("  " + p)
    for n in missing:
        print("  FALLBACK " + n)
    failed = bool(problems) or (strict and bool(missing))
    print("AUDIT_FAILED" if failed else "AUDIT_OK")
    return 1 if failed else 0


def main() -> int:
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("contract")
    a = sub.add_parser("audit")
    a.add_argument("theme")
    a.add_argument("--strict", action="store_true")
    args = ap.parse_args()
    return cmd_contract() if args.cmd == "contract" else cmd_audit(args.theme, args.strict)


if __name__ == "__main__":
    sys.exit(main())
