using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private sealed class Toast07
        {
            public string text;public bool error;public float visibleSeconds;public CanvasGroup view;
        }
        private readonly List<Toast07> toasts07=new List<Toast07>();
        private readonly List<KeyValuePair<CanvasGroup,float>> toastAvoidedLabels07=new List<KeyValuePair<CanvasGroup,float>>();
        private RectTransform toastLayer07;
        private string toastContext07="";
        private bool toastDirty07;
        private bool toastApplicationPaused07;
        private int toastLastDrawFrame07=-1;

        private void QueueNotice07(string text,bool error=false)
        {
            noticeText07=text??"";
            if(string.IsNullOrEmpty(noticeText07)){toasts07.Clear();HideNotices07();toastDirty07=false;return;}
            // Keep the original text for existing save-error copy/detail handlers.
            string shown=noticeText07;
            if(shown=="独立练习：目标50分，固定4红、4红、2蓝、3蓝、6蓝。不会改变正式挑战存档。")
                shown="目标50分：4红4红2蓝3蓝6蓝\n独立固定骰练习，不改正式存档";
            var duplicate=toasts07.FirstOrDefault(t=>t.text==shown&&t.error==error);
            if(duplicate!=null)duplicate.visibleSeconds=0;
            else toasts07.Add(new Toast07{text=shown,error=error});
            toastDirty07=true;
        }
        private string NoticeContext07()=>string.Join("/",inMenu,modal,debugPanel,IsBusy,Session?.HasPendingSave??false,
            guideInline07,!string.IsNullOrEmpty(boundaryTip07),draggedDie07!=null,IsControllerDragging,Screen.width,Screen.height,textScale07,
            ViewState?.phase??"menu",ViewState?.actionSequence??0,toastApplicationPaused07);
        private void PauseNotices07(bool paused){toastApplicationPaused07=paused;toastDirty07=true;}
        private void HideNotices07()
        {
            foreach(var item in toasts07)item.view=null;
            if(toastLayer07!=null)toastLayer07.gameObject.SetActive(false);
            // Restoring alpha never reactivates an obsolete label after UiKit.Clear/Rent.
            foreach(var label in toastAvoidedLabels07)if(label.Key!=null)label.Key.alpha=label.Value;
            toastAvoidedLabels07.Clear();
        }
        private void DrawToasts07()
        {
            HideNotices07();toastContext07=NoticeContext07();toastDirty07=false;toastLastDrawFrame07=Time.frameCount;
            if(toasts07.Count==0||Session?.HasPendingSave==true||toastApplicationPaused07)return;
            bool paused=modal||draggedDie07!=null||IsControllerDragging;
            var available=toasts07.Where(t=>t.error||!paused&&!IsBusy&&!guideInline07&&string.IsNullOrEmpty(boundaryTip07)).OrderByDescending(t=>t.error).ToArray();
            if(available.Length==0)return;
            // Existing modal error pages own persistence failures. Ordinary pause/settings
            // suspend an already visible notice; rejected debug actions use their existing status band.
            if(modal&&!debugPanel)return;
            if(toastLayer07==null)
            {
                toastLayer07=UiKit.Rect(canvas.transform,"ToastLayer07",0,0,1920,1080);
                var group=UiKit.Component<CanvasGroup>(toastLayer07.gameObject);group.blocksRaycasts=true;group.interactable=true;
            }
            toastLayer07.gameObject.SetActive(true);toastLayer07.SetAsLastSibling();UiKit.Clear(toastLayer07);
            var measure=UiKit.Text(toastLayer07,"Measure","",0,0,456,2000,20,Color.clear);
            var blockers=ToastBlockers07();var occupied=new List<Rect>();int drawn=0;
            foreach(var item in available)
            {
                measure.rectTransform.sizeDelta=new Vector2(456,2000);measure.text=item.text;measure.ForceMeshUpdate();
                bool details=measure.textInfo.lineCount>2;
                float buttonHeight=Mathf.Max(60,40/Mathf.Max(.1f,canvas.scaleFactor));
                float buttonWidth=Mathf.Max(92,40/Mathf.Max(.1f,canvas.scaleFactor));
                float textWidth=details?444-buttonWidth:456;
                string shown=details?ToastSummary07(measure,item.text,textWidth,2):item.text;
                float padding=details?8:10;
                float height=Mathf.Ceil(measure.GetPreferredValues(shown,textWidth,float.PositiveInfinity).y+padding*2);
                if(details)height=Mathf.Max(height,buttonHeight);
                bool previewFallback;Rect? selected=FindToastSpace07(height,blockers,occupied,drawn,out previewFallback);
                if(!selected.HasValue)
                {
                    // A busy full table can fit a single-line summary beside the same
                    // explicit details target. Lack of whitespace never opens a modal.
                    details=true;padding=8;textWidth=444-buttonWidth;
                    shown=ToastSummary07(measure,item.text,textWidth,1);
                    height=Mathf.Max(Mathf.Ceil(measure.GetPreferredValues(shown,textWidth,float.PositiveInfinity).y+padding*2),buttonHeight);
                    selected=FindToastSpace07(height,blockers,occupied,drawn,out previewFallback);
                }
                if(!selected.HasValue)continue; // Retain text and visible-time budget until a clear region is available.
                Rect area=selected.Value;var panel=UiKit.Frame(toastLayer07,drawn==0?"Toast":"ToastSecond",area.x,area.y,480,height,UiKit.Paper,item.error?UiKit.Red:UiKit.Ink);
                if(previewFallback){var shadow=panel.Find("Shadow");if(shadow!=null)shadow.gameObject.SetActive(false);}
                var text=UiKit.Text(panel,"Text",shown,12,padding,textWidth,height-padding*2,20,UiKit.Ink);text.overflowMode=TextOverflowModes.Overflow;
                if(details)
                {
                    var selectedItem=item;
                    var open=UiKit.ActionButton(panel,drawn==0?"ToastDetails07":"ToastDetailsSecond07","详情",480-buttonWidth-12,(height-buttonHeight)*.5f,buttonWidth,buttonHeight,()=>
                    {
                        toasts07.Remove(selectedItem);toastDirty07=true;
                        ShowNoticeDetails07(selectedItem.text,selectedItem.error);
                    },UiKit.Paper);
                    var label=open.GetComponentInChildren<TextMeshProUGUI>();label.fontSize=20*UiKit.FontScale;label.textWrappingMode=TextWrappingModes.NoWrap;
                    // Keep keyboard focus inside this compact target, including
                    // the 62px diagnostic score band above the factor buttons.
                    var focus=open.transform.Find("Focus") as RectTransform;
                    if(focus!=null){focus.anchoredPosition=new Vector2(2,-2);focus.localScale=new Vector3((buttonWidth-4)/(buttonWidth+6),(buttonHeight-4)/(buttonHeight+6),1);}
                }
                item.view=UiKit.Component<CanvasGroup>(panel.gameObject);item.view.alpha=Mathf.Clamp01((2.2f-item.visibleSeconds)/.2f);item.view.blocksRaycasts=details;item.view.interactable=details;
                occupied.Add(area);AvoidToastLabels07(area);drawn++;
                if(previewFallback||drawn==2)break;
            }
            measure.gameObject.SetActive(false);
        }
        private static string ToastSummary07(TMP_Text measure,string complete,float width,int maxLines)
        {
            measure.rectTransform.sizeDelta=new Vector2(width,2000);
            // Trim at text-element boundaries, preserve the original reason prefix,
            // and make shortening explicit. The full original remains in Toast07.
            int[] starts=System.Globalization.StringInfo.ParseCombiningCharacters(complete);
            for(int keep=starts.Length;keep>0;keep--)
            {
                string candidate=keep==starts.Length?complete:complete.Substring(0,starts[keep]).TrimEnd()+"…";
                measure.text=candidate;measure.ForceMeshUpdate();
                if(measure.textInfo.lineCount<=maxLines)return candidate;
            }
            return "详情"; // Only reachable for an unsupported viewport narrower than one glyph.
        }
        private Rect? FindToastSpace07(float height,List<Rect> blockers,List<Rect> occupied,int drawn,out bool previewFallback)
        {
            previewFallback=false;
            foreach(var point in ToastCandidates07(height))
            {
                var rect=new Rect(point.x,point.y,480,height);
                if(rect.xMin<24||rect.xMax>1896||rect.yMin<24||rect.yMax>1056)continue;
                if(occupied.Any(o=>ToastOverlap07(rect,o,12))||blockers.Any(b=>ToastOverlap07(rect,b,4)))continue;
                return rect;
            }
            bool diagnosticOverflow=IsDebugWorkspace&&(ViewState.dice.Count>10||ViewState.controllers.Count>10||ViewState.stage.capacity>8);
            if(drawn==0&&diagnosticOverflow&&!modal)
            {
                // Existing explicit 32-instance diagnostic exception: only its
                // read-only score preview can yield, never dice or main actions.
                var preview=new Rect(1408,Compact07?282:288,480,height);
                // A details hit target must also leave the six multiplier buttons
                // unobscured. Retry as a one-line summary when this band is too short.
                if(!main.GetComponentsInChildren<Selectable>(false).Any(control=>ToastOverlap07(preview,ToastRect07((RectTransform)control.transform),0)))
                {previewFallback=true;return preview;}
            }
            return null;
        }
        private void TickToasts07()
        {
            if(toasts07.Count==0){if(toastLayer07!=null&&toastLayer07.gameObject.activeSelf)HideNotices07();return;}
            if(toastDirty07||toastContext07!=NoticeContext07())DrawToasts07();
            bool changed=false;
            foreach(var item in toasts07.ToArray())
            {
                if(item.view==null||!item.view.gameObject.activeInHierarchy||Time.frameCount==toastLastDrawFrame07)continue;
                // Count only actual visible unscaled time. Pauses/hidden transactions consume none.
                item.visibleSeconds+=Time.unscaledDeltaTime;item.view.alpha=Mathf.Clamp01((2.2f-item.visibleSeconds)/.2f);
                if(item.visibleSeconds>=2.2f){toasts07.Remove(item);changed=true;}
            }
            if(changed){if(toasts07.Count==0)noticeText07="";DrawToasts07();}
        }
        private void ShowNoticeDetails07(string text,bool error)
        {
            if(Session?.HasPendingSave==true)return;
            HideNotices07();var p=Art07Modal(error?"操作未完成 · 完整说明":"完整提示",480,180,960,720);
            var list=UiKit.Scroll(p,"NoticeDetails",24,104,912,568,568);float y=0;
            Art07DetailSection(list,"NoticeDetail",error?"原因":"说明",text,ref y,880);list.sizeDelta=new Vector2(list.sizeDelta.x,Math.Max(568,y+16));
        }
        private IEnumerable<Vector2> ToastCandidates07(float height)
        {
            if(debugPanel&&modal){yield return new Vector2(1392,980-height);yield break;}
            if(inMenu){yield return new Vector2(120,1000-height);yield return new Vector2(616,1000-height);yield break;}
            if(ViewState?.phase=="Shop")
            {
                // v0.73: first the owned strip (its captions yield), then the title row above the shelves.
                yield return new Vector2(392,1048-height);yield return new Vector2(888,1048-height);
                yield return new Vector2(600,24);yield return new Vector2(1096,24);yield break;
            }
            if(Compact07)
            {
                bool lowerRowEmpty=(ViewState?.hand?.orderedLockedIds.Count??0)<=4;
                if(lowerRowEmpty){yield return new Vector2(648,921-height);yield return new Vector2(1140,921-height);}
                yield return new Vector2(384,653-height);yield return new Vector2(1408,653-height);
                if(!lowerRowEmpty){yield return new Vector2(648,921-height);yield return new Vector2(1140,921-height);}
            }
            else
            {
                yield return new Vector2(1408,928-height);yield return new Vector2(912,928-height);yield return new Vector2(416,928-height);
                yield return new Vector2(384,620);yield return new Vector2(1408,620);
                yield return new Vector2(1408,784-height);yield return new Vector2(384,748-height);
            }
            // Stage preview/results also get actual whitespace rather than a fixed overlay.
            if(ViewState?.hand==null)
                for(float y=936-height;y>=480;y-=80)for(float x=392;x<=1408;x+=496)yield return new Vector2(x,y);
        }
        private static bool ToastOverlap07(Rect a,Rect b,float gap)=>a.xMin<b.xMax+gap&&a.xMax>b.xMin-gap&&a.yMin<b.yMax+gap&&a.yMax>b.yMin-gap;
        private Rect ToastRect07(RectTransform target)
        {
            var corners=new Vector3[4];target.GetWorldCorners(corners);var points=corners.Select(p=>((RectTransform)canvas.transform).InverseTransformPoint(p)).ToArray();
            // The canvas pivot is not assumed to be top-left; convert through its actual rect.
            var root=((RectTransform)canvas.transform).rect;
            return Rect.MinMaxRect(points.Min(p=>p.x)-root.xMin,root.yMax-points.Max(p=>p.y),points.Max(p=>p.x)-root.xMin,root.yMax-points.Min(p=>p.y));
        }
        private static bool ToastSecondaryLabel07(string name)=>name.StartsWith("EmptySlot",StringComparison.Ordinal)||name=="LockRule"||name=="ShopHint"||name=="DebugStatus"||name=="OwnedLabel"||name=="OwnedItemsTitle"||name=="OwnedDiceMore";
        private Rect ToastClip07(Rect rect,Transform target)
        {
            Rect Intersect(Rect a,Rect b)
            {
                float x=Mathf.Max(a.xMin,b.xMin),y=Mathf.Max(a.yMin,b.yMin);
                return Rect.MinMaxRect(x,y,Mathf.Max(x,Mathf.Min(a.xMax,b.xMax)),Mathf.Max(y,Mathf.Min(a.yMax,b.yMax)));
            }
            foreach(var mask in target.GetComponentsInParent<Mask>(false))if(mask.isActiveAndEnabled)rect=Intersect(rect,ToastRect07(mask.rectTransform));
            foreach(var mask in target.GetComponentsInParent<RectMask2D>(false))if(mask.isActiveAndEnabled)rect=Intersect(rect,ToastRect07(mask.rectTransform));
            return Intersect(rect,new Rect(0,0,1920,1080));
        }
        /// <summary>Verification aid: the screen elements that block a notice of this height at each candidate position.</summary>
        public string[] NoticeBlockerReport073(float height)
        {
            var names=new List<string>();var blockers=ToastBlockers07(names);var lines=new List<string>();
            foreach(var point in ToastCandidates07(height))
            {
                var rect=new Rect(point.x,point.y,480,height);var hits=new List<string>();
                for(int i=0;i<blockers.Count;i++)if(ToastOverlap07(rect,blockers[i],4))hits.Add(names[i]+"@"+blockers[i]);
                lines.Add(rect+": "+(hits.Count==0?"free":string.Join(" | ",hits.Take(6))));
            }
            return lines.ToArray();
        }
        private List<Rect> ToastBlockers07(List<string> names=null)
        {
            Transform source=modal?overlay:main;var result=new List<Rect>();
            void Add(Rect rect,Transform target){rect=ToastClip07(rect,target);if(rect.width>0&&rect.height>0){result.Add(rect);names?.Add(target.name);}}
            foreach(var selectable in source.GetComponentsInChildren<Selectable>(false))Add(ToastRect07((RectTransform)selectable.transform),selectable.transform);
            foreach(var pointer in source.GetComponentsInChildren<DiePointer>(false))
            {var rect=ToastRect07((RectTransform)pointer.transform);rect.xMin-=16;rect.xMax+=16;rect.yMin-=16;rect.yMax+=16;Add(rect,pointer.transform);}
            foreach(var text in source.GetComponentsInChildren<TextMeshProUGUI>(false))
            {
                if(string.IsNullOrWhiteSpace(text.text)||ToastSecondaryLabel07(text.name))continue;
                text.ForceMeshUpdate();var bounds=text.textBounds;var a=text.transform.TransformPoint(bounds.min);var b=text.transform.TransformPoint(bounds.max);
                var root=(RectTransform)canvas.transform;var x=root.InverseTransformPoint(a);var y=root.InverseTransformPoint(b);
                Add(Rect.MinMaxRect(Mathf.Min(x.x,y.x)-root.rect.xMin,root.rect.yMax-Mathf.Max(x.y,y.y),Mathf.Max(x.x,y.x)-root.rect.xMin,root.rect.yMax-Mathf.Min(x.y,y.y)),text.transform);
            }
            return result;
        }
        private void AvoidToastLabels07(Rect area)
        {
            Transform source=modal?overlay:main;
            foreach(var label in source.GetComponentsInChildren<TextMeshProUGUI>(false))
                if(ToastSecondaryLabel07(label.name)&&ToastOverlap07(area,ToastRect07(label.rectTransform),0))
                {
                    var group=UiKit.Component<CanvasGroup>(label.gameObject);
                    if(!toastAvoidedLabels07.Any(old=>old.Key==group))toastAvoidedLabels07.Add(new KeyValuePair<CanvasGroup,float>(group,group.alpha));
                    group.alpha=0;
                }
        }
    }
}
