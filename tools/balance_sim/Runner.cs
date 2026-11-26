// Full-run bot. Drives the real GameSession with legal commands only; it sees what a player sees
// (current faces, shop offers, prices, the cycle panel) and never reads RNG state. Planning uses its own random stream.
// p1.1.0: each same-name card is an independent instance; boss loot is decided before other shop actions.
// p1.1.1: growth without a cap (start + catch-up + grown part), unique multiplier cards, the free cultivation slot,
// two-face and self-selected die upgrades, glass that breaks after a submission, UnlockDie (C154 / key ring), and the
// per-stage fields the target calibration reads (StageLog, CardSnap, ShelfSnap, UpgradeLog).
// Three-stage cycles, gift groups and heuristics are documented in README.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DiceCraft.Core;

/// <summary>p1.1.1: one card of the build as a stage is entered (or as the run ends). value = what a growth card is
/// worth now (start + catch-up + grown part: hundredths, or points for base growth; 0 for other cards), growthKind =
/// additive / base / multiplier / "" ; star = C137's pip (0 before the stage draws it); cultivated = permanent faces
/// a cultivation card has rewritten (0-24); slotFree = it takes no ordinary slot (slotless, or the free cultivation card).</summary>
public sealed class CardSnap
{
    public string instanceId, id, enchant, growthKind = "";
    public int position, star, cultivated; public long growth, catchup, value; public bool slotFree;
}
/// <summary>p1.1.1 BX-10: one controller of the first shelf of a shop, as the bot found it.</summary>
public sealed class ShelfSnap { public string id, enchant; public long price; public bool affordable, unique; }
/// <summary>p1.1.1 BX-14: one die upgrade transaction. via = shelf / self (self-selected) / free (gift or loot).</summary>
public sealed class UpgradeLog { public string id, via; public int faces; public long price; }

public sealed class StageLog
{
    // ---- p1.1.1 calibration fields ----
    /// <summary>The bot's estimate when the stage was entered, per hand: mean and 25th-percentile hand, and the
    /// submissions it counted on. handMeanOverTarget / handP25OverTarget divide by the target actually played
    /// (power / target = handMeanOverTarget x subsBudget).</summary>
    public double handMean, handP25, handMeanOverTarget, handP25OverTarget; public int subsBudget;
    /// <summary>Highest single submission of the stage (real session score).</summary>
    public double bestHand;
    /// <summary>The build as the stage was entered: cards in order, hand and colour levels above 0, dice count is `dice`, gold is `goldStart`.</summary>
    public List<CardSnap> build = new List<CardSnap>();
    public Dictionary<string, int> handLevels = new Dictionary<string, int>(), colorLevels = new Dictionary<string, int>();
    /// <summary>Gold faces and glass dice among the permanent dice as the stage was entered.</summary>
    public int goldFaces, glassDice, permanentDice;
    /// <summary>BX-17, boss stages only: for every growth card, power of the build without it / power with it (no
    /// modifier, mean stage total). Key = instanceId + ":" + definition.</summary>
    public Dictionary<string, double> growthShare;
    /// <summary>BX-10: the first shelf of the shop after this stage (before any refresh), and the gold the bot had then.</summary>
    public List<ShelfSnap> shelf = new List<ShelfSnap>(); public long shelfGold; public int shopCycle;
    /// <summary>BX-14: die upgrade transactions in the shop after this stage.</summary>
    public List<UpgradeLog> upgrades = new List<UpgradeLog>();
    /// <summary>Dice unlocked during the stage with the free unlock of C154 / with a key ring use (I13).</summary>
    public int unlocksFree, unlocksKey;
    /// <summary>Submitted glass dice that broke during the stage.</summary>
    public int glassBroken;
    public int stage, subsUsed, unusedSubs, dice, ctrl, slots, itemsUsed;
    /// <summary>normal / pressure / boss.</summary>
    public string kind;
    public bool risk, riskOffered, cleared;
    /// <summary>p1.1.1: a submission of this stage beat the run's highest single hand (what C125 grows on).</summary>
    public bool recordBroken;
    /// <summary>target = the target actually played (pressure x1.5, TargetPercent, after a lifted modifier);
    /// targetBase = the stage's normal target. power / safe = the bot's estimate for the stage as it was entered
    /// (modifier in force unless lifted); powerRaw / safeRaw = the same build without any modifier;
    /// powerMod / safeMod / targetMod = with the boss modifier in force (boss stages only, also when it was lifted).</summary>
    public double target, targetBase, score, power, safe, powerRaw, safeRaw, powerMod, safeMod, targetMod;
    /// <summary>Boss modifier of the stage; nullifiedBy = "C131" or "I08" when it was lifted.</summary>
    public string modifier, nullifiedBy;
    /// <summary>Loot kind offered in the shop after this (boss) stage, its offers, and what the bot took ("GOLD" = the gold option).</summary>
    public string lootKind;
    public string lootDecision;
    public int lootPicks, lootPicksAvailable;
    public long goldStart, reward, unused, interest, pig, ctlIncome, goldAfterSettle, spent, goldAfterShop;
    public List<string> bought = new List<string>(), gifts = new List<string>(), lootOffered = new List<string>(), lootTaken = new List<string>();
    /// <summary>offered = controllers seen on the shelf of the shop after this stage; granted = controllers received at
    /// stage end (C112); items = items used during the stage.</summary>
    public List<string> offered = new List<string>(), granted = new List<string>(), items = new List<string>();
    /// <summary>Accepted acquisitions with another copy held immediately before the purchase (after any sale).</summary>
    public List<string> duplicateBought = new List<string>(), duplicateLoot = new List<string>();
    public string[] controllers = Array.Empty<string>();
    /// <summary>--lab, boss stages only: for every modifier that could have been drawn for this cycle, the share of this
    /// build's power it takes away (0..1) with the controllers as they stand, and - for the modifiers that disable the
    /// leftmost controllers - the same after the best reordering the bot can find.</summary>
    public Dictionary<string, double> lab, labOrdered;
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
    /// <summary>Every rejected command issued in Shop, including mandatory commands. Expected 0.</summary>
    public int shopRejected; public string firstShopRejection;
    public List<string> shopRejections = new List<string>();
    /// <summary>Actual surviving copies at the terminal state, rather than the entering-stage snapshot.</summary>
    public Dictionary<string, int> finalCopies = new Dictionary<string, int>();
    /// <summary>Stage previews where the session's quoted budgets differ from the rules the bot plans with. Expected 0.</summary>
    public int quoteMismatch; public string firstQuoteMismatch;
    public List<StageLog> stages = new List<StageLog>();
    // ---- p1.1.1 ----
    /// <summary>Stage the run died in (0 = won) and its kind; the build at the terminal state (cultivation counts after the last stage).</summary>
    public int failStage; public string failKind;
    public List<CardSnap> finalBuild = new List<CardSnap>();
    public int unlocksFree, unlocksKey; public double highestHand;
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
    /// <summary>p1.1.1: sum of the additive totals (hundredths) of the simulated hands.</summary>
    public double additive;
    public double MeanAdditive => hands == 0 ? 100 : additive / hands;
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
    readonly Dictionary<string, long> paidThisShop = new Dictionary<string, long>();
    HashSet<string> enteringControllerIds = new HashSet<string>();
    ulong powerSeed; string powerTag = "preview";
    /// <summary>When set, every accepted command is recorded in the format the in-game capture tour replays.</summary>
    public List<Dictionary<string, object>> recorded;

    /// <summary>--prepared: before a boss whose modifier disables the leftmost controllers, move the controllers the build
    /// misses least to the left (a player who reads the stage intel). --lab: record the controlled modifier comparison.</summary>
    public static bool Prepared, Lab;
    /// <summary>--grant I13,C154: content every run starts with (a test switch for mechanisms the bot rarely reaches).</summary>
    public static string[] Grant = Array.Empty<string>();
    public Runner(GameConfig cfg, Params P, BotOptions opt) { this.cfg = cfg; this.P = P; this.opt = opt; if (opt.weak) { strong.depthMax = 1; strong.s0 = 8; } }

