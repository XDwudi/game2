using System;
using System.Collections.Generic;
using System.Linq;

namespace DiceCraft.Core
{
    public sealed class HandbookRow
    {
        public string handId, displayName, condition, example, stackingRule, formReason, upgradeReason;
        public int level, maxLevel, baseMult100, incrementPerLevel100, currentMult100, minimumDice;
        public bool canForm, upgradeEligible;
    }

    /// <summary>Read-only rule-derived presentation values; never changes a run or performs RNG.</summary>
    public static class HandbookRules
    {
        public static int MinimumDice(string id, IEnumerable<ControllerInstance> controllers, GameConfig cfg)
            => id == "straight" && ControllerRules.Has(controllers, "C29") ? cfg.Controller("C29").parameters.minimumStraightLength : cfg.Hand(id).minSubmittedDice;

        public static HandbookRow[] Build(GameConfig cfg, IEnumerable<HandLevel> levels,
            IEnumerable<ControllerInstance> controllers, int capacity, int permanentDiceCount, int activeDiceCount)
        {
            var owned = controllers.ToArray();
            var upgrades = levels.ToDictionary(h => h.handId, h => h.level, StringComparer.Ordinal);
            var rows = new List<HandbookRow>
            {
                new HandbookRow { handId = "scatter", displayName = "散点", condition = "没有同点、顺子或颜色牌型时保底", example = "2、5", stackingRule = "仅保底，不与其他牌型相加", baseMult100 = 100, currentMult100 = 100, minimumDice = 1, canForm = activeDiceCount > 0 && capacity > 0, formReason = activeDiceCount > 0 && capacity > 0 ? "以实际提交结果为准" : "没有可提交骰子", upgradeEligible = false, upgradeReason = "散点不可强化" }
            };
            foreach (var hand in cfg.handDefinitions)
            {
                int level = upgrades.TryGetValue(hand.id, out int saved) ? saved : 0;
                int minimum = MinimumDice(hand.id, owned, cfg);
                bool form = capacity >= minimum && activeDiceCount >= minimum;
                string formReason = capacity < minimum ? "需要至少 " + minimum + " 个提交栏（当前 " + capacity + "）" : activeDiceCount < minimum ? "需要至少 " + minimum + " 颗活动骰（当前 " + activeDiceCount + "）" : "数量与容量满足；以实际提交结果为准";
                if (hand.id == "straight" && ControllerRules.Has(owned, "C12")) { form = false; formReason = "不要奇数只产生有效2、4、6，不能形成连续顺子"; }
                if (hand.id == "all_blue" && ControllerRules.Has(owned, "C03")) { form = false; formReason = "首当其冲令首骰有效红，当前构筑不能形成全蓝"; }
                bool eligible = level < hand.maxLevel && capacity >= minimum && permanentDiceCount >= hand.requirePermanentDice;
                string upgradeReason = level >= hand.maxLevel ? "已达最高等级" : capacity < minimum ? "当前提交栏不足以购买该强化" : permanentDiceCount < hand.requirePermanentDice ? "购买强化需要至少 " + hand.requirePermanentDice + " 颗永久骰；临时骰不计" : "符合强化资格；仍需商店供货与金币";
                rows.Add(new HandbookRow { handId = hand.id, displayName = hand.displayName, level = level, maxLevel = hand.maxLevel, baseMult100 = hand.baseMult100, incrementPerLevel100 = hand.incrementPerLevel100, currentMult100 = HandEvaluator.Multiplier100(hand, level), minimumDice = minimum, canForm = form, formReason = formReason, upgradeEligible = eligible, upgradeReason = upgradeReason, condition = Condition(hand.id, minimum, owned), example = Example(hand.id, minimum), stackingRule = hand.tieBreakRank > 0 ? "同点家族只取当前强化后最高倍率" : hand.id == "straight" ? "与同点家族、颜色牌型相加" : "全红与全蓝互斥；与同点、顺子相加" });
            }
            return rows.ToArray();
        }

        private static string Condition(string id, int minimum, ControllerInstance[] owned)
        {
            switch (id)
            {
                case "two_pair": return "至少两个不同数字各有两颗；四同点不能拆成两对";
                case "full_house": return "一个数字至少三颗，另一数字至少两颗";
                case "straight": return "至少 " + minimum + " 个不同且连续的点数" + (ControllerRules.Has(owned, "C29") ? "（简单顺子）" : "") + "；不首尾相接";
                case "all_blue": return "至少五颗，全部为有效蓝色";
                case "all_red": return "至少五颗，全部为有效红色";
                default: return "某个有效数字至少出现 " + minimum + " 次";
            }
        }
        private static string Example(string id, int minimum)
        {
            switch (id)
            {
                case "two_pair": return "2、2、4、4";
                case "full_house": return "2、2、2、5、5";
                case "straight": return minimum == 4 ? "1、2、3、4" : "1、2、3、4、5";
                case "all_blue": return "蓝1、蓝2、蓝3、蓝4、蓝5";
                case "all_red": return "红1、红2、红3、红4、红5";
                default: return string.Join("、", Enumerable.Repeat("4", minimum));
            }
        }
    }
}
