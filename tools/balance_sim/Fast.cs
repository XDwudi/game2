// Fast planning model. Build.Score() mirrors Core/Rules/ScoreCalculator.Evaluate with doubles instead of BigInteger so
// a hand policy can evaluate millions of submissions. `verify` mode checks it against the real calculator.
// p0.9.0: all 100 controllers, pool dice, rolls used, submissions left, permanent dice count, die facts
// (face texture, mono-colour, mono-pip, opposite pip), retriggers, hand multiplier adds, growth and the C73 red merge.
// p1.0.0: all 150 controllers, the scoring effects of boss modifiers (ModFx), controllers disabled for the stage,
// blue wave, the item multiplier, stage kind, inventory size, empty ordinary slots, hands already used in the stage.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DiceCraft.Core;

public struct Rng
{
    public ulong s;
    public Rng(ulong seed) { s = (seed + 1) * 0x9E3779B97F4A7C15UL; if (s == 0) s = 0x1234567UL; Next(); Next(); }
    public ulong Next() { s ^= s >> 12; s ^= s << 25; s ^= s >> 27; return s * 0x2545F4914F6CDD1DUL; }
    public double NextDouble() => (Next() >> 11) * (1.0 / 9007199254740992.0);
    public int NextInt(int n) => (int)(NextDouble() * n);
}

/// <summary>Set of triggered ability numbers (C01..C153). Replaces the UInt128 mask of p0.9.0, which stopped at 127.</summary>
public struct HitSet
{
    ulong a, b, c;
    public void Set(int i) { if (i < 64) a |= 1UL << i; else if (i < 128) b |= 1UL << (i - 64); else c |= 1UL << (i - 128); }
    public bool Has(int i) => i < 64 ? (a >> i & 1UL) != 0 : i < 128 ? (b >> (i - 64) & 1UL) != 0 : (c >> (i - 128) & 1UL) != 0;
}

public sealed class FDie
{
    public string id;
    public int[] pip = new int[6], opp = new int[6]; public bool[] red = new bool[6], gold = new bool[6], tex = new bool[6];
    public double[] cdf = new double[6];
    public bool glass, temp, monoColor, monoPip; public int six;
}

/// <summary>One frozen submission in the fast model's terms: the ordered submitted dice plus the hand context.</summary>
public sealed class Sub
{
    public int n;
    public readonly int[] pip = new int[12], six = new int[12], opp = new int[12];
    public readonly bool[] red = new bool[12], gold = new bool[12], glass = new bool[12], tex = new bool[12], monoC = new bool[12], monoP = new bool[12], temp = new bool[12];
    /// <summary>Rolls left / rolls thrown this hand.</summary>
    public int rolls, rollsUsed = 1;
    /// <summary>Pool dice (rolled, alive, not submitted): count, pip sum, how many show C71's pip, how many show a red face.</summary>
    public int poolN, poolSum, poolHot, poolRed;
    public void Put(int k, FDie d, int f)
    {
        pip[k] = d.pip[f]; red[k] = d.red[f]; gold[k] = d.gold[f]; glass[k] = d.glass; six[k] = d.six; opp[k] = d.opp[f]; tex[k] = d.tex[f]; monoC[k] = d.monoColor; monoP[k] = d.monoPip; temp[k] = d.temp;
    }
    public void SetPool(FDie[] dice, int[] face, int mask, int hotPip)
    {
        poolN = poolSum = poolHot = poolRed = 0;
        for (int i = 0; mask != 0; i++, mask >>= 1)
        {
            if ((mask & 1) == 0) continue;
            var d = dice[i]; int f = face[i], v = d.pip[f];
            poolN++; poolSum += v; if (v == hotPip) poolHot++; if (d.red[f]) poolRed++;
        }
    }
}

/// <summary>p1.0.0: the effects of one boss modifier as flags. Scoring effects are applied by Build.Score; the
/// session effects are only read by the bot (MustFill, NoItems, ...). None = no modifier in force.</summary>
public sealed class ModFx
{
    public static readonly ModFx None = new ModFx();
    public string id;
    public bool sealOdd, sealEven, sealLow, sealHigh, sealRed, sealBlue, sealFirst, sealAlt, noStraight, smallHands, noRepeat, handHalf, zeroLevels, noRed, noBlue, firstHalf, laterHalf, pipsOnly, growthZero, silence;
    /// <summary>valueSeal: a seal that depends on the die (pip or colour); posSeal: a seal that depends on the slot.</summary>
    public bool valueSeal, posSeal;
    public bool mustFill, noItems, loseHalfGold, taxPerDie, noReward, growthFreeze, handDowngrade, disables;
    public int targetPercent = 100;
    public static readonly string[] ScoringEffects = { "SealOdd", "SealEven", "SealLow", "SealHigh", "SealRed", "SealBlue", "SealFirst", "SealAlternate", "NoStraight", "SmallHandsOnly", "NoRepeatHand", "HandHalf", "ZeroLevels", "NoRed", "NoBlue", "FirstHalf", "LaterHalf", "PipsOnly", "GrowthZero", "EnchantSilence" };
    public static ModFx Of(BossModifierDefinition m)
    {
        var f = new ModFx { id = m.id };
        foreach (string e in m.effects)
            switch (e)
            {
                case "SealOdd": f.sealOdd = true; break; case "SealEven": f.sealEven = true; break; case "SealLow": f.sealLow = true; break; case "SealHigh": f.sealHigh = true; break;
                case "SealRed": f.sealRed = true; break; case "SealBlue": f.sealBlue = true; break; case "SealFirst": f.sealFirst = true; break; case "SealAlternate": f.sealAlt = true; break;
                case "NoStraight": f.noStraight = true; break; case "SmallHandsOnly": f.smallHands = true; break; case "NoRepeatHand": f.noRepeat = true; break; case "HandHalf": f.handHalf = true; break;
                case "ZeroLevels": f.zeroLevels = true; break; case "NoRed": f.noRed = true; break; case "NoBlue": f.noBlue = true; break; case "FirstHalf": f.firstHalf = true; break; case "LaterHalf": f.laterHalf = true; break;
                case "PipsOnly": f.pipsOnly = true; break; case "GrowthZero": f.growthZero = true; break; case "EnchantSilence": f.silence = true; break;
                case "MustFill": f.mustFill = true; break; case "NoItems": f.noItems = true; break; case "LoseHalfGold": f.loseHalfGold = true; break; case "TaxPerDie": f.taxPerDie = true; break;
                case "NoReward": f.noReward = true; break; case "GrowthFreeze": f.growthFreeze = true; break; case "HandDowngrade": f.handDowngrade = true; break;
                case "TargetPercent": f.targetPercent = m.amount; break; case "DisableLeftmost": case "DisableRarity": f.disables = true; break;
                case "RollsDelta": case "RollsFixed": case "SubmissionsDelta": case "CapacityDelta": break;   // budgets: read through StageRules
                default: throw new NotSupportedException("Unknown boss modifier effect: " + e);
            }
        f.valueSeal = f.sealOdd || f.sealEven || f.sealLow || f.sealHigh || f.sealRed || f.sealBlue; f.posSeal = f.sealFirst || f.sealAlt;
        return f;
    }
}

/// <summary>A stage the build is valued for without being inside it (stage select, shop): the boss modifier, the
/// controllers it disables, whether it is a boss stage, and optionally the budgets the session quotes.</summary>
public sealed class StageCtx
{
    public string modifierId; public string[] disabled = Array.Empty<string>(); public bool boss;
    public int capacity = -1, rolls = -1, submissions = -1;
    public string Key => (modifierId ?? "-") + (boss ? "B" : "n") + (disabled == null ? "?" : string.Join(",", disabled)) + "/" + capacity + "/" + rolls + "/" + submissions;
}

