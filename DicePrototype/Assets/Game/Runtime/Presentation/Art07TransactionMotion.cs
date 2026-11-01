using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DiceCraft.Presentation
{
    /// <summary>Visual-only transaction clock. Captured meshes have no Buttons, events, or rule data.</summary>
    public sealed class Art07TransactionMotion : MonoBehaviour
    {
        private sealed class SnapshotMesh { public RectTransform rect; public CanvasRenderer renderer; public Mesh mesh; public Material material; }
        private sealed class Track
        {
            public RectTransform rect; public CanvasGroup group; public float alpha, delay, duration; public Vector3 scale, position; public Quaternion rotation;
            public bool flip, rise, expand, hidden;
        }
        private readonly List<SnapshotMesh> meshes=new List<SnapshotMesh>();
        private readonly List<Track> tracks=new List<Track>();
        private readonly List<Vector4> uv=new List<Vector4>();
        private RectTransform snapshot;
        private CanvasGroup snapshotGroup;
        private Action<float> customTick;
        private Action finished;
        private bool reduced, shrinkOld;
        private float oldDuration, timeScale=1;
        public bool IsPlaying { get; private set; }
        public bool IsPrepared { get; private set; }
        public float Elapsed { get; private set; }
        public float Duration { get; private set; }
        public int CapturedGraphicCount { get; private set; }
        public int CreatedSnapshotMeshes=>meshes.Count;
        public string Kind { get; private set; }
        public string[] AnimatedTargets=>tracks.Select(t=>t.rect==null?"":t.rect.name).ToArray();
        public void Prepare(IEnumerable<RectTransform> roots)
        {
            if(IsPlaying)return;
            DiscardPrepared();EnsureSnapshot();Canvas.ForceUpdateCanvases();
            var actual=roots.Where(r=>r!=null&&r.gameObject.activeInHierarchy).Distinct().ToArray();
            var all=actual.SelectMany(r=>r.GetComponentsInChildren<Graphic>(false)).Distinct().ToArray();
            var corners=new Vector3[4];Vector3 low=Vector3.positiveInfinity,high=Vector3.negativeInfinity;
            foreach(var r in actual){r.GetWorldCorners(corners);foreach(var corner in corners){var local=transform.InverseTransformPoint(corner);low=Vector3.Min(low,local);high=Vector3.Max(high,local);}}
            snapshot.anchoredPosition=actual.Length==0?Vector2.zero:(Vector2)((low+high)*.5f);snapshot.localScale=Vector3.one;
            var canvas=GetComponentInParent<Canvas>().rootCanvas;int count=0;
            foreach(var g in all)
            {
                if(!g.isActiveAndEnabled||g.canvasRenderer.cull||g.canvasRenderer.GetInheritedAlpha()<=0)continue;
                var mask=g.GetComponent<Mask>();if(mask!=null&&mask.isActiveAndEnabled&&!mask.showMaskGraphic)continue;
                var source=g.canvasRenderer.GetMesh();if(source==null||source.vertexCount==0)continue;
                if(count>=256)throw new InvalidOperationException("Transaction snapshot exceeds 256 graphics.");
                var e=GetMesh(count++);e.mesh.Clear();e.mesh.vertices=source.vertices;e.mesh.colors32=source.colors32;
                for(int channel=0;channel<4;channel++){uv.Clear();source.GetUVs(channel,uv);e.mesh.SetUVs(channel,uv);}
                e.mesh.normals=source.normals;e.mesh.tangents=source.tangents;e.mesh.triangles=source.triangles;
                var vertices=e.mesh.vertices;var matrix=snapshot.worldToLocalMatrix*g.transform.localToWorldMatrix;
                for(int i=0;i<vertices.Length;i++)vertices[i]=matrix.MultiplyPoint3x4(vertices[i]);e.mesh.vertices=vertices;e.mesh.RecalculateBounds();
                // TMP_Text inherits Graphic.mainTexture (white). Its actual atlas
                // lives on the font material; each TMP_SubMeshUI owns its separate
                // fallback/multi-atlas material and must keep that texture too.
                Material material=g is TMPro.TMP_Text t?t.fontSharedMaterial:g is TMPro.TMP_SubMeshUI sub?sub.sharedMaterial:g.material;
                Texture texture=g is TMPro.TMP_Text||g is TMPro.TMP_SubMeshUI?material?.mainTexture:g.mainTexture;
                // TMP releases fallback materials when the source card is disabled.
                // Each mesh slot owns one reusable material so its committed image survives that release.
                if(e.material==null)e.material=new Material(material){name="Art07 transaction material "+(count-1),hideFlags=HideFlags.DontSave};
                else {e.material.shader=material.shader;e.material.CopyPropertiesFromMaterial(material);}
                e.material.shaderKeywords=material.shaderKeywords;e.material.renderQueue=material.renderQueue;
                e.renderer.materialCount=1;e.renderer.SetMaterial(e.material,0);e.renderer.SetTexture(texture);
                Color tint=g.canvasRenderer.GetColor();tint.a*=g.canvasRenderer.GetInheritedAlpha();e.renderer.SetColor(tint);
                bool clipped=false;Rect clip=default;
                foreach(var m in g.GetComponentsInParent<Mask>(false))if(m.isActiveAndEnabled&&m.transform!=g.transform)IntersectClip(m.rectTransform,canvas.transform,ref clipped,ref clip);
                foreach(var m in g.GetComponentsInParent<RectMask2D>(false))if(m.isActiveAndEnabled&&m.transform!=g.transform)IntersectClip(m.rectTransform,canvas.transform,ref clipped,ref clip);
                if(clipped)e.renderer.EnableRectClipping(clip);else e.renderer.DisableRectClipping();e.renderer.SetMesh(e.mesh);e.rect.gameObject.SetActive(true);
            }
            CapturedGraphicCount=count;IsPrepared=count>0;snapshotGroup.alpha=0; // Still invisible until a successful committed receipt.
        }
        /// <summary>Show an owned prepared mesh snapshot without a clock, motion, input or callback.</summary>
        public void HoldPrepared()
        {
            if(IsPlaying)throw new InvalidOperationException("A playing transaction cannot become a static backdrop.");
            EnsureSnapshot();snapshot.localScale=Vector3.one;snapshotGroup.alpha=IsPrepared?1:0;
        }
        public void Begin(string kind,float normalDuration,float oldFade,bool shrink,bool fast,bool reduce,Action onFinished,Action<float> visualTick=null)
        {
            if(IsPlaying)Finish();EnsureSnapshot();Kind=kind;Elapsed=0;reduced=reduce;shrinkOld=shrink;
            Duration=reduce?Math.Min(.12f,normalDuration):fast?Math.Min(.15f,normalDuration):normalDuration;
            timeScale=normalDuration<=0?1:Duration/normalDuration;oldDuration=oldFade*timeScale;finished=onFinished;customTick=visualTick;
            IsPlaying=Duration>0;snapshotGroup.alpha=IsPrepared&&oldDuration>0?1:0;
            Evaluate();if(!IsPlaying)Finish();
        }
        public void TrackReveal(RectTransform rect,float normalDelay,float normalDuration,bool flip=false,bool rise=false,bool expand=false)
        {
            if(rect==null||tracks.Any(t=>t.rect==rect))return;
            var group=rect.GetComponent<CanvasGroup>();if(group==null)group=rect.gameObject.AddComponent<CanvasGroup>();
            tracks.Add(new Track{rect=rect,group=group,alpha=group.alpha,scale=rect.localScale,position=rect.localPosition,rotation=rect.localRotation,delay=normalDelay*timeScale,duration=Math.Max(.001f,normalDuration*timeScale),flip=flip,rise=rise,expand=expand});Evaluate();
        }
        public void HideUntilFinished(RectTransform rect)
        {
            if(rect==null||tracks.Any(t=>t.rect==rect))return;var group=rect.GetComponent<CanvasGroup>();if(group==null)group=rect.gameObject.AddComponent<CanvasGroup>();
            tracks.Add(new Track{rect=rect,group=group,alpha=group.alpha,scale=rect.localScale,position=rect.localPosition,rotation=rect.localRotation,hidden=true});group.alpha=0;
        }
        public void Tick(float delta,bool paused)
        {
            if(!IsPlaying||paused)return;Elapsed=Math.Min(Duration,Elapsed+Math.Max(0,delta));Evaluate();if(Elapsed>=Duration)Finish();
        }
        private void Evaluate()
        {
            if(snapshotGroup==null)return;float old=oldDuration<=0?1:Mathf.Clamp01(Elapsed/oldDuration);snapshotGroup.alpha=IsPrepared?1-old:0;
            snapshot.localScale=Vector3.one*(shrinkOld&&!reduced?1-.04f*old:1);
            foreach(var t in tracks)
            {
                if(t.rect==null||t.group==null)continue;if(t.hidden){t.group.alpha=0;continue;}
                float n=Mathf.Clamp01((Elapsed-t.delay)/t.duration),ease=1-Mathf.Pow(1-n,3);t.group.alpha=t.alpha*n;
                t.rect.localScale=t.scale;t.rect.localPosition=t.position;t.rect.localRotation=t.rotation;
                if(!reduced){if(t.flip)t.rect.localRotation=t.rotation*Quaternion.Euler(0,-80*(1-ease),0);if(t.rise)t.rect.localPosition=t.position+Vector3.down*12*(1-ease);if(t.expand)t.rect.localScale=new Vector3(t.scale.x*Math.Max(.001f,ease),t.scale.y,t.scale.z);}
            }
            customTick?.Invoke(timeScale<=0?Duration:Elapsed/timeScale);
        }
        public void Finish(bool invokeCallback=true)
        {
            bool wasPlaying=IsPlaying;IsPlaying=false;
            if(wasPlaying&&invokeCallback)customTick?.Invoke(timeScale<=0?Duration:Duration/timeScale);
            foreach(var t in tracks)if(t.rect!=null){t.rect.localScale=t.scale;t.rect.localPosition=t.position;t.rect.localRotation=t.rotation;if(t.group!=null)t.group.alpha=t.alpha;}
            tracks.Clear();customTick=null;DiscardPrepared();var callback=finished;finished=null;if(invokeCallback&&wasPlaying)callback?.Invoke();
        }
        public void DiscardPrepared()
        {
            if(IsPlaying)return;IsPrepared=false;CapturedGraphicCount=0;
            foreach(var item in meshes){item.renderer.Clear();item.rect.gameObject.SetActive(false);}if(snapshotGroup!=null){snapshotGroup.alpha=0;snapshot.localScale=Vector3.one;}
        }
        private void EnsureSnapshot()
        {
            if(snapshot!=null)return;var go=new GameObject("CommittedOldMeshes",typeof(RectTransform),typeof(CanvasGroup));snapshot=(RectTransform)go.transform;
            snapshot.SetParent(transform,false);snapshot.anchorMin=snapshot.anchorMax=snapshot.pivot=new Vector2(0,1);snapshot.sizeDelta=Vector2.zero;
            snapshotGroup=go.GetComponent<CanvasGroup>();snapshotGroup.alpha=0;snapshotGroup.interactable=false;snapshotGroup.blocksRaycasts=false;
        }
        private SnapshotMesh GetMesh(int i)
        {
            if(i<meshes.Count)return meshes[i];var go=new GameObject("TransactionMesh"+i,typeof(RectTransform),typeof(CanvasRenderer));var rect=(RectTransform)go.transform;
            rect.SetParent(snapshot,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=Vector2.zero;
            var entry=new SnapshotMesh{rect=rect,renderer=go.GetComponent<CanvasRenderer>(),mesh=new Mesh{name="Art07 transaction snapshot "+i}};entry.mesh.MarkDynamic();meshes.Add(entry);return entry;
        }
        private static void IntersectClip(RectTransform mask,Transform canvas,ref bool clipped,ref Rect result)
        {
            var corners=new Vector3[4];mask.GetWorldCorners(corners);Vector3 low=canvas.InverseTransformPoint(corners[0]),high=low;
            for(int i=1;i<4;i++){var p=canvas.InverseTransformPoint(corners[i]);low=Vector3.Min(low,p);high=Vector3.Max(high,p);}
            var current=Rect.MinMaxRect(low.x,low.y,high.x,high.y);result=clipped?Rect.MinMaxRect(Math.Max(result.xMin,current.xMin),Math.Max(result.yMin,current.yMin),Math.Min(result.xMax,current.xMax),Math.Min(result.yMax,current.yMax)):current;clipped=true;
        }
        private void OnDestroy(){Finish(false);foreach(var m in meshes){if(m.mesh!=null)Destroy(m.mesh);if(m.material!=null)Destroy(m.material);}}
    }
}
