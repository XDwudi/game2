"""Reviewer-owned inputs and shop/probability oracle. No production imports.

Score expectations use the supplied, inspected Appendix F for cross-language
comparison; probability/shop implementations below were written independently
from prose E, with exact Fraction arithmetic and explicit xorshift32 rejection.
"""
from pathlib import Path
from fractions import Fraction
from collections import Counter
import ast
import hashlib
import json
import random
import re

ROOT = Path(__file__).resolve().parents[3]
SPEC = ROOT / 'docs/specs/P0_v0.5.md'
OUT = Path(__file__).parent
text = SPEC.read_text(encoding='utf-8-sig')
cfg = json.loads(re.findall(r'```json\s*\n(.*?)\n```', text, re.S)[0])
ref = re.findall(r'```python\s*\n(.*?)\n```', text, re.S)[0]
tree = ast.parse(ref)
assert all(isinstance(n, (ast.ImportFrom, ast.Assign, ast.FunctionDef)) for n in tree.body)
assert {n.module for n in tree.body if isinstance(n, ast.ImportFrom)} == {'collections', 'fractions'}
assert not any(isinstance(n, ast.Call) and isinstance(n.func, ast.Name) and n.func.id in {'eval', 'exec', 'open', 'compile', '__import__'} for n in ast.walk(tree))
namespace = {}
exec(compile(tree, 'reviewed-appendix-f', 'exec'), namespace)
r = random.Random(71520261030)
NON = set(cfg['enchantRules']['nonSubmitControllerIds'])
ENCHANTS = [('none', 80), ('slotless', 12), ('base5', 6), ('mult120', 2)]
controllers = {c['id']: c for c in cfg['controllers']}


def probability_oracle(faces, large, small, high, low):
    probability = []
    for face in faces:
        w = Fraction(face['baseWeight'])
        for source in faces:
            opposite = cfg['faceOpposites'][source['faceId']] == face['faceId']
            if source['texture'] == 'smooth':
                w *= Fraction(80 if opposite else 104, 100)
            elif source['texture'] == 'rough':
                w *= Fraction(120 if opposite else 96, 100)
        if (large and face['permanentPip'] == 6) or (small and face['permanentPip'] == 1):
            w *= Fraction(3, 2)
        probability.append(w)
    mass = sum(probability)
    probability = [p / mass for p in probability]
    if high != low:
        indexes = {p: [i for i, f in enumerate(faces) if f['permanentPip'] == p] for p in range(1, 7)}
        receiving = [p for p in (range(4, 7) if high else range(1, 4)) if indexes[p]]
        giving = [i for i, f in enumerate(faces) if (f['permanentPip'] >= 4) != high]
        donor = sum(probability[i] for i in giving)
        if receiving and donor:
            transfer = min(donor, Fraction(3, 20))
            original = probability[:]
            for i in giving:
                probability[i] *= 1 - transfer / donor
            for pip in receiving:
                fraction = transfer / len(receiving) / sum(original[i] for i in indexes[pip])
                for i in indexes[pip]:
                    probability[i] += original[i] * fraction
    full = [p * 1000000 for p in probability]
    result = [int(p) for p in full]
    ranking = sorted(range(6), key=lambda i: (-(full[i] - result[i]), faces[i]['faceId']))
    for i in ranking[:1000000 - sum(result)]:
        result[i] += 1
    return result


