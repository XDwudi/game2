"""Independent read-only delivery review probes. No native tools, network, or release mutation."""
import copy
import hashlib
import json
import os
from pathlib import Path
import sys
import tempfile
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).parent
sys.dont_write_bytecode = True
sys.path[:0] = [str(ROOT / 'tools'), str(ROOT / 'tools/macos/v06')]
import package_v06 as packaging
from compare_replays import validate_report
from test_mac_tools import report, normal_row, debug_row, IDENTITY
from verify_mac import metadata_inventory
from compare_replays import COMMAND_KINDS
import re

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

sources = [ROOT / p for p in ['tools/package_v06.py', 'tools/release_v06_manifest.py', 'tools/publish_v06_release.py',
    'tools/macos/v06/compare_replays.py', 'tools/macos/v06/verify_mac.py', 'tools/macos/v06/package_checks.py']]
before = {str(p.relative_to(ROOT)): digest(p) for p in sources}
findings = []
mac = report(True)
baseline = validate_report(mac, 'macOS')
mac['displayChecks'].append({'id': 'exclusive-non-native-escape', 'status': 'not_applicable',
    'reason': 'macOS uses MaximizedWindow; no ExclusiveFullScreen claim is made.'})
result = validate_report(mac, 'macOS')
findings.append({'id': 'DR01', 'scenario': 'Actual frozen Player Mac display status added to otherwise valid synthetic report',
    'baselineFailures': baseline, 'actualFailures': result, 'defectReproduced': baseline == [] and result != []})

# Deliberately synthesize only one completed row while preserving hashes of the ACTUAL
# 145/175-command input traces. A gate should derive coverage from those source inputs.
with tempfile.TemporaryDirectory(prefix='delivery-review-', dir=OUT / 'delivery-review-fixtures') as tmp:
    root = Path(tmp)
    trace_dir = root / 'evidence/v06/session'
    trace_dir.mkdir(parents=True)
    for name in ['normal-run-trace.json', 'debug-24-trace.json']:
        (trace_dir / name).write_bytes((ROOT / 'evidence/v06/session' / name).read_bytes())
    results = root / 'windows'
    results.mkdir()
    actual = report(False)
    actual['traceSha256'] = digest(trace_dir / 'normal-run-trace.json')
    actual['debugTraceSha256'] = digest(trace_dir / 'debug-24-trace.json')
    (results / 'player-report.json').write_text(json.dumps(actual), encoding='utf-8')
    (results / 'normal-replay.jsonl').write_text(json.dumps(normal_row()) + '\n', encoding='utf-8')
    (results / 'debug24-replay.jsonl').write_text(json.dumps(debug_row()) + '\n', encoding='utf-8')
    rejected, error = False, None
    with patch.object(packaging, 'ROOT', root):
        try:
            packaging.validate_windows_report(results, IDENTITY)
        except (ValueError, KeyError, TypeError) as exc:
            rejected, error = True, str(exc)
    findings.append({'id': 'DR02', 'scenario': 'One-row evidence with real trace SHA hashes and self-declared report counts',
        'actualNormalTraceSteps': len(json.loads((trace_dir / 'normal-run-trace.json').read_text(encoding='utf-8-sig'))['steps']),
        'actualDebugTraceSteps': len(json.loads((trace_dir / 'debug-24-trace.json').read_text(encoding='utf-8-sig'))['steps']),
        'providedRowsEach': 1, 'rejected': rejected, 'error': error, 'defectReproduced': not rejected})
    metadata = root / 'metadata'
    (metadata / 'Licenses').mkdir(parents=True)
    names = ['BUILD_IDENTITY.json', 'Licenses/NotoSansCJK-OFL.txt', '开始游玩.txt']
    for name in names:
        (metadata / name).write_text('synthetic reviewer fixture ' + name, encoding='utf-8')
    rows = metadata_inventory(metadata)
    missing_rejected = []
    for name in names:
        path = metadata / name
        original = path.read_bytes()
        path.unlink()
        try:
            metadata_inventory(metadata)
            missing_rejected.append(False)
        except ValueError:
            missing_rejected.append(True)
        path.write_bytes(original)
    source = (ROOT / 'tools/macos/v06/verify_mac.py').read_text(encoding='utf-8')
    findings.append({'id': 'DR03', 'scenario': 'Complete returned Mac payload metadata/license preservation',
        'allThreeMetadataRequired': [r['path'] for r in rows] == names,
        'eachMissingFileRejected': missing_rejected,
        'dittoArchivesCompletePayloadStaticInspection': '["/usr/bin/ditto", "-c", "-k", unpack, package]' in source,
        'nativeDittoExecuted': False})
enum_source = (ROOT / 'DicePrototype/Assets/Game/Core/Session/SessionModels.cs').read_text(encoding='utf-8')
actual_enum = tuple(x.strip() for x in re.search(r'enum CommandKind \{([^}]+)\}', enum_source).group(1).split(','))
findings.append({'id': 'ENUM', 'scenario': 'Debug trace numeric enum mapped to this actual Core source', 'mappingMatchesCurrentSource': actual_enum == COMMAND_KINDS})

after = {str(p.relative_to(ROOT)): digest(p) for p in sources}
output = {'scope': 'Synthetic independent negative probes only; no Windows/Mac/Unity/GitHub execution',
    'beforeSha256': before, 'afterSha256': after, 'sourcesStableDuringRun': before == after, 'findings': findings}
target = OUT / ('delivery-review-probes-result-' + os.environ.get('REVIEW_REVISION', '01') + '.json')
with target.open('x', encoding='utf-8') as f:
    json.dump(output, f, indent=2)
print(json.dumps(output, indent=2))
