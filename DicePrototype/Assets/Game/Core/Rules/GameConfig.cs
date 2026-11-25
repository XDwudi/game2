// Generated from authoritative P0 v0.6 Appendix A; see tools/generate_v06_dtos.py.
// v0.8 added by hand: controllerSellDivisor, perControllerUnits2 (C47), maxGrowthPerShop (C51); see docs/specs/v08/RULES_DELTA.md.
// v0.9 added by hand: maxOrdinaryControllerSlots, controllerSlotPriceStep and the C54-C103 parameters; see docs/specs/v09/RULES_SPEC.md.
// v1.0 added by hand: boss modifiers, loot kinds, stageKind, face weights and the C104-C153 parameters; see docs/specs/v10/RULES_SPEC.md.
// v1.1 added by hand: player-facing shortText/details on the six content categories; see docs/specs/v11/DEV_PLAN.md W10.
// v1.1.1 added by hand: additive zone, growth start/event, cultivation, item uses and shop fields; see docs/specs/v111/RULES_DELTA.md 12.3
// and tools/v111_config.py, which writes the config and fixes the field names and units.
using System;

namespace DiceCraft.Core
{
    [Serializable]
    public sealed partial class GameConfig
    {
        public string configVersion;
        public int saveSchemaVersion;
        public string unityMajorMinor;
        public string blenderMajorMinor;
        public string[] removedControllerIds;
        public int activeContentCount;
        public int initialDiceCount;
        public int maxPermanentDice;
        public int maxTemporaryDice;
        public int maxActiveDice;
        public int initialSubmitCapacity;
        public int maxSubmitCapacity;
        public bool purchasedDiceChangeCapacity;
        public int totalRollsPerHand;
        public int submissionsPerStage;
        public int bonusPerUnusedRoll;
        public int initialGold;
        public int baseStageReward;
        public int riskStageReward;
        public int goldPerUnusedSubmission;
        public int interestDivisor;
        public int maxInterestPrincipal;
        public int interestPrincipalStep;
        public int maxInterestPrincipalPurchases;
        public string interestBalanceTiming;
        public int initialControllerSlots;
        public int maxOrdinaryControllerSlots;
        public int controllerSlotPriceStep;
        public int maxControllerInstances;
        public bool slotlessConsumesBaseSlot;
        public int initialConsumableSlots;
        public int maxConsumableSlots;
        public int baseControllerSellPrice;
        public int controllerSellDivisor;
        public int consumableSellPrice;
        public int minDiscountedItemPrice;
        public int maxItemDiscount;
        public int minStraightLength;
        public int minPipValue;
        public int maxPipValue;
        public int maxHandLevel;
        public int maxColorLevel;
        public string scoreStorage;
        public string walletStorage;
        public string rounding;
        public DiceDefinition diceDefinition;
        public FaceOpposites faceOpposites;
        public int probabilityUnits;
        public string probabilityRounding;
        public bool allowZeroProbabilityAfterGroupTransfer;
        public int smoothOppositeWeight100;
        public int smoothOtherWeight100;
        public int roughOppositeWeight100;
        public int roughOtherWeight100;
        public int temporaryPipBiasWeight100;
        public int glassBreakPercent;
        public int glassContributionMultiplier;
        // v1.1.1: a submitted gold face adds goldAdd100 to the additive zone; a face that turns gold gains weight.
        public int goldAdd100;
        public int gildedFaceWeightDelta;
        public int maxFaceWeight;
        // v1.1.1: head start of a growth card offered in a later shop cycle, per cycle after the first
        // (additive and multiplier in hundredths, base in whole points), and the resale bonus of cultivation cards.
        public GrowthCatchupConfig growthCatchupPerCycle;
        public int cultivationFacesPerSaleBonus;
        public int cultivationMaxSaleBonus;
        public RedReward[] redRewards;
        public EnchantmentDefinition[] enchantments;
        public EnchantmentRules enchantRules;
        public HandDefinition[] handDefinitions;
        public ColorDefinition[] colorDefinitions;
        public ControllerDefinition[] controllers;
        public ConsumableDefinition[] consumables;
        public ServiceDefinition[] services;
        public DieUpgradeDefinition[] diceUpgrades;
        public StageDefinition[] stages;
        public BossModifierDefinition[] bossModifiers;
        public LootKindDefinition[] lootKinds;
        public int lootGoldValue;
        public int lootHandLevels;
        public StageFlowConfig stageFlow;
        public ControllerOrderConfig controllerOrder;
        public ShopConfig shop;
        public GenerationConfig generation;
        public RngConfig rng;
        public PresentationConfig presentation;
        public DisplayConfig display;
        public string[] buildTargets;
        public DebugConfig debug;
        public int maxControllerSlots => maxControllerInstances;
        public int controllerSellPrice => baseControllerSellPrice;
    }

