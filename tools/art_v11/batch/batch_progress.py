"""Count W22 illustration progress per theme: jobs whose latest revision has a valid record, and the latest jobSha match."""
import hashlib
import json
import sys
from pathlib import Path

root = Path(sys.argv[1])
themes = ["swiss", "sancai", "comic", "deco", "glass"]
for t in themes:
    base = root / "ArtSource/Art11" / t / "illustrations"
    jobs = sorted(p for p in base.iterdir() if (p / "job.json").exists())
    done = stale = invalid = none = 0
    for j in jobs:
        revs = sorted((r for r in j.glob("r*") if (r / "record.json").exists()), key=lambda r: int(r.name[1:]))
        if not revs:
            none += 1
            continue
        rec = json.loads((revs[-1] / "record.json").read_text(encoding="utf-8"))
        job_sha = hashlib.sha256((j / "job.json").read_bytes()).hexdigest()
        rec_sha = rec.get("jobSha256")
        ok = bool(rec.get("runValid")) and not rec.get("runProblems")
        if not ok:
            invalid += 1
        elif rec_sha and rec_sha != job_sha:
            stale += 1
        else:
            done += 1
    print(f"{t:7s} jobs={len(jobs)} done={done} stale={stale} invalid={invalid} none={none}")
