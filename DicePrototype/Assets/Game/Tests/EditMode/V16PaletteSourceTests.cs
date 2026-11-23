using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Tests
{
    public sealed class V16PaletteSourceTests
    {
        // SCREENS_LAYOUT §12.1 owners, relative to Assets/Game. Include future W16 UI partials below.
        static readonly string[] Sources = {
            "UI/GameApp.Art071Screens", "UI/GameApp.Art07Loading", "UI/GameApp.Art07", "UI/GameApp.Art07NewRun",
            "UI/GameApp.V10", "UI/GameApp.Screens16", "UI/GameApp.Play11", "UI/GameApp.V05", "Core/Rules/HandbookRules",
            "UI/GameApp.Art07Settings", "UI/GameApp.Theme11", "UI/Theme/GameTheme", "UI/GameApp.Display",
            "Runtime/DisplaySettings", "UI/GameApp.Modals", "UI/GameApp.Art07LegacySave", "Runtime/AtomicSaveStore",
            "UI/GameApp.Art07StatusDetails", "UI/GameApp.Art07Toasts", "UI/GameApp.Art07Guide", "UI/GameApp.Art07GuideDock",
            "UI/GameApp.Art073StyleLegend", "UI/GameApp.Art07Effects", "UI/GameApp.Shop", "UI/GameApp.Art07Presentation",
            "UI/GameApp.Art07ScoreDrawer", "UI/GameApp.Controllers", "Runtime/GameApp", "Core/Session/GameSession",
            "Core/Session/GameSession.Submission", "Editor/Art07AssetLibrary"
        };

        // Per-file ceilings from the pre-W16 sources (2fb2b08d). Repeating an old call consumes a new slot.
        // A removed constant can disappear. Neutral sprites and same-source RGB opacity masks remain supported.
        static readonly string[] Legacy = {
            "UI/GameApp.Art07NewRun Color(.015f,.03f,.035f,.35f)",
            "UI/GameApp.Modals Color(.015f,.03f,.035f,.35f)", "UI/GameApp.Modals Color(.015f,.03f,.035f,.35f)",
            "UI/GameApp.Modals Color(.015f,.03f,.035f,.3f)",
            "UI/GameApp.Play11 Color(1,1,1,.38f)", "UI/GameApp.Play11 Color(1,1,1,.5f)",
            "UI/GameApp.V05 Color(.08f,.18f,.18f,.12f)", "UI/GameApp.V05 Color(0,0,0,.04f)",
            "UI/GameApp.V05 Color(.42f,.25f,.10f)", "Editor/Art07AssetLibrary Hex(\"B1DBD7\")",
            "Editor/Art07AssetLibrary Color(0,0,0,.35f)", "Editor/Art07AssetLibrary Color(1,.72f,.3f,.35f)"
        };

        const string Shared = "background blue boss danger felt ghost gold ink muted onFelt panel panelLight paper pressure " +
            "primary rarityAdvanced rarityCommon rarityRare red reward shadow teal textColor violet";
        const string Steps = "buttonStep buttonStepPrimary caption section";
        const string AllKeys = Shared + " " + Steps + " buttonSecondary valueBadge";
        static readonly Dictionary<string, string> ThemeExtras = new Dictionary<string, string> {
            {"comic", Steps}, {"sancai", ""}, {"deco", Steps + " buttonSecondary"},
            {"swiss", "valueBadge"}, {"glass", Steps + " buttonSecondary"}
        };

        // Token boundaries keep comments, ordinary/verbatim strings and character literals out of the scan.
        // Preserve quoted Hex arguments and normalize only trivia, so locations and nested alpha calls survive.
        static readonly Regex Lexer = new Regex(
            @"//[^\r\n]*|(?s:/\*.*?\*/)|@""(?:""""|[^""])*""|""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'|" +
            @"[A-Za-z_]\w*|(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?[fFdDmM]?|\S");
        static readonly Regex AlphaOnly = new Regex(
            @"^(?<owner>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\.r,\k<owner>\.g,\k<owner>\.b,[^,].*$");
        static readonly HashSet<string> NamedColors = typeof(Color).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(Color) && p.Name != "white" && p.Name != "black" && p.Name != "clear")
            .Select(p => p.Name).ToHashSet();

        static List<string> SourceErrors(string file, string source)
        {
            var budget = Legacy.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
            var tokens = Lexer.Matches(source).Cast<Match>()
                .Where(m => !m.Value.StartsWith("//", StringComparison.Ordinal) && !m.Value.StartsWith("/*", StringComparison.Ordinal)).ToList();
            var errors = new List<string>();
            for (int i = 0; i < tokens.Count; i++)
            {
                string name = tokens[i].Value;
                string usage;
                if (name == "Color" && i + 2 < tokens.Count && tokens[i + 1].Value == "." && NamedColors.Contains(tokens[i + 2].Value))
                    usage = "Color." + tokens[i + 2].Value;
                else
                {
                    bool constructor = (name == "Color" || name == "Color32") && i > 0 &&
                        (tokens[i - 1].Value == "new" || (i >= 3 && tokens[i - 3].Value == "new" && tokens[i - 1].Value == "."));
                    bool parse = name == "Hex" || name == "TryParseHtmlString";
                    if (!(constructor || parse) || i + 1 >= tokens.Count || tokens[i + 1].Value != "(") continue;
                    int end = i + 2, depth = 1;
                    for (; end < tokens.Count && depth > 0; end++)
                    {
                        if (tokens[end].Value == "(") depth++;
                        if (tokens[end].Value == ")") depth--;
                    }
                    string args = string.Concat(tokens.Skip(i + 2).Take(end - i - 3).Select(m => m.Value));
                    if (name == "Color" && AlphaOnly.IsMatch(args)) continue;
                    usage = name + "(" + args + ")";
                }
                string key = file + " " + usage;
                if (budget.TryGetValue(key, out int remaining) && remaining > 0) budget[key]--;
                else errors.Add($"{file}.cs:{1 + source.Take(tokens[i].Index).Count(c => c == '\n')} {usage}");
            }
            return errors;
        }

        static IEnumerable<string> UnexpectedKeys(string json, string extra)
        {
            var allowed = (Shared + " " + extra).Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            using (var doc = JsonDocument.Parse(json))
            {
                var seen = new HashSet<string>();
                foreach (var key in doc.RootElement.GetProperty("palette").EnumerateObject())
                    if (!allowed.Contains(key.Name) || !seen.Add(key.Name)) yield return key.Name;
            }
        }

        [Test]
        public void EDIT_V16_SC14_SourceUsesPaletteOrExistingMasks()
        {
            CheckSources(Path.Combine(Application.dataPath, "Game"));
        }

        internal static void CheckSources(string root)
        {
            var inventory = new HashSet<string>(Sources);
            foreach (string path in Directory.GetFiles(Path.Combine(root, "UI"), "*16*.cs", SearchOption.AllDirectories))
                inventory.Add(Path.ChangeExtension(Path.GetRelativePath(root, path), null).Replace('\\', '/'));
            var errors = new List<string>();
            foreach (string file in inventory.OrderBy(x => x))
                errors.AddRange(SourceErrors(file, File.ReadAllText(Path.Combine(root, file + ".cs"))));
            Assert.That(errors, Is.Empty, string.Join("\n", errors));
        }

        [Test]
        public void EDIT_V16_SC14_DefaultAndFiveJsonPalettesUseExistingKeys()
        {
            CheckPalettes(Application.dataPath);
        }

        internal static void CheckPalettes(string assets)
        {
            var fields = BindingFlags.Instance | BindingFlags.Public;
            foreach (var type in new[] {typeof(ThemePalette), typeof(ThemePalette.Hexes)})
                Assert.That(type.GetFields(fields).Select(f => f.Name), Is.EquivalentTo(AllKeys.Split(' ')), type.FullName);
            foreach (var pair in ThemeExtras)
            {
                var path = Path.Combine(assets, "Resources", "Themes", pair.Key, "theme.json");
                Assert.That(UnexpectedKeys(File.ReadAllText(path), pair.Value), Is.Empty, pair.Key);
            }
        }

        [Test]
        public void EDIT_V16_SC14_RejectsNewColorsDuplicatesAndUnknownKeys()
        {
            const string file = "UI/GameApp.Modals";
            const string retained = "new Color(.015f,.03f,.035f,.35f);";
            Assert.That(SourceErrors(file, retained + retained), Is.Empty);
            Assert.That(SourceErrors(file, retained + retained + retained), Has.Count.EqualTo(1));
            Assert.That(SourceErrors("UI/GameApp.Screens16", retained), Has.Count.EqualTo(1));
            string examples = "// UiKit.Hex(\"FF0000\")\nvar word = \"new Color(1,0,0)\"; /* Color.magenta */ " +
                "new Color(UiKit.Paper.r,UiKit.Paper.g,UiKit.Paper.b,Mathf.Clamp01(a));";
            Assert.That(SourceErrors(file, examples), Is.Empty);
            Assert.That(SourceErrors(file, "new Color(UiKit.Paper.r, UiKit.Gold.g, UiKit.Paper.b, .5f); " +
                "UiKit.Hex(\"FF0000\"); new UnityEngine.Color32(1,2,3,255); Color.red;"), Has.Count.EqualTo(4));
            Assert.That(UnexpectedKeys("{\"id\":\"outerMetadata\",\"palette\":{\"ink\":\"a\",\"typo\":\"b\",\"ink\":\"c\"}}", ""),
                Is.EquivalentTo(new[] {"typo", "ink"}));
            Assert.That(UnexpectedKeys("{\"palette\":{\"buttonSecondary\":\"abc\"}}", ThemeExtras["sancai"]),
                Is.EquivalentTo(new[] {"buttonSecondary"}));
        }
    }
}
