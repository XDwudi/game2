using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DiceCraft.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DiceCraft.Tests
{
    /// <summary>
    /// V1.1 task W20X item 3 (THEME_SPEC 3.1): the texts that the non-play screens draw straight on the table, found in the
    /// running game. Each non-play screen is walked in every style; a visible text with no opaque interface surface behind
    /// it (the surface rule of the W11b visible-frame check: no filled image drawn before the text holds the centre and at
    /// least half of its glyphs) is on the table. Modal states list only the modal's own texts (the screen under the scrim
    /// is listed on its own).
    ///
    /// The list (screen, object path, ink rectangle at 1920x1080 in design units, palette key of its colour, the colour each
    /// style drew, see-through layers between table and text) is written to artifacts/cache/v11/w20x/&lt;size&gt;/table-text/
    /// table_text.json; tools/art_v11/table_text.json is that file from the 1920x1080 run, checked by
    /// tools/art_v11/table_text_contrast.py. The test fails when the set of table texts no longer matches the committed list
    /// (a new text on the table, or one gone), so the list stays complete; moved rectangles and changed colours are reported.
    /// </summary>
    public sealed partial class GameplayPlayModeTests
    {
        private sealed class TableText20X
        {
            public string screen,name,text,colorKey;public Rect rect,box;
            public readonly SortedDictionary<string,string> colors=new SortedDictionary<string,string>(StringComparer.Ordinal);
            public readonly List<string> themes=new List<string>(),veils=new List<string>();
        }
        private static readonly string[] PaletteOrder20X={"textColor","muted","gold","paper","ink","teal","red","blue","violet","danger","shadow","ghost","primary","reward","boss","pressure","panel","panelLight","background","felt","rarityCommon","rarityAdvanced","rarityRare"};

        private static string Hex20X(Color c,bool alpha)=>alpha?ColorUtility.ToHtmlStringRGBA(c):ColorUtility.ToHtmlStringRGB(c);
        /// <summary>The palette key whose colour the text has in the active style (first in PaletteOrder20X), or custom.</summary>
        private static string PaletteKey20X(Color c)
        {
            var palette=UiKit.Palette;
            foreach(string key in PaletteOrder20X)
            {
                var field=typeof(ThemePalette).GetField(key,BindingFlags.Public|BindingFlags.Instance);
                if(field==null)continue;var p=(Color)field.GetValue(palette);
                if(Mathf.Abs(p.r-c.r)<.6f/255&&Mathf.Abs(p.g-c.g)<.6f/255&&Mathf.Abs(p.b-c.b)<.6f/255)return key;
            }
            return "custom";
        }
        private Rect Design20X(Rect screen)
        {
            var main=W14Main;
            var a=main.InverseTransformPoint(new Vector3(screen.xMin,screen.yMin));var b=main.InverseTransformPoint(new Vector3(screen.xMax,screen.yMax));
            float x0=Mathf.Min(a.x,b.x),x1=Mathf.Max(a.x,b.x),y0=Mathf.Min(-a.y,-b.y),y1=Mathf.Max(-a.y,-b.y);
            return Rect.MinMaxRect(x0,y0,x1,y1);
        }
        private static string R20X(float v)=>(Mathf.Round(v*2)/2).ToString("0.#",CultureInfo.InvariantCulture);

        /// <summary>Adds the texts of <paramref name="scope"/> that stand on the table in this style.</summary>
        private void CollectTableTexts20X(Dictionary<string,TableText20X> found,string screen,string theme,Transform scope)
        {
            Canvas.ForceUpdateCanvases();
            var order=app.canvas.GetComponentsInChildren<Graphic>(false);
            foreach(var t in scope.GetComponentsInChildren<TextMeshProUGUI>(false))
            {
                if(!t.enabled||!t.gameObject.activeInHierarchy||string.IsNullOrWhiteSpace(t.text))continue;
                float alpha=t.color.a*t.canvasRenderer.GetInheritedAlpha();if(alpha<.05f)continue;
                t.ForceMeshUpdate();var rects=W11bGlyphRects(t);if(rects.Count==0)continue;
                if(W11bVisibleFrame(t,rects,order,out _)!=null)continue;
                var union=Rect.MinMaxRect(rects.Min(r=>r.xMin),rects.Min(r=>r.yMin),rects.Max(r=>r.xMax),rects.Max(r=>r.yMax));
                string path=W20XPath(t.transform,scope);string key=screen+"|"+path;
                if(!found.TryGetValue(key,out var entry))
                {
                    entry=new TableText20X{screen=screen,name=path,text=t.text.Replace("\n","/"),colorKey=PaletteKey20X(t.color),rect=Design20X(union),box=Design20X(V06ScreenRect(t.rectTransform))};
                    // See-through layers drawn before the text over its centre (a modal's scrim), in drawing order.
                    int index=Array.IndexOf(order,t);
                    for(int i=0;i<index;i++)
                    {
                        if(!(order[i] is Image image)||!image.enabled||!image.gameObject.activeInHierarchy||image.canvasRenderer.cull)continue;
                        float a=image.color.a*image.canvasRenderer.GetInheritedAlpha();if(a<.05f||W11bPaints(image,true))continue;
                        if(image.sprite!=null||!V06ScreenRect(image.rectTransform).Contains(union.center))continue;
                        var veil=image.color;veil.a=a;entry.veils.Add(Hex20X(veil,true));
                    }
                    found[key]=entry;
                }
                if(!entry.themes.Contains(theme))entry.themes.Add(theme);
                var drawn=t.color;drawn.a=alpha;entry.colors[theme]=alpha>.995f?Hex20X(drawn,false):Hex20X(drawn,true);
            }
        }
        private static string W20XPath(Transform t,Transform root)
        {
            var parts=new List<string>();for(var x=t;x!=null&&x!=root;x=x.parent)parts.Add(x.name);
            parts.Add(root.name);parts.Reverse();return string.Join("/",parts);
        }
        private Transform Overlay20X=>(Transform)typeof(GameApp).GetField("overlay",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(app);

        private static string Json20X(string s)
        {
            var b=new StringBuilder("\"");
            foreach(char c in s)
            {
                if(c=='"'||c=='\\')b.Append('\\').Append(c);
                else if(c<' ')b.Append("\\u").Append(((int)c).ToString("x4"));
                else b.Append(c);
            }
            return b.Append('"').ToString();
        }
        private static string TableTextJson20X(IEnumerable<TableText20X> entries,string source)
        {
            var palette=new ThemePalette();var b=new StringBuilder();
            b.Append("{\n  \"schema\": \"w20x-table-text-1\",\n");
            b.Append("  \"source\": ").Append(Json20X(source)).Append(",\n");
            b.Append("  \"note\": ").Append(Json20X("Texts the non-play screens draw with no opaque interface surface behind them. rect: the glyphs' ink, box: the text box; design units at 1920x1080, x and y from the top left. colors: the colour each style drew (RRGGBB, RRGGBBAA when see-through). themes: the styles in which the text stands on the table. veils: see-through layers between the table and the text, in drawing order. palette: the default style's values of the palette keys (ThemePalette).")).Append(",\n");
            b.Append("  \"themes\": [").Append(string.Join(", ",IdentityThemes20X.Select(Json20X))).Append("],\n");
            b.Append("  \"palette\": {");
            b.Append(string.Join(", ",PaletteOrder20X.Select(k=>{var f=typeof(ThemePalette).GetField(k);return f==null?null:Json20X(k)+": "+Json20X(ColorUtility.ToHtmlStringRGB((Color)f.GetValue(palette)));}).Where(x=>x!=null)));
            b.Append("},\n  \"entries\": [\n");
            var lines=new List<string>();
            foreach(var e in entries)
            {
                lines.Add("    {\"screen\": "+Json20X(e.screen)+", \"name\": "+Json20X(e.name)+", \"text\": "+Json20X(e.text.Length>60?e.text.Substring(0,60)+"…":e.text)+
                    ", \"rect\": ["+R20X(e.rect.x)+", "+R20X(e.rect.y)+", "+R20X(e.rect.width)+", "+R20X(e.rect.height)+"]"+
                    ", \"box\": ["+R20X(e.box.x)+", "+R20X(e.box.y)+", "+R20X(e.box.width)+", "+R20X(e.box.height)+"]"+
                    ", \"colorKey\": "+Json20X(e.colorKey)+
                    ", \"colors\": {"+string.Join(", ",e.colors.Select(p=>Json20X(p.Key)+": "+Json20X(p.Value)))+"}"+
                    ", \"themes\": ["+string.Join(", ",IdentityThemes20X.Where(e.themes.Contains).Select(Json20X))+"]"+
                    ", \"veils\": ["+string.Join(", ",e.veils.Select(Json20X))+"]}");
            }
            b.Append(string.Join(",\n",lines)).Append("\n  ]\n}\n");
            return b.ToString();
        }
        /// <summary>The screen keys and object paths of a committed list (a small reader for the list this test writes).</summary>
        private static HashSet<string> TableTextKeys20X(string json)
        {
            var keys=new HashSet<string>(StringComparer.Ordinal);
            foreach(System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(json,"\\{\"screen\": \"((?:[^\"\\\\]|\\\\.)*)\", \"name\": \"((?:[^\"\\\\]|\\\\.)*)\""))
                keys.Add(m.Groups[1].Value+"|"+m.Groups[2].Value);
            return keys;
        }

        [UnityTest] public IEnumerator PLAY_W20X_03_TableTextOnNonPlayScreens()
        {
            Assert.That(Screen.width==1920&&Screen.height==1080||Screen.width==1280&&Screen.height==720,Is.True,"Run at actual 1920x1080 or 1280x720.");
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);int previewValue=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            string savedTheme=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            var found=new Dictionary<string,TableText20X>(StringComparer.Ordinal);var screens=new List<string>();
            IEnumerator Walk(string screen,Transform scope)
            {
                if(!screens.Contains(screen))screens.Add(screen);
                foreach(string theme in IdentityThemes20X)
                {
                    app.ApplyTheme11(theme);yield return Art073Settle(.35f);
                    CollectTableTexts20X(found,screen,theme,scope??W14Main);
                }
            }
            IEnumerator WalkModal(string screen,Action open)
            {
                if(!screens.Contains(screen))screens.Add(screen);
                foreach(string theme in IdentityThemes20X)
                {
                    app.ApplyTheme11(theme);open();yield return Art073Settle(.35f);
                    CollectTableTexts20X(found,screen,theme,Overlay20X);
                    if(HasActive("CloseDialog"))Click("CloseDialog");yield return null;
                }
            }
            try
            {
                PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);GameApp.CleanCapture11=true;
                // The menu and what opens over it.
                yield return Walk("menu",null);
                yield return WalkModal("menu-settings",()=>{Click("MenuSettings");Click("SettingsTab1");});
                yield return WalkModal("menu-handbook",()=>Click("MenuHandbook"));
                yield return WalkModal("menu-new-run",()=>Click("NewRun"));
                // Stage select.
                StartRun("72003");Assert.That(app.Session.State.phase,Is.EqualTo("StagePreview"));
                yield return Walk("select",null);
                // Run end: won after the last stage, failed when the last submission misses.
                yield return IdentityEndStage20X(app.Config.stages.Length-1);Click("SettlementContinue");yield return WaitForIdle();
                Assert.That(app.Session.State.phase,Is.EqualTo("RunWon"));
                yield return Walk("run-won",null);
                var failing=IdentityWinningHand20X(3);failing.target="99999999";failing.remainingSubmissions=1;W11Apply(failing);yield return W11Settle();
                var miss=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(miss.accepted,Is.True,miss.error);app.SkipVisual();yield return W11Settle();
                if(app.Session.State.phase=="StageSettlement"&&HasActive("SettlementContinue")){Click("SettlementContinue");yield return WaitForIdle();}
                Assert.That(app.Session.State.phase,Is.EqualTo("RunFailed"));
                yield return Walk("run-failed",null);
                // The shops, the face pick and what opens over the shop.
                yield return W14Load(W14ShopFixture(false));
                string offer=app.Session.State.shop.offers.First(o=>o.category=="controller").offerId;
                yield return Walk("shop-common",null);
                yield return WalkModal("shop-offer-details",()=>Click("Offer_"+offer));
                yield return WalkModal("shop-chapter",()=>Click("ViewCycle"));
                yield return WalkModal("shop-handbook",()=>Click("Handbook"));
                yield return WalkModal("shop-dice-styles",()=>app.ShowDiceStyleLegend073());
                yield return W14Load(W14ShopFixture(true));
                yield return Walk("shop-full",null);
                var pick=ShopBoundaryFixture();pick.shop.offers.Clear();AddBoundaryOffer(pick,"pick-gold","die_upgrade","U06");
                yield return W14Load(pick);
                string die=app.Session.State.dice.OrderBy(d=>d.createdSerial).First().dieId;
                Click("Buy_pick-gold");yield return Walk("facepick-s1",null);
                Click("Target_"+die);yield return Walk("facepick-s2",null);
                Click("Face_F02");yield return Walk("facepick-s3",null);
                Click("CancelFacePick14");yield return null;
            }
            finally
            {
                GameApp.CleanCapture11=false;app.ApplyTheme11(GameTheme.DefaultId);
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,previewValue);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,savedTheme);PlayerPrefs.Save();
            }
            var entries=found.Values.OrderBy(e=>screens.IndexOf(e.screen)).ThenBy(e=>e.rect.y).ThenBy(e=>e.rect.x).ThenBy(e=>e.name,StringComparer.Ordinal).ToList();
            string json=TableTextJson20X(entries,"PLAY_W20X_03_TableTextOnNonPlayScreens at "+Screen.width+"x"+Screen.height+", text 100 %, screens: "+string.Join(", ",screens));
            string written=Path.Combine(W20XOutput("table-text"),"table_text.json");File.WriteAllText(written,json,new UTF8Encoding(false));
            foreach(var e in entries)TestContext.WriteLine(e.screen+" "+e.name+" '"+e.text+"' "+e.colorKey+" on the table in "+string.Join(",",e.themes));
            TestContext.WriteLine("W20X-03: "+entries.Count+" table texts on "+screens.Count+" screens, written to "+written);
            Assert.That(screens.Count,Is.EqualTo(16),"Every non-play screen of the walk.");
            string committed=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","tools","art_v11","table_text.json"));
            Assert.That(File.Exists(committed),Is.True,"Commit the 1920x1080 list as tools/art_v11/table_text.json (written to "+written+").");
            var expected=TableTextKeys20X(File.ReadAllText(committed));var actual=new HashSet<string>(entries.Select(e=>e.screen+"|"+e.name),StringComparer.Ordinal);
            var added=actual.Except(expected).OrderBy(k=>k).ToList();var gone=expected.Except(actual).OrderBy(k=>k).ToList();
            Assert.That(added.Count+gone.Count,Is.Zero,"The table texts changed; regenerate tools/art_v11/table_text.json from "+written+".\nNew on the table: "+string.Join("; ",added)+"\nNo longer on the table: "+string.Join("; ",gone));
        }
    }
}