    CommandResult Send(CommandKind kind, Action<SessionCommand> configure)
    {
        var command = new SessionCommand { kind = kind, commandId = "b" + (++seq), expectedActionSequence = session.ActionSequence };
        configure?.Invoke(command);
        if (kind == CommandKind.ConfirmLocks) foreach (string id in command.dieIds) if (!session.TogglePending(id)) throw new Exception("Illegal pending " + id);
        var before = session.State;
        var offer = kind == CommandKind.Buy ? before.shop?.offers.FirstOrDefault(o => o.offerId == command.offerId) : null;
        long paid = offer == null ? 0 : session.GetOfferPrice(offer);
        bool duplicate = offer?.category == "controller" && before.controllers.Any(c => c.alive && c.definitionId == offer.contentId);
        var result = session.Execute(command);
        if (!result.accepted && before?.phase == "Shop")
        {
            string rejection = $"stage {before.stage.stageIndex + 1} {kind} {command.offerId ?? command.itemInstanceId ?? command.handId}: {result.error}";
            if (log.shopRejected++ == 0) log.firstShopRejection = rejection;
            log.shopRejections.Add(rejection);
        }
        if (result.accepted && duplicate) (offer.groupId == "LOOT" ? cur.duplicateLoot : cur.duplicateBought).Add(offer.contentId);
        if (result.accepted && offer?.category == "controller" && string.IsNullOrEmpty(offer.groupId)) paidThisShop[offer.controller.instanceId] = paid;
        if (result.accepted && offer?.groupId == "LOOT")
        {
            if (cur.lootDecision == null) cur.lootDecision = offer.category == "gold" ? "cash-out" : "pick";
            if (offer.category != "gold") cur.lootPicks++;
        }
        if (result.accepted)
        {
            recorded?.Add(new Dictionary<string, object> {
                ["kind"] = kind.ToString(), ["seed"] = command.seed, ["risk"] = command.risk, ["offerId"] = command.offerId, ["itemInstanceId"] = command.itemInstanceId,
                ["targetDieId"] = command.targetDieId, ["targetFaceId"] = command.targetFaceId, ["handId"] = command.handId,
                ["dieIds"] = command.dieIds ?? Array.Empty<string>(), ["targetPips"] = command.targetPips ?? Array.Empty<int>(),
                // p1.1.1: the faces of a die upgrade and the upgrade of a self-selected one (UnlockDie only needs targetDieId).
                ["upgradeId"] = command.upgradeId, ["targetFaceIds"] = command.targetFaceIds ?? Array.Empty<string>() });
            if (recorded != null && command.kind == CommandKind.ReorderControllers)
                recorded[recorded.Count - 1]["controllerIds"] = command.controllerIds;
        }
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
        try { return PlayRun(seed); }
        catch (Exception e)
        {
            log ??= new RunLog { seed = seed };
            log.fail = "ERROR " + e;
            log.reached = session?.State?.stage == null ? 0 : session.State.stage.stageIndex + 1;
            return log;
        }
        finally
        {
            if (log != null)
            {
                log.seconds = clock.Elapsed.TotalSeconds;
                var terminal = session?.State;
                if (terminal != null) log.finalCopies = terminal.controllers.Where(c => c.alive).GroupBy(c => c.definitionId).ToDictionary(g => g.Key, g => g.Count());
                if (terminal != null) try { log.finalBuild = Snap(terminal); } catch { }
            }
        }
    }
    RunLog PlayRun(uint seed)
    {
        session = new GameSession(cfg, new MemorySaveStore()); seq = 0; log = new RunLog { seed = seed };
        strong.rng = new Rng(seed * 31UL + 7); cheap.rng = new Rng(seed * 17UL + 3); powerSeed = seed * 1000003UL;
        Act(CommandKind.NewRun, c => c.seed = seed);
        if (Grant.Length > 0)
        {
            // Test switch (--grant): the run starts holding these items / cards, put into the freshly created state
            // the way the session itself creates them. Never used for a balance measurement.
            var st = session.State;
            foreach (string id in Grant)
            {
                int serial = st.nextItemSerial++; string instanceId = "ITEM_" + serial.ToString("D5");
                if (id.StartsWith("I", StringComparison.Ordinal)) st.inventory.Add(new OwnedItem { instanceId = instanceId, contentId = id, usesRemaining = Math.Max(1, cfg.Consumable(id).initialUses) });
                else st.controllers.Add(new ControllerInstance { instanceId = instanceId, acquiredSerial = serial, definitionId = id, enchantmentId = "none", state = new ControllerDynamicState { activeColor = id == "C45" ? "blue" : null } });
            }
            session = new GameSession(cfg, new MemorySaveStore(), st);
        }
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
                    log.reached = s.stage.stageIndex + 1; log.fail = s.failureReason; log.failStage = log.reached; log.failKind = cfg.stages[s.stage.stageIndex].stageKind; return log;
                default: throw new Exception("Unexpected phase " + s.phase);
            }
        }
        throw new Exception("Action loop");
    }

    static double D(string v) => double.Parse(v, CultureInfo.InvariantCulture);

    // ---------- power estimate ----------
    /// <summary>mean = expected stage total; safe = stage total if every hand were a 25th-percentile hand.
    /// V blends both so that swingy cards are not valued at their lucky average.</summary>
    public struct Pw
    {
        public double mean, safe;
        /// <summary>p1.1.1 output: the mean and the 25th-percentile hand behind mean / safe, and the submissions counted.</summary>
        public double hand, handSafe; public int subs;
        public double V => Math.Sqrt(Math.Max(1e-9, mean) * Math.Max(safe, mean * 0.02));
    }
    readonly double[] handBuf = new double[256];
    /// <summary>Simulated hands cycle through the stage's submission positions: the ordinal (C150, C153, C07, C127,
    /// FirstHalf / LaterHalf), the submissions left (C68, C102 pay on the last one) and the primary hands already
    /// used in the stage (NoRepeatHand).</summary>
    public Pw Power(Build b, int hands, ulong seed, HandStats stats = null, double halfScore = 0)
    {
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        cheap.Set(b); cheap.needed = double.PositiveInfinity; cheap.rollRng = new Rng(seed); cheap.rng = new Rng(seed ^ 0xABCDEFUL);
        double sum = 0; var starRng = new Rng(seed ^ 0x51A7UL); int allDice = (1 << b.dice.Length) - 1;
        for (int h = 0; h < hands; h++)
        {
            int position = h % b.submissions;
            b.subsLeft = b.submissions - position; b.ordinal = position;
            if (position == 0)
            {
                // A new simulated stage: no hand used yet, nothing scored (C156), every die back (glass), and a fresh
                // star pip for every C137 whose pip is not known yet (a card valued outside its stage).
                b.usedHands = 0; b.stageScore = 0; cheap.startAlive = allDice;
                for (int k = 0; k < b.star137.Length; k++) if (b.starOpen[k]) b.star137[k] = (byte)(1 + starRng.NextInt(6));
            }
            double v = handBuf[h] = cheap.PlayHand(out int prim, out int nSub); sum += v;
            b.stageScore += Math.Floor(v + 1e-6);
            // p1.1.1: a submitted glass die that breaks is gone for the rest of the stage (and, in a real run, for good).
            // With no die left the remaining hands of this simulated stage score nothing.
            cheap.startAlive &= ~cheap.lastBroken;
            if (prim >= 0) b.usedHands |= 1 << prim;
            if (stats != null && nSub > 0)
            {
                stats.hands++; stats.prim[prim < 0 ? 13 : prim]++; stats.rolls += cheap.lastRollsUsed; stats.dice += nSub; stats.redDice += cheap.lastRedN; stats.pool += cheap.lastPoolN; stats.additive += b.lastAdditive;
                if (cheap.lastRedN == 0) stats.noRed++; if (cheap.lastRedCount >= 3) stats.red3++; if (halfScore > 0 && v >= halfScore) stats.half++;
            }
        }
        Array.Sort(handBuf, 0, hands);
        double took = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalSeconds;
        log.powerCalls++; log.secPower += took; log.powerSecondsBy[powerTag] = log.powerSecondsBy.GetValueOrDefault(powerTag) + took; log.powerCallsBy[powerTag] = log.powerCallsBy.GetValueOrDefault(powerTag) + 1;
        return new Pw { mean = sum / hands * b.submissions, safe = handBuf[hands / 4] * b.submissions, hand = sum / hands, handSafe = handBuf[hands / 4], subs = b.submissions };
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
        foreach (var c in st.controllers) k.Append(c.instanceId).Append('=').Append(c.definitionId).Append(':').Append(c.enchantmentId).Append(c.alive ? '+' : '-').Append(c.state.growth).Append(',').Append(c.state.growthCatchup).Append(',').Append(c.state.starPip).Append(c.state.activeColor).Append(';');
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
        return new StageCtx { modifierId = mod, boss = ctx.boss, disabled = ControllerRules.DisabledBy(st.controllers, cfg, mod), capacity = ctx.capacity, rolls = ctx.rolls, submissions = ctx.submissions, target = ctx.target };
    }
    /// <summary>The build as it will stand once the stage has started: every C136 source adds a temporary die, and a
    /// real C137 without a star pip (shop, preview) is given pip 0 here; see StarPips for how it is valued.</summary>
    RunState AtStageStart(RunState st, StageCtx ctx)
    {
        if (!st.controllers.Any(c => c.definitionId == "C136")) return st;
        var sources = ControllerRules.ResolveSources(st.controllers, null, ctx == null ? null : ctx.disabled);
        int temps = sources.Count(x => x.abilityId == "C136") * cfg.Controller("C136").parameters.dicePerStage;
        if (temps == 0) return st;
        var copy = st.Clone();
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
            var from = seen.stats; stats.hands = from.hands; stats.noRed = from.noRed; stats.red3 = from.red3; stats.half = from.half; stats.dice = from.dice; stats.redDice = from.redDice; stats.pool = from.pool; stats.rolls = from.rolls; stats.additive = from.additive;
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
            total.hand += p.hand / 2; total.handSafe += p.handSafe / 2; total.subs = p.subs;
        }
        foreach (var c in open) c.state.activeColor = null;
        return total;
    }

    // ---------- stage ----------
    void Preview(RunState s)
    {
        KeepAltarRight(); if (Prepared) PrepareForBoss();
        s = session.State;
        int index = s.stage.stageIndex; var def = cfg.stages[index]; bool boss = def.stageKind == "boss";
        cur = new StageLog { stage = index + 1, kind = def.stageKind, goldStart = s.gold, dice = s.dice.Count, ctrl = s.controllers.Count, slots = s.controllerSlots, targetBase = D(def.normalTarget),
            controllers = s.controllers.Select(c => c.definitionId + (c.enchantmentId == "none" ? "" : ":" + c.enchantmentId)).ToArray() };
        enteringControllerIds = s.controllers.Select(c => c.instanceId).ToHashSet();
        cur.build = Snap(s);
        foreach (var h in s.handLevels) if (h.level > 0) cur.handLevels[h.handId] = h.level;
        foreach (var h in s.colorLevels) if (h.level > 0) cur.colorLevels[h.handId] = h.level;
        var permanent = s.dice.Where(d => d.lifetime == "permanent").ToArray();
        cur.permanentDice = permanent.Length; cur.glassDice = permanent.Count(d => d.isGlass); cur.goldFaces = permanent.Sum(d => d.faces.Count(f => f.isGold));
        log.stages.Add(cur);
        evalCtx = new StageCtx { boss = boss, disabled = Array.Empty<string>(), target = cur.targetBase };
        var raw = PowerOfState(s); cur.powerRaw = raw.mean; cur.safeRaw = raw.safe; var pw = raw;
        if (boss && !string.IsNullOrEmpty(s.stage.modifierId))
        {
            // The boss modifier as the cycle panel shows it, and the stage as the session quotes it (budgets, target,
            // disabled controllers; with a scapegoat the modifier is quoted as already lifted).
            string mod = s.stage.modifierId; var fx = P.Fx(mod); var entry = session.EntryView();
            cur.modifier = mod; cur.targetMod = cur.targetBase * fx.targetPercent / 100.0;
            var withMod = PowerOfState(s, new StageCtx { modifierId = mod, boss = true, disabled = ControllerRules.DisabledBy(s.controllers, cfg, mod), target = cur.targetMod });
            cur.powerMod = withMod.mean; cur.safeMod = withMod.safe;
            var quoted = new StageCtx { modifierId = string.IsNullOrEmpty(entry.modifierId) ? null : entry.modifierId, disabled = entry.disabledControllerIds, boss = true,
                capacity = session.EffectiveCapacity, rolls = session.InitialRolls, submissions = session.InitialSubmissions, target = D(entry.target) };
            CheckQuotes(s, entry, quoted);
            if (Lab) RecordLab(s, index);
            var st = s;
            if (entry.scapegoat) { cur.nullifiedBy = "C131"; st = s.Clone(); st.controllers.Remove(st.controllers.First(c => c.definitionId == "C131")); }
            pw = PowerOfState(st, quoted);
        }
        cur.power = pw.mean; cur.safe = pw.safe; cur.handMean = pw.hand; cur.handP25 = pw.handSafe; cur.subsBudget = pw.subs;
        if (boss) cur.growthShare = GrowthShares(s, raw);
        // Pressure challenge: accept only with a margin over the x1.5 target.
        if (def.riskEnabled)
        {
            cur.riskOffered = true;
            if (!opt.weak && pw.safe >= opt.riskFactor * D(def.riskTarget)) { Act(CommandKind.ChooseChallenge, c => c.risk = true); cur.risk = true; }
        }
        Act(CommandKind.StartStage);
        cur.target = D(session.State.stage.target);
        cur.handMeanOverTarget = cur.handMean / cur.target; cur.handP25OverTarget = cur.handP25 / cur.target;
    }

    /// <summary>p1.1.1 output: the cards of a state in their order (see CardSnap).</summary>
    List<CardSnap> Snap(RunState s)
    {
        string free = ControllerRules.FreeCultivationInstanceId(s.controllers, cfg); var list = new List<CardSnap>(); int position = 0;
        foreach (var c in s.controllers.Where(c => c.alive))
        {
            string id = c.definitionId; bool grows = ControllerRules.HasGrowth(id);
            list.Add(new CardSnap
            {
                instanceId = c.instanceId, id = id, enchant = string.IsNullOrEmpty(c.enchantmentId) ? "none" : c.enchantmentId, position = position++,
                growth = c.state.growth, catchup = c.state.growthCatchup, value = grows ? ControllerRules.GrowthValue(cfg, id, c.state) : 0,
                growthKind = ControllerRules.IsGrowthAdditive(id) ? "additive" : ControllerRules.IsGrowthBase(id) ? "base" : ControllerRules.IsGrowthMultiplier(id) ? "multiplier" : "",
                star = c.state.starPip, cultivated = c.state.cultivationFacesChanged, slotFree = c.enchantmentId == ControllerRules.SlotlessEnchantmentId || c.instanceId == free
            });
        }
        return list;
    }
    /// <summary>BX-17: for every growth card held, power without it / power with it (mean stage total, no modifier).
    /// One more power estimate per growth card, so it is only taken before boss stages.</summary>
    Dictionary<string, double> GrowthShares(RunState s, Pw with)
    {
        var shares = new Dictionary<string, double>(); string tag = powerTag; powerTag = "growth-share";
        foreach (var c in s.controllers.Where(c => c.alive && ControllerRules.HasGrowth(c.definitionId)))
        {
            var without = s.Clone(); without.controllers.RemoveAll(x => x.instanceId == c.instanceId);
            shares[c.instanceId + ":" + c.definitionId] = PowerOfState(without).mean / Math.Max(1e-9, with.mean);
        }
        powerTag = tag; return shares;
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
        var rngBefore = strong.rng;
        int add = strong.Decide(locked, alive, rolled, s.hand.rollsRemaining, s.hand.rollsUsed, face, s.hand.c05Used, out bool submit);
        if (locked != 0 && TryUnlock(s, all, locked, alive, face, strong.lastDecideValue, rngBefore)) return;   // the state changed: decide again
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
        var receipt = Act(CommandKind.Submit).receipt; double real = D(receipt.score);
        if (real > cur.bestHand) cur.bestHand = real; if (real > log.highestHand) log.highestHand = real;
        cur.glassBroken += receipt.effects.Count(e => e.kind == "DieBroken");
        // Self-check: the planner's value for the submission it chose must be the score the real session awards.
        if (b.cnt[53] == 0)
        {
            log.predicted++;
            if (Math.Abs(Math.Floor(expected + 1e-6) - real) > 1e-9 * real + 0.5 && log.mispredicted++ == 0)
                log.firstMisprediction = $"stage {s.stage.stageIndex + 1} modifier={session.State.stage.modifierId} planner={expected:R} session={real:R} controllers=[{string.Join(",", s.controllers.Select(c => c.definitionId))}]";
        }
    }
    /// <summary>p1.1.1 section 8: take one locked die back (UnlockDie) when that is clearly worth more than going on
    /// with the locks as they are. The session picks the source: the free unlock of C154 first, then a key ring use.
    /// Each locked die is valued by the same decision the planner makes for the hand (its utility: the score, capped
    /// at what the stage still needs), with that die back among the unlocked ones: with rolls left it may be thrown
    /// again, without rolls it may simply be left out of the submission. The free unlock is taken for a gain of 8%,
    /// a key ring use (one third of a 3-gold item) for 20%; never when the hand already clears the stage.
    /// The planner's look-ahead itself does not know that locks can be undone.</summary>
    bool TryUnlock(RunState s, DieInstance[] all, int locked, int alive, int[] face, double current, Rng rngBefore)
    {
        var quote = session.UnlockQuote(); if (!quote.available) return false;
        if (current >= strong.needed) return false;
        double margin = quote.sourceId == "C154" ? 1.08 : 1.20, best = current * margin + 1e-9; int pick = -1;
        var keep = strong.rng;
        for (int i = 0; i < all.Length; i++)
        {
            if ((locked >> i & 1) == 0) continue;
            int locked2 = locked & ~(1 << i);
            // The same planning random numbers as the decision it is compared with.
            strong.rng = rngBefore; strong.Decide(locked2, alive, alive & ~locked2, s.hand.rollsRemaining, s.hand.rollsUsed, face, s.hand.c05Used, out _);
            if (strong.lastDecideValue > best) { best = strong.lastDecideValue; pick = i; }
        }
        strong.rng = keep;
        if (pick < 0) return false;
        string dieId = all[pick].dieId, source = session.UnlockQuote(dieId).sourceId;
        if (!Try(CommandKind.UnlockDie, c => c.targetDieId = dieId, "unlock " + dieId)) return false;
        if (source == "C154") { cur.unlocksFree++; log.unlocksFree++; } else { cur.unlocksKey++; log.unlocksKey++; cur.itemsUsed++; cur.items.Add("I13"); }
        return true;
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
        cur.cleared = true; cur.score = D(r.score); cur.subsUsed = s.stage.submissionOrdinal; cur.unusedSubs = r.remainingSubmissions; cur.recordBroken = s.stage.handRecordBroken;
        cur.reward = r.baseReward; cur.unused = r.unusedSubmissionReward; cur.interest = r.interest; cur.ctlIncome = r.controllerIncome; cur.pig = r.goldBefore - cur.goldStart; cur.goldAfterSettle = r.goldAfter;
        cur.goldAfterShop = r.goldAfter;
        // Controllers that appeared during the stage can only come from C112.
        foreach (var c in s.controllers) if (!enteringControllerIds.Contains(c.instanceId)) cur.granted.Add(c.definitionId);
    }

    // ---------- shop ----------
    /// <summary>exact: the gain was computed, not measured from sampled hands, so the noise floor (minGain) does not apply.</summary>
    sealed class Cand { public string key, label; public double gain; public long price; public Action buy; public bool stale, reevaluate, exact; }
    /// <summary>A gain below this is not worth a command even when it is exact.</summary>
    const double ExactFloor = 0.01;
    double Floor(Cand c) => c.exact ? ExactFloor : opt.minGain;

    static RunState WithController(RunState s, ControllerInstance offer, string removeInstanceId)
    {
        var st = s.Clone(); if (removeInstanceId != null) st.controllers.RemoveAll(c => c.instanceId == removeInstanceId);
        // p1.1.1: a growth card on offer carries its catch-up, which the acquired card keeps; everything else starts fresh.
        var c2 = offer.Clone(); c2.state = new ControllerDynamicState { growthCatchup = offer.state?.growthCatchup ?? 0 }; c2.alive = true; st.controllers.Add(c2); return st;
    }
    /// <summary>Use the session quote, capped by what this bot actually paid in the open shop. The cap also keeps
    /// estimates conservative before the separately implemented C98 resale rule is integrated.</summary>
    long SaleEstimate(string instanceId)
    {
        long quoted = session.GetControllerSalePrice(instanceId);
        return paidThisShop.TryGetValue(instanceId, out long paid) ? Math.Min(quoted, paid) : quoted;
    }
    GameSession AfterSale(RunState s, string instanceId)
    {
        var quote = new GameSession(cfg, new MemorySaveStore(), s);
        var result = quote.Execute(new SessionCommand { kind = CommandKind.Sell, itemInstanceId = instanceId,
            commandId = "sim-sale-" + instanceId, expectedActionSequence = quote.ActionSequence });
        if (!result.accepted) throw new InvalidOperationException("Sale estimate rejected: " + result.error);
        var after = quote.State; after.gold = s.gold + SaleEstimate(instanceId);
        return new GameSession(cfg, new MemorySaveStore(), after);
    }

    // ---------- habits: simple observable quantities the growth and economy estimates are built from ----------
    /// <summary>Per-stage rates. From the run so far (blended with a prior worth three stages) and from the hands
    /// simulated with the current build.</summary>
    sealed class Habits
    {
        public double subs = 2.2, unusedSubs = 0.8, upgrades = 0.3, levels = 0.8, sells = 0.3, buys = 1.0, items = 0.3, risk = 0.5, record = 0.7;   // per stage
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
            record = Blend(cleared.Sum(x => x.recordBroken ? 1.0 : 0.0), n, 0.7),
            rolls = stats.RollsPerHand, noRed = stats.ShareNoRed, red3 = stats.ShareRed3, same = stats.ShareSame, half = stats.ShareHalf,
            dice = stats.DicePerHand, pool = stats.PoolPerHand, redShare = stats.RedShare,
            glass = s.dice.Count(d => d.isGlass), gold = s.gold, danger = danger, comfy = comfy
        };
    }
    int StagesOfKind(string kind, int from, int toExclusive) { int n = 0; for (int i = Math.Max(0, from); i < Math.Min(cfg.stages.Length, toExclusive); i++) if (cfg.stages[i].stageKind == kind) n++; return n; }
    /// <summary>Boss stages still to clear that are followed by a shop (the final boss pays nothing afterwards).</summary>
    int BossesAhead(int nextIndex) => StagesOfKind("boss", nextIndex, cfg.stages.Length - 1);

    /// <summary>Growth cards start small, so the shop values every build at its projected state (p1.1.1: no cap):
    ///   grown' = grown + events per stage x event size x horizon,   horizon = min(stages still to play, 6; 2 if the next stage looks dangerous)
    /// The events per stage of each card are listed at ProjectedGrowth. C141 decays instead (see below). Applied to
    /// owned and candidate cards alike, so an owned growth card is not sold for a card that is only stronger today.
    /// Face-growth cards: see ProjectFaces.</summary>
    void Project(RunState st, int nextIndex)
    {
        double horizon = Math.Min(cfg.stages.Length - nextIndex, hab.danger ? 2 : GrowthHorizon);
        var fade = cfg.Controller("C141").parameters;
        foreach (var c in st.controllers.ToArray())
        {
            string id = c.definitionId;
            if (id == "C141")
            {
                // p1.1.1: +4 falling by 1 per cleared stage. Valued at its mean bonus over the horizon (0 once it is
                // gone); dropped from the projection when that mean is below +1.
                int steps = Math.Max(1, (int)Math.Round(horizon)); double sum = 0;
                for (int k = 0; k < steps; k++) sum += Math.Max(0, fade.initialAdditive100 - (c.state.growth + k) * fade.decayPerClear100);
                double mean = sum / steps;
                if (mean < 100) { st.controllers.Remove(c); continue; }
                c.state.growth = Math.Max(c.state.growth, Math.Min(fade.expireGrowth - 1, (long)Math.Round((fade.initialAdditive100 - mean) / fade.decayPerClear100)));
                continue;
            }
            if (!ControllerRules.HasGrowth(id)) continue;
            c.state.growth = (long)Math.Round(Math.Max(c.state.growth, ProjectedGrowth(id, c.state.growth, nextIndex, horizon)));
        }
        ProjectFaces(st, horizon);
    }
    /// <summary>Stages a growth card is projected over when the next stage does not look dangerous (p1.1.1: no cap on
    /// the value itself, so the horizon is the only brake).</summary>
    public const double GrowthHorizon = 6;
    /// <summary>p1.1.1 section 5: the grown part (in the unit of the card: hundredths, or points for base cards) a
    /// growth card is expected to hold after `horizon` more stages. No cap. Events per stage come from the habits:
    ///   C55 S                      C56 S x share(no red)            C57 S x share(red group >= 3)
    ///   C58 1                      C59 unused submissions           C60 die upgrade transactions per shop
    ///   C61 levels gained per shop C62 rolls per hand x S           C51 share of shops with a sale (at most 1)
    ///   C125 share of cleared stages that beat the run's highest hand
    ///   C129 boss stages in the horizon / horizon                   C132 pressure stages x acceptance / horizon
    ///   C134 item uses per stage (a key ring use counts)            C100 0 (the bot never feeds it)
    ///   C54: N = horizon x S submissions of g := p (g + event) + (1 - p) g / 2, p = share of hands repeating the
    ///        previous primary hand (a different hand halves the grown part instead of clearing it).</summary>
    double ProjectedGrowth(string id, double g, int nextIndex, double horizon)
    {
        double step = cfg.Controller(id).parameters.growthEvent, rate;
        switch (id)
        {
            case "C54":
                {
                    double p = Math.Min(0.95, hab.same); int n = (int)Math.Round(horizon * hab.subs);
                    for (int k = 0; k < n; k++) g = p * (g + step) + (1 - p) * Math.Floor(g / 2);
                    return g;
                }
            case "C51": rate = step * Math.Min(1, hab.sells); break;
            case "C55": rate = step * hab.subs; break;
            case "C56": rate = step * hab.subs * hab.noRed; break;
            case "C57": rate = step * hab.subs * hab.red3; break;
            case "C58": rate = step; break;
            case "C59": rate = step * hab.unusedSubs; break;
            case "C60": rate = step * hab.upgrades; break;
            case "C61": rate = step * hab.levels; break;
            case "C62": rate = step * hab.rolls * hab.subs; break;
            case "C125": rate = step * hab.record; break;
            case "C129": rate = step * StagesOfKind("boss", nextIndex, Math.Min(cfg.stages.Length - 1, nextIndex + (int)horizon)) / Math.Max(1, horizon); break;
            case "C132": rate = step * hab.risk * StagesOfKind("pressure", nextIndex, nextIndex + (int)horizon) / Math.Max(1, horizon); break;
            case "C134": rate = step * hab.items; break;
            default: rate = 0; break;
        }
        return g + rate * horizon;
    }
    static bool Projectable(string id) => ControllerRules.HasGrowthCounter(id) || FaceGrowers.Contains(id);
    static readonly HashSet<string> FaceGrowers = new HashSet<string> { "C115", "C117", "C118", "C119", "C120", "C121", "C122", "C123", "C124", "C126", "C127" };

    /// <summary>Face-growth cards change dice slowly, so a build that holds one is valued with the dice it is expected
    /// to have after `horizon` stages. Stage-end cards follow their exact rule; after-score cards are approximated
    /// from the bot's habits (N = horizon x submissions per stage, D = dice per submission):
    /// p1.1.1 values:
    ///   C120 / C121  exact: every stage each permanent die's three lowest faces +1 / three highest faces -1
    ///   C115  one gold face and one +1 pip a stage (the rule: three random faces out of gold / red / blue / +1)
    ///   C117  1.2 N faces turn gold (and gain weight), highest pips first, dice in turn
    ///   C118  when at least 60% of the submitted dice share a colour: mixed opposite pairs take that colour, 0.5 N D pairs at most
    ///   C119  2 N faces take the die's highest pip (lowest for small-pip builds), one die after another
    ///   C122  0.6 N D faces become 5 outright, the face farthest from 5 first, dice in turn
    ///   C123  0.6 N D faces: a 1 gains 1, a 4-6 loses 2 but not below 3
    ///   C124  0.8 N D steps of +2 weight on each die's most valuable face, up to the weight cap
    ///   C126  0.8 N x pool dice per submission steps of +2 on the lowest faces
    ///   C127  0.5 N copies of the best face onto the worst face of another die (a new gold face gains weight)
    /// Duplicates and mimics add sources; idempotent changes are applied only once per event. C122 / C123 use
    /// a frozen face for each event, so two or more sources can overshoot their target.</summary>
    void ProjectFaces(RunState st, double horizon)
    {
        if (!st.controllers.Any(c => FaceGrowers.Contains(c.definitionId))) return;
        var sources = ControllerRules.ResolveSources(st.controllers);
        Func<string, int> count = id => sources.Count(x => x.abilityId == id);
        var dice = st.dice.Where(d => d.lifetime == "permanent").OrderBy(d => d.createdSerial).ToList(); if (dice.Count == 0) return;
        Func<DieInstance, FaceState[]> ordered = d => d.faces.OrderBy(f => f.faceId, StringComparer.Ordinal).ToArray();
        int stages = Math.Max(0, (int)Math.Round(horizon)); double subs = horizon * hab.subs;
        bool low = st.controllers.Any(c => c.definitionId == "C10" || c.definitionId == "C17" || c.definitionId == "C66");
        int k120 = count("C120"), k121 = count("C121"), per120 = cfg.Controller("C120").parameters.facesPerDie, per121 = cfg.Controller("C121").parameters.facesPerDie;
        Action<FaceState> gild = f => { if (!f.isGold) { f.isGold = true; f.baseWeight = Math.Min(cfg.maxFaceWeight, f.baseWeight + cfg.gildedFaceWeightDelta); } };
        for (int t = 0; t < stages && k120 + k121 > 0; t++)
        {
            // Exact: every stage each source raises the three lowest faces that can still rise (lowers the three highest).
            for (int k = 0; k < k120; k++) foreach (var d in dice) foreach (var f in ordered(d).Where(x => x.permanentPip < cfg.maxPipValue).OrderBy(x => x.permanentPip).Take(per120).ToArray()) f.permanentPip++;
            for (int k = 0; k < k121; k++) foreach (var d in dice) foreach (var f in ordered(d).Where(x => x.permanentPip > cfg.minPipValue).OrderByDescending(x => x.permanentPip).Take(per121).ToArray()) f.permanentPip--;
        }
        Func<double, int> steps = x => (int)Math.Round(Math.Max(0, x));
        // C115: three random faces a stage out of gold / red / blue / +1; projected as one gold face and one +1 a stage.
        for (int t = 0, n = stages * count("C115"); t < n; t++)
        {
            var g = ordered(dice[t % dice.Count]).Where(x => !x.isGold).OrderByDescending(x => x.permanentPip).FirstOrDefault(); if (g != null) gild(g);
            var up = ordered(dice[t % dice.Count]).Where(x => x.permanentPip < cfg.maxPipValue).OrderBy(x => x.permanentPip).FirstOrDefault(); if (up != null && !low) up.permanentPip++;
        }
        // C117 gilds the up faces of the first and the last submitted die (two faces a submission, fewer once many are gold).
        for (int t = 0, n = steps(subs * 1.2) * Math.Min(1, count("C117")); t < n; t++)
        { var f = ordered(dice[t % dice.Count]).Where(x => !x.isGold).OrderByDescending(x => x.permanentPip).FirstOrDefault(); if (f != null) gild(f); }
        if (count("C118") > 0 && (hab.redShare >= 0.6 || hab.redShare <= 0.4))
        {
            string colour = hab.redShare >= 0.6 ? "red" : "blue"; int left = steps(subs * hab.dice * 0.5);
            foreach (var d in dice)
                foreach (var f in ordered(d))
                {
                    var o = d.faces.First(x => x.faceId == cfg.Opposite(f.faceId));
                    if (left > 0 && f.baseColor != o.baseColor) { f.baseColor = colour; o.baseColor = colour; left--; }
                }
        }
        // C119: two faces of the first and of the last die a submission (up to four), projected as two a submission.
        for (int left = steps(subs * 2.0) * Math.Min(1, count("C119")), di = 0; left > 0 && di < dice.Count; di++)
        {
            var faces = ordered(dice[di]); int want = low ? faces.Min(x => x.permanentPip) : faces.Max(x => x.permanentPip);
            foreach (var f in faces) if (left > 0 && f.permanentPip != want) { f.permanentPip = want; left--; }
        }
        int target = cfg.Controller("C122").parameters.pip;
        int k122 = count("C122"), k123 = count("C123");
        // C122 (p1.1.1): the up face of every submitted die becomes 5 outright; 0.6 N D faces, dice in turn.
        for (int t = 0, n = k122 == 0 ? 0 : steps(subs * hab.dice * 0.6); t < n; t++)
        { var f = ordered(dice[t % dice.Count]).Where(x => x.permanentPip != target).OrderByDescending(x => Math.Abs(x.permanentPip - target)).FirstOrDefault(); if (f != null) f.permanentPip = target; }
        // C123 (p1.1.1): an up face showing 1 gains lowPipDelta, one showing 4-6 loses 2 but not below highPipFloor.
        var q123 = cfg.Controller("C123").parameters; int[] band = q123.pips;
        for (int t = 0, n = k123 == 0 ? 0 : steps(subs * hab.dice * 0.6); t < n; t++)
        {
            var f = ordered(dice[t % dice.Count]).Where(x => x.permanentPip < band[0] || x.permanentPip > band[1]).OrderByDescending(x => x.permanentPip < band[0] ? band[0] - x.permanentPip : x.permanentPip - band[1]).FirstOrDefault(); if (f == null) continue;
            f.permanentPip = f.permanentPip < band[0] ? Math.Min(cfg.maxPipValue, f.permanentPip + q123.lowPipDelta * k123) : Math.Max(q123.highPipFloor, f.permanentPip + q123.highPipDelta * k123);
        }
        int d124 = cfg.Controller("C124").parameters.delta, d126 = cfg.Controller("C126").parameters.delta;
        for (int t = 0, n = steps(subs * hab.dice * 0.8) * count("C124"); t < n; t++)
        { var f = ordered(dice[t % dice.Count]).Where(x => x.baseWeight < cfg.maxFaceWeight).OrderByDescending(x => (low ? 7 - x.permanentPip : x.permanentPip) + (x.isGold ? 3 : 0)).FirstOrDefault(); if (f != null) f.baseWeight = Math.Min(cfg.maxFaceWeight, f.baseWeight + d124); }
        for (int t = 0, n = steps(subs * hab.pool * 0.8) * count("C126"); t < n; t++)
        { var f = ordered(dice[t % dice.Count]).Where(x => x.permanentPip < cfg.maxPipValue).OrderBy(x => x.permanentPip).FirstOrDefault(); if (f != null) f.permanentPip = Math.Min(cfg.maxPipValue, f.permanentPip + d126); }
        // C127 (p1.1.1): every hand of at least two dice copies the first die's up face onto the last die's; 0.5 N copies.
        for (int t = 0, n = steps(subs * 0.5) * Math.Min(1, count("C127")); t < n && dice.Count > 1; t++)
        {
            var all = dice.SelectMany(d => d.faces.Select(f => (d, f))).ToArray();
            var best = all.OrderByDescending(x => (low ? 7 - x.f.permanentPip : x.f.permanentPip) + (x.f.isGold ? 3 : 0)).First();
            var worst = all.Where(x => x.d != best.d).OrderBy(x => (low ? 7 - x.f.permanentPip : x.f.permanentPip) + (x.f.isGold ? 3 : 0)).First();
            worst.f.permanentPip = best.f.permanentPip; worst.f.baseColor = best.f.baseColor; worst.f.texture = best.f.texture; if (best.f.isGold) gild(worst.f); else worst.f.isGold = false;
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
            case "I13": return s.inventory.Any(x => x.contentId == "I13") ? 0.5 : 2;   // key ring: three unlocks (see Select)
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
            case "C115": return 0;   // p1.1.1: valued through ProjectFaces (one gold face and one +1 a stage)
            case "C116": return G * 4 * Math.Min(bosses, Math.Max(0, cfg.maxPermanentDice - s.dice.Count(d => d.lifetime == "permanent"))) * 0.6;
            case "C133": { var p = cfg.Controller("C133").parameters; return G * (p.goldPerBoss * bosses + p.goldPerPressure * hab.risk * StagesOfKind("pressure", nextIndex, cfg.stages.Length)) * 0.6; }
            case "C140": return G * hab.sells * 2 * Math.Min(10, remaining) * 0.6;
            // p1.1.1 C154: one free unlock a hand; valued like one key ring use a stage (a third of a 3-gold item).
            case "C154": return G * Math.Min(12, remaining) * 1.0 * 0.6;
            case "C131": return 0.5 * -Math.Log(Math.Max(0.05, 1 - bossLoss));
            case "C130": return evalCtx != null && evalCtx.boss ? 0 : 0.5 * Math.Log(P.mult[130]);
            case "C138": return -G * Math.Min(8, remaining) * P.p138cost * Math.Max(1, hab.subs * 0.7) * 0.6;
            default: return 0;
        }
    }
    /// <summary>Economy heuristics for the whole build. Marginal values subtract this total before and after a
    /// change, so extra copies, mimics, capped rewards and while-held switches use the same path as first copies.</summary>
    double EconomyValue(RunState s, int nextIndex)
    {
        double total = 0; int remaining = cfg.stages.Length - nextIndex, shops = Math.Max(0, remaining - 1), bosses = BossesAhead(nextIndex);
        foreach (var group in ControllerRules.ResolveSources(s.controllers).Where(x => x.abilityId != null).GroupBy(x => x.abilityId))
        {
            string id = group.Key; int count = group.Count();
            switch (id)
            {
                case "C140": case "C154": count = 1; break;
                case "C111": case "C113": count = Math.Min(3, count); break;
                case "C110": count = Math.Min(s.consumableSlots, count); break;
                case "C131": count = Math.Min(bosses, s.controllers.Count(c => c.alive && c.definitionId == "C131")); break;
                case "C96":
                    double top = count * cfg.Controller(id).parameters.interestPrincipalAdd / (double)cfg.interestDivisor;
                    long baseCap = cfg.maxInterestPrincipal + (long)cfg.interestPrincipalStep * s.interestCapPurchases;
                    double extra = Math.Max(0, Math.Min(top, (hab.gold - baseCap) / (double)cfg.interestDivisor));
                    if (hab.comfy) extra = Math.Max(extra, top / 2);
                    total += 0.04 * Math.Min(10, remaining) * extra * 0.6; continue;
                case "C112":
                    int free = Math.Max(0, s.controllerSlots - ControllerRules.OrdinaryUsed(s.controllers, cfg));
                    total += 0.04 * 2 * Math.Min(free, count * shops); continue;
                case "C116":
                    total += 0.04 * 4 * Math.Min(count * bosses, Math.Max(0, cfg.maxPermanentDice - s.dice.Count(d => d.lifetime == "permanent"))) * 0.6; continue;
            }
            total += count * Bonus(id, nextIndex, s);
        }
        return total;
    }
    /// <summary>Principal that still earns interest: base cap, +25 per C96 source, +10 per O10 bought.</summary>
    long InterestCap(RunState s) => cfg.maxInterestPrincipal + (long)ControllerRules.ResolveSources(s.controllers).Count(x => x.abilityId == "C96") * cfg.Controller("C96").parameters.interestPrincipalAdd + (long)cfg.interestPrincipalStep * s.interestCapPurchases;

    /// <summary>Power with each mimic (C41) placed by coordinate search. mode 0 = every position (the owned build,
    /// a mimic on offer); mode 1 = current order; mode 2 = current order or left of the newest card. Every mimic
    /// is identified by instance ID, including adjacent chains and copies of the same target definition.</summary>
    Pw BestMimicPower(RunState st, int mode = 0)
    {
        BestMimicBuild(st, mode, out var best); return best;
    }
    RunState BestMimicBuild(RunState st, int mode, out Pw best)
    {
        best = PowerOfState(st);
        var ids = st.controllers.Where(c => c.definitionId == "C41").Select(c => c.instanceId).ToArray();
        if (mode == 1 || ids.Length == 0 || st.controllers.Count < 2) return st;
        var current = st; string newest = st.controllers.Last().instanceId;
        for (int pass = 0; pass < (ids.Length > 1 ? 2 : 1); pass++)
        {
            bool changed = false;
            foreach (string id in ids)
            {
                var others = current.controllers.Where(c => c.instanceId != id).Select(c => c.instanceId).ToList();
                var positions = mode == 2 ? new[] { others.IndexOf(newest) } : Enumerable.Range(0, others.Count + 1);
                RunState better = current;
                foreach (int at in positions)
                {
                    if (at < 0) continue;
                    var order = others.ToList(); order.Insert(at, id);
                    if (order.SequenceEqual(current.controllers.Select(c => c.instanceId))) continue;
                    var copy = current.Clone(); copy.controllers = order.Select(cid => copy.controllers.First(c => c.instanceId == cid)).ToList();
                    var p = PowerOfState(copy);
                    if (p.V > best.V * (1 + 1e-12)) { best = p; better = copy; }
                }
                if (better != current) { current = better; changed = true; }
            }
            if (!changed) break;
        }
        return current;
    }
    void OptimizeMimic()
    {
        var s = session.State;
        if (!s.controllers.Any(c => c.definitionId == "C41") || s.controllers.Count < 2) return;
        var best = BestMimicBuild(s, 0, out _); var order = best.controllers.Select(c => c.instanceId).ToArray();
        if (!order.SequenceEqual(s.controllers.Select(c => c.instanceId))) Act(CommandKind.ReorderControllers, c => c.controllerIds = order);
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

    /// <summary>The order that loses least to a DisableLeftmost modifier: the controllers whose single loss costs least go
    /// left (their relative order kept), accepted only if the estimate under the modifier really improves. Null = keep.</summary>
    string[] BestOrderAgainst(RunState s, string modifierId)
    {
        var def = cfg.bossModifiers.FirstOrDefault(m => m.id == modifierId);
        if (def == null || !def.effects.Contains("DisableLeftmost") || s.controllers.Count <= def.amount || s.controllers.Any(c => c.definitionId == "C131" || c.definitionId == "C100")) return null;
        var ctx = new StageCtx { modifierId = modifierId, boss = true, disabled = null };   // null: resolved per build by Effective
        double current = PowerOfState(s, ctx).V;
        var left = s.controllers.Select(c => new { c.instanceId, v = PowerOfState(s, new StageCtx { modifierId = modifierId, boss = true, disabled = new[] { c.instanceId } }).V })
            .OrderByDescending(x => x.v).Take(def.amount).Select(x => x.instanceId).ToList();
        var order = s.controllers.Where(c => left.Contains(c.instanceId)).Concat(s.controllers.Where(c => !left.Contains(c.instanceId))).Select(c => c.instanceId).ToArray();
        if (order.SequenceEqual(s.controllers.Select(c => c.instanceId))) return null;
        var copy = s.Clone(); copy.controllers = order.Select(id => copy.controllers.First(c => c.instanceId == id)).ToList();
        return PowerOfState(copy, ctx).V > current * 1.001 ? order : null;
    }
    void PrepareForBoss()
    {
        var s = session.State; if (s.phase != "Shop" && s.phase != "StagePreview") return;
        int index = s.phase == "Shop" ? s.stage.stageIndex + 1 : s.stage.stageIndex;
        if (index >= cfg.stages.Length || cfg.stages[index].stageKind != "boss") return;
        string mod = session.CycleView().Where(v => v.stageKind == "boss").Select(v => v.modifierId).FirstOrDefault(); if (string.IsNullOrEmpty(mod)) return;
        string tag = powerTag; powerTag = "boss-order"; var order = BestOrderAgainst(s, mod); powerTag = tag;
        if (order != null) Act(CommandKind.ReorderControllers, c => c.controllerIds = order);
    }
    /// <summary>--lab: the same build under every modifier this cycle could have drawn.</summary>
    void RecordLab(RunState s, int stageIndex)
    {
        int cycle = stageIndex / 3 + 1; bool final = cycle == cfg.stages.Length / 3; string tag = powerTag; powerTag = "lab";
        cur.lab = new Dictionary<string, double>(); cur.labOrdered = new Dictionary<string, double>();
        foreach (var m in cfg.bossModifiers.Where(m => final ? m.tier == 4 : m.tier < 4 && m.minCycle <= cycle))
        {
            cur.lab[m.id] = BossLoss(s, m.id, true);
            var order = BestOrderAgainst(s, m.id);
            if (order != null) { var copy = s.Clone(); copy.controllers = order.Select(id => copy.controllers.First(c => c.instanceId == id)).ToList(); cur.labOrdered[m.id] = BossLoss(copy, m.id, true); }
            else if (m.effects.Contains("DisableLeftmost")) cur.labOrdered[m.id] = cur.lab[m.id];
        }
        powerTag = tag;
    }

    /// <summary>Share of the build's power that the boss modifier of the coming boss stage takes away (0..1), from
    /// two estimates of the same build with the same simulated dice: 1 - V(with) / V(without), a TargetPercent
    /// modifier counted as power divided by the same factor. Purely economic modifiers come out as 0.</summary>
    /// <para><paramref name="exact"/> also takes away the controllers the modifier disables (by position or rarity).
    /// The published bot (p1.0.0 evidence) valued the modifier without them: a StageCtx starts with an empty disabled
    /// list, so <see cref="Effective"/> never resolved it. That stays as it was so the published runs reproduce;
    /// the modifier lab always measures exactly and the prepared bot (--prepared) decides on the exact figure.</para>
    double BossLoss(RunState s, string modifierId, bool exact = false)
    {
        if (string.IsNullOrEmpty(modifierId)) return 0;
        double without = PowerOfState(s, new StageCtx { boss = true, disabled = Array.Empty<string>() }).V;
        double with = PowerOfState(s, new StageCtx { modifierId = modifierId, boss = true, disabled = exact ? ControllerRules.DisabledBy(s.controllers, cfg, modifierId) : Array.Empty<string>() }).V * 100.0 / P.Fx(modifierId).targetPercent;
        return Math.Max(0, 1 - with / Math.Max(1e-9, without));
    }
    /// <summary>BossLoss for the build and boss of the current shop iteration, computed only when something asks for it
    /// (a scapegoat, a charm or a bribe on offer).</summary>
    RunState lossState; string lossModifier; double? lossValue;
    double bossLoss { get { if (lossValue == null) { string tag = powerTag; powerTag = "boss-loss"; lossValue = BossLoss(lossState, lossModifier, Prepared); powerTag = tag; } return lossValue.Value; } }

    void Shop()
    {
        powerCache.Clear(); statsCache.Clear(); paidThisShop.Clear();
        long goldIn = session.State.gold; int refreshes = 0; bool evaluated = false, countered = false; var cands = new List<Cand>();
        double basePow = 0, ratio = 0, theta = 0; long reserve = 0; bool danger = false; int nextIndex = session.State.stage.stageIndex + 1;
        var nextDef = cfg.stages[nextIndex]; bool nextBoss = nextDef.stageKind == "boss";
        double target = D(nextDef.normalTarget), halfScore = target / Math.Max(1, cfg.Controller("C97").parameters.targetDivisor);
        var seenOffers = new HashSet<string>(); var doneGroups = new HashSet<string>();
        {
            var s0 = session.State; var loot = s0.shop.groups.FirstOrDefault(g => g.groupId == "LOOT");
            if (loot != null) { cur.lootKind = loot.kind; cur.lootPicksAvailable = loot.picks; cur.lootOffered.AddRange(s0.shop.offers.Where(o => o.groupId == "LOOT").Select(OfferLabel)); }
            // BX-10: the first shelf as the bot found it (before loot, purchases or a refresh).
            cur.shelfGold = s0.gold; cur.shopCycle = session.ShopCycleIn(s0);
            foreach (var o in s0.shop.offers.Where(o => o.category == "controller" && string.IsNullOrEmpty(o.groupId) && !o.sold))
            {
                long price = session.GetOfferPrice(o);
                cur.shelf.Add(new ShelfSnap { id = o.contentId, enchant = string.IsNullOrEmpty(o.controller?.enchantmentId) ? "none" : o.controller.enchantmentId, price = price, affordable = s0.gold >= price, unique = ControllerRules.IsUniqueController(o.contentId, cfg) });
            }
        }
        for (int iter = 0; iter < 60; iter++)
        {
            var s = session.State; powerTag = "shop-base";
            foreach (var o in s.shop.offers.Where(o => o.category == "controller" && string.IsNullOrEmpty(o.groupId))) if (seenOffers.Add(o.offerId)) cur.offered.Add(o.contentId);
            // The stage the purchases are for. The cycle panel shows the boss of the cycle that contains the next stage.
            string bossMod = session.CycleView().Where(v => v.stageKind == "boss").Select(v => v.modifierId).FirstOrDefault();
            var normalCtx = new StageCtx { boss = nextBoss, disabled = Array.Empty<string>() }; var bossCtx = new StageCtx { modifierId = bossMod, boss = true, disabled = Prepared ? null : Array.Empty<string>() };
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
            if (session.IsLootPending) throw new InvalidOperationException("Boss loot remains unresolved after the loot decision.");
            powerTag = "offers";
            // Boss countermeasures, once per shop: I08 when the boss is next, otherwise a bribe (O09).
            if (!countered && CounterBoss(s, nextBoss, ratio)) { countered = true; evaluated = false; continue; }
            long spendable = s.gold - reserve;
            if (!evaluated) { cands = Evaluate(s, basePow, stats, nextIndex, null, null, spendable); evaluated = true; }
            Cand pick = null;
            foreach (var c in cands.Where(c => !c.stale && c.gain >= Floor(c)).OrderByDescending(c => c.gain / Math.Max(1, c.price)))
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
            if (fresh == null || fresh.gain < Floor(fresh) || s.gold < fresh.price) continue;
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
                foreach (string id in new[] { "I06", "I09", "I07", "I02", "I12", "I13" })
                {
                    var now = session.State; var offer = now.shop.offers.FirstOrDefault(o => o.category == "consumable" && !o.sold && string.IsNullOrEmpty(o.groupId) && o.contentId == id);
                    if (offer == null || now.gold < session.GetOfferPrice(offer) || session.PurchaseError(offer.offerId) != null) continue;
                    if (id == "I12" && ItemValue("I12", now) < 2) continue;
                    if (id == "I13" && now.inventory.Any(x => x.contentId == "I13")) continue;   // p1.1.1: one key ring is enough
                    Act(CommandKind.Buy, c => c.offerId = offer.offerId); cur.bought.Add(id);
                }
        }
        // p1.1.1 key ring (I13): bought with gold that earns no interest any more, one at a time, never against a NoItems boss.
        {
            var now = session.State; string bossMod = session.CycleView().Where(v => v.stageKind == "boss").Select(v => v.modifierId).FirstOrDefault();
            var ring = now.shop.offers.FirstOrDefault(o => o.category == "consumable" && !o.sold && string.IsNullOrEmpty(o.groupId) && o.contentId == "I13");
            bool barred = nextBoss && !string.IsNullOrEmpty(bossMod) && P.Fx(bossMod).noItems;
            if (ring != null && !barred && !opt.weak && !now.inventory.Any(x => x.contentId == "I13") && now.inventory.Count < now.consumableSlots
                && now.gold - session.GetOfferPrice(ring) >= InterestCap(now) && session.PurchaseError(ring.offerId) == null)
            { Act(CommandKind.Buy, c => c.offerId = ring.offerId); cur.bought.Add("I13"); }
        }
        powerTag = "mimic"; OptimizeMimic(); KeepAltarRight(); if (Prepared) PrepareForBoss(); powerTag = "preview";
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
        // Only LOOT is legal until its first pick or cash-out. Afterwards its remaining picks are still offered.
        foreach (var group in s.shop.groups.OrderBy(g => g.groupId == "LOOT" ? 0 : 1))
        {
            if (group.closed || group.picked >= group.picks || done.Contains(group.groupId)) continue;
            bool isLoot = group.groupId == "LOOT";
            if (session.IsLootPending && !isLoot) continue;
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
            case "U03": f.baseColor = "red"; break; case "U04": f.baseColor = "blue"; break;
            // p1.1.1 section 7.2: a face that turns gold gains weight (up to the cap).
            case "U06": if (!f.isGold) { f.isGold = true; f.baseWeight = Math.Min(cfg.maxFaceWeight, f.baseWeight + cfg.gildedFaceWeightDelta); } break;
            case "U08": f.permanentPip = Math.Min(cfg.maxPipValue, f.permanentPip + 1); break; case "U09": f.permanentPip = Math.Max(cfg.minPipValue, f.permanentPip - 1); break;
            case "U10": f.baseWeight = Math.Min(cfg.maxFaceWeight, f.baseWeight + 1); break;
            case "U11": { var o = die.faces.First(x => x.faceId == cfg.Opposite(faceId)); o.permanentPip = f.permanentPip; o.baseColor = f.baseColor; break; }
        }
    }

    /// <summary>p1.1.1: where a die upgrade goes and what it is worth (shelf offers, gifts and the self-selected ones
    /// share this). dieId = null: no use for it. A per-face upgrade takes one or two faces of one die (section 7.1).
    ///   U06 gold   computed, not sampled: the die whose chance of showing a gold face rises most (its two highest
    ///              non-gold faces; lowest for small-pip builds), gain = ln(1 + q x dP x goldAdd / (100 + A)) plus
    ///              q x dP x ln(step) per C89 source, with q = share of the dice that are submitted, dP = the rise of
    ///              that chance including the +1 weight of a gilded face, A = the mean additive total of the bot's hands.
    ///   U05 glass  the oldest plain permanent die; the measured gain while it lives and the measured loss once it has
    ///              broken are weighted by the share of the horizon it survives (15% per submission).
    ///   others     each face of the short list (UpgradeTargets) alone, then the best two together; measured.
    ///   U01 / U02 / U07 are never used.</summary>
    (string dieId, string[] faces, double gain, bool exact) PlanUpgrade(RunState s, string u, HandStats stats, Func<RunState, double> gain, int nextIndex)
    {
        if (u == "U01" || u == "U02" || u == "U07") return (null, null, 0, false);
        var permanent = s.dice.Where(d => d.lifetime == "permanent").OrderBy(d => d.createdSerial).ToArray();
        if (u == "U05")
        {
            var die = permanent.FirstOrDefault(d => !d.isGlass && session.IsUpgradeTargetLegal(u, d.dieId, null)); if (die == null || permanent.Length < 2) return (null, null, 0, false);
            var glass = s.Clone(); glass.dice.First(d => d.dieId == die.dieId).isGlass = true;
            var gone = s.Clone(); gone.dice.RemoveAll(d => d.dieId == die.dieId);
            double horizon = Math.Min(cfg.stages.Length - nextIndex, hab.danger ? 2 : GrowthHorizon), n = Math.Max(1, horizon * hab.subs * Math.Min(1, hab.dice / Math.Max(1, s.dice.Count)));
            double keep = 1 - P.glassBreak, alive = (1 - Math.Pow(keep, n)) / (P.glassBreak * n);
            return (die.dieId, Array.Empty<string>(), alive * gain(glass) + (1 - alive) * gain(gone), false);
        }
        int maxFaces = Math.Max(1, session.MaxSelectableFaces(u));
        if (u == "U06")
        {
            bool low = s.controllers.Any(c => c.definitionId == "C10" || c.definitionId == "C17" || c.definitionId == "C66");
            double bestRise = 0; string bd = null; string[] bf = null;
            foreach (var die in permanent)
            {
                var legal = session.LegalUpgradeFaces(u, die.dieId); if (legal.Length == 0) continue;
                var pick = die.faces.Where(f => legal.Contains(f.faceId)).OrderByDescending(f => low ? -f.permanentPip : f.permanentPip).Take(maxFaces).ToArray();
                double total = die.faces.Sum(f => (double)f.baseWeight), golden = die.faces.Where(f => f.isGold).Sum(f => (double)f.baseWeight), before = golden / total;
                foreach (var f in pick) { int w = Math.Min(cfg.maxFaceWeight, f.baseWeight + cfg.gildedFaceWeightDelta); total += w - f.baseWeight; golden += w; }
                double rise = golden / total - before;
                if (rise > bestRise) { bestRise = rise; bd = die.dieId; bf = pick.Select(f => f.faceId).OrderBy(x => x, StringComparer.Ordinal).ToArray(); }
            }
            if (bd == null) return (null, null, 0, false);
            double q = Math.Min(1, hab.dice / Math.Max(1, s.dice.Count)), k89 = ControllerRules.ResolveSources(s.controllers).Count(x => x.abilityId == "C89");
            double g = Math.Log(1 + q * bestRise * P.goldAdd / (100 + stats.MeanAdditive)) + k89 * q * bestRise * Math.Log(P.p89step);
            return (bd, bf, g, true);
        }
        double bestG = double.NegativeInfinity; string dieId = null; var singles = new List<(string face, double g)>();
        foreach (var (die, f) in UpgradeTargets(s, u))
        {
            var st = s.Clone(); ApplyUpgrade(st, u, die.dieId, f.faceId);
            double g = gain(st); singles.Add((f.faceId, g)); dieId = die.dieId; if (g > bestG) bestG = g;
        }
        if (dieId == null) return (null, null, 0, false);
        var ranked = singles.OrderByDescending(x => x.g).ToArray(); string[] faces = { ranked[0].face };
        if (maxFaces >= 2 && ranked.Length >= 2)
        {
            var both = new[] { ranked[0].face, ranked[1].face }.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var st = s.Clone(); foreach (string f in both) ApplyUpgrade(st, u, dieId, f);
            double g = gain(st); if (g > bestG) { bestG = g; faces = both; }
        }
        return (dieId, faces, bestG, false);
    }
    /// <summary>BX-14: die upgrade transactions of the shop after the current stage, by how they were paid for.</summary>
    void NoteUpgrade(string u, int faces, long price, string via)
    {
        cur.upgrades.Add(new UpgradeLog { id = u, via = via, faces = faces, price = price });
    }

    /// <summary>Values the offers of the shelf (group == null) or of one gift group. Gift offers cost nothing; a
    /// controller gift that needs a slot is valued with the same sell-then-buy logic as a purchase.</summary>
    List<Cand> Evaluate(RunState s, double basePow, HandStats stats, int nextIndex, string onlyKey = null, string group = null, long spendable = long.MaxValue)
    {
        var list = new List<Cand>();
        // Offers the purchase rule could not accept whatever they turn out to be worth (they would leave less gold
        // than the reserve) are not valued at all. A real sale may finance a purchase when a slot must be freed;
        // buying a same-name copy itself never replaces a card or grants a trade-in credit.
        long maxSale = s.controllers.Count == 0 ? 0 : s.controllers.Max(c => SaleEstimate(c.instanceId));
        double baseEconomy = EconomyValue(s, nextIndex);
        Func<RunState, double> economyGain = st => EconomyValue(st, nextIndex) - baseEconomy;
        Func<RunState, int, double> gainOf = (st, mode) => { Project(st, nextIndex); return Math.Log(Math.Max(1e-9, BestMimicPower(st, mode).V) / Math.Max(1e-9, basePow)); };
        Func<RunState, double> gain = st => gainOf(st, 1);
        // p1.1.1 section 6: the leftmost cultivation card without a slotless enchant takes no ordinary slot, so whether
        // a card fits is asked of the build as it would stand (a new card goes to the right end).
        int ordinary = ControllerRules.OrdinaryUsed(s.controllers, cfg);
        Func<RunState, bool> fits = st => st.controllers.Count <= cfg.maxControllerInstances && ControllerRules.OrdinaryUsed(st.controllers, cfg) <= s.controllerSlots;
        // With every slot used a new controller needs one to be sold. Trying every owned card for every offer is too slow
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
                .Select(w => { var st = s.Clone(); st.controllers.RemoveAll(c => c.instanceId == w.instanceId); double bonus = economyGain(st); return (w, loss: -gain(st) - bonus); })
                .OrderBy(x => x.loss).Take(2).Select(x => x.w).ToArray();
            powerTag = before; return weakest;
        };
        var saleQuotes = new Dictionary<string, GameSession>();
        foreach (var offer in s.shop.offers.Where(o => !o.sold && (o.groupId ?? "") == (group ?? "")))
        {
            string key = offer.offerId; if (onlyKey != null && key != onlyKey) continue;
            long price = session.GetOfferPrice(offer);
            if (price - (offer.category == "controller" ? maxSale : 0) > spendable) continue;
            Action plainBuy = () => Act(CommandKind.Buy, c => c.offerId = offer.offerId);
            switch (offer.category)
            {
                case "controller":
                    {
                        string id = offer.contentId;
                        if (id == "C100") break;   // limitation: the bot never plays the sacrifice card
                        // p1.1.1 section 9.3: a unique multiplier card that is already held cannot be bought or claimed again.
                        if (ControllerRules.OwnsUniqueController(s.controllers, id, cfg)) break;
                        string label = OfferLabel(offer);
                        var direct = capped ? null : WithController(s, offer.controller, null);
                        if (direct != null && fits(direct))
                        {
                            if (price > spendable) break;
                            var candidate = direct; double bonus = economyGain(candidate);
                            double g = gainOf(candidate, id == "C41" ? 0 : 2) + bonus;
                            list.Add(new Cand { key = key, label = label, gain = g, price = price, buy = plainBuy });
                        }
                        else
                        {
                            double bestG = double.NegativeInfinity; ControllerInstance victim = null; long net = 0;
                            foreach (var w in victims())
                            {
                                if (!saleQuotes.TryGetValue(w.instanceId, out var quote)) saleQuotes[w.instanceId] = quote = AfterSale(s, w.instanceId);
                                long cost = quote.GetOfferPrice(offer) - SaleEstimate(w.instanceId);
                                if (cost > spendable || cost > s.gold) continue;
                                var candidate = WithController(quote.State, offer.controller, null);
                                // Selling a card that took no ordinary slot (slotless, or the free cultivation card) may not make room.
                                if (!fits(candidate)) continue;
                                double bonus = economyGain(candidate);
                                double g = gainOf(candidate, id == "C41" ? 0 : 2) + bonus;
                                if (g > bestG) { bestG = g; victim = w; net = cost; }
                            }
                            if (victim == null) break;
                            string vid = victim.instanceId, vdef = victim.definitionId;
                            list.Add(new Cand { key = key, label = label + "(replaces " + vdef + ")", gain = bestG, price = net,
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
                        string u = offer.contentId;
                        var plan = PlanUpgrade(s, u, stats, gain, nextIndex); if (plan.dieId == null) break;
                        string via = group == null ? "shelf" : "free";
                        list.Add(new Cand { key = key, label = u, gain = plan.gain, price = price, exact = plan.exact,
                            buy = () => { Act(CommandKind.Buy, c => { c.offerId = offer.offerId; c.targetDieId = plan.dieId; c.targetFaceIds = plan.faces; }); NoteUpgrade(u, plan.faces.Length, price, via); } });
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
        // p1.1.1 section 9.2: up to selfDieUpgradeLimit self-selected die upgrades a shop, one more gold than the shelf
        // price, two faces at a time. Tried: gold (valued analytically), +1 pip and the two dyes (measured).
        if (group == null && session.CustomDieUpgradesRemaining > 0 && !session.IsLootPending)
            foreach (var option in session.CustomDieUpgradeOptions())
            {
                string u = option.upgradeId; if (u != "U06" && u != "U08" && u != "U03" && u != "U04") continue;
                string key = "self:" + u; if (onlyKey != null && key != onlyKey) continue;
                if (option.error != null || !option.hasTarget || option.price > spendable) continue;
                var plan = PlanUpgrade(s, u, stats, gain, nextIndex); if (plan.dieId == null) continue;
                long price = option.price;
                list.Add(new Cand { key = key, label = u + "@self", gain = plan.gain, price = price, exact = plan.exact, reevaluate = true,
                    buy = () => { Act(CommandKind.CustomDieUpgrade, c => { c.upgradeId = u; c.targetDieId = plan.dieId; c.targetFaceIds = plan.faces; }); NoteUpgrade(u, plan.faces.Length, price, "self"); } });
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
