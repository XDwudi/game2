using System;
using System.Linq;
using System.Text;

namespace DiceCraft.Core
{
    public sealed partial class GameSession
    {
        /// <summary>Validates a detached stable snapshot. Loading never draws or replays an event.</summary>
        public void ValidateState(RunState s)
        {
            Check(s != null && s.saveSchemaVersion == cfg.saveSchemaVersion && s.configVersion == cfg.configVersion, "存档版本不兼容，请保留旧存档并开始新挑战。");
            Check(!string.IsNullOrEmpty(s.runId), "存档缺少挑战ID。");
            Check(new[] { "StagePreview", "HandReady", "Selecting", "StageSettlement", "Shop", "RunWon", "RunFailed" }.Contains(s.phase), "存档阶段无效。");
            Check(s.gold >= 0 && s.actionSequence >= 0 && s.submissionOrdinalGlobal >= 0, "存档金币或操作序号无效。");
            Check(!s.isDebug || s.debugWorkspace, "调试存档必须属于独立工作区。");
            Check(s.debugWorkspace || (s.debugCapacityOverride == 0 && s.randomOverrides != null && s.randomOverrides.Count == 0), "普通存档不能包含实验覆盖。");
            Check(s.baseSubmitCapacity == cfg.initialSubmitCapacity, "基础提交容量无效。");
            Check(s.controllerSlots == cfg.initialControllerSlots || (s.isDebug && s.controllerSlots >= 1 && s.controllerSlots <= cfg.debug.maxControllerInstances), "控制器槽位无效。");
            Check(s.consumableSlots >= cfg.initialConsumableSlots && s.consumableSlots <= (s.isDebug ? cfg.debug.maxConsumableInventory : cfg.maxConsumableSlots)
                && s.itemDiscount >= 0 && s.itemDiscount <= cfg.maxItemDiscount, "道具容量或折扣无效。");
            Check(s.controllerOfferPurchases >= 0 && s.controllerOfferPurchases <= cfg.shop.maxOfferExpansionPurchases
                && s.consumableOfferPurchases >= 0 && s.consumableOfferPurchases <= cfg.shop.maxOfferExpansionPurchases
                && ControllerOfferCount(s) <= cfg.shop.maxControllerOffers && ConsumableOfferCount(s) <= cfg.shop.maxConsumableOffers, "商店扩容状态无效。");
            Check(s.debugCapacityOverride >= 0 && s.debugCapacityOverride <= cfg.debug.maxSubmitCapacity && (s.isDebug || s.debugCapacityOverride == 0), "调试容量覆盖无效。");
            Check(s.nextDieSerial > 0 && s.nextItemSerial > 0 && s.nextOfferSerial > 0 && s.nextHandSerial > 0, "实例计数器无效。");
            Check(s.diceRngState != 0 && s.shopRngState != 0 && s.effectRngState != 0, "随机流状态无效。");
            Check(s.dice != null && s.controllers != null && s.inventory != null && s.handLevels != null && s.colorLevels != null && s.processedReceipts != null
                && s.lastEffects != null && s.lastRandomResults != null && s.randomOverrides != null && s.consumedItemIds != null, "存档缺少集合。");
            Check(s.dice.All(x => x != null) && s.dice.Count <= (s.isDebug ? cfg.debug.maxDice : cfg.maxActiveDice), "骰子数量无效。");
            Check(s.isDebug || (PermanentCount(s) <= cfg.maxPermanentDice && s.dice.Count(x => x.lifetime == "stage_temporary") <= cfg.maxTemporaryDice), "普通骰池数量超限。");
            Check(s.dice.Select(x => x.dieId).Distinct(StringComparer.Ordinal).Count() == s.dice.Count && s.dice.Select(x => x.createdSerial).Distinct().Count() == s.dice.Count, "骰子身份重复。");
            foreach (var d in s.dice)
            {
                Check(d.createdSerial > 0 && d.createdSerial < s.nextDieSerial && d.dieId == "D" + d.createdSerial.ToString("D3"), "骰子身份无效。");
                Check((d.lifetime == "permanent" || d.lifetime == "stage_temporary") && d.dataRevision >= 0, "骰子寿命或版本无效。"); cfg.ValidateFaces(d.faces);
            }
            ControllerRules.ValidateEquipment(s.controllers, cfg, s.isDebug);
            Check(s.controllers.All(c => c.alive && c.acquiredSerial > 0 && c.acquiredSerial < s.nextItemSerial && c.instanceId == "ITEM_" + c.acquiredSerial.ToString("D5")), "控制器实例身份无效。");
            foreach (var c in s.controllers) ValidateDynamicState(c, false);
            Check(s.inventory.Count <= (s.isDebug ? cfg.debug.maxConsumableInventory : s.consumableSlots)
                && s.inventory.All(x => x != null && cfg.consumables.Any(c => c.id == x.contentId)), "道具库存无效。");
            foreach (var item in s.inventory) { int serial = ParseItemSerial(item.instanceId); Check(serial > 0 && serial < s.nextItemSerial && item.instanceId == "ITEM_" + serial.ToString("D5"), "道具实例来源无效。"); }
            var instances = s.controllers.Select(x => x.instanceId).Concat(s.inventory.Select(x => x.instanceId)).ToArray();
            Check(instances.Distinct(StringComparer.Ordinal).Count() == instances.Length, "物品实例身份重复。");
            Check(s.consumedItemIds.All(x => !string.IsNullOrEmpty(x)) && s.consumedItemIds.Distinct(StringComparer.Ordinal).Count() == s.consumedItemIds.Count
                && !s.inventory.Any(x => s.consumedItemIds.Contains(x.instanceId)), "已消耗道具重复存在。");
            Check(s.handLevels.Count == cfg.handDefinitions.Length && s.handLevels.All(x => x != null && cfg.handDefinitions.Any(h => h.id == x.handId)
                && x.level >= 0 && x.level <= (s.isDebug ? cfg.debug.maxHandLevel : cfg.maxHandLevel))
                && s.handLevels.Select(x => x.handId).Distinct().Count() == s.handLevels.Count, "牌型等级无效。");
            Check(s.colorLevels.Count == cfg.colorDefinitions.Length && s.colorLevels.All(x => x != null && cfg.colorDefinitions.Any(h => h.id == x.handId)
                && x.level >= 0 && x.level <= (s.isDebug ? cfg.debug.maxColorLevel : cfg.maxColorLevel))
                && s.colorLevels.Select(x => x.handId).Distinct().Count() == s.colorLevels.Count, "颜色等级无效。");
            Check(string.IsNullOrEmpty(s.previousPrimaryHandId) || s.previousPrimaryHandId == "scatter" || cfg.handDefinitions.Any(h => h.id == s.previousPrimaryHandId), "上一主牌型无效。");
            Check(s.stage != null && s.stage.stageIndex >= 0 && s.stage.stageIndex < cfg.stages.Length && s.stage.stageId == cfg.stages[s.stage.stageIndex].stageId, "关卡身份无效。");
            Check(ExactScoreMath.Parse(s.stage.target) > 0 && ExactScoreMath.Parse(s.stage.score) >= 0 && s.stage.remainingSubmissions >= 0
                && s.stage.submissionOrdinal >= 0 && s.stage.stageRollBonus >= 0 && s.stage.capacity >= 1 && s.stage.capacity <= (s.isDebug ? cfg.debug.maxSubmitCapacity : cfg.maxSubmitCapacity), "关卡数值无效。");
            Check(s.stage.qualified == (ExactScoreMath.Compare(s.stage.score, s.stage.target) >= 0), "达标标记与累计分不一致。");
            Check(!s.stage.settled || (s.stage.qualified && s.stage.settlementId == s.runId + "/" + s.stage.stageId), "关卡结算身份无效。");
            var stageDef = cfg.stages[s.stage.stageIndex];
            Check(!s.stage.risk || stageDef.riskEnabled, "高压分支无效。");
            Check(s.isDebug || (s.stage.target == (s.stage.risk ? stageDef.riskTarget : stageDef.normalTarget)
                && s.stage.reward == (s.stage.risk ? stageDef.riskReward : stageDef.normalReward)), "关卡目标或奖励与配置不符。");
            Check(s.dice.Where(x => x.lifetime == "stage_temporary").All(x => x.createdStageId == s.stage.stageId), "临时骰来自其他关卡。");
            ValidateBindings(s);
            if (s.phase == "HandReady" || s.phase == "Selecting") ValidateActiveHand(s);
            if (s.phase == "StageSettlement" || s.phase == "Shop" || s.phase == "RunWon")
                Check(s.stage.settled && s.lastStageSettlement != null && s.lastStageSettlement.stageId == s.stage.stageId && s.stage.qualified, "结算阶段无效。");
            if (s.stage.settled)
            {
                var r = s.lastStageSettlement;
                Check(r != null && r.settlementId == s.stage.settlementId && r.stageId == s.stage.stageId && r.score == s.stage.score && r.target == s.stage.target
                    && r.remainingSubmissions == s.stage.remainingSubmissions, "结算回执不匹配。");
                Check(r.goldBefore >= 0 && r.pigSourceCount >= 0 && r.pig == (r.pigSourceCount > 0) && r.interestPrincipal == Math.Min(r.goldBefore, cfg.maxInterestPrincipal)
                    && r.interest == checked((1L + r.pigSourceCount * cfg.Controller("C01").parameters.extraInterestPerTen) * (r.interestPrincipal / cfg.interestDivisor))
                    && r.unusedSubmissionReward == checked((long)r.remainingSubmissions * cfg.goldPerUnusedSubmission)
                    && r.goldAfter == checked(r.goldBefore + r.baseReward + r.unusedSubmissionReward + r.interest), "结算收益无效。");
            }
            if (s.phase == "RunWon") Check(s.stage.stageIndex == cfg.stages.Length - 1, "非最终关不能胜利。");
            if (s.phase == "StageSettlement") Check(new[] { "Shop", "RunWon", "RunFailed" }.Contains(s.stage.settlementContinuation), "结算后继无效。");
            if (s.phase == "Shop") { Check(s.stage.stageIndex < cfg.stages.Length - 1 && s.dice.Count > 0, "最终关或空骰组不能进入商店。"); ValidateShop(s, instances); }
            Check(s.processedReceipts.All(x => x != null && x.command != null && !string.IsNullOrEmpty(x.commandId) && x.commandId == x.command.commandId
                && x.actionSequence > 0 && x.actionSequence <= s.actionSequence && x.beforeSequence == x.actionSequence - 1 && x.effects != null && x.randomResults != null
                && x.goldBefore >= 0 && x.goldAfter >= 0 && !string.IsNullOrEmpty(x.stateHash)), "命令回执无效。");
            foreach (var r in s.processedReceipts) {
                ExactScoreMath.Parse(r.score); ValidateRandomRecords(r.randomResults);
                if (r.command.kind == CommandKind.Submit) { Check(r.submission != null && r.submission.snapshot != null && r.submission.breakdown != null
                    && r.score == r.submission.breakdown.score && r.goldBefore == r.submission.snapshot.wallet, "提交回执缺少完整计分计划或冻结钱包不一致。"); ValidateSubmissionHistory(r.submission, s.runId); }
            }
            Check(s.processedReceipts.Select(x => x.commandId).Distinct().Count() == s.processedReceipts.Count, "命令回执重复。");
            for (int i = 1; i < s.processedReceipts.Count; i++) Check(s.processedReceipts[i].actionSequence > s.processedReceipts[i - 1].actionSequence, "回执序号无序。");
            ValidateRandomRecords(s.lastRandomResults); ValidateRandomOverrides(s);
            if (s.lastSubmission != null) ValidateSubmissionHistory(s.lastSubmission, s.runId);
        }