    [Serializable]
    public sealed class GrowthCatchupConfig
    {
        public int additive;
        public int @base;
        public int multiplier;
    }

    [Serializable]
    public sealed class DiceDefinition
    {
        public string id;
        public FaceDefinition[] faces;
        public bool isGlass;
    }

    [Serializable]
    public sealed class FaceDefinition
    {
        public string faceId;
        public int pipValue;
        public string colorId;
        public int baseWeight;
        public string texture;
        public bool isGold;
    }

    [Serializable]
    public sealed class FaceOpposites
    {
        public string F01;
        public string F06;
        public string F02;
        public string F05;
        public string F03;
        public string F04;
    }

    [Serializable]
    public sealed class RedReward
    {
        public int count;
        public int factor100;
        public string title;
    }

    [Serializable]
    public sealed class EnchantmentDefinition
    {
        public string id;
        public string displayName;
        public int weight;
        public int buyPremium;
        public int maxPerController;
    }

    [Serializable]
    public sealed class EnchantmentRules
    {
        public string[] rawDrawOrder;
        public string ineligibleScoreEnchantPolicy;
        public NonSubmitDrawWeights nonSubmitDrawWeights;
        public int triggerCountPerSourcePerSubmitMax;
        public int baseAdd;
        // v1.1.1: the score enchantment (stable id mult2) adds scoreAdd100 hundredths to the additive zone.
        public int scoreAdd100;
        public int submissionBonus;
        public int rollBonus;
        public string[] nonSubmitControllerIds;
        public bool postScorePredicatesUseFrozenSnapshot;
        public string submissionGrantTiming;
        public string rollGrantTiming;
        public bool copyTargetEnchantment;
        // v1.1.1: chance (percent) that a random shop offer carries an enchantment, by shop cycle 1-8.
        public int[] shopChanceByCyclePercent;
    }

    [Serializable]
    public sealed class NonSubmitDrawWeights
    {
        public int none;
        public int slotless;
        public int submit1;
        public int roll1;
    }

    [Serializable]
    public sealed class HandDefinition
    {
        public string id;
        public string displayName;
        public int baseMult100;
        public int incrementPerLevel100;
        public int baseUpgradePrice;
        public int maxLevel;
        public int structuralPriority;
        public int minSubmittedDice;
        public int requirePermanentDice;
    }

    [Serializable]
    public sealed class ColorDefinition
    {
        public string id;
        public string displayName;
        // v1.1.1: a colour is an additive bonus (hundredths), no longer a multiplier zone of its own.
        public int baseAdd100;
        public int addPerLevel100;
        public int baseUpgradePrice;
        public int maxLevel;
        public int minSubmittedDice;
        public bool countsAsHand;
    }

    [Serializable]
    public sealed class ControllerDefinition
    {
        public string id;
        public string displayName;
        public int buyPrice;
        public int baseSellPrice;
        public string salePriceMode;
        public string rarity;
        public int offerWeight;
        public string trigger;
        // v1.1: maxOwnedNormal removed (same-name cards may be held more than once; RULES_DELTA section 9).
        public bool scoreEnchantEligible;
        // v1.1.1: cultivation cards rewrite permanent die faces; the leftmost one without a slotless enchantment takes no slot.
        public bool isCultivation;
        public ControllerParameters parameters;
        public string description;
        public string shortText;
        public string details;
    }

