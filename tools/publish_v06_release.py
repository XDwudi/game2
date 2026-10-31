"""Publish the three frozen v0.6 packages using the user's approved Release channel.

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
from package_v06 import (ROOT, RELEASE, TAG, GATE, read_json, sha, validated_gate, check_package, local_path,
                         validate_windows_report, candidate_directory, release_manifest_path)
from macos.v06.package_checks import safe_archive_inventory
from macos.v06.compare_replays import review_reference_notice, failed_performance_notice

API = "https://api.github.com/repos/XDwudi/game2"
EVIDENCE = ROOT / "evidence/v06/delivery/github-release"
EXPECTED_NAMES = {"DicePrototype-p0.6.0-win-x64.zip", "DicePrototype-p0.6.0-macos-universal.zip", "DicePrototype-p0.6.0-MacVerificationKit.zip"}


def release_inputs(review_candidate=False, allow_failed_windows_performance=False):
    if allow_failed_windows_performance and not review_candidate:
        raise ValueError("--allow-failed-windows-performance requires --review-candidate")
    identity, _, tests = validated_gate()
    manifest = read_json(release_manifest_path(review_candidate))
    schema = "v06-diagnostic-review-candidate-1" if allow_failed_windows_performance else "v06-restricted-review-candidate-1" if review_candidate else "v06-release-candidate-1"
    if (manifest.get("schema") != schema or manifest.get("releaseTag") != TAG
            or manifest.get("reviewCandidate", False) is not review_candidate
            or manifest.get("allowFailedWindowsPerformance", False) is not allow_failed_windows_performance
            or any(manifest.get(key) != value for key, value in identity.items()) or manifest.get("tests") != tests
            or manifest.get("buildGate") != {"path": GATE, "sha256": sha(ROOT / GATE)}):
        raise ValueError("Manifest is stale or belongs to another tested source")
    items = manifest["platforms"] + [manifest["macVerificationKit"]]
    expected = {Path(item["packagePath"]).name: item for item in items}
    if len(items) != 3 or set(expected) != EXPECTED_NAMES:
        raise ValueError("Only the three authorized v0.6 ZIP attachments are allowed")
    for name, item in expected.items():
        if item["packagePath"] != candidate_directory(review_candidate) + "/" + name:
            raise ValueError("Unexpected package path")
        check_package(item)
    for row in manifest.get("windowsSeparateProcessDisplayRecovery", {}).get("files", []):
        if sha(local_path(row["path"])) != row["sha256"]:
            raise ValueError("Separate-process display recovery evidence changed")
    recovery_status = manifest.get("windowsSeparateProcessDisplayRecovery", {}).get("status")
    if recovery_status != "passed" and not (review_candidate and recovery_status == "not_executed_native_display_pending"):
        raise ValueError("Windows separate-process display recovery was not verified")
    if sha(local_path(manifest["windowsEvidence"]) / "player-report.json") != manifest["windowsReportSha256"]:
        raise ValueError("Windows Player report differs from the frozen manifest")
    report = validate_windows_report(local_path(manifest["windowsEvidence"]), identity, review_candidate, allow_failed_windows_performance)
    expected_status = "DIAGNOSTIC_TEST_CANDIDATE_WINDOWS_PERFORMANCE_FAILED_NATIVE_PENDING" if allow_failed_windows_performance else "RESTRICTED_TEST_CANDIDATE_WINDOWS_NATIVE_PENDING"
    if review_candidate and (manifest.get("fullReleaseAccepted") is not False
            or manifest.get("releaseStatus") != expected_status
            or manifest.get("windowsReference", {}).get("fullWindowsPassed") is not False
            or manifest.get("windowsReference", {}).get("windowsNativeDisplayStatus") != "PENDING_P22_NOT_PASSED"):
        raise ValueError("Review manifest does not clearly preserve its Windows native limitation")
    if allow_failed_windows_performance and (manifest.get("windowsReference") != review_reference_notice(report, True)
            or manifest.get("diagnosticSummary") != review_reference_notice(report, True)
            or manifest.get("windowsPerformance") != {**failed_performance_notice(report), "originalPerformanceReport": report["performance"]}):
        raise ValueError("Diagnostic manifest must retain the exact FAILED performance and focus/GPU observations")
    tag = subprocess.run(["git", "rev-parse", "--verify", "refs/tags/" + TAG + "^{commit}"], cwd=ROOT, text=True, capture_output=True)
    if tag.returncode == 0 and tag.stdout.strip() != identity["sourceCommit"]:
        raise ValueError("Existing local release tag points to different source")
    return manifest, expected, identity["sourceCommit"]


def release_body(manifest):
    tests, perf = manifest["tests"], manifest["windowsPerformance"]
    hashes = "\n".join("- `" + Path(item["packagePath"]).name + "` — " + str(item["byteSize"]) + " bytes / SHA-256 `" + item["sha256"] + "`"
                       for item in manifest["platforms"] + [manifest["macVerificationKit"]])
    restriction = "**限制版测试候选：Windows 原生显示 P22 尚未通过，完整 Windows / 整版本验收未完成。以下 Windows 结果仅覆盖玩法、UI、轨迹与原门槛性能；不把 partial 报告改称 passed。按已授权渠道提供人工测试包；Mac 仍须完整严格实机验收，整个版本未 ACCEPTED。**\n\n" if manifest.get("reviewCandidate") else ""
    if manifest.get("allowFailedWindowsPerformance"):
        restriction = (f"**诊断测试候选：Windows 性能 FAILED，P95 {perf['p95FrameIntervalMs']} ms > 16.7 ms，"
                       f"采样 {perf['sampleSeconds']} 秒，焦点帧 {perf['focusedSampleFrames']}，异常 GPU 样本 {perf['gpuInvalidSamples']}。"
                       "原生独占 P22 未通过，Mac 实机未测，整个版本未 ACCEPTED。Windows 仅作玩法/UI/轨迹对照，原始失败报告保留；Mac 自身性能、原生显示和采样完整性仍须严格验收。**\n\n")
    return restriction + f"""P0 v0.6 原型测试版，三个完整 ZIP 通过 GitHub 发行附件提供；无需 Git LFS。

