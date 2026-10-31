"""Independent offline negative checks: partial/native-diagnostic results cannot become release acceptance."""
from pathlib import Path
import copy, datetime, hashlib, json, sys
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / 'tools/macos/v06'))
from compare_replays import validate_report
from test_mac_tools import report

paths = [ROOT / 'tools/macos/v06' / p for p in ('compare_replays.py', 'test_mac_tools.py')]
def snapshot(): return {p.relative_to(ROOT).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in paths}
before = snapshot()
cases = []
for side, mac in (('windows', False), ('macOS', True)):
    original = report(mac)
    assert not validate_report(original, side)
    cases.append({'side': side, 'case': 'synthetic_complete_control', 'expectedAccepted': True, 'failures': []})
    for status in ('partial_passed_native_display_pending', 'display_passed', 'diagnostic_completed'):
        changed = copy.deepcopy(original)
        changed['status'] = status
        failures = validate_report(changed, side)
        assert any(f['reason'] == 'player_did_not_pass' for f in failures)
        cases.append({'side': side, 'case': status, 'expectedAccepted': False, 'failures': failures})
    changed = copy.deepcopy(original)
    # Even mislabelling the completion status cannot hide absent native checks.
    changed['nativeDisplayDeferred'] = True
    changed['displayChecks'] = [{'id': 'native-display-verification-deferred', 'status': 'not_executed'}]
    changed['v06Checks'] = [x for x in changed['v06Checks'] if x['id'] != 'display-state-isolation']
    failures = validate_report(changed, side)
    assert {f['reason'] for f in failures} == {'native_display_sections_missing_or_failed', 'v06_ui_sections_missing_or_failed'}
    cases.append({'side': side, 'case': 'partial_mislabelled_passed_missing_native', 'expectedAccepted': False, 'failures': failures})
    for missing in ('shop-controller-refresh-ui', 'shop-free-first-refresh-service-ui'):
        changed = copy.deepcopy(original)
        changed['v06Checks'] = [x for x in changed['v06Checks'] if x['id'] != missing]
        failures = validate_report(changed, side)
        assert any(f['reason'] == 'v06_ui_sections_missing_or_failed' for f in failures)
        cases.append({'side': side, 'case': 'missing_' + missing, 'expectedAccepted': False, 'failures': failures})
after = snapshot()
assert before == after
output = OUT / 'partial-gate-review01.json'
assert not output.exists()
output.write_text(json.dumps({'scope': __doc__, 'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'status': 'passed', 'cases': cases, 'before': before, 'after': after, 'actualPlayerExecuted': False}, indent=2) + '\n', encoding='utf8')
print(f'{len(cases)} independent offline checks passed; actual Player not executed.')
