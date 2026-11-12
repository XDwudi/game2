using UnityEngine;

namespace DiceCraft
{
    // v0.72 rc.2 micro-motion. Decorative only: none of these move a hit rectangle, change a value
    // or delay input, and all of them rest immediately under "reduce motion".

    /// <summary>A readout gives one short pop when its value changes within the same context (run / stage).</summary>
    public sealed class Art072ValuePop:MonoBehaviour
    {
        public const float Duration=.34f,Peak=1.14f;
        private string shown,context;private float started=-1;
        public bool Playing=>started>=0;
        public void Configure(string contextKey,string value)
        {
            // A redraw with the same value must not cancel a pop that is already playing.
            bool changed=shown!=null&&context==contextKey&&shown!=value;
            if(context!=contextKey)started=-1;
            shown=value;context=contextKey;
            if(changed)started=UiKit.ReducedMotion?-1:Time.unscaledTime;
            Apply();
        }
        private void LateUpdate(){if(started>=0)Apply();}
        private void Apply()
        {
            float scale=1;
            if(started>=0)
            {
                float t=(Time.unscaledTime-started)/Duration;
                if(t>=1||UiKit.ReducedMotion)started=-1;else scale=1+(Peak-1)*Mathf.Sin(t*Mathf.PI);
            }
            transform.localScale=new Vector3(scale,scale,1);
        }
        private void OnDisable(){started=-1;transform.localScale=Vector3.one;}
    }

    /// <summary>Eases a meter fill to its new width instead of jumping.</summary>
    public sealed class Art072ProgressEase:MonoBehaviour
    {
        public const float Seconds=.45f;
        private string context;private float from,target,shown=-1,started=-1,height;
        public float Target=>target;
        public void Configure(string contextKey,float width,float barHeight)
        {
            height=barHeight;
            if(context!=contextKey||shown<0||UiKit.ReducedMotion){context=contextKey;from=shown=target=width;started=-1;}
            else if(Mathf.Abs(width-target)>.01f){from=shown;target=width;started=Time.unscaledTime;}
            Apply();
        }
        private void LateUpdate(){if(started>=0)Apply();}
        private void Apply()
        {
            if(started>=0)
            {
                float t=UiKit.ReducedMotion?1:Mathf.Clamp01((Time.unscaledTime-started)/Seconds);
                shown=Mathf.Lerp(from,target,1-(1-t)*(1-t)*(1-t));
                if(t>=1){started=-1;shown=target;}
            }
            ((RectTransform)transform).sizeDelta=new Vector2(shown,height);
        }
        private void OnDisable(){started=-1;shown=target;}
    }

    /// <summary>Slow breathing of a selection ring's opacity.</summary>
    public sealed class Art072Pulse:MonoBehaviour
    {
        public const float Period=1.5f,Floor=.55f;
        private UnityEngine.UI.Graphic graphic;private Color color;
        public void Configure(Color baseColor)
        {
            graphic=GetComponent<UnityEngine.UI.Graphic>();color=baseColor;Apply();
        }
        private void LateUpdate(){Apply();}
        private void Apply()
        {
            if(graphic==null)return;
            float k=UiKit.ReducedMotion?1:Mathf.Lerp(Floor,1,.5f+.5f*Mathf.Cos(Time.unscaledTime*(Mathf.PI*2/Period)));
            graphic.color=new Color(color.r,color.g,color.b,color.a*k);
        }
    }
}
