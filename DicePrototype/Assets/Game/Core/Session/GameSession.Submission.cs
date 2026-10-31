using System;
using System.Linq;
using System.Globalization;

namespace DiceCraft.Core
{
    public sealed partial class GameSession
    {
        private SubmissionSnapshot FreezeSubmission(RunState s)
        {
            var snapshot = new SubmissionSnapshot {
                runId = s.runId, stageId = s.stage.stageId, handId = s.hand.handId, configVersion = s.configVersion,
                capacity = s.stage.capacity, rollsRemaining = s.hand.rollsRemaining, submissionOrdinalBefore = s.stage.submissionOrdinal,
                redWave = s.hand.redWave, debug = s.isDebug, wallet = s.gold, previousPrimaryHandId = s.previousPrimaryHandId,
                mimicBindings = s.stage.mimicBindings.Select(x => new MimicBinding { sourceInstanceId = x.sourceInstanceId, targetInstanceId = x.targetInstanceId }).ToArray(),
                colorLevels = s.colorLevels.Select(x => x.Clone()).ToArray(), equippedControllers = s.controllers.Select(x => x.Clone()).ToArray(),
                handLevels = s.handLevels.Select(x => new HandLevel { handId = x.handId, level = x.level }).ToArray(),
                dice = s.hand.orderedLockedIds.Select(id => {
                    var d = FindDie(s, id); var r = s.hand.currentResults.First(x => x.dieId == id);
                    var face = d.faces.First(f => f.faceId == r.currentFaceId);
                    return new SubmissionDie { dieId = id, faceId = r.currentFaceId, currentPip = r.currentPip, currentColor = face.baseColor,
                        permanentSixFaceSum = d.faces.Sum(f => f.permanentPip), isGlass = d.isGlass, isGold = face.isGold,
                        lifetime = d.lifetime, permanentFacesCopy = d.faces.Select(f => f.Clone()).ToArray() };
                }).ToArray()
            };
            return snapshot;
        }

