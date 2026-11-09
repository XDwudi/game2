using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DiceCraft.Presentation
{
    /// <summary>Screen-space layout of the existing Combo art. The central score and
    /// description area is transparent; chip sizes do not inherit a stretched square.</summary>
    public sealed class Art07ScoreBurstLayout : MonoBehaviour
    {
        private struct Original { public RectTransform rect; public Vector2 position,size,pivot; public Vector3 scale; public Quaternion rotation; public Image image; public bool preserveAspect; public Sprite sprite;public Color color; }
        private readonly List<Original> originals=new List<Original>();
        private readonly RectTransform[] borders=new RectTransform[32];
        private Art07PrefabView owner;
        private Art071ComboEdgeAccents edgeAccents071;
        private bool configured;
        private Vector2 extent;
        private Sprite squareChip;
        private float squareGeometryFraction;
        public bool IsConfigured=>configured;
        public void Initialize(Art07PrefabView view)
        {
            if(owner!=null)return;owner=view;
            edgeAccents071=view.GetComponent<Art071ComboEdgeAccents>();
            if(edgeAccents071==null)edgeAccents071=view.gameObject.AddComponent<Art071ComboEdgeAccents>();
            edgeAccents071.Initialize(view);
            Remember((RectTransform)view.transform);Remember(view.visualRoot);
            foreach(var layer in view.burstLayers)if(layer!=null)Remember(layer.transform as RectTransform);
            foreach(var star in view.burstStaticStars)if(star!=null)Remember(star.transform as RectTransform);
            foreach(var part in view.particles)Remember(part as RectTransform);
            // The legacy prefab's first four slots use star_spark. The committed
            // Combo contract instead calls for diamonds (L1) / square chips (L2+).
            // Reuse the square already bound to this same prefab, never load/draw.
            foreach(var part in view.particles)
            {
                var graphic=part==null?null:part.GetComponent<Image>();
                if(graphic!=null&&graphic.sprite!=null&&graphic.sprite.name.StartsWith("fx_square_chip",System.StringComparison.Ordinal)){squareChip=graphic.sprite;break;}
            }
            if(squareChip==null)throw new System.InvalidOperationException("Combo prefab requires its authored fx_square_chip sprite.");
            var padding=UnityEngine.Sprites.DataUtility.GetPadding(squareChip);var original=squareChip.rect;
            squareGeometryFraction=Mathf.Max((original.width-padding.x-padding.z)/original.width,(original.height-padding.y-padding.w)/original.height);
            if(squareGeometryFraction<=0)throw new System.InvalidOperationException("Combo square sprite has no visible geometry.");
            for(int i=0;i<borders.Length;i++)
            {
                var go=new GameObject("ScoreStepBorder"+i,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
                var rect=(RectTransform)go.transform;rect.SetParent(view.visualRoot,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
                var graphic=go.GetComponent<Image>();graphic.raycastTarget=false;graphic.color=new Color(.953f,.729f,.345f,i<16?.8f:.42f);
                borders[i]=rect;go.SetActive(false);
            }
        }
        private void Remember(RectTransform rect)
        {
            if(rect==null||originals.Exists(x=>x.rect==rect))return;
            var image=rect.GetComponent<Image>();originals.Add(new Original{rect=rect,position=rect.anchoredPosition,size=rect.sizeDelta,pivot=rect.pivot,scale=rect.localScale,rotation=rect.localRotation,image=image,preserveAspect=image!=null&&image.preserveAspect,sprite=image==null?null:image.sprite,color=image==null?Color.white:image.color});
        }
        private static void Place(RectTransform rect,float x,float y,float width,float height)
        {
            rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);rect.localScale=Vector3.one;rect.localRotation=Quaternion.identity;
            var image=rect.GetComponent<Image>();if(image!=null)image.preserveAspect=false;
        }
        public void Configure(Vector2 size,bool lowQuality=false)
        {
            extent=size;configured=true;var root=(RectTransform)owner.transform;root.localScale=Vector3.one;root.sizeDelta=size;
            owner.visualRoot.localScale=Vector3.one;owner.visualRoot.sizeDelta=size;owner.visualRoot.anchoredPosition=Vector2.zero;edgeAccents071.Configure(size,lowQuality);Evaluate(0);
        }
        public void Evaluate(float time)
        {
            if(!configured)return;int tier=owner.BurstLevel;float w=extent.x,h=extent.y;
            // Authored circular glows are replaced by narrow stepped perimeter strokes.
            // This keeps the authored gold palette without painting behind gold numerals.
            for(int i=0;i<owner.burstLayers.Length;i++)if(owner.burstLayers[i]!=null)owner.burstLayers[i].SetActive(i==0||i==2&&tier==3||i==5&&tier==5&&!owner.ReducedMotion);
            if(owner.burstLayers.Length>0)Place((RectTransform)owner.burstLayers[0].transform,w-620,h-7,Mathf.Min(360,w-40),2);
            if(owner.burstLayers.Length>2)Place((RectTransform)owner.burstLayers[2].transform,20,h-10,(w-40)*(owner.ReducedMotion?1:1-Mathf.Pow(1-time,3)),4);
            if(owner.burstLayers.Length>5)
            {
                var flame=owner.burstLayers[5].GetComponent<Image>();var padding=UnityEngine.Sprites.DataUtility.GetPadding(flame.sprite);
                float drawnHeight=(flame.sprite.rect.height-padding.y-padding.w)/flame.sprite.rect.height;
                Place((RectTransform)flame.transform,h<110?368:w*.35f,h-46,238,26/Mathf.Max(.01f,drawnHeight));
            }
            for(int layer=0;layer<2;layer++)
            {
                bool visible=tier>=2&&(layer==0||tier>=4);float inset=(layer==0?3:8)+(owner.ReducedMotion?0:3*Mathf.Pow(1-time,3)),step=tier==5?12:6,thickness=layer==0?(tier>=4?3:2):1;
                // Symmetric stepped rectangle, with left lower corners explicitly routed.
                Vector2[] points={new Vector2(inset+step,inset),new Vector2(w-inset-step,inset),new Vector2(w-inset-step,inset+step),new Vector2(w-inset,inset+step),new Vector2(w-inset,h-inset-step),new Vector2(w-inset-step,h-inset-step),new Vector2(w-inset-step,h-inset),new Vector2(inset+step,h-inset),new Vector2(inset+step,h-inset-step),new Vector2(inset,h-inset-step),new Vector2(inset,inset+step),new Vector2(inset+step,inset+step)};
                for(int i=0;i<16;i++)
                {
                    var line=borders[layer*16+i];line.gameObject.SetActive(visible&&i<points.Length);if(i>=points.Length)continue;
                    Vector2 a=points[i],b=points[(i+1)%points.Length],d=b-a;Place(line,a.x,a.y,d.magnitude,thickness);line.localRotation=Quaternion.Euler(0,0,-Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);
                }
            }
            int activeChips=0;for(int i=0;i<owner.particles.Length;i++)if(owner.particles[i]!=null&&owner.particles[i].gameObject.activeSelf)activeChips++;
            int rank=0;
            for(int i=0;i<owner.particles.Length;i++)
            {
                if(!(owner.particles[i] is RectTransform chip))continue;
                var graphic=chip.GetComponent<Image>();graphic.sprite=squareChip;
                Color tint=tier==1?new Color(.953f,.729f,.345f):i%2==0?new Color(.925f,.396f,.365f):new Color(.333f,.714f,.851f);
                tint.a=graphic.color.a;graphic.color=tint;
                // Size against the real imported drawing quad including padding.
                // GPU alpha measurements independently require 5–7 visible pixels;
                // this keeps both packed and unpacked sprite imports legible.
                float fraction=(rank+.5f)/Mathf.Max(1,activeChips),size=(i%3==0?6.5f:6f)/squareGeometryFraction;
                // The compact ten-card layout leaves exactly 12 reference pixels
                // between its second row and the score panel. Use its midpoint;
                // a second row or vertical drift would enter the lower card text.
                Place(chip,24+fraction*(w-48),-6,size,size);chip.pivot=new Vector2(.5f,.5f);
                chip.localScale=Vector3.one;chip.localRotation=Quaternion.Euler(0,0,tier==1?45:(i%2==0?1:-1)*22*time);
                if(chip.gameObject.activeSelf)rank++;
            }
            for(int i=0;i<owner.burstStaticStars.Length;i++)if(owner.burstStaticStars[i]!=null)
            {
                owner.burstStaticStars[i].SetActive(owner.ReducedMotion&&i<tier);
                Place((RectTransform)owner.burstStaticStars[i].transform,w-170+i*23,h-13,10,10);
            }
            edgeAccents071.Evaluate(time);
        }
        public void ResetLayout()
        {
            if(!configured)return;configured=false;edgeAccents071.ResetForPool();
            foreach(var saved in originals)if(saved.rect!=null){saved.rect.anchoredPosition=saved.position;saved.rect.sizeDelta=saved.size;saved.rect.pivot=saved.pivot;saved.rect.localScale=saved.scale;saved.rect.localRotation=saved.rotation;if(saved.image!=null){saved.image.preserveAspect=saved.preserveAspect;saved.image.sprite=saved.sprite;saved.image.color=saved.color;}}
            foreach(var rect in borders)if(rect!=null)rect.gameObject.SetActive(false);
        }
    }
}
