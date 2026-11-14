// Full-run bot. Drives the real GameSession with legal commands only; it sees what a player sees
// (current faces, shop offers, prices) and never reads RNG state. Planning uses its own random stream.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DiceCraft.Core;

public sealed class StageLog
{
    public int stage, subsUsed, unusedSubs, dice, ctrl, slots;
    public bool risk, cleared;
    public double target, score, power, safe;
    public long goldStart, reward, unused, interest, pig, ctlIncome, goldAfterSettle, spent, goldAfterShop;
    public List<string> bought = new List<string>();
    public string[] controllers = Array.Empty<string>();
}
public sealed class RunLog
{
    public uint seed; public bool won; public int reached; public string fail; public double seconds;
    /// <summary>Submissions whose score the planner predicted before submitting (builds without the random C53),
    /// and how many of those the real session scored differently.</summary>
    public int predicted, mispredicted; public string firstMisprediction;
    public List<StageLog> stages = new List<StageLog>();
}

public sealed class BotOptions
{
    public bool weak;            // one-step look-ahead only, no interest reserve
    public int powerHands = 32;
    public double minGain = 0.06;   // below this a measured gain is indistinguishable from sampling noise
    public double riskFactor = 1.3;
}

/// <summary>What the bot saw in its simulated hands with the current build; feeds the growth and economy estimates.</summary>
public sealed class HandStats
{
    public int hands, noRed, red3, half; public double rolls;
    public int[] prim = new int[14];   // 13 = scatter
    public double ShareNoRed => hands == 0 ? 0 : noRed / (double)hands;
    public double ShareRed3 => hands == 0 ? 0 : red3 / (double)hands;
    public double ShareHalf => hands == 0 ? 0 : half / (double)hands;
    public double RollsPerHand => hands == 0 ? 3 : rolls / hands;
    /// <summary>Chance that two consecutive hands share a primary hand (scatter counts).</summary>
    public double ShareSame => hands == 0 ? 0 : prim.Sum(x => (double)x * x) / ((double)hands * hands);
}

public sealed class Runner
{
    readonly GameConfig cfg; readonly Params P; readonly BotOptions opt;
    GameSession session; int seq; RunLog log; StageLog cur;
    readonly Planner strong = new Planner(), cheap = new Planner { depthMax = 1, s0 = 6, fullEnum = 0 };
    ulong powerSeed;
    /// <summary>When set, every accepted command is recorded in the format the in-game capture tour replays.</summary>
    public List<Dictionary<string, object>> recorded;

    public Runner(GameConfig cfg, Params P, BotOptions opt) { this.cfg = cfg; this.P = P; this.opt = opt; if (opt.weak) { strong.depthMax = 1; strong.s0 = 8; } }

