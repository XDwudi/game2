"""Code-native, editable symbols for shared actions. No runtime numbers baked in."""
from build_art_samples import Pixel,MANIFEST,save_asset,SOURCE,C
import json,math

def symbol(id):
 p=Pixel((32,32));col='paper';s=id.removeprefix('ui_icon_')
 if s in ('plus','multiply','close'):
  p.cross(16,16,10,col,s!='plus')
 elif s in ('arrow_right','back'):
  if s=='arrow_right':p.poly([(5,13),(19,13),(19,7),(29,16),(19,25),(19,19),(5,19)],col)
  else:p.poly([(27,13),(13,13),(13,7),(3,16),(13,25),(13,19),(27,19)],col)
 elif s=='lock':
  p.circle((8,4,23,22),col);p.circle((12,8,19,21),(0,0,0,0));p.card(6,15,20,14,col);p.circle((14,19,17,22),'ink');p.rect((15,21,16,25),'ink')
 elif s=='pending':
  for x,y in [(4,4),(12,4),(20,4),(28,4),(4,12),(28,12),(4,20),(28,20),(4,28),(12,28),(20,28),(28,28)]:p.rect((x-1,y-1,x+1,y+1),'gold')
  p.line([(9,16),(14,21),(24,10)],'gold',3)
 elif s=='refresh':
  p.d.arc((5,5,26,26),40,310,fill=C[col],width=4);p.poly([(23,4),(28,14),(17,11)],col)
 elif s=='coin':
  p.circle((4,5,28,29),'goldShade','ink',1);p.circle((4,3,27,26),'gold','ink',1);p.circle((7,6,24,23),'gold','paper',1);p.rect((13,11,18,17),'ink');p.line([(8,7),(12,5)],'paper',2)
 elif s=='roll':
  p.poly([(7,8),(26,8),(23,27),(10,27)],col,'ink',2);p.line([(9,4),(24,4)],'blue',3);p.circle((14,14,18,18),'blue');p.d.arc((2,3,30,29),30,160,fill=C['blue'],width=2)
 elif s=='submit':
  p.line([(4,19),(4,27),(28,27),(28,19)],col,3);p.poly([(14,22),(18,22),(18,12),(24,12),(16,4),(8,12),(14,12)],col)
 elif s=='settings':
  pts=[]
  for i in range(32):
   a=i*math.pi/16;r=14 if i%4 in (0,1) else 10;pts.append((16+r*math.cos(a),16+r*math.sin(a)))
  p.poly(pts,col);p.circle((11,11,21,21),(0,0,0,0))
 elif s in ('sound','muted'):
  p.poly([(4,12),(10,12),(18,5),(18,27),(10,20),(4,20)],col)
  if s=='sound':p.d.arc((14,8,27,24),-75,75,fill=C[col],width=2);p.d.arc((10,3,32,29),-70,70,fill=C[col],width=2)
  else:p.line([(22,11),(29,21)],'red',3);p.line([(29,11),(22,21)],'red',3)
 elif s=='info':
  p.circle((4,4,28,28),col);p.rect((15,13,17,23),'ink');p.rect((14,8,18,10),'ink')
 elif s=='check':p.line([(5,16),(12,24),(28,7)],'positive',4)
 elif s=='warning':
  p.poly([(16,3),(30,28),(2,28)],'gold','ink',1);p.rect((15,11,17,20),'ink');p.rect((15,23,17,25),'ink')
 elif s=='temporary':
  p.line([(8,5),(24,5)],col,3);p.line([(8,27),(24,27)],col,3);p.poly([(10,7),(22,7),(20,12),(17,16),(20,20),(22,25),(10,25),(12,20),(15,16),(12,12)],col,'ink',1);p.poly([(12,23),(16,18),(20,23)],'gold')
 elif s=='smooth':
  for y in (10,17,24):p.line([(3,y),(8,y-3),(14,y-3),(19,y),(25,y),(29,y-3)],'blue',2)
 elif s=='rough':
  for x,y,r in [(8,10,4),(22,9,3),(16,23,5)]:p.circle((x-r,y-r,x+r,y+r),'paper','ink',1)
 elif s=='gold':
  p.line([(5,5),(27,5),(27,27),(5,27),(5,5)],'gold',3)
  for x,y,sx,sy in [(2,2,1,1),(30,2,-1,1),(2,30,1,-1),(30,30,-1,-1)]:p.line([(x+sx*5,y),(x,y),(x,y+sy*5)],'gold',1)
 elif s=='glass':
  p.poly([(10,3),(23,3),(29,12),(16,29),(3,12)],'blue','paper',1);p.line([(10,3),(10,12),(16,29),(23,12),(23,3)],'paper',1);p.line([(3,12),(29,12)],'ink',1)
 elif s=='color_red':p.circle((5,5,27,27),'red','ink',2)
 elif s=='color_blue':p.poly([(16,3),(29,16),(16,29),(3,16)],'blue','ink',2)
 elif s=='broken_link':
  p.d.arc((3,5,19,20),80,330,fill=C['violet'],width=3);p.d.arc((13,13,29,28),260,520,fill=C['violet'],width=3);p.line([(14,3),(13,9)],'gold',2);p.line([(23,7),(18,11)],'gold',2)
 elif s=='broken':
  p.card(5,4,23,25,col);p.line([(18,4),(13,11),(20,16),(12,23),(17,28)],'dangerFill',3)
 elif s=='unavailable':
  p.circle((4,4,28,28),(0,0,0,0),'muted',3);p.line([(7,25),(25,7)],'muted',3)
 elif s=='eye':
  p.poly([(2,16),(9,9),(16,6),(23,9),(30,16),(23,23),(16,26),(9,23)],col);p.circle((10,10,22,22),'ink');p.circle((14,14,18,18),'blue')
 else:raise ValueError('Missing explicit symbol design '+s)
 return p

records=[]
for a in MANIFEST['sharedResources']['assets']:
 if a['assetId'].startswith('ui_icon_'):records.append(save_asset(a['assetId'],symbol(a['assetId']),a['sourcePath'],a['runtimePath'],tuple(a['dimensionsPx'])))
(SOURCE/'public_symbols_manifest.json').write_text(json.dumps({'artVersion':'art0.7.0','assets':records,'status':'produced_pending_engine_review'},indent=2),encoding='utf8')
print(json.dumps({'symbols':len(records)}))
