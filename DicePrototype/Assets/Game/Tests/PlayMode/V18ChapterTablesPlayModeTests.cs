using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [UnityTest]
        public IEnumerator PLAY_V18_DefaultAndSwissChapterTableSamples()
        {
            Assert.That((Screen.width == 1920 && Screen.height == 1080) ||
                        (Screen.width == 1280 && Screen.height == 720), Is.True);
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "cache",
                "v11", "w18", "golden", Screen.width + "x" + Screen.height));
            Directory.CreateDirectory(output);
            var report = new List<string> { "Unity " + Application.unityVersion + " | " + Screen.width + "x" + Screen.height +
                " | source " + IdentityHead20X() };
            var errors = new List<string>();
            bool clean = GameApp.CleanCapture11;
            try
            {
                PlayerPrefs.SetInt("reduced", 1);
                GameApp.CleanCapture11 = true;
                yield return LoadBootstrap();
                StartRun("20261122");
                foreach (var theme in new[]{"default", "swiss"})
                {
                    app.ApplyTheme11(theme);
                    var source = theme == "default"
                        ? Resources.Load<Texture2D>("ChapterTables18/default_play") : ThemeRegistry.Current.TableScene;
                    Assert.That(source, Is.Not.Null);
                    Color32? firstRail = null;
                    var chapterReadings = new List<float>();
                    for (int chapter = 1; chapter <= 8; chapter++)
                    {
                        bool representative = chapter == 1 || chapter == 4 || chapter == 8;
                        int tiers = representative ? 3 : 1;
                        for (int tier = 0; tier < tiers; tier++)
                        {
                            yield return Stage18(chapter, tier, tier == 1);
                            var st = app.Session.State.stage;
                            string kind = app.Config.stages[st.stageIndex].stageKind;
                            Assert.That(st.stageIndex, Is.EqualTo((chapter-1)*3 + tier));
                            Assert.That(kind, Is.EqualTo(new[]{"normal","pressure","boss"}[tier]));
                            Assert.That(st.risk, Is.EqualTo(tier == 1));
                            Assert.That(app.TableMood11, Is.EqualTo(tier == 2 ? 1f : 0f));
                            Assert.That(FeltPicture20X(), Is.SameAs(source));
                            string prefix = theme + "-ch" + chapter + "-" + kind;
                            if (representative)
                            {
                                string state = StateJson();
                                foreach (int size in new[]{100,150})
                                {
                                    W11TextScale(size);
                                    yield return W11Settle();
                                    Assert.That(LabelByName18("Chapter").GetParsedText(), Does.Contain("第 " + chapter + " 章"));
                                    Assert.That(LabelByName18("StageLabel11").GetParsedText(), Is.EqualTo("第 " + (st.stageIndex+1) + " 关"));
                                    yield return new WaitForEndOfFrame();
                                    var shot = ScreenCapture.CaptureScreenshotAsTexture();
                                    try { File.WriteAllBytes(Path.Combine(output, prefix + "-" + size + ".png"), shot.EncodeToPNG()); }
                                    finally { UnityEngine.Object.Destroy(shot); }
                                }
                                Assert.That(StateJson(), Is.EqualTo(state), "capture/redraw modified the run");
                            }
                            // A genuine rendered albedo sample with the dice and canvas temporarily hidden.
                            Color32[] felt = null;
                            yield return FeltOnly20Xb(px => felt = px);
                            var mid = felt[(Screen.height/2)*Screen.width + Screen.width/2];
                            // The Swiss paper extends to the left edge. Use its solid red graphic at
                            // upper right, and the default table's wooden frame at the left edge.
                            var rail = theme == "default" ? felt[(Screen.height/2)*Screen.width + 2]
                                : felt[(Screen.height*9/10)*Screen.width + Screen.width*99/100];
                            float lightness = (float)(116 * Math.Pow(GameApp.Luminance11(mid), 1.0/3.0) - 16);
                            report.Add(prefix + " stage=" + (st.stageIndex+1) + " target=" + st.target + " modifier=" +
                                st.modifierId + " center=#" + ColorUtility.ToHtmlStringRGB(mid) + " L*=" + lightness.ToString("F2") +
                                " rail=#" + ColorUtility.ToHtmlStringRGB(rail) + " bossMood=" + app.TableMood11);
                            if (tier == 0)
                            {
                                if (chapterReadings.Count > 0 && lightness >= chapterReadings.Last())
                                    errors.Add(prefix + " chapter lightness did not decrease");
                                chapterReadings.Add(lightness);
                                if (firstRail.HasValue && Delta20Xb(rail, firstRail.Value) > 1)
                                    errors.Add(prefix + " retained frame changed");
                                firstRail ??= rail;
                            }
                            else if (tier == 1 && Math.Abs(lightness - chapterReadings.Last()) > 1)
                                errors.Add(prefix + " pressure stage changed the whole felt");
                            else if (tier == 2 && lightness >= chapterReadings.Last())
                                errors.Add(prefix + " boss stage did not darken the visible table");
                        }
                    }
                    float actual = chapterReadings[7] - chapterReadings[0];
                    float expected = theme == "default" ? -7 : -9;
                    if (Math.Abs(actual - expected) > 1.5f)
                        errors.Add(theme + " first-to-last chapter L* change " + actual + ", expected " + expected);
                    yield return Stage18(8, 1, false);
                    Assert.That(app.TableMood11, Is.Zero);
                    Assert.That(app.Session.State.stage.risk, Is.False);
                    yield return W14Load(W14ShopFixture(false));
                    app.ApplyTheme11(theme);
                    yield return W11Settle();
                    var renderer = GameObject.Find("Art07Felt").GetComponent<Renderer>();
                    var props = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(props);
                    Assert.That(props.GetVector("_ChapterShift18"), Is.EqualTo(Vector4.zero), "The shop has no chapter tint.");
                    Texture ordinaryPage = theme == "default" ? renderer.sharedMaterial.GetTexture("_TableMap") : ThemeRegistry.Current.TableScene;
                    Assert.That(FeltPicture20X(), Is.SameAs(ordinaryPage), "The default/Swiss shop keeps its approved background.");
                }
            }
            finally
            {
                report.AddRange(errors.Select(e => "FAIL " + e));
                File.WriteAllLines(Path.Combine(output, "chapter-tables.txt"), report);
                GameApp.CleanCapture11 = clean;
                if (app != null) app.ApplyTheme11(GameTheme.DefaultId, false);
            }
            Assert.That(errors, Is.Empty, string.Join("\n", errors));
        }

        private TextMeshProUGUI LabelByName18(string name) =>
            app.canvas.GetComponentsInChildren<TextMeshProUGUI>().Single(t => t.name == name);

        private IEnumerator Stage18(int chapter, int tier, bool risk)
        {
            int index = (chapter-1)*3+tier;
            var def = app.Config.stages[index];
            var scene = W11Scene(W11Cards3, new[]{4,4,6,2,4}, "bbbrb", 2, true,
                stageIndex:index, risk:risk, target:risk ? def.riskTarget : def.normalTarget, score:"0");
            if (tier == 2) scene.modifierId = app.Session.State.cycles[chapter-1].modifierId;
            W11Apply(scene);
            if (tier == 2)
            {
                string target = app.Session.CycleView()[tier].bossTarget;
                if (target != scene.target) { scene.target = target; W11Apply(scene); }
            }
            yield return W11Settle();
        }
    }
}
