import hashlib
import json
import os
import struct
import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path
from unittest.mock import patch

from tools import package_v11 as pack
from tools import v11_build_report as native
from tools import publish_v11_release as publish
from tools import replay_v11 as replay
from PIL import Image


def write(root, name, data):
    path = root / name
    path.parent.mkdir(parents=True, exist_ok=True)
    if isinstance(data, dict):
        data = json.dumps(data).encode()
    path.write_bytes(data)
    return path


def fixture_fonts(root):
    m = pack.delivery.read_json(pack.ROOT / pack.MANIFEST)
    packed = set()
    for f in [*m["fonts"], *m["ifIncludedInBuild"]]:
        font = write(root, f["fontFile"], f["name"].encode())
        f["fontSha256"] = pack.delivery.sha(font)
        gu = hashlib.md5(f["fontFile"].encode()).hexdigest()
        write(root, f["fontFile"] + ".meta", ("guid: " + gu + "\n").encode())
        ofl = write(root, f["licenseFile"], b"OFL\r\n")
        f["licenseSha256"] = pack.delivery.sha(ofl)
    for theme in pack.THEMES:
        path = f"Assets/Resources/Themes/{theme}/Fonts/title.asset"
        guid = hashlib.md5(path.encode()).hexdigest()
        write(root, "DicePrototype/" + path + ".meta", ("guid: " + guid + "\n").encode())
        packed.add((path, guid))
    write(root, pack.MANIFEST, m)
    return m, packed


def fixture_gate(root, commit="a" * 40):
    identity = dict(sourceCommit=commit, releaseVersion=pack.VERSION, contentConfigVersion=pack.VERSION,
                    saveSchemaVersion=7, artVersion="art1.1.0", backend="Mono", unityEditorVersion="6000.6.7f1")
    tests, files = [], []
    for mode, resolution in [("EditMode", None), ("PlayMode", "1920x1080"), ("PlayMode", "1280x720")]:
        count = pack.delivery.HISTORICAL_MINIMUM[mode]
        attrs = {"testcasecount": str(count), "total": str(count), "passed": str(count), "failed": "0",
                 "skipped": "0", "inconclusive": "0", "result": "Passed"}
        xml = ET.Element("test-run", attrs)
        suite = ET.SubElement(xml, "test-suite", attrs)
        ET.SubElement(ET.SubElement(suite, "properties"), "property", name="platform", value=mode)
        names = [f"Fixture.{mode}{i}" for i in range(count)]
        if resolution:
            names[-1] = pack.delivery.RESOLUTION_CASE
        for name in names:
            case = ET.SubElement(suite, "test-case", fullname=name, result="Passed")
            if name == pack.delivery.RESOLUTION_CASE:
                ET.SubElement(case, "output").text = f"Three fonts measured at {resolution} only. The other resolution remains a separate required run."
        filename = f"evidence/tests/{resolution or 'edit'}.xml"
        xmlpath = write(root, filename, ET.tostring(xml))
        report = {"path": filename, "sha256": pack.delivery.sha(xmlpath)}
        binding_name = filename.replace(".xml", ".binding.json")
        bindpath = write(root, binding_name, {"report": report})
        binding = {"path": binding_name, "sha256": pack.delivery.sha(bindpath)}
        tests.append({"mode": mode, "resolution": resolution, "names": names, "passed": count, "report": report, "binding": binding})
        files += [report, binding]
    files.append({"path": pack.MANIFEST, "sha256": pack.delivery.sha(root / pack.MANIFEST)})
    gate = {"schema": "v11-build-gate-1", "identity": identity, "sourceCommit": identity["sourceCommit"],
            "expectedCounts": pack.delivery.HISTORICAL_MINIMUM, "editModePassed": 797, "playModePassed": 30,
            "files": files, "tests": tests}
    write(root, pack.GATE, gate)
    return gate


