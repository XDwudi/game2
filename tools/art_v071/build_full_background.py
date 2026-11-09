"""Author the v071 felt as a deterministic, periodic 1024px textile.

Creates new pixels from editable parameters, not an upscaled legacy image.
Broad illumination is deliberately left to the shared engine material.
"""
from pathlib import Path
import hashlib
import json
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / 'ArtSource/Art071/Full/Backgrounds'
SIZE = 1024
SEED = 7101
BASE = np.array([23.0, 62.0, 64.0])


def author_felt():
    rng = np.random.default_rng(SEED)
    t = np.linspace(0, 2 * np.pi, SIZE, endpoint=True)
    x, y = np.meshgrid(t, t)
    # Periodic multiscale fiber bundles: no conspicuous grid, grain, or printed UI.
    fibers = np.zeros((SIZE, SIZE))
    for frequency, amplitude, count in ((6, .50, 12), (24, .36, 18), (86, .20, 24)):
        for _ in range(count):
            angle = rng.uniform(0, 2 * np.pi)
            fx = int(round(frequency * np.cos(angle)))
            fy = int(round(frequency * np.sin(angle)))
            fibers += amplitude / np.sqrt(count) * np.sin(fx * x + fy * y + rng.uniform(0, 2 * np.pi))
    # Fine two-direction threads with softly varying spacing, low contrast.
    warp = np.sin(170 * x + .7 * np.sin(13 * y))
    weft = np.sin(163 * y + .6 * np.sin(11 * x))
    weave = .22 * warp + .18 * weft + .08 * warp * weft
    luminance = np.clip(fibers * 1.5 + weave, -3.8, 3.8)
    rgb = np.clip(np.rint(BASE + luminance[..., None] * np.array([.62, 1, 1])), 0, 255).astype(np.uint8)
    rgb[-1] = rgb[0]
    rgb[:, -1] = rgb[:, 0]
    return rgb


def main():
    DEST.mkdir(parents=True, exist_ok=True)
    pixels = author_felt()
    target = DEST / 'bg_felt_tile.png'
    Image.fromarray(pixels, 'RGB').save(target)
    row = {
        'id': 'bg_felt_tile', 'source': target.relative_to(ROOT).as_posix(),
        'sha256': hashlib.sha256(target.read_bytes()).hexdigest(), 'dimensions': [SIZE, SIZE],
        'mode': 'RGB', 'editableSource': Path(__file__).relative_to(ROOT).as_posix(),
        'scriptSha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        'method': 'periodic Fourier fiber bundles with subtle deterministic warp/weft',
        'seed': SEED, 'baseColor': '#173E40', 'edgePixelMismatch': int(np.count_nonzero(pixels[0] != pixels[-1]) + np.count_nonzero(pixels[:, 0] != pixels[:, -1])),
        'filter': 'Bilinear', 'wrap': 'Repeat', 'mipmaps': True,
        'lighting': 'separate shared shader; no baked vignette, text, icons, or buttons',
        'status': 'authored; engine visual review pending',
    }
    (DEST / 'manifest.json').write_text(json.dumps(row, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(row, ensure_ascii=False))


if __name__ == '__main__':
    main()
