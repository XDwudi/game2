using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        // v1.1 (DEV_PLAN 4.5 item 4, task W09): the old-version save explanation, its menu button state and the
        // "保留备份并开始新局" path are gone; an incompatible save is removed at startup and the menu shows the no-save state.
        // What remains is the current-version load explanation (a corrupt save, or a backup that was restored), unchanged.
        private void DrawLoadIssue07(string detail,bool recoveredBackup)
        {
            string message=detail??"读取信息未提供。";
            var p=Art07Modal(recoveredBackup?"存档恢复说明":"存档读取说明",480,180,960,720);
            var list=UiKit.Scroll(p,"LoadIssueDetails07",24,100,912,436,436);float y=0;
            Art07DetailSection(list,"RecoveryState",recoveredBackup?"已恢复可用检查点":"当前存档未能读取",recoveredBackup?"已使用通过验证的备份检查点。继续游戏会显示这个已保存状态，不会补播奖励或重新抽取结果。":"本次没有加载这份存档。原文件保持不变；关闭本页只返回菜单，不会开始新局或迁移数据。",ref y,880);
            Art07DetailSection(list,"RecoveryDetail","读取详情",message,ref y,880);list.sizeDelta=new Vector2(list.sizeDelta.x,y+16);
            UiKit.Button(p,"CopyLoadIssue07","复制读取详情",24,560,912,60,()=>{GUIUtility.systemCopyBuffer=message;});
            UiKit.ActionButton(p,"CloseLoadIssue07","返回主菜单",24,644,912,60,()=>{CloseModal();inMenu=true;Refresh();},UiKit.Paper);
        }
    }
}
