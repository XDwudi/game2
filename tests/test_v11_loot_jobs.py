"""W17's first six illustrations must resolve real content and reach the existing artgen prompt path."""
import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools" / "art_gen"))
sys.path.insert(0, str(ROOT / "tools" / "art_v11"))
import artgen
import make_loot_jobs as loot


class LootJobTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.table = json.loads((ROOT / loot.TABLE).read_text(encoding="utf-8"))
        cls.config = artgen.load_json(ROOT / artgen.CONFIG_DEFAULT)["lootKinds"]
        cls.kit = artgen.load_kit(ROOT, loot.KIT)

    def test_default_jobs_match_config_and_subjects(self):
        ids = {entry["id"] for entry in self.config}
        self.assertEqual(ids, {"L1", "L2", "L3", "L4", "L5", "L6"})
        self.assertEqual(ids, self.table["subjects"].keys())
        expected = loot.build_jobs(self.table)
        actual = {path.relative_to(ROOT) for path in (ROOT / loot.JOBS).glob("*/job.json")}
        self.assertEqual(actual, expected.keys())
        for path, job in expected.items():
            with self.subTest(path=path):
                self.assertEqual(artgen.load_job(ROOT / path), job)
                self.assertEqual(path.parent.name, job["contentId"])
                self.assertEqual(job["jobId"], "V11-DEFAULT-" + path.parent.name)

    def test_real_prompt_gets_pinned_default_plate_and_functional_cues(self):
        self.assertEqual(loot.KIT, artgen.KIT_DEFAULT)
        plate = self.kit["categories"]["illustration"]["plate"]
        for entry in self.config:
            cid = entry["id"]
            with self.subTest(content_id=cid):
                subject = self.table["subjects"][cid]
                content = artgen.load_content(ROOT, artgen.CONFIG_DEFAULT, cid)
                self.assertEqual(content["displayName"], subject["name"])
                self.assertEqual(content["configDescription"], entry["description"])
                self.assertEqual(content["list"], "lootKinds")
                job = artgen.load_job(ROOT / loot.JOBS / cid / "job.json")
                plan = artgen.compose(self.kit, job, ROOT, content)
                self.assertTrue(plan["transparent"])
                self.assertEqual(plan["content"]["contentId"], cid)
                self.assertEqual([(p["path"], p["sha256"]) for p in plan["references"]],
                                 [(plate["path"], plate["sha256"])])
                for cue in [subject["subject"], *subject["cues"], *subject["avoid"], *self.table["defaults"]["cues"]]:
                    self.assertIn(cue, plan["imagePrompt"])


if __name__ == "__main__":
    unittest.main()
