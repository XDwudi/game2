using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using DiceCraft.Core;

// Automated legal-input smoke test. Strategy sees current visible faces, not RNG states or future outcomes.
// Hypothetical rolls use a separate System.Random stream, and never mutate the GameSession.
public static class NormalRunSearch
{
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
    private static GameConfig cfg;
    private static GameSession session;
    private static Random planning;
    private static List<RecordedStep> steps;
    private static int sequence;

    public sealed class RecordedStep
    {
        public string kind, offerId, itemInstanceId, targetDieId, targetFaceId, handId;
        public string[] dieIds = Array.Empty<string>();
        public int[] targetPips = Array.Empty<int>();
        public bool risk;
        public uint seed;
        public long actionSequence, submissionScore, stageScore, target;
        public int gold, stageIndex, rollsRemaining, remainingSubmissions;
        public string phase;
    }
    private sealed class Choice { public string[] order; public long score; }
    private sealed class SearchResult { public uint seed; public bool won; public int reachedStage, actions; public string finalPhase; public int gold; }

    public static int Main(string[] args)
    {
        cfg = JsonSerializer.Deserialize<GameConfig>(File.ReadAllText("DicePrototype/Assets/Game/Data/P0Config.json"), Json); cfg.Validate();
        Directory.CreateDirectory("evidence/runs");
        uint first = args.Length > 0 ? uint.Parse(args[0]) : 20261028;
        int maximum = args.Length > 1 ? int.Parse(args[1]) : 100;
        var results = new List<SearchResult>();
        for (int run = 0; run < maximum; run++)
        {
            uint seed = first + (uint)run; planning = new Random(731); steps = new List<RecordedStep>(); sequence = 0;
            session = new GameSession(cfg, new MemorySaveStore());
            Act(CommandKind.NewRun, c => c.seed = seed);
            while (session.State.phase != "RunWon" && session.State.phase != "RunFailed")
            {
                if (steps.Count > 500) throw new Exception("Unexpected action loop");
                var state = session.State;
                switch (state.phase)
                {
                    case "StagePreview": Act(CommandKind.StartStage); break;
                    case "HandReady": UsePreparationItems(); Act(CommandKind.Roll); break;
                    case "Selecting": PlaySelection(); break;
                    case "StageSettlement": Act(CommandKind.Continue); break;
                    case "Shop": Shop(); Act(CommandKind.LeaveShop); break;
                    default: throw new Exception("Unexpected phase " + state.phase);
                }
            }
            var end = session.State;
            var result = new SearchResult { seed = seed, won = end.phase == "RunWon", reachedStage = end.stage.stageIndex + 1, actions = steps.Count, finalPhase = end.phase, gold = end.gold };
            results.Add(result);
            Console.WriteLine($"SEED {seed} {end.phase} stage={result.reachedStage} actions={steps.Count} gold={end.gold}");
            File.WriteAllText("evidence/runs/search-statistics.json", JsonSerializer.Serialize(new { purpose = "Automated legal-action integration validation; searched seeds are not a player win-rate sample", policy = "Visible-state one-step Monte Carlo with independent Random(731); no rule RNG introspection", configVersion = cfg.configVersion, results }, Json));
            if (result.won)
            {
                string path = "evidence/runs/normal-win.json";
                File.WriteAllText(path, JsonSerializer.Serialize(new { seed = seed, configVersion = cfg.configVersion, isDebug = end.isDebug, policy = "Visible-state one-step Monte Carlo, independent planning RNG", steps = steps }, Json));
                File.WriteAllText("evidence/runs/normal-win-final-state.json", JsonSerializer.Serialize(end, Json));
                Console.WriteLine("WIN_TRAJECTORY " + Path.GetFullPath(path)); return 0;
            }
        }
        return 2;
    }

