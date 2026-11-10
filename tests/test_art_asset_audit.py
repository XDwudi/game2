"""Real PNG fixtures and negative boundaries for the read-only art auditor."""

import contextlib
import copy
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest

from PIL import Image


SCRIPT = Path(__file__).resolve().parents[1] / "tools" / "art_asset_audit.py"
spec = importlib.util.spec_from_file_location("art_asset_audit", SCRIPT)
audit_tool = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit_tool)


class ArtAssetAuditTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.put("spec.md", b"specified")
        self.put("config.json", b"{}")
        self.png()
        self.manifest = {
            "inputSpec": "spec.md", "inputSpecSha256": self.hash("spec.md"),
            "contentSource": "config.json", "contentSourceSha256": self.hash("config.json"),
            "references": [{"path": "sample.png", "sha256": self.hash("sample.png")}],
            "assets": [{"path": "sample.png", "sha256": self.hash("sample.png"),
                        "contentId": "C01", "dimensions": [4, 4]}],
        }

    def put(self, path, content):
        target = self.root / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(content)
        return target

    def hash(self, path):
        return audit_tool.digest((self.root / path).read_bytes())

    def png(self, *, mode="RGBA", color=(230, 220, 200, 255)):
        image = Image.new(mode, (4, 4), color)
        if mode == "RGBA" and color[3] == 255:
            image.putpixel((0, 0), (0, 0, 0, 0))
            image.putpixel((1, 0), (230, 220, 200, 128))
        image.save(self.root / "sample.png")

    def refresh_hashes(self):
        self.manifest["assets"][0]["sha256"] = self.hash("sample.png")
        self.manifest["references"][0]["sha256"] = self.hash("sample.png")

    def run_audit(self, **kwargs):
        self.put("manifest.json", json.dumps(self.manifest).encode())
        return audit_tool.audit(self.root, "manifest.json", **kwargs)

    def assert_fails(self, report, fragment):
        self.assertEqual(report["result"], "FAILED")
        self.assertIn(fragment, "\n".join(report["errors"]))

    def test_valid_png_runtime_and_no_input_mutation(self):
        self.put("runtime/sample.png", (self.root / "sample.png").read_bytes())
        self.put("runtime/sample.png.meta", b"fileFormatVersion: 2\nguid: fixture\n")
        before = {p: p.read_bytes() for p in self.root.rglob("*") if p.is_file()}
        report = self.run_audit(require_alpha=True, runtime_dir="runtime")
        self.assertEqual(report["result"], "INTEGRITY_PASSED")
        image = report["assets"][0]["image"]
        self.assertEqual(image["alphaExtrema"], [0, 255])
        self.assertEqual(image["transparentPixels"], 1)
        self.assertEqual(image["partialAlphaPixels"], 1)
        self.assertEqual(image["opaquePixels"], 14)
        self.assertEqual(image["visibleBounds"], [0, 0, 4, 4])
        self.assertTrue(report["notVerified"])
        self.assertTrue(all(path.read_bytes() == content for path, content in before.items()))

    def test_empty_assets_and_wrong_shapes_fail(self):
        original = copy.deepcopy(self.manifest)
        for replacement in ([], None, {}, [None]):
            self.manifest = copy.deepcopy(original)
            self.manifest["assets"] = replacement
            self.assertEqual(self.run_audit()["result"], "FAILED")
        self.manifest = original
        self.manifest["references"] = None
        self.assert_fails(self.run_audit(), "references must")

    def test_missing_and_stale_input_fail(self):
        (self.root / "spec.md").unlink()
        self.assert_fails(self.run_audit(), "missing file")
        self.put("spec.md", b"changed")
        self.assert_fails(self.run_audit(), "SHA256 mismatch")

    def test_duplicate_ids_and_paths_fail(self):
        self.manifest["assets"].append(copy.deepcopy(self.manifest["assets"][0]))
        self.assert_fails(self.run_audit(), "duplicate contentId")
        self.manifest["assets"][1]["contentId"] = "C02"
        self.assert_fails(self.run_audit(), "duplicate asset path")

    def test_outside_and_absolute_manifest_paths_fail(self):
        for path in ("../outside.png", str(self.root / "sample.png"), "C:\\elsewhere\\asset.png"):
            self.manifest["assets"][0]["path"] = path
            self.assertEqual(self.run_audit()["result"], "FAILED")

    def test_missing_asset_bad_hash_dimensions_and_id_fail(self):
        original = copy.deepcopy(self.manifest)
        for field, value, fragment in (("path", "missing.png", "missing file"),
                                       ("sha256", "0" * 64, "SHA256 mismatch"),
                                       ("sha256", "bad", "invalid or missing sha256"),
                                       ("dimensions", [5, 4], "dimensions mismatch"),
                                       ("dimensions", [True, 4], "positive integers"),
                                       ("contentId", "", "non-empty string")):
            self.manifest = copy.deepcopy(original)
            self.manifest["assets"][0][field] = value
            self.assert_fails(self.run_audit(), fragment)

    def test_corrupt_empty_and_non_png_fail(self):
        for data in (b"", b"not an image", (self.root / "sample.png").read_bytes()[:40]):
            self.put("sample.png", data)
            self.refresh_hashes()
            self.assertEqual(self.run_audit()["result"], "FAILED")
        Image.new("RGB", (4, 4)).save(self.root / "sample.png", format="JPEG")
        self.refresh_hashes()
        self.assert_fails(self.run_audit(), "not a PNG")

    def test_fully_transparent_fails_and_opaque_is_opt_in(self):
        self.png(color=(0, 0, 0, 0))
        self.refresh_hashes()
        self.assert_fails(self.run_audit(), "entirely transparent")
        self.png(mode="RGB", color=(100, 100, 100))
        self.refresh_hashes()
        self.assertEqual(self.run_audit()["result"], "INTEGRITY_PASSED")
        self.assert_fails(self.run_audit(require_alpha=True), "fully opaque")

    def test_runtime_missing_mismatch_and_meta_fail(self):
        (self.root / "runtime").mkdir()
        self.assert_fails(self.run_audit(runtime_dir="runtime"), "missing file")
        self.put("runtime/sample.png", b"changed")
        self.assert_fails(self.run_audit(runtime_dir="runtime"), "SHA256 mismatch")
        self.put("runtime/sample.png", (self.root / "sample.png").read_bytes())
        self.assert_fails(self.run_audit(runtime_dir="runtime"), ".meta missing")
        self.put("runtime/sample.png.meta", b"")
        self.assert_fails(self.run_audit(runtime_dir="runtime"), ".meta is empty")

    def test_malformed_manifest_reports_failure(self):
        self.put("broken.json", b"{ nope")
        self.assertEqual(audit_tool.audit(self.root, "broken.json")["result"], "FAILED")

    def test_duplicate_json_keys_cannot_hide_conflicting_data(self):
        self.put("duplicate.json", b'{"assets": [], "assets": []}')
        self.assert_fails(audit_tool.audit(self.root, "duplicate.json"), "duplicate JSON key")

    def test_cli_preserves_existing_output_and_input(self):
        self.run_audit()
        original = (self.root / "manifest.json").read_bytes()
        args = ["--root", str(self.root), "--manifest", "manifest.json"]
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(audit_tool.main(args + ["--output", "manifest.json"]), 2)
            self.assertEqual(audit_tool.main(args + ["--output", "../outside-report.json"]), 2)
            self.assertEqual(audit_tool.main(args + ["--output", "report.json"]), 0)
            report_bytes = (self.root / "report.json").read_bytes()
            self.assertEqual(audit_tool.main(args + ["--output", "report.json"]), 2)
        self.assertEqual((self.root / "manifest.json").read_bytes(), original)
        self.assertEqual((self.root / "report.json").read_bytes(), report_bytes)

    def test_cli_failure_exit_and_report(self):
        self.manifest["assets"] = []
        self.run_audit()
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            code = audit_tool.main(["--root", str(self.root), "--manifest", "manifest.json",
                                    "--output", "failed.json"])
        self.assertEqual(code, 1)
        self.assertEqual(json.loads((self.root / "failed.json").read_text())["result"], "FAILED")


if __name__ == "__main__":
    unittest.main()
