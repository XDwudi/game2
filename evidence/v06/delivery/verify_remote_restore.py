"""Bounded fresh-download startup/checkpoint check; never a full Player/performance pass."""
from pathlib import Path
import hashlib
import json
import re
import subprocess
import sys
import time
import zipfile
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / 'tools'))
from macos.v06.package_checks import safe_archive_inventory

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))

def main():
    attempt = sys.argv[1] if len(sys.argv) == 2 else '01'
    assert re.fullmatch(r'[0-9]{2}', attempt), 'Two digit attempt required'
    proofs = ROOT / 'evidence/v06/delivery/github-release/review-candidate'
    proof = read(proofs / 'DicePrototype-p0.6.0-win-x64.zip.download.json')
    archive = Path(proof['path'])
    assert proof['httpStatus'] == 200 and proof['matchesFrozenManifest'] is True
    assert sha(archive) == proof['sha256'] and archive.stat().st_size == proof['byteSize']
    safe_archive_inventory(archive)
    target = ROOT / ('artifacts/cache/v06-remote-player' + attempt)
    results = proofs / ('windows-restored-run' + attempt)
    assert not target.exists() and not results.exists(), 'Fresh directories required'
    target.mkdir(); results.mkdir()
    with zipfile.ZipFile(archive) as z:
        assert z.testzip() is None
        z.extractall(target)
    identity = read(target / 'BUILD_IDENTITY.json')
    assert identity == read(ROOT / 'DicePrototype/Assets/Resources/BuildIdentity.json')
    exe = target / 'DicePrototype.exe'
    assert sha(exe) == sha(ROOT / 'Builds/v06/Windows/DicePrototype.exe')
    checkpoint = ROOT / 'evidence/v06/player/player-20261031-154556-f25586f0/cross-platform-checkpoint'
    source_sha = sha(checkpoint / 'active-run.json')
    expected = read(checkpoint / 'expected.json')
    arguments = [str(exe), '--verify-p0', str(ROOT / 'evidence/v06/session/normal-run-trace.json'),
                 '--verify-debug-trace', str(ROOT / 'evidence/v06/session/debug-24-trace.json'),
                 '--verify-import-save', str(checkpoint), '--verify-defer-native-display',
                 '--evidence-dir', str(results), '-logFile', str(ROOT / ('artifacts/cache/v06/remote-restore' + attempt + '.log'))]
    startup = subprocess.STARTUPINFO()
    startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    startup.wShowWindow = 1
    p = subprocess.Popen(arguments, cwd=target, startupinfo=startup)
    result = {'schema': 'v06-fresh-remote-targeted-restore-1', 'startedUtc': datetime.now(timezone.utc).isoformat(),
              'runnerSha256': sha(__file__), 'attempt': attempt,
              'pid': p.pid, 'actualExecutable': str(exe), 'executableSha256': sha(exe),
              'remoteAssetId': proof['assetId'], 'downloadSha256': sha(archive), 'identity': identity,
              'status': 'started', 'sourceSaveSha256': source_sha,
              'scope': 'Fresh GitHub bytes extracted to a new directory, real Player checkpoint continuation and rendered startup only. Own process intentionally terminated after bounded checks; no full replay/performance/native-display acceptance.'}
    try:
        deadline = time.monotonic() + 60
        while time.monotonic() < deadline:
            if p.poll() is not None:
                raise RuntimeError('Player exited before targeted checks completed')
            folders = list(results.glob('player-*'))
            if len(folders) == 1:
                folder = folders[0]
                check = folder / 'imported-checkpoint/verification.json'
                report_path = folder / 'player-report.json'
                if check.is_file() and report_path.is_file() and (folder / 'main-menu-1920x1080.png').is_file():
                    try:
                        report, receipt = read(report_path), read(check)
                    except json.JSONDecodeError:
                        time.sleep(.2); continue
                    assert receipt['passed'] and receipt['sourceSaveSha256'] == source_sha
                    assert receipt['inputStateSha256'] == expected['inputStateSha256']
                    assert receipt['outputStateSha256'] == expected['outputStateSha256']
                    assert receipt['submissionScore'] == expected['submissionScore']
                    assert report['importedCheckpointStatus'] == 'passed'
                    assert json.loads(report['buildIdentity']) == identity, 'Embedded JSON build identity mismatch'
                    assert report['environment']['buildGuid'] == 'c137ca83c7ec4fbab3243ff0aa663bfb', 'Actual build GUID mismatch'
                    assert report['environment']['isEditor'] is False and not report['unityErrors']
                    assert sha(checkpoint / 'active-run.json') == source_sha
                    result.update(status='targeted_checks_passed', receipt=receipt, report=str(report_path),
                                  reportStatus=report['status'], mainMenuScreenshot=str(folder / 'main-menu-1920x1080.png'),
                                  checkpointEvidenceSha256=sha(check), screenshotSha256=sha(folder / 'main-menu-1920x1080.png'))
                    break
            time.sleep(.2)
        else:
            raise TimeoutError('Targeted checkpoint/startup deadline exceeded')
    except Exception as error:
        result.update(status='failed', failureType=type(error).__name__, failure=str(error))
    finally:
        result['terminatedOnlyOwnedProcess'] = p.poll() is None
        if p.poll() is None:
            p.terminate()
        result['playerExitCode'] = p.wait(timeout=15)
        result['endedUtc'] = datetime.now(timezone.utc).isoformat()
        (proofs / ('windows-remote-restore' + attempt + '.json')).write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(result, ensure_ascii=False))
    return 0 if result['status'] == 'targeted_checks_passed' else 1

if __name__ == '__main__':
    raise SystemExit(main())
