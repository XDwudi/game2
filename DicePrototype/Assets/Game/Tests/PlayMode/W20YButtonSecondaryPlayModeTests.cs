using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
    /// V1.1 task W20Y item 4 (THEME_SPEC §2 buttonSecondary): the face of every second-level button (GameApp.SolidButton11)
    /// is the style's buttonSecondary; a disabled one is that colour a further 25 % toward panelLight; the labels on it
    /// still take their colour from the face as drawn (LabelOn11, W11b).
    ///
    /// Styles are built in memory for the run (ThemeRegistry.RegisterForTests; nothing under Resources): "w20y-probe" is the
    /// default style with buttonSecondary D8B26A, "w20y-white" and "w20y-black" the same with FFFFFF and 000000. A tinted
    /// image is drawn as (1 - a) x under + a x sprite x tint in linear light, so at a point where the face is the top graphic
    /// the probe's pixel lies on the line from the black style's pixel to the white style's pixel at the probe's tint
    /// (the W20X-02 check, 3 levels for rounding); where the sprite is opaque white (every style draws its own tint there)
    /// the probe's pixel is its tint itself. The point is the centre of the face; the labels and icons over it are switched
    /// off for that frame. Each label on the face (Label, or a price button's Price and PriceWord13) must be the colour
    /// LabelOn11 picks for the drawn pixel: its own colour (text colour, gold for a price, muted when the button is
    /// disabled) where that reaches 4.5:1, else the palette colour (ink, paper, text colour) with the best contrast.
    ///
    /// Buttons: on the play screen the action row's secondary button (重掷, then 取消选择 with a die chosen), 查看骰子, and
    /// after a scored hand 上一手明细 and the score row's 完整记录 chip; in the common shop a card's price button, a goods
    /// row's price button and 查看本章; in the full shop a card's price button (卡牌已满 10 张) and an item's (道具栏已满),
    /// both disabled. The shipped styles (default, swiss, sancai, comic) set no buttonSecondary: at the same points their
    /// faces are the face drawn before W20Y, lerp(background, panel, .55) (disabled: a further 25 % toward panelLight), and
    /// their labels follow the same rule. Report: artifacts/cache/v11/w20y/&lt;size&gt;/w20y-04/w20y-04.txt.
    /// </summary>
    public sealed partial class GameplayPlayModeTests
    {
        private const string ProbeSecondary20Y="D8B26A";
        private static readonly string[] Shipped20Y={"default","swiss","sancai","comic"};

        private static string W20YOutput(string name)
        {
            string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","artifacts","cache","v11","w20y",Screen.width+"x"+Screen.height,name));
            Directory.CreateDirectory(dir);return dir;
        }
        private static ThemePalette Secondary20Y(string hex)
        {
            var p=new ThemePalette();
            Assert.That(ThemePalette.TryHex(hex,out p.buttonSecondary),Is.True,hex);
            return p;
        }
        /// <summary>The face a second-level button should have in the active style, from the palette's fields (not through
        /// UiKit.ButtonSecondary): the style's buttonSecondary when it sets one, else the face drawn before W20Y,
        /// lerp(background, panel, .55); disabled, a further 25 % toward panelLight.</summary>
        private static Color ExpectedFace20Y(bool enabled)
        {
            var p=UiKit.Palette;var c=p.buttonSecondary.a>0?p.buttonSecondary:Color.Lerp(p.background,p.panel,.55f);
            return enabled?c:Color.Lerp(c,p.panelLight,.25f);
        }
        private enum Own20Y{TextColor,Gold,Muted}
        private static Color OwnColour20Y(Own20Y own)=>own==Own20Y.Gold?UiKit.Gold:own==Own20Y.Muted?UiKit.Muted:UiKit.TextColor;
        private sealed class Label20Y{public string name;public Own20Y own;}
        private sealed class Face20Y{public string name;public Func<UnityEngine.UI.Button> button;public bool enabled;public Label20Y[] labels;}
        private static Label20Y[] PlainLabel20Y=>new[]{new Label20Y{name="Label",own=Own20Y.TextColor}};
        private static Label20Y[] ShopLabel20Y(bool enabled)=>new[]{new Label20Y{name="Label",own=enabled?Own20Y.TextColor:Own20Y.Muted}};
        private static Label20Y[] PriceLabels20Y(bool enabled)=>new[]
        {
            new Label20Y{name="Price",own=enabled?Own20Y.Gold:Own20Y.Muted},new Label20Y{name="PriceWord13",own=enabled?Own20Y.TextColor:Own20Y.Muted},
        };
        private static string R20Y(float v)=>v.ToString("0.00",CultureInfo.InvariantCulture);
        /// <summary>LabelOn11's pick for a fill (W11b): the label's own colour where it reaches 4.5:1, else the palette colour
        /// (ink, paper, text colour, first best on a tie) with the best contrast.</summary>
        private static Color LabelPick20Y(Color fill,Color own,out string rule)
        {
            float mine=GameApp.Contrast11(own,fill);
            if(mine>=GameApp.MinContrast11){rule="its own colour, "+R20Y(mine)+":1";return own;}
            Color[] colours={UiKit.Ink,UiKit.Paper,UiKit.TextColor};string[] names={"ink","paper","text colour"};
            int best=0;float ratio=0;
            for(int i=0;i<colours.Length;i++){float r=GameApp.Contrast11(colours[i],fill);if(r>ratio){ratio=r;best=i;}}
            rule=names[best]+" "+R20Y(ratio)+":1 (its own colour "+R20Y(mine)+":1)"+(ratio<GameApp.PickContrast11?", under the 4.95:1 pick":"");
            return colours[best];
        }
        private static bool Same8Bit20Y(Color a,Color b){Color32 x=a,y=b;return x.r==y.r&&x.g==y.g&&x.b==y.b;}
        private static int Delta20Y(Color32 a,Color32 b)=>Math.Max(Math.Abs(a.r-b.r),Math.Max(Math.Abs(a.g-b.g),Math.Abs(a.b-b.b)));

        /// <summary>Applies each style in turn and reads every face: its tint against the expected face, the pixel at its centre
        /// with what is drawn over it switched off, and its labels against LabelOn11's pick for that pixel. For the probe set
        /// (probe, white, black) the pixels are then checked as sprite x tint.</summary>
        private IEnumerator FacePass20Y(string screen,string[] themes,IList<Face20Y> faces,bool probeSet,List<string> report,List<string> failures)
        {
            var points=faces.Select(f=>new TintPoint20X{name=f.name}).ToArray();
            foreach(string theme in themes)
            {
                app.ApplyTheme11(theme);yield return Art073Settle(.35f);
                Assert.That(app.ThemeId11,Is.EqualTo(theme));
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
                    var line=new List<string>{at+": face "+(f.enabled?"enabled":"disabled")+", "+(face.sprite!=null?face.sprite.name:"no sprite")+", tint #"+ColorUtility.ToHtmlStringRGB(face.color)+
                        ", centre ("+R20Y(centre.x)+", "+R20Y(centre.y)+") of "+R20Y(r.width)+"x"+R20Y(r.height)+" at screen pixel "+pixel.x+","+pixel.y+" drawn "+C20X(drawn)};
                    foreach(var l in f.labels)
                    {
                        var t=button.transform.Find(l.name)?.GetComponent<TextMeshProUGUI>();
                        if(t==null||!t.gameObject.activeInHierarchy){failures.Add(at+": no active label "+l.name);continue;}
                        Color pick=LabelPick20Y((Color)drawn,OwnColour20Y(l.own),out string rule);
                        string said=at+" "+l.name+" '"+t.text.Replace("\n","/")+"': #"+ColorUtility.ToHtmlStringRGB(t.color)+", LabelOn11 for the drawn "+C20X(drawn)+" picks #"+ColorUtility.ToHtmlStringRGB(pick)+" ("+rule+")";
                        line.Add("  "+said);
                        if(!Same8Bit20Y(t.color,pick))failures.Add(said);
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

        [UnityTest] public IEnumerator PLAY_W20Y_04_SecondLevelButtonsTakeButtonSecondaryAsDrawn()
        {
            Assert.That(Screen.width==1920&&Screen.height==1080||Screen.width==1280&&Screen.height==720,Is.True,"Run at actual 1920x1080 or 1280x720.");
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);int previewValue=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            string savedTheme=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            var report=new List<string>();var failures=new List<string>();
            string[] probe={"w20y-probe","w20y-white","w20y-black"};
            try
            {
                PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);PlayerPrefs.SetInt("reduced",1);GameApp.CleanCapture11=true;
                yield return LoadBootstrap();
                ThemeRegistry.RegisterForTests(GameTheme.CreateForTests("w20y-probe",Secondary20Y(ProbeSecondary20Y)));
                ThemeRegistry.RegisterForTests(GameTheme.CreateForTests("w20y-white",Secondary20Y("FFFFFF")));
                ThemeRegistry.RegisterForTests(GameTheme.CreateForTests("w20y-black",Secondary20Y("000000")));
                foreach(string id in Shipped20Y)
                {
                    var theme=ThemeRegistry.Find(id);Assert.That(theme,Is.Not.Null,id);
                    Assert.That(theme.Palette.buttonSecondary.a,Is.Zero,id+" sets no buttonSecondary.");
                }
                report.Add("probe buttonSecondary #"+ProbeSecondary20Y+"; white FFFFFF; black 000000 (default palette otherwise, default sprites)");
                IEnumerator Both(string screen,Face20Y[] faces)
                {
                    yield return FacePass20Y(screen,probe,faces,true,report,failures);
                    yield return FacePass20Y(screen,Shipped20Y,faces,false,report,failures);
                }

                // ---- Play: two dice locked, the rest rolled: 提交 and the secondary 重掷; 查看骰子.
                StartRun("72020");
                W11Apply(W11Scene(W11Cards3,new[]{4,4,6,2,4},"bbbrb",2,true));yield return W11Settle();
                yield return Both("play",new[]
                {
                    new Face20Y{name="Roll (secondary 重掷)",button=()=>Button("Roll"),enabled=true,labels=PlainLabel20Y},
                    new Face20Y{name="ViewDice (查看骰子)",button=()=>Button("ViewDice"),enabled=true,labels=PlainLabel20Y},
                });
                // A die chosen: the secondary is 取消选择.
                app.Session.TogglePending(app.Session.State.dice[4].dieId);app.Refresh();yield return W11Settle();
                yield return Both("play, a die chosen",new[]{new Face20Y{name="Clear (secondary 取消选择)",button=()=>Button("Clear"),enabled=true,labels=PlainLabel20Y}});
                // After a scored hand (the target reached, the stage goes on): 上一手明细 and the score row's 完整记录 chip.
                W11Apply(IdentityWinningHand20X(3));yield return W11Settle();
                var submit=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(submit.accepted,Is.True,submit.error);
                app.SkipVisual();yield return W11Settle();
                Assert.That(app.Session.State.phase,Is.EqualTo("HandReady"));
                yield return Both("play, after a scored hand",new[]
                {
                    new Face20Y{name="PreviousSubmission (上一手明细)",button=()=>Button("PreviousSubmission"),enabled=true,labels=PlainLabel20Y},
                    new Face20Y{name="StablePresentationDetails07 (完整记录)",button=()=>Button("StablePresentationDetails07"),enabled=true,labels=PlainLabel20Y},
                });

                // ---- The common shop: a card's price button, a goods row's price button, 查看本章.
                yield return W14Load(W14ShopFixture(false));
                var shop=app.Session.State.shop.offers.Where(o=>string.IsNullOrEmpty(o.groupId)).ToArray();
                string card=shop.First(o=>o.category=="controller").offerId;
                string good=shop.Where(o=>o.category=="hand_upgrade"||o.category=="color_upgrade"||o.category=="consumable"||o.category=="service")
                    .Select(o=>o.offerId).FirstOrDefault(id=>HasActive("Buy_"+id)&&Button("Buy_"+id).interactable);
                Assert.That(good,Is.Not.Null,"The common shop has a goods row that can be bought.");
                yield return Both("common shop",new[]
                {
                    new Face20Y{name="Buy_"+card+" (a card's price button)",button=()=>Button("Buy_"+card),enabled=true,labels=PriceLabels20Y(true)},
                    new Face20Y{name="Buy_"+good+" (a goods row's price button)",button=()=>Button("Buy_"+good),enabled=true,labels=PriceLabels20Y(true)},
                    new Face20Y{name="ViewCycle (查看本章)",button=()=>Button("ViewCycle"),enabled=true,labels=ShopLabel20Y(true)},
                });

                // ---- The full shop: ten cards held and the item bar full, so both price buttons are disabled with their reason.
                yield return W14Load(W14ShopFixture(true));
                var full=app.Session.State.shop.offers.Where(o=>string.IsNullOrEmpty(o.groupId)).ToArray();
                string fullCard=full.First(o=>o.category=="controller").offerId,item=full.First(o=>o.category=="consumable").offerId;
                yield return Both("full shop",new[]
                {
                    new Face20Y{name="Buy_"+fullCard+" (a card's price button, disabled)",button=()=>Button("Buy_"+fullCard),enabled=false,labels=PriceLabels20Y(false)},
                    new Face20Y{name="Buy_"+item+" (an item's price button, disabled)",button=()=>Button("Buy_"+item),enabled=false,labels=PriceLabels20Y(false)},
                });
            }
            finally
            {
                GameApp.CleanCapture11=false;
                foreach(string id in new[]{"w20y-probe","w20y-white","w20y-black"})ThemeRegistry.UnregisterForTests(id);
                app.ApplyTheme11(GameTheme.DefaultId);
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,previewValue);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,savedTheme);PlayerPrefs.Save();
                File.WriteAllLines(Path.Combine(W20YOutput("w20y-04"),"w20y-04.txt"),report.Concat(new[]{"","PROBLEMS "+failures.Count}).Concat(failures));
            }
            foreach(string line in report)TestContext.WriteLine(line);
            Assert.That(failures,Is.Empty,string.Join("\n",failures.Take(40)));
        }
    }
}
