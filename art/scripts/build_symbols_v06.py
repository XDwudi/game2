"""Incremental P0 v0.6 vector symbols and deterministic short feedback sounds.

Run --samples, inspect art/previews/v06/samples.png, then run with no arguments.
Reuses project-native editable SVG geometry; does not touch dice assets or Unity metadata.
"""
from pathlib import Path
import argparse, hashlib, json, math, re, struct, wave
from PIL import Image, ImageDraw, ImageFont
from build_symbols import Symbol, ROOT, OUT, SOURCE, AUDIO, S, INK, MINT, RED, BLUE, GOLD, DARK, FONT
from build_symbols_v05 import save_symbol, sheet, audio, VIOLET, MUTED

PREVIEWS = ROOT / 'art/previews/v06'
EVIDENCE = ROOT / 'evidence/v06/art'
UPDATED = ['C09','C10','C13','C14','C27','C30','C38','C39']
NEW = [f'C{i:02}' for i in range(41,54)] + ['O05','O06','O07']
ENCHANTS = [('slotless', MINT, 'hollow-slot-minus'), ('base10', GOLD, 'plus-ten'), ('mult2', VIOLET, 'multiply-two'), ('submit1', BLUE, 'submission-document-plus-one'), ('roll1', '#efae79', 'roll-cup-plus-one')]


