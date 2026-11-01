using UnityEngine;

namespace DiceCraft
{
    // Position-only, UI-owned opening motion. Rule state and the presentation
    // receipt clock are never referenced. The regular modal owns the close tail.
    public sealed class Art07ScoreDrawerMotion : MonoBehaviour
    {
        private RectTransform panel;
        private Vector2 destination;
        private float elapsed,duration;
        private bool moving,initialized;
        public bool IsPlaying=>moving;
        public float Duration=>duration;
        public void Begin(float seconds,bool reduced)
        {
            panel=(RectTransform)transform;destination=panel.anchoredPosition;initialized=true;
            elapsed=0;duration=reduced?Mathf.Min(.08f,seconds):seconds;moving=!reduced&&duration>0;
            panel.anchoredPosition=destination+(moving?new Vector2(0,24):Vector2.zero);
        }
        private void Update()
        {
            if(!moving)return;
            elapsed+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(elapsed/duration);
            panel.anchoredPosition=destination+new Vector2(0,24*Mathf.Pow(1-t,3));
            if(t>=1){moving=false;panel.anchoredPosition=destination;}
        }
        private void OnDisable(){moving=false;if(initialized&&panel!=null)panel.anchoredPosition=destination;}
    }
}
