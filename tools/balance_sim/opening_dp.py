"""没有任何卡的起手：精确动态规划（5 颗标准骰、0 级牌型、无控制器 / 道具 / 骰面改造）。

用途：比较颜色规则预设对“裸起手”的影响。整理自 V1.1.1 平衡分析时的临时脚本
（opening_dp.py / colour_grid.py / colour_grid2.py / colour_blind.py），以 colour_grid.py
的 Model 写法为基础。只覆盖“没有任何卡的起手”，不代表带构筑的对局。

规则模型
--------
* 5 颗标准骰，面 1..6；1 点和 4 点是红面，2/3/5/6 是蓝面。
* 每手最多掷 3 次。每次掷完可以把任意几颗未锁骰锁定，锁定不可逆；未锁的骰子在下次投掷
  全部重掷。随时可以提交（至少 1 颗），提交的就是已锁定的骰子，可以不交满 5 颗。
  第 3 次掷完必须提交。
* 得分 = floor((提交骰点数和 + 3 × 剩余未用投掷次数) × 牌型倍率 × 颜色倍率 × 红组倍率)。
  floor 用精确有理数计算（Fraction），不受浮点误差影响。
* 牌型倍率（hand_mult）：五豹子 12、四条 8、三条带一对 6、三条 4、两对 3、一对 2、散点 1；
  含 1-2-3-4-5 或 2-3-4-5-6 即顺子：散点时为 10，与同点牌型并存时两者相加（如一对+顺子 = 12）。
  只有 5 颗骰时顺子不可能与同点牌型并存，相加规则在本模型的起手里不会触发（多骰时才会）。
* 颜色倍率：提交的骰子全部是蓝面 → blue；全部是红面 → red；混色 → 1。
* 红组：提交中“同点数红骰”的最大组（1 点组与 4 点组分别计数，取大者）。红组 k 颗的倍率为
  red_group[k]，k ≤ 1 时为 1。全红手牌同样吃红组倍率（两者相乘）。
* 预设：
  - p110（现行）：全蓝 ×3、全红 ×2；红组 2/3/4/5 = ×1.75/×2.5/×3.5/×5。
  - p111（新）：全蓝 ×1.25、全红 ×1.5（无卡时 加成 +0.25 / +0.5 等价于这两个倍数）；
    红组 2/3/4/5 = ×1/×2/×3/×4.5。

策略
----
* 最优打法：每手独立地最大化期望分（不看目标、不看前几手结果）。
* 不看颜色的玩家：按“关闭颜色与红组”的规则求出最优策略（只凑牌型和点数），
  再用真实规则给它的最终手牌计分。
* 平局（多个选择期望相同）按确定性的枚举顺序取第一个：锁定子集按排序后的元组字典序，
  同一子集先“提交”后“继续掷”。对最优打法，平局不影响期望分，只可能影响手牌分布类指标。
* 对不看颜色的玩家，平局（关闭颜色时同分、但真实计分不同）会改变真实期望分，且没有
  “正确”的取法；因此另给出 ``tieRange``：平局全部朝真实计分最不利 / 最有利方向取时的期望分。

指标口径（都在“最终提交的手牌”的分布上取概率）
----------------------------------------------
* ``all_blue`` 全蓝：提交的骰子全部是蓝面（不论交了几颗）。
* ``all_red`` 全红：提交的骰子全部是红面。
* ``red_group_only`` 仅红组收益：混色（既非全蓝也非全红），且红组倍率 > 1。
* ``no_colour_bonus`` 没有任何颜色收益：颜色倍率 = 1 且红组倍率 = 1。按预设规则
  （全蓝 / 全红倍率都 ≠ 1）这等于“既不是全蓝也不是全红，红组倍率也是 ×1”。
  上面四项在 p110 / p111 下互斥且相加为 100%。
* ``red_group_3plus`` 红组 3 颗以上：最大同点红组 ≥ 3 颗。与上面的分类重叠
  （例如 4-4-4-1-1 既是全红也是红组 3 颗）。
* ``straight`` 含顺子：牌型含顺子（含“同点牌型+顺子”）。
* ``short_submit`` 没交满：提交少于 5 颗。
* ``pass3[t]`` 三手累计达标：三手独立同分布、每手都按同一策略，三手得分之和 ≥ t 的概率。
  单手分布为精确分数，卷积用浮点计算。

命令行
------
    python tools/balance_sim/opening_dp.py --rules p111 --targets 160,320,500
    python tools/balance_sim/opening_dp.py --rules p110 --json
    python tools/balance_sim/opening_dp.py --rules p111 --blue 1.5 --red-group 1,2,3,4.5
"""
from __future__ import annotations