def universal():
    result = bytearray(112)
    struct.pack_into(">II", result, 0, 0xcafebabe, 2)
    for i, cpu in enumerate([0x01000007, 0x0100000c]):
        offset = 48 + 32 * i
        struct.pack_into(">IIIII", result, 8 + 20 * i, cpu, 3, offset, 32, 4)
        struct.pack_into("<IIII", result, offset, 0xfeedfacf, cpu, 3, 2)
    return bytes(result)


def build(root, platform, packed, identity):
    rel, target, native_target, profile, _ = pack.TARGET[platform]
    output = root / "Builds/v11" / rel
    if platform == "macos":
        files = [write(output, "Contents/MacOS/DiceCraft P0", universal()),
                 write(output, "Contents/Resources/Data/data.unity3d", b"test data")]
    else:
        exe = bytearray(70)
        exe[:2] = b"MZ"
        struct.pack_into("<I", exe, 60, 64)
        exe[64:] = b"PE\0\0\x64\x86"
        files = [write(output.parent, output.name, exe), write(output.parent, "DicePrototype_Data/data.unity3d", b"test data")]
    guid = "a" * 32
    report = {"schema": "v11-player-build-1", "buildGuid": guid, "result": "Succeeded", "platform": target,
              "profile": f"Assets/Settings/BuildProfiles/{profile}.asset", "errors": 0, "output": str(output),
              "bytes": sum(p.stat().st_size for p in files), "identity": json.dumps(identity), "unity": "6000.6.7f1",
              "gateSha256": pack.delivery.sha(root / pack.GATE),
              "playerFiles": [{"path": p.relative_to(output.parent).as_posix(), "sha256": pack.delivery.sha(p)} for p in files]}
    data = {"buildGUID": guid, "buildResult": "1", "totalErrors": "0", "outputPath": str(output),
            "totalSize": str(report["bytes"]), "buildProfilePath": report["profile"], "platformName": native_target,
            "assets": packed, "files": {str(p): p.stat().st_size for p in files}, "packedObjects": 4, "packedGroups": 1}
    native_file = "evidence/v11/unity/test-" + platform + ".buildreport"
    path = write(root, native_file, b"test input for the mocked binary2text boundary")
    report["nativeBuildReport"] = {"path": native_file, "sha256": pack.delivery.sha(path)}
    return write(root, native_file + ".json", report), data, files


