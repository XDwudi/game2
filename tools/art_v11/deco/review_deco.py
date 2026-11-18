"""Offline review mocks for the deco kit (Python emulation of the game's drawing; not Unity captures).

    python tools/art_v11/deco/build_deco.py --review            # after build_deco.py has written the kit

Writes ArtSource/Art11/deco/review/:
- play_states.png and play_<state>_<100|150>.png: the play screen (GameApp.Play11 L11 rectangles, DrawActions11 states)
  over Table/table_scene.png;
- shop_w14.png: the W14 shop (GameApp.Shop14 S14 at 100 % text) over Table/page_scene.png with the tints the game
  applies (section, caption and the button steps from theme.json, W20X);
- palette.png: the palette with its contrast table; sprites.png: the drawn sprites on their tints.
Sprites the kit does not ship yet are drawn with the default theme's sprite (the game's fallback) and listed on each
image. Sprites are sampled as the game's UI sprites are (bilinear, no mipmaps), tinted and blended in linear light.
Card names, texts and pictures are stand-ins (default illustrations; the W22 deco redraws are not in this worktree).
"""
from __future__ import annotations

import importlib.util
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve()
_SPEC = importlib.util.spec_from_file_location("deco_build", HERE.parent / "build_deco.py")
bd = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(bd)

ROOT = bd.ROOT
OUT = ROOT / bd.REVIEW / "review"
THEME = ROOT / bd.THEME
ART = ROOT / "DicePrototype/Assets/Art/Art071/Illustrations"
LABEL = (246, 236, 214)
NOTE = (190, 176, 150)


# ---------------------------------------------------------------------------------------------- kit
def load_kit():
    """Shipped deco sprites; any referenced sprite the kit lacks is the default one (the game's fallback)."""
    specs = {s["name"]: s for s in json.loads(bd.CONTRACT.read_text(encoding="utf-8"))["sprites"]}
    kit, fallback = {}, []
    for name, s in specs.items():
        own = THEME / "UI" / (name + ".png")
        path = own if own.is_file() else ROOT / s["default"]
        if not own.is_file() and s["referenced"]:
            fallback.append(name)
        with Image.open(path) as im:
            kit[name] = im.convert("RGBA")
    borders = {n: s["border"] for n, s in specs.items()}
    ppu = {n: s["pixelsPerUnit"] for n, s in specs.items()}
    palette = json.loads((THEME / "theme.json").read_text(encoding="utf-8"))["palette"]
    with Image.open(THEME / "Table/table_scene.png") as im:
        table = im.convert("RGB")
    with Image.open(THEME / "Table/page_scene.png") as im:
        page = im.convert("RGB")
    return kit, borders, ppu, palette, table, page, sorted(fallback)


def art(name: str) -> Image.Image:
    with Image.open(ART / f"illustration_{name.lower()}.png") as im:
        return im.convert("RGBA")


# ---------------------------------------------------------------------------------------------- linear canvas
def _lin(c):
    return np.where(c <= .04045, c / 12.92, ((c + .055) / 1.055) ** 2.4)


def _gam(c):
    return np.where(c <= .0031308, c * 12.92, 1.055 * np.power(np.clip(c, 0, None), 1 / 2.4) - .055)


_TEX: dict[int, tuple] = {}


def _lin_cached(im: Image.Image) -> np.ndarray:
    """Linear premultiplied texture of a sprite, converted once per image object."""
    hit = _TEX.get(id(im))
    if hit is None or hit[0] is not im:
        hit = (im, _lin_texture(im))
        _TEX[id(im)] = hit
    return hit[1]


def _lin_texture(im: Image.Image) -> np.ndarray:
    a = np.asarray(im.convert("RGBA")).astype(np.float32) / 255.0
    out = np.empty_like(a)
    out[..., 3] = a[..., 3]
    out[..., :3] = _lin(a[..., :3]) * a[..., 3:]
    return out


def _slice_axis(n_dst: int, n_src: int, lo: float, hi: float, lo_dst: float, hi_dst: float) -> np.ndarray:
    x = np.arange(n_dst, dtype=np.float64) + .5
    if lo_dst + hi_dst > n_dst:
        k = n_dst / (lo_dst + hi_dst)
        lo_dst, hi_dst = lo_dst * k, hi_dst * k
    u = lo + (x - lo_dst) / max(n_dst - lo_dst - hi_dst, 1e-6) * (n_src - lo - hi)
    head, tail = x < lo_dst, x > n_dst - hi_dst
    u[head] = x[head] / max(lo_dst, 1e-6) * lo
    u[tail] = n_src - (n_dst - x[tail]) / max(hi_dst, 1e-6) * hi
    return u


def _bilinear(tex: np.ndarray, us: np.ndarray, vs: np.ndarray) -> np.ndarray:
    h, w = tex.shape[:2]
    x, y = us - .5, vs - .5
    x0, y0 = np.floor(x).astype(int), np.floor(y).astype(int)
    fx, fy = (x - x0).astype(np.float32), (y - y0).astype(np.float32)
    rows = tex[:, np.clip(x0, 0, w - 1)] * (1 - fx)[None, :, None] + tex[:, np.clip(x0 + 1, 0, w - 1)] * fx[None, :, None]
    return rows[np.clip(y0, 0, h - 1)] * (1 - fy)[:, None, None] + rows[np.clip(y0 + 1, 0, h - 1)] * fy[:, None, None]


