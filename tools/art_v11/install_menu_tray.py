"""Fit a verified menu-tray illustration into its transparent 1060 by 552 design rectangle."""
import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
FRAME = (1060, 552)


def install(theme, revision):
    source_dir = ROOT / "ArtSource/Art11" / theme / "menu" / revision
    record = json.loads((source_dir / "record.json").read_text(encoding="utf8"))
    source = source_dir / "original.png"
    digest = hashlib.sha256(source.read_bytes()).hexdigest()
    if not record["runValid"] or record["output"]["sha256"] != digest:
        raise ValueError("A verified artgen revision matching the archived PNG is required.")
    with Image.open(source) as image:
        if image.mode != "RGBA":
            raise ValueError("Menu tray must have a transparent alpha channel.")
        bounds = image.getchannel("A").point(lambda value: 255 if value >= 8 else 0).getbbox()
        if not bounds:
            raise ValueError("The tray is entirely transparent.")
        left, top, right, bottom = bounds
        # Keep the antialiased outer lip and its short contact shadow.
        crop = (max(0,left-4), max(0,top-4), min(image.width,right+4), min(image.height,bottom+4))
        tray = image.crop(crop)
        scale = min(FRAME[0] / tray.width, FRAME[1] / tray.height)
        if scale > 1:
            raise ValueError("The tray source is too small for its maximum display size.")
        size = tuple(round(value * scale) for value in tray.size)
        tray = tray.resize(size, Image.Resampling.LANCZOS)
    offset = tuple((outer - inner) // 2 for outer, inner in zip(FRAME, size))
    result = Image.new("RGBA",FRAME)
    result.paste(tray,offset)
    target = ROOT / "DicePrototype/Assets/Resources/MenuArt17" / (theme+".png")
    target.parent.mkdir(parents=True,exist_ok=True)
    result.save(target)
    entry = {"revision":revision,"sourceSha256":digest,"alphaCrop":crop,"frame":FRAME,"size":size,
             "offset":offset,"asset":target.relative_to(ROOT).as_posix(),
             "sha256":hashlib.sha256(target.read_bytes()).hexdigest()}
    (source_dir.parent / "install.json").write_bytes((json.dumps(entry,indent=2)+"\n").encode("utf8"))


if __name__ == "__main__":
    cli = argparse.ArgumentParser(description=__doc__)
    cli.add_argument("theme", choices=["default","swiss","sancai","comic","deco","glass"])
    cli.add_argument("revision", type=lambda value: value if value.startswith("r") and value[1:].isdigit()
                     else cli.error("revision must be an rNN directory"))
    args = cli.parse_args()
    install(args.theme,args.revision)
