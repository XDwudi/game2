"""Package the current Windows build without Unity's non-shipping backup folder."""
import hashlib
import json
from pathlib import Path
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]
BUILD = ROOT / "Builds/Windows"
OUTPUT = ROOT / "Builds/DiceCraft-P0-Windows.zip"


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def main():
    if not (BUILD / "DiceCraft.exe").is_file():
        raise SystemExit("Builds/Windows/DiceCraft.exe is missing; build with Unity first.")
    files = sorted(p for p in BUILD.rglob("*") if p.is_file()
                   and not any("BackUpThisFolder_ButDontShipItWithYourGame" in part for part in p.parts))
    manifest = {
        "sourceCommit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
        "sourceStatus": subprocess.check_output(["git", "status", "--porcelain", "--untracked-files=no"], cwd=ROOT, text=True),
        "note": "Packaging integrity only. Acceptance status and build-to-source PDB checks are in docs/STATE.md and evidence/review. A dirty sourceStatus must be assessed; HEAD alone does not identify uncommitted build sources.",
        "files": [{"path": p.relative_to(BUILD).as_posix(), "bytes": p.stat().st_size, "sha256": sha(p)} for p in files],
    }
    evidence = ROOT / "evidence/delivery"
    evidence.mkdir(exist_ok=True)
    encoded = json.dumps(manifest, ensure_ascii=False, indent=2).encode("utf-8")
    (evidence / "windows-build-manifest.json").write_bytes(encoded)
    with zipfile.ZipFile(OUTPUT, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for p in files:
            archive.write(p, "DiceCraft-P0/" + p.relative_to(BUILD).as_posix())
        archive.writestr("DiceCraft-P0/build-manifest.json", encoded)
        archive.write(ROOT / "docs/PLAYER_README.txt", "DiceCraft-P0/开始游玩.txt")
    with zipfile.ZipFile(OUTPUT) as archive:
        bad = archive.testzip()
        if bad:
            raise RuntimeError("ZIP CRC validation failed: " + bad)
    result = {"archive": OUTPUT.relative_to(ROOT).as_posix(), "bytes": OUTPUT.stat().st_size,
              "sha256": sha(OUTPUT), "runtimeFiles": len(files), "zipCrc": "passed"}
    (evidence / "package.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result))


if __name__ == "__main__":
    main()