import argparse
import bisect
import io
import itertools
import json
import sys
from collections import Counter, defaultdict
from dataclasses import dataclass, replace
from fractions import Fraction
from functools import lru_cache
from math import factorial

RED_FACES = frozenset({1, 4})
DICE = 5
MAX_ROLLS = 3
UNUSED_ROLL_BONUS = 3
STRAIGHT_MULT = 10
STRAIGHTS = (frozenset({1, 2, 3, 4, 5}), frozenset({2, 3, 4, 5, 6}))


@dataclass(frozen=True)
class Rules:
    """颜色规则。red_group 依次为红组 2/3/4/5 颗的倍率。"""
    name: str
    blue: Fraction
    red: Fraction
    red_group: tuple
    colour_on: bool = True

    def red_group_mult(self, k: int) -> Fraction:
        return Fraction(1) if k < 2 else self.red_group[k - 2]


def make_rules(name, blue, red, red_group, colour_on=True) -> Rules:
    rg = tuple(Fraction(str(x)) for x in red_group)
    if len(rg) != 4:
        raise ValueError("red_group 需要 4 个数（红组 2/3/4/5 颗）")
    return Rules(name, Fraction(str(blue)), Fraction(str(red)), rg, colour_on)


PRESETS = {
    "p110": make_rules("p110", 3, 2, (1.75, 2.5, 3.5, 5)),
    "p111": make_rules("p111", 1.25, 1.5, (1, 2, 3, 4.5)),
}
COLOUR_OFF = make_rules("colour_off", 1, 1, (1, 1, 1, 1), colour_on=False)


def hand_mult(dice):
    """返回 (牌型名, 牌型倍率)。顺子与同点牌型并存时倍率相加。"""
    counts = sorted(Counter(dice).values(), reverse=True)
    name, mult = "散点", 1
    if counts[0] >= 5:
        name, mult = "五豹子", 12
    elif counts[0] == 4:
        name, mult = "四条", 8
    elif counts[0] == 3 and len(counts) > 1 and counts[1] >= 2:
        name, mult = "三条带一对", 6
    elif counts[0] == 3:
        name, mult = "三条", 4
    elif counts[0] == 2 and len(counts) > 1 and counts[1] == 2:
        name, mult = "两对", 3
    elif counts[0] == 2:
        name, mult = "一对", 2
    faces = set(dice)
    if any(faces >= s for s in STRAIGHTS):
        if mult == 1:
            name, mult = "顺子", STRAIGHT_MULT
        else:
            name, mult = name + "+顺子", mult + STRAIGHT_MULT
    return name, mult


def red_group_size(dice) -> int:
    reds = [x for x in dice if x in RED_FACES]
    return max(Counter(reds).values()) if reds else 0


def colour_mults(dice, rules: Rules):
    """返回 (颜色倍率, 红组倍率)。"""
    if not rules.colour_on or not dice:
        return Fraction(1), Fraction(1)
    nred = sum(1 for x in dice if x in RED_FACES)
    col = rules.red if nred == len(dice) else rules.blue if nred == 0 else Fraction(1)
    return col, rules.red_group_mult(red_group_size(dice))


