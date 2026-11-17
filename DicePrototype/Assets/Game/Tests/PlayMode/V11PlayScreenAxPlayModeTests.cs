using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UObj = UnityEngine.Object;

namespace DiceCraft.Tests
{
    /// <summary>
    /// V1.1 task W11 acceptance checks of what the player sees (DEV_PLAN 6: AX-01, AX-02, AX-03, AX-05). They read the
    /// rendered frame, not only text content: v1.0 shipped two defects (a covered boss readout and an undrawn title) that
    /// passed every text assertion.
    /// </summary>
    public sealed partial class GameplayPlayModeTests
    {
        // ------------------------------------------------------------------ AX-01: nothing covers a submitted die
        /// <summary>Screen polygon of a die body: the eight cube corners projected, convex hull, drawn in by a margin so the
        /// rounded edges and their anti-aliasing stay outside the sampled area.</summary>
        private List<Vector2> W11DieHull(string id,float inset)
        {
            var pivot=app.table.GetFaceOrientation(id);Assert.That(pivot,Is.Not.Null,"No 3D die for "+id);
            var points=new List<Vector2>();
            for(int i=0;i<8;i++)
            {
                var local=new Vector3((i&1)==0?-.5f:.5f,(i&2)==0?-.5f:.5f,(i&4)==0?-.5f:.5f);
                var screen=app.table.viewCamera.WorldToScreenPoint(pivot.TransformPoint(local));points.Add(new Vector2(screen.x,screen.y));
            }
            points=points.Distinct().OrderBy(p=>p.x).ThenBy(p=>p.y).ToList();
            float Cross(Vector2 o,Vector2 a,Vector2 b)=>(a.x-o.x)*(b.y-o.y)-(a.y-o.y)*(b.x-o.x);
            var hull=new List<Vector2>();
            foreach(var p in points){while(hull.Count>=2&&Cross(hull[hull.Count-2],hull[hull.Count-1],p)<=0)hull.RemoveAt(hull.Count-1);hull.Add(p);}
            for(int i=points.Count-2,t=hull.Count+1;i>=0;i--){var p=points[i];while(hull.Count>=t&&Cross(hull[hull.Count-2],hull[hull.Count-1],p)<=0)hull.RemoveAt(hull.Count-1);hull.Add(p);}
            hull.RemoveAt(hull.Count-1);
            var centre=new Vector2(hull.Average(p=>p.x),hull.Average(p=>p.y));
            return hull.Select(p=>centre+(p-centre)*(1-inset)).ToList();
        }
        private static bool W11Inside(List<Vector2> polygon,Vector2 p)
        {
            bool inside=false;
            for(int i=0,j=polygon.Count-1;i<polygon.Count;j=i++)
                if((polygon[i].y>p.y)!=(polygon[j].y>p.y)&&p.x<(polygon[j].x-polygon[i].x)*(p.y-polygon[i].y)/(polygon[j].y-polygon[i].y)+polygon[i].x)inside=!inside;
            return inside;
        }
        /// <summary>Captures the frame with and without the interface canvas and counts changed pixels inside each die body.</summary>
        private IEnumerator W11AssertDiceUncovered(string label,IList<string> dice,List<string> report)
        {
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();var with=ScreenCapture.CaptureScreenshotAsTexture();
            app.canvas.enabled=false;
            yield return new WaitForEndOfFrame();var without=ScreenCapture.CaptureScreenshotAsTexture();
            app.canvas.enabled=true;Canvas.ForceUpdateCanvases();
            try
            {
                var a=with.GetPixels32();var b=without.GetPixels32();int w=with.width,h=with.height;
                foreach(string id in dice)
                {
                    var hull=W11DieHull(id,.16f);int inside=0,changed=0;
                    int x0=Mathf.Max(0,Mathf.FloorToInt(hull.Min(p=>p.x))),x1=Mathf.Min(w-1,Mathf.CeilToInt(hull.Max(p=>p.x)));
                    int y0=Mathf.Max(0,Mathf.FloorToInt(hull.Min(p=>p.y))),y1=Mathf.Min(h-1,Mathf.CeilToInt(hull.Max(p=>p.y)));
                    for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                    {
                        if(!W11Inside(hull,new Vector2(x+.5f,y+.5f)))continue;inside++;
                        var p=a[y*w+x];var q=b[y*w+x];if(Math.Abs(p.r-q.r)+Math.Abs(p.g-q.g)+Math.Abs(p.b-q.b)>12)changed++;
                    }
                    report.Add(label+" "+id+" inside="+inside+" changed="+changed);
                    Assert.That(inside,Is.GreaterThan(200),label+": die "+id+" body not on screen.");
                    Assert.That(changed,Is.LessThanOrEqualTo(2),label+": the interface paints over submitted die "+id+" ("+changed+" of "+inside+" body pixels).");
                }
                if(report.Count<12)File.WriteAllBytes(Path.Combine(W11Output,"ax01-"+label+".png"),with.EncodeToPNG());
            }
            finally{UObj.Destroy(with);UObj.Destroy(without);}
        }

