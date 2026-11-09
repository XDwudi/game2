"""Stage selected, hash-verified original art bytes; Unity owns import settings/references."""
from pathlib import Path
import argparse, hashlib, json, shutil

ROOT=Path(__file__).resolve().parents[2]
DEST=ROOT/'DicePrototype/Assets/Art/Art071'
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--complete',action='store_true');args=parser.parse_args()
    config=read(ROOT/'DicePrototype/Assets/Game/Data/P0Config.json')
    ids=[a['id'] for kind in ('controllers','consumables','services','diceUpgrades') for a in config[kind]]
    rows=[]; missing=[]
    def copy(source,target,expected,kind):
        source=source.resolve();target=target.resolve()
        if not source.is_relative_to(ROOT/'ArtSource') or not target.is_relative_to(DEST):
            raise ValueError('Asset source/destination escaped the production directories')
        if sha(source)!=expected: raise ValueError('Source SHA mismatch: '+str(source))
        target.parent.mkdir(parents=True,exist_ok=True)
        if not target.exists() or sha(target)!=expected: shutil.copyfile(source,target)
        if sha(target)!=expected: raise ValueError('Runtime copy SHA mismatch')
        rows.append(dict(id=target.stem,kind=kind,source=source.relative_to(ROOT).as_posix(),runtime=target.relative_to(ROOT/'DicePrototype').as_posix(),sha256=expected))
    for ident in ids:
        folder=ROOT/'ArtSource/Art071/IllustrationsFull'/ident
        record=folder/'record.json'
        if record.exists():
            r=read(record)
            raw=r.get('path') or r.get('file') or r.get('originalPath')
            source=Path(raw) if raw else folder/'original.png'
            if not source.is_absolute(): source=ROOT/source if len(source.parts)>1 else folder/source
            copy(source,DEST/'Illustrations'/('illustration_'+ident.lower()+'.png'),r['sha256'],'illustration')
        else:
            source=ROOT/'ArtSource/Art071/Illustrations'/('illustration_'+ident.lower()+'.png')
            if source.exists(): copy(source,DEST/'Illustrations'/source.name,sha(source),'illustration_reused')
            else: missing.append(ident)
    common=read(ROOT/'ArtSource/Art071/Full/UI/manifest.json')
    for r in common['assets']:
        copy(ROOT/r['export'],DEST/'Full'/r['category']/Path(r['export']).name,r['sha256'],'public')
    background=ROOT/'ArtSource/Art071/Full/Backgrounds/bg_felt_tile.png'
    copy(background,DEST/'Full/Backgrounds'/background.name,sha(background),'background')
    # Additional surfaces have their own explicit manifest; preview sheets are never imported.
    surfaces=ROOT/'ArtSource/Art071/Full/Surfaces/manifest.json'
    if surfaces.exists():
        for r in read(surfaces)['assets']:
            copy(ROOT/r['export'],DEST/'Full/Surfaces'/Path(r['export']).name,r['sha256'],'surface')
    if args.complete and missing: raise ValueError('Missing illustrations: '+','.join(missing))
    proof=dict(schema=1,status='staged_not_unity_verified',complete=len(missing)==0,missing=missing,assets=rows)
    (DEST/'FullSourceManifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(dict(illustrations=len(ids)-len(missing),missing=missing,staged=len(rows)),ensure_ascii=False))

if __name__=='__main__': main()
