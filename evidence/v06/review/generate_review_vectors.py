"""Reviewer-owned differential inputs. Pure score oracle is the reviewed spec;
probability implementation below is separately authored from prose. No game imports.
"""
from collections import Counter
from fractions import Fraction
from pathlib import Path
import json
import random

import supplied_reference as reference

HERE=Path(__file__).resolve().parent
CFG=json.loads((HERE/'input-json-0.json').read_text(encoding='utf-8'))
R=random.Random(20261031606)
ENCHANTS=['none','slotless','base10','mult2','submit1','roll1']
IDS=[c['id'] for c in CFG['controllers']]


def probability_oracle(faces,large,small,high,low):
    probabilities=[]
    for face in faces:
        w=Fraction(face['baseWeight'])
        for source in faces:
            opposite=CFG['faceOpposites'][source['faceId']]==face['faceId']
            if source['texture']=='smooth':w*=Fraction(80 if opposite else 104,100)
            if source['texture']=='rough':w*=Fraction(120 if opposite else 96,100)
        if large and face['permanentPip']==6:w*=Fraction(3,2)
        if small and face['permanentPip']==1:w*=Fraction(3,2)
        probabilities.append(w)
    total=sum(probabilities)
    probabilities=[p/total for p in probabilities]
    net=high-low
    if net:
        is_high=net>0
        donor=[i for i,f in enumerate(faces) if (f['permanentPip']>=4)!=is_high]
        receiving={p:[i for i,f in enumerate(faces) if f['permanentPip']==p]
                   for p in (range(4,7) if is_high else range(1,4))}
        receiving={p:indexes for p,indexes in receiving.items() if indexes}
        donor_mass=sum(probabilities[i] for i in donor)
        if donor_mass and receiving:
            transfer=min(donor_mass,Fraction(3*abs(net),20))
            original=probabilities[:]
            for i in donor:probabilities[i]*=1-transfer/donor_mass
            for indexes in receiving.values():
                original_digit_mass=sum(original[i] for i in indexes)
                for i in indexes:probabilities[i]+=transfer/len(receiving)*original[i]/original_digit_mass
    scaled=[p*1000000 for p in probabilities]
    result=[int(p) for p in scaled]
    ranked=sorted(range(6),key=lambda i:(-(scaled[i]-result[i]),faces[i]['faceId']))
    for i in ranked[:1000000-sum(result)]:result[i]+=1
    return result


def controller(cid,serial,enchant='none',alive=True):
    return {'instanceId':f'RV{serial:03d}','definitionId':cid,'enchantmentId':enchant,'alive':alive,
            'state':{'activeColor':R.choice(['red','blue']) if cid=='C45' else None,
                     'turnoverUnits2':R.choice([3,4,5,19,2001]) if cid=='C51' else 3,
                     'fundAccrual':3*R.randrange(10) if cid in ('C41','C52') else 0}}


def complete(data,label):
    data.setdefault('rolls_remaining',0);data.setdefault('levels',{});data.setdefault('color_levels',{})
    data.setdefault('capacity',8);data.setdefault('red_wave',False);data.setdefault('submission_ordinal',0)
    data.setdefault('wallet',0);data.setdefault('previous_primary',None);data.setdefault('bindings',None)
    data.setdefault('debug',True)
    data.setdefault('random_units2',{c['instanceId']:R.choice([2,4,6,8,10,12,14,16,1])
                          for c in data['controllers'] if c['definitionId']=='C53' and c['alive']})
    expected=reference.evaluate(**data)
    # The production DTO uses its established factor/trigger names; keep canonical
    # numerical output and after-score contract instead of testing cosmetic aliases.
    fields=['effectivePips','effectiveColors','pipContributions','unusedRollBonus','pairKinds','pairBaseAdd',
            'walletBaseAdd','emptySlotBaseAdd','steadyBaseAdd','enchantBaseAdd','baseTotal','chosenGroupId',
            'primaryHandId','groupMult100','straightMult100','totalHandMult100','colorId','colorMult100',
            'redValue','redCount','redRewardCapped','redMult100','controllerMultiplierNumerator',
            'controllerMultiplierDenominator','triggeredSourceIds','enchantMultiplierCount','scoreNumerator',
            'scoreDenominator','score','afterScorePlan']
    return {'id':label,'input':data,'expected':{f:expected[f] for f in fields}}


