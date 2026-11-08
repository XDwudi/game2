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
        private float displacement,cardScale=1;
        private readonly System.Collections.Generic.List<RectTransform> cardVisuals=new System.Collections.Generic.List<RectTransform>();
        public void SetSurface(int value){RestoreCard();surface=value;Apply();}
        public void Configure(Button value,GameObject ring)
        {
            RestoreCard();surface=0;displacement=0;
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
        private void Update()
        {
            if(surface!=1)return;
            bool lifted=button!=null&&button.IsInteractable()&&hovered&&!pressed&&!UiKit.ReducedMotion;
            float target=lifted?8:0,targetScale=lifted?1.03f:1;
            float t=1-Mathf.Exp(-Time.unscaledDeltaTime/ .045f);
            float next=Mathf.Lerp(displacement,target,t),scale=Mathf.Lerp(cardScale,targetScale,t);
            if(cardVisuals.Count==0)foreach(Transform child in transform)if(child.gameObject.activeSelf&&child.gameObject!=focus)cardVisuals.Add((RectTransform)child);
            TransformCardVisuals(next,scale);
            displacement=next;cardScale=scale;
        }
        private void TransformCardVisuals(float next,float scale)
        {
            // The transparent hit root stays fixed. Every painted child shares one visual transform.
            var root=(RectTransform)transform;Vector2 center=new Vector2(root.rect.width*.5f,-root.rect.height*.5f);
            foreach(var r in cardVisuals)if(r!=null)
            {
                Vector2 original=(r.anchoredPosition-center-Vector2.up*displacement)/cardScale;
                r.anchoredPosition=center+original*scale+Vector2.up*next;r.localScale*=scale/cardScale;
            }
        }
        private void RestoreCard()
        {
            if(surface==1)TransformCardVisuals(0,1);
            displacement=0;cardScale=1;cardVisuals.Clear();
        }
        public void OnPointerEnter(PointerEventData e){hovered=true;if(button!=null&&button.IsInteractable())UiKit.Feedback?.Invoke("SFX_UI_HOVER");Apply();}
        public void OnPointerExit(PointerEventData e){hovered=pressed=false;Apply();}
        public void OnPointerDown(PointerEventData e){if(e.button!=PointerEventData.InputButton.Left)return;pressed=true;if(button!=null&&button.IsInteractable())UiKit.Feedback?.Invoke("SFX_UI_PRESS");Apply();}
        public void OnPointerUp(PointerEventData e){pressed=false;Apply();}
        public void OnSelect(BaseEventData e){selected=true;Apply();}
        public void OnDeselect(BaseEventData e){selected=pressed=false;Apply();}
        private void OnDisable(){hovered=pressed=selected=false;RestoreCard();Apply();}
        private void OnApplicationFocus(bool value){if(!value){hovered=pressed=selected=false;RestoreCard();Apply();}}
    }
}