public sealed class Params
{
    public const int N = 154;
    public readonly GameConfig cfg;
    public readonly int[] hBase = new int[13], hInc = new int[13], hPri = new int[13];
    public readonly string[] hId = new string[13];
    public readonly Dictionary<string, int> hIndex = new Dictionary<string, int>();
    public readonly int PAIR, TWO_PAIR, TRIPS, FULL, THREE_PAIR, QUADS, TWO_TRIPS, FIVE, FOUR_PAIR, SIX, SEVEN, EIGHT, STRAIGHT;
    public readonly int blueBase, blueInc, redBase, redInc;
    public readonly int[] redTable = new int[9];
    public readonly double[] mult = new double[N];
    public static readonly int[] Fixed = { 2, 6, 8, 9, 11, 14, 18, 28, 30, 31, 32, 33, 40, 42, 46, 74, 75, 77, 81, 82, 83, 86, 88, 101, 102, 104, 105, 106, 130, 138, 144, 149, 153 };
    public static readonly int[] GrowthMul = { 54, 56, 57, 58, 60, 61, 90, 100, 129, 132, 134 }, GrowthAdd = { 55, 59, 62 };
    public readonly double c53mean; public readonly int[] c53units;
    public readonly int p3idx, p6min, p10pip, p10add, p11c2, p11c3, p12add, p13add, p17add, p19max, p19add, p20pip, p20add, p24from, p24to, p27add, p28min, p29len, p30min, p31min, p32d, p33min, p33diff, p39add, p44add, p45mult, p47units, p48add, p50add, enchBase, enchMult, unusedRoll, glassMult, goldMult, minStraight;
    public readonly bool[] p17 = new bool[9], p66 = new bool[9], p67 = new bool[9];
    public readonly double glassBreak;
    // p0.9.0
    public readonly int p63units, p64idx, p64r, p65r, p66r, p67r, p68r, p69r, p70add, p71pip, p72mul, p75max, p76add, p80sum, p80add, p82mask, p86mask, p84add, p84min, p85add, p85min, p87min, p88min, p91add, p92add, p93mult, p103max, p103strictMax;
    public readonly double p71step, p87step, p89step, p103strict;
    // p1.0.0
    public readonly int p104min, p106min, p107add, p108idx, p108add, p109r, p109min, p127ord, p127min, p135add, p137add, p138cost, p139gold, p139step4, p139max, p141init, p142add, p143r, p143req, p147step4, p148units2, p149req, p151units2, p152step4, p153ord, redCap;
    public readonly double glassBreakFilm;
    /// <summary>isCommon[a]: controller Ca has the rarity that C152 counts.</summary>
    public readonly bool[] isCommon = new bool[N];
    readonly Dictionary<string, ModFx> fx = new Dictionary<string, ModFx>(StringComparer.Ordinal);
    public ModFx Fx(string modifierId) => string.IsNullOrEmpty(modifierId) ? ModFx.None : fx[modifierId];
    public static string Cid(int a) => "C" + a.ToString("D2");
    public Params(GameConfig c)
    {
        cfg = c;
        for (int i = 0; i < 13; i++) { var h = c.handDefinitions[i]; hId[i] = h.id; hIndex[h.id] = i; hBase[i] = h.baseMult100; hInc[i] = h.incrementPerLevel100; hPri[i] = h.structuralPriority; }
        PAIR = hIndex["pair"]; TWO_PAIR = hIndex["two_pair"]; TRIPS = hIndex["trips"]; FULL = hIndex["full_house"]; THREE_PAIR = hIndex["three_pair"]; QUADS = hIndex["quads"]; TWO_TRIPS = hIndex["two_trips"];
        FIVE = hIndex["five_kind"]; FOUR_PAIR = hIndex["four_pair"]; SIX = hIndex["six_kind"]; SEVEN = hIndex["seven_kind"]; EIGHT = hIndex["eight_kind"]; STRAIGHT = hIndex["straight"];
        var b = c.Color("all_blue"); var r = c.Color("all_red"); blueBase = b.baseMult100; blueInc = b.incrementPerLevel100; redBase = r.baseMult100; redInc = r.incrementPerLevel100;
        foreach (var rr in c.redRewards) redTable[rr.count] = rr.factor100;
        redCap = c.debug.redGroupCapForExperiments; if (redCap > 8) throw new NotSupportedException("Fast scorer assumes a red reward table of at most 8.");
        Func<int, ControllerParameters> p = id => c.Controller(Cid(id)).parameters;
        foreach (int id in Fixed) mult[id] = p(id).scoreMultiplier;
        c53units = p(53).factorUnits2; c53mean = c53units.Average() / 2.0;
        p3idx = p(3).index; p6min = p(6).minDistinct; p10pip = p(10).pip; p10add = p(10).perDieAdd; p11c2 = p(11).requiredCounts.pip2; p11c3 = p(11).requiredCounts.pip3; p12add = p(12).oddToEvenAdd; p13add = p(13).oddPerDieAdd;
        p17add = p(17).perDieAdd; foreach (int v in p(17).pips) p17[v] = true; p19max = p(19).maxPermanentSixFaceSum; p19add = p(19).perDieAdd; p20pip = p(20).lastActualCapacitySlotPip; p20add = p(20).perDieAdd;
        p24from = p(24).redGroupKeyFrom; p24to = p(24).redGroupKeyTo; p27add = p(27).baseAddPerDistinctPairedPip; p28min = p(28).minDistinctPairs; p29len = p(29).minimumStraightLength; p30min = p(30).minSamePip; p31min = p(31).minSamePip;
        p32d = p(32).distinctPips; p33min = p(33).minDice; p33diff = p(33).firstMinusSecondAtLeast; p39add = p(39).perDieAdd; p44add = p(44).baseAddPerFrozenGold; p47units = p(47).perControllerUnits2; p45mult = p(45).matchingDieContributionMultiplier;
        p48add = p(48).baseAddPerEmptySubmitSlot; p50add = p(50).baseAdd; enchBase = c.enchantRules.baseAdd; enchMult = c.enchantRules.scoreMultiplier; unusedRoll = c.bonusPerUnusedRoll;
        glassMult = c.glassContributionMultiplier; goldMult = c.goldContributionMultiplier; minStraight = c.minStraightLength; glassBreak = c.glassBreakPercent / 100.0;
        if (p(39).effectiveColor != "blue") throw new NotSupportedException("Fast scorer assumes C39 counts blue dice.");
        if (p3idx != 0 || p(64).index != 0 || p(108).index != 0 || p(119).index != 0 || p(127).sourceIndex != 0) throw new NotSupportedException("Fast scorer assumes the first-die cards read index 0.");
        p63units = p(63).perDieUnits10; p64idx = p(64).index; p64r = p(64).retriggers; p65r = p(65).retriggers; p66r = p(66).retriggers; p67r = p(67).retriggers; p68r = p(68).retriggers; p69r = p(69).retriggers;
        foreach (int v in p(66).pips) p66[v] = true; foreach (int v in p(67).pips) p67[v] = true;
        p70add = p(70).baseAddPerPoolDie; p71pip = p(71).pip; p71step = p(71).stepNumerator / (double)p(71).stepDenominator; p72mul = p(72).poolPipSumMultiplier;
        p75max = p(75).maxRollsUsed; p76add = p(76).baseAddPerExtraRoll; p80sum = p(80).pairSum; p80add = p(80).baseAddPerPair;
        foreach (string id in p(82).handIds) p82mask |= 1 << hIndex[id]; foreach (string id in p(86).handIds) p86mask |= 1 << hIndex[id];
        p84add = p(84).handMultAdd100; p84min = p(84).minSamePip; p85add = p(85).handMultAdd100; p85min = p(85).minSamePip;
        p87min = p(87).minSamePip; p87step = p(87).stepNumerator / (double)p(87).stepDenominator; p88min = p(88).minDice; p89step = p(89).stepNumerator / (double)p(89).stepDenominator;
        p91add = p(91).perDieAdd; p92add = p(92).perDieAdd; p93mult = p(93).dieContributionMultiplier;
        mult[103] = p(103).scoreMultiplier; p103strict = p(103).strictScoreMultiplier; p103max = p(103).maxOwnedPermanentDice; p103strictMax = p(103).strictMaxOwnedPermanentDice;
        // p1.0.0
        p104min = p(104).minDice; p106min = p(106).minDice; p107add = p(107).perDieAdd; p108idx = p(108).index; p108add = p(108).baseAddPerPip; p109r = p(109).retriggers; p109min = p(109).minDice;
        p127ord = p(127).submissionOrdinal; p127min = p(127).minDice; p135add = p(135).baseAddPerItem; p137add = p(137).perDieAdd; p138cost = p(138).goldCost;
        p139gold = p(139).goldPerStep; p139step4 = p(139).stepUnits4; p139max = p(139).maxSteps; p141init = p(141).initialMultiplierUnits2; p142add = p(142).handMultAdd100;
        p143r = p(143).retriggers; p143req = p(143).requiredDice; p147step4 = p(147).stepUnits4; p148units2 = p(148).perRollUnits2; p149req = p(149).requiredDice;
        p151units2 = p(151).perSlotUnits2; p152step4 = p(152).stepUnits4; p153ord = p(153).submissionOrdinal; glassBreakFilm = p(145).glassBreakPercent / 100.0;
        foreach (var d in c.controllers) isCommon[int.Parse(d.id.Substring(1))] = d.rarity == p(152).rarity;
        foreach (var m in c.bossModifiers) fx[m.id] = ModFx.Of(m);
    }
}

public sealed class Build
{
    public Params P;
    public int[] cnt = new int[Params.N], eb = new int[Params.N], em = new int[Params.N];
    /// <summary>Per ability: product of (20 + growth) / 20 and sum of growth over its sources (a mimic shares its target's value).</summary>
    public double[] gMul = new double[Params.N], gAdd = new double[Params.N];
    public int[] present = Array.Empty<int>();
    public int alive, capacity, ordinal, rollsPerHand, submissions, prev = -1;
    /// <summary>Owned permanent dice, and submissions left in the stage before the one being scored (1 = last chance).</summary>
    public int permDice, subsLeft = 99;
    public long wallet; public bool redWave, pos;
    public int[] handLv = new int[13]; public int blueLv, redLv;
    public FDie[] dice = Array.Empty<FDie>();
    public int c45Red, c45Blue; public double c51 = 1, c53 = -1;
    public HitSet lastHits; public int lastRedN, lastRedCount;
    /// <summary>Per-die value of the last Score call, in submission order. contrib = contribution before retriggers
    /// and before the positional seals (SealFirst / SealAlternate); full = contrib x (1 + retriggers that follow the
    /// die, not the slot). The order search places dice on slots with these.</summary>
    public readonly double[] contrib = new double[12], full = new double[12];
    /// <summary>True when submission order matters only through slot effects: retriggers of the first / last / middle
    /// die (C64, C65, C109) and the positional seals. Then dice are simply assigned to slots by value.</summary>
    public bool posSimple;
    // ---- p1.0.0 context ----
    /// <summary>The boss modifier in force (never null).</summary>
    public ModFx fx = ModFx.None;
    /// <summary>Stage kind is boss (C130); items held (C135); empty ordinary slots (C151); alive controllers of
    /// common rarity, disabled ones included (C152); primary hands already scored this stage as a bit mask (NoRepeatHand).</summary>
    public bool boss, blueWave; public int inventory, emptySlots, commons, usedHands, itemMult = 1;
    /// <summary>C141: product of (initial - stages survived) / 2 over its sources.</summary>
    public double c141 = 1;
    /// <summary>C138 sources in controller order: 0 = no scoring enchant, 1 = base10, 2 = mult2. Only the first
    /// wallet / cost sources trigger, so their enchants cannot be pooled in eb / em.</summary>
    public byte[] e138 = Array.Empty<byte>();
    /// <summary>Chance that a glass die breaks on a roll (2% with C145, otherwise the configured 8%).</summary>
    public double glassBreak;
    /// <summary>Face-growth cards are held, so equal-scoring submission orders are not equally good (see Planner.FinalEval).</summary>
    public bool growOrder, lowBuild;