    private static void Act(CommandKind kind, Action<SessionCommand> configure = null)
    {
        var before = session.State;
        var command = new SessionCommand { kind = kind, commandId = "normal_" + (++sequence), expectedActionSequence = before == null ? 0 : before.actionSequence };
        configure?.Invoke(command);
        if (kind == CommandKind.ConfirmLocks) foreach (string id in command.dieIds) if (!session.TogglePending(id)) throw new Exception("Illegal pending " + id);
        var result = session.Execute(command);
        if (!result.accepted) throw new Exception("Rejected legal-policy action " + kind + ": " + result.error);
        var s = session.State;
        if (s.isDebug) throw new Exception("Normal run became Debug");
        steps.Add(new RecordedStep { kind = kind.ToString(), seed = command.seed, risk = command.risk,
            dieIds = command.dieIds, targetPips = command.targetPips, offerId = command.offerId, itemInstanceId = command.itemInstanceId,
            targetDieId = command.targetDieId, targetFaceId = command.targetFaceId, handId = command.handId,
            actionSequence = s.actionSequence, submissionScore = result.receipt.score, stageScore = s.stage.score, target = s.stage.target,
            gold = s.gold, stageIndex = s.stage.stageIndex, rollsRemaining = s.hand == null ? 0 : s.hand.rollsRemaining,
            remainingSubmissions = s.stage.remainingSubmissions, phase = s.phase });
    }

    private static bool Has(RunState state, string id) => state.controllers.Any(x => x.contentId == id);

    private static void UsePreparationItems()
    {
        foreach (string id in new[] { "I07", "I06", "I04", "I05" })
        {
            var state = session.State;
            var item = state.inventory.FirstOrDefault(x => x.contentId == id);
            if (item == null) continue;
            if (id == "I04" && !state.dice.Any(d => d.faces.Any(f => f.permanentPip == 6))) continue;
            if (id == "I05" && !state.dice.Any(d => d.faces.Any(f => f.permanentPip == 1))) continue;
            Act(CommandKind.UseItem, c => c.itemInstanceId = item.instanceId);
        }
    }

    private static void PlaySelection()
    {
        var s = session.State;
        var current = s.hand.currentResults.ToDictionary(x => x.dieId, x => x.Clone());
        var best = BestImmediate(s, current, s.hand.orderedLockedIds.ToArray(), s.hand.rollsRemaining);
        long needed = s.stage.target - s.stage.score;
        bool mustStop = s.hand.rollsRemaining == 0 || s.hand.orderedLockedIds.Count == s.dice.Count;
        if (best.score >= needed || mustStop) { SubmitChoice(s, best); return; }

        // Candidate commitments: existing locks, like-valued groups, color/parity groups, diverse numbers,
        // and the highest visible score. This is a playing policy, not an exhaustive optimal solver.
        var holdSets = new List<string[]> { s.hand.orderedLockedIds.ToArray(), best.order };
        var free = s.dice.Where(d => !s.hand.orderedLockedIds.Contains(d.dieId)).Select(d => d.dieId).ToArray();
        Func<string,int> effective = id => current[id].currentPip + (Has(s,"C12") && current[id].currentPip % 2 == 1 ? 1 : 0);
        foreach (var group in free.GroupBy(effective)) holdSets.Add(s.hand.orderedLockedIds.Concat(group).ToArray());
        foreach (string color in new[] { "red", "blue" }) holdSets.Add(s.hand.orderedLockedIds.Concat(free.Where(id => current[id].currentColor == color)).ToArray());
        if (Has(s,"C14") || Has(s,"C13")) holdSets.Add(s.hand.orderedLockedIds.Concat(free.Where(id=>effective(id)%2==1)).ToArray());
        if (Has(s,"C06")) holdSets.Add(s.hand.orderedLockedIds.Concat(free.GroupBy(effective).Select(g=>g.First())).ToArray());
        if (Has(s,"C11")) holdSets.Add(s.hand.orderedLockedIds.Concat(free.Where(id=>effective(id)==2||effective(id)==3).GroupBy(effective).SelectMany(g=>g.Take(2))).ToArray());
        var legal = holdSets.Where(x => x.Length <= s.stage.capacity && x.Length < s.dice.Count)
            .GroupBy(x => string.Join("|",x.OrderBy(y=>y))).Select(x=>x.First()).ToArray();
        double value = best.score; string[] keep = null;
        foreach (var held in legal)
        {
            double expected = 0;
            int nextRolls = s.hand.rollsRemaining - 1;
            if (Has(s,"C05") && !s.hand.c05Used && nextRolls==0 && s.dice.Count-held.Length>=1 && s.dice.Count-held.Length<=2) nextRolls += 3;
            for (int sample=0;sample<20;sample++)
            {
                var hypothetical = current.ToDictionary(x=>x.Key,x=>x.Value.Clone());
                foreach (var die in s.dice.Where(d=>!held.Contains(d.dieId)))
                {
                    // This policy does not purchase glass. Refuse accidental unsupported simulation.
                    if (die.isGlass) throw new Exception("Glass entered normal policy without survival model");
                    var units = ProbabilityCalculator.BuildUnits(die.faces,s.hand.largeBuff,s.hand.smallBuff,cfg);
                    int r=planning.Next(cfg.probabilityUnits),index=0,total=units[0];while(r>=total)total+=units[++index];
                    var face=die.faces[index];var h=hypothetical[die.dieId];h.currentFaceId=face.faceId;h.currentPip=face.permanentPip;h.currentColor=face.baseColor;
                }
                var future=BestImmediate(s,hypothetical,held,nextRolls);
                // A small continuation allowance makes early pair commitments less myopic.
                expected += Math.Min(future.score,needed) + Math.Max(0,future.score-needed)*0.03;
            }
            expected/=20;
            if (nextRolls>0) expected*=1.08;
            if(expected>value*1.015){value=expected;keep=held;}
        }
        if(keep==null){SubmitChoice(s,best);return;}
        var additions=keep.Where(id=>!s.hand.orderedLockedIds.Contains(id)).ToArray();
        if(additions.Length>0)Act(CommandKind.ConfirmLocks,c=>c.dieIds=additions);
        Act(CommandKind.Roll);
    }

