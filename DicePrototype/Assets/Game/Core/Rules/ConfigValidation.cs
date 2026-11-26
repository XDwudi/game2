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
        public BossModifierDefinition Modifier(string id) => Required(bossModifiers, d => d.id, id);
        public LootKindDefinition Loot(string id) => Required(lootKinds, d => d.id, id);
        public static readonly string[] ModifierEffects = { "SealOdd", "SealEven", "SealLow", "SealHigh", "SealRed", "SealBlue", "SealFirst", "SealAlternate", "NoStraight", "SmallHandsOnly", "NoRepeatHand", "HandHalf", "ZeroLevels", "NoRed", "NoBlue", "FirstHalf", "LaterHalf", "PipsOnly", "GrowthHalf", "EnchantSilence", "RollsDelta", "RollsFixed", "SubmissionsDelta", "CapacityDelta", "TargetPercent", "MustFill", "NoItems", "LoseHalfGold", "TaxPerDie", "NoReward", "GrowthFreeze", "HandDowngrade", "DisableLeftmost", "DisableRarity" };
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
        // p1.1.1 name lists (docs/specs/v111/RULES_DELTA.md 4.5, 5, 6 and 9.3). Changing one of them is a rule change.
        private static readonly string[] RemovedIds = { "C04", "C16", "C23", "C90", "C128", "C145" };
        private static readonly string[] CultivationIds = { "C07", "C15", "C26", "C36", "C37", "C38", "C115", "C117", "C118", "C119", "C120", "C121", "C122", "C123", "C124", "C126", "C127" };
        private static readonly string[] UniqueIds = { "C02", "C09", "C31", "C33", "C41", "C42", "C53", "C54", "C58", "C71", "C87", "C89", "C100", "C101", "C103", "C125", "C130", "C138", "C155" };
        private static readonly string[] AdditiveGrowthIds = { "C51", "C56", "C57", "C60", "C61", "C129", "C132", "C134" };
        private static readonly string[] BaseGrowthIds = { "C55", "C59", "C62" };
        private static readonly string[] MultiplierGrowthIds = { "C54", "C58", "C100", "C125" };
        private static readonly string[] AdditiveBonusIds = { "C06", "C08", "C11", "C14", "C18", "C28", "C30", "C32", "C40", "C46", "C74", "C75", "C77", "C81", "C82", "C83", "C86", "C88", "C91", "C102", "C104", "C105", "C106", "C144", "C149", "C153", "C156" };
        private static readonly string[] ScoreMultiplierIds = { "C02", "C09", "C31", "C33", "C42", "C101", "C103", "C130", "C138", "C155" };
        public void Validate()
        {
            Check(configVersion == "p1.1.1" && saveSchemaVersion == 8, "Unsupported config/save version.");
            Check(unityMajorMinor == "6000.6" && blenderMajorMinor == "5.2", "Toolchain.");
            Check(initialDiceCount == 5 && initialGold == 0 && maxPermanentDice == 8 && maxTemporaryDice == 3 && maxActiveDice == 10, "Initial/dice limits.");
            Check(initialSubmitCapacity == 5 && maxSubmitCapacity == 8 && !purchasedDiceChangeCapacity, "Independent submit capacity.");
            Check(totalRollsPerHand == 3 && submissionsPerStage == 3 && bonusPerUnusedRoll == 3, "Budgets.");
            Check(initialControllerSlots == 5 && maxOrdinaryControllerSlots == 7 && controllerSlotPriceStep > 0 && maxControllerInstances == 10 && !slotlessConsumesBaseSlot, "Controller limits.");
            Check(initialConsumableSlots == 2 && maxConsumableSlots == 5 && minDiscountedItemPrice == 1 && maxItemDiscount == 3, "Consumable economy.");
            Check(baseControllerSellPrice == 1 && controllerSellDivisor >= 1 && consumableSellPrice == 1 && baseStageReward > 0 && riskStageReward > baseStageReward && goldPerUnusedSubmission >= 0, "Economy.");
            Check(interestDivisor > 0 && maxInterestPrincipal >= interestDivisor && maxInterestPrincipal % interestDivisor == 0 && interestPrincipalStep > 0 && interestPrincipalStep % interestDivisor == 0 && maxInterestPrincipalPurchases >= 0 && interestBalanceTiming == "finalize_before_rewards_after_all_submit_income", "Interest.");
            Check(minPipValue == 1 && maxPipValue == 6 && minStraightLength == 5 && maxHandLevel == 12 && maxColorLevel == 12, "Hand/pip limits.");
            Check(scoreStorage == "nonnegative_big_integer_decimal_string" && walletStorage == "checked_nonnegative_int64" && rounding == "single_final_floor", "Exact storage contract.");
            Check(probabilityUnits == 1000000 && probabilityRounding == "largest_remainder_face_id_tie" && allowZeroProbabilityAfterGroupTransfer, "Probability precision.");
            Check(smoothOppositeWeight100 > 0 && smoothOtherWeight100 > 0 && roughOppositeWeight100 > 0 && roughOtherWeight100 > 0 && temporaryPipBiasWeight100 > 0, "Probability weights.");
            Check(glassBreakPercent == 15 && glassContributionMultiplier == 3 && goldAdd100 == 50 && gildedFaceWeightDelta == 1 && maxFaceWeight == 5, "Glass/gold/weight.");
            Check(growthCatchupPerCycle != null && growthCatchupPerCycle.additive == 50 && growthCatchupPerCycle.@base == 6 && growthCatchupPerCycle.multiplier == 10, "Growth catch-up.");
            Check(cultivationFacesPerSaleBonus == 4 && cultivationMaxSaleBonus == 6, "Cultivation resale.");
            Check(diceDefinition != null && diceDefinition.id == "standard_d6" && diceDefinition.faces != null && diceDefinition.faces.Length == 6 && diceDefinition.faces.All(f => f != null), "Standard die.");
            ValidateFaces(DiceFactory.CreateStandard(this, 1).faces);
            Check(faceOpposites != null, "Opposites missing."); for (int i = 1; i <= 6; i++) { string id = "F" + i.ToString("D2"); Check(Opposite(id) != id && Opposite(Opposite(id)) == id, "Opposites."); }
            ValidateIds(redRewards, r => r.count.ToString(), Enumerable.Range(0, 9).Select(i => i.ToString()).ToArray());
            Check(redRewards.OrderBy(r => r.count).Select(r => r.factor100).SequenceEqual(new[] { 100, 100, 100, 200, 300, 450, 600, 800, 1100 }), "Red rewards.");
            string[] hands = { "pair", "two_pair", "trips", "full_house", "three_pair", "quads", "two_trips", "five_kind", "four_pair", "six_kind", "seven_kind", "eight_kind", "straight" };
            ValidateIds(handDefinitions, h => h.id, hands);
            foreach (var h in handDefinitions) Check(!string.IsNullOrWhiteSpace(h.displayName) && h.baseMult100 > 0 && h.incrementPerLevel100 > 0 && h.baseUpgradePrice > 0 && h.maxLevel == maxHandLevel && h.minSubmittedDice > 0 && h.minSubmittedDice <= maxSubmitCapacity && h.requirePermanentDice >= 0 && h.requirePermanentDice <= maxPermanentDice && h.structuralPriority > 0, "Hand: " + h.id);
            Check(handDefinitions.Select(h => h.structuralPriority).Distinct().Count() == 13, "Unique structural priorities.");
            ValidateIds(colorDefinitions, c => c.id, new[] { "all_blue", "all_red" });
            foreach (var c in colorDefinitions) Check(c.baseAdd100 == (c.id == "all_blue" ? 25 : 50) && c.addPerLevel100 == 50 && c.baseUpgradePrice > 0 && c.maxLevel == maxColorLevel && c.minSubmittedDice == 1 && !c.countsAsHand, "Additive color.");
            Check(removedControllerIds != null && removedControllerIds.SequenceEqual(RemovedIds), "Retired IDs.");
            ValidateIds(controllers, c => c.id, Ids("C", 156).Except(removedControllerIds).ToArray());
            ValidateIds(consumables, c => c.id, Ids("I", 13)); ValidateIds(services, s => s.id, Ids("O", 11)); ValidateIds(diceUpgrades, u => u.id, Ids("U", 11));
            Check(activeContentCount == 185 && controllers.Length + consumables.Length + services.Length + diceUpgrades.Length == activeContentCount, "Content count.");
            Check(controllers.Count(c => c.rarity == "common") == 29 && controllers.Count(c => c.rarity == "uncommon") == 87 && controllers.Count(c => c.rarity == "rare") == 34, "Rarity counts.");
            Check(new HashSet<string>(controllers.Where(c => c.isCultivation).Select(c => c.id), StringComparer.Ordinal).SetEquals(CultivationIds), "Cultivation cards.");
            Check(shop != null && shop.controllerWeights != null, "Shop missing.");
            foreach (var c in controllers)
            {
                ValidatePlayerText(c.id, c.shortText, c.details);
                Check(!string.IsNullOrWhiteSpace(c.displayName) && !string.IsNullOrWhiteSpace(c.description) && !string.IsNullOrWhiteSpace(c.trigger) && c.parameters != null && c.buyPrice >= 2 && c.baseSellPrice == 1, "Controller: " + c.id);
                int w = c.rarity == "common" ? shop.controllerWeights.common : c.rarity == "uncommon" ? shop.controllerWeights.uncommon : c.rarity == "rare" ? shop.controllerWeights.rare : 0;
                Check(w > 0 && c.offerWeight == w, "Controller rarity.");
            }
            foreach (var c in consumables)
            {
                ValidatePlayerText(c.id, c.shortText, c.details);
                Check(c.baseBuyPrice >= 1 && c.sellPrice == 1 && (c.window == "Selecting" || c.window == "HandReady" || c.window == "HandReadyOrSelecting") && c.initialUses == (c.id == "I13" ? 3 : 1), "Consumable.");
            }
            Check(Consumable("I13").window == "Selecting" && Consumable("I13").baseBuyPrice == 3 && Consumable("I01").baseBuyPrice == 1 && Consumable("I10").baseBuyPrice == 1, "v1.1.1 items.");
            foreach (var s in services)
            {
                ValidatePlayerText(s.id, s.shortText, s.details);
                Check(s.baseBuyPrice >= 1 && !string.IsNullOrWhiteSpace(s.priceRule), "Service.");
            }
            foreach (var u in diceUpgrades)
            {
                ValidatePlayerText(u.id, u.shortText, u.details);
                Check(u.buyPrice >= 1 && u.target == (u.id == "U05" ? "Die" : "Face") && u.maxSelectableFaces == (u.id == "U05" ? 0 : u.id == "U11" ? 1 : 2), "Die upgrade.");
            }
            ValidateEnchantments(); ValidateParameters(); ValidateBosses();
            ValidateIds(stages, s => s.stageId, Enumerable.Range(1, 24).Select(i => "STAGE_" + i.ToString("D2")).ToArray());
            foreach (var s in stages) Check(s.indexInChapter >= 1 && s.indexInChapter <= 3 && s.stageKind == new[] { "normal", "pressure", "boss" }[s.indexInChapter - 1] && s.riskEnabled == (s.stageKind == "pressure") && ExactScoreMath.Parse(s.normalTarget) > 0 && ExactScoreMath.Parse(s.normalTarget) % 2 == 0 && s.normalReward > 0 && s.shopAfterClear == (s.stageId != "STAGE_24") && (s.riskEnabled ? (ExactScoreMath.Parse(s.riskTarget) * 2 == ExactScoreMath.Parse(s.normalTarget) * 3 && s.riskReward > s.normalReward) : (string.IsNullOrEmpty(s.riskTarget) && s.riskReward == 0)), "Stage.");
            Check(stageFlow != null && stageFlow.latchQualified && !stageFlow.autoEndOnReached && stageFlow.autoEndWhenNoSubmissions && stageFlow.manualEndRequiresQualified && stageFlow.manualEndDiscardsUnsubmittedHand && stageFlow.overScoreGold == 0 && stageFlow.clearRewardOnlyAtFinalization && stageFlow.submissionIncomeAppliedWithinSubmit, "Stage flow.");
            Check(controllerOrder != null && controllerOrder.stageBindingsFrozen && controllerOrder.dragChangesOnlyOrder && controllerOrder.maxMimicResolveDepth == 32 && controllerOrder.onTargetLost == "no_base_effect_until_next_stage", "Controller binding.");
            Check(shop.initialControllerOffers == 4 && shop.maxControllerOffers == 6 && shop.serviceOffers == 2 && shop.initialConsumableOffers == 2 && shop.maxConsumableOffers == 5 && shop.upgradeOffers == 2 && shop.paidRefreshEnabled && shop.firstRefreshPrice == 3 && shop.refreshPriceStep == 2 && shop.refreshTarget == "controllers_only" && shop.freeRefreshConsumesFirstIndex && shop.maxOfferExpansionPurchases == 3, "Shop contracts.");
            Check(shop.selfStudyLimit == 2 && shop.selfStudyPremium == 1 && shop.upgradePricePerExistingLevel == 1 && shop.selfDieUpgradeLimit == 2 && shop.selfDieUpgradePremium == 1 && shop.firstControllerGuaranteeMaxBasePrice == 4, "v1.1.1 shop limits.");
            Check(shop.uniqueControllerIds != null && shop.uniqueControllerIds.SequenceEqual(UniqueIds), "Unique multiplier cards.");
            Check(shop.discountCategories != null && new HashSet<string>(shop.discountCategories).SetEquals(new[] { "consumable", "service", "new_die" }) && shop.upgradePoolIncludes.SequenceEqual(new[] { "hand", "color" }), "Shop categories.");
            Check(generation != null && generation.newDieTemplateWeights != null && generation.newDieTemplateWeights.random > 0 && generation.newDieTemplateWeights.low > 0 && generation.newDieTemplateWeights.high > 0 && generation.newDieTemplateWeights.random + generation.newDieTemplateWeights.low + generation.newDieTemplateWeights.high == 100, "Generation.");
            ValidateRange(generation.pipRange); ValidateRange(generation.lowRange); ValidateRange(generation.highRange);
            Check(generation.newDieBasePrice == 3 && generation.maxNewDieBasePrice == 6 && generation.redFaceChancePercent >= 0 && generation.redFaceChancePercent <= 100, "Die prices.");
            Check(rng != null && rng.algorithm == "xorshift32_v1" && rng.diceSeedXor != rng.shopSeedXor && rng.diceSeedXor != rng.effectSeedXor && rng.shopSeedXor != rng.effectSeedXor && rng.zeroSeedReplacement == 0x9E3779B9u, "RNG.");
            Check(presentation != null && presentation.rollSeconds > 0 && presentation.fastRollSeconds > 0 && presentation.handbookHandRows == 14 && presentation.handbookColorRows == 2 && presentation.contentIconCount == 185 && presentation.enchantmentBadgeCount == 5, "Presentation counts.");
            Check(buildTargets != null && buildTargets.SequenceEqual(new[] { "Windows_x86_64", "macOS_Universal" }), "Build targets.");
            Check(display != null && display.initialMode == "borderless" && display.confirmationTimeoutSeconds == 15 && display.persistOnlyConfirmedSettings, "Display contract.");
            Check(debug != null && debug.unlockPhrase == "XD is god" && debug.caseSensitive && !debug.trimInput && debug.separateSaveSlot && debug.markRunAsDebugOnFirstMutation && debug.allowDuplicateControllerIds && debug.maxDice == 32 && debug.maxSubmitCapacity == 32 && debug.maxControllerInstances == 32 && debug.maxHandLevel == 100 && debug.maxColorLevel == 100 && debug.maxEditableWallet == 1000000000 && debug.maxEditableBudget == 999, "Debug limits.");
            ProbabilityCalculator.BuildUnits(DiceFactory.CreateStandard(this, 1).faces, false, false, this);
        }
        private void ValidateEnchantments()
        {
            string[] ids = { "none", "slotless", "base10", "mult2", "submit1", "roll1" }; ValidateIds(enchantments, e => e.id, ids);
            Check(enchantments.Select(e => e.id).SequenceEqual(ids) && enchantments.Select(e => e.weight).SequenceEqual(new[] { 50, 18, 10, 4, 9, 9 }) && enchantments[0].buyPremium == 0 && enchantments.Skip(1).All(e => e.buyPremium > 0) && Enchantment("base10").buyPremium == 2 && Enchantment("mult2").buyPremium == 5, "Enchantments.");
            Check(enchantRules != null && enchantRules.rawDrawOrder.SequenceEqual(ids) && enchantRules.nonSubmitControllerIds.SequenceEqual(new[] { "C05", "C21", "C22", "C34", "C35", "C52", "C78", "C79", "C95", "C96", "C97", "C98", "C99", "C110", "C111", "C112", "C113", "C114", "C115", "C116", "C120", "C121", "C131", "C133", "C136", "C140", "C154" }) && enchantRules.baseAdd == 15 && enchantRules.scoreAdd100 == 150 && enchantRules.submissionBonus == 1 && enchantRules.rollBonus == 1 && !enchantRules.copyTargetEnchantment && enchantRules.triggerCountPerSourcePerSubmitMax == 1, "Enchant policies.");
            Check(enchantRules.shopChanceByCyclePercent != null && enchantRules.shopChanceByCyclePercent.SequenceEqual(new[] { 10, 20, 35, 45, 55, 60, 65, 65 }), "Shop enchant chance by cycle.");
            var w = enchantRules.nonSubmitDrawWeights; Check(w != null && w.none == 100 && w.slotless == 50 && w.submit1 == 25 && w.roll1 == 25, "Dedicated enchant pool.");
            foreach (var c in controllers) Check(c.scoreEnchantEligible == !enchantRules.nonSubmitControllerIds.Contains(c.id), "Score enchant eligibility.");
        }
        private void ValidateParameters()
        {
            Func<string, ControllerParameters> p = id => Controller(id).parameters;
            foreach (string id in AdditiveBonusIds) Check(p(id).additiveBonus100 > 0 && Controller(id).trigger == "ScoreAdditive", id + " additive bonus.");
            foreach (string id in ScoreMultiplierIds) Check(p(id).scoreMultiplier100 > 100, id + " multiplier.");
            Check(p("C11").requiredCounts != null && p("C11").requiredCounts.pip2 > 0 && p("C11").requiredCounts.pip3 > 0, "C11 required counts did not deserialize.");
            Check(p("C03").index == 0 && p("C12").oddToEvenAdd == 1 && p("C09").capacityDelta == -1 && p("C21").capacityDelta == 1 && p("C22").capacityDelta == 2, "Effective/structural parameters.");
            Check(p("C34").probabilityPointsPerSource == 15 && p("C35").probabilityPointsPerSource == 15, "Probability source points.");
            Check(p("C45").matchingDieContributionMultiplier == 3 && Controller("C52").buyPrice == 4 && p("C52").sellPriceAddPerSuccessfulStageFinalize == 3 && p("C51").maxGrowthPerShop >= 0, "Dynamic sources.");
            Check(p("C53").factorUnits2 != null && p("C53").factorUnits2.SequenceEqual(new[] { 1, 2, 2, 3, 4, 4, 6, 6, 8 }), "C53 outcome table.");
            Check(p("C42").destroyChancePercentAfterValidSubmission == 15, "C42 risk.");
            // v1.1.1 additive formulas (section 4.2 and 4.4)
            Check(p("C19").minOneAdd100 > 0 && p("C19").minTwoAdd100 > 0 && p("C91").minDice == 3 && p("C137").additivePerMatch100 > 0, "Reworked additive cards.");
            Check(p("C47").additivePerController100 > 0 && p("C49").additivePerFive100 > 0 && p("C49").noFiveMultiplier100 > 0 && p("C49").noFiveMultiplier100 < 100 && p("C63").additivePerPermanentDie100 > 0, "Counting additive cards.");
            Check(p("C139").goldPerStep > 0 && p("C139").additivePerGoldStep100 > 0 && p("C147").additivePerPoolDie100 > 0 && p("C148").additivePerUnusedRoll100 > 0 && p("C150").additivePerPreviousSubmit100 > 0 && p("C151").additivePerEmptyOrdinarySlot100 > 0 && p("C152").additivePerCommon100 > 0, "Stepped additive cards.");
            Check(p("C141").initialAdditive100 > 0 && p("C141").decayPerClear100 > 0 && p("C141").initialAdditive100 % p("C141").decayPerClear100 == 0 && p("C141").expireGrowth == p("C141").initialAdditive100 / p("C141").decayPerClear100, "C141 decay.");
            // v1.1.1 growth cards (section 5): the kind, a positive start and event, and a matching trigger.
            foreach (var c in controllers)
            {
                string kind = AdditiveGrowthIds.Contains(c.id) ? "additive" : BaseGrowthIds.Contains(c.id) ? "base" : MultiplierGrowthIds.Contains(c.id) ? "multiplier" : null;
                if (kind == null) Check(string.IsNullOrEmpty(c.parameters.growthKind) && c.parameters.growthStart == 0 && c.parameters.growthEvent == 0, c.id + " is not a growth card.");
                else Check(c.parameters.growthKind == kind && c.parameters.growthEvent > 0 && c.parameters.growthStart > (kind == "multiplier" ? 100 : 0) && c.trigger == (kind == "additive" ? "GrowthAdditive" : kind == "base" ? "GrowthBase" : "GrowthMultiply"), c.id + " growth.");
            }
            // v0.9 (docs/specs/v09/RULES_SPEC.md)
            foreach (string id in new[] { "C64", "C65", "C66", "C67", "C68", "C69" }) Check(p(id).retriggers > 0, id + " retriggers.");
            foreach (string id in new[] { "C71", "C87", "C89" }) Check(p(id).stepNumerator > 0 && p(id).stepDenominator > 0, id + " step.");
            foreach (string id in new[] { "C82", "C86" }) Check(p(id).handIds != null && p(id).handIds.Length > 0 && p(id).handIds.All(h => handDefinitions.Any(d => d.id == h)), id + " hands.");
            Check(p("C66").pips != null && p("C67").pips != null, "v0.9 lists.");
            Check(p("C57").minRedGroup > 0 && p("C75").maxRollsUsed == 1 && p("C80").pairSum == 7 && p("C80").baseAddPerPair > 0, "v0.9 conditions.");
            Check(p("C84").handMultAdd100 > 0 && p("C85").handMultAdd100 > 0 && p("C87").minSamePip >= 2 && p("C93").dieContributionMultiplier > 0, "v0.9 hand parameters.");
            Check(p("C78").rollsPerHandDelta > 0 && p("C79").submissionsDelta > 0 && p("C101").submissionsDelta < 0, "v0.9 budgets.");
            Check(p("C95").goldPerStage > 0 && p("C96").interestPrincipalAdd > 0 && p("C96").interestPrincipalAdd % interestDivisor == 0 && p("C97").goldPerTrigger > 0 && p("C97").maxTriggersPerStage > 0 && p("C97").targetDivisor > 0 && p("C98").priceDelta < 0 && p("C99").saleBonusPerStage > 0, "v0.9 economy.");
            Check(p("C103").maxOwnedPermanentDice > p("C103").strictMaxOwnedPermanentDice && p("C103").strictScoreMultiplier100 > p("C103").scoreMultiplier100, "C103.");
            // v1.0 (docs/specs/v10/RULES_SPEC.md)
            foreach (string id in new[] { "C109", "C143" }) Check(p(id).retriggers > 0, id + " retriggers.");
            Check(p("C107").perDieAdd > 0 && p("C108").baseAddPerPip > 0 && p("C135").baseAddPerItem > 0 && p("C142").handMultAdd100 > 0, "v1.0 adds.");
            Check(p("C110").itemsPerStage > 0 && p("C111").giftChoices > 0 && p("C111").giftPicks > 0 && p("C113").giftChoices > 0 && p("C113").giftPicks > 0 && p("C114").enchantsPerBoss > 0 && p("C116").dicePerBoss > 0 && p("C136").dicePerStage > 0, "v1.0 generators.");
            Check(p("C112").rarity == "common" && p("C152").rarity == "common", "v1.0 rarity filters.");
            Check(p("C133").goldPerBoss >= 0 && p("C133").goldPerPressure >= 0 && p("C138").goldCost == 2, "v1.0 gold.");
            // v1.1.1 face rewrites (section 6.1): which faces a card touches and by how much.
            Check(p("C115").facesPerStage == 3 && p("C115").upgradeIds != null && p("C115").upgradeIds.SequenceEqual(new[] { "U03", "U04", "U06", "U08" }) && p("C115").upgradeIds.All(u => diceUpgrades.Any(d => d.id == u && d.target == "Face")), "C115 upgrades.");
            foreach (string id in new[] { "C36", "C37" }) Check(p(id).indices != null && p(id).indices.SequenceEqual(new[] { 0, 1, 2 }) && p(id).delta == (id == "C36" ? 1 : -1) && p(id).minDice == 2, id + " targets.");
            foreach (string id in new[] { "C38", "C117", "C119" }) Check(p(id).targetIndices == "first_and_last_distinct", id + " targets.");
            Check(p("C119").facesPerDie == 2 && p("C120").delta == 1 && p("C121").delta == -1 && p("C120").facesPerDie == 3 && p("C121").facesPerDie == 3, "v1.1.1 faces per die.");
            Check(p("C122").pip == 5 && p("C124").delta == 2 && p("C126").delta == 2 && p("C127").minDice == 2 && p("C127").sourceIndex == 0, "v1.1.1 face growth.");
            Check(p("C123").pips != null && p("C123").pips.SequenceEqual(new[] { 2, 3 }) && p("C123").lowPipDelta == 1 && p("C123").highPipDelta == -2 && p("C123").highPipFloor == 3, "C123 deltas.");
            // v1.1.1 new cards (section 4.5)
            Check(p("C154").freeUnlocksPerHand == 1 && Controller("C154").trigger == "UnlockDie" && p("C155").minFours == 4 && p("C155").minOnes == 2 && p("C155").enhancedScoreMultiplier100 > p("C155").scoreMultiplier100 && p("C156").targetDivisor == 2, "v1.1.1 new cards.");
        }
        private void ValidateBosses()
        {
            ValidateIds(bossModifiers, m => m.id, Ids("B", 39));
            foreach (var m in bossModifiers)
            {
                ValidatePlayerText(m.id, m.shortText, m.details);
                Check(!string.IsNullOrWhiteSpace(m.displayName) && !string.IsNullOrWhiteSpace(m.description) && m.tier >= 1 && m.tier <= 4 && m.filter != null, "Boss modifier: " + m.id);
                Check(m.minCycle == (m.tier == 1 ? 1 : m.tier == 2 ? 2 : m.tier == 3 ? 4 : 8), "Boss modifier cycle: " + m.id);
                Check(m.effects != null && m.effects.Length > 0 && m.effects.Distinct(StringComparer.Ordinal).Count() == m.effects.Length && m.effects.All(e => ModifierEffects.Contains(e)), "Boss modifier effects: " + m.id);
                foreach (string e in m.effects)
                {
                    if (e == "RollsDelta" || e == "SubmissionsDelta" || e == "CapacityDelta") Check(m.amount < 0, e + " amount: " + m.id);
                    if (e == "RollsFixed" || e == "TaxPerDie" || e == "HandDowngrade" || e == "DisableLeftmost" || e == "DisableRarity") Check(m.amount > 0, e + " amount: " + m.id);
                    if (e == "TargetPercent") Check(m.amount > 100 && stages.All(s => (ExactScoreMath.Parse(s.normalTarget) * m.amount) % 100 == 0), "TargetPercent: " + m.id);
                    if (e == "DisableRarity") Check(m.filter == "common" || m.filter == "uncommon" || m.filter == "rare", "DisableRarity filter: " + m.id);
                }
            }
            // v1.1.1 (section 10): the five modifiers whose target fields or effect codes changed.
            Check(Modifier("B06").tier == 2 && Modifier("B06").effects.SequenceEqual(new[] { "EnchantSilence" }), "B06.");
            Check(Modifier("B25").effects.SequenceEqual(new[] { "DisableLeftmost" }) && Modifier("B25").amount == 2, "B25.");
            Check(Modifier("B29").effects.SequenceEqual(new[] { "DisableRarity" }) && Modifier("B29").filter == "rare" && Modifier("B29").amount == 1, "B29.");
            Check(Modifier("B35").effects.SequenceEqual(new[] { "ZeroLevels", "GrowthHalf" }), "B35.");
            Check(Modifier("B37").effects.SequenceEqual(new[] { "DisableLeftmost", "EnchantSilence" }) && Modifier("B37").amount == 2, "B37.");
            Check(bossModifiers.Count(m => m.tier < 4) >= 8 && bossModifiers.Count(m => m.tier == 1) >= 2 && bossModifiers.Count(m => m.tier == 4) >= 2, "Boss pools.");
            ValidateIds(lootKinds, k => k.id, Enumerable.Range(1, 6).Select(i => "L" + i).ToArray());
            foreach (var k in lootKinds)
            {
                ValidatePlayerText(k.id, k.shortText, k.details);
                Check(!string.IsNullOrWhiteSpace(k.displayName) && !string.IsNullOrWhiteSpace(k.description) && k.weight > 0 && k.picks >= 1 && k.picks <= 3, "Loot kind: " + k.id);
            }
            Check(lootGoldValue > 0 && lootHandLevels >= 1 && rng.stageSeedXor != rng.diceSeedXor && rng.stageSeedXor != rng.shopSeedXor && rng.stageSeedXor != rng.effectSeedXor && rng.stageSeedXor != 0, "Loot/stage stream.");
            Check(stages.Length % 3 == 0, "Stages must form complete cycles.");
        }
        private static void ValidatePlayerText(string id, string shortText, string details)
        {
            Check(!string.IsNullOrWhiteSpace(shortText), "Missing shortText: " + id);
            // Details may be empty when the short text already states the whole rule; never null or blank-only.
            Check(details != null, "Missing details: " + id);
            Check(details.Length == 0 || !string.IsNullOrWhiteSpace(details), "Blank details: " + id);
            // Count Unicode code points, matching Python len() in the content extractor.
            int characters = 0;
            for (int i = 0; i < shortText.Length; i++)
            {
                characters++;
                if (char.IsSurrogatePair(shortText, i)) i++;
            }
            Check(characters <= 28, "shortText exceeds 28 characters: " + id);
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