class Package11Tests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name).resolve()
        self.manifest, self.assets = fixture_fonts(self.root)

    def test_font_licenses_have_exact_ofl_bytes_and_separate_copyright(self):
        fonts, _, _ = pack.check_font_assets(self.root, self.assets, {})
        licenses = pack.license_bundle(self.root, fonts)
        self.assertEqual(pack.REQUIRED_LICENSES | {"README.txt"}, set(licenses))
        for font in fonts:
            self.assertTrue(licenses[font["packageFile"]].endswith(b"OFL\r\n"))
            self.assertIn(font["copyright"].encode(), licenses[font["packageFile"]])
            self.assertIn(font["source"].encode(), licenses["README.txt"])
        write(self.root, fonts[0]["licenseFile"], b"OFL\n")
        with self.assertRaisesRegex(ValueError, "OFL source"):
            pack.license_bundle(self.root, fonts)

    def test_detects_liberation_if_referenced_or_loose_and_title_even_when_renamed(self):
        lib = self.manifest["ifIncludedInBuild"][0]
        for asset, loose in [({("Assets/Moved/LiberationSans SDF.asset", "1" * 32)}, {}), (set(), {"resources/renamed": lib["fontSha256"]})]:
            fonts, _, _ = pack.check_font_assets(self.root, self.assets | asset, loose)
            self.assertIn("LiberationSans-OFL.txt", pack.license_bundle(self.root, fonts))
        source = next(f for f in self.manifest["fonts"] if "/Themes/glass/" in f["fontFile"])
        guid = hashlib.md5(source["fontFile"].encode()).hexdigest()
        for asset in [("Assets/renamed.file", guid), ("Assets/renamed/" + Path(source["fontFile"]).name, "0" * 32)]:
            with self.assertRaisesRegex(ValueError, "Packed title font source"):
                pack.check_font_assets(self.root, self.assets | {asset}, {})
        with self.assertRaisesRegex(ValueError, "omits static"):
            pack.check_font_assets(self.root, set(), {})

    def test_windows_and_mac_archive_readback_and_reproducibility(self):
        gate = fixture_gate(self.root)
        write(self.root, pack.README, b"player guide")
        for platform in pack.TARGET:
            with self.subTest(platform=platform):
                report, data, files = build(self.root, platform, self.assets, gate["identity"])
                with patch.object(pack, "read_native", return_value=data), patch.object(pack.subprocess, "run"):
                    verified = pack.package(self.root, platform, report, "mock")
                    path = self.root / "Releases" / pack.VERSION / verified["package"]
                    with zipfile.ZipFile(path) as archive:
                        self.assertEqual(verified["members"], len(archive.namelist()))
                        for name in pack.REQUIRED_LICENSES | {"README.txt"}:
                            notice = archive.read(f"{pack.TOP}/Licenses/{name}")
                            if platform == "macos":
                                self.assertEqual(notice, archive.read(f"{pack.TOP}/DicePrototype.app/Contents/Resources/Licenses/{name}"))
                        if platform == "macos":
                            info = archive.getinfo(f"{pack.TOP}/DicePrototype.app/Contents/MacOS/DiceCraft P0")
                            self.assertEqual(0o100755, info.external_attr >> 16)
                    receipt = self.root / "evidence/v11/packages" / (path.name + ".json")
                    self.assertEqual(verified, pack.package(self.root, platform, report, "mock"))
                    # Crash after archive publication: reproduce bytes, recover only the missing receipt.
                    receipt.unlink()
                    os.utime(files[-1], (1000000000, 1000000000))
                    repeat = pack.package(self.root, platform, report, "mock")
                    self.assertEqual(verified["sha256"], repeat["sha256"])
                    self.assertEqual(verified, pack.delivery.read_json(receipt))
                    path.write_bytes(b"unrelated archive")
                    with self.assertRaisesRegex(FileExistsError, "Existing player archive differs"):
                        pack.package(self.root, platform, report, "mock")
                    self.assertEqual(b"unrelated archive", path.read_bytes())

    def test_reject_same_resolution_twice_and_modified_nunit(self):
        gate = fixture_gate(self.root)
        report, data, files = build(self.root, "windows", self.assets, gate["identity"])
        proof = pack.delivery.read_json(report)
        gate["tests"][2]["resolution"] = "1920x1080"
        with self.assertRaisesRegex(ValueError, "both PlayMode"):
            pack.verify_gate(self.root, gate, proof)
        gate["tests"][2]["resolution"] = "1280x720"
        gate["tests"][2]["report"] = gate["tests"][1]["report"]
        with self.assertRaisesRegex(ValueError, "resolution differs"):
            pack.verify_gate(self.root, gate, proof)
        write(self.root, gate["tests"][0]["report"]["path"], b"stale")
        with self.assertRaisesRegex(ValueError, "Stale test"):
            pack.verify_gate(self.root, gate, proof)

    def test_build_identity_inventory_architecture_and_same_size_replacement(self):
        gate = fixture_gate(self.root)
        write(self.root, pack.README, b"player guide")
        path, data, files = build(self.root, "windows", self.assets, gate["identity"])
        proof = pack.delivery.read_json(path)
        with self.assertRaisesRegex(ValueError, "Native BuildReport"):
            pack.build_inputs(self.root, "windows", proof, dict(data, buildGUID="b" * 32))
        stray = write(files[0].parent, "stale.data", b"stray")
        with self.assertRaisesRegex(ValueError, "inventory"):
            pack.build_inputs(self.root, "windows", proof, data)
        stray.unlink()
        files[-1].write_bytes(b"bad bytes")
        with patch.object(pack, "read_native", return_value=data), patch.object(pack.subprocess, "run"):
            with self.assertRaisesRegex(ValueError, "build-time hash"):
                pack.package(self.root, "windows", path, "mock")
        self.assertFalse(list((self.root / "Releases").rglob("*.zip")))
        files[0].write_bytes(b"MZ" + b"\0" * 68)
        with self.assertRaisesRegex(ValueError, "not x64"):
            pack.build_inputs(self.root, "windows", proof, data)

    def test_incomplete_native_serialization_refused(self):
        header = ("ID: 1 (ClassID: 1125) BuildReport\n\tm_Summary  (BuildSummary)\n"
                  + "\n".join("\t\t" + item for item in [
                      "buildGUID " + "a" * 32 + " (GUID)", 'platformName "Win64" (string)',
                      'buildProfilePath "profile" (string)', 'outputPath "player.exe" (string)',
                      "totalSize 20 (UInt64)", "totalErrors 0 (int)", "buildResult 1 (int)"])
                  + '\n\tm_Files  (vector)\n\t\tsize 1 (int)\n\t\tdata  (BuildReportFile)\n'
                  + '\t\t\tpath "player.exe" (string)\n\t\t\ttotalSize 20 (UInt64)\n')
        link = "\t\tdata  (PPtr<Object>)\n\t\t\tm_FileID 0 (int)\n\t\t\tm_PathID 2 (SInt64)\n"
        group = ('ID: 2 (ClassID: 1126) PackedAssets\n\tm_Contents  (vector)\n\t\tsize 1 (int)\n'
                 '\t\tdata  (BuildReportPackedAssetInfo)\n\t\t\tbuildTimeAssetPath "Assets/title.asset" (string)\n'
                 '\t\t\tsourceAssetGUID ' + "b" * 32 + ' (GUID)\n')
        vector = "\tm_Appendices  (vector)\n\t\tsize "
        complete = header + vector + "1 (int)\n" + link + group
        measured = native.parse(complete)
        self.assertEqual({("Assets/title.asset", "b" * 32)}, measured["assets"])
        self.assertEqual({"player.exe": 20}, measured["files"])
        with self.assertRaisesRegex(ValueError, "Duplicate packed"):
            native.parse(header + vector + "2 (int)\n" + link + link + group)
        with self.assertRaisesRegex(ValueError, "Incomplete native"):
            native.parse(complete + group.replace("ID: 2 ", "ID: 3 "))
        with self.assertRaisesRegex(ValueError, "one native"):
            native.parse("result: Succeeded")
        with self.assertRaisesRegex(ValueError, "Incomplete"):
            native.rows("\tm_Files  (vector)\n\t\tsize 2 (int)\n\t\tdata  (BuildReportFile)\n", "m_Files", "BuildReportFile")