    [Serializable]
    public sealed class ControllerParameters
    {
        public int goldPerValidSubmission;
        public int extraInterestPerTen;
        public int index;
        public int rollGrant;
        public int minUnlocked;
        public int maxUnlocked;
        public int perSourcePerHandLimit;
        public bool snapshotBeforeAnyGrant;
        public int minDistinct;
        public int submissionOrdinal;
        public int requiredDice;
        public int sourceIndex;
        public int targetIndex;
        public int capacityDelta;
        public int pip;
        public int perDieAdd;
        public RequiredPipCounts requiredCounts;
        public int oddToEvenAdd;
        public bool idempotent;
        public int oddPerDieAdd;
        public bool scanAllInteriorIndices;
        public string[] pattern;
        public bool freezeAllTargets;
        public int[] pips;
        public int lastActualCapacitySlotPip;
        public bool requireFullCapacity;
        public int redGroupKeyFrom;
        public int redGroupKeyTo;
        public bool mergeAllEffectiveRed;
        public string allPhysicalFacesColor;
        public int baseAddPerDistinctPairedPip;
        public int minCountPerGroup;
        public int minDistinctPairs;
        public int minimumStraightLength;
        public int minSamePip;
        public int distinctPips;
        public int minDice;
        public int firstMinusSecondAtLeast;
        public int[] targetPips;
        public int probabilityPointsPerSource;
        public bool requireEmptySlot;
        public int[] indices;
        public int delta;
        public string targetIndices;
        public string color;
        public string effectiveColor;
        public bool strictIncreasingArithmetic;
        public string target;
        public bool copyBaseAbility;
        public bool copyTargetEnchantment;
        public bool copyExistingFundAccrual;
        public bool shareDynamicTargetState;
        public bool retargetOnLossInStage;
        public int destroyChancePercentAfterValidSubmission;
        public string riskBearer;
        public bool samePrimaryHandAsPreviousValidSubmit;
        public int levelAddPerSource;
        public bool crossStageHistory;
        public bool scatterCanUpgrade;
        public int baseAddPerFrozenGold;
        public int matchingDieContributionMultiplier;
        public string[] colors;
        public bool initialDrawOnAcquisition;
        public bool nextDrawEveryValidSubmit;
        public bool sharedStateAdvancesOnce;
        public int baseAddPerEmptySubmitSlot;
        public int baseAdd;
        public bool mimicsDoNotDuplicateStateGrowth;
        public string saleRecipients;
        public int baseBuyPrice;
        public int baseSellPrice;
        public int sellPriceAddPerSuccessfulStageFinalize;
        public int[] factorUnits2;
        public int maxGrowthPerShop;
        public bool uniform;
        public bool drawOncePerRealOwnerPerSubmit;
        public bool mimicReusesOwnerOutcome;
        // v0.9 (docs/specs/v09/RULES_SPEC.md), added by hand. v1.1.1 removed the parameters no card uses any more
        // (scoreMultiplier, the unit-2/4/10/20 steps, growthPerEvent, growthPerBuyPrice, textures and others).
        public int minRedGroup;
        public int retriggers;
        public int baseAddPerPoolDie;
        public int poolPipSumMultiplier;
        public int stepNumerator;
        public int stepDenominator;
        public int maxRollsUsed;
        public int baseAddPerExtraRoll;
        public int rollsPerHandDelta;
        public int submissionsDelta;
        public int pairSum;
        public int baseAddPerPair;
        public string[] handIds;
        public int handMultAdd100;
        public int dieContributionMultiplier;
        public int goldPerStage;
        public int interestPrincipalAdd;
        public int goldPerTrigger;
        public int maxTriggersPerStage;
        public int targetDivisor;
        public int priceDelta;
        public int saleBonusPerStage;
        public int maxOwnedPermanentDice;
        public int strictMaxOwnedPermanentDice;
        // v1.0 (docs/specs/v10/RULES_SPEC.md), added by hand.
        public int baseAddPerPip;
        public int itemsPerStage;
        public int giftChoices;
        public int giftPicks;
        public string rarity;
        public int enchantsPerBoss;
        public int facesPerStage;
        public string[] upgradeIds;
        public int dicePerBoss;
        public int dicePerStage;
        public int goldPerBoss;
        public int goldPerPressure;
        public int baseAddPerItem;
        public int goldCost;
        public int goldPerStep;
        public int expireGrowth;
        // v1.1.1 (docs/specs/v111/RULES_DELTA.md 12.3), added by hand. Every key ending in 100 is the real value x 100.
        public int scoreMultiplier100;
        public int strictScoreMultiplier100;
        public int enhancedScoreMultiplier100;
        public int additiveBonus100;
        public int minOneAdd100;
        public int minTwoAdd100;
        public int additivePerController100;
        public int additivePerFive100;
        public int noFiveMultiplier100;
        public int additivePerPermanentDie100;
        public int additivePerMatch100;
        public int additivePerGoldStep100;
        public int initialAdditive100;
        public int decayPerClear100;
        public int additivePerPoolDie100;
        public int additivePerUnusedRoll100;
        public int additivePerPreviousSubmit100;
        public int additivePerEmptyOrdinarySlot100;
        public int additivePerCommon100;
        // Growth cards: growthKind is additive / base / multiplier; start and event are hundredths, or whole points for base.
        public string growthKind;
        public int growthStart;
        public int growthEvent;
        public int facesPerDie;
        public int lowPipDelta;
        public int highPipDelta;
        public int highPipFloor;
        public int freeUnlocksPerHand;
        public int minFours;
        public int minOnes;
    }

