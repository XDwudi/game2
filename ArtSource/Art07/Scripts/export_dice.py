"""Source manifest entrypoint; uses the versioned, editable export implementation."""
from pathlib import Path
import runpy
runpy.run_path(str(Path(__file__).resolve().parents[3]/'tools/art_v07/build_dice.py'),run_name='__main__')
