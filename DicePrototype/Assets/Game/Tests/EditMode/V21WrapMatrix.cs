using System;
using System.Collections.Generic;

namespace DiceCraft.Tests
{
    // Design units from the six installed W22i card sprites (FONTS_WRAP §7.2), independent of the wrapping algorithm.
    public static class V21WrapMatrix
    {
        public sealed class Frame
        {
            public string theme;
            public float shopSides, shopTop, shopBottom, tableSides, tableTop, tableBottom, ring, bossLootWidth;
            public Frame(string theme, float shopSides, float shopTop, float shopBottom, float tableSides, float tableTop, float tableBottom, float ring, float bossLootWidth)
            {
                this.theme = theme; this.shopSides = shopSides; this.shopTop = shopTop; this.shopBottom = shopBottom;
                this.tableSides = tableSides; this.tableTop = tableTop; this.tableBottom = tableBottom; this.ring = ring; this.bossLootWidth = bossLootWidth;
            }
        }
        public static readonly Frame[] Themes = {
            new Frame("default", 29.5f, 13.5f, 17f, 24.5f, 11f, 14.5f, 6f, 344),
            new Frame("swiss", 10f, 5f, 5f, 10f, 5f, 5f, 9f, 342),
            new Frame("sancai", 21f, 10.5f, 12.5f, 25f, 12.5f, 14.5f, 5.5f, 342),
            new Frame("comic", 16f, 5.5f, 10.5f, 16f, 5.5f, 10.5f, 5f, 339.5f),
            new Frame("deco", 18f, 9f, 9f, 18f, 9f, 9f, 4.5f, 341.5f),
            new Frame("glass", 18f, 9f, 9f, 18f, 9f, 9f, 5f, 339)
        };

        // Templates with actual runtime substitutions from GameApp.ShopFacePick14. Includes each S1 prompt,
        // all S3 notes and reason variants, ordinary / special face changes, the chosen die and price.
        public static readonly string[] FaceLines = {
            "选一颗骰子，再选要改的面（在下面的“我的骰子”里）", "选一颗骰子，整颗变成玻璃骰（在下面的“我的骰子”里）",
            "选一颗要删除的骰子（在下面的“我的骰子”里）", "它的对面（5 点蓝面（光滑））更难掷出",
            "它的对面（2 点蓝面）更容易掷出", "计分 ×3；每次掷到它有 8% 概率碎掉，锁定后不会碎",
            "连同它的改造一起删除；提交栏仍是 5 格", "朝上提交时这颗计分 ×2", "颜色、纹理、黄金不变；买下时才抽",
            "更容易掷出这一面", "把这一面的点数和颜色复制到对面", "5 点红面 → 随机变成 1、2、3、4、6 之一",
            "对面 2 点蓝面 → 5 点红面", "5 点红面（黄金、权重 2） → 5 点红面（光滑、黄金、权重 2）",
            "第 3 颗 · 金面 2 · 红面多 → 玻璃骰", "这一面的权重已到上限 5，换一个面",
            "这一面已经是光滑的，换一个面", "这一面已经是粗糙的，换一个面", "这一面已经是红色，换一个面",
            "这一面已经是蓝色，换一个面", "这颗已经是玻璃骰，换一颗", "这一面已经镀金，换一个面",
            "这一面已经是 6 点，换一个面", "这一面已经是 1 点，换一个面", "对面已经和这一面相同，换一个面",
            "至少要留 1 颗骰子", "还差 10 金币", "先领取首领战利品", "第 3 颗 · 红面多 · 大点", "价格 12（你有 24）", "选要改的面：点下面的方块，或点骰子上的面",
            "删除 第 3 颗 · 金面 2 · 红面多", "免费", "第 1 颗 · 金面 2 · 红五面",
            "4 点蓝面 → 4 点红面", "4 点红面 → 4 点蓝面", "4 点红面 → 4 点红面（黄金）",
            "4 点红面 → 5 点红面", "4 点红面 → 3 点红面", "4 点红面 → 4 点红面（权重 2）",
            "4 点红面 → 4 点红面（粗糙）"
        };

