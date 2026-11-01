using UnityEngine;
using UnityEngine.UI;

namespace DiceCraft
{
    // A passive position marker accompanies wheel/touch dragging without covering text hit areas.
    public sealed class Art07ScrollIndicator : MonoBehaviour
    {
        private ScrollRect scroll;
        private RectTransform track,thumb;
        public void Bind(ScrollRect owner,RectTransform rail,RectTransform handle)
        {scroll=owner;track=rail;thumb=handle;UpdatePosition();}
        private void LateUpdate()=>UpdatePosition();
        private void UpdatePosition()
        {
            if(scroll==null||track==null||thumb==null||scroll.content==null||scroll.viewport==null)return;
            float visible=scroll.viewport.rect.height,total=scroll.content.rect.height;
            bool overflowing=total>visible+1;
            if(track.gameObject.activeSelf!=overflowing)track.gameObject.SetActive(overflowing);
            if(!overflowing)return;
            float height=track.rect.height,handleHeight=Mathf.Clamp(height*visible/total,18,height);
            thumb.sizeDelta=new Vector2(8,handleHeight);
            thumb.anchoredPosition=new Vector2(0,-(height-handleHeight)*(1-Mathf.Clamp01(scroll.verticalNormalizedPosition)));
        }
    }
}
