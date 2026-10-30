"""Generate deterministic additional fixtures using the reviewed Appendix F reference.

This is a test-data generator, not the game's RNG or production rule implementation.
"""
import hashlib
import json
from pathlib import Path
import random
import re

ROOT = Path(__file__).resolve().parents[1]
spec = (ROOT / 'docs/specs/P0_v0.5.md').read_text(encoding='utf-8-sig')
code = re.search(r'```python\s*\n(.*?)\n```', spec, re.S).group(1)
# This exact source was reviewed before execution: imports Counter/Fraction and defines
# deterministic scoring/probability functions. The guard prevents silent future code changes.
EXPECTED_SHA = '0b2583b283a41aebdd53d3615ce57d12588cd4ae0c2c91e8d6693d1b4bc24b54'
source_sha = hashlib.sha256(code.encode()).hexdigest()
if source_sha != EXPECTED_SHA:
    raise SystemExit(f'Reference source requires review: {source_sha}')
reference = {}
exec(compile(code, 'P0_v0.5 Appendix F reviewed reference', 'exec'), reference)
config = json.loads((ROOT / 'DicePrototype/Assets/Game/Data/P0Config.json').read_text(encoding='utf-8'))
rng = random.Random(50317)
scores = []
for i in range(1024):
    capacity = rng.randint(1, 8)
    dice = [dict(p=rng.randint(1, 6), c=rng.choice(['red', 'blue']), sum6=rng.randint(6, 36), gold=bool(rng.getrandbits(1)), glass=bool(rng.getrandbits(1))) for _ in range(rng.randint(1, capacity))]
    ids = sorted(rng.sample([c['id'] for c in config['controllers']], rng.randint(0, 10)))
    enchants = {id: rng.choice(['none', 'slotless'] if id in reference['NON_SUBMIT'] else ['none', 'slotless', 'base5', 'mult120']) for id in ids}
    levels = {h['id']: rng.randint(0, 8) for h in config['handDefinitions']}
    rolls = rng.randint(0, 15)
    wave = bool(rng.getrandbits(1))
    ordinal = rng.randint(0, 3)
    scores.append(dict(id=f'EXTRA-{i:04}', dice=dice, controllerIds=ids, enchants=enchants, levels=levels, rollsRemaining=rolls, capacity=capacity, redWave=wave, submissionOrdinal=ordinal,
        expected=reference['evaluate'](dice, rolls, ids, levels, capacity, wave, enchants, ordinal)))
probabilities = []
for i in range(1024):
    faces = [(f'F{j+1:02}', rng.randint(1, 6), rng.choice(['none', 'smooth', 'rough']), rng.choice([1, 2, 17, 100, 1000000, 2147483647])) for j in range(6)]
    large, small, high, low = [bool(rng.getrandbits(1)) for _ in range(4)]
    buffs = ([6] if large else []) + ([1] if small else [])
    probabilities.append(dict(faces=[dict(faceId=f[0], permanentPip=f[1], texture=f[2], baseWeight=f[3], baseColor='blue', isGold=False) for f in faces], large=large, small=small, high=high, low=low, expected=reference['probs'](faces, buffs, high, low)))
out = ROOT/'evidence/v05/rules/reference-extra.json'
out.write_text(json.dumps(dict(referenceSha256=source_sha, fixtureSeed=50317, scores=scores, probabilities=probabilities), ensure_ascii=False, separators=(',', ':'))+'\n', encoding='utf-8')
print(f'{len(scores)} scores and {len(probabilities)} exact Fraction probability fixtures written; {out.stat().st_size} bytes')
