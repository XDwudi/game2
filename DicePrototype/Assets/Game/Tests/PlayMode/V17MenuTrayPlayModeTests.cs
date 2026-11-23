using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DiceCraft.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        // The no-tray pass may run against the W16 menu before the renderer or art is installed.
        [UnityTest] public IEnumerator PLAY_V17_DefaultMenuNoTrayBaseline() => MenuComparison17(false);
        [UnityTest] public IEnumerator PLAY_V17_DefaultMenuTrayGolden() => MenuComparison17(true);

        [UnityTest] public IEnumerator PLAY_V23_MenuTrayAcrossThemes([ValueSource(nameof(Styles22i))] string theme)
            => MenuComparison17(true, theme);

        [UnityTest] public IEnumerator PLAY_V23_PausingAnActiveBossRestoresTheMenuLightingAndResumesTheSameHand()
        {
            var fixture=ShopAfterStage10(1);
            fixture.isDebug=false;
            fixture.debugWorkspace=false;
            var session=new GameSession(app.Config,new MemorySaveStore(),fixture);
            CoreShopStep(session,CommandKind.LeaveShop);
            CoreShopStep(session,CommandKind.StartStage);
            Assert.That(session.State.stage.stageKind,Is.EqualTo("boss"));
            Assert.That(session.State.stage.modifierNullified,Is.False);
            Assert.That(session.State.stage.modifierId,Is.Not.Null.And.Not.Empty);
            new AtomicSaveStore(app.SaveLocation,app.Config,SaveSlotPolicy.Normal).Save(session.State);
            yield return LoadBootstrap();
            yield return MenuPress17("ContinueRun");
            string save=ExtraFileHash07(app.SaveLocation),state=ExtraStateHash07();
            long sequence=app.Session.ActionSequence;
            var felt=GameObject.Find("Art07Felt").GetComponent<Renderer>();
            var properties=new MaterialPropertyBlock();
            void CheckLighting(bool menu)
            {
                felt.GetPropertyBlock(properties);
                Assert.That(properties.GetFloat("_Dim11"),Is.EqualTo(menu ? 0f : .26f).Within(.001f));
                Assert.That(properties.GetFloat("_Vignette11"),Is.EqualTo(menu ? 0f : .5f).Within(.001f));
                Assert.That(properties.GetFloat("_ShowMenuTray17"),Is.EqualTo(menu ? 1f : 0f));
                Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));
                Assert.That(ExtraStateHash07(),Is.EqualTo(state));
                Assert.That(ExtraFileHash07(app.SaveLocation),Is.EqualTo(save));
            }
            CheckLighting(false);
            yield return MenuPress17("Pause");
            yield return MenuPress17("MainMenu");
            Assert.That(HasActive("ContinueRun"),Is.True);
            CheckLighting(true);
            yield return CaptureReleasedTheme11("main-menu-from-active-boss");
            yield return MenuPress17("ContinueRun");
            CheckLighting(false);
            Assert.That(HasActive("BossModifierName"),Is.True);
            yield return CaptureReleasedTheme11("resumed-active-boss");
        }

        private IEnumerator MenuComparison17(bool compare, string theme = GameTheme.DefaultId)
        {
            Assert.That((Screen.width == 1920 && Screen.height == 1080) || (Screen.width == 1280 && Screen.height == 720));
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "cache", "v11",
                "w17", compare ? "golden" : "baseline", Screen.width + "x" + Screen.height));
            if (theme != GameTheme.DefaultId) output = Path.Combine(output, theme);
            Directory.CreateDirectory(output);
            var report = new List<string> { IdentityHead20X() + " | Unity " + Application.unityVersion + " | " + Screen.width + "x" + Screen.height };
            var failures = new List<string>();
            bool clean = GameApp.CleanCapture11, reduced = UiKit.ReducedMotion;
            string prior = app.ThemeId11;
            var toggle = typeof(GameApp).GetMethod("ShowMenuTray17", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(bool) }, null);
            if (compare) Assert.That(toggle, Is.Not.Null, "Install the actual tray switch for the paired run.");
            var renderer = GameObject.Find("Art07Felt").GetComponent<Renderer>();
            Material felt = renderer.sharedMaterial;
            var props = new MaterialPropertyBlock();
            int showProperty = Shader.PropertyToID("_ShowMenuTray17"), textureProperty = Shader.PropertyToID("_MenuTray17");
            float Shown() { renderer.GetPropertyBlock(props); return props.HasFloat(showProperty) ? props.GetFloat(showProperty) : felt.GetFloat(showProperty); }
            Texture Tray() { renderer.GetPropertyBlock(props); return props.HasTexture(textureProperty) ? props.GetTexture(textureProperty) : felt.GetTexture(textureProperty); }
            void Switch(bool on) { if (toggle != null) toggle.Invoke(app, new object[] { on }); }
            try
            {
                GameApp.CleanCapture11 = true;
                app.ApplyTheme11(theme);
                Assert.That(app.ThemeId11,Is.EqualTo(theme));
                Assert.That(app.Session.State, Is.Null, "The test fixture opens with an empty isolated save slot.");
                foreach (bool saved in new[] { false, true })
                {
                    if (saved)
                    {
                        yield return MenuPress17("NewRun");
                        FindActive<TMP_InputField>("SeedInput").text = "20261110";
                        yield return MenuPress17("StartNew");
                        Assert.That(app.Session.State.seed, Is.EqualTo(20261110u));
                        yield return MenuPress17("Pause"); yield return MenuPress17("MainMenu");
                    }
                    string state = ExtraStateHash07(), save = ExtraFileHash07(app.SaveLocation);
                    long seq = app.Session.ActionSequence;
                    foreach (int scale in new[] { 100, 150 })
                    {
                        W11TextScale(scale);
                        UiKit.ReducedMotion = true; // Pin the wordmark's idle float for a paired pixel comparison.
                        yield return Art073Settle(.75f);
                        string id = (saved ? "saved-" : "empty-") + scale;
                        Switch(false); yield return Art073Settle(.25f);
                        if (felt.HasProperty("_ShowMenuTray17")) Assert.That(Shown(), Is.LessThan(.01f), id);
                        AssertMenuForeground17(saved);
                        yield return new WaitForEndOfFrame();
                        var off = ScreenCapture.CaptureScreenshotAsTexture();
                        try
                        {
                            WriteArtFrame23(Path.Combine(output, id + "-off.png"), off);
                            if (!compare) continue;
                            yield return new WaitForEndOfFrame();
                            var offRepeat = Screen20Xb();
                            Switch(true); yield return Art073Settle(.25f);
                            Assert.That(felt.HasProperty("_MenuTray17") && felt.HasProperty("_ShowMenuTray17"), Is.True);
                            var expectedTray = Resources.Load<Texture2D>("MenuArt17/" + theme);
                            Assert.That(expectedTray, Is.Not.Null, theme + " tray asset");
                            report.Add(id+" | "+theme+" | Resources/MenuArt17/"+expectedTray.name);
                            Assert.That(Tray(), Is.SameAs(expectedTray), "The menu binds the current style's own tray");
                            Assert.That(Shown(), Is.GreaterThan(.99f), id);
                            AssertMenuForeground17(saved);
                            yield return new WaitForEndOfFrame();
                            var on = ScreenCapture.CaptureScreenshotAsTexture();
                            try
                            {
                                WriteArtFrame23(Path.Combine(output, id + "-on.png"), on);
                                MenuCrops17(on, output, id);
                                yield return new WaitForEndOfFrame();
                                var onRepeat = Screen20Xb();
                                MenuDelta17(off.GetPixels32(), offRepeat, on.GetPixels32(), onRepeat, id, report, failures);
                                yield return MenuText17(id, report, failures);
                            }
                            finally { UnityEngine.Object.Destroy(on); }
                        }
                        finally { UnityEngine.Object.Destroy(off); }
                    }
                    if (compare)
                    {
                        foreach (string route in new[] { "MenuSettings", "MenuHandbook", "Quit", "NewRun" })
                        {
                            yield return MenuPress17(route);
                            Assert.That(HasActive("Dialog"), Is.True, route);
                            if (route == "NewRun") Assert.That(Shown(), Is.LessThan(.01f), "The new game screen clears the tray.");
                            yield return MenuPress17(route == "MenuSettings" ? "SettingsDone" : "CloseDialog");
                            Assert.That(Shown(), Is.GreaterThan(.99f), "Return to menu after " + route);
                        }
                    }
                    Assert.That(ExtraStateHash07(), Is.EqualTo(state));
                    Assert.That(ExtraFileHash07(app.SaveLocation), Is.EqualTo(save));
                    Assert.That(app.Session.ActionSequence, Is.EqualTo(seq));
                }
                if (compare)
                {
                    yield return MenuPress17("ContinueRun");
                    Assert.That(HasActive("StartStage"));
                    Assert.That(Shown(), Is.LessThan(.01f), "The stage selector uses its own table.");
                    Assert.That(Tray(), Is.SameAs(Texture2D.blackTexture), "No menu artwork retained on the stage table.");
                    // Switching in a live run cannot restore a tray or write a save. The real menu
                    // must subsequently bind the newly selected artwork, then rebind the first style.
                    string state=ExtraStateHash07(),save=ExtraFileHash07(app.SaveLocation);
                    long sequence=app.Session.ActionSequence;
                    app.ApplyTheme11(GameTheme.DefaultId); yield return Art073Settle(.25f);
                    Assert.That(Shown(), Is.LessThan(.01f));
                    Assert.That(Tray(), Is.SameAs(Texture2D.blackTexture));
                    Assert.That(ExtraStateHash07(),Is.EqualTo(state));Assert.That(ExtraFileHash07(app.SaveLocation),Is.EqualTo(save));
                    Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));
                    yield return MenuPress17("Pause"); yield return MenuPress17("MainMenu");
                    state=ExtraStateHash07();save=ExtraFileHash07(app.SaveLocation);sequence=app.Session.ActionSequence;
                    Assert.That(Shown(), Is.GreaterThan(.99f));
                    Assert.That(Tray(),Is.SameAs(Resources.Load<Texture2D>("MenuArt17/"+GameTheme.DefaultId)));
                    app.ApplyTheme11(theme); yield return Art073Settle(.25f);
                    Assert.That(Shown(), Is.GreaterThan(.99f));
                    Assert.That(Tray(),Is.SameAs(Resources.Load<Texture2D>("MenuArt17/"+theme)));
                    if(theme==GameTheme.DefaultId)
                    {
                        const string missing="w23-missing-menu";
                        ThemeRegistry.RegisterForTests(GameTheme.CreateForTests(missing,new ThemePalette()));
                        try
                        {
                            app.ApplyTheme11(missing);yield return Art073Settle(.25f);
                            Assert.That(app.ThemeId11,Is.EqualTo(missing));
                            Assert.That(Shown(),Is.LessThan(.01f),"A style without a tray shows the real table and live foreground.");
                            Assert.That(Tray(),Is.SameAs(Texture2D.blackTexture),"No previous theme's tray remains bound.");
                            AssertMenuForeground17(true);
                        }
                        finally {app.ApplyTheme11(theme);ThemeRegistry.UnregisterForTests(missing);}
                        yield return Art073Settle(.25f);
                        Assert.That(Tray(),Is.SameAs(Resources.Load<Texture2D>("MenuArt17/"+theme)));
                    }
                    Assert.That(ExtraStateHash07(),Is.EqualTo(state));Assert.That(ExtraFileHash07(app.SaveLocation),Is.EqualTo(save));
                    Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));
                }
            }
            finally
            {
                UiKit.ReducedMotion = reduced; GameApp.CleanCapture11 = clean;
                W11TextScale(100); app.ApplyTheme11(prior);
                File.WriteAllLines(Path.Combine(output, "menu-review.txt"), report.Concat(failures));
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        private IEnumerator MenuPress17(string name)
        {
            yield return Art073Settle(.23f);
            Assert.That(Button(name).IsInteractable(), Is.True, name);
            Art071PointerClick(name);
            yield return WaitForIdle(); yield return Art073Settle(.25f);
        }

        [Serializable]
        private sealed class ArtFrameEvidence23
        {
            public string path,sha256,caseName,head;
            public int width,height;
        }

        private static string WriteArtFrame23(string path,Texture2D picture)
        {
            string cache=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","artifacts","cache","v11"))+Path.DirectorySeparatorChar;
            string full=Path.GetFullPath(path);
            Assert.That(full.StartsWith(cache,StringComparison.OrdinalIgnoreCase),Is.True,full);
            byte[] encoded=picture.EncodeToPNG();
            File.WriteAllBytes(full,encoded);
            string digest;
            using(var sha=System.Security.Cryptography.SHA256.Create())
                digest=BitConverter.ToString(sha.ComputeHash(encoded)).Replace("-","").ToLowerInvariant();
            var frame=new ArtFrameEvidence23{
                path=full.Substring(cache.Length).Replace(Path.DirectorySeparatorChar,'/'),
                sha256=digest,width=picture.width,height=picture.height,caseName=TestContext.CurrentContext.Test.FullName,
                head=IdentityHead20X()
            };
            TestContext.WriteLine("W23_FRAME "+JsonUtility.ToJson(frame));
            return digest;
        }

        private void AssertMenuForeground17(bool saved)
        {
            Assert.That(HasActive("ContinueRun"), Is.EqualTo(saved));
            Assert.That(FindActive<TextMeshProUGUI>("SavedSeed").text, Is.EqualTo(saved ? "第 1 关 · 0 金币" : "暂无存档 · 开始你的第一局"));
            Assert.That(W11PrimaryColoured(Button(saved ? "ContinueRun" : "NewRun").targetGraphic.color));
            var mark = FindActive<RectTransform>("MenuWordmark");
            Assert.That(mark.Find("Title").GetComponent<Image>().sprite.name, Is.EqualTo("ui_wordmark_dicecraft"));
            for (int i = 0; i < 4; i++)
            {
                var card = FindActive<RectTransform>("MenuCardBack" + i);
                Assert.That(card.sizeDelta, Is.EqualTo(new Vector2(218, 280)));
                if (i == 0 || i == 3)
                {
                    Assert.That(card.Find("PrintedCardBack").GetComponent<Image>().sprite.name, Is.EqualTo("ui_card_back"));
                    continue;
                }
                var name = card.GetComponentsInChildren<TextMeshProUGUI>().Single(t => t.name == "Name");
                name.ForceMeshUpdate();
                Assert.That(name.GetParsedText(), Is.EqualTo(app.Config.Controller(i == 1 ? "C48" : "C29").displayName));
            }
            for (int i = 0; i < 3; i++)
            {
                var pivot = app.table.GetFaceOrientation("$menu" + i);
                Assert.That(pivot != null && pivot.gameObject.activeInHierarchy, Is.True, "3D die " + i);
                Assert.That(pivot.GetComponentsInChildren<Renderer>().Any(r => r.enabled && r.gameObject.activeInHierarchy), Is.True);
                var die = DiceFactory.CreateStandard(app.Config, i + 1);
                var face = die.faces.Single(f => f.permanentPip == 2 * i + 1);
                float up = Vector3.Dot(pivot.TransformDirection(DiceTable.Normals[DiceTable.FaceIndex(face.faceId)]), Vector3.up);
                Assert.That(up, Is.GreaterThan(.999f), "Physical upper face must show " + face.permanentPip);
            }
        }

        private static void MenuDelta17(Color32[] a, Color32[] aa, Color32[] b, Color32[] bb, string id, List<string> report, List<string> failures)
        {
            // Two samples per state distinguish ordinary frame noise from a persistent renderer difference.
            int inside = 0, outside = 0;
            int sx = Screen.width, sy = Screen.height;
            int Gap(byte x, byte xx, byte y, byte yy) => Math.Max(Math.Min(x, xx) - Math.Max(y, yy), Math.Min(y, yy) - Math.Max(x, xx));
            for (int k = 0; k < a.Length; k++)
            {
                if (Math.Max(Gap(a[k].r, aa[k].r, b[k].r, bb[k].r),
                    Math.Max(Gap(a[k].g, aa[k].g, b[k].g, bb[k].g), Gap(a[k].b, aa[k].b, b[k].b, bb[k].b))) <= 5) continue;
                float x = ((k % sx) + .5f) * 1920 / sx, y = (sy - (k / sx) - .5f) * 1080 / sy;
                if (x >= 89 && x <= 1151 && y >= 423 && y <= 977) inside++; else outside++;
            }
            string line = $"{id} paired stable pixels, Δchannel >5/255 inside tray rectangle {inside}, outside {outside}";
            report.Add(line);
            if (inside < 100 || outside != 0) failures.Add(line);
        }

        private static void MenuCrops17(Texture2D frame, string output, string id)
        {
            void Crop(string name, int x, int y, int w, int h)
            {
                float scale = frame.width / 1920f;
                int left = Mathf.RoundToInt(x * scale), bottom = frame.height - Mathf.RoundToInt((y + h) * scale);
                int width = Mathf.RoundToInt(w * scale), height = Mathf.RoundToInt(h * scale);
                var cut = new Texture2D(width, height, TextureFormat.RGB24, false);
                try
                {
                    cut.SetPixels(frame.GetPixels(left, bottom, width, height)); cut.Apply();
                    WriteArtFrame23(Path.Combine(output, id + "-" + name + ".png"), cut);
                }
                finally { UnityEngine.Object.Destroy(cut); }
            }
            Crop("wordmark", 128, 86, 1000, 334); Crop("C48", 346, 418, 300, 350); Crop("C29", 570, 425, 300, 370);
            Crop("die1", 218, 766, 228, 228); Crop("die3", 526, 780, 228, 228); Crop("die5", 862, 768, 228, 228);
            Crop("menu", 1216, 76, 616, 928);
        }

        private IEnumerator MenuText17(string id, List<string> report, List<string> failures)
        {
            var texts = new List<TextMeshProUGUI>();
            foreach (string root in new[] { "MenuWordmark", "MenuPanel", "MenuCardBack1", "MenuCardBack2", "BuildVersion" })
                texts.AddRange(FindActive<RectTransform>(root).GetComponentsInChildren<TextMeshProUGUI>(false).Where(t => t.enabled && !string.IsNullOrWhiteSpace(t.text)));
            Assert.That(texts.Count, Is.GreaterThan(8), id + " must sample the actual foreground");
            try
            {
                foreach (var text in texts) { text.ForceMeshUpdate(); text.enabled = false; }
                yield return new WaitForEndOfFrame();
                var blank = Screen20Xb();
                foreach (var text in texts) text.enabled = true;
                foreach (var text in texts)
                {
                    text.ForceMeshUpdate();
                    var chars = text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(c => c.isVisible).ToArray();
                    if (chars.Length == 0 || text.isTextTruncated) { failures.Add(id + " empty/truncated " + text.name); continue; }
                    float lowest = float.MaxValue;
                    foreach (var c in chars)
                    {
                        var at = text.transform.TransformPoint((c.bottomLeft + c.topRight) * .5f);
                        int x = Mathf.Clamp(Mathf.RoundToInt(at.x), 0, Screen.width - 1), y = Mathf.Clamp(Mathf.RoundToInt(at.y), 0, Screen.height - 1);
                        lowest = Mathf.Min(lowest, GameApp.Contrast11(c.color, blank[y * Screen.width + x]));
                    }
                    string row = $"{id} {W11bPath(text.transform)} ({chars.Length} glyphs) min sampled contrast {lowest:F2}";
                    report.Add(row); if (lowest < GameApp.MinContrast11) failures.Add(row);
                }
            }
            finally { foreach (var text in texts) text.enabled = true; }
        }
    }
}
