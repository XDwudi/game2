"""Publish the three frozen v0.71 packages using the user's approved Release channel.

Credentials come from Git's configured helper and never enter files or output.
Existing assets are never deleted or overwritten. Downloads strip authentication
on cross-host redirects and are verified independently against the frozen manifest.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import re
from pathlib import Path
import subprocess
import urllib.error
import urllib.parse
import urllib.request
import zipfile
from package_v071 import ROOT, TAG, read_json, sha, local_path, validate_release_tag
from release_v071_manifest import build_manifest, EXPECTED_NAMES, PACKAGE_DIRECTORY
from publish_v07_release import SafeRedirect, authenticated_url
from macos.v071.package_checks import safe_archive_inventory

API = "https://api.github.com/repos/XDwudi/game2"
EVIDENCE = ROOT / "evidence/v071-full/delivery/github-release"
DEFAULT_MANIFEST = PACKAGE_DIRECTORY + "/full-art-release-manifest.json"


def release_anchor(source_commit, existing_anchor=None):
    if existing_anchor is not None and not re.fullmatch(r"[0-9a-f]{40}", existing_anchor):
        raise ValueError("Existing release anchor must be an exact commit SHA")
    return existing_anchor or source_commit


def require_existing_anchor(request, anchor):
    release_anchor("", anchor)
    existing = request(API + "/commits/" + anchor)
    if existing.get("sha") != anchor:
        raise ValueError("Binary-only release anchor is not an existing remote commit")


def release_inputs(manifest_path, review_candidate=False, allow_failed_windows_performance=False, existing_anchor=None, release_tag=TAG):
    validate_release_tag(release_tag)
    manifest_path = local_path(manifest_path)
    manifest = read_json(manifest_path)
    if manifest.get("releaseTag") != release_tag:
        raise ValueError("Manifest release tag differs from explicit --release-tag")
    if (manifest.get("reviewCandidate", False) is not review_candidate
            or manifest.get("allowFailedWindowsPerformance", False) is not allow_failed_windows_performance):
        raise ValueError("Manifest switches differ from the explicit release scope")
    inputs = manifest["manifestInputs"]
    current = build_manifest(local_path(inputs["windowsResults"]), local_path(inputs["windowsBuild"]),
        local_path(inputs["macosBuild"]), local_path(inputs["artReviewEvidence"]),
        local_path(inputs["displayRecovery"]) if inputs.get("displayRecovery") else None,
        review_candidate, allow_failed_windows_performance, inputs["packageDirectory"], inputs["packageProofDirectory"], release_tag)
    if current != manifest:
        raise ValueError("Full-art manifest is stale or belongs to another tested source")
    items = manifest["platforms"] + [manifest["macVerificationKit"]]
    expected = {Path(item["packagePath"]).name: item for item in items}
    if len(items) != 3 or set(expected) != EXPECTED_NAMES:
        raise ValueError("Only the three authorized v0.71 full-art ZIP attachments are allowed")
    tag = subprocess.run(["git", "rev-parse", "--verify", "refs/tags/" + release_tag + "^{commit}"], cwd=ROOT, text=True, capture_output=True)
    if tag.returncode == 0 and tag.stdout.strip() != release_anchor(manifest["sourceCommit"], existing_anchor):
        raise ValueError("Existing local release tag points to a different release anchor")
    return manifest, expected, manifest["sourceCommit"]


def release_title(manifest):
    if manifest.get("allowFailedWindowsPerformance"):
        native = manifest.get("windowsReference", {}).get("windowsNativeDisplayStatus")
        if native == "AUTOMATED_CHECKS_PASSED_PHYSICAL_EVENTS_UNTESTED":
            label = "性能失败的诊断候选（自动显示检查通过，物理场景待验）"
        elif native == "PENDING_P22_NOT_PASSED":
            label = "性能失败及原生显示待解决的诊断候选"
        else:
            raise ValueError("Diagnostic release title requires the validated native display scope")
    else:
        label = "原生显示待解决的限制版候选" if manifest.get("reviewCandidate") else "Windows / macOS 测试版"
    edition = "Image2主界面与桌面更新" if manifest.get("menuTable") else "全量美术"
    return "骰子构筑 P0 v0.71 " + edition + " — " + label


def release_body(manifest, existing_anchor=None):
    tests, perf = manifest["tests"], manifest["windowsPerformance"]
    menu_update = "本版包含 Image2 主界面纸菜单/卡牌与真实骰子布局，以及木沿、深青毛毡桌面背景；旧 p0.7.1-rc.1 附件保持原样。\n\n" if manifest.get("menuTable") else ""
    hashes = "\n".join("- `" + Path(item["packagePath"]).name + "` — " + str(item["byteSize"]) + " bytes / SHA-256 `" + item["sha256"] + "`"
                       for item in manifest["platforms"] + [manifest["macVerificationKit"]])
    restriction = "**限制版测试候选：Windows 原生显示 P22 尚未通过，完整 Windows / 整版本验收未完成。以下 Windows 结果仅覆盖玩法、UI、轨迹与原门槛性能；不把 partial 报告改称 passed。按已授权渠道提供人工测试包；Mac 仍须完整严格实机验收，整个版本未 ACCEPTED。**\n\n" if manifest.get("reviewCandidate") else ""
    if manifest.get("allowFailedWindowsPerformance"):
        native = manifest.get("windowsReference", {}).get("windowsNativeDisplayStatus")
        native_text = "所选完整报告的自动原生显示检查通过，物理焦点/显示器变更未验；" if native == "AUTOMATED_CHECKS_PASSED_PHYSICAL_EVENTS_UNTESTED" else "原生独占 P22 未通过，"
        restriction = (f"**诊断测试候选：Windows 性能 FAILED，P95 {perf['p95FrameIntervalMs']} ms（原门槛 ≤16.7 ms），"
                       f"采样 {perf['sampleSeconds']} 秒，焦点帧 {perf['focusedSampleFrames']}，异常 GPU 样本 {perf['gpuInvalidSamples']}、缺失 {perf['gpuMissingSamples']}，覆盖状态 {perf['measurementCoverageStatus']}。"
                       + native_text + "Mac 实机未测，整个版本未 ACCEPTED。Windows 仅作玩法/UI/轨迹对照，原始失败报告保留；Mac 自身性能、原生显示和采样完整性仍须严格验收。**\n\n")
    if existing_anchor is not None:
        anchor = release_anchor(manifest["sourceCommit"], existing_anchor)
        restriction += (f"**本次仅发行三个成品附件，未推送本次游戏Git源码提交或单独上传开发证据目录。** Mac验证工具包含执行验证所需脚本与Windows对照数据。实际构建源是本地冻结 `{manifest['sourceCommit']}`，"
                        f"包内 BUILD_IDENTITY.json 与校验清单绑定该源。发行标签借用远端已存在的 `{anchor}` 作为存放附件的位置，"
                        "它不是本次构建源码；GitHub 自动生成的 Source code 下载也不是本版源码，请下载下列三个具名 ZIP。\n\n")
    return restriction + menu_update + f"""P0 v0.71 全量美术与界面原型测试版，三个完整 ZIP 通过 GitHub 发行附件提供；无需 Git LFS。