        private void ValidateDynamicState(ControllerInstance c, bool offer)
        {
            Check(c.state != null && c.state.fundAccrual >= 0 && c.state.fundAccrual % 3 == 0 && c.state.turnoverUnits2 >= 3, "控制器动态状态无效。");
            Check(c.definitionId == "C41" || c.definitionId == "C52" || c.state.fundAccrual == 0, "非基金实例不能保存基金累计。");
            Check(c.definitionId == "C51" || c.state.turnoverUnits2 == 3, "非周转实例不能保存成长倍率。");
            Check(c.definitionId == "C45" ? (offer ? string.IsNullOrEmpty(c.state.activeColor) : c.state.activeColor == "red" || c.state.activeColor == "blue") : string.IsNullOrEmpty(c.state.activeColor), "控制器颜色状态无效。");
            if (offer) Check(c.state.fundAccrual == 0 && c.state.turnoverUnits2 == 3, "商品不能继承旧实例状态。");
        }

        private void ValidateBindings(RunState s)
        {
            Check(s.stage.mimicBindings != null && s.stage.controllerOrderSnapshot != null && s.stage.submissionBonusSourceIds != null && s.stage.destroyedControllerIds != null, "关卡来源快照缺失。");
            var ids = s.controllers.Select(c => c.instanceId).ToArray();
            Check(s.stage.controllerOrderSnapshot.Distinct().Count() == s.stage.controllerOrderSnapshot.Length
                && s.stage.destroyedControllerIds.Distinct().Count() == s.stage.destroyedControllerIds.Count, "关卡来源身份重复。");
            Check(s.stage.mimicBindings.All(b => b != null && !string.IsNullOrEmpty(b.sourceInstanceId))
                && s.stage.mimicBindings.Select(b => b.sourceInstanceId).Distinct().Count() == s.stage.mimicBindings.Length, "复制绑定身份无效。");
            // Settled stages retain historical bindings even after ordinary shop sales and replacements.
            if (!s.stage.settled)
                foreach (var b in s.stage.mimicBindings)
                {
                    Check(s.controllers.Any(c => c.instanceId == b.sourceInstanceId && c.definitionId == "C41") || s.stage.destroyedControllerIds.Contains(b.sourceInstanceId), "复制绑定来源无效。");
                    Check(string.IsNullOrEmpty(b.targetInstanceId) || ids.Contains(b.targetInstanceId) || s.stage.destroyedControllerIds.Contains(b.targetInstanceId), "复制绑定目标无效；仅合法损坏历史允许缺失。");
                }
            var map = s.stage.mimicBindings.ToDictionary(b => b.sourceInstanceId, b => b.targetInstanceId, StringComparer.Ordinal);
            foreach (var start in map.Keys)
            {
                var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal); string current = start;
                while (!string.IsNullOrEmpty(current) && map.ContainsKey(current)) { Check(seen.Add(current) && seen.Count <= cfg.debug.maxControllerInstances, "复制绑定不能成环。"); current = map[current]; }
            }
        }

