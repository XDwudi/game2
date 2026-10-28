"""Synthetic fixtures exercise the checker; these are not game or engine tests."""

from __future__ import annotations

import contextlib
import copy
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


SCRIPT = Path(__file__).resolve().parents[1] / "tools" / "quality_check.py"
spec = importlib.util.spec_from_file_location("quality_check", SCRIPT)
qc = importlib.util.module_from_spec(spec)
spec.loader.exec_module(qc)

NUNIT3 = '''<test-run result="Passed" testcasecount="2" total="2" passed="2" failed="0" inconclusive="0" skipped="0">
<test-suite result="Passed" total="2" passed="2" failed="0" inconclusive="0" skipped="0">
<test-case fullname="Game.Move" result="Passed"/>
<test-case fullname="Game.Save" result="Passed"/>
</test-suite></test-run>'''

NUNIT2 = '''<test-results total="2" errors="0" failures="0" not-run="0" inconclusive="0" ignored="0" skipped="0" invalid="0">
<test-suite name="Game" result="Success" executed="True" success="True"><results>
<test-case name="Game.Move" result="Success" executed="True" success="True"/>
<test-case name="Game.Save" result="Success" executed="True" success="True"/>
</results></test-suite></test-results>'''


class QualityCheckTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()

    def put(self, name, content):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")
        return path

    def entry(self, name, content="sample"):
        path = self.put(name, content)
        return {"path": name, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}

    def manifest(self, data):
        self.put("manifest.json", json.dumps(data))
        return "manifest.json"

    def snapshot(self):
        return {"schema_version": 1, "inputs": [self.entry("spec.txt")],
                "artifacts": [self.entry("evidence.txt")]}

    def source_manifest(self):
        files = [self.entry("vendor/LICENSE", "MIT license fixture"), self.entry("vendor/template.md")]
        for item in files:
            item["upstream_path"] = item["path"].split("/")[-1]
        return {"schema_version": 1, "sources": [{"id": "fixture", "url": "https://example.com/repo",
                "commit": "a" * 40, "license_path": "vendor/LICENSE", "files": files}]}

    def run_nunit(self, content, minimum=2, required=None):
        if isinstance(content, bytes):
            (self.root / "results.xml").write_bytes(content)
        else:
            self.put("results.xml", content)
        return qc.check_nunit(self.root, "results.xml", minimum, required or [])

    def test_two_valid_formats(self):
        for content in (NUNIT2, NUNIT3):
            with self.subTest(content=content[:25]):
                self.assertEqual(self.run_nunit(content, required=["Game.Save"])["checked_cases"], 2)

    def test_nunit3_filter_may_have_larger_discovery_count(self):
        self.assertEqual(self.run_nunit(NUNIT3.replace('testcasecount="2"', 'testcasecount="7"'))["checked_cases"], 2)

    def test_utf16_reports_and_legal_metadata_are_supported(self):
        metadata3 = NUNIT3.replace('<test-suite result=',
            '<command-line>runner --test</command-line><filter><and><test>Game.Move</test><cat>unit</cat></and></filter><test-suite result=', 1)
        metadata3 = metadata3.replace('<test-case fullname="Game.Move" result="Passed"/>',
            '<environment os-version="test"/><settings><setting name="workers" value="1"/></settings>'
            '<test-case fullname="Game.Move" result="Passed"><properties><property name="kind" value="unit"/></properties>'
            '<output><![CDATA[example <test-case> is text]]></output><assertions><assertion result="Passed">'
            '<message>ok</message></assertion></assertions><attachments><attachment><filePath>C:/evidence.png</filePath>'
            '<description>fixture</description></attachment></attachments></test-case>')
        metadata2 = NUNIT2.replace('<test-suite name=',
            '<environment os-version="test"/><culture-info current-culture="en-US"/><test-suite name=', 1)
        metadata2 = metadata2.replace('<results>', '<categories><category name="unit"/></categories><results>', 1)
        for content in (metadata2, metadata3):
            for encoding in ("utf-8", "utf-16"):
                with self.subTest(version=content[:20], encoding=encoding):
                    raw = (f'<?xml version="1.0" encoding="{encoding}"?>' + content).encode(encoding)
                    self.assertEqual(self.run_nunit(raw)["checked_cases"], 2)

    def test_dtd_and_internal_entity_rejected_after_decoding(self):
        for encoding in ("utf-8", "utf-16", "utf-16-le", "utf-16-be"):
            for declaration in ('<!DOCTYPE test-run>', '<!DOCTYPE test-run [<!ENTITY testname "Game.Move">]>'):
                with self.subTest(encoding=encoding, declaration=declaration):
                    content = NUNIT3.replace("Game.Move", "&testname;") if "ENTITY" in declaration else NUNIT3
                    declared = "UTF-16" if encoding.startswith("utf-16") else "UTF-8"
                    raw = (f'<?xml version="1.0" encoding="{declared}"?>' + declaration + content).encode(encoding)
                    with self.assertRaisesRegex(qc.CheckError, "DTD"):
                        self.run_nunit(raw)

    def test_namespaced_failed_case_and_failure_are_not_ignored(self):
        variants = [NUNIT3.replace('</test-suite>', '<x:test-case xmlns:x="urn:unexpected" fullname="Game.Bad" result="Failed"/></test-suite>'),
                    NUNIT3.replace('</test-suite>', '<x:failure xmlns:x="urn:unexpected">failed</x:failure></test-suite>'),
                    NUNIT3.replace('fullname="Game.Save"', 'xmlns:x="urn:unexpected" x:result="Failed" fullname="Game.Save"')]
        for content in variants:
            with self.subTest(content=content):
                with self.assertRaises(qc.CheckError):
                    self.run_nunit(content)

    def test_cases_in_metadata_or_illegal_parents_are_rejected(self):
        variants = [NUNIT3.replace('<test-case fullname="Game.Move" result="Passed"/>',
                                  '<environment><test-case fullname="Game.Move" result="Passed"/></environment>'),
                    NUNIT3.replace('<test-case fullname="Game.Move" result="Passed"/>',
                                  '<output><test-case fullname="Game.Move" result="Passed"/></output>'),
                    NUNIT3.replace('<test-suite result="Passed" total="2" passed="2" failed="0" inconclusive="0" skipped="0">', '').replace('</test-suite>', ''),
                    NUNIT2.replace('<results>', '').replace('</results>', ''),
                    NUNIT2.replace('<test-case name="Game.Move" result="Success" executed="True" success="True"/>',
                                   '<properties><test-case name="Game.Move" result="Success" executed="True" success="True"/></properties>')]
        for content in variants:
            with self.subTest(content=content):
                with self.assertRaises(qc.CheckError):
                    self.run_nunit(content)

    def test_unknown_result_structures_and_contradictory_flags_fail(self):
        variants = [NUNIT3.replace('</test-run>', '<unexpected result="Failed" total="1" failed="1"/></test-run>'),
                    NUNIT3.replace('</test-suite>', '<environment result="Failed"/></test-suite>'),
                    NUNIT3.replace('<test-run ', '<test-run executed="False" ', 1),
                    NUNIT3.replace('</test-suite>', '<assertions><assertion result="Failed"><message>bad</message></assertion></assertions></test-suite>')]
        for content in variants:
            with self.subTest(content=content):
                with self.assertRaises(qc.CheckError):
                    self.run_nunit(content)

    def test_xml_regression_failures_return_nonzero_cli(self):
        variants = [(f'<?xml version="1.0" encoding="UTF-16"?><!DOCTYPE test-run [<!ENTITY testname "Game.Move">]>'
                     + NUNIT3.replace("Game.Move", "&testname;")).encode("utf-16"),
                    NUNIT3.replace('</test-suite>', '<x:failure xmlns:x="urn:unexpected"/></test-suite>').encode(),
                    NUNIT3.replace('<test-case fullname="Game.Move" result="Passed"/>',
                                   '<environment><test-case fullname="Game.Move" result="Passed"/></environment>').encode()]
        for raw in variants:
            (self.root / "results.xml").write_bytes(raw)
            output = io.StringIO()
            with contextlib.redirect_stdout(output):
                code = qc.main(["--root", str(self.root), "nunit", "results.xml", "--min-tests", "2"])
            with self.subTest(raw=raw[:60]):
                self.assertEqual(code, 1)
                self.assertEqual(json.loads(output.getvalue())["status"], "CHECKS_FAILED")

    def test_nunit_failing_skipped_unknown_and_unexecuted_cases(self):
        variants = [NUNIT3.replace('fullname="Game.Save" result="Passed"', f'fullname="Game.Save" result="{state}"')
                    for state in ("Failed", "Skipped", "Inconclusive", "Unknown", "Warning")]
        variants += [NUNIT2.replace('name="Game.Save" result="Success"', f'name="Game.Save" result="{state}"')
                     for state in ("Failure", "Error", "Ignored", "Skipped", "Inconclusive")]
        variants += [NUNIT2.replace('executed="True"', 'executed="False"', 1),
                     NUNIT2.replace('success="True"', 'success="False"', 1)]
        for content in variants:
            with self.subTest(content=content):
                with self.assertRaises(qc.CheckError):
                    self.run_nunit(content)

    def test_nunit_counters_must_match_actual_cases(self):
        variants = [NUNIT3.replace('failed="0"', 'failed="1"', 1),
                    NUNIT3.replace('skipped="0"', 'skipped="1"', 1),
                    NUNIT3.replace('passed="2"', 'passed="1"', 1),
                    NUNIT3.replace('total="2"', 'total="3"', 1),
                    NUNIT3.replace('testcasecount="2"', 'testcasecount="1"'),
                    NUNIT2.replace('errors="0"', 'errors="1"'),
                    NUNIT2.replace('total="2"', 'total="1"'),
                    NUNIT2.replace('not-run="0"', 'not-run="1"'),
                    NUNIT3.replace('failed="0"', 'failed="-1"', 1),
                    NUNIT3.replace('passed="2"', '', 1)]
        for content in variants:
            with self.subTest(content=content):
                with self.assertRaises(qc.CheckError):
                    self.run_nunit(content)

    def test_nunit_missing_duplicate_and_suite_failure(self):
        with self.assertRaises(qc.CheckError):
            self.run_nunit(NUNIT3, required=["Game.Missing"])
        with self.assertRaises(qc.CheckError):
            self.run_nunit(NUNIT3.replace("Game.Save", "Game.Move"))
        with self.assertRaises(qc.CheckError):
            self.run_nunit(NUNIT3.replace('<test-suite result="Passed"', '<test-suite result="Failed"'))
        with self.assertRaises(qc.CheckError):
            self.run_nunit(NUNIT3.replace('</test-suite>', '<failure><message>teardown</message></failure></test-suite>'))

    def test_nunit_zero_empty_malformed_and_missing(self):
        zero = '<test-run result="Passed" testcasecount="0" total="0" passed="0" failed="0" inconclusive="0" skipped="0"/>'
        for content in (zero, "", "<test-run", "<testsuite/>", '<!DOCTYPE a><test-run/>'):
            with self.subTest(content=content):
                with self.assertRaises((qc.CheckError, qc.ET.ParseError)):
                    self.run_nunit(content, minimum=1)
        with self.assertRaises(qc.CheckError):
            qc.check_nunit(self.root, "missing.xml", 1, [])
        with self.assertRaises(qc.CheckError):
            self.run_nunit(NUNIT3, minimum=0)

    def test_valid_snapshot_and_sources(self):
        self.assertEqual(qc.check_snapshot(self.root, self.manifest(self.snapshot()))["checked_files"], 2)
        self.assertEqual(qc.check_sources(self.root, self.manifest(self.source_manifest()))["checked_files"], 2)

    def test_stale_snapshot_and_tampered_source(self):
        data = self.snapshot()
        manifest = self.manifest(data)
        self.put("spec.txt", "changed")
        with self.assertRaisesRegex(qc.CheckError, "mismatch"):
            qc.check_snapshot(self.root, manifest)
        data = self.source_manifest()
        manifest = self.manifest(data)
        self.put("vendor/template.md", "tampered")
        with self.assertRaisesRegex(qc.CheckError, "mismatch"):
            qc.check_sources(self.root, manifest)

    def test_snapshot_missing_empty_duplicate_and_bad_hash(self):
        data = self.snapshot()
        variants = []
        for section in ("inputs", "artifacts"):
            changed = copy.deepcopy(data)
            changed[section] = []
            variants.append(changed)
        for value in ("", "TBD", "0" * 63):
            changed = copy.deepcopy(data)
            changed["artifacts"][0]["sha256"] = value
            variants.append(changed)
        changed = copy.deepcopy(data)
        changed["artifacts"] = copy.deepcopy(changed["inputs"])
        variants.append(changed)
        changed = copy.deepcopy(data)
        changed["artifacts"][0]["path"] = "missing.txt"
        variants.append(changed)
        for variant in variants:
            with self.subTest(variant=variant):
                with self.assertRaises(qc.CheckError):
                    qc.check_snapshot(self.root, self.manifest(variant))
        data["artifacts"] = [self.entry("empty.txt", "")]
        with self.assertRaisesRegex(qc.CheckError, "empty"):
            qc.check_snapshot(self.root, self.manifest(data))

    def test_paths_cannot_escape_or_use_alias_segments(self):
        for name in ("../outside.txt", "/outside.txt", "C:/outside.txt", "C:\\outside.txt", "./spec.txt", "a//b.txt"):
            data = self.snapshot()
            data["inputs"][0]["path"] = name
            with self.subTest(name=name):
                with self.assertRaises(qc.CheckError):
                    qc.check_snapshot(self.root, self.manifest(data))

    def test_sources_require_pinned_commit_and_hashed_license(self):
        data = self.source_manifest()
        variants = []
        for field, value in (("commit", "main"), ("license_path", "missing.txt"), ("files", [])):
            changed = copy.deepcopy(data)
            changed["sources"][0][field] = value
            variants.append(changed)
        changed = copy.deepcopy(data)
        changed["sources"][0]["files"] = changed["sources"][0]["files"][1:]
        variants.append(changed)
        changed = copy.deepcopy(data)
        changed["sources"].append(copy.deepcopy(changed["sources"][0]))
        variants.append(changed)
        for variant in variants:
            with self.subTest(variant=variant):
                with self.assertRaises(qc.CheckError):
                    qc.check_sources(self.root, self.manifest(variant))

    def test_schema_and_duplicate_json_keys_rejected(self):
        for content in ('{"schema_version":1,"schema_version":1}', '{"schema_version":true}',
                        '{"schema_version":1,"inputs":NaN}', '[]', '{'):
            self.put("manifest.json", content)
            with self.subTest(content=content):
                with self.assertRaises((qc.CheckError, json.JSONDecodeError)):
                    qc.read_manifest(self.root, "manifest.json")

    def test_main_failure_is_structured_and_nonzero(self):
        output = io.StringIO()
        with contextlib.redirect_stdout(output):
            code = qc.main(["--root", str(self.root), "nunit", "missing.xml", "--min-tests", "1"])
        result = json.loads(output.getvalue())
        self.assertEqual(code, 1)
        self.assertEqual(result["status"], "CHECKS_FAILED")
        self.assertTrue(result["integrity_only"])

    def test_cli_success_and_failure_exit_codes(self):
        self.put("results.xml", NUNIT3)
        base = [sys.executable, str(SCRIPT), "--root", str(self.root), "nunit", "results.xml", "--min-tests", "2"]
        good = subprocess.run(base, capture_output=True, text=True, check=False)
        self.assertEqual(good.returncode, 0, good.stderr)
        self.assertEqual(json.loads(good.stdout)["status"], "CHECKS_PASSED")
        bad = subprocess.run(base + ["--require-test", "Game.Missing"], capture_output=True, text=True, check=False)
        self.assertEqual(bad.returncode, 1, bad.stderr)


if __name__ == "__main__":
    unittest.main()
