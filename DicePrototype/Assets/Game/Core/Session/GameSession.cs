using System;
using System.Collections.Generic;
using System.Linq;

namespace DiceCraft.Core
{
    /// <summary>Single writer for accepted gameplay state. Presentation never owns dice, money or RNG.</summary>
    public sealed partial class GameSession
    {
        private readonly GameConfig cfg;
        private readonly ISaveStore store;
        private readonly object gate = new object();
        private RunState state, pendingSaveState;
        private CommandReceipt pendingReceipt;
        private readonly List<string> pending = new List<string>();
        private bool busy;
        public event Action<CommandReceipt> Committed;
        public string LastPresentationError { get; private set; }
        public RunState State { get { lock (gate) return state == null ? null : state.Clone(); } }
        public string[] PendingIds { get { lock (gate) return pending.ToArray(); } }
        public bool HasPendingSave { get { lock (gate) return pendingSaveState != null; } }
        public bool Busy { get { lock (gate) return busy; } set { lock (gate) busy = value; } }

        public GameSession(GameConfig config, ISaveStore saveStore, RunState restored = null)
        {
            cfg = config ?? throw new ArgumentNullException(nameof(config));
            store = saveStore ?? throw new ArgumentNullException(nameof(saveStore));
            cfg.Validate();
            if (restored != null) { ValidateState(restored); state = restored.Clone(); }
        }

        public bool TogglePending(string dieId)
        {
            lock (gate)
            {
                if (busy || pendingSaveState != null || state == null || state.phase != "Selecting") return false;
                var result = state.hand.currentResults.FirstOrDefault(x => x.dieId == dieId);
                if (result == null || !result.hasRolled || result.isLocked) return false;
                if (pending.Contains(dieId)) pending.Remove(dieId); else pending.Add(dieId);
                return true;
            }
        }

        public void ClearPending() { lock (gate) { if (!busy && pendingSaveState == null) pending.Clear(); } }

        public CommandResult Execute(SessionCommand command)
        {
            lock (gate)
            {
                if (command == null || string.IsNullOrWhiteSpace(command.commandId)) return Reject("缺少命令 ID。");
                var original = state == null ? null : state.processedReceipts.FirstOrDefault(x => x.commandId == command.commandId);
                if (original != null) return new CommandResult { accepted = true, duplicate = true, receipt = original.Clone() };
                if (pendingSaveState != null) return new CommandResult { pendingSave = true, error = "存档失败待重试；所有规则操作已暂停。", receipt = pendingReceipt.Clone() };
                if (busy) return Reject("表现进行中，请等待或跳过。");
                long sequence = state == null ? 0 : state.actionSequence;
                if (command.expectedActionSequence != sequence) return Reject("操作序号已过期，请刷新后再试。");
                try
                {
                    Check(command.kind == CommandKind.NewRun || state != null, "请先新建挑战。");
                    Check(command.kind == CommandKind.ConfirmLocks || pending.Count == 0, "请先确认锁定或清除待选骰子。");
                    var candidate = command.kind == CommandKind.NewRun ? CreateRun(command) : state.Clone();
                    int goldBefore = state == null || command.kind == CommandKind.NewRun ? candidate.gold : state.gold;
                    candidate.lastEffects.Clear();
                    long scored = 0;
                    switch (command.kind)
                    {
                        case CommandKind.NewRun: Check(state == null || state.phase == "RunWon" || state.phase == "RunFailed", "进行中的挑战不能直接覆盖。"); break;
                        case CommandKind.ChooseChallenge: ChooseChallenge(candidate, command.risk); break;
                        case CommandKind.StartStage: StartStage(candidate); break;
                        case CommandKind.Roll: Roll(candidate, null, null); break;
                        case CommandKind.ConfirmLocks: ConfirmLocks(candidate); break;
                        case CommandKind.Reorder: Reorder(candidate, command.dieIds); break;
                        case CommandKind.UseItem: UseItem(candidate, command); break;
                        case CommandKind.Submit: scored = Submit(candidate); break;
                        case CommandKind.Continue: Continue(candidate); break;
                        case CommandKind.Buy: Buy(candidate, command); break;
                        case CommandKind.Sell: Sell(candidate, command.itemInstanceId); break;
                        case CommandKind.Study: Study(candidate, command.handId); break;
                        case CommandKind.LeaveShop: LeaveShop(candidate); break;
                        default: throw new RuleViolation("未知命令。");
                    }
                    candidate.actionSequence = checked(sequence + 1);
                    var recordedCommand = command.Clone();
                    if (command.kind == CommandKind.ConfirmLocks) recordedCommand.dieIds = pending.ToArray();
                    var receipt = new CommandReceipt {
                        commandId = command.commandId, actionSequence = candidate.actionSequence, command = recordedCommand,
                        phase = candidate.phase, message = command.kind.ToString(), score = scored,
                        goldBefore = goldBefore, goldAfter = candidate.gold,
                        effects = candidate.lastEffects.Select(x => x.Clone()).ToList()
                    };
                    candidate.processedReceipts.Add(receipt);
                    ValidateState(candidate);
                    pendingSaveState = candidate;
                    pendingReceipt = receipt;
                    return SavePending();
                }
                catch (RuleViolation e) { return Reject(e.Message); }
                catch (OverflowException) { return Reject("数值超过可保存范围，操作已取消。"); }
            }
        }

