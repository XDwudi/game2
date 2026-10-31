using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [UnityTest]
        public IEnumerator PLAY_V06_RemoteThirtyTwoTargetsScrollAndKeepExactlyTwoWithoutOverlap()
        {
            var fixture=NewBoundaryFixture("remote-thirty-two");
            while(fixture.dice.Count<32)fixture.dice.Add(DiceFactory.CreateStandard(app.Config,fixture.nextDieSerial++));
            while(fixture.controllers.Count<32)fixture.controllers.Add(BoundaryController(fixture,"C02"));
            string itemId=BoundaryItem(fixture,"I03");
            yield return LoadBoundaryFixture(fixture);ResumeBoundaryFixture();Click("StartStage");
            string title=FindActive<TMPro.TextMeshProUGUI>("ControllersTitle").text;
            Assert.That(title,Does.Contain("调试配置"));Assert.That(title,Does.Contain("持有 32"));Assert.That(title,Does.Not.Contain("/"));
            string original=StateJson();byte[] originalDisk=File.ReadAllBytes(app.SaveLocation);
            var before=app.Session.State;
            OpenRemoteItem();
            Assert.That(Button("RemoteConfirm").interactable,Is.False);
            Assert.That(TargetFullyInsideViewport(Button("RemoteDieD032")),Is.False,"Last targets must be clipped until the player scrolls.");
            yield return WheelTargetIntoView("RemoteTargetList","RemoteDieD031");
            yield return VisibleTargetClick("RemoteDieD031");
            Assert.That(Button("RemoteConfirm").interactable,Is.False,"One target cannot consume the item.");
            Assert.That(TargetFullyInsideViewport(Button("RemoteDieD032")),Is.True,"Selecting one target must preserve scroll position.");
            yield return VisibleTargetClick("RemoteDieD032");
            yield return VisibleTargetClick("RemoteDieD030");
            Assert.That(HasActive("ChosenD030"),Is.False,"A third target cannot replace either chosen target.");
            Assert.That(Button("RemoteConfirm").interactable,Is.True);
            yield return VisibleTargetClick("RemotePipD031_2");yield return VisibleTargetClick("RemotePipD032_5");
            Assert.That(StateJson(),Is.EqualTo(original),"Scrolling and editing targets are presentation-only.");
            CollectionAssert.AreEqual(originalDisk,File.ReadAllBytes(app.SaveLocation));
            yield return VisibleTargetClick("RemoteConfirm");
            var after=app.Session.State;
            Assert.That(after.actionSequence,Is.EqualTo(before.actionSequence+1));
            Assert.That(after.hand.rollsRemaining,Is.EqualTo(before.hand.rollsRemaining-1));
            Assert.That(after.dice.Count,Is.EqualTo(32));
            Assert.That(after.inventory.Any(item=>item.instanceId==itemId),Is.False);
            Assert.That(after.consumedItemIds.Count(id=>id==itemId),Is.EqualTo(1));
            var command=after.processedReceipts.Last().command;
            CollectionAssert.AreEqual(new[]{"D031","D032"},command.dieIds);
            CollectionAssert.AreEqual(new[]{2,5},command.targetPips);
            Assert.That(after.hand.currentResults.Count(result=>result.remoteOverride),Is.EqualTo(2));
            Assert.That(after.hand.currentResults.Single(result=>result.dieId=="D031").currentPip,Is.EqualTo(2));
            Assert.That(after.hand.currentResults.Single(result=>result.dieId=="D032").currentPip,Is.EqualTo(5));
            Assert.That(after.hand.currentResults.All(result=>result.hasRolled),Is.True);
            app.SkipVisual();yield return WaitForIdle();
        }

        [UnityTest]
        public IEnumerator PLAY_V06_ShopThirtyTwoTargetsScrollUpgradeAndDeleteOnlyChosenDie()
        {
            var fixture=ShopBoundaryFixture();
            while(fixture.dice.Count<32)fixture.dice.Add(DiceFactory.CreateStandard(app.Config,fixture.nextDieSerial++));
            fixture.shop.offers.Clear();
            AddBoundaryOffer(fixture,"target32-gold","die_upgrade","U06");
            AddBoundaryOffer(fixture,"target32-remove","service","O04");
            yield return LoadBoundaryFixture(fixture);ResumeBoundaryFixture();
            string original=StateJson();byte[] originalDisk=File.ReadAllBytes(app.SaveLocation);
            var before=app.Session.State;
            OpenBoundaryOffer("target32-gold");
            Assert.That(TargetFullyInsideViewport(Button("Target_D031")),Is.False);
            yield return WheelTargetIntoView("ShopTargetList","Target_D031");
            yield return VisibleTargetClick("Target_D031");
            Assert.That(TargetFullyInsideViewport(Button("Target_D032")),Is.True,"Selecting a die must not reset the target list to the top.");
            yield return VisibleTargetClick("Face_F01");
            Assert.That(StateJson(),Is.EqualTo(original));
            CollectionAssert.AreEqual(originalDisk,File.ReadAllBytes(app.SaveLocation));
            yield return VisibleTargetClick("ConfirmPurchase");
            var upgraded=app.Session.State;
            Assert.That(upgraded.dice.Single(die=>die.dieId=="D031").faces.Single(face=>face.faceId=="F01").isGold,Is.True);
            foreach(var die in upgraded.dice.Where(die=>die.dieId!="D031"))
                Assert.That(JsonUtility.ToJson(die),Is.EqualTo(JsonUtility.ToJson(before.dice.Single(old=>old.dieId==die.dieId))));
            Assert.That(upgraded.gold,Is.EqualTo(before.gold-app.Config.Upgrade("U06").buyPrice));
            Assert.That(upgraded.actionSequence,Is.EqualTo(before.actionSequence+1));
            OpenBoundaryOffer("target32-remove");
            yield return WheelTargetIntoView("ShopTargetList","Target_D032");
            yield return VisibleTargetClick("Target_D032");yield return VisibleTargetClick("CloseDialog");
            Assert.That(StateJson(),Is.EqualTo(JsonUtility.ToJson(upgraded)),"Cancelling deletion must not mutate the target or wallet.");
            OpenBoundaryOffer("target32-remove");
            yield return WheelTargetIntoView("ShopTargetList","Target_D032");
            yield return VisibleTargetClick("Target_D032");yield return VisibleTargetClick("ConfirmPurchase");
            var removed=app.Session.State;
            Assert.That(removed.dice.Count,Is.EqualTo(31));
            Assert.That(removed.dice.Any(die=>die.dieId=="D032"),Is.False);
            Assert.That(removed.dice.Single(die=>die.dieId=="D031").faces.Single(face=>face.faceId=="F01").isGold,Is.True);
            Assert.That(removed.baseSubmitCapacity,Is.EqualTo(before.baseSubmitCapacity));
            Assert.That(removed.stage.capacity,Is.EqualTo(before.stage.capacity));
            Assert.That(removed.gold,Is.EqualTo(upgraded.gold-app.Config.Service("O04").baseBuyPrice));
            Assert.That(removed.actionSequence,Is.EqualTo(upgraded.actionSequence+1));
            Assert.That(removed.shopRngState,Is.EqualTo(before.shopRngState));
            yield return null;
        }

        private IEnumerator WheelTargetIntoView(string listName,string buttonName)
        {
            var scroll=FindActive<UnityEngine.UI.ScrollRect>(listName);
            Assert.That(scroll.content.rect.height,Is.GreaterThan(scroll.viewport.rect.height));
            int events=0;
            while(!TargetFullyInsideViewport(Button(buttonName))&&events++<20)
            {
                var pointer=new PointerEventData(EventSystem.current)
                {
                    position=RectTransformUtility.WorldToScreenPoint(null,scroll.viewport.TransformPoint(scroll.viewport.rect.center)),
                    scrollDelta=new Vector2(0,-1)
                };
                ExecuteEvents.Execute(scroll.gameObject,pointer,ExecuteEvents.scrollHandler);
                Canvas.ForceUpdateCanvases();yield return null;
            }
            Assert.That(events,Is.GreaterThan(0),"The regression must exercise actual ScrollRect wheel handling.");
            Assert.That(TargetFullyInsideViewport(Button(buttonName)),Is.True,"Target is still clipped after scrolling: "+buttonName);
            Assert.That(scroll.verticalNormalizedPosition,Is.LessThan(1));
        }

        private static bool TargetFullyInsideViewport(UnityEngine.UI.Button button)
        {
            var scroll=button.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            if(scroll==null)return true;
            var corners=new Vector3[4];((RectTransform)button.transform).GetWorldCorners(corners);
            var bounds=scroll.viewport.rect;
            return corners.All(corner=>
            {
                var local=scroll.viewport.InverseTransformPoint(corner);
                return local.x>=bounds.xMin-.5f&&local.x<=bounds.xMax+.5f&&local.y>=bounds.yMin-.5f&&local.y<=bounds.yMax+.5f;
            });
        }

        private IEnumerator VisibleTargetClick(string name)
        {
            Canvas.ForceUpdateCanvases();var button=Button(name);var rect=(RectTransform)button.transform;
            Vector2 initialPosition=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            var initialPointer=new PointerEventData(EventSystem.current){position=initialPosition};
            var initialHits=new List<RaycastResult>();EventSystem.current.RaycastAll(initialPointer,initialHits);
            TestContext.WriteLine($"Target before rendered frame: {name}, depth={button.targetGraphic.depth}, cull={button.targetGraphic.canvasRenderer.cull}, hits={string.Join(",",initialHits.Take(4).Select(hit=>hit.gameObject.name+":"+hit.depth))}");
            // A player cannot click a newly created graphic before its first rendered frame.
            // Keep the real raycast assertion after that frame; forcing layout alone is insufficient.
            yield return null;
            Canvas.ForceUpdateCanvases();button=Button(name);rect=(RectTransform)button.transform;
            Assert.That(button.IsInteractable(),Is.True,name);
            Assert.That(TargetFullyInsideViewport(button),Is.True,"Cannot click a masked target: "+name);
            Vector2 position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            Assert.That(position.x,Is.InRange(0,Screen.width));Assert.That(position.y,Is.InRange(0,Screen.height));
            var pointer=new PointerEventData(EventSystem.current){position=position,button=PointerEventData.InputButton.Left};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            string hitNames=string.Join(",",hits.Take(4).Select(hit=>hit.gameObject.name+":"+hit.depth));
            TestContext.WriteLine($"Target after rendered frame: {name}, depth={button.targetGraphic.depth}, cull={button.targetGraphic.canvasRenderer.cull}, hits={hitNames}");
            Assert.That(hits.Count,Is.GreaterThan(0),"Target has no actual UI raycast hit: "+name);
            Assert.That(hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>(),Is.SameAs(button),"Another UI element covers the target: "+name+"; hits="+hitNames+"; targetDepth="+button.targetGraphic.depth);
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);Canvas.ForceUpdateCanvases();
        }
    }
}
