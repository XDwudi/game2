"""Create a local p1.1.0 player ZIP from a gated Unity build and its native .buildreport.

python tools/package_v11.py --platform windows --build-report evidence/v11/unity/build-windows-....json
Use --binary2text to select the installed Unity 6000.6.7f1 binary2text on another host.
"""
import argparse
import hashlib
import io
import json
import os
import re
import shutil
import struct
import subprocess
import tempfile
import zipfile
from pathlib import Path

if __package__:
    from . import v11_delivery as delivery
    from .macos.v071.package_checks import macho_info
    from .package_v10 import SKIP
    from .v11_build_report import read_native
else:
    import v11_delivery as delivery
    from macos.v071.package_checks import macho_info
    from package_v10 import SKIP
    from v11_build_report import read_native

ROOT = delivery.ROOT
VERSION = "p1.1.0"
TOP = "DicePrototype-" + VERSION
MANIFEST = "DicePrototype/Assets/Game/Fonts/fonts_manifest.json"
GATE = "evidence/v11/unity/build-gate.json"
README = "docs/PLAYER_README_V11.txt"
THEMES = ("comic", "deco", "glass", "sancai")
PLAYER_THEMES = ("default", "swiss", "sancai", "comic", "deco", "glass")
BINARY2TEXT = shutil.which("binary2text") or "E:/unity/6000.6.7f1/Editor/Data/Tools/binary2text.exe"
REQUIRED_LICENSES = {"NotoSansCJKsc-OFL.txt", "NotoSansSC-Bold-OFL.txt", "MaShanZheng-OFL.txt",
                     "ZCOOLKuaiLe-OFL.txt", "ZCOOLXiaoWei-OFL.txt", "NotoSerifSC-OFL.txt"}
TARGET = {"windows": ("Windows/DicePrototype.exe", "StandaloneWindows64", "Win64", "Windows_x64", "win-x64"),
          "macos": ("macOS/DicePrototype.app", "StandaloneOSX", "OSXUniversal", "macOS_Universal", "macos-universal")}


# Frozen FW-09 comparison, verified against both native Unity reports, archives and release readbacks.
V10_SIZE_BASELINE = {
    "release": "p1.0.0-rc.1",
    "sourceCommit": "c7d93792189e5bad6a34435e2fa5a231cf7f3641",
    "sourcePath": "DicePrototype",
    "sourceTree": "6bde351b51119019eae213a81391e75d61e80477",
    "remoteReadback": {"path": "evidence/v10/delivery/github-release-p1.0.0-rc.1.json",
     "sha256": "23b0b3e59497c6753f763bb40184cf7a6ce804e2b5fa2b122f3ba104c2b6f474"},
    "platforms": {
        "windows": {
            "buildGuid": "c1ece476a2f54dab8cd922b175438544",
            "nativeBytes": 538658274,
            "zipBytes": 362557580,
            "nativeReport": {"path": "DicePrototype/Library/BuildHistory/20261115-203756Z-b0136681/b01366817ab7aef4db08ac8e96da07a2.buildreport",
             "sha256": "74c99dc7184c13e741d001cd8e44e43ef9368b4a46928ba81fa95e5f313fc57d"},
            "buildSummary": {"path": "evidence/v10/unity/build-windows-20261115-203823.json",
             "sha256": "1b2c57358f9a10037f75def50356ad84f8e9a9bfe0fc3915aa72387ebe59beea"},
            "packageReceipt": {"path": "evidence/v10/packages/DicePrototype-p1.0.0-win-x64.zip.json",
             "sha256": "9123284a9f0bb8eb92bdb5ea703a0cd5a298c3f246ca3c3a494035b790320f00"},
            "archive": {"path": "Releases/p1.0.0/DicePrototype-p1.0.0-win-x64.zip",
             "sha256": "69e783f8895243a14f10412b7a47d236f9a283489f670f334311af5b77cb6471"},
        },
        "macos": {
            "buildGuid": "400351e873564c47a359e1f5010debcf",
            "nativeBytes": 670607783,
            "zipBytes": 402910309,
            "nativeReport": {"path": "DicePrototype/Library/BuildHistory/20261115-203852Z-8721491f/8721491f23ab84b4bb1a6191cb501e65.buildreport",
             "sha256": "807a180b6857d064d335bc80604467c816d794239395b8aaf620b2fc3fd85eaa"},
            "buildSummary": {"path": "evidence/v10/unity/build-macos-20261115-203918.json",
             "sha256": "2c23c311455fe6f7caee79b5aad3e273d581c87c8119419e1eb226b868ff1e4b"},
            "packageReceipt": {"path": "evidence/v10/packages/DicePrototype-p1.0.0-macos-universal.zip.json",
             "sha256": "4d107a94939c60bde2edde328e61195f958832f47329b9d269d549a279f8de2a"},
            "archive": {"path": "Releases/p1.0.0/DicePrototype-p1.0.0-macos-universal.zip",
             "sha256": "413878c3dc8267941e3a61dec87bfc56a2e7a4d6016a49f66e54132f171af6af"},
        },
    },
}


