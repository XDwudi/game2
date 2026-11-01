using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Verification;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [UnityTest]
        public IEnumerator PLAY_ART07_DiceLabelsTenPoolUnrolledRolledPendingKeepEveryPhysicalFaceClear()
        {
            foreach(int font in new[]{100,125,150})
            {
                yield return DiceLabelBegin07(font);
                foreach(bool rolled in new[]{false,true})
                {
                    DiceLabelApply07(10,rolled);yield return new WaitForSecondsRealtime(.25f);
                    DiceLabelCheck07();yield return CaptureEdge07("dice-labels-ten-"+(rolled?"rolled":"unrolled")+"-font"+font);
                    if(!rolled)continue;
                    foreach(var die in app.Session.State.dice.Take(8))ClickDie(die.dieId);
                    yield return new WaitForSecondsRealtime(.25f);
                    Assert.That(app.Session.PendingIds.Length,Is.EqualTo(8));DiceLabelCheck07();
                    yield return CaptureEdge07("dice-labels-ten-pending-font"+font);
                }
            }
        }

        [UnityTest]
        public IEnumerator PLAY_ART07_DiceLabelsSingleRowAndEightLockedSlotsPreserveFullAttributesAndIdentity()
        {
            foreach(int font in new[]{100,125,150})
            {
                yield return DiceLabelBegin07(font);DiceLabelApply07(10,true);
                foreach(var die in app.Session.State.dice.Take(5))ClickDie(die.dieId);Click("Confirm");yield return WaitForIdle();
                yield return new WaitForSecondsRealtime(.25f);DiceLabelCheck07();
                Assert.That(app.Session.State.hand.orderedLockedIds.Count,Is.EqualTo(5));
                yield return CaptureEdge07("dice-labels-five-pool-five-locked-font"+font);
                foreach(var die in app.Session.State.dice.Where(d=>!app.Session.State.hand.orderedLockedIds.Contains(d.dieId)).Take(3))ClickDie(die.dieId);
                Click("Confirm");yield return WaitForIdle();yield return new WaitForSecondsRealtime(.25f);DiceLabelCheck07();
                Assert.That(app.Session.State.hand.orderedLockedIds.Count,Is.EqualTo(8));
                Assert.That(app.canvas.GetComponentsInChildren<TextMeshProUGUI>(false).Any(t=>t.name.StartsWith("PendingD")),Is.False,"Pending ordinals must disappear on actual lock confirmation.");
                yield return CaptureEdge07("dice-labels-eight-locked-font"+font);
            }
        }

        [UnityTest]
        public IEnumerator PLAY_ART07_DiceLabelsDebugThirtyTwoPagesNeverCoverDiceSlotsOrSidebarControls()
        {
            foreach(int font in new[]{100,125,150})
            {
                yield return DiceLabelBegin07(font);DiceLabelApply07(32,true);
                string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);
                var seen=new HashSet<string>();
                for(int page=0;page<4;page++)
                {
                    yield return new WaitForSecondsRealtime(.25f);DiceLabelCheck07();DiceLabelPagerCheck07("PoolPage");DiceLabelPagerCheck07("SlotPage");
                    foreach(var die in app.canvas.GetComponentsInChildren<DiePointer>(false).Where(p=>p.name.StartsWith("Die_")))seen.Add(die.name);
                    Assert.That(FindActive<TextMeshProUGUI>("PoolPageIndex").text,Is.EqualTo((page+1)+" / 4"));
                    Assert.That(FindActive<TextMeshProUGUI>("SlotPageIndex").text,Is.EqualTo((page+1)+" / 4"));
                    yield return CaptureEdge07("dice-labels-debug-page"+(page+1)+"-font"+font);
                    Click("PoolPage");Click("SlotPage");
                }
                Assert.That(seen.Count,Is.EqualTo(32));Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            }
        }

        private IEnumerator DiceLabelBegin07(int font)
        {
            if(!intPreferences.ContainsKey("art07.colorAssist"))intPreferences["art07.colorAssist"]=PlayerPrefs.HasKey("art07.colorAssist")?(int?)PlayerPrefs.GetInt("art07.colorAssist"):null;
            PlayerPrefs.SetInt("art07.colorAssist",1);PlayerPrefs.SetInt("art07.textScale",font);PlayerPrefs.SetInt("art07.guideSeen",1);
            yield return LoadBootstrap();StartRun("70807");Assert.That(app.UnlockDebug("XD is god"),Is.True);Assert.That(app.EnterDebugWorkspace(),Is.True);Click("CloseDialog");
        }
        private void DiceLabelApply07(int count,bool rolled)
        {
            var state=app.Session.State;var scene=Art07Fixtures.Create(app.Config,count==32?Art07FixtureKind.Debug32:Art07FixtureKind.MaximumPool,state.nextItemSerial,state.nextDieSerial);
            scene.sourceRunId=state.sourceRunId;scene.orderedLockedIds.Clear();
            if(rolled)foreach(var result in scene.currentResults)result.isLocked=false;else scene.currentResults=null;
            // Include glass+temporary together and both colors, with no RNG draw
            // needed to produce this isolated visual-test checkpoint.
            scene.dice[4].isGlass=true;scene.dice[4].lifetime="stage_temporary";scene.dice[4].createdStageId=app.Config.stages[0].stageId;
            var applied=app.Send(new SessionCommand{kind=CommandKind.ApplyDebugScene,debugScene=scene});Assert.That(applied.accepted,Is.True,applied.error);
            if(HasActive("RunDebugScene"))Click("RunDebugScene");if(HasActive("CloseDialog"))Click("CloseDialog");
        }
        private void DiceLabelCheck07()
        {
            Canvas.ForceUpdateCanvases();string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);
            bool compact=Screen.safeArea.width<=1366&&UiKit.FontScale>=1.25f;
            var pointers=app.canvas.GetComponentsInChildren<DiePointer>(false).Where(p=>p.name.StartsWith("Die_")).ToArray();
            var bodies=new Dictionary<string,List<Rect>>();
            foreach(var pointer in pointers)
            {
                string id=pointer.name.Substring(4);var orientation=app.table.GetFaceOrientation(id);Assert.That(orientation,Is.Not.Null);
                bodies[id]=orientation.GetComponentsInChildren<Renderer>(false).Where(r=>r.enabled).Select(DiceLabelRendererRect07).ToList();
                Assert.That(bodies[id].Count,Is.GreaterThan(0));
                bool locked=app.Session.State.hand.orderedLockedIds.Contains(id);Rect hit=V06ScreenRect((RectTransform)pointer.transform);
                Assert.That(hit.width,Is.GreaterThanOrEqualTo(39.9f));Assert.That(hit.height,Is.GreaterThanOrEqualTo(39.9f));
                if(locked)
                {
                    int slot=app.Session.State.hand.orderedLockedIds.IndexOf(id);Rect frame=V06ScreenRect(FindActive<RectTransform>("Slot"+slot));
                    foreach(var body in bodies[id])
                    {
                        Assert.That(body.xMin,Is.GreaterThanOrEqualTo(frame.xMin-.5f));Assert.That(body.xMax,Is.LessThanOrEqualTo(frame.xMax+.5f));
                        Assert.That(body.yMin,Is.GreaterThanOrEqualTo(frame.yMin-.5f));Assert.That(body.yMax,Is.LessThanOrEqualTo(frame.yMax+.5f));
                    }
                }
                if(!locked)
                {
                    Assert.That(hit.width,Is.EqualTo((compact?180:126)*app.canvas.scaleFactor).Within(.5));
                    Assert.That(hit.height,Is.EqualTo((compact?90:112)*app.canvas.scaleFactor).Within(.5));
                    int shown=pointers.Count(p=>!app.Session.State.hand.orderedLockedIds.Contains(p.name.Substring(4)));
                    float expected=compact?.72f:shown>5?1.04f:1.18f;
                    Assert.That(orientation.lossyScale.x,Is.EqualTo(expected).Within(.001),"Do not shrink bodies to conceal a label collision.");
                }
            }
            foreach(var pointer in pointers)
            {
                string id=pointer.name.Substring(4);var die=app.Session.State.dice.Single(d=>d.dieId==id);bool locked=app.Session.State.hand.orderedLockedIds.Contains(id);
                var state=FindActive<TextMeshProUGUI>("DieState"+id);state.ForceMeshUpdate();
                Assert.That(state.fontSize,Is.EqualTo((locked?14:compact?14:17)*UiKit.FontScale).Within(.01));
                var result=app.Session.State.hand.currentResults.FirstOrDefault(r=>r.dieId==id);
                Assert.That(state.text,Does.Contain(result?.hasRolled==true?(result.currentColor=="red"?"● 红":"◇ 蓝"):"待投掷"));
                if(die.isGlass)Assert.That(state.text,Does.Contain("玻璃"));if(die.lifetime=="stage_temporary")Assert.That(state.text,Does.Contain("临时"));
                var labels=new List<TextMeshProUGUI>{state};
                if(app.Session.PendingIds.Contains(id))labels.Add(FindActive<TextMeshProUGUI>("Pending"+id));
                foreach(var label in labels)
                {
                    var glyphs=DiceLabelVisibleGlyphRects07(label);Assert.That(glyphs.Count,Is.GreaterThan(0),label.name+" must render glyphs, not only own a rect");
                    Assert.That(label.isTextOverflowing,Is.False,label.name);Assert.That(label.isTextTruncated,Is.False,label.name);
                    Rect textRect=V06ScreenRect(label.rectTransform);
                    foreach(var glyph in glyphs)
                    {
                        Assert.That(glyph.xMin,Is.GreaterThanOrEqualTo(textRect.xMin-.75f),label.name);Assert.That(glyph.xMax,Is.LessThanOrEqualTo(textRect.xMax+.75f),label.name);
                        Assert.That(glyph.yMin,Is.GreaterThanOrEqualTo(textRect.yMin-.75f),label.name);Assert.That(glyph.yMax,Is.LessThanOrEqualTo(textRect.yMax+.75f),label.name);
                    }
                    if(locked)
                    {
                        int slot=app.Session.State.hand.orderedLockedIds.IndexOf(id);
                        var number=DiceLabelVisibleGlyphRects07(FindActive<TextMeshProUGUI>("SlotIndex"+slot));
                        foreach(var glyph in glyphs)foreach(var ordinal in number)Assert.That(glyph.Overlaps(ordinal),Is.False,label.name+" covers its slot ordinal");
                        Rect mark=V06ScreenRect(FindActive<RectTransform>("LockMark"+slot));
                        foreach(var glyph in glyphs)Assert.That(glyph.Overlaps(mark),Is.False,label.name+" covers its lock mark");
                    }
                    foreach(var body in bodies)foreach(var face in body.Value)Assert.That(textRect.Overlaps(face),Is.False,label.name+" covers projected physical face of "+body.Key);
                    foreach(var other in pointers)if(other!=pointer)Assert.That(textRect.Overlaps(V06ScreenRect((RectTransform)other.transform)),Is.False,label.name+" covers another die's click target");
                    foreach(string action in new[]{"Roll","Confirm","Clear","Submit","EndStage"})if(HasActive(action))Assert.That(textRect.Overlaps(V06ScreenRect((RectTransform)Button(action).transform)),Is.False,label.name+" covers "+action);
                    Assert.That(textRect.xMin,Is.GreaterThanOrEqualTo(Screen.safeArea.xMin));Assert.That(textRect.xMax,Is.LessThanOrEqualTo(Screen.safeArea.xMax));
                    Assert.That(textRect.yMin,Is.GreaterThanOrEqualTo(Screen.safeArea.yMin));Assert.That(textRect.yMax,Is.LessThanOrEqualTo(Screen.safeArea.yMax));
                }
            }
            foreach(var title in app.canvas.GetComponentsInChildren<TextMeshProUGUI>(false).Where(t=>t.name=="PoolTitle"||t.name.StartsWith("SlotIndex")))
                foreach(var glyph in DiceLabelVisibleGlyphRects07(title))foreach(var body in bodies)foreach(var face in body.Value)
                    Assert.That(glyph.Overlaps(face),Is.False,title.name+" glyph covers projected physical face of "+body.Key);
            var pool=pointers.Where(p=>!app.Session.State.hand.orderedLockedIds.Contains(p.name.Substring(4))).OrderBy(p=>app.Session.State.dice.Single(d=>d.dieId==p.name.Substring(4)).createdSerial).ToArray();
            if(pool.Length>5)
            {
                Rect first=V06ScreenRect((RectTransform)pool[0].transform),second=V06ScreenRect((RectTransform)pool[5].transform);
                Assert.That(first.yMin-second.yMax,Is.GreaterThanOrEqualTo((compact?6:8)*app.canvas.scaleFactor-.2f));
            }
            Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
        }
        private void DiceLabelPagerCheck07(string id)
        {
            var button=Button(id);Rect rect=V06ScreenRect((RectTransform)button.transform);
            Assert.That(rect.width,Is.GreaterThanOrEqualTo(39.9));Assert.That(rect.height,Is.GreaterThanOrEqualTo(39.9));
            foreach(var pointer in app.canvas.GetComponentsInChildren<DiePointer>(false).Where(p=>p.name.StartsWith("Die_")))Assert.That(rect.Overlaps(V06ScreenRect((RectTransform)pointer.transform)),Is.False,id+" covers "+pointer.name);
            foreach(var other in app.canvas.GetComponentsInChildren<Button>(false))if(other!=button)Assert.That(rect.Overlaps(V06ScreenRect((RectTransform)other.transform)),Is.False,id+" covers control "+other.name);
            foreach(var text in app.canvas.GetComponentsInChildren<TextMeshProUGUI>(false))
                if(!text.transform.IsChildOf(button.transform)&&!string.IsNullOrWhiteSpace(text.text))Assert.That(rect.Overlaps(V06ScreenRect(text.rectTransform)),Is.False,id+" covers text "+text.name);
        }
        private List<Rect> DiceLabelVisibleGlyphRects07(TextMeshProUGUI label)
        {
            label.ForceMeshUpdate();Assert.That(label.color.a,Is.GreaterThan(.01f),label.name);Assert.That(label.canvasRenderer.cull,Is.False,label.name);
            var result=new List<Rect>();int expected=label.text.Count(ch=>!char.IsWhiteSpace(ch));
            for(int i=0;i<label.textInfo.characterCount;i++)
            {
                var glyph=label.textInfo.characterInfo[i];if(char.IsWhiteSpace(glyph.character))continue;
                Assert.That(glyph.isVisible,Is.True,label.name+" missing visible glyph "+glyph.character);
                Assert.That(glyph.textElement,Is.Not.Null,label.name+" missing font glyph "+glyph.character);
                var vertices=label.textInfo.meshInfo[glyph.materialReferenceIndex].colors32;
                for(int v=0;v<4;v++)Assert.That(vertices[glyph.vertexIndex+v].a,Is.GreaterThan(0),label.name+" has transparent glyph "+glyph.character);
                Vector3 bl=label.rectTransform.TransformPoint(glyph.bottomLeft),tr=label.rectTransform.TransformPoint(glyph.topRight);
                Rect rect=Rect.MinMaxRect(Mathf.Min(bl.x,tr.x),Mathf.Min(bl.y,tr.y),Mathf.Max(bl.x,tr.x),Mathf.Max(bl.y,tr.y));
                Assert.That(rect.width,Is.GreaterThan(0));Assert.That(rect.height,Is.GreaterThan(0));result.Add(rect);
            }
            Assert.That(result.Count,Is.EqualTo(expected),label.name+" must retain every non-space character at the active font scale");return result;
        }
        private Rect DiceLabelRendererRect07(Renderer renderer)
        {
            Bounds bounds=renderer.bounds;Vector2 min=new Vector2(float.MaxValue,float.MaxValue),max=new Vector2(float.MinValue,float.MinValue);
            for(int i=0;i<8;i++)
            {
                var point=app.table.viewCamera.WorldToScreenPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                min=Vector2.Min(min,point);max=Vector2.Max(max,point);
            }
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
    }
}
