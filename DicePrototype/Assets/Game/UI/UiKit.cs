using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DiceCraft
{
    public static class UiKit
    {
        public static readonly Color Ink = Hex("162629"), Panel = Hex("203F43"), PanelLight = Hex("315358"),
            Paper = Hex("F4E8CB"), Muted = Hex("B8C9C5"), Gold = Hex("F3BA58"), Teal = Hex("79C99C"), Red = Hex("EC655D"), Blue = Hex("55B6D9"),
            Background=Hex("102A2D"), Felt=Hex("173E40"), TextColor=Hex("F7EED9"), Violet=Hex("AD92DB"), Danger=Hex("9E3440"), Shadow=Hex("07191C");
        /// <summary>v0.72 rc.2 tint of the quiet secondary-button outline; readable on felt and on paper.</summary>
        public static readonly Color Ghost=Hex("6A9296");
        /// <summary>v0.72 frame edge weight in reference units (matches the 2x-density ink line of the HD surfaces).</summary>
        public const float EdgeUnit=2f;
        public static float FontScale=1f;
        public static bool ReducedMotion;
        public static Action<string> Feedback;
        public static Sprite[] Templates;
        public static Sprite Template(string name)=>Templates?.FirstOrDefault(s=>s!=null&&s.name==name);
        public static TMP_FontAsset Font;
        public static Color Hex(string s) { ColorUtility.TryParseHtmlString("#" + s, out Color c); return c; }
        public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            return Rent(parent,name,"Rect",x,y,w,h);
        }
        private static RectTransform Rent(Transform parent,string name,string role,float x,float y,float w,float h)
        {
            // Pools belong to their actual parent, so scene destruction releases all references.
            // The role prevents a former text/button/container from being reused as another type.
            var pool=Component<UiElementPool>(parent.gameObject);
            string key=role+":"+name;
            if(!pool.elements.TryGetValue(key,out var r)||r==null)
            {
                r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
                pool.elements[key]=r;
            }
            // TMP owns fallback and multi-atlas child meshes; clearing them hides glyphs on pooled redraw.
            else if(role!="Text")Clear(r);
            r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
            r.localScale=Vector3.one;r.localRotation=Quaternion.identity;
            r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h);
            r.SetAsLastSibling();r.gameObject.SetActive(true);return r;
        }
        public static T Component<T>(GameObject go) where T:UnityEngine.Component
        { var existing=go.GetComponent<T>();return existing!=null?existing:go.AddComponent<T>(); }
        public static RectTransform Box(Transform p, string name, float x, float y, float w, float h, Color color, bool raycast = false)
        {
            var r = Rent(p,name,"Box",x,y,w,h); var image = Component<UnityEngine.UI.Image>(r.gameObject); image.color = color; image.raycastTarget = raycast; return r;
        }
        public static TextMeshProUGUI Text(Transform p, string name, string value, float x, float y, float w, float h, float size = 24, Color? color = null, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var r = Rent(p,name,"Text",x,y,w,h); var t = Component<TextMeshProUGUI>(r.gameObject);
            t.font = Font; t.fontStyle = FontStyles.Normal; t.fontWeight = FontWeight.Regular;
            t.text = value; t.fontSize = size*FontScale; t.color = color ?? TextColor; t.alignment = align;
            t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Overflow; return t;
        }
        public static UnityEngine.UI.Button Button(Transform p, string name, string value, float x, float y, float w, float h, Action click, bool enabled = true, bool primary = false)
        {
            var r = Rent(p,name,"Button",x,y,w,h);var image=Component<UnityEngine.UI.Image>(r.gameObject);image.color=Color.clear;image.raycastTarget=true;
            var step=Box(r,"Step",2,8,w-4,h-6,enabled?(primary?Hex("B56B29"):Hex("8F453A")):Shadow);
            step.GetComponent<UnityEngine.UI.Image>().sprite=Template("ui_button_bevel");step.GetComponent<UnityEngine.UI.Image>().type=UnityEngine.UI.Image.Type.Sliced;
            var face=Box(r,"Face",0,0,w,h-7,enabled?(primary?Gold:Paper):PanelLight);
            var faceImage=face.GetComponent<UnityEngine.UI.Image>();faceImage.sprite=Template("ui_button_bevel");faceImage.type=UnityEngine.UI.Image.Type.Sliced;
            Box(r,"Lip",14,5,w-28,1,enabled?new Color(1,1,1,.35f):Color.clear);
            var b = Component<UnityEngine.UI.Button>(r.gameObject);b.onClick.RemoveAllListeners();b.targetGraphic=image;
            b.targetGraphic=face.GetComponent<UnityEngine.UI.Image>();
            var cs = b.colors; cs.normalColor = Color.white; cs.highlightedColor = new Color(1.08f,1.08f,1.08f); cs.pressedColor = new Color(.84f,.88f,.88f); cs.disabledColor = Color.white; b.colors = cs;
            b.interactable = enabled; b.onClick.AddListener(() => click?.Invoke());
            Text(r, "Label", value, 8, 0, w-16, h-7, 23, enabled ? Ink : Muted, TextAlignmentOptions.Center);
            Outline(r,"Focus",-3,-3,w+6,h+6,Paper,2);
            Component<Art07ButtonMotion>(r.gameObject).Configure(b,r.Find("Focus").gameObject);
            return b;
        }
        public static void CardSurface(UnityEngine.UI.Button button,bool compact)
        {
            var image=button.targetGraphic as UnityEngine.UI.Image;if(image==null)return;
            image.sprite=Template(compact?"ui_card_controller_compact":"ui_card_controller");image.type=UnityEngine.UI.Image.Type.Sliced;image.color=Color.white;
            var root=(RectTransform)button.transform;
            image.rectTransform.anchoredPosition=Vector2.zero;image.rectTransform.sizeDelta=root.sizeDelta;
            root.Find("Step")?.gameObject.SetActive(false);root.Find("Lip")?.gameObject.SetActive(false);
            Component<Art07ButtonMotion>(root.gameObject).SetSurface(1);
        }
        public static UnityEngine.UI.Button FlatButton(Transform p,string name,string value,float x,float y,float w,float h,Action click,bool enabled=true,bool primary=false)
        {
            var button=Button(p,name,value,x,y,w,h,click,enabled,primary);
            var root=(RectTransform)button.transform;
            root.Find("Step").gameObject.SetActive(false);root.Find("Lip").gameObject.SetActive(false);
            // v0.72 rc.2: a quiet rounded outline (or a solid rounded tab when selected) replaces the
            // square tint + hairline underline. Callers that recolour the face keep working: it only tints.
            var face=(UnityEngine.UI.Image)button.targetGraphic;var ghost=Template(primary?"ui_frame_block":"ui_button_ghost");
            face.sprite=ghost;face.type=ghost!=null?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;
            face.color=primary?PanelLight:ghost!=null?(enabled?Ghost:new Color(Ghost.r,Ghost.g,Ghost.b,.45f)):new Color(0,0,0,.08f);
            face.rectTransform.anchoredPosition=Vector2.zero;face.rectTransform.sizeDelta=new Vector2(w,h);
            var label=root.Find("Label").GetComponent<TextMeshProUGUI>();label.color=enabled?(primary?Gold:TextColor):Muted;
            label.rectTransform.sizeDelta=new Vector2(w-16,h);
            Box(root,"Divider",8,h-1,w-16,1,ghost!=null?Color.clear:PanelLight);
            Component<Art07ButtonMotion>(root.gameObject).SetSurface(2);
            return button;
        }
        public static RectTransform PaperFrame(Transform p,string name,float x,float y,float w,float h,Color fill,Color? edge=null,bool raycast=false)
        {
            var r=Box(p,name,x,y,w,h,Color.white,raycast);var image=r.GetComponent<UnityEngine.UI.Image>();
            image.sprite=Template("ui_paper_panel");image.type=UnityEngine.UI.Image.Type.Sliced;image.fillCenter=true;
            if(edge.HasValue&&edge.Value!=Ink)Outline(r,"SemanticEdge",5,5,w-10,h-10,edge.Value,2);
            return r;
        }
        public static UnityEngine.UI.Button ActionButton(Transform p,string name,string value,float x,float y,float w,float h,Action click,Color color,bool enabled=true)
        {
            var b=Button(p,name,value,x,y,w,h,click,enabled,true);
            b.targetGraphic.color=enabled?color:Panel;
            b.GetComponentInChildren<TextMeshProUGUI>().color=enabled?(color==Danger?TextColor:Ink):TextColor;
            return b;
        }
        public static RectTransform Frame(Transform p,string name,float x,float y,float w,float h,Color fill,Color? edge=null,bool raycast=false)
        {
            if(fill==Paper)return PaperFrame(p,name,x,y,w,h,fill,edge,raycast);
            // v0.72: a rounded two-unit edge from the shared HD block instead of a square hairline.
            var r=Box(p,name,x,y,w,h,edge??PanelLight,raycast);
            var block=Template("ui_frame_block");float line=block!=null&&w>=12&&h>=12?EdgeUnit:1;
            var edgeImage=r.GetComponent<UnityEngine.UI.Image>();edgeImage.sprite=block;edgeImage.type=block!=null?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;
            var inner=Box(r,"Fill",line,line,w-2*line,h-2*line,fill).GetComponent<UnityEngine.UI.Image>();
            inner.sprite=block;inner.type=block!=null?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;
            return r;
        }
        /// <summary>v0.72: give a flat frame an authored surface (empty slot, inset strip). No-op without the art.</summary>
        public static void Surface(RectTransform frame,string template,Color? tint=null)
        {
            var sprite=Template(template);var image=frame!=null?frame.GetComponent<UnityEngine.UI.Image>():null;if(sprite==null||image==null)return;
            image.sprite=sprite;image.type=UnityEngine.UI.Image.Type.Sliced;image.color=tint??Color.white;
            frame.Find("Fill")?.gameObject.SetActive(false);
        }
        /// <summary>v0.72 sunken strip behind a group of readouts. Purely decorative; never a raycast target.</summary>
        public static RectTransform Inset(Transform p,string name,float x,float y,float w,float h)
        {
            var r=Box(p,name,x,y,w,h,Template("ui_info_inset")!=null?Color.white:new Color(0,0,0,.18f));
            var image=r.GetComponent<UnityEngine.UI.Image>();image.sprite=Template("ui_info_inset");image.type=image.sprite!=null?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;
            return r;
        }
        /// <summary>v0.72 rc.2: rounded open ring (slots, trays, selection). The centre is never painted, so 3D dice show through.</summary>
        public static RectTransform Ring(Transform p,string name,float x,float y,float w,float h,Color color)
        {
            var sprite=Template("ui_ring");
            var r=Box(p,name,x,y,w,h,sprite!=null?color:Color.clear);var image=r.GetComponent<UnityEngine.UI.Image>();
            image.sprite=sprite;image.type=sprite!=null?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;image.fillCenter=false;
            if(sprite==null)Outline(r,"Edge",0,0,w,h,color,EdgeUnit);
            return r;
        }
        /// <summary>v0.72 rc.2: slim rounded meter. The fill eases to its new width; purely decorative.</summary>
        public static RectTransform Progress(Transform p,string name,float x,float y,float w,float h,float ratio,Color fill,string context)
        {
            var track=Box(p,name,x,y,w,h,Hex("07191C"));var trackImage=track.GetComponent<UnityEngine.UI.Image>();
            trackImage.sprite=Template("ui_progress_track");trackImage.type=trackImage.sprite!=null?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;
            trackImage.pixelsPerUnitMultiplier=48f/Mathf.Max(1,h);
            float target=ratio<=0?0:Mathf.Max(h,w*Mathf.Clamp01(ratio));
            var bar=Box(track,"Fill",0,0,target,h,fill);var barImage=bar.GetComponent<UnityEngine.UI.Image>();
            barImage.sprite=Template("ui_progress_fill");barImage.type=barImage.sprite!=null?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;
            barImage.pixelsPerUnitMultiplier=48f/Mathf.Max(1,h);
            Component<Art072ProgressEase>(bar.gameObject).Configure(context,target,h);
            return track;
        }
        public static void Outline(Transform p,string name,float x,float y,float w,float h,Color color,float line=2)
        {
            var r=Rect(p,name,x,y,w,h);Box(r,"Top",0,0,w,line,color);Box(r,"Bottom",0,h-line,w,line,color);
            Box(r,"Left",0,0,line,h,color);Box(r,"Right",w-line,0,line,h,color);
        }
        public static TMP_InputField Input(Transform p, string name, string placeholder, string initial, float x,float y,float w,float h)
        {
            var r = Rent(p,name,"Input",x,y,w,h);var image=Component<UnityEngine.UI.Image>(r.gameObject);image.color=PanelLight;image.raycastTarget=true;
            var field = Component<TMP_InputField>(r.gameObject);
            field.onValueChanged.RemoveAllListeners();field.onEndEdit.RemoveAllListeners();field.onSubmit.RemoveAllListeners();
            field.DeactivateInputField();field.enabled=false;
            var viewport = Rect(r,"Viewport",16,6,w-32,h-12); Component<RectMask2D>(viewport.gameObject);
            var t = Text(viewport,"Text",initial,0,0,w-32,h-12,26,Paper,TextAlignmentOptions.MidlineLeft);
            var ph = Text(viewport,"Placeholder",placeholder,0,0,w-32,h-12,24,Muted,TextAlignmentOptions.MidlineLeft);
            field.textViewport=viewport; field.textComponent=t; field.placeholder=ph; field.text=initial; field.characterLimit=10;
            field.contentType=TMP_InputField.ContentType.IntegerNumber;field.enabled=true;
            foreach(var caret in viewport.GetComponentsInChildren<TMP_SelectionCaret>(true))caret.gameObject.SetActive(true);
            field.ForceLabelUpdate();return field;
        }
        public static RectTransform Scroll(Transform p,string name,float x,float y,float w,float h,float contentHeight)
        {
            var root=Rent(p,name,"Scroll",x,y,w,h); var scroll=Component<ScrollRect>(root.gameObject);
            var vp=Box(root,"Viewport",0,0,w,h,new Color(0,0,0,.01f),true); Component<Mask>(vp.gameObject).showMaskGraphic=false;
            var content=Rect(vp,"Content",0,0,w-18,contentHeight); scroll.viewport=vp; scroll.content=content;
            scroll.horizontal=false; scroll.vertical=true; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=50;
            scroll.StopMovement();scroll.verticalNormalizedPosition=1;content.anchoredPosition=Vector2.zero;
            var track=Box(root,"ScrollTrack",w-12,4,8,h-8,Background);
            var thumb=Box(track,"ScrollThumb",0,0,8,h-8,Muted);
            Component<Art07ScrollIndicator>(root.gameObject).Bind(scroll,track,thumb);
            return content;
        }
        public static void Clear(Transform parent)
        {
            var selected=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            if(selected!=null&&selected.transform.IsChildOf(parent))EventSystem.current.SetSelectedGameObject(null);
            for(int i=parent.childCount-1;i>=0;i--) parent.GetChild(i).gameObject.SetActive(false);
        }
        public static void Release(Transform parent)
        {
            Clear(parent);
            var pool=parent.GetComponent<UiElementPool>();if(pool!=null)pool.elements.Clear();
            for(int i=parent.childCount-1;i>=0;i--) UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
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
