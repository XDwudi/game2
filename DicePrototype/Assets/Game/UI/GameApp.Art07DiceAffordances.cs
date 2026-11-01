using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private bool CanMoveLockedDice07=>!modal&&!IsBusy&&Session!=null&&!Session.HasPendingSave&&Session.PendingIds.Length==0&&ViewState?.phase=="Selecting";
        private void DrawLockedGrip07(RectTransform hit,string id,bool compact)
        {
            if(!CanMoveLockedDice07)return;
            // Decoration inside the existing die hit rectangle. There is no extra
            // raycast target, so the same real DiePointer remains the drag owner.
            float width=compact?20:10,height=compact?32:24;
            var grip=UiKit.Rect(hit,"LockedGrip_"+id,compact?14:1,(hit.rect.height-height)*.5f,width,height);
            float dot=compact?4:3;
            for(int y=0;y<3;y++)for(int x=0;x<2;x++)
                UiKit.Box(grip,"Dot"+x+y,x*(width-dot),y*(height-dot)/2,dot,dot,UiKit.Paper,false);
        }
        private void SyncLockedGripsAfterModal07()
        {
            if(main==null)return;
            bool canMove=CanMoveLockedDice07&&ViewState.hand!=null;
            // Reconcile only the decoration on the already laid-out, visible hit targets.
            // Closing a paused presentation must not redraw dice, reset its clock or reveal the final preview.
            foreach(var pointer in main.GetComponentsInChildren<DiePointer>(false))
            {
                if(!pointer.name.StartsWith("Die_",System.StringComparison.Ordinal))continue;
                string id=pointer.name.Substring(4);var hit=(RectTransform)pointer.transform;
                if(canMove&&ViewState.hand.orderedLockedIds.Contains(id))DrawLockedGrip07(hit,id,Compact07);
                else
                {
                    var grip=hit.Find("LockedGrip_"+id);if(grip!=null)grip.gameObject.SetActive(false);
                }
            }
        }

        private void DrawPendingCheck07(string id,float x,float y,float height,bool compact)
        {
            float size=compact?22:24;
            var check=UiKit.Rect(main,"PendingCheck_"+id,x,y+(height-size)*.5f,size,size);
            var graphic=UiKit.Component<Art07HollowCheck>(check.gameObject);graphic.color=UiKit.Gold;graphic.raycastTarget=false;
        }
    }
}
