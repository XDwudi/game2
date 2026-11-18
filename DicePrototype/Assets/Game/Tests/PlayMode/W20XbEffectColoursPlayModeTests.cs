using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UObj = UnityEngine.Object;

namespace DiceCraft.Tests
{
    /// <summary>
    /// V1.1 task W20Xb item B (THEME_SPEC §1, §5; coordinator 2026-11-18): the three effects that drew fixed colours read
    /// the palette when they are drawn. Art071EnchantCue: the base papers teal, the multiplier arc (segments or sprite)
    /// violet, the open-slot edges paper. Art071ComboEdgeAccents: the ring, arcs and stars gold. Art07ScoreBurstLayout: the
    /// stepped borders gold, the square chips gold at tier 1 and the dice red and blue above it.
    ///
    /// Each effect is drawn in default, swiss, sancai, comic and an in-memory style whose six colours all differ from the
    /// default (red and blue are the same in every shipped style, THEME_SPEC §2); every graphic is compared, as the 8-bit
    /// vertex colour UI draws, with the active palette. In default the colours equal the constants the effects used before
    /// W20Xb. A cue and a burst drawn in one style and then only ticked after a switch take the new style's colours (draw
    /// time, not build time). Report: artifacts/cache/v11/w20xb/&lt;size&gt;/w20xb-b/w20xb-b.txt.
    /// </summary>
    public sealed partial class GameplayPlayModeTests
    {
        private struct FxPalette20Xb{public Color32 teal,violet,paper,gold,red,blue;}
        /// <summary>The colours the effects were drawn with until W20Xb (static fields and literals), as 8-bit colour.</summary>
        private static readonly FxPalette20Xb Before20Xb=new FxPalette20Xb
        {
            teal=new Color32(0x79,0xC9,0x9C,0xFF),violet=new Color32(0xAD,0x92,0xDB,0xFF),paper=new Color32(0xF4,0xE8,0xCB,0xFF),
            gold=new Color(.953f,.729f,.345f,1),red=new Color(.925f,.396f,.365f,1),blue=new Color(.333f,.714f,.851f,1),
        };
        private static FxPalette20Xb Active20Xb()=>new FxPalette20Xb{teal=UiKit.Teal,violet=UiKit.Violet,paper=UiKit.Paper,gold=UiKit.Gold,red=UiKit.Red,blue=UiKit.Blue};
        private static string Rgb20Xb(Color32 c)=>c.r.ToString("X2")+c.g.ToString("X2")+c.b.ToString("X2");

        /// <summary>Every effect graphic, drawn, with the key it must show: name, key, its colour (8-bit).</summary>
        private IEnumerable<(string name,string key,Color32 seen,Color seenFloat)> EffectGraphics20Xb(Art071EnchantCue cue,Sprite white,Art07PrefabView view)
        {
            var found=new List<(string,string,Color32,Color)>();
            void Add(Graphic g,string key){if(g!=null&&g.gameObject.activeInHierarchy)found.Add((g.name,key,(Color32)g.color,g.color));}
            Image Find(Component root,string name)=>root.GetComponentsInChildren<Image>(true).FirstOrDefault(i=>i.name==name);
            var from=new Vector2(-35,-4);var to=new Vector2(35,8);
            // Enchant cue: segments for the arc first (no arc sprite), then the arc sprite.
            cue.Configure(new Vector2(180,100),white,white);
            Assert.That(cue.Bind("base10","w20xb",false,from,to),Is.True);cue.Tick(.3f);
            for(int i=0;i<4;i++)Add(Find(cue,"MintPaper"+i),"teal");
            Assert.That(cue.Bind("mult2","w20xb",false,from,to),Is.True);cue.Tick(.3f);
            for(int i=0;i<8;i++)Add(Find(cue,"VioletArcSegment"+i),"violet");
            Assert.That(cue.Bind("slotless","w20xb",false,from,to),Is.True);cue.Tick(.3f);
            for(int i=0;i<4;i++)Add(Find(cue,"OpenSlotEdge"+i),"paper");
            cue.Configure(new Vector2(180,100),white,white,white);
            Assert.That(cue.Bind("mult2","w20xb",false,from,to),Is.True);cue.Tick(.3f);
            Add(Find(cue,"MultiplierArc"),"violet");
            cue.ResetForPool();
            // Combo, tier 5: the ring and arcs, the stars, the borders, the red and blue chips.
            view.ResetForPool();view.SetBurstLevel(5);view.Play(Vector3.zero);view.ConfigureScoreBurst(new Vector2(1528,156));view.Evaluate(.2f);
            foreach(var image in view.GetComponentsInChildren<Image>(true).Where(i=>i.name=="ComboLocalRing071"||i.name.StartsWith("ComboGoldArc071_",StringComparison.Ordinal)))Add(image,"gold");
            foreach(var star in view.burstStaticStars)if(star!=null)Add(star.GetComponent<Image>(),"gold");
            foreach(var image in view.GetComponentsInChildren<Image>(true).Where(i=>i.name.StartsWith("ScoreStepBorder",StringComparison.Ordinal)))Add(image,"gold");
            for(int i=0;i<view.particles.Length;i++)if(view.particles[i]!=null)Add(view.particles[i].GetComponent<Image>(),i%2==0?"red":"blue");
            // Tier 1: the chips are gold.
            view.ResetForPool();view.SetBurstLevel(1);view.Play(Vector3.zero);view.ConfigureScoreBurst(new Vector2(1528,156));view.Evaluate(.2f);
            for(int i=0;i<view.particles.Length;i++)if(view.particles[i]!=null)Add(view.particles[i].GetComponent<Image>(),"gold (tier 1)");
            view.ResetForPool();
            return found;
        }
        private static Color32 Expected20Xb(FxPalette20Xb p,string key)=>key.StartsWith("teal")?p.teal:key.StartsWith("violet")?p.violet:key.StartsWith("paper")?p.paper:key.StartsWith("gold")?p.gold:key.StartsWith("red")?p.red:p.blue;

