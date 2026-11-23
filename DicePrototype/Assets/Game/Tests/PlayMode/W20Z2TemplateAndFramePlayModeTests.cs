using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UObj = UnityEngine.Object;

namespace DiceCraft.Tests
{
    /// <summary>
    /// V1.1 task W20Z2 (W20Z_REPORT §9.2 and §6.7, coordinator 2026-11-19).
    ///
    /// 01 (item 2): the card template keeps a card's labels in a pool by name, and a compact card's mini path measured its
    ///    short text with the auto-sizing a 150 % text card had left on the same label: the play row's cards are pooled by
    ///    instance (text cards at 150 % with five cards or fewer, mini cards at 100 % with eight or more). In every style C21
    ///    is drawn as a 100 % mini card on a fresh object, and on the object that was just drawn as a 150 % text card: both
    ///    show the short text, at the same size, without auto-sizing.
    /// 02 (items 3 and 4): three texts that sat on drawn frame lines keep inside the frame of the surface under them, as
    ///    the W11b visible-frame check reads it (the surface's own frame and every open ring drawn over it), by at least one
    ///    design unit: the boss stage card's reward line (every loot kind and the final boss's line, a one-line and the
    ///    longest boss effect), the 我的构筑 caption row of the shop (common and full shop), and the handbook's explanation of
    ///    the selected hand (every hand; with enlarged text the part its scroll box shows). The reward line also starts no
    ///    further left than the stage card's title text. In every style, at 100 % and 150 % text. Report:
    ///    artifacts/cache/v11/w20z2/&lt;size&gt;/w20z2-02/w20z2-02.txt.
    /// </summary>
    public sealed partial class GameplayPlayModeTests
    {
        private static readonly MethodInfo DrawCard20Z2=typeof(GameApp).GetMethod("DrawCard13",BindingFlags.NonPublic|BindingFlags.Instance);
        private static readonly MethodInfo GalleryVariant20Z2=typeof(GameApp).GetMethod("GalleryVariant13",BindingFlags.NonPublic|BindingFlags.Instance);
        /// <summary>The shipped styles (default first), without the in-memory test styles of other cases.</summary>
        private static string[] Styles20Z2=>ThemeRegistry.All.Where(t=>!t.Id.StartsWith("w20",StringComparison.Ordinal)).Select(t=>t.Id).ToArray();
        private static string W20Z2Output(string name)
        {
            string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","artifacts","cache","v11","w20z2",Screen.width+"x"+Screen.height,name));
            Directory.CreateDirectory(dir);return dir;
        }
        private static TextMeshProUGUI ShortText20Z2(UnityEngine.UI.Button card)=>card.transform.Find("ShortText11")?.GetComponent<TextMeshProUGUI>();