        [UnityTest] public IEnumerator PLAY_V11_AX01_ScoringFramesNeverCoverASubmittedDie()
        {
            StartRun("71101");var report=new List<string>();
            foreach(string speed in new[]{"Normal","Fast"})
            {
                if(speed!="Normal")FinalOptions07(speed,false);
                // Four blue fours against a low target: every source pops, the top tier lights the rims, the stage qualifies (stamp).
                W11Apply(W11Scene(new[]{"C39","C139","C34","C08","C46"},new[]{4,4,4,4,2},"bbbbr",4,true,target:"900",score:"0"));
                yield return W11Settle();
                var dice=app.Session.State.hand.orderedLockedIds.ToArray();
                var result=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(result.accepted,Is.True,result.error);
                bool enabled=app.enabled;app.enabled=false;
                try
                {
                    var player=W11Player();var beats=player.Sequence.Beats.ToArray();Assert.That(beats.Length,Is.GreaterThan(2));
                    int n=0;
                    foreach(var beat in beats)
                        foreach(float f in new[]{.02f,.5f,.98f})
                        {
                            double target=beat.StartMs+Math.Max(1,beat.DurationMs*f);double step=target-player.Clock.ElapsedMilliseconds;
                            if(step>0)player.Tick(step/1000.0);if(!player.IsPlaying)break;
                            W11Invoke("TickScore11");W11Invoke("TickLibraryEffects07",0f);W11Invoke("TickHighScore073",.05f);W11Invoke("TickQualifiedStamp07",.05f);
                            yield return W11AssertDiceUncovered(speed+"-"+(n++).ToString("00")+"-"+beat.Phase,dice,report);
                        }
                    Assert.That(n,Is.GreaterThanOrEqualTo(6),"Too few sampled frames.");
                    Assert.That(app.HighScoreTierShown073,Is.GreaterThan(0),"The fixture must reach a celebration tier.");
                }
                finally{app.enabled=enabled;}
                app.SkipVisual();yield return W11Settle();
            }
            File.WriteAllLines(Path.Combine(W11Output,"ax01-report.txt"),report);
        }

