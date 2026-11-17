using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using DiceCraft.Core;
using DiceCraft.Presentation;
using DiceCraft.Verification;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UObj = UnityEngine.Object;

namespace DiceCraft.Tests
{
    /// <summary>
    /// V1.1 task W11b (review follow-up of the W11 play screen). Each check reads the rendered frame or the real layout:
    /// label contrast on filled surfaces in the default and Swiss themes, texts inside the visible frame of their surface
    /// (used by the AX text pass), the settle panel's reached state, balanced two-line hints, and the formula chip row
    /// at 150 % with the longest names.
    /// </summary>
    public sealed partial class GameplayPlayModeTests
    {
        // ================================================================== surfaces and their visible frames
        private static readonly Dictionary<Sprite,Color32[]> w11bSpritePixels=new Dictionary<Sprite,Color32[]>();
        /// <summary>A sprite's own pixels (sRGB), read through a render texture because UI textures are not CPU-readable.</summary>
        private static Color32[] W11bSpritePixels(Sprite sprite,out int width,out int height)
        {
            width=height=0;if(sprite==null)return null;
            Rect r;try{r=sprite.textureRect;}catch(Exception){return null;}
            width=Mathf.RoundToInt(r.width);height=Mathf.RoundToInt(r.height);
            if(width<1||height<1)return null;
            if(w11bSpritePixels.TryGetValue(sprite,out var cached))return cached;
            var texture=sprite.texture;
            var rt=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var previous=RenderTexture.active;var copy=new Texture2D(width,height,TextureFormat.RGBA32,false,false);
            try{Graphics.Blit(texture,rt);RenderTexture.active=rt;copy.ReadPixels(new Rect(r.x,r.y,width,height),0,0);copy.Apply(false);cached=copy.GetPixels32();}
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);UObj.Destroy(copy);}
            w11bSpritePixels[sprite]=cached;return cached;
        }
        /// <summary>Screen rects of a text's visible glyphs (whitespace excluded).</summary>
        private static List<Rect> W11bGlyphRects(TextMeshProUGUI text)
        {
            var rects=new List<Rect>();
            for(int i=0;i<text.textInfo.characterCount;i++)
            {
                var info=text.textInfo.characterInfo[i];if(!info.isVisible||char.IsWhiteSpace(info.character))continue;
                var a=text.transform.TransformPoint(info.bottomLeft);var b=text.transform.TransformPoint(info.topRight);
                rects.Add(Rect.MinMaxRect(Mathf.Min(a.x,b.x),Mathf.Min(a.y,b.y),Mathf.Max(a.x,b.x),Mathf.Max(a.y,b.y)));
            }
            return rects;
        }
        /// <summary>
        /// The visible inside of a sliced image in screen pixels: its rect drawn in, on each side, by what its sprite paints
        /// along the middle row or column inside the slice border. A filled surface loses its transparent margin and every
        /// pixel unlike the centre fill (frame lines, a bevel, a lip, double lines); an open ring loses its margin and its
        /// line. Sliced borders are drawn at 1 / (pixels per unit x multiplier), shrunk when the rect is smaller than both.
        /// </summary>
        private static Rect W11bInnerRect(Image image,bool ring)
        {
            var rt=image.rectTransform;
            if(image.sprite==null||image.type!=Image.Type.Sliced)return V06ScreenRect(rt);
            var pixels=W11bSpritePixels(image.sprite,out int w,out int h);
            if(pixels==null||w<3||h<3)return V06ScreenRect(rt);
            var border=image.sprite.border;var fill=pixels[(h/2)*w+w/2];
            int Inset(Func<int,Color32> at,int count,float border1)
            {
                int limit=Mathf.Min(count/2,Mathf.RoundToInt(border1)),inset=0;
                for(int i=0;i<limit;i++)
                {
                    var p=at(i);bool opaque=p.a>=128;
                    bool frame=ring?opaque:!opaque||Math.Abs(p.r-fill.r)+Math.Abs(p.g-fill.g)+Math.Abs(p.b-fill.b)>48;
                    if(frame)inset=i+1;
                }
                return inset;
            }
            int midX=w/2,midY=h/2;
            float left=Inset(i=>pixels[midY*w+i],w,border.x),right=Inset(i=>pixels[midY*w+w-1-i],w,border.z);
            float bottom=Inset(i=>pixels[i*w+midX],h,border.y),top=Inset(i=>pixels[(h-1-i)*w+midX],h,border.w);
            Rect local=rt.rect;float ppu=Mathf.Max(.0001f,image.pixelsPerUnit*image.pixelsPerUnitMultiplier);
            float sx=Mathf.Min(1,local.width/Mathf.Max(.0001f,(border.x+border.z)/ppu)),sy=Mathf.Min(1,local.height/Mathf.Max(.0001f,(border.y+border.w)/ppu));
            var a=rt.TransformPoint(new Vector3(local.xMin+left/ppu*sx,local.yMin+bottom/ppu*sy));
            var b=rt.TransformPoint(new Vector3(local.xMax-right/ppu*sx,local.yMax-top/ppu*sy));
            return Rect.MinMaxRect(Mathf.Min(a.x,b.x),Mathf.Min(a.y,b.y),Mathf.Max(a.x,b.x),Mathf.Max(a.y,b.y));
        }
        /// <summary>A visible image: a filled surface (its sprite paints its centre) or, with filled false, an open sliced ring.</summary>
        private static bool W11bPaints(Image image,bool filled)
        {
            if(!image.enabled||!image.gameObject.activeInHierarchy||image.canvasRenderer.cull)return false;
            float alpha=image.color.a*image.canvasRenderer.GetInheritedAlpha();
            if(!filled)return image.sprite!=null&&image.type==Image.Type.Sliced&&!image.fillCenter&&alpha>=.3f;
            if(alpha<.5f||image.type==Image.Type.Sliced&&!image.fillCenter)return false;
            if(image.sprite==null)return true;
            var pixels=W11bSpritePixels(image.sprite,out int w,out int h);
            return pixels==null||pixels[(h/2)*w+w/2].a>=128;
        }
        /// <summary>
        /// The surface a text is drawn on and the part of it inside its drawn frame (screen pixels): the topmost filled image
        /// drawn before the text that holds the centre of its glyphs and at least half of their area, drawn in by its own
        /// outline and by the inner edge of every open ring drawn over it before the text. Null when only the table (or an
        /// open frame such as the tray) is under the text. <paramref name="order"/> is the canvas's graphics in drawing order.
        /// </summary>
        private static Rect? W11bVisibleFrame(TextMeshProUGUI text,List<Rect> glyphs,Graphic[] order,out string surface)
        {
            surface=null;if(glyphs.Count==0)return null;
            var union=Rect.MinMaxRect(glyphs.Min(g=>g.xMin),glyphs.Min(g=>g.yMin),glyphs.Max(g=>g.xMax),glyphs.Max(g=>g.yMax));
            int index=Array.IndexOf(order,text),at=-1;if(index<0)return null;
            for(int i=index-1;i>=0&&at<0;i--)
            {
                if(!(order[i] is Image image)||!W11bPaints(image,true))continue;
                var r=V06ScreenRect(image.rectTransform);if(!r.Contains(union.center))continue;
                float overlap=Mathf.Max(0,Mathf.Min(r.xMax,union.xMax)-Mathf.Max(r.xMin,union.xMin))*Mathf.Max(0,Mathf.Min(r.yMax,union.yMax)-Mathf.Max(r.yMin,union.yMin));
                if(overlap>=.5f*union.width*union.height)at=i;
            }
            if(at<0)return null;
            var under=(Image)order[at];surface=W11bPath(under.transform);var frame=W11bInnerRect(under,false);
            for(int i=at+1;i<index;i++)
            {
                if(!(order[i] is Image ring)||!W11bPaints(ring,false))continue;
                if(!V06ScreenRect(ring.rectTransform).Contains(union.center))continue;
                var inner=W11bInnerRect(ring,true);surface+=" + ring "+ring.name;
                frame=Rect.MinMaxRect(Mathf.Max(frame.xMin,inner.xMin),Mathf.Max(frame.yMin,inner.yMin),Mathf.Min(frame.xMax,inner.xMax),Mathf.Min(frame.yMax,inner.yMax));
            }
            return frame;
        }
        private static string W11bPath(Transform t)=>t.parent!=null?t.parent.name+"/"+t.name:t.name;
        /// <summary>W11b: a text on two or more lines has at least three characters on every line (no orphan).</summary>
        private static void W11bAssertNoOrphan(TextMeshProUGUI text,string scope)
        {
            text.ForceMeshUpdate();var info=text.textInfo;if(info.lineCount<2)return;
            for(int l=0;l<info.lineCount;l++)
            {
                var line=info.lineInfo[l];int visible=0;
                for(int c=line.firstCharacterIndex;c<=line.lastCharacterIndex&&c<info.characterCount;c++){var ch=info.characterInfo[c];if(ch.isVisible&&!char.IsWhiteSpace(ch.character))visible++;}
                Assert.That(visible,Is.GreaterThanOrEqualTo(3),scope+" "+text.name+" line "+(l+1)+" of "+info.lineCount+" has "+visible+" characters: '"+text.text.Replace("\n","/")+"'");
            }
        }

        // ================================================================== fault 1: labels on filled surfaces read (4.5:1)
        /// <summary>
        /// Every visible label drawn on a filled surface (the surface rule of the visible-frame check) against that surface
        /// as drawn: the frame is captured with the labels hidden, and the median colour under each label's glyphs is its
        /// fill. WCAG 2 contrast of the label colour on that fill is at least 4.5:1. Labels of disabled controls are exempt
        /// (WCAG: inactive components). Marks on the felt (slot numbers, empty-slot dots) are measured too: with a theme's
        /// onFelt token they must read (4.5:1 for the numbers, 3:1 for the dots); without one they keep today's colours.
        /// </summary>
        private IEnumerator W11bContrastPass(string scope,List<string> report,List<string> failures)
        {
            Canvas.ForceUpdateCanvases();
            var overlay=(Transform)typeof(GameApp).GetField("overlay",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(app);
            var order=app.canvas.GetComponentsInChildren<Graphic>(false);
            var labels=new List<KeyValuePair<TextMeshProUGUI,List<Rect>>>();var surfaces=new Dictionary<TextMeshProUGUI,string>();
            var felt=new List<KeyValuePair<TextMeshProUGUI,List<Rect>>>();
            foreach(var t in app.canvas.GetComponentsInChildren<TextMeshProUGUI>(false))
            {
                if(!t.enabled||!t.gameObject.activeInHierarchy||string.IsNullOrWhiteSpace(t.text)||t.color.a<.9f||t.canvasRenderer.GetInheritedAlpha()<.9f)continue;
                if(overlay!=null&&t.transform.IsChildOf(overlay))continue;
                // A disabled control is exempt; a formula chip is a readout that only stops taking clicks while the hand scores.
                var control=t.GetComponentInParent<Selectable>();
                if(control!=null&&!control.interactable&&(control.transform.parent==null||control.transform.parent.name!="ScorePanel"))continue;
                t.ForceMeshUpdate();var rects=W11bGlyphRects(t);if(rects.Count==0)continue;
                if(t.name.StartsWith("SlotIndex",StringComparison.Ordinal)||t.name.StartsWith("EmptySlot",StringComparison.Ordinal)){felt.Add(new KeyValuePair<TextMeshProUGUI,List<Rect>>(t,rects));continue;}
                if(W11bVisibleFrame(t,rects,order,out string surface)==null)continue;
                labels.Add(new KeyValuePair<TextMeshProUGUI,List<Rect>>(t,rects));surfaces[t]=surface;
            }
            Assert.That(labels.Count,Is.GreaterThan(10),scope+": too few labels on surfaces.");
            var all=labels.Concat(felt).ToList();
            yield return new WaitForEndOfFrame();var drawn=ScreenCapture.CaptureScreenshotAsTexture();
            foreach(var pair in all)pair.Key.enabled=false;
            yield return new WaitForEndOfFrame();var shot=ScreenCapture.CaptureScreenshotAsTexture();
            foreach(var pair in all)pair.Key.enabled=true;Canvas.ForceUpdateCanvases();
            try
            {
                var px=shot.GetPixels32();var dx=drawn.GetPixels32();int w=shot.width,h=shot.height;
                bool token=UiKit.OnFelt(Color.clear).a>0;
                foreach(var pair in all)
                {
                    var under=new List<Color>();var ink=new List<KeyValuePair<int,Color>>();
                    foreach(var r in pair.Value)
                        for(int y=Mathf.Max(0,Mathf.FloorToInt(r.yMin));y<Mathf.Min(h,Mathf.CeilToInt(r.yMax));y++)
                            for(int x=Mathf.Max(0,Mathf.FloorToInt(r.xMin));x<Mathf.Min(w,Mathf.CeilToInt(r.xMax));x++)
                            {
                                var p=px[y*w+x];var q=dx[y*w+x];under.Add(p);
                                ink.Add(new KeyValuePair<int,Color>(Math.Abs(p.r-q.r)+Math.Abs(p.g-q.g)+Math.Abs(p.b-q.b),q));
                            }
                    if(under.Count<6)continue;
                    under.Sort((a,b)=>GameApp.Luminance11(a).CompareTo(GameApp.Luminance11(b)));
                    Color fill=under[under.Count/2];var label=pair.Key.color;label.a=1;
                    float ratio=GameApp.Contrast11(label,fill);
                    // For the record: the glyph pixels as drawn (the tenth of the glyph pixels that differ most from the fill):
                    // thin strokes never draw the full label colour, which is why the pick aims above AA.
                    ink.Sort((a,b)=>b.Key.CompareTo(a.Key));var core=ink.Take(Math.Max(1,ink.Count/10)).Select(k=>k.Value).ToList();
                    core.Sort((a,b)=>GameApp.Luminance11(a).CompareTo(GameApp.Luminance11(b)));float drawnRatio=GameApp.Contrast11(core[core.Count/2],fill);
                    bool onFelt=!surfaces.ContainsKey(pair.Key);
                    float minimum=!onFelt?GameApp.MinContrast11:!token?0:pair.Key.name.StartsWith("EmptySlot",StringComparison.Ordinal)?3f:GameApp.MinContrast11;
                    string line=scope+" "+W11bPath(pair.Key.transform)+" on "+(onFelt?"felt":surfaces[pair.Key])+" '"+pair.Key.text.Replace("\n","/")+"' label #"+ColorUtility.ToHtmlStringRGB(label)+
                        " fill #"+ColorUtility.ToHtmlStringRGB(fill)+" "+ratio.ToString("0.00",CultureInfo.InvariantCulture)+":1 (glyphs as drawn "+drawnRatio.ToString("0.00",CultureInfo.InvariantCulture)+":1)"+
                        (minimum>0?" min "+minimum.ToString("0.0",CultureInfo.InvariantCulture):" default felt mark, not graded");
                    report.Add(line);if(ratio<minimum)failures.Add(line);
                }
            }
            finally{UObj.Destroy(shot);UObj.Destroy(drawn);}
        }

        [UnityTest] public IEnumerator PLAY_V11_W11b_LabelsOnFilledSurfacesReachContrastInDefaultAndSwiss()
        {
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);int previewValue=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            string savedTheme=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            var report=new List<string>();var failures=new List<string>();
            try
            {
                PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);
                StartRun("71109");
                foreach(string theme in new[]{"default","swiss"})
                {
                    app.ApplyTheme11(theme);Assert.That(app.ThemeId11,Is.EqualTo(theme));
                    // The default theme sets no onFelt token: its felt marks keep today's colours exactly.
                    if(theme=="default")Assert.That(UiKit.OnFelt(Color.magenta),Is.EqualTo(Color.magenta),"The default palette must not set onFelt.");
                    else Assert.That(UiKit.OnFelt(Color.magenta),Is.Not.EqualTo(Color.magenta),"Swiss sets onFelt for marks on its light felt.");
                    // Selecting: the primary Confirm, the chosen die's tag, the expected-score chips, locked dice in their slots.
                    W11Apply(W11Scene(W11Cards3,new[]{4,4,6,2,4},"bbbrb",2,true));
                    app.Session.TogglePending(app.Session.State.dice[4].dieId);app.Refresh();yield return W11Settle();
                    yield return W11bContrastPass(theme+"-selecting",report,failures);
                    // A boss stage before the first throw: the boss card, the Roll primary.
                    W11Apply(W11Scene(new[]{"C39","C139","C34","C56","C08"},new[]{1,2,3,4,5},"bbrbb",0,false,stageIndex:8,modifier:"B02",target:"13500",items:2));yield return W11Settle();
                    yield return W11bContrastPass(theme+"-boss",report,failures);
                    // Mid-scoring on the total: the lit chips, the total chip, the "+N" tickets, the card badges, the gain banner.
                    W11Apply(W11Scene(new[]{"C39","C139","C34","C08","C46"},new[]{4,4,4,4,2},"bbbbr",4,true,target:"3000",score:"0"));yield return W11Settle();
                    var result=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(result.accepted,Is.True,result.error);
                    bool enabled=app.enabled;app.enabled=false;
                    try
                    {
                        W11HoldAt(PresentationPhase.FinalScore,.95f);yield return null;
                        Assert.That(app.ScoreTickets11,Is.GreaterThan(0),"The tickets must be on screen.");Assert.That(app.ScoreCardBadges11,Is.GreaterThan(0),"The card badges must be on screen.");
                        Assert.That(HasActive("TopGain11"),Is.True,"The gain banner must be on screen.");
                        yield return W11bContrastPass(theme+"-scoring-total",report,failures);
                    }
                    finally{app.enabled=enabled;}
                    // After the hand, qualified: the stable chips and badges, the settle panel's rewards, the End-stage button.
                    app.SkipVisual();yield return W11Settle();Assert.That(app.Session.State.stage.qualified,Is.True);
                    yield return W11bContrastPass(theme+"-qualified",report,failures);
                }
            }
            finally
            {
                File.WriteAllLines(Path.Combine(W11Output,"w11b-contrast.txt"),report);
                app.ApplyTheme11(GameTheme.DefaultId);
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,previewValue);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,savedTheme);PlayerPrefs.Save();
            }
            Assert.That(report.Count,Is.GreaterThan(80),"Too few labels measured.");
            Assert.That(failures,Is.Empty,"Labels below their contrast minimum:\n"+string.Join("\n",failures));
        }

        // ================================================================== fault 3: never "离目标还差 0"
        [UnityTest] public IEnumerator PLAY_V11_W11b_SettlePanelTurnsToReachedTheMomentTheTotalReachesTheTarget()
        {
            StartRun("71110");
            var displayed=typeof(GameApp).GetMethod("DisplayedStageScore11",BindingFlags.NonPublic|BindingFlags.Instance);
            foreach(string speed in new[]{"Normal","Fast"})
            {
                if(speed!="Normal")FinalOptions07(speed,false);
                // The checkpoint's scoring scene: 0 of 3,000 before, the hand scores well past it, so the total crosses the
                // target part-way through the roll-in.
                W11Apply(W11Scene(new[]{"C39","C139","C34","C08","C46"},new[]{4,4,4,4,2},"bbbbr",4,true,target:"3000",score:"0"));yield return W11Settle();
                Assert.That(HasActive("GoalGap11"),Is.True);Assert.That(FindActive<TextMeshProUGUI>("GoalGap11").text,Is.Not.EqualTo("0"));
                var result=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(result.accepted,Is.True,result.error);
                var target=ExactScoreMath.Parse("3000");
                bool enabled=app.enabled;app.enabled=false;int gapFrames=0,reachedFrames=0,reachedInFinal=0,gapInFinal=0;bool reached=false;
                try
                {
                    var player=W11Player();var beats=player.Sequence.Beats.ToArray();
                    foreach(var beat in beats)
                        foreach(float f in new[]{.02f,.1f,.2f,.3f,.4f,.6f,.8f,.98f})
                        {
                            double at=beat.StartMs+Math.Max(1,beat.DurationMs*f);double step=at-player.Clock.ElapsedMilliseconds;
                            if(step>0)player.Tick(step/1000.0);if(!player.IsPlaying)break;
                            W11Invoke("TickScore11");Canvas.ForceUpdateCanvases();
                            string frame=speed+" "+beat.Phase+" "+f.ToString("0.00",CultureInfo.InvariantCulture);
                            bool gap=HasActive("GoalGap11"),qualified=HasActive("Qualified");
                            Assert.That(gap^qualified,Is.True,frame+": the panel shows exactly one state (gap "+gap+", reached "+qualified+").");
                            var shown=ExactScoreMath.Parse((string)displayed.Invoke(app,null));
                            if(gap)
                            {
                                gapFrames++;if(beat.Phase==PresentationPhase.FinalScore)gapInFinal++;
                                Assert.That(reached,Is.False,frame+": the panel went back to the gap after showing the reached state.");
                                Assert.That(FindActive<TextMeshProUGUI>("GoalGap11").text,Is.Not.EqualTo("0"),frame+": the panel reads 离目标还差 0.");
                                Assert.That(shown<target,Is.True,frame+": the shown total "+shown+" has reached the target but the panel still shows the gap.");
                            }
                            else
                            {
                                reachedFrames++;reached=true;if(beat.Phase==PresentationPhase.FinalScore)reachedInFinal++;
                                Assert.That(shown>=target||beat.Phase>=PresentationPhase.PostScore,Is.True,frame+": reached shown before the total reached the target.");
                            }
                        }
                }
                finally{app.enabled=enabled;}
                Assert.That(gapFrames,Is.GreaterThan(0),speed+": no frame before the target was reached.");
                Assert.That(reachedFrames,Is.GreaterThan(0),speed+": the panel never showed the reached state.");
                if(speed=="Normal")Assert.That(gapInFinal>0&&reachedInFinal>0,Is.True,"Normal speed: the switch must happen inside the roll-in (FinalScore), gap frames "+gapInFinal+", reached frames "+reachedInFinal+".");
                app.SkipVisual();yield return W11Settle();
                Assert.That(HasActive("Qualified"),Is.True);Assert.That(HasActive("GoalGap11"),Is.False);
            }
        }

        // ================================================================== fault 4: balanced two-line hints
        /// <summary>Every hint the action row can show (the reasons of DrawActions11, the transaction summaries that stand in
        /// for them on the play screen, the first-time tip prefix), at each text size, in the real ActionReason label.</summary>
        private static readonly string[] W11bHints=
        {
            "计分中…","锁定中…","掷骰中…","按“掷骰”开始这一手。","没有可掷的骰子：提交已锁定的骰子。","提交栏只剩 1 格：先取消多选的骰子。",
            "按“锁定”留下选中的骰子；再点一次骰子可以取消。","按“提交”计分。","按“提交”计分，或者重掷其余的骰子。",
            "首领减益“满员”：要放满提交栏（5 颗）才能提交。","点选要留下的骰子，再按“锁定”；也可以重掷。","投掷已用完：点选要留下的骰子，再按“锁定”。",
            "已达标：按“掷骰”继续，或者按“结束本关”结算。","已达标：可以继续提交，也可以结束本关。",
            "玻璃骰已破碎 2 颗 · 提交容量保持","本次计分有效全红 · 永久骰面不变","本手 6 点偏置已应用","本关临时骰 +2","本次遥控投掷已提交",
        };
        [UnityTest] public IEnumerator PLAY_V11_W11b_HintsBreakAtBalancedPunctuationWithoutOrphans()
        {
            StartRun("71111");var report=new List<string>();
            var balanced=typeof(GameApp).GetMethod("BalancedLines11",BindingFlags.NonPublic|BindingFlags.Static);
            foreach(int font in new[]{100,125,150})
            {
                W11TextScale(font);W11Apply(W11Scene(W11Cards3,new[]{4,4,6,2,4},"bbbrb",2,true));yield return W11Settle();
                var label=FindActive<TextMeshProUGUI>("ActionReason");float width=label.rectTransform.rect.width;string original=label.text;
                try
                {
                    foreach(string hint in W11bHints)
                    {
                        string text=(string)balanced.Invoke(null,new object[]{label,hint,width});
                        label.text=text;label.ForceMeshUpdate();
                        string scope="font"+font+" '"+hint+"'";var info=label.textInfo;
                        report.Add(scope+" -> '"+text.Replace("\n"," / ")+"' lines "+info.lineCount+" size "+label.fontSize.ToString("0.0",CultureInfo.InvariantCulture));
                        Assert.That(label.isTextTruncated,Is.False,scope+" truncated");
                        Assert.That(info.lineCount,Is.LessThanOrEqualTo(2),scope+" takes "+info.lineCount+" lines");
                        W11bAssertNoOrphan(label,scope);
                        if(info.lineCount==2)
                        {
                            // Balanced: the shorter line is at least a third of the longer one; with punctuation in the middle
                            // half of the text the break comes right after it.
                            float a=info.lineInfo[0].length,b=info.lineInfo[1].length;
                            Assert.That(Mathf.Min(a,b),Is.GreaterThanOrEqualTo(Mathf.Max(a,b)/3f),scope+" unbalanced lines "+a+" / "+b);
                            int mid=hint.Length/4;bool punctuationInMiddle=hint.Skip(mid).Take(hint.Length/2).Any(c=>"，。；：、！？".IndexOf(c)>=0);
                            if(punctuationInMiddle&&text.Contains("\n"))
                            {
                                char last=text[text.IndexOf('\n')-1],next=text[text.IndexOf('\n')+1];
                                Assert.That("，。；：、！？）”".IndexOf(last)>=0||"（“".IndexOf(next)>=0,Is.True,scope+" breaks away from punctuation: '"+text.Replace("\n","/")+"'");
                            }
                        }
                    }
                }
                finally{label.text=original;}
            }
            W11TextScale(100);
            File.WriteAllLines(Path.Combine(W11Output,"w11b-hints.txt"),report);
        }

        // ================================================================== fault 5: the chip row at 150 % with the longest names
        /// <summary>The formula row: every chip, sign and text inside the row, none over another; chip names at the body size
        /// times the text scale (the base always named); values and names inside their chip's visible frame.</summary>
        private void W11bAssertChipRow(string scope,List<string> report)
        {
            Canvas.ForceUpdateCanvases();
            var row=FindActive<RectTransform>("ScorePanel");var rowRect=V06ScreenRect(row);float slack=1;
            var order=app.canvas.GetComponentsInChildren<Graphic>(false);
            var parts=new List<KeyValuePair<string,Rect>>();int named=0,unnamed=0;
            foreach(Transform child in row)
            {
                if(!child.gameObject.activeInHierarchy)continue;
                var text=child.GetComponent<TextMeshProUGUI>();
                if(text!=null)
                {
                    if(string.IsNullOrWhiteSpace(text.text))continue;text.ForceMeshUpdate();var glyphs=W11bGlyphRects(text);if(glyphs.Count==0)continue;
                    parts.Add(new KeyValuePair<string,Rect>(child.name,Rect.MinMaxRect(glyphs.Min(g=>g.xMin),glyphs.Min(g=>g.yMin),glyphs.Max(g=>g.xMax),glyphs.Max(g=>g.yMax))));
                    continue;
                }
                parts.Add(new KeyValuePair<string,Rect>(child.name,V06ScreenRect((RectTransform)child)));
                if(!child.name.StartsWith("Factor",StringComparison.Ordinal))continue;
                var name=child.Find("Name");var value=child.Find("Label").GetComponent<TextMeshProUGUI>();
                bool shown=name!=null&&name.gameObject.activeInHierarchy;if(shown)named++;else unnamed++;
                if(child.name=="Factor0")Assert.That(shown,Is.True,scope+": the base chip keeps its name.");
                Assert.That(value.isTextTruncated,Is.False,scope+" "+child.name+" value truncated");
                foreach(var t in shown?new[]{name.GetComponent<TextMeshProUGUI>(),value}:new[]{value})
                {
                    if(t==null)continue;
                    if(t.name=="Name")Assert.That(t.fontSize,Is.GreaterThanOrEqualTo(19.5f*UiKit.FontScale-.01f),scope+" "+child.name+" name "+t.fontSize+" units at text scale "+UiKit.FontScale);
                    t.ForceMeshUpdate();var glyphs=W11bGlyphRects(t);var frame=W11bVisibleFrame(t,glyphs,order,out string surface);
                    Assert.That(frame.HasValue,Is.True,scope+" "+child.name+"/"+t.name+" has no chip under it");
                    foreach(var g in glyphs)Assert.That(g.xMin>=frame.Value.xMin-1.5f&&g.xMax<=frame.Value.xMax+1.5f&&g.yMin>=frame.Value.yMin-1.5f&&g.yMax<=frame.Value.yMax+1.5f,Is.True,
                        scope+" "+child.name+"/"+t.name+" glyph "+g+" outside the chip's visible frame "+frame.Value+" ('"+t.text+"')");
                }
            }
            Assert.That(parts.Count(p=>p.Key.StartsWith("Factor",StringComparison.Ordinal)),Is.GreaterThanOrEqualTo(1),scope+": no factor chips.");
            foreach(var p in parts)
                Assert.That(p.Value.xMin>=rowRect.xMin-slack&&p.Value.xMax<=rowRect.xMax+slack&&p.Value.yMin>=rowRect.yMin-slack&&p.Value.yMax<=rowRect.yMax+slack,Is.True,scope+" "+p.Key+" "+p.Value+" leaves the row "+rowRect);
            for(int i=0;i<parts.Count;i++)for(int j=i+1;j<parts.Count;j++)
            {
                var a=parts[i].Value;var b=parts[j].Value;
                Assert.That(Math.Min(a.xMax,b.xMax)-Math.Max(a.xMin,b.xMin)>1&&Math.Min(a.yMax,b.yMax)-Math.Max(a.yMin,b.yMin)>1,Is.False,scope+": "+parts[i].Key+" and "+parts[j].Key+" overlap ("+a+" / "+b+")");
            }
            report.Add(scope+": "+string.Join(" | ",row.Cast<Transform>().Where(c=>c.gameObject.activeInHierarchy&&c.name.StartsWith("Factor",StringComparison.Ordinal))
                .Select(c=>(c.Find("Name")!=null&&c.Find("Name").gameObject.activeInHierarchy?c.Find("Name").GetComponent<TextMeshProUGUI>().text+" ":"")+c.Find("Label").GetComponent<TextMeshProUGUI>().text))+
                " ; named "+named+", value only "+unnamed+", text scale "+UiKit.FontScale.ToString("0.00",CultureInfo.InvariantCulture));
        }
        private void W11bApplyFixture(Art07FixtureKind kind,Action<DebugScene> change=null)
        {
            Assert.That(app.UnlockDebug("XD is god"),Is.True);Assert.That(app.EnterDebugWorkspace(),Is.True);
            var s=app.Session.State;var scene=Art07Fixtures.Create(app.Config,kind,s.nextItemSerial,s.nextDieSerial);scene.sourceRunId=s.sourceRunId;
            change?.Invoke(scene);W11Apply(scene);
        }
        [UnityTest] public IEnumerator PLAY_V11_W11b_FormulaChipRowFitsWithTheLongestNamesAt150()
        {
            StartRun("71112");var report=new List<string>();
            try
            {
                foreach(int font in new[]{100,150})
                {
                    W11TextScale(font);string tag="font"+font;
                    // The longest row: ten cards (an enchanted ×2 among them), all-red dice on a straight with pairs, the 灵光一闪
                    // item and the 强弩之末 boss halving (two four-character names) on a boss stage's second hand.
                    W11bApplyFixture(Art07FixtureKind.MaximumTable,scene=>
                    {
                        scene.stageIndex=8;scene.modifierId="B30";scene.submissionOrdinal=1;scene.doubleScore=true;
                        for(int i=0;i<scene.dice.Count;i++)
                        {
                            var result=scene.currentResults[i];var face=scene.dice[i].faces.Single(f=>f.faceId==result.currentFaceId);
                            face.baseColor="red";result.currentColor="red";
                        }
                    });
                    yield return W11Settle();
                    var factors=app.canvas.GetComponentsInChildren<UnityEngine.UI.Button>(false).Count(b=>b.transform.parent!=null&&b.transform.parent.name=="ScorePanel"&&b.name.StartsWith("Factor",StringComparison.Ordinal));
                    Assert.That(factors,Is.GreaterThanOrEqualTo(6),tag+": the longest-row fixture must show at least six factor chips.");
                    Assert.That(HasActive("Factor6")||HasActive("Factor7"),Is.True,tag+": the item and boss chips must be in the row.");
                    W11bAssertChipRow(tag+"-longest-preview",report);
                    // The zero-multiplier value ("×0 没有有效 5 点") is the longest value text.
                    W11bApplyFixture(Art07FixtureKind.ZeroMultiplier);yield return W11Settle();
                    W11bAssertChipRow(tag+"-zero-multiplier",report);
                    // After a hand with post-score changes: the "后置：…" summary takes room at the right end.
                    W11bApplyFixture(Art07FixtureKind.PermanentPost);yield return W11Settle();
                    Art07Fixtures.Submit(app,Art07FixtureKind.PermanentPost);app.SkipVisual();yield return W11Settle();
                    Assert.That(HasActive("StableResultFactors07"),Is.True,tag+": the post-score summary must be in the row.");
                    W11bAssertChipRow(tag+"-stable-with-post",report);
                    // On the total of a top-combo hand: the total chip widens for the combo marks.
                    W11bApplyFixture(Art07FixtureKind.Burst08);yield return W11Settle();
                    Art07Fixtures.Submit(app,Art07FixtureKind.Burst08);
                    bool enabled=app.enabled;app.enabled=false;
                    try{W11HoldAt(PresentationPhase.FinalScore,.95f);yield return null;W11bAssertChipRow(tag+"-burst-total",report);}
                    finally{app.enabled=enabled;}
                    app.SkipVisual();yield return W11Settle();
                }
            }
            finally{W11TextScale(100);File.WriteAllLines(Path.Combine(W11Output,"w11b-chip-row.txt"),report);}
        }
    }
}
