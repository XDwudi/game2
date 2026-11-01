using System;
using System.Linq;
using DiceCraft.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private RectTransform art07Tooltip;
        private string art07TooltipOwner;
        private string art07HandSelection;

        private static int Art07OfferTab(ShopOffer offer)
        {
            switch(offer.category)
            {
                case "consumable":return 0;
                case "hand_upgrade":case "color_upgrade":return 1;
                case "die_upgrade":return 2;
                case "service":return 3;
                case "new_die":return 4;
                default:return -1;
            }
        }
        private string ShopInterestText07()
        {
            // Match settlement's resolved ability sources, including current C41 copies.
            long rate=1L+ViewSources.Count(s=>s.abilityId=="C01")*Config.Controller("C01").parameters.extraInterestPerTen;
            return "每"+Config.interestDivisor+"金币利息 +"+rate+"\n计息本金上限 "+Config.maxInterestPrincipal;
        }
        private string Art07Rarity(string id)
        {
            var rarity=Config.Controller(id).rarity;
            return rarity=="rare"?"稀有":rarity=="uncommon"?"进阶":"常见";
        }
        private void DrawControllerRarity07(Transform parent,string id,bool compact,float width,float height)
        {
            string rarity=Config.Controller(id).rarity;int count=rarity=="rare"?3:rarity=="uncommon"?2:1;
            Color tint=rarity=="rare"?UiKit.Violet:rarity=="uncommon"?UiKit.Blue:UiKit.Teal;
            bool mini=compact&&width<180,detail=!compact&&height>=260;
            float tickX=mini?8:12,tickWidth=mini?6:8,tickStep=mini?9:11;
            var strip=UiKit.Rect(parent,"RarityTicks",tickX,mini?16:detail?28:compact?18:22,tickStep*2+tickWidth,4);
            for(int i=0;i<count;i++)UiKit.Box(strip,"Tick"+i,i*tickStep,0,tickWidth,4,tint);
            // The category shares the top metadata row with ticks, never the footer/shadow.
            var label=UiKit.Text(parent,"RarityLabel",Art07Rarity(id),mini?38:48,detail?12:mini?4:compact?2:8,mini?52:detail?80:compact?70:64,detail?36:mini?28:32,
                mini?12:detail?16:compact?13:14,UiKit.Ink,TextAlignmentOptions.MidlineLeft);
            label.textWrappingMode=TextWrappingModes.NoWrap;label.overflowMode=TextOverflowModes.Ellipsis;
        }
        private void DrawControllerFullBody07(Transform parent,string definition,string state,string enchantment,int ordinal=0)
        {
            // Card paper ends above the template shadow: all readable ink stays above y204.
            DrawControllerRarity07(parent,definition,false,160,224);
            EnchantBadge(parent,enchantment,120,6,32);
            // Noto CJK line metrics are 1.448 x size: preserve the full line box at 150%.
            // At 125%+ the 64-high title box uses transparent ascent/descent space;
            // visible title ink remains between the metadata and illustration.
            bool enlarged=UiKit.FontScale>1;
            var name=UiKit.Text(parent,"Name",NameOf(definition),12,enlarged?24:38,136,enlarged?64:36,20,UiKit.Ink,TextAlignmentOptions.Center);
            name.textWrappingMode=TextWrappingModes.NoWrap;name.overflowMode=TextOverflowModes.Ellipsis;
            float icon=UiKit.FontScale>=1.5f?80:enlarged?88:96;
            Icon(parent,definition,(160-icon)/2,enlarged?84:80,icon);
            float footerY=UiKit.FontScale>=1.5f?166:enlarged?172:176;
            if(ordinal>0)UiKit.Text(parent,"Ordinal",ordinal.ToString(),8,footerY,16,204-footerY,12,UiKit.Ink,TextAlignmentOptions.Center);
            string shortState=state=="点击查看条件"?"查看条件":state.Replace("→ ","→");
            var value=UiKit.Text(parent,"State",shortState,ordinal>0?24:12,footerY,ordinal>0?128:136,204-footerY,16,UiKit.Ink,TextAlignmentOptions.Center);
            value.textWrappingMode=TextWrappingModes.NoWrap;value.overflowMode=TextOverflowModes.Ellipsis;
        }
        private string Art07OfferState(ControllerInstance item)
        {
            if(item==null)return "持有即装备";
            switch(item.definitionId)
            {
                case "C45":return string.IsNullOrEmpty(item.state?.activeColor)?"购入定色":item.state.activeColor=="red"?"当前 红 ●":"当前 蓝 ◆";
                case "C51":return "当前 ×"+((item.state?.turnoverUnits2??3)/2m).ToString("0.#");
                case "C52":return "卖价 "+ControllerRules.SalePrice(item,Config);
                case "C41":return "模拟右邻";
                case "C53":return "随机倍率";
                default:return "技能详情";
            }
        }
        private void Art07ShopShelf()
        {
            UiKit.Frame(main,"ControllerShelf",368,24,1528,400,UiKit.Panel,UiKit.PanelLight);
            UiKit.Text(main,"ControllerShelfTitle","控制器货架",392,36,550,52,32,UiKit.Paper);
            int price=Session.GetRefreshPrice();
            UiKit.ActionButton(main,"RefreshControllers",price==0?"本店首次免费":"刷新控制器  "+price,1432,32,440,60,()=>
            {
                Send(new SessionCommand{kind=CommandKind.RefreshControllers});
            },UiKit.Blue,!IsBusy&&!Session.HasPendingSave&&ViewState.gold>=price);
            var offers=ViewState.shop.offers.Where(o=>o.category=="controller").ToArray();
            for(int i=0;i<offers.Length;i++)
            {
                var offer=offers[i];float x=392+i*184;
                var card=UiKit.ActionButton(main,"Offer_"+offer.offerId,"",x,96,160,224,()=>InspectOffer(offer),UiKit.Paper,!IsBusy);
                UiKit.CardSurface(card,false);
                DrawControllerFullBody07(card.transform,offer.contentId,Art07OfferState(offer.controller),offer.controller?.enchantmentId);
                UiKit.Text(main,"OfferAvailability_"+offer.offerId,Art07Availability07(offer),x,394,160,44,18,UiKit.Gold,TextAlignmentOptions.Center);
                Art07AttachTooltip(card,offer.offerId,NameOf(offer.contentId),Description(offer.contentId),EnchantLabel(offer.controller?.enchantmentId),()=>InspectOffer(offer));
                Art07BuyButton(main,offer,x,328,160,64);
                if(offer.sold)
                {
                    var stamp=UiKit.Frame(card.transform,"SoldStamp",8,88,144,48,UiKit.Paper,UiKit.Ink);
                    UiKit.Text(stamp,"Text","已购买",4,4,136,40,23,UiKit.Ink,TextAlignmentOptions.Center);
                    stamp.localRotation=Quaternion.Euler(0,0,7);
                }
            }
            int capacity=Config.shop.initialControllerOffers+ViewState.controllerOfferPurchases;
            for(int i=offers.Length;i<capacity;i++)
            {
                var p=UiKit.Frame(main,"EmptyControllerOffer"+i,392+i*184,96,160,224,UiKit.Background,UiKit.PanelLight);
                UiKit.Text(p,"Empty","本店暂无\n合法商品",12,40,136,110,22,UiKit.Muted,TextAlignmentOptions.Center);
                UiKit.Text(p,"Applied","容量已生效",12,166,136,46,18,UiKit.Muted,TextAlignmentOptions.Center);
            }
        }
        private void Art07BuyButton(Transform parent,ShopOffer offer,float x,float y,float width,float height)
        {
            long cost=Session.GetOfferNetPrice(offer);
            // A capacity failure never replaces the visible transaction price.
            string label=offer.sold?"已购买":cost<0?"获得 "+(-cost):"购买 "+cost;
            UiKit.ActionButton(parent,"Buy_"+offer.offerId,label,x,y,width,height,()=>Art07BuyOffer(offer),UiKit.Gold,!offer.sold&&!IsBusy&&!Session.HasPendingSave);
        }
        private string Art07Availability07(ShopOffer offer)
        {
            if(offer.sold)return "已购买";
            long cost=Session.GetOfferNetPrice(offer);
            if(cost>ViewState.gold)return "还差 "+(cost-ViewState.gold);
            if(offer.category=="controller")
            {
                var old=ViewState.controllers.FirstOrDefault(c=>c.definitionId==offer.contentId);
                var final=ViewState.controllers.Where(c=>c!=old).Concat(new[]{offer.controller}).ToArray();
                if(final.Length>Config.maxControllerInstances)return "总持有已满";
                if(ControllerRules.OrdinaryUsed(final)>Config.initialControllerSlots)return "普通槽已满";
                if(old!=null)return "替换旧实例";
            }
            if(offer.category=="consumable"&&ViewState.inventory.Count>=ViewState.consumableSlots)return "道具库存满";
            if(offer.category=="die_upgrade"||offer.contentId=="O04")return "需选择目标";
            string error=Session.PurchaseError(offer.offerId);
            return error==null?"":offer.category=="new_die"?"永久骰已满":"查看限制";
        }
        private void Art07BuyOffer(ShopOffer offer)
        {
            if(IsBusy||Session.HasPendingSave||offer.sold)return;
            bool replacement=offer.category=="controller"&&ViewState.controllers.Any(c=>c.definitionId==offer.contentId);
            if(replacement||offer.category=="die_upgrade"||offer.contentId=="O04"||Session.PurchaseError(offer.offerId)!=null)
            {InspectOffer(offer);return;}
            Send(new SessionCommand{kind=CommandKind.Buy,offerId=offer.offerId});
        }
        private void Art07ProductCard(ShopOffer offer,float x,float y,float width,Transform parent=null)
        {
            parent=parent??main;
            var p=UiKit.Frame(parent,"Product_"+offer.offerId,x,y,width,416,UiKit.Panel,UiKit.PanelLight);
            string title=offer.category=="new_die"?"加入骰子":NameOf(offer.contentId);
            UiKit.Text(p,"Name",title,16,12,width-32,64,27,UiKit.Paper,TextAlignmentOptions.Center);
            if(offer.category=="new_die")
            {
                for(int i=0;i<6;i++)Art07PipTile(p,"PreviewFace"+i,offer.die.faces[i].permanentPip,offer.die.faces[i].baseColor=="red"?UiKit.Red:UiKit.Blue,30+i*62,84,52);
                UiKit.Text(p,"DiceCapacity","只增加骰池\n提交容量保持 K="+Session.EffectiveCapacity,24,160,width-48,96,24,UiKit.Muted,TextAlignmentOptions.Center);
            }
            else if(offer.category=="hand_upgrade"||offer.category=="color_upgrade")
            {
                bool color=offer.category=="color_upgrade";
                int level=(color?ViewState.colorLevels:ViewState.handLevels).First(l=>l.handId==offer.contentId).level;
                int basis=color?Config.Color(offer.contentId).baseMult100:Config.Hand(offer.contentId).baseMult100;
                int step=color?Config.Color(offer.contentId).incrementPerLevel100:Config.Hand(offer.contentId).incrementPerLevel100;
                UiKit.Text(p,"Level","Lv."+level+"  →  "+(level+1),24,100,width-48,64,32,UiKit.Teal,TextAlignmentOptions.Center);
                UiKit.Text(p,"Multiplier","×"+Factor(basis+step*level)+"  →  ×"+Factor(basis+step*(level+1)),24,182,width-48,88,29,UiKit.Gold,TextAlignmentOptions.Center);
            }
            else
            {
                Icon(p,offer.contentId,width/2-56,80,112);
                // Long conditions remain intact in the read-only detail sheet.
                string text=Description(offer.contentId);int stop=text.IndexOf('。');if(stop>=0)text=text.Substring(0,stop+1);
                var description=UiKit.Text(p,"Summary",text,20,204,width-40,64,22,UiKit.Muted,TextAlignmentOptions.Center);
                description.overflowMode=TextOverflowModes.Ellipsis;
            }
            string availability=Art07Availability07(offer);
            UiKit.Button(p,"Inspect",string.IsNullOrEmpty(availability)?"查看说明":availability,16,276,width-32,60,()=>InspectOffer(offer),!IsBusy);
            Art07BuyButton(p,offer,16,344,width-32,64);
        }
        private void Art07ShopOwned()
        {
            controllerRects.Clear();
            string capacity=ViewState.debugWorkspace?"调试配置 · 持有 "+ViewState.controllers.Count+"\n普通占槽 "+ControllerRules.OrdinaryUsed(ViewState.controllers):"普通槽 "+ControllerRules.OrdinaryUsed(ViewState.controllers)+"/"+ViewState.controllerSlots+" · 总数 "+ViewState.controllers.Count+"/"+Config.maxControllerInstances;
            UiKit.Text(main,"ControllerCapacity07",capacity,40,380,288,72,18,UiKit.Paper);
            UiKit.Text(main,"OwnedControllersTitle","已装备 · 拖拽排序",40,454,288,42,21,UiKit.Muted);
            float rowHeight=92;
            var list=UiKit.Scroll(main,"ShopOwnedControllers",40,504,288,326,Math.Max(326,((ViewState.controllers.Count+1)/2)*rowHeight));
            for(int i=0;i<ViewState.controllers.Count;i++)
            {
                var item=ViewState.controllers[i];
                var button=UiKit.ActionButton(list,"Controller_"+item.instanceId,"",(i%2)*142,(i/2)*rowHeight,134,rowHeight-8,()=>ShowController(item,true),UiKit.Paper,!IsBusy);
                UiKit.CardSurface(button,true);
                DrawControllerRarity07(button.transform,item.definitionId,true,134,rowHeight-8);
                Icon(button.transform,item.definitionId,8,40,24);
                var name=UiKit.Text(button.transform,"Name",NameOf(item.definitionId),38,36,88,34,14,UiKit.Ink,TextAlignmentOptions.Center);
                name.textWrappingMode=TextWrappingModes.NoWrap;name.overflowMode=TextOverflowModes.Ellipsis;
                EnchantBadge(button.transform,item.enchantmentId,98,2,28);
                AttachControllerDrag(button,item);
                Art07AttachTooltip(button,item.instanceId,NameOf(item.definitionId),Description(item.definitionId),ControllerStateLabel(item),()=>ShowController(item,true));
            }
            if(ViewState.controllers.Count==0)UiKit.Text(list,"NoOwned","尚未装备控制器\n购买后会出现在这里。",8,32,264,120,24,UiKit.Muted,TextAlignmentOptions.Center);
            UiKit.Text(main,"OwnedItemsTitle","道具 "+ViewState.inventory.Count+(ViewState.debugWorkspace?"":"/"+ViewState.consumableSlots)+" · 永久骰 "+ViewState.dice.Count(d=>d.lifetime=="permanent")+(ViewState.debugWorkspace?"":"/"+Config.maxPermanentDice),40,846,288,40,17,UiKit.Muted);
            int visible=Math.Min(5,ViewState.inventory.Count);
            for(int i=0;i<visible;i++)
            {
                var item=ViewState.inventory[i];
                var button=UiKit.Button(main,"ShopItem_"+item.instanceId,"",40+(i%3)*96,892+(i/3)*72,88,64,()=>ShowOwned(item,true),!IsBusy);
                Icon(button.transform,item.contentId,20,6,48);
                Art07AttachTooltip(button,item.instanceId,NameOf(item.contentId),Description(item.contentId),"出售获得 1 金币",()=>ShowOwned(item,true));
            }
        }
        private RectTransform Art07Modal(string title,float x,float y,float width,float height)
        {
            var p=OpenModal(title);UiKit.Clear(p);
            p.anchoredPosition=new Vector2(x,-y);p.sizeDelta=new Vector2(width,height);
            UiKit.Outline(p,"Frame",0,0,width,height,UiKit.Paper,3);
            UiKit.Text(p,"DialogTitle",title,24,8,width-128,80,32,UiKit.Paper);
            UiKit.Button(p,"CloseDialog","×",width-88,12,64,64,CloseModal);
            return p;
        }
        private void Art07Replacement(ShopOffer offer,ControllerInstance old)
        {
            var p=Art07Modal("替换控制器 · "+NameOf(offer.contentId),440,220,1040,640);
            var oldCard=UiKit.Frame(p,"OldCard",24,110,176,264,UiKit.Paper,UiKit.Ink);
            var newCard=UiKit.Frame(p,"NewCard",840,110,176,264,UiKit.Paper,UiKit.Ink);
            Icon(oldCard,old.definitionId,16,60,144);Icon(newCard,offer.contentId,16,60,144);
            UiKit.Text(oldCard,"Kind","当前实例",8,12,160,48,23,UiKit.Ink,TextAlignmentOptions.Center);
            UiKit.Text(newCard,"Kind","新报价",8,12,160,48,23,UiKit.Ink,TextAlignmentOptions.Center);
            EnchantBadge(oldCard,old.enchantmentId,132,50,32);EnchantBadge(newCard,offer.controller.enchantmentId,132,50,32);
            UiKit.Text(oldCard,"Enchant",EnchantShort(old.enchantmentId),8,212,160,44,20,UiKit.Ink,TextAlignmentOptions.Center);
            UiKit.Text(newCard,"Enchant",EnchantShort(offer.controller.enchantmentId),8,212,160,44,20,UiKit.Ink,TextAlignmentOptions.Center);
            var list=UiKit.Scroll(p,"ReplacementDetails",224,104,592,430,430);float y=0;
            long cost=Session.GetOfferNetPrice(offer),trade=Session.GetOfferTradeIn(offer);
            Art07DetailSection(list,"Trade","本次交易","新卡标价 "+Session.GetOfferPrice(offer)+"\n旧卡回收 "+trade+"\n"+(cost>=0?"净支出 "+cost:"净收入 "+(-cost))+"\n余额 "+ViewState.gold+" → "+((System.Numerics.BigInteger)ViewState.gold-cost),ref y,560);
            Art07DetailSection(list,"State","新实例重新初始化","旧实例的成长及附魔不会迁移。\n旧基金累计 "+old.state.fundAccrual+"；新基金累计 "+offer.controller.state.fundAccrual+"。\n"+Art07OfferState(offer.controller),ref y,560);
            Art07DetailSection(list,"Capacity","装备占槽",ControllerPurchaseText(offer),ref y,560);
            string error=Session.PurchaseError(offer.offerId);
            if(error!=null)Art07DetailSection(list,"Unavailable","当前不可替换",error,ref y,560);
            list.sizeDelta=new Vector2(list.sizeDelta.x,y+16);
            UiKit.Button(p,"CancelReplacement","取消",24,552,312,64,CloseModal);
            UiKit.ActionButton(p,"ConfirmPurchase",cost>=0?"支付 "+cost+" 并替换":"替换并获得 "+(-cost),360,552,656,64,
                ()=>{CloseModal();Send(new SessionCommand{kind=CommandKind.Buy,offerId=offer.offerId});},UiKit.Gold,error==null&&!IsBusy);
        }
        private void Art07AttachTooltip(UnityEngine.UI.Button button,string owner,string title,string description,string state,Action detail)
        {
            var hover=UiKit.Component<Art07HoverTip>(button.gameObject);
            hover.show=()=>
            {
                if(modal||IsControllerDragging||IsBusy)return;
                var corners=new Vector3[4];((RectTransform)button.transform).GetWorldCorners(corners);
                Vector2 position=ScreenToDesign(RectTransformUtility.WorldToScreenPoint(null,corners[3]));
                float x=Mathf.Clamp(position.x+12,24,1476),y=Mathf.Clamp(position.y+10,24,666);
                art07TooltipOwner=owner;art07Tooltip=UiKit.Frame(overlay,"ContentTooltip",x,y,420,390,UiKit.Paper,UiKit.Ink);
                UiKit.Text(art07Tooltip,"Name",title,16,12,388,52,25,UiKit.Ink);
                var text=UiKit.Text(art07Tooltip,"Description",description,16,76,388,150,20,UiKit.Ink);text.overflowMode=TextOverflowModes.Ellipsis;
                var stateText=UiKit.Text(art07Tooltip,"State",state,16,234,388,88,19,UiKit.Ink);stateText.overflowMode=TextOverflowModes.Ellipsis;
                UiKit.Text(art07Tooltip,"More","点击卡片查看完整说明",16,334,388,44,18,UiKit.Ink,TextAlignmentOptions.Center);
            };
            hover.hide=()=>{if(art07TooltipOwner==owner&&art07Tooltip!=null){art07Tooltip.gameObject.SetActive(false);art07TooltipOwner=null;}};
        }
        private RectTransform Art07PipTile(Transform parent,string name,int value,Color color,float x,float y,float size)
        {
            var r=UiKit.Frame(parent,name,x,y,size,size,UiKit.Paper,UiKit.Ink);
            float dot=Math.Max(4,size*.095f);
            Action<float,float> pip=(px,py)=>UiKit.Box(r,"Pip"+px+"_"+py,size*px-dot/2,size*py-dot/2,dot,dot,color);
            if(value%2==1)pip(.5f,.5f);
            if(value>=2){pip(.28f,.28f);pip(.72f,.72f);}
            if(value>=4){pip(.28f,.72f);pip(.72f,.28f);}
            if(value==6){pip(.28f,.5f);pip(.72f,.5f);}
            return r;
        }
    }
    [AddComponentMenu("")]
    public sealed class Art07HoverTip:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler
    {
        public Action show,hide;
        private bool hovered,shown;
        private float due;
        public void OnPointerEnter(PointerEventData e){hovered=true;due=Time.unscaledTime+.3f;}
        public void OnPointerExit(PointerEventData e){hovered=false;due=Time.unscaledTime+.06f;}
        public void OnPointerDown(PointerEventData e){hovered=false;shown=false;hide?.Invoke();}
        private void Update(){if(Time.unscaledTime<due)return;if(hovered&&!shown){shown=true;show?.Invoke();}else if(!hovered&&shown){shown=false;hide?.Invoke();}}
        private void OnDisable(){hovered=shown=false;hide?.Invoke();}
    }
}
