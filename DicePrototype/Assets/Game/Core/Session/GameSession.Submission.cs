using System;
using System.Linq;

namespace DiceCraft.Core
{
    public sealed partial class GameSession
    {
        private SubmissionSnapshot FreezeSubmission(RunState s)
        {
            var snapshot = new SubmissionSnapshot {
                runId = s.runId, stageId = s.stage.stageId, handId = s.hand.handId, configVersion = s.configVersion,
                capacity = s.stage.capacity, rollsRemaining = s.hand.rollsRemaining, submissionOrdinalBefore = s.stage.submissionOrdinal,
                redWave = s.hand.redWave, equippedControllers = s.controllers.Select(x => x.Clone()).ToArray(),
                handLevels = s.handLevels.Select(x => new HandLevel { handId = x.handId, level = x.level }).ToArray(),
                dice = s.hand.orderedLockedIds.Select(id => {
                    var d = FindDie(s, id); var r = s.hand.currentResults.First(x => x.dieId == id);
                    var face = d.faces.First(f => f.faceId == r.currentFaceId);
                    return new SubmissionDie { dieId = id, faceId = r.currentFaceId, currentPip = r.currentPip, currentColor = r.currentColor,
                        permanentSixFaceSum = d.faces.Sum(f => f.permanentPip), isGlass = d.isGlass, isGold = face.isGold,
                        lifetime = d.lifetime, permanentFacesCopy = d.faces.Select(f => f.Clone()).ToArray() };
                }).ToArray()
            };
            return snapshot;
        }

        private long Submit(RunState s)
        {
            RequirePhase(s, "Selecting");
            Check(s.hand.hasRolled && s.hand.orderedLockedIds.Count > 0 && s.hand.orderedLockedIds.Count <= s.stage.capacity,
                "至少投掷一次并确认锁定一颗骰子后才可提交。");
            Check(s.stage.remainingSubmissions > 0 && !s.stage.settled, "没有可用的提交机会。");
            var snapshot = FreezeSubmission(s);
            var score = ScoreCalculator.Evaluate(snapshot, cfg);
            s.lastSubmission = new SubmissionRecord { snapshot = snapshot, breakdown = score };
            s.stage.remainingSubmissions--; s.stage.submissionOrdinal++;
            s.stage.score = checked(s.stage.score + score.score);

            ApplySubmissionEffects(s, snapshot, score);
            s.stage.qualified = s.stage.qualified || s.stage.score >= s.stage.target;
            if (s.stage.remainingSubmissions == 0)
            {
                if (s.stage.qualified) FinalizeStage(s);
                else { s.phase = "RunFailed"; s.failureReason = "SubmissionsExhausted"; }
            }
            else StartHand(s);
            return score.score;
        }

        private void ApplySubmissionEffects(RunState s, SubmissionSnapshot snapshot, ScoreBreakdown score)
        {
            // All conditions/targets are frozen before any mutable write. Equipment order never
            // changes this pipeline: copy, whole-die red, net growth, red face, blue face.
            var triggered = score.triggeredControllerIds;
            if (triggered.Contains("C07"))
            {
                var p = cfg.Controller("C07").parameters;
                var source = snapshot.dice[p.sourceIndex]; var target = FindDie(s, snapshot.dice[p.targetIndex].dieId);
                target.faces = source.permanentFacesCopy.Select(f => f.Clone()).ToArray();
                target.isGlass = source.isGlass; target.dataRevision++;
                s.lastEffects.Add(new EffectRecord { kind = "DieCopied", sourceId = "C07", dieId = target.dieId, otherDieId = source.dieId });
            }
            if (triggered.Contains("C26"))
            {
                var target = FindDie(s, snapshot.dice[0].dieId);
                foreach (var face in target.faces) face.baseColor = cfg.Controller("C26").parameters.allFacesColor;
                target.dataRevision++;
                s.lastEffects.Add(new EffectRecord { kind = "DieDyed", sourceId = "C26", dieId = target.dieId, detail = "red" });
            }
            if (triggered.Contains("C36") || triggered.Contains("C37"))
            {
                var changes = new System.Collections.Generic.Dictionary<int, int>();
                foreach (string id in new[] { "C36", "C37" }.Where(triggered.Contains))
                {
                    var p = cfg.Controller(id).parameters;
                    foreach (int index in p.indices)
                    {
                        int current; changes.TryGetValue(index, out current);
                        changes[index] = checked(current + p.permanentFaceDelta);
                    }
                }
                foreach (var change in changes.OrderBy(x => x.Key))
                {
                    var frozen = snapshot.dice[change.Key]; var target = FindDie(s, frozen.dieId);
                    var face = target.faces.First(f => f.faceId == frozen.faceId); int old = face.permanentPip;
                    face.permanentPip = Math.Max(cfg.minPipValue, Math.Min(cfg.maxPipValue, checked(old + change.Value)));
                    if (face.permanentPip != old) target.dataRevision++;
                    s.lastEffects.Add(new EffectRecord { kind = "FaceValueChanged", sourceId = triggered.Contains("C36") && triggered.Contains("C37") ? "C36+C37" : triggered.Contains("C36") ? "C36" : "C37",
                        dieId = target.dieId, faceId = face.faceId, oldValue = old, newValue = face.permanentPip });
                }
            }
            if (triggered.Contains("C15")) DyeSubmittedFace(s, snapshot.dice[cfg.Controller("C15").parameters.targetIndex], "red", "C15");
            if (triggered.Contains("C38")) DyeSubmittedFace(s, snapshot.dice[cfg.Controller("C38").parameters.index], cfg.Controller("C38").parameters.currentPhysicalFaceColor, "C38");
        }

