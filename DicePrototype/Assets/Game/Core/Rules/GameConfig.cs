// Serializable fields mirror authoritative P0 v0.5 Appendix A.
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
        public int initialDiceCount;
        public int maxPermanentDice;
        public int maxActiveDice;
        public int initialSubmitCapacity;
        public int maxSubmitCapacity;
        public int maxTemporaryDice;
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
        public int controllerSellPrice;
        public int consumableSellPrice;
        public int minDiscountedItemPrice;
        public int maxItemDiscount;
        public int minDiceForColorHand;
        public int minStraightLength;
        public int minPipValue;
        public int maxPipValue;
        public int maxHandLevel;
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
        public ControllerDefinition[] controllers;
        public ConsumableDefinition[] consumables;
        public ServiceDefinition[] services;
        public DieUpgradeDefinition[] diceUpgrades;
        public StageDefinition[] stages;
        public StageFlowConfig stageFlow;
        public ShopConfig shop;
        public GenerationConfig generation;
        public RngConfig rng;
        public PresentationConfig presentation;
        public string[] buildTargets;
        public DebugConfig debug;
        // Source compatibility only: this is the total instance limit, never ordinary occupied slots.
        public int maxControllerSlots => maxControllerInstances;
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
        public string ineligibleScoreEnchantFallback;
        public int triggerCountPerSubmitMax;
        public int baseAdd;
        public int multiplierNumerator;
        public int multiplierDenominator;
        public string[] nonSubmitControllerIds;
        public bool postScorePredicatesUseFrozenSnapshot;
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
        public int tieBreakRank;
        public int minSubmittedDice;
        public bool enabledInNormalShop;
        public int requirePermanentDice;
    }

    [Serializable]
    public sealed class ControllerDefinition
    {
        public string id;
        public string displayName;
        public int buyPrice;
        public int sellPrice;
        public string rarity;
        public int offerWeight;
        public string trigger;
        public int maxOwned;
        public bool scoreEnchantEligible;
        public ControllerParameters parameters;
        public string description;
    }

    [Serializable]
    public sealed class ControllerParameters
    {
        public int extraInterestPerTen;
        public int scoreMultiplier;
        public int occupiedIndex;
        public int submissionBonus;
        public int rollBonus;
        public int rollGrant;
        public int perHandLimit;
        public int minUnlocked;
        public int maxUnlocked;
        public bool checkOnlyAfterRoll;
        public int minDistinct;
        public bool onlyFirstSubmission;
        public int requiredDice;
        public int sourceIndex;
        public int targetIndex;
        public int capacityDelta;
        public int pip;
        public int contributionAdd;
        public int pipA;
        public int countA;
        public int pipB;
        public int countB;
        public int oddPipAdd;
        public int oddContributionAdd;
        public string pattern;
        public int maxPip;
        public int maxPermanentFaceSum;
        public int lastSlotPip;
        public int dieContributionAdd;
        public bool requireFullCapacity;
        public int redGroupPipFrom;
        public int redGroupPipTo;
        public bool mergeAllRed;
        public string allFacesColor;
        public int minSamePip;
        public int baseAdd;
        public int perSubmitLimit;
        public int minDifferentPairs;
        public int minimumStraightLength;
        public int requiredDistinct;
        public int minDice;
        public int firstMinusSecondAtLeast;
        public int[] targetPips;
        public int maxTransferUnits;
        public bool requireEmptySlot;
        public int[] indices;
        public int permanentFaceDelta;
        public int index;
        public string currentPhysicalFaceColor;
        public string effectiveColor;
        public bool positiveCommonDifference;
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
        public int normalTarget;
        public int normalReward;
        public bool riskEnabled;
        public int riskTarget;
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
        public bool rewardOnlyAtFinalization;
    }

    [Serializable]
    public sealed class ShopConfig
    {
        public int controllerOffers;
        public int consumableOffers;
        public int handUpgradeOffers;
        public int diceUpgradeOffers;
        public int serviceOffers;
        public int newDiceOffers;
        public int purchaseLimit;
        public bool restockPurchasedOffers;
        public bool paidRefreshEnabled;
        public int selfStudyLimit;
        public int selfStudyPremium;
        public int upgradePricePerExistingLevel;
        public bool controllersUniqueOwned;
        public bool allowDifferentEnchantReplacement;
        public int replaceSaleCredit;
        public int firstControllerGuaranteeMaxBasePrice;
        public bool firstControllerGuaranteeUnownedOnly;
        public ControllerWeights controllerWeights;
        public string sellingWindow;
        public string[] discountCategories;
        public int firstControllerGuaranteeMaxPrice => firstControllerGuaranteeMaxBasePrice;
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
    }

    [Serializable]
    public sealed class PresentationConfig
    {
        public float rollSeconds;
        public float fastRollSeconds;
        public int referenceWidth;
        public int referenceHeight;
        public int handbookRows;
    }

    [Serializable]
    public sealed class DebugConfig
    {
        public bool markRunAsDebugOnUse;
        public int maxControllerInstances;
        public bool freeUnlockTestOnly;
        // Source compatibility only: this is the total instance limit, never ordinary occupied slots.
        public int maxControllerSlots => maxControllerInstances;
    }

}