class Publication11Tests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name).resolve()
        _, assets = fixture_fonts(self.root)
        write(self.root, pack.README, b"fixture README")
        self.notes = "docs/fixture.md"
        write(self.root, self.notes, b"Fixture release notes")
        self.git("init", "-q", "-b", "codex/v11-fixture")
        self.git("remote", "add", "origin", publish.previous.REMOTE)
        self.git("add", "DicePrototype", "docs")
        self.git("commit", "--quiet", "-m", "Synthetic fixture only")
        commit = publish.git(self.root, "rev-parse", "HEAD")
        gate = fixture_gate(self.root, commit)
        for platform in pack.TARGET:
            report, data, _ = build(self.root, platform, assets, gate["identity"])
            with patch.object(pack, "read_native", return_value=data):
                pack.package(self.root, platform, report, "mock")
        self.windows = pack.delivery.read_json(self.root / "evidence/v11/packages" / (replay.WINDOWS + ".json"))
        write(self.root, "evidence/v11/review/final.md", b"Independent W25 review fixture")
        self.replay_dir = self.root / "evidence/v11/replays/final"
        trace_path = write(self.root, "evidence/v11/replays/trace.json",
                           {"configVersion": pack.VERSION, "seed": 5, "terminalPhase": "RunFailed",
                            "steps": [{"kind": "NewRun", "seed": 5}, {"kind": "Submit"}]})
        trace = replay.validated_trace(trace_path)
        data_path = (self.root / "fresh/DicePrototype_Data").as_posix()
        runs = []
        for theme in replay.THEMES:
            sub = self.replay_dir / theme
            write(sub, "Player.log", b"Unity player fixture")
            images = ["00-menu.png", "01-stage-preview.png", "02-final-RunFailed.png"]
            for name in images:
                Image.new("RGB", (32, 24), "red").save(sub / name)
            write(sub, "tour.json", {"schema": "v11-windows-replay-1", "theme": theme, "images": images, "status": "completed",
                                     "error": "", "buildGuid": self.windows["buildGuid"], "buildIdentity": json.dumps(gate["identity"]),
                                     "unityVersion": "6000.6.7f1", "artVersion": "art1.1.0", "rulesVersion": pack.VERSION,
                                     "isEditor": False, "platform": "WindowsPlayer", "traceSha256": pack.delivery.sha(trace_path),
                                     "playerDataPath": data_path, "terminalPhase": "RunFailed", "stepsSent": 1, "traceSteps": 1,
                                     "logErrors": 0, "finalStage": 1, "screenWidth": 32, "screenHeight": 24})
            runs.append(replay.check_run(self.root, theme, sub, self.windows, trace_path, trace, data_path))
        write(self.replay_dir, "index.json", {"schema": "v11-replay-index-1", "windowsPackageSha256": self.windows["sha256"],
                                             "sourceCommit": commit, "buildGuid": self.windows["buildGuid"],
                                             "playerDataPath": data_path, "verifiedExtractedFiles": self.windows["members"],
                                             "trace": replay.ref(self.root, trace_path), "runs": runs})

    def git(self, *args):
        subprocess.run(["git", "-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid",
                        "-c", "core.autocrlf=false", "-c", "commit.gpgsign=false", *args],
                       cwd=self.root, check=True, capture_output=True)

    def test_offline_dry_run_fingerprint_following_packages_and_notes(self):
        from contextlib import redirect_stdout
        import io
        with (patch.object(publish, "ROOT", self.root), patch.object(publish.previous, "token", side_effect=AssertionError("credentials")),
              patch.object(publish, "remote_refs", side_effect=AssertionError("network")),
              patch.object(publish, "GitHub", side_effect=AssertionError("network")),
              patch.object(sys, "argv", ["publish", "--notes", self.notes]), redirect_stdout(io.StringIO()) as output):
            publish.main()
        plan = json.loads(output.getvalue())
        self.assertTrue(plan["dryRun"])
        self.assertEqual(2, len(plan["assets"]))
        before = plan["planSha256"]
        write(self.root, self.notes, b"New fixture release wording")
        after, _ = publish.plan_release(self.root, self.notes)
        self.assertNotEqual(before, publish.plan_digest(after))
        tampered = next((self.root / "Releases" / pack.VERSION).glob("*.zip"))
        tampered.write_bytes(b"other bytes")
        with self.assertRaisesRegex(ValueError, "Package does not match"):
            publish.plan_release(self.root, self.notes)

    def test_frozen_sources_and_native_receipt_bound(self):
        extra = write(self.root, "DicePrototype/Assets/changed.txt", b"changed")
        with self.assertRaisesRegex(ValueError, "local changes"):
            publish.plan_release(self.root, self.notes)
        self.git("add", extra.relative_to(self.root).as_posix())
        self.git("commit", "--quiet", "-m", "Changed fixture source")
        with self.assertRaisesRegex(ValueError, "between the frozen"):
            publish.plan_release(self.root, self.notes)
        self.git("revert", "--no-edit", "HEAD")
        raw = next((self.root / "evidence/v11/unity").glob("*.buildreport"))
        raw.write_bytes(b"changed evidence")
        with self.assertRaisesRegex(ValueError, "evidence has changed"):
            publish.plan_release(self.root, self.notes)

    def test_draft_retry_reuses_verified_assets_before_publishing(self):
        from unittest.mock import Mock
        plan, notes = publish.plan_release(self.root, self.notes)
        first, second = plan["assets"]
        asset = lambda a, i: {"name": a["name"], "size": a["bytes"], "id": i, "state": "uploaded"}
        one, two = asset(first, 1), asset(second, 2)
        release = {"id": 9, "tag_name": publish.TAG, "target_commitish": plan["target"], "name": publish.TITLE,
                   "body": notes, "prerelease": True, "draft": True, "assets": [one], "html_url": "fixture"}
        ready = dict(release, assets=[one, two])
        final = dict(ready, draft=False)
        server = Mock()
        server.find_release.return_value = release
        server.upload.return_value = two
        server.check_download.side_effect = lambda info, expected: expected["sha256"]
        server.json.side_effect = [ready, final, final]
        with patch.object(publish, "remote_refs", side_effect=[None, plan["target"]]):
            result = publish.execute(self.root, plan, notes, server)
        self.assertEqual(result["target"], plan["target"])
        self.assertEqual(server.upload.call_count, 1)
        self.assertEqual(server.check_download.call_count, 2)
        self.assertEqual(server.json.call_args_list[1].args[1], "PATCH")
        server.reset_mock()
        server.check_download.side_effect = ValueError("corrupt remote download")
        with patch.object(publish, "remote_refs", return_value=None), self.assertRaisesRegex(ValueError, "corrupt"):
            publish.execute(self.root, plan, notes, server)
        server.json.assert_not_called()

    def test_replay_requires_actual_theme_identity_exception_free_log_and_images(self):
        sub = self.replay_dir / "glass"
        raw = sub / "tour.json"
        initial = pack.delivery.read_json(raw)
        for attr, bad in [("theme", "default"), ("isEditor", True), ("buildGuid", "x"), ("stepsSent", 0), ("logErrors", 1)]:
            with self.subTest(attr=attr):
                write(sub, raw.name, dict(initial, **{attr: bad}))
                with self.assertRaisesRegex(ValueError, "actual theme"):
                    replay.verify_index(self.root, self.replay_dir / "index.json", self.windows)
        write(sub, raw.name, initial)
        write(sub, "Player.log", b"NullReferenceException: player callback failed")
        with self.assertRaisesRegex(ValueError, "Unity exception"):
            replay.verify_index(self.root, self.replay_dir / "index.json", self.windows)
        write(sub, "Player.log", b"Unity player fixture")
        Image.new("RGB", (32, 24), "blue").save(sub / "00-menu.png")
        with self.assertRaisesRegex(ValueError, "Changed Windows run"):
            replay.verify_index(self.root, self.replay_dir / "index.json", self.windows)

    def test_unpack_checks_payload_and_requires_fresh_members(self):
        fresh = self.root / "r05-unpacked"
        fresh.mkdir()
        zip_path = self.root / "Releases" / pack.VERSION / replay.WINDOWS
        exe = replay.extract_verified(zip_path, self.windows, fresh)
        self.assertEqual("DicePrototype.exe", exe.name)
        with self.assertRaises(FileExistsError):
            replay.extract_verified(zip_path, self.windows, fresh)
        with self.assertRaisesRegex(ValueError, "ZIP differs"):
            replay.extract_verified(zip_path, dict(self.windows, sha256="0" * 64), fresh)


if __name__ == "__main__":
    unittest.main()
