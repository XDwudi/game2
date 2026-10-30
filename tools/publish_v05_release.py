"""Publish the three frozen v0.5 packages using the user's approved Release channel.

Credentials come from Git's configured helper and never enter files or output.
Existing assets are never deleted or overwritten. Downloads strip authentication
on cross-host redirects and are verified independently against the frozen manifest.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import subprocess
import urllib.error
import urllib.parse
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
API = "https://api.github.com/repos/XDwudi/game2"
TAG = "p0.5.0-rc.1"
TARGET = "3d307c610878b57b81e7a619c3909a6196cb747b"
EVIDENCE = ROOT / "evidence/v05/delivery/github-release"
BODY = """P0 v0.5 原型测试版，三个完整 ZIP 同步提供。用户已明确同意使用 GitHub 发行附件替代额度耗尽的 Git LFS。

- **Windows x64**：完整解压后运行 `DicePrototype.exe`。554 项 EditMode、24 项 PlayMode 通过；指定 Windows 设备 180 秒压力 P95 为 16.6913 ms。
- **macOS Universal**：包含 arm64 与 x86_64；这是交叉构建候选，**M5 实机、Metal、原生封装与跨平台存档尚未验收**，Intel 运行也未验证。
- **M5 验收工具包**：已包含同版 Mac 包、Windows 真实检查点和对照轨迹。复制到 Mac 并解压，按“执行说明.md”运行 `VerifyMac.command`，回传生成的证据与最终 Mac ZIP。

游戏源码：`967f309a6be402612514714f46f3743421267799`。
配置 p0.5.0 / 存档 schema 3；24 关、40 控制器、附魔与牌型手册。
这是预发布测试包，整版本仍待 Mac 和真人验收。

