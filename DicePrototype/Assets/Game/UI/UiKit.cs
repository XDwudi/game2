using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DiceCraft
{
    public static class UiKit
    {
        public static readonly Color Ink = Hex("111D22"), Panel = Hex("1B2B31"), PanelLight = Hex("24383E"),
            Paper = Hex("EBEEE7"), Muted = Hex("91AAA9"), Gold = Hex("E6BD78"), Teal = Hex("66D6BE"), Red = Hex("F27176"), Blue = Hex("6DB7F4");
        public static TMP_FontAsset Font;
        public static Color Hex(string s) { ColorUtility.TryParseHtmlString("#" + s, out Color c); return c; }
        public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); return r;
        }
        public static RectTransform Box(Transform p, string name, float x, float y, float w, float h, Color color, bool raycast = false)
        {
            var r = Rect(p, name, x, y, w, h); var image = r.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = raycast; return r;
        }
        public static TextMeshProUGUI Text(Transform p, string name, string value, float x, float y, float w, float h, float size = 24, Color? color = null, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var r = Rect(p, name, x, y, w, h); var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Font; t.text = value; t.fontSize = size; t.color = color ?? Paper; t.alignment = align;
            t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Overflow; return t;
        }
        public static UnityEngine.UI.Button Button(Transform p, string name, string value, float x, float y, float w, float h, Action click, bool enabled = true, bool primary = false)
        {
            var r = Box(p, name, x, y, w, h, primary ? Gold : PanelLight, true); var b = r.gameObject.AddComponent<UnityEngine.UI.Button>();
            var cs = b.colors; cs.normalColor = Color.white; cs.highlightedColor = new Color(1.12f,1.12f,1.12f); cs.pressedColor = new Color(.72f,.8f,.8f); cs.disabledColor = new Color(.55f,.55f,.55f,.65f); b.colors = cs;
            b.interactable = enabled; b.onClick.AddListener(() => click?.Invoke());
            var t = Text(r, "Label", value, 8, 4, w-16, h-8, 23, primary ? Ink : Paper, TextAlignmentOptions.Center);
            return b;
        }
        public static TMP_InputField Input(Transform p, string name, string placeholder, string initial, float x,float y,float w,float h)
        {
            var r = Box(p, name,x,y,w,h,PanelLight,true); var field = r.gameObject.AddComponent<TMP_InputField>();
            var viewport = Rect(r,"Viewport",16,6,w-32,h-12); viewport.gameObject.AddComponent<RectMask2D>();
            var t = Text(viewport,"Text",initial,0,0,w-32,h-12,26,Paper,TextAlignmentOptions.MidlineLeft);
            var ph = Text(viewport,"Placeholder",placeholder,0,0,w-32,h-12,24,Muted,TextAlignmentOptions.MidlineLeft);
            field.textViewport=viewport; field.textComponent=t; field.placeholder=ph; field.text=initial; field.characterLimit=10;
            field.contentType=TMP_InputField.ContentType.IntegerNumber; return field;
        }
        public static RectTransform Scroll(Transform p,string name,float x,float y,float w,float h,float contentHeight)
        {
            var root=Rect(p,name,x,y,w,h); var scroll=root.gameObject.AddComponent<ScrollRect>();
            var vp=Box(root,"Viewport",0,0,w,h,new Color(0,0,0,.01f),true); vp.gameObject.AddComponent<Mask>().showMaskGraphic=false;
            var content=Rect(vp,"Content",0,0,w-18,contentHeight); scroll.viewport=vp; scroll.content=content;
            scroll.horizontal=false; scroll.vertical=true; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=50;
            return content;
        }
        public static void Clear(Transform parent)
        {
            var selected=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            if(selected!=null&&selected.transform.IsChildOf(parent))EventSystem.current.SetSelectedGameObject(null);
            for(int i=parent.childCount-1;i>=0;i--) { var go=parent.GetChild(i).gameObject; go.SetActive(false); UnityEngine.Object.Destroy(go); }
        }
    }

    public sealed class DiePointer : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Action<PointerEventData> clicked, dragStart, dragged, dragEnd;
        public void OnPointerClick(PointerEventData e) { if(!e.dragging) clicked?.Invoke(e); }
        public void OnBeginDrag(PointerEventData e) { dragStart?.Invoke(e); }
        public void OnDrag(PointerEventData e) { dragged?.Invoke(e); }
        public void OnEndDrag(PointerEventData e) { dragEnd?.Invoke(e); }
    }
}
