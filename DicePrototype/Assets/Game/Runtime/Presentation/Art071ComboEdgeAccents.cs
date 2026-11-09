using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DiceCraft.Presentation
{
    /// <summary>One local print ring, two short arcs, and reused gold stars.
    /// Prewarmed once; read-only text geometry excludes every score/button ink area.
    /// No particles, gameplay state, RNG, material creation, or unbounded allocations.</summary>
    public sealed class Art071ComboEdgeAccents : MonoBehaviour
    {
        private Art07PrefabView owner;
        private readonly UnityEngine.UI.Image[] marks=new UnityEngine.UI.Image[3];
        private readonly Vector2[] centres=new Vector2[8], sizes=new Vector2[8];
        private readonly bool[] placed=new bool[8];
        private readonly List<Rect> forbidden=new List<Rect>(20),reserved=new List<Rect>(8);
        private Vector2 extent;
        private bool configured,suppressed;
        private static readonly Color Gold=new Color(.953f,.729f,.345f,1);
        public void Initialize(Art07PrefabView view)
        {
            if(owner!=null)return;owner=view;
            for(int i=0;i<marks.Length;i++)
            {
                var go=new GameObject(i==0?"ComboLocalRing071":"ComboGoldArc071_"+i,typeof(RectTransform),typeof(CanvasRenderer),typeof(UnityEngine.UI.Image));
                var rect=(RectTransform)go.transform;rect.SetParent(view.visualRoot,false);
                rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=Vector2.one*.5f;
                var image=go.GetComponent<UnityEngine.UI.Image>();image.raycastTarget=false;image.preserveAspect=true;image.color=Gold;
                marks[i]=image;go.SetActive(false);
            }
        }
        public void Configure(Vector2 area,bool lowQuality)
        {
            extent=area;suppressed=lowQuality;configured=true;forbidden.Clear();reserved.Clear();
            System.Array.Clear(placed,0,placed.Length);
            // Configure is called after the real score text/button hierarchy exists.
            // Reserve the complete visible string bounds, including TMP fallback glyphs.
            var score=owner.transform.parent;
            foreach(var text in score.GetComponentsInChildren<TMP_Text>(false))
            {
                if(string.IsNullOrEmpty(text.text))continue;text.ForceMeshUpdate();
                if(text.textInfo.characterCount==0)continue;
                var bounds=text.textBounds;if(bounds.size.x<=0||bounds.size.y<=0)continue;
                var local=new Rect(bounds.min.x,bounds.min.y,bounds.size.x,bounds.size.y);
                if(text.name=="FinalScore")
                {
                    // Reserve the existing peak around the actual TMP pivot; the
                    // final number can grow after this configuration is captured.
                    float peak=ScorePresentationTier.ScalePeak(owner.BurstLevel,new PresentationOptions());
                    local=Rect.MinMaxRect(Mathf.Min(local.xMin,local.xMin*peak),Mathf.Min(local.yMin,local.yMin*peak),Mathf.Max(local.xMax,local.xMax*peak),Mathf.Max(local.yMax,local.yMax*peak));
                }
                var r=InVisual(text.transform,local);
                forbidden.Add(Pad(r,4));
            }
            foreach(var button in score.GetComponentsInChildren<UnityEngine.UI.Button>(false))
                forbidden.Add(Pad(InVisual(button.transform,((RectTransform)button.transform).rect),3));
            // The legacy L5 flame remains at its tested position and 24–28px ink height.
            // Conservatively reserve its full 238px design width, not just the narrow alpha.
            if(owner.BurstLevel==5)forbidden.Add(new Rect(area.y<110?368:area.x*.35f,area.y-49,238,38));
            float diameter=area.y<110?28:42;
            sizes[0]=Vector2.one*diameter;sizes[1]=sizes[2]=Vector2.one*(area.y<110?32:46);
            for(int i=3;i<sizes.Length;i++)sizes[i]=Vector2.one*14;
            marks[0].sprite=owner.scoreRing071;marks[1].sprite=marks[2].sprite=owner.scoreArc071;
            for(int i=0;i<placed.Length;i++)
            {
                bool needed=i==0?owner.BurstLevel>=3:i<3?owner.BurstLevel>=4:owner.BurstLevel==5;
                if(!needed)continue;
                placed[i]=TryEdgeSlot(sizes[i],i,out centres[i]);
            }
            Evaluate(0);
        }
        // Positive-down coordinates relative to visualRoot. Whole maximum animated
        // quads must fit a perimeter lane and avoid text, controls, flame and other marks.
        private bool TryEdgeSlot(Vector2 size,int ordinal,out Vector2 centre)
        {
            float margin=3,halfX=size.x*.5f,halfY=size.y*.5f;
            for(int side=0;side<4;side++)for(int step=0;step<24;step++)
            {
                float u=((step+ordinal*5)%24+.5f)/24;
                Vector2 point=side==0?new Vector2(Mathf.Lerp(halfX+margin,extent.x-halfX-margin,u),extent.y-halfY-margin):
                    side==1?new Vector2(Mathf.Lerp(halfX+margin,extent.x-halfX-margin,u),halfY+margin):
                    side==2?new Vector2(halfX+margin,Mathf.Lerp(halfY+margin,extent.y-halfY-margin,u)):
                    new Vector2(extent.x-halfX-margin,Mathf.Lerp(halfY+margin,extent.y-halfY-margin,u));
                var bounds=new Rect(point-size*.5f,size);
                if(bounds.xMin<0||bounds.yMin<0||bounds.xMax>extent.x||bounds.yMax>extent.y)continue;
                bool clear=true;foreach(var r in forbidden)if(r.Overlaps(bounds)){clear=false;break;}
                if(clear)foreach(var r in reserved)if(r.Overlaps(bounds)){clear=false;break;}
                if(!clear)continue;reserved.Add(Pad(bounds,4));centre=point;return true;
            }
            centre=Vector2.zero;return false; // UI readability wins if a future layout removes all space.
        }
        public void Evaluate(float normalized)
        {
            if(!configured)return;float time=Mathf.Clamp01(normalized);
            bool animate=!owner.ReducedMotion&&!suppressed;
            // One burst, fully quiet by 65% of the existing final-score beat.
            float opacity=time<.08f?time/.08f:1-Mathf.Clamp01((time-.08f)/.57f);
            for(int i=0;i<marks.Length;i++)
            {
                bool visible=animate&&owner.BurstLevel>=(i==0?3:4)&&placed[i]&&marks[i].sprite!=null&&time<.65f;
                marks[i].gameObject.SetActive(visible);if(!visible)continue;
                float growth=i==0?Mathf.Lerp(.62f,1,1-Mathf.Pow(1-Mathf.Min(1,time/.65f),3)):Mathf.Lerp(.82f,1,Mathf.Min(1,time/.4f));
                var r=marks[i].rectTransform;r.anchoredPosition=new Vector2(centres[i].x,-centres[i].y);r.sizeDelta=sizes[i];r.localScale=Vector3.one*growth;r.localRotation=Quaternion.identity;
                marks[i].color=new Color(Gold.r,Gold.g,Gold.b,opacity*(i==0?.8f:.95f));
            }
            if(owner.ReducedMotion)return; // Existing static tier markers remain untouched.
            for(int i=0;i<owner.burstStaticStars.Length;i++)
            {
                var star=owner.burstStaticStars[i];if(star==null)continue;int slot=i+3;
                bool visible=animate&&owner.BurstLevel==5&&slot<placed.Length&&placed[slot]&&time<.65f;
                star.SetActive(visible);if(!visible)continue;
                var r=(RectTransform)star.transform;r.pivot=Vector2.one*.5f;r.anchoredPosition=new Vector2(centres[slot].x,-centres[slot].y);
                r.sizeDelta=sizes[slot];r.localScale=Vector3.one*Mathf.Lerp(.55f,1,Mathf.Min(1,time/.25f));r.localRotation=Quaternion.identity;
                var image=star.GetComponent<UnityEngine.UI.Image>();if(image!=null){image.preserveAspect=true;image.raycastTarget=false;image.color=new Color(Gold.r,Gold.g,Gold.b,opacity);}
            }
        }
        public void ResetForPool()
        {
            configured=false;forbidden.Clear();reserved.Clear();System.Array.Clear(placed,0,placed.Length);
            foreach(var mark in marks)if(mark!=null){mark.gameObject.SetActive(false);mark.rectTransform.localScale=Vector3.one;mark.color=Gold;}
        }
        private Rect InVisual(Transform source,Rect rect)
        {
            var a=owner.visualRoot.InverseTransformPoint(source.TransformPoint(new Vector3(rect.xMin,rect.yMin)));
            var b=owner.visualRoot.InverseTransformPoint(source.TransformPoint(new Vector3(rect.xMax,rect.yMax)));
            return Rect.MinMaxRect(Mathf.Min(a.x,b.x),-Mathf.Max(a.y,b.y),Mathf.Max(a.x,b.x),-Mathf.Min(a.y,b.y));
        }
        private static Rect Pad(Rect r,float amount)=>Rect.MinMaxRect(r.xMin-amount,r.yMin-amount,r.xMax+amount,r.yMax+amount);
    }
}
