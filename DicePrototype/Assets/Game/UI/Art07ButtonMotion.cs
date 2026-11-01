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
        public void Configure(Button value,GameObject ring)
        {
            button=value;focus=ring;face=transform.Find("Face") as RectTransform;lip=transform.Find("Lip") as RectTransform;label=transform.Find("Label") as RectTransform;
            faceOrigin=face.anchoredPosition;lipOrigin=lip.anchoredPosition;labelOrigin=label.anchoredPosition;
            hovered=pressed=selected=false;Apply();
        }
        private void Apply()
        {
            bool enabled=button!=null&&button.IsInteractable();
            float dy=enabled&&pressed?-4:enabled&&hovered&&!UiKit.ReducedMotion?2:0;
            if(face!=null)face.anchoredPosition=faceOrigin+Vector2.up*dy;
            if(lip!=null)lip.anchoredPosition=lipOrigin+Vector2.up*dy;
            if(label!=null)label.anchoredPosition=labelOrigin+Vector2.up*dy;
            if(focus!=null)focus.SetActive(enabled&&selected);
        }
        public void OnPointerEnter(PointerEventData e){hovered=true;if(button!=null&&button.IsInteractable())UiKit.Feedback?.Invoke("SFX_UI_HOVER");Apply();}
        public void OnPointerExit(PointerEventData e){hovered=pressed=false;Apply();}
        public void OnPointerDown(PointerEventData e){if(e.button!=PointerEventData.InputButton.Left)return;pressed=true;if(button!=null&&button.IsInteractable())UiKit.Feedback?.Invoke("SFX_UI_PRESS");Apply();}
        public void OnPointerUp(PointerEventData e){pressed=false;Apply();}
        public void OnSelect(BaseEventData e){selected=true;Apply();}
        public void OnDeselect(BaseEventData e){selected=pressed=false;Apply();}
        private void OnDisable(){hovered=pressed=selected=false;Apply();}
        private void OnApplicationFocus(bool value){if(!value){hovered=pressed=selected=false;Apply();}}
    }
}
