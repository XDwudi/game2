using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using DiceCraft.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UObj = UnityEngine.Object;

namespace DiceCraft.Tests
{
    // Uses the real Bootstrap and the existing isolated save/preferences fixture.
    // No AssetDatabase fallback: absent build-time references must fail in a Player too.
    public sealed partial class GameplayPlayModeTests
    {
        private Art07PrefabView[] ArtLibraryReferences()
        {
            var field = typeof(GameApp).GetField("art07Prefabs", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Bootstrap GameApp must expose the serialized production art07Prefabs library.");
            Assert.That(field.FieldType, Is.EqualTo(typeof(Art07PrefabView[])));
            var references = (Art07PrefabView[])field.GetValue(app);
            Assert.That(references, Is.Not.Null.And.Not.Empty, "The runtime library must be serialized into Bootstrap, not loaded through an Editor-only API.");
            Assert.That(references.All(v => v != null), Is.True, "A missing prefab reference is not an optional asset.");
            return references;
        }

        private Art07PrefabView ArtLibraryClone(string id)
        {
            var matches = ArtLibraryReferences().Where(v => v.assetId == id).ToArray();
            Assert.That(matches, Has.Length.EqualTo(1), "Expected exactly one serialized view for " + id);
            var clone = UObj.Instantiate(matches[0], app.canvas.transform, false);
            clone.name = "AssetLibraryTest_" + id;
            clone.autoTick = false;
            return clone;
        }

        private static void AssertArtMaterial(Material material, string label)
        {
            Assert.That(material, Is.Not.Null, label + " has no material");
            Assert.That(material.shader, Is.Not.Null, label + " has no shader");
            Assert.That(material.shader.name, Does.Not.Contain("InternalErrorShader"), label);
            Assert.That(material.shader.isSupported, Is.True, label + " shader is not supported by the running graphics backend");
        }

        private static int ActiveArtParticles(Art07PrefabView view) => view.particles.Count(p => p != null && p.gameObject.activeInHierarchy);

        private static void AssertArtVector(Vector3 actual, Vector3 expected, string message)
        { Assert.That(Vector3.Distance(actual, expected), Is.LessThan(.001f), message); }

        private static void ClickArtView(Art07PrefabView view)
        {
            Assert.That(view.action, Is.Not.Null);
            Assert.That(view.action.IsActive() && view.action.IsInteractable(), Is.True);
            ExecuteEvents.Execute(view.action.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        }

        [UnityTest]
        public IEnumerator PLAY_ART07_LibraryAll26SerializedPrefabsInstantiateWithRealMeshesTextAndSupportedMaterials()
        {
            string[] required = {
                "ui_button", "ui_controller_card", "ui_item_card", "ui_upgrade_card", "ui_dice_slot", "ui_stat_counter", "ui_score_breakdown", "ui_probability_bar", "ui_tooltip", "ui_modal", "dice_view", "ui_missing_asset",
                "fx_source_hit", "fx_value_ticket", "fx_target_bracket", "fx_face_change", "fx_copy_link", "fx_slot_expand", "fx_probability_change", "fx_cash_ticket", "fx_random_reveal", "fx_card_destroy", "fx_dice_break", "fx_combo_burst", "fx_stage_stamp", "fx_acquire_remove"
            };
            var library = ArtLibraryReferences();
            CollectionAssert.AreEquivalent(required, library.Select(v => v.assetId), "Stable IDs identify semantic assets, not merely a count of files.");
            foreach (var original in library)
            {
                Assert.That(original.gameObject.scene.IsValid(), Is.False, original.assetId + " must reference an asset, not a currently active scene instance.");
                var clone = ArtLibraryClone(original.assetId);
                try
                {
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    Assert.That(clone.GetComponentsInChildren<MonoBehaviour>(true).All(c => c != null), Is.True, original.assetId + " contains a missing script.");
                    var graphics = clone.GetComponentsInChildren<Graphic>(true);
                    var meshes = clone.GetComponentsInChildren<MeshFilter>(true);
                    Assert.That(graphics.Length + meshes.Length, Is.GreaterThan(0), original.assetId + " has no renderable content.");
                    foreach (var graphic in graphics) AssertArtMaterial(graphic.materialForRendering, original.assetId + "/" + graphic.name);
                    foreach (var label in clone.GetComponentsInChildren<TMP_Text>(true))
                    {
                        Assert.That(label.font, Is.Not.Null, original.assetId + "/" + label.name + " font");
                        Assert.That(label.font.atlasTextures, Is.Not.Null.And.Not.Empty);
                        Assert.That(label.font.atlasTextures.All(t => t != null), Is.True, label.name + " font atlas");
                        AssertArtMaterial(label.fontSharedMaterial, original.assetId + "/" + label.name + " TMP");
                    }
                    foreach (var mesh in meshes)
                    {
                        Assert.That(mesh.sharedMesh, Is.Not.Null, original.assetId + "/" + mesh.name + " mesh");
                        Assert.That(mesh.sharedMesh.vertexCount, Is.GreaterThan(0));
                        var renderer = mesh.GetComponent<MeshRenderer>();
                        Assert.That(renderer, Is.Not.Null, mesh.name);
                        Assert.That(renderer.sharedMaterials, Is.Not.Empty);
                        foreach (var material in renderer.sharedMaterials) AssertArtMaterial(material, original.assetId + "/" + mesh.name);
                    }
                    if (clone.isEffect)
                    {
                        foreach (var graphic in graphics) Assert.That(graphic.raycastTarget, Is.False, original.assetId + " blocks a gameplay target.");
                        if (clone.group != null) Assert.That(clone.group.blocksRaycasts, Is.False);
                    }
                }
                finally { UObj.Destroy(clone.gameObject); }
                yield return null;
            }
            Assert.That(app.Session.State, Is.Null, "Inspecting view assets must not start or mutate a run.");
        }

        [UnityTest]
        public IEnumerator PLAY_ART07_ControllerFullCompactPoolReuseKeepsOneIdentityAndClearsBadgeTintAndOldClick()
        {
            var view = ArtLibraryClone("ui_controller_card");
            try
            {
                var defaultIcon = view.icon.sprite;
                Color originalTint = view.icon.color;
                var alternateIcon = app.contentIcons.First(s => s != defaultIcon);
                var badge = app.enchantIcons.First(s => s != null);
                var nodes = view.GetComponentsInChildren<Transform>(true);
                int oldCalls = 0, newCalls = 0;
                view.Bind("controller-instance-A", "可以重来", "×12345678901234567890", "A 的说明", alternateIcon);
                view.BindBadge(badge); view.BindAction(() => oldCalls++); view.SetCompact(true);
                Assert.That(view.BoundInstanceId, Is.EqualTo("controller-instance-A"));
                Assert.That(((RectTransform)view.transform).sizeDelta, Is.EqualTo(new Vector2(208, 104)));
                Assert.That(view.compactRoot.gameObject.activeInHierarchy, Is.True);
                Assert.That(view.fullOnly.All(g => !g.activeSelf), Is.True);
                Assert.That(view.compactTitle.text, Is.EqualTo(view.title.text));
                Assert.That(view.compactValue.text, Is.EqualTo("×12345678901234567890"));
                Assert.That(view.compactIcon.sprite, Is.SameAs(alternateIcon));
                Assert.That(view.compactBadge.enabled, Is.True); Assert.That(view.compactBadge.sprite, Is.SameAs(badge));
                Assert.That(view.action.targetGraphic, Is.SameAs(view.compactRoot.Find("Surface").GetComponent<Image>()));
                ClickArtView(view); Assert.That(oldCalls, Is.EqualTo(1));
                view.icon.color = Color.magenta; view.SetCompact(false);
                Assert.That(view.badge.enabled, Is.True); Assert.That(view.badge.sprite, Is.SameAs(badge));
                Assert.That(view.title.text, Is.EqualTo("可以重来"));
                view.ResetForPool();
                Assert.That(view.BoundInstanceId, Is.Null); Assert.That(view.gameObject.activeSelf, Is.False);
                Assert.That(view.title.text, Is.Empty); Assert.That(view.compactValue.text, Is.Empty);
                Assert.That(view.badge.enabled || view.compactBadge.enabled, Is.False);
                Assert.That(view.icon.color, Is.EqualTo(originalTint));
                view.gameObject.SetActive(true);
                view.Bind("controller-instance-B", "错乱乘积", "×0.5", "只显示本次已提交结果");
                Assert.That(view.BoundInstanceId, Is.EqualTo("controller-instance-B"));
                Assert.That(((RectTransform)view.transform).sizeDelta, Is.EqualTo(new Vector2(160, 224)));
                Assert.That(view.icon.sprite, Is.SameAs(defaultIcon)); Assert.That(view.compactIcon.sprite, Is.SameAs(defaultIcon));
                ClickArtView(view); Assert.That(oldCalls, Is.EqualTo(1), "Returning a card must remove its old business callback.");
                view.BindAction(() => newCalls++); ClickArtView(view);
                Assert.That(newCalls, Is.EqualTo(1)); Assert.That(oldCalls, Is.EqualTo(1));
                CollectionAssert.AreEquivalent(nodes, view.GetComponentsInChildren<Transform>(true), "Rebinding/switching does not create another hierarchy.");
                Canvas.ForceUpdateCanvases(); yield return null;
            }
            finally { UObj.Destroy(view.gameObject); }
        }

        [UnityTest]
        public IEnumerator PLAY_ART07_BurstL5ReducedThenL0ThenNormalCannotRetainCelebrationOrParticleMotion()
        {
            var view = ArtLibraryClone("fx_combo_burst");
            try
            {
                Assert.That(view.particles, Has.Length.EqualTo(36));
                var particleNodes = view.particles.ToArray(); var resting = particleNodes.Select(p => p.localPosition).ToArray();
                Vector3 position = view.visualRoot.anchoredPosition3D, scale = view.visualRoot.localScale;
                view.SetBurstLevel(5); view.Play(position); view.Evaluate(.5f);
                Assert.That(ActiveArtParticles(view), Is.EqualTo(36)); Assert.That(view.defaultDuration, Is.EqualTo(.6f).Within(.0001f));
                Assert.That(view.particles.Where((p, i) => Vector3.Distance(p.localPosition, resting[i]) > .1f).Count(), Is.GreaterThan(0));
                view.ResetForPool(); view.SetBurstLevel(5); view.Play(position, true); view.Evaluate(.7f);
                Assert.That(ActiveArtParticles(view), Is.Zero);
                AssertArtVector(view.visualRoot.anchoredPosition3D, position, "Reduced motion position");
                AssertArtVector(view.visualRoot.localScale, scale, "Reduced motion scale");
                Assert.That(view.burstStaticStars.Count(g => g.activeInHierarchy), Is.EqualTo(5), "Reduced motion still identifies the same tier.");
                for (int i = 0; i < view.particles.Length; i++) AssertArtVector(view.particles[i].localPosition, resting[i], "Reduced motion particle " + i);
                view.ResetForPool(); view.SetBurstLevel(0); view.Play(position); view.Evaluate(.5f);
                Assert.That(ActiveArtParticles(view), Is.Zero); Assert.That(view.burstStaticStars.Count(g => g.activeInHierarchy), Is.Zero);
                Assert.That(view.burstLayers[0].activeInHierarchy, Is.True);
                Assert.That(view.burstLayers.Skip(1).All(g => !g.activeSelf), Is.True, "L0 must not inherit a ring/halo/flame from the preceding L5.");
                view.ResetForPool(); view.SetBurstLevel(2); view.Play(position); view.Evaluate(.5f);
                Assert.That(view.ReducedMotion, Is.False); Assert.That(ActiveArtParticles(view), Is.EqualTo(8));
                CollectionAssert.AreEqual(particleNodes, view.particles, "Tier changes reuse the authored particle pool.");
                yield return null;
            }
            finally { UObj.Destroy(view.gameObject); }
        }

        [UnityTest]
        public IEnumerator PLAY_ART07_CopyLinkElbowThenStraightResetsBindingSegmentsAndArrowEndpoint()
        {
            var view = ArtLibraryClone("fx_copy_link");
            try
            {
                Vector2 from = new Vector2(10, -20), to = new Vector2(200, -90);
                view.Bind("copy-A", "", "共享抽中 ×0.5", "右邻实例"); view.SetLink(from, to, 250); view.Play(Vector3.zero); view.Evaluate(.4f);
                Assert.That(view.lineSegments.Count(r => r.gameObject.activeInHierarchy), Is.EqualTo(3));
                AssertArtVector(view.lineSegments[0].anchoredPosition, from, "Elbow start");
                AssertArtVector(view.lineSegments[2].anchoredPosition + (Vector2)(view.lineSegments[2].localRotation * Vector3.right) * view.lineSegments[2].rect.width, to, "Elbow end");
                AssertArtVector(view.icon.rectTransform.anchoredPosition, to, "Arrow follows actual target");
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(view.icon.rectTransform.localEulerAngles.z, 180)), Is.LessThan(.01f));
                view.ResetForPool(); Assert.That(view.lineSegments.All(r => !r.gameObject.activeSelf), Is.True);
                from = new Vector2(15, -30); to = new Vector2(115, -30);
                view.Bind("copy-B", "", "共享抽中 ×1", "新右邻实例"); view.SetLink(from, to); view.Play(Vector3.zero); view.Evaluate(.4f);
                Assert.That(view.BoundInstanceId, Is.EqualTo("copy-B"));
                Assert.That(view.lineSegments.Count(r => r.gameObject.activeInHierarchy), Is.EqualTo(1));
                AssertArtVector(view.lineSegments[0].anchoredPosition, from, "Straight start");
                Assert.That(view.lineSegments[0].rect.width, Is.EqualTo(100).Within(.001f));
                AssertArtVector(view.icon.rectTransform.anchoredPosition, to, "Straight endpoint");
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(view.icon.rectTransform.localEulerAngles.z, 0)), Is.LessThan(.01f));
                yield return null;
            }
            finally { UObj.Destroy(view.gameObject); }
        }

        [UnityTest]
        public IEnumerator PLAY_ART07_PooledTicketRealClockPauseResumeAndCancelNeverInvokeAnOldReceiptCallback()
        {
            var view = ArtLibraryClone("fx_value_ticket");
            try
            {
                int finished = 0, stale = 0, current = 0; view.autoTick = true;
                view.Bind("receipt-A", "", "+123456789012345678901234567890", "基础贡献");
                view.Play(new Vector3(200, -40, 0), false, 2, () => finished++);
                yield return null; view.SetPaused(true);
                float heldTime = view.NormalizedTime; Vector3 heldPosition = view.visualRoot.anchoredPosition3D;
                yield return new WaitForSecondsRealtime(.08f);
                Assert.That(view.NormalizedTime, Is.EqualTo(heldTime)); AssertArtVector(view.visualRoot.anchoredPosition3D, heldPosition, "Paused ticket must not drift");
                Assert.That(finished, Is.Zero); view.SetPaused(false);
                double deadline = Time.realtimeSinceStartupAsDouble + 1;
                while (view.NormalizedTime <= heldTime && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(view.NormalizedTime, Is.GreaterThan(heldTime));
                view.Complete(); view.Complete(); Assert.That(finished, Is.EqualTo(1), "Complete is idempotent for one receipt.");
                view.Play(Vector3.zero, false, 2, () => stale++); view.ResetForPool();
                view.visualRoot.anchoredPosition3D = new Vector3(38, -26, 0);
                view.Bind("receipt-B", "", "×0.5", "新来源");
                view.Play(new Vector3(80, -20, 0), false, .04f, () => current++);
                AssertArtVector(view.visualRoot.anchoredPosition3D, new Vector3(38, -26, 0), "A re-rented ticket starts from its new location");
                deadline = Time.realtimeSinceStartupAsDouble + 1;
                while (view.IsPlaying && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(view.IsPlaying, Is.False); Assert.That(current, Is.EqualTo(1)); Assert.That(stale, Is.Zero);
                Assert.That(view.value.text, Is.EqualTo("×0.5"), "Presentation cannot interpolate or normalize the exact operand string.");
            }
            finally { UObj.Destroy(view.gameObject); }
        }

        [UnityTest]
        public IEnumerator PLAY_ART07_ExactScoreAndZeroProbabilitySurvivePresentationAndClearOnPoolReturn()
        {
            var score = ArtLibraryClone("ui_score_breakdown"); var probability = ArtLibraryClone("ui_probability_bar");
            try
            {
                const string exact = "100000000000000000000000000000000000000000000000000000000000000000000000000000000";
                score.Bind("submission-A", "三个对子", exact, "精确分项");
                foreach (var key in score.fieldKeys) score.SetField(key, key == "controllers" ? "×1/4" : exact);
                score.Play(Vector3.zero); score.Evaluate(.73f); score.Complete();
                Assert.That(score.value.text, Is.EqualTo(exact));
                Assert.That(score.fieldValues[Array.IndexOf(score.fieldKeys, "controllers")].text, Is.EqualTo("×1/4"));
                probability.Bind("die-A", "有效点数", "37%", ""); probability.SetFraction(.37f);
                probability.Play(Vector3.zero); probability.Evaluate(.73f); probability.Complete();
                Assert.That(probability.fill.fillAmount, Is.EqualTo(.37f).Within(.0001f));
                probability.SetFraction(0); Assert.That(probability.fill.fillAmount, Is.Zero, "Impossible outcomes must not have a minimum visible fill.");
                score.ResetForPool(); probability.ResetForPool();
                Assert.That(score.value.text, Is.Empty); Assert.That(score.fieldValues.All(t => t.text == ""), Is.True);
                Assert.That(probability.fill.fillAmount, Is.Zero); Assert.That(probability.BoundInstanceId, Is.Null);
                yield return null;
            }
            finally { UObj.Destroy(score.gameObject); UObj.Destroy(probability.gameObject); }
        }

        [UnityTest]
        public IEnumerator PLAY_ART07_DieViewCommittedFaceBindingsAndGlassMaterialResetWithoutChangingSharedAssets()
        {
            var view = ArtLibraryClone("dice_view");
            try
            {
                Assert.That(view.physicalFaces, Has.Length.EqualTo(6));
                var originalMeshes = view.physicalFaces.Select(f => f.sharedMesh).ToArray();
                var originalMaterial = view.diceBody.sharedMaterial; Quaternion originalPose = view.faceOrientation.localRotation;
                var swapped = originalMeshes.Reverse().ToArray(); Quaternion committed = Quaternion.Euler(0, 90, 90);
                view.BindDie("die-A", swapped, committed, app.table.glassMaterial);
                CollectionAssert.AreEqual(swapped, view.physicalFaces.Select(f => f.sharedMesh));
                Assert.That(view.diceBody.sharedMaterial, Is.SameAs(app.table.glassMaterial));
                Assert.That(Quaternion.Angle(view.faceOrientation.localRotation, committed), Is.LessThan(.001f));
                view.ResetForPool();
                CollectionAssert.AreEqual(originalMeshes, view.physicalFaces.Select(f => f.sharedMesh));
                Assert.That(view.diceBody.sharedMaterial, Is.SameAs(originalMaterial));
                Assert.That(Quaternion.Angle(view.faceOrientation.localRotation, originalPose), Is.LessThan(.001f));
                Assert.That(view.BoundInstanceId, Is.Null);
                var source = ArtLibraryReferences().Single(v => v.assetId == "dice_view");
                CollectionAssert.AreEqual(originalMeshes, source.physicalFaces.Select(f => f.sharedMesh), "Binding a clone must not mutate the shared prefab.");
                yield return null;
            }
            finally { UObj.Destroy(view.gameObject); }
        }
    }
}