- Windows x64：完整解压后运行 `DicePrototype.exe`。真实 Unity EditMode {tests['unityEditMode']['passed']} 项、PlayMode {tests['unityPlayMode']['passed']} 项通过；记录设备压力采样 {perf['sampleSeconds']} 秒，LateUpdate 帧间隔 P95 {perf['p95FrameIntervalMs']} ms（门槛 16.7 ms）。
- macOS Universal：含 arm64 与 x86_64，是 Windows 交叉构建传输候选。**M5 原生运行、Metal、ditto 封装、签名状态与双向存档仍待实机验证**；Intel 运行未测。
- M5 验收工具包：含同版 Mac 包、正常及 debug24 输入轨迹、Windows 真实对照结果与检查点。解压后按“执行说明.md”运行 `VerifyMac.command` 并回传证据。

源码：`{manifest['sourceCommit']}`；美术 art0.7.1，规则 p0.6.0 / schema4。全部 71 项活跃内容插画、公共高清符号/附魔/特效、纸面、卡背、字标和桌布已接入；复用已接入并经技术复核的纸卡与陶瓷骰子母模型。50 控制器、24 关、7 道具、7 服务、7 强化等原规则保持。设置中输入 `XD is god` 进入调试；旧版 Run 保留并隔离。
正常轨迹结果为 {manifest['ruleReproduction']['normalOutcome']}（第 {manifest['ruleReproduction']['normalFinalStage']} 关），debug24 仅验证流程，不代表正常平衡。
这是预发布测试包；Mac、物理输入/显示环境与真人体验仍待验收。

已知未完成项：真实 AudioMixer 总线尚未接入，声音当前使用分类音量回退。正式音频路由与混音听审仍待完成；本包不代表整版美术、声音或体验已验收。