        private void ValidateActiveHand(RunState s)
        {
            Check(s.stage.started && !s.stage.settled && (s.isDebug || s.stage.remainingSubmissions > 0) && s.dice.Count > 0, "活动关卡无效。");
            Check(s.hand != null && s.hand.orderedLockedIds != null && s.hand.currentResults != null && s.hand.c05UsedEmitterIds != null && s.hand.rollBonusSourceIds != null, "手牌状态缺失。");
            Check(s.hand.rollsRemaining >= 0 && s.hand.rollsUsed >= 0 && !string.IsNullOrEmpty(s.hand.handId), "投掷预算无效。");
            Check(s.hand.currentResults.Count == s.dice.Count && s.hand.currentResults.All(r => r != null && s.dice.Any(d => d.dieId == r.dieId))
                && s.hand.currentResults.Select(x => x.dieId).Distinct().Count() == s.dice.Count, "当前骰结果身份不匹配。");
            Check(s.hand.orderedLockedIds.Count <= s.stage.capacity && s.hand.orderedLockedIds.Distinct().Count() == s.hand.orderedLockedIds.Count
                && s.hand.orderedLockedIds.All(id => s.hand.currentResults.Any(r => r.dieId == id && r.isLocked && r.hasRolled)), "锁定数组无效。");
            Check(s.hand.currentResults.All(r => r.isLocked == s.hand.orderedLockedIds.Contains(r.dieId)), "锁定标志不一致。");
            foreach (var r in s.hand.currentResults.Where(x => x.hasRolled))
            {
                var face = FindDie(s, r.dieId).faces.FirstOrDefault(f => f.faceId == r.currentFaceId);
                Check(r.currentPip >= cfg.minPipValue && r.currentPip <= cfg.maxPipValue && face != null && r.currentColor == face.baseColor, "当前物理面或覆盖值无效。");
            }
            Check(s.phase != "HandReady" || (!s.hand.hasRolled && s.hand.orderedLockedIds.Count == 0 && s.hand.currentResults.All(r => !r.hasRolled)), "首掷前状态不一致。");
            Check(s.phase != "Selecting" || (s.hand.hasRolled && s.hand.currentResults.All(r => r.hasRolled)), "选择阶段缺少投掷结果。");
            var c05 = Sources(s).Where(x => x.abilityId == "C05").Select(x => x.emitterId).ToArray();
            Check(s.hand.c05UsedEmitterIds.Distinct().Count() == s.hand.c05UsedEmitterIds.Count && s.hand.c05UsedEmitterIds.All(c05.Contains), "C05来源触发记录无效。");
        }