def digest(data):
    return hashlib.sha256(data).hexdigest()


def contained(root, relative):
    path = (root / relative).resolve()
    if not path.is_relative_to(root.resolve()) or Path(relative).is_absolute():
        raise ValueError("Expected repository-relative path: " + str(relative))
    return path


def asset_guid(path):
    guid = re.search(r"^guid: ([0-9a-f]{32})$", Path(str(path) + ".meta").read_text(encoding="utf-8-sig"), re.M)
    if not guid:
        raise ValueError("Missing Unity asset GUID: " + str(path))
    return guid[1]


def required_art_inventory(root, packed):
    """F-THEME-02. Match each frozen source PNG and its meta GUID to one native packed row."""
    verified = {}
    seen_guids = set()
    for theme in PLAYER_THEMES:
        loot = "Assets/Art/Art11/Loot" if theme == "default" else f"Assets/Resources/Themes/{theme}/Art"
        paths = [f"Assets/Resources/MenuArt17/{theme}.png",
                 *(f"{loot}/illustration_L{n}.png" for n in range(1, 7))]
        for path in paths:
            source = root / "DicePrototype" / path
            if not source.is_file() or not Path(str(source) + ".meta").is_file():
                raise ValueError("Missing release art source or meta: " + path)
            guid = asset_guid(source)
            if guid in seen_guids or guid == "0" * 32 or (path, guid) not in packed:
                raise ValueError("Missing or ambiguous native art path and GUID: " + path)
            seen_guids.add(guid)
            verified[path] = guid
    return verified


def check_font_assets(root, packed, loose):
    manifest = delivery.read_json(root / MANIFEST)
    fonts, extras = manifest["fonts"], manifest["ifIncludedInBuild"]
    if (manifest["schema"] != 1 or len(fonts) != len(REQUIRED_LICENSES)
            or {f["packageFile"] for f in fonts} != REQUIRED_LICENSES
            or [f["packageFile"] for f in extras] != ["LiberationSans-OFL.txt"]):
        raise ValueError("Unexpected W21 font manifest inventory")
    paths = {p.casefold() for p, _ in packed}
    guids = {guid for _, guid in packed}
    prohibited_names, prohibited_hashes = set(), set()

    def asset_info(path):
        guid = asset_guid(path)
        asset_path = path.relative_to(root / "DicePrototype").as_posix().casefold()
        return asset_path in paths or guid in guids

    for theme in THEMES:
        title = root / f"DicePrototype/Assets/Resources/Themes/{theme}/Fonts/title.asset"
        if not asset_info(title):
            raise ValueError("Packed inventory omits static title atlas: " + theme)
        matches = [f for f in fonts if f"/Fonts/Themes/{theme}/" in f["fontFile"]]
        if len(matches) != 1:
            raise ValueError("Missing title font manifest record: " + theme)
        source = contained(root, matches[0]["fontFile"])
        basename = source.name.casefold()
        if asset_info(source) or any(p.rsplit("/", 1)[-1] == basename for p in paths):
            raise ValueError("Packed title font source: " + source.name)
        prohibited_names.add(basename)
        prohibited_hashes.add(matches[0]["fontSha256"])
    lib_file = contained(root, extras[0]["fontFile"])
    liberation = asset_info(lib_file) or any(
        "liberationsans" in p.rsplit("/", 1)[-1].replace(" ", "") and Path(p).suffix in {".asset", ".ttf", ".mat"}
        for p in paths)
    liberation |= any(Path(p).name.casefold() == lib_file.name.casefold() or h == extras[0]["fontSha256"]
                      for p, h in loose.items())
    # A source can move in the editor while retaining its original GUID.
    if not liberation:
        liberation = any(asset_info(path) for path in lib_file.parent.rglob("LiberationSans*")
                         if path.suffix in {".asset", ".mat"})
    return fonts + (extras if liberation else []), prohibited_names, prohibited_hashes


