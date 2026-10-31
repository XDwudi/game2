"""Independent rerun of the reviewed asset validator, restricted to review outputs.

This is not an independent image-generation implementation or Unity import test.
The producer validator has been read before importing; its only write path is OUT.
"""
from pathlib import Path
import datetime
import hashlib
import importlib.util
import json
import sys

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
SOURCE = ROOT / 'evidence/v06/art/validate_assets.py'
RESULTS = OUT / (sys.argv[1] if len(sys.argv)>1 else 'assets-rerun02')
assert RESULTS.resolve().parent == OUT.resolve(), 'Output must stay under review'
assert not RESULTS.exists(), 'Refusing to overwrite retained results'
FILES = [SOURCE, ROOT/'art/scripts/build_symbols_v06.py', ROOT/'art/scripts/build_symbols.py',
         ROOT/'DicePrototype/Assets/Game/Art/asset_map.json', ROOT/'DicePrototype/Assets/Game/Data/P0Config.json']

def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def snapshot():
    files = FILES + list((ROOT/'DicePrototype/Assets/Game/Art').rglob('*.png')) + list((ROOT/'DicePrototype/Assets/Game/Audio').rglob('*.wav')) + list((ROOT/'art/source_icons').rglob('*.svg')) + list((ROOT/'art/previews/v06').glob('*.png'))
    return {str(p.relative_to(ROOT)).replace('\\','/'):digest(p) for p in sorted(set(files))}

before = snapshot()
started = datetime.datetime.now(datetime.timezone.utc).isoformat()
module_spec = importlib.util.spec_from_file_location('reviewed_asset_validation', SOURCE)
module = importlib.util.module_from_spec(module_spec)
module_spec.loader.exec_module(module)
module.OUT = RESULTS
module.validate()
after = snapshot()
assert before == after, 'Asset source changed during review rerun'
result = json.loads((RESULTS/'asset_validation.json').read_text(encoding='utf8'))
# Verify every source named by the validator was included in the before/after binding.
for item in result['icons']:
    assert item['source'] in before
for item in result['enchantments']:
    for kind in ('badge','frame'):
        assert item[kind]['source'] in before
binding = {'startedUtc':started,'completedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
           'sourceStableDuringRun':True,'validatorOrigin':'Producer validator statically reviewed, rerun by independent reviewer with output redirected under review.',
           'sourceHashes':before,'counts':{key:len(result[key]) for key in ('icons','enchantments','audio','pipComponents')},
           'reportSha256':digest(RESULTS/'asset_validation.json'),
           'limits':['No Unity import or build execution','No native screen or audio playback verification','Original icon visual style remains subject to user acceptance']}
(RESULTS/'binding.json').write_text(json.dumps(binding,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(json.dumps(binding['counts']))
