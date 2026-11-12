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
        public IEnumerator PLAY_Art07_DiceNetSeparatesPermanentRemoteAndEffectiveAndKeepsZeroProbabilityEmpty()
        {
            var fixture=NewBoundaryFixture("art07-dice-net");
            fixture.controllers.Add(BoundaryController(fixture,"C12"));BoundaryItem(fixture,"I03");
            foreach(var face in fixture.dice[0].faces){face.permanentPip=1;face.baseColor="blue";}
            yield return LoadBoundaryFixture(fixture);ResumeBoundaryFixture();Click("StartStage");
            OpenRemoteItem();ChooseRemoteTargets();Click("RemotePipD001_3");Click("RemoteConfirm");app.SkipVisual();
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
            Assert.That(layers,Does.Contain("1 → 3 / 蓝 B（遥控） → 4 / 蓝 B"));
            var current=app.Session.State.hand.currentResults.Single(r=>r.dieId=="D001");
            Assert.That(FindActive<Transform>("Face_"+current.currentFaceId).Find("CurrentFace").gameObject.activeSelf,Is.True);
            Assert.That(FindActive<RectTransform>("ProbabilityBar2").rect.width,Is.Zero,"A truly impossible permanent number has an empty bar.");
            Assert.That(FindActive<TextMeshProUGUI>("ProbabilityValue2").text,Is.EqualTo("0%"));
            Click("Face_F05");
            Assert.That(FindActive<Transform>("Face_F05").Find("SelectedH0").gameObject.activeSelf,Is.True);
            // The real die turns to the chosen face (instantly under reduced motion, otherwise within 0.18 s).
            double turnDeadline=Time.realtimeSinceStartupAsDouble+2;while(app.SheetTurning073&&Time.realtimeSinceStartupAsDouble<turnDeadline)yield return null;
            Assert.That(app.SheetFacingFace073,Is.EqualTo(4),"Face 5 must look at the player after its button is pressed.");
            Assert.That(FindActive<TextMeshProUGUI>("FacingFace073").text,Does.Contain("面 5"));
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
