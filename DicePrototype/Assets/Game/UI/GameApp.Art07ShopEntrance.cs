using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private void BindShopEntrance07()
        {
            var shelfNames=new[]{"ControllerShelf","ControllerShelfTitle","RefreshControllers","Tab0","Tab1","Tab2","Tab3","Tab4","ActiveShopTab","NoOffers","ShopHint","LeaveShop"};
            var shelf=shelfNames.Select(n=>main.Find(n) as RectTransform).Where(r=>r!=null&&r.gameObject.activeInHierarchy).ToList();
            var scrollbar=main.Find("ShopProducts/ScrollTrack") as RectTransform;if(scrollbar!=null)shelf.Add(scrollbar);
            var cards=new List<RectTransform[]>();
            var controllers=ViewState.shop.offers.Where(o=>o.category=="controller").ToArray();
            foreach(var offer in controllers)
                cards.Add(new[]{main.Find("Offer_"+offer.offerId) as RectTransform,main.Find("Buy_"+offer.offerId) as RectTransform,main.Find("OfferAvailability_"+offer.offerId) as RectTransform});
            int capacity=Config.shop.initialControllerOffers+ViewState.controllerOfferPurchases;
            for(int i=controllers.Length;i<capacity;i++)cards.Add(new[]{main.Find("EmptyControllerOffer"+i) as RectTransform});
            var products=main.Find("ShopProducts/Viewport/Content");
            if(products!=null)
                for(int i=0;i<products.childCount;i++)
                {
                    var card=products.GetChild(i) as RectTransform;
                    if(card!=null&&card.gameObject.activeInHierarchy&&(card.name.StartsWith("Product_",StringComparison.Ordinal)||card.name=="SelfStudy"))cards.Add(new[]{card});
                }
            UiKit.Component<Art07ShopEntrance>(main.gameObject).Bind(ViewState.shop.generationId,reduced,instant07,()=>modal||inMenu,shelf.ToArray(),cards.ToArray());
        }
    }
}
