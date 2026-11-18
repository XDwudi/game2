using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UObj = UnityEngine.Object;

namespace DiceCraft.Tests
{
    /// <summary>
    /// V1.1 task W20X items 1 and 2 (THEME_SPEC §2, §3; W20X-02): the optional Table/page_scene.png and the tint keys
    /// buttonStep / buttonStepPrimary, section and caption, measured on the drawn frame.
    ///
    /// Styles are built in memory for the run (ThemeRegistry.RegisterForTests; nothing under Resources, which would ship):
    /// "w20x-probe" with a plain red table_scene, a plain blue page_scene and distinct tints, and two styles with the same
    /// pictures whose new tints are all white or all black. A tinted image is drawn as (1 - a) x under + a x sprite x tint
    /// in linear light, so the probe's pixel at a point where that image is the top graphic must lie on the line from the
    /// black style's pixel to the white style's pixel, at the probe tint. The comic style is the real case: its own
    /// page_scene and tints (0A080A steps, a white section, FBDC0F captions) against copies of comic with white and black
    /// tints. Reduced motion is on, so the felt draws its pictures unchanged. A frame-by-frame walk checks that the felt
    /// picture changes in the same frame as the screen between the shop and the play screen. Report:
    /// artifacts/cache/v11/w20x/&lt;size&gt;/w20x-02.txt.
    /// </summary>
    public sealed partial class GameplayPlayModeTests
    {
        private static readonly Color32 ProbeTable20X=new Color32(210,40,40,255),ProbePage20X=new Color32(40,60,210,255);
        private const string ProbeStep20X="2E8B57",ProbeStepPrimary20X="7A3DB8",ProbeSection20X="3A5FB0",ProbeCaption20X="C8A23A";
        private static Texture2D Solid20X(Color32 colour)
        {
            var texture=new Texture2D(1920,1080,TextureFormat.RGBA32,false,false){name="w20x-solid-"+ColorUtility.ToHtmlStringRGB(colour),wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color32[1920*1080];for(int i=0;i<pixels.Length;i++)pixels[i]=colour;
            texture.SetPixels32(pixels);texture.Apply(false,false);return texture;
        }
        private static ThemePalette Tints20X(ThemePalette basis,string all)=>Tints20X(basis,all,all,all,all);
        private static ThemePalette Tints20X(ThemePalette basis,string step,string stepPrimary,string section,string caption)
        {
            var p=(basis??new ThemePalette()).Clone();
            Assert.That(ThemePalette.TryHex(step,out p.buttonStep)&&ThemePalette.TryHex(stepPrimary,out p.buttonStepPrimary)&&ThemePalette.TryHex(section,out p.section)&&ThemePalette.TryHex(caption,out p.caption),Is.True);
            return p;
        }
        private static Texture FeltPicture20X()
        {
            var props=new MaterialPropertyBlock();GameObject.Find("Art07Felt").GetComponent<Renderer>().GetPropertyBlock(props);return props.GetTexture("_TableMap");
        }
        /// <summary>Screen pixel of a point given in a rect's own layout units (x right, y down from its top left).</summary>
        private static Vector2Int ScreenPixel20X(RectTransform target,Vector2 local)
        {
            var r=target.rect;var p=RectTransformUtility.WorldToScreenPoint(null,target.TransformPoint(new Vector3(r.xMin+local.x,r.yMax-local.y,0)));
            return new Vector2Int(Mathf.Clamp(Mathf.FloorToInt(p.x),0,Screen.width-1),Mathf.Clamp(Mathf.FloorToInt(p.y),0,Screen.height-1));
        }
        private static Color32 Pixel20X(Vector2Int point)
        {
            var shot=ScreenCapture.CaptureScreenshotAsTexture();
            try{return shot.GetPixel(point.x,point.y);}finally{UObj.Destroy(shot);}
        }
        private static string C20X(Color32 c)=>c.r+","+c.g+","+c.b;
        /// <summary>Which test picture a felt pixel shows: page blue or table red (the hue survives a modal's scrim).</summary>
        private static string Picture20X(Color32 c)=>c.b>c.r+40?"page":c.r>c.b+40?"table":"neither ("+C20X(c)+")";
        private static bool SameTint20X(Color a,Color b)=>Mathf.Abs(a.r-b.r)<.002f&&Mathf.Abs(a.g-b.g)<.002f&&Mathf.Abs(a.b-b.b)<.002f&&Mathf.Abs(a.a-b.a)<.002f;

        /// <summary>The pixel of <paramref name="target"/> at a point, with every graphic drawn after it over that point
        /// switched off for the frame, so the pixel is the target (sprite times tint) over what lies under it.</summary>
        private IEnumerator TargetPixel20X(Graphic target,Vector2Int point,Action<Color32> read)
        {
            Canvas.ForceUpdateCanvases();
            var order=app.canvas.GetComponentsInChildren<Graphic>(false);int at=Array.IndexOf(order,target);
            Assert.That(at,Is.GreaterThanOrEqualTo(0),target.name+" is drawn.");
            var hidden=new List<Graphic>();
            for(int i=at+1;i<order.Length;i++)
            {
                var g=order[i];if(!g.enabled||!g.gameObject.activeInHierarchy)continue;
                if(RectTransformUtility.RectangleContainsScreenPoint(g.rectTransform,point,null)){g.enabled=false;hidden.Add(g);}
            }
            yield return new WaitForEndOfFrame();
            try{read(Pixel20X(point));}
            finally{foreach(var g in hidden)if(g!=null)g.enabled=true;}
        }

        private sealed class TintPoint20X
        {
            public string name;public Func<Graphic> target;public Func<Color> key;public Func<Vector2> local;
            /// <summary>The point may lie on ink that no tint can change (the comic step: its whole visible band is the sprite's
            /// black bottom line); then the pixel cannot show the tint and only the tint value is checked.</summary>
            public bool mayBeInk;
            public readonly Dictionary<string,Color32> drawn=new Dictionary<string,Color32>();
            public readonly Dictionary<string,Color> tint=new Dictionary<string,Color>();
        }
        /// <summary>Reads every point in each style (the first is the one checked, the other two span its line).</summary>
        private IEnumerator TintPass20X(string screen,string[] themes,IList<TintPoint20X> points,List<string> report,List<string> failures)
        {
            foreach(string theme in themes)
            {
                app.ApplyTheme11(theme);yield return Art073Settle(.35f);
                foreach(var p in points)
                {
                    var target=p.target();Assert.That(target,Is.Not.Null,screen+" "+p.name);
                    p.tint[theme]=target.color;
                    if(theme==themes[0]&&!SameTint20X(target.color,p.key()))failures.Add(screen+" "+p.name+": tint #"+ColorUtility.ToHtmlStringRGBA(target.color)+", key #"+ColorUtility.ToHtmlStringRGBA(p.key()));
                    var point=ScreenPixel20X(target.rectTransform,p.local());
                    yield return TargetPixel20X(target,point,c=>p.drawn[theme]=c);
                }
            }
            foreach(var p in points)TintCheck20X(screen,themes,p,report,failures);
        }
        /// <summary>
        /// Linear light: drawn(t) = under x (1 - a) + a x sprite x t, so the drawn pixel is a straight line in the tint between
        /// the black and the white style; the probe's pixel must lie on it at the probe's tint (3 levels for rounding).
        /// </summary>
        private static void TintCheck20X(string screen,string[] themes,TintPoint20X p,List<string> report,List<string> failures)
        {
            Color32 lo=p.drawn[themes[2]],hi=p.drawn[themes[1]],seen=p.drawn[themes[0]];
            Color t0=p.tint[themes[2]],t1=p.tint[themes[1]],tp=p.tint[themes[0]];
            float Channel(byte l,byte h,float c0,float c1,float c)
            {
                float a=Mathf.GammaToLinearSpace(l/255f),b=Mathf.GammaToLinearSpace(h/255f);
                float x0=Mathf.GammaToLinearSpace(c0),x1=Mathf.GammaToLinearSpace(c1),x=Mathf.GammaToLinearSpace(c);
                float k=Mathf.Abs(x1-x0)<1e-4f?0:(x-x0)/(x1-x0);
                return Mathf.LinearToGammaSpace(Mathf.Clamp01(a+(b-a)*k))*255;
            }
            var expected=new Color32((byte)Mathf.RoundToInt(Channel(lo.r,hi.r,t0.r,t1.r,tp.r)),(byte)Mathf.RoundToInt(Channel(lo.g,hi.g,t0.g,t1.g,tp.g)),(byte)Mathf.RoundToInt(Channel(lo.b,hi.b,t0.b,t1.b,tp.b)),255);
            int delta=Math.Max(Math.Abs(expected.r-seen.r),Math.Max(Math.Abs(expected.g-seen.g),Math.Abs(expected.b-seen.b)));
            string line=screen+" "+p.name+": tint #"+ColorUtility.ToHtmlStringRGB(tp)+", drawn "+C20X(seen)+", sprite x tint "+C20X(expected)+" (from "+C20X(lo)+" at #"+ColorUtility.ToHtmlStringRGB(t0)+" and "+C20X(hi)+" at #"+ColorUtility.ToHtmlStringRGB(t1)+"), largest channel difference "+delta;
            report.Add(line);if(delta>3)failures.Add(line);
            if(lo.r==hi.r&&lo.g==hi.g&&lo.b==hi.b)
            {
                if(p.mayBeInk&&hi.r+hi.g+hi.b<=6)report.Add(screen+" "+p.name+": the sprite is ink at this point in every tint (drawn "+C20X(hi)+"); the tint value is checked, the pixel cannot show it");
                else failures.Add(screen+" "+p.name+": the black and white styles drew the same pixel; the point does not show the tinted image");
            }
        }

        private static Graphic StepOf20X(UnityEngine.UI.Button b)=>b.transform.Find("Step").GetComponent<Image>();
        /// <summary>A point of the step band left under the face, in the step's own units: the step spans button y 8 to h + 2
        /// and the face 0 to h - 7, so the band is the step's last 9 units; 7 units above its foot the default bevel sprite
        /// is white (its dark foot line is the last 3 units).</summary>
        private static Vector2 StepPoint20X(Graphic step){var r=step.rectTransform.rect;return new Vector2(r.width*.5f,r.height-7);}

        [UnityTest] public IEnumerator PLAY_W20X_02_PageSceneOnNonPlayScreensAndTintsAsDrawn()
        {
            Assert.That(Screen.width==1920&&Screen.height==1080||Screen.width==1280&&Screen.height==720,Is.True,"Run at actual 1920x1080 or 1280x720.");
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);int previewValue=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            string savedTheme=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            var report=new List<string>();var failures=new List<string>();
            Texture2D table=Solid20X(ProbeTable20X),page=Solid20X(ProbePage20X);
            string[] probe={"w20x-probe","w20x-white","w20x-black"},comicSet={"comic","w20x-comic-white","w20x-comic-black"};
            try
            {
                PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);PlayerPrefs.SetInt("reduced",1);GameApp.CleanCapture11=true;
                yield return LoadBootstrap();
                var comic=ThemeRegistry.Find("comic");Assert.That(comic,Is.Not.Null);comic.EnsureLoaded();
                ThemeRegistry.RegisterForTests(GameTheme.CreateForTests("w20x-probe",Tints20X(null,ProbeStep20X,ProbeStepPrimary20X,ProbeSection20X,ProbeCaption20X),table,page));
                ThemeRegistry.RegisterForTests(GameTheme.CreateForTests("w20x-white",Tints20X(null,"FFFFFF"),table,page));
                ThemeRegistry.RegisterForTests(GameTheme.CreateForTests("w20x-black",Tints20X(null,"000000"),table,page));
                ThemeRegistry.RegisterForTests(GameTheme.CreateForTests("w20x-comic-white",Tints20X(comic.Palette,"FFFFFF"),comic.TableScene,comic.PageScene,comic));
                ThemeRegistry.RegisterForTests(GameTheme.CreateForTests("w20x-comic-black",Tints20X(comic.Palette,"000000"),comic.TableScene,comic.PageScene,comic));
                // Lazy loading: page_scene is read with the other table pictures when a style is first used; only comic ships one.
                foreach(var theme in ThemeRegistry.All.Where(t=>!t.Id.StartsWith("w20x",StringComparison.Ordinal)))
                {
                    theme.EnsureLoaded();
                    report.Add("style "+theme.Id+": page_scene "+(theme.PageScene!=null?theme.PageScene.width+"x"+theme.PageScene.height:"none")+", table_scene "+(theme.TableScene!=null?theme.TableScene.width+"x"+theme.TableScene.height:"the scene's own"));
                    Assert.That(theme.PageScene!=null,Is.EqualTo(theme.Id=="comic"),theme.Id+" page_scene");
                }

                // ---- A. Which picture each screen draws (the backdrop table), probe style; felt pixel at design (6, 540).
                IEnumerator Expect(string screen,bool play,bool feltVisible=true)
                {
                    yield return Art073Settle(.3f);yield return new WaitForEndOfFrame();
                    var shown=FeltPicture20X();var expected=play?(Texture)table:page;
                    if(!ReferenceEquals(shown,expected))failures.Add(screen+": _TableMap is "+(shown!=null?shown.name:"null")+", expected the "+(play?"table":"page")+" picture");
                    if(app.PlayBackdrop11!=play)failures.Add(screen+": PlayBackdrop11 is "+app.PlayBackdrop11);
                    if(!feltVisible){report.Add("backdrop "+screen+": "+(play?"table_scene":"page_scene")+" (the screen covers the felt)");yield break;}
                    var felt=Pixel20X(ScreenPixel20X(W14Main,new Vector2(6,540)));
                    string seen=Picture20X(felt);if(seen!=(play?"table":"page"))failures.Add(screen+": the felt pixel at (6, 540) shows "+seen);
                    report.Add("backdrop "+screen+": "+(play?"table_scene":"page_scene")+", felt pixel "+C20X(felt));
                }
                app.ApplyTheme11("w20x-probe");
                yield return Expect("menu",false);
                Click("MenuSettings");yield return Expect("settings over the menu",false);Click("CloseDialog");
                StartRun("72002");yield return Expect("stage select",false);
                W11Apply(W11Scene(W11Cards3,new[]{4,4,6,2,4},"bbbrb",2,true));yield return W11Settle();
                yield return Expect("play",true);
                Click("Controller_"+app.Session.State.controllers[0].instanceId);yield return Expect("card details over the play screen",true);Click("CloseDialog");
                app.ShowHandbook();yield return Expect("handbook over the play screen",true);Click("CloseDialog");
                app.ShowCardGallery13(0);yield return Expect("card gallery",false,false);app.HideCardGallery13();yield return Expect("play screen after the gallery",true);
                yield return IdentityEndStage20X(3);yield return Expect("stage settlement",true);
                Click("SettlementContinue");yield return WaitForIdle();Assert.That(app.Session.State.phase,Is.EqualTo("Shop"));
                yield return Expect("shop",false);
                Click("Offer_"+app.Session.State.shop.offers.First(o=>o.category=="controller").offerId);yield return Expect("card details over the shop",false);Click("CloseDialog");
                Click("Handbook");yield return Expect("handbook over the shop",false);Click("CloseDialog");
                app.ShowDiceStyleLegend073();yield return Expect("dice styles over the shop",false);Click("CloseDialog");

                // ---- B. Tints as drawn at named points. Steps: on the run end, a secondary button and the primary 再开一局.
                yield return IdentityEndStage20X(app.Config.stages.Length-1);Click("SettlementContinue");yield return WaitForIdle();
                Assert.That(app.Session.State.phase,Is.EqualTo("RunWon"));yield return Expect("run end",false);
                foreach(var set in new[]{probe,comicSet})
                {
                    // 设置 / 暂停 is a secondary UiKit.Button, 再开一局 a primary one (UiKit.ActionButton).
                    const string secondary="Pause",primary="Again";
                    yield return TintPass20X("run end",set,new[]
                    {
                        new TintPoint20X{name=secondary+"/Step (buttonStep)",target=()=>StepOf20X(Button(secondary)),key=()=>UiKit.ButtonStep,local=()=>StepPoint20X(StepOf20X(Button(secondary))),mayBeInk=set==comicSet},
                        new TintPoint20X{name=primary+"/Step (buttonStepPrimary)",target=()=>StepOf20X(Button(primary)),key=()=>UiKit.ButtonStepPrimary,local=()=>StepPoint20X(StepOf20X(Button(primary))),mayBeInk=set==comicSet},
                    },report,failures);
                }
                // The dice-style page's sample cells take section when a style sets it.
                foreach(var set in new[]{probe,comicSet})
                {
                    var cell=new TintPoint20X{name="StyleCell0 (section)",target=()=>FindActive<Image>("StyleCell0"),key=()=>UiKit.Section(Color.white),local=()=>new Vector2(24,100)};
                    foreach(string theme in set){app.ApplyTheme11(theme);app.ShowDiceStyleLegend073();yield return Art073Settle(.35f);
                        cell.tint[theme]=cell.target().color;if(theme==set[0]&&!SameTint20X(cell.tint[theme],cell.key()))failures.Add("dice styles StyleCell0: tint #"+ColorUtility.ToHtmlStringRGBA(cell.tint[theme]));
                        yield return TargetPixel20X(cell.target(),ScreenPixel20X(cell.target().rectTransform,cell.local()),c=>cell.drawn[theme]=c);Click("CloseDialog");}
                    TintCheck20X("dice styles",set,cell,report,failures);
                }
                // The pool tab on the play screen (caption).
                W11Apply(W11Scene(W11Cards3,new[]{4,4,6,2,4},"bbbrb",2,true));yield return W11Settle();
                foreach(var set in new[]{probe,comicSet})
                    yield return TintPass20X("play",set,new[]{new TintPoint20X{name="PoolTab11 (caption)",target=()=>FindActive<Image>("PoolTab11"),key=()=>UiKit.Caption(UiKit.Panel),local=()=>new Vector2(50,10)}},report,failures);
                // W20Xb item C: the tray's other two tabs take caption as well, the pool summary (a pool card in the build, the
                // hand rolled) and the page tab (more than ten dice in the pool); the styles without caption keep Panel on all three.
                W11Apply(W11Scene(new[]{"C70","C39","C139"},new[]{4,4,6,2,4,5,1,3,6,2,4,5},"bbbrbbrbbbrb",0,true));yield return W11Settle();
                Assert.That(HasActive("PoolSummaryTab11"),Is.True,"The pool card draws the summary tab.");Assert.That(HasActive("PoolPageTab11"),Is.True,"Twelve dice page the pool.");
                foreach(var set in new[]{probe,comicSet})
                    yield return TintPass20X("play, twelve dice and a pool card",set,new[]
                    {
                        new TintPoint20X{name="PoolSummaryTab11 (caption)",target=()=>FindActive<Image>("PoolSummaryTab11"),key=()=>UiKit.Caption(UiKit.Panel),local=()=>new Vector2(FindActive<Image>("PoolSummaryTab11").rectTransform.rect.width*.5f,10)},
                        new TintPoint20X{name="PoolPageTab11 (caption)",target=()=>FindActive<Image>("PoolPageTab11"),key=()=>UiKit.Caption(UiKit.Panel),local=()=>new Vector2(FindActive<Image>("PoolPageTab11").rectTransform.rect.width*.5f,10)},
                    },report,failures);
                foreach(string theme in new[]{"default","swiss","sancai"})
                {
                    app.ApplyTheme11(theme);yield return Art073Settle(.3f);
                    foreach(string tab in new[]{"PoolTab11","PoolSummaryTab11","PoolPageTab11"})
                    {
                        var seen=FindActive<Image>(tab).color;
                        if(!SameTint20X(seen,UiKit.Panel))failures.Add(theme+" "+tab+": #"+ColorUtility.ToHtmlStringRGBA(seen)+", expected Panel #"+ColorUtility.ToHtmlStringRGBA(UiKit.Panel));
                        else report.Add(theme+" "+tab+": Panel #"+ColorUtility.ToHtmlStringRGB(seen)+" (no caption)");
                    }
                }
                // The shop's sections (section) and their header strips (caption).
                yield return W14Load(W14ShopFixture(false));
                foreach(var set in new[]{probe,comicSet})
                    yield return TintPass20X("shop",set,new[]
                    {
                        new TintPoint20X{name="ControllerShelf (section)",target=()=>FindActive<Image>("ControllerShelf"),key=()=>UiKit.Section(UiKit.Panel),local=()=>new Vector2(1100,300)},
                        new TintPoint20X{name="ControllerShelf/HeaderStrip (caption)",target=()=>FindActive<RectTransform>("ControllerShelf").Find("HeaderStrip").GetComponent<Image>(),key=()=>UiKit.Caption(UiKit.PanelLight),local=()=>new Vector2(800,30)},
                        new TintPoint20X{name="Build14 (section)",target=()=>FindActive<Image>("Build14"),key=()=>UiKit.Section(UiKit.Panel),local=()=>new Vector2(800,90)},
                        new TintPoint20X{name="DiceCounter/HeaderStrip (caption)",target=()=>FindActive<RectTransform>("DiceCounter").Find("HeaderStrip").GetComponent<Image>(),key=()=>UiKit.Caption(UiKit.PanelLight),local=()=>new Vector2(380,30)},
                    },report,failures);
                // The fallbacks in the shipped styles that set none of the keys: today's values, read at draw time.
                foreach(string theme in new[]{"default","swiss","sancai"})
                {
                    app.ApplyTheme11(theme);yield return Art073Settle(.3f);
                    void Same(string what,Color seen,Color expected){if(!SameTint20X(seen,expected))failures.Add(theme+" "+what+": #"+ColorUtility.ToHtmlStringRGBA(seen)+", expected #"+ColorUtility.ToHtmlStringRGBA(expected));}
                    Same("ControllerShelf",FindActive<Image>("ControllerShelf").color,UiKit.Panel);
                    Same("Build14",FindActive<Image>("Build14").color,UiKit.Panel);
                    Same("HeaderStrip",FindActive<RectTransform>("ControllerShelf").Find("HeaderStrip").GetComponent<Image>().color,UiKit.PanelLight);
                    Assert.That(UiKit.ButtonStep,Is.EqualTo(UiKit.Hex("8F453A")));Assert.That(UiKit.ButtonStepPrimary,Is.EqualTo(UiKit.Hex("B56B29")));
                    Assert.That(UiKit.Section(Color.white),Is.EqualTo(Color.white));Assert.That(UiKit.Caption(UiKit.Panel),Is.EqualTo(UiKit.Panel));
                    // Without page_scene the shop keeps the table picture: the style's table_scene, or the scene's own for default.
                    var authored=GameObject.Find("Art07Felt").GetComponent<Renderer>().sharedMaterial.GetTexture("_TableMap");
                    Assert.That(FeltPicture20X(),Is.SameAs(ThemeRegistry.Current.TableScene!=null?(Texture)ThemeRegistry.Current.TableScene:authored),theme+": without page_scene the shop keeps table_scene.");
                }

                // ---- C. comic: its own page_scene on the shop and its table_scene on the play screen.
                app.ApplyTheme11("comic");yield return Art073Settle(.3f);
                Assert.That(FeltPicture20X(),Is.SameAs(comic.PageScene),"comic shop: page_scene");
                var comicShop=Pixel20X(ScreenPixel20X(W14Main,new Vector2(6,540)));
                Click("LeaveShop");yield return WaitForIdle();yield return Art073Settle(.3f);Assert.That(app.Session.State.phase,Is.EqualTo("StagePreview"));
                Assert.That(FeltPicture20X(),Is.SameAs(comic.PageScene),"comic stage select: page_scene");
                Click("StartStage");yield return WaitForIdle();yield return Art073Settle(.3f);
                Assert.That(FeltPicture20X(),Is.SameAs(comic.TableScene),"comic play screen: table_scene");
                var comicPlay=Pixel20X(ScreenPixel20X(W14Main,new Vector2(6,540)));
                report.Add("comic felt pixel at (6, 540): shop "+C20X(comicShop)+" (page_scene), play "+C20X(comicPlay)+" (table_scene)");

                // ---- D. No flash: frame by frame through shop -> stage select -> play and play -> settlement -> shop.
                app.ApplyTheme11("w20x-probe");
                yield return W14Load(W14ShopFixture(false));app.ApplyTheme11("w20x-probe");yield return Art073Settle(.3f);
                var frames=new List<string>();
                IEnumerator Watch(string step,Action act)
                {
                    for(int f=-2;f<10;f++)
                    {
                        if(f==0)act();
                        yield return new WaitForEndOfFrame();
                        bool play=app.PlayBackdrop11;var felt=Pixel20X(ScreenPixel20X(W14Main,new Vector2(6,540)));string seen=Picture20X(felt);
                        string phase=app.ViewState?.phase??"menu";
                        frames.Add(step+" frame "+f+": "+phase+", "+(play?"table":"page")+" expected, felt "+seen);
                        if(seen!=(play?"table":"page"))failures.Add("flash at "+step+" frame "+f+": "+phase+" shows the "+seen+" picture");
                        if(f<9)yield return null;
                    }
                }
                yield return Watch("shop -> stage select",()=>Click("LeaveShop"));
                Assert.That(app.Session.State.phase,Is.EqualTo("StagePreview"));
                yield return Watch("stage select -> play",()=>Click("StartStage"));
                Assert.That(app.Session.State.phase,Is.EqualTo("HandReady"));
                W11Apply(IdentityWinningHand20X(app.Session.State.stage.stageIndex));yield return W11Settle();
                yield return Watch("play -> scoring",()=>{var r=app.Send(new SessionCommand{kind=DiceCraft.Core.CommandKind.Submit});Assert.That(r.accepted,Is.True,r.error);});
                app.SkipVisual();yield return W11Settle();
                yield return Watch("play -> stage settlement",()=>{var r=app.Send(new SessionCommand{kind=DiceCraft.Core.CommandKind.EndStage});Assert.That(r.accepted,Is.True,r.error);});
                app.SkipVisual();yield return W11Settle();Assert.That(app.Session.State.phase,Is.EqualTo("StageSettlement"));
                yield return Watch("stage settlement -> shop",()=>Click("SettlementContinue"));
                Assert.That(app.Session.State.phase,Is.EqualTo("Shop"));
                report.AddRange(frames);
            }
            finally
            {
                app.HideCardGallery13();GameApp.CleanCapture11=false;
                foreach(string id in new[]{"w20x-probe","w20x-white","w20x-black","w20x-comic-white","w20x-comic-black"})ThemeRegistry.UnregisterForTests(id);
                app.ApplyTheme11(GameTheme.DefaultId);
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,previewValue);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,savedTheme);PlayerPrefs.Save();
                UObj.Destroy(table);UObj.Destroy(page);
                File.WriteAllLines(Path.Combine(W20XOutput("w20x-02"),"w20x-02.txt"),report.Concat(new[]{"","PROBLEMS "+failures.Count}).Concat(failures));
            }
            foreach(string line in report)TestContext.WriteLine(line);
            Assert.That(failures,Is.Empty,string.Join("\n",failures.Take(40)));
        }

    }
}