        private void ValidateShop(RunState s, string[] instances)
        {
            var shop = s.shop;
            Check(shop != null && shop.generated && shop.offers != null && !string.IsNullOrEmpty(shop.generationId) && shop.controllerRefreshCount >= 0
                && shop.controllerGeneration == shop.controllerRefreshCount && shop.selfStudyUsed >= 0 && shop.selfStudyUsed <= cfg.shop.selfStudyLimit, "商店状态无效。");
            Check(shop.offers.All(x => x != null && !string.IsNullOrEmpty(x.offerId)) && shop.offers.Select(x => x.offerId).Distinct().Count() == shop.offers.Count, "商品身份重复。");
            Check(shop.offers.Count(o => o.category == "controller") <= ControllerOfferCount(s) && shop.offers.Count(o => o.category == "consumable") <= ConsumableOfferCount(s), "商品格数量超限。");
            foreach (var o in shop.offers)
            {
                bool known = o.category == "controller" ? cfg.controllers.Any(x => x.id == o.contentId)
                    : o.category == "consumable" ? cfg.consumables.Any(x => x.id == o.contentId)
                    : o.category == "hand_upgrade" ? cfg.handDefinitions.Any(x => x.id == o.contentId) || cfg.colorDefinitions.Any(x => x.id == o.contentId)
                    : o.category == "die_upgrade" ? cfg.diceUpgrades.Any(x => x.id == o.contentId)
                    : o.category == "service" ? cfg.services.Any(x => x.id == o.contentId && x.id != "O03")
                    : o.category == "new_die" && o.contentId == "O03" && o.die != null;
                Check(known && o.basePrice >= 0 && o.priceReasons != null && o.batchId == BatchId(shop, o.category), "商品定义或批次无效。");
                if (o.category == "controller")
                {
                    var c = o.controller;
                    Check(c != null && c.alive && c.definitionId == o.contentId && ControllerRules.IsEnchantmentLegal(c.definitionId, c.enchantmentId, cfg)
                        && c.acquiredSerial > 0 && c.acquiredSerial < s.nextItemSerial && c.instanceId == "ITEM_" + c.acquiredSerial.ToString("D5")
                        && o.basePrice == cfg.Controller(c.definitionId).buyPrice, "控制器商品实例无效。");
                    ValidateDynamicState(c, true);
                }
                if (o.category == "new_die") { cfg.ValidateFaces(o.die.faces); Check(o.basePrice == QuoteNewDie(o.die), "新骰报价不匹配。"); }
            }
            foreach (var category in new[] { "controller", "consumable", "hand_upgrade", "die_upgrade", "service" })
            { var offers = shop.offers.Where(o => o.category == category).ToArray(); Check(offers.Select(o => o.contentId).Distinct().Count() == offers.Length, "同批商品定义重复。"); }
            var cards = shop.offers.Where(o => o.category == "controller").Select(o => o.controller).ToArray();
            Check(cards.Select(c => c.instanceId).Distinct().Count() == cards.Length && shop.offers.Where(o => o.category == "controller" && !o.sold).All(o => !instances.Contains(o.controller.instanceId)), "商品实例与装备身份冲突。");
        }