def score(dice, unused, rules: Rules) -> int:
    """一手提交的得分。dice 为提交的骰子，unused 为剩余未用投掷次数。"""
    if not dice:
        return 0
    _, hm = hand_mult(dice)
    col, rg = colour_mults(dice, rules)
    base = sum(dice) + UNUSED_ROLL_BONUS * unused
    v = base * hm * col * rg
    return v.numerator // v.denominator


@lru_cache(None)
def roll_outcomes(n):
    """掷 n 颗骰的所有无序结果及其精确概率：[(排序元组, Fraction)]。"""
    out = []
    for combo in itertools.combinations_with_replacement(range(1, 7), n):
        ways = factorial(n)
        for v in Counter(combo).values():
            ways //= factorial(v)
        out.append((combo, Fraction(ways, 6 ** n)))
    return tuple(out)


@lru_cache(None)
def sub_multisets(combo):
    """combo 的全部子多重集（排序元组），按确定性顺序排列。"""
    return tuple(sorted({s for k in range(len(combo) + 1) for s in itertools.combinations(combo, k)}))


class Solver:
    """在给定规则下求期望分最大的策略，并给出最终手牌分布。"""

    def __init__(self, rules: Rules):
        self.rules = rules
        self._score, self._w, self._best, self._dist = {}, {}, {}, {}

    def score(self, dice, unused):
        key = (dice, unused)
        v = self._score.get(key)
        if v is None:
            v = self._score[key] = score(dice, unused, self.rules)
        return v

    def roll_value(self, locked, r):
        """已锁 locked、还有 r (≥1) 次投掷、即将投掷时的最优期望分。"""
        key = (locked, r)
        v = self._w.get(key)
        if v is None:
            v = sum(p * self.best(locked, combo, r - 1)[0] for combo, p in roll_outcomes(DICE - len(locked)))
            self._w[key] = v
        return v

    def best(self, locked, combo, remaining):
        """刚掷出 combo、之后还剩 remaining 次投掷时的最优 (价值, (新锁定, 动作))。动作 0=提交 1=继续掷。"""
        key = (locked, combo, remaining)
        hit = self._best.get(key)
        if hit is not None:
            return hit
        bv, bc = -1, None
        for s in sub_multisets(combo):
            new = tuple(sorted(locked + s))
            if new:
                v = self.score(new, remaining)
                if v > bv:
                    bv, bc = v, (new, 0)
            if remaining > 0 and len(new) < DICE:
                v = self.roll_value(new, remaining)
                if v > bv:
                    bv, bc = v, (new, 1)
        self._best[key] = (bv, bc)
        return bv, bc

    def expected_value(self) -> Fraction:
        return self.roll_value((), MAX_ROLLS)

    def final_distribution(self, locked=(), r=MAX_ROLLS):
        """最优策略下最终提交的分布：{(提交骰子, 剩余投掷): Fraction}。"""
        key = (locked, r)
        d = self._dist.get(key)
        if d is not None:
            return d
        acc = defaultdict(Fraction)
        for combo, p in roll_outcomes(DICE - len(locked)):
            _, (new, act) = self.best(locked, combo, r - 1)
            if act == 0:
                acc[(new, r - 1)] += p
            else:
                for k, q in self.final_distribution(new, r - 1).items():
                    acc[k] += p * q
        d = self._dist[key] = dict(acc)
        return d


