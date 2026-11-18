using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using DiceCraft.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UObj = UnityEngine.Object;

namespace DiceCraft.Tests
{
    /// <summary>
    /// V1.1 task W20Xb item E: captures for the coordinator's review of sancai and comic (STATE: the Unity captures before
    /// deco and glass start), at the run's screen size with 100 % and 150 % text, and three measurements.
    ///   Captures: the play screen selecting, rolling (dice in the air), at a boss stage, under pressure, with the target
    ///   reached, and with ten cards in two compact rows; card details and the handbook over it; the stage settlement; the
    ///   full shop; the face pick S2; stage select; the settings page (both tabs); the W13 card-gallery pages, also in default.
    ///   (1) Every hint the action row can show (W11bHints) in the real ActionReason label: where its lines end against the
    ///       hint panel's inner frame (the drawn frame, FrameInsets11), in canvas units; for comic at 150 % the question is
    ///       whether a second line stays inside it.
    ///   (2) The action-row buttons on the play states: the centre of the label's ink against the centre of the face as drawn
    ///       (its fill, inside the ink outline, and its whole footprint with outline and shadow), from three frames: the
    ///       button, the button without its label, the button without its face.
    ///   (3) Every controller card drawn by the template as a compact card (the play row's two-row mini card at 100 % and
    ///       150 %, the five-card text card at 150 %) in the gallery's eight badge combinations: whether its short text is
    ///       shown, in default, sancai and comic.
    /// Motion is normal (the review sees the game as played). Output: artifacts/cache/v11/w20xb/&lt;size&gt;/review/&lt;label&gt;/,
    /// label from DICECRAFT_W20XB_REVIEW_LABEL (default suite-&lt;UTC time&gt;), with checks.txt and the measurements as JSON.
    /// The test asserts that every capture and measurement was made; the measurements are for review, not judged here.
    /// </summary>
    public sealed partial class GameplayPlayModeTests
    {
        private static readonly string[] ReviewThemes20Xb={"sancai","comic"};
        private static readonly int[] ReviewText20Xb={100,150};
        private string reviewDir20Xb;
        private readonly List<string> reviewFiles20Xb=new List<string>();

        private IEnumerator ReviewShot20Xb(string theme,int text,string screen)
        {
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                Assert.That(texture.width,Is.EqualTo(Screen.width));
                string file=theme+"-text"+text+"-"+screen+".png";
                File.WriteAllBytes(Path.Combine(reviewDir20Xb,file),texture.EncodeToPNG());reviewFiles20Xb.Add(file);
            }
            finally{UObj.Destroy(texture);}
        }
        private IEnumerator ReviewStyle20Xb(string theme,int text)
        {
            app.ApplyTheme11(theme);Assert.That(app.ThemeId11,Is.EqualTo(theme));W11TextScale(text);
            yield return Art073Settle(.35f);
        }
        private static Color32[] Screen20Xb()
        {
            var shot=ScreenCapture.CaptureScreenshotAsTexture();
            try{return shot.GetPixels32();}finally{UObj.Destroy(shot);}
        }
        /// <summary>A rect's screen pixels (overlay canvas: world = screen), grown by a margin and clamped; y up from the bottom.</summary>
        private static RectInt ScreenRect20Xb(RectTransform r,int margin)
        {
            var c=new Vector3[4];r.GetWorldCorners(c);
            int x0=Mathf.Max(0,Mathf.FloorToInt(c.Min(v=>v.x))-margin),x1=Mathf.Min(Screen.width,Mathf.CeilToInt(c.Max(v=>v.x))+margin);
            int y0=Mathf.Max(0,Mathf.FloorToInt(c.Min(v=>v.y))-margin),y1=Mathf.Min(Screen.height,Mathf.CeilToInt(c.Max(v=>v.y))+margin);
            return new RectInt(x0,y0,x1-x0,y1-y0);
        }

