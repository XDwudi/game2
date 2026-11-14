// Fast planning model. Score() mirrors Core/Rules/ScoreCalculator.Evaluate with doubles instead of BigInteger so a
// hand policy can evaluate millions of submissions. `verify` mode checks it against the real calculator.
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

public sealed class FDie
{
    public string id;
    public int[] pip = new int[6]; public bool[] red = new bool[6], gold = new bool[6];
    public double[] cdf = new double[6];
    public bool glass, temp; public int six;
}

public sealed class Params
{
    public readonly GameConfig cfg;
    public readonly int[] hBase = new int[13], hInc = new int[13], hPri = new int[13];
    public readonly string[] hId = new string[13];
    public readonly Dictionary<string, int> hIndex = new Dictionary<string, int>();
    public readonly int PAIR, TWO_PAIR, TRIPS, FULL, THREE_PAIR, QUADS, TWO_TRIPS, FIVE, FOUR_PAIR, SIX, SEVEN, EIGHT, STRAIGHT;
    public readonly int blueBase, blueInc, redBase, redInc;
    public readonly int[] redTable = new int[9];
    public readonly double[] mult = new double[54];
    public static readonly int[] Fixed = { 2, 6, 8, 9, 11, 14, 18, 28, 30, 31, 32, 33, 40, 42, 46 };
    public readonly double c53mean; public readonly int[] c53units;
    public readonly int p6min, p10pip, p10add, p11c2, p11c3, p12add, p13add, p17add, p19max, p19add, p20pip, p20add, p24from, p24to, p27add, p28min, p29len, p30min, p31min, p32d, p33min, p33diff, p39add, p44add, p45mult, p47units, p48add, p50add, enchBase, enchMult, unusedRoll, glassMult, goldMult, minStraight;
    public readonly bool[] p17 = new bool[9];
    public readonly double glassBreak;
    public Params(GameConfig c)
    {
        cfg = c;
        for (int i = 0; i < 13; i++) { var h = c.handDefinitions[i]; hId[i] = h.id; hIndex[h.id] = i; hBase[i] = h.baseMult100; hInc[i] = h.incrementPerLevel100; hPri[i] = h.structuralPriority; }
        PAIR = hIndex["pair"]; TWO_PAIR = hIndex["two_pair"]; TRIPS = hIndex["trips"]; FULL = hIndex["full_house"]; THREE_PAIR = hIndex["three_pair"]; QUADS = hIndex["quads"]; TWO_TRIPS = hIndex["two_trips"];
        FIVE = hIndex["five_kind"]; FOUR_PAIR = hIndex["four_pair"]; SIX = hIndex["six_kind"]; SEVEN = hIndex["seven_kind"]; EIGHT = hIndex["eight_kind"]; STRAIGHT = hIndex["straight"];
        var b = c.Color("all_blue"); var r = c.Color("all_red"); blueBase = b.baseMult100; blueInc = b.incrementPerLevel100; redBase = r.baseMult100; redInc = r.incrementPerLevel100;
        foreach (var rr in c.redRewards) redTable[rr.count] = rr.factor100;
        foreach (int id in Fixed) mult[id] = c.Controller("C" + id.ToString("D2")).parameters.scoreMultiplier;
        Func<int, ControllerParameters> p = id => c.Controller("C" + id.ToString("D2")).parameters;
        c53units = p(53).factorUnits2; c53mean = c53units.Average() / 2.0;
        p6min = p(6).minDistinct; p10pip = p(10).pip; p10add = p(10).perDieAdd; p11c2 = p(11).requiredCounts.pip2; p11c3 = p(11).requiredCounts.pip3; p12add = p(12).oddToEvenAdd; p13add = p(13).oddPerDieAdd;
        p17add = p(17).perDieAdd; foreach (int v in p(17).pips) p17[v] = true; p19max = p(19).maxPermanentSixFaceSum; p19add = p(19).perDieAdd; p20pip = p(20).lastActualCapacitySlotPip; p20add = p(20).perDieAdd;
        p24from = p(24).redGroupKeyFrom; p24to = p(24).redGroupKeyTo; p27add = p(27).baseAddPerDistinctPairedPip; p28min = p(28).minDistinctPairs; p29len = p(29).minimumStraightLength; p30min = p(30).minSamePip; p31min = p(31).minSamePip;
        p32d = p(32).distinctPips; p33min = p(33).minDice; p33diff = p(33).firstMinusSecondAtLeast; p39add = p(39).perDieAdd; p44add = p(44).baseAddPerFrozenGold; p47units = p(47).perControllerUnits2; p45mult = p(45).matchingDieContributionMultiplier;
        p48add = p(48).baseAddPerEmptySubmitSlot; p50add = p(50).baseAdd; enchBase = c.enchantRules.baseAdd; enchMult = c.enchantRules.scoreMultiplier; unusedRoll = c.bonusPerUnusedRoll;
        glassMult = c.glassContributionMultiplier; goldMult = c.goldContributionMultiplier; minStraight = c.minStraightLength; glassBreak = c.glassBreakPercent / 100.0;
    }
}

