using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace DiceCraft.Core
{
    /// <summary>Frozen exact arithmetic and event plan. No random draw, mutation, storage or session dependency.</summary>
    public static class ScoreCalculator
    {
        private static readonly HashSet<string> FixedMultipliers = new HashSet<string>(new[] { "C02", "C06", "C08", "C09", "C11", "C14", "C18", "C28", "C30", "C31", "C32", "C33", "C40", "C42", "C46", "C74", "C75", "C77", "C81", "C82", "C83", "C86", "C88", "C101", "C102", "C104", "C105", "C106", "C130", "C138", "C144", "C149", "C153" }, StringComparer.Ordinal);
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
            bool zeroLevels = fx("ZeroLevels"), silence = fx("EnchantSilence"), pipsOnly = fx("PipsOnly"), growthZero = fx("GrowthZero");
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
            int color = colorId == null ? 100 : HandEvaluator.ColorMultiplier100(cfg.Color(colorId), Level(colorLevels, colorId), s.debug ? cfg.debug.maxColorLevel : cfg.maxColorLevel);
            if ((colorId == "all_red" && fx("NoRed")) || (colorId == "all_blue" && fx("NoBlue"))) color = 100;
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
            int emptySlots = Math.Max(0, s.ordinaryControllerSlots - ControllerRules.OrdinaryUsed(instances));
            int commons = instances.Count(c => c.alive && cfg.Controller(c.definitionId).rarity == cfg.Controller("C152").parameters.rarity);
            long goldBudget = s.wallet; int goldSpent = 0;
            Func<AbilitySource, long> growthOf = t => growthZero ? 0 : t.state.growth;
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
                    case "C54": case "C55": case "C56": case "C57": case "C58": case "C59": case "C60": case "C61": case "C62": case "C63": case "C64": case "C90": case "C94": case "C100": case "C101": hit = true; break;
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
                    case "C91": hit = s.dice.Any(d => p.textures.Contains(DieFacts.Texture(d))); break;
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
                    case "C127": hit = s.submissionOrdinalBefore == p.submissionOrdinal && n >= p.minDice; break;
                    case "C130": hit = s.stageKind == "boss"; break;
                    case "C135": hit = s.inventoryCount > 0; break;
                    case "C137": hit = s.dice.Any(d => d.lifetime == "stage_temporary"); break;
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
            int baseCount = silence ? 0 : triggered.Count(t => t.enchantmentId == "base10"), multCount = silence ? 0 : triggered.Count(t => t.enchantmentId == "mult2");
            int enchantAdd = pipsOnly ? 0 : checked(baseCount * cfg.enchantRules.baseAdd);
            BigInteger baseTotal = pipSum + bonus + pairAdd + walletAdd + emptyAdd + steadyAdd + controllerAddTotal + enchantAdd;
            int redMult = fx("NoRed") ? 100 : cfg.redRewards.Single(r => r.count == Math.Min(redCount, cfg.debug.redGroupCapForExperiments)).factor100;
            var factors = new List<MultiplierFactor>(); BigInteger xnum = 1, xden = 1;
            var choices = (s.resolvedRandomChoices ?? Array.Empty<RandomMultiplierChoice>()).ToDictionary(c => c.ownerInstanceId, c => c.units2, StringComparer.Ordinal);
            foreach (var t in triggered)
            {
                BigInteger num = 1, den = 1; bool multiply = true;
                if (FixedMultipliers.Contains(t.abilityId)) num = cfg.Controller(t.abilityId).parameters.scoreMultiplier;
                else switch (t.abilityId)
                    {
                        case "C47": num = Math.Max(2, checked(instances.Count(c => c.alive) * cfg.Controller("C47").parameters.perControllerUnits2)); den = 2; break;
                        case "C49": num = Count(counts, 5); break;
                        case "C51": num = t.state.turnoverUnits2; den = 2; break;
                        // v0.9
                        case "C54": case "C56": case "C57": case "C58": case "C60": case "C61": case "C90": case "C100": case "C129": case "C132": case "C134": num = 20 + (BigInteger)growthOf(t); den = 20; break;
                        // v1.0
                        case "C139": { var q = cfg.Controller("C139").parameters; num = 4 + (BigInteger)Math.Min(q.maxSteps, s.wallet / q.goldPerStep) * q.stepUnits4; den = 4; break; }
                        case "C141": { var q = cfg.Controller("C141").parameters; num = q.initialMultiplierUnits2 - (BigInteger)t.state.growth; den = 2; break; }
                        case "C147": num = 4 + (BigInteger)pool.Length * cfg.Controller("C147").parameters.stepUnits4; den = 4; break;
                        case "C148": num = 2 + (BigInteger)s.rollsRemaining * cfg.Controller("C148").parameters.perRollUnits2; den = 2; break;
                        case "C150": num = s.submissionOrdinalBefore + 1; break;
                        case "C151": num = 2 + (BigInteger)emptySlots * cfg.Controller("C151").parameters.perSlotUnits2; den = 2; break;
                        case "C152": num = 4 + (BigInteger)commons * cfg.Controller("C152").parameters.stepUnits4; den = 4; break;
                        case "C63": { var q = cfg.Controller("C63").parameters; num = Math.Max(10, checked(s.permanentDiceCount * q.perDieUnits10)); den = 10; break; }
                        case "C71": { var q = cfg.Controller("C71").parameters; int k = pool.Count(d => d.currentPip == q.pip); num = BigInteger.Pow(q.stepNumerator, k); den = BigInteger.Pow(q.stepDenominator, k); break; }
                        case "C87": { var q = cfg.Controller("C87").parameters; int k = maxSame - q.minSamePip + 1; num = BigInteger.Pow(q.stepNumerator, k); den = BigInteger.Pow(q.stepDenominator, k); break; }
                        case "C89": { var q = cfg.Controller("C89").parameters; int k = s.dice.Count(d => d.isGold); num = BigInteger.Pow(q.stepNumerator, k); den = BigInteger.Pow(q.stepDenominator, k); break; }
                        case "C103": { var q = cfg.Controller("C103").parameters; num = s.permanentDiceCount <= q.strictMaxOwnedPermanentDice ? q.strictScoreMultiplier : q.scoreMultiplier; break; }
                        case "C53": if (!choices.TryGetValue(t.stateOwnerId, out int units)) throw new ArgumentException("C53 outcome must be frozen before Evaluate; use Preview for ranges."); num = units; den = 2; break;
                        default: multiply = false; break;
                    }
                if (!multiply) continue;
                var gcd = BigInteger.GreatestCommonDivisor(num, den); num /= gcd; den /= gcd;
                factors.Add(new MultiplierFactor { emitterId = t.emitterId, abilityId = t.abilityId, stateOwnerId = t.stateOwnerId, numerator = ExactScoreMath.SaveDecimal(num), denominator = ExactScoreMath.SaveDecimal(den) });
                xnum *= num; xden *= den; gcd = BigInteger.GreatestCommonDivisor(xnum, xden); xnum /= gcd; xden /= gcd;
            }
            // v0.9 growth of real instances, applied by the session after this score is recorded.
            var growthChanges = new List<GrowthChange>();
            foreach (var c in instances.Where(x => x.alive))
            {
                switch (c.definitionId)
                {
                    case "C54": growthChanges.Add(!string.IsNullOrEmpty(s.previousPrimaryHandId) && primary == s.previousPrimaryHandId ? new GrowthChange { ownerInstanceId = c.instanceId, abilityId = "C54", delta = cfg.Controller("C54").parameters.growthPerEvent } : new GrowthChange { ownerInstanceId = c.instanceId, abilityId = "C54", reset = true }); break;
                    case "C55": growthChanges.Add(new GrowthChange { ownerInstanceId = c.instanceId, abilityId = "C55", delta = cfg.Controller("C55").parameters.growthPerEvent }); break;
                    case "C56": if (redN == 0) growthChanges.Add(new GrowthChange { ownerInstanceId = c.instanceId, abilityId = "C56", delta = cfg.Controller("C56").parameters.growthPerEvent }); break;
                    case "C57": if (redCount >= cfg.Controller("C57").parameters.minRedGroup) growthChanges.Add(new GrowthChange { ownerInstanceId = c.instanceId, abilityId = "C57", delta = cfg.Controller("C57").parameters.growthPerEvent }); break;
                }
            }
            // v1.0 factors outside the controller chain: the item multiplier (I09) and the boss half-score effects.
            var globalFactors = new List<MultiplierFactor>(); BigInteger gnum = 1, gden = 1;
            if (s.scoreItemMultiplier > 1) { globalFactors.Add(new MultiplierFactor { emitterId = "ITEM", abilityId = "I09", numerator = ExactScoreMath.SaveDecimal(s.scoreItemMultiplier), denominator = "1" }); gnum *= s.scoreItemMultiplier; }
            if ((fx("FirstHalf") && s.submissionOrdinalBefore == 0) || (fx("LaterHalf") && s.submissionOrdinalBefore >= 1)) { globalFactors.Add(new MultiplierFactor { emitterId = "BOSS", abilityId = modifier.id, numerator = "1", denominator = "2" }); gden *= 2; }
            // v1.0 permanent face changes, per triggered source in controller order, computed from the frozen faces only.
            var faceChanges = new List<FaceChange>();
            foreach (var t in triggered)
            {
                var p = cfg.Controller(t.abilityId).parameters;
                Action<SubmissionDie, string, string, int, string> change = (die, faceId, kind, amount, text) => faceChanges.Add(new FaceChange { sourceId = t.abilityId, emitterId = t.emitterId, dieId = die.dieId, faceId = faceId, kind = kind, amount = amount, text = text });
                switch (t.abilityId)
                {
                    case "C117": { var die = s.dice[n - 1]; if (DieFacts.Face(die) != null) change(die, die.faceId, "gold", 1, null); break; }
                    case "C118": foreach (var die in s.dice) { var face = DieFacts.Face(die); if (face != null) change(die, cfg.Opposite(die.faceId), "color", 0, face.baseColor); } break;
                    case "C119": { var die = s.dice[p.index]; var face = DieFacts.Face(die); var other = face == null ? null : die.permanentFacesCopy.OrderBy(f => f.faceId, StringComparer.Ordinal).FirstOrDefault(f => f.permanentPip != face.permanentPip); if (other != null) change(die, other.faceId, "pipSet", face.permanentPip, null); break; }
                    case "C122": foreach (var die in s.dice) { var face = DieFacts.Face(die); if (face != null && face.permanentPip != p.pip) change(die, die.faceId, "pipDelta", face.permanentPip < p.pip ? 1 : -1, null); } break;
                    case "C123": foreach (var die in s.dice) { var face = DieFacts.Face(die); if (face != null && (face.permanentPip < p.pips[0] || face.permanentPip > p.pips[1])) change(die, die.faceId, "pipDelta", face.permanentPip < p.pips[0] ? 1 : -1, null); } break;
                    case "C124": foreach (var die in s.dice) if (DieFacts.Face(die) != null) change(die, die.faceId, "weightDelta", p.delta, null); break;
                    case "C125": foreach (int i in n == 1 ? new[] { 0 } : new[] { 0, n - 1 }) { var face = DieFacts.Face(s.dice[i]); if (face != null && face.texture == "none") change(s.dice[i], s.dice[i].faceId, "texture", 0, p.textures[0]); } break;
                    case "C126": foreach (var d in pool) faceChanges.Add(new FaceChange { sourceId = t.abilityId, emitterId = t.emitterId, dieId = d.dieId, faceId = d.faceId, kind = "pipDelta", amount = p.delta }); break;
                    case "C127":
                        {
                            var source = DieFacts.Face(s.dice[p.sourceIndex]); var target = s.dice[n - 1];
                            if (source == null || DieFacts.Face(target) == null) break;
                            change(target, target.faceId, "pipSet", source.permanentPip, null); change(target, target.faceId, "color", 0, source.baseColor);
                            change(target, target.faceId, "texture", 0, source.texture); change(target, target.faceId, "gold", source.isGold ? 1 : 0, null); break;
                        }
                }
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
                cannonRiskEmitterIds = triggered.Where(t => t.abilityId == "C42").Select(t => t.emitterId).ToArray(),
                growthChanges = growthChanges.ToArray(),
                faceChanges = faceChanges.ToArray(),
                goldSpent = goldSpent
            };
            BigInteger numerator = baseTotal * hand * color * redMult * xnum * BigInteger.Pow(cfg.enchantRules.scoreMultiplier, multCount) * gnum, denominator = 1000000 * xden * gden;
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
