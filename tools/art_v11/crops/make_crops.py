"""V1.1 W13a: square icon crops for the compact card size (report: docs/specs/v11/W13A_CROPS.md).

The compact card shows a theme's card illustration as a square icon of about 40-56 px at 1080p and 27-37 px at
720p. The approved art plan wants a cropped icon version there, not the whole illustration shrunk. The game cuts
that square out of the existing texture at run time (Sprite.Create). This script only finds the square: it writes a
crop table and, on request, review sheets. It never writes images into the game.

    python tools/art_v11/crops/make_crops.py --theme default \
        --source DicePrototype/Assets/Art/Art071/Illustrations \
        --out DicePrototype/Assets/Resources/IconCrops/default.json --sheet-dir artifacts/cache/v11/crops

Sources are illustration_<id>.png files with either lower- or upper-case ids; table keys are upper case.

Units (repeated in the table as "units"). W x H is the source texture's pixel size:
    x = left / W, y = top / H (measured from the top), size = side / min(W, H).
    Unity rect, origin bottom-left: side = size * min(W, H); new Rect(x * W, H - y * H - side, side, side).
For a square source this is just x, y and size times the side. A crop whose source size differs from the table's
"imageSize" carries its own "imageSize" (some default-theme sources are 1536x1024, 1312x1199 or 1774x887).

Method "alpha-core-1". All lengths are fractions of the source's short side S, so any resolution works.
 1. Analyse alpha at a 512 px short side; opaque = alpha >= 0.5.
 2. Core: opening with a disk of radius OPEN_RADIUS. Action strokes, sparkles, halftone dots, dashed paths, strings
    and swooshes are thinner than that disk and leave little or no core.
 3. Kept parts: connected opaque parts whose core is >= CORE_MIN_RATIO of the largest part's core and, when the
    theme draws ink outlines (the largest part's rim is dark), whose own rim is >= INK_MIN dark ink. Flat-colour
    action strokes have no ink rim. A kept part keeps its whole silhouette, thin bits included.
 4. Full-subject square: the smallest square holding CORE_COVER of the kept core and SIL_COVER of the kept
    silhouette (thin light protrusions such as strings or stroke tips may fall outside).
 5. Focus square: the smallest square, not below FOCUS_MIN of the full-subject square, that still holds FOCUS_CORE
    of the kept core. It may only shave bases, stands and handles, because these stay protected:
      - every separate kept part (a loose die, coins beside a box) at PART_KEEP;
      - the upper TOP_KEEP of the subject's height at TOP_COVER (flags, coins, crowns, flames, dice on top);
      - every cream island (light, unsaturated area: die faces, paper, porcelain) of at least ISLAND_MIN, as the
        core inside its bounding box grown by ISLAND_HALO, at ISLAND_KEEP; this keeps whole dice, shaded sides too;
      - every thick sub-object (core blob other than the largest) is kept whole or dropped whole, never sliced.
    Equal squares: the one holding most core, then the one nearest the core centroid, then the top-left one.
 6. Crop: the focus square grown so MARGIN of the crop side is left on each side, clamped inside the image, at
    least MIN_SIZE.
 7. A subject wider (or taller) than the largest square that fits is cut. A mirror-symmetric subject (silhouette
    IoU with its mirror >= MIRROR_SYMMETRIC: fan, balance, two rollers) gets a centred square; otherwise the square
    goes where the islands it holds whole (die faces) are largest, then where it holds the most core.
 8. OVERRIDES, a short per-theme table of manual squares with written reasons, replaces steps 4-7 for its ids.

Confidence: the method's estimate that the square works as the card's compact icon without a person adjusting it.
It cannot see meaning; it measures the risks below. It is the product of these factors, and each factor below 1
writes its reason into the crop's "note":
    zoom    1.0 while the crop side is <= ZOOM_OK of the image's long side, 0.7 at 1.0 (crop ~ full image).
    cut     1.0 while <= CUT_OK of the kept core is outside the crop, 0.4 at 30%; x WIDE_FACTOR when the subject is
            wider than the largest square (a forced cut, not one chosen from the subject's layout).
    fill    1.0 when the kept silhouette covers >= 30% of the crop, 0.6 at <= 12% (a thin subject reads as lines).
    split   0.85 for two, 0.75 for three or more subject groups (parts SPLIT_GAP apart, each >= 15% of the core).
    detail  1.0 up to DETAIL_OK fine dark marks (halftone dots, pips, small marks) in a 96 px rendering, 0.75 at
            DETAIL_BAD: most of them merge into grey at 32 px, so the icon is busy.
Crops below REVIEW_BELOW go to the side-by-side review sheet. An override is measured the same way.
"""
import os

# Single-threaded on purpose: other agents may be timing Unity tests on this machine.
for _name in ("OMP_NUM_THREADS", "MKL_NUM_THREADS", "OPENBLAS_NUM_THREADS", "NUMEXPR_NUM_THREADS"):
    os.environ.setdefault(_name, "1")

import argparse  # noqa: E402
from collections import Counter  # noqa: E402
import json  # noqa: E402
import math  # noqa: E402
from pathlib import Path  # noqa: E402
import re  # noqa: E402
import sys  # noqa: E402

import numpy as np  # noqa: E402
from PIL import Image, ImageDraw, ImageFont  # noqa: E402
from scipy import ndimage as ndi  # noqa: E402

ROOT = Path(__file__).resolve().parents[3]
SCHEMA = "icon-crops-1"
METHOD = "alpha-core-1"
GENERATED_BY = "tools/art_v11/crops/make_crops.py"
UNITS = ("x=left/W, y=top/H (from the top), size=side/min(W,H); W,H = source pixel size; "
         "Unity rect = (x*W, H-y*H-side, side, side) with side = size*min(W,H)")
ID_RE = re.compile(r"^illustration_([a-z]+\d+)\.png$", re.IGNORECASE)
PAPER = (0xF4, 0xE8, 0xCB)
GREY = (128, 128, 128)