        public CommandResult RetrySave()
        {
            lock (gate) return pendingSaveState == null ? Reject("没有待重试的存档。") : SavePending();
        }

        private CommandResult SavePending()
        {
            try { store.Save(pendingSaveState.Clone()); }
            catch (Exception e) { return new CommandResult { pendingSave = true, error = "存档写入失败：" + e.Message, receipt = pendingReceipt.Clone() }; }
            state = pendingSaveState;
            var receipt = pendingReceipt.Clone();
            pendingSaveState = null; pendingReceipt = null;
            if (receipt.command.kind == CommandKind.ConfirmLocks || receipt.command.kind == CommandKind.NewRun || receipt.command.kind == CommandKind.Submit) pending.Clear();
            if (Committed != null)
                foreach (Action<CommandReceipt> observer in Committed.GetInvocationList())
                    try { observer(receipt.Clone()); } catch (Exception e) { LastPresentationError = e.Message; }
            return new CommandResult { accepted = true, receipt = receipt };
        }

        public SubmissionSnapshot PreviewSnapshot()
        {
            lock (gate) return state == null || state.hand == null || state.phase != "Selecting" || state.hand.orderedLockedIds.Count == 0 ? null : FreezeSubmission(state);
        }

        public ScoreBreakdown PreviewScore()
        {
            var snapshot = PreviewSnapshot();
            return snapshot == null ? null : ScoreCalculator.Evaluate(snapshot, cfg);
        }

        public EffectRecord[] PreviewSubmissionEffects()
        {
            lock (gate)
            {
                if (state == null || state.hand == null || state.phase != "Selecting" || state.hand.orderedLockedIds.Count == 0) return Array.Empty<EffectRecord>();
                // Reuse the real submission rules on an isolated state, without persistence,
                // observers, RNG changes or consuming the player's pending selection.
                var candidate = state.Clone(); candidate.lastEffects.Clear();
                Submit(candidate);
                return candidate.lastEffects.Where(e => e.kind == "DieCopied" || e.kind == "FaceDyed").Select(e => e.Clone()).ToArray();
            }
        }

        public int[] ProbabilityUnits(string dieId)
        {
            lock (gate)
            {
                Check(state != null, "没有活动挑战。");
                var die = FindDie(state, dieId);
                return ProbabilityCalculator.BuildUnits(die.faces, state.hand != null && state.hand.largeBuff, state.hand != null && state.hand.smallBuff, cfg);
            }
        }

        public int EffectiveCapacity { get { lock (gate) return state == null ? cfg.initialSubmitCapacity : ShopCapacity(state); } }
        public int InitialRolls { get { lock (gate) return state == null ? cfg.totalRollsPerHand : InitialRollBudget(state); } }
        public int InitialSubmissions { get { lock (gate) return state == null ? cfg.submissionsPerStage : cfg.submissionsPerStage + (Has(state, "C04") ? cfg.Controller("C04").parameters.submissionBonus : 0); } }
        public int GetOfferPrice(ShopOffer offer) { lock (gate) { Check(state != null, "没有活动挑战。"); return OfferPrice(state, offer); } }
        public string[] LegalStudyHands() { lock (gate) return state == null ? Array.Empty<string>() : cfg.handDefinitions.Where(x => IsHandLegal(state, x.id)).Select(x => x.id).ToArray(); }
        public int GetStudyPrice(string handId) { lock (gate) { Check(state != null, "没有活动挑战。"); return HandPrice(state, handId, true); } }

