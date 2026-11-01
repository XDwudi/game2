using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DiceCraft.Presentation
{
    /// <summary>Read-only permanent mesh transitions. Core values never enter this component.
    /// One shared transient material; ordinary opaque face materials remain untouched.</summary>
    public sealed class Art07FaceTransition : MonoBehaviour
    {
        public sealed class FaceImage
        {
            public string Id;public Mesh Mesh;public MeshRenderer Renderer;public Color Color;public bool Visible;
        }
        private sealed class Pair
        {
            public MeshFilter oldMesh,newMesh;public MeshRenderer oldView,newView,physical;
            public Transform oldTransform,newTransform;public bool physicalEnabled;
            public string id;public float delay,duration;public bool wipe,shrink,exclusivePips;public Color oldColor,newColor;
            public MaterialPropertyBlock oldProps=new MaterialPropertyBlock(),newProps=new MaterialPropertyBlock();
        }
        private readonly List<Pair> all=new List<Pair>(),active=new List<Pair>();
        private readonly Stack<Pair> spare=new Stack<Pair>();
        private sealed class Body { public MeshRenderer renderer; public MaterialPropertyBlock original=new MaterialPropertyBlock(),working=new MaterialPropertyBlock();public float before,after,duration; }
        private readonly List<Body> bodies=new List<Body>();
        private Material material;
        public int ActiveFaces=>active.Count;
        public int CreatedPairs=>all.Count;
        public string[] BeforeMeshNames=>active.Select(x=>x.oldMesh.sharedMesh?.name??"").ToArray();
        public string[] TargetFaceIds=>active.Select(x=>x.id).ToArray();
        public float Progress { get; private set; }
        public void AddBody(MeshRenderer renderer,float before,float after,float seconds)
        {
            if(renderer==null)return;var body=new Body{renderer=renderer,before=before,after=after,duration=Math.Max(.001f,seconds)};renderer.GetPropertyBlock(body.original);renderer.GetPropertyBlock(body.working);bodies.Add(body);
            body.working.SetFloat("_Glass",before);renderer.SetPropertyBlock(body.working);
        }
        public void Initialize()
        {
            if(material!=null)return;
            var shader=Resources.Load<Shader>("Art07FaceTransition");
            if(shader==null)throw new InvalidOperationException("Art07FaceTransition shader must be included as a Resources asset.");
            material=new Material(shader){name="Art07 transient permanent faces"};
            for(int i=0;i<16;i++)spare.Push(Create());
        }
        private Pair Create()
        {
            var p=new Pair();
            for(int i=0;i<2;i++)
            {
                var go=new GameObject(i==0?"PreviousPermanentFace":"CommittedPermanentFace",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);
                var filter=go.GetComponent<MeshFilter>();var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=i;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                if(i==0){p.oldMesh=filter;p.oldView=renderer;p.oldTransform=go.transform;}else{p.newMesh=filter;p.newView=renderer;p.newTransform=go.transform;}go.SetActive(false);
            }
            all.Add(p);return p;
        }
        public static FaceImage[] Capture(Transform orientation,string dieId,Color[] actualColors)
        {
            if(orientation==null)return Array.Empty<FaceImage>();
            var result=new FaceImage[6];
            for(int i=0;i<6;i++)
            {
                var face=orientation.Find("Face_F"+(i+1).ToString("00"));var renderer=face.GetComponent<MeshRenderer>();
                result[i]=new FaceImage{Id=dieId+"/F"+(i+1).ToString("00"),Mesh=face.GetComponent<MeshFilter>().sharedMesh,Renderer=renderer,Visible=renderer.enabled,Color=actualColors[i]};
            }
            return result;
        }
        public void Add(FaceImage previous,FaceImage next,float seconds,float delay,bool wipe,bool shrink,bool exclusivePips=false)
        {
            Initialize();if(next?.Renderer==null||previous?.Mesh==null||next.Mesh==null)return;
            if(active.Count>=192)throw new InvalidOperationException("Permanent transition exceeds 32 dice × six physical faces.");
            Pair p=null;
            while(spare.Count>0&&p==null){var candidate=spare.Pop();if(HasViews(candidate))p=candidate;else Discard(candidate);}
            if(p==null)p=Create();p.physical=next.Renderer;p.physicalEnabled=next.Visible;
            p.id=next.Id;p.delay=delay;p.duration=Math.Max(.001f,seconds);p.wipe=wipe;p.shrink=shrink;p.exclusivePips=exclusivePips;p.oldColor=previous.Color;p.newColor=next.Color;
            p.oldMesh.sharedMesh=previous.Mesh;p.newMesh.sharedMesh=next.Mesh;
            foreach(var t in new[]{p.oldTransform,p.newTransform}){t.SetParent(next.Renderer.transform,false);t.localPosition=Vector3.forward*.0002f;t.localRotation=Quaternion.identity;t.localScale=Vector3.one;t.gameObject.SetActive(next.Visible);}
            active.Add(p);Apply(p,0);Progress=0;
        }
        public void SetTime(float seconds)
        {
            Progress=1;
            for(int i=active.Count-1;i>=0;i--)
            {
                var p=active[i];if(p.physical==null||!HasViews(p)){Return(p);active.RemoveAt(i);continue;}
                float t=Mathf.Clamp01((seconds-p.delay)/p.duration);Progress=Math.Min(Progress,t);Apply(p,t);
                if(t>=1){Return(p);active.RemoveAt(i);}
            }
            for(int i=bodies.Count-1;i>=0;i--){var b=bodies[i];float t=Mathf.Clamp01(seconds/b.duration);if(b.renderer!=null){b.working.SetFloat("_Glass",Mathf.Lerp(b.before,b.after,t));b.renderer.SetPropertyBlock(b.working);if(t>=1)b.renderer.SetPropertyBlock(b.original);}if(t>=1)bodies.RemoveAt(i);}
        }
        private static void Apply(Pair p,float progress)
        {
            if(p.physical!=null)p.physical.enabled=false;
            p.oldProps.SetColor("_BaseColor",p.oldColor);p.newProps.SetColor("_BaseColor",p.newColor);
            p.oldProps.SetFloat("_Progress",progress);p.oldProps.SetFloat("_Wipe",p.wipe?1:0);p.oldProps.SetFloat("_ShrinkPips",p.shrink?1:0);
            p.newProps.SetFloat("_Progress",progress);p.newProps.SetFloat("_Wipe",p.wipe?1:0);p.newProps.SetFloat("_ShrinkPips",p.shrink?1:0);
            p.oldProps.SetFloat("_ExclusivePips",p.exclusivePips?1:0);p.newProps.SetFloat("_ExclusivePips",p.exclusivePips?1:0);
            p.oldProps.SetFloat("_OldLayer",1);p.newProps.SetFloat("_OldLayer",0);p.oldView.SetPropertyBlock(p.oldProps);p.newView.SetPropertyBlock(p.newProps);
        }
        private void Return(Pair p)
        {
            if(p.physical!=null)p.physical.enabled=p.physicalEnabled;p.physical=null;p.id=null;
            if(!HasViews(p)){Discard(p);return;}
            foreach(var t in new[]{p.oldTransform,p.newTransform})if(t!=null)
            {
                t.gameObject.SetActive(false);
                // Unity forbids reparenting into a hierarchy while that hierarchy is
                // deactivating. In OnDisable the hidden pair may stay at its old face;
                // Add reparents it only after the pool is active again.
                if(isActiveAndEnabled)t.SetParent(transform,false);
                t.localPosition=Vector3.zero;t.localRotation=Quaternion.identity;t.localScale=Vector3.one;
            }
            p.oldMesh.sharedMesh=null;p.newMesh.sharedMesh=null;spare.Push(p);
        }
        // The owning face can disappear before this pool during scene unload. Unity's
        // destroyed-object null must be checked on every borrowed renderer/transform.
        private static bool HasViews(Pair p)=>p!=null&&p.oldTransform!=null&&p.newTransform!=null&&p.oldMesh!=null&&p.newMesh!=null&&p.oldView!=null&&p.newView!=null;
        private void Discard(Pair p)
        {
            all.Remove(p);
            if(p.oldTransform!=null)Destroy(p.oldTransform.gameObject);
            if(p.newTransform!=null)Destroy(p.newTransform.gameObject);
        }
        public void Finish(){foreach(var p in active)Return(p);active.Clear();foreach(var b in bodies)if(b.renderer!=null)b.renderer.SetPropertyBlock(b.original);bodies.Clear();Progress=1;}
        private void OnDisable()=>Finish();
        private void OnDestroy()
        {
            Finish();
            // Disabled pairs may still be children of live physical faces, so they
            // must be destroyed explicitly when the pool owner itself is removed.
            foreach(var p in all){if(p.oldTransform!=null)Destroy(p.oldTransform.gameObject);if(p.newTransform!=null)Destroy(p.newTransform.gameObject);}
            all.Clear();spare.Clear();if(material!=null)Destroy(material);
        }
    }
}
