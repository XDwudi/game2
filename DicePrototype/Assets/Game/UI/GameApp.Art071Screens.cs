using System;
using System.Linq;
using DiceCraft.Core;
using TMPro;
using UnityEngine;

namespace DiceCraft
{
    // Page composition only. All actions keep the existing session and save paths.
    public sealed partial class GameApp
    {
        private void Menu071()
        {
            // The Image2 concept supplies composition; all ink, cards and dice remain live views.
            var mark=UiKit.PaperFrame(main,"MenuWordmark",128,86,1000,296,UiKit.Paper);
            UiKit.Box(mark,"RegistrationRule",34,24,932,2,UiKit.Ink);
            var title=UiKit.Component<UnityEngine.UI.Image>(UiKit.Rect(main,"Title",166,120,924,174).gameObject);
            title.sprite=UiKit.Template("ui_wordmark_dicecraft");title.preserveAspect=true;title.raycastTarget=false;
            UiKit.Component<Art072IdleFloat>(title.gameObject).Configure(4,3.2f,0);
            UiKit.Text(main,"Subtitle","把一次投掷，变成你的构筑。",166,298,924,74,25,UiKit.Ink,TextAlignmentOptions.Center);
            UiKit.Text(main,"MenuRules","投掷  →  选择  →  锁定  →  提交",166,382,924,48,21,UiKit.Paper,TextAlignmentOptions.Center);

            // Card stock, controller faces and real ceramic dice share the battle assets.
            // Overlap is composed in the cards; the front dice remain unobscured by Canvas ink.
            float[] cardX={164,374,602,824},cardY={484,450,460,490},angles={12,5,-5,-12};
            for(int i=0;i<4;i++)
            {
                var card=UiKit.PaperFrame(main,"MenuCardBack"+i,cardX[i],cardY[i],218,280,UiKit.Paper);
                card.GetComponent<UnityEngine.UI.Image>().sprite=UiKit.Template("ui_card_controller");
                card.localRotation=Quaternion.Euler(0,0,angles[i]);
                if(i==1||i==2)DrawControllerFullBody07(card,i==1?"C48":"C29","控制器",null);
                else
                {
                    var back=UiKit.Component<UnityEngine.UI.Image>(UiKit.Rect(card,"PrintedCardBack",4,4,210,272).gameObject);
                    back.sprite=UiKit.Template("ui_card_back");back.preserveAspect=true;back.raycastTarget=false;
                }
            }
            var centers=new[]{new Vector2(332,880),new Vector2(640,894),new Vector2(976,882)};
            for(int i=0;i<3;i++)
            {
                var die=DiceFactory.CreateStandard(Config,i+1);die.dieId="$menu"+i;
                var face=die.faces.First(f=>f.permanentPip==i*2+1);
                table.Display(die,new DieHandState{dieId=die.dieId,currentFaceId=face.faceId,currentPip=face.permanentPip,currentColor=face.baseColor,hasRolled=true,isLocked=true},centers[i],1.65f);
            }
            UiKit.Text(main,"LockHint","确认锁定后，本手不可直接解除。",166,992,924,48,16,UiKit.Muted,TextAlignmentOptions.Center);

            var p=UiKit.PaperFrame(main,"MenuPanel",1216,76,616,928,UiKit.Paper);
            var heading=UiKit.Text(p,"MenuLabel","一桌新的可能",32,24,552,68,30,UiKit.Ink,TextAlignmentOptions.Center);
            heading.fontStyle=FontStyles.Bold;
            UiKit.Box(p,"MenuRule",40,108,536,2,Color.Lerp(UiKit.Paper,UiKit.Ink,.6f));
            bool hasSave=ViewState!=null||HasIncompatibleSave07;
            UiKit.ActionButton(p,"ContinueRun",HasIncompatibleSave07?"旧版本存档":"继续对局",36,148,544,88,ContinueRun07,UiKit.Gold,hasSave);
            string saved=ViewState==null?(HasIncompatibleSave07?IncompatibleVersionSummary07():"暂无存档 · 开始你的第一局"):
                $"第 {ViewState.stage.stageIndex+1} 关 · {ViewState.gold} 金币\n{(ViewState.debugWorkspace?"调试":"普通")}对局 · 种子 {ViewState.seed}";
            UiKit.Text(p,"SavedSeed",saved,48,236,520,106,20,PaperMuted071,TextAlignmentOptions.Center);
            if(hasSave)UiKit.Button(p,"NewRun","新建挑战",36,342,544,80,ShowNewRun07);
            else UiKit.ActionButton(p,"NewRun","新建挑战",36,342,544,80,ShowNewRun07,UiKit.Blue);
            UiKit.Button(p,"MenuHandbook","牌型手册",36,442,544,80,ShowMenuHandbook07);
            UiKit.Button(p,"MenuSettings","设置",36,542,544,80,ShowSettings07);
            UiKit.Button(p,"Quit","退出游戏",36,642,544,80,ConfirmQuit07);
            var identityAsset=Resources.Load<TextAsset>("BuildIdentity");
            var identity=identityAsset==null?null:JsonUtility.FromJson<BuildIdentity>(identityAsset.text);
            string build=Application.isEditor?null:identity?.sourceCommit;
            build=string.IsNullOrEmpty(build)?"开发中":build.Substring(0,Math.Min(8,build.Length));
            UiKit.Text(main,"Version",Config.configVersion+" / "+ArtVersion+"\n构建 "+build,1264,804,520,96,16,PaperMuted071,TextAlignmentOptions.Center);
            var version=UiKit.FlatButton(main,"BuildVersion","版本与构建",1264,908,520,64,ShowBuildVersion);
            version.GetComponentInChildren<TextMeshProUGUI>().color=UiKit.Ink;
            version.transform.Find("Divider").GetComponent<UnityEngine.UI.Image>().color=PaperMuted071;
            DrawNotice07();
            var veil=UiKit.Box(main,"MenuEntranceVeil",0,0,1920,1080,UiKit.Background).GetComponent<UnityEngine.UI.Image>();veil.transform.SetAsLastSibling();
            UiKit.Component<Art07MenuReveal>(main.gameObject).Bind("menu:"+(ViewState?.runId??"new")+":"+(ViewState?.actionSequence??0),p,veil,
                (RectTransform)p.Find("ContinueRun"),(RectTransform)p.Find("NewRun"),(RectTransform)p.Find("MenuHandbook"),(RectTransform)p.Find("MenuSettings"),(RectTransform)p.Find("Quit"));
        }

