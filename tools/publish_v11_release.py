"""Prepare the local p1.1.0-rc.1 release plan. Only --execute contacts the remote.

python tools/publish_v11_release.py --notes docs/RELEASE_NOTES_V11_RC1.md

With an untracked review under evidence/, bind the final review file's SHA-256 in
the committed notes as an HTML comment: <!-- W25 SHA-256: <hash> -->.
"""
import argparse
import hashlib
import json
import os
import re
import subprocess
import tempfile
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

if __package__:
    from . import package_v11 as pack
    from . import publish_v10_release as previous
    from . import replay_v11 as replay
else:
    import package_v11 as pack
    import publish_v10_release as previous
    import replay_v11 as replay

TAG = "p1.1.0-rc.1"
TITLE = f"骰子构筑 v1.1 试玩版（{TAG}）"
ROOT = pack.ROOT
API = previous.API
RELEASE_TOOLS = (
    "tools/publish_v11_release.py", "tools/publish_v10_release.py", "tools/package_v11.py", "tools/package_v10.py",
    "tools/replay_v11.py", "tools/v11_build_report.py", "tools/v11_delivery.py", "tools/quality_check.py",
    "tools/macos/v071/package_checks.py", "tools/balance_sim/Program.cs", "tools/balance_sim/Runner.cs",
    "tools/v11_text/glossary_rules.json",
)


def git(root, *args):
    return subprocess.check_output(["git", "-c", "core.quotePath=false", *args], cwd=root, text=True, encoding="utf-8").strip()


def checked_ref(root, ref):
    path = pack.contained(root, ref["path"])
    if pack.delivery.sha(path) != ref["sha256"]:
        raise ValueError("Release evidence has changed: " + ref["path"])
    return path


def verify_release_sources(root, target, files):
    tracked = git(root, "ls-files", "--", *files).splitlines()
    if set(tracked) != set(files) or subprocess.run(
            ["git", "diff", "--quiet", target, "--", *files], cwd=root).returncode:
        raise ValueError("Release inputs must match the release target commit")


