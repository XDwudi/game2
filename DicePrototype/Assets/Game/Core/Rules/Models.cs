using System;
using System.Linq;

namespace DiceCraft.Core
{
    [Serializable]
    public sealed class ControllerInstance
    {
        public string instanceId, definitionId;
        public string enchantmentId = "none";
        public int acquiredSerial;
        // acquiredSerial is the schema's creation serial; keep the established persistent key.
        public int createdSerial { get => acquiredSerial; set => acquiredSerial = value; }
        public bool alive = true;
        public ControllerDynamicState state = new ControllerDynamicState();
        // Nonserialized compatibility alias for generic content presentation.
        public string contentId { get => definitionId; set => definitionId = value; }
        public ControllerInstance Clone() { var copy = (ControllerInstance)MemberwiseClone(); copy.state = state?.Clone(); return copy; }
    }

    [Serializable]
    public sealed class ControllerDynamicState
    {
        public string activeColor;
        public int turnoverUnits2 = 3;
        public long fundAccrual;
        // v0.9: permanent growth counter (0.05 steps for multiplier cards, raw points for base-add cards) and the
        // resale bonus granted by C99. Both stay with the real instance and are lost when it is sold or destroyed.
        // v1.1.1: `growth` is what the card has grown through its events, in hundredths for additive and multiplier
        // growth cards and in whole points for base growth cards (C141 counts cleared stages in it, 0-3).
        public long growth;
        public long saleBonus;
        // v1.1.1: head start fixed when the card was offered in a later shop cycle (same unit as growth); the star pip
        // drawn for a real C137 when a stage starts (0 = none yet); permanent faces a cultivation card has rewritten (0-24).
        public long growthCatchup;
        public int starPip;
        public int cultivationFacesChanged;
        public ControllerDynamicState Clone() => (ControllerDynamicState)MemberwiseClone();
    }
    [Serializable]
    public sealed class MimicBinding
    {
        public string sourceInstanceId, targetInstanceId;
        public MimicBinding Clone() => (MimicBinding)MemberwiseClone();
    }
    [Serializable]
    public sealed class RandomMultiplierChoice
    {
        public string ownerInstanceId;
        public int units2;
        public RandomMultiplierChoice Clone() => (RandomMultiplierChoice)MemberwiseClone();
    }
    [Serializable]
    public sealed class AbilitySource
    {
        public string emitterId, emitterDefinitionId, abilityId, stateOwnerId, enchantmentId;
        public ControllerDynamicState state;
        public int orderIndex;
    }

    [Serializable]
    public sealed class FaceState
    {
        public string faceId;
        public int permanentPip;
        public string baseColor;
        public int baseWeight = 1;
        public string texture = "none";
        public bool isGold;
        // v1.1.1: a card has rewritten this permanent face at least once; never cleared afterwards.
        public bool cultivated;
        public FaceState Clone() => (FaceState)MemberwiseClone();
    }

    [Serializable]
    public sealed class DieInstance
    {
        public string dieId;
        public int createdSerial;
        public string lifetime;
        public string createdStageId;
        public FaceState[] faces;
        public bool isGlass;
        public int dataRevision;

        public DieInstance Clone()
        {
            var result = (DieInstance)MemberwiseClone();
            result.faces = faces.Select(f => f.Clone()).ToArray();
            return result;
        }
    }

    [Serializable]
    public sealed class SubmissionDie
    {
        public string dieId, faceId;
        public int currentPip, permanentSixFaceSum;
        public string currentColor;
        public bool isGold, isGlass;
        public FaceState[] permanentFacesCopy;
        public string lifetime;
        public SubmissionDie Clone()
        {
            var result = (SubmissionDie)MemberwiseClone();
            result.permanentFacesCopy = permanentFacesCopy?.Select(f => f.Clone()).ToArray();
            return result;
        }
    }

    /// <summary>v0.9: a rolled, surviving die that was left in the pool instead of being submitted.</summary>
    [Serializable]
    public sealed class PoolDie
    {
        public string dieId, faceId, currentColor;
        public int currentPip;
        public PoolDie Clone() => (PoolDie)MemberwiseClone();
    }

    [Serializable]
    public sealed class HandLevel
    {
        public string handId;
        public int level;
        public HandLevel Clone() => (HandLevel)MemberwiseClone();
    }