def fw09_size_report(root, platform, native, zip_bytes):
    baseline = V10_SIZE_BASELINE["platforms"][platform]
    titles = {}
    for theme in THEMES:
        asset = f"Assets/Resources/Themes/{theme}/Fonts/title.asset"
        guid = asset_guid(root / "DicePrototype" / asset)
        size = native["packedBytesByAsset"].get((asset, guid))
        if size is None:
            raise ValueError("No native packed data for title atlas path and GUID: " + theme)
        titles[theme] = {"asset": asset, "guid": guid, "packedObjectBytes": size}
    native_bytes = int(native["totalSize"])
    return {
        "titleAssets": titles,
        "baselineRelease": V10_SIZE_BASELINE["release"],
        "baselineSourceCommit": V10_SIZE_BASELINE["sourceCommit"],
        "baselineSourcePath": V10_SIZE_BASELINE["sourcePath"],
        "baselineSourceTree": V10_SIZE_BASELINE["sourceTree"],
        "baselineRemoteReadback": V10_SIZE_BASELINE["remoteReadback"],
        "platformBaseline": baseline,
        "currentNativeBytes": native_bytes, "wholeNativeBuildDeltaBytes": native_bytes - baseline["nativeBytes"],
        "currentZipBytes": zip_bytes, "wholeZipDeltaBytes": zip_bytes - baseline["zipBytes"],
    }


def license_bundle(root, fonts):
    result = {}
    lines = ["本文件夹收录本游戏所用字体的版权声明与许可证（SIL Open Font License 1.1）。", ""]
    for f in fonts:
        body = contained(root, f["licenseFile"]).read_bytes()
        if (digest(body) != f["licenseSha256"] or f["license"] != "SIL Open Font License 1.1"
                or delivery.sha(contained(root, f["fontFile"])) != f["fontSha256"]):
            raise ValueError("Font or OFL source differs from W21 manifest: " + f["name"])
        copyright_text = f["copyright"].strip()
        if not copyright_text:
            raise ValueError("Missing font copyright: " + f["name"])
        header = copyright_text.encode("utf-8")
        result[f["packageFile"]] = body if header in body else header + b"\n\n" + body
        notice = " ".join(copyright_text.split())
        lines.append(f"{f['name']} {f['version']}：{notice}；{f['license']}；来源 {f['source']}；用于 {f['usage']}。")
    result["README.txt"] = ("\n".join(lines) + "\n").encode("utf-8")
    return result


def verify_gate(root, gate, report):
    identity = json.loads(report["identity"])
    if (gate.get("schema") != "v11-build-gate-1" or identity != gate["identity"]
            or identity["sourceCommit"] != gate["sourceCommit"] or identity["releaseVersion"] != VERSION
            or identity["contentConfigVersion"] != VERSION or identity["saveSchemaVersion"] != 7
            or identity["artVersion"] != "art1.1.0" or identity["backend"] != "Mono"
            or identity["unityEditorVersion"] != "6000.6.7f1" or report["unity"] != identity["unityEditorVersion"]
            or report["gateSha256"] != delivery.sha(root / GATE)):
        raise ValueError("Build proof does not match the frozen v1.1 delivery gate")
    tests = gate["tests"]
    keys = [(t["mode"], t.get("resolution")) for t in tests]
    if len(keys) != 3 or set(keys) != {("EditMode", None), *(("PlayMode", r) for r in delivery.RESOLUTIONS)}:
        raise ValueError("Gate must cover EditMode and both PlayMode GameViews")
    files = {f["path"]: f["sha256"] for f in gate["files"]}
    if len(files) != len(gate["files"]):
        raise ValueError("Duplicate file in frozen gate")
    if files.get(MANIFEST) != delivery.sha(root / MANIFEST):
        raise ValueError("Font manifest differs from the frozen build")
    for test in tests:
        mode = test["mode"]
        count = gate["expectedCounts"][mode]
        if (count < delivery.HISTORICAL_MINIMUM[mode] or test["passed"] != count
                or len(set(test["names"])) != count or len(test["names"]) != count
                or gate["editModePassed" if mode == "EditMode" else "playModePassed"] != count):
            raise ValueError("Incomplete gated suite: " + mode)
        for ref in (test["report"], test["binding"]):
            if files.get(ref["path"]) != ref["sha256"] or delivery.sha(contained(root, ref["path"])) != ref["sha256"]:
                raise ValueError("Stale test evidence: " + ref["path"])
        path = contained(root, test["report"]["path"])
        measured = delivery.verify_nunit(path, mode, count, root)
        if measured["names"] != sorted(test["names"]):
            raise ValueError("Gate test names differ from executed cases")
        if mode == "PlayMode" and delivery.measured_resolution(path) != test["resolution"]:
            raise ValueError("Gated resolution differs from actual NUnit evidence")
    return identity