def plan_release(root, notes_path, replays_path="evidence/v11/replays/final/index.json",
                 review_path="evidence/v11/review/final.md", binary2text=pack.BINARY2TEXT):
    if git(root, "remote", "get-url", "origin") != previous.REMOTE:
        raise ValueError("Unexpected origin")
    branch = git(root, "branch", "--show-current")
    if branch != "codex/v1.1":
        raise ValueError("Expected the v1.1 development branch")
    target = git(root, "rev-parse", "HEAD")
    gate = pack.delivery.read_json(root / pack.GATE)
    frozen = gate["sourceCommit"]
    subprocess.run(["git", "merge-base", "--is-ancestor", frozen, target], cwd=root, check=True)
    paths = pack.delivery.SCOPES
    if subprocess.run(["git", "diff", "--quiet", frozen, target, "--", *paths], cwd=root).returncode:
        raise ValueError("Buildable sources changed between the frozen commit and release target")
    if git(root, "status", "--porcelain", "--", *paths):
        raise ValueError("Buildable sources have local changes")
    tag = subprocess.run(["git", "rev-parse", "--verify", f"refs/tags/{TAG}^{{commit}}"], cwd=root,
                         text=True, capture_output=True)
    if tag.returncode == 0 and tag.stdout.strip() != target:
        raise ValueError("Local release tag targets a different commit")
    notes = pack.contained(root, notes_path)
    body = notes.read_text(encoding="utf-8-sig")
    if not body.strip():
        raise ValueError("Empty release notes")
    review = pack.contained(root, review_path)
    if not review.read_text(encoding="utf-8-sig").strip():
        raise ValueError("The W25 independent review is empty")
    release_files = [*RELEASE_TOOLS, pack.README, notes.relative_to(root).as_posix()]
    review_name = review.relative_to(root).as_posix()
    if git(root, "ls-files", "--", review_name):
        release_files.append(review_name)
    elif f"<!-- W25 SHA-256: {pack.delivery.sha(review)} -->" not in body:
        raise ValueError("Bind the local W25 report in committed notes with <!-- W25 SHA-256: <hash> -->")
    verify_release_sources(root, target, release_files)
    assets = []
    replay_index = None
    for platform, spec in pack.TARGET.items():
        name = f"{pack.TOP}-{spec[-1]}.zip"
        package = root / "Releases" / pack.VERSION / name
        ref = f"evidence/v11/packages/{name}.json"
        proof = pack.delivery.read_json(root / ref)
        if (proof["schema"] != "v11-player-package-1" or proof["package"] != name or proof["platform"] != platform
                or proof["identity"] != gate["identity"] or proof["gateSha256"] != pack.delivery.sha(root / pack.GATE)
                or proof["playModeResolutions"] != list(pack.delivery.RESOLUTIONS)
                or proof["packedGroups"] < 1 or proof["packedObjects"] < 1 or proof["members"] != len(proof["files"])
                or proof["members"] < 1 or package.stat().st_size != proof["bytes"]
                or pack.delivery.sha(package) != proof["sha256"]):
            raise ValueError("Package does not match the frozen proof: " + name)
        if proof["files"].get(pack.TOP + "/开始游玩.txt") != pack.delivery.sha(root / pack.README):
            raise ValueError("Player README in the archive differs from the release target: " + name)
        build = pack.delivery.read_json(checked_ref(root, proof["buildReport"]))
        native_path = checked_ref(root, proof["nativeBuildReport"])
        if (build["schema"] != "v11-player-build-1" or build["platform"] != spec[1] or build["result"] != "Succeeded"
                or build["errors"] != 0 or build["nativeBuildReport"] != proof["nativeBuildReport"]
                or build["buildGuid"] != proof["buildGuid"] or not re.fullmatch("[0-9a-f]{32}", build["buildGuid"])):
            raise ValueError("Release build chain differs: " + name)
        native = pack.read_native(native_path, binary2text)
        if native["buildGUID"] != build["buildGuid"] or native["platformName"] != spec[2]:
            raise ValueError("Native release build identity differs: " + name)
        if proof.get("requiredArtPathGuids") != pack.required_art_inventory(root, native["packedBytesByAsset"]):
            raise ValueError("Release art inventory differs from native BuildReport: " + name)
        if proof.get("fontSizeReport") != pack.fw09_size_report(root, platform, native, proof["bytes"]):
            raise ValueError("FW-09 measurements differ from native BuildReport: " + name)
        pack.verify_gate(root, gate, build)
        if platform == "windows":
            replay_index = replay.verify_index(root, pack.contained(root, replays_path), proof)
        assets.append({"name": name, "bytes": proof["bytes"], "sha256": proof["sha256"], "platform": platform,
                       "buildGuid": build["buildGuid"], "packageReceipt": {"path": ref, "sha256": pack.delivery.sha(root / ref)},
                       "buildReport": proof["buildReport"], "nativeBuildReport": proof["nativeBuildReport"]})
    plan = {"schema": "v11-release-plan-1", "tag": TAG, "title": TITLE, "branch": branch, "target": target,
            "sourceCommit": frozen, "identity": gate["identity"], "gateSha256": pack.delivery.sha(root / pack.GATE),
            "notes": replay.ref(root, notes), "w25Review": replay.ref(root, review), "windowsReplays": replay_index, "assets": assets}
    return plan, body


