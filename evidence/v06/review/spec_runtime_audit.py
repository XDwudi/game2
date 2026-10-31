"""Reviewer-only input consistency checks, not implementation acceptance.

The extracted reference was read in full and its AST/imports inspected before use.
No Unity/game code is loaded and no production path is written.
"""
from collections import Counter
from fractions import Fraction
from itertools import combinations_with_replacement, product
from pathlib import Path
import hashlib
import json
import platform
import sys

import supplied_reference as supplied

HERE = Path(__file__).resolve().parent
CONFIG = json.loads((HERE / 'input-json-0.json').read_text(encoding='utf-8'))
CASES = json.loads((HERE / 'input-json-1.json').read_text(encoding='utf-8'))


def subset_diff(expected, actual, path=''):
    if isinstance(expected, dict):
        issues = []
        for key, value in expected.items():
            if key not in actual:
                issues.append({'path': path + '/' + key, 'missing': value})
            else:
                issues += subset_diff(value, actual[key], path + '/' + key)
        return issues
    return [] if expected == actual else [{'path': path, 'expected': expected, 'actual': actual}]


def independent_baseline(pips, colors, level):
    """A partition-based implementation derived from prose/config, not reference calls.

    Tests ordinary no-controller hands, including all same-value overlaps and
    strengthened low-rank groups overtaking the structural primary.
    """
    counts = sorted(Counter(pips).values(), reverse=True)
    pairs = sum(c >= 2 for c in counts)
    triplets = sum(c >= 3 for c in counts)
    repeated = {'pair': counts[0] >= 2, 'two_pair': pairs >= 2,
                'trips': counts[0] >= 3, 'full_house': len(counts) > 1 and counts[0] >= 3 and counts[1] >= 2,
                'three_pair': pairs >= 3, 'quads': counts[0] >= 4,
                'two_trips': triplets >= 2, 'five_kind': counts[0] >= 5,
                'four_pair': pairs >= 4, 'six_kind': counts[0] >= 6,
                'seven_kind': counts[0] >= 7, 'eight_kind': counts[0] >= 8}
    straight = any(set(range(start, start + 5)) <= set(pips) for start in (1, 2))
    definitions = {d['id']: d for d in CONFIG['handDefinitions']}
    values = {key: d['baseMult100'] + d['incrementPerLevel100'] * level.get(key, 0)
              for key, d in definitions.items()}
    eligible = [key for key, yes in repeated.items() if yes]
    winner = sorted(eligible, key=lambda key: (values[key], definitions[key]['structuralPriority']), reverse=True)
    chosen = winner[0] if winner else None
    structural = eligible + (['straight'] if straight else [])
    primary = max(structural, key=lambda key: definitions[key]['structuralPriority']) if structural else 'scatter'
    hand = max(100, (values[chosen] if chosen else 0) + (values['straight'] if straight else 0))
    color_id = 'all_red' if all(c == 'red' for c in colors) else 'all_blue' if all(c == 'blue' for c in colors) else None
    color = {'all_red': 200, 'all_blue': 300, None: 100}[color_id]
    red_counts = Counter(p for p, c in zip(pips, colors) if c == 'red')
    red_count = max(red_counts.values(), default=0)
    red_mult = [100, 100, 175, 250, 350, 500, 700, 1000, 1400][red_count]
    # Fraction provides a separate exact rational rounding path.
    score = int(Fraction(sum(pips)) * Fraction(hand, 100) * Fraction(color, 100) * Fraction(red_mult, 100))
    return dict(score=str(score), chosenGroupId=chosen, primaryHandId=primary,
                totalHandMult100=hand, colorId=color_id, colorMult100=color,
                redCount=red_count, redMult100=red_mult)