        // ------------------------------------------------------------------ AX-02: one primary action in every state
        /// <summary>Screen-level buttons in the primary colour. The formula chips (children of the score row) are not actions;
        /// in the default palette the card chip shares the gold of the primary colour.</summary>
        private UnityEngine.UI.Button[] W11PrimaryButtons()
            =>app.canvas.GetComponentsInChildren<UnityEngine.UI.Button>(false).Where(b=>b.gameObject.activeInHierarchy&&b.targetGraphic!=null
                &&b.transform.parent!=null&&b.transform.parent.name=="Screen"&&Vector4.Distance(b.targetGraphic.color,UiKit.Primary)<.01f).ToArray();
        /// <summary>One primary action in its fixed place. Enabled, it is the one primary-coloured button; a primary action that
        /// cannot be taken yet ("最多锁 N 颗") keeps the place, disabled, and then no button is primary-coloured.</summary>
        private void W11AssertOnePrimary(string state,string expected,bool enabled=true)
        {
            Canvas.ForceUpdateCanvases();var primary=W11PrimaryButtons();
            if(expected==null){Assert.That(primary,Is.Empty,state+": no action is available, so no primary-coloured button.");return;}
            Assert.That(new[]{"Roll","Confirm","Submit"},Does.Contain(expected));
            if(enabled)Assert.That(primary.Select(b=>b.name).ToArray(),Is.EqualTo(new[]{expected}),state+": exactly one primary-coloured button.");
            else Assert.That(primary,Is.Empty,state+": a disabled primary action is not drawn in the primary colour.");
            var button=Button(expected);Assert.That(button.interactable,Is.EqualTo(enabled),state);
            var rect=(RectTransform)button.transform;
            // The primary action always sits in the same place.
            Assert.That(rect.anchoredPosition,Is.EqualTo(W11PrimaryPosition??rect.anchoredPosition),state+": the primary button moved.");
            W11PrimaryPosition=rect.anchoredPosition;
        }
        private Vector2? W11PrimaryPosition;
        [UnityTest] public IEnumerator PLAY_V11_AX02_OnePrimaryActionButtonInEveryState()
        {
            StartRun("71102");
            foreach(int font in new[]{100,150})
            {
                W11TextScale(font);W11PrimaryPosition=null;
                W11Apply(W11Scene(W11Cards3,new[]{1,2,3,4,5},"bbbbb",0,false));yield return W11Settle();
                W11AssertOnePrimary("not thrown","Roll");Assert.That(HasActive("Confirm")||HasActive("Clear")||HasActive("Submit"),Is.False,"Unavailable actions are not shown.");
                Click("Roll");yield return WaitForIdle();yield return W11Settle();
                W11AssertOnePrimary("thrown, nothing chosen","Roll");
                ClickDie(app.Session.State.dice[0].dieId);yield return null;
                W11AssertOnePrimary("dice chosen","Confirm");Assert.That(Button("Clear").interactable,Is.True);Assert.That(HasActive("Submit"),Is.False);
                Click("Confirm");yield return WaitForIdle();yield return W11Settle();
                W11AssertOnePrimary("locked, can submit","Submit");Assert.That(Button("Roll").interactable,Is.True,"Re-roll stays as the secondary action.");
                // Too many chosen for the room left: the primary stays Confirm, disabled, and says how many fit.
                W11Apply(W11Scene(W11Cards3,new[]{4,4,6,2,4},"bbbrb",3,true,capacity:4));
                foreach(var die in app.Session.State.dice.Where(d=>!app.Session.State.hand.orderedLockedIds.Contains(d.dieId)))app.Session.TogglePending(die.dieId);
                app.Refresh();yield return null;
                W11AssertOnePrimary("too many chosen","Confirm",enabled:false);
                Assert.That(Button("Confirm").GetComponentInChildren<TextMeshProUGUI>().text,Does.Contain("最多锁 1 颗"));
                // Thrown, out of throws, nothing locked: no action to emphasise.
                var scene=W11Scene(W11Cards3,new[]{4,4,6,2,4},"bbbrb",0,true);scene.rollsRemaining=0;W11Apply(scene);yield return W11Settle();
                W11AssertOnePrimary("out of throws, nothing locked",null);
                // Busy: the presentation offers only Skip, as a second-level button.
                W11Apply(W11Scene(W11Cards3,new[]{4,4,4,2,6},"bbbrb",3,true));yield return W11Settle();
                var result=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(result.accepted,Is.True,result.error);
                Canvas.ForceUpdateCanvases();Assert.That(app.IsBusy,Is.True);W11AssertOnePrimary("scoring",null);Assert.That(Button("Skip").interactable,Is.True);
                app.SkipVisual();yield return W11Settle();
            }
            W11TextScale(100);
        }