class Rng:
    def __init__(self, state):
        self.state = state or 0x9e3779b9

    def index(self, n):
        limit = (0xffffffff // n) * n
        while True:
            x = self.state
            x ^= (x << 13) & 0xffffffff
            x ^= x >> 17
            x ^= (x << 5) & 0xffffffff
            self.state = x
            u = x - 1
            if u < limit:
                return u % n

    def pick(self, pool):
        v = self.index(sum(w for _, w in pool))
        for entry, weight in pool:
            if v < weight:
                return entry
            v -= weight
        raise AssertionError('CDF')


def face(i):
    return {'faceId': f'F{i+1:02}', 'permanentPip': r.randint(1, 6), 'baseColor': r.choice(['red', 'blue']),
            'baseWeight': r.choice([1, 1, 17, 999999, 2147483647]), 'texture': r.choice(['none', 'smooth', 'rough']), 'isGold': r.choice([False, True])}


def shop_oracle(inp):
    rng = Rng(inp['rng'])
    owned = {c['id']: c['enchant'] for c in inp['owned']}
    def replacement(cid):
        return [(e, w) for e, w in ENCHANTS if e != 'none' and e != owned[cid] and (cid not in NON or e == 'slotless')]
    pool = sorted(cid for cid in controllers if cid not in owned or replacement(cid))
    offers = []
    for index in range(3):
        if not pool:
            break
        preferred = [cid for cid in pool if cid not in owned and controllers[cid]['buyPrice'] <= 8]
        selected = rng.pick([(cid, controllers[cid]['offerWeight']) for cid in ((preferred or pool) if index == 0 else pool)])
        enchant = rng.pick(replacement(selected) if selected in owned else ENCHANTS)
        if selected not in owned and selected in NON and enchant in ('base5', 'mult120'):
            enchant = 'none'
        offers.append({'category': 'controller', 'id': selected, 'basePrice': controllers[selected]['buyPrice'], 'enchant': enchant})
        pool.remove(selected)
    cap = max(1, min(8, 5 + ('C21' in owned) + 2 * ('C22' in owned) - ('C09' in owned)))
    hands = [h['id'] for h in cfg['handDefinitions'] if inp['levels'][h['id']] < 8 and
             (4 if h['id'] == 'straight' and 'C29' in owned else h['minSubmittedDice']) <= cap and h['requirePermanentDice'] <= len(inp['dice'])]
    upgrades = []
    flat = [f for d in inp['dice'] for f in d['faces']]
    for uid, legal in [('U01', any(f['texture'] != 'smooth' for f in flat)), ('U02', any(f['texture'] != 'rough' for f in flat)),
                       ('U03', any(f['baseColor'] != 'red' for f in flat)), ('U04', any(f['baseColor'] != 'blue' for f in flat)),
                       ('U05', any(not d['glass'] for d in inp['dice'])), ('U06', any(not f['isGold'] for f in flat)), ('U07', True)]:
        if legal:
            upgrades.append(uid)
    services = ([] if inp['slots'] == 5 else ['O01']) + ([] if inp['discount'] == 3 else ['O02']) + ([] if len(inp['dice']) == 1 else ['O04'])
    def draw_uniform(ids, count, category, catalog, field):
        options = sorted(ids)
        prices = {c['id']: c[field] for c in catalog}
        for _ in range(min(count, len(options))):
            chosen = options.pop(rng.index(len(options)))
            offers.append({'category': category, 'id': chosen, 'basePrice': prices[chosen], 'enchant': None})
    draw_uniform([c['id'] for c in cfg['consumables']], 2, 'consumable', cfg['consumables'], 'baseBuyPrice')
    draw_uniform(hands, 2, 'hand_upgrade', cfg['handDefinitions'], 'baseUpgradePrice')
    draw_uniform(upgrades, 2, 'die_upgrade', cfg['diceUpgrades'], 'buyPrice')
    draw_uniform(services, 1, 'service', cfg['services'], 'baseBuyPrice')
    if len(inp['dice']) < 8:
        for _ in range(2):
            template = rng.index(100)
            limits = (1, 6) if template < 50 else (1, 3) if template < 75 else (4, 6)
            dice = [{'pip': limits[0] + rng.index(limits[1] - limits[0] + 1), 'color': 'red' if rng.index(100) < 40 else 'blue'} for _ in range(6)]
            pips, colors = [d['pip'] for d in dice], [d['color'] for d in dice]
            price = 6 + 2 * (sum(pips) >= 24) + 2 * (sum(pips) <= 13) + 2 * (max(Counter(pips).values()) >= 3) + 2 * (max(Counter(colors).values()) >= 5)
            offers.append({'category': 'new_die', 'id': 'O03', 'basePrice': price, 'enchant': None, 'die': dice})
    return {'offers': offers, 'rng': rng.state}


scores = []
for i in range(1024):
    count = r.randint(1, 8)
    cards = r.sample(list(controllers), r.randint(0, 10))
    enchants = {cid: r.choice(['none', 'slotless'] if cid in NON else [e for e, _ in ENCHANTS]) for cid in cards}
    dice = [{'p': r.randint(1, 6), 'c': r.choice(['red', 'blue']), 'sum6': r.randint(6, 36), 'gold': r.choice([True, False]), 'glass': r.choice([True, False])} for _ in range(count)]
    item = {'id': f'R{i:04}', 'dice': dice, 'rollsRemaining': r.randint(0, 12), 'controllerIds': cards,
            'capacity': r.randint(count, 8), 'levels': {h['id']: r.randint(0, 8) for h in cfg['handDefinitions']},
            'redWave': r.randrange(4) == 0, 'enchants': enchants, 'submissionOrdinal': r.randrange(4)}
    item['expected'] = namespace['evaluate'](dice, item['rollsRemaining'], cards, item['levels'], item['capacity'], item['redWave'], enchants, item['submissionOrdinal'])
    scores.append(item)
probabilities = []
for i in range(512):
    faces = [face(j) for j in range(6)]
    r.shuffle(faces)
    flags = [r.choice([False, True]) for _ in range(4)]
    probabilities.append({'id': f'P{i:04}', 'faces': faces, 'large': flags[0], 'small': flags[1], 'high': flags[2], 'low': flags[3], 'expected': probability_oracle(faces, *flags)})
shops = []
for i in range(128):
    ids = r.sample(list(controllers), r.randint(0, 10))
    item = {'id': f'SHOP{i:03}', 'rng': r.randrange(1, 0x100000000), 'slots': r.randint(2, 5), 'discount': r.randint(0, 3),
            'owned': [{'id': cid, 'enchant': 'slotless' if j >= 4 else r.choice(['none', 'slotless'] if cid in NON else [e for e, _ in ENCHANTS])} for j, cid in enumerate(ids)],
            'levels': {h['id']: r.randint(0, 8) for h in cfg['handDefinitions']},
            'dice': [{'glass': r.choice([False, True]), 'faces': [face(j) for j in range(6)]} for _ in range(r.randint(1, 8))]}
    item['expected'] = shop_oracle(item)
    shops.append(item)
output = {'inputSpecSha256': hashlib.sha256(SPEC.read_bytes()).hexdigest(), 'seed': 71520261030,
          'scores': scores, 'probabilities': probabilities, 'shops': shops}
(OUT / 'independent-vectors.json').write_text(json.dumps(output, ensure_ascii=False, separators=(',', ':')), encoding='utf-8')
print(f'Generated {len(scores)} score, {len(probabilities)} probability, {len(shops)} full-shop vectors')