def main():
    problems = []
    golden_checked_fields = 0
    for case in CASES:
        actual = supplied.evaluate(**case['input'])
        differences = subset_diff(case['expected'], actual)
        golden_checked_fields += len(case['expected'])
        if differences:
            problems.append({'case': case['id'], 'differences': differences})

    config_mismatches = []
    for name, rows in [('controllers', supplied.CONTROLLER_ROWS), ('consumables', supplied.CONSUMABLE_ROWS),
                       ('services', supplied.SERVICE_ROWS), ('diceUpgrades', supplied.UPGRADE_ROWS)]:
        entries = {entry['id']: entry for entry in CONFIG[name]}
        for row in rows:
            current = entries[row[0]]
            price = current['baseBuyPrice'] if name in ('consumables','services') else current['buyPrice']
            if current['displayName'] != row[1] or price != row[2]:
                config_mismatches.append({'collection': name, 'id': row[0], 'nameOrPriceMismatch': True})
    for row in supplied.HAND_ROWS:
        current = next(entry for entry in CONFIG['handDefinitions'] if entry['id'] == row[0])
        for key, value in zip(('displayName','baseMult100','incrementPerLevel100','baseUpgradePrice','structuralPriority','minSubmittedDice'), row[1:]):
            if current[key] != value:
                config_mismatches.append({'collection':'handDefinitions','id':row[0],'field':key,'config':current[key],'reference':value})

    independent_count = 0
    independent_mismatches = []
    levels = ({}, {'pair':8, 'two_pair':8, 'trips':8}, {d['id']:8 for d in CONFIG['handDefinitions']})
    digest = hashlib.sha256()
    for length in range(1,9):
        for pips in combinations_with_replacement(range(1,7), length):
            for colors in (['red'] * length, ['blue'] * length, ['red' if i % 2 else 'blue' for i in range(length)]):
                for upgrades in levels:
                    expected = independent_baseline(pips, colors, upgrades)
                    actual = supplied.evaluate([{'p':p,'c':c} for p,c in zip(pips,colors)], capacity=8, levels=upgrades)
                    differences = subset_diff(expected, actual)
                    if differences:
                        independent_mismatches.append({'pips':pips,'colors':colors,'levels':upgrades,'differences':differences})
                    digest.update(json.dumps([pips,colors,upgrades,expected],sort_keys=True).encode())
                    independent_count += 1

    # Exhaust all legal replacement pools and raw tickets by deterministic intervals.
    pools = 0
    tickets = 0
    pool_failures = []
    for controller in CONFIG['controllers']:
        cid = controller['id']
        pool = supplied.enchant_pool(cid)
        total = sum(pool.values())
        if Fraction(pool['none'], total) != Fraction(1,2):
            pool_failures.append({'id':cid,'pool':pool})
        tickets += total
        for old in pool:
            replacement = supplied.enchant_pool(cid,old)
            if 'none' in replacement or old in replacement or not replacement:
                pool_failures.append({'id':cid,'old':old,'pool':replacement})
            pools += 1

    vectors = [
        (0,[166667,166667,166667,166667,166666,166666]),
        (1,[116667,116667,116667,216667,216666,216666]),
        (2,[66667,66667,66667,266667,266666,266666]),
        (3,[16667,16667,16667,316667,316666,316666]),
    ]
    faces = [(f'F{i:02}',i,'none') for i in range(1,7)]
    probability_failures = []
    for delta, expected in vectors:
        actual = supplied.probs(faces,high_count=delta)
        if actual != expected: probability_failures.append({'delta':delta,'expected':expected,'actual':actual})
    duplicate = [(f'F{i:02}',1 if i==1 else 6,'none') for i in range(1,7)]
    actual = supplied.probs(duplicate,high_count=2)
    if actual != [0,200000,200000,200000,200000,200000]:
        probability_failures.append({'duplicate':actual})
    probability_invariant_count = 0
    for pips in combinations_with_replacement(range(1,7),6):
        for delta in range(-3,4):
            pf = [(f'F{i+1:02}',p,'none') for i,p in enumerate(pips)]
            probs = supplied.probs(pf,high_count=max(0,delta),low_count=max(0,-delta))
            if sum(probs) != 1_000_000 or min(probs)<0:
                probability_failures.append({'pips':pips,'delta':delta,'probabilities':probs})
            probability_invariant_count += 1

    # Standalone xorshift protocol check from prose, not game implementation.
    state=1; raw=[]
    for _ in range(5):
        state ^= (state << 13) & 0xffffffff
        state ^= state >> 17
        state ^= (state << 5) & 0xffffffff
        raw.append(state)
    rng_pass = raw == [270369,67634689,2647435461,307599695,2398689233]
    result = {
        'scope':'Input consistency only; supplied Python is not production C# and no player was run.',
        'python':sys.version,'platform':platform.platform(),'source':str(HERE/'supplied_reference.py'),
        'referenceSha256':hashlib.sha256((HERE/'supplied_reference.py').read_bytes()).hexdigest(),
        'goldenCases':len(CASES),'goldenTopLevelExpectedFields':golden_checked_fields,'goldenMismatches':problems,
        'configReferenceMismatches':config_mismatches,
        'independentBaselineCount':independent_count,'independentBaselineDigest':digest.hexdigest(),
        'independentBaselineMismatches':independent_mismatches,
        'enchantFirstDrawTickets':tickets,'ownedLegalPools':pools,'enchantPoolFailures':pool_failures,
        'probabilityFixedVectors':5,'probabilityInvariantCount':probability_invariant_count,'probabilityFailures':probability_failures,
        'rngRawVector':raw,'rngRawVectorPass':rng_pass,
    }
    result['pass'] = not any([problems,config_mismatches,independent_mismatches,pool_failures,probability_failures]) and rng_pass
    (HERE/'spec-runtime-results.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({key:value for key,value in result.items() if not isinstance(value,list)},ensure_ascii=False))
    raise SystemExit(0 if result['pass'] else 1)


if __name__=='__main__':
    main()
