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
        private static Color PaperMuted071 => Color.Lerp(UiKit.Ink,UiKit.Paper,.28f);

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
        // The five-argument form is the stable entry point (the asset gallery calls it by name).
        private void DrawControllerFullBody07(Transform parent,string definition,string state,string enchantment,int ordinal=0)=>DrawControllerBody073(parent,definition,state,enchantment,ordinal,0);
        private void DrawControllerBody073(Transform parent,string definition,string state,string enchantment,int ordinal,float bodyHeight)
        {
            // The same paper card is used by the HUD, shop and frozen settlement backdrop.
            // Read its actual geometry instead of baking the old 160 x 224 layout into its ink.
            var rect=parent as RectTransform;
            float width=rect!=null&&rect.rect.width>0?rect.rect.width:160;
            float height=rect!=null&&rect.rect.height>0?rect.rect.height:224;
            // v0.73: a shop card keeps price and purchase below its body; only the body height is laid out here.
            if(bodyHeight>0)height=bodyHeight;
            bool full=width>=190&&height>=250;
            float padding=full?12:10,bottom=height-(full?14:20);
            float fontScale=Mathf.Max(1,UiKit.FontScale);
            float metadataHeight=Mathf.Max(30,14*fontScale*1.55f);
            DrawControllerRarity07(parent,definition,false,width,height);
            var rarity=(RectTransform)parent.Find("RarityLabel");
            rarity.anchoredPosition=new Vector2(48,-6);
            rarity.sizeDelta=new Vector2(width-48-padding-40,metadataHeight);
            var rarityText=rarity.GetComponent<TextMeshProUGUI>();
            rarityText.fontSize=14*UiKit.FontScale;
            var ticks=(RectTransform)parent.Find("RarityTicks");
            ticks.anchoredPosition=new Vector2(padding,-(6+metadataHeight*.5f-2));
            EnchantBadge(parent,enchantment,width-padding-32,6,32);

            // Reserve a complete CJK line at enlarged text sizes. Decoration yields to readable ink.
            float titleSize=full?24:20,titleHeight=titleSize*fontScale*1.55f;
            float titleY=full?32:30;
            var name=UiKit.Text(parent,"Name",NameOf(definition),padding,titleY,width-padding*2,titleHeight,titleSize,UiKit.Ink,TextAlignmentOptions.Center);
            name.fontStyle=FontStyles.Bold;
            name.textWrappingMode=TextWrappingModes.NoWrap;name.overflowMode=TextOverflowModes.Ellipsis;
            float footerHeight=Mathf.Max(24,16*fontScale*1.55f);
            float footerY=bottom-footerHeight;
            float imageTop=titleY+titleHeight+2;
            float imageSize=Mathf.Min(width-padding*2,footerY-imageTop-3);
            Icon(parent,definition,(width-imageSize)*.5f,imageTop,imageSize);
            if(ordinal>0)UiKit.Text(parent,"Ordinal",ordinal.ToString(),padding,footerY,16,footerHeight,12,UiKit.Ink,TextAlignmentOptions.Center);
            string shortState=state=="点击查看条件"?"查看条件":(state??"").Replace("→ ","→");
            float stateX=ordinal>0?padding+18:padding;
            var value=UiKit.Text(parent,"State",shortState,stateX,footerY,width-padding-stateX,footerHeight,16,UiKit.Ink,TextAlignmentOptions.Center);
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
            var list=PaperDetailScroll071(p,"ReplacementDetails",224,104,592,430);float y=0;
            long cost=Session.GetOfferNetPrice(offer),trade=Session.GetOfferTradeIn(offer);
            Art07DetailSection(list,"Trade","本次交易","新卡标价 "+Session.GetOfferPrice(offer)+"\n旧卡回收 "+trade+"\n"+(cost>=0?"净支出 "+cost:"净收入 "+(-cost))+"\n余额 "+ViewState.gold+" → "+((System.Numerics.BigInteger)ViewState.gold-cost),ref y,560,true);
            Art07DetailSection(list,"State","新实例重新初始化","旧实例的成长及附魔不会迁移。\n旧基金累计 "+old.state.fundAccrual+"；新基金累计 "+offer.controller.state.fundAccrual+"。\n"+Art07OfferState(offer.controller),ref y,560,true);
            Art07DetailSection(list,"Capacity","装备占槽",ControllerPurchaseText(offer),ref y,560,true);
            string error=Session.PurchaseError(offer.offerId);
            if(error!=null)Art07DetailSection(list,"Unavailable","当前不可替换",error,ref y,560,true);
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
                bool besidePool=!Compact07&&UiKit.FontScale<=1&&ViewState.phase=="Selecting"&&ViewState.dice.Count<=5&&
                    ViewState.dice.Count-ViewState.hand.orderedLockedIds.Count<=3;
                float x=besidePool?1476:Mathf.Clamp(position.x+12,24,1476),y=besidePool?456:Mathf.Clamp(position.y+10,24,666);
                art07TooltipOwner=owner;art07Tooltip=UiKit.Frame(overlay,"ContentTooltip",x,y,420,besidePool?286:390,UiKit.Paper,UiKit.Ink);
                UiKit.Text(art07Tooltip,"Name",title,16,12,388,52,25,UiKit.Ink);
                var text=UiKit.Text(art07Tooltip,"Description",description,16,besidePool?64:76,388,besidePool?108:150,20,UiKit.Ink);text.overflowMode=TextOverflowModes.Ellipsis;
                var stateText=UiKit.Text(art07Tooltip,"State",state,16,besidePool?178:234,388,besidePool?54:88,19,UiKit.Ink);stateText.overflowMode=TextOverflowModes.Ellipsis;
                UiKit.Text(art07Tooltip,"More","点击卡片查看完整说明",16,besidePool?238:334,388,44,18,UiKit.Ink,TextAlignmentOptions.Center);
                UiKit.Component<Art07PanelMotion>(art07Tooltip.gameObject).Enter(.08f,.06f,true,reduced);
            };
            hover.hide=()=>{if(art07TooltipOwner==owner&&art07Tooltip!=null){art07Tooltip.gameObject.SetActive(false);art07TooltipOwner=null;}};
        }
        private RectTransform Art07PipTile(Transform parent,string name,int value,Color color,float x,float y,float size)
        {
            // Tiny rules examples need a clean face: a sliced paper-card border
            // would consume the corner-pip space at handbook's 30px size.
            // v0.72: tiles with room for it read as small dice (rounded body, round pips);
            // the handbook's tiny examples keep the plain square face.
            bool shaped=size>=36;var block=shaped?UiKit.Template("ui_frame_block"):null;var round=shaped?UiKit.Template("ui_pip_dot"):null;
            float edge=block!=null?UiKit.EdgeUnit:1;
            var r=UiKit.Box(parent,name,x,y,size,size,UiKit.Ink);
            var body=r.GetComponent<UnityEngine.UI.Image>();body.sprite=block;body.type=block!=null?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;
            var face=UiKit.Box(r,"ExampleFace",edge,edge,size-2*edge,size-2*edge,UiKit.Paper).GetComponent<UnityEngine.UI.Image>();
            face.sprite=block;face.type=block!=null?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;
            float dot=Math.Max(4,size*(round!=null?.17f:.095f));
            Action<float,float> pip=(px,py)=>UiKit.Box(r,"Pip"+px+"_"+py,size*px-dot/2,size*py-dot/2,dot,dot,color).GetComponent<UnityEngine.UI.Image>().sprite=round;
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