    [Serializable]
    public sealed class SubmissionSnapshot
    {
        public string runId, stageId, handId, configVersion;
        public SubmissionDie[] dice;
        public int capacity, rollsRemaining, submissionOrdinalBefore;
        public bool redWave, debug;
        public long wallet;
        public string previousPrimaryHandId;
        public string[] equippedControllerIds = Array.Empty<string>();
        public ControllerInstance[] equippedControllers = Array.Empty<ControllerInstance>();
        public HandLevel[] handLevels = Array.Empty<HandLevel>();
        public HandLevel[] colorLevels = Array.Empty<HandLevel>();
        // Null means preview adjacency; empty means a frozen map with no bindings.
        public MimicBinding[] mimicBindings;
        public RandomMultiplierChoice[] resolvedRandomChoices = Array.Empty<RandomMultiplierChoice>();
        // v0.9 frozen context: rolls thrown this hand, submissions left before this one, owned permanent dice, pool dice.
        public int rollsUsed, submissionsRemainingBefore, permanentDiceCount;
        public PoolDie[] poolDice = Array.Empty<PoolDie>();
        // v1.0 frozen context: stage kind, the active boss modifier (empty when none or nullified), controllers disabled
        // for this stage, primary hands already scored this stage, inventory size, ordinary slots, item effects.
        public string stageKind, modifierId;
        public string[] disabledControllerIds = Array.Empty<string>(), stageHandsUsed = Array.Empty<string>();
        public int inventoryCount, ordinaryControllerSlots, scoreItemMultiplier;
        public bool blueWave;
        // v1.1.1 frozen context: the stage score before this submission and the effective stage target, as decimal
        // strings. Null or empty means 0 and 1; a snapshot with an effective C156 source must state both.
        public string stageScoreBefore, stageTarget;
        public SubmissionSnapshot Clone()
        {
            var result = (SubmissionSnapshot)MemberwiseClone();
            result.dice = dice?.Select(d => d.Clone()).ToArray();
            result.equippedControllerIds = (string[])(equippedControllerIds ?? Array.Empty<string>()).Clone();
            result.equippedControllers = (equippedControllers ?? Array.Empty<ControllerInstance>()).Select(c => c.Clone()).ToArray();
            result.handLevels = (handLevels ?? Array.Empty<HandLevel>()).Select(h => h.Clone()).ToArray();
            result.colorLevels = (colorLevels ?? Array.Empty<HandLevel>()).Select(h => h.Clone()).ToArray();
            result.mimicBindings = mimicBindings?.Select(b => b.Clone()).ToArray();
            result.resolvedRandomChoices = (resolvedRandomChoices ?? Array.Empty<RandomMultiplierChoice>()).Select(c => c.Clone()).ToArray();
            result.poolDice = (poolDice ?? Array.Empty<PoolDie>()).Select(d => d.Clone()).ToArray();
            result.disabledControllerIds = (string[])(disabledControllerIds ?? Array.Empty<string>()).Clone();
            result.stageHandsUsed = (string[])(stageHandsUsed ?? Array.Empty<string>()).Clone();
            return result;
        }
    }

