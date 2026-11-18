using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DiceCraft.Tests
{
    /// <summary>
    /// V1.1 task W20Y2 (THEME_SPEC §2, the sentence after the table): the label of a disabled second-level button
    /// (GameApp.SolidButton11) is LabelOn11(face, muted), the rule the enabled labels use, with muted as the label's own
    /// colour: muted where it reaches 4.5:1 on the face as drawn, else the palette colour (ink, paper, text colour) with the
    /// best contrast. Before W20Y2 it was always muted, 1.60:1 on the disabled face of a light buttonSecondary.
    ///
    /// Styles as in PLAY_W20Y_04, built in memory for the run (nothing under Resources): "w20y2-probe" is the default style
    /// with buttonSecondary D8B26A (disabled face AE9A66), "w20y2-white" and "w20y2-black" the same with FFFFFF and 000000,
    /// which span the sprite x tint line of the W20X-02 check. Each face is read at its centre with what is drawn over it
    /// switched off (the W20Y-04 method).
    ///
    /// Buttons: on the play screen after a scored hand, while the app is busy, 查看骰子 and 上一手明细 (disabled) and
    /// 跳过演出 (enabled, the busy row's only action). The busy frame is the first frame of a throw: GameApp sets
    /// Session.Busy and redraws, and under reduced motion (on for the run, as in PLAY_W20Y_04) a throw ends in that same
    /// call, so the test sets the flag the same way and holds it for the pass. In the full shop a card's price button
    /// (卡牌已满 10 张) and an item's (道具栏已满), disabled; PriceButton13 hides the button's own label and writes Price and
    /// PriceWord13 with the same rule.
    ///
    /// Every label must be LabelOn11's pick for the drawn pixel, and every face its tint as before (W20Y2 changes no face).
    /// A disabled label must reach 4.5:1 on the drawn pixel; in the shipped styles (default, swiss, sancai, comic) it must
    /// be muted; in the probe style muted must be under 4.5:1 on the disabled face, so the probe reaches the fallback.
    /// Report: artifacts/cache/v11/w20y2/&lt;size&gt;/w20y2-01/w20y2-01.txt.
    /// </summary>
    public sealed partial class GameplayPlayModeTests
    {
        private const string Probe20Y2="w20y2-probe";
        private static readonly string[] ProbeSet20Y2={Probe20Y2,"w20y2-white","w20y2-black"};

        private static string W20Y2Output(string name)
        {
            string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","artifacts","cache","v11","w20y2",Screen.width+"x"+Screen.height,name));
            Directory.CreateDirectory(dir);return dir;
        }
        private static Label20Y[] DisabledPlainLabel20Y2=>new[]{new Label20Y{name="Label",own=Own20Y.Muted}};

        /// <summary>
        /// The W20Y-04 face pass (FacePass20Y) with the W20Y2 checks on the labels of a disabled face: LabelOn11's pick for the
        /// drawn pixel with muted as the label's own colour, at least 4.5:1 there, muted in a shipped style; in the probe style
        /// muted itself is under 4.5:1 on that face.
        /// </summary>
        private IEnumerator FacePass20Y2(string screen,string[] themes,IList<Face20Y> faces,bool probeSet,List<string> report,List<string> failures)
        {
            var points=faces.Select(f=>new TintPoint20X{name=f.name}).ToArray();
            foreach(string theme in themes)
            {
                app.ApplyTheme11(theme);yield return Art073Settle(.35f);
                Assert.That(app.ThemeId11,Is.EqualTo(theme));
                bool shipped=Shipped20Y.Contains(theme);
                for(int i=0;i<faces.Count;i++)
                {
                    var f=faces[i];string at=screen+" ["+theme+"] "+f.name;
                    var button=f.button();
                    if(button.interactable!=f.enabled)failures.Add(at+": interactable is "+button.interactable+", expected "+f.enabled);
                    var face=(Image)button.targetGraphic;
                    Color want=ExpectedFace20Y(f.enabled);points[i].tint[theme]=face.color;
                    if(!SameTint20X(face.color,want))failures.Add(at+": face tint #"+ColorUtility.ToHtmlStringRGBA(face.color)+", expected #"+ColorUtility.ToHtmlStringRGBA(want));
                    var r=face.rectTransform.rect;var centre=new Vector2(r.width*.5f,r.height*.5f);var pixel=ScreenPixel20X(face.rectTransform,centre);
                    Color32 drawn=default;
                    yield return TargetPixel20X(face,pixel,c=>drawn=c);
                    points[i].drawn[theme]=drawn;
                    float muted=GameApp.Contrast11(UiKit.Muted,(Color)drawn);
                    var line=new List<string>{at+": face "+(f.enabled?"enabled":"disabled")+", "+(face.sprite!=null?face.sprite.name:"no sprite")+", tint #"+ColorUtility.ToHtmlStringRGB(face.color)+
                        ", centre ("+R20Y(centre.x)+", "+R20Y(centre.y)+") of "+R20Y(r.width)+"x"+R20Y(r.height)+" at screen pixel "+pixel.x+","+pixel.y+" drawn "+C20X(drawn)+
                        "; muted #"+ColorUtility.ToHtmlStringRGB(UiKit.Muted)+" there "+R20Y(muted)+":1"};
                    if(!f.enabled&&theme==Probe20Y2&&muted>=GameApp.MinContrast11)failures.Add(at+": muted reaches "+R20Y(muted)+":1 on the probe's disabled face; the probe would not reach the fallback");
                    foreach(var l in f.labels)
                    {
                        var t=button.transform.Find(l.name)?.GetComponent<TextMeshProUGUI>();
                        if(t==null||!t.gameObject.activeInHierarchy){failures.Add(at+": no active label "+l.name);continue;}
                        Color pick=LabelPick20Y((Color)drawn,OwnColour20Y(l.own),out string rule);
                        float seen=GameApp.Contrast11(t.color,(Color)drawn);
                        string said=at+" "+l.name+" '"+t.text.Replace("\n","/")+"': #"+ColorUtility.ToHtmlStringRGB(t.color)+", "+R20Y(seen)+":1 on the drawn "+C20X(drawn)+
                            "; LabelOn11 picks #"+ColorUtility.ToHtmlStringRGB(pick)+" ("+rule+")";
                        line.Add("  "+said);
                        if(!Same8Bit20Y(t.color,pick))failures.Add(said+": not LabelOn11's pick");
                        if(f.enabled)continue;
                        if(seen<GameApp.MinContrast11)failures.Add(said+": a disabled label under 4.5:1");
                        if(shipped&&!Same8Bit20Y(t.color,UiKit.Muted))failures.Add(said+": a shipped style's disabled label is not muted #"+ColorUtility.ToHtmlStringRGB(UiKit.Muted));
                    }
                    report.AddRange(line);
                }
            }
            if(!probeSet)yield break;
            foreach(var p in points)
            {
                TintCheck20X(screen,themes,p,report,failures);
                // Where every style draws its own tint (8-bit, 3 levels), the sprite is opaque white there: sprite x tint = tint.
                bool white=themes.All(t=>Delta20Y(p.drawn[t],(Color32)p.tint[t])<=3);
                Color32 seen=p.drawn[themes[0]],tint=p.tint[themes[0]];
                string direct=screen+" "+p.name+": sprite x tint with the sprite "+(white?"opaque white at the point (white style "+C20X(p.drawn[themes[1]])+" for tint #"+ColorUtility.ToHtmlStringRGB(p.tint[themes[1]])+", black style "+C20X(p.drawn[themes[2]])+" for #"+ColorUtility.ToHtmlStringRGB(p.tint[themes[2]])+")":"not plain white at the point")+
                    ": probe tint "+C20X(tint)+", drawn "+C20X(seen)+", largest channel difference "+Delta20Y(seen,tint);
                report.Add(direct);
                if(!white)failures.Add(direct+" (the face centre was expected to show its tint unchanged)");
                else if(Delta20Y(seen,tint)>3)failures.Add(direct);
            }
        }

        [UnityTest] public IEnumerator PLAY_W20Y2_01_DisabledSecondLevelLabelsReadOnTheDrawnFace()
        {
            Assert.That(Screen.width==1920&&Screen.height==1080||Screen.width==1280&&Screen.height==720,Is.True,"Run at actual 1920x1080 or 1280x720.");
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);int previewValue=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            string savedTheme=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            var report=new List<string>();var failures=new List<string>();bool heldBusy=false;
            try
            {
                PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);PlayerPrefs.SetInt("reduced",1);GameApp.CleanCapture11=true;
                yield return LoadBootstrap();
                ThemeRegistry.RegisterForTests(GameTheme.CreateForTests(Probe20Y2,Secondary20Y(ProbeSecondary20Y)));
                ThemeRegistry.RegisterForTests(GameTheme.CreateForTests(ProbeSet20Y2[1],Secondary20Y("FFFFFF")));
                ThemeRegistry.RegisterForTests(GameTheme.CreateForTests(ProbeSet20Y2[2],Secondary20Y("000000")));
                foreach(string id in Shipped20Y)
                {
                    var theme=ThemeRegistry.Find(id);Assert.That(theme,Is.Not.Null,id);
                    Assert.That(theme.Palette.buttonSecondary.a,Is.Zero,id+" sets no buttonSecondary.");
                }
                report.Add("probe "+Probe20Y2+" buttonSecondary #"+ProbeSecondary20Y+"; "+ProbeSet20Y2[1]+" FFFFFF; "+ProbeSet20Y2[2]+" 000000 (default palette otherwise, default sprites)");
                IEnumerator Both(string screen,Face20Y[] faces)
                {
                    yield return FacePass20Y2(screen,ProbeSet20Y2,faces,true,report,failures);
                    yield return FacePass20Y2(screen,Shipped20Y,faces,false,report,failures);
                }

                // ---- Play: a scored hand first (so 上一手明细 is drawn), then the busy frame of a throw.
                StartRun("72021");
                W11Apply(IdentityWinningHand20X(3));yield return W11Settle();
                var submit=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(submit.accepted,Is.True,submit.error);
                app.SkipVisual();yield return W11Settle();
                Assert.That(app.Session.State.phase,Is.EqualTo("HandReady"));Assert.That(app.Session.State.lastSubmission,Is.Not.Null);
                Assert.That(Button("ViewDice").interactable&&Button("PreviousSubmission").interactable,Is.True,"Idle: 查看骰子 and 上一手明细 can be used.");
                app.Session.Busy=true;heldBusy=true;app.Refresh();Canvas.ForceUpdateCanvases();
                Assert.That(app.IsBusy,Is.True);Assert.That(HasActive("Skip"),Is.True,"The busy action row offers 跳过演出.");
                yield return Both("play, busy",new[]
                {
                    new Face20Y{name="ViewDice (查看骰子, disabled while busy)",button=()=>Button("ViewDice"),enabled=false,labels=DisabledPlainLabel20Y2},
                    new Face20Y{name="PreviousSubmission (上一手明细, disabled while busy)",button=()=>Button("PreviousSubmission"),enabled=false,labels=DisabledPlainLabel20Y2},
                    new Face20Y{name="Skip (跳过演出, enabled)",button=()=>Button("Skip"),enabled=true,labels=PlainLabel20Y},
                });
                Assert.That(app.IsBusy,Is.True,"Nothing ended the busy frame during the pass.");
                app.Session.Busy=false;heldBusy=false;app.Refresh();yield return W11Settle();

                // ---- The full shop: ten cards held and the item bar full, so both price buttons are disabled with their reason.
                yield return W14Load(W14ShopFixture(true));
                var full=app.Session.State.shop.offers.Where(o=>string.IsNullOrEmpty(o.groupId)).ToArray();
                string fullCard=full.First(o=>o.category=="controller").offerId,item=full.First(o=>o.category=="consumable").offerId;
                foreach(string id in new[]{fullCard,item})
                {
                    var word=Button("Buy_"+id).transform.Find("PriceWord13")?.GetComponent<TextMeshProUGUI>();
                    Assert.That(word,Is.Not.Null,"Buy_"+id+" has its reason row.");Assert.That(word.text,Does.Contain("已满"),"Buy_"+id+" says why it cannot be bought.");
                }
                yield return Both("full shop",new[]
                {
                    new Face20Y{name="Buy_"+fullCard+" (a card's price button, disabled)",button=()=>Button("Buy_"+fullCard),enabled=false,labels=PriceLabels20Y(false)},
                    new Face20Y{name="Buy_"+item+" (an item's price button, disabled)",button=()=>Button("Buy_"+item),enabled=false,labels=PriceLabels20Y(false)},
                });
            }
            finally
            {
                if(heldBusy&&app!=null&&app.Session!=null)app.Session.Busy=false;
                GameApp.CleanCapture11=false;
                foreach(string id in ProbeSet20Y2)ThemeRegistry.UnregisterForTests(id);
                app.ApplyTheme11(GameTheme.DefaultId);
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,previewValue);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,savedTheme);PlayerPrefs.Save();
                File.WriteAllLines(Path.Combine(W20Y2Output("w20y2-01"),"w20y2-01.txt"),report.Concat(new[]{"","PROBLEMS "+failures.Count}).Concat(failures));
            }
            foreach(string line in report)TestContext.WriteLine(line);
            Assert.That(failures,Is.Empty,string.Join("\n",failures.Take(40)));
        }
    }
}
