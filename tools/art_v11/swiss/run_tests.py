"""Run unchanged unittest discovery with all temporary writes in W12's allowed area.

Old tests explicitly request tools/v11_text temporary directories. Redirect only
temporary-directory placement, preserving every test and assertion unchanged.
"""
import os
from pathlib import Path
import sys
import tempfile
import unittest

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[3]
SCRATCH = ROOT / "ArtSource/Art11/swiss/test-temp"
SCRATCH.mkdir(parents=True, exist_ok=True)
tempfile.tempdir = str(SCRATCH)
os.environ["TMP"] = os.environ["TEMP"] = str(SCRATCH)
os.environ["PYTHONDONTWRITEBYTECODE"] = "1"
original_mkdtemp = tempfile.mkdtemp


def confined_mkdtemp(suffix=None, prefix=None, dir=None):
    return original_mkdtemp(suffix=suffix, prefix=prefix, dir=str(SCRATCH))


tempfile.mkdtemp = confined_mkdtemp
sys.path.insert(0, str(ROOT))
result = unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.discover(str(ROOT / "tests")))
tempfile.mkdtemp = original_mkdtemp
if not any(SCRATCH.iterdir()):
    SCRATCH.rmdir()
raise SystemExit(not result.wasSuccessful())
