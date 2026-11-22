#!/usr/bin/env python3
"""artgen: standardised AI art generation for the DicePrototype project.

Every generated image is tied to one versioned style kit
(ArtSource/StyleKit/style_kit.json): the same anchor plate, the same fixed
prompt blocks; only the subject changes. Generation runs through the locally
installed Codex CLI (ChatGPT sign-in, built-in image tool). After each run the
tool reads the Codex session log and checks, from the image tool's own
generation record, that the model really received the pinned reference images
(by SHA-256), the exact composed prompt and a transparent-background request.
A run that fails any check is recorded as INVALID_RUN. The untouched original
is archived with a record and a review sheet is built next to it.

The tool never edits production assets, never approves art and never
overwrites an earlier revision. Usage: docs/art/AI_ART_PIPELINE.md.
"""
from __future__ import annotations

import argparse
import datetime as dt
import glob
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

# artgen-1.2 (V1.1 W19): review sheets also work for theme kits whose palettes do not name ivory, petrol and ink.
TOOL_VERSION = "artgen-1.2"
ROOT = Path(__file__).resolve().parents[2]
KIT_DEFAULT = Path("ArtSource/StyleKit/style_kit.json")
CONFIG_DEFAULT = Path("DicePrototype/Assets/Game/Data/P0Config.json")
LANCZOS = getattr(Image, "Resampling", Image).LANCZOS
CONTENT_LISTS = ("controllers", "consumables", "services", "diceUpgrades", "lootKinds")
LOOK_TOLERANCE = 0.03

# Nearest-colour roles used only for drift indicators (never a pass/fail gate).
ROLE_CENTERS = [
    ("dark", "162629"), ("dark", "173E40"), ("dark", "203F43"), ("dark", "0E1F21"),
    ("ivory", "F4E8CB"), ("ivory", "F7EED9"), ("ivory", "E6D6B2"), ("ivory", "FFFFFF"),
    ("coral", "EC655D"), ("coral", "C9473F"), ("coral", "F28C82"),
    ("cyan", "55B6D9"), ("cyan", "3A8FB3"), ("cyan", "8FD0E8"),
    ("amber", "F3BA58"), ("amber", "D9952F"), ("amber", "F8D58E"),
    ("other", "79C99C"), ("other", "AD92DB"),
]
ROLE_ORDER = ["dark", "ivory", "coral", "cyan", "amber", "other"]
OTHER_DISTANCE = 70.0


class ArtGenError(Exception):
    pass


# ---------------------------------------------------------------- utilities

def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def load_json(path: Path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def write_text_lf(path: Path, text: str) -> None:
    """Write UTF-8 with LF line endings on every platform, so file hashes are stable."""
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)


def write_json(path: Path, obj) -> None:
    write_text_lf(path, json.dumps(obj, ensure_ascii=False, indent=2) + "\n")


def hex_rgb(value: str) -> tuple:
    value = value.lstrip("#")
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


def utc_now() -> str:
    return dt.datetime.now(dt.timezone.utc).isoformat()


def rel(path: Path, root: Path) -> str:
    try:
        return Path(path).resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path)


def norm_path(value: str) -> str:
    return os.path.normcase(os.path.normpath(value.replace("/", os.sep)))


def norm_prompt(value: str) -> str:
    return value.replace("\r\n", "\n").strip()


# ---------------------------------------------------------------- style kit

def load_kit(root: Path, kit_path: Path) -> dict:
    path = kit_path if kit_path.is_absolute() else root / kit_path
    if not path.is_file():
        raise ArtGenError(f"style kit not found: {path}")
    kit = load_json(path)
    kit["_path"] = path
    return kit


def kit_category(kit: dict, name: str) -> dict:
    try:
        return kit["categories"][name]
    except KeyError:
        raise ArtGenError(f"style kit has no category '{name}'") from None


def file_set_sha(root: Path, paths: list) -> str:
    h = hashlib.sha256()
    for p in paths:
        h.update(p.encode("utf-8") + b"\0" + sha256_file(root / p).encode("ascii") + b"\n")
    return h.hexdigest()


def verify_kit(kit: dict, root: Path) -> list:
    """Return a list of problems; empty means every pinned file matches its SHA-256."""
    problems = []
    for cname, cat in kit.get("categories", {}).items():
        anchors = cat.get("anchors", [])
        if not anchors:
            problems.append(f"{cname}: no anchors")
        for anchor in anchors:
            path = root / anchor["path"]
            if not path.is_file():
                problems.append(f"{cname}: anchor {anchor['id']} missing: {anchor['path']}")
            elif not anchor.get("sha256"):
                problems.append(f"{cname}: anchor {anchor['id']} has no pinned sha256 (run pin-anchors)")
            elif sha256_file(path) != anchor["sha256"]:
                problems.append(f"{cname}: anchor {anchor['id']} changed since it was pinned: {anchor['path']}")
        plate = cat.get("plate")
        if plate:
            path = root / plate["path"]
            if not path.is_file():
                problems.append(f"{cname}: plate missing: {plate['path']} (run build-plate)")
            elif not plate.get("sha256"):
                problems.append(f"{cname}: plate has no pinned sha256 (run build-plate)")
            elif sha256_file(path) != plate["sha256"]:
                problems.append(f"{cname}: plate changed since it was pinned: {plate['path']}")
        ranges = cat.get("indicatorRanges")
        if ranges:
            missing = [p for p in ranges.get("files", []) if not (root / p).is_file()]
            if missing:
                problems.append(f"{cname}: {len(missing)} range reference file(s) missing, e.g. {missing[0]}")
            elif file_set_sha(root, ranges.get("files", [])) != ranges.get("setSha256"):
                problems.append(f"{cname}: indicator range reference set changed since ranges were computed")
    return problems


