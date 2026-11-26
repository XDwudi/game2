using System;
using System.Collections.Generic;
using System.Linq;
using DiceCraft.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        public Sprite[] lootIllustrations15;
        private readonly HashSet<string> missingLoot15 = new HashSet<string>();
        private GameSession lootSession15;
        private string lootGeneration15, lastPhase15, lootGroup15;
        private bool lootPage15, lootFromShop15, cameFromLoot15;
        private bool armLootReveal15;
        private static GameApp guardedPage15;
        private double pointerReady15;
        public bool LootPage15 => lootPage15;
        public bool LootFromShop15 => lootFromShop15;
        public static bool PagePointerGuard15 => guardedPage15 != null && Time.realtimeSinceStartupAsDouble < guardedPage15.pointerReady15;

        private void ArmPagePointerGuard15()
        {
            guardedPage15 = this;
            pointerReady15 = Time.realtimeSinceStartupAsDouble + .3;
        }

        private void SyncLootPage15()
        {
            bool sameSession = ReferenceEquals(lootSession15, Session);
            bool fromSettlement = sameSession && lastPhase15 == "StageSettlement";
            lootSession15 = Session;
            lastPhase15 = inMenu ? null : ViewState?.phase;
            var shop = lastPhase15 == "Shop" ? ViewState.shop : null;
            if (shop == null || !sameSession || lootGeneration15 != shop.generationId)
            {
                lootGeneration15 = shop?.generationId;
                cameFromLoot15 = lootFromShop15 = false;
                lootPage15 = shop != null && Session.IsLootPendingIn(ViewState);
                lootGroup15 = lootPage15 ? "LOOT" : null;
                armLootReveal15 = lootPage15 && fromSettlement;
                if (!armLootReveal15) ClearLootReveal15();
            }
            else if (Session.IsLootPendingIn(ViewState) && !lootPage15)
            {
                lootGroup15 = "LOOT";
                lootPage15 = true;
            }
        }

        private void OpenClaims15()
        {
            if (Session.HasPendingSave || IsBusy) return;
            var groups = ClaimGroups15();
            if (groups.Length == 0) return;
            lootGroup15 = groups.FirstOrDefault(g => !g.closed)?.groupId ?? groups[0].groupId;
            lootFromShop15 = lootPage15 = true;
            ArmPagePointerGuard15();
            Refresh();
        }

        private ShopGroup[] ClaimGroups15() => ViewState.shop.groups.Where(g => g.groupId != "LOOT" ||
            !g.closed || g.groupId == lootGroup15 && lootPage15).ToArray();

        private void EnterShop15()
        {
            if (!lootPage15 || IsBusy || modal || facePick14 != null || Session.HasPendingSave || Session.IsLootPending) return;
            cameFromLoot15 = true;
            lootPage15 = lootFromShop15 = false;
            lootGroup15 = null;
            ClearLootReveal15();
            ArmPagePointerGuard15();
            Refresh();
        }

        private Sprite LootPictureSprite15(string lootKind)
        {
            if (string.IsNullOrEmpty(lootKind)) return null;
            string name = "illustration_" + lootKind;
            var themed = ThemeRegistry.Current?.ContentArt(name);
            if (themed != null) return themed;
            var fallback = lootIllustrations15?.FirstOrDefault(s => s != null && string.Equals(s.name, name, StringComparison.OrdinalIgnoreCase));
            if (fallback == null || ThemeId11 != GameTheme.DefaultId)
                RecordMissingArt15(name, fallback == null ? "placeholder" : "default");
            return fallback;
        }

        private void RecordMissingArt15(string name, string fallback)
        {
            string item = ThemeId11 + "/" + name + " " + fallback;
            if (missingLoot15.Add(item)) Debug.Log("W15_MISSING_ART " + item);
        }

        private void DrawLootPicture15(RectTransform parent,string kind,float x,float y,float size)
        {
            var sprite=LootPictureSprite15(kind);
            var picture=UiKit.Box(parent,"LootPicture15",x,y,size,size,sprite!=null?Color.white:Color.clear);
            var image=picture.GetComponent<Image>();
            image.sprite=sprite;image.type=Image.Type.Simple;image.preserveAspect=true;image.raycastTarget=false;
            if(sprite!=null)return;
            UiKit.Outline(picture,"MissingLoot15",0,0,size,size,UiKit.Muted,2);
            UiKit.Text(picture,"MissingPicture15",NameOf(kind).Substring(0,1),4,4,size-8,size-8,30,LabelOn11(parent),TextAlignmentOptions.Center);
        }

        private static int LootRemaining15(ShopGroup g) => g.closed ? 0 : Math.Max(0, g.picks - g.picked);
        private static string LootStatus15(ShopGroup g) => g.closed ? "已领完" : "还可以领 " + LootRemaining15(g) + " 件";
        private string GroupName15(ShopGroup g) => g.groupId == "LOOT" ? "首领战利品 · " + NameOf(g.kind) : NameOf(g.kind) + "的赠礼";

        private void DrawLootPage15(ShopLayout14 layout)
        {
            var group = ViewState.shop.groups.First(g => g.groupId == lootGroup15);
            var choices = ViewState.shop.offers.Where(o => o.groupId == group.groupId && o.category != "gold").ToArray();
            var area = new Rect(layout.cards.x, layout.cards.y, layout.cards.width, layout.build.y - ShopGap14 - layout.cards.y);
            var tabs = lootFromShop15 ? ClaimGroups15() : null;
            bool manyTabs = tabs != null && tabs.Length > 1;
            string heading = manyTabs ? "" : lootFromShop15 ? GroupName15(group) : "选择战利品";
            var panel = Section14(main, "LootChoices15", "GiftTitle_" + group.groupId, heading, area, out _, titleMax:790);
            if (manyTabs) DrawClaimTabs15(panel, tabs);
            else FitLine13(panel, "LootStatus15", LootStatus15(group), area.width-330, (ShopHead14-Line11(22))*.5f,
                290, 22, LabelOn11(panel.Find("HeaderStrip") as RectTransform), TextAlignmentOptions.MidlineRight, false);
            var cards = new List<RectTransform>();
            const float width = 220, gap = 48;
            float height = Large11 ? 364 : 348, left = (area.width - choices.Length * width - (choices.Length-1)*gap)*.5f;
            var names = group.kind == "L5" ? DieTitles14(choices.Select(o=>o.die).ToArray()) : null;
            for(int i=0;i<choices.Length;i++)
                cards.Add(DrawLootCard15(panel, choices[i], group, names == null ? default : names[i], left+i*(width+gap), 200-area.y, width, height));
            if(choices.Length==0)
                UiKit.Text(panel,"LootOnlyCash15","这一组没有能领的东西，只能折现。",64,190,area.width-128,90,24,LabelOn11(panel),TextAlignmentOptions.Center);
            if(!lootFromShop15 && !group.closed && group.picked==0)
            {
                var cash = ViewState.shop.offers.First(o=>o.groupId==group.groupId && o.category=="gold");
                float y = (Large11?616:600)-area.y, w = Large11?280:240;
                var button = ShopButton14(panel,"LootCashOut15","折现",340-area.x,y,w,Large11?64:60,()=>Art07BuyOffer(cash),!IsBusy&&!Session.HasPendingSave);
                var label = button.transform.Find("Label").GetComponent<TextMeshProUGUI>();
                float labelW = label.GetPreferredValues("折现").x, priceW = ShopLabelWidth14(Config.lootGoldValue.ToString(),22);
                float x = (w-labelW-8-26-4-priceW)*.5f;
                label.alignment=TextAlignmentOptions.MidlineLeft;
                label.rectTransform.anchoredPosition=new Vector2(x,0);
                label.rectTransform.sizeDelta=new Vector2(labelW+4,60);
                var coin=UiKit.Box(button.transform,"Coin",x+labelW+8,17,26,26,Color.white).GetComponent<Image>();
                coin.sprite=UiKit.Template("ui_icon_coin");coin.preserveAspect=true;
                UiKit.Text(button.transform,"Price",Config.lootGoldValue.ToString(),x+labelW+38,0,priceW,60,22,LabelOn11((Image)button.targetGraphic,UiKit.Gold),TextAlignmentOptions.MidlineLeft);
                var caption=UiKit.Text(panel,"LootCashNote15","放弃这一组，换成 "+Config.lootGoldValue+" 金币。只能在还没领取时选择。",
                    340-area.x+w+24,y,Large11?760:800,Large11?92:60,20,LabelOn11(panel),TextAlignmentOptions.MidlineLeft);
                caption.text=BalancedLines11(caption,caption.text,caption.rectTransform.rect.width);
            }
            if(!group.closed && choices.Any(o=>!o.sold && ShopAvailability14(o).Contains("满") && o.category!="new_die"))
            {
                float y=(Large11?724:688)-area.y,h=Large11?92:60;
                UiKit.Outline(panel,"LootRoomFrame15",340-area.x,y,1064,h,UiKit.Muted,2);
                UiKit.Text(panel,"LootRoomHint15","栏位满了：点下面“我的构筑”里的卡牌或道具可以卖掉，腾出位置。",
                    354-area.x,y,1036,h,20,LabelOn11(panel),TextAlignmentOptions.MidlineLeft);
            }
            DrawLootInfo15(layout,group);
            ShopBuild14(layout);
            DrawEnterShop15(layout,group);
            if(facePick14!=null)
            {
                UiKit.Box(main,"FacePickVeil14",0,0,1920,1080,Alpha14(UiKit.Shadow,.6f),true);
                FacePickPanel14(layout);
            }
            ShopOwnedDice14(layout,facePick14!=null);
            BindLootReveal15(cards, armLootReveal15);
            armLootReveal15=false;
        }

        private void DrawClaimTabs15(RectTransform panel, ShopGroup[] groups)
        {
            // The transparent tabs reveal the header strip. Keep it in their real background chain.
            var strip=(RectTransform)panel.Find("HeaderStrip");
            float x=20-strip.anchoredPosition.x,y=4+strip.anchoredPosition.y,w=(panel.rect.width-40-(groups.Length-1)*8)/groups.Length;
            foreach(var g in groups)
            {
                var b=UiKit.FlatButton(strip,"GiftTab_"+g.groupId,GroupName15(g)+"（"+LootStatus15(g)+"）",x,y,w,60,()=>
                {lootGroup15=g.groupId;facePick14=null;Refresh();},!IsBusy,g.groupId==lootGroup15);
                var t=b.transform.Find("Label").GetComponent<TextMeshProUGUI>();
                t.fontSize=22*UiKit.FontScale;t.color=LabelOn11((Image)b.targetGraphic);
                while(t.GetPreferredValues(t.text).x>w-20 && t.fontSize>19.5f)t.fontSize=Mathf.Max(19.5f,t.fontSize-1);
                x+=w+8;
            }
        }

        private void DrawLootInfo15(ShopLayout14 l, ShopGroup group)
        {
            var r=l.dice;
            var info=Surface11(main,"LootInfo15",r.x,r.y,r.width,r.height,"ui_section_panel",UiKit.Section(UiKit.Panel));
            float size=Large11?144:200, y=24, left=24, available=r.width-2*left;
            bool boss=group.groupId=="LOOT";
            float x=(r.width-size)*.5f;
            if(boss)DrawLootPicture15(info,group.kind,x,y,size);
            else if(ContentSprite071(group.kind) is Sprite art)
            {
                var image=UiKit.Box(info,"LootPicture15",x,y,size,size,Color.white).GetComponent<Image>();
                image.sprite=art;image.preserveAspect=true;
            }
            else
            {
                UiKit.Outline(info,"MissingLoot15",x,y,size,size,UiKit.Muted,2);
                UiKit.Text(info,"MissingName15",NameOf(group.kind).Substring(0,1),x,y,size,size,30,LabelOn11(info),TextAlignmentOptions.Center);
            }
            y+=size+16;
            var name=FitLine13(info,"LootName15",boss?NameOf(group.kind):GroupName15(group),left,y,available,32,LabelOn11(info),
                TextAlignmentOptions.Center,true,role:TextRole.BossName);
            y+=name.rectTransform.rect.height+8;
            string shortText=ShortText11(group.kind),details=boss?Config.Loot(group.kind).details:null;
            void Paragraph(string id,string content,float font,Color ink)
            {
                if(string.IsNullOrWhiteSpace(content))return;
                var t=UiKit.Text(info,id,content,left,y,available,r.height-y-16,font,ink,TextAlignmentOptions.TopLeft);
                t.text=BalancedLines11(t,content,available);t.ForceMeshUpdate();
                float h=Mathf.Ceil(t.GetPreferredValues(t.text,available,0).y)+2;
                t.rectTransform.sizeDelta=new Vector2(available,h);y+=h+10;
            }
            Paragraph("LootShort15",shortText,24,LabelOn11(info));
            Paragraph("LootDetails15",details,19.5f,LabelOn11(info,UiKit.Muted));
        }

        private RectTransform DrawLootCard15(RectTransform panel,ShopOffer o,ShopGroup group,DieName14 dieName,float x,float y,float w,float h)
        {
            bool unselected=group.closed&&!o.sold;
            var card=new CardFace13{contentId=o.contentId,category=o.category,enchantment=o.controller?.enchantmentId??"none",
                disabled=o.sold,sold=o.sold,price=unselected?-1:0,claim=true,buttonName="Buy_"+o.offerId,
                availability=ShopAvailability14(o),buy=()=>Art07BuyOffer(o)};
            if(o.category=="controller") card.value=ShopCardValue14(o.controller,out card.valueWarning);
            if(o.category=="new_die")
            {
                card.rewardDie=o.die;card.label=dieName.title;card.text=dieName.subtitle;
                card.picture=(t,r)=>OfferDiePreview14(t,r,o);
            }
            if(o.category=="hand_upgrade")
            {
                bool color=Config.colorDefinitions.Any(d=>d.id==o.contentId);
                int level=(color?ViewState.colorLevels:ViewState.handLevels).First(v=>v.handId==o.contentId).level;
                int max=color?Config.maxColorLevel:Config.Hand(o.contentId).maxLevel;
                int next=Math.Min(max,level+Math.Max(1,o.levels));
                // v1.1.1 (W7a): a hand has a multiplier, a colour a bonus of the additive zone ("加成 +0.75 → +1.25").
                int basis=color?0:Config.Hand(o.contentId).baseMult100,step=color?0:Config.Hand(o.contentId).incrementPerLevel100;
                card.text="直接升级：Lv."+level+" → Lv."+next+"\n"+(color?ColorUpgrade111(o.contentId,level,next-level):"倍率 ×"+Factor(basis+step*level)+" → ×"+Factor(basis+step*next));
                card.picture=(t,r)=>HandPattern14(t,o.contentId,color,r);
            }
            var b=DrawCard13(panel,(o.category=="controller"?"Offer_":"Product_")+o.offerId,x,y,w,h,card,
                CardSize13.Shop,()=>{if(!IsBusy)InspectOffer(o);},!IsBusy&&!unselected);
            if(!unselected)Art07AttachTooltip(b,o.offerId,card.label??NameOf(o.contentId),card.text??PlayerRules11(o.contentId),
                o.sold?"已领取":string.IsNullOrEmpty(card.availability)?"免费":card.availability,()=>InspectOffer(o));
            var groupAlpha=UiKit.Component<CanvasGroup>(b.gameObject);
            groupAlpha.alpha=unselected?.35f:1f;groupAlpha.interactable=!unselected;groupAlpha.blocksRaycasts=!unselected;
            return (RectTransform)b.transform;
        }

        private void DrawEnterShop15(ShopLayout14 l,ShopGroup group)
        {
            var r=l.leave;bool can=!IsBusy&&!Session.HasPendingSave&&!Session.IsLootPending;
            if(lootFromShop15){ShopButton14(main,"LootEnterShop15","返回商店",r.x,r.y,r.width,r.height,EnterShop15,can);return;}
            var b=UiKit.ActionButton(main,"LootEnterShop15","进入商店",r.x,r.y,r.width,r.height,EnterShop15,UiKit.Primary,can);
            var face=(Image)b.targetGraphic;var label=b.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            string note=Session.IsLootPending?"先领取或折现":group.closed?null:"剩下 "+LootRemaining15(group)+" 件在商店右上角也能领";
            label.fontStyle=FontStyles.Bold;UiKit.Role(label,TextRole.PrimaryButton);label.fontSize=(Large11?20:34)*UiKit.FontScale;
            label.color=LabelOn11(face,can?(Color?)null:UiKit.Muted);NoWrap11(label);
            float top=4, mainH=note==null?r.height-10:Large11?34:49;
            float labelW=Mathf.Ceil(label.GetPreferredValues(label.text).x)+4, arrow=Large11?26:36;
            float x=(r.width-labelW-arrow-10)*.5f;
            label.rectTransform.anchoredPosition=new Vector2(x,-top);label.rectTransform.sizeDelta=new Vector2(labelW,mainH);
            var icon=UiKit.Box(b.transform,"Arrow",x+labelW+10,top+(mainH-arrow)*.5f,arrow,arrow,label.color).GetComponent<Image>();
            icon.sprite=UiKit.Template("ui_icon_arrow");icon.preserveAspect=true;
            if(note!=null)
            {
                var caption=UiKit.Text(b.transform,"LootEnterNote15",note,8,top+mainH-(Large11?6:0),r.width-16,r.height-mainH-10,
                    Large11?19.5f/UiKit.FontScale:19.5f,LabelOn11(face),TextAlignmentOptions.Center);NoWrap11(caption);
                if(GlyphBox11(caption,out var ink))
                {
                    var frame=FrameInsets11(face);
                    float innerBottom=face.rectTransform.rect.height-frame.y-1;
                    float over=-caption.rectTransform.anchoredPosition.y+ink.yMax-innerBottom;
                    if(over>0)caption.rectTransform.anchoredPosition+=Vector2.up*over;
                    if(GlyphBox11(label,out var heading))
                    {
                        float noteTop=-caption.rectTransform.anchoredPosition.y+ink.yMin;
                        float lift=Mathf.Min(top+heading.yMax+1-noteTop,top+heading.yMin-frame.w);
                        if(lift>0)
                        {
                            label.rectTransform.anchoredPosition+=Vector2.up*lift;
                            icon.rectTransform.anchoredPosition+=Vector2.up*lift;
                        }
                    }
                }
            }
        }

        private void InspectLootUpgrade15(ShopOffer o)
        {
            var p=Art07Modal(NameOf(o.contentId),480,180,960,720);
            Icon(p,o.contentId,24,112,176);
            var list=PaperDetailScroll071(p,"OfferDetail",232,104,704,492);float y=0;
            Art07DetailSection(list,"Skill","效果",PlayerRules11(o.contentId),ref y,672,true);
            string reason=ShopAvailability14(o);
            Art07DetailSection(list,"Price","价格",string.IsNullOrEmpty(reason)?"免费领取":reason,ref y,672,true);
            list.sizeDelta=new Vector2(list.sizeDelta.x,y+16);
            ShopButton14(p,"ConfirmPurchase","免费领取",24,628,912,64,()=>BeginFacePick14(o),string.IsNullOrEmpty(reason)&&!IsBusy);
        }
    }
}
