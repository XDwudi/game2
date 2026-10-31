using System;
using System.Linq;
using DiceCraft.Core;
using TMPro;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private void Shop()
        {
            string[] tabs={"控制器","道具","牌型研习","骰子工坊"};
            for(int i=0;i<tabs.Length;i++){int index=i;UiKit.Button(main,"Tab"+i,tabs[i],36+i*325,164,310,59,()=>{shopTab=index;PlayerPrefs.SetInt("shopTab",index);Refresh();},true,shopTab==i);}
            UiKit.Button(main,"LeaveShop","前往下一关 →",1420,164,460,59,()=>{CloseModal();Send(new SessionCommand{kind=CommandKind.LeaveShop});},true,true);
            UiKit.Text(main,"ShopHint","控制器可单独刷新；其他商品保留。普通出售1金币，基金售价包含本实例累积。",40,242,1250,40,21,UiKit.Muted);
            if(shopTab==0)UiKit.Button(main,"RefreshControllers","刷新控制器 · "+Session.GetRefreshPrice()+" 金币",1360,236,520,46,()=>{var r=Send(new SessionCommand{kind=CommandKind.RefreshControllers});if(r.accepted)PlaySound("refresh");},ViewState.gold>=Session.GetRefreshPrice());
            var offers=ViewState.shop.offers.Where(o=>shopTab==0?o.category=="controller":shopTab==1?o.category=="consumable"||o.category=="service"&&o.contentId!="O04":shopTab==2?o.category=="hand_upgrade"||o.category=="color_upgrade":o.category=="die_upgrade"||o.category=="new_die"||o.contentId=="O04").ToList();
            int count=offers.Count+(shopTab==2?1:0),rows=Math.Max(1,(count+2)/3);
            var content=UiKit.Scroll(main,"ShopScroll",36,297,1844,503,rows*350);
            for(int i=0;i<offers.Count;i++)OfferCard(content,offers[i],i,(i%3)*610,(i/3)*350);
            if(shopTab==2){int i=offers.Count;var p=UiKit.Box(content,"SelfStudy",i%3*610,i/3*350,590,330,UiKit.Panel);UiKit.Text(p,"Name","自选研习",25,28,540,55,32,UiKit.Gold);UiKit.Text(p,"Description","任选一种当前合法牌型升级。\n按正常升级价 +1；本店一次。\n先购买其他强化后，价格与资格会实时更新。",25,106,540,130,25,UiKit.Muted);UiKit.Button(p,"Study","选择牌型",25,260,540,48,ShowStudy,ViewState.shop.selfStudyUsed==0);}
            if(offers.Count==0&&shopTab!=2)UiKit.Text(content,"NoOffers","本页没有可供应的商品。现有商品不会因购买或出售而补货。",35,60,1700,80,29,UiKit.Muted);
            InventoryStrip(true);
            if(tutorial)UiKit.Text(main,"TutorialShop","练习：选一件商品查看，再确认购买。物品不会在点选目标时提前扣费。",40,810,1700,38,23,UiKit.Teal);
        }
        private void OfferCard(Transform parent,ShopOffer offer,int slot,float x,float y)
        {
            var p=UiKit.Box(parent,"OfferSlot_"+slot,x,y,590,330,UiKit.Panel);p.name="Offer_"+offer.offerId;
            Icon(p,offer.contentId,22,23,58);
            UiKit.Text(p,"Name",offer.category=="new_die"?"加入骰子 · 六面候选":NameOf(offer.contentId),98,25,465,45,29,UiKit.Paper);
            int price=Session.GetOfferPrice(offer);
            string desc=offer.category=="new_die"?string.Join("  ",offer.die.faces.Select(f=>f.permanentPip+(f.baseColor=="red"?"R":"B")))+"\n"+string.Join(" · ",offer.priceReasons):Description(offer.contentId);
            if(offer.category=="controller"){EnchantBadge(p,offer.controller?.enchantmentId,520,25,42);desc+="\n"+EnchantLabel(offer.controller?.enchantmentId);}
            UiKit.Text(p,"Description",desc,24,94,542,148,23,UiKit.Muted);
            UiKit.Text(p,"Price",offer.sold?"已售罄":Session.GetOfferNetPrice(offer)+" 金币"+(Session.GetOfferTradeIn(offer)>0?" · 替换":""),24,265,210,40,24,UiKit.Gold);
            UiKit.Button(p,"Inspect",offer.sold?"已购入":"查看 / 购买",225,257,340,52,()=>InspectOffer(offer),!offer.sold);
        }
        private void InventoryStrip(bool selling)
        {
            float y=878;controllerRects.Clear(); UiKit.Text(main,"ControllersTitle",$"控制器 · 普通槽 {ControllerRules.OrdinaryUsed(ViewState.controllers)}/{ViewState.controllerSlots} · 持有 {ViewState.controllers.Count}/{Config.maxControllerInstances}",40,y-22,1100,30,21,UiKit.Muted);
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
        private void Icon(Transform p,string id,float x,float y,float size)
        {
            if(contentIcons==null)return;var sprite=contentIcons.FirstOrDefault(s=>s!=null&&s.name==id);if(sprite==null)return;
            var r=UiKit.Rect(p,"Icon_"+id,x,y,size,size);var image=UiKit.Component<UnityEngine.UI.Image>(r.gameObject);image.sprite=sprite;image.raycastTarget=false;
        }
        private void ShowOwned(OwnedItem item,bool selling)
        {
            var p=OpenModal(NameOf(item.contentId)); Icon(p,item.contentId,40,100,90);
            UiKit.Text(p,"Description",Description(item.contentId),160,110,1360,170,32);
            UiKit.Text(p,"ItemType",item.contentId.StartsWith("C")?"控制器 · 持有即装备 · 当前Run有效":"局内道具 · 合法窗口使用 · 使用后消耗",40,320,1550,65,27,UiKit.Muted);
            if(selling){UiKit.Text(p,"SellHint","出售后获得 1 金币。已产生的永久骰面变化会保留。",40,430,1550,90,28,UiKit.Gold);UiKit.Button(p,"Sell","确认出售 · +1",40,610,1500,70,()=>{CloseModal();Send(new SessionCommand{kind=CommandKind.Sell,itemInstanceId=item.instanceId});},true,true);}
            else if(item.contentId.StartsWith("I"))
            {
                string window=Config.Consumable(item.contentId).window;
                UiKit.Text(p,"Window","使用时机："+(window=="Selecting"?"投掷后，至少已有一颗确认锁定的骰子":window=="HandReady"?"本手第一次投掷前":"本手投掷前或投掷后，先处理待锁选择"),40,425,1510,60,25,UiKit.Muted);
                UiKit.Button(p,"Use","使用道具",40,610,1500,70,()=>{if(item.contentId=="I03")RemoteTargets(item);else{CloseModal();Send(new SessionCommand{kind=CommandKind.UseItem,itemInstanceId=item.instanceId});}},!IsBusy&&Session.PendingIds.Length==0,true);
            }
            else UiKit.Text(p,"HeldController","关内保持装备。可以在下一间商店出售。",40,500,1500,70,27,UiKit.Gold);
        }
        private void InspectOffer(ShopOffer offer)
        {
            if(offer.category=="die_upgrade"||offer.contentId=="O04"){TargetDie(offer,null);return;}
            var p=OpenModal(offer.category=="new_die"?"加入骰子 · 完整六面":NameOf(offer.contentId));
            if(offer.category=="new_die")
            {
                FaceCards(p,offer.die,125,null,null);
                UiKit.Text(p,"DiePriceReasons",string.Join(" · ",offer.priceReasons)+$"\n永久骰 {ViewState.dice.Count(d=>d.lifetime=="permanent")} → {ViewState.dice.Count(d=>d.lifetime=="permanent")+1}；提交容量 K={Session.EffectiveCapacity} 保持不变",40,465,1500,100,26,UiKit.Muted);
            }
            else
            {
                Icon(p,offer.contentId,40,130,110);UiKit.Text(p,"Description",Description(offer.contentId),180,130,1330,210,32);
                string category=offer.category=="controller"?ControllerPurchaseText(offer):offer.category=="consumable"?$"买入库存，使用时消耗：{ViewState.inventory.Count}/{ViewState.consumableSlots}":"购买后本Run立即生效，不占槽。";
                UiKit.Text(p,"Capacity",category,40,360,1500,100,25,UiKit.Muted);
                if(offer.category=="controller"){EnchantBadge(p,offer.controller.enchantmentId,40,480,48);UiKit.Text(p,"Enchantment",EnchantLabel(offer.controller.enchantmentId),105,480,1400,70,25,UiKit.Teal);}
                if(offer.contentId=="C12")UiKit.Text(p,"Conflict","注意：奇数会变成有效偶数，影响奇数控制器、2233、顺子和红组判定。",40,545,1500,32,22,UiKit.Gold);
                if(offer.contentId=="C09")UiKit.Text(p,"Conflict","注意：有效提交容量减少1；牌型升级的购买资格会重新检查。",40,545,1500,32,22,UiKit.Gold);
            }
            BuyFooter(p,offer,null,null);
        }
        private void BuyFooter(Transform p,ShopOffer offer,string die,string face)
        {
            long price=Session.GetOfferNetPrice(offer);
            string error=Session.PurchaseError(offer.offerId,die,face);
            UiKit.Text(p,"Price",$"价格 {price} 金币    ·    余额 {ViewState.gold} → {ViewState.gold-price}",40,585,1500,50,31,UiKit.Gold);
            if(error!=null)UiKit.Text(p,"PurchaseUnavailable",error,40,635,1500,32,21,UiKit.Gold);
            UiKit.Button(p,"ConfirmPurchase",error==null?"确认购买":"暂不能购买",40,680,1500,64,()=>{CloseModal();Send(new SessionCommand{kind=CommandKind.Buy,offerId=offer.offerId,targetDieId=die,targetFaceId=face});},error==null,true);
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
        private void TargetDie(ShopOffer offer,string selectedDie)
        {
            var p=OpenModal(NameOf(offer.contentId)+" · 选择永久骰");
            var candidates=ViewState.dice.Where(d=>d.lifetime=="permanent").ToArray();
            for(int i=0;i<candidates.Length;i++){string id=candidates[i].dieId;UiKit.Button(p,"Target_"+id,id,40+i*187,103,177,52,()=>TargetDie(offer,id),true,id==selectedDie);}
            if(selectedDie==null){UiKit.Text(p,"TargetHelp","先选择骰子；选目标和取消都不会扣款。",40,220,1500,90,31,UiKit.Muted);return;}
            var die=candidates.First(d=>d.dieId==selectedDie);
            if(offer.contentId=="O04"||offer.contentId=="U05")
            {
                FaceCards(p,die,190,null,null);
                UiKit.Text(p,"Risk",offer.contentId=="O04"?"这颗骰子及全部强化将移除；基础提交容量保留。至少保留1颗永久骰。":"玻璃：本骰贡献 ×2；每次实际参与投掷有15%概率永久损坏。已锁定时不检查损坏。",40,470,1500,95,27,UiKit.Gold);
                BuyFooter(p,offer,selectedDie,null);return;
            }
            FaceCards(p,die,190,face=>PreviewFacePurchase(offer,die,face),null);
            UiKit.Text(p,"FaceHelp","选择一个物理面查看改造前后。faceId 不随点数变化。",40,520,1500,65,27,UiKit.Muted);
        }
        private void PreviewFacePurchase(ShopOffer offer,DieInstance die,string faceId)
        {
            var p=OpenModal(NameOf(offer.contentId)+" · "+die.dieId+" / "+faceId);
            var before=FaceProbability(die);var copy=die.Clone();var target=copy.faces.First(f=>f.faceId==faceId);
            if(offer.contentId=="U01")target.texture="smooth";if(offer.contentId=="U02")target.texture="rough";if(offer.contentId=="U03")target.baseColor="red";if(offer.contentId=="U04")target.baseColor="blue";if(offer.contentId=="U06")target.isGold=true;
            var after=FaceProbability(copy);
            FaceCards(p,copy,125,null,after);
            string change=string.Join("   ",die.faces.Select((f,i)=>f.faceId+" "+(before[i]/10000f).ToString("0.00")+"→"+(after[i]/10000f).ToString("0.00")+"%"));
            UiKit.Text(p,"ProbChange",change,40,460,1500,65,24,UiKit.Teal);
            UiKit.Text(p,"Details",offer.contentId=="U07"?"确认后在其他5个点数中等概率选择一个；不会重新抽取颜色、纹理或镀层。":Description(offer.contentId),40,523,1500,55,23,UiKit.Muted);
            BuyFooter(p,offer,die.dieId,faceId);
        }
        private void FaceCards(Transform p,DieInstance die,float y,Action<string> choose,int[] probabilities)
        {
            var units=probabilities??FaceProbability(die);
            for(int i=0;i<6;i++)
            {
                var f=die.faces[i];float x=40+i*250;var card=UiKit.Box(p,"Face_"+f.faceId,x,y,235,235,UiKit.PanelLight);
                UiKit.Text(card,"ID",f.faceId,14,12,205,35,23,UiKit.Muted);
                UiKit.Text(card,"Pip",f.permanentPip.ToString(),14,53,205,85,60,f.baseColor=="red"?UiKit.Red:UiKit.Blue,TextAlignmentOptions.Center);
                UiKit.Text(card,"Color",(f.baseColor=="red"?"红 R":"蓝 B")+(f.isGold?" · 镀金":""),10,140,215,35,22,UiKit.Paper,TextAlignmentOptions.Center);
                UiKit.Text(card,"Texture",(f.texture=="smooth"?"光滑":f.texture=="rough"?"粗糙":"无纹理")+"   "+(units[i]/10000f).ToString("0.00")+"%",10,181,215,34,20,UiKit.Muted,TextAlignmentOptions.Center);
                var faceButton=card.GetComponent<UnityEngine.UI.Button>();
                if(faceButton!=null){faceButton.onClick.RemoveAllListeners();faceButton.enabled=false;faceButton.interactable=false;}
                if(choose!=null){string id=f.faceId;faceButton=UiKit.Component<UnityEngine.UI.Button>(card.gameObject);faceButton.enabled=true;faceButton.interactable=true;faceButton.targetGraphic=card.GetComponent<UnityEngine.UI.Image>();faceButton.targetGraphic.raycastTarget=true;faceButton.onClick.AddListener(()=>choose(id));}
            }
        }
    }
}
