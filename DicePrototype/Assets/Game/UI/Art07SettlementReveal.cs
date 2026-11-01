using System;
using UnityEngine;

namespace DiceCraft
{
    /// <summary>Finite, receipt-identity-bound UI clock. Never owns a game command or wallet value.</summary>
    public sealed class Art07SettlementReveal : MonoBehaviour
    {
        private string key;
        private RectTransform paper;
        private CanvasGroup paperAlpha;
        private CanvasGroup[] cashRows=Array.Empty<CanvasGroup>();
        private RectTransform[] coins=Array.Empty<RectTransform>();
        private CanvasGroup[] coinAlpha=Array.Empty<CanvasGroup>();
        private UnityEngine.UI.Button skip;
        private Vector2 coinFrom,coinTo;
        private float speed=1;
        private bool reduced;
        public bool IsPlaying { get; private set; }
        public float Elapsed { get; private set; }
        public float Duration { get; private set; }
        public int Begins { get; private set; }
        public int ActiveCoinCount { get { int n=0;foreach(var c in coins)if(c!=null&&c.gameObject.activeSelf)n++;return n; } }
        public string Identity=>key;

        private static CanvasGroup Alpha(GameObject go)
        {
            var group=go.GetComponent<CanvasGroup>();if(group==null)group=go.AddComponent<CanvasGroup>();
            group.blocksRaycasts=true;group.interactable=true;return group;
        }
        public void Bind(string identity,bool animate,bool fast,bool reduce,bool instant,RectTransform sheet,
            RectTransform[] rows,RectTransform[] symbols,Vector2 from,Vector2 to,UnityEngine.UI.Button skipButton)
        {
            bool same=key==identity;
            paper=sheet;paperAlpha=Alpha(sheet.gameObject);skip=skipButton;coinFrom=from;coinTo=to;
            cashRows=new CanvasGroup[rows.Length];for(int i=0;i<rows.Length;i++)cashRows[i]=Alpha(rows[i].gameObject);
            coins=symbols;coinAlpha=new CanvasGroup[symbols.Length];for(int i=0;i<symbols.Length;i++)coinAlpha[i]=Alpha(symbols[i].gameObject);
            if(!same)
            {
                key=identity;Elapsed=0;speed=fast?2:1;reduced=reduce;
                Duration=instant?0:reduce?.12f:.74f/speed;
                IsPlaying=animate&&Duration>0;if(IsPlaying)Begins++;
                if(!IsPlaying)Elapsed=Duration;
            }
            else if(instant||reduce!=reduced||fast!=(speed>1))Complete();
            Evaluate();
        }
        public void Tick(float seconds,bool paused)
        {
            if(!IsPlaying||paused)return;
            Elapsed=Math.Min(Duration,Elapsed+Math.Max(0,seconds));Evaluate();
            if(Elapsed>=Duration)Complete();
        }
        public void SyncOptions(bool fast,bool reduce,bool instant)
        {
            if(IsPlaying&&(instant||reduce!=reduced||fast!=(speed>1)))Complete();
        }
        public void Complete()
        {
            Elapsed=Duration;IsPlaying=false;
            if(paper!=null)paper.localScale=Vector3.one;
            if(paperAlpha!=null)paperAlpha.alpha=1;
            foreach(var g in cashRows)if(g!=null)g.alpha=1;
            foreach(var c in coins)if(c!=null)c.gameObject.SetActive(false);
            if(skip!=null){skip.interactable=false;skip.gameObject.SetActive(false);}
        }
        private void Evaluate()
        {
            if(!IsPlaying){Complete();return;}
            if(skip!=null){skip.gameObject.SetActive(true);skip.interactable=true;}
            float normal=Elapsed*speed;
            float entrance=reduced?Mathf.Clamp01(Elapsed/.12f):Mathf.Clamp01(normal/.18f);
            if(paperAlpha!=null)paperAlpha.alpha=entrance;
            if(paper!=null)paper.localScale=reduced?Vector3.one:new Vector3(1,Mathf.Lerp(.92f,1,1-Mathf.Pow(1-entrance,3)),1);
            for(int i=0;i<cashRows.Length;i++)if(cashRows[i]!=null)
                cashRows[i].alpha=reduced?1:Mathf.Clamp01((normal-.18f-i*.1f)/.1f);
            for(int i=0;i<coins.Length;i++)
            {
                if(coins[i]==null)continue;
                float t=(normal-.38f-i*.025f)/.26f;
                bool active=!reduced&&t>=0&&t<1;
                coins[i].gameObject.SetActive(active);if(!active)continue;
                float ease=1-Mathf.Pow(1-t,2);
                var p=Vector2.Lerp(coinFrom,coinTo,ease)+Vector2.up*(28*Mathf.Sin(t*Mathf.PI));
                coins[i].anchoredPosition=p+new Vector2(0,(i%2)*6);
                coins[i].localScale=Vector3.one;
                if(coinAlpha[i]!=null)coinAlpha[i].alpha=Mathf.Min(1,t*8)*Mathf.Min(1,(1-t)*5);
            }
        }
        private void OnApplicationFocus(bool focused){if(!focused)Complete();}
        private void OnApplicationPause(bool paused){if(paused)Complete();}
        private void OnDisable(){Complete();}
    }
}
