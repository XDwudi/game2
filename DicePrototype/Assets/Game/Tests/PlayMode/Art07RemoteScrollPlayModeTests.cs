using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [Serializable] private sealed class RemoteTargetGeometry07
        {public string id;public Rect viewportLocal,screen;public bool fullyVisible;}
        [Serializable] private sealed class RemoteScrollGeometry07
        {
            public string id,sourceManifestSha,runtimeMvid,stateHash,saveSha;
            public int width,height,fontPercent,wheelEvents;public float sensitivity,normalizedPosition;
            public Rect viewport,content;public Vector2 contentOffset;
            public List<RemoteTargetGeometry07> targets=new List<RemoteTargetGeometry07>();
        }
        private IEnumerator RemoteGeometry07(string id,int firstVisible,int wheelEvents)
        {
            yield return ExtraCapture07(id,"S13","Real remote-target row after unit wheel events, with actual viewport and all32 target bounds.");
            var scroll=FindActive<ScrollRect>("RemoteTargetList");var row=new RemoteScrollGeometry07{id=id,sourceManifestSha=extra07.sourceManifestSha256,runtimeMvid=extra07.runtimeMvid,
                stateHash=ExtraStateHash07(),saveSha=ExtraFileHash07(app.SaveLocation),width=Screen.width,height=Screen.height,fontPercent=Mathf.RoundToInt(UiKit.FontScale*100),wheelEvents=wheelEvents,
                sensitivity=scroll.scrollSensitivity,normalizedPosition=scroll.verticalNormalizedPosition,viewport=scroll.viewport.rect,content=scroll.content.rect,contentOffset=scroll.content.anchoredPosition};
            foreach(var button in scroll.content.GetComponentsInChildren<Button>(false))
            {
                if(!button.name.StartsWith("RemoteDie"))continue;
                var corners=new Vector3[4];((RectTransform)button.transform).GetWorldCorners(corners);var local=corners.Select(scroll.viewport.InverseTransformPoint).ToArray();
                row.targets.Add(new RemoteTargetGeometry07{id=button.name.Substring("RemoteDie".Length),viewportLocal=Rect.MinMaxRect(local.Min(p=>p.x),local.Min(p=>p.y),local.Max(p=>p.x),local.Max(p=>p.y)),screen=ExtraBounds07((RectTransform)button.transform),fullyVisible=TargetFullyInsideViewport(button)});
            }
            File.WriteAllText(Path.Combine(extra07.output,id+"-geometry.json"),JsonUtility.ToJson(row,true));
            Assert.That(row.targets.Count,Is.EqualTo(32));
            CollectionAssert.AreEquivalent(Enumerable.Range(firstVisible,8).Select(n=>"D"+n.ToString("D3")),row.targets.Where(t=>t.fullyVisible).Select(t=>t.id),"One wheel tick must land on a complete selectable row, not split two64-high rows across a64-high viewport.");
            foreach(var target in row.targets.Where(t=>t.fullyVisible))
            {
                Assert.That(target.screen.width,Is.GreaterThanOrEqualTo(39.99f),target.id);Assert.That(target.screen.height,Is.GreaterThanOrEqualTo(39.99f),target.id);
            }
            // Compare real neighboring-row bounds rather than assuming a particular constant.
            var upper=row.targets.Single(t=>t.id=="D001");var lower=row.targets.Single(t=>t.id=="D009");
            Assert.That(upper.screen.yMin-lower.screen.yMax,Is.GreaterThanOrEqualTo(3.99f),"Adjacent target hot zones must retain four physical pixels of separation.");
        }
        [UnityTest] public IEnumerator PLAY_ART07_RemoteThirtyTwoRowsEachWheelFullyVisibleAndScrollReadOnlyAtTwoFonts()
        {
            yield return ExtraBegin07();
            foreach(int font in new[]{100,150})
            {
                yield return CardSetFont07(font);
                var fixture=NewBoundaryFixture("remote-row-geometry-"+font);
                while(fixture.dice.Count<32)fixture.dice.Add(DiceFactory.CreateStandard(app.Config,fixture.nextDieSerial++));
                while(fixture.controllers.Count<32)fixture.controllers.Add(BoundaryController(fixture,"C02"));
                string item=BoundaryItem(fixture,"I03");yield return ExtraLoadFixture07(fixture,"remote-rows-font"+font);
                yield return ExtraPress07("StartStage",true);string original=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);long sequence=app.Session.ActionSequence;
                yield return ExtraPress07("Item_"+item);yield return ExtraPress07("Use");
                var scroll=FindActive<ScrollRect>("RemoteTargetList");Assert.That(scroll.content.rect.height,Is.GreaterThan(scroll.viewport.rect.height));
                for(int row=0;row<4;row++)
                {
                    if(row>0)
                    {
                        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,scroll.viewport.TransformPoint(scroll.viewport.rect.center)),scrollDelta=new Vector2(0,-1)};
                        ExecuteEvents.Execute(scroll.gameObject,pointer,ExecuteEvents.scrollHandler);Canvas.ForceUpdateCanvases();yield return null;
                    }
                    yield return RemoteGeometry07("font"+font+"-remote-row"+row,row*8+1,row);
                    Assert.That(StateJson(),Is.EqualTo(original));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));
                }
                float last=scroll.verticalNormalizedPosition;
                yield return ExtraPress07("RemoteDieD031");Assert.That(HasActive("ChosenD031"),Is.True);Assert.That(Button("RemoteConfirm").interactable,Is.False);
                Assert.That(FindActive<ScrollRect>("RemoteTargetList").verticalNormalizedPosition,Is.EqualTo(last).Within(.001f));
                yield return RemoteGeometry07("font"+font+"-remote-selection-retains-row",25,3);
                yield return ExtraPress07("CloseDialog");Assert.That(StateJson(),Is.EqualTo(original));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                Assert.That(app.Session.State.inventory.Any(i=>i.instanceId==item),Is.True);Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));
            }
            ExtraFinish07();
        }
    }
}
