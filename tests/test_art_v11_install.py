"""W22i: the installed card illustrations and boss emblems against ArtSource (tools/art_v11/install_art.py).

The committed manifests must name, for every content id and emblem family, the latest revision whose record says
runValid: true (derived here again, independently of the tool), with the record's SHA-256; the installed files must be
those originals byte for byte, and nothing else may sit beside them. The tool itself is run on a fixture tree: it picks
the latest valid revision over later invalid or unrecorded runs, refuses a missing or invalid record, and a second run
writes nothing.
"""

import contextlib
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import re
import shutil
import struct
import subprocess
import sys
import tempfile
import unittest
import zlib

# Test discovery must not leave Python cache files beside production sources.
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
TOOL = ROOT / "tools/art_v11/install_art.py"
ART = ROOT / "ArtSource/Art11"
MANIFESTS = ROOT / "tools/art_v11/install_manifests"
CONFIG = ROOT / "DicePrototype/Assets/Game/Data/P0Config.json"
STYLES = ("swiss", "sancai", "comic", "deco", "glass")
FAMILIES = ("seal", "constraint", "multiplier", "cards", "economy", "target", "final")
TARGETS = {style: f"DicePrototype/Assets/Resources/Themes/{style}/Art" for style in STYLES}
TARGETS["default"] = "DicePrototype/Assets/Art/Art11/Emblems"
ENTRY_KEYS = ["file", "revision", "sha256", "bytes", "size", "record"]


def load_tool(name="install_art_under_test"):
    spec = importlib.util.spec_from_file_location(name, TOOL)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def latest_valid(folder):
    """The latest rNN folder whose record.json says runValid: true (None when there is none)."""
    best = None
    for path in folder.iterdir():
        match = re.fullmatch(r"r(\d{2,})", path.name)
        record = path / "record.json"
        if match and record.is_file() and json.loads(record.read_text(encoding="utf-8")).get("runValid") is True:
            if best is None or int(match.group(1)) > best[0]:
                best = (int(match.group(1)), path.name)
    return best[1] if best else None


def config_ids():
    config = json.loads(CONFIG.read_text(encoding="utf-8"))
    return {item["id"] for key in ("controllers", "consumables", "services", "diceUpgrades") for item in config[key]}


class InstalledArtTests(unittest.TestCase):
    """The committed manifests and installed files."""

    @classmethod
    def setUpClass(cls):
        cls.manifests = {}
        for style in STYLES + ("default",):
            path = MANIFESTS / f"{style}.json"
            cls.manifests[style] = (path.read_text(encoding="utf-8"), json.loads(path.read_text(encoding="utf-8")))
        cls.ids = config_ids()

    def entries(self, style):
        data = self.manifests[style][1]
        return [("illustrations", e["id"], e) for e in data["illustrations"]] + \
               [("emblems", e["family"], e) for e in data["emblems"]]

    def test_manifest_format(self):
        for style, (text, data) in self.manifests.items():
            with self.subTest(style=style):
                self.assertEqual([k for k, _ in json.loads(text, object_pairs_hook=list)],
                                 ["schema", "style", "generatedBy", "rule", "target", "illustrations", "emblems"])
                self.assertEqual(data["schema"], "w22i-art-install-1")
                self.assertEqual(data["style"], style)
                self.assertEqual(data["generatedBy"], "tools/art_v11/install_art.py")
                self.assertEqual(data["target"], TARGETS[style])
                self.assertTrue(text.endswith("}\n") and "\r" not in text)
                for kind, key, entry in self.entries(style):
                    self.assertEqual(list(entry), [kind == "illustrations" and "id" or "family"] + ENTRY_KEYS, key)

    def test_every_content_id_and_family_once(self):
        for style in STYLES:
            with self.subTest(style=style):
                data = self.manifests[style][1]
                ids = [e["id"] for e in data["illustrations"]]
                self.assertEqual(len(ids), 184, "Review W22i scope if the content set changes.")
                self.assertEqual(set(ids), self.ids)
                self.assertEqual(len(set(ids)), len(ids))
                self.assertEqual([e["file"] for e in data["illustrations"]], [f"illustration_{i}.png" for i in ids])
        for style in STYLES + ("default",):
            with self.subTest(style=style, part="emblems"):
                emblems = self.manifests[style][1]["emblems"]
                self.assertEqual([e["family"] for e in emblems], list(FAMILIES))
                self.assertEqual([e["file"] for e in emblems], [f"emblem_{f}.png" for f in FAMILIES])
        self.assertEqual(self.manifests["default"][1]["illustrations"], [],
                         "the default style's illustrations stay in Art071/Illustrations")

    def test_entries_are_the_latest_valid_revision_with_the_record_hash(self):
        for style in STYLES + ("default",):
            for kind, key, entry in self.entries(style):
                with self.subTest(style=style, item=key):
                    folder = ART / style / kind / key
                    self.assertEqual(entry["revision"], latest_valid(folder), "the latest revision with a valid record")
                    record_path = folder / entry["revision"] / "record.json"
                    self.assertEqual(entry["record"], record_path.relative_to(ROOT).as_posix())
                    record = json.loads(record_path.read_text(encoding="utf-8"))
                    self.assertIs(record["runValid"], True)
                    self.assertEqual(record["revision"], entry["revision"])
                    self.assertEqual(record["output"]["sha256"], entry["sha256"])
                    self.assertEqual(record["output"]["archivedPath"],
                                     (folder / entry["revision"] / "original.png").relative_to(ROOT).as_posix())
                    if kind == "illustrations":
                        self.assertEqual(record["content"]["contentId"], key)
                    original = folder / entry["revision"] / "original.png"
                    self.assertEqual(sha256(original), entry["sha256"], "ArtSource original matches its record")
                    self.assertEqual(original.stat().st_size, entry["bytes"])
                    with original.open("rb") as handle:
                        head = handle.read(24)
                    self.assertEqual(list(struct.unpack(">II", head[16:24])), entry["size"])

    def test_installed_files_are_the_originals_byte_for_byte(self):
        for style in STYLES + ("default",):
            target = ROOT / TARGETS[style]
            data = self.manifests[style][1]
            listed = {e["file"] for e in data["illustrations"] + data["emblems"]}
            with self.subTest(style=style, part="folder"):
                installed = {p.name for p in target.glob("*.png")}
                self.assertEqual(installed, listed, "nothing beside the manifest's files")
                others = sorted(p.name for p in target.iterdir()
                                if not (p.suffix == ".png" or p.name.endswith(".png.meta")))
                self.assertEqual(others, [], "only PNGs and the .meta Unity writes")
            for kind, key, entry in self.entries(style):
                with self.subTest(style=style, item=key):
                    source = ROOT / entry["record"].rsplit("/", 1)[0] / "original.png"
                    self.assertEqual((target / entry["file"]).read_bytes(), source.read_bytes())

    def test_tool_check_passes_on_the_repository(self):
        result = subprocess.run([sys.executable, "-B", str(TOOL), "--check"], cwd=ROOT, capture_output=True, text=True,
                                timeout=1200)
        self.assertEqual(result.returncode, 0, result.stdout[-3000:] + result.stderr[-3000:])
        self.assertIn("INSTALL_CHECK_OK", result.stdout)


