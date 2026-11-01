using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private bool HasIncompatibleSave07=>ViewState==null&&saves!=null&&saves.LastIncompatibleSave!=null&&Session?.State==null;
        private string IncompatibleVersionSummary07()
        {
            var old=saves?.LastIncompatibleSave;
            return old==null?"旧版本存档":"旧版本存档 · "+(string.IsNullOrEmpty(old.ConfigVersion)?"未声明配置版本":old.ConfigVersion)+" / schema "+old.Schema;
        }
        private void OpenIncompatibleSave07(){ShowLoadIssue07(saves?.LastLoadNotice,false);}
        private void DrawLoadIssue07(string detail,bool recoveredBackup)
        {
            string message=detail??"读取信息未提供。";
            var legacy=!recoveredBackup&&HasIncompatibleSave07?saves.LastIncompatibleSave:null;
            var p=Art07Modal(recoveredBackup?"存档恢复说明":legacy!=null?"旧版本存档":"存档读取说明",480,180,960,720);
            var list=UiKit.Scroll(p,"LoadIssueDetails07",24,100,912,436,436);float y=0;
            Art07DetailSection(list,"RecoveryState",recoveredBackup?"已恢复可用检查点":"当前存档未能读取",recoveredBackup?"已使用通过验证的备份检查点。继续游戏会显示这个已保存状态，不会补播奖励或重新抽取结果。":"本次没有加载这份存档。原文件保持不变；关闭本页只返回菜单，不会开始新局或迁移数据。",ref y,880);
            if(legacy!=null)
            {
                string version="存档："+(string.IsNullOrEmpty(legacy.ConfigVersion)?"未声明配置版本":legacy.ConfigVersion)+" / schema "+legacy.Schema+"\n当前："+Config.configVersion+" / schema "+Config.saveSchemaVersion;
                Art07DetailSection(list,"LegacyVersions07","版本不兼容",version,ref y,880);
                Art07DetailSection(list,"LegacyPreservation07","保留旧存档",legacy.IsLegacyPreserved?"现有读取流程已保留旧版本备份。开始新局不会把旧档迁移为新规则。设置与历史不由此入口删除。":"原文件仍保留。确认创建新局时，现有保存流程会先保留被替换文件；取消新局不会写入任何内容。设置与历史不由此入口删除。",ref y,880);
            }
            Art07DetailSection(list,"RecoveryDetail","读取详情",message,ref y,880);list.sizeDelta=new Vector2(list.sizeDelta.x,y+16);
            UiKit.Button(p,"CopyLoadIssue07","复制读取详情",24,560,legacy==null?912:432,60,()=>{GUIUtility.systemCopyBuffer=(legacy==null?"":IncompatibleVersionSummary07()+"\n当前 "+Config.configVersion+" / schema "+Config.saveSchemaVersion+"\n")+message;});
            if(legacy!=null)UiKit.Button(p,"CloseLoadIssue07","返回主菜单",504,560,432,60,()=>{CloseModal();inMenu=true;Refresh();});
            if(legacy==null)UiKit.ActionButton(p,"CloseLoadIssue07","返回主菜单",24,644,912,60,()=>{CloseModal();inMenu=true;Refresh();},UiKit.Paper);
            else
            {
                var begin=UiKit.ActionButton(p,"LegacyNewRun07","保留备份并开始 v0.6 规则的新局",24,644,912,60,()=>{CloseModal();inMenu=true;Refresh();ShowNewRun07();},UiKit.Paper);
                // Opens the existing seed/confirmation UI only; this callback does not issue NewRun.
                var label=begin.GetComponentInChildren<TMPro.TextMeshProUGUI>();label.fontSize=22*UiKit.FontScale;
            }
        }
    }
}
