using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [UnityTest]
        public IEnumerator PLAY_V25_InstalledBossEmblemsOnFourRealScreens()
        {
            string[] themes = { "default", "swiss", "sancai", "comic", "deco", "glass" };
            var cases = new[] { ("B05", "seal"), ("B27", "constraint"), ("B08", "multiplier"),
                ("B25", "cards"), ("B12", "economy"), ("B11", "target"), ("B38", "final") };
            var logs = new List<string>();
            void OnLog(string message, string trace, LogType type)
            {
                if (message.StartsWith("W15_MISSING_ART ", StringComparison.Ordinal)
                    && (message.Contains("/emblem_") || message.Contains("/ui_emblem_tier"))) logs.Add(message);
            }
            Assert.That(Screen.width == 1920 && Screen.height == 1080 || Screen.width == 1280 && Screen.height == 720);
            string initialTheme = app.ThemeId11;
            Application.logMessageReceived += OnLog;
            try
            {
                StartRun("20261122");
                int observations = 0;
                foreach (string theme in themes)
                {
                    app.ApplyTheme11(theme);
                    foreach (var (modifier, family) in cases)
                    {
                        var scene = W11Scene(Array.Empty<string>(), new[] { 2, 4, 6 }, "rbr", 0, false,
                            stageIndex: family == "final" ? 23 : 2, modifier: modifier);
                        W11Apply(scene);
                        Assert.That(app.Session.State.stage.modifierId, Is.EqualTo(modifier));
                        foreach (int percent in new[] { 100, 150 })
                        {
                            W11TextScale(percent);
                            AssertEmblemDrawn25(theme, family, modifier, "BossEmblem11", percent == 100 ? 72 : 60);
                            AssertBossReadoutVisible10(modifier);
                            AssertEmblemLabels25("BossModifierName", "BossModifierText", "BossLoot11");
                            observations++;
                        }
                    }
                }
                // Submit a real final hand and continue its receipt before checking the winning screen.
                yield return TerminalFixture15("win");
                if (HasActive("SkipResultReveal")) Click("SkipResultReveal");
                foreach (string theme in themes)
                {
                    app.ApplyTheme11(theme);
                    foreach (int percent in new[] { 100, 150 })
                    {
                        W11TextScale(percent);
                        AssertEmblemDrawn25(theme, "final", "B38", "BossEmblem15", 176);
                        AssertEmblemLabels25("EndMessage");
                        observations++;
                    }
                }
                // The shop shows the *next* boss. The settlement uses the boss just defeated.
                var shop = ShopAfterStage10(1);
                shop.cycles[0].modifierId = "B12";
                yield return W14Load(shop);
                foreach (string theme in themes)
                {
                    app.ApplyTheme11(theme);
                    foreach (int percent in new[] { 100, 150 })
                    {
                        W11TextScale(percent);
                        AssertEmblemDrawn25(theme, "economy", "B12", "BossMark14", 22);
                        Assert.That(Text10("NextStage14"), Does.Contain("首领"));
                        AssertEmblemLabels25("NextStage14");
                        observations++;
                    }
                }
                yield return LoadBoundaryFixture(BeforeBossLoot15("L1"));
                ResumeBoundaryFixture();
                yield return WaitForIdle();
                Assert.That(app.Session.State.phase, Is.EqualTo("StageSettlement"));
                foreach (string theme in themes)
                {
                    app.ApplyTheme11(theme);
                    foreach (int percent in new[] { 100, 150 })
                    {
                        W11TextScale(percent);
                        AssertEmblemDrawn25(theme, "target", "B11", "BossEmblem15", 176);
                        Assert.That(Text10("BossName15"), Is.EqualTo(app.Config.Modifier("B11").displayName));
                        AssertEmblemLabels25("BossName15");
                        observations++;
                    }
                }
                Assert.That(observations, Is.EqualTo(120));
                Assert.That(logs, Is.Empty, string.Join("\n", logs));
                TestContext.WriteLine($"{IdentityHead20X()} Unity {Application.unityVersion} {Screen.width}x{Screen.height} "
                    + $"{observations} live emblems; no default substitution, procedural placeholder or missing frame");
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                app.ApplyTheme11(initialTheme);
                W11TextScale(100);
            }
        }

        private void AssertEmblemDrawn25(string theme, string family, string modifier, string rootName, int size)
        {
            string context = $"{theme}/{family}/{modifier}/{rootName}/{size}";
            Assert.That(app.ThemeId11, Is.EqualTo(theme), context);
            string key = "emblem_" + family;
            var expected = theme == "default" ? app.defaultBossEmblems15.SingleOrDefault(s => s != null && s.name == key)
                : Resources.Load<Sprite>($"Themes/{theme}/Art/{key}");
            Assert.That(expected, Is.Not.Null, context + " authored sprite");
            var root = FindActive<RectTransform>(rootName);
            var face = root.Find("BossFamily15")?.GetComponent<Image>();
            Assert.That(face, Is.Not.Null, context);
            Assert.That(face.isActiveAndEnabled && face.color == Color.white && face.preserveAspect, Is.True, context);
            Assert.That(face.sprite, Is.SameAs(expected), context + " actual image");
            Assert.That(root.rect.size, Is.EqualTo(new Vector2(size, size)), context + " outer box");
            float inner = size == 22 ? size : size * 144f / 176f;
            Assert.That(face.rectTransform.rect.width, Is.EqualTo(inner).Within(.02f), context);
            Assert.That(face.rectTransform.rect.height, Is.EqualTo(inner).Within(.02f), context);
            var placeholder = root.GetComponentsInChildren<RectTransform>(false)
                .Where(r => r.name.StartsWith("MissingFamilyTier", StringComparison.Ordinal) || r.name == "TierFrameFallback15");
            Assert.That(placeholder, Is.Empty, context);
            var frame = root.Find("TierFrame15")?.GetComponent<Image>();
            if (size == 22) Assert.That(frame, Is.Null, context + " frame-free shop mark");
            else
            {
                string tierKey = "ui_emblem_tier" + app.Config.Modifier(modifier).tier;
                Assert.That(frame, Is.Not.Null, context);
                Assert.That(frame.isActiveAndEnabled && frame.sprite != null, Is.True, context);
                Assert.That(frame.sprite.name, Is.EqualTo(tierKey), context);
            }
        }

        private void AssertEmblemLabels25(params string[] names)
        {
            foreach (string name in names)
            {
                var text = FindActive<TextMeshProUGUI>(name);
                text.ForceMeshUpdate();
                string value = text.text;
                Assert.That(value, Is.Not.Empty, name);
                Assert.That(text.GetParsedText(), Is.EqualTo(value), name + " full source text");
                Assert.That(text.textInfo.characterCount, Is.EqualTo(value.Length), name);
                for (int i = 0; i < value.Length; i++)
                {
                    if (char.IsWhiteSpace(value[i])) continue;
                    var glyph = text.textInfo.characterInfo[i];
                    Assert.That(glyph.isVisible, Is.True, name + "/" + value[i]);
                    Assert.That(glyph.textElement.unicode, Is.EqualTo((uint)value[i]), name + "/" + value[i]);
                    Assert.That(glyph.pointSize, Is.GreaterThanOrEqualTo(19.49f), name + "/" + value[i]);
                }
            }
        }
    }
}