    public static Build From(RunState s, Params P, bool inStage, bool large = false, bool small = false, StageCtx ctx = null)
    {
        var cfg = P.cfg; var b = new Build { P = P };
        bool started = inStage && s.stage != null && s.stage.started && !s.stage.settled;
        var bindings = started ? s.stage.mimicBindings : null;
        var all = s.controllers.ToArray();
        // The modifier and the controllers it disables: the running stage, or the stage described by ctx.
        string modifierId = ctx != null ? ctx.modifierId : started && !s.stage.modifierNullified ? s.stage.modifierId : null;
        string[] disabled = ctx != null ? ctx.disabled ?? Array.Empty<string>() : started ? s.stage.disabledControllerIds : Array.Empty<string>();
        var rules = StageRules.For(cfg, modifierId, disabled);
        b.fx = P.Fx(modifierId);
        var sources = ControllerRules.ResolveSources(all, bindings, disabled);
        for (int i = 0; i < Params.N; i++) b.gMul[i] = 1;
        var gold138 = new List<byte>(); bool film = false;
        foreach (var src in sources)
        {
            if (src.abilityId == null) continue;
            int a = int.Parse(src.abilityId.Substring(1)); b.cnt[a]++;
            if (a == 138) gold138.Add((byte)(src.enchantmentId == "base10" ? 1 : src.enchantmentId == "mult2" ? 2 : 0));
            else if (src.enchantmentId == "base10") b.eb[a]++; else if (src.enchantmentId == "mult2") b.em[a]++;
            if (a == 45) { if (src.state.activeColor == "red") b.c45Red++; else b.c45Blue++; }
            if (a == 51) b.c51 *= src.state.turnoverUnits2 / 2.0;
            if (a == 141) b.c141 *= (P.p141init - src.state.growth) / 2.0;
            if (a == 145) film = true;
            if (ControllerRules.IsGrowthMultiplier(src.abilityId)) b.gMul[a] *= (20 + src.state.growth) / 20.0;
            else if (ControllerRules.IsGrowthBase(src.abilityId)) b.gAdd[a] += src.state.growth;
        }
        b.e138 = gold138.ToArray(); b.glassBreak = film ? P.glassBreakFilm : P.glassBreak;
        b.present = Enumerable.Range(1, Params.N - 1).Where(i => b.cnt[i] > 0).ToArray();
        b.alive = all.Count(c => c.alive);
        foreach (var c in all) if (c.alive && P.isCommon[int.Parse(c.definitionId.Substring(1))]) b.commons++;
        b.emptySlots = Math.Max(0, s.controllerSlots - ControllerRules.OrdinaryUsed(all));
        b.inventory = s.inventory.Count;
        b.capacity = ctx != null && ctx.capacity > 0 ? ctx.capacity : started ? s.stage.capacity : ControllerRules.Capacity(all, cfg, false, bindings, rules);
        b.rollsPerHand = ctx != null && ctx.rolls > 0 ? ctx.rolls : ControllerRules.RollBudget(all, cfg, started ? s.stage.stageRollBonus : 0, bindings, rules);
        b.submissions = ctx != null && ctx.submissions > 0 ? ctx.submissions : ControllerRules.SubmissionBudget(all, cfg, bindings, rules);
        b.wallet = s.gold; b.ordinal = started ? s.stage.submissionOrdinal : 0;
        // Stage kind: the running stage, the stage being previewed, or (after settlement) the next one.
        int stageIndex = s.stage == null ? 0 : Math.Min(cfg.stages.Length - 1, s.stage.stageIndex + (s.stage.settled ? 1 : 0));
        b.boss = ctx != null ? ctx.boss : cfg.stages[stageIndex].stageKind == "boss";
        if (started)
        {
            foreach (string id in s.stage.primaryHandsUsed) b.usedHands |= 1 << P.hIndex[id];
            if (s.hand != null) { b.redWave = s.hand.redWave; b.blueWave = s.hand.blueWave; b.itemMult = s.hand.doubleScore ? 2 : 1; }
        }
        b.prev = string.IsNullOrEmpty(s.previousPrimaryHandId) || s.previousPrimaryHandId == "scatter" ? -1 : P.hIndex[s.previousPrimaryHandId];
        foreach (var h in s.handLevels) b.handLv[P.hIndex[h.handId]] = h.level;
        foreach (var h in s.colorLevels) { if (h.handId == "all_blue") b.blueLv = h.level; else b.redLv = h.level; }
        b.SetOrderFlags();
        b.permDice = s.dice.Count(d => d.lifetime == "permanent");
        b.dice = s.dice.OrderBy(d => d.createdSerial).Select(d => MakeDie(d, cfg, large, small, b.cnt[34], b.cnt[35])).ToArray();
        return b;
    }

    /// <summary>Which kinds of submission order effects this build has (call again after changing cnt or fx by hand).</summary>
    public void SetOrderFlags()
    {
        // Pattern cards: first-die colour, last slot, first-minus-second, rising run, alternating colours, reds first,
        // palindrome, +1 neighbours, first pip. Slot cards: retrigger the first / last / middle die, positional seals.
        int pattern = cnt[3] + cnt[20] + cnt[33] + cnt[40] + cnt[104] + cnt[105] + cnt[106] + cnt[107] + cnt[108];
        bool slots = cnt[64] + cnt[65] + cnt[109] > 0 || fx.posSeal;
        pos = pattern > 0 || slots; posSimple = pos && pattern == 0;
        growOrder = cnt[117] + cnt[119] + cnt[127] > 0; lowBuild = cnt[10] + cnt[17] + cnt[19] + cnt[66] > 0;
    }

    public static FDie MakeDie(DieInstance d, GameConfig cfg, bool large, bool small, int high, int low)
    {
        var faces = d.faces.OrderBy(f => f.faceId, StringComparer.Ordinal).ToArray();
        // Real probability units: face weights (p1.0.0, up to 5), textures, item buffs and the C34 / C35 shift.
        var units = ProbabilityCalculator.BuildUnits(faces, large, small, cfg, high, low);
        var f = new FDie { id = d.dieId, glass = d.isGlass, temp = d.lifetime != "permanent", six = faces.Sum(x => x.permanentPip) };
        var textures = cfg.Controller("C91").parameters.textures;
        f.monoColor = faces.All(x => x.baseColor == faces[0].baseColor); f.monoPip = faces.All(x => x.permanentPip == faces[0].permanentPip);
        double acc = 0;
        for (int i = 0; i < 6; i++)
        {
            f.pip[i] = faces[i].permanentPip; f.red[i] = faces[i].baseColor == "red"; f.gold[i] = faces[i].isGold; acc += units[i] / (double)cfg.probabilityUnits; f.cdf[i] = acc;
            f.tex[i] = textures.Contains(faces[i].texture); string oppId = cfg.Opposite(faces[i].faceId); f.opp[i] = faces.First(x => x.faceId == oppId).permanentPip;
        }
        f.cdf[5] = 1.0000001;
        return f;
    }

    static double PowI(double x, int e) { double r = 1; for (int k = 0; k < e; k++) r *= x; return r; }

