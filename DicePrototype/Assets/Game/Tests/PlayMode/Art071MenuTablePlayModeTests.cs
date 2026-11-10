using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        private static readonly string[] MenuTableButtons071 = { "ContinueRun", "NewRun", "MenuHandbook", "MenuSettings", "Quit", "BuildVersion" };

        private void MenuTableActualViewport071()
        {
            Assert.That((Screen.width == 1920 && (Screen.height == 1080 || Screen.height == 1200)) ||
                        (Screen.width == 1280 && Screen.height == 720) || (Screen.width == 2560 && Screen.height == 1600), Is.True,
                "Run at a real 1080p, 720p, or 16:10 GameView; this test does not resize or simulate the viewport.");
        }

        // Uses the existing real first-hit pointer helper, not direct onClick invocation.
        private IEnumerator MenuTablePress071(string name, bool mutation = false)
        {
            yield return ExtraIdle07();
            var button = Button(name); yield return ExtraReveal07((RectTransform)button.transform); button = Button(name);
            var rect = ExtraBounds07((RectTransform)button.transform);
            Assert.That(button.IsInteractable(), Is.True, name);
            Assert.That(rect.width, Is.GreaterThanOrEqualTo(39.99f), name);
            Assert.That(rect.height, Is.GreaterThanOrEqualTo(39.99f), name);
            var row = new ExtraAction07 { name = name, mutationAllowed = mutation, stateBefore = ExtraStateHash07(),
                saveBefore = ExtraFileHash07(app.SaveLocation), beforeSequence = app.Session.ActionSequence };
            Art071PointerClick(name);
            yield return ExtraIdle07();
            row.stateAfter = ExtraStateHash07(); row.saveAfter = ExtraFileHash07(app.SaveLocation); row.afterSequence = app.Session.ActionSequence;
            extra07.actions.Add(row); ExtraWrite07();
            if (!mutation)
            {
                Assert.That(row.stateAfter, Is.EqualTo(row.stateBefore), name + " must be view-only");
                Assert.That(row.saveAfter, Is.EqualTo(row.saveBefore), name + " must not rewrite the checkpoint");
                Assert.That(row.afterSequence, Is.EqualTo(row.beforeSequence), name + " must not consume a command");
            }
        }

        private IEnumerator MenuTableFont071(int font)
        {
            yield return MenuTablePress071("MenuSettings"); yield return MenuTablePress071("SettingsTab2");
            for (int i = 0; i < 3 && Mathf.RoundToInt(UiKit.FontScale * 100) != font; i++) yield return MenuTablePress071("TextScale");
            Assert.That(Mathf.RoundToInt(UiKit.FontScale * 100), Is.EqualTo(font));
            yield return MenuTablePress071("SettingsDone");
        }

        private IEnumerator MenuTableNewRun071()
        {
            yield return MenuTablePress071("NewRun");
            FindActive<TMP_InputField>("SeedInput").text = "20261110";
            yield return MenuTablePress071("StartNew", true);
            Assert.That(app.Session.State.seed, Is.EqualTo(20261110u));
            Assert.That(app.Session.State.debugWorkspace, Is.False);
        }

        [UnityTest]
        public IEnumerator PLAY_ART071_MenuTableRealMenuRoutesPreserveCancelAndContinueCheckpoint()
        {
            MenuTableActualViewport071(); yield return ExtraBegin07();
            Assert.That(Button("ContinueRun").IsInteractable(), Is.False);
            Assert.That(File.Exists(app.SaveLocation), Is.False);
            yield return ExtraCapture07("table-menu-empty", "S01", "Actual empty menu before navigation; no run has been created.");
            foreach (string entry in new[] { "NewRun", "MenuHandbook", "BuildVersion", "Quit" })
            {
                yield return MenuTablePress071(entry);
                if (entry == "NewRun")
                {
                    FindActive<TMP_InputField>("SeedInput").text = "123";
                    yield return MenuTablePress071("RandomSeed");
                    Assert.That(FindActive<TMP_InputField>("SeedInput").text, Is.Not.Empty);
                }
                if (entry == "BuildVersion") Assert.That(FindActive<TextMeshProUGUI>("BuildIdentity").text, Does.Contain(GameApp.ArtVersion).And.Contain(app.Config.configVersion));
                if (entry == "Quit") Assert.That(Button("ConfirmQuit").IsInteractable(), Is.True); // Never terminate the test host.
                yield return MenuTablePress071("CloseDialog");
            }
            yield return MenuTablePress071("MenuSettings"); yield return MenuTablePress071("SettingsDone");
            Assert.That(app.Session.State, Is.Null); Assert.That(File.Exists(app.SaveLocation), Is.False);
            yield return MenuTableNewRun071();
            string state = ExtraStateHash07(), save = ExtraFileHash07(app.SaveLocation); long sequence = app.Session.ActionSequence;
            yield return MenuTablePress071("Pause"); yield return MenuTablePress071("MainMenu");
            Assert.That(Button("ContinueRun").IsInteractable(), Is.True);
            Assert.That(FindActive<TextMeshProUGUI>("SavedSeed").text, Does.Contain("20261110"));
            yield return ExtraCapture07("table-menu-saved", "S01", "Actual ordinary checkpoint, enabled Continue and live saved seed.");
            yield return MenuTablePress071("NewRun");
            Assert.That(FindActive<TextMeshProUGUI>("Overwrite").text, Does.Contain("20261110"));
            yield return MenuTablePress071("CloseDialog"); yield return MenuTablePress071("ContinueRun");
            Assert.That(Button("StartStage").IsInteractable(), Is.True);
            Assert.That(ExtraStateHash07(), Is.EqualTo(state)); Assert.That(ExtraFileHash07(app.SaveLocation), Is.EqualTo(save));
            Assert.That(app.Session.ActionSequence, Is.EqualTo(sequence));
            ExtraFinish07();
        }

        [Serializable] private sealed class MenuTableGlyphReport071
        {
            public string scope, runtimeMvid, stateHash, saveSha;
            public int width, height, fontPercent;
            public List<CardGlyph07> labels = new List<CardGlyph07>();
            public List<string> errors = new List<string>();
        }

        private void MenuTableGlyphs071(string name)
        {
            var root = FindActive<RectTransform>("Screen"); var panel = FindActive<RectTransform>("MenuPanel");
            var row = new MenuTableGlyphReport071 { scope = "Complete menu metadata and six action labels; visible glyph meshes, own rectangles, safe area and mutual overlap.",
                runtimeMvid = typeof(GameApp).Module.ModuleVersionId.ToString(), stateHash = ExtraStateHash07(), saveSha = ExtraFileHash07(app.SaveLocation),
                width = Screen.width, height = Screen.height, fontPercent = Mathf.RoundToInt(UiKit.FontScale * 100) };
            var labels = new List<TextMeshProUGUI>();
            foreach (string id in new[] { "Subtitle", "MenuRules", "LockHint", "MenuLabel", "SavedSeed", "Version" }) labels.Add(FindActive<TextMeshProUGUI>(id));
            foreach (string id in MenuTableButtons071)
            {
                var button = Button(id); Rect bounds = ExtraBounds07((RectTransform)button.transform);
                Assert.That(bounds.width, Is.GreaterThanOrEqualTo(39.99f), id);
                Assert.That(bounds.height, Is.GreaterThanOrEqualTo(39.99f), id);
                if (button.IsInteractable()) Assert.That(ExtraHit07(button.gameObject, bounds.center), Is.True, id + " first hit");
                labels.Add(button.GetComponentInChildren<TextMeshProUGUI>());
            }
            foreach (var label in labels)
            {
                var glyph = CardGlyphCheck07(root, label, label.text, root.rect.height);
                row.labels.Add(glyph); row.errors.AddRange(glyph.errors.Select(e => label.name + ": " + e));
                Rect physical = ExtraBounds07(label.rectTransform);
                Rect safe = Screen.safeArea;
                if (physical.xMin < safe.xMin - .25f || physical.yMin < safe.yMin - .25f || physical.xMax > safe.xMax + .25f || physical.yMax > safe.yMax + .25f)
                    row.errors.Add(label.name + " leaves actual safe area");
                // The right-hand metadata must remain on the actual paper, including labels parented outside MenuPanel.
                if (label.name == "MenuLabel" || label.name == "SavedSeed" || label.name == "Version" || label.transform.IsChildOf(Button("BuildVersion").transform))
                {
                    var paper = CardGlyphCheck07(panel, label, label.text, CardPaperBottom07(panel));
                    row.errors.AddRange(paper.errors.Where(e => e.Contains("paper region")).Select(e => label.name + ": " + e));
                }
            }
            for (int a = 0; a < row.labels.Count; a++) for (int b = a + 1; b < row.labels.Count; b++)
                if (CardOverlap07(row.labels[a].ink, row.labels[b].ink)) row.errors.Add(row.labels[a].name + " glyph overlaps " + row.labels[b].name);
            File.WriteAllText(Path.Combine(extra07.output, name + "-glyphs.json"), JsonUtility.ToJson(row, true));
            Assert.That(row.errors, Is.Empty, "See retained glyph report and actual screenshot.");
        }

        [UnityTest]
        public IEnumerator PLAY_ART071_MenuTableRealFramesAnd150GlyphsPreserveWorldDiceAperture()
        {
            MenuTableActualViewport071(); int width = Screen.width, height = Screen.height; yield return ExtraBegin07();
            foreach (int font in new[] { 100, 150 })
            {
                yield return MenuTableFont071(font);
                string label = "table-menu-font" + font;
                yield return ExtraCapture07(label, "S01", "Real viewport and UI-set font; actual menu paper/cards/three world dice.");
                MenuTableGlyphs071(label);
                Assert.That(app.canvas.GetComponentsInChildren<Image>(false).Count(i => i.name == "PrintedCardBack"), Is.EqualTo(2));
                for (int i = 0; i < 3; i++) Assert.That(app.table.GetFaceOrientation("$menu" + i).gameObject.activeInHierarchy, Is.True);
            }
            yield return MenuTablePress071("NewRun");
            yield return ExtraCapture07("table-new-run-font150", "S02", "Actual paper aperture and live initial dice preview, not a baked background die.");
            yield return MenuTablePress071("CloseDialog");
            yield return MenuTableNewRun071(); yield return MenuTablePress071("StartStage", true); yield return MenuTablePress071("Roll", true);
            yield return ExtraCapture07("table-selecting-font150", "S04", "Ordinary real roll at 150%; world table under real dice and live HUD.");
            yield return ExtraIdle07(); Art071PointerClick("Die_D001"); yield return ExtraIdle07();
            yield return MenuTablePress071("Confirm", true);
            string before = ExtraStateHash07(), save = ExtraFileHash07(app.SaveLocation);
            Art071PointerClick("Die_D001"); yield return ExtraIdle07();
            var preview = app.table.GetFaceOrientation("$die-preview");
            Assert.That(preview, Is.Not.Null); Assert.That(preview.gameObject.activeInHierarchy, Is.True);
            yield return ExtraCapture07("table-dice-detail-font150", "S14", "Actual world dice preview through the existing modal aperture; visual inspection remains required.");
            yield return MenuTablePress071("CloseDialog");
            Assert.That(ExtraStateHash07(), Is.EqualTo(before)); Assert.That(ExtraFileHash07(app.SaveLocation), Is.EqualTo(save));
            yield return MenuTablePress071("Pause"); yield return MenuTablePress071("MainMenu");
            yield return ExtraCapture07("table-saved-menu-font150", "S01", "Two-line saved checkpoint and Continue at 150%.");
            MenuTableGlyphs071("table-saved-menu-font150");
            Assert.That(Screen.width, Is.EqualTo(width)); Assert.That(Screen.height, Is.EqualTo(height)); ExtraFinish07();
        }

        [Serializable] private sealed class MenuTableGpuSample071
        {
            public Vector2 sourceUV, screenPixel; public Color reference, actual; public float error, flippedError;
        }
        [Serializable] private sealed class MenuTableGpuReport071
        {
            public string runtimeMvid, graphicsApi, colorSpace, texture, shaderSha, textureSha;
            public int width, height, textureWidth, textureHeight; public Rect fit;
            public float meanReferenceError, meanFlippedError, meanGrainDifference, meanReducedDifference, meanDisabledDifference;
            public float referenceLumaRange, outsideLumaMax; public int outsideSamples;
            public List<MenuTableGpuSample071> samples = new List<MenuTableGpuSample071>();
        }

        private IEnumerator MenuTablePreference071(string name, string tab, bool on)
        {
            yield return MenuTablePress071("MenuSettings"); yield return MenuTablePress071(tab);
            string expected = on ? "开" : "关";
            if (!Button(name).GetComponentInChildren<TextMeshProUGUI>().text.StartsWith(expected, StringComparison.Ordinal)) yield return MenuTablePress071(name);
            Assert.That(Button(name).GetComponentInChildren<TextMeshProUGUI>().text, Does.StartWith(expected));
            yield return MenuTablePress071("SettingsDone");
        }

        private static float MenuTableError071(Color a, Color b) => (Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b)) / 3;
        private static Color MenuTablePixel071(Color32[] data, int width, int height, Vector2 point)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt(point.x), 1, width - 2), y = Mathf.Clamp(Mathf.RoundToInt(point.y), 1, height - 2);
            Color sum = Color.clear;
            for (int j = -1; j <= 1; j++) for (int i = -1; i <= 1; i++) sum += (Color)data[(y + j) * width + x + i];
            return sum / 9;
        }

        [UnityTest]
        public IEnumerator PLAY_ART071_TableWorldGpuMatchesAuthoredDirectionAndSurvivesGrainAndReduced()
        {
            MenuTableActualViewport071(); yield return ExtraBegin07();
            // Shared fixture already restores reduced. Register these additional profile keys before touching their real UI.
            foreach (string key in new[] { "art07.grain", "art07.scanlines" })
                if (!intPreferences.ContainsKey(key)) intPreferences[key] = PlayerPrefs.HasKey(key) ? (int?)PlayerPrefs.GetInt(key) : null;
            yield return MenuTablePreference071("Reduced", "SettingsTab2", false);
            yield return MenuTablePreference071("Scanlines", "SettingsTab1", false);
            yield return MenuTablePreference071("Grain", "SettingsTab1", false);
            var receiver = GameObject.Find("Art07Felt").GetComponent<Renderer>(); var material = receiver.sharedMaterial;
            var source = material.GetTexture("_TableMap") as Texture2D;
            Assert.That(source, Is.Not.Null); Assert.That(source.isReadable, Is.False); Assert.That(source.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(material.GetFloat("_UseTableMap"), Is.GreaterThan(.5f));
            var savedBlock = new MaterialPropertyBlock(); receiver.GetPropertyBlock(savedBlock);
            var reveal = app.canvas.GetComponentsInChildren<Art07MenuReveal>(true).Single(); bool wasRevealEnabled = reveal.enabled; reveal.enabled = false;
            var target = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var referenceTarget = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var pixels = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false, true);
            var referencePixels = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
            var oldActive = RenderTexture.active; target.Create(); referenceTarget.Create();
            var result = new MenuTableGpuReport071 { runtimeMvid = typeof(GameApp).Module.ModuleVersionId.ToString(), graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                colorSpace = QualitySettings.activeColorSpace.ToString(), texture = source.name, width = Screen.width, height = Screen.height,
                textureWidth = source.width, textureHeight = source.height,
                shaderSha = ExtraFileHash07(Path.Combine(Application.dataPath, "Game/Runtime/Shaders/Art07Felt.shader")),
                textureSha = ExtraFileHash07(Path.Combine(Application.dataPath, "Art/Art071/Backgrounds/bg_table_scene.png")) };
            Color32[] Capture(string name)
            {
                app.table.Hide();
                // Freeze only decorative drift/breath so the comparison isolates pixels; preserve the actual preference values.
                var block = new MaterialPropertyBlock(); receiver.GetPropertyBlock(block); block.SetFloat("_MenuDriftPixels", 0); block.SetFloat("_MenuBreath", 0); receiver.SetPropertyBlock(block);
                RenderPipeline.SubmitRenderRequest(app.table.viewCamera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply(false, false);
                File.WriteAllBytes(Path.Combine(extra07.output, name + ".png"), pixels.EncodeToPNG()); return pixels.GetPixels32();
            }
            try
            {
                // Independent GPU copy of the non-readable imported asset; no reimplementation of the production shader.
                Graphics.Blit(source, referenceTarget); RenderTexture.active = referenceTarget;
                referencePixels.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); referencePixels.Apply(false, false);
                File.WriteAllBytes(Path.Combine(extra07.output, "table-source-gpu-reference-linear.png"), referencePixels.EncodeToPNG());
                var expected = referencePixels.GetPixels32(); var clean = Capture("table-world-grain-off-linear");
                yield return MenuTablePreference071("Grain", "SettingsTab1", true); var grain = Capture("table-world-grain-on-linear");
                yield return MenuTablePreference071("Reduced", "SettingsTab2", true); var reducedPixels = Capture("table-world-reduced-linear");
                var offBlock = new MaterialPropertyBlock(); receiver.GetPropertyBlock(offBlock); offBlock.SetFloat("_UseTableMap", 0); receiver.SetPropertyBlock(offBlock);
                var disabled = Capture("table-world-disabled-control-linear");
                // Fit is derived from the authored image dimensions and available physical pixels, independently of shader UV code.
                float scale = Mathf.Min(result.width / (float)source.width, result.height / (float)source.height);
                Vector2 fitSize = new Vector2(source.width, source.height) * scale;
                result.fit = new Rect((new Vector2(result.width, result.height) - fitSize) / 2, fitSize);
                float low = 1, high = 0;
                for (int y = 0; y < 9; y++) for (int x = 0; x < 15; x++)
                {
                    var uv = new Vector2(.04f + x * .92f / 14, .04f + y * .92f / 8);
                    Vector2 screen = result.fit.min + Vector2.Scale(uv, result.fit.size);
                    Color reference = MenuTablePixel071(expected, source.width, source.height, Vector2.Scale(uv, new Vector2(source.width - 1, source.height - 1)));
                    Color actual = MenuTablePixel071(clean, result.width, result.height, screen);
                    Color flipped = MenuTablePixel071(expected, source.width, source.height, Vector2.Scale(new Vector2(uv.x, 1 - uv.y), new Vector2(source.width - 1, source.height - 1)));
                    var sample = new MenuTableGpuSample071 { sourceUV = uv, screenPixel = screen, reference = reference, actual = actual,
                        error = MenuTableError071(actual, reference), flippedError = MenuTableError071(actual, flipped) };
                    result.samples.Add(sample); result.meanReferenceError += sample.error; result.meanFlippedError += sample.flippedError;
                    result.meanGrainDifference += MenuTableError071(actual, MenuTablePixel071(grain, result.width, result.height, screen));
                    result.meanReducedDifference += MenuTableError071(actual, MenuTablePixel071(reducedPixels, result.width, result.height, screen));
                    result.meanDisabledDifference += MenuTableError071(actual, MenuTablePixel071(disabled, result.width, result.height, screen));
                    float luma = (reference.r + reference.g + reference.b) / 3; low = Mathf.Min(low, luma); high = Mathf.Max(high, luma);
                }
                int n = result.samples.Count; result.meanReferenceError /= n; result.meanFlippedError /= n; result.meanGrainDifference /= n;
                result.meanReducedDifference /= n; result.meanDisabledDifference /= n; result.referenceLumaRange = high - low;
                for (int y = 8; y < result.height; y += 16) for (int x = 8; x < result.width; x += 16)
                {
                    var p = new Vector2(x, y); Rect expanded = new Rect(result.fit.x - 4, result.fit.y - 4, result.fit.width + 8, result.fit.height + 8);
                    if (expanded.Contains(p)) continue;
                    Color c = MenuTablePixel071(clean, result.width, result.height, p); result.outsideSamples++;
                    result.outsideLumaMax = Mathf.Max(result.outsideLumaMax, (c.r + c.g + c.b) / 3);
                }
                File.WriteAllText(Path.Combine(extra07.output, "table-world-gpu.json"), JsonUtility.ToJson(result, true));
                Assert.That(result.referenceLumaRange, Is.GreaterThan(.03f), "The test source must contain visible structure, not a flat placeholder.");
                Assert.That(result.meanReferenceError, Is.LessThan(.025f), "World frame must match original authored direction and fit; see flipped-error diagnostic and actual PNGs.");
                Assert.That(result.meanDisabledDifference, Is.GreaterThan(.01f), "Disabling the authored map must visibly differ; property values alone do not prove rendering.");
                Assert.That(result.meanGrainDifference, Is.LessThan(.01f), "Grain may alter microdetail, not erase the table.");
                Assert.That(result.meanReducedDifference, Is.LessThan(.01f), "Reduced must retain the table composition.");
                if (result.fit.height < result.height - 8 || result.fit.width < result.width - 8)
                {
                    Assert.That(result.outsideSamples, Is.GreaterThan(0));
                    Assert.That(result.outsideLumaMax, Is.LessThan(.08f), "Extra aspect-ratio area is the dim room, not stretched or repeated table art.");
                }
            }
            finally
            {
                receiver.SetPropertyBlock(savedBlock); reveal.enabled = wasRevealEnabled; RenderTexture.active = oldActive;
                target.Release(); referenceTarget.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(referenceTarget);
                UnityEngine.Object.Destroy(pixels); UnityEngine.Object.Destroy(referencePixels); app.Refresh();
            }
            ExtraFinish07();
        }
    }
}
