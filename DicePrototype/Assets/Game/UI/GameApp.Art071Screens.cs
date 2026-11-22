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
            ShowMenuTray17(true);
            DrawMenuWordmark16(true);
            float[] cardX={164,374,602,824},cardY={484,450,460,490},angles={12,5,-5,-12};
            for(int i=0;i<4;i++)
            {
                RectTransform card;
                if(i==1||i==2)
                {
                    var face=new CardFace13{contentId=i==1?"C48":"C29"};
                    var printed=DrawCard13(main,"MenuCardBack"+i,cardX[i],cardY[i],218,280,face,CardSize13.Table,null,false);
                    card=(RectTransform)printed.transform;
                    foreach(var graphic in printed.GetComponentsInChildren<UnityEngine.UI.Graphic>())graphic.raycastTarget=false;
                }
                else
                {
                    card=UiKit.PaperFrame(main,"MenuCardBack"+i,cardX[i],cardY[i],218,280,UiKit.Paper);
                    card.GetComponent<UnityEngine.UI.Image>().sprite=UiKit.Template("ui_card_controller");
                    var back=UiKit.Box(card,"PrintedCardBack",4,4,210,272,Color.white).GetComponent<UnityEngine.UI.Image>();
                    back.sprite=UiKit.Template("ui_card_back");back.preserveAspect=true;
                }
                card.localRotation=Quaternion.Euler(0,0,angles[i]);
            }
            var centers=new[]{new Vector2(332,880),new Vector2(640,894),new Vector2(976,882)};
            for(int i=0;i<3;i++)
            {
                var die=DiceFactory.CreateStandard(Config,i+1);die.dieId="$menu"+i;
                var face=die.faces.First(f=>f.permanentPip==i*2+1);
                table.Display(die,new DieHandState{dieId=die.dieId,currentFaceId=face.faceId,currentPip=face.permanentPip,currentColor=face.baseColor,hasRolled=true,isLocked=true},centers[i],1.65f);
            }
            var p=UiKit.PaperFrame(main,"MenuPanel",1216,76,616,928,UiKit.Paper);
            var heading=UiKit.Text(p,"MenuLabel","一桌新的可能",32,24,552,68,30,UiKit.Ink,TextAlignmentOptions.Center);
            heading.fontStyle=FontStyles.Bold;
            UiKit.Box(p,"MenuRule",40,108,536,2,Color.Lerp(UiKit.Paper,UiKit.Ink,.6f));
            bool hasSave=ViewState!=null;
            if(hasSave)
            {
                Primary16(p,"ContinueRun","继续对局",36,148,544,88,ContinueRun07);
                UiKit.Button(p,"NewRun","开始新的一局",36,342,544,80,ShowNewRun07);
            }
            else Primary16(p,"NewRun","开始新的一局",36,148,544,88,ShowNewRun07);
            string saved=hasSave?$"第 {ViewState.stage.stageIndex+1} 关 · {PlayerNumberFormat.Gold(ViewState.gold)} 金币":"暂无存档 · 开始你的第一局";
            if(hasSave&&ViewState.debugWorkspace)saved+="\n调试对局 · 种子 "+ViewState.seed;
            UiKit.Text(p,"SavedSeed",saved,48,236,520,96,20,LabelOn11(p,PaperMuted071),TextAlignmentOptions.Center);
            float first=hasSave?442:342;
            UiKit.Button(p,"MenuHandbook","牌型手册",36,first,544,80,ShowMenuHandbook07);
            UiKit.Button(p,"MenuSettings","设置",36,first+100,544,80,ShowSettings07);
            UiKit.Button(p,"Quit","退出游戏",36,first+200,544,80,ConfirmQuit07);
            var version=UiKit.FlatButton(main,"BuildVersion","版本与构建",1264,908,520,64,ShowBuildVersion);
            version.GetComponentInChildren<TextMeshProUGUI>().color=LabelOn11(p,UiKit.Ink);
            version.targetGraphic.color=PaperMuted071;
            DrawNotice07();
            var veil=UiKit.Box(main,"MenuEntranceVeil",0,0,1920,1080,UiKit.Background).GetComponent<UnityEngine.UI.Image>();veil.transform.SetAsLastSibling();
            UiKit.Component<Art07MenuReveal>(main.gameObject).Bind("menu:"+(ViewState?.runId??"new")+":"+(ViewState?.actionSequence??0),p,veil,
                new[]{"ContinueRun","NewRun","MenuHandbook","MenuSettings","Quit"}.Select(id=>p.Find(id) as RectTransform).Where(t=>t!=null&&t.gameObject.activeSelf).ToArray());
        }

        private static void ReceiptRule071(Transform parent,string name,float x,float y,float width)
        {
            // One quiet printed line keeps the receipt readable without per-row card frames.
            UiKit.Box(parent,name,x,y,width,1,Color.Lerp(UiKit.Ink,UiKit.Paper,.72f));
        }
    }
}
