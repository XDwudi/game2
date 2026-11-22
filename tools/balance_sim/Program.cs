// Balance simulator for the dice-building prototype (rules p1.1.0).
//   verify  : compare the fast planning scorer with the real ScoreCalculator on random submissions
//   cards   : marginal strength of every controller in reference builds
//   scen    : hand-built scenarios (accumulation end states, starter odds)
//   run     : complete runs through the real GameSession with a bot
//   trace   : record the bot's first commands of one run
// Usage: dotnet tools/balance_sim/bin/Release/net10.0/BalanceSim.dll <mode> [--config path] [--seeds N] [--first S] [--out file] [--log file] [--weak] [--threads T]
// --log writes everything that is printed to a UTF-8 file as well (redirecting stdout in PowerShell garbles the Chinese names).
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DiceCraft.Core;

public static class Program
{
    static GameConfig cfg; static Params P; static ParallelOptions Par;
    static string Arg(string[] a, string name, string def) { int i = Array.IndexOf(a, name); return i >= 0 && i + 1 < a.Length ? a[i + 1] : def; }

    sealed class Tee : TextWriter
    {
        readonly TextWriter a, b; public Tee(TextWriter a, TextWriter b) { this.a = a; this.b = b; }
        public override Encoding Encoding => Encoding.UTF8;
        public override void Write(char value) { lock (this) { a.Write(value); b.Write(value); } }
        public override void Write(string value) { lock (this) { a.Write(value); b.Write(value); } }
        public override void WriteLine(string value) { lock (this) { a.WriteLine(value); b.WriteLine(value); b.Flush(); } }
        public override void Flush() { a.Flush(); b.Flush(); }
    }

    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture; CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try { Console.OutputEncoding = new UTF8Encoding(false); } catch { }
        string logPath = Arg(args, "--log", null); StreamWriter logFile = null;
        if (logPath != null) { logFile = new StreamWriter(logPath, false, new UTF8Encoding(false)); Console.SetOut(new Tee(Console.Out, logFile)); }
        try
        {
            string mode = args.Length > 0 ? args[0] : "verify";
            string path = Arg(args, "--config", "DicePrototype/Assets/Game/Data/P0Config.json");
            cfg = JsonSerializer.Deserialize<GameConfig>(GameConfig.PrepareJsonForUnity(File.ReadAllText(path)), new JsonSerializerOptions { IncludeFields = true });
            cfg.Validate(); P = new Params(cfg);
            int threads = int.Parse(Arg(args, "--threads", "0"));
            Par = new ParallelOptions { MaxDegreeOfParallelism = threads > 0 ? threads : Math.Min(8, Math.Max(1, Environment.ProcessorCount - 2)) };
            Console.WriteLine("config=" + path + " version=" + cfg.configVersion + " threads=" + Par.MaxDegreeOfParallelism + " args=" + string.Join(" ", args));
            switch (mode)
            {
                case "verify": return Verify(int.Parse(Arg(args, "--n", "200000")), Arg(args, "--out", null));
                case "cards": return Cards(int.Parse(Arg(args, "--hands", "1500")), Arg(args, "--out", null));
                case "scen": return Scenarios(int.Parse(Arg(args, "--hands", "3000")), Arg(args, "--out", null));
                case "probe": return Probe();
                case "trace": return Trace(uint.Parse(Arg(args, "--seed", "5000")), int.Parse(Arg(args, "--steps", "80")), Arg(args, "--out", "trace.json"), args.Contains("--full"));
                case "run": Runner.Prepared = args.Contains("--prepared"); Runner.Lab = args.Contains("--lab"); return Runs(uint.Parse(Arg(args, "--first", "1000")), int.Parse(Arg(args, "--seeds", "200")), Arg(args, "--out", null), args.Contains("--weak"), path);
                default: Console.WriteLine("unknown mode"); return 2;
            }
        }
        finally { Console.Out.Flush(); logFile?.Dispose(); }
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
    /// <summary>JSON has no NaN: medians of empty groups and similar values are written as null.</summary>
    sealed class FiniteDouble : System.Text.Json.Serialization.JsonConverter<double>
    {
        public override double Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => reader.TokenType == JsonTokenType.Null ? double.NaN : reader.GetDouble();
        public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options) { if (double.IsFinite(value)) writer.WriteNumberValue(value); else writer.WriteNullValue(); }
    }
    static readonly JsonSerializerOptions Pretty = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, Converters = { new FiniteDouble() } };
    static int Num(string controllerId) => int.Parse(controllerId.Substring(1));
    static string Generation(int a) => a < 54 ? "p0.6" : a < 104 ? "p0.9" : "p1.0";

    // ------------------------------------------------------------------ verify
    /// <summary>Enumerate all independent C53 draws, retaining repeated entries in the configured distribution.
    /// This also verifies that the planning expectation preserves the correlation of one owner's mimics.</summary>
    static (int builds, int outcomes, int mismatches) VerifyC53Expectations()
    {
        string[][] builds = { new[] { "C53", "C53" }, new[] { "C41", "C53", "C53" }, new[] { "C41", "C41", "C53", "C53", "C53" } };
        int outcomes = 0, mismatches = 0;
        foreach (var definitions in builds)
        {
            var st = Fresh(); st.controllers = definitions.Select((id, i) => Ctl(id, i + 1)).ToList();
            var b = Build.From(st, P, false); var h = new Sub { n = 1, rolls = 1 }; h.Put(0, b.dice[0], 0);
            double expected = b.Score(h, out _), sum = 0; int count = 0;
            var die = st.dice[0]; var face = die.faces.First(f => f.faceId == "F01");
            var snap = new SubmissionSnapshot
            {
                runId = "c53-expectation", stageId = "STAGE_01", handId = "H", configVersion = cfg.configVersion,
                capacity = b.capacity, rollsRemaining = h.rolls, wallet = st.gold, equippedControllers = st.controllers.ToArray(),
                handLevels = st.handLevels.ToArray(), colorLevels = st.colorLevels.ToArray(), rollsUsed = 1,
                submissionsRemainingBefore = b.subsLeft, permanentDiceCount = b.permDice, ordinaryControllerSlots = st.controllerSlots,
                dice = new[] { new SubmissionDie { dieId = die.dieId, faceId = face.faceId, currentPip = face.permanentPip, currentColor = face.baseColor,
                    permanentSixFaceSum = die.faces.Sum(f => f.permanentPip), lifetime = die.lifetime, permanentFacesCopy = die.faces } }
            };
            var choices = st.controllers.Where(c => c.definitionId == "C53").Select(c => new RandomMultiplierChoice { ownerInstanceId = c.instanceId }).ToArray();
            void Visit(int owner)
            {
                if (owner < choices.Length)
                {
                    foreach (int units in P.c53units) { choices[owner].units2 = units; Visit(owner + 1); }
                    return;
                }
                snap.resolvedRandomChoices = choices;
                var real = ScoreCalculator.Evaluate(snap, cfg);
                double exact = (double)BigInteger.Parse(real.scoreNumerator) / (double)BigInteger.Parse(real.scoreDenominator);
                b.SetRandomChoices(choices); double actual = b.Score(h, out _);
                if (Math.Abs(actual - exact) > 1e-9 * Math.Max(1, exact)) mismatches++;
                sum += exact; count++; outcomes++;
            }
            Visit(0);
            double mean = sum / count;
            if (Math.Abs(expected - mean) > 1e-9 * Math.Max(1, mean)) mismatches++;
        }
        return (builds.Length, outcomes, mismatches);
    }

    /// <summary>Random frozen submissions scored by both the real ScoreCalculator.Evaluate and the fast scorer.
    /// A case passes when (1) the fast value equals the exact rational score (scoreNumerator / scoreDenominator)
    /// within 1e-9 relative, (2) its floor equals the real floored score, (3) the primary hand is the same and
    /// (4) the triggered ability set and ordered source instance IDs are the same. p1.0.0 cases also draw a boss modifier, disabled controllers,
    /// temporary dice, face weights and the eight new snapshot fields. Coverage is printed per controller, per boss
    /// modifier and per scoring effect; a scoring controller or effect that never took effect fails the run.</summary>
    static int Verify(int n, string outPath)
    {
        var baseState = Fresh(); var rnd = new Random(12345); int bad = 0; double maxRel = 0;
        var ids = cfg.controllers.Select(c => c.id).Where(id => id != "C41").ToArray(); string[] ench = { "none", "slotless", "base10", "mult2", "submit1", "roll1" };
        string[] textures = { "none", "none", "none", "smooth", "rough" }; string[] kinds = { null, "normal", "pressure", "boss", "boss" };
        var present = new int[Params.N]; var trig = new int[Params.N];
        var duplicatePresent = new int[Params.N]; var duplicateTriggered = new int[Params.N];
        int duplicateBuilds = 0, twoCopyBuilds = 0, threeCopyBuilds = 0, duplicateMimicBuilds = 0, duplicateCopiedByMimicBuilds = 0;
        var feature = new SortedDictionary<string, int>(StringComparer.Ordinal);
        Action<string, bool> mark = (name, on) => { feature[name] = feature.GetValueOrDefault(name) + (on ? 1 : 0); };
        var modSeen = cfg.bossModifiers.ToDictionary(m => m.id, m => 0); var modChanged = cfg.bossModifiers.ToDictionary(m => m.id, m => 0);
        var fxSeen = ModFx.ScoringEffects.ToDictionary(e => e, e => 0); var fxActive = ModFx.ScoringEffects.ToDictionary(e => e, e => 0);
        var h = new Sub(); int[] face = new int[12]; int[] pal = new int[4];
        for (int it = 0; it < n; it++)
        {
            var st = baseState.Clone(); st.controllers.Clear(); st.dice.Clear();
            int serial = 1, shapeClass = it % 6;
            // Keep no-copy controls, force two/three real copies, mix them with mimics, and also sample with replacement.
            // The controlled classes remain randomized in definition, state, enchantment, position and context.
            var pick = new List<string>();
            if (shapeClass == 0)
            {
                pick.AddRange(ids.OrderBy(_ => rnd.Next()).Take(rnd.Next(0, 10)));
                if (rnd.Next(4) == 0) pick.Insert(rnd.Next(pick.Count + 1), "C41");
            }
            else if (shapeClass == 5)
            {
                for (int k = rnd.Next(2, 11); k > 0; k--) pick.Add(rnd.Next(8) == 0 ? "C41" : ids[rnd.Next(ids.Length)]);
            }
            else
            {
                string duplicate = ids[rnd.Next(ids.Length)]; int copies = shapeClass == 1 || shapeClass == 3 ? 2 : 3;
                int mimics = shapeClass == 3 ? 1 : shapeClass == 4 ? 2 : 0;
                pick.AddRange(Enumerable.Repeat(duplicate, copies));
                pick.AddRange(ids.Where(id => id != duplicate).OrderBy(_ => rnd.Next()).Take(rnd.Next(11 - copies - mimics)));
                pick = pick.OrderBy(_ => rnd.Next()).ToList();
                // Adjacent mimics resolve to one chosen real duplicate; its state may differ from the other copies.
                int target = pick.IndexOf(duplicate);
                for (int k = 0; k < mimics; k++) pick.Insert(target, "C41");
            }
            var multiplicities = pick.GroupBy(id => id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
            bool hasDuplicate = multiplicities.Values.Any(count => count >= 2);
            if (hasDuplicate) duplicateBuilds++;
            if (multiplicities.Values.Any(count => count == 2)) twoCopyBuilds++;
            if (multiplicities.Values.Any(count => count >= 3)) threeCopyBuilds++;
            if (hasDuplicate && pick.Contains("C41")) duplicateMimicBuilds++;
            foreach (string id in pick)
            {
                string e; do e = ench[rnd.Next(ench.Length)]; while (!ControllerRules.IsEnchantmentLegal(id, e, cfg));
                var c = Ctl(id, serial++, e, rnd.Next(2) == 0 ? "red" : "blue"); if (id == "C51") c.state.turnoverUnits2 = 3 + rnd.Next(5);
                if (ControllerRules.HasGrowth(id)) c.state.growth = rnd.Next(4) == 0 ? 0 : rnd.Next(0, 201);
                if (id == "C141") c.state.growth = rnd.Next(0, cfg.Controller("C141").parameters.expireGrowth);   // stages survived: 0..3
                st.controllers.Add(c);
            }
            // The shape the face-up sides are steered towards (see below). For a run (7) most dice keep all six pips,
            // otherwise five dice showing five consecutive pips would be too rare to cover the straight cards.
            int shape = rnd.Next(12);
            int m = shape == 7 && rnd.Next(4) != 0 ? rnd.Next(5, 11) : rnd.Next(1, 11);
            for (int d = 0; d < m; d++)
            {
                var die = DiceFactory.CreateStandard(cfg, d + 1);
                bool low = rnd.Next(4) == 0, monoPip = rnd.Next(6) == 0, monoColor = rnd.Next(5) == 0, weighted = rnd.Next(4) == 0, full = shape == 7 && rnd.Next(5) != 0; int onePip = rnd.Next(1, 7); string oneColor = rnd.Next(2) == 0 ? "red" : "blue";
                foreach (var f in die.faces)
                {
                    if (!full) f.permanentPip = monoPip ? onePip : low ? rnd.Next(1, 3) : rnd.Next(1, 7);
                    f.baseColor = monoColor ? oneColor : rnd.Next(2) == 0 ? "red" : "blue";
                    f.isGold = rnd.Next(5) == 0; f.texture = textures[rnd.Next(textures.Length)]; if (weighted) f.baseWeight = rnd.Next(1, cfg.maxFaceWeight + 1);
                }
                die.isGlass = rnd.Next(5) == 0; if (rnd.Next(6) == 0) die.lifetime = "stage_temporary"; st.dice.Add(die);
            }
            foreach (var hl in st.handLevels) hl.level = rnd.Next(0, 9);
            foreach (var hl in st.colorLevels) hl.level = rnd.Next(0, 9);
            int purse = rnd.Next(10); st.gold = purse < 2 ? rnd.Next(0, 6) : purse < 8 ? rnd.Next(0, 60) : rnd.Next(60, 130);
            st.controllerSlots = rnd.Next(6) == 0 ? 0 : cfg.initialControllerSlots + rnd.Next(cfg.maxOrdinaryControllerSlots - cfg.initialControllerSlots + 1);
            st.inventory.Clear(); if (rnd.Next(2) == 0) for (int q = rnd.Next(1, 4); q > 0; q--) st.inventory.Add(new OwnedItem { instanceId = "I" + q, contentId = "I06" });
            // p1.0.0 stage context: a boss modifier in three cases of five, the controllers it disables plus (one case
            // in six) one or two arbitrary ones, the stage kind, hands already used in the stage.
            string modifierId = rnd.Next(5) < 2 ? null : cfg.bossModifiers[rnd.Next(cfg.bossModifiers.Length)].id;
            var disabled = new List<string>(ControllerRules.DisabledBy(st.controllers, cfg, modifierId));
            if (rnd.Next(6) == 0 && st.controllers.Count > 0) for (int q = rnd.Next(1, 3); q > 0; q--) { string id = st.controllers[rnd.Next(st.controllers.Count)].instanceId; if (!disabled.Contains(id)) disabled.Add(id); }
            string stageKind = kinds[rnd.Next(kinds.Length)];
            var ctx = new StageCtx { modifierId = modifierId, disabled = disabled.ToArray(), boss = stageKind == "boss" };
            var b = Build.From(st, P, false, ctx: ctx); b.redWave = rnd.Next(12) == 0; b.blueWave = !b.redWave && rnd.Next(12) == 0; b.itemMult = rnd.Next(10) == 0 ? 2 : 1; b.ordinal = rnd.Next(3);
            var usedHands = new List<string>(); if (rnd.Next(2) == 0) for (int q = rnd.Next(1, 4); q > 0; q--) { string id = P.hId[rnd.Next(13)]; if (!usedHands.Contains(id)) usedHands.Add(id); }
            foreach (string id in usedHands) b.usedHands |= 1 << P.hIndex[id];
            int prevSel = rnd.Next(-2, 13); b.prev = Math.Max(-1, prevSel);   // -2 = no previous hand, -1 = scatter
            if (rnd.Next(2) == 0) b.capacity = rnd.Next(1, 9);
            int cnt = rnd.Next(1, Math.Min(m, b.capacity) + 1);
            if (shape == 7 && m >= 5 && rnd.Next(4) != 0) { b.capacity = Math.Max(b.capacity, 5); cnt = rnd.Next(5, Math.Min(m, b.capacity) + 1); }   // room for a straight
            int poolN = rnd.Next(0, Math.Min(5, m - cnt) + 1), rolls = rnd.Next(0, 4);
            b.subsLeft = rnd.Next(1, 5); b.permDice = rnd.Next(0, 9); h.rolls = rolls; h.rollsUsed = rnd.Next(1, 7);
            var order = Enumerable.Range(0, m).OrderBy(_ => rnd.Next()).ToArray();
            // Two thirds of the cases steer the face-up sides towards a shape so the hand-type and order cards trigger
            // often: one pip, pairs, triples, a run, a palindrome, all even, a +1 chain, exactly one pair; and
            // independently towards a colour pattern: alternating, reds first, one colour. The dice stay random.
            int start = rnd.Next(1, 7), runStart = rnd.Next(1, 3), colourShape = rnd.Next(8), redLead = rnd.Next(1, 4); bool firstRed = rnd.Next(2) == 0;
            for (int q = 0; q < 4; q++) pal[q] = rnd.Next(1, 7);
            for (int rank = 0; rank < m; rank++)
            {
                int di = order[rank]; face[di] = rnd.Next(6);
                int want = shape == 4 ? start : shape == 5 ? (start + rank / 2 - 1) % 6 + 1 : shape == 6 ? (start + rank / 3 - 1) % 6 + 1 : shape == 7 ? runStart + rank % 5
                    : shape == 8 ? (rank < cnt ? pal[Math.Min(rank, cnt - 1 - rank) % 4] : 0) : shape == 9 ? 2 * rnd.Next(1, 4) : shape == 10 ? Math.Min(6, start + rank) : shape == 11 ? (rank < 2 ? start : start % 6 + 1 + rank % 2) : 0;
                if (want > 6) want = 6;
                int wantRed = colourShape == 5 ? (((rank & 1) == 0) == firstRed ? 1 : 0) : colourShape == 6 ? (rank < redLead ? 1 : 0) : colourShape == 7 ? (firstRed ? 1 : 0) : -1;
                if (want == 0 && wantRed < 0) continue;
                var fd = b.dice[di];
                var match = Enumerable.Range(0, 6).Where(x => (want == 0 || fd.pip[x] == want) && (wantRed < 0 || fd.red[x] == (wantRed == 1))).ToArray();
                if (match.Length == 0 && want != 0) match = Enumerable.Range(0, 6).Where(x => fd.pip[x] == want).ToArray();
                if (match.Length == 0 && wantRed >= 0) match = Enumerable.Range(0, 6).Where(x => fd.red[x] == (wantRed == 1)).ToArray();
                if (match.Length > 0) face[di] = match[rnd.Next(match.Length)];
            }
            var sub = new SubmissionDie[cnt]; var pool = new PoolDie[poolN]; int poolMask = 0; h.n = cnt;
            for (int i = 0; i < cnt + poolN; i++)
            {
                int di = order[i]; var d = st.dice[di]; var f = d.faces.First(x => x.faceId == "F0" + (face[di] + 1));
                if (i < cnt)
                {
                    sub[i] = new SubmissionDie { dieId = d.dieId, faceId = f.faceId, currentPip = f.permanentPip, currentColor = f.baseColor, permanentSixFaceSum = d.faces.Sum(x => x.permanentPip), isGlass = d.isGlass, isGold = f.isGold, lifetime = d.lifetime, permanentFacesCopy = d.faces.Select(x => x.Clone()).ToArray() };
                    h.Put(i, b.dice[di], face[di]);
                }
                else { pool[i - cnt] = new PoolDie { dieId = d.dieId, faceId = f.faceId, currentPip = f.permanentPip, currentColor = f.baseColor }; poolMask |= 1 << di; }
            }
            h.SetPool(b.dice, face, poolMask, P.p71pip);
            var choices = st.controllers.Where(c => c.definitionId == "C53")
                .Select(c => new RandomMultiplierChoice { ownerInstanceId = c.instanceId, units2 = P.c53units[rnd.Next(P.c53units.Length)] }).ToArray();
            b.SetRandomChoices(choices);
            var snap = new SubmissionSnapshot
            {
                runId = "v", stageId = "STAGE_01", handId = "H", configVersion = cfg.configVersion, capacity = b.capacity, rollsRemaining = rolls, submissionOrdinalBefore = b.ordinal,
                redWave = b.redWave, wallet = st.gold, previousPrimaryHandId = prevSel == -2 ? null : prevSel == -1 ? "scatter" : P.hId[prevSel], equippedControllers = st.controllers.Select(c => c.Clone()).ToArray(),
                handLevels = st.handLevels.Select(x => x.Clone()).ToArray(), colorLevels = st.colorLevels.Select(x => x.Clone()).ToArray(), mimicBindings = null, dice = sub,
                resolvedRandomChoices = choices,
                rollsUsed = h.rollsUsed, submissionsRemainingBefore = b.subsLeft, permanentDiceCount = b.permDice, poolDice = pool,
                stageKind = stageKind, modifierId = modifierId ?? "", disabledControllerIds = disabled.ToArray(), stageHandsUsed = usedHands.ToArray(),
                inventoryCount = st.inventory.Count, ordinaryControllerSlots = st.controllerSlots, blueWave = b.blueWave, scoreItemMultiplier = b.itemMult == 2 ? 2 : 0
            };
            var real = ScoreCalculator.Evaluate(snap, cfg);
            double truth = (double)BigInteger.Parse(real.score), exact = (double)BigInteger.Parse(real.scoreNumerator) / (double)BigInteger.Parse(real.scoreDenominator);
            double fast = b.Score(h, out int prim);
            string realPrim = real.primaryHandId == "scatter" ? null : real.primaryHandId; string fastPrim = prim < 0 ? null : P.hId[prim];
            var fastTrig = new HashSet<string>(Enumerable.Range(1, Params.N - 1).Where(a => b.lastHits.Has(a)).Select(Params.Cid), StringComparer.Ordinal);
            var fastSources = b.TriggeredSourceIds().ToArray();
            double rel = Math.Abs(fast - exact) / Math.Max(1.0, exact); if (rel > maxRel) maxRel = rel;
            bool ok = rel <= 1e-9 && Math.Abs(Math.Floor(fast + 1e-6) - truth) <= 1e-9 * truth + 0.5 && realPrim == fastPrim && fastTrig.SetEquals(real.triggeredControllerIds)
                && fastSources.SequenceEqual(real.triggeredSourceIds);
            if (!ok && ++bad <= 8)
                Console.WriteLine($"MISMATCH it={it} real={real.score} exact={exact:R} fast={fast:R} prim {realPrim}/{fastPrim} trig real=[{string.Join(",", real.triggeredControllerIds)}] fast=[{string.Join(",", fastTrig.OrderBy(x => x, StringComparer.Ordinal))}] ctl=[{string.Join(",", st.controllers.Select(c => c.definitionId + ":" + c.enchantmentId + (c.state.growth > 0 ? "+g" + c.state.growth : "") + (disabled.Contains(c.instanceId) ? "!off" : "")))}] pips=[{string.Join(",", sub.Select(d => d.currentPip + d.currentColor.Substring(0, 1) + (d.lifetime == "permanent" ? "" : "t")))}] pool=[{string.Join(",", pool.Select(d => d.currentPip + d.currentColor.Substring(0, 1)))}] cap={b.capacity} rolls={rolls} used={h.rollsUsed} subsLeft={b.subsLeft} perm={b.permDice} mod={modifierId} kind={stageKind} ordinal={b.ordinal} wallet={st.gold} inv={st.inventory.Count} slots={st.controllerSlots} waves={b.redWave}/{b.blueWave} item={b.itemMult} usedHands=[{string.Join(",", usedHands)}]");
            // Coverage.
            var sources = ControllerRules.ResolveSources(snap.equippedControllers, null, snap.disabledControllerIds);
            foreach (var group in sources.Where(s => s.abilityId != null && s.emitterDefinitionId != "C41").GroupBy(s => s.abilityId))
            {
                if (group.Count() < 2) continue;
                duplicatePresent[Num(group.Key)]++;
                if (group.Count(s => real.triggeredSourceIds.Contains(s.emitterId)) >= 2) duplicateTriggered[Num(group.Key)]++;
            }
            if (sources.Any(s => s.emitterDefinitionId == "C41" && s.abilityId != null && multiplicities.GetValueOrDefault(s.abilityId) >= 2)) duplicateCopiedByMimicBuilds++;
            mark("two C53 owners with different frozen draws", choices.Select(c => c.units2).Distinct().Count() >= 2);
            mark("C53 owner copied by a mimic", sources.Any(s => s.emitterDefinitionId == "C41" && s.abilityId == "C53"));
            foreach (string id in sources.Where(s => s.abilityId != null).Select(s => s.abilityId).Distinct()) present[Num(id)]++;
            foreach (string id in real.triggeredControllerIds) trig[Num(id)]++;
            mark("pool dice > 0", poolN > 0); mark("pool red merged into red group (C73)", real.redPoolCount > 0); mark("retriggers > 0", real.pipRetriggers.Any(x => x > 0));
            mark("hand multiplier add > 0", real.handMultAdd100 > 0); mark("controller base add > 0", real.controllerBaseAdds.Length > 0);
            mark("mimic (C41) copying an ability", sources.Any(s => s.emitterDefinitionId == "C41" && s.abilityId != null));
            mark("mimic (C41) copying a growth card", sources.Any(s => s.emitterDefinitionId == "C41" && s.abilityId != null && ControllerRules.HasGrowth(s.abilityId)));
            mark("growth > 0 on a growth card", st.controllers.Any(c => c.state.growth > 0));
            mark("submitted die: textured face up", sub.Any(d => DieFacts.Texture(d) != "none")); mark("submitted die: mono-colour", sub.Any(DieFacts.MonoColor)); mark("submitted die: mono-pip", sub.Any(DieFacts.MonoPip));
            mark("submitted die: gold face up", sub.Any(d => d.isGold)); mark("submitted die: glass", sub.Any(d => d.isGlass)); mark("submitted die: temporary", sub.Any(d => d.lifetime == "stage_temporary"));
            mark("last submission of the stage", b.subsLeft == 1); mark("exactly one roll used", h.rollsUsed == 1); mark("no rolls left", rolls == 0);
            mark("enchant base10 or mult2 counted", real.enchantBaseCount + real.enchantMultiplierCount > 0);
            // p1.0.0 inputs
            mark("stage kind boss", stageKind == "boss"); mark("inventory > 0", st.inventory.Count > 0); mark("blue wave", b.blueWave); mark("red wave", b.redWave); mark("item multiplier x2 (I09)", b.itemMult == 2);
            mark("empty ordinary slots > 0", st.controllerSlots - ControllerRules.OrdinaryUsed(st.controllers) > 0); mark("stage hands used: primary repeated", usedHands.Contains(real.primaryHandId));
            mark("red group bonus (C146)", real.redBonusCount > 0); mark("gold spent (C138)", real.afterScorePlan.goldSpent > 0);
            mark("C138: fewer sources triggered than held", sources.Count(s => s.abilityId == "C138") * cfg.Controller("C138").parameters.goldCost > real.afterScorePlan.goldSpent && sources.Any(s => s.abilityId == "C138"));
            mark("face changes planned", real.afterScorePlan.faceChanges.Length > 0); mark("sealed die", real.sealedDice.Any(x => x));
            mark("disabled controllers > 0", disabled.Count > 0);
            if (disabled.Count > 0)
            {
                var open = snap.Clone(); open.disabledControllerIds = Array.Empty<string>(); var free = ScoreCalculator.Evaluate(open, cfg);
                mark("disabled controller changed the result", free.scoreNumerator != real.scoreNumerator || free.scoreDenominator != real.scoreDenominator || !free.triggeredSourceIds.SequenceEqual(real.triggeredSourceIds));
            }
            if (modifierId != null)
            {
                // The same snapshot without the modifier shows whether the modifier, and each of its effects, did anything.
                var plain = snap.Clone(); plain.modifierId = ""; var off = ScoreCalculator.Evaluate(plain, cfg);
                modSeen[modifierId]++; if (off.scoreNumerator != real.scoreNumerator || off.scoreDenominator != real.scoreDenominator) modChanged[modifierId]++;
                foreach (string e in cfg.Modifier(modifierId).effects)
                {
                    if (!fxSeen.ContainsKey(e)) continue;
                    fxSeen[e]++; bool active;
                    switch (e)
                    {
                        case "NoStraight": active = off.straightMult100 > 0; break;
                        case "SmallHandsOnly": active = off.matchedGroupIds.Any(g => g != "pair" && g != "two_pair" && g != "trips"); break;
                        case "NoRepeatHand": case "HandHalf": active = real.handMultBeforeModifier100 != real.totalHandMult100; break;
                        case "ZeroLevels": active = off.groupMult100 != real.groupMult100 || off.straightMult100 != real.straightMult100 || off.colorMult100 != real.colorMult100; break;
                        case "NoRed": active = off.redMult100 != real.redMult100 || (off.colorId == "all_red" && off.colorMult100 != real.colorMult100); break;
                        case "NoBlue": active = off.colorId == "all_blue" && off.colorMult100 != real.colorMult100; break;
                        case "FirstHalf": case "LaterHalf": active = real.globalFactors.Any(g => g.emitterId == "BOSS"); break;
                        case "PipsOnly": active = off.baseTotal != real.baseTotal; break;
                        case "GrowthZero": active = sources.Any(s => s.abilityId != null && ControllerRules.HasGrowth(s.abilityId) && s.state.growth > 0); break;
                        case "EnchantSilence": active = off.enchantBaseCount + off.enchantMultiplierCount > 0; break;
                        default: active = real.sealedDice.Any(x => x); break;   // the eight seals
                    }
                    if (active) fxActive[e]++;
                }
            }
        }
        Console.WriteLine($"verify: {n} random submissions, mismatches={bad}, max relative error vs exact rational={maxRel:E2}");
        Console.WriteLine($"duplicate builds checked: {duplicateBuilds}; with a two-copy definition={twoCopyBuilds}; with three-or-more={threeCopyBuilds}; mixed with C41={duplicateMimicBuilds}; active C41 copying a duplicated definition={duplicateCopiedByMimicBuilds}");
        var c53Expectation = VerifyC53Expectations();
        Console.WriteLine($"C53 expectations: {c53Expectation.builds} builds, {c53Expectation.outcomes} frozen outcomes, mismatches={c53Expectation.mismatches}");
        Console.WriteLine("\ncoverage: cases where each input was exercised");
        foreach (var kv in feature) Console.WriteLine($"  {kv.Key,-46} {kv.Value}");
        Console.WriteLine("\ncoverage per controller: present = cases where the ability was a source (owned or mimicked, not disabled), triggered = cases where the real calculator listed it as triggered");
        var nonScoring = new HashSet<string>(cfg.enchantRules.nonSubmitControllerIds, StringComparer.Ordinal) { "C41" };
        var gaps = new List<string>(); var rows = new List<object>(); var line = new StringBuilder();
        int col = 0;
        foreach (var c in cfg.controllers)
        {
            int a = Num(c.id); bool scoring = !nonScoring.Contains(c.id);
            line.Append($"{c.id,-5}{present[a],7}/{trig[a],-7}{(scoring ? " " : "*")} ");
            if (++col % 6 == 0) { Console.WriteLine("  " + line); line.Clear(); }
            rows.Add(new { id = c.id, name = c.displayName, scoring, generation = Generation(a), present = present[a], triggered = trig[a], duplicatePresent = duplicatePresent[a], duplicateTriggered = duplicateTriggered[a] });
            if (scoring && (present[a] == 0 || trig[a] == 0)) gaps.Add(c.id);
            if (scoring && (duplicatePresent[a] == 0 || duplicateTriggered[a] == 0)) gaps.Add(c.id + " duplicate");
        }
        if (line.Length > 0) Console.WriteLine("  " + line);
        Console.WriteLine("  (* = non-scoring by rule: never a triggered source. C41 is counted under the ability it copies.)");
        foreach (string gen in new[] { "p0.6", "p0.9", "p1.0" })
        {
            var set = cfg.controllers.Where(c => Generation(Num(c.id)) == gen && !nonScoring.Contains(c.id)).ToArray();
            Console.WriteLine($"scoring controllers {gen}: {set.Length}; with present>0 and triggered>0: {set.Count(c => present[Num(c.id)] > 0 && trig[Num(c.id)] > 0)}; min present={set.Min(c => present[Num(c.id)])}, min triggered={set.Min(c => trig[Num(c.id)])}");
        }
        Console.WriteLine("\ncoverage per boss modifier: cases / cases where removing the modifier changes the exact score (session-only modifiers never do)");
        var modRows = new List<object>(); line.Clear(); col = 0;
        foreach (var mdef in cfg.bossModifiers)
        {
            bool scoring = mdef.effects.Any(e => fxSeen.ContainsKey(e));
            line.Append($"{mdef.id,-4}{modSeen[mdef.id],6}/{modChanged[mdef.id],-6}{(scoring ? " " : "*")} ");
            if (++col % 6 == 0) { Console.WriteLine("  " + line); line.Clear(); }
            modRows.Add(new { id = mdef.id, name = mdef.displayName, tier = mdef.tier, effects = mdef.effects, scoring, cases = modSeen[mdef.id], changedScore = modChanged[mdef.id] });
            if (scoring && (modSeen[mdef.id] == 0 || modChanged[mdef.id] == 0)) gaps.Add(mdef.id);
        }
        if (line.Length > 0) Console.WriteLine("  " + line);
        Console.WriteLine("  (* = no scoring effect)");
        Console.WriteLine("\ncoverage per scoring effect: cases with the effect / cases where it took effect");
        var fxRows = new List<object>();
        foreach (string e in ModFx.ScoringEffects)
        {
            Console.WriteLine($"  {e,-16} {fxSeen[e],7} / {fxActive[e]}");
            fxRows.Add(new { effect = e, cases = fxSeen[e], active = fxActive[e] });
            if (fxSeen[e] == 0 || fxActive[e] == 0) gaps.Add(e);
        }
        if (feature["disabled controller changed the result"] == 0) gaps.Add("disabled controllers");
        if (duplicateBuilds == 0 || twoCopyBuilds == 0 || threeCopyBuilds == 0 || duplicateCopiedByMimicBuilds == 0) gaps.Add("duplicate build classes");
        if (feature.GetValueOrDefault("two C53 owners with different frozen draws") == 0 || feature.GetValueOrDefault("C53 owner copied by a mimic") == 0) gaps.Add("independent and mimicked C53 draws");
        Console.WriteLine(gaps.Count == 0 ? "\ncoverage: OK, every scoring controller, scoring boss modifier and scoring effect was present and took effect at least once" : "\ncoverage: GAPS " + string.Join(",", gaps));
        if (outPath != null) File.WriteAllText(outPath, JsonSerializer.Serialize(new { config = cfg.configVersion, cases = n, mismatches = bad, maxRelativeError = maxRel,
            duplicateBuilds, twoCopyBuilds, threeCopyBuilds, duplicateMimicBuilds, duplicateCopiedByMimicBuilds,
            c53ExpectationBuilds = c53Expectation.builds, c53ExpectationOutcomes = c53Expectation.outcomes, c53ExpectationMismatches = c53Expectation.mismatches,
            features = feature, controllers = rows, modifiers = modRows, effects = fxRows, coverageGaps = gaps }, Pretty));
        return bad == 0 && c53Expectation.mismatches == 0 && gaps.Count == 0 ? 0 : 1;
    }

    // ------------------------------------------------------------------ measurement helpers
    public sealed class Stat { public double mean, median, p10, p90, hit, dice, stage3, stage; public int subs; public int[] prim = new int[13]; }
    /// <summary>Hands cycle through the stage's submission positions (ordinal, submissions left, hands already used),
    /// so "first / later / last submission" cards pay on the right hands; `stage` = expected stage total =
    /// mean hand x submissions per stage.</summary>
    public static Stat Measure(RunState st, int hands, ulong seed, int hitId = 0, double target = 0, StageCtx ctx = null)
    {
        var b = Build.From(st, P, false, ctx: ctx);
        var pl = new Planner(); pl.Set(b); pl.rollRng = new Rng(seed); pl.rng = new Rng(seed ^ 0x77UL);
        var v = new double[hands]; var s = new Stat(); double hit = 0, dice = 0;
        for (int h = 0; h < hands; h++)
        {
            int position = h % b.submissions;
            b.subsLeft = b.submissions - position; b.ordinal = position; if (position == 0) b.usedHands = 0;
            v[h] = pl.PlayHand(out int prim, out int nSub); if (prim >= 0) { s.prim[prim]++; b.usedHands |= 1 << prim; }
            dice += nSub; if (hitId > 0 && nSub > 0 && pl.lastHits.Has(hitId)) hit++;
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

    // ------------------------------------------------------------------ dice evolution (face-growth cards)
    /// <summary>Plays `stages` stages of hands with the planner and applies what the cards do to the dice, so that
    /// face-growth cards can be measured after some stages of ordinary play. The changes come from the real
    /// ScoreCalculator (afterScorePlan of a real snapshot of the submission the planner chose); the appliers below
    /// mirror GameSession.ApplyPermanentEffects and the stage-end rule of C120 / C121. Controller growth, gold,
    /// glass breaks and the random stage-end cards are not simulated: this is a measurement aid, not a session.</summary>
    static void Evolve(RunState st, int stages, ulong seed)
    {
        var pl = new Planner { rollRng = new Rng(seed), rng = new Rng(seed ^ 0x99UL), steer = true };
        for (int stage = 0; stage < stages; stage++)
        {
            int subs = ControllerRules.SubmissionBudget(st.controllers, cfg);
            for (int k = 0; k < subs; k++)
            {
                var b = Build.From(st, P, false); b.ordinal = k; b.subsLeft = subs - k; pl.Set(b);
                pl.PlayHand(out _, out int nSub); if (nSub == 0) continue;
                var dice = st.dice.OrderBy(d => d.createdSerial).ToArray(); int[] face = pl.LastFaces; var chosen = pl.bestOrd.Take(pl.bestN).ToArray();
                Func<int, FaceState> up = i => dice[i].faces.First(f => f.faceId == "F0" + (face[i] + 1));
                var snap = new SubmissionSnapshot
                {
                    runId = "e", stageId = "STAGE_01", handId = "H", configVersion = cfg.configVersion, capacity = b.capacity, rollsRemaining = pl.lastRollsLeft, submissionOrdinalBefore = k,
                    wallet = st.gold, previousPrimaryHandId = st.previousPrimaryHandId, equippedControllers = st.controllers.Select(c => c.Clone()).ToArray(),
                    handLevels = st.handLevels.Select(x => x.Clone()).ToArray(), colorLevels = st.colorLevels.Select(x => x.Clone()).ToArray(), mimicBindings = null,
                    resolvedRandomChoices = st.controllers.Where(c => c.definitionId == "C53").Select(c => new RandomMultiplierChoice { ownerInstanceId = c.instanceId, units2 = P.c53units[0] }).ToArray(),
                    rollsUsed = pl.lastRollsUsed, submissionsRemainingBefore = subs - k, permanentDiceCount = b.permDice, stageKind = "normal", inventoryCount = st.inventory.Count, ordinaryControllerSlots = st.controllerSlots,
                    dice = chosen.Select(i => new SubmissionDie { dieId = dice[i].dieId, faceId = up(i).faceId, currentPip = up(i).permanentPip, currentColor = up(i).baseColor, permanentSixFaceSum = dice[i].faces.Sum(f => f.permanentPip), isGlass = dice[i].isGlass, isGold = up(i).isGold, lifetime = dice[i].lifetime, permanentFacesCopy = dice[i].faces.Select(f => f.Clone()).ToArray() }).ToArray(),
                    poolDice = Enumerable.Range(0, dice.Length).Where(i => (pl.lastAlive >> i & 1) != 0 && !chosen.Contains(i)).Select(i => new PoolDie { dieId = dice[i].dieId, faceId = up(i).faceId, currentPip = up(i).permanentPip, currentColor = up(i).baseColor }).ToArray()
                };
                var plan = ScoreCalculator.Evaluate(snap, cfg).afterScorePlan;
                Func<string, DieInstance> die = id => st.dice.First(d => d.dieId == id);
                if (plan.copyFirstToSecond) { var target = die(snap.dice[1].dieId); target.faces = snap.dice[0].permanentFacesCopy.Select(f => f.Clone()).ToArray(); target.isGlass = snap.dice[0].isGlass; }
                if (plan.dyeAllFacesRed) foreach (var f in die(snap.dice[0].dieId).faces) f.baseColor = "red";
                foreach (int index in plan.growthIndices) { var f = die(snap.dice[index].dieId).faces.First(x => x.faceId == snap.dice[index].faceId); f.permanentPip = Math.Max(cfg.minPipValue, Math.Min(cfg.maxPipValue, f.permanentPip + plan.growthNetDelta)); }
                foreach (int index in plan.redSpreadIndices) die(snap.dice[index].dieId).faces.First(x => x.faceId == snap.dice[index].faceId).baseColor = "red";
                foreach (int index in plan.blueSpreadIndices) die(snap.dice[index].dieId).faces.First(x => x.faceId == snap.dice[index].faceId).baseColor = "blue";
                foreach (var change in plan.faceChanges)
                {
                    var f = die(change.dieId).faces.First(x => x.faceId == change.faceId);
                    switch (change.kind)
                    {
                        case "pipDelta": f.permanentPip = Math.Max(cfg.minPipValue, Math.Min(cfg.maxPipValue, f.permanentPip + change.amount)); break;
                        case "pipSet": f.permanentPip = change.amount; break;
                        case "color": f.baseColor = change.text; break;
                        case "gold": f.isGold = change.amount != 0; break;
                        case "texture": f.texture = change.text; break;
                        case "weightDelta": f.baseWeight = Math.Max(1, Math.Min(cfg.maxFaceWeight, f.baseWeight + change.amount)); break;
                    }
                }
                st.previousPrimaryHandId = plan.nextPreviousPrimary;
            }
            var sources = ControllerRules.ResolveSources(st.controllers);
            foreach (string id in new[] { "C120", "C121" })
                for (int q = sources.Count(x => x.abilityId == id); q > 0; q--)
                    foreach (var d in st.dice.Where(d => d.lifetime == "permanent").OrderBy(d => d.createdSerial))
                    {
                        var ordered = d.faces.OrderBy(f => f.faceId, StringComparer.Ordinal).ToArray();
                        var f = id == "C120" ? ordered.OrderBy(x => x.permanentPip).First() : ordered.OrderByDescending(x => x.permanentPip).First();
                        if (id == "C120" ? f.permanentPip < cfg.maxPipValue : f.permanentPip > cfg.minPipValue) f.permanentPip += cfg.Controller(id).parameters.delta;
                    }
        }
    }

    // ------------------------------------------------------------------ cards
    /// <summary>Growth a card would plausibly hold around the middle of a run (stage 12 or so, bought around stage 4).
    /// These are stated assumptions for the `cards` table, not measurements.</summary>
    public static readonly Dictionary<string, (long growth, string why)> MidGrowth = new Dictionary<string, (long, string)>
    {
        ["C54"] = (9, "3 consecutive same-primary submissions (x1.45)"), ["C55"] = (40, "20 submissions (+40 base)"), ["C56"] = (20, "10 submissions without red (x2.0)"),
        ["C57"] = (24, "8 submissions with a red group of 3+ (x2.2)"), ["C58"] = (32, "8 stages cleared (x2.6)"), ["C59"] = (32, "8 unused submissions (+32 base)"),
        ["C60"] = (12, "6 die-face upgrades bought (x1.6)"), ["C61"] = (20, "10 hand/colour levels gained (x2.0)"), ["C62"] = (50, "50 rolls thrown (+50 base)"),
        ["C90"] = (30, "2 glass dice broken (x2.5)"), ["C100"] = (20, "10 gold of controllers sacrificed (x2.0)"),
        // p1.0.0
        ["C129"] = (30, "3 boss stages cleared (x2.5)"), ["C132"] = (36, "3 pressure challenges completed (x2.8)"), ["C134"] = (30, "10 items used (x2.5)"),
        ["C141"] = (2, "2 stages after purchase (x2.0; x3 when bought, gone after 4 stages)")
    };
    /// <summary>Cards whose effect depends on something the reference builds do not have. The stated situation is
    /// applied to the build with the card and to the build it is compared with.</summary>
    static readonly Dictionary<string, (string note, Action<RunState> prep, bool boss)> CardSetup = new Dictionary<string, (string, Action<RunState>, bool)>
    {
        ["C130"] = ("首领关", null, true),
        ["C135"] = ("背包里有2个道具", s => { s.inventory.Add(new OwnedItem { instanceId = "ITEM_90001", contentId = "I06" }); s.inventory.Add(new OwnedItem { instanceId = "ITEM_90002", contentId = "I07" }); }, false),
        ["C137"] = ("带1颗临时骰", s => s.dice.Add(DiceFactory.CreateStandard(cfg, s.nextDieSerial++, "stage_temporary", "")), false),
        ["C138"] = ("钱包30金（每次提交另付3金）", s => s.gold = 30, false),
        ["C139"] = ("钱包40金", s => s.gold = 40, false),
    };
    /// <summary>Non-scoring cards whose effect can be put into the build directly: only the build with the card gets it.</summary>
    static readonly Dictionary<string, (string note, Action<RunState> effect)> CardEffect = new Dictionary<string, (string, Action<RunState>)>
    {
        ["C136"] = ("折算：每关多1颗标准临时骰", s => s.dice.Add(DiceFactory.CreateStandard(cfg, s.nextDieSerial++, "stage_temporary", ""))),
        ["C128"] = ("折算：1颗骰子变玻璃（第1关；不计碎裂损失）", s => s.dice.OrderBy(d => d.createdSerial).First(d => !d.isGlass).isGlass = true),
    };
    /// <summary>Cards that change dice over time are also measured after this many stages of ordinary play (Evolve).</summary>
    static readonly string[] DiceGrowers = { "C07", "C15", "C26", "C36", "C37", "C38", "C117", "C118", "C119", "C120", "C121", "C122", "C123", "C124", "C125", "C126", "C127" };
    static readonly int[] EvolveStages = { 6, 12 };

    static int Cards(int hands, string outPath)
    {
        var contexts = new (string name, Func<RunState> make)[] { ("A 裸装起手(5标准骰,无卡,0金)", () => { var s = Fresh(); s.gold = 0; return s; }), ("B 中期通用(稳稳分数+稳稳乘数,常用牌型Lv2,15金)", Mid) };
        var rows = new List<object>();
        Console.WriteLine("equivalent multiplier = expected stage total (mean hand x submissions per stage) with the card / without it.");
        Console.WriteLine("growth cards are listed twice: at growth 0 (as bought) and at the stated mid-run growth:");
        foreach (var kv in MidGrowth) Console.WriteLine($"  {kv.Key} {cfg.Controller(kv.Key).displayName}: growth {kv.Value.growth} = {kv.Value.why}");
        Console.WriteLine("cards measured in a stated situation (applied to both builds): " + string.Join("; ", CardSetup.Select(k => k.Key + " " + k.Value.note)));
        Console.WriteLine("non-scoring cards measured through their effect: " + string.Join("; ", CardEffect.Select(k => k.Key + " " + k.Value.note)));
        Console.WriteLine($"dice-changing cards are listed again after {string.Join(" and ", EvolveStages)} stages of ordinary play with the card (rows @s6 / @s12: three hands per stage, dice changed by the real after-score plans; the only steering is the bot's order tie-break for C117 / C119 / C127): " + string.Join(" ", DiceGrowers));
        foreach (var (name, make) in contexts)
        {
            var baseStat = Measure(make(), hands, 4242);
            Console.WriteLine($"\n== {name}: E[hand]={baseStat.mean:F1} median={baseStat.median:F1} p10={baseStat.p10:F1} p90={baseStat.p90:F1} avgDice={baseStat.dice:F2} submissions={baseStat.subs}");
            Console.WriteLine("   primary: " + string.Join(" ", Enumerable.Range(0, 13).Where(i => baseStat.prim[i] > 0).Select(i => P.hId[i] + "=" + (100.0 * baseStat.prim[i] / hands).ToString("F0") + "%")));
            var jobs = new List<(ControllerDefinition c, long growth, int evolve)>();
            foreach (var c in cfg.controllers)
            {
                jobs.Add((c, 0, 0)); if (MidGrowth.TryGetValue(c.id, out var g)) jobs.Add((c, g.growth, 0));
                if (DiceGrowers.Contains(c.id)) foreach (int stages in EvolveStages) jobs.Add((c, 0, stages));
            }
            var res = new ConcurrentBag<(ControllerDefinition c, long growth, int evolve, double ratio, double hit, double med, string note)>();
            Parallel.ForEach(jobs, Par, job =>
            {
                var c = job.c; double stage = 0, hit = 0, med = 0; string[] colors = c.id == "C45" ? new[] { "red", "blue" } : new[] { (string)null };
                bool special = CardSetup.TryGetValue(c.id, out var setup); bool hasEffect = CardEffect.TryGetValue(c.id, out var effect);
                var ctx = special && setup.boss ? new StageCtx { boss = true } : null;
                // The comparison build: the context itself, or the context put into the card's stated situation.
                var reference = baseStat;
                if (special) { var r0 = make(); setup.prep?.Invoke(r0); reference = Measure(r0, hands, 4242, 0, 0, ctx); }
                foreach (string col in colors)
                {
                    var st = make(); if (special) setup.prep?.Invoke(st);
                    st.controllers.Add(Ctl(c.id, 9, "none", col, job.growth));
                    if (job.evolve > 0) Evolve(st, job.evolve, 777);
                    if (hasEffect) effect.effect(st);
                    var s = Measure(st, hands, 4242, Num(c.id), 0, ctx); stage += s.stage / colors.Length; hit += s.hit / colors.Length; med += s.median / colors.Length;
                }
                res.Add((c, job.growth, job.evolve, stage / reference.stage, hit, med / Math.Max(1e-9, reference.median), special ? setup.note : hasEffect ? effect.note : null));
            });
            foreach (var r in res.OrderByDescending(x => x.ratio))
            {
                string tag = r.evolve > 0 ? "@s" + r.evolve : MidGrowth.ContainsKey(r.c.id) ? "@g" + r.growth : "";
                Console.WriteLine($"{r.c.id + tag,-9} {r.c.displayName,-6}\t{r.c.rarity,-8}\t${r.c.buyPrice}\tx{r.ratio:F2}\tmedian x{r.med:F2}\thit {r.hit * 100:F0}%\tln/$ {Math.Log(Math.Max(r.ratio, 1e-9)) / r.c.buyPrice:F3}{(r.note == null ? "" : "\t[" + r.note + "]")}");
                rows.Add(new { context = name, id = r.c.id, name = r.c.displayName, rarity = r.c.rarity, price = r.c.buyPrice, generation = Generation(Num(r.c.id)), growth = r.growth, stagesPlayed = r.evolve, situation = r.note, ratio = r.ratio, medianRatio = r.med, hit = r.hit, lnPerGold = Math.Log(Math.Max(r.ratio, 1e-9)) / r.c.buyPrice });
            }
        }
        if (outPath != null) File.WriteAllText(outPath, JsonSerializer.Serialize(new { config = cfg.configVersion, hands, midGrowth = MidGrowth.ToDictionary(k => k.Key, k => new { growth = k.Value.growth, assumption = k.Value.why }),
            situations = CardSetup.ToDictionary(k => k.Key, k => k.Value.note), effects = CardEffect.ToDictionary(k => k.Key, k => k.Value.note), diceGrowers = DiceGrowers, evolveStages = EvolveStages, rows }, Pretty));
        return 0;
    }

    // ------------------------------------------------------------------ scenarios
    static void AllFaces(RunState st, Action<FaceState> edit) { foreach (var d in st.dice) foreach (var f in d.faces) edit(f); }
    static void AddDice(RunState st, int upTo) { while (st.dice.Count < upTo) st.dice.Add(DiceFactory.CreateStandard(cfg, st.nextDieSerial++)); }
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
        list.Add(("全红+C25+8骰K=8(C21+C22)", mk(new[] { "C25", "C21", "C22" }, s => { AddDice(s, 8); AllFaces(s, f => f.baseColor = "red"); })));
        list.Add(("起手+蓝色扩散C38(未染色)", mk(new[] { "C38" }, null)));
        list.Add(("乘区四件:C33+C42+C09+C46", mk(new[] { "C33", "C42", "C09", "C46" }, null)));
        list.Add(("乘区四件:C30+C31+C32+C46", mk(new[] { "C30", "C31", "C32", "C46" }, null)));
        list.Add(("基础四件:C50+C27+C48+C39", mk(new[] { "C50", "C27", "C48", "C39" }, null)));
        list.Add(("有钱真好C44@50金", mk(new[] { "C44" }, s => s.gold = 50)));
        list.Add(("控制欲C47+3件无关卡", mk(new[] { "C47", "C01", "C52", "C21" }, null)));
        list.Add(("全牌型Lv8(无卡)", mk(new string[0], s => { foreach (var h in s.handLevels) h.level = 8; foreach (var h in s.colorLevels) h.level = 8; })));
        list.Add(("5骰全黄金面", mk(new string[0], s => AllFaces(s, f => f.isGold = true))));
        list.Add(("5骰全玻璃", mk(new string[0], s => { foreach (var d in s.dice) d.isGlass = true; })));
        list.Add(("8颗标准骰K=5", mk(new string[0], s => AddDice(s, 8))));
        // ---- p1.0.0: the long-term "built" states of the new archetypes ----
        list.Add(("[v1.0] 5骰全黄金面+金玉满堂C89", mk(new[] { "C89" }, s => AllFaces(s, f => f.isGold = true))));
        list.Add(("[v1.0] 每骰一半面黄金(点金手C117约9次提交)+C89", mk(new[] { "C89", "C117" }, s => { foreach (var d in s.dice) foreach (var f in d.faces.Where(f => f.permanentPip >= 4)) f.isGold = true; })));
        list.Add(("[v1.0] 全6骰面(潜移默化C119/拔苗助长C120成型)+始终如一C93+天地大同C32", mk(new[] { "C93", "C32" }, s => AllFaces(s, f => f.permanentPip = 6))));
        list.Add(("[v1.0] 全6骰面+C93+C32+掷地有声C67+66大顺C20", mk(new[] { "C93", "C32", "C67", "C20" }, s => AllFaces(s, f => f.permanentPip = 6))));
        list.Add(("[v1.0] 全1骰面(大事化小C121成型)+C10+C93+C32", mk(new[] { "C10", "C93", "C32" }, s => AllFaces(s, f => f.permanentPip = 1))));
        list.Add(("[v1.0] 全1骰面+C10+C93+C32+小小回声C66+小头大用C19", mk(new[] { "C10", "C93", "C32", "C66", "C19" }, s => AllFaces(s, f => f.permanentPip = 1))));
        list.Add(("[v1.0] 每骰单点(各骰点数不同1..5)+C93", mk(new[] { "C93" }, s => { int k = 1; foreach (var d in s.dice.OrderBy(d => d.createdSerial)) { foreach (var f in d.faces) f.permanentPip = k; k++; } })));
        list.Add(("[v1.0] 全5骰面(见五成群C122成型)", mk(new[] { "C122" }, s => AllFaces(s, f => f.permanentPip = 5))));
        list.Add(("[v1.0] 全5骰面+千年老二C49", mk(new[] { "C122", "C49" }, s => AllFaces(s, f => f.permanentPip = 5))));
        list.Add(("[v1.0] 全5骰面+C49+C32+C30+C31", mk(new[] { "C49", "C32", "C30", "C31" }, s => AllFaces(s, f => f.permanentPip = 5))));
        list.Add(("[v1.0] 2/3点骰面(二三其德C123成型:1,2→2;3..6→3)+2233 C11", mk(new[] { "C123", "C11" }, s => AllFaces(s, f => f.permanentPip = f.permanentPip <= 2 ? 2 : 3))));
        list.Add(("[v1.0] 2/3点骰面+C11+对对穿肠C28+好事成双C143", mk(new[] { "C11", "C28", "C143" }, s => AllFaces(s, f => f.permanentPip = f.permanentPip <= 2 ? 2 : 3))));
        list.Add(("[v1.0] 6点面权重5(灌铅C124/配重U10成型)", mk(new string[0], s => AllFaces(s, f => { if (f.permanentPip == 6) f.baseWeight = 5; }))));
        list.Add(("[v1.0] 6点面权重5+掷地有声C67+三条开泰C30+四平八稳C31", mk(new[] { "C67", "C30", "C31" }, s => AllFaces(s, f => { if (f.permanentPip == 6) f.baseWeight = 5; }))));
        list.Add(("[v1.0] 4/5/6点面权重3", mk(new string[0], s => AllFaces(s, f => { if (f.permanentPip >= 4) f.baseWeight = 3; }))));
        list.Add(("[v1.0] 5骰全玻璃+防碎膜C145", mk(new[] { "C145" }, s => { foreach (var d in s.dice) d.isGlass = true; })));
        list.Add(("[v1.0] 5骰全玻璃+C145+玻璃心C90(成长30)", () => { var s = mk(new[] { "C145" }, x => { foreach (var d in x.dice) d.isGlass = true; })(); s.controllers.Add(Ctl("C90", 7, "none", null, 30)); return s; }));
        list.Add(("[v1.0] 顺子:一条龙C142+顺水推舟C83+顺藤摸瓜C107", mk(new[] { "C142", "C83", "C107" }, null)));
        list.Add(("[v1.0] 顺子:C142+C83+C107+简单顺子C29+顺子Lv4", mk(new[] { "C142", "C83", "C107", "C29" }, s => s.handLevels.First(h => h.handId == "straight").level = 4)));
        list.Add(("[v1.0] 顺子:8骰+C142+C83+C107(更容易凑顺)", mk(new[] { "C142", "C83", "C107" }, s => AddDice(s, 8))));
        list.Add(("[v1.0] 留池:8骰K=5+观众席C147", mk(new[] { "C147" }, s => AddDice(s, 8))));
        list.Add(("[v1.0] 留池:8骰+C147+空城计C74+板凳深度C70+厚积薄发C72", mk(new[] { "C147", "C74", "C70", "C72" }, s => AddDice(s, 8))));
        list.Add(("[v1.0] 留池:8骰全6面(养精蓄锐C126成型)+C147+幕后推手C71", mk(new[] { "C147", "C126", "C71" }, s => { AddDice(s, 8); AllFaces(s, f => f.permanentPip = 6); })));
        list.Add(("[v1.0] 单骰:单刀赴会C149", mk(new[] { "C149" }, null)));
        list.Add(("[v1.0] 单骰:C149+一马当先C64+空城计C74+观众席C147", mk(new[] { "C149", "C64", "C74", "C147" }, null)));
        list.Add(("[v1.0] 单骰:全6面全黄金+C149+C64+C74+C147+C89", mk(new[] { "C149", "C64", "C74", "C147", "C89" }, s => AllFaces(s, f => { f.permanentPip = 6; f.isGold = true; }))));
        list.Add(("[v1.0] 7骰提交:7骰K=7(C22)", mk(new[] { "C22" }, s => AddDice(s, 7))));
        list.Add(("[v1.0] 8骰提交:8骰K=8(C21+C22)", mk(new[] { "C21", "C22" }, s => AddDice(s, 8))));
        list.Add(("[v1.0] 8骰提交:8骰K=8全6面(八豹子)", mk(new[] { "C21", "C22" }, s => { AddDice(s, 8); AllFaces(s, f => f.permanentPip = 6); })));
        list.Add(("[v1.0] 8骰提交:8骰K=8全6面+C93+C32+C87得寸进尺", mk(new[] { "C21", "C22", "C93", "C32", "C87" }, s => { AddDice(s, 8); AllFaces(s, f => f.permanentPip = 6); })));
        list.Add(("[v1.0] 提交顺序:红蓝相间C104+泾渭分明C105+对称美学C106", mk(new[] { "C104", "C105", "C106" }, null)));
        list.Add(("[v1.0] 提交顺序:排头兵C108+中流砥柱C109+一马当先C64+压轴好戏C65", mk(new[] { "C108", "C109", "C64", "C65" }, null)));
        list.Add(("[v1.0] 偶数:全偶数面(2/4/6)+偶数为王C144", mk(new[] { "C144" }, s => AllFaces(s, f => { if (f.permanentPip % 2 == 1) f.permanentPip++; }))));
        list.Add(("[v1.0] 红组:全红骰面+红运当头C25+锦上添红C146+红色共鸣C69", mk(new[] { "C25", "C146", "C69" }, s => AllFaces(s, f => f.baseColor = "red"))));
        list.Add(("[v1.0] 节奏:渐入佳境C150+开门红C153+见好就收C148", mk(new[] { "C150", "C153", "C148" }, null)));
        list.Add(("[v1.0] 构筑:留白C151(其余4栏空)", mk(new[] { "C151" }, null)));
        list.Add(("[v1.0] 构筑:草根逆袭C152+4张普通卡(C50,C27,C48,C39)", mk(new[] { "C152", "C50", "C27", "C48", "C39" }, null)));
        list.Add(("[v1.0] 金币:富甲一方C139@80金+有钱真好C44", mk(new[] { "C139", "C44" }, s => s.gold = 80)));
        list.Add(("[v1.0] 临时骰:雇佣兵C136(1颗临时骰)+客串明星C137", mk(new[] { "C136", "C137" }, s => s.dice.Add(DiceFactory.CreateStandard(cfg, s.nextDieSerial++, "stage_temporary", "")))));
        list.Add(("[v1.0] 首领关:逆流而上C130+屠龙者C129(成长30)", () => { var s = mk(new[] { "C130" }, null)(); s.controllers.Add(Ctl("C129", 7, "none", null, 30)); return s; }));
        var rows = new ConcurrentDictionary<int, string>(); var data = new ConcurrentDictionary<int, object>();
        // Which stage's normal target the scenario's expected stage total reaches (0 = none).
        Func<double, int> stageReached = total => { int k = 0; for (int i = 0; i < cfg.stages.Length; i++) if (total >= double.Parse(cfg.stages[i].normalTarget)) k = i + 1; return k; };
        Parallel.For(0, list.Count, Par, i =>
        {
            var state = list[i].make(); var ctx = list[i].name.Contains("首领关") ? new StageCtx { boss = true } : null;
            var s = Measure(state, hands, 99, 0, 0, ctx);
            rows[i] = $"{list[i].name,-40}\tE[hand]={s.mean,12:F0}\tmedian={s.median,12:F0}\tp10={s.p10,10:F0}\tp90={s.p90,12:F0}\tdice={s.dice:F1}\tstage total={s.stage,12:F0} (reaches target of stage {stageReached(s.stage)})\t" + string.Join(" ", Enumerable.Range(0, 13).Where(k => s.prim[k] * 20 >= hands).Select(k => P.hId[k] + "=" + (100.0 * s.prim[k] / hands).ToString("F0") + "%"));
            data[i] = new { name = list[i].name, mean = s.mean, median = s.median, p10 = s.p10, p90 = s.p90, dice = s.dice, submissions = s.subs, stageTotal = s.stage, reachesStageTarget = stageReached(s.stage) };
        });
        for (int i = 0; i < list.Count; i++) Console.WriteLine(rows[i]);
        // Starter odds against the first targets.
        Console.WriteLine("\n-- 裸装三手合计达标概率 --");
        foreach (var stage in cfg.stages.Take(4)) { var s = Measure(Fresh(), hands * 3, 7, 0, double.Parse(stage.normalTarget)); Console.WriteLine($"{stage.stageId} target={stage.normalTarget}: P(clear with no controllers)={s.stage3 * 100:F1}%"); }
        if (outPath != null) File.WriteAllText(outPath, JsonSerializer.Serialize(Enumerable.Range(0, list.Count).Select(i => data[i]), new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        return 0;
    }

    // ------------------------------------------------------------------ probe
    /// <summary>Small hand-built cases against the real rules core for behaviour that looked wrong while the
    /// simulator was upgraded. Each prints what the core does next to what the spec (docs/specs/v10/RULES_SPEC.md)
    /// says; nothing here feeds the other modes.</summary>
    static int Probe()
    {
        int findings = 0;
        // 1. C43 under ZeroLevels (B28 / B35) with the primary hand already at the level cap.
        {
            var st = Fresh(); st.controllers.Clear(); st.controllers.Add(Ctl("C43", 1));
            foreach (var hl in st.handLevels) hl.level = hl.handId == "pair" ? cfg.maxHandLevel : 0;
            var dice = st.dice.OrderBy(d => d.createdSerial).Take(2).ToArray();
            Func<string, SubmissionSnapshot> snap = modifier => new SubmissionSnapshot
            {
                runId = "p", stageId = "STAGE_03", handId = "H", configVersion = cfg.configVersion, capacity = 5, rollsRemaining = 0, submissionOrdinalBefore = 1, wallet = 0, previousPrimaryHandId = "pair",
                equippedControllers = st.controllers.Select(c => c.Clone()).ToArray(), handLevels = st.handLevels.Select(x => x.Clone()).ToArray(), colorLevels = st.colorLevels.Select(x => x.Clone()).ToArray(), mimicBindings = null,
                rollsUsed = 3, submissionsRemainingBefore = 2, permanentDiceCount = 5, stageKind = "boss", modifierId = modifier, ordinaryControllerSlots = 5,
                dice = dice.Select(d => new SubmissionDie { dieId = d.dieId, faceId = "F06", currentPip = 6, currentColor = d.faces.First(f => f.faceId == "F06").baseColor, permanentSixFaceSum = d.faces.Sum(f => f.permanentPip), lifetime = d.lifetime, permanentFacesCopy = d.faces.Select(f => f.Clone()).ToArray() }).ToArray()
            };
            var plain = ScoreCalculator.Evaluate(snap(""), cfg).afterScorePlan; var zero = ScoreCalculator.Evaluate(snap("B28"), cfg).afterScorePlan;
            bool wrong = zero.upgradeApplied > 0;
            Console.WriteLine($"[1] C43 with pair at level {cfg.maxHandLevel} (the cap), previous primary = pair, submitting a pair:");
            Console.WriteLine($"    without modifier: upgradeRequested={plain.upgradeRequested} upgradeApplied={plain.upgradeApplied} (capped at the level cap, as in v0.9)");
            Console.WriteLine($"    with B28 ZeroLevels: upgradeRequested={zero.upgradeRequested} upgradeApplied={zero.upgradeApplied}");
            Console.WriteLine(wrong
                ? "    FINDING: under ZeroLevels the cap is measured from level 0, so the plan raises a level-8 hand to 9. GameSession.ApplySubmissionEffects adds it unchecked and ValidateState then rejects the whole Submit command (hand level out of range): the player cannot submit that hand. Spec 3.1 says ZeroLevels only makes hands and colours *count as* level 0 for scoring."
                : "    ok: the applied upgrade respects the real level cap.");
            if (wrong) findings++;
            // The same through the real session, when the core allows building it: left to the rules tests.
        }
        Console.WriteLine($"probe: {findings} finding(s)");
        return 0;
    }

    // ------------------------------------------------------------------ trace
    /// <summary>Records a prefix for the historical tour, or all commands through the terminal phase for release verification.</summary>
    static int Trace(uint seed, int steps, string outPath, bool full)
    {
        var runner = new Runner(cfg, P, new BotOptions()) { recorded = new List<Dictionary<string, object>>() };
        var log = runner.Play(seed);
        if ((log.fail ?? "").StartsWith("ERROR") || log.shopRejected != 0 || log.rejected != 0 || log.mispredicted != 0 || log.quoteMismatch != 0)
        {
            Console.WriteLine($"seed {seed}: trace run failed diagnostics: {log.fail}; shopRejected={log.shopRejected}; optionalRejected={log.rejected}; scoreMismatches={log.mispredicted}; quoteMismatches={log.quoteMismatch}");
            return 1;
        }
        var all = runner.recorded; var head = full ? all : all.Take(steps + 1).ToList();
        if (!full && head.Count <= steps) { Console.WriteLine($"seed {seed}: run ended after {all.Count - 1} commands, fewer than {steps}"); return 3; }
        if (!full && head.Any(s => (string)s["kind"] == "ReorderControllers")) { Console.WriteLine($"seed {seed}: opening uses ReorderControllers, pick another seed"); return 4; }
        var replay = new GameSession(cfg, new MemorySaveStore()); int lootDecisions = 0, extraLootPicks = 0;
        var replayOptions = new JsonSerializerOptions { IncludeFields = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
        for (int i = 0; i < head.Count; i++)
        {
            var command = JsonSerializer.Deserialize<SessionCommand>(JsonSerializer.Serialize(head[i]), replayOptions);
            command.commandId = "trace-" + i; command.expectedActionSequence = replay.ActionSequence;
            var state = replay.State;
            var offer = command.kind == CommandKind.Buy ? state.shop?.offers.FirstOrDefault(o => o.offerId == command.offerId) : null;
            if (offer?.groupId == "LOOT") { if (replay.IsLootPending) lootDecisions++; else extraLootPicks++; }
            if (command.kind == CommandKind.ConfirmLocks)
                foreach (string id in command.dieIds) if (!replay.TogglePending(id)) throw new InvalidOperationException("Trace replay has an illegal pending die: " + id);
            var result = replay.Execute(command);
            if (!result.accepted) throw new InvalidOperationException($"Trace replay rejected step {i} {command.kind}: {result.error}");
        }
        Console.WriteLine($"trace replay: accepted={head.Count}, rejected=0, initialLootDecisions={lootDecisions}, extraLootPicks={extraLootPicks}; full-run shopRejected={log.shopRejected}, scoreMismatches={log.mispredicted}");
        var trace = new Dictionary<string, object> { ["seed"] = seed, ["configVersion"] = cfg.configVersion, ["policy"] = "balance_sim bot, legal commands only", ["steps"] = head };
        if (full)
        {
            if (replay.State?.phase != "RunWon" && replay.State?.phase != "RunFailed")
                throw new InvalidOperationException("Full trace did not reach a terminal phase");
            trace["terminalPhase"] = replay.State.phase;
        }
        File.WriteAllText(outPath, JsonSerializer.Serialize(trace,
            new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Console.WriteLine($"seed {seed}: wrote {head.Count} steps ({string.Join(",", head.GroupBy(s => (string)s["kind"]).Select(g => g.Key + "=" + g.Count()))}); run reached stage {log.reached}, won={log.won}");
        return 0;
    }

    // ------------------------------------------------------------------ runs
    static double Median(IEnumerable<double> xs) { var a = xs.OrderBy(x => x).ToArray(); return a.Length == 0 ? double.NaN : a[a.Length / 2]; }
    static double Pct(int part, int whole) => whole == 0 ? 0 : 100.0 * part / whole;
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
        Console.WriteLine($"\nruns={ok.Length} errors={seeds - ok.Length} wins={ok.Count(l => l.won)} ({winRate:F1}%) bot={(weak ? "weak" : Runner.Prepared ? "planner+prepared" : "planner")} seeds={first}..{first + (uint)seeds - 1} time={sw.Elapsed.TotalSeconds:F0}s");
        int predicted = logs.Sum(l => l.predicted), mispredicted = logs.Sum(l => l.mispredicted), rejected = logs.Sum(l => l.rejected), quoteMismatch = logs.Sum(l => l.quoteMismatch);
        int shopRejected = logs.Sum(l => l.shopRejected);
        Console.WriteLine($"planner self-check: {predicted} real submissions predicted (builds without C53), {mispredicted} scored differently by the session" + (mispredicted > 0 ? "; first: " + logs.First(l => l.mispredicted > 0).firstMisprediction : ""));
        Console.WriteLine($"optional commands refused by the session: {rejected}" + (rejected > 0 ? "; first: " + logs.First(l => l.rejected > 0).firstRejection : ""));
        Console.WriteLine($"shop commands refused by the session: {shopRejected}" + (shopRejected > 0 ? "; first: " + logs.First(l => l.shopRejected > 0).firstShopRejection : ""));
        Console.WriteLine($"stage previews whose quoted budgets differ from the rules: {quoteMismatch}" + (quoteMismatch > 0 ? "; first: " + logs.First(l => l.quoteMismatch > 0).firstQuoteMismatch : ""));
        Console.WriteLine("slowest runs: " + string.Join(" ", ok.OrderByDescending(l => l.seconds).Take(5).Select(l => $"{l.seed}:{l.seconds:F0}s")) + $"  sum of run times={ok.Sum(l => l.seconds):F0}s (shop {ok.Sum(l => l.secShop):F0}s, stage select {ok.Sum(l => l.secPreview):F0}s, hands {ok.Sum(l => l.secHands):F0}s; {ok.Sum(l => (long)l.powerCalls)} power estimates took {ok.Sum(l => l.secPower):F0}s)");
        Console.WriteLine("power estimates by purpose: " + string.Join("  ", ok.SelectMany(l => l.powerSecondsBy.Keys).Distinct().OrderBy(k => k).Select(k => $"{k}: {ok.Sum(l => l.powerCallsBy.GetValueOrDefault(k))} calls {ok.Sum(l => l.powerSecondsBy.GetValueOrDefault(k)):F0}s")));
        Console.WriteLine("stage\tkind\ttarget\treach%\tclear%\tsubs\tscore/target(med,cleared)\tpower/target(med)\tgoldStart\tincome(reward+unused+interest+inStage+ctl)\tspent\tgoldLeave\tctrl\tslots\tdice\trisk%");
        var stageRows = new List<object>(); var clearByStage = new double[cfg.stages.Length + 1]; var powerByStage = new double[cfg.stages.Length + 1];
        var all = ok.SelectMany(l => l.stages).ToArray();
        for (int k = 1; k <= cfg.stages.Length; k++)
        {
            var at = all.Where(s => s.stage == k).ToArray(); if (at.Length == 0) break;
            var cl = at.Where(s => s.cleared).ToArray();
            double reach = 100.0 * at.Length / ok.Length, clear = 100.0 * cl.Length / at.Length;
            var row = new
            {
                stage = k, kind = cfg.stages[k - 1].stageKind, target = cfg.stages[k - 1].normalTarget, reach, clear, subs = cl.Length == 0 ? 0 : cl.Average(s => s.subsUsed),
                overkill = Median(cl.Select(s => s.score / s.target)), power = Median(at.Select(s => s.power / s.target)), safePower = Median(at.Select(s => s.safe / s.target)),
                powerRaw = Median(at.Select(s => s.powerRaw / s.targetBase)), safePowerRaw = Median(at.Select(s => s.safeRaw / s.targetBase)),
                goldStart = at.Average(s => s.goldStart), reward = cl.Length == 0 ? 0 : cl.Average(s => s.reward), unused = cl.Length == 0 ? 0 : cl.Average(s => s.unused),
                interest = cl.Length == 0 ? 0 : cl.Average(s => s.interest), pig = cl.Length == 0 ? 0 : cl.Average(s => s.pig), ctlIncome = cl.Length == 0 ? 0 : cl.Average(s => s.ctlIncome), spent = cl.Length == 0 ? 0 : cl.Average(s => s.spent),
                goldLeave = cl.Length == 0 ? 0 : cl.Average(s => s.goldAfterShop), ctrl = at.Average(s => s.ctrl), slots = at.Average(s => s.slots), dice = at.Average(s => s.dice), risk = 100.0 * at.Count(s => s.risk) / at.Length
            };
            stageRows.Add(row); clearByStage[k] = clear; powerByStage[k] = row.power;
            Console.WriteLine($"{k}\t{row.kind}\t{row.target}\t{reach:F0}\t{clear:F0}\t{row.subs:F2}\t{row.overkill:F2}\t{row.power:F2}\t{row.goldStart:F1}\t{row.reward:F1}+{row.unused:F1}+{row.interest:F1}+{row.pig:F1}+{row.ctlIncome:F1}\t{row.spent:F1}\t{row.goldLeave:F1}\t{row.ctrl:F1}\t{row.slots:F1}\t{row.dice:F1}\t{row.risk:F0}");
        }

        // ---- stage kinds, pressure challenge ----
        Console.WriteLine("\nclear rate by stage kind (stages reached / cleared):");
        var kindRows = new List<object>();
        foreach (string kind in new[] { "normal", "pressure", "boss" })
        {
            var at = all.Where(s => s.kind == kind).ToArray(); int cl = at.Count(s => s.cleared);
            Console.WriteLine($"  {kind,-9} reached {at.Length,5}  cleared {cl,5}  {Pct(cl, at.Length):F1}%");
            kindRows.Add(new { kind, reached = at.Length, cleared = cl, clearPct = Pct(cl, at.Length) });
        }
        var offered = all.Where(s => s.riskOffered).ToArray(); var taken = offered.Where(s => s.risk).ToArray(); var declined = offered.Where(s => !s.risk).ToArray();
        Console.WriteLine($"pressure challenge (x1.5 target, reward {cfg.riskStageReward}): offered {offered.Length}, accepted {taken.Length} ({Pct(taken.Length, offered.Length):F1}%), cleared when accepted {Pct(taken.Count(s => s.cleared), taken.Length):F1}%, cleared when declined {Pct(declined.Count(s => s.cleared), declined.Length):F1}%");
        Console.WriteLine("  accepted % by stage: " + string.Join(" ", offered.GroupBy(s => s.stage).OrderBy(g => g.Key).Select(g => g.Key + ":" + Pct(g.Count(s => s.risk), g.Count()).ToString("F0"))));
        var pressure = new { offered = offered.Length, accepted = taken.Length, acceptedPct = Pct(taken.Length, offered.Length), clearedWhenAcceptedPct = Pct(taken.Count(s => s.cleared), taken.Length), clearedWhenDeclinedPct = Pct(declined.Count(s => s.cleared), declined.Length),
            byStage = offered.GroupBy(s => s.stage).OrderBy(g => g.Key).Select(g => new { stage = g.Key, offered = g.Count(), accepted = g.Count(s => s.risk), clearedWhenAccepted = g.Count(s => s.risk && s.cleared) }).ToArray() };

        // ---- boss modifiers ----
        Console.WriteLine("\nboss modifiers (faced = boss stages entered with it; lifted = by C131 or I08; clear% counts every stage faced; power = median estimate / target with the modifier in force; raw = without it):");
        Console.WriteLine("id\tname\ttier\tfaced\tlifted\tclear%\tpower/target\traw power/target\tloss%");
        var bossRows = new List<object>();
        foreach (var mdef in cfg.bossModifiers)
        {
            var at = all.Where(s => s.modifier == mdef.id).ToArray(); if (at.Length == 0) { bossRows.Add(new { id = mdef.id, name = mdef.displayName, tier = mdef.tier, effects = mdef.effects, faced = 0, lifted = 0, cleared = 0, clearPct = double.NaN, powerOverTarget = double.NaN, rawPowerOverTarget = double.NaN, lossPct = double.NaN }); continue; }
            int cl = at.Count(s => s.cleared), lifted = at.Count(s => s.nullifiedBy != null);
            double pm = Median(at.Select(s => s.powerMod / s.targetMod)), pr = Median(at.Select(s => s.powerRaw / s.targetBase)), loss = Median(at.Select(s => 100 * (1 - s.powerMod / s.targetMod / (s.powerRaw / s.targetBase))));
            Console.WriteLine($"{mdef.id}\t{mdef.displayName}\t{mdef.tier}\t{at.Length}\t{lifted}\t{Pct(cl, at.Length):F0}\t{pm:F2}\t{pr:F2}\t{loss:F0}");
            bossRows.Add(new { id = mdef.id, name = mdef.displayName, tier = mdef.tier, effects = mdef.effects, faced = at.Length, lifted, cleared = cl, clearPct = Pct(cl, at.Length), powerOverTarget = pm, rawPowerOverTarget = pr, lossPct = loss });
        }
        // ---- --lab: every modifier of the cycle's pool on the same builds ----
        var labRows = new List<object>();
        if (all.Any(s => s.lab != null))
        {
            Console.WriteLine("\nmodifier lab (the build that reached each boss stage, valued under every modifier its cycle could have drawn; loss = share of power taken away; ordered = after moving the least-missed controllers left):");
            Console.WriteLine("id\tname\ttier\tbuilds\tloss% median\tp25\tp75\tshare of builds losing >=50%\tordered loss% median");
            double Q(IEnumerable<double> v, double q) { var a = v.OrderBy(x => x).ToArray(); return a.Length == 0 ? double.NaN : a[(int)Math.Min(a.Length - 1, Math.Floor(q * a.Length))]; }
            foreach (var mdef in cfg.bossModifiers)
            {
                var v = all.Where(s => s.lab != null && s.lab.ContainsKey(mdef.id)).Select(s => 100 * s.lab[mdef.id]).ToArray(); if (v.Length == 0) continue;
                var o = all.Where(s => s.labOrdered != null && s.labOrdered.ContainsKey(mdef.id)).Select(s => 100 * s.labOrdered[mdef.id]).ToArray();
                double med = Median(v), p25 = Q(v, 0.25), p75 = Q(v, 0.75), heavy = Pct(v.Count(x => x >= 50), v.Length), ordered = o.Length == 0 ? double.NaN : Median(o);
                Console.WriteLine($"{mdef.id}\t{mdef.displayName}\t{mdef.tier}\t{v.Length}\t{med:F0}\t{p25:F0}\t{p75:F0}\t{heavy:F0}\t{(o.Length == 0 ? "-" : ordered.ToString("F0"))}");
                labRows.Add(new { id = mdef.id, name = mdef.displayName, tier = mdef.tier, builds = v.Length, lossMedianPct = med, lossP25Pct = p25, lossP75Pct = p75, heavyPct = heavy, orderedLossMedianPct = ordered });
            }
        }
        var never = cfg.bossModifiers.Where(mdef => !all.Any(s => s.modifier == mdef.id)).Select(mdef => mdef.id).ToArray();
        if (never.Length > 0) Console.WriteLine("never faced: " + string.Join(" ", never));
        Console.WriteLine("by tier: " + string.Join("  ", cfg.bossModifiers.GroupBy(mdef => mdef.tier).OrderBy(g => g.Key).Select(g => { var at = all.Where(s => s.modifier != null && g.Any(mdef => mdef.id == s.modifier)).ToArray(); return $"tier {g.Key}: faced {at.Length}, clear {Pct(at.Count(s => s.cleared), at.Length):F1}%"; })));

        // ---- loot and gifts ----
        Console.WriteLine("\nboss loot (offered = shops with that loot kind; picks = offers taken; gold = took the gold option; none = took nothing):");
        var lootRows = new List<object>();
        foreach (var kind in cfg.lootKinds)
        {
            var at = all.Where(s => s.lootKind == kind.id).ToArray(); int gold = at.Count(s => s.lootTaken.Contains("GOLD")), none = at.Count(s => s.lootTaken.Count == 0);
            var picks = at.SelectMany(s => s.lootTaken).Where(x => x != "GOLD").Select(x => x.Split('(')[0]).GroupBy(x => x).OrderByDescending(g => g.Count()).ToArray();
            Console.WriteLine($"  {kind.id} {kind.displayName}\toffered {at.Length}\ttook offers in {at.Length - gold - none} ({Pct(at.Length - gold - none, at.Length):F0}%)\tgold {gold} ({Pct(gold, at.Length):F0}%)\tnone {none}\tpicks: " + string.Join(" ", picks.Take(12).Select(g => g.Key + "x" + g.Count())));
            int picksTaken = at.Sum(s => s.lootPicks), picksAvailable = at.Sum(s => s.lootPicksAvailable);
            var pickCountHistogram = at.GroupBy(s => s.lootPicks).OrderBy(g => g.Key).ToDictionary(g => g.Key.ToString(), g => g.Count());
            Console.WriteLine($"    picks taken {picksTaken}/{picksAvailable}; per-shop counts: " + string.Join(" ", pickCountHistogram.Select(kv => kv.Key + ":" + kv.Value)));
            lootRows.Add(new { id = kind.id, name = kind.displayName, offered = at.Length, tookOffers = at.Length - gold - none, tookGold = gold, tookNothing = none, picks = picks.ToDictionary(g => g.Key, g => g.Count()),
                picksTaken, picksAvailable, pickCountHistogram, decisions = at.GroupBy(s => s.lootDecision ?? "none").ToDictionary(g => g.Key, g => g.Count()) });
        }
        var giftPicks = all.SelectMany(s => s.gifts).GroupBy(x => x).OrderByDescending(g => g.Count()).ToArray();
        Console.WriteLine("controller gifts taken (C111 items, C113 die upgrades): " + (giftPicks.Length == 0 ? "none" : string.Join(" ", giftPicks.Select(g => g.Key + "x" + g.Count()))));
        var itemUse = all.SelectMany(s => s.items).GroupBy(x => x).OrderBy(g => g.Key).ToArray();
        Console.WriteLine("items used per run: " + string.Join(" ", itemUse.Select(g => $"{g.Key}:{g.Count() / (double)ok.Length:F2}")));

        // ---- purchases and per-card statistics ----
        var bought = new Dictionary<string, List<int>>(); var endWin = new Dictionary<string, int>(); var endAll = new Dictionary<string, int>();
        var shown = new Dictionary<string, int>(); var looted = new Dictionary<string, int>(); var granted = new Dictionary<string, int>();
        foreach (var l in ok)
        {
            foreach (var s in l.stages)
            {
                foreach (string b in s.bought) { string id = b.Split(':', '(')[0]; if (!bought.TryGetValue(id, out var li)) bought[id] = li = new List<int>(); li.Add(s.stage); }
                foreach (string c in s.offered) shown[c] = shown.GetValueOrDefault(c) + 1;
                foreach (string c in s.lootTaken.Where(x => x.StartsWith("C"))) { string id = c.Split(':', '(')[0]; looted[id] = looted.GetValueOrDefault(id) + 1; }
                foreach (string c in s.granted) granted[c] = granted.GetValueOrDefault(c) + 1;
            }
            var last = l.stages.LastOrDefault(); if (last == null) continue;
            foreach (string c in last.controllers.Select(x => x.Split(':')[0]).Distinct()) { endAll[c] = endAll.GetValueOrDefault(c) + 1; if (l.won) endWin[c] = endWin.GetValueOrDefault(c) + 1; }
        }
        // Acquisition events count only copies added while another instance is still held. Final copies use the
        // actual terminal state, since cards may be destroyed after the last stage's entry snapshot.
        var duplicateShelf = all.SelectMany(s => s.duplicateBought).GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
        var duplicateLoot = all.SelectMany(s => s.duplicateLoot).GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
        int shelfCopies = duplicateShelf.Values.Sum(), lootCopies = duplicateLoot.Values.Sum();
        int shelfAcquired = bought.Where(kv => cfg.controllers.Any(c => c.id == kv.Key)).Sum(kv => kv.Value.Count), lootAcquired = looted.Values.Sum();
        object TerminalCopies(string outcome, RunLog[] at)
        {
            int total = at.Sum(l => l.finalCopies.Values.Sum()), extra = at.Sum(l => l.finalCopies.Values.Sum(n => Math.Max(0, n - 1)));
            int withDuplicates = at.Count(l => l.finalCopies.Values.Any(n => n > 1));
            Console.WriteLine($"  terminal {outcome}: runs={at.Length} total copies={total} extra copies={extra} runs with duplicates={withDuplicates}");
            return new { runs = at.Length, totalCopies = total, extraCopies = extra, runsWithDuplicates = withDuplicates,
                copiesPerRun = at.Length == 0 ? 0 : total / (double)at.Length, extraCopiesPerRun = at.Length == 0 ? 0 : extra / (double)at.Length,
                extraCopiesHistogram = at.GroupBy(l => l.finalCopies.Values.Sum(n => Math.Max(0, n - 1))).OrderBy(g => g.Key).ToDictionary(g => g.Key.ToString(), g => g.Count()),
                maxSameNameCopiesHistogram = at.GroupBy(l => l.finalCopies.Values.DefaultIfEmpty(0).Max()).OrderBy(g => g.Key).ToDictionary(g => g.Key.ToString(), g => g.Count()) };
        }
        Console.WriteLine($"\nduplicate acquisitions: shelf={shelfCopies}/{shelfAcquired} ({Pct(shelfCopies, shelfAcquired):F2}%), loot={lootCopies}/{lootAcquired} ({Pct(lootCopies, lootAcquired):F2}%)");
        var terminalDeath = TerminalCopies("death", ok.Where(l => !l.won).ToArray()); var terminalWin = TerminalCopies("win", ok.Where(l => l.won).ToArray());
        var duplicateCards = cfg.controllers.Select(c => new {
            id = c.id, name = c.displayName, shelf = duplicateShelf.GetValueOrDefault(c.id), loot = duplicateLoot.GetValueOrDefault(c.id),
            terminalCopiesAtDeath = ok.Where(l => !l.won).Sum(l => l.finalCopies.GetValueOrDefault(c.id)),
            terminalCopiesAtWin = ok.Where(l => l.won).Sum(l => l.finalCopies.GetValueOrDefault(c.id)),
            terminalExtraCopies = ok.Sum(l => Math.Max(0, l.finalCopies.GetValueOrDefault(c.id) - 1)),
            terminalRunsWithDuplicates = ok.Count(l => l.finalCopies.GetValueOrDefault(c.id) > 1),
            maxTerminalCopies = ok.Select(l => l.finalCopies.GetValueOrDefault(c.id)).DefaultIfEmpty(0).Max()
        }).OrderByDescending(c => c.shelf + c.loot).ThenByDescending(c => c.terminalExtraCopies).ThenBy(c => c.id).ToArray();
        Console.WriteLine("most duplicated cards (shelf+loot acquisitions): " + string.Join(" ", duplicateCards.Where(c => c.shelf + c.loot > 0).Take(15).Select(c => $"{c.id}:{c.shelf}+{c.loot}")));
        var duplicates = new { shelf = new { duplicateCopies = shelfCopies, acquiredCopies = shelfAcquired, pct = Pct(shelfCopies, shelfAcquired) },
            loot = new { duplicateCopies = lootCopies, acquiredCopies = lootAcquired, pct = Pct(lootCopies, lootAcquired) }, atDeath = terminalDeath, atWin = terminalWin, cards = duplicateCards };
        int wins = Math.Max(1, ok.Count(l => l.won));
        Console.WriteLine("\nother purchases\tperRun\tavgStage");
        var itemRows = new List<object>();
        foreach (var kv in bought.Where(x => !x.Key.StartsWith("C") || x.Key.Length < 3 || !char.IsDigit(x.Key[1])).OrderByDescending(x => x.Value.Count))
        {
            Console.WriteLine($"{kv.Key}\t{kv.Value.Count / (double)ok.Length:F2}\t{kv.Value.Average():F1}");
            itemRows.Add(new { id = kv.Key, name = "", perRun = kv.Value.Count / (double)ok.Length, avgStage = kv.Value.Average(), finalPct = 0.0, winPct = 0.0 });
        }
        Console.WriteLine("\ncontroller\trarity\tprice\tshownPerRun\tboughtPerRun\tbuy%ofShown\tlootPerRun\tgrantedPerRun\tavgStage\tinFinalBuild%\tinWinningBuild%");
        var cardRows = new List<object>();
        foreach (var c in cfg.controllers.OrderByDescending(c => (bought.TryGetValue(c.id, out var li) ? li.Count : 0) + looted.GetValueOrDefault(c.id)))
        {
            int nb = bought.TryGetValue(c.id, out var stagesBought) ? stagesBought.Count : 0, ns = shown.GetValueOrDefault(c.id), nl = looted.GetValueOrDefault(c.id), ng = granted.GetValueOrDefault(c.id);
            double fin = 100.0 * endAll.GetValueOrDefault(c.id) / ok.Length, win = 100.0 * endWin.GetValueOrDefault(c.id) / wins, avg = nb == 0 ? double.NaN : stagesBought.Average();
            Console.WriteLine($"{c.id} {c.displayName}\t{c.rarity}\t{c.buyPrice}\t{ns / (double)ok.Length:F2}\t{nb / (double)ok.Length:F2}\t{Pct(nb, ns):F0}\t{nl / (double)ok.Length:F2}\t{ng / (double)ok.Length:F2}\t{avg:F1}\t{fin:F0}\t{win:F0}");
            var row = new { id = c.id, name = c.displayName, rarity = c.rarity, price = c.buyPrice, generation = Generation(Num(c.id)), shownPerRun = ns / (double)ok.Length, perRun = nb / (double)ok.Length, buyPctOfShown = Pct(nb, ns), lootPerRun = nl / (double)ok.Length, grantedPerRun = ng / (double)ok.Length, avgStage = avg, finalPct = fin, winPct = win };
            cardRows.Add(row); if (nb > 0) itemRows.Add(new { id = c.id, name = c.displayName, perRun = row.perRun, avgStage = avg, finalPct = fin, winPct = win });
        }
        var neverBought = cfg.controllers.Select(c => c.id).Where(id => !bought.ContainsKey(id)).ToArray();
        Console.WriteLine("\ncontrollers never bought: " + string.Join(" ", neverBought));
        Console.WriteLine("\nmost present in winning builds (share of winning runs holding it at the last stage):");
        Func<string, string> nameOf = id => cfg.controllers.FirstOrDefault(c => c.id == id)?.displayName ?? "";
        var winRows = endWin.OrderByDescending(x => x.Value).Take(20).Select(x => new { id = x.Key, name = nameOf(x.Key), winPct = 100.0 * x.Value / wins }).ToArray();
        Console.WriteLine("  " + string.Join("  ", winRows.Select(r => $"{r.id} {r.name} {r.winPct:F0}%")));

        // ---- deaths ----
        var deathList = ok.Where(l => !l.won).GroupBy(l => l.reached).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count());
        Console.WriteLine("\ndeaths by stage: " + string.Join(" ", deathList.Select(g => g.Key + ":" + g.Value)));
        int deathsTotal = Math.Max(1, deathList.Values.Sum()); int blocks = (cfg.stages.Length + 3) / 4;
        var blockShare = Enumerable.Range(0, blocks).Select(bk => 100.0 * deathList.Where(g => (g.Key - 1) / 4 == bk).Sum(g => g.Value) / deathsTotal).ToArray();
        Console.WriteLine("death share by four-stage block (% of all deaths): " + string.Join("  ", blockShare.Select((x, bk) => $"{bk * 4 + 1}-{bk * 4 + 4}: {x:F0}%")));
        Console.WriteLine("death share by four-stage block (% of all runs):   " + string.Join("  ", blockShare.Select((x, bk) => $"{bk * 4 + 1}-{bk * 4 + 4}: {x * deathsTotal / ok.Length:F0}%")));
        var deathsByKind = ok.Where(l => !l.won).GroupBy(l => cfg.stages[Math.Max(1, l.reached) - 1].stageKind).ToDictionary(g => g.Key, g => g.Count());
        Console.WriteLine("deaths by stage kind: " + string.Join(" ", deathsByKind.Select(g => g.Key + ":" + g.Value)));
        int lastStage = cfg.stages.Length; var final = all.Where(s => s.stage == lastStage).ToArray();
        double lastRaw = Median(final.Select(s => s.powerRaw / s.targetBase)), lastMod = Median(final.Select(s => s.powerMod / Math.Max(1, s.targetMod)));
        Console.WriteLine($"stage {lastStage}: median power/target before the final-boss modifier {lastRaw:F2}, with it {lastMod:F2}   clear% stages 1-{lastStage}: " + string.Join(" ", Enumerable.Range(1, lastStage).Select(k => clearByStage[k].ToString("F0"))));
        Console.WriteLine("fail reasons: " + string.Join(" ", ok.Where(l => !l.won).GroupBy(l => l.fail ?? "?").Select(g => g.Key + ":" + g.Count())));
        if (outPath != null)
        {
            var summary = new { winRatePct = winRate, deathsByStage = deathList.ToDictionary(g => g.Key.ToString(), g => g.Value), deathShareByBlockPct = blockShare, deathsByStageKind = deathsByKind,
                lastStagePowerOverTargetMedian = lastRaw, lastStagePowerOverTargetWithModifierMedian = lastMod, clearPctByStage = clearByStage.Skip(1).ToArray(),
                targets = cfg.stages.Select(s => s.normalTarget).ToArray(), stageKinds = cfg.stages.Select(s => s.stageKind).ToArray(), riskTargetFactor = 1.5 };
            File.WriteAllText(outPath, JsonSerializer.Serialize(new { config = cfg.configVersion, configPath, bot = weak ? "weak" : Runner.Prepared ? "planner+prepared" : "planner", firstSeed = first, seeds, runs = ok.Length, errors = seeds - ok.Length, wins = ok.Count(l => l.won),
                plannerSelfCheck = new { predicted, mispredicted }, rejectedOptionalCommands = rejected, rejectedShopCommands = shopRejected,
                shopRejections = logs.SelectMany(l => l.shopRejections.Select(reason => new { l.seed, reason })).ToArray(), quoteMismatches = quoteMismatch, summary, stages = stageRows, stageKinds = kindRows, pressure, bossModifiers = bossRows, bossLab = labRows, loot = lootRows, duplicates,
                gifts = giftPicks.ToDictionary(g => g.Key, g => g.Count()), itemsUsedPerRun = itemUse.ToDictionary(g => g.Key, g => g.Count() / (double)ok.Length), items = itemRows, cards = cardRows, winningBuilds = winRows, neverBought, logs }, Pretty));
        }
        return seeds == ok.Length && mispredicted == 0 && rejected == 0 && shopRejected == 0 && quoteMismatch == 0 ? 0 : 1;
    }
}
