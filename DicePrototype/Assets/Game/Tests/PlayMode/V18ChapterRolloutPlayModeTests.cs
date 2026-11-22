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
        private static readonly string[] RolloutStyles18 = { "default", "swiss", "sancai", "comic", "deco", "glass" };
        private string RolloutOutput18
        {
            get
            {
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "cache",
                    "v11", "w18", "rollout-runs", Screen.width + "x" + Screen.height));
                Directory.CreateDirectory(dir); return dir;
            }
        }

        private static Vector4 ChapterUniform18()
        {
            var block = new MaterialPropertyBlock();
            GameObject.Find("Art07Felt").GetComponent<Renderer>().GetPropertyBlock(block);
            return block.GetVector("_ChapterShift18");
        }

        private static float Lightness18(Color32 color)
        {
            float y = GameApp.Luminance11(color);
            return y > .00885645f ? 116 * Mathf.Pow(y, 1f / 3f) - 16 : 903.2963f * y;
        }

        private static Color32 Point18(Color32[] pixels, int x, int y)
        {
            int px = Mathf.FloorToInt(x * Screen.width / 1920f), py = Screen.height - 1 - Mathf.FloorToInt(y * Screen.height / 1080f);
            return pixels[py * Screen.width + px];
        }

        private static IEnumerator RolloutShot18(string output)
        {
            yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(output + ".png", image.EncodeToPNG()); }
            finally { UnityEngine.Object.Destroy(image); }
        }

        [UnityTest]
        public IEnumerator PLAY_V18_CT01_CT02_CT03_ChapterRollout()
        {
            Assert.That(Screen.width == 1920 && Screen.height == 1080 || Screen.width == 1280 && Screen.height == 720);
            var report = new List<string> { IdentityHead20X() + " | Unity " + Application.unityVersion };
            var failures = new List<string>();
            var clean = GameApp.CleanCapture11;
            var anchors = new[] { new Vector2Int(2, 540), new Vector2Int(1900, 105), new Vector2Int(8, 540),
                new Vector2Int(60, 60), new Vector2Int(7, 540), new Vector2Int(945, 150) };
            try
            {
                PlayerPrefs.SetInt("reduced", 1); GameApp.CleanCapture11 = true;
                yield return LoadBootstrap(); StartRun("20261122");
                for (int style = 0; style < RolloutStyles18.Length; style++)
                {
                    string theme = RolloutStyles18[style];
                    app.ApplyTheme11(theme);
                    Assert.That(app.ThemeId11, Is.EqualTo(theme));
                    float expectedDelta = theme == "swiss" ? -9 : theme == "default" || theme == "comic" ? -7 : 7;
                    var chapterL = new List<float>(); Color32? locked = null;
                    for (int chapter = 1; chapter <= 8; chapter++)
                    {
                        bool representative = chapter == 1 || chapter == 4 || chapter == 8;
                        for (int tier = 0; tier < 3; tier++)
                        {
                            int index = (chapter - 1) * 3 + tier; var def = app.Config.stages[index];
                            // Exercise empty hand, full single row, and compact double-row cards as well as both hand phases.
                            string[] cards = chapter == 1 ? Array.Empty<string>() : chapter == 4 ? W11Cards7 :
                                chapter == 8 ? W11Cards7.Concat(new[]{"C01","C02","C03"}).ToArray() : W11Cards3;
                            var scene = W11Scene(cards, new[] {4,4,6,2,4}, "bbbrb", tier == 0 ? 0 : 2, tier != 0,
                                stageIndex:index, risk:tier == 1, target:tier == 1 ? def.riskTarget : def.normalTarget, score:"0");
                            scene.controllerSlotPurchases = app.Config.maxOrdinaryControllerSlots - app.Config.initialControllerSlots;
                            for (int i = app.Config.maxOrdinaryControllerSlots; i < scene.controllers.Count; i++)
                                scene.controllers[i].enchantmentId = "slotless";
                            if (tier == 2) scene.modifierId = app.Session.State.cycles[chapter-1].modifierId;
                            W11Apply(scene);
                            if(tier == 2 && app.Session.CycleView()[2].bossTarget != scene.target)
                            { scene.target = app.Session.CycleView()[2].bossTarget; W11Apply(scene); }
                            yield return W11Settle();
                            string id = theme + "-ch" + chapter + "-" + def.stageKind;
                            Assert.That(app.Session.State.stage.stageIndex, Is.EqualTo(index));
                            Assert.That(app.Session.State.stage.risk, Is.EqualTo(tier == 1));
                            Assert.That(app.TableMood11, Is.EqualTo(tier == 2 ? 1 : 0), id);
                            var shift = ChapterUniform18();
                            Assert.That(shift.w, Is.EqualTo(chapter == 1 ? 0 : style + 1), id);
                            if (style >= 2 && chapter > 1)
                            {
                                var mpb = new MaterialPropertyBlock(); GameObject.Find("Art07Felt").GetComponent<Renderer>().GetPropertyBlock(mpb);
                                Assert.That(mpb.GetTexture("_ChapterMasks18"), Is.SameAs(Resources.Load<Texture2D>("ChapterTables18/material_masks")));
                            }
                            string state = ExtraStateHash07(), bytes = ExtraFileHash07(app.SaveLocation);
                            long sequence = app.Session.ActionSequence;
                            if (style >= 2 && representative)
                            {
                                foreach (int percent in new[] {100,150})
                                {
                                    W11TextScale(percent); yield return W11Settle();
                                    Assert.That(LabelByName18("Chapter").GetParsedText(), Does.Contain("第 " + chapter + " 章"));
                                    Assert.That(LabelByName18("StageLabel11").GetParsedText(), Is.EqualTo("第 " + (index + 1) + " 关"));
                                    yield return RolloutShot18(Path.Combine(RolloutOutput18, id + "-" + percent));
                                    yield return StageContrast18(id + "-" + percent, report, failures);
                                }
                            }
                            else app.Refresh();
                            Assert.That(ExtraStateHash07(), Is.EqualTo(state), id);
                            Assert.That(ExtraFileHash07(app.SaveLocation), Is.EqualTo(bytes), id);
                            Assert.That(app.Session.ActionSequence, Is.EqualTo(sequence), id);
                            Color32[] pixels = null; yield return FeltOnly20Xb(values => pixels = values);
                            float actual = Lightness18(Point18(pixels, 960, 540));
                            Color32 rail = Point18(pixels, anchors[style].x, anchors[style].y);
                            report.Add($"{id} | stage={index+1} | mode={shift.w} | centre L*={actual:F2} | ornament #{ColorUtility.ToHtmlStringRGB(rail)}");
                            if (tier == 0)
                            {
                                if (chapterL.Count > 0 && (actual - chapterL.Last()) * expectedDelta <= 0) failures.Add(id + " direction/duplicate");
                                chapterL.Add(actual);
                                if (locked.HasValue && Delta20Xb(rail, locked.Value) > 2) failures.Add(id + " decoration tinted");
                                locked ??= rail;
                            }
                            else if (tier == 1 && Math.Abs(actual - chapterL.Last()) > 1) failures.Add(id + " whole-table pressure tint");
                            else if (tier == 2 && actual >= chapterL.Last()) failures.Add(id + " active boss did not darken the table");
                        }
                        yield return Stage18(chapter, 1, false);
                        Assert.That(app.TableMood11, Is.Zero);
                        Assert.That(app.Session.State.stage.risk, Is.False);
                        Assert.That(HasActive("StageCardEdge11"), Is.False, "Declining high pressure must remove its warning card edge.");
                    }
                    float difference = chapterL[7] - chapterL[0];
                    if (Mathf.Abs(difference - expectedDelta) > 1.5f) failures.Add(theme + " first/last L* " + difference);
                }
            }
            finally
            {
                File.WriteAllLines(Path.Combine(RolloutOutput18, "24-stages.txt"), report.Concat(failures));
                GameApp.CleanCapture11 = clean; W11TextScale(100); app.ApplyTheme11(GameTheme.DefaultId, false);
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        private IEnumerator StageContrast18(string scope, List<string> report, List<string> failures)
        {
            var names = new[]{"StageLabel11","StageCardTitle11","Chapter","Target","StageGoal11","BossModifierName","BossModifierText"};
            var labels = W14Main.GetComponentsInChildren<TextMeshProUGUI>(false).Where(t => names.Contains(t.name)).ToArray();
            Assert.That(labels.Length, Is.GreaterThanOrEqualTo(5), scope);
            try
            {
                foreach (var t in labels) t.enabled = false;
                yield return new WaitForEndOfFrame(); var blank = Screen20Xb();
                foreach (var t in labels) t.enabled = true;
                foreach (var t in labels)
                {
                    t.ForceMeshUpdate(); var ratios = new List<float>();
                    foreach (var ch in t.textInfo.characterInfo.Take(t.textInfo.characterCount).Where(c=>c.isVisible))
                    {
                        var p = t.transform.TransformPoint((ch.bottomLeft + ch.topRight) * .5f);
                        var fill = blank[Mathf.Clamp((int)p.y,0,Screen.height-1)*Screen.width + Mathf.Clamp((int)p.x,0,Screen.width-1)];
                        ratios.Add(GameApp.Contrast11(ch.color, fill));
                    }
                    if(ratios.Count==0) { failures.Add(scope + " no visible ink " + t.name); continue; }
                    string line=$"{scope} {t.name} minimum actual background contrast {ratios.Min():F2}";
                    report.Add(line); if (ratios.Min()<GameApp.MinContrast11) failures.Add(line);
                }
            }
            finally { foreach (var t in labels) if(t!=null)t.enabled=true; }
        }

        [UnityTest]
        public IEnumerator PLAY_V18_CT03_ChapterScoringFrames()
        {
            StartRun("20261122");
            foreach(string theme in RolloutStyles18)
            {
                app.ApplyTheme11(theme);
                foreach(int percent in new[]{100,150})
                {
                    W11TextScale(percent);
                    W11Apply(W11Scene(W11Cards7,new[]{1,2,3,4,5},"bbbbr",4,true,stageIndex:9,target:"3000",score:"0"));
                    yield return W11Settle();
                    var commit = app.Send(new SessionCommand{kind=CommandKind.Submit});
                    Assert.That(commit.accepted,Is.True,commit.error);
                    bool enabled = app.enabled; app.enabled = false;
                    try
                    {
                        W11HoldAt(DiceCraft.Presentation.PresentationPhase.DiceContributions,.8f);
                        yield return RolloutShot18(Path.Combine(RolloutOutput18,theme+"-ch4-scoring-"+percent));
                        Assert.That(ChapterUniform18().w,Is.GreaterThan(0));
                    }
                    finally { app.enabled=enabled; }
                    app.SkipVisual(); yield return W11Settle();
                }
            }
            W11TextScale(100); app.ApplyTheme11(GameTheme.DefaultId,false);
        }

        [UnityTest]
        public IEnumerator PLAY_V18_CT04_ChapterRolloutSurvivesScreensAndLiftedBoss()
        {
            PlayerPrefs.SetInt("reduced", 1);
            var initial = ShopAfterStage10(1);
            initial.cycles[0].modifierId = "B25"; initial.cycles[0].lootKind = "L1";
            Equip09(initial, "C131", "C46");
            yield return W14Load(initial);
            Click("LeaveShop"); yield return WaitForIdle();
            Assert.That(app.Session.EntryView().scapegoat, Is.True);
            Click("StartStage"); yield return WaitForIdle();
            Assert.That(app.Session.State.stage.modifierNullified, Is.True);
            var protectedState = ExtraStateHash07(); var protectedSave = ExtraFileHash07(app.SaveLocation);
            foreach (string theme in RolloutStyles18)
            {
                app.ApplyTheme11(theme); yield return W11Settle();
                Assert.That(app.TableMood11, Is.Zero, theme + " lifted boss");
                Assert.That(HasActive("StageCardEdgeInner11"), Is.False);
                Assert.That(ExtraStateHash07(), Is.EqualTo(protectedState));
                Assert.That(ExtraFileHash07(app.SaveLocation), Is.EqualTo(protectedSave));
            }
            // Real paused save load, then walk chapter-aware and page-only screens in every palette.
            var bossShop=ShopAfterStage10(10);
            yield return W14Load(bossShop);
            Click("LeaveShop");yield return WaitForIdle();
            Click("StartStage");yield return WaitForIdle();
            var boss = app.Session.State;
            Assert.That(boss.stage.stageIndex,Is.EqualTo(11));
            Assert.That(boss.stage.modifierNullified,Is.False);
            string savedBoss=ExtraFileHash07(app.SaveLocation);
            yield return LoadBootstrap(); ResumeBoundaryFixture(); yield return W11Settle();
            Assert.That(ExtraFileHash07(app.SaveLocation),Is.EqualTo(savedBoss));
            Assert.That(app.Session.State.runId,Is.EqualTo(boss.runId));
            Assert.That(app.Session.State.stage.stageId,Is.EqualTo(boss.stage.stageId));
            Assert.That(app.Session.State.stage.modifierId, Is.EqualTo(boss.stage.modifierId));
            foreach(string theme in RolloutStyles18)
            {
                app.ApplyTheme11(theme); yield return W11Settle(); var expected = ChapterUniform18();
                Assert.That(expected.w, Is.GreaterThan(0)); Assert.That(app.TableMood11, Is.EqualTo(1));
                string state = ExtraStateHash07(), save = ExtraFileHash07(app.SaveLocation);
                Click("Pause"); yield return W11Settle();
                Assert.That(ChapterUniform18(), Is.EqualTo(expected)); Click("Resume");
                app.ShowHandbook(); yield return W11Settle();
                Assert.That(ChapterUniform18(), Is.EqualTo(expected)); Click("CloseDialog");
                Assert.That(ExtraStateHash07(), Is.EqualTo(state)); Assert.That(ExtraFileHash07(app.SaveLocation), Is.EqualTo(save));
            }
            // A normal stage crosses the settlement → shop → next stage boundary without any boss-loot workflow.
            yield return IdentityEndStage20X(9); yield return W11Settle();
            Assert.That(app.TableMood11, Is.Zero); Assert.That(ChapterUniform18().w, Is.GreaterThan(0));
            Click("SettlementContinue"); yield return WaitForIdle(); Assert.That(app.Session.State.phase, Is.EqualTo("Shop"));
            Assert.That(ChapterUniform18(), Is.EqualTo(Vector4.zero));
            Click("LeaveShop"); yield return WaitForIdle(); Assert.That(app.Session.State.phase, Is.EqualTo("StagePreview"));
            Assert.That(ChapterUniform18(), Is.EqualTo(Vector4.zero));
            Click("StartStage"); yield return WaitForIdle(); Assert.That(ChapterUniform18().w, Is.GreaterThan(0));
            Click("Pause"); Click("MainMenu"); yield return W11Settle();
            Assert.That(ChapterUniform18(), Is.EqualTo(Vector4.zero)); Assert.That(app.TableMood11, Is.Zero);
            app.ApplyTheme11(GameTheme.DefaultId, false);
        }
    }
}
