using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace DiceCraft.Core
{
    /// <summary>Frozen exact arithmetic and event plan. No random draw, mutation, storage or session dependency.
    /// p1.1.1 (docs/specs/v111/RULES_DELTA.md section 2): score = floor(B x H x R x (1 + A) x M x G), where B is the
    /// base score, H the hand multiplier, R the red group, A the additive total, M the controller-side product and
    /// G the item and boss factors. A, the growth values and every key ending in 100 are hundredths.</summary>
    public static class ScoreCalculator
    {
        public static ScorePreview Preview(SubmissionSnapshot snapshot, GameConfig cfg)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var all = ControllerRules.Resolve(snapshot);
            string[] owners = ControllerRules.ResolveSources(all, snapshot.mimicBindings, snapshot.disabledControllerIds).Where(s => s.abilityId == "C53").Select(s => s.stateOwnerId).Distinct(StringComparer.Ordinal).ToArray();
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
            var instances = ControllerRules.Resolve(s); var sources = ControllerRules.ResolveSources(instances, s.mimicBindings, s.disabledControllerIds);
            // v1.0 boss modifier: scoring effects read from the frozen modifier id (empty when none or nullified).
            var modifier = string.IsNullOrEmpty(s.modifierId) ? null : cfg.Modifier(s.modifierId);
            Func<string, bool> fx = e => modifier != null && modifier.effects.Contains(e);
            bool zeroLevels = fx("ZeroLevels"), silence = fx("EnchantSilence"), pipsOnly = fx("PipsOnly"), growthHalf = fx("GrowthHalf");
            Func<string, int> count = id => ControllerRules.SourceCount(sources, id);
            Func<string, bool> has = id => count(id) > 0;
            var levels = (zeroLevels ? Array.Empty<HandLevel>() : s.handLevels ?? Array.Empty<HandLevel>()).ToDictionary(h => h.handId, h => h.level, StringComparer.Ordinal);
            var colorLevels = (zeroLevels ? Array.Empty<HandLevel>() : s.colorLevels ?? Array.Empty<HandLevel>()).ToDictionary(h => h.handId, h => h.level, StringComparer.Ordinal);
            var pips = s.dice.Select(d => d.currentPip).ToArray(); var colors = s.dice.Select(d => d.currentColor).ToArray();
            bool changedOdd = has("C12") && pips.Any(p => p % 2 == 1);
            if (has("C12")) for (int i = 0; i < pips.Length; i++) if (pips[i] % 2 == 1) pips[i] += cfg.Controller("C12").parameters.oddToEvenAdd;
            if (has("C03")) colors[cfg.Controller("C03").parameters.index] = "red";
            if (s.redWave) for (int i = 0; i < colors.Length; i++) colors[i] = "red";
            if (s.blueWave) for (int i = 0; i < colors.Length; i++) colors[i] = "blue";
            int n = pips.Length, redN = colors.Count(c => c == "red");
            var counts = pips.GroupBy(p => p).ToDictionary(g => g.Key, g => g.Count());
            int maxSame = counts.Values.Max(), pairKinds = counts.Values.Count(c => c >= 2);
            var matched = HandEvaluator.SameValueCandidates(counts);
            if (fx("SmallHandsOnly")) matched = matched.Where(m => m == "pair" || m == "two_pair" || m == "trips").ToArray();
            var chosen = matched.Select(cfg.Hand).OrderByDescending(h => Multiplier(h, levels, s.debug ? cfg.debug.maxHandLevel : h.maxLevel)).ThenByDescending(h => h.structuralPriority).FirstOrDefault();
            int group = chosen == null ? 0 : Multiplier(chosen, levels, s.debug ? cfg.debug.maxHandLevel : chosen.maxLevel);
            int requiredLength = has("C29") ? cfg.Controller("C29").parameters.minimumStraightLength : cfg.minStraightLength;
            bool hasStraight = !fx("NoStraight") && HandEvaluator.HasStraight(counts.Keys, requiredLength);
            int straight = hasStraight ? Multiplier(cfg.Hand("straight"), levels, s.debug ? cfg.debug.maxHandLevel : cfg.maxHandLevel) : 0;
            string primary = matched.Concat(hasStraight ? new[] { "straight" } : Array.Empty<string>()).Select(cfg.Hand).OrderByDescending(h => h.structuralPriority).FirstOrDefault()?.id ?? "scatter";
            string colorId = redN == n ? "all_red" : redN == 0 ? "all_blue" : null;
            // v1.1.1: a colour is an additive bonus; NoRed / NoBlue clear only the matching bonus and keep colorId.
            int colorAdd = colorId == null ? 0 : HandEvaluator.ColorAdd100(cfg.Color(colorId), Level(colorLevels, colorId), s.debug ? cfg.debug.maxColorLevel : cfg.maxColorLevel);
            if ((colorId == "all_red" && fx("NoRed")) || (colorId == "all_blue" && fx("NoBlue"))) colorAdd = 0;
            int hand = Math.Max(100, checked(group + straight)); // re-evaluated below once the v0.9 hand multiplier adds are known
            int[] spread = Enumerable.Range(1, Math.Max(0, n - 2)).Where(i => colors[i] == "blue" && colors[i - 1] == "red" && colors[i + 1] == "red").ToArray();
            // v0.9 frozen context: pool dice, pairs summing to pairSum, and the red group (needed by trigger predicates).
            var pool = s.poolDice ?? Array.Empty<PoolDie>();
            int pairSum = cfg.Controller("C80").parameters.pairSum, sumPairs = 0;
            for (int a = 1; a * 2 <= pairSum; a++) sumPairs += a * 2 == pairSum ? Count(counts, a) / 2 : Math.Min(Count(counts, a), Count(counts, pairSum - a));
            bool merge = has("C25"), map = has("C24");
            var redGroup = Enumerable.Range(0, n).Where(i => colors[i] == "red").GroupBy(i => merge ? 0 : map && pips[i] == cfg.Controller("C24").parameters.redGroupKeyFrom ? cfg.Controller("C24").parameters.redGroupKeyTo : pips[i]).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).FirstOrDefault();
            int[] redMembers = redGroup?.ToArray() ?? Array.Empty<int>();
            int submittedRed = redMembers.Length, redValue = redGroup?.Key ?? 0;
            int redPool = has("C73") && submittedRed > 0 ? pool.Count(d => d.currentColor == "red") : 0;
            int redBonus = has("C146") && submittedRed > 0 ? 1 : 0;
            int redCount = checked(submittedRed + redPool + redBonus);
            // v1.0 context: dice sealed by the boss modifier, empty ordinary slots, common controllers, the C138 gold budget.
            var sealedDice = new bool[n];
            for (int i = 0; i < n; i++)
                sealedDice[i] = (fx("SealOdd") && pips[i] % 2 == 1) || (fx("SealEven") && pips[i] % 2 == 0) || (fx("SealLow") && pips[i] <= 3) || (fx("SealHigh") && pips[i] >= 4)
                    || (fx("SealRed") && colors[i] == "red") || (fx("SealBlue") && colors[i] == "blue") || (fx("SealFirst") && i == 0) || (fx("SealAlternate") && i % 2 == 0);
            int emptySlots = Math.Max(0, s.ordinaryControllerSlots - ControllerRules.OrdinaryUsed(instances, cfg));
            int commons = instances.Count(c => c.alive && cfg.Controller(c.definitionId).rarity == cfg.Controller("C152").parameters.rarity);
            long goldBudget = s.wallet; int goldSpent = 0;
            // v1.1.1 context: the smallest effective pip (C19), the frozen stage score and target (C156), and the value
            // of a growth card = start + catch-up + grown part, the grown part read at half under GrowthHalf.
            int minPip = pips.Min();
            BigInteger stageScoreBefore = string.IsNullOrEmpty(s.stageScoreBefore) ? BigInteger.Zero : ExactScoreMath.Parse(s.stageScoreBefore);
            BigInteger stageTarget = string.IsNullOrEmpty(s.stageTarget) ? BigInteger.One : ExactScoreMath.Parse(s.stageTarget);
            if (has("C156") && (string.IsNullOrEmpty(s.stageScoreBefore) || string.IsNullOrEmpty(s.stageTarget))) throw new ArgumentException("C156 needs the frozen stage score and target.");
            Func<AbilitySource, long> growthOf = t => ControllerRules.GrowthValue(cfg, t.abilityId, t.state, growthHalf);
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
                    case "C19": hit = minPip == 1 || minPip == 2; break;
                    case "C20": hit = n == s.capacity && pips[n - 1] == p.lastActualCapacitySlotPip; break;
                    case "C24": hit = Enumerable.Range(0, n).Any(i => colors[i] == "red" && pips[i] == p.redGroupKeyFrom); break;
                    case "C25": hit = redN > 0; break;
                    case "C26": hit = n == p.requiredDice; break;
                    case "C27": hit = pairKinds > 0; break;
                    case "C28": hit = pairKinds >= p.minDistinctPairs; break;
                    case "C29": hit = hasStraight; break;
                    case "C30": case "C31": hit = maxSame >= p.minSamePip; break;
                    case "C32": hit = counts.Count == p.distinctPips; break;
                    case "C33": hit = n >= p.minDice && pips[0] - pips[1] >= p.firstMinusSecondAtLeast; break;
                    case "C36": case "C37": hit = n >= p.minDice && n < s.capacity; break;
                    case "C39": hit = colors.Any(c => c == p.effectiveColor); break;
                    case "C40": hit = n >= 3 && pips[1] > pips[0] && pips[2] - pips[1] == pips[1] - pips[0]; break;
                    case "C43": hit = primary != "scatter" && primary == s.previousPrimaryHandId; break;
                    case "C44": hit = s.wallet > 0; break;
                    // v0.9
                    case "C54": case "C55": case "C56": case "C57": case "C58": case "C59": case "C60": case "C61": case "C62": case "C63": case "C64": case "C94": case "C100": case "C101": hit = true; break;
                    case "C65": hit = n == s.capacity; break;
                    case "C66": case "C67": hit = pips.Any(v => p.pips.Contains(v)); break;
                    case "C68": case "C102": hit = s.submissionsRemainingBefore == 1; break;
                    case "C69": hit = submittedRed > 0; break;
                    case "C70": case "C72": hit = pool.Length > 0; break;
                    case "C71": hit = pool.Any(d => d.currentPip == p.pip); break;
                    case "C73": hit = submittedRed > 0 && pool.Any(d => d.currentColor == "red"); break;
                    case "C74": hit = pool.Length >= n; break;
                    case "C75": hit = s.rollsUsed == p.maxRollsUsed; break;
                    case "C76": hit = s.rollsUsed >= 2; break;
                    case "C77": hit = s.rollsRemaining == 0; break;
                    case "C80": hit = sumPairs > 0; break;
                    case "C81": hit = primary == "scatter"; break;
                    case "C82": case "C86": hit = matched.Any(m => p.handIds.Contains(m)); break;
                    case "C83": hit = hasStraight; break;
                    case "C84": case "C85": case "C87": hit = maxSame >= p.minSamePip; break;
                    case "C88": hit = n >= p.minDice && counts.Count == n; break;
                    case "C89": hit = s.dice.Any(d => d.isGold); break;
                    // v1.1.1 C91: at least minDice dice, every neighbour differs and the direction flips at every step.
                    case "C91": hit = n >= p.minDice && Enumerable.Range(1, n - 1).All(i => pips[i] != pips[i - 1]) && Enumerable.Range(2, n - 2).All(i => (pips[i] > pips[i - 1]) != (pips[i - 1] > pips[i - 2])); break;
                    case "C92": hit = s.dice.Any(DieFacts.MonoColor); break;
                    case "C93": hit = s.dice.Any(DieFacts.MonoPip); break;
                    case "C103": hit = s.permanentDiceCount <= p.maxOwnedPermanentDice; break;
                    // v1.0
                    case "C108": case "C117": case "C118": case "C119": case "C122": case "C123": case "C124": case "C125": case "C129": case "C132": case "C134": case "C141": hit = true; break;
                    case "C104": hit = n >= p.minDice && Enumerable.Range(1, n - 1).All(i => colors[i] != colors[i - 1]); break;
                    case "C105": hit = redN > 0 && redN < n && colors.Take(redN).All(c => c == "red"); break;
                    case "C106": hit = n >= p.minDice && Enumerable.Range(0, n).All(i => pips[i] == pips[n - 1 - i]); break;
                    case "C107": hit = Enumerable.Range(1, n - 1).Any(i => pips[i] == pips[i - 1] + 1); break;
                    case "C109": hit = n % 2 == 1 && n >= p.minDice; break;
                    case "C126": case "C147": hit = pool.Length > 0; break;
                    case "C127": hit = n >= p.minDice; break;
                    case "C130": hit = s.stageKind == "boss"; break;
                    case "C135": hit = s.inventoryCount > 0; break;
                    // v1.1.1 C137: the star pip of the real owner (a mimic reads its target's); 0 = no pip drawn yet.
                    case "C137": hit = source.state.starPip >= 1 && pips.Any(v => v == source.state.starPip); break;
                    case "C138": hit = goldBudget >= p.goldCost; if (hit) { goldBudget -= p.goldCost; goldSpent = checked(goldSpent + p.goldCost); } break;
                    case "C139": hit = s.wallet >= p.goldPerStep; break;
                    case "C142": hit = hasStraight; break;
                    case "C143": hit = counts.Values.Any(c => c == p.requiredDice); break;
                    case "C144": hit = pips.All(v => v % 2 == 0); break;
                    case "C146": hit = submittedRed > 0; break;
                    case "C148": hit = s.rollsRemaining > 0; break;
                    case "C149": hit = n == p.requiredDice; break;
                    case "C150": hit = s.submissionOrdinalBefore >= 1; break;
                    case "C151": hit = emptySlots > 0; break;
                    case "C152": hit = commons > 0; break;
                    case "C153": hit = s.submissionOrdinalBefore == p.submissionOrdinal; break;
                    // v1.1.1
                    case "C155": hit = Count(counts, 4) >= p.minFours; break;
                    case "C156": hit = stageScoreBefore * p.targetDivisor < stageTarget; break;
                    default: hit = false; break;
                }
                if (hit) triggered.Add(source);
            }
            Func<string, int> hits = id => triggered.Count(t => t.abilityId == id);
            var contributions = Enumerable.Range(0, n).Select(i => ScoreExplanation.DescribeDie(s, i, pips, colors[i], cfg, sources, pipsOnly, sealedDice[i]).contribution).ToArray();
            // v0.9 retriggers: each entry adds `count` extra copies of one die's contribution.
            var retriggerEntries = new List<RetriggerEntry>(); var retriggers = new int[n];
            foreach (var t in triggered)
            {
                var p = cfg.Controller(t.abilityId).parameters; int[] targets;
                switch (t.abilityId)
                {
                    case "C64": targets = new[] { p.index }; break;
                    case "C65": targets = new[] { n - 1 }; break;
                    case "C66": case "C67": targets = Enumerable.Range(0, n).Where(i => p.pips.Contains(pips[i])).ToArray(); break;
                    case "C68": targets = Enumerable.Range(0, n).ToArray(); break;
                    case "C69": targets = redMembers; break;
                    case "C109": targets = new[] { (n - 1) / 2 }; break;
                    case "C143": targets = Enumerable.Range(0, n).Where(i => counts[pips[i]] == p.requiredDice).ToArray(); break;
                    default: continue;
                }
                foreach (int i in targets) { retriggers[i] = checked(retriggers[i] + p.retriggers); retriggerEntries.Add(new RetriggerEntry { emitterId = t.emitterId, abilityId = t.abilityId, stateOwnerId = t.stateOwnerId, dieIndex = i, count = p.retriggers }); }
            }
            BigInteger pipSum = Enumerable.Range(0, n).Aggregate(BigInteger.Zero, (a, i) => a + (BigInteger)contributions[i] * (1 + retriggers[i]));
            // v0.9 whole-hand base adds, in controller order.
            var controllerAdds = new List<ControllerBaseAdd>(); BigInteger controllerAddTotal = 0;
            foreach (var t in triggered)
            {
                var p = cfg.Controller(t.abilityId).parameters; BigInteger amount;
                switch (t.abilityId)
                {
                    case "C55": case "C59": case "C62": amount = growthOf(t); break;
                    case "C70": amount = (BigInteger)pool.Length * p.baseAddPerPoolDie; break;
                    case "C72": amount = (BigInteger)pool.Sum(d => d.currentPip) * p.poolPipSumMultiplier; break;
                    case "C76": amount = (BigInteger)(s.rollsUsed - 1) * p.baseAddPerExtraRoll; break;
                    case "C80": amount = (BigInteger)sumPairs * p.baseAddPerPair; break;
                    case "C108": amount = (BigInteger)pips[p.index] * p.baseAddPerPip; break;
                    case "C135": amount = (BigInteger)s.inventoryCount * p.baseAddPerItem; break;
                    default: continue;
                }
                if (amount == 0 || pipsOnly) continue;
                controllerAdds.Add(new ControllerBaseAdd { emitterId = t.emitterId, abilityId = t.abilityId, stateOwnerId = t.stateOwnerId, amount = ExactScoreMath.SaveDecimal(amount) }); controllerAddTotal += amount;
            }
            int handAdd = checked(triggered.Where(t => t.abilityId == "C84" || t.abilityId == "C85" || t.abilityId == "C142").Sum(t => cfg.Controller(t.abilityId).parameters.handMultAdd100));
            hand = Math.Max(100, checked(group + straight + handAdd));
            int handBeforeModifier = hand;
            if (fx("NoRepeatHand") && primary != "scatter" && (s.stageHandsUsed ?? Array.Empty<string>()).Contains(primary)) hand = 100;
            if (fx("HandHalf")) hand = Math.Max(100, hand / 2);
            int bonus = checked(cfg.bonusPerUnusedRoll * s.rollsRemaining);
            int pairAdd = pipsOnly ? 0 : checked(hits("C27") * pairKinds * cfg.Controller("C27").parameters.baseAddPerDistinctPairedPip);
            BigInteger walletAdd = pipsOnly ? 0 : (BigInteger)s.wallet * hits("C44") * cfg.Controller("C44").parameters.baseAddPerFrozenGold;
            int emptyAdd = pipsOnly ? 0 : checked(hits("C48") * (s.capacity - n) * cfg.Controller("C48").parameters.baseAddPerEmptySubmitSlot);
            int steadyAdd = pipsOnly ? 0 : checked(hits("C50") * cfg.Controller("C50").parameters.baseAdd);
            int baseCount = silence ? 0 : triggered.Count(t => t.enchantmentId == ControllerRules.BaseEnchantmentId);
            int enchantAdd = pipsOnly ? 0 : checked(baseCount * cfg.enchantRules.baseAdd);
            BigInteger baseTotal = pipSum + bonus + pairAdd + walletAdd + emptyAdd + steadyAdd + controllerAddTotal + enchantAdd;
            int redMult = fx("NoRed") ? 100 : cfg.redRewards.Single(r => r.count == Math.Min(redCount, cfg.debug.redGroupCapForExperiments)).factor100;
            var factors = new List<MultiplierFactor>(); BigInteger xnum = 1, xden = 1;
            var choices = (s.resolvedRandomChoices ?? Array.Empty<RandomMultiplierChoice>()).ToDictionary(c => c.ownerInstanceId, c => c.units2, StringComparer.Ordinal);
            // v1.1.1 additive zone, in the fixed order: colour, each submitted gold face, card abilities in card order,
            // then the score enchantment of each triggered source in card order. Zero amounts are not listed.
            var additive = new List<AdditiveEntry>(); BigInteger additiveTotal = 0;
            Action<string, string, AbilitySource, int, BigInteger> addBonus = (kind, abilityId, from, dieIndex, amount) =>
            {
                if (amount <= 0) return;
                additive.Add(new AdditiveEntry { kind = kind, abilityId = abilityId, emitterId = from?.emitterId, stateOwnerId = from?.stateOwnerId, dieIndex = dieIndex, amount100 = checked((long)amount) }); additiveTotal += amount;
            };
            addBonus("color", colorId, null, -1, colorAdd);
            // A gold face counts when it is submitted face up, also on a sealed die or under PipsOnly.
            for (int i = 0; i < n; i++) if (s.dice[i].isGold) addBonus("gold", "U06", null, i, cfg.goldAdd100);
            foreach (var t in triggered)
            {
                var p = cfg.Controller(t.abilityId).parameters; BigInteger amount;
                switch (t.abilityId)
                {
                    case "C19": amount = minPip == 1 ? p.minOneAdd100 : minPip == 2 ? p.minTwoAdd100 : 0; break;
                    case "C47": amount = (BigInteger)instances.Count(c => c.alive) * p.additivePerController100; break;
                    case "C49": amount = (BigInteger)Count(counts, 5) * p.additivePerFive100; break;
                    case "C63": amount = (BigInteger)s.permanentDiceCount * p.additivePerPermanentDie100; break;
                    case "C137": amount = (BigInteger)pips.Count(v => v == t.state.starPip) * p.additivePerMatch100; break;
                    case "C139": amount = (BigInteger)(s.wallet / p.goldPerStep) * p.additivePerGoldStep100; break;
                    // C141 keeps its count of cleared stages in `growth`; GrowthHalf does not touch the countdown.
                    case "C141": amount = p.initialAdditive100 - (BigInteger)t.state.growth * p.decayPerClear100; break;
                    case "C147": amount = (BigInteger)pool.Length * p.additivePerPoolDie100; break;
                    case "C148": amount = (BigInteger)s.rollsRemaining * p.additivePerUnusedRoll100; break;
                    case "C150": amount = (BigInteger)s.submissionOrdinalBefore * p.additivePerPreviousSubmit100; break;
                    case "C151": amount = (BigInteger)emptySlots * p.additivePerEmptyOrdinarySlot100; break;
                    case "C152": amount = (BigInteger)commons * p.additivePerCommon100; break;
                    default: amount = ControllerRules.IsGrowthAdditive(t.abilityId) ? growthOf(t) : p.additiveBonus100; break;
                }
                addBonus("controller", t.abilityId, t, -1, amount);
            }
            int enchantAddCount = 0;
            if (!silence) foreach (var t in triggered.Where(x => x.enchantmentId == ControllerRules.ScoreEnchantmentId)) { addBonus("enchantment", ControllerRules.ScoreEnchantmentId, t, -1, cfg.enchantRules.scoreAdd100); enchantAddCount++; }
            // Controller-side product M: every factor is exact, in card order.
            foreach (var t in triggered)
            {
                BigInteger num = 1, den = 1; bool multiply = true; var q = cfg.Controller(t.abilityId).parameters;
                switch (t.abilityId)
                {
                    // v1.1.1 C49: with a 5 it only adds; without one every triggered source halves the score.
                    case "C49": if (Count(counts, 5) > 0) multiply = false; else { num = q.noFiveMultiplier100; den = 100; } break;
                    case "C53": if (!choices.TryGetValue(t.stateOwnerId, out int units)) throw new ArgumentException("C53 outcome must be frozen before Evaluate; use Preview for ranges."); num = units; den = 2; break;
                    case "C71": { int k = pool.Count(d => d.currentPip == q.pip); num = BigInteger.Pow(q.stepNumerator, k); den = BigInteger.Pow(q.stepDenominator, k); break; }
                    case "C87": { int k = maxSame - q.minSamePip + 1; num = BigInteger.Pow(q.stepNumerator, k); den = BigInteger.Pow(q.stepDenominator, k); break; }
                    case "C89": { int k = s.dice.Count(d => d.isGold); num = BigInteger.Pow(q.stepNumerator, k); den = BigInteger.Pow(q.stepDenominator, k); break; }
                    case "C103": num = s.permanentDiceCount <= q.strictMaxOwnedPermanentDice ? q.strictScoreMultiplier100 : q.scoreMultiplier100; den = 100; break;
                    case "C155": num = Count(counts, 1) >= q.minOnes ? q.enhancedScoreMultiplier100 : q.scoreMultiplier100; den = 100; break;
                    default:
                        if (ControllerRules.IsGrowthMultiplier(t.abilityId)) { num = growthOf(t); den = 100; }
                        else if (q.scoreMultiplier100 > 0) { num = q.scoreMultiplier100; den = 100; }
                        else multiply = false;
                        break;
                }
                if (!multiply) continue;
                var gcd = BigInteger.GreatestCommonDivisor(num, den); num /= gcd; den /= gcd;
                factors.Add(new MultiplierFactor { emitterId = t.emitterId, abilityId = t.abilityId, stateOwnerId = t.stateOwnerId, numerator = ExactScoreMath.SaveDecimal(num), denominator = ExactScoreMath.SaveDecimal(den) });
                xnum *= num; xden *= den; gcd = BigInteger.GreatestCommonDivisor(xnum, xden); xnum /= gcd; xden /= gcd;
            }
            // Growth of real instances caused by this submission, applied by the session after the score is recorded:
            // at most one entry per real, effective instance, in card order. A controller disabled for this stage has
            // no ability at all, so it plans no state change either. delta is in the unit of the card.
            var disabledNow = new HashSet<string>(s.disabledControllerIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            var growthChanges = new List<GrowthChange>();
            foreach (var c in instances.Where(x => x.alive && !disabledNow.Contains(x.instanceId)))
            {
                int growthEvent = ControllerRules.HasGrowth(c.definitionId) ? cfg.Controller(c.definitionId).parameters.growthEvent : 0;
                switch (c.definitionId)
                {
                    // v1.1.1 C54: same primary hand as the previous valid submission grows it; another hand halves what
                    // it has grown; the very first submission of a run has nothing to compare with and plans nothing.
                    case "C54":
                        if (string.IsNullOrEmpty(s.previousPrimaryHandId)) break;
                        growthChanges.Add(primary == s.previousPrimaryHandId ? new GrowthChange { ownerInstanceId = c.instanceId, abilityId = "C54", delta = growthEvent } : new GrowthChange { ownerInstanceId = c.instanceId, abilityId = "C54", halve = true }); break;
                    case "C55": growthChanges.Add(new GrowthChange { ownerInstanceId = c.instanceId, abilityId = "C55", delta = growthEvent }); break;
                    case "C56": if (redN == 0) growthChanges.Add(new GrowthChange { ownerInstanceId = c.instanceId, abilityId = "C56", delta = growthEvent }); break;
                    case "C57": if (redCount >= cfg.Controller("C57").parameters.minRedGroup) growthChanges.Add(new GrowthChange { ownerInstanceId = c.instanceId, abilityId = "C57", delta = growthEvent }); break;
                }
            }
            // v1.0 factors outside the controller chain: the item multiplier (I09) and the boss half-score effects.
            var globalFactors = new List<MultiplierFactor>(); BigInteger gnum = 1, gden = 1;
            if (s.scoreItemMultiplier > 1) { globalFactors.Add(new MultiplierFactor { emitterId = "ITEM", abilityId = "I09", numerator = ExactScoreMath.SaveDecimal(s.scoreItemMultiplier), denominator = "1" }); gnum *= s.scoreItemMultiplier; }
            if ((fx("FirstHalf") && s.submissionOrdinalBefore == 0) || (fx("LaterHalf") && s.submissionOrdinalBefore >= 1)) { globalFactors.Add(new MultiplierFactor { emitterId = "BOSS", abilityId = modifier.id, numerator = "1", denominator = "2" }); gden *= 2; }
            // Permanent face changes, per triggered source in controller order, computed from the frozen faces only.
            // v1.1.1 section 6.1: every entry names the real owner, who earns the cultivation count if the face changes.
            var faceChanges = new List<FaceChange>(); int[] firstAndLast = n == 1 ? new[] { 0 } : new[] { 0, n - 1 };
            foreach (var t in triggered)
            {
                var p = cfg.Controller(t.abilityId).parameters;
                Action<string, string, string, int, string> change = (dieId, faceId, kind, amount, text) => faceChanges.Add(new FaceChange { sourceId = t.abilityId, emitterId = t.emitterId, stateOwnerId = t.stateOwnerId, dieId = dieId, faceId = faceId, kind = kind, amount = amount, text = text });
                switch (t.abilityId)
                {
                    case "C117": foreach (int i in firstAndLast) if (DieFacts.Face(s.dice[i]) != null) change(s.dice[i].dieId, s.dice[i].faceId, "gold", 1, null); break;
                    case "C118": foreach (var die in s.dice) { var face = DieFacts.Face(die); if (face != null) change(die.dieId, cfg.Opposite(die.faceId), "color", 0, face.baseColor); } break;
                    case "C119":
                        foreach (int i in firstAndLast)
                        {
                            var die = s.dice[i]; var face = DieFacts.Face(die); if (face == null) continue;
                            foreach (var other in die.permanentFacesCopy.Where(f => f.faceId != die.faceId && f.permanentPip != face.permanentPip).OrderBy(f => f.faceId, StringComparer.Ordinal).Take(p.facesPerDie)) change(die.dieId, other.faceId, "pipSet", face.permanentPip, null);
                        }
                        break;
                    case "C122": foreach (var die in s.dice) if (DieFacts.Face(die) != null) change(die.dieId, die.faceId, "pipSet", p.pip, null); break;
                    // C123 chooses by the frozen physical pip: below the middle band +lowPipDelta, above it highPipDelta.
                    case "C123":
                        foreach (var die in s.dice)
                        {
                            var face = DieFacts.Face(die); if (face == null) continue;
                            if (face.permanentPip < p.pips[0]) change(die.dieId, die.faceId, "pipDelta", p.lowPipDelta, null);
                            else if (face.permanentPip > p.pips[p.pips.Length - 1]) change(die.dieId, die.faceId, "pipDelta", p.highPipDelta, null);
                        }
                        break;
                    case "C124": foreach (var die in s.dice) if (DieFacts.Face(die) != null) change(die.dieId, die.faceId, "weightDelta", p.delta, null); break;
                    case "C126": foreach (var d in pool) change(d.dieId, d.faceId, "pipDelta", p.delta, null); break;
                    case "C127":
                        {
                            var source = DieFacts.Face(s.dice[p.sourceIndex]); var target = s.dice[n - 1];
                            if (source == null || DieFacts.Face(target) == null) break;
                            change(target.dieId, target.faceId, "pipSet", source.permanentPip, null); change(target.dieId, target.faceId, "color", 0, source.baseColor);
                            change(target.dieId, target.faceId, "texture", 0, source.texture); change(target.dieId, target.faceId, "gold", source.isGold ? 1 : 0, null); break;
                        }
                }
            }
            // Sources behind the older rewrites, in the order those rewrites run: C07, C26, C36/C37, C15, C38.
            Func<AbilitySource, CultivationSourceBinding> bind = t => new CultivationSourceBinding { abilityId = t.abilityId, emitterId = t.emitterId, stateOwnerId = t.stateOwnerId };
            Func<string, IEnumerable<CultivationSourceBinding>> firstOf = id => triggered.Where(t => t.abilityId == id).Take(1).Select(bind);
            var cultivationSources = firstOf("C07").Concat(firstOf("C26")).Concat(triggered.Where(t => t.abilityId == "C36" || t.abilityId == "C37").Select(bind)).Concat(firstOf("C15")).Concat(firstOf("C38")).ToArray();
            // C43 clamps by the real level: a hand that is really at the cap is not upgraded even when it scored at level 0.
            var realLevels = (s.handLevels ?? Array.Empty<HandLevel>()).ToDictionary(h => h.handId, h => h.level, StringComparer.Ordinal);
            int requested = checked(hits("C43") * cfg.Controller("C43").parameters.levelAddPerSource);
            var plan = new AfterScorePlan
            {
                earnedGold = checked(hits("C01") * cfg.Controller("C01").parameters.goldPerValidSubmission),
                copyFirstToSecond = hits("C07") > 0,
                redSpreadIndices = hits("C15") > 0 ? spread : Array.Empty<int>(),
                dyeAllFacesRed = hits("C26") > 0,
                growthNetDelta = checked(hits("C36") * cfg.Controller("C36").parameters.delta + hits("C37") * cfg.Controller("C37").parameters.delta),
                growthIndices = hits("C36") + hits("C37") > 0 ? cfg.Controller(hits("C36") > 0 ? "C36" : "C37").parameters.indices.Where(i => i < n).ToArray() : Array.Empty<int>(),
                blueSpreadIndices = hits("C38") > 0 ? (n == 1 ? new[] { 0 } : new[] { 0, n - 1 }) : Array.Empty<int>(),
                upgradeHandId = requested > 0 ? primary : null,
                upgradeRequested = requested,
                upgradeApplied = requested > 0 ? Math.Max(0, Math.Min(requested, (s.debug ? cfg.debug.maxHandLevel : cfg.maxHandLevel) - Level(realLevels, primary))) : 0,
                nextPreviousPrimary = primary,
                nextColorOwnerIds = instances.Where(c => c.alive && !disabledNow.Contains(c.instanceId) && c.definitionId == "C45").Select(c => c.instanceId).ToArray(),
                randomFactorOwnerIds = instances.Where(c => c.alive && !disabledNow.Contains(c.instanceId) && c.definitionId == "C53").Select(c => c.instanceId).ToArray(),
                cannonRiskEmitterIds = triggered.Where(t => t.abilityId == "C42").Select(t => t.emitterId).ToArray(),
                growthChanges = growthChanges.ToArray(),
                faceChanges = faceChanges.ToArray(),
                goldSpent = goldSpent,
                glassRiskDieIds = s.dice.Where(d => d.isGlass).Select(d => d.dieId).ToArray(),
                cultivationSourceBindings = cultivationSources
            };
            // B x H x R x (1 + A) x M x G as one reduced fraction; the only rounding is the final floor.
            BigInteger numerator = baseTotal * hand * redMult * (100 + additiveTotal) * xnum * gnum, denominator = (BigInteger)1000000 * xden * gden;
            var reduce = BigInteger.GreatestCommonDivisor(numerator, denominator); numerator /= reduce; denominator /= reduce;
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
                colorAdd100 = colorAdd,
                additiveEntries = additive.ToArray(),
                additiveTotal100 = checked((long)additiveTotal),
                enchantAdditiveCount = enchantAddCount,
                pairKinds = pairKinds,
                pairBaseAdd = pairAdd,
                walletBaseAdd = ExactScoreMath.SaveDecimal(walletAdd),
                emptySlotBaseAdd = emptyAdd,
                steadyBaseAdd = steadyAdd,
                enchantBaseAdd = enchantAdd,
                baseTotal = ExactScoreMath.SaveDecimal(baseTotal),
                enchantBaseCount = baseCount,
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
                pipRetriggers = retriggers,
                retriggerEntries = retriggerEntries.ToArray(),
                handMultAdd100 = handAdd,
                redPoolCount = redPool,
                controllerBaseAdds = controllerAdds.ToArray(),
                sealedDice = sealedDice,
                globalFactors = globalFactors.ToArray(),
                handMultBeforeModifier100 = handBeforeModifier,
                redBonusCount = redBonus,
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
            if (s.rollsUsed < 0 || s.submissionsRemainingBefore < 0 || s.permanentDiceCount < 0) throw new ArgumentException("Invalid frozen hand context.");
            if (!string.IsNullOrEmpty(s.stageScoreBefore)) ExactScoreMath.Parse(s.stageScoreBefore);
            if (!string.IsNullOrEmpty(s.stageTarget) && ExactScoreMath.Parse(s.stageTarget) <= 0) throw new ArgumentException("Invalid frozen stage target.");
            var pool = s.poolDice ?? Array.Empty<PoolDie>();
            if (pool.Length > (s.debug ? cfg.debug.maxDice : cfg.maxActiveDice) || pool.Any(d => d == null || string.IsNullOrEmpty(d.dieId) || d.currentPip < 1 || d.currentPip > 6 || (d.currentColor != "red" && d.currentColor != "blue"))
                || pool.Select(d => d.dieId).Concat(s.dice.Select(d => d.dieId)).Distinct(StringComparer.Ordinal).Count() != pool.Length + s.dice.Length) throw new ArgumentException("Invalid frozen pool dice.");
            if (!string.IsNullOrEmpty(s.modifierId)) cfg.Modifier(s.modifierId);
            if (!string.IsNullOrEmpty(s.stageKind) && s.stageKind != "normal" && s.stageKind != "pressure" && s.stageKind != "boss") throw new ArgumentException("Invalid stage kind.");
            if (s.inventoryCount < 0 || s.ordinaryControllerSlots < 0 || (s.scoreItemMultiplier != 0 && s.scoreItemMultiplier != 2) || (s.redWave && s.blueWave)) throw new ArgumentException("Invalid frozen item context.");
            var controllerIds = ControllerRules.Resolve(s).Select(c => c.instanceId).ToArray(); var disabledIds = s.disabledControllerIds ?? Array.Empty<string>();
            if (disabledIds.Any(id => !controllerIds.Contains(id)) || disabledIds.Distinct(StringComparer.Ordinal).Count() != disabledIds.Length) throw new ArgumentException("Invalid disabled controllers.");
            foreach (string used in s.stageHandsUsed ?? Array.Empty<string>()) cfg.Hand(used);
            var owners = ControllerRules.Resolve(s).Where(c => c.alive && c.definitionId == "C53").Select(c => c.instanceId).ToArray();
            if (choices.Any(c => !owners.Contains(c.ownerInstanceId))) throw new ArgumentException("Random choice owner is not a live C53.");
        }
        private static void ValidateLevels(HandLevel[] entries, Func<string, int> max, int debugMax)
        {
            var levels = entries ?? Array.Empty<HandLevel>(); if (levels.Any(h => h == null) || levels.Select(h => h.handId).Distinct(StringComparer.Ordinal).Count() != levels.Length) throw new ArgumentException("Duplicate/null levels.");
            foreach (var h in levels) { int bound = max(h.handId); if (h.level < 0 || h.level > (debugMax > 0 ? debugMax : bound)) throw new ArgumentException("Level out of range."); }
        }
    }
    /// <summary>v0.9: facts about a submitted die derived from its frozen six faces. A snapshot without the face copy never qualifies.</summary>
    public static class DieFacts
    {
        private static bool Complete(SubmissionDie d) => d.permanentFacesCopy != null && d.permanentFacesCopy.Length == 6 && d.permanentFacesCopy.All(f => f != null);
        /// <summary>v1.0: the frozen physical face that is up, or null when the snapshot carries no six-face copy.</summary>
        public static FaceState Face(SubmissionDie d) => Complete(d) ? d.permanentFacesCopy.FirstOrDefault(f => f.faceId == d.faceId) : null;
        public static string Texture(SubmissionDie d) => Complete(d) ? d.permanentFacesCopy.FirstOrDefault(f => f.faceId == d.faceId)?.texture ?? "none" : "none";
        public static bool MonoColor(SubmissionDie d) => Complete(d) && d.permanentFacesCopy.All(f => f.baseColor == d.permanentFacesCopy[0].baseColor);
        public static bool MonoPip(SubmissionDie d) => Complete(d) && d.permanentFacesCopy.All(f => f.permanentPip == d.permanentFacesCopy[0].permanentPip);
        public static int OppositePip(SubmissionDie d, GameConfig cfg)
        {
            if (!Complete(d)) return 0; string opposite = cfg.Opposite(d.faceId);
            return d.permanentFacesCopy.FirstOrDefault(f => f.faceId == opposite)?.permanentPip ?? 0;
        }
    }
    public static class HandEvaluator
    {
        public static int Multiplier100(HandDefinition hand, int level, int maxLevel = -1)
        {
            if (hand == null) throw new ArgumentNullException(nameof(hand)); if (level < 0 || level > (maxLevel < 0 ? hand.maxLevel : maxLevel)) throw new ArgumentOutOfRangeException(nameof(level)); return checked(hand.baseMult100 + hand.incrementPerLevel100 * level);
        }
        /// <summary>v1.1.1: the additive bonus of a colour in hundredths (all blue 25 + 50 per level, all red 50 + 50 per level).</summary>
        public static int ColorAdd100(ColorDefinition color, int level, int maxLevel = -1)
        {
            if (color == null) throw new ArgumentNullException(nameof(color)); if (level < 0 || level > (maxLevel < 0 ? color.maxLevel : maxLevel)) throw new ArgumentOutOfRangeException(nameof(level)); return checked(color.baseAdd100 + color.addPerLevel100 * level);
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
