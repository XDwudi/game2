using System;
using System.Collections.Generic;
using System.Linq;

namespace DiceCraft.Core
{
    public sealed class HandbookRow
    {
        public string handId, category, displayName, condition, example, stackingRule, formReason, upgradeReason;
        public int level, maxLevel, baseMult100, incrementPerLevel100, currentMult100, minimumDice;
        public bool canForm, upgradeEligible;
    }
    public static class HandbookRules
    {
        public static int MinimumDice(string id, IEnumerable<ControllerInstance> controllers, GameConfig cfg, MimicBinding[] bindings = null)
        {
            if (id == "all_blue" || id == "all_red") return 1;
            return id == "straight" && ControllerRules.ResolveSources(controllers, bindings).Any(s => s.abilityId == "C29") ? cfg.Controller("C29").parameters.minimumStraightLength : cfg.Hand(id).minSubmittedDice;
        }
        public static HandbookRow[] Build(GameConfig cfg, IEnumerable<HandLevel> levels, IEnumerable<ControllerInstance> controllers, int capacity, int permanentDiceCount, int activeDiceCount)
            => Build(cfg, levels, Array.Empty<HandLevel>(), controllers, capacity, permanentDiceCount, activeDiceCount);
        public static HandbookRow[] Build(GameConfig cfg, IEnumerable<HandLevel> levels, IEnumerable<HandLevel> colorLevels, IEnumerable<ControllerInstance> controllers, int capacity, int permanentDiceCount, int activeDiceCount, MimicBinding[] bindings = null, bool debug = false)
        {
            var owned = controllers.ToArray(); var sources = ControllerRules.ResolveSources(owned, bindings); var upgrades = levels.ToDictionary(h => h.handId, h => h.level, StringComparer.Ordinal); var colors = colorLevels.ToDictionary(h => h.handId, h => h.level, StringComparer.Ordinal);
            var rows = new List<HandbookRow> { new HandbookRow { handId = "scatter", category = "hand", displayName = "散点", condition = "没有成立的同点或顺子时牌型乘区保底×1", example = "蓝2、红5", stackingRule = "不与其他牌型相加；独立颜色、红组和控制器仍可生效", baseMult100 = 100, currentMult100 = 100, minimumDice = 1, canForm = activeDiceCount > 0 && capacity > 0, formReason = "以实际提交结果为准", upgradeEligible = false, upgradeReason = "散点不可强化" } };
            foreach (var h in cfg.handDefinitions.OrderBy(h => h.structuralPriority))
            {
                int level = upgrades.TryGetValue(h.id, out int saved) ? saved : 0, min = MinimumDice(h.id, owned, cfg, bindings), max = debug ? cfg.debug.maxHandLevel : h.maxLevel;
                bool form = capacity >= min && activeDiceCount >= min; string reason = capacity < min ? "需要至少 " + min + " 个提交栏" : activeDiceCount < min ? "需要至少 " + min + " 颗活动骰" : "数量与容量满足；以实际提交结果为准";
                if (h.id == "straight" && sources.Any(s => s.abilityId == "C12")) { form = false; reason = "不要奇数只产生有效2、4、6，无法形成连续顺子"; }
                if (h.id == "four_pair" && sources.Any(s => s.abilityId == "C12")) { form = false; reason = "不要奇数只产生三种有效点数，无法形成四个不同对子"; }
                bool eligible = level < max && capacity >= min && permanentDiceCount >= h.requirePermanentDice;
                rows.Add(new HandbookRow { handId = h.id, category = "hand", displayName = h.displayName, level = level, maxLevel = max, baseMult100 = h.baseMult100, incrementPerLevel100 = h.incrementPerLevel100, currentMult100 = HandEvaluator.Multiplier100(h, level, max), minimumDice = min, canForm = form, formReason = reason, upgradeEligible = eligible, upgradeReason = level >= max ? "已达最高等级" : capacity < min ? "提交栏不足" : permanentDiceCount < h.requirePermanentDice ? "永久骰不足；临时骰不计" : "符合强化资格", condition = Condition(h.id, min), example = Example(h.id, min), stackingRule = h.id == "straight" ? "与同点家族最高倍率相加；颜色为独立乘区" : "同点家族只取当前强化后最高倍率；结构主牌型按结构优先级" });
            }
            foreach (var c in cfg.colorDefinitions)
            {
                int level = colors.TryGetValue(c.id, out int saved) ? saved : 0, max = debug ? cfg.debug.maxColorLevel : c.maxLevel; bool form = activeDiceCount > 0 && capacity > 0;
                string reason = "非空提交即可；以全部有效颜色判定"; if (c.id == "all_blue" && sources.Any(s => s.abilityId == "C03")) { form = false; reason = "首当其冲令首骰有效红，无法形成全蓝"; }
                rows.Add(new HandbookRow { handId = c.id, category = "color", displayName = c.displayName, level = level, maxLevel = max, baseMult100 = c.baseMult100, incrementPerLevel100 = c.incrementPerLevel100, currentMult100 = HandEvaluator.ColorMultiplier100(c, level, max), minimumDice = 1, canForm = form, formReason = reason, upgradeEligible = level < max, upgradeReason = level < max ? "符合颜色强化资格" : "已达最高等级", condition = "非空提交的全部骰子均为有效" + (c.id == "all_blue" ? "蓝色" : "红色"), example = c.id == "all_blue" ? "蓝6" : "红1", stackingRule = "独立颜色乘区；不计入牌型或C43结构主牌型" });
            }
            return rows.ToArray();
        }
        private static string Condition(string id, int minimum)
        {
            switch (id) { case "two_pair": return "至少两个不同点数各两颗；四同不能拆对"; case "three_pair": return "至少三个不同点数各两颗"; case "four_pair": return "至少四个不同点数各两颗"; case "two_trips": return "至少两个不同点数各三颗；六同不能拆三条"; case "full_house": return "一个点数至少三颗，另一点数至少两颗"; case "straight": return "至少 " + minimum + " 个不同且连续的点数，不首尾相接"; default: return "某个有效点数至少出现 " + minimum + " 次"; }
        }
        private static string Example(string id, int minimum)
        {
            switch (id) { case "two_pair": return "2、2、4、4"; case "three_pair": return "1、1、2、2、3、3"; case "four_pair": return "1、1、2、2、3、3、4、4"; case "two_trips": return "2、2、2、4、4、4"; case "full_house": return "2、2、2、5、5"; case "straight": return minimum == 4 ? "1、2、3、4" : "1、2、3、4、5"; default: return string.Join("、", Enumerable.Repeat("4", minimum)); }
        }
    }
}
