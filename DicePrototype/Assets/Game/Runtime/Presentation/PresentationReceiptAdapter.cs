using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using DiceCraft.Core;

namespace DiceCraft.Presentation
{
    /// <summary>
    /// Adapts committed p0.6.0 receipts. It never calls Evaluate, Execute, or any RNG.
    /// Attribution supplements absent from the legacy trace are documented in README.md.
    /// </summary>
    public static class PresentationReceiptAdapter
    {
        public static PresentationReceipt BuildCommitted(CommandResult result, RunState before, RunState after, GameConfig config)
        {
            if (result == null || !result.accepted || result.pendingSave || result.noChange || result.duplicate || result.receipt == null)
                throw new ArgumentException("Only a new successfully saved command may start a presentation.");
            return Build(result.receipt, before, after, config);
        }

        public static PresentationReceipt Build(CommandReceipt receipt, RunState before, RunState after, GameConfig config)
        {
            if (receipt == null || after == null || config == null) throw new ArgumentNullException();
            if (string.IsNullOrEmpty(receipt.commandId)) throw new ArgumentException("Missing committed receipt ID.");
            var builder = new Builder(receipt, before, after, config);
            return builder.Build();
        }

        /// <summary>Safe text-only final-state fallback if a historical receipt cannot be fully explained.</summary>
        public static PresentationReceipt Fallback(CommandReceipt receipt, RunState before, RunState after, string reason)
        {
            if (receipt == null || after == null) throw new ArgumentNullException();
            string score = receipt.submission?.breakdown?.score ?? receipt.score ?? "0";
            var events = new[] { new PresentationEvent(receipt.commandId + "/final", 0, PresentationPhase.FinalScore, "final_score", after: score, detail: reason) };
            return new PresentationReceipt(receipt.commandId, after.runId, receipt.submission?.snapshot?.handId ?? before?.hand?.handId,
                score, new ExactFraction(1, 1), before?.stage?.score ?? "0", after.stage?.score ?? "0", after.stage?.target ?? "0",
                before?.stage?.qualified ?? false, after.stage?.qualified ?? false, events, receipt.submission != null, after.phase, reason);
        }

        private sealed class Builder
        {
            private readonly CommandReceipt receipt;
            private readonly RunState before, after;
            private readonly GameConfig cfg;
            private readonly List<PresentationEvent> events = new List<PresentationEvent>();
            private SubmissionSnapshot snap;
            private ScoreBreakdown score;
            private AbilitySource[] sources = Array.Empty<AbilitySource>();
            private readonly Dictionary<string, FaceState[]> permanent = new Dictionary<string, FaceState[]>(StringComparer.Ordinal);
            public Builder(CommandReceipt receipt, RunState before, RunState after, GameConfig cfg)
            { this.receipt = receipt; this.before = before; this.after = after; this.cfg = cfg; }
            public PresentationReceipt Build()
            {
                Add(PresentationPhase.Receipt, "committed", afterValue: receipt.phase);
                var multiplier = new ExactFraction(1, 1);
                if (receipt.submission != null)
                {
                    snap = receipt.submission.snapshot; score = receipt.submission.breakdown;
                    if (snap == null || score == null) throw new ArgumentException("Incomplete submission receipt.");
                    sources = ControllerRules.ResolveSources(ControllerRules.Resolve(snap), snap.mimicBindings);
                    multiplier = new ExactFraction((BigInteger)score.totalHandMult100 * score.colorMult100 * score.redMult100 * ExactScoreMath.Parse(score.controllerMultiplierNumerator) * BigInteger.Pow(cfg.enchantRules.scoreMultiplier, score.enchantMultiplierCount),
                        1000000 * ExactScoreMath.Parse(score.controllerMultiplierDenominator));
                    BuildScoring();
                    Add(PresentationPhase.FinalScore, "final_score", beforeValue: "0", afterValue: score.score,
                        detail: score.score == "0" && score.multiplierFactors.Any(f => f.numerator == "0") ? "千年老二 ×0：没有有效5点" : null,
                        audio: ScorePresentationTier.Resolve(score.score, ExactScoreMath.SaveDecimal(multiplier.Numerator), ExactScoreMath.SaveDecimal(multiplier.Denominator)) >= 3 ? "SFX_SCORE_BURST" : "SFX_SCORE_STEP");
                }
                else if (before != null)
                    sources = ControllerRules.ResolveSources(before.controllers, before.stage?.mimicBindings);
                BuildPostEffects();
                if (!(before?.stage?.qualified ?? false) && (after.stage?.qualified ?? false))
                    Add(PresentationPhase.TargetState, "qualified", beforeValue: "false", afterValue: "true", audio: "SFX_QUALIFIED");
                // A Run result is shown only once the actual Continue command has entered that result phase.
                if ((after.phase == "RunWon" || after.phase == "RunFailed") && before?.phase != after.phase)
                    Add(PresentationPhase.TargetState, "run_result", beforeValue: before?.phase, afterValue: after.phase, audio: "SFX_RUN_END");
                return new PresentationReceipt(receipt.commandId, after.runId, snap?.handId ?? before?.hand?.handId,
                    score?.score ?? receipt.score ?? "0", multiplier, before?.stage?.score ?? "0", after.stage?.score ?? "0", after.stage?.target ?? "0",
                    before?.stage?.qualified ?? false, after.stage?.qualified ?? false, events, snap != null, after.phase);
            }

