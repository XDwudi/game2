using System;
using System.Collections.Generic;
using System.Linq;

namespace DiceCraft.Core
{
    [Serializable]
    public sealed class RandomRecord
    {
        public string stream, kind, sourceId, rawValue, appliedValue;
        public uint beforeState, afterState;
        public int range, rawTicket;
        public bool overridden;
        public RandomRecord Clone() => (RandomRecord)MemberwiseClone();
    }

    /// <summary>One-shot branch override. kind: C53, C45, C42 or Glass; value: units2, red/blue or break/keep.</summary>
    [Serializable]
    public sealed class DebugRandomOverride
    {
        public string kind, sourceId, value;
        public DebugRandomOverride Clone() => (DebugRandomOverride)MemberwiseClone();
    }

    /// <summary>Importable data only. No receipts or executable content, and therefore no recursive RunState graph.</summary>
    [Serializable]
    public sealed class DebugScene
    {
        public string configVersion, sourceRunId, note, previousPrimaryHandId;
        public bool isDebug, risk;
        public long gold, submissionOrdinalGlobal;
        public uint seed, diceRngState, shopRngState, effectRngState;
        public int stageIndex, capacityOverride = 5, consumableSlots = 2, itemDiscount;
        public int controllerOfferPurchases, consumableOfferPurchases;
        public bool freeFirstRefreshOwned;
        public string target = "180", score = "0";
        // -1 means use the freshly prepared default. Zero is an explicit experimental budget.
        public int remainingSubmissions = -1, rollsRemaining = -1, submissionOrdinal, stageRollBonus;
        public bool redWave, largeBuff, smallBuff;
        // v0.9: rolls already thrown this hand (0 = derive from the results), C97 triggers used this stage, bought slots.
        public int rollsUsed, warIncomeCount, controllerSlotPurchases;
        // v1.0: the boss modifier forced onto the scene (empty = none), hands already scored this stage, item effects
        // of the hand, capacity added by items, and the O10 purchase count.
        public string modifierId = "";
        public List<string> primaryHandsUsed = new List<string>();
        public bool blueWave, doubleScore;
        public int itemCapacityBonus, interestCapPurchases;
        // v1.1.1: the records of the debug run itself (never the formal all-time record) and whether a submission of
        // the scene's stage already beat the highest hand. Empty strings mean the defaults 0 and 0/1. The star pip of
        // a C137 (0 = no value this stage, or 1-6), the growth and catch-up of growth cards, the rewrite count of
        // cultivation cards and the cultivated mark of a face travel inside `controllers` and `dice`.
        public string highestHandScore = "0", highestClearRatioNumerator = "0", highestClearRatioDenominator = "1";
        public bool handRecordBroken;
        public List<DieInstance> dice = new List<DieInstance>();
        public List<ControllerInstance> controllers = new List<ControllerInstance>();
        public List<OwnedItem> inventory = new List<OwnedItem>();
        public List<HandLevel> handLevels = new List<HandLevel>(), colorLevels = new List<HandLevel>();
        // null selects new-hand defaults; explicit results must include every die.
        public List<DieHandState> currentResults;
        public List<string> orderedLockedIds = new List<string>(), c05UsedEmitterIds = new List<string>();
        // null binds the current order. An explicit empty array intentionally binds nothing.
        public MimicBinding[] mimicBindings;
        public List<DebugRandomOverride> randomOverrides = new List<DebugRandomOverride>();

