using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using DiceCraft.Core;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine.UI;
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

        // V1.1.1 W9b: C155's short text broke as "……×2； / 再 / 含两颗1点改为×4。" on four styles' shelf cards. No line may
        // be a lone character, a short middle line counts like a short last line, and "两颗1点" stays together when it can.
        [Test] public void W9B_NoLoneCharacterOnAMiddleLine()
        {
            const string c155 = "含四颗4点得分×2；再含两颗1点改为×4。";
            Assert.That(breaker.Wrap(c155, 10, int.MaxValue, ch => 1), Is.EqualTo("含四颗4点得分×2；\n再含两颗1点\n改为×4。"));
            for (int width = 6; width <= 20; width++)
            {
                string wrapped = breaker.Wrap(c155, width, int.MaxValue, ch => 1);
                Assert.That(wrapped, Is.Not.Null, "width " + width);
                foreach (string line in wrapped.Split('\n'))
                    Assert.That(line.Count(ch => !char.IsPunctuation(ch) && !char.IsWhiteSpace(ch)), Is.GreaterThanOrEqualTo(2),
                        "width " + width + ": " + wrapped.Replace("\n", " / "));
            }
        }

        // Import geometry from the installed sprites. EditMode can run without a GPU, so read the
        // source PNG rather than blitting the non-readable player texture. Ring positions, heading
        // creation, TMP ink measurement and the final reward bounds all call the production methods.
        private static Dictionary<(string theme, float scale, bool lifted), float[]> BossRewardWidths21()
        {
            var widths = new Dictionary<(string,float,bool),float[]>();
            const BindingFlags statics = BindingFlags.Static | BindingFlags.NonPublic;
            var type = typeof(GameApp);
            object Call(string name, params object[] args) => type.GetMethod(name,statics).Invoke(null,args);
            var area = (Rect)type.GetField("StageCardArea11",statics).GetValue(null);
            var titleFont = typeof(UiKit).GetProperty("TitleFont");
            var titleRoles = typeof(UiKit).GetProperty("TitleRoles");
            var oldFont = UiKit.Font; var oldTitle = UiKit.TitleFont; var oldRoles = UiKit.TitleRoles;
            var oldLookup = UiKit.ThemeTemplate; float oldScale = UiKit.FontScale;
            var host = new GameObject("FW11_LayoutCanvas",typeof(RectTransform),typeof(Canvas));
            try
            {
                host.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
                UiKit.Font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Game/Fonts/DiceChinese.asset");
                foreach (var frame in V21WrapMatrix.Themes)
                {
                    string root="Assets/Resources/Themes/"+frame.theme+"/";
                    var installed=frame.theme=="default"?null:AssetDatabase.LoadAssetAtPath<TextAsset>(root+"theme.json");
                    if(frame.theme!="default")Assert.That(installed,Is.Not.Null,root);
                    var metadata=installed==null?null:JsonUtility.FromJson<FontFile21>(installed.text).fonts;
                    bool styled=metadata?.roles?.Contains("sectionTitle")==true;
                    var face=styled?AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(root+metadata.title+".asset"):null;
                    if(styled)Assert.That(face,Is.Not.Null,root+metadata.title);
                    titleFont.SetValue(null,face);titleRoles.SetValue(null,styled?TextRole.SectionTitle:TextRole.None);
                    string framePath=frame.theme=="default"?"Assets/Art/Art11/Surfaces/ui_stage_card.png":root+"UI/ui_stage_card.png";
                    string ringPath=frame.theme=="default"?"Assets/Art/Art073/Surfaces/ui_ring.png":root+"UI/ui_ring.png";
                    var paper=AssetDatabase.LoadAssetAtPath<Sprite>(framePath);
                    var ring=AssetDatabase.LoadAssetAtPath<Sprite>(ringPath);
                    Assert.That(paper,Is.Not.Null,framePath);Assert.That(ring,Is.Not.Null,ringPath);
                    var paperPixels=ImportedInsets21(framePath,paper,false);var ringPixels=ImportedInsets21(ringPath,ring,true);
                    UiKit.ThemeTemplate=name=>name=="ui_ring"?ring:null;
                    var card=UiKit.Box(host.transform,"StageCard11",0,0,area.width,area.height,Color.white);
                    var surface=card.GetComponent<Image>();surface.sprite=paper;surface.type=Image.Type.Sliced;
                    Call("DrawBossRings11",card,area.width,area.height);
                    float inner=(float)Call("StageInnerWidth11",area.width);
                    foreach(float scale in new[]{1f,1.5f})foreach(bool lifted in new[]{false,true})
                    {
                        UiKit.FontScale=scale;bool large=scale>1;
                        float h=(float)Call("StageCardHeaderHeight11",large);
                        var header=UiKit.Rect(card,"StageCardHeader11",14,14,inner,h);
                        Func<Image,Vector4> edge=i=>paperPixels/(i.pixelsPerUnit*i.pixelsPerUnitMultiplier);
                        Func<Image,Vector4> band=i=>ringPixels/(i.pixelsPerUnit*i.pixelsPerUnitMultiplier);
                        var inside=(Rect)Call("StageCardInside11",card,!lifted,area.width,area.height,edge,band);
                        var group=new HashSet<float>();
                        foreach(string tier in new[]{"一档","二档","三档","终局"})
                        {
                            var section=(TextMeshProUGUI)Call("StageCardHeading11",header,(lifted?"减益已解除 · ":"首领减益 · ")+tier,inner,h,large,Color.black);
                            var row=(Vector2)Call("BossRewardRow11",section,inside,inner);
                            Assert.That(row.y,Is.GreaterThan(0));group.Add(row.y);
                        }
                        widths.Add((frame.theme,scale,lifted),group.OrderBy(v=>v).ToArray());
                    }
                }
                return widths;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UiKit.Font=oldFont;UiKit.ThemeTemplate=oldLookup;UiKit.FontScale=oldScale;
                titleFont.SetValue(null,oldTitle);titleRoles.SetValue(null,oldRoles);
            }
        }

        [Serializable] private sealed class FontFile21 { public FontRoles21 fonts; }
        [Serializable] private sealed class FontRoles21 { public string title; public string[] roles; }

        private static Vector4 ImportedInsets21(string path,Sprite sprite,bool ring)
        {
            var raster=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                Assert.That(raster.LoadImage(File.ReadAllBytes(path)),Is.True,path);
                int w=raster.width,h=raster.height;var px=raster.GetPixels32();var center=px[(h/2)*w+w/2];
                int Scan(int n,float border,Func<int,Color32> sample)
                {
                    int edge=0;
                    for(int i=0;i<Math.Min(n/2,Mathf.RoundToInt(border));i++)
                    {
                        var c=sample(i);
                        if(ring?c.a>=128:c.a<128||Math.Abs(c.r-center.r)+Math.Abs(c.g-center.g)+Math.Abs(c.b-center.b)>48)edge=i+1;
                    }
                    return edge;
                }
                var b=sprite.border;
                return new Vector4(Scan(w,b.x,i=>px[(h/2)*w+i]),Scan(h,b.y,i=>px[i*w+w/2]),
                    Scan(w,b.z,i=>px[(h/2)*w+w-1-i]),Scan(h,b.w,i=>px[(h-1-i)*w+w/2]));
            }
            finally { UnityEngine.Object.DestroyImmediate(raster); }
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
            Assert.That(config.bossModifiers.Length,Is.EqualTo(39));
            Assert.That(config.lootKinds.Length,Is.EqualTo(6));
            foreach (var c in config.bossModifiers) entries.Add(c.id, c.shortText);
            foreach (var c in config.lootKinds) entries.Add(c.id, c.shortText);
            // p1.1.1 (W7a; RULES_DELTA 1): 185 content items (150 cards, 13 items, 11 services, 11 face upgrades), was 184.
            Assert.That(entries.Count, Is.EqualTo(config.activeContentCount + 39 + 6)); Assert.That(config.activeContentCount, Is.EqualTo(185));
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
            var measured = new HashSet<string>();
            var widths = BossRewardWidths21();
            var problems = new List<string>();
            int layouts = 0, notShown = 0;
            using (var report = new StreamWriter(Path.Combine(output, "layouts.tsv")))
            {
                report.WriteLine("theme\tscenario\tid\twidth\tfont\tcap\tart\ttext\tlines");
                foreach (var frame in V21WrapMatrix.Themes)
                foreach (var entry in entries)
                foreach (var row in V21WrapMatrix.Cases(breaker, Measure, frame, entry.Key, entry.Value,
                    (scale,lifted)=>widths[(frame.theme,scale,lifted)], entry.Key.StartsWith("L",StringComparison.Ordinal) ? config.Loot(entry.Key).displayName : null))
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
                    measured.Add(context);
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
                    // V1.1.1 W9b: no line is a lone character, the middle ones included (C155 "再" on a line of its own).
                    if (lines.Length > 1)
                        for (int k = 0; k < lines.Length; k++)
                            if (lines[k].Count(ch => !char.IsPunctuation(ch) && !char.IsWhiteSpace(ch)) < 2)
                                problems.Add(context + " orphan on line " + (k + 1) + ": " + row.lines.Replace("\n", " / "));
                }
            }
            // The C116 cut falls in both nested words. Only the specified cut contexts are permitted.
            var allowed = new[]{"default","sancai"}.SelectMany(theme=>new[]{theme+"/table7/C116 永久",
                theme+"/table7/C116 永久骰",theme+"/table7/C124 更容易"});
            problems.AddRange(exceptions.Except(allowed));
            foreach(var frame in V21WrapMatrix.Themes)
            foreach(int percent in new[]{100,150})
            {
                void Require(string variant,string id)
                {
                    string key=frame.theme+"/"+variant+percent+"/"+id;
                    if(!measured.Contains(key))problems.Add("Missing measured layout "+key);
                }
                foreach(var boss in config.bossModifiers){Require("stage",boss.id);Require("stage-select",boss.id);}
                foreach(var loot in config.lootKinds)
                {
                    foreach(string variant in new[]{"stage","stage-select","claim","settlement"})Require(variant,loot.id);
                    Require("stage-lifted",loot.id);
                }
                foreach(var upgrade in config.diceUpgrades)Require("face",upgrade.id);
                Require("face","O04");
                for(int i=0;i<V21WrapMatrix.FaceLines.Length;i++)
                {
                    string key=frame.theme+"/face",suffix=percent+"/F"+i;
                    if(!measured.Contains(key+suffix)&&!measured.Contains(key+"-beside-die"+suffix))
                        problems.Add("Missing measured face sample "+frame.theme+" "+percent+" F"+i);
                }
            }
            TestContext.WriteLine("FW-11 layouts=" + layouts + ", optional goods texts too long for two lines=" + notShown +
                                  ", word breaks=" + string.Join(", ", exceptions) + ", output=" + output);
            Assert.That(layouts, Is.GreaterThan(7000));
            Assert.That(problems, Is.Empty, string.Join("\n", problems.Take(40)));
        }

        private static string Invariant(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
