using System.Collections;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private bool guideDocked07,guideDockTailHold07;
        private float GuideHeight16=>Large11?188:144;
        private Rect ApplyGuideDock07(bool shop)
        {
            if(shop)
            {
                // V1.1 W14 (SHOP_LAYOUT 2): the paper rests on the dice of 我的骰子, under its title and right below 骰子与骰面
                // that the guide points at. No price, purchase target or leave-shop button is covered; the interface dice
                // under the paper step aside while it is open (CoveredByGuide073), as on the v0.73 owned-dice strip.
                var o=S14.owned;
                return new Rect(o.x+(o.width-400)*.5f,o.y+ShopHead14+Mathf.Max(0,(o.height-ShopHead14-ShopFoot14-GuideHeight16)*.5f),400,GuideHeight16);
            }
            // V1.1 (W11): with no free spot on the table the paper rests on the stage card, which steps aside while the paper
            // is open (its information is also one click away in the stage intel). No card, die or action moves.
            var l=L11;var stage=GuideElement07("StageCard11");if(stage!=null)stage.gameObject.SetActive(false);
            return new Rect(l.stage.x-4,l.stage.y+50,400,GuideHeight16);
        }
        private IEnumerator ReleaseGuideDock07()
        {
            // Keep yielded cards clear of the existing 80ms paper exit tail.
            // Closing is immediate; this only defers restoring decoration positions.
            while(guideTail07!=null&&guideTail07.IsPlaying)yield return null;
            guideDockTailHold07=false;
            if(!guide07&&!inMenu&&!modal)Refresh();
        }
    }
}
