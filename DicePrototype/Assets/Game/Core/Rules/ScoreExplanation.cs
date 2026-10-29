using System;
using System.Collections.Generic;
using System.Linq;

namespace DiceCraft.Core
{
    /// <summary>Exact arithmetic explanation over frozen submission inputs. No session, I/O or RNG.</summary>
    public sealed class ScoreStep
    {
        public string kind, sourceId, operation;
        public decimal before, operand, after;
    }

    public sealed class DieScoreTrace
    {
        public string dieId;
        public int contribution;
        public ScoreStep[] steps;
    }

    public sealed class ScoreTrace
    {
        public DieScoreTrace[] dice;
        public ScoreStep[] global;
    }

    public static class ScoreExplanation
    {
        // This shared routine is also used by ScoreCalculator, so additions and layer order
        // have one rule implementation. Presentation never evaluates controller conditions.
        internal static DieScoreTrace DescribeDie(SubmissionSnapshot snapshot, int index, int pip,
            GameConfig cfg, HashSet<string> controllers)
        {
            var die = snapshot.dice[index];
            var steps = new List<ScoreStep>();
            decimal value = pip;
            Action<string, int> add = (id, amount) => Step(steps, "contribution", id, "+", ref value, amount);
            if (controllers.Contains("C10") && pip == cfg.Controller("C10").parameters.pip)
                add("C10", cfg.Controller("C10").parameters.contributionAdd);
            if (controllers.Contains("C13") && pip % 2 == 1)
                add("C13", cfg.Controller("C13").parameters.oddContributionAdd);
            if (controllers.Contains("C17") && pip <= cfg.Controller("C17").parameters.maxPip)
                add("C17", cfg.Controller("C17").parameters.contributionAdd);
            if (controllers.Contains("C19") && die.permanentSixFaceSum <= cfg.Controller("C19").parameters.maxPermanentFaceSum)
                add("C19", cfg.Controller("C19").parameters.contributionAdd);
            if (die.isGlass) Step(steps, "glass", null, "×", ref value, cfg.glassContributionMultiplier);
            if (die.isGold) Step(steps, "gold", null, "×", ref value, cfg.goldContributionMultiplier);
            return new DieScoreTrace {dieId = die.dieId, contribution = checked((int)value), steps = steps.ToArray()};
        }

        public static ScoreTrace Build(SubmissionSnapshot snapshot, ScoreBreakdown breakdown, GameConfig cfg)
        {
            if (snapshot == null || breakdown == null || cfg == null) throw new ArgumentNullException();
            var controllers = new HashSet<string>(snapshot.equippedControllerIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            var dice = snapshot.dice.Select((d, i) => DescribeDie(snapshot, i, breakdown.effectivePips[i], cfg, controllers)).ToArray();
            for (int i = 0; i < dice.Length; i++)
                if (dice[i].contribution != breakdown.pipContributions[i]) throw new ArgumentException("Frozen contribution does not match its explanation.");
            var steps = new List<ScoreStep>();
            decimal value = breakdown.pipSum;
            Step(steps, "unused_rolls", null, "+", ref value, breakdown.unusedRollBonus);
            Step(steps, "hand", null, "×", ref value, breakdown.totalHandMult100 / 100m);
            Step(steps, "red_group", null, "×", ref value, breakdown.redMult100 / 100m);
            foreach (string id in breakdown.triggeredMultiplierIds)
                Step(steps, "controller", id, "×", ref value, cfg.Controller(id).parameters.scoreMultiplier);
            steps.Add(new ScoreStep {kind = "floor", operation = "floor", before = value, after = decimal.Floor(value)});
            value = decimal.Floor(value);
            Step(steps, "final_add", breakdown.finalFlatAdd > 0 ? "C20" : null, "+", ref value, breakdown.finalFlatAdd);
            if (value != breakdown.score) throw new ArgumentException("Frozen score does not match its explanation.");
            return new ScoreTrace {dice = dice, global = steps.ToArray()};
        }

        private static void Step(List<ScoreStep> steps, string kind, string id, string operation, ref decimal value, decimal operand)
        {
            decimal before = value;
            value = operation == "+" ? checked(value + operand) : checked(value * operand);
            steps.Add(new ScoreStep {kind = kind, sourceId = id, operation = operation, before = before, operand = operand, after = value});
        }
    }
}