    private static Choice BestImmediate(RunState s, Dictionary<string,DieHandState> current, string[] locked, int rolls)
    {
        var free=s.dice.Where(d=>!locked.Contains(d.dieId)).Select(d=>d.dieId).ToArray();
        var best=new Choice{order=locked,score=-1};
        for(int mask=0;mask<(1<<free.Length);mask++)
        {
            var ids=locked.Concat(free.Where((id,i)=>(mask&(1<<i))!=0)).ToArray();
            if(ids.Length==0||ids.Length>s.stage.capacity)continue;
            // Only positional score modifiers require extra ordering candidates.
            var orders=new List<string[]>{ids};
            if(Has(s,"C03"))for(int i=1;i<ids.Length;i++){var ordered=(string[])ids.Clone();var a=ordered[0];ordered[0]=ordered[i];ordered[i]=a;orders.Add(ordered);}
            foreach(var order in orders)
            {
                if(Has(s,"C20")&&order.Length==s.stage.capacity)
                {
                    int at=Array.FindIndex(order,id=>current[id].currentPip==6||(Has(s,"C12")&&current[id].currentPip==5));
                    if(at>=0){var a=order[at];order[at]=order[order.Length-1];order[order.Length-1]=a;}
                }
                var snap=new SubmissionSnapshot{runId=s.runId,stageId=s.stage.stageId,handId=s.hand.handId,configVersion=cfg.configVersion,
                    capacity=s.stage.capacity,rollsRemaining=rolls,submissionOrdinalBefore=s.stage.submissionOrdinal,redWave=s.hand.redWave,
                    handLevels=s.handLevels.ToArray(),equippedControllerIds=s.controllers.Select(x=>x.contentId).ToArray(),
                    dice=order.Select(id=>{var d=s.dice.First(x=>x.dieId==id);var r=current[id];var f=d.faces.First(x=>x.faceId==r.currentFaceId);
                        return new SubmissionDie{dieId=id,faceId=r.currentFaceId,currentPip=r.currentPip,currentColor=r.currentColor,
                            permanentSixFaceSum=d.faces.Sum(x=>x.permanentPip),isGold=f.isGold,isGlass=d.isGlass,lifetime=d.lifetime};}).ToArray()};
                long score=ScoreCalculator.Evaluate(snap,cfg).score;
                if(score>best.score)best=new Choice{order=(string[])order.Clone(),score=score};
            }
        }
        return best;
    }

