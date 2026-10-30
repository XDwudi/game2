using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace DiceCraft.Core
{
    /// <summary>Pure exact scoring over a frozen ordered snapshot; no RNG or mutation.</summary>
    public static class ScoreCalculator
    {
        private static readonly string[] MultiplierIds = { "C02", "C06", "C08", "C09", "C11", "C14", "C18", "C28", "C30", "C31", "C32", "C33", "C40" };
        public static ScoreBreakdown Evaluate(SubmissionSnapshot s, GameConfig cfg)
        {
            Validate(s, cfg);
            var instances = ControllerRules.Resolve(s);
            var controllers = new HashSet<string>(instances.Select(c => c.definitionId), StringComparer.Ordinal);
            var levels = (s.handLevels ?? Array.Empty<HandLevel>()).ToDictionary(h => h.handId, h => h.level, StringComparer.Ordinal);
            var pips = s.dice.Select(d => d.currentPip).ToArray();
            var colors = s.dice.Select(d => d.currentColor).ToArray();
            bool changedOdd = controllers.Contains("C12") && pips.Any(p => p % 2 == 1);
            if (controllers.Contains("C12"))
                for (int i = 0; i < pips.Length; i++) if (pips[i] % 2 == 1) pips[i] = checked(pips[i] + cfg.Controller("C12").parameters.oddPipAdd);
            if (controllers.Contains("C03")) colors[cfg.Controller("C03").parameters.occupiedIndex] = "red";
            if (s.redWave) for (int i = 0; i < colors.Length; i++) colors[i] = "red";
            int n = pips.Length, redN = colors.Count(c => c == "red");
            var counts = pips.GroupBy(p => p).ToDictionary(g => g.Key, g => g.Count());
            int maxSame = counts.Values.Max(), pairKinds = counts.Values.Count(c => c >= 2);
            bool shortStraight = HandEvaluator.HasStraight(counts.Keys, cfg.Controller("C29").parameters.minimumStraightLength);
            var trigger = new HashSet<string>(StringComparer.Ordinal);
            Action<string, bool> hit = (id, condition) => { if (condition && controllers.Contains(id)) trigger.Add(id); };
            hit("C02", redN == 0); hit("C03", true);
            hit("C06", counts.Count >= cfg.Controller("C06").parameters.minDistinct);
            hit("C07", s.submissionOrdinalBefore == 0 && n == cfg.Controller("C07").parameters.requiredDice && pips[0] != pips[1]);
            hit("C08", n < s.capacity); hit("C09", true);
            hit("C10", counts.ContainsKey(cfg.Controller("C10").parameters.pip));
            var p11 = cfg.Controller("C11").parameters;
            hit("C11", Count(counts, p11.pipA) >= p11.countA && Count(counts, p11.pipB) >= p11.countB);
            hit("C12", changedOdd); hit("C13", pips.Any(p => p % 2 == 1)); hit("C14", pips.All(p => p % 2 == 1));
            hit("C15", n >= 3 && string.Join(",", colors.Take(3)) == cfg.Controller("C15").parameters.pattern);
            hit("C17", pips.Any(p => p <= cfg.Controller("C17").parameters.maxPip));
            hit("C18", redN == n - redN);
            hit("C19", s.dice.Any(d => d.permanentSixFaceSum <= cfg.Controller("C19").parameters.maxPermanentFaceSum));
            hit("C20", n == s.capacity && pips[n - 1] == cfg.Controller("C20").parameters.lastSlotPip);
            hit("C24", Enumerable.Range(0, n).Any(i => colors[i] == "red" && pips[i] == cfg.Controller("C24").parameters.redGroupPipFrom));
            hit("C25", redN > 0); hit("C26", n == cfg.Controller("C26").parameters.requiredDice);
            hit("C27", maxSame >= cfg.Controller("C27").parameters.minSamePip);
            hit("C28", pairKinds >= cfg.Controller("C28").parameters.minDifferentPairs);
            hit("C29", shortStraight); hit("C30", maxSame >= cfg.Controller("C30").parameters.minSamePip);
            hit("C31", maxSame >= cfg.Controller("C31").parameters.minSamePip);
            hit("C32", counts.Count == cfg.Controller("C32").parameters.requiredDistinct);
            hit("C33", n >= cfg.Controller("C33").parameters.minDice && pips[0] >= pips[1] + cfg.Controller("C33").parameters.firstMinusSecondAtLeast);
            hit("C36", n >= cfg.Controller("C36").parameters.minDice && n < s.capacity);
            hit("C37", n >= cfg.Controller("C37").parameters.minDice && n < s.capacity);
            hit("C38", true); hit("C39", colors.Any(c => c == cfg.Controller("C39").parameters.effectiveColor));
            hit("C40", n >= 3 && pips[1] > pips[0] && pips[2] - pips[1] == pips[1] - pips[0]);
            var contributions = Enumerable.Range(0, n).Select(i => ScoreExplanation.DescribeDie(s, i, pips[i], colors[i], cfg, controllers).contribution).ToArray();
            string[] matched = HandEvaluator.SameValueCandidates(counts);
            HandDefinition chosen = matched.Select(cfg.Hand).OrderByDescending(h => Multiplier(h, levels)).ThenByDescending(h => h.tieBreakRank).FirstOrDefault();
            int group = chosen == null ? 0 : Multiplier(chosen, levels);
            int requiredLength = controllers.Contains("C29") ? cfg.Controller("C29").parameters.minimumStraightLength : cfg.minStraightLength;
            int straight = HandEvaluator.HasStraight(counts.Keys, requiredLength) ? Multiplier(cfg.Hand("straight"), levels) : 0;
            int color = n >= cfg.minDiceForColorHand && (redN == n || redN == 0) ? Multiplier(cfg.Hand(redN == n ? "all_red" : "all_blue"), levels) : 0;
            int hand = Math.Max(100, checked(group + straight + color));
            bool merge = controllers.Contains("C25"), map = controllers.Contains("C24");
            var redGroup = Enumerable.Range(0, n).Where(i => colors[i] == "red")
                .GroupBy(i => merge ? 0 : map && pips[i] == cfg.Controller("C24").parameters.redGroupPipFrom ? cfg.Controller("C24").parameters.redGroupPipTo : pips[i])
                .OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).FirstOrDefault();
            int redCount = redGroup?.Count() ?? 0, redValue = redGroup?.Key ?? 0;
            int redMult = cfg.redRewards.Single(r => r.count == redCount).factor100;
            string[] multTriggers = MultiplierIds.Where(trigger.Contains).ToArray();
            int multiplier = 1;
            foreach (string id in multTriggers) multiplier = checked(multiplier * cfg.Controller(id).parameters.scoreMultiplier);
            int pipSum = contributions.Sum(), bonus = checked(cfg.bonusPerUnusedRoll * s.rollsRemaining);
            int pairAdd = trigger.Contains("C27") ? cfg.Controller("C27").parameters.baseAdd : 0;
            int baseCount = instances.Count(c => trigger.Contains(c.definitionId) && c.enchantmentId == "base5");
            int multCount = instances.Count(c => trigger.Contains(c.definitionId) && c.enchantmentId == "mult120");
            int enchantAdd = checked(baseCount * cfg.enchantRules.baseAdd), baseTotal = checked(pipSum + bonus + pairAdd + enchantAdd);
            BigInteger numerator = (BigInteger)baseTotal * hand * redMult * multiplier * BigInteger.Pow(cfg.enchantRules.multiplierNumerator, multCount);
            BigInteger denominator = 10000 * BigInteger.Pow(cfg.enchantRules.multiplierDenominator, multCount);
            long score = checked((long)(numerator / denominator));
            return new ScoreBreakdown
            {
                effectivePips = pips, effectiveColors = colors, pipContributions = contributions,
                matchedGroupIds = matched, chosenGroupId = chosen?.id, pipSum = pipSum, unusedRollBonus = bonus,
                pairBaseAdd = pairAdd, enchantBaseAdd = enchantAdd, baseTotal = baseTotal,
                groupMult100 = group, straightMult100 = straight, colorMult100 = color, totalHandMult100 = hand,
                redGroupingMode = merge ? "all_red" : map ? "one_as_four" : "same_value",
                redValue = redValue, redCount = redCount, redMult100 = redMult,
                controllerMultiplier = multiplier, triggeredMultiplierIds = multTriggers, triggeredControllerIds = trigger.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                enchantBaseCount = baseCount, enchantMultiplierCount = multCount,
                scoreNumerator = numerator.ToString(CultureInfo.InvariantCulture), scoreDenominator = denominator.ToString(CultureInfo.InvariantCulture), score = score
            };
        }
        private static int Count(Dictionary<int, int> counts, int pip) => counts.TryGetValue(pip, out int count) ? count : 0;
        private static int Multiplier(HandDefinition hand, Dictionary<string, int> levels) => HandEvaluator.Multiplier100(hand, levels.TryGetValue(hand.id, out int level) ? level : 0);
        private static void Validate(SubmissionSnapshot s, GameConfig cfg)
        {
            if (s == null || cfg == null) throw new ArgumentNullException(s == null ? nameof(s) : nameof(cfg));
            if (s.capacity < 1 || s.capacity > cfg.maxSubmitCapacity || s.dice == null || s.dice.Length < 1 || s.dice.Length > s.capacity || s.rollsRemaining < 0 || s.submissionOrdinalBefore < 0)
                throw new ArgumentException("Invalid submission count, capacity or budget.");
            if (!string.IsNullOrEmpty(s.configVersion) && s.configVersion != cfg.configVersion) throw new ArgumentException("Submission config mismatch.");
            if (s.dice.Any(d => d == null || string.IsNullOrEmpty(d.dieId) || string.IsNullOrEmpty(d.faceId) || d.currentPip < cfg.minPipValue || d.currentPip > cfg.maxPipValue || (d.currentColor != "red" && d.currentColor != "blue") || d.permanentSixFaceSum < cfg.minPipValue * 6 || d.permanentSixFaceSum > cfg.maxPipValue * 6))
                throw new ArgumentException("Invalid submission die.");
            if (s.dice.Select(d => d.dieId).Distinct(StringComparer.Ordinal).Count() != s.dice.Length) throw new ArgumentException("Duplicate submitted die ID.");
            foreach (var die in s.dice) cfg.Opposite(die.faceId);
            // Pure snapshots may represent explicitly marked test fixtures above ordinary slots;
            // session ownership/slot legality is checked at the persistent state boundary.
            ControllerRules.ValidateEquipment(ControllerRules.Resolve(s), cfg, debug: true);
            var levels = s.handLevels ?? Array.Empty<HandLevel>();
            if (levels.Any(h => h == null) || levels.Select(h => h.handId).Distinct(StringComparer.Ordinal).Count() != levels.Length) throw new ArgumentException("Invalid/duplicate hand level.");
            foreach (var h in levels) if (h.level < 0 || h.level > cfg.Hand(h.handId).maxLevel) throw new ArgumentException("Hand level out of range.");
        }
    }

    public static class HandEvaluator
    {
        public static int Multiplier100(HandDefinition hand, int level)
        {
            if (hand == null) throw new ArgumentNullException(nameof(hand));
            if (level < 0 || level > hand.maxLevel) throw new ArgumentOutOfRangeException(nameof(level));
            return checked(hand.baseMult100 + hand.incrementPerLevel100 * level);
        }
        public static string[] SameValueCandidates(IReadOnlyDictionary<int, int> counts)
        {
            if (counts == null) throw new ArgumentNullException(nameof(counts));
            var result = new List<string>();
            int max = counts.Count == 0 ? 0 : counts.Values.Max();
            if (max >= 2) result.Add("pair");
            if (counts.Values.Count(n => n >= 2) >= 2) result.Add("two_pair");
            if (max >= 3) result.Add("trips");
            if (counts.Any(a => a.Value >= 3 && counts.Any(b => b.Key != a.Key && b.Value >= 2))) result.Add("full_house");
            if (max >= 4) result.Add("quads");
            if (max >= 5) result.Add("five_kind");
            if (max >= 6) result.Add("six_kind");
            if (max >= 7) result.Add("seven_kind");
            if (max >= 8) result.Add("eight_kind");
            return result.ToArray();
        }

        public static bool HasStraight(IEnumerable<int> values, int length)
        {
            if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));
            int run = 0, last = int.MinValue;
            foreach (int value in values.Distinct().OrderBy(v => v))
            {
                run = last != int.MinValue && value == last + 1 ? run + 1 : 1;
                if (run >= length) return true;
                last = value;
            }
            return false;
        }
    }
}