    /// <summary>Unfloored score of one submission; equals ScoreCalculator.Evaluate's scoreNumerator / scoreDenominator.</summary>
    public double Score(Sub h, out int primary)
    {
        var P = this.P; var cnt = this.cnt; var fx = this.fx; int n = h.n;
        // 1. Effective pips and colours: C12, C03, then the waves (they override C03; never both).
        Span<int> pip = stackalloc int[n]; Span<bool> red = stackalloc bool[n]; Span<int> c = stackalloc int[9]; c.Clear();
        bool has12 = cnt[12] > 0, changedOdd = false;
        for (int i = 0; i < n; i++) { int v = h.pip[i]; if (has12 && (v & 1) == 1) { v += P.p12add; changedOdd = true; } pip[i] = v; red[i] = h.red[i]; }
        if (cnt[3] > 0) red[P.p3idx] = true;
        if (redWave) red.Fill(true); else if (blueWave) red.Fill(false);
        int redN = 0, goldN = 0; bool anyOdd = false, allOdd = true, anyLow = false, anySmall = false, redOne = false, any66 = false, any67 = false, anyTex = false, anyMonoC = false, anyMonoP = false, anyTemp = false;
        for (int i = 0; i < n; i++)
        {
            int v = pip[i]; c[v]++;
            if (red[i]) { redN++; if (v == P.p24from) redOne = true; }
            if ((v & 1) == 1) anyOdd = true; else allOdd = false;
            if (P.p17[v]) anyLow = true; if (h.six[i] <= P.p19max) anySmall = true;
            if (P.p66[v]) any66 = true; if (P.p67[v]) any67 = true;
            if (h.gold[i]) goldN++; if (h.tex[i]) anyTex = true; if (h.monoC[i]) anyMonoC = true; if (h.monoP[i]) anyMonoP = true; if (h.temp[i]) anyTemp = true;
        }
        // 2. Hands. SmallHandsOnly keeps pair / two pair / trips; NoStraight removes the straight; ZeroLevels zeroes levels.
        int distinct = 0, maxSame = 0, pairKinds = 0, trips = 0, run = 0, maxRun = 0; bool exactGroup = false;
        for (int v = 1; v <= 8; v++)
        {
            int k = c[v];
            if (k == 0) { run = 0; continue; }
            run++; if (run > maxRun) maxRun = run;
            distinct++; if (k > maxSame) maxSame = k; if (k >= 2) pairKinds++; if (k >= 3) trips++; if (k == P.p143req) exactGroup = true;
        }
        Span<int> mh = stackalloc int[13]; int nm = 0;
        if (maxSame >= 2) mh[nm++] = P.PAIR; if (pairKinds >= 2) mh[nm++] = P.TWO_PAIR; if (maxSame >= 3) mh[nm++] = P.TRIPS;
        if (!fx.smallHands)
        {
            if (trips >= 1 && pairKinds >= 2) mh[nm++] = P.FULL;
            if (pairKinds >= 3) mh[nm++] = P.THREE_PAIR; if (maxSame >= 4) mh[nm++] = P.QUADS; if (trips >= 2) mh[nm++] = P.TWO_TRIPS; if (maxSame >= 5) mh[nm++] = P.FIVE; if (pairKinds >= 4) mh[nm++] = P.FOUR_PAIR;
            if (maxSame >= 6) mh[nm++] = P.SIX; if (maxSame >= 7) mh[nm++] = P.SEVEN; if (maxSame >= 8) mh[nm++] = P.EIGHT;
        }
        bool zero = fx.zeroLevels;
        int group = 0, bestPri = 0, matched = 0; primary = -1;
        for (int k = 0; k < nm; k++) { int hd = mh[k]; matched |= 1 << hd; int m = P.hBase[hd] + (zero ? 0 : P.hInc[hd] * handLv[hd]); if (m > group) group = m; if (P.hPri[hd] > bestPri) { bestPri = P.hPri[hd]; primary = hd; } }
        int reqLen = cnt[29] > 0 ? P.p29len : P.minStraight;
        bool hasStraight = !fx.noStraight && maxRun >= reqLen; int straight = 0;
        if (hasStraight) { int S = P.STRAIGHT; straight = P.hBase[S] + (zero ? 0 : P.hInc[S] * handLv[S]); if (P.hPri[S] > bestPri) primary = S; }
        int color = 100;
        if (redN == n) { if (!fx.noRed) color = P.redBase + (zero ? 0 : P.redInc * redLv); }
        else if (redN == 0) { if (!fx.noBlue) color = P.blueBase + (zero ? 0 : P.blueInc * blueLv); }

        // Pairs of dice whose pips add up to pairSum (each die used once).
        int sumPairs = 0;
        for (int a = 1; a * 2 <= P.p80sum && a <= 8; a++) { int o = P.p80sum - a; sumPairs += a == o ? c[a] / 2 : o <= 8 ? Math.Min(c[a], c[o]) : 0; }
        // 3. Largest red group among the submitted dice (more dice first, then the higher key); C25 merges all red dice.
        // C73 adds the red pool dice, C146 one more (once, however many sources).
        bool merge = cnt[25] > 0, map = cnt[24] > 0; int subRed = 0, redKey = 0;
        if (redN > 0)
        {
            if (merge) subRed = redN;
            else
            {
                Span<int> rc = stackalloc int[9]; rc.Clear();
                for (int i = 0; i < n; i++) if (red[i]) rc[map && pip[i] == P.p24from ? P.p24to : pip[i]]++;
                for (int key = 1; key <= 8; key++) if (rc[key] > 0 && rc[key] >= subRed) { subRed = rc[key]; redKey = key; }
            }
        }
        int redPool = cnt[73] > 0 && subRed > 0 ? h.poolRed : 0, redCount = subRed + redPool + (cnt[146] > 0 && subRed > 0 ? 1 : 0);
        lastRedN = redN; lastRedCount = redCount;

        // 4. Which sources trigger. Disabled controllers are not sources at all (Build.From left them out of cnt).
        var hits = new HitSet(); Span<bool> on = stackalloc bool[Params.N]; on.Clear(); int hits138 = 0;
        foreach (int a in present)
        {
            bool hit;
            switch (a)
            {
                case 1: case 3: case 9: case 38: case 42: case 45: case 46: case 47: case 49: case 50: case 51: case 53: hit = true; break;
                case 2: hit = redN == 0; break;
                case 6: hit = distinct >= P.p6min; break;
                case 7: hit = ordinal == 0 && n == 2 && pip[0] != pip[1]; break;
                case 8: case 48: hit = n < capacity; break;
                case 10: hit = c[P.p10pip] > 0; break;
                case 11: hit = c[2] >= P.p11c2 && c[3] >= P.p11c3; break;
                case 12: hit = changedOdd; break;
                case 13: hit = anyOdd; break;
                case 14: hit = allOdd; break;
                case 15: { hit = false; for (int i = 1; i < n - 1; i++) if (!red[i] && red[i - 1] && red[i + 1]) { hit = true; break; } break; }
                case 17: hit = anyLow; break;
                case 18: hit = redN == n - redN; break;
                case 19: hit = anySmall; break;
                case 20: hit = n == capacity && pip[n - 1] == P.p20pip; break;
                case 24: hit = redOne; break;
                case 25: hit = redN > 0; break;
                case 26: hit = n == 1; break;
                case 27: hit = pairKinds > 0; break;
                case 28: hit = pairKinds >= P.p28min; break;
                case 29: hit = hasStraight; break;
                case 30: hit = maxSame >= P.p30min; break;
                case 31: hit = maxSame >= P.p31min; break;
                case 32: hit = distinct == P.p32d; break;
                case 33: hit = n >= P.p33min && pip[0] - pip[1] >= P.p33diff; break;
                case 36: case 37: hit = n >= 2 && n < capacity; break;
                case 39: hit = redN < n; break;
                case 40: hit = n >= 3 && pip[1] > pip[0] && pip[2] - pip[1] == pip[1] - pip[0]; break;
                case 43: hit = primary >= 0 && primary == prev; break;
                case 44: hit = wallet > 0; break;
                // p0.9.0
                case 54: case 55: case 56: case 57: case 58: case 59: case 60: case 61: case 62: case 63: case 64: case 90: case 94: case 100: case 101: hit = true; break;
                case 65: hit = n == capacity; break;
                case 66: hit = any66; break;
                case 67: hit = any67; break;
                case 68: case 102: hit = subsLeft == 1; break;
                case 69: hit = subRed > 0; break;
                case 70: case 72: hit = h.poolN > 0; break;
                case 71: hit = h.poolHot > 0; break;
                case 73: hit = subRed > 0 && h.poolRed > 0; break;
                case 74: hit = h.poolN >= n; break;
                case 75: hit = h.rollsUsed == P.p75max; break;
                case 76: hit = h.rollsUsed >= 2; break;
                case 77: hit = h.rolls == 0; break;
                case 80: hit = sumPairs > 0; break;
                case 81: hit = primary < 0; break;
                case 82: hit = (matched & P.p82mask) != 0; break;
                case 86: hit = (matched & P.p86mask) != 0; break;
                case 83: hit = hasStraight; break;
                case 84: hit = maxSame >= P.p84min; break;
                case 85: hit = maxSame >= P.p85min; break;
                case 87: hit = maxSame >= P.p87min; break;
                case 88: hit = n >= P.p88min && distinct == n; break;
                case 89: hit = goldN > 0; break;
                case 91: hit = anyTex; break;
                case 92: hit = anyMonoC; break;
                case 93: hit = anyMonoP; break;
                case 103: hit = permDice <= P.p103max; break;
                // p1.0.0
                case 108: case 117: case 118: case 119: case 122: case 123: case 124: case 125: case 129: case 132: case 134: case 141: hit = true; break;
                case 104: { hit = n >= P.p104min; for (int i = 1; hit && i < n; i++) if (red[i] == red[i - 1]) hit = false; break; }
                case 105: { hit = redN > 0 && redN < n; for (int i = 0; hit && i < redN; i++) if (!red[i]) hit = false; break; }
                case 106: { hit = n >= P.p106min; for (int i = 0; hit && i < n / 2; i++) if (pip[i] != pip[n - 1 - i]) hit = false; break; }
                case 107: { hit = false; for (int i = 1; i < n; i++) if (pip[i] == pip[i - 1] + 1) { hit = true; break; } break; }
                case 109: hit = (n & 1) == 1 && n >= P.p109min; break;
                case 126: case 147: hit = h.poolN > 0; break;
                case 127: hit = ordinal == P.p127ord && n >= P.p127min; break;
                case 130: hit = boss; break;
                case 135: hit = inventory > 0; break;
                case 137: hit = anyTemp; break;
                // Sources are served left to right from the frozen wallet; each one that triggers reserves the cost.
                case 138: hits138 = (int)Math.Min(cnt[138], wallet / P.p138cost); hit = hits138 > 0; break;
                case 139: hit = wallet >= P.p139gold; break;
                case 142: hit = hasStraight; break;
                case 143: hit = exactGroup; break;
                case 144: hit = !anyOdd; break;
                case 146: hit = subRed > 0; break;
                case 148: hit = h.rolls > 0; break;
                case 149: hit = n == P.p149req; break;
                case 150: hit = ordinal >= 1; break;
                case 151: hit = emptySlots > 0; break;
                case 152: hit = commons > 0; break;
                case 153: hit = ordinal == P.p153ord; break;
                default: hit = false; break;
            }
            if (hit) { hits.Set(a); on[a] = true; }
        }
        lastHits = hits;
        // 8. Hand multiplier: adds, floor of x1, then NoRepeatHand and HandHalf.
        int handAdd = (on[84] ? cnt[84] * P.p84add : 0) + (on[85] ? cnt[85] * P.p85add : 0) + (on[142] ? cnt[142] * P.p142add : 0);
        int hand = Math.Max(100, group + straight + handAdd);
        if (fx.noRepeat && primary >= 0 && (usedHands >> primary & 1) != 0) hand = 100;
        if (fx.handHalf) hand = Math.Max(100, hand / 2);

        // 5-6. Per-die contributions and retriggers. A sealed die contributes nothing; PipsOnly keeps the bare pip.
        int a10 = cnt[10] * P.p10add, a13 = cnt[13] * P.p13add, a17 = cnt[17] * P.p17add, a19 = cnt[19] * P.p19add, a20 = cnt[20] * P.p20add, a39 = cnt[39] * P.p39add;
        int a91 = cnt[91] * P.p91add, a92 = cnt[92] * P.p92add, k93 = cnt[93], k94 = cnt[94], a107 = cnt[107] * P.p107add, a137 = cnt[137] * P.p137add;
        int r64 = cnt[64] * P.p64r, r65 = on[65] ? cnt[65] * P.p65r : 0, r66 = cnt[66] * P.p66r, r67 = cnt[67] * P.p67r, r68 = on[68] ? cnt[68] * P.p68r : 0, r69 = cnt[69] * P.p69r;
        int r109 = on[109] ? cnt[109] * P.p109r : 0, r143 = cnt[143] * P.p143r, mid = (n - 1) / 2;
        bool valueSeal = fx.valueSeal, pipsOnly = fx.pipsOnly;
        double sum = 0;
        for (int i = 0; i < n; i++)
        {
            int v = pip[i]; double x;
            if (valueSeal && ((fx.sealOdd && (v & 1) == 1) || (fx.sealEven && (v & 1) == 0) || (fx.sealLow && v <= 3) || (fx.sealHigh && v >= 4) || (fx.sealRed && red[i]) || (fx.sealBlue && !red[i]))) x = 0;
            else if (pipsOnly) x = v;
            else
            {
                x = v;
                if (v == P.p10pip) x += a10; if ((v & 1) == 1) x += a13; if (P.p17[v]) x += a17; if (h.six[i] <= P.p19max) x += a19;
                if (a20 != 0 && n == capacity && i == capacity - 1 && v == P.p20pip) x += a20;
                if (!red[i]) x += a39;
                if (h.tex[i]) x += a91; if (h.monoC[i]) x += a92; x += k94 * h.opp[i];
                if (a107 != 0 && i > 0 && v == pip[i - 1] + 1) x += a107;
                if (h.temp[i]) x += a137;
                int k45 = red[i] ? c45Red : c45Blue; for (int k = 0; k < k45; k++) x *= P.p45mult;
                if (h.monoP[i]) for (int k = 0; k < k93; k++) x *= P.p93mult;
                if (h.glass[i]) x *= P.glassMult; if (h.gold[i]) x *= P.goldMult;
            }
            int rt = r68;
            if (P.p66[v]) rt += r66; if (P.p67[v]) rt += r67;
            if (r69 != 0 && red[i] && (merge || (map && v == P.p24from ? P.p24to : v) == redKey)) rt += r69;
            if (r143 != 0 && c[v] == P.p143req) rt += r143;
            contrib[i] = x; full[i] = x * (1 + rt);
            if (fx.posSeal && ((fx.sealFirst && i == 0) || (fx.sealAlt && (i & 1) == 0))) continue;
            if (i == P.p64idx) rt += r64; if (i == n - 1) rt += r65; if (i == mid) rt += r109;
            sum += x * (1 + rt);
        }
        // 7. Base: pips, unused rolls, whole-hand adds and base enchants (none of the adds under PipsOnly).
        int bc = 0, mc = 0;
        if (!fx.silence)
        {
            foreach (int a in present) if (on[a]) { bc += eb[a]; mc += em[a]; }
            for (int k = 0; k < hits138; k++) { if (e138[k] == 1) bc++; else if (e138[k] == 2) mc++; }
        }
        double baseTotal = sum + P.unusedRoll * h.rolls;
        if (!pipsOnly)
        {
            baseTotal += bc * P.enchBase;
            if (on[27]) baseTotal += cnt[27] * pairKinds * P.p27add;
            if (on[44]) baseTotal += (double)wallet * cnt[44] * P.p44add;
            if (on[48]) baseTotal += cnt[48] * (capacity - n) * P.p48add;
            if (on[50]) baseTotal += cnt[50] * P.p50add;
            if (!fx.growthZero) foreach (int a in Params.GrowthAdd) if (cnt[a] > 0) baseTotal += gAdd[a];
            if (on[70]) baseTotal += cnt[70] * h.poolN * P.p70add;
            if (on[72]) baseTotal += cnt[72] * h.poolSum * P.p72mul;
            if (on[76]) baseTotal += cnt[76] * (h.rollsUsed - 1) * P.p76add;
            if (on[80]) baseTotal += cnt[80] * sumPairs * P.p80add;
            if (on[108]) baseTotal += cnt[108] * pip[P.p108idx] * P.p108add;
            if (on[135]) baseTotal += cnt[135] * inventory * P.p135add;
        }
        int redMult = fx.noRed ? 100 : P.redTable[Math.Min(redCount, P.redCap)];
        // 9. Controller multipliers, enchant multipliers, item multiplier, boss half-score.
        double X = 1;
        foreach (int a in Params.Fixed) if (on[a]) { int times = a == 138 ? hits138 : cnt[a]; for (int k = 0; k < times; k++) X *= P.mult[a]; }
        if (on[47]) for (int k = 0; k < cnt[47]; k++) X *= Math.Max(2, alive * P.p47units) / 2.0;
        if (on[49]) for (int k = 0; k < cnt[49]; k++) X *= c[5];
        if (on[51]) X *= c51;
        if (on[53]) for (int k = 0; k < cnt[53]; k++) X *= c53 < 0 ? P.c53mean : c53 / 2.0;
        if (!fx.growthZero) foreach (int a in Params.GrowthMul) if (cnt[a] > 0) X *= gMul[a];
        if (cnt[63] > 0) X *= PowI(Math.Max(10, permDice * P.p63units) / 10.0, cnt[63]);
        if (on[71]) X *= PowI(P.p71step, h.poolHot * cnt[71]);
        if (on[87]) X *= PowI(P.p87step, (maxSame - P.p87min + 1) * cnt[87]);
        if (on[89]) X *= PowI(P.p89step, goldN * cnt[89]);
        if (on[103]) X *= PowI(permDice <= P.p103strictMax ? P.p103strict : P.mult[103], cnt[103]);
        if (on[139]) X *= PowI((4 + Math.Min(P.p139max, wallet / P.p139gold) * P.p139step4) / 4.0, cnt[139]);
        if (cnt[141] > 0) X *= c141;
        if (on[147]) X *= PowI((4 + h.poolN * P.p147step4) / 4.0, cnt[147]);
        if (on[148]) X *= PowI((2 + h.rolls * P.p148units2) / 2.0, cnt[148]);
        if (on[150]) X *= PowI(ordinal + 1, cnt[150]);
        if (on[151]) X *= PowI((2 + emptySlots * P.p151units2) / 2.0, cnt[151]);
        if (on[152]) X *= PowI((4 + commons * P.p152step4) / 4.0, cnt[152]);
        for (int k = 0; k < mc; k++) X *= P.enchMult;
        X *= itemMult;
        if ((fx.firstHalf && ordinal == 0) || (fx.laterHalf && ordinal >= 1)) X *= 0.5;
        return baseTotal * hand * color * redMult * X / 1e6;
    }
}