        [UnityTest] public IEnumerator PLAY_W20Z2_01_MiniCardDrawnWhereATextCardWasShowsItsShortText()
        {
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);int previewValue=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            string savedTheme=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            var report=new List<string>();var failures=new List<string>();
            var compact=Enum.Parse(typeof(GameApp.CardSize13),"Compact");
            var c21=app.Config.Controller("C21");int rarity=c21.rarity=="rare"?2:c21.rarity=="uncommon"?1:0;
            string expected=c21.shortText;
            try
            {
                PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);
                var styles=Styles20Z2;
                Assert.That(styles,Does.Contain("default").And.Contain("deco").And.Contain("glass"),"default and the styles");
                foreach(string theme in styles)
                {
                    app.ApplyTheme11(theme);Assert.That(app.ThemeId11,Is.EqualTo(theme));yield return null;
                    var probe=UiKit.Rect(app.canvas.transform,"W20Z2CarryProbe_"+theme,0,0,1920,1080);
                    try
                    {
                        object face=GalleryVariant20Z2.Invoke(app,new object[]{"C21",rarity,0,1});
                        W11TextScale(100);
                        // A label's state as set by the template: shown or hidden, a fixed size or the auto-sizing range.
                        string line(TextMeshProUGUI t)=>t==null?"no label":(t.gameObject.activeSelf?"shown":"hidden")+", "+(t.enableAutoSizing?"auto-sized "+t.fontSizeMin.ToString("0.#",CultureInfo.InvariantCulture)+"-"+t.fontSizeMax.ToString("0.#",CultureInfo.InvariantCulture)+" px":"fixed "+t.fontSize.ToString("0.##",CultureInfo.InvariantCulture)+" px");
                        var fresh=(UnityEngine.UI.Button)DrawCard20Z2.Invoke(app,new object[]{probe,"Fresh",40f,40f,280f,117f,face,compact,null,true});
                        var freshText=ShortText20Z2(fresh);string freshLine=line(freshText);
                        // W21 chooses the largest complete legal wrap. Inspect it before reusing its pooled label.
                        W11TextScale(150);
                        var tall=(UnityEngine.UI.Button)DrawCard20Z2.Invoke(app,new object[]{probe,"Reused",40f,400f,280f,244f,face,compact,null,true});
                        var tallText=ShortText20Z2(tall);string tallLine=line(tallText);
                        Assert.That(tallText,Is.Not.Null,theme);
                        tallText.ForceMeshUpdate();
                        Assert.That(tallText.gameObject.activeSelf,Is.True,theme);
                        Assert.That(tallText.text.Replace("\n",""),Is.EqualTo(expected),theme);
                        Assert.That(tallText.fontSize,Is.InRange(19.5f,30f),theme);
                        Assert.That(tallText.isTextOverflowing||tallText.isTextTruncated,Is.False,theme);
                        // Also reproduce a label pooled with legacy TMP auto-sizing enabled.
                        tallText.fontSizeMin=19.5f;tallText.fontSizeMax=30;tallText.enableAutoSizing=true;
                        W11TextScale(100);
                        var reused=(UnityEngine.UI.Button)DrawCard20Z2.Invoke(app,new object[]{probe,"Reused",40f,400f,280f,117f,face,compact,null,true});
                        Assert.That(reused,Is.SameAs(tall),theme+": the mini card is drawn on the pooled object of the text card.");
                        var reusedText=ShortText20Z2(reused);
                        report.Add(theme+": C21 mini card 100 % fresh: "+freshLine+"; the 150 % text card on the pooled object: "+tallLine+"; then the mini card on that object: "+line(reusedText));
                        if(freshText==null||!freshText.gameObject.activeSelf)failures.Add(theme+": a fresh 100 % mini card does not show C21's short text (the check needs it to)");
                        if(reusedText==null||!reusedText.gameObject.activeSelf){failures.Add(theme+": the mini card drawn where the text card was hides C21's short text");continue;}
                        if(reusedText.enableAutoSizing)failures.Add(theme+": the mini card's short text keeps the text card's auto-sizing ("+reusedText.fontSizeMin+" to "+reusedText.fontSizeMax+")");
                        if(freshText!=null&&Mathf.Abs(reusedText.fontSize-freshText.fontSize)>.01f)failures.Add(theme+": the reused mini card's short text is "+reusedText.fontSize+" px, a fresh one "+freshText.fontSize+" px");
                        if(reusedText.text.Replace("\n","")!=expected)failures.Add(theme+": the reused mini card shows '"+reusedText.text+"', the short text is '"+expected+"'");
                        reusedText.ForceMeshUpdate();
                        int drawn=reusedText.textInfo.characterInfo.Take(reusedText.textInfo.characterCount).Count(c=>c.isVisible&&!char.IsWhiteSpace(c.character));
                        int wanted=expected.Count(c=>!char.IsWhiteSpace(c));
                        if(reusedText.isTextOverflowing||drawn!=wanted)failures.Add(theme+": the reused mini card draws "+drawn+" of "+wanted+" characters"+(reusedText.isTextOverflowing?" and overflows":""));
                    }
                    finally{UObj.Destroy(probe.gameObject);}
                }
            }
            finally
            {
                W11TextScale(100);app.ApplyTheme11(GameTheme.DefaultId);
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,previewValue);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,savedTheme);PlayerPrefs.Save();
                File.WriteAllLines(Path.Combine(W20Z2Output("w20z2-01"),"w20z2-01.txt"),report.Concat(new[]{"","PROBLEMS "+failures.Count}).Concat(failures));
            }
            foreach(string row in report)TestContext.WriteLine(row);
            Assert.That(failures,Is.Empty,string.Join("\n",failures));
        }

        /// <summary>How far the glyphs of <paramref name="text"/> keep inside the drawn frame of the surface under them
        /// (W11bVisibleFrame), in design units (negative: outside), with only the part inside <paramref name="clip"/> (a scroll
        /// box's mask) counted. Adds a failure under <paramref name="minimum"/>.</summary>
        private void Inside20Z2(string scope,TextMeshProUGUI text,RectTransform clip,float minimum,List<string> report,List<string> failures)
        {
            text.ForceMeshUpdate();var glyphs=W11bGlyphRects(text);
            if(clip!=null)
            {
                var c=V06ScreenRect(clip);
                glyphs=glyphs.Select(g=>Rect.MinMaxRect(Mathf.Max(g.xMin,c.xMin),Mathf.Max(g.yMin,c.yMin),Mathf.Min(g.xMax,c.xMax),Mathf.Min(g.yMax,c.yMax))).Where(g=>g.width>.01f&&g.height>.01f).ToList();
            }
            string at=scope+" "+W11bPath(text.transform)+" '"+text.text.Replace("\n"," / ")+"'";
            if(glyphs.Count==0){failures.Add(at+": no glyph drawn");return;}
            var order=app.canvas.GetComponentsInChildren<Graphic>(false);
            var frame=W11bVisibleFrame(text,glyphs,order,out string surface);
            if(!frame.HasValue){failures.Add(at+": no drawn surface under it");return;}
            var f=frame.Value;float unit=Screen.width/1920f;
            float left=glyphs.Min(g=>g.xMin-f.xMin)/unit,right=glyphs.Min(g=>f.xMax-g.xMax)/unit,bottom=glyphs.Min(g=>g.yMin-f.yMin)/unit,top=glyphs.Min(g=>f.yMax-g.yMax)/unit;
            float inside=Mathf.Min(Mathf.Min(left,right),Mathf.Min(bottom,top));
            string F(float v)=>v.ToString("0.0",CultureInfo.InvariantCulture);
            report.Add(at+" on "+surface+": glyphs inside the drawn frame by left "+F(left)+", right "+F(right)+", top "+F(top)+", bottom "+F(bottom)+" units");
            if(inside<minimum)failures.Add(at+": a glyph is "+F(inside)+" units inside the drawn frame of "+surface+" (at least "+F(minimum)+")");
        }

        [UnityTest,Timeout(900000)] public IEnumerator PLAY_W20Z2_02_RewardLineBuildCaptionsAndHandbookTextKeepInsideTheirFrames()
        {
            Assert.That(Screen.width==1920&&Screen.height==1080||Screen.width==1280&&Screen.height==720,Is.True,"Run at actual 1920x1080 or 1280x720.");
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);int previewValue=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            string savedTheme=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            var report=new List<string>();var failures=new List<string>();
            const float Clearance=1f;
            int[] texts={100,150};
            try
            {
                PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);GameApp.CleanCapture11=true;
                var styles=Styles20Z2;Assert.That(styles.Length,Is.GreaterThanOrEqualTo(6),"default and the five styles");
                StartRun("72005");
                float unit=Screen.width/1920f;
                // ---- A. The boss stage card (item 3). B02 has a one-line effect; the longest regular effect takes two lines.
                string longest=app.Config.bossModifiers.Where(m=>m.tier<4).OrderByDescending(m=>(m.shortText??"").Length).ThenBy(m=>m.id,StringComparer.Ordinal).First().id;
                report.Add("boss modifiers: B02 '"+app.Config.Modifier("B02").shortText+"', the longest regular effect "+longest+" '"+app.Config.Modifier(longest).shortText+"'");
                foreach(string modifier in new[]{"B02",longest})
                {
                    W11Apply(W11Scene(new[]{"C39","C139","C34","C56","C08"},new[]{1,2,3,4,5},"bbrbb",0,false,stageIndex:8,modifier:modifier,target:"13500",score:"0",items:2));yield return W11Settle();
                    // The loot kind is changed on the drawn copy of the state (GameApp.ViewState; Session.State hands out clones),
                    // which stays the drawn state while no command runs; the session itself is not touched.
                    var state=app.ViewState;int cycle=state.stage.stageIndex/3;
                    Assert.That(cycle,Is.LessThan(state.cycles.Count),"The boss stage has a cycle plan.");
                    string planned=state.cycles[cycle].lootKind;
                    try
                    {
                        // Every loot kind, then "" (the final boss's line 终局首领 · 通关即胜利).
                        foreach(string loot in app.Config.lootKinds.Select(k=>k.id).Concat(new[]{""}))
                        {
                            state.cycles[cycle].lootKind=loot;
                            foreach(string theme in styles)foreach(int text in texts)
                            {
                                app.ApplyTheme11(theme);W11TextScale(text);yield return null;Canvas.ForceUpdateCanvases();
                                string scope=theme+" "+text+"% boss "+modifier+" loot "+(loot==""?"(final)":loot);
                                var line=FindActive<TextMeshProUGUI>("BossLoot11");var title=FindActive<TextMeshProUGUI>("StageCardTitle11");
                                string wanted=loot==""?"终局首领 · 通关即胜利":"通关奖励："+app.Config.lootKinds.First(k=>k.id==loot).shortText;
                                if(line.text.Replace("\n","")!=wanted)failures.Add(scope+": the reward line reads '"+line.text.Replace("\n"," / ")+"', not '"+wanted+"'");
                                Inside20Z2(scope,line,null,Clearance,report,failures);
                                line.ForceMeshUpdate();title.ForceMeshUpdate();
                                float lineLeft=W11bGlyphRects(line).Min(g=>g.xMin),titleLeft=W11bGlyphRects(title).Min(g=>g.xMin);
                                report.Add(scope+": the reward line starts "+((lineLeft-titleLeft)/unit).ToString("0.0",CultureInfo.InvariantCulture)+" units right of the title's text");
                                if(lineLeft<titleLeft-1.5f)failures.Add(scope+": the reward line starts "+((titleLeft-lineLeft)/unit).ToString("0.0",CultureInfo.InvariantCulture)+" units left of the title's text");
                                if(line.fontSize<19.49f)failures.Add(scope+": the reward line is "+line.fontSize+" units (AX-03: 19.5)");
                                if(line.isTextTruncated||line.isTextOverflowing)failures.Add(scope+": the reward line is cut");
                            }
                        }
                    }
                    finally{state.cycles[cycle].lootKind=planned;}
                }
                W11TextScale(100);app.ApplyTheme11(GameTheme.DefaultId);app.Refresh();yield return null;
                // ---- B. The handbook's explanation of the selected hand (item 4), every hand, over the play screen.
                foreach(string theme in styles)foreach(int text in texts)
                {
                    app.ApplyTheme11(theme);W11TextScale(text);yield return null;
                    app.ShowHandbook();yield return null;
                    var rows=app.canvas.GetComponentsInChildren<UnityEngine.UI.Button>(false).Where(b=>b.name.StartsWith("HandbookRow_",StringComparison.Ordinal)).Select(b=>b.name).ToList();
                    Assert.That(rows.Count,Is.GreaterThan(5),"The handbook lists the hands.");
                    foreach(string row in rows)
                    {
                        Click(row);yield return null;Canvas.ForceUpdateCanvases();
                        var selection=FindActive<TextMeshProUGUI>("HandbookSelection");
                        Assert.That(selection.isTextTruncated||selection.isTextOverflowing,Is.False,"All lines of the hand explanation fit without scrolling.");
                        Inside20Z2(theme+" "+text+"% handbook "+row.Substring(12),selection,null,Clearance,report,failures);
                    }
                    if(HasActive("CloseDialog"))Click("CloseDialog");yield return null;
                }
                W11TextScale(100);app.ApplyTheme11(GameTheme.DefaultId);
                // ---- C. The 我的构筑 caption row of the shop (item 4): title, capacity, order hint and the items' count.
                foreach(bool full in new[]{false,true})
                {
                    yield return W14Load(W14ShopFixture(full));
                    foreach(string theme in styles)foreach(int text in texts)
                    {
                        app.ApplyTheme11(theme);W11TextScale(text);yield return null;Canvas.ForceUpdateCanvases();
                        foreach(string label in new[]{"BuildTitle14","ControllerCapacity07","OwnedControllersTitle","OwnedItemsTitle"})
                            Inside20Z2(theme+" "+text+"% "+(full?"full":"common")+" shop",FindActive<TextMeshProUGUI>(label),null,Clearance,report,failures);
                    }
                    W11TextScale(100);
                }
            }
            finally
            {
                W11TextScale(100);GameApp.CleanCapture11=false;app.ApplyTheme11(GameTheme.DefaultId);
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,previewValue);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,savedTheme);PlayerPrefs.Save();
                File.WriteAllLines(Path.Combine(W20Z2Output("w20z2-02"),"w20z2-02.txt"),report.Concat(new[]{"","PROBLEMS "+failures.Count}).Concat(failures));
            }
            TestContext.WriteLine(report.Count+" measurements, "+failures.Count+" problems");
            Assert.That(failures,Is.Empty,string.Join("\n",failures.Take(40)));
        }
    }
}