public sealed class Build
{
    public Params P;
    public int[] cnt = new int[54], eb = new int[54], em = new int[54];
    public int[] present = Array.Empty<int>();
    public int alive, capacity, ordinal, rollsPerHand, submissions, prev = -1;
    public long wallet; public bool redWave, pos;
    public int[] handLv = new int[13]; public int blueLv, redLv;
    public FDie[] dice = Array.Empty<FDie>();
    public int c45Red, c45Blue; public double c51 = 1, c53 = -1;
    public ulong lastHits;

    public static Build From(RunState s, Params P, bool inStage, bool large = false, bool small = false)
    {
        var cfg = P.cfg; var b = new Build { P = P };
        var bindings = inStage && s.stage != null && s.stage.started && !s.stage.settled ? s.stage.mimicBindings : null;
        var all = s.controllers.ToArray();
        var sources = ControllerRules.ResolveSources(all, bindings);
        foreach (var src in sources)
        {
            if (src.abilityId == null) continue;
            int a = int.Parse(src.abilityId.Substring(1)); b.cnt[a]++;
            if (src.enchantmentId == "base10") b.eb[a]++; else if (src.enchantmentId == "mult2") b.em[a]++;
            if (a == 45) { if (src.state.activeColor == "red") b.c45Red++; else b.c45Blue++; }
            if (a == 51) b.c51 *= src.state.turnoverUnits2 / 2.0;
        }
        b.present = Enumerable.Range(1, 53).Where(i => b.cnt[i] > 0).ToArray();
        b.alive = all.Count(c => c.alive);
        b.capacity = inStage && s.stage != null && s.stage.started ? s.stage.capacity : ControllerRules.Capacity(all, cfg, false, bindings);
        b.rollsPerHand = ControllerRules.RollBudget(all, cfg, inStage && s.stage != null ? s.stage.stageRollBonus : 0, bindings);
        b.submissions = ControllerRules.SubmissionBudget(all, cfg);
        b.wallet = s.gold; b.ordinal = s.stage == null ? 0 : s.stage.submissionOrdinal;
        b.prev = string.IsNullOrEmpty(s.previousPrimaryHandId) || s.previousPrimaryHandId == "scatter" ? -1 : P.hIndex[s.previousPrimaryHandId];
        foreach (var h in s.handLevels) b.handLv[P.hIndex[h.handId]] = h.level;
        foreach (var h in s.colorLevels) { if (h.handId == "all_blue") b.blueLv = h.level; else b.redLv = h.level; }
        b.pos = b.cnt[3] + b.cnt[20] + b.cnt[33] + b.cnt[40] > 0;
        b.dice = s.dice.OrderBy(d => d.createdSerial).Select(d => MakeDie(d, cfg, large, small, b.cnt[34], b.cnt[35])).ToArray();
        return b;
    }

