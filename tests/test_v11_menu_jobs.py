"""Exercise the five W23 jobs through the artgen composition used by the batch runner."""
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools/art_gen"))
sys.path.insert(0, str(ROOT / "tools/art_v11"))
import artgen
import install_menu_tray
import make_menu_jobs as menu


class MenuJobTests(unittest.TestCase):
    def test_tray_importers_copy_the_golden_settings_and_keep_distinct_stable_guids(self):
        with tempfile.TemporaryDirectory() as directory:
            folder = Path(directory)
            reference = (ROOT / "DicePrototype/Assets/Resources/MenuArt17/default.png.meta").read_text(encoding="utf-8")
            (folder / "default.png.meta").write_text(reference, encoding="utf-8")
            guids = {reference.splitlines()[1]}
            for theme in menu.MATERIALS:
                target = folder / (theme + ".png")
                meta = target.with_suffix(".png.meta")
                install_menu_tray.install_import_settings(target, theme)
                contents = meta.read_text(encoding="utf-8")
                lines = contents.splitlines()
                self.assertEqual(lines[2:], reference.splitlines()[2:])
                self.assertNotIn(lines[1], guids)
                guids.add(lines[1])
                install_menu_tray.install_import_settings(target, theme)
                self.assertEqual(contents, meta.read_text(encoding="utf-8"))

    def test_all_six_installed_trays_match_their_native_sources_and_importers(self):
        from PIL import Image
        base = ROOT / "DicePrototype/Assets/Resources/MenuArt17"
        reference = (base / "default.png.meta").read_text(encoding="utf8").splitlines()
        sources, assets, guids = set(), set(), set()
        for theme in ("default", *menu.MATERIALS):
            with self.subTest(theme=theme):
                folder = ROOT / "ArtSource/Art11" / theme / "menu"
                installed = artgen.load_json(folder / "install.json")
                revision = folder / installed["revision"]
                record = artgen.load_json(revision / "record.json")
                original = revision / "original.png"
                target = base / (theme + ".png")
                self.assertIs(record["runValid"], True)
                self.assertEqual(record["revision"], revision.name)
                self.assertEqual(record["jobPath"], (folder / "job.json").relative_to(ROOT).as_posix())
                self.assertEqual(record["output"]["archivedPath"], original.relative_to(ROOT).as_posix())
                self.assertEqual(artgen.sha256_file(original), record["output"]["sha256"])
                self.assertEqual(installed["sourceSha256"], record["output"]["sha256"])
                self.assertEqual(installed["asset"], target.relative_to(ROOT).as_posix())
                self.assertEqual(artgen.sha256_file(target), installed["sha256"])
                with Image.open(target) as picture:
                    self.assertEqual((picture.size,picture.mode), ((1060,552),"RGBA"))
                settings = target.with_suffix(".png.meta").read_text(encoding="utf8").splitlines()
                self.assertEqual(settings[2:],reference[2:])
                sources.add(installed["sourceSha256"])
                assets.add(installed["sha256"])
                guids.add(settings[1])
        self.assertEqual((len(sources),len(assets),len(guids)), (6,6,6))

    def test_jobs_match_actual_theme_ids_and_the_menu_material_spec(self):
        descriptors = (ROOT / "DicePrototype/Assets/Resources/Themes").glob("*/theme.json")
        themes = {artgen.load_json(path)["id"] for path in descriptors}
        materials = {"swiss": "folded warm-paper", "sancai": "glaze", "comic": "halftone",
                     "deco": "black lacquer", "glass": "lead seams"}
        self.assertEqual(themes, materials.keys())
        self.assertEqual(themes, menu.MATERIALS.keys())
        for theme, material in materials.items():
            with self.subTest(theme=theme):
                job = artgen.load_job(ROOT / menu.ART / theme / "menu/job.json")
                self.assertEqual(job, menu.menu_job(theme))
                self.assertEqual(job["jobId"], f"V11-{theme.upper()}-MENU-TRAY")
                self.assertIn(material, job["subject"])
                self.assertIn("single uninterrupted resting surface", job["subject"])
                self.assertIn("No cards, dice, pips", job["subject"])

    def test_prompt_uses_themes_own_plate_and_verified_r02_only_for_the_outline(self):
        original = "ArtSource/Art11/default/menu/r02/original.png"
        record = artgen.load_json(ROOT / Path(original).with_name("record.json"))
        self.assertTrue(record["runValid"])
        self.assertEqual(record["output"]["archivedPath"], original)
        for theme in menu.MATERIALS:
            with self.subTest(theme=theme):
                kit = artgen.load_kit(ROOT, menu.KITS / theme / "style_kit.json")
                self.assertEqual(artgen.verify_kit(kit, ROOT), [])
                job = artgen.load_job(ROOT / menu.ART / theme / "menu/job.json")
                plan = artgen.compose(kit, job, ROOT)
                plate = kit["categories"]["illustration"]["plate"]
                style, silhouette = plan["references"]
                self.assertTrue(plan["transparent"])
                self.assertEqual((style["path"], style["sha256"]), (plate["path"], plate["sha256"]))
                self.assertTrue(style["role"].startswith("STYLE PLATE"))
                self.assertEqual((silhouette["path"], silhouette["sha256"]), (original, record["output"]["sha256"]))
                self.assertIn("SUBJECT AND SILHOUETTE REFERENCE ONLY", silhouette["role"])
                self.assertIn("Reference 1 is the only style reference", silhouette["role"])
                self.assertIn(job["subject"], plan["imagePrompt"])
                self.assertNotIn("edit", job)

    def test_batch_list_matches_generated_jobs_and_reruns_preserve_them(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            reference = root / menu.DEFAULT_TRAY
            reference.parent.mkdir(parents=True)
            reference.touch()
            for theme in menu.MATERIALS:
                kit = root / menu.KITS / theme / "style_kit.json"
                kit.parent.mkdir(parents=True)
                kit.touch()
            job_list = root / "batch/menu.txt"
            with patch.object(menu, "ROOT", root), patch.object(sys, "argv", ["make_menu_jobs", "--list", str(job_list)]):
                menu.main()
                lines = job_list.read_text(encoding="utf-8").splitlines()
                self.assertEqual(len(lines), len(menu.MATERIALS))
                mtimes = {}
                for theme, line in zip(menu.MATERIALS, lines):
                    kit, path = line.split("|")
                    self.assertEqual(kit, f"ArtSource/StyleKit/themes/{theme}/style_kit.json")
                    self.assertEqual(path, f"ArtSource/Art11/{theme}/menu/job.json")
                    job = root / path
                    self.assertEqual(json.loads(job.read_text(encoding="utf-8")), menu.menu_job(theme))
                    mtimes[job] = job.stat().st_mtime_ns
                menu.main()
                self.assertEqual(mtimes, {job: job.stat().st_mtime_ns for job in mtimes})


if __name__ == "__main__":
    unittest.main()
