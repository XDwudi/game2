using System;
using System.Collections.Generic;
using System.Linq;
using DiceCraft.Core;
using TMPro;
using UnityEngine;

namespace DiceCraft
{
    // V1.1 task W13 (CARD_TEMPLATE §8, CT-01/CT-02/CT-04): a debug gallery that lays the card template out in every size,
    // rarity, enchantment, value badge and disabled state, the goods without a rarity and every price-button state, drawn by
    // the production template over the whole screen. It is an inspection view for tests and reviews, not a game screen: the
    // shop (W14) and the loot and settlement pages (W15) adopt the template in their own layouts. The last page lays out
    // the compact icons the W13a review asked to see (icons flagged for an edit, the closest look-alike pairs).
    public sealed partial class GameApp
    {
        public const int CardGalleryPages13=7;
        public static readonly string[] CardGalleryTitles13={"商店尺寸 · 常见与进阶","商店尺寸 · 稀有与商品","桌面尺寸","紧凑尺寸 · 两行卡","紧凑尺寸 · 文字卡","紧凑图标审查","货品行与图块"};
        private static readonly string[] CardGalleryLegends13=
        {
            "220×348（放大文字 220×364）· 行：常见、进阶 · 列：无角标、附魔、数值、附魔+数值、失效、失效+附魔、警示、警示+附魔",
            "220×348（放大文字 220×364）· 上行：稀有，列同前页 · 下行：道具、服务、骰面改造、新骰子，各两种价格状态",
            "100% 时为对局卡排七张的宽度；放大文字时对局改用紧凑尺寸，此处为满宽 216 · 行：常见、进阶、稀有 · 列同商店页",
            "280×117（对局两行卡）· 每种稀有度两行：前四种、后四种角标组合 · 末行：四类商品",
            "280×244（放大文字、五张以内）· 行：常见、进阶、稀有 · 列：前六种角标组合",
            "280×117 两行卡 · (a) 待修图与临界 · (b) 最相近的图标对",
            "W14 · E2 货品行（商店“升级与服务”与“骰子与骰面”的行高）各种价格状态 · E3 我的构筑图块（三种稀有度，序号、附魔）",
        };
        /// <summary>The W13a review rows (coordinator, 2026-11-17): ids still flagged for an edited icon, then the borderline ones.</summary>
        public static readonly string[] IconReviewFlagged13={"C33","C34","C107","C110","C111","C88","C103","C113","C126","C127","C130","C131"};
        /// <summary>The closest look-alike pairs, drawn side by side.</summary>
        public static readonly string[][] IconReviewPairs13={new[]{"C40","C87"},new[]{"I11","U08"},new[]{"U08","U10"},new[]{"C08","C22"},new[]{"C08","C79"}};
        private RectTransform cardGallery13;

