"""Version adapter regressions; synthetic tooling tests, never engine/device evidence."""
import copy
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import sys
import tempfile
import types
import unittest
from unittest.mock import Mock, patch

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
sys.path.insert(0, str(ROOT / "tools/macos/v07"))
import package_v07 as previous
import package_v071 as packaging
import v071_delivery as delivery
import run_v071_windows_player as windows_runner
import verify_v071_display_recovery as recovery_runner
from test_mac_tools import IDENTITY, report
from macos.v071.compare_replays import validate_report


def current(value):
    # Only synthetic fixture version labels change; no real report is rewritten.
    return json.loads(json.dumps(value).replace("p0.7.0", "p0.7.1").replace("art0.7.0", "art0.7.1"))


class VersionAdapterTests(unittest.TestCase):
    def test_new_identity_passes_and_old_identity_is_rejected(self):
        self.assertEqual(current(IDENTITY), packaging.validate_identity(current(IDENTITY)))
        with self.assertRaises(ValueError): packaging.validate_identity(IDENTITY)
        self.assertEqual(IDENTITY, previous.validate_identity(IDENTITY))
        with self.assertRaises(ValueError): previous.validate_identity(current(IDENTITY))

    def test_version_change_keeps_rules_schema_hashes_and_engine_strict(self):
        for field, value in (("contentConfigVersion", "p0.7.1"), ("saveSchemaVersion", True),
                             ("saveSchemaVersion", 5), ("unityEditorVersion", "6000.6.8f1"),
                             ("backend", "IL2CPP"), ("sourceCommit", "stale")):
            with self.subTest(field=field, value=value):
                with self.assertRaises(ValueError): packaging.validate_identity(dict(current(IDENTITY), **{field: value}))

    def test_package_destinations_cannot_overwrite_v07(self):
        self.assertEqual("evidence/v071/unity/build-gate.json", packaging.GATE)
        self.assertEqual("Releases/p0.7.1", packaging.RELEASE)
        self.assertEqual("p0.7.1-rc.1", packaging.TAG)
        self.assertNotEqual(previous.GATE, packaging.GATE)
        self.assertNotEqual(previous.RELEASE, packaging.RELEASE)

    def test_new_reports_keep_original_probe_schema_and_checks(self):
        for side, mac in (("windows", False), ("macOS", True)):
            self.assertEqual([], validate_report(current(report(mac)), side))
            self.assertTrue(validate_report(report(mac), side))
            changed = current(report(mac)); changed["uiChecks"].pop()
            self.assertTrue(validate_report(changed, side))

    def test_current_version_does_not_bypass_performance_or_source_binding(self):
        for mutate in (lambda r: r["performance"].update(sampleSeconds=179.9),
                       lambda r: r["performance"]["frameInterval"].update(p95=16.7001),
                       lambda r: r["environment"].update(artVersion="art0.7.0")):
            value = current(report()); mutate(value)
            self.assertTrue(validate_report(value, "windows"))

    def test_discovery_keeps_exact_inventory(self):
        names = ["Edit.Test" + str(i) for i in range(1013)]
        inventory = dict(mode="EditMode", unity="6000.6.7f1", names=names)
        self.assertEqual(names, delivery.validate_discovery(inventory, "EditMode", len(names)))
        with self.assertRaises(ValueError): delivery.validate_discovery(inventory, "EditMode", 797)
        inventory["names"] = names[:-1] + [names[0]]
        with self.assertRaises(ValueError): delivery.validate_discovery(inventory, "EditMode", 1013)


