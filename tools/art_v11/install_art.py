"""V1.1 W22i: install the styles' card illustrations and boss emblems into the game (THEME_SPEC 3, SETTLEMENT_LAYOUT 8.5).

For every content id under ArtSource/Art11/<style>/illustrations/<id>/ and every emblem family under
ArtSource/Art11/<style>/emblems/<family>/, the revision installed is the latest rNN folder whose record.json says
"runValid": true. Its original.png is copied byte for byte (so git stores no new blob) after its SHA-256 has been checked
against the record's output.sha256:

    illustrations (swiss, sancai, comic, deco, glass)  DicePrototype/Assets/Resources/Themes/<style>/Art/illustration_<ID>.png
    emblems of those five styles                        DicePrototype/Assets/Resources/Themes/<style>/Art/emblem_<family>.png
    emblems of the default style                        DicePrototype/Assets/Art/Art11/Emblems/emblem_<family>.png

The default style's illustrations stay where they are (Assets/Art/Art071/Illustrations, bound in the Bootstrap scene).
A revision folder without record.json (a run that was interrupted before it was recorded) or with "runValid" other than
true is never installed. The tool refuses to install anything (exit 2, nothing written) when an id or family has no
valid record, when the chosen record does not describe its own folder (revision, archived path, content id or job
path), when its original.png is missing, or when the file's SHA-256 differs from the record. The ids must be the 184
content ids of P0Config, the families the seven of SETTLEMENT_LAYOUT 8.5.

A manifest per style (tools/art_v11/install_manifests/<style>.json; default.json for the default emblems) lists every
installed file with its id or family, the source revision, its SHA-256, size, pixel size and record path. Files are
written only when their bytes differ, so a second run changes nothing; illustration_* / emblem_* PNGs in a target folder
that the plan no longer lists are removed with their Unity .meta. Unity imports the new files with the theme import rule
(Editor/V11ThemeImport.cs: sprite, max 1024, compressed), the same as the Swiss trial images.

    python tools/art_v11/install_art.py              # install and write the manifests
    python tools/art_v11/install_art.py --check      # verify, write nothing (exit 1 when anything differs)
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import struct
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[2]
SCHEMA = "w22i-art-install-1"
GENERATED_BY = "tools/art_v11/install_art.py"
RULE = ("the latest rNN whose record.json has runValid true; original.png copied byte for byte after its sha256 was "
        "checked against the record's output.sha256")
STYLES = ("swiss", "sancai", "comic", "deco", "glass")
DEFAULT = "default"
FAMILIES = ("seal", "constraint", "multiplier", "cards", "economy", "target", "final")
CONTENT_LISTS = ("controllers", "consumables", "services", "diceUpgrades")
LOOT_IDS = tuple("L" + str(index) for index in range(1, 7))
REVISION = re.compile(r"^r(\d{2,})$")
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"


class InstallError(Exception):
    """A missing or invalid record, or a source that does not match it: nothing is installed."""


class Paths:
    """Where the tool reads and writes; the tests point it at a fixture tree."""

    def __init__(self, root=ROOT, art=None, game=None, manifests=None, config=None):
        root = Path(root)
        self.root = root
        self.art = Path(art) if art else root / "ArtSource" / "Art11"
        self.game = Path(game) if game else root / "DicePrototype" / "Assets"
        self.manifests = Path(manifests) if manifests else root / "tools" / "art_v11" / "install_manifests"
        self.config = Path(config) if config else self.game / "Game" / "Data" / "P0Config.json"

    def rel(self, path):
        path = Path(path).resolve()
        try:
            return path.relative_to(self.root.resolve()).as_posix()
        except ValueError:
            return path.as_posix()

    def target(self, style):
        if style == DEFAULT:
            return self.game / "Art" / "Art11" / "Emblems"
        return self.game / "Resources" / "Themes" / style / "Art"


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def png_size(path):
    with open(path, "rb") as handle:
        head = handle.read(24)
    if head[:8] != PNG_SIGNATURE or head[12:16] != b"IHDR":
        raise InstallError(f"{path}: not a PNG file")
    return list(struct.unpack(">II", head[16:24]))


def id_key(name):
    """Natural order for content ids (C2 before C10); families keep FAMILIES order."""
    match = re.match(r"([A-Z]+)(\d+)$", name)
    return (match.group(1), int(match.group(2))) if match else (name, 0)


def config_ids(paths):
    config = json.loads(paths.config.read_text(encoding="utf-8"))
    return sorted((item["id"] for key in CONTENT_LISTS for item in config[key]), key=id_key)


def revisions(folder):
    """rNN folders, oldest first."""
    found = [(int(m.group(1)), p.name) for p in folder.iterdir() if p.is_dir() and (m := REVISION.match(p.name))]
    return [name for _, name in sorted(found)]


def choose(paths, folder, expect, revision=None):
    """The latest revision with a valid record, checked against the record. `expect` holds the record fields that must
    match: jobPath, and contentId for an illustration. Returns the manifest entry (without id or family)."""
    if not folder.is_dir():
        raise InstallError(f"{paths.rel(folder)}: no such folder")
    chosen, record = None, None
    candidates = [revision] if revision is not None else reversed(revisions(folder))
    for name in candidates:
        path = folder / name / "record.json"
        if not path.is_file():
            continue  # an interrupted run that was never recorded
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
        except (OSError, ValueError) as error:
            raise InstallError(f"{paths.rel(path)}: unreadable record ({error})") from error
        if data.get("runValid") is True:
            chosen, record = name, data
            break
    if chosen is None:
        if revision is not None:
            raise InstallError(f"{paths.rel(folder)}/{revision}: approved revision lacks a valid record")
        raise InstallError(f"{paths.rel(folder)}: no revision has a record with runValid true")
    record_path = folder / chosen / "record.json"
    where = paths.rel(record_path)
    original = folder / chosen / "original.png"
    if record.get("revision") != chosen:
        raise InstallError(f"{where}: the record names revision {record.get('revision')!r}, its folder is {chosen}")
    output = record.get("output") or {}
    if output.get("archivedPath") != paths.rel(original):
        raise InstallError(f"{where}: archivedPath {output.get('archivedPath')!r} is not {paths.rel(original)!r}")
    if record.get("jobPath") != expect["jobPath"]:
        raise InstallError(f"{where}: jobPath {record.get('jobPath')!r} is not {expect['jobPath']!r}")
    if "contentId" in expect and (record.get("content") or {}).get("contentId") != expect["contentId"]:
        raise InstallError(f"{where}: contentId {(record.get('content') or {}).get('contentId')!r} is not {expect['contentId']!r}")
    wanted = output.get("sha256")
    if not isinstance(wanted, str) or not re.fullmatch(r"[0-9a-f]{64}", wanted):
        raise InstallError(f"{where}: no output.sha256")
    if not original.is_file():
        raise InstallError(f"{paths.rel(original)}: missing")
    actual = sha256(original)
    if actual != wanted:
        raise InstallError(f"{paths.rel(original)}: sha256 {actual} differs from the record's {wanted}")
    return {"revision": chosen, "sha256": actual, "bytes": original.stat().st_size, "size": png_size(original),
            "record": where, "source": original}


def plan(paths, style):
    """Every file to install for one style: (manifest entries, {target name: source path})."""
    art = paths.art / style
    illustrations, emblems = [], []
    if style != DEFAULT:
        folder = art / "illustrations"
        if not folder.is_dir():
            raise InstallError(f"{paths.rel(folder)}: no such folder")
        ids = config_ids(paths)
        present = sorted((p.name for p in folder.iterdir() if p.is_dir()), key=id_key)
        missing, extra = sorted(set(ids) - set(present), key=id_key), sorted(set(present) - set(ids), key=id_key)
        if missing or extra:
            raise InstallError(f"{paths.rel(folder)}: ids missing {missing}, not in P0Config {extra}")
        for content_id in ids:
            entry = choose(paths, folder / content_id, {
                "jobPath": f"ArtSource/Art11/{style}/illustrations/{content_id}/job.json", "contentId": content_id})
            illustrations.append({"id": content_id, "file": f"illustration_{content_id}.png", **entry})
    folder = art / "emblems"
    for family in FAMILIES:
        entry = choose(paths, folder / family, {"jobPath": f"ArtSource/Art11/{style}/emblems/{family}/job.json"})
        emblems.append({"family": family, "file": f"emblem_{family}.png", **entry})
    return illustrations, emblems


def manifest_text(paths, style, illustrations, emblems):
    def row(entry, key):
        fields = {key: entry[key], "file": entry["file"], "revision": entry["revision"], "sha256": entry["sha256"],
                  "bytes": entry["bytes"], "size": entry["size"], "record": entry["record"]}
        return "    " + json.dumps(fields, ensure_ascii=False)

    lines = ["{",
             f'  "schema": {json.dumps(SCHEMA)},',
             f'  "style": {json.dumps(style)},',
             f'  "generatedBy": {json.dumps(GENERATED_BY)},',
             f'  "rule": {json.dumps(RULE)},',
             f'  "target": {json.dumps(paths.rel(paths.target(style)))},',
             f'  "illustrations": [' + ("" if illustrations else "],")]
    if illustrations:
        lines += [",\n".join(row(e, "id") for e in illustrations), "  ],"]
    lines.append('  "emblems": [')
    lines += [",\n".join(row(e, "family") for e in emblems), "  ]", "}", ""]
    return "\n".join(lines)


def install(paths, styles, check=False, log=print):
    """Installs (or, with check, verifies) the styles. Every style is planned before anything is written."""
    planned = {style: plan(paths, style) for style in styles}
    differences, written, removed = [], 0, 0
    for style, (illustrations, emblems) in planned.items():
        target = paths.target(style)
        wanted = {e["file"]: e for e in illustrations + emblems}
        for name, entry in sorted(wanted.items()):
            path = target / name
            if path.is_file() and path.stat().st_size == entry["bytes"] and sha256(path) == entry["sha256"]:
                continue
            if check:
                differences.append(f"{paths.rel(path)}: not the {entry['revision']} original")
                continue
            target.mkdir(parents=True, exist_ok=True)
            path.write_bytes(entry["source"].read_bytes())
            if sha256(path) != entry["sha256"]:
                raise InstallError(f"{paths.rel(path)}: copy does not match {entry['sha256']}")
            written += 1
        if target.is_dir():
            for old in sorted(target.glob("*.png")):
                if old.name in {f"illustration_{key}.png" for key in LOOT_IDS}:
                    continue  # W17 owns these six names in each theme's shared Art directory.
                if (old.name.startswith("illustration_") or old.name.startswith("emblem_")) and old.name not in wanted:
                    if check:
                        differences.append(f"{paths.rel(old)}: not in the plan")
                        continue
                    old.unlink()
                    meta = old.with_name(old.name + ".meta")
                    if meta.exists():
                        meta.unlink()
                    removed += 1
        text = manifest_text(paths, style, illustrations, emblems)
        manifest = paths.manifests / f"{style}.json"
        if not manifest.is_file() or manifest.read_bytes() != text.encode("utf-8"):
            if check:
                differences.append(f"{paths.rel(manifest)}: differs from the plan")
            else:
                paths.manifests.mkdir(parents=True, exist_ok=True)
                manifest.write_bytes(text.encode("utf-8"))
        entries = illustrations + emblems
        counts = ", ".join(f"{r} {sum(1 for e in entries if e['revision'] == r)}"
                           for r in sorted({e["revision"] for e in entries}))
        log(f"STYLE {style}: {len(illustrations)} illustrations, {len(emblems)} emblems -> {paths.rel(target)}; "
            f"revisions {counts}")
    if check:
        for line in differences[:40]:
            log("DIFFERS " + line)
        if len(differences) > 40:
            log(f"... and {len(differences) - 40} more")
        log("INSTALL_CHECK_OK" if not differences else f"INSTALL_CHECK_FAILED {len(differences)}")
        return 0 if not differences else 1
    log(f"INSTALLED written={written} removed={removed} styles={','.join(styles)}")
    return 0


def reviewed_rollout(paths):
    """Read the approved W23 source pins if this checkout has received its initial rollout."""
    manifest = paths.manifests / "w23-rollout.json"
    if not manifest.exists():
        return {}
    expected = {(style, key) for style in STYLES for key in (*LOOT_IDS, "menu")}
    pins = {}
    try:
        data = json.loads(manifest.read_bytes())
        if data["schema"] != "w23-reviewed-art-1" or not isinstance(data["items"], list):
            raise ValueError("unsupported schema or rows")
        for entry in data["items"]:
            pair = entry["theme"], entry["id"]
            if pair not in expected or pair in pins:
                raise ValueError(f"unknown or duplicate theme/id {pair!r}")
            if not REVISION.fullmatch(entry["revision"]):
                raise ValueError(f"{pair!r}: invalid revision")
            for field in ("sourceSha256", "sha256", "recordSha256"):
                if not re.fullmatch(r"[0-9a-f]{64}",entry[field]):
                    raise ValueError(f"{pair!r}: invalid {field}")
            if pair[1] != "menu" and entry["sourceSha256"] != entry["sha256"]:
                raise ValueError(f"{pair!r}: reward bytes must equal the selected original")
            pins[pair] = entry
        if set(pins) != expected:
            raise ValueError("expected the five complete themes, each with six rewards and one tray")
    except (OSError, ValueError, KeyError, TypeError) as error:
        raise InstallError(f"{paths.rel(manifest)}: invalid approved rollout ({error})") from error
    return pins


def install_loot(paths, styles, check=False, log=print):
    """Validate all six records per requested style before installing a single file."""
    from PIL import Image

    pins = reviewed_rollout(paths)
    planned = []
    for style in styles:
        target = paths.game / "Art/Art11/Loot" if style == DEFAULT else paths.target(style)
        entries = []
        for key in LOOT_IDS:
            folder = paths.art / style / "loot" / key
            pin = pins.get((style,key))
            item = choose(paths, folder, {"jobPath": paths.rel(folder/"job.json"), "contentId": key},
                          revision=pin["revision"] if pin else None)
            if pin and (item["sha256"] != pin["sourceSha256"] or
                        sha256(paths.root / item["record"]) != pin["recordSha256"]):
                raise InstallError(f"{style}/{key}: original or generation record differs from the approved W23 revision {pin['revision']}")
            with Image.open(item["source"]) as image:
                if image.mode != "RGBA" or image.size != (1254,1254) or image.getchannel("A").getextrema() != (0,255):
                    raise InstallError(f"{paths.rel(item['source'])}: expected a 1254-square transparent RGBA illustration")
            destination = target / f"illustration_{key}.png"
            planned.append((destination, item["source"], item["sha256"]))
            entries.append({"id":key,"asset":paths.rel(destination),**{k:v for k,v in item.items() if k!="source"}})
        manifest = paths.manifests / f"{style}-loot.json"
        data = (json.dumps({"style":style,"loot":entries}, indent=2)+"\n").encode("utf8")
        planned.append((manifest,data,hashlib.sha256(data).hexdigest()))
    differences = 0
    for target, source, digest in planned:
        if target.is_file() and sha256(target)==digest:
            continue
        differences += 1
        if check:
            log("DIFFERS "+paths.rel(target))
            continue
        target.parent.mkdir(parents=True, exist_ok=True)
        with tempfile.NamedTemporaryFile(dir=target.parent,delete=False,suffix=".tmp") as temp:
            temp.write(source if isinstance(source, bytes) else source.read_bytes())
        temp_path=Path(temp.name)
        try:
            if sha256(temp_path)!=digest:
                raise InstallError(f"Copy verification failed for {paths.rel(target)}")
            temp_path.replace(target)
        finally:
            temp_path.unlink(missing_ok=True)
    log(f"LOOT styles={','.join(styles)} illustrations={len(styles)*len(LOOT_IDS)} differences={differences}")
    return int(check and differences>0)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--styles", default=",".join(STYLES + (DEFAULT,)),
                        help="comma list of styles (default: the five styles and default, whose emblems only are installed)")
    parser.add_argument("--check", action="store_true", help="verify the installed files and manifests; write nothing")
    parser.add_argument("--loot", action="store_true", help="install six reward illustrations per style, using approved W23 revisions wherever a rollout receipt exists")
    args = parser.parse_args(argv)
    styles = [s.strip() for s in args.styles.split(",") if s.strip()]
    unknown = sorted(set(styles) - set(STYLES + (DEFAULT,)))
    if unknown:
        parser.error(f"unknown styles {unknown}")
    try:
        return (install_loot if args.loot else install)(Paths(), styles, check=args.check)
    except InstallError as error:
        print(f"REFUSED {error}")
        return 2


if __name__ == "__main__":
    sys.exit(main())