        // ---------------------------------------------------------------- (1) hint lines against the inner frame
        private static readonly MethodInfo Balanced20Xb=typeof(GameApp).GetMethod("BalancedLines11",BindingFlags.NonPublic|BindingFlags.Static);
        private static readonly MethodInfo FrameInsets20Xb=typeof(GameApp).GetMethod("FrameInsets11",BindingFlags.NonPublic|BindingFlags.Static);
        [Serializable] private sealed class HintLine20Xb{public string theme,hint,text;public int textPercent,lines;public float fontSize,left,right,top,bottom,lastLineBottom,lastLineRight;public bool inside;}
        /// <summary>Clearances (canvas units, positive inside) of the label's glyph boxes from the panel's inner frame.</summary>
        private static HintLine20Xb HintLines20Xb(TextMeshProUGUI label,Image underlay)
        {
            label.ForceMeshUpdate();
            var inset=(Vector4)FrameInsets20Xb.Invoke(null,new object[]{underlay});// x left, y bottom, z right, w top
            var u=underlay.rectTransform;var r=u.rect;
            var inner=Rect.MinMaxRect(r.xMin+inset.x,r.yMin+inset.y,r.xMax-inset.z,r.yMax-inset.w);
            var info=label.textInfo;var result=new HintLine20Xb{lines=info.lineCount,fontSize=label.fontSize,left=float.MaxValue,right=float.MaxValue,top=float.MaxValue,bottom=float.MaxValue,lastLineBottom=float.MaxValue,lastLineRight=float.MaxValue};
            for(int i=0;i<info.characterCount;i++)
            {
                var ch=info.characterInfo[i];if(!ch.isVisible)continue;
                Vector3 a=u.InverseTransformPoint(label.transform.TransformPoint(ch.bottomLeft)),b=u.InverseTransformPoint(label.transform.TransformPoint(ch.topRight));
                float xMin=Mathf.Min(a.x,b.x),xMax=Mathf.Max(a.x,b.x),yMin=Mathf.Min(a.y,b.y),yMax=Mathf.Max(a.y,b.y);
                result.left=Mathf.Min(result.left,xMin-inner.xMin);result.right=Mathf.Min(result.right,inner.xMax-xMax);
                result.top=Mathf.Min(result.top,inner.yMax-yMax);result.bottom=Mathf.Min(result.bottom,yMin-inner.yMin);
                if(ch.lineNumber==info.lineCount-1){result.lastLineBottom=Mathf.Min(result.lastLineBottom,yMin-inner.yMin);result.lastLineRight=Mathf.Min(result.lastLineRight,inner.xMax-xMax);}
            }
            result.inside=result.left>=0&&result.right>=0&&result.top>=0&&result.bottom>=0;
            return result;
        }
        private static string F20Xb(float v)=>v.ToString("0.0",CultureInfo.InvariantCulture);

