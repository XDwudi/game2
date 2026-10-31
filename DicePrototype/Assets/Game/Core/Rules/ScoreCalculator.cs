using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace DiceCraft.Core
{
    /// <summary>Frozen exact arithmetic and event plan. No random draw, mutation, storage or session dependency.</summary>
    public static class ScoreCalculator
    {
        private static readonly HashSet<string> FixedMultipliers = new HashSet<string>(new[] { "C02", "C06", "C08", "C09", "C11", "C14", "C18", "C28", "C30", "C31", "C32", "C33", "C40", "C42", "C46" }, StringComparer.Ordinal);
        public static ScorePreview Preview(SubmissionSnapshot snapshot, GameConfig cfg)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var all = ControllerRules.Resolve(snapshot);
            string[] owners = ControllerRules.ResolveSources(all, snapshot.mimicBindings).Where(s => s.abilityId == "C53").Select(s => s.stateOwnerId).Distinct(StringComparer.Ordinal).ToArray();
            var choices = snapshot.resolvedRandomChoices ?? Array.Empty<RandomMultiplierChoice>();
            var unknown = owners.Where(id => !choices.Any(c => c.ownerInstanceId == id)).ToArray();
            var lo = snapshot.Clone(); var hi = snapshot.Clone();
            lo.resolvedRandomChoices = choices.Select(c => c.Clone()).Concat(unknown.Select(id => new RandomMultiplierChoice { ownerInstanceId = id, units2 = cfg.Controller("C53").parameters.factorUnits2.Min() })).ToArray();
            hi.resolvedRandomChoices = choices.Select(c => c.Clone()).Concat(unknown.Select(id => new RandomMultiplierChoice { ownerInstanceId = id, units2 = cfg.Controller("C53").parameters.factorUnits2.Max() })).ToArray();
            return new ScorePreview { minimum = Evaluate(lo, cfg), maximum = Evaluate(hi, cfg), isRandom = unknown.Length > 0, randomOwnerIds = unknown };
        }
        public static ScoreBreakdown Evaluate(SubmissionSnapshot s, GameConfig cfg)
        {
            Validate(s, cfg);
            var instances = ControllerRules.Resolve(s); var sources = ControllerRules.ResolveSources(instances, s.mimicBindings);
            Func<string, int> count = id => ControllerRules.SourceCount(sources, id);
            Func<string, bool> has = id => count(id) > 0;
            var levels = (s.handLevels ?? Array.Empty<HandLevel>()).ToDictionary(h => h.handId, h => h.level, StringComparer.Ordinal);
            var colorLevels = (s.colorLevels ?? Array.Empty<HandLevel>()).ToDictionary(h => h.handId, h => h.level, StringComparer.Ordinal);
            var pips = s.dice.Select(d => d.currentPip).ToArray(); var colors = s.dice.Select(d => d.currentColor).ToArray();
            bool changedOdd = has("C12") && pips.Any(p => p % 2 == 1);
            if (has("C12")) for (int i = 0; i < pips.Length; i++) if (pips[i] % 2 == 1) pips[i] += cfg.Controller("C12").parameters.oddToEvenAdd;
            if (has("C03")) colors[cfg.Controller("C03").parameters.index] = "red";
            if (s.redWave) for (int i = 0; i < colors.Length; i++) colors[i] = "red";
            int n = pips.Length, redN = colors.Count(c => c == "red");
            var counts = pips.GroupBy(p => p).ToDictionary(g => g.Key, g => g.Count());
            int maxSame = counts.Values.Max(), pairKinds = counts.Values.Count(c => c >= 2);
            var matched = HandEvaluator.SameValueCandidates(counts);
            var chosen = matched.Select(cfg.Hand).OrderByDescending(h => Multiplier(h, levels, s.debug ? cfg.debug.maxHandLevel : h.maxLevel)).ThenByDescending(h => h.structuralPriority).FirstOrDefault();
            int group = chosen == null ? 0 : Multiplier(chosen, levels, s.debug ? cfg.debug.maxHandLevel : chosen.maxLevel);
            int requiredLength = has("C29") ? cfg.Controller("C29").parameters.minimumStraightLength : cfg.minStraightLength;
            bool hasStraight = HandEvaluator.HasStraight(counts.Keys, requiredLength);
            int straight = hasStraight ? Multiplier(cfg.Hand("straight"), levels, s.debug ? cfg.debug.maxHandLevel : cfg.maxHandLevel) : 0;
            string primary = matched.Concat(hasStraight ? new[] { "straight" } : Array.Empty<string>()).Select(cfg.Hand).OrderByDescending(h => h.structuralPriority).FirstOrDefault()?.id ?? "scatter";
            string colorId = redN == n ? "all_red" : redN == 0 ? "all_blue" : null;
            int color = colorId == null ? 100 : HandEvaluator.ColorMultiplier100(cfg.Color(colorId), Level(colorLevels, colorId), s.debug ? cfg.debug.maxColorLevel : cfg.maxColorLevel);
            int hand = Math.Max(100, checked(group + straight));
            int[] spread = Enumerable.Range(1, Math.Max(0, n - 2)).Where(i => colors[i] == "blue" && colors[i - 1] == "red" && colors[i + 1] == "red").ToArray();
            var triggered = new List<AbilitySource>();
            foreach (var source in sources)
            {
                string id = source.abilityId; if (id == null) continue; var p = cfg.Controller(id).parameters; bool hit;
                switch (id)
                {
                    case "C01": case "C03": case "C09": case "C38": case "C42": case "C45": case "C46": case "C47": case "C49": case "C50": case "C51": case "C53": hit = true; break;
                    case "C02": hit = redN == 0; break;
                    case "C06": hit = counts.Count >= p.minDistinct; break;
                    case "C07": hit = s.submissionOrdinalBefore == p.submissionOrdinal && n == p.requiredDice && pips[p.sourceIndex] != pips[p.targetIndex]; break;
                    case "C08": case "C48": hit = n < s.capacity; break;
                    case "C10": hit = counts.ContainsKey(p.pip); break;
                    case "C11": hit = Count(counts, 2) >= p.requiredCounts.pip2 && Count(counts, 3) >= p.requiredCounts.pip3; break;
                    case "C12": hit = changedOdd; break;
                    case "C13": hit = pips.Any(v => v % 2 == 1); break;
                    case "C14": hit = pips.All(v => v % 2 == 1); break;
                    case "C15": hit = spread.Length > 0; break;
                    case "C17": hit = pips.Any(v => p.pips.Contains(v)); break;
                    case "C18": hit = redN == n - redN; break;
                    case "C19": hit = s.dice.Any(d => d.permanentSixFaceSum <= p.maxPermanentSixFaceSum); break;
                    case "C20": hit = n == s.capacity && pips[n - 1] == p.lastActualCapacitySlotPip; break;
                    case "C24": hit = Enumerable.Range(0, n).Any(i => colors[i] == "red" && pips[i] == p.redGroupKeyFrom); break;
                    case "C25": hit = redN > 0; break;
                    case "C26": hit = n == p.requiredDice; break;
                    case "C27": hit = pairKinds > 0; break;
                    case "C28": hit = pairKinds >= p.minDistinctPairs; break;
                    case "C29": hit = HandEvaluator.HasStraight(counts.Keys, p.minimumStraightLength); break;
                    case "C30": case "C31": hit = maxSame >= p.minSamePip; break;
                    case "C32": hit = counts.Count == p.distinctPips; break;
                    case "C33": hit = n >= p.minDice && pips[0] - pips[1] >= p.firstMinusSecondAtLeast; break;
                    case "C36": case "C37": hit = n >= p.minDice && n < s.capacity; break;
                    case "C39": hit = colors.Any(c => c == p.effectiveColor); break;
                    case "C40": hit = n >= 3 && pips[1] > pips[0] && pips[2] - pips[1] == pips[1] - pips[0]; break;
                    case "C43": hit = primary != "scatter" && primary == s.previousPrimaryHandId; break;
                    case "C44": hit = s.wallet > 0; break;
                    default: hit = false; break;
                }
                if (hit) triggered.Add(source);
            }
            Func<string, int> hits = id => triggered.Count(t => t.abilityId == id);
            var contributions = Enumerable.Range(0, n).Select(i => ScoreExplanation.DescribeDie(s, i, pips[i], colors[i], cfg, sources).contribution).ToArray();
            BigInteger pipSum = contributions.Aggregate(BigInteger.Zero, (a, b) => a + b);
            int bonus = checked(cfg.bonusPerUnusedRoll * s.rollsRemaining);
            int pairAdd = checked(hits("C27") * pairKinds * cfg.Controller("C27").parameters.baseAddPerDistinctPairedPip);
            BigInteger walletAdd = (BigInteger)s.wallet * hits("C44") * cfg.Controller("C44").parameters.baseAddPerFrozenGold;
            int emptyAdd = checked(hits("C48") * (s.capacity - n) * cfg.Controller("C48").parameters.baseAddPerEmptySubmitSlot);
            int steadyAdd = checked(hits("C50") * cfg.Controller("C50").parameters.baseAdd);
            int baseCount = triggered.Count(t => t.enchantmentId == "base10"), multCount = triggered.Count(t => t.enchantmentId == "mult2");
            int enchantAdd = checked(baseCount * cfg.enchantRules.baseAdd);
            BigInteger baseTotal = pipSum + bonus + pairAdd + walletAdd + emptyAdd + steadyAdd + enchantAdd;
            bool merge = has("C25"), map = has("C24");
            var redGroup = Enumerable.Range(0, n).Where(i => colors[i] == "red").GroupBy(i => merge ? 0 : map && pips[i] == cfg.Controller("C24").parameters.redGroupKeyFrom ? cfg.Controller("C24").parameters.redGroupKeyTo : pips[i]).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).FirstOrDefault();
            int redCount = redGroup?.Count() ?? 0, redValue = redGroup?.Key ?? 0;
            int redMult = cfg.redRewards.Single(r => r.count == Math.Min(redCount, cfg.debug.redGroupCapForExperiments)).factor100;
            var factors = new List<MultiplierFactor>(); BigInteger xnum = 1, xden = 1;
            var choices = (s.resolvedRandomChoices ?? Array.Empty<RandomMultiplierChoice>()).ToDictionary(c => c.ownerInstanceId, c => c.units2, StringComparer.Ordinal);
            foreach (var t in triggered)
            {
                BigInteger num = 1, den = 1; bool multiply = true;
                if (FixedMultipliers.Contains(t.abilityId)) num = cfg.Controller(t.abilityId).parameters.scoreMultiplier;
                else switch (t.abilityId)
                    {
                        case "C47": num = instances.Count(c => c.alive); break;
                        case "C49": num = Count(counts, 5); break;
                        case "C51": num = t.state.turnoverUnits2; den = 2; break;
                        case "C53": if (!choices.TryGetValue(t.stateOwnerId, out int units)) throw new ArgumentException("C53 outcome must be frozen before Evaluate; use Preview for ranges."); num = units; den = 2; break;
                        default: multiply = false; break;
                    }
                if (!multiply) continue;
                var gcd = BigInteger.GreatestCommonDivisor(num, den); num /= gcd; den /= gcd;
                factors.Add(new MultiplierFactor { emitterId = t.emitterId, abilityId = t.abilityId, stateOwnerId = t.stateOwnerId, numerator = ExactScoreMath.SaveDecimal(num), denominator = ExactScoreMath.SaveDecimal(den) });
                xnum *= num; xden *= den; gcd = BigInteger.GreatestCommonDivisor(xnum, xden); xnum /= gcd; xden /= gcd;
            }
            int requested = checked(hits("C43") * cfg.Controller("C43").parameters.levelAddPerSource);
            var plan = new AfterScorePlan
            {
                earnedGold = checked(hits("C01") * cfg.Controller("C01").parameters.goldPerValidSubmission),
                copyFirstToSecond = hits("C07") > 0,
                redSpreadIndices = hits("C15") > 0 ? spread : Array.Empty<int>(),
                dyeAllFacesRed = hits("C26") > 0,
                growthNetDelta = checked(hits("C36") * cfg.Controller("C36").parameters.delta + hits("C37") * cfg.Controller("C37").parameters.delta),
                growthIndices = hits("C36") + hits("C37") > 0 ? new[] { 0, 1 } : Array.Empty<int>(),
                blueSpreadIndices = hits("C38") > 0 ? (n == 1 ? new[] { 0 } : new[] { 0, n - 1 }) : Array.Empty<int>(),
                upgradeHandId = requested > 0 ? primary : null,
                upgradeRequested = requested,
                upgradeApplied = requested > 0 ? Math.Min(requested, (s.debug ? cfg.debug.maxHandLevel : cfg.maxHandLevel) - Level(levels, primary)) : 0,
                nextPreviousPrimary = primary,
                nextColorOwnerIds = instances.Where(c => c.alive && c.definitionId == "C45").Select(c => c.instanceId).ToArray(),
                randomFactorOwnerIds = instances.Where(c => c.alive && c.definitionId == "C53").Select(c => c.instanceId).ToArray(),
                cannonRiskEmitterIds = triggered.Where(t => t.abilityId == "C42").Select(t => t.emitterId).ToArray()
            };
            BigInteger numerator = baseTotal * hand * color * redMult * xnum * BigInteger.Pow(cfg.enchantRules.scoreMultiplier, multCount), denominator = 1000000 * xden;
            return new ScoreBreakdown
            {
                effectivePips = pips,
                effectiveColors = colors,
                pipContributions = contributions,
                pipSum = ExactScoreMath.SaveDecimal(pipSum),
                unusedRollBonus = bonus,
                matchedGroupIds = matched,
                chosenGroupId = chosen?.id,
                primaryHandId = primary,
                colorId = colorId,
                groupMult100 = group,
                straightMult100 = straight,
                totalHandMult100 = hand,
                colorMult100 = color,
                pairKinds = pairKinds,
                pairBaseAdd = pairAdd,
                walletBaseAdd = ExactScoreMath.SaveDecimal(walletAdd),
                emptySlotBaseAdd = emptyAdd,
                steadyBaseAdd = steadyAdd,
                enchantBaseAdd = enchantAdd,
                baseTotal = ExactScoreMath.SaveDecimal(baseTotal),
                enchantBaseCount = baseCount,
                enchantMultiplierCount = multCount,
                redGroupingMode = merge ? "all_red" : map ? "one_as_four" : "same_value",
                redCount = redCount,
                redValue = redValue,
                redMult100 = redMult,
                redRewardCapped = redCount > cfg.debug.redGroupCapForExperiments,
                controllerMultiplierNumerator = ExactScoreMath.SaveDecimal(xnum),
                controllerMultiplierDenominator = ExactScoreMath.SaveDecimal(xden),
                multiplierFactors = factors.ToArray(),
                triggeredMultiplierIds = factors.Select(f => f.abilityId).ToArray(),
                triggeredControllerIds = triggered.Select(t => t.abilityId).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                triggeredSourceIds = triggered.Select(t => t.emitterId).ToArray(),
                afterScorePlan = plan,
                scoreNumerator = ExactScoreMath.SaveDecimal(numerator),
                scoreDenominator = ExactScoreMath.SaveDecimal(denominator),
                score = ExactScoreMath.SaveDecimal(numerator / denominator)
            };
        }
        private static int Count(IReadOnlyDictionary<int, int> counts, int pip) => counts.TryGetValue(pip, out int n) ? n : 0;
        private static int Level(Dictionary<string, int> levels, string id) => levels.TryGetValue(id, out int n) ? n : 0;
        private static int Multiplier(HandDefinition h, Dictionary<string, int> levels, int max) => HandEvaluator.Multiplier100(h, Level(levels, h.id), max);
        private static void Validate(SubmissionSnapshot s, GameConfig cfg)
        {
            if (s == null || cfg == null) throw new ArgumentNullException();
            if (s.capacity < 1 || s.capacity > (s.debug ? cfg.debug.maxSubmitCapacity : cfg.maxSubmitCapacity) || s.dice == null || s.dice.Length < 1 || s.dice.Length > s.capacity || s.rollsRemaining < 0 || s.submissionOrdinalBefore < 0 || s.wallet < 0) throw new ArgumentException("Invalid frozen submission.");
            if (!string.IsNullOrEmpty(s.configVersion) && s.configVersion != cfg.configVersion) throw new ArgumentException("Config mismatch.");
            if (s.dice.Any(d => d == null || string.IsNullOrEmpty(d.dieId) || string.IsNullOrEmpty(d.faceId) || d.currentPip < 1 || d.currentPip > 6 || (d.currentColor != "red" && d.currentColor != "blue") || d.permanentSixFaceSum < 6 || d.permanentSixFaceSum > 36) || s.dice.Select(d => d.dieId).Distinct(StringComparer.Ordinal).Count() != s.dice.Length) throw new ArgumentException("Invalid frozen dice.");
            foreach (var die in s.dice) cfg.Opposite(die.faceId);
            ControllerRules.ValidateInstances(ControllerRules.Resolve(s), cfg, s.debug, false);
            ValidateLevels(s.handLevels, id => cfg.Hand(id).maxLevel, s.debug ? cfg.debug.maxHandLevel : 0);
            ValidateLevels(s.colorLevels, id => cfg.Color(id).maxLevel, s.debug ? cfg.debug.maxColorLevel : 0);
            if (s.previousPrimaryHandId != null && s.previousPrimaryHandId != "" && s.previousPrimaryHandId != "scatter") cfg.Hand(s.previousPrimaryHandId);
            var choices = s.resolvedRandomChoices ?? Array.Empty<RandomMultiplierChoice>();
            if (choices.Any(c => c == null || string.IsNullOrEmpty(c.ownerInstanceId) || !cfg.Controller("C53").parameters.factorUnits2.Contains(c.units2)) || choices.Select(c => c.ownerInstanceId).Distinct(StringComparer.Ordinal).Count() != choices.Length) throw new ArgumentException("Invalid random score choices.");
            var owners = ControllerRules.Resolve(s).Where(c => c.alive && c.definitionId == "C53").Select(c => c.instanceId).ToArray();
            if (choices.Any(c => !owners.Contains(c.ownerInstanceId))) throw new ArgumentException("Random choice owner is not a live C53.");
        }
        private static void ValidateLevels(HandLevel[] entries, Func<string, int> max, int debugMax)
        {
            var levels = entries ?? Array.Empty<HandLevel>(); if (levels.Any(h => h == null) || levels.Select(h => h.handId).Distinct(StringComparer.Ordinal).Count() != levels.Length) throw new ArgumentException("Duplicate/null levels.");
            foreach (var h in levels) { int bound = max(h.handId); if (h.level < 0 || h.level > (debugMax > 0 ? debugMax : bound)) throw new ArgumentException("Level out of range."); }
        }
    }
    public static class HandEvaluator
    {
        public static int Multiplier100(HandDefinition hand, int level, int maxLevel = -1)
        {
            if (hand == null) throw new ArgumentNullException(nameof(hand)); if (level < 0 || level > (maxLevel < 0 ? hand.maxLevel : maxLevel)) throw new ArgumentOutOfRangeException(nameof(level)); return checked(hand.baseMult100 + hand.incrementPerLevel100 * level);
        }
        public static int ColorMultiplier100(ColorDefinition color, int level, int maxLevel = -1)
        {
            if (color == null) throw new ArgumentNullException(nameof(color)); if (level < 0 || level > (maxLevel < 0 ? color.maxLevel : maxLevel)) throw new ArgumentOutOfRangeException(nameof(level)); return checked(color.baseMult100 + color.incrementPerLevel100 * level);
        }
        public static string[] SameValueCandidates(IReadOnlyDictionary<int, int> counts)
        {
            if (counts == null) throw new ArgumentNullException(nameof(counts)); var ids = new List<string>(); int max = counts.Count == 0 ? 0 : counts.Values.Max(), pairs = counts.Values.Count(n => n >= 2), trips = counts.Values.Count(n => n >= 3);
            if (max >= 2) ids.Add("pair"); if (pairs >= 2) ids.Add("two_pair"); if (max >= 3) ids.Add("trips"); if (counts.Any(a => a.Value >= 3 && counts.Any(b => b.Key != a.Key && b.Value >= 2))) ids.Add("full_house");
            if (pairs >= 3) ids.Add("three_pair"); if (max >= 4) ids.Add("quads"); if (trips >= 2) ids.Add("two_trips"); if (max >= 5) ids.Add("five_kind"); if (pairs >= 4) ids.Add("four_pair"); if (max >= 6) ids.Add("six_kind"); if (max >= 7) ids.Add("seven_kind"); if (max >= 8) ids.Add("eight_kind"); return ids.ToArray();
        }
        public static bool HasStraight(IEnumerable<int> values, int length)
        {
            if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length)); int run = 0, last = int.MinValue;
            foreach (int v in values.Distinct().OrderBy(x => x)) { run = last != int.MinValue && v == last + 1 ? run + 1 : 1; if (run >= length) return true; last = v; }
            return false;
        }
    }
}
