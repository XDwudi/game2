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
                rollsUsed = s.hand.rollsUsed, submissionsRemainingBefore = s.stage.remainingSubmissions, permanentDiceCount = PermanentCount(s),
                stageKind = StageKind(s), modifierId = ActiveModifierId(s) ?? "", disabledControllerIds = (string[])ActiveDisabled(s).Clone(),
                stageHandsUsed = s.stage.primaryHandsUsed.ToArray(), inventoryCount = s.inventory.Count, ordinaryControllerSlots = s.controllerSlots,
                blueWave = s.hand.blueWave, scoreItemMultiplier = s.hand.doubleScore ? 2 : 0,
                poolDice = s.dice.OrderBy(d => d.createdSerial).Where(d => !s.hand.orderedLockedIds.Contains(d.dieId)).Select(d => s.hand.currentResults.First(x => x.dieId == d.dieId)).Where(r => r.hasRolled)
                    .Select(r => new PoolDie { dieId = r.dieId, faceId = r.currentFaceId, currentPip = r.currentPip, currentColor = r.currentColor }).ToArray(),
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
            Check(!Fx(s, "MustFill") || s.hand.orderedLockedIds.Count == Math.Min(s.stage.capacity, s.dice.Count), "首领减益“满员”：必须占满提交栏才能提交。");
            var snapshot = FreezeSubmission(s);
            var effectRng = new GameRng(s.effectRngState);
            snapshot.resolvedRandomChoices = snapshot.equippedControllers.Where(c => c.definitionId == "C53").Select(c => {
                int[] map = cfg.Controller("C53").parameters.factorUnits2;
                var trace = DrawRaw(s, effectRng, "effect", "C53", c.instanceId, map.Length);
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
            // v0.9 C97: a single submission worth at least target / targetDivisor pays gold, a limited number of times per stage.
            var war = cfg.Controller("C97").parameters; int warSources = Sources(s).Count(x => x.abilityId == "C97");
            if (warSources > 0 && s.stage.warIncomeCount < war.maxTriggersPerStage
                && ExactScoreMath.Parse(score.score) * war.targetDivisor >= ExactScoreMath.Parse(s.stage.target))
            {
                long income = checked((long)warSources * war.goldPerTrigger);
                s.stage.warIncomeCount++; s.gold = checked(s.gold + income);
                s.lastEffects.Add(new EffectRecord { kind = "WarIncome", sourceId = "C97", oldValue = s.stage.warIncomeCount, newValue = income });
            }
            // v1.0 boss tax: after scoring, submission income and C138, each submitted die costs gold down to zero.
            if (Fx(s, "TaxPerDie"))
            {
                long tax = Math.Min(s.gold, checked((long)snapshot.dice.Length * cfg.Modifier(s.stage.modifierId).amount));
                if (tax > 0) { s.gold -= tax; s.lastEffects.Add(new EffectRecord { kind = "GoldTaxed", sourceId = s.stage.modifierId, oldValue = s.gold + tax, newValue = s.gold }); }
            }
            if (score.primaryHandId != "scatter" && !s.stage.primaryHandsUsed.Contains(score.primaryHandId)) s.stage.primaryHandsUsed.Add(score.primaryHandId);
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
            if (!Fx(s, "GrowthFreeze")) foreach (var change in plan.growthChanges)
            {
                var owner = s.controllers.First(c => c.instanceId == change.ownerInstanceId); long before = owner.state.growth;
                owner.state.growth = change.reset ? 0 : checked(before + change.delta);
                if (owner.state.growth != before) s.lastEffects.Add(new EffectRecord { kind = change.reset ? "ControllerGrowthReset" : "ControllerGrown", sourceId = change.abilityId, detail = owner.instanceId, oldValue = before, newValue = owner.state.growth });
            }
            if (plan.earnedGold != 0)
            {
                s.gold = checked(s.gold + plan.earnedGold);
                s.lastEffects.Add(new EffectRecord { kind = "SubmissionIncome", sourceId = "C01", newValue = plan.earnedGold });
            }
            if (plan.goldSpent > 0)
            {
                // v1.0 C138: paid after the submission income, from the wallet frozen with the snapshot.
                long before = s.gold; s.gold = Math.Max(0, checked(before - plan.goldSpent));
                s.lastEffects.Add(new EffectRecord { kind = "GoldSpent", sourceId = "C138", oldValue = before, newValue = s.gold });
            }
            if (!string.IsNullOrEmpty(plan.upgradeHandId) && plan.upgradeRequested > 0)
            {
                var level = s.handLevels.First(h => h.handId == plan.upgradeHandId);
                int old = level.level; level.level = checked(level.level + plan.upgradeApplied);
                s.lastEffects.Add(new EffectRecord { kind = "HandGrown", sourceId = "C43", detail = plan.upgradeHandId, oldValue = old, newValue = level.level });
                Grow(s, "C61", checked((long)cfg.Controller("C61").parameters.growthPerEvent * plan.upgradeApplied));
            }
            if (Fx(s, "HandDowngrade") && score.primaryHandId != "scatter")
            {
                // v1.0 boss modifier: the primary hand of this submission loses levels after any C43 upgrade.
                var level = s.handLevels.First(h => h.handId == score.primaryHandId); int old = level.level;
                level.level = Math.Max(0, old - cfg.Modifier(s.stage.modifierId).amount);
                if (level.level != old) s.lastEffects.Add(new EffectRecord { kind = "HandDowngraded", sourceId = s.stage.modifierId, detail = score.primaryHandId, oldValue = old, newValue = level.level });
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
            // v1.0 face changes, in list order, after every older effect.
            foreach (var change in plan.faceChanges ?? Array.Empty<FaceChange>())
            {
                var target = s.dice.FirstOrDefault(d => d.dieId == change.dieId); var face = target == null ? null : target.faces.FirstOrDefault(f => f.faceId == change.faceId);
                if (face == null) continue;
                ApplyFaceChange(s, target, face, change.sourceId, change.kind, change.amount, change.text);
            }
        }

        /// <summary>v1.0: one permanent change to a face. Returns whether anything changed; always records the effect.</summary>
        private bool ApplyFaceChange(RunState s, DieInstance die, FaceState face, string sourceId, string kind, int amount, string text)
        {
            long before, after;
            switch (kind)
            {
                case "pipDelta": before = face.permanentPip; face.permanentPip = Math.Max(cfg.minPipValue, Math.Min(cfg.maxPipValue, checked(face.permanentPip + amount))); after = face.permanentPip; break;
                case "pipSet": Check(amount >= cfg.minPipValue && amount <= cfg.maxPipValue, "骰面点数无效。"); before = face.permanentPip; face.permanentPip = amount; after = face.permanentPip; break;
                case "color": Check(text == "red" || text == "blue", "骰面颜色无效。"); before = face.baseColor == "red" ? 1 : 0; face.baseColor = text; after = text == "red" ? 1 : 0; break;
                case "gold": before = face.isGold ? 1 : 0; face.isGold = amount != 0; after = face.isGold ? 1 : 0; break;
                case "texture": Check(text == "none" || text == "smooth" || text == "rough", "骰面纹理无效。"); before = face.texture == text ? 1 : 0; face.texture = text; after = 1; break;
                case "weightDelta": before = face.baseWeight; face.baseWeight = Math.Max(1, Math.Min(cfg.maxFaceWeight, checked(face.baseWeight + amount))); after = face.baseWeight; break;
                default: throw new RuleViolation("未知骰面变更。");
            }
            bool changed = before != after; if (changed) die.dataRevision++;
            s.lastEffects.Add(new EffectRecord { kind = "FaceChanged", sourceId = sourceId, dieId = die.dieId, faceId = face.faceId, detail = kind + (string.IsNullOrEmpty(text) ? "" : ":" + text), oldValue = before, newValue = after });
            return changed;
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
            // v0.9 stage-clear growth: C58 per stage, C59 per unused submission, C99 raises every owned controller's resale.
            Grow(s, "C58", cfg.Controller("C58").parameters.growthPerEvent);
            Grow(s, "C59", checked((long)cfg.Controller("C59").parameters.growthPerEvent * s.stage.remainingSubmissions));
            // v1.0 stage-clear growth: C129 per boss stage, C132 per completed pressure challenge, C141 counts down and expires.
            bool bossStage = StageKind(s) == "boss", lastStage = s.stage.stageIndex == cfg.stages.Length - 1;
            if (bossStage) Grow(s, "C129", cfg.Controller("C129").parameters.growthPerEvent);
            if (s.stage.risk) Grow(s, "C132", cfg.Controller("C132").parameters.growthPerEvent);
            Grow(s, "C141", 1);
            foreach (var expired in s.controllers.Where(c => c.definitionId == "C141" && c.state.growth >= cfg.Controller("C141").parameters.expireGrowth).ToArray())
            {
                s.controllers.Remove(expired); s.stage.destroyedControllerIds.Add(expired.instanceId);
                s.lastEffects.Add(new EffectRecord { kind = "ControllerExpired", sourceId = "C141", detail = expired.instanceId });
            }
            long saleBonus = checked((long)Sources(s).Count(x => x.abilityId == "C99") * cfg.Controller("C99").parameters.saleBonusPerStage);
            if (saleBonus > 0)
            {
                foreach (var c in s.controllers) c.state.saleBonus = checked(c.state.saleBonus + saleBonus);
                s.lastEffects.Add(new EffectRecord { kind = "SaleBonusGrown", sourceId = "C99", oldValue = s.controllers.Count, newValue = saleBonus });
            }
            ApplyStageEndControllers(s, bossStage, lastStage);
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

        /// <summary>
        /// v1.0 stage-end abilities. Face growth first (C120 then C121, once per source, permanent dice only), then the
        /// random generators left to right from the effect stream: C115 always, C112 unless the run just ended, and
        /// after a boss stage C114 and C116.
        /// </summary>
        private void ApplyStageEndControllers(RunState s, bool bossStage, bool lastStage)
        {
            var sources = Sources(s);
            foreach (string id in new[] { "C120", "C121" })
                for (int n = sources.Count(x => x.abilityId == id); n > 0; n--)
                    foreach (var die in s.dice.Where(d => d.lifetime == "permanent").OrderBy(d => d.createdSerial))
                    {
                        var ordered = die.faces.OrderBy(f => f.faceId, StringComparer.Ordinal).ToArray();
                        var face = id == "C120" ? ordered.OrderBy(f => f.permanentPip).First() : ordered.OrderByDescending(f => f.permanentPip).First();
                        if (id == "C120" ? face.permanentPip < cfg.maxPipValue : face.permanentPip > cfg.minPipValue) ApplyFaceChange(s, die, face, id, "pipDelta", cfg.Controller(id).parameters.delta, null);
                    }
            var rng = new GameRng(s.effectRngState);
            foreach (var source in sources)
            {
                var p = source.abilityId == null ? null : cfg.Controller(source.abilityId).parameters;
                if (source.abilityId == "C115")
                {
                    for (int i = 0; i < p.facesPerStage; i++)
                    {
                        var dice = s.dice.Where(d => d.lifetime == "permanent").OrderBy(d => d.createdSerial).ToArray(); if (dice.Length == 0) break;
                        var die = dice[DrawRaw(s, rng, "effect", "C115Die", source.emitterId, dice.Length).rawTicket];
                        var faces = die.faces.OrderBy(f => f.faceId, StringComparer.Ordinal).ToArray(); var face = faces[DrawRaw(s, rng, "effect", "C115Face", die.dieId, faces.Length).rawTicket];
                        var trace = DrawRaw(s, rng, "effect", "C115Upgrade", die.dieId + "/" + face.faceId, p.upgradeIds.Length); string upgrade = p.upgradeIds[trace.rawTicket]; trace.rawValue = trace.appliedValue = upgrade;
                        bool legal = UpgradeTargetLegal(die, face, upgrade);
                        s.lastEffects.Add(new EffectRecord { kind = legal ? "RandomUpgrade" : "RandomUpgradeMissed", sourceId = "C115", dieId = die.dieId, faceId = face.faceId, detail = upgrade });
                        if (legal) { s.effectRngState = rng.State; ApplyUpgrade(s, upgrade, die.dieId, face.faceId); rng = new GameRng(s.effectRngState); }
                    }
                }
                else if (source.abilityId == "C112" && !lastStage)
                {
                    var candidates = cfg.controllers.Where(c => c.rarity == p.rarity && !Has(s, c.id)).OrderBy(c => c.id, StringComparer.Ordinal).ToArray();
                    if (candidates.Length == 0 || ControllerRules.OrdinaryUsed(s.controllers) >= s.controllerSlots || s.controllers.Count >= cfg.maxControllerInstances) continue;
                    var trace = DrawRaw(s, rng, "effect", "C112Controller", source.emitterId, candidates.Length); var picked = candidates[trace.rawTicket]; trace.rawValue = trace.appliedValue = picked.id;
                    int serial = s.nextItemSerial++;
                    var granted = new ControllerInstance { instanceId = "ITEM_" + serial.ToString("D5"), acquiredSerial = serial, definitionId = picked.id, enchantmentId = "none" };
                    if (granted.definitionId == "C45") { var color = DrawRaw(s, rng, "effect", "C45Acquire", granted.instanceId, 2); granted.state.activeColor = color.rawValue = color.appliedValue = color.rawTicket == 0 ? "red" : "blue"; }
                    s.controllers.Add(granted);
                    s.lastEffects.Add(new EffectRecord { kind = "ControllerGranted", sourceId = "C112", detail = granted.instanceId + ":" + granted.definitionId });
                }
                else if (source.abilityId == "C114" && bossStage && !lastStage)
                {
                    for (int i = 0; i < p.enchantsPerBoss; i++) if (!EnchantRandomController(s, rng, "C114", source.emitterId)) break;
                }
                else if (source.abilityId == "C116" && bossStage && !lastStage)
                {
                    for (int i = 0; i < p.dicePerBoss; i++)
                    {
                        if (PermanentCount(s) >= cfg.maxPermanentDice || s.dice.Count(d => d.lifetime == "permanent") >= cfg.maxActiveDice) break;
                        var die = GenerateDie(s, rng, "effect"); die.createdSerial = s.nextDieSerial++; die.dieId = "D" + die.createdSerial.ToString("D3"); die.lifetime = "permanent"; die.createdStageId = "";
                        s.dice.Add(die); s.lastEffects.Add(new EffectRecord { kind = "DieAdded", sourceId = "C116", dieId = die.dieId, detail = source.emitterId });
                    }
                }
            }
            s.effectRngState = rng.State;
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
            var sources = Sources(s);
            // v1.0: O10 raises the cap; C133 pays a bounty on boss and pressure stages; NoReward voids the stage reward.
            long principalCap = InterestPrincipalCap(s);
            var hunter = cfg.Controller("C133").parameters; bool boss = StageKind(s) == "boss", voided = Fx(s, "NoReward");
            long bounty = checked((long)sources.Count(x => x.abilityId == "C133") * ((boss ? hunter.goldPerBoss : 0) + (s.stage.risk ? hunter.goldPerPressure : 0)));
            long controllerIncome = checked((long)sources.Count(x => x.abilityId == "C95") * cfg.Controller("C95").parameters.goldPerStage + bounty);
            long principal = Math.Min(s.gold, principalCap);
            int pigs = Sources(s).Count(x => x.abilityId == "C01");
            int interestPerTen = 1 + pigs * cfg.Controller("C01").parameters.extraInterestPerTen;
            long interest = checked(interestPerTen * (principal / cfg.interestDivisor));
            long unusedReward = voided ? 0 : checked(s.stage.remainingSubmissions * cfg.goldPerUnusedSubmission);
            int baseReward = voided ? 0 : s.stage.reward; int cycle = s.stage.stageIndex / 3;
            return new StageSettlement {
                settlementId = s.runId + "/" + s.stage.stageId, stageId = s.stage.stageId,
                rewardVoided = voided, bountyIncome = bounty, stageKind = StageKind(s),
                lootKind = boss && s.stage.stageIndex < cfg.stages.Length - 1 && cycle < s.cycles.Count ? s.cycles[cycle].lootKind ?? "" : "",
                goldBefore = s.gold, baseReward = baseReward, remainingSubmissions = s.stage.remainingSubmissions,
                unusedSubmissionReward = unusedReward, interest = interest, interestPrincipal = principal,
                interestPrincipalCap = principalCap, controllerIncome = controllerIncome,
                goldAfter = checked(s.gold + baseReward + unusedReward + interest + controllerIncome),
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
            RequirePhase(s, "Shop"); RequireLootResolved(s); // v1.1: boss loot first
            Check(s.dice.Count > 0, "骰组耗尽，不能开始下一关。");
            PrepareStagePreview(s, s.stage.stageIndex + 1);
        }

        /// <summary>
        /// v1.0: lifts the boss modifier before the first roll of the stage (I08). Budgets, capacity and the target are
        /// recomputed without the modifier and the difference is added to the current values, so earlier item bonuses stay.
        /// </summary>
        private void NullifyModifier(RunState s, string sourceId)
        {
            var with = ActiveRules(s); var bindings = s.stage.mimicBindings; string modifier = s.stage.modifierId;
            int capacity = ControllerRules.Capacity(s.controllers, cfg, false, bindings, with), submissions = ControllerRules.SubmissionBudget(s.controllers, cfg, bindings, with), rolls = ControllerRules.RollBudget(s.controllers, cfg, 0, bindings, with);
            s.stage.modifierNullified = true; s.stage.disabledControllerIds = Array.Empty<string>();
            var without = ActiveRules(s);
            if (!(s.isDebug && s.debugCapacityOverride > 0))
                s.stage.capacity = Math.Max(1, Math.Min(s.isDebug ? cfg.debug.maxSubmitCapacity : cfg.maxSubmitCapacity, s.stage.capacity + ControllerRules.Capacity(s.controllers, cfg, false, bindings, without) - capacity));
            s.stage.remainingSubmissions = Math.Max(1, checked(s.stage.remainingSubmissions + ControllerRules.SubmissionBudget(s.controllers, cfg, bindings, without) - submissions));
            s.hand.rollsRemaining = Math.Max(0, checked(s.hand.rollsRemaining + ControllerRules.RollBudget(s.controllers, cfg, 0, bindings, without) - rolls));
            if (ControllerRules.ModifierHas(cfg, modifier, "TargetPercent"))
            {
                // A normal run takes the target from the configuration again; a debug scene may carry a custom target, which is scaled back.
                s.stage.target = s.isDebug ? ExactScoreMath.SaveDecimal(ExactScoreMath.Parse(s.stage.target) * 100 / cfg.Modifier(modifier).amount) : StageTarget(s, cfg.stages[s.stage.stageIndex], s.stage.risk);
                s.stage.qualified = s.stage.qualified || ExactScoreMath.Compare(s.stage.score, s.stage.target) >= 0;
            }
            s.stage.submissionBonusSourceIds = s.controllers.Where(c => c.enchantmentId == "submit1").Select(c => c.instanceId).ToArray();
            s.hand.rollBonusSourceIds = s.controllers.Where(c => c.enchantmentId == "roll1").Select(c => c.instanceId).ToArray();
            s.lastEffects.Add(new EffectRecord { kind = "ModifierNullified", sourceId = sourceId, detail = ":" + modifier });
        }

        private void UseItem(RunState s, SessionCommand command)
        {
            RequirePhase(s, "HandReady", "Selecting");
            var item = s.inventory.FirstOrDefault(x => x.instanceId == command.itemInstanceId);
            Check(item != null, "道具不存在或已使用。");
            Check(!Fx(s, "NoItems") || item.contentId == "I08", "首领减益“徒手”：本关不能使用道具。");
            switch (item.contentId)
            {
                case "I01":
                    Check(s.phase == "Selecting" && s.hand.hasRolled && s.hand.orderedLockedIds.Count > 0, "红色浪潮需要至少一颗已锁骰。");
                    Check(!s.hand.redWave, "本手红色浪潮已经生效。"); Check(!s.hand.blueWave, "本手蓝色浪潮已经生效，不能再用红色浪潮。"); s.hand.redWave = true;
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
                    Check(!s.hand.smallBuff, "本手小数多来已经生效。"); // v1.1: named the card C37 by mistake (text proposal §5 error 1)
                    Check(s.hand.rollsRemaining > 0 && s.dice.Any(x => !s.hand.orderedLockedIds.Contains(x.dieId) && x.faces.Any(f => f.permanentPip == cfg.minPipValue)), "没有可投掷且含永久1点面的未锁骰。"); s.hand.smallBuff = true;
                    break;
                case "I06": s.stage.remainingSubmissions = checked(s.stage.remainingSubmissions + 1); break;
                case "I07": s.stage.stageRollBonus = checked(s.stage.stageRollBonus + 1); s.hand.rollsRemaining = checked(s.hand.rollsRemaining + 1); break;
                // v1.0
                case "I08":
                    Check(s.phase == "HandReady" && s.stage.submissionOrdinal == 0 && s.hand.rollsUsed == 0 && ActiveModifierId(s) != null, "破咒符只能在首领关第一手首掷之前、减益仍生效时使用。");
                    NullifyModifier(s, "I08");
                    break;
                case "I09": Check(!s.hand.doubleScore, "本手灵光一闪已经生效。"); s.hand.doubleScore = true; break;
                case "I10":
                    Check(s.phase == "Selecting" && s.hand.hasRolled && s.hand.orderedLockedIds.Count > 0, "蓝色浪潮需要至少一颗已锁骰。");
                    Check(!s.hand.blueWave, "本手蓝色浪潮已经生效。"); Check(!s.hand.redWave, "本手红色浪潮已经生效，不能再用蓝色浪潮。"); s.hand.blueWave = true;
                    break;
                case "I11":
                    {
                        Check(s.phase == "Selecting" && s.hand.hasRolled && s.hand.orderedLockedIds.Count > 0, "点金笔需要至少一颗已锁骰。");
                        var die = FindDie(s, s.hand.orderedLockedIds[0]); var result = s.hand.currentResults.First(x => x.dieId == die.dieId);
                        var face = die.faces.First(f => f.faceId == result.currentFaceId); Check(!face.isGold, "第一颗已锁骰的朝上面已经是黄金面。");
                        ApplyFaceChange(s, die, face, "I11", "gold", 1, null);
                        break;
                    }
                case "I12":
                    Check(s.stage.capacity < (s.isDebug ? cfg.debug.maxSubmitCapacity : cfg.maxSubmitCapacity), "提交栏容量已到上限。");
                    s.stage.capacity++; s.stage.itemCapacityBonus++;
                    break;
                default: throw new RuleViolation("未知道具。");
            }
            s.inventory.Remove(item); s.consumedItemIds.Add(item.instanceId);
            s.lastEffects.Add(new EffectRecord { kind = "ItemUsed", sourceId = item.contentId, detail = item.instanceId });
            Grow(s, "C134", cfg.Controller("C134").parameters.growthPerEvent);
        }
    }
}