    [Serializable]
    public sealed class ScoreBreakdown
    {
        public int[] effectivePips;
        public long[] pipContributions;
        public string[] effectiveColors, matchedGroupIds;
        public string chosenGroupId, primaryHandId, colorId;
        public string pipSum;
        public int unusedRollBonus;
        public int groupMult100, straightMult100, totalHandMult100;
        public int redValue, redCount, redMult100;
        public string controllerMultiplierNumerator, controllerMultiplierDenominator;
        public int pairKinds, pairBaseAdd, emptySlotBaseAdd, steadyBaseAdd, enchantBaseAdd, enchantBaseCount;
        public string walletBaseAdd, baseTotal;
        public bool redRewardCapped;
        public string redGroupingMode, scoreNumerator, scoreDenominator;
        public string[] triggeredMultiplierIds, triggeredControllerIds, triggeredSourceIds;
        public MultiplierFactor[] multiplierFactors;
        public AfterScorePlan afterScorePlan;
        public string score;
        // v0.9: per-die retrigger counts (pipSum = sum of contribution x (1 + retriggers)), hand multiplier add,
        // pool dice merged into the red group, and the base adds of the v0.9 controllers in source order.
        public int[] pipRetriggers = Array.Empty<int>();
        public RetriggerEntry[] retriggerEntries = Array.Empty<RetriggerEntry>();
        public int handMultAdd100, redPoolCount;
        public ControllerBaseAdd[] controllerBaseAdds = Array.Empty<ControllerBaseAdd>();
        // v1.0: dice sealed by the boss modifier (contribution 0), factors outside the controller chain (item x2,
        // boss half), the hand multiplier before NoRepeatHand / HandHalf, and the extra red count from C146.
        public bool[] sealedDice = Array.Empty<bool>();
        public MultiplierFactor[] globalFactors = Array.Empty<MultiplierFactor>();
        public int handMultBeforeModifier100, redBonusCount;
        // v1.1.1: the additive zone. colorAdd100 is the colour bonus actually applied; additiveEntries lists every
        // bonus in order (colour, gold faces, card abilities, score enchantments); the score is multiplied by
        // (100 + additiveTotal100) / 100; enchantAdditiveCount is the number of score-enchantment entries.
        public int colorAdd100, enchantAdditiveCount;
        public long additiveTotal100;
        public AdditiveEntry[] additiveEntries = Array.Empty<AdditiveEntry>();
        // LEGACY-GOLDEN: the golden-vector tests of p1.1.0 and earlier still read the old field name. They are kept
        // unchanged (and fail) until the independent reference is regenerated for p1.1.1; this read-only alias only
        // lets them compile. Not a field, so it is neither saved nor part of the contract. Remove it with those vectors.
        // A few interface readers still show it as the "colour" factor (docs/specs/v111/W3A_NOTES.md lists them); it is
        // 1 + the colour bonus only and ignores every other additive entry, so they must move to additiveTotal100.
        public int colorMult100 => 100 + colorAdd100;
        public ScoreBreakdown Clone()
        {
            var result = (ScoreBreakdown)MemberwiseClone();
            result.effectivePips = (int[])effectivePips.Clone();
            result.pipContributions = (long[])pipContributions.Clone();
            result.effectiveColors = (string[])effectiveColors.Clone();
            result.matchedGroupIds = (string[])matchedGroupIds.Clone();
            result.triggeredMultiplierIds = (string[])triggeredMultiplierIds.Clone();
            result.triggeredControllerIds = (string[])triggeredControllerIds.Clone();
            result.triggeredSourceIds = (string[])triggeredSourceIds.Clone();
            result.multiplierFactors = multiplierFactors.Select(f => f.Clone()).ToArray();
            result.afterScorePlan = afterScorePlan.Clone();
            result.pipRetriggers = (int[])(pipRetriggers ?? Array.Empty<int>()).Clone();
            result.retriggerEntries = (retriggerEntries ?? Array.Empty<RetriggerEntry>()).Select(a => a.Clone()).ToArray();
            result.controllerBaseAdds = (controllerBaseAdds ?? Array.Empty<ControllerBaseAdd>()).Select(a => a.Clone()).ToArray();
            result.sealedDice = (bool[])(sealedDice ?? Array.Empty<bool>()).Clone();
            result.globalFactors = (globalFactors ?? Array.Empty<MultiplierFactor>()).Select(f => f.Clone()).ToArray();
            result.additiveEntries = (additiveEntries ?? Array.Empty<AdditiveEntry>()).Select(e => e.Clone()).ToArray();
            return result;
        }
    }

