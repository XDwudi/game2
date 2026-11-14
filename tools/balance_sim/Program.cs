// Balance simulator for the dice-building prototype.
//   verify  : compare the fast planning scorer with the real ScoreCalculator on random submissions
//   cards   : marginal strength of every controller in reference builds
//   scen    : hand-built scenarios (accumulation end states, starter odds)
//   run     : complete runs through the real GameSession with a bot
// Usage: dotnet run -c Release --project tools/balance_sim -- <mode> [--config path] [--seeds N] [--first S] [--out file] [--weak] [--threads T]
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DiceCraft.Core;

public static class Program
{
    static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true, WriteIndented = false };
    static GameConfig cfg; static Params P; static ParallelOptions Par;
    static string Arg(string[] a, string name, string def) { int i = Array.IndexOf(a, name); return i >= 0 && i + 1 < a.Length ? a[i + 1] : def; }

    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        string mode = args.Length > 0 ? args[0] : "verify";
        string path = Arg(args, "--config", "DicePrototype/Assets/Game/Data/P0Config.json");
        cfg = JsonSerializer.Deserialize<GameConfig>(GameConfig.PrepareJsonForUnity(File.ReadAllText(path)), new JsonSerializerOptions { IncludeFields = true });
        cfg.Validate(); P = new Params(cfg);
        int threads = int.Parse(Arg(args, "--threads", "0"));
        Par = new ParallelOptions { MaxDegreeOfParallelism = threads > 0 ? threads : Math.Min(8, Math.Max(1, Environment.ProcessorCount - 2)) };
        Console.WriteLine("config=" + path + " version=" + cfg.configVersion + " threads=" + Par.MaxDegreeOfParallelism);
        switch (mode)
        {
            case "verify": return Verify(int.Parse(Arg(args, "--n", "200000")), Arg(args, "--out", null));
            case "cards": return Cards(int.Parse(Arg(args, "--hands", "1500")), Arg(args, "--out", null));
            case "scen": return Scenarios(int.Parse(Arg(args, "--hands", "3000")), Arg(args, "--out", null));
            case "trace": return Trace(uint.Parse(Arg(args, "--seed", "5000")), int.Parse(Arg(args, "--steps", "80")), Arg(args, "--out", "trace.json"));
            case "run": return Runs(uint.Parse(Arg(args, "--first", "1000")), int.Parse(Arg(args, "--seeds", "200")), Arg(args, "--out", null), args.Contains("--weak"), path);
            default: Console.WriteLine("unknown mode"); return 2;
        }
    }

    public static RunState Fresh(uint seed = 1)
    {
        var session = new GameSession(cfg, new MemorySaveStore());
        var r = session.Execute(new SessionCommand { kind = CommandKind.NewRun, commandId = "new", seed = seed });
        if (!r.accepted) throw new Exception(r.error);
        return session.State;
    }
    public static ControllerInstance Ctl(string id, int serial, string ench = "none", string color = null, long growth = 0)
        => new ControllerInstance { instanceId = "X" + serial, definitionId = id, enchantmentId = ench, acquiredSerial = serial, state = new ControllerDynamicState { activeColor = id == "C45" ? (color ?? "blue") : null, growth = growth } };
    static readonly JsonSerializerOptions Pretty = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // ------------------------------------------------------------------ verify
    /// <summary>Random frozen submissions scored by both the real ScoreCalculator.Evaluate and the fast scorer.
    /// A case passes when (1) the fast value equals the exact rational score (scoreNumerator / scoreDenominator)
    /// within 1e-9 relative, (2) its floor equals the real floored score, (3) the primary hand is the same and
    /// (4) the set of triggered abilities is the same. Coverage is printed per controller.</summary>
    static int Verify(int n, string outPath)
    {
        var baseState = Fresh(); var rnd = new Random(12345); int bad = 0; double maxRel = 0;
        var ids = cfg.controllers.Select(c => c.id).Where(id => id != "C41").ToArray(); string[] ench = { "none", "slotless", "base10", "mult2", "submit1", "roll1" };
        string[] textures = { "none", "none", "none", "smooth", "rough" };
        var present = new int[Params.N]; var trig = new int[Params.N];
        var feature = new SortedDictionary<string, int>(StringComparer.Ordinal);
        Action<string, bool> mark = (name, on) => { feature[name] = feature.GetValueOrDefault(name) + (on ? 1 : 0); };
        var h = new Sub(); int[] face = new int[12];
        for (int it = 0; it < n; it++)
        {
            var st = baseState.Clone(); st.controllers.Clear(); st.dice.Clear();
            int k = rnd.Next(0, 10), serial = 1;
            var pick = ids.OrderBy(_ => rnd.Next()).Take(k).ToList();
            if (rnd.Next(4) == 0) pick.Insert(rnd.Next(pick.Count + 1), "C41");   // mimic at a random position (last = no target)
            foreach (string id in pick)
            {
                string e; do e = ench[rnd.Next(ench.Length)]; while (!ControllerRules.IsEnchantmentLegal(id, e, cfg));
                var c = Ctl(id, serial++, e, rnd.Next(2) == 0 ? "red" : "blue"); if (id == "C51") c.state.turnoverUnits2 = 3 + rnd.Next(5);
                if (ControllerRules.HasGrowth(id)) c.state.growth = rnd.Next(4) == 0 ? 0 : rnd.Next(0, 201);
                st.controllers.Add(c);
            }
            int m = rnd.Next(1, 11);
            for (int d = 0; d < m; d++)
            {
                var die = DiceFactory.CreateStandard(cfg, d + 1);
                bool low = rnd.Next(4) == 0, monoPip = rnd.Next(6) == 0, monoColor = rnd.Next(5) == 0; int onePip = rnd.Next(1, 7); string oneColor = rnd.Next(2) == 0 ? "red" : "blue";
                foreach (var f in die.faces)
                {
                    f.permanentPip = monoPip ? onePip : low ? rnd.Next(1, 3) : rnd.Next(1, 7); f.baseColor = monoColor ? oneColor : rnd.Next(2) == 0 ? "red" : "blue";
                    f.isGold = rnd.Next(5) == 0; f.texture = textures[rnd.Next(textures.Length)];
                }
                die.isGlass = rnd.Next(5) == 0; st.dice.Add(die);
            }
            foreach (var hl in st.handLevels) hl.level = rnd.Next(0, 9);
            foreach (var hl in st.colorLevels) hl.level = rnd.Next(0, 9);
            st.gold = rnd.Next(0, 60);
            var b = Build.From(st, P, false); b.redWave = rnd.Next(10) == 0; b.ordinal = rnd.Next(3);
            int prevSel = rnd.Next(-2, 13); b.prev = Math.Max(-1, prevSel);   // -2 = no previous hand, -1 = scatter
            if (rnd.Next(2) == 0) b.capacity = rnd.Next(1, 9);
            int cnt = rnd.Next(1, Math.Min(m, b.capacity) + 1), poolN = rnd.Next(0, Math.Min(5, m - cnt) + 1), rolls = rnd.Next(0, 4);
            b.subsLeft = rnd.Next(1, 5); b.permDice = rnd.Next(0, 9); h.rolls = rolls; h.rollsUsed = rnd.Next(1, 7);
            var order = Enumerable.Range(0, m).OrderBy(_ => rnd.Next()).ToArray();
            // Half of the cases steer the face-up sides towards a hand shape (one pip, pairs, triples, a run) so the
            // hand-type cards trigger often; the dice themselves stay random.
            int shape = rnd.Next(8), start = rnd.Next(1, 7), runStart = rnd.Next(1, 3);
            for (int rank = 0; rank < m; rank++)
            {
                int di = order[rank]; face[di] = rnd.Next(6);
                int want = shape == 4 ? start : shape == 5 ? (start + rank / 2 - 1) % 6 + 1 : shape == 6 ? (start + rank / 3 - 1) % 6 + 1 : shape == 7 ? runStart + rank % 5 : 0;
                if (want == 0) continue;
                var match = Enumerable.Range(0, 6).Where(x => b.dice[di].pip[x] == want).ToArray();
                if (match.Length > 0) face[di] = match[rnd.Next(match.Length)];
            }
            var sub = new SubmissionDie[cnt]; var pool = new PoolDie[poolN]; int poolMask = 0; h.n = cnt;
            for (int i = 0; i < cnt + poolN; i++)
            {
                int di = order[i]; var d = st.dice[di]; var f = d.faces.First(x => x.faceId == "F0" + (face[di] + 1));
                if (i < cnt)
                {
                    sub[i] = new SubmissionDie { dieId = d.dieId, faceId = f.faceId, currentPip = f.permanentPip, currentColor = f.baseColor, permanentSixFaceSum = d.faces.Sum(x => x.permanentPip), isGlass = d.isGlass, isGold = f.isGold, lifetime = "permanent", permanentFacesCopy = d.faces.Select(x => x.Clone()).ToArray() };
                    h.Put(i, b.dice[di], face[di]);
                }
                else { pool[i - cnt] = new PoolDie { dieId = d.dieId, faceId = f.faceId, currentPip = f.permanentPip, currentColor = f.baseColor }; poolMask |= 1 << di; }
            }
            h.SetPool(b.dice, face, poolMask, P.p71pip);
            int units = P.c53units[rnd.Next(P.c53units.Length)]; b.c53 = units;
            var snap = new SubmissionSnapshot
            {
                runId = "v", stageId = "STAGE_01", handId = "H", configVersion = cfg.configVersion, capacity = b.capacity, rollsRemaining = rolls, submissionOrdinalBefore = b.ordinal,
                redWave = b.redWave, wallet = st.gold, previousPrimaryHandId = prevSel == -2 ? null : prevSel == -1 ? "scatter" : P.hId[prevSel], equippedControllers = st.controllers.Select(c => c.Clone()).ToArray(),
                handLevels = st.handLevels.Select(x => x.Clone()).ToArray(), colorLevels = st.colorLevels.Select(x => x.Clone()).ToArray(), mimicBindings = null, dice = sub,
                resolvedRandomChoices = st.controllers.Where(c => c.definitionId == "C53").Select(c => new RandomMultiplierChoice { ownerInstanceId = c.instanceId, units2 = units }).ToArray(),
                rollsUsed = h.rollsUsed, submissionsRemainingBefore = b.subsLeft, permanentDiceCount = b.permDice, poolDice = pool
            };
            var real = ScoreCalculator.Evaluate(snap, cfg);
            double truth = (double)BigInteger.Parse(real.score), exact = (double)BigInteger.Parse(real.scoreNumerator) / (double)BigInteger.Parse(real.scoreDenominator);
            double fast = b.Score(h, out int prim);
            string realPrim = real.primaryHandId == "scatter" ? null : real.primaryHandId; string fastPrim = prim < 0 ? null : P.hId[prim];
            var fastTrig = new HashSet<string>(Enumerable.Range(1, Params.N - 1).Where(a => Build.H(b.lastHits, a)).Select(Params.Cid), StringComparer.Ordinal);
            double rel = Math.Abs(fast - exact) / Math.Max(1.0, exact); if (rel > maxRel) maxRel = rel;
            bool ok = rel <= 1e-9 && Math.Abs(Math.Floor(fast + 1e-6) - truth) <= 1e-9 * truth + 0.5 && realPrim == fastPrim && fastTrig.SetEquals(real.triggeredControllerIds);
            if (!ok && ++bad <= 8)
                Console.WriteLine($"MISMATCH it={it} real={real.score} exact={exact:R} fast={fast:R} prim {realPrim}/{fastPrim} trig real=[{string.Join(",", real.triggeredControllerIds)}] fast=[{string.Join(",", fastTrig.OrderBy(x => x, StringComparer.Ordinal))}] ctl=[{string.Join(",", st.controllers.Select(c => c.definitionId + ":" + c.enchantmentId + (c.state.growth > 0 ? "+g" + c.state.growth : "")))}] pips=[{string.Join(",", sub.Select(d => d.currentPip + d.currentColor.Substring(0, 1)))}] pool=[{string.Join(",", pool.Select(d => d.currentPip + d.currentColor.Substring(0, 1)))}] cap={b.capacity} rolls={rolls} used={h.rollsUsed} subsLeft={b.subsLeft} perm={b.permDice}");
            // Coverage.
            var sources = ControllerRules.ResolveSources(snap.equippedControllers, null);
            foreach (string id in sources.Where(s => s.abilityId != null).Select(s => s.abilityId).Distinct()) present[int.Parse(id.Substring(1))]++;
            foreach (string id in real.triggeredControllerIds) trig[int.Parse(id.Substring(1))]++;
            mark("pool dice > 0", poolN > 0); mark("pool red merged into red group (C73)", real.redPoolCount > 0); mark("retriggers > 0", real.pipRetriggers.Any(x => x > 0));
            mark("hand multiplier add > 0", real.handMultAdd100 > 0); mark("v0.9 controller base add > 0", real.controllerBaseAdds.Length > 0);
            mark("mimic (C41) copying an ability", sources.Any(s => s.emitterDefinitionId == "C41" && s.abilityId != null));
            mark("mimic (C41) copying a growth card", sources.Any(s => s.emitterDefinitionId == "C41" && s.abilityId != null && ControllerRules.HasGrowth(s.abilityId)));
            mark("growth > 0 on a growth card", st.controllers.Any(c => c.state.growth > 0));
            mark("submitted die: textured face up", sub.Any(d => DieFacts.Texture(d) != "none")); mark("submitted die: mono-colour", sub.Any(DieFacts.MonoColor)); mark("submitted die: mono-pip", sub.Any(DieFacts.MonoPip));
            mark("submitted die: gold face up", sub.Any(d => d.isGold)); mark("submitted die: glass", sub.Any(d => d.isGlass));
            mark("last submission of the stage", b.subsLeft == 1); mark("exactly one roll used", h.rollsUsed == 1); mark("no rolls left", rolls == 0);
            mark("enchant base10 or mult2 counted", real.enchantBaseCount + real.enchantMultiplierCount > 0);
        }
        Console.WriteLine($"verify: {n} random submissions, mismatches={bad}, max relative error vs exact rational={maxRel:E2}");
        Console.WriteLine("\ncoverage: cases where each input was exercised");
        foreach (var kv in feature) Console.WriteLine($"  {kv.Key,-42} {kv.Value}");
        Console.WriteLine("\ncoverage per controller: present = cases where the ability was a source (owned or mimicked), triggered = cases where the real calculator listed it as triggered");
        var nonScoring = new HashSet<string>(cfg.enchantRules.nonSubmitControllerIds, StringComparer.Ordinal) { "C41" };
        var gaps = new List<string>(); var rows = new List<object>(); var line = new System.Text.StringBuilder();
        int col = 0;
        foreach (var c in cfg.controllers)
        {
            int a = int.Parse(c.id.Substring(1)); bool scoring = !nonScoring.Contains(c.id);
            line.Append($"{c.id,-5}{present[a],7}/{trig[a],-7}{(scoring ? " " : "*")} ");
            if (++col % 6 == 0) { Console.WriteLine("  " + line); line.Clear(); }
            rows.Add(new { id = c.id, name = c.displayName, scoring, isNew = a >= 54, present = present[a], triggered = trig[a] });
            if (scoring && (present[a] == 0 || trig[a] == 0)) gaps.Add(c.id);
        }
        if (line.Length > 0) Console.WriteLine("  " + line);
        Console.WriteLine("  (* = non-scoring by rule: never a triggered source. C41 is counted under the ability it copies.)");
        var newScoring = cfg.controllers.Where(c => int.Parse(c.id.Substring(1)) >= 54 && !nonScoring.Contains(c.id)).ToArray();
        Console.WriteLine($"\nnew scoring controllers (C54..C103 minus non-scoring): {newScoring.Length}; with present>0 and triggered>0: {newScoring.Count(c => present[int.Parse(c.id.Substring(1))] > 0 && trig[int.Parse(c.id.Substring(1))] > 0)}; min present={newScoring.Min(c => present[int.Parse(c.id.Substring(1))])}, min triggered={newScoring.Min(c => trig[int.Parse(c.id.Substring(1))])}");
        Console.WriteLine(gaps.Count == 0 ? "coverage: OK, every scoring controller was present and triggered at least once" : "coverage: GAPS " + string.Join(",", gaps));
        if (outPath != null) File.WriteAllText(outPath, JsonSerializer.Serialize(new { config = cfg.configVersion, cases = n, mismatches = bad, maxRelativeError = maxRel, features = feature, controllers = rows, coverageGaps = gaps }, Pretty));
        return bad == 0 && gaps.Count == 0 ? 0 : 1;
    }

    // ------------------------------------------------------------------ measurement helpers
    public sealed class Stat { public double mean, median, p10, p90, hit, dice, stage3, stage; public int subs; public int[] prim = new int[13]; }
    /// <summary>Hands cycle through the stage's submission positions (so C68 / C102 pay on the last one);
    /// `stage` = expected stage total = mean hand x submissions per stage.</summary>
    public static Stat Measure(RunState st, int hands, ulong seed, int hitId = 0, double target = 0)
    {
        var b = Build.From(st, P, false);
        var pl = new Planner(); pl.Set(b); pl.rollRng = new Rng(seed); pl.rng = new Rng(seed ^ 0x77UL);
        var v = new double[hands]; var s = new Stat(); double hit = 0, dice = 0;
        for (int h = 0; h < hands; h++)
        {
            b.subsLeft = b.submissions - h % b.submissions;
            v[h] = pl.PlayHand(out int prim, out int nSub); if (prim >= 0) s.prim[prim]++;
            dice += nSub; if (hitId > 0 && nSub > 0 && Build.H(pl.lastHits, hitId)) hit++;
        }
        s.mean = v.Average(); s.hit = hit / hands; s.dice = dice / hands; s.subs = b.submissions; s.stage = s.mean * b.submissions;
        if (target > 0) { int ok = 0, groups = hands / b.submissions; for (int g = 0; g < groups; g++) { double t = 0; for (int k = 0; k < b.submissions; k++) t += v[g * b.submissions + k]; if (t >= target) ok++; } s.stage3 = ok / (double)groups; }
        Array.Sort(v); s.median = v[hands / 2]; s.p10 = v[hands / 10]; s.p90 = v[hands * 9 / 10];
        return s;
    }
    static RunState Mid()
    {
        var st = Fresh(); st.controllers.Add(Ctl("C50", 1)); st.controllers.Add(Ctl("C46", 2)); st.gold = 15;
        foreach (var h in st.handLevels) if (new[] { "pair", "two_pair", "trips", "full_house", "straight", "quads" }.Contains(h.handId)) h.level = 2;
        return st;
    }

    // ------------------------------------------------------------------ cards
    /// <summary>Growth a card would plausibly hold around the middle of a run (stage 12 or so, bought around stage 4).
    /// These are stated assumptions for the `cards` table, not measurements.</summary>
    public static readonly Dictionary<string, (long growth, string why)> MidGrowth = new Dictionary<string, (long, string)>
    {
        ["C54"] = (9, "3 consecutive same-primary submissions (x1.45)"), ["C55"] = (40, "20 submissions (+40 base)"), ["C56"] = (20, "10 submissions without red (x2.0)"),
        ["C57"] = (24, "8 submissions with a red group of 3+ (x2.2)"), ["C58"] = (32, "8 stages cleared (x2.6)"), ["C59"] = (32, "8 unused submissions (+32 base)"),
        ["C60"] = (12, "6 die-face upgrades bought (x1.6)"), ["C61"] = (20, "10 hand/colour levels gained (x2.0)"), ["C62"] = (50, "50 rolls thrown (+50 base)"),
        ["C90"] = (30, "2 glass dice broken (x2.5)"), ["C100"] = (20, "10 gold of controllers sacrificed (x2.0)")
    };
    static int Cards(int hands, string outPath)
    {
        var contexts = new (string name, Func<RunState> make)[] { ("A 裸装起手(5标准骰,无卡,0金)", () => { var s = Fresh(); s.gold = 0; return s; }), ("B 中期通用(稳稳分数+稳稳乘数,常用牌型Lv2,15金)", Mid) };
        var rows = new List<object>();
        Console.WriteLine("equivalent multiplier = expected stage total (mean hand x submissions per stage) with the card / without it.");
        Console.WriteLine("growth cards are listed twice: at growth 0 (as bought) and at the stated mid-run growth:");
        foreach (var kv in MidGrowth) Console.WriteLine($"  {kv.Key} {cfg.Controller(kv.Key).displayName}: growth {kv.Value.growth} = {kv.Value.why}");
        foreach (var (name, make) in contexts)
        {
            var baseStat = Measure(make(), hands, 4242);
            Console.WriteLine($"\n== {name}: E[hand]={baseStat.mean:F1} median={baseStat.median:F1} p10={baseStat.p10:F1} p90={baseStat.p90:F1} avgDice={baseStat.dice:F2} submissions={baseStat.subs}");
            Console.WriteLine("   primary: " + string.Join(" ", Enumerable.Range(0, 13).Where(i => baseStat.prim[i] > 0).Select(i => P.hId[i] + "=" + (100.0 * baseStat.prim[i] / hands).ToString("F0") + "%")));
            var jobs = new List<(ControllerDefinition c, long growth)>();
            foreach (var c in cfg.controllers) { jobs.Add((c, 0)); if (MidGrowth.TryGetValue(c.id, out var g)) jobs.Add((c, g.growth)); }
            var res = new ConcurrentBag<(ControllerDefinition c, long growth, double ratio, double hit, double med)>();
            Parallel.ForEach(jobs, Par, job =>
            {
                var c = job.c; double stage = 0, hit = 0, med = 0; string[] colors = c.id == "C45" ? new[] { "red", "blue" } : new[] { (string)null };
                foreach (string col in colors)
                {
                    var st = make(); st.controllers.Add(Ctl(c.id, 9, "none", col, job.growth));
                    var s = Measure(st, hands, 4242, int.Parse(c.id.Substring(1))); stage += s.stage / colors.Length; hit += s.hit / colors.Length; med += s.median / colors.Length;
                }
                res.Add((c, job.growth, stage / baseStat.stage, hit, med / Math.Max(1e-9, baseStat.median)));
            });
            foreach (var r in res.OrderByDescending(x => x.ratio))
            {
                string tag = MidGrowth.ContainsKey(r.c.id) ? "@g" + r.growth : "";
                Console.WriteLine($"{r.c.id + tag,-9} {r.c.displayName,-6}\t{r.c.rarity,-8}\t${r.c.buyPrice}\tx{r.ratio:F2}\tmedian x{r.med:F2}\thit {r.hit * 100:F0}%\tln/$ {Math.Log(Math.Max(r.ratio, 1e-9)) / r.c.buyPrice:F3}");
                rows.Add(new { context = name, id = r.c.id, name = r.c.displayName, rarity = r.c.rarity, price = r.c.buyPrice, isNew = int.Parse(r.c.id.Substring(1)) >= 54, growth = r.growth, ratio = r.ratio, medianRatio = r.med, hit = r.hit, lnPerGold = Math.Log(Math.Max(r.ratio, 1e-9)) / r.c.buyPrice });
            }
        }
        if (outPath != null) File.WriteAllText(outPath, JsonSerializer.Serialize(new { config = cfg.configVersion, hands, midGrowth = MidGrowth.ToDictionary(k => k.Key, k => new { growth = k.Value.growth, assumption = k.Value.why }), rows }, Pretty));
        return 0;
    }

    // ------------------------------------------------------------------ scenarios
    static void AllFaces(RunState st, Action<FaceState> edit) { foreach (var d in st.dice) foreach (var f in d.faces) edit(f); }
    static int Scenarios(int hands, string outPath)
    {
        var list = new List<(string name, Func<RunState> make)>();
        Func<string[], Action<RunState>, Func<RunState>> mk = (ids, edit) => () => { var s = Fresh(); int n = 1; foreach (string id in ids) s.controllers.Add(Ctl(id, n++)); edit?.Invoke(s); return s; };
        list.Add(("起手裸装", mk(new string[0], null)));
        list.Add(("全1骰面", mk(new string[0], s => AllFaces(s, f => f.permanentPip = 1))));
        list.Add(("全1骰面+少就是多C10", mk(new[] { "C10" }, s => AllFaces(s, f => f.permanentPip = 1))));
        list.Add(("全1骰面+C10+天地大同C32", mk(new[] { "C10", "C32" }, s => AllFaces(s, f => f.permanentPip = 1))));
        list.Add(("全1骰面+C10+C32+三条开泰C30+四平八稳C31", mk(new[] { "C10", "C32", "C30", "C31" }, s => AllFaces(s, f => f.permanentPip = 1))));
        list.Add(("半成型:每骰1/2/3点面已减为1(15次减点)", mk(new string[0], s => AllFaces(s, f => { if (f.permanentPip <= 3) f.permanentPip = 1; }))));
        list.Add(("半成型:同上+少就是多C10", mk(new[] { "C10" }, s => AllFaces(s, f => { if (f.permanentPip <= 3) f.permanentPip = 1; }))));
        list.Add(("全1骰面+C10+小头大用C19", mk(new[] { "C10", "C19" }, s => AllFaces(s, f => f.permanentPip = 1))));
        list.Add(("半成型:每骰4/5/6点面已加为6(15次加点)", mk(new string[0], s => AllFaces(s, f => { if (f.permanentPip >= 4) f.permanentPip = 6; }))));
        list.Add(("全6骰面+66大顺C20", mk(new[] { "C20" }, s => AllFaces(s, f => f.permanentPip = 6))));
        list.Add(("全6骰面", mk(new string[0], s => AllFaces(s, f => f.permanentPip = 6))));
        list.Add(("全6骰面+C32+C30+C31", mk(new[] { "C32", "C30", "C31" }, s => AllFaces(s, f => f.permanentPip = 6))));
        list.Add(("全蓝骰面", mk(new string[0], s => AllFaces(s, f => f.baseColor = "blue"))));
        list.Add(("全蓝骰面+蓝色疯狂C02", mk(new[] { "C02" }, s => AllFaces(s, f => f.baseColor = "blue"))));
        list.Add(("全蓝骰面+C02+蓝色价值C39", mk(new[] { "C02", "C39" }, s => AllFaces(s, f => f.baseColor = "blue"))));
        list.Add(("全红骰面", mk(new string[0], s => AllFaces(s, f => f.baseColor = "red"))));
        list.Add(("全红骰面+红运当头C25", mk(new[] { "C25" }, s => AllFaces(s, f => f.baseColor = "red"))));
        list.Add(("全红+C25+8骰K=8(C21+C22)", mk(new[] { "C25", "C21", "C22" }, s => { for (int i = 6; i <= 8; i++) s.dice.Add(DiceFactory.CreateStandard(cfg, i)); AllFaces(s, f => f.baseColor = "red"); })));
        list.Add(("起手+蓝色扩散C38(未染色)", mk(new[] { "C38" }, null)));
        list.Add(("乘区四件:C33+C42+C09+C46", mk(new[] { "C33", "C42", "C09", "C46" }, null)));
        list.Add(("乘区四件:C30+C31+C32+C46", mk(new[] { "C30", "C31", "C32", "C46" }, null)));
        list.Add(("基础四件:C50+C27+C48+C39", mk(new[] { "C50", "C27", "C48", "C39" }, null)));
        list.Add(("有钱真好C44@50金", mk(new[] { "C44" }, s => s.gold = 50)));
        list.Add(("控制欲C47+3件无关卡", mk(new[] { "C47", "C01", "C52", "C21" }, null)));
        list.Add(("全牌型Lv8(无卡)", mk(new string[0], s => { foreach (var h in s.handLevels) h.level = 8; foreach (var h in s.colorLevels) h.level = 8; })));
        list.Add(("5骰全黄金面", mk(new string[0], s => AllFaces(s, f => f.isGold = true))));
        list.Add(("5骰全玻璃", mk(new string[0], s => { foreach (var d in s.dice) d.isGlass = true; })));
        list.Add(("8颗标准骰K=5", mk(new string[0], s => { for (int i = 6; i <= 8; i++) s.dice.Add(DiceFactory.CreateStandard(cfg, i)); })));
        var rows = new ConcurrentDictionary<int, string>(); var data = new ConcurrentDictionary<int, object>();
        Parallel.For(0, list.Count, Par, i =>
        {
            var s = Measure(list[i].make(), hands, 99, 0, 0);
            rows[i] = $"{list[i].name,-40}\tE[hand]={s.mean,12:F0}\tmedian={s.median,12:F0}\tp10={s.p10,10:F0}\tp90={s.p90,12:F0}\tdice={s.dice:F1}\t" + string.Join(" ", Enumerable.Range(0, 13).Where(k => s.prim[k] * 20 >= hands).Select(k => P.hId[k] + "=" + (100.0 * s.prim[k] / hands).ToString("F0") + "%"));
            data[i] = new { name = list[i].name, mean = s.mean, median = s.median, p10 = s.p10, p90 = s.p90, dice = s.dice };
        });
        for (int i = 0; i < list.Count; i++) Console.WriteLine(rows[i]);
        // Starter odds against the first targets.
        Console.WriteLine("\n-- 裸装三手合计达标概率 --");
        foreach (var stage in cfg.stages.Take(4)) { var s = Measure(Fresh(), hands * 3, 7, 0, double.Parse(stage.normalTarget)); Console.WriteLine($"{stage.stageId} target={stage.normalTarget}: P(clear with no controllers)={s.stage3 * 100:F1}%"); }
        if (outPath != null) File.WriteAllText(outPath, JsonSerializer.Serialize(Enumerable.Range(0, list.Count).Select(i => data[i]), new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        return 0;
    }

    // ------------------------------------------------------------------ trace
    /// <summary>Records the bot's first commands of one run so the packaged game can replay them (capture tour).
    /// The tour cannot send controller reordering, so a seed whose opening needs it is refused.</summary>
    static int Trace(uint seed, int steps, string outPath)
    {
        var runner = new Runner(cfg, P, new BotOptions()) { recorded = new List<Dictionary<string, object>>() };
        var log = runner.Play(seed);
        var all = runner.recorded; var head = all.Take(steps + 1).ToList();
        if (head.Count <= steps) { Console.WriteLine($"seed {seed}: run ended after {all.Count - 1} commands, fewer than {steps}"); return 3; }
        if (head.Any(s => (string)s["kind"] == "ReorderControllers")) { Console.WriteLine($"seed {seed}: opening uses ReorderControllers, pick another seed"); return 4; }
        File.WriteAllText(outPath, JsonSerializer.Serialize(new Dictionary<string, object> { ["seed"] = seed, ["configVersion"] = cfg.configVersion, ["policy"] = "balance_sim bot, legal commands only", ["steps"] = head },
            new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Console.WriteLine($"seed {seed}: wrote {head.Count} steps ({string.Join(",", head.GroupBy(s => (string)s["kind"]).Select(g => g.Key + "=" + g.Count()))}); run reached stage {log.reached}, won={log.won}");
        return 0;
    }

    // ------------------------------------------------------------------ runs
    static double Median(IEnumerable<double> xs) { var a = xs.OrderBy(x => x).ToArray(); return a.Length == 0 ? double.NaN : a[a.Length / 2]; }
    static int Runs(uint first, int seeds, string outPath, bool weak, string configPath)
    {
        var logs = new RunLog[seeds]; int done = 0; var sw = System.Diagnostics.Stopwatch.StartNew();
        Parallel.For(0, seeds, Par, i =>
        {
            try { logs[i] = new Runner(cfg, P, new BotOptions { weak = weak }).Play(first + (uint)i); }
            catch (Exception e) { Console.WriteLine("seed " + (first + (uint)i) + " ERROR " + e.Message); logs[i] = new RunLog { seed = first + (uint)i, fail = "ERROR:" + e.Message }; }
            int d = Interlocked.Increment(ref done); if (d % 20 == 0) Console.WriteLine($"  {d}/{seeds} runs, {sw.Elapsed.TotalSeconds:F0}s");
        });
        var ok = logs.Where(l => l.fail == null || !l.fail.StartsWith("ERROR")).ToArray();
        double winRate = 100.0 * ok.Count(l => l.won) / Math.Max(1, ok.Length);
        Console.WriteLine($"\nruns={ok.Length} errors={seeds - ok.Length} wins={ok.Count(l => l.won)} ({winRate:F1}%) bot={(weak ? "weak" : "planner")} seeds={first}..{first + (uint)seeds - 1} time={sw.Elapsed.TotalSeconds:F0}s");
        int predicted = ok.Sum(l => l.predicted), mispredicted = ok.Sum(l => l.mispredicted);
        Console.WriteLine($"planner self-check: {predicted} real submissions predicted (builds without C53), {mispredicted} scored differently by the session" + (mispredicted > 0 ? "; first: " + ok.First(l => l.mispredicted > 0).firstMisprediction : ""));
        Console.WriteLine("slowest runs: " + string.Join(" ", ok.OrderByDescending(l => l.seconds).Take(5).Select(l => $"{l.seed}:{l.seconds:F0}s")) + $"  sum of run times={ok.Sum(l => l.seconds):F0}s");
        Console.WriteLine("stage\ttarget\treach%\tclear%\tsubs\tscore/target(med,cleared)\tpower/target(med)\tgoldStart\tincome(reward+unused+interest+inStage+C95)\tspent\tgoldLeave\tctrl\tslots\tdice\trisk%");
        var stageRows = new List<object>(); var clearByStage = new double[cfg.stages.Length + 1]; var powerByStage = new double[cfg.stages.Length + 1];
        for (int k = 1; k <= cfg.stages.Length; k++)
        {
            var at = ok.Select(l => l.stages.FirstOrDefault(s => s.stage == k)).Where(s => s != null).ToArray(); if (at.Length == 0) break;
            var cl = at.Where(s => s.cleared).ToArray();
            double reach = 100.0 * at.Length / ok.Length, clear = 100.0 * cl.Length / at.Length;
            var row = new
            {
                stage = k, target = cfg.stages[k - 1].normalTarget, reach, clear, subs = cl.Length == 0 ? 0 : cl.Average(s => s.subsUsed),
                overkill = Median(cl.Select(s => s.score / s.target)), power = Median(at.Select(s => s.power / s.target)), safePower = Median(at.Select(s => s.safe / s.target)),
                goldStart = at.Average(s => s.goldStart), reward = cl.Length == 0 ? 0 : cl.Average(s => s.reward), unused = cl.Length == 0 ? 0 : cl.Average(s => s.unused),
                interest = cl.Length == 0 ? 0 : cl.Average(s => s.interest), pig = cl.Length == 0 ? 0 : cl.Average(s => s.pig), ctlIncome = cl.Length == 0 ? 0 : cl.Average(s => s.ctlIncome), spent = cl.Length == 0 ? 0 : cl.Average(s => s.spent),
                goldLeave = cl.Length == 0 ? 0 : cl.Average(s => s.goldAfterShop), ctrl = at.Average(s => s.ctrl), slots = at.Average(s => s.slots), dice = at.Average(s => s.dice), risk = 100.0 * at.Count(s => s.risk) / at.Length
            };
            stageRows.Add(row); clearByStage[k] = clear; powerByStage[k] = row.power;
            Console.WriteLine($"{k}\t{row.target}\t{reach:F0}\t{clear:F0}\t{row.subs:F2}\t{row.overkill:F2}\t{row.power:F2}\t{row.goldStart:F1}\t{row.reward:F1}+{row.unused:F1}+{row.interest:F1}+{row.pig:F1}+{row.ctlIncome:F1}\t{row.spent:F1}\t{row.goldLeave:F1}\t{row.ctrl:F1}\t{row.slots:F1}\t{row.dice:F1}\t{row.risk:F0}");
        }
        // What the bot bought, and what winning builds contained.
        var bought = new Dictionary<string, List<int>>(); var endWin = new Dictionary<string, int>(); var endAll = new Dictionary<string, int>();
        foreach (var l in ok)
        {
            foreach (var s in l.stages) foreach (string b in s.bought) { string id = b.Split(':', '(')[0]; if (!bought.TryGetValue(id, out var li)) bought[id] = li = new List<int>(); li.Add(s.stage); }
            var last = l.stages.LastOrDefault(); if (last == null) continue;
            foreach (string c in last.controllers.Select(x => x.Split(':')[0]).Distinct()) { endAll[c] = endAll.GetValueOrDefault(c) + 1; if (l.won) endWin[c] = endWin.GetValueOrDefault(c) + 1; }
        }
        int wins = Math.Max(1, ok.Count(l => l.won));
        Func<string, string> nameOf = id => cfg.controllers.FirstOrDefault(c => c.id == id)?.displayName ?? "";
        Console.WriteLine("\nitem\tboughtPerRun\tavgStage\tinFinalBuild%\tinWinningBuild%");
        var itemRows = new List<object>();
        foreach (var kv in bought.OrderByDescending(x => x.Value.Count))
        {
            string name = nameOf(kv.Key);
            double fin = 100.0 * endAll.GetValueOrDefault(kv.Key) / ok.Length, win = 100.0 * endWin.GetValueOrDefault(kv.Key) / wins;
            Console.WriteLine($"{kv.Key} {name}\t{kv.Value.Count / (double)ok.Length:F2}\t{kv.Value.Average():F1}\t{fin:F0}\t{win:F0}");
            itemRows.Add(new { id = kv.Key, name, perRun = kv.Value.Count / (double)ok.Length, avgStage = kv.Value.Average(), finalPct = fin, winPct = win });
        }
        var neverBought = cfg.controllers.Select(c => c.id).Where(id => !bought.ContainsKey(id)).ToArray();
        Console.WriteLine("\ncontrollers never bought: " + string.Join(" ", neverBought));
        Console.WriteLine("\nmost present in winning builds (share of winning runs holding it at the last stage):");
        var winRows = endWin.OrderByDescending(x => x.Value).Take(20).Select(x => new { id = x.Key, name = nameOf(x.Key), winPct = 100.0 * x.Value / wins }).ToArray();
        Console.WriteLine("  " + string.Join("  ", winRows.Select(r => $"{r.id} {r.name} {r.winPct:F0}%")));
        var deathList = ok.Where(l => !l.won).GroupBy(l => l.reached).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count());
        Console.WriteLine("\ndeaths by stage: " + string.Join(" ", deathList.Select(g => g.Key + ":" + g.Value)));
        int deathsTotal = Math.Max(1, deathList.Values.Sum()); int blocks = (cfg.stages.Length + 3) / 4;
        var blockShare = Enumerable.Range(0, blocks).Select(bk => 100.0 * deathList.Where(g => (g.Key - 1) / 4 == bk).Sum(g => g.Value) / deathsTotal).ToArray();
        Console.WriteLine("death share by four-stage block (% of all deaths): " + string.Join("  ", blockShare.Select((x, bk) => $"{bk * 4 + 1}-{bk * 4 + 4}: {x:F0}%")));
        Console.WriteLine("death share by four-stage block (% of all runs):   " + string.Join("  ", blockShare.Select((x, bk) => $"{bk * 4 + 1}-{bk * 4 + 4}: {x * deathsTotal / ok.Length:F0}%")));
        int lastStage = cfg.stages.Length;
        Console.WriteLine($"median power/target at stage {lastStage}: {powerByStage[lastStage]:F2}   clear% stages 13-24: " + string.Join(" ", Enumerable.Range(13, Math.Max(0, lastStage - 12)).Select(k => clearByStage[k].ToString("F0"))));
        Console.WriteLine("fail reasons: " + string.Join(" ", ok.Where(l => !l.won).GroupBy(l => l.fail ?? "?").Select(g => g.Key + ":" + g.Count())));
        if (outPath != null)
        {
            var summary = new { winRatePct = winRate, deathsByStage = deathList.ToDictionary(g => g.Key.ToString(), g => g.Value), deathShareByBlockPct = blockShare, lastStagePowerOverTargetMedian = powerByStage[lastStage], clearPctByStage = clearByStage.Skip(1).ToArray(), targets = cfg.stages.Select(s => s.normalTarget).ToArray() };
            File.WriteAllText(outPath, JsonSerializer.Serialize(new { config = cfg.configVersion, configPath, bot = weak ? "weak" : "planner", firstSeed = first, seeds, runs = ok.Length, errors = seeds - ok.Length, wins = ok.Count(l => l.won), plannerSelfCheck = new { predicted, mispredicted }, summary, stages = stageRows, items = itemRows, winningBuilds = winRows, neverBought, logs = ok }, Pretty));
        }
        return seeds - ok.Length == 0 ? 0 : 1;
    }
}