        private string Submit(RunState s)
        {
            RequirePhase(s, "Selecting");
            Check(s.hand.hasRolled && s.hand.orderedLockedIds.Count > 0 && s.hand.orderedLockedIds.Count <= s.stage.capacity,
                "至少投掷一次并确认锁定一颗骰子后才可提交。");
            Check(s.stage.remainingSubmissions > 0 && !s.stage.settled, "没有可用的提交机会。");
            var snapshot = FreezeSubmission(s);
            var effectRng = new GameRng(s.effectRngState);
            snapshot.resolvedRandomChoices = snapshot.equippedControllers.Where(c => c.definitionId == "C53").Select(c => {
                var trace = DrawRaw(s, effectRng, "effect", "C53", c.instanceId, 9);
                int[] map = { 2, 4, 6, 8, 10, 12, 14, 16, 1 };
                int selected = int.Parse(ApplyOverride(s, trace, map[trace.rawTicket].ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
                return new RandomMultiplierChoice { ownerInstanceId = c.instanceId, units2 = selected };
            }).ToArray();
            s.effectRngState = effectRng.State;
            var score = ScoreCalculator.Evaluate(snapshot, cfg);
            s.lastSubmission = new SubmissionRecord { snapshot = snapshot, breakdown = score };
            s.stage.remainingSubmissions--; s.stage.submissionOrdinal++;
            s.submissionOrdinalGlobal = checked(s.submissionOrdinalGlobal + 1);
            s.stage.score = ExactScoreMath.Add(s.stage.score, score.score);

            ApplySubmissionEffects(s, snapshot, score);
            s.stage.qualified = s.stage.qualified || ExactScoreMath.Compare(s.stage.score, s.stage.target) >= 0;
            if (s.stage.remainingSubmissions == 0 || s.dice.Count == 0)
            {
                if (s.stage.qualified) FinalizeStage(s);
                else { s.phase = "RunFailed"; s.failureReason = "SubmissionsExhausted"; }
            }
            else StartHand(s);
            return score.score;
        }

        private void ApplySubmissionEffects(RunState s, SubmissionSnapshot snapshot, ScoreBreakdown score)
        {
            var plan = score.afterScorePlan;
            ApplyPermanentEffects(s, snapshot, plan);
            if (plan.earnedGold != 0)
            {
                s.gold = checked(s.gold + plan.earnedGold);
                s.lastEffects.Add(new EffectRecord { kind = "SubmissionIncome", sourceId = "C01", newValue = plan.earnedGold });
            }
            if (!string.IsNullOrEmpty(plan.upgradeHandId) && plan.upgradeRequested > 0)
            {
                var level = s.handLevels.First(h => h.handId == plan.upgradeHandId);
                int old = level.level; level.level = checked(level.level + plan.upgradeApplied);
                s.lastEffects.Add(new EffectRecord { kind = "HandGrown", sourceId = "C43", detail = plan.upgradeHandId, oldValue = old, newValue = level.level });
            }
            s.previousPrimaryHandId = plan.nextPreviousPrimary;
            var rng = new GameRng(s.effectRngState);
            foreach (var ownerId in plan.nextColorOwnerIds)
            {
                var owner = s.controllers.First(c => c.instanceId == ownerId);
                var trace = DrawRaw(s, rng, "effect", "C45", ownerId, 2);
                owner.state.activeColor = ApplyOverride(s, trace, trace.rawTicket == 0 ? "red" : "blue");
                s.lastEffects.Add(new EffectRecord { kind = "ActiveColorChanged", sourceId = "C45", detail = ownerId + ":" + owner.state.activeColor });
            }
            foreach (var emitterId in plan.cannonRiskEmitterIds)
            {
                if (DrawRisk(s, rng, "C42", emitterId, 15))
                {
                    s.controllers.RemoveAll(c => c.instanceId == emitterId);
                    s.stage.destroyedControllerIds.Add(emitterId);
                    s.lastEffects.Add(new EffectRecord { kind = "ControllerDestroyed", sourceId = "C42", detail = emitterId });
                }
            }
            s.effectRngState = rng.State;
        }

        private void ApplyPermanentEffects(RunState s, SubmissionSnapshot snapshot, AfterScorePlan plan)
        {
            if (plan.copyFirstToSecond)
            {
                var source = snapshot.dice[0]; var target = FindDie(s, snapshot.dice[1].dieId);
                var copy = source.permanentFacesCopy.Select(f => f.Clone()).ToArray();
                bool changed = target.isGlass != source.isGlass || target.faces.Where((f, i) => !SameFace(f, copy[i])).Any();
                target.faces = copy; target.isGlass = source.isGlass;
                if (changed) target.dataRevision++;
                s.lastEffects.Add(new EffectRecord { kind = "DieCopied", sourceId = "C07", dieId = target.dieId, otherDieId = source.dieId });
            }
            if (plan.dyeAllFacesRed)
            {
                var target = FindDie(s, snapshot.dice[0].dieId); bool changed = target.faces.Any(f => f.baseColor != "red");
                foreach (var face in target.faces) face.baseColor = "red";
                if (changed) target.dataRevision++;
                s.lastEffects.Add(new EffectRecord { kind = "DieDyed", sourceId = "C26", dieId = target.dieId, detail = "red" });
            }
            foreach (int index in plan.growthIndices)
            {
                var frozen = snapshot.dice[index]; var target = FindDie(s, frozen.dieId);
                var face = target.faces.First(f => f.faceId == frozen.faceId); int old = face.permanentPip;
                face.permanentPip = Math.Max(cfg.minPipValue, Math.Min(cfg.maxPipValue, checked(old + plan.growthNetDelta)));
                if (face.permanentPip != old) target.dataRevision++;
                s.lastEffects.Add(new EffectRecord { kind = "FaceValueChanged", sourceId = plan.growthNetDelta == 0 ? "C36+C37" : plan.growthNetDelta > 0 ? "C36" : "C37",
                    dieId = target.dieId, faceId = face.faceId, oldValue = old, newValue = face.permanentPip });
            }
            foreach (int index in plan.redSpreadIndices) DyeSubmittedFace(s, snapshot.dice[index], "red", "C15");
            foreach (int index in plan.blueSpreadIndices) DyeSubmittedFace(s, snapshot.dice[index], "blue", "C38");
        }

        private static bool SameFace(FaceState a, FaceState b) => a.faceId == b.faceId && a.permanentPip == b.permanentPip && a.baseColor == b.baseColor
            && a.texture == b.texture && a.isGold == b.isGold && a.baseWeight == b.baseWeight;

        private void DyeSubmittedFace(RunState s, SubmissionDie frozen, string color, string sourceId)
        {
            var target = FindDie(s, frozen.dieId); var face = target.faces.First(f => f.faceId == frozen.faceId);
            if (face.baseColor != color) { face.baseColor = color; target.dataRevision++; }
            s.lastEffects.Add(new EffectRecord { kind = "FaceDyed", sourceId = sourceId, dieId = target.dieId, faceId = frozen.faceId, detail = color });
        }

        private void EndStage(RunState s)
        {
            RequirePhase(s, "HandReady", "Selecting");
            Check(s.stage.qualified && !s.stage.settled && s.stage.remainingSubmissions > 0, "本关尚未达标或已经结束。");
            // Unsubmitted locks are discarded: no score, Submit hooks or budget consumption.
            FinalizeStage(s);
        }

        private void FinalizeStage(RunState s)
        {
            if (s.stage.settled) return;
            Check(s.stage.qualified, "尚未达标，不能结束本轮。");
            var fundSources = Sources(s).Where(x => x.abilityId == "C52").Select(x => x.emitterId).Distinct(StringComparer.Ordinal).ToArray();
            s.lastStageSettlement = CalculateStageSettlement(s);
            foreach (string emitterId in fundSources)
            {
                var emitter = s.controllers.First(c => c.instanceId == emitterId);
                emitter.state.fundAccrual = checked(emitter.state.fundAccrual + 3);
                s.lastEffects.Add(new EffectRecord { kind = "FundAccrued", sourceId = "C52", detail = emitterId, newValue = emitter.state.fundAccrual });
            }
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
            long principal = Math.Min(s.gold, cfg.maxInterestPrincipal);
            int pigs = Sources(s).Count(x => x.abilityId == "C01");
            int interestPerTen = 1 + pigs * cfg.Controller("C01").parameters.extraInterestPerTen;
            long interest = checked(interestPerTen * (principal / cfg.interestDivisor));
            long unusedReward = checked(s.stage.remainingSubmissions * cfg.goldPerUnusedSubmission);
            return new StageSettlement {
                settlementId = s.runId + "/" + s.stage.stageId, stageId = s.stage.stageId,
                goldBefore = s.gold, baseReward = s.stage.reward, remainingSubmissions = s.stage.remainingSubmissions,
                unusedSubmissionReward = unusedReward, interest = interest, interestPrincipal = principal,
                goldAfter = checked(s.gold + s.stage.reward + unusedReward + interest),
                score = s.stage.score, target = s.stage.target, risk = s.stage.risk, pig = pigs > 0, pigSourceCount = pigs
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
                    Check(s.dice.Count < (s.isDebug ? cfg.debug.maxDice : cfg.maxActiveDice) && (s.isDebug || s.dice.Count(x => x.lifetime == "stage_temporary") < cfg.maxTemporaryDice), "临时骰或活动骰数量已满。");
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
