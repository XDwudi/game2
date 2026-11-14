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
    }
    [Serializable] public sealed class DieScoreTrace { public string dieId; public long contribution; public ScoreStep[] steps; /* v0.9: contribution x (1 + retriggers) */ public int retriggers; public string total; }
    [Serializable] public sealed class ScoreTrace { public DieScoreTrace[] dice; public ScoreStep[] global; }
    public static class ScoreExplanation
    {
        internal static DieScoreTrace DescribeDie(SubmissionSnapshot snapshot, int index, int pip, string color, GameConfig cfg, AbilitySource[] sources)
        {
            var die = snapshot.dice[index]; var steps = new List<ScoreStep>(); var value = new ExactFraction(pip, 1);
            foreach (var source in sources)
            {
                if (source.abilityId == null) continue; var p = cfg.Controller(source.abilityId).parameters; int add = 0;
                switch (source.abilityId)
                {
                    case "C10": if (pip == p.pip) add = p.perDieAdd; break;
                    case "C13": if (pip % 2 == 1) add = p.oddPerDieAdd; break;
                    case "C17": if (p.pips.Contains(pip)) add = p.perDieAdd; break;
                    case "C19": if (die.permanentSixFaceSum <= p.maxPermanentSixFaceSum) add = p.perDieAdd; break;
                    case "C20": if (snapshot.dice.Length == snapshot.capacity && index == snapshot.capacity - 1 && pip == p.lastActualCapacitySlotPip) add = p.perDieAdd; break;
                    case "C39": if (color == p.effectiveColor) add = p.perDieAdd; break;
                    case "C91": if (p.textures.Contains(DieFacts.Texture(die))) add = p.perDieAdd; break;
                    case "C92": if (DieFacts.MonoColor(die)) add = p.perDieAdd; break;
                    case "C94": add = DieFacts.OppositePip(die, cfg); break;
                }
                if (add != 0) Step(steps, "contribution", source, "+", ref value, new ExactFraction(add, 1));
            }
            foreach (var source in sources.Where(s => s.abilityId == "C45" && s.state.activeColor == color)) Step(steps, "controller_die", source, "×", ref value, new ExactFraction(cfg.Controller("C45").parameters.matchingDieContributionMultiplier, 1));
            if (DieFacts.MonoPip(die)) foreach (var source in sources.Where(s => s.abilityId == "C93")) Step(steps, "controller_die", source, "×", ref value, new ExactFraction(cfg.Controller("C93").parameters.dieContributionMultiplier, 1));
            if (die.isGlass) Step(steps, "glass", null, "×", ref value, new ExactFraction(cfg.glassContributionMultiplier, 1));
            if (die.isGold) Step(steps, "gold", null, "×", ref value, new ExactFraction(cfg.goldContributionMultiplier, 1));
            return new DieScoreTrace { dieId = die.dieId, contribution = checked((long)value.Numerator), steps = steps.ToArray() };
        }
        public static ScoreTrace Build(SubmissionSnapshot snapshot, ScoreBreakdown breakdown, GameConfig cfg)
        {
            if (snapshot == null || breakdown == null || cfg == null) throw new ArgumentNullException();
            var sources = ControllerRules.ResolveSources(ControllerRules.Resolve(snapshot), snapshot.mimicBindings);
            var dice = snapshot.dice.Select((d, i) => DescribeDie(snapshot, i, breakdown.effectivePips[i], breakdown.effectiveColors[i], cfg, sources)).ToArray();
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
            foreach (var source in sources.Where(s => s.enchantmentId == "base10" && breakdown.triggeredSourceIds.Contains(s.emitterId))) Step(steps, "enchantment_base", source, "+", ref value, new ExactFraction(cfg.enchantRules.baseAdd, 1));
            if (value.Numerator != ExactScoreMath.Parse(breakdown.baseTotal)) throw new ArgumentException("Frozen base mismatch.");
            Step(steps, "hand", null, "×", ref value, new ExactFraction(breakdown.totalHandMult100, 100));
            Step(steps, "color", null, "×", ref value, new ExactFraction(breakdown.colorMult100, 100));
            Step(steps, "red_group", null, "×", ref value, new ExactFraction(breakdown.redMult100, 100));
            foreach (var factor in breakdown.multiplierFactors) Step(steps, "controller", sources.Single(s => s.emitterId == factor.emitterId), "×", ref value, new ExactFraction(ExactScoreMath.Parse(factor.numerator), ExactScoreMath.Parse(factor.denominator)));
            foreach (var source in sources.Where(s => s.enchantmentId == "mult2" && breakdown.triggeredSourceIds.Contains(s.emitterId))) Step(steps, "enchantment_multiplier", source, "×", ref value, new ExactFraction(cfg.enchantRules.scoreMultiplier, 1));
            string floor = ExactScoreMath.SaveDecimal(value.Numerator / value.Denominator);
            steps.Add(new ScoreStep { kind = "floor", operation = "floor", before = value.ToString(), operand = "1", after = floor });
            if (floor != breakdown.score) throw new ArgumentException("Frozen score mismatch.");
            return new ScoreTrace { dice = dice, global = steps.ToArray() };
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