    [Serializable]
    public sealed class RequiredPipCounts
    {
        public int pip2;
        public int pip3;
    }

    [Serializable]
    public sealed class ConsumableDefinition
    {
        public string id;
        public string displayName;
        public int baseBuyPrice;
        public int sellPrice;
        public string window;
        public string lifetime;
        // v1.1.1: how many uses a new copy holds (I13 holds 3, every other item 1).
        public int initialUses;
        public string description;
        public string shortText;
        public string details;
    }

    [Serializable]
    public sealed class ServiceDefinition
    {
        public string id;
        public string displayName;
        public int baseBuyPrice;
        public string priceRule;
        public string description;
        public string shortText;
        public string details;
    }

    [Serializable]
    public sealed class DieUpgradeDefinition
    {
        public string id;
        public string displayName;
        public int buyPrice;
        public string target;
        // v1.1.1: faces of one die a single purchase may change (0 = the whole die, no face is chosen).
        public int maxSelectableFaces;
        public string description;
        public string shortText;
        public string details;
    }

    [Serializable]
    public sealed class StageDefinition
    {
        public string stageId;
        public int chapter;
        public int indexInChapter;
        public string stageKind;
        public string normalTarget;
        public int normalReward;
        public bool riskEnabled;
        public string riskTarget;
        public int riskReward;
        public bool shopAfterClear;
    }

    /// <summary>v1.0: a boss-stage debuff. tier 1-3 are the regular pool, tier 4 the final-boss pool.</summary>
    [Serializable]
    public sealed class BossModifierDefinition
    {
        public string id;
        public string displayName;
        public int tier;
        public int minCycle;
        public string[] effects;
        public int amount;
        public string filter;
        public string description;
        public string shortText;
        public string details;
    }

    /// <summary>v1.0: a kind of boss loot offered as a free group in the shop after a boss stage.</summary>
    [Serializable]
    public sealed class LootKindDefinition
    {
        public string id;
        public string displayName;
        public int weight;
        public int picks;
        public string description;
        public string shortText;
        public string details;
    }

    [Serializable]
    public sealed class StageFlowConfig
    {
        public bool latchQualified;
        public bool autoEndOnReached;
        public bool autoEndWhenNoSubmissions;
        public bool manualEndRequiresQualified;
        public bool manualEndDiscardsUnsubmittedHand;
        public int overScoreGold;
        public bool clearRewardOnlyAtFinalization;
        public bool submissionIncomeAppliedWithinSubmit;
    }

    [Serializable]
    public sealed class ControllerOrderConfig
    {
        public string[] reorderNormalPhases;
        public bool stageBindingsFrozen;
        public bool dragChangesOnlyOrder;
        public string onTargetLost;
        public int maxMimicResolveDepth;
    }