            private void BuildScoring()
            {
                // Explicit frozen value/group ownership; never infer trigger from score increase.
                foreach (var source in sources.Where(Hit))
                {
                    IEnumerable<int> indices = Array.Empty<int>(); string family = null;
                    switch (source.abilityId)
                    {
                        case "C12": indices = Enumerable.Range(0, snap.dice.Length).Where(i => snap.dice[i].currentPip != score.effectivePips[i]); family = "effective_pip"; break;
                        case "C03": if (!snap.redWave && snap.dice[0].currentColor != score.effectiveColors[0]) indices = new[] { 0 }; family = "effective_color"; break;
                        case "C24":
                            if (score.redGroupingMode == "one_as_four") indices = Enumerable.Range(0, snap.dice.Length).Where(i => score.effectiveColors[i] == "red" && score.effectivePips[i] == cfg.Controller("C24").parameters.redGroupKeyFrom);
                            family = "red_group_key"; break;
                        case "C25":
                            if (score.redGroupingMode == "all_red" && Enumerable.Range(0, snap.dice.Length).Where(i => score.effectiveColors[i] == "red").Select(i => score.effectivePips[i]).Distinct().Count() > 1)
                                indices = Enumerable.Range(0, snap.dice.Length).Where(i => score.effectiveColors[i] == "red");
                            family = "red_group_merge"; break;
                        case "C29":
                            if (score.straightMult100 > 0 && !HandEvaluator.HasStraight(score.effectivePips, cfg.minStraightLength)) indices = Enumerable.Range(0, snap.dice.Length);
                            family = "short_straight"; break;
                    }
                    var targets = indices.ToArray();
                    if (family != null && targets.Length > 0)
                        Add(PresentationPhase.DiceContributions, family, source, targets.Select(i => snap.dice[i].dieId), targets.Select(i => snap.dice[i].faceId),
                            beforeValue: string.Join(",", targets.Select(i => family == "effective_color" ? snap.dice[i].currentColor : snap.dice[i].currentPip.ToString(CultureInfo.InvariantCulture))),
                            afterValue: family == "short_straight" ? "straight" : family == "red_group_merge" ? "all_red" : family == "red_group_key" ? "one_as_four" : string.Join(",", targets.Select(i => family == "effective_color" ? score.effectiveColors[i] : score.effectivePips[i].ToString(CultureInfo.InvariantCulture))), chain: true, scoring: true);
                }
                var trace = ScoreExplanation.Build(snap, score, cfg);
                for (int i = 0; i < trace.dice.Length; i++)
                {
                    var die = snap.dice[i]; var dt = trace.dice[i];
                    Add(PresentationPhase.DiceContributions, "die_pips", dice: new[] { die.dieId }, faces: new[] { die.faceId }, afterValue: score.effectivePips[i].ToString(CultureInfo.InvariantCulture), scoring: true);
                    foreach (var step in dt.steps)
                    {
                        var source = sources.FirstOrDefault(s => s.emitterId == step.emitterId);
                        Add(PresentationPhase.DiceContributions, step.kind, source, new[] { die.dieId }, new[] { die.faceId }, step.before, step.operand, step.after, step.operation,
                            chain: source != null && step.before != step.after, scoring: true);
                    }
                }
                foreach (var step in trace.global)
                {
                    if (step.kind == "floor") continue; // Only the receipt's final floor is displayed.
                    if (step.kind == "controller_base" && step.emitterId == null)
                    {
                        var owners = sources.Where(s => s.abilityId == step.sourceId && Hit(s)).ToArray();
                        if (owners.Length == 0) throw new ArgumentException("Base contribution has no frozen source.");
                        var total = ExactFraction.Parse(step.operand); var each = new ExactFraction(total.Numerator, total.Denominator * owners.Length);
                        var current = ExactFraction.Parse(step.before);
                        foreach (var source in owners)
                        {
                            var next = current + each;
                            Add(PresentationPhase.BaseAndStructure, step.kind, source, beforeValue: current.ToString(), operand: each.ToString(), afterValue: next.ToString(), operation: "+", chain: each.Numerator != 0, scoring: true);
                            current = next;
                        }
                        if (current.ToString() != step.after) throw new ArgumentException("Base contribution attribution does not sum to receipt.");
                        continue;
                    }
                    var emitter = sources.FirstOrDefault(s => s.emitterId == step.emitterId);
                    var phase = step.kind == "controller" || step.kind == "enchantment_multiplier" ? PresentationPhase.ControllerAndEnchant : PresentationPhase.BaseAndStructure;
                    bool meaningful = step.operation == "+" ? ExactFraction.Parse(step.operand).Numerator != 0 : step.operation == "×" && step.operand != "1";
                    Add(phase, step.kind, emitter, beforeValue: step.before, operand: step.operand, afterValue: step.after, operation: step.operation,
                        chain: emitter != null && meaningful, scoring: true, audio: phase == PresentationPhase.ControllerAndEnchant ? "SFX_CARD_TRIGGER" : "SFX_SCORE_STEP");
                }
                foreach (var choice in snap.resolvedRandomChoices ?? Array.Empty<RandomMultiplierChoice>())
                {
                    var owner = sources.FirstOrDefault(s => s.emitterId == choice.ownerInstanceId);
                    var linked = sources.Where(s => s.abilityId == "C53" && s.stateOwnerId == choice.ownerInstanceId).Select(s => s.emitterId).ToArray();
                    var random = receipt.randomResults.FirstOrDefault(r => r.kind == "C53" && r.sourceId == choice.ownerInstanceId);
                    Add(PresentationPhase.ControllerAndEnchant, "random_reveal", owner, operand: new ExactFraction(choice.units2, 2).ToString(), afterValue: new ExactFraction(choice.units2, 2).ToString(),
                        detail: random?.overridden == true ? "调试指定分支" : "已提交随机结果", sourceEmitters: linked);
                }
            }