    [Serializable]
    public sealed class MultiplierFactor
    {
        public string emitterId, abilityId, stateOwnerId, numerator, denominator;
        public MultiplierFactor Clone() => (MultiplierFactor)MemberwiseClone();
    }
    /// <summary>v1.1.1: one bonus of the additive zone. kind is color, gold, controller or enchantment; abilityId is the
    /// colour id, U06, the card ability id or mult2. emitterId and stateOwnerId are null without a card source.
    /// dieIndex is the zero-based submitted index of a gold face and -1 otherwise (JsonUtility has no nullable int).
    /// amount100 is the bonus in hundredths and always positive.</summary>
    [Serializable]
    public sealed class AdditiveEntry
    {
        public string kind, abilityId, emitterId, stateOwnerId;
        public int dieIndex = -1;
        public long amount100;
        public AdditiveEntry Clone() => (AdditiveEntry)MemberwiseClone();
    }
    /// <summary>v1.1.1: a triggered source of one of the older face rewrites (C07, C26, C36, C37, C15, C38), so the
    /// session can credit the real owner's cultivation count.</summary>
    [Serializable]
    public sealed class CultivationSourceBinding
    {
        public string abilityId, emitterId, stateOwnerId;
        public CultivationSourceBinding Clone() => (CultivationSourceBinding)MemberwiseClone();
    }
    [Serializable]
    public sealed class AfterScorePlan
    {
        public GrowthChange[] growthChanges = Array.Empty<GrowthChange>();
        public int earnedGold;
        public bool copyFirstToSecond;
        public int[] redSpreadIndices = Array.Empty<int>();
        public bool dyeAllFacesRed;
        public int growthNetDelta;
        public int[] growthIndices = Array.Empty<int>(), blueSpreadIndices = Array.Empty<int>();
        public string upgradeHandId;
        public int upgradeRequested, upgradeApplied;
        public string nextPreviousPrimary;
        public string[] nextColorOwnerIds = Array.Empty<string>(), randomFactorOwnerIds = Array.Empty<string>(), cannonRiskEmitterIds = Array.Empty<string>();
        // v1.0: permanent face changes applied in order after the older effects, and gold spent by C138.
        public FaceChange[] faceChanges = Array.Empty<FaceChange>();
        public int goldSpent;
        // v1.1.1: submitted dice that were glass when frozen, in submission order (the session draws their break
        // tickets), and the sources behind the older face rewrites in the order those rewrites are applied.
        public string[] glassRiskDieIds = Array.Empty<string>();
        public CultivationSourceBinding[] cultivationSourceBindings = Array.Empty<CultivationSourceBinding>();
        public AfterScorePlan Clone()
        {
            var c = (AfterScorePlan)MemberwiseClone();
            c.redSpreadIndices = (int[])redSpreadIndices.Clone(); c.growthIndices = (int[])growthIndices.Clone(); c.blueSpreadIndices = (int[])blueSpreadIndices.Clone();
            c.nextColorOwnerIds = (string[])nextColorOwnerIds.Clone(); c.randomFactorOwnerIds = (string[])randomFactorOwnerIds.Clone(); c.cannonRiskEmitterIds = (string[])cannonRiskEmitterIds.Clone();
            c.growthChanges = (growthChanges ?? Array.Empty<GrowthChange>()).Select(g => g.Clone()).ToArray();
            c.faceChanges = (faceChanges ?? Array.Empty<FaceChange>()).Select(f => f.Clone()).ToArray();
            c.glassRiskDieIds = (string[])(glassRiskDieIds ?? Array.Empty<string>()).Clone();
            c.cultivationSourceBindings = (cultivationSourceBindings ?? Array.Empty<CultivationSourceBinding>()).Select(b => b.Clone()).ToArray(); return c;
        }
    }
    [Serializable]
    public sealed class ControllerBaseAdd
    {
        public string emitterId, abilityId, stateOwnerId, amount;
        public ControllerBaseAdd Clone() => (ControllerBaseAdd)MemberwiseClone();
    }
    [Serializable]
    public sealed class RetriggerEntry
    {
        public string emitterId, abilityId, stateOwnerId;
        public int dieIndex, count;
        public RetriggerEntry Clone() => (RetriggerEntry)MemberwiseClone();
    }
    /// <summary>v0.9: growth applied to a real controller instance after this submission is scored.
    /// v1.1.1: delta is in the unit of the card (hundredths, or whole points for base growth cards).</summary>
    [Serializable]
    public sealed class GrowthChange
    {
        public string ownerInstanceId, abilityId;
        public long delta;
        public bool reset;
        // v1.1.1: halve the grown part (growth = growth / 2, rounded down) instead of adding; `reset` is no longer produced.
        public bool halve;
        public GrowthChange Clone() => (GrowthChange)MemberwiseClone();
    }
    /// <summary>v1.0: one permanent change to a die face, produced from the frozen snapshot and applied by the session.
    /// kind: pipDelta, pipSet, color, gold, texture, weightDelta.</summary>
    [Serializable]
    public sealed class FaceChange
    {
        public string sourceId, emitterId, dieId, faceId, kind, text;
        public int amount;
        // v1.1.1: the real instance whose ability this is (the copied card for a mimic); it earns the cultivation count.
        public string stateOwnerId;
        public FaceChange Clone() => (FaceChange)MemberwiseClone();
    }
    [Serializable]
    public sealed class ScorePreview
    {
        public ScoreBreakdown minimum, maximum;
        public bool isRandom;
        public string[] randomOwnerIds;
    }

    public static class DiceFactory
    {
        public static DieInstance CreateStandard(GameConfig config, int serial,
            string lifetime = "permanent", string stageId = "")
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (serial <= 0) throw new ArgumentOutOfRangeException(nameof(serial));
            if (lifetime != "permanent" && lifetime != "stage_temporary")
                throw new ArgumentException("Unknown die lifetime.", nameof(lifetime));
            return new DieInstance
            {
                dieId = "D" + serial.ToString("D3", System.Globalization.CultureInfo.InvariantCulture),
                createdSerial = serial,
                lifetime = lifetime,
                createdStageId = stageId,
                isGlass = config.diceDefinition.isGlass,
                dataRevision = 0,
                faces = config.diceDefinition.faces.Select(f => new FaceState
                {
                    faceId = f.faceId,
                    permanentPip = f.pipValue,
                    baseColor = f.colorId,
                    baseWeight = f.baseWeight,
                    texture = f.texture,
                    isGold = f.isGold
                }).ToArray()
            };
        }
    }
}
