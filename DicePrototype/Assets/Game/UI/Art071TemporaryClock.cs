using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceCraft
{
    /// <summary>Static clock beside existing glyphs; never owns input, time or die state.</summary>
    [RequireComponent(typeof(RectTransform),typeof(CanvasRenderer))]
    public sealed class Art071TemporaryClock : MaskableGraphic
    {
        private readonly List<Rect> glyphs = new List<Rect>(24);
        private readonly List<LineBounds> lines = new List<LineBounds>(4);
        private struct LineBounds { public int number; public Rect bounds; public bool temporary; }
        public bool LastPlacementSucceeded { get; private set; }
        public bool UsedLowerMargin { get; private set; }
        public bool UsedRightGutter { get; private set; }
        public Rect PlacedBoundsInLabel { get; private set; }
        private const float Gap = 3f, Minimum = 12f;

        /// <param name="allowedTopDown">Existing free annotation area, relative to label top-left, y downward.</param>
        public bool PlaceOn(TextMeshProUGUI label, bool temporary, Rect allowedTopDown, float preferredDiameter, Rect? rightGutterTopDown = null)
        {
            raycastTarget = false; LastPlacementSucceeded = false; UsedLowerMargin = false; UsedRightGutter = false;
            PlacedBoundsInLabel = default;
            if (!temporary) { gameObject.SetActive(false); return true; }
            if (label == null) throw new ArgumentNullException(nameof(label));
            label.ForceMeshUpdate(); glyphs.Clear(); lines.Clear();
            var info = label.textInfo;
            int word = label.text.IndexOf("临时", StringComparison.Ordinal);
            for (int i = 0; i < info.characterCount; i++)
            {
                var c = info.characterInfo[i];
                if (!c.isVisible) continue;
                Rect bounds = Rect.MinMaxRect(c.bottomLeft.x, c.bottomLeft.y, c.topRight.x, c.topRight.y);
                glyphs.Add(bounds);
                int lineIndex = lines.FindIndex(l => l.number == c.lineNumber);
                bool isTemporary = word >= 0 && c.index >= word && c.index < word + 2;
                if (lineIndex < 0) lines.Add(new LineBounds { number = c.lineNumber, bounds = bounds, temporary = isTemporary });
                else
                {
                    var line = lines[lineIndex];
                    line.bounds = Rect.MinMaxRect(Mathf.Min(line.bounds.xMin, bounds.xMin), Mathf.Min(line.bounds.yMin, bounds.yMin),
                        Mathf.Max(line.bounds.xMax, bounds.xMax), Mathf.Max(line.bounds.yMax, bounds.yMax));
                    line.temporary |= isTemporary; lines[lineIndex] = line;
                }
            }
            Rect labelRect = label.rectTransform.rect;
            Rect allowed = new Rect(labelRect.xMin + allowedTopDown.x, labelRect.yMax - allowedTopDown.yMax,
                allowedTopDown.width, allowedTopDown.height);
            float diameter = Mathf.Max(Minimum, preferredDiameter);
            // Prefer the literal temporary line, then a shorter existing state line.
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i]; if ((pass == 0) != line.temporary) continue;
                    float available = allowed.xMax - line.bounds.xMax - Gap;
                    float size = Mathf.Min(diameter, available);
                    if (size < Minimum) continue;
                    Rect candidate = new Rect(line.bounds.xMax + Gap, line.bounds.center.y - size * .5f, size, size);
                    if (CanPlace(candidate, allowed)) return Apply(label, candidate, false);
                }
            // Narrow compact glass+temporary labels may occupy their whole only line.
            // Use existing empty slot margin, never change the text, its rect or hit target.
            if (lines.Count > 0)
            {
                var last = lines[0].bounds;
                for (int i = 1; i < lines.Count; i++) if (lines[i].bounds.yMin < last.yMin) last = lines[i].bounds;
                foreach (float requested in new[] { diameter, Minimum })
                {
                    float size = Mathf.Min(requested, allowed.width);
                    float left = Mathf.Clamp(last.xMax - size, allowed.xMin, allowed.xMax - size);
                    Rect candidate = new Rect(left, last.yMin - Gap - size, size, size);
                    if (size >= Minimum && CanPlace(candidate, allowed)) return Apply(label, candidate, true);
                }
            }
            // An existing inter-slot gutter is the final annotation area. Text, slot
            // geometry and pointer bounds remain untouched; no glyph is sacrificed.
            if (rightGutterTopDown.HasValue && lines.Count > 0)
            {
                Rect topDown = rightGutterTopDown.Value;
                Rect gutter = new Rect(labelRect.xMin + topDown.x, labelRect.yMax - topDown.yMax, topDown.width, topDown.height);
                float size = Mathf.Min(diameter, gutter.width, gutter.height);
                if (size >= Minimum)
                {
                    var target = lines[lines.Count - 1];
                    for (int i = 0; i < lines.Count; i++) if (lines[i].temporary) target = lines[i];
                    float y = Mathf.Clamp(target.bounds.center.y - size * .5f, gutter.yMin, gutter.yMax - size);
                    Rect candidate = new Rect(gutter.center.x - size * .5f, y, size, size);
                    if (CanPlace(candidate, gutter)) { UsedRightGutter = true; return Apply(label, candidate, false); }
                }
            }
            gameObject.SetActive(false);
            return false; // Caller and real layout tests must surface this; never count hiding as success.
        }

        private bool CanPlace(Rect candidate, Rect allowed)
        {
            if (candidate.xMin < allowed.xMin || candidate.xMax > allowed.xMax ||
                candidate.yMin < allowed.yMin || candidate.yMax > allowed.yMax) return false;
            foreach (var glyph in glyphs)
            {
                var padded = Rect.MinMaxRect(glyph.xMin - Gap, glyph.yMin - Gap, glyph.xMax + Gap, glyph.yMax + Gap);
                if (candidate.Overlaps(padded)) return false;
            }
            return true;
        }
        private bool Apply(TextMeshProUGUI label, Rect bounds, bool lower)
        {
            var rt = rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = bounds.size; rt.localScale = Vector3.one; rt.localRotation = Quaternion.identity;
            rt.anchoredPosition = bounds.center - new Vector2(label.rectTransform.rect.xMin, label.rectTransform.rect.yMax);
            PlacedBoundsInLabel = bounds; LastPlacementSucceeded = true; UsedLowerMargin = lower;
            gameObject.SetActive(true); SetVerticesDirty(); return true;
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); Rect r = rectTransform.rect; Vector2 center = r.center;
            float radius = Mathf.Min(r.width, r.height) * .5f, stroke = Mathf.Min(1.6f, radius * .25f);
            const int segments = 24;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
                Vector2 va = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), vb = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                Quad(vh, center + va * radius, center + vb * radius, center + vb * (radius - stroke), center + va * (radius - stroke));
            }
            Bar(vh, center, center + Vector2.up * radius * .52f, stroke);
            Bar(vh, center, center + Vector2.right * radius * .42f, stroke);
        }
        private void Bar(VertexHelper vh, Vector2 a, Vector2 b, float width)
        {
            Vector2 d = (b - a).normalized, normal = new Vector2(-d.y, d.x) * width * .5f;
            Quad(vh, a - normal, b - normal, b + normal, a + normal);
        }
        private void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            int start = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero);
            vh.AddVert(c, color, Vector2.zero); vh.AddVert(d, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
