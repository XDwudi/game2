using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DiceCraft.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceCraft
{
    // V1.1 task W14 (docs/specs/v11/SHOP_LAYOUT.md, art proposal A3): the shop page. Presentation only: every purchase,
    // refresh, sale, reorder and claim goes through the same session commands as before; no rule or price is computed here.
    // Four sections and a top bar: 卡牌 (ControllerShelf), 升级与服务 (ShopProducts), 我的构筑 (Build14), 骰子与骰面
    // (DiceCounter) over 我的骰子 (OwnedStrip), and 下一关, the screen's only primary button. Section roots and buttons keep
    // the v0.73 names that tests, tours and the transaction visuals find (Offer_, Buy_, Product_ with Inspect, ShopItem_,
    // Controller_, OwnedDie_, LeaveShop, RefreshControllers, Study, ViewCycle, ViewGifts, Pause, Handbook).
    // Colours come from the active style's palette only (SHOP_LAYOUT 10; checked by the source scan of SH-09).
    public sealed partial class GameApp
    {
        // ---------------------------------------------------------------- layout (SHOP_LAYOUT 2)
        private const float ShopTop14=92,ShopBottom14=1072,ShopGap14=8,ShopHead14=68,ShopFoot14=7,ShopPad14=12,ShopRowGap14=6;
        private struct ShopLayout14 { public Rect top,cards,goods,build,dice,owned,leave; public float cardH,tileH,captionH,ownedHead; }
        /// <summary>The section rectangles of SHOP_LAYOUT 2 at the current text size (100 %, or 125/150 % as Large11).</summary>
        private ShopLayout14 S14
        {
            get
            {
                bool c=Large11;float cardH=ShopCardHeight14,tileH=c?116:104,caption=c?40:28,ownedHead=ShopHead14,leave=c?72:96;
                float avail=ShopBottom14-ShopTop14-2*ShopGap14;
                float cards=ShopHead14+cardH+ShopFoot14,build=4+caption+2+tileH+ShopFoot14,goods=avail-cards-build;
                float owned=ownedHead+2*104+6+ShopFoot14,dice=avail-owned-leave,y=ShopTop14;
                return new ShopLayout14
                {
                    top=new Rect(16,12,1888,72),
                    cards=new Rect(16,y,1404,cards),goods=new Rect(16,y+cards+ShopGap14,1404,goods),build=new Rect(16,y+cards+goods+2*ShopGap14,1404,build),
                    dice=new Rect(1432,y,472,dice),owned=new Rect(1432,y+dice+ShopGap14,472,owned),leave=new Rect(1432,y+dice+owned+2*ShopGap14,472,leave),
                    cardH=cardH,tileH=tileH,captionH=caption,ownedHead=ownedHead
                };
            }
        }
        /// <summary>A palette colour at a given opacity (the only colour constructor the shop files use, SH-09).</summary>
        private static Color Alpha14(Color c,float a)=>new Color(c.r,c.g,c.b,a);
        private int goodsPage14,ownedDicePage14,counterPage14;
        private string goodsPageShop14;
        public int GoodsPage14=>goodsPage14;
        // The 472-wide right column leaves its rows' names and "新骰子 · 点数和 N" room only with a narrower price button
        // (a long reason takes two rows there; SHOP_LAYOUT 2).
        private const float GoodsPriceWidth14=180,DicePriceWidth14=150;
        private float GoodsPriceW14=>Large11?184:GoodsPriceWidth14;

        private void Shop14()
        {
            var l=S14;
            // A new shop starts on the first page of its goods.
            if(goodsPageShop14!=ViewState.shop.generationId){goodsPageShop14=ViewState.shop.generationId;goodsPage14=0;ownedDicePage14=0;counterPage14=0;facePick14=null;}
            if(facePick14!=null&&!FacePickStillValid14())facePick14=null;
            ShopTopButtons14(l);
            if(LootPage15){DrawLootPage15(l);return;}
            ShopCards14(l);
            ShopGoods14(l);
            ShopBuild14(l);
            ShopLeave14(l);
            if(facePick14==null)
            {
                ShopDiceCounter14(l);
                ShopOwnedDice14(l,false);
            }
            else
            {
                // S1–S3 (SHOP_LAYOUT 7): the rest of the shop is dimmed and does not take clicks; the dice and the panel
                // are drawn above the veil.
                UiKit.Box(main,"FacePickVeil14",0,0,1920,1080,Alpha14(UiKit.Shadow,.6f),true);
                FacePickPanel14(l);
                ShopOwnedDice14(l,true);
            }
            BindShopEntrance14();
        }

        // ---------------------------------------------------------------- sections
        /// <summary>One section style (SHOP_LAYOUT 10): the section panel times the theme's section tint (Panel when it sets
        /// none), a header strip times its caption tint (PanelLight when it sets none; W20X, THEME_SPEC §2), the title in the
        /// readable text colour on that strip. Returns the panel; <paramref name="titleRight"/> is where the title ends
        /// (section units).</summary>
        private RectTransform Section14(Transform parent,string name,string titleName,string title,Rect r,out float titleRight,bool strip=true,float titleMax=320)
        {
            var panel=Surface11(parent,name,r.x,r.y,r.width,r.height,"ui_section_panel",UiKit.Section(UiKit.Panel));
            var f=FrameInsets11(panel.GetComponent<Image>());
            var on=panel;
            if(strip)
            {
                on=Surface11(panel,"HeaderStrip",f.x,f.w,r.width-f.x-f.z,ShopHead14-f.w-2,"ui_section_header",UiKit.Caption(UiKit.PanelLight));
            }
            float lineH=Line11(22),top=strip?f.w+(ShopHead14-f.w-2-lineH)*.5f:f.w+4;
            var label=FitLine13(panel,titleName,title,f.x+14,top,Mathf.Min(titleMax,r.width-f.x-f.z-28),22,LabelOn11(on,UiKit.TextColor),TextAlignmentOptions.MidlineLeft,true,role:TextRole.SectionTitle);
            label.ForceMeshUpdate();titleRight=f.x+14+label.textBounds.size.x+2;
            return panel;
        }
        /// <summary>A second-level button of the shop (W11's solid style) that lifts on hover like the top-bar buttons; its
        /// label moves right of an icon when there is one.</summary>
        private UnityEngine.UI.Button ShopButton14(Transform parent,string name,string label,float x,float y,float w,float h,Action click,bool enabled,string icon=null,float font=20)
        {
            var b=SolidButton11(parent,name,label,x,y,w,h,click,enabled,font);
            UiKit.Component<Art07ButtonMotion>(b.gameObject).SetSurface(0);
            var face=(Image)b.targetGraphic;var text=b.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            NoWrap11(text);text.fontSize=Mathf.Max(19.5f,font*UiKit.FontScale);
            text.color=enabled?LabelOn11(face,UiKit.TextColor):LabelOn11(face,UiKit.Muted);
            if(icon!=null&&UiKit.Template(icon)!=null)
            {
                float size=Mathf.Min(34,h-24),textW=text.GetPreferredValues(label).x,start=(w-(size+8+textW))*.5f;
                var image=UiKit.Box(b.transform,"NavIcon",start,(h-size)*.5f,size,size,text.color).GetComponent<Image>();
                image.sprite=UiKit.Template(icon);image.preserveAspect=true;image.raycastTarget=false;
                text.alignment=TextAlignmentOptions.MidlineLeft;
                text.rectTransform.anchoredPosition=new Vector2(start+size+8,0);text.rectTransform.sizeDelta=new Vector2(Mathf.Max(1,w-(start+size+8)-4),h);
            }
            else text.rectTransform.sizeDelta=new Vector2(w-16,h);
            return b;
        }
        private static float ShopLabelWidth14(string text,float font,TMP_FontAsset face=null)=>Mathf.Ceil(PreferredWidth11(text,Mathf.Max(font,19.5f/Mathf.Max(.01f,UiKit.FontScale)),face))+4;

        // ---------------------------------------------------------------- top bar (SHOP_LAYOUT 3)
        private struct ShopTopSlots14 { public Rect gold,cash,cycle,gifts,hand,pause; public bool showGifts; public string giftsLabel; }
        /// <summary>The right end of the top bar, right to left: settings, handbook, the loot / gift entrance, 查看本章, the
        /// gold readout and the slot where a gold change pops; the next-stage line takes what is left.</summary>
        private ShopTopSlots14 ShopTopRight14(ShopLayout14 l)
        {
            var slots=new ShopTopSlots14();bool c=Large11;float y=l.top.y+6,h=60,right=l.top.xMax-8;
            float IconW(string label)=>34+8+ShopLabelWidth14(label,20)+28;
            slots.pause=new Rect(right-IconW("设置"),y,IconW("设置"),h);right=slots.pause.x-10;
            slots.hand=new Rect(right-IconW("手册"),y,IconW("手册"),h);right=slots.hand.x-10;
            var groups=ViewState.shop?.groups;var loot=groups?.FirstOrDefault(g=>g.groupId=="LOOT"&&!g.closed);
            slots.showGifts=!LootPage15&&(loot!=null||groups!=null&&groups.Any(g=>g.groupId!="LOOT"));
            if(slots.showGifts)
            {
                slots.giftsLabel=loot!=null?"首领战利品（还可领 "+Math.Max(0,loot.picks-loot.picked)+" 件）":"商店赠礼";
                float w=ShopLabelWidth14(slots.giftsLabel,20)+32;slots.gifts=new Rect(right-w,y,w,h);right=slots.gifts.x-10;
            }
            float cycle=44+8+ShopLabelWidth14("查看本章",20)+28;slots.cycle=new Rect(right-cycle,y,cycle,h);right=slots.cycle.x-16;
            // The amount is bold at 34 units (31.5 with enlarged text), a key number of the screen (SHOP_LAYOUT 3).
            float money=ShopLabelWidth14(PlayerNumberFormat.Gold(ViewState.gold),ShopMoneySize14)*1.12f+6;
            float gold=40+money+ShopLabelWidth14("金币",c?17:20)+6;slots.gold=new Rect(right-gold,l.top.y+6,gold,60);right=slots.gold.x-12;
            slots.cash=new Rect(right-224,l.top.y+7,224,58);
            return slots;
        }
        /// <summary>The top bar's surface and texts ("Stats07", the play screen's name for it): the shop and the stage it is
        /// before, the next stage with its kind and target (the boss modifier included, R5), and the gold.</summary>
        private void ShopTopBar14()
        {
            var l=S14;bool c=Large11;var s=ViewState;
            var bar=Surface11(main,"Stats07",l.top.x,l.top.y,l.top.width,l.top.height,"ui_topbar",UiKit.Panel);
            var frame=FrameInsets11(bar.GetComponent<Image>());var slots=ShopTopRight14(l);
            int next=s.stage.stageIndex+1;bool last=next>=Config.stages.Length;
            float lineH=Line11(c?20:26),titleY=frame.w+Mathf.Max(0,(l.top.height-frame.w-frame.y-lineH)*.5f);
            var title=FitLine13(bar,"ShopTitle",LootPage15&&!lootFromShop15?"首领战利品":last?"商店":"商店 · 第 "+(next+1)+" 关前",frame.x+16,titleY,360,c?20:26,LabelOn11(bar,UiKit.TextColor),TextAlignmentOptions.MidlineLeft,true,role:TextRole.ScreenTitle);
            title.ForceMeshUpdate();float centreX=frame.x+16+title.textBounds.size.x+40,centreRight=slots.cash.x-l.top.x-8;
            // The next stage: kind and target; a boss stage leads its kind with the boss mark.
            string kind=last?"":Config.stages[next].stageKind;string text;
            if(last)text="最后一关已完成";
            else
            {
                string word=kind=="boss"?"首领":kind=="pressure"?"高压（可在选关页选择）":"普通";
                text="下一关：第 "+(next+1)+" 关 · "+word+" · 目标 "+CompactScore(ShopNextTarget11(next));
            }
            float markW=kind=="boss"?30:0,width=Mathf.Max(60,centreRight-centreX-markW);
            var centre=FitLine13(bar,"NextStage14",text,centreX+markW,0,width,20,LabelOn11(bar,UiKit.TextColor),TextAlignmentOptions.MidlineLeft,false);
            if(!FitsLine13(centre))
            {
                // Still too long at the body size: kind and target break onto a second line (SHOP_LAYOUT 2.2).
                centre.textWrappingMode=TextWrappingModes.Normal;centre.text=BalancedLines11(centre,text,width);centre.ForceMeshUpdate();
                centre.rectTransform.sizeDelta=new Vector2(width,Mathf.Ceil(centre.GetPreferredValues(centre.text,width,0).y)+2);
            }
            float centreH=centre.rectTransform.rect.height;
            centre.rectTransform.anchoredPosition=new Vector2(centreX+markW,-(frame.w+Mathf.Max(0,(l.top.height-frame.w-frame.y-centreH)*.5f)));
            if(kind=="boss")
            {
                BossEmblem15(bar,"BossMark14",s.cycles[next/3].modifierId,centreX,(l.top.height-24)*.5f,22,false);
            }
            // Gold: the coin, the amount and 金币, as on the play screen's top bar.
            var g=slots.gold;float gx=g.x-l.top.x;
            var coin=UiKit.Box(bar,"MoneyIcon11",gx,12,36,36,Color.white).GetComponent<Image>();coin.sprite=UiKit.Template("ui_icon_coin");coin.preserveAspect=true;coin.raycastTarget=false;coin.gameObject.SetActive(coin.sprite!=null);
            float unitW=ShopLabelWidth14("金币",c?17:20);
            var money=NoWrap11(UiKit.Text(bar,"Money",PlayerNumberFormat.Gold(s.gold),gx+40,0,g.width-40-unitW-6,l.top.height,ShopMoneySize14,LabelOn11(bar,UiKit.Gold),TextAlignmentOptions.MidlineRight));
            money.fontStyle=FontStyles.Bold;
            UiKit.Component<Art072ValuePop>(money.gameObject).Configure(s.runId+"|wallet",money.text);
            NoWrap11(UiKit.Text(bar,"MoneyLabel11","金币",gx+g.width-unitW,0,unitW,l.top.height,Body11(c?17:20),LabelOn11(bar,UiKit.Muted),TextAlignmentOptions.MidlineLeft));
            if(IsDebugWorkspace&&!CleanCapture11)UiKit.Box(main,"DebugMark",0,0,1920,6,UiKit.Violet);
        }
        /// <summary>The gold amount's size (before the text scale): 34 units at 100 %, 31.5 units of drawn type with enlarged
        /// text, never smaller (a key number, PLAY_LAYOUT's 31.5 floor).</summary>
        public float ShopMoneySize14=>Large11?31.5f/UiKit.FontScale:34;
        /// <summary>Where a gold change pops in the shop: the free slot of the top bar left of the gold readout.</summary>
        private Vector2 ShopCashPoint14()=>ShopTopRight14(S14).cash.position;
        /// <summary>利息：每 5 金币 +1，每关最多 +5 (UI_REWRITES Art07Shop.cs:34): the rate with C01 copies, the cap from the
        /// session's principal cap (C96, O10), as the settlement will use them.</summary>
        private string ShopInterestText07()
        {
            long rate=1L+ViewSources.Count(s=>s.abilityId=="C01")*Config.Controller("C01").parameters.extraInterestPerTen;
            long cap=Session.InterestPrincipalCapIn(ViewState);
            long most=cap/Math.Max(1,Config.interestDivisor)*rate;
            return "利息：每 "+Config.interestDivisor+" 金币 +"+rate+"，每关最多 +"+most;
        }
        /// <summary>The top bar's buttons and the gold hover, drawn before any face-pick veil so the veil covers them.</summary>
        private void ShopTopButtons14(ShopLayout14 l)
        {
            var slots=ShopTopRight14(l);bool free=!IsBusy;
            var hover=HitArea073(main,"MoneyHover14",slots.gold.x,slots.gold.y,slots.gold.width,slots.gold.height,null);
            Art07AttachTooltip(hover,"shop-gold","金币",ShopInterestText07(),"",null,"");
            // 查看本章: three dots for the chapter's stages (filled: cleared) lead its label.
            var cycle=ShopButton14(main,"ViewCycle","查看本章",slots.cycle.x,slots.cycle.y,slots.cycle.width,slots.cycle.height,ShowCycle10,free);
            var cycleLabel=cycle.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            int stage=ViewState.stage.stageIndex+1,first=stage/3*3;
            for(int i=0;i<3;i++)
            {
                bool done=first+i<stage;float size=done?12:10;
                var dot=UiKit.Box(cycle.transform,"ChapterDot14_"+i,16+i*16+(12-size)*.5f,(slots.cycle.height-size)*.5f,size,size,done?cycleLabel.color:Alpha14(cycleLabel.color,.45f)).GetComponent<Image>();
                dot.sprite=UiKit.Template("ui_pip_dot");dot.preserveAspect=true;dot.raycastTarget=false;
            }
            cycleLabel.alignment=TextAlignmentOptions.MidlineLeft;cycleLabel.rectTransform.anchoredPosition=new Vector2(16+44+8,0);
            cycleLabel.rectTransform.sizeDelta=new Vector2(slots.cycle.width-(16+44+8)-8,slots.cycle.height);
            if(slots.showGifts)
            {
                var groups=ViewState.shop.groups;bool open=groups.Any(g=>!g.closed);
                ShopButton14(main,"ViewGifts",slots.giftsLabel,slots.gifts.x,slots.gifts.y,slots.gifts.width,slots.gifts.height,OpenClaims15,free);
                // Q4 A: no gold; a small Danger dot says something is still to be taken.
                if(open){var dot=UiKit.Box(main,"GiftsPending10",slots.gifts.xMax-12,slots.gifts.y-2,14,14,UiKit.Danger).GetComponent<Image>();dot.sprite=UiKit.Template("ui_pip_dot");dot.raycastTarget=false;}
            }
            ShopButton14(main,"Handbook","手册",slots.hand.x,slots.hand.y,slots.hand.width,slots.hand.height,ShowHandbook,free&&!Session.HasPendingSave,"ui_icon_book");
            ShopButton14(main,"Pause","设置",slots.pause.x,slots.pause.y,slots.pause.width,slots.pause.height,Pause,true,"ui_icon_gear");
        }

        // ---------------------------------------------------------------- 卡牌 (SHOP_LAYOUT 2, 6)
        private void ShopCards14(ShopLayout14 l)
        {
            var offers=ViewState.shop.offers.Where(o=>o.category=="controller"&&string.IsNullOrEmpty(o.groupId)).ToArray();
            int capacity=Config.shop.initialControllerOffers+ViewState.controllerOfferPurchases;
            var shelf=Section14(main,"ControllerShelf","ControllerShelfTitle","卡牌",l.cards,out float titleRight);
            var strip=shelf.Find("HeaderStrip") as RectTransform;
            // Refresh: 刷新 ⦿3, 免费刷新, or (disabled) 还差 N ⦿5 / 先领战利品.
            int price=Session.GetRefreshPrice();bool loot=Session.IsLootPending;
            string word=loot?"先领战利品":ViewState.gold<price?"还差 "+(price-ViewState.gold):price==0?"免费刷新":"刷新";
            bool can=!loot&&ViewState.gold>=price&&!IsBusy&&!Session.HasPendingSave;
            float wordW=ShopLabelWidth14(word,20),priceW=price==0||loot?0:26+4+ShopLabelWidth14(price.ToString(CultureInfo.InvariantCulture),22)+4;
            float refreshW=16+34+8+wordW+(priceW>0?10+priceW:0)+16,rx=l.cards.width-ShopPad14-refreshW;
            var refresh=ShopButton14(shelf,"RefreshControllers",word,rx,4,refreshW,60,()=>Send(new SessionCommand{kind=CommandKind.RefreshControllers}),can,"ui_icon_refresh");
            var label=refresh.transform.Find("Label").GetComponent<TextMeshProUGUI>();var icon=refresh.transform.Find("NavIcon") as RectTransform;
            // Icon, word, then the coin and the price (Gold where it reads at 4.5:1, else the readable pick).
            if(icon!=null)icon.anchoredPosition=new Vector2(16,-13);
            label.rectTransform.anchoredPosition=new Vector2(16+34+8,0);label.rectTransform.sizeDelta=new Vector2(wordW,60);
            if(priceW>0)
            {
                var face=(Image)refresh.targetGraphic;float px=16+34+8+wordW+10;
                var coin=UiKit.Box(refresh.transform,"Coin",px,17,26,26,can?Color.white:Alpha14(Color.white,.6f)).GetComponent<Image>();coin.sprite=UiKit.Template("ui_icon_coin");coin.preserveAspect=true;coin.raycastTarget=false;
                var p=NoWrap11(UiKit.Text(refresh.transform,"RefreshPrice",price.ToString(CultureInfo.InvariantCulture),px+30,0,priceW-30,60,22,LabelOn11(face,can?UiKit.Gold:UiKit.Muted),TextAlignmentOptions.MidlineLeft));
                p.fontStyle=FontStyles.Bold;
            }
            // 在售 4/6 between the title and the refresh button.
            float capX=titleRight+16;
            FitLine13(shelf,"ShelfCapacity071","在售 "+offers.Count(o=>!o.sold)+"/"+capacity,capX,(ShopHead14-Line11(18))*.5f,Mathf.Max(40,rx-capX-12),18,LabelOn11(strip,UiKit.Muted),TextAlignmentOptions.MidlineLeft,false);
            int slots=Math.Max(offers.Length,capacity);
            float w=ShopCardWidth14,gap=(l.cards.width-2*ShopPad14-6*w)/5f,y=l.cards.y+ShopHead14;
            for(int i=0;i<slots;i++)
            {
                float x=l.cards.x+ShopPad14+i*(w+gap);
                if(i<offers.Length){ShopCard14(offers[i],x,y,w,l.cardH);continue;}
                // A new slot without a candidate (after 增加控制): a slot surface the card's size with one line.
                var empty=Surface11(main,"EmptyControllerOffer"+i,x,y,w,l.cardH,"ui_slot_inventory",UiKit.PanelLight);
                var e=UiKit.Text(empty,"Empty","暂时没有可上架的卡牌",24,l.cardH*.5f-40,w-48,80,Body11(18),LabelOn11(empty,UiKit.Muted),TextAlignmentOptions.Center);
                e.textWrappingMode=TextWrappingModes.Normal;
            }
        }
        /// <summary>The value badge of a card on the shelf: the number of its offer state (CARD_TEMPLATE §4); traits and
        /// placeholders ("技能详情", "随机倍率", "模拟右邻", "购入定色") have none.</summary>
        private string ShopCardValue14(ControllerInstance c,out bool warning)
        {
            warning=false;string state=Art07OfferState(c);
            if(state=="技能详情"||state=="随机倍率"||state=="模拟右邻"||state=="购入定色"||state=="持有即装备")return null;
            CardValue13(state.Replace(" ●","").Replace(" ◆",""),out string value,out warning);return value;
        }
        private void ShopCard14(ShopOffer offer,float x,float y,float w,float h)
        {
            var c=offer.controller;string value=ShopCardValue14(c,out bool warning);
            var face=new CardFace13{contentId=offer.contentId,enchantment=c?.enchantmentId??"none",value=value,valueWarning=warning,disabled=offer.sold,sold=offer.sold,
                price=Session.GetOfferNetPrice(offer),availability=offer.sold?null:ShopAvailability14(offer),buy=()=>Art07BuyOffer(offer),buttonName="Buy_"+offer.offerId};
            var card=DrawCard13(main,"Offer_"+offer.offerId,x,y,w,h,face,CardSize13.Shop,()=>{if(!IsBusy)InspectOffer(offer);},!IsBusy);
            Art07AttachTooltip(card,offer.offerId,NameOf(offer.contentId),PlayerRules11(offer.contentId),(EnchantLabel(c?.enchantmentId??"none")+"\n"+Art07OfferState(c,forDetails:true)).TrimEnd(),()=>InspectOffer(offer));
        }

        // ---------------------------------------------------------------- availability (SHOP_LAYOUT 6)
        /// <summary>Why a good cannot be bought now, as its price button says it; empty when it can (or, for a face upgrade
        /// or 删除骰子, when the face-pick flow can start: the target is chosen there).</summary>
        private string ShopAvailability14(ShopOffer offer)
        {
            if(offer.sold)return "已购买";
            bool gift=!string.IsNullOrEmpty(offer.groupId);
            if(gift&&ViewState.shop.groups.Any(g=>g.groupId==offer.groupId&&g.closed))return "未选";
            if(offer.groupId!="LOOT"&&Session.IsLootPending)return "先领战利品";
            long cost=Session.GetOfferNetPrice(offer);
            if(cost>ViewState.gold)return "还差 "+(cost-ViewState.gold);
            if(offer.category=="controller")
            {
                // v1.1 (G3): the copy is added to what you hold; nothing is replaced (RULES_DELTA 1.1 item 5).
                var final=ViewState.controllers.Concat(new[]{offer.controller}).ToArray();
                if(final.Length>Config.maxControllerInstances)return "卡牌已满 "+Config.maxControllerInstances+" 张";
                if(ControllerRules.OrdinaryUsed(final,Config)>ViewState.controllerSlots)return "卡牌栏位已满";
            }
            if(offer.category=="consumable"&&ViewState.inventory.Count>=ViewState.consumableSlots)return "道具栏已满";
            if(offer.category=="die_upgrade")
            {
                bool whole=offer.contentId=="U05";
                foreach(var die in ViewState.dice.Where(d=>d.lifetime=="permanent"))
                    foreach(var face in whole?new FaceState[]{null}:die.faces)
                        if(Session.PurchaseError(offer.offerId,die.dieId,face?.faceId)==null)return "";
                return whole?"没有可改的骰子":"没有可改的面";
            }
            if(offer.contentId=="O04")
            {
                var any=ViewState.dice.FirstOrDefault(d=>d.lifetime=="permanent");
                string removalError=Session.PurchaseError(offer.offerId,any?.dieId);
                if(removalError==null)return "";
                return LastDieRemovalBlocked16(offer,removalError)?"至少留 1 颗骰子":"暂不能买";
            }
            string error=Session.PurchaseError(offer.offerId);
            if(error==null)return "";
            return NewDieLimit16(offer,error)??(gift?"暂不能领":"暂不能买");
        }

        // ---------------------------------------------------------------- 升级与服务 (SHOP_LAYOUT 2, Q3)
        private ShopOffer[] ShopGoodsOffers14()=>ViewState.shop.offers.Where(o=>string.IsNullOrEmpty(o.groupId)&&(o.category=="hand_upgrade"||o.category=="color_upgrade")).
            Concat(ViewState.shop.offers.Where(o=>string.IsNullOrEmpty(o.groupId)&&o.category=="consumable")).
            Concat(ViewState.shop.offers.Where(o=>string.IsNullOrEmpty(o.groupId)&&o.category=="service")).ToArray();
        /// <summary>Rows per page and row height of 升级与服务: three columns; at 100 % up to three rows (nine goods, no
        /// paging), with enlarged text two rows and pages of six.</summary>
        private void GoodsGrid14(ShopLayout14 l,int count,out int rows,out int perPage,out float rowH,out float colW)
        {
            bool paged=Large11;perPage=paged?6:9;
            rows=paged?2:Mathf.Clamp(Mathf.CeilToInt(count/3f),1,3);
            float area=l.goods.height-ShopHead14-ShopFoot14;
            rowH=Mathf.Floor(Mathf.Min(150,(area-(rows-1)*ShopRowGap14)/rows));
            colW=(l.goods.width-2*ShopPad14-2*12)/3f;
        }
        public int GoodsPageCount14=>Mathf.Max(1,Mathf.CeilToInt(ShopGoodsOffers14().Length/(Large11?6f:9f)));
        private void ShopGoods14(ShopLayout14 l)
        {
            var offers=ShopGoodsOffers14();
            var panel=Section14(main,"ShopProducts","ProductsTitle14","升级与服务",l.goods,out float titleRight);
            var strip=panel.Find("HeaderStrip") as RectTransform;
            GoodsGrid14(l,offers.Length,out int rows,out int perPage,out float rowH,out float colW);
            int pages=Mathf.Max(1,Mathf.CeilToInt(offers.Length/(float)perPage));goodsPage14=Mathf.Clamp(goodsPage14,0,pages-1);
            // Header, right to left: the page buttons (enlarged text, more than six goods), then 指定升级.
            float right=l.goods.width-ShopPad14;
            if(pages>1)
            {
                ShopButton14(panel,"ProductsNext14","›",right-60,4,60,60,()=>{goodsPage14=Math.Min(pages-1,goodsPage14+1);Refresh();},goodsPage14<pages-1,null,26);right-=60+8;
                string pageText="第 "+(goodsPage14+1)+"/"+pages+" 页";float pw=ShopLabelWidth14(pageText,18);
                FitLine13(panel,"ProductsPage14",pageText,right-pw,(ShopHead14-Line11(18))*.5f,pw,18,LabelOn11(strip,UiKit.TextColor),TextAlignmentOptions.Center,false);right-=pw+8;
                ShopButton14(panel,"ProductsPrev14","‹",right-60,4,60,60,()=>{goodsPage14=Math.Max(0,goodsPage14-1);Refresh();},goodsPage14>0,null,26);right-=60+12;
            }
            int studyLeft=Config.shop.selfStudyLimit-ViewState.shop.selfStudyUsed;
            // v1.1 (W09): named like the dialog it opens ("指定升级"); the surcharge comes from the configuration.
            string study=studyLeft<=0?"本店指定升级已用完":ViewState.shop.selfStudyUsed==0?"指定升级 · 加价 "+Config.shop.selfStudyPremium:"指定升级 · 还剩 "+studyLeft+" 次";
            float sw=ShopLabelWidth14(study,20)+32;
            ShopButton14(panel,"Study",study,right-sw,4,sw,60,ShowStudy,studyLeft>0&&!IsBusy&&!Session.IsLootPending);right-=sw+12;
            // v1.1.1 (W7b, RULES_DELTA 9.2): the self-selected die upgrade stands beside 指定升级 in the same style; what is
            // left of the shop's allowance is the session's answer.
            int customLeft=Session.CustomDieUpgradesRemainingIn(ViewState);
            string custom=CustomUpgradeEntryText111(customLeft);float cw=ShopLabelWidth14(custom,20)+32;
            ShopButton14(panel,"CustomUpgrade111",custom,right-cw,4,cw,60,ShowCustomUpgrades111,customLeft>0&&!IsBusy&&!Session.IsLootPending);right-=cw+12;
            // The empty kinds are named in the header (SHOP_LAYOUT 9).
            var empties=new List<string>();
            if(!offers.Any(o=>o.category=="hand_upgrade"||o.category=="color_upgrade"))empties.Add("本店没有可升级的牌型");
            if(!offers.Any(o=>o.category=="consumable"))empties.Add("本店暂无道具");
            if(!offers.Any(o=>o.category=="service"))empties.Add("暂无服务");
            if(empties.Count>0)FitLine13(panel,"ProductsEmpty14",string.Join("，",empties),titleRight+16,(ShopHead14-Line11(18))*.5f,Mathf.Max(40,right-titleRight-28),18,LabelOn11(strip,UiKit.Muted),TextAlignmentOptions.MidlineLeft,false);
            var shown=offers.Skip(goodsPage14*perPage).Take(perPage).ToArray();
            for(int i=0;i<shown.Length;i++)GoodsRow14(panel,shown[i],ShopPad14+(i%3)*(colW+12),ShopHead14+(i/3)*(rowH+ShopRowGap14),colW,rowH,GoodsPriceW14);
        }
        /// <summary>One priced compact row (CARD_TEMPLATE E2) with an Inspect hit area over its texts (the v0.73 name).</summary>
        private UnityEngine.UI.Button GoodsRow14(Transform parent,ShopOffer offer,float x,float y,float w,float h,float priceW)
        {
            bool hand=offer.category=="hand_upgrade"||offer.category=="color_upgrade";
            var face=new CardFace13{contentId=offer.contentId,category=offer.category,price=Session.GetOfferNetPrice(offer),sold=offer.sold,disabled=offer.sold,
                availability=offer.sold?null:ShopAvailability14(offer),buy=()=>Art07BuyOffer(offer),buttonName="Buy_"+offer.offerId,priceWidth=priceW};
            string tip=PlayerRules11(offer.contentId);
            if(hand)
            {
                bool color=Config.colorDefinitions.Any(d=>d.id==offer.contentId);
                int level=(color?ViewState.colorLevels:ViewState.handLevels).First(v=>v.handId==offer.contentId).level;
                int basis=color?0:Config.Hand(offer.contentId).baseMult100,step=color?0:Config.Hand(offer.contentId).incrementPerLevel100;
                // UI_REWRITES Art073Shop.cs:277: "Lv.0 → Lv.1" and the multiplier change on one line (the short text).
                // v1.1.1 (W7a): a colour changes its bonus instead ("加成 +0.75 → +1.25").
                face.text="Lv."+level+" → Lv."+(level+1)+" · "+(color?ColorUpgrade111(offer.contentId,level):"×"+Factor(basis+step*level)+" → ×"+Factor(basis+step*(level+1)));
                face.picture=(t,r)=>HandPattern14(t,offer.contentId,color,r);tip=face.text+"\n"+tip;
            }
            if(offer.category=="die_upgrade"||offer.contentId=="O04")face.buy=()=>BeginFacePick14(offer);
            var row=DrawCard13(parent,"Product_"+offer.offerId,x,y,w,h,face,CardSize13.Compact,()=>{if(!IsBusy)InspectOffer(offer);},!IsBusy);
            var price=row.transform.Find("Buy_"+offer.offerId) as RectTransform;
            float left=hand?0:4,inspectW=(price!=null?price.anchoredPosition.x:w)-4-left;
            var inspect=HitArea073(row.transform,"Inspect",left,0,Mathf.Max(1,inspectW),h,()=>{if(!IsBusy)InspectOffer(offer);});
            Art07AttachTooltip(inspect,offer.offerId,NameOf(offer.contentId),tip,face.availability??"",()=>InspectOffer(offer));
            return row;
        }
        /// <summary>A hand upgrade's picture: its handbook example as pip tiles (one row, or two for long patterns).</summary>
        private void HandPattern14(Transform card,string id,bool color,Rect r)
        {
            var box=UiKit.Rect(card,"Icon_"+id,r.x,r.y,r.width,r.height);
            var row=HandbookRules.Build(Config,ViewState.handLevels,ViewState.colorLevels,ViewState.controllers,Session.EffectiveCapacity,
                ViewState.dice.Count(d=>d.lifetime=="permanent"),ViewState.dice.Count,ViewBindings,ViewState.isDebug).FirstOrDefault(x=>x.handId==id);
            int[] numbers=(row?.example??"").Where(char.IsDigit).Select(ch=>ch-'0').Where(n=>n>=1&&n<=6).ToArray();
            if(numbers.Length==0)return;
            int perRow=numbers.Length>4?(numbers.Length+1)/2:numbers.Length,lines=(numbers.Length+perRow-1)/perRow;
            float gap=3,tile=Mathf.Floor(Mathf.Min((r.width-(perRow-1)*gap)/perRow,(r.height-(lines-1)*gap)/lines));
            float x0=(r.width-(perRow*tile+(perRow-1)*gap))*.5f,y0=(r.height-(lines*tile+(lines-1)*gap))*.5f;
            Color pip=color?(id=="all_red"?UiKit.Red:UiKit.Blue):UiKit.Ink;
            for(int i=0;i<numbers.Length;i++)Art07PipTile(box,"HandTile14_"+i,numbers[i],pip,x0+(i%perRow)*(tile+gap),y0+(i/perRow)*(tile+gap),tile);
        }

        // ---------------------------------------------------------------- 骰子与骰面 (SHOP_LAYOUT 2, 5)
        private float DiceRowHeight14(ShopLayout14 l)=>Mathf.Floor((l.dice.height-ShopHead14-ShopFoot14-3*ShopRowGap14)/4);
        private void ShopDiceCounter14(ShopLayout14 l)
        {
            var panel=Section14(main,"DiceCounter","DiceCounterTitle","骰子与骰面",l.dice,out float titleRight);
            var strip=panel.Find("HeaderStrip") as RectTransform;
            var dice=ViewState.shop.offers.Where(o=>o.category=="new_die"&&string.IsNullOrEmpty(o.groupId)).ToArray();
            var faces=ViewState.shop.offers.Where(o=>o.category=="die_upgrade"&&string.IsNullOrEmpty(o.groupId)).ToArray();
            float rowH=DiceRowHeight14(l),w=l.dice.width-2*ShopPad14,y=ShopHead14;
            var titles=DieTitles14(dice.Select(o=>o.die).ToArray());
            if(dice.Length==0&&ViewState.dice.Count(d=>d.lifetime=="permanent")>=Config.maxPermanentDice)
            {
                // Dice full: one line in place of the dice rows (SHOP_LAYOUT 9).
                var none=FitLine13(panel,"NoDiceOffers","骰子已满 "+Config.maxPermanentDice+" 颗，本店不出售骰子",ShopPad14+8,y+8,w-16,18,LabelOn11(panel,UiKit.Muted),TextAlignmentOptions.MidlineLeft,false);
                y+=none.rectTransform.rect.height+16;
            }
            // Normal play has at most four rows (two new dice, two upgrades). A debug workspace with more pages by the rows
            // that fit (Q3 A: pages, never a scroll view), its page buttons in the header strip as in 升级与服务.
            var rows=dice.Select((o,i)=>new KeyValuePair<ShopOffer,int>(o,i)).Concat(faces.Select(o=>new KeyValuePair<ShopOffer,int>(o,-1))).ToArray();
            int perPage=Mathf.Max(1,Mathf.FloorToInt((l.dice.height-ShopFoot14-y+ShopRowGap14)/(rowH+ShopRowGap14)));
            int pages=Mathf.Max(1,Mathf.CeilToInt(rows.Length/(float)perPage));counterPage14=Mathf.Clamp(counterPage14,0,pages-1);
            if(pages>1)
            {
                float right=l.dice.width-ShopPad14;
                ShopButton14(panel,"CounterNext14","›",right-60,4,60,60,()=>{counterPage14=Math.Min(pages-1,counterPage14+1);Refresh();},counterPage14<pages-1,null,26);right-=60+8;
                string pageText="第 "+(counterPage14+1)+"/"+pages+" 页";float pw=ShopLabelWidth14(pageText,18);
                FitLine13(panel,"CounterPage14",pageText,right-pw,(ShopHead14-Line11(18))*.5f,pw,18,LabelOn11(strip,UiKit.TextColor),TextAlignmentOptions.Center,false);right-=pw+8;
                ShopButton14(panel,"CounterPrev14","‹",right-60,4,60,60,()=>{counterPage14=Math.Max(0,counterPage14-1);Refresh();},counterPage14>0,null,26);
            }
            foreach(var row in rows.Skip(counterPage14*perPage).Take(perPage))
            {
                if(row.Value>=0)NewDieRow14(panel,row.Key,titles[row.Value],ShopPad14,y,w,rowH);
                else GoodsRow14(panel,row.Key,ShopPad14,y,w,rowH,DicePriceWidth14);
                y+=rowH+ShopRowGap14;
            }
            if(faces.Length==0&&counterPage14==pages-1)FitLine13(panel,"NoUpgrades","本店暂无改造",ShopPad14+8,y+8,w-16,18,LabelOn11(panel,UiKit.Muted),TextAlignmentOptions.MidlineLeft,false);
        }
        // Real dice draw at their supplied scale and cell centre. The transparent cell also owns click and drag.
        private Art073DieView OfferDiePreview14(Transform parent,Rect art,ShopOffer offer)
        {
            float padX=Mathf.Max(8,(60-art.width)*.5f),padY=Mathf.Max(8,(60-art.height)*.5f);
            return DieView073(parent,"DiePreview073","shop-die-"+offer.offerId,offer.die,
                art.x-padX,art.y-padY,art.width+2*padX,art.height+2*padY,art.height/140f,false,true,
                ()=>{if(!IsBusy)InspectOffer(offer);});
        }

        private void NewDieRow14(Transform parent,ShopOffer offer,DieName14 title,float x,float y,float w,float h)
        {
            var face=new CardFace13{contentId="new_die",category="new_die",label=title.title,categoryText=title.subtitle,price=Session.GetOfferNetPrice(offer),sold=offer.sold,disabled=offer.sold,
                availability=offer.sold?null:ShopAvailability14(offer),buy=()=>Art07BuyOffer(offer),buttonName="Buy_"+offer.offerId,priceWidth=DicePriceWidth14,text=""};
            Art073DieView view=null;
            // The block holds the real die (drag turns it, a click opens the candidate's details); its hit area is the whole
            // block over the frame band, at least 60 units (SH-06).
            face.picture=(t,r)=>view=OfferDiePreview14(t,r,offer);
            var row=DrawCard13(parent,"Product_"+offer.offerId,x,y,w,h,face,CardSize13.Compact,()=>{if(!IsBusy)InspectOffer(offer);},!IsBusy);
            if(title.same)
            {
                // "同款": the second of two identical candidates (SHOP_LAYOUT 5.1).
                var name=row.transform.Find("Name").GetComponent<TextMeshProUGUI>();name.ForceMeshUpdate();
                var nameRect=name.rectTransform;float tagX=nameRect.anchoredPosition.x+name.textBounds.size.x+8;
                var tag=Surface11(row.transform,"SameTag14",tagX,-nameRect.anchoredPosition.y+2,ShopLabelWidth14("同款",16)+14,Mathf.Max(24,nameRect.rect.height-4),"ui_chip_label",UiKit.PanelLight);
                var box=ChipText11(tag);NoWrap11(UiKit.Text(tag,"SameTagText14","同款",box.x,box.y,box.width,box.height,Body11(16),LabelOn11(tag,UiKit.TextColor),TextAlignmentOptions.Center));
            }
            var price=row.transform.Find("Buy_"+offer.offerId) as RectTransform;
            float left=view!=null?((RectTransform)view.transform).anchoredPosition.x+((RectTransform)view.transform).rect.width+4:4;
            var inspect=HitArea073(row.transform,"Inspect",left,0,Mathf.Max(1,(price!=null?price.anchoredPosition.x:w)-4-left),h,()=>{if(!IsBusy)InspectOffer(offer);});
            DieTip14(inspect.gameObject,"die-"+offer.offerId,title.title,offer.die,NewDieTipLines14(offer),"点击查看这颗骰子");
            if(view!=null)DieTip14(view.gameObject,"die-"+offer.offerId,title.title,offer.die,NewDieTipLines14(offer),"点击查看这颗骰子");
        }
        /// <summary>价格 5 = 基础 3 + 点数大 1 + 颜色集中 1 (the offer's price reasons), and the dice count after buying.</summary>
        private string[] NewDieTipLines14(ShopOffer offer)
        {
            var terms=new List<string>();int sum=0;
            foreach(string reason in offer.priceReasons??new string[0])
            {
                string r=reason.Replace("高点数","点数大").Replace("小点数","点数小").Replace(" +"," ");
                terms.Add(r);int space=r.LastIndexOf(' ');if(space>0&&int.TryParse(r.Substring(space+1),NumberStyles.Integer,CultureInfo.InvariantCulture,out int n))sum+=n;
            }
            long net=Session.GetOfferNetPrice(offer);
            string line="价格 "+net+" = "+string.Join(" + ",terms);
            if(sum>offer.basePrice)line+="（上限 "+offer.basePrice+"）";
            if(net<offer.basePrice)line+=" − 打折 "+(offer.basePrice-net);
            int owned=ViewState.dice.Count(d=>d.lifetime=="permanent");
            return new[]{"点数和 "+offer.die.faces.Sum(f=>f.permanentPip),line,"买下后：骰子 "+(owned+1)+"/"+Config.maxPermanentDice+" 颗"};
        }

        // ---------------------------------------------------------------- 我的骰子 (SHOP_LAYOUT 2, 7)
        public const int OwnedDicePerPage14=8;
        private void ShopOwnedDice14(ShopLayout14 l,bool picking)
        {
            var dice=ViewState.dice.OrderBy(d=>d.createdSerial).ToArray();int permanent=dice.Count(d=>d.lifetime=="permanent");
            string title="我的骰子 "+permanent+(ViewState.debugWorkspace&&!CleanCapture11?"":"/"+Config.maxPermanentDice)+" · 提交栏 "+Session.EffectiveCapacity+" 格";
            int pages=Mathf.Max(1,Mathf.CeilToInt(dice.Length/(float)OwnedDicePerPage14));ownedDicePage14=Mathf.Clamp(ownedDicePage14,0,pages-1);
            var panel=Section14(main,"OwnedStrip","OwnedDiceTitle14",title,l.owned,out float titleRight,true,pages>1?l.owned.width-2*ShopPad14-60-6-ShopLabelWidth14("第 4/4 页",16)-6-60-16:440);
            // S1–S3: the table the face is chosen on is lit above the veil.
            if(picking)panel.GetComponent<Image>().color=UiKit.PanelLight;
            float right=l.owned.width-ShopPad14;
            if(pages>1)
            {
                // Only the debug workspace holds more than eight dice: eight per page.
                ShopButton14(panel,"DicePageNext14","›",right-60,4,60,60,()=>{ownedDicePage14=Math.Min(pages-1,ownedDicePage14+1);Refresh();},ownedDicePage14<pages-1,null,26);right-=60+6;
                string pageText="第 "+(ownedDicePage14+1)+"/"+pages+" 页";float pw=ShopLabelWidth14(pageText,16);
                var strip=panel.Find("HeaderStrip") as RectTransform;
                FitLine13(panel,"DicePage14",pageText,right-pw,(ShopHead14-Line11(16))*.5f,pw,16,LabelOn11(strip,UiKit.TextColor),TextAlignmentOptions.Center,false);right-=pw+6;
                ShopButton14(panel,"DicePagePrev14","‹",right-60,4,60,60,()=>{ownedDicePage14=Math.Max(0,ownedDicePage14-1);Refresh();},ownedDicePage14>0,null,26);right-=60+8;
            }
            const float cell=104;float gap=(l.owned.width-2*ShopPad14-4*cell)/3f,top=l.ownedHead;
            var shown=dice.Skip(ownedDicePage14*OwnedDicePerPage14).Take(OwnedDicePerPage14).ToArray();
            for(int i=0;i<shown.Length;i++)
            {
                var die=shown[i];string id=die.dieId;int position=Array.IndexOf(dice,die)+1;
                float x=ShopPad14+(i%4)*(cell+gap),y=top+(i/4)*(cell+6);
                var slot=Surface11(panel,"OwnedDieCell"+i,x,y,cell,cell,"ui_slot_inventory",UiKit.PanelLight);
                var view=DieView073(panel,"OwnedDie_"+id,"owned-"+id,die,x,y,cell,cell,.74f,false,false,()=>{if(!IsBusy&&!modal&&facePick14==null)ShowDie(id);});
                string label="第 "+position+" 颗 · "+DieTitle14(die,Config.generation).title;
                DieTip14(view.gameObject,"owned-"+id,label,die,new[]{"点数和 "+die.faces.Sum(f=>f.permanentPip)},picking?"":"点击查看这颗骰子");
                if(picking)FacePickTarget14(panel,die,x,y,cell);
            }
        }

        // ---------------------------------------------------------------- 我的构筑 (SHOP_LAYOUT 8)
        public const float BuildTileWidth14=100,BuildItemSize14=60;
        private void ShopBuild14(ShopLayout14 l)
        {
            controllerRects.Clear();
            var panel=Surface11(main,"Build14",l.build.x,l.build.y,l.build.width,l.build.height,"ui_section_panel",UiKit.Section(UiKit.Panel));
            var doomed=AltarVictims09();var cards=ViewState.controllers;
            // v1.1 (text proposal 5 #5): practice and the debug workspace keep their own prefix.
            string capacity=IsPracticeRun11?"练习模式 · 持有 "+cards.Count+" 张 · 占用栏位 "+ControllerRules.OrdinaryUsed(cards,Config):
                ViewState.debugWorkspace&&!CleanCapture11?"调试配置 · 持有 "+cards.Count+" · 占用栏位 "+ControllerRules.OrdinaryUsed(cards,Config):
                "卡牌栏位 "+ControllerRules.OrdinaryUsed(cards,Config)+"/"+ViewState.controllerSlots+" · 共 "+cards.Count+"/"+Config.maxControllerInstances+" 张";
            // v1.1.1 (W7b, RULES_DELTA 6): the count line says when the leftmost cultivation card is the one not counted.
            capacity+=FreeCultivationNote111(cards);
            float captionSize=Body11(18),captionY=4+(l.captionH-Line11(captionSize))*.5f,itemsX=l.build.width-ShopPad14-(5*BuildItemSize14+4*6);
            var head=FitLine13(panel,"BuildTitle14","我的构筑",ShopPad14,captionY,140,captionSize,LabelOn11(panel,UiKit.TextColor),TextAlignmentOptions.MidlineLeft,true);
            head.ForceMeshUpdate();float x=ShopPad14+head.textBounds.size.x+16;
            var cap=FitLine13(panel,"ControllerCapacity07",capacity,x,captionY,Mathf.Max(40,itemsX-x-16),captionSize,LabelOn11(panel,UiKit.Muted),TextAlignmentOptions.MidlineLeft,false);
            cap.ForceMeshUpdate();x+=cap.textBounds.size.x+14;
            var order=FitLine13(panel,"OwnedControllersTitle",doomed.Count>0?"红框的卡会被祭坛摧毁，拖动可避开":"拖动调整顺序",x,captionY,Mathf.Max(40,itemsX-x-16),captionSize,
                doomed.Count>0?CueOn11(panel,UiKit.Danger):LabelOn11(panel,UiKit.Muted),TextAlignmentOptions.MidlineLeft,false);
            float tileY=4+l.captionH+2;int shown=cards.Count>10?9:cards.Count;
            // V1.1 (W22i, W20Z2 finding): the row keeps 2 units inside the panel's drawn frame (BuildTileRow14).
            var row=BuildTileRow14(panel,l.build.height,tileY,l.tileH);float tileX=row.x,tileH=row.y;
            // One name size along the row (E3): the longest name decides.
            var tiles=TileRow13(shown,(i,px)=>
            {
                var owned=cards[i];
                var face=new CardFace13{contentId=owned.definitionId,enchantment=owned.enchantmentId,ordinal=i+1,namePx=px};
                return DrawCard13(panel,"Controller_"+owned.instanceId,tileX+i*(BuildTileWidth14+4),tileY,BuildTileWidth14,tileH,face,CardSize13.Tile,()=>ShowController(owned,true),!IsBusy);
            });
            for(int i=0;i<shown;i++)
            {
                var item=cards[i];bool altar=doomed.Contains(item.instanceId);var tile=tiles[i];
                if(altar)
                {
                    UiKit.Outline(tile.transform,"AltarVictim09",1,1,BuildTileWidth14-2,tileH-2,UiKit.Danger,3);
                    var name=tile.transform.Find("Name").GetComponent<TextMeshProUGUI>();name.color=CueOn11((Image)tile.targetGraphic,UiKit.Danger);
                }
                AttachControllerDrag(tile,item);
                Art07AttachTooltip(tile,item.instanceId,NameOf(item.definitionId),ShortText11(item.definitionId),BuildTileTip14(item),()=>ShowController(item,true));
            }
            if(cards.Count>10)ShopButton14(panel,"AllControllers14","全部 "+cards.Count+" 张",tileX+9*(BuildTileWidth14+4),tileY+(tileH-60)*.5f,BuildTileWidth14,60,ShowControllerOrder,!IsBusy,null,18);
            if(cards.Count==0)FitLine13(panel,"NoOwned",LootPage15?"还没有装备卡牌，领取后出现在这里。":"还没有装备卡牌，买下后出现在这里。",ShopPad14+8,tileY+(l.tileH-Line11(20))*.5f,Mathf.Max(40,itemsX-ShopPad14-24),20,LabelOn11(panel,UiKit.Muted),TextAlignmentOptions.MidlineLeft,false);
            // Items: 道具 1/3 over up to five cells.
            var itemsTitle=FitLine13(panel,"OwnedItemsTitle","道具 "+ViewState.inventory.Count+(ViewState.debugWorkspace&&!CleanCapture11?"":"/"+ViewState.consumableSlots),itemsX,captionY,5*BuildItemSize14+24,captionSize,LabelOn11(panel,UiKit.TextColor),TextAlignmentOptions.MidlineLeft,false);
            // V1.1 (W20Z2): the caption row was placed at fixed offsets (4 units from the top, ShopPad14 from the left), inside
            // the default frame but on a thicker one (deco's gold frame, comic's ink line): it keeps 2 units inside the drawn frame.
            KeepRowInsideFrame14(panel,new[]{head,cap,order},itemsTitle);
            int itemSlots=Math.Min(5,Math.Max(ViewState.consumableSlots,ViewState.inventory.Count));float itemY=tileY+(l.tileH-BuildItemSize14)*.5f;
            for(int i=0;i<itemSlots;i++)
            {
                float ix=itemsX+i*(BuildItemSize14+6);
                if(i>=ViewState.inventory.Count){Surface11(panel,"OwnedItemEmpty"+i,ix,itemY,BuildItemSize14,BuildItemSize14,"ui_slot_inventory",UiKit.PanelLight);continue;}
                var item=ViewState.inventory[i];
                var button=UiKit.Button(panel,"ShopItem_"+item.instanceId,"",ix,itemY,BuildItemSize14,BuildItemSize14,()=>ShowOwned(item,true),!IsBusy);
                UiKit.CardSurface(button,true);((Image)button.targetGraphic).sprite=UiKit.Template("ui_card_item");
                Icon(button.transform,item.contentId,8,6,BuildItemSize14-16);
                ItemUses111(button.transform,item,BuildItemSize14); // v1.1.1 (W7b): the key ring's uses left
                Art07AttachTooltip(button,item.instanceId,NameOf(item.contentId),PlayerRules11(item.contentId),"出售获得 1 金币",()=>ShowOwned(item,true));
            }
        }
        /// <summary>
        /// V1.1 (W20Z2): a row of labels drawn at fixed offsets keeps 2 units inside the drawn frame of its panel
        /// (FrameInsets11), measured on the labels' glyph boxes (GlyphBox11): when a style's frame is thicker than the
        /// offsets allow, the whole row moves down, and its leading labels right, by what their glyphs need. A row already
        /// clear of the frame does not move, so the styles it fits keep it where it was.
        /// </summary>
        private static void KeepRowInsideFrame14(RectTransform panel,TextMeshProUGUI[] leading,params TextMeshProUGUI[] trailing)
        {
            const float clearance=2;
            var frame=FrameInsets11(panel.GetComponent<Image>());
            float down=0,right=0;bool first=true;
            foreach(var label in leading.Concat(trailing))
            {
                if(label==null||!label.gameObject.activeSelf||!GlyphBox11(label,out var box))continue;
                // The panel's units run down from its top; a label's pivot is its top left (UiKit.Rent).
                var at=label.rectTransform.anchoredPosition;
                down=Mathf.Max(down,frame.w+clearance-(-at.y+box.yMin));
                if(first&&Array.IndexOf(leading,label)>=0){right=Mathf.Max(0,frame.x+clearance-(at.x+box.xMin));first=false;}
            }
            if(down<=0&&right<=0)return;
            foreach(var label in leading)if(label!=null)label.rectTransform.anchoredPosition+=new Vector2(right,-down);
            foreach(var label in trailing)if(label!=null)label.rectTransform.anchoredPosition+=new Vector2(0,-down);
        }
        /// <summary>
        /// V1.1 (W22i; W20Z2 finding 2): the 我的构筑 tiles were placed at fixed offsets, ShopPad14 from the panel's left and
        /// ShopFoot14 above its foot: inside the default frame, but on a thicker one (deco's gold frame, sancai's gold rim,
        /// comic's ink line and shadow) their edges sat on the frame line. The row keeps 2 units inside the panel's drawn
        /// frame (FrameInsets11, the W20Z2 measure): it moves right by what the first tile needs, and the tiles lose the
        /// height the foot needs (their picture block shrinks with them, DrawTile13). A style whose frame leaves the room keeps
        /// the row where and as tall as it was. Returns (left of the first tile, tile height), in panel units.
        /// </summary>
        private static Vector2 BuildTileRow14(RectTransform panel,float panelHeight,float top,float height)
        {
            const float clearance=2;
            var frame=FrameInsets11(panel.GetComponent<Image>());// x left, y bottom, z right, w top
            float left=Mathf.Max(ShopPad14,frame.x+clearance);
            float tall=Mathf.Min(height,panelHeight-frame.y-clearance-top);
            return new Vector2(left,Mathf.Max(1,tall));
        }
        /// <summary>A tile's hover: its enchantment and its sale price; a card bought in this shop and capped at the price
        /// paid says so (C98, RULES_DELTA 1.1 item 9).</summary>
        private string BuildTileTip14(ControllerInstance item)=>EnchantLabel(item.enchantmentId)+"\n"+SaleText14(item);
        /// <summary>卖价 N 金币, or 卖价 1（本店买入，不超过买价 1，离店后恢复） while the same-shop cap holds.</summary>
        private string SaleText14(ControllerInstance item)
        {
            long sale=Session.GetControllerSalePrice(item.instanceId);
            var paid=ViewState.phase=="Shop"?ViewState.shop?.paidControllerPrices?.FirstOrDefault(p=>p!=null&&p.instanceId==item.instanceId):null;
            bool capped=paid!=null&&paid.paid<ControllerRules.SalePrice(item,Config,false)&&sale<=paid.paid;
            return capped?"卖价 "+sale+"（本店买入，不超过买价 "+paid.paid+"，离店后恢复）":"卖价 "+sale+" 金币";
        }

        // ---------------------------------------------------------------- 下一关 (SHOP_LAYOUT 4)
        /// <summary>Picks still to be taken from the open loot and gift groups (void when the player leaves).</summary>
        private void OpenPicks14(out int loot,out int gifts)
        {
            loot=gifts=0;
            foreach(var g in ViewState.shop?.groups??new List<ShopGroup>())
            {
                if(g.closed)continue;int left=Math.Max(0,g.picks-g.picked);
                if(g.groupId=="LOOT")loot+=left;else gifts+=left;
            }
        }
        private void ShopLeave14(ShopLayout14 l)
        {
            bool pending=Session.IsLootPending,enabled=!IsBusy&&!Session.HasPendingSave&&!pending;bool c=Large11;
            OpenPicks14(out int loot,out int gifts);
            string note=pending?"先领取首领战利品":loot>0&&gifts>0?"还有 "+(loot+gifts)+" 件战利品和赠礼未领，离开后作废":loot>0?"还有 "+loot+" 件战利品未领，离开后作废":gifts>0?"还有 "+gifts+" 件赠礼未领，离开后作废":null;
            var leave=UiKit.ActionButton(main,"LeaveShop","下一关",l.leave.x,l.leave.y,l.leave.width,l.leave.height,()=>{CloseModal();CancelFacePick14();Send(new SessionCommand{kind=CommandKind.LeaveShop});},UiKit.Primary,enabled);
            var face=(Image)leave.targetGraphic;float faceH=l.leave.height-7;
            var label=leave.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            label.fontSize=(c?20:34)*UiKit.FontScale;label.fontStyle=FontStyles.Bold;UiKit.Role(label,TextRole.PrimaryButton);NoWrap11(label);label.color=LabelOn11(face,enabled?(Color?)null:label.color);
            float labelW=label.GetPreferredValues("下一关").x,arrow=c?30:42,labelH=Mathf.Ceil(label.fontSize*1.3f);
            float noteH=note!=null?Line11(Body11(18)):0,blockH=labelH+noteH,top=Mathf.Max(0,(faceH-blockH)*.5f);
            float startX=(l.leave.width-(labelW+10+arrow))*.5f;
            label.alignment=TextAlignmentOptions.MidlineLeft;label.rectTransform.anchoredPosition=new Vector2(startX,-top);label.rectTransform.sizeDelta=new Vector2(labelW+4,labelH);
            var arrowImage=UiKit.Box(leave.transform,"Arrow",startX+labelW+10,top+(labelH-arrow)*.5f,arrow,arrow,label.color).GetComponent<Image>();
            arrowImage.sprite=UiKit.Template("ui_icon_arrow");arrowImage.preserveAspect=true;arrowImage.raycastTarget=false;
            if(note!=null)NoWrap11(UiKit.Text(leave.transform,"LeaveShopNote14",note,12,top+labelH,l.leave.width-24,noteH,Body11(18),LabelOn11(face,enabled?(Color?)null:label.color),TextAlignmentOptions.Center));
        }

        // ---------------------------------------------------------------- entrance (S10, v0.73 timing)
        private void BindShopEntrance14()
        {
            var shelf=new[]{"Stats07","ControllerShelf","ShopProducts","Build14","DiceCounter","OwnedStrip","FacePick14","LeaveShop"}.Where(n=>!cameFromLoot15||n!="Stats07"&&n!="Build14"&&n!="OwnedStrip").Select(n=>main.Find(n) as RectTransform).Where(r=>r!=null&&r.gameObject.activeInHierarchy).ToList();
            var cards=new List<RectTransform[]>();
            var controllers=ViewState.shop.offers.Where(o=>o.category=="controller"&&string.IsNullOrEmpty(o.groupId)).ToArray();
            foreach(var offer in controllers)cards.Add(new[]{main.Find("Offer_"+offer.offerId) as RectTransform});
            int capacity=Config.shop.initialControllerOffers+ViewState.controllerOfferPurchases;
            for(int i=controllers.Length;i<capacity;i++)cards.Add(new[]{main.Find("EmptyControllerOffer"+i) as RectTransform});
            // One group per goods section keeps the whole first entrance inside its 0.52 s budget however many goods there are.
            foreach(string sectionName in new[]{"ShopProducts","DiceCounter"})
            {
                var section=main.Find(sectionName);if(section==null)continue;
                var group=section.Cast<Transform>().OfType<RectTransform>().Where(r=>r.gameObject.activeSelf&&r.name.StartsWith("Product_",StringComparison.Ordinal)).ToArray();
                if(group.Length>0)cards.Add(group);
            }
            UiKit.Component<Art07ShopEntrance>(main.gameObject).Bind(ViewState.shop.generationId,reduced,instant07,()=>modal||inMenu,shelf.ToArray(),cards.Where(g=>g.All(r=>r!=null)).ToArray());
        }

        // ---------------------------------------------------------------- dice tooltips (SHOP_LAYOUT 5.3)
        /// <summary>A hover over a die: its title, the six faces as pip tiles in their colours (gold faces with a gold rim,
        /// no face numbers), and a few lines.</summary>
        private void DieTip14(GameObject target,string owner,string title,DieInstance die,string[] lines,string more)
        {
            var hover=UiKit.Component<Art07HoverTip>(target);
            hover.show=()=>
            {
                if(modal||IsControllerDragging||IsBusy)return;
                var rect=(RectTransform)target.transform;var corners=new Vector3[4];rect.GetWorldCorners(corners);
                Vector2 at=ScreenToDesign(RectTransformUtility.WorldToScreenPoint(null,corners[3]));
                const float w=420;float tile=48,lineH=Line11(19),h=16+Line11(22)+8+tile+Line11(16)+8+lines.Length*lineH+(string.IsNullOrEmpty(more)?0:lineH)+16;
                float x=Mathf.Clamp(at.x+12,24,1920-24-w),y=Mathf.Clamp(at.y+10,24,1080-24-h);
                art07TooltipOwner=owner;art07Tooltip=UiKit.Frame(overlay,"ContentTooltip",x,y,w,h,UiKit.Paper,UiKit.Ink);
                Color ink=LabelOn11(art07Tooltip,UiKit.Ink);
                float ty=16;
                FitLine13(art07Tooltip,"Name",title,16,ty,w-32,22,ink,TextAlignmentOptions.MidlineLeft,true);ty+=Line11(22)+8;
                FaceStrip14(art07Tooltip,die,16,ty,tile,ink);ty+=tile+Line11(16)+8;
                for(int i=0;i<lines.Length;i++){FitLine13(art07Tooltip,"Line"+i,lines[i],16,ty,w-32,19,ink,TextAlignmentOptions.MidlineLeft,false);ty+=lineH;}
                if(!string.IsNullOrEmpty(more))FitLine13(art07Tooltip,"More",more,16,ty,w-32,18,ink,TextAlignmentOptions.MidlineLeft,false);
                UiKit.Component<Art07PanelMotion>(art07Tooltip.gameObject).Enter(.08f,.06f,true,reduced);
            };
            hover.hide=()=>{if(art07TooltipOwner==owner&&art07Tooltip!=null){art07Tooltip.gameObject.SetActive(false);art07TooltipOwner=null;}};
        }
        /// <summary>The six faces of a die in a row: pip tiles coloured like the faces (with colour assist, blue pips are
        /// diamonds), a gold rim on gold faces, the texture written under a tile (光滑 / 粗糙). Returns the tiles' row.</summary>
        private RectTransform FaceStrip14(Transform parent,DieInstance die,float x,float y,float tile,Color caption,Action<string> pick=null,string selected=null,string prefix="FaceTile14_",float gap=8,
            ICollection<string> alsoSelected=null,ICollection<string> legal=null)
        {
            var row=UiKit.Rect(parent,"FaceStrip14",x,y,6*tile+5*gap,tile+Line11(16));
            for(int i=0;i<die.faces.Length&&i<6;i++)
            {
                var f=die.faces[i];float fx=i*(tile+gap);
                RectTransform t;
                if(pick!=null)
                {
                    string id=f.faceId;var b=UiKit.Button(row,prefix+f.faceId,"",fx,0,tile,tile,()=>pick(id),true);
                    var bf=(Image)b.targetGraphic;bf.sprite=UiKit.Template("ui_frame_block");bf.type=bf.sprite!=null?Image.Type.Sliced:Image.Type.Simple;bf.color=UiKit.Paper;
                    b.transform.Find("Step")?.gameObject.SetActive(false);b.transform.Find("Lip")?.gameObject.SetActive(false);b.transform.Find("Label")?.gameObject.SetActive(false);
                    ((RectTransform)bf.transform).anchoredPosition=Vector2.zero;((RectTransform)bf.transform).sizeDelta=new Vector2(tile,tile);
                    t=(RectTransform)b.transform;
                }
                else{var plain=UiKit.Box(row,prefix+f.faceId,fx,0,tile,tile,UiKit.Paper);var pi=plain.GetComponent<Image>();pi.sprite=UiKit.Template("ui_frame_block");pi.type=pi.sprite!=null?Image.Type.Sliced:Image.Type.Simple;t=plain;}
                Color pip=f.baseColor=="red"?UiKit.Red:UiKit.Blue;bool diamond=colorAssist07&&f.baseColor!="red";
                float dot=Mathf.Max(5,tile*.17f);
                void Pip(float px,float py)
                {
                    var p=UiKit.Box(t,"Pip"+px.ToString("0.00",CultureInfo.InvariantCulture)+"_"+py.ToString("0.00",CultureInfo.InvariantCulture),tile*px-dot*.5f,tile*py-dot*.5f,dot,dot,pip);
                    var image=p.GetComponent<Image>();image.raycastTarget=false;
                    if(diamond){image.sprite=null;p.localRotation=Quaternion.Euler(0,0,45);p.sizeDelta=new Vector2(dot*.8f,dot*.8f);}
                    else image.sprite=UiKit.Template("ui_pip_dot");
                }
                int v=f.permanentPip;
                if(v%2==1)Pip(.5f,.5f);
                if(v>=2){Pip(.28f,.28f);Pip(.72f,.72f);}
                if(v>=4){Pip(.28f,.72f);Pip(.72f,.28f);}
                if(v==6){Pip(.28f,.5f);Pip(.72f,.5f);}
                if(f.isGold)UiKit.Ring(t,"GoldRim14",-3,-3,tile+6,tile+6,UiKit.Gold);
                // v1.1.1 (W7b): a face the upgrade cannot change is dimmed; with two faces chosen both are ringed.
                if(legal!=null&&!legal.Contains(f.faceId)){var dim=UiKit.Box(t,"Unavailable14",0,0,tile,tile,Alpha14(UiKit.Shadow,.45f)).GetComponent<Image>();dim.raycastTarget=false;}
                if(f.faceId==selected||alsoSelected!=null&&alsoSelected.Contains(f.faceId))UiKit.Ring(t,"Selected14",-5,-5,tile+10,tile+10,UiKit.Gold);
                string texture=f.texture=="smooth"?"光滑":f.texture=="rough"?"粗糙":"";
                if(texture.Length>0)NoWrap11(UiKit.Text(row,"Texture14_"+f.faceId,texture,fx-gap*.5f,tile,tile+gap,Line11(16),Body11(16),caption,TextAlignmentOptions.Center));
            }
            return row;
        }
    }
}
