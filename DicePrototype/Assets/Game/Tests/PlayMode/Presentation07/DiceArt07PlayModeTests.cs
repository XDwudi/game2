using System.Collections;
using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UObj = UnityEngine.Object;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [UnityTest] public IEnumerator PLAY_ART07_DiceLockAndRetargetMoveWithoutTeleportAndPendingLiftKeepsIdentity()
        {
            var die = DiceFactory.CreateStandard(app.Config, 981);
            var result = new DieHandState { dieId = die.dieId, currentFaceId = "F01", currentPip = 1, currentColor = "red", hasRolled = true };
            app.table.ReducedMotion = false; UiKit.ReducedMotion = false;
            app.table.Display(die, result, new Vector2(700, 540)); app.table.FinishRoll();
            var orientation = app.table.GetFaceOrientation(die.dieId); var root = orientation.parent.parent;
            Vector3 start = root.position; result.isLocked = true;
            app.table.Display(die, result, new Vector2(1100, 820), .96f);
            Assert.That(root.position, Is.EqualTo(start)); Assert.That(app.table.IsTransitioning, Is.True);
            yield return null;
            Assert.That((root.position - start).sqrMagnitude, Is.GreaterThan(.000001f));
            Vector3 interrupted = root.position;
            app.table.Display(die, result, new Vector2(800, 820), .96f);
            Assert.That(root.position, Is.EqualTo(interrupted), "Retargeting preserves the actually displayed pose.");
            yield return new WaitForSecondsRealtime(.23f);
            Assert.That(app.table.IsTransitioning, Is.False);
            Assert.That(app.table.GetFaceOrientation(die.dieId), Is.SameAs(orientation));
            Vector3 resting = root.position; result.isLocked = false;
            app.table.Display(die, result, new Vector2(800, 820), .96f, true);
            yield return new WaitForSecondsRealtime(.23f);
            float screenDelta = app.table.viewCamera.WorldToScreenPoint(root.position).y - app.table.viewCamera.WorldToScreenPoint(resting).y;
            float expected = app.table.designRoot != null ? Mathf.Abs(app.table.designRoot.TransformVector(new Vector3(0, 6, 0)).y) : 6f * Screen.height / 1080f;
            Assert.That(screenDelta, Is.EqualTo(expected).Within(.05f)); Assert.That(die.faces[0].permanentPip, Is.EqualTo(1));
            app.table.ReducedMotion = true; app.table.Display(die, result, new Vector2(1200, 540), 1.18f, false);
            Assert.That(app.table.IsTransitioning, Is.False); app.table.ReducedMotion = false;
        }

        [UnityTest] public IEnumerator PLAY_ART07_BothRollSpeedsFixPhysicalFaceForFinal120Milliseconds()
        {
            var die = DiceFactory.CreateStandard(app.Config, 982); var lockedDie = DiceFactory.CreateStandard(app.Config, 983);
            var locked = new DieHandState { dieId = lockedDie.dieId, currentFaceId = "F01", currentPip = 1, currentColor = "red", hasRolled = true, isLocked = true };
            app.table.ReducedMotion = false; UiKit.ReducedMotion = false;
            foreach (float duration in new[] { .9f, .15f })
            {
                app.table.Display(lockedDie, locked, new Vector2(1200, 540));
                var hand = new DieHandState { dieId = die.dieId, currentFaceId = "F02", currentPip = 2, currentColor = "blue", hasRolled = true };
                app.table.Display(die, hand, new Vector2(850, 540)); app.table.FinishRoll();
                Quaternion lockedPose = app.table.GetFaceOrientation(lockedDie.dieId).rotation;
                hand.currentFaceId = "F06"; hand.currentPip = 6;
                app.table.Display(die, hand, new Vector2(850, 540)); app.table.BeginRoll(duration);
                double deadline = Time.realtimeSinceStartupAsDouble + duration + 2;
                int heldFrames = 0;
                while (app.table.IsRolling && Time.realtimeSinceStartupAsDouble < deadline)
                {
                    if (app.table.RollTimeRemaining <= .12f)
                    {
                        var orientation = app.table.GetFaceOrientation(die.dieId);
                        Assert.That(Vector3.Dot(orientation.TransformDirection(DiceTable.Normals[5]), Vector3.up), Is.GreaterThan(.9999f));
                        Assert.That(Quaternion.Angle(orientation.rotation, Quaternion.AngleAxis(18, Vector3.up) * Quaternion.FromToRotation(DiceTable.Normals[5], Vector3.up)), Is.LessThan(.02f));
                        heldFrames++;
                    }
                    Assert.That(Quaternion.Angle(app.table.GetFaceOrientation(lockedDie.dieId).rotation, lockedPose), Is.LessThan(.001f));
                    yield return null;
                }
                Assert.That(app.table.IsRolling, Is.False); Assert.That(heldFrames, Is.GreaterThan(0), "The final hold must span actual rendered frames.");
            }
        }

        [UnityTest] public IEnumerator PLAY_ART07_GlassBackFacesHiddenAndGpuShellTransparentWhilePipsStayOpaque()
        {
            var die = DiceFactory.CreateStandard(app.Config, 984); die.isGlass = true;
            var hand = new DieHandState { dieId = die.dieId, currentFaceId = "F01", currentPip = 1, currentColor = "red", hasRolled = true, isLocked = true };
            app.table.Display(die, hand, new Vector2(960, 540), 1); app.table.FinishRoll();
            var orientation = app.table.GetFaceOrientation(die.dieId); var root = orientation.parent.parent;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
            Assert.That(orientation.Find("Face_F06").GetComponent<MeshRenderer>().enabled, Is.False);
            Assert.That(orientation.Find("Face_F01").GetComponent<MeshRenderer>().enabled, Is.True);
            var pipMesh = orientation.Find("Face_F01").GetComponent<MeshFilter>().sharedMesh;
            var tintablePips = pipMesh.vertices.Zip(pipMesh.colors, (v, c) => new { vertex=v, color=c })
                .Where(p => Mathf.Approximately(p.color.r,p.color.g) && Mathf.Approximately(p.color.g,p.color.b) && p.color.r>0).ToArray();
            Assert.That(tintablePips, Is.Not.Empty, "The rule-colored enamel mesh must contain actual tintable vertices; a zero-match scan cannot pass.");
            Assert.That(tintablePips.All(p => p.vertex.z>0 && p.vertex.z<.01f), Is.True, "Every tintable pip must retain a shallow positive depth offset over its face, independently of its shading color.");
            var cameraObject = new GameObject("Art07GlassCapture"); var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = .9f; camera.aspect = 1;
            camera.transform.position = root.position + new Vector3(0, 3, -1.73205f); camera.transform.LookAt(root.position);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.cullingMask = 1 << 31; camera.nearClipPlane = .1f; camera.farClipPlane = 10;
            var target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear); target.Create();
            var pixels = new Texture2D(128, 128, TextureFormat.RGBA32, false, true); var originalActive = RenderTexture.active;
            Color Capture(Vector3 localPoint, Color background)
            {
                camera.backgroundColor = background;
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); pixels.Apply(false, false);
                Vector3 uv = camera.WorldToViewportPoint(orientation.TransformPoint(localPoint));
                return pixels.GetPixel(Mathf.Clamp(Mathf.RoundToInt(uv.x * 128), 0, 127), Mathf.Clamp(Mathf.RoundToInt(uv.y * 128), 0, 127));
            }
            try
            {
                yield return null;
                var red = new Color(.9f, .05f, .1f, 1); var blue = new Color(.05f, .1f, .9f, 1);
                Color shellA = Capture(new Vector3(.15f, .5f, .15f), red), shellB = Capture(new Vector3(.15f, .5f, .15f), blue);
                Assert.That(Mathf.Abs(shellA.r - shellB.r) + Mathf.Abs(shellA.b - shellB.b), Is.GreaterThan(.15f), "Glass shell must reveal different backgrounds in actual GPU output.");
                Color pipA = Capture(new Vector3(0, .503f, 0), red), pipB = Capture(new Vector3(0, .503f, 0), blue);
                Assert.That(Mathf.Abs(pipA.r - pipB.r) + Mathf.Abs(pipA.g - pipB.g) + Mathf.Abs(pipA.b - pipB.b), Is.LessThan(.03f), "Opaque red pip must not be background-tinted through glass.");
            }
            finally
            {
                RenderTexture.active = originalActive; target.Release(); UObj.Destroy(target); UObj.Destroy(pixels); UObj.Destroy(cameraObject);
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
            }
        }
    }
}
