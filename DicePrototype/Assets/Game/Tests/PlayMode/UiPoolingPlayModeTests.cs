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
            Click("Confirm");
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
                Click("Expanded");
                var scroll = FindActive<ScrollRect>("ScoreDetail");
                if (detailScroll == null) detailScroll = scroll;
                else Assert.That(scroll, Is.SameAs(detailScroll));
                Canvas.ForceUpdateCanvases();
                Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height), "The fixture needs genuinely scrollable score details.");
                Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(.001f));
                Assert.That(scroll.velocity.sqrMagnitude, Is.LessThan(.0001f));
                scroll.verticalNormalizedPosition = 0; scroll.velocity = new Vector2(0, 70);
                Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(0f).Within(.001f));
                Click("CloseDialog"); Click("Pause");
                var slider = FindActive<Slider>("Volume");
                if (volumeSlider == null) volumeSlider = slider;
                else
                {
                    Assert.That(slider, Is.SameAs(volumeSlider));
                    Assert.That(slider.value, Is.EqualTo(.45f).Within(.0001f));
                    Assert.That(slider.fillRect.anchorMax.x, Is.EqualTo(.45f).Within(.0001f), "Reused fillRect must show the retained volume, not a full bar.");
                }
                slider.value = .45f;
                slider.onValueChanged.AddListener(_ => staleSliderCalls++);
                Click("Fast"); // Rebuild Pause with the same slider, fill reference and value.
                Assert.That(FindActive<Slider>("Volume"), Is.SameAs(slider));
                Assert.That(slider.fillRect.anchorMax.x, Is.EqualTo(.45f).Within(.0001f));
                Assert.That(staleSliderCalls, Is.Zero, "Old slider callbacks must be removed before rebuilding.");
                slider.onValueChanged.AddListener(_ => staleSliderCalls++);
                Click("MainMenu");
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
                Click("ContinueRun");
                Assert.That(StateJson(), Is.EqualTo(stableState));
                Canvas.ForceUpdateCanvases(); yield return null;
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
            yield return LoadBoundaryFixture(fixture); Click("ContinueRun");
            string original = StateJson();
            UnityEngine.UI.Button confirm = null;
            int staleFaceCalls = 0;
            for (int repeat = 0; repeat < 6; repeat++)
            {
                OpenBoundaryOffer("old-smooth"); Click("Target_D001");
                var oldFace = Button("Face_F02");
                oldFace.onClick.AddListener(() => staleFaceCalls++);
                Click("Face_F01");
                var readonlyFace = FindActive<Transform>("Face_F02").GetComponent<UnityEngine.UI.Button>();
                Assert.That(readonlyFace, Is.SameAs(oldFace));
                Assert.That(readonlyFace.enabled, Is.False); Assert.That(readonlyFace.interactable, Is.False);
                string title = FindActive<TextMeshProUGUI>("DialogTitle").text;
                readonlyFace.onClick.Invoke(); // Deliberately bypass disabled UI to prove stale listeners were removed.
                Assert.That(staleFaceCalls, Is.Zero);
                Assert.That(FindActive<TextMeshProUGUI>("DialogTitle").text, Is.EqualTo(title), "A read-only face must not reopen the former target preview.");
                if (confirm == null) confirm = Button("ConfirmPurchase");
                else Assert.That(Button("ConfirmPurchase"), Is.SameAs(confirm));
                Click("CloseDialog");
                OpenBoundaryOffer("new-gold"); Click("Target_D001"); Click("Target_D002"); Click("Face_F03");
                Assert.That(Button("ConfirmPurchase"), Is.SameAs(confirm));
                Assert.That(StateJson(), Is.EqualTo(original));
                if (repeat < 5) Click("CloseDialog");
            }
            long sequence = app.Session.State.actionSequence;
            int gold = app.Session.State.gold;
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
            OpenBoundaryOffer("new-controller");
            Assert.That(Button("ConfirmPurchase"), Is.SameAs(confirm), "The same dialog button also changes purchase category.");
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
