"""Independently inspect complete candidate ZIP bytes; never extract or execute."""
import hashlib,json,pathlib,struct,sys,zipfile
root=pathlib.Path(__file__).resolve().parents[3]
output=root/'evidence/v05/review'/sys.argv[1]
if output.exists():raise SystemExit('Retain prior evidence; choose new output')
results=[]
for platform in ['windows','macos']:
    mp=root/'evidence/v05/packages'/f'{platform}-package-candidate.json'
    m=json.loads(mp.read_text(encoding='utf-8-sig'));p=root/m['packagePath'];errors=[];native=[]
    def ck(ok,msg):
        if not ok:errors.append(msg)
    ck(hashlib.sha256(p.read_bytes()).hexdigest()==m['sha256'],'ZIP digest')
    with zipfile.ZipFile(p) as z:
        ck(z.testzip() is None,'CRC')
        files=[q for q in z.infolist() if not q.is_dir()];byname={q['path']:q for q in m['files']}
        ck(len(files)==len({q.filename for q in files}),'duplicate ZIP names')
        ck({q.filename for q in files}==set(byname),'manifest/ZIP file set mismatch')
        for q in files:
            data=z.read(q);h=hashlib.sha256(data).hexdigest()
            if q.filename in byname:
                e=byname[q.filename]
                ck(len(data)==e['byteSize'] and h==e['sha256'],'entry '+q.filename)
                ck(oct(q.external_attr>>16)==e['zipMode'],'mode '+q.filename)
            if data[:4]==b'\xca\xfe\xba\xbe':
                count=struct.unpack_from('>I',data,4)[0];slices=[]
                for i in range(count):
                    cpu,sub,offset,size,align=struct.unpack_from('>IIIII',data,8+20*i)
                    ck(offset+size<=len(data),'slice bounds '+q.filename)
                    magic,c2,s2,ft=struct.unpack_from('<IIII',data,offset)
                    ck(magic==0xfeedfacf and c2==cpu and s2==sub,'64bit CPU header '+q.filename)
                    slices.append(dict(cpu=cpu,subtype=sub,filetype=ft,offset=offset,byteSize=size))
                ck({s['cpu'] for s in slices}=={0x01000007,0x0100000c},'Universal CPU '+q.filename)
                native.append(dict(path=q.filename,slices=slices))
        ck(json.loads(z.read('BUILD_IDENTITY.json'))==m['identity'],'embedded identity')
        if platform=='macos':
            ck(len(native)==6,'Mach-O count')
            ck(any(all(s['filetype']==2 for s in n['slices']) for n in native),'no executable')
    results.append(dict(platform=platform,manifestSha256=hashlib.sha256(mp.read_bytes()).hexdigest(),zipPath=m['packagePath'],zipSha256=m['sha256'],zipBytes=p.stat().st_size,fileCount=len(files),manifestFileCount=len(byname),native=native,errors=errors,sourceCommit=m['identity']['sourceCommit']))
output.write_text(json.dumps(dict(scope='Independent read of complete candidate ZIP bytes; no extraction or native execution, no release approval',results=results),indent=2)+'\n',encoding='utf8')
print(json.dumps([{k:v for k,v in r.items() if k!='native'} for r in results],indent=2))
raise SystemExit(any(r['errors'] for r in results))
