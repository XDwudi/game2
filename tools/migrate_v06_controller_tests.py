from pathlib import Path
import re,sys,json
sys.path.insert(0,str(Path('evidence/v06/review').resolve()))
import supplied_reference as ref
p=Path('DicePrototype/Assets/Game/Tests/EditMode/Rules/ControllerScoreTests.cs');s=p.read_text(encoding='utf-8')
s=s.replace('JsonUtility.FromJson<GameConfig>(json)','JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(json))')
changes=[]
def update(m):
 id,label,pips,colors,old,options=m.groups(); capacity=int(re.search(r'capacity:\s*(\d+)',options).group(1)) if 'capacity:' in options else 5
 sum6=int(re.search(r'permanentSum:\s*(\d+)',options).group(1)) if 'permanentSum:' in options else 21
 dice=[{'p':int(p),'c':'red' if c=='R' else 'blue','sum6':sum6,'gold':False,'glass':False} for p,c in zip(pips.split(','),colors)]
 # Supplied independent Python reference, not the implementation under test.
 got=ref.evaluate(dice,controllers=[] if 'equipped: false' in options else [id],capacity=capacity)
 new=int(got['score']);changes.append({'id':id,'case':label,'old':int(old),'new':new})
 return f'yield return Case("{id}", "{label}", "{pips}", "{colors}", {new}{options});'
s=re.sub(r'yield return Case\("(C\d+)", "([^"]+)", "([^"]+)", "([^"]+)", (\d+)([^\n]*?)\);',update,s)
s=s.replace('Is.EqualTo(expectedScore)','Is.EqualTo(expectedScore.ToString(System.Globalization.CultureInfo.InvariantCulture))')
p.write_text(s,encoding='utf-8',newline='\n')
Path('evidence/v06/rules/migrated-controller-expectations.json').write_text(json.dumps(changes,indent=2),encoding='utf-8')