- Windows x64：完整解压后运行 `DicePrototype.exe`。真实 Unity EditMode {tests['unityEditMode']['passed']} 项、PlayMode {tests['unityPlayMode']['passed']} 项通过；记录设备压力采样 {perf['sampleSeconds']} 秒，LateUpdate 帧间隔 P95 {perf['p95FrameIntervalMs']} ms（门槛 16.7 ms）。
- macOS Universal：含 arm64 与 x86_64，是 Windows 交叉构建传输候选。**M5 原生运行、Metal、ditto 封装、签名状态与双向存档仍待实机验证**；Intel 运行未测。
- M5 验收工具包：含同版 Mac 包、正常及 debug24 输入轨迹、Windows 真实对照结果与检查点。解压后按“执行说明.md”运行 `VerifyMac.command` 并回传证据。

源码：`{manifest['sourceCommit']}`；配置 p0.6.0 / schema4；50 控制器、24 关、独立颜色乘区、商店刷新、控制器拖拽、显示设置与独立调试工作区。设置中输入 `XD is god` 进入调试；旧版 Run 保留并隔离。
正常轨迹结果为 {manifest['ruleReproduction']['normalOutcome']}（第 {manifest['ruleReproduction']['normalFinalStage']} 关），debug24 仅验证流程，不代表正常平衡。
这是预发布测试包；Mac、物理输入/显示环境与真人体验仍待验收。

