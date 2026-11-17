using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [UnityTest]
        public IEnumerator PLAY_PooledRefreshKeepsNodeIdentitiesAndResetsScrollInputAndSlider()
        {
            StartRun("515151"); Click("StartStage"); Click("Roll"); Click("Skip");
            foreach (string id in app.Session.State.dice.Select(d => d.dieId).ToArray()) ClickDie(id);
            Click("Confirm");yield return WaitForIdle();
            string stableState = StateJson();
            var diePointer = FindActive<DiePointer>("Die_D001");
            TMP_InputField input = null;
            ScrollRect detailScroll = null;
            Slider volumeSlider = null;
            int staleInputCalls = 0, staleSliderCalls = 0;
            IEnumerator Cycle()
            {
                app.Refresh(); app.Refresh();
                Assert.That(FindActive<DiePointer>("Die_D001"), Is.SameAs(diePointer));
                ClickDie("D001"); // Locked die detail uses the distinct transparent modal role.
                Assert.That(HasActive("RotatePreview"), Is.True); Click("CloseDialog");
                yield return LegacyClick07("Factor0");
                yield return LegacyClick07("FullScoreDetails"); // S07 opens the three-column drawer first.
                var scroll = FindActive<ScrollRect>("ScoreDetail");
                if (detailScroll == null) detailScroll = scroll;
                else Assert.That(scroll, Is.SameAs(detailScroll));
                Canvas.ForceUpdateCanvases();
                Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height), "The fixture needs genuinely scrollable score details.");
                Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(.001f));
                Assert.That(scroll.velocity.sqrMagnitude, Is.LessThan(.0001f));
                scroll.verticalNormalizedPosition = 0; scroll.velocity = new Vector2(0, 70);
                Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(0f).Within(.001f));
                Click("CloseDialog"); Click("Pause"); Click("Settings"); Click("SettingsTab0");
                var slider = FindActive<Slider>("MasterSlider");
                if (volumeSlider == null) volumeSlider = slider;
                else
                {
                    Assert.That(slider, Is.SameAs(volumeSlider));
                    Assert.That(slider.value, Is.EqualTo(.45f).Within(.0001f));
                    Assert.That(slider.fillRect.anchorMax.x, Is.EqualTo(.45f).Within(.0001f), "Reused fillRect must show the retained volume, not a full bar.");
                }
                slider.value = .45f;
                slider.onValueChanged.AddListener(_ => staleSliderCalls++);
                Click("SettingsTab1"); Click("SettingsTab0"); // Rebuild sound settings with the same slider, fill reference and value.
                Assert.That(FindActive<Slider>("MasterSlider"), Is.SameAs(slider));
                Assert.That(slider.fillRect.anchorMax.x, Is.EqualTo(.45f).Within(.0001f));
                Assert.That(staleSliderCalls, Is.Zero, "Old slider callbacks must be removed before rebuilding.");
                slider.onValueChanged.AddListener(_ => staleSliderCalls++);
                Click("SettingsDone"); Click("Pause"); Click("MainMenu"); Click("NewRun");
                var seed = FindActive<TMP_InputField>("SeedInput");
                if (input == null) input = seed;
                else Assert.That(seed, Is.SameAs(input));
                Assert.That(seed.text, Is.Empty, "Reopening the seed input must reset the old typed value.");
                Assert.That(seed.isFocused, Is.False);
                seed.text = "999999";
                Assert.That(staleInputCalls, Is.Zero, "An old input listener must not survive its next rental.");
                EventSystem.current.SetSelectedGameObject(seed.gameObject); seed.ActivateInputField();
                yield return null; yield return null;
                Assert.That(seed.isFocused, Is.True);
                Assert.That(seed.GetComponentsInChildren<TMP_SelectionCaret>(true).Any(c => c.gameObject.activeInHierarchy), Is.True,
                    "Focused pooled TMP input must retain an active caret after its viewport was cleared.");
                seed.onValueChanged.AddListener(_ => staleInputCalls++);
                Click("CloseDialog"); Click("ContinueRun");
                Assert.That(StateJson(), Is.EqualTo(stableState));
                Canvas.ForceUpdateCanvases();yield return new WaitForSecondsRealtime(.15f);
            }
            yield return Cycle(); yield return Cycle(); // Warm both modal roles, all labels, caret and scroll.
            Transform[] warmedNodes = app.canvas.GetComponentsInChildren<Transform>(true);
            for (int repeat = 0; repeat < 12; repeat++)
            {
                yield return Cycle();
                CollectionAssert.AreEquivalent(warmedNodes, app.canvas.GetComponentsInChildren<Transform>(true),
                    "Refresh/menu/modal cycle " + repeat + " created, destroyed or retained additional nodes after warm-up.");
                AssertOneInteractiveComponentPerNode();
            }
            Assert.That(staleInputCalls, Is.Zero); Assert.That(staleSliderCalls, Is.Zero);
        }

        [UnityTest]
        public IEnumerator PLAY_PooledPurchaseAndReadonlyFacesCannotRetainOldTargetsOrListeners()
        {
            var fixture = ShopBoundaryFixture(); fixture.shop.offers.Clear();
            AddBoundaryOffer(fixture, "old-smooth", "die_upgrade", "U01");
            AddBoundaryOffer(fixture, "new-gold", "die_upgrade", "U06");
            AddBoundaryOffer(fixture, "new-controller", "controller", "C02");
            AddBoundaryOffer(fixture, "whole-glass", "die_upgrade", "U05");
            yield return LoadBoundaryFixture(fixture); ResumeBoundaryFixture();
            string original = StateJson();
            yield return OpenBoundaryOffer("new-controller"); var controllerConfirm=Button("ConfirmPurchase"); Click("CloseDialog");
            UnityEngine.UI.Button confirm = null;
            int staleFaceCalls = 0;
            // V1.1 (W14, SHOP_LAYOUT 7): the face is chosen on the table (S1 die, S2 face, S3 confirm) instead of in the dice
            // sheet. The same three guarantees are checked on the pooled face buttons and the pooled confirmation: a whole-die
            // upgrade offers no face to choose, and the hidden pooled face it leaves behind cannot choose one; a face drawn
            // again has lost the listener of the earlier flow; the reused confirmation executes one latest purchase.
            for (int repeat = 0; repeat < 6; repeat++)
            {
                yield return OpenBoundaryOffer("old-smooth"); Click("Target_D001");
                var oldFace = Button("Face_F02");
                oldFace.onClick.AddListener(() => staleFaceCalls++);
                Click("Face_F01"); Assert.That(app.FacePickFace14, Is.EqualTo("F01")); Click("CancelFacePick14");
                yield return OpenBoundaryOffer("whole-glass"); Click("Target_D001"); // Whole-die targeting must not use per-face handlers.
                Assert.That(app.FacePickStep14, Is.EqualTo("S3")); Assert.That(HasActive("Face_F02"), Is.False, "A whole-die upgrade shows no face to choose.");
                string die = app.FacePickDie14;
                oldFace.onClick.Invoke(); // Deliberately call the hidden pooled face of the earlier flow (with the probe listener).
                Assert.That(app.FacePickStep14, Is.EqualTo("S3")); Assert.That(app.FacePickFace14, Is.Null, "A read-only face must not choose a face for a whole die.");
                Assert.That(app.FacePickDie14, Is.EqualTo(die)); Assert.That(StateJson(), Is.EqualTo(original));
                staleFaceCalls = 0; // the probe ran because that face has not been drawn again yet; count from here
                if (confirm == null) confirm = Button("ConfirmPurchase");
                else Assert.That(Button("ConfirmPurchase"), Is.SameAs(confirm));
                Click("CancelFacePick14");
                yield return OpenBoundaryOffer("new-gold"); Click("Target_D001"); Click("Target_D002");
                var reused = Button("Face_F02"); Assert.That(reused, Is.SameAs(oldFace), "The face buttons are pooled.");
                reused.onClick.Invoke(); // drawn again: the listener added in the earlier flow is gone
                Assert.That(staleFaceCalls, Is.Zero, "A pooled face drawn again must not keep a former listener.");
                Assert.That(app.FacePickFace14, Is.EqualTo("F02")); Click("ChangeFace14"); Click("Face_F03");
                Assert.That(app.FacePickDie14, Is.EqualTo("D002")); Assert.That(app.FacePickFace14, Is.EqualTo("F03"));
                Assert.That(Button("ConfirmPurchase"), Is.SameAs(confirm));
                Assert.That(StateJson(), Is.EqualTo(original));
                if (repeat < 5) Click("CancelFacePick14");
            }
            long sequence = app.Session.State.actionSequence;
            long gold = app.Session.State.gold;
            string log = Path.Combine(saveDirectory, "debug-actions.jsonl");
            int priorLines = File.Exists(log) ? File.ReadAllLines(log).Length : 0;
            Click("ConfirmPurchase");
            var bought = app.Session.State;
            Assert.That(bought.actionSequence, Is.EqualTo(sequence + 1), "A reused button must execute one latest purchase, not every former listener.");
            Assert.That(bought.gold, Is.EqualTo(gold - app.Config.Upgrade("U06").buyPrice));
            Assert.That(bought.shop.offers.Single(o => o.offerId == "old-smooth").sold, Is.False);
            Assert.That(bought.shop.offers.Single(o => o.offerId == "new-gold").sold, Is.True);
            Assert.That(bought.dice.Single(d => d.dieId == "D001").dataRevision, Is.Zero);
            var selected = bought.dice.Single(d => d.dieId == "D002");
            Assert.That(selected.dataRevision, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] {"F03"}, selected.faces.Where(f => f.isGold).Select(f => f.faceId));
            Assert.That(File.ReadAllLines(log).Length, Is.EqualTo(priorLines + 1));
            yield return OpenBoundaryOffer("new-controller");
            Assert.That(Button("ConfirmPurchase"), Is.SameAs(controllerConfirm), "The separate opaque controller detail reuses its own confirmation button.");
            Click("ConfirmPurchase");
            Assert.That(app.Session.State.actionSequence, Is.EqualTo(sequence + 2));
            Assert.That(app.Session.State.controllers.Count(c => c.contentId == "C02"), Is.EqualTo(1));
            Assert.That(File.ReadAllLines(log).Length, Is.EqualTo(priorLines + 2));
            AssertOneInteractiveComponentPerNode();
            yield return null;
        }

        private void AssertOneInteractiveComponentPerNode()
        {
            foreach (var node in app.canvas.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(node.GetComponents<UnityEngine.UI.Button>().Length, Is.LessThanOrEqualTo(1), node.name + " Button");
                Assert.That(node.GetComponents<Slider>().Length, Is.LessThanOrEqualTo(1), node.name + " Slider");
                Assert.That(node.GetComponents<TMP_InputField>().Length, Is.LessThanOrEqualTo(1), node.name + " Input");
                Assert.That(node.GetComponents<ScrollRect>().Length, Is.LessThanOrEqualTo(1), node.name + " Scroll");
                Assert.That(node.GetComponents<DiePointer>().Length, Is.LessThanOrEqualTo(1), node.name + " Pointer");
            }
        }
    }
}
