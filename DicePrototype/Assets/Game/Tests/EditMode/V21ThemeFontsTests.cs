using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed class V21ThemeFontsTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void W21_FontsAbsentOrMissingPreserveTheBaseLabel(bool missing)
        {
            const string id = "w21-font-fallback-probe";
            string fonts = missing ? ",\"fonts\":{\"title\":\"Fonts/not-installed\"}" : "";
            var file = new TextAsset("{\"id\":\"" + id + "\"" + fonts + "}");
            var labelObject = new GameObject("FontFallback", typeof(RectTransform));
            string previousTheme = ThemeRegistry.Current?.Id;
            try
            {
                var parse = typeof(GameTheme).GetMethod("FromFile", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(parse, Is.Not.Null);
                var theme = (GameTheme)parse.Invoke(null, new object[] {file});
                ThemeRegistry.RegisterForTests(theme);
                int warnings = 0;
                void CountWarning(string message, string stack, LogType kind)
                {
                    if (kind == LogType.Warning && message.Contains(id)) warnings++;
                }
                Application.logMessageReceived += CountWarning;
                try
                {
                    if (missing) LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape(id + "/Fonts/not-installed")));
                    ThemeRegistry.Activate(id);
                    theme.EnsureLoaded();
                    ThemeRegistry.Activate(id);
                }
                finally { Application.logMessageReceived -= CountWarning; }
                Assert.That(warnings, Is.EqualTo(missing ? 1 : 0));
                Assert.That(theme.TitleFont, Is.Null);
                Assert.That(theme.TitleRoles, Is.EqualTo(TextRole.None));

                var label = labelObject.AddComponent<TextMeshProUGUI>();
                var baseline = label.font;
                label.fontStyle = FontStyles.Bold;
                foreach (TextRole role in System.Enum.GetValues(typeof(TextRole)))
                {
                    Assert.That(UiKit.Role(label, role), Is.False, role.ToString());
                    Assert.That(label.font, Is.SameAs(baseline));
                    Assert.That(label.fontStyle, Is.EqualTo(FontStyles.Bold));
                }
            }
            finally
            {
                if (previousTheme == null) ThemeRegistry.ResetForTests();
                else
                {
                    ThemeRegistry.Activate(previousTheme);
                    ThemeRegistry.UnregisterForTests(id);
                }
                Object.DestroyImmediate(labelObject);
                Object.DestroyImmediate(file);
            }
        }
    }
}