        // ------------------------------------------------------------------ AX-05: every source pops where it is
        [UnityTest] public IEnumerator PLAY_V11_AX05_EachContributingDieAndCardPopsAtItsSource()
        {
            StartRun("71105");
            foreach(string speed in new[]{"Normal","Fast"})
            {
                if(speed!="Normal")FinalOptions07(speed,false);
                W11Apply(W11Scene(new[]{"C39","C34","C08","C46","C50"},new[]{4,4,4,4,2},"bbbbr",4,true,target:"100000"));yield return W11Settle();
                var dice=app.Session.State.hand.orderedLockedIds.ToArray();var slots=dice.ToDictionary(id=>id,id=>V06ScreenRect(FindActive<RectTransform>("Slot"+Array.IndexOf(dice,id))));
                var cards=app.Session.State.controllers.ToDictionary(c=>c.instanceId,c=>V06ScreenRect((RectTransform)Button("Controller_"+c.instanceId).transform));
                var before=app.Session.State;var result=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(result.accepted,Is.True,result.error);
                var receipt=PresentationReceiptAdapter.BuildCommitted(result,before,app.Session.State,app.Config);
                var popDice=new HashSet<string>();var popCards=new HashSet<string>();
                double deadline=Time.realtimeSinceStartupAsDouble+8;
                while(app.PresentationPlaying07&&Time.realtimeSinceStartupAsDouble<deadline)
                {
                    var layer=app.canvas.transform.Find("Score11Layer");
                    if(layer!=null)foreach(Transform child in layer)
                    {
                        if(!child.gameObject.activeInHierarchy)continue;var text=child.GetComponentInChildren<TextMeshProUGUI>();
                        if(text==null||string.IsNullOrEmpty(text.text)||text.color.a<.9f)continue;var r=V06ScreenRect((RectTransform)child);
                        if(child.name.StartsWith("DieTicket11_",StringComparison.Ordinal))
                        {
                            string id=child.name.Substring(12);Assert.That(slots.ContainsKey(id),Is.True,"Ticket on a die that was not submitted: "+id);
                            var hull=W11DieHull(id,0);float top=hull.Max(p=>p.y);
                            Assert.That(r.yMin,Is.GreaterThanOrEqualTo(top-1),"The +N of "+id+" must sit above its die, not on it.");
                            Assert.That(r.xMin>=slots[id].xMin-2&&r.xMax<=slots[id].xMax+2,Is.True,"The +N of "+id+" stays over its own slot.");
                            Assert.That(text.text,Does.Match(@"^\+\d|封印"));popDice.Add(id);
                        }
                        if(child.name.StartsWith("CardBadge11_",StringComparison.Ordinal))
                        {
                            string id=child.name.Substring(12);Assert.That(cards.ContainsKey(id),Is.True);
                            var card=cards[id];Assert.That(r.xMin>=card.xMin-2&&r.xMax<=card.xMax+2&&r.yMax<=card.yMax+20*app.canvas.scaleFactor,Is.True,"The badge of "+id+" stays on its card.");
                            popCards.Add(id);
                        }
                    }
                    yield return null;
                }
                CollectionAssert.AreEquivalent(dice,popDice,speed+": every submitted die shows its +N.");
                CollectionAssert.IsSubsetOf(receipt.ScoringEmitterIds.ToArray(),popCards.ToArray(),speed+": every card that took part shows its badge.");
                Assert.That(popCards,Is.Not.Empty);
                app.SkipVisual();yield return W11Settle();
            }
        }

