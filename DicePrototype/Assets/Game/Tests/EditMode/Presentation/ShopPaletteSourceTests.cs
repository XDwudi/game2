using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Presentation.Tests
{
    /// <summary>
    /// V1.1 W14 SH-09 (docs/specs/v11/SHOP_LAYOUT.md 10): the shop's files take every colour from the active style's
    /// palette. No UiKit.Hex, no Color32 and no new Color with constant components; the only constructor allowed is a palette
    /// colour at another opacity (new Color(c.r, c.g, c.b, alpha)). The named Color.white and Color.clear stay allowed: they
    /// are a sprite's neutral tint and an invisible hit area, not colours of the style.
    /// </summary>
    public sealed class ShopPaletteSourceTests
    {
        private static readonly string[] ShopFiles = { "GameApp.Shop14.cs", "GameApp.ShopFacePick14.cs", "GameApp.Art073Shop.cs", "GameApp.Art07Shop.cs", "GameApp.Shop.cs" };

        [Test] public void SH09_ShopFilesUseOnlyThePalette()
        {
            var constructor = new Regex(@"new\s+Color\s*\(([^()]*(?:\([^()]*\)[^()]*)*)\)");
            var alphaMask = new Regex(@"^\s*([A-Za-z_][\w\.]*)\.r\s*,\s*\1\.g\s*,\s*\1\.b\s*,");
            var problems = new System.Collections.Generic.List<string>();int files = 0, masks = 0;
            foreach (string name in ShopFiles)
            {
                string path = Path.Combine(Application.dataPath, "Game", "UI", name);
                Assert.That(File.Exists(path), Is.True, path);files++;
                var lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = lines[i];int comment = code.IndexOf("//", System.StringComparison.Ordinal);if (comment >= 0) code = code.Substring(0, comment);
                    if (Regex.IsMatch(code, @"\bHex\s*\(")) problems.Add(name + ":" + (i + 1) + " UiKit.Hex: " + lines[i].Trim());
                    if (Regex.IsMatch(code, @"\bnew\s+Color32\s*\(")) problems.Add(name + ":" + (i + 1) + " Color32: " + lines[i].Trim());
                    foreach (Match m in constructor.Matches(code))
                    {
                        if (alphaMask.IsMatch(m.Groups[1].Value)) { masks++; continue; }
                        problems.Add(name + ":" + (i + 1) + " constant colour: " + m.Value);
                    }
                }
            }
            TestContext.WriteLine("SH-09: " + files + " files, " + masks + " palette alpha masks, " + problems.Count + " fixed colours");
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }
    }
}
