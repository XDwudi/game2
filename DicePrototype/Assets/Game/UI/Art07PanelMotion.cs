using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DiceCraft
{
    /// <summary>UI-only fade. Close tails contain draw meshes, never live buttons or business callbacks.</summary>
    public sealed class Art07PanelMotion : MonoBehaviour
    {
        private sealed class TailMesh
        {
            public RectTransform rect;
            public CanvasRenderer renderer;
            public Mesh mesh;
        }
        private readonly List<TailMesh> tailMeshes = new List<TailMesh>();
        private readonly List<Vector4> copiedUvs = new List<Vector4>();
        private CanvasGroup group, previewVeil;
        private float elapsed, duration, previewDelay, previewFade;
        private bool playing, exitTail;
        public bool IsPlaying => playing;
        public bool IsExitTail => exitTail;
        public float Elapsed => elapsed;
        public float Duration => duration;
        public float Alpha => group != null ? group.alpha : 1;
        public float ExitDuration { get; private set; } = .12f;
        public int EnterSequence { get; private set; }
        public int CapturedGraphicCount { get; private set; }

        public void Enter(float seconds, float exitSeconds, bool fresh, bool reducedMotion)
        {
            EnsureGroup();
            exitTail = false; ExitDuration = exitSeconds; group.interactable = true; group.blocksRaycasts = true;
            if (!fresh) { if (!playing) group.alpha = 1; return; }
            EnterSequence++; elapsed = 0; duration = reducedMotion ? Mathf.Min(.08f, seconds) : seconds;
            playing = duration > 0; group.alpha = playing ? 0 : 1;
        }
        public void SetPreviewReveal(CanvasGroup veil, float delay, float seconds, bool fresh, bool reducedMotion)
        {
            previewVeil = veil; previewDelay = reducedMotion ? 0 : delay; previewFade = reducedMotion ? .08f : seconds;
            if (veil == null) return;
            veil.interactable = false; veil.blocksRaycasts = false; veil.ignoreParentGroups = true;
            veil.alpha = !fresh && !playing ? 0 : 1 - Mathf.Clamp01((elapsed - previewDelay) / Mathf.Max(.001f, previewFade));
        }
        private void Update()
        {
            if (!playing) return;
            elapsed += Time.unscaledDeltaTime; float t = Mathf.Clamp01(elapsed / Mathf.Max(.001f, duration));
            group.alpha = exitTail ? 1 - t : 1 - (1 - t) * (1 - t);
            if (previewVeil != null) previewVeil.alpha = 1 - Mathf.Clamp01((elapsed - previewDelay) / Mathf.Max(.001f, previewFade));
            if (t >= 1)
            {
                playing = false; if (previewVeil != null) previewVeil.alpha = 0;
                if (exitTail) CancelTail();
            }
        }
        public void CancelTail()
        {
            if (!exitTail) return;
            playing = false; CapturedGraphicCount = 0;
            foreach (var item in tailMeshes) { item.renderer.Clear(); item.rect.gameObject.SetActive(false); }
            gameObject.SetActive(false);
        }
        public void CaptureExit(RectTransform panel, float seconds, bool reducedMotion)
        {
            if (panel == null || !panel.gameObject.activeInHierarchy) return;
            CancelTail(); exitTail = true; previewVeil = null;
            EnsureGroup();
            group.interactable = false; group.blocksRaycasts = false; group.alpha = 1;
            gameObject.SetActive(true); transform.SetAsLastSibling(); Canvas.ForceUpdateCanvases();
            int count = 0; Matrix4x4 worldToTail = transform.worldToLocalMatrix;
            var canvas = GetComponentInParent<Canvas>().rootCanvas;
            foreach (var graphic in panel.GetComponentsInChildren<Graphic>(false))
            {
                if (!graphic.isActiveAndEnabled || graphic.canvasRenderer.cull || graphic.canvasRenderer.GetInheritedAlpha() <= 0) continue;
                var ownMask = graphic.GetComponent<Mask>(); if (ownMask != null && ownMask.isActiveAndEnabled && !ownMask.showMaskGraphic) continue;
                var source = graphic.canvasRenderer.GetMesh(); if (source == null || source.vertexCount == 0) continue;
                var entry = GetTailMesh(count); entry.mesh.Clear();
                entry.mesh.vertices = source.vertices; entry.mesh.colors32 = source.colors32;
                // TMP stores its SDF scale in uv0.w. The Vector2 Mesh.uv accessor would erase it.
                for(int channel=0;channel<4;channel++){copiedUvs.Clear();source.GetUVs(channel,copiedUvs);entry.mesh.SetUVs(channel,copiedUvs);}
                entry.mesh.normals = source.normals; entry.mesh.tangents = source.tangents; entry.mesh.triangles = source.triangles;
                var vertices = entry.mesh.vertices; var matrix = worldToTail * graphic.transform.localToWorldMatrix;
                for (int i = 0; i < vertices.Length; i++) vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
                entry.mesh.vertices = vertices; entry.mesh.RecalculateBounds();
                // Use original non-stencil material: the live Mask hierarchy is about to close.
                // TMP's Graphic.material may still be the uGUI default; its rendered glyphs use fontSharedMaterial.
                var material=graphic is TMPro.TMP_Text text?text.fontSharedMaterial:graphic.material;
                entry.renderer.materialCount=1;entry.renderer.SetMaterial(material,0);entry.renderer.SetTexture(graphic.mainTexture);
                Color tint = graphic.canvasRenderer.GetColor(); tint.a *= graphic.canvasRenderer.GetInheritedAlpha(); entry.renderer.SetColor(tint);
                bool clipped = false; Rect clip = default;
                foreach (var mask in graphic.GetComponentsInParent<Mask>(false))
                    if (mask.isActiveAndEnabled && graphic.transform != mask.transform) IntersectClip(mask.rectTransform, canvas.transform, ref clipped, ref clip);
                foreach (var mask in graphic.GetComponentsInParent<RectMask2D>(false))
                    if (mask.isActiveAndEnabled && graphic.transform != mask.transform) IntersectClip(mask.rectTransform, canvas.transform, ref clipped, ref clip);
                if (clipped) entry.renderer.EnableRectClipping(clip); else entry.renderer.DisableRectClipping();
                entry.renderer.SetMesh(entry.mesh); entry.rect.gameObject.SetActive(true); count++;
            }
            CapturedGraphicCount = count; elapsed = 0; duration = reducedMotion ? Mathf.Min(.08f, seconds) : seconds; playing = count > 0 && duration > 0;
            if (!playing) CancelTail();
        }
        private TailMesh GetTailMesh(int index)
        {
            if (index < tailMeshes.Count) return tailMeshes[index];
            var go = new GameObject("ModalTailMesh" + index, typeof(RectTransform), typeof(CanvasRenderer)); var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = Vector2.zero;
            var item = new TailMesh { rect = rect, renderer = go.GetComponent<CanvasRenderer>(), mesh = new Mesh { name = "Art07 modal tail mesh " + index } };
            item.mesh.MarkDynamic(); tailMeshes.Add(item); return item;
        }
        private void EnsureGroup()
        {
            if(group!=null)return;
            group=GetComponent<CanvasGroup>();
            if(group==null)group=gameObject.AddComponent<CanvasGroup>();
        }
        private static void IntersectClip(RectTransform mask, Transform canvas, ref bool clipped, ref Rect result)
        {
            var corners = new Vector3[4]; mask.GetWorldCorners(corners);
            Vector3 low = canvas.InverseTransformPoint(corners[0]), high = low;
            for (int i = 1; i < corners.Length; i++) { Vector3 p = canvas.InverseTransformPoint(corners[i]); low = Vector3.Min(low, p); high = Vector3.Max(high, p); }
            var current = Rect.MinMaxRect(low.x, low.y, high.x, high.y);
            if (!clipped) { result = current; clipped = true; }
            else result = Rect.MinMaxRect(Mathf.Max(result.xMin, current.xMin), Mathf.Max(result.yMin, current.yMin), Mathf.Min(result.xMax, current.xMax), Mathf.Min(result.yMax, current.yMax));
        }
        private void OnDestroy() { foreach (var entry in tailMeshes) if (entry.mesh != null) Destroy(entry.mesh); }
    }
}