    CommandResult Act(CommandKind kind, Action<SessionCommand> configure = null)
    {
        var command = new SessionCommand { kind = kind, commandId = "b" + (++seq), expectedActionSequence = session.ActionSequence };
        configure?.Invoke(command);
        if (kind == CommandKind.ConfirmLocks) foreach (string id in command.dieIds) if (!session.TogglePending(id)) throw new Exception("Illegal pending " + id);
        var result = session.Execute(command);
        if (!result.accepted) throw new Exception("Rejected " + kind + ": " + result.error);
        recorded?.Add(new Dictionary<string, object> {
            ["kind"] = kind.ToString(), ["seed"] = command.seed, ["risk"] = command.risk, ["offerId"] = command.offerId, ["itemInstanceId"] = command.itemInstanceId,
            ["targetDieId"] = command.targetDieId, ["targetFaceId"] = command.targetFaceId, ["handId"] = command.handId,
            ["dieIds"] = command.dieIds ?? Array.Empty<string>(), ["targetPips"] = command.targetPips ?? Array.Empty<int>() });
        return result;
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
        for (int guard = 0; guard < 5000; guard++)
        {
            var s = session.State;
            switch (s.phase)
            {
                case "StagePreview": Preview(s); break;
                case "HandReady":
                    if (s.stage.qualified) Act(CommandKind.EndStage);
                    else { UseStageItems(s); Act(CommandKind.Roll); }
                    break;
                case "Selecting": Select(s); break;
                case "StageSettlement": Settle(s); Act(CommandKind.Continue); break;
                case "Shop": Shop(); Act(CommandKind.LeaveShop); break;
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
    /// <summary>Simulated hands cycle through the stage's submission positions, so "last submission" cards
    /// (C68, C102) pay on one hand in `submissions`.</summary>
    public Pw Power(Build b, int hands, ulong seed, HandStats stats = null, double halfScore = 0)
    {
        cheap.Set(b); cheap.needed = double.PositiveInfinity; cheap.rollRng = new Rng(seed); cheap.rng = new Rng(seed ^ 0xABCDEFUL);
        double sum = 0;
        for (int h = 0; h < hands; h++)
        {
            b.subsLeft = b.submissions - h % b.submissions;
            double v = handBuf[h] = cheap.PlayHand(out int prim, out int nSub); sum += v;
            if (stats != null && nSub > 0)
            {
                stats.hands++; stats.prim[prim < 0 ? 13 : prim]++; stats.rolls += cheap.lastRollsUsed;
                if (cheap.lastRedN == 0) stats.noRed++; if (cheap.lastRedCount >= 3) stats.red3++; if (halfScore > 0 && v >= halfScore) stats.half++;
            }
        }
        Array.Sort(handBuf, 0, hands);
        return new Pw { mean = sum / hands * b.submissions, safe = handBuf[hands / 4] * b.submissions };
    }
    /// <summary>Power() is deterministic for a given build (the hand seed is fixed per run), so results are reused
    /// within one shop visit and the following stage preview.</summary>
    readonly Dictionary<string, Pw> powerCache = new Dictionary<string, Pw>();
    readonly System.Text.StringBuilder keyBuf = new System.Text.StringBuilder();
    string Key(RunState st)
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
        k.Append('|').Append(st.gold).Append('|').Append(st.previousPrimaryHandId).Append('|').Append(st.stage == null ? 0 : st.stage.submissionOrdinal);
        return k.ToString();
    }
    Pw PowerOfState(RunState st, HandStats stats = null, double halfScore = 0)
    {
        string key = Key(st);
        if (stats == null && powerCache.TryGetValue(key, out var hit)) return hit;
        var pw = PowerOfStateUncached(st, stats, halfScore); powerCache[key] = pw; return pw;
    }
    Pw PowerOfStateUncached(RunState st, HandStats stats, double halfScore)
    {
        // A C45 that is not owned yet has no colour; value it as the average of both.
        var open = st.controllers.Where(c => c.definitionId == "C45" && string.IsNullOrEmpty(c.state.activeColor)).ToArray();
        if (open.Length == 0) return Power(Build.From(st, P, false), opt.powerHands, powerSeed, stats, halfScore);
        var total = new Pw();
        foreach (string col in new[] { "red", "blue" })
        {
            foreach (var c in open) c.state.activeColor = col;
            var p = Power(Build.From(st, P, false), opt.powerHands, powerSeed, stats, halfScore); total.mean += p.mean / 2; total.safe += p.safe / 2;
        }
        foreach (var c in open) c.state.activeColor = null;
        return total;
    }

    // ---------- stage ----------
    void Preview(RunState s)
    {
        KeepAltarRight();
        s = session.State;
        var def = cfg.stages[s.stage.stageIndex];
        cur = new StageLog { stage = s.stage.stageIndex + 1, goldStart = s.gold, dice = s.dice.Count, ctrl = s.controllers.Count, slots = s.controllerSlots, controllers = s.controllers.Select(c => c.definitionId + (c.enchantmentId == "none" ? "" : ":" + c.enchantmentId)).ToArray() };
        log.stages.Add(cur);
        var pw = PowerOfState(s); cur.power = pw.mean; cur.safe = pw.safe;
        if (def.riskEnabled && !opt.weak && pw.safe >= opt.riskFactor * D(def.riskTarget)) { Act(CommandKind.ChooseChallenge, c => c.risk = true); cur.risk = true; }
        cur.target = D(cur.risk ? def.riskTarget : def.normalTarget);
        Act(CommandKind.StartStage);
    }

    void UseStageItems(RunState s)
    {
        if (s.stage.submissionOrdinal != 0 || s.hand.hasRolled) return;
        foreach (string id in new[] { "I07", "I06", "I02" })
            foreach (var item in session.State.inventory.Where(x => x.contentId == id).ToArray())
            {
                var now = session.State;
                if (id == "I02" && (now.dice.Count >= cfg.maxActiveDice || now.dice.Count(d => d.lifetime == "stage_temporary") >= cfg.maxTemporaryDice)) continue;
                Act(CommandKind.UseItem, c => c.itemInstanceId = item.instanceId);
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
        double expected = strong.EvalMask(locked | add, alive & ~(locked | add), s.hand.rollsRemaining, s.hand.rollsUsed, face);
        var order = strong.bestOrd.Take(strong.bestN).Select(i => all[i].dieId).ToArray();
        if (!session.State.hand.orderedLockedIds.SequenceEqual(order)) Act(CommandKind.Reorder, c => c.dieIds = order);
        double real = D(Act(CommandKind.Submit).receipt.score);
        // Self-check: the planner's value for the submission it chose must be the score the real session awards.
        if (b.cnt[53] == 0)
        {
            log.predicted++;
            if (Math.Abs(Math.Floor(expected + 1e-6) - real) > 1e-9 * real + 0.5 && log.mispredicted++ == 0)
                log.firstMisprediction = $"stage {s.stage.stageIndex + 1} planner={expected:R} session={real:R} controllers=[{string.Join(",", s.controllers.Select(c => c.definitionId))}]";
        }
    }

    void Settle(RunState s)
    {
        var r = s.lastStageSettlement;
        cur.cleared = true; cur.score = D(r.score); cur.subsUsed = s.stage.submissionOrdinal; cur.unusedSubs = r.remainingSubmissions;
        cur.reward = r.baseReward; cur.unused = r.unusedSubmissionReward; cur.interest = r.interest; cur.ctlIncome = r.controllerIncome; cur.pig = r.goldBefore - cur.goldStart; cur.goldAfterSettle = r.goldAfter;
        cur.goldAfterShop = r.goldAfter;
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
        public double subs = 2.2, unusedSubs = 0.8, upgrades = 0.3, levels = 0.8, sells = 0.3, buys = 1.0;   // per stage
        public double rolls = 3, noRed = 0.1, red3 = 0.1, same = 0.3, half = 0.3;                                // per simulated hand
        public int glass; public long gold; public bool danger, comfy;
    }
    Habits hab = new Habits();
    static double Blend(double sum, int n, double prior) => (sum + prior * 3) / (n + 3);
    void Observe(RunState s, HandStats stats, bool danger, bool comfy)
    {
        var cleared = log.stages.Where(x => x.cleared).ToArray(); int n = cleared.Length;
        hab = new Habits
        {
            subs = Blend(cleared.Sum(x => (double)x.subsUsed), n, 2.2), unusedSubs = Blend(cleared.Sum(x => (double)x.unusedSubs), n, 0.8),
            upgrades = Blend(cleared.Sum(x => (double)x.bought.Count(l => l.StartsWith("U0"))), n, 0.3),
            levels = Blend(cleared.Sum(x => (double)x.bought.Count(l => l.StartsWith("up:") || l.StartsWith("study:"))), n, 0.8),
            sells = Blend(cleared.Sum(x => (double)x.bought.Count(l => l.Contains("(replaces"))), n, 0.3),
            buys = Blend(cleared.Sum(x => (double)x.bought.Count(l => l.StartsWith("C") || l.StartsWith("U0"))), n, 1.0),
            rolls = stats.RollsPerHand, noRed = stats.ShareNoRed, red3 = stats.ShareRed3, same = stats.ShareSame, half = stats.ShareHalf,
            glass = s.dice.Count(d => d.isGlass), gold = s.gold, danger = danger, comfy = comfy
        };
    }

    /// <summary>Growth cards are worth nothing at purchase, so the shop values every build at its projected growth:
    ///   growth' = min(cap, growth + rate x horizon),   horizon = min(stages still to play, 6; 2 if the next stage looks dangerous)
    /// with a per-stage rate estimated from the habits above (S = submissions used per stage):
    ///   C55 2*S                         C56 2*S*share(no red)            C57 3*S*share(red group >= 3)
    ///   C58 4                           C59 4*unused submissions         C60 2*die upgrades bought per stage
    ///   C61 2*levels bought per stage   C62 1*rolls per hand*S           C90 15*glass dice*break chance*rolls per hand*S/2
    ///   C54 3*S*share(same primary), and never above its steady state 3*p/(1-p) because a different hand resets it
    ///   C100 0 (the bot never feeds it)
    /// cap: 100 for multiplier cards (x6), 300 for base-add cards. Applied to owned and candidate cards alike, so an
    /// owned growth card is not sold for a card that is only stronger today.</summary>
    void Project(RunState st, int nextIndex)
    {
        double horizon = Math.Min(cfg.stages.Length - nextIndex, hab.danger ? 2 : 6);
        foreach (var c in st.controllers)
        {
            string id = c.definitionId; if (!ControllerRules.HasGrowth(id)) continue;
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
                default: rate = 0; break;
            }
            c.state.growth = (long)Math.Round(Math.Max(g, Math.Min(limit, g + rate * horizon)));
        }
    }

    /// <summary>Gold-equivalent value of cards that earn or save money instead of scoring, in log-power units:
    /// one gold is worth G = 0.04 (the bot's normal buy threshold per gold), times the stages it will still pay for
    /// (capped), times a discount for money that arrives late.
    ///   C01 1 gold per submission        C52 +3 resale per stage            C43 hand levels (flat)
    ///   C95 goldPerStage per stage       C96 extra interest: (wallet above 25) / 5, at most 5; 2.5 if the bot is comfortable enough to save
    ///   C97 3 gold x min(2, S x share of hands scoring at least half the next target)
    ///   C98 1 gold x controllers and die upgrades bought per stage
    ///   C99 1 gold per stage held on every controller it later sells: sells per stage x stages left x min(stages left, 6) / 2</summary>
    double Bonus(string id, int nextIndex)
    {
        int remaining = cfg.stages.Length - nextIndex; const double G = 0.04;
        switch (id)
        {
            case "C01": return G * Math.Min(12, remaining) * 2.2 * 0.6;   // p0.8.0: income per submission only
            case "C52": return G * Math.Min(8, remaining) * 3 * 0.5;
            case "C43": return remaining >= 6 ? 0.45 : 0;
            case "C95": return G * Math.Min(12, remaining) * cfg.Controller("C95").parameters.goldPerStage * 0.6;
            case "C96":
                {
                    double top = cfg.Controller("C96").parameters.interestPrincipalAdd / (double)cfg.interestDivisor;
                    double extra = Math.Max(0, Math.Min(top, (hab.gold - cfg.maxInterestPrincipal) / (double)cfg.interestDivisor));
                    if (hab.comfy) extra = Math.Max(extra, top / 2);
                    return G * Math.Min(10, remaining) * extra * 0.6;
                }
            case "C97": { var p = cfg.Controller("C97").parameters; return G * Math.Min(10, remaining) * p.goldPerTrigger * Math.Min(p.maxTriggersPerStage, hab.subs * hab.half) * 0.5; }
            case "C98": return G * Math.Min(12, remaining) * -cfg.Controller("C98").parameters.priceDelta * hab.buys * 0.6;
            case "C99": return G * hab.sells * remaining * Math.Min(remaining, 6) / 2.0 * cfg.Controller("C99").parameters.saleBonusPerStage * 0.5;
            default: return 0;
        }
    }
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

    void Shop()
    {
        powerCache.Clear();
        long goldIn = session.State.gold; int refreshes = 0; bool evaluated = false; var cands = new List<Cand>();
        double basePow = 0, ratio = 0, theta = 0; long reserve = 0; bool danger = false; int nextIndex = session.State.stage.stageIndex + 1;
        double target = D(cfg.stages[nextIndex].normalTarget), halfScore = target / Math.Max(1, cfg.Controller("C97").parameters.targetDivisor);
        for (int iter = 0; iter < 40; iter++)
        {
            var s = session.State;
            var stats = new HandStats();
            PowerOfState(s, stats, halfScore); var bp = BestMimicPower(s);
            ratio = bp.safe / target; danger = ratio < 1.1; bool comfy = ratio > 2.5;
            theta = danger ? 0.012 : comfy ? 0.09 : 0.04;
            // C96 raises the interest principal cap, so gold is worth keeping up to the higher cap.
            long cap = cfg.maxInterestPrincipal + (long)ControllerRules.ResolveSources(s.controllers).Count(x => x.abilityId == "C96") * cfg.Controller("C96").parameters.interestPrincipalAdd;
            reserve = opt.weak || danger ? 0 : comfy ? cap : (long)(cap * (ratio - 1.1) / 1.4);
            Observe(s, stats, danger, comfy);
            // Gains compare builds at their projected growth; the danger test above uses the build as it is today.
            basePow = bp.V;
            if (s.controllers.Any(c => ControllerRules.HasGrowth(c.definitionId))) { var proj = s.Clone(); Project(proj, nextIndex); basePow = BestMimicPower(proj).V; }
            if (!evaluated) { cands = Evaluate(s, basePow, stats, nextIndex); evaluated = true; }
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
            var fresh = Evaluate(s, basePow, stats, nextIndex, pick.key).FirstOrDefault();
            pick.stale = true;
            if (fresh == null || fresh.gain < opt.minGain || s.gold < fresh.price) continue;
            if (!(!opt.weak && s.gold - fresh.price >= cap) && (fresh.gain / Math.Max(1, fresh.price) < theta || s.gold - fresh.price < reserve)) continue;
            fresh.buy(); cur.bought.Add(fresh.label);
            if (fresh.reevaluate) evaluated = false;   // a new slot changes what every controller offer costs
            if (fresh.label.StartsWith("C")) OptimizeMimic();
        }
        // Emergency consumables when the next target still looks out of reach.
        {
            var s = session.State; ratio = BestMimicPower(s).safe / target;
            if (ratio < 1.0)
                foreach (string id in new[] { "I06", "I07", "I02" })
                {
                    var now = session.State; var offer = now.shop.offers.FirstOrDefault(o => o.category == "consumable" && !o.sold && o.contentId == id);
                    if (offer == null || now.gold < session.GetOfferPrice(offer) || session.PurchaseError(offer.offerId) != null) continue;
                    Act(CommandKind.Buy, c => c.offerId = offer.offerId); cur.bought.Add(id);
                }
        }
        OptimizeMimic(); KeepAltarRight();
        var end = session.State; cur.goldAfterShop = end.gold; cur.spent = goldIn - end.gold;
    }

    List<Cand> Evaluate(RunState s, double basePow, HandStats stats, int nextIndex, string onlyKey = null)
    {
        var list = new List<Cand>();
        Func<RunState, int, double> gainOf = (st, mode) => { Project(st, nextIndex); return Math.Log(Math.Max(1e-9, BestMimicPower(st, mode).V) / Math.Max(1e-9, basePow)); };
        Func<RunState, double> gain = st => gainOf(st, 1);
        int ordinary = s.controllers.Count(c => c.enchantmentId != "slotless");
        // With every slot used a new controller must replace one. Trying every owned card for every offer is too slow
        // with 5-7 slots, so only the two cards the build misses least (power without the card, plus its gold value)
        // are tried as the one to sell.
        ControllerInstance[] weakest = null;
        Func<ControllerInstance[]> victims = () => weakest ?? (weakest = s.controllers.Where(c => c.enchantmentId != "slotless")
            .Select(w => { var st = s.Clone(); st.controllers.RemoveAll(c => c.instanceId == w.instanceId); return (w, loss: -gain(st) + Bonus(w.definitionId, nextIndex)); })
            .OrderBy(x => x.loss).Take(2).Select(x => x.w).ToArray());
        foreach (var offer in s.shop.offers.Where(o => !o.sold))
        {
            string key = offer.offerId; if (onlyKey != null && key != onlyKey) continue;
            long price = session.GetOfferPrice(offer);
            switch (offer.category)
            {
                case "controller":
                    {
                        string id = offer.contentId; if (s.controllers.Any(c => c.definitionId == id)) break;
                        if (id == "C100") break;   // limitation: the bot never plays the sacrifice card
                        bool slotless = offer.controller.enchantmentId == "slotless";
                        string label = id + (offer.controller.enchantmentId == "none" ? "" : ":" + offer.controller.enchantmentId);
                        if (s.controllers.Count >= cfg.maxControllerInstances) break;
                        if (slotless || ordinary < s.controllerSlots)
                        {
                            double g = gainOf(WithController(s, offer.controller, null), id == "C41" ? 0 : 2) + Bonus(id, nextIndex);
                            list.Add(new Cand { key = key, label = label, gain = g, price = price, buy = () => Act(CommandKind.Buy, c => c.offerId = offer.offerId) });
                        }
                        else
                        {
                            double bestG = double.NegativeInfinity; ControllerInstance victim = null;
                            foreach (var w in victims())
                            {
                                double g = gainOf(WithController(s, offer.controller, w.instanceId), id == "C41" ? 0 : 2) + Bonus(id, nextIndex) - Bonus(w.definitionId, nextIndex);
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
                        var st = s.Clone(); st.handLevels.Concat(st.colorLevels).First(x => x.handId == offer.contentId).level++;
                        list.Add(new Cand { key = key, label = "up:" + offer.contentId, gain = gain(st), price = price, buy = () => Act(CommandKind.Buy, c => c.offerId = offer.offerId) });
                        break;
                    }
                case "new_die":
                    {
                        if (s.dice.Count(d => d.lifetime == "permanent") >= cfg.maxPermanentDice) break;
                        var st = s.Clone(); var d = offer.die.Clone(); d.createdSerial = st.nextDieSerial; d.dieId = "DNEW"; d.lifetime = "permanent"; st.dice.Add(d);
                        list.Add(new Cand { key = key, label = "die", gain = gain(st), price = price, buy = () => Act(CommandKind.Buy, c => c.offerId = offer.offerId) });
                        break;
                    }
                case "die_upgrade":
                    {
                        string u = offer.contentId; if (u == "U01" || u == "U02" || u == "U07") break;
                        double bestG = double.NegativeInfinity; string bd = null, bf = null;
                        if (u == "U05")
                        {
                            var die = s.dice.Where(d => d.lifetime == "permanent" && !d.isGlass).OrderBy(d => d.createdSerial).FirstOrDefault(); if (die == null) break;
                            var st = s.Clone(); st.dice.First(d => d.dieId == die.dieId).isGlass = true; bestG = gain(st) * 0.5; bd = die.dieId; bf = die.faces[0].faceId;
                        }
                        else
                        {
                            Func<FaceState, bool> legal = f => u == "U03" ? f.baseColor != "red" : u == "U04" ? f.baseColor != "blue" : !f.isGold;
                            var die = s.dice.Where(d => d.lifetime == "permanent" && d.faces.Any(legal)).OrderBy(d => d.createdSerial).FirstOrDefault(); if (die == null) break;
                            foreach (var f in die.faces.Where(legal))
                            {
                                var st = s.Clone(); var tf = st.dice.First(d => d.dieId == die.dieId).faces.First(x => x.faceId == f.faceId);
                                if (u == "U03") tf.baseColor = "red"; else if (u == "U04") tf.baseColor = "blue"; else tf.isGold = true;
                                double g = gain(st); if (g > bestG) { bestG = g; bd = die.dieId; bf = f.faceId; }
                            }
                        }
                        if (bd == null) break;
                        string dieId = bd, faceId = bf;
                        list.Add(new Cand { key = key, label = u, gain = bestG, price = price, buy = () => Act(CommandKind.Buy, c => { c.offerId = offer.offerId; c.targetDieId = dieId; c.targetFaceId = faceId; }) });
                        break;
                    }
                case "service":
                    if (offer.contentId == "O05" && nextIndex < 14) list.Add(new Cand { key = key, label = "O05", gain = 0.06 * price, price = price, buy = () => Act(CommandKind.Buy, c => c.offerId = offer.offerId) });
                    // p0.9.0: one more ordinary slot once every slot is used and at least three stages remain. Valued like
                    // O05 at a fixed gain per gold (0.10, just above the "comfortable" threshold), so it is bought with
                    // gold the bot does not need for its interest reserve.
                    if (offer.contentId == "O08" && ordinary >= s.controllerSlots && cfg.stages.Length - nextIndex >= 3 && s.controllers.Count < cfg.maxControllerInstances)
                        list.Add(new Cand { key = key, label = "O08", gain = 0.10 * price, price = price, reevaluate = true, buy = () => Act(CommandKind.Buy, c => c.offerId = offer.offerId) });
                    break;
            }
        }
        if (s.shop.selfStudyUsed < cfg.shop.selfStudyLimit)
        {
            var legal = session.LegalStudyHands(); int total = Math.Max(1, stats.prim.Take(13).Sum());
            var want = Enumerable.Range(0, 13).Where(i => stats.prim[i] * 8 >= total).Select(i => P.hId[i]).Concat(new[] { "all_blue", "all_red" }).Where(legal.Contains).ToArray();
            foreach (string id in want)
            {
                string key = "study:" + id; if (onlyKey != null && key != onlyKey) continue;
                var st = s.Clone(); st.handLevels.Concat(st.colorLevels).First(x => x.handId == id).level++;
                list.Add(new Cand { key = key, label = key, gain = gain(st), price = session.GetStudyPrice(id), buy = () => Act(CommandKind.Study, c => c.handId = id) });
            }
        }
        return list;
    }
}
