using System.IO;
using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Presentation.Tests
{
    /// <summary>
    /// V1.1 W14 SH-07 (docs/specs/v11/SHOP_LAYOUT.md 5.1, 5.2): dice are named by their traits, never "骰子 A / B". The
    /// examples of 5.2, both tie rules (and the completion pending confirmation), and the thresholds read from the
    /// configuration's new-die pricing (generation).
    /// </summary>
    public sealed class ShopDieTitleTests
    {
        private GameConfig cfg;
        [SetUp] public void Setup() { cfg = JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(File.ReadAllText(Path.Combine(Application.dataPath, "Game/Data/P0Config.json")))); cfg.Validate(); }

        /// <summary>A die from six pips and a colour string ("RBRBRB"); 'G' marks a gold face (red), 'g' a gold face (blue).</summary>
        private static DieInstance Die(int[] pips, string colours, bool glass = false)
        {
            var faces = pips.Select((p, i) => new FaceState { faceId = "F0" + (i + 1), permanentPip = p, baseColor = colours[i] == 'R' || colours[i] == 'G' ? "red" : "blue", isGold = colours[i] == 'G' || colours[i] == 'g' }).ToArray();
            return new DieInstance { dieId = "", faces = faces, isGlass = glass, lifetime = "permanent" };
        }

        [Test] public void SH07_TheExamplesOfSection52()
        {
            Assert.That(cfg.generation.highSumThreshold, Is.EqualTo(24)); Assert.That(cfg.generation.smallSumThreshold, Is.EqualTo(13)); Assert.That(cfg.generation.maxRepeatThreshold, Is.EqualTo(3));
            // A4 screenshot die A: sum 19, three red, three blue.
            var a = GameApp.DieTitle14(Die(new[] { 1, 2, 3, 4, 5, 4 }, "RBRBRB"), cfg.generation);
            Assert.That(a.title, Is.EqualTo("红蓝各半")); Assert.That(a.subtitle, Is.EqualTo("点数和 19"));
            // A4 screenshot die B: sum 10, two red, four blue.
            var b = GameApp.DieTitle14(Die(new[] { 1, 1, 2, 2, 2, 2 }, "RRBBBB"), cfg.generation);
            Assert.That(b.title, Is.EqualTo("蓝面多 · 小点")); Assert.That(b.subtitle, Is.EqualTo("点数和 10"));
            // High template (4-6), one red face.
            Assert.That(GameApp.DieTitle14(Die(new[] { 4, 5, 6, 6, 5, 4 }, "RBBBBB"), cfg.generation).title, Is.EqualTo("蓝五面 · 大点"));
            // Low template (1-3), four red faces: the repeat comes after colour and size.
            var low = Die(new[] { 1, 1, 1, 2, 3, 2 }, "RRRRBB");
            Assert.That(GameApp.DieTitle14(low, cfg.generation).title, Is.EqualTo("红面多 · 小点"));
            Assert.That(GameApp.DieWords14(low, cfg.generation, out string repeat), Is.EqualTo(new[] { "红面多", "小点", "三个 1" })); Assert.That(repeat, Is.EqualTo("三个 1"));
            // A loot special die (W15): one gold face leads.
            Assert.That(GameApp.DieTitle14(Die(new[] { 3, 4, 5, 2, 3, 4 }, "GRRBBB"), cfg.generation).title, Is.EqualTo("金面 1 · 红蓝各半"));
            Assert.That(GameApp.DieTitle14(Die(new[] { 1, 2, 3, 4, 5, 6 }, "RRRBBB", true), cfg.generation).title, Is.EqualTo("玻璃 · 红蓝各半"));
        }

        [Test] public void SH07_ColourWordsCoverEveryRedCount()
        {
            string[] expected = { "全蓝", "蓝五面", "蓝面多", "红蓝各半", "红面多", "红五面", "全红" };
            for (int red = 0; red <= 6; red++)
            {
                string colours = new string('R', red) + new string('B', 6 - red);
                Assert.That(GameApp.DieWords14(Die(new[] { 1, 2, 3, 4, 5, 6 }, colours), cfg.generation, out _).First(), Is.EqualTo(expected[red]), red + " red faces");
            }
            // The larger group of equal pips wins; on a tie, the higher pip.
            Assert.That(GameApp.DieWords14(Die(new[] { 2, 2, 2, 5, 5, 5 }, "RRRBBB"), cfg.generation, out string tie).Last(), Is.EqualTo("三个 5")); Assert.That(tie, Is.EqualTo("三个 5"));
            Assert.That(GameApp.DieWords14(Die(new[] { 6, 6, 6, 6, 6, 6 }, "BBBBBB"), cfg.generation, out _), Is.EqualTo(new[] { "全蓝", "大点", "六个 6" }));
        }

        [Test] public void SH07_TiesAddTheNextUnusedWordThenTagIdenticalDiceAsTheSame()
        {
            // Same title "红面多 · 小点"; their repeats differ, so the next unused word tells them apart.
            var names = GameApp.DieTitles14(new[] { Die(new[] { 1, 1, 1, 2, 3, 2 }, "RRRRBB"), Die(new[] { 2, 2, 2, 1, 3, 1 }, "RRRRBB") }, cfg.generation);
            Assert.That(names.Select(n => n.title), Is.EqualTo(new[] { "红面多 · 小点 · 三个 1", "红面多 · 小点 · 三个 2" }));
            Assert.That(names.All(n => !n.same), Is.True);
            // No repeat: the pip sum ("和 N") is next.
            names = GameApp.DieTitles14(new[] { Die(new[] { 1, 2, 3, 4, 5, 4 }, "RBRBRB"), Die(new[] { 1, 2, 3, 4, 5, 6 }, "RBRBRB") }, cfg.generation);
            Assert.That(names.Select(n => n.title), Is.EqualTo(new[] { "红蓝各半 · 和 19", "红蓝各半 · 和 21" }));
            // The same six faces in another order: equal, and the second is tagged 同款.
            names = GameApp.DieTitles14(new[] { Die(new[] { 1, 2, 3, 4, 5, 6 }, "RBRBRB"), Die(new[] { 6, 5, 4, 3, 2, 1 }, "BRBRBR") }, cfg.generation);
            Assert.That(names[0].title, Is.EqualTo(names[1].title)); Assert.That(names[0].same, Is.False); Assert.That(names[1].same, Is.True);
            Assert.That(names.Select(n => n.subtitle), Is.EqualTo(new[] { "点数和 21", "点数和 21" }));
            // Distinct titles stay as they are.
            names = GameApp.DieTitles14(new[] { Die(new[] { 1, 2, 3, 4, 5, 4 }, "RBRBRB"), Die(new[] { 1, 1, 2, 2, 2, 2 }, "RRBBBB") }, cfg.generation);
            Assert.That(names.Select(n => n.title), Is.EqualTo(new[] { "红蓝各半", "蓝面多 · 小点" }));
        }

        [Test] public void SH07_DifferentDiceThatStillShareATitleAddTheirRedSumThenListTheirPips()
        {
            // Same red count, same sum, no repeat, different faces: the red faces' sums tell them apart.
            var names = GameApp.DieTitles14(new[] { Die(new[] { 6, 1, 3, 4, 5, 2 }, "RRRBBB"), Die(new[] { 5, 2, 3, 4, 1, 6 }, "RRRBBB") }, cfg.generation);
            Assert.That(names.Select(n => n.title), Is.EqualTo(new[] { "红蓝各半 · 和 21 · 红 10", "红蓝各半 · 和 21 · 红 10" }));
            Assert.That(names.All(n => !n.same), Is.True, "Different faces are never 同款.");
            // Equal red sums too: the subtitles list the pips by colour.
            Assert.That(names[0].subtitle, Is.EqualTo("红 6 3 1 · 蓝 5 4 2")); Assert.That(names[1].subtitle, Is.EqualTo("红 5 3 2 · 蓝 6 4 1"));
            Assert.That(names[0].subtitle, Is.Not.EqualTo(names[1].subtitle));
            names = GameApp.DieTitles14(new[] { Die(new[] { 6, 1, 3, 4, 5, 2 }, "RRRBBB"), Die(new[] { 5, 1, 3, 4, 6, 2 }, "RRRBBB") }, cfg.generation);
            Assert.That(names.Select(n => n.title), Is.EqualTo(new[] { "红蓝各半 · 和 21 · 红 10", "红蓝各半 · 和 21 · 红 9" }));
            Assert.That(names.Select(n => n.subtitle), Is.EqualTo(new[] { "点数和 21", "点数和 21" }));
        }

        [Test] public void SH07_ThresholdsComeFromTheConfiguration()
        {
            var die = Die(new[] { 3, 3, 4, 4, 5, 5 }, "RBRBRB"); // sum 24, two of each
            Assert.That(GameApp.DieTitle14(die, cfg.generation).title, Is.EqualTo("红蓝各半 · 大点"));
            var g = JsonUtility.FromJson<GenerationConfig>(JsonUtility.ToJson(cfg.generation));
            g.highSumThreshold = 25; g.smallSumThreshold = 24; g.maxRepeatThreshold = 2;
            Assert.That(GameApp.DieWords14(die, g, out string repeat), Is.EqualTo(new[] { "红蓝各半", "小点", "两个 5" }));
            Assert.That(repeat, Is.EqualTo("两个 5"));
        }
    }
}
