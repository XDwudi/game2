"""Serial batch driver for the v1.0 illustrations (wraps tools/art_gen/artgen.py generate).

For each job in tools/art_v10/jobs.py order: skip ids that already have a valid revision; otherwise call
`artgen generate`. A run artgen marks INVALID_RUN is kept on disk and retried, at most --retries extra times
(AI_ART_PIPELINE 4.4: the same defect is re-rolled twice at most). Nothing is copied into the Unity project here;
tools/art_v10/stage.py does that after review.

    python tools/art_v10/run_batch.py [--only C104,C105] [--retries 2] [--status]
"""
import argparse
import json
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).resolve().parent))
from jobs import JOBS, SUBJECTS  # noqa: E402

LOG = ROOT / "artifacts" / "cache" / "v10" / "art-logs"


def revisions(content_id: str) -> list:
    out = []
    for rev in sorted((JOBS / content_id).glob("r[0-9][0-9]")):
        record = rev / "record.json"
        if record.exists():
            data = json.loads(record.read_text(encoding="utf-8"))
            out.append({"rev": rev.name, "valid": bool(data.get("runValid")), "problems": data.get("runProblems") or [],
                        "quota": (data.get("codex") or {}).get("quotaUsedPercentLast"),
                        "seconds": (data.get("codex") or {}).get("durationSeconds")})
        else:
            out.append({"rev": rev.name, "valid": False, "problems": ["no record.json"], "quota": None, "seconds": None})
    return out


def status() -> dict:
    rows = {}
    for content_id, _ in SUBJECTS:
        revs = revisions(content_id)
        rows[content_id] = {"valid": [r["rev"] for r in revs if r["valid"]], "invalid": [r["rev"] for r in revs if not r["valid"]]}
    return rows


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", default="")
    ap.add_argument("--retries", type=int, default=2)
    ap.add_argument("--status", action="store_true")
    args = ap.parse_args()
    if args.status:
        rows = status()
        done = [i for i, r in rows.items() if r["valid"]]
        print(json.dumps({"valid": len(done), "total": len(rows), "missing": [i for i in rows if i not in done],
                          "withInvalidRuns": {i: r["invalid"] for i, r in rows.items() if r["invalid"]}}, ensure_ascii=False, indent=1))
        return 0
    only = {x.strip() for x in args.only.split(",") if x.strip()}
    LOG.mkdir(parents=True, exist_ok=True)
    log = LOG / "batch.log"
    failed = []
    for content_id, _ in SUBJECTS:
        if only and content_id not in only:
            continue
        if any(r["valid"] for r in revisions(content_id)):
            continue
        ok = False
        for attempt in range(args.retries + 1):
            started = time.time()
            proc = subprocess.run([sys.executable, str(ROOT / "tools" / "art_gen" / "artgen.py"), "generate", "--job",
                                   str(JOBS / content_id / "job.json")], cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
            revs = revisions(content_id)
            last = revs[-1] if revs else {"rev": "-", "valid": False, "problems": ["no revision written"], "quota": None}
            line = f"{time.strftime('%Y-%m-%dT%H:%M:%S')} {content_id} attempt={attempt + 1} exit={proc.returncode} rev={last['rev']} valid={last['valid']} quota={last['quota']} seconds={time.time() - started:.0f} problems={last['problems']}"
            with log.open("a", encoding="utf-8") as handle:
                handle.write(line + "\n")
                if proc.returncode != 0:
                    handle.write("  stdout: " + proc.stdout[-600:].replace("\n", " | ") + "\n  stderr: " + proc.stderr[-600:].replace("\n", " | ") + "\n")
            print(line, flush=True)
            if last["valid"] and proc.returncode == 0:
                ok = True
                break
        if not ok:
            failed.append(content_id)
    print("BATCH_DONE failed=" + ",".join(failed), flush=True)
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