            private bool Hit(AbilitySource source) => score != null && score.triggeredSourceIds.Contains(source.emitterId);
            private void BuildPostEffects()
            {
                foreach (var d in before?.dice ?? new List<DieInstance>()) permanent[d.dieId] = d.faces.Select(f => f.Clone()).ToArray();
                if (snap != null) foreach (var d in snap.dice.Where(d => d.permanentFacesCopy != null)) permanent[d.dieId] = d.permanentFacesCopy.Select(f => f.Clone()).ToArray();
                foreach (var effect in receipt.effects ?? new List<EffectRecord>())
                {
                    var abilities = (effect.sourceId ?? "").Split('+');
                    var owners = sources.Where(s => abilities.Contains(s.abilityId) && (score == null || Hit(s))).ToArray();
                    string explicitEmitter = effect.kind == "ControllerDestroyed" || effect.kind == "FundAccrued" ? effect.detail : null;
                    if (explicitEmitter != null) owners = sources.Where(s => s.emitterId == explicitEmitter).ToArray();
                    string ownerId = null, old = effect.oldValue.ToString(CultureInfo.InvariantCulture), value = effect.newValue.ToString(CultureInfo.InvariantCulture), family = effect.kind;
                    string audio = null; string[] dice = effect.dieId == null ? Array.Empty<string>() : new[] { effect.dieId };
                    string[] faces = effect.faceId == null ? Array.Empty<string>() : new[] { effect.faceId };
                    switch (effect.kind)
                    {
                        case "DieCopied":
                            old = FaceSummary(effect.dieId);
                            if (effect.otherDieId != null && permanent.TryGetValue(effect.otherDieId, out var copied)) permanent[effect.dieId] = copied.Select(f => f.Clone()).ToArray();
                            value = FaceSummary(effect.dieId); audio = "SFX_DYE_COPY"; break;
                        case "DieDyed":
                            old = FaceSummary(effect.dieId); if (permanent.TryGetValue(effect.dieId, out var dyed)) foreach (var face in dyed) face.baseColor = effect.detail;
                            value = FaceSummary(effect.dieId); audio = old == value ? null : "SFX_DYE_COPY"; break;
                        case "FaceDyed":
                            var dyeFace = Face(effect.dieId, effect.faceId); old = dyeFace?.baseColor; value = effect.detail;
                            if (dyeFace != null) dyeFace.baseColor = value; audio = old == value ? null : "SFX_DYE_COPY"; break;
                        case "FaceValueChanged":
                            var pipFace = Face(effect.dieId, effect.faceId); if (pipFace != null) pipFace.permanentPip = checked((int)effect.newValue);
                            audio = effect.newValue > effect.oldValue ? "SFX_GROW" : null; break;
                        case "HandGrown": audio = effect.newValue > effect.oldValue ? "SFX_GROW" : null; break;
                        case "SubmissionIncome": audio = effect.newValue > 0 ? "SFX_COIN" : null; break;
                        case "FundAccrued":
                            old = before?.controllers.FirstOrDefault(c => c.instanceId == effect.detail)?.state.fundAccrual.ToString(CultureInfo.InvariantCulture) ?? "0";
                            audio = null; break; // A changed sale quote is never wallet income.
                        case "ActiveColorChanged":
                            int split = (effect.detail ?? "").LastIndexOf(':');
                            if (split >= 0) { ownerId = effect.detail.Substring(0, split); value = effect.detail.Substring(split + 1); }
                            old = before?.controllers.FirstOrDefault(c => c.instanceId == ownerId)?.state.activeColor ?? snap?.equippedControllers.FirstOrDefault(c => c.instanceId == ownerId)?.state.activeColor;
                            owners = sources.Where(s => s.abilityId == "C45" && s.stateOwnerId == ownerId).ToArray(); break;
                        case "ControllerDestroyed": case "DieBroken": audio = "SFX_BREAK"; break;
                        case "TemporaryDieExpired": audio = null; break;
                    }
                    // Keep a source group where p0.6 recorded only an ability ID. Do not invent a last writer.
                    var single = owners.Length == 1 ? owners[0] : null;
                    Add(PresentationPhase.PostScore, family, single, dice, faces, old, null, value,
                        detail: effect.detail, audio: audio, sourceEmitters: owners.Select(s => s.emitterId), ownerOverride: ownerId);
                }
                if (receipt.settlement != null)
                    Add(PresentationPhase.PostScore, "stage_income", beforeValue: receipt.settlement.goldBefore.ToString(CultureInfo.InvariantCulture),
                        operand: (receipt.settlement.goldAfter - receipt.settlement.goldBefore).ToString(CultureInfo.InvariantCulture), afterValue: receipt.settlement.goldAfter.ToString(CultureInfo.InvariantCulture), operation: "+", audio: "SFX_COIN");
            }
            private FaceState Face(string die, string face) => die != null && permanent.TryGetValue(die, out var list) ? list.FirstOrDefault(f => f.faceId == face) : null;
            private string FaceSummary(string die) => die != null && permanent.TryGetValue(die, out var list) ? string.Join(";", list.Select(f => f.faceId + ":" + f.permanentPip + ":" + f.baseColor)) : null;
            private void Add(PresentationPhase phase, string family, AbilitySource source = null, IEnumerable<string> dice = null, IEnumerable<string> faces = null,
                string beforeValue = null, string operand = null, string afterValue = null, string operation = null, string detail = null, bool chain = false, bool scoring = false,
                string audio = null, IEnumerable<string> sourceEmitters = null, string ownerOverride = null)
            {
                int ordinal = events.Count;
                events.Add(new PresentationEvent(receipt.commandId + "/" + ordinal.ToString("D4", CultureInfo.InvariantCulture), ordinal, phase, family,
                    source?.emitterId, ownerOverride ?? source?.stateOwnerId, source?.abilityId, sourceEmitters, dice, faces, beforeValue, operand, afterValue, operation, detail, chain, scoring, audio));
            }
        }
    }
}