/// <summary>Hand policy: after every roll choose which dice to lock and whether to submit, by sampled look-ahead.
/// Every evaluation scores the real submission context: the dice that would stay in the pool, the rolls thrown so
/// far, the rolls left, the stage's remaining submissions (Build.subsLeft) and the boss modifier (Build.fx).</summary>
public sealed class Planner
{
    public Build b; Params P;
    public Rng rng = new Rng(1), rollRng = new Rng(2);
    public double needed = double.PositiveInfinity;
    public int depthMax = 2, s0 = 12, s1 = 5, sQuick = 6, keepTop = 6, fullEnum = 5;
    int m, K; FDie[] dice; bool burn;
    readonly int[][] faceBuf = Enumerable.Range(0, 6).Select(_ => new int[12]).ToArray();
    readonly int[][] candBuf = Enumerable.Range(0, 6).Select(_ => new int[1100]).ToArray();
    readonly int[] idx = new int[12], ord = new int[12], ord2 = new int[12], slotOf = new int[12], dieOf = new int[12], listA = new int[12], listB = new int[12];
    readonly double[] baseC = new double[12], baseF = new double[12], slotW = new double[12];
    readonly Sub h = new Sub();
    public readonly int[] bestOrd = new int[12]; public int bestN, lastPrimary, lastRedN, lastRedCount, lastRollsUsed, lastPoolN; public HitSet lastHits;
    /// <summary>After PlayHand: the dice still alive (bit mask) and the rolls left when the hand was submitted.</summary>
    public int lastAlive, lastRollsLeft;
    /// <summary>PlayHand also applies the order tie-break for face-growth cards (the real bot always does).</summary>
    public bool steer;
    /// <summary>Faces of the hand PlayHand just finished (index = die), for callers that replay the submission.</summary>
    public int[] LastFaces => faceBuf[0];
    readonly int[] grp = new int[9];
    static readonly int[][][] Perms = BuildPerms();
    public long evals;

    static int[][][] BuildPerms()
    {
        var all = new int[5][][];
        for (int n = 0; n <= 4; n++) { var list = new List<int[]>(); Permute(Enumerable.Range(0, n).ToArray(), 0, list); all[n] = list.ToArray(); }
        return all;
    }
    static void Permute(int[] a, int k, List<int[]> outp)
    {
        if (k >= a.Length) { outp.Add((int[])a.Clone()); return; }
        for (int i = k; i < a.Length; i++) { (a[k], a[i]) = (a[i], a[k]); Permute(a, k + 1, outp); (a[k], a[i]) = (a[i], a[k]); }
    }

    public void Set(Build build)
    {
        b = build; P = build.P; dice = build.dice; m = dice.Length; K = build.capacity;
        // C76 pays per extra roll and C77 only with no rolls left, so leftover rolls are worth burning on the pool dice.
        burn = b.cnt[76] + b.cnt[77] > 0;
    }
    double U(double x) => x <= needed ? x : needed + 0.02 * (x - needed);
    static int Pop(int x) => BitOperations.PopCount((uint)x);

    void Fill(int[] order, int n, int[] face) { h.n = n; for (int k = 0; k < n; k++) h.Put(k, dice[order[k]], face[order[k]]); }
    int Eff(int die, int[] face) { int v = dice[die].pip[face[die]]; return b.cnt[12] > 0 && (v & 1) == 1 ? v + P.p12add : v; }
    /// <summary>Rough size of a die's contribution, used only to pick the die that C65 should retrigger.</summary>
    int Weight(int die, int[] face) { var d = dice[die]; return Eff(die, face) * (d.glass ? P.glassMult : 1) * (d.gold[face[die]] ? P.goldMult : 1); }
    void Keep(double v, int prim, int n, int[] order, ref double best)
    {
        if (v <= best) return;
        best = v; lastPrimary = prim; lastHits = b.lastHits; lastRedN = b.lastRedN; lastRedCount = b.lastRedCount; lastPoolN = h.poolN; Array.Copy(order, bestOrd, n);
    }
    void Try(int n, int[] face, ref double best) { Fill(ord, n, face); Keep(b.Score(h, out int prim), prim, n, ord, ref best); }