    private static void SubmitChoice(RunState s, Choice choice)
    {
        var additional=choice.order.Where(id=>!s.hand.orderedLockedIds.Contains(id)).ToArray();
        if(additional.Length>0)Act(CommandKind.ConfirmLocks,c=>c.dieIds=additional);
        if(!session.State.hand.orderedLockedIds.SequenceEqual(choice.order))Act(CommandKind.Reorder,c=>c.dieIds=choice.order);
        var state=session.State;
        var red=state.inventory.FirstOrDefault(x=>x.contentId=="I01");
        if(red!=null&&!state.hand.redWave&&state.hand.orderedLockedIds.Count>0)
        {
            var snap=session.PreviewSnapshot();snap.redWave=true;
            if(ScoreCalculator.Evaluate(snap,cfg).score>session.PreviewScore().score*1.3)Act(CommandKind.UseItem,c=>c.itemInstanceId=red.instanceId);
        }
        Act(CommandKind.Submit);
    }

    private static int ControllerValue(RunState s,string id)
    {
        var values=new Dictionary<string,int>{{"C01",20},{"C02",90},{"C03",8},{"C04",45},{"C05",60},{"C06",65},{"C07",0},{"C08",90},{"C09",120},{"C10",30},{"C11",35},{"C12",140},{"C13",35},{"C14",65},{"C15",10},{"C16",100},{"C17",45},{"C18",40},{"C19",3},{"C20",6}};
        if(Has(s,"C12")&&new[]{"C06","C10","C11","C13","C14"}.Contains(id))return 0;
        if(id=="C12"&&s.controllers.Count(x=>new[]{"C06","C11","C13","C14"}.Contains(x.contentId))>=2)return 35;
        if(id=="C02"&&Has(s,"C03"))return 0;
        if(id=="C03"&&Has(s,"C02"))return 0;
        return values[id];
    }

    private static void Shop()
    {
        for(int round=0;round<8;round++)
        {
            var s=session.State;
            var candidates=s.shop.offers.Where(o=>o.category=="controller"&&!o.sold&&!Has(s,o.contentId)).OrderByDescending(o=>ControllerValue(s,o.contentId)/(double)cfg.Controller(o.contentId).buyPrice).ToArray();
            bool bought=false;
            foreach(var offer in candidates)
            {
                int value=ControllerValue(s,offer.contentId);if(value<30)continue;
                if(s.controllers.Count>=s.controllerSlots)
                {
                    var worst=s.controllers.OrderBy(x=>ControllerValue(s,x.contentId)).First();
                    if(value<ControllerValue(s,worst.contentId)+25||s.gold+1<session.GetOfferPrice(offer))continue;
                    Act(CommandKind.Sell,c=>c.itemInstanceId=worst.instanceId);
                }
                if(session.PurchaseError(offer.offerId)!=null)continue;
                Act(CommandKind.Buy,c=>c.offerId=offer.offerId);bought=true;break;
            }
            if(!bought)break;
        }
        // Reusable score growth after core equipment, retaining some money if the build is immature.
        var state=session.State;
        if(state.controllers.Count>=2||state.stage.stageIndex>=2)
        {
            var legal=session.LegalStudyHands();
            string desired=state.stage.capacity<5||Has(state,"C09")?"trips":Has(state,"C12")?"quads":"full_house";
            foreach(var offer in state.shop.offers.Where(o=>o.category=="hand_upgrade"&&!o.sold).OrderBy(o=>o.contentId==desired?0:1))
                if(new[]{desired,"trips","two_pair","quads"}.Contains(offer.contentId)&&session.PurchaseError(offer.offerId)==null&&session.State.gold>=session.GetOfferPrice(offer)+3)Act(CommandKind.Buy,c=>c.offerId=offer.offerId);
            if(legal.Contains(desired)&&session.State.gold>=session.GetStudyPrice(desired)+3)Act(CommandKind.Study,c=>c.handId=desired);
        }
        state=session.State;
        foreach(var offer in state.shop.offers.Where(o=>o.category=="consumable"&&!o.sold).OrderBy(o=>o.contentId=="I07"?0:o.contentId=="I06"?1:2))
        {
            if(!new[]{"I07","I06","I01"}.Contains(offer.contentId))continue;
            if(state.stage.stageIndex<3&&state.controllers.Count<3)continue;
            if(session.PurchaseError(offer.offerId)==null&&session.State.gold>=session.GetOfferPrice(offer)+2)Act(CommandKind.Buy,c=>c.offerId=offer.offerId);
        }
    }
}
