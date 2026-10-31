using System;
using System.Collections.Generic;
using System.Linq;

namespace DiceCraft.Core
{
    public enum CommandKind { NewRun, ChooseChallenge, StartStage, Roll, ConfirmLocks, Reorder, UseItem, Submit, Continue, Buy, Sell, Study, LeaveShop, EndStage, ReorderControllers, RefreshControllers, ApplyDebugScene }

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
        public string[] controllerIds = Array.Empty<string>();
        public DebugScene debugScene;
        public int[] targetPips = Array.Empty<int>();
        public SessionCommand Clone() { var c = (SessionCommand)MemberwiseClone(); c.dieIds = dieIds == null ? null : (string[])dieIds.Clone(); c.controllerIds = controllerIds == null ? null : (string[])controllerIds.Clone(); c.targetPips = targetPips == null ? null : (int[])targetPips.Clone(); c.debugScene = debugScene == null ? null : debugScene.Clone(); return c; }
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
        public bool hasRolled, redWave, largeBuff, smallBuff;
        public bool c05Used { get { return c05UsedEmitterIds.Count > 0; } }
        public List<string> c05UsedEmitterIds = new List<string>();
        public string[] rollBonusSourceIds = Array.Empty<string>();
        public List<string> orderedLockedIds = new List<string>();
        public List<DieHandState> currentResults = new List<DieHandState>();
        public HandState Clone()
        {
            var c = (HandState)MemberwiseClone();
            c.orderedLockedIds = new List<string>(orderedLockedIds);
            c.currentResults = currentResults.Select(x => x.Clone()).ToList();
            c.c05UsedEmitterIds = new List<string>(c05UsedEmitterIds);
            c.rollBonusSourceIds = (string[])rollBonusSourceIds.Clone();
            return c;
        }
    }

    [Serializable]
    public sealed class StageState
    {
        public string stageId;
        public int stageIndex, capacity, remainingSubmissions, submissionOrdinal, stageRollBonus, reward;
        public string target = "0", score = "0";
        public string[] controllerOrderSnapshot = Array.Empty<string>();
        public string[] submissionBonusSourceIds = Array.Empty<string>();
        public MimicBinding[] mimicBindings = Array.Empty<MimicBinding>();
        public List<string> destroyedControllerIds = new List<string>();
        public bool risk, started, qualified, settled;
        public string settlementContinuation, settlementId;
        public StageState Clone() { var c = (StageState)MemberwiseClone(); c.controllerOrderSnapshot = (string[])controllerOrderSnapshot.Clone(); c.submissionBonusSourceIds = (string[])submissionBonusSourceIds.Clone(); c.mimicBindings = mimicBindings.Select(x => new MimicBinding { sourceInstanceId = x.sourceInstanceId, targetInstanceId = x.targetInstanceId }).ToArray(); c.destroyedControllerIds = new List<string>(destroyedControllerIds); return c; }
    }

    [Serializable]
    public sealed class ShopOffer
    {
        public string offerId, batchId, category, contentId;
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
        public string shopId, generationId;
        public bool generated;
        public int selfStudyUsed, controllerRefreshCount, controllerGeneration;
        public List<ShopOffer> offers = new List<ShopOffer>();
        public ShopState Clone() { var c = (ShopState)MemberwiseClone(); c.offers = offers.Select(x => x.Clone()).ToList(); return c; }
    }

    [Serializable]
    public sealed class EffectRecord
    {
        public string kind, sourceId, dieId, faceId, otherDieId, detail;
        public long oldValue, newValue;
        public EffectRecord Clone() { return (EffectRecord)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class StageSettlement
    {
        public string settlementId, stageId;
        public long goldBefore, unusedSubmissionReward, interest, interestPrincipal, goldAfter;
        public int baseReward, remainingSubmissions, pigSourceCount;
        public string score = "0", target = "0";
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
        public string commandId, phase, message, stateHash, score = "0";
        public long actionSequence, beforeSequence, goldBefore, goldAfter;
        public List<RandomRecord> randomResults = new List<RandomRecord>();
        public SubmissionRecord submission;
        public StageSettlement settlement;
        public SessionCommand command;
        public List<EffectRecord> effects = new List<EffectRecord>();
        public CommandReceipt Clone() { var c = (CommandReceipt)MemberwiseClone(); c.command = command == null ? null : command.Clone(); c.effects = effects.Select(x => x.Clone()).ToList(); c.randomResults = randomResults.Select(x => x.Clone()).ToList(); c.submission = submission?.Clone(); c.settlement = settlement?.Clone(); return c; }
    }

    [Serializable]
    public sealed class RunState
    {
        public string runId, configVersion, phase, failureReason, sourceRunId, previousPrimaryHandId;
        public int saveSchemaVersion, baseSubmitCapacity, controllerSlots, consumableSlots, itemDiscount;
        public int controllerOfferPurchases, consumableOfferPurchases, debugCapacityOverride;
        public bool freeFirstRefreshOwned, debugWorkspace;
        public int nextDieSerial, nextItemSerial, nextOfferSerial, nextHandSerial;
        public long actionSequence, gold, submissionOrdinalGlobal;
        public uint seed, diceRngState, shopRngState, effectRngState;
        public bool isDebug;
        public List<DieInstance> dice = new List<DieInstance>();
        public List<ControllerInstance> controllers = new List<ControllerInstance>();
        public List<OwnedItem> inventory = new List<OwnedItem>();
        public List<HandLevel> handLevels = new List<HandLevel>();
        public List<HandLevel> colorLevels = new List<HandLevel>();
        public List<DebugRandomOverride> randomOverrides = new List<DebugRandomOverride>();
        public List<RandomRecord> lastRandomResults = new List<RandomRecord>();
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
            c.colorLevels = colorLevels.Select(x => x.Clone()).ToList();
            c.randomOverrides = randomOverrides.Select(x => x.Clone()).ToList();
            c.lastRandomResults = lastRandomResults.Select(x => x.Clone()).ToList();
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
        public bool accepted, duplicate, pendingSave, noChange;
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