        /// <summary>Draws gallery page <paramref name="page"/> (see <see cref="CardGalleryTitles13"/>) above the whole screen
        /// and returns its root.</summary>
        public RectTransform ShowCardGallery13(int page)
        {
            page=Mathf.Clamp(page,0,CardGalleryPages13-1);
            if(cardGallery13==null)cardGallery13=UiKit.Rect(canvas.transform,"CardGallery13",0,0,1920,1080);
            cardGallery13.anchoredPosition=main.anchoredPosition;
            cardGallery13.gameObject.SetActive(true);cardGallery13.SetAsLastSibling();UiKit.Clear(cardGallery13);
            SetGalleryBackdrop11(true);// V1.1 (W20X): not the play layout
            var root=cardGallery13;
            var backdrop=UiKit.Box(root,"GalleryBackdrop13",0,0,1920,1080,UiKit.Background,true);
            float barH=Mathf.Max(64,Mathf.Ceil(22*UiKit.FontScale*1.46f)+28);
            var bar=Surface11(root,"GalleryTitleBar13",24,16,1872,barH,"ui_topbar",UiKit.Panel);
            var frame=FrameInsets11(bar.GetComponent<UnityEngine.UI.Image>());
            string title="卡面模板 · "+CardGalleryTitles13[page]+" · "+(ThemeRegistry.Current?.DisplayName??"默认")+" · 文字 "+Mathf.RoundToInt(UiKit.FontScale*100)+"%";
            float titleH=Mathf.Ceil(22*UiKit.FontScale*1.46f)+2;
            FitLine13(bar,"GalleryTitle13",title,frame.x+16,(barH-titleH)*.5f,1872-frame.x-frame.z-32,22,LabelOn11(bar,UiKit.TextColor),TextAlignmentOptions.MidlineLeft,true);
            var legend=FitLine13(root,"GalleryLegend13",CardGalleryLegends13[page],40,16+barH+8,1840,18,LabelOn11(backdrop,UiKit.Muted),TextAlignmentOptions.MidlineLeft,false);
            float top=16+barH+8+legend.rectTransform.rect.height+8;
            switch(page)
            {
                case 0:GalleryShop13(root,top,new[]{0,1},false);break;
                case 1:GalleryShop13(root,top,new[]{2},true);break;
                case 2:GalleryTable13(root,top);break;
                case 3:GalleryCompactMini13(root,top);break;
                case 4:GalleryCompactTall13(root,top);break;
                case 5:GalleryIconReview13(root,backdrop,top);break;
                default:GalleryRowsAndTiles14(root,backdrop,top);break;
            }
            return root;
        }
        public void HideCardGallery13(){if(cardGallery13!=null){UiKit.Clear(cardGallery13);cardGallery13.gameObject.SetActive(false);}SetGalleryBackdrop11(false);}

