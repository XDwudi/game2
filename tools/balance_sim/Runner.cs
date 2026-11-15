// Full-run bot. Drives the real GameSession with legal commands only; it sees what a player sees
// (current faces, shop offers, prices, the cycle panel) and never reads RNG state. Planning uses its own random stream.
// p1.0.0: three-stage cycles (pressure choice with the x1.5 target, boss modifiers), gift groups (boss loot,
// C111 / C113), the new items, die upgrades and services. Heuristics are documented in README.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DiceCraft.Core;

public sealed class StageLog
{
    public int stage, subsUsed, unusedSubs, dice, ctrl, slots, itemsUsed;
    /// <summary>normal / pressure / boss.</summary>
    public string kind;
    public bool risk, riskOffered, cleared;
    /// <summary>target = the target actually played (pressure x1.5, TargetPercent, after a lifted modifier);
    /// targetBase = the stage's normal target. power / safe = the bot's estimate for the stage as it was entered
    /// (modifier in force unless lifted); powerRaw / safeRaw = the same build without any modifier;
    /// powerMod / safeMod / targetMod = with the boss modifier in force (boss stages only, also when it was lifted).</summary>
    public double target, targetBase, score, power, safe, powerRaw, safeRaw, powerMod, safeMod, targetMod;
    /// <summary>Boss modifier of the stage; nullifiedBy = "C131" or "I08" when it was lifted.</summary>
    public string modifier, nullifiedBy;
    /// <summary>Loot kind offered in the shop after this (boss) stage, its offers, and what the bot took ("GOLD" = the gold option).</summary>
    public string lootKind;
    public long goldStart, reward, unused, interest, pig, ctlIncome, goldAfterSettle, spent, goldAfterShop;
    public List<string> bought = new List<string>(), gifts = new List<string>(), lootOffered = new List<string>(), lootTaken = new List<string>();
    /// <summary>offered = controllers seen on the shelf of the shop after this stage; granted = controllers received at
    /// stage end (C112); items = items used during the stage.</summary>
    public List<string> offered = new List<string>(), granted = new List<string>(), items = new List<string>();
    public string[] controllers = Array.Empty<string>();
}
public sealed class RunLog
{
    public uint seed; public bool won; public int reached; public string fail; public double seconds, secShop, secPreview, secHands, secPower; public int powerCalls;
    /// <summary>Diagnostics: seconds and calls of power estimates by what they were for.</summary>
    public Dictionary<string, double> powerSecondsBy = new Dictionary<string, double>(); public Dictionary<string, int> powerCallsBy = new Dictionary<string, int>();
    /// <summary>Submissions whose score the planner predicted before submitting (builds without the random C53),
    /// and how many of those the real session scored differently.</summary>
    public int predicted, mispredicted; public string firstMisprediction;
    /// <summary>Optional commands (items, gifts) the session refused; the bot carries on. Expected to stay 0.</summary>
    public int rejected; public string firstRejection;
    /// <summary>Stage previews where the session's quoted budgets differ from the rules the bot plans with. Expected 0.</summary>
    public int quoteMismatch; public string firstQuoteMismatch;
    public List<StageLog> stages = new List<StageLog>();
}

public sealed class BotOptions
{
    public bool weak;            // one-step look-ahead only, no interest reserve
    public int powerHands = 32;
    public double minGain = 0.06;   // below this a measured gain is indistinguishable from sampling noise
    public double riskFactor = 1.3; // pressure challenge: safe power must be at least this many times the x1.5 target
}

/// <summary>What the bot saw in its simulated hands with the current build; feeds the growth and economy estimates.</summary>
public sealed class HandStats
{
    public int hands, noRed, red3, half, dice, redDice, pool; public double rolls;
    public int[] prim = new int[14];   // 13 = scatter
    public double ShareNoRed => hands == 0 ? 0 : noRed / (double)hands;
    public double ShareRed3 => hands == 0 ? 0 : red3 / (double)hands;
    public double ShareHalf => hands == 0 ? 0 : half / (double)hands;
    public double RollsPerHand => hands == 0 ? 3 : rolls / hands;
    public double DicePerHand => hands == 0 ? 4 : dice / (double)hands;
    public double PoolPerHand => hands == 0 ? 1 : pool / (double)hands;
    public double RedShare => dice == 0 ? 0.5 : redDice / (double)dice;
    /// <summary>Chance that two consecutive hands share a primary hand (scatter counts).</summary>
    public double ShareSame => hands == 0 ? 0 : prim.Sum(x => (double)x * x) / ((double)hands * hands);
}

public sealed class Runner
{
    readonly GameConfig cfg; readonly Params P; readonly BotOptions opt;
    GameSession session; int seq; RunLog log; StageLog cur;
    readonly Planner strong = new Planner(), cheap = new Planner { depthMax = 1, s0 = 6, fullEnum = 0 };
    ulong powerSeed; string powerTag = "preview";
    /// <summary>When set, every accepted command is recorded in the format the in-game capture tour replays.</summary>
    public List<Dictionary<string, object>> recorded;

    public Runner(GameConfig cfg, Params P, BotOptions opt) { this.cfg = cfg; this.P = P; this.opt = opt; if (opt.weak) { strong.depthMax = 1; strong.s0 = 8; } }

    CommandResult Send(CommandKind kind, Action<SessionCommand> configure)
    {
        var command = new SessionCommand { kind = kind, commandId = "b" + (++seq), expectedActionSequence = session.ActionSequence };
        configure?.Invoke(command);
        if (kind == CommandKind.ConfirmLocks) foreach (string id in command.dieIds) if (!session.TogglePending(id)) throw new Exception("Illegal pending " + id);
        var result = session.Execute(command);
        if (result.accepted)
            recorded?.Add(new Dictionary<string, object> {
                ["kind"] = kind.ToString(), ["seed"] = command.seed, ["risk"] = command.risk, ["offerId"] = command.offerId, ["itemInstanceId"] = command.itemInstanceId,
                ["targetDieId"] = command.targetDieId, ["targetFaceId"] = command.targetFaceId, ["handId"] = command.handId,
                ["dieIds"] = command.dieIds ?? Array.Empty<string>(), ["targetPips"] = command.targetPips ?? Array.Empty<int>() });
        return result;
    }
    /// <summary>A command the run cannot continue without.</summary>
    CommandResult Act(CommandKind kind, Action<SessionCommand> configure = null)
    {
        var result = Send(kind, configure);
        if (!result.accepted) throw new Exception("Rejected " + kind + ": " + result.error);
        return result;
    }
    /// <summary>An optional command (item, gift). A refusal is counted and reported, and the run goes on.</summary>
    bool Try(CommandKind kind, Action<SessionCommand> configure, string what)
    {
        var result = Send(kind, configure);
        if (result.accepted) return true;
        if (log.rejected++ == 0) log.firstRejection = $"stage {session.State.stage.stageIndex + 1} {kind} {what}: {result.error}";
        return false;
    }
    bool TryUse(OwnedItem item)
    {
        if (!Try(CommandKind.UseItem, c => c.itemInstanceId = item.instanceId, item.contentId)) return false;
        cur.itemsUsed++; cur.items.Add(item.contentId); return true;
    }