{hashes}

全量资源范围与独立技术复核已绑定到本版清单；图片生成或自动检查不代签人工风格/体验验收。
<!-- dicecraft-v071-full-art:{hashlib.sha256(json.dumps(manifest,sort_keys=True,separators=(',',':'),ensure_ascii=False).encode('utf-8')).hexdigest()} -->
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


def release_evidence(review_candidate=False, release_tag=TAG):
    validate_release_tag(release_tag)
    directory = EVIDENCE if release_tag == TAG else EVIDENCE / release_tag
    return directory / "review-candidate" if review_candidate else directory


def download_directory(review_candidate=False, release_tag=TAG):
    validate_release_tag(release_tag)
    directory = ROOT / ("artifacts/cache/v071-full-release-review-download" if review_candidate else "artifacts/cache/v071-full-release-download")
    return directory if release_tag == TAG else directory / release_tag


def record(name, value, review_candidate=False, release_tag=TAG):
    directory = release_evidence(review_candidate, release_tag)
    directory.mkdir(parents=True, exist_ok=True)
    with (directory / name).open("x", encoding="utf-8", newline="\n") as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)
        stream.write("\n")


def verify_remote_release(release, target_commit, title, body, action, resolved_tag, release_tag=TAG):
    validate_release_tag(release_tag)
    if (release.get("tag_name") != release_tag or release.get("name") != title
            or release.get("body") != body or release.get("prerelease") is not True):
        raise ValueError("Existing release is not this exact full-art draft; no release will be overwritten")
    if resolved_tag is None and release.get("target_commitish") != target_commit:
        raise ValueError("Draft release target differs from the tested Git commit")
    if action in ("draft", "upload", "publish") and release.get("draft") is not True:
        raise ValueError("Refusing to mutate or reuse an already published release")


def verify_download_proof(proof, asset, item, target_commit, manifest_path, release_tag=TAG, release_target=None):
    validate_release_tag(release_tag)
    if (proof.get("releaseTag", TAG) != release_tag
            or release_target is not None and proof.get("releaseTagCommit") != release_target):
        raise ValueError("Download proof belongs to a different release tag/anchor")
    if (proof.get("assetId") != asset["id"] or proof.get("name") != asset["name"]
            or proof.get("sha256") != item["sha256"] or proof.get("byteSize") != item["byteSize"]
            or proof.get("httpStatus") != 200 or proof.get("crc") != "passed" or proof.get("sourceCommit") != target_commit
            or proof.get("manifestSha256") != sha(local_path(manifest_path))
            or proof.get("matchesFrozenManifest") is not True):
        raise ValueError("Fresh download proof missing or mismatched")
    path=local_path(proof["path"])
    if sha(path) != item["sha256"] or path.stat().st_size != item["byteSize"]:
        raise ValueError("Fresh download bytes changed after verification")



