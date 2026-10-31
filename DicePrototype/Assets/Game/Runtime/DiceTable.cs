using System;
using System.Collections.Generic;
using DiceCraft.Core;
using UnityEngine;

namespace DiceCraft
{
    public sealed class DiceTable : MonoBehaviour
    {
        public Camera viewCamera;
        public Mesh bodyMesh;
        public Material bodyMaterial, faceMaterial, glassMaterial;
        public AnimationClip[] rollClips;
        private readonly Dictionary<string, Visual> visuals = new Dictionary<string, Visual>();
        private readonly Stack<Visual> spareVisuals = new Stack<Visual>(10);
        private readonly List<string> unusedIds = new List<string>(10);
        private readonly Mesh[] faceMeshes = new Mesh[6 * 2 * 3];
        // Native Unity objects/IDs must be created on the main thread, never while the
        // MonoBehaviour is being deserialized or its managed field initializers run.
        private MaterialPropertyBlock faceProperties;
        private int baseColorId;
        private const string PreviewId = "$die-preview";
        private GameObject sampleRoot;
        private Transform samplePivot;
        private MotionClip[] motions;
        private int cachedMeshCount;
        private float duration, elapsed;
        private bool animate;
        private sealed class Visual
        {
            public GameObject root;
            public Transform rollPivot, pivot;
            public MeshFilter bodyFilter;
            public MeshRenderer body;
            public readonly MeshFilter[] faces = new MeshFilter[6];
            public readonly MeshRenderer[] faceRenderers = new MeshRenderer[6];
            public readonly int[] faceKeys = { -1, -1, -1, -1, -1, -1 };
            public readonly int[] faceColors = { -1, -1, -1, -1, -1, -1 };
            public Quaternion target, previousOrientation, compensation;
            public Vector3 resting, previousCenter, initialOffset;
            public bool rolling, hasDisplayed;
            public int displayFrame, clipIndex;
        }
        private sealed class MotionClip
        {
            public AnimationClip clip;
            public Quaternion inverseStartRotation, rotation;
            public Vector3 startPosition, position;
        }

        public RectTransform designRoot;
        public bool IsRolling => animate;
        public int CachedFaceMeshCount => cachedMeshCount;
        public int PooledVisualCount => spareVisuals.Count;
        public int ImportedRollClipCount
        {
            get
            {
                if (rollClips == null) return 0;
                int count = 0;
                for (int i = 0; i < rollClips.Length; i++) if (rollClips[i] != null && rollClips[i].length > 0) count++;
                return count;
            }
        }
        public static readonly Vector3[] Normals = { Vector3.up, Vector3.forward, Vector3.right, Vector3.left, Vector3.back, Vector3.down };
        public static int FaceIndex(string id) { return string.IsNullOrEmpty(id) ? 0 : Mathf.Clamp(int.Parse(id.Substring(1))-1,0,5); }
        public void Hide() { foreach(var v in visuals.Values) v.root.SetActive(false); }

        /// <summary>The actual transform that owns the visible physical faces, including during a roll.</summary>
        public Transform GetFaceOrientation(string dieId)
        {
            return visuals.TryGetValue(dieId, out Visual visual) ? visual.pivot : null;
        }