def icon(cid):
    c=Symbol()
    if cid=='C09':
        c.rect(38,69,65,100,10);c.rect(117,69,65,100,10);c.minus(211,119,19);c.text(110,212,'×4',45,MINT)
    elif cid=='C10':
        c.dice(27,70,106,1);c.plus(187,89,20);c.text(188,165,'10',50,MINT)
    elif cid=='C13':
        c.text(83,129,'3',110);c.plus(185,104,23);c.text(186,166,'3',45,MINT)
    elif cid=='C14':
        c.text(128,99,'1 3 5',52);c.arc(128,129,91,15,165,MINT,9);c.text(128,194,'×3',43,MINT)
    elif cid=='C27':
        c.dice(40,35,76,2,INK);c.dice(140,35,76,2,INK);c.plus(75,177,22,MINT,10);c.text(164,174,'10',65,MINT)
    elif cid=='C30':
        for x in [27,97,167]:c.dice(x,68,62,3,INK)
        c.line([(34,157),(223,157)],MINT,8);c.text(128,205,'×4',48,MINT)
    elif cid=='C38':
        c.dice(25,100,66,1,BLUE);c.rect(103,100,50,66,8,outline=MUTED,width=5);c.dice(165,100,66,1,BLUE)
        for x in [59,199]:
            c.line([(x-12,55),(x+12,78)],INK,10);c.polygon([(x+3,69),(x+24,50),(x+37,64),(x+16,84)],BLUE)
        c.line([(25,200),(92,200)],BLUE,7);c.line([(165,200),(231,200)],BLUE,7)
    elif cid=='C39':
        c.dice(31,75,111,2,BLUE);c.plus(198,96,22,MINT,9);c.text(198,164,'6',56,MINT)
    elif cid=='C41':
        # A hollow mask faces the immediate right neighbour; no target enchant is pictured.
        c.line([(29,67),(63,49),(96,67),(131,49),(157,67),(148,143),(112,179),(74,175),(39,144),(29,67)],VIOLET,8)
        c.line([(52,104),(69,111),(82,103)],INK,8);c.line([(105,103),(122,111),(138,103)],INK,8)
        c.arc(95,133,22,25,155,VIOLET,6);c.arrow(163,114,228,114,MINT,10,17)
        c.line([(169,186),(222,186)],MINT,7)
    elif cid=='C42':
        c.polygon([(53,132),(164,71),(186,111),(76,166)],BLUE)
        c.line([(66,138),(112,118),(103,135),(139,115)],INK,6)
        c.circle(79,190,28,outline=INK,width=8);c.circle(148,190,28,outline=INK,width=8)
        c.line([(63,171),(164,171)],INK,8);c.line([(169,63),(192,109)],BLUE,9)
        c.line([(198,59),(226,40)],GOLD,8);c.line([(208,82),(235,82)],GOLD,7)
    elif cid=='C43':
        c.rect(29,39,70,89,10,outline=INK,width=6);c.rect(119,39,70,89,10,outline=INK,width=6)
        for x in [64,154]:c.circle(x-12,69,6,fill=MINT,outline=None);c.circle(x+12,96,6,fill=MINT,outline=None)
        c.line([(32,215),(90,215),(90,183),(149,183),(149,151),(220,151)],MINT,9)
        c.arrow(211,126,211,50,GOLD,8,14)
    elif cid=='C44':
        c.line([(54,102),(89,68),(163,68),(197,103),(216,185),(190,214),(64,214),(39,185),(54,102)],GOLD,8)
        c.polygon([(92,63),(73,28),(126,40),(178,28),(159,63)],GOLD)
        c.plus(128,152,32,MINT,11)
    elif cid=='C45':
        c.arc(128,128,86,100,260,RED,13);c.arc(128,128,86,-80,80,BLUE,13)
        c.polygon([(152,66),(149,147),(104,191),(107,112)],INK)
        c.circle(128,129,11,fill=DARK,outline=INK,width=5)
        c.circle(128,27,8,fill=RED,outline=None);c.circle(128,229,8,fill=BLUE,outline=None)
    elif cid=='C46':
        c.circle(128,127,90,outline=MINT,width=10);c.text(128,123,'×3',78,INK)
        c.line([(81,222),(175,222)],MINT,8)
    elif cid=='C47':
        for x,y,col in [(29,38,MUTED),(57,65,VIOLET),(86,94,MINT)]:c.rect(x,y,128,135,16,outline=col,width=7)
        for x,y in [(122,133),(178,188)]:c.circle(x,y,9,fill=INK,outline=None)
        c.circle(150,160,9,fill=INK,outline=None)
    elif cid=='C48':
        c.rect(31,39,77,102,10,outline=INK,width=7);c.rect(147,39,77,102,10,outline=MUTED,width=7)
        c.circle(69,90,9,fill=INK,outline=None);c.plus(65,196,22,MINT,9);c.text(155,193,'10',60,MINT)
    elif cid=='C49':
        c.dice(38,35,102,5,INK);c.text(190,103,'n',75,MINT)
        c.line([(44,186),(211,186)],MINT,8)
        for x in [57,97,137,177,217]:c.line([(x,174),(x,205)],MINT,6)
    elif cid=='C50':
        c.rect(30,44,196,170,20,outline=GOLD,width=9);c.text(128,124,'+20',77,GOLD)
        c.line([(59,189),(197,189)],INK,6)
    elif cid=='C51':
        c.rect(30,41,69,113,12,outline=INK,width=7);c.arrow(71,99,184,99,MINT,9,18)
        c.line([(30,218),(227,218)],MINT,7)
        for i,h in enumerate([22,45,68]):c.rect(116+i*38,205-h,22,h,4,fill=MINT,outline=None,width=0)
        c.line([(199,77),(218,98),(199,119)],MINT,7)
    elif cid=='C52':
        c.rect(29,36,132,179,13,outline=GOLD,width=8);c.line([(51,71),(136,71)],GOLD,7);c.text(93,127,'+3',45,GOLD)
        c.circle(185,171,45,fill=DARK,outline=MINT,width=8);c.line([(185,141),(185,171),(208,181)],MINT,7)
    elif cid=='C53':
        c.circle(127,129,90,outline=VIOLET,width=8)
        for i in range(9):
            a=math.radians(-90+i*40);c.line([(127+42*math.cos(a),129+42*math.sin(a)),(127+87*math.cos(a),129+87*math.sin(a))],VIOLET,5)
        c.circle(127,129,39,fill=DARK,outline=INK,width=6);c.text(127,127,'?',55,INK)
        c.polygon([(118,24),(139,24),(128,51)],GOLD)
    elif cid in ('O05','O06'):
        col=MINT if cid=='O05' else GOLD
        c.rect(29,86,198,136,13,outline=INK,width=7)
        c.line([(25,82),(45,42),(211,42),(231,82)],col,9)
        for x in [51,94,137,180]:c.line([(x,47),(x-8,77)],col,6)
        if cid=='O05':
            c.rect(47,116,57,79,7,outline=col,width=6);c.circle(76,155,7,fill=col,outline=None)
        else:
            c.circle(75,164,29,outline=col,width=6);c.rect(60,118,30,19,4,outline=col,width=5)
        c.plus(170,154,29,col,10)
    elif cid=='O07':
        c.line([(32,71),(93,36),(222,36),(222,214),(93,214),(32,178),(32,71)],GOLD,7)
        c.arc(142,124,57,-130,140,MINT,8);c.arrow(99,163,85,145,MINT,7,12);c.text(145,117,'0',72,INK)
        c.circle(60,125,9,outline=GOLD,width=5)
    else:raise ValueError(cid)
    return c


