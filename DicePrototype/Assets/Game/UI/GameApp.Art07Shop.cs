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
        // V1.1 W14: ShopInterestText07 moved to GameApp.Shop14.cs (the gold hover of the shop's top bar).
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
                case "C51":return "当前 ×"+PlayerNumberFormat.Ratio(item.state?.turnoverUnits2??3,2);
                case "C52":return "卖价 "+ControllerRules.SalePrice(item,Config);
                case "C41":return "模拟右邻";
                case "C53":return "随机倍率";
                default:
                    if(ControllerRules.IsGrowthMultiplier(item.definitionId))return "从 ×1 成长";
                    if(ControllerRules.IsGrowthBase(item.definitionId))return "从 +0 成长";
                    return "技能详情";
            }
        }
        /// <summary>
        /// v1.1 (W09, user decision D5): the state line of a card that was just bought. A 成长基金 (C52) shows what the
        /// session pays for it now, read from the committed state: bought from this shop's shelf, it resells for at most the
        /// price paid until the player leaves. (Offer cards keep the ordinary resale price above, the price after the shop.)
        /// </summary>
        private string AcquiredCardState11(ControllerInstance item,RunState after)
            =>item?.definitionId=="C52"&&after!=null?"卖价 "+Session.ControllerSalePriceIn(after,item):Art07OfferState(item);
        /// <summary>V1.1 W14: one source for the price buttons' reasons, the shop's and the gift dialog's (SHOP_LAYOUT 6).</summary>
        private string Art07Availability07(ShopOffer offer)=>ShopAvailability14(offer);
        private void Art07BuyOffer(ShopOffer offer)
        {
            if(IsBusy||Session.HasPendingSave||offer.sold)return;
            // V1.1 W14 (SHOP_LAYOUT 7): a face upgrade or 删除骰子 of the shelf is chosen on the table; a gift group's face
            // upgrade keeps the dice sheet until W15.
            if(string.IsNullOrEmpty(offer.groupId)&&(offer.category=="die_upgrade"||offer.contentId=="O04")){BeginFacePick14(offer);return;}
            if(offer.category=="die_upgrade"||offer.contentId=="O04"||Session.PurchaseError(offer.offerId)!=null)
            {InspectOffer(offer);return;}
            Send(new SessionCommand{kind=CommandKind.Buy,offerId=offer.offerId});
        }
        private RectTransform Art07Modal(string title,float x,float y,float width,float height)
        {
            var p=OpenModal(title);UiKit.Clear(p);
            p.anchoredPosition=new Vector2(x,-y);p.sizeDelta=new Vector2(width,height);
            UiKit.Outline(p,"Frame",0,0,width,height,UiKit.Paper,3);
            UiKit.Role(UiKit.Text(p,"DialogTitle",title,24,8,width-128,80,32,UiKit.Paper),TextRole.ScreenTitle);
            UiKit.Button(p,"CloseDialog","×",width-88,12,64,64,CloseModal);
            return p;
        }
        // v1.1 (G3): the replacement dialog (Art07Replacement) was removed with the replacement rule; an owned card's
        // offer opens the ordinary offer details, which say how many copies you will hold.
        private void Art07AttachTooltip(UnityEngine.UI.Button button,string owner,string title,string description,string state,Action detail,string more="点击卡片查看完整说明")
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
                if(!string.IsNullOrEmpty(more))UiKit.Text(art07Tooltip,"More",more,16,besidePool?238:334,388,44,18,UiKit.Ink,TextAlignmentOptions.Center);
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