        private void ValidateRandomOverrides(RunState s)
        {
            Check(s.randomOverrides.All(x => x != null) && s.randomOverrides.Select(x => x.kind + "/" + x.sourceId).Distinct().Count() == s.randomOverrides.Count, "随机覆盖重复或缺失。");
            Check(s.randomOverrides.Count == 0 || (s.isDebug && s.debugWorkspace), "正常挑战不允许随机覆盖。");
            foreach (var o in s.randomOverrides)
            {
                if (o.kind == "C53") Check(s.controllers.Any(c => c.instanceId == o.sourceId && c.definitionId == "C53") && new[] { "1", "2", "4", "6", "8", "10", "12", "14", "16" }.Contains(o.value), "C53覆盖必须指向真实所有者和合法倍率。");
                else if (o.kind == "C45") Check(s.controllers.Any(c => c.instanceId == o.sourceId && c.definitionId == "C45") && (o.value == "red" || o.value == "blue"), "C45覆盖无效。");
                else if (o.kind == "C42") Check(Sources(s).Any(c => c.emitterId == o.sourceId && c.abilityId == "C42") && (o.value == "break" || o.value == "keep"), "C42覆盖无效。");
                else if (o.kind == "Glass") Check(s.dice.Any(d => d.dieId == o.sourceId && d.isGlass) && (o.value == "break" || o.value == "keep"), "玻璃覆盖无效。");
                else throw new RuleViolation("未知随机覆盖类型。");
            }
        }