        public struct Row
        {
            public string theme, scenario, id, text, lines;
            public float width, font, art;
            public int cap;
        }
        public static IEnumerable<Row> Cases(LineBreak21 breaker, Func<char, float> advanceEm, Frame frame, string id, string text, string lootName = null)
        {
            bool controller = id.StartsWith("C", StringComparison.Ordinal);
            bool info = id.StartsWith("B", StringComparison.Ordinal) || id.StartsWith("L", StringComparison.Ordinal);
            Row Result(string name, float width, float font, int cap, string value = null, bool balanced = false) => new Row {
                theme = frame.theme, scenario = name, id = id, text = value ?? text, width = width, font = font, cap = cap,
                lines = breaker.Wrap(value ?? text, width / font, cap, advanceEm, balanced)
            };
            Row Paragraph(string name, float width, float font, string value)
            {
                var row = Result(name,width,font,2,value,true);
                return row.lines != null ? row : Result(name,width,font,int.MaxValue,value);
            }

            foreach (float scale in new[] { 1f, 1.5f })
            {
                if (id.StartsWith("F", StringComparison.Ordinal) || id.StartsWith("U", StringComparison.Ordinal) || id == "O04")
                {
                    bool dieName = text.StartsWith("第 ",StringComparison.Ordinal) && !text.Contains(" → ");
                    bool besideDie = dieName || text.StartsWith("选要改的面：",StringComparison.Ordinal);
                    yield return Result((besideDie ? "face-beside-die" : "face") + Percent(scale),
                        besideDie ? 308 : 440,20 * scale,dieName ? 2 : int.MaxValue,balanced:dieName);
                }
                if (id.StartsWith("F", StringComparison.Ordinal)) continue;
                if (info)
                {
                    // Stage card's effect/reward columns at both text sizes (GameApp.Play11).
                    bool reward = id.StartsWith("L", StringComparison.Ordinal);
                    // StageCardInside11 reads the style's double ring. A lifted boss has no rings.
                    float width = reward ? frame.bossLootWidth : 392 - 36 - (scale == 1 ? 72 : 60) - 12;
                    string stage = reward ? "通关奖励：" + text : text;
                    float initial = reward ? Math.Max(19.5f, 18 * scale) : 20 * scale;
                    for (float font = initial; ; font = Math.Max(19.5f, font - 1))
                    {
                        yield return Paragraph("stage" + Percent(scale),width,font,stage);
                        if(reward && width != 344) yield return Paragraph("stage-lifted" + Percent(scale),344,font,stage);
                        if (font <= 19.5f) break;
                    }
                    string selected = reward ? "战利品 · " + lootName + "：" + text : text;
                    yield return Paragraph("stage-select" + Percent(scale),424,19.5f * scale,selected);
                    if(reward)
                    {
                        yield return Result("claim" + Percent(scale),424,24 * scale,2,text,true);
                        yield return Result("settlement" + Percent(scale),scale == 1 ? 400 : 480,20 * scale,2,text,true);
                    }
                    continue;
                }
                foreach (bool twoPrices in new[] { false, true })
                    yield return Tall(true, 220, scale, twoPrices);
                if (controller && scale == 1)
                {
                    yield return Tall(false, 216, 1, false);
                    yield return Tall(false, (1448f - 72) / 7, 1, false);
                }
                if (controller && scale > 1)
                {
                    float width = 280 - frame.tableSides - 8, block = Math.Min(84, width * .34f);
                    float room = 244 - frame.tableTop - frame.tableBottom - 8
                        - Math.Max(4 + Ceil(20 * scale * 1.46f) + 2, 6 + block + 11 + 4);
                    for (int tick = 600; tick >= 390; tick--)
                    {
                        var row = Result("text150", width, tick / 20f, int.MaxValue);
                        if (Height(row.lines, row.font) <= room || tick == 390) { yield return row; break; }
                    }
                }
                if (!controller)
                {
                    // Goods row may omit a short text that does not fit; verify those displayed at the two-line cap.
                    float rowWidth = (1404 - 2 * 12 - 2 * 12) / 3f;
                    float width = rowWidth - frame.tableSides - 8 - (scale == 1 ? 180 : 184) - 8 - 76 - 10;
                    yield return Result("goods" + Percent(scale), width, Math.Max(19.5f, 18 * scale), 2);
                }
            }

            Row Tall(bool shop, float outerWidth, float scale, bool twoPrices)
            {
                float width = outerWidth - (shop ? frame.shopSides : frame.tableSides) - 2 * frame.ring - 8;
                float faceTop = shop ? frame.shopTop : frame.tableTop;
                float cy = faceTop + frame.ring + 3;
                float y = controller ? Math.Max(cy, faceTop + frame.ring / 2 + (shop ? 17 : 15) + 2)
                    : cy + Ceil(Math.Max(19.5f,16 * scale) * 1.46f) + 2;
                float height = shop ? (scale > 1 ? 364 : 348) : 244;
                float bottom = height - (shop ? frame.shopBottom : frame.tableBottom) - frame.ring - 3;
                if (shop) bottom -= (twoPrices ? (scale == 1 ? 79 : 97) : 60) + 6;
                float name = 22 * scale;
                string scenario = shop ? (twoPrices ? "shop2-" : "shop1-") + Percent(scale)
                    : outerWidth == 216 ? "table216" : "table7";
                while (true)
                {
                    float available = bottom - (y + Ceil(name * 1.46f) + 4) - 4;
                    for (float font = 20 * scale; ; font = Math.Max(19.5f, font - 1))
                    {
                        var row = Result(scenario, width, font, int.MaxValue);
                        row.art = Math.Min(width - 8, available - Ceil(Height(row.lines, font)) - 2);
                        if (row.art >= (shop ? 72 : 56)) return row;
                        if (font > 19.5f) continue;
                        if (row.art >= 40) return row;
                        if (name <= 19.51f)
                        {
                            int cap = 1;
                            while (Ceil(Height(cap + 1, font)) + 2 <= available - 40) cap++;
                            row = Result(scenario, width, font, cap);
                            row.art = available - Ceil(Height(row.lines, font)) - 2;
                            return row;
                        }
                        break;
                    }
                    name = Math.Max(19.5f, name - 1.5f);
                }
            }
        }

        private static int Percent(float scale) => (int)(scale * 100);
        private static float Ceil(float value) => (float)Math.Ceiling(value);
        private static float Height(string lines, float font)
            => lines == null ? 1e6f : Height(lines.Split('\n').Length, font);
        private static float Height(int count, float font) => font * (1.448f + (count - 1) * 1.308f);
    }
}
