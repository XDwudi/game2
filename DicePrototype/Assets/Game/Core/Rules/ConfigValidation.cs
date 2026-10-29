using System;
using System.Collections.Generic;
using System.Linq;

namespace DiceCraft.Core
{
    public sealed partial class GameConfig
    {
        public ControllerDefinition Controller(string id) => Required(controllers, d => d.id, id);
        public HandDefinition Hand(string id) => Required(handDefinitions, d => d.id, id);
        public ConsumableDefinition Consumable(string id) => Required(consumables, d => d.id, id);
        public ServiceDefinition Service(string id) => Required(services, d => d.id, id);
        public DieUpgradeDefinition Upgrade(string id) => Required(diceUpgrades, d => d.id, id);
        private static T Required<T>(T[] entries, Func<T, string> key, string id)
        {
            if (entries == null) throw new InvalidOperationException("Configuration is not loaded.");
            foreach (T entry in entries) if (key(entry) == id) return entry;
            throw new ArgumentException("Unknown configured ID: " + id, nameof(id));
        }

        /// <summary>Call before deserializing, so omitted zero/false-valued fields cannot silently default.</summary>
        public static void ValidateJson(string json) => ConfigJsonContract.Validate(json);

        /// <summary>Validates data, supported explicit handlers and cross references. Throws on any defect.</summary>
        public void Validate()
        {
            Check(configVersion == "p0.4.0" && saveSchemaVersion == 2, "Unsupported config/save version.");
            Check(unityMajorMinor == "6000.6" && blenderMajorMinor == "5.2", "Unsupported toolchain.");
            Check(initialDiceCount >= 1 && initialDiceCount <= maxPermanentDice, "Initial dice count.");
            Check(maxPermanentDice == 7 && maxActiveDice == 10 && maxTemporaryDice == 3, "Dice capacity contract.");
            Check(initialSubmitCapacity >= 1 && initialSubmitCapacity <= maxSubmitCapacity && maxSubmitCapacity == 7, "Submit capacity.");
            Check(totalRollsPerHand > 0 && submissionsPerStage > 0 && bonusPerUnusedRoll >= 0, "Hand budgets.");
            Check(initialGold >= 0 && baseStageReward > 0 && riskStageReward > 0 && goldPerUnusedSubmission >= 0, "Economy.");
            Check(interestDivisor > 0 && interestCap == -1 && interestBalanceTiming == "before_current_stage_rewards", "Interest contract.");
            Check(initialControllerSlots == 4 && maxControllerSlots == 10 && !controllerSlotUpgradeInCatalog, "Controller slots.");
            Check(initialConsumableSlots == 2 && maxConsumableSlots == 5 && maxItemDiscount == 3, "Item slots/discount.");
            Check(controllerSellPrice == 1 && consumableSellPrice == 1 && minDiscountedItemPrice == 2, "Buy/sell minimum.");
            Check(minDiceForColorHand == 5 && minStraightLength == 5 && minPipValue == 1 && maxPipValue == 6 && maxHandLevel == 4, "P0 hand limits.");
            Check(probabilityUnits == 1000000 && probabilityRounding == "largest_remainder_face_id_tie", "Probability precision.");
            Check(smoothOppositeWeight100 > 0 && smoothOtherWeight100 > 0 && roughOppositeWeight100 > 0 && roughOtherWeight100 > 0 && temporaryPipBiasWeight100 > 0, "Probability weights.");
            Check(glassBreakPercent >= 0 && glassBreakPercent <= 100 && glassContributionMultiplier > 0 && goldContributionMultiplier > 0, "Glass/gold.");
            Check(diceDefinition != null && diceDefinition.id == "standard_d6", "Standard dice definition.");
            Check(diceDefinition.faces != null && diceDefinition.faces.Length == 6 && diceDefinition.faces.All(f => f != null), "Standard six physical faces.");
            ValidateFaces(DiceFactory.CreateStandard(this, 1).faces);
            Check(faceOpposites != null, "Face opposites missing.");
            for (int i = 1; i <= 6; i++)
            {
                string face = "F" + i.ToString("D2");
                string opposite = Opposite(face);
                Check(opposite != face && Opposite(opposite) == face, "Face opposite mapping.");
            }
            Check(redRewards != null && redRewards.Length == maxSubmitCapacity + 1, "Red reward count.");
            Check(redRewards.Select(r => r.count).Distinct().Count() == redRewards.Length, "Duplicate red count.");
            for (int i = 0; i <= maxSubmitCapacity; i++)
                Check(redRewards.Any(r => r.count == i && r.factor100 >= 100 && !string.IsNullOrWhiteSpace(r.title)), "Invalid red reward.");
            string[] handIds = { "pair", "two_pair", "trips", "full_house", "quads", "five_kind", "six_kind", "seven_kind", "straight", "all_blue", "all_red" };
            ValidateIds(handDefinitions, h => h.id, handIds);
            foreach (var h in handDefinitions)
                Check(!string.IsNullOrWhiteSpace(h.displayName) && h.baseMult100 > 0 && h.incrementPerLevel100 > 0 && h.baseUpgradePrice > 0 && h.maxLevel == maxHandLevel && h.minSubmittedDice > 0 && h.minSubmittedDice <= maxSubmitCapacity && h.requirePermanentDice >= 0 && h.requirePermanentDice <= maxPermanentDice && h.enabledInNormalShop, "Invalid hand definition: " + h.id);
            ValidateIds(controllers, c => c.id, Ids("C", 20));
            foreach (var c in controllers)
            {
                Check(!string.IsNullOrWhiteSpace(c.displayName) && !string.IsNullOrWhiteSpace(c.description) && !string.IsNullOrWhiteSpace(c.trigger) && c.parameters != null && c.buyPrice > 0 && c.sellPrice == controllerSellPrice && c.offerWeight > 0 && c.maxOwned == 1, "Invalid controller: " + c.id);
                Check(c.rarity == "common" || c.rarity == "uncommon" || c.rarity == "rare", "Unknown rarity.");
            }
            ValidateControllerParameters();
            ValidateIds(consumables, c => c.id, Ids("I", 7));
            foreach (var c in consumables)
                Check(!string.IsNullOrWhiteSpace(c.displayName) && !string.IsNullOrWhiteSpace(c.description) && !string.IsNullOrWhiteSpace(c.lifetime) && (c.window == "Selecting" || c.window == "HandReady" || c.window == "HandReadyOrSelecting") && c.baseBuyPrice >= minDiscountedItemPrice && c.sellPrice == consumableSellPrice, "Invalid consumable: " + c.id);
            ValidateIds(services, c => c.id, Ids("O", 4));
            foreach (var c in services)
                Check(!string.IsNullOrWhiteSpace(c.displayName) && !string.IsNullOrWhiteSpace(c.description) && !string.IsNullOrWhiteSpace(c.priceRule) && c.baseBuyPrice >= 0 && (c.id == "O03" || c.baseBuyPrice > 0), "Invalid service: " + c.id);
            ValidateIds(diceUpgrades, c => c.id, Ids("U", 7));
            foreach (var c in diceUpgrades)
                Check(!string.IsNullOrWhiteSpace(c.displayName) && !string.IsNullOrWhiteSpace(c.description) && c.buyPrice > 0 && c.target == (c.id == "U05" ? "Die" : "Face"), "Invalid upgrade: " + c.id);
            ValidateIds(stages, s => s.stageId, Enumerable.Range(1, 6).Select(i => "STAGE_" + i.ToString("D2")).ToArray());
            foreach (var s in stages)
                Check(s.normalTarget > 0 && s.normalReward > 0 && (!s.riskEnabled || (s.riskTarget > s.normalTarget && s.riskReward > s.normalReward)) && s.shopAfterClear == (s.stageId != "STAGE_06"), "Invalid stage: " + s.stageId);
            Check(shop != null && shop.controllerOffers > 0 && shop.consumableOffers > 0 && shop.handUpgradeOffers > 0 && shop.diceUpgradeOffers > 0 && shop.serviceOffers > 0 && shop.newDiceOffers > 0 && shop.purchaseLimit == -1 && !shop.restockPurchasedOffers && !shop.paidRefreshEnabled && shop.selfStudyLimit == 1 && shop.selfStudyPremium >= 0 && shop.upgradePricePerExistingLevel >= 0 && shop.controllersUniqueOwned && shop.firstControllerGuaranteeMaxPrice > 0 && shop.sellingWindow == "Shop", "Shop contract.");
            Check(shop.controllerWeights != null && shop.controllerWeights.common > 0 && shop.controllerWeights.uncommon > 0 && shop.controllerWeights.rare > 0, "Rarity weights.");
            Check(shop.discountCategories != null && new HashSet<string>(shop.discountCategories).SetEquals(new[] { "consumable", "service", "new_die" }), "Discount categories.");
            Check(generation != null && generation.newDieTemplateWeights != null && generation.newDieTemplateWeights.random > 0 && generation.newDieTemplateWeights.low > 0 && generation.newDieTemplateWeights.high > 0, "Generation weights.");
            Check(generation.newDieTemplateWeights.random + generation.newDieTemplateWeights.low + generation.newDieTemplateWeights.high == 100, "Template weights total.");
            ValidateRange(generation.pipRange); ValidateRange(generation.lowRange); ValidateRange(generation.highRange);
            Check(generation.redFaceChancePercent >= 0 && generation.redFaceChancePercent <= 100 && generation.newDieBasePrice > 0 && generation.highSumThreshold > 0 && generation.smallSumThreshold > 0 && generation.maxRepeatThreshold > 0 && generation.sameColorThreshold > 0 && generation.highSumPremium >= 0 && generation.smallSumPremium >= 0 && generation.maxRepeatPremium >= 0 && generation.sameColorPremium >= 0, "Generation prices/thresholds.");
            Check(rng != null && rng.algorithm == "xorshift32_v1" && rng.diceSeedXor != rng.shopSeedXor && rng.diceSeedXor != rng.effectSeedXor && rng.shopSeedXor != rng.effectSeedXor && rng.zeroSeedReplacement == 0x9E3779B9u, "RNG contract.");
            Check(presentation != null && presentation.rollSeconds > 0 && presentation.fastRollSeconds > 0 && presentation.referenceWidth > 0 && presentation.referenceHeight > 0, "Presentation configuration.");
            Check(debug != null && debug.markRunAsDebugOnUse && debug.maxControllerSlots <= maxControllerSlots && !debug.unlimitedGold && debug.freeUnlockTestOnly, "Debug safeguards.");
            ProbabilityCalculator.BuildUnits(DiceFactory.CreateStandard(this, 1).faces, false, false, this);
        }