        private static void ValidateRandomRecords(System.Collections.Generic.IEnumerable<RandomRecord> records)
        {
            Check(records.All(r => r != null && new[] { "dice", "shop", "effect" }.Contains(r.stream) && r.beforeState != 0 && r.afterState != 0
                && r.range > 0 && r.rawTicket >= 0 && r.rawTicket < r.range && r.rawValue != null && r.appliedValue != null), "随机回执无效。");
        }

        private void ValidateSubmissionHistory(SubmissionRecord record, string runId)
        {
            Check(record.snapshot != null && record.breakdown != null && record.snapshot.runId == runId && record.snapshot.configVersion == cfg.configVersion, "提交历史身份无效。");
            Check(record.snapshot.handLevels != null && record.snapshot.handLevels.Length == cfg.handDefinitions.Length
                && record.snapshot.colorLevels != null && record.snapshot.colorLevels.Length == cfg.colorDefinitions.Length
                && record.snapshot.equippedControllers != null && record.snapshot.mimicBindings != null && record.snapshot.resolvedRandomChoices != null, "提交历史必须保留完整等级和来源快照。");
            Check(record.snapshot.dice != null && record.snapshot.dice.All(d => d != null), "提交历史骰面缺失。");
            foreach (var die in record.snapshot.dice)
            {
                cfg.ValidateFaces(die.permanentFacesCopy);
                var face = die.permanentFacesCopy.FirstOrDefault(f => f.faceId == die.faceId);
                Check(face != null && die.permanentSixFaceSum == die.permanentFacesCopy.Sum(f => f.permanentPip)
                    && die.isGold == face.isGold && die.currentColor == face.baseColor, "提交历史与冻结物理面不一致。");
            }
            var expected = ScoreCalculator.Evaluate(record.snapshot, cfg);
            var a = new StringBuilder(); var b = new StringBuilder(); AppendCanonical(a, record.breakdown); AppendCanonical(b, expected);
            Check(a.ToString() == b.ToString(), "提交历史明细与冻结快照不一致。");
        }
    }
}