    public RunLog Play(uint seed)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        try { return PlayRun(seed); } finally { if (log != null) log.seconds = clock.Elapsed.TotalSeconds; }
    }
    RunLog PlayRun(uint seed)
    {
        session = new GameSession(cfg, new MemorySaveStore()); seq = 0; log = new RunLog { seed = seed };
        strong.rng = new Rng(seed * 31UL + 7); cheap.rng = new Rng(seed * 17UL + 3); powerSeed = seed * 1000003UL;
        Act(CommandKind.NewRun, c => c.seed = seed);
        for (int guard = 0; guard < 8000; guard++)
        {
            var s = session.State;
            switch (s.phase)
            {
                case "StagePreview": { long t0 = System.Diagnostics.Stopwatch.GetTimestamp(); Preview(s); log.secPreview += System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalSeconds; break; }
                case "HandReady":
                    if (s.stage.qualified) Act(CommandKind.EndStage);
                    else { UseStageItems(s); Act(CommandKind.Roll); }
                    break;
                case "Selecting": { long t0 = System.Diagnostics.Stopwatch.GetTimestamp(); Select(s); log.secHands += System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalSeconds; break; }
                case "StageSettlement": Settle(s); Act(CommandKind.Continue); break;
                case "Shop": { long t0 = System.Diagnostics.Stopwatch.GetTimestamp(); Shop(); log.secShop += System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalSeconds; Act(CommandKind.LeaveShop); break; }
                case "RunWon": log.won = true; log.reached = cfg.stages.Length; return log;
                case "RunFailed":
                    cur.cleared = false; cur.score = D(s.stage.score); cur.subsUsed = s.stage.submissionOrdinal;
                    log.reached = s.stage.stageIndex + 1; log.fail = s.failureReason; return log;
                default: throw new Exception("Unexpected phase " + s.phase);
            }
        }
        throw new Exception("Action loop");
    }

    static double D(string v) => double.Parse(v, CultureInfo.InvariantCulture);

    // ---------- power estimate ----------
    /// <summary>mean = expected stage total; safe = stage total if every hand were a 25th-percentile hand.
    /// V blends both so that swingy cards are not valued at their lucky average.</summary>
    public struct Pw { public double mean, safe; public double V => Math.Sqrt(Math.Max(1e-9, mean) * Math.Max(safe, mean * 0.02)); }
    readonly double[] handBuf = new double[256];
    /// <summary>Simulated hands cycle through the stage's submission positions: the ordinal (C150, C153, C07, C127,
    /// FirstHalf / LaterHalf), the submissions left (C68, C102 pay on the last one) and the primary hands already
    /// used in the stage (NoRepeatHand).</summary>
    public Pw Power(Build b, int hands, ulong seed, HandStats stats = null, double halfScore = 0)
    {
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        cheap.Set(b); cheap.needed = double.PositiveInfinity; cheap.rollRng = new Rng(seed); cheap.rng = new Rng(seed ^ 0xABCDEFUL);
        double sum = 0;
        for (int h = 0; h < hands; h++)
        {
            int position = h % b.submissions;
            b.subsLeft = b.submissions - position; b.ordinal = position; if (position == 0) b.usedHands = 0;
            double v = handBuf[h] = cheap.PlayHand(out int prim, out int nSub); sum += v;
            if (prim >= 0) b.usedHands |= 1 << prim;
            if (stats != null && nSub > 0)
            {
                stats.hands++; stats.prim[prim < 0 ? 13 : prim]++; stats.rolls += cheap.lastRollsUsed; stats.dice += nSub; stats.redDice += cheap.lastRedN; stats.pool += cheap.lastPoolN;
                if (cheap.lastRedN == 0) stats.noRed++; if (cheap.lastRedCount >= 3) stats.red3++; if (halfScore > 0 && v >= halfScore) stats.half++;
            }
        }
        Array.Sort(handBuf, 0, hands);
        double took = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalSeconds;
        log.powerCalls++; log.secPower += took; log.powerSecondsBy[powerTag] = log.powerSecondsBy.GetValueOrDefault(powerTag) + took; log.powerCallsBy[powerTag] = log.powerCallsBy.GetValueOrDefault(powerTag) + 1;
        return new Pw { mean = sum / hands * b.submissions, safe = handBuf[hands / 4] * b.submissions };
    }
    /// <summary>Power() is deterministic for a given build (the hand seed is fixed per run), so results are reused
    /// within one shop visit and the following stage preview.</summary>
    readonly Dictionary<string, Pw> powerCache = new Dictionary<string, Pw>();
    readonly System.Text.StringBuilder keyBuf = new System.Text.StringBuilder();
    /// <summary>The stage that shop and preview estimates are made for when no context is passed explicitly.</summary>
    StageCtx evalCtx;
    string Key(RunState st, StageCtx ctx)
    {
        var k = keyBuf; k.Clear();
        foreach (var c in st.controllers) k.Append(c.definitionId).Append(':').Append(c.enchantmentId).Append(c.alive ? '+' : '-').Append(c.state.growth).Append(',').Append(c.state.turnoverUnits2).Append(c.state.activeColor).Append(';');
        k.Append('|');
        foreach (var d in st.dice.OrderBy(d => d.createdSerial))
        {
            k.Append(d.isGlass ? 'G' : 'g').Append(d.lifetime == "permanent" ? 'P' : 'T');
            foreach (var f in d.faces.OrderBy(f => f.faceId, StringComparer.Ordinal)) k.Append(f.permanentPip).Append(f.baseColor[0]).Append(f.isGold ? '*' : '.').Append(f.texture[0]).Append(f.baseWeight);
            k.Append(';');
        }
        k.Append('|'); foreach (var h in st.handLevels) k.Append(h.level).Append(','); foreach (var h in st.colorLevels) k.Append(h.level).Append(',');
        k.Append('|').Append(st.gold).Append('|').Append(st.previousPrimaryHandId).Append('|').Append(st.controllerSlots).Append('|').Append(st.inventory.Count).Append('|').Append(ctx == null ? "" : ctx.Key);
        return k.ToString();
    }
    /// <summary>Resolves a context for one particular build: a scapegoat (C131) lifts the modifier, and the modifier
    /// disables controllers by position or rarity, so the list depends on the build being valued.</summary>
    StageCtx Effective(RunState st, StageCtx ctx)
    {
        if (ctx == null || ctx.disabled != null) return ctx;
        string mod = ctx.modifierId;
        if (mod != null && st.controllers.Any(c => c.definitionId == "C131")) mod = null;
        return new StageCtx { modifierId = mod, boss = ctx.boss, disabled = ControllerRules.DisabledBy(st.controllers, cfg, mod), capacity = ctx.capacity, rolls = ctx.rolls, submissions = ctx.submissions };
    }
    /// <summary>The build as it will stand once the stage has started: every C136 source adds a temporary die and
    /// every real C128 turns the oldest non-glass permanent die into glass.</summary>
    RunState AtStageStart(RunState st, StageCtx ctx)
    {
        if (!st.controllers.Any(c => c.definitionId == "C136" || c.definitionId == "C128")) return st;
        var sources = ControllerRules.ResolveSources(st.controllers, null, ctx == null ? null : ctx.disabled);
        int temps = sources.Count(x => x.abilityId == "C136") * cfg.Controller("C136").parameters.dicePerStage;
        int glass = sources.Count(x => x.abilityId == "C128" && x.emitterDefinitionId == "C128") * cfg.Controller("C128").parameters.dicePerStage;
        if (temps == 0 && glass == 0) return st;
        var copy = st.Clone();
        for (int i = 0; i < glass; i++) { var die = copy.dice.Where(d => d.lifetime == "permanent" && !d.isGlass).OrderBy(d => d.createdSerial).FirstOrDefault(); if (die == null) break; die.isGlass = true; }
        for (int i = 0; i < temps; i++)
        {
            if (copy.dice.Count >= cfg.maxActiveDice || copy.dice.Count(d => d.lifetime == "stage_temporary") >= cfg.maxTemporaryDice) break;
            copy.dice.Add(DiceFactory.CreateStandard(cfg, copy.nextDieSerial++, "stage_temporary", ""));
        }
        return copy;
    }
    Pw PowerOfState(RunState st, StageCtx ctx = null, HandStats stats = null, double halfScore = 0)
    {
        ctx = ctx ?? evalCtx;
        string key = Key(st, ctx);
        if (stats == null) { if (powerCache.TryGetValue(key, out var hit)) return hit; }
        else if (statsCache.TryGetValue(key, out var seen) && seen.halfScore == halfScore && powerCache.TryGetValue(key, out var same))
        {
            // The same build was already played with statistics in this shop: hand the recorded statistics back.
            var from = seen.stats; stats.hands = from.hands; stats.noRed = from.noRed; stats.red3 = from.red3; stats.half = from.half; stats.dice = from.dice; stats.redDice = from.redDice; stats.pool = from.pool; stats.rolls = from.rolls;
            Array.Copy(from.prim, stats.prim, from.prim.Length); return same;
        }
        var pw = PowerOfStateUncached(st, ctx, stats, halfScore); powerCache[key] = pw;
        if (stats != null) statsCache[key] = (stats, halfScore);
        return pw;
    }
    readonly Dictionary<string, (HandStats stats, double halfScore)> statsCache = new Dictionary<string, (HandStats, double)>();
    Pw PowerOfStateUncached(RunState st, StageCtx ctx, HandStats stats, double halfScore)
    {
        ctx = Effective(st, ctx); st = AtStageStart(st, ctx);
        // A C45 that is not owned yet has no colour; value it as the average of both.
        var open = st.controllers.Where(c => c.definitionId == "C45" && string.IsNullOrEmpty(c.state.activeColor)).ToArray();
        if (open.Length == 0) return Power(Build.From(st, P, false, ctx: ctx), opt.powerHands, powerSeed, stats, halfScore);
        var total = new Pw();
        foreach (string col in new[] { "red", "blue" })
        {
            foreach (var c in open) c.state.activeColor = col;
            var p = Power(Build.From(st, P, false, ctx: ctx), opt.powerHands, powerSeed, stats, halfScore); total.mean += p.mean / 2; total.safe += p.safe / 2;
        }
        foreach (var c in open) c.state.activeColor = null;
        return total;
    }

    // ---------- stage ----------
    void Preview(RunState s)
    {
        KeepAltarRight();
        s = session.State;
        int index = s.stage.stageIndex; var def = cfg.stages[index]; bool boss = def.stageKind == "boss";
        cur = new StageLog { stage = index + 1, kind = def.stageKind, goldStart = s.gold, dice = s.dice.Count, ctrl = s.controllers.Count, slots = s.controllerSlots, targetBase = D(def.normalTarget),
            controllers = s.controllers.Select(c => c.definitionId + (c.enchantmentId == "none" ? "" : ":" + c.enchantmentId)).ToArray() };
        log.stages.Add(cur);
        evalCtx = new StageCtx { boss = boss, disabled = Array.Empty<string>() };
        var raw = PowerOfState(s); cur.powerRaw = raw.mean; cur.safeRaw = raw.safe; var pw = raw;
        if (boss && !string.IsNullOrEmpty(s.stage.modifierId))
        {
            // The boss modifier as the cycle panel shows it, and the stage as the session quotes it (budgets, target,
            // disabled controllers; with a scapegoat the modifier is quoted as already lifted).
            string mod = s.stage.modifierId; var fx = P.Fx(mod); var entry = session.EntryView();
            cur.modifier = mod; cur.targetMod = cur.targetBase * fx.targetPercent / 100.0;
            var withMod = PowerOfState(s, new StageCtx { modifierId = mod, boss = true, disabled = ControllerRules.DisabledBy(s.controllers, cfg, mod) });
            cur.powerMod = withMod.mean; cur.safeMod = withMod.safe;
            var quoted = new StageCtx { modifierId = string.IsNullOrEmpty(entry.modifierId) ? null : entry.modifierId, disabled = entry.disabledControllerIds, boss = true,
                capacity = session.EffectiveCapacity, rolls = session.InitialRolls, submissions = session.InitialSubmissions };
            CheckQuotes(s, entry, quoted);
            var st = s;
            if (entry.scapegoat) { cur.nullifiedBy = "C131"; st = s.Clone(); st.controllers.Remove(st.controllers.First(c => c.definitionId == "C131")); }
            pw = PowerOfState(st, quoted);
        }
        cur.power = pw.mean; cur.safe = pw.safe;
        // Pressure challenge: accept only with a margin over the x1.5 target.
        if (def.riskEnabled)
        {
            cur.riskOffered = true;
            if (!opt.weak && pw.safe >= opt.riskFactor * D(def.riskTarget)) { Act(CommandKind.ChooseChallenge, c => c.risk = true); cur.risk = true; }
        }
        Act(CommandKind.StartStage);
        cur.target = D(session.State.stage.target);
    }

    /// <summary>The bot plans with ControllerRules + StageRules; the stage-select screen shows the session's own
    /// numbers. They must agree; a difference is recorded and reported, never hidden.</summary>
    void CheckQuotes(RunState s, StageEntryView entry, StageCtx quoted)
    {
        var entering = s.controllers.ToList(); string mod = quoted.modifierId;
        if (entry.scapegoat) entering.Remove(entering.First(c => c.definitionId == "C131"));
        var rules = StageRules.For(cfg, mod, ControllerRules.DisabledBy(entering, cfg, mod));
        int cap = ControllerRules.Capacity(entering, cfg, false, null, rules), rolls = ControllerRules.RollBudget(entering, cfg, 0, null, rules), subs = ControllerRules.SubmissionBudget(entering, cfg, null, rules);
        bool sameDisabled = new HashSet<string>(rules.disabledIds).SetEquals(quoted.disabled);
        if (s.controllers.Any(c => c.definitionId == "C100")) return;   // altars change the entering set; not modelled here
        if ((cap != quoted.capacity || rolls != quoted.rolls || subs != quoted.submissions || !sameDisabled) && log.quoteMismatch++ == 0)
            log.firstQuoteMismatch = $"stage {s.stage.stageIndex + 1} modifier {s.stage.modifierId}: session quotes capacity/rolls/submissions {quoted.capacity}/{quoted.rolls}/{quoted.submissions} disabled [{string.Join(",", quoted.disabled)}], rules give {cap}/{rolls}/{subs} [{string.Join(",", rules.disabledIds)}]";
    }

    /// <summary>Items used before the first roll of a stage.
    ///   I08  when the boss modifier costs at least a quarter of the build's power and the stage is not already safe
    ///        (safe power below 2x target), or when it would downgrade hands the build has levelled (HandDowngrade);
    ///   I07, I02  unless the stage is safe (safe power at least 2x target); I06 only when the stage looks out of
    ///        reach (safe power below target), otherwise it waits for the last submission (SubmissionItems). With a
    ///        full bag and C110 / C111 feeding it, one of them is used anyway to make room;
    ///   I12  when more dice are alive than the bar holds, one more slot adds at least 10% and the stage is not safe.
    /// Under NoItems only I08 is allowed.</summary>
    void UseStageItems(RunState s)
    {
        if (s.stage.submissionOrdinal != 0 || s.hand.hasRolled || s.inventory.Count == 0) return;
        var fx = P.Fx(session.CurrentModifierId);
        var charm = s.inventory.FirstOrDefault(x => x.contentId == "I08");
        if (charm != null && fx != ModFx.None && cur.nullifiedBy == null)
        {
            double with = Math.Sqrt(Math.Max(1e-9, cur.powerMod) * Math.Max(cur.safeMod, cur.powerMod * 0.02)) / Math.Max(1, cur.targetMod);
            double without = Math.Sqrt(Math.Max(1e-9, cur.powerRaw) * Math.Max(cur.safeRaw, cur.powerRaw * 0.02)) / cur.targetBase;
            bool hurts = 1 - with / Math.Max(1e-9, without) >= 0.25 && cur.safe < 2.0 * cur.target;
            bool downgrade = fx.handDowngrade && s.handLevels.Any(h => h.level > 0);
            if ((hurts || downgrade) && TryUse(charm))
            {
                cur.nullifiedBy = "I08"; s = session.State; cur.target = D(s.stage.target); cur.power = cur.powerRaw; cur.safe = cur.safeRaw; fx = ModFx.None;
            }
        }
        if (fx.noItems) return;
        // p0.9.0 used these as soon as it had them (it only bought them when a stage looked out of reach). With free
        // items arriving from C110 / C111 / loot that would waste them on safe stages, so:
        bool safeStage = cur.safe >= 2.0 * cur.target, shortStage = cur.safe < 1.0 * cur.target;
        bool fed = s.controllers.Any(c => c.definitionId == "C110" || c.definitionId == "C111");
        foreach (string id in new[] { "I07", "I02", "I06" })
            foreach (var item in session.State.inventory.Where(x => x.contentId == id).ToArray())
            {
                var now = session.State; bool bagFull = fed && now.inventory.Count >= now.consumableSlots;
                if (id == "I02" && (now.dice.Count >= cfg.maxActiveDice || now.dice.Count(d => d.lifetime == "stage_temporary") >= cfg.maxTemporaryDice)) continue;
                // I06 waits for the last submission of a stage that is still short (SubmissionItems) unless the stage looks out of reach.
                if (id == "I06" ? !(shortStage || bagFull) : safeStage && !bagFull) continue;
                TryUse(item);
            }
        foreach (var item in session.State.inventory.Where(x => x.contentId == "I12").ToArray())
        {
            var now = session.State;
            if (now.stage.capacity >= cfg.maxSubmitCapacity || now.dice.Count <= now.stage.capacity || cur.safe >= 2.0 * cur.target) break;
            var b0 = Build.From(now, P, true); var p0 = Power(b0, opt.powerHands, powerSeed);
            var b1 = Build.From(now, P, true); b1.capacity++; var p1 = Power(b1, opt.powerHands, powerSeed);
            if (p1.V < 1.10 * p0.V || !TryUse(item)) break;
        }
    }

    void Select(RunState s)
    {
        var b = Build.From(s, P, true, s.hand.largeBuff, s.hand.smallBuff);
        b.subsLeft = s.stage.remainingSubmissions;   // C68 / C102 pay only on the stage's last submission
        strong.Set(b); strong.needed = Math.Max(1, D(s.stage.target) - D(s.stage.score));
        var all = s.dice.OrderBy(d => d.createdSerial).ToArray();
        int[] face = new int[12]; int locked = 0, alive = (1 << all.Length) - 1;
        for (int i = 0; i < all.Length; i++)
        {
            var r = s.hand.currentResults.First(x => x.dieId == all[i].dieId);
            face[i] = r.hasRolled ? int.Parse(r.currentFaceId.Substring(1)) - 1 : 0;
            if (s.hand.orderedLockedIds.Contains(all[i].dieId)) locked |= 1 << i;
        }
        int rolled = alive & ~locked;
        int add = strong.Decide(locked, alive, rolled, s.hand.rollsRemaining, s.hand.rollsUsed, face, s.hand.c05Used, out bool submit);
        if (add != 0) Act(CommandKind.ConfirmLocks, c => c.dieIds = Enumerable.Range(0, all.Length).Where(i => (add >> i & 1) != 0).Select(i => all[i].dieId).ToArray());
        if (!submit) { Act(CommandKind.Roll); return; }
        // The unlocked dice stay in the pool of this submission.
        int mask = locked | add, pool = alive & ~mask, rollsLeft = s.hand.rollsRemaining, used = s.hand.rollsUsed;
        double expected = strong.FinalEval(mask, pool, rollsLeft, used, face, true);
        Func<Build> rebuild = () =>
        {
            var now = session.State; var nb = Build.From(now, P, true, now.hand.largeBuff, now.hand.smallBuff); nb.subsLeft = now.stage.remainingSubmissions;
            strong.Set(nb); expected = strong.FinalEval(mask, pool, rollsLeft, used, face, true); return nb;
        };
        if (s.inventory.Count > 0 && !b.fx.noItems) b = SubmissionItems(s, b, mask, pool, rollsLeft, used, face, all, ref expected, rebuild);
        SetOrder(all);
        double real = D(Act(CommandKind.Submit).receipt.score);
        // Self-check: the planner's value for the submission it chose must be the score the real session awards.
        if (b.cnt[53] == 0)
        {
            log.predicted++;
            if (Math.Abs(Math.Floor(expected + 1e-6) - real) > 1e-9 * real + 0.5 && log.mispredicted++ == 0)
                log.firstMisprediction = $"stage {s.stage.stageIndex + 1} modifier={session.State.stage.modifierId} planner={expected:R} session={real:R} controllers=[{string.Join(",", s.controllers.Select(c => c.definitionId))}]";
        }
    }
    void SetOrder(DieInstance[] all)
    {
        var order = strong.bestOrd.Take(strong.bestN).Select(i => all[i].dieId).ToArray();
        if (!session.State.hand.orderedLockedIds.SequenceEqual(order)) Act(CommandKind.Reorder, c => c.dieIds = order);
    }

    /// <summary>Items used on the submission that is about to be made (all dice are locked, the order is planned).
    ///   I10 / I01  when the wave raises this submission by at least 25% and the submission alone does not clear the stage;
    ///   I11  on the first die of the order when its face is not gold yet (if order does not matter, the highest
    ///        non-gold pip is moved to the front first);
    ///   I06  when this is the stage's last submission and it does not clear the stage;
    ///   I09  when the submission does not clear the stage on its own and either doubling it does, or it is the last
    ///        submission, or the stage looked risky (safe power below 1.3x target) and the hand is at least 70% of a typical one.</summary>
    Build SubmissionItems(RunState s, Build b, int mask, int pool, int rollsLeft, int used, int[] face, DieInstance[] all, ref double expected, Func<Build> rebuild)
    {
        double needed = strong.needed;
        if (expected < needed && !b.redWave && !b.blueWave)
        {
            var blue = s.inventory.FirstOrDefault(x => x.contentId == "I10"); var redItem = s.inventory.FirstOrDefault(x => x.contentId == "I01");
            double vb = -1, vr = -1;
            if (blue != null) { b.blueWave = true; vb = strong.FinalEval(mask, pool, rollsLeft, used, face, false); b.blueWave = false; }
            if (redItem != null) { b.redWave = true; vr = strong.FinalEval(mask, pool, rollsLeft, used, face, false); b.redWave = false; }
            var wave = vb >= 1.25 * expected && vb >= vr ? blue : vr >= 1.25 * expected ? redItem : null;
            if (wave != null && TryUse(wave)) b = rebuild(); else if (blue != null || redItem != null) strong.FinalEval(mask, pool, rollsLeft, used, face, true);
        }
        var pen = session.State.inventory.FirstOrDefault(x => x.contentId == "I11");
        if (pen != null)
        {
            int n = strong.bestN, pick = -1;
            if (!b.pos)
            {
                for (int k = 0; k < n; k++) { int die = strong.bestOrd[k]; if (!b.dice[die].temp && !b.dice[die].gold[face[die]] && (pick < 0 || b.dice[die].pip[face[die]] > b.dice[strong.bestOrd[pick]].pip[face[strong.bestOrd[pick]]])) pick = k; }
                if (pick > 0) { (strong.bestOrd[0], strong.bestOrd[pick]) = (strong.bestOrd[pick], strong.bestOrd[0]); pick = 0; }
            }
            else if (!b.dice[strong.bestOrd[0]].temp && !b.dice[strong.bestOrd[0]].gold[face[strong.bestOrd[0]]]) pick = 0;
            if (pick == 0) { SetOrder(all); if (TryUse(pen)) b = rebuild(); }
        }
        // I06: one more submission, taken only when this is the last one and it does not clear the stage.
        var extra = session.State.inventory.FirstOrDefault(x => x.contentId == "I06");
        if (extra != null && expected < needed && session.State.stage.remainingSubmissions == 1 && TryUse(extra)) b = rebuild();
        var spark = session.State.inventory.FirstOrDefault(x => x.contentId == "I09");
        if (spark != null && !session.State.hand.doubleScore && expected < needed)
        {
            double typical = cur.power / Math.Max(1, b.submissions);
            bool clinch = 2 * expected >= needed, last = session.State.stage.remainingSubmissions == 1, risky = cur.safe < 1.3 * cur.target && expected >= 0.7 * typical;
            if ((clinch || last || risky) && TryUse(spark)) b = rebuild();
        }
        return b;
    }

    void Settle(RunState s)
    {
        var r = s.lastStageSettlement;
        cur.cleared = true; cur.score = D(r.score); cur.subsUsed = s.stage.submissionOrdinal; cur.unusedSubs = r.remainingSubmissions;
        cur.reward = r.baseReward; cur.unused = r.unusedSubmissionReward; cur.interest = r.interest; cur.ctlIncome = r.controllerIncome; cur.pig = r.goldBefore - cur.goldStart; cur.goldAfterSettle = r.goldAfter;
        cur.goldAfterShop = r.goldAfter;
        // Controllers that appeared during the stage can only come from C112.
        var before = new HashSet<string>(cur.controllers.Select(c => c.Split(':')[0]));
        foreach (var c in s.controllers) if (!before.Contains(c.definitionId)) cur.granted.Add(c.definitionId);
    }

    // ---------- shop ----------
    sealed class Cand { public string key, label; public double gain; public long price; public Action buy; public bool stale, reevaluate; }

    static RunState WithController(RunState s, ControllerInstance offer, string removeInstanceId)
    {
        var st = s.Clone(); if (removeInstanceId != null) st.controllers.RemoveAll(c => c.instanceId == removeInstanceId);
        var c2 = offer.Clone(); c2.state = new ControllerDynamicState(); c2.alive = true; st.controllers.Add(c2); return st;
    }

    // ---------- habits: simple observable quantities the growth and economy estimates are built from ----------
    /// <summary>Per-stage rates. From the run so far (blended with a prior worth three stages) and from the hands
    /// simulated with the current build.</summary>
    sealed class Habits
    {
        public double subs = 2.2, unusedSubs = 0.8, upgrades = 0.3, levels = 0.8, sells = 0.3, buys = 1.0, items = 0.3, risk = 0.5;   // per stage
        public double rolls = 3, noRed = 0.1, red3 = 0.1, same = 0.3, half = 0.3, dice = 4, pool = 1, redShare = 0.5;                  // per simulated hand
        public int glass; public long gold; public bool danger, comfy;
    }
    Habits hab = new Habits();
    static double Blend(double sum, int n, double prior) => (sum + prior * 3) / (n + 3);
    void Observe(RunState s, HandStats stats, bool danger, bool comfy)
    {
        var cleared = log.stages.Where(x => x.cleared).ToArray(); int n = cleared.Length;
        var pressure = cleared.Where(x => x.riskOffered).ToArray();
        hab = new Habits
        {
            subs = Blend(cleared.Sum(x => (double)x.subsUsed), n, 2.2), unusedSubs = Blend(cleared.Sum(x => (double)x.unusedSubs), n, 0.8),
            upgrades = Blend(cleared.Sum(x => (double)x.bought.Concat(x.gifts).Concat(x.lootTaken).Count(l => l.StartsWith("U"))), n, 0.3),
            levels = Blend(cleared.Sum(x => (double)x.bought.Count(l => l.StartsWith("up:") || l.StartsWith("study:"))), n, 0.8),
            sells = Blend(cleared.Sum(x => (double)x.bought.Concat(x.lootTaken).Count(l => l.Contains("(replaces"))), n, 0.3),
            buys = Blend(cleared.Sum(x => (double)x.bought.Count(l => l.StartsWith("C") || l.StartsWith("U"))), n, 1.0),
            items = Blend(cleared.Sum(x => (double)x.itemsUsed), n, 0.3),
            risk = Blend(pressure.Sum(x => x.risk ? 1.0 : 0.0), pressure.Length, 0.5),
            rolls = stats.RollsPerHand, noRed = stats.ShareNoRed, red3 = stats.ShareRed3, same = stats.ShareSame, half = stats.ShareHalf,
            dice = stats.DicePerHand, pool = stats.PoolPerHand, redShare = stats.RedShare,
            glass = s.dice.Count(d => d.isGlass), gold = s.gold, danger = danger, comfy = comfy
        };
    }
    int StagesOfKind(string kind, int from, int toExclusive) { int n = 0; for (int i = Math.Max(0, from); i < Math.Min(cfg.stages.Length, toExclusive); i++) if (cfg.stages[i].stageKind == kind) n++; return n; }
    /// <summary>Boss stages still to clear that are followed by a shop (the final boss pays nothing afterwards).</summary>
    int BossesAhead(int nextIndex) => StagesOfKind("boss", nextIndex, cfg.stages.Length - 1);

    /// <summary>Growth cards are worth nothing at purchase, so the shop values every build at its projected state:
    ///   growth' = min(cap, growth + rate x horizon),   horizon = min(stages still to play, 6; 2 if the next stage looks dangerous)
    /// with a per-stage rate estimated from the habits above (S = submissions used per stage):
    ///   C55 2*S                         C56 2*S*share(no red)            C57 3*S*share(red group >= 3)
    ///   C58 4                           C59 4*unused submissions         C60 2*die upgrades bought per stage
    ///   C61 2*levels bought per stage   C62 1*rolls per hand*S           C90 15*glass dice*break chance*rolls per hand*S/2
    ///   C54 3*S*share(same primary), and never above its steady state 3*p/(1-p) because a different hand resets it
    ///   C100 0 (the bot never feeds it)
    /// p1.0.0:
    ///   C129 10 x boss stages inside the horizon (the final boss does not count)
    ///   C132 12 x pressure stages inside the horizon x the bot's own acceptance rate so far
    ///   C134 3 x items used per stage
    ///   C141 decays: it is valued at the geometric mean of its multiplier over the horizon (x1 once it is gone) and
    ///        dropped from the projection when that mean is below x1.25
    /// cap: 100 for multiplier cards (x6), 300 for base-add cards. Applied to owned and candidate cards alike, so an
    /// owned growth card is not sold for a card that is only stronger today. Face-growth cards: see ProjectFaces.</summary>
    void Project(RunState st, int nextIndex)
    {
        double horizon = Math.Min(cfg.stages.Length - nextIndex, hab.danger ? 2 : 6);
        var fade = cfg.Controller("C141").parameters;
        foreach (var c in st.controllers.ToArray())
        {
            string id = c.definitionId;
            if (id == "C141")
            {
                int steps = Math.Max(1, (int)Math.Round(horizon)); double sumLog = 0;
                for (int k = 0; k < steps; k++) { double g2 = c.state.growth + k; if (g2 < fade.expireGrowth) sumLog += Math.Log((fade.initialMultiplierUnits2 - g2) / 2.0); }
                double mean = Math.Exp(sumLog / steps);
                if (mean < 1.25) { st.controllers.Remove(c); continue; }
                c.state.growth = Math.Max(c.state.growth, Math.Min(fade.expireGrowth - 1, (long)Math.Round(fade.initialMultiplierUnits2 - 2 * mean)));
                continue;
            }
            if (!ControllerRules.HasGrowth(id)) continue;
            double g = c.state.growth, step = cfg.Controller(id).parameters.growthPerEvent, rate, limit = ControllerRules.IsGrowthBase(id) ? 300 : 100;
            switch (id)
            {
                case "C54": { double p = Math.Min(0.9, hab.same); rate = step * hab.subs * p; limit = Math.Min(limit, Math.Max(g, step * p / (1 - p))); break; }
                case "C55": rate = step * hab.subs; break;
                case "C56": rate = step * hab.subs * hab.noRed; break;
                case "C57": rate = step * hab.subs * hab.red3; break;
                case "C58": rate = step; break;
                case "C59": rate = step * hab.unusedSubs; break;
                case "C60": rate = step * hab.upgrades; break;
                case "C61": rate = step * hab.levels; break;
                case "C62": rate = step * hab.rolls * hab.subs; break;
                case "C90": rate = step * hab.glass * P.glassBreak * hab.rolls * hab.subs * 0.5; break;
                case "C129": rate = step * StagesOfKind("boss", nextIndex, Math.Min(cfg.stages.Length - 1, nextIndex + (int)horizon)) / Math.Max(1, horizon); break;
                case "C132": rate = step * hab.risk * StagesOfKind("pressure", nextIndex, nextIndex + (int)horizon) / Math.Max(1, horizon); break;
                case "C134": rate = step * hab.items; break;
                default: rate = 0; break;
            }
            c.state.growth = (long)Math.Round(Math.Max(g, Math.Min(limit, g + rate * horizon)));
        }
        ProjectFaces(st, horizon);
    }
    static bool Projectable(string id) => ControllerRules.HasGrowthCounter(id) || FaceGrowers.Contains(id);
    static readonly HashSet<string> FaceGrowers = new HashSet<string> { "C117", "C118", "C119", "C120", "C121", "C122", "C123", "C124", "C125", "C126", "C127" };

    /// <summary>Face-growth cards change dice slowly, so a build that holds one is valued with the dice it is expected
    /// to have after `horizon` stages. Stage-end cards follow their exact rule; after-score cards are approximated
    /// from the bot's habits (N = horizon x submissions per stage, D = dice per submission):
    ///   C120 / C121  exact: every stage each permanent die's lowest face +1 / highest face -1
    ///   C117  0.8 N faces turn gold, highest pips first, dice in turn
    ///   C118  when at least 60% of the submitted dice share a colour: mixed opposite pairs take that colour, 0.5 N D pairs at most
    ///   C119  0.5 N faces take the die's highest pip (lowest for small-pip builds), one die after another
    ///   C122  0.8 N D single steps towards 5, the face farthest from 5 first, dice in turn
    ///   C123  0.8 N D single steps into 2..3, same order
    ///   C124  0.8 N D weight steps on each die's most valuable face (pip x gold), up to the weight cap
    ///   C125  1.6 N faces turn smooth, highest pips first
    ///   C126  0.8 N x pool dice per submission steps of +1 on the lowest faces
    ///   C127  0.7 x horizon copies of the best face onto the worst face of another die
    /// A mimic of one of these cards counts as a second source.</summary>
    void ProjectFaces(RunState st, double horizon)
    {
        if (!st.controllers.Any(c => FaceGrowers.Contains(c.definitionId))) return;
        var sources = ControllerRules.ResolveSources(st.controllers);
        Func<string, int> count = id => sources.Count(x => x.abilityId == id);
        var dice = st.dice.Where(d => d.lifetime == "permanent").OrderBy(d => d.createdSerial).ToList(); if (dice.Count == 0) return;
        Func<DieInstance, FaceState[]> ordered = d => d.faces.OrderBy(f => f.faceId, StringComparer.Ordinal).ToArray();
        int stages = Math.Max(0, (int)Math.Round(horizon)); double subs = horizon * hab.subs;
        bool low = st.controllers.Any(c => c.definitionId == "C10" || c.definitionId == "C17" || c.definitionId == "C19" || c.definitionId == "C66");
        int k120 = count("C120"), k121 = count("C121");
        for (int t = 0; t < stages && k120 + k121 > 0; t++)
        {
            for (int k = 0; k < k120; k++) foreach (var d in dice) { var f = ordered(d).OrderBy(x => x.permanentPip).First(); if (f.permanentPip < cfg.maxPipValue) f.permanentPip++; }
            for (int k = 0; k < k121; k++) foreach (var d in dice) { var f = ordered(d).OrderByDescending(x => x.permanentPip).First(); if (f.permanentPip > cfg.minPipValue) f.permanentPip--; }
        }
        Func<double, int> steps = x => (int)Math.Round(Math.Max(0, x));
        for (int t = 0, n = steps(subs * 0.8) * count("C117"); t < n; t++)
        { var f = ordered(dice[t % dice.Count]).Where(x => !x.isGold).OrderByDescending(x => x.permanentPip).FirstOrDefault(); if (f != null) f.isGold = true; }
        if (count("C118") > 0 && (hab.redShare >= 0.6 || hab.redShare <= 0.4))
        {
            string colour = hab.redShare >= 0.6 ? "red" : "blue"; int left = steps(subs * hab.dice * 0.5) * count("C118");
            foreach (var d in dice)
                foreach (var f in ordered(d))
                {
                    var o = d.faces.First(x => x.faceId == cfg.Opposite(f.faceId));
                    if (left > 0 && f.baseColor != o.baseColor) { f.baseColor = colour; o.baseColor = colour; left--; }
                }
        }
        for (int left = steps(subs * 0.5) * count("C119"), di = 0; left > 0 && di < dice.Count; di++)
        {
            var faces = ordered(dice[di]); int want = low ? faces.Min(x => x.permanentPip) : faces.Max(x => x.permanentPip);
            foreach (var f in faces) if (left > 0 && f.permanentPip != want) { f.permanentPip = want; left--; }
        }
        int target = cfg.Controller("C122").parameters.pip;
        for (int t = 0, n = steps(subs * hab.dice * 0.8) * count("C122"); t < n; t++)
        { var f = ordered(dice[t % dice.Count]).Where(x => x.permanentPip != target).OrderByDescending(x => Math.Abs(x.permanentPip - target)).FirstOrDefault(); if (f != null) f.permanentPip += f.permanentPip < target ? 1 : -1; }
        int[] band = cfg.Controller("C123").parameters.pips;
        for (int t = 0, n = steps(subs * hab.dice * 0.8) * count("C123"); t < n; t++)
        { var f = ordered(dice[t % dice.Count]).Where(x => x.permanentPip < band[0] || x.permanentPip > band[1]).OrderByDescending(x => x.permanentPip < band[0] ? band[0] - x.permanentPip : x.permanentPip - band[1]).FirstOrDefault(); if (f != null) f.permanentPip += f.permanentPip < band[0] ? 1 : -1; }
        for (int t = 0, n = steps(subs * hab.dice * 0.8) * count("C124"); t < n; t++)
        { var f = ordered(dice[t % dice.Count]).Where(x => x.baseWeight < cfg.maxFaceWeight).OrderByDescending(x => (low ? 7 - x.permanentPip : x.permanentPip) * (x.isGold ? 2 : 1)).FirstOrDefault(); if (f != null) f.baseWeight++; }
        for (int t = 0, n = steps(subs * 1.6) * count("C125"); t < n; t++)
        { var f = ordered(dice[t % dice.Count]).Where(x => x.texture == "none").OrderByDescending(x => x.permanentPip).FirstOrDefault(); if (f != null) f.texture = cfg.Controller("C125").parameters.textures[0]; }
        for (int t = 0, n = steps(subs * hab.pool * 0.8) * count("C126"); t < n; t++)
        { var f = ordered(dice[t % dice.Count]).Where(x => x.permanentPip < cfg.maxPipValue).OrderBy(x => x.permanentPip).FirstOrDefault(); if (f != null) f.permanentPip += cfg.Controller("C126").parameters.delta > 0 ? 1 : 0; }
        for (int t = 0, n = steps(stages * 0.7) * count("C127"); t < n && dice.Count > 1; t++)
        {
            var all = dice.SelectMany(d => d.faces.Select(f => (d, f))).ToArray();
            var best = all.OrderByDescending(x => (low ? 7 - x.f.permanentPip : x.f.permanentPip) * (x.f.isGold ? 2 : 1)).First();
            var worst = all.Where(x => x.d != best.d).OrderBy(x => (low ? 7 - x.f.permanentPip : x.f.permanentPip) * (x.f.isGold ? 2 : 1)).First();
            worst.f.permanentPip = best.f.permanentPip; worst.f.baseColor = best.f.baseColor; worst.f.texture = best.f.texture; worst.f.isGold = best.f.isGold;
        }
    }

    /// <summary>What an item is worth to this bot, in gold. Items it never uses are worth nothing to it.</summary>
    double ItemValue(string id, RunState s)
    {
        switch (id)
        {
            case "I06": return 3;                                            // one more submission
            case "I09": return 3;                                            // one submission doubled
            case "I07": return 2.5;                                          // one more roll per hand for a stage
            case "I11": return 2.5;                                          // a permanent gold face
            case "I02": return s.dice.Count < cfg.maxActiveDice ? 2 : 0;    // a temporary die
            case "I12": return s.dice.Count(d => d.lifetime == "permanent") > ControllerRules.Capacity(s.controllers, cfg) ? 2 : 0.5;
            case "I08": return bossLoss >= 0.25 ? 4 : 1;                    // lifts the coming boss modifier
            case "I10": case "I01": return 1;                               // waves: used only when they add 25%
            default: return 0;                                               // I03, I04, I05: never used by the bot
        }
    }

    /// <summary>Gold-equivalent value of cards that earn or save money instead of scoring, in log-power units:
    /// one gold is worth G = 0.04 (the bot's normal buy threshold per gold), times the stages it will still pay for
    /// (capped), times a discount for money that arrives late.
    ///   C01 1 gold per submission        C52 +3 resale per stage            C43 hand levels (flat)
    ///   C95 goldPerStage per stage       C96 extra interest: (wallet above 25) / 5, at most 5; 2.5 if the bot is comfortable enough to save
    ///   C97 3 gold x min(2, S x share of hands scoring at least half the next target)
    ///   C98 1 gold x controllers and die upgrades bought per stage
    ///   C99 1 gold per stage held on every controller it later sells: sells per stage x stages left x min(stages left, 6) / 2
    /// p1.0.0 (B = boss stages still followed by a shop, R = stages left):
    ///   C110 1.4 gold per stage (mean ItemValue of a random item)      C111 2.5 gold per shop (best of three items)
    ///   C112 2 gold per empty ordinary slot it can still fill          C113 1.5 gold per shop (a free die upgrade)
    ///   C114 0.15 per boss (the value given to one random enchant)      C115 0.004 per stage left (a random gold face now and then)
    ///   C116 4 gold per boss while there is room for another die        C133 5 gold per boss + 3 per pressure stage x acceptance rate
    ///   C140 2 gold per controller sold                                 C145 0.10 per glass die (+2 dice if C128 is held), at most 0.6
    ///   C131 half of ln(power without / with the coming boss modifier)  C130 half of ln 3 unless the estimate is already for a boss stage
    ///   C138 minus 3 gold per submission it will charge for</summary>
    double Bonus(string id, int nextIndex, RunState s)
    {
        int remaining = cfg.stages.Length - nextIndex, shops = Math.Max(0, remaining - 1), bosses = BossesAhead(nextIndex); const double G = 0.04;
        switch (id)
        {
            case "C01": return G * Math.Min(12, remaining) * 2.2 * 0.6;   // p0.8.0: income per submission only
            case "C52": return G * Math.Min(8, remaining) * 3 * 0.5;
            case "C43": return remaining >= 6 ? 0.45 : 0;
            case "C95": return G * Math.Min(12, remaining) * cfg.Controller("C95").parameters.goldPerStage * 0.6;
            case "C96":
                {
                    double top = cfg.Controller("C96").parameters.interestPrincipalAdd / (double)cfg.interestDivisor;
                    double extra = Math.Max(0, Math.Min(top, (hab.gold - InterestCap(s)) / (double)cfg.interestDivisor));
                    if (hab.comfy) extra = Math.Max(extra, top / 2);
                    return G * Math.Min(10, remaining) * extra * 0.6;
                }
            case "C97": { var p = cfg.Controller("C97").parameters; return G * Math.Min(10, remaining) * p.goldPerTrigger * Math.Min(p.maxTriggersPerStage, hab.subs * hab.half) * 0.5; }
            case "C98": return G * Math.Min(12, remaining) * -cfg.Controller("C98").parameters.priceDelta * hab.buys * 0.6;
            case "C99": return G * hab.sells * remaining * Math.Min(remaining, 6) / 2.0 * cfg.Controller("C99").parameters.saleBonusPerStage * 0.5;
            // p1.0.0
            case "C110": return G * Math.Min(12, remaining) * 1.4 * 0.6;
            case "C111": return G * Math.Min(12, shops) * 2.5 * 0.6;
            case "C112": { int free = Math.Max(0, s.controllerSlots - s.controllers.Count(c => c.enchantmentId != "slotless" && c.definitionId != "C112") - 1); return G * 2.0 * Math.Min(free, shops); }
            case "C113": return G * Math.Min(12, shops) * 1.5 * 0.6;
            case "C114": return 0.15 * bosses * 0.6;
            case "C115": return 0.004 * remaining;
            case "C116": return G * 4 * Math.Min(bosses, Math.Max(0, cfg.maxPermanentDice - s.dice.Count(d => d.lifetime == "permanent"))) * 0.6;
            case "C133": { var p = cfg.Controller("C133").parameters; return G * (p.goldPerBoss * bosses + p.goldPerPressure * hab.risk * StagesOfKind("pressure", nextIndex, cfg.stages.Length)) * 0.6; }
            case "C140": return G * hab.sells * 2 * Math.Min(10, remaining) * 0.6;
            case "C145": return Math.Min(0.6, 0.10 * (s.dice.Count(d => d.isGlass) + (s.controllers.Any(c => c.definitionId == "C128") ? 2 : 0)));
            case "C131": return 0.5 * -Math.Log(Math.Max(0.05, 1 - bossLoss));
            case "C130": return evalCtx != null && evalCtx.boss ? 0 : 0.5 * Math.Log(P.mult[130]);
            case "C138": return -G * Math.Min(8, remaining) * P.p138cost * Math.Max(1, hab.subs * 0.7) * 0.6;
            default: return 0;
        }
    }
    /// <summary>Principal that still earns interest: base cap, +25 per C96 source, +10 per O10 bought.</summary>
    long InterestCap(RunState s) => cfg.maxInterestPrincipal + (long)ControllerRules.ResolveSources(s.controllers).Count(x => x.abilityId == "C96") * cfg.Controller("C96").parameters.interestPrincipalAdd + (long)cfg.interestPrincipalStep * s.interestCapPurchases;

    /// <summary>Power with the mimic (C41) placed where it helps most. mode 0 = every position (the owned build, a mimic on offer);
    /// mode 1 = only where the mimic stands now (its order is re-optimised after every controller purchase);
    /// mode 2 = where it stands now, or directly left of the newest controller (controller offers).</summary>
    Pw BestMimicPower(RunState st, int mode = 0)
    {
        var mimic = st.controllers.FirstOrDefault(c => c.definitionId == "C41");
        if (mimic == null) return PowerOfState(st);
        var best = new Pw(); bool any = false; var others = st.controllers.Where(c => c != mimic).ToList();
        if (mode != 0 && others.Count > 0)
        {
            best = PowerOfState(st);
            if (mode == 2 && st.controllers[st.controllers.Count - 1] != mimic && st.controllers.IndexOf(mimic) != st.controllers.Count - 2)
            {
                var copy = st.Clone(); var m2 = copy.controllers.First(c => c.definitionId == "C41"); copy.controllers.Remove(m2); copy.controllers.Insert(copy.controllers.Count - 1, m2);
                var p = PowerOfState(copy); if (p.V > best.V) best = p;
            }
            return best;
        }
        for (int at = 0; at < others.Count; at++)
        {
            var copy = st.Clone(); var m2 = copy.controllers.First(c => c.definitionId == "C41"); copy.controllers.Remove(m2); copy.controllers.Insert(at, m2);
            var p = PowerOfState(copy); if (!any || p.V > best.V) { best = p; any = true; }
        }
        return any ? best : PowerOfState(st);
    }
    void OptimizeMimic()
    {
        var s = session.State; var mimic = s.controllers.FirstOrDefault(c => c.definitionId == "C41"); if (mimic == null || s.controllers.Count < 2) return;
        double best = -1; string[] bestOrder = null; var others = s.controllers.Where(c => c.instanceId != mimic.instanceId).Select(c => c.instanceId).ToList();
        for (int at = 0; at < others.Count; at++)
        {
            var order = others.ToList(); order.Insert(at, mimic.instanceId);
            var copy = s.Clone(); copy.controllers = order.Select(id => copy.controllers.First(c => c.instanceId == id)).ToList();
            double p = PowerOfState(copy).V; if (p > best) { best = p; bestOrder = order.ToArray(); }
        }
        if (bestOrder != null && !bestOrder.SequenceEqual(s.controllers.Select(c => c.instanceId))) Act(CommandKind.ReorderControllers, c => c.controllerIds = bestOrder);
    }
    /// <summary>C100 destroys its right neighbour at stage start. The bot never buys it; if it ever holds one it keeps
    /// it rightmost so that nothing is destroyed.</summary>
    void KeepAltarRight()
    {
        var s = session.State; if (s.phase != "Shop" && s.phase != "StagePreview") return;
        if (!s.controllers.Any(c => c.definitionId == "C100")) return;
        var order = s.controllers.Where(c => c.definitionId != "C100").Concat(s.controllers.Where(c => c.definitionId == "C100")).Select(c => c.instanceId).ToArray();
        if (!order.SequenceEqual(s.controllers.Select(c => c.instanceId))) Act(CommandKind.ReorderControllers, c => c.controllerIds = order);
    }

    /// <summary>Share of the build's power that the boss modifier of the coming boss stage takes away (0..1), from
    /// two estimates of the same build with the same simulated dice: 1 - V(with) / V(without), a TargetPercent
    /// modifier counted as power divided by the same factor. Purely economic modifiers come out as 0.</summary>
    double BossLoss(RunState s, string modifierId)
    {
        if (string.IsNullOrEmpty(modifierId)) return 0;
        double without = PowerOfState(s, new StageCtx { boss = true, disabled = Array.Empty<string>() }).V;
        double with = PowerOfState(s, new StageCtx { modifierId = modifierId, boss = true }).V * 100.0 / P.Fx(modifierId).targetPercent;
        return Math.Max(0, 1 - with / Math.Max(1e-9, without));
    }
    /// <summary>BossLoss for the build and boss of the current shop iteration, computed only when something asks for it
    /// (a scapegoat, a charm or a bribe on offer).</summary>
    RunState lossState; string lossModifier; double? lossValue;
    double bossLoss { get { if (lossValue == null) { string tag = powerTag; powerTag = "boss-loss"; lossValue = BossLoss(lossState, lossModifier); powerTag = tag; } return lossValue.Value; } }

    void Shop()
    {
        powerCache.Clear(); statsCache.Clear();
        long goldIn = session.State.gold; int refreshes = 0; bool evaluated = false, countered = false; var cands = new List<Cand>();
        double basePow = 0, ratio = 0, theta = 0; long reserve = 0; bool danger = false; int nextIndex = session.State.stage.stageIndex + 1;
        var nextDef = cfg.stages[nextIndex]; bool nextBoss = nextDef.stageKind == "boss";
        double target = D(nextDef.normalTarget), halfScore = target / Math.Max(1, cfg.Controller("C97").parameters.targetDivisor);
        var seenOffers = new HashSet<string>(); var doneGroups = new HashSet<string>();
        {
            var s0 = session.State; var loot = s0.shop.groups.FirstOrDefault(g => g.groupId == "LOOT");
            if (loot != null) { cur.lootKind = loot.kind; cur.lootOffered.AddRange(s0.shop.offers.Where(o => o.groupId == "LOOT").Select(OfferLabel)); }
        }
        for (int iter = 0; iter < 60; iter++)
        {
            var s = session.State; powerTag = "shop-base";
            foreach (var o in s.shop.offers.Where(o => o.category == "controller" && string.IsNullOrEmpty(o.groupId))) if (seenOffers.Add(o.offerId)) cur.offered.Add(o.contentId);
            // The stage the purchases are for. The cycle panel shows the boss of the cycle that contains the next stage.
            string bossMod = session.CycleView().Where(v => v.stageKind == "boss").Select(v => v.modifierId).FirstOrDefault();
            var normalCtx = new StageCtx { boss = nextBoss, disabled = Array.Empty<string>() }; var bossCtx = new StageCtx { modifierId = bossMod, boss = true };
            evalCtx = normalCtx;
            var stats = new HandStats();
            PowerOfState(s, null, stats, halfScore); var bp = BestMimicPower(s);
            ratio = bp.safe / target;
            lossState = s; lossModifier = bossMod; lossValue = null;
            if (nextBoss) ratio = PowerOfState(s, bossCtx).safe / (target * P.Fx(bossMod).targetPercent / 100.0);
            danger = ratio < 1.1; bool comfy = ratio > 2.5;
            theta = danger ? 0.012 : comfy ? 0.09 : 0.04;
            // When the boss is next and the build looks too weak for it, purchases are valued under its modifier.
            if (nextBoss && danger && !string.IsNullOrEmpty(bossMod)) { evalCtx = bossCtx; bp = BestMimicPower(s); }
            // C96 and O10 raise the interest principal cap, so gold is worth keeping up to the higher cap.
            long cap = InterestCap(s);
            reserve = opt.weak || danger ? 0 : comfy ? cap : (long)(cap * (ratio - 1.1) / 1.4);
            Observe(s, stats, danger, comfy);
            // Gains compare builds at their projected growth; the danger test above uses the build as it is today.
            basePow = bp.V;
            if (s.controllers.Any(c => Projectable(c.definitionId))) { var proj = s.Clone(); Project(proj, nextIndex); basePow = BestMimicPower(proj).V; }
            // Free things first: boss loot and controller gifts change what is worth buying.
            powerTag = "gifts";
            if (TakeGift(s, basePow, stats, nextIndex, danger, doneGroups)) { evaluated = false; continue; }
            powerTag = "offers";
            // Boss countermeasures, once per shop: I08 when the boss is next, otherwise a bribe (O09).
            if (!countered && CounterBoss(s, nextBoss, ratio)) { countered = true; evaluated = false; continue; }
            long spendable = s.gold - reserve;
            if (!evaluated) { cands = Evaluate(s, basePow, stats, nextIndex, null, null, spendable); evaluated = true; }
            Cand pick = null;
            foreach (var c in cands.Where(c => !c.stale && c.gain >= opt.minGain).OrderByDescending(c => c.gain / Math.Max(1, c.price)))
            {
                if (s.gold < c.price) continue;
                // Gold above the interest cap earns nothing, so any real improvement is worth buying with it.
                bool surplus = !opt.weak && s.gold - c.price >= cap;
                if (!surplus && (c.gain / Math.Max(1, c.price) < theta || s.gold - c.price < reserve)) continue;
                pick = c; break;
            }
            if (pick == null)
            {
                // Refresh when desperate, or when sitting on gold that no longer earns interest.
                bool rich = !opt.weak && s.gold - session.GetRefreshPrice() >= cap + 5;
                if (refreshes < 2 && ((danger && s.gold >= session.GetRefreshPrice() + 4) || rich))
                {
                    Act(CommandKind.RefreshControllers); refreshes++; cur.bought.Add("refresh"); evaluated = false; continue;
                }
                break;
            }
            // Re-check the pick against the current state: earlier purchases may have changed its value or its slot.
            var fresh = Evaluate(s, basePow, stats, nextIndex, pick.key, null, spendable).FirstOrDefault();
            pick.stale = true;
            if (fresh == null || fresh.gain < opt.minGain || s.gold < fresh.price) continue;
            if (!(!opt.weak && s.gold - fresh.price >= cap) && (fresh.gain / Math.Max(1, fresh.price) < theta || s.gold - fresh.price < reserve)) continue;
            fresh.buy(); cur.bought.Add(fresh.label);
            if (fresh.reevaluate) evaluated = false;   // a new slot changes what every controller offer costs
            if (fresh.label.StartsWith("C")) OptimizeMimic();
        }
        // Emergency consumables when the next target still looks out of reach (not when the next boss forbids items).
        {
            var s = session.State; string bossMod = session.CycleView().Where(v => v.stageKind == "boss").Select(v => v.modifierId).FirstOrDefault();
            evalCtx = new StageCtx { boss = nextBoss, disabled = Array.Empty<string>() };
            ratio = nextBoss ? PowerOfState(s, new StageCtx { modifierId = bossMod, boss = true }).safe / (target * P.Fx(bossMod).targetPercent / 100.0) : BestMimicPower(s).safe / target;
            bool barred = nextBoss && P.Fx(bossMod).noItems && !s.controllers.Any(c => c.definitionId == "C131");
            if (ratio < 1.0 && !barred)
                foreach (string id in new[] { "I06", "I09", "I07", "I02", "I12" })
                {
                    var now = session.State; var offer = now.shop.offers.FirstOrDefault(o => o.category == "consumable" && !o.sold && string.IsNullOrEmpty(o.groupId) && o.contentId == id);
                    if (offer == null || now.gold < session.GetOfferPrice(offer) || session.PurchaseError(offer.offerId) != null) continue;
                    if (id == "I12" && ItemValue("I12", now) < 2) continue;
                    Act(CommandKind.Buy, c => c.offerId = offer.offerId); cur.bought.Add(id);
                }
        }
        powerTag = "mimic"; OptimizeMimic(); KeepAltarRight(); powerTag = "preview";
        var end = session.State; cur.goldAfterShop = end.gold; cur.spent = goldIn - end.gold;
    }

    static string OfferLabel(ShopOffer o)
    {
        switch (o.category)
        {
            case "controller": return o.contentId + (o.controller == null || o.controller.enchantmentId == "none" ? "" : ":" + o.controller.enchantmentId);
            case "hand_upgrade": return "up:" + o.contentId;
            case "new_die": return "die";
            case "gold": return "GOLD";
            default: return o.contentId;
        }
    }

    /// <summary>Takes one free offer, if any is worth taking; returns true when the state changed.
    /// Every offer of a gift group is valued exactly like a shop offer (power gain at projected growth, plus the
    /// economy bonus); items by ItemValue x 0.04. Boss loot: the picks with the highest gains are compared with the
    /// gold option, worth lootGoldValue x 0.04 (x 0.012 when the next stage looks dangerous, where gold matters less
    /// than power); the gold is taken when it is worth more than everything else together, or when nothing helps.
    /// Controller gifts (C111 / C113) have no gold option: the best offer with a positive value is taken.</summary>
    bool TakeGift(RunState s, double basePow, HandStats stats, int nextIndex, bool danger, HashSet<string> done)
    {
        foreach (var group in s.shop.groups)
        {
            if (group.closed || group.picked >= group.picks || done.Contains(group.groupId)) continue;
            bool isLoot = group.groupId == "LOOT";
            var options = Evaluate(s, basePow, stats, nextIndex, null, group.groupId).Where(c => c.gain > 0.005).OrderByDescending(c => c.gain).ToList();
            var gold = s.shop.offers.FirstOrDefault(o => o.groupId == group.groupId && o.category == "gold" && !o.sold);
            double goldGain = gold != null && group.picked == 0 ? gold.basePrice * (danger ? 0.012 : 0.04) : -1;
            double best = options.Take(group.picks - group.picked).Sum(c => c.gain);
            if (goldGain >= 0 && (options.Count == 0 || best < goldGain))
            {
                if (Try(CommandKind.Buy, c => c.offerId = gold.offerId, "loot gold")) { cur.lootTaken.Add("GOLD"); return true; }
                done.Add(group.groupId); continue;
            }
            if (options.Count == 0) { done.Add(group.groupId); continue; }
            var take = options[0];
            try { take.buy(); }
            catch (Exception e) { if (log.rejected++ == 0) log.firstRejection = $"stage {s.stage.stageIndex + 1} gift {take.label}: {e.Message}"; done.Add(group.groupId); continue; }
            (isLoot ? cur.lootTaken : cur.gifts).Add(take.label);
            if (take.label.StartsWith("C")) OptimizeMimic();
            return true;
        }
        return false;
    }

    /// <summary>Against a boss modifier that hurts this build.
    ///   I08 (shelf): bought when the boss is the next stage, the modifier costs at least 25% and safe power under it is below 1.5x target.
    ///   O09 (bribe): bought when the modifier costs at least 30% and the boss is next and below 1.5x target, or when it costs at least 50%.
    /// Both need the gold; neither respects the interest reserve.</summary>
    bool CounterBoss(RunState s, bool nextBoss, double ratio)
    {
        if (s.controllers.Any(c => c.definitionId == "C131") || s.inventory.Any(x => x.contentId == "I08")) return false;
        var charm = !nextBoss || ratio >= 1.5 ? null : s.shop.offers.FirstOrDefault(o => o.category == "consumable" && !o.sold && string.IsNullOrEmpty(o.groupId) && o.contentId == "I08");
        if (charm != null && (s.gold < session.GetOfferPrice(charm) || session.PurchaseError(charm.offerId) != null)) charm = null;
        var bribe = s.shop.offers.FirstOrDefault(o => o.category == "service" && !o.sold && o.contentId == "O09");
        if (bribe != null && (s.gold < session.GetOfferPrice(bribe) || session.PurchaseError(bribe.offerId) != null)) bribe = null;
        if (charm == null && bribe == null) return false;
        double loss = bossLoss;
        if (charm != null && loss >= 0.25) { Act(CommandKind.Buy, c => c.offerId = charm.offerId); cur.bought.Add("I08"); return true; }
        if (bribe != null && ((loss >= 0.30 && nextBoss && ratio < 1.5) || loss >= 0.50)) { Act(CommandKind.Buy, c => c.offerId = bribe.offerId); cur.bought.Add("O09"); return true; }
        return false;
    }

    /// <summary>Faces a die upgrade is tried on. Trying every face of every die costs too many estimates, so each
    /// upgrade has a short list (the measured power gain then picks among them):
    ///   U03 / U04 / U06  every legal face of the oldest die that has one (as before)
    ///   U08 +1 pip       on the oldest die with a legal face: its lowest face and its highest face below 6
    ///   U09 -1 pip       on the oldest die with a legal face: its highest face and its lowest face above 1
    ///   U10 weight +1    on the oldest die with a legal face: its most valuable face (pip x gold) and a face of its most common pip
    ///   U11 mirror       on the oldest die with a legal face: the highest and the lowest face whose opposite differs</summary>
    IEnumerable<(DieInstance die, FaceState face)> UpgradeTargets(RunState s, string u)
    {
        var dice = s.dice.Where(d => d.lifetime == "permanent").OrderBy(d => d.createdSerial).ToArray();
        Func<DieInstance, FaceState[]> legal = d => d.faces.OrderBy(f => f.faceId, StringComparer.Ordinal).Where(f => session.IsUpgradeTargetLegal(u, d.dieId, f.faceId)).ToArray();
        if (u == "U03" || u == "U04" || u == "U06")
        {
            var die = dice.FirstOrDefault(d => legal(d).Length > 0); if (die == null) yield break;
            foreach (var f in legal(die)) yield return (die, f);
            yield break;
        }
        foreach (var die in dice.Where(d => legal(d).Length > 0).Take(1))
        {
            var faces = legal(die); FaceState a, b;
            switch (u)
            {
                case "U08": a = faces.OrderBy(f => f.permanentPip).First(); b = faces.OrderByDescending(f => f.permanentPip).First(); break;
                case "U09": a = faces.OrderByDescending(f => f.permanentPip).First(); b = faces.OrderBy(f => f.permanentPip).First(); break;
                case "U10": { int common = die.faces.GroupBy(f => f.permanentPip).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).First().Key; a = faces.OrderByDescending(f => f.permanentPip * (f.isGold ? 2 : 1)).First(); b = faces.FirstOrDefault(f => f.permanentPip == common) ?? a; break; }
                case "U11": a = faces.OrderByDescending(f => f.permanentPip).First(); b = faces.OrderBy(f => f.permanentPip).First(); break;
                default: yield break;
            }
            yield return (die, a); if (b != a) yield return (die, b);
        }
    }
    void ApplyUpgrade(RunState st, string u, string dieId, string faceId)
    {
        var die = st.dice.First(d => d.dieId == dieId); var f = die.faces.First(x => x.faceId == faceId);
        switch (u)
        {
            case "U03": f.baseColor = "red"; break; case "U04": f.baseColor = "blue"; break; case "U06": f.isGold = true; break;
            case "U08": f.permanentPip++; break; case "U09": f.permanentPip--; break; case "U10": f.baseWeight++; break;
            case "U11": { var o = die.faces.First(x => x.faceId == cfg.Opposite(faceId)); o.permanentPip = f.permanentPip; o.baseColor = f.baseColor; break; }
        }
    }

    /// <summary>Values the offers of the shelf (group == null) or of one gift group. Gift offers cost nothing; a
    /// controller gift that needs a slot is valued with the same replacement logic as a purchase.</summary>
    List<Cand> Evaluate(RunState s, double basePow, HandStats stats, int nextIndex, string onlyKey = null, string group = null, long spendable = long.MaxValue)
    {
        var list = new List<Cand>();
        // Offers the purchase rule could not accept whatever they turn out to be worth (they would leave less gold
        // than the reserve) are not valued at all. A controller may be paid for partly by the card it replaces.
        long tradeIn = s.controllers.Count == 0 ? 0 : s.controllers.Max(c => session.GetControllerSalePrice(c.instanceId));
        Func<RunState, int, double> gainOf = (st, mode) => { Project(st, nextIndex); return Math.Log(Math.Max(1e-9, BestMimicPower(st, mode).V) / Math.Max(1e-9, basePow)); };
        Func<RunState, double> gain = st => gainOf(st, 1);
        int ordinary = s.controllers.Count(c => c.enchantmentId != "slotless"); bool film = s.controllers.Any(c => c.definitionId == "C145");
        // With every slot used a new controller must replace one. Trying every owned card for every offer is too slow
        // with 5-7 slots, so only the two cards the build misses least (power without the card, plus its gold value)
        // are tried as the one to sell.
        // p1.0.0: with the total at the instance cap (slotless cards from O11 / C114 / loot make that common) any
        // card may be the one sold, slotless ones included; otherwise only cards that occupy an ordinary slot.
        bool capped = s.controllers.Count >= cfg.maxControllerInstances;
        ControllerInstance[] weakest = null;
        Func<ControllerInstance[]> victims = () =>
        {
            if (weakest != null) return weakest;
            string before = powerTag; powerTag = "victims";
            weakest = s.controllers.Where(c => capped || c.enchantmentId != "slotless")
                .Select(w => { var st = s.Clone(); st.controllers.RemoveAll(c => c.instanceId == w.instanceId); return (w, loss: -gain(st) + Bonus(w.definitionId, nextIndex, s)); })
                .OrderBy(x => x.loss).Take(2).Select(x => x.w).ToArray();
            powerTag = before; return weakest;
        };
        foreach (var offer in s.shop.offers.Where(o => !o.sold && (o.groupId ?? "") == (group ?? "")))
        {
            string key = offer.offerId; if (onlyKey != null && key != onlyKey) continue;
            long price = session.GetOfferPrice(offer);
            if (price - (offer.category == "controller" ? tradeIn : 0) > spendable) continue;
            Action plainBuy = () => Act(CommandKind.Buy, c => c.offerId = offer.offerId);
            switch (offer.category)
            {
                case "controller":
                    {
                        string id = offer.contentId; if (s.controllers.Any(c => c.definitionId == id)) break;
                        if (id == "C100") break;   // limitation: the bot never plays the sacrifice card
                        bool slotless = offer.controller.enchantmentId == "slotless";
                        string label = OfferLabel(offer);
                        // C128 turns one more die into glass every stage; without the film (C145) those dice soon break.
                        double scale = id == "C128" ? (film ? 0.8 : 0.15) : 1;
                        if (!capped && (slotless || ordinary < s.controllerSlots))
                        {
                            double g = gainOf(WithController(s, offer.controller, null), id == "C41" ? 0 : 2) * scale + Bonus(id, nextIndex, s);
                            list.Add(new Cand { key = key, label = label, gain = g, price = price, buy = plainBuy });
                        }
                        else
                        {
                            double bestG = double.NegativeInfinity; ControllerInstance victim = null;
                            foreach (var w in victims())
                            {
                                // Selling a slotless card frees no ordinary slot, so it cannot make room for an ordinary one when the slots are full.
                                if (!slotless && w.enchantmentId == "slotless" && ordinary >= s.controllerSlots) continue;
                                double g = gainOf(WithController(s, offer.controller, w.instanceId), id == "C41" ? 0 : 2) * scale + Bonus(id, nextIndex, s) - Bonus(w.definitionId, nextIndex, s);
                                if (g > bestG) { bestG = g; victim = w; }
                            }
                            if (victim == null) break;
                            long sell = session.GetControllerSalePrice(victim.instanceId); string vid = victim.instanceId, vdef = victim.definitionId;
                            list.Add(new Cand { key = key, label = label + "(replaces " + vdef + ")", gain = bestG, price = price - sell,
                                buy = () => { Act(CommandKind.Sell, c => c.itemInstanceId = vid); Act(CommandKind.Buy, c => c.offerId = offer.offerId); } });
                        }
                        break;
                    }
                case "hand_upgrade":
                    {
                        if (session.PurchaseError(offer.offerId) != null && s.gold >= price) break;
                        var st = s.Clone(); var entry = st.handLevels.Concat(st.colorLevels).First(x => x.handId == offer.contentId);
                        entry.level = Math.Min(entry.level + Math.Max(1, offer.levels), cfg.colorDefinitions.Any(c => c.id == offer.contentId) ? cfg.maxColorLevel : cfg.maxHandLevel);
                        list.Add(new Cand { key = key, label = "up:" + offer.contentId, gain = gain(st), price = price, buy = plainBuy });
                        break;
                    }
                case "new_die":
                    {
                        if (s.dice.Count(d => d.lifetime == "permanent") >= cfg.maxPermanentDice || s.dice.Count >= cfg.maxActiveDice) break;
                        var st = s.Clone(); var d = offer.die.Clone(); d.createdSerial = st.nextDieSerial; d.dieId = "DNEW"; d.lifetime = "permanent"; st.dice.Add(d);
                        list.Add(new Cand { key = key, label = "die", gain = gain(st), price = price, buy = plainBuy });
                        break;
                    }
                case "die_upgrade":
                    {
                        string u = offer.contentId; if (u == "U01" || u == "U02" || u == "U07") break;
                        double bestG = double.NegativeInfinity; string bd = null, bf = null;
                        if (u == "U05")
                        {
                            // Glass triples a die but it can break; the measured gain is halved (kept at 90% with the film C145).
                            var die = s.dice.Where(d => d.lifetime == "permanent" && !d.isGlass).OrderBy(d => d.createdSerial).FirstOrDefault(); if (die == null) break;
                            var st = s.Clone(); st.dice.First(d => d.dieId == die.dieId).isGlass = true; bestG = gain(st) * (film ? 0.9 : 0.5); bd = die.dieId; bf = die.faces[0].faceId;
                        }
                        else
                            foreach (var (die, f) in UpgradeTargets(s, u))
                            {
                                var st = s.Clone(); ApplyUpgrade(st, u, die.dieId, f.faceId);
                                double g = gain(st); if (g > bestG) { bestG = g; bd = die.dieId; bf = f.faceId; }
                            }
                        if (bd == null) break;
                        string dieId = bd, faceId = bf;
                        list.Add(new Cand { key = key, label = u, gain = bestG, price = price, buy = () => Act(CommandKind.Buy, c => { c.offerId = offer.offerId; c.targetDieId = dieId; c.targetFaceId = faceId; }) });
                        break;
                    }
                case "consumable":
                    // Only gift items are valued here; shelf items are bought by the emergency rule in Shop().
                    if (group == null || session.PurchaseError(offer.offerId) != null) break;
                    list.Add(new Cand { key = key, label = offer.contentId, gain = 0.04 * ItemValue(offer.contentId, s), price = price, buy = plainBuy });
                    break;
                case "service":
                    if (session.PurchaseError(offer.offerId) != null && s.gold >= price) break;
                    if (offer.contentId == "O05" && nextIndex < 14) list.Add(new Cand { key = key, label = "O05", gain = 0.06 * price, price = price, buy = plainBuy });
                    // p0.9.0: one more ordinary slot once every slot is used and at least three stages remain. Valued like
                    // O05 at a fixed gain per gold (0.10, just above the "comfortable" threshold), so it is bought with
                    // gold the bot does not need for its interest reserve.
                    if (offer.contentId == "O08" && ordinary >= s.controllerSlots && cfg.stages.Length - nextIndex >= 3 && s.controllers.Count < cfg.maxControllerInstances)
                        list.Add(new Cand { key = key, label = "O08", gain = 0.10 * price, price = price, reevaluate = true, buy = plainBuy });
                    // p1.0.0 O10: +10 interest principal = up to +2 gold per stage, valued like C96 (0.04 per gold, 60%).
                    // It only pays when the bot holds that much gold: counted in full when it is comfortable, otherwise
                    // by how far the wallet is already above the present cap.
                    if (offer.contentId == "O10")
                    {
                        double top = cfg.interestPrincipalStep / (double)cfg.interestDivisor, extra = Math.Max(0, Math.Min(top, (s.gold - price - InterestCap(s)) / (double)cfg.interestDivisor));
                        if (hab.comfy) extra = top;
                        list.Add(new Cand { key = key, label = "O10", gain = 0.04 * Math.Min(10, cfg.stages.Length - nextIndex - 1) * extra * 0.6, price = price, reevaluate = true, buy = plainBuy });
                    }
                    // p1.0.0 O11: a random enchant on a random plain controller. Expected value from the enchant weights
                    // (mult2 8%, submit1 18%, roll1 18%, base10 20%, slotless 36%): 0.14, plus 0.11 for the freed slot
                    // when every slot is used. Needs at least three controllers so the enchant lands on something useful.
                    if (offer.contentId == "O11" && s.controllers.Count >= 3)
                        list.Add(new Cand { key = key, label = "O11", gain = 0.14 + (ordinary >= s.controllerSlots ? 0.11 : 0), price = price, reevaluate = true, buy = plainBuy });
                    break;
            }
        }
        if (group == null && s.shop.selfStudyUsed < cfg.shop.selfStudyLimit)
        {
            var legal = session.LegalStudyHands(); int total = Math.Max(1, stats.prim.Take(13).Sum());
            var want = Enumerable.Range(0, 13).Where(i => stats.prim[i] * 8 >= total).Select(i => P.hId[i]).Concat(new[] { "all_blue", "all_red" }).Where(legal.Contains).ToArray();
            foreach (string id in want)
            {
                string key = "study:" + id; if (onlyKey != null && key != onlyKey) continue;
                if (session.GetStudyPrice(id) > spendable) continue;
                var st = s.Clone(); st.handLevels.Concat(st.colorLevels).First(x => x.handId == id).level++;
                list.Add(new Cand { key = key, label = key, gain = gain(st), price = session.GetStudyPrice(id), buy = () => Act(CommandKind.Study, c => c.handId = id) });
            }
        }
        return list;
    }
}
