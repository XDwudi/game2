"""Rebuild serializable config DTO fields from the frozen v0.5 JSON contract."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
c = json.loads((ROOT/'DicePrototype/Assets/Game/Data/P0Config.json').read_text(encoding='utf-8'))
names = {'diceDefinition':'DiceDefinition','faces':'FaceDefinition','faceOpposites':'FaceOpposites',
 'redRewards':'RedReward','handDefinitions':'HandDefinition','controllers':'ControllerDefinition',
 'parameters':'ControllerParameters','consumables':'ConsumableDefinition','services':'ServiceDefinition',
 'diceUpgrades':'DieUpgradeDefinition','stages':'StageDefinition','shop':'ShopConfig','controllerWeights':'ControllerWeights',
 'generation':'GenerationConfig','newDieTemplateWeights':'TemplateWeights','rng':'RngConfig',
 'presentation':'PresentationConfig','debug':'DebugConfig','enchantments':'EnchantmentDefinition',
 'enchantRules':'EnchantmentRules','stageFlow':'StageFlowConfig'}
classes={}
def infer(k,v):
    if isinstance(v,bool): return 'bool'
    if isinstance(v,int): return 'uint' if v>2147483647 else 'int'
    if isinstance(v,float): return 'float'
    if isinstance(v,str): return 'string'
    if isinstance(v,dict):
        name=names[k]; add(name,v); return name
    if isinstance(v,list):
        if not v: raise ValueError(k)
        ts={infer(k,x) for x in v}
        assert len(ts)==1, (k,ts)
        return ts.pop()+'[]'
    raise ValueError((k,v))
def add(name, obj):
    fields=classes.setdefault(name,{})
    for k,v in obj.items():
        t=infer(k,v)
        assert k not in fields or fields[k]==t
        fields[k]=t
add('GameConfig',c)
lines=['// Serializable fields mirror authoritative P0 v0.5 Appendix A.','using System;','','namespace DiceCraft.Core','{']
for name, fields in classes.items():
    lines += ['    [Serializable]', f'    public sealed {"partial " if name=="GameConfig" else ""}class {name}', '    {']
    lines += [f'        public {t} {k};' for k,t in fields.items()]
    if name in ('GameConfig','DebugConfig'): lines += ['        // Source compatibility only: this is the total instance limit, never ordinary occupied slots.', '        public int maxControllerSlots => maxControllerInstances;']
    if name=='ShopConfig': lines += ['        public int firstControllerGuaranteeMaxPrice => firstControllerGuaranteeMaxBasePrice;']
    lines += ['    }','']
lines+=['}']
(ROOT/'DicePrototype/Assets/Game/Core/Rules/GameConfig.cs').write_text('\n'.join(lines)+'\n',encoding='utf-8',newline='\n')
# Required per-controller keys remain explicit to detect missing zero/false parameters.
p=ROOT/'DicePrototype/Assets/Game/Core/Rules/ConfigJsonContract.cs'
s=p.read_text(encoding='utf-8')
a=s.index('        private static readonly Dictionary'); b=s.index('\n\n        public static void Validate',a)
contract='        private static readonly Dictionary<string, string[]> Parameters = new Dictionary<string, string[]>(StringComparer.Ordinal)\n        {\n'
contract += ',\n'.join('            {"'+x['id']+'", new[]{'+','.join('"'+k+'"' for k in x['parameters'])+'}}' for x in c['controllers'])
contract += '\n        };'
p.write_text(s[:a]+contract+s[b:],encoding='utf-8',newline='\n')