        // ------------------------------------------------------------------ AX-03 and the new texts: drawn, inside, readable
        private static readonly string[] W11BodyTexts={"Chapter","MoneyLabel11","StageCardTitle11","BossModifierText","BossLoot11","StageGoalLabel11","StageReward11","StageNote11",
            "RollBudgetLabel","SubmitBudgetLabel","ItemsTitle","SlotsTitle","LockRule","Hand","FormulaCaptionText11","ActionReason","GoalGapLabel11","GoalReward11","QualifiedReward0","QualifiedReward1","QualifiedReward2","PoolTitle"};
        private static readonly string[] W11KeyNumbers={"StageScore","Target","Money","RollBudgetValue","SubmitBudgetValue","FinalScore","StableResultScore07","GoalGap11","StageGoal11","StageLabel11","BossModifierName"};
        private List<Rect> W11GlyphCheck(TextMeshProUGUI text,string scope)
        {
            text.ForceMeshUpdate();var rects=new List<Rect>();int expected=text.GetParsedText().Count(c=>!char.IsWhiteSpace(c));
            Assert.That(text.isTextTruncated,Is.False,scope+" "+text.name+" truncated");
            for(int i=0;i<text.textInfo.characterCount;i++)
            {
                var info=text.textInfo.characterInfo[i];if(!info.isVisible||char.IsWhiteSpace(info.character))continue;
                var a=text.transform.TransformPoint(info.bottomLeft);var b=text.transform.TransformPoint(info.topRight);
                rects.Add(Rect.MinMaxRect(Mathf.Min(a.x,b.x),Mathf.Min(a.y,b.y),Mathf.Max(a.x,b.x),Mathf.Max(a.y,b.y)));
            }
            Assert.That(rects.Count,Is.EqualTo(expected),scope+" "+text.name+" draws "+rects.Count+" of "+expected+" characters: '"+text.text+"'");
            var own=V06ScreenRect(text.rectTransform);float slack=1.5f;
            foreach(var r in rects)Assert.That(r.xMin>=own.xMin-slack&&r.xMax<=own.xMax+slack&&r.yMin>=own.yMin-slack&&r.yMax<=own.yMax+slack,Is.True,scope+" "+text.name+" glyph "+r+" outside its box "+own);
            return rects;
        }
        private IEnumerator W11TextPass(string scope)
        {
            Canvas.ForceUpdateCanvases();
            var texts=app.canvas.GetComponentsInChildren<TextMeshProUGUI>(false).Where(t=>t.gameObject.activeInHierarchy&&!string.IsNullOrWhiteSpace(t.text)&&
                (W11BodyTexts.Contains(t.name)||W11KeyNumbers.Contains(t.name)||t.name=="ShortText11"||t.name=="Name"&&t.transform.parent.name.StartsWith("Controller_",StringComparison.Ordinal)||t.name.StartsWith("Pending")||t.transform.parent.name.StartsWith("Factor",StringComparison.Ordinal))).ToArray();
            Assert.That(texts.Length,Is.GreaterThan(15),scope+": too few texts found.");
            var all=new List<KeyValuePair<TextMeshProUGUI,List<Rect>>>();
            foreach(var t in texts)
            {
                var rects=W11GlyphCheck(t,scope);all.Add(new KeyValuePair<TextMeshProUGUI,List<Rect>>(t,rects));
                // AX-03: body text at least 13 px and key numbers at least 21 px at 1280x720 (19.5 and 31.5 design units).
                float px=t.fontSize*app.canvas.scaleFactor,design=t.fontSize;
                if(W11BodyTexts.Contains(t.name)||t.name=="ShortText11")Assert.That(design,Is.GreaterThanOrEqualTo(19.49f),scope+" "+t.name+" body text "+design+" units");
                if(W11KeyNumbers.Contains(t.name)&&t.name!="StageLabel11"&&t.name!="BossModifierName")Assert.That(design,Is.GreaterThanOrEqualTo(31.49f),scope+" "+t.name+" key number "+design+" units");
                if(Screen.height==720&&(W11BodyTexts.Contains(t.name)||t.name=="ShortText11"))Assert.That(px,Is.GreaterThanOrEqualTo(12.99f),scope+" "+t.name);
            }
            // No two of these texts paint over each other.
            for(int i=0;i<all.Count;i++)for(int j=i+1;j<all.Count;j++)
                foreach(var a in all[i].Value)foreach(var b in all[j].Value)
                    if(Math.Min(a.xMax,b.xMax)-Math.Max(a.xMin,b.xMin)>1&&Math.Min(a.yMax,b.yMax)-Math.Max(a.yMin,b.yMin)>1)
                        Assert.Fail(scope+": "+all[i].Key.name+" and "+all[j].Key.name+" overlap ("+a+" / "+b+")");
            // GPU: each text changes its own pixels when shown (actually drawn, not covered by a later graphic).
            foreach(var t in texts)t.enabled=false;
            yield return new WaitForEndOfFrame();var hidden=ScreenCapture.CaptureScreenshotAsTexture();
            foreach(var t in texts)t.enabled=true;Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();var shown=ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                var a=hidden.GetPixels32();var b=shown.GetPixels32();int w=hidden.width,h=hidden.height;
                foreach(var pair in all)
                {
                    int ink=0;
                    foreach(var r in pair.Value)
                        for(int y=Mathf.Max(0,Mathf.FloorToInt(r.yMin));y<Mathf.Min(h,Mathf.CeilToInt(r.yMax));y++)
                            for(int x=Mathf.Max(0,Mathf.FloorToInt(r.xMin));x<Mathf.Min(w,Mathf.CeilToInt(r.xMax));x++)
                            {var p=a[y*w+x];var q=b[y*w+x];if(Math.Abs(p.r-q.r)+Math.Abs(p.g-q.g)+Math.Abs(p.b-q.b)>24)ink++;}
                    Assert.That(ink,Is.GreaterThanOrEqualTo(2*pair.Value.Count),scope+" "+pair.Key.name+" is not visibly drawn ('"+pair.Key.text+"', ink "+ink+")");
                }
            }
            finally{UObj.Destroy(hidden);UObj.Destroy(shown);}
        }
        /// <summary>Every button and die on the play screen is at least 40 physical pixels both ways (60 units at 720p).</summary>
        private void W11AssertHitTargets(string scope)
        {
            foreach(var control in app.canvas.GetComponentsInChildren<UnityEngine.UI.Selectable>(false).Where(c=>c.gameObject.activeInHierarchy&&c.interactable))
            {
                var r=V06ScreenRect((RectTransform)control.transform);
                Assert.That(r.width>=39.99f&&r.height>=39.99f,Is.True,scope+": "+control.name+" is "+r.width+" x "+r.height+" px");
            }
            foreach(var die in app.canvas.GetComponentsInChildren<DiePointer>(false).Where(d=>d.name.StartsWith("Die_",StringComparison.Ordinal)))
            {
                var r=V06ScreenRect((RectTransform)die.transform);Assert.That(r.width>=39.99f&&r.height>=39.99f,Is.True,scope+": "+die.name);
            }
        }
        [UnityTest] public IEnumerator PLAY_V11_W11_NewTextsAreDrawnCompleteAndReadableAtThreeTextSizes()
        {
            StartRun("71103");
            foreach(int font in new[]{100,125,150})
            {
                W11TextScale(font);string tag="font"+font;
                W11Apply(W11Scene(W11Cards3,new[]{4,4,6,2,4},"bbbrb",2,true));app.Session.TogglePending(app.Session.State.dice[4].dieId);app.Refresh();yield return W11Settle();
                yield return W11TextPass(tag+"-selecting");W11AssertHitTargets(tag+"-selecting");
                W11Apply(W11Scene(new[]{"C39","C139","C34","C56","C08"},new[]{1,2,3,4,5},"bbrbb",0,false,stageIndex:8,modifier:"B02",target:"13500"));yield return W11Settle();
                yield return W11TextPass(tag+"-boss");W11AssertHitTargets(tag+"-boss");
                W11Apply(W11Scene(new[]{"C39","C46","C50","C08"},new[]{5,5,3,5,1},"bbrbr",2,true,stageIndex:4,risk:true,target:"3000",score:"1240"));yield return W11Settle();
                yield return W11TextPass(tag+"-pressure");
                W11Apply(W11Scene(W11Cards7,new[]{6,6,5,3,1,4,2,6,5,1},"bbrbrbbrbb",3,true,items:3,capacity:8));yield return W11Settle();
                yield return W11TextPass(tag+"-ten-dice");W11AssertHitTargets(tag+"-ten-dice");
                // Qualified, after a scored hand: the stable result, the settle panel's rewards, the end-stage border.
                W11Apply(W11Scene(new[]{"C39","C46"},new[]{4,4,4,4,2},"bbbbr",4,true,target:"50"));yield return W11Settle();
                var result=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(result.accepted,Is.True,result.error);app.SkipVisual();yield return W11Settle();
                Assert.That(app.Session.State.stage.qualified,Is.True);Assert.That(HasActive("QualifiedGoldBorder"),Is.True);
                Assert.That(FindActive<TextMeshProUGUI>("FormulaCaptionText11").text,Does.StartWith("上一手"));
                yield return W11TextPass(tag+"-qualified");W11AssertHitTargets(tag+"-qualified");
                // A new stage before any hand is scored says so (PLAY_LAYOUT 5).
                W11Apply(W11Scene(W11Cards3,new[]{1,2,3,4,5},"bbbbb",0,false,stageIndex:6,target:"6000"));yield return W11Settle();
                Assert.That(FindActive<TextMeshProUGUI>("FormulaCaptionText11").text,Is.EqualTo("本关还没有得分"));
                Assert.That(HasActive("LockRule"),Is.False,"The lock note appears only when something is locked.");
                Assert.That(FindActive<TextMeshProUGUI>("ActionReason").text,Does.Contain("掷骰"));
            }
            W11TextScale(100);
        }