WORK = 512                # analysis short side in pixels
ALPHA_CUT = 0.5           # opaque threshold
OPEN_RADIUS = 0.028       # opening disk radius (x S); parts thinner than about 5.6% of S leave no core
CORE_MIN_RATIO = 0.02     # a part is kept when its core is >= 2% of the largest part's core ...
INK_MIN = 0.30            # ... and >= 30% of its rim band is dark ink (only when the main part has an ink rim)
INK_BAND = 0.006          # rim band width (x S)
INK_LUMA = 0.36           # "dark ink" luma threshold (0..1)
CORE_COVER = 995          # per mille of the kept core mass inside the full-subject square
SIL_COVER = 970           # per mille of the kept silhouette mass inside the full-subject square
FOCUS_CORE = 880          # per mille of the kept core mass inside the focus square
FOCUS_MIN = 0.78          # the focus square is at least this share of the full-subject square
PART_KEEP = 900           # per mille of each separate kept part's core inside the focus square
TOP_KEEP = 0.40           # the upper 40% of the subject's height is protected from trimming ...
TOP_COVER = 990           # ... at this per mille of its core
BLOB_MIN = 0.005          # core blobs below this share of the core mass are ignored by the no-slicing rule
BLOB_WHOLE = 900          # a kept blob has at least this per mille inside the focus square ...
BLOB_GONE = 150           # ... and a dropped blob at most this per mille
ISLAND_LUMA = 0.70        # cream island: luma at least this ...
ISLAND_CHROMA = 0.30      # ... and chroma (max-min)/max at most this
ISLAND_MIN = 0.001        # islands smaller than this share of S^2 are ignored
ISLAND_KEEP = 950         # per mille of each island's protected region inside the focus square
ISLAND_HALO = 0.04        # protected region = core inside the island's bounding box grown by this (x S)
WIDE_ISLAND_WEIGHT = 4    # wide sources: whole-island pixels count this many times core pixels when placing
MIRROR_SYMMETRIC = 0.85   # wide sources: silhouette IoU with its mirror image at or above this -> centred square
MARGIN = 0.06             # border on each side, as a share of the crop side
MIN_SIZE = 0.40           # smallest crop side (x S)
ZOOM_OK = 0.85            # crops up to this size get no zoom penalty
CUT_OK = 0.12             # share of the kept core mass a crop may leave out without penalty
DETAIL_OK = 80            # fine dark marks at 96 px without penalty; DETAIL_BAD gets the lowest factor
DETAIL_BAD = 130
WIDE_FACTOR = 0.8         # confidence factor when the subject is wider than the largest square
SPLIT_GAP = 0.03          # parts closer than this (x S) belong to one subject group
SPLIT_MIN_SHARE = 0.15    # a subject group counts when it holds >= 15% of the kept core mass
REVIEW_BELOW = 0.80       # crops below this confidence go to the side-by-side review sheet
EIGHT = np.ones((3, 3), dtype=bool)

# Per-theme manual squares, in table units (x, y, size), each with the reason the automatic square was replaced.
# Keep this short (about 10 ids at most); a crop that no square can save needs an art edit instead.
OVERRIDES = {
    "default": {},
}


# ----------------------------------------------------------------------------------------------- source discovery

def id_sort_key(content_id):
    """Natural order: C2 before C10. Used for sheets and reports; the table itself is sorted as plain strings."""
    match = re.match(r"([A-Z]+)(\d+)$", content_id)
    return (match.group(1), int(match.group(2))) if match else (content_id, 0)


def discover(source):
    """Map upper-case content id -> illustration path. Rejects two files that only differ in case."""
    found = {}
    for path in sorted(Path(source).iterdir(), key=lambda p: p.name):
        match = ID_RE.match(path.name)
        if not match or not path.is_file():
            continue
        content_id = match.group(1).upper()
        if content_id in found:
            raise SystemExit(f"duplicate illustration id {content_id}: {found[content_id].name}, {path.name}")
        found[content_id] = path
    if not found:
        raise SystemExit(f"no illustration_<id>.png files in {source}")
    return dict(sorted(found.items(), key=lambda item: id_sort_key(item[0])))


def repo_relative(path):
    path = Path(path).resolve()
    try:
        return path.relative_to(ROOT).as_posix()
    except ValueError:
        return path.as_posix()


# ------------------------------------------------------------------------------------------------------- analysis

def load_rgba(path):
    with Image.open(path) as image:
        return image.convert("RGBA")


def opening(mask, radius):
    """Binary opening with a disk, through two distance transforms (exact Euclidean disk, fast)."""
    eroded = ndi.distance_transform_edt(mask) > radius
    if not eroded.any():
        return eroded
    return (ndi.distance_transform_edt(~eroded) <= radius) & mask


