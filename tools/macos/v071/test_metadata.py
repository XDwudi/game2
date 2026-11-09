"""Real license-byte boundary tests; no Mac/Unity execution or acceptance claim."""
from pathlib import Path
import tempfile
import unittest

from verify_mac import metadata_inventory


ROOT = Path(__file__).resolve().parents[3]
BOLD = {
    "Licenses/NotoSansSC-Bold-OFL.txt": ROOT / "ArtSource/Art071/Fonts/LICENSE.txt",
    "Licenses/NotoSansSC-Bold-COPYRIGHT.txt": ROOT / "ArtSource/Art071/Fonts/COPYRIGHT.txt",
}


class MacBoldMetadataTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.payload = Path(self.temp.name)
        files = {
            **BOLD,
            "Licenses/NotoSansCJK-OFL.txt": ROOT / "DicePrototype/Assets/Game/Fonts/LICENSE.txt",
            "开始游玩.txt": ROOT / "docs/PLAYER_README_V071.txt",
        }
        (self.payload / "BUILD_IDENTITY.json").write_text('{"testFixture":true}\n', encoding="utf-8")
        for name, source in files.items():
            target = self.payload / name
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(source.read_bytes())

    def test_each_missing_bold_notice_rejects_fresh_payload(self):
        metadata_inventory(self.payload)
        for name in BOLD:
            with self.subTest(name=name):
                target = self.payload / name
                original = target.read_bytes()
                target.unlink()
                try:
                    with self.assertRaisesRegex(ValueError, "Missing regular release metadata/license"):
                        metadata_inventory(self.payload)
                finally:
                    target.write_bytes(original)

    def test_same_length_bold_notice_tampering_changes_roundtrip_manifest(self):
        before = metadata_inventory(self.payload)
        for name in BOLD:
            with self.subTest(name=name):
                target = self.payload / name
                original = target.read_bytes()
                self.assertTrue(original)
                target.write_bytes((b"Z" if original[:1] != b"Z" else b"Y") + original[1:])
                try:
                    self.assertEqual(len(original), target.stat().st_size)
                    # The real fresh-extraction path compares these manifests exactly.
                    self.assertNotEqual(before, metadata_inventory(self.payload))
                finally:
                    target.write_bytes(original)
        self.assertEqual(before, metadata_inventory(self.payload))


if __name__ == "__main__":
    unittest.main()
