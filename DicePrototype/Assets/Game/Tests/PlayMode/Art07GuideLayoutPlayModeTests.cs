using System.Collections;
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
    public sealed partial class GameplayPlayModeTests
    {
        // Run this same test at actual 1920x1080 and 1280x720 Game View sizes.
        // All 100%, 125%, and 150% settings are exercised; Screen.safeArea decides the
        // production compact branch. No fake viewport or rule change is used.
        [UnityTest]
        public IEnumerator PLAY_ART07_GuideLayoutAllFourActionsAvoidActualDiceAndHudAndPointAtLiveTargets()
        {
            foreach(int font in new[]{100,125,150})
            {
                PlayerPrefs.SetInt("art07.textScale",font);PlayerPrefs.SetInt("art07.guideSeen",0);
                yield return LoadBootstrap();StartRun("70783");Click("StartStage");
                for(int step=0;step<4;step++)
                {
                    yield return new WaitForSecondsRealtime(.25f);
                    Assert.That(FindActive<TextMeshProUGUI>("GuideTitle").text,Does.StartWith((step+1)+" / 5"));
                    GuideCheckLayout07(step==0?"Roll":step==1?"Die_D001":step==2?"Confirm":"Submit");
                    var motion=FindActive<Art07PanelMotion>("FirstRunGuide");
                    Assert.That(motion.Duration,Is.EqualTo(.12f).Within(.001));Assert.That(motion.ExitDuration,Is.EqualTo(.08f).Within(.001));
                    int sequence=motion.EnterSequence;string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);
                    app.Refresh();yield return null;
                    Assert.That(motion.EnterSequence,Is.EqualTo(sequence),"A redraw must not replay the entrance.");
                    Assert.That(motion.Alpha,Is.EqualTo(1).Within(.001));
                    Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                    yield return CaptureEdge07("S19-guide-repaired-"+(step+1)+"-font"+font);
                    if(step==0){Click("Roll");app.SkipVisual();}
                    else if(step==1)ClickDie("D001");
                    else if(step==2){Click("Confirm");yield return WaitForIdle();}
                }
                string closeState=StateJson();byte[] closeDisk=File.ReadAllBytes(app.SaveLocation);
                Click("DismissGuide");Assert.That(HasActive("FirstRunGuide"),Is.False);Assert.That(HasActive("GuideArrow"),Is.False);
                Assert.That(Button("Submit").interactable,Is.True);Assert.That(PlayerPrefs.GetInt("art07.guideSeen"),Is.EqualTo(1));
                var tail=FindActive<Art07PanelMotion>("GuideFadeTail07");
                Assert.That(tail.Duration,Is.EqualTo(.08f).Within(.001));Assert.That(tail.IsExitTail,Is.True);
                Assert.That(tail.GetComponent<CanvasGroup>().blocksRaycasts,Is.False);Assert.That(tail.GetComponentsInChildren<Selectable>(true),Is.Empty);
                yield return new WaitForSecondsRealtime(.12f);Assert.That(HasActive("GuideFadeTail07"),Is.False);
                Assert.That(StateJson(),Is.EqualTo(closeState));CollectionAssert.AreEqual(closeDisk,File.ReadAllBytes(app.SaveLocation));
            }
        }

        [UnityTest]
        public IEnumerator PLAY_ART07_GuideLayoutShopPaperLeavesPricesAndTabsVisibleAtBothFontScales()
        {
            foreach(int font in new[]{100,125,150})
            {
                PlayerPrefs.SetInt("art07.textScale",font);PlayerPrefs.SetInt("art07.guideSeen",0);
                var fixture=NewBoundaryFixture("art07-guide-layout-shop-"+font);fixture.isDebug=false;fixture.debugWorkspace=false;
                foreach(var die in fixture.dice)foreach(var face in die.faces){face.permanentPip=6;face.baseColor="blue";}
                new AtomicSaveStore(app.SaveLocation,app.Config,SaveSlotPolicy.Normal).Save(fixture);
                yield return LoadBootstrap();Click("ContinueRun");Click("StartStage");Click("Roll");app.SkipVisual();
                foreach(var die in app.Session.State.dice)ClickDie(die.dieId);
                Click("Confirm");yield return WaitForIdle();Click("Submit");app.SkipVisual();Assert.That(app.Session.CanEndStage,Is.True);
                Click("EndStage");Click("SettlementContinue");yield return new WaitForSecondsRealtime(.25f);
                Assert.That(app.Session.State.phase,Is.EqualTo("Shop"));Assert.That(FindActive<TextMeshProUGUI>("GuideTitle").text,Does.StartWith("5 / 5"));
                string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);GuideCheckLayout07("Tab4");
                yield return CaptureEdge07("S19-guide-repaired-shop-font"+font);
                Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            }
        }

        [UnityTest]
        public IEnumerator PLAY_ART07_GuideLayoutManualReopenAtMaximumCapacityKeepsCompleteInlineHelpAndClose()
        {
            foreach(int font in new[]{100,125,150})
            {
                PlayerPrefs.SetInt("art07.textScale",font);PlayerPrefs.SetInt("art07.guideSeen",1);
                var fixture=NewBoundaryFixture("art07-guide-max-"+font);fixture.isDebug=false;fixture.debugWorkspace=false;fixture.consumableSlots=5;
                while(fixture.dice.Count<8)fixture.dice.Add(DiceFactory.CreateStandard(app.Config,fixture.dice.Count+1));
                fixture.nextDieSerial=9;
                string[] ids={"C01","C02","C03","C06","C21","C22","C27","C28","C34","C35"};
                for(int i=0;i<ids.Length;i++)fixture.controllers.Add(BoundaryController(fixture,ids[i],i<4?"none":"slotless"));
                string first=BoundaryItem(fixture,"I02"),second=BoundaryItem(fixture,"I02");
                BoundaryItem(fixture,"I01");BoundaryItem(fixture,"I04");BoundaryItem(fixture,"I05");
                new AtomicSaveStore(app.SaveLocation,app.Config,SaveSlotPolicy.Normal).Save(fixture);
                yield return LoadBootstrap();Click("ContinueRun");Click("StartStage");
                Assert.That(app.Send(new SessionCommand{kind=CommandKind.UseItem,itemInstanceId=first}).accepted,Is.True);
                Assert.That(app.Send(new SessionCommand{kind=CommandKind.UseItem,itemInstanceId=second}).accepted,Is.True);
                Assert.That(app.Session.State.dice.Count,Is.EqualTo(10));Assert.That(app.Session.State.controllers.Count,Is.EqualTo(10));
                string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);
                var originalPositions=app.canvas.GetComponentsInChildren<RectTransform>(false).Where(r=>r.name.StartsWith("Controller_")||r.name.StartsWith("Item_")||r.name.StartsWith("ItemEmpty")||r.name=="ItemsTitle").ToDictionary(r=>r.name,r=>r.anchoredPosition);
                Click("Handbook");Click("ReviewGuide");yield return new WaitForSecondsRealtime(.25f);
                Assert.That(FindActive<TextMeshProUGUI>("GuideBody").text,Is.EqualTo("每手总投掷包含首掷。点击掷骰开始。"));
                GuideCheckLayout07("Roll");
                var liveCards=app.Session.State.controllers.Select(item=>(RectTransform)Button("Controller_"+item.instanceId).transform).ToArray();
                for(int i=0;i<liveCards.Length;i++)
                {
                    var item=app.Session.State.controllers[i];var cardRead=CardCheck07(liveCards[i],item,"hud-compact",CardExpectedFooter07(item,false));
                    Assert.That(cardRead.errors,Is.Empty,"The yielded card keeps all actual glyphs, badges and icon clear: "+item.instanceId+" / "+string.Join("; ",cardRead.errors));
                    for(int j=i+1;j<liveCards.Length;j++)Assert.That(GuideCheckScreenRect07(liveCards[i]).Overlaps(GuideCheckScreenRect07(liveCards[j])),Is.False,"Yielded card hit areas overlap.");
                }
                typeof(GameApp).GetProperty("notice",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(app,"存档已读取");
                app.Refresh();yield return null;
                Assert.That(HasActive("Toast"),Is.False,"A later status toast cannot paint over reopened full-paper guidance.");
                yield return CaptureEdge07("S19-guide-repaired-full-paper-font"+font);
                Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                Click("DismissGuide");Assert.That(HasActive("FirstRunGuide"),Is.False);Assert.That(Button("Roll").interactable,Is.True);
                yield return new WaitForSecondsRealtime(.14f);
                foreach(var original in originalPositions)
                {
                    var restored=app.canvas.GetComponentsInChildren<RectTransform>(false).Single(r=>r.name==original.Key);
                    Assert.That(restored.anchoredPosition,Is.EqualTo(original.Value),"Closing restores the original card/item layout: "+original.Key);
                }
                Assert.That(FindActive<RectTransform>("Stats07").Find("BrandPaper/Brand").gameObject.activeInHierarchy,Is.True);
                Assert.That(FindActive<RectTransform>("Stats07").Find("Edition").gameObject.activeSelf,Is.True);
                Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            }
        }

        private void GuideCheckLayout07(string targetName)
        {
            Canvas.ForceUpdateCanvases();var paper=FindActive<RectTransform>("FirstRunGuide");Rect card=GuideCheckScreenRect07(paper);
            float scale=app.canvas.scaleFactor;
            Assert.That(card.width,Is.EqualTo(400*scale).Within(1));Assert.That(card.height,Is.EqualTo(144*scale).Within(1));
            Rect physicalPaper=GuidePhysicalPaperBounds071(paper);
            Assert.That(card.xMin,Is.GreaterThanOrEqualTo(Screen.safeArea.xMin));Assert.That(card.xMax,Is.LessThanOrEqualTo(Screen.safeArea.xMax));
            Assert.That(card.yMin,Is.GreaterThanOrEqualTo(Screen.safeArea.yMin));Assert.That(card.yMax,Is.LessThanOrEqualTo(Screen.safeArea.yMax));
            Rect close=GuideCheckScreenRect07((RectTransform)Button("DismissGuide").transform);
            Assert.That(close.width,Is.GreaterThanOrEqualTo(39.9f));Assert.That(close.height,Is.GreaterThanOrEqualTo(39.9f));
            foreach(string name in new[]{"GuideTitle","GuideBody"})
            {
                var text=FindActive<TextMeshProUGUI>(name);text.ForceMeshUpdate();Rect bounds=GuideCheckScreenRect07(text.rectTransform);
                Assert.That(bounds.Overlaps(close),Is.False,name+" text rectangle overlaps the close target");
                Assert.That(text.isTextOverflowing,Is.False,name+" must retain its full copy at the selected font size");
                Assert.That(text.isTextTruncated,Is.False,name);
                foreach(var character in text.textInfo.characterInfo.Take(text.textInfo.characterCount))
                {
                    if(char.IsWhiteSpace(character.character))continue;
                    Assert.That(character.isVisible,Is.True,name+" missing glyph "+character.character);
                    var mesh=text.textInfo.meshInfo[character.materialReferenceIndex];
                    for(int i=0;i<4;i++)
                    {
                        Vector2 point=RectTransformUtility.WorldToScreenPoint(null,text.transform.TransformPoint(mesh.vertices[character.vertexIndex+i]));
                        Assert.That(point.x,Is.InRange(physicalPaper.xMin-.5f,physicalPaper.xMax+.5f),name+" glyph escapes the actual sliced paper horizontally");
                        Assert.That(point.y,Is.InRange(physicalPaper.yMin-.5f,physicalPaper.yMax+.5f),name+" glyph escapes the actual sliced paper vertically");
                    }
                }
            }
            var root=paper.parent;
            foreach(var button in root.GetComponentsInChildren<Button>(false))
                if(!button.transform.IsChildOf(paper))Assert.That(card.Overlaps(GuideCheckScreenRect07((RectTransform)button.transform)),Is.False,"Paper covers control "+button.name);
            foreach(var text in root.GetComponentsInChildren<TextMeshProUGUI>(false))
                if(!text.transform.IsChildOf(paper)&&!string.IsNullOrWhiteSpace(text.text))Assert.That(card.Overlaps(GuideCheckScreenRect07(text.rectTransform)),Is.False,"Paper covers HUD "+text.name);
            foreach(var die in app.Session.State.dice)
            {
                var orientation=app.table.GetFaceOrientation(die.dieId);if(orientation==null||!orientation.gameObject.activeInHierarchy)continue;
                foreach(var renderer in orientation.GetComponentsInChildren<Renderer>(false))
                    if(renderer.enabled)Assert.That(card.Overlaps(GuideCheckRendererRect07(renderer)),Is.False,"Paper covers projected die "+die.dieId);
            }
            var arrow=FindActive<RectTransform>("GuideArrow");var marker=FindActive<RectTransform>("GuideArrowTip");
            Vector2 tip=RectTransformUtility.WorldToScreenPoint(null,marker.position);
            var target=root.GetComponentsInChildren<RectTransform>(false).Single(t=>t.name==targetName);
            Rect targetRect=GuideCheckScreenRect07(target);
            Vector2 nearest=new Vector2(Mathf.Clamp(tip.x,targetRect.xMin,targetRect.xMax),Mathf.Clamp(tip.y,targetRect.yMin,targetRect.yMax));
            Assert.That(targetRect.Contains(tip),Is.False,"The arrow must stop outside the target/pips.");
            Assert.That(Vector2.Distance(tip,nearest),Is.EqualTo(4*scale).Within(1.5f),"Arrow must point at the actual live target edge.");
            Assert.That(arrow.GetComponentsInChildren<Graphic>(false).All(g=>!g.raycastTarget),Is.True);
            Rect arrowRect=GuideCheckScreenRect07(arrow);
            foreach(var die in app.Session.State.dice)
            {
                var orientation=app.table.GetFaceOrientation(die.dieId);if(orientation==null||!orientation.gameObject.activeInHierarchy)continue;
                foreach(var renderer in orientation.GetComponentsInChildren<Renderer>(false))if(renderer.enabled)
                    Assert.That(arrowRect.Overlaps(GuideCheckRendererRect07(renderer)),Is.False,"Guide arrow covers projected die "+die.dieId);
            }
        }
        private Rect GuidePhysicalPaperBounds071(RectTransform paper)
        {
            // v0.71 moved the paper from an opaque Fill child onto the root Image.
            // Its rounded 512px master leaves real transparent/shadow padding:
            // left 4, bottom 17, right 14, top 4px. Nine-slicing retains these
            // insets at the Image's rendered PPU; the full 400x144 hit rect is
            // not available paper for glyphs. Keep all old size/overlap checks.
            var image=paper.GetComponent<Image>();
            Assert.That(image,Is.Not.Null,"Guide must expose its actual paper Image.");
            Assert.That(image.sprite,Is.SameAs(UiKit.Template("ui_paper_panel")));
            Assert.That(image.sprite,Is.Not.Null);Assert.That(image.type,Is.EqualTo(Image.Type.Sliced));
            Assert.That(image.fillCenter,Is.True,"The guide must retain its opaque paper center.");
            Assert.That(image.color,Is.EqualTo(Color.white),"The authored paper texture must not be tinted or made translucent.");
            float alpha=image.color.a*image.canvasRenderer.GetAlpha();
            foreach(var group in image.GetComponentsInParent<CanvasGroup>(false))alpha*=group.alpha;
            Assert.That(alpha,Is.GreaterThan(.99f));Assert.That(image.canvasRenderer.cull,Is.False);
            float ppu=image.pixelsPerUnit*image.pixelsPerUnitMultiplier;
            Rect local=image.GetPixelAdjustedRect();
            local=Rect.MinMaxRect(local.xMin+4/ppu,local.yMin+17/ppu,local.xMax-14/ppu,local.yMax-4/ppu);
            Vector2 low=RectTransformUtility.WorldToScreenPoint(null,image.rectTransform.TransformPoint(new Vector3(local.xMin,local.yMin,0)));
            Vector2 high=RectTransformUtility.WorldToScreenPoint(null,image.rectTransform.TransformPoint(new Vector3(local.xMax,local.yMax,0)));
            return Rect.MinMaxRect(low.x,low.y,high.x,high.y);
        }
        private static Rect GuideCheckScreenRect07(RectTransform transform)
        {
            var corners=new Vector3[4];transform.GetWorldCorners(corners);
            Vector2 min=new Vector2(float.MaxValue,float.MaxValue),max=new Vector2(float.MinValue,float.MinValue);
            foreach(var corner in corners){var p=RectTransformUtility.WorldToScreenPoint(null,corner);min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        private Rect GuideCheckRendererRect07(Renderer renderer)
        {
            Bounds bounds=renderer.bounds;Vector2 min=new Vector2(float.MaxValue,float.MaxValue),max=new Vector2(float.MinValue,float.MinValue);
            for(int i=0;i<8;i++)
            {
                var p=app.table.viewCamera.WorldToScreenPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                min=Vector2.Min(min,p);max=Vector2.Max(max,p);
            }
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
    }
}
