"""71 bespoke, rule-motif illustrations in the approved art07 pixel vocabulary.

The four original golden samples remain unchanged. Each other entry has an
explicit composition; shared drawing tools enforce line/palette consistency.
"""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,math,random
from build_art_samples import Pixel,MANIFEST,save_asset,SOURCE,RUNTIME,C,c02,c41,c45,c52

PIPS={1:[(0,0)],2:[(-1,-1),(1,1)],3:[(-1,-1),(0,0),(1,1)],4:[(-1,-1),(1,-1),(-1,1),(1,1)],5:[(-1,-1),(1,-1),(0,0),(-1,1),(1,1)],6:[(-1,-1),(1,-1),(-1,0),(1,0),(-1,1),(1,1)]}
def die(p,x,y,w,n=1,color='blue',fill='paper',shadow=True):
 if shadow:p.card(x+2,y+3,w,w,'shadow')
 p.card(x,y,w,w,fill);p.line([(x+3,y+2),(x+w-4,y+2)],'text',1)
 r=1 if w<19 else 2
 for dx,dy in PIPS[n]:
  cx=int(x+w/2+dx*w*.23);cy=int(y+w/2+dy*w*.23);p.circle((cx-r,cy-r,cx+r,cy+r),color)
def iso(p,x,y,w=26,color='paper',pipcolor='blue'):
 h=w//2;p.poly([(x,y+h//2),(x+h,y),(x+w,y+h//2),(x+w,y+h*2),(x+h,y+h*2+h//2),(x,y+h*2)],'ink');p.poly([(x+2,y+h//2),(x+h,y+2),(x+w-2,y+h//2),(x+h,y+h)],color);p.poly([(x+2,y+h//2+2),(x+h-1,y+h+2),(x+h-1,y+h*2+h//2-2),(x+2,y+h*2-1)],'paperShade' if color=='paper' else 'blueShade');p.poly([(x+h+1,y+h+2),(x+w-2,y+h//2+2),(x+w-2,y+h*2-1),(x+h+1,y+h*2+h//2-2)],color)
 p.circle((x+h-2,y+h//2-2,x+h+2,y+h//2+2),pipcolor);p.circle((x+4,y+h+3,x+7,y+h+6),pipcolor);p.circle((x+h+4,y+h+7,x+h+7,y+h+10),pipcolor)
def arrow(p,x,y,w=16,col='violet',up=False,down=False):
 if up:p.poly([(x,y+w),(x+4,y+w),(x+4,y+7),(x+9,y+7),(x+2,y),(x-5,y+7),(x,y+7)],col,'ink',1)
 elif down:p.poly([(x,y),(x+4,y),(x+4,y+w-7),(x+9,y+w-7),(x+2,y+w),(x-5,y+w-7),(x,y+w-7)],col,'ink',1)
 else:p.poly([(x,y-2),(x+w-7,y-2),(x+w-7,y-7),(x+w,y),(x+w-7,y+7),(x+w-7,y+2),(x,y+2)],col,'ink',1)
def coin(p,x,y,r=6):
 p.circle((x-r,y-r+2,x+r,y+r+2),'goldShade','ink',1);p.circle((x-r,y-r,x+r,y+r),'gold','ink',1);p.circle((x-r+2,y-r+2,x+r-2,y+r-2),'gold','paper',1);p.rect((x-1,y-1,x+1,y+1),'goldShade')
def bracket(p,x,y,w,col='positive',up=False):
 p.line([(x,y-4 if up else y+4),(x,y),(x+w,y),(x+w,y-4 if up else y+4)],col,2)
def slot(p,x,y,w=12,occupied=False,n=1):
 p.card(x,y,w,w+3,'panel');p.line([(x+2,y+w),(x+w-3,y+w)],'paperShade',1)
 if occupied:die(p,x+2,y+2,w-4,n,'blue',shadow=False)
def ticket(p,x,y,w=30,h=20,col='paper'):
 p.poly([(x+2,y),(x+w-3,y),(x+w-1,y+3),(x+w-4,y+6),(x+w-1,y+9),(x+w-1,y+h-3),(x+w-3,y+h-1),(x+2,y+h-1),(x,y+h-4),(x+3,y+h-7),(x,y+h-10),(x,y+2)],col,'ink',2)
def hourglass(p,x,y,col='gold'):
 p.line([(x,y),(x+10,y)],'ink',2);p.line([(x,y+14),(x+10,y+14)],'ink',2);p.poly([(x+2,y+2),(x+8,y+2),(x+6,y+7),(x+8,y+12),(x+2,y+12),(x+4,y+7)],col)
def cup(p,x,y,w=22,h=25):
 p.poly([(x,y),(x+w,y),(x+w-4,y+h),(x+4,y+h)],'paper','ink',2);p.line([(x+2,y+3),(x+w-2,y+3)],'blue',3);p.circle((x+w//2-2,y+11,x+w//2+2,y+15),'blue')
def card_shape(p,x,y,w=16,h=24,col='paper'):
 p.card(x+1,y+2,w,h,'shadow');p.card(x,y,w,h,col);p.rect((x+4,y+6,x+w-5,y+h-9),'violetShade');p.line([(x+4,y+h-5),(x+w-5,y+h-5)],'paperShade',1)
def digit(p,n,x,y,scale=1,col='ink'):
 patterns={'0':['111','101','101','101','111'],'1':['010','110','010','010','111'],'2':['111','001','111','100','111'],'3':['111','001','111','001','111'],'4':['101','101','111','001','001'],'5':['111','100','111','001','111'],'6':['111','100','111','101','111']}
 for yy,row in enumerate(patterns[str(n)]):
  for xx,v in enumerate(row):
   if v=='1':p.rect((x+xx*scale,y+yy*scale,x+(xx+1)*scale-1,y+(yy+1)*scale-1),col)
def paintbrush(p,x,y,col):
 p.poly([(x+10,y),(x+17,y+4),(x+9,y+21),(x+2,y+17)],'paperShade','ink',2);p.poly([(x+2,y+17),(x+9,y+21),(x+4,y+33),(x-6,y+28)],'paper','ink',2);p.poly([(x-4,y+24),(x+5,y+28),(x+4,y+33),(x-6,y+28)],col)
def ring(p,box,col,w=2):p.circle(box,None,col,w)

def controller(id):
 if id=='C02':return c02()
 if id=='C41':return c41()
 if id=='C45':return c45()
 if id=='C52':return c52()
 p=Pixel();p.new('main object')
 if id=='C01':
  p.circle((10,23,48,49),'paperShade','ink',2);p.circle((9,20,46,45),'paper','ink',2);p.poly([(17,25),(17,16),(25,22)],'paper','ink',2);p.rect((8,30,15,38),'paper');p.rect((13,42,19,51),'paperShade');p.rect((36,42,42,51),'paperShade');p.circle((18,27,20,29),'ink');p.line([(28,22),(38,22)],'ink',2);p.line([(46,29),(54,26),(55,30),(51,33)],'paper',2);coin(p,34,12,6)
 elif id=='C03':
  for i in range(3):slot(p,7+i*17,31,14)
  p.circle((11,35,16,40),'red');p.line([(12,30),(12,9)],'paper',3);p.poly([(14,10),(28,10),(24,16),(28,21),(14,21)],'red','ink',1)
 elif id=='C05':
  p.d.arc((8,8,53,53),25,310,fill=C['blue'],width=5);p.poly([(45,7),(55,19),(41,19)],'gold','ink',1)
  for x,y in [(22,29),(31,35),(40,29)]:p.circle((x-3,y-3,x+3,y+3),'paper','ink',1)
  p.line([(44,48),(44,55),(48,55)],'gold',2)
 elif id=='C06':
  for x,y,n in [(8,23,1),(23,13,3),(38,23,6)]:ticket(p,x,y,18,30);digit(p,n,x+6,y+11,2,'violetShade')
 elif id=='C07':
  p.new('solid source die');die(p,7,12,23,4,'violetShade')
  p.new('ghost destination field');p.card(36,34,21,21,(*bytes.fromhex(C['paper'][1:]),48),edge=None)
  p.new('open destination outline')
  # Four separated brackets leave the destination visibly hollow at 32px.
  for pts in [[(36,40),(36,36),(38,34),(42,34)],[(50,34),(54,34),(56,36),(56,40)],[(56,48),(56,52),(54,54),(50,54)],[(42,54),(38,54),(36,52),(36,48)]]:p.line(pts,'violet',2)
  for x,y in [(41,39),(50,39),(41,48),(50,48)]:p.circle((x,y,x+3,y+3),None,'violet',1)
  p.new('square copy arrow');p.line([(20,36),(20,43),(25,43)],'ink',5);p.line([(20,36),(20,43),(25,43)],'violet',3);arrow(p,23,43,13,'violet')
 elif id=='C08':
  for i in range(5):slot(p,6+i*10,28,9,i<2,i+1)
  bracket(p,7,46,47,'blue');p.line([(28,18),(36,18)],'paper',2)
 elif id=='C09':
  for i in range(3):slot(p,7+i*13,27,12)
  p.poly([(47,26),(57,18),(57,38),(47,41)],'paperShade','ink',2);ring(p,(18,10,43,35),'violet',3);p.cross(30,22,5,'paper',True)
 elif id=='C10':
  p.card(11,16,29,33,'paperShade');p.circle((15,18,36,39),'red','ink',2);p.circle((23,25,28,30),'paper');p.rect((17,43,34,48),'redShade');p.cross(48,37,8,'positive')
 elif id=='C11':
  for i,n in enumerate([2,2,3,3]):die(p,6+i*13,23,12,n,'violetShade')
  bracket(p,7,43,22,'violet');bracket(p,33,43,22,'violet');p.line([(29,44),(29,51),(34,51)],'paper',2)
 elif id=='C12':
  ticket(p,7,12,23,30);digit(p,1,15,20,2);ticket(p,32,27,24,30);digit(p,2,40,36,2,'violetShade');arrow(p,24,16,24,'violet');p.poly([(24,38),(30,43),(24,45)],'paperShade')
 elif id in ['C13','C14']:
  for x,y,n in [(13,25,1),(27,17,3),(41,25,5)]:
   p.circle((x-5,y-5,x+10,y+10),'red','ink',1);digit(p,n,x,y-1,1,'paper')
  if id=='C13':p.rect((14,46,49,50),'paperShade');p.cross(31,44,7,'positive')
  else:ring(p,(6,8,57,54),'violet',3);p.line([(9,47),(15,52)],'paper',2)
 elif id=='C15':
  for i,col in enumerate(['red','blue','red']):p.circle((8+i*17,16,20+i*17,28),col,'ink',2)
  p.poly([(31,33),(23,44),(24,51),(30,55),(37,51),(39,45)],'red','ink',2);p.line([(13,31),(13,37),(23,37)],'red',2);p.line([(48,31),(48,37),(39,37)],'red',2)
 elif id=='C17':
  for x,y,w,n in [(7,33,13,1),(22,25,17,2),(41,15,17,3)]:die(p,x,y,w,n,'ink')
  p.rect((9,52,56,56),'gold');p.line([(10,53),(55,53)],'paper',1)
 elif id=='C18':
  p.poly([(29,17),(35,17),(35,48),(45,52),(45,55),(19,55),(19,52),(29,48)],'paperShade','ink',1);p.line([(11,20),(52,20)],'paper',3);p.circle((8,28,21,41),'red','ink',2);p.poly([(48,26),(57,35),(48,44),(39,35)],'blue','ink',2);p.line([(15,21),(15,28)],'paper',1);p.line([(48,21),(48,27)],'paper',1);p.circle((29,14,35,20),'gold','ink',1)
 elif id=='C19':
  die(p,9,12,16,1,'ink');p.poly([(10,35),(18,31),(26,35),(34,31),(42,35),(50,31),(55,35),(55,47),(47,51),(39,47),(31,51),(23,47),(15,51),(10,47)],'paper','ink',1)
  for x in range(15,53,7):p.circle((x,39,x+2,41),'blue')
  bracket(p,13,55,39,'positive',True)
 elif id=='C20':
  slot(p,12,12,33);die(p,16,15,25,6,'blue');p.line([(39,44),(39,57)],'gold',2);p.poly([(40,45),(55,45),(50,50),(55,54),(40,54)],'gold','ink',1);p.rect((15,48,29,51),'paperShade')
 elif id in ['C21','C22']:
  for i in range(3):slot(p,7+i*14,26,13)
  p.cross(41,32,4,'blue' if id=='C21' else 'gold')
  if id=='C22':slot(p,48,26,10);p.cross(53,32,3,'gold')
  p.line([(9,45),(54,45)],'paperShade',2);arrow(p,12,17,20,'blue' if id=='C21' else 'gold')
 elif id=='C24':
  die(p,7,26,21,1,'red');die(p,36,26,21,4,'red');p.poly([(18,22),(18,16),(45,16),(45,22),(40,22),(40,20),(23,20),(23,22)],'gold','ink',1);p.line([(19,52),(45,52)],'red',2)
 elif id=='C25':
  for x,y,n in [(8,10,1),(25,7,3),(43,10,4)]:die(p,x,y,13,n,'red')
  p.line([(14,26),(25,39)],'red',2);p.line([(31,22),(31,37)],'red',2);p.line([(49,26),(38,39)],'red',2);ring(p,(19,34,45,57),'red',4);p.circle((28,42,36,50),'gold')
 elif id=='C26':
  iso(p,12,20,29,'paper','red')
  for i in range(6):a=i*math.pi/3;p.circle((int(45+8*math.cos(a))-3,int(15+8*math.sin(a))-3,int(45+8*math.cos(a))+3,int(15+8*math.sin(a))+3),'red','ink',1)
 elif id=='C27':
  die(p,9,13,20,2,'ink');die(p,35,13,20,2,'ink');bracket(p,10,42,44,'positive',True);p.cross(32,50,6,'positive')
 elif id=='C28':
  for x,y,n in [(9,10,2),(29,10,2),(22,35,3),(42,35,3)]:die(p,x,y,15,n,'violetShade')
  bracket(p,10,29,33,'violet',True);bracket(p,23,54,32,'violet',True);p.cross(11,45,5,'paper',True)
 elif id=='C29':
  for i in range(4):p.rect((9+i*10,44-i*9,20+i*10,51-i*9),'paper');p.line([(9+i*10,44-i*9),(20+i*10,44-i*9)],'blue',2)
  p.line([(48,14),(55,14),(51,7),(57,7)],'paperShade',2)
 elif id in ['C30','C31']:
  if id=='C30':positions=[(22,9),(8,34),(37,34)]
  else:positions=[(9,9),(36,9),(9,36),(36,36)];ring(p,(6,6,57,57),'gold',1)
  for x,y in positions:die(p,x,y,18,3 if id=='C30' else 4,'violetShade')
  if id=='C31':p.line([(6,6),(57,6),(57,57),(6,57),(6,6)],'gold',2)
 elif id=='C32':
  for x,y in [(7,9),(41,9),(7,41),(41,41)]:die(p,x,y,15,1,'violetShade')
  for start in [(19,21),(43,21),(19,43),(43,43)]:p.line([start,(31,32)],'violet',2)
  p.circle((26,27,37,38),'paper','ink',2);p.circle((30,31,33,34),'violetShade')
 elif id=='C33':
  die(p,9,12,24,6,'ink');die(p,40,32,16,2,'ink');arrow(p,23,47,20,'gold');p.line([(36,43),(36,50)],'violet',1);p.line([(40,44),(40,51)],'violet',1)
 elif id in ['C34','C35']:
  for i in range(3):
   h=13+(i if id=='C34' else 2-i)*9;x=11+i*15;p.rect((x,48-h,x+9,48),'blue' if id=='C34' else 'positive');p.line([(x,48-h),(x+9,48-h)],'paper',2);digit(p,[4,5,6][i] if id=='C34' else [1,2,3][i],x+3,49-h,1,'ink')
  p.line([(8,54),(55,54)],'paperShade',2);p.poly([(29,51),(25,58),(35,58)],'paperShade');p.circle((48 if id=='C34' else 13,12,53 if id=='C34' else 18,17),'gold')
 elif id in ['C36','C37']:
  for x in [10,36]:slot(p,x,31 if id=='C36' else 12,19,True,2)
  if id=='C36':
   for x in [11,37]:p.line([(x,27),(x,22),(x+7,22),(x+7,16),(x+14,16),(x+14,10)],'positive',3);arrow(p,x+12,7,9,'positive',up=True)
  else:
   for x in [11,37]:p.line([(x,37),(x,42),(x+7,42),(x+7,48),(x+14,48),(x+14,54)],'blue',3);arrow(p,x+12,47,10,'blue',down=True)
 elif id=='C38':
  for i in range(3):slot(p,7+i*17,30,14,True,i+1)
  p.line([(13,27),(13,18),(49,18),(49,27)],'blue',3);p.rect((8,23,17,27),'blue');p.rect((44,23,53,27),'blue');p.line([(21,14),(42,14)],'paperShade',2)
 elif id=='C39':
  p.circle((8,12,35,39),'blue','ink',2);p.circle((19,22,24,27),'paper');ticket(p,29,33,27,21,'positive');p.cross(36,42,4,'ink')
  for i in range(6):p.rect((32+i*4,50,33+i*4,53),'ink')
 elif id=='C40':
  for i in range(3):x=10+i*15;y=42-i*10;p.rect((x,y,x+9,53),'positive');p.line([(x-2,y-4),(x+12,y-4)],'violet',2)
  p.line([(21,37),(25,27)],'paper',2);p.line([(36,27),(40,17)],'paper',2)
 elif id=='C42':
  p.new('carriage and danger hub');p.circle((12,41,25,54),'ink');p.circle((16,45,21,50),'danger');p.poly([(31,41),(41,43),(48,55),(25,55)],'paperShade','ink',2);p.line([(31,50),(43,50)],'danger',2)
  p.new('translucent paper glass barrel');p.poly([(11,18),(19,10),(51,29),(46,43),(35,45),(9,29)],(*bytes.fromhex(C['paper'][1:]),144),'ink',2)
  p.new('glass highlight and danger collar');p.line([(14,18),(20,14),(44,29)],'paper',2);p.poly([(11,19),(17,14),(22,17),(16,26),(11,24)],'danger','ink',1)
  p.new('single diamond refraction');p.line([(24,23),(31,21),(38,30),(30,33),(24,23)],'ink',2);p.line([(24,23),(31,21),(38,30)],'text',1)
  p.new('hollow short muzzle');p.circle((38,27,55,44),'paper','ink',2);p.circle((42,31,51,40),'ink');p.line([(43,38),(48,40),(51,37)],'danger',2)
 elif id=='C43':
  ticket(p,8,31,27,23);ticket(p,24,12,27,23);p.line([(13,37),(28,37)],'positive',2);p.line([(30,18),(44,18)],'positive',2);arrow(p,51,33,21,'gold',up=True)
 elif id=='C44':
  p.poly([(19,12),(41,12),(37,20),(47,33),(46,45),(38,50),(17,50),(9,43),(11,32),(23,20)],'gold','ink',2);p.line([(20,20),(39,20)],'paper',2);p.rect((10,50,53,56),'positive');p.cross(31,37,7,'paper')
 elif id=='C46':
  p.circle((8,11,56,56),'shadow');p.circle((7,7,54,54),'violetShade','ink',2);ring(p,(12,12,49,49),'paper',3);p.cross(30,30,9,'paper',True);p.line([(23,51),(38,51)],'violet',2)
 elif id=='C47':
  card_shape(p,8,17,18,26,'paperShade');card_shape(p,22,10,19,28);card_shape(p,38,17,18,26);p.card(20,43,26,14,'gold');p.rect((25,47,40,52),'ink')
  p.line([(27,49),(38,49)],'paperShade',1)
 elif id=='C48':
  for x in range(10,54,8):p.rect((x,9,x+3,11),'paper');p.rect((x,53,x+3,55),'paper')
  for y in range(17,51,8):p.rect((9,y,11,y+3),'paper');p.rect((53,y,55,y+3),'paper')
  p.cross(32,32,13,'positive')
 elif id=='C49':
  die(p,15,10,33,5,'goldShade');p.line([(9,53),(55,53)],'violet',2)
  for i in range(6):p.line([(11+i*8,49),(11+i*8,56)],'gold',1)
 elif id=='C50':
  ticket(p,8,17,47,33);p.cross(16,24,4,'positive');p.cross(48,43,4,'positive');digit(p,2,23,27,2);digit(p,0,33,27,2);p.line([(21,43),(39,43)],'paperShade',1)
 elif id=='C51':
  p.card(12,9,30,46,'paperShade');p.poly([(16,13),(36,8),(36,49),(16,53)],'paper','ink',2);p.circle((29,30,32,33),'goldShade');card_shape(p,39,29,15,23,'violet');arrow(p,24,21,24,'positive');p.line([(48,13),(48,23)],'positive',2);p.line([(45,18),(51,18)],'positive',1)
 elif id=='C53':
  p.circle((7,7,56,56),'goldShade','ink',2)
  for i in range(9):p.d.pieslice((10,10,53,53),i*40,(i+1)*40,fill=C['violet' if i%2 else 'paper'],outline=C['ink'],width=1)
  p.d.pieslice((16,16,47,47),0,180,fill=C['violetShade']);p.circle((28,28,36,36),'gold','ink',1);p.poly([(29,6),(37,6),(33,16)],'gold','ink',1)
 else:raise ValueError(id)
 return p

def item(id):
 p=Pixel();p.new('main object')
 if id=='I01':
  for i in range(5):p.card(6+i*10,25,9,15,'paper')
  p.line([(6,38),(12,33),(20,33),(26,39),(34,39),(41,32),(49,32),(57,36)],'red',4);p.line([(8,18),(15,14),(23,18),(31,21),(39,16),(48,13),(56,18)],'red',3)
 elif id=='I02':
  for x in range(10,51,8):p.rect((x,16,x+4,18),'violet');p.rect((x,52,x+4,54),'violet')
  for y in range(20,51,8):p.rect((9,y,11,y+4),'violet');p.rect((49,y,51,y+4),'violet')
  die(p,15,22,29,4,'violetShade');hourglass(p,42,39,'gold');p.cross(34,10,5,'positive')
 elif id=='I03':
  p.card(7,20,20,36,'paper');p.rect((11,24,22,31),'blueShade');p.circle((11,36,16,41),'red');p.circle((18,44,23,49),'blue');p.line([(16,20),(16,12)],'ink',3);die(p,37,9,18,2,'blue');die(p,37,37,18,3,'blue');p.line([(27,31),(32,31),(32,18),(37,18)],'blue',1);p.line([(32,31),(32,45),(37,45)],'blue',1)
  for x,y in [(46,18),(46,46)]:p.line([(x-12,y),(x+12,y)],'blue',1);p.line([(x,y-12),(x,y+12)],'blue',1)
 elif id in ['I04','I05']:
  die(p,17,31,28,6 if id=='I04' else 1,'blue' if id=='I04' else 'positive')
  for x,h in [(13,7),(28,12),(43,17)]:p.rect((x,26-h,x+8,26),'blue' if id=='I04' else 'positive');p.poly([(x-2,26-h),(x+4,20-h),(x+10,26-h)],'paper')
 elif id=='I06':
  ticket(p,10,27,44,27);p.line([(18,35),(45,35)],'paperShade',2);p.line([(18,43),(36,43)],'paperShade',2);p.cross(32,15,9,'blue')
 elif id=='I07':
  cup(p,10,23,25,29);p.d.arc((26,8,57,38),-110,180,fill=C['blue'],width=3);p.poly([(25,17),(27,29),(35,20)],'blue','ink',1);p.cross(43,45,8,'blue')
 else:raise ValueError(id)
 return p

def service(id):
 p=Pixel();p.new('main object')
 if id=='O01':
  p.line([(23,16),(23,9),(39,9),(39,16)],'paper',3);p.card(12,16,39,40,'paper');p.rect((16,24,47,28),'positive');p.card(17,34,28,16,'paperShade');p.line([(20,38),(42,38)],'ink',1)
  for x in [20,29,38]:p.rect((x,42,x+5,47),'positive')
  p.line([(43,26),(43,33)],'ink',2)
 elif id=='O02':
  p.poly([(9,27),(26,10),(54,10),(54,39),(36,56),(9,56)],'paperShade','ink',2);p.poly([(8,22),(25,7),(51,7),(51,36),(34,51),(8,51)],'paper','ink',2);p.circle((39,12,45,18),'gold','ink',1);p.line([(17,41),(40,24)],'goldShade',3);p.circle((16,25,22,31),'gold','ink',1);p.circle((34,37,40,43),'gold','ink',1)
 elif id=='O03':
  for x,y in [(17,8),(17,21),(17,34),(30,21),(43,21),(17,47)]:die(p,x,y,12,1,'blue',shadow=False)
  p.poly([(7,43),(12,39),(18,45),(49,45),(55,39),(58,44),(53,57),(12,57)],'paperShade','ink',1)
 elif id=='O04':
  die(p,21,8,23,3,'blue');p.poly([(8,35),(19,29),(54,36),(47,55),(15,55)],'paperShade','ink',2);p.poly([(8,35),(23,40),(31,32),(19,29)],'paper','ink',1);p.poly([(32,33),(47,40),(55,35),(47,30)],'paper','ink',1);p.rect((23,46,39,49),'dangerFill')
 elif id in ['O05','O06','O07']:
  p.line([(7,13),(7,55),(57,55),(57,13)],'paperShade',3);p.rect((8,39,56,43),'paperShade')
  if id in ['O05','O07']:card_shape(p,12,16,16,22);card_shape(p,33,16,16,22)
  else:
   p.card(14,24,13,14,'blue');p.rect((17,19,24,24),'paper');ticket(p,34,19,16,20)
  if id=='O07':p.d.arc((13,6,51,33),-150,5,fill=C['gold'],width=3);p.poly([(43,8),(55,13),(46,19)],'gold','ink',1);ticket(p,22,46,20,13,'paper');ring(p,(28,49,36,55),'goldShade',1)
  else:p.line([(45,9),(45,56)],'violet' if id=='O05' else 'blue',2);p.cross(46,48,5,'violet' if id=='O05' else 'blue')
 else:raise ValueError(id)
 return p

def upgrade(id):
 p=Pixel();p.new('main object')
 if id in ['U01','U02','U06']:
  die(p,11,11,43,5,'blue')
  if id=='U01':p.poly([(13,32),(43,13),(49,13),(15,38)],'blueLight');p.line([(14,43),(49,19)],'text',2)
  elif id=='U02':
   for x,y in [(17,17),(23,14),(35,16),(46,17),(15,27),(49,31),(17,42),(25,48),(39,47),(48,43)]:p.rect((x,y,x+1,y+1),'paperShade')
   p.poly([(44,54),(54,44),(54,54)],'ink')
  else:
   p.line([(15,15),(50,15),(50,50),(15,50),(15,15)],'gold',2)
   for x,y in [(15,15),(48,15),(15,48),(48,48)]:p.rect((x,y,x+2,y+2),'goldShade')
 elif id in ['U03','U04']:
  col='red' if id=='U03' else 'blue';paintbrush(p,25,8,col)
  if id=='U03':p.circle((13,42,30,58),col,'ink',2)
  else:p.poly([(22,40),(33,49),(22,58),(11,49)],col,'ink',2)
 elif id=='U05':
  iso(p,13,13,37,'blueLight','ink');p.line([(17,23),(31,17),(46,24)],'text',2);p.line([(20,31),(28,35),(25,45)],'paper',2)
 elif id=='U07':
  die(p,8,15,25,2,'blue');p.poly([(29,13),(41,18),(41,47),(29,42)],'violet','ink',1);die(p,36,30,21,5,'blue');p.d.arc((36,7,49,20),170,400,fill=C['violet'],width=3);p.line([(44,18),(44,24)],'violet',3);p.rect((43,27,45,29),'violet')
 else:raise ValueError(id)
 return p

def main(only=None):
 records=[]
 old={r['contentId']:r for r in json.loads((SOURCE/'content_manifest.json').read_text(encoding='utf8'))['assets']} if only else {}
 for a in MANIFEST['contentResources']:
  id=a['id'];fn={'C':controller,'I':item,'O':service,'U':upgrade}[id[0]];p=fn(id)
  if only and id!=only:records.append(old[id]);continue
  if id not in ['C02','C41','C45','C52']:
   # Same fixed guard-band transform for the whole expansion batch. Never
   # auto-fit each silhouette or enlarge tiny objects based on their bounds.
   normalized=[]
   for label,layer in p.layers:
    full=Image.new('RGBA',(64,64));full.paste(layer.resize((56,56),Image.Resampling.NEAREST),(4,4));normalized.append((label,full))
   p.layers=normalized
  rec=save_asset(a['icon_id'],p,a['iconSource'],a['iconExport'],tuple(a['sourceResolution']));rec.update({'contentId':id,'iconMotif':a['iconMotif'],'category':a['category'],'sourceGenerator':'tools/art_v07/build_content_icons.py'});records.append(rec)
 (SOURCE/'content_manifest.json').write_text(json.dumps({'artVersion':'art0.7.0','approvedGoldenSource':['C02','C41','C45','C52'],'status':'generated_pending_engine_review','assets':records},ensure_ascii=False,indent=2),encoding='utf8')
 font=ImageFont.truetype('C:/Windows/Fonts/consola.ttf',14)
 for size,cell in [(64,104),(32,80)]:
  cols=10;sheet=Image.new('RGB',(cols*cell,math.ceil(len(records)/cols)*cell),C['felt']);d=ImageDraw.Draw(sheet)
  for i,r in enumerate(records):
   x=i%cols*cell;y=i//cols*cell;im=Image.open(Path('DicePrototype')/r['runtimePath']).convert('RGBA').resize((size,size),Image.Resampling.NEAREST);sheet.paste(im,(x+(cell-size)//2,y+5),im);d.text((x+8,y+cell-24),r['contentId'],font=font,fill=C['paper'])
  sheet.save(SOURCE/f'Previews/content_{size}px.png')
 groups=[['C27','C28'],['C30','C31'],['C21','I06'],['O01','O06'],['C37','I05'],['C34','C35'],['U03','U04']];sheet=Image.new('RGB',(880,len(groups)*116),C['paper']);d=ImageDraw.Draw(sheet)
 for row,group in enumerate(groups):
  for col,id in enumerate(group):
   a=next(a for a in MANIFEST['contentResources'] if a['id']==id);im=Image.open(Path('DicePrototype')/a['iconExport']).convert('RGBA');x=col*440;y=row*116;large=im.resize((96,96),Image.Resampling.NEAREST);sheet.paste(large,(x+16,y+8),large);small=im.resize((64,64),Image.Resampling.NEAREST);sheet.paste(small,(x+140,y+24),small);tiny=im.resize((32,32),Image.Resampling.NEAREST);sheet.paste(tiny,(x+240,y+40),tiny);d.text((x+300,y+44),id,font=font,fill=C['ink'])
 sheet.save(SOURCE/'Previews/similar_content_comparison.png');print(json.dumps({'contentCount':len(records),'retiredExcluded':not any(r['contentId'] in ['C04','C16','C23'] for r in records)}))
if __name__=='__main__':
 import argparse
 parser=argparse.ArgumentParser();parser.add_argument('--only');args=parser.parse_args();main(args.only)
