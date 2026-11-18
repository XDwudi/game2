using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Presentation.Tests
{
    /// <summary>
    /// V1.1 task W20Y item 3 (docs/specs/v11/THEME_SPEC.md §2 buttonSecondary): the face of a second-level button
    /// (GameApp.SolidButton11). The key parses from theme.json like every other key (six or eight digits, with or without a
    /// leading '#'); alpha 0 means unset. A palette that does not set it gives exactly the face drawn before W20Y,
    /// lerp(background, panel, .55), and a disabled one a further 25 % toward panelLight; invalid values are ignored; swiss,
    /// sancai and comic do not set it (the premise of the W20Y pixel-identity captures). PLAY_W20Y_04 checks the drawn faces.
    /// </summary>
    public sealed class ThemeInterfaceW20YTests
    {
        [Serializable] private sealed class ThemeFile20Y { public ThemePalette.Hexes palette; }
        private static readonly string[] Styles={"swiss","sancai","comic"};

        private static ThemePalette Parse(string json)=>ThemePalette.From(JsonUtility.FromJson<ThemePalette.Hexes>(json));
        private static string Hex(Color c)=>ColorUtility.ToHtmlStringRGBA(c);
        private static string ThemeJson(string id)
        {
            string path=Path.Combine(Application.dataPath,"Resources","Themes",id,"theme.json");
            Assert.That(File.Exists(path),Is.True,path);return File.ReadAllText(path);
        }
        /// <summary>The palette of a shipped style, read from its theme.json the way GameTheme does (JsonUtility into Hexes).</summary>
        private static ThemePalette StylePalette(string id)
        {
            var file=JsonUtility.FromJson<ThemeFile20Y>(ThemeJson(id));
            Assert.That(file?.palette,Is.Not.Null,id+": theme.json has a palette");return ThemePalette.From(file.palette);
        }
        /// <summary>The face GameApp.SolidButton11 drew before W20Y (a35670b4), from the palette's own fields.</summary>
        private static Color Before(ThemePalette p,bool enabled){var tint=Color.Lerp(p.background,p.panel,.55f);return enabled?tint:Color.Lerp(tint,p.panelLight,.25f);}
        /// <summary>The face since W20Y: GameApp.SolidButton11's expression, read through UiKit at draw time.</summary>
        private static Color Now(bool enabled){var tint=UiKit.ButtonSecondary(Color.Lerp(UiKit.Background,UiKit.Panel,.55f));return enabled?tint:Color.Lerp(tint,UiKit.PanelLight,.25f);}

        [TearDown] public void RestoreDefaultPalette(){UiKit.Palette=null;}

        [Test] public void W20Y03_ButtonSecondaryParsesLikeEveryOtherKey()
        {
            var six=Parse("{\"buttonSecondary\":\"C9A45C\"}");
            Assert.That(Hex(six.buttonSecondary),Is.EqualTo("C9A45CFF"),"Six digits are opaque.");
            var eight=Parse("{\"buttonSecondary\":\"C9A45C80\"}");
            Assert.That(Hex(eight.buttonSecondary),Is.EqualTo("C9A45C80"),"Eight digits carry an alpha.");
            var hash=Parse("{\"buttonSecondary\":\"#3A5FB0\"}");
            Assert.That(Hex(hash.buttonSecondary),Is.EqualTo("3A5FB0FF"),"A leading # is accepted.");
            var hashEight=Parse("{\"buttonSecondary\":\"#3A5FB0C0\"}");
            Assert.That(Hex(hashEight.buttonSecondary),Is.EqualTo("3A5FB0C0"),"A leading # with eight digits.");
            var d=new ThemePalette();
            foreach(var p in new[]{six,eight,hash,hashEight})
            {
                // The other keys keep their defaults.
                Assert.That(p.background,Is.EqualTo(d.background));Assert.That(p.panel,Is.EqualTo(d.panel));Assert.That(p.panelLight,Is.EqualTo(d.panelLight));
                // Read through UiKit at draw time: the set value replaces the fallback (today's face); disabled, it is that
                // value a further 25 % toward panelLight, the rule SolidButton11 applied to the old face.
                UiKit.Palette=p;
                Assert.That(UiKit.ButtonSecondary(Color.magenta),Is.EqualTo(p.buttonSecondary),"A set buttonSecondary replaces the fallback.");
                Assert.That(Now(true),Is.EqualTo(p.buttonSecondary));
                Assert.That(Now(false),Is.EqualTo(Color.Lerp(p.buttonSecondary,p.panelLight,.25f)));
            }
        }

        [Test] public void W20Y03_WithoutTheKeyTheFaceIsTodaysLerp()
        {
            var d=new ThemePalette();
            Assert.That(d.buttonSecondary.a,Is.Zero,"buttonSecondary is unset by default.");
            // The default face as SolidButton11 computed it before W20Y, from the v0.73 constants (background 102A2D, panel
            // 203F43, panelLight 315358), parsed the same way as then (UiKit.Hex).
            Color face=Color.Lerp(UiKit.Hex("102A2D"),UiKit.Hex("203F43"),.55f),off=Color.Lerp(face,UiKit.Hex("315358"),.25f);
            UiKit.Palette=d;
            Assert.That(Now(true),Is.EqualTo(face));Assert.That(Now(false),Is.EqualTo(off));
            Assert.That(ColorUtility.ToHtmlStringRGBA(Now(true)),Is.EqualTo("193639FF"));Assert.That(ColorUtility.ToHtmlStringRGBA(Now(false)),Is.EqualTo("1F3D41FF"));
            // Any palette that leaves the key out (empty, other keys set, the shipped swiss, sancai and comic palettes): the
            // face is exactly the old expression of that palette, and the accessor returns the fallback unchanged.
            var palettes=new List<KeyValuePair<string,ThemePalette>>
            {
                new KeyValuePair<string,ThemePalette>("default",d),
                new KeyValuePair<string,ThemePalette>("{}",Parse("{}")),
                new KeyValuePair<string,ThemePalette>("other keys",Parse("{\"background\":\"010203\",\"panel\":\"405060\",\"panelLight\":\"A0B0C0\",\"section\":\"FFFFFF\",\"caption\":\"FBDC0F\",\"buttonStep\":\"0A080A\"}")),
            };
            foreach(string id in Styles)palettes.Add(new KeyValuePair<string,ThemePalette>(id,StylePalette(id)));
            foreach(var pair in palettes)
            {
                var p=pair.Value;UiKit.Palette=p;
                Assert.That(p.buttonSecondary.a,Is.Zero,pair.Key+": unset");
                Assert.That(UiKit.ButtonSecondary(Color.magenta),Is.EqualTo(Color.magenta),pair.Key+": the fallback comes back unchanged");
                Assert.That(Now(true),Is.EqualTo(Before(p,true)),pair.Key+": enabled face");
                Assert.That(Now(false),Is.EqualTo(Before(p,false)),pair.Key+": disabled face");
            }
        }

        [Test] public void W20Y03_InvalidValuesAreIgnored()
        {
            foreach(string value in new[]{"GGGGGG","12345","1234567","C9A45C8","#12","","   ","0xC9A45C","C9A45C 80","C9A45C80FF"})
            {
                var p=Parse("{\"panel\":\"010203\",\"buttonSecondary\":\""+value+"\"}");
                Assert.That(Hex(p.panel),Is.EqualTo("010203FF"),"Known keys still parse next to an invalid value '"+value+"'.");
                Assert.That(p.buttonSecondary.a,Is.Zero,"An invalid buttonSecondary keeps the default (unset): '"+value+"'");
                UiKit.Palette=p;Assert.That(Now(true),Is.EqualTo(Before(p,true)),"'"+value+"': today's face");
            }
            // Keys that only look like it are ignored.
            var near=Parse("{\"buttonSecondaryColour\":\"FF0000\",\"secondaryButton\":\"00FF00\",\"buttonSecondaries\":\"0000FF\"}");
            Assert.That(near.buttonSecondary.a,Is.Zero,"Unknown keys are ignored.");
            // Alpha 0 means unset, as for every optional key (onFelt, valueBadge, section, caption).
            var clear=Parse("{\"buttonSecondary\":\"C9A45C00\"}");
            Assert.That(Hex(clear.buttonSecondary),Is.EqualTo("C9A45C00"));
            UiKit.Palette=clear;
            Assert.That(UiKit.ButtonSecondary(Color.magenta),Is.EqualTo(Color.magenta),"Alpha 0 is unset.");
            Assert.That(Now(true),Is.EqualTo(Before(clear,true)));Assert.That(Now(false),Is.EqualTo(Before(clear,false)));
        }

        [Test] public void W20Y03_ShippedStylesDoNotSetIt()
        {
            // The pixel-identity premise of W20Y item 5: default, swiss, sancai and comic draw their second-level buttons as before.
            Assert.That(new ThemePalette().buttonSecondary.a,Is.Zero,"default");
            foreach(string id in Styles)
            {
                Assert.That(ThemeJson(id),Does.Not.Contain("\"buttonSecondary\""),id+" sets buttonSecondary");
                Assert.That(StylePalette(id).buttonSecondary.a,Is.Zero,id+": buttonSecondary parses as unset");
            }
        }
    }
}