    public static FDie MakeDie(DieInstance d, GameConfig cfg, bool large, bool small, int high, int low)
    {
        var faces = d.faces.OrderBy(f => f.faceId, StringComparer.Ordinal).ToArray();
        var units = ProbabilityCalculator.BuildUnits(faces, large, small, cfg, high, low);
        var f = new FDie { id = d.dieId, glass = d.isGlass, temp = d.lifetime != "permanent", six = faces.Sum(x => x.permanentPip) };
        double acc = 0;
        for (int i = 0; i < 6; i++) { f.pip[i] = faces[i].permanentPip; f.red[i] = faces[i].baseColor == "red"; f.gold[i] = faces[i].isGold; acc += units[i] / (double)cfg.probabilityUnits; f.cdf[i] = acc; }
        f.cdf[5] = 1.0000001;
        return f;
    }

    public double Score(int n, int[] sp, bool[] sr, bool[] sg, bool[] sgl, int[] ssix, int rolls, out int primary)
    {
        var P = this.P; var cnt = this.cnt;
        Span<int> pip = stackalloc int[n]; Span<bool> red = stackalloc bool[n]; Span<int> c = stackalloc int[9]; c.Clear();
        bool has12 = cnt[12] > 0, changedOdd = false;
        for (int i = 0; i < n; i++) { int v = sp[i]; if (has12 && (v & 1) == 1) { v += P.p12add; changedOdd = true; } pip[i] = v; red[i] = sr[i] || redWave; }
        if (cnt[3] > 0) red[0] = true;
        int redN = 0; bool anyOdd = false, allOdd = true, anyLow = false, anySmall = false, redOne = false;
        for (int i = 0; i < n; i++)
        {
            int v = pip[i]; c[v]++;
            if (red[i]) { redN++; if (v == P.p24from) redOne = true; }
            if ((v & 1) == 1) anyOdd = true; else allOdd = false;
            if (P.p17[v]) anyLow = true; if (ssix[i] <= P.p19max) anySmall = true;
        }
        int distinct = 0, maxSame = 0, pairKinds = 0, trips = 0, run = 0, maxRun = 0;
        for (int v = 1; v <= 6; v++)
        {
            int k = c[v];
            if (k == 0) { run = 0; continue; }
            run++; if (run > maxRun) maxRun = run;
            distinct++; if (k > maxSame) maxSame = k; if (k >= 2) pairKinds++; if (k >= 3) trips++;
        }
        Span<int> mh = stackalloc int[13]; int nm = 0;
        if (maxSame >= 2) mh[nm++] = P.PAIR; if (pairKinds >= 2) mh[nm++] = P.TWO_PAIR; if (maxSame >= 3) mh[nm++] = P.TRIPS; if (trips >= 1 && pairKinds >= 2) mh[nm++] = P.FULL;
        if (pairKinds >= 3) mh[nm++] = P.THREE_PAIR; if (maxSame >= 4) mh[nm++] = P.QUADS; if (trips >= 2) mh[nm++] = P.TWO_TRIPS; if (maxSame >= 5) mh[nm++] = P.FIVE; if (pairKinds >= 4) mh[nm++] = P.FOUR_PAIR;
        if (maxSame >= 6) mh[nm++] = P.SIX; if (maxSame >= 7) mh[nm++] = P.SEVEN; if (maxSame >= 8) mh[nm++] = P.EIGHT;
        int group = 0, bestPri = 0; primary = -1;
        for (int k = 0; k < nm; k++) { int h = mh[k]; int m = P.hBase[h] + P.hInc[h] * handLv[h]; if (m > group) group = m; if (P.hPri[h] > bestPri) { bestPri = P.hPri[h]; primary = h; } }
        int reqLen = cnt[29] > 0 ? P.p29len : P.minStraight;
        bool hasStraight = maxRun >= reqLen; int straight = 0;
        if (hasStraight) { int S = P.STRAIGHT; straight = P.hBase[S] + P.hInc[S] * handLv[S]; if (P.hPri[S] > bestPri) primary = S; }
        int hand = Math.Max(100, group + straight);
        int color = redN == n ? P.redBase + P.redInc * redLv : redN == 0 ? P.blueBase + P.blueInc * blueLv : 100;

        ulong hits = 0;
        foreach (int a in present)
        {
            bool h;
            switch (a)
            {
                case 1: case 3: case 9: case 38: case 42: case 45: case 46: case 47: case 49: case 50: case 51: case 53: h = true; break;
                case 2: h = redN == 0; break;
                case 6: h = distinct >= P.p6min; break;
                case 7: h = ordinal == 0 && n == 2 && pip[0] != pip[1]; break;
                case 8: case 48: h = n < capacity; break;
                case 10: h = c[P.p10pip] > 0; break;
                case 11: h = c[2] >= P.p11c2 && c[3] >= P.p11c3; break;
                case 12: h = changedOdd; break;
                case 13: h = anyOdd; break;
                case 14: h = allOdd; break;
                case 15: { h = false; for (int i = 1; i < n - 1; i++) if (!red[i] && red[i - 1] && red[i + 1]) { h = true; break; } break; }
                case 17: h = anyLow; break;
                case 18: h = redN == n - redN; break;
                case 19: h = anySmall; break;
                case 20: h = n == capacity && pip[n - 1] == P.p20pip; break;
                case 24: h = redOne; break;
                case 25: h = redN > 0; break;
                case 26: h = n == 1; break;
                case 27: h = pairKinds > 0; break;
                case 28: h = pairKinds >= P.p28min; break;
                case 29: h = maxRun >= P.p29len; break;
                case 30: h = maxSame >= P.p30min; break;
                case 31: h = maxSame >= P.p31min; break;
                case 32: h = distinct == P.p32d; break;
                case 33: h = n >= P.p33min && pip[0] - pip[1] >= P.p33diff; break;
                case 36: case 37: h = n >= 2 && n < capacity; break;
                case 39: h = redN < n; break;
                case 40: h = n >= 3 && pip[1] > pip[0] && pip[2] - pip[1] == pip[1] - pip[0]; break;
                case 43: h = primary >= 0 && primary == prev; break;
                case 44: h = wallet > 0; break;
                default: h = false; break;
            }
            if (h) hits |= 1UL << a;
        }
        lastHits = hits;
        int a10 = cnt[10] * P.p10add, a13 = cnt[13] * P.p13add, a17 = cnt[17] * P.p17add, a19 = cnt[19] * P.p19add, a20 = cnt[20] * P.p20add, a39 = cnt[39] * P.p39add;
        double sum = 0;
        for (int i = 0; i < n; i++)
        {
            int v = pip[i]; double x = v;
            if (v == P.p10pip) x += a10; if ((v & 1) == 1) x += a13; if (P.p17[v]) x += a17; if (ssix[i] <= P.p19max) x += a19;
            if (a20 != 0 && n == capacity && i == capacity - 1 && v == P.p20pip) x += a20;
            if (!red[i]) x += a39;
            int k45 = red[i] ? c45Red : c45Blue; for (int k = 0; k < k45; k++) x *= P.p45mult;
            if (sgl[i]) x *= P.glassMult; if (sg[i]) x *= P.goldMult;
            sum += x;
        }
        int bc = 0, mc = 0;
        foreach (int a in present) if ((hits >> a & 1) != 0) { bc += eb[a]; mc += em[a]; }
        double baseTotal = sum + P.unusedRoll * rolls + bc * P.enchBase;
        if ((hits >> 27 & 1) != 0) baseTotal += cnt[27] * pairKinds * P.p27add;
        if ((hits >> 44 & 1) != 0) baseTotal += (double)wallet * cnt[44] * P.p44add;
        if ((hits >> 48 & 1) != 0) baseTotal += cnt[48] * (capacity - n) * P.p48add;
        if ((hits >> 50 & 1) != 0) baseTotal += cnt[50] * P.p50add;
        int redCount = 0;
        if (redN > 0)
        {
            if (cnt[25] > 0) redCount = redN;
            else
            {
                Span<int> rc = stackalloc int[9]; rc.Clear(); bool map = cnt[24] > 0;
                for (int i = 0; i < n; i++) if (red[i]) { int key = map && pip[i] == P.p24from ? P.p24to : pip[i]; int q = ++rc[key]; if (q > redCount) redCount = q; }
            }
        }
        int redMult = P.redTable[Math.Min(redCount, 8)];
        double X = 1;
        foreach (int a in Params.Fixed) if ((hits >> a & 1) != 0) for (int k = 0; k < cnt[a]; k++) X *= P.mult[a];
        if ((hits >> 47 & 1) != 0) for (int k = 0; k < cnt[47]; k++) X *= Math.Max(2, alive * P.p47units) / 2.0;
        if ((hits >> 49 & 1) != 0) for (int k = 0; k < cnt[49]; k++) X *= c[5];
        if ((hits >> 51 & 1) != 0) X *= c51;
        if ((hits >> 53 & 1) != 0) for (int k = 0; k < cnt[53]; k++) X *= c53 < 0 ? P.c53mean : c53 / 2.0;
        for (int k = 0; k < mc; k++) X *= P.enchMult;
        return baseTotal * hand * color * redMult * X / 1e6;
    }
}