        private void StagePreview071()
        {
            DrawControllers07();
            for(int chapter=0;chapter<8;chapter++)
            {
                float left=380+chapter*188;bool currentChapter=chapter==ViewState.stage.stageIndex/3;
                var caption=UiKit.Text(main,"StageChapter"+chapter,"第 "+(chapter+1)+" 章",left,340,164,40,17,currentChapter?UiKit.Gold:UiKit.Muted,TextAlignmentOptions.Center);
                caption.fontStyle=currentChapter?FontStyles.Bold:FontStyles.Normal;
                UiKit.Box(main,"StageChapterRule"+chapter,left,412,164,currentChapter?3:1,currentChapter?UiKit.Gold:UiKit.PanelLight);
                for(int within=0;within<3;within++)
                {
                    int index=chapter*3+within;float x=left+within*56;
                    bool complete=index<ViewState.stage.stageIndex,current=index==ViewState.stage.stageIndex;
                    Color fill=current?UiKit.Gold:complete?Color.Lerp(UiKit.Felt,UiKit.Teal,.35f):UiKit.Felt;
                    UiKit.Box(main,"StageTick"+index,x,380,48,28,fill);
                    UiKit.Text(main,"StageTickLabel"+index,complete?"✓":(index+1).ToString(),x,380,48,28,12,current?UiKit.Ink:complete?UiKit.Teal:UiKit.Muted,TextAlignmentOptions.Center);
                    if(current)UiKit.Outline(main,"CurrentStageOutline",x-2,378,52,32,UiKit.Gold,1);
                }
            }
            var def=Config.stages[ViewState.stage.stageIndex];float x0=def.riskEnabled?464:816;
            ChallengeCard071("Normal","普通挑战",tutorial?"50":def.normalTarget,def.normalReward,x0,false);
            if(def.riskEnabled)ChallengeCard071("Risk","高压挑战",def.riskTarget,def.riskReward,1056,true);
            UiKit.Text(main,"PreviewBudgets",$"投掷 {Session.InitialRolls} 次 / 手     提交 {Session.InitialSubmissions} 次 / 关     容量 K {Session.EffectiveCapacity}",416,830,1380,58,26,UiKit.Muted);
            UiKit.Text(main,"ControllerOrderWarning","入关后，控制器顺序固定。",416,890,1380,40,20,UiKit.Gold);
            UiKit.ActionButton(main,"StartStage","开始挑战 →",816,936,720,76,StartPreviewStage07,UiKit.Blue);
            BindStageCardsEntrance07();
        }