def remote_tag_target(request, release_tag=TAG):
    validate_release_tag(release_tag)
    try:
        ref = request(API + "/git/ref/tags/" + urllib.parse.quote(release_tag, safe=""))
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
    parser.add_argument("action", choices=["check", "draft", "upload", "download", "publish", "inspect"])
    parser.add_argument("--name")
    parser.add_argument("--manifest", default=DEFAULT_MANIFEST)
    parser.add_argument("--release-tag", default=TAG, type=validate_release_tag)
    parser.add_argument("--existing-release-anchor-commit", help="Publish only the three binaries; anchor the tag to this already remote commit without uploading new source")
    parser.add_argument("--review-candidate", action="store_true", help="Select the separate restricted test-candidate manifest")
    parser.add_argument("--allow-review-candidate", action="store_true", help="Explicitly publish the restricted test candidate in the authorized channel; does not waive native-display/full-acceptance requirements")
    parser.add_argument("--allow-failed-windows-performance", action="store_true", help="Explicit diagnostic candidate with Windows performance FAILED; required in addition to both review switches for publish")
    args = parser.parse_args()
    if args.release_tag != TAG and args.manifest == DEFAULT_MANIFEST:
        parser.error("A new release tag requires an explicit new --manifest")
    remote = subprocess.check_output(["git", "remote", "get-url", "origin"], cwd=ROOT, text=True).strip()
    if remote != "https://github.com/XDwudi/game2.git":
        raise ValueError("Configured remote differs from user-authorized repository")
    manifest, expected, target_commit = release_inputs(args.manifest, args.review_candidate, args.allow_failed_windows_performance, args.existing_release_anchor_commit, args.release_tag)
    release_target = release_anchor(target_commit, args.existing_release_anchor_commit)
    # Reject before consulting credentials or making any network request.
    ensure_publish_allowed(manifest, args.action, args.allow_review_candidate, args.allow_failed_windows_performance)
    body = release_body(manifest, args.existing_release_anchor_commit)
    title = release_title(manifest)
    if args.action == "check":
        print(json.dumps({"status":"offline_inputs_verified_no_network","sourceCommit":target_commit,"releaseTagCommit":release_target,"releaseTag":args.release_tag,
                          "manifestSha256":sha(local_path(args.manifest)),"attachments":sorted(expected),
                          "title":title,"body":body},ensure_ascii=False))
        return
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
                   "User-Agent": "game2-v071-full-art-delivery", "X-GitHub-Api-Version": "2026-03-10"}
        if payload is not None:
            data = json.dumps(payload, ensure_ascii=False).encode("utf-8")
            headers["Content-Type"] = "application/json"
        if content_type:
            headers["Content-Type"] = content_type
            headers["Content-Length"] = str(data.seek(0, 2))
            data.seek(0)
        with opener.open(urllib.request.Request(url, data=data, headers=headers, method=method), timeout=180) as response:
            return json.load(response)

    if args.existing_release_anchor_commit is not None:
        require_existing_anchor(request, release_target)
    resolved_tag = remote_tag_target(request, args.release_tag)
    if resolved_tag is not None and resolved_tag != release_target:
        raise ValueError("Existing remote release tag points to a different release anchor")
    releases = request(API + "/releases?per_page=100")
    matches = [r for r in releases if r["tag_name"] == args.release_tag]
    if len(matches) > 1:
        raise ValueError("Ambiguous release")
    release = matches[0] if matches else None
    if args.action == "draft":
        if release is None:
            release = request(API + "/releases", "POST", {
                "tag_name": args.release_tag, "target_commitish": release_target,
                "name": title,
                "body": body, "draft": True, "prerelease": True, "make_latest": "false"})
    if release is None:
        raise ValueError("Create draft before uploading")
    verify_remote_release(release,release_target,title,body,args.action,resolved_tag,args.release_tag)
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
        record(args.name + ".upload.json", asset, args.review_candidate, args.release_tag)
        print(json.dumps({"uploaded": asset["name"], "size": asset["size"], "digest": asset["digest"]}), flush=True)
    if args.action == "download":
        if args.name not in expected or args.name not in assets:
            raise ValueError("Requested asset missing")
        item, asset = expected[args.name], assets[args.name]
        directory = download_directory(args.review_candidate, args.release_tag)
        directory.mkdir(parents=True, exist_ok=True)
        target = directory / args.name
        if target.exists():
            raise ValueError("Download target already exists; refusing to reuse local bytes as network evidence")
        headers = {"Authorization": "Bearer " + token, "Accept": "application/octet-stream",
                   "User-Agent": "game2-v071-full-art-delivery", "X-GitHub-Api-Version": "2026-03-10"}
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
                  "crc": "passed", "matchesFrozenManifest": True, "sourceCommit": target_commit,"releaseTagCommit":release_target,
                  "manifestSha256": sha(local_path(args.manifest)), "reviewCandidate": args.review_candidate,"releaseTag":args.release_tag}
        record(args.name + ".download.json", result, args.review_candidate, args.release_tag)
        print(json.dumps(result, ensure_ascii=False), flush=True)
    if args.action == "publish":
        for name, item in expected.items():
            asset = assets.get(name)
            proof_path = release_evidence(args.review_candidate, args.release_tag) / (name + ".download.json")
            if not asset or asset["state"] != "uploaded" or asset["digest"] != "sha256:" + item["sha256"] or asset["size"] != item["byteSize"]:
                raise ValueError("All three uploaded assets must match before publishing")
            proof = json.loads(proof_path.read_text(encoding="utf-8"))
            verify_download_proof(proof,asset,item,target_commit,args.manifest,args.release_tag,release_target)
        release = request(API + "/releases/" + str(release["id"]), "PATCH",
                          {"tag_name": args.release_tag, "target_commitish": release_target,
                           "draft": False, "prerelease": True, "make_latest": "false", "name": title, "body": body})
        if remote_tag_target(request, args.release_tag) != release_target:
            raise ValueError("Published release tag differs from frozen source; retain failure for review")
    release = request(API + "/releases/" + str(release["id"]))
    record("release-" + args.action + "-" + datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S-%f") + ".json", release, args.review_candidate, args.release_tag)
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
