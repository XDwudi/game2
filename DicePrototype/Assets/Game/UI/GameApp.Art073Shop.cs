using System;
using System.Collections.Generic;
using System.Linq;
using DiceCraft.Core;
using TMPro;
using UnityEngine;
using Image=UnityEngine.UI.Image;

namespace DiceCraft
{
    // v0.73 single-page shop (reference image 2). Presentation only: every purchase, refresh, sale and
    // reorder goes through the same session commands and the same detail dialogs as before.
    public sealed partial class GameApp
    {
        private static readonly Color SectionRed073=UiKit.Hex("B8433F"),SectionTeal073=UiKit.Hex("2F8F86"),SectionViolet073=UiKit.Hex("7B5FB5");
        private const float ShopLeft073=368,ShopWidth073=1528,ShelfTop073=72,ShelfHeight073=428,ProductsTop073=512,ProductsHeight073=416;

        private static Color EnchantTint073(string enchantment)
        {
            switch(enchantment)
            {
                case "slotless":return UiKit.Violet;case "base10":return UiKit.Teal;case "mult2":return UiKit.Gold;
                case "submit1":return UiKit.Hex("3F8F6B");case "roll1":return UiKit.Blue;default:return Color.clear;
            }
        }
        /// <summary>Enchantment-coloured frame just inside the ink edge of a paper card (reference image 3).</summary>
        private void EnchantFrame073(Transform card,string enchantment,float width,float height)
        {
            Color tint=EnchantTint073(enchantment);var sprite=UiKit.Template("ui_frame_enchant");
            var frame=UiKit.Box(card,"EnchantFrame073",0,0,width,height,sprite!=null?tint:Color.clear).GetComponent<Image>();
            frame.sprite=sprite;frame.type=sprite!=null?Image.Type.Sliced:Image.Type.Simple;frame.fillCenter=false;frame.raycastTarget=false;
            frame.gameObject.SetActive(tint.a>0&&sprite!=null);
        }
        private RectTransform Section073(Transform parent,string name,string title,string titleName,float x,float y,float w,float h,Color tint)
        {
            var panel=UiKit.Box(parent,name,x,y,w,h,Color.white);var image=panel.GetComponent<Image>();
            image.sprite=UiKit.Template("ui_section_panel");image.type=image.sprite!=null?Image.Type.Sliced:Image.Type.Simple;
            if(image.sprite==null)image.color=UiKit.Background;
            UiKit.Box(panel,"HeaderStrip",5,5,w-10,40,Color.Lerp(tint,UiKit.Ink,.62f));
            float band=Mathf.Min(w-10,Mathf.Max(150,58+title.Length*27*UiKit.FontScale));
            var label=UiKit.Box(panel,"HeaderBand",5,5,band,40,tint).GetComponent<Image>();
            label.sprite=UiKit.Template("ui_section_header");label.type=label.sprite!=null?Image.Type.Sliced:Image.Type.Simple;
            var text=UiKit.Text(panel,titleName,title,22,5,band-48,40,21,UiKit.TextColor,TextAlignmentOptions.MidlineLeft);
            text.fontStyle=FontStyles.Bold;text.textWrappingMode=TextWrappingModes.NoWrap;
            return panel;
        }
        private RectTransform PriceChip073(Transform parent,string name,string value,float x,float y,float w,float h,bool dim)
        {
            Color gold=dim?Color.Lerp(UiKit.Gold,UiKit.Paper,.55f):UiKit.Gold;
            var chip=UiKit.Box(parent,name,x,y,w,h,gold);var image=chip.GetComponent<Image>();
            image.sprite=UiKit.Template("ui_price_chip");image.type=image.sprite!=null?Image.Type.Sliced:Image.Type.Simple;
            float coin=Mathf.Min(26,h-8);
            var icon=UiKit.Box(chip,"Coin",9,(h-coin)*.5f,coin,coin,dim?new Color(1,1,1,.6f):Color.white).GetComponent<Image>();icon.sprite=UiKit.Template("ui_icon_coin");
            var price=UiKit.Text(chip,"Price",value,9+coin,0,w-coin-18,h,21,UiKit.Ink,TextAlignmentOptions.Midline);
            price.fontStyle=FontStyles.Bold;price.textWrappingMode=TextWrappingModes.NoWrap;
            return chip;
        }
        private static string PriceText073(long cost)=>cost<0?"+"+(-cost):cost.ToString(System.Globalization.CultureInfo.InvariantCulture);
        private UnityEngine.UI.Button BuyButton073(Transform parent,ShopOffer offer,float x,float y,float w,float h)
        {
            long cost=Session.GetOfferNetPrice(offer);
            string label=offer.sold?"已购买":cost<0?"替换":"购买";
            return UiKit.ActionButton(parent,"Buy_"+offer.offerId,label,x,y,w,h,()=>Art07BuyOffer(offer),UiKit.Paper,!offer.sold&&!IsBusy&&!Session.HasPendingSave);
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

        private void Shop073()
        {
            var title=UiKit.Text(main,"ShopTitle","商店",ShopLeft073+4,10,220,60,42,UiKit.TextColor);title.fontStyle=FontStyles.Bold;
            ShopControllers073();
            ShopDiceCounter073();
            ShopProducts073();
            ShopOwnedControllers073();
            ShopOwnedStrip073();
            var leave=UiKit.ActionButton(main,"LeaveShop","下一关",1512,952,384,96,
                ()=>{CloseModal();Send(new SessionCommand{kind=CommandKind.LeaveShop});},UiKit.Gold,!IsBusy&&!Session.HasPendingSave);
            var leaveLabel=leave.transform.Find("Label").GetComponent<TextMeshProUGUI>();leaveLabel.fontSize=34*UiKit.FontScale;leaveLabel.fontStyle=FontStyles.Bold;
            leaveLabel.rectTransform.anchoredPosition=new Vector2(8,0);leaveLabel.rectTransform.sizeDelta=new Vector2(300,89);
            var arrow=UiKit.Box(leave.transform,"Arrow",316,24,42,42,UiKit.Ink).GetComponent<Image>();arrow.sprite=UiKit.Template("ui_icon_arrow");arrow.raycastTarget=false;
            BindShopEntrance073();
        }

        // ---------------------------------------------------------------- controllers
        private void ShopControllers073()
        {
            var offers=ViewState.shop.offers.Where(o=>o.category=="controller").ToArray();
            int capacity=Config.shop.initialControllerOffers+ViewState.controllerOfferPurchases;
            var shelf=Section073(main,"ControllerShelf","控制器卡牌","ControllerShelfTitle",ShopLeft073,ShelfTop073,1012,ShelfHeight073,SectionRed073);
            UiKit.Text(shelf,"ShelfCapacity071","货架 "+offers.Length+" / "+capacity,560,5,430,40,18,UiKit.Muted,TextAlignmentOptions.MidlineRight);
            int slots=Math.Max(offers.Length,capacity);bool twoRows=slots>4;
            float areaLeft=ShopLeft073+16,areaWidth=1012-32-180,top=ShelfTop073+52;
            float cardW=twoRows?262:slots==4?196:252,cardH=twoRows?176:368,gapX=twoRows?12:slots==4?10:16,gapY=16;
            int perRow=twoRows?3:Math.Max(1,slots);
            for(int i=0;i<slots;i++)
            {
                float x=areaLeft+(i%perRow)*(cardW+gapX),y=top+(i/perRow)*(cardH+gapY);
                if(i<offers.Length)ShopControllerCard073(offers[i],x,y,cardW,cardH,twoRows);
                else
                {
                    var empty=UiKit.Box(main,"EmptyControllerOffer"+i,x,y,cardW,cardH,Color.white);var slot=empty.GetComponent<Image>();
                    slot.sprite=UiKit.Template("ui_slot_inventory");slot.type=slot.sprite!=null?Image.Type.Sliced:Image.Type.Simple;if(slot.sprite==null)slot.color=new Color(0,0,0,.08f);
                    UiKit.Text(empty,"Empty","本店暂无合法商品",12,cardH*.5f-44,cardW-24,56,18,UiKit.Muted,TextAlignmentOptions.Center);
                    UiKit.Text(empty,"Applied","容量已生效",12,cardH*.5f+14,cardW-24,36,15,UiKit.Muted,TextAlignmentOptions.Center);
                }
            }
            int price=Session.GetRefreshPrice();
            float rx=ShopLeft073+1012-16-156,ry=ShelfTop073+124;
            var refresh=UiKit.ActionButton(main,"RefreshControllers","刷新",rx,ry,156,224,()=>Send(new SessionCommand{kind=CommandKind.RefreshControllers}),
                UiKit.Blue,!IsBusy&&!Session.HasPendingSave&&ViewState.gold>=price);
            var refreshLabel=refresh.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            refreshLabel.fontSize=28*UiKit.FontScale;refreshLabel.fontStyle=FontStyles.Bold;
            refreshLabel.rectTransform.anchoredPosition=new Vector2(8,-104);refreshLabel.rectTransform.sizeDelta=new Vector2(140,48);
            var icon=UiKit.Box(refresh.transform,"RefreshIcon",42,22,72,72,UiKit.Ink).GetComponent<Image>();icon.sprite=UiKit.Template("ui_icon_refresh");icon.raycastTarget=false;icon.preserveAspect=true;
            PriceChip073(refresh.transform,"RefreshPrice",price==0?"免费":price.ToString(),22,160,112,38,ViewState.gold<price);
        }
        private void ShopControllerCard073(ShopOffer offer,float x,float y,float w,float h,bool wide)
        {
            string id=offer.offerId;string enchant=offer.controller?.enchantmentId;
            var card=UiKit.Button(main,"Offer_"+id,"",x,y,w,h,()=>InspectOffer(offer),!IsBusy);
            UiKit.CardSurface(card,false);ShopCardSurface073((Image)card.targetGraphic);
            EnchantFrame073(card.transform,enchant,w,h);
            long cost=Session.GetOfferNetPrice(offer);
            string availability=offer.sold?"":Art07Availability07(offer);
            if(wide)
            {
                // Two rows of small cards: the top 104 units are exactly the compact HUD card body.
                DrawControllerRarity07(card.transform,offer.contentId,true,w,h);
                EnchantBadge(card.transform,enchant,w-40,2,28);
                Icon(card.transform,offer.contentId,12,38,64);
                var name=UiKit.Text(card.transform,"Name",NameOf(offer.contentId),84,28,w-96,42,18,UiKit.Ink,TextAlignmentOptions.Center);
                name.textWrappingMode=TextWrappingModes.NoWrap;name.overflowMode=TextOverflowModes.Ellipsis;
                var state=UiKit.Text(card.transform,"State",Art07OfferState(offer.controller),84,68,w-96,34,14,UiKit.Ink,TextAlignmentOptions.Center);
                state.textWrappingMode=TextWrappingModes.NoWrap;state.overflowMode=TextOverflowModes.Ellipsis;
                PriceChip073(card.transform,"PriceChip",PriceText073(cost),12,112,96,34,offer.sold);
                var reason=UiKit.Text(main,"OfferAvailability_"+id,availability,x+8,y+148,104,22,12,UiKit.Danger,TextAlignmentOptions.Center);reason.textWrappingMode=TextWrappingModes.NoWrap;
                BuyButton073(main,offer,x+118,y+108,w-130,62);
            }
            else
            {
                // One row of tall cards: the upper 250 units are exactly the full controller card body.
                const float body=250;float buyY=h-70,chipY=buyY-38,availY=body-12;
                DrawControllerBody073(card.transform,offer.contentId,Art07OfferState(offer.controller),enchant,0,body);
                PriceChip073(card.transform,"PriceChip",PriceText073(cost),(w-124)*.5f,chipY,124,34,offer.sold);
                var reason=UiKit.Text(main,"OfferAvailability_"+id,availability,x+8,y+availY,w-16,22,14,UiKit.Danger,TextAlignmentOptions.Center);reason.textWrappingMode=TextWrappingModes.NoWrap;
                BuyButton073(main,offer,x+16,y+buyY,w-32,62);
            }
            Art07AttachTooltip(card,id,NameOf(offer.contentId),Description(offer.contentId),EnchantLabel(enchant)+"\n"+Art07OfferState(offer.controller),()=>InspectOffer(offer));
        }

        // ---------------------------------------------------------------- dice counter
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
        private void ShopDiceCounter073()
        {
            var counter=Section073(main,"DiceCounter","骰子柜台","DiceCounterTitle",ShopLeft073+1028,ShelfTop073,500,ShelfHeight073,SectionTeal073);
            var offers=ViewState.shop.offers.Where(o=>o.category=="new_die").ToArray();
            if(offers.Length==0)
            {
                UiKit.Text(counter,"NoDiceOffers","永久骰已满\n本店不出售骰子",24,160,452,120,22,UiKit.Muted,TextAlignmentOptions.Center);return;
            }
            for(int i=0;i<offers.Length&&i<2;i++)
            {
                var offer=offers[i];const float w=232,h=368;float x=12+i*244,y=52;
                var p=UiKit.PaperFrame(counter,"Product_"+offer.offerId,x,y,w,h,UiKit.Paper);ShopCardSurface073(p.GetComponent<Image>());
                var name=UiKit.Text(p,"Name","骰子 "+(char)('A'+i),10,8,w-20,38,24,UiKit.Ink,TextAlignmentOptions.Center);name.fontStyle=FontStyles.Bold;
                var stand=UiKit.Box(p,"Pedestal",(w-160)*.5f,112,160,80,Color.white).GetComponent<Image>();stand.sprite=UiKit.Template("ui_pedestal");stand.raycastTarget=false;
                stand.gameObject.SetActive(stand.sprite!=null);
                // The real die: drag to turn it, click to open the full candidate sheet.
                DieView073(p,"DiePreview073","shop-die-"+offer.offerId,offer.die,16,36,w-32,136,1.0f,false,true,()=>InspectOffer(offer));
                var summary=UiKit.Text(p,"Summary",DieSummary073(offer.die),10,194,w-20,40,13,PaperMuted071,TextAlignmentOptions.Center);summary.lineSpacing=-10;
                // The die itself owns drag and click above; this strip under it opens the same candidate sheet.
                HitArea073(p,"Inspect",0,192,w-6,62,()=>{if(!IsBusy)InspectOffer(offer);});
                string availability=offer.sold?"":Art07Availability07(offer);
                long cost=Session.GetOfferNetPrice(offer);
                UiKit.Text(p,"Availability",availability,8,h-136,w-16,22,14,UiKit.Danger,TextAlignmentOptions.Center);
                PriceChip073(p,"PriceChip",PriceText073(cost),(w-124)*.5f,h-112,124,36,offer.sold);
                BuyButton073(p,offer,16,h-72,w-32,62);
                if(offer.sold)SoldVeil073(p,w,h);
            }
        }
        private void SoldVeil073(Transform card,float w,float h)
        {
            var veil=UiKit.Box(card,"SoldVeil",6,6,w-16,h-18,new Color(UiKit.Paper.r,UiKit.Paper.g,UiKit.Paper.b,.55f));veil.SetSiblingIndex(Mathf.Max(0,card.childCount-3));
        }

        // ---------------------------------------------------------------- consumables, research, face upgrades, services
        private void ShopProducts073()
        {
            var root=UiKit.Rect(main,"ShopProducts",ShopLeft073,ProductsTop073,ShopWidth073,ProductsHeight073);
            var offers=ViewState.shop.offers;
            var consumables=offers.Where(o=>o.category=="consumable").ToArray();
            var research=offers.Where(o=>o.category=="hand_upgrade"||o.category=="color_upgrade").ToArray();
            var upgrades=offers.Where(o=>o.category=="die_upgrade").ToArray();
            var services=offers.Where(o=>o.category=="service").ToArray();
            const float h=ProductsHeight073;

            var items=Section073(root,"ConsumableSection","消耗品","ConsumableTitle",0,0,452,h,SectionRed073);
            ProductShelf073(items,"ConsumableShelf",consumables,12,52,428,356,consumables.Length==3?3:2);
            if(consumables.Length==0)UiKit.Text(items,"NoOffers","本店暂无道具",12,160,428,60,20,UiKit.Muted,TextAlignmentOptions.Center);

            var study=Section073(root,"ResearchSection","研究强化","ResearchTitle",464,0,452,h,SectionTeal073);
            var selfStudy=UiKit.ActionButton(study,"Study",ViewState.shop.selfStudyUsed==0?"自选研习 · 正常价 +1":"本店已研习",12,52,428,60,ShowStudy,UiKit.Gold,ViewState.shop.selfStudyUsed==0&&!IsBusy);
            selfStudy.transform.Find("Label").GetComponent<TextMeshProUGUI>().fontSize=20*UiKit.FontScale;
            ProductShelf073(study,"ResearchShelf",research,12,120,428,288,2);
            if(research.Length==0)UiKit.Text(study,"NoResearch","本店没有可升级的牌型",12,220,428,60,20,UiKit.Muted,TextAlignmentOptions.Center);

            var faces=Section073(root,"UpgradeSection","骰面改造","UpgradeTitle",928,0,392,h,SectionTeal073);
            ProductShelf073(faces,"UpgradeShelf",upgrades,12,52,368,356,2);
            if(upgrades.Length==0)UiKit.Text(faces,"NoUpgrades","本店暂无改造",12,160,368,60,20,UiKit.Muted,TextAlignmentOptions.Center);

            var service=Section073(root,"ServiceSection","商店服务","ServiceTitle",1332,0,196,h,SectionViolet073);
            ProductShelf073(service,"ServiceShelf",services,12,52,172,356,1);
            if(services.Length==0)UiKit.Text(service,"NoServices","暂无服务",12,160,172,60,20,UiKit.Muted,TextAlignmentOptions.Center);
        }
        /// <summary>One row of cards; more goods than one row scroll inside the section instead of leaving it.</summary>
        private void ProductShelf073(Transform section,string scrollName,ShopOffer[] offers,float x,float y,float width,float cardHeight,int columns)
        {
            if(offers.Length<=columns){ProductRow073(section,offers,x,y,width,cardHeight,columns);return;}
            int rows=(offers.Length+columns-1)/columns;
            var list=UiKit.Scroll(section,scrollName,x,y,width+6,cardHeight,rows*(cardHeight+8)-8);
            ProductRow073(list,offers,0,0,width-18,cardHeight,columns);
        }
        private void ProductRow073(Transform parent,ShopOffer[] offers,float x,float y,float width,float cardHeight,int columns)
        {
            const float gap=12;float w=(width-(columns-1)*gap)/columns;
            for(int i=0;i<offers.Length;i++)ProductCard073(parent,offers[i],x+(i%columns)*(w+gap),y+(i/columns)*(cardHeight+8),w,cardHeight);
        }
        private void ProductCard073(Transform parent,ShopOffer offer,float x,float y,float w,float h)
        {
            var p=UiKit.PaperFrame(parent,"Product_"+offer.offerId,x,y,w,h,UiKit.Paper);ShopCardSurface073(p.GetComponent<Image>());
            float buyY=h-72,chipY=buyY-40,availY=chipY-22;
            // Reserve one complete line at the chosen text size: a clipped CJK line renders as nothing at all.
            float nameSize=w>=200?22:19,nameHeight=Mathf.Max(38,Mathf.Ceil(nameSize*UiKit.FontScale*1.5f)),bodyTop=6+nameHeight+2;
            var name=UiKit.Text(p,"Name",NameOf(offer.contentId),8,6,w-16,nameHeight,nameSize,UiKit.Ink,TextAlignmentOptions.Center);
            name.fontStyle=FontStyles.Bold;name.textWrappingMode=TextWrappingModes.NoWrap;name.overflowMode=TextOverflowModes.Ellipsis;
            if(offer.category=="hand_upgrade"||offer.category=="color_upgrade")
            {
                bool color=Config.colorDefinitions.Any(definition=>definition.id==offer.contentId);
                int level=(color?ViewState.colorLevels:ViewState.handLevels).First(l=>l.handId==offer.contentId).level;
                int basis=color?Config.Color(offer.contentId).baseMult100:Config.Hand(offer.contentId).baseMult100;
                int step=color?Config.Color(offer.contentId).incrementPerLevel100:Config.Hand(offer.contentId).incrementPerLevel100;
                var row=HandbookRules.Build(Config,ViewState.handLevels,ViewState.colorLevels,ViewState.controllers,Session.EffectiveCapacity,
                    ViewState.dice.Count(d=>d.lifetime=="permanent"),ViewState.dice.Count,ViewBindings,ViewState.isDebug).First(r=>r.handId==offer.contentId);
                // A hand pattern, not the faces of a die: the small tiles stay as the handbook draws them.
                int[] numbers=row.example.Where(char.IsDigit).Select(c=>c-'0').Where(n=>n>=1&&n<=6).ToArray();
                float tile=Mathf.Min(UiKit.FontScale>1?30:38,(w-24-Mathf.Max(0,numbers.Length-1)*5)/Mathf.Max(1,numbers.Length));
                float start=(w-(numbers.Length*tile+Mathf.Max(0,numbers.Length-1)*5))*.5f;
                Color pip=color?(offer.contentId=="all_red"?UiKit.Red:UiKit.Blue):UiKit.Ink;
                for(int i=0;i<numbers.Length;i++)Art07PipTile(p,"StudyExample"+i,numbers[i],pip,start+i*(tile+5),bodyTop+2,tile);
                float levelTop=bodyTop+tile+6,levelHeight=Mathf.Max(26,Mathf.Ceil(18*UiKit.FontScale*1.45f)*.82f);
                UiKit.Text(p,"Level","Lv."+level+" → "+(level+1),8,levelTop,w-16,levelHeight,UiKit.FontScale>1?15:18,PaperMuted071,TextAlignmentOptions.Center);
                var multiplier=UiKit.Text(p,"Multiplier","×"+Factor(basis+step*level)+" → ×"+Factor(basis+step*(level+1)),8,levelTop+levelHeight,w-16,Mathf.Max(30,availY-levelTop-levelHeight),UiKit.FontScale>1?17:21,UiKit.Ink,TextAlignmentOptions.Top);multiplier.fontStyle=FontStyles.Bold;
            }
            else
            {
                float summaryHeight=Mathf.Max(46,Mathf.Ceil(15*UiKit.FontScale*1.5f)+4);
                float art=Mathf.Clamp(availY-bodyTop-summaryHeight-4,40,Mathf.Min(w-48,124));
                Icon(p,offer.contentId,(w-art)*.5f,bodyTop,art);
                string text=Description(offer.contentId);int stop=text.IndexOf('。');if(stop>=0)text=text.Substring(0,stop);
                var summary=UiKit.Text(p,"Summary",text,10,bodyTop+art+4,w-20,availY-bodyTop-art-4,15,UiKit.Ink,TextAlignmentOptions.Top);
                summary.overflowMode=TextOverflowModes.Ellipsis;summary.lineSpacing=-8;
            }
            string availability=offer.sold?"":Art07Availability07(offer);
            UiKit.Text(p,"Availability",availability,6,availY,w-12,22,14,UiKit.Danger,TextAlignmentOptions.Center);
            long cost=Session.GetOfferNetPrice(offer);
            var inspect=HitArea073(p,"Inspect",0,0,w-6,chipY-2,()=>{if(!IsBusy)InspectOffer(offer);});
            Art07AttachTooltip(inspect,offer.offerId,NameOf(offer.contentId),Description(offer.contentId),string.IsNullOrEmpty(availability)?"价格 "+PriceText073(cost)+" 金币":availability,()=>InspectOffer(offer));
            PriceChip073(p,"PriceChip",PriceText073(cost),(w-Mathf.Min(124,w-24))*.5f,chipY,Mathf.Min(124,w-24),36,offer.sold);
            BuyButton073(p,offer,12,buyY,w-28,62);
            if(offer.sold)SoldVeil073(p,w,h);
        }

        // ---------------------------------------------------------------- what the player owns
        private void ShopOwnedControllers073()
        {
            controllerRects.Clear();
            string capacity=ViewState.debugWorkspace?"调试配置 · 持有 "+ViewState.controllers.Count+"\n普通占槽 "+ControllerRules.OrdinaryUsed(ViewState.controllers):"普通槽 "+ControllerRules.OrdinaryUsed(ViewState.controllers)+"/"+ViewState.controllerSlots+" · 总数 "+ViewState.controllers.Count+"/"+Config.maxControllerInstances;
            UiKit.Text(main,"ControllerCapacity07",capacity,40,540,288,48,17,UiKit.Paper);
            UiKit.Text(main,"OwnedControllersTitle","已装备 · 拖拽排序",40,590,288,34,19,UiKit.Muted);
            const float rowHeight=92;
            var list=UiKit.Scroll(main,"ShopOwnedControllers",40,628,288,328,Math.Max(328,((ViewState.controllers.Count+1)/2)*rowHeight));
            for(int i=0;i<ViewState.controllers.Count;i++)
            {
                var item=ViewState.controllers[i];
                var button=UiKit.ActionButton(list,"Controller_"+item.instanceId,"",(i%2)*142,(i/2)*rowHeight,134,rowHeight-8,()=>ShowController(item,true),UiKit.Paper,!IsBusy);
                UiKit.CardSurface(button,true);
                EnchantFrame073(button.transform,item.enchantmentId,134,rowHeight-8);
                DrawControllerRarity07(button.transform,item.definitionId,true,134,rowHeight-8);
                Icon(button.transform,item.definitionId,8,40,24);
                var name=UiKit.Text(button.transform,"Name",NameOf(item.definitionId),36,36,86,34,13,UiKit.Ink,TextAlignmentOptions.Center);
                name.textWrappingMode=TextWrappingModes.NoWrap;name.overflowMode=TextOverflowModes.Ellipsis;
                EnchantBadge(button.transform,item.enchantmentId,98,2,28);
                AttachControllerDrag(button,item);
                Art07AttachTooltip(button,item.instanceId,NameOf(item.definitionId),Description(item.definitionId),ControllerStateLabel(item),()=>ShowController(item,true));
            }
            if(ViewState.controllers.Count==0)UiKit.Text(list,"NoOwned","尚未装备控制器\n购买后会出现在这里。",8,32,256,120,21,UiKit.Muted,TextAlignmentOptions.Center);
        }
        private void ShopOwnedStrip073()
        {
            var strip=UiKit.Box(main,"OwnedStrip",ShopLeft073,948,1128,104,Color.white);var image=strip.GetComponent<Image>();
            image.sprite=UiKit.Template("ui_section_panel");image.type=image.sprite!=null?Image.Type.Sliced:Image.Type.Simple;if(image.sprite==null)image.color=UiKit.Background;
            UiKit.Text(strip,"OwnedLabel","当前持有",16,0,104,104,20,UiKit.Muted,TextAlignmentOptions.MidlineLeft);
            var dice=ViewState.dice.OrderBy(d=>d.createdSerial).ToArray();int shown=Math.Min(8,dice.Length);
            for(int i=0;i<8;i++)
            {
                float x=122+i*72;
                var cell=UiKit.Box(strip,"OwnedDieCell"+i,x,16,66,72,Color.white).GetComponent<Image>();
                cell.sprite=UiKit.Template("ui_slot_inventory");cell.type=cell.sprite!=null?Image.Type.Sliced:Image.Type.Simple;if(cell.sprite==null)cell.color=new Color(0,0,0,.1f);
                if(i>=shown)continue;
                var die=dice[i];string id=die.dieId;
                // Real dice, not face tiles: drag turns one, a click opens the dice library on it.
                DieView073(strip,"OwnedDie_"+id,"owned-"+id,die,x,16,66,72,.52f,false,false,()=>{if(!IsBusy&&!modal)ShowDie(id);});
            }
            if(dice.Length>shown)UiKit.Text(strip,"OwnedDiceMore","+"+(dice.Length-shown),122+8*72-4,34,40,36,17,UiKit.Muted,TextAlignmentOptions.Center);
            float itemsX=122+8*72+38;
            UiKit.Text(strip,"OwnedItemsTitle","道具\n"+ViewState.inventory.Count+(ViewState.debugWorkspace?"":"/"+ViewState.consumableSlots),itemsX,14,64,76,16,UiKit.Muted,TextAlignmentOptions.Center);
            int itemSlots=Math.Min(5,Math.Max(ViewState.consumableSlots,ViewState.inventory.Count));
            for(int i=0;i<itemSlots;i++)
            {
                float x=itemsX+70+i*70;
                if(i>=ViewState.inventory.Count)
                {
                    var hole=UiKit.Box(strip,"OwnedItemEmpty"+i,x,20,64,64,Color.white).GetComponent<Image>();
                    hole.sprite=UiKit.Template("ui_slot_inventory");hole.type=hole.sprite!=null?Image.Type.Sliced:Image.Type.Simple;if(hole.sprite==null)hole.color=new Color(0,0,0,.1f);
                    continue;
                }
                var item=ViewState.inventory[i];
                var button=UiKit.Button(strip,"ShopItem_"+item.instanceId,"",x,20,64,64,()=>ShowOwned(item,true),!IsBusy);
                UiKit.CardSurface(button,true);((Image)button.targetGraphic).sprite=UiKit.Template("ui_card_item");
                Icon(button.transform,item.contentId,10,6,44);
                Art07AttachTooltip(button,item.instanceId,NameOf(item.contentId),Description(item.contentId),"出售获得 1 金币",()=>ShowOwned(item,true));
            }
        }
        /// <summary>Next stage, permanent dice and submit capacity under the wallet (reference image 2, left column).</summary>
        private void ShopStatus073(Transform panel)
        {
            int next=ViewState.stage.stageIndex+1;bool last=next>=Config.stages.Length;
            string target=last?"—":CompactScore(Config.stages[next].normalTarget);
            ShopStatusRow073(panel,"NextStage",last?"最后一关已完成":"第 "+(next+1)+" 关目标",last?"":target,298,UiKit.Red);
            ShopStatusRow073(panel,"PermanentDice","永久骰",ViewState.dice.Count(d=>d.lifetime=="permanent")+(ViewState.debugWorkspace?"":" / "+Config.maxPermanentDice),364,UiKit.Blue);
            ShopStatusRow073(panel,"SubmitCapacity","提交容量",Session.EffectiveCapacity.ToString(),430,UiKit.Teal);
        }
        private void ShopStatusRow073(Transform panel,string id,string label,string value,float y,Color key)
        {
            UiKit.Inset(panel,id+"Inset",10,y,300,60);
            UiKit.Box(panel,id+"Accent",14,y+10,3,40,new Color(key.r,key.g,key.b,.9f));
            var caption=UiKit.Text(panel,id+"Label",label,24,y,170,60,17,UiKit.Muted,TextAlignmentOptions.MidlineLeft);caption.textWrappingMode=TextWrappingModes.NoWrap;
            var number=UiKit.Text(panel,id+"Value",value,150,y,152,60,value.Length>9?17:22,UiKit.TextColor,TextAlignmentOptions.MidlineRight);
            number.textWrappingMode=TextWrappingModes.NoWrap;number.overflowMode=TextOverflowModes.Overflow;
        }

        private void BindShopEntrance073()
        {
            var shelfNames=new[]{"ShopTitle","ControllerShelf","DiceCounter","RefreshControllers","OwnedStrip","LeaveShop"};
            var shelf=shelfNames.Select(n=>main.Find(n) as RectTransform).Where(r=>r!=null&&r.gameObject.activeInHierarchy).ToList();
            var products=main.Find("ShopProducts");
            if(products!=null)for(int i=0;i<products.childCount;i++){var section=products.GetChild(i) as RectTransform;if(section!=null&&section.gameObject.activeInHierarchy)shelf.Add(section);}
            var cards=new List<RectTransform[]>();
            var controllers=ViewState.shop.offers.Where(o=>o.category=="controller").ToArray();
            foreach(var offer in controllers)
                cards.Add(new[]{main.Find("Offer_"+offer.offerId) as RectTransform,main.Find("Buy_"+offer.offerId) as RectTransform,main.Find("OfferAvailability_"+offer.offerId) as RectTransform});
            int capacity=Config.shop.initialControllerOffers+ViewState.controllerOfferPurchases;
            for(int i=controllers.Length;i<capacity;i++)cards.Add(new[]{main.Find("EmptyControllerOffer"+i) as RectTransform});
            // One group per section keeps the whole first entrance inside its 0.52 s budget however many goods there are.
            foreach(string sectionName in new[]{"DiceCounter","ShopProducts/ConsumableSection","ShopProducts/ResearchSection","ShopProducts/UpgradeSection","ShopProducts/ServiceSection"})
            {
                var section=main.Find(sectionName);if(section==null)continue;
                var group=section.GetComponentsInChildren<RectTransform>(false).Where(r=>r.name.StartsWith("Product_",StringComparison.Ordinal)||r.name=="Study").ToArray();
                if(group.Length>0)cards.Add(group);
            }
            UiKit.Component<Art07ShopEntrance>(main.gameObject).Bind(ViewState.shop.generationId,reduced,instant07,()=>modal||inMenu,shelf.ToArray(),cards.ToArray());
        }
    }
}
