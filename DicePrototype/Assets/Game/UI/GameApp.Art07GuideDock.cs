using System.Collections;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private bool guideDocked07,guideDockTailHold07;
        private Rect ApplyGuideDock07(bool shop)
        {
            if(shop)
            {
                // v0.73: the paper rests on the owned-dice strip, right of its caption and left of the
                // item slots. No price, purchase target or leave-shop button is displaced; the strip's
                // dice under the paper hide while it is open (see CoveredByGuide073).
                return new Rect(504,928,400,144);
            }
            // V1.1 (W11): with no free spot on the table the paper rests on the stage card, which steps aside while the paper
            // is open (its information is also one click away in the stage intel). No card, die or action moves.
            var l=L11;var stage=GuideElement07("StageCard11");if(stage!=null)stage.gameObject.SetActive(false);
            return new Rect(l.stage.x-4,l.stage.y+50,400,144);
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