        private void ValidateControllerParameters()
        {
            foreach (string id in new[] { "C02", "C06", "C08", "C09", "C11", "C14", "C18" }) Check(Controller(id).parameters.scoreMultiplier > 0, id + " multiplier.");
            Check(Controller("C01").parameters.extraInterestPerTen > 0, "C01 interest.");
            Check(Controller("C03").parameters.occupiedIndex == 0, "C03 index.");
            Check(totalRollsPerHand + Controller("C04").parameters.rollBonus > 0 && Controller("C04").parameters.submissionBonus > 0, "C04 budgets.");
            var p = Controller("C05").parameters;
            Check(p.rollGrant > 0 && p.perHandLimit == 1 && p.minUnlocked == 1 && p.maxUnlocked == 2 && p.checkOnlyAfterRoll, "C05 trigger.");
            Check(Controller("C06").parameters.minDistinct > 0 && Controller("C06").parameters.minDistinct <= maxPipValue, "C06 distinct.");
            p = Controller("C07").parameters;
            Check(p.onlyFirstSubmission && p.requiredDice == 2 && p.sourceIndex == 0 && p.targetIndex == 1, "C07 copy.");
            Check(Controller("C09").parameters.capacityDelta == -1, "C09 capacity.");
            Check(Controller("C10").parameters.pip == 1 && Controller("C10").parameters.contributionAdd > 0, "C10 contribution.");
            p = Controller("C11").parameters;
            Check(p.pipA == 2 && p.pipB == 3 && p.countA == 2 && p.countB == 2, "C11 structure.");
            Check(Controller("C12").parameters.oddPipAdd == 1 && Controller("C13").parameters.oddContributionAdd > 0, "Odd contributions.");
            Check(Controller("C15").parameters.pattern == "red,blue,red" && Controller("C15").parameters.targetIndex == 1, "C15 pattern.");
            Check(Controller("C16").parameters.rollBonus > 0, "C16 rolls.");
            Check(Controller("C17").parameters.maxPip == 3 && Controller("C17").parameters.contributionAdd > 0, "C17 contribution.");
            Check(Controller("C19").parameters.maxPermanentFaceSum > 0 && Controller("C19").parameters.contributionAdd > 0, "C19 contribution.");
            Check(Controller("C20").parameters.lastSlotPip == 6 && Controller("C20").parameters.finalScoreAdd > 0 && Controller("C20").parameters.requireFullCapacity, "C20 slot.");
        }

