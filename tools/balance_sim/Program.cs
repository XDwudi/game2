// Balance simulator for the dice-building prototype.
//   verify  : compare the fast planning scorer with the real ScoreCalculator on random submissions
//   cards   : marginal strength of every controller in reference builds
//   scen    : hand-built scenarios (accumulation end states, starter odds)
//   run     : complete runs through the real GameSession with a bot
// Usage: dotnet run -c Release --project tools/balance_sim -- <mode> [--config path] [--seeds N] [--first S] [--out file] [--weak]
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
    static GameConfig cfg; static Params P;
    static string Arg(string[] a, string name, string def) { int i = Array.IndexOf(a, name); return i >= 0 && i + 1 < a.Length ? a[i + 1] : def; }

    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        string mode = args.Length > 0 ? args[0] : "verify";
        string path = Arg(args, "--config", "DicePrototype/Assets/Game/Data/P0Config.json");
        cfg = JsonSerializer.Deserialize<GameConfig>(GameConfig.PrepareJsonForUnity(File.ReadAllText(path)), new JsonSerializerOptions { IncludeFields = true });
        cfg.Validate(); P = new Params(cfg);
        Console.WriteLine("config=" + path + " version=" + cfg.configVersion);
        switch (mode)
        {
            case "verify": return Verify(int.Parse(Arg(args, "--n", "200000")));
            case "cards": return Cards(int.Parse(Arg(args, "--hands", "1500")), Arg(args, "--out", null));
            case "scen": return Scenarios(int.Parse(Arg(args, "--hands", "3000")), Arg(args, "--out", null));
            case "run": return Runs(uint.Parse(Arg(args, "--first", "1000")), int.Parse(Arg(args, "--seeds", "200")), Arg(args, "--out", null), args.Contains("--weak"), int.Parse(Arg(args, "--threads", "0")));
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
    public static ControllerInstance Ctl(string id, int serial, string ench = "none", string color = null)
        => new ControllerInstance { instanceId = "X" + serial, definitionId = id, enchantmentId = ench, acquiredSerial = serial, state = new ControllerDynamicState { activeColor = id == "C45" ? (color ?? "blue") : null } };

    // ------------------------------------------------------------------ verify
    static int Verify(int n)
    {
        var baseState = Fresh(); var rnd = new Random(12345); int bad = 0;
        var ids = cfg.controllers.Select(c => c.id).ToArray(); string[] ench = { "none", "slotless", "base10", "mult2", "submit1", "roll1" };
        int[] sp = new int[12], ssix = new int[12]; bool[] sr = new bool[12], sg = new bool[12], sgl = new bool[12];
        for (int it = 0; it < n; it++)
        {
            var st = baseState.Clone(); st.controllers.Clear(); st.dice.Clear();
            int k = rnd.Next(0, 8); int serial = 1;
            foreach (string id in ids.OrderBy(_ => rnd.Next()).Take(k))
            {
                string e; do e = ench[rnd.Next(ench.Length)]; while (!ControllerRules.IsEnchantmentLegal(id, e, cfg));
                var c = Ctl(id, serial++, e, rnd.Next(2) == 0 ? "red" : "blue"); if (id == "C51") c.state.turnoverUnits2 = 3 + rnd.Next(5); st.controllers.Add(c);
            }
            int m = rnd.Next(1, 9);
            for (int d = 0; d < m; d++)
            {
                var die = DiceFactory.CreateStandard(cfg, d + 1);
                bool low = rnd.Next(4) == 0;
                foreach (var f in die.faces) { f.permanentPip = low ? rnd.Next(1, 3) : rnd.Next(1, 7); f.baseColor = rnd.Next(2) == 0 ? "red" : "blue"; f.isGold = rnd.Next(5) == 0; }
                die.isGlass = rnd.Next(5) == 0; st.dice.Add(die);
            }
            foreach (var h in st.handLevels) h.level = rnd.Next(0, 9);
            foreach (var h in st.colorLevels) h.level = rnd.Next(0, 9);
            st.gold = rnd.Next(0, 60);
            var b = Build.From(st, P, false); b.redWave = rnd.Next(10) == 0; b.ordinal = rnd.Next(3); b.prev = rnd.Next(-1, 13);
            int cnt = rnd.Next(1, Math.Min(m, b.capacity) + 1), rolls = rnd.Next(0, 4);
            var chosen = st.dice.OrderBy(_ => rnd.Next()).Take(cnt).ToArray();
            var sub = new SubmissionDie[cnt];
            for (int i = 0; i < cnt; i++)
            {
                var d = chosen[i]; var f = d.faces[rnd.Next(6)];
                sub[i] = new SubmissionDie { dieId = d.dieId, faceId = f.faceId, currentPip = f.permanentPip, currentColor = f.baseColor, permanentSixFaceSum = d.faces.Sum(x => x.permanentPip), isGlass = d.isGlass, isGold = f.isGold, lifetime = "permanent", permanentFacesCopy = d.faces.Select(x => x.Clone()).ToArray() };
                sp[i] = f.permanentPip; sr[i] = f.baseColor == "red"; sg[i] = f.isGold; sgl[i] = d.isGlass; ssix[i] = sub[i].permanentSixFaceSum;
            }
            int units = P.c53units[rnd.Next(P.c53units.Length)]; b.c53 = units;
            var snap = new SubmissionSnapshot
            {
                runId = "v", stageId = "STAGE_01", handId = "H", configVersion = cfg.configVersion, capacity = b.capacity, rollsRemaining = rolls, submissionOrdinalBefore = b.ordinal,
                redWave = b.redWave, wallet = st.gold, previousPrimaryHandId = b.prev < 0 ? null : P.hId[b.prev], equippedControllers = st.controllers.Select(c => c.Clone()).ToArray(),
                handLevels = st.handLevels.Select(h => h.Clone()).ToArray(), colorLevels = st.colorLevels.Select(h => h.Clone()).ToArray(), mimicBindings = null, dice = sub,
                resolvedRandomChoices = st.controllers.Where(c => c.definitionId == "C53").Select(c => new RandomMultiplierChoice { ownerInstanceId = c.instanceId, units2 = units }).ToArray()
            };
            var real = ScoreCalculator.Evaluate(snap, cfg); double truth = (double)BigInteger.Parse(real.score);
            double fast = b.Score(cnt, sp, sr, sg, sgl, ssix, rolls, out int prim);
            string realPrim = real.primaryHandId == "scatter" ? null : real.primaryHandId; string fastPrim = prim < 0 ? null : P.hId[prim];
            if (Math.Abs(Math.Floor(fast + 1e-6) - truth) > 1e-9 * truth + 0.5 || realPrim != fastPrim)
            {
                if (++bad <= 5) Console.WriteLine($"MISMATCH it={it} real={real.score} fast={fast} prim {realPrim}/{fastPrim} ctl=[{string.Join(",", st.controllers.Select(c => c.definitionId + ":" + c.enchantmentId))}] pips=[{string.Join(",", sub.Select(d => d.currentPip + d.currentColor.Substring(0, 1)))}] cap={b.capacity}");
            }
        }
        Console.WriteLine($"verify: {n} random submissions, mismatches={bad}");
        return bad == 0 ? 0 : 1;
    }

    // ------------------------------------------------------------------ measurement helpers
    public sealed class Stat { public double mean, median, p10, p90, hit, dice, stage3; public int[] prim = new int[13]; }
    public static Stat Measure(RunState st, int hands, ulong seed, int hitId = 0, double target = 0)
    {
        var b = Build.From(st, P, false);
        var pl = new Planner(); pl.Set(b); pl.rollRng = new Rng(seed); pl.rng = new Rng(seed ^ 0x77UL);
        var v = new double[hands]; var s = new Stat(); double hit = 0, dice = 0;
        for (int h = 0; h < hands; h++)
        {
            v[h] = pl.PlayHand(out int prim, out int nSub); if (prim >= 0) s.prim[prim]++;
            dice += nSub; if (hitId > 0 && (pl.lastHits >> hitId & 1) != 0) hit++;
        }
        s.mean = v.Average(); s.hit = hit / hands; s.dice = dice / hands;
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
    static int Cards(int hands, string outPath)
    {
        var contexts = new (string name, Func<RunState> make)[] { ("A 裸装起手(5标准骰,无卡,0金)", () => { var s = Fresh(); s.gold = 0; return s; }), ("B 中期通用(稳稳分数+稳稳乘数,常用牌型Lv2,15金)", Mid) };
        var rows = new List<object>();
        foreach (var (name, make) in contexts)
        {
            var baseStat = Measure(make(), hands, 4242);
            Console.WriteLine($"\n== {name}: E[hand]={baseStat.mean:F1} median={baseStat.median:F1} p10={baseStat.p10:F1} p90={baseStat.p90:F1} avgDice={baseStat.dice:F2}");
            Console.WriteLine("   primary: " + string.Join(" ", Enumerable.Range(0, 13).Where(i => baseStat.prim[i] > 0).Select(i => P.hId[i] + "=" + (100.0 * baseStat.prim[i] / hands).ToString("F0") + "%")));
            var res = new ConcurrentBag<(ControllerDefinition c, double ratio, double hit, double med)>();
            Parallel.ForEach(cfg.controllers, c =>
            {
                double mean = 0, hit = 0, med = 0; string[] colors = c.id == "C45" ? new[] { "red", "blue" } : new[] { (string)null };
                foreach (string col in colors)
                {
                    var st = make(); st.controllers.Add(Ctl(c.id, 9, "none", col));
                    var s = Measure(st, hands, 4242, int.Parse(c.id.Substring(1))); mean += s.mean / colors.Length; hit += s.hit / colors.Length; med += s.median / colors.Length;
                }
                res.Add((c, mean / baseStat.mean, hit, med / Math.Max(1e-9, baseStat.median)));
            });
            foreach (var r in res.OrderByDescending(x => x.ratio))
            {
                Console.WriteLine($"{r.c.id} {r.c.displayName,-6}\t{r.c.rarity,-8}\t${r.c.buyPrice}\tx{r.ratio:F2}\tmedian x{r.med:F2}\thit {r.hit * 100:F0}%\tln/$ {Math.Log(Math.Max(r.ratio, 1e-9)) / r.c.buyPrice:F3}");
                rows.Add(new { context = name, id = r.c.id, name = r.c.displayName, rarity = r.c.rarity, price = r.c.buyPrice, ratio = r.ratio, medianRatio = r.med, hit = r.hit });
            }
        }
        if (outPath != null) File.WriteAllText(outPath, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
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
        Parallel.For(0, list.Count, i =>
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

    // ------------------------------------------------------------------ runs
    static double Median(IEnumerable<double> xs) { var a = xs.OrderBy(x => x).ToArray(); return a.Length == 0 ? double.NaN : a[a.Length / 2]; }
    static int Runs(uint first, int seeds, string outPath, bool weak, int threads)
    {
        var logs = new RunLog[seeds]; int done = 0; var sw = System.Diagnostics.Stopwatch.StartNew();
        var po = new ParallelOptions { MaxDegreeOfParallelism = threads > 0 ? threads : Math.Max(1, Environment.ProcessorCount - 2) };
        Parallel.For(0, seeds, po, i =>
        {
            try { logs[i] = new Runner(cfg, P, new BotOptions { weak = weak }).Play(first + (uint)i); }
            catch (Exception e) { Console.WriteLine("seed " + (first + (uint)i) + " ERROR " + e.Message); logs[i] = new RunLog { seed = first + (uint)i, fail = "ERROR:" + e.Message }; }
            int d = Interlocked.Increment(ref done); if (d % 20 == 0) Console.WriteLine($"  {d}/{seeds} runs, {sw.Elapsed.TotalSeconds:F0}s");
        });
        var ok = logs.Where(l => l.fail == null || !l.fail.StartsWith("ERROR")).ToArray();
        Console.WriteLine($"\nruns={ok.Length} errors={seeds - ok.Length} wins={ok.Count(l => l.won)} ({100.0 * ok.Count(l => l.won) / ok.Length:F1}%) bot={(weak ? "weak" : "planner")} time={sw.Elapsed.TotalSeconds:F0}s");
        Console.WriteLine("stage\ttarget\treach%\tclear%\tsubs\tscore/target(med,cleared)\tpower/target(med)\tgoldStart\tincome(reward+unused+interest+pig)\tspent\tgoldLeave\tctrl\tdice\trisk%");
        var stageRows = new List<object>();
        for (int k = 1; k <= cfg.stages.Length; k++)
        {
            var at = ok.Select(l => l.stages.FirstOrDefault(s => s.stage == k)).Where(s => s != null).ToArray(); if (at.Length == 0) break;
            var cl = at.Where(s => s.cleared).ToArray();
            double reach = 100.0 * at.Length / ok.Length, clear = 100.0 * cl.Length / at.Length;
            var row = new
            {
                stage = k, target = cfg.stages[k - 1].normalTarget, reach, clear, subs = cl.Length == 0 ? 0 : cl.Average(s => s.subsUsed),
                overkill = Median(cl.Select(s => s.score / s.target)), power = Median(at.Select(s => s.power / s.target)),
                goldStart = at.Average(s => s.goldStart), reward = cl.Length == 0 ? 0 : cl.Average(s => s.reward), unused = cl.Length == 0 ? 0 : cl.Average(s => s.unused),
                interest = cl.Length == 0 ? 0 : cl.Average(s => s.interest), pig = cl.Length == 0 ? 0 : cl.Average(s => s.pig), spent = cl.Length == 0 ? 0 : cl.Average(s => s.spent),
                goldLeave = cl.Length == 0 ? 0 : cl.Average(s => s.goldAfterShop), ctrl = at.Average(s => s.ctrl), dice = at.Average(s => s.dice), risk = 100.0 * at.Count(s => s.risk) / at.Length
            };
            stageRows.Add(row);
            Console.WriteLine($"{k}\t{row.target}\t{reach:F0}\t{clear:F0}\t{row.subs:F2}\t{row.overkill:F2}\t{row.power:F2}\t{row.goldStart:F1}\t{row.reward:F1}+{row.unused:F1}+{row.interest:F1}+{row.pig:F1}\t{row.spent:F1}\t{row.goldLeave:F1}\t{row.ctrl:F1}\t{row.dice:F1}\t{row.risk:F0}");
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
        Console.WriteLine("\nitem\tboughtPerRun\tavgStage\tinFinalBuild%\tinWinningBuild%");
        var itemRows = new List<object>();
        foreach (var kv in bought.OrderByDescending(x => x.Value.Count))
        {
            string name = kv.Key.StartsWith("C") && kv.Key.Length == 3 ? cfg.Controller(kv.Key).displayName : "";
            double fin = 100.0 * endAll.GetValueOrDefault(kv.Key) / ok.Length, win = 100.0 * endWin.GetValueOrDefault(kv.Key) / wins;
            Console.WriteLine($"{kv.Key} {name}\t{kv.Value.Count / (double)ok.Length:F2}\t{kv.Value.Average():F1}\t{fin:F0}\t{win:F0}");
            itemRows.Add(new { id = kv.Key, name, perRun = kv.Value.Count / (double)ok.Length, avgStage = kv.Value.Average(), finalPct = fin, winPct = win });
        }
        var deaths = ok.Where(l => !l.won).GroupBy(l => l.reached).OrderBy(g => g.Key).Select(g => g.Key + ":" + g.Count());
        Console.WriteLine("\ndeaths by stage: " + string.Join(" ", deaths));
        if (outPath != null)
        {
            var opts = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            File.WriteAllText(outPath, JsonSerializer.Serialize(new { config = cfg.configVersion, bot = weak ? "weak" : "planner", runs = ok.Length, wins = ok.Count(l => l.won), stages = stageRows, items = itemRows, logs = ok }, opts));
        }
        return 0;
    }
}
