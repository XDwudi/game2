using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DiceCraft
{
    // Only the button face moves; the hit rectangle and click event never move or repeat.
    public sealed class Art07ButtonMotion:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler,ISelectHandler,IDeselectHandler
    {
        private Button button;
        private GameObject focus;
        private RectTransform face,lip,label;
        private Vector2 faceOrigin,lipOrigin,labelOrigin;
        private bool hovered,pressed,selected;
        private int surface;
        private float displacement,cardScale=1,tilt,settledAt;
        private struct CardVisual{public RectTransform rect;public Vector2 position;public Vector3 scale;public Quaternion rotation;}
        private readonly System.Collections.Generic.List<CardVisual> cardVisuals=new System.Collections.Generic.List<CardVisual>();
        // v0.72 card life: a slow idle sway once the layout has rested, and a tilt toward the pointer.
        public const float IdleSwayDegrees072=.6f,IdleSwayPeriod072=3.2f,IdleSettleSeconds072=.6f,HoverTiltDegrees072=3.5f;
        public static bool IdleSway072=true;
        public float CardTilt072=>tilt;
        public void SetSurface(int value){RestoreCard();surface=value;Apply();}
        public void Configure(Button value,GameObject ring)
        {
            // UiKit has just re-rented every painted child at its authored transform, so the
            // previous baselines are obsolete: forget them instead of writing them back.
            cardVisuals.Clear();displacement=0;cardScale=1;tilt=0;surface=0;
            button=value;focus=ring;face=transform.Find("Face") as RectTransform;lip=transform.Find("Lip") as RectTransform;label=transform.Find("Label") as RectTransform;
            faceOrigin=face.anchoredPosition;lipOrigin=lip.anchoredPosition;labelOrigin=label.anchoredPosition;
            hovered=pressed=selected=false;Apply();
        }
        private void Apply()
        {
            bool enabled=button!=null&&button.IsInteractable();
            if(focus!=null)focus.SetActive(enabled&&selected);
            if(surface!=0)return;
            float dy=enabled&&pressed?-3:enabled&&hovered&&!UiKit.ReducedMotion?1:0;
            if(face!=null)face.anchoredPosition=faceOrigin+Vector2.up*dy;
            if(lip!=null)lip.anchoredPosition=lipOrigin+Vector2.up*dy;
            if(label!=null)label.anchoredPosition=labelOrigin+Vector2.up*dy;
        }
        private void LateUpdate()
        {
            if(surface!=1)return;
            bool lifted=button!=null&&button.IsInteractable()&&hovered&&!pressed&&!UiKit.ReducedMotion;
            float target=lifted?8:0,targetScale=lifted?1.03f:1,targetTilt=0;
            if(cardVisuals.Count==0)
            {
                foreach(Transform child in transform)if(child.gameObject.activeSelf&&child.gameObject!=focus)
                {var r=(RectTransform)child;cardVisuals.Add(new CardVisual{rect=r,position=r.anchoredPosition,scale=r.localScale,rotation=r.localRotation});}
                settledAt=Time.unscaledTime;
            }
            var root=(RectTransform)transform;
            if(lifted)
            {
                var pointer=UnityEngine.InputSystem.Pointer.current;
                if(pointer!=null&&RectTransformUtility.ScreenPointToLocalPointInRectangle(root,pointer.position.ReadValue(),null,out Vector2 local))
                    targetTilt=-Mathf.Clamp((local.x-root.rect.center.x)/Mathf.Max(1,root.rect.width*.5f),-1,1)*HoverTiltDegrees072;
            }
            else if(IdleSway072&&!UiKit.ReducedMotion&&!pressed)
            {
                // Rest first so a redraw never starts mid-swing; neighbours are out of phase.
                float rested=Time.unscaledTime-settledAt-IdleSettleSeconds072;
                if(rested>0)targetTilt=Mathf.Sin((Time.unscaledTime+(transform.GetSiblingIndex()%17)*.83f)*(Mathf.PI*2/IdleSwayPeriod072))*IdleSwayDegrees072*Mathf.Clamp01(rested);
            }
            float t=1-Mathf.Exp(-Time.unscaledDeltaTime/ .045f);
            displacement=Mathf.Lerp(displacement,target,t);cardScale=Mathf.Lerp(cardScale,targetScale,t);tilt=Mathf.Lerp(tilt,targetTilt,t);
            TransformCardVisuals();
        }
        private void TransformCardVisuals()
        {
            // The transparent hit root stays fixed. Every painted child shares one visual transform,
            // always derived from its authored baseline so nothing drifts over time.
            var root=(RectTransform)transform;Vector2 center=new Vector2(root.rect.width*.5f,-root.rect.height*.5f);
            var turn=Quaternion.Euler(0,0,tilt);
            foreach(var v in cardVisuals)if(v.rect!=null)
            {
                Vector2 offset=turn*((v.position-center)*cardScale);
                v.rect.anchoredPosition=center+offset+Vector2.up*displacement;v.rect.localScale=v.scale*cardScale;v.rect.localRotation=turn*v.rotation;
            }
        }
        private void RestoreCard()
        {
            displacement=0;cardScale=1;tilt=0;
            if(surface==1)TransformCardVisuals();
            cardVisuals.Clear();
        }
        public void OnPointerEnter(PointerEventData e){hovered=true;if(button!=null&&button.IsInteractable())UiKit.Feedback?.Invoke("SFX_UI_HOVER");Apply();}
        public void OnPointerExit(PointerEventData e){hovered=pressed=false;settledAt=Time.unscaledTime;Apply();}
        public void OnPointerDown(PointerEventData e){if(e.button!=PointerEventData.InputButton.Left)return;pressed=true;if(button!=null&&button.IsInteractable())UiKit.Feedback?.Invoke("SFX_UI_PRESS");Apply();}
        public void OnPointerUp(PointerEventData e){pressed=false;settledAt=Time.unscaledTime;Apply();}
        public void OnSelect(BaseEventData e){selected=true;Apply();}
        public void OnDeselect(BaseEventData e){selected=pressed=false;Apply();}
        private void OnDisable(){hovered=pressed=selected=false;RestoreCard();Apply();}
        private void OnApplicationFocus(bool value){if(!value){hovered=pressed=selected=false;RestoreCard();Apply();}}
    }
}