def enchantment(eid):
    c=Symbol();col=dict((e[0],e[1]) for e in ENCHANTS)[eid]
    if eid=='slotless':
        c.rect(77,54,102,128,16,outline=col,width=12);c.minus(129,116,27,col,13)
        c.line([(34,98),(56,83)],INK,10);c.line([(200,83),(222,98)],INK,10);c.line([(95,218),(161,218)],col,9)
    elif eid=='base10':
        c.plus(63,117,25,col,14);c.text(160,111,'10',96,col);c.line([(40,208),(214,208)],INK,9)
    elif eid=='mult2':
        c.circle(128,128,90,outline=col,width=10);c.text(128,119,'×2',91,col)
    elif eid=='submit1':
        c.rect(30,35,120,184,15,outline=col,width=10);c.line([(57,72),(124,72)],col,8);c.line([(57,106),(107,106)],col,8)
        c.plus(190,124,26,col,12);c.text(191,192,'1',57,INK)
    elif eid=='roll1':
        c.polygon([(40,59),(129,59),(146,214),(24,214)],col);c.line([(57,82),(46,188)],DARK,10)
        c.dice(156,29,61,3,INK);c.plus(195,141,25,col,12);c.text(195,204,'1',50,INK)
    else:raise ValueError(eid)
    return c