class Analysis:
    """Alpha analysis of one illustration at the working resolution (short side WORK)."""

    def __init__(self, image):
        self.W, self.H = image.size
        self.S = min(self.W, self.H)
        self.k = self.S / WORK                     # source pixels per working pixel
        ww, wh = max(1, round(self.W / self.k)), max(1, round(self.H / self.k))
        small = np.asarray(image.resize((ww, wh), Image.BOX), dtype=np.float32) / 255.0
        self.w, self.h = ww, wh
        alpha, rgb = small[..., 3], small[..., :3]
        self.mask = alpha >= ALPHA_CUT
        self.notes = []
        self.part_cores = []
        self.islands = []
        self.transparent = bool(self.mask.any() and not self.mask.all())
        if not self.transparent:
            # No usable transparency: the whole picture is the subject.
            self.notes.append("no transparent background; subject = whole image")
            self.core = np.ones((wh, ww), dtype=bool)
            self.sil = self.core.copy()
            return
        luma = 0.299 * rgb[..., 0] + 0.587 * rgb[..., 1] + 0.114 * rgb[..., 2]
        edt = ndi.distance_transform_edt(self.mask)
        core = opening(self.mask, OPEN_RADIUS * WORK)
        if not core.any():
            self.notes.append("no thick parts; core = whole opaque mask")
            core = self.mask.copy()
        labels, count = ndi.label(self.mask, structure=EIGHT)
        index = np.arange(1, count + 1)
        core_area = ndi.sum(core, labels, index)
        band = self.mask & (edt <= max(1.0, INK_BAND * WORK))
        rim = ndi.sum(band, labels, index)
        rim_dark = ndi.sum(band & (luma < INK_LUMA), labels, index)
        ink = rim_dark / np.maximum(rim, 1)
        main = int(np.argmax(core_area))           # first maximum: deterministic
        ink_theme = bool(ink[main] >= INK_MIN)
        keep = (core_area >= CORE_MIN_RATIO * core_area[main]) & ((ink >= INK_MIN) | (not ink_theme))
        keep[main] = True
        kept = np.isin(labels, index[keep])
        self.core = core & kept
        self.sil = kept
        # Cream islands: light, unsaturated areas inside the kept subject, enclosed by ink. Die faces are the most
        # common ones (also paper, porcelain, masks). A focus square keeps every island of a useful size whole.
        top = rgb.max(axis=-1)
        chroma = (top - rgb.min(axis=-1)) / np.maximum(top, 1e-6)
        light = kept & (luma >= ISLAND_LUMA) & (chroma <= ISLAND_CHROMA)
        island_labels, island_count = ndi.label(light)
        self.islands = []          # (island pixels, protected region: core inside the island's box plus a halo)
        if island_count:
            sizes = ndi.sum(light, island_labels, np.arange(1, island_count + 1))
            boxes = ndi.find_objects(island_labels)
            halo = int(round(ISLAND_HALO * WORK))
            for i in np.nonzero(sizes >= ISLAND_MIN * WORK * WORK)[0]:
                ys, xs = boxes[i]
                y0, y1 = max(0, ys.start - halo), min(wh, ys.stop + halo)
                x0, x1 = max(0, xs.start - halo), min(ww, xs.stop + halo)
                region = np.zeros_like(self.core)
                region[y0:y1, x0:x1] = self.core[y0:y1, x0:x1]
                self.islands.append((island_labels == i + 1, region))
        # Separate kept parts (loose dice, coins, a second object) may not be cut by a focus square.
        for i in range(count):
            if keep[i] and i != main:
                part = self.core & (labels == i + 1)
                if part.any():
                    self.part_cores.append(part)

    # --- square search on the working grid ---------------------------------------------------------------------

    @staticmethod
    def _integral(a):
        sat = np.zeros((a.shape[0] + 1, a.shape[1] + 1), dtype=np.int64)
        sat[1:, 1:] = np.cumsum(np.cumsum(a.astype(np.int64), axis=0), axis=1)
        return sat

    @staticmethod
    def _boxsums(sat, s):
        return sat[s:, s:] - sat[:-s, s:] - sat[s:, :-s] + sat[:-s, :-s]

    def _pick(self, ok, score, s, target):
        """Among feasible positions: highest score, then nearest to `target` (x, y), then top-left most."""
        ok = ok & (score == score[ok].max())
        ys, xs = np.nonzero(ok)
        dist = (xs + s / 2.0 - target[0]) ** 2 + (ys + s / 2.0 - target[1]) ** 2
        best = np.lexsort((xs, ys, np.round(dist, 6)))[0]
        return int(xs[best]), int(ys[best])

    def squares(self):
        """Returns (full, focus, cut); squares are (x0, y0, side) in working pixels. full: steps 4 and 7 of the
        module notes; focus: step 5, or None when no smaller square keeps every protected region; cut: the subject
        does not fit the largest square (step 7), in which case full is that placed largest square."""
        sat_c, sat_s = self._integral(self.core), self._integral(self.sil)
        total_c, total_s = int(sat_c[-1, -1]), int(sat_s[-1, -1])
        # Protected regions of the focus square: every separate kept part, and the subject's upper region (flags,
        # coins, crowns, flames and dice on top are where these compositions put the identity; bases sit below).
        protected = [(self._integral(p), int(p.sum()), PART_KEEP) for p in self.part_cores]
        protected += [(self._integral(region), int(region.sum()), ISLAND_KEEP)
                      for _, region in self.islands if region.any()]
        rows = np.nonzero(self.core.any(axis=1))[0]
        if len(rows):
            band_end = int(math.ceil(rows[0] + TOP_KEEP * (rows[-1] - rows[0] + 1)))
            top_core = self.core.copy()
            top_core[band_end:, :] = False
            protected.append((self._integral(top_core), int(top_core.sum()), TOP_COVER))
        # Thick sub-objects joined to the rest by thin necks (a die touching a box, a handle) are separate blobs of
        # the core. Apart from the largest blob, a focus square keeps or drops each blob whole, never slices it.
        blobs = []
        blob_labels, blob_count = ndi.label(self.core, structure=EIGHT)
        if blob_count > 1:
            sizes = ndi.sum(self.core, blob_labels, np.arange(1, blob_count + 1))
            largest = int(np.argmax(sizes))
            for i in range(blob_count):
                if i != largest and sizes[i] >= BLOB_MIN * total_c:
                    blob = blob_labels == i + 1
                    blobs.append((self._integral(blob), int(sizes[i])))
        cy, cx = ndi.center_of_mass(self.core)
        centroid = (cx + 0.5, cy + 0.5)
        smax = min(self.w, self.h)

        def smallest(lo, hi, test):
            while lo < hi:
                mid = (lo + hi) // 2
                if test(mid)[0].any():
                    hi = mid
                else:
                    lo = mid + 1
            return lo

        def full_test(s):
            bc, bs = self._boxsums(sat_c, s), self._boxsums(sat_s, s)
            return (bc * 1000 >= CORE_COVER * total_c) & (bs * 1000 >= SIL_COVER * total_s), bc + bs

        ok, score = full_test(smax)
        if not ok.any():
            # Wider (or taller) than the largest square. A mirror-symmetric subject (fan, balance, two rollers)
            # is centred so both sides lose the same. Otherwise the square goes where the islands it holds whole
            # (die faces first) are largest, then where it holds the most core mass; ties go nearest the centroid.
            wide_axis = 1 if self.w >= self.h else 0
            span = np.nonzero(self.sil.any(axis=1 - wide_axis))[0]
            lo, hi = int(span[0]), int(span[-1]) + 1
            part = self.sil[:, lo:hi] if wide_axis == 1 else self.sil[lo:hi, :]
            mirrored = part[:, ::-1] if wide_axis == 1 else part[::-1, :]
            symmetry = int((part & mirrored).sum()) / max(1, int((part | mirrored).sum()))
            if symmetry >= MIRROR_SYMMETRIC:
                offset = int(round((lo + hi) / 2.0 - smax / 2.0))
                offset = min(max(0, offset), (self.w if wide_axis == 1 else self.h) - smax)
                return ((offset, 0, smax) if wide_axis == 1 else (0, offset, smax)), None, True
            score = self._boxsums(sat_c, smax)
            for island, _ in self.islands:
                total = int(island.sum())
                whole = self._boxsums(self._integral(island), smax) * 1000 >= ISLAND_KEEP * total
                score = score + whole * (WIDE_ISLAND_WEIGHT * total)
            return self._pick(np.ones_like(score, dtype=bool), score, smax, centroid) + (smax,), None, True
        s_full = smallest(1, smax, full_test)
        ok, score = full_test(s_full)
        full = self._pick(ok, score, s_full, centroid) + (s_full,)

        def focus_test(s):
            bc = self._boxsums(sat_c, s)
            ok = bc * 1000 >= FOCUS_CORE * total_c
            for sat_p, total_p, permille in protected:
                ok &= self._boxsums(sat_p, s) * 1000 >= permille * total_p
            for sat_b, total_b in blobs:
                inside = self._boxsums(sat_b, s) * 1000
                ok &= (inside >= BLOB_WHOLE * total_b) | (inside <= BLOB_GONE * total_b)
            return ok, bc

        s_lo = max(1, int(math.ceil(FOCUS_MIN * s_full)))
        s_focus = smallest(s_lo, s_full, focus_test)
        ok, score = focus_test(s_focus)
        if not ok.any():
            return full, None, False
        focus = self._pick(ok, score, s_focus, centroid) + (s_focus,)
        return full, focus, False

    # --- metrics of a final square, given in source pixels ---------------------------------------------------------

    def metrics(self, left, top, side):
        ix0, iy0 = int(math.floor(left / self.k + 0.5)), int(math.floor(top / self.k + 0.5))
        ix1, iy1 = int(math.floor((left + side) / self.k + 0.5)), int(math.floor((top + side) / self.k + 0.5))
        inside = np.zeros_like(self.sil)
        inside[iy0:iy1, ix0:ix1] = True
        total_c = int(self.core.sum())
        core_out = 1.0 - (int((self.core & inside).sum()) / total_c if total_c else 1.0)
        fill = int((self.sil & inside).sum()) / max(1, (iy1 - iy0) * (ix1 - ix0))
        groups = 1
        if self.transparent:
            near = ndi.distance_transform_edt(~self.sil) <= SPLIT_GAP * WORK
            glabels, gcount = ndi.label(near, structure=EIGHT)
            if gcount > 1:
                masses = ndi.sum(self.core, glabels, np.arange(1, gcount + 1))
                groups = int((masses >= SPLIT_MIN_SHARE * max(1, total_c)).sum())
        return {"core_out": core_out, "fill": fill, "groups": groups}


