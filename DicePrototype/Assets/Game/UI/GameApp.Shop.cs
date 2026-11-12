using System;
using System.Linq;
using DiceCraft.Core;
using TMPro;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        // v0.73: one page with every section visible (reference image 2); the tabbed v0.7 shop is gone.
        private void Shop()=>Shop073();
        private void InventoryStrip(bool selling)
        {
            float y=878;controllerRects.Clear();
            string controllerTitle=ViewState.debugWorkspace?$"控制器 · 调试配置 · 持有 {ViewState.controllers.Count} · 普通占槽 {ControllerRules.OrdinaryUsed(ViewState.controllers)}":$"控制器 · 普通槽 {ControllerRules.OrdinaryUsed(ViewState.controllers)}/{ViewState.controllerSlots} · 持有 {ViewState.controllers.Count}/{Config.maxControllerInstances}";
            UiKit.Text(main,"ControllersTitle",controllerTitle,40,y-22,1100,30,21,UiKit.Muted);
            int slots=Math.Min(10,Math.Max(4,ViewState.controllers.Count));float width=1100f/Math.Min(slots,5);
            for(int i=0;i<slots;i++)
            {
                int row=i/5,col=i%5;float x=40+col*width,cy=y+18+row*63;
                if(i<ViewState.controllers.Count){var item=ViewState.controllers[i];var b=UiKit.Button(main,"Controller_"+item.instanceId,NameOf(item.definitionId)+"\n"+EnchantShort(item.enchantmentId)+CompactControllerState(item),x,cy,width-10,53,()=>ShowController(item,selling),!IsBusy);b.GetComponentInChildren<TextMeshProUGUI>().fontSize=16;EnchantBadge(b.transform,item.enchantmentId,5,5,22);AttachControllerDrag(b,item);}
                else {UiKit.Box(main,"ControllerEmpty"+i,x,cy,width-10,53,UiKit.Panel);UiKit.Text(main,"EmptyLabel"+i,"空槽",x+8,cy+10,width-26,32,18,UiKit.Muted,TextAlignmentOptions.Center);}
            }
            UiKit.Text(main,"ItemsTitle",$"局内道具  {ViewState.inventory.Count}/{ViewState.consumableSlots}",1190,y-22,690,30,21,UiKit.Muted);
            int itemCount=Math.Min(5,ViewState.consumableSlots);inventoryPage=Math.Min(inventoryPage,Math.Max(0,(ViewState.consumableSlots-1)/5));float itemW=690f/itemCount;
            UiKit.Button(main,"AllControllerStates","排列 / 全部状态",875,y-23,260,34,ShowControllerOrder);
            if(ViewState.consumableSlots>5)UiKit.Button(main,"InventoryPage","道具页 "+(inventoryPage+1)+" >",1630,y-24,240,34,()=>{inventoryPage=(inventoryPage+1)%((ViewState.consumableSlots+4)/5);Refresh();});
            for(int visible=0;visible<Math.Min(5,ViewState.consumableSlots-inventoryPage*5);visible++)
            {
                int i=visible+inventoryPage*5;float x=1190+visible*itemW;
                if(i<ViewState.inventory.Count){var item=ViewState.inventory[i];var b=UiKit.Button(main,"Item_"+item.instanceId,NameOf(item.contentId),x,y+18,itemW-8,75,()=>ShowOwned(item,selling));b.GetComponentInChildren<TextMeshProUGUI>().fontSize=19;}
                else{UiKit.Box(main,"ItemEmpty"+i,x,y+18,itemW-8,75,UiKit.Panel);UiKit.Text(main,"ItemEmptyText"+i,"空槽",x+5,y+42,itemW-18,28,18,UiKit.Muted,TextAlignmentOptions.Center);}
            }
        }
        private Sprite ContentSprite071(string id) => string.IsNullOrEmpty(id)?null:
            contentIcons?.FirstOrDefault(s=>s!=null&&s.name.Equals("illustration_"+id,StringComparison.OrdinalIgnoreCase))
            ??contentIcons?.FirstOrDefault(s=>s!=null&&(s.name==id||s.name.Equals("icon_"+id,StringComparison.OrdinalIgnoreCase)));

        private void Icon(Transform p,string id,float x,float y,float size)
        {
            var sprite=ContentSprite071(id);
            var r=UiKit.Rect(p,"Icon_"+id,x,y,size,size);
            if(sprite!=null)
            {
                var image=UiKit.Component<UnityEngine.UI.Image>(r.gameObject);image.sprite=sprite;image.color=Color.white;image.raycastTarget=false;image.preserveAspect=true;
                if((id=="C09"||id=="C46")&&!sprite.name.StartsWith("illustration_",StringComparison.OrdinalIgnoreCase))
                {
                    // Fixed multipliers are live, legible type inside the authored emblem.
                    float bx=id=="C09"?.34f:.26f,by=id=="C09"?.29f:.34f,bw=id=="C09"?.30f:.48f,bh=.30f;
                    var badge=UiKit.Box(r,"MultiplierBadge",size*bx,size*by,size*bw,size*bh,UiKit.Paper);
                    var number=UiKit.Text(badge,"Multiplier",id=="C09"?"×4":"×3",0,0,size*bw,size*bh,size*.16f,UiKit.Ink,TextAlignmentOptions.Midline);
                    number.fontSize=size*.16f;number.textWrappingMode=TextWrappingModes.NoWrap;
                }
            }
            else
            {
                // A visible, identified fallback cannot be confused with another content item.
                var old=r.GetComponent<UnityEngine.UI.Image>();if(old!=null){old.sprite=null;old.color=Color.clear;old.raycastTarget=false;}
                UiKit.Outline(r,"Missing",2,2,size-4,size-4,UiKit.Muted);
                UiKit.Text(r,"Id",id??"—",2,2,size-4,size-4,Math.Min(22,size/4),UiKit.Muted,TextAlignmentOptions.Center);
            }
        }
        private void ShowOwned(OwnedItem item,bool selling)
        {
            var p=Art07Modal(NameOf(item.contentId),480,180,960,720);Icon(p,item.contentId,24,112,176);
            var list=PaperDetailScroll071(p,"ItemDetail",232,104,704,492);float y=0;
            Art07DetailSection(list,"Skill","道具效果",Description(item.contentId),ref y,672,true);
            string window=Config.Consumable(item.contentId).window;
            Art07DetailSection(list,"Window","合法窗口",window=="Selecting"?"投掷后，至少已有一颗确认锁定的骰子。":window=="HandReady"?"本手第一次投掷前。":"本手投掷前或投掷后，先处理待锁选择。",ref y,672,true);
            string reason=selling?null:Art07ItemUnavailable(item);
            Art07DetailSection(list,"UseState",selling?"出售结果":"当前状态",selling?"未使用道具固定卖价 1 金币；出售后失去这件库存。":reason??"当前可以使用；成功后消耗一件库存。",ref y,672,true);
            list.sizeDelta=new Vector2(list.sizeDelta.x,y+16);
            if(selling)
            {
                UiKit.Button(p,"Sell","确认出售 · +1",24,628,912,64,()=>{CloseModal();Send(new SessionCommand{kind=CommandKind.Sell,itemInstanceId=item.instanceId});},!IsBusy);
                UiKit.Outline(p,"SellOutline",24,628,912,64,UiKit.Red,2);
            }
            else UiKit.ActionButton(p,"Use",reason==null?"使用道具":"当前不能使用",24,628,912,64,()=>
            {
                if(item.contentId=="I03")RemoteTargets(item);
                else{CloseModal();Send(new SessionCommand{kind=CommandKind.UseItem,itemInstanceId=item.instanceId});}
            },UiKit.Paper,reason==null);
        }
        private string Art07ItemUnavailable(OwnedItem item)
        {
            if(IsBusy||Session.HasPendingSave)return "等待当前操作完成。";
            if(Session.PendingIds.Length>0)return "先确认锁定或清除待选。";
            var s=ViewState;var h=s.hand;
            if(s.phase!="HandReady"&&s.phase!="Selecting")return "此道具只能在关内使用。";
            switch(item.contentId)
            {
                case "I01":return !h.hasRolled||h.orderedLockedIds.Count==0?"需要投掷并至少锁定一颗骰子。":h.redWave?"本手已经生效。":null;
                case "I02":return s.phase!="HandReady"?"只能在本手首掷前使用。":s.dice.Count>=(s.isDebug?Config.debug.maxDice:Config.maxActiveDice)||(!s.isDebug&&s.dice.Count(d=>d.lifetime=="stage_temporary")>=Config.maxTemporaryDice)?"临时骰或活动骰数量已满。":null;
                case "I03":return h.rollsRemaining<=0?"需要至少一次剩余投掷。":s.dice.Count(d=>!h.orderedLockedIds.Contains(d.dieId))<2?"需要两颗不同的未锁骰。":null;
                case "I04":return h.largeBuff?"本手已经生效。":h.rollsRemaining<=0||!s.dice.Any(d=>!h.orderedLockedIds.Contains(d.dieId)&&d.faces.Any(f=>f.permanentPip==6))?"需要可投掷且含永久 6 点面的未锁骰。":null;
                case "I05":return h.smallBuff?"本手已经生效。":h.rollsRemaining<=0||!s.dice.Any(d=>!h.orderedLockedIds.Contains(d.dieId)&&d.faces.Any(f=>f.permanentPip==1))?"需要可投掷且含永久 1 点面的未锁骰。":null;
                default:return null;
            }
        }
        private void InspectOffer(ShopOffer offer)
        {
            if(offer.category=="die_upgrade"||offer.contentId=="O04"){TargetDie(offer,null);return;}
            var old=offer.category=="controller"?ViewState.controllers.FirstOrDefault(c=>c.definitionId==offer.contentId):null;
            if(old!=null){Art07Replacement(offer,old);return;}
            if(offer.category=="new_die")
            {
                ShowNewDieOffer07(offer);return;
            }
            var p=Art07Modal(NameOf(offer.contentId),480,180,960,720);
            Icon(p,offer.contentId,24,112,176);
            if(offer.category=="controller")EnchantBadge(p,offer.controller.enchantmentId,160,102,40);
            var list=PaperDetailScroll071(p,"OfferDetail",232,104,704,492);float y=0;
            Art07DetailSection(list,"Skill","效果",Description(offer.contentId),ref y,672,true);
            if(offer.category=="controller")
            {
                Art07DetailSection(list,"Enchant","附魔",EnchantLabel(offer.controller.enchantmentId),ref y,672,true);
                Art07DetailSection(list,"Capacity","购入后",ControllerPurchaseText(offer)+"\n"+Art07OfferState(offer.controller),ref y,672,true);
            }
            else Art07DetailSection(list,"Category","作用范围",offer.category=="consumable"?$"买入库存，使用时消耗。库存 {ViewState.inventory.Count} / {ViewState.consumableSlots}。":"购买成功后，在当前 Run 立即生效。",ref y,672,true);
            string error=Session.PurchaseError(offer.offerId);
            Art07DetailSection(list,"Price","价格与余额","价格 "+Session.GetOfferPrice(offer)+" 金币 · 当前余额 "+ViewState.gold+(error==null?"":"\n"+error),ref y,672,true);
            list.sizeDelta=new Vector2(list.sizeDelta.x,y+16);
            UiKit.ActionButton(p,"ConfirmPurchase",offer.sold?"已购买":"支付 "+Session.GetOfferPrice(offer)+" 并购买",24,628,912,64,()=>{CloseModal();Send(new SessionCommand{kind=CommandKind.Buy,offerId=offer.offerId});},UiKit.Gold,error==null&&!IsBusy);
        }
        private void BuyFooter(Transform p,ShopOffer offer,string die,string face)
        {
            long price=Session.GetOfferNetPrice(offer);
            string error=Session.PurchaseError(offer.offerId,die,face);
            UiKit.Text(p,"Price",$"价格 {price} 金币    ·    余额 {ViewState.gold} → {(System.Numerics.BigInteger)ViewState.gold-price}",40,585,1500,50,31,UiKit.Gold);
            if(error!=null)UiKit.Text(p,"PurchaseUnavailable",error,40,635,1500,32,21,UiKit.Gold);
            UiKit.Button(p,"ConfirmPurchase",error==null?(price<0?"购买并获得 "+(-price):"支付 "+price+" 并购买"):"暂不能购买",40,680,1500,64,()=>{CloseModal();Send(new SessionCommand{kind=CommandKind.Buy,offerId=offer.offerId,targetDieId=die,targetFaceId=face});},error==null,true);
        }
        private void ShowStudy()
        {
            var p=OpenModal("自选研习 · 本店一次");var legal=Session.LegalStudyHands();
            var list=UiKit.Scroll(p,"StudyList",40,100,1500,620,Math.Max(600,legal.Length*85));
            for(int i=0;i<legal.Length;i++)
            {
                string id=legal[i];bool color=id=="all_blue"||id=="all_red";int level=(color?ViewState.colorLevels:ViewState.handLevels).First(x=>x.handId==id).level,price=Session.GetStudyPrice(id);int baseMult=color?Config.Color(id).baseMult100:Config.Hand(id).baseMult100,increment=color?Config.Color(id).incrementPerLevel100:Config.Hand(id).incrementPerLevel100;
                UiKit.Text(list,"StudyName"+id,$"{NameOf(id)}   Lv.{level} → {level+1}    倍率 {Factor(baseMult+increment*level)} → {Factor(baseMult+increment*(level+1))}",12,i*85+15,1040,50,27);
                UiKit.Button(list,"StudyBuy"+id,price+" 金币",1110,i*85,340,65,()=>{CloseModal();Send(new SessionCommand{kind=CommandKind.Study,handId=id});},ViewState.gold>=price,true);
            }
            if(legal.Length==0)UiKit.Text(list,"NoLegal","当前没有满足容量、永久骰数且未满级的牌型。",15,30,1400,90,28,UiKit.Muted);
        }
        private void TargetDie(ShopOffer offer,string selectedDie,float scrollPosition=1)
        {
            DrawDiceSheet07(selectedDie,offer,null,scrollPosition);
        }
        private void PreviewFacePurchase(ShopOffer offer,DieInstance die,string faceId)
        {
            DrawDiceSheet07(die.dieId,offer,faceId,1);
        }
    }
}
