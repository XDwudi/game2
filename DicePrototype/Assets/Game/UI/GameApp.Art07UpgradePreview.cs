using DiceCraft.Core;
using DiceCraft.Presentation;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private Art07UpgradeSurface upgradeSurface07;
        private void BeginSpecialUpgradePreview07(DieInstance before,DieInstance after,string faceId,bool whole,Vector2 center,Quaternion rotation)
        {
            permanentTransition07?.Finish();
            if(upgradeSurface07==null){var go=new GameObject("CommittedUpgradeSurface07");go.transform.SetParent(transform,false);upgradeSurface07=go.AddComponent<Art07UpgradeSurface>();}
            upgradeSurface07.Finish();
            table.ShowPreview(before,center,rotation,2);
            var previous=table.GetFaceOrientation("$die-preview").Find("Face_"+faceId).GetComponent<MeshFilter>().sharedMesh;
            table.ShowPreview(after,center,rotation,2);var orientation=table.GetFaceOrientation("$die-preview");
            if(whole)upgradeSurface07.BeginGlass(orientation.Find("Body").GetComponent<MeshRenderer>(),before.isGlass?table.glassMaterial:table.bodyMaterial,after.isGlass?table.glassMaterial:table.bodyMaterial);
            else
            {
                var renderer=orientation.Find("Face_"+faceId).GetComponent<MeshRenderer>();var committed=renderer.GetComponent<MeshFilter>().sharedMesh;
                var color=after.faces[DiceTable.FaceIndex(faceId)].baseColor=="red"?UiKit.Red:UiKit.Blue;
                upgradeSurface07.BeginFaceFlip(renderer,previous,committed,color,UiKit.Paper,UiKit.Violet,table.viewCamera.transform.up,table.viewCamera.transform.right);
            }
        }
    }
}