def build_inputs(root, platform, proof, native):
    output_rel, target, native_target, profile, _ = TARGET[platform]
    output = (root / "Builds/v11" / output_rel).resolve()
    directory = output.parent
    if (proof.get("schema") != "v11-player-build-1" or proof["result"] != "Succeeded" or proof["errors"] != 0
            or native["buildResult"] != "1" or native["totalErrors"] != "0" or native["platformName"] != native_target
            or not re.fullmatch("[0-9a-f]{32}", proof["buildGuid"]) or proof["buildGuid"] == "0" * 32
            or native["buildGUID"] != proof["buildGuid"] or int(native["totalSize"]) != proof["bytes"]
            or proof["bytes"] <= 0 or proof["platform"] != target or Path(proof["output"]).resolve() != output
            or Path(native["outputPath"]).resolve() != output or proof["profile"] != native["buildProfilePath"]
            or proof["profile"] != "Assets/Settings/BuildProfiles/" + profile + ".asset"):
        raise ValueError("Native BuildReport does not match the selected player build")
    rows = {}
    for path_str, size in native["files"].items():
        path = Path(path_str).resolve()
        if not path.is_relative_to(directory):
            raise ValueError("Unexpected native output location: " + path_str)
        rel = path.relative_to(directory)
        if not any(part.endswith(SKIP) for part in rel.parts):
            if rel.as_posix() in rows:
                raise ValueError("Duplicate player output: " + path_str)
            rows[rel.as_posix()] = size
    checksums = {f["path"]: f["sha256"] for f in proof["playerFiles"]}
    actual = {p.relative_to(directory).as_posix(): p for p in directory.rglob("*")
              if p.is_file() and not any(part.endswith(SKIP) for part in p.relative_to(directory).parts)}
    if (not rows or set(rows) != set(actual) or set(rows) != set(checksums)
            or len(checksums) != len(proof["playerFiles"])):
        raise ValueError("Player directory, native file inventory and build-time hashes differ")
    if any(actual[name].stat().st_size != size for name, size in rows.items()):
        raise ValueError("Player file size changed after build")
    native_files = []
    if platform == "macos":
        for name, path in actual.items():
            info = macho_info(path)
            if info is not None:
                if sorted(info["architectures"]) != ["arm64", "x86_64"]:
                    raise ValueError("Non-Universal Mac binary: " + name)
                native_files.append(name)
        mains = [name for name in native_files if Path(name).parent.as_posix() == output.name + "/Contents/MacOS"]
        if len(mains) != 1:
            raise ValueError("Missing or ambiguous Universal main executable")
    else:
        if output.name not in actual:
            raise ValueError("Missing Windows player executable")
        with output.open("rb") as stream:
            header = stream.read(64)
            if len(header) != 64 or header[:2] != b"MZ":
                raise ValueError("Missing Windows PE header")
            stream.seek(struct.unpack_from("<I", header, 60)[0])
            if stream.read(6) != b"PE\0\0\x64\x86":
                raise ValueError("Windows player executable is not x64")
    return actual, checksums, sorted(native_files)


