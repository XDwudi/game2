using System;
using System.Collections.Generic;
using UnityEngine;

namespace DiceCraft
{
    /// <summary>S03 live-card entrance only. Keyed by the actual run/stage; never owns game commands.</summary>
    public sealed class Art07StageCardsMotion : MonoBehaviour
    {
        private readonly HashSet<string> shownStages=new HashSet<string>();
        private string run,stage;
        private RectTransform[] cards=Array.Empty<RectTransform>();
        private Vector2[] origins=Array.Empty<Vector2>();
        private CanvasGroup[] groups=Array.Empty<CanvasGroup>();
        private Func<bool> paused;
        private bool reduced;
        private double lastClock;
        public bool IsPlaying { get; private set; }
        public float Elapsed { get; private set; }
        public float Duration { get; private set; }
        public int EnterSequence { get; private set; }
        public float Offset { get; private set; }

        public void Bind(string runId,string stageId,bool reduce,bool instant,Func<bool> isPaused,params RectTransform[] roots)
        {
            bool same=run==runId&&stage==stageId;
            run=runId;stage=stageId;paused=isPaused;reduced=reduce;
            cards=roots;origins=new Vector2[cards.Length];groups=new CanvasGroup[cards.Length];
            for(int i=0;i<cards.Length;i++)
            {
                origins[i]=cards[i].anchoredPosition;
                groups[i]=UiKit.Component<CanvasGroup>(cards[i].gameObject);
                groups[i].interactable=true;groups[i].blocksRaycasts=true;
            }
            if(shownStages.Add(runId+"/"+stageId))
            {
                EnterSequence++;Elapsed=0;Duration=instant?0:reduce?.08f:.16f;IsPlaying=Duration>0;lastClock=Time.realtimeSinceStartupAsDouble;
            }
            else if(!same||!IsPlaying){Complete();return;}
            if(instant){Complete();return;}
            if(reduce){Duration=Mathf.Min(Duration,.08f);Elapsed=Mathf.Min(Elapsed,Duration);}
            Evaluate();
        }

        private void Update()
        {
            if(!IsPlaying)return;
            if(cards.Length==0||cards[0]==null||!cards[0].gameObject.activeInHierarchy){Complete();return;}
            double now=Time.realtimeSinceStartupAsDouble;float delta=(float)Math.Max(0,now-lastClock);lastClock=now;
            if(paused?.Invoke()==true)return;
            Elapsed=Mathf.Min(Duration,Elapsed+delta);Evaluate();
            if(Elapsed>=Duration)Complete();
        }
        private void Evaluate()
        {
            float t=Duration<=0?1:Mathf.Clamp01(Elapsed/Duration),ease=1-(1-t)*(1-t);
            Offset=reduced?0:20*(1-ease);
            for(int i=0;i<cards.Length;i++)if(cards[i]!=null)
            {cards[i].anchoredPosition=origins[i]+Vector2.down*Offset;if(groups[i]!=null)groups[i].alpha=ease;}
        }
        public void Complete()
        {
            IsPlaying=false;Elapsed=Duration;Offset=0;
            for(int i=0;i<cards.Length;i++)if(cards[i]!=null)
            {cards[i].anchoredPosition=origins[i];if(groups[i]!=null)groups[i].alpha=1;}
        }
        private void OnDisable(){Complete();}
    }
}