def png_bytes(seed):
    """A tiny valid 2x1 RGBA PNG whose pixels depend on seed."""
    raw = b"\x00" + bytes([seed % 256, 0, 0, 255, 0, seed % 256, 0, 128])
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", 2, 1, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b""))


class FixtureTree:
    """ArtSource-like tree with one style ("swiss", two content ids) and the default emblems."""

    IDS = ("C01", "C02")

    def __init__(self, root):
        self.root = Path(root)
        self.art = self.root / "ArtSource" / "Art11"
        self.game = self.root / "DicePrototype" / "Assets"
        self.manifests = self.root / "tools" / "art_v11" / "install_manifests"
        config = self.game / "Game" / "Data" / "P0Config.json"
        config.parent.mkdir(parents=True)
        config.write_text(json.dumps({"controllers": [{"id": i} for i in self.IDS], "consumables": [], "services": [],
                                      "diceUpgrades": []}), encoding="utf-8")
        seed = 1
        for content_id in self.IDS:
            self.revision("swiss", "illustrations", content_id, "r01", seed, True)
            seed += 1
        for style in ("swiss", "default"):
            for family in FAMILIES:
                self.revision(style, "emblems", family, "r01", seed, True)
                seed += 1

    def revision(self, style, kind, key, name, seed, valid, record=True, **override):
        folder = self.art / style / kind / key / name
        folder.mkdir(parents=True)
        data = png_bytes(seed)
        (folder / "original.png").write_bytes(data)
        if record:
            rel = (folder / "original.png").relative_to(self.root).as_posix()
            body = {"revision": name, "runValid": valid, "jobPath": f"ArtSource/Art11/{style}/{kind}/{key}/job.json",
                    "content": {"contentId": key} if kind == "illustrations" else None,
                    "output": {"archivedPath": rel, "sha256": hashlib.sha256(data).hexdigest()}}
            body.update(override)
            (folder / "record.json").write_text(json.dumps(body), encoding="utf-8")
        return folder

    def run(self, tool, styles=("swiss", "default"), check=False):
        paths = tool.Paths(self.root, self.art, self.game, self.manifests)
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = tool.install(paths, list(styles), check=check)
        return code, out.getvalue()

    def target(self, style="swiss"):
        return self.game / ("Art/Art11/Emblems" if style == "default" else f"Resources/Themes/{style}/Art")


