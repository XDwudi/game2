"""Cache-only adapter: validate a frozen local/downloaded ZIP; --execute extracts/runs.

No network, source changes, packaging or acceptance. The original v3 runner stays
unchanged; only byte-proven executable/runtime locations are rebound in memory.
"""
import argparse
from datetime import datetime
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import shutil
import sys
from urllib.parse import urlsplit
import zipfile

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[3]
RUNNER = ROOT / "tools/diagnostics/v07/run_resource_diagnostic_v3.py"
RUNNER_SHA = "859333e3d91837d136d17d091cddb3449f53b6b9ec7a5dd07d429daa324f07f0"
NAME = "DicePrototype-p0.7.0-win-x64.zip"


def require(value, message):
    if not value:
        raise ValueError(message)


def validate_server_asset(receipt, upload, release, item, tag, commit):
    """Cross-check unchanged server records; draft browser aliases are not Git tags."""
    asset_id, release_id = upload.get("id"), release.get("id")
    require(type(asset_id) is int and asset_id > 0 and type(release_id) is int and release_id > 0,
            "Invalid server asset/release ID")
    api = "https://api.github.com/repos/XDwudi/game2/releases"
    require(release.get("url") == api + "/" + str(release_id)
            and release.get("assets_url") == api + "/" + str(release_id) + "/assets"
            and release.get("upload_url") == "https://uploads.github.com/repos/XDwudi/game2/releases/" + str(release_id) + "/assets{?name,label}"
            and release.get("draft") is True and release.get("prerelease") is True
            and release.get("published_at") is None and release.get("tag_name") == tag
            and release.get("target_commitish") == commit, "Server draft release identity mismatch")
    fields = {"id": asset_id, "url": api + "/assets/" + str(asset_id), "name": NAME,
              "state": "uploaded", "size": item["byteSize"], "digest": "sha256:" + item["sha256"],
              "content_type": "application/zip"}
    require(type(upload.get("size")) is int and all(upload.get(k) == v for k, v in fields.items()),
            "Upload record does not identify the frozen asset")
    assets = release.get("assets")
    require(isinstance(assets, list), "Server release asset list missing")
    matches = [a for a in assets if a.get("id") == asset_id or a.get("name") == NAME]
    require(len(matches) == 1 and all(matches[0].get(k) == v for k, v in fields.items()),
            "Server release asset differs from upload record")
    browser = upload.get("browser_download_url")
    require(isinstance(browser, str) and matches[0].get("browser_download_url") == browser
            and receipt.get("downloadUrl") == browser and type(receipt.get("assetId")) is int
            and receipt["assetId"] == asset_id, "Download/upload/server asset URL or ID mismatch")
    url = urlsplit(browser)
    parts = url.path.split('/')
    require(url.scheme == "https" and url.netloc == "github.com" and not url.query and not url.fragment
            and len(parts) == 7 and parts[:5] == ['', 'XDwudi', 'game2', 'releases', 'download']
            and parts[5] not in ('', '.', '..') and parts[6] == NAME
            and release.get("html_url") == "https://github.com/XDwudi/game2/releases/tag/" + parts[5],
            "Browser download URL is not the recorded repository draft release")
    return {"releaseId": release_id, "assetId": asset_id, "releaseApiUrl": release["url"],
            "assetApiUrl": upload["url"], "tag": tag, "targetCommit": commit,
            "draft": True, "browserDownloadUrl": browser, "releaseHtmlUrl": release["html_url"]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--directory", required=True, type=Path)
    parser.add_argument("--source", choices=("local", "downloaded"), default="local")
    parser.add_argument("--server-release-record", type=Path, help="Existing publisher server release JSON; required for downloaded mode")
    parser.add_argument("--execute", action="store_true")
    args = parser.parse_args()
    if (args.source == "downloaded") != (args.server_release_record is not None):
        parser.error("--server-release-record is required only with --source downloaded")
    require(hashlib.sha256(RUNNER.read_bytes()).hexdigest() == RUNNER_SHA, "Reviewed runner changed")
    spec = importlib.util.spec_from_file_location("resource_v3", RUNNER)
    r = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(r)
    sys.path.insert(0, str(ROOT / "tools"))
    import publish_v07_release as publisher
    from macos.v07.package_checks import safe_archive_inventory

    # These imports/functions perform local validation only; publisher.main is never called.
    manifest, expected, commit = publisher.release_inputs(True, True)
    item = expected[NAME]
    archive = ROOT / item["packagePath"]
    manifest_path = publisher.release_manifest_path(True)
    manifest_bytes = manifest_path.read_bytes()
    require(json.loads(manifest_bytes.decode("utf-8-sig")) == manifest, "Manifest changed during validation")
    proof_path = publisher.local_path(item["packageEvidence"]["path"])
    proof_bytes = proof_path.read_bytes()
    require(hashlib.sha256(proof_bytes).hexdigest() == item["packageEvidence"]["sha256"], "Package member proof changed")
    proof = json.loads(proof_bytes.decode("utf-8-sig"))
    receipt_path = None
    receipt_facts = None
    server_facts = None
    server_hashes = {}
    if args.source == "downloaded":
        receipt_path = publisher.release_evidence(True) / (NAME + ".download.json")
        receipt_bytes = receipt_path.read_bytes()
        receipt = json.loads(receipt_bytes.decode("utf-8-sig"))
        archive = ROOT / "artifacts/cache/v07-release-review-download" / NAME
        upload_path = publisher.release_evidence(True) / (NAME + ".upload.json")
        release_path = args.server_release_record.resolve(strict=True)
        require(release_path.parent == publisher.release_evidence(True).resolve()
                and release_path.name.startswith("release-") and release_path.suffix == ".json",
                "Use an actual publisher server release record in its evidence directory")
        upload_bytes, release_bytes = upload_path.read_bytes(), release_path.read_bytes()
        server_facts = validate_server_asset(receipt, json.loads(upload_bytes.decode("utf-8-sig")),
            json.loads(release_bytes.decode("utf-8-sig")), item, manifest["releaseTag"], commit)
        server_hashes = {str(upload_path): hashlib.sha256(upload_bytes).hexdigest(),
                         str(release_path): hashlib.sha256(release_bytes).hexdigest()}
        host = receipt.get("finalHost")
        require(isinstance(host, str) and len(host) <= 253 and all(
            re.fullmatch(r"[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?", label) for label in host.split('.')),
            "Invalid download receipt final host")
        require(isinstance(receipt.get("utc"), str) and datetime.fromisoformat(receipt["utc"].replace('Z', '+00:00')).tzinfo is not None,
                "Invalid download receipt timestamp")
        require(receipt.get("httpStatus") == 200 and receipt.get("crc") == "passed"
                and receipt.get("matchesFrozenManifest") is True and receipt.get("reviewCandidate") is True
                and receipt.get("sourceCommit") == commit and receipt.get("name") == NAME
                and receipt.get("manifestSha256") == hashlib.sha256(manifest_bytes).hexdigest()
                and Path(receipt.get("path", "")).resolve() == archive.resolve()
                and receipt.get("sha256") == item["sha256"] and receipt.get("byteSize") == item["byteSize"],
                "Missing or mismatched genuine publisher download receipt")
        receipt_facts = {key: receipt.get(key) for key in ("utc", "assetId", "downloadUrl", "finalHost", "httpStatus", "zipEntries")}
    require(r.sha(archive) == item["sha256"] and archive.stat().st_size == item["byteSize"], "Frozen ZIP changed")
    require(proof["sha256"] == item["sha256"] and proof["byteSize"] == item["byteSize"]
            and proof["packagePath"] == item["packagePath"], "Member proof identifies a different ZIP")
    inventory = safe_archive_inventory(archive)
    if receipt_facts is not None:
        require(type(receipt_facts["zipEntries"]) is int and receipt_facts["zipEntries"] == len(inventory), "Download receipt member count mismatch")
    require(all(row["kind"] == "file" for row in inventory), "This Windows package must contain regular files only")
    for row in inventory:
        require(all(not set(part).intersection('<>:"|?*') and part.rstrip(' .') == part and not Path(part).is_reserved()
                    for part in row["path"].split('/')), "Unsafe Windows member name")
    members = {row["path"]: row for row in proof["files"]}
    require(len(members) == len(proof["files"]), "Duplicate proof members")
    with zipfile.ZipFile(archive) as z:
        require(z.testzip() is None, "ZIP CRC failed")
        require(set(z.namelist()) == set(members), "ZIP member set differs from frozen proof")
        for info in z.infolist():
            row = members[info.filename]
            require(info.file_size == row["byteSize"] and oct(info.external_attr >> 16) == row["zipMode"]
                    and hashlib.sha256(z.read(info)).hexdigest() == row["sha256"], "ZIP member differs: " + info.filename)
        package_identity = json.loads(z.read("BUILD_IDENTITY.json"))

    container = r.fresh_cache_directory(args.directory, ROOT)
    extracted = container / "player"
    bound = r.preflight(ROOT, ROOT / item["buildReport"]["path"], container / "run", 270)
    require(package_identity == proof["identity"] == bound["identity"], "Package identity differs from frozen gate")
    require(proof["buildReport"]["buildGuid"] == item["buildGuid"] == bound["buildGuid"], "Build GUID mismatch")
    require(bound["buildReportSha256"] == proof["buildReport"]["sha256"] == item["buildReport"]["sha256"], "Original build report changed")
    expected_tree = {"exists": True, "files": sorted(
        [{"path": n, "bytes": v["byteSize"], "sha256": v["sha256"]} for n, v in members.items()], key=lambda row: row["path"])}
    # Keep actual report/output intact. Rebinding is allowed only after member-byte equality.
    bound["exe"] = extracted / "DicePrototype.exe"
    bound["runtime"] = extracted / "DicePrototype_Data/Managed/DiceCraft.Runtime.dll"
    for key, name in [("exeSha256", "DicePrototype.exe"), ("runtimeSha256", "DicePrototype_Data/Managed/DiceCraft.Runtime.dll"),
                      ("bootSha256", "DicePrototype_Data/boot.config")]:
        require(members[name]["sha256"] == bound[key], "Extracted runtime is not the originally built runtime")
    inputs = {str(RUNNER): RUNNER_SHA, str(manifest_path): hashlib.sha256(manifest_bytes).hexdigest(),
              str(proof_path): item["packageEvidence"]["sha256"], str(archive): item["sha256"]}
    if receipt_path is not None:
        inputs[str(receipt_path)] = hashlib.sha256(receipt_bytes).hexdigest()
    inputs.update(server_hashes)

    def check_inputs():
        require(all(r.sha(Path(p)) == digest for p, digest in inputs.items()), "Frozen/download input changed after preflight")

    check_inputs()
    if not args.execute:
        print(json.dumps({"status": "plan_only", "writesOrProcessesStarted": False, "sourceCommit": commit,
                          "sourceMode": args.source, "remoteDownloadEvidence": args.source == "downloaded",
                          "publisherReceiptFacts": receipt_facts,
                          "serverAssetFacts": server_facts,
                          "archive": str(archive), "container": str(container), "argv": r.player_argv(bound),
                          "wholeReleaseAccepted": False}, ensure_ascii=False, indent=2))
        return 0
    require(r.fresh_cache_directory(args.directory, ROOT) == container, "Output location changed after preflight")
    container.mkdir(parents=True, exist_ok=False)
    result = {"status": "failed_or_incomplete", "wholeReleaseAccepted": False, "formalPerformanceAcceptance": False,
              "sourceMode": args.source, "remoteDownloadEvidence": args.source == "downloaded",
              "publisherReceiptFacts": receipt_facts,
              "serverAssetFacts": server_facts,
              "inputHashes": inputs, "archive": str(archive), "extracted": str(extracted),
              "actualBuildReportUnmodified": str(bound["buildReport"]), "errors": []}
    code = 1
    try:
        extracted.mkdir()
        with zipfile.ZipFile(archive) as z:
            for info in z.infolist():
                target = extracted / info.filename
                require(target.resolve().is_relative_to(extracted.resolve()), "Extraction escapes fresh directory")
                target.parent.mkdir(parents=True, exist_ok=True)
                with z.open(info) as source, target.open("xb") as destination:
                    shutil.copyfileobj(source, destination)
        check_inputs()
        require(r.tree_guard(extracted) == expected_tree, "Fresh extracted bytes differ from all frozen members")
        r.write_new(container / "extracted-member-verification.json", expected_tree)

        def launch_checked(argv, outer, timeout):
            check_inputs()
            require(r.tree_guard(extracted) == expected_tree, "Extracted tree changed before actual launch")
            return r.process_run(argv, outer, timeout)

        code = r.execute(bound, launch=launch_checked)
        check_inputs()
        require(r.tree_guard(extracted) == expected_tree, "Extracted tree changed during diagnostic")
        result["status"] = args.source + "_extraction_diagnostic_completed_not_acceptance" if code == 0 else "failed_or_incomplete"
        result["runnerSummary"] = str(container / "run/runner-summary.json")
    except BaseException as error:
        result["errors"].append(type(error).__name__ + ": " + str(error))
        code = 1
    finally:
        r.write_new(container / "remote-extraction-result.json", result)
    return code


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError) as error:
        print("Refused: " + type(error).__name__ + ": " + str(error), file=sys.stderr)
        raise SystemExit(2)
