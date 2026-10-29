using System;
using System.Linq;

namespace DiceCraft.Core
{
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
        public bool redWave;
        public string[] equippedControllerIds = Array.Empty<string>();
        public HandLevel[] handLevels = Array.Empty<HandLevel>();
        public SubmissionSnapshot Clone()
        {
            var result = (SubmissionSnapshot)MemberwiseClone();
            result.dice = dice?.Select(d => d.Clone()).ToArray();
            result.equippedControllerIds = (string[])equippedControllerIds.Clone();
            result.handLevels = handLevels.Select(h => h.Clone()).ToArray();
            return result;
        }
    }

    [Serializable]
    public sealed class ScoreBreakdown
    {
        public int[] effectivePips, pipContributions;
        public string[] effectiveColors, matchedGroupIds;
        public string chosenGroupId;
        public int pipSum, unusedRollBonus;
        public int groupMult100, straightMult100, colorMult100, totalHandMult100;
        public int redValue, redCount, redMult100;
        public int controllerMultiplier, finalFlatAdd;
        public string[] triggeredMultiplierIds;
        public long score;
        public ScoreBreakdown Clone()
        {
            var result = (ScoreBreakdown)MemberwiseClone();
            result.effectivePips = (int[])effectivePips.Clone();
            result.pipContributions = (int[])pipContributions.Clone();
            result.effectiveColors = (string[])effectiveColors.Clone();
            result.matchedGroupIds = (string[])matchedGroupIds.Clone();
            result.triggeredMultiplierIds = (string[])triggeredMultiplierIds.Clone();
            return result;
        }
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
                createdSerial = serial, lifetime = lifetime, createdStageId = stageId,
                isGlass = config.diceDefinition.isGlass, dataRevision = 0,
                faces = config.diceDefinition.faces.Select(f => new FaceState
                {
                    faceId = f.faceId, permanentPip = f.pipValue, baseColor = f.colorId,
                    baseWeight = f.baseWeight, texture = f.texture, isGold = f.isGold
                }).ToArray()
            };
        }
    }
}
