using System;
using UnityEngine;

namespace DiceCraft.Presentation
{
    /// <summary>
    /// Visual-only, externally clocked enchant overlay. Attach to a dedicated UI child,
    /// not to a card's interactive root. The caller owns event identity, routing, duration
    /// and pooling, just as it does for Art07PrefabView with autoTick=false.
    /// Coordinates are local UI units relative to this overlay's centre; no gameplay
    /// value is computed or written here.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class Art071EnchantCue : MonoBehaviour
    {
        private enum Kind { None, Base, Multiplier, Slotless, Submission, Roll }
        private const int PaperCount = 4, ArcSegments = 8;
        private static readonly Color Mint = new Color32(0x79, 0xC9, 0x9C, 0xFF);
        private static readonly Color Violet = new Color32(0xAD, 0x92, 0xDB, 0xFF);
        private static readonly Color Paper = new Color32(0xF4, 0xE8, 0xCB, 0xFF);
        private readonly UnityEngine.UI.Image[] papers = new UnityEngine.UI.Image[PaperCount];
        private readonly UnityEngine.UI.Image[] arcLines = new UnityEngine.UI.Image[ArcSegments];
        private readonly UnityEngine.UI.Image[] edges = new UnityEngine.UI.Image[4];
        private UnityEngine.UI.Image ticket, cup, arc;
        private CanvasGroup group;
        private Vector2 extent, source, destination;
        private Kind kind;
        private int paperBudget;
        private bool built, configured;

        public string BoundEnchantmentId { get; private set; }
        public string BoundInstanceId { get; private set; }
        public bool IsBound => kind != Kind.None;
        public bool ReducedMotion { get; private set; }
        public float NormalizedTime { get; private set; }
        public int AllocatedGraphicCount => built ? PaperCount + ArcSegments + 4 + 3 : 0;

        /// <summary>
        /// Prewarm once per pooled overlay. The slotless outline fits inside extent.
        /// ticket/cup should be the existing submit1 badge and ui_icon_roll; the optional
        /// arc is fx_arc_short. Missing arc uses eight simple geometric segments.
        /// Reconfiguration reuses every Graphic; it never creates a material or sprite.
        /// </summary>
        public void Configure(Vector2 area, Sprite submitTicket, Sprite rollCup, Sprite multiplierArc = null)
        {
            if (!Finite(area) || area.x <= 0 || area.y <= 0)
                throw new ArgumentOutOfRangeException(nameof(area));
            EnsureBuilt();
            extent = area;
            ((RectTransform)transform).sizeDelta = area;
            ticket.sprite = submitTicket;
            cup.sprite = rollCup;
            arc.sprite = multiplierArc;
            configured = true;
            ResetForPool();
        }

        /// <summary>
        /// Bind only a committed trigger chosen by the caller. Returns false for none
        /// or unsupported IDs. Numeric labels remain the caller's exact receipt text.
        /// Source/target must use this overlay's local centre coordinates; the caller
        /// chooses clear routes and an appropriate slotless frame extent.
        /// </summary>
        public bool Bind(string enchantId, string instanceId, bool reducedMotion,
            Vector2 from, Vector2 to, int maximumPaper = PaperCount)
        {
            if (!configured) throw new InvalidOperationException("Configure the enchant cue before Bind.");
            ResetForPool();
            if (!Finite(from) || !Finite(to)) throw new ArgumentOutOfRangeException(nameof(from));
            Kind selected;
            switch (enchantId)
            {
                case "base10": selected = Kind.Base; break;
                case "mult2": selected = Kind.Multiplier; break;
                case "slotless": selected = Kind.Slotless; break;
                case "submit1": selected = Kind.Submission; break;
                case "roll1": selected = Kind.Roll; break;
                default: return false;
            }
            if (selected == Kind.Submission && ticket.sprite == null || selected == Kind.Roll && cup.sprite == null)
                throw new InvalidOperationException("The requested enchant cue requires its authored ticket/cup sprite.");
            kind = selected;
            BoundEnchantmentId = enchantId;
            BoundInstanceId = instanceId;
            source = from;
            destination = to;
            paperBudget = Mathf.Clamp(maximumPaper, 0, PaperCount);
            ReducedMotion = reducedMotion;
            SelectGraphics();
            Tick(0);
            return true;
        }

        public void SetReducedMotion(bool reducedMotion)
        {
            ReducedMotion = reducedMotion;
            if (!IsBound) return;
            SelectGraphics();
            Tick(NormalizedTime);
        }

        public void ResizeExtent(Vector2 area)
        {
            if(!Finite(area)||area.x<=0||area.y<=0)throw new ArgumentOutOfRangeException(nameof(area));
            extent=area;((RectTransform)transform).sizeDelta=area;
        }

        /// <summary>
        /// Absolute normalized time, not delta time. Repeated, paused or backward calls
        /// are deterministic. Reduced motion keeps geometry static and only fades out;
        /// its short (normally .12 s) lifetime belongs to the external clock.
        /// </summary>
        public void Tick(float normalized01)
        {
            if (!IsBound) return;
            float t = float.IsNaN(normalized01) ? 0 : Mathf.Clamp01(normalized01);
            NormalizedTime = t;
            group.alpha = t < .7f ? 1 : Mathf.Clamp01((1 - t) / .3f);
            float eased = 1 - Mathf.Pow(1 - t, 3);
            if (kind == Kind.Base)
            {
                for (int i = 0; i < PaperCount; i++)
                {
                    if (ReducedMotion)
                    {
                        // A static mint plus replaces flying paper, including on Low quality.
                        Place(papers[i], source, i == 0 ? new Vector2(12, 3) : new Vector2(3, 12), 0);
                        continue;
                    }
                    float direction = -55 + i * 37;
                    float radians = direction * Mathf.Deg2Rad;
                    Vector2 offset = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)) * (5 + 19 * eased);
                    offset.y -= 8 * t * t;
                    Place(papers[i], source + offset, new Vector2(4, 7), direction + (i % 2 == 0 ? 55 : -55) * t);
                }
            }
            else if (kind == Kind.Multiplier) DrawArc(ReducedMotion ? 1 : eased);
            else if (kind == Kind.Slotless)
            {
                float expand = ReducedMotion ? 1 : Mathf.Lerp(.84f, 1, eased);
                Vector2 half = new Vector2(Mathf.Max(2, extent.x * .5f - 3), Mathf.Max(2, extent.y * .5f - 3)) * expand;
                Vector2 a = destination + new Vector2(-half.x, -half.y), b = destination + new Vector2(half.x, -half.y);
                Vector2 c = destination + half, d = destination + new Vector2(-half.x, half.y);
                Segment(edges[0], a, b, 2); Segment(edges[1], b, c, 2);
                Segment(edges[2], c, d, 2); Segment(edges[3], d, a, 2);
            }
            else if (kind == Kind.Submission)
                Place(ticket, ReducedMotion ? destination : Vector2.Lerp(source, destination, eased), new Vector2(34, 26), ReducedMotion ? 0 : -6 * (1 - eased));
            else if (kind == Kind.Roll)
                Place(cup, ReducedMotion ? destination : Vector2.Lerp(source, destination, eased), new Vector2(32, 32), ReducedMotion ? 0 : Mathf.Sin(t * Mathf.PI * 2) * 22 * (1 - t));
        }

        /// <summary>Hide and clear every per-event value without destroying children or changing the host's active state.</summary>
        public void ResetForPool()
        {
            kind = Kind.None;
            BoundEnchantmentId = BoundInstanceId = null;
            source = destination = Vector2.zero;
            NormalizedTime = 0;
            ReducedMotion = false;
            paperBudget = 0;
            if (!built) return;
            group.alpha = 0;
            group.interactable = group.blocksRaycasts = false;
            SelectGraphics();
            foreach (var image in papers) ResetGraphic(image);
            foreach (var image in arcLines) ResetGraphic(image);
            foreach (var image in edges) ResetGraphic(image);
            ResetGraphic(ticket); ResetGraphic(cup); ResetGraphic(arc);
        }

        private void EnsureBuilt()
        {
            if (built) return;
            group = GetComponent<CanvasGroup>();
            group.interactable = group.blocksRaycasts = false;
            for (int i = 0; i < PaperCount; i++) papers[i] = Graphic("MintPaper" + i, Mint);
            for (int i = 0; i < ArcSegments; i++) arcLines[i] = Graphic("VioletArcSegment" + i, Violet);
            for (int i = 0; i < 4; i++) edges[i] = Graphic("OpenSlotEdge" + i, Paper);
            ticket = Graphic("SubmissionTicket", Color.white);
            cup = Graphic("RollCup", Color.white);
            arc = Graphic("MultiplierArc", Violet);
            ticket.preserveAspect = cup.preserveAspect = arc.preserveAspect = true;
            built = true;
        }

        private UnityEngine.UI.Image Graphic(string objectName, Color tint)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            var r = (RectTransform)go.transform;
            r.SetParent(transform, false);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;
            image.color = tint;
            go.SetActive(false);
            return image;
        }

        private void SelectGraphics()
        {
            for (int i = 0; i < PaperCount; i++) Visible(papers[i], kind == Kind.Base && i < (ReducedMotion ? 2 : paperBudget));
            for (int i = 0; i < ArcSegments; i++) Visible(arcLines[i], kind == Kind.Multiplier && arc.sprite == null);
            foreach (var edge in edges) Visible(edge, kind == Kind.Slotless);
            Visible(ticket, kind == Kind.Submission);
            Visible(cup, kind == Kind.Roll);
            Visible(arc, kind == Kind.Multiplier && arc.sprite != null);
        }

        private void DrawArc(float progress)
        {
            Vector2 delta = destination - source;
            float distance = delta.magnitude;
            Vector2 direction = distance > .001f ? delta / distance : Vector2.right;
            Vector2 normal = new Vector2(-direction.y, direction.x);
            float bend = Mathf.Min(18, distance * .12f);
            Vector2 centre = Vector2.Lerp(source, destination, progress) + normal * (Mathf.Sin(progress * Mathf.PI) * bend);
            float width = Mathf.Clamp(distance * .25f, 18, 52);
            float rotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            if (arc.sprite != null)
            {
                Place(arc, centre, new Vector2(width, 18), rotation);
                return;
            }
            Vector2 last = centre - direction * (width * .5f);
            for (int i = 0; i < ArcSegments; i++)
            {
                float u = (i + 1f) / ArcSegments;
                Vector2 next = centre + direction * ((u - .5f) * width) + normal * (Mathf.Sin(u * Mathf.PI) * 7);
                Segment(arcLines[i], last, next, 2);
                last = next;
            }
        }

        private static void Place(UnityEngine.UI.Image image, Vector2 position, Vector2 size, float angle)
        {
            var r = image.rectTransform;
            r.anchoredPosition = position;
            r.sizeDelta = size;
            r.localScale = Vector3.one;
            r.localRotation = Quaternion.Euler(0, 0, angle);
        }

        private static void Segment(UnityEngine.UI.Image image, Vector2 from, Vector2 to, float thickness)
        {
            Vector2 delta = to - from;
            Place(image, (from + to) * .5f, new Vector2(delta.magnitude, thickness), Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private static void Visible(UnityEngine.UI.Image image, bool visible)
        { if (image.gameObject.activeSelf != visible) image.gameObject.SetActive(visible); }

        private static void ResetGraphic(UnityEngine.UI.Image image)
        { Place(image, Vector2.zero, Vector2.zero, 0); image.raycastTarget = false; }

        private static bool Finite(Vector2 value)
        { return !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.x) && !float.IsInfinity(value.y); }
    }
}