/// <summary>Hand policy: after every roll choose which dice to lock and whether to submit, by sampled look-ahead.</summary>
public sealed class Planner
{
    public Build b; Params P;
    public Rng rng = new Rng(1), rollRng = new Rng(2);
    public double needed = double.PositiveInfinity;
    public int depthMax = 2, s0 = 12, s1 = 5, sQuick = 6, keepTop = 6, fullEnum = 5;
    int m, K; FDie[] dice;
    readonly int[][] faceBuf = Enumerable.Range(0, 6).Select(_ => new int[12]).ToArray();
    readonly int[][] candBuf = Enumerable.Range(0, 6).Select(_ => new int[1100]).ToArray();
    readonly int[] sp = new int[12], ssix = new int[12], idx = new int[12], ord = new int[12];
    readonly bool[] sr = new bool[12], sg = new bool[12], sgl = new bool[12];
    public readonly int[] bestOrd = new int[12]; public int bestN, lastPrimary; public ulong lastHits;
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

    public void Set(Build build) { b = build; P = build.P; dice = build.dice; m = dice.Length; K = build.capacity; }
    double U(double x) => x <= needed ? x : needed + 0.02 * (x - needed);
    static int Pop(int x) => BitOperations.PopCount((uint)x);

    void Fill(int[] order, int n, int[] face)
    {
        for (int k = 0; k < n; k++) { var d = dice[order[k]]; int f = face[order[k]]; sp[k] = d.pip[f]; sr[k] = d.red[f]; sg[k] = d.gold[f]; sgl[k] = d.glass; ssix[k] = d.six; }
    }
    int Eff(int die, int[] face) { int v = dice[die].pip[face[die]]; return b.cnt[12] > 0 && (v & 1) == 1 ? v + P.p12add : v; }