        public static DebugScene FromState(RunState s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            return new DebugScene {
                configVersion = s.configVersion, sourceRunId = s.sourceRunId ?? s.runId, isDebug = s.isDebug,
                gold = s.gold, seed = s.seed, diceRngState = s.diceRngState, shopRngState = s.shopRngState, effectRngState = s.effectRngState,
                stageIndex = s.stage.stageIndex, risk = s.stage.risk, target = s.stage.target, score = s.stage.score,
                capacityOverride = s.debugCapacityOverride > 0 ? s.debugCapacityOverride : s.stage.capacity,
                remainingSubmissions = s.stage.remainingSubmissions, submissionOrdinal = s.stage.submissionOrdinal, stageRollBonus = s.stage.stageRollBonus,
                consumableSlots = s.consumableSlots, itemDiscount = s.itemDiscount,
                controllerOfferPurchases = s.controllerOfferPurchases, consumableOfferPurchases = s.consumableOfferPurchases, freeFirstRefreshOwned = s.freeFirstRefreshOwned,
                previousPrimaryHandId = s.previousPrimaryHandId, submissionOrdinalGlobal = s.submissionOrdinalGlobal,
                dice = s.dice.Select(x => x.Clone()).ToList(), controllers = s.controllers.Select(x => x.Clone()).ToList(),
                inventory = s.inventory.Select(x => x.Clone()).ToList(), handLevels = s.handLevels.Select(x => x.Clone()).ToList(), colorLevels = s.colorLevels.Select(x => x.Clone()).ToList(),
                rollsRemaining = s.hand == null ? -1 : s.hand.rollsRemaining,
                rollsUsed = s.hand == null ? 0 : s.hand.rollsUsed, warIncomeCount = s.stage.warIncomeCount, controllerSlotPurchases = s.controllerSlotPurchases,
                modifierId = s.stage.started && !s.stage.settled && !s.stage.modifierNullified ? s.stage.modifierId ?? "" : "", primaryHandsUsed = new List<string>(s.stage.primaryHandsUsed),
                blueWave = s.hand != null && s.hand.blueWave, doubleScore = s.hand != null && s.hand.doubleScore, itemCapacityBonus = s.stage.itemCapacityBonus, interestCapPurchases = s.interestCapPurchases,
                highestHandScore = s.highestHandScore, highestClearRatioNumerator = s.highestClearRatioNumerator, highestClearRatioDenominator = s.highestClearRatioDenominator,
                handRecordBroken = s.stage.started && !s.stage.settled && s.stage.handRecordBroken,
                redWave = s.hand != null && s.hand.redWave, largeBuff = s.hand != null && s.hand.largeBuff, smallBuff = s.hand != null && s.hand.smallBuff,
                currentResults = s.hand == null ? null : s.hand.currentResults.Select(x => x.Clone()).ToList(),
                orderedLockedIds = s.hand == null ? new List<string>() : new List<string>(s.hand.orderedLockedIds),
                c05UsedEmitterIds = s.hand == null ? new List<string>() : new List<string>(s.hand.c05UsedEmitterIds),
                mimicBindings = s.stage.started && !s.stage.settled ? s.stage.mimicBindings.Select(CloneBinding).ToArray() : null,
                randomOverrides = s.randomOverrides.Select(x => x.Clone()).ToList()
            };
        }

        public DebugScene Clone()
        {
            var c = (DebugScene)MemberwiseClone();
            c.dice = dice?.Select(x => x?.Clone()).ToList(); c.controllers = controllers?.Select(x => x?.Clone()).ToList();
            c.inventory = inventory?.Select(x => x?.Clone()).ToList(); c.handLevels = handLevels?.Select(x => x?.Clone()).ToList(); c.colorLevels = colorLevels?.Select(x => x?.Clone()).ToList();
            c.currentResults = currentResults?.Select(x => x?.Clone()).ToList();
            c.orderedLockedIds = orderedLockedIds == null ? null : new List<string>(orderedLockedIds);
            c.primaryHandsUsed = primaryHandsUsed == null ? new List<string>() : new List<string>(primaryHandsUsed);
            c.c05UsedEmitterIds = c05UsedEmitterIds == null ? null : new List<string>(c05UsedEmitterIds);
            c.mimicBindings = mimicBindings?.Select(CloneBinding).ToArray(); c.randomOverrides = randomOverrides?.Select(x => x?.Clone()).ToList();
            return c;
        }
        private static MimicBinding CloneBinding(MimicBinding b) => b == null ? null : new MimicBinding { sourceInstanceId = b.sourceInstanceId, targetInstanceId = b.targetInstanceId };
    }
}