        // ------------------------------------------------------------------ helpers for the rewritten v0.7 assertions (W11_TEST_CHANGES)
        /// <summary>Of the given action names, those the action row shows (PLAY_LAYOUT 2: an unavailable action is not shown).</summary>
        private string[] W11ShownActions(params string[] names)=>names.Where(HasActive).ToArray();
        private static readonly string[] W11BadgeNames={"ColorBadge11_","GlassBadge11_","TempBadge11_"};
        private bool W11ColorAssist=>(bool)typeof(GameApp).GetField("colorAssist07",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(app);
        /// <summary>The shape badges a die shows, in their drawing order (colour, glass, temporary).</summary>
        private RectTransform[] W11Badges(string id)=>W11BadgeNames.Where(n=>HasActive(n+id)).Select(n=>FindActive<RectTransform>(n+id)).ToArray();
        /// <summary>The badges a die should show: its colour when thrown and colour assist is on (red circle, blue diamond),
        /// glass, stage-temporary (PLAY_LAYOUT 4, B4).</summary>
        private Play11Badge.Shape[] W11ExpectedBadges(string id)
        {
            var state=app.Session.State;var die=state.dice.Single(d=>d.dieId==id);var result=state.hand.currentResults.FirstOrDefault(r=>r.dieId==id);
            var shapes=new List<Play11Badge.Shape>();
            if(result?.hasRolled==true&&W11ColorAssist)shapes.Add(result.currentColor=="red"?Play11Badge.Shape.Circle:Play11Badge.Shape.Diamond);
            if(die.isGlass)shapes.Add(Play11Badge.Shape.Glass);
            if(die.lifetime=="stage_temporary")shapes.Add(Play11Badge.Shape.Clock);
            return shapes.ToArray();
        }
        /// <summary>Each badge of a die is drawn (a mesh, not culled), at least 12 design units, on screen, clear of every
        /// other die's projected body and of its own die's body core (the hull drawn in by 16 %, where the pips are).</summary>
        private void W11AssertBadgesClear(string id,string scope)
        {
            var others=app.Session.State.dice.Where(d=>d.dieId!=id).Select(d=>app.table.GetFaceOrientation(d.dieId)).Where(p=>p!=null&&p.gameObject.activeInHierarchy)
                .SelectMany(p=>p.GetComponentsInChildren<Renderer>(false)).Where(r=>r.enabled).Select(DiceLabelRendererRect07).ToArray();
            var core=W11DieHull(id,.16f);
            foreach(var badge in W11Badges(id))
            {
                var graphic=badge.GetComponent<Play11Badge>();Assert.That(graphic,Is.Not.Null,scope+" "+badge.name);
                Assert.That(graphic.canvasRenderer.cull,Is.False,scope+" "+badge.name);
                Assert.That(graphic.canvasRenderer.GetMesh()==null?0:graphic.canvasRenderer.GetMesh().vertexCount,Is.GreaterThan(0),scope+" "+badge.name+" has no mesh");
                Rect r=V06ScreenRect(badge);
                Assert.That(r.width,Is.GreaterThanOrEqualTo(12*app.canvas.scaleFactor-.01f),scope+" "+badge.name);
                Assert.That(ScoreContains07(Screen.safeArea,r),Is.True,scope+" "+badge.name+" leaves the screen");
                foreach(var body in others)Assert.That(r.Overlaps(body),Is.False,scope+" "+badge.name+" covers another die");
                for(int i=0;i<5;i++)for(int j=0;j<5;j++)
                    Assert.That(W11Inside(core,new Vector2(r.xMin+r.width*(i+.5f)/5,r.yMin+r.height*(j+.5f)/5)),Is.False,scope+" "+badge.name+" covers the body of its own die");
            }
        }
    }
}