{hashes}
"""


def ensure_publish_allowed(manifest, action, allow_review_candidate=False, allow_failed_windows_performance=False):
    if bool(manifest.get("allowFailedWindowsPerformance")) != allow_failed_windows_performance:
        raise ValueError("Diagnostic manifest requires explicit --allow-failed-windows-performance for every action")
    if allow_failed_windows_performance and not manifest.get("reviewCandidate"):
        raise ValueError("Failed-performance reference is only valid for a review candidate")
    if manifest.get("reviewCandidate") and action == "publish" and not allow_review_candidate:
        raise ValueError("Restricted candidate publishing requires --review-candidate and --allow-review-candidate to explicitly select the limited technical scope; this does not waive P22 or full acceptance")
    if allow_review_candidate and (not manifest.get("reviewCandidate") or action != "publish"):
        raise ValueError("--allow-review-candidate is only valid for publishing the explicitly selected restricted manifest")


def release_evidence(review_candidate=False):
    return EVIDENCE / "review-candidate" if review_candidate else EVIDENCE


def record(name, value, review_candidate=False):
    directory = release_evidence(review_candidate)
    directory.mkdir(parents=True, exist_ok=True)
    with (directory / name).open("x", encoding="utf-8", newline="\n") as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)
        stream.write("\n")


class SafeRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        if urllib.parse.urlparse(newurl).scheme != "https":
            raise ValueError("Refusing non-HTTPS redirect")
        redirected = super().redirect_request(req, fp, code, msg, headers, newurl)
        if redirected is None:
            return None
        before, after = urllib.parse.urlparse(req.full_url), urllib.parse.urlparse(newurl)
        if (before.hostname, before.port or 443) != (after.hostname, after.port or 443):
            redirected.remove_header("Authorization")
        return redirected


def authenticated_url(url):
    parts = urllib.parse.urlparse(url)
    if (parts.scheme != "https" or parts.hostname not in ("api.github.com", "uploads.github.com")
            or parts.port not in (None, 443) or parts.username or parts.password
            or not parts.path.startswith("/repos/XDwudi/game2/")):
        raise ValueError("Unexpected authenticated request URL")


def remote_tag_target(request):
    try:
        ref = request(API + "/git/ref/tags/" + urllib.parse.quote(TAG, safe=""))
    except urllib.error.HTTPError as error:
        if error.code == 404:
            return None
        raise
    obj = ref["object"]
    for _ in range(8):
        if obj["type"] == "commit":
            return obj["sha"]
        if obj["type"] != "tag":
            break
        obj = request(API + "/git/tags/" + obj["sha"])["object"]
    raise ValueError("Release tag does not resolve to a commit")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("action", choices=["draft", "upload", "download", "publish", "inspect"])
    parser.add_argument("--name")
    parser.add_argument("--review-candidate", action="store_true", help="Select the separate restricted test-candidate manifest")
    parser.add_argument("--allow-review-candidate", action="store_true", help="Explicitly publish the restricted test candidate in the authorized channel; does not waive native-display/full-acceptance requirements")
    parser.add_argument("--allow-failed-windows-performance", action="store_true", help="Explicit diagnostic candidate with Windows performance FAILED; required in addition to both review switches for publish")
    args = parser.parse_args()
    remote = subprocess.check_output(["git", "remote", "get-url", "origin"], cwd=ROOT, text=True).strip()
    if remote != "https://github.com/XDwudi/game2.git":
        raise ValueError("Configured remote differs from user-authorized repository")
    manifest, expected, target_commit = release_inputs(args.review_candidate, args.allow_failed_windows_performance)
    # Reject before consulting credentials or making any network request.
    ensure_publish_allowed(manifest, args.action, args.allow_review_candidate, args.allow_failed_windows_performance)
    body = release_body(manifest)
    auth = subprocess.run(["git", "credential", "fill"], cwd=ROOT,
                          input="protocol=https\nhost=github.com\n\n", text=True, capture_output=True)
    if auth.returncode:
        raise ValueError("Git credential helper failed")
    credential = dict(line.split("=", 1) for line in auth.stdout.splitlines() if "=" in line)
    token = credential.get("password")
    if not token:
        raise ValueError("Git credential helper supplied no password/token")
    opener = urllib.request.build_opener(SafeRedirect())

    def request(url, method="GET", payload=None, data=None, content_type=None):
        authenticated_url(url)
        headers = {"Authorization": "Bearer " + token, "Accept": "application/vnd.github+json",
                   "User-Agent": "game2-v06-delivery", "X-GitHub-Api-Version": "2026-03-10"}
        if payload is not None:
            data = json.dumps(payload, ensure_ascii=False).encode("utf-8")
            headers["Content-Type"] = "application/json"
        if content_type:
            headers["Content-Type"] = content_type
            headers["Content-Length"] = str(data.seek(0, 2))
            data.seek(0)
        with opener.open(urllib.request.Request(url, data=data, headers=headers, method=method), timeout=180) as response:
            return json.load(response)

    resolved_tag = remote_tag_target(request)
    if resolved_tag is not None and resolved_tag != target_commit:
        raise ValueError("Existing remote release tag points to different source")
    releases = request(API + "/releases?per_page=100")
    matches = [r for r in releases if r["tag_name"] == TAG]
    if len(matches) > 1:
        raise ValueError("Ambiguous release")
    release = matches[0] if matches else None
    if args.action == "draft":
        if release is None:
            release = request(API + "/releases", "POST", {
                "tag_name": TAG, "target_commitish": target_commit,
                "name": "骰子构筑 P0 v0.6 — " + ("性能失败及原生显示待解决的诊断候选" if args.allow_failed_windows_performance else "原生显示待解决的限制版候选" if args.review_candidate else "Windows / macOS 测试版"),
                "body": body, "draft": True, "prerelease": True, "make_latest": "false"})
    if release is None:
        raise ValueError("Create draft before uploading")
    if resolved_tag is None and release.get("target_commitish") != target_commit:
        raise ValueError("Draft release target differs from the tested Git commit")
    if args.action in ("upload", "publish") and release.get("draft") is not True:
        raise ValueError("Refusing to mutate an already published release")
    assets = {a["name"]: a for a in release["assets"]}
    if len(assets) != len(release["assets"]) or set(assets) - EXPECTED_NAMES:
        raise ValueError("Unexpected/duplicate release assets; no existing asset will be removed")
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
        record(args.name + ".upload.json", asset, args.review_candidate)
        print(json.dumps({"uploaded": asset["name"], "size": asset["size"], "digest": asset["digest"]}), flush=True)
    if args.action == "download":
        if args.name not in expected or args.name not in assets:
            raise ValueError("Requested asset missing")
        item, asset = expected[args.name], assets[args.name]
        directory = ROOT / ("artifacts/cache/v06-release-review-download" if args.review_candidate else "artifacts/cache/v06-release-download")
        directory.mkdir(parents=True, exist_ok=True)
        target = directory / args.name
        if target.exists():
            raise ValueError("Download target already exists; refusing to reuse local bytes as network evidence")
        headers = {"Authorization": "Bearer " + token, "Accept": "application/octet-stream",
                   "User-Agent": "game2-v06-delivery", "X-GitHub-Api-Version": "2026-03-10"}
        authenticated_url(asset["url"])
        with opener.open(urllib.request.Request(asset["url"], headers=headers), timeout=180) as response:
            http_status = response.status
            final_host = urllib.parse.urlparse(response.geturl()).hostname
            if response.status != 200 or urllib.parse.urlparse(response.geturl()).scheme != "https":
                raise ValueError("Asset download did not return a successful HTTPS response")
            with target.open("xb") as stream:
                received = 0
                for block in iter(lambda: response.read(1024 * 1024), b""):
                    received += len(block)
                    if received > item["byteSize"]:
                        raise ValueError("Download exceeds frozen asset size")
                    stream.write(block)
        actual = sha(target)
        if actual != item["sha256"] or target.stat().st_size != item["byteSize"]:
            raise ValueError("Downloaded bytes differ from frozen manifest")
        with zipfile.ZipFile(target) as archive:
            if archive.testzip() is not None:
                raise ValueError("Downloaded ZIP CRC failed")
            count = len(archive.infolist())
        safe_archive_inventory(target)
        result = {"utc": datetime.now(timezone.utc).isoformat(), "assetId": asset["id"],
                  "name": args.name, "downloadUrl": asset["browser_download_url"],
                  "httpStatus": http_status, "finalHost": final_host, "path": str(target),
                  "sha256": actual, "byteSize": target.stat().st_size, "zipEntries": count,
                  "crc": "passed", "matchesFrozenManifest": True, "sourceCommit": target_commit,
                  "manifestSha256": sha(release_manifest_path(args.review_candidate)), "reviewCandidate": args.review_candidate}
        record(args.name + ".download.json", result, args.review_candidate)
        print(json.dumps(result, ensure_ascii=False), flush=True)
    if args.action == "publish":
        for name, item in expected.items():
            asset = assets.get(name)
            proof_path = release_evidence(args.review_candidate) / (name + ".download.json")
            if not asset or asset["state"] != "uploaded" or asset["digest"] != "sha256:" + item["sha256"] or asset["size"] != item["byteSize"]:
                raise ValueError("All three uploaded assets must match before publishing")
            proof = json.loads(proof_path.read_text(encoding="utf-8"))
            if (proof["assetId"] != asset["id"] or proof["sha256"] != item["sha256"] or proof["byteSize"] != item["byteSize"]
                    or proof.get("httpStatus") != 200 or proof.get("crc") != "passed" or proof.get("sourceCommit") != target_commit
                    or proof.get("manifestSha256") != sha(release_manifest_path(args.review_candidate))
                    or proof.get("matchesFrozenManifest") is not True or sha(local_path(proof["path"])) != item["sha256"]):
                raise ValueError("Fresh download proof missing or mismatched")
        release = request(API + "/releases/" + str(release["id"]), "PATCH",
                          {"draft": False, "prerelease": True, "make_latest": "false", "body": body})
        if remote_tag_target(request) != target_commit:
            raise ValueError("Published release tag differs from frozen source; retain failure for review")
    release = request(API + "/releases/" + str(release["id"]))
    record("release-" + args.action + "-" + datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S-%f") + ".json", release, args.review_candidate)
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