        /// <summary>Cards per rarity (common, advanced, rare), longest short text first (the hardest fit).</summary>
        private string[][] GalleryCards13()
            =>new[]{"common","uncommon","rare"}.Select(r=>Config.controllers.Where(c=>c.rarity==r).OrderByDescending(c=>(c.shortText??"").Length).ThenBy(c=>c.id,StringComparer.Ordinal).Select(c=>c.id).ToArray()).ToArray();
        private static readonly string[] GalleryEnchants13={"mult2","roll1","base10","slotless","submit1"};
        private static readonly string[] GalleryValues13={"得分 ×1.5","当前 红","本手可用"},GalleryValuesWithEnchant13={"基础分 +12","加成 +2.5","基础分 +120"}; // v1.1.1 (W7a): the value badges as the table writes them now - the three kinds of value keep their word
        /// <summary>The eight badge combinations: none, enchantment, value, both, disabled, disabled with an enchantment,
        /// warning (首领关将失效), warning with an enchantment (将被祭坛摧毁). A disabled card's badge reads 本关失效.</summary>
        private CardFace13 GalleryVariant13(string id,int rarity,int variant,int ordinal)
        {
            var card=new CardFace13{contentId=id,ordinal=ordinal};
            string enchant=GalleryEnchants13[(rarity*3+variant)%GalleryEnchants13.Length];
            switch(variant)
            {
                case 1:card.enchantment=enchant;break;
                case 2:card.value=GalleryValues13[rarity];break;
                case 3:card.enchantment=enchant;card.value=GalleryValuesWithEnchant13[rarity];break;
                case 4:card.disabled=true;card.value="本关失效";card.valueWarning=true;break;
                case 5:card.enchantment=enchant;card.disabled=true;card.value="本关失效";card.valueWarning=true;break;
                case 6:card.value="首领关将失效";card.valueWarning=true;break;
                case 7:card.enchantment=enchant;card.value="将被祭坛摧毁";card.valueWarning=true;break;
            }
            return card;
        }
        private static readonly long[] GalleryPrices13={3,7,9,14,6,8,12,5};
        /// <summary>The price states of a shop card by column: buy, short of gold, cards full, sold, slots full.</summary>
        private void GalleryPrice13(CardFace13 card,int column)
        {
            card.price=GalleryPrices13[column%GalleryPrices13.Length];card.sold=column==3;
            card.availability=column==1?"还差 3":column==2?"卡牌已满 "+Config.maxControllerInstances+" 张":column==4?"卡牌栏位已满":column==6?"还差 12":null;
        }
        private static string GalleryCategory13(string id)=>id.StartsWith("I",StringComparison.Ordinal)?"consumable":id.StartsWith("O",StringComparison.Ordinal)?"service":id.StartsWith("U",StringComparison.Ordinal)?"die_upgrade":"controller";
        private string LongestGood13(IEnumerable<string> ids)=>ids.OrderByDescending(id=>ShortText11(id).Length).ThenBy(id=>id,StringComparer.Ordinal).First();
        /// <summary>One of each kind of good: the longest item, service and face upgrade, and a new die.</summary>
        private List<CardFace13> GalleryGoods13(bool priced)
        {
            var items=Config.consumables.Select(c=>c.id).ToArray();var services=Config.services.Select(s=>s.id).ToArray();var upgrades=Config.diceUpgrades.Select(u=>u.id).ToArray();
            var newDie=UiKit.Template("ui_icon_roll");
            var goods=new List<CardFace13>
            {
                new CardFace13{contentId=LongestGood13(items),category="consumable"},
                new CardFace13{contentId=LongestGood13(services),category="service"},
                new CardFace13{contentId=LongestGood13(upgrades),category="die_upgrade"},
                new CardFace13{contentId="new_die",category="new_die",label="骰子 A",text="六面和 21 · 红 3 蓝 3",art=newDie},
            };
            if(!priced)return goods;
            goods[0].price=3;goods[1].price=5;goods[2].price=4;goods[2].availability="需选择目标";goods[3].price=6;
            // The second of each kind shows the other price states.
            goods.Add(new CardFace13{contentId=items.First(id=>id!=goods[0].contentId),category="consumable",price=4,availability="道具栏已满"});
            goods.Add(new CardFace13{contentId=services.First(id=>id!=goods[1].contentId),category="service",price=8,availability="还差 5"});
            goods.Add(new CardFace13{contentId=upgrades.First(id=>id!=goods[2].contentId),category="die_upgrade",price=3,sold=true});
            goods.Add(new CardFace13{contentId="new_die",category="new_die",label="骰子 B",text="六面和 24 · 红 4 蓝 2 · 黄金 1",art=newDie,price=9,availability="还差 1"});
            // Shelf order: item, item, service, service, upgrade, upgrade, die, die.
            return new List<CardFace13>{goods[0],goods[4],goods[1],goods[5],goods[2],goods[6],goods[3],goods[7]};
        }

        /// <summary>V1.1 W14 (SHOP_LAYOUT 2, E1): the Shop-size card of the shop shelf and of this gallery, 220 wide. With the
        /// 60-unit price button it is 18 units taller at 100 % (330 → 348) and 4 at 150 % (360 → 364), the price button's own
        /// growth, so every card keeps the picture, name and complete short text it had in W13; measured by
        /// <see cref="MeasureShopCard14"/> over every card (PLAY_V14_E1).</summary>
        public float ShopCardHeight14=>Large11?364:348;
        public const float ShopCardWidth14=220;
        /// <summary>What the template made of one Shop-size card: the picture's size, the name's size, and where the short
        /// text's last glyph ends against the top of the price button (card units, downward).</summary>
        public struct ShopCardFit14{public float art,namePx,textBottom,priceTop;public bool complete;}
        /// <summary>W14 E1 measurement: one Shop-size card (220 wide, at the given height, price 3 with the given reason or
        /// 购买) on the gallery layer; the short text is complete when its last glyph ends above the price button.</summary>
        public ShopCardFit14 MeasureShopCard14(string id,string availability,float height)
        {
            if(cardGallery13==null)cardGallery13=UiKit.Rect(canvas.transform,"CardGallery13",0,0,1920,1080);
            cardGallery13.anchoredPosition=main.anchoredPosition;cardGallery13.gameObject.SetActive(true);cardGallery13.SetAsLastSibling();
            var card=new CardFace13{contentId=id,price=3,availability=availability};
            var t=DrawCard13(cardGallery13,"MeasureShopCard14",40,40,ShopCardWidth14,height,card,CardSize13.Shop,null,true).transform;
            var body=t.Find("ShortText11").GetComponent<TextMeshProUGUI>();body.ForceMeshUpdate();
            var price=(RectTransform)t.Find("PriceButton13");var art=t.Find("Icon_"+id) as RectTransform;
            float bottom=-body.rectTransform.anchoredPosition.y-body.textBounds.min.y,top=-price.anchoredPosition.y;
            return new ShopCardFit14{art=art!=null?art.rect.height:0,namePx=t.Find("Name").GetComponent<TextMeshProUGUI>().fontSize,textBottom=bottom,priceTop=top,complete=bottom<=top-1};
        }

