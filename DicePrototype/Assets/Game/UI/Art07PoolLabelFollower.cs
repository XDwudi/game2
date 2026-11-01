using UnityEngine;

namespace DiceCraft
{
    /// <summary>Keeps an unlocked die's annotation beside its actual pose during lock-in reflow.</summary>
    public sealed class Art07PoolLabelFollower : MonoBehaviour
    {
        private DiceTable table;
        private Transform pose;
        private RectTransform label;
        private Vector2 rest, target;

        public void Bind(DiceTable owner,string dieId,Vector2 center,bool follow)
        {
            label=(RectTransform)transform;rest=label.anchoredPosition;
            table=owner;pose=follow?owner.GetFaceOrientation(dieId):null;
            target=new Vector2(center.x,-center.y);enabled=follow;
            if(follow)Align();
        }
        private void LateUpdate()=>Align();
        private void Align()
        {
            if(pose==null||table==null||table.designRoot==null||table.viewCamera==null)return;
            Vector3 screen=table.viewCamera.WorldToScreenPoint(pose.parent.parent.position);
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(table.designRoot,screen,null,out Vector2 actual))
                label.anchoredPosition=rest+actual-target;
        }
    }
}
