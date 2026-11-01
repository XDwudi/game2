using System;
using System.Collections.Generic;
using UnityEngine;

namespace DiceCraft.Presentation
{
    /// <summary>Read-only renderer for a committed upgrade. No rule state, RNG, or clock of its own.</summary>
    public sealed class Art07UpgradeSurface : MonoBehaviour
    {
        private MeshRenderer body, cover, turning, question;
        private MaterialPropertyBlock originalBody,bodyProperties,faceProperties;
        private Color beforeColor,afterColor;private float beforeGlass,afterGlass;
        private Mesh beforePatch,afterPatch,coverMesh,questionMesh;
        private Transform targetFace;
        public bool IsActive { get; private set; }
        public bool IsGlassTransition { get; private set; }
        public float Progress { get; private set; }
        public float TurnDegrees { get; private set; }
        public string VisibleFaceStage { get; private set; }="";

        private void Awake()
        {
            originalBody=new MaterialPropertyBlock();bodyProperties=new MaterialPropertyBlock();faceProperties=new MaterialPropertyBlock();
        }

        public void BeginGlass(MeshRenderer renderer,Material previous,Material committed)
        {
            Finish();if(renderer==null||previous==null||committed==null)throw new ArgumentNullException(nameof(renderer));
            body=renderer;body.GetPropertyBlock(originalBody);body.GetPropertyBlock(bodyProperties);
            beforeColor=previous.GetColor("_BaseColor");afterColor=committed.GetColor("_BaseColor");
            beforeGlass=previous.GetFloat("_Glass");afterGlass=committed.GetFloat("_Glass");
            IsGlassTransition=true;IsActive=true;SetProgress(0,false);
        }

        public void BeginFaceFlip(MeshRenderer physical,Mesh previous,Mesh committed,Color pipColor,Color paper,Color violet,Vector3 screenUp,Vector3 screenRight)
        {
            Finish();if(physical==null||previous==null||committed==null)throw new ArgumentNullException(nameof(physical));
            EnsureViews();targetFace=physical.transform;
            // The opaque cover hides only the pip protection square. Gold/texture markers
            // remain on the original face outside it; the other five faces never move.
            BuildPatch(beforePatch,previous,paper,false);BuildPatch(afterPatch,committed,paper,true);
            BuildCover(coverMesh,paper);BuildQuestion(questionMesh,violet);
            foreach(var r in new[]{cover,turning,question})
            {
                r.transform.SetParent(targetFace,false);r.transform.localRotation=Quaternion.identity;r.transform.localScale=Vector3.one;
                r.sharedMaterial=physical.sharedMaterial;faceProperties.Clear();faceProperties.SetColor("_BaseColor",pipColor);r.SetPropertyBlock(faceProperties);
            }
            cover.transform.localPosition=new Vector3(0,0,.008f);question.transform.localPosition=new Vector3(0,0,.013f);
            var localUp=targetFace.InverseTransformDirection(screenUp);localUp.z=0;
            question.transform.localRotation=Quaternion.AngleAxis(Vector3.SignedAngle(Vector3.up,localUp,Vector3.forward),Vector3.forward);
            question.transform.localScale=new Vector3(Vector3.Dot(question.transform.right,screenRight)<0?-1:1,1,1);
            cover.GetComponent<MeshFilter>().sharedMesh=coverMesh;question.GetComponent<MeshFilter>().sharedMesh=questionMesh;
            IsGlassTransition=false;IsActive=true;SetProgress(0,false);
        }

        public void SetProgress(float progress,bool reducedMotion)
        {
            if(!IsActive)return;Progress=Mathf.Clamp01(progress);
            if(IsGlassTransition)
            {
                if(body==null){Finish();return;}
                // Interpolate the actual ceramic base too. Setting _Glass on a material
                // already coloured cyan made the previous first frame look like glass.
                bodyProperties.SetColor("_BaseColor",Color.Lerp(beforeColor,afterColor,Progress));
                bodyProperties.SetFloat("_Glass",Mathf.Lerp(beforeGlass,afterGlass,Progress));body.SetPropertyBlock(bodyProperties);return;
            }
            if(targetFace==null){Finish();return;}
            bool old=Progress<.35f,unknown=Progress>=.35f&&Progress<.60f,done=Progress>=1;
            VisibleFaceStage=done?"CommittedPips":old?"OldPips":unknown?"Question":"CommittedPips";
            // One monotonic half-turn, with an edge-on pause while the question stamp
            // is visible. No cross-fade of old and new pips, and no invented intermediate face.
            TurnDegrees=reducedMotion?0:old?90*Mathf.SmoothStep(0,1,Progress/.35f):unknown?90:90+90*Mathf.SmoothStep(0,1,(Progress-.60f)/.40f);
            cover.gameObject.SetActive(!done);question.gameObject.SetActive(unknown&&!done);turning.gameObject.SetActive(!unknown&&!done);
            turning.GetComponent<MeshFilter>().sharedMesh=old?beforePatch:afterPatch;
            // The committed patch was built on the reverse. Reduced motion uses a
            // static front-facing result and still preserves the question/reveal sequence.
            float visibleAngle=reducedMotion?(old?0:180):TurnDegrees;
            turning.transform.localRotation=Quaternion.AngleAxis(visibleAngle,Vector3.up);
            float lift=reducedMotion?0:.36f*Mathf.Sin(TurnDegrees*Mathf.Deg2Rad);
            turning.transform.localPosition=new Vector3(0,0,.012f+lift);
        }