    /// <summary>Best achievable score for submitting the dice in `mask` while the dice in `pool` stay unsubmitted
    /// (searching submission order only when order matters). `rolls` = rolls left, `used` = rolls thrown this hand.
    /// Order search: every permutation up to four dice; above that structured candidates (dice assigned to slots by
    /// value for retriggers and positional seals, every die tried first, and one constructed order per pattern card:
    /// rising triple C40, alternating colours C104, reds first C105, palindrome C106, +1 chains C107).
    /// While sampled futures are valued (`light`) only the structured candidates are used, with a few first dice.</summary>
    public double EvalMask(int mask, int pool, int rolls, int used, int[] face)
    {
        int n = 0; for (int i = 0; i < m; i++) if ((mask >> i & 1) != 0) idx[n++] = i;
        bestN = n; evals++;
        h.SetPool(dice, face, pool, P.p71pip); h.rolls = rolls; h.rollsUsed = used;
        double best = -1;
        Fill(idx, n, face); Keep(b.Score(h, out int prim0), prim0, n, idx, ref best);
        if (!b.pos || n == 1) return best;
        if (!b.posSimple && n <= 4 && !light)
        {
            var perms = Perms[n];
            for (int q = 1; q < perms.Length; q++) { var perm = perms[q]; for (int k = 0; k < n; k++) ord[k] = idx[perm[k]]; Try(n, face, ref best); }
            return best;
        }
        // Values of the dice in index order; the constructed orders below are built from them.
        for (int k = 0; k < n; k++) { baseC[k] = b.contrib[k]; baseF[k] = b.full[k]; }
        if (SlotOrder(n)) Try(n, face, ref best);
        if (b.posSimple) return best;
        var cnt = b.cnt;
        if (cnt[3] + cnt[20] + cnt[33] + cnt[40] + cnt[64] + cnt[108] > 0 || b.fx.sealFirst)
        {
            // The first die matters for C03 (colour), C64 (retrigger), C108 (its pip), C33 (it wants the lowest remaining die second).
            if (!light)
                for (int a = 0; a < n; a++) TryFirst(n, a, face, ref best);
            else
            {
                // Look-ahead: only the likely first dice - the highest pip, the largest contribution, and for C03
                // the highest blue and the highest red die.
                int hi = 0, top = 0, blue = -1, redDie = -1;
                for (int k = 0; k < n; k++)
                {
                    int e = Eff(idx[k], face);
                    if (e > Eff(idx[hi], face) || (e == Eff(idx[hi], face) && baseC[k] > baseC[hi])) hi = k;
                    if (baseC[k] > baseC[top]) top = k;
                    if (cnt[3] > 0) { if (dice[idx[k]].red[face[idx[k]]]) { if (redDie < 0 || e > Eff(idx[redDie], face)) redDie = k; } else if (blue < 0 || e > Eff(idx[blue], face)) blue = k; }
                }
                TryFirst(n, hi, face, ref best);
                if (top != hi) TryFirst(n, top, face, ref best);
                if (blue >= 0 && blue != hi && blue != top) TryFirst(n, blue, face, ref best);
                if (redDie >= 0 && redDie != hi && redDie != top) TryFirst(n, redDie, face, ref best);
            }
        }
        // C40 wants the first three dice in a rising arithmetic run; there are only six such value triples.
        if (cnt[40] > 0)
            for (int v1 = 1; v1 <= 4; v1++)
                for (int d = 1; v1 + 2 * d <= 6; d++)
                {
                    int r1 = -1, b1 = -1, c2 = -1, c3 = -1;
                    for (int k = 0; k < n; k++)
                    {
                        int e = Eff(idx[k], face);
                        if (e == v1) { if (dice[idx[k]].red[face[idx[k]]]) { if (r1 < 0) r1 = k; } else if (b1 < 0) b1 = k; }
                        else if (e == v1 + d) { if (c2 < 0) c2 = k; }
                        else if (e == v1 + 2 * d) { if (c3 < 0) c3 = k; }
                    }
                    if (c2 < 0 || c3 < 0) continue;
                    if (r1 >= 0) TryOrder(n, r1, c2, c3, face, ref best);
                    if (b1 >= 0) TryOrder(n, b1, c2, c3, face, ref best);
                }
        if (cnt[104] + cnt[105] > 0 && !b.redWave && !b.blueWave) ColourOrders(n, face, ref best);
        if (cnt[106] > 0 && n >= P.p106min && Palindrome(n, face)) Try(n, face, ref best);
        if (cnt[107] > 0) { Chains(n, face); Try(n, face, ref best); }
        return best;
    }

    /// <summary>Writes into `ord` the order that puts the most valuable dice on the most retriggered slots and the
    /// least valuable ones on the sealed slots (B05 first slot, B38 slots 1/3/5/7). False when no slot differs.</summary>
    bool SlotOrder(int n)
    {
        var fx = b.fx; int r64 = b.cnt[64] * P.p64r, r65 = n == K ? b.cnt[65] * P.p65r : 0, r109 = b.cnt[109] > 0 && (n & 1) == 1 && n >= P.p109min ? b.cnt[109] * P.p109r : 0;
        if (r64 == 0 && r65 == 0 && r109 == 0 && !fx.posSeal) return false;
        bool seals = fx.posSeal;
        for (int i = 0; i < n; i++)
        {
            bool closed = (fx.sealFirst && i == 0) || (fx.sealAlt && (i & 1) == 0);
            slotW[i] = closed ? -1 : (i == P.p64idx ? r64 : 0) + (i == n - 1 ? r65 : 0) + (i == (n - 1) / 2 ? r109 : 0);
            slotOf[i] = i; dieOf[i] = i;
        }
        // Slots by weight, dice by value, both descending (stable insertion sorts; n <= 8).
        var val = seals ? baseF : baseC;
        for (int i = 1; i < n; i++) { int t = slotOf[i], j = i - 1; while (j >= 0 && slotW[slotOf[j]] < slotW[t]) { slotOf[j + 1] = slotOf[j]; j--; } slotOf[j + 1] = t; }
        for (int i = 1; i < n; i++) { int t = dieOf[i], j = i - 1; while (j >= 0 && val[dieOf[j]] < val[t]) { dieOf[j + 1] = dieOf[j]; j--; } dieOf[j + 1] = t; }
        bool same = true;
        for (int i = 0; i < n; i++) { ord[slotOf[i]] = idx[dieOf[i]]; }
        for (int i = 0; i < n; i++) if (ord[i] != idx[i]) { same = false; break; }
        return !same;
    }

    void SortByValueDesc(int[] list, int len) { for (int i = 1; i < len; i++) { int t = list[i], j = i - 1; while (j >= 0 && baseC[list[j]] < baseC[t]) { list[j + 1] = list[j]; j--; } list[j + 1] = t; } }

    /// <summary>C104: red and blue alternate (needs counts within one of each other). C105: every red left of every blue.</summary>
    void ColourOrders(int n, int[] face, ref double best)
    {
        int nr = 0, nb = 0;
        for (int k = 0; k < n; k++) { if (dice[idx[k]].red[face[idx[k]]]) listA[nr++] = k; else listB[nb++] = k; }
        if (nr == 0 || nb == 0) return;
        SortByValueDesc(listA, nr); SortByValueDesc(listB, nb);
        if (b.cnt[104] > 0 && n >= P.p104min && Math.Abs(nr - nb) <= 1)
        {
            bool redFirst = nr >= nb; int ir = 0, ib = 0;
            for (int i = 0; i < n; i++) ord[i] = idx[((i & 1) == 0) == redFirst ? listA[ir++] : listB[ib++]];
            Try(n, face, ref best);
        }
        if (b.cnt[105] > 0)
        {
            // Largest red first (C64), largest blue last (C65).
            int w = 0; for (int i = 0; i < nr; i++) ord[w++] = idx[listA[i]]; for (int i = nb - 1; i >= 0; i--) ord[w++] = idx[listB[i]];
            Try(n, face, ref best);
        }
    }

    /// <summary>C106: mirror pairs of equal pips around the centre, the most valuable pair outermost. False when the pips cannot form a palindrome.</summary>
    bool Palindrome(int n, int[] face)
    {
        for (int v = 0; v < 9; v++) grp[v] = 0;
        for (int k = 0; k < n; k++) grp[Eff(idx[k], face)] |= 1 << k;
        int oddOnes = 0; for (int v = 1; v <= 8; v++) if ((Pop(grp[v]) & 1) == 1) oddOnes++;
        if (oddOnes > 1) return false;
        int nl = 0, middle = -1;
        for (int v = 8; v >= 1; v--)
        {
            int g = grp[v];
            while (Pop(g) >= 2) { int k1 = BitOperations.TrailingZeroCount(g); g &= g - 1; int k2 = BitOperations.TrailingZeroCount(g); g &= g - 1; listA[nl] = k1; listB[nl] = k2; nl++; }
            if (g != 0) middle = BitOperations.TrailingZeroCount(g);
        }
        // Pairs by the value of their left die, descending.
        for (int i = 1; i < nl; i++) { int t = listA[i], u = listB[i], j = i - 1; while (j >= 0 && baseC[listA[j]] < baseC[t]) { listA[j + 1] = listA[j]; listB[j + 1] = listB[j]; j--; } listA[j + 1] = t; listB[j + 1] = u; }
        for (int i = 0; i < nl; i++) { ord[i] = idx[listA[i]]; ord[n - 1 - i] = idx[listB[i]]; }
        if (middle >= 0) ord[nl] = idx[middle];
        return true;
    }