        private void ValidateRange(int[] range) => Check(range != null && range.Length == 2 && range[0] >= minPipValue && range[1] <= maxPipValue && range[0] <= range[1], "Pip generation range.");
        public string Opposite(string id)
        {
            switch (id)
            {
                case "F01": return faceOpposites.F01; case "F02": return faceOpposites.F02;
                case "F03": return faceOpposites.F03; case "F04": return faceOpposites.F04;
                case "F05": return faceOpposites.F05; case "F06": return faceOpposites.F06;
                default: throw new ArgumentException("Unknown face ID: " + id);
            }
        }
        public void ValidateFaces(FaceState[] faces)
        {
            ValidateIds(faces, f => f.faceId, Enumerable.Range(1, 6).Select(i => "F" + i.ToString("D2")).ToArray());
            foreach (var f in faces)
                Check(f.permanentPip >= minPipValue && f.permanentPip <= maxPipValue && (f.baseColor == "red" || f.baseColor == "blue") && f.baseWeight > 0 && (f.texture == "none" || f.texture == "smooth" || f.texture == "rough"), "Invalid face: " + f.faceId);
        }
        private static string[] Ids(string prefix, int count) => Enumerable.Range(1, count).Select(i => prefix + i.ToString("D2")).ToArray();
        private static void ValidateIds<T>(T[] entries, Func<T, string> key, string[] ids) where T : class
        {
            Check(entries != null && entries.Length == ids.Length && entries.All(e => e != null), "Missing/wrong entry count.");
            Check(entries.Select(key).Distinct(StringComparer.Ordinal).Count() == ids.Length && new HashSet<string>(entries.Select(key), StringComparer.Ordinal).SetEquals(ids), "Missing, duplicate or unknown ID.");
        }
        internal static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