class TieBoundEvaluator:
    """不看颜色的玩家在平局时怎么选并无依据。本类在“只取按 policy 规则为最优的动作”的约束下，
    让平局全部朝真实规则下最有利（pick=max）或最不利（pick=min）的方向选，给出其期望分的上下界。"""

    def __init__(self, policy: Solver, real: Rules, pick=max):
        self.policy, self.real, self.pick = policy, real, pick
        self._w, self._b = {}, {}

    def roll_value(self, locked, r):
        key = (locked, r)
        v = self._w.get(key)
        if v is None:
            v = sum(p * self.best(locked, combo, r - 1) for combo, p in roll_outcomes(DICE - len(locked)))
            self._w[key] = v
        return v

    def best(self, locked, combo, remaining):
        key = (locked, combo, remaining)
        v = self._b.get(key)
        if v is not None:
            return v
        target = self.policy.best(locked, combo, remaining)[0]
        cands = []
        for s in sub_multisets(combo):
            new = tuple(sorted(locked + s))
            if new and self.policy.score(new, remaining) == target:
                cands.append(Fraction(score(new, remaining, self.real)))
            if remaining > 0 and len(new) < DICE and self.policy.roll_value(new, remaining) == target:
                cands.append(self.roll_value(new, remaining))
        v = self._b[key] = self.pick(cands)
        return v

    def expected_value(self) -> Fraction:
        return self.roll_value((), MAX_ROLLS)


@lru_cache(None)
def solver_for(rules: Rules) -> Solver:
    return Solver(rules)


def score_distribution(finals, rules: Rules):
    """把最终手牌分布按 rules 计分，返回 {得分: Fraction}。"""
    out = defaultdict(Fraction)
    for (dice, unused), p in finals.items():
        out[score(dice, unused, rules)] += p
    return dict(out)


def hand_shares(finals, rules: Rules):
    """按模块 docstring 的口径统计最终手牌占比（Fraction）。"""
    keys = ("all_blue", "all_red", "red_group_only", "no_colour_bonus",
            "red_group_3plus", "straight", "short_submit")
    sh = {k: Fraction(0) for k in keys}
    for (dice, _), p in finals.items():
        nred = sum(1 for x in dice if x in RED_FACES)
        col, rg = colour_mults(dice, rules)
        if nred == 0:
            sh["all_blue"] += p
        elif nred == len(dice):
            sh["all_red"] += p
        elif rg > 1:
            sh["red_group_only"] += p
        if col == 1 and rg == 1:
            sh["no_colour_bonus"] += p
        if red_group_size(dice) >= 3:
            sh["red_group_3plus"] += p
        if "顺子" in hand_mult(dice)[0]:
            sh["straight"] += p
        if len(dice) < DICE:
            sh["short_submit"] += p
    return sh


def pass_probability(dist, target, hands=3) -> float:
    """hands 手独立同分布的得分之和 ≥ target 的概率（浮点卷积）。"""
    one = sorted((s, float(p)) for s, p in dist.items())
    total = {0: 1.0}
    for _ in range(hands - 1):
        nxt = defaultdict(float)
        for a, pa in total.items():
            for b, pb in one:
                nxt[a + b] += pa * pb
        total = nxt
    scores = [s for s, _ in one]
    tail = [0.0] * (len(one) + 1)
    for i in range(len(one) - 1, -1, -1):
        tail[i] = tail[i + 1] + one[i][1]
    return sum(pa * tail[bisect.bisect_left(scores, target - a)] for a, pa in total.items())


def expected(dist) -> Fraction:
    return sum((s * p for s, p in dist.items()), Fraction(0))


def evaluate(rules: Rules, targets=()):
    """计算全部指标，返回可直接序列化为 JSON 的 dict（浮点）。"""
    opt = solver_for(rules)
    opt_finals = opt.final_distribution()
    naive = solver_for(COLOUR_OFF)
    naive_finals = naive.final_distribution()
    opt_dist = score_distribution(opt_finals, rules)
    naive_dist = score_distribution(naive_finals, rules)
    ev = opt.expected_value()
    nev = expected(naive_dist)
    lo = TieBoundEvaluator(naive, rules, min).expected_value()
    hi = TieBoundEvaluator(naive, rules, max).expected_value()
    return {
        "rules": {"name": rules.name, "blue": float(rules.blue), "red": float(rules.red),
                  "redGroup": {str(k + 2): float(v) for k, v in enumerate(rules.red_group)}},
        "optimal": {"ev": float(ev),
                    "shares": {k: float(v) for k, v in hand_shares(opt_finals, rules).items()},
                    "pass3": {str(t): pass_probability(opt_dist, t) for t in targets}},
        "colourBlind": {"ev": float(nev), "pctOfOptimal": float(nev / ev * 100),
                        "tieRange": {"evMin": float(lo), "evMax": float(hi),
                                     "pctMin": float(lo / ev * 100), "pctMax": float(hi / ev * 100)},
                        "shares": {k: float(v) for k, v in hand_shares(naive_finals, rules).items()},
                        "pass3": {str(t): pass_probability(naive_dist, t) for t in targets}},
        "colourOffEv": float(naive.expected_value()),
    }


