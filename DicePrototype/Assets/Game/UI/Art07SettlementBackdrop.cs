using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using DiceCraft.Presentation;

namespace DiceCraft
{
    /// <summary>Owned render copies only. No game state, commands, input handlers or clock.</summary>
    public sealed class Art07SettlementBackdrop : MonoBehaviour
    {
        private sealed class FrozenMesh
        {
            public GameObject node;
            public MeshFilter filter;
            public MeshRenderer renderer;
            public MaterialPropertyBlock properties;
            public Vector2 designPosition;
            public float planeY;
        }
        private readonly List<Art07TransactionMotion> groups=new List<Art07TransactionMotion>();
        private readonly List<FrozenMesh> diceMeshes=new List<FrozenMesh>();
        private Transform world;
        private DiceTable table;
        private int activeGroups,activeDiceMeshes;
        public int GraphicCount=>groups.Take(activeGroups).Sum(g=>g.CapturedGraphicCount);
        public int DiceMeshCount=>activeDiceMeshes;
        public int CreatedUiMeshCount=>groups.Sum(g=>g.CreatedSnapshotMeshes);
        public int CreatedDiceMeshCount=>diceMeshes.Count;
        public bool Visible { get; private set; }
        public bool HasCapture=>GraphicCount>0||activeDiceMeshes>0;

        public void Capture(IEnumerable<RectTransform> roots,IEnumerable<Transform> orientations,Transform worldParent)
        {
            Clear();gameObject.SetActive(true);table=worldParent.GetComponent<DiceTable>();
            var selected=roots.Where(r=>r!=null&&r.gameObject.activeInHierarchy).Distinct().ToArray();
            var batch=new List<RectTransform>();int count=0;
            foreach(var root in selected)
            {
                // Preserve the existing transaction snapshot limit for every group.
                int n=root.GetComponentsInChildren<Graphic>(false).Length;
                if(n>256)throw new InvalidOperationException("Settlement backdrop root exceeds 256 graphics: "+root.name);
                if(count+n>256){CaptureGroup(batch);batch.Clear();count=0;}
                batch.Add(root);count+=n;
            }
            if(batch.Count>0)CaptureGroup(batch);
            if(world==null)
            {
                world=new GameObject("SettlementFrozenDice07").transform;
                world.SetParent(worldParent,false);
            }
            world.gameObject.SetActive(true);
            foreach(var pose in orientations.Where(p=>p!=null&&p.gameObject.activeInHierarchy).Distinct())
                foreach(var source in pose.GetComponentsInChildren<MeshRenderer>(false))
                {
                    var filter=source.GetComponent<MeshFilter>();
                    if(!source.enabled||filter==null||filter.sharedMesh==null)continue;
                    var item=GetDiceMesh(activeDiceMeshes++);
                    item.node.name=pose.parent.parent.name+"/"+source.name;
                    item.node.layer=source.gameObject.layer;
                    item.node.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
                    item.planeY=source.transform.position.y;
                    if(table!=null&&table.designRoot!=null)
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(table.designRoot,table.viewCamera.WorldToScreenPoint(source.transform.position),null,out item.designPosition);
                    Vector3 parentScale=world.lossyScale,s=source.transform.lossyScale;
                    item.node.transform.localScale=new Vector3(s.x/parentScale.x,s.y/parentScale.y,s.z/parentScale.z);
                    // DiceTable's face cache and imported body meshes are immutable render assets.
                    item.filter.sharedMesh=filter.sharedMesh;
                    item.renderer.sharedMaterials=source.sharedMaterials;
                    source.GetPropertyBlock(item.properties);item.renderer.SetPropertyBlock(item.properties);
                    item.renderer.shadowCastingMode=source.shadowCastingMode;
                    item.renderer.receiveShadows=source.receiveShadows;
                    item.renderer.lightProbeUsage=source.lightProbeUsage;
                    item.renderer.reflectionProbeUsage=source.reflectionProbeUsage;
                    item.renderer.sortingLayerID=source.sortingLayerID;item.renderer.sortingOrder=source.sortingOrder;
                    item.node.SetActive(true);
                }
            Hide();
        }
        private void CaptureGroup(List<RectTransform> roots)
        {
            if(roots.Count==0)return;
            if(activeGroups==groups.Count)
            {
                var node=new GameObject("FrozenUiGroup"+groups.Count,typeof(RectTransform),typeof(CanvasGroup),typeof(Art07TransactionMotion));
                var rect=(RectTransform)node.transform;rect.SetParent(transform,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.sizeDelta=new Vector2(1920,1080);
                var alpha=node.GetComponent<CanvasGroup>();alpha.interactable=false;alpha.blocksRaycasts=false;
                groups.Add(node.GetComponent<Art07TransactionMotion>());
            }
            var group=groups[activeGroups++];group.gameObject.SetActive(true);group.Prepare(roots);
        }
        private FrozenMesh GetDiceMesh(int index)
        {
            if(index<diceMeshes.Count)return diceMeshes[index];
            var node=new GameObject("FrozenDieMesh",typeof(MeshFilter),typeof(MeshRenderer));node.transform.SetParent(world,false);
            var item=new FrozenMesh{node=node,filter=node.GetComponent<MeshFilter>(),renderer=node.GetComponent<MeshRenderer>(),properties=new MaterialPropertyBlock()};
            diceMeshes.Add(item);return item;
        }
        public void Show()
        {
            gameObject.SetActive(true);Visible=true;
            for(int i=0;i<activeGroups;i++){groups[i].gameObject.SetActive(true);groups[i].HoldPrepared();}
            // A display-mode/viewport change may move the design root. Reproject frozen
            // geometry anchors; preserve each recorded physical pose, scale and face mesh.
            if(table!=null&&table.designRoot!=null&&table.viewCamera!=null)
                for(int i=0;i<activeDiceMeshes;i++)
                {
                    var item=diceMeshes[i];var screen=RectTransformUtility.WorldToScreenPoint(null,table.designRoot.TransformPoint(item.designPosition));
                    var ray=table.viewCamera.ScreenPointToRay(screen);var plane=new Plane(Vector3.up,new Vector3(0,item.planeY,0));
                    if(plane.Raycast(ray,out float distance))item.node.transform.position=ray.GetPoint(distance);
                }
            if(world!=null)world.gameObject.SetActive(true);
        }
        public void Hide()
        {
            Visible=false;
            foreach(var group in groups)group.gameObject.SetActive(false);
            if(world!=null)world.gameObject.SetActive(false);
        }
        public void Clear()
        {
            Hide();foreach(var group in groups)group.DiscardPrepared();
            foreach(var mesh in diceMeshes){mesh.node.SetActive(false);mesh.filter.sharedMesh=null;mesh.renderer.sharedMaterials=Array.Empty<Material>();mesh.renderer.SetPropertyBlock(null);}
            activeGroups=activeDiceMeshes=0;
        }
        private void OnDisable()=>Hide();
        private void OnDestroy(){if(world!=null)Destroy(world.gameObject);}
    }
}
