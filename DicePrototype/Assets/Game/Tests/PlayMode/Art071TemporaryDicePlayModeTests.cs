using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UObj = UnityEngine.Object;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [UnityTest]
        public IEnumerator PLAY_ART071_TemporaryBodyRealPoolReuseRestoresOrdinaryCoverageAndSharedMaterial()
        {
            var host = new GameObject("TemporaryBodyReuse071"); var table = host.AddComponent<DiceTable>();
            table.viewCamera = app.table.viewCamera; table.designRoot = app.table.designRoot;
            table.bodyMesh = app.table.bodyMesh; table.bodyMaterial = app.table.bodyMaterial;
            table.glassMaterial = app.table.glassMaterial; table.faceMaterial = app.table.faceMaterial; table.outlineMaterial = app.table.outlineMaterial;
            table.ReducedMotion = true;
            var die = DiceFactory.CreateStandard(app.Config, 9101); die.lifetime = "stage_temporary";
            var hand = new DieHandState { dieId = die.dieId, currentFaceId = "F01", currentPip = 1, currentColor = "red", hasRolled = true };
            var block = new MaterialPropertyBlock(); Material shared = app.table.bodyMaterial;
            try
            {
                table.Display(die, hand, new Vector2(960, 540)); table.FinishRoll();
                var pose = table.GetFaceOrientation(die.dieId); var body = pose.Find("Body").GetComponent<MeshRenderer>();
                var identity = body.GetEntityId(); body.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_ShadowCoverage071"), Is.EqualTo(.5f)); Assert.That(body.sharedMaterial, Is.SameAs(shared));
                die.lifetime = "permanent"; table.Display(die, hand, new Vector2(960, 540)); body.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_ShadowCoverage071"), Is.EqualTo(1f)); Assert.That(body.GetEntityId(), Is.EqualTo(identity));
                die.lifetime = "stage_temporary"; table.Display(die, hand, new Vector2(960, 540));
                for (int i = 0; i < 3; i++)
                {
                    table.Hide(); yield return null; // Real DiceTable.LateUpdate puts the inactive Visual into its spare stack.
                    Assert.That(table.GetFaceOrientation(die.dieId), Is.Null); Assert.That(table.PooledVisualCount, Is.EqualTo(1));
                    die = DiceFactory.CreateStandard(app.Config, 9102 + i); hand.dieId = die.dieId;
                    table.Display(die, hand, new Vector2(960, 540));
                    var reused = table.GetFaceOrientation(die.dieId).Find("Body").GetComponent<MeshRenderer>(); reused.GetPropertyBlock(block);
                    Assert.That(reused.GetEntityId(), Is.EqualTo(identity), "Exercise actual reused renderer, not a fresh scene or new Visual.");
                    Assert.That(reused.sharedMaterial, Is.SameAs(shared)); Assert.That(block.GetFloat("_ShadowCoverage071"), Is.EqualTo(1f));
                    Assert.That(table.GetComponentsInChildren<MeshRenderer>(true).Length, Is.EqualTo(8));
                    die.lifetime = "stage_temporary"; table.Display(die, hand, new Vector2(960, 540)); reused.GetPropertyBlock(block);
                    Assert.That(block.GetFloat("_ShadowCoverage071"), Is.EqualTo(.5f));
                }
                TestContext.WriteLine("TEMP071_REUSE actual renderer=" + identity + "; runtime=" + typeof(DiceTable).Module.ModuleVersionId);
            }
            finally { UObj.Destroy(host); }
        }

        [UnityTest]
        public IEnumerator PLAY_ART071_TemporaryClock150ActualResolutionTenPoolAndEightLockedRemainVisible()
        {
            Assert.That((Screen.width == 1920 && Screen.height == 1080) || (Screen.width == 1280 && Screen.height == 720), Is.True,
                "Run this same case once in each actual 1920x1080 and 1280x720 GameView; no synthetic resize counts.");
            yield return ExtraBegin07();
            foreach (bool colorAssist in new[] { true, false })
            {
                yield return DiceLabelBegin07(150); DiceLabelApply07(10, true);
                SettlementField07("colorAssist07", colorAssist); app.Refresh(); yield return WaitInteractionLayout07();
                string suffix = Screen.width + "x" + Screen.height + (colorAssist ? "-color" : "-no-color");
                if (colorAssist) DiceLabelCheck07();
                yield return Temp071ClockFrame("temporary-ten-pool-" + suffix);
                foreach (var die in app.Session.State.dice.Take(8)) ClickDie(die.dieId);
                Click("Confirm"); yield return WaitForIdle(); yield return WaitInteractionLayout07();
                Assert.That(app.Session.State.hand.orderedLockedIds.Count, Is.EqualTo(8));
                if (colorAssist) DiceLabelCheck07();
                yield return Temp071ClockFrame("temporary-eight-locked-" + suffix);
                int clocks = app.canvas.GetComponentsInChildren<Art071TemporaryClock>(true).Length;
                for (int i = 0; i < 3; i++) { app.Refresh(); yield return null; }
                Assert.That(app.canvas.GetComponentsInChildren<Art071TemporaryClock>(true).Length, Is.EqualTo(clocks), "Refresh must reuse clock nodes.");
            }
            ExtraFinish07();
        }

        private IEnumerator Temp071ClockFrame(string scope)
        {
            yield return ExtraCapture07(scope, "S05", "Actual temporary clocks, ten active dice/eight slots at 150%; both color-assist states. Pending independent visual review.");
            var ids = app.canvas.GetComponentsInChildren<DiePointer>(false).Where(p => p.name.StartsWith("Die_")).Select(p => p.name.Substring(4)).ToArray();
            var temporary = ids.Where(id => app.Session.State.dice.Single(d => d.dieId == id).lifetime == "stage_temporary").ToArray();
            Assert.That(temporary, Is.Not.Empty, "A zero-temporary fixture cannot verify clock visibility.");
            var clocks = temporary.Select(id => FindActive<TextMeshProUGUI>("DieState" + id).GetComponentInChildren<Art071TemporaryClock>(true)).ToArray();
            Temp071CheckClocks(clocks, scope);
            foreach (string id in ids.Except(temporary))
                Assert.That(FindActive<TextMeshProUGUI>("DieState" + id).GetComponentsInChildren<Art071TemporaryClock>(false), Is.Empty, "Ordinary die must not retain a pooled clock.");
            yield return Temp071ClockGpu(scope, clocks);
        }

        private void Temp071CheckClocks(Art071TemporaryClock[] clocks, string scope)
        {
            Canvas.ForceUpdateCanvases();
            var text = app.canvas.GetComponentsInChildren<TextMeshProUGUI>(false)
                .Where(t => t.name.StartsWith("DieState") || t.name.StartsWith("SlotIndex") || t.name.StartsWith("PendingD") || ((t.name.StartsWith("PermanentStamp_") || t.name.StartsWith("PermanentPrefix_")) && t.alpha > .01f)).ToArray();
            var glyphs = text.SelectMany(DiceLabelVisibleGlyphRects07).ToArray();
            var bodies = app.canvas.GetComponentsInChildren<DiePointer>(false).Where(p => p.name.StartsWith("Die_"))
                .SelectMany(p => app.table.GetFaceOrientation(p.name.Substring(4)).GetComponentsInChildren<Renderer>(false))
                .Where(r => r.enabled).Select(DiceLabelRendererRect07).ToArray();
            foreach (var clock in clocks)
            {
                Assert.That(clock, Is.Not.Null, scope); Assert.That(clock.isActiveAndEnabled, Is.True, scope);
                Assert.That(clock.LastPlacementSucceeded, Is.True, scope); Assert.That(clock.raycastTarget, Is.False);
                Assert.That(clock.canvasRenderer.cull, Is.False); Assert.That(clock.canvasRenderer.GetInheritedAlpha(), Is.GreaterThan(0));
                Assert.That(clock.canvasRenderer.GetMesh(), Is.Not.Null); Assert.That(clock.canvasRenderer.GetMesh().vertexCount, Is.GreaterThan(0));
                Rect bounds = V06ScreenRect(clock.rectTransform);
                Assert.That(ScoreContains07(Screen.safeArea, bounds), Is.True, scope);
                Assert.That(bounds.width, Is.GreaterThanOrEqualTo(12 * app.canvas.scaleFactor - .01f));
                foreach (Rect glyph in glyphs)
                {
                    float gap = 3 * app.canvas.scaleFactor;
                    Rect padded = Rect.MinMaxRect(glyph.xMin - gap, glyph.yMin - gap, glyph.xMax + gap, glyph.yMax + gap);
                    Assert.That(bounds.Overlaps(padded), Is.False, scope + " clock lacks its 3-design-pixel glyph clearance");
                }
                foreach (Rect body in bodies) Assert.That(bounds.Overlaps(body), Is.False, scope + " clock covers projected physical die face");
                foreach (var mark in app.canvas.GetComponentsInChildren<RectTransform>(false).Where(r => r.name.StartsWith("LockMark")))
                    Assert.That(bounds.Overlaps(V06ScreenRect(mark)), Is.False, scope + " clock covers lock mark");
                foreach (var edge in app.canvas.GetComponentsInChildren<UnityEngine.UI.Image>(false))
                    if (edge.transform.parent != null && edge.transform.parent.name.StartsWith("Slot") && int.TryParse(edge.transform.parent.name.Substring(4), out _))
                        Assert.That(bounds.Overlaps(V06ScreenRect(edge.rectTransform)), Is.False, scope + " clock covers slot border");
            }
        }

        private IEnumerator Temp071ClockGpu(string scope, Art071TemporaryClock[] clocks)
        {
            Texture2D hidden = null, visible = null;
            try
            {
                foreach (var clock in clocks) clock.enabled = false;
                yield return new WaitForEndOfFrame(); hidden = ScreenCapture.CaptureScreenshotAsTexture();
                foreach (var clock in clocks) clock.enabled = true;
                Canvas.ForceUpdateCanvases(); yield return new WaitForEndOfFrame(); visible = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(extra07.output, scope + "-clocks-hidden.png"), hidden.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(extra07.output, scope + "-clocks-visible.png"), visible.EncodeToPNG());
                var a = hidden.GetPixels32(); var b = visible.GetPixels32();
                foreach (var clock in clocks)
                {
                    Rect r = V06ScreenRect(clock.rectTransform); int ink = 0;
                    for (int y = Mathf.Max(0, Mathf.FloorToInt(r.yMin)); y < Mathf.Min(Screen.height, Mathf.CeilToInt(r.yMax)); y++)
                        for (int x = Mathf.Max(0, Mathf.FloorToInt(r.xMin)); x < Mathf.Min(Screen.width, Mathf.CeilToInt(r.xMax)); x++)
                        { int i = y * Screen.width + x; if (Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b) > 6) ink++; }
                    Assert.That(ink, Is.GreaterThan(2), scope + " actual clock has no GPU ink: " + clock.transform.parent.name);
                    TestContext.WriteLine(scope + " " + clock.transform.parent.name + " actual clock ink pixels=" + ink + " screen=" + r);
                }
            }
            finally { foreach (var clock in clocks) if (clock != null) clock.enabled = true; if (hidden != null) UObj.Destroy(hidden); if (visible != null) UObj.Destroy(visible); }
        }

        [UnityTest]
        public IEnumerator PLAY_ART071_TemporaryShadowActualGpuSamePoseHasVisibleLighterContactShadow()
        {
            yield return ExtraBegin07();
            bool appEnabled = app.enabled; app.enabled = false; app.table.Hide();
            var die = DiceFactory.CreateStandard(app.Config, 9190);
            var hand = new DieHandState { dieId = die.dieId, currentFaceId = "F01", currentPip = 1, currentColor = "red", hasRolled = true };
            app.table.Display(die, hand, new Vector2(960, 540), 1.4f); app.table.FinishRoll();
            var body = app.table.GetFaceOrientation(die.dieId).Find("Body").GetComponent<MeshRenderer>();
            Material shared = body.sharedMaterial; var originalCast = body.shadowCastingMode;
            var camera = app.table.viewCamera; var target = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            target.Create(); var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false, true); var active = RenderTexture.active;
            string shaderPath = Path.Combine(Application.dataPath, "Game/Runtime/Shaders/Art07Ceramic.shader"), shaderHash = ExtraFileHash07(shaderPath);
            Color32[] Capture(string name)
            {
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply(false, false);
                File.WriteAllBytes(Path.Combine(extra07.output, name + ".png"), texture.EncodeToPNG()); return texture.GetPixels32();
            }
            try
            {
                yield return null;
                body.shadowCastingMode = ShadowCastingMode.Off; var withoutA = Capture("temporary-shadow-no-caster-a"); var withoutB = Capture("temporary-shadow-no-caster-b");
                body.shadowCastingMode = ShadowCastingMode.On; var ordinary = Capture("temporary-shadow-ordinary");
                die.lifetime = "stage_temporary"; app.table.Display(die, hand, new Vector2(960, 540), 1.4f); app.table.FinishRoll();
                var temporary = Capture("temporary-shadow-stage-temporary");
                int shadowPixels = 0, changingBackground = 0; long ordinaryDark = 0, temporaryDark = 0;
                for (int i = 0; i < ordinary.Length; i++)
                {
                    int noise = Math.Abs(withoutA[i].r - withoutB[i].r) + Math.Abs(withoutA[i].g - withoutB[i].g) + Math.Abs(withoutA[i].b - withoutB[i].b);
                    if (noise > 1) changingBackground++;
                    int full = withoutA[i].r + withoutA[i].g + withoutA[i].b - ordinary[i].r - ordinary[i].g - ordinary[i].b;
                    if (full <= 3) continue; shadowPixels++; ordinaryDark += full;
                    temporaryDark += Math.Max(0, withoutA[i].r + withoutA[i].g + withoutA[i].b - temporary[i].r - temporary[i].g - temporary[i].b);
                }
                var report = new TemporaryShadowGpu071 { width = target.width, height = target.height, shadowPixels = shadowPixels,
                    changingBackgroundPixels = changingBackground, ordinaryDarkness = ordinaryDark, temporaryDarkness = temporaryDark,
                    shaderSha256 = shaderHash, runtimeMvid = typeof(GameApp).Module.ModuleVersionId.ToString(), graphicsApi = SystemInfo.graphicsDeviceType.ToString() };
                File.WriteAllText(Path.Combine(extra07.output, "temporary-shadow-gpu.json"), JsonUtility.ToJson(report, true));
                Assert.That(changingBackground, Is.Zero, "Same-pose no-caster controls must be stable to isolate shadows.");
                Assert.That(shadowPixels, Is.GreaterThan(8), "Ordinary shadow must be visible in actual GPU output, not merely coverage=1.");
                Assert.That(temporaryDark, Is.GreaterThan(0).And.LessThan(ordinaryDark), "Actual temporary contact shadow must remain visible and lighter.");
                Assert.That(body.sharedMaterial, Is.SameAs(shared)); Assert.That(ExtraFileHash07(shaderPath), Is.EqualTo(shaderHash));
            }
            finally { body.shadowCastingMode = originalCast; RenderTexture.active = active; target.Release(); UObj.Destroy(target); UObj.Destroy(texture); app.enabled = appEnabled; app.Refresh(); }
            ExtraFinish07();
        }
        [Serializable] private sealed class TemporaryShadowGpu071
        {
            public int width, height, shadowPixels, changingBackgroundPixels;
            public long ordinaryDarkness, temporaryDarkness;
            public string shaderSha256, runtimeMvid, graphicsApi;
            public string scope = "Same actual camera/light/material/pose, only body shadow casting/temporary coverage differs; visual review of softness and pattern still pending.";
        }

        [UnityTest]
        public IEnumerator PLAY_ART071_TemporaryClockRepositionsAfterCommittedPermanentLabelRewrite()
        {
            yield return ExtraBegin07(); SettlementField07("textScale07", 150); UiKit.FontScale = 1.5f;
            var scene = FxScene07(s =>
            {
                FxControllers07(s, "C07", "C36", "C38"); s.remainingSubmissions = 1; s.target = "1"; s.capacityOverride = 3; s.submissionOrdinal = 0;
                s.orderedLockedIds = s.orderedLockedIds.Take(2).ToList(); for (int i = 0; i < s.currentResults.Count; i++) s.currentResults[i].isLocked = i < 2;
                s.dice[0].faces.Single(f => f.faceId == "F06").permanentPip = 3; s.currentResults[0].currentPip = 1; s.currentResults[0].remoteOverride = true;
                s.dice[1].lifetime = "stage_temporary"; s.dice[1].createdStageId = app.Config.stages[0].stageId;
            });
            var submitted = app.Send(new SessionCommand { kind = CommandKind.Submit }); Assert.That(submitted.accepted, Is.True, submitted.error);
            string id = scene.dice[1].dieId;
            Assert.That(submitted.receipt.effects.Any(e => e.kind == "DieCopied" && e.dieId == id), Is.True);
            Assert.That(submitted.receipt.effects.Any(e => e.kind == "FaceValueChanged" && e.dieId == id), Is.True);
            yield return WaitFxPhase07(PresentationPhase.PostScore);
            bool enabled = app.enabled; app.enabled = false;
            try
            {
                var label = FindActive<TextMeshProUGUI>("DieState" + id);
                var stamp = FindActive<TextMeshProUGUI>("PermanentStamp_" + id);
                var prefix = FindActive<TextMeshProUGUI>("PermanentPrefix_" + id);
                Assert.That(stamp.text, Is.EqualTo("永久")); Assert.That(prefix.text, Is.EqualTo(stamp.text));
                Assert.That(stamp.fontSize, Is.EqualTo(14 * UiKit.FontScale).Within(.01f));
                Assert.That(stamp.alpha + prefix.alpha, Is.EqualTo(1).Within(.001f));
                var displayedPrefix = stamp.alpha >= prefix.alpha ? stamp : prefix;
                AssertPostPrefixGeometry07(label, displayedPrefix);
                var current = scene.currentResults.Single(r => r.dieId == id);
                int oldPip = scene.dice[1].faces.Single(f => f.faceId == current.currentFaceId).permanentPip;
                var committedGrowth = submitted.receipt.effects.Last(e => e.kind == "FaceValueChanged" && e.dieId == id && e.faceId == current.currentFaceId);
                string copiedColor = scene.dice[0].faces.Single(f => f.faceId == current.currentFaceId).baseColor == "red" ? "红" : "蓝";
                string flat = new string(label.text.Where(c => !char.IsWhiteSpace(c)).ToArray());
                Assert.That(flat, Is.EqualTo(oldPip + "→" + committedGrowth.newValue + copiedColor), "Validate committed before/copy/growth data and the separate permanent prefix together.");
                var clock = label.GetComponentInChildren<Art071TemporaryClock>(true); Temp071CheckClocks(new[] { clock }, "permanent-rewrite");
                yield return Temp071ClockGpu("temporary-permanent-rewrite", new[] { clock });
            }
            finally { app.enabled = enabled; app.SkipVisual(); }
            ExtraFinish07();
        }
    }
}