        private void EnsureViews()
        {
            if(cover!=null)return;
            cover=Create("UpgradePipCover07");turning=Create("UpgradeTurningPatch07");question=Create("UpgradeQuestion07");
            beforePatch=NewMesh("Upgrade previous pip patch");afterPatch=NewMesh("Upgrade committed pip patch");
            coverMesh=NewMesh("Upgrade pip cover");questionMesh=NewMesh("Upgrade question stamp");
        }
        private MeshRenderer Create(string name)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);
            var renderer=go.GetComponent<MeshRenderer>();renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;go.SetActive(false);return renderer;
        }
        private static Mesh NewMesh(string name){var mesh=new Mesh{name=name};mesh.MarkDynamic();return mesh;}
        private static void Quad(List<Vector3> v,List<int> t,List<Color> c,float x,float y,float w,float h,float z,Color color)
        {
            int n=v.Count;v.Add(new Vector3(x,y,z));v.Add(new Vector3(x+w,y,z));v.Add(new Vector3(x+w,y+h,z));v.Add(new Vector3(x,y+h,z));
            t.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});for(int i=0;i<4;i++)c.Add(color);
        }
        private static void Fill(Mesh mesh,List<Vector3> v,List<int> t,List<Color> c)
        {mesh.Clear();mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.SetColors(c);mesh.RecalculateNormals();mesh.RecalculateBounds();}
        private static void BuildCover(Mesh mesh,Color paper)
        {var v=new List<Vector3>();var t=new List<int>();var c=new List<Color>();Quad(v,t,c,-.315f,-.315f,.63f,.63f,0,paper);Fill(mesh,v,t,c);}
        private static void BuildPatch(Mesh mesh,Mesh source,Color paper,bool reverse)
        {
            var v=new List<Vector3>();var t=new List<int>();var c=new List<Color>();Quad(v,t,c,-.315f,-.315f,.63f,.63f,0,paper);
            var sv=source.vertices;var sc=source.colors;var st=source.triangles;
            for(int i=0;i<st.Length;i+=3)
            {
                bool inside=true;for(int k=0;k<3;k++){var p=sv[st[i+k]];inside&=Mathf.Abs(p.x)<.31f&&Mathf.Abs(p.y)<.31f;}
                if(!inside)continue;
                for(int k=0;k<3;k++){int s=st[i+k];t.Add(v.Count);v.Add(sv[s]+Vector3.forward*.002f);c.Add(sc.Length==sv.Length?sc[s]:Color.white);}
            }
            if(reverse)for(int i=0;i<v.Count;i++)v[i]=Quaternion.AngleAxis(180,Vector3.up)*v[i];Fill(mesh,v,t,c);
        }
        private static void BuildQuestion(Mesh mesh,Color violet)
        {
            // A small pixel-print question stamp, geometry rather than font/raster dependency.
            string[] cells={"01110","11011","00011","00110","00100","00000","00100"};
            var v=new List<Vector3>();var t=new List<int>();var c=new List<Color>();const float cell=.048f;
            for(int y=0;y<7;y++)for(int x=0;x<5;x++)if(cells[y][x]=='1')Quad(v,t,c,(x-2.5f)*cell,(2.5f-y)*cell,cell,cell,0,violet);Fill(mesh,v,t,c);
        }
        public void Finish()
        {
            if(body!=null)body.SetPropertyBlock(originalBody);body=null;targetFace=null;IsActive=false;Progress=1;TurnDegrees=0;VisibleFaceStage="";
            foreach(var renderer in new[]{cover,turning,question})if(renderer!=null)
            {renderer.gameObject.SetActive(false);if(isActiveAndEnabled)renderer.transform.SetParent(transform,false);renderer.transform.localRotation=Quaternion.identity;renderer.transform.localPosition=Vector3.zero;}
        }
        private void OnDisable()=>Finish();
        private void OnDestroy()
        {
            Finish();foreach(var r in new[]{cover,turning,question})if(r!=null)Destroy(r.gameObject);
            foreach(var mesh in new[]{beforePatch,afterPatch,coverMesh,questionMesh})if(mesh!=null)Destroy(mesh);
        }
    }
}