        private void DyeSubmittedFace(RunState s, SubmissionDie frozen, string color, string sourceId)
        {
            var target = FindDie(s, frozen.dieId);
            target.faces.First(f => f.faceId == frozen.faceId).baseColor = color; target.dataRevision++;
            s.lastEffects.Add(new EffectRecord { kind = "FaceDyed", sourceId = sourceId, dieId = target.dieId, faceId = frozen.faceId, detail = color });
        }

        private void EndStage(RunState s)
        {
            RequirePhase(s, "HandReady", "Selecting");
            Check(s.stage.qualified && !s.stage.settled, "本关尚未达标或已经结束。");
            // Unsubmitted locks are discarded: no score, Submit hooks or budget consumption.
            FinalizeStage(s);
        }

        private void FinalizeStage(RunState s)
        {
            if (s.stage.settled) return;
            Check(s.stage.qualified, "尚未达标，不能结束本轮。");
            s.lastStageSettlement = CalculateStageSettlement(s);
            s.gold = s.lastStageSettlement.goldAfter;
            s.stage.settlementId = s.lastStageSettlement.settlementId;
            s.stage.settled = true;
            foreach (var d in s.dice.Where(x => x.lifetime == "stage_temporary").ToArray())
            {
                s.lastEffects.Add(new EffectRecord { kind = "TemporaryDieExpired", dieId = d.dieId }); s.dice.Remove(d);
            }
            s.hand = null; s.stage.stageRollBonus = 0;
            if (s.stage.stageIndex == cfg.stages.Length - 1) s.stage.settlementContinuation = "RunWon";
            else if (PermanentCount(s) == 0) { s.stage.settlementContinuation = "RunFailed"; s.failureReason = "NoPermanentDice"; }
            else s.stage.settlementContinuation = "Shop";
            s.phase = "StageSettlement";
        }

        /// <summary>Detached read-only quote; no action sequence, RNG, score or reward is changed.</summary>
        public StageSettlement PreviewStageSettlement()
        {
            lock (gate)
            {
                if (state == null || state.stage == null || !state.stage.qualified) return null;
                return state.stage.settled ? state.lastStageSettlement.Clone() : CalculateStageSettlement(state);
            }
        }

        private StageSettlement CalculateStageSettlement(RunState s)
        {
            int principal = Math.Min(s.gold, cfg.maxInterestPrincipal);
            int interestPerTen = 1 + (Has(s, "C01") ? cfg.Controller("C01").parameters.extraInterestPerTen : 0);
            int interest = checked(interestPerTen * (principal / cfg.interestDivisor));
            int unusedReward = checked(s.stage.remainingSubmissions * cfg.goldPerUnusedSubmission);
            return new StageSettlement {
                settlementId = s.runId + "/" + s.stage.stageId, stageId = s.stage.stageId,
                goldBefore = s.gold, baseReward = s.stage.reward, remainingSubmissions = s.stage.remainingSubmissions,
                unusedSubmissionReward = unusedReward, interest = interest, interestPrincipal = principal,
                goldAfter = checked(s.gold + s.stage.reward + unusedReward + interest),
                score = s.stage.score, target = s.stage.target, risk = s.stage.risk, pig = Has(s, "C01")
            };
        }

