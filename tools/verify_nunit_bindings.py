"""Verify NUnit binding files against the current working tree.

Usage: python -I verify_bindings.py <repo_root> <binding.json> [...]
For each binding: report sha matches the xml; every file hash listed in
started/finished matches the file on disk now; started == finished.
"""
import hashlib
import json
import sys
from pathlib import Path


def sha(p: Path) -> str:
    h = hashlib.sha256()
    with p.open("rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def entries(block):
    files = block.get("files", [])
    out = {}
    for e in files:
        path = e.get("path") or e.get("file")
        digest = e.get("sha256") or e.get("hash")
        out[path] = digest
    return out


def main():
    root = Path(sys.argv[1])
    for b in sys.argv[2:]:
        data = json.loads(Path(b).read_text(encoding="utf-8"))
        rep = data["report"]
        rep_ok = sha(root / rep["path"]) == rep["sha256"]
        s = entries(data["started"])
        f = entries(data["finished"])
        same = s == f
        mism, missing = [], []
        for path, digest in f.items():
            p = root / path
            if not p.exists():
                missing.append(path)
            elif sha(p) != digest:
                mism.append(path)
        print(f"{Path(b).name}: report_sha_ok={rep_ok} files={len(f)} started==finished={same} "
              f"mismatch_now={len(mism)} missing_now={len(missing)}")
        for m in mism[:15]:
            print("   MISMATCH", m)
        for m in missing[:15]:
            print("   MISSING", m)
        keys = [k for k in f if k and ("Shop14" in k or "Art07Guide" in k or "Card11" in k)]
        print("   key files listed:", ", ".join(sorted(Path(k).name for k in keys)))


if __name__ == "__main__":
    main()