class InstallToolTests(unittest.TestCase):
    def setUp(self):
        self.tool = load_tool()
        self.scratch = tempfile.TemporaryDirectory(prefix="w22i-install-")
        self.addCleanup(self.scratch.cleanup)
        self.tree = FixtureTree(self.scratch.name)

    def test_installs_byte_identical_and_a_second_run_writes_nothing(self):
        code, _ = self.tree.run(self.tool)
        self.assertEqual(code, 0)
        manifest = (self.tree.manifests / "swiss.json").read_bytes()
        target = self.tree.target()
        self.assertEqual(sorted(p.name for p in target.iterdir()),
                         sorted(["illustration_C01.png", "illustration_C02.png"] + [f"emblem_{f}.png" for f in FAMILIES]))
        self.assertEqual((target / "illustration_C02.png").read_bytes(),
                         (self.tree.art / "swiss/illustrations/C02/r01/original.png").read_bytes())
        stamps = {p.name: p.stat().st_mtime_ns for p in target.iterdir()}
        code, text = self.tree.run(self.tool)
        self.assertEqual(code, 0)
        self.assertIn("INSTALLED written=0 removed=0", text)
        self.assertEqual({p.name: p.stat().st_mtime_ns for p in target.iterdir()}, stamps)
        self.assertEqual((self.tree.manifests / "swiss.json").read_bytes(), manifest)
        self.assertEqual(self.tree.run(self.tool, check=True)[0], 0)

    def test_latest_valid_revision_wins_over_later_invalid_or_unrecorded_runs(self):
        folder = self.tree.art / "swiss/illustrations/C01"
        self.tree.revision("swiss", "illustrations", "C01", "r02", 50, True)
        self.tree.revision("swiss", "illustrations", "C01", "r03", 51, False)   # a later invalid run
        self.tree.revision("swiss", "illustrations", "C01", "r04", 52, True, record=False)   # interrupted, no record
        self.tree.revision("swiss", "illustrations", "C02", "r02", 53, False)
        self.assertEqual(self.tree.run(self.tool)[0], 0)
        data = json.loads((self.tree.manifests / "swiss.json").read_text(encoding="utf-8"))
        chosen = {e["id"]: e["revision"] for e in data["illustrations"]}
        self.assertEqual(chosen, {"C01": "r02", "C02": "r01"})
        self.assertEqual((self.tree.target() / "illustration_C01.png").read_bytes(),
                         (folder / "r02/original.png").read_bytes())

    def test_refuses_without_a_valid_record(self):
        cases = {
            "no valid run": lambda: self.invalidate("C02"),
            "sha256 mismatch": lambda: self.tree.revision("swiss", "illustrations", "C02", "r02", 60, True,
                                                          output={"archivedPath": "ArtSource/Art11/swiss/illustrations/C02/r02/original.png",
                                                                  "sha256": "0" * 64}),
            "missing original": lambda: (self.tree.revision("swiss", "illustrations", "C02", "r02", 61, True)
                                         / "original.png").unlink(),
            "wrong content id": lambda: self.tree.revision("swiss", "illustrations", "C02", "r02", 62, True,
                                                           content={"contentId": "C01"}),
            "wrong revision": lambda: self.tree.revision("swiss", "illustrations", "C02", "r02", 63, True, revision="r01"),
            "no folder for an id": lambda: shutil.rmtree(self.tree.art / "swiss/illustrations/C02"),
            "emblem without a record": lambda: os.remove(self.tree.art / "default/emblems/final/r01/record.json"),
        }
        for name, break_it in cases.items():
            with self.subTest(case=name):
                scratch = tempfile.TemporaryDirectory(prefix="w22i-install-")
                self.addCleanup(scratch.cleanup)
                self.tree = FixtureTree(scratch.name)
                break_it()
                with self.assertRaises(self.tool.InstallError):
                    self.tree.run(self.tool)
                self.assertFalse(self.tree.target().exists(), "nothing is written when the plan is refused")
                self.assertFalse(self.tree.manifests.exists())

    def invalidate(self, content_id):
        record = self.tree.art / f"swiss/illustrations/{content_id}/r01/record.json"
        data = json.loads(record.read_text(encoding="utf-8"))
        data["runValid"] = False
        record.write_text(json.dumps(data), encoding="utf-8")

    def test_stale_files_are_removed_with_their_meta_and_check_reports_them(self):
        self.assertEqual(self.tree.run(self.tool)[0], 0)
        target = self.tree.target()
        (target / "illustration_C99.png").write_bytes(png_bytes(9))
        (target / "illustration_C99.png.meta").write_text("guid", encoding="utf-8")
        (target / "illustration_C01.png").write_bytes(png_bytes(99))
        code, text = self.tree.run(self.tool, check=True)
        self.assertEqual(code, 1)
        self.assertIn("illustration_C99.png: not in the plan", text)
        self.assertIn("illustration_C01.png: not the r01 original", text)
        code, text = self.tree.run(self.tool)
        self.assertEqual(code, 0)
        self.assertIn("INSTALLED written=1 removed=1", text)
        self.assertFalse((target / "illustration_C99.png").exists())
        self.assertFalse((target / "illustration_C99.png.meta").exists())
        self.assertEqual(self.tree.run(self.tool, check=True)[0], 0)


if __name__ == "__main__":
    unittest.main()