def pin_anchors(kit: dict, root: Path) -> list:
    """Fill empty anchor sha256 fields. Never overwrites an existing (possibly mismatching) value."""
    pinned = []
    for cname, cat in kit.get("categories", {}).items():
        for anchor in cat.get("anchors", []):
            if anchor.get("sha256"):
                continue
            path = root / anchor["path"]
            if not path.is_file():
                raise ArtGenError(f"{cname}: anchor {anchor['id']} missing: {anchor['path']}")
            anchor["sha256"] = sha256_file(path)
            pinned.append(f"{cname}/{anchor['id']}")
    return pinned


def save_kit(kit: dict) -> None:
    data = {k: v for k, v in kit.items() if not k.startswith("_")}
    write_json(kit["_path"], data)


def fit_into(img: Image.Image, box: int) -> Image.Image:
    img = img.copy()
    img.thumbnail((box, box), LANCZOS)
    return img


def render_plate(cat: dict, root: Path) -> Image.Image:
    plate = cat["plate"]
    cols, cell, pad = int(plate["columns"]), int(plate["cell"]), int(plate.get("padding", 24))
    anchors = cat["anchors"]
    rows = (len(anchors) + cols - 1) // cols
    canvas = Image.new("RGB", (cols * cell, rows * cell), hex_rgb(plate["background"]))
    for i, anchor in enumerate(anchors):
        art = fit_into(Image.open(root / anchor["path"]).convert("RGBA"), cell - 2 * pad)
        x = (i % cols) * cell + (cell - art.width) // 2
        y = (i // cols) * cell + (cell - art.height) // 2
        canvas.paste(art, (x, y), art)
    return canvas


def build_plate(kit: dict, cname: str, root: Path, replace: bool = False) -> str:
    cat = kit_category(kit, cname)
    problems = [p for p in verify_kit(kit, root) if p.startswith(f"{cname}: anchor")]
    if problems:
        raise ArtGenError("; ".join(problems))
    image = render_plate(cat, root)
    path = root / cat["plate"]["path"]
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(".tmp.png")
    image.save(tmp, format="PNG")
    digest = sha256_file(tmp)
    if path.exists() and sha256_file(path) != digest and not replace:
        tmp.unlink()
        raise ArtGenError(f"{path} exists with different content; a new plate needs a new kit version (use --replace only with a version bump)")
    os.replace(tmp, path)
    cat["plate"]["sha256"] = digest
    cat["plate"]["builtWith"] = f"{TOOL_VERSION}; Pillow {Image.__version__}"
    return digest


GEOMETRY_KEYS = ("transparentShare", "subjectWidthShare", "subjectHeightShare")


def compute_ranges(kit: dict, cname: str, root: Path, pattern: str) -> dict:
    """Indicator min/max over an explicit, pinned file list (the shipped set), stored in the kit.

    Colour shares use every file. Geometry shares (canvas coverage) only make sense for the square
    canvas new illustrations are delivered on, so they use the square files only.
    """
    files = sorted(rel(Path(p), root) for p in glob.glob(str(root / pattern)))
    if not files:
        raise ArtGenError(f"no files match {pattern}")
    images = [Image.open(root / f) for f in files]
    values = [indicators(img) for img in images]
    square = [v for v, img in zip(values, images) if img.width == img.height]
    if not square:
        raise ArtGenError("no square files in the range set")
    ranges = {}
    for key in values[0].keys():
        pool = square if key in GEOMETRY_KEYS else values
        ranges[key] = [round(min(v[key] for v in pool), 4), round(max(v[key] for v in pool), 4)]
    entry = {"source": pattern, "count": len(files), "squareCount": len(square),
             "geometryKeysSquareOnly": list(GEOMETRY_KEYS), "computedWith": TOOL_VERSION, "files": files,
             "setSha256": file_set_sha(root, files), "ranges": ranges}
    kit_category(kit, cname)["indicatorRanges"] = entry
    return entry


# ---------------------------------------------------------------- content + prompt

def load_content(root: Path, config_rel: Path, content_id: str) -> dict:
    config_path = root / config_rel
    config = load_json(config_path)
    for key in CONTENT_LISTS:
        for entry in config.get(key, []):
            if entry.get("id") == content_id:
                return {
                    "contentId": content_id,
                    "list": key,
                    "displayName": entry.get("displayName"),
                    "configDescription": entry.get("description"),
                    "configPath": config_rel.as_posix(),
                    "configSha256": sha256_file(config_path),
                }
    raise ArtGenError(f"content id {content_id} not found in {config_rel}")


AGENT_TEMPLATE = """This is a non-interactive art-production call. Do not run shell commands, do not edit files, and do not \
read skills or other files: the arguments below are complete.
Call the image generation tool (image_gen.imagegen) exactly once with exactly these arguments:
- prompt: the text between <<< and >>> below, verbatim and complete
- referenced_image_paths: {paths_json}
- transparent_background: {transparent}
The reference images, in order, have these roles:
{roles}
<<<
{prompt}
>>>
When the tool has returned, reply with exactly one line: the absolute path of the saved image file, or UNKNOWN."""


def compose(kit: dict, job: dict, root: Path, content: dict | None = None) -> dict:
    cat = kit_category(kit, job["category"])
    refs = [{"path": cat["plate"]["path"], "sha256": cat["plate"].get("sha256"), "role": cat["plateRole"]}]
    for extra in job.get("extraReferences", []) or []:
        refs.append({"path": extra["path"], "sha256": None, "role": extra["role"]})
    edit = job.get("edit")
    if edit:
        refs.append({"path": edit["target"], "sha256": None,
                     "role": "EDIT TARGET: the image to modify. Keep its subject, composition, colours and line work; "
                             "change only what the edit instruction says."})
    for ref in refs:
        path = root / ref["path"]
        if not path.is_file():
            raise ArtGenError(f"reference image not found: {ref['path']}")
        actual = sha256_file(path)
        if ref["sha256"] and ref["sha256"] != actual:
            raise ArtGenError(f"reference changed since it was pinned: {ref['path']}")
        ref["sha256"] = actual
    subject = job["subject"].strip()
    lines = []
    if edit:
        lines.append(f"Edit the EDIT TARGET (reference image {len(refs)}). Change only this: {edit['change'].strip()} "
                     "Keep everything else as it is.")
        lines.append(f"Subject, for context only: {subject}")
    else:
        lines.append(f"Create ONE standalone card-theme illustration. Subject: {subject}")
    lines.append(f"Style: {cat['styleBlock']}")
    lines.append(f"Colour roles: {cat['colorRoles']}")
    lines.append(f"Composition: {cat['composition']}")
    lines.append(f"Output: {cat['output']['instruction']}")
    lines.append(f"Avoid: {cat['avoid']}")
    image_prompt = "\n".join(lines)
    abs_paths = [(root / r["path"]).resolve().as_posix() for r in refs]
    roles = "\n".join(f"{i}. {abs_paths[i - 1]} - {r['role']}" for i, r in enumerate(refs, 1))
    transparent = bool(cat["output"].get("transparent", False))
    agent_prompt = AGENT_TEMPLATE.format(paths_json=json.dumps(abs_paths), transparent=str(transparent).lower(),
                                         roles=roles, prompt=image_prompt)
    expected = [{"path": abs_paths[i], "sha256": r["sha256"]} for i, r in enumerate(refs)]
    return {"imagePrompt": image_prompt, "agentPrompt": agent_prompt, "references": refs,
            "expectedImages": expected, "transparent": transparent, "content": content}


# ---------------------------------------------------------------- codex

def find_codex(explicit: str | None = None) -> list:
    if explicit:
        return [explicit]
    if os.environ.get("ARTGEN_CODEX"):
        return [os.environ["ARTGEN_CODEX"]]
    found = shutil.which("codex")
    if found:
        return [found]
    local = os.environ.get("LOCALAPPDATA")
    if local:
        hits = sorted(glob.glob(os.path.join(local, "OpenAI", "Codex", "bin", "*", "codex.exe")),
                      key=os.path.getmtime, reverse=True)
        if hits:
            return [hits[0]]
    raise ArtGenError("Codex CLI not found; set ARTGEN_CODEX or pass --codex")


def codex_home() -> Path:
    return Path(os.environ.get("CODEX_HOME") or (Path.home() / ".codex"))


def codex_version(cmd: list) -> str:
    try:
        out = subprocess.run([*cmd, "--version"], capture_output=True, timeout=60)
        return out.stdout.decode("utf-8", "replace").strip() or "unknown"
    except (OSError, subprocess.SubprocessError):
        return "unknown"


def run_codex(cmd: list, agent_prompt: str, workdir: Path, events: Path, last: Path, stderr: Path,
              timeout: int, extra_args: list) -> int:
    args = [*cmd, "exec", "--skip-git-repo-check", "-s", "read-only", "-C", str(workdir), *extra_args,
            "--json", "-o", str(last), "-"]
    with open(events, "wb") as out, open(stderr, "wb") as err:
        proc = subprocess.run(args, input=agent_prompt.encode("utf-8"), stdout=out, stderr=err, timeout=timeout)
    return proc.returncode


def parse_events(path: Path) -> dict:
    info = {"threadId": None, "usage": None, "errors": []}
    if not path.is_file():
        return info
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        line = line.strip()
        if not line.startswith("{"):
            continue
        try:
            event = json.loads(line)
        except json.JSONDecodeError:
            continue
        kind = event.get("type")
        if kind == "thread.started":
            info["threadId"] = event.get("thread_id")
        elif kind == "turn.completed":
            info["usage"] = event.get("usage")
        elif kind in ("error", "turn.failed"):
            info["errors"].append(json.dumps(event, ensure_ascii=False)[:500])
    return info


def find_rollout(home: Path, thread_id: str) -> Path | None:
    hits = sorted(glob.glob(str(home / "sessions" / "**" / f"rollout-*{thread_id}.jsonl"), recursive=True))
    return Path(hits[-1]) if hits else None


# A real invocation looks like `tools.image_gen__imagegen({...})`. Tool-discovery snippets that merely
# mention "imagegen" (ALL_TOOLS.filter(... /imagegen/ ...), reading imagegen/SKILL.md) are not image calls.
IMAGE_CALL = re.compile(r"image_gen(?:__|\.)imagegen\s*\(")
IMAGE_TOOL_NAME = re.compile(r"(?:image_gen(?:__|\.))?imagegen")


def empty_rollout() -> dict:
    return {"imageCalls": [], "generations": [], "quotaUsedPercent": [], "agentModel": None}


def parse_rollout(path: Path) -> dict:
    info = empty_rollout()
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        try:
            entry = json.loads(line)
        except json.JSONDecodeError:
            continue
        payload = entry.get("payload") or {}
        if entry.get("type") == "turn_context" and payload.get("model"):
            info["agentModel"] = payload["model"]
        ptype = payload.get("type")
        if ptype in ("custom_tool_call", "function_call"):
            text = payload.get("input") or payload.get("arguments") or ""
            name = str(payload.get("name") or "")
            if IMAGE_CALL.search(text) or IMAGE_TOOL_NAME.fullmatch(name):
                info["imageCalls"].append(text)
        elif ptype == "token_count":
            primary = ((payload.get("rate_limits") or {}).get("primary") or {})
            if primary.get("used_percent") is not None:
                info["quotaUsedPercent"].append(primary["used_percent"])
        item = payload.get("item") or {}
        if item.get("type") == "Extension" and str(item.get("kind", "")).startswith("image_gen"):
            info["generations"].append({key: item.get(key) for key in (
                "id", "kind", "status", "failure", "revisedPrompt", "transparentBackground", "inputImages",
                "savedPath", "generationId")})
    return info


_STR = r'"((?:[^"\\]|\\.)*)"|\'((?:[^\'\\]|\\.)*)\''


def call_reference_paths(call: str) -> list | None:
    match = re.search(r"referenced_image_paths\"?\s*:\s*\[(.*?)\]", call, re.S)
    if not match:
        return None
    paths = []
    for dq, sq in re.findall(_STR, match.group(1)):
        raw = dq if dq else sq
        try:
            paths.append(json.loads('"' + raw.replace('\\\'', "'") + '"'))
        except json.JSONDecodeError:
            paths.append(raw)
    return paths


def call_prompt(call: str) -> str | None:
    """The literal prompt string passed in the call, or None when it is not a literal (e.g. a variable)."""
    match = re.search(r'(?:^|[{,\s])"?prompt"?\s*:\s*("(?:[^"\\]|\\.)*"|`[^`]*`)', call, re.S)
    if not match:
        return None
    literal = match.group(1)
    if literal.startswith("`"):
        return literal[1:-1]
    try:
        return json.loads(literal)
    except json.JSONDecodeError:
        return None


def check_run(rollout: dict, expected: list, prompt: str, want_transparent: bool) -> dict:
    """Verify one run from its session log. `expected` is [{path, sha256}] in reference order."""
    problems = []
    calls, gens = rollout["imageCalls"], rollout["generations"]
    checks = {"imageCallCount": len(calls), "generationCount": len(gens)}
    if len(calls) != 1:
        problems.append(f"expected exactly one image call, found {len(calls)}")
    if len(gens) != 1:
        problems.append(f"expected exactly one image generation record, found {len(gens)}")
    call = calls[-1] if calls else ""
    gen = gens[-1] if gens else {}
    if gen and (gen.get("status") != "completed" or gen.get("failure")):
        problems.append(f"image generation did not complete: status={gen.get('status')} failure={gen.get('failure')}")

    paths = call_reference_paths(call)
    checks["callReferencedImagePaths"] = paths
    if paths is None:
        problems.append("image call passed no referenced_image_paths: the style references were NOT used")
    elif [norm_path(p) for p in paths] != [norm_path(e["path"]) for e in expected]:
        problems.append("image call referenced different images than required")
    inputs = gen.get("inputImages")
    checks["modelInputImages"] = inputs
    if inputs is None:
        if gen:
            problems.append("generation record lists no inputImages; what the model received cannot be verified")
    elif [i.get("sha256") for i in inputs] != [e["sha256"] for e in expected]:
        problems.append("images received by the image model differ from the pinned references (SHA-256 mismatch)")

    call_transparent = re.search(r"transparent_background\"?\s*:\s*true", call) is not None
    gen_transparent = gen.get("transparentBackground")
    transparent = gen_transparent if gen_transparent is not None else call_transparent
    checks["transparentRequested"] = transparent
    if want_transparent and not transparent:
        problems.append("a transparent background was not requested")

    literal = call_prompt(call)
    revised = gen.get("revisedPrompt")
    checks["callPromptExact"] = None if literal is None else norm_prompt(literal) == norm_prompt(prompt)
    checks["revisedPromptExact"] = None if revised is None else norm_prompt(revised) == norm_prompt(prompt)
    if checks["callPromptExact"] is False:
        problems.append("the prompt passed to the image tool differs from the composed prompt")
    if checks["revisedPromptExact"] is False:
        problems.append("the prompt the image model used differs from the composed prompt")
    if checks["callPromptExact"] is None and checks["revisedPromptExact"] is None:
        problems.append("the prompt actually used could not be verified")
    return {"checks": checks, "problems": problems}


# ---------------------------------------------------------------- image analysis

def alpha_stats(img: Image.Image) -> dict:
    rgba = np.asarray(img.convert("RGBA"))
    alpha = rgba[..., 3]
    h, w = alpha.shape
    ys, xs = np.nonzero(alpha > 16)
    bbox = [int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1] if xs.size else None
    stats = {
        "dimensions": [w, h],
        "mode": img.mode,
        "alphaExtrema": [int(alpha.min()), int(alpha.max())],
        "transparentShare": round(float((alpha == 0).mean()), 4),
        "alphaBounds": bbox,
    }
    if bbox:
        stats["subjectWidthShare"] = round((bbox[2] - bbox[0]) / w, 4)
        stats["subjectHeightShare"] = round((bbox[3] - bbox[1]) / h, 4)
        stats["subjectAreaShare"] = round(float((alpha > 16).mean()), 4)
    return stats


def role_shares(img: Image.Image, stride: int = 3) -> dict:
    rgba = np.asarray(img.convert("RGBA")).astype(np.float32)[::stride, ::stride]
    mask = rgba[..., 3] >= 200
    rgb = rgba[..., :3][mask]
    if rgb.size == 0:
        return {role: 0.0 for role in ROLE_ORDER}
    centers = np.array([hex_rgb(c) for _, c in ROLE_CENTERS], dtype=np.float32)
    labels = [r for r, _ in ROLE_CENTERS]
    dist = np.sqrt(((rgb[:, None, :] - centers[None, :, :]) ** 2).sum(-1))
    nearest = dist.argmin(1)
    far = dist.min(1) > OTHER_DISTANCE
    counts = {role: 0 for role in ROLE_ORDER}
    for idx, is_far in zip(nearest, far):
        counts["other" if is_far else labels[idx]] += 1
    total = float(len(nearest))
    return {role: round(counts[role] / total, 4) for role in ROLE_ORDER}


def light_fringe_share(img: Image.Image) -> float:
    rgba = np.asarray(img.convert("RGBA")).astype(np.float32)
    alpha = rgba[..., 3]
    semi = (alpha >= 16) & (alpha <= 239)
    if not semi.any():
        return 0.0
    rgb = rgba[..., :3][semi]
    luma = rgb @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
    return round(float((luma > 170).mean()), 4)


def indicators(img: Image.Image) -> dict:
    stats = alpha_stats(img)
    values = {f"role.{k}": v for k, v in role_shares(img).items()}
    values["transparentShare"] = stats["transparentShare"]
    values["subjectWidthShare"] = stats.get("subjectWidthShare", 0.0)
    values["subjectHeightShare"] = stats.get("subjectHeightShare", 0.0)
    values["lightFringeShare"] = light_fringe_share(img)
    return values


def compare_to_ranges(candidate: dict, ranges: dict, tolerance: float = LOOK_TOLERANCE) -> dict:
    result = {}
    for key, value in candidate.items():
        lo, hi = ranges.get(key, [value, value])
        look = value < lo - tolerance or value > hi + tolerance
        result[key] = {"value": value, "refMin": lo, "refMax": hi, "look": look}
    return result


def ranges_from(values: list) -> dict:
    keys = values[0].keys() if values else []
    return {k: [min(v[k] for v in values), max(v[k] for v in values)] for k in keys}


# ---------------------------------------------------------------- review sheet

def _font(size: int):
    for name in ("msyh.ttc", "simhei.ttf", "arial.ttf", "DejaVuSans.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def _on(bg: tuple, art: Image.Image, size: int, box: int) -> Image.Image:
    tile = Image.new("RGB", (box, box), bg)
    a = fit_into(art, size)
    tile.paste(a, ((box - a.width) // 2, (box - a.height) // 2), a)
    return tile


def _luma(c: tuple) -> float:
    return (0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]) / 255


def sheet_colours(kit: dict, cat: dict) -> tuple:
    """Backdrops and text colour of the review sheet: (paper, second backdrop, ink, labels).

    The default-style kits name these colours ivory, petrol and ink. Theme kits (V1.1 W19) name their palettes freely,
    so a missing name falls back to: the category plate's backdrop (the theme's card backdrop) for the first tile; for
    the second tile a contrasting backdrop, dark under a light card and light under a dark one, so fringes and halos
    show either way; the darkest palette colour for text. Kits that have the three names draw exactly as before."""
    pal = kit.get("palette", {})
    colours = [hex_rgb(v) for v in pal.values() if isinstance(v, str) and len(v) == 6]
    darkest = min(colours, key=_luma) if colours else hex_rgb("162629")
    if "ivory" in pal and "petrol" in pal:
        paper, second = hex_rgb(pal["ivory"]), hex_rgb(pal["petrol"])
        labels = ("original on paper (edges, halftone, lines)", "original on felt (white fringe / halo shows here)",
                  "(row 1 paper card, row 2 felt)")
    else:
        paper = hex_rgb(pal.get("ivory") or cat.get("plate", {}).get("background") or "F4E8CB")
        light = _luma(paper) >= 0.4
        second = hex_rgb(pal["petrol"]) if "petrol" in pal else (darkest if light else hex_rgb("F4E8CB"))
        labels = ("original on the theme's card backdrop",
                  "original on a " + ("dark" if light else "light") + " backdrop (fringes and halos show here)",
                  "(row 1 card backdrop, row 2 contrasting backdrop)")
    ink = hex_rgb(pal["ink"]) if "ink" in pal else darkest
    if _luma(ink) > 0.45:   # text sits on the sheet's light background
        ink = hex_rgb("162629")
    return paper, second, ink, labels


def build_sheet(kit: dict, cname: str, candidate_path: Path, out_png: Path, out_json: Path, root: Path,
                title: str, validity: str) -> dict:
    cat = kit_category(kit, cname)
    paper, felt, ink, labels = sheet_colours(kit, cat)
    cand = Image.open(candidate_path).convert("RGBA")
    anchor_imgs = [(a["id"], Image.open(root / a["path"]).convert("RGBA")) for a in cat["anchors"]]
    stored = cat.get("indicatorRanges")
    if stored:
        ranges, range_source = stored["ranges"], f"{stored['count']} shipped illustrations ({stored['source']})"
    else:
        ranges, range_source = ranges_from([indicators(img) for _, img in anchor_imgs]), "the pinned anchors"
    cand_ind = indicators(cand)
    comparison = compare_to_ranges(cand_ind, ranges)

    width = 1680
    sizes = cat.get("displaySizes", [160, 96, 64])
    lineup_cell = 168
    lineup_cols = min(len(anchor_imgs) + 1, width // lineup_cell)
    lineup_rows = (len(anchor_imgs) + 1 + lineup_cols - 1) // lineup_cols
    height = 60 + 540 + 40 + 2 * (max(sizes) + 24) + 40 + lineup_rows * (lineup_cell + 22) + 60
    sheet = Image.new("RGB", (width, height), (236, 231, 220))
    draw = ImageDraw.Draw(sheet)
    f_big, f_mid, f_small = _font(26), _font(18), _font(15)
    draw.text((20, 16), f"{title}   |   run: {validity}   |   kit: {kit.get('kitVersion')}", fill=ink, font=f_big)

    y = 60
    sheet.paste(_on(paper, cand, 500, 520), (20, y))
    sheet.paste(_on(felt, cand, 500, 520), (560, y))
    draw.text((20, y + 522), labels[0], fill=ink, font=f_small)
    draw.text((560, y + 522), labels[1], fill=ink, font=f_small)
    tx = 1100
    draw.text((tx, y), "Drift indicators (not a gate)", fill=ink, font=f_mid)
    ty = y + 30
    for key, row in comparison.items():
        mark = "LOOK" if row["look"] else "ok"
        colour = (190, 60, 50) if row["look"] else ink
        draw.text((tx, ty), f"{key:<20} {row['value']:.3f}  [{row['refMin']:.3f}-{row['refMax']:.3f}]  {mark}",
                  fill=colour, font=f_small)
        ty += 22
    draw.text((tx, ty + 10), f"Range = min-max over {range_source}.\nLOOK = outside that range by more than"
              f" {LOOK_TOLERANCE:.2f}.\nInspect, then accept with a reason or fix.", fill=ink, font=f_small)

    y += 560
    draw.text((20, y), "approximate display sizes: " + " / ".join(f"{s}px" for s in sizes)
              + "   " + labels[2], fill=ink, font=f_mid)
    y += 28
    for row_i, bg in enumerate((paper, felt)):
        x = 20
        for s in sizes:
            box = s + 16
            sheet.paste(_on(bg, cand, s, box), (x, y + row_i * (max(sizes) + 24)))
            x += box + 24
    y += 2 * (max(sizes) + 24) + 12
    draw.text((20, y), "same-size lineup: pinned anchors + candidate (red frame)", fill=ink, font=f_mid)
    y += 28
    items = anchor_imgs + [("CANDIDATE", cand)]
    for i, (name, img) in enumerate(items):
        cx = 20 + (i % lineup_cols) * lineup_cell
        cy = y + (i // lineup_cols) * (lineup_cell + 22)
        sheet.paste(_on(paper, img, lineup_cell - 16, lineup_cell - 6), (cx, cy))
        if name == "CANDIDATE":
            draw.rectangle([cx - 2, cy - 2, cx + lineup_cell - 4, cy + lineup_cell - 4], outline=(214, 70, 60), width=4)
        draw.text((cx + 4, cy + lineup_cell - 4), name, fill=ink, font=f_small)
    sheet.save(out_png, format="PNG")
    report = {"tool": TOOL_VERSION, "candidate": rel(candidate_path, root), "candidateSha256": sha256_file(candidate_path),
              "kitVersion": kit.get("kitVersion"), "category": cname, "rangeSource": range_source,
              "lookTolerance": LOOK_TOLERANCE,
              "anchors": [{"id": a["id"], "sha256": a.get("sha256")} for a in cat["anchors"]],
              "indicators": comparison, "lookFlags": [k for k, v in comparison.items() if v["look"]],
              "note": "Indicators locate drift for a human or independent reviewer. They are not pass/fail criteria."}
    write_json(out_json, report)
    return report


# ---------------------------------------------------------------- generate

def next_revision(job_dir: Path) -> str:
    existing = [int(m.group(1)) for p in job_dir.iterdir() if p.is_dir() and (m := re.fullmatch(r"r(\d{2})", p.name))]
    return f"r{(max(existing) + 1 if existing else 1):02d}"


def load_job(job_path: Path) -> dict:
    job = load_json(job_path)
    for key in ("jobId", "category", "subject"):
        if not job.get(key):
            raise ArtGenError(f"job is missing '{key}': {job_path}")
    return job


def locate_output(home: Path, thread_id: str | None, rollout: dict, problems: list) -> Path | None:
    saved = [g.get("savedPath") for g in rollout["generations"] if g.get("savedPath")]
    if saved:
        path = Path(saved[-1])
        if path.is_file():
            return path
        problems.append(f"generation record names a saved file that does not exist: {path}")
    pngs = sorted(glob.glob(str(home / "generated_images" / (thread_id or "__none__") / "*.png")))
    if len(pngs) != 1:
        problems.append(f"expected one generated image for the thread, found {len(pngs)}")
    return Path(pngs[-1]) if pngs else None


def generate(root: Path, kit_rel: Path, job_path: Path, codex_cmd: list, timeout: int = 900,
             extra_args: list | None = None, config_rel: Path = CONFIG_DEFAULT) -> dict:
    kit = load_kit(root, kit_rel)
    problems = verify_kit(kit, root)
    if problems:
        raise ArtGenError("style kit does not verify: " + "; ".join(problems))
    job = load_job(job_path)
    content = load_content(root, config_rel, job["contentId"]) if job.get("contentId") else None
    plan = compose(kit, job, root, content)
    job_dir = job_path.parent
    rev = next_revision(job_dir)
    rev_dir = job_dir / rev
    rev_dir.mkdir()
    prompt_file, request_file = rev_dir / "prompt.txt", rev_dir / "codex-request.txt"
    write_text_lf(prompt_file, plan["imagePrompt"] + "\n")
    write_text_lf(request_file, plan["agentPrompt"] + "\n")
    events, last, stderr = rev_dir / "codex-events.jsonl", rev_dir / "codex-last-message.txt", rev_dir / "codex-stderr.txt"
    workdir = Path(tempfile.mkdtemp(prefix="artgen-"))
    started = time.time()
    exit_code, run_problems = None, []
    try:
        exit_code = run_codex(codex_cmd, plan["agentPrompt"], workdir, events, last, stderr, timeout, extra_args or [])
    except subprocess.TimeoutExpired:
        run_problems.append(f"codex timed out after {timeout}s")
    except (OSError, subprocess.SubprocessError) as exc:
        run_problems.append(f"codex could not be run: {exc}")
    finally:
        shutil.rmtree(workdir, ignore_errors=True)
    duration = round(time.time() - started, 1)
    ev = parse_events(events)
    if exit_code not in (0, None):
        run_problems.append(f"codex exited with {exit_code}")
    run_problems += [f"codex error event: {e}" for e in ev["errors"]]

    home = codex_home()
    rollout_path = find_rollout(home, ev["threadId"]) if ev["threadId"] else None
    rollout = empty_rollout()
    if rollout_path:
        try:
            rollout = parse_rollout(rollout_path)
        except OSError as exc:
            run_problems.append(f"codex session log unreadable: {exc}")
    elif ev["threadId"]:
        run_problems.append("codex session log not found; the image call could not be verified")
    check = check_run(rollout, plan["expectedImages"], plan["imagePrompt"], plan["transparent"])
    run_problems += check["problems"]

    output = None
    source = locate_output(home, ev["threadId"], rollout, run_problems)
    if source:
        archived = rev_dir / "original.png"
        try:
            shutil.copyfile(source, archived)
            output = {"sourcePath": str(source), "archivedPath": rel(archived, root), "sha256": sha256_file(archived),
                      "copiedByteIdentical": sha256_file(source) == sha256_file(archived)}
            output.update(alpha_stats(Image.open(archived)))
            if plan["transparent"] and output["alphaExtrema"][0] > 0:
                run_problems.append("output has no fully transparent pixels although transparency was required")
        except OSError as exc:
            run_problems.append(f"output could not be archived or read: {exc}")

    valid = not run_problems and output is not None and "alphaExtrema" in output
    record = {
        "schema": "artgen-record-1",
        "toolVersion": TOOL_VERSION,
        "jobId": job["jobId"],
        "jobPath": rel(job_path, root),
        "jobSha256": sha256_file(job_path),
        "category": job["category"],
        "revision": rev,
        "mode": "edit" if job.get("edit") else "new",
        "transparentRequired": plan["transparent"],
        "content": content,
        "styleKit": {"path": rel(kit["_path"], root), "sha256": sha256_file(kit["_path"]),
                     "kitVersion": kit.get("kitVersion"), "status": kit.get("status")},
        "references": [{"index": i, "path": r["path"], "sha256": r["sha256"], "role": r["role"]}
                       for i, r in enumerate(plan["references"], 1)],
        "prompt": {"imagePromptPath": rel(prompt_file, root), "imagePromptFileSha256": sha256_file(prompt_file),
                   "agentRequestPath": rel(request_file, root), "agentRequestFileSha256": sha256_file(request_file)},
        "codex": {"command": codex_cmd, "version": codex_version(codex_cmd), "exitCode": exit_code,
                  "threadId": ev["threadId"], "sessionLog": str(rollout_path) if rollout_path else None,
                  "agentModel": rollout["agentModel"], "usage": ev["usage"], "durationSeconds": duration,
                  "quotaUsedPercentFirst": rollout["quotaUsedPercent"][0] if rollout["quotaUsedPercent"] else None,
                  "quotaUsedPercentLast": rollout["quotaUsedPercent"][-1] if rollout["quotaUsedPercent"] else None},
        "imageTool": {"name": "image_gen.imagegen (Codex built-in)",
                      "modelSnapshot": "not exposed by the tool; OpenAI documents the built-in tool as gpt-image-2",
                      "generations": rollout["generations"], "runChecks": check["checks"]},
        "output": output,
        "runValid": valid,
        "runProblems": run_problems,
        "status": "CANDIDATE: review pending" if valid else "INVALID_RUN: not eligible for review or use",
        "recordedUtc": utc_now(),
    }
    write_json(rev_dir / "record.json", record)
    if output and "alphaExtrema" in output:
        title = f"{job['jobId']} {job.get('contentId') or ''} {rev}"
        try:
            build_sheet(kit, job["category"], rev_dir / "original.png", rev_dir / "review_sheet.png",
                        rev_dir / "review_metrics.json", root, title, "VALID" if valid else "INVALID_RUN")
        except (OSError, KeyError, ValueError) as exc:
            # The sheet is a review aid: the run's own verdict above stands. The problem is still recorded.
            record["runProblems"].append(f"review sheet could not be built: {exc!r}")
            write_json(rev_dir / "record.json", record)
    return record


def transparency_required(root: Path, record: dict) -> bool:
    if "transparentRequired" in record:
        return bool(record["transparentRequired"])
    kit_path = root / (record.get("styleKit") or {}).get("path", "")
    if kit_path.is_file():
        cat = load_json(kit_path).get("categories", {}).get(record.get("category"), {})
        if "output" in cat:
            return bool(cat["output"].get("transparent", False))
    return True


def recheck(root: Path, record_path: Path) -> dict:
    """Re-run today's run checks on an archived revision, from its own record and session log,
    and confirm the archived files still match the hashes in the record."""
    record = load_json(record_path)
    rev_dir = record_path.parent
    prompt_file = rev_dir / "prompt.txt"
    prompt = prompt_file.read_text(encoding="utf-8")
    expected = [{"path": (root / r["path"]).resolve().as_posix(), "sha256": r["sha256"]} for r in record["references"]]
    log = record.get("codex", {}).get("sessionLog")
    rollout = parse_rollout(Path(log)) if log and Path(log).is_file() else empty_rollout()
    result = check_run(rollout, expected, prompt, transparency_required(root, record))
    if not log or not Path(log).is_file():
        result["problems"].insert(0, "session log not available")
    archive = {}
    original = rev_dir / "original.png"
    want = (record.get("output") or {}).get("sha256")
    archive["originalMatchesRecord"] = original.is_file() and want is not None and sha256_file(original) == want
    meta = record.get("prompt") or {}
    if "imagePromptFileSha256" in meta:
        archive["promptMatchesRecord"] = sha256_file(prompt_file) == meta["imagePromptFileSha256"]
    elif "imagePromptSha256" in meta:  # artgen-1.0 hashed the LF text, not the file bytes
        text = prompt_file.read_bytes().decode("utf-8").replace("\r\n", "\n")
        archive["promptMatchesRecord"] = hashlib.sha256(text.encode("utf-8")).hexdigest() == meta["imagePromptSha256"]
    else:
        archive["promptMatchesRecord"] = False
    result["archive"] = archive
    result["problems"] += [f"archived file no longer matches the record: {k}" for k, ok in archive.items() if not ok]
    return result


# ---------------------------------------------------------------- CLI

def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument("--kit", type=Path, default=KIT_DEFAULT)
    sub = parser.add_subparsers(dest="cmd", required=True)
    sub.add_parser("verify-kit", help="check every pinned anchor, plate and range reference set")
    sub.add_parser("pin-anchors", help="fill empty anchor sha256 fields (never overwrites)")
    bp = sub.add_parser("build-plate", help="render a category's style plate from its anchors and pin its SHA-256")
    bp.add_argument("--category", default="illustration")
    bp.add_argument("--replace", action="store_true")
    cr = sub.add_parser("compute-ranges", help="store indicator ranges over a pinned file list in the kit")
    cr.add_argument("--category", default="illustration")
    cr.add_argument("--glob", required=True)
    pr = sub.add_parser("prompt", help="print the composed request for a job (no generation)")
    pr.add_argument("--job", type=Path, required=True)
    gen = sub.add_parser("generate", help="run one generation for a job and archive it as the next revision")
    gen.add_argument("--job", type=Path, required=True)
    gen.add_argument("--codex", default=None)
    gen.add_argument("--timeout", type=int, default=900)
    gen.add_argument("--codex-arg", action="append", default=[],
                     help="extra argument for 'codex exec'; use the = form, e.g. --codex-arg=--ignore-user-config")
    rc = sub.add_parser("recheck", help="re-run the run checks on an archived revision's record.json")
    rc.add_argument("--record", type=Path, required=True)
    sh = sub.add_parser("sheet", help="(re)build the review sheet for an image")
    sh.add_argument("--image", type=Path, required=True)
    sh.add_argument("--category", default="illustration")
    sh.add_argument("--title", default="")
    args = parser.parse_args(argv)
    root = args.root.resolve()
    try:
        if args.cmd == "verify-kit":
            problems = verify_kit(load_kit(root, args.kit), root)
            print("KIT_VERIFIED" if not problems else "KIT_PROBLEMS:\n- " + "\n- ".join(problems))
            return 0 if not problems else 1
        if args.cmd == "pin-anchors":
            kit = load_kit(root, args.kit)
            pinned = pin_anchors(kit, root)
            save_kit(kit)
            print("pinned: " + (", ".join(pinned) if pinned else "nothing new"))
            return 0
        if args.cmd == "build-plate":
            kit = load_kit(root, args.kit)
            digest = build_plate(kit, args.category, root, args.replace)
            save_kit(kit)
            print(f"plate {kit_category(kit, args.category)['plate']['path']} sha256 {digest}")
            return 0
        if args.cmd == "compute-ranges":
            kit = load_kit(root, args.kit)
            entry = compute_ranges(kit, args.category, root, args.glob)
            save_kit(kit)
            print(json.dumps({"count": entry["count"], "ranges": entry["ranges"]}, indent=2))
            return 0
        if args.cmd == "prompt":
            kit = load_kit(root, args.kit)
            job = load_job(args.job.resolve())
            content = load_content(root, CONFIG_DEFAULT, job["contentId"]) if job.get("contentId") else None
            print(compose(kit, job, root, content)["agentPrompt"])
            return 0
        if args.cmd == "generate":
            record = generate(root, args.kit, args.job.resolve(), find_codex(args.codex), args.timeout, args.codex_arg)
            print(json.dumps({"revision": record["revision"], "status": record["status"],
                              "runProblems": record["runProblems"],
                              "output": (record["output"] or {}).get("archivedPath")}, ensure_ascii=False, indent=2))
            return 0 if record["runValid"] else 1
        if args.cmd == "recheck":
            result = recheck(root, args.record.resolve())
            print(json.dumps(result, ensure_ascii=False, indent=2))
            return 0 if not result["problems"] else 1
        if args.cmd == "sheet":
            kit = load_kit(root, args.kit)
            image = args.image.resolve()
            report = build_sheet(kit, args.category, image, image.with_name(image.stem + "_review_sheet.png"),
                                 image.with_name(image.stem + "_review_metrics.json"), root, args.title or image.name, "n/a")
            print(json.dumps({"lookFlags": report["lookFlags"]}, ensure_ascii=False))
            return 0
    except ArtGenError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2
    return 2


if __name__ == "__main__":
    sys.exit(main())
