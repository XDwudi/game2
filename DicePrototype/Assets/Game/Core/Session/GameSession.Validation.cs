using System;
using System.Linq;

namespace DiceCraft.Core
{
    public sealed partial class GameSession
    {
        /// <summary>Validate persisted state before publishing it. Loading never replays effects.</summary>
        public void ValidateState(RunState s)
        {
            Check(s != null && s.saveSchemaVersion == cfg.saveSchemaVersion && s.configVersion == cfg.configVersion, "存档版本不兼容，请保留旧存档并开始新挑战。");
            Check(!string.IsNullOrEmpty(s.runId), "存档缺少挑战 ID。");
            Check(new[] { "StagePreview", "HandReady", "Selecting", "StageSettlement", "Shop", "RunWon", "RunFailed" }.Contains(s.phase), "存档阶段无效。");
            Check(s.gold >= 0 && s.actionSequence >= 0, "存档金币或操作序号无效。");
            Check(s.baseSubmitCapacity >= cfg.initialSubmitCapacity && s.baseSubmitCapacity <= cfg.maxSubmitCapacity, "存档提交容量无效。");
            Check(s.controllerSlots >= cfg.initialControllerSlots && s.controllerSlots <= cfg.maxControllerSlots && (s.isDebug || s.controllerSlots == cfg.initialControllerSlots), "存档控制器槽位无效。");
            Check(s.consumableSlots >= cfg.initialConsumableSlots && s.consumableSlots <= cfg.maxConsumableSlots && s.itemDiscount >= 0 && s.itemDiscount <= cfg.maxItemDiscount, "存档道具容量或折扣无效。");
            Check(s.nextDieSerial > 0 && s.nextItemSerial > 0 && s.nextOfferSerial > 0 && s.nextHandSerial > 0, "存档实例计数器无效。");
            Check(s.diceRngState != 0 && s.shopRngState != 0 && s.effectRngState != 0, "存档随机流状态无效。");
            Check(s.dice != null && s.controllers != null && s.inventory != null && s.handLevels != null && s.processedReceipts != null && s.lastEffects != null && s.consumedItemIds != null, "存档缺少集合。");
            Check(s.dice.All(x => x != null) && s.dice.Count <= cfg.maxActiveDice && PermanentCount(s) <= cfg.maxPermanentDice && s.dice.Count(x => x.lifetime == "stage_temporary") <= cfg.maxTemporaryDice, "存档骰子数量无效。");
            Check(s.dice.Select(x => x.dieId).Distinct().Count() == s.dice.Count && s.dice.Select(x => x.createdSerial).Distinct().Count() == s.dice.Count, "存档骰子身份重复。");
            foreach (var d in s.dice)
            {
                Check(d.createdSerial > 0 && d.createdSerial < s.nextDieSerial && d.dieId == "D" + d.createdSerial.ToString("D3"), "存档骰子身份无效。");
                Check(d.lifetime == "permanent" || d.lifetime == "stage_temporary", "存档骰子寿命无效。");
                Check(d.dataRevision >= 0, "存档骰子版本无效。");
                cfg.ValidateFaces(d.faces);
            }
            Check(s.controllers.Count <= s.controllerSlots && s.inventory.Count <= s.consumableSlots, "存档物品超过槽位。");
            Check(s.controllers.All(x => x != null && cfg.controllers.Any(c => c.id == x.contentId)) && s.controllers.Select(x => x.contentId).Distinct().Count() == s.controllers.Count, "存档控制器无效或重复。");
            Check(s.inventory.All(x => x != null && cfg.consumables.Any(c => c.id == x.contentId)), "存档道具无效。");
            var instances = s.controllers.Concat(s.inventory).Select(x => x.instanceId).ToArray();
            Check(instances.All(x => !string.IsNullOrEmpty(x)) && instances.Distinct().Count() == instances.Length, "存档物品身份无效。");
            Check(s.consumedItemIds.Distinct().Count() == s.consumedItemIds.Count && !s.inventory.Any(x => s.consumedItemIds.Contains(x.instanceId)), "存档已消耗道具重复存在。");
            Check(s.handLevels.Count == cfg.handDefinitions.Length && s.handLevels.All(x => x != null && cfg.handDefinitions.Any(h => h.id == x.handId && x.level >= 0 && x.level <= h.maxLevel)) && s.handLevels.Select(x => x.handId).Distinct().Count() == s.handLevels.Count, "存档牌型等级无效。");
            Check(s.stage != null && s.stage.stageIndex >= 0 && s.stage.stageIndex < cfg.stages.Length && s.stage.stageId == cfg.stages[s.stage.stageIndex].stageId, "存档关卡无效。");
            Check(s.stage.target > 0 && s.stage.score >= 0 && s.stage.remainingSubmissions >= 0 && s.stage.submissionOrdinal >= 0 && s.stage.stageRollBonus >= 0 && s.stage.capacity >= 1 && s.stage.capacity <= cfg.maxSubmitCapacity, "存档关卡数值无效。");
            Check(!s.stage.risk || cfg.stages[s.stage.stageIndex].riskEnabled, "存档高压分支无效。");
            var stageDefinition = cfg.stages[s.stage.stageIndex];
            Check(s.isDebug || (s.stage.target == (s.stage.risk ? stageDefinition.riskTarget : stageDefinition.normalTarget)
                && s.stage.reward == (s.stage.risk ? stageDefinition.riskReward : stageDefinition.normalReward)), "存档关卡目标或奖励不匹配配置。");
            Check(s.dice.Where(x => x.lifetime == "stage_temporary").All(x => x.createdStageId == s.stage.stageId), "存档临时骰来自其他关卡。");
            if (s.phase == "HandReady" || s.phase == "Selecting")
            {
                Check(s.stage.started && !s.stage.settled && s.stage.remainingSubmissions > 0 && s.dice.Count > 0, "存档活动关卡无效。");
                Check(s.hand != null && s.hand.orderedLockedIds != null && s.hand.currentResults != null, "存档缺少手牌状态。");
                Check(s.hand.rollsRemaining >= 0 && s.hand.rollsUsed >= 0 && !string.IsNullOrEmpty(s.hand.handId), "存档投掷次数无效。");
                Check(s.hand.currentResults.Count == s.dice.Count && s.hand.currentResults.All(r => r != null && s.dice.Any(d => d.dieId == r.dieId)) && s.hand.currentResults.Select(x => x.dieId).Distinct().Count() == s.dice.Count, "存档当前骰结果身份不匹配。");
                Check(s.hand.orderedLockedIds.Count <= s.stage.capacity && s.hand.orderedLockedIds.Distinct().Count() == s.hand.orderedLockedIds.Count && s.hand.orderedLockedIds.All(id => s.hand.currentResults.Any(r => r.dieId == id && r.isLocked)), "存档锁定数组无效。");
                Check(s.hand.currentResults.All(r => r.isLocked == s.hand.orderedLockedIds.Contains(r.dieId)), "存档锁定标记不一致。");
                foreach (var r in s.hand.currentResults.Where(x => x.hasRolled))
                    Check(r.currentPip >= cfg.minPipValue && r.currentPip <= cfg.maxPipValue && (r.currentColor == "red" || r.currentColor == "blue") && FindDie(s, r.dieId).faces.Any(f => f.faceId == r.currentFaceId), "存档骰面结果无效。");
                Check(s.phase != "HandReady" || (!s.hand.hasRolled && s.hand.orderedLockedIds.Count == 0 && s.hand.currentResults.All(r => !r.hasRolled)), "存档首掷前状态不一致。");
                Check(s.phase != "Selecting" || (s.hand.hasRolled && s.hand.currentResults.All(r => r.hasRolled)), "存档选骰阶段没有投掷结果。");
            }
            if (s.phase == "StageSettlement" || s.phase == "Shop" || s.phase == "RunWon")
                Check(s.stage.settled && s.lastStageSettlement != null && s.lastStageSettlement.stageId == s.stage.stageId && s.stage.score >= s.stage.target, "存档结算状态无效。");
            if (s.phase == "RunWon") Check(s.stage.stageIndex == cfg.stages.Length - 1, "非最终关不能保存为胜利。");
            if (s.phase == "StageSettlement") Check(new[] { "Shop", "RunWon", "RunFailed" }.Contains(s.stage.settlementContinuation), "存档结算去向无效。");
            if (s.phase == "Shop") Check(s.stage.stageIndex < cfg.stages.Length - 1 && s.dice.Count > 0, "最终关或空骰组不能进入商店。");
            if (s.phase == "Shop")
            {
                Check(s.shop != null && s.shop.generated && s.shop.offers != null && s.shop.selfStudyUsed >= 0 && s.shop.selfStudyUsed <= cfg.shop.selfStudyLimit, "存档商店无效。");
                Check(s.shop.offers.All(x => x != null && !string.IsNullOrEmpty(x.offerId)) && s.shop.offers.Select(x => x.offerId).Distinct().Count() == s.shop.offers.Count, "存档商品身份无效。");
                foreach (var offer in s.shop.offers)
                {
                    bool known = offer.category == "controller" ? cfg.controllers.Any(x => x.id == offer.contentId)
                        : offer.category == "consumable" ? cfg.consumables.Any(x => x.id == offer.contentId)
                        : offer.category == "hand_upgrade" ? cfg.handDefinitions.Any(x => x.id == offer.contentId)
                        : offer.category == "die_upgrade" ? cfg.diceUpgrades.Any(x => x.id == offer.contentId)
                        : offer.category == "service" ? cfg.services.Any(x => x.id == offer.contentId && x.id != "O03")
                        : offer.category == "new_die" && offer.contentId == "O03" && offer.die != null;
                    Check(known && offer.basePrice >= 0 && offer.priceReasons != null, "存档包含未知商品。");
                    if (offer.category == "new_die") { cfg.ValidateFaces(offer.die.faces); Check(offer.basePrice == QuoteNewDie(offer.die), "存档新骰报价不匹配。"); }
                }
            }
            Check(s.processedReceipts.All(x => x != null && x.command != null && !string.IsNullOrEmpty(x.commandId) && x.commandId == x.command.commandId && x.actionSequence > 0 && x.actionSequence <= s.actionSequence && x.effects != null), "存档回执无效。");
            Check(s.processedReceipts.Select(x => x.commandId).Distinct().Count() == s.processedReceipts.Count, "存档回执重复。");
            for (int i = 1; i < s.processedReceipts.Count; i++) Check(s.processedReceipts[i].actionSequence > s.processedReceipts[i - 1].actionSequence, "存档回执序号无序。");
        }
    }
}
