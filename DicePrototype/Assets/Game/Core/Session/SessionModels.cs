using System;
using System.Collections.Generic;
using System.Linq;

namespace DiceCraft.Core
{
    public enum CommandKind { NewRun, ChooseChallenge, StartStage, Roll, ConfirmLocks, Reorder, UseItem, Submit, Continue, Buy, Sell, Study, LeaveShop, EndStage }

    [Serializable]
    public sealed class SessionCommand
    {
        public string commandId;
        public long expectedActionSequence;
        public CommandKind kind;
        public uint seed;
        public bool risk;
        public string offerId, itemInstanceId, targetDieId, targetFaceId, handId;
        public string[] dieIds = Array.Empty<string>();
        public int[] targetPips = Array.Empty<int>();
        public SessionCommand Clone() { var c = (SessionCommand)MemberwiseClone(); c.dieIds = dieIds == null ? null : (string[])dieIds.Clone(); c.targetPips = targetPips == null ? null : (int[])targetPips.Clone(); return c; }
    }

    [Serializable]
    public sealed class OwnedItem
    {
        public string instanceId, contentId;
        public OwnedItem Clone() { return (OwnedItem)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class DieHandState
    {
        public string dieId, currentFaceId, currentColor;
        public int currentPip;
        public bool hasRolled, isLocked, remoteOverride;
        public DieHandState Clone() { return (DieHandState)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class HandState
    {
        public string handId;
        public int rollsUsed, rollsRemaining;
        public bool hasRolled, redWave, largeBuff, smallBuff, c05Used;
        public List<string> orderedLockedIds = new List<string>();
        public List<DieHandState> currentResults = new List<DieHandState>();
        public HandState Clone()
        {
            var c = (HandState)MemberwiseClone();
            c.orderedLockedIds = new List<string>(orderedLockedIds);
            c.currentResults = currentResults.Select(x => x.Clone()).ToList();
            return c;
        }
    }

    [Serializable]
    public sealed class StageState
    {
        public string stageId;
        public int stageIndex, capacity, remainingSubmissions, submissionOrdinal, stageRollBonus, reward;
        public long target, score;
        public bool risk, started, qualified, settled;
        public string settlementContinuation, settlementId;
        public StageState Clone() { return (StageState)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class ShopOffer
    {
        public string offerId, category, contentId;
        public int basePrice;
        public bool sold;
        public DieInstance die;
        public ControllerInstance controller;
        public string[] priceReasons = Array.Empty<string>();
        public ShopOffer Clone()
        {
            var c = (ShopOffer)MemberwiseClone();
            c.die = die == null ? null : die.Clone();
            c.controller = controller == null ? null : controller.Clone();
            c.priceReasons = (string[])priceReasons.Clone();
            return c;
        }
    }

    [Serializable]
    public sealed class ShopState
    {
        public string shopId;
        public bool generated;
        public int selfStudyUsed;
        public List<ShopOffer> offers = new List<ShopOffer>();
        public ShopState Clone() { var c = (ShopState)MemberwiseClone(); c.offers = offers.Select(x => x.Clone()).ToList(); return c; }
    }

    [Serializable]
    public sealed class EffectRecord
    {
        public string kind, sourceId, dieId, faceId, otherDieId, detail;
        public int oldValue, newValue;
        public EffectRecord Clone() { return (EffectRecord)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class StageSettlement
    {
        public string settlementId, stageId;
        public int goldBefore, baseReward, remainingSubmissions, unusedSubmissionReward, interest, interestPrincipal, goldAfter;
        public long score, target;
        public bool risk, pig;
        public StageSettlement Clone() { return (StageSettlement)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class SubmissionRecord
    {
        public SubmissionSnapshot snapshot;
        public ScoreBreakdown breakdown;
        public SubmissionRecord Clone() { return new SubmissionRecord { snapshot = SessionCopies.Snapshot(snapshot), breakdown = SessionCopies.Breakdown(breakdown) }; }
    }

    [Serializable]
    public sealed class CommandReceipt
    {
        public string commandId, phase, message;
        public long actionSequence, score;
        public int goldBefore, goldAfter;
        public SessionCommand command;
        public List<EffectRecord> effects = new List<EffectRecord>();
        public CommandReceipt Clone() { var c = (CommandReceipt)MemberwiseClone(); c.command = command == null ? null : command.Clone(); c.effects = effects.Select(x => x.Clone()).ToList(); return c; }
    }

    [Serializable]
    public sealed class RunState
    {
        public string runId, configVersion, phase, failureReason;
        public int saveSchemaVersion, gold, baseSubmitCapacity, controllerSlots, consumableSlots, itemDiscount;
        public int nextDieSerial, nextItemSerial, nextOfferSerial, nextHandSerial;
        public long actionSequence;
        public uint seed, diceRngState, shopRngState, effectRngState;
        public bool isDebug;
        public List<DieInstance> dice = new List<DieInstance>();
        public List<ControllerInstance> controllers = new List<ControllerInstance>();
        public List<OwnedItem> inventory = new List<OwnedItem>();
        public List<HandLevel> handLevels = new List<HandLevel>();
        public List<string> consumedItemIds = new List<string>();
        public List<CommandReceipt> processedReceipts = new List<CommandReceipt>();
        public List<EffectRecord> lastEffects = new List<EffectRecord>();
        public StageState stage;
        public HandState hand;
        public ShopState shop;
        public SubmissionRecord lastSubmission;
        public StageSettlement lastStageSettlement;

        public RunState Clone()
        {
            var c = (RunState)MemberwiseClone();
            c.dice = dice.Select(x => x.Clone()).ToList();
            c.controllers = controllers.Select(x => x.Clone()).ToList();
            c.inventory = inventory.Select(x => x.Clone()).ToList();
            c.handLevels = handLevels.Select(x => new HandLevel { handId = x.handId, level = x.level }).ToList();
            c.consumedItemIds = new List<string>(consumedItemIds);
            c.processedReceipts = processedReceipts.Select(x => x.Clone()).ToList();
            c.lastEffects = lastEffects.Select(x => x.Clone()).ToList();
            c.stage = stage == null ? null : stage.Clone(); c.hand = hand == null ? null : hand.Clone(); c.shop = shop == null ? null : shop.Clone();
            c.lastSubmission = lastSubmission == null ? null : lastSubmission.Clone();
            c.lastStageSettlement = lastStageSettlement == null ? null : lastStageSettlement.Clone();
            return c;
        }
    }

    public sealed class CommandResult
    {
        public bool accepted, duplicate, pendingSave;
        public string error;
        public CommandReceipt receipt;
    }

    /// <summary>The adapter must atomically persist the full snapshot or throw. It must not mutate it.</summary>
    public interface ISaveStore { void Save(RunState snapshot); }

    public sealed class MemorySaveStore : ISaveStore
    {
        public RunState Saved { get; private set; }
        public void Save(RunState snapshot) { Saved = snapshot.Clone(); }
    }

    internal static class SessionCopies
    {
        public static SubmissionSnapshot Snapshot(SubmissionSnapshot s)
        {
            return s == null ? null : s.Clone();
        }

        public static ScoreBreakdown Breakdown(ScoreBreakdown b)
        {
            return b == null ? null : b.Clone();
        }
    }
}