    [Serializable]
    public sealed class ShopConfig
    {
        public int initialControllerOffers;
        public int maxControllerOffers;
        public int initialConsumableOffers;
        public int maxConsumableOffers;
        public int upgradeOffers;
        public string[] upgradePoolIncludes;
        public int diceUpgradeOffers;
        public int serviceOffers;
        public int newDiceOffers;
        public int purchaseLimit;
        public bool restockPurchasedOffers;
        public int selfStudyLimit;
        public int selfStudyPremium;
        public int upgradePricePerExistingLevel;
        // v1.1: controllersUniqueOwned, allowDifferentEnchantReplacement, replacementCredit,
        // replacementCountsAsOneControllerSale and firstControllerGuaranteeUnownedOnly removed with the replacement rule.
        public int firstControllerGuaranteeMaxBasePrice;
        public ControllerWeights controllerWeights;
        public string sellingWindow;
        public string[] discountCategories;
        public bool paidRefreshEnabled;
        public string refreshTarget;
        public int firstRefreshPrice;
        public int refreshPriceStep;
        public bool freeRefreshConsumesFirstIndex;
        public string refreshCounterReset;
        public bool offerExpansionAffectsCurrentShop;
        public int maxOfferExpansionPurchases;
        // v1.1.1: self-chosen die upgrades per shop, their surcharge, and the multiplier cards that are never offered while owned.
        public int selfDieUpgradeLimit;
        public int selfDieUpgradePremium;
        public string[] uniqueControllerIds;
        public int firstControllerGuaranteeMaxPrice => firstControllerGuaranteeMaxBasePrice;
        public int controllerOffers => initialControllerOffers;
        public int consumableOffers => initialConsumableOffers;
        public int handUpgradeOffers => upgradeOffers;
    }

    [Serializable]
    public sealed class ControllerWeights
    {
        public int common;
        public int uncommon;
        public int rare;
    }

    [Serializable]
    public sealed class GenerationConfig
    {
        public TemplateWeights newDieTemplateWeights;
        public int[] pipRange;
        public int[] lowRange;
        public int[] highRange;
        public int redFaceChancePercent;
        public int newDieBasePrice;
        public int maxNewDieBasePrice;
        public int highSumThreshold;
        public int highSumPremium;
        public int smallSumThreshold;
        public int smallSumPremium;
        public int maxRepeatThreshold;
        public int maxRepeatPremium;
        public int sameColorThreshold;
        public int sameColorPremium;
    }

    [Serializable]
    public sealed class TemplateWeights
    {
        public int random;
        public int low;
        public int high;
    }

    [Serializable]
    public sealed class RngConfig
    {
        public string algorithm;
        public uint diceSeedXor;
        public uint shopSeedXor;
        public uint effectSeedXor;
        public uint stageSeedXor;
        public uint zeroSeedReplacement;
        public string[] submitEffectDrawOrder;
    }

    [Serializable]
    public sealed class PresentationConfig
    {
        public float rollSeconds;
        public float fastRollSeconds;
        public int referenceWidth;
        public int referenceHeight;
        public int handbookHandRows;
        public int handbookColorRows;
        public int contentIconCount;
        public int enchantmentBadgeCount;
        public int handbookRows => handbookHandRows + handbookColorRows;
    }

    [Serializable]
    public sealed class DisplayConfig
    {
        public string initialMode;
        public int windowWorkAreaRatioPercent;
        public int preferredAspectWidth;
        public int preferredAspectHeight;
        public int confirmationTimeoutSeconds;
        public bool persistOnlyConfirmedSettings;
        public PlatformDisplayModes windowsModes;
        public PlatformDisplayModes macModes;
    }

    [Serializable]
    public sealed class PlatformDisplayModes
    {
        public string fullscreen;
        public string borderless;
        public string windowed;
    }

    [Serializable]
    public sealed class DebugConfig
    {
        public string unlockPhrase;
        public bool caseSensitive;
        public bool trimInput;
        public bool availableInPlayableBuilds;
        public bool separateSaveSlot;
        public bool markRunAsDebugOnFirstMutation;
        public bool allowDuplicateControllerIds;
        public int maxDice;
        public int maxSubmitCapacity;
        public int maxControllerInstances;
        public int maxConsumableInventory;
        public int maxHandLevel;
        public int maxColorLevel;
        public int maxEditableWallet;
        public int maxEditableBudget;
        public int minPipValue;
        public int maxPipValue;
        public int redGroupCapForExperiments;
        public string highestDefinedKind;
        public bool supportsStageJump;
        public bool debugMutationResetsHand;
        public bool debugFundAndStateEditing;
        public int maxControllerSlots => maxControllerInstances;
    }

}
