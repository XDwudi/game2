using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        private static readonly MethodInfo WrapCard21 = typeof(GameApp).GetMethod("WrapLines21",
            BindingFlags.Static | BindingFlags.NonPublic);

        private static void CheckCardLines21(TextMeshProUGUI label, string source, string scope, List<string> failures)
        {
            label.ForceMeshUpdate();
            int supplied = label.text.Count(c => c == '\n') + 1;
            if (label.textInfo.lineCount != supplied)
                failures.Add(scope + ": TMP drew " + label.textInfo.lineCount + " lines for " + supplied + " supplied");
            string expected = (string)WrapCard21.Invoke(null,
                new object[] { label, source, label.rectTransform.rect.width, supplied });
            if (expected != label.text)
                failures.Add(scope + ": expected [" + expected + "], saw [" + label.text + "]");
        }

        [UnityTest]
        public IEnumerator PLAY_V21_FW12_AllStylesDrawCompleteCardLines()
        {
            var failures = new List<string>();
            var checkedStyles = new HashSet<string>();
            string previous = app.ThemeId11;
            try
            {
                StartRun("72112");
                foreach (var theme in ThemeRegistry.All)
                {
                    app.ApplyTheme11(theme.Id);
                    int controllerLabels = 0;
                    foreach (int percent in new[] { 100, 150 })
                    {
                        W11TextScale(percent);
                        for (int page = 0; page < GameApp.CardGalleryPages13; page++)
                        {
                            var gallery = app.ShowCardGallery13(page);
                            yield return null;
                            var order = app.canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(false);
                            foreach (var label in gallery.GetComponentsInChildren<TextMeshProUGUI>(false)
                                         .Where(t => t.name == "ShortText11" && t.gameObject.activeInHierarchy))
                            {
                                string where = theme.Id + "/" + percent + "/page" + page + "/" + label.transform.parent.name;
                                W13GlyphCheck(label, where, order, failures);
                                // Mini cards retain TMP wrapping; priced rows and text cards use the new solver.
                                var parent = label.transform.parent;
                                if (parent.GetComponent<RectTransform>().rect.height < 200 && parent.Find("PriceButton13") == null) continue;
                                var icon = parent.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("Icon_C", StringComparison.Ordinal));
                                if (icon == null) continue;
                                var configured = app.Config.Controller(icon.name.Substring(5));
                                CheckCardLines21(label, configured.shortText, where, failures);
                                controllerLabels++;
                            }
                        }
                        app.HideCardGallery13();
                    }
                    Assert.That(controllerLabels, Is.GreaterThan(0), theme.Id);
                    checkedStyles.Add(theme.Id);
                }
            }
            finally
            {
                app.HideCardGallery13();
                W11TextScale(100);
                app.ApplyTheme11(previous);
            }
            Assert.That(checkedStyles.Count, Is.EqualTo(6));
            Assert.That(failures, Is.Empty, string.Join("\n", failures.Take(25)));
        }
    }
}