        private RunState CreateRun(SessionCommand command)
        {
            var s = new RunState {
                runId = "RUN_" + command.commandId, configVersion = cfg.configVersion, saveSchemaVersion = cfg.saveSchemaVersion,
                phase = "StagePreview", gold = cfg.initialGold, seed = command.seed, baseSubmitCapacity = cfg.initialSubmitCapacity,
                controllerSlots = cfg.initialControllerSlots, consumableSlots = cfg.initialConsumableSlots,
                nextDieSerial = cfg.initialDiceCount + 1, nextItemSerial = 1, nextOfferSerial = 1, nextHandSerial = 1,
                diceRngState = new GameRng(command.seed ^ cfg.rng.diceSeedXor).State,
                shopRngState = new GameRng(command.seed ^ cfg.rng.shopSeedXor).State,
                effectRngState = new GameRng(command.seed ^ cfg.rng.effectSeedXor).State
            };
            for (int i = 1; i <= cfg.initialDiceCount; i++) s.dice.Add(DiceFactory.CreateStandard(cfg, i));
            foreach (var h in cfg.handDefinitions) s.handLevels.Add(new HandLevel { handId = h.id, level = 0 });
            PrepareStagePreview(s, 0);
            return s;
        }

        private void PrepareStagePreview(RunState s, int index)
        {
            Check(index >= 0 && index < cfg.stages.Length, "没有下一关。");
            var def = cfg.stages[index];
            s.stage = new StageState { stageIndex = index, stageId = def.stageId, target = def.normalTarget,
                reward = def.normalReward, capacity = ShopCapacity(s), remainingSubmissions = cfg.submissionsPerStage + (Has(s, "C04") ? cfg.Controller("C04").parameters.submissionBonus : 0) };
            s.hand = null; s.shop = null; s.phase = "StagePreview";
        }

        private void ChooseChallenge(RunState s, bool risk)
        {
            RequirePhase(s, "StagePreview");
            var def = cfg.stages[s.stage.stageIndex];
            Check(!risk || def.riskEnabled, "本关没有高压挑战。");
            s.stage.risk = risk; s.stage.target = risk ? def.riskTarget : def.normalTarget; s.stage.reward = risk ? def.riskReward : def.normalReward;
        }

        private void StartStage(RunState s)
        {
            RequirePhase(s, "StagePreview");
            Check(s.dice.Count > 0, "骰组耗尽。");
            s.stage.capacity = ShopCapacity(s); s.stage.started = true;
            s.stage.remainingSubmissions = cfg.submissionsPerStage + (Has(s, "C04") ? cfg.Controller("C04").parameters.submissionBonus : 0);
            StartHand(s);
        }

        private int InitialRollBudget(RunState s)
        {
            return checked(cfg.totalRollsPerHand + (Has(s, "C04") ? cfg.Controller("C04").parameters.rollBonus : 0)
                + (Has(s, "C16") ? cfg.Controller("C16").parameters.rollBonus : 0) + (s.stage == null ? 0 : s.stage.stageRollBonus));
        }

        private void StartHand(RunState s)
        {
            s.hand = new HandState { handId = "H" + (s.nextHandSerial++).ToString("D4"), rollsRemaining = InitialRollBudget(s) };
            foreach (var die in s.dice.OrderBy(x => x.createdSerial)) s.hand.currentResults.Add(new DieHandState { dieId = die.dieId });
            s.phase = "HandReady";
        }

        private void ConfirmLocks(RunState s)
        {
            RequirePhase(s, "Selecting");
            Check(pending.Count > 0, "没有待锁骰子。");
            Check(s.hand.orderedLockedIds.Count + pending.Count <= s.stage.capacity, "提交栏容量不足；没有锁定任何骰子。");
            foreach (string id in pending)
            {
                var r = s.hand.currentResults.FirstOrDefault(x => x.dieId == id);
                Check(r != null && r.hasRolled && !r.isLocked, "待锁目标已经失效。");
            }
            foreach (string id in pending) { s.hand.currentResults.First(x => x.dieId == id).isLocked = true; s.hand.orderedLockedIds.Add(id); }
        }