        public void Display(DieInstance d, DieHandState hand, Vector2 uiCenter, float size = 1.18f)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));
            DisplayInternal(d.dieId, d, hand, uiCenter, size, false);
        }

        /// <summary>
        /// A separate, permanent-data preview using the existing table camera. The UI must leave
        /// its viewport transparent; dragging can pass Quaternion.Euler(pitch, yaw, 0). This view
        /// never participates in rolling and never changes the supplied die or its current result.
        /// </summary>
        public void ShowPreview(DieInstance d, Vector2 uiCenter, Quaternion rotation, float size = 1.6f)
        {
            Visual v = DisplayInternal(PreviewId, d, null, uiCenter, size, true);
            v.pivot.localRotation = rotation;
            v.target = rotation;
        }

        public void HidePreview()
        {
            if (visuals.TryGetValue(PreviewId, out Visual v)) v.root.SetActive(false);
        }

        private Visual DisplayInternal(string id, DieInstance d, DieHandState hand, Vector2 uiCenter, float size, bool preview)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));
            if (viewCamera == null) throw new InvalidOperationException("DiceTable requires its table camera.");
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            if (faceProperties == null)
            {
                faceProperties = new MaterialPropertyBlock();
                baseColorId = Shader.PropertyToID("_BaseColor");
            }
            if(!visuals.TryGetValue(id,out Visual v))
            {
                v = spareVisuals.Count > 0 ? spareVisuals.Pop() : CreateVisual();
                v.root.name = id;
                v.hasDisplayed = false;
                v.rollPivot.localPosition = Vector3.zero;
                v.rollPivot.localRotation = Quaternion.identity;
                v.rollPivot.localScale = Vector3.one;
                visuals.Add(id,v);
            }
            bool preserveMotion = animate && v.hasDisplayed && v.rolling && !preview;
            // Refresh applies the accepted state before BeginRoll is called in the same frame.
            // Keep the actually displayed pose so BeginRoll can restore it before rendering.
            if (!preserveMotion)
            {
                v.previousOrientation = v.hasDisplayed ? v.rollPivot.localRotation * v.pivot.localRotation : Quaternion.identity;
                v.previousCenter = v.hasDisplayed ? v.rollPivot.position : Vector3.zero;
                v.displayFrame = Time.frameCount;
            }
            v.root.SetActive(true); v.root.transform.localScale=Vector3.one*size;
            var plane=new Plane(Vector3.up,Vector3.zero);
            Vector2 screen=designRoot!=null?RectTransformUtility.WorldToScreenPoint(null,designRoot.TransformPoint(new Vector3(uiCenter.x,-uiCenter.y,0))):new Vector2(uiCenter.x/1920f*Screen.width,(1-uiCenter.y/1080f)*Screen.height);
            Ray ray=viewCamera.ScreenPointToRay(screen);
            if (!plane.Raycast(ray,out float dist)) throw new InvalidOperationException("Table camera ray must intersect the dice plane.");
            v.resting=ray.GetPoint(dist);
            if (!v.hasDisplayed) v.previousCenter = v.resting;
            v.root.transform.position=v.resting;
            int current=FaceIndex(hand?.currentFaceId); v.target=Quaternion.FromToRotation(Normals[current],Vector3.up);
            if (!preserveMotion)
            {
                v.rollPivot.localPosition = Vector3.zero;
                v.rollPivot.localRotation = Quaternion.identity;
                v.rollPivot.localScale = Vector3.one;
                v.pivot.localRotation=v.target;
            }
            v.bodyFilter.sharedMesh=bodyMesh;
            Material material=d.isGlass ? glassMaterial : bodyMaterial;
            if (v.body.sharedMaterial != material) v.body.sharedMaterial=material;
            for(int f=0;f<6;f++)
            {
                var state=d.faces[f]; int pip=hand!=null && hand.hasRolled && f==current ? hand.currentPip : state.permanentPip;
                if (pip < 1 || pip > 6) throw new ArgumentOutOfRangeException(nameof(pip));
                int texture = state.texture == "smooth" ? 1 : state.texture == "rough" ? 2 : 0;
                int key = (pip-1) * 6 + (state.isGold ? 3 : 0) + texture;
                if (v.faceKeys[f] != key || v.faces[f].sharedMesh == null)
                {
                    if (faceMeshes[key] == null)
                    {
                        faceMeshes[key] = MakeFace(pip,state.isGold,state.texture);
                        faceMeshes[key].name = "Pips_" + pip + "_" + (state.isGold ? "Gold_" : "Plain_") + texture;
                        cachedMeshCount++;
                    }
                    v.faces[f].sharedMesh=faceMeshes[key];
                    v.faceKeys[f]=key;
                }
                int color = state.baseColor == "red" ? 0 : 1;
                if (v.faceColors[f] != color || v.faceRenderers[f].sharedMaterial != faceMaterial)
                {
                    v.faceRenderers[f].sharedMaterial=faceMaterial;
                    faceProperties.SetColor(baseColorId,color == 0 ? UiKit.Red : UiKit.Blue);
                    v.faceRenderers[f].SetPropertyBlock(faceProperties);
                    v.faceColors[f]=color;
                }
            }
            v.rolling=!preview && (hand==null || !hand.isLocked);
            v.clipIndex=Math.Max(0,d.createdSerial);
            v.hasDisplayed=true;
            return v;
        }

        private Visual CreateVisual()
        {
            var v=new Visual();
            v.root=new GameObject("DiceRoot");v.root.transform.SetParent(transform,false);
            v.rollPivot=new GameObject("RollPivot").transform;v.rollPivot.SetParent(v.root.transform,false);
            v.pivot=new GameObject("FaceOrientation").transform;v.pivot.SetParent(v.rollPivot,false);
            var body=new GameObject("Body",typeof(MeshFilter),typeof(MeshRenderer));body.transform.SetParent(v.pivot,false);
            v.bodyFilter=body.GetComponent<MeshFilter>();v.body=body.GetComponent<MeshRenderer>();
            for(int f=0;f<6;f++)
            {
                var face=new GameObject("Face_F"+(f+1).ToString("00"),typeof(MeshFilter),typeof(MeshRenderer));face.transform.SetParent(v.pivot,false);
                face.transform.localPosition=Normals[f]*.506f;face.transform.localRotation=Quaternion.FromToRotation(Vector3.forward,Normals[f]);
                v.faces[f]=face.GetComponent<MeshFilter>();v.faceRenderers[f]=face.GetComponent<MeshRenderer>();
            }
            return v;
        }

        public void BeginRoll(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (seconds <= 0) { FinishRoll(); return; }
            EnsureMotionClips();
            foreach (var v in visuals.Values)
            {
                if (!v.root.activeSelf || !v.rolling) continue;
                Quaternion previous = v.displayFrame == Time.frameCount ? v.previousOrientation : v.rollPivot.localRotation*v.pivot.localRotation;
                Vector3 previousCenter = v.displayFrame == Time.frameCount ? v.previousCenter : v.rollPivot.position;
                v.compensation = previous * Quaternion.Inverse(v.target);
                v.initialOffset = v.root.transform.InverseTransformVector(previousCenter-v.resting);
                v.pivot.localRotation=v.target;
                v.rollPivot.localRotation=v.compensation;
                v.rollPivot.localPosition=v.initialOffset;
                v.rollPivot.localScale=Vector3.one;
            }
            duration=seconds;elapsed=0;animate=true;
        }

        public void FinishRoll()
        {
            animate=false;
            foreach(var v in visuals.Values)
            {
                if(v.root==null || v.rollPivot==null || v.pivot==null) continue;
                v.rollPivot.localPosition=Vector3.zero;
                v.rollPivot.localRotation=Quaternion.identity;
                v.rollPivot.localScale=Vector3.one;
                v.pivot.localRotation=v.target;
                v.root.transform.position=v.resting;
            }
        }

        void Update()
        {
            if(!animate) return;elapsed+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(elapsed/duration);
            // 0.10s connection, 0.70s authored roll, 0.10s settling at normal 0.90s speed.
            // The same normalized timeline is compressed for the 0.15s fast option.
            float roll = Mathf.Clamp01((t-1f/9f)/(7f/9f));
            float alignment = Mathf.SmoothStep(0,1,roll);
            SampleMotions(roll);
            foreach(var v in visuals.Values) if(v.root.activeSelf && v.rolling)
            {
                MotionClip motion = motions != null && motions.Length > 0 ? motions[v.clipIndex%motions.Length] : null;
                Quaternion rotation;
                Vector3 position;
                if(motion != null && motion.clip != null)
                {
                    rotation=motion.rotation;position=motion.position;
                }
                else
                {
                    // Isolated tests/unconfigured scenes remain operable; the production scene
                    // binds both imported clips and can assert ImportedRollClipCount == 2.
                    float envelope=Mathf.Sin(roll*Mathf.PI);envelope*=envelope;
                    rotation=Quaternion.Euler(720*envelope,360*envelope,180*Mathf.Sin(roll*Mathf.PI*2)*envelope);
                    position=Vector3.up*(Mathf.Sin(roll*Mathf.PI)*.38f);
                }
                v.rollPivot.localRotation=rotation*Quaternion.Slerp(v.compensation,Quaternion.identity,alignment);
                v.rollPivot.localPosition=position+Vector3.Lerp(v.initialOffset,Vector3.zero,Mathf.SmoothStep(0,1,Mathf.Clamp01(t*9)));
                v.rollPivot.localScale=Vector3.one;
            }
            if(t>=1) FinishRoll();
        }

        private void EnsureMotionClips()
        {
            int count=rollClips?.Length??0;
            bool changed=motions==null || motions.Length!=count;
            if(!changed) for(int i=0;i<count;i++) if(motions[i].clip!=rollClips[i]) {changed=true;break;}
            if(!changed) return;
            if(count>0 && sampleRoot==null)
            {
                // Sample only a renderer-free rig. Imported files also contain root/child TRS
                // curves; those must never overwrite actual layout, face orientation or meshes.
                sampleRoot=new GameObject("DiceRollSampleRig");sampleRoot.hideFlags=HideFlags.HideAndDontSave;
                samplePivot=new GameObject("RollPivot").transform;samplePivot.SetParent(sampleRoot.transform,false);
                var faceOrientation=new GameObject("FaceOrientation").transform;faceOrientation.SetParent(samplePivot,false);
                var body=new GameObject("DiceBody").transform;body.SetParent(faceOrientation,false);
                for(int f=1;f<=6;f++) {var marker=new GameObject("Face_F"+f.ToString("00")).transform;marker.SetParent(faceOrientation,false);}
                // Player builds require an Animator to sample non-Legacy (Generic) clips.
                // Editor sampling allows an absent Animator, so Editor-only tests miss this.
                // No controller is assigned: explicit SampleAnimation calls own this rig's pose.
                var sampler=sampleRoot.AddComponent<Animator>();
                sampler.applyRootMotion=false;
                sampler.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            }
            motions=new MotionClip[count];
            for(int i=0;i<count;i++)
            {
                var motion=new MotionClip {clip=rollClips[i]};motions[i]=motion;
                if(motion.clip==null || motion.clip.length<=0) {motion.clip=null;continue;}
                motion.clip.SampleAnimation(sampleRoot,0);
                motion.startPosition=samplePivot.position;
                motion.inverseStartRotation=Quaternion.Inverse(samplePivot.rotation);
            }
        }

        private void SampleMotions(float normalizedTime)
        {
            if(motions==null || sampleRoot==null) return;
            for(int i=0;i<motions.Length;i++)
            {
                var motion=motions[i];if(motion.clip==null) continue;
                motion.clip.SampleAnimation(sampleRoot,normalizedTime*motion.clip.length);
                motion.position=samplePivot.position-motion.startPosition;
                motion.rotation=samplePivot.rotation*motion.inverseStartRotation;
                // Exact rest is explicit: clip import compression cannot leave a tilted result.
                if(normalizedTime<=0 || normalizedTime>=1) {motion.position=Vector3.zero;motion.rotation=Quaternion.identity;}
            }
        }

        void LateUpdate()
        {
            // Hide/Display constitute one refresh; reclaim only after the entire layout pass.
            // Reusing by dieId during that pass preserves the previous pose for animations.
            unusedIds.Clear();
            foreach(var pair in visuals) if(!pair.Value.root.activeSelf) unusedIds.Add(pair.Key);
            for(int i=0;i<unusedIds.Count;i++)
            {
                Visual v=visuals[unusedIds[i]];visuals.Remove(unusedIds[i]);
                v.hasDisplayed=false;v.rolling=false;v.root.name="PooledDie";
                if(spareVisuals.Count<10) spareVisuals.Push(v);else Destroy(v.root);
            }
        }
        private static Mesh MakeFace(int pip,bool gold,string texture)
        {
            var vertices=new List<Vector3>();var indices=new List<int>();var colors=new List<Color>();
            Action<float,float,float,Color> circle=(x,y,r,color)=> { int s=vertices.Count; vertices.Add(new Vector3(x,y,0));colors.Add(color);
                for(int j=0;j<=16;j++){float a=j*Mathf.PI*2/16;vertices.Add(new Vector3(x+Mathf.Cos(a)*r,y+Mathf.Sin(a)*r,0));colors.Add(color);if(j>0){indices.Add(s);indices.Add(s+j);indices.Add(s+j+1);}} };
            Action<float,float,float,float,Color> bar=(x,y,w,h,color)=> {int s=vertices.Count;vertices.Add(new Vector3(x,y,0));vertices.Add(new Vector3(x+w,y,0));vertices.Add(new Vector3(x+w,y+h,0));vertices.Add(new Vector3(x,y+h,0));for(int k=0;k<4;k++)colors.Add(color);indices.AddRange(new[]{s,s+1,s+2,s,s+2,s+3});};
            if(pip%2==1) circle(0,0,.064f,Color.white);
            if(pip>=2) { circle(-.22f,.22f,.064f,Color.white);circle(.22f,-.22f,.064f,Color.white); }
            if(pip>=4) { circle(.22f,.22f,.064f,Color.white);circle(-.22f,-.22f,.064f,Color.white); }
            if(pip==6) { circle(-.22f,0,.064f,Color.white);circle(.22f,0,.064f,Color.white); }
            // Frame and texture markers remain separate from the rule color/pip count.
            if(gold) { bar(-.4f,-.4f,.8f,.025f,UiKit.Gold);bar(-.4f,.375f,.8f,.025f,UiKit.Gold);bar(-.4f,-.4f,.025f,.8f,UiKit.Gold);bar(.375f,-.4f,.025f,.8f,UiKit.Gold); }
            if(texture=="rough") for(int i=0;i<5;i++) bar(-.16f+i*.07f,-.365f,.025f,.055f,Color.white);
            if(texture=="smooth") bar(-.16f,-.35f,.32f,.018f,Color.white);
            var m=new Mesh {name="DynamicFace"};m.SetVertices(vertices);m.SetTriangles(indices,0);m.SetColors(colors);m.RecalculateNormals();m.RecalculateBounds();return m;
        }
        void OnDisable() { FinishRoll(); }
        void OnDestroy()
        {
            for(int i=0;i<faceMeshes.Length;i++) if(faceMeshes[i]!=null) Destroy(faceMeshes[i]);
            if(sampleRoot!=null) Destroy(sampleRoot);
        }
    }
}
