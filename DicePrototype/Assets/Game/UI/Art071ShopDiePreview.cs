using System;
using DiceCraft.Core;
using UnityEngine;

namespace DiceCraft
{
    /// <summary>Positions one permanent-data preview in a transparent shop aperture.</summary>
    public sealed class Art071ShopDiePreview : MonoBehaviour
    {
        private DiceTable table;
        private RectTransform aperture,viewport;
        private DieInstance snapshot;
        private string slot,previewId;
        private Func<bool> canShow;
        private Action<string,DieInstance,Vector2> place;
        private Matrix4x4 lastMatrix;
        private Matrix4x4 lastCameraMatrix;
        private Vector2 lastScreen;
        private CanvasGroup[] opacityGroups;
        private bool hasPose;
        private readonly Vector3[] corners=new Vector3[4];

        public void Bind(DiceTable owner,RectTransform window,RectTransform clip,string key,DieInstance die,
            Func<bool> visible,Action<string,DieInstance,Vector2> render)
        {
            HideOwnPreview();table=owner;aperture=window;viewport=clip;slot=key;snapshot=die;
            previewId="$static-preview:"+key;
            canShow=visible;place=render;hasPose=false;opacityGroups=null;enabled=true;
        }

        private void LateUpdate()
        {
            if(table==null||aperture==null||snapshot==null||place==null||canShow?.Invoke()!=true||!ReadyToShow())
            {HideOwnPreview();return;}
            var pose=table.GetFaceOrientation(previewId);
            var screen=new Vector2(Screen.width,Screen.height);
            var matrix=aperture.localToWorldMatrix;
            var cameraMatrix=table.viewCamera.projectionMatrix*table.viewCamera.worldToCameraMatrix;
            if(!hasPose||matrix!=lastMatrix||cameraMatrix!=lastCameraMatrix||screen!=lastScreen||pose==null||!pose.gameObject.activeInHierarchy)
            {
                var point=aperture.TransformPoint(aperture.rect.center);
                var local=table.designRoot.InverseTransformPoint(point);
                place(slot,snapshot,new Vector2(local.x,-local.y));
                lastMatrix=matrix;lastCameraMatrix=cameraMatrix;lastScreen=screen;hasPose=true;
            }
        }

        private bool ReadyToShow()
        {
            if(!aperture.gameObject.activeInHierarchy||table.designRoot==null||table.viewCamera==null)return false;
            // The first-visit card is still translating/fading until alpha 1. Start the mesh
            // after that short shared entrance, then follow its real position while scrolling.
            // Cache after BindShopEntrance07 has added its CanvasGroup in this same UI rebuild.
            if(opacityGroups==null)opacityGroups=aperture.GetComponentsInParent<CanvasGroup>();
            foreach(var group in opacityGroups)if(group!=null&&group.alpha<.999f)return false;
            if(viewport==null)return true;
            // A Canvas Mask does not clip main-camera meshes. Hide the sample whenever its
            // aperture is partially outside the scroll viewport, rather than spill onto tabs.
            aperture.GetWorldCorners(corners);
            var area=viewport.rect;
            foreach(var corner in corners)
            {
                var p=viewport.InverseTransformPoint(corner);
                if(p.x<area.xMin+.5f||p.x>area.xMax-.5f||p.y<area.yMin+.5f||p.y>area.yMax-.5f)return false;
            }
            return true;
        }

        private void HideOwnPreview()
        {
            hasPose=false;
            if(table==null||string.IsNullOrEmpty(slot))return;
            var pose=table.GetFaceOrientation(previewId);if(pose==null)return;
            // Current DiceTable hierarchy is root / RollPivot / FaceOrientation. Walk only
            // this named preview's ancestry; never disable the table or another pooled slot.
            var root=pose;
            while(root.parent!=null&&root.parent!=table.transform)root=root.parent;
            if(root.parent==table.transform&&root.name==previewId)root.gameObject.SetActive(false);
        }
        private void OnDisable(){HideOwnPreview();}
        private void OnDestroy(){HideOwnPreview();}
    }
}
