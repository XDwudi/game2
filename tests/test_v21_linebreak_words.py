"""Checks the reviewed W21 lexicon against the live configuration and the face-panel test corpus."""
import importlib.util
import tempfile
import unittest
from pathlib import Path

tool = Path(__file__).resolve().parents[1] / "tools" / "v21_linebreak_words.py"
spec = importlib.util.spec_from_file_location("v21_words", tool)
words = importlib.util.module_from_spec(spec)
spec.loader.exec_module(words)


class LineBreakWordsTests(unittest.TestCase):
    def test_every_protected_word_is_used_in_the_target_text(self):
        count, texts = words.check()
        self.assertGreater(count, 0)
        self.assertGreater(texts, 184 + 39 + 6)

    def test_review_report_preserves_non_punctuation_break_contexts(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "layouts.tsv"
            target = Path(directory) / "review.tsv"
            source.write_text("theme\tscenario\tid\twidth\tfont\tcap\tlines\n"
                              "swiss\tshop100\tC01\t184\t20\t4\t每次提交后 / 获得1金币。\n"
                              "comic\tshop100\tC02\t186\t20\t4\t获得， / 才能使用。\n", encoding="utf-8")
            self.assertEqual((2, 1), words.report_candidates(source, target))
            self.assertIn("后|获\t次提交后|获得1金\tC01\tswiss", target.read_text(encoding="utf-8"))
