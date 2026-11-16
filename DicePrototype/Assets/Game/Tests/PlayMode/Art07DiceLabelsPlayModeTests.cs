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
            // V1.1 (W11, PLAY_LAYOUT 4 and 8, docs/specs/v11/W11_TEST_CHANGES.md): the pool is one row of up to ten dice that
            // share the tray width, and a die no longer has a text label beside it. Its colour (with colour assist), glass
            // and stage-temporary marks are shape badges at its top-right corner (in the slot's right column once locked);
            // a chosen die has an "已选 N" tag under it; nothing says "待投掷". The v0.7 intent stays: every die and every
            // mark is complete, on screen and clear of other dice, slot numbers, lock marks and actions, and a body is never
            // shrunk to hide a collision.
            Canvas.ForceUpdateCanvases();string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);
            var state=app.Session.State;var lockedIds=state.hand.orderedLockedIds;
            var pointers=app.canvas.GetComponentsInChildren<DiePointer>(false).Where(p=>p.name.StartsWith("Die_")).ToArray();
            var bodies=new Dictionary<string,List<Rect>>();
            bool paged=state.dice.Count(d=>!lockedIds.Contains(d.dieId))>10;
            int shown=pointers.Count(p=>!lockedIds.Contains(p.name.Substring(4)));
            float cellW=Mathf.Min(272,(1408-(paged?72:0))/(float)Mathf.Max(1,shown)),scale=Mathf.Min(1.75f,(cellW-30)/101f);
            foreach(var pointer in pointers)
            {
                string id=pointer.name.Substring(4);var orientation=app.table.GetFaceOrientation(id);Assert.That(orientation,Is.Not.Null);
                bodies[id]=orientation.GetComponentsInChildren<Renderer>(false).Where(r=>r.enabled).Select(DiceLabelRendererRect07).ToList();
                Assert.That(bodies[id].Count,Is.GreaterThan(0));
                bool locked=lockedIds.Contains(id);Rect hit=V06ScreenRect((RectTransform)pointer.transform);
                Assert.That(hit.width,Is.GreaterThanOrEqualTo(39.9f));Assert.That(hit.height,Is.GreaterThanOrEqualTo(39.9f));
                if(locked)
                {
                    int slot=lockedIds.IndexOf(id);Rect frame=V06ScreenRect(FindActive<RectTransform>("Slot"+slot));
                    foreach(var body in bodies[id])
                    {
                        Assert.That(body.xMin,Is.GreaterThanOrEqualTo(frame.xMin-.5f));Assert.That(body.xMax,Is.LessThanOrEqualTo(frame.xMax+.5f));
                        Assert.That(body.yMin,Is.GreaterThanOrEqualTo(frame.yMin-.5f));Assert.That(body.yMax,Is.LessThanOrEqualTo(frame.yMax+.5f));
                    }
                }
                else
                {
                    // One row (a page of up to ten): the cell is the tray width shared out, the body scale follows the cell.
                    Assert.That(hit.width,Is.EqualTo(Mathf.Min(cellW-6,scale*139+8)*app.canvas.scaleFactor).Within(.5));
                    Assert.That(hit.height,Is.EqualTo((scale*136+14)*app.canvas.scaleFactor).Within(.5));
                    Assert.That(orientation.lossyScale.x,Is.EqualTo(scale).Within(.001),"Do not shrink bodies to conceal a label collision.");
                }
            }
            var actions=new[]{"Roll","Confirm","Clear","Submit","EndStage"}.Where(HasActive).ToArray();
            Assert.That(actions,Does.Contain("EndStage"),"The end-stage button is always on the play screen.");
            Assert.That(app.canvas.GetComponentsInChildren<TextMeshProUGUI>(false).Any(t=>t.text.Contains("待投掷")),Is.False,"No die says 待投掷 (B4).");
            foreach(var pointer in pointers)
            {
                string id=pointer.name.Substring(4);bool locked=lockedIds.Contains(id);
                CollectionAssert.AreEqual(W11ExpectedBadges(id),W11Badges(id).Select(b=>b.GetComponent<Play11Badge>().Kind).ToArray(),id+": colour, glass and temporary marks as shapes");
                W11AssertBadgesClear(id,"dice-labels");
                var marks=W11Badges(id).Select(V06ScreenRect).ToList();
                if(app.Session.PendingIds.Contains(id))
                {
                    var label=FindActive<TextMeshProUGUI>("Pending"+id);
                    Assert.That(label.text,Is.EqualTo("已选 "+(System.Array.IndexOf(app.Session.PendingIds,id)+1)));
                    Assert.That(label.fontSize,Is.GreaterThanOrEqualTo(19.49f),"AX-03 body size");
                    var glyphs=DiceLabelVisibleGlyphRects07(label);Assert.That(glyphs.Count,Is.GreaterThan(0),label.name+" must render glyphs, not only own a rect");
                    Assert.That(label.isTextOverflowing,Is.False,label.name);Assert.That(label.isTextTruncated,Is.False,label.name);
                    Rect textRect=V06ScreenRect(label.rectTransform);
                    foreach(var glyph in glyphs)
                    {
                        Assert.That(glyph.xMin,Is.GreaterThanOrEqualTo(textRect.xMin-.75f),label.name);Assert.That(glyph.xMax,Is.LessThanOrEqualTo(textRect.xMax+.75f),label.name);
                        Assert.That(glyph.yMin,Is.GreaterThanOrEqualTo(textRect.yMin-.75f),label.name);Assert.That(glyph.yMax,Is.LessThanOrEqualTo(textRect.yMax+.75f),label.name);
                    }
                    foreach(var body in bodies)foreach(var face in body.Value)Assert.That(textRect.Overlaps(face),Is.False,label.name+" covers projected physical face of "+body.Key);
                    marks.Add(textRect);
                }
                foreach(var mark in marks)
                {
                    foreach(var other in pointers)if(other!=pointer)Assert.That(mark.Overlaps(V06ScreenRect((RectTransform)other.transform)),Is.False,id+" mark covers another die's click target "+other.name);
                    foreach(string action in actions)Assert.That(mark.Overlaps(V06ScreenRect((RectTransform)Button(action).transform)),Is.False,id+" mark covers "+action);
                    Assert.That(ScoreContains07(Screen.safeArea,mark),Is.True,id+" mark leaves the safe area");
                    if(locked)
                    {
                        int slot=lockedIds.IndexOf(id);
                        foreach(var ordinal in DiceLabelVisibleGlyphRects07(FindActive<TextMeshProUGUI>("SlotIndex"+slot)))Assert.That(mark.Overlaps(ordinal),Is.False,id+" mark covers its slot ordinal");
                        Assert.That(mark.Overlaps(V06ScreenRect(FindActive<RectTransform>("LockMark"+slot))),Is.False,id+" mark covers its lock mark");
                        Assert.That(ScoreContains07(V06ScreenRect(FindActive<RectTransform>("Slot"+slot)),mark),Is.True,id+" mark leaves its slot");
                    }
                }
            }
            foreach(var title in app.canvas.GetComponentsInChildren<TextMeshProUGUI>(false).Where(t=>t.name=="PoolTitle"||t.name.StartsWith("SlotIndex")))
                foreach(var glyph in DiceLabelVisibleGlyphRects07(title))foreach(var body in bodies)foreach(var face in body.Value)
                    Assert.That(glyph.Overlaps(face),Is.False,title.name+" glyph covers projected physical face of "+body.Key);
            // The v0.7 two-row pool had a gap between its rows; the one-row pool shares a centre line and no two hits overlap.
            var pool=pointers.Where(p=>!lockedIds.Contains(p.name.Substring(4))).Select(p=>V06ScreenRect((RectTransform)p.transform)).ToArray();
            for(int i=0;i<pool.Length;i++)for(int j=i+1;j<pool.Length;j++)
            {
                Assert.That(pool[i].center.y,Is.EqualTo(pool[j].center.y).Within(.5f),"One row of dice");
                Assert.That(pool[i].Overlaps(pool[j]),Is.False,"Two dice share a click target");
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
