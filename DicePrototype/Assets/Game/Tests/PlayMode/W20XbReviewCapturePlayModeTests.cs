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
    /// V1.1 task W20Xb item E, extended by W20Z (2026-11-18, the deco and glass styles merged): captures for the
    /// coordinator's review of the preview styles sancai, comic, deco and glass, at the run's screen size with 100 % and
    /// 150 % text, and measurements.
    ///   Captures: the play screen selecting, rolling (dice in the air), at a boss stage, under pressure, with the target
    ///   reached, and with ten cards in two compact rows; card details and the handbook over it; the stage settlement; the
    ///   full shop; the face pick S2; stage select; the settings page (both tabs); the W13 card-gallery pages, also in default.
    ///   W20Z adds: the play screen while busy (13: after a scored hand, Session.Busy as a throw sets it: 查看骰子 and
    ///   上一手明细 disabled, 跳过演出), a boss stage whose modifier enters the formula (14: B26 慢热 ×0.5 right after the red
    ///   一对 factor), and the common shop (15: price buttons that can be used).
    ///   (1) Every hint the action row can show (W11bHints) in the real ActionReason label: where its lines end against the
    ///       hint panel's inner frame (the drawn frame, FrameInsets11), in canvas units; whether a second line stays inside it.
    ///   (2) The action-row buttons on the play states: the centre of the label's ink against the centre of the face as drawn
    ///       (its fill, inside the ink outline, and its whole footprint with outline and shadow), from three frames: the
    ///       button, the button without its label, the button without its face.
    ///   (3) Every controller card drawn by the template as a compact card (the play row's two-row mini card at 100 % and
    ///       150 %, the five-card text card at 150 %) in the gallery's eight badge combinations: whether its short text is
    ///       shown, in default and the four styles. W20Z: each pass on fresh card objects (W20Xb's single probe carried the
    ///       text card's auto-sizing into the later mini passes), and per style C21's mini card drawn fresh against one drawn
    ///       on a card object that was a text card before.
    ///   (4) W20Z: second-level buttons (GameApp.SolidButton11, the buttonSecondary face): the face as drawn (its centre
    ///       pixel with what is drawn over it off, the W20Y-04 read), its tint, each label's colour and contrast on that
    ///       pixel, and what it sits on (the frame with the button's graphics off: the pixels inside its face, their median
    ///       and the contrast range, and the surface drawn there); the report pairs a button enabled and disabled (查看骰子
    ///       and 上一手明细 idle and busy, the card and item price buttons in the common and the full shop) with CIEDE2000
    ///       between the faces as drawn.
    ///   (5) W20Z: on the boss formula screen the stage card's boss plate (StageCardHeader11), the boss factor chip, the red
    ///       hand factor chip and the primary button as drawn, with CIEDE2000 and contrast between them.
    ///   (6) W20Z: the sprite names a style left to the default sprite during the walk (UiKit.Template asked the style and
    ///       got none).
    ///   (7) W20Z: for every capture, the labels TMP reports truncated or overflowing, and the labels whose glyphs leave the
    ///       drawn frame of the surface under them (W11bVisibleFrame) by more than 1.5 screen pixels.
    ///   (8) W20Z: the W11b label pass (W11bContrastPass) on the play states, the busy screen, the boss formula, the
    ///       settlement and both shops: every label on a filled surface against its fill as drawn (4.5:1).
    /// Motion is normal (the review sees the game as played). Output: artifacts/cache/v11/w20z/&lt;size&gt;/review/&lt;label&gt;/
    /// (W20Xb wrote to w20xb), label from DICECRAFT_W20XB_REVIEW_LABEL (default suite-&lt;UTC time&gt;), with checks.txt and the
    /// measurements as JSON. The test asserts that every capture and measurement was made; the measurements are for review,
    /// not judged here.
    /// </summary>
    public sealed partial class GameplayPlayModeTests
    {
        // W20Xb captured sancai and comic; W20Z added deco and glass (merged 2026-11-18).
        private static readonly string[] ReviewThemes20Xb={"sancai","comic","deco","glass"};
        private static readonly int[] ReviewText20Xb={100,150};
        private string reviewDir20Xb;
        private readonly List<string> reviewFiles20Xb=new List<string>();

        /// <summary>W20Z: artifacts/cache/v11/w20z/&lt;size&gt;/&lt;name&gt;.</summary>
        private static string W20ZOutput(string name)
        {
            string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","artifacts","cache","v11","w20z",Screen.width+"x"+Screen.height,name));
            Directory.CreateDirectory(dir);return dir;
        }
        private IEnumerator ReviewShot20Xb(string theme,int text,string screen,Transform root=null)
        {
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            string file=theme+"-text"+text+"-"+screen+".png";
            try
            {
                Assert.That(texture.width,Is.EqualTo(Screen.width));
                File.WriteAllBytes(Path.Combine(reviewDir20Xb,file),texture.EncodeToPNG());reviewFiles20Xb.Add(file);
            }
            finally{UObj.Destroy(texture);}
            TextAudit20Z(file,root);
        }
        private IEnumerator ReviewStyle20Xb(string theme,int text)
        {
            app.ApplyTheme11(theme);Assert.That(app.ThemeId11,Is.EqualTo(theme));WatchTemplates20Z(theme);W11TextScale(text);
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
        /// <summary>W20Z: per style, C21's mini card drawn fresh and on a card object that was a text card before.</summary>
        private readonly List<string> compactCarry20Z=new List<string>();

        // ---------------------------------------------------------------- (4) second-level buttons as drawn (W20Z)
        [Serializable] private sealed class Label20Z{public string name,text,colour;public float alpha,contrast;}
        [Serializable] private sealed class Secondary20Z
        {
            public string theme,screen,role,button,key,sprite,tint,drawn,surface,underMedian;public int textPercent,underPixels;public bool enabled;
            public float contrastUnder,contrastUnderP10,contrastUnderP90;public List<Label20Z> labels=new List<Label20Z>();
        }
        private readonly List<Secondary20Z> secondaries20Z=new List<Secondary20Z>();
        private static string Hex20Z(Color c)=>ColorUtility.ToHtmlStringRGB(c);
        private static string R20Z(double v)=>v.ToString("0.00",CultureInfo.InvariantCulture);
        /// <summary>The topmost filled surface drawn before <paramref name="first"/> that holds <paramref name="point"/> (screen
        /// pixels, y up), named by its path and sprite; "the felt" when only the table is there.</summary>
        private string SurfaceUnder20Z(Graphic first,Vector2Int point)
        {
            var order=app.canvas.GetComponentsInChildren<Graphic>(false);int at=Array.IndexOf(order,first);var p=new Vector2(point.x+.5f,point.y+.5f);
            for(int i=at-1;i>=0;i--)
            {
                if(!(order[i] is Image image)||!W11bPaints(image,true))continue;
                if(V06ScreenRect(image.rectTransform).Contains(p))return W11bPath(image.transform)+(image.sprite!=null?" ("+image.sprite.name+")":" (no sprite)");
            }
            return "the felt";
        }
        /// <summary>One second-level button: the face's centre pixel with what is drawn over it off (W20Y-04), its labels on that
        /// pixel, and what the button sits on (one frame with every graphic of the button off: the pixels inside its face).</summary>
        private IEnumerator MeasureSecondary20Z(string theme,int text,string screen,string role,string name)
        {
            var button=FindActive<UnityEngine.UI.Button>(name);var face=(Image)button.targetGraphic;
            var rt=face.rectTransform;var r=rt.rect;var pixel=ScreenPixel20X(rt,new Vector2(r.width*.5f,r.height*.5f));
            Color32 drawn=default;yield return TargetPixel20X(face,pixel,c=>drawn=c);
            var rootGraphic=button.GetComponent<Graphic>();var key=UiKit.Palette.buttonSecondary;
            var result=new Secondary20Z{theme=theme,textPercent=text,screen=screen,role=role,button=name,enabled=button.interactable,key=key.a>0?Hex20Z(key):"unset",
                sprite=face.sprite!=null?face.sprite.name:"none",tint=Hex20Z(face.color),drawn=Hex20Z((Color)drawn),surface=SurfaceUnder20Z(rootGraphic!=null?rootGraphic:face,pixel)};
            foreach(var t in button.GetComponentsInChildren<TextMeshProUGUI>(false))
            {
                if(!t.enabled||string.IsNullOrWhiteSpace(t.text))continue;
                var c=t.color;float alpha=c.a;c.a=1;
                result.labels.Add(new Label20Z{name=t.name,text=t.text.Replace("\n"," / "),colour=Hex20Z(c),alpha=alpha,contrast=GameApp.Contrast11(c,(Color)drawn)});
            }
            var own=button.GetComponentsInChildren<Graphic>(false).Where(g=>g.enabled).ToList();
            Color32[] under;
            foreach(var g in own)g.enabled=false;
            try{yield return null;yield return new WaitForEndOfFrame();under=Screen20Xb();}
            finally{foreach(var g in own)if(g!=null)g.enabled=true;}
            var area=ScreenRect20Xb(rt,0);int inset=Mathf.Max(1,Mathf.RoundToInt(2f*Screen.width/1920f));
            var pixels=new List<Color>();
            for(int y=area.yMin+inset;y<area.yMax-inset;y++)for(int x=area.xMin+inset;x<area.xMax-inset;x++)pixels.Add(under[y*Screen.width+x]);
            if(pixels.Count>0)
            {
                pixels.Sort((a,b)=>GameApp.Luminance11(a).CompareTo(GameApp.Luminance11(b)));
                var median=pixels[pixels.Count/2];result.underMedian=Hex20Z(median);result.underPixels=pixels.Count;
                var ratios=pixels.Select(q=>GameApp.Contrast11((Color)drawn,q)).OrderBy(v=>v).ToList();
                result.contrastUnder=GameApp.Contrast11((Color)drawn,median);result.contrastUnderP10=ratios[ratios.Count/10];result.contrastUnderP90=ratios[Math.Min(ratios.Count-1,ratios.Count*9/10)];
            }
            secondaries20Z.Add(result);
        }

        // ---------------------------------------------------------------- (5) the boss plate and the boss factor (W20Z)
        [Serializable] private sealed class Surface20Z{public string name,tint,drawn,label,labelColour;public float labelContrast;}
        [Serializable] private sealed class Boss20Z
        {
            public string theme,chips;public int textPercent;public bool adjacent;public Surface20Z plate,bossChip,redChip,primary;
            public float de00BossChipRedChip,contrastBossChipRedChip,de00BossChipPrimary,de00PlatePrimary,de00PlateBossChip,de00PlateRedChip;
        }
        private readonly List<Boss20Z> bosses20Z=new List<Boss20Z>();
        private IEnumerator ReadSurface20Z(string name,Image face,TextMeshProUGUI label,Action<Surface20Z,Color32> done)
        {
            var r=face.rectTransform.rect;var pixel=ScreenPixel20X(face.rectTransform,new Vector2(r.width*.5f,r.height*.5f));
            Color32 drawn=default;yield return TargetPixel20X(face,pixel,c=>drawn=c);
            var s=new Surface20Z{name=name,tint=Hex20Z(face.color),drawn=Hex20Z((Color)drawn)};
            if(label!=null){var c=label.color;c.a=1;s.label=label.text.Replace("\n"," / ");s.labelColour=Hex20Z(c);s.labelContrast=GameApp.Contrast11(c,(Color)drawn);}
            done(s,drawn);
        }
        private static int FactorRole20Z(Transform t)=>t.name.StartsWith("Factor",StringComparison.Ordinal)&&int.TryParse(t.name.Substring(6),out int role)?role:-1;
        private IEnumerator MeasureBoss20Z(string theme,int text)
        {
            var record=new Boss20Z{theme=theme,textPercent=text};Color32 plate=default,boss=default,red=default,primary=default;
            var header=FindActive<Image>("StageCardHeader11");
            yield return ReadSurface20Z("StageCardHeader11 (boss plate)",header,header.transform.Find("StageCardTitle11")?.GetComponent<TextMeshProUGUI>(),(s,c)=>{record.plate=s;plate=c;});
            var chips=FindActive<RectTransform>("ScorePanel").Cast<Transform>().Where(x=>x.gameObject.activeInHierarchy&&FactorRole20Z(x)>=0).OrderBy(x=>((RectTransform)x).anchoredPosition.x).ToList();
            string ChipText(Transform x)=>((x.Find("Name")?.gameObject.activeSelf??false)?x.Find("Name").GetComponent<TextMeshProUGUI>().text+" ":"")+(x.Find("Label")?.GetComponent<TextMeshProUGUI>()?.text??"");
            record.chips=string.Join(" | ",chips.Select(x=>x.name+" "+ChipText(x)));
            int bossAt=chips.FindIndex(x=>FactorRole20Z(x)>=6),redAt=chips.FindIndex(x=>FactorRole20Z(x)==1);
            record.adjacent=bossAt>=0&&redAt>=0&&bossAt==redAt+1;
            if(bossAt<0||redAt<0){reviewFailures20Xb.Add(theme+" "+text+"% boss formula: no boss factor or no red hand factor ("+record.chips+")");bosses20Z.Add(record);yield break;}
            foreach(var (at,which) in new[]{(bossAt,"boss"),(redAt,"red")})
            {
                var chip=chips[at];var face=(Image)chip.GetComponent<UnityEngine.UI.Button>().targetGraphic;var label=chip.Find("Label")?.GetComponent<TextMeshProUGUI>();
                yield return ReadSurface20Z(chip.name+" '"+ChipText(chip)+"'",face,label,(s,c)=>{if(which=="boss"){record.bossChip=s;boss=c;}else{record.redChip=s;red=c;}});
            }
            var submit=FindActive<UnityEngine.UI.Button>("Submit");
            yield return ReadSurface20Z("Submit (primary)",(Image)submit.targetGraphic,submit.transform.Find("Label")?.GetComponent<TextMeshProUGUI>(),(s,c)=>{record.primary=s;primary=c;});
            record.de00BossChipRedChip=(float)W13DeltaE00((Color)boss,(Color)red);record.contrastBossChipRedChip=GameApp.Contrast11((Color)boss,(Color)red);
            record.de00BossChipPrimary=(float)W13DeltaE00((Color)boss,(Color)primary);record.de00PlatePrimary=(float)W13DeltaE00((Color)plate,(Color)primary);
            record.de00PlateBossChip=(float)W13DeltaE00((Color)plate,(Color)boss);record.de00PlateRedChip=(float)W13DeltaE00((Color)plate,(Color)red);
            bosses20Z.Add(record);
        }

        // ---------------------------------------------------------------- (6) sprites left to the default (W20Z)
        private readonly Dictionary<string,SortedSet<string>> fallbacks20Z=new Dictionary<string,SortedSet<string>>(StringComparer.Ordinal);
        /// <summary>After ApplyTheme11 (which sets UiKit.ThemeTemplate to the style's lookup): records every name the style has
        /// no sprite for, until the next ApplyTheme11. The default style has no lookup and is not watched.</summary>
        private void WatchTemplates20Z(string theme)
        {
            var inner=UiKit.ThemeTemplate;if(inner==null)return;
            if(!fallbacks20Z.TryGetValue(theme,out var missing))fallbacks20Z[theme]=missing=new SortedSet<string>(StringComparer.Ordinal);
            UiKit.ThemeTemplate=name=>{var sprite=inner(name);if(sprite==null&&name!=null)missing.Add(name);return sprite;};
        }

        // ---------------------------------------------------------------- (7) labels cut or over their frame (W20Z)
        [Serializable] private sealed class TextIssue20Z{public string file,kind,path,text,surface;public float outside;}
        private readonly List<TextIssue20Z> textIssues20Z=new List<TextIssue20Z>();
        private readonly Dictionary<string,int> textCounts20Z=new Dictionary<string,int>(StringComparer.Ordinal);
        private static readonly FieldInfo Overlay20Z=typeof(GameApp).GetField("overlay",BindingFlags.NonPublic|BindingFlags.Instance);
        /// <summary>Every visible label of the capture (under <paramref name="root"/> when given; tooltips and the drag layer
        /// left out, as in the W11b pass): truncated or overflowing as TMP reports, and glyphs that leave the drawn frame of the
        /// surface under them by more than 1.5 screen pixels.</summary>
        private void TextAudit20Z(string file,Transform root)
        {
            Canvas.ForceUpdateCanvases();
            var overlay=Overlay20Z?.GetValue(app) as Transform;var dialog=overlay!=null?overlay.Find("Dialog"):null;
            var order=app.canvas.GetComponentsInChildren<Graphic>(false);int audited=0;
            foreach(var t in app.canvas.GetComponentsInChildren<TextMeshProUGUI>(false))
            {
                if(!t.enabled||!t.gameObject.activeInHierarchy||string.IsNullOrWhiteSpace(t.text)||t.color.a<.5f||t.canvasRenderer.GetInheritedAlpha()<.5f)continue;
                // The overlay holds tooltips and drag marks (left out) and the modal dialog (card details, handbook: audited).
                if(overlay!=null&&t.transform.IsChildOf(overlay)&&(dialog==null||!t.transform.IsChildOf(dialog)))continue;
                if(root!=null&&!t.transform.IsChildOf(root))continue;
                t.ForceMeshUpdate();var glyphs=W11bGlyphRects(t);if(glyphs.Count==0)continue;audited++;
                string path=W11bPath(t.transform),shown=t.text.Replace("\n"," / ");
                if(t.isTextTruncated)textIssues20Z.Add(new TextIssue20Z{file=file,kind="truncated",path=path,text=shown});
                if(t.isTextOverflowing)textIssues20Z.Add(new TextIssue20Z{file=file,kind="overflowing",path=path,text=shown});
                var frame=W11bVisibleFrame(t,glyphs,order,out string surface);
                if(!frame.HasValue)continue;
                var f=frame.Value;float outside=glyphs.Max(g=>Mathf.Max(Mathf.Max(f.xMin-g.xMin,g.xMax-f.xMax),Mathf.Max(f.yMin-g.yMin,g.yMax-f.yMax)));
                if(outside>1.5f)textIssues20Z.Add(new TextIssue20Z{file=file,kind="outside its frame",path=path,text=shown,surface=surface,outside=outside});
            }
            textCounts20Z[file]=audited;
        }

        // ---------------------------------------------------------------- (8) the W11b label pass (W20Z)
        private readonly List<string> contrastReport20Z=new List<string>(),contrastFailures20Z=new List<string>();

        private IEnumerator ReviewPlayState20Xb(string state,Action apply,bool roll,string secondary=null)
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
                if(secondary!=null)yield return MeasureSecondary20Z(theme,text,state,secondary+" (action row)",secondary);
                yield return W11bContrastPass(theme+" "+text+"% "+state,contrastReport20Z,contrastFailures20Z);
            }
        }
        private readonly List<ButtonCentre20Xb> buttonCentres20Xb=new List<ButtonCentre20Xb>();
        private readonly List<string> reviewFailures20Xb=new List<string>();

        /// <summary>W20Z: four styles, three more screens and the measurements take about six minutes at 1080p, past the
        /// test framework's default limit of three; the limit is raised for this test only.</summary>
        [UnityTest, Timeout(900000)] public IEnumerator PLAY_W20XB_E_ReviewCapturesOfSancaiComicDecoAndGlass()
        {
            Assert.That(Screen.width==1920&&Screen.height==1080||Screen.width==1280&&Screen.height==720,Is.True,"Capture at actual 1920x1080 or 1280x720.");
            foreach(string theme in ReviewThemes20Xb)Assert.That(ThemeRegistry.Find(theme),Is.Not.Null,theme);
            string label=Environment.GetEnvironmentVariable("DICECRAFT_W20XB_REVIEW_LABEL");
            if(string.IsNullOrWhiteSpace(label))label="suite-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            reviewDir20Xb=W20ZOutput(Path.Combine("review",label));reviewFiles20Xb.Clear();buttonCentres20Xb.Clear();reviewFailures20Xb.Clear();
            secondaries20Z.Clear();bosses20Z.Clear();fallbacks20Z.Clear();textIssues20Z.Clear();textCounts20Z.Clear();contrastReport20Z.Clear();contrastFailures20Z.Clear();compactCarry20Z.Clear();
            foreach(string old in Directory.GetFiles(reviewDir20Xb,"*.png"))File.Delete(old);
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);int previewValue=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            string savedTheme=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            var checks=new List<string>();var hints=new List<HintLine20Xb>();var compact=new List<CompactCards20Xb>();
            int expectedShots=0;bool heldBusy=false;var defaultSprite=new Dictionary<string,bool>(StringComparer.Ordinal);
            try
            {
                PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);GameApp.CleanCapture11=true;
                // (6) The watch itself: a name the style has no sprite for is recorded (then forgotten).
                app.ApplyTheme11(ReviewThemes20Xb[0]);WatchTemplates20Z(ReviewThemes20Xb[0]);Assert.That(UiKit.Template("w20z-no-such-sprite"),Is.Null);
                Assert.That(fallbacks20Z[ReviewThemes20Xb[0]].Remove("w20z-no-such-sprite"),Is.True,"The watch records a name the style has no sprite for.");
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
                // The play screen. W20Z: the action row's secondary (取消选择, 重掷) measured as a buttonSecondary face on the felt.
                yield return ReviewPlayState20Xb("02-play-selecting",()=>{W11Apply(W11Scene(W11Cards3,new[]{4,4,6,2,4},"bbbrb",2,true));app.Session.TogglePending(app.Session.State.dice[4].dieId);app.Refresh();},false,"Clear");
                yield return ReviewPlayState20Xb("03-play-rolling",()=>W11Apply(W11Scene(W11Cards3,new[]{1,2,3,4,5},"bbbbb",0,false)),true);
                yield return ReviewPlayState20Xb("04-play-boss",()=>W11Apply(W11Scene(new[]{"C39","C139","C34","C56","C08"},new[]{1,2,3,4,5},"bbrbb",0,false,stageIndex:8,modifier:"B02",target:"13500",score:"0",items:2)),false);
                yield return ReviewPlayState20Xb("05-play-pressure",()=>W11Apply(W11Scene(new[]{"C39","C46","C50","C08"},new[]{5,5,3,5,1},"bbrbr",2,true,stageIndex:4,risk:true,target:"3000",score:"1240")),false,"Roll");
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
                // W20Z: the busy play screen. After a scored hand (so 上一手明细 is drawn) the app is made busy the way a throw makes
                // it (Session.Busy and a redraw, as PLAY_W20Y2_01): 查看骰子 and 上一手明细 disabled, 跳过演出 the row's action.
                foreach(string theme in ReviewThemes20Xb)foreach(int text in ReviewText20Xb)
                {
                    yield return ReviewStyle20Xb(theme,text);
                    W11Apply(IdentityWinningHand20X(3));yield return W11Settle();
                    var submit=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(submit.accepted,Is.True,submit.error);
                    app.SkipVisual();yield return W11Settle();
                    Assert.That(app.Session.State.phase,Is.EqualTo("HandReady"));Assert.That(app.Session.State.lastSubmission,Is.Not.Null);
                    yield return MeasureSecondary20Z(theme,text,"play after a hand","ViewDice","ViewDice");
                    yield return MeasureSecondary20Z(theme,text,"play after a hand","PreviousSubmission","PreviousSubmission");
                    yield return MeasureSecondary20Z(theme,text,"play after a hand","完整记录 (formula row)","StablePresentationDetails07");
                    app.Session.Busy=true;heldBusy=true;app.Refresh();Canvas.ForceUpdateCanvases();
                    yield return new WaitForSecondsRealtime(.3f);Canvas.ForceUpdateCanvases();
                    Assert.That(app.IsBusy,Is.True,"The busy frame is held.");Assert.That(HasActive("Skip"),Is.True,"The busy action row offers 跳过演出.");
                    yield return ReviewShot20Xb(theme,text,"13-play-busy");expectedShots++;
                    yield return MeasureSecondary20Z(theme,text,"play busy","ViewDice","ViewDice");
                    yield return MeasureSecondary20Z(theme,text,"play busy","PreviousSubmission","PreviousSubmission");
                    yield return MeasureSecondary20Z(theme,text,"play busy","Skip (action row)","Skip");
                    yield return W11bContrastPass(theme+" "+text+"% 13-play-busy",contrastReport20Z,contrastFailures20Z);
                    Assert.That(app.IsBusy,Is.True,"Nothing ended the busy frame during the pass.");
                    app.Session.Busy=false;heldBusy=false;app.Refresh();yield return W11Settle();
                }
                // W20Z: a boss stage whose modifier enters the formula, B26 慢热 (the stage's first hand x0.5), with a blue and a red
                // four locked and no cards: 基础分 x 一对 x2 x 慢热 x0.5, the boss factor right after the red hand factor.
                foreach(string theme in ReviewThemes20Xb)foreach(int text in ReviewText20Xb)
                {
                    yield return ReviewStyle20Xb(theme,text);
                    W11Apply(W11Scene(new string[0],new[]{4,4,6,2,5},"brbbb",2,true,stageIndex:5,modifier:"B26",target:"4000",score:"0"));yield return W11Settle();
                    yield return ReviewShot20Xb(theme,text,"14-play-boss-formula");expectedShots++;
                    yield return MeasureBoss20Z(theme,text);
                    yield return W11bContrastPass(theme+" "+text+"% 14-play-boss-formula",contrastReport20Z,contrastFailures20Z);
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
                                // The panel with this hint, for the review.
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
                    yield return W11bContrastPass(theme+" "+text+"% 10-settlement",contrastReport20Z,contrastFailures20Z);
                }
                // The full shop. W20Z: its second-level buttons: the disabled price buttons on the paper cards (卡牌已满 10 张,
                // 道具栏已满), a goods row's price button, the top bar's 查看本章 / 手册 / 设置, the shelf's 刷新.
                yield return W14Load(W14ShopFixture(true));
                var fullOffers=app.Session.State.shop.offers.Where(o=>string.IsNullOrEmpty(o.groupId)).ToArray();
                string fullCard=fullOffers.First(o=>o.category=="controller").offerId,fullItem=fullOffers.First(o=>o.category=="consumable").offerId;
                foreach(string theme in ReviewThemes20Xb)foreach(int text in ReviewText20Xb)
                {
                    yield return ReviewStyle20Xb(theme,text);yield return ReviewShot20Xb(theme,text,"11-shop-full");expectedShots++;
                    yield return MeasureSecondary20Z(theme,text,"full shop","card price","Buy_"+fullCard);
                    yield return MeasureSecondary20Z(theme,text,"full shop","item price","Buy_"+fullItem);
                    string goods=fullOffers.Where(o=>o.category=="hand_upgrade"||o.category=="color_upgrade"||o.category=="service"||o.category=="new_die"||o.category=="die_upgrade")
                        .Select(o=>o.offerId).FirstOrDefault(id=>HasActive("Buy_"+id)&&Button("Buy_"+id).interactable);
                    if(goods!=null)yield return MeasureSecondary20Z(theme,text,"full shop","goods row price","Buy_"+goods);
                    yield return MeasureSecondary20Z(theme,text,"full shop","查看本章 (top bar)","ViewCycle");
                    yield return MeasureSecondary20Z(theme,text,"full shop","手册 (top bar)","Handbook");
                    yield return MeasureSecondary20Z(theme,text,"full shop","设置 (top bar)","Pause");
                    yield return MeasureSecondary20Z(theme,text,"full shop","刷新 (shelf)","RefreshControllers");
                    yield return W11bContrastPass(theme+" "+text+"% 11-shop-full",contrastReport20Z,contrastFailures20Z);
                }
                // W20Z: the common shop, whose card and item price buttons can be used (the enabled half of the price pairs).
                yield return W14Load(W14ShopFixture(false));
                var commonOffers=app.Session.State.shop.offers.Where(o=>string.IsNullOrEmpty(o.groupId)).ToArray();
                string commonCard=commonOffers.First(o=>o.category=="controller").offerId,commonItem=commonOffers.FirstOrDefault(o=>o.category=="consumable")?.offerId;
                foreach(string theme in ReviewThemes20Xb)foreach(int text in ReviewText20Xb)
                {
                    yield return ReviewStyle20Xb(theme,text);yield return ReviewShot20Xb(theme,text,"15-shop-common");expectedShots++;
                    yield return MeasureSecondary20Z(theme,text,"common shop","card price","Buy_"+commonCard);
                    if(commonItem!=null&&HasActive("Buy_"+commonItem))yield return MeasureSecondary20Z(theme,text,"common shop","item price","Buy_"+commonItem);
                    yield return W11bContrastPass(theme+" "+text+"% 15-shop-common",contrastReport20Z,contrastFailures20Z);
                }
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
                    {var galleryRoot=app.ShowCardGallery13(page);yield return Art073Settle(.3f);yield return ReviewShot20Xb(theme,text,"30-gallery-p"+page,galleryRoot);expectedShots++;}
                    app.HideCardGallery13();
                }
                // (3) Compact cards: every controller card in the eight badge combinations of the gallery. W20Z: every pass (style,
                // card size, text size) draws on a probe of its own. The template keeps a card's labels in a pool by name, and its
                // mini path does not reset enableAutoSizing, which the tall text card turns on (DrawCompactCard13). W20Xb drew every
                // pass on one probe, so every mini pass after the first text-card pass measured with auto-sizing still on (at the
                // text card's largest size) and counted almost every short text as hidden: every style but default.
                var compactSize=Enum.Parse(typeof(GameApp.CardSize13),"Compact");
                var cards=app.Config.controllers.Select(c=>(id:c.id,rarity:c.rarity=="rare"?2:c.rarity=="uncommon"?1:0)).ToList();
                var sizes=new[]{("mini 280x117",100,280f,117f),("mini 280x117",150,280f,117f),("text card 280x244",150,280f,244f)};
                int passes=0;
                bool Shown20Z(UnityEngine.UI.Button drawnCard){var body=drawnCard.transform.Find("ShortText11");return body!=null&&body.gameObject.activeSelf;}
                foreach(string theme in new[]{"default"}.Concat(ReviewThemes20Xb))
                {
                    app.ApplyTheme11(theme);WatchTemplates20Z(theme);
                    foreach(var (size,text,w,h) in sizes)
                    {
                        W11TextScale(text);yield return null;
                        var record=new CompactCards20Xb{theme=theme,size=size,textPercent=text,cards=cards.Count};
                        var probe=UiKit.Rect(app.canvas.transform,"W20XbCompactProbe"+passes++,0,0,1920,1080);
                        try
                        {
                            foreach(var card in cards)for(int v=0;v<8;v++)
                            {
                                object face=GalleryVariant20Xb.Invoke(app,new object[]{card.id,card.rarity,v,v+1});
                                var drawn=(UnityEngine.UI.Button)DrawCard20Xb.Invoke(app,new object[]{probe,"W20XbProbe",40f,40f,w,h,face,compactSize,null,true});
                                record.drawn++;
                                if(!Shown20Z(drawn))record.hidden.Add(card.id+"/"+v);
                            }
                        }
                        finally{UObj.Destroy(probe.gameObject);}
                        compact.Add(record);
                    }
                    // The same carry-over in the template itself: C21 (no badge, the shortest short text) as a mini card at 100 %
                    // drawn fresh, and drawn on the card object that showed the same card as a 280x244 text card at 150 % before
                    // (the play row's cards are pooled by instance: 150 % with five cards or fewer, then eight cards at 100 %).
                    var carry=UiKit.Rect(app.canvas.transform,"W20XbCarryProbe"+passes++,0,0,1920,1080);
                    try
                    {
                        int rarity=cards.First(c=>c.id=="C21").rarity;object c21=GalleryVariant20Xb.Invoke(app,new object[]{"C21",rarity,0,1});
                        W11TextScale(100);bool fresh=Shown20Z((UnityEngine.UI.Button)DrawCard20Xb.Invoke(app,new object[]{carry,"Fresh",40f,40f,280f,117f,c21,compactSize,null,true}));
                        W11TextScale(150);DrawCard20Xb.Invoke(app,new object[]{carry,"Reused",40f,400f,280f,244f,c21,compactSize,null,true});
                        W11TextScale(100);var reused=(UnityEngine.UI.Button)DrawCard20Xb.Invoke(app,new object[]{carry,"Reused",40f,400f,280f,117f,c21,compactSize,null,true});
                        var shortLabel=reused.transform.Find("ShortText11")?.GetComponent<TextMeshProUGUI>();
                        compactCarry20Z.Add(theme+": C21 as a mini card at 100 %: "+(fresh?"shown":"hidden")+" when drawn fresh, "+(Shown20Z(reused)?"shown":"hidden")+
                            " on the card object drawn as a 150 % text card before (its label's auto-sizing "+(shortLabel!=null&&shortLabel.enableAutoSizing?"still on, "+F20Xb(shortLabel.fontSizeMin)+" to "+F20Xb(shortLabel.fontSizeMax):"off")+")");
                    }
                    finally{UObj.Destroy(carry.gameObject);}
                }
            }
            finally
            {
                if(heldBusy&&app!=null&&app.Session!=null)app.Session.Busy=false;
                app.HideCardGallery13();W11TextScale(100);GameApp.CleanCapture11=false;app.ApplyTheme11(GameTheme.DefaultId);
                // (6) With the default style active (no style lookup): does a default sprite stand in for each name?
                foreach(string name in fallbacks20Z.Values.SelectMany(s=>s).Distinct())defaultSprite[name]=UiKit.Template(name)!=null;
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,previewValue);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,savedTheme);PlayerPrefs.Save();
                // ---- The report.
                checks.Add("W20Xb E / W20Z review captures, "+Screen.width+"x"+Screen.height+", label "+label+", HEAD "+IdentityHead20X()+", "+reviewFiles20Xb.Count+" captures, styles "+string.Join(", ",ReviewThemes20Xb));
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
                checks.Add("  The template's pooled label (W20Z): "+(compactCarry20Z.Count==0?"not measured":""));
                foreach(string line in compactCarry20Z)checks.Add("    "+line);
                checks.Add("");checks.Add("(4) Second-level buttons (SolidButton11) as drawn: face (centre pixel) and tint; labels with contrast on the face; what the button sits on (median of the pixels under its face with the button off) and the contrast of the face against it (median pixel; 10th-90th percentile of all):");
                foreach(var s in secondaries20Z)
                    checks.Add("  "+s.theme+" "+s.textPercent+"% "+s.screen+" "+s.role+" ["+s.button+"] "+(s.enabled?"enabled":"DISABLED")+", buttonSecondary "+s.key+": face #"+s.drawn+" (tint #"+s.tint+", "+s.sprite+"); "+
                        string.Join("; ",s.labels.Select(l=>l.name+" #"+l.colour+(l.alpha<.999f?" a"+R20Z(l.alpha):"")+" "+R20Z(l.contrast)+":1"))+"; on "+s.surface+" #"+s.underMedian+": "+R20Z(s.contrastUnder)+":1 ("+R20Z(s.contrastUnderP10)+"-"+R20Z(s.contrastUnderP90)+")");
                checks.Add("  Enabled against disabled, faces as drawn (CIEDE2000):");
                foreach(var group in secondaries20Z.GroupBy(s=>s.theme+"|"+s.textPercent+"|"+s.role))
                {
                    var on=group.FirstOrDefault(s=>s.enabled);var off=group.FirstOrDefault(s=>!s.enabled);if(on==null||off==null)continue;
                    ColorUtility.TryParseHtmlString("#"+on.drawn,out Color a);ColorUtility.TryParseHtmlString("#"+off.drawn,out Color b);
                    checks.Add("    "+on.theme+" "+on.textPercent+"% "+on.role+": enabled #"+on.drawn+" ("+on.screen+") / disabled #"+off.drawn+" ("+off.screen+"): CIEDE2000 "+R20Z(W13DeltaE00(a,b))+", contrast "+R20Z(GameApp.Contrast11(a,b))+":1; labels "+
                        string.Join(" ",on.labels.Select(l=>"#"+l.colour))+" / "+string.Join(" ",off.labels.Select(l=>"#"+l.colour)));
                }
                checks.Add("");checks.Add("(5) Boss stage (B26 慢热 in the formula): the plate, the boss and red factor chips and the primary as drawn (centre pixel; tint after LabelOn11):");
                foreach(var b in bosses20Z)
                {
                    string Part(Surface20Z s)=>s==null?"missing":s.name+": #"+s.drawn+" (tint #"+s.tint+")"+(s.labelColour!=null?", label '"+s.label+"' #"+s.labelColour+" "+R20Z(s.labelContrast)+":1":"");
                    checks.Add("  "+b.theme+" "+b.textPercent+"% chips: "+b.chips+(b.adjacent?" (boss right after red)":" (NOT ADJACENT)"));
                    checks.Add("    "+Part(b.plate));checks.Add("    "+Part(b.bossChip));checks.Add("    "+Part(b.redChip));checks.Add("    "+Part(b.primary));
                    checks.Add("    CIEDE2000: boss chip / red chip "+R20Z(b.de00BossChipRedChip)+" (contrast "+R20Z(b.contrastBossChipRedChip)+":1); boss chip / primary "+R20Z(b.de00BossChipPrimary)+"; plate / primary "+R20Z(b.de00PlatePrimary)+"; plate / boss chip "+R20Z(b.de00PlateBossChip)+"; plate / red chip "+R20Z(b.de00PlateRedChip));
                }
                checks.Add("");checks.Add("(6) Sprite names a style had no sprite for during the walk (default: a default sprite was drawn instead):");
                foreach(var pair in fallbacks20Z.OrderBy(p=>p.Key,StringComparer.Ordinal))
                    checks.Add("  "+pair.Key+": "+(pair.Value.Count==0?"none":string.Join(", ",pair.Value.Select(n=>n+(defaultSprite.TryGetValue(n,out bool d)&&d?" (default)":" (no sprite anywhere)")))));
                checks.Add("");checks.Add("(7) Labels truncated, overflowing, or with glyphs more than 1.5 px outside the drawn frame under them, per capture (labels audited):");
                foreach(string file in reviewFiles20Xb)
                {
                    var issues=textIssues20Z.Where(i=>i.file==file).ToList();
                    checks.Add("  "+file+" ("+(textCounts20Z.TryGetValue(file,out int n)?n:0)+"): "+(issues.Count==0?"none":string.Join("; ",issues.Select(i=>i.kind+" "+i.path+" '"+i.text+"'"+(i.kind=="outside its frame"?" by "+F20Xb(i.outside)+" px on "+i.surface:"")))));
                }
                checks.Add("");checks.Add("(8) W11b label pass: "+contrastReport20Z.Count+" labels measured, "+contrastFailures20Z.Count+" under their minimum:");
                foreach(string theme in ReviewThemes20Xb)checks.Add("  "+theme+": "+contrastReport20Z.Count(l=>l.StartsWith(theme+" ",StringComparison.Ordinal))+" labels, "+contrastFailures20Z.Count(l=>l.StartsWith(theme+" ",StringComparison.Ordinal))+" under");
                foreach(string line in contrastFailures20Z)checks.Add("  UNDER "+line);
                File.WriteAllLines(Path.Combine(reviewDir20Xb,"checks.txt"),checks.Concat(new[]{"","PROBLEMS "+reviewFailures20Xb.Count}).Concat(reviewFailures20Xb));
                File.WriteAllText(Path.Combine(reviewDir20Xb,"hints.json"),"["+string.Join(",\n",hints.Select(h=>JsonUtility.ToJson(h)))+"]\n");
                File.WriteAllText(Path.Combine(reviewDir20Xb,"buttons.json"),"["+string.Join(",\n",buttonCentres20Xb.Select(b=>JsonUtility.ToJson(b)))+"]\n");
                File.WriteAllText(Path.Combine(reviewDir20Xb,"compact-cards.json"),"["+string.Join(",\n",compact.Select(c=>JsonUtility.ToJson(c)))+"]\n");
                File.WriteAllText(Path.Combine(reviewDir20Xb,"secondary.json"),"["+string.Join(",\n",secondaries20Z.Select(s=>JsonUtility.ToJson(s)))+"]\n");
                File.WriteAllText(Path.Combine(reviewDir20Xb,"boss.json"),"["+string.Join(",\n",bosses20Z.Select(b=>JsonUtility.ToJson(b)))+"]\n");
                File.WriteAllText(Path.Combine(reviewDir20Xb,"texts.json"),"["+string.Join(",\n",textIssues20Z.Select(i=>JsonUtility.ToJson(i)))+"]\n");
                File.WriteAllLines(Path.Combine(reviewDir20Xb,"label-contrast.txt"),contrastReport20Z.Concat(new[]{"","UNDER "+contrastFailures20Z.Count}).Concat(contrastFailures20Z));
                var manifest=new IdentityManifest20X{label=label,utc=DateTime.UtcNow.ToString("O"),head=IdentityHead20X(),width=Screen.width,height=Screen.height,files=reviewFiles20Xb.ToArray()};
                File.WriteAllText(Path.Combine(reviewDir20Xb,"manifest.json"),JsonUtility.ToJson(manifest,true));
            }
            foreach(string line in checks.Take(12))TestContext.WriteLine(line);
            Assert.That(reviewFiles20Xb.Count,Is.EqualTo(expectedShots),"Every capture was made.");
            Assert.That(hints.Count,Is.EqualTo(W11bHints.Length*ReviewThemes20Xb.Length*ReviewText20Xb.Length),"Every hint was measured.");
            Assert.That(compact.Count,Is.EqualTo(3*(1+ReviewThemes20Xb.Length)),"Every compact size in default and the styles.");
            Assert.That(compactCarry20Z.Count,Is.EqualTo(1+ReviewThemes20Xb.Length),"The pooled-label check in default and the styles.");
            Assert.That(buttonCentres20Xb.Count,Is.GreaterThan(0));
            foreach(string theme in ReviewThemes20Xb)foreach(int text in ReviewText20Xb)foreach(string role in new[]{"ViewDice","PreviousSubmission","card price"})
            {
                var mine=secondaries20Z.Where(s=>s.theme==theme&&s.textPercent==text&&s.role==role).ToList();
                Assert.That(mine.Any(s=>s.enabled)&&mine.Any(s=>!s.enabled),Is.True,theme+" "+text+"% "+role+": measured enabled and disabled.");
            }
            Assert.That(bosses20Z.Count,Is.EqualTo(ReviewThemes20Xb.Length*ReviewText20Xb.Length),"The boss formula in every style and text size.");
            Assert.That(contrastReport20Z.Count,Is.GreaterThan(0));
            Assert.That(reviewFailures20Xb,Is.Empty,string.Join("\n",reviewFailures20Xb.Take(20)));
        }
    }
}