def fine_marks(image, left, top, side):
    """Fine dark detail of the crop: dark specks (halftone dots, pips, small marks; CIE L* < 40 on paper, 2-30 px,
    4-connected) in a 96 px rendering. Most of them merge into grey at 32 px, so many marks mean a busy icon."""
    piece = image.crop((left, top, left + side, top + side)).convert("RGBa").resize((96, 96), Image.LANCZOS)
    rgba = np.asarray(piece.convert("RGBA"), dtype=np.float64) / 255.0
    alpha = rgba[..., 3:4]
    lightness = srgb_to_lab(rgba[..., :3] * alpha + np.array(PAPER) / 255.0 * (1 - alpha))[..., 0]
    dark = (rgba[..., 3] > 0.5) & (lightness < 40)
    labels, count = ndi.label(dark)
    if not count:
        return 0
    sizes = ndi.sum(dark, labels, np.arange(1, count + 1))
    return int(((sizes >= 2) & (sizes <= 30)).sum())


def ramp(value, at_one, at_floor, floor):
    """1.0 at `at_one`, `floor` at `at_floor`, linear between, clamped."""
    t = (value - at_one) / (at_floor - at_one)
    return float(min(1.0, max(floor, 1.0 - t * (1.0 - floor))))


def confidence_of(scale, m, wide):
    """scale: crop side / the image's long side, i.e. the crop's size relative to the whole picture shown fitted."""
    factors, notes = [], []
    zoom = ramp(scale, ZOOM_OK, 1.0, 0.7)
    if zoom < 1.0:
        notes.append(f"subject spans the whole canvas, crop ~ full image ({scale:.2f})" if scale >= 0.97
                     else f"subject spans most of the canvas, crop {scale:.2f} of the image")
    factors.append(zoom)
    cut = ramp(m["core_out"], CUT_OK, 0.30, 0.4)
    if wide:
        # The cut is forced by the picture's shape, not chosen by the subject's layout.
        notes.append("subject wider than the largest square")
        cut *= WIDE_FACTOR
    if m["core_out"] > CUT_OK:
        notes.append(f"crop cuts {100 * m['core_out']:.0f}% of the subject mass")
    factors.append(cut)
    fill = ramp(m["fill"], 0.30, 0.12, 0.6)
    if fill < 1.0:
        notes.append(f"thin subject, fills {100 * m['fill']:.0f}% of the crop")
    factors.append(fill)
    split = 1.0 if m["groups"] <= 1 else (0.85 if m["groups"] == 2 else 0.75)
    if split < 1.0:
        notes.append("two separate subjects" if m["groups"] == 2 else f"{m['groups']} separate subjects")
    factors.append(split)
    detail = ramp(m["marks"], DETAIL_OK, DETAIL_BAD, 0.75)
    if detail < 1.0:
        notes.append(f"busy at small size, {m['marks']} fine dark marks")
    factors.append(detail)
    conf = 1.0
    for f in factors:
        conf *= f
    return conf, notes


