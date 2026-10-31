using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace DiceCraft.Core
{
    public sealed partial class GameSession
    {
        public static GameSession CreateDebugWorkspace(GameConfig config, ISaveStore debugStore, RunState source = null)
        {
            var result = new GameSession(config, debugStore);
            var snapshot = source == null ? result.CreateRun(new SessionCommand { commandId = "debug-blank", seed = 1 }) : source.Clone();
            result.ValidateState(snapshot);
            snapshot.sourceRunId = source == null ? "blank" : source.runId;
            snapshot.runId = "DEBUG_" + Guid.NewGuid().ToString("N");
            snapshot.debugWorkspace = true;
            snapshot.actionSequence = 0; snapshot.processedReceipts.Clear(); snapshot.lastEffects.Clear(); snapshot.lastRandomResults.Clear();
            snapshot.lastSubmission = null;
            if (snapshot.stage.settled)
            {
                snapshot.stage.settlementId = snapshot.runId + "/" + snapshot.stage.stageId;
                snapshot.lastStageSettlement.settlementId = snapshot.stage.settlementId;
            }
            result.ValidateState(snapshot); result.state = snapshot;
            return result;
        }

        public DebugScene ExportDebugScene() { lock (gate) return state == null ? null : DebugScene.FromState(state); }

        public CommandResult AbandonPendingSave()
        {
            lock (gate)
            {
                if (pendingSaveState == null) return Reject("没有待放弃的存档。");
                pendingSaveState = null; pendingReceipt = null;
                return new CommandResult { accepted = true, noChange = true };
            }
        }

        private void ApplyDebugScene(RunState s, DebugScene input)
        {
            Check(s.debugWorkspace, "调试修改只能在独立调试工作区执行。");
            Check(input != null && input.configVersion == cfg.configVersion, "调试场景配置版本无效。");
            Check(input.dice != null && input.controllers != null && input.inventory != null && input.handLevels != null && input.colorLevels != null
                && input.orderedLockedIds != null && input.c05UsedEmitterIds != null && input.randomOverrides != null, "调试场景缺少集合。");
            Check(input.dice.Count >= 1 && input.dice.Count <= cfg.debug.maxDice && input.dice.All(x => x != null), "调试骰池必须为1至32颗。");
            Check(input.controllers.All(x => x != null) && input.inventory.All(x => x != null) && input.handLevels.All(x => x != null) && input.colorLevels.All(x => x != null), "调试场景存在空元素。");
            foreach (var die in input.dice) cfg.ValidateFaces(die.faces);
            Check(input.capacityOverride >= 1 && input.capacityOverride <= cfg.debug.maxSubmitCapacity, "调试提交容量必须为1至32。");
            Check(input.gold >= 0 && input.gold <= cfg.debug.maxEditableWallet && input.submissionOrdinalGlobal >= 0 && input.stageIndex >= 0 && input.stageIndex < cfg.stages.Length, "调试钱包或关卡无效。");
            Check(input.remainingSubmissions >= -1 && input.remainingSubmissions <= cfg.debug.maxEditableBudget
                && input.rollsRemaining >= -1 && input.rollsRemaining <= cfg.debug.maxEditableBudget
                && input.submissionOrdinal >= 0 && input.submissionOrdinal <= cfg.debug.maxEditableBudget
                && input.stageRollBonus >= 0 && input.stageRollBonus <= cfg.debug.maxEditableBudget, "调试预算输入必须为0至999，-1仅表示使用默认预算。");
            Check(input.randomOverrides.All(x => x != null), "调试随机覆盖不完整。");
            // Only a target already lost in the committed history may remain unresolved.
            var historicalMissing = s.stage == null ? Array.Empty<string>() : s.stage.destroyedControllerIds.ToArray();
            var draft = input.Clone();
            s.isDebug = true; s.debugCapacityOverride = draft.capacityOverride;
            s.dice = draft.dice; s.controllers = draft.controllers; s.inventory = draft.inventory;
            s.handLevels = draft.handLevels; s.colorLevels = draft.colorLevels; s.gold = draft.gold;
            s.seed = draft.seed; s.diceRngState = draft.diceRngState; s.shopRngState = draft.shopRngState; s.effectRngState = draft.effectRngState;
            s.consumableSlots = draft.consumableSlots; s.itemDiscount = draft.itemDiscount;
            s.controllerOfferPurchases = draft.controllerOfferPurchases; s.consumableOfferPurchases = draft.consumableOfferPurchases; s.freeFirstRefreshOwned = draft.freeFirstRefreshOwned;
            s.previousPrimaryHandId = draft.previousPrimaryHandId; s.submissionOrdinalGlobal = draft.submissionOrdinalGlobal; s.randomOverrides = draft.randomOverrides;
            s.controllerSlots = cfg.initialControllerSlots; s.failureReason = null; s.lastSubmission = null; s.lastStageSettlement = null; s.shop = null;
            s.nextDieSerial = Math.Max(s.nextDieSerial, checked(s.dice.Max(d => d.createdSerial) + 1));
            int maxController = s.controllers.Select(c => c.acquiredSerial).DefaultIfEmpty(0).Max();
            int maxItem = s.inventory.Select(i => ParseItemSerial(i.instanceId)).DefaultIfEmpty(0).Max();
            s.nextItemSerial = Math.Max(s.nextItemSerial, checked(Math.Max(maxController, maxItem) + 1));
            Check(!s.inventory.Any(i => s.consumedItemIds.Contains(i.instanceId)), "已消耗的实例ID不能复用；请创建新实例。");
            var stage = cfg.stages[draft.stageIndex];
            s.stage = new StageState {
                stageId = stage.stageId, stageIndex = draft.stageIndex, risk = draft.risk, target = draft.target, score = draft.score,
                capacity = draft.capacityOverride, reward = draft.risk ? stage.riskReward : stage.normalReward,
                remainingSubmissions = draft.remainingSubmissions < 0 ? ControllerRules.SubmissionBudget(s.controllers, cfg) : draft.remainingSubmissions,
                submissionOrdinal = draft.submissionOrdinal, stageRollBonus = draft.stageRollBonus, started = true,
                controllerOrderSnapshot = s.controllers.Select(c => c.instanceId).ToArray(),
                mimicBindings = draft.mimicBindings ?? ControllerRules.FreezeBindings(s.controllers.ToArray()),
                destroyedControllerIds = historicalMissing.ToList(),
                submissionBonusSourceIds = s.controllers.Where(c => c.enchantmentId == "submit1").Select(c => c.instanceId).ToArray(),
                qualified = ExactScoreMath.Compare(draft.score, draft.target) >= 0
            };
            ValidateBindings(s);
            foreach (var die in s.dice.Where(d => d.lifetime == "stage_temporary")) die.createdStageId = stage.stageId;
            StartHand(s);
            if (draft.rollsRemaining >= 0) s.hand.rollsRemaining = draft.rollsRemaining;
            s.hand.redWave = draft.redWave; s.hand.largeBuff = draft.largeBuff; s.hand.smallBuff = draft.smallBuff;
            s.hand.c05UsedEmitterIds = draft.c05UsedEmitterIds;
            if (draft.currentResults != null)
            {
                Check(draft.currentResults.All(r => r != null), "调试当前结果不完整。");
                s.hand.currentResults = draft.currentResults;
                foreach (var r in s.hand.currentResults)
                {
                    var d = FindDie(s, r.dieId);
                    if (r.hasRolled)
                    {
                        var face = d.faces.FirstOrDefault(f => f.faceId == r.currentFaceId);
                        Check(face != null, "调试物理面ID无效。"); r.currentColor = face.baseColor;
                        if (!r.remoteOverride) r.currentPip = face.permanentPip;
                    }
                    r.isLocked = draft.orderedLockedIds.Contains(r.dieId);
                }
                s.hand.hasRolled = s.hand.currentResults.Any(r => r.hasRolled);
                s.hand.rollsUsed = s.hand.hasRolled ? 1 : 0;
                s.hand.orderedLockedIds = draft.orderedLockedIds;
                s.phase = s.hand.hasRolled ? "Selecting" : "HandReady";
            }
            else Check(draft.orderedLockedIds.Count == 0, "没有当前结果时不能指定已锁骰。");
            s.lastEffects.Add(new EffectRecord { kind = "DebugSceneApplied", sourceId = s.sourceRunId, detail = draft.note, newValue = s.stage.capacity });
        }

        private static int ParseItemSerial(string id)
        {
            int n;
            Check(id != null && id.StartsWith("ITEM_", StringComparison.Ordinal) && int.TryParse(id.Substring(5), NumberStyles.None, CultureInfo.InvariantCulture, out n), "物品实例ID格式无效。");
            return int.Parse(id.Substring(5), CultureInfo.InvariantCulture);
        }

        private bool ReorderControllers(RunState s, string[] order)
        {
            RequirePhase(s, "Shop", "StagePreview");
            Check(order != null && order.Length == s.controllers.Count && order.Distinct(StringComparer.Ordinal).Count() == order.Length
                && order.All(id => s.controllers.Any(c => c.instanceId == id)), "控制器调序必须保留同一组实例。");
            if (order.SequenceEqual(s.controllers.Select(c => c.instanceId))) return false;
            s.controllers = order.Select(id => s.controllers.First(c => c.instanceId == id)).ToList();
            if (s.phase == "StagePreview") s.stage.capacity = ShopCapacity(s);
            s.lastEffects.Add(new EffectRecord { kind = "ControllersReordered", detail = string.Join(",", order) });
            return true;
        }

        private RandomRecord DrawRaw(RunState s, GameRng rng, string stream, string kind, string sourceId, int count)
        {
            uint before = rng.State; int ticket = rng.NextIndex(count);
            var record = new RandomRecord { stream = stream, kind = kind, sourceId = sourceId, beforeState = before, afterState = rng.State,
                range = count, rawTicket = ticket, rawValue = ticket.ToString(CultureInfo.InvariantCulture), appliedValue = ticket.ToString(CultureInfo.InvariantCulture) };
            s.lastRandomResults.Add(record); return record;
        }

        private string ApplyOverride(RunState s, RandomRecord record, string rawValue)
        {
            record.rawValue = rawValue; record.appliedValue = rawValue;
            if (s.isDebug && s.debugWorkspace)
            {
                var branch = s.randomOverrides.FirstOrDefault(x => x.kind == record.kind && x.sourceId == record.sourceId);
                if (branch != null) { record.overridden = true; record.appliedValue = branch.value; s.randomOverrides.Remove(branch); }
            }
            return record.appliedValue;
        }

        private bool DrawRisk(RunState s, GameRng rng, string kind, string id, int percent)
        {
            var record = DrawRaw(s, rng, "effect", kind, id, 100);
            return ApplyOverride(s, record, record.rawTicket < percent ? "break" : "keep") == "break";
        }

        private void ApplyPreviewOverrides(SubmissionSnapshot snapshot, RunState s)
        {
            if (!s.isDebug || !s.debugWorkspace) return;
            snapshot.resolvedRandomChoices = s.randomOverrides.Where(x => x.kind == "C53")
                .Select(x => new RandomMultiplierChoice { ownerInstanceId = x.sourceId, units2 = int.Parse(x.value, CultureInfo.InvariantCulture) }).ToArray();
        }

        // Hash the complete committed rules state, excluding the receipt chain itself to avoid a self-reference.
        public static string StateHash(RunState snapshot)
        {
            var text = new StringBuilder(); AppendCanonical(text, snapshot);
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "").ToLowerInvariant();
        }

        private static void AppendCanonical(StringBuilder output, object value)
        {
            if (value == null) { output.Append("null;"); return; }
            if (value is string) { string s = (string)value; output.Append(s.Length).Append(':').Append(s).Append(';'); return; }
            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is decimal) { output.Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append(';'); return; }
            if (value is IEnumerable) { output.Append('['); foreach (var item in (IEnumerable)value) AppendCanonical(output, item); output.Append(']'); return; }
            output.Append('{');
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public).OrderBy(f => f.Name, StringComparer.Ordinal))
            {
                if (type == typeof(RunState) && field.Name == "processedReceipts") continue;
                // JsonUtility restores optional null strings as empty strings. Their rule meaning is
                // identical; numeric decimal fields are validated separately and cannot be empty.
                object fieldValue = field.GetValue(value);
                if (field.FieldType == typeof(string) && fieldValue == null) fieldValue = "";
                if (type == typeof(RunState))
                {
                    // JsonUtility also materializes null nested DTOs as default objects. An
                    // absent shop/hand/settlement has no identity; hash that semantic absence
                    // exactly as the original null. Active DTO identities are validated first.
                    if (field.Name == "shop" && fieldValue is ShopState shop && string.IsNullOrEmpty(shop.shopId) && !shop.generated) fieldValue = null;
                    if (field.Name == "hand" && fieldValue is HandState hand && string.IsNullOrEmpty(hand.handId)) fieldValue = null;
                    if (field.Name == "lastStageSettlement" && fieldValue is StageSettlement settlement && string.IsNullOrEmpty(settlement.settlementId)) fieldValue = null;
                }
                output.Append(field.Name).Append('='); AppendCanonical(output, fieldValue);
            }
            output.Append('}');
        }
    }
}
