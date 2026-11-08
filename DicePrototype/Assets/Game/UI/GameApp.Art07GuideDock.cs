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
                // Preserve the complete shop hint alongside the paper. No price,
                // purchase target, dice preview, or leave-shop button is displaced.
                var hint=GuideElement07("ShopHint");
                if(hint!=null)
                {
                    hint.anchoredPosition=new Vector2(816,-936);hint.sizeDelta=new Vector2(632,120);
                    var text=hint.GetComponent<TMPro.TMP_Text>();if(text!=null)text.textWrappingMode=TMPro.TextWrappingModes.Normal;
                }
                return new Rect(392,928,400,144);
            }
            // The sidebar's top 144 reference pixels contain branding only. Let
            // the first card column yield horizontally while a full paper is open.
            var stats=GuideElement07("Stats07");
            if(stats!=null)foreach(string name in new[]{"BrandPaper","Edition"}){var decoration=stats.Find(name);if(decoration!=null)decoration.gameObject.SetActive(false);}
            float start=Compact07?426:432,right=Compact07?1612:1528,ordinaryGap=Compact07?12:24;
            int count=ViewState.controllers.Count,shown=Mathf.Min(10,count);
            for(int i=0;i<Mathf.Max(4,shown);i++)
            {
                RectTransform card=i<shown?GuideElement07("Controller_"+ViewState.controllers[i].instanceId):GuideElement07("ControllerEmpty"+i);
                if(card==null)continue;
                float gap=Mathf.Min(ordinaryGap,Mathf.Max(4,(right-start-5*card.rect.width)/4));
                card.anchoredPosition=new Vector2(start+i%5*(card.rect.width+gap),card.anchoredPosition.y);
            }
            if(Compact07)
                for(int i=0;i<main.childCount;i++)
                {
                    var child=main.GetChild(i) as RectTransform;
                    if(child!=null&&(child.name=="ItemsTitle"||child.name.StartsWith("Item_")||child.name.StartsWith("ItemEmpty")))
                        child.anchoredPosition+=Vector2.right*18;
                }
            return new Rect(Compact07?18:24,Compact07?18:24,400,144);
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