def to_units(left, top, side, W, H):
    """Integer 1e-4 units, nudged so the rounded square stays inside the image."""
    S = min(W, H)
    xi = int(round(left * 10000 / W))
    yi = int(round(top * 10000 / H))
    si = int(round(side * 10000 / S))
    while xi * W + si * S > 10000 * W:
        xi -= 1
    while yi * H + si * S > 10000 * H:
        yi -= 1
    return max(0, xi), max(0, yi), si


def fmt4(units):
    return f"{units // 10000}.{units % 10000:04d}"


def crop_one(path, theme, content_id):
    image = load_rgba(path)
    a = Analysis(image)
    override = OVERRIDES.get(theme, {}).get(content_id)
    notes = list(a.notes)
    full, focus, wide = a.squares()
    if override:
        # Written exactly as given (4 decimals); a square that leaves the image is an error, not a clamp.
        xi, yi, si = (int(round(override[key] * 10000)) for key in ("x", "y", "size"))
        if min(xi, yi) < 0 or xi * a.W + si * a.S > 10000 * a.W or yi * a.H + si * a.S > 10000 * a.H:
            raise SystemExit(f"override {theme}/{content_id} leaves the {a.W}x{a.H} image")
        side = int(round(si * a.S / 10000))
        left = min(int(round(xi * a.W / 10000)), a.W - side)
        top = min(int(round(yi * a.H / 10000)), a.H - side)
        method = "override"
    else:
        x0, y0, s = focus or full
        crop = s / (1.0 - 2.0 * MARGIN) if not wide else float(s)
        crop = max(min(crop, float(min(a.w, a.h))), MIN_SIZE * WORK)
        cx, cy = x0 + s / 2.0, y0 + s / 2.0
        side = min(int(round(crop * a.k)), a.S)
        left = min(max(0, int(round((cx - crop / 2.0) * a.k))), a.W - side)
        top = min(max(0, int(round((cy - crop / 2.0) * a.k))), a.H - side)
        xi, yi, si = to_units(left, top, side, a.W, a.H)
        method = METHOD
    m = a.metrics(left, top, side)
    m["marks"] = fine_marks(image, left, top, side)
    conf, reasons = confidence_of(side / max(a.W, a.H), m, wide and not override)
    notes.extend(reasons)
    if override:
        notes.insert(0, "override: " + override["reason"])
    entry = {"x": xi, "y": yi, "size": si, "confidence": int(round(conf * 10000)), "method": method,
             "note": "; ".join(notes), "imageSize": [a.W, a.H], "metrics": m, "pixels": (left, top, side),
             "full": full, "focus": focus, "k": a.k}
    return entry, image


# ------------------------------------------------------------------------------------------------- table writing

def table_text(theme, source, crops, image_size):
    lines = ["{",
             f'  "schema": {json.dumps(SCHEMA)},',
             f'  "theme": {json.dumps(theme)},',
             f'  "source": {json.dumps(source)},',
             f'  "generatedBy": {json.dumps(GENERATED_BY)},',
             f'  "imageSize": [{image_size[0]}, {image_size[1]}],',
             f'  "units": {json.dumps(UNITS)},',
             '  "crops": {']
    keys = sorted(crops)
    for n, key in enumerate(keys):
        c = crops[key]
        fields = {"confidence": fmt4(c["confidence"]), "method": json.dumps(c["method"]),
                  "size": fmt4(c["size"]), "x": fmt4(c["x"]), "y": fmt4(c["y"])}
        if c["note"]:
            fields["note"] = json.dumps(c["note"], ensure_ascii=False)
        if tuple(c["imageSize"]) != tuple(image_size):
            fields["imageSize"] = f'[{c["imageSize"][0]}, {c["imageSize"][1]}]'
        body = ", ".join(f'"{k}": {fields[k]}' for k in sorted(fields))
        lines.append(f'    {json.dumps(key)}: {{{body}}}' + ("," if n < len(keys) - 1 else ""))
    lines += ["  }", "}", ""]
    return "\n".join(lines)


def build(theme, source, out, sheet_dir=None, log=print):
    files = discover(source)
    crops, renders = {}, {}
    sizes = Counter()
    for content_id, path in files.items():
        entry, image = crop_one(path, theme, content_id)
        crops[content_id] = entry
        sizes[tuple(entry["imageSize"])] += 1
        if sheet_dir is not None:
            renders[content_id] = small_renders(image, entry["pixels"])
        image.close()
    unknown = sorted(set(OVERRIDES.get(theme, {})) - set(crops))
    if unknown:
        raise SystemExit(f"overrides for ids without an illustration: {', '.join(unknown)}")
    image_size = sorted(sizes.items(), key=lambda kv: (-kv[1], kv[0]))[0][0]
    text = table_text(theme, repo_relative(source), crops, image_size)
    out = Path(out)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_bytes(text.encode("utf-8"))
    low = [cid for cid in crops if crops[cid]["confidence"] < REVIEW_BELOW * 10000]
    log(f"CROPS_WRITTEN theme={theme} count={len(crops)} low={len(low)} out={repo_relative(out)}")
    if sheet_dir is not None:
        write_sheets(Path(sheet_dir), crops, renders, log)
    return crops


# ------------------------------------------------------------------------------------------------- review sheets

