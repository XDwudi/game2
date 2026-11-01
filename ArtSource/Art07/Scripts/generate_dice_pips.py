"""Source manifest entrypoint for deterministic pips and material masks."""
from pathlib import Path
import runpy,sys
folder=Path(__file__).resolve().parents[3]/'tools/art_v07'
sys.path.insert(0,str(folder))
runpy.run_path(str(folder/'build_dice_textures.py'),run_name='__main__')
