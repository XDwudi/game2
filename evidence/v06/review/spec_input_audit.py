"""Read-only input audit; writes only reviewer evidence under this directory."""
from pathlib import Path
from collections import Counter
import ast
import hashlib
import json
import re
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
INPUT = Path('C:/Users/f/.codex/attachments/e41f74ae-4d05-4335-84e8-70a567f51939')
MD = INPUT / '骰子构筑游戏_AI开发规格_P0_v0.6.md'
WORD = INPUT / '骰子构筑游戏_原型开发策划案_P0_v0.6.docx'

def save(name, data):
    (OUT/name).write_text(json.dumps(data, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def norm(s):
    # Preserve operators, digits and Chinese; strip whitespace and Markdown decoration only.
    return re.sub(r'\s+', '', s.replace('**','').replace('`','').replace('\\',''))

def pnorm(s):
    return ''.join(c for c in s if c.isalnum())

def extract_json(text):
    return [json.loads(s) for s in re.findall(r'```json\s*\n(.*?)\n```', text, re.S)]

def compare(a,b,p=''):
    if type(a) is not type(b): return [{'path':p,'old':a,'new':b}]
    if isinstance(a,dict):
        changes=[]
        for k in sorted(a.keys()|b.keys()):
            path=p+'/'+k
            if k not in a: changes.append({'path':path,'added':b[k]})
            elif k not in b: changes.append({'path':path,'removed':a[k]})
            else: changes.extend(compare(a[k],b[k],path))
        return changes
    if isinstance(a,list):
        if all(isinstance(x,dict) and 'id' in x for x in a+b):
            return compare({x['id']:x for x in a},{x['id']:x for x in b},p)
        return [] if a==b else [{'path':p,'old':a,'new':b}]
    return [] if a==b else [{'path':p,'old':a,'new':b}]

def main():
    text=MD.read_text(encoding='utf-8-sig')
    blocks=extract_json(text)
    for i,b in enumerate(blocks): save(f'input-json-{i}.json',b)
    old=extract_json((ROOT/'docs/specs/P0_v0.5.md').read_text(encoding='utf-8-sig'))[0]
    save('v05-v06-config-diff.json',compare(old,blocks[0]))
    main_text=text.split('# AI开发附录')[0]
    ns={'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
    with zipfile.ZipFile(WORD) as z:
        doc=ET.fromstring(z.read('word/document.xml'))
        body=doc.find('w:body',ns)
        table_rows=[]; paras=[]; all_blocks=[]
        for el in body:
            if el.tag.endswith('}tbl'):
                for row in el.findall('w:tr',ns):
                    cells=[''.join(t.text or '' for t in c.findall('.//w:t',ns)) for c in row.findall('w:tc',ns)]
                    table_rows.append(cells);all_blocks.append(' | '.join(cells))
            elif el.tag.endswith('}p'):
                p=''.join(t.text or '' for t in el.findall('.//w:t',ns))
                if p: paras.append(p);all_blocks.append(p)
        comment_names=[n for n in z.namelist() if n.startswith('word/comments')]
        revisions={tag:len(doc.findall('.//w:'+tag,ns)) for tag in ['ins','del']}
    (OUT/'word-main-extracted.txt').write_text('\n'.join(all_blocks)+'\n',encoding='utf-8')
    md_rows=[];md_paras=[]
    for line in main_text.splitlines():
        line=line.strip()
        if not line or line.startswith('<!--'):continue
        if line.startswith('|'):
            cells=[x.strip() for x in line.strip('|').split('|')]
            if all(re.fullmatch(r'[-: ]+',x) for x in cells):continue
            md_rows.append(cells)
        elif not line.startswith('#') and not line.startswith('- [') and not line.startswith('$$'):
            md_paras.append(line)
    row_mismatch=[]
    for i in range(max(len(md_rows),len(table_rows))):
        a=md_rows[i] if i<len(md_rows) else None;b=table_rows[i] if i<len(table_rows) else None
        if a is None or b is None or [norm(x) for x in a]!=[norm(x) for x in b]:
            row_mismatch.append({'index':i,'md':a,'word':b})
    joined=pnorm('\n'.join(all_blocks))
    missing=[p for p in md_paras if pnorm(p) and pnorm(p) not in joined]
    save('word-md-comparison.json',{'mdRows':len(md_rows),'wordRows':len(table_rows),'operatorPreservingMismatches':row_mismatch,'mdParagraphs':len(md_paras),'wordParagraphs':len(paras),'missingParagraphs':missing,'commentsParts':comment_names,'revisions':revisions,'scope':'Content comparison only; no layout/render acceptance.'})
    cfg=blocks[0]; golden=blocks[1]
    requirement_rows=[]
    for line_number,line in enumerate(text.splitlines(),1):
        match=re.match(r'^\|\s*((?:E|P)\d{2,3})\s*\|',line)
        if match:
            requirement_rows.append({'id':match.group(1),'line':line_number,'cells':[cell.strip() for cell in line.strip().strip('|').split('|')]})
    save('required-acceptance-rows.json',requirement_rows)
    changes=compare(old,cfg)
    counts=Counter(c['path'].split('/')[1] for c in changes)
    diff_lines=['# v0.5 → v0.6 配置迁移索引','',
        '此表是原样配置差异索引，不是对原规格的修改。完整逐字段差异见 `v05-v06-config-diff.json`。',
        '',f'- 递归差异总数：{len(changes)}；列表按稳定 ID 比较。',
        '- 新控制器 C41–C53；C04/C16/C23 退役且不复用 ID。',
        '- 服务 O05–O07 新增；I05 名称更新但稳定 ID 保留。',
        '', '| 配置域 | 递归差异项 |','|---|---:|']
    diff_lines += [f'| {key} | {count} |' for key,count in sorted(counts.items())]
    for collection in ('controllers','consumables','services','diceUpgrades','handDefinitions'):
        prior={v['id']:v for v in old[collection]}; current={v['id']:v for v in cfg[collection]}
        diff_lines += ['',f'## {collection}','','| ID | 旧价 | 新价 | 修改字段 |','|---|---:|---:|---|']
        for identity in sorted(prior.keys() | current.keys()):
            before=prior.get(identity); after=current.get(identity)
            key=next((k for k in ('buyPrice','baseBuyPrice','baseUpgradePrice') if k in (after or before)),None)
            old_price=before.get(key,'—') if before else '—';new_price=after.get(key,'—') if after else '—'
            if before is None:fields='新增'
            elif after is None:fields='退役'
            else:
                delta=compare(before,after)
                fields=', '.join(sorted({c['path'].split('/')[1] for c in delta})) or '无'
            diff_lines.append(f'| {identity} | {old_price} | {new_price} | {fields} |')
    (OUT/'v05-v06-migration-index.md').write_text('\n'.join(diff_lines)+'\n',encoding='utf-8')
    codes=re.findall(r'```python\s*\n(.*?)\n```',text,re.S)
    if codes:
        code=codes[-1]
        (OUT/'supplied_reference.py').write_text(code+'\n',encoding='utf-8')
        tree=ast.parse(code)
        imports=[ast.unparse(n) for n in ast.walk(tree) if isinstance(n,(ast.Import,ast.ImportFrom))]
        top=[{'type':type(n).__name__,'line':getattr(n,'lineno',None),'name':getattr(n,'name',None)} for n in tree.body]
        calls=Counter(ast.unparse(n.func) for n in ast.walk(tree) if isinstance(n,ast.Call))
        save('reference-static-inspection.json',{'imports':imports,'topLevel':top,'calls':dict(calls),'executedByExtractionStep':False,'executionEvidence':'spec-runtime-results.json is a separate reviewed runtime check.'})
    cases=golden if isinstance(golden,list) else golden.get('cases',golden.get('goldenCases',[]))
    save('input-structure.json',{
        'reviewer':'/root/v06_spec_review','sourceFiles':[{'path':str(p),'sha256':digest(p),'bytes':p.stat().st_size} for p in [MD,WORD]],
        'jsonBlocks':len(blocks),'configFields':len(cfg),'arrays':{k:len(v) for k,v in cfg.items() if isinstance(v,list)},
        'goldenType':type(golden).__name__,'goldenKeys':list(golden) if isinstance(golden,dict) else [],'goldenCount':len(cases),
        'goldenFirst':cases[0] if cases else None,'goldenIdUnique':len({x.get('id') for x in cases})==len(cases),
        'controllerIds':[c['id'] for c in cfg['controllers']],'configDiffCount':len(compare(old,cfg)),
        'wordRowMismatches':len(row_mismatch),'wordMissingParagraphs':len(missing)
    })
    print(json.dumps({'jsonBlocks':len(blocks),'counts':{k:len(v) for k,v in cfg.items() if isinstance(v,list)},'goldenCount':len(cases),'wordRows':len(table_rows),'mdRows':len(md_rows),'rowMismatches':len(row_mismatch),'paragraphMismatches':len(missing),'configChanges':len(compare(old,cfg))},ensure_ascii=False))

if __name__=='__main__': main()
