"""Import Appendix A/B verbatim JSON values; never derive expectations from production code."""
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
source = ROOT / 'docs/specs/P0_v0.6.md'
blocks = re.findall(r'```json\s*(.*?)\s*```', source.read_text(encoding='utf-8'), re.S)
assert len(blocks) == 2
config, golden = map(json.loads, blocks)
assert config['configVersion'] == 'p0.6.0' and len(golden) == 160
for name, value in [('P0Config.json', config), ('GoldenScores.json', golden)]:
    target = ROOT / 'DicePrototype/Assets/Game/Data' / name
    target.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
out = ROOT / 'evidence/v06/rules'
out.mkdir(parents=True, exist_ok=True)
(out / 'import-provenance.json').write_text(json.dumps({'source': str(source.relative_to(ROOT)), 'source_sha256': hashlib.sha256(source.read_bytes()).hexdigest(), 'golden_count': len(golden), 'golden_expected_fields': list(golden[0]['expected']), 'copied_without_arithmetic': True}, indent=2), encoding='utf-8')
