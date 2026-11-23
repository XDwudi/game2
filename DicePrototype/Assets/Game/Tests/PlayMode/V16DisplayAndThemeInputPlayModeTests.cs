using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [UnityTest]
        public IEnumerator PLAY_V16_ThemeKeyboardSelectApplyAndReturnKeepSessionAndSave()
        {
            var oldTheme = app.ThemeId11;
            bool hadTheme = PlayerPrefs.HasKey(ThemeRegistry.PreferenceKey), hadPreview = PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);
            string preference = PlayerPrefs.GetString(ThemeRegistry.PreferenceKey);
            int preview = PlayerPrefs.GetInt(ThemeRegistry.PreviewKey);
            try
            {
                PlayerPrefs.SetInt(ThemeRegistry.PreviewKey, 1);
                app.ApplyTheme11(GameTheme.DefaultId);
                StartRun("71603");
                Click("StartStage");
                Click("Roll");
                app.SkipVisual();
                yield return WaitForIdle();
                var state = StateJson();
                var save = File.ReadAllBytes(app.SaveLocation);
                var sequence = app.Session.ActionSequence;
                Click("Pause"); Click("Settings"); Click("SettingsTab1"); Click("Theme");
                var options = ThemeRegistry.Selectable(true);
                Assert.That(options.Count, Is.EqualTo(6));
                Assert.That(options[0].Id, Is.EqualTo(GameTheme.DefaultId));
                string first = "ThemeTile16_" + options[0].Id, next = "ThemeTile16_" + options[1].Id;
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Button(first).gameObject));
                using (var keyboard = new RoutedKeyboard())
                {
                    yield return keyboard.Press(Key.RightArrow);
                    Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Button(next).gameObject));
                    Assert.That(app.ThemeId11, Is.EqualTo(GameTheme.DefaultId));
                    yield return keyboard.Press(Key.Enter);
                    Assert.That(app.ThemeId11, Is.EqualTo(options[1].Id));
                    Assert.That(PlayerPrefs.GetString(ThemeRegistry.PreferenceKey), Is.EqualTo(options[1].Id));
                    Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Button(next).gameObject));
                    Assert.That(InformationButton07(next), Is.EqualTo("✓ " + options[1].DisplayName));
                    Assert.That(Button(next).transform.Find("CurrentTheme16").gameObject.activeInHierarchy);
                    for (int i = 0; i < options.Count + 2 && EventSystem.current.currentSelectedGameObject != Button("ThemeDone16").gameObject; i++)
                        yield return keyboard.Press(Key.Tab);
                    Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Button("ThemeDone16").gameObject));
                    yield return keyboard.Press(Key.Enter);
                    Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Button("Theme").gameObject));
                    Assert.That(HasActive("ThemeDone16"), Is.False);
                    Click("Theme");
                    yield return keyboard.Press(Key.Escape);
                    Assert.That(HasActive("Dialog") || HasActive("ThemeDone16"), Is.False);
                }
                Assert.That(app.Session.ActionSequence, Is.EqualTo(sequence));
                Assert.That(StateJson(), Is.EqualTo(state));
                CollectionAssert.AreEqual(save, File.ReadAllBytes(app.SaveLocation));
            }
            finally
            {
                app.ApplyTheme11(oldTheme);
                if (hadTheme) PlayerPrefs.SetString(ThemeRegistry.PreferenceKey, preference); else PlayerPrefs.DeleteKey(ThemeRegistry.PreferenceKey);
                if (hadPreview) PlayerPrefs.SetInt(ThemeRegistry.PreviewKey, preview); else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                PlayerPrefs.Save();
            }
        }

        [UnityTest]
        public IEnumerator PLAY_V16_DisplayChoicesRemainDistinctFromOnlyPrimaryApply()
        {
            var oldTheme = app.ThemeId11;
            int oldScale = Mathf.RoundToInt(UiKit.FontScale * 100);
            try
            {
                foreach (string theme in new[] { "default", "swiss", "sancai", "comic", "deco", "glass" })
                foreach (int scale in new[] { 100, 150 })
                {
                    app.ApplyTheme11(theme);
                    W11TextScale(scale);
                    Click("MenuSettings"); Click("SettingsTab1"); Click("DisplaySettings");
                    Click("Mode1");
                    AssertDisplaySelected16("Mode1", null);
                    Click("Mode2");
                    AssertDisplaySelected16("Mode2", null);
                    var sizes = FindActive<RectTransform>("Dialog").GetComponentsInChildren<Button>(false)
                        .Where(b => b.name.StartsWith("Resolution", StringComparison.Ordinal)).ToArray();
                    Assert.That(sizes, Is.Not.Empty);
                    foreach (var size in new[] { sizes[0].name, sizes[sizes.Length - 1].name }.Distinct())
                    {
                        Click(size);
                        AssertDisplaySelected16("Mode2", size);
                    }
                    Assert.That(app.DisplayPreferences.IsPending, Is.False);
                    Click("CloseDialog");
                    yield return null;
                }
            }
            finally { app.ApplyTheme11(oldTheme); W11TextScale(oldScale); }
        }

        private void AssertDisplaySelected16(string mode, string resolution)
        {
            var buttons = FindActive<RectTransform>("Dialog").GetComponentsInChildren<Button>(false);
            var primary = buttons.Where(b => b.IsInteractable() && W11PrimaryColoured(b.targetGraphic.color));
            CollectionAssert.AreEqual(new[] { "ApplyDisplay" }, primary.Select(b => b.name));
            var choices = buttons.Where(b => b.name.StartsWith("Mode", StringComparison.Ordinal)
                || b.name.StartsWith("Resolution", StringComparison.Ordinal)).ToArray();
            var marked = choices.Where(b =>
            {
                var mark = b.targetGraphic.transform.Find("SelectedDisplayChoice16");
                return mark != null && mark.gameObject.activeInHierarchy;
            }).ToArray();
            Assert.That(marked.Select(b => b.name), Does.Contain(mode));
            Assert.That(marked.Count(b => b.name.StartsWith("Mode", StringComparison.Ordinal)), Is.EqualTo(1));
            Assert.That(marked.Count(b => b.name.StartsWith("Resolution", StringComparison.Ordinal)), Is.EqualTo(1));
            if (resolution != null) Assert.That(marked.Select(b => b.name), Does.Contain(resolution));
            foreach (var b in marked)
            {
                var indicator = b.targetGraphic.transform.Find("SelectedDisplayChoice16").GetComponent<Image>();
                Assert.That(indicator.raycastTarget, Is.False);
                Assert.That(indicator.color.a, Is.EqualTo(1).Within(.01));
                Assert.That(indicator.rectTransform.rect.width, Is.GreaterThan(0));
                Assert.That(indicator.rectTransform.rect.height, Is.GreaterThan(0));
            }
        }
    }
}
