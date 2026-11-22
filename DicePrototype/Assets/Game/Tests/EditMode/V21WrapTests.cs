using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using DiceCraft.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DiceCraft.Tests
{
    public sealed class V21WrapTests
    {
        [Serializable] private sealed class Lexicon { public int version; public string[] words; }

        private LineBreak21 breaker;
        private string[] words;

        [OneTimeSetUp] public void LoadLexicon()
        {
            var data = JsonUtility.FromJson<Lexicon>(Resources.Load<TextAsset>("Data/LineBreakWords").text);
            Assert.That(data.version, Is.EqualTo(1));
            words = data.words;
            breaker = new LineBreak21(words);
        }

        [TestCase("+15%"), TestCase("×1.5"), TestCase("−1点"), TestCase("1~2颗"), TestCase("50%→65%"),
         TestCase("2-5-2"), TestCase("≤13"), TestCase("第2颗"), TestCase("+3金币"), TestCase("Lv.0"),
         TestCase("第二颗"), TestCase("三选一"), TestCase("第 3 颗")]
        public void NumericUnitsAreNotSplitAtTheHeightLimit(string unit)
        {
            var wrapped = breaker.Wrap("甲乙" + unit + "丙丁", unit.Length, 3, ch => 1);
            Assert.That(wrapped, Does.Contain(unit));
            Assert.That(breaker.Wrap("甲乙" + unit + "丙丁", unit.Length - 1, 4, ch => 1), Is.Null);
        }

        [Test] public void PunctuationAndSeparatorsAreKeptWithTheirClause()
        {
            Assert.That(breaker.Wrap("领取金币 · 提交骰子", 4, 2, ch => 1, true), Is.EqualTo("领取金币\n提交骰子"));
            Assert.That(breaker.Wrap("甲乙，丙丁（戊己）", 5, 3, ch => 1), Does.Not.Contain("（\n"));
            Assert.That(breaker.Wrap("甲乙，丙丁（戊己）", 5, 3, ch => 1), Does.Not.Contain("\n，"));
            Assert.That(breaker.Wrap("甲乙\n丙丁", 2, 2, ch => 1), Is.EqualTo("甲乙\n丙丁"));
            Assert.That(breaker.Wrap("甲乙\n丙丁", 2, 1, ch => 1), Is.Null);
            Assert.That(breaker.Wrap("甲乙 · 丙丁", 12, 2, ch => 1), Is.EqualTo("甲乙 · 丙丁"));
        }

        [Test] public void PreferWholeWordsAndBalancedFinalLines()
        {
            Assert.That(breaker.Wrap("甲乙丙丁戊己庚", 4, 2, ch => 1), Is.EqualTo("甲乙丙\n丁戊己庚"));
            Assert.That(breaker.Wrap("甲乙丙丁戊己庚。", 7, 2, ch => 1).Split('\n')[1].TrimEnd('。').Length, Is.GreaterThanOrEqualTo(3));
            Assert.That(breaker.Wrap("金币红骰提交栏", 4, 2, ch => 1), Is.EqualTo("金币红骰\n提交栏"));
            Assert.That(breaker.Wrap("甲乙红骰", 2, 2, ch => 1, true), Is.Null);
        }

        [Test] public void FW11_AllShortTextsFitAcrossThemesAndReportEveryBreak()
        {
            var config = JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(
                File.ReadAllText(Path.Combine(Application.dataPath, "Game/Data/P0Config.json"))));
            var entries = new Dictionary<string, string>();
            foreach (var c in config.controllers) entries.Add(c.id, c.shortText);
            foreach (var c in config.consumables) entries.Add(c.id, c.shortText);
            foreach (var c in config.services) entries.Add(c.id, c.shortText);
            foreach (var c in config.diceUpgrades) entries.Add(c.id, c.shortText);
            foreach (var c in config.bossModifiers) entries.Add(c.id, c.shortText);
            foreach (var c in config.lootKinds) entries.Add(c.id, c.shortText);
            Assert.That(entries.Count, Is.EqualTo(184 + 39 + 6));
            for (int i = 0; i < V21WrapMatrix.FaceLines.Length; i++) entries.Add("F" + i, V21WrapMatrix.FaceLines[i]);

            var atlas = AssetDatabase.LoadMainAssetAtPath("Assets/Game/Fonts/DiceChinese.asset");
            Assert.That(atlas, Is.Not.Null);
            var getAdvance = typeof(GameApp).GetMethod("GlyphAdvance11", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(getAdvance, Is.Not.Null);
            var advances = new Dictionary<char, float>();
            float Measure(char ch)
            {
                if (!advances.TryGetValue(ch, out float value))
                {
                    value = (float)getAdvance.Invoke(null, new object[] { atlas, (uint)ch, 1f, 0 });
                    if (value < 0) value = ch >= 0x2E80 ? 1f : ch == ' ' ? .3f : .62f;
                    advances.Add(ch, value);
                }
                return value;
            }

            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/cache/v11/w21/wrap"));
            Directory.CreateDirectory(output);
            var exceptions = new HashSet<string>();
            var problems = new List<string>();
            int layouts = 0, notShown = 0;
            using (var report = new StreamWriter(Path.Combine(output, "layouts.tsv")))
            {
                report.WriteLine("theme\tscenario\tid\twidth\tfont\tcap\tart\ttext\tlines");
                foreach (var frame in V21WrapMatrix.Themes)
                foreach (var entry in entries)
                foreach (var row in V21WrapMatrix.Cases(breaker, Measure, frame, entry.Key, entry.Value))
                {
                    string context = row.theme + "/" + row.scenario + "/" + row.id;
                    layouts++;
                    report.WriteLine(string.Join("\t", row.theme, row.scenario, row.id, Invariant(row.width),
                        Invariant(row.font), row.cap.ToString(), Invariant(row.art), row.text, row.lines?.Replace("\n", " / ") ?? ""));
                    if (row.lines == null)
                    {
                        if (row.scenario.StartsWith("goods", StringComparison.Ordinal)) { notShown++; continue; }
                        problems.Add(context + " has no layout"); continue;
                    }
                    if (row.lines.Replace("\n", "").Replace(" · ", "") != row.text.Replace(" · ", ""))
                        problems.Add(context + " changes text");
                    int pos = 0;
                    string[] lines = row.lines.Split('\n');
                    foreach (string line in lines)
                    {
                        if (line.Sum(ch => Measure(ch)) * row.font > row.width + .002f) problems.Add(context + " too wide: " + line);
                        if (line.Length == 0) { problems.Add(context + " empty line"); break; }
                        if ("，。；：、！？）”》】」’…".IndexOf(line[0]) >= 0 || "（“《【「‘+−×÷→~≤≥≈=每第这那各".IndexOf(line[line.Length - 1]) >= 0)
                            problems.Add(context + " punctuation: " + line);
                        pos += line.Length;
                        if (pos >= row.text.Length) continue;
                        if (string.CompareOrdinal(row.text, pos, " · ", 0, Math.Min(3, row.text.Length - pos)) == 0
                            && pos + 3 <= row.text.Length) { pos += 3; continue; }
                        foreach (var word in words)
                        {
                            for (int k = Math.Max(0, pos - word.Length + 1); k < pos; k++)
                                if (k + word.Length > pos && k + word.Length <= row.text.Length &&
                                    string.CompareOrdinal(row.text, k, word, 0, word.Length) == 0)
                                    exceptions.Add(context + " " + word);
                        }
                    }
                    if (lines.Length > 1 && lines[lines.Length - 1].Count(ch => !char.IsPunctuation(ch) && !char.IsWhiteSpace(ch)) < 2)
                        problems.Add(context + " orphan");
                }
            }
            foreach (string issue in exceptions)
            {
                string context = issue.Split(' ')[0];
                if (context != "default/table7/C116" && context != "sancai/table7/C116" &&
                    context != "default/table7/C124" && context != "sancai/table7/C124") problems.Add(issue);
            }
            TestContext.WriteLine("FW-11 layouts=" + layouts + ", optional goods texts too long for two lines=" + notShown +
                                  ", word breaks=" + string.Join(", ", exceptions) + ", output=" + output);
            Assert.That(layouts, Is.GreaterThan(7000));
            Assert.That(problems, Is.Empty, string.Join("\n", problems.Take(40)));
        }

        private static string Invariant(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
