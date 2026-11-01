"""Synthetic v0.7 version/discovery regressions; no engine/device run is implied."""
import copy
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
import package_v07 as packaging
import v07_delivery as delivery
from compare_replays import validate_report, validate_windows_review_reference, validate_windows_failed_performance_reference, validate_recovery
from test_mac_tools import IDENTITY, report
from test_review_candidate import partial
from test_diagnostic_candidate import diagnostic
import test_delivery_tools as fixtures
from test_delivery_tools import write_json
from verify_mac import load_identity

V07_CHECKS = {"v07-menu-settings-navigation", "v07-shop-five-tabs-fixed-shelf", "v07-dice-sheet-cross-and-purity"}


class ArtVersionTests(unittest.TestCase):
    def test_release_art_rules_and_save_are_distinct_required_identity_fields(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "identity.json"
            write_json(path, IDENTITY)
            self.assertEqual(IDENTITY, packaging.validate_identity(IDENTITY))
            self.assertEqual(IDENTITY, load_identity(path))
            for field, value in (("releaseVersion", "p0.6.0"), ("artVersion", None), ("artVersion", "art0.6.0"),
                                 ("contentConfigVersion", "p0.7.0"), ("saveSchemaVersion", 7)):
                with self.subTest(field=field, value=value):
                    changed = dict(IDENTITY, **{field: value})
                    write_json(path, changed)
                    with self.assertRaises(ValueError): packaging.validate_identity(changed)
                    with self.assertRaises(ValueError): load_identity(path)

    def test_old_report_schema_and_probe_rejected_even_with_current_art_field(self):
        for key, value in (("schema", "p0-player-verification-6"), ("probeRevision", "p06-player-ui-native-display-9")):
            changed = report(); changed[key] = value
            self.assertTrue(validate_report(changed, "windows"))

    def test_matching_wrong_art_and_rules_never_pass(self):
        for field, value in (("artVersion", None), ("artVersion", "art0.6.0"), ("rulesVersion", "p0.7.0")):
            for side, mac in (("windows", False), ("macOS", True)):
                changed = report(mac); changed[field] = value
                changed["environment"][field] = value
                for row in changed["uiChecks"]: row[field] = value
                self.assertTrue(validate_report(changed, side))

    def test_embedded_identity_cannot_silently_change_art_only(self):
        changed = report(); changed["buildIdentity"] = json.dumps(dict(IDENTITY, artVersion="art0.8.0"))
        self.assertTrue(validate_report(changed, "windows"))

    def test_all_three_actual_art_ui_sections_required_in_every_reference_scope(self):
        for factory, validator in ((report, lambda r: validate_report(r, "windows")),
                                   (partial, validate_windows_review_reference), (diagnostic, validate_windows_failed_performance_reference)):
            for missing in V07_CHECKS:
                changed = factory(); changed["uiChecks"] = [row for row in changed["uiChecks"] if row["id"] != missing]
                self.assertTrue(validator(changed), (factory.__name__, missing))

    def test_version_binding_failures_cannot_be_filtered_out_by_review_switch(self):
        for factory, validator in ((report, lambda r: validate_report(r, "windows")),
                                   (partial, validate_windows_review_reference), (diagnostic, validate_windows_failed_performance_reference)):
            for section in ("environment", "uiChecks", "displayChecks"):
                changed = factory(); target = changed[section] if section == "environment" else changed[section][0]
                target["artVersion"] = "art0.6.0"
                self.assertTrue(validator(changed), (factory.__name__, section))

    def test_current_art_cannot_hide_short_nan_or_failed_performance(self):
        for seconds in (179.99, float("nan"), float("inf"), True, None):
            changed = report(True); changed["performance"]["sampleSeconds"] = seconds
            self.assertTrue(validate_report(changed, "macOS"), seconds)
        for p95 in (16.7001, float("nan"), float("inf"), True, 0):
            changed = report(True); changed["performance"]["frameInterval"]["p95"] = p95
            self.assertTrue(validate_report(changed, "macOS"), p95)

    def test_current_art_still_requires_full_native_modes(self):
        for side, mac, removed in (("windows", False, "mode-ExclusiveFullScreen"), ("macOS", True, "mode-MaximizedWindow")):
            changed = report(mac); changed["displayChecks"] = [row for row in changed["displayChecks"] if row["id"] != removed]
            self.assertTrue(validate_report(changed, side))


class OriginalResolutionCoverageTests(unittest.TestCase):
    def test_new_original_spec_sizes_cannot_be_missing_failed_or_unexecuted(self):
        for side, mac in (("windows", False), ("macOS", True)):
            for size in ("1366x768", "2560x1600", "3440x1440"):
                for state in ("missing", "failed", "not_executed"):
                    value = report(mac)
                    if state == "missing": value["displayChecks"] = [x for x in value["displayChecks"] if x["id"] != "window-" + size]
                    else: next(x for x in value["displayChecks"] if x["id"] == "window-" + size)["status"] = state
                    self.assertTrue(validate_report(value, side), (side, size, state))

    def test_supported_window_requires_actual_exact_size_and_mode(self):
        for field, wrong in (("width", 2560), ("height", 1440), ("mode", "FullScreenWindow")):
            value = report()
            next(x for x in value["displayChecks"] if x["id"] == "window-1366x768")["actualNativeReadback"][field] = wrong
            self.assertTrue(validate_report(value, "windows"))

    def test_actual_unsupported_window_needs_reason_and_never_claims_pass(self):
        value = report(); row = next(x for x in value["displayChecks"] if x["id"] == "window-3440x1440")
        row.update(status="not_supported", reason="Requested client dimensions exceed current display work area.")
        row["actualNativeReadback"] = {"width":1920,"height":1080,"mode":"Windowed"}
        self.assertEqual([], validate_report(value, "windows"))
        self.assertEqual("not_supported", row["status"])
        for reason in (None, "", "  "):
            row["reason"] = reason; self.assertTrue(validate_report(value, "windows"))

    def test_partial_reference_stays_partial_and_cannot_claim_native_sizes(self):
        value = partial(); self.assertEqual([], validate_windows_review_reference(value))
        self.assertTrue(validate_report(value, "windows"))
        value["displayChecks"].append({"id":"window-1366x768", "status":"passed", "artVersion":"art0.7.0", "probeRevision":value["probeRevision"]})
        self.assertTrue(validate_windows_review_reference(value))


class DiscoveryConfigurationTests(unittest.TestCase):
    def inventory(self, mode, count):
        return {"mode": mode, "unity": "6000.6.7f1", "names": [mode + ".Synthetic" + str(i) for i in range(count)]}

    def test_counts_are_explicit_and_can_grow_without_editing_tool(self):
        for mode, floor in delivery.HISTORICAL_MINIMUM.items():
            count = floor + 17  # Synthetic increase, not a real v0.7 discovery count.
            value = self.inventory(mode, count)
            self.assertEqual(value["names"], delivery.validate_discovery(value, mode, count))
            for wrong in (count - 1, count + 1, None, True, 0):
                with self.assertRaises(ValueError): delivery.validate_discovery(value, mode, wrong)

    def test_historical_floor_is_regression_guard_not_current_expected_count(self):
        for mode, floor in delivery.HISTORICAL_MINIMUM.items():
            with self.assertRaises(ValueError): delivery.validate_discovery(self.inventory(mode, floor - 1), mode, floor - 1)
            self.assertEqual(floor, len(delivery.validate_discovery(self.inventory(mode, floor), mode, floor)))

    def test_duplicates_empty_or_wrong_engine_discovery_cannot_pass(self):
        mode = "EditMode"; count = delivery.HISTORICAL_MINIMUM[mode]
        for mutate in (lambda r: r["names"].__setitem__(-1, r["names"][0]),
                       lambda r: r["names"].__setitem__(-1, ""), lambda r: r.update(unity="6000.6.0f1"),
                       lambda r: r.update(mode="PlayMode")):
            value = self.inventory(mode, count); mutate(value)
            with self.assertRaises(ValueError): delivery.validate_discovery(value, mode, count)

    def test_cli_cannot_create_gate_without_explicit_counts(self):
        with patch.object(sys, "argv", ["v07_delivery.py", "gate", "--edit", "synthetic.xml", "--play", "synthetic.xml", "--pdb", "synthetic.json"]), \
                patch.object(delivery, "gate") as gate, patch("sys.stderr"):
            with self.assertRaises(SystemExit): delivery.main()
            gate.assert_not_called()


class FrozenCountTests(unittest.TestCase):
    setUp = fixtures.EvidenceGateTests.setUp
    tearDown = fixtures.EvidenceGateTests.tearDown
    save_gate = fixtures.EvidenceGateTests.save_gate

    def test_packaging_rejects_unbound_or_changed_explicit_discovery_counts(self):
        for expected in ({}, {"EditMode": 7, "PlayMode": 4}, {"EditMode": True, "PlayMode": 3}):
            self.gate["expectedCounts"] = expected; self.save_gate()
            with self.assertRaises(ValueError): packaging.validated_gate()


class RecoveryVersionTests(unittest.TestCase):
    setUp = fixtures.RecoveryEvidenceTests.setUp
    tearDown = fixtures.RecoveryEvidenceTests.tearDown

    def test_writer_reader_and_actual_player_version_must_agree(self):
        files = {"ready-to-terminate.json": self.ready, "recovery-read-receipt.json": self.receipt,
                 str(self.report_dir / "player-report.json"): self.report}
        for name, original in files.items():
            for field, value in (("artVersion", "art0.6.0"), ("probeRevision", "p06-old-probe")):
                changed = copy.deepcopy(original); changed[field] = value
                write_json(self.root / name, changed)
                with self.subTest(file=name, field=field), self.assertRaises(ValueError): validate_recovery(self.root, IDENTITY)
                write_json(self.root / name, original)


if __name__ == "__main__":
    unittest.main()
