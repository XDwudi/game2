using System;

namespace DiceCraft.Core
{
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
    public sealed class DiceDefinition
    {
        public string id;
        public FaceDefinition[] faces;
        public bool isGlass;
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
        public int finalScoreAdd;
        public bool requireFullCapacity;
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
        public ControllerParameters parameters;
        public string description;
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
        public int normalTarget;
        public int normalReward;
        public bool riskEnabled;
        public int riskTarget;
        public int riskReward;
        public bool shopAfterClear;
    }

    [Serializable]
    public sealed class ControllerWeights
    {
        public int common;
        public int uncommon;
        public int rare;
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
        public int firstControllerGuaranteeMaxPrice;
        public ControllerWeights controllerWeights;
        public string sellingWindow;
        public string[] discountCategories;
    }

    [Serializable]
    public sealed class TemplateWeights
    {
        public int random;
        public int low;
        public int high;
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
    }

    [Serializable]
    public sealed class DebugConfig
    {
        public bool markRunAsDebugOnUse;
        public int maxControllerSlots;
        public bool unlimitedGold;
        public bool freeUnlockTestOnly;
    }

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
        public int totalRollsPerHand;
        public int submissionsPerStage;
        public int bonusPerUnusedRoll;
        public int initialGold;
        public int baseStageReward;
        public int riskStageReward;
        public int goldPerUnusedSubmission;
        public int interestDivisor;
        public int interestCap;
        public string interestBalanceTiming;
        public int initialControllerSlots;
        public int maxControllerSlots;
        public bool controllerSlotUpgradeInCatalog;
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
        public int smoothOppositeWeight100;
        public int smoothOtherWeight100;
        public int roughOppositeWeight100;
        public int roughOtherWeight100;
        public int temporaryPipBiasWeight100;
        public int glassBreakPercent;
        public int glassContributionMultiplier;
        public int goldContributionMultiplier;
        public RedReward[] redRewards;
        public HandDefinition[] handDefinitions;
        public ControllerDefinition[] controllers;
        public ConsumableDefinition[] consumables;
        public ServiceDefinition[] services;
        public DieUpgradeDefinition[] diceUpgrades;
        public StageDefinition[] stages;
        public ShopConfig shop;
        public GenerationConfig generation;
        public RngConfig rng;
        public PresentationConfig presentation;
        public DebugConfig debug;
    }

}

