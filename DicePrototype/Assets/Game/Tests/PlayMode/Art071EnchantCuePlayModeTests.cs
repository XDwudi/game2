using System.Linq;
using DiceCraft.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Tests
{
    // Pure component tests: no Bootstrap, GameSession, save file, preference or RNG.
    // White sprites exercise binding only; actual authored art needs the integration render review.
    public sealed class Art071EnchantCuePlayModeTests
    {
        private GameObject host;
        private Art071EnchantCue cue;
        private Sprite sprite;
        private static readonly string[] Ids = { "base10", "mult2", "slotless", "submit1", "roll1" };
        private static readonly Vector2 From = new Vector2(-35, -4), To = new Vector2(35, 8);

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("EnchantCueComponentFixture", typeof(RectTransform), typeof(CanvasGroup));
            cue = host.AddComponent<Art071EnchantCue>();
            sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f);
            cue.Configure(new Vector2(180, 100), sprite, sprite);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(sprite);
        }

        [TestCase("base10")]
        [TestCase("mult2")]
        [TestCase("slotless")]
        [TestCase("submit1")]
        [TestCase("roll1")]
        public void PLAY_ART071_EnchantCue_ExternalClockMovesEachFamilyAndReducedMotionStaysStatic(string id)
        {
            Assert.That(cue.Bind(id, "committed-source", false, From, To), Is.True);
            Assert.That(cue.BoundInstanceId, Is.EqualTo("committed-source"));
            cue.Tick(.1f);
            var first = Geometry();
            cue.Tick(.55f);
            CollectionAssert.AreNotEqual(first, Geometry(), id + " must have its requested visual motion");
            Assert.That(Images().Count(i => i.gameObject.activeSelf && i.rectTransform.rect.width > 0 && i.rectTransform.rect.height > 0), Is.GreaterThan(0));
            Assert.That(Images().All(i => !i.raycastTarget), Is.True);
            Assert.That(host.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(host.GetComponent<CanvasGroup>().interactable, Is.False);

            // Absolute time can be paused and sought without accumulating transforms.
            cue.Tick(.25f); var quarter = Geometry();
            cue.Tick(.8f); cue.Tick(.25f);
            CollectionAssert.AreEqual(quarter, Geometry(), "clock seek must be deterministic");
            cue.Tick(.25f);
            CollectionAssert.AreEqual(quarter, Geometry(), "paused clock must not drift");

            cue.SetReducedMotion(true);
            cue.Tick(.1f); var reduced = Geometry();
            cue.Tick(.6f);
            CollectionAssert.AreEqual(reduced, Geometry(), "reduced motion must not translate, rotate, expand or emit moving paper");
            Assert.That(host.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
            cue.Tick(1);
            Assert.That(host.GetComponent<CanvasGroup>().alpha, Is.Zero);
            Assert.That(host.activeSelf, Is.True, "the external pool owns activation");
        }

        [Test]
        public void PLAY_ART071_EnchantCue_ResetAndRepeatedUseKeepThePrewarmedNodeAndGraphicIdentity()
        {
            var nodes = host.GetComponentsInChildren<Transform>(true).Select(x => x.GetEntityId()).ToArray();
            var graphics = Images().Select(x => x.GetEntityId()).ToArray();
            for (int cycle = 0; cycle < 4; cycle++)
            {
                cue.Configure(new Vector2(180 + cycle, 100), sprite, sprite, cycle % 2 == 0 ? sprite : null);
                foreach (string id in Ids)
                {
                    Assert.That(cue.Bind(id, "source-" + cycle, cycle % 2 == 0, From, To), Is.True);
                    cue.Tick(.45f);
                    cue.ResetForPool();
                    Assert.That(cue.IsBound, Is.False);
                    Assert.That(cue.BoundInstanceId, Is.Null);
                    Assert.That(cue.BoundEnchantmentId, Is.Null);
                    Assert.That(cue.NormalizedTime, Is.Zero);
                    Assert.That(cue.ReducedMotion, Is.False);
                    Assert.That(host.GetComponent<CanvasGroup>().alpha, Is.Zero);
                    Assert.That(Images().All(i => !i.gameObject.activeSelf && !i.raycastTarget && i.rectTransform.anchoredPosition == Vector2.zero && i.rectTransform.sizeDelta == Vector2.zero && i.rectTransform.localRotation == Quaternion.identity && i.rectTransform.localScale == Vector3.one), Is.True);
                    CollectionAssert.AreEqual(nodes, host.GetComponentsInChildren<Transform>(true).Select(x => x.GetEntityId()).ToArray());
                    CollectionAssert.AreEqual(graphics, Images().Select(x => x.GetEntityId()).ToArray());
                }
            }
            Assert.That(cue.AllocatedGraphicCount, Is.EqualTo(graphics.Length));
        }

        [Test]
        public void PLAY_ART071_EnchantCue_OptionalArcAndPaperBudgetDoNotInventMissingAuthoredIconsOrUnknownEffects()
        {
            cue.Configure(new Vector2(180, 100), sprite, sprite, sprite);
            Assert.That(cue.Bind("mult2", "source", false, From, To), Is.True);
            Assert.That(Images().Single(i => i.gameObject.activeSelf).sprite, Is.SameAs(sprite));
            cue.Bind("base10", "source", false, From, To, 0);
            Assert.That(Images().All(i => !i.gameObject.activeSelf), Is.True);
            cue.SetReducedMotion(true);
            Assert.That(Images().Count(i => i.gameObject.activeSelf), Is.GreaterThan(0), "a static mark remains when flying paper is disabled");
            Assert.That(cue.Bind("none", "source", false, From, To), Is.False);
            Assert.That(cue.IsBound, Is.False);
            Assert.That(Images().All(i => !i.gameObject.activeSelf), Is.True);
            cue.Configure(new Vector2(180, 100), null, null);
            Assert.Throws<System.InvalidOperationException>(() => cue.Bind("submit1", "source", false, From, To));
            Assert.That(cue.IsBound, Is.False);
            Assert.Throws<System.InvalidOperationException>(() => cue.Bind("roll1", "source", false, From, To));
            Assert.That(cue.IsBound, Is.False);
        }

        private UnityEngine.UI.Image[] Images() => host.GetComponentsInChildren<UnityEngine.UI.Image>(true);
        private float[] Geometry()
        {
            return Images().SelectMany(i =>
            {
                var r = i.rectTransform;
                return new[] { i.gameObject.activeSelf ? 1f : 0f, r.anchoredPosition.x, r.anchoredPosition.y,
                    r.sizeDelta.x, r.sizeDelta.y, r.localRotation.x, r.localRotation.y, r.localRotation.z, r.localRotation.w,
                    r.localScale.x, r.localScale.y, r.localScale.z };
            }).ToArray();
        }
    }
}
