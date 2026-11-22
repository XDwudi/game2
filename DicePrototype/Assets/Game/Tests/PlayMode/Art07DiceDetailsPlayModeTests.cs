using System.Collections;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [UnityTest]
        public IEnumerator PLAY_W16B_BlueWaveDieSheetNamesTheCommittedSourceOfColor()
        {
            var initial=NewBoundaryFixture("w16b-blue-wave");
            foreach(var f in initial.dice[0].faces){f.baseColor="red";f.texture="none";}
            initial.dice[0].faces[0].baseWeight=2;
            BoundaryItem(initial,"I10");
            yield return LoadBoundaryFixture(initial);ResumeBoundaryFixture();Click("StartStage");Click("Roll");app.SkipVisual();
            ClickDie("D001");Click("Confirm");yield return WaitForIdle();
            var item=app.Session.State.inventory.First(i=>i.contentId=="I10");
            Click("Item_"+item.instanceId);Click("Use");yield return WaitForIdle();
            var disk=File.ReadAllBytes(app.SaveLocation);string state=StateJson();
            ClickDie("D001");
            Assert.That(InformationText07("ValueSourcesBody"),Does.Contain(app.Config.Consumable("I10").displayName));
            Assert.That(InformationText07("ProbabilitySourcesBody"),Is.EqualTo("骰面权重（下表已计入）"));
            Assert.That(InformationText07("LayersBody"),Does.Contain("红色").And.Contain("蓝色"));
            Click("CloseDialog");Assert.That(StateJson(),Is.EqualTo(state));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
        }

        [UnityTest]
        public IEnumerator PLAY_Art07_DiceNetSeparatesPermanentRemoteAndEffectiveAndKeepsZeroProbabilityEmpty()
        {
            var fixture=NewBoundaryFixture("art07-dice-net");
            fixture.controllers.Add(BoundaryController(fixture,"C12"));BoundaryItem(fixture,"I03");
            foreach(var face in fixture.dice[0].faces){face.permanentPip=1;face.baseColor="blue";}
            yield return LoadBoundaryFixture(fixture);ResumeBoundaryFixture();Click("StartStage");
            Click("ViewDice");
            Assert.That(FindActive<TextMeshProUGUI>("LayersBody").text,Is.EqualTo("还没掷"));
            Click("CloseDialog");
            OpenRemoteItem();ChooseRemoteTargets();Click("RemotePipD001_3");Click("RemoteConfirm");app.SkipVisual();
            Click("ViewDice");
            Assert.That(FindActive<TextMeshProUGUI>("LayersBody").text,Is.EqualTo("骰面原本 1 点蓝色 · 这次掷出 3 点蓝色"));
            Assert.That(FindActive<TextMeshProUGUI>("ValueSourcesBody").text,Does.Contain("遥控骰子指定了点数"));
            Click("CloseDialog");
            ClickDie("D001");Click("Confirm");yield return WaitForIdle();string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);
            ClickDie("D001");
            var dialog=FindActive<RectTransform>("Dialog");
            Assert.That(dialog.anchoredPosition,Is.EqualTo(new Vector2(208,-96)));
            Assert.That(dialog.sizeDelta,Is.EqualTo(new Vector2(1504,888)));
            // v0.73: no flat face net. Six face buttons in physical order turn the real die; each is 92 x 64.
            string[] ids={"F01","F02","F03","F04","F05","F06"};
            for(int i=0;i<ids.Length;i++)
            {
                var face=FindActive<RectTransform>("Face_"+ids[i]);
                Assert.That(face.sizeDelta,Is.EqualTo(new Vector2(92,64)));
                Assert.That(face.anchoredPosition,Is.EqualTo(new Vector2(848+i*101,-160)),"Face button order: "+ids[i]);
                Assert.That(face.Find("Label").GetComponent<TextMeshProUGUI>().text,Is.EqualTo("面 "+(i+1)));
            }
            Assert.That(app.canvas.GetComponentsInChildren<RectTransform>(false).Any(r=>r.name=="ExampleFace"),Is.False,"The dice sheet must not draw flat face tiles.");
            string layers=FindActive<TextMeshProUGUI>("LayersBody").text;
            // v1.1 (W09, TX-02): colours without the English initials R/B.
            Assert.That(layers,Is.EqualTo("骰面原本 1 点蓝色 · 这次掷出 3 点蓝色 · 计分按 4 点蓝色"));
            Assert.That(FindActive<TextMeshProUGUI>("ValueSourcesBody").text,Does.Contain("遥控骰子指定了点数（骰面本身不变）"));
            var current=app.Session.State.hand.currentResults.Single(r=>r.dieId=="D001");
            Assert.That(FindActive<Transform>("Face_"+current.currentFaceId).Find("CurrentFace").gameObject.activeSelf,Is.True);
            Assert.That(FindActive<RectTransform>("ProbabilityBar2").rect.width,Is.Zero,"A truly impossible permanent number has an empty bar.");
            Assert.That(FindActive<TextMeshProUGUI>("ProbabilityValue2").text,Is.EqualTo("0%"));
            Click("Face_F05");
            Assert.That(FindActive<Transform>("Face_F05").Find("SelectedH0").gameObject.activeSelf,Is.True);
            // The real die turns to the chosen face (instantly under reduced motion, otherwise within 0.18 s).
            double turnDeadline=Time.realtimeSinceStartupAsDouble+2;while(app.SheetTurning073&&Time.realtimeSinceStartupAsDouble<turnDeadline)yield return null;
            Assert.That(app.SheetFacingFace073,Is.EqualTo(4),"Face 5 must look at the player after its button is pressed.");
            Assert.That(FindActive<TextMeshProUGUI>("FacingFace073").text,Does.StartWith("正对你的是第 5 面"));
            var rotate=FindActive<DiePointer>("RotatePreview");
            ExecuteEvents.Execute(rotate.gameObject,new PointerEventData(EventSystem.current){delta=new Vector2(80,40)},ExecuteEvents.dragHandler);
            Click("ProbabilityGroups");Click("CloseDialog");
            Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            Assert.That(HasActive("Die_D001"),Is.True,"Closing the preview must restore the real board.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator PLAY_Art07_AbandonUnsavedRollRestoresCheckpointWithoutReplayingOrReplacingCandidate()
        {
            StartRun("70707");Click("StartStage");
            string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);long sequence=app.Session.State.actionSequence;
            UnityEngine.Events.UnityEvent abandonedButton;
            using(var locked=new FileStream(app.SaveLocation,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                Click("Roll");Assert.That(app.Session.HasPendingSave,Is.True);Assert.That(app.IsBusy,Is.False);
                Assert.That(StateJson(),Is.EqualTo(before));
                Assert.That(FindActive<TextMeshProUGUI>("DialogTitle").text,Is.EqualTo("本次结果尚未保存"));
                abandonedButton=Button("AbandonPendingSave").onClick;Click("AbandonPendingSave");
                Assert.That(app.Session.HasPendingSave,Is.False);Assert.That(StateJson(),Is.EqualTo(before));
                Assert.That(HasActive("Dialog"),Is.False);Assert.That(Button("Roll").interactable,Is.True);
                CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            }
            abandonedButton.Invoke();Assert.That(StateJson(),Is.EqualTo(before));
            Click("Roll");Assert.That(app.Session.State.actionSequence,Is.EqualTo(sequence+1));
            Assert.That(app.Session.HasPendingSave,Is.False);app.SkipVisual();
            Assert.That(JsonUtility.ToJson(new AtomicSaveStore(app.SaveLocation,app.Config).Load()),Is.EqualTo(StateJson()));
            yield return null;
        }
    }
}
