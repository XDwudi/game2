using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        public IEnumerator PLAY_V21_FW18_StagePressureHintDrawsCompleteParentheses()
        {
            Assert.That(Screen.width == 1280 && Screen.height == 720 || Screen.width == 1920 && Screen.height == 1080);
            var originalTheme = app.ThemeId11;
            int originalScale = Mathf.RoundToInt(UiKit.FontScale * 100);
            bool originalClean = GameApp.CleanCapture11;
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/cache/v11/w21/pressure-hint",
                Screen.width + "x" + Screen.height));
            Directory.CreateDirectory(folder);
            try
            {
                GameApp.CleanCapture11 = true;
                StartRun("24016");
                foreach (string theme in new[] { "default", "swiss", "sancai", "comic", "deco", "glass" })
                {
                    app.ApplyTheme11(theme);
                    Assert.That(app.ThemeId11, Is.EqualTo(theme));
                    foreach (int percent in new[] { 100, 150 })
                    {
                        W11TextScale(percent);
                        string scope = theme + "-" + percent;
                        yield return RolloutShot18(Path.Combine(folder, scope));
                        AssertPressureHintLines16("到这一关时再选择。高压挑战：目标 ×1.5，过关奖励 10 金币（普通 5）。", scope);
                    }
                }
            }
            finally
            {
                GameApp.CleanCapture11 = originalClean;
                app.ApplyTheme11(originalTheme);
                W11TextScale(originalScale);
            }
        }

        private void AssertPressureHintLines16(string expected, string scope)
        {
            var label = FindActive<TextMeshProUGUI>("PressureHint");
            label.ForceMeshUpdate();
            Assert.That(label.GetParsedText().Replace("\n", ""), Is.EqualTo(expected), scope);
            Assert.That(label.isTextTruncated || label.isTextOverflowing, Is.False, scope);
            var lines = new List<string>();
            for (int row = 0; row < label.textInfo.lineCount; row++)
            {
                var line = label.textInfo.lineInfo[row];
                var ink = new List<char>();
                for (int i = line.firstCharacterIndex; i <= line.lastCharacterIndex; i++)
                {
                    var character = label.textInfo.characterInfo[i];
                    if (char.IsWhiteSpace(character.character)) continue;
                    Assert.That(character.isVisible, Is.True, scope + "/" + character.character);
                    ink.Add(character.character);
                }
                Assert.That(ink, Is.Not.Empty, scope + " empty rendered line");
                Assert.That(ink[ink.Count - 1], Is.Not.EqualTo('（'), scope + "/" + new string(ink.ToArray()));
                lines.Add(new string(ink.ToArray()));
            }
            TestContext.WriteLine(scope + " PressureHint: " + string.Join(" / ", lines));
        }

        [UnityTest]
        public IEnumerator PLAY_V16_StageSelectionAndChapterIntelSixStyles()
        {
            Assert.That((Screen.width == 1920 && Screen.height == 1080) || (Screen.width == 1280 && Screen.height == 720));
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "cache",
                "v11", "w16", "stages", Screen.width + "x" + Screen.height));
            Directory.CreateDirectory(output);
            var failures = new List<string>();
            var report = new List<string> { IdentityHead20X() + " | Unity " + Application.unityVersion };
            bool clean = GameApp.CleanCapture11;
            try
            {
                GameApp.CleanCapture11 = true;
                foreach (string theme in new[] { "default", "swiss", "sancai", "comic", "deco", "glass" })
                {
                    var shop = ShopAfterStage10(0);
                    shop.cycles[0].modifierId = "B25"; shop.cycles[0].lootKind = "L1";
                    yield return W14Load(shop);
                    app.ApplyTheme11(theme);
                    Assert.That(app.ThemeId11, Is.EqualTo(theme));
                    foreach (int percent in new[] { 100, 150 })
                    {
                        W11TextScale(percent);
                        Click("ViewCycle");
                        yield return W11Settle();
                        var dialog = FindActive<RectTransform>("Dialog");
                        Rect16(dialog, 150, percent == 150 ? 58 : 170, 1620, percent == 150 ? 964 : 740);
                        for (int i = 0; i < 3; i++) Rect16(dialog.Find("CycleCard" + i) as RectTransform,
                            86 + 488 * i, 96, 472, percent == 150 ? 592 : 452);
                        Assert.That(HasActive("Risk"), Is.False, "The intelligence dialog is read-only.");
                        Assert.That(FindActive<TextMeshProUGUI>("CycleHint").text, Does.Contain("每章的首领减益"));
                        yield return StageFrame16(theme + "-" + percent + "-intel", output, report, failures, dialog);
                        Click("CloseCycle");
                    }
                    Click("LeaveShop"); yield return WaitForIdle();
                    Assert.That(app.Session.State.phase, Is.EqualTo("StagePreview"));
                    foreach (int percent in new[] { 100, 150 })
                    {
                        W11TextScale(percent); yield return W11Settle();
                        var main = W14Main;
                        var strip = (RectTransform)main.Find("ChapterStrip16");
                        var entry = (RectTransform)main.Find("EntryNotes16");
                        var rules = (RectTransform)main.Find("PrepRules16");
                        var start = (RectTransform)main.Find("StartStage");
                        Rect16(rules, 1496, 96, 392, 244);
                        Rect16(strip, 32, 352, 1448, percent == 150 ? 104 : 84);
                        Rect16(start, 1496, 976, 392, 96);
                        Assert.That(-entry.anchoredPosition.y + entry.sizeDelta.y, Is.EqualTo(964).Within(.5));
                        Assert.That(entry.anchoredPosition.y, Is.LessThanOrEqualTo(-340));
                        Assert.That(Enumerable.Range(0, 24).All(i => strip.GetComponentsInChildren<TextMeshProUGUI>()
                            .Any(t => t.name == "StageTickLabel" + i)), Is.True);
                        Assert.That(Button("StartStage").transform.Find("Arrow").GetComponent<Image>().sprite.name, Is.EqualTo("ui_icon_arrow"));
                        Assert.That(start.Find("Label").GetComponent<TextMeshProUGUI>().GetParsedText(), Is.EqualTo("开始第 2 关"));
                        for (int i = 0; i < 3; i++) Rect16(main.Find("CycleCard" + i) as RectTransform, 32 + 488 * i,
                            percent == 150 ? 468 : 448, 472, percent == 150 ? 592 : 452);
                        AssertPressureHintLines16("高压挑战：目标 ×1.5，过关奖励 10 金币（普通 5）。", theme + "-" + percent);
                        Assert.That(CardText10("CycleCard2", "ModifierText"), Is.EqualTo(app.Config.Modifier("B25").shortText));
                        var sections = new List<Transform> { strip, entry, rules, start };
                        sections.AddRange(Enumerable.Range(0, 3).Select(i => main.Find("CycleCard" + i)));
                        var pluses = main.GetComponentsInChildren<TextMeshProUGUI>().Where(t => t.name == "Plus").ToArray();
                        foreach (var plus in pluses)
                        {
                            Assert.That(plus.transform.parent.name, Is.EqualTo("PlusDisc16"));
                            sections.Add(plus.transform.parent);
                        }
                        yield return StageFrame16(theme + "-" + percent + "-select", output, report, failures, sections.ToArray());
                        Click("Risk"); yield return null;
                        Assert.That(Button("Risk").transform.Find("Label").GetComponent<TextMeshProUGUI>().text, Is.EqualTo("✓ 已选择"));
                    }
                }
            }
            finally
            {
                W11TextScale(100); app.ApplyTheme11(GameTheme.DefaultId); GameApp.CleanCapture11 = clean;
                File.WriteAllLines(Path.Combine(output, "stages-report.txt"), report.Concat(failures));
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        private static void Rect16(RectTransform rect, float x, float y, float w, float h)
        {
            Assert.That(rect, Is.Not.Null);
            Assert.That(rect.anchoredPosition.x, Is.EqualTo(x).Within(.5), rect.name);
            Assert.That(-rect.anchoredPosition.y, Is.EqualTo(y).Within(.5), rect.name);
            Assert.That(rect.sizeDelta.x, Is.EqualTo(w).Within(.5), rect.name);
            Assert.That(rect.sizeDelta.y, Is.EqualTo(h).Within(.5), rect.name);
        }

        // Measure the visible tree itself. W11bContrastPass deliberately skips the entire modal layer.
        // Sample per glyph here so each rich-text warning colour is checked independently.
        private IEnumerator StageFrame16(string id, string output, List<string> report, List<string> failures, params Transform[] parts)
        {
            Canvas.ForceUpdateCanvases();
            var order = app.canvas.GetComponentsInChildren<Graphic>(false);
            var labels = parts.SelectMany(p => p.GetComponentsInChildren<TextMeshProUGUI>(false))
                .Where(t => t.enabled && t.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(t.text)).Distinct().ToArray();
            Assert.That(labels.Length, Is.GreaterThan(12), id);
            var glyphs = new Dictionary<TextMeshProUGUI, List<Rect>>();
            foreach (var text in labels) glyphs[text] = W13GlyphCheck(text, id, order, failures);
            yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                File.WriteAllBytes(Path.Combine(output, id + ".png"), image.EncodeToPNG());
                foreach (var text in labels) text.enabled = false;
                yield return new WaitForEndOfFrame();
                var blank = Screen20Xb();
                foreach (var text in labels) text.enabled = true;
                foreach (var text in labels)
                {
                    text.ForceMeshUpdate();
                    var chars = text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(c => c.isVisible).ToArray();
                    if (chars.Length != glyphs[text].Count || chars.Length == 0) { failures.Add(id + " incomplete glyphs " + text.name); continue; }
                    if (text.GetComponentInParent<Selectable>() is Selectable control && !control.interactable) continue;
                    float lowest = float.MaxValue;
                    for (int n = 0; n < chars.Length; n++)
                    {
                        var bounds = glyphs[text][n];
                        int x = Mathf.Clamp(Mathf.RoundToInt(bounds.center.x), 0, Screen.width - 1);
                        int y = Mathf.Clamp(Mathf.RoundToInt(bounds.center.y), 0, Screen.height - 1);
                        lowest = Mathf.Min(lowest, GameApp.Contrast11(chars[n].color, blank[y * Screen.width + x]));
                    }
                    report.Add($"{id} {W11bPath(text.transform)} | {chars.Length} glyphs | min contrast {lowest:F2}");
                    if (lowest < GameApp.MinContrast11) failures.Add(report.Last());
                }
            }
            finally { foreach (var text in labels) text.enabled = true; UnityEngine.Object.Destroy(image); }
        }
    }
}
