"""Independent read-only inspection of actual diagnostic ZIPs, all member bytes and bound inputs."""
from pathlib import Path
import hashlib,json,struct,sys,zipfile
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[3];OUT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT/'tools/macos/v06'))
from package_checks import safe_archive_inventory
from compare_replays import validate_windows_reference,review_reference_notice
label=sys.argv[1];dest=OUT/(label+'.json');assert not dest.exists()
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for b in iter(lambda:f.read(1024*1024),b''):h.update(b)
    return h.hexdigest()
def member_sha(z,n):
    h=hashlib.sha256()
    with z.open(n) as f:
        for b in iter(lambda:f.read(1024*1024),b''):h.update(b)
    return h.hexdigest()
base=ROOT/'Releases/p0.6.0/review-candidate';mp=base/'review-candidate-manifest.json';m=read(mp)
report_path=ROOT/m['windowsEvidence']/'player-report.json';r=read(report_path)
inputs=[mp,report_path]+list((ROOT/'evidence/v06/packages').glob('*review-candidate.json'))+list(base.glob('*.zip'))
before={p.relative_to(ROOT).as_posix():sha(p) for p in inputs};errors=[];packages=[]
def check(ok,note):
    if not ok:errors.append(note)
check(m['schema']=='v06-diagnostic-review-candidate-1' and m['allowFailedWindowsPerformance'] is True and m['fullReleaseAccepted'] is False,'Wrong diagnostic scope')
check(m['diagnosticSummary']==review_reference_notice(r,True),'Manifest omits/changes diagnostic summary')
check(m['windowsPerformance']['originalPerformanceReport']==r['performance'] and m['windowsReportSha256']==sha(report_path),'Original failed performance was changed')
check(not validate_windows_reference(r,True,True),'Actual completed gameplay reference invalid')
identity={k:json.loads(r['buildIdentity'])[k] for k in json.loads(r['buildIdentity'])}
for platform in ('windows','macos'):
    proof=read(ROOT/'evidence/v06/packages'/f'{platform}-package-review-candidate.json')
    entry=next(x for x in m['platforms'] if x['platform']==('Windows' if platform=='windows' else 'macOS'))
    path=ROOT/proof['packagePath'];check(sha(path)==proof['sha256']==entry['sha256'] and path.stat().st_size==proof['byteSize']==entry['byteSize'],'Package hash/size '+platform)
    safe_archive_inventory(path);digests={}
    with zipfile.ZipFile(path) as z:
        wanted={x['path']:x for x in proof['files']};check(len(wanted)==len(proof['files']) and set(wanted)==set(z.namelist()),'Member set '+platform)
        for n in z.namelist():
            got=member_sha(z,n);digests[n]=got;check(got==wanted[n]['sha256'] and z.getinfo(n).file_size==wanted[n]['byteSize'],'Member bytes '+platform+'/'+n)
            built=ROOT/('Builds/v06/Windows' if platform=='windows' else 'Builds/v06/macOS')/n
            if built.is_file():check(got==sha(built),'Actual build differs '+n)
        check(json.loads(z.read('BUILD_IDENTITY.json'))==identity,'Package identity '+platform)
        notice=json.loads(z.read('REVIEW_CANDIDATE.json'))
        check(notice['schema']=='v06-diagnostic-review-notice-1' and notice['identity']==identity and notice['windowsPerformance']['acceptanceStatus']=='FAILED' and notice['fullReleaseAccepted'] is False,'Notice scope '+platform)
        text=z.read('开始游玩.txt').decode('utf-8-sig');check('FAILED' in text[:1200] and '23.0348' in text[:1200] and 'P22' in text[:1200],'First-screen diagnostic notice '+platform)
        if platform=='windows':
            exe=z.read('DicePrototype.exe');off=struct.unpack_from('<I',exe,60)[0];check(exe[:2]==b'MZ' and exe[off:off+4]==b'PE\0\0' and struct.unpack_from('<H',exe,off+4)[0]==0x8664,'Not actual x64 PE')
        else:
            for native in read(OUT/'macos-native-review01.json')['nativeComponents']:
                name=native['path'].split('Builds/v06/macOS/',1)[1];check(digests[name]==native['sha256'],'Mac native slice source differs '+name)
    packages.append({'platform':platform,'path':proof['packagePath'],'sha256':proof['sha256'],'byteSize':proof['byteSize'],'membersReadAndCrcChecked':len(digests)})
