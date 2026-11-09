using DiceCraft.Core;
using TMPro;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private void DrawTemporaryClock071(TextMeshProUGUI label, DieInstance die, bool locked, bool compact)
        {
            bool temporary = die.lifetime == "stage_temporary";
            var existing = label.transform.Find("TemporaryClock071");
            if (!temporary)
            {
                if (existing != null) existing.gameObject.SetActive(false);
                return;
            }
            var root = UiKit.Rect(label.transform, "TemporaryClock071", 0, 0, 16, 16);
            var clock = UiKit.Component<Art071TemporaryClock>(root.gameObject);
            clock.color = UiKit.Paper; clock.raycastTarget = false;
            var size = label.rectTransform.rect.size;
            Rect allowed = new Rect(0, 0, size.x, size.y);
            Rect? gutter = null;
            if (locked && lockedSlotRects07.TryGetValue(die.dieId, out Rect slot))
            {
                Vector2 p = label.rectTransform.anchoredPosition;
                // Keep the existing text-column left edge; the dice body and grip are left of it.
                float right = slot.xMax - 4 - p.x, bottom = slot.yMax - 4 + p.y;
                allowed = new Rect(0, 0, Mathf.Max(0, right), Mathf.Max(0, bottom));
                // The fixed slot gap already exists; it is neither a larger slot nor a larger click target.
                float margin = compact ? 3 : 4;
                float nextLeft = slot.xMax + (compact ? 18 : 24);
                foreach (var next in lockedSlotRects07.Values)
                    if (Mathf.Abs(next.y - slot.y) < 1 && next.x > slot.x) nextLeft = Mathf.Min(nextLeft, next.x);
                gutter = new Rect(slot.xMax + margin - p.x, slot.y + 4 + p.y,
                    nextLeft - slot.xMax - margin * 2, slot.height - 8);
            }
            bool placed = clock.PlaceOn(label, true, allowed, compact ? 18 : 16, gutter);
            if (!placed) Debug.LogError("TEMPORARY_CLOCK071_LAYOUT: no clear annotation space for " + die.dieId);
        }
    }
}