def write_archive(path, files, checksums, additions, executables):
    if set(files) & set(additions):
        raise ValueError("Player contains a reserved package member")
    all_names = sorted([*files, *additions])
    if len(set(name.casefold() for name in all_names)) != len(all_names):
        raise ValueError("Case-insensitive ZIP member collision")
    members = {}
    with zipfile.ZipFile(path, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for name in all_names:
            info = zipfile.ZipInfo(TOP + "/" + name)  # Fixed DOS epoch and Unix modes on every host.
            info.create_system = 3
            info.external_attr = (0o100755 if name in executables else 0o100644) << 16
            info.compress_type = zipfile.ZIP_DEFLATED
            expected = checksums[name] if name in files else digest(additions[name])
            source = files[name].open("rb") if name in files else io.BytesIO(additions[name])
            with source, archive.open(info, "w", force_zip64=True) as target:
                h = hashlib.sha256()
                while chunk := source.read(1024 * 1024):
                    h.update(chunk)
                    target.write(chunk)
            if h.hexdigest() != expected:
                raise ValueError("Player bytes differ from the build-time hash: " + name)
            members[info.filename] = expected
    with zipfile.ZipFile(path) as archive:
        if archive.namelist() != list(members):
            raise ValueError("Archive member inventory mismatch")
        for name, expected in members.items():
            with archive.open(name) as stream:
                if hashlib.file_digest(stream, "sha256").hexdigest() != expected:
                    raise ValueError("ZIP readback differs from player: " + name)
    return members


def package(root, platform, proof_path, binary2text):
    report = delivery.read_json(proof_path)
    identity = verify_gate(root, delivery.read_json(root / GATE), report)
    subprocess.run(["git", "merge-base", "--is-ancestor", identity["sourceCommit"], "HEAD"], cwd=root, check=True)
    native_path = contained(root, report["nativeBuildReport"]["path"])
    if native_path.suffix != ".buildreport" or delivery.sha(native_path) != report["nativeBuildReport"]["sha256"]:
        raise ValueError("Native Unity BuildReport has changed")
    native = read_native(native_path, binary2text)
    files, checksums, executables = build_inputs(root, platform, report, native)
    required_art = required_art_inventory(root, native["assets"])
    fonts, forbidden_names, forbidden_hashes = check_font_assets(root, native["assets"], checksums)
    if any(Path(p).name.casefold() in forbidden_names or sha in forbidden_hashes for p, sha in checksums.items()):
        raise ValueError("Player contains a loose title font source")
    licenses = license_bundle(root, fonts)
    additions = {"开始游玩.txt": (root / README).read_bytes()}
    directories = ["Licenses/"]
    if platform == "macos":
        directories.append("DicePrototype.app/Contents/Resources/Licenses/")
    for prefix in directories:
        additions.update({prefix + name: content for name, content in licenses.items()})
    name = f"{TOP}-{TARGET[platform][-1]}.zip"
    archive = root / "Releases" / VERSION / name
    receipt = root / "evidence/v11/packages" / (name + ".json")
    archive.parent.mkdir(parents=True, exist_ok=True)
    receipt.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(dir=archive.parent) as zip_temp, tempfile.TemporaryDirectory(dir=receipt.parent) as proof_temp:
        pending = Path(zip_temp) / name
        members = write_archive(pending, files, checksums, additions, executables)
        proof = {"schema": "v11-player-package-1", "package": name, "platform": platform, "identity": identity,
                 "bytes": pending.stat().st_size, "sha256": delivery.sha(pending), "members": len(members),
                 "files": members, "gateSha256": report["gateSha256"], "buildGuid": report["buildGuid"],
                 "buildReport": {"path": proof_path.relative_to(root).as_posix(), "sha256": delivery.sha(proof_path)},
                 "nativeBuildReport": report["nativeBuildReport"], "packedGroups": native["packedGroups"],
                 "packedObjects": native["packedObjects"], "playModeResolutions": list(delivery.RESOLUTIONS),
                 "requiredArtPathGuids": required_art,
                 "fontSizeReport": fw09_size_report(root, platform, native, pending.stat().st_size),
                 "excludedTitleSourceFiles": sorted(forbidden_names),
                 "liberationSansIncluded": any(f["packageFile"] == "LiberationSans-OFL.txt" for f in fonts),
                 "fontLicenses": sorted(licenses), "universalBinaries": executables,
                 "macOSLaunch": "未验证" if platform == "macos" else "不适用"}
        draft = Path(proof_temp) / receipt.name
        draft.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        # If interrupted between the two file publications, a retry rebuilds/reads back the ZIP
        # and fills the missing side. A different archive or receipt is never overwritten.
        if receipt.exists() and delivery.read_json(receipt) != proof:
            raise FileExistsError("Existing package receipt differs: " + str(receipt))
        try:
            os.link(pending, archive)
        except FileExistsError:
            if delivery.sha(archive) != proof["sha256"]:
                raise FileExistsError("Existing player archive differs: " + str(archive))
        try:
            os.link(draft, receipt)
        except FileExistsError:
            if delivery.read_json(receipt) != proof:
                raise FileExistsError("Existing package receipt differs: " + str(receipt))
    return proof


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--platform", required=True, choices=TARGET)
    parser.add_argument("--build-report", required=True, help="Repository-relative Unity build proof JSON")
    parser.add_argument("--binary2text", default=BINARY2TEXT)
    args = parser.parse_args()
    proof = package(ROOT, args.platform, contained(ROOT, args.build_report), args.binary2text)
    print(json.dumps({k: proof[k] for k in ("package", "bytes", "sha256", "members")}))


if __name__ == "__main__":
    main()
