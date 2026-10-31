"""Independent offline review; every Player process is mocked, never launched."""
from pathlib import Path
import hashlib, importlib.util, io, json, os, sys, tempfile, unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
sys.dont_write_bytecode = True
sys.path.insert(0, str(ROOT / 'tools'))
source = ROOT / 'evidence/v06/session/display-recovery/test_windows_recovery_runner.py'
spec = importlib.util.spec_from_file_location('recovery_producer_tests', source)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)
runner = mod.runner


class AdditionalIndependentCases(mod.RunnerTests):
    def test_pending_false_cannot_pass_even_if_hash_matches(self):
        mod.write(self.settings, {'pending': False})
        self.ready['pendingSha256'] = runner.sha(self.settings)
        mod.write(self.shared / 'ready-to-terminate.json', self.ready)
        process = mod.FakeProcess()
        with patch.object(runner.subprocess, 'Popen', return_value=process), self.assertRaises(ValueError):
            runner.run_writer(self.args, self.shared, self.raw, mod.IDENTITY)
        self.assertEqual(process.kill_calls, 1)
        self.assertFalse((self.shared / 'reader').exists())

    def test_writer_termination_at_confirmation_limit_is_failure(self):
        process = mod.FakeProcess()
        with patch.object(runner.subprocess, 'Popen', return_value=process), \
                patch.object(runner, 'wait_for_ready', return_value=self.ready), \
                patch.object(runner.time, 'monotonic', side_effect=[100, 115]), self.assertRaises(RuntimeError):
            runner.run_writer(self.args, self.shared, self.raw, mod.IDENTITY)
        self.assertEqual(process.kill_calls, 1)
        self.assertFalse((self.shared / 'reader').exists())

    def test_reader_probe_mvid_mismatch_rejected(self):
        report_dir = self.shared / 'reader/player-synthetic'
        report_dir.mkdir(parents=True)
        mod.write(self.shared / 'recovery-read-receipt.json', {'evidenceDirectory': str(report_dir), 'writerProcessId': 101, 'readerProcessId': 202})
        mod.write(report_dir / 'player-report.json', {'probeRevision': 'p06-test', 'probeAssemblyMvid': 'different'})
        proof = {'environment': {'platform': 'WindowsPlayer', 'processArchitecture': 'X64', 'buildGuid': 'test'}}
        with patch.object(runner, 'validate_recovery', return_value=proof), self.assertRaises(ValueError):
            runner.check_actual_recovery(self.shared, mod.IDENTITY, {'buildGuid': 'test'},
                                         {'probeRevision': 'p06-test', 'probeAssemblyMvid': 'main'}, {'pid': 101}, {'pid': 202})


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


label = sys.argv[1]
target = OUT / (label + '.json')
log = OUT / (label + '.log')
assert not target.exists() and not log.exists()
temp = OUT / 'unit-temp'
temp.mkdir(exist_ok=True)
tempfile.tempdir = str(temp)
files = [source, ROOT / 'tools/verify_v06_display_recovery.py', ROOT / 'tools/package_v06.py', ROOT / 'tools/macos/v06/compare_replays.py']
before = {str(p.relative_to(ROOT)): sha(p) for p in files}
suite = unittest.defaultTestLoader.loadTestsFromTestCase(AdditionalIndependentCases)
stream = io.StringIO()
result = unittest.TextTestRunner(stream=stream, verbosity=2).run(suite)
after = {str(p.relative_to(ROOT)): sha(p) for p in files}
log.write_text(stream.getvalue(), encoding='utf8')
output = {'scope': 'Offline mocked-process tool tests only; no Windows Player started',
          'tests': result.testsRun, 'failures': len(result.failures), 'errors': len(result.errors),
          'sourceBefore': before, 'sourceAfter': after, 'sourceStable': before == after,
          'passed': result.wasSuccessful() and before == after}
target.write_text(json.dumps(output, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
print(json.dumps({k: output[k] for k in ('tests', 'failures', 'errors', 'sourceStable', 'passed')}))
sys.exit(0 if output['passed'] else 1)