        [UnityTest] public IEnumerator PLAY_W20XB_B_EffectColoursFollowThePaletteAtDrawTime()
        {
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);int previewValue=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            string savedTheme=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            var report=new List<string>();var failures=new List<string>();
            // A style whose six effect colours all differ from the default (the shipped styles keep the dice red and blue).
            var probe=new ThemePalette().Clone();
            Assert.That(ThemePalette.TryHex("2B8C6E",out probe.teal)&&ThemePalette.TryHex("5A2DA0",out probe.violet)&&ThemePalette.TryHex("D9F0FF",out probe.paper)
                &&ThemePalette.TryHex("C8A000",out probe.gold)&&ThemePalette.TryHex("B0102A",out probe.red)&&ThemePalette.TryHex("1040B0",out probe.blue),Is.True);
            var host=new GameObject("W20XbEnchantFixture",typeof(RectTransform),typeof(CanvasGroup));host.transform.SetParent(app.canvas.transform,false);
            var cue=host.AddComponent<Art071EnchantCue>();var white=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),Vector2.one*.5f);
            var comboHost=new GameObject("W20XbComboFixture",typeof(RectTransform));comboHost.transform.SetParent(app.canvas.transform,false);
            var view=ArtLibraryClone("fx_combo_burst");view.transform.SetParent(comboHost.transform,false);
            var seenByTheme=new Dictionary<string,Dictionary<string,Color32>>();
            try
            {
                PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);
                ThemeRegistry.RegisterForTests(GameTheme.CreateForTests("w20xb-fx",probe));
                // Inactive effect prefabs may defer Awake until first Play: warm the one-time construction first.
                view.SetBurstLevel(5);view.Play(Vector3.zero);view.ConfigureScoreBurst(new Vector2(1528,156));view.ResetForPool();
                foreach(string theme in new[]{"default","swiss","sancai","comic","w20xb-fx"})
                {
                    app.ApplyTheme11(theme);yield return null;
                    var palette=Active20Xb();var seen=new Dictionary<string,Color32>();int checkedGraphics=0;
                    foreach(var g in EffectGraphics20Xb(cue,white,view))
                    {
                        var expected=Expected20Xb(palette,g.key);checkedGraphics++;
                        if(g.seen.r!=expected.r||g.seen.g!=expected.g||g.seen.b!=expected.b)failures.Add(theme+" "+g.name+" ("+g.key+"): #"+Rgb20Xb(g.seen)+", the palette's "+g.key+" is #"+Rgb20Xb(expected));
                        if(theme=="default")
                        {
                            var before=Expected20Xb(Before20Xb,g.key);
                            if(g.seen.r!=before.r||g.seen.g!=before.g||g.seen.b!=before.b)failures.Add("default "+g.name+" ("+g.key+"): #"+Rgb20Xb(g.seen)+", drawn before W20Xb as #"+Rgb20Xb(before));
                        }
                        seen[g.key.Split(' ')[0]]=g.seen;
                    }
                    foreach(string key in new[]{"teal","violet","paper","gold","red","blue"})if(!seen.ContainsKey(key))failures.Add(theme+": no drawn graphic shows "+key);
                    seenByTheme[theme]=seen;
                    report.Add(theme+": "+checkedGraphics+" graphics; "+string.Join(", ",seen.OrderBy(k=>k.Key,StringComparer.Ordinal).Select(k=>k.Key+" #"+Rgb20Xb(k.Value))));
                }
                // Switching the style changes every colour (the probe differs from the default in all six).
                foreach(var key in seenByTheme["default"].Keys)
                {
                    var a=seenByTheme["default"][key];var b=seenByTheme["w20xb-fx"][key];
                    if(a.r==b.r&&a.g==b.g&&a.b==b.b)failures.Add(key+": the same colour in default and in w20xb-fx");
                    var changedIn=new[]{"swiss","sancai","comic"}.Where(t=>{var c=seenByTheme[t][key];return c.r!=a.r||c.g!=a.g||c.b!=a.b;}).ToArray();
                    report.Add(key+": default #"+Rgb20Xb(a)+", w20xb-fx #"+Rgb20Xb(b)+"; changed in "+(changedIn.Length>0?string.Join(", ",changedIn):"none of the shipped styles (THEME_SPEC §2 keeps it)"));
                }
                // Draw time: drawn in default, the style switched, then only ticked or evaluated.
                app.ApplyTheme11("default");yield return null;
                cue.Configure(new Vector2(180,100),white,white);Assert.That(cue.Bind("base10","w20xb",false,new Vector2(-35,-4),new Vector2(35,8)),Is.True);cue.Tick(.2f);
                view.ResetForPool();view.SetBurstLevel(5);view.Play(Vector3.zero);view.ConfigureScoreBurst(new Vector2(1528,156));view.Evaluate(.1f);
                app.ApplyTheme11("w20xb-fx");yield return null;
                cue.Tick(.35f);view.Evaluate(.25f);
                var paper0=host.GetComponentsInChildren<Image>(true).First(i=>i.name=="MintPaper0");
                var ring=view.GetComponentsInChildren<Image>(true).First(i=>i.name=="ComboLocalRing071");
                var border=view.GetComponentsInChildren<Image>(true).First(i=>i.name=="ScoreStepBorder0");
                var chip=view.particles.Select(p=>p!=null?p.GetComponent<Image>():null).First(i=>i!=null&&i.gameObject.activeInHierarchy);
                foreach(var (g,key,expected) in new[]{(paper0,"teal",(Color32)probe.teal),(ring,"gold",(Color32)probe.gold),(border,"gold",(Color32)probe.gold),(chip,chip.name,(Color32)(Array.IndexOf(view.particles,chip.transform)%2==0?probe.red:probe.blue))})
                {
                    Color32 c=g.color;bool ok=c.r==expected.r&&c.g==expected.g&&c.b==expected.b;
                    report.Add("draw time: "+g.name+" drawn in default, then ticked in w20xb-fx: #"+Rgb20Xb(c)+(ok?" (the new style's)":" (expected #"+Rgb20Xb(expected)+")"));
                    if(!ok)failures.Add("draw time: "+g.name+" kept #"+Rgb20Xb(c)+" after the switch, expected "+key+" #"+Rgb20Xb(expected));
                }
            }
            finally
            {
                cue.ResetForPool();view.ResetForPool();
                UObj.Destroy(view.gameObject);UObj.Destroy(comboHost);UObj.Destroy(host);UObj.Destroy(white);
                ThemeRegistry.UnregisterForTests("w20xb-fx");app.ApplyTheme11(GameTheme.DefaultId);
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,previewValue);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,savedTheme);PlayerPrefs.Save();
                File.WriteAllLines(Path.Combine(W20XbOutput("w20xb-b"),"w20xb-b.txt"),report.Concat(new[]{"","PROBLEMS "+failures.Count}).Concat(failures));
            }
            foreach(string line in report)TestContext.WriteLine(line);
            Assert.That(failures,Is.Empty,string.Join("\n",failures.Take(40)));
        }
    }
}