def main():
    score=[]
    trigger_pips={'C07':[1,4],'C11':[2,2,3,3],'C15':[1,2,3,4,5],'C20':[1,2,3,4,6],
                  'C27':[1,1,2,2,3,3],'C28':[1,1,2,2,3,3],'C29':[1,2,3,4],
                  'C30':[2,2,2,4],'C31':[2,2,2,2],'C32':[3,3],'C33':[6,2],
                  'C36':[1,6],'C37':[1,6],'C40':[1,3,5],'C43':[2,2],'C49':[5,5,3]}
    for cid in IDS:
        for e in ENCHANTS:
            for variant in range(2):
                cards=[controller('C41',1,e),controller(cid,2,ENCHANTS[(ENCHANTS.index(e)+1)%6])]
                if cid=='C41':cards.append(controller('C46',3,'base10'))
                pips=trigger_pips.get(cid,[1,1,4,4,6]) if variant==0 else [6]
                colors=['red' if i%2==0 else 'blue' for i in range(len(pips))] if variant==0 else ['blue']
                dice=[{'p':p,'c':c,'sum6':12 if i==0 else 21,'gold':i%2==0,'glass':i%3==0}
                      for i,(p,c) in enumerate(zip(pips,colors))]
                inp={'dice':dice,'controllers':cards,'capacity':5 if cid=='C20' and variant==0 else 8,
                     'wallet':1000 if variant==0 else 0,'rolls_remaining':variant,
                     'levels':{'pair':8,'trips':4},'previous_primary':'pair' if cid=='C43' else None}
                score.append(complete(inp,f'MIMIC-{cid}-{e}-{variant}'))
    for extreme in ('C45','C51','C42'):
        cards=[controller(extreme,i+1,'mult2') for i in range(32)]
        for c in cards:
            if extreme=='C45':c['state']['activeColor']='red'
            if extreme=='C51':c['state']['turnoverUnits2']=2001
        score.append(complete({'controllers':cards,'dice':[{'p':6,'c':'red','sum6':36,'gold':True,'glass':True} for _ in range(32)],
                               'capacity':32,'levels':{'eight_kind':100},'color_levels':{'all_red':100},
                               'rolls_remaining':999,'wallet':1000000000},f'EXTREME-32-{extreme}'))
    score.append(complete({'controllers':[controller('C41',1),controller('C53',2)],
                          'dice':[{'p':1,'c':'blue'}],'random_units2':{'RV002':1}},'EXTREME-SHARED-HALF'))
    while len(score)<1024:
        debug=R.random()<0.55
        count=R.randrange(1,33) if debug else R.randrange(1,9)
        capacity=R.randrange(count,33) if debug else R.randrange(count,9)
        card_count=R.randrange(0,33) if debug else R.randrange(0,5)
        definitions=[R.choice(IDS) for _ in range(card_count)] if debug else R.sample(IDS,card_count)
        cards=[]
        for i,cid in enumerate(definitions):
            enchant=R.choice(ENCHANTS if debug else list(reference.enchant_pool(cid)))
            cards.append(controller(cid,i+1,enchant))
        if not debug:
            capacity=reference.effective_capacity(cards)
            count=R.randrange(1,capacity+1)
        dice=[{'p':R.randrange(1,7),'c':R.choice(['red','blue']),'sum6':R.randrange(6,37),
               'gold':R.choice([False,True]),'glass':R.choice([False,True])} for _ in range(count)]
        maxlevel=100 if debug else 8
        inp={'dice':dice,'controllers':cards,'capacity':capacity,'debug':debug,'red_wave':R.random()<.2,
             'wallet':R.choice([0,1,999,1000000000]),'rolls_remaining':R.randrange(0,1000) if debug else R.randrange(0,8),
             'levels':{h['id']:R.randrange(maxlevel+1) for h in CFG['handDefinitions']},
             'color_levels':{h['id']:R.randrange(maxlevel+1) for h in CFG['colorDefinitions']},
             'submission_ordinal':R.randrange(3),'previous_primary':R.choice([h['id'] for h in CFG['handDefinitions']]+['scatter',None])}
        if R.random()<.15:
            inp['bindings']={c['instanceId']:'missing-owner' for c in cards if c['definitionId']=='C41'}
        score.append(complete(inp,f'FUZZ-{len(score):04d}'))
    probabilities=[]
    for i in range(512):
        faces=[{'faceId':f'F{j+1:02}','permanentPip':R.randrange(1,7),'baseColor':R.choice(['red','blue']),
                'baseWeight':R.choice([1,17,999999,2147483647]),'texture':R.choice(['none','smooth','rough']),
                'isGold':R.choice([True,False])} for j in range(6)]
        R.shuffle(faces)
        large,small=R.choice([True,False]),R.choice([True,False])
        high,low=R.randrange(0,33),R.randrange(0,33)
        probabilities.append({'id':f'PROB-{i:04}','faces':faces,'large':large,'small':small,'high':high,'low':low,
                              'expected':probability_oracle(faces,large,small,high,low)})
    result={'seed':20261031606,'scoreOracle':'Reviewed supplied reference, differential testing, not an independent full-controller oracle.',
            'probabilityOracle':'Reviewer Fraction implementation from prose.','scores':score,'probabilities':probabilities}
    (HERE/'review-vectors.json').write_text(json.dumps(result,ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8')
    print(json.dumps({'scores':len(score),'probabilities':len(probabilities),'maxScoreDigits':max(len(x['expected']['score']) for x in score),
                      'scoresBeyondInt64':sum(int(x['expected']['score'])>9223372036854775807 for x in score)}))


if __name__=='__main__':main()
