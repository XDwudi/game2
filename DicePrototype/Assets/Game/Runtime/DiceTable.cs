using System;
using System.Collections.Generic;
using DiceCraft.Core;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class DiceTable : MonoBehaviour
    {
        public Camera viewCamera;
        public Mesh bodyMesh;
        public Material bodyMaterial, faceMaterial, glassMaterial, outlineMaterial;
        public AnimationClip[] rollClips;
        private readonly Dictionary<string, Visual> visuals = new Dictionary<string, Visual>();
        private readonly Stack<Visual> spareVisuals = new Stack<Visual>(33);
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
            public MeshFilter outlineFilter;
            public MeshRenderer outlineRenderer;
            public readonly MeshFilter[] faces = new MeshFilter[6];
            public readonly MeshRenderer[] faceRenderers = new MeshRenderer[6];
            public readonly int[] faceKeys = { -1, -1, -1, -1, -1, -1 };
            public readonly int[] faceColors = { -1, -1, -1, -1, -1, -1 };
            public Quaternion target, previousOrientation, compensation;
            public Vector3 resting, previousCenter, initialOffset;
            public Vector3 layoutFrom, layoutScaleFrom, layoutScaleTo;
            public Quaternion layoutRotationFrom;
            public float layoutElapsed,layoutDuration;
            public bool rolling, hasDisplayed, layoutMoving, locked, lockPunch, pending07, lockTrack07;
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
        public float RollTimeRemaining => animate ? Mathf.Max(0, duration - elapsed) : 0;
        public bool ReducedMotion { get; set; }
        public bool IsTransitioning { get { foreach (var v in visuals.Values) if (v.layoutMoving) return true; return false; } }
        public const float LayoutTransitionSeconds = .18f;
        public float LayoutDuration { get; set; } = LayoutTransitionSeconds;
        public bool Paused { get; set; }
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

        public void Display(DieInstance d, DieHandState hand, Vector2 uiCenter, float size = 1.18f, bool pending = false)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));
            DisplayInternal(d.dieId, d, hand, uiCenter, size, false, pending);
        }
        public void DisplayDragged(DieInstance d,DieHandState hand,Vector2 uiCenter,float size)
        {DisplayInternal(d.dieId,d,hand,uiCenter,size,false,false,true);}

        /// <summary>
        /// A separate, permanent-data preview using the existing table camera. The UI must leave
        /// its viewport transparent; dragging can pass Quaternion.Euler(pitch, yaw, 0). This view
        /// never participates in rolling and never changes the supplied die or its current result.
        /// </summary>
        public void ShowPreview(DieInstance d, Vector2 uiCenter, Quaternion rotation, float size = 1.6f)
        {
            Visual v = DisplayInternal(PreviewId, d, null, uiCenter, size, true, false);
            v.pivot.localRotation = rotation;
            v.target = rotation;
            UpdateFaceVisibility(v);
        }

        private const string StaticPreviewPrefix07 = "$static-preview:";
        /// <summary>Named, permanent-face-only static preview. Never rolls or mutates the input die.</summary>
        public void ShowStaticPreview07(string slot,DieInstance die,Vector2 uiCenter,Quaternion rotation,float size=1f)
        {
            if(string.IsNullOrEmpty(slot))throw new ArgumentException(nameof(slot));
            Visual v=DisplayInternal(StaticPreviewPrefix07+slot,die,null,uiCenter,size,true,false);
            v.pivot.localRotation=rotation;v.target=rotation;UpdateFaceVisibility(v);
        }
        public void HidePreview()
        {
            foreach(var pair in visuals)
                if(pair.Key==PreviewId||pair.Key.StartsWith(StaticPreviewPrefix07,StringComparison.Ordinal))pair.Value.root.SetActive(false);
        }

        private Visual DisplayInternal(string id, DieInstance d, DieHandState hand, Vector2 uiCenter, float size, bool preview, bool pending,bool dragged=false)
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
                v.layoutMoving = false; v.locked = v.pending07 = v.lockTrack07 = false;
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
            v.root.SetActive(true);
            var plane=new Plane(Vector3.up,Vector3.zero);
            Vector2 screen=designRoot!=null?RectTransformUtility.WorldToScreenPoint(null,designRoot.TransformPoint(new Vector3(uiCenter.x,-uiCenter.y,0))):new Vector2(uiCenter.x/1920f*Screen.width,(1-uiCenter.y/1080f)*Screen.height);
            Ray ray=viewCamera.ScreenPointToRay(screen);
            if (!plane.Raycast(ray,out float dist)) throw new InvalidOperationException("Table camera ray must intersect the dice plane.");
            Vector3 destination = ray.GetPoint(dist);
            if (pending && !preview)
            {
                // Six design pixels of world-space lift; the caller's pointer rectangle remains unchanged.
                float pixels = designRoot != null ? Mathf.Abs(designRoot.TransformVector(new Vector3(0, 6, 0)).y) : 6f * Screen.height / 1080f;
                float perUnit = Mathf.Abs(viewCamera.WorldToScreenPoint(destination + Vector3.up).y - viewCamera.WorldToScreenPoint(destination).y);
                if (perUnit > .001f) destination += Vector3.up * (pixels / perUnit);
            }
            bool changed = (destination - v.resting).sqrMagnitude > .0000001f || (Vector3.one * size - v.layoutScaleTo).sqrMagnitude > .0000001f;
            bool newlyLocked = hand != null && hand.isLocked && !v.locked;
            v.resting = destination;
            int current=FaceIndex(hand?.currentFaceId); v.target=Quaternion.AngleAxis(18,Vector3.up)*Quaternion.FromToRotation(Normals[current],Vector3.up);
            bool scheduledLock = newlyLocked && lockDelays07.ContainsKey(id);
            if (changed && v.hasDisplayed && !preview && !dragged && !preserveMotion && !v.lockTrack07 && !ReducedMotion && !UiKit.ReducedMotion)
            {
                v.layoutFrom = v.root.transform.position; v.layoutScaleFrom = v.root.transform.localScale;
                v.layoutRotationFrom = v.pivot.localRotation; v.layoutElapsed = 0;
                v.layoutDuration=scheduledLock?LockMoveSeconds07:pending!=v.pending07?(pending?PendingRiseSeconds07:PendingReturnSeconds07):LayoutDuration;
                v.lockTrack07=scheduledLock;
                v.layoutMoving = true; v.lockPunch = newlyLocked;
            }
            v.layoutScaleTo = Vector3.one * size;
            if (!v.hasDisplayed) v.previousCenter = v.resting;
            if (!v.layoutMoving || preview || dragged || ReducedMotion || UiKit.ReducedMotion)
            { v.layoutMoving = false; v.root.transform.position = v.resting; v.root.transform.localScale = v.layoutScaleTo; }
            if (!preserveMotion)
            {
                v.rollPivot.localPosition = Vector3.zero;
                v.rollPivot.localRotation = Quaternion.identity;
                v.rollPivot.localScale = Vector3.one;
                if (!v.layoutMoving) v.pivot.localRotation=v.target;
            }
            v.bodyFilter.sharedMesh=bodyMesh;
            v.outlineFilter.sharedMesh=bodyMesh;v.outlineRenderer.sharedMaterial=outlineMaterial;v.outlineRenderer.enabled=outlineMaterial!=null;
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
            v.locked=hand!=null && hand.isLocked;v.pending07=pending;
            v.clipIndex=Math.Max(0,d.createdSerial);
            v.hasDisplayed=true;
            UpdateFaceVisibility(v);
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
            var outline=new GameObject("InkContour",typeof(MeshFilter),typeof(MeshRenderer));outline.transform.SetParent(v.pivot,false);
            v.outlineFilter=outline.GetComponent<MeshFilter>();v.outlineRenderer=outline.GetComponent<MeshRenderer>();v.outlineRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            for(int f=0;f<6;f++)
            {
                var face=new GameObject("Face_F"+(f+1).ToString("00"),typeof(MeshFilter),typeof(MeshRenderer));face.transform.SetParent(v.pivot,false);
                face.transform.localPosition=Normals[f]*.502f;face.transform.localRotation=Quaternion.FromToRotation(Vector3.forward,Normals[f]);
                v.faces[f]=face.GetComponent<MeshFilter>();v.faceRenderers[f]=face.GetComponent<MeshRenderer>();
            }
            return v;
        }

        public void BeginRoll(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (seconds <= 0 || ReducedMotion || UiKit.ReducedMotion) { FinishRoll(); return; }
            EnsureMotionClips();
            foreach (var v in visuals.Values)
            {
                if (!v.root.activeSelf || !v.rolling) continue;
                Quaternion previous = v.displayFrame == Time.frameCount ? v.previousOrientation : v.rollPivot.localRotation*v.pivot.localRotation;
                Vector3 previousCenter = v.displayFrame == Time.frameCount ? v.previousCenter : v.rollPivot.position;
                v.layoutMoving=false;v.root.transform.position=v.resting;v.root.transform.localScale=v.layoutScaleTo;
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
                v.root.transform.localScale=v.layoutScaleTo;v.layoutMoving=false;
                UpdateFaceVisibility(v);
            }
        }

        public void FinishLayoutTransitions()
        {
            foreach(var v in visuals.Values)
            {
                if(!v.layoutMoving)continue;v.layoutMoving=false;v.root.transform.position=v.resting;
                v.root.transform.localScale=v.layoutScaleTo;v.pivot.localRotation=v.target;UpdateFaceVisibility(v);
            }
        }

        void Update()
        {
            if(Paused)return;
            TickLayoutTransitions07(Time.unscaledDeltaTime);
            if(!animate)return;elapsed+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(elapsed/duration);
            // Both speeds reserve the final 120ms for a fixed physical face. At Normal,
            // lift=0..120ms, authored roll/alignment=120..780ms, rest by820ms.
            float alignmentEnd=Mathf.Max(0,duration-.12f);
            float liftTime=Mathf.Min(.12f,alignmentEnd*.5f);
            float roll = alignmentEnd<=liftTime?1:Mathf.Clamp01((elapsed-liftTime)/(alignmentEnd-liftTime));
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
                float settleEnd=duration*(820f/900f);
                float baseLift=elapsed<liftTime?Mathf.SmoothStep(0,.16f,elapsed/Mathf.Max(.001f,liftTime)):
                    .16f*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-duration*(600f/900f))/Mathf.Max(.001f,settleEnd-duration*(600f/900f)))));
                if(elapsed>=alignmentEnd)rotation=Quaternion.identity;
                if(elapsed>=settleEnd){position=Vector3.zero;baseLift=0;}
                v.rollPivot.localRotation=rotation*Quaternion.Slerp(v.compensation,Quaternion.identity,alignment);
                v.rollPivot.localPosition=position+Vector3.up*baseLift+Vector3.Lerp(v.initialOffset,Vector3.zero,Mathf.SmoothStep(0,1,Mathf.Clamp01(t*9)));
                v.rollPivot.localScale=Vector3.one;
                UpdateFaceVisibility(v);
            }
            if(t>=1) FinishRoll();
        }

        private void UpdateFaceVisibility(Visual visual)
        {
            if(viewCamera==null||visual.pivot==null)return;
            Vector3 towardCamera=viewCamera.orthographic?-viewCamera.transform.forward:(viewCamera.transform.position-visual.pivot.position).normalized;
            for(int f=0;f<6;f++)visual.faceRenderers[f].enabled=Vector3.Dot(visual.pivot.TransformDirection(Normals[f]),towardCamera)>.001f;
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
                v.hasDisplayed=false;v.rolling=false;v.layoutMoving=false;v.root.name="PooledDie";
                if(spareVisuals.Count<33) spareVisuals.Push(v);else Destroy(v.root);
            }
        }
        private static Mesh MakeFace(int pip,bool gold,string texture)
        {
            var vertices=new List<Vector3>();var indices=new List<int>();var colors=new List<Color>();
            Action<float,float,float,Color> rawCircle=(x,y,r,color)=> { float z=color==Color.white?.001f:0;int s=vertices.Count; vertices.Add(new Vector3(x,y,z));colors.Add(color);
                for(int j=0;j<=16;j++){float a=j*Mathf.PI*2/16;vertices.Add(new Vector3(x+Mathf.Cos(a)*r,y+Mathf.Sin(a)*r,z));colors.Add(color);if(j>0){indices.Add(s);indices.Add(s+j);indices.Add(s+j+1);}} };
            Action<float,float,float,Color> circle=(x,y,r,color)=>{rawCircle(x,y,r+.009f,UiKit.Ink);rawCircle(x,y,r,color);};
            Action<float,float,float,float,Color> bar=(x,y,w,h,color)=> {int s=vertices.Count;vertices.Add(new Vector3(x,y,0));vertices.Add(new Vector3(x+w,y,0));vertices.Add(new Vector3(x+w,y+h,0));vertices.Add(new Vector3(x,y+h,0));for(int k=0;k<4;k++)colors.Add(color);indices.AddRange(new[]{s,s+1,s+2,s,s+2,s+3});};
            if(pip%2==1) circle(0,0,.064f,Color.white);
            if(pip>=2) { circle(-.22f,.22f,.064f,Color.white);circle(.22f,-.22f,.064f,Color.white); }
            if(pip>=4) { circle(.22f,.22f,.064f,Color.white);circle(-.22f,-.22f,.064f,Color.white); }
            if(pip==6) { circle(-.22f,0,.064f,Color.white);circle(.22f,0,.064f,Color.white); }
            // Frame and texture markers remain separate from the rule color/pip count.
            if(gold) { bar(-.42f,-.42f,.84f,.04f,UiKit.Gold);bar(-.42f,.38f,.84f,.04f,UiKit.Gold);bar(-.42f,-.42f,.04f,.84f,UiKit.Gold);bar(.38f,-.42f,.04f,.84f,UiKit.Gold); }
            // Texture marks stay outside the pip protection square and use neutral ink/paper,
            // never the face's red/blue tint. Stable corner marks remain legible when reduced.
            if(texture=="rough")
            {
                Color grain=Color.Lerp(UiKit.Paper,UiKit.Ink,.18f);
                for(int i=0;i<3;i++)rawCircle(-.29f+i*.07f,-.342f,.014f,UiKit.Ink);
                for(int i=0;i<7;i++){bar(-.35f,-.25f+i*.078f,.009f,.012f,grain);bar(.337f,-.22f+i*.072f,.012f,.009f,grain);}
            }
            if(texture=="smooth")
            {
                Color shine=Color.Lerp(UiKit.Paper,new Color(.96f,.99f,.95f),.7f);
                bar(-.34f,.315f,.38f,.06f,shine);bar(-.35f,-.28f,.04f,.27f,shine);
                bar(-.22f,-.35f,.13f,.014f,UiKit.Ink);bar(-.06f,-.328f,.13f,.014f,UiKit.Ink);bar(.1f,-.35f,.13f,.014f,UiKit.Ink);
            }
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