TILE_SIZES = (96, 48, 32)
FULL_SIZES = (150, 96)


def font():
    return ImageFont.load_default()


def render_full(image, n):
    """Whole illustration fitted (contain) into an n x n transparent square: (tile, scale, offset)."""
    w, h = image.size
    scale = n / max(w, h)
    tw, th = max(1, round(w * scale)), max(1, round(h * scale))
    small = image.convert("RGBa").resize((tw, th), Image.LANCZOS).convert("RGBA")
    canvas = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    canvas.paste(small, ((n - tw) // 2, (n - th) // 2))
    return canvas, scale, ((n - tw) // 2, (n - th) // 2)


def no_mip_sample(image, pixels, n):
    """The crop as a GPU draws it from a texture without mipmaps: one bilinear sample of the full-resolution
    texture per output pixel centre (premultiplied). The illustrations are imported with enableMipMap 0, so this
    is closer to a run-time Sprite.Create crop shown at n px than the area-filtered review tiles are."""
    left, top, side = pixels
    rgba = np.asarray(image)
    centres = (np.arange(n) + 0.5) * side / n - 0.5
    ys, xs = top + centres, left + centres
    y0 = np.clip(np.floor(ys).astype(int), 0, rgba.shape[0] - 2)
    x0 = np.clip(np.floor(xs).astype(int), 0, rgba.shape[1] - 2)
    fy, fx = (ys - y0)[:, None, None], (xs - x0)[None, :, None]

    def texels(dy, dx):
        t = rgba[(y0 + dy)[:, None], (x0 + dx)[None, :]].astype(np.float64)
        return np.concatenate([t[..., :3] * t[..., 3:4] / 255.0, t[..., 3:4]], axis=-1)

    out = (texels(0, 0) * (1 - fx) + texels(0, 1) * fx) * (1 - fy) + (texels(1, 0) * (1 - fx) + texels(1, 1) * fx) * fy
    alpha = out[..., 3:4]
    rgb = np.where(alpha > 0, out[..., :3] * 255.0 / np.maximum(alpha, 1e-9), 0.0)
    tile = np.concatenate([rgb, alpha], axis=-1)
    return Image.fromarray(np.clip(np.floor(tile + 0.5), 0, 255).astype(np.uint8), "RGBA")


def small_renders(image, pixels):
    """The crop at the review sizes (area-filtered, premultiplied), the no-mipmap GPU sample at 32 px, and the
    whole picture, so sheets need no full-resolution image in memory."""
    left, top, side = pixels
    piece = image.crop((left, top, left + side, top + side)).convert("RGBa")
    out = {n: piece.resize((n, n), Image.LANCZOS).convert("RGBA") for n in TILE_SIZES}
    out["no-mip", 32] = no_mip_sample(image, pixels, 32)
    for n in FULL_SIZES:
        out["full", n] = render_full(image, n)
    return out


def on(bg, icon):
    base = Image.new("RGBA", icon.size, bg + (255,))
    base.alpha_composite(icon)
    return base.convert("RGB")


def grid_sheet(ids, renders, n, bg, cols=16, pad=8, label_h=12, key=None):
    cell_w, cell_h = n + 2 * pad, n + 2 * pad + label_h
    rows = (len(ids) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * cell_w, rows * cell_h), bg)
    draw = ImageDraw.Draw(sheet)
    ink = (0, 0, 0) if sum(bg) > 400 else (255, 255, 255)
    for i, cid in enumerate(ids):
        x, y = (i % cols) * cell_w + pad, (i // cols) * cell_h + pad
        sheet.paste(on(bg, renders[cid][n if key is None else key]), (x, y))
        draw.text((x, y + n + 2), cid, fill=ink, font=font())
    return sheet


def mark_crop(draw, x, y, full, crop, width):
    _, scale, (ox, oy) = full
    left, top, side = crop["pixels"]
    draw.rectangle((x + ox + left * scale, y + oy + top * scale,
                    x + ox + (left + side) * scale - 1, y + oy + (top + side) * scale - 1),
                   outline=(220, 30, 30), width=width)


def write_sheets(folder, crops, renders, log):
    folder.mkdir(parents=True, exist_ok=True)
    ids = sorted(crops, key=id_sort_key)
    for n in (48, 32):
        for name, bg in (("grey", GREY), ("paper", PAPER)):
            sheet = grid_sheet(ids, renders, n, bg)
            sheet.save(folder / f"sheet_{n}_{name}.png")
            if n == 32:
                # The same 32 px pixels, enlarged without filtering, for reviewers' eyes.
                sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST).save(
                    folder / f"sheet_{n}_{name}_x2.png")
    # Advisory: what the same crops look like when a GPU samples the mip-0 texture at 32 px (no mipmaps).
    sheet = grid_sheet(ids, renders, 32, PAPER, key=("no-mip", 32))
    sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST).save(folder / "sheet_32_paper_nomip_x2.png")
    write_overlay(folder, ids, crops, renders)
    low = [cid for cid in ids if crops[cid]["confidence"] < REVIEW_BELOW * 10000]
    write_side_by_side(folder, low, crops, renders)
    write_report(folder / "crops_report.txt", ids, crops)
    write_similarity(folder, ids, renders, log)
    log(f"SHEETS_WRITTEN dir={repo_relative(folder)} low={len(low)}")


def write_overlay(folder, ids, crops, renders, n=150, cols=10, per_page=60):
    """Every whole illustration with its crop square drawn on it (for checking the method)."""
    for page in range(0, len(ids), per_page):
        chunk = ids[page:page + per_page]
        rows = (len(chunk) + cols - 1) // cols
        sheet = Image.new("RGB", (cols * (n + 10), rows * (n + 24)), PAPER)
        draw = ImageDraw.Draw(sheet)
        for i, cid in enumerate(chunk):
            x, y = (i % cols) * (n + 10) + 5, (i // cols) * (n + 24) + 5
            full = renders[cid]["full", n]
            sheet.paste(on(PAPER, full[0]), (x, y))
            # Thin blue: the full-subject square before trimming and margin. Red: the crop.
            fx, fy, fs = crops[cid]["full"]
            k, scale, (ox, oy) = crops[cid]["k"], full[1], full[2]
            draw.rectangle((x + ox + fx * k * scale, y + oy + fy * k * scale,
                            x + ox + (fx + fs) * k * scale - 1, y + oy + (fy + fs) * k * scale - 1),
                           outline=(40, 90, 220), width=1)
            mark_crop(draw, x, y, full, crops[cid], 2)
            draw.text((x, y + n + 2), f'{cid} {crops[cid]["confidence"] / 10000:.2f}', fill=(0, 0, 0), font=font())
        sheet.save(folder / f"overlay_{page // per_page + 1}.png")


def wrap(text, width):
    lines, line = [], ""
    for word in text.split(" "):
        if line and len(line) + len(word) + 1 > width:
            lines.append(line)
            line = word
        else:
            line = (line + " " + word).strip()
    return lines + [line]


def write_side_by_side(folder, ids, crops, renders, n=96, cols=2, rows=12):
    """Pages of: whole illustration (crop square marked) beside the crop, both at 96 px, then the crop at 32 px."""
    for old in folder.glob("review_low_confidence_*.png"):
        old.unlink()
    row_h = n + 14
    cell_w = 10 + 2 * n + 32 + 3 * 16 + 300
    per_page = cols * rows
    for page in range(max(1, (len(ids) + per_page - 1) // per_page)):
        chunk = ids[page * per_page:(page + 1) * per_page]
        sheet = Image.new("RGB", (cols * cell_w, max(1, (len(chunk) + cols - 1) // cols) * row_h + 10), PAPER)
        draw = ImageDraw.Draw(sheet)
        if not chunk:
            draw.text((10, 10), "no crops below the review threshold", fill=(0, 0, 0), font=font())
        for i, cid in enumerate(chunk):
            x, y = (i % cols) * cell_w + 10, 10 + (i // cols) * row_h
            full = renders[cid]["full", n]
            sheet.paste(on(PAPER, full[0]), (x, y))
            mark_crop(draw, x, y, full, crops[cid], 1)
            sheet.paste(on(PAPER, renders[cid][n]), (x + n + 16, y))
            sheet.paste(on(PAPER, renders[cid][32]), (x + 2 * n + 32, y))
            text_x = x + 2 * n + 32 + 32 + 16
            draw.text((text_x, y), f'{cid}  confidence {crops[cid]["confidence"] / 10000:.2f}', fill=(0, 0, 0),
                      font=font())
            for j, text in enumerate(wrap(crops[cid]["note"], 40)[:7]):
                draw.text((text_x, y + 16 + 12 * j), text, fill=(60, 60, 60), font=font())
        sheet.save(folder / f"review_low_confidence_{page + 1}.png")


def confidence_buckets(crops):
    buckets = Counter()
    for c in crops.values():
        conf = c["confidence"] / 10000
        buckets[">=0.90" if conf >= 0.9 else "0.80-0.89" if conf >= 0.8 else "0.70-0.79" if conf >= 0.7
                else "0.50-0.69" if conf >= 0.5 else "<0.50"] += 1
    return [(k, buckets[k]) for k in (">=0.90", "0.80-0.89", "0.70-0.79", "0.50-0.69", "<0.50")]


def write_report(path, ids, crops):
    lines = ["id     x       y       size    conf  method        note"]
    for cid in ids:
        c = crops[cid]
        lines.append(f'{cid:<6} {fmt4(c["x"])}  {fmt4(c["y"])}  {fmt4(c["size"])}  {fmt4(c["confidence"])}  '
                     f'{c["method"]:<13} {c["note"]}')
    lines.append("")
    lines.append("confidence: " + ", ".join(f"{k} {v}" for k, v in confidence_buckets(crops)))
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


# ------------------------------------------------------------------------------------- AX-07 similarity (advisory)

def srgb_to_lab(rgb):
    rgb = np.asarray(rgb, dtype=np.float64)
    lin = np.where(rgb <= 0.04045, rgb / 12.92, ((rgb + 0.055) / 1.055) ** 2.4)
    m = np.array([[0.4124564, 0.3575761, 0.1804375],
                  [0.2126729, 0.7151522, 0.0721750],
                  [0.0193339, 0.1191920, 0.9503041]])
    xyz = np.stack([(lin * m[i]).sum(axis=-1) for i in range(3)], axis=-1) / np.array([0.95047, 1.0, 1.08883])
    eps = (6 / 29) ** 3
    f = np.where(xyz > eps, np.cbrt(xyz), xyz / (3 * (6 / 29) ** 2) + 4 / 29)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], axis=-1)


def icon_features(tile):
    """tile: 32 px RGBA crop. Returns (8x8 Lab layout on paper, 32x32 lightness on paper, subject Lab histogram)."""
    rgba = np.asarray(tile, dtype=np.float64) / 255.0
    alpha = rgba[..., 3:4]
    paper = np.array(PAPER, dtype=np.float64) / 255.0
    lab = srgb_to_lab(rgba[..., :3] * alpha + paper * (1 - alpha))
    layout = lab.reshape(8, 4, 8, 4, 3).mean(axis=(1, 3))
    lightness = lab[..., 0] / 100.0
    sub = srgb_to_lab(rgba[..., :3])
    li = np.clip((sub[..., 0] / 25.0).astype(int), 0, 3)
    ai = np.clip(((sub[..., 1] + 40) / 20.0).astype(int), 0, 3)
    bi = np.clip(((sub[..., 2] + 40) / 20.0).astype(int), 0, 3)
    hist = np.zeros(64)
    np.add.at(hist, (li * 16 + ai * 4 + bi).ravel(), rgba[..., 3].ravel())
    hist /= max(hist.sum(), 1e-9)
    return layout, lightness, hist


def ssim_matrix(images, sigma=1.5):
    """Mean SSIM between every pair of equally sized grey images (Gaussian window)."""
    n = len(images)
    x = np.stack(images)
    mu = np.stack([ndi.gaussian_filter(v, sigma) for v in x])
    var = np.stack([ndi.gaussian_filter(v * v, sigma) for v in x]) - mu * mu
    c1, c2 = 0.01 ** 2, 0.03 ** 2
    out = np.ones((n, n))
    for i in range(n - 1):
        prod = np.stack([ndi.gaussian_filter(x[i] * x[j], sigma) for j in range(i + 1, n)])
        cov = prod - mu[i] * mu[i + 1:]
        num = (2 * mu[i] * mu[i + 1:] + c1) * (2 * cov + c2)
        den = (mu[i] ** 2 + mu[i + 1:] ** 2 + c1) * (var[i] + var[i + 1:] + c2)
        vals = (num / den).mean(axis=(1, 2))
        out[i, i + 1:] = vals
        out[i + 1:, i] = vals
    return out


def similarity(ids, renders):
    """Pairwise AX-07 distances. Returns (total, layout, struct, colour, medians) as n x n arrays."""
    feats = [icon_features(renders[cid][32]) for cid in ids]
    layout = np.stack([f[0] for f in feats])
    hist = np.stack([f[2] for f in feats])
    n = len(ids)
    d_layout = np.zeros((n, n))
    for i in range(n):
        d_layout[i] = np.sqrt(((layout - layout[i]) ** 2).sum(axis=-1)).mean(axis=(1, 2))
    d_colour = 0.5 * np.abs(hist[:, None, :] - hist[None, :, :]).sum(axis=-1)
    d_struct = 1.0 - ssim_matrix([f[1] for f in feats])
    iu = np.triu_indices(n, 1)
    med = [float(np.median(d[iu])) for d in (d_layout, d_struct, d_colour)]
    total = (d_layout / med[0] + d_struct / med[1] + d_colour / med[2]) / 3.0
    return total, d_layout, d_struct, d_colour, med


def write_similarity(folder, ids, renders, log, top=30):
    total, d_layout, d_struct, d_colour, med = similarity(ids, renders)
    iu = np.triu_indices(len(ids), 1)
    order = np.lexsort((iu[1], iu[0], np.round(total[iu], 9)))
    lines = ["AX-07 advisory similarity of the compact icons at 32 px on paper (#F4E8CB). Not a gate.",
             "distance = mean of three terms, each divided by its median over all pairs (1.00 = a typical pair):",
             "  layout = mean CIE76 dE between 8x8 Lab thumbnails (colour placement, coarse shape)",
             "  struct = 1 - SSIM of the 32 px lightness (Gaussian window, sigma 1.5)",
             "  colour = total-variation distance of the subjects' Lab colour histograms (4x4x4 bins)",
             f"pairs: {len(order)}; medians: layout {med[0]:.2f} dE, struct {med[1]:.3f}, colour {med[2]:.3f}",
             "",
             "rank  pair         distance  layout  struct  colour"]
    pairs = []
    for rank, k in enumerate(order[:top], 1):
        i, j = iu[0][k], iu[1][k]
        pairs.append((ids[i], ids[j], float(total[i, j])))
        lines.append(f"{rank:>4}  {ids[i]:<5} {ids[j]:<5}  {total[i, j]:7.3f}  {d_layout[i, j]:6.2f}  "
                     f"{d_struct[i, j]:6.3f}  {d_colour[i, j]:6.3f}")
    (folder / "similarity.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")
    write_pair_sheet(folder, pairs, renders)
    log(f"SIMILARITY_WRITTEN pairs={len(order)} closest={pairs[0][0]}-{pairs[0][1]} {pairs[0][2]:.3f}")


def write_pair_sheet(folder, pairs, renders, per_page=10):
    """Pages of the closest pairs. Each row: both icons at 32 px (real size), the same 32 px pixels enlarged 3x
    without filtering, then both crops at 96 px for reference."""
    for old in folder.glob("similarity_pairs*.png"):
        old.unlink()
    row_h = 104
    for page in range(max(1, (len(pairs) + per_page - 1) // per_page)):
        chunk = pairs[page * per_page:(page + 1) * per_page]
        sheet = Image.new("RGB", (32 * 2 + 96 * 4 + 7 * 8 + 150, max(1, len(chunk)) * row_h + 8), PAPER)
        draw = ImageDraw.Draw(sheet)
        for r, (a, b, d) in enumerate(chunk):
            y = 4 + r * row_h
            x = 4
            for cid in (a, b):
                sheet.paste(on(PAPER, renders[cid][32]), (x, y + 32))
                x += 32 + 8
            for cid in (a, b):
                sheet.paste(on(PAPER, renders[cid][32]).resize((96, 96), Image.NEAREST), (x, y))
                x += 96 + 8
            for cid in (a, b):
                sheet.paste(on(PAPER, renders[cid][96]), (x, y))
                x += 96 + 8
            draw.text((x, y + 4), f"{page * per_page + r + 1:>2}. {a} / {b}", fill=(0, 0, 0), font=font())
            draw.text((x, y + 18), f"distance {d:.3f}", fill=(60, 60, 60), font=font())
        sheet.save(folder / f"similarity_pairs_{page + 1}.png")


# ------------------------------------------------------------------------------------------------------------ CLI

def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--theme", required=True, help="theme id written into the table; selects OVERRIDES")
    parser.add_argument("--source", required=True, help="folder with illustration_<id>.png files")
    parser.add_argument("--out", required=True, help="crop table JSON to write")
    parser.add_argument("--sheet-dir", help="also write review sheets and the AX-07 similarity report here")
    args = parser.parse_args(argv)
    build(args.theme, Path(args.source), Path(args.out), Path(args.sheet_dir) if args.sheet_dir else None)
    return 0


if __name__ == "__main__":
    sys.exit(main())
