using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Presentation.Tests
{
    /// <summary>
    /// V1.1 task W20X-01 (docs/specs/v11/THEME_SPEC.md §2): the palette keys buttonStep / buttonStepPrimary, section and
    /// caption. They parse from theme.json like every other key; a theme that sets none of them gets exactly the colours
    /// drawn before W20X; unknown keys are still ignored. W20X-02 (§3): every shipped Table/page_scene.png is imported with
    /// the settings of its table_scene.png.
    /// </summary>
    public sealed class ThemeInterfaceW20XTests
    {
        private static ThemePalette Parse(string json)=>ThemePalette.From(JsonUtility.FromJson<ThemePalette.Hexes>(json));
        private static string Hex(Color c)=>ColorUtility.ToHtmlStringRGBA(c);

        [TearDown] public void RestoreDefaultPalette(){UiKit.Palette=null;}

        [Test] public void W20X01_NewKeysParseLikeEveryOtherKey()
        {
            var p=Parse("{\"buttonStep\":\"0A080A\",\"buttonStepPrimary\":\"#123456\",\"section\":\"FFFFFF\",\"caption\":\"FBDC0F80\"}");
            Assert.That(Hex(p.buttonStep),Is.EqualTo("0A080AFF"));
            Assert.That(Hex(p.buttonStepPrimary),Is.EqualTo("123456FF"),"A leading # is accepted.");
            Assert.That(Hex(p.section),Is.EqualTo("FFFFFFFF"));
            Assert.That(Hex(p.caption),Is.EqualTo("FBDC0F80"),"Eight digits carry an alpha.");
            UiKit.Palette=p;
            Assert.That(UiKit.ButtonStep,Is.EqualTo(p.buttonStep));Assert.That(UiKit.ButtonStepPrimary,Is.EqualTo(p.buttonStepPrimary));
            Assert.That(UiKit.Section(Color.magenta),Is.EqualTo(p.section),"A set section replaces the fallback.");
            Assert.That(UiKit.Caption(Color.magenta),Is.EqualTo(p.caption),"A set caption replaces the fallback.");
        }

        [Test] public void W20X01_WithoutTheKeysEverythingIsDrawnAsBefore()
        {
            // The step colours UiKit.Button wrote as constants until W20X, parsed the same way as then (UiKit.Hex).
            var p=new ThemePalette();
            Assert.That(p.buttonStep,Is.EqualTo(UiKit.Hex("8F453A")));Assert.That(p.buttonStepPrimary,Is.EqualTo(UiKit.Hex("B56B29")));
            Assert.That(p.section.a,Is.Zero,"section is unset by default");Assert.That(p.caption.a,Is.Zero,"caption is unset by default");
            foreach(var palette in new[]{new ThemePalette(),Parse("{}"),Parse("{\"panel\":\"151618\",\"panelLight\":\"4E4E4E\"}")})
            {
                UiKit.Palette=palette;
                Assert.That(UiKit.ButtonStep,Is.EqualTo(UiKit.Hex("8F453A")));Assert.That(UiKit.ButtonStepPrimary,Is.EqualTo(UiKit.Hex("B56B29")));
                // The shop's section panel took Panel, its header strip PanelLight, the pool tab Panel, the dice-style cells no tint.
                Assert.That(UiKit.Section(UiKit.Panel),Is.EqualTo(palette.panel));
                Assert.That(UiKit.Caption(UiKit.PanelLight),Is.EqualTo(palette.panelLight));
                Assert.That(UiKit.Caption(UiKit.Panel),Is.EqualTo(palette.panel));
                Assert.That(UiKit.Section(Color.white),Is.EqualTo(Color.white));
            }
        }

        [Test] public void W20X01_UnknownAndInvalidValuesAreIgnored()
        {
            var p=Parse("{\"panel\":\"010203\",\"buttonStepColour\":\"FF0000\",\"sectionTint\":\"00FF00\",\"captions\":\"0000FF\",\"section\":\"GGGGGG\",\"caption\":\"12345\"}");
            Assert.That(Hex(p.panel),Is.EqualTo("010203FF"),"Known keys still parse next to unknown ones.");
            var d=new ThemePalette();
            Assert.That(p.buttonStep,Is.EqualTo(d.buttonStep));Assert.That(p.buttonStepPrimary,Is.EqualTo(d.buttonStepPrimary));
            Assert.That(p.section.a,Is.Zero,"An invalid section keeps the default (unset).");Assert.That(p.caption.a,Is.Zero,"An invalid caption keeps the default (unset).");
        }

        [Test] public void W20X01_StylesWithoutTheKeysSetNoneOfThem()
        {
            // The pixel-identity premise of W20X-05: default, swiss and sancai set none of the four keys.
            foreach(string id in new[]{"swiss","sancai"})
            {
                string path=Path.Combine(Application.dataPath,"Resources","Themes",id,"theme.json");Assert.That(File.Exists(path),Is.True,path);
                string json=File.ReadAllText(path);
                foreach(string key in new[]{"buttonStep","buttonStepPrimary","section","caption"})Assert.That(json,Does.Not.Contain("\""+key+"\""),id+" sets "+key);
            }
        }

        [Test] public void W20X02_PageSceneIsImportedLikeTableScene()
        {
            string themes=Path.Combine(Application.dataPath,"Resources","Themes");int checkedThemes=0;
            foreach(string folder in Directory.GetDirectories(themes))
            {
                string page=Path.Combine(folder,"Table","page_scene.png"),table=Path.Combine(folder,"Table","table_scene.png");
                if(!File.Exists(page))continue;
                Assert.That(File.Exists(table),Is.True,folder+": page_scene without table_scene");
                // The importer settings as committed: every line of the two .meta files but the guid.
                string[] Settings(string png)=>File.ReadAllLines(png+".meta").Where(l=>!l.StartsWith("guid:")).ToArray();
                Assert.That(Settings(page),Is.EqualTo(Settings(table)),folder+": page_scene.png.meta differs from table_scene.png.meta");
                // And as loaded.
                string id=Path.GetFileName(folder);
                var p=Resources.Load<Texture2D>("Themes/"+id+"/Table/page_scene");var t=Resources.Load<Texture2D>("Themes/"+id+"/Table/table_scene");
                Assert.That(p,Is.Not.Null);Assert.That(t,Is.Not.Null);
                Assert.That(new object[]{p.width,p.height,p.format,p.mipmapCount,p.wrapMode,p.filterMode},Is.EqualTo(new object[]{t.width,t.height,t.format,t.mipmapCount,t.wrapMode,t.filterMode}),id);
                checkedThemes++;
            }
            Assert.That(checkedThemes,Is.GreaterThan(0),"At least one shipped style has a page_scene (comic).");
        }
    }
}
