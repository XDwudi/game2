using System;
using System.Collections.Generic;
using System.Linq;
using DiceCraft.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private struct RevealCard15 {public RectTransform rect,back; public CanvasGroup opacity; public Vector2 origin;}
        private RevealCard15[] revealCards15;
        private CanvasGroup[] revealSections15;
        private Transform lootSkip15, lootMask15;
        private float revealTime15, revealDuration15;
        private bool revealFast15, revealReduced15;
        public bool LootRevealPlaying15 => revealCards15 != null;
        public float LootRevealTime15 => revealTime15;
        public float LootRevealDuration15 => revealDuration15;

        private void BindLootReveal15(List<RectTransform> cards,bool first)
        {
            if(!first)
            {
                if(LootRevealPlaying15)ClearLootReveal15();
                return;
            }
            if(instant07 || cards.Count==0)return;
            revealFast15=fast;revealReduced15=reduced;revealTime15=0;
            revealDuration15=reduced?.12f:Mathf.Max(.24f,(cards.Count-1)*.08f+.20f)*(fast?.5f:1f);
            revealSections15=new[]{"Stats07","Build14","OwnedStrip","LootInfo15"}.Select(n=>main.Find(n)).Where(t=>t!=null)
                .Select(t=>UiKit.Component<CanvasGroup>(t.gameObject)).ToArray();
            revealCards15=cards.Select(rect=>
            {
                var back=UiKit.Box(rect,"CardBack15",0,0,rect.rect.width,rect.rect.height,UiKit.PanelLight);
                var image=back.GetComponent<Image>();image.sprite=UiKit.Template("ui_card_shop");image.type=Image.Type.Sliced;
                back.SetAsLastSibling();
                return new RevealCard15{rect=rect,back=back,opacity=UiKit.Component<CanvasGroup>(rect.gameObject),origin=rect.anchoredPosition};
            }).ToArray();
            lootMask15=HitArea073(main,"LootRevealSurface15",0,84,1920,996,FinishLootReveal15).transform;
            var r=S14.leave;
            lootSkip15=ShopButton14(main,"SkipLootReveal15","跳过演出",r.x,r.y,r.width,r.height,FinishLootReveal15,true).transform;
            main.Find("LootEnterShop15")?.gameObject.SetActive(false);
            ApplyLootReveal15();
        }

        private void TickLootReveal15()
        {
            if(!LootRevealPlaying15)return;
            if(fast!=revealFast15||reduced!=revealReduced15||instant07||!LootPage15||inMenu)
            {FinishLootReveal15();return;}
            if(modal)return;
            revealTime15=Mathf.Min(revealDuration15,revealTime15+Time.unscaledDeltaTime);
            if(revealTime15>=revealDuration15){FinishLootReveal15();return;}
            ApplyLootReveal15();
        }

        private void ApplyLootReveal15()
        {
            float elapsed=revealTime15*(revealFast15?2:1);
            foreach(var g in revealSections15)g.alpha=Mathf.Clamp01(revealReduced15?revealTime15/.12f:elapsed/.24f);
            for(int i=0;i<revealCards15.Length;i++)
            {
                var card=revealCards15[i];float p=Mathf.Clamp01((elapsed-i*.08f)/.2f);
                float scale=revealReduced15?1:Mathf.Abs(Mathf.Cos(Mathf.PI*p));
                card.rect.localScale=new Vector3(scale,1,1);
                card.rect.anchoredPosition=card.origin+Vector2.right*((1-scale)*card.rect.rect.width*.5f);
                card.opacity.alpha=revealReduced15?Mathf.Clamp01(revealTime15/.12f):1;
                card.back.gameObject.SetActive(!revealReduced15&&p<.5f);
            }
        }

        private void ClearLootReveal15()
        {
            if(revealCards15==null)return;
            foreach(var card in revealCards15)
            {
                if(card.rect==null)continue;
                card.rect.localScale=Vector3.one;card.rect.anchoredPosition=card.origin;card.opacity.alpha=1;
                if(card.back!=null)card.back.gameObject.SetActive(false);
            }
            foreach(var group in revealSections15)if(group!=null)group.alpha=1;
            if(lootSkip15!=null)lootSkip15.gameObject.SetActive(false);
            if(lootMask15!=null)lootMask15.gameObject.SetActive(false);
            if(main!=null && LootPage15)main.Find("LootEnterShop15")?.gameObject.SetActive(true);
            revealTime15=revealDuration15;revealCards15=null;revealSections15=null;
        }

        private void FinishLootReveal15() => ClearLootReveal15();

        private Action<float> LootReceiptTicks15(ShopOffer bought,RunState after)
        {
            if(string.IsNullOrEmpty(bought.groupId)||!LootPage15)return null;
            var group=after.shop.groups.First(g=>g.groupId==bought.groupId);
            var fading=group.closed?after.shop.offers.Where(o=>o.groupId==group.groupId&&!o.sold&&o.category!="gold")
                .Select(o=>FindTransactionRect07((o.category=="controller"?"Offer_":"Product_")+o.offerId))
                .Where(r=>r!=null).Select(r=>UiKit.Component<CanvasGroup>(r.gameObject)).ToArray():Array.Empty<CanvasGroup>();
            bool cash=bought.category=="gold"&&!reduced;
            var coins=new RectTransform[cash?Math.Min(5,Config.lootGoldValue):0];
            var target=ShopTopRight14(S14).gold.center;
            var start=new Vector2(Large11?480:460,Large11?648:630);
            for(int i=0;i<coins.Length;i++)
            {
                coins[i]=UiKit.Box(transactionReadout07,"LootCoin15_"+i,start.x,start.y,32,32,Color.white);
                var image=coins[i].GetComponent<Image>();image.sprite=UiKit.Template("ui_icon_coin");image.preserveAspect=true;
            }
            return seconds=>
            {
                foreach(var g in fading)if(g!=null)g.alpha=Mathf.Lerp(1,.35f,Mathf.Clamp01(seconds/.2f));
                for(int i=0;i<coins.Length;i++)
                {
                    float t=Mathf.Clamp01((seconds-i*.025f)/.24f);
                    var pos=Vector2.Lerp(start,target,t);pos.y-=48*Mathf.Sin(Mathf.PI*t);
                    coins[i].anchoredPosition=new Vector2(pos.x-16,-pos.y+16);
                    coins[i].gameObject.SetActive(seconds>=i*.025f&&t<1);
                }
            };
        }
    }
}
