using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceCraft
{
    /// <summary>The receipt clocks only UI values. The session has already granted the reward.</summary>
    public sealed class Art07SettlementReveal : MonoBehaviour
    {
        private string key;
        private RectTransform paper,stamp,front,back;
        private CanvasGroup paperAlpha,proceedAlpha,stampAlpha;
        private CanvasGroup[] chips=Array.Empty<CanvasGroup>(),coinAlpha=Array.Empty<CanvasGroup>();
        private RectTransform[] coins=Array.Empty<RectTransform>();
        private Button skip,proceed,details;
        private TextMeshProUGUI total,wallet;
        private Vector2 from,to,stampAt;
        private long before,after;
        private bool reduced,boss,canContinue;
        private float speed;
        public bool IsPlaying {get;private set;}
        public float Elapsed {get;private set;}
        public float Duration {get;private set;}
        public int Begins {get;private set;}
        public string Identity=>key;
        public int ActiveCoinCount {get {int count=0;foreach(var coin in coins)if(coin!=null&&coin.gameObject.activeSelf)count++;return count;}}
        private static CanvasGroup Group(Component obj)=>UiKit.Component<CanvasGroup>(obj.gameObject);

        public void Bind(string identity,bool animate,bool fast,bool reduce,bool instant,RectTransform sheet,
            RectTransform[] rows,RectTransform[] symbols,Vector2 start,Vector2 target,Button skipButton,Button primary,Button detailButton,
            TextMeshProUGUI amount,TextMeshProUGUI money,long goldBefore,long goldAfter,bool bossTimeline,
            RectTransform defeated,RectTransform plaqueFront,RectTransform plaqueBack)
        {
            bool same=key==identity;
            paper=sheet;paperAlpha=Group(sheet);skip=skipButton;proceed=primary;proceedAlpha=Group(primary);
            canContinue=primary.interactable;details=detailButton;
            total=amount;wallet=money;before=goldBefore;after=goldAfter;
            stamp=defeated;front=plaqueFront;back=plaqueBack;boss=bossTimeline;
            if(stamp!=null){stampAt=stamp.anchoredPosition;stampAlpha=Group(stamp);}
            coins=symbols;chips=Array.ConvertAll(rows,item=>Group(item));coinAlpha=Array.ConvertAll(coins,item=>Group(item));
            from=start;to=target;
            if(!same)
            {
                key=identity;speed=fast?2:1;reduced=reduce;
                Duration=instant?0:reduce?.12f:(boss?(front==null?1.10f:1.55f):.74f)/speed;
                IsPlaying=animate&&Duration>0;Elapsed=IsPlaying?0:Duration;if(IsPlaying)Begins++;
            }
            else if(instant||reduce!=reduced||fast!=(speed>1)){Complete();return;}
            Evaluate();
        }

        public void Tick(float seconds,bool paused)
        {
            if(!IsPlaying||paused)return;
            Elapsed=Math.Min(Duration,Elapsed+Math.Max(0,seconds));
            if(Elapsed>=Duration)Complete();else Evaluate();
        }
        public void SyncOptions(bool fast,bool reduce,bool instant)
        {
            if(IsPlaying&&(instant||reduce!=reduced||fast!=(speed>1)))Complete();
        }
        public void Complete()
        {
            if(paper==null)return;
            Elapsed=Duration;IsPlaying=false;Evaluate();
        }

        private static float Segment(float t,float start,float end)=>Mathf.InverseLerp(start,end,t);
        private static long Rolling(long from,long to,float progress)
        {
            if(progress<=0)return from;if(progress>=1)return to;
            return (long)decimal.Round(from+((decimal)to-from)*(decimal)progress,0,MidpointRounding.AwayFromZero);
        }
        private static void Number(TextMeshProUGUI text,string value)
        {
            if(text!=null&&text.text!=value)text.text=value;
        }
        private void Evaluate()
        {
            bool finished=!IsPlaying;
            float t=Elapsed*speed;
            float entrance=finished?1:reduced?Mathf.Clamp01(Elapsed/.12f):Segment(t,0,.18f);
            paperAlpha.alpha=entrance;
            paper.localScale=reduced||finished?Vector3.one:new Vector3(1,Mathf.Lerp(.92f,1,1-Mathf.Pow(1-entrance,3)),1);
            bool finalValues=finished||reduced;
            float chipStart=boss?.30f:.18f;
            for(int i=0;i<chips.Length;i++)chips[i].alpha=finalValues?1:Segment(t,chipStart+i*.1f,chipStart+(i+1)*.1f);
            Number(total,"+"+PlayerNumberFormat.Gold(Rolling(0,after-before,finalValues?1:Segment(t,chipStart,chipStart+.4f))));
            float flyStart=boss?.56f:.38f,flyEnd=boss?1.10f:.74f;
            Number(wallet,PlayerNumberFormat.Gold(Rolling(before,after,finalValues?1:Segment(t,flyStart,flyEnd))));
            for(int i=0;i<coins.Length;i++)
            {
                float u=(t-flyStart-i*(boss?.035f:.025f))/(boss?.40f:.26f);
                bool active=!finalValues&&u>=0&&u<1;coins[i].gameObject.SetActive(active);
                if(!active)continue;
                coins[i].anchoredPosition=Vector2.Lerp(from,to,1-Mathf.Pow(1-u,2))+Vector2.up*((28+i%2*6)*Mathf.Sin(u*Mathf.PI));
                coinAlpha[i].alpha=Mathf.Min(1,u*8)*Mathf.Min(1,(1-u)*5);
            }
            if(stamp!=null)
            {
                float fall=finalValues||!boss?1:Segment(t,.18f,.54f),remain=Mathf.Pow(1-fall,3);
                stampAlpha.alpha=fall>0?1:0;stamp.localScale=Vector3.one*(1+.18f*remain);stamp.anchoredPosition=stampAt+Vector2.up*18*remain;
            }
            if(front!=null&&back!=null)
            {
                bool face=finalValues||t>=1.15f;
                front.gameObject.SetActive(face);back.gameObject.SetActive(!face);
                float x=finalValues?1:face?Segment(t,1.15f,1.40f):1-Segment(t,.90f,1.15f);
                (face?front:back).localScale=new Vector3(x,1,1);
            }
            float primaryAlpha=finished?1:!reduced&&boss&&front!=null?Segment(t,1.40f,1.55f):0;
            proceed.gameObject.SetActive(primaryAlpha>0);proceedAlpha.alpha=primaryAlpha;
            proceed.interactable=finished&&canContinue;proceedAlpha.blocksRaycasts=finished;
            skip.gameObject.SetActive(!finished&&primaryAlpha==0);skip.interactable=!finished;
            details.interactable=true;
        }

        private void OnApplicationFocus(bool focused){if(!focused&&IsPlaying)Complete();}
        private void OnApplicationPause(bool paused){if(paused&&IsPlaying)Complete();}
        private void OnDisable(){if(IsPlaying)Complete();}
    }
}