[交付说明](https://github.com/XDwudi/game2/blob/main/docs/DELIVERY_V05.md) · [包大小与 SHA-256](https://github.com/XDwudi/game2/blob/main/Releases/p0.5.0/release-manifest.json)
"""


def sha(path):
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def record(name, value):
    EVIDENCE.mkdir(parents=True, exist_ok=True)
    (EVIDENCE / name).write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


class SafeRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        if urllib.parse.urlparse(newurl).scheme != "https":
            raise ValueError("Refusing non-HTTPS redirect")
        redirected = super().redirect_request(req, fp, code, msg, headers, newurl)
        if urllib.parse.urlparse(req.full_url).hostname != urllib.parse.urlparse(newurl).hostname:
            redirected.remove_header("Authorization")
        return redirected


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("action", choices=["draft", "upload", "download", "publish", "inspect"])
    parser.add_argument("--name")
    args = parser.parse_args()
    remote = subprocess.check_output(["git", "remote", "get-url", "origin"], cwd=ROOT, text=True).strip()
    if remote != "https://github.com/XDwudi/game2.git":
        raise ValueError("Configured remote differs from user-authorized repository")
    manifest = json.loads((ROOT / "Releases/p0.5.0/release-manifest.json").read_text(encoding="utf-8"))
    expected = {Path(x["packagePath"]).name: x for x in manifest["platforms"] + [manifest["macVerificationKit"]]}
    auth = subprocess.run(["git", "credential", "fill"], cwd=ROOT,
                          input="protocol=https\nhost=github.com\n\n", text=True, capture_output=True)
    if auth.returncode:
        raise ValueError("Git credential helper failed")
    credential = dict(line.split("=", 1) for line in auth.stdout.splitlines() if "=" in line)
    token = credential["password"]
    opener = urllib.request.build_opener(SafeRedirect())

    def request(url, method="GET", payload=None, data=None, content_type=None):
        host = urllib.parse.urlparse(url).hostname
        if host not in ("api.github.com", "uploads.github.com"):
            raise ValueError("Unexpected authenticated request host")
        headers = {"Authorization": "Bearer " + token, "Accept": "application/vnd.github+json",
                   "User-Agent": "game2-v05-delivery", "X-GitHub-Api-Version": "2026-03-10"}
        if payload is not None:
            data = json.dumps(payload, ensure_ascii=False).encode("utf-8")
            headers["Content-Type"] = "application/json"
        if content_type:
            headers["Content-Type"] = content_type
            headers["Content-Length"] = str(data.seek(0, 2))
            data.seek(0)
        with opener.open(urllib.request.Request(url, data=data, headers=headers, method=method), timeout=180) as response:
            return json.load(response)

    releases = request(API + "/releases?per_page=100")
    matches = [r for r in releases if r["tag_name"] == TAG]
    if len(matches) > 1:
        raise ValueError("Ambiguous release")
    release = matches[0] if matches else None
    if args.action == "draft":
        if release is None:
            release = request(API + "/releases", "POST", {
                "tag_name": TAG, "target_commitish": TARGET,
                "name": "骰子构筑 P0 v0.5 — Windows / macOS 测试版",
                "body": BODY, "draft": True, "prerelease": True, "make_latest": "false"})
    if release is None:
        raise ValueError("Create draft before uploading")
    assets = {a["name"]: a for a in release["assets"]}
    if args.action == "upload":
        if args.name not in expected:
            raise ValueError("Only the three authorized frozen ZIPs may be uploaded")
        item = expected[args.name]
        path = ROOT / item["packagePath"]
        if sha(path) != item["sha256"] or path.stat().st_size != item["byteSize"]:
            raise ValueError("Local package differs from frozen manifest")
        asset = assets.get(args.name)
        if asset is None:
            base = release["upload_url"].split("{", 1)[0]
            with path.open("rb") as stream:
                asset = request(base + "?" + urllib.parse.urlencode({"name": args.name}),
                                "POST", data=stream, content_type="application/zip")
        if (asset["state"] != "uploaded" or asset["size"] != item["byteSize"]
                or asset.get("digest") != "sha256:" + item["sha256"]):
            raise ValueError("Server asset size/state/digest mismatch; existing asset was not overwritten")
        record(args.name + ".upload.json", asset)
        print(json.dumps({"uploaded": asset["name"], "size": asset["size"], "digest": asset["digest"]}), flush=True)
    if args.action == "download":
        if args.name not in expected or args.name not in assets:
            raise ValueError("Requested asset missing")
        item, asset = expected[args.name], assets[args.name]
        directory = ROOT / "artifacts/cache/v05-release-download"
        directory.mkdir(parents=True, exist_ok=True)
        target = directory / args.name
        if target.exists():
            raise ValueError("Download target already exists; refusing to reuse local bytes as network evidence")
        headers = {"Authorization": "Bearer " + token, "Accept": "application/octet-stream",
                   "User-Agent": "game2-v05-delivery", "X-GitHub-Api-Version": "2026-03-10"}
        with opener.open(urllib.request.Request(asset["url"], headers=headers), timeout=180) as response:
            http_status = response.status
            final_host = urllib.parse.urlparse(response.geturl()).hostname
            with target.open("xb") as stream:
                for block in iter(lambda: response.read(1024 * 1024), b""):
                    stream.write(block)
        actual = sha(target)
        if actual != item["sha256"] or target.stat().st_size != item["byteSize"]:
            raise ValueError("Downloaded bytes differ from frozen manifest")
        with zipfile.ZipFile(target) as archive:
            if archive.testzip() is not None:
                raise ValueError("Downloaded ZIP CRC failed")
            count = len(archive.infolist())
        result = {"utc": datetime.now(timezone.utc).isoformat(), "assetId": asset["id"],
                  "name": args.name, "downloadUrl": asset["browser_download_url"],
                  "httpStatus": http_status, "finalHost": final_host, "path": str(target),
                  "sha256": actual, "byteSize": target.stat().st_size, "zipEntries": count,
                  "crc": "passed", "matchesFrozenManifest": True}
        record(args.name + ".download.json", result)
        print(json.dumps(result, ensure_ascii=False), flush=True)
    if args.action == "publish":
        for name, item in expected.items():
            asset = assets.get(name)
            proof_path = EVIDENCE / (name + ".download.json")
            if not asset or asset["state"] != "uploaded" or asset["digest"] != "sha256:" + item["sha256"]:
                raise ValueError("All three uploaded assets must match before publishing")
            proof = json.loads(proof_path.read_text(encoding="utf-8"))
            if proof["assetId"] != asset["id"] or proof["sha256"] != item["sha256"]:
                raise ValueError("Fresh download proof missing or mismatched")
        release = request(API + "/releases/" + str(release["id"]), "PATCH",
                          {"draft": False, "prerelease": True, "make_latest": "false", "body": BODY})
    release = request(API + "/releases/" + str(release["id"]))
    record("release-" + args.action + ".json", release)
    print(json.dumps({"releaseId": release["id"], "tag": release["tag_name"],
                      "draft": release["draft"], "prerelease": release["prerelease"],
                      "url": release["html_url"], "assets": [{k: a[k] for k in
                          ("id", "name", "state", "size", "digest", "browser_download_url")} for a in release["assets"]]},
                     ensure_ascii=False), flush=True)


if __name__ == "__main__":
    try:
        main()
    except urllib.error.HTTPError as error:
        raise SystemExit("GitHub HTTP error " + str(error.code))
    except urllib.error.URLError as error:
        raise SystemExit("Network request failed: " + type(error.reason).__name__)
