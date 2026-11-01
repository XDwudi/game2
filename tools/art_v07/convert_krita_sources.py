"""Convert layered OpenRaster masters to real Krita documents using official CLI."""
from pathlib import Path
import argparse,subprocess,zipfile,json,hashlib,xml.etree.ElementTree as ET,os
ROOT=Path(__file__).resolve().parents[2]
p=argparse.ArgumentParser();p.add_argument('--krita',required=True);p.add_argument('--only');a=p.parse_args()
exe=Path(a.krita).resolve();records=[]
for ora in sorted((ROOT/'ArtSource/Art07').rglob('*.ora')):
 if a.only and a.only not in ora.name:continue
 kra=ora.with_suffix('.kra')
 if not kra.exists() or kra.stat().st_mtime < ora.stat().st_mtime:
  r=subprocess.run([str(exe),'--nosplash','--export','--export-filename',str(kra),str(ora)],cwd=exe.parent,timeout=75,capture_output=True,creationflags=subprocess.CREATE_NO_WINDOW if os.name=='nt' else 0)
  if r.returncode or not kra.exists():raise RuntimeError(f'{ora.name}: Krita failed: {r.returncode}: {r.stderr[-400:]}')
 with zipfile.ZipFile(kra) as z:
  assert z.read('mimetype')==b'application/x-krita';xml=ET.fromstring(z.read('maindoc.xml'));layers=xml.findall('.//{*}layer');assert layers
  records.append({'path':str(kra.relative_to(ROOT)).replace('\\','/'),'layers':len(layers),'bytes':kra.stat().st_size,'sha256':hashlib.sha256(kra.read_bytes()).hexdigest(),'reopenedAsZip':True})
 print('KRA',kra.stem,len(layers),flush=True)
(ROOT/'ArtSource/Art07/krita_conversion.json').write_text(json.dumps({'toolVersion':'Krita 5.3.4','sourceUrl':'https://download.kde.org/Attic/krita/5.3.4/krita-x64-5.3.4.zip','downloadSha256':'4bf5b51100b30fa68edd12b995932b82f8a0080ef115b5461941fa2d186d126c','officialMirrorlistHashMatched':True,'records':records},indent=2),encoding='utf8')