        // ---------------------------------------------------------------- (2) label centre against face centre
        [Serializable] private sealed class ButtonCentre20Xb{public string theme,state,button,label;public int textPercent;public bool enabled,stable;public float labelX,labelY,fillX,fillY,footX,footY,fillRowMidX,dxFill,dyFill,dxFoot,dyFoot,dxFillRow;public int labelPixels,fillPixels,footprintPixels;public string fillColour;}
        private IEnumerator MeasureButton20Xb(string theme,int text,string state,string name,List<ButtonCentre20Xb> results,List<string> failures)
        {
            var button=FindActive<UnityEngine.UI.Button>(name);var face=(Image)button.targetGraphic;var label=button.GetComponentInChildren<TextMeshProUGUI>();
            var area=ScreenRect20Xb((RectTransform)button.transform,8);int w=Screen.width;float scale=Screen.width/1920f;
            Color32[] all,noLabel,noFace,again;
            yield return new WaitForEndOfFrame();all=Screen20Xb();
            label.enabled=false;
            try{yield return null;yield return new WaitForEndOfFrame();noLabel=Screen20Xb();face.enabled=false;yield return null;yield return new WaitForEndOfFrame();noFace=Screen20Xb();}
            finally{label.enabled=true;face.enabled=true;}
            yield return null;yield return new WaitForEndOfFrame();again=Screen20Xb();
            bool stable=true;int labelN=0,footN=0;
            int lx0=int.MaxValue,lx1=-1,ly0=int.MaxValue,ly1=-1,fx0=int.MaxValue,fx1=-1,fy0=int.MaxValue,fy1=-1;
            var counts=new Dictionary<int,int>();
            for(int y=area.yMin;y<area.yMax;y++)for(int x=area.xMin;x<area.xMax;x++)
            {
                int i=y*w+x;
                if(Delta20Xb(all[i],again[i])>2)stable=false;
                if(Delta20Xb(all[i],noLabel[i])>8){labelN++;lx0=Math.Min(lx0,x);lx1=Math.Max(lx1,x);ly0=Math.Min(ly0,y);ly1=Math.Max(ly1,y);}
                if(Delta20Xb(noLabel[i],noFace[i])>8){footN++;fx0=Math.Min(fx0,x);fx1=Math.Max(fx1,x);fy0=Math.Min(fy0,y);fy1=Math.Max(fy1,y);var c=noLabel[i];int key=c.r<<16|c.g<<8|c.b;counts[key]=counts.TryGetValue(key,out int n)?n+1:1;}
            }
            var result=new ButtonCentre20Xb{theme=theme,textPercent=text,state=state,button=name,label=label.text.Replace("\n"," / "),enabled=button.interactable,stable=stable,labelPixels=labelN,footprintPixels=footN};
            if(labelN==0||footN==0){failures.Add(theme+" "+text+"% "+state+" "+name+": label pixels "+labelN+", face pixels "+footN);results.Add(result);yield break;}
            // The fill: the face's most frequent colour (the tinted inside), and every face pixel within 24 levels of it.
            int fill=counts.OrderByDescending(k=>k.Value).First().Key;var fillColour=new Color32((byte)(fill>>16),(byte)(fill>>8&255),(byte)(fill&255),255);
            int sx0=int.MaxValue,sx1=-1,sy0=int.MaxValue,sy1=-1,fillN=0;int labelRow=(ly0+ly1)/2,rowX0=int.MaxValue,rowX1=-1;
            for(int y=area.yMin;y<area.yMax;y++)for(int x=area.xMin;x<area.xMax;x++)
            {
                int i=y*w+x;if(Delta20Xb(noLabel[i],noFace[i])<=8||Delta20Xb(noLabel[i],fillColour)>24)continue;
                fillN++;sx0=Math.Min(sx0,x);sx1=Math.Max(sx1,x);sy0=Math.Min(sy0,y);sy1=Math.Max(sy1,y);
                if(y==labelRow){rowX0=Math.Min(rowX0,x);rowX1=Math.Max(rowX1,x);}
            }
            float Units(float px)=>px/scale;
            result.labelX=Units((lx0+lx1+1)*.5f);result.labelY=Units(Screen.height-(ly0+ly1+1)*.5f);
            result.fillX=Units((sx0+sx1+1)*.5f);result.fillY=Units(Screen.height-(sy0+sy1+1)*.5f);
            result.footX=Units((fx0+fx1+1)*.5f);result.footY=Units(Screen.height-(fy0+fy1+1)*.5f);
            result.fillRowMidX=rowX1>=0?Units((rowX0+rowX1+1)*.5f):float.NaN;
            result.dxFill=result.labelX-result.fillX;result.dyFill=result.labelY-result.fillY;// design units, x right, y down
            result.dxFoot=result.labelX-result.footX;result.dyFoot=result.labelY-result.footY;
            result.dxFillRow=rowX1>=0?result.labelX-result.fillRowMidX:float.NaN;
            result.fillPixels=fillN;result.fillColour=ColorUtility.ToHtmlStringRGB(fillColour);
            results.Add(result);
            // A crop of the button for the review, three times enlarged.
            var crop=new Texture2D(area.width,area.height,TextureFormat.RGBA32,false,false);
            try
            {
                var px=new Color32[area.width*area.height];
                for(int y=0;y<area.height;y++)for(int x=0;x<area.width;x++)px[y*area.width+x]=all[(area.yMin+y)*w+area.xMin+x];
                crop.SetPixels32(px);crop.Apply(false,false);
                string dir=Path.Combine(reviewDir20Xb,"buttons");Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir,theme+"-text"+text+"-"+state+"-"+name+".png"),crop.EncodeToPNG());
            }
            finally{UObj.Destroy(crop);}
        }

        // ---------------------------------------------------------------- (3) compact cards' short text
        private static readonly MethodInfo DrawCard20Xb=typeof(GameApp).GetMethod("DrawCard13",BindingFlags.NonPublic|BindingFlags.Instance);
        private static readonly MethodInfo GalleryVariant20Xb=typeof(GameApp).GetMethod("GalleryVariant13",BindingFlags.NonPublic|BindingFlags.Instance);
        [Serializable] private sealed class CompactCards20Xb{public string theme,size;public int textPercent,cards,drawn;public List<string> hidden=new List<string>();}

        private IEnumerator ReviewPlayState20Xb(string state,Action apply,bool roll)
        {
            foreach(string theme in ReviewThemes20Xb)foreach(int text in ReviewText20Xb)
            {
                yield return ReviewStyle20Xb(theme,text);
                apply();yield return W11Settle();
                if(roll)
                {
                    var result=app.Send(new SessionCommand{kind=CommandKind.Roll});Assert.That(result.accepted,Is.True,result.error);
                    yield return new WaitForSecondsRealtime(.2f);Assert.That(app.IsBusy,Is.True,"The dice are still in the air.");
                    yield return ReviewShot20Xb(theme,text,state);
                    yield return MeasureButton20Xb(theme,text,state,"Skip",buttonCentres20Xb,reviewFailures20Xb);
                    app.SkipVisual();yield return W11Settle();
                    continue;
                }
                yield return ReviewShot20Xb(theme,text,state);
                foreach(string name in new[]{"Roll","Confirm","Clear","Submit","EndStage"})
                    if(app.canvas.GetComponentsInChildren<UnityEngine.UI.Button>(false).Any(b=>b.name==name))yield return MeasureButton20Xb(theme,text,state,name,buttonCentres20Xb,reviewFailures20Xb);
            }
        }
        private readonly List<ButtonCentre20Xb> buttonCentres20Xb=new List<ButtonCentre20Xb>();
        private readonly List<string> reviewFailures20Xb=new List<string>();

        [UnityTest] public IEnumerator PLAY_W20XB_E_ReviewCapturesOfSancaiAndComic()
        {
            Assert.That(Screen.width==1920&&Screen.height==1080||Screen.width==1280&&Screen.height==720,Is.True,"Capture at actual 1920x1080 or 1280x720.");
            foreach(string theme in ReviewThemes20Xb)Assert.That(ThemeRegistry.Find(theme),Is.Not.Null,theme);
            string label=Environment.GetEnvironmentVariable("DICECRAFT_W20XB_REVIEW_LABEL");
            if(string.IsNullOrWhiteSpace(label))label="suite-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            reviewDir20Xb=W20XbOutput(Path.Combine("review",label));reviewFiles20Xb.Clear();buttonCentres20Xb.Clear();reviewFailures20Xb.Clear();
            foreach(string old in Directory.GetFiles(reviewDir20Xb,"*.png"))File.Delete(old);
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);int previewValue=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            string savedTheme=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            var checks=new List<string>();var hints=new List<HintLine20Xb>();var compact=new List<CompactCards20Xb>();
            int expectedShots=0;
            try
            {
                PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);GameApp.CleanCapture11=true;
                // The settings page, from the menu: the first tab and the 画面 tab.
                foreach(string theme in ReviewThemes20Xb)foreach(int text in ReviewText20Xb)
                {
                    yield return ReviewStyle20Xb(theme,text);
                    Click("MenuSettings");yield return Art073Settle(.3f);yield return ReviewShot20Xb(theme,text,"20-settings");
                    Click("SettingsTab1");yield return Art073Settle(.3f);yield return ReviewShot20Xb(theme,text,"21-settings-picture");
                    IdentityClose20X();expectedShots+=2;
                }
                // Stage select.
                StartRun("72005");Assert.That(app.Session.State.phase,Is.EqualTo("StagePreview"));
                foreach(string theme in ReviewThemes20Xb)foreach(int text in ReviewText20Xb){yield return ReviewStyle20Xb(theme,text);yield return ReviewShot20Xb(theme,text,"01-select");expectedShots++;}
                // The play screen.
                yield return ReviewPlayState20Xb("02-play-selecting",()=>{W11Apply(W11Scene(W11Cards3,new[]{4,4,6,2,4},"bbbrb",2,true));app.Session.TogglePending(app.Session.State.dice[4].dieId);app.Refresh();},false);
                yield return ReviewPlayState20Xb("03-play-rolling",()=>W11Apply(W11Scene(W11Cards3,new[]{1,2,3,4,5},"bbbbb",0,false)),true);
                yield return ReviewPlayState20Xb("04-play-boss",()=>W11Apply(W11Scene(new[]{"C39","C139","C34","C56","C08"},new[]{1,2,3,4,5},"bbrbb",0,false,stageIndex:8,modifier:"B02",target:"13500",score:"0",items:2)),false);
                yield return ReviewPlayState20Xb("05-play-pressure",()=>W11Apply(W11Scene(new[]{"C39","C46","C50","C08"},new[]{5,5,3,5,1},"bbrbr",2,true,stageIndex:4,risk:true,target:"3000",score:"1240")),false);
                yield return ReviewPlayState20Xb("06-play-target-reached",()=>{W11Apply(W11Scene(W11Cards3,new[]{4,4,6,2,4},"bbbrb",2,true,target:"3000",score:"3240"));Assert.That(app.Session.State.stage.qualified,Is.True,"The target is reached.");},false);
                yield return ReviewPlayState20Xb("07-play-ten-cards",()=>W11Apply(W13TenCards()),false);
                expectedShots+=6*ReviewThemes20Xb.Length*ReviewText20Xb.Length;
                // Over the ten-card play screen: a card's details and the handbook.
                string firstCard=app.Session.State.controllers[0].instanceId;
                foreach(string theme in ReviewThemes20Xb)foreach(int text in ReviewText20Xb)
                {
                    yield return ReviewStyle20Xb(theme,text);
                    Click("Controller_"+firstCard);yield return Art073Settle(.3f);yield return ReviewShot20Xb(theme,text,"08-card-details");IdentityClose20X();
                    app.ShowHandbook();yield return Art073Settle(.3f);yield return ReviewShot20Xb(theme,text,"09-handbook");IdentityClose20X();
                    expectedShots+=2;
                }
                // (1) Every action-row hint in the real label, on the selecting screen.
                foreach(string theme in ReviewThemes20Xb)foreach(int text in ReviewText20Xb)
                {
                    yield return ReviewStyle20Xb(theme,text);
                    W11Apply(W11Scene(W11Cards3,new[]{4,4,6,2,4},"bbbrb",2,true));yield return W11Settle();
                    var hint=FindActive<TextMeshProUGUI>("ActionReason");var underlay=FindActive<Image>("ActionReasonUnderlay");string original=hint.text;
                    try
                    {
                        foreach(string text0 in W11bHints)
                        {
                            hint.text=(string)Balanced20Xb.Invoke(null,new object[]{hint,text0,hint.rectTransform.rect.width});
                            var measured=HintLines20Xb(hint,underlay);measured.theme=theme;measured.textPercent=text;measured.hint=text0;measured.text=hint.text.Replace("\n"," / ");
                            hints.Add(measured);
                            if(measured.lines>=2)
                            {
                                // The panel with this hint, three times enlarged, for the review.
                                yield return new WaitForEndOfFrame();var all=Screen20Xb();var area=ScreenRect20Xb(underlay.rectTransform,6);
                                var crop=new Texture2D(area.width,area.height,TextureFormat.RGBA32,false,false);
                                try
                                {
                                    var px=new Color32[area.width*area.height];
                                    for(int y=0;y<area.height;y++)for(int x=0;x<area.width;x++)px[y*area.width+x]=all[(area.yMin+y)*Screen.width+area.xMin+x];
                                    crop.SetPixels32(px);crop.Apply(false,false);string dir=Path.Combine(reviewDir20Xb,"hints");Directory.CreateDirectory(dir);
                                    File.WriteAllBytes(Path.Combine(dir,theme+"-text"+text+"-hint"+Array.IndexOf(W11bHints,text0).ToString("00")+".png"),crop.EncodeToPNG());
                                }
                                finally{UObj.Destroy(crop);}
                            }
                        }
                    }
                    finally{hint.text=original;hint.ForceMeshUpdate();}
                }
                // The stage settlement.
                foreach(string theme in ReviewThemes20Xb)foreach(int text in ReviewText20Xb)
                {
                    yield return ReviewStyle20Xb(theme,text);yield return IdentityEndStage20X(3);yield return Art073Settle(.45f);
                    yield return ReviewShot20Xb(theme,text,"10-settlement");expectedShots++;
                }
                // The full shop.
                yield return W14Load(W14ShopFixture(true));
                foreach(string theme in ReviewThemes20Xb)foreach(int text in ReviewText20Xb){yield return ReviewStyle20Xb(theme,text);yield return ReviewShot20Xb(theme,text,"11-shop-full");expectedShots++;}
                // The face pick, S2 (choose a face).
                var pick=ShopBoundaryFixture();pick.shop.offers.Clear();AddBoundaryOffer(pick,"pick-gold","die_upgrade","U06");
                yield return W14Load(pick);
                string die=app.Session.State.dice.OrderBy(d=>d.createdSerial).First().dieId;
                foreach(string theme in ReviewThemes20Xb)foreach(int text in ReviewText20Xb)
                {
                    yield return ReviewStyle20Xb(theme,text);
                    Click("Buy_pick-gold");yield return Art073Settle(.3f);Click("Target_"+die);yield return Art073Settle(.3f);
                    yield return ReviewShot20Xb(theme,text,"12-facepick-s2");expectedShots++;
                    Click("CancelFacePick14");yield return null;
                }
                // The W13 card-gallery pages, in default too.
                foreach(string theme in new[]{"default"}.Concat(ReviewThemes20Xb))foreach(int text in ReviewText20Xb)
                {
                    yield return ReviewStyle20Xb(theme,text);
                    for(int page=0;page<GameApp.CardGalleryPages13;page++)
                    {app.ShowCardGallery13(page);yield return Art073Settle(.3f);yield return ReviewShot20Xb(theme,text,"30-gallery-p"+page);expectedShots++;}
                    app.HideCardGallery13();
                }
                // (3) Compact cards: every controller card in the eight badge combinations of the gallery.
                var probe=UiKit.Rect(app.canvas.transform,"W20XbCompactProbe",0,0,1920,1080);
                try
                {
                    var compactSize=Enum.Parse(typeof(GameApp.CardSize13),"Compact");
                    var cards=app.Config.controllers.Select(c=>(id:c.id,rarity:c.rarity=="rare"?2:c.rarity=="uncommon"?1:0)).ToList();
                    var sizes=new[]{("mini 280x117",100,280f,117f),("mini 280x117",150,280f,117f),("text card 280x244",150,280f,244f)};
                    foreach(string theme in new[]{"default"}.Concat(ReviewThemes20Xb))
                    {
                        app.ApplyTheme11(theme);
                        foreach(var (size,text,w,h) in sizes)
                        {
                            W11TextScale(text);yield return null;
                            var record=new CompactCards20Xb{theme=theme,size=size,textPercent=text,cards=cards.Count};
                            foreach(var card in cards)for(int v=0;v<8;v++)
                            {
                                object face=GalleryVariant20Xb.Invoke(app,new object[]{card.id,card.rarity,v,v+1});
                                var drawn=(UnityEngine.UI.Button)DrawCard20Xb.Invoke(app,new object[]{probe,"W20XbProbe",40f,40f,w,h,face,compactSize,null,true});
                                var body=drawn.transform.Find("ShortText11");record.drawn++;
                                if(body==null||!body.gameObject.activeSelf)record.hidden.Add(card.id+"/"+v);
                            }
                            compact.Add(record);
                        }
                    }
                }
                finally{UObj.Destroy(probe.gameObject);}
            }
            finally
            {
                app.HideCardGallery13();W11TextScale(100);GameApp.CleanCapture11=false;app.ApplyTheme11(GameTheme.DefaultId);
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,previewValue);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,savedTheme);PlayerPrefs.Save();
                // ---- The report.
                checks.Add("W20Xb E review captures, "+Screen.width+"x"+Screen.height+", label "+label+", HEAD "+IdentityHead20X()+", "+reviewFiles20Xb.Count+" captures");
                checks.Add("");checks.Add("(1) Action-row hints against the hint panel's inner frame (canvas units; positive = inside; the glyph boxes of TMP):");
                foreach(var h in hints)
                    checks.Add("  "+h.theme+" "+h.textPercent+"% '"+h.text+"' lines "+h.lines+" size "+F20Xb(h.fontSize)+": left "+F20Xb(h.left)+", right "+F20Xb(h.right)+", top "+F20Xb(h.top)+", bottom "+F20Xb(h.bottom)+(h.lines>=2?"; last line: bottom "+F20Xb(h.lastLineBottom)+", right "+F20Xb(h.lastLineRight):"")+(h.inside?"":"  OUTSIDE"));
                checks.Add("");checks.Add("(2) Action-row buttons: label ink centre minus face centre (design units, x right, y down): fill (inside the outline) / footprint (with outline and shadow) / fill row at the label's centre:");
                foreach(var b in buttonCentres20Xb)
                    checks.Add("  "+b.theme+" "+b.textPercent+"% "+b.state+" "+b.button+(b.enabled?"":" (disabled)")+" '"+b.label+"': fill dx "+F20Xb(b.dxFill)+" dy "+F20Xb(b.dyFill)+" / footprint dx "+F20Xb(b.dxFoot)+" dy "+F20Xb(b.dyFoot)+" / row dx "+F20Xb(b.dxFillRow)+"; fill #"+b.fillColour+(b.stable?"":" (the button moved between the frames)"));
                checks.Add("");checks.Add("(3) Compact cards whose short text default shows and the style hides (card/badge combination 0-7 as in the gallery):");
                foreach(var c in compact.Where(c=>c.theme!="default"))
                {
                    var reference=compact.First(d=>d.theme=="default"&&d.size==c.size&&d.textPercent==c.textPercent);
                    var lost=c.hidden.Except(reference.hidden).ToList();var gained=reference.hidden.Except(c.hidden).ToList();
                    checks.Add("  "+c.theme+" "+c.size+" "+c.textPercent+"%: hidden "+c.hidden.Count+" of "+c.drawn+" (default "+reference.hidden.Count+"); default shows, "+c.theme+" hides: "+lost.Count+(lost.Count>0?" — "+string.Join(" ",lost):"")+(gained.Count>0?"; "+c.theme+" shows, default hides: "+string.Join(" ",gained):""));
                }
                File.WriteAllLines(Path.Combine(reviewDir20Xb,"checks.txt"),checks.Concat(new[]{"","PROBLEMS "+reviewFailures20Xb.Count}).Concat(reviewFailures20Xb));
                File.WriteAllText(Path.Combine(reviewDir20Xb,"hints.json"),"["+string.Join(",\n",hints.Select(h=>JsonUtility.ToJson(h)))+"]\n");
                File.WriteAllText(Path.Combine(reviewDir20Xb,"buttons.json"),"["+string.Join(",\n",buttonCentres20Xb.Select(b=>JsonUtility.ToJson(b)))+"]\n");
                File.WriteAllText(Path.Combine(reviewDir20Xb,"compact-cards.json"),"["+string.Join(",\n",compact.Select(c=>JsonUtility.ToJson(c)))+"]\n");
                var manifest=new IdentityManifest20X{label=label,utc=DateTime.UtcNow.ToString("O"),head=IdentityHead20X(),width=Screen.width,height=Screen.height,files=reviewFiles20Xb.ToArray()};
                File.WriteAllText(Path.Combine(reviewDir20Xb,"manifest.json"),JsonUtility.ToJson(manifest,true));
            }
            foreach(string line in checks.Take(12))TestContext.WriteLine(line);
            Assert.That(reviewFiles20Xb.Count,Is.EqualTo(expectedShots),"Every capture was made.");
            Assert.That(hints.Count,Is.EqualTo(W11bHints.Length*ReviewThemes20Xb.Length*ReviewText20Xb.Length),"Every hint was measured.");
            Assert.That(compact.Count,Is.EqualTo(3*3),"Every compact size in three styles.");
            Assert.That(buttonCentres20Xb.Count,Is.GreaterThan(0));
            Assert.That(reviewFailures20Xb,Is.Empty,string.Join("\n",reviewFailures20Xb.Take(20)));
        }
    }
}
