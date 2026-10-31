"""Generate exact public config schema; numeric C11 map keys use a narrow loader adapter."""
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
c=json.loads((ROOT/'DicePrototype/Assets/Game/Data/P0Config.json').read_text(encoding='utf-8'))
names={'diceDefinition':'DiceDefinition','faces':'FaceDefinition','faceOpposites':'FaceOpposites','redRewards':'RedReward','handDefinitions':'HandDefinition','colorDefinitions':'ColorDefinition','controllers':'ControllerDefinition','parameters':'ControllerParameters','consumables':'ConsumableDefinition','services':'ServiceDefinition','diceUpgrades':'DieUpgradeDefinition','stages':'StageDefinition','shop':'ShopConfig','controllerWeights':'ControllerWeights','generation':'GenerationConfig','newDieTemplateWeights':'TemplateWeights','rng':'RngConfig','presentation':'PresentationConfig','debug':'DebugConfig','enchantments':'EnchantmentDefinition','enchantRules':'EnchantmentRules','stageFlow':'StageFlowConfig','nonSubmitDrawWeights':'NonSubmitDrawWeights','requiredCounts':'RequiredPipCounts','controllerOrder':'ControllerOrderConfig','display':'DisplayConfig','windowsModes':'PlatformDisplayModes','macModes':'PlatformDisplayModes'}
classes={}
def infer(k,v):
    if v is None and k=='riskTarget':return 'string'
    if isinstance(v,bool):return 'bool'
    if isinstance(v,int):return 'uint' if v>2147483647 else 'int'
    if isinstance(v,float):return 'float'
    if isinstance(v,str):return 'string'
    if isinstance(v,dict):
        n=names[k];add(n,v);return n
    if isinstance(v,list):
        ts={infer(k,x) for x in v};assert len(ts)==1,(k,ts);return ts.pop()+'[]'
    raise ValueError((k,v))
def add(name,obj):
    fields=classes.setdefault(name,{})
    for k,v in obj.items():
        t=infer(k,v);k='pip'+k if k.isdigit() else k
        assert k not in fields or fields[k]==t,(name,k)
        fields[k]=t
add('GameConfig',c)
lines=['// Generated from authoritative P0 v0.6 Appendix A; see tools/generate_v06_dtos.py.','using System;','','namespace DiceCraft.Core','{']
for name,fields in classes.items():
    lines+=['    [Serializable]',f'    public sealed {"partial " if name=="GameConfig" else ""}class {name}','    {']
    lines += [f'        public {t} {k};' for k,t in fields.items()]
    if name in ('GameConfig','DebugConfig'):lines+=['        public int maxControllerSlots => maxControllerInstances;']
    if name=='GameConfig':lines+=['        public int controllerSellPrice => baseControllerSellPrice;']
    if name=='ShopConfig':lines+=['        public int firstControllerGuaranteeMaxPrice => firstControllerGuaranteeMaxBasePrice;','        public int controllerOffers => initialControllerOffers;','        public int consumableOffers => initialConsumableOffers;','        public int handUpgradeOffers => upgradeOffers;']
    if name=='PresentationConfig':lines+=['        public int handbookRows => handbookHandRows + handbookColorRows;']
    lines+=['    }','']
lines+=['}']
(ROOT/'DicePrototype/Assets/Game/Core/Rules/GameConfig.cs').write_text('\n'.join(lines)+'\n',encoding='utf-8',newline='\n')
p=ROOT/'DicePrototype/Assets/Game/Core/Rules/ConfigJsonContract.cs';s=p.read_text(encoding='utf-8');a=s.index('        private static readonly Dictionary');b=s.index('\n\n        public static void Validate',a)
contract='        private static readonly Dictionary<string, string[]> Parameters = new Dictionary<string, string[]>(StringComparer.Ordinal)\n        {\n'
contract+=',\n'.join('            {"'+x['id']+'", new[]{'+','.join('"'+k+'"' for k in x['parameters'])+'}}' for x in c['controllers'])+'\n        };'
p.write_text(s[:a]+contract+s[b:],encoding='utf-8',newline='\n')
