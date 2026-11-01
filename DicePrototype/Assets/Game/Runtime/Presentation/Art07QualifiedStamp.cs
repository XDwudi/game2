using UnityEngine;
using TMPro;

namespace DiceCraft.Presentation
{
    /// <summary>480 ms Normal visual: 280 ms within the receipt, 200 ms nonblocking tail.
    /// It owns no input gate and never calls a game command.</summary>
    public sealed class Art07QualifiedStamp : MonoBehaviour
    {
        private RectTransform full,badge;
        private CanvasGroup fullGroup,badgeGroup;
        private TMP_Text title;
        private Vector2 start,end,fullCorrection,badgeCorrection;
        private float correctionAt;
        private bool reduced;
        public bool IsPlaying { get; private set; }
        public float Elapsed { get; private set; }
        public float Duration { get; private set; }
        public string ReceiptId { get; private set; }
        public int Begins { get; private set; }
        public void Initialize(RectTransform fullStamp,CanvasGroup fullAlpha,TMP_Text fullTitle,RectTransform smallBadge,CanvasGroup smallAlpha)
        {full=fullStamp;fullGroup=fullAlpha;title=fullTitle;badge=smallBadge;badgeGroup=smallAlpha;Hide();}
        public void Begin(string receiptId,bool reduce,bool fast,Vector2 origin,Vector2 destination)
        {
            ReceiptId=receiptId;Begins++;reduced=reduce;start=origin;end=destination;fullCorrection=badgeCorrection=Vector2.zero;correctionAt=0;Elapsed=0;Duration=reduce||fast?.12f:.48f;IsPlaying=true;
            full.gameObject.SetActive(true);badge.gameObject.SetActive(true);Evaluate();
        }
        public void Tick(float seconds,bool paused)
        {if(!IsPlaying||paused)return;Elapsed=Mathf.Min(Duration,Elapsed+Mathf.Max(0,seconds));Evaluate();if(Elapsed>=Duration)Finish();}
        public void SetPositions(Vector2 origin,Vector2 destination)
        {
            if(start==origin&&end==destination)return;
            Vector2 oldFull=full!=null?full.anchoredPosition:origin,oldBadge=badge!=null?badge.anchoredPosition:destination;
            start=origin;end=destination;
            if(IsPlaying)
            {
                fullCorrection=badgeCorrection=Vector2.zero;correctionAt=Elapsed;Evaluate();
                if(Elapsed>0)
                {
                    // A layout retarget never teleports either visible layer. Normal
                    // consumes the offset during the existing remaining time only.
                    fullCorrection=oldFull-full.anchoredPosition;badgeCorrection=oldBadge-badge.anchoredPosition;
                    Evaluate();
                }
            }
            else if(badge!=null&&badge.gameObject.activeSelf)badge.anchoredPosition=end;
        }
        private void Evaluate()
        {
            if(full==null)return;
            if(reduced||Duration<.48f){full.anchoredPosition=start+fullCorrection;full.localScale=Vector3.one;fullGroup.alpha=1;title.alpha=1;badgeGroup.alpha=0;return;}
            float tail=Mathf.Clamp01((Elapsed-.28f)/.2f),ease=tail*tail*(3-2*tail);
            float remaining=Mathf.Clamp01((Elapsed-correctionAt)/Mathf.Max(.0001f,Duration-correctionAt));
            float correction=1-remaining*remaining*(3-2*remaining);
            // Approach the badge horizontally first: the full-width paper must stay
            // above the score title while it is still large, then settle at the badge.
            var position=new Vector2(Mathf.Lerp(start.x,end.x,ease),Mathf.Lerp(start.y,end.y,ease*ease));
            full.anchoredPosition=position+fullCorrection*correction;
            full.localScale=Vector3.one*(Elapsed<.1f?Mathf.Lerp(.96f,1,Elapsed/.1f):Mathf.Lerp(1,.1f,ease));
            fullGroup.alpha=1-tail;title.alpha=1-Mathf.Clamp01(tail*3);badge.anchoredPosition=end+badgeCorrection*correction;badge.localScale=Vector3.one;badgeGroup.alpha=tail;
        }
        public void Finish()
        {
            IsPlaying=false;Elapsed=Duration;if(full!=null){full.gameObject.SetActive(false);full.localScale=Vector3.one;fullGroup.alpha=1;title.alpha=1;}
            if(badge!=null){badge.gameObject.SetActive(true);badge.anchoredPosition=end;badge.localScale=Vector3.one;badgeGroup.alpha=1;}
        }
        public void Hide(){IsPlaying=false;if(full!=null)full.gameObject.SetActive(false);if(badge!=null)badge.gameObject.SetActive(false);}
        private void OnApplicationFocus(bool focused){if(!focused&&IsPlaying)Finish();}
        private void OnApplicationPause(bool paused){if(paused&&IsPlaying)Finish();}
        private void OnDisable()=>Hide();
    }
}
