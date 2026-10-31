"""Run on any OS: python -m unittest discover -s tools/macos/v06 -p test_*.py -v.

Synthetic cases test the tooling only. They are never Mac runtime evidence.
"""
import json
from pathlib import Path
import stat
import struct
import tempfile
import unittest
import zipfile

from compare_replays import compare_directories, compare_stream, DEBUG_FIELDS, NORMAL_FIELDS, load_rows
from package_checks import macho_info, safe_archive_inventory
from verify_mac import load_identity


def thin(cpu, filetype=2):
    return struct.pack("<IIIIIIII", 0xFEEDFACF, cpu, 0, filetype, 0, 0, 0, 0)


def fat(bits=32):
    # Two valid, aligned slices. No machine code execution occurs in these tests.
    a, b = thin(0x01000007), thin(0x0100000C)
    row = ">IIIII" if bits == 32 else ">IIQQII"
    records = []
    for cpu, offset in ((0x01000007, 128), (0x0100000C, 256)):
        values = (cpu, 0, offset, 32, 7) + (() if bits == 32 else (0,))
        records.append(struct.pack(row, *values))
    data = struct.pack(">II", 0xCAFEBABE if bits == 32 else 0xCAFEBABF, 2) + b"".join(records)
    return data.ljust(128, b"\0") + a + b"\0" * (256 - 160) + b


def normal_row(index=0):
    return dict(index=index, kind="NewRun" if index == 0 else "StartStage", actionSequence=index + 1, phase="StagePreview",
                submissionScore="0", stageScore="0", target="180", gold=0, stageIndex=0, rollsRemaining=0, remainingSubmissions=3,
                diceRngState=41, shopRngState=42, effectRngState=43, isDebug=False, qualified=False, ruleStateSha256="a" * 64,
                savedStateMatches=True, renderedStateMatches=True, commandId="arbitrary-guid")


def debug_row(index=0):
    return dict(index=index, kind="Continue", actionSequence=index + 1, stageIndex=23, phase="RunWon",
                gold=200, score="115000", risk=False, ruleStateSha256="b" * 64, savedStateMatches=True, renderedStateMatches=True)


IDENTITY = dict(sourceCommit="c" * 40, contentConfigSha256="d" * 64, goldenCaseSetSha256="e" * 64,
                releaseVersion="p0.6.0", contentConfigVersion="p0.6.0", saveSchemaVersion=4, unityEditorVersion="6000.6.7f1", backend="Mono")


def report(mac=False):
    return dict(schema="p0-player-verification-6", probeRevision="p06-player-ui-native-display-9", traceSha256="f" * 64, debugTraceSha256="1" * 64,
                configSha256="d" * 64, configVersion="p0.6.0", buildIdentity=json.dumps(IDENTITY), status="passed", exitCode=0,
                environment=dict(platform="OSXPlayer" if mac else "WindowsPlayer", isEditor=False, processArchitecture="Arm64" if mac else "X64", graphicsApi="Metal" if mac else "Direct3D11"),
                normalReplay=dict(passed=True, normalIsDebug=False, expectedSteps=1, completedSteps=1, terminalPhase="RunFailed"), debug24=dict(passed=True, isDebug=True, steps=[debug_row()]),
                v06Checks=[dict(id=key, status="passed") for key in ("debug-password-isolation", "debug-editor-full-configuration", "controller-drag-and-cancel", "controller-stage-frozen", "display-state-isolation", "debug24-real-player-replay", "remote-target32-ui", "shop-target32-ui", "shop-controller-refresh-ui", "shop-free-first-refresh-service-ui")],
                displayChecks=[dict(id=key, status="passed") for key in ("mode-Windowed", "mode-FullScreenWindow", "mode-MaximizedWindow" if mac else "mode-ExclusiveFullScreen", "15-second-timeout-revert", "escape-revert")],
                stressCompleted=True, handbookPureReadChecks=20, unityErrors=[], checkpointExported=True,
                importedCheckpointStatus="passed" if mac else "not_requested",
                performance=dict(status="within_frame_budget", sampleSeconds=180.01, targetResolutionMet=True,
                                 frameBudgetMet=True, frameCount=10800, frameInterval={"p95": 16.6}, dice=10, submitSlots=8, controllers=10, inventory=5))


class ReplayTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.windows, self.mac = self.root / "windows", self.root / "mac"
        for path, is_mac in ((self.windows, False), (self.mac, True)):
            path.mkdir()
            self.write(path, "player-report.json", report(is_mac))
            self.write(path, "normal-replay.jsonl", normal_row())
            self.write(path, "debug24-replay.jsonl", debug_row())

    def tearDown(self):
        self.temp.cleanup()

    def write(self, path, name, obj):
        (path / name).write_text(json.dumps(obj) + "\n", encoding="utf-8")

    def test_identical_actual_fields_pass_ignoring_command_guid(self):
        value = normal_row()
        value["commandId"] = "different-platform-guid"
        self.write(self.mac, "normal-replay.jsonl", value)
        self.assertTrue(compare_directories(self.windows, self.mac)["passed"])

    def test_rule_hash_difference_fails(self):
        value = normal_row()
        value["ruleStateSha256"] = "0" * 64
        self.write(self.mac, "normal-replay.jsonl", value)
        self.assertFalse(compare_directories(self.windows, self.mac)["passed"])

    def test_empty_report_does_not_pass(self):
        self.write(self.mac, "player-report.json", {})
        self.assertFalse(compare_directories(self.windows, self.mac)["passed"])

    def test_missing_hash_does_not_pass_on_both_sides(self):
        value = normal_row()
        del value["ruleStateSha256"]
        self.assertFalse(compare_stream([value], [value], NORMAL_FIELDS, "normal")["passed"])

    def test_skipped_sequence_fails(self):
        value = normal_row(1)
        value["actionSequence"] = 3
        self.assertFalse(compare_stream([normal_row(), value], [normal_row(), value], NORMAL_FIELDS, "normal")["passed"])

    def test_debug_sequence_starts_at_first_apply_command(self):
        self.assertTrue(compare_stream([debug_row()], [debug_row()], DEBUG_FIELDS, "debug24")["passed"])
        value = debug_row()
        value["actionSequence"] = 2
        self.assertFalse(compare_stream([value], [value], DEBUG_FIELDS, "debug24")["passed"])

    def test_mismatched_count_fails(self):
        self.assertFalse(compare_stream([normal_row()], [normal_row(), normal_row(1)], NORMAL_FIELDS, "normal")["passed"])

    def test_failed_disk_verification_fails(self):
        value = normal_row()
        value["savedStateMatches"] = False
        self.assertFalse(compare_stream([value], [value], NORMAL_FIELDS, "normal")["passed"])

    def test_boolean_cannot_impersonate_integer_sequence(self):
        value = normal_row()
        value["actionSequence"] = True
        self.assertFalse(compare_stream([value], [value], NORMAL_FIELDS, "normal")["passed"])

    def test_bom_records_are_read(self):
        path = self.root / "bom.jsonl"
        path.write_text("\ufeff" + json.dumps(normal_row()) + "\n\ufeff" + json.dumps(normal_row(1)), encoding="utf-8")
        self.assertEqual(2, len(load_rows(path)))

    def test_no_rows_is_failure(self):
        path = self.root / "empty.jsonl"
        path.write_text("\n", encoding="utf-8")
        with self.assertRaises(ValueError):
            load_rows(path)

    def test_report_conditions_fail_individually(self):
        changes = (("status", "running"), ("checkpointExported", False), ("importedCheckpointStatus", "not_requested"),
                   ("unityErrors", ["failure"]), ("handbookPureReadChecks", 19), ("stressCompleted", False),
                   ("traceSha256", "1" * 64), ("buildIdentity", "{}"), ("performance", {}),
                   ("v06Checks", []), ("displayChecks", []), ("debugTraceSha256", "2" * 64))
        for key, value in changes:
            with self.subTest(key=key):
                changed = report(True)
                changed[key] = value
                self.write(self.mac, "player-report.json", changed)
                self.assertFalse(compare_directories(self.windows, self.mac)["passed"])

    def test_rosetta_architecture_rejected(self):
        changed = report(True)
        changed["environment"]["processArchitecture"] = "X64"
        self.write(self.mac, "player-report.json", changed)
        self.assertFalse(compare_directories(self.windows, self.mac)["passed"])

    def test_report_cannot_claim_more_steps(self):
        changed = report(True)
        changed["normalReplay"]["completedSteps"] = 2
        self.write(self.mac, "player-report.json", changed)
        self.assertFalse(compare_directories(self.windows, self.mac)["passed"])

    def test_final_debug_stage_must_be_24(self):
        changed = debug_row()
        changed["stageIndex"] = 22
        for path in (self.windows, self.mac):
            self.write(path, "debug24-replay.jsonl", changed)
        self.assertFalse(compare_directories(self.windows, self.mac)["passed"])

    def test_big_integer_strings_are_exact_and_canonical(self):
        row = normal_row()
        row["stageScore"] = "99999999999999999999999999999999999999"
        self.assertTrue(compare_stream([row], [row], NORMAL_FIELDS, "normal")["passed"])
        for invalid in (999, "01", "-1", "1e40", "", None):
            row["stageScore"] = invalid
            self.assertFalse(compare_stream([row], [row], NORMAL_FIELDS, "normal")["passed"])

    def test_debug_disk_and_render_confirmation_is_required(self):
        row = debug_row()
        row["savedStateMatches"] = False
        self.assertFalse(compare_stream([row], [row], DEBUG_FIELDS, "debug24")["passed"])

    def test_same_old_schema_on_both_sides_still_fails(self):
        for path, mac in ((self.windows, False), (self.mac, True)):
            changed = report(mac)
            changed["schema"] = "p0-player-verification-5"
            self.write(path, "player-report.json", changed)
        self.assertFalse(compare_directories(self.windows, self.mac)["passed"])

    def test_mac_metal_is_required(self):
        changed = report(True)
        changed["environment"]["graphicsApi"] = "OpenGLCore"
        self.write(self.mac, "player-report.json", changed)
        self.assertFalse(compare_directories(self.windows, self.mac)["passed"])

    def test_matching_wrong_save_identity_cannot_pass(self):
        for path, mac in ((self.windows, False), (self.mac, True)):
            changed = report(mac)
            changed["buildIdentity"] = json.dumps(dict(IDENTITY, saveSchemaVersion=3))
            self.write(path, "player-report.json", changed)
        self.assertFalse(compare_directories(self.windows, self.mac)["passed"])

    def test_claimed_performance_pass_cannot_hide_p95_failure(self):
        changed = report(True)
        changed["performance"]["frameInterval"]["p95"] = 20
        self.write(self.mac, "player-report.json", changed)
        self.assertFalse(compare_directories(self.windows, self.mac)["passed"])

    def test_only_mac_exclusive_branch_may_be_not_applicable(self):
        from compare_replays import validate_report
        changed = report(True)
        changed["displayChecks"].append({"id": "exclusive-non-native-escape", "status": "not_applicable"})
        self.assertEqual([], validate_report(changed, "macOS"))
        changed["displayChecks"][-1]["id"] = "unrecognized-check"
        self.assertNotEqual([], validate_report(changed, "macOS"))
        changed = report(False)
        changed["displayChecks"].append({"id": "exclusive-non-native-escape", "status": "not_applicable"})
        self.assertNotEqual([], validate_report(changed, "windows"))

    def test_each_target_and_refresh_regression_is_required(self):
        from compare_replays import validate_report
        for side, mac in (("windows", False), ("macOS", True)):
            for missing in ("remote-target32-ui", "shop-target32-ui", "shop-controller-refresh-ui", "shop-free-first-refresh-service-ui"):
                changed = report(mac)
                changed["v06Checks"] = [row for row in changed["v06Checks"] if row["id"] != missing]
                with self.subTest(side=side, missing=missing):
                    self.assertNotEqual([], validate_report(changed, side))


class PackageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)

    def tearDown(self):
        self.temp.cleanup()

    def binary(self, data):
        path = self.root / "native"
        path.write_bytes(data)
        return path

    def test_mac_launcher_is_lf_without_bom(self):
        data = Path(__file__).with_name("VerifyMac.command").read_bytes()
        self.assertTrue(data.startswith(b"#!/bin/bash\n"))
        self.assertNotIn(b"\r", data)
        self.assertFalse(data.startswith(b"\xef\xbb\xbf"))

    def test_fat_32_and_64_tables(self):
        for bits in (32, 64):
            self.assertEqual({"arm64", "x86_64"}, set(macho_info(self.binary(fat(bits)))["architectures"]))

    def test_thin_architecture_identified(self):
        self.assertEqual(["arm64"], macho_info(self.binary(thin(0x0100000C)))["architectures"])

    def test_non_native_returns_none(self):
        self.assertIsNone(macho_info(self.binary(b"not a native binary")))

    def test_truncated_native_is_rejected(self):
        with self.assertRaises(ValueError):
            macho_info(self.binary(b"\xcf\xfa\xed\xfe"))
        with self.assertRaises(ValueError):
            macho_info(self.binary(fat()[:-1]))

    def test_fat_inner_arch_must_match(self):
        value = bytearray(fat())
        value[260:264] = struct.pack("<I", 0x01000007)
        with self.assertRaises(ValueError):
            macho_info(self.binary(value))

    def test_fat_overlap_rejected(self):
        value = bytearray(fat())
        value[36:40] = struct.pack(">I", 128)
        with self.assertRaises(ValueError):
            macho_info(self.binary(value))

    def archive(self, entries):
        path = self.root / "package.zip"
        with zipfile.ZipFile(path, "w") as archive:
            for name, value, symlink in entries:
                info = zipfile.ZipInfo(name)
                info.filename = name  # Avoid Windows ZipInfo normalizing the intentionally malformed separator.
                if symlink:
                    info.create_system = 3
                    info.external_attr = (stat.S_IFLNK | 0o777) << 16
                archive.writestr(info, value)
        return path

    def test_safe_internal_framework_link_preserved(self):
        path = self.archive([("DicePrototype.app/Versions/A/data", "x", False), ("DicePrototype.app/Versions/Current", "A", True)])
        rows = safe_archive_inventory(path)
        self.assertEqual("symlink", rows[1]["kind"])

    def test_standard_framework_multi_link_target_preserved(self):
        rows = safe_archive_inventory(self.archive([
            ("Framework/Versions/A/Library", "binary", False),
            ("Framework/Versions/Current", "A", True),
            ("Framework/Library", "Versions/Current/Library", True)]))
        self.assertEqual(2, sum(row["kind"] == "symlink" for row in rows))

    def test_nested_link_ancestor_escape_rejected_before_extraction(self):
        # Independent review reproduction: POSIX a -> '.' makes a/b/c's
        # physical parent b, so ../../outside escapes despite lexical depth.
        entries = [("a", ".", True), ("a/b/", "", False),
                   ("a/b/c", "../../outside", True), ("a/b/c/file.txt", "payload", False)]
        with self.assertRaises(ValueError):
            safe_archive_inventory(self.archive(entries))

    def test_isolated_nested_link_is_rejected_without_descendant_file(self):
        with self.assertRaises(ValueError):
            safe_archive_inventory(self.archive([("a", ".", True), ("a/b/c", "../../outside", True)]))

    def test_case_mixed_link_ancestor_rejected(self):
        for member in ("a/b/c", "A/B/c/file.txt"):
            with self.subTest(member=member), self.assertRaises(ValueError):
                safe_archive_inventory(self.archive([("A", ".", True), (member, "payload", False)]))

    def test_link_target_resolves_alias_before_dotdot(self):
        # Neither member has a symlink ancestor; checking only member paths
        # still misses this isolated link target escape.
        with self.assertRaises(ValueError):
            safe_archive_inventory(self.archive([("A", ".", True), ("elsewhere", "a/../outside", True)]))

    def test_case_mixed_link_cycle_rejected(self):
        with self.assertRaises(ValueError):
            safe_archive_inventory(self.archive([("A", "b", True), ("B", "a", True)]))

    def test_unicode_normalized_ancestor_rejected(self):
        with self.assertRaises(ValueError):
            safe_archive_inventory(self.archive([("caf\u00e9", ".", True), ("cafe\u0301/b/c", "payload", False)]))

    def test_archive_traversal_and_absolute_paths_rejected(self):
        for bad in ("../evil", "/evil", "C:/evil", "a\\evil", "a/./evil", "a//evil"):
            with self.subTest(path=bad), self.assertRaises(ValueError):
                safe_archive_inventory(self.archive([(bad, "x", False)]))

    def test_external_link_rejected(self):
        with self.assertRaises(ValueError):
            safe_archive_inventory(self.archive([("app/out", "../../outside", True)]))

    def test_cycle_rejected(self):
        with self.assertRaises(ValueError):
            safe_archive_inventory(self.archive([("app/a", "b", True), ("app/b", "a", True)]))

    def test_case_collisions_rejected(self):
        with self.assertRaises(ValueError):
            safe_archive_inventory(self.archive([("app/Foo", "x", False), ("app/foo", "y", False)]))

    def test_empty_archive_rejected(self):
        with self.assertRaises(ValueError):
            safe_archive_inventory(self.archive([]))

    def test_identity_requires_full_real_shapes(self):
        path = self.root / "identity.json"
        path.write_text(json.dumps(IDENTITY), encoding="utf-8")
        self.assertEqual(IDENTITY, load_identity(path))
        changed = dict(IDENTITY, sourceCommit="pending")
        path.write_text(json.dumps(changed), encoding="utf-8")
        with self.assertRaises(ValueError):
            load_identity(path)


if __name__ == "__main__":
    unittest.main()
