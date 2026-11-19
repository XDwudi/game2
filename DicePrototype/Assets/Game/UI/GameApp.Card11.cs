using System;
using System.Collections.Generic;
using System.Linq;
using DiceCraft.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceCraft
{
    /// <summary>The baked compact card icons of one theme (task W13, CARD_TEMPLATE §7), bound by Editor/V13CardSetup from
    /// Assets/Art/Art11/Icons/&lt;theme&gt;/icon_&lt;ID&gt;.png.</summary>
    [Serializable] public sealed class CompactIconSet13 { public string theme; public Sprite[] icons=new Sprite[0]; }

    // V1.1 task W13 (docs/specs/v11/CARD_TEMPLATE.md): one drawing entry for every card in three sizes. Shop (shop shelf,
    // loot page) and Table (the play row's full cards) stack gem, name, illustration with its badges and the complete short
    // text; Shop adds the price button. Compact (the play row's text and mini cards, lists) puts a framed icon block beside
    // the name. Rarity is the colour of the frame and a gem whose shape also differs (round, diamond, six-point star); goods
    // without a rarity get a Ghost frame and their category instead. Every label keeps to the drawn frames (FrameInsets11)
    // and takes its colour from the drawn fill (LabelOn11).
    public sealed partial class GameApp
    {
        public CompactIconSet13[] compactIcons13=new CompactIconSet13[0];
        private static readonly Dictionary<string,Sprite> compactIconCache13=new Dictionary<string,Sprite>(StringComparer.Ordinal);
        /// <summary>The drawn band of each rarity ring sprite, in sprite pixels: measured once (a GPU read-back), not per card.</summary>
        private static readonly Dictionary<Sprite,Vector4> ringInset13=new Dictionary<Sprite,Vector4>();
        /// <summary>(theme, id) pairs resolved since the last theme switch.</summary>
        public static int CompactIconCacheCount13=>compactIconCache13.Count;
        /// <summary>Emptied on every theme switch (ApplyTheme11), like the W11c surface caches: the icons and ring sprites
        /// belong to the theme that drew them.</summary>
        private static void ClearCardCaches13(){compactIconCache13.Clear();ringInset13.Clear();}

        /// <summary>Shop, Table and Compact (W13); Tile (W14 E3): the icon block and the name, for the shop's 我的构筑 row.</summary>
        public enum CardSize13{Shop,Table,Compact,Tile}
        /// <summary>What one card shows. Goods without a rarity (items, services, face upgrades, new dice) set their
        /// category; a new die names itself, brings its own picture and its own short text (its faces).
        /// W14: a priced Compact card is a goods row (E2) with its price button on the right, <see cref="priceWidth"/> wide;
        /// <see cref="categoryText"/> adds to the category line; <see cref="picture"/> draws the block's content instead of
        /// the icon (a real die, a hand pattern); <see cref="buttonName"/> names the price button (the shop's "Buy_" ids).</summary>
        private sealed class CardFace13
        {
            public string contentId,category="controller",enchantment="none",value,label,text;
            public bool valueWarning,disabled,sold;
            public int ordinal;
            public long price=-1;
            public string availability;
            public Action buy;
            public Sprite art;
            public string buttonName,categoryText;
            public float priceWidth;
            public Action<Transform,Rect> picture;
            // W14 E3: the drawn size of a tile's name when a row of tiles shares one (0: the tile's own fit).
            public float namePx;
        }

        /// <summary>The compact icon of a content id (CARD_TEMPLATE §7). V1.1 (W22i): a style that has its own art never
        /// shows another style's picture: the current style's baked icon, then its own whole illustration, and only when it
        /// has neither the default style's icon, then the default illustration. Cached per (theme, id); a theme switch
        /// empties the cache.</summary>
        private Sprite CompactIcon13(string id)
        {
            string theme=ThemeId11,key=theme+"|"+id,name="icon_"+id;
            if(compactIconCache13.TryGetValue(key,out var cached))return cached;
            Sprite IconOf(string candidate)
            {
                var set=compactIcons13?.FirstOrDefault(s=>s!=null&&string.Equals(s.theme,candidate,StringComparison.OrdinalIgnoreCase));
                return set?.icons?.FirstOrDefault(s=>s!=null&&string.Equals(s.name,name,StringComparison.OrdinalIgnoreCase));
            }
            Sprite found=null;
            if(!string.Equals(theme,GameTheme.DefaultId,StringComparison.OrdinalIgnoreCase))
            {
                found=IconOf(theme);
                if(found==null&&!string.IsNullOrEmpty(id)&&ThemeRegistry.Current!=null)found=ThemeRegistry.Current.ContentArt("illustration_"+id);
            }
            if(found==null)found=IconOf(GameTheme.DefaultId);
            // ContentSprite071 asks the current style first; here the style has no art for the id, so it is the default's.
            if(found==null)found=ContentSprite071(id);
            compactIconCache13[key]=found;return found;
        }

        /// <summary>common / advanced / rare for a card, null for goods without a rarity.</summary>
        private string RarityKey13(string id)
        {
            if(string.IsNullOrEmpty(id)||!id.StartsWith("C",StringComparison.Ordinal))return null;
            string rarity=Config.Controller(id).rarity;return rarity=="rare"?"rare":rarity=="uncommon"?"advanced":"common";
        }
        private static Color RarityTint13(string key)=>key=="rare"?UiKit.RarityRare:key=="advanced"?UiKit.RarityAdvanced:key=="common"?UiKit.RarityCommon:UiKit.Ghost;
        private static string GoodsCategory13(string category)
            =>category=="consumable"?"道具":category=="service"?"服务":category=="die_upgrade"?"骰面改造":category=="new_die"?"新骰子":
              category=="hand_upgrade"||category=="color_upgrade"?"牌型升级":"";
        /// <summary>A disabled card is drawn dimmed: face, frame and picture darken; its labels take the dimmed fill's colour.</summary>
        private static readonly Color DimFace13=new Color(.62f,.62f,.62f,1);
        private static Color Dim13(Color c)=>new Color(c.r*.62f,c.g*.62f,c.b*.62f,c.a);

        /// <summary>
        /// The value badge of a table card (CARD_TEMPLATE §4) from its state line (CardState07): the number itself
        /// ("当前 ×1.5" → "×1.5", "当前 +12" → "+12", "当前红" → "当前 红") or a state that changes play (本关失效, 首领关将失效,
        /// 将被祭坛摧毁, 右邻失效 as warnings; the copied card of 模仿者, 本手可用/已用, 售价); no badge for "点击查看条件" or a fixed
        /// trait ("随机倍率").
        /// </summary>
        private static void CardValue13(string state,out string value,out bool warning)
        {
            value=null;warning=false;
            if(string.IsNullOrEmpty(state)||state=="点击查看条件"||state=="技能详情"||state=="随机倍率")return;
            warning=state=="本关失效"||state=="首领关将失效"||state=="将被祭坛摧毁"||state=="右邻失效";
            if(state.StartsWith("当前 ×",StringComparison.Ordinal)||state.StartsWith("当前 +",StringComparison.Ordinal))value=state.Substring(3);
            else if(state=="当前红"||state=="当前蓝")value="当前 "+state.Substring(2);
            else value=state.Replace("→ ","→");
        }

        /// <summary>How far an open ring's drawn band reaches in from each edge (left, bottom, right, top), in rect units: the
        /// last opaque pixel inside the slice border along the middle row and column.</summary>
        private static Vector4 RingInsets13(Image ring)
        {
            if(ring==null||ring.sprite==null||ring.type!=Image.Type.Sliced)return Vector4.zero;
            if(!ringInset13.TryGetValue(ring.sprite,out var px))
            {
                px=Vector4.zero;var p=SpritePixels11(ring.sprite,out int w,out int h);
                if(p!=null)
                {
                    var border=ring.sprite.border;
                    int Inset(Func<int,Color32> at,int count,float limit)
                    {
                        int n=Mathf.Min(count/2,Mathf.RoundToInt(limit)),inset=0;
                        for(int i=0;i<n;i++)if(at(i).a>=128)inset=i+1;
                        return inset;
                    }
                    px=new Vector4(Inset(i=>p[(h/2)*w+i],w,border.x),Inset(i=>p[i*w+w/2],h,border.y),Inset(i=>p[(h/2)*w+w-1-i],w,border.z),Inset(i=>p[(h-1-i)*w+w/2],h,border.w));
                }
                ringInset13[ring.sprite]=px;
            }
            return px/Mathf.Max(.0001f,ring.pixelsPerUnit*ring.pixelsPerUnitMultiplier);
        }

        private static Image TintedSprite13(Transform parent,string name,string template,float x,float y,float w,float h,Color tint,bool sliced)
        {
            var image=UiKit.Box(parent,name,x,y,w,h,tint).GetComponent<Image>();
            image.sprite=UiKit.Template(template);image.raycastTarget=false;
            image.type=sliced&&image.sprite!=null?Image.Type.Sliced:Image.Type.Simple;image.fillCenter=!sliced;image.preserveAspect=!sliced;
            image.pixelsPerUnitMultiplier=1;
            return image;
        }
        /// <summary>The rarity ring over a rectangle: returns the inside of its band.</summary>
        private static Rect RarityRing13(Transform parent,string name,Rect area,Color tint)
        {
            var ring=TintedSprite13(parent,name,"ui_card_frame_rarity",area.x,area.y,area.width,area.height,tint,true);
            if(ring.sprite==null){ring.color=Color.clear;UiKit.Outline(ring.transform,"Edge",0,0,area.width,area.height,tint,3);return new Rect(area.x+3,area.y+3,area.width-6,area.height-6);}
            var band=RingInsets13(ring);
            return new Rect(area.x+band.x,area.y+band.w,Mathf.Max(1,area.width-band.x-band.z),Mathf.Max(1,area.height-band.w-band.y));
        }
        private void RarityGem13(Transform parent,string rarity,Color tint,float centreX,float centreY,float size)
        {
            if(rarity==null)return;
            var gem=TintedSprite13(parent,"RarityGem13","ui_gem_"+rarity,centreX-size*.5f,centreY-size*.5f,size,size,tint,false);
            if(gem.sprite==null){gem.sprite=UiKit.Template("ui_pip_dot");gem.color=tint;}
        }
        /// <summary>A one-line label that shrinks (down to the 19.5-unit body size) until its drawn glyphs fit its width with a
        /// unit to spare, measured on TMP's own mesh (a bold label paints past the font's advances). <paramref name="maxPx"/>
        /// caps the starting size when the caller needs the line smaller; the label is as tall as its line (Line11).</summary>
        private static TextMeshProUGUI FitLine13(Transform parent,string name,string text,float x,float y,float w,float size,Color color,TextAlignmentOptions align,bool bold,float maxPx=0)
        {
            float px=size*UiKit.FontScale;if(maxPx>0)px=Mathf.Min(px,Mathf.Max(19.5f,maxPx));
            float min=Mathf.Min(px,19.5f);
            var label=NoWrap11(UiKit.Text(parent,name,text,x,y,w,Mathf.Ceil(px*1.46f)+2,px/UiKit.FontScale,color,align));
            if(bold)label.fontStyle=FontStyles.Bold;
            for(;;)
            {
                label.fontSize=px;label.ForceMeshUpdate();
                if(px<=min||label.textBounds.size.x<=w-1)break;
                px=Mathf.Max(min,px-1);
            }
            label.rectTransform.sizeDelta=new Vector2(w,Mathf.Ceil(px*1.46f)+2);
            return label;
        }
        /// <summary>True when a label's drawn glyphs fit its own width.</summary>
        private static bool FitsLine13(TextMeshProUGUI label)=>label.textBounds.size.x<=label.rectTransform.rect.width-1;
        /// <summary>The picture of a card or good: the illustration (Table, Shop) or the compact icon. A disabled card's
        /// picture is dimmed with it.</summary>
        private void CardArt13(Transform parent,CardFace13 card,float x,float y,float size,bool compact)
        {
            if(card.art==null&&!compact){Icon(parent,card.contentId,x,y,size);}
            else
            {
                var r=UiKit.Rect(parent,"Icon_"+card.contentId,x,y,size,size);
                var sprite=card.art??CompactIcon13(card.contentId);
                if(sprite!=null){var image=UiKit.Component<Image>(r.gameObject);image.sprite=sprite;image.color=Color.white;image.raycastTarget=false;image.preserveAspect=true;}
                else{UiKit.Outline(r,"Missing",2,2,size-4,size-4,UiKit.Muted);UiKit.Text(r,"Id",MissingIconLabel11(card.contentId),2,2,size-4,size-4,Math.Min(22,size/4),UiKit.Muted,TextAlignmentOptions.Center);}
            }
            if(card.disabled){var image=parent.Find("Icon_"+card.contentId)?.GetComponent<Image>();if(image!=null)image.color=new Color(.55f,.55f,.55f,1);}
        }
        /// <summary>
        /// The value badge (CARD_TEMPLATE §4): a gold chip, a Danger chip for a warning, whose bold label is the card's
        /// "State" (post-score effects rewrite it in place). The label starts at <paramref name="size"/> (times the text
        /// scale) and steps down to fit between minX and maxX, never below the 19.5-unit body floor (AX-03: 13 px at 720p;
        /// the W11 state line it replaces kept that floor). Its left edge is at preferredX when it fits, otherwise as far
        /// left as minX. Returns the chip's rect (empty without a value).
        /// </summary>
        private Rect ValueBadge13(Transform parent,CardFace13 card,float preferredX,float y,float minX,float maxX,float size)
        {
            if(string.IsNullOrEmpty(card.value))return Rect.zero;
            var chip=Surface11(parent,"ValueBadge13",preferredX,y,40,30,"ui_chip_label",card.valueWarning?UiKit.Danger:UiKit.ValueBadge);
            var image=chip.GetComponent<Image>();var frame=FrameInsets11(image);
            var text=NoWrap11(UiKit.Text(parent,"State",card.value,preferredX,y,40,30,size,Color.white,TextAlignmentOptions.Center));
            text.fontStyle=FontStyles.Bold;
            float floor=19.5f/UiKit.FontScale,at=Mathf.Max(size,floor),pad=frame.x+frame.z+12;
            float Width(float s){text.fontSize=s*UiKit.FontScale;return Mathf.Ceil(text.GetPreferredValues(card.value,0,0).x)+2;}
            while(at>floor&&Width(at)+pad>maxX-minX)at=Mathf.Max(floor,at-.5f);
            float w=Width(at)+pad,h=Mathf.Ceil(at*UiKit.FontScale*1.25f)+10;
            float x=Mathf.Max(minX,Mathf.Min(preferredX,maxX-w));
            chip.anchoredPosition=new Vector2(x,-y);chip.sizeDelta=new Vector2(w,h);
            var box=ChipText11(chip);
            text.rectTransform.anchoredPosition=new Vector2(x+box.x,-(y+box.y));text.rectTransform.sizeDelta=new Vector2(box.width,box.height);
            text.color=LabelOn11(image);
            return new Rect(x,y,w,h);
        }
        /// <summary>The enchantment badge (badge_enchant_*), dimmed with a disabled card; a pooled badge is reset otherwise.</summary>
        private void CardEnchant13(Transform card,CardFace13 face,float x,float y,float size)
        {
            EnchantBadge(card,face.enchantment,x,y,size);
            var image=card.Find("EnchantFrame/Badge")?.GetComponent<Image>();if(image!=null)image.color=face.disabled?DimFace13:Color.white;
        }
        private static bool Enchanted13(CardFace13 card)=>!string.IsNullOrEmpty(card.enchantment)&&card.enchantment!="none";
        private void OrdinalDisc13(Transform parent,int ordinal,float x,float y,float size)
        {
            if(ordinal<=0)return;
            var disc=UiKit.Box(parent,"OrdinalDisc11",x,y,size,size,UiKit.Ink).GetComponent<Image>();disc.sprite=UiKit.Template("ui_pip_dot");disc.raycastTarget=false;
            NoWrap11(UiKit.Text(parent,"Ordinal",ordinal.ToString(),x,y,size,size,Mathf.Min(14,size*.6f/Mathf.Max(1,UiKit.FontScale)),LabelOn11(disc),TextAlignmentOptions.Center));
        }

        /// <summary>
        /// Draws one card (CARD_TEMPLATE §2–5) as a button named <paramref name="name"/> and returns it; the caller adds drag
        /// and hover. Shop and Table: gem across the top of the rarity frame, name, picture with the enchantment (top right)
        /// and value (top left) badges, the complete short text; Table adds the ordinal disc, Shop the price button.
        /// Compact: a framed icon block (value badge on its top-left corner, gem across its foot) with the ordinal disc and
        /// the name beside it, the enchantment badge in the card's top-right corner and, when it fits, the short text.
        /// </summary>
        private UnityEngine.UI.Button DrawCard13(Transform parent,string name,float x,float y,float w,float h,CardFace13 card,CardSize13 size,Action click,bool interactable)
        {
            bool tile=size==CardSize13.Tile,compact=size==CardSize13.Compact||tile,goods=card.category!="controller";
            var b=UiKit.Button(parent,name,"",x,y,w,h,click,interactable);
            UiKit.CardSurface(b,compact&&h<200);
            var face=(Image)b.targetGraphic;
            if(size==CardSize13.Shop){var shop=UiKit.Template("ui_card_shop");if(shop!=null){face.sprite=shop;face.type=Image.Type.Sliced;}}
            // W14 E3: a tile is about 100 units square; the compact face's frame (11–14 units in the default style) is drawn
            // at half its thickness there, so a four-character name at the 19.5-unit floor fits inside it.
            face.pixelsPerUnitMultiplier=tile?2:1;
            face.color=card.disabled?DimFace13:Color.white;
            var t=b.transform;
            string rarity=goods?null:RarityKey13(card.contentId);
            Color tint=RarityTint13(rarity);if(card.disabled)tint=Dim13(tint);
            var edge=FrameInsets11(face);
            var inner=new Rect(edge.x,edge.w,Mathf.Max(1,w-edge.x-edge.z),Mathf.Max(1,h-edge.w-edge.y));
            string title=goods&&!string.IsNullOrEmpty(card.label)?card.label:NameOf(card.contentId);
            string text=card.text??ShortText11(card.contentId);
            if(tile)DrawTile13(t,card,inner,rarity,tint,title);
            else if(compact)DrawCompactCard13(t,card,inner,w,h,rarity,tint,title,text,goods);
            else DrawTallCard13(t,card,inner,w,h,size,rarity,tint,title,text,goods);
            return b;
        }

        private void DrawTallCard13(Transform t,CardFace13 card,Rect inner,float w,float h,CardSize13 size,string rarity,Color tint,string title,string text,bool goods)
        {
            bool shop=size==CardSize13.Shop;var face=(Image)t.GetComponent<UnityEngine.UI.Button>().targetGraphic;
            var content=RarityRing13(t,"RarityFrame13",inner,tint);
            content=new Rect(content.x+4,content.y+3,content.width-8,content.height-6);
            // The gem sits across the middle of the frame's top band (CARD_TEMPLATE §2: across the top edge).
            float gem=shop?34:30,gemY=inner.y+(content.y-3-inner.y)*.5f;
            if(!goods)RarityGem13(t,rarity,tint,w*.5f,gemY,gem);
            float top=goods?content.y:Mathf.Max(content.y,gemY+gem*.5f+2);
            // Table: the ordinal disc in the top-left corner (CARD_TEMPLATE §2).
            float disc=size==CardSize13.Table&&card.ordinal>0?Mathf.Ceil(Mathf.Max(22,14*UiKit.FontScale*1.5f)):0;
            if(disc>0)OrdinalDisc13(t,card.ordinal,content.x,content.y,disc);
            float badge=h>200?32:28;bool enchanted=Enchanted13(card);
            // Price button (Shop) at the foot: reserved first.
            float priceH=shop&&card.price>=0?PriceButtonHeight13(card,content.width):0;
            float bottom=content.yMax-(priceH>0?priceH+6:0);
            // The badges sit side by side on the picture: value top left, enchantment top right (CARD_TEMPLATE §2, §4). When
            // the value badge leaves no room there (a small picture beside a long text, enlarged text), the enchantment badge
            // takes the card's top-right corner, its W11 place, and the second pass keeps the name clear of it.
            bool corner=false;
            for(int pass=0;pass<2;pass++)
            {
                // Name row: clear of the ordinal disc (left) and of a corner badge (right); centred on the card when only
                // one of them, or neither, is there.
                float insetL=disc>0?disc+4:corner?badge+4:0,insetR=corner?badge+4:insetL,y=top;
                // Goods name their category above the name (CARD_TEMPLATE §3).
                if(goods)
                {
                    var category=FitLine13(t,"Category13",GoodsCategory13(card.category),content.x+insetL,y,content.width-insetL-insetR,16,LabelOn11(face,PaperMuted071),TextAlignmentOptions.Center,false);
                    y+=category.rectTransform.rect.height;
                }
                // The short text is complete (TX-03): the picture gives way first (down to 56, a shop's to 72), then the text
                // steps toward the 19.5-unit body size; with enlarged text the name then steps toward the body size too, and
                // only then does the picture go down to its last 40 units.
                var body=UiKit.Text(t,"ShortText11",text,content.x,0,content.width,10,20,LabelOn11(face,UiKit.Ink),TextAlignmentOptions.Top);
                body.lineSpacing=-14;body.enableAutoSizing=false;body.textWrappingMode=TextWrappingModes.Normal;
                float minArt=shop?72:56,floorArt=Mathf.Min(40,content.width-8),namePx=0,textH=0,art=0,artTop=0;
                for(int shrink=0;shrink<20;shrink++)
                {
                    var nameLabel=FitLine13(t,"Name",title,content.x+insetL,y,content.width-insetL-insetR,22,LabelOn11(face,UiKit.Ink),TextAlignmentOptions.Center,true,namePx);
                    artTop=y+nameLabel.rectTransform.rect.height+2;
                    float font=20*UiKit.FontScale;
                    for(int step=0;step<60;step++)
                    {
                        body.fontSize=font;textH=Mathf.Ceil(body.GetPreferredValues(text,content.width,0).y)+2;
                        art=Mathf.Min(content.width-8,bottom-artTop-4-textH);
                        if(art>=minArt||font<=19.5f)break;
                        font=Mathf.Max(19.5f,font-1);
                    }
                    if(art>=floorArt||nameLabel.fontSize<=19.51f)break;
                    namePx=Mathf.Max(19.5f,nameLabel.fontSize-1.5f);
                }
                art=Mathf.Max(floorArt,art);
                float artX=(w-art)*.5f;
                CardArt13(t,card,artX,artTop,art,false);
                var value=ValueBadge13(t,card,artX+2,artTop+2,content.x,content.xMax,18);
                body.rectTransform.anchoredPosition=new Vector2(content.x,-(artTop+art+4));body.rectTransform.sizeDelta=new Vector2(content.width,Mathf.Max(textH,bottom-(artTop+art+4)));
                if(enchanted)
                {
                    float ex=artX+art-badge-2,ey=artTop+2;
                    if(!corner&&value.width>0&&value.xMax+4>ex){corner=true;continue;}
                    if(corner){ex=content.xMax-badge;ey=content.y;}
                    CardEnchant13(t,card,ex,ey,badge);
                }
                break;
            }
            if(priceH>0)PriceButton13(t,card,content.x,content.yMax-priceH,content.width,priceH);
        }

        private void DrawCompactCard13(Transform t,CardFace13 card,Rect inner,float w,float h,string rarity,Color tint,string title,string text,bool goods)
        {
            var face=(Image)t.GetComponent<UnityEngine.UI.Button>().targetGraphic;
            bool tall=h>=200;var content=new Rect(inner.x+4,inner.y+4,inner.width-8,inner.height-8);
            // W14 E2: a priced compact card is a goods row; its price button takes the right end of the content, so the name
            // and the short text end 8 units before it.
            bool priced=card.price>=0;float priceW=priced?Mathf.Max(card.priceWidth,100):0,priceH=priced?PriceButtonHeight13(card,priceW):0;
            float limit=content.xMax-(priced?priceW+8:0);
            // The icon block: a framed square (CARD_TEMPLATE §2) with the rarity gem across its foot, so the shape cue of the
            // rarity stays on a compact card too while the value badge takes the block's top-left corner.
            float block=tall?Mathf.Min(84,content.width*.34f):Mathf.Min(content.height-8,76),gem=tall?22:18;
            var blockArea=new Rect(content.x,content.y+(tall?6:Mathf.Max(0,(content.height-block-gem*.5f)*.5f)),block,block);
            var picture=RarityRing13(t,"RarityFrame13",blockArea,tint);
            var pictureRect=new Rect(picture.x+1,picture.y+1,Mathf.Min(picture.width,picture.height)-2,Mathf.Min(picture.width,picture.height)-2);
            if(card.picture!=null)card.picture(t,pictureRect);else CardArt13(t,card,pictureRect.x,pictureRect.y,pictureRect.width,true);
            if(!goods)RarityGem13(t,rarity,tint,blockArea.center.x,blockArea.yMax,gem);
            // The enchantment badge keeps the card's top-right corner (the W11 compact place; §2 names no place for it in the
            // compact size); the value badge starts at the block's top-left and may reach right up to it.
            float badge=tall?32:28;bool enchanted=Enchanted13(card);
            if(enchanted)CardEnchant13(t,card,content.xMax-badge,content.y,badge);
            var value=ValueBadge13(t,card,blockArea.x,blockArea.y,content.x,limit-(enchanted&&!priced?badge+4:0),16);
            // Name beside the block, below the value badge where the badge reaches over; the ordinal disc leads it, clear of
            // the icon. Goods name their category above it. A goods row caps the lines at 19.5 (category) and 22 units
            // (name) so a short text can still follow them at 150 % text.
            float tx=blockArea.xMax+10,ty=content.y+(tall?4:2);
            if(value.width>0&&value.xMax>tx-4)ty=Mathf.Max(ty,value.yMax+2);
            float right=limit-(enchanted&&ty<content.y+badge+2?badge+4:0),tw=Mathf.Max(20,right-tx);
            if(goods)
            {
                string category=GoodsCategory13(card.category)+(string.IsNullOrEmpty(card.categoryText)?"":" · "+card.categoryText);
                var label=FitLine13(t,"Category13",category,tx,ty,tw,15,LabelOn11(face,PaperMuted071),TextAlignmentOptions.MidlineLeft,false,priced?19.5f:0);
                ty+=label.rectTransform.rect.height;
            }
            float disc=card.ordinal>0?Mathf.Ceil(Mathf.Max(20,14*UiKit.FontScale*1.4f)):0,nameX=tx+(disc>0?disc+6:0);
            var nameLabel=FitLine13(t,"Name",title,nameX,ty,Mathf.Max(20,right-nameX),20,LabelOn11(face,UiKit.Ink),TextAlignmentOptions.MidlineLeft,true,priced?22:0);
            if(!FitsLine13(nameLabel)&&right<limit)
            {
                // Too long beside the enchantment badge even at the body size: the name row moves under the badge.
                ty=Mathf.Max(ty,content.y+badge+2);right=limit;
                nameLabel=FitLine13(t,"Name",title,nameX,ty,Mathf.Max(20,right-nameX),20,LabelOn11(face,UiKit.Ink),TextAlignmentOptions.MidlineLeft,true,priced?22:0);
            }
            if(priced&&!FitsLine13(nameLabel))
            {
                // A goods row's long name (a new die's three-word title at 150 %) wraps to two balanced lines at the body size.
                float width=Mathf.Max(20,right-nameX);
                nameLabel.textWrappingMode=TextWrappingModes.Normal;nameLabel.fontSize=19.5f;
                nameLabel.text=BalancedLines11(nameLabel,title,width);nameLabel.ForceMeshUpdate();
                nameLabel.rectTransform.sizeDelta=new Vector2(width,Mathf.Ceil(nameLabel.GetPreferredValues(nameLabel.text,width,0).y)+2);
            }
            float nameH=nameLabel.rectTransform.rect.height;
            if(disc>0)OrdinalDisc13(t,card.ordinal,tx,ty+(nameH-disc)*.5f,disc);
            float textTop=ty+nameH;
            if(tall)
            {
                // A tall compact card has the whole width under the block (and its gem) for the short text, auto-sized to
                // the 19.5 floor.
                float below=Mathf.Max(textTop,blockArea.yMax+gem*.5f+4),room=Mathf.Max(1,content.yMax-below);
                var body=UiKit.Text(t,"ShortText11",text,content.x,below,content.width,room,20,LabelOn11(face,UiKit.Ink),TextAlignmentOptions.TopLeft);
                body.lineSpacing=-14;body.enableAutoSizing=false;body.fontSize=19.5f;
                // Not shown when it does not fit whole even at the floor (§2: the hover and the details have it).
                if(body.GetPreferredValues(text,content.width,0).y>room+.5f)body.gameObject.SetActive(false);
                else{body.enableAutoSizing=true;body.fontSizeMax=20*UiKit.FontScale;body.fontSizeMin=19.5f;}
            }
            else
            {
                // A mini card shows its short text only when it fits whole at the body size (PLAY_LAYOUT 8 row 14); below
                // the name row it has the column's whole width (the enchantment badge is above it), up to a goods row's
                // price button.
                float room=content.yMax-textTop,textW=Mathf.Max(20,limit-tx);
                var body=UiKit.Text(t,"ShortText11",text,tx,textTop,textW,Mathf.Max(1,room),Body11(18),LabelOn11(face,UiKit.Ink),TextAlignmentOptions.TopLeft);
                // V1.1 (W20Z2): the label is pooled by name on the card object, and the play row pools its cards by instance: a
                // 150 % text card drawn on this object before (the tall branch above) left its auto-sizing on, so the mini card
                // measured its short text at the text card's largest size and hid a text that fits at its own fixed size.
                body.lineSpacing=-16;body.enableAutoSizing=false;
                // W14 E2: a goods row's text that wraps breaks at its punctuation or a " · " ("Lv.0 → Lv.1 / ×2 → ×2.75")
                // when that break still fits the row.
                if(priced)
                {
                    string balanced=BalancedLines11(body,text,textW);
                    if(balanced!=text&&body.GetPreferredValues(balanced,textW,0).y<=room+.5f)body.text=balanced;
                }
                if(body.GetPreferredValues(body.text,textW,0).y>room+.5f)body.gameObject.SetActive(false);
            }
            // W14 E2: the price button, centred on the content's height (CARD_TEMPLATE §5: the price is the button).
            if(priced)PriceButton13(t,card,content.xMax-priceW,content.y+Mathf.Max(0,(content.height-priceH)*.5f),priceW,priceH);
        }

        /// <summary>
        /// W14 E3 (SHOP_LAYOUT 8): a tile of the 我的构筑 row, about 100 units square: the framed icon block with the rarity
        /// gem inside its foot, and the name under it; the ordinal disc in the top-left corner and the enchantment badge in
        /// the top-right corner over the block. No short text and no value badge (the hover and the details have them).
        /// </summary>
        private void DrawTile13(Transform t,CardFace13 card,Rect inner,string rarity,Color tint,string title)
        {
            var face=(Image)t.GetComponent<UnityEngine.UI.Button>().targetGraphic;
            var content=new Rect(inner.x+2,inner.y+2,Mathf.Max(1,inner.width-4),Mathf.Max(1,inner.height-4));
            Color ink=LabelOn11(face,UiKit.Ink);
            // The name is measured first (it may step down to the 19.5-unit floor, or start at the size its row shares), then
            // drawn again last, over nothing.
            float nameH=FitLine13(t,"Name",title,content.x,content.y,content.width,20,ink,TextAlignmentOptions.Center,true,card.namePx).rectTransform.rect.height;
            // The ordinal disc keeps the top-left corner clear of the picture: the block starts right of it.
            const float disc=20;float lead=card.ordinal>0?disc+2:0;
            float block=Mathf.Max(24,Mathf.Min(content.width-lead,content.height-nameH-2));
            var blockArea=new Rect(lead>0?content.x+lead:content.x+(content.width-block)*.5f,content.y,block,block);
            var picture=RarityRing13(t,"RarityFrame13",blockArea,tint);
            float art=Mathf.Min(picture.width,picture.height)-2;
            CardArt13(t,card,picture.x+1,picture.y+1,art,true);
            float gem=Mathf.Min(16,block*.3f);
            if(rarity!=null)RarityGem13(t,rarity,tint,blockArea.center.x,blockArea.yMax-gem*.5f-1,gem);
            if(card.ordinal>0)OrdinalDisc13(t,card.ordinal,content.x,content.y,disc);
            if(Enchanted13(card))CardEnchant13(t,card,content.xMax-24,content.y,24);
            FitLine13(t,"Name",title,content.x,content.yMax-nameH,content.width,20,ink,TextAlignmentOptions.Center,true,card.namePx);
        }
        /// <summary>W14 E3: draws a row of tiles so that their names share one size, the size the longest name fits at (a
        /// three-character name is not drawn larger than its neighbours, and every picture is the same size). A second
        /// pass redraws the row only when the first gave different sizes. Returns the tiles as finally drawn.</summary>
        private UnityEngine.UI.Button[] TileRow13(int count,Func<int,float,UnityEngine.UI.Button> draw)
        {
            var tiles=new UnityEngine.UI.Button[count];
            for(int i=0;i<count;i++)tiles[i]=draw(i,0);
            if(count<2)return tiles;
            float[] sizes=tiles.Select(tile=>tile.transform.Find("Name")?.GetComponent<TextMeshProUGUI>()?.fontSize??0).ToArray();
            float smallest=sizes.Where(s=>s>0).DefaultIfEmpty(0).Min();
            if(smallest<=0||sizes.All(s=>Mathf.Approximately(s,smallest)))return tiles;
            for(int i=0;i<count;i++)tiles[i]=draw(i,smallest);
            return tiles;
        }

        // Price row metrics: coin, gap, price (bold, 22), gap, word. The row uses the button's width less 16 units (its frame
        // and a margin); the button's height allows 16 units for its drawn frame (top line, foot lip).
        private const float Coin13=26,CoinGap13=4,WordGap13=8,PriceFrame13=16;
        private string PriceWord13(CardFace13 card)=>card.sold?"已购买":string.IsNullOrEmpty(card.availability)?"购买":card.availability;
        private float PriceRowWidth13(CardFace13 card,float font)=>Coin13+CoinGap13+PreferredWidth11(PriceText073(card.price),22)+4+WordGap13+PreferredWidth11(PriceWord13(card),font);
        /// <summary>One row when coin, price and word fit (the word stepping down to the 19.5-unit floor); otherwise two rows:
        /// coin and price, then the word.</summary>
        private bool PriceTwoRows13(CardFace13 card,float width)=>PriceRowWidth13(card,19.5f/UiKit.FontScale)>width-16;
        private float PriceRowHeight13=>Mathf.Max(Coin13,Mathf.Ceil(22*UiKit.FontScale*1.3f))+4;
        /// <summary>V1.1 W14 (SHOP_LAYOUT 12.4, E1): a price button is at least 60 units high, drawn and hit alike, the play
        /// screen's rule (40 px at 720p).</summary>
        public const float PriceButtonMin14=60;
        private float PriceButtonHeight13(CardFace13 card,float width)
        {
            if(!PriceTwoRows13(card,width))return Mathf.Max(PriceButtonMin14,Mathf.Ceil(20*UiKit.FontScale*1.46f)+12);
            return Mathf.Max(PriceButtonMin14,PriceRowHeight13+Mathf.Ceil(PriceReasonFont13(card,width)*UiKit.FontScale*1.4f)+2+PriceFrame13);
        }
        /// <summary>The reason row of a two-row price button; in a goods row (W14 E2, a set <c>priceWidth</c>) it is set at the
        /// 19.5-unit body size, so the button is never taller than its row allows (93 units at 150 % text).</summary>
        private float PriceReasonFont13(CardFace13 card,float width)
        {
            float font=FitWord13(PriceWord13(card),width-16);
            return card.priceWidth>0?Mathf.Min(font,19.5f/UiKit.FontScale):font;
        }
        /// <summary>
        /// The price is the button (CARD_TEMPLATE §5): coin and price in Gold, then "购买"; when the good cannot be bought
        /// the button is disabled and says why. The second-level solid style, never the primary colour (AX-02).
        /// </summary>
        private void PriceButton13(Transform parent,CardFace13 card,float x,float y,float w,float h)
        {
            bool can=!card.sold&&string.IsNullOrEmpty(card.availability);
            var button=SolidButton11(parent,card.buttonName??"PriceButton13","",x,y,w,h,()=>{if(!IsBusy&&!modal)card.buy?.Invoke();},can,20);
            var face=(Image)button.targetGraphic;var root=button.transform;
            root.Find("Label")?.gameObject.SetActive(false);
            string word=PriceWord13(card),price=PriceText073(card.price);
            bool twoRows=PriceTwoRows13(card,w);
            float wordFont=20;
            if(twoRows)wordFont=PriceReasonFont13(card,w);
            else while(wordFont*UiKit.FontScale>19.5f&&PriceRowWidth13(card,wordFont)>w-16)wordFont=Mathf.Max(19.5f/UiKit.FontScale,wordFont-.5f);
            float priceW=PreferredWidth11(price,22)+4,wordW=PreferredWidth11(word,wordFont);
            // A button that cannot be used keeps its reason readable (Muted where it reaches 4.5:1 on the face, else the
            // readable pick) although WCAG exempts inactive controls: the reason is the information.
            Color priceColor=LabelOn11(face,can?UiKit.Gold:UiKit.Muted),wordColor=LabelOn11(face,can?UiKit.TextColor:UiKit.Muted);
            Image Coin(float cx,float cy)
            {
                var icon=UiKit.Box(root,"Coin",cx,cy,Coin13,Coin13,can?Color.white:new Color(1,1,1,.6f)).GetComponent<Image>();
                icon.sprite=UiKit.Template("ui_icon_coin");icon.preserveAspect=true;icon.raycastTarget=false;return icon;
            }
            if(!twoRows)
            {
                float start=(w-(Coin13+CoinGap13+priceW+WordGap13+wordW))*.5f;
                Coin(start,(h-Coin13)*.5f);
                NoWrap11(UiKit.Text(root,"Price",price,start+Coin13+CoinGap13,0,priceW,h,22,priceColor,TextAlignmentOptions.MidlineLeft)).fontStyle=FontStyles.Bold;
                NoWrap11(UiKit.Text(root,"PriceWord13",word,start+Coin13+CoinGap13+priceW+WordGap13,0,wordW+2,h,wordFont,wordColor,TextAlignmentOptions.MidlineLeft));
                return;
            }
            // Two rows inside the face's drawn frame: coin and price, then the reason.
            var frame=FrameInsets11(face);float priceRow=PriceRowHeight13,wordRow=Mathf.Ceil(wordFont*UiKit.FontScale*1.4f)+2;
            float top=frame.w+Mathf.Max(0,(h-frame.w-frame.y)-(priceRow+wordRow))*.5f,left=(w-(Coin13+CoinGap13+priceW))*.5f;
            Coin(left,top+(priceRow-Coin13)*.5f);
            NoWrap11(UiKit.Text(root,"Price",price,left+Coin13+CoinGap13,top,priceW,priceRow,22,priceColor,TextAlignmentOptions.MidlineLeft)).fontStyle=FontStyles.Bold;
            NoWrap11(UiKit.Text(root,"PriceWord13",word,8,top+priceRow,w-16,wordRow,wordFont,wordColor,TextAlignmentOptions.Center));
        }
        private static float FitWord13(string word,float width)
        {
            float font=20;while(font*UiKit.FontScale>19.5f&&PreferredWidth11(word,font)>width)font=Mathf.Max(19.5f/UiKit.FontScale,font-.5f);
            return font;
        }
    }
}
