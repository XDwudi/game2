using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DiceCraft
{
    // Only pointer clicks are filtered. Submit, navigation, hover and tooltip events keep their normal UGUI routing.
    public sealed class PageButton15 : Button
    {
        public override void OnPointerClick(PointerEventData e)
        {
            if (GameApp.PagePointerGuard15 && name != "SkipLootReveal15" && name != "LootRevealSurface15" &&
                name != "SkipSettlementReveal07" && name != "Skip") return;
            base.OnPointerClick(e);
        }
    }
}
