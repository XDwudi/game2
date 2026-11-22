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
            var rows = new List<HandbookRow> { new HandbookRow { handId = "scatter", category = "hand", displayName = "散点", condition = "没有凑成任何牌型时，牌型倍率 ×1。", example = "蓝2、红5", stackingRule = "不与其他牌型相加；独立颜色、红组和卡牌仍可生效", baseMult100 = 100, currentMult100 = 100, minimumDice = 1, canForm = activeDiceCount > 0 && capacity > 0, formReason = "", upgradeEligible = false, upgradeReason = "不能升级" } };
            foreach (var h in cfg.handDefinitions.OrderBy(h => h.structuralPriority))
            {
                int level = upgrades.TryGetValue(h.id, out int saved) ? saved : 0, min = MinimumDice(h.id, owned, cfg, bindings), max = debug ? cfg.debug.maxHandLevel : h.maxLevel;
                bool form = capacity >= min && activeDiceCount >= min; string reason = capacity < min ? "提交栏至少要 " + min + " 格" : activeDiceCount < min ? "至少要有 " + min + " 颗骰子" : "骰子和提交栏都够，能不能凑出看掷骰";
                if (h.id == "straight" && sources.Any(s => s.abilityId == "C12")) { form = false; reason = "有“不要奇数”时只会出现 2、4、6，凑不成顺子"; }
                if (h.id == "four_pair" && sources.Any(s => s.abilityId == "C12")) { form = false; reason = "有“不要奇数”时只会出现 2、4、6，凑不成四对"; }
                bool eligible = level < max && capacity >= min && permanentDiceCount >= h.requirePermanentDice;
                rows.Add(new HandbookRow { handId = h.id, category = "hand", displayName = h.displayName, level = level, maxLevel = max, baseMult100 = h.baseMult100, incrementPerLevel100 = h.incrementPerLevel100, currentMult100 = HandEvaluator.Multiplier100(h, level, max), minimumDice = min, canForm = form, formReason = reason, upgradeEligible = eligible, upgradeReason = level >= max ? "已达最高等级" : capacity < min ? "提交栏不足" : permanentDiceCount < h.requirePermanentDice ? "永久骰不足；临时骰不计" : "可以升级", condition = Condition(h.id, min), example = Example(h.id, min), stackingRule = h.id == "straight" ? "与同点家族最高倍率相加；颜色为独立乘区" : "同点家族只取当前强化后最高倍率；结构主牌型按结构优先级" });
            }
            foreach (var c in cfg.colorDefinitions)
            {
                int level = colors.TryGetValue(c.id, out int saved) ? saved : 0, max = debug ? cfg.debug.maxColorLevel : c.maxLevel; bool form = activeDiceCount > 0 && capacity > 0;
                string reason = capacity < 1 ? "提交栏至少要 1 格" : activeDiceCount < 1 ? "至少要有 1 颗骰子" : ""; if (c.id == "all_blue" && sources.Any(s => s.abilityId == "C03")) { form = false; reason = "有“首当其冲”时第 1 颗总算红色，凑不成全蓝"; }
                rows.Add(new HandbookRow { handId = c.id, category = "color", displayName = c.displayName, level = level, maxLevel = max, baseMult100 = c.baseMult100, incrementPerLevel100 = c.incrementPerLevel100, currentMult100 = HandEvaluator.ColorMultiplier100(c, level, max), minimumDice = 1, canForm = form, formReason = reason, upgradeEligible = level < max, upgradeReason = level < max ? "可以升级" : "已达最高等级", condition = "非空提交的全部骰子均为有效" + (c.id == "all_blue" ? "蓝色" : "红色"), example = c.id == "all_blue" ? "蓝6" : "红1", stackingRule = "独立颜色乘区；不计入牌型或C43结构主牌型" });
            }
            return rows.ToArray();
        }
        private static string Condition(string id, int minimum)
        {
            switch (id) { case "two_pair": return "两种点数各 2 颗（4 颗相同不算两对）"; case "three_pair": return "三种点数各 2 颗"; case "four_pair": return "四种点数各 2 颗"; case "two_trips": return "两种点数各 3 颗（6 颗相同不算两个三条）"; case "full_house": return "一种点数 3 颗以上，另一种 2 颗以上"; case "straight": return minimum + " 个连续点数（6 和 1 不相连）"; default: return "同一点数至少 " + minimum + " 颗"; }
        }
        private static string Example(string id, int minimum)
        {
            switch (id) { case "two_pair": return "2、2、4、4"; case "three_pair": return "1、1、2、2、3、3"; case "four_pair": return "1、1、2、2、3、3、4、4"; case "two_trips": return "2、2、2、4、4、4"; case "full_house": return "2、2、2、5、5"; case "straight": return minimum == 4 ? "1、2、3、4" : "1、2、3、4、5"; default: return string.Join("、", Enumerable.Repeat("4", minimum)); }
        }
    }
}