kp=read(ROOT/'evidence/v06/packages/mac-verification-kit-review-candidate.json');kit=ROOT/kp['packagePath'];safe_archive_inventory(kit)
check(sha(kit)==kp['sha256']==m['macVerificationKit']['sha256'],'Kit SHA differs')
prefix='DicePrototype-MacVerification/'
with zipfile.ZipFile(kit) as z:
    wanted={prefix+x['path']:x['sha256'] for x in kp['inputs']};check(set(wanted)==set(z.namelist()),'Kit member set differs')
    for name,expected in wanted.items():check(member_sha(z,name)==expected,'Kit input differs '+name)
    check(z.read(prefix+'inputs/windows-results/player-report.json')==report_path.read_bytes(),'Kit original failed report bytes differ')
    check(member_sha(z,prefix+'inputs/macos.zip')==packages[1]['sha256'],'Nested Mac ZIP differs')
    for name in ('normal-replay.jsonl','debug24-replay.jsonl','cross-platform-checkpoint/active-run.json','cross-platform-checkpoint/command.json','cross-platform-checkpoint/expected.json'):
        check(z.read(prefix+'inputs/windows-results/'+name)==(ROOT/m['windowsEvidence']/name).read_bytes(),'Kit original replay/checkpoint differs '+name)
    for name in ('normal-run-trace.json','debug-24-trace.json'):
        check(z.read(prefix+'inputs/'+name)==(ROOT/'evidence/v06/session'/name).read_bytes(),'Kit trace input differs '+name)
    for name in ('VerifyMac.command','verify_mac.py','package_checks.py','compare_replays.py'):
        check(z.read(prefix+name)==(ROOT/'tools/macos/v06'/name).read_bytes(),'Kit reviewed tool differs '+name)
    shell=z.read(prefix+'VerifyMac.command');check(not shell.startswith(b'\xef\xbb\xbf') and b'\r\n' not in shell,'Mac shell has Windows line endings/BOM')
    instructions=z.read(prefix+'执行说明.md').decode('utf-8-sig');check('FAILED' in instructions[:1200] and '23.0348' in instructions[:1200],'M5 instructions omit initial explicit performance failure')
    notice=json.loads(z.read(prefix+'inputs/review-candidate.json'));check(notice['windowsPerformance']['acceptanceStatus']=='FAILED' and notice['fullWindowsPassed'] is False,'Kit diagnostic notice differs')
packages.append({'platform':'kit','path':kp['packagePath'],'sha256':kp['sha256'],'byteSize':kp['byteSize'],'membersReadAndCrcChecked':len(kp['inputs'])})
after={p.relative_to(ROOT).as_posix():sha(p) for p in inputs};check(before==after,'Candidate artifacts changed during audit')
result={'scope':__doc__,'status':'verified_diagnostic_packages_only' if not errors else 'needs_rework','errors':errors,'packages':packages,'sourceIdentity':identity,
 'originalPlayerStatus':r['status'],'performanceAccepted':False,'nativeP22Accepted':False,'macRuntimeExecuted':False,'fullReleaseAccepted':False,
 'files':before,'stable':before==after,'limits':['All ZIP bytes/members/CRC and identities checked; no native app launched by reviewer.','Actual remote downloaded bytes and extracted Windows smoke are separate.','Mac runtime/signing/ditto and human acceptance remain pending.']}
dest.write_text(json.dumps(result,indent=2)+'\n',encoding='utf8');print(json.dumps({'status':result['status'],'errors':errors,'packages':packages}))
