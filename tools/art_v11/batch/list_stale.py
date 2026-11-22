"""List illustration jobs whose latest valid revision was rendered from an older job.json (record jobSha256 differs)."""
import hashlib
import json
import sys
from pathlib import Path

root = Path(sys.argv[1])
for t in sys.argv[2].split(","):
    base = root / "ArtSource/Art11" / t / "illustrations"
    stale = []
    for j in sorted(p for p in base.iterdir() if (p / "job.json").exists()):
        revs = sorted((r for r in j.glob("r*") if (r / "record.json").exists()), key=lambda r: int(r.name[1:]))
        if not revs:
            continue
        rec = json.loads((revs[-1] / "record.json").read_text(encoding="utf-8"))
        if rec.get("jobSha256") != hashlib.sha256((j / "job.json").read_bytes()).hexdigest():
            stale.append(f"{j.name}/{revs[-1].name}@{rec.get('recordedUtc', '?')[:16]}")
    print(t, len(stale), " ".join(stale))