    /// <summary>Best achievable score for this set of dice (searching submission order only when order matters).</summary>
    public double EvalMask(int mask, int rolls, int[] face)
    {
        int n = 0; for (int i = 0; i < m; i++) if ((mask >> i & 1) != 0) idx[n++] = i;
        bestN = n; evals++;
        if (!b.pos || n == 1)
        {
            Fill(idx, n, face); Array.Copy(idx, bestOrd, n);
            double v = b.Score(n, sp, sr, sg, sgl, ssix, rolls, out lastPrimary); lastHits = b.lastHits; return v;
        }
        double best = -1;
        if (n <= 4)
        {
            foreach (var perm in Perms[n])
            {
                for (int k = 0; k < n; k++) ord[k] = idx[perm[k]];
                Fill(ord, n, face); double v = b.Score(n, sp, sr, sg, sgl, ssix, rolls, out int prim);
                if (v > best) { best = v; lastPrimary = prim; lastHits = b.lastHits; Array.Copy(ord, bestOrd, n); }
            }
            return best;
        }
        // The first die matters for C03 (colour) and C33 (it wants the lowest remaining die second).
        for (int a = 0; a < n; a++)
        {
            int lo = -1; for (int k = 0; k < n; k++) if (k != a && (lo < 0 || Eff(idx[k], face) < Eff(idx[lo], face))) lo = k;
            TryOrder(n, a, lo, -1, rolls, face, ref best);
        }
        // C40 wants the first three dice in a rising arithmetic run; there are only six such value triples.
        if (b.cnt[40] > 0)
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
                    if (r1 >= 0) TryOrder(n, r1, c2, c3, rolls, face, ref best);
                    if (b1 >= 0) TryOrder(n, b1, c2, c3, rolls, face, ref best);
                }
        return best;
    }

    void TryOrder(int n, int a, int c, int third, int rolls, int[] face, ref double best)
    {
        ord[0] = idx[a]; ord[1] = idx[c]; int w = 2;
        if (third >= 0) ord[w++] = idx[third];
        int head = w;
        for (int k = 0; k < n; k++) if (k != a && k != c && k != third) ord[w++] = idx[k];
        for (int i = head + 1; i < n; i++) { int t = ord[i], e = Eff(t, face), j = i - 1; while (j >= head && Eff(ord[j], face) > e) { ord[j + 1] = ord[j]; j--; } ord[j + 1] = t; }
        if (b.cnt[20] > 0 && n == K)
            for (int t = n - 1; t >= head; t--) if (Eff(ord[t], face) == P.p20pip) { (ord[n - 1], ord[t]) = (ord[t], ord[n - 1]); break; }
        Fill(ord, n, face); double v = b.Score(n, sp, sr, sg, sgl, ssix, rolls, out int prim);
        if (v > best) { best = v; lastPrimary = prim; lastHits = b.lastHits; Array.Copy(ord, bestOrd, n); }
    }

    readonly int[] hcBuf = new int[64];
    public int exactLimit = 6;

    /// <summary>With many free dice the exact subset search is too slow; use structured candidates plus hill climbing.</summary>
    double BestCompleteHeur(int locked, int rolled, int rolls, int[] face, out int add)
    {
        int nc = Hold(locked, rolled, face, hcBuf, 0); double best = -1; add = 0; int nl = Pop(locked), room = K - nl;
        int top = 0;
        for (int t = 0; t < room; t++)
        {
            int pick = -1; for (int i = 0; i < m; i++) if ((rolled >> i & 1) != 0 && (top >> i & 1) == 0 && (pick < 0 || Eff(i, face) > Eff(pick, face))) pick = i;
            if (pick < 0) break; top |= 1 << pick;
        }
        hcBuf[nc++] = top;
        for (int k = 0; k < nc; k++)
        {
            int tot = locked | hcBuf[k]; if (tot == 0) continue;
            double v = EvalMask(tot, rolls, face); if (v > best) { best = v; add = hcBuf[k]; }
        }
        for (int pass = 0; pass < 6; pass++)
        {
            bool improved = false;
            for (int i = 0; i < m; i++)
            {
                if ((rolled >> i & 1) == 0) continue;
                int a2 = add ^ (1 << i); if (nl + Pop(a2) > K || (locked | a2) == 0) continue;
                double v = EvalMask(locked | a2, rolls, face); if (v > best) { best = v; add = a2; improved = true; }
            }
            if (!improved) break;
        }
        return best < 0 ? 0 : best;
    }

    double BestComplete(int locked, int rolled, int rolls, int[] face, out int add)
    {
        if (Pop(rolled) > exactLimit) return BestCompleteHeur(locked, rolled, rolls, face, out add);
        double best = -1; add = 0; int nl = Pop(locked);
        for (int a = rolled; ; a = (a - 1) & rolled)
        {
            int tot = locked | a;
            if (tot != 0 && nl + Pop(a) <= K) { double v = EvalMask(tot, rolls, face); if (v > best) { best = v; add = a; } }
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
        if (b.cnt[11] > 0) buf[n++] = Trim(Trim(grp[2], 2) | Trim(grp[3], 2), room);
        int u = 0;
        for (int i = 0; i < n; i++) { bool dup = false; for (int j = 0; j < u; j++) if (buf[j] == buf[i]) { dup = true; break; } if (!dup) buf[u++] = buf[i]; }
        return u;
    }

    int SampleFace(FDie d) { double x = rng.NextDouble(); int f = 0; while (x >= d.cdf[f]) f++; return f; }

    double EVRoll(int locked, int alive, int r, int[] faceIn, int depth, bool c05used, int limit, int samples)
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
                if (d.glass && rng.NextDouble() < P.glassBreak) { aliveNow &= ~bit; continue; }
                f[i] = SampleFace(d); rolled |= bit;
            }
            int r2 = r - 1; bool used = c05used;
            if (r2 == 0 && !used && b.cnt[5] > 0) { int un = Pop(aliveNow) - Pop(locked); if (un >= 1 && un <= 2) { r2 += 3 * b.cnt[5]; used = true; } }
            double best = 0; int addS = 0;
            if ((locked | rolled) != 0) best = U(BestComplete(locked, rolled, r2, f, out addS));
            if (r2 > 0 && depth < limit)
            {
                int[] cand = candBuf[depth]; int nc = Hold(locked, rolled, f, cand, addS);
                for (int k = 0; k < nc; k++)
                {
                    int L2 = locked | cand[k]; if ((aliveNow & ~L2) == 0) continue;
                    double v = EVRoll(L2, aliveNow, r2, f, depth + 1, used, limit, s1); if (v > best) best = v;
                }
            }
            sum += best;
        }
        return sum / samples;
    }

    /// <summary>Returns the dice to lock now; submit=true means lock them and submit.</summary>
    public int Decide(int locked, int alive, int rolled, int r, int[] face, bool c05used, out bool submit)
    {
        int addS = 0; double raw = (locked | rolled) == 0 ? 0 : BestComplete(locked, rolled, r, face, out addS);
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
            rng.s = seed; val[k] = EVRoll(L2, alive, r, face, 1, c05used, 1, depthMax >= 2 && r >= 2 ? sQuick : s0);
        }
        double bestV = U(raw); int bestH = -1;
        if (depthMax >= 2 && r >= 2)
        {
            for (int t = 0; t < keepTop; t++)
            {
                int top = -1; for (int k = 0; k < nc; k++) if (val[k] >= 0 && (top < 0 || val[k] > val[top])) top = k;
                if (top < 0) break;
                rng.s = seed ^ 0x5bd1e995UL; double v = EVRoll(locked | cand[top], alive, r, face, 1, c05used, 2, s0);
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
        int[] face = faceBuf[0]; int locked = 0, alive = (1 << m) - 1, r = b.rollsPerHand; bool c05 = false;
        ulong rollSeed = rollRng.Next(); int rollNo = 0;
        while (true)
        {
            int rolled = 0;
            for (int i = 0; i < m; i++)
            {
                int bit = 1 << i; if ((alive & bit) == 0 || (locked & bit) != 0) continue;
                var one = new Rng(rollSeed + (ulong)(rollNo * 64 + i) * 7919UL);
                var d = dice[i];
                if (d.glass && one.NextDouble() < P.glassBreak) { alive &= ~bit; continue; }
                double x = one.NextDouble(); int f = 0; while (x >= d.cdf[f]) f++;
                face[i] = f; rolled |= bit;
            }
            r--; rollNo++;
            if (r == 0 && !c05 && b.cnt[5] > 0) { int un = Pop(alive) - Pop(locked); if (un >= 1 && un <= 2) { r += 3 * b.cnt[5]; c05 = true; } }
            if ((locked | rolled) == 0) { primary = -1; nSub = 0; lastHits = 0; return 0; }
            int add = Decide(locked, alive, rolled, r, face, c05, out bool submit);
            locked |= add;
            if (submit) { double v = EvalMask(locked, r, face); primary = lastPrimary; nSub = Pop(locked); return v; }
        }
    }
}
