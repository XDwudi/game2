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
        public long ActionSequence { get { lock (gate) return state == null ? 0 : state.actionSequence; } }
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
                    Check(command.kind == CommandKind.ConfirmLocks || command.kind == CommandKind.ApplyDebugScene || pending.Count == 0, "请先确认锁定或清除待选骰子。");
                    var candidate = command.kind == CommandKind.NewRun ? CreateRun(command) : state.Clone();
                    if (command.kind == CommandKind.NewRun && state != null && state.debugWorkspace)
                    { candidate.debugWorkspace = true; candidate.isDebug = true; candidate.sourceRunId = state.sourceRunId; }
                    long goldBefore = state == null || command.kind == CommandKind.NewRun ? candidate.gold : state.gold;
                    candidate.lastEffects.Clear(); candidate.lastRandomResults.Clear();
                    string scored = "0";
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
                        case CommandKind.EndStage: EndStage(candidate); break;
                        case CommandKind.Continue: Continue(candidate); break;
                        case CommandKind.Buy: Buy(candidate, command); break;
                        case CommandKind.Sell: Sell(candidate, command.itemInstanceId); break;
                        case CommandKind.Study: Study(candidate, command.handId); break;
                        case CommandKind.LeaveShop: LeaveShop(candidate); break;
                        case CommandKind.ReorderControllers:
                            if (!ReorderControllers(candidate, command.controllerIds)) return new CommandResult { accepted = true, noChange = true };
                            break;
                        case CommandKind.RefreshControllers: RefreshControllers(candidate); break;
                        case CommandKind.ApplyDebugScene: ApplyDebugScene(candidate, command.debugScene); break;
                        default: throw new RuleViolation("未知命令。");
                    }
                    if (candidate.debugWorkspace) candidate.isDebug = true;
                    candidate.actionSequence = checked(sequence + 1);
                    var recordedCommand = command.Clone();
                    if (command.kind == CommandKind.ConfirmLocks) recordedCommand.dieIds = pending.ToArray();
                    var receipt = new CommandReceipt {
                        commandId = command.commandId, actionSequence = candidate.actionSequence, command = recordedCommand,
                        phase = candidate.phase, message = command.kind.ToString(), score = scored,
                        goldBefore = goldBefore, goldAfter = candidate.gold, beforeSequence = sequence,
                        randomResults = candidate.lastRandomResults.Select(x => x.Clone()).ToList(), stateHash = StateHash(candidate),
                        submission = command.kind == CommandKind.Submit ? candidate.lastSubmission.Clone() : null,
                        settlement = candidate.stage.settled && (state == null || !state.stage.settled) ? candidate.lastStageSettlement.Clone() : null,
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
                catch (ArgumentException e) { return Reject("输入不合法：" + e.Message); }
                catch (FormatException e) { return Reject("数据格式无效：" + e.Message); }
                catch (InvalidOperationException e) { return Reject("数据校验失败：" + e.Message); }
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
            if (receipt.command.kind == CommandKind.ConfirmLocks || receipt.command.kind == CommandKind.NewRun || receipt.command.kind == CommandKind.Submit || receipt.command.kind == CommandKind.ApplyDebugScene) pending.Clear();
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
            var preview = PreviewScoreRange();
            return preview == null ? null : preview.minimum;
        }

        public ScorePreview PreviewScoreRange()
        {
            var snapshot = PreviewSnapshot();
            if (snapshot == null) return null;
            lock (gate) ApplyPreviewOverrides(snapshot, state);
            return ScoreCalculator.Preview(snapshot, cfg);
        }

        public EffectRecord[] PreviewSubmissionEffects()
        {
            lock (gate)
            {
                var snapshot = PreviewSnapshot();
                if (snapshot == null) return Array.Empty<EffectRecord>();
                var candidate = state.Clone(); candidate.lastEffects.Clear();
                var score = ScoreCalculator.Preview(snapshot, cfg).minimum;
                ApplyPermanentEffects(candidate, snapshot, score.afterScorePlan);
                return candidate.lastEffects.Select(e => e.Clone()).ToArray();
            }
        }

        public int[] ProbabilityUnits(string dieId)
        {
            lock (gate)
            {
                Check(state != null, "没有活动挑战。");
                var die = FindDie(state, dieId);
                return ProbabilityCalculator.BuildUnits(die.faces, state.hand != null && state.hand.largeBuff, state.hand != null && state.hand.smallBuff, cfg, Sources(state).Count(x => x.abilityId == "C34"), Sources(state).Count(x => x.abilityId == "C35"));
            }
        }

                // Between stages the next stage's numbers are quoted for the controllers that will survive the altars (C100).
        private static bool BeforeStage(RunState s) => s.phase == "StagePreview";
        public int EffectiveCapacity { get { lock (gate) return state == null ? cfg.initialSubmitCapacity : BeforeStage(state) ? PreviewCapacity(state) : InStage(state) ? state.stage.capacity : ShopCapacity(state); } }
        public int InitialRolls { get { lock (gate) { if (state == null) return cfg.totalRollsPerHand; if (!BeforeStage(state)) return InitialRollBudget(state); var entering = Entering(state, out var rules); return ControllerRules.RollBudget(entering, cfg, 0, null, rules); } } }
        public int InitialSubmissions { get { lock (gate) { if (state == null) return cfg.submissionsPerStage; if (!BeforeStage(state)) return ControllerRules.SubmissionBudget(state.controllers.ToArray(), cfg, ActiveBindings(state), ActiveRules(state)); var entering = Entering(state, out var rules); return ControllerRules.SubmissionBudget(entering, cfg, null, rules); } } }
        private int PreviewCapacity(RunState s) { if (s.isDebug && s.debugCapacityOverride > 0) return s.debugCapacityOverride; var entering = Entering(s, out var rules); return ControllerRules.Capacity(entering, cfg, false, null, rules); }

        // ---- v1.0 stage context ----
        private static bool InStage(RunState s) => s.stage != null && s.stage.started && !s.stage.settled;
        /// <summary>The kind of the current stage, always read from the configuration (debug scenes may jump stages).</summary>
        private string StageKind(RunState s) => cfg.stages[s.stage.stageIndex].stageKind;
        /// <summary>The stage carries a boss modifier that has not been nullified (whether or not the stage has started).</summary>
        private static bool ModifierPending(RunState s) => s.stage != null && !string.IsNullOrEmpty(s.stage.modifierId) && !s.stage.modifierNullified;
        /// <summary>The boss modifier in force right now: only between StartStage and the end of settlement.</summary>
        private static string ActiveModifierId(RunState s) => InStage(s) && ModifierPending(s) ? s.stage.modifierId : null;
        private bool Fx(RunState s, string effect) => ControllerRules.ModifierHas(cfg, ActiveModifierId(s), effect);
        private static string[] ActiveDisabled(RunState s) => InStage(s) ? s.stage.disabledControllerIds : Array.Empty<string>();
        private StageRules ActiveRules(RunState s) => StageRules.For(cfg, ActiveModifierId(s), ActiveDisabled(s));
        /// <summary>
        /// The controllers that will enter the stage being previewed and the rules it will run under: after the altars
        /// (C100) and after a scapegoat (C131) has removed itself and the modifier.
        /// </summary>
        private ControllerInstance[] Entering(RunState s, out StageRules rules)
        {
            var after = ControllerRules.AfterAltars(s.controllers).ToList(); string modifier = ModifierPending(s) ? s.stage.modifierId : null;
            if (modifier != null) { var goat = after.FirstOrDefault(c => c.definitionId == "C131"); if (goat != null) { after.Remove(goat); modifier = null; } }
            rules = StageRules.For(cfg, modifier, ControllerRules.DisabledBy(after, cfg, modifier));
            return after.ToArray();
        }
        /// <summary>The one place a boss modifier scales a target (TargetPercent); any other modifier leaves it as it is.</summary>
        private string ScaledTarget(string target, string modifierId)
            => ControllerRules.ModifierHas(cfg, modifierId, "TargetPercent") ? ExactScoreMath.SaveDecimal(ExactScoreMath.Parse(target) * cfg.Modifier(modifierId).amount / 100) : target;
        private string StageTarget(RunState s, StageDefinition def, bool risk)
            => ScaledTarget(risk ? def.riskTarget : def.normalTarget, ModifierPending(s) ? s.stage.modifierId : null);
        public string CurrentModifierId { get { lock (gate) return state == null ? null : ActiveModifierId(state); } }

        // ---- v1.0 quotes for the interface: it asks for these numbers instead of re-deriving a rule ----
        /// <summary>The interest principal cap: base + C96 sources + the O10 purchases of this run.</summary>
        private long InterestPrincipalCap(RunState s) => checked(cfg.maxInterestPrincipal + (long)Sources(s).Count(x => x.abilityId == "C96") * cfg.Controller("C96").parameters.interestPrincipalAdd + (long)cfg.interestPrincipalStep * s.interestCapPurchases);
        /// <summary>v1.0 C145: the break chance of a glass die per roll (a disabled C145 is not a source).</summary>
        private int GlassBreakPercent(RunState s) => Sources(s).Any(x => x.abilityId == "C145") ? cfg.Controller("C145").parameters.glassBreakPercent : cfg.glassBreakPercent;
        public long InterestPrincipalCapIn(RunState view) { lock (gate) return InterestPrincipalCap(view); }
        public int GlassBreakPercentIn(RunState view) { lock (gate) return GlassBreakPercent(view); }
        /// <summary>What an owned controller would sell for in the given state (C140, shop-acquired rule, funds and bonuses included).</summary>
        public long ControllerSalePriceIn(RunState view, ControllerInstance c) { lock (gate) return SalePrice(view, c); }
        /// <summary>The capacity <see cref="EffectiveCapacity"/> would report if the controllers stood in this order (drag preview).</summary>
        public int CapacityForOrder(IList<string> orderedInstanceIds)
        {
            lock (gate)
            {
                if (state == null) return cfg.initialSubmitCapacity;
                var copy = state.Clone(); Check(orderedInstanceIds != null && orderedInstanceIds.Count == copy.controllers.Count && orderedInstanceIds.All(id => copy.controllers.Any(c => c.instanceId == id)), "控制器顺序无效。");
                copy.controllers = orderedInstanceIds.Select(id => copy.controllers.First(c => c.instanceId == id)).ToList();
                return BeforeStage(copy) ? PreviewCapacity(copy) : InStage(copy) ? copy.stage.capacity : ShopCapacity(copy);
            }
        }

        /// <summary>v1.0: draws the whole cycle plan (boss modifier and loot kind per cycle) from the stage stream.</summary>
        private void BuildCyclePlan(RunState s)
        {
            var rng = new GameRng(s.stageRngState); int cycles = cfg.stages.Length / 3; string previousLoot = null;
            for (int cycle = 1; cycle <= cycles; cycle++)
            {
                bool final = cycle == cycles; string modifier = DrawModifier(s, rng, cycle, s.cycles.Select(c => c.modifierId)); string loot = "";
                if (!final)
                {
                    var kinds = cfg.lootKinds.Where(k => k.id != previousLoot).OrderBy(k => k.id, StringComparer.Ordinal).ToArray();
                    var trace = DrawRaw(s, rng, "stage", "LootKind", "CYCLE_" + cycle, kinds.Sum(k => k.weight)); int draw = trace.rawTicket;
                    foreach (var kind in kinds) { draw -= kind.weight; if (draw < 0) { loot = kind.id; break; } }
                    trace.rawValue = trace.appliedValue = loot; previousLoot = loot;
                }
                s.cycles.Add(new CyclePlan { modifierId = modifier, lootKind = loot });
            }
            s.stageRngState = rng.State;
        }
        private BossModifierDefinition[] ModifierPool(int cycle, IEnumerable<string> excluded)
        {
            bool final = cycle == cfg.stages.Length / 3; var omit = new HashSet<string>(excluded ?? Array.Empty<string>(), StringComparer.Ordinal);
            return cfg.bossModifiers.Where(m => (final ? m.tier == 4 : m.tier < 4 && m.minCycle <= cycle) && !omit.Contains(m.id)).OrderBy(m => m.id, StringComparer.Ordinal).ToArray();
        }
        private string DrawModifier(RunState s, GameRng rng, int cycle, IEnumerable<string> excluded)
        {
            var pool = ModifierPool(cycle, excluded); Check(pool.Length > 0, "没有可用的首领减益。");
            Func<BossModifierDefinition, int> weight = m => m.tier == 4 ? 1 : m.tier;
            var trace = DrawRaw(s, rng, "stage", "BossModifier", "CYCLE_" + cycle, pool.Sum(weight)); int draw = trace.rawTicket; string picked = pool[0].id;
            foreach (var m in pool) { draw -= weight(m); if (draw < 0) { picked = m.id; break; } }
            trace.rawValue = trace.appliedValue = picked; return picked;
        }
        /// <summary>v1.0 read-only view of the three stages of the cycle that contains the next stage to play.</summary>
        public CycleStageView[] CycleView()
        {
            lock (gate)
            {
                if (state == null || state.stage == null) return Array.Empty<CycleStageView>();
                bool ahead = state.phase == "Shop" || state.phase == "StageSettlement" || state.phase == "RunWon";
                int focus = Math.Min(cfg.stages.Length - 1, state.stage.stageIndex + (ahead && state.stage.settled ? 1 : 0)), first = focus / 3 * 3; var plan = state.cycles[focus / 3];
                return Enumerable.Range(first, 3).Select(i =>
                {
                    var def = cfg.stages[i]; bool boss = def.stageKind == "boss", own = i == state.stage.stageIndex;
                    return new CycleStageView
                    {
                        stageIndex = i, stageKind = def.stageKind, normalTarget = def.normalTarget, riskTarget = def.riskTarget, normalReward = def.normalReward, riskReward = def.riskReward,
                        cleared = i < state.stage.stageIndex || (own && state.stage.settled), current = i == focus, risk = own && state.stage.risk,
                        modifierId = boss ? plan.modifierId : "", lootKind = boss ? plan.lootKind : "", modifierNullified = own && state.stage.modifierNullified,
                        bossTarget = ScaledTarget(def.normalTarget, boss ? plan.modifierId : null)
                    };
                }).ToArray();
            }
        }
        /// <summary>v1.0: what entering the previewed stage will do, for the stage-select screen.</summary>
        public StageEntryView EntryView()
        {
            lock (gate)
            {
                if (state == null || !BeforeStage(state)) return null;
                var entering = Entering(state, out var rules); bool goat = ModifierPending(state) && ControllerRules.AfterAltars(state.controllers).Any(c => c.definitionId == "C131");
                return new StageEntryView { scapegoat = goat, modifierId = goat || !ModifierPending(state) ? "" : state.stage.modifierId, disabledControllerIds = rules.disabledIds,
                    target = goat ? cfg.stages[state.stage.stageIndex].normalTarget : state.stage.target };
            }
        }
        public int GetOfferPrice(ShopOffer offer) { lock (gate) { Check(state != null, "没有活动挑战。"); return OfferPrice(state, offer); } }
        /// <summary>v1.1: buying never trades an owned card in, so this is always 0. Kept for the interface.</summary>
        public long GetOfferTradeIn(ShopOffer offer) { lock (gate) { Check(state != null, "没有活动挑战。"); return 0; } }
        /// <summary>v1.1: equal to <see cref="GetOfferPrice"/> (no trade-in).</summary>
        public long GetOfferNetPrice(ShopOffer offer) { lock (gate) { Check(state != null, "没有活动挑战。"); return OfferPrice(state, offer); } }
        public int OrdinaryControllerSlotsUsed { get { lock (gate) return state == null ? 0 : state.controllers.Count(x => x.enchantmentId != "slotless"); } }
        public bool CanEndStage { get { lock (gate) return state != null && state.stage.qualified && !state.stage.settled && (state.phase == "HandReady" || state.phase == "Selecting") && !busy && pendingSaveState == null && pending.Count == 0; } }
        public string[] LegalStudyHands() { lock (gate) return state == null ? Array.Empty<string>() : StudyIds(state).ToArray(); }
        public int GetStudyPrice(string handId) { lock (gate) { Check(state != null, "没有活动挑战。"); return HandPrice(state, handId, true); } }

        private RunState CreateRun(SessionCommand command)
        {
            var s = new RunState {
                runId = "RUN_" + command.commandId, configVersion = cfg.configVersion, saveSchemaVersion = cfg.saveSchemaVersion,
                phase = "StagePreview", gold = cfg.initialGold, seed = command.seed, baseSubmitCapacity = cfg.initialSubmitCapacity,
                controllerSlots = cfg.initialControllerSlots, consumableSlots = cfg.initialConsumableSlots,
                nextDieSerial = cfg.initialDiceCount + 1, nextItemSerial = 1, nextOfferSerial = 1, nextHandSerial = 1,
                diceRngState = new GameRng(command.seed ^ cfg.rng.diceSeedXor).State,
                stageRngState = new GameRng(command.seed ^ cfg.rng.stageSeedXor).State,
                shopRngState = new GameRng(command.seed ^ cfg.rng.shopSeedXor).State,
                effectRngState = new GameRng(command.seed ^ cfg.rng.effectSeedXor).State
            };
            for (int i = 1; i <= cfg.initialDiceCount; i++) s.dice.Add(DiceFactory.CreateStandard(cfg, i));
            foreach (var h in cfg.handDefinitions) s.handLevels.Add(new HandLevel { handId = h.id, level = 0 });
            foreach (var c in cfg.colorDefinitions) s.colorLevels.Add(new HandLevel { handId = c.id, level = 0 });
            BuildCyclePlan(s);
            PrepareStagePreview(s, 0);
            return s;
        }

        private void PrepareStagePreview(RunState s, int index)
        {
            Check(index >= 0 && index < cfg.stages.Length, "没有下一关。");
            var def = cfg.stages[index];
            s.stage = new StageState { stageIndex = index, stageId = def.stageId, reward = def.normalReward, stageKind = def.stageKind,
                modifierId = def.stageKind == "boss" ? s.cycles[index / 3].modifierId : "" };
            s.stage.target = StageTarget(s, def, false); RefreshPreviewBudgets(s);
            s.hand = null; s.shop = null; s.phase = "StagePreview";
        }

        private void RefreshPreviewBudgets(RunState s)
        {
            var entering = Entering(s, out var rules);
            s.stage.capacity = PreviewCapacity(s); s.stage.remainingSubmissions = ControllerRules.SubmissionBudget(entering, cfg, null, rules);
        }

        private void ChooseChallenge(RunState s, bool risk)
        {
            RequirePhase(s, "StagePreview");
            var def = cfg.stages[s.stage.stageIndex];
            Check(!risk || def.riskEnabled, "本关没有高压挑战。");
            s.stage.risk = risk; s.stage.target = StageTarget(s, def, risk); s.stage.reward = risk ? def.riskReward : def.normalReward;
        }

        private void StartStage(RunState s)
        {
            RequirePhase(s, "StagePreview");
            Check(s.dice.Count > 0, "骰组耗尽。");
            // v0.9 C100: each real altar, left to right, destroys its right neighbour before order and bindings freeze.
            foreach (var pair in ControllerRules.AltarPlan(s.controllers))
            {
                var altar = s.controllers.First(c => c.instanceId == pair.Key); var victim = s.controllers.First(c => c.instanceId == pair.Value);
                s.controllers.Remove(victim); s.randomOverrides.RemoveAll(o => o.sourceId == victim.instanceId); long before = altar.state.growth;
                altar.state.growth = checked(before + (long)cfg.Controller(victim.definitionId).buyPrice * cfg.Controller("C100").parameters.growthPerBuyPrice);
                s.lastEffects.Add(new EffectRecord { kind = "ControllerSacrificed", sourceId = "C100", detail = altar.instanceId + ":" + victim.instanceId + ":" + victim.definitionId, oldValue = before, newValue = altar.state.growth });
            }
            // v1.0 C131: the leftmost scapegoat destroys itself and lifts the boss modifier before anything else of it applies.
            if (ModifierPending(s))
            {
                var goat = s.controllers.FirstOrDefault(c => c.definitionId == "C131");
                if (goat != null)
                {
                    s.controllers.Remove(goat); s.randomOverrides.RemoveAll(o => o.sourceId == goat.instanceId); s.stage.modifierNullified = true;
                    s.stage.target = StageTarget(s, cfg.stages[s.stage.stageIndex], s.stage.risk);
                    s.lastEffects.Add(new EffectRecord { kind = "ModifierNullified", sourceId = "C131", detail = goat.instanceId + ":" + s.stage.modifierId });
                }
            }
            s.stage.controllerOrderSnapshot = s.controllers.Select(c => c.instanceId).ToArray();
            s.stage.mimicBindings = ControllerRules.FreezeBindings(s.controllers.ToArray());
            s.stage.started = true;
            s.stage.disabledControllerIds = ControllerRules.DisabledBy(s.controllers, cfg, ActiveModifierId(s));
            foreach (string id in s.stage.disabledControllerIds) s.lastEffects.Add(new EffectRecord { kind = "ControllerDisabled", sourceId = s.stage.modifierId, detail = id });
            if (Fx(s, "LoseHalfGold"))
            {
                long before = s.gold; s.gold = checked(before - before / 2);
                s.lastEffects.Add(new EffectRecord { kind = "GoldLost", sourceId = s.stage.modifierId, oldValue = before, newValue = s.gold });
            }
            ApplyStageStartControllers(s);
            var rules = ActiveRules(s);
            s.stage.submissionBonusSourceIds = rules.silence ? Array.Empty<string>() : s.controllers.Where(c => c.enchantmentId == "submit1" && !rules.disabledIds.Contains(c.instanceId)).Select(c => c.instanceId).ToArray();
            s.stage.capacity = ShopCapacity(s);
            s.stage.remainingSubmissions = ControllerRules.SubmissionBudget(s.controllers, cfg, s.stage.mimicBindings, rules);
            StartHand(s);
        }

        /// <summary>v1.0 stage-start abilities, left to right: C128 glass, C136 temporary die, C110 random item.</summary>
        private void ApplyStageStartControllers(RunState s)
        {
            var rng = new GameRng(s.effectRngState);
            foreach (var source in Sources(s))
            {
                if (source.abilityId == "C128" && source.emitterDefinitionId == "C128")
                {
                    for (int i = 0; i < cfg.Controller("C128").parameters.dicePerStage; i++)
                    {
                        var die = s.dice.Where(d => d.lifetime == "permanent" && !d.isGlass).OrderBy(d => d.createdSerial).FirstOrDefault(); if (die == null) break;
                        die.isGlass = true; die.dataRevision++;
                        s.lastEffects.Add(new EffectRecord { kind = "DieGlassed", sourceId = "C128", dieId = die.dieId, detail = source.emitterId });
                    }
                }
                else if (source.abilityId == "C136")
                {
                    for (int i = 0; i < cfg.Controller("C136").parameters.dicePerStage; i++)
                    {
                        if (s.dice.Count >= (s.isDebug ? cfg.debug.maxDice : cfg.maxActiveDice) || (!s.isDebug && s.dice.Count(x => x.lifetime == "stage_temporary") >= cfg.maxTemporaryDice)) break;
                        var die = DiceFactory.CreateStandard(cfg, s.nextDieSerial++, "stage_temporary", s.stage.stageId); s.dice.Add(die);
                        s.lastEffects.Add(new EffectRecord { kind = "DieAdded", sourceId = "C136", dieId = die.dieId, detail = source.emitterId });
                    }
                }
                else if (source.abilityId == "C110")
                {
                    for (int i = 0; i < cfg.Controller("C110").parameters.itemsPerStage; i++)
                    {
                        if (s.inventory.Count >= s.consumableSlots) break;
                        var ids = cfg.consumables.Select(c => c.id).OrderBy(x => x, StringComparer.Ordinal).ToArray();
                        var trace = DrawRaw(s, rng, "effect", "C110Item", source.emitterId, ids.Length); string id = ids[trace.rawTicket]; trace.rawValue = trace.appliedValue = id;
                        var item = NewOwnedItem(s, id); s.inventory.Add(item);
                        s.lastEffects.Add(new EffectRecord { kind = "ItemGranted", sourceId = "C110", detail = item.instanceId + ":" + id });
                    }
                }
            }
            s.effectRngState = rng.State;
        }

        private int InitialRollBudget(RunState s)
        {
            return ControllerRules.RollBudget(s.controllers, cfg, s.stage == null ? 0 : s.stage.stageRollBonus, ActiveBindings(s), ActiveRules(s));
        }

        private void StartHand(RunState s)
        {
            s.hand = new HandState { handId = "H" + (s.nextHandSerial++).ToString("D4"), rollsRemaining = InitialRollBudget(s), rollBonusSourceIds = ActiveRules(s).silence ? Array.Empty<string>() : s.controllers.Where(c => c.enchantmentId == "roll1" && !ActiveDisabled(s).Contains(c.instanceId)).Select(c => c.instanceId).ToArray() };
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
            Grow(s, "C62", cfg.Controller("C62").parameters.growthPerEvent);
            int breakPercent = GlassBreakPercent(s);
            foreach (var die in unlocked)
            {
                if (die.isGlass && DrawRisk(s, effectRng, "Glass", die.dieId, breakPercent))
                {
                    s.dice.Remove(die); s.hand.currentResults.RemoveAll(x => x.dieId == die.dieId);
                    s.lastEffects.Add(new EffectRecord { kind = "DieBroken", sourceId = "U05", dieId = die.dieId });
                    Grow(s, "C90", cfg.Controller("C90").parameters.growthPerEvent);
                    continue;
                }
                var faces = die.faces.OrderBy(x => x.faceId, StringComparer.Ordinal).ToArray();
                var units = ProbabilityCalculator.BuildUnits(faces, s.hand.largeBuff, s.hand.smallBuff, cfg, Sources(s).Count(x => x.abilityId == "C34"), Sources(s).Count(x => x.abilityId == "C35"));
                int sample = DrawRaw(s, diceRng, "dice", "DieFace", die.dieId, cfg.probabilityUnits).rawTicket, faceIndex = 0, ceiling = units[0];
                while (sample >= ceiling) ceiling += units[++faceIndex];
                var face = faces[faceIndex]; var r = s.hand.currentResults.First(x => x.dieId == die.dieId);
                r.currentFaceId = face.faceId; r.currentPip = face.permanentPip; r.currentColor = face.baseColor; r.hasRolled = true; r.remoteOverride = false;
                int remoteIndex = remoteIds == null ? -1 : Array.IndexOf(remoteIds, die.dieId);
                if (remoteIndex >= 0) { r.currentPip = remotePips[remoteIndex]; r.remoteOverride = true; }
            }
            s.diceRngState = diceRng.State; s.effectRngState = effectRng.State;
            if (s.dice.Count == 0)
            {
                if (s.stage.qualified) FinalizeStage(s);
                else { s.phase = "RunFailed"; s.failureReason = "NoDice"; }
                return;
            }
            int remainingUnlocked = s.dice.Count - s.hand.orderedLockedIds.Count;
            if (s.hand.rollsRemaining == 0)
            {
                var p = cfg.Controller("C05").parameters;
                var eligible = Sources(s).Where(x => x.abilityId == "C05" && !s.hand.c05UsedEmitterIds.Contains(x.emitterId)).ToArray();
                if (remainingUnlocked >= p.minUnlocked && remainingUnlocked <= p.maxUnlocked)
                    foreach (var source in eligible)
                    {
                        s.hand.rollsRemaining = checked(s.hand.rollsRemaining + p.rollGrant);
                        s.hand.c05UsedEmitterIds.Add(source.emitterId);
                        s.lastEffects.Add(new EffectRecord { kind = "ExtraRolls", sourceId = "C05", detail = source.emitterId, newValue = p.rollGrant });
                    }
            }
            s.phase = "Selecting";
        }

        /// <summary>
        /// v0.9: adds growth to every real instance of one controller. Mimics share the owner's value and never grow.
        /// v1.0: a controller disabled for the stage has no ability, so its growth value does not move either — this
        /// includes the countdown of C141 (spec 2.3 step 3); a boss modifier can also freeze every growth value.
        /// </summary>
        private void Grow(RunState s, string definitionId, long delta)
        {
            if (delta == 0 || Fx(s, "GrowthFreeze")) return;
            var disabled = ActiveDisabled(s);
            foreach (var c in s.controllers.Where(x => x.definitionId == definitionId && !disabled.Contains(x.instanceId)))
            {
                long before = c.state.growth; c.state.growth = checked(before + delta);
                s.lastEffects.Add(new EffectRecord { kind = "ControllerGrown", sourceId = definitionId, detail = c.instanceId, oldValue = before, newValue = c.state.growth });
            }
        }
        private static MimicBinding[] ActiveBindings(RunState s) { return s.stage != null && s.stage.started && !s.stage.settled ? s.stage.mimicBindings : null; }
        private static AbilitySource[] Sources(RunState s) { return ControllerRules.ResolveSources(s.controllers.ToArray(), ActiveBindings(s), ActiveDisabled(s)); }
        private static bool Has(RunState s, string id) { return s.controllers.Any(x => x.contentId == id); }
        private static DieInstance FindDie(RunState s, string id) { var d = s.dice.FirstOrDefault(x => x.dieId == id); Check(d != null, "目标骰子不存在。"); return d; }
        private static void RequirePhase(RunState s, params string[] phases) { Check(phases.Contains(s.phase), "当前阶段不能执行此操作。"); }
        private static void Check(bool condition, string message) { if (!condition) throw new RuleViolation(message); }
        private static CommandResult Reject(string error) { return new CommandResult { error = error }; }
        private sealed class RuleViolation : Exception { public RuleViolation(string message) : base(message) { } }
    }
}