class Canvas:
    """Linear-light premultiplied canvas in screen pixels; `scale` is screen px per design unit (1080p 1, 720p 2/3)."""

    def __init__(self, w: int, h: int, background, scale: float = 1.0):
        self.scale = scale
        self.buf = np.zeros((h, w, 4), np.float32)
        if isinstance(background, Image.Image):
            self.buf[...] = _lin_texture(background.convert("RGBA").resize((w, h), Image.Resampling.LANCZOS))
        else:
            self.buf[..., :3] = _lin(bd.col(background))
            self.buf[..., 3] = 1.0

    def _over(self, layer: np.ndarray, x0: int, y0: int):
        H, W = self.buf.shape[:2]
        xs, ys = max(0, x0), max(0, y0)
        xe, ye = min(W, x0 + layer.shape[1]), min(H, y0 + layer.shape[0])
        if xe > xs and ye > ys:
            src = layer[ys - y0:ye - y0, xs - x0:xe - x0]
            self.buf[ys:ye, xs:xe] = src + self.buf[ys:ye, xs:xe] * (1 - src[..., 3:4])

    def _rect(self, x, y, w, h):
        s = self.scale
        x0, y0 = int(round(x * s)), int(round(y * s))
        return x0, y0, max(1, int(round((x + w) * s)) - x0), max(1, int(round((y + h) * s)) - y0)

    def sprite(self, im: Image.Image, x, y, w, h, tint=None, border=None, ppu: float = 200.0, alpha: float = 1.0):
        x0, y0, W, H = self._rect(x, y, w, h)
        tex = _lin_cached(im)
        th, tw = tex.shape[:2]
        if border is not None and any(border):
            l, b, r, t = border
            k = 100.0 / ppu * self.scale
            us = _slice_axis(W, tw, l, r, l * k, r * k)
            vs = _slice_axis(H, th, t, b, t * k, b * k) if (t or b) else (np.arange(H) + .5) / H * th
        else:
            us, vs = (np.arange(W) + .5) / W * tw, (np.arange(H) + .5) / H * th
        layer = _bilinear(tex, us, vs)
        if tint is not None:
            layer[..., :3] *= _lin(np.asarray(tint, np.float32))
        if alpha < 1:
            layer *= alpha
        self._over(layer, x0, y0)

    def fit(self, im: Image.Image, x, y, w, h, tint=None, alpha: float = 1.0):
        """preserveAspect: the picture fitted into the rect and centred."""
        x0, y0, W, H = self._rect(x, y, w, h)
        pic = im.convert("RGBA").copy()
        pic.thumbnail((W, H), Image.Resampling.LANCZOS)
        layer = _lin_texture(pic)
        if tint is not None:
            layer[..., :3] *= _lin(np.asarray(tint, np.float32))
        if alpha < 1:
            layer *= alpha
        self._over(layer, x0 + (W - pic.width) // 2, y0 + (H - pic.height) // 2)

    def box(self, x, y, w, h, colour, alpha: float = 1.0):
        x0, y0, W, H = self._rect(x, y, w, h)
        layer = np.zeros((H, W, 4), np.float32)
        layer[..., :3] = _lin(bd.col(colour)) * alpha
        layer[..., 3] = alpha
        self._over(layer, x0, y0)

    def text(self, x, y, value: str, size: float, colour: str, font, anchor: str = "la", spacing: float = 4.0):
        if not value:
            return
        f = ImageFont.truetype(str(font), max(6, int(round(size * self.scale))))
        dr = ImageDraw.Draw(Image.new("L", (1, 1)))
        box = dr.multiline_textbbox((0, 0), value, font=f, anchor=anchor, spacing=spacing) if "\n" in value else \
            dr.textbbox((0, 0), value, font=f, anchor=anchor)
        box = (math.floor(box[0]), math.floor(box[1]), math.ceil(box[2]), math.ceil(box[3]))
        layer = Image.new("RGBA", (box[2] - box[0] + 4, box[3] - box[1] + 4), (0, 0, 0, 0))
        d = ImageDraw.Draw(layer)
        if "\n" in value:
            d.multiline_text((2 - box[0], 2 - box[1]), value, font=f, fill=bd.rgb8(colour) + (255,), anchor=anchor,
                             spacing=spacing)
        else:
            d.text((2 - box[0], 2 - box[1]), value, font=f, fill=bd.rgb8(colour) + (255,), anchor=anchor)
        self._over(_lin_texture(layer), int(round(x * self.scale)) + box[0] - 2, int(round(y * self.scale)) + box[1] - 2)

    def image(self) -> Image.Image:
        rgb = _gam(self.buf[..., :3] / np.maximum(self.buf[..., 3:4], 1e-6))
        return Image.fromarray(np.clip(np.rint(rgb * 255), 0, 255).astype(np.uint8), "RGB")


def width_of(text: str, size: float, font=None) -> float:
    return ImageFont.truetype(str(font or bd.FONT), max(6, int(round(size)))).getlength(text)


def frame_insets_px(pixels: np.ndarray, border) -> tuple[int, int, int, int]:
    """GameApp.FrameInsets11 (left, bottom, right, top) in sprite px. Unity rows run up."""
    up = pixels[::-1].astype(np.int32)
    h, w = up.shape[:2]
    fill = up[h // 2, w // 2]

    def inset(get, count, limit):
        last = 0
        for i in range(min(count // 2, int(round(limit)))):
            c = get(i)
            if c[3] < 128 or int(np.abs(c[:3] - fill[:3]).sum()) > 48:
                last = i + 1
        return last
    l, b, r, t = border
    return (inset(lambda i: up[h // 2, i], w, l), inset(lambda i: up[i, w // 2], h, b),
            inset(lambda i: up[h // 2, w - 1 - i], w, r), inset(lambda i: up[h - 1 - i, w // 2], h, t))


def ring_insets_px(pixels: np.ndarray, border) -> tuple[int, int, int, int]:
    """GameApp.Card11 RingInsets13 (left, bottom, right, top) in sprite px."""
    up = pixels[::-1]
    h, w = up.shape[:2]

    def inset(get, count, limit):
        last = 0
        for i in range(min(count // 2, int(round(limit)))):
            if get(i)[3] >= 128:
                last = i + 1
        return last
    l, b, r, t = border
    return (inset(lambda i: up[h // 2, i], w, l), inset(lambda i: up[i, w // 2], h, b),
            inset(lambda i: up[h // 2, w - 1 - i], w, r), inset(lambda i: up[h - 1 - i, w // 2], h, t))


def wrap(text: str, font, size: float, width: float) -> list[str]:
    """Greedy CJK wrap; closing punctuation never starts a line: it takes the previous character down with it."""
    f = ImageFont.truetype(str(font), max(6, int(round(size))))
    lines, line = [], ""
    for ch in text:
        if line and f.getlength(line + ch) > width:
            if ch in "。，、；：！？）》”" and len(line) > 1:
                lines.append(line[:-1])
                line = line[-1] + ch
            else:
                lines.append(line)
                line = ch
        else:
            line += ch
    return lines + [line] if line else lines


def balanced_lines(text: str, font, size: float, width: float) -> list[str]:
    """GameApp.BalancedLines11: one break where the halves are most even, preferring punctuation or a " · "."""
    f = ImageFont.truetype(str(font), max(6, int(round(size))))
    if "\n" in text or len(text) < 6 or f.getlength(text) <= width:
        return [text]
    prefix = [0.0]
    for ch in text:
        prefix.append(prefix[-1] + f.getlength(ch))
    total, after, before = prefix[-1], "，。；：、！？）”", "（“"
    best, best_skip, best_score = -1, 0, float("inf")
    for i in range(3, len(text) - 2):
        skip = 3 if text[i:i + 3] == " · " else 0
        if skip == 0 and (text[i] == " " or text[i - 1] == " "):
            continue
        if len(text) - (i + skip) < 3:
            continue
        punctuation = skip > 0 or text[i - 1] in after or text[i] in before
        a, b = prefix[i], total - prefix[i + skip]
        score = max(a, b) + (0 if punctuation else width * .6) + (width * 4 if a > width or b > width else 0)
        if score < best_score:
            best, best_skip, best_score = i, skip, score
    return [text] if best < 0 else [text[:best], text[best + best_skip:]]


# ---------------------------------------------------------------------------------------------- the game's rules
class Game:
    """The kit, palette and text scale, with the drawing helpers the game's code uses (UiKit, LabelOn11)."""

    def __init__(self, kit, borders, ppu, palette, scale_text: float = 1.0):
        self.kit, self.borders, self.ppu, self.P = kit, borders, ppu, palette
        self.fs = scale_text
        self.large = scale_text > 1.01

    # colours
    def c(self, key: str) -> np.ndarray:
        return bd.col(self.P[key])

    def centre(self, name: str) -> str:
        px = np.asarray(self.kit[name])
        return bd.hexc(px[px.shape[0] // 2, px.shape[1] // 2, :3] / 255.0)

    def fill(self, name: str, tint) -> str:
        tint_hex = tint if isinstance(tint, str) else bd.hexc(tint)
        return bd.drawn(self.centre(name), tint_hex)

    def label_on(self, name: str, tint, preferred: str | None = None):
        """GameApp.LabelOn11: a meaning colour stays at 4.5:1; else the best of ink / paper / textColor at 4.95:1; else
        the surface's tint steps 5 % toward black (or white) until the lightest (or darkest) label reaches 4.95:1.
        Returns (label hex, tint hex as drawn)."""
        P = self.P
        tint_hex = tint if isinstance(tint, str) else bd.hexc(tint)
        fill = bd.drawn(self.centre(name), tint_hex)
        if preferred is not None and bd.contrast(preferred, fill) >= 4.5:
            return preferred, tint_hex
        cands = [P["ink"], P["paper"], P["textColor"]]
        best = max(cands, key=lambda c: bd.contrast(c, fill))
        if bd.contrast(best, fill) >= 4.95:
            return best, tint_hex
        light = max(cands, key=bd.luminance)
        dark = min(cands, key=bd.luminance)

        def steps(toward: str, label: str):
            t = bd.col(tint_hex)
            for i in range(25):
                if bd.contrast(label, bd.drawn(self.centre(name), bd.hexc(t))) >= 4.95:
                    return i, bd.hexc(t)
                t = t + (bd.col(toward) - t) * .05
            return -1, None
        deepen, dt = steps("000000", light)
        lighten, lt = steps("FFFFFF", dark)
        if deepen < 0 and lighten < 0:
            return best, tint_hex
        down = lighten < 0 or (0 <= deepen <= lighten)
        return (light, dt) if down else (dark, lt)

    def sec(self, enabled: bool = True) -> str:
        t = bd.lerp_hex(self.P["background"], self.P["panel"], .55)
        return t if enabled else bd.lerp_hex(t, self.P["panelLight"], .25)

    def body(self, size: float) -> float:
        """Body11: a size that ends at no less than 19.5 units after the text scale (returned already scaled)."""
        return max(size, 19.5 / self.fs) * self.fs

    def line(self, size: float) -> float:
        return math.ceil(size * self.fs * 1.46) + 2

    # surfaces
    def put(self, c: Canvas, name: str, x, y, w, h, tint=None, alpha: float = 1.0, mult: float = 1.0):
        t = None if tint is None else (bd.col(tint) if isinstance(tint, str) else tint)
        c.sprite(self.kit[name], x, y, w, h, tint=t, border=self.borders[name], ppu=self.ppu[name] * mult, alpha=alpha)

    def button(self, c: Canvas, x, y, w, h, label: str, size: float, enabled: bool = True, primary: bool = False,
               face=None, font=None, label_colour: str | None = None, two_line: str | None = None):
        """UiKit.Button: the step (2, 8, w - 4, h - 6), the face (0, 0, w, h - 7), the lip; the label LabelOn11."""
        P = self.P
        step = (P["buttonStepPrimary"] if primary else P["buttonStep"]) if enabled else P["shadow"]
        face = face or (P["primary"] if primary else P["paper"]) if enabled else (face or (P["panel"] if primary else P["panelLight"]))
        self.put(c, "ui_button_bevel", x + 2, y + 8, w - 4, h - 6, step)
        label_hex, face_drawn = self.label_on("ui_button_bevel", face) if enabled else (label_colour or P["muted"], face)
        self.put(c, "ui_button_bevel", x, y, w, h - 7, face_drawn)
        if enabled:
            c.box(x + 14, y + 5, w - 28, 1, "FFFFFF", .35)
        colour = label_colour if (label_colour and not enabled) else label_hex
        if two_line:
            c.text(x + w / 2, y + (h - 7) / 2 - size * .38, label, size, colour, font or bd.FONT, "mm")
            c.text(x + w / 2, y + (h - 7) / 2 + size * .52, two_line, size * .72, colour, font or bd.FONT, "mm")
        else:
            c.text(x + w / 2, y + (h - 7) / 2, label, size, colour, font or bd.FONT, "mm")
        return face_drawn

    def solid(self, c: Canvas, x, y, w, h, label: str, size: float, enabled: bool = True, icon: str | None = None,
              wrap_width: float | None = None):
        """GameApp.SolidButton11: ui_chip_formula x lerp(Background, Panel, .55); label LabelOn11(face, TextColor)."""
        tint = self.sec(enabled)
        self.put(c, "ui_chip_formula", x, y, w, h, tint)
        label_hex, _ = self.label_on("ui_chip_formula", tint, self.P["textColor"] if enabled else self.P["muted"])
        if icon:
            s = min(34, h - 24) if icon != "ui_icon_dice_view" else 40
            tw = width_of(label, size)
            sx = x + (w - (s + 8 + tw)) / 2 if icon != "ui_icon_dice_view" else x + 16
            tinted = icon in bd.NEUTRAL_NAMES
            c.fit(self.kit[icon], sx, y + (h - s) / 2, s, s, tint=bd.col(label_hex) if tinted else None)
            if icon == "ui_icon_dice_view":
                c.text(x + w / 2, y + h / 2, label, size, label_hex, bd.FONT, "mm")
            else:
                c.text(sx + s + 8, y + h / 2, label, size, label_hex, bd.FONT, "lm")
        elif wrap_width and width_of(label, size) > wrap_width:
            lines = balanced_lines(label, bd.FONT, size, wrap_width)
            c.text(x + w / 2, y + h / 2, "\n".join(lines), size, label_hex, bd.FONT, "mm", spacing=-2)
        else:
            c.text(x + w / 2, y + h / 2, label, size, label_hex, bd.FONT, "mm")
        return label_hex

    def chip_text_box(self, name: str, x, y, w, h):
        """ChipText11: as wide as the inside of the chip's drawn frame and as tall as the chip, centred on that inside."""
        fl, fb, fr, ft = (v / 2 for v in frame_insets_px(np.asarray(self.kit[name]), self.borders[name]))
        centre = ft + max(1, h - ft - fb) * .5
        return x + fl, y + centre - h * .5, max(1, w - fl - fr), h


# ---------------------------------------------------------------------------------------------- dice
def die_image(px: int, faces=((5, "red"), (3, "blue"), (2, "red")), P=None) -> Image.Image:
    r = bd.Raster(int(px * 1.25), int(px * 1.3))
    pips = tuple(n for n, _ in faces)
    cols = tuple(bd.col(P["red"]) if k == "red" else bd.col(P["blue"]) if k == "blue" else bd.col("15110D") for _, k in faces)
    bd._die_layer(r, px * .625, px * .62, px, pips=pips, pip_rgb=cols)
    return r.export()


def draw_die(g: Game, c: Canvas, cx, cy, scale, faces):
    """A thrown die standing at (cx, cy): body about 101 x 127 units per unit of scale (GameApp.Play11)."""
    size = 101 * scale
    im = die_image(int(round(size * 1.6)), faces, g.P)
    w = size * 1.25
    c.box(cx - size * .42, cy + size * .52, size * .84, size * .1, "000000", .25)
    c.fit(im, cx - w / 2, cy - size * .7, w, size * 1.3)


# ---------------------------------------------------------------------------------------------- cards (W13 template)
CARDS = (  # (rarity, name, short text, stand-in illustration, enchant)
    ("common", "排头兵", "第一颗提交的骰子点数×4，加到基础分。", "C108"),
    ("advanced", "蓝色浪潮", "本手提交时，提交的骰子全部按蓝色计算。", "I10"),
    ("rare", "点金手", "每次提交后，最后一颗骰子朝上的面永久镀金。", "C117"),
    ("common", "配重", "这一面权重+1（上限5），更容易掷出这一面。", "U10"),
)
RARITY_KEY = {"common": "rarityCommon", "advanced": "rarityAdvanced", "rare": "rarityRare", None: "ghost"}


def price_button(g: Game, c: Canvas, x, y, w, h, price: int = 8, word: str = "购买"):
    """PriceButton13: SolidButton11 with the coin, the price (gold when it reads) and the word."""
    tint = g.sec()
    g.put(c, "ui_chip_formula", x, y, w, h, tint)
    label_hex, _ = g.label_on("ui_chip_formula", tint, g.P["textColor"])
    gold_hex, _ = g.label_on("ui_chip_formula", tint, g.P["gold"])
    coin, size = 24, 20 * g.fs
    total = coin + 4 + width_of(str(price), 22 * g.fs) + 8 + width_of(word, size)
    sx = x + (w - total) / 2
    c.fit(g.kit["ui_icon_coin"], sx, y + (h - coin) / 2, coin, coin)
    c.text(sx + coin + 4, y + h / 2, str(price), 22 * g.fs, gold_hex, bd.FONT, "lm")
    c.text(sx + coin + 4 + width_of(str(price), 22 * g.fs) + 8, y + h / 2, word, size, label_hex, bd.FONT, "lm")


def draw_card(g: Game, c: Canvas, x, y, w, h, size: str, rarity, title: str, text: str, pic: Image.Image,
              category: str = "骰面改造", price_w: float = 0.0, ordinal: int = 0, price: int = 8):
    """GameApp.Card11 DrawCard13: DrawTallCard13 (shop, table), DrawCompactCard13 (compact incl. tall, goods rows),
    DrawTile13 (tile); stand-in name, picture, text and price."""
    P = g.P
    goods = rarity is None
    tall_compact = size == "compact" and h >= 200
    face_name = "ui_card_shop" if size == "shop" else "ui_card_controller_compact" if size in ("compact", "goods", "tile") and h < 200 \
        else "ui_card_controller"
    mult = 2.0 if size == "tile" else 1.0
    g.put(c, face_name, x, y, w, h, mult=mult)
    fl, fb, fr, ft = (v / (2 * mult) for v in frame_insets_px(np.asarray(g.kit[face_name]), g.borders[face_name]))
    inner = (x + fl, y + ft, w - fl - fr, h - ft - fb)
    rl, rb, rr, rt = (v / 2 for v in ring_insets_px(np.asarray(g.kit["ui_card_frame_rarity"]), g.borders["ui_card_frame_rarity"]))
    tint = P[RARITY_KEY[rarity]]
    ink, _ = g.label_on(face_name, "FFFFFF", P["ink"])
    if size == "tile":
        content = (inner[0] + 2, inner[1] + 2, inner[2] - 4, inner[3] - 4)
        name_h = math.ceil(19.5 * 1.46) + 2
        lead = 22 if ordinal else 0
        block = max(24, min(content[2] - lead, content[3] - name_h - 2))
        bx = content[0] + lead if ordinal else content[0] + (content[2] - block) / 2
        by = content[1]
        g.put(c, "ui_card_frame_rarity", bx, by, block, block, tint)
        c.fit(pic, bx + rl + 1, by + rt + 1, min(block - rl - rr, block - rt - rb) - 2, min(block - rl - rr, block - rt - rb) - 2)
        if not goods:
            gem = min(16, block * .3)
            c.fit(g.kit["ui_gem_" + rarity], bx + block / 2 - gem / 2, by + block - gem - 1, gem, gem, tint=bd.col(tint))
        if ordinal:
            c.fit(g.kit["ui_pip_dot"], content[0], content[1], 20, 20, tint=g.c("ink"))
            c.text(content[0] + 10, content[1] + 10, str(ordinal), 12, P["paper"], bd.FONT, "mm")
        c.text(content[0] + content[2] / 2, content[1] + content[3] - name_h / 2, title, 19.5, ink, bd.FONT, "mm")
        return
    if size in ("compact", "goods"):
        content = (inner[0] + 4, inner[1] + 4, inner[2] - 8, inner[3] - 8)
        priced = size == "goods"
        pw = max(price_w, 100) if priced else 0
        limit = content[0] + content[2] - (pw + 8 if priced else 0)
        if tall_compact:
            block, gem_size = min(84, content[2] * .34), 22
            bx, by = content[0], content[1] + 6
        else:
            block, gem_size = min(content[3] - 8, 76), 18
            bx, by = content[0], content[1] + max(0, (content[3] - block - gem_size * .5) * .5)
        g.put(c, "ui_card_frame_rarity", bx, by, block, block, tint)
        side = min(block - rl - rr, block - rt - rb) - 2
        c.fit(pic, bx + rl + 1, by + rt + 1, side, side)
        if not goods:
            c.fit(g.kit["ui_gem_" + rarity], bx + block * .5 - gem_size * .5, by + block - gem_size * .5, gem_size, gem_size,
                  tint=bd.col(tint))
        tx, ty = bx + block + 10, content[1] + (4 if tall_compact else 2)
        if goods:
            c.text(tx, ty, category, 19.5 if priced else 15 * g.fs, g.label_on(face_name, "FFFFFF", "6E6050")[0], bd.BODY_FONT)
            ty += 26 * (g.fs if not priced else 1)
        disc = math.ceil(max(20, 14 * g.fs * 1.4)) if ordinal else 0
        name_x = tx + (disc + 6 if disc else 0)
        name_size = 20 * g.fs
        while name_size > 19.5 and width_of(title, name_size) > limit - name_x - 1:
            name_size -= 1
        name_h = math.ceil(name_size * 1.46) + 2
        if disc:
            c.fit(g.kit["ui_pip_dot"], tx, ty + (name_h - disc) / 2, disc, disc, tint=g.c("ink"))
            c.text(tx + disc / 2, ty + name_h / 2, str(ordinal), min(14, disc * .6 / g.fs) * g.fs, P["paper"], bd.FONT, "mm")
        c.text(name_x, ty + name_h / 2, title, name_size, ink, bd.FONT, "lm")
        top = ty + name_h
        if tall_compact:
            below = max(top, by + block + gem_size * .5 + 4)
            room = content[1] + content[3] - below
            lines = wrap(text, bd.BODY_FONT, 19.5, content[2])
            lh = 19.5 * 1.45 - 2.8
            if len(lines) * lh <= room + .5:
                for i, ln in enumerate(lines):
                    c.text(content[0], below + i * lh, ln, 19.5, ink, bd.BODY_FONT)
        else:
            room = content[1] + content[3] - top
            body = 19.5 if priced else g.body(18)
            lines = wrap(text, bd.BODY_FONT, body, limit - tx)
            lh = body * 1.45 - body * .16
            if len(lines) * lh + body * .16 <= room + .5:
                for i, ln in enumerate(lines):
                    c.text(tx, top + i * lh, ln, body, ink, bd.BODY_FONT)
        if priced:
            price_button(g, c, content[0] + content[2] - pw, content[1] + max(0, (content[3] - 60) / 2), pw, 60, price)
        return
    # Tall card (shop, table).
    g.put(c, "ui_card_frame_rarity", *inner, tint)
    content = (inner[0] + rl + 4, inner[1] + rt + 3, inner[2] - rl - rr - 8, inner[3] - rt - rb - 6)
    gem_size = 34 if size == "shop" else 30
    gem_y = inner[1] + (content[1] - 3 - inner[1]) * .5
    top = content[1] if goods else max(content[1], gem_y + gem_size * .5 + 2)
    disc = math.ceil(max(22, 14 * g.fs * 1.5)) if (size == "table" and ordinal) else 0
    price_h = 60 if size == "shop" else 0
    bottom = content[1] + content[3] - (price_h + 6 if price_h else 0)
    if goods:
        c.text(x + w / 2, top + 13, category, 16, "6E6050", bd.BODY_FONT, "mm")
        top += 26
    name_h = math.ceil(22 * g.fs * 1.46) + 2
    art_top = top + name_h + 2
    font = 20 * g.fs
    lines = wrap(text, bd.BODY_FONT, font, content[2])
    lh = font * 1.45 - font * .14
    art_size = max(40, min(content[2] - 8, bottom - art_top - 4 - (len(lines) * lh + 2)))
    c.fit(pic, x + (w - art_size) * .5, art_top, art_size, art_size)
    name_size = 22 * g.fs
    inset_l = disc + 4 if disc else 0
    while name_size > 19.5 and width_of(title, name_size) > content[2] - 2 * inset_l - 1:
        name_size -= 1
    c.text(x + w / 2, top + name_h / 2, title, name_size, ink, bd.FONT, "mm")
    for i, ln in enumerate(lines):
        c.text(x + w / 2, art_top + art_size + 4 + i * lh, ln, font, ink, bd.BODY_FONT, "ma")
    if price_h:
        price_button(g, c, content[0], content[1] + content[3] - price_h, content[2], price_h, price)
    if not goods:
        c.fit(g.kit["ui_gem_" + rarity], x + w * .5 - gem_size * .5, gem_y - gem_size * .5, gem_size, gem_size, tint=bd.col(tint))
    if disc:
        c.fit(g.kit["ui_pip_dot"], content[0], content[1], disc, disc, tint=g.c("ink"))
        c.text(content[0] + disc / 2, content[1] + disc / 2, str(ordinal), min(14, disc * .6 / g.fs) * g.fs, P["paper"],
               bd.FONT, "mm")


# ---------------------------------------------------------------------------------------------- the play screen
STATES = {
    # W11 capture states plus rolling, target reached, boss and pressure (comic README action-row table).
    "a": dict(primary=("锁定 2 颗", True), secondary="取消选择", end="end", hint="按“锁定”留下选中的骰子；再点一次骰子可以取消。",
              locked=0, selected=2, rolled=True),
    "b3": dict(primary=None, secondary=None, end="skip", hint="计分中…", locked=2, selected=0, rolled=True),
    "c": dict(primary=("掷骰 [R]", True), secondary=None, end="end", hint="按“掷骰”开始这一手。", locked=0, selected=0,
              rolled=False, kind="boss"),
    "f": dict(primary=("提交 2 颗", True), secondary="重掷 · 还能掷 2 [R]", end="end", hint="按“提交”计分，或者重掷其余的骰子。",
              locked=2, selected=0, rolled=True),
    "rolling": dict(primary=("重掷 · 还能掷 1 [R]", False), secondary=None, end="skip", hint="掷骰中…", locked=1,
                    selected=0, rolled=True),
    "qualified": dict(primary=("提交 2 颗", True), secondary="重掷 · 还能掷 2 [R]", end="qualified",
                      hint="已达标：可以继续提交，也可以结束本关。", locked=2, selected=0, rolled=True),
    "boss": dict(primary=("掷骰 [R]", True), secondary=None, end="end", hint="首领减益“满员”：要放满提交栏（5 颗）才能提交。",
                 locked=0, selected=0, rolled=False, kind="boss"),
    "pressure": dict(primary=("提交 2 颗", True), secondary="重掷 · 还能掷 2 [R]", end="end",
                     hint="按“提交”计分，或者重掷其余的骰子。", locked=2, selected=0, rolled=True, kind="pressure"),
}


def layout(large: bool) -> dict:
    """GameApp.Play11 L11."""
    info = 360 if large else 300
    return {"top": (16, 12, 1888, 72), "cards": (32, 96, 1448, 244), "stage": (1496, 96, 392, 244),
            "tray": (32, 352, 1448, 376), "dice": (52, 374, 1408, 276), "chips": (52, 660, 1408, 60),
            "resources": (1496, 352, 392, 376), "submit": (32, 740, 1448, 202), "info": (32, 740, info, 202),
            "rail": (32 + info + 12, 740, 1448 - info - 12, 202), "settle": (1496, 740, 392, 202),
            "hint": (32, 958, 600, 88) if large else (32, 958, 512, 88),
            "secondary": (644, 966, 240, 72) if large else (560, 966, 264, 72),
            "primary": (896, 962, 380, 80) if large else (840, 962, 400, 80), "end": (1496, 962, 392, 80)}


def stage_tint(g: Game, kind: str) -> str:
    return g.P["boss"] if kind == "boss" else g.P["pressure"] if kind == "pressure" else g.P["panelLight"]


def top_bar(g: Game, c: Canvas, kind: str, qualified: bool):
    P, fs, large = g.P, g.fs, g.large
    x0, y0, W, H = 16, 12, 1888, 72
    g.put(c, "ui_topbar", x0, y0, W, H, P["panel"])
    tag_w = 176 if large else 160
    tint = stage_tint(g, kind)
    label, tint_drawn = g.label_on("ui_chip_formula", tint)
    g.put(c, "ui_chip_formula", x0 + 8, y0 + 8, tag_w, 56, tint_drawn)
    c.text(x0 + 8 + tag_w / 2, y0 + 8 + 24 + 1, "第 9 关" if kind == "boss" else "第 4 关", (24 if large else 28) * fs, label,
           bd.FONT, "mm")
    dots_x = x0 + 8 + tag_w + 20
    kind_word = {"boss": "首领关", "pressure": "高压关"}.get(kind, "普通关")
    cap_size = g.body(17 if large else 20)
    muted, _ = g.label_on("ui_topbar", P["panel"], P["muted"])
    c.text(dots_x, y0 + 36 + 2, f"第 {3 if kind == 'boss' else 2} 章 · " + kind_word, cap_size, muted, bd.FONT, "la")
    dot = 14 if large else 20
    top = y0 + (8 if large else 12)
    chapter = 2 if kind == "boss" else 1
    for i in range(8):
        cur = i == chapter
        size = dot if cur else dot * .7
        colour = tint if cur else P["gold"] if i < chapter else P["panelLight"]
        dx = dots_x + i * 24 + (20 - size) * .5
        c.fit(g.kit["ui_pip_dot"], dx, top + (dot - size) * .5, size, size, tint=bd.col(colour))
        if cur:
            g.put(c, "ui_ring", dots_x + i * 24 + (20 - dot) * .5 - 3, top - 3, dot + 6, dot + 6, P["paper"], mult=6)
    cap_w = width_of(f"第 2 章 · {kind_word}", cap_size)
    fx = max(dots_x - x0 + 8 * 24 + 56, dots_x - x0 + cap_w + 20)
    gold_w = 232 if large else 176
    gold_x = 1668 - 16 - gold_w
    fw = min(640 if large else 560, gold_x - x0 - 16 - 284 - fx)
    fxa = x0 + fx
    g.put(c, "ui_info_inset", fxa, y0 + 6, fw, 60)
    score_size = (28 if large else 34) * fs
    score_hex, _ = g.label_on("ui_info_inset", "FFFFFF", P["gold"] if qualified else P["textColor"])
    score = "4,120" if qualified else "2,480"
    sw = min(fw * .55, width_of(score, score_size) + 4)
    c.text(fxa + 14 + sw, y0 + 6 + 24, score, score_size, score_hex, bd.FONT, "rm")
    target_hex = g.label_on("ui_info_inset", "FFFFFF", P["muted"])[0]
    if kind == "pressure":
        target_hex = cue_on(g, "ui_info_inset", "FFFFFF", P["pressure"])
    tx = fxa + 14 + sw * 1.12 + 8
    c.text(tx, y0 + 6 + 4 + 22, "/ 6,000" if kind == "pressure" else "/ 4,000", (26 if large else 32) * fs, target_hex,
           bd.FONT, "lm")
    ratio = 1.0 if qualified else .62
    track_w = fw - 28
    g.put(c, "ui_progress_track", fxa + 14, y0 + 6 + 48, track_w, 7, "07191C", mult=48 / 7)
    fill = P["gold"] if qualified else P["pressure"] if kind == "pressure" else P["blue"]
    g.put(c, "ui_progress_fill", fxa + 14, y0 + 6 + 48, max(7, track_w * ratio), 7, fill, mult=48 / 7)
    # Gold: icon, amount (gold when it reads), 金币; then 手册 and 暂停 (UiKit.Button, paper faces on the bronze plinth).
    c.fit(g.kit["ui_icon_coin"], gold_x + 6, y0 + 18, 36, 36)
    money_hex, _ = g.label_on("ui_topbar", P["panel"], P["gold"])
    c.text(gold_x + gold_w - (52 if large else 44), y0 + 36, "28", (30 if large else 34) * fs, money_hex, bd.FONT, "rm")
    c.text(gold_x + gold_w - (50 if large else 42), y0 + 36, "金币", g.body(17 if large else 20), muted, bd.FONT, "lm")
    for x, label in ((1668, "手册"), (1788, "暂停")):
        g.button(c, x, 18, 108, 60, label, 23 * fs)


def cue_on(g: Game, name: str, tint, cue: str) -> str:
    """GameApp.CueOn11: the stage cue keeps its hue, stepping 5 % toward white or black (at most 8) to reach 4.5:1."""
    fill = g.fill(name, tint)
    if bd.contrast(cue, fill) >= 4.5:
        return cue
    w1 = bd.hexc(bd.col(cue) + (1 - bd.col(cue)) * .05)
    b1 = bd.hexc(bd.col(cue) * .95)
    away = "FFFFFF" if bd.contrast(w1, fill) >= bd.contrast(b1, fill) else "000000"
    for n in range(1, 9):
        t = bd.hexc(bd.col(cue) + (bd.col(away) - bd.col(cue)) * .05 * n)
        if bd.contrast(t, fill) >= 4.5:
            return t
    return g.label_on(name, tint)[0]


def stage_card(g: Game, c: Canvas, kind: str):
    P, fs, large = g.P, g.fs, g.large
    x, y, w, h = 1496, 96, 392, 244
    g.put(c, "ui_stage_card", x, y, w, h, P["paper"])
    inner = w - 36
    if kind == "boss":
        g.put(c, "ui_ring", x + 4, y + 4, w - 16, h - 18, P["boss"])
        g.put(c, "ui_ring", x + 9, y + 9, w - 26, h - 28, P["boss"], alpha=.55)
    elif kind == "pressure":
        g.put(c, "ui_ring", x + 5, y + 5, w - 18, h - 20, P["pressure"])
    tint = stage_tint(g, kind)
    head_h = max(40, g.line(g.body(18 if large else 22) / fs))
    label, tint_drawn = g.label_on("ui_chip_formula", tint)
    g.put(c, "ui_chip_formula", x + 14, y + 14, inner, head_h, tint_drawn)
    title = "首领减益 · 二档" if kind == "boss" else "高压关" if kind == "pressure" else "普通关"
    c.text(x + 14 + 12, y + 14 + head_h / 2, title, g.body(18 if large else 22), label, bd.FONT, "lm")
    ink = g.label_on("ui_stage_card", P["paper"], P["ink"])[0]
    muted = g.label_on("ui_stage_card", P["paper"], "6E6050")[0]
    if kind == "boss":
        top = y + 14 + head_h + 6
        emb = 60 if large else 72
        g.put(c, "ui_chip_formula", x + 14, top + 4, emb, emb, P["rarityAdvanced"])
        for i in range(2):
            c.box(x + 14 + emb * .22, top + 4 + emb * .22 + i * emb * .17, emb * .56, emb * .08, P["ink"])
        c.text(x + 14 + emb + 12, top + 4, "满员", (20 if large else 28) * fs, ink, bd.FONT, "la")
        c.text(x + 14 + emb + 12, top + 46 * (fs if large else 1), "提交栏放满才能提交。", 20 * fs, ink, bd.BODY_FONT, "la")
        c.text(x + 14, top + max(4 + emb, 80) + 8, "通关奖励：首领战利品 3 选 1", g.body(18), bd.lerp_hex(P["ink"], P["boss"], .55),
               bd.BODY_FONT, "la")
        return
    ls = g.body(14 if large else 20)
    ty = y + 14 + head_h + 1
    c.text(x + 14, ty + g.line(ls / fs) / 2, "目标分数", ls, muted, bd.FONT, "lm")
    ty += g.line(ls / fs)
    goal = (28 if large else 42) * fs
    goal_hex = cue_on(g, "ui_stage_card", P["paper"], P["pressure"]) if kind == "pressure" else ink
    if kind == "pressure" and bd.contrast(P["pressure"], g.fill("ui_stage_card", P["paper"])) < 4.5:
        goal_hex = g.label_on("ui_stage_card", P["paper"], P["pressure"])[0]
    c.text(x + 14, ty + g.line(goal / fs) / 2, "6,000" if kind == "pressure" else "4,000", goal, goal_hex, bd.FONT, "lm")
    ty += g.line(goal / fs)
    c.text(x + 14, ty + g.line(ls / fs) / 2, "过关奖励 +10 金币" if kind == "pressure" else "过关奖励 +5 金币", ls, ink, bd.FONT, "lm")
    ty += g.line(ls / fs)
    note = "目标 ×1.5，过关金币 +5" if kind == "pressure" else "提交栏 5 格 · 每手掷 3 次"
    ns = g.body(14 if large else 17)
    c.text(x + 14, ty + g.line(ns / fs) / 2, note, ns, muted, bd.FONT, "lm")


POOL_FACES = (((4, "blue"), (2, "red"), (6, "blue")), ((3, "red"), (5, "red"), (1, "blue")),
              ((6, "blue"), (3, "blue"), (2, "red")), ((2, "red"), (6, "red"), (4, "blue")),
              ((5, "blue"), (1, "red"), (3, "blue")))


def badge(g: Game, c: Canvas, x, y, size, shape: str, colour: str):
    """Play11Badge (code-drawn): a red circle or a blue diamond with an ink edge and a paper rim."""
    r = bd.Raster(int(size * 2), int(size * 2))
    cx = cy = size
    if shape == "circle":
        d = bd.sd_circle(r.X, r.Y, cx, cy, size * .42)
    else:
        d = bd.sd_poly(r.X, r.Y, [(cx, cy - size * .5), (cx + size * .5, cy), (cx, cy + size * .5), (cx - size * .5, cy)])
    r.over(bd.col(g.P["paper"]), bd.coverage(d - size * .14))
    r.over(bd.col(g.P["ink"]), bd.coverage(d - size * .07))
    r.over(bd.col(colour), bd.coverage(d))
    c.fit(r.export(), x - size / 2, y - size / 2, size * 2, size * 2)


def tray(g: Game, c: Canvas, L: dict, s: dict, kind: str):
    P, fs, large = g.P, g.fs, g.large
    x, y, w, h = L["tray"]
    rim = bd.lerp_hex(P["panel"], P["panelLight"], .55)
    g.put(c, "ui_dice_tray", x, y, w, h, rim)
    if kind == "boss":
        g.put(c, "ui_ring", x + 6, y + 6, w - 12, h - 12, P["boss"])
        g.put(c, "ui_ring", x + 10, y + 10, w - 20, h - 20, P["boss"], alpha=.6)
    elif kind == "pressure":
        g.put(c, "ui_ring", x + 7, y + 7, w - 14, h - 14, P["pressure"], mult=1.6)
    else:
        g.put(c, "ui_ring", x + 7, y + 7, w - 14, h - 14, P["ghost"], alpha=.75, mult=1.6)
    tab_w = 120 if large else 100
    font = g.body(18 if large else 20)
    fl, fb, fr, ft = (v / 2 for v in frame_insets_px(np.asarray(g.kit["ui_chip_formula"]), g.borders["ui_chip_formula"]))
    tab_h = max(34, math.ceil(font + fb + ft + 4))
    caption = P.get("caption", P["panel"])
    label, tint_drawn = g.label_on("ui_chip_formula", caption, P["muted"])
    g.put(c, "ui_chip_formula", x + 24, y - 14, tab_w, tab_h, tint_drawn)
    bx, by, bw, bh = g.chip_text_box("ui_chip_formula", x + 24, y - 14, tab_w, tab_h)
    c.text(bx + bw / 2, by + bh / 2, "骰池", font, label, bd.FONT, "mm")
    pool = 5 - s["locked"]
    dx, dy, dw, dh = L["dice"]
    cell = min(272, dw / max(1, pool))
    scale = min(1.75, (cell - 30) / 101)
    start = dx + (dw - pool * cell) * .5
    cy = dy + dh * .5 - 10
    for j in range(pool):
        cx = start + j * cell + cell * .5
        faces = POOL_FACES[j % len(POOL_FACES)]
        draw_die(g, c, cx, cy, scale, faces)
        half_w, half_h = scale * 53, scale * 65
        if s["rolled"]:
            red = faces[0][1] == "red"
            badge(g, c, cx + half_w * .72, cy - half_h * .9, 24 if not large else 26, "circle" if red else "diamond",
                  P["red"] if red else P["blue"])
        if j < s["selected"]:
            g.put(c, "ui_ring", cx - half_w - 10, cy - half_h - 14, half_w * 2 + 20, half_h * 2 + 22, P["gold"])
            tw, th = min(cell - 8, 120 if large else 96), 36 if large else 32
            label, tint_drawn = g.label_on("ui_chip_formula", P["gold"])
            g.put(c, "ui_chip_formula", cx - tw / 2, cy + half_h + 10, tw, th, tint_drawn)
            tb = g.chip_text_box("ui_chip_formula", cx - tw / 2, cy + half_h + 10, tw, th)
            c.text(tb[0] + tb[2] / 2, tb[1] + tb[3] / 2, f"已选 {j + 1}", g.body(14 if large else 17), label, bd.FONT, "mm")
    if not s["rolled"]:
        c.box(dx - 4, dy - 6, dw + 8, dh + 8, P["shadow"], .32)
    formula_row(g, c, L, s)


def formula_row(g: Game, c: Canvas, L: dict, s: dict):
    P, fs, large = g.P, g.fs, g.large
    x, y, w, h = L["chips"]
    unlit = bd.lerp_hex(P["background"], P["panel"], .5)
    cap_font = g.body(17 if large else 20)
    on_felt = P.get("onFelt", P["textColor"])
    if s["locked"] == 0:
        caption = "本关还没有得分"
        g.put(c, "ui_chip_formula", x, y, w, h, unlit)
        col, _ = g.label_on("ui_chip_formula", unlit, P["muted"])
        c.text(x + w / 2, y + h / 2, caption, cap_font, col, bd.FONT, "mm")
        return
    caption = "计分中…" if s["end"] == "skip" and s["primary"] is None else "预计"
    cap_w = min(max(width_of(caption, cap_font) + 32, 120 if large else 96), 420 if large else 330)
    g.put(c, "ui_chip_formula", x, y, cap_w, h, unlit)
    col, _ = g.label_on("ui_chip_formula", unlit, P["gold"] if caption == "计分中…" else P["muted"])
    c.text(x + cap_w / 2, y + h / 2, caption, cap_font, col, bd.FONT, "mm")
    factors = (("基础分", "26", P["blue"]), ("一对", "×2", P["red"]), ("全蓝", "×3", P["blue"]), ("卡牌", "×1.5", P["gold"]))
    vf, sep, pad, nf = (22 if large else 26) * fs, (26 if large else 30), 30, 19.5 * fs
    cx = x + cap_w + 12
    for i, (name, value, tint) in enumerate(factors):
        cw = width_of(value, vf) + width_of(name, nf) + pad
        label, tint_drawn = g.label_on("ui_chip_formula", tint)
        g.put(c, "ui_chip_formula", cx, y, cw, h, tint_drawn)
        c.text(cx + 10, y + h / 2, name, nf, label, bd.FONT, "lm")
        c.text(cx + cw - 10, y + h / 2, value, vf, label, bd.FONT, "rm")
        cx += cw
        c.text(cx + sep / 2, y + h / 2, "×" if i < len(factors) - 1 else "=", (20 if large else 24) * fs, on_felt, bd.FONT, "mm")
        cx += sep
    total = "234"
    tw = min(max(width_of(total, (26 if large else 32) * fs) + 36, 140 if large else 130), 520 if large else 460)
    label, tint_drawn = g.label_on("ui_chip_formula", P["reward"])
    g.put(c, "ui_chip_formula", cx, y, tw, h, tint_drawn)
    c.text(cx + tw - 14, y + h / 2, total, (26 if large else 32) * fs, label, bd.FONT, "rm")


def resources(g: Game, c: Canvas, L: dict, s: dict):
    P, fs, large = g.P, g.fs, g.large
    x, y, w, h = L["resources"]
    g.put(c, "ui_panel_hud", x, y, w, h)
    ix, iw = x + 14, w - 28
    for i, (label, value, key) in enumerate((("还能掷", "2", "teal"), ("还能提交", "3", "gold"))):
        wy = y + 14 + i * 96
        g.put(c, "ui_info_inset", ix, wy, iw, 86)
        if i == 0:
            c.fit(g.kit["ui_icon_roll"], ix + 14, wy + 25, 36, 36)
        else:
            c.box(ix + 16, wy + 31, 32, 24, P["paper"])
            c.box(ix + 22, wy + 37, 20, 2, P["ink"])
            c.box(ix + 22, wy + 44, 14, 2, P["ink"])
        muted, _ = g.label_on("ui_info_inset", "FFFFFF", P["muted"])
        c.text(ix + 60, wy + 43, label, (20 if large else 24) * fs, muted, bd.FONT, "lm")
        vhex, _ = g.label_on("ui_info_inset", "FFFFFF", P[key])
        c.text(ix + iw - 14, wy + 43, value, (44 if large else 52) * fs, vhex, bd.FONT, "rm")
    ty = y + 210
    title = g.body(13 if large else 20)
    muted, _ = g.label_on("ui_panel_hud", "FFFFFF", P["muted"])
    c.text(ix, ty + g.line(title / fs) / 2, "道具 1 / 3", title, muted, bd.FONT, "lm")
    for i in range(3):
        px, py = ix + i * 75, ty + 34
        if i == 0:
            g.put(c, "ui_card_item", px, py, 64, 64)
            c.fit(art("I10"), px + 6, py + 5, 52, 52)
        else:
            g.put(c, "ui_ring", px, py, 64, 64, P["panelLight"])
    g.solid(c, ix, ty + 34 + 64 + 12, iw, 60, "查看骰子", 22 * fs, icon="ui_icon_dice_view")


def submit_row(g: Game, c: Canvas, L: dict, s: dict):
    P, fs, large = g.P, g.fs, g.large
    x, y, w, h = L["info"]
    g.put(c, "ui_panel_hud", x, y, w, h)
    tc, _ = g.label_on("ui_panel_hud", "FFFFFF", P["textColor"])
    c.text(x + 16, y + 10 + 18, f"提交栏 {s['locked']} / 5", (20 if large else 24) * fs, tc, bd.FONT, "lm")
    hy = y + 48
    if s["locked"]:
        gold, _ = g.label_on("ui_panel_hud", "FFFFFF", P["gold"])
        rs = g.body(15 if large else 18)
        lines = wrap("已锁定 · 本手不可移回骰池", bd.FONT, rs, w - 32)
        for i, ln in enumerate(lines):
            c.text(x + 16, hy + i * rs * 1.4, ln, rs, gold, bd.FONT, "la")
        hy += len(lines) * rs * 1.4 + 6
        hs = g.body(17 if large else 21)
        c.text(x + 16, hy, "预计：一对\n全蓝", hs, tc, bd.FONT, "la", spacing=2)
    else:
        muted, _ = g.label_on("ui_panel_hud", "FFFFFF", P["muted"])
        c.text(x + 16, hy, "锁定骰子后显示预计牌型", g.body(15 if large else 18), muted, bd.FONT, "la")
    rx, ry, rw, rh = L["rail"]
    rim = bd.lerp_hex(P["panel"], P["panelLight"], .55)
    g.put(c, "ui_dice_tray", rx, ry, rw, rh, rim)
    slots, gap = 5, 12
    inner = rw - 48
    slot_w = min(152, (inner - gap * (slots - 1)) / slots)
    sx = rx + 24 + (inner - (slots * slot_w + (slots - 1) * gap)) * .5
    sy = ry + 16
    on_felt = P.get("onFelt", P["muted"])
    for i in range(slots):
        xx = sx + i * (slot_w + gap)
        locked = i < s["locked"]
        ring = bd.lerp_hex(P["gold"], P["paper"], .15) if locked else P["ghost"]
        g.put(c, "ui_ring", xx, sy, slot_w, 170, ring, alpha=1.0 if locked else .75)
        c.text(xx + slot_w - 18, sy + 6 + 13, str(i + 1), (14 if large else 16) * fs, on_felt, bd.FONT, "mm")
        if locked:
            scale = min(1.1, min((slot_w - 38) / 101, (170 - 38) / 127))
            draw_die(g, c, xx + 2 + 50.5 * scale, sy + 38 + 63.5 * scale, scale, POOL_FACES[i + 2])
            g.put(c, "ui_tray_front", xx + 4, sy + 170 - 6, slot_w - 8, 20)
            c.fit(g.kit["ui_tag_lock"], xx + slot_w - 32 + 3, sy + 36, 22, 24)
        else:
            c.text(xx + slot_w / 2, sy + 60 + 24, "·  ·  ·", 20 * fs, on_felt, bd.FONT, "mm")


def settle(g: Game, c: Canvas, L: dict, s: dict, kind: str):
    P, fs, large = g.P, g.fs, g.large
    x, y, w, h = L["settle"]
    g.put(c, "ui_panel_hud", x, y, w, h)
    ix, iw, ty = x + 16, w - 32, y + 12
    tc, _ = g.label_on("ui_panel_hud", "FFFFFF", P["textColor"])
    muted, _ = g.label_on("ui_panel_hud", "FFFFFF", P["muted"])
    if s["end"] == "qualified":
        gold, _ = g.label_on("ui_panel_hud", "FFFFFF", P["gold"])
        head = (18 if large else 24) * fs
        c.text(ix, ty + g.line(head / fs) / 2, "现在结束 +9 金币", head, gold, bd.FONT, "lm")
        ty += g.line(head / fs) + 2
        ls = g.body(13 if large else 19)
        for line in ("过关奖励  +5", "剩余提交奖励  +2", "利息  +2"):
            c.text(ix, ty + g.line(ls / fs) / 2, line, ls, tc, bd.FONT, "lm")
            ty += g.line(ls / fs)
        return
    ls = g.body(18 if large else 22)
    c.text(ix, ty + g.line(ls / fs) / 2, "离目标还差", ls, muted, bd.FONT, "lm")
    ty += g.line(ls / fs)
    gs = (30 if large else 44) * fs
    gap_hex = cue_on(g, "ui_panel_hud", "FFFFFF", P["pressure"]) if kind == "pressure" else tc
    c.text(ix, ty + g.line(gs / fs) / 2, "3,520" if kind == "pressure" else "1,520", gs, gap_hex, bd.FONT, "lm")
    ty += g.line(gs / fs) + 2
    ns = g.body(15 if large else 18)
    lines = wrap("达标后结束：过关奖励 +5 金币，剩下的每次提交 +1", bd.BODY_FONT, ns, iw)
    for i, ln in enumerate(lines):
        c.text(ix, ty + i * (ns * 1.45 - ns * .1), ln, ns, muted, bd.BODY_FONT, "la")


def actions(g: Game, c: Canvas, L: dict, s: dict) -> dict:
    """DrawActions11. Returns the measured rectangles and the hint's text room (units under the last line)."""
    P, fs, large = g.P, g.fs, g.large
    hx, hy, hw, hh = L["hint"]
    g.put(c, "ui_panel_hud", hx, hy, hw, hh)
    size = g.body(16 if large else 21)
    smallest = g.body(13 if large else 17)
    lines = balanced_lines(s["hint"], bd.FONT, size, hw - 32)
    while size > smallest:
        if len(lines) * size * 1.45 - (len(lines) - 1) * size * .08 <= hh - 12 and \
                max(width_of(ln, size) for ln in lines) <= hw - 32:
            break
        size -= .5
    lh = size * 1.45 - size * .08
    block = len(lines) * lh
    top = hy + 6 + (hh - 12 - block) / 2
    tc, _ = g.label_on("ui_panel_hud", "FFFFFF", P["textColor"])
    for i, ln in enumerate(lines):
        c.text(hx + 16, top + i * lh + lh / 2, ln, size, tc, bd.FONT, "lm")
    # The CJK glyph box ends about 0.42 em below the line's middle (Noto Sans SC: ideographic descent ~0.12 em).
    glyph_bottom = top + (len(lines) - 1) * lh + lh / 2 + size * .42
    fl, fb, fr, ft = (v / 2 for v in frame_insets_px(np.asarray(g.kit["ui_panel_hud"]), g.borders["ui_panel_hud"]))
    room = hy + hh - fb - glyph_bottom
    used = {"hint": L["hint"]}
    if s["secondary"]:
        x, y, w, h = L["secondary"]
        g.solid(c, x, y, w, h, s["secondary"], (17 if large else 21) * fs, wrap_width=w - 16)
        used["secondary"] = L["secondary"]
    if s["primary"]:
        label, enabled = s["primary"]
        x, y, w, h = L["primary"]
        g.button(c, x, y, w, h, label, (22 if large else 28) * fs, enabled=enabled, primary=True,
                 label_colour=P["textColor"])
        used["primary+step"] = (x, y, w, h + 2)
    x, y, w, h = L["end"]
    if s["end"] == "skip":
        g.solid(c, x, y, w, h, "跳过演出", 24 * fs)
        used["skip"] = L["end"]
    elif s["end"] == "qualified":
        g.button(c, x, y, w, h, "结束本关 · 结算", (20 if large else 24) * fs)
        g.put(c, "ui_ring", x - 4, y - 4, w + 8, h + 4, P["reward"])
        used["end+step"] = (x, y, w, h + 2)
        used["reward ring"] = (x - 4, y - 4, w + 8, h + 4)
    else:
        g.button(c, x, y, w, h, "结束本关 · 结算", (20 if large else 24) * fs, enabled=False, label_colour=P["muted"],
                 two_line="达标后可结束")
        used["end+step"] = (x, y, w, h + 2)
    return {"used": used, "hint_lines": len(lines), "hint_size": size, "hint_room": room}


def cards_row(g: Game, c: Canvas, count: int, slots: int = 5):
    """GameApp.Play11 DrawCards11 (cards 32, 96, 1448, 244)."""
    P, large = g.P, g.large
    x0, y0, W, H = 32, 96, 1448, 244
    mini = count > 7 or (large and count > 5)
    if mini:
        shown = 9 if count > 10 else count
        cw, ch = (W - 4 * 12) / 5, (H - 10) / 2
        for i in range(shown):
            r, n, t, a = CARDS[i % len(CARDS)]
            draw_card(g, c, x0 + i % 5 * (cw + 12), y0 + i // 5 * (ch + 10), cw, ch, "compact", r, n, t, art(a), ordinal=i + 1)
        if count > 10:
            g.button(c, x0 + 4 * (cw + 12), y0 + ch + 10, cw, ch, f"全部 {count} 件 >", 22 * g.fs)
        return
    empty = max(0, slots - count)
    cap = 280 if large else 216
    card_w = 0 if count == 0 else min(cap, (W - empty * 68 - 12 * (count - 1)) / count)
    x = x0
    for i in range(count):
        r, n, t, a = CARDS[i % len(CARDS)]
        draw_card(g, c, x, y0, card_w, H, "compact" if large else "table", r, n, t, art(a), ordinal=i + 1)
        x += card_w + 12
    for _ in range(empty):
        if x + 56 > x0 + W + .5:
            break
        g.put(c, "ui_slot_inventory", x, y0 + 8, 56, H - 16)
        c.text(x + 28, y0 + H / 2, "+", 30 * g.fs, P["muted"], bd.FONT, "mm")
        x += 68


def play_screen(kit, borders, ppu, palette, table, state: str = "f", large: bool = False, cards: int = 3) -> tuple:
    g = Game(kit, borders, ppu, palette, 1.5 if large else 1.0)
    s = STATES[state]
    kind = s.get("kind", "normal")
    c = Canvas(1920, 1080, table)
    if kind == "boss":   # the felt darkens and a vignette closes in (felt shader _Dim11 .26, _Vignette11 .5)
        c.buf[..., :3] *= .74
    L = layout(large)
    top_bar(g, c, kind, s["end"] == "qualified")
    cards_row(g, c, cards)
    stage_card(g, c, kind)
    tray(g, c, L, s, kind)
    resources(g, c, L, s)
    submit_row(g, c, L, s)
    settle(g, c, L, s, kind)
    info = actions(g, c, L, s)
    return c.image(), info


# ---------------------------------------------------------------------------------------------- the W14 shop
SHOP_TOP, SHOP_GAP, SHOP_HEAD, SHOP_PAD = 92, 8, 68, 12


def shop_layout():
    """GameApp.Shop14 S14 at 100 % text."""
    avail = 1072 - SHOP_TOP - 2 * SHOP_GAP
    cards, build = SHOP_HEAD + 348 + 7, 4 + 28 + 2 + 104 + 7
    goods = avail - cards - build
    owned = SHOP_HEAD + 2 * 104 + 6 + 7
    leave = 96
    dice = avail - owned - leave
    y = SHOP_TOP
    return {"cards": (16, y, 1404, cards), "goods": (16, y + cards + SHOP_GAP, 1404, goods),
            "build": (16, y + cards + goods + 2 * SHOP_GAP, 1404, build), "dice": (1432, y, 472, dice),
            "owned": (1432, y + dice + SHOP_GAP, 472, owned), "leave": (1432, y + dice + owned + 2 * SHOP_GAP, 472, leave)}


def shop_screen(kit, borders, ppu, palette, page, notes: list) -> Image.Image:
    g = Game(kit, borders, ppu, palette, 1.0)
    P = g.P
    c = Canvas(1920, 1080, page)
    L = shop_layout()
    section = P.get("section", P["panel"])
    caption = P.get("caption", P["panelLight"])
    # Top bar (ShopTopBar14, ShopTopRight14).
    g.put(c, "ui_topbar", 16, 12, 1888, 72, P["panel"])
    tl, tb, tr, tt = (v / 2 for v in frame_insets_px(np.asarray(kit["ui_topbar"]), borders["ui_topbar"]))
    mid = 12 + tt + (72 - tt - tb) / 2
    key, _ = g.label_on("ui_topbar", P["panel"], P["textColor"])
    c.text(16 + tl + 16, mid, "商店 · 第 5 关前", 26, key, bd.FONT, "lm")
    c.text(16 + tl + 16 + width_of("商店 · 第 5 关前", 26) + 40, mid, "下一关：第 5 关 · 普通 · 目标 5,200", 20, key, bd.FONT, "lm")
    right = 1904 - 8
    icon_w = lambda label: 34 + 8 + width_of(label, 20) + 4 + 28
    pause = (right - icon_w("设置"), 18, icon_w("设置"), 60)
    right = pause[0] - 10
    hand = (right - icon_w("手册"), 18, icon_w("手册"), 60)
    right = hand[0] - 10
    cycle_w = 44 + 8 + width_of("查看本章", 20) + 4 + 28
    cycle = (right - cycle_w, 18, cycle_w, 60)
    right = cycle[0] - 16
    unit_w = width_of("金币", 20) + 4
    money_w = (width_of("19", 34) + 4) * 1.12 + 6
    gold_w = 40 + money_w + unit_w + 6
    gx = right - gold_w
    c.fit(kit["ui_icon_coin"], gx, 24, 36, 36)
    c.text(gx + gold_w - unit_w - 6, 48, "19", 34, g.label_on("ui_topbar", P["panel"], P["gold"])[0], bd.FONT, "rm")
    c.text(gx + gold_w - unit_w, 48, "金币", 20, g.label_on("ui_topbar", P["panel"], P["muted"])[0], bd.FONT, "lm")
    g.solid(c, *pause, "设置", 20, icon="ui_icon_gear")
    g.solid(c, *hand, "手册", 20, icon="ui_icon_book")
    k = g.solid(c, *cycle, "", 20)
    for i in range(3):
        size = 12 if i < 1 else 10
        c.fit(kit["ui_pip_dot"], cycle[0] + 16 + i * 16 + (12 - size) / 2, cycle[1] + (60 - size) / 2, size, size, tint=bd.col(k))
    c.text(cycle[0] + 16 + 44 + 8, cycle[1] + 30, "查看本章", 20, k, bd.FONT, "lm")
    # Sections (Section14) and 我的构筑 (ShopBuild14: the section panel without a strip).
    fl, fb, fr, ft = (v / 2 for v in frame_insets_px(np.asarray(kit["ui_section_panel"]), borders["ui_section_panel"]))
    panel_fill = g.fill("ui_section_panel", section)
    strip_fill = g.fill("ui_section_header", caption)
    title_key, title_tint = g.label_on("ui_section_header", caption, P["textColor"])
    muted_key, _ = g.label_on("ui_section_header", caption, P["muted"])
    notes.append(f"section fill {panel_fill} (ui_section_panel x {section}); caption strip {strip_fill} "
                 f"(ui_section_header x {caption}): titles {title_key} {bd.contrast(title_key, strip_fill):.2f}:1, "
                 f"notes {muted_key} {bd.contrast(muted_key, strip_fill):.2f}:1")
    titles = {"cards": "卡牌", "goods": "升级与服务", "dice": "骰子与骰面", "owned": "我的骰子 8/8 · 提交栏 5 格"}
    title_right = {}
    for name, (x, y, w, h) in L.items():
        if name == "leave":
            continue
        g.put(c, "ui_section_panel", x, y, w, h, section)
        if name in titles:
            g.put(c, "ui_section_header", x + fl, y + ft, w - fl - fr, SHOP_HEAD - ft - 2, title_tint)
            c.text(x + fl + 14, y + ft + (SHOP_HEAD - ft - 2) / 2, titles[name], 22, title_key, bd.FONT, "lm")
            title_right[name] = x + fl + 14 + width_of(titles[name], 22) + 2
    # 卡牌: 在售 and the refresh button in the strip, four offers (220 x 348, stride 232).
    x, y, w, h = L["cards"]
    word_w, price_w = width_of("刷新", 20) + 4, 26 + 4 + width_of("3", 22) + 4 + 4
    refresh_w = 16 + 34 + 8 + word_w + 10 + price_w + 16
    rx = x + w - SHOP_PAD - refresh_w
    k = g.solid(c, rx, y + 4, refresh_w, 60, "", 20)
    c.fit(kit["ui_icon_refresh"], rx + 16, y + 4 + 13, 34, 34, tint=bd.col(k))
    c.text(rx + 58, y + 34, "刷新", 20, k, bd.FONT, "lm")
    c.fit(kit["ui_icon_coin"], rx + 58 + word_w + 10, y + 4 + 17, 26, 26)
    c.text(rx + 58 + word_w + 40, y + 34, "3", 22, g.label_on("ui_chip_formula", g.sec(), P["gold"])[0], bd.FONT, "lm")
    c.text(title_right["cards"] + 16, y + ft + (SHOP_HEAD - ft - 2) / 2, "在售 4/4", 18, muted_key, bd.FONT, "lm")
    for i in range(4):
        r, n, t, a = CARDS[i]
        draw_card(g, c, x + SHOP_PAD + i * 232, y + SHOP_HEAD, 220, 348, "shop", r, n, t, art(a), price=(8, 9, 11, 8)[i])
    # 升级与服务: 指定升级 in the strip, nine goods rows (452 x 103, price 180).
    x, y, w, h = L["goods"]
    study = "指定升级 · 加价 2"
    study_w = width_of(study, 20) + 4 + 32
    g.solid(c, x + w - SHOP_PAD - study_w, y + 4, study_w, 60, study, 20)
    area = h - SHOP_HEAD - 7
    row_h = int(min(150, (area - 2 * 6) / 3))
    col_w = (w - 2 * SHOP_PAD - 2 * 12) / 3
    goods = (("牌型升级", "一对", "Lv.0 → Lv.1 · ×2 → ×2.5", "U10"), ("牌型升级", "全蓝", "Lv.1 → Lv.2 · ×3 → ×3.5", "U10"),
             ("牌型升级", "顺子", "Lv.0 → Lv.1 · ×4 → ×4.5", "U10"), ("道具", "蓝色浪潮", "下一次有效提交全部骰子临时视为蓝色。", "I10"),
             ("道具", "重来", "重掷一次，不消耗掷骰次数。", "I10"), ("道具", "点金", "选一颗骰子，这一面镀金。", "I10"),
             ("服务", "增加控制", "卡牌栏位 +1。", "O06"), ("服务", "删除骰子", "移除一颗骰子。", "O06"),
             ("服务", "进货", "下一家商店多一张卡。", "O06"))
    for i, (cat, title, text, a) in enumerate(goods):
        draw_card(g, c, x + SHOP_PAD + (i % 3) * (col_w + 12), y + SHOP_HEAD + (i // 3) * (row_h + 6), col_w, row_h, "goods",
                  None, title, text, art(a), category=cat, price_w=180, price=(2, 2, 3, 2, 1, 2, 3, 4, 2)[i])
    # 我的构筑: caption line, six tiles (100 x 104), the items.
    x, y, w, h = L["build"]
    bk = g.label_on("ui_section_panel", section, P["textColor"])[0]
    bm = g.label_on("ui_section_panel", section, P["muted"])[0]
    notes.append(f"我的构筑 on the section fill {panel_fill}: caption {bk} {bd.contrast(bk, panel_fill):.2f}:1, "
                 f"notes {bm} {bd.contrast(bm, panel_fill):.2f}:1")
    cy = y + 4 + 14
    c.text(x + SHOP_PAD + 2, cy, "我的构筑", 18, bk, bd.FONT, "lm")
    tx = x + SHOP_PAD + width_of("我的构筑", 18) + 18
    capacity = "卡牌栏位 5/5 · 共 6/10 张"
    c.text(tx, cy, capacity, 18, bm, bd.FONT, "lm")
    c.text(tx + width_of(capacity, 18) + 14, cy, "拖动调整顺序", 18, bm, bd.FONT, "lm")
    items_x = x + w - SHOP_PAD - (5 * 60 + 4 * 6)
    c.text(items_x, cy, "道具 1/3", 18, bk, bd.FONT, "lm")
    for i in range(6):
        r, n, t, a = CARDS[i % 4]
        draw_card(g, c, x + SHOP_PAD + i * 104, y + 34, 100, 104, "tile", r, n, t, art(a), ordinal=i + 1)
    for i in range(3):
        ix, iy = items_x + i * 66, y + 34 + 22
        if i == 0:
            g.put(c, "ui_card_item", ix, iy, 60, 60)
            c.fit(art("I10"), ix + 8, iy + 6, 44, 44)
        else:
            g.put(c, "ui_slot_inventory", ix, iy, 60, 60, P["panelLight"])
    # 骰子与骰面: two new dice and two face upgrades (448 x 121, price 150).
    x, y, w, h = L["dice"]
    row_h = int((h - SHOP_HEAD - 7 - 3 * 6) / 4)
    rows = (("新骰子 · 点数和 21", "红蓝各半", "", die_image(120, ((5, "red"), (2, "blue"), (3, "red")), P)),
            ("新骰子 · 点数和 24", "大点蓝骰", "", die_image(120, ((6, "blue"), (4, "blue"), (4, "red")), P)),
            ("骰面改造", "配重", "这一面权重+1（上限5），更容易掷出这一面。", art("U10")),
            ("骰面改造", "抛光", "这一面改为光滑。", art("U10")))
    for i, (cat, title, text, pic) in enumerate(rows):
        draw_card(g, c, x + SHOP_PAD, y + SHOP_HEAD + i * (row_h + 6), w - 2 * SHOP_PAD, row_h, "goods", None, title, text, pic,
                  category=cat, price_w=150, price=(5, 5, 2, 1)[i])
    # 我的骰子: eight cells.
    x, y, w, h = L["owned"]
    gap = (w - 2 * SHOP_PAD - 4 * 104) / 3
    for i in range(8):
        cx, cy2 = x + SHOP_PAD + (i % 4) * (104 + gap), y + SHOP_HEAD + (i // 4) * 110
        g.put(c, "ui_slot_inventory", cx, cy2, 104, 104, P["panelLight"])
        c.fit(die_image(96, POOL_FACES[i % 5], P), cx + 8, cy2 + 6, 88, 88)
    # 下一关 (ShopLeave14): UiKit.ActionButton, primary, label LabelOn11 on the face, 34 bold, the arrow after it.
    x, y, w, h = L["leave"]
    face = g.button(c, x, y, w, h, "", 34, primary=True)
    key, _ = g.label_on("ui_button_bevel", face)
    notes.append(f"下一关: face {g.fill('ui_button_bevel', face)}, label {key} "
                 f"{bd.contrast(key, g.fill('ui_button_bevel', face)):.2f}:1, plinth {P['buttonStepPrimary']}")
    label_w, arrow = width_of("下一关", 34), 42
    sx = x + (w - (label_w + 10 + arrow)) / 2
    c.text(sx, y + (h - 7) / 2, "下一关", 34, key, bd.FONT, "lm")
    c.fit(kit["ui_icon_arrow"], sx + label_w + 10, y + (h - 7 - arrow) / 2, arrow, arrow, tint=bd.col(key))
    return c.image()


# ---------------------------------------------------------------------------------------------- sheets
def caption_bar(im: Image.Image, text: str, size: int = 20, colour=LABEL) -> Image.Image:
    font = ImageFont.truetype(str(bd.BODY_FONT), size)
    lines = text.split("\n")
    bar_h = 10 + len(lines) * (size + 6)
    out = Image.new("RGB", (im.width, im.height + bar_h), (24, 20, 16))
    d = ImageDraw.Draw(out)
    for i, ln in enumerate(lines):
        d.text((12, 6 + i * (size + 6)), ln, fill=colour, font=font)
    out.paste(im, (0, bar_h))
    return out


def stack(images, gap: int = 8, bg=(24, 20, 16)) -> Image.Image:
    out = Image.new("RGB", (max(i.width for i in images), sum(i.height for i in images) + gap * (len(images) - 1)), bg)
    y = 0
    for im in images:
        out.paste(im, (0, y))
        y += im.height + gap
    return out


def palette_sheet(palette: dict) -> Image.Image:
    report = bd.palette_report(palette)
    keys = list(palette)
    sw, rows = 300, (len(keys) + 3) // 4
    im = Image.new("RGB", (4 * sw + 40 + 760, max(rows * 64 + 60, 40 + len(report) * 22)), (24, 20, 16))
    d = ImageDraw.Draw(im)
    f, small = ImageFont.truetype(str(bd.LATIN), 18), ImageFont.truetype(str(bd.LATIN), 15)
    d.text((20, 12), "DECO PALETTE (theme.json, written by build_deco.py)", fill=LABEL, font=f)
    for i, k in enumerate(keys):
        x, y = 20 + (i % 4) * sw, 44 + (i // 4) * 64
        d.rectangle((x, y, x + 52, y + 52), fill=bd.rgb8(palette[k]), outline=(90, 80, 60))
        d.text((x + 62, y + 6), k, fill=LABEL, font=f)
        d.text((x + 62, y + 30), palette[k], fill=NOTE, font=small)
    x0 = 4 * sw + 40
    d.text((x0, 12), "CONTRAST AND DISTANCE (palette_report)", fill=LABEL, font=f)
    for i, (k, v) in enumerate(report.items()):
        d.text((x0, 44 + i * 22), f"{k}", fill=NOTE, font=small)
        d.text((x0 + 640, 44 + i * 22), f"{v:6.2f}", fill=LABEL, font=small)
    return im


def sprite_sheet(kit, borders, ppu, palette, names) -> Image.Image:
    """The drawn sprites: each at 1:1 sprite pixels on a mid grey, and (tinted ones) at a typical size and tint in game
    units on the felt."""
    g = Game(kit, borders, ppu, palette)
    P = palette
    tints = {"ui_button_bevel": [("primary", P["primary"]), ("paper", P["paper"]), ("buttonStep", P["buttonStep"])],
             "ui_chip_formula": [("sec", g.sec()), ("gold", P["gold"]), ("red", P["red"]), ("caption", P["caption"])],
             "ui_topbar": [("panel", P["panel"])], "ui_stage_card": [("paper", P["paper"])],
             "ui_ring": [("ghost", P["ghost"]), ("gold", P["gold"]), ("boss", P["boss"])],
             "ui_dice_tray": [("tray rim", bd.lerp_hex(P["panel"], P["panelLight"], .55))],
             "ui_section_header": [("caption", P["caption"])]}
    cells = []
    font = ImageFont.truetype(str(bd.LATIN), 15)
    for name in names:
        im = kit[name]
        thumb = im.copy()
        thumb.thumbnail((256, 256), Image.Resampling.LANCZOS)
        cell = Image.new("RGB", (560, 300), (24, 20, 16))
        bg = Image.new("RGBA", thumb.size, (96, 96, 100, 255))
        bg.alpha_composite(thumb)
        cell.paste(bg.convert("RGB"), (8, 30))
        d = ImageDraw.Draw(cell)
        ins = frame_insets_px(np.asarray(im), borders[name]) if any(borders[name]) else "-"
        d.text((8, 6), f"{name}  {im.width}x{im.height}  insets(px L,B,R,T)={ins}", fill=LABEL, font=font)
        if name in tints:
            c = Canvas(280, 250, P["felt"])
            for i, (label, t) in enumerate(tints[name]):
                if name == "ui_dice_tray":
                    g.put(c, name, 10, 20, 250, 200, t)
                elif name == "ui_section_header":
                    g.put(c, name, 6, 20 + i * 70, 268, 59, t)
                else:
                    g.put(c, name, 12 + (i % 2) * 134, 16 + (i // 2) * 110, 124, 64 if name != "ui_ring" else 90, t)
            cell.paste(c.image(), (272, 30))
        cells.append(cell)
    cols = 3
    sheet = Image.new("RGB", (cols * 568, ((len(cells) + cols - 1) // cols) * 308), (24, 20, 16))
    for i, cell in enumerate(cells):
        sheet.paste(cell, ((i % cols) * 568, (i // cols) * 308))
    return sheet


# Concept crops (page, box) beside the deco pieces they gave their shape to (README "Concept features").
CONCEPT_SOURCES = (
    ("card frame: gold band, black gap, chamfer (roll.png)", "roll", (350, 40, 450, 120), "ui_card_controller", None, (0, 0, 120, 96)),
    ("HUD plates: gold double rule on lacquer (roll.png)", "roll", (40, 268, 310, 360), "ui_panel_hud", None, (0, 0, 220, 96)),
    ("crimson corner triangles (card.png)", "card", (40, 270, 160, 420), "ui_panel_hud", None, (0, 0, 96, 96)),
    ("ivory plaque buttons (roll.png)", "roll", (620, 800, 860, 868), "ui_button_bevel", "paper", (0, 0, 200, 72)),
    ("crimson main button (shop.png)", "shop", (1300, 830, 1650, 910), "ui_button_bevel", "primary", (0, 0, 200, 72)),
    ("crimson section tab with a pointed end (shop.png)", "shop", (300, 84, 700, 120), "ui_section_header", "caption", None),
    ("chamfered tray (triptych.png)", "triptych", (215, 195, 640, 420), "ui_dice_tray", "rim", (0, 0, 200, 140)),
    ("bottom fan on a stepped plinth (roll.png)", "roll", (730, 860, 960, 941), None, None, None),
)


def concept_sheet(kit, borders, ppu, palette, table) -> Image.Image:
    g = Game(kit, borders, ppu, palette)
    P = palette
    font = ImageFont.truetype(str(bd.LATIN), 16)
    rows = []
    for label, page, box, name, tint, crop in CONCEPT_SOURCES:
        with Image.open(ROOT / bd.CONCEPTS / f"{page}.png") as im:
            src = im.convert("RGB").crop(box)
        s = 260 / max(src.width, src.height) * 1.6
        src = src.resize((round(src.width * s), round(src.height * s)), Image.Resampling.LANCZOS)
        if name is None:
            fan = table.crop((1270, 940, 1500, 1080))
            mine = fan.resize((fan.width * 2, fan.height * 2), Image.Resampling.LANCZOS)
        else:
            c = Canvas(460, 260, P["felt"])
            t = {"paper": P["paper"], "primary": P["primary"], "caption": P["caption"],
                 "rim": bd.lerp_hex(P["panel"], P["panelLight"], .55)}.get(tint)
            if name == "ui_section_header":
                fl, fb, fr, ft = (v / 2 for v in frame_insets_px(np.asarray(kit["ui_section_panel"]), borders["ui_section_panel"]))
                g.put(c, "ui_section_panel", 4, 4, 452, 120, P["section"])
                g.put(c, name, 4 + fl, 4 + ft, 452 - fl - fr, 68 - ft - 2, t)
            elif name == "ui_dice_tray":
                g.put(c, name, 10, 10, 440, 240, t)
                g.put(c, "ui_ring", 17, 17, 426, 226, P["ghost"], alpha=.75, mult=1.6)
            else:
                g.put(c, name, 10, 10, 440 if crop is None else crop[2] * 2, 240 if crop is None else crop[3] * 2, t)
            mine = c.image()
        row = Image.new("RGB", (src.width + mine.width + 40, max(src.height, mine.height) + 30), (24, 20, 16))
        ImageDraw.Draw(row).text((10, 4), label, fill=LABEL, font=font)
        row.paste(src, (10, 26))
        row.paste(mine, (src.width + 30, 26))
        rows.append(row)
    return caption_bar(stack(rows, 6), "CONCEPT CROPS (left, ArtSource/StyleKit/themes/deco/concepts) and the deco pieces drawn "
                                       "from them (right, offline, on the felt, tinted as the game tints them)", 16)


def main(argv=()):
    kit, borders, ppu, palette, table, page, fallback = load_kit()
    OUT.mkdir(parents=True, exist_ok=True)
    chunks = [", ".join(fallback[i:i + 11]) for i in range(0, len(fallback), 11)]
    fb = ",\n    ".join(chunks)
    note = ("OFFLINE MOCK (Python emulation of GameApp.Play11 / Card11 / Shop14 / UiKit; not a Unity capture). Stand-in "
            "card art: default illustrations; 3D dice: flat stand-ins.\nDefault-theme fallbacks (deco sprite not drawn "
            f"yet, {len(fallback)}): " + (fb if fb else "none"))
    states = [s for s in argv if s in STATES] or ["a", "f"]
    strips = []
    for state in states:
        for large in (False, True):
            im, info = play_screen(kit, borders, ppu, palette, table, state, large)
            tag = f"{state}_{150 if large else 100}"
            text = (f"STATE {state}  TEXT {150 if large else 100} %   hint: {info['hint_lines']} line(s) at "
                    f"{info['hint_size']:.1f} units, {info['hint_room']:.1f} units under the last line to the frame\n" + note)
            caption_bar(im, text, 18).save(OUT / f"play_{tag}.png", optimize=False, compress_level=9)
            strips.append(caption_bar(im.resize((960, 540), Image.Resampling.LANCZOS), f"{state} {150 if large else 100} %", 18))
            print(f"DECO_PLAY {tag}: hint {info['hint_lines']} line(s) {info['hint_size']:.1f} units, room "
                  f"{info['hint_room']:.1f} units; " + "; ".join(f"{k} {v}" for k, v in info["used"].items()))
    pairs = [stack([strips[i], strips[i + 1]], 0) for i in range(0, len(strips), 2)]
    sheet = Image.new("RGB", (pairs[0].width * 2 + 8, max(p.height for p in pairs) * ((len(pairs) + 1) // 2)), (24, 20, 16))
    for i, p in enumerate(pairs):
        sheet.paste(p, ((i % 2) * (p.width + 8), (i // 2) * p.height))
    caption_bar(sheet, note, 16).save(OUT / "play_states.png", optimize=False, compress_level=9)
    notes = []
    shop = shop_screen(kit, borders, ppu, palette, page, notes)
    caption_bar(shop, "W14 SHOP (GameApp.Shop14 S14, 100 % text) over Table/page_scene.png with the theme's section, caption "
                      "and button-step tints (W20X)\n" + note + "\n" + "\n".join(notes), 16).save(
        OUT / "shop_w14.png", optimize=False, compress_level=9)
    for n in notes:
        print("DECO_SHOP", n)
    palette_sheet(palette).save(OUT / "palette.png", optimize=False, compress_level=9)
    drawn_names = [n for n in bd.PHASE1_NAMES if (THEME / "UI" / (n + ".png")).is_file()]
    sprite_sheet(kit, borders, ppu, palette, drawn_names).save(OUT / "sprites.png", optimize=False, compress_level=9)
    concept_sheet(kit, borders, ppu, palette, table).save(OUT / "concept_sources.png", optimize=False, compress_level=9)
    # The two palette questions of README "需要决定" side by side: the current palette, a gold main button (the other
    # kits' convention), a dark-bronze background (the secondary buttons' mix gets lighter than the felt).
    variants = (("CURRENT: primary 9E1D22 crimson, background 0D0A08", {}),
                ("ALTERNATIVE 1: primary = gold E2B05A (plinth stays gold)", {"primary": palette["gold"]}),
                ("ALTERNATIVE 2: background 3A2E22 (dark bronze): secondary chip = lerp(background, panel, .55)",
                 {"background": "3A2E22"}))
    rows = []
    for title, change in variants:
        p = dict(palette, **change)
        im, _ = play_screen(kit, borders, ppu, p, table, "f", False)
        top = im.crop((0, 0, 1920, 96))
        bottom = im.crop((0, 640, 1920, 1080))
        sec = bd.lerp_hex(p["background"], p["panel"], .55)
        rows.append(caption_bar(stack([top, bottom], 4), f"{title}   (secondary chip {sec}: "
                                                         f"{bd.contrast(sec, p['felt']):.2f}:1 to the felt)", 18))
    caption_bar(stack(rows, 10), "PALETTE ALTERNATIVES (offline mock, state f, 100 % text): top bar and the lower half of the "
                                 "play screen", 18).save(OUT / "alternatives.png", optimize=False, compress_level=9)
    print("DECO_REVIEW", OUT.relative_to(ROOT).as_posix(), sorted(p.name for p in OUT.iterdir()))


if __name__ == "__main__":
    main(sys.argv[1:])
