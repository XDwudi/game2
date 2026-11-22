using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        // v1.1 (DEV_PLAN 4.5 item 4, task W09): the old-version save explanation, its menu button state and the
        // "保留备份并开始新局" path are gone; an incompatible save is removed at startup and the menu shows the no-save state.
        // Current-version errors retain the full captured details even while the initial explanation is collapsed.
        private void DrawLoadIssue07(string detail,bool recoveredBackup,bool expanded=false)
        {
            string message=detail??"读取信息未提供。";
            var p=Art07Modal(recoveredBackup?"存档恢复说明":"存档读取说明",480,180,960,720,"load-issue16");
            var list=UiKit.Scroll(p,"LoadIssueDetails07",24,100,912,436,436);float y=0;
            ModalSection16(list,p,"RecoveryState",recoveredBackup?"已用自动备份恢复":"存档读不出来",recoveredBackup?"存档文件损坏，已改用上一份备份，可能回到稍早的进度。":"原文件没有改动。关闭此页回到主菜单。",ref y,880);
            if(expanded)ModalSection16(list,p,"RecoveryDetail","读取详情",message,ref y,880);
            list.sizeDelta=new Vector2(list.sizeDelta.x,Mathf.Max(436,y+16));
            UiKit.Button(p,"ToggleLoadIssue16",expanded?"收起读取详情":"展开读取详情",24,560,448,60,()=>DrawLoadIssue07(detail,recoveredBackup,!expanded));
            UiKit.Button(p,"CopyLoadIssue07","复制读取详情",488,560,448,60,()=>{GUIUtility.systemCopyBuffer=message;});
            UiKit.Button(p,"CloseLoadIssue07","返回主菜单",24,644,912,60,()=>{CloseModal();inMenu=true;Refresh();});
        }
    }
}