        private void ChallengeCard071(string id,string title,string score,int reward,float x,bool risk)
        {
            bool selected=PreviewRisk07==risk;
            var p=UiKit.PaperFrame(main,id+"Card",x,432,480,360,UiKit.Paper,UiKit.Ink);
            if(selected)UiKit.Outline(p,"SelectedContract",-4,-4,488,368,UiKit.Gold,3);
            UiKit.Box(p,"ContractRule",24,96,432,2,Color.Lerp(UiKit.Ink,UiKit.Paper,.65f));
            var heading=UiKit.Text(p,"Title",title,24,24,300,68,30,UiKit.Ink);heading.fontStyle=FontStyles.Bold;
            if(risk)
            {
                UiKit.Box(p,"RiskHeader",24,16,432,4,UiKit.Red);
                for(int i=0;i<14;i++)
                {
                    var stripe=UiKit.Box(p,"RiskStripe"+i,460,106+i*11,8,2,UiKit.Red);stripe.localRotation=Quaternion.Euler(0,0,-45);
                }
                var stamp=UiKit.Rect(p,"RiskSeal",348,32,104,46);stamp.localRotation=Quaternion.Euler(0,0,-6);
                UiKit.Outline(stamp,"StampBorder",0,0,104,46,UiKit.Danger,2);
                UiKit.Text(stamp,"RiskTag","高压",4,0,96,46,18,UiKit.Danger,TextAlignmentOptions.Center);
            }
            else
            {
                var seal=UiKit.Rect(p,"DiceTableSeal",376,32,64,52);
                UiKit.Outline(seal,"SealBorder",0,0,64,52,UiKit.Ink,2);
                foreach(var point in new[]{new Vector2(13,13),new Vector2(43,13),new Vector2(28,28)})UiKit.Box(seal,"Pip"+point.x+"_"+point.y,point.x,point.y,8,8,UiKit.Ink);
            }
            UiKit.Text(p,"GoalLabel","目标分数",24,110,432,40,18,PaperMuted071);
            string compact=CompactScore(score);
            var target=UiKit.Text(p,"Goal",compact,24,144,432,82,compact.Length>10?30:UiKit.FontScale>1?34:44,UiKit.Ink);
            target.fontStyle=FontStyles.Bold;target.textWrappingMode=TextWrappingModes.NoWrap;
            UiKit.Text(p,"Reward","基础奖励  +"+reward+" 金币",24,224,432,54,23,UiKit.Ink);
            var select=UiKit.FlatButton(p,id,selected?"✓ 已选择":"选择挑战",24,284,432,60,()=>ChoosePreview07(risk),true,selected);
            select.GetComponentInChildren<TextMeshProUGUI>().color=UiKit.Ink;
            ((UnityEngine.UI.Image)select.targetGraphic).color=selected?Color.Lerp(UiKit.Paper,UiKit.Gold,.45f):new Color(0,0,0,.04f);
            select.transform.Find("Divider").GetComponent<UnityEngine.UI.Image>().color=Color.Lerp(UiKit.Ink,UiKit.Paper,.6f);
        }

        private static void ReceiptRule071(Transform parent,string name,float x,float y,float width)
        {
            // One quiet printed line keeps the receipt readable without per-row card frames.
            UiKit.Box(parent,name,x,y,width,1,Color.Lerp(UiKit.Ink,UiKit.Paper,.72f));
        }
    }
}
