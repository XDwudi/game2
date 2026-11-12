"""Package a gated v0.72 player build into a release ZIP and record what was packed.

This packager is deliberately smaller than package_v071: v0.72 ships no Windows performance
run and no Mac verification kit, and says so in the proof. It refuses unless the build report
succeeded and its identity matches the gate and the frozen source commit.
"""
import argparse
import hashlib
import json
import subprocess
import zipfile
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
VERSION = "p0.7.2"
GATE = ROOT / "evidence/v072/unity/build-gate.json"
RELEASE = ROOT / "Releases" / VERSION
PROOFS = ROOT / "evidence/v072/packages"
README = ROOT / "docs/PLAYER_README_V072.txt"
SKIP = ("_BackUpThisFolder_ButDontShipItWithYourGame", "_BurstDebugInformation_DoNotShip")


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def main():
    parser = argparse.ArgumentParser()
    # rc.2 adds the macOS Universal transfer package. It is built and zipped on Windows: unsigned,
    # not notarized and never launched on a Mac. The proof and the player note say so.
    parser.add_argument("--platform", required=True, choices=["windows", "macos"])
    parser.add_argument("--build-report", required=True)
    args = parser.parse_args()
    mac = args.platform == "macos"
    report_path = ROOT / args.build_report
    report = read_json(report_path)
    identity = json.loads(report["identity"])
    gate = read_json(GATE)
    head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    if report.get("result") != "Succeeded" or report.get("errors") != 0:
        raise ValueError("Build report is not a successful build")
    if report.get("platform") != ("StandaloneOSX" if mac else "StandaloneWindows64"):
        raise ValueError("Build report platform does not match --platform")
    if identity != gate["identity"] or identity["releaseVersion"] != VERSION or identity["sourceCommit"] != gate["sourceCommit"]:
        raise ValueError("Build identity does not match the verified gate")
    ancestor = subprocess.run(["git", "merge-base", "--is-ancestor", identity["sourceCommit"], head], cwd=ROOT)
    if ancestor.returncode != 0:
        raise ValueError("Frozen source commit is not in the current history")
    directory = ROOT / ("Builds/v072/macOS" if mac else "Builds/v072/Windows")
    files = sorted(p for p in directory.rglob("*") if p.is_file() and not any(part.endswith(SKIP) for part in p.relative_to(directory).parts))
    # The Mac executable carries the product name ("DiceCraft P0"), so look for the bundle layout instead.
    if not files or not any((p.parent.name == "MacOS" and p.parent.parent.name == "Contents") if mac else p.name == "DicePrototype.exe" for p in files):
        raise ValueError("Player build output is missing: " + str(directory))
    name = f"DicePrototype-{VERSION}-{'macos-universal' if mac else 'win-x64'}.zip"
    RELEASE.mkdir(parents=True, exist_ok=True)
    PROOFS.mkdir(parents=True, exist_ok=True)
    output = RELEASE / name
    if output.exists():
        raise ValueError("Refusing to overwrite an existing package: " + str(output))
    top = f"DicePrototype-{VERSION}"
    members, native = [], []
    if mac:
        import sys
        sys.path.insert(0, str(ROOT / "tools"))
        from macos.v071.package_checks import macho_info
        for path in files:
            info = macho_info(path)
            if info is not None:
                native.append({"path": path.relative_to(directory).as_posix(), "architectures": info["architectures"]})
        main = [n for n in native if "/Contents/MacOS/" in n["path"]]
        if len(main) != 1 or sorted(main[0]["architectures"]) != ["arm64", "x86_64"]:
            raise ValueError("Mac player executable is not a single Universal (x86_64 + arm64) binary")
        if any(sorted(n["architectures"]) != ["arm64", "x86_64"] for n in native):
            raise ValueError("A native Mac component is not Universal")
    executable = {n["path"] for n in native}
    with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        archive.write(README, f"{top}/开始游玩.txt")
        members.append({"path": f"{top}/开始游玩.txt", "sha256": sha(README)})
        for path in files:
            relative = path.relative_to(directory).as_posix()
            member = f"{top}/{relative}"
            info = zipfile.ZipInfo.from_file(path, member)
            if mac:
                # Created on Windows: write Unix modes explicitly so unzip on macOS keeps binaries runnable.
                info.create_system = 3
                info.external_attr = ((0o100755 if relative in executable else 0o100644) << 16)
            with path.open("rb") as stream:
                archive.writestr(info, stream.read(), zipfile.ZIP_DEFLATED, 6)
            members.append({"path": member, "sha256": sha(path)})
    with zipfile.ZipFile(output) as archive:
        if archive.testzip() is not None or len(archive.namelist()) != len(members):
            raise ValueError("Written archive failed its integrity check")
    proof = {"schema": "v072-package-1", "utc": datetime.now(timezone.utc).isoformat(), "platform": args.platform, "package": name,
             "bytes": output.stat().st_size, "sha256": sha(output), "members": len(members), "identity": identity,
             "buildReport": {"path": args.build_report, "sha256": sha(report_path), "buildGuid": report.get("buildGuid")},
             "gate": {"sha256": sha(GATE), "editModePassed": gate["editModePassed"], "playModePassed": gate["playModePassed"]},
             "notRun": ["Windows frame-time / exclusive-fullscreen verification run", "macOS launch on real hardware", "signing and notarization"],
             "files": members}
    if mac:
        proof["machO"] = native
        proof["codeSignatureFolderPresent"] = any("/_CodeSignature/" in ("/" + m["path"]) for m in members)
        proof["macOSPackaging"] = ("Windows-created transfer package: no Developer ID signature, not notarized, never launched on macOS; "
                                   "any _CodeSignature folder is whatever the Unity build wrote and was not verified with codesign")
    (PROOFS / (name + ".json")).write_text(json.dumps(proof, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({k: proof[k] for k in ("package", "bytes", "sha256", "members")}))


if __name__ == "__main__":
    main()
