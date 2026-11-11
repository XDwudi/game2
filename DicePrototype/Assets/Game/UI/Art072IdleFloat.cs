using UnityEngine;

namespace DiceCraft
{
    /// <summary>
    /// v0.72 idle life for static art (wordmark, menu still life): a slow smooth vertical float.
    /// It rests first so layout reads and captures see the authored position, and stops under
    /// reduced motion. Presentation only; it never moves a hit rectangle that receives input.
    /// </summary>
    public sealed class Art072IdleFloat:MonoBehaviour
    {
        public const float SettleSeconds=.6f;
        private RectTransform rect;
        private Vector2 origin;
        private float amplitude,period=3.2f,phase,settledAt;
        public float Offset{get;private set;}
        public void Configure(float amplitudeUnits,float periodSeconds,float phaseSeconds)
        {
            rect=(RectTransform)transform;origin=rect.anchoredPosition;
            amplitude=amplitudeUnits;period=Mathf.Max(.2f,periodSeconds);phase=phaseSeconds;settledAt=Time.unscaledTime;Offset=0;
        }
        private void LateUpdate()
        {
            if(rect==null)return;
            float rested=Time.unscaledTime-settledAt-SettleSeconds,next=0;
            if(!UiKit.ReducedMotion&&rested>0)
                next=Mathf.Sin((Time.unscaledTime+phase)*(Mathf.PI*2/period))*amplitude*Mathf.Clamp01(rested);
            if(Mathf.Approximately(next,Offset))return;
            Offset=next;rect.anchoredPosition=origin+Vector2.up*Offset;
        }
        private void OnDisable(){if(rect!=null){Offset=0;rect.anchoredPosition=origin;}}
    }
}