    /// <summary>C107: cut the dice into rising +1 chains (smallest unused pip first, extend while pip+1 is available).</summary>
    void Chains(int n, int[] face)
    {
        int left = (1 << n) - 1, w = 0;
        while (left != 0)
        {
            int start = -1; for (int k = 0; k < n; k++) if ((left >> k & 1) != 0 && (start < 0 || Eff(idx[k], face) < Eff(idx[start], face))) start = k;
            int cur = Eff(idx[start], face); ord[w++] = idx[start]; left &= ~(1 << start);
            while (true)
            {
                int next = -1; for (int k = 0; k < n; k++) if ((left >> k & 1) != 0 && Eff(idx[k], face) == cur + 1) { next = k; break; }
                if (next < 0) break;
                ord[w++] = idx[next]; left &= ~(1 << next); cur++;
            }
        }
    }

    /// <summary>Die `a` (position in idx) first, the lowest remaining pip second, the rest ascending.</summary>
    void TryFirst(int n, int a, int[] face, ref double best)
    {
        int lo = -1; for (int k = 0; k < n; k++) if (k != a && (lo < 0 || Eff(idx[k], face) < Eff(idx[lo], face))) lo = k;
        TryOrder(n, a, lo, -1, face, ref best);
    }

    void TryOrder(int n, int a, int c, int third, int[] face, ref double best)
    {
        ord[0] = idx[a]; ord[1] = idx[c]; int w = 2;
        if (third >= 0) ord[w++] = idx[third];
        int head = w;
        for (int k = 0; k < n; k++) if (k != a && k != c && k != third) ord[w++] = idx[k];
        for (int i = head + 1; i < n; i++) { int t = ord[i], e = Eff(t, face), j = i - 1; while (j >= head && Eff(ord[j], face) > e) { ord[j + 1] = ord[j]; j--; } ord[j + 1] = t; }
        if (n == K)
        {
            // Last slot: C20 wants its pip there; C65 retriggers it, so it wants the largest contribution there.
            if (b.cnt[20] > 0) for (int t = n - 1; t >= head; t--) if (Eff(ord[t], face) == P.p20pip) { (ord[n - 1], ord[t]) = (ord[t], ord[n - 1]); break; }
            if (b.cnt[65] > 0)
            {
                int top = n - 1; for (int t = head; t < n - 1; t++) if (Weight(ord[t], face) > Weight(ord[top], face)) top = t;
                if (top != n - 1 && !(b.cnt[20] > 0 && Eff(ord[n - 1], face) == P.p20pip && Eff(ord[top], face) != P.p20pip)) (ord[n - 1], ord[top]) = (ord[top], ord[n - 1]);
            }
        }
        Try(n, face, ref best);
    }

    /// <summary>EvalMask plus a final polish of the order, used for the submission that is really made (and for the
    /// last evaluation of a simulated hand): pairwise swaps while the score rises, then - with `tieBreak` - swaps
    /// that keep the score but suit the face-growth cards better (C119 first die, C117 last die, C127 first to last).
    /// The returned value is always the score of bestOrd, so the planner self-check stays exact.</summary>
    public double FinalEval(int mask, int pool, int rolls, int used, int[] face, bool tieBreak)
    {
        double best = EvalMask(mask, pool, rolls, used, face); int n = bestN;
        if (n < 2 || (!b.pos && !(tieBreak && b.growOrder))) return best;
        Array.Copy(bestOrd, ord2, n);
        // Up to four dice every order was already tried; retrigger-only slot effects are assigned exactly. What is
        // left to polish: pattern cards with five or more dice, and positional seals.
        if (b.pos && (b.posSimple ? b.fx.posSeal : n > 4))
            for (int pass = 0; pass < 6; pass++)
            {
                bool improved = false;
                for (int i = 0; i < n - 1; i++)
                    for (int j = i + 1; j < n; j++)
                    {
                        (ord2[i], ord2[j]) = (ord2[j], ord2[i]);
                        Fill(ord2, n, face); double v = b.Score(h, out int prim);
                        if (v > best * (1 + 1e-12)) { Keep(v, prim, n, ord2, ref best); improved = true; }
                        else (ord2[i], ord2[j]) = (ord2[j], ord2[i]);
                    }
                if (!improved) break;
            }
        if (tieBreak && b.growOrder)
        {
            double pref = Preference(ord2, n, face);
            for (int pass = 0; pass < 4; pass++)
            {
                bool improved = false;
                for (int i = 0; i < n - 1; i++)
                    for (int j = i + 1; j < n; j++)
                    {
                        (ord2[i], ord2[j]) = (ord2[j], ord2[i]);
                        Fill(ord2, n, face); double v = b.Score(h, out _); double p2 = Preference(ord2, n, face);
                        if (Math.Abs(v - best) <= 1e-12 * Math.Max(1, best) && p2 > pref + 1e-9) { pref = p2; improved = true; }
                        else (ord2[i], ord2[j]) = (ord2[j], ord2[i]);
                    }
                if (!improved) break;
            }
            Array.Copy(ord2, bestOrd, n);
        }
        // Leave the planner's "last" fields describing bestOrd.
        Fill(bestOrd, n, face); double fin = b.Score(h, out int p3);
        lastPrimary = p3; lastHits = b.lastHits; lastRedN = b.lastRedN; lastRedCount = b.lastRedCount; lastPoolN = h.poolN;
        return fin;
    }

    /// <summary>How well an order feeds the face-growth cards (only compared between orders of equal score).</summary>
    double Preference(int[] order, int n, int[] face)
    {
        int f = order[0], l = order[n - 1]; var df = dice[f]; var dl = dice[l]; int pf = df.pip[face[f]], pl = dl.pip[face[l]]; double dir = b.lowBuild ? -1 : 1, s = 0;
        if (b.cnt[119] > 0 && !df.temp) { int same = 0; for (int k = 0; k < 6; k++) if (df.pip[k] == pf) same++; if (same < 6) s += 100 * (dir * pf + 3 * same); }
        if (b.cnt[117] > 0 && !dl.temp && !dl.gold[face[l]]) s += 10 * (6 + pl);
        if (b.cnt[127] > 0 && b.ordinal == P.p127ord && n >= P.p127min && !dl.temp) s += 20 * dir * (pf * (df.gold[face[f]] ? 2 : 1) - pl);
        return s;
    }

    readonly int[] hcBuf = new int[64];
    public int exactLimit = 6;

    /// <summary>MustFill (B01): a submission must hold exactly min(capacity, alive dice) dice. -1 = no such rule.</summary>
    int Need(int locked, int rolled) => b.fx.mustFill ? Math.Min(K, Pop(locked) + Pop(rolled)) : -1;
    /// <summary>Grows or trims `add` to exactly `count` of the rolled dice (highest pips are added, lowest index removed).</summary>
    int Fit(int add, int rolled, int count, int[] face)
    {
        while (Pop(add) > count) add &= add - 1;
        while (Pop(add) < count)
        {
            int pick = -1; for (int i = 0; i < m; i++) if ((rolled >> i & 1) != 0 && (add >> i & 1) == 0 && (pick < 0 || Eff(i, face) > Eff(pick, face))) pick = i;
            if (pick < 0) break; add |= 1 << pick;
        }
        return add;
    }

    /// <summary>With many free dice the exact subset search is too slow; use structured candidates plus hill climbing.</summary>
    double BestCompleteHeur(int locked, int rolled, int rolls, int used, int[] face, out int add)
    {
        int nc = Hold(locked, rolled, face, hcBuf, 0); double best = -1; add = 0; int nl = Pop(locked), room = K - nl, need = Need(locked, rolled);
        int top = 0;
        for (int t = 0; t < room; t++)
        {
            int pick = -1; for (int i = 0; i < m; i++) if ((rolled >> i & 1) != 0 && (top >> i & 1) == 0 && (pick < 0 || Eff(i, face) > Eff(pick, face))) pick = i;
            if (pick < 0) break; top |= 1 << pick;
        }
        hcBuf[nc++] = top;
        if (b.cnt[149] + b.cnt[26] > 0 && nl == 0)
        {
            // Single-die cards: the one die with the largest rough contribution.
            int one = -1; for (int i = 0; i < m; i++) if ((rolled >> i & 1) != 0 && (one < 0 || Weight(i, face) > Weight(one, face))) one = i;
            if (one >= 0) hcBuf[nc++] = 1 << one;
        }
        for (int k = 0; k < nc; k++)
        {
            int a = need < 0 ? hcBuf[k] : Fit(hcBuf[k], rolled, need - nl, face);
            int tot = locked | a; if (tot == 0) continue;
            double v = EvalMask(tot, rolled & ~a, rolls, used, face); if (v > best) { best = v; add = a; }
        }
        for (int pass = 0; pass < 6; pass++)
        {
            bool improved = false;
            if (need < 0)
                for (int i = 0; i < m; i++)
                {
                    if ((rolled >> i & 1) == 0) continue;
                    int a2 = add ^ (1 << i); if (nl + Pop(a2) > K || (locked | a2) == 0) continue;
                    double v = EvalMask(locked | a2, rolled & ~a2, rolls, used, face); if (v > best) { best = v; add = a2; improved = true; }
                }
            else
                // The count is fixed, so climb by exchanging one chosen die for one left out.
                for (int i = 0; i < m; i++)
                {
                    if ((add >> i & 1) == 0) continue;
                    for (int j = 0; j < m; j++)
                    {
                        if ((rolled >> j & 1) == 0 || (add >> j & 1) != 0) continue;
                        int a2 = add & ~(1 << i) | 1 << j;
                        double v = EvalMask(locked | a2, rolled & ~a2, rolls, used, face); if (v > best) { best = v; add = a2; improved = true; break; }
                    }
                }
            if (!improved) break;
        }
        return best < 0 ? 0 : best;
    }

    /// <summary>Best submission available right now: the locked dice plus any subset `add` of the freshly rolled ones;
    /// the rolled dice that are not added are the pool dice of that submission.</summary>
    double BestComplete(int locked, int rolled, int rolls, int used, int[] face, out int add)
    {
        if (Pop(rolled) > exactLimit) return BestCompleteHeur(locked, rolled, rolls, used, face, out add);
        double best = -1; add = 0; int nl = Pop(locked), need = Need(locked, rolled);
        for (int a = rolled; ; a = (a - 1) & rolled)
        {
            int tot = locked | a, k = nl + Pop(a);
            if (tot != 0 && k <= K && (need < 0 || k == need)) { double v = EvalMask(tot, rolled & ~a, rolls, used, face); if (v > best) { best = v; add = a; } }
            if (a == 0) break;
        }
        return best < 0 ? 0 : best;
    }

