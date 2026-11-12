"""Create the v0.72 GitHub pre-release and upload its packaged assets.

Credentials come from Git's configured helper and never enter files or output. The script only
publishes what package_v072 recorded: each asset must match its proof, the tag must point at a
commit that is already on the remote, and every upload is read back (size) and re-downloaded (SHA).
Dry run by default; pass --execute to touch the network.
"""
import argparse
import hashlib
import json
import subprocess
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REMOTE = "https://github.com/XDwudi/game2.git"
API = "https://api.github.com/repos/XDwudi/game2"
UPLOADS = "https://uploads.github.com/repos/XDwudi/game2"
RELEASE = ROOT / "Releases/p0.7.2"
PROOFS = ROOT / "evidence/v072/packages"
EVIDENCE = ROOT / "evidence/v072/delivery"


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT, text=True).strip()


def sha_bytes(data):
    return hashlib.sha256(data).hexdigest()


def token():
    auth = subprocess.run(["git", "credential", "fill"], cwd=ROOT, input="protocol=https\nhost=github.com\n\n", text=True, capture_output=True)
    if auth.returncode != 0:
        raise ValueError("Git credential helper failed")
    fields = dict(line.split("=", 1) for line in auth.stdout.splitlines() if "=" in line)
    if not fields.get("password"):
        raise ValueError("Git credential helper supplied no token")
    return fields["password"]


def call(method, url, secret, body=None, content_type="application/json", accept="application/vnd.github+json"):
    request = urllib.request.Request(url, data=body, method=method)
    request.add_header("Authorization", "Bearer " + secret)
    request.add_header("Accept", accept)
    request.add_header("X-GitHub-Api-Version", "2022-11-28")
    request.add_header("User-Agent", "release-publisher")
    if body is not None:
        request.add_header("Content-Type", content_type)
    try:
        with urllib.request.urlopen(request, timeout=1800) as response:
            return response.status, response.read()
    except urllib.error.HTTPError as error:
        return error.code, error.read()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--tag", required=True)
    parser.add_argument("--notes", required=True, help="Markdown file with the release body")
    parser.add_argument("--execute", action="store_true")
    args = parser.parse_args()
    if not args.tag.startswith("p0.7.2-rc."):
        raise ValueError("Tag must be p0.7.2-rc.N")
    if git("remote", "get-url", "origin") != REMOTE:
        raise ValueError("origin is not the expected repository")
    proofs = sorted(PROOFS.glob("*.zip.json"))
    if not proofs:
        raise ValueError("No package proofs found")
    assets = []
    for proof_path in proofs:
        proof = json.loads(proof_path.read_text(encoding="utf-8"))
        package = RELEASE / proof["package"]
        data = package.read_bytes()
        if len(data) != proof["bytes"] or sha_bytes(data) != proof["sha256"]:
            raise ValueError("Package differs from its proof: " + proof["package"])
        assets.append({"name": proof["package"], "bytes": len(data), "sha256": proof["sha256"], "sourceCommit": proof["identity"]["sourceCommit"]})
    commits = {a["sourceCommit"] for a in assets}
    if len(commits) != 1:
        raise ValueError("Packages were built from different commits")
    target = git("rev-parse", "HEAD")
    if subprocess.run(["git", "merge-base", "--is-ancestor", commits.pop(), target], cwd=ROOT).returncode != 0:
        raise ValueError("Built source commit is not contained in HEAD")
    remote_heads = git("ls-remote", "origin").split()
    if target not in remote_heads:
        raise ValueError("HEAD is not on the remote yet; push the branch first")
    notes = (ROOT / args.notes).read_text(encoding="utf-8")
    plan = {"tag": args.tag, "target": target, "assets": assets}
    if not args.execute:
        print(json.dumps({"dryRun": True, **plan}, ensure_ascii=False, indent=2))
        return
    secret = token()
    status, body = call("GET", f"{API}/releases/tags/{args.tag}", secret)
    if status == 200:
        raise ValueError("Release tag already exists; choose the next rc number")
    status, body = call("POST", f"{API}/releases", secret, json.dumps({
        "tag_name": args.tag, "target_commitish": target, "name": "骰子构筑 P0 v0.72 高清简约美术测试版 (" + args.tag + ")",
        "body": notes, "draft": True, "prerelease": True}).encode("utf-8"))
    if status != 201:
        raise ValueError(f"Create release failed: HTTP {status} {body[:300]!r}")
    release = json.loads(body)
    uploaded = []
    for asset in assets:
        data = (RELEASE / asset["name"]).read_bytes()
        status, body = call("POST", f"{UPLOADS}/releases/{release['id']}/assets?name={asset['name']}", secret, data, "application/zip")
        if status != 201:
            raise ValueError(f"Upload failed for {asset['name']}: HTTP {status} {body[:300]!r}")
        info = json.loads(body)
        if info["size"] != asset["bytes"]:
            raise ValueError("Uploaded size mismatch: " + asset["name"])
        uploaded.append({**asset, "id": info["id"], "apiUrl": info["url"]})
    status, body = call("PATCH", f"{API}/releases/{release['id']}", secret, json.dumps({"tag_name": args.tag, "target_commitish": target, "draft": False, "prerelease": True}).encode("utf-8"))
    if status != 200:
        raise ValueError(f"Publish failed: HTTP {status} {body[:300]!r}")
    published = json.loads(body)
    for asset in uploaded:
        status, data = call("GET", asset["apiUrl"], secret, accept="application/octet-stream")
        if status != 200 or len(data) != asset["bytes"] or sha_bytes(data) != asset["sha256"]:
            raise ValueError("Remote download does not match the local package: " + asset["name"])
        asset["remoteDownloadSha256"] = sha_bytes(data)
        asset["downloadUrl"] = f"https://github.com/XDwudi/game2/releases/download/{published['tag_name']}/{asset['name']}"
        del asset["apiUrl"]
    EVIDENCE.mkdir(parents=True, exist_ok=True)
    receipt = {"schema": "v072-release-1", "utc": datetime.now(timezone.utc).isoformat(), "releaseId": published["id"], "tag": published["tag_name"],
               "target": target, "url": published["html_url"], "draft": published["draft"], "prerelease": published["prerelease"], "assets": uploaded}
    # One receipt per tag: an earlier release candidate's record is never overwritten.
    (EVIDENCE / f"github-release-{published['tag_name']}.json").write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(receipt, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
