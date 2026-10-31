// Generated from authoritative P0 v0.6 Appendix A; see tools/generate_v06_dtos.py.
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
        public string interestBalanceTiming;
        public int initialControllerSlots;
        public int maxControllerInstances;
        public bool slotlessConsumesBaseSlot;
        public int initialConsumableSlots;
        public int maxConsumableSlots;
        public int baseControllerSellPrice;
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
        public int goldContributionMultiplier;
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
        public int scoreMultiplier;
        public int submissionBonus;
        public int rollBonus;
        public string[] nonSubmitControllerIds;
        public bool postScorePredicatesUseFrozenSnapshot;
        public string submissionGrantTiming;
        public string rollGrantTiming;
        public bool copyTargetEnchantment;
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
        public int baseMult100;
        public int incrementPerLevel100;
        public int baseUpgradePrice;
        public int maxLevel;
        public int minSubmittedDice;
        public string multiplierZone;
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
        public int maxOwnedNormal;
        public bool scoreEnchantEligible;
        public ControllerParameters parameters;
        public string description;
    }

    [Serializable]
    public sealed class ControllerParameters
    {
        public int goldPerValidSubmission;
        public int extraInterestPerTen;
        public int scoreMultiplier;
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
        public int maxPermanentSixFaceSum;
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
        public string factor;
        public int baseAddPerEmptySubmitSlot;
        public bool allowZero;
        public int baseAdd;
        public int initialMultiplierUnits2;
        public int addUnits2PerRealControllerSale;
        public bool mimicsDoNotDuplicateStateGrowth;
        public string saleRecipients;
        public int baseBuyPrice;
        public int baseSellPrice;
        public int sellPriceAddPerSuccessfulStageFinalize;
        public int[] factorUnits2;
        public bool uniform;
        public bool drawOncePerRealOwnerPerSubmit;
        public bool mimicReusesOwnerOutcome;
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
        public string description;
    }

    [Serializable]
    public sealed class ServiceDefinition
    {
        public string id;
        public string displayName;
        public int baseBuyPrice;
        public string priceRule;
        public string description;
    }

    [Serializable]
    public sealed class DieUpgradeDefinition
    {
        public string id;
        public string displayName;
        public int buyPrice;
        public string target;
        public string description;
    }

    [Serializable]
    public sealed class StageDefinition
    {
        public string stageId;
        public int chapter;
        public int indexInChapter;
        public string normalTarget;
        public int normalReward;
        public bool riskEnabled;
        public string riskTarget;
        public int riskReward;
        public bool shopAfterClear;
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
        public bool controllersUniqueOwned;
        public bool allowDifferentEnchantReplacement;
        public string replacementCredit;
        public bool replacementCountsAsOneControllerSale;
        public int firstControllerGuaranteeMaxBasePrice;
        public bool firstControllerGuaranteeUnownedOnly;
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