class WindowsVersionAdapterTests(unittest.TestCase):
    def test_default_plan_does_not_launch_or_write(self):
        argv = ["run_v071_windows_player.py", "--build-report", "not-yet-built.json",
                "--expected-build-guid", "not-yet-built", "--evidence-root", "evidence/v071/not-created",
                "--raw-root", "artifacts/cache/v071/not-created"]
        with patch.object(sys, "argv", argv), patch.object(windows_runner, "launch") as launch, \
             patch.object(windows_runner, "write_new") as write, contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(0, windows_runner.main())
        launch.assert_not_called(); write.assert_not_called()

    def test_output_guard_rejects_old_version_root_and_existing_directory(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            for guard in (windows_runner.new_child, recovery_runner.new_path):
                with self.assertRaises(ValueError): guard(root / "evidence/v07/player", root / "evidence/v071")
                with self.assertRaises(ValueError): guard(root / "evidence/v071", root / "evidence/v071")
                existing = root / "evidence/v071/existing"; existing.mkdir(parents=True, exist_ok=True)
                with self.assertRaises((ValueError, FileExistsError)): guard(existing, existing.parent)
                self.assertEqual(existing.parent / "new", guard(existing.parent / "new", existing.parent))

    def test_recovery_ready_receipt_requires_current_art_version(self):
        with tempfile.TemporaryDirectory() as temp:
            shared = Path(temp)
            receipt = dict(schema="p07-native-display-recovery-1", artVersion="art0.7.1",
                           probeRevision="p07-player-art-ui-21", status="awaiting_external_process_termination",
                           writerProcessId=123, settingsPath=str(shared / "display-settings.json"))
            process = Mock(pid=123); process.poll.return_value = None
            path = shared / "ready-to-terminate.json"
            path.write_text(json.dumps(receipt), encoding="utf-8")
            self.assertEqual(receipt, recovery_runner.wait_for_ready(process, shared, 30))
            receipt["artVersion"] = "art0.7.0"
            path.write_text(json.dumps(receipt), encoding="utf-8")
            with self.assertRaises(ValueError): recovery_runner.wait_for_ready(process, shared, 30)


def load_tests(loader, tests, pattern):
    # Reuse the existing independent gate cases without copying a second test suite.
    # Only import target and fixture discovery directory are adapted. Assertions and
    # corruption cases remain byte-for-byte the original test source.
    path = ROOT / "tools/macos/v07/test_delivery_tools.py"
    source = path.read_text(encoding="utf-8").replace("import package_v07 as packaging", "import package_v071 as packaging")
    source = source.replace("evidence/v07/unity", "evidence/v071/unity")
    module = types.ModuleType("v071_reused_gate_tests"); module.__file__ = str(path)
    exec(compile(source, str(path), "exec"), module.__dict__)
    module.IDENTITY = current(IDENTITY)
    tests.addTests(loader.loadTestsFromTestCase(module.EvidenceGateTests))

    # Reuse the existing three-scenario serial runner contract, not a copied suite.
    # Its launches/recovery are mocks. Version adaptation affects synthetic fixtures
    # and module/path targets only; all native/performance-failure assertions stay.
    path = ROOT / "tools/macos/v07/test_strict_failed_reference.py"
    source = path.read_text(encoding="utf-8")
    for old, new in (("import package_v07 as packaging", "import package_v071 as packaging"),
                     ("import verify_v07_display_recovery as recovery", "import verify_v071_display_recovery as recovery"),
                     ("import run_v07_windows_player as runner", "import run_v071_windows_player as runner"),
                     ("from compare_replays import", "from macos.v071.compare_replays import"),
                     ("Builds/v07/Windows", "Builds/v071/Windows"),
                     ('self.root/"evidence/v07"/scenario', 'self.root/"evidence/v071"/scenario'),
                     ("artifacts/cache/v07", "artifacts/cache/v071")):
        source = source.replace(old, new)
    serial = types.ModuleType("v071_reused_serial_tests"); serial.__file__ = str(path)
    exec(compile(source, str(path), "exec"), serial.__dict__)
    serial.IDENTITY = current(IDENTITY)
    base_report, base_diagnostic = serial.report, serial.diagnostic
    serial.report = lambda *a, **k: current(base_report(*a, **k))
    serial.diagnostic = lambda *a, **k: current(base_diagnostic(*a, **k))
    base_setup = serial.SerialSelectionContracts.setUp
    def current_setup(self):
        base_setup(self)
        self.report = current(self.report)
    serial.SerialSelectionContracts.setUp = current_setup
    tests.addTests(loader.loadTestsFromTestCase(serial.SerialSelectionContracts))
    return tests


if __name__ == "__main__":
    unittest.main()
