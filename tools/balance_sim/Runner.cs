// Full-run bot. Drives the real GameSession with legal commands only; it sees what a player sees
// (current faces, shop offers, prices) and never reads RNG state. Planning uses its own random stream.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DiceCraft.Core;

public sealed class StageLog
{
    public int stage, subsUsed, dice, ctrl;
    public bool risk, cleared;
    public double target, score, power, safe;
    public long goldStart, reward, unused, interest, pig, goldAfterSettle, spent, goldAfterShop;
    public List<string> bought = new List<string>();
    public string[] controllers = Array.Empty<string>();
}
public sealed class RunLog
{
    public uint seed; public bool won; public int reached; public string fail;
    public List<StageLog> stages = new List<StageLog>();
}

public sealed class BotOptions
{
    public bool weak;            // one-step look-ahead only, no interest reserve
    public int powerHands = 32;
    public double minGain = 0.06;   // below this a measured gain is indistinguishable from sampling noise
    public double riskFactor = 1.3;
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

    void Act(CommandKind kind, Action<SessionCommand> configure = null)
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
    }

    public RunLog Play(uint seed)
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
    public Pw Power(Build b, int hands, ulong seed, int[] freq = null)
    {
        cheap.Set(b); cheap.needed = double.PositiveInfinity; cheap.rollRng = new Rng(seed); cheap.rng = new Rng(seed ^ 0xABCDEFUL);
        double sum = 0;
        for (int h = 0; h < hands; h++) { handBuf[h] = cheap.PlayHand(out int prim, out _); sum += handBuf[h]; if (freq != null && prim >= 0) freq[prim]++; }
        Array.Sort(handBuf, 0, hands);
        return new Pw { mean = sum / hands * b.submissions, safe = handBuf[hands / 4] * b.submissions };
    }
    Pw PowerOfState(RunState st, int[] freq = null)
    {
        // A C45 that is not owned yet has no colour; value it as the average of both.
        var open = st.controllers.Where(c => c.definitionId == "C45" && string.IsNullOrEmpty(c.state.activeColor)).ToArray();
        if (open.Length == 0) return Power(Build.From(st, P, false), opt.powerHands, powerSeed, freq);
        var total = new Pw();
        foreach (string col in new[] { "red", "blue" })
        {
            foreach (var c in open) c.state.activeColor = col;
            var p = Power(Build.From(st, P, false), opt.powerHands, powerSeed, freq); total.mean += p.mean / 2; total.safe += p.safe / 2;
        }
        foreach (var c in open) c.state.activeColor = null;
        return total;
    }

    // ---------- stage ----------
    void Preview(RunState s)
    {
        var def = cfg.stages[s.stage.stageIndex];
        cur = new StageLog { stage = s.stage.stageIndex + 1, goldStart = s.gold, dice = s.dice.Count, ctrl = s.controllers.Count, controllers = s.controllers.Select(c => c.definitionId + (c.enchantmentId == "none" ? "" : ":" + c.enchantmentId)).ToArray() };
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
        strong.Set(b); strong.needed = Math.Max(1, D(s.stage.target) - D(s.stage.score));
        var pool = s.dice.OrderBy(d => d.createdSerial).ToArray();
        int[] face = new int[12]; int locked = 0, alive = (1 << pool.Length) - 1;
        for (int i = 0; i < pool.Length; i++)
        {
            var r = s.hand.currentResults.First(x => x.dieId == pool[i].dieId);
            face[i] = r.hasRolled ? int.Parse(r.currentFaceId.Substring(1)) - 1 : 0;
            if (s.hand.orderedLockedIds.Contains(pool[i].dieId)) locked |= 1 << i;
        }
        int rolled = alive & ~locked;
        int add = strong.Decide(locked, alive, rolled, s.hand.rollsRemaining, face, s.hand.c05Used, out bool submit);
        if (add != 0) Act(CommandKind.ConfirmLocks, c => c.dieIds = Enumerable.Range(0, pool.Length).Where(i => (add >> i & 1) != 0).Select(i => pool[i].dieId).ToArray());
        if (!submit) { Act(CommandKind.Roll); return; }
        strong.EvalMask(locked | add, s.hand.rollsRemaining, face);
        var order = strong.bestOrd.Take(strong.bestN).Select(i => pool[i].dieId).ToArray();
        if (!session.State.hand.orderedLockedIds.SequenceEqual(order)) Act(CommandKind.Reorder, c => c.dieIds = order);
        Act(CommandKind.Submit);
    }

    void Settle(RunState s)
    {
        var r = s.lastStageSettlement;
        cur.cleared = true; cur.score = D(r.score); cur.subsUsed = s.stage.submissionOrdinal;
        cur.reward = r.baseReward; cur.unused = r.unusedSubmissionReward; cur.interest = r.interest; cur.pig = r.goldBefore - cur.goldStart; cur.goldAfterSettle = r.goldAfter;
        cur.goldAfterShop = r.goldAfter;
    }

    // ---------- shop ----------
    sealed class Cand { public string key, label; public double gain; public long price; public Func<RunState, RunState> apply; public Action buy; public bool stale; }

    static RunState WithController(RunState s, ControllerInstance offer, string removeInstanceId)
    {
        var st = s.Clone(); if (removeInstanceId != null) st.controllers.RemoveAll(c => c.instanceId == removeInstanceId);
        var c2 = offer.Clone(); c2.state = new ControllerDynamicState(); c2.alive = true; st.controllers.Add(c2); return st;
    }
    double Bonus(string id, int nextIndex)
    {
        int remaining = cfg.stages.Length - nextIndex;
        if (id == "C01") return 0.04 * Math.Min(12, remaining) * 2.2 * 0.6;   // p0.8.0: income per submission only
        if (id == "C52") return 0.04 * Math.Min(8, remaining) * 3 * 0.5;
        if (id == "C43") return remaining >= 6 ? 0.45 : 0;
        return 0;
    }
    Pw BestMimicPower(RunState st)
    {
        var mimic = st.controllers.FirstOrDefault(c => c.definitionId == "C41");
        if (mimic == null) return PowerOfState(st);
        var best = new Pw(); bool any = false; var others = st.controllers.Where(c => c != mimic).ToList();
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

    void Shop()
    {
        long goldIn = session.State.gold; int refreshes = 0; bool evaluated = false; var cands = new List<Cand>();
        double basePow = 0, ratio = 0, theta = 0; long reserve = 0; bool danger = false; int nextIndex = session.State.stage.stageIndex + 1;
        double target = D(cfg.stages[nextIndex].normalTarget);
        for (int iter = 0; iter < 40; iter++)
        {
            var s = session.State;
            var freq = new int[13];
            var bp = BestMimicPower(s); basePow = bp.V; PowerOfState(s, freq);
            ratio = bp.safe / target; danger = ratio < 1.1; bool comfy = ratio > 2.5;
            theta = danger ? 0.012 : comfy ? 0.09 : 0.04;
            long cap = cfg.maxInterestPrincipal;
            reserve = opt.weak || danger ? 0 : comfy ? cap : (long)(cap * (ratio - 1.1) / 1.4);
            if (!evaluated) { cands = Evaluate(s, basePow, freq, nextIndex); evaluated = true; }
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
            var fresh = Evaluate(s, basePow, freq, nextIndex, pick.key).FirstOrDefault();
            pick.stale = true;
            if (fresh == null || fresh.gain < opt.minGain || s.gold < fresh.price) continue;
            if (!(!opt.weak && s.gold - fresh.price >= cap) && (fresh.gain / Math.Max(1, fresh.price) < theta || s.gold - fresh.price < reserve)) continue;
            fresh.buy(); cur.bought.Add(fresh.label);
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
        OptimizeMimic();
        var end = session.State; cur.goldAfterShop = end.gold; cur.spent = goldIn - end.gold;
    }

    List<Cand> Evaluate(RunState s, double basePow, int[] freq, int nextIndex, string onlyKey = null)
    {
        var list = new List<Cand>();
        Func<RunState, double> gain = st => Math.Log(Math.Max(1e-9, BestMimicPower(st).V) / Math.Max(1e-9, basePow));
        int ordinary = s.controllers.Count(c => c.enchantmentId != "slotless");
        foreach (var offer in s.shop.offers.Where(o => !o.sold))
        {
            string key = offer.offerId; if (onlyKey != null && key != onlyKey) continue;
            long price = session.GetOfferPrice(offer);
            switch (offer.category)
            {
                case "controller":
                    {
                        string id = offer.contentId; if (s.controllers.Any(c => c.definitionId == id)) break;
                        bool slotless = offer.controller.enchantmentId == "slotless";
                        string label = id + (offer.controller.enchantmentId == "none" ? "" : ":" + offer.controller.enchantmentId);
                        if (s.controllers.Count >= cfg.maxControllerInstances) break;
                        if (slotless || ordinary < cfg.initialControllerSlots)
                        {
                            double g = gain(WithController(s, offer.controller, null)) + Bonus(id, nextIndex);
                            list.Add(new Cand { key = key, label = label, gain = g, price = price, buy = () => Act(CommandKind.Buy, c => c.offerId = offer.offerId) });
                        }
                        else
                        {
                            double bestG = double.NegativeInfinity; ControllerInstance victim = null;
                            foreach (var w in s.controllers.Where(c => c.enchantmentId != "slotless"))
                            {
                                double g = gain(WithController(s, offer.controller, w.instanceId)) + Bonus(id, nextIndex) - Bonus(w.definitionId, nextIndex);
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
                    break;
            }
        }
        if (s.shop.selfStudyUsed < cfg.shop.selfStudyLimit)
        {
            var legal = session.LegalStudyHands(); int total = Math.Max(1, freq.Sum());
            var want = Enumerable.Range(0, 13).Where(i => freq[i] * 8 >= total).Select(i => P.hId[i]).Concat(new[] { "all_blue", "all_red" }).Where(legal.Contains).ToArray();
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