        private void Reorder(RunState s, string[] order)
        {
            RequirePhase(s, "Selecting");
            Check(order != null && order.Length > 0 && order.Length == s.hand.orderedLockedIds.Count && order.Distinct().Count() == order.Length
                && order.All(s.hand.orderedLockedIds.Contains), "调序必须保留全部已锁骰子，不能添加、移除或留空。");
            s.hand.orderedLockedIds = order.ToList();
        }

        private void Roll(RunState s, string[] remoteIds, int[] remotePips)
        {
            RequirePhase(s, "HandReady", "Selecting");
            Check(s.hand.rollsRemaining > 0, "没有剩余投掷次数。");
            var unlocked = s.dice.Where(x => !s.hand.orderedLockedIds.Contains(x.dieId)).OrderBy(x => x.createdSerial).ToArray();
            Check(unlocked.Length > 0, "全部骰子已锁定，不能空投。");
            var diceRng = new GameRng(s.diceRngState); var effectRng = new GameRng(s.effectRngState);
            s.hand.rollsRemaining--; s.hand.rollsUsed++; s.hand.hasRolled = true;
            foreach (var die in unlocked)
            {
                if (die.isGlass && effectRng.NextIndex(100) < cfg.glassBreakPercent)
                {
                    s.dice.Remove(die); s.hand.currentResults.RemoveAll(x => x.dieId == die.dieId);
                    s.lastEffects.Add(new EffectRecord { kind = "DieBroken", sourceId = "U05", dieId = die.dieId });
                    continue;
                }
                var faces = die.faces.OrderBy(x => x.faceId, StringComparer.Ordinal).ToArray();
                var units = ProbabilityCalculator.BuildUnits(faces, s.hand.largeBuff, s.hand.smallBuff, cfg);
                int sample = diceRng.NextIndex(cfg.probabilityUnits), faceIndex = 0, ceiling = units[0];
                while (sample >= ceiling) ceiling += units[++faceIndex];
                var face = faces[faceIndex]; var r = s.hand.currentResults.First(x => x.dieId == die.dieId);
                r.currentFaceId = face.faceId; r.currentPip = face.permanentPip; r.currentColor = face.baseColor; r.hasRolled = true; r.remoteOverride = false;
                int remoteIndex = remoteIds == null ? -1 : Array.IndexOf(remoteIds, die.dieId);
                if (remoteIndex >= 0) { r.currentPip = remotePips[remoteIndex]; r.remoteOverride = true; }
            }
            s.diceRngState = diceRng.State; s.effectRngState = effectRng.State;
            if (s.dice.Count == 0) { s.phase = "RunFailed"; s.failureReason = "NoDice"; return; }
            int remainingUnlocked = s.dice.Count - s.hand.orderedLockedIds.Count;
            if (Has(s, "C05") && !s.hand.c05Used && s.hand.rollsRemaining == 0)
            {
                var p = cfg.Controller("C05").parameters;
                if (remainingUnlocked >= p.minUnlocked && remainingUnlocked <= p.maxUnlocked)
                {
                    s.hand.rollsRemaining = checked(s.hand.rollsRemaining + p.rollGrant); s.hand.c05Used = true;
                    s.lastEffects.Add(new EffectRecord { kind = "ExtraRolls", sourceId = "C05", newValue = p.rollGrant });
                }
            }
            s.phase = "Selecting";
        }

        private static bool Has(RunState s, string id) { return s.controllers.Any(x => x.contentId == id); }
        private static DieInstance FindDie(RunState s, string id) { var d = s.dice.FirstOrDefault(x => x.dieId == id); Check(d != null, "目标骰子不存在。"); return d; }
        private static void RequirePhase(RunState s, params string[] phases) { Check(phases.Contains(s.phase), "当前阶段不能执行此操作。"); }
        private static void Check(bool condition, string message) { if (!condition) throw new RuleViolation(message); }
        private static CommandResult Reject(string error) { return new CommandResult { error = error }; }
        private sealed class RuleViolation : Exception { public RuleViolation(string message) : base(message) { } }
    }
}
