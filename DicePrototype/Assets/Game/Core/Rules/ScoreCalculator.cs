using System;
using System.Collections.Generic;
using System.Linq;

namespace DiceCraft.Core
{
    /// <summary>Pure calculation over a frozen, ordered snapshot. Never changes faces, state or RNG.</summary>
    public static class ScoreCalculator
    {
        public static ScoreBreakdown Evaluate(SubmissionSnapshot s, GameConfig cfg)
        {
            Validate(s, cfg);
            var controllers = new HashSet<string>(s.equippedControllerIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            var levels = (s.handLevels ?? Array.Empty<HandLevel>()).ToDictionary(h => h.handId, h => h.level, StringComparer.Ordinal);
            var pips = s.dice.Select(d => d.currentPip).ToArray();
            var colors = s.dice.Select(d => d.currentColor).ToArray();
            if (controllers.Contains("C12"))
                for (int i = 0; i < pips.Length; i++) if (pips[i] % 2 == 1) pips[i] = checked(pips[i] + cfg.Controller("C12").parameters.oddPipAdd);
            if (controllers.Contains("C03")) colors[cfg.Controller("C03").parameters.occupiedIndex] = "red";
            if (s.redWave) for (int i = 0; i < colors.Length; i++) colors[i] = "red";
            var contributions = new int[pips.Length];
            checked
            {
                for (int i = 0; i < pips.Length; i++)
                {
                    contributions[i] = ScoreExplanation.DescribeDie(s, i, pips[i], cfg, controllers).contribution;
                }
            }
            var counts = pips.GroupBy(p => p).ToDictionary(g => g.Key, g => g.Count());
            string[] matched = HandEvaluator.SameValueCandidates(counts);
            HandDefinition chosen = matched.Select(cfg.Hand)
                .OrderByDescending(h => Multiplier(h, levels)).ThenByDescending(h => h.tieBreakRank).FirstOrDefault();
            int group = chosen == null ? 0 : Multiplier(chosen, levels);
            int straight = HandEvaluator.HasStraight(counts.Keys, cfg.minStraightLength) ? Multiplier(cfg.Hand("straight"), levels) : 0;
            int redN = colors.Count(c => c == "red");
            int color = 0;
            if (s.dice.Length >= cfg.minDiceForColorHand)
            {
                if (redN == s.dice.Length) color = Multiplier(cfg.Hand("all_red"), levels);
                else if (redN == 0) color = Multiplier(cfg.Hand("all_blue"), levels);
            }
            int hand = Math.Max(100, checked(group + straight + color));
            var redGroup = Enumerable.Range(0, pips.Length).Where(i => colors[i] == "red")
                .GroupBy(i => pips[i]).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).FirstOrDefault();
            int redCount = redGroup?.Count() ?? 0;
            int redValue = redGroup?.Key ?? 0;
            int redMult = cfg.redRewards.Single(r => r.count == redCount).factor100;
            var triggers = new List<string>();
            AddTrigger(triggers, controllers, "C02", redN == 0);
            AddTrigger(triggers, controllers, "C06", counts.Count >= cfg.Controller("C06").parameters.minDistinct);
            AddTrigger(triggers, controllers, "C08", s.dice.Length < s.capacity);
            AddTrigger(triggers, controllers, "C09", true);
            var p11 = cfg.Controller("C11").parameters;
            AddTrigger(triggers, controllers, "C11", Count(counts, p11.pipA) >= p11.countA && Count(counts, p11.pipB) >= p11.countB);
            AddTrigger(triggers, controllers, "C14", pips.All(p => p % 2 == 1));
            AddTrigger(triggers, controllers, "C18", redN == pips.Length - redN);
            int multiplier = 1;
            foreach (string id in triggers) multiplier = checked(multiplier * cfg.Controller(id).parameters.scoreMultiplier);
            int flat = controllers.Contains("C20") && s.dice.Length == s.capacity && pips[pips.Length - 1] == cfg.Controller("C20").parameters.lastSlotPip
                ? cfg.Controller("C20").parameters.finalScoreAdd : 0;
            int pipSum = contributions.Sum();
            int bonus = checked(cfg.bonusPerUnusedRoll * s.rollsRemaining);
            long score = checked(checked((long)checked(pipSum + bonus) * hand * redMult * multiplier) / 10000L + flat);
            return new ScoreBreakdown
            {
                effectivePips = pips, effectiveColors = colors, pipContributions = contributions,
                matchedGroupIds = matched, chosenGroupId = chosen?.id, pipSum = pipSum, unusedRollBonus = bonus,
                groupMult100 = group, straightMult100 = straight, colorMult100 = color, totalHandMult100 = hand,
                redValue = redValue, redCount = redCount, redMult100 = redMult,
                controllerMultiplier = multiplier, triggeredMultiplierIds = triggers.ToArray(), finalFlatAdd = flat, score = score
            };
        }

        private static int Count(Dictionary<int, int> counts, int pip) => counts.TryGetValue(pip, out int count) ? count : 0;
        private static int Multiplier(HandDefinition hand, Dictionary<string, int> levels)
            => checked(hand.baseMult100 + hand.incrementPerLevel100 * (levels.TryGetValue(hand.id, out int level) ? level : 0));
        private static void AddTrigger(List<string> triggers, HashSet<string> equipped, string id, bool condition)
        { if (condition && equipped.Contains(id)) triggers.Add(id); }
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
            var controllers = s.equippedControllerIds ?? Array.Empty<string>();
            if (controllers.Length > cfg.maxControllerSlots || controllers.Distinct(StringComparer.Ordinal).Count() != controllers.Length) throw new ArgumentException("Duplicate/excess controller.");
            foreach (string id in controllers) cfg.Controller(id);
            var levels = s.handLevels ?? Array.Empty<HandLevel>();
            if (levels.Any(h => h == null) || levels.Select(h => h.handId).Distinct(StringComparer.Ordinal).Count() != levels.Length) throw new ArgumentException("Invalid/duplicate hand level.");
            foreach (var h in levels) if (h.level < 0 || h.level > cfg.Hand(h.handId).maxLevel) throw new ArgumentException("Hand level out of range.");
        }
    }

    public static class HandEvaluator
    {
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
