using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace DiceCraft.Core
{
    [Serializable]
    public sealed class ScoreStep
    {
        public string kind, sourceId, emitterId, stateOwnerId, operation;
        // Reduced nonnegative rational strings, never rounded display doubles.
        public string before, operand, after;
        // v0.9: who changed this step without owning it, as "C84:150,C85:300" (hand multiplier adds, units of 0.01)
        // or "C73:2" (pool dice merged into the red group). Display only.
        public string note;
        // v1.1.1: only on the "additive" step: the bonuses that add up to A, in order, so the line can be expanded.
        public AdditiveEntry[] entries;
    }
    [Serializable] public sealed class DieScoreTrace { public string dieId; public long contribution; public ScoreStep[] steps; /* v0.9: contribution x (1 + retriggers) */ public int retriggers; public string total; }
    [Serializable] public sealed class ScoreTrace { public DieScoreTrace[] dice; public ScoreStep[] global; }
    public static class ScoreExplanation
    {
        internal static DieScoreTrace DescribeDie(SubmissionSnapshot snapshot, int index, int[] pips, string color, GameConfig cfg, AbilitySource[] sources, bool pipsOnly = false, bool sealedDie = false)
        {
            int pip = pips[index]; var die = snapshot.dice[index]; var steps = new List<ScoreStep>(); var value = new ExactFraction(pip, 1);
            // v1.0 boss modifiers: PipsOnly keeps the bare effective pip; a sealed die contributes nothing.
            if (pipsOnly || sealedDie)
            {
                if (sealedDie) Step(steps, "sealed", new AbilitySource { abilityId = snapshot.modifierId }, "×", ref value, new ExactFraction(0, 1));
                return new DieScoreTrace { dieId = die.dieId, contribution = checked((long)value.Numerator), steps = steps.ToArray() };
            }
            foreach (var source in sources)
            {
                if (source.abilityId == null) continue; var p = cfg.Controller(source.abilityId).parameters; int add = 0;
                switch (source.abilityId)
                {
                    case "C10": if (pip == p.pip) add = p.perDieAdd; break;
                    case "C13": if (pip % 2 == 1) add = p.oddPerDieAdd; break;
                    case "C17": if (p.pips.Contains(pip)) add = p.perDieAdd; break;
                    case "C20": if (snapshot.dice.Length == snapshot.capacity && index == snapshot.capacity - 1 && pip == p.lastActualCapacitySlotPip) add = p.perDieAdd; break;
                    case "C39": if (color == p.effectiveColor) add = p.perDieAdd; break;
                    case "C92": if (DieFacts.MonoColor(die)) add = p.perDieAdd; break;
                    case "C94": add = DieFacts.OppositePip(die, cfg); break;
                    case "C107": if (index > 0 && pip == pips[index - 1] + 1) add = p.perDieAdd; break;
                }
                if (add != 0) Step(steps, "contribution", source, "+", ref value, new ExactFraction(add, 1));
            }
            foreach (var source in sources.Where(s => s.abilityId == "C45" && s.state.activeColor == color)) Step(steps, "controller_die", source, "×", ref value, new ExactFraction(cfg.Controller("C45").parameters.matchingDieContributionMultiplier, 1));
            if (DieFacts.MonoPip(die)) foreach (var source in sources.Where(s => s.abilityId == "C93")) Step(steps, "controller_die", source, "×", ref value, new ExactFraction(cfg.Controller("C93").parameters.dieContributionMultiplier, 1));
            if (die.isGlass) Step(steps, "glass", null, "×", ref value, new ExactFraction(cfg.glassContributionMultiplier, 1));
            // v1.1.1: a gold face no longer doubles the die; it adds to the additive zone (see Build).
            return new DieScoreTrace { dieId = die.dieId, contribution = checked((long)value.Numerator), steps = steps.ToArray() };
        }
        public static ScoreTrace Build(SubmissionSnapshot snapshot, ScoreBreakdown breakdown, GameConfig cfg)
        {
            if (snapshot == null || breakdown == null || cfg == null) throw new ArgumentNullException();
            var sources = ControllerRules.ResolveSources(ControllerRules.Resolve(snapshot), snapshot.mimicBindings, snapshot.disabledControllerIds);
            bool pipsOnly = ControllerRules.ModifierHas(cfg, snapshot.modifierId, "PipsOnly"); var sealedDice = breakdown.sealedDice ?? Array.Empty<bool>();
            if (sealedDice.Length != 0 && sealedDice.Length != snapshot.dice.Length) throw new ArgumentException("Frozen seal mismatch.");
            var dice = snapshot.dice.Select((d, i) => DescribeDie(snapshot, i, breakdown.effectivePips, breakdown.effectiveColors[i], cfg, sources, pipsOnly, sealedDice.Length != 0 && sealedDice[i])).ToArray();
            if (dice.Where((d, i) => d.contribution != breakdown.pipContributions[i]).Any()) throw new ArgumentException("Frozen contribution mismatch.");
            // v0.9: each retrigger source appends one "+ contribution x count" step to the die it repeats.
            var retriggers = breakdown.pipRetriggers ?? Array.Empty<int>(); var entries = breakdown.retriggerEntries ?? Array.Empty<RetriggerEntry>();
            if (retriggers.Length != 0 && retriggers.Length != dice.Length) throw new ArgumentException("Frozen retrigger mismatch.");
            for (int i = 0; i < dice.Length; i++)
            {
                var value0 = new ExactFraction(dice[i].contribution, 1); var extra = new List<ScoreStep>(dice[i].steps); int count = 0;
                foreach (var entry in entries.Where(e => e.dieIndex == i))
                {
                    count = checked(count + entry.count);
                    Step(extra, "retrigger", new AbilitySource { abilityId = entry.abilityId, emitterId = entry.emitterId, stateOwnerId = entry.stateOwnerId }, "+", ref value0, new ExactFraction((BigInteger)dice[i].contribution * entry.count, 1));
                }
                if (count != (retriggers.Length == 0 ? 0 : retriggers[i])) throw new ArgumentException("Frozen retrigger attribution mismatch.");
                dice[i].steps = extra.ToArray(); dice[i].retriggers = count; dice[i].total = value0.ToString();
            }
            BigInteger sum = dice.Aggregate(BigInteger.Zero, (a, d) => a + ExactScoreMath.Parse(d.total));
            if (sum != ExactScoreMath.Parse(breakdown.pipSum)) throw new ArgumentException("Frozen sum mismatch.");
            var steps = new List<ScoreStep>(); var value = new ExactFraction(sum, 1);
            Step(steps, "unused_rolls", null, "+", ref value, new ExactFraction(breakdown.unusedRollBonus, 1));
            Add(steps, "C27", breakdown.pairBaseAdd, ref value); Add(steps, "C44", ExactScoreMath.Parse(breakdown.walletBaseAdd), ref value); Add(steps, "C48", breakdown.emptySlotBaseAdd, ref value); Add(steps, "C50", breakdown.steadyBaseAdd, ref value);
            foreach (var add in breakdown.controllerBaseAdds ?? Array.Empty<ControllerBaseAdd>()) Step(steps, "controller_base", sources.Single(s => s.emitterId == add.emitterId), "+", ref value, new ExactFraction(ExactScoreMath.Parse(add.amount), 1));
            bool silence = ControllerRules.ModifierHas(cfg, snapshot.modifierId, "EnchantSilence");
            if (!silence && !pipsOnly) foreach (var source in sources.Where(s => s.enchantmentId == "base10" && breakdown.triggeredSourceIds.Contains(s.emitterId))) Step(steps, "enchantment_base", source, "+", ref value, new ExactFraction(cfg.enchantRules.baseAdd, 1));
            if (value.Numerator != ExactScoreMath.Parse(breakdown.baseTotal)) throw new ArgumentException("Frozen base mismatch.");
            // v1.0: the hand step shows the multiplier before NoRepeatHand / HandHalf; the boss step carries the rest.
            int handBefore = breakdown.handMultBeforeModifier100 > 0 ? breakdown.handMultBeforeModifier100 : breakdown.totalHandMult100;
            Step(steps, "hand", null, "×", ref value, new ExactFraction(handBefore, 100));
            steps[steps.Count - 1].note = string.Join(",", sources.Where(s => (s.abilityId == "C84" || s.abilityId == "C85" || s.abilityId == "C142") && breakdown.triggeredSourceIds.Contains(s.emitterId)).Select(s => s.abilityId + ":" + cfg.Controller(s.abilityId).parameters.handMultAdd100));
            if (handBefore != breakdown.totalHandMult100) Step(steps, "boss_hand", new AbilitySource { abilityId = snapshot.modifierId }, "×", ref value, new ExactFraction(breakdown.totalHandMult100, handBefore));
            Step(steps, "red_group", null, "×", ref value, new ExactFraction(breakdown.redMult100, 100));
            steps[steps.Count - 1].note = string.Join(",", new[] { breakdown.redPoolCount > 0 ? "C73:" + breakdown.redPoolCount : null, breakdown.redBonusCount > 0 ? "C146:" + breakdown.redBonusCount : null }.Where(x => x != null));
            if (steps[steps.Count - 1].note == "") steps[steps.Count - 1].note = null;
            // v1.1.1: one "additive" line x(1 + A), shown even when A is 0, expandable into its entries.
            var additiveEntries = CheckedAdditiveEntries(snapshot, breakdown, cfg, sources, silence);
            Step(steps, "additive", null, "×", ref value, new ExactFraction(100 + (BigInteger)breakdown.additiveTotal100, 100));
            steps[steps.Count - 1].entries = additiveEntries;
            foreach (var factor in breakdown.multiplierFactors) Step(steps, "controller", sources.Single(s => s.emitterId == factor.emitterId), "×", ref value, new ExactFraction(ExactScoreMath.Parse(factor.numerator), ExactScoreMath.Parse(factor.denominator)));
            foreach (var factor in breakdown.globalFactors ?? Array.Empty<MultiplierFactor>()) Step(steps, factor.emitterId == "ITEM" ? "item" : "boss", new AbilitySource { abilityId = factor.abilityId }, "×", ref value, new ExactFraction(ExactScoreMath.Parse(factor.numerator), ExactScoreMath.Parse(factor.denominator)));
            string floor = ExactScoreMath.SaveDecimal(value.Numerator / value.Denominator);
            steps.Add(new ScoreStep { kind = "floor", operation = "floor", before = value.ToString(), operand = "1", after = floor });
            if (floor != breakdown.score) throw new ArgumentException("Frozen score mismatch.");
            return new ScoreTrace { dice = dice, global = steps.ToArray() };
        }
        /// <summary>The additive entries of a frozen breakdown after checking them against the snapshot: the order of
        /// the four kinds, the colour bonus, one entry per submitted gold face, card entries only from triggered
        /// sources in card order, one score-enchantment entry per triggered enchanted source, and the total.</summary>
        private static AdditiveEntry[] CheckedAdditiveEntries(SubmissionSnapshot snapshot, ScoreBreakdown breakdown, GameConfig cfg, AbilitySource[] sources, bool silence)
        {
            var entries = (breakdown.additiveEntries ?? Array.Empty<AdditiveEntry>()).Select(e => e?.Clone()).ToArray();
            string[] kinds = { "color", "gold", "controller", "enchantment" };
            bool ok = entries.All(e => e != null && e.amount100 > 0 && Array.IndexOf(kinds, e.kind) >= 0)
                && entries.Select(e => Array.IndexOf(kinds, e.kind)).SequenceEqual(entries.Select(e => Array.IndexOf(kinds, e.kind)).OrderBy(k => k))
                && entries.Aggregate(BigInteger.Zero, (sum, e) => sum + e.amount100) == breakdown.additiveTotal100;
            if (ok)
            {
                var color = entries.Where(e => e.kind == "color").ToArray(); var gold = entries.Where(e => e.kind == "gold").ToArray();
                var cards = entries.Where(e => e.kind == "controller").ToArray(); var enchants = entries.Where(e => e.kind == "enchantment").ToArray();
                var order = sources.Select((s, i) => new { s.emitterId, i }).ToDictionary(x => x.emitterId, x => x.i, StringComparer.Ordinal);
                var triggered = new HashSet<string>(breakdown.triggeredSourceIds ?? Array.Empty<string>(), StringComparer.Ordinal);
                ok = (breakdown.colorAdd100 > 0 ? color.Length == 1 && color[0].amount100 == breakdown.colorAdd100 && color[0].abilityId == breakdown.colorId && color[0].emitterId == null : color.Length == 0)
                    && gold.Select(e => e.dieIndex).SequenceEqual(Enumerable.Range(0, snapshot.dice.Length).Where(i => snapshot.dice[i].isGold)) && gold.All(e => e.amount100 == cfg.goldAdd100 && e.emitterId == null)
                    && cards.All(e => e.emitterId != null && triggered.Contains(e.emitterId) && order.ContainsKey(e.emitterId) && sources[order[e.emitterId]].abilityId == e.abilityId)
                    && cards.Select(e => order[e.emitterId]).SequenceEqual(cards.Select(e => order[e.emitterId]).OrderBy(i => i)) && cards.Select(e => e.emitterId).Distinct(StringComparer.Ordinal).Count() == cards.Length
                    && enchants.Select(e => e.emitterId).SequenceEqual(silence ? Enumerable.Empty<string>() : sources.Where(s => s.enchantmentId == ControllerRules.ScoreEnchantmentId && triggered.Contains(s.emitterId)).Select(s => s.emitterId))
                    && enchants.All(e => e.amount100 == cfg.enchantRules.scoreAdd100 && e.abilityId == ControllerRules.ScoreEnchantmentId) && enchants.Length == breakdown.enchantAdditiveCount;
            }
            if (!ok) throw new ArgumentException("Frozen additive mismatch.");
            return entries;
        }
        private static void Add(List<ScoreStep> steps, string id, BigInteger amount, ref ExactFraction value)
        {
            if (amount != 0) Step(steps, "controller_base", new AbilitySource { abilityId = id }, "+", ref value, new ExactFraction(amount, 1));
        }
        private static void Step(List<ScoreStep> steps, string kind, AbilitySource source, string operation, ref ExactFraction value, ExactFraction operand)
        {
            string before = value.ToString(); value = operation == "+" ? value + operand : value * operand;
            steps.Add(new ScoreStep { kind = kind, sourceId = source?.abilityId, emitterId = source?.emitterId, stateOwnerId = source?.stateOwnerId, operation = operation, before = before, operand = operand.ToString(), after = value.ToString() });
        }
    }
    public readonly struct ExactFraction
    {
        public readonly BigInteger Numerator, Denominator;
        public ExactFraction(BigInteger numerator, BigInteger denominator)
        {
            if (numerator < 0 || denominator <= 0) throw new ArgumentOutOfRangeException(); var gcd = BigInteger.GreatestCommonDivisor(numerator, denominator); Numerator = numerator / gcd; Denominator = denominator / gcd;
        }
        public static ExactFraction Parse(string value)
        {
            var parts = value.Split('/'); if (parts.Length < 1 || parts.Length > 2) throw new ArgumentException("Invalid fraction."); return new ExactFraction(ExactScoreMath.Parse(parts[0]), parts.Length == 2 ? ExactScoreMath.Parse(parts[1]) : BigInteger.One);
        }
        public override string ToString() => ExactScoreMath.SaveDecimal(Numerator) + (Denominator == 1 ? "" : "/" + ExactScoreMath.SaveDecimal(Denominator));
        public static ExactFraction operator +(ExactFraction a, ExactFraction b) => new ExactFraction(a.Numerator * b.Denominator + b.Numerator * a.Denominator, a.Denominator * b.Denominator);
        public static ExactFraction operator *(ExactFraction a, ExactFraction b) => new ExactFraction(a.Numerator * b.Numerator, a.Denominator * b.Denominator);
    }
}