    int Trim(int mask, int room) { while (Pop(mask) > room) mask &= mask - 1; return mask; }

    int Hold(int locked, int rolled, int[] face, int[] buf, int addS)
    {
        int n = 0, room = K - Pop(locked);
        buf[n++] = 0; if (addS > 0) buf[n++] = addS;
        if (room <= 0) return n;
        for (int v = 0; v < 9; v++) grp[v] = 0;
        int blue = 0, redm = 0, odd = 0, distinct = 0;
        for (int i = 0; i < m; i++)
        {
            if ((rolled >> i & 1) == 0) continue;
            int v = Eff(i, face); if (grp[v] == 0) distinct |= 1 << i; grp[v] |= 1 << i;
            if (dice[i].red[face[i]]) redm |= 1 << i; else blue |= 1 << i;
            if ((v & 1) == 1) odd |= 1 << i;
        }
        int g1 = 0, g2 = 0, g3 = 0;
        for (int v = 6; v >= 1; v--)
        {
            int g = grp[v]; if (g == 0) continue;
            buf[n++] = Trim(g, room); if ((g & blue) != 0 && (g & redm) != 0) { buf[n++] = Trim(g & blue, room); buf[n++] = Trim(g & redm, room); }
            if (Pop(g) > Pop(g1)) { g3 = g2; g2 = g1; g1 = g; } else if (Pop(g) > Pop(g2)) { g3 = g2; g2 = g; } else if (Pop(g) > Pop(g3)) g3 = g;
        }
        buf[n++] = Trim(g1 | g2, room); buf[n++] = Trim(g1 | g2 | g3, room);
        buf[n++] = Trim(blue, room); buf[n++] = Trim(redm, room); buf[n++] = Trim(distinct, room);
        if (b.cnt[13] + b.cnt[14] > 0) buf[n++] = Trim(odd, room);
        if (b.cnt[144] > 0) buf[n++] = Trim(rolled & ~odd, room);
        if (b.cnt[104] + b.cnt[18] > 0) { int half = Math.Min(Pop(redm), Pop(blue)); if (half > 0) buf[n++] = Trim(Trim(redm, Math.Min(half + 1, (room + 1) / 2)) | Trim(blue, Math.Min(half + 1, (room + 1) / 2)), room); }
        if (b.cnt[11] > 0) buf[n++] = Trim(Trim(grp[2], 2) | Trim(grp[3], 2), room);
        if (b.cnt[66] > 0) { int lowm = 0; for (int v = 1; v <= 8; v++) if (P.p66[v]) lowm |= grp[v]; buf[n++] = Trim(lowm, room); }
        if (b.cnt[80] > 0)
        {
            int pairs = 0;
            for (int a = 1; a * 2 < P.p80sum && a <= 8; a++) { int o = P.p80sum - a; if (o > 8) continue; int k = Math.Min(Pop(grp[a]), Pop(grp[o])); pairs |= Trim(grp[a], k) | Trim(grp[o], k); }
            buf[n++] = Trim(pairs, room);
        }
        int u = 0;
        for (int i = 0; i < n; i++) { bool dup = false; for (int j = 0; j < u; j++) if (buf[j] == buf[i]) { dup = true; break; } if (!dup) buf[u++] = buf[i]; }
        return u;
    }

    int SampleFace(FDie d) { double x = rng.NextDouble(); int f = 0; while (x >= d.cdf[f]) f++; return f; }

    /// <summary>True while sampled futures are being valued: the order search then uses only constructed candidates
    /// (no permutations, a few first dice). The hand that is really on the table is always searched in full.</summary>
    bool light;

    double EVRoll(int locked, int alive, int r, int used, int[] faceIn, int depth, bool c05used, int limit, int samples)
    {
        bool outer = light; light = true;
        double value = EVRollCore(locked, alive, r, used, faceIn, depth, c05used, limit, samples);
        light = outer; return value;
    }

    double EVRollCore(int locked, int alive, int r, int used, int[] faceIn, int depth, bool c05used, int limit, int samples)
    {
        int[] f = faceBuf[depth]; double sum = 0;
        for (int s = 0; s < samples; s++)
        {
            Array.Copy(faceIn, f, m);
            int aliveNow = alive, rolled = 0;
            for (int i = 0; i < m; i++)
            {
                int bit = 1 << i; if ((alive & bit) == 0 || (locked & bit) != 0) continue;
                var d = dice[i];
                if (d.glass && rng.NextDouble() < b.glassBreak) { aliveNow &= ~bit; continue; }
                f[i] = SampleFace(d); rolled |= bit;
            }
            int r2 = r - 1, used2 = used + 1; bool c05 = c05used;
            if (r2 == 0 && !c05 && b.cnt[5] > 0) { int un = Pop(aliveNow) - Pop(locked); if (un >= 1 && un <= 2) { r2 += 3 * b.cnt[5]; c05 = true; } }
            double best = 0; int addS = 0;
            if ((locked | rolled) != 0) best = U(BestComplete(locked, rolled, r2, used2, f, out addS));
            if (r2 > 0 && depth < limit)
            {
                int[] cand = candBuf[depth]; int nc = Hold(locked, rolled, f, cand, addS);
                for (int k = 0; k < nc; k++)
                {
                    int L2 = locked | cand[k]; if ((aliveNow & ~L2) == 0) continue;
                    double v = EVRoll(L2, aliveNow, r2, used2, f, depth + 1, c05, limit, s1); if (v > best) best = v;
                }
            }
            else if (r2 > 0 && burn && (locked | addS) != 0 && (rolled & ~addS) != 0)
            {
                // Look-ahead horizon reached with rolls left: estimate "lock this hand and throw the leftover rolls at
                // the pool dice" by scoring the same submission with every remaining roll used.
                double v = U(EvalMask(locked | addS, rolled & ~addS, 0, used2 + r2, f)); if (v > best) best = v;
            }
            sum += best;
        }
        return sum / samples;
    }

    /// <summary>Returns the dice to lock now; submit=true means lock them and submit. `r` = rolls left, `used` = rolls thrown.</summary>
    public int Decide(int locked, int alive, int rolled, int r, int used, int[] face, bool c05used, out bool submit)
    {
        int addS = 0; double raw = (locked | rolled) == 0 ? 0 : BestComplete(locked, rolled, r, used, face, out addS);
        submit = true;
        if (r <= 0 || raw >= needed) return addS;
        int[] cand = candBuf[0]; int nc;
        if (Pop(rolled) <= fullEnum) { nc = 0; for (int a = rolled; ; a = (a - 1) & rolled) { cand[nc++] = a; if (a == 0) break; } }
        else nc = Hold(locked, rolled, face, cand, addS);
        ulong seed = rng.Next();
        Span<double> val = stackalloc double[nc];
        for (int k = 0; k < nc; k++)
        {
            int L2 = locked | cand[k];
            if (Pop(L2) > K || (alive & ~L2) == 0) { val[k] = -1; continue; }
            rng.s = seed; val[k] = EVRoll(L2, alive, r, used, face, 1, c05used, 1, depthMax >= 2 && r >= 2 ? sQuick : s0);
        }
        double bestV = U(raw); int bestH = -1;
        if (depthMax >= 2 && r >= 2)
        {
            for (int t = 0; t < keepTop; t++)
            {
                int top = -1; for (int k = 0; k < nc; k++) if (val[k] >= 0 && (top < 0 || val[k] > val[top])) top = k;
                if (top < 0) break;
                rng.s = seed ^ 0x5bd1e995UL; double v = EVRoll(locked | cand[top], alive, r, used, face, 1, c05used, 2, s0);
                val[top] = -1;
                if (v > bestV * 1.01) { bestV = v; bestH = cand[top]; }
            }
        }
        else
        {
            for (int k = 0; k < nc; k++) if (val[k] > bestV * 1.01) { bestV = val[k]; bestH = cand[k]; }
        }
        rng.s = seed * 0x2545F4914F6CDD1DUL + 99;
        if (bestH < 0) return addS;
        submit = false; return bestH;
    }

    /// <summary>Plays one simulated hand with this build. The physical outcome of die i on roll k comes from rollRng
    /// in a fixed layout so that two builds compared with the same seed see the same dice.</summary>
    public double PlayHand(out int primary, out int nSub)
    {
        int[] face = faceBuf[0]; int locked = 0, alive = (1 << m) - 1, r = b.rollsPerHand, used = 0; bool c05 = false;
        ulong rollSeed = rollRng.Next(); int rollNo = 0;
        while (true)
        {
            int rolled = 0;
            for (int i = 0; i < m; i++)
            {
                int bit = 1 << i; if ((alive & bit) == 0 || (locked & bit) != 0) continue;
                var one = new Rng(rollSeed + (ulong)(rollNo * 64 + i) * 7919UL);
                var d = dice[i];
                if (d.glass && one.NextDouble() < b.glassBreak) { alive &= ~bit; continue; }
                double x = one.NextDouble(); int f = 0; while (x >= d.cdf[f]) f++;
                face[i] = f; rolled |= bit;
            }
            r--; rollNo++; used++; lastRollsUsed = used;
            if (r == 0 && !c05 && b.cnt[5] > 0) { int un = Pop(alive) - Pop(locked); if (un >= 1 && un <= 2) { r += 3 * b.cnt[5]; c05 = true; } }
            if ((locked | rolled) == 0) { primary = -1; nSub = 0; lastHits = default; lastRedN = 0; lastRedCount = 0; lastPoolN = 0; bestN = 0; return 0; }
            int add = Decide(locked, alive, rolled, r, used, face, c05, out bool submit);
            locked |= add;
            if (submit) { double v = FinalEval(locked, alive & ~locked, r, used, face, steer); primary = lastPrimary; nSub = Pop(locked); lastAlive = alive; lastRollsLeft = r; return v; }
        }
    }
}
