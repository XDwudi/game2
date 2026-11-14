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
        public long growth;
        public long saleBonus;
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
        public int groupMult100, straightMult100, colorMult100, totalHandMult100;
        public int redValue, redCount, redMult100;
        public string controllerMultiplierNumerator, controllerMultiplierDenominator;
        public int pairKinds, pairBaseAdd, emptySlotBaseAdd, steadyBaseAdd, enchantBaseAdd, enchantBaseCount, enchantMultiplierCount;
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
            return result;
        }
    }

    [Serializable]
    public sealed class MultiplierFactor
    {
        public string emitterId, abilityId, stateOwnerId, numerator, denominator;
        public MultiplierFactor Clone() => (MultiplierFactor)MemberwiseClone();
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
        public AfterScorePlan Clone()
        {
            var c = (AfterScorePlan)MemberwiseClone();
            c.redSpreadIndices = (int[])redSpreadIndices.Clone(); c.growthIndices = (int[])growthIndices.Clone(); c.blueSpreadIndices = (int[])blueSpreadIndices.Clone();
            c.nextColorOwnerIds = (string[])nextColorOwnerIds.Clone(); c.randomFactorOwnerIds = (string[])randomFactorOwnerIds.Clone(); c.cannonRiskEmitterIds = (string[])cannonRiskEmitterIds.Clone();
            c.growthChanges = (growthChanges ?? Array.Empty<GrowthChange>()).Select(g => g.Clone()).ToArray(); return c;
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
    /// <summary>v0.9: growth applied to a real controller instance after this submission is scored.</summary>
    [Serializable]
    public sealed class GrowthChange
    {
        public string ownerInstanceId, abilityId;
        public long delta;
        public bool reset;
        public GrowthChange Clone() => (GrowthChange)MemberwiseClone();
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
