"""Package complete v0.5 players and record bytes; macOS final ditto acceptance is separate."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import stat
import zipfile

from v05_delivery import ROOT, IDENTITY, read_json, sha
from macos.package_checks import macho_info, safe_archive_inventory


def verification_kit(windows_results):
    report=read_json(windows_results / "player-report.json")
    if report.get("status")!="passed" or not report.get("stressCompleted"):
        raise ValueError("A passing real Windows Player run is required for the Mac comparison kit")
    identity=read_json(ROOT / IDENTITY)
    if json.loads(report["buildIdentity"])["sourceCommit"]!=identity["sourceCommit"]:
        raise ValueError("Windows evidence belongs to another source version")
    output=ROOT / "Releases/p0.5.0/DicePrototype-p0.5.0-MacVerificationKit.zip"
    if output.exists():
        raise ValueError("Verification kit already exists; retain old evidence explicitly")
    prefix="DicePrototype-MacVerification/"
    with zipfile.ZipFile(output,"w",compression=zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
        for name in ("VerifyMac.command","verify_mac.py","package_checks.py","compare_replays.py"):
            archive.write(ROOT / "tools/macos" / name,prefix+name)
        archive.write(ROOT / "docs/tasks/P0-V05-MAC-VERIFICATION.md",prefix+"执行说明.md")
        archive.write(ROOT / "Releases/p0.5.0/DicePrototype-p0.5.0-macos-universal.zip",prefix+"inputs/macos.zip",compress_type=zipfile.ZIP_STORED)
        archive.write(ROOT / IDENTITY,prefix+"inputs/BUILD_IDENTITY.json")
        archive.write(ROOT / "evidence/v05/session/normal-run-trace.json",prefix+"inputs/normal-run-trace.json")
        for name in ("player-report.json","normal-replay.jsonl","debug24-replay.jsonl",
                     "cross-platform-checkpoint/active-run.json","cross-platform-checkpoint/command.json","cross-platform-checkpoint/expected.json"):
            archive.write(windows_results / name,prefix+"inputs/windows-results/"+name)
    with zipfile.ZipFile(output) as archive:
        if archive.testzip():raise ValueError("Verification kit ZIP CRC failure")
    safe_archive_inventory(output)
    result={"packagePath":output.relative_to(ROOT).as_posix(),"sha256":sha(output),"byteSize":output.stat().st_size,
            "sourceCommit":identity["sourceCommit"],"windowsResults":windows_results.relative_to(ROOT).as_posix(),"crc":"passed"}
    (ROOT / "evidence/v05/packages/mac-verification-kit.json").write_text(json.dumps(result,indent=2),encoding="utf-8")
    print(json.dumps(result))


def package(platform):
    mac = platform == "macos"
    directory = ROOT / ("Builds/v05/macOS" if mac else "Builds/v05/Windows")
    required = directory / ("DicePrototype.app/Contents/Info.plist" if mac else "DicePrototype.exe")
    if not required.is_file():
        raise ValueError("Missing player: " + str(required))
    identity = read_json(ROOT / IDENTITY)
    output = ROOT / "Releases/p0.5.0" / ("DicePrototype-p0.5.0-macos-universal.zip" if mac else "DicePrototype-p0.5.0-win-x64.zip")
    output.parent.mkdir(parents=True, exist_ok=True)
    if output.exists():
        raise ValueError("Keep previous package evidence; move or archive the old package explicitly first")
    files = sorted(p for p in directory.rglob("*") if (p.is_file() or p.is_symlink())
                   and not any("BackUpThisFolder" in part or "BurstDebugInformation" in part for part in p.parts))
    records, native = [], []
    with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for path in files:
            name = path.relative_to(directory).as_posix()
            native_info = macho_info(path) if mac and not path.is_symlink() else None
            if native_info:
                if set(native_info["architectures"]) != {"arm64", "x86_64"}:
                    raise ValueError("Not Universal: " + name)
                native.append({"path": name, **native_info})
            mode = stat.S_IFREG | 0o644
            if native_info and any(s["filetype"] == 2 for s in native_info["slices"]):
                mode = stat.S_IFREG | 0o755
            data = os.readlink(path).encode("utf-8") if path.is_symlink() else path.read_bytes()
            if path.is_symlink():
                mode = stat.S_IFLNK | 0o777
                data = os.readlink(path).encode("utf-8")
            item = zipfile.ZipInfo(name)
            item.create_system = 3
            item.external_attr = mode << 16
            item.compress_type = zipfile.ZIP_DEFLATED
            archive.writestr(item, data)
            records.append({"path": name, "byteSize": len(data), "sha256": hashlib.sha256(data).hexdigest(), "zipMode": oct(mode)})
        archive.writestr("BUILD_IDENTITY.json", json.dumps(identity, ensure_ascii=False, indent=2))
        archive.write(ROOT / "DicePrototype/Assets/Game/Fonts/LICENSE.txt", "Licenses/NotoSansCJK-OFL.txt")
        archive.write(ROOT / "docs/PLAYER_README_V05.txt", "开始游玩.txt")
    # Inventory the complete archive, including metadata/license/readme entries.
    # Hash symlink target bytes stored in ZIP rather than following the link.
    with zipfile.ZipFile(output) as archive:
        records=[{"path":item.filename,"byteSize":item.file_size,
                  "sha256":hashlib.sha256(archive.read(item)).hexdigest(),"zipMode":oct(item.external_attr>>16)}
                 for item in archive.infolist()]
    with zipfile.ZipFile(output) as archive:
        if archive.testzip():
            raise ValueError("ZIP CRC failure")
    safe_archive_inventory(output)
    proof = {"identity": identity, "platform": platform, "packagePath": output.relative_to(ROOT).as_posix(),
             "sha256": sha(output), "byteSize": output.stat().st_size, "files": records,
             "nativeComponents": native, "crc": "passed", "acceptance": "pending_player_and_clean_restore",
             "macOSPackaging": "Windows-created transfer candidate; final macOS ditto/re-extract/native-run pending" if mac else None}
    evidence = ROOT / "evidence/v05/packages"
    evidence.mkdir(parents=True, exist_ok=True)
    (evidence / (platform + "-package-candidate.json")).write_text(json.dumps(proof, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({key: proof[key] for key in ("packagePath", "sha256", "byteSize", "crc", "acceptance")}))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("platform", choices=["windows", "macos", "kit"])
    parser.add_argument("--windows-results", type=Path)
    args=parser.parse_args()
    if args.platform=="kit":
        if args.windows_results is None:parser.error("kit needs --windows-results")
        verification_kit(args.windows_results.resolve())
    else:package(args.platform)