        private void Continue(RunState s)
        {
            RequirePhase(s, "StageSettlement");
            Check(s.stage.settled, "尚未完成结算。");
            if (s.stage.settlementContinuation == "Shop") { GenerateShop(s); s.phase = "Shop"; }
            else s.phase = s.stage.settlementContinuation;
        }

        private void LeaveShop(RunState s)
        {
            RequirePhase(s, "Shop");
            Check(s.dice.Count > 0, "骰组耗尽，不能开始下一关。");
            PrepareStagePreview(s, s.stage.stageIndex + 1);
        }

        private void UseItem(RunState s, SessionCommand command)
        {
            RequirePhase(s, "HandReady", "Selecting");
            var item = s.inventory.FirstOrDefault(x => x.instanceId == command.itemInstanceId);
            Check(item != null, "道具不存在或已使用。");
            switch (item.contentId)
            {
                case "I01":
                    Check(s.phase == "Selecting" && s.hand.hasRolled && s.hand.orderedLockedIds.Count > 0, "红色浪潮需要至少一颗已锁骰。");
                    Check(!s.hand.redWave, "本手红色浪潮已经生效。"); s.hand.redWave = true;
                    break;
                case "I02":
                    Check(s.phase == "HandReady", "临时骰子只能在本手首掷前使用。");
                    Check(s.dice.Count < cfg.maxActiveDice && s.dice.Count(x => x.lifetime == "stage_temporary") < cfg.maxTemporaryDice, "临时骰或活动骰数量已满。");
                    var d = DiceFactory.CreateStandard(cfg, s.nextDieSerial++, "stage_temporary", s.stage.stageId);
                    s.dice.Add(d); s.hand.currentResults.Add(new DieHandState { dieId = d.dieId });
                    s.lastEffects.Add(new EffectRecord { kind = "DieAdded", sourceId = "I02", dieId = d.dieId });
                    break;
                case "I03":
                    Check(command.dieIds != null && command.targetPips != null && command.dieIds.Length == 2 && command.targetPips.Length == 2
                        && command.dieIds.Distinct().Count() == 2, "遥控需要恰好两颗不同的未锁骰及各自目标点数。");
                    Check(command.targetPips.All(x => x >= cfg.minPipValue && x <= cfg.maxPipValue), "目标点数必须为1至6。");
                    Check(command.dieIds.All(id => s.dice.Any(x => x.dieId == id) && !s.hand.orderedLockedIds.Contains(id)), "遥控目标必须是未锁存活骰。");
                    Roll(s, command.dieIds, command.targetPips);
                    break;
                case "I04":
                    Check(!s.hand.largeBuff, "本手大数多来已经生效。");
                    Check(s.hand.rollsRemaining > 0 && s.dice.Any(x => !s.hand.orderedLockedIds.Contains(x.dieId) && x.faces.Any(f => f.permanentPip == cfg.maxPipValue)), "没有可投掷且含永久6点面的未锁骰。"); s.hand.largeBuff = true;
                    break;
                case "I05":
                    Check(!s.hand.smallBuff, "本手缩小射线已经生效。");
                    Check(s.hand.rollsRemaining > 0 && s.dice.Any(x => !s.hand.orderedLockedIds.Contains(x.dieId) && x.faces.Any(f => f.permanentPip == cfg.minPipValue)), "没有可投掷且含永久1点面的未锁骰。"); s.hand.smallBuff = true;
                    break;
                case "I06": s.stage.remainingSubmissions = checked(s.stage.remainingSubmissions + 1); break;
                case "I07": s.stage.stageRollBonus = checked(s.stage.stageRollBonus + 1); s.hand.rollsRemaining = checked(s.hand.rollsRemaining + 1); break;
                default: throw new RuleViolation("未知道具。");
            }
            s.inventory.Remove(item); s.consumedItemIds.Add(item.instanceId);
            s.lastEffects.Add(new EffectRecord { kind = "ItemUsed", sourceId = item.contentId, detail = item.instanceId });
        }
    }
}