def plan_digest(plan):
    return pack.digest(json.dumps(plan, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode())


class HTTPSRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        if urllib.parse.urlsplit(newurl).scheme != "https":
            raise ValueError("Refusing non-HTTPS release redirect")
        return super().redirect_request(req, fp, code, msg, headers, newurl)


class GitHub:
    def __init__(self, secret):
        self.secret = secret
        self.opener = urllib.request.build_opener(HTTPSRedirect())

    def request(self, url, method="GET", data=None, size=None, accept="application/vnd.github+json"):
        if not (url.startswith(API + "/") or url.startswith(previous.UPLOADS + "/")):
            raise ValueError("Unexpected authenticated URL")
        req = urllib.request.Request(url, data=data, method=method)
        req.add_unredirected_header("Authorization", "Bearer " + self.secret)
        req.add_header("Accept", accept)
        req.add_header("User-Agent", "DicePrototype-release-publisher")
        req.add_header("X-GitHub-Api-Version", "2022-11-28")
        if data is not None:
            req.add_header("Content-Type", "application/json" if size is None else "application/zip")
        if size is not None:
            req.add_header("Content-Length", str(size))
        return self.opener.open(req, timeout=1800)

    def json(self, url, method="GET", value=None):
        data = json.dumps(value, ensure_ascii=False).encode() if value is not None else None
        with self.request(url, method, data) as response:
            return json.load(response)

    def find_release(self, tag):
        # GitHub can omit drafts from the by-tag endpoint; list authenticated releases so a partial upload resumes.
        for page in range(1, 100000):
            results = self.json(f"{API}/releases?per_page=100&page={page}")
            matches = [r for r in results if r["tag_name"] == tag]
            if len(matches) > 1:
                raise ValueError("Multiple releases claim the same tag")
            if matches:
                return matches[0]
            if len(results) < 100:
                return None
        raise ValueError("Incomplete release inventory")

    def upload(self, release_id, path, size):
        url = f"{previous.UPLOADS}/releases/{release_id}/assets?name={urllib.parse.quote(path.name)}"
        with path.open("rb") as stream, self.request(url, "POST", stream, size) as response:
            return json.load(response)

    def check_download(self, info, expected):
        if info["name"] != expected["name"] or info["size"] != expected["bytes"] or info["state"] != "uploaded":
            raise ValueError("Remote asset metadata mismatch: " + expected["name"])
        with self.request(f"{API}/releases/assets/{int(info['id'])}", accept="application/octet-stream") as stream:
            sha = hashlib.sha256()
            size = 0
            while chunk := stream.read(1024 * 1024):
                sha.update(chunk)
                size += len(chunk)
        if size != expected["bytes"] or sha.hexdigest() != expected["sha256"]:
            raise ValueError("Remote download differs: " + expected["name"])
        return sha.hexdigest()


def remote_refs(root, plan):
    branch_ref = "refs/heads/" + plan["branch"]
    tag_ref = "refs/tags/" + TAG
    lines = git(root, "ls-remote", "origin", branch_ref, tag_ref, tag_ref + "^{}").splitlines()
    refs = {ref: commit for commit, ref in (line.split() for line in lines)}
    if refs.get(branch_ref) != plan["target"]:
        raise ValueError("The remote branch does not point at the planned release target")
    existing = refs.get(tag_ref + "^{}", refs.get(tag_ref))
    if existing is not None and existing != plan["target"]:
        raise ValueError("Remote release tag targets a different commit")
    return existing


def execute(root, plan, notes, server):
    remote_tag = remote_refs(root, plan)
    settings = {"tag_name": TAG, "target_commitish": plan["target"], "name": TITLE, "body": notes, "prerelease": True}
    release = server.find_release(TAG)
    if release is None:
        release = server.json(API + "/releases", "POST", {**settings, "draft": True})
    release_id = int(release["id"])
    if (release.get("tag_name") != TAG or release.get("name") != TITLE or release.get("body") != notes
            or release.get("prerelease") is not True or (remote_tag is None and release.get("target_commitish") != plan["target"])):
        raise ValueError("Existing release differs from the prepared release plan")
    expected = {a["name"]: a for a in plan["assets"]}
    existing = {a["name"]: a for a in release["assets"]}
    if (len(existing) != len(release["assets"]) or set(existing) - set(expected)
            or (not release["draft"] and set(existing) != set(expected))):
        raise ValueError("Unexpected release asset inventory")
    verified = []
    for name, asset in expected.items():
        info = existing.get(name)
        if info is None:
            info = server.upload(release_id, root / "Releases" / pack.VERSION / name, asset["bytes"])
        sha = server.check_download(info, asset)
        verified.append({"name": name, "id": info["id"], "bytes": info["size"], "remoteDownloadSha256": sha})
    url = f"{API}/releases/{release_id}"
    verified_inventory = sorted((a["name"], a["id"], a["bytes"]) for a in verified)
    if release["draft"]:
        ready = server.json(url)
        if (ready.get("draft") is not True or ready.get("prerelease") is not True or ready.get("tag_name") != TAG
                or ready.get("name") != TITLE or ready.get("body") != notes
                or sorted((a["name"], a["id"], a["size"]) for a in ready["assets"]) != verified_inventory):
            raise ValueError("Draft changed before publication")
        server.json(url, "PATCH", {"draft": False, "prerelease": True})
    final = server.json(url)
    if (final.get("draft") is not False or final.get("prerelease") is not True or final.get("tag_name") != TAG
            or final.get("body") != notes or final.get("name") != TITLE or remote_refs(root, plan) != plan["target"]
            or sorted((a["name"], a["id"], a["size"]) for a in final["assets"]) !=
            verified_inventory):
        raise ValueError("Published tag, notes or asset set differs from the verified draft")
    return {"schema": "v11-release-1", "plan": plan, "planSha256": plan_digest(plan), "releaseId": release_id,
            "url": final["html_url"], "tag": TAG, "target": plan["target"], "assets": verified}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tag", choices=[TAG], default=TAG)
    parser.add_argument("--notes", required=True, help="Repository-relative release notes")
    parser.add_argument("--replays", default="evidence/v11/replays/final/index.json", help="R-05 index from replay_v11.py")
    parser.add_argument("--review", default="evidence/v11/review/final.md", help="Final W25 independent review report")
    parser.add_argument("--binary2text", default=pack.BINARY2TEXT, help="Unity binary2text for both frozen native reports")
    parser.add_argument("--expect-plan-sha256", help="Check against an earlier local dry run")
    parser.add_argument("--execute", action="store_true")
    args = parser.parse_args()
    plan, notes = plan_release(ROOT, args.notes, args.replays, args.review, args.binary2text)
    fingerprint = plan_digest(plan)
    if args.expect_plan_sha256 and args.expect_plan_sha256 != fingerprint:
        raise ValueError("The release plan changed since the dry run")
    if not args.execute:
        print(json.dumps({"dryRun": True, "planSha256": fingerprint, **plan}, indent=2, ensure_ascii=False))
        return
    path = ROOT / f"evidence/v11/delivery/github-release-{TAG}.json"
    if path.exists() and pack.delivery.read_json(path).get("planSha256") != fingerprint:
        raise ValueError("An existing release receipt belongs to a different plan")
    proof = execute(ROOT, plan, notes, GitHub(previous.token()))
    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(dir=path.parent) as temp:
        prepared = Path(temp) / path.name
        with prepared.open("w", encoding="utf-8") as stream:
            json.dump({**proof, "utc": datetime.now(timezone.utc).isoformat()}, stream, ensure_ascii=False, indent=2)
            stream.write("\n")
        try:
            os.link(prepared, path)
        except FileExistsError:
            previous_proof = pack.delivery.read_json(path)
            previous_proof.pop("utc", None)
            if previous_proof != proof:
                raise ValueError("An existing release receipt differs from the verified release")
    print(json.dumps({"tag": TAG, "url": proof["url"], "planSha256": fingerprint}, ensure_ascii=False))


if __name__ == "__main__":
    main()
