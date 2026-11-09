using System.Linq;
using DiceCraft.Core;
using TMPro;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        // A current permanent-face sample. Buying still opens the existing target selector.
        private void DrawShopUpgradePreview071(RectTransform paper,ShopOffer offer,float width)
        {
            var original=ViewState.dice.Where(d=>d.lifetime=="permanent").OrderBy(d=>d.createdSerial).FirstOrDefault()
                ??ViewState.dice.OrderBy(d=>d.createdSerial).FirstOrDefault();
            if(original==null)
            {
                UiKit.Text(paper,"CurrentDiePreviewLabel","当前没有骰子",24,112,width-48,72,22,PaperMuted071,TextAlignmentOptions.Center);
                return;
            }
            var die=original.Clone();
            const float apertureTop=108,apertureWidth=180,apertureHeight=112;
            float apertureLeft=(width-apertureWidth)*.5f;

            // Preserve the shared nine-slice border and shadow. Only its center is opened;
            // four inset paper pieces surround the real main-camera mesh aperture.
            var paperImage=paper.GetComponent<UnityEngine.UI.Image>();
            paperImage.fillCenter=false;paperImage.raycastTarget=false;
            const float paperInset=16;
            UiKit.Box(paper,"PreviewPaperTop",paperInset,paperInset,width-paperInset*2,apertureTop-paperInset,UiKit.Paper);
            UiKit.Box(paper,"PreviewPaperBottom",paperInset,apertureTop+apertureHeight,width-paperInset*2,416-paperInset-apertureTop-apertureHeight,UiKit.Paper);
            UiKit.Box(paper,"PreviewPaperLeft",paperInset,apertureTop,apertureLeft-paperInset,apertureHeight,UiKit.Paper);
            UiKit.Box(paper,"PreviewPaperRight",apertureLeft+apertureWidth,apertureTop,width-paperInset-apertureLeft-apertureWidth,apertureHeight,UiKit.Paper);
            UiKit.Outline(paper,"PreviewApertureBorder",apertureLeft-1,apertureTop-1,apertureWidth+2,apertureHeight+2,Color.Lerp(UiKit.Ink,UiKit.Paper,.5f),1);
            // Paper pieces were added after the title; keep its existing live TMP node above ink.
            paper.Find("Name")?.SetAsLastSibling();
            // Theme artwork identifies the upgrade; the centre remains the real current die.
            // Standard shop cards are 456 wide: this gives a 64px motif wholly inside
            // the left paper column (x45..109), clear of the 3D aperture (x138..318).
            float themeSize=Mathf.Min(64,apertureLeft-paperInset-12);
            if(themeSize>=16)
                Icon(paper,offer.contentId,(paperInset+apertureLeft-themeSize)*.5f,
                    apertureTop+(apertureHeight-themeSize)*.5f,themeSize);
            UiKit.Text(paper,"CurrentDiePreviewLabel","当前骰子 · "+die.dieId,16,76,width-32,30,14,PaperMuted071,TextAlignmentOptions.Center);
            var aperture=UiKit.Rect(paper,"CurrentDiePreview071",apertureLeft,apertureTop,apertureWidth,apertureHeight);
            var scroll=paper.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            UiKit.Component<Art071ShopDiePreview>(aperture.gameObject).Bind(table,aperture,scroll!=null?scroll.viewport:null,
                "shop-upgrade-"+offer.offerId,die,
                ()=>!modal&&!inMenu&&!IsBusy&&ViewState?.phase=="Shop"&&shopTab==2,
                (slot,snapshot,center)=>PlaceNewRunDie07(slot,snapshot,center,Quaternion.Euler(0,12,0)));
        }
    }
}
