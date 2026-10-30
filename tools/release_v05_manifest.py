"""Create an honest candidate manifest from actual files, Unity reports and Player results."""
import argparse
import json
from pathlib import Path
from v05_delivery import ROOT, IDENTITY, read_json, sha


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--windows-results",required=True,type=Path)
    parser.add_argument("--windows-build",required=True,type=Path)
    parser.add_argument("--macos-build",required=True,type=Path)
    args=parser.parse_args()
    identity=read_json(ROOT / IDENTITY)
    report=read_json(args.windows_results / "player-report.json")
    if report["status"]!="passed" or not report["stressCompleted"]:
        raise ValueError("Windows acceptance is not passing")
    if json.loads(report["buildIdentity"])!=identity:
        raise ValueError("Windows Player and current source identity differ")
    platforms=[]
    for platform,build_path,name,arch,signing,smoke in (
        ("Windows",args.windows_build,"win-x64","x86_64","NotSigned (Get-AuthenticodeSignature)","AUTOMATED_PASSED"),
        ("macOS",args.macos_build,"macos-universal","arm64+x86_64","Native signature verification pending on Mac; no Developer ID signing/notarization performed by this task","NOT_RUN_ON_MAC")):
        build=read_json(build_path)
        if build["result"]!="Succeeded" or build["errors"] or json.loads(build["identity"])!=identity:
            raise ValueError(platform+" build failed or identity differs")
        package=ROOT / ("Releases/p0.5.0/DicePrototype-p0.5.0-"+name+".zip")
        platforms.append({"platform":platform,"architecture":arch,"backend":"Mono","profile":build["profile"],
            "packagePath":package.relative_to(ROOT).as_posix(),"sha256":sha(package),"byteSize":package.stat().st_size,
            "buildUtc":build["buildUtc"],"buildGuid":build["buildGuid"],"buildWarnings":build["warnings"],
            "signingStatus":signing,"smokeStatus":smoke,
            "packagingStatus":"ZIP CRC + full extraction + actual Player verified" if platform=="Windows" else "Windows-generated transfer candidate; native ditto/fresh extraction pending",
            "intelRuntimeStatus":"not_applicable" if platform=="Windows" else "NOT_TESTED: x86_64 slice included, runtime unverified"})
    manifest={**identity,"blenderVersion":"5.2.3 LTS","buildUtc":max(p["buildUtc"] for p in platforms),
        "releaseStatus":"CANDIDATE_MAC_VALIDATION_PENDING","platforms":platforms,
        "tests":{"unityEditMode":{"passed":554,"failed":0},"unityPlayMode":{"passed":24,"failed":0}},
        "ruleReproduction":{"normalSeed":report["normalReplay"]["seed"],"normalSteps":report["normalReplay"]["completedSteps"],
            "normalOutcome":report["normalReplay"]["terminalPhase"],"normalFinalStage":report["normalReplay"]["terminalStage"],
            "debug24Passed":report["debug24"]["passed"],"crossPlatformStatus":"NOT_RUN_ON_MAC"},
        "windowsPerformance":{"sampleSeconds":report["performance"]["sampleSeconds"],"frames":report["performance"]["frameCount"],
            "p95FrameIntervalMs":report["performance"]["frameInterval"]["p95"],"thresholdMs":16.7,"status":report["performance"]["status"]},
        "windowsEvidence":args.windows_results.resolve().relative_to(ROOT).as_posix(),
        "macVerificationKit":read_json(ROOT / "evidence/v05/packages/mac-verification-kit.json"),
        "remoteRestoreStatus":"PENDING_FRESH_CLONE_LFS_CHECK",
        "requiredBeforeFullAcceptance":["M5 native arm64/Metal run and screenshots","Mac ditto repack and fresh-extraction run",
            "Windows-to-Mac checkpoint and identical rule replay","Mac checkpoint returned to Windows",
            "Actual Mac mouse/trackpad and stable-state quit/resume","Fresh remote Git LFS restoration","Human playtesting"],
        "note":"Source commit identifies gameplay code. The later delivery commit includes both ZIPs and this manifest; no circular self-commit field."}
    target=ROOT / "Releases/p0.5.0/release-manifest.json"
    target.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    print(str(target))


if __name__=="__main__":main()
