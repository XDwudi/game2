"""Publish reproducible, compressed Editor diagnostics without launch credentials."""
from pathlib import Path
import gzip
import hashlib
import json
import re

ROOT = Path(__file__).resolve().parents[2]
PRIVATE = re.compile(r"licensing|entitlement|serial\s*(?:number|:)|access[ _-]?token|refresh[ _-]?token|id[ _-]?token|bearer|authorization|client[ _-]?secret|\bauth\b|session\s*id|correlation\s*id|machine\s*id|user\s*id", re.I)


def sanitize(raw):
    output = []
    arguments = False
    for line in raw.splitlines():
        if "COMMAND LINE ARGUMENTS" in line:
            arguments = True
            output.append("[Startup command-line block omitted: may contain account credentials.]")
            continue
        if arguments:
            if "Successfully changed project path" not in line:
                continue
            arguments = False
        output.append("[Account/licensing identifier or credential line redacted]" if PRIVATE.search(line) else line)
    return "\n".join(output) + "\n"


def sha(data):
    return hashlib.sha256(data).hexdigest()


if __name__ == "__main__":
    sample = "Version 6000.6\nMachine Id: private\nCOMMAND LINE ARGUMENTS:\n-token\nsecret\nSuccessfully changed project path to: workspace\nUnauthorizedAccessException: retained\nTEST_FAILED retained"
    clean = sanitize(sample)
    assert "secret" not in clean and "private" not in clean
    assert "UnauthorizedAccessException: retained" in clean and "TEST_FAILED retained" in clean
    assert "hidden" not in sanitize("COMMAND LINE ARGUMENTS:\nhidden")
    directory = ROOT / "evidence/environment/sanitized"
    directory.mkdir(exist_ok=True)
    manifest = []
    for path in sorted((ROOT / "evidence/environment").glob("editor*.log")):
        raw = path.read_bytes()
        data = sanitize(raw.decode("utf-8", errors="replace")).encode("utf-8")
        published = directory / (path.stem + ".txt.gz")
        compressed = gzip.compress(data, mtime=0)
        published.write_bytes(compressed)
        manifest.append(dict(original=path.relative_to(ROOT).as_posix(), rawSha256=sha(raw),
            published=published.relative_to(ROOT).as_posix(), publishedSha256=sha(compressed),
            redaction="Launch argument blocks and account/licensing identifiers omitted. Diagnostic errors, test and build results outside those blocks retained."))
    (directory / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps(dict(sanitizerChecks=3, publishedLogs=len(manifest))))
