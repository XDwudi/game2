using System;
using System.Collections.Generic;
using System.Linq;
using DiceCraft.Core;
using UnityEngine;
using Image=UnityEngine.UI.Image;

namespace DiceCraft
{
    // v0.73 shop helpers that outlive its page. V1.1 W14 replaced the page itself (GameApp.Shop14.cs, SHOP_LAYOUT.md) and
    // removed its fixed section colours (THEME_SPEC §5); what remains is shared with the gift dialog, the play screen's stage
    // card, the card template and the purchase receipts.
    public sealed partial class GameApp
    {
        private static string PriceText073(long cost)=>cost<0?"+"+(-cost):cost.ToString(System.Globalization.CultureInfo.InvariantCulture);
        /// <summary>The take / buy button of the gift dialog (W15 redraws that dialog).</summary>
        private UnityEngine.UI.Button BuyButton073(Transform parent,ShopOffer offer,float x,float y,float w,float h)
        {
            // v1.0: a gift-group offer is taken, not bought; once its group is closed the rest are void ("未选", W14).
            bool gift=!string.IsNullOrEmpty(offer.groupId),closed=gift&&ViewState.shop.groups.Any(g=>g.groupId==offer.groupId&&g.closed);
            string label=gift?(offer.sold?"已领取":closed?"未选":"领取"):offer.sold?"已购买":"购买";
            return UiKit.ActionButton(parent,"Buy_"+offer.offerId,label,x,y,w,h,()=>{if(gift)CloseModal();Art07BuyOffer(offer);},UiKit.Paper,!offer.sold&&!closed&&!IsBusy&&!Session.HasPendingSave);
        }
        private UnityEngine.UI.Button HitArea073(Transform parent,string name,float x,float y,float w,float h,Action click)
        {
            var area=UiKit.Box(parent,name,x,y,w,h,Color.clear,true);
            var button=UiKit.Component<UnityEngine.UI.Button>(area.gameObject);button.onClick.RemoveAllListeners();
            button.targetGraphic=area.GetComponent<Image>();button.transition=UnityEngine.UI.Selectable.Transition.None;
            button.interactable=true;button.enabled=true;button.onClick.AddListener(()=>click?.Invoke());return button;
        }
        private static void ShopCardSurface073(Image image)
        {
            var sprite=UiKit.Template("ui_card_shop");if(sprite==null)return;
            image.sprite=sprite;image.type=Image.Type.Sliced;image.color=Color.white;
        }
        /// <summary>The receipt card of a bought die: its pip sum and colours, its finishes on a second line.</summary>
        private static string DieSummary073(DieInstance die)
        {
            int red=die.faces.Count(f=>f.baseColor=="red"),sum=die.faces.Sum(f=>f.permanentPip);
            var extras=new List<string>();
            if(die.isGlass)extras.Add("玻璃");
            if(die.faces.Any(f=>f.isGold))extras.Add("黄金 "+die.faces.Count(f=>f.isGold));
            if(die.faces.Any(f=>f.texture=="smooth"))extras.Add("光滑 "+die.faces.Count(f=>f.texture=="smooth"));
            if(die.faces.Any(f=>f.texture=="rough"))extras.Add("粗糙 "+die.faces.Count(f=>f.texture=="rough"));
            return "六面和 "+sum+" · 红 "+red+" 蓝 "+(6-red)+(extras.Count>0?"\n"+string.Join(" · ",extras):"");
        }
        private void SoldVeil073(Transform card,float w,float h)
        {
            var veil=UiKit.Box(card,"SoldVeil",6,6,w-16,h-18,new Color(UiKit.Paper.r,UiKit.Paper.g,UiKit.Paper.b,.55f));veil.SetSiblingIndex(Mathf.Max(0,card.childCount-3));
        }
    }
}