def frame(eid):
    c=Symbol();col=dict((e[0],e[1]) for e in ENCHANTS)[eid];c.rect(9,9,238,238,22,outline=col,width=6)
    if eid=='slotless':
        for x in [83,119,155]:c.line([(x,20),(x+18,20)],col,4)
    elif eid=='base10':
        for x,y in [(28,28),(228,228)]:c.plus(x,y,11,col,4)
    elif eid=='mult2':
        for x,y in [(28,28),(228,228)]:c.polygon([(x,y-10),(x+10,y),(x,y+10),(x-10,y)],col)
    elif eid=='submit1':
        for x,y in [(25,23),(216,211)]:c.rect(x,y,17,23,3,outline=col,width=3);c.line([(x+4,y+7),(x+13,y+7)],col,2)
    else:
        for x,y in [(26,22),(215,210)]:c.polygon([(x,y),(x+15,y),(x+19,y+22),(x-4,y+22)],col)
    return c


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--samples',action='store_true');args=parser.parse_args()
    PREVIEWS.mkdir(parents=True,exist_ok=True);EVIDENCE.mkdir(parents=True,exist_ok=True)
    ids=['C41','C42','C27','O05'] if args.samples else UPDATED+NEW
    for cid in ids:save_symbol(icon(cid),cid,OUT/'Icons')
    entries=[(cid,OUT/'Icons'/(cid+'.png')) for cid in ids]
    for eid,col,shape in ENCHANTS:
        save_symbol(enchantment(eid),'Enchant_'+eid,OUT/'Textures',128);save_symbol(frame(eid),'Frame_Enchant_'+eid,OUT/'Textures')
        entries.append((eid,OUT/'Textures'/('Enchant_'+eid+'.png')))
    sheet(entries,PREVIEWS/('samples.png' if args.samples else 'NewContentContactSheet.png'),5,160)
    if args.samples:
        print('SAMPLES_READY',PREVIEWS/'samples.png');return
    cfg=json.loads((ROOT/'DicePrototype/Assets/Game/Data/P0Config.json').read_text(encoding='utf-8-sig'))
    definitions=[x for category in ['controllers','consumables','services','diceUpgrades'] for x in cfg[category]]
    assert len(definitions)==71
    manifest=json.loads((OUT/'asset_map.json').read_text(encoding='utf-8-sig'))
    manifest['configVersion']=cfg['configVersion'];manifest['assetSchemaVersion']=3
    manifest['icons']=[{'contentId':x['id'],'iconId':x['id'],'name':x['displayName'],'path':f'Assets/Game/Art/Icons/{x["id"]}.png','source':f'art/source_icons/{x["id"]}.svg','category':{'C':'controller','I':'consumable','O':'service','U':'die_upgrade'}[x['id'][0]]} for x in definitions]
    manifest['enchantments']=[{'id':eid,'badgePath':f'Assets/Game/Art/Textures/Enchant_{eid}.png','badgeSource':f'art/source_icons/Enchant_{eid}.svg','badgeSize':[128,128],'framePath':f'Assets/Game/Art/Textures/Frame_Enchant_{eid}.png','frameSource':f'art/source_icons/Frame_Enchant_{eid}.svg','tint':col,'shape':shape} for eid,col,shape in ENCHANTS]
    sounds=[audio('enchant_base10',[(880,1),(1320,.22)],.16),audio('enchant_mult2',[(1046.5,.65),(1569.75,.45),(2093,.18)],.22),audio('reorder',[(660,.7),(990,.25)],.12),audio('refresh',[(523.25,.45),(659.25,.4),(783.99,.35)],.28),audio('cannon',[(110,.9),(165,.5),(1337,.16),(2191,.09)],.42),audio('debug',[(1174.66,.55),(1567.98,.25)],.13)]
    manifest['audio']=[a for a in manifest['audio'] if not a['id'].startswith('enchant_') and a['id'] not in {s['id'] for s in sounds}]+sounds
    (OUT/'asset_map.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    sheet([(x['id'],OUT/'Icons'/(x['id']+'.png')) for x in definitions],PREVIEWS/'AllContentContactSheet.png',8,128)
    sheet([(cid,OUT/'Icons'/(cid+'.png')) for cid in UPDATED+NEW],PREVIEWS/'NewContentThumbnail64.png',6,64)
    badges=Image.new('RGB',(600,86),(17,31,41));draw=ImageDraw.Draw(badges);font=ImageFont.truetype(FONT,15)
    for i,(eid,_,_) in enumerate(ENCHANTS):
        tile=Image.open(OUT/'Textures'/('Enchant_'+eid+'.png')).convert('RGBA').resize((32,32),Image.Resampling.LANCZOS)
        badges.paste(tile,(i*120+44,8),tile);draw.text((i*120+60,62),eid,fill='white',font=font,anchor='mm')
    badges.save(PREVIEWS/'EnchantmentThumbnail32.png')
    sheet([(e,OUT/'Textures'/('Frame_Enchant_'+e+'.png')) for e,_,_ in ENCHANTS],PREVIEWS/'EnchantmentFrames.png',5,128)
    report={'configVersion':cfg['configVersion'],'newContentIcons':NEW,'updatedExistingIcons':UPDATED,'totalContentIcons':71,'enchantmentBadges':5,'enchantmentFrames':5,'newAudioClips':[a['id'] for a in sounds],'source':'original-editable-vector-and-synthesis','rasterizer':'Pillow','sampleScale':S,'unityImportValidated':False}
    (EVIDENCE/'symbols_generation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');print(json.dumps(report))

if __name__=='__main__':main()