SHARE_LABELS = (("all_blue", "全蓝"), ("all_red", "全红"), ("red_group_only", "仅红组收益(混色)"),
                ("no_colour_bonus", "没有任何颜色收益"), ("red_group_3plus", "红组3颗以上"),
                ("straight", "含顺子"), ("short_submit", "没交满"))


def format_report(res) -> str:
    r = res["rules"]
    rg = "/".join(f"{v:g}" for v in r["redGroup"].values())
    o, n = res["optimal"], res["colourBlind"]
    lines = [f"== 规则 {r['name']}：全蓝 ×{r['blue']:g}  全红 ×{r['red']:g}  红组 2/3/4/5 颗 ×{rg}",
             f"最优打法 每手期望分 {o['ev']:.1f}",
             "最终手牌占比（最优）：" + "  ".join(f"{lab} {o['shares'][k]*100:.1f}%" for k, lab in SHARE_LABELS),
             f"不看颜色的玩家 每手期望分 {n['ev']:.1f}（最优的 {n['pctOfOptimal']:.1f}%）；"
             f"平局取法的范围 {n['tieRange']['evMin']:.1f}–{n['tieRange']['evMax']:.1f}"
             f"（{n['tieRange']['pctMin']:.1f}%–{n['tieRange']['pctMax']:.1f}%）",
             f"参考：完全关闭颜色与红组时每手期望分 {res['colourOffEv']:.1f}"]
    if o["pass3"]:
        lines.append("三手累计达标概率 最优 / 不看颜色：")
        for t in o["pass3"]:
            lines.append(f"  ≥{t}: {o['pass3'][t]*100:.1f}% / {n['pass3'][t]*100:.1f}%")
    return "\n".join(lines)


def parse_args(argv=None):
    ap = argparse.ArgumentParser(description="没有任何卡的起手：精确动态规划")
    ap.add_argument("--rules", choices=sorted(PRESETS), default="p111", help="规则预设（默认 p111）")
    ap.add_argument("--blue", type=str, help="覆盖全蓝倍率")
    ap.add_argument("--red", type=str, help="覆盖全红倍率")
    ap.add_argument("--red-group", type=str, help="覆盖红组 2/3/4/5 颗倍率，逗号分隔 4 个数")
    ap.add_argument("--targets", type=str, default="", help="三手累计目标，逗号分隔")
    ap.add_argument("--json", action="store_true", help="输出 JSON")
    return ap.parse_args(argv)


def rules_from_args(a) -> Rules:
    base = PRESETS[a.rules]
    custom = a.blue is not None or a.red is not None or a.red_group is not None
    rules = replace(base,
                    blue=Fraction(a.blue) if a.blue is not None else base.blue,
                    red=Fraction(a.red) if a.red is not None else base.red)
    if a.red_group is not None:
        rules = replace(rules, red_group=make_rules("x", 1, 1, a.red_group.split(",")).red_group)
    return replace(rules, name=base.name + "+custom") if custom else rules


def main(argv=None):
    a = parse_args(argv)
    rules = rules_from_args(a)
    targets = [int(t) for t in a.targets.split(",") if t.strip()]
    res = evaluate(rules, targets)
    out = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", newline="\n")
    try:
        out.write(json.dumps(res, ensure_ascii=False, indent=2) if a.json else format_report(res))
        out.write("\n")
    finally:
        out.flush()
        out.detach()
    return 0


if __name__ == "__main__":
    sys.exit(main())