        /// <summary>Shop size (<see cref="ShopCardHeight14"/>): 220 × 348 units; with enlarged text 220 × 364, the room a long
        /// short text needs beside a two-row price button (卡牌已满 10 张, 卡牌栏位已满) at 150 %.</summary>
        private void GalleryShop13(Transform root,float top,int[] rarities,bool goodsRow)
        {
            var ids=GalleryCards13();const float w=220,gap=14,rowGap=16;float h=ShopCardHeight14,x0=(1920-(8*w+7*gap))*.5f;
            int row=0;
            foreach(int r in rarities)
            {
                for(int v=0;v<8;v++)
                {
                    var card=GalleryVariant13(ids[r][v%ids[r].Length],r,v,0);GalleryPrice13(card,v);
                    DrawCard13(root,"GalleryShop13_"+r+"_"+v,x0+v*(w+gap),top+row*(h+rowGap),w,h,card,CardSize13.Shop,null,true);
                }
                row++;
            }
            if(!goodsRow)return;
            var goods=GalleryGoods13(true);
            for(int i=0;i<goods.Count;i++)DrawCard13(root,"GalleryGoods13_"+i,x0+i*(w+gap),top+row*(h+rowGap),w,h,goods[i],CardSize13.Shop,null,true);
        }
        /// <summary>Table size at its narrowest real width: seven cards in the play row ((1448 − 6 × 12) / 7 units). With
        /// enlarged text the play row draws compact cards instead, so the page then uses the full card's width (216).</summary>
        private void GalleryTable13(Transform root,float top)
        {
            var ids=GalleryCards13();float w=Large11?216:(L11.cards.width-6*12)/7f,h=L11.cards.height,gap=12;float x0=(1920-(8*w+7*gap))*.5f;
            for(int r=0;r<3;r++)for(int v=0;v<8;v++)
                DrawCard13(root,"GalleryTable13_"+r+"_"+v,x0+v*(w+gap),top+r*(h+gap),w,h,GalleryVariant13(ids[r][v%ids[r].Length],r,v,v+1),CardSize13.Table,null,true);
        }
        /// <summary>Compact size as the play row's two-row (mini) cards: (1448 − 4 × 12) / 5 by (244 − 10) / 2 units.</summary>
        private void GalleryCompactMini13(Transform root,float top)
        {
            var ids=GalleryCards13();float w=(L11.cards.width-4*12)/5f,h=(L11.cards.height-10)/2f,gapX=24,gapY=12;float x0=(1920-(4*w+3*gapX))*.5f;
            for(int r=0;r<3;r++)for(int v=0;v<8;v++)
                DrawCard13(root,"GalleryCompact13_"+r+"_"+v,x0+(v%4)*(w+gapX),top+(r*2+v/4)*(h+gapY),w,h,GalleryVariant13(ids[r][(8+v)%ids[r].Length],r,v,r*8+v+1),CardSize13.Compact,null,true);
            var goods=GalleryGoods13(false);
            for(int i=0;i<goods.Count;i++)DrawCard13(root,"GalleryCompactGoods13_"+i,x0+i*(w+gapX),top+6*(h+gapY),w,h,goods[i],CardSize13.Compact,null,true);
        }
        /// <summary>Compact size as the play row's text cards (enlarged text, five cards or fewer): 280 × 244 units.</summary>
        private void GalleryCompactTall13(Transform root,float top)
        {
            var ids=GalleryCards13();float w=280,h=L11.cards.height,gapX=24,gapY=12;float x0=(1920-(6*w+5*gapX))*.5f;
            for(int r=0;r<3;r++)for(int v=0;v<6;v++)
                DrawCard13(root,"GalleryCompactTall13_"+r+"_"+v,x0+v*(w+gapX),top+r*(h+gapY),w,h,GalleryVariant13(ids[r][(16+v)%ids[r].Length],r,v,v+1),CardSize13.Compact,null,true);
        }
        /// <summary>
        /// W14 (CARD_TEMPLATE E2, E3): goods rows with their price button at the shop's row sizes (升级与服务: 452 wide, three
        /// rows at 100 %, two with enlarged text; 骰子与骰面: 448 wide) in every price state, and the tiles of 我的构筑 (100 wide)
        /// in the three rarities with ordinal and enchantment.
        /// </summary>
        private void GalleryRowsAndTiles14(Transform root,RectTransform backdrop,float top)
        {
            var l=S14;var items=Config.consumables.Select(c=>c.id).ToArray();var services=Config.services.Select(s=>s.id).ToArray();
            GoodsGrid14(l,9,out _,out _,out float rowH,out float colW);if(Large11)GoodsGrid14(l,6,out _,out _,out rowH,out colW);
            float diceH=DiceRowHeight14(l),x0=(1920-(2*colW+448+2*24))*.5f;
            var level=ViewState.handLevels.First(h=>h.handId=="pair").level;var pair=Config.Hand("pair");
            var rows=new List<CardFace13>
            {
                new CardFace13{contentId=LongestGood13(items),category="consumable",price=3},
                new CardFace13{contentId=items.First(id=>id!=LongestGood13(items)),category="consumable",price=4,availability="道具栏已满"},
                new CardFace13{contentId=LongestGood13(services),category="service",price=5,availability="还差 3"},
                new CardFace13{contentId="O04",category="service",price=1,availability="至少留 1 颗骰子"},
                new CardFace13{contentId="pair",category="hand_upgrade",price=3,text="Lv."+level+" → Lv."+(level+1)+" · ×"+Factor(pair.baseMult100+pair.incrementPerLevel100*level)+" → ×"+Factor(pair.baseMult100+pair.incrementPerLevel100*(level+1)),picture=(t,r)=>HandPattern14(t,"pair",false,r)},
                new CardFace13{contentId=services.First(id=>id!=LongestGood13(services)&&id!="O04"),category="service",price=6,sold=true,disabled=true},
            };
            for(int i=0;i<rows.Count;i++){rows[i].priceWidth=GoodsPriceW14;DrawCard13(root,"GalleryGoodsRow14_"+i,x0+(i%2)*(colW+24),top+(i/2)*(rowH+8),colW,rowH,rows[i],CardSize13.Compact,null,true);}
            var dieArt=UiKit.Template("ui_icon_roll");var sample=DiceFactory.CreateStandard(Config,1);
            var dice=new List<CardFace13>
            {
                new CardFace13{contentId="new_die",category="new_die",label=DieTitle14(sample,Config.generation).title,text="",art=dieArt,price=4},
                new CardFace13{contentId="new_die",category="new_die",label="红蓝各半 · 小点 · 三个 1",text="",art=dieArt,price=5,availability="骰子已满 "+Config.maxPermanentDice+" 颗"},
                new CardFace13{contentId=LongestGood13(Config.diceUpgrades.Select(u=>u.id)),category="die_upgrade",price=2},
                new CardFace13{contentId="U05",category="die_upgrade",price=2,availability="没有可改的骰子"},
            };
            float dx=x0+2*(colW+24);
            for(int i=0;i<dice.Count;i++){dice[i].priceWidth=DicePriceWidth14;DrawCard13(root,"GalleryGoodsDie14_"+i,dx,top+i*(diceH+8),448,diceH,dice[i],CardSize13.Compact,null,true);}
            float tilesTop=top+Mathf.Max(3*(rowH+8),4*(diceH+8))+8,tileH=l.tileH;
            var ids=GalleryCards13();var tiles=new List<KeyValuePair<string,CardFace13>>();
            for(int r=0;r<3;r++)foreach(int v in new[]{0,1,3})
                tiles.Add(new KeyValuePair<string,CardFace13>("GalleryTile14_"+r+"_"+v,GalleryVariant13(ids[r][v%ids[r].Length],r,v,tiles.Count+1)));
            // As in 我的构筑: the names of the row share one size.
            TileRow13(tiles.Count,(i,px)=>{tiles[i].Value.namePx=px;return DrawCard13(root,tiles[i].Key,40+i*(BuildTileWidth14+8),tilesTop,BuildTileWidth14,tileH,tiles[i].Value,CardSize13.Tile,null,true);});
        }
        /// <summary>The W13a review: each icon in a two-row card of the play row, its id above it; no badges or ordinal cover it.</summary>
        private void GalleryIconReview13(Transform root,RectTransform backdrop,float top)
        {
            float w=(L11.cards.width-4*12)/5f,h=(L11.cards.height-10)/2f,gapX=24,pairGap=56;
            Color caption=LabelOn11(backdrop,UiKit.Muted),heading=LabelOn11(backdrop,UiKit.TextColor);
            float captionH=Mathf.Ceil(18*UiKit.FontScale*1.46f)+2;
            float y=top;
            void Card(string id,float x,float at,int n)
            {
                FitLine13(root,"IconId13_"+n,id,x,at,w,18,caption,TextAlignmentOptions.MidlineLeft,true);
                DrawCard13(root,"GalleryIcon13_"+n,x,at+captionH,w,h,new CardFace13{contentId=id,category=GalleryCategory13(id)},CardSize13.Compact,null,true);
            }
            var a=FitLine13(root,"IconSectionA13","(a) 仍待修图：C33 C34 C107 C110 C111 · 临界：C88 C103 C113 C126 C127 C130 C131",40,y,1840,20,heading,TextAlignmentOptions.MidlineLeft,true);
            y+=a.rectTransform.rect.height+4;
            float x0=(1920-(6*w+5*gapX))*.5f;int n=0;
            for(int i=0;i<IconReviewFlagged13.Length;i++,n++)
            {
                if(i>0&&i%6==0)y+=captionH+h+12;
                Card(IconReviewFlagged13[i],x0+(i%6)*(w+gapX),y,n);
            }
            y+=captionH+h+24;
            var b=FitLine13(root,"IconSectionB13","(b) 最相近的图标对：C40/C87 · I11/U08 · U08/U10 · C08/C22 · C08/C79",40,y,1840,20,heading,TextAlignmentOptions.MidlineLeft,true);
            y+=b.rectTransform.rect.height+4;
            float pairW=2*w+8;float px0=(1920-(3*pairW+2*pairGap))*.5f;
            for(int p=0;p<IconReviewPairs13.Length;p++)
            {
                if(p>0&&p%3==0)y+=captionH+h+12;
                float x=px0+(p%3)*(pairW+pairGap);
                Card(IconReviewPairs13[p][0],x,y,n++);Card(IconReviewPairs13[p][1],x+w+8,y,n++);
            }
        }
    }
}
