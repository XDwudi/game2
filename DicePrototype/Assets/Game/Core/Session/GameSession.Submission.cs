using System;
using System.Collections.Generic;
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
                // v1.1.1 sections 4.5, 11, 12.1: the stage score before this submission and the target in force right now
                // (pressure choice and the boss TargetPercent included, the plain target again once the modifier is lifted).
                // Preview and Submit both freeze here, so C156 reads the same two numbers in both.
                stageScoreBefore = s.stage.score, stageTarget = s.stage.target,
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
                "先掷骰并锁定至少 1 颗骰子，再提交。");
            Check(s.stage.remainingSubmissions > 0 && !s.stage.settled, "本关的提交次数用完了。");
            Check(!Fx(s, "MustFill") || s.hand.orderedLockedIds.Count == Math.Min(s.stage.capacity, s.dice.Count), "首领减益“满员”：必须占满提交栏才能提交。");
            var snapshot = FreezeSubmission(s);
            var effectRng = new GameRng(s.effectRngState);
            // v1.1.1 section 4.1 and A06: every real, effective C53 draws once in card order; a C53 disabled for the
            // stage has no ability and consumes no ticket.
            snapshot.resolvedRandomChoices = snapshot.equippedControllers.Where(c => c.definitionId == "C53" && !snapshot.disabledControllerIds.Contains(c.instanceId)).Select(c => {
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
            // v1.1.1 section 5.2: the final integer score of this hand against the run's highest single hand. Only a
            // strictly higher score is a record; the flag stays for the rest of the stage and feeds C125 at settlement.
            if (ExactScoreMath.Compare(score.score, s.highestHandScore) > 0)
            {
                s.lastEffects.Add(new EffectRecord { kind = "HandRecordBroken", detail = s.highestHandScore + ":" + score.score });
                s.highestHandScore = score.score; s.stage.handRecordBroken = true;
            }

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
            // v1.1.1 section 7.3: the glass dice of this submission are judged last, after every other after-score effect.
            BreakSubmittedGlass(s, score.afterScorePlan);
            if (score.primaryHandId != "scatter" && !s.stage.primaryHandsUsed.Contains(score.primaryHandId)) s.stage.primaryHandsUsed.Add(score.primaryHandId);
            s.stage.qualified = s.stage.qualified || ExactScoreMath.Compare(s.stage.score, s.stage.target) >= 0;
            if (s.stage.remainingSubmissions == 0 || s.dice.Count == 0)
            {
                // A qualified stage settles even when the last die just broke; the settlement then decides what follows.
                if (s.stage.qualified) FinalizeStage(s);
                else { s.phase = "RunFailed"; s.failureReason = s.stage.remainingSubmissions == 0 ? "SubmissionsExhausted" : "NoDice"; }
            }
            else StartHand(s);
            return score.score;
        }

        /// <summary>
        /// v1.1.1 section 7.3: only the dice that were glass when this valid submission was frozen are at risk, sealed
        /// or scoring 0 included; pool dice and rolls never are. In frozen submission order each draws one ticket of
        /// the effect stream (0-14 breaks at 15%). A broken die leaves at once; nothing already paid is taken back.
        /// </summary>
        private void BreakSubmittedGlass(RunState s, AfterScorePlan plan)
        {
            if (plan.glassRiskDieIds == null || plan.glassRiskDieIds.Length == 0) return;
            var rng = new GameRng(s.effectRngState); int percent = GlassBreakPercent(s);
            foreach (string dieId in plan.glassRiskDieIds)
            {
                if (!DrawRisk(s, rng, "Glass", dieId, percent)) continue;
                s.dice.RemoveAll(d => d.dieId == dieId); s.hand.currentResults.RemoveAll(r => r.dieId == dieId); s.hand.orderedLockedIds.Remove(dieId);
                s.lastEffects.Add(new EffectRecord { kind = "DieBroken", sourceId = "U05", dieId = dieId });
            }
            s.effectRngState = rng.State;
        }

        private void ApplySubmissionEffects(RunState s, SubmissionSnapshot snapshot, ScoreBreakdown score)
        {
            var plan = score.afterScorePlan;
            ApplyPermanentEffects(s, snapshot, plan);
            // v1.1.1 sections 5 and 5.1: the plan lists real, effective instances only; under GrowthFreeze nothing moves,
            // the halving of C54 included. `halve` keeps half of what the card has grown, rounded down to its unit; the
            // start value and the catch-up are never touched. GrowthHalf only changes what the scorer reads.
            if (!Fx(s, "GrowthFreeze")) foreach (var change in plan.growthChanges)
            {
                var owner = s.controllers.First(c => c.instanceId == change.ownerInstanceId); long before = owner.state.growth;
                owner.state.growth = change.halve ? before / 2 : change.reset ? 0 : checked(before + change.delta);
                if (owner.state.growth != before) s.lastEffects.Add(new EffectRecord { kind = change.halve ? "ControllerGrowthHalved" : change.reset ? "ControllerGrowthReset" : "ControllerGrown", sourceId = change.abilityId, detail = owner.instanceId, oldValue = before, newValue = owner.state.growth });
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
                Grow(s, "C61", checked((long)cfg.Controller("C61").parameters.growthEvent * plan.upgradeApplied));
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
            // v1.1.1 section 6.1: C07 -> C26 -> C36/C37 -> C15 -> C38, then the per-source plans in card order. Every
            // step is one execution of its ability and is credited by Rewrite (section 6 "改写与计数").
            var bindings = plan.cultivationSourceBindings ?? Array.Empty<CultivationSourceBinding>();
            Func<string, string, CultivationSourceBinding[]> sourcesOf = (a, b) => bindings.Where(x => x.abilityId == a || x.abilityId == b).ToArray();
            Func<int, FaceRef> submittedFace = index => { var die = FindDie(s, snapshot.dice[index].dieId); return new FaceRef(die, die.faces.First(f => f.faceId == snapshot.dice[index].faceId)); };
            if (plan.copyFirstToSecond)
            {
                // The frozen first die is copied field by field: the weights come with it (no extra gilding weight) and
                // the target keeps its own history marks, so only the faces really rewritten here get the mark.
                var source = snapshot.dice[0]; var target = FindDie(s, snapshot.dice[1].dieId);
                Rewrite(s, sourcesOf("C07", null), target.faces.Select(f => new FaceRef(target, f)), () =>
                {
                    bool changed = target.isGlass != source.isGlass;
                    foreach (var face in target.faces)
                    {
                        var from = source.permanentFacesCopy.FirstOrDefault(f => f.faceId == face.faceId);
                        if (from == null || SameFaceProperties(face, from)) continue;
                        face.permanentPip = from.permanentPip; face.baseColor = from.baseColor; face.texture = from.texture; face.isGold = from.isGold; face.baseWeight = from.baseWeight; changed = true;
                    }
                    target.isGlass = source.isGlass;
                    if (changed) target.dataRevision++;
                    s.lastEffects.Add(new EffectRecord { kind = "DieCopied", sourceId = "C07", dieId = target.dieId, otherDieId = source.dieId });
                });
            }
            if (plan.dyeAllFacesRed)
            {
                var target = FindDie(s, snapshot.dice[0].dieId);
                Rewrite(s, sourcesOf("C26", null), target.faces.Select(f => new FaceRef(target, f)), () =>
                {
                    bool changed = target.faces.Any(f => f.baseColor != "red");
                    foreach (var face in target.faces) face.baseColor = "red";
                    if (changed) target.dataRevision++;
                    s.lastEffects.Add(new EffectRecord { kind = "DieDyed", sourceId = "C26", dieId = target.dieId, detail = "red" });
                });
            }
            if (plan.growthIndices.Length > 0)
            {
                // C36/C37: the net delta is applied once to each planned face and clamped on the value it has now. A face
                // that really changed counts for every triggered C36 and C37 source (ruling A13); net 0 or a clamp does not.
                var targets = plan.growthIndices.Select(submittedFace).ToArray();
                Rewrite(s, sourcesOf("C36", "C37"), targets, () =>
                {
                    foreach (var target in targets)
                    {
                        int old = target.face.permanentPip;
                        target.face.permanentPip = Math.Max(cfg.minPipValue, Math.Min(cfg.maxPipValue, checked(old + plan.growthNetDelta)));
                        if (target.face.permanentPip != old) target.die.dataRevision++;
                        s.lastEffects.Add(new EffectRecord { kind = "FaceValueChanged", sourceId = plan.growthNetDelta == 0 ? "C36+C37" : plan.growthNetDelta > 0 ? "C36" : "C37",
                            dieId = target.die.dieId, faceId = target.face.faceId, oldValue = old, newValue = target.face.permanentPip });
                    }
                });
            }
            if (plan.redSpreadIndices.Length > 0)
                Rewrite(s, sourcesOf("C15", null), plan.redSpreadIndices.Select(submittedFace).ToArray(), () => { foreach (int index in plan.redSpreadIndices) DyeSubmittedFace(s, snapshot.dice[index], "red", "C15"); });
            if (plan.blueSpreadIndices.Length > 0)
                Rewrite(s, sourcesOf("C38", null), plan.blueSpreadIndices.Select(submittedFace).ToArray(), () => { foreach (int index in plan.blueSpreadIndices) DyeSubmittedFace(s, snapshot.dice[index], "blue", "C38"); });
            // v1.0 face changes, in list order, after every older effect. The list is built source by source, so the
            // consecutive entries of one emitter are one execution of its ability.
            var changes = plan.faceChanges ?? Array.Empty<FaceChange>();
            for (int start = 0; start < changes.Length;)
            {
                int end = start; while (end < changes.Length && changes[end].emitterId == changes[start].emitterId && changes[end].sourceId == changes[start].sourceId) end++;
                var first = changes[start];
                var run = changes.Skip(start).Take(end - start).Select(change =>
                {
                    var die = s.dice.FirstOrDefault(d => d.dieId == change.dieId);
                    return new { change, target = new FaceRef(die, die == null ? null : die.faces.FirstOrDefault(f => f.faceId == change.faceId)) };
                }).Where(x => x.target.face != null).ToArray();
                start = end;
                Rewrite(s, new[] { new CultivationSourceBinding { abilityId = first.sourceId, emitterId = first.emitterId, stateOwnerId = first.stateOwnerId } }, run.Select(x => x.target).ToArray(), () =>
                {
                    foreach (var x in run)
                    {
                        // Section 7.2: a card that gilds a face goes through the shared gilding entry. Section 6.1 C123:
                        // its minus only lowers a face that is above the floor now, and never below the floor.
                        if (x.change.kind == "gold" && x.change.amount != 0) GildFace(s, x.target.die, x.target.face, x.change.sourceId);
                        else ApplyFaceChange(s, x.target.die, x.target.face, x.change.sourceId, x.change.kind, x.change.amount, x.change.text,
                            x.change.sourceId == "C123" && x.change.kind == "pipDelta" && x.change.amount < 0 ? cfg.Controller("C123").parameters.highPipFloor : 0);
                    }
                });
            }
        }

        /// <summary>A physical face together with the die that owns it.</summary>
        private struct FaceRef
        {
            public readonly DieInstance die; public readonly FaceState face;
            public FaceRef(DieInstance die, FaceState face) { this.die = die; this.face = face; }
        }

        /// <summary>The five permanent properties section 6 compares: pip, base colour, texture, gold and base weight.</summary>
        private static bool SameFaceProperties(FaceState a, FaceState b) => a.permanentPip == b.permanentPip && a.baseColor == b.baseColor
            && a.texture == b.texture && a.isGold == b.isGold && a.baseWeight == b.baseWeight;

        /// <summary>
        /// v1.1.1 section 6 "改写与计数": runs one execution of a face-rewriting ability and compares the five permanent
        /// properties of every face it planned to rewrite before and after. A face of a permanent die that differs is a
        /// successful rewrite: it gets the `cultivated` mark and every effective source that took part credits its real
        /// owner once (the copied card for a mimic), up to the cap. A temporary die, a repaint in the same colour and a
        /// value stopped by a limit count nothing. One "FaceCultivated" effect is recorded per source and face.
        /// </summary>
        private void Rewrite(RunState s, CultivationSourceBinding[] sources, IEnumerable<FaceRef> planned, Action execute)
        {
            var watched = planned.Where(p => p.die != null && p.face != null).GroupBy(p => p.face).Select(g => g.First())
                .Select(p => new { p.die, p.face, before = p.face.Clone() }).ToArray();
            execute();
            int cap = checked(cfg.cultivationFacesPerSaleBonus * cfg.cultivationMaxSaleBonus);
            foreach (var w in watched)
            {
                if (w.die.lifetime != "permanent" || SameFaceProperties(w.before, w.face)) continue;
                w.face.cultivated = true;
                foreach (var source in sources)
                {
                    var owner = s.controllers.FirstOrDefault(c => c.instanceId == source.stateOwnerId);
                    int before = owner == null ? 0 : owner.state.cultivationFacesChanged, after = before;
                    if (owner != null && cfg.Controller(owner.definitionId).isCultivation) owner.state.cultivationFacesChanged = after = Math.Min(cap, before + 1);
                    s.lastEffects.Add(new EffectRecord { kind = "FaceCultivated", sourceId = source.abilityId, dieId = w.die.dieId, faceId = w.face.faceId,
                        detail = source.emitterId + ":" + source.stateOwnerId, oldValue = before, newValue = after });
                }
            }
        }

        /// <summary>
        /// v1.1.1 section 7.2: the one entry for turning a face gold. A face that was not gold becomes gold and gains
        /// gildedFaceWeightDelta base weight up to maxFaceWeight; a face that is already gold changes nothing. The base
        /// colour stays. Returns whether the face was gilded now. Callers in this package: C117, C127 and the U06 result
        /// of C115. The shop upgrade U06, the item I11 and the L5 loot die are wired to it by W3b-2.
        /// </summary>
        private bool GildFace(RunState s, DieInstance die, FaceState face, string sourceId)
        {
            bool wasGold = face.isGold; int weight = face.baseWeight; bool gilded = Gild(face);
            if (gilded) die.dataRevision++;
            s.lastEffects.Add(new EffectRecord { kind = "FaceChanged", sourceId = sourceId, dieId = die.dieId, faceId = face.faceId, detail = "gold", oldValue = wasGold ? 1 : 0, newValue = 1 });
            if (face.baseWeight != weight) s.lastEffects.Add(new EffectRecord { kind = "FaceChanged", sourceId = sourceId, dieId = die.dieId, faceId = face.faceId, detail = "weightDelta", oldValue = weight, newValue = face.baseWeight });
            return gilded;
        }
        /// <summary>The state change of <see cref="GildFace"/> alone, for a face that belongs to no die of the run yet (a generated offer).</summary>
        private bool Gild(FaceState face)
        {
            if (face.isGold) return false;
            face.isGold = true;
            if (face.baseWeight < cfg.maxFaceWeight) face.baseWeight = Math.Min(cfg.maxFaceWeight, checked(face.baseWeight + cfg.gildedFaceWeightDelta));
            return true;
        }

        /// <summary>v1.0: one permanent change to a face. Returns whether anything changed; always records the effect.
        /// v1.1.1: a negative pipDelta with <paramref name="pipFloor"/> above 0 only lowers a face that is above that
        /// floor and stops at it (C123).</summary>
        private bool ApplyFaceChange(RunState s, DieInstance die, FaceState face, string sourceId, string kind, int amount, string text, int pipFloor = 0)
        {
            long before, after;
            switch (kind)
            {
                case "pipDelta":
                    {
                        before = face.permanentPip; int floor = amount < 0 && pipFloor > 0 ? pipFloor : cfg.minPipValue;
                        if (amount >= 0 || face.permanentPip > floor) face.permanentPip = Math.Max(floor, Math.Min(cfg.maxPipValue, checked(face.permanentPip + amount)));
                        after = face.permanentPip; break;
                    }
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

        private void DyeSubmittedFace(RunState s, SubmissionDie frozen, string color, string sourceId)
        {
            var target = FindDie(s, frozen.dieId); var face = target.faces.First(f => f.faceId == frozen.faceId);
            if (face.baseColor != color) { face.baseColor = color; target.dataRevision++; }
            s.lastEffects.Add(new EffectRecord { kind = "FaceDyed", sourceId = sourceId, dieId = target.dieId, faceId = frozen.faceId, detail = color });
        }

        private void EndStage(RunState s)
        {
            RequirePhase(s, "HandReady", "Selecting");
            Check(s.stage.qualified && !s.stage.settled && s.stage.remainingSubmissions > 0, "达到目标后才能结束本关。");
            // Unsubmitted locks are discarded: no score, Submit hooks or budget consumption.
            FinalizeStage(s);
        }

        private void FinalizeStage(RunState s)
        {
            if (s.stage.settled) return;
            Check(s.stage.qualified, "达到目标后才能结束本关。");
            var fundSources = Sources(s).Where(x => x.abilityId == "C52").Select(x => x.emitterId).Distinct(StringComparer.Ordinal).ToArray();
            s.lastStageSettlement = CalculateStageSettlement(s);
            // v1.1.1 section 13: the clear ratio of this stage = final stage score / the target the settlement used, as a
            // reduced fraction, compared with the record by cross-multiplication. An equal value keeps the record.
            {
                var score = ExactScoreMath.Parse(s.stage.score); var target = ExactScoreMath.Parse(s.stage.target); var gcd = System.Numerics.BigInteger.GreatestCommonDivisor(score, target);
                var numerator = score / gcd; var denominator = target / gcd;
                if (numerator * ExactScoreMath.Parse(s.highestClearRatioDenominator) > ExactScoreMath.Parse(s.highestClearRatioNumerator) * denominator)
                {
                    s.lastEffects.Add(new EffectRecord { kind = "ClearRatioRecord", detail = s.highestClearRatioNumerator + "/" + s.highestClearRatioDenominator + ":" + ExactScoreMath.SaveDecimal(numerator) + "/" + ExactScoreMath.SaveDecimal(denominator) });
                    s.highestClearRatioNumerator = ExactScoreMath.SaveDecimal(numerator); s.highestClearRatioDenominator = ExactScoreMath.SaveDecimal(denominator);
                }
            }
            foreach (string emitterId in fundSources)
            {
                var emitter = s.controllers.First(c => c.instanceId == emitterId);
                emitter.state.fundAccrual = checked(emitter.state.fundAccrual + 3);
                s.lastEffects.Add(new EffectRecord { kind = "FundAccrued", sourceId = "C52", detail = emitterId, newValue = emitter.state.fundAccrual });
            }
            // v0.9 stage-clear growth: C58 per stage, C59 per unused submission, C99 raises every owned controller's resale.
            Grow(s, "C58", cfg.Controller("C58").parameters.growthEvent);
            Grow(s, "C59", checked((long)cfg.Controller("C59").parameters.growthEvent * s.stage.remainingSubmissions));
            // v1.0 stage-clear growth: C129 per boss stage, C132 per completed pressure challenge, C141 counts down and expires.
            bool bossStage = StageKind(s) == "boss", lastStage = s.stage.stageIndex == cfg.stages.Length - 1;
            if (bossStage) Grow(s, "C129", cfg.Controller("C129").parameters.growthEvent);
            if (s.stage.risk) Grow(s, "C132", cfg.Controller("C132").parameters.growthEvent);
            // v1.1.1 section 5.2: C125 grows once per cleared stage in which the run's highest single hand was beaten,
            // however many times it was beaten. Like every Grow, only real, effective, unfrozen instances move.
            if (s.stage.handRecordBroken) Grow(s, "C125", cfg.Controller("C125").parameters.growthEvent);
            // C141 counts its decays in `growth`; at expireGrowth the card is removed on the spot (no sale event).
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
            // v1.1.1 section 5.3: the star pip belongs to the stage that just ended; between stages a C137 has none (0)
            // and the next stage draws again.
            foreach (var c in s.controllers) c.state.starPip = 0;
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
            // v1.1.1 section 6.1: every C120 source first, then every C121 source, each in emitter order; per source and
            // per permanent die (creation order) the up to facesPerDie lowest / highest faces that can still move, ties
            // by face id, each moved by one step.
            foreach (string id in new[] { "C120", "C121" })
                foreach (var source in sources.Where(x => x.abilityId == id))
                {
                    var p = cfg.Controller(id).parameters; var credit = new[] { new CultivationSourceBinding { abilityId = id, emitterId = source.emitterId, stateOwnerId = source.stateOwnerId } };
                    foreach (var die in s.dice.Where(d => d.lifetime == "permanent").OrderBy(d => d.createdSerial).ToArray())
                    {
                        var movable = die.faces.Where(f => id == "C120" ? f.permanentPip < cfg.maxPipValue : f.permanentPip > cfg.minPipValue);
                        var picked = (id == "C120" ? movable.OrderBy(f => f.permanentPip) : movable.OrderByDescending(f => f.permanentPip)).ThenBy(f => f.faceId, StringComparer.Ordinal).Take(p.facesPerDie).ToArray();
                        Rewrite(s, credit, picked.Select(f => new FaceRef(die, f)).ToArray(), () => { foreach (var face in picked) ApplyFaceChange(s, die, face, id, "pipDelta", p.delta, null); });
                    }
                }
            var rng = new GameRng(s.effectRngState);
            foreach (var source in sources)
            {
                var p = source.abilityId == null ? null : cfg.Controller(source.abilityId).parameters;
                if (source.abilityId == "C115")
                {
                    // v1.1.1 section 6.1: up to facesPerStage different faces per source. Each step lists, by die creation
                    // order and face id, the faces this source has not rewritten yet that still accept at least one of its
                    // upgrades, draws one of them, then draws one of that face's legal upgrades in upgrade-id order: two
                    // tickets of the effect stream. An empty list ends the source without a draw.
                    var done = new HashSet<string>(StringComparer.Ordinal); var upgradeIds = p.upgradeIds.OrderBy(x => x, StringComparer.Ordinal).ToArray();
                    var credit = new[] { new CultivationSourceBinding { abilityId = "C115", emitterId = source.emitterId, stateOwnerId = source.stateOwnerId } };
                    for (int i = 0; i < p.facesPerStage; i++)
                    {
                        var candidates = s.dice.Where(d => d.lifetime == "permanent").OrderBy(d => d.createdSerial)
                            .SelectMany(d => d.faces.OrderBy(f => f.faceId, StringComparer.Ordinal).Select(f => new FaceRef(d, f)))
                            .Where(x => !done.Contains(x.die.dieId + "/" + x.face.faceId) && upgradeIds.Any(u => UpgradeTargetLegal(x.die, x.face, u))).ToArray();
                        if (candidates.Length == 0) break;
                        var faceTrace = DrawRaw(s, rng, "effect", "C115Face", source.emitterId, candidates.Length); var target = candidates[faceTrace.rawTicket];
                        string key = target.die.dieId + "/" + target.face.faceId; faceTrace.rawValue = faceTrace.appliedValue = key; done.Add(key);
                        var legal = upgradeIds.Where(u => UpgradeTargetLegal(target.die, target.face, u)).ToArray();
                        var trace = DrawRaw(s, rng, "effect", "C115Upgrade", key, legal.Length); string upgrade = legal[trace.rawTicket]; trace.rawValue = trace.appliedValue = upgrade;
                        s.lastEffects.Add(new EffectRecord { kind = "RandomUpgrade", sourceId = "C115", dieId = target.die.dieId, faceId = target.face.faceId, detail = upgrade });
                        Rewrite(s, credit, new[] { target }, () =>
                        {
                            switch (upgrade)
                            {
                                case "U03": ApplyFaceChange(s, target.die, target.face, "C115", "color", 0, "red"); break;
                                case "U04": ApplyFaceChange(s, target.die, target.face, "C115", "color", 0, "blue"); break;
                                case "U06": GildFace(s, target.die, target.face, "C115"); break;
                                case "U08": ApplyFaceChange(s, target.die, target.face, "C115", "pipDelta", 1, null); break;
                                default: throw new RuleViolation(UnknownDieUpgrade);
                            }
                        });
                    }
                }
                else if (source.abilityId == "C112" && !lastStage)
                {
                    var candidates = cfg.controllers.Where(c => c.rarity == p.rarity && !Has(s, c.id)).OrderBy(c => c.id, StringComparer.Ordinal).ToArray();
                    if (candidates.Length == 0 || ControllerRules.OrdinaryUsed(s.controllers, cfg) >= s.controllerSlots || s.controllers.Count >= cfg.maxControllerInstances) continue;
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
            long interest = InterestOnPrincipal(principal, pigs);
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
            Check(!Fx(s, "NoItems") || item.contentId == "I08", ItemsForbidden);
            // v1.1.1 section 8: a key ring has no effect of its own; it is spent by the UnlockDie command.
            Check(item.contentId != "I13", KeyRingUsesUnlock);
            switch (item.contentId)
            {
                case "I01":
                    Check(s.phase == "Selecting" && s.hand.hasRolled && s.hand.orderedLockedIds.Count > 0, "红色浪潮需要先锁定至少 1 颗骰子。");
                    Check(!s.hand.redWave, "本手红色浪潮已经生效。"); Check(!s.hand.blueWave, "本手蓝色浪潮已经生效，不能再用红色浪潮。"); s.hand.redWave = true;
                    break;
                case "I02":
                    Check(s.phase == "HandReady", "临时骰子在本手第一次掷骰前才能用。");
                    Check(s.dice.Count < (s.isDebug ? cfg.debug.maxDice : cfg.maxActiveDice) && (s.isDebug || s.dice.Count(x => x.lifetime == "stage_temporary") < cfg.maxTemporaryDice), s.isDebug?$"骰子已到上限（总数最多 {cfg.debug.maxDice} 颗）。":$"骰子已到上限（临时骰最多 {cfg.maxTemporaryDice} 颗，总数最多 {cfg.maxActiveDice} 颗）。");
                    var d = DiceFactory.CreateStandard(cfg, s.nextDieSerial++, "stage_temporary", s.stage.stageId);
                    s.dice.Add(d); s.hand.currentResults.Add(new DieHandState { dieId = d.dieId });
                    s.lastEffects.Add(new EffectRecord { kind = "DieAdded", sourceId = "I02", dieId = d.dieId });
                    break;
                case "I03":
                    Check(command.dieIds != null && command.targetPips != null && command.dieIds.Length == 2 && command.targetPips.Length == 2
                        && command.dieIds.Distinct().Count() == 2, "请选 2 颗骰池里的骰子并各选一个点数。");
                    Check(command.targetPips.All(x => x >= cfg.minPipValue && x <= cfg.maxPipValue), "目标点数必须为1至6。");
                    Check(command.dieIds.All(id => s.dice.Any(x => x.dieId == id) && !s.hand.orderedLockedIds.Contains(id)), "请选 2 颗骰池里的骰子并各选一个点数。");
                    Roll(s, command.dieIds, command.targetPips);
                    break;
                case "I04":
                    Check(!s.hand.largeBuff, "本手大数多来已经生效。");
                    Check(s.hand.rollsRemaining > 0 && s.dice.Any(x => !s.hand.orderedLockedIds.Contains(x.dieId) && x.faces.Any(f => f.permanentPip == cfg.maxPipValue)), "需要骰池里有带 6 点面的骰子，且本手还能掷。"); s.hand.largeBuff = true;
                    break;
                case "I05":
                    Check(!s.hand.smallBuff, "本手小数多来已经生效。"); // v1.1: named the card C37 by mistake (text proposal §5 error 1)
                    Check(s.hand.rollsRemaining > 0 && s.dice.Any(x => !s.hand.orderedLockedIds.Contains(x.dieId) && x.faces.Any(f => f.permanentPip == cfg.minPipValue)), "需要骰池里有带 1 点面的骰子，且本手还能掷。"); s.hand.smallBuff = true;
                    break;
                case "I06": s.stage.remainingSubmissions = checked(s.stage.remainingSubmissions + 1); break;
                case "I07": s.stage.stageRollBonus = checked(s.stage.stageRollBonus + 1); s.hand.rollsRemaining = checked(s.hand.rollsRemaining + 1); break;
                // v1.0
                case "I08":
                    Check(s.phase == "HandReady" && s.stage.submissionOrdinal == 0 && s.hand.rollsUsed == 0 && ActiveModifierId(s) != null, "破咒符只能在首领关第一次掷骰前、减益仍生效时使用。");
                    NullifyModifier(s, "I08");
                    break;
                case "I09": Check(!s.hand.doubleScore, "本手灵光一闪已经生效。"); s.hand.doubleScore = true; break;
                case "I10":
                    Check(s.phase == "Selecting" && s.hand.hasRolled && s.hand.orderedLockedIds.Count > 0, "蓝色浪潮需要先锁定至少 1 颗骰子。");
                    Check(!s.hand.blueWave, "本手蓝色浪潮已经生效。"); Check(!s.hand.redWave, "本手红色浪潮已经生效，不能再用蓝色浪潮。"); s.hand.blueWave = true;
                    break;
                case "I11":
                    {
                        Check(s.phase == "Selecting" && s.hand.hasRolled && s.hand.orderedLockedIds.Count > 0, "点金笔需要先锁定至少 1 颗骰子。");
                        var die = FindDie(s, s.hand.orderedLockedIds[0]); var result = s.hand.currentResults.First(x => x.dieId == die.dieId);
                        var face = die.faces.First(f => f.faceId == result.currentFaceId); Check(!face.isGold, "第 1 颗锁定骰子朝上的面已经是黄金面。");
                        GildFace(s, die, face, "I11"); // v1.1.1 section 7.2: the shared gilding entry, weight +1 up to the cap
                        break;
                    }
                case "I12":
                    Check(s.stage.capacity < (s.isDebug ? cfg.debug.maxSubmitCapacity : cfg.maxSubmitCapacity), $"提交栏已经是最多的 {(s.isDebug?cfg.debug.maxSubmitCapacity:cfg.maxSubmitCapacity)} 格。");
                    s.stage.capacity++; s.stage.itemCapacityBonus++;
                    break;
                default: throw new RuleViolation("未知道具。");
            }
            SpendItemUse(s, item);
        }
    }
}
