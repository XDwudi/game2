using UnityEngine;

namespace DiceCraft
{
    /// <summary>Finite visual-only screen entrance. Controls stay usable throughout.</summary>
    public sealed class Art07ScreenReveal : MonoBehaviour
    {
        private CanvasGroup[] rows;
        private string boundKey;
        private float elapsed,duration;
        private bool playing,afterStamp,reduced;
        private RectTransform stamp;
        private Vector2 stampPosition;
        private string stampKey;
        private float stampElapsed;
        public bool IsPlaying=>playing;
        public float Elapsed=>elapsed;
        public float Duration=>duration;
        public bool AutomaticTick { get; set; }=true;

        public void Bind(string key,float seconds,params RectTransform[] elements)=>BindRows(key,seconds,false,elements);
        public void BindAfterStamp(string key,params RectTransform[] elements)=>BindRows(key,1.2f,true,elements);
        private void BindRows(string key,float seconds,bool ordered,RectTransform[] elements)
        {
            bool same=boundKey==key;
            if(!same)
            {
                boundKey=key;elapsed=0;reduced=UiKit.ReducedMotion;
                duration=reduced?.12f:seconds;afterStamp=ordered;playing=true;
            }
            rows=new CanvasGroup[elements.Length];
            for(int i=0;i<elements.Length;i++)
            {
                var group=elements[i].GetComponent<CanvasGroup>();if(group==null)group=elements[i].gameObject.AddComponent<CanvasGroup>();
                rows[i]=group;group.blocksRaycasts=true;group.interactable=true;
            }
            Evaluate();
        }
        public void BindStamp(string key,RectTransform seal)
        {
            stamp=seal;
            if(stampKey!=key){stampKey=key;stampPosition=seal.anchoredPosition;stampElapsed=playing?0:.36f;}
            EvaluateStamp();
        }
        public void Complete()
        {
            playing=false;elapsed=duration;stampElapsed=.36f;
            if(rows!=null)foreach(var g in rows)if(g!=null)g.alpha=1;
            if(stamp!=null){stamp.localScale=Vector3.one;stamp.anchoredPosition=stampPosition;}
        }
        public void Tick(float seconds)
        {
            if(reduced!=UiKit.ReducedMotion){Complete();return;}
            float delta=Mathf.Max(0,seconds);
            if(stamp!=null&&stamp.gameObject.activeInHierarchy&&stampElapsed<.36f)
            {stampElapsed=Mathf.Min(.36f,stampElapsed+delta);EvaluateStamp();}
            if(!playing)return;
            elapsed=Mathf.Min(duration,elapsed+delta);Evaluate();
            if(elapsed>=duration)Complete();
        }
        private void EvaluateStamp()
        {
            if(stamp==null)return;
            float t=Mathf.Clamp01(stampElapsed/.36f),remaining=(1-t)*(1-t);
            stamp.localScale=Vector3.one*(reduced?1:1+.18f*remaining);
            stamp.anchoredPosition=stampPosition+(reduced?Vector2.zero:new Vector2(0,18*remaining));
        }
        private void Evaluate()
        {
            if(rows==null)return;
            for(int i=0;i<rows.Length;i++)if(rows[i]!=null)
            {
                if(!playing){rows[i].alpha=1;continue;}
                if(reduced){rows[i].alpha=Mathf.Clamp01(elapsed/.12f);continue;}
                if(afterStamp)
                {
                    // The 360ms seal resolves before the six independent result rows.
                    float rowSeconds=(duration-.36f)/Mathf.Max(1,rows.Length);
                    rows[i].alpha=Mathf.Clamp01((elapsed-.36f-i*rowSeconds)/rowSeconds);
                }
                else
                {
                    float step=duration/(rows.Length+1);
                    rows[i].alpha=Mathf.Clamp01((elapsed-i*step)/Mathf.Max(.001f,step*2));
                }
            }
        }
        private void Update(){if(AutomaticTick)Tick(Time.unscaledDeltaTime);}
        private void OnApplicationFocus(bool focused){if(!focused)Complete();}
        private void OnApplicationPause(bool paused){if(paused)Complete();}
        private void OnDisable()=>Complete();
    }
}
