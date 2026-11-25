using System;
using System.Linq;
using DiceCraft.Core;
using TMPro;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        // V1.1 W14: the A3 shop page (SHOP_LAYOUT.md); v0.73's single page and v0.7's tabs are gone.
        private void Shop()=>Shop14();
        private Sprite[] contentIndexSource073;
        private System.Collections.Generic.Dictionary<string,Sprite> contentIndex073;
        // v0.73: indexed once per icon set. The former scan read every sprite name (a native call and a new
        // string each) for every card on every redraw.
        private Sprite ContentSprite071(string id)
        {
            if(string.IsNullOrEmpty(id)||contentIcons==null)return null;
            // V1.1: the active theme's own illustration wins; a theme without one keeps the default art.
            var themed=ThemeRegistry.Current?.ContentArt("illustration_"+id);
            if(themed!=null)return themed;
            if(contentIndex073==null||!ReferenceEquals(contentIndexSource073,contentIcons))
            {
                contentIndexSource073=contentIcons;contentIndex073=new System.Collections.Generic.Dictionary<string,Sprite>(contentIcons.Length,StringComparer.OrdinalIgnoreCase);
                foreach(var sprite in contentIcons)if(sprite!=null&&!contentIndex073.ContainsKey(sprite.name))contentIndex073.Add(sprite.name,sprite);
            }
            if(contentIndex073.TryGetValue("illustration_"+id,out var art)&&art!=null)return art;
            if(contentIndex073.TryGetValue(id,out art)&&art!=null)return art;
            return contentIndex073.TryGetValue("icon_"+id,out art)&&art!=null?art:null;
        }

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
                // v1.1 (W09): it shows the first character of the name, never the content id.
                var old=r.GetComponent<UnityEngine.UI.Image>();if(old!=null){old.sprite=null;old.color=Color.clear;old.raycastTarget=false;}
                UiKit.Outline(r,"Missing",2,2,size-4,size-4,UiKit.Muted);
                UiKit.Text(r,"Id",MissingIconLabel11(id),2,2,size-4,size-4,Math.Min(22,size/4),UiKit.Muted,TextAlignmentOptions.Center);
            }
        }
        private void ShowOwned(OwnedItem item,bool selling)
        {
            var p=Art07Modal(NameOf(item.contentId),480,180,960,720);Icon(p,item.contentId,24,112,176);
            var list=PaperDetailScroll071(p,"ItemDetail",232,104,704,492);float y=0;
            Art07DetailSection(list,"Skill","道具效果",PlayerRules11(item.contentId),ref y,672,true);
            string window=Config.Consumable(item.contentId).window;
            Art07DetailSection(list,"Window","何时能用",window=="Selecting"?"掷骰后且锁定了至少 1 颗骰子":window=="HandReady"?"本手第一次掷骰前":"本手任何时候",ref y,672,true);
            string reason=selling?null:Art07ItemUnavailable(item);
            Art07DetailSection(list,"UseState",selling?"出售结果":"当前状态",selling?"卖出得 1 金币。":reason??"现在可以使用（用后消失）。",ref y,672,true);
            list.sizeDelta=new Vector2(list.sizeDelta.x,y+16);
            if(selling)
            {
                UiKit.Button(p,"Sell","卖出 +1 金币",24,628,912,64,()=>{CloseModal();Send(new SessionCommand{kind=CommandKind.Sell,itemInstanceId=item.instanceId});},!IsBusy);
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
            if(Session.PendingIds.Length>0)return "先锁定或取消选中的骰子。";
            var s=ViewState;var h=s.hand;
            if(s.phase!="HandReady"&&s.phase!="Selecting")return "此道具只能在关内使用。";
            if(item.contentId!="I08"&&ControllerRules.ModifierHas(Config,Session.CurrentModifierId,"NoItems"))return "首领减益“徒手”：本关不能使用道具。";
            switch(item.contentId)
            {
                case "I01":return !h.hasRolled||h.orderedLockedIds.Count==0?"需要先掷骰并锁定至少 1 颗骰子。":h.redWave?"本手已经生效。":h.blueWave?"本手已经用了蓝色浪潮。":null;
                case "I02":return s.phase!="HandReady"?"本手第一次掷骰前才能用。":s.dice.Count>=(s.isDebug?Config.debug.maxDice:Config.maxActiveDice)||(!s.isDebug&&s.dice.Count(d=>d.lifetime=="stage_temporary")>=Config.maxTemporaryDice)?s.isDebug?$"骰子已到上限（总数最多 {Config.debug.maxDice} 颗）。":$"骰子已到上限（临时骰最多 {Config.maxTemporaryDice} 颗，总数最多 {Config.maxActiveDice} 颗）。":null;
                case "I03":return h.rollsRemaining<=0?"需要至少一次剩余投掷。":s.dice.Count(d=>!h.orderedLockedIds.Contains(d.dieId))<2?"需要骰池里有 2 颗骰子。":null;
                case "I04":return h.largeBuff?"本手已经生效。":h.rollsRemaining<=0||!s.dice.Any(d=>!h.orderedLockedIds.Contains(d.dieId)&&d.faces.Any(f=>f.permanentPip==6))?"需要骰池里有带 6 点面的骰子，且本手还能掷。":null;
                case "I05":return h.smallBuff?"本手已经生效。":h.rollsRemaining<=0||!s.dice.Any(d=>!h.orderedLockedIds.Contains(d.dieId)&&d.faces.Any(f=>f.permanentPip==1))?"需要骰池里有带 1 点面的骰子，且本手还能掷。":null;
                default:return ItemUnavailable10(item);
            }
        }
        private const string UnavailableShopReason16="这件商品暂时不能购买。";

        private static bool LastDieRemovalBlocked16(ShopOffer offer,string reason)=>
            reason==GameSession.ServiceUnavailable&&offer?.category=="service"&&offer.contentId=="O04";

        private string NewDieLimit16(ShopOffer offer,string reason)
        {
            if(reason!=GameSession.NewDieUnavailable||offer?.category!="new_die"||offer.die==null||ViewState==null)return null;
            if(ViewState.dice.Count(d=>d.lifetime=="permanent")>=Config.maxPermanentDice)return "骰子已满 "+Config.maxPermanentDice+" 颗";
            if(ViewState.dice.Count>=Config.maxActiveDice)return "骰子已满 "+Config.maxActiveDice+" 颗";
            return null;
        }

        private string PlayerShopReason16(string reason,ShopOffer offer,string dieId=null,string faceId=null)
        {
            switch(reason)
            {
                case "商品不存在。":return "商品不存在或已售罄。";
                case "请选择永久骰子。":return "请选择骰子。";
                case "普通卡牌栏位或总张数已满，可先出售卡牌。":
                    return ViewState?.controllers.Count>=Config.maxControllerInstances
                        ?"卡牌已满 "+Config.maxControllerInstances+" 张，先卖掉一张。":"卡牌栏位已满，先卖掉一张。";
                case "道具栏位已满，可先出售道具。":return "道具栏已满，先卖掉一件。";
                case "牌型已满级，或当前容量/永久骰数量不足。":return "这个牌型已满级，或骰子/提交栏不够。";
                case GameSession.UpgradeTargetRejected:return SelectedUpgradeReason16(offer,dieId,faceId);
                case GameSession.SelfStudyLimitReached:return "本店指定升级已用完";
                case GameSession.NewDieUnavailable:
                    string limit=NewDieLimit16(offer,reason);
                    return limit==null?UnavailableShopReason16:limit+"。";
                case GameSession.ServiceUnavailable:
                    if(LastDieRemovalBlocked16(offer,reason))return "至少要留 1 颗骰子。";
                    if(offer?.category!="service")return UnavailableShopReason16;
                    switch(offer.contentId)
                    {
                        case "O11":return "没有可以附魔的卡牌。";
                        case "O07":return "已购买";
                        case "O01":case "O02":case "O05":case "O06":case "O08":case "O10":
                            return "这项服务已用到上限。";
                        default:return UnavailableShopReason16;
                    }
                case "商品类别无效。":case "没有可用附魔候选。":case "附魔抽样未覆盖票值。":
                case "本商店已经生成。":case "未知战利品种类。":case "当前没有商店。":case "没有活动挑战。":
                case "卡牌报价实例无效。":case "卡牌附魔不合法。":case "折现报价无效。":case "未知商品类型。":
                case "没有可以附魔的卡牌。":case "服务不能以此方式购买。":case GameSession.UnknownDieUpgrade:case "没有合法的卡牌候选。":
                    return UnavailableShopReason16;
                default:return reason;
            }
        }

        private void QueueCommandRejection16(SessionCommand cmd,string reason)
        {
            string shown=null;
            if(cmd.kind==CommandKind.Buy||cmd.kind==CommandKind.Sell||cmd.kind==CommandKind.Study||cmd.kind==CommandKind.RefreshControllers)
            {
                var offer=ViewState?.shop?.offers.FirstOrDefault(o=>o.offerId==cmd.offerId);
                string formatted=PlayerShopReason16(reason,offer,cmd.targetDieId,cmd.targetFaceId);
                if(formatted!=reason)shown=formatted;
                if(shown==UnavailableShopReason16)Debug.LogWarning("商店操作被拒："+reason);
            }
            QueueFormattedNotice16(reason,true,shown);
        }

        private void InspectOffer(ShopOffer offer)
        {
            // V1.1 W14 (SHOP_LAYOUT 7): the shelf's face upgrades and 删除骰子 are chosen on the table; a gift group's face
            // upgrade (铁匠铺, the loot's 骰面工坊) keeps the dice sheet until W15.
            if(string.IsNullOrEmpty(offer.groupId)&&(offer.category=="die_upgrade"||offer.contentId=="O04")&&ViewState.phase=="Shop"){BeginFacePick14(offer);return;}
            if(!string.IsNullOrEmpty(offer.groupId)&&offer.category=="die_upgrade"){InspectLootUpgrade15(offer);return;}
            // v1.1 (G3): a card you already own is bought like any other; the copy is added, nothing is replaced.
            if(offer.category=="new_die")
            {
                ShowNewDieOffer07(offer);return;
            }
            var p=Art07Modal(NameOf(offer.contentId),480,180,960,720);
            Icon(p,offer.contentId,24,112,176);
            if(offer.category=="controller")EnchantBadge(p,offer.controller.enchantmentId,160,102,40);
            var list=PaperDetailScroll071(p,"OfferDetail",232,104,704,492);float y=0;
            Art07DetailSection(list,"Skill","效果",PlayerRules11(offer.contentId),ref y,672,true);
            if(offer.category=="controller")
            {
                Art07DetailSection(list,"Enchant","附魔",EnchantLabel(offer.controller.enchantmentId),ref y,672,true);
                string offerState=Art07OfferState(offer.controller,forDetails:true);
                Art07DetailSection(list,"Capacity","买下后",ControllerPurchaseText(offer)+(offerState.Length==0?"":"\n"+offerState),ref y,672,true);
            }
            // V1.1 W14 (UI_REWRITES Shop.cs:134): what the purchase does, in the player's words.
            else Art07DetailSection(list,"Category","作用范围",offer.category=="consumable"?$"买下放进道具栏（{ViewState.inventory.Count}/{ViewState.consumableSlots}），用一次就消失。":"买下立即生效，持续到本局结束。",ref y,672,true);
            string error=PlayerShopReason16(Session.PurchaseError(offer.offerId),offer);
            // v1.1 (W09): gold with thousands separators, "金币" instead of 余额 (text proposal 2.2); W14: 价格：5 金币（你有 7）.
            Art07DetailSection(list,"Price","价格",(!string.IsNullOrEmpty(offer.groupId)?"免费领取":"价格："+Session.GetOfferPrice(offer)+" 金币（你有 "+PlayerNumberFormat.Gold(ViewState.gold)+"）")+(error==null?"":"\n"+error),ref y,672,true);
            list.sizeDelta=new Vector2(list.sizeDelta.x,y+16);
            UiKit.ActionButton(p,"ConfirmPurchase",offer.sold?(string.IsNullOrEmpty(offer.groupId)?"已购买":"已领取"):!string.IsNullOrEmpty(offer.groupId)?"免费领取":"支付 "+Session.GetOfferPrice(offer)+" 并购买",24,628,912,64,()=>{CloseModal();Send(new SessionCommand{kind=CommandKind.Buy,offerId=offer.offerId});},UiKit.Gold,error==null&&!IsBusy);
        }
        private void ShowStudy()
        {
            // v1.1 (text proposal 5 #3): the number of uses comes from the configuration (two per shop), not "一次".
            var p=OpenModal("指定升级 · 本店还剩 "+Math.Max(0,Config.shop.selfStudyLimit-ViewState.shop.selfStudyUsed)+" 次");var legal=Session.LegalStudyHands();
            var list=UiKit.Scroll(p,"StudyList",40,100,1500,620,Math.Max(600,legal.Length*85));
            for(int i=0;i<legal.Length;i++)
            {
                string id=legal[i];bool color=id=="all_blue"||id=="all_red";int level=(color?ViewState.colorLevels:ViewState.handLevels).First(x=>x.handId==id).level,price=Session.GetStudyPrice(id);int baseMult=color?100+Config.Color(id).baseAdd100:Config.Hand(id).baseMult100,increment=color?Config.Color(id).addPerLevel100:Config.Hand(id).incrementPerLevel100; /* W3A-ADAPT: a colour is an additive bonus; shown as 1 + bonus until W3b/UI rewords it */
                UiKit.Text(list,"StudyName"+id,$"{NameOf(id)}   Lv.{level} → {level+1}    倍率 {Factor(baseMult+increment*level)} → {Factor(baseMult+increment*(level+1))}",12,i*85+15,1040,50,27);
                UiKit.Button(list,"StudyBuy"+id,price+" 金币",1110,i*85,340,65,()=>{CloseModal();Send(new SessionCommand{kind=CommandKind.Study,handId=id});},ViewState.gold>=price,true);
            }
            if(legal.Length==0)UiKit.Text(list,"NoLegal","现在没有能升级的牌型（都满级了，或骰子/提交栏不够）。",15,30,1400,90,28,UiKit.Muted);
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
