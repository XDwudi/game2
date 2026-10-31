using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DiceCraft.Core
{
    public sealed partial class GameConfig
    {
        public EnchantmentDefinition Enchantment(string id) => Required(enchantments, d => d.id, id);
        public ControllerDefinition Controller(string id) => Required(controllers, d => d.id, id);
        public HandDefinition Hand(string id) => Required(handDefinitions, d => d.id, id);
        public ColorDefinition Color(string id) => Required(colorDefinitions, d => d.id, id);
        public ConsumableDefinition Consumable(string id) => Required(consumables, d => d.id, id);
        public ServiceDefinition Service(string id) => Required(services, d => d.id, id);
        public DieUpgradeDefinition Upgrade(string id) => Required(diceUpgrades, d => d.id, id);
        private static T Required<T>(T[] entries, Func<T, string> key, string id)
        {
            if (entries == null) throw new InvalidOperationException("Configuration is not loaded.");
            foreach (T entry in entries) if (key(entry) == id) return entry;
            throw new ArgumentException("Unknown configured ID: " + id, nameof(id));
        }
        public static void ValidateJson(string json) => ConfigJsonContract.Validate(json);
        /// <summary>Validate original schema, then translate only the numeric C11 object keys for JsonUtility.</summary>
        public static string PrepareJsonForUnity(string json)
        {
            ValidateJson(json);
            return Regex.Replace(json, "(\"requiredCounts\"\\s*:\\s*\\{)([^{}]*)(\\})", m => m.Groups[1].Value + Regex.Replace(m.Groups[2].Value, "\"([23])\"(?=\\s*:)", x => "\"pip" + x.Groups[1].Value + "\"") + m.Groups[3].Value);
        }
        public void Validate()
        {
            Check(configVersion == "p0.6.0" && saveSchemaVersion == 4, "Unsupported config/save version.");
            Check(unityMajorMinor == "6000.6" && blenderMajorMinor == "5.2", "Toolchain.");
            Check(initialDiceCount == 5 && initialGold == 0 && maxPermanentDice == 8 && maxTemporaryDice == 3 && maxActiveDice == 10, "Initial/dice limits.");
            Check(initialSubmitCapacity == 5 && maxSubmitCapacity == 8 && !purchasedDiceChangeCapacity, "Independent submit capacity.");
            Check(totalRollsPerHand == 3 && submissionsPerStage == 3 && bonusPerUnusedRoll == 3, "Budgets.");
            Check(initialControllerSlots == 4 && maxControllerInstances == 10 && !slotlessConsumesBaseSlot, "Controller limits.");
            Check(initialConsumableSlots == 2 && maxConsumableSlots == 5 && minDiscountedItemPrice == 1 && maxItemDiscount == 3, "Consumable economy.");
            Check(baseControllerSellPrice == 1 && consumableSellPrice == 1 && baseStageReward > 0 && riskStageReward > baseStageReward && goldPerUnusedSubmission >= 0, "Economy.");
            Check(interestDivisor == 10 && maxInterestPrincipal == 50 && interestBalanceTiming == "finalize_before_rewards_after_all_submit_income", "Interest.");
            Check(minPipValue == 1 && maxPipValue == 6 && minStraightLength == 5 && maxHandLevel == 8 && maxColorLevel == 8, "Hand/pip limits.");
            Check(scoreStorage == "nonnegative_big_integer_decimal_string" && walletStorage == "checked_nonnegative_int64" && rounding == "single_final_floor", "Exact storage contract.");
            Check(probabilityUnits == 1000000 && probabilityRounding == "largest_remainder_face_id_tie" && allowZeroProbabilityAfterGroupTransfer, "Probability precision.");
            Check(smoothOppositeWeight100 > 0 && smoothOtherWeight100 > 0 && roughOppositeWeight100 > 0 && roughOtherWeight100 > 0 && temporaryPipBiasWeight100 > 0, "Probability weights.");
            Check(glassBreakPercent >= 0 && glassBreakPercent <= 100 && glassContributionMultiplier > 0 && goldContributionMultiplier > 0, "Glass/gold.");
            Check(diceDefinition != null && diceDefinition.id == "standard_d6" && diceDefinition.faces != null && diceDefinition.faces.Length == 6 && diceDefinition.faces.All(f => f != null), "Standard die.");
            ValidateFaces(DiceFactory.CreateStandard(this, 1).faces);
            Check(faceOpposites != null, "Opposites missing."); for (int i = 1; i <= 6; i++) { string id = "F" + i.ToString("D2"); Check(Opposite(id) != id && Opposite(Opposite(id)) == id, "Opposites."); }
            ValidateIds(redRewards, r => r.count.ToString(), Enumerable.Range(0, 9).Select(i => i.ToString()).ToArray());
            Check(redRewards.OrderBy(r => r.count).Select(r => r.factor100).SequenceEqual(new[] { 100, 100, 175, 250, 350, 500, 700, 1000, 1400 }), "Red rewards.");
            string[] hands = { "pair", "two_pair", "trips", "full_house", "three_pair", "quads", "two_trips", "five_kind", "four_pair", "six_kind", "seven_kind", "eight_kind", "straight" };
            ValidateIds(handDefinitions, h => h.id, hands);
            foreach (var h in handDefinitions) Check(!string.IsNullOrWhiteSpace(h.displayName) && h.baseMult100 > 0 && h.incrementPerLevel100 > 0 && h.baseUpgradePrice > 0 && h.maxLevel == maxHandLevel && h.minSubmittedDice > 0 && h.minSubmittedDice <= maxSubmitCapacity && h.requirePermanentDice >= 0 && h.requirePermanentDice <= maxPermanentDice && h.structuralPriority > 0, "Hand: " + h.id);
            Check(handDefinitions.Select(h => h.structuralPriority).Distinct().Count() == 13, "Unique structural priorities.");
            ValidateIds(colorDefinitions, c => c.id, new[] { "all_blue", "all_red" });
            foreach (var c in colorDefinitions) Check(c.baseMult100 >= 100 && c.incrementPerLevel100 > 0 && c.baseUpgradePrice > 0 && c.maxLevel == maxColorLevel && c.minSubmittedDice == 1 && !c.countsAsHand && c.multiplierZone == "independent_color", "Independent color.");
            Check(removedControllerIds != null && removedControllerIds.SequenceEqual(new[] { "C04", "C16", "C23" }), "Retired IDs.");
            ValidateIds(controllers, c => c.id, Ids("C", 53).Except(removedControllerIds).ToArray());
            ValidateIds(consumables, c => c.id, Ids("I", 7)); ValidateIds(services, s => s.id, Ids("O", 7)); ValidateIds(diceUpgrades, u => u.id, Ids("U", 7));
            Check(activeContentCount == 71 && controllers.Length + consumables.Length + services.Length + diceUpgrades.Length == activeContentCount, "Content count.");
            Check(shop != null && shop.controllerWeights != null, "Shop missing.");
            foreach (var c in controllers)
            {
                Check(!string.IsNullOrWhiteSpace(c.displayName) && !string.IsNullOrWhiteSpace(c.description) && !string.IsNullOrWhiteSpace(c.trigger) && c.parameters != null && c.buyPrice >= 2 && c.baseSellPrice == 1 && c.maxOwnedNormal == 1, "Controller: " + c.id);
                int w = c.rarity == "common" ? shop.controllerWeights.common : c.rarity == "uncommon" ? shop.controllerWeights.uncommon : c.rarity == "rare" ? shop.controllerWeights.rare : 0;
                Check(w > 0 && c.offerWeight == w, "Controller rarity.");
            }
            foreach (var c in consumables) Check(c.baseBuyPrice >= 1 && c.sellPrice == 1 && (c.window == "Selecting" || c.window == "HandReady" || c.window == "HandReadyOrSelecting"), "Consumable.");
            foreach (var s in services) Check(s.baseBuyPrice >= 1 && !string.IsNullOrWhiteSpace(s.priceRule), "Service.");
            foreach (var u in diceUpgrades) Check(u.buyPrice >= 1 && u.target == (u.id == "U05" ? "Die" : "Face"), "Die upgrade.");
            ValidateEnchantments(); ValidateParameters();
            ValidateIds(stages, s => s.stageId, Enumerable.Range(1, 24).Select(i => "STAGE_" + i.ToString("D2")).ToArray());
            foreach (var s in stages) Check(ExactScoreMath.Parse(s.normalTarget) > 0 && s.normalReward > 0 && s.shopAfterClear == (s.stageId != "STAGE_24") && (s.riskEnabled ? (ExactScoreMath.Parse(s.riskTarget) > ExactScoreMath.Parse(s.normalTarget) && s.riskReward > s.normalReward) : (string.IsNullOrEmpty(s.riskTarget) && s.riskReward == 0)), "Stage.");
            Check(stageFlow != null && stageFlow.latchQualified && !stageFlow.autoEndOnReached && stageFlow.autoEndWhenNoSubmissions && stageFlow.manualEndRequiresQualified && stageFlow.manualEndDiscardsUnsubmittedHand && stageFlow.overScoreGold == 0 && stageFlow.clearRewardOnlyAtFinalization && stageFlow.submissionIncomeAppliedWithinSubmit, "Stage flow.");
            Check(controllerOrder != null && controllerOrder.stageBindingsFrozen && controllerOrder.dragChangesOnlyOrder && controllerOrder.maxMimicResolveDepth == 32 && controllerOrder.onTargetLost == "no_base_effect_until_next_stage", "Controller binding.");
            Check(shop.initialControllerOffers == 3 && shop.maxControllerOffers == 6 && shop.initialConsumableOffers == 2 && shop.maxConsumableOffers == 5 && shop.upgradeOffers == 2 && shop.paidRefreshEnabled && shop.firstRefreshPrice == 3 && shop.refreshPriceStep == 2 && shop.refreshTarget == "controllers_only" && shop.freeRefreshConsumesFirstIndex && shop.replacementCredit == "actual_old_instance_sale_price" && shop.replacementCountsAsOneControllerSale && shop.maxOfferExpansionPurchases == 3, "Shop contracts.");
            Check(shop.discountCategories != null && new HashSet<string>(shop.discountCategories).SetEquals(new[] { "consumable", "service", "new_die" }) && shop.upgradePoolIncludes.SequenceEqual(new[] { "hand", "color" }), "Shop categories.");
            Check(generation != null && generation.newDieTemplateWeights != null && generation.newDieTemplateWeights.random > 0 && generation.newDieTemplateWeights.low > 0 && generation.newDieTemplateWeights.high > 0 && generation.newDieTemplateWeights.random + generation.newDieTemplateWeights.low + generation.newDieTemplateWeights.high == 100, "Generation.");
            ValidateRange(generation.pipRange); ValidateRange(generation.lowRange); ValidateRange(generation.highRange);
            Check(generation.newDieBasePrice == 3 && generation.maxNewDieBasePrice == 6 && generation.redFaceChancePercent >= 0 && generation.redFaceChancePercent <= 100, "Die prices.");
            Check(rng != null && rng.algorithm == "xorshift32_v1" && rng.diceSeedXor != rng.shopSeedXor && rng.diceSeedXor != rng.effectSeedXor && rng.shopSeedXor != rng.effectSeedXor && rng.zeroSeedReplacement == 0x9E3779B9u, "RNG.");
            Check(presentation != null && presentation.rollSeconds > 0 && presentation.fastRollSeconds > 0 && presentation.handbookHandRows == 14 && presentation.handbookColorRows == 2 && presentation.contentIconCount == 71 && presentation.enchantmentBadgeCount == 5, "Presentation counts.");
            Check(buildTargets != null && buildTargets.SequenceEqual(new[] { "Windows_x86_64", "macOS_Universal" }), "Build targets.");
            Check(display != null && display.initialMode == "borderless" && display.confirmationTimeoutSeconds == 15 && display.persistOnlyConfirmedSettings, "Display contract.");
            Check(debug != null && debug.unlockPhrase == "XD is god" && debug.caseSensitive && !debug.trimInput && debug.separateSaveSlot && debug.markRunAsDebugOnFirstMutation && debug.allowDuplicateControllerIds && debug.maxDice == 32 && debug.maxSubmitCapacity == 32 && debug.maxControllerInstances == 32 && debug.maxHandLevel == 100 && debug.maxColorLevel == 100 && debug.maxEditableWallet == 1000000000 && debug.maxEditableBudget == 999, "Debug limits.");
            ProbabilityCalculator.BuildUnits(DiceFactory.CreateStandard(this, 1).faces, false, false, this);
        }
        private void ValidateEnchantments()
        {
            string[] ids = { "none", "slotless", "base10", "mult2", "submit1", "roll1" }; ValidateIds(enchantments, e => e.id, ids);
            Check(enchantments.Select(e => e.id).SequenceEqual(ids) && enchantments.Select(e => e.weight).SequenceEqual(new[] { 50, 18, 10, 4, 9, 9 }) && enchantments.Select(e => e.buyPremium).SequenceEqual(new[] { 0, 3, 2, 5, 3, 2 }), "Enchantments.");
            Check(enchantRules != null && enchantRules.rawDrawOrder.SequenceEqual(ids) && enchantRules.nonSubmitControllerIds.SequenceEqual(new[] { "C05", "C21", "C22", "C34", "C35", "C52" }) && enchantRules.baseAdd == 10 && enchantRules.scoreMultiplier == 2 && enchantRules.submissionBonus == 1 && enchantRules.rollBonus == 1 && !enchantRules.copyTargetEnchantment && enchantRules.triggerCountPerSourcePerSubmitMax == 1, "Enchant policies.");
            var w = enchantRules.nonSubmitDrawWeights; Check(w != null && w.none == 100 && w.slotless == 50 && w.submit1 == 25 && w.roll1 == 25, "Dedicated enchant pool.");
            foreach (var c in controllers) Check(c.scoreEnchantEligible == !enchantRules.nonSubmitControllerIds.Contains(c.id), "Score enchant eligibility.");
        }
        private void ValidateParameters()
        {
            foreach (string id in new[] { "C02", "C06", "C08", "C09", "C11", "C14", "C18", "C28", "C30", "C31", "C32", "C33", "C40", "C42", "C46" }) Check(Controller(id).parameters.scoreMultiplier > 0, id + " multiplier.");
            Check(Controller("C11").parameters.requiredCounts != null && Controller("C11").parameters.requiredCounts.pip2 > 0 && Controller("C11").parameters.requiredCounts.pip3 > 0, "C11 required counts did not deserialize.");
            Check(Controller("C03").parameters.index == 0 && Controller("C12").parameters.oddToEvenAdd == 1 && Controller("C09").parameters.capacityDelta == -1 && Controller("C21").parameters.capacityDelta == 1 && Controller("C22").parameters.capacityDelta == 2, "Effective/structural parameters.");
            Check(Controller("C34").parameters.probabilityPointsPerSource == 15 && Controller("C35").parameters.probabilityPointsPerSource == 15, "Probability source points.");
            Check(Controller("C45").parameters.matchingDieContributionMultiplier == 3 && Controller("C51").parameters.initialMultiplierUnits2 == 3 && Controller("C52").buyPrice == 4 && Controller("C52").parameters.sellPriceAddPerSuccessfulStageFinalize == 3, "Dynamic sources.");
            Check(Controller("C53").parameters.factorUnits2.SequenceEqual(new[] { 2, 4, 6, 8, 10, 12, 14, 16, 1 }), "C53 sample order.");
        }
        private void ValidateRange(int[] r) => Check(r != null && r.Length == 2 && r[0] >= minPipValue && r[1] <= maxPipValue && r[0] <= r[1], "Pip range.");
        public string Opposite(string id)
        {
            switch (id) { case "F01": return faceOpposites.F01; case "F02": return faceOpposites.F02; case "F03": return faceOpposites.F03; case "F04": return faceOpposites.F04; case "F05": return faceOpposites.F05; case "F06": return faceOpposites.F06; default: throw new ArgumentException("Unknown face ID: " + id); }
        }
        public void ValidateFaces(FaceState[] faces)
        {
            ValidateIds(faces, f => f.faceId, Enumerable.Range(1, 6).Select(i => "F" + i.ToString("D2")).ToArray());
            foreach (var f in faces) Check(f.permanentPip >= minPipValue && f.permanentPip <= maxPipValue && (f.baseColor == "red" || f.baseColor == "blue") && f.baseWeight > 0 && (f.texture == "none" || f.texture == "smooth" || f.texture == "rough"), "Invalid face: " + f.faceId);
        }
        private static string[] Ids(string p, int n) => Enumerable.Range(1, n).Select(i => p + i.ToString("D2")).ToArray();
        private static void ValidateIds<T>(T[] entries, Func<T, string> key, string[] ids) where T : class
        {
            Check(entries != null && entries.Length == ids.Length && entries.All(e => e != null), "Missing/wrong entry count.");
            Check(entries.Select(key).Distinct(StringComparer.Ordinal).Count() == ids.Length && new HashSet<string>(entries.Select(key), StringComparer.Ordinal).SetEquals(ids), "Missing, duplicate or unknown ID.");
        }
        internal static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
