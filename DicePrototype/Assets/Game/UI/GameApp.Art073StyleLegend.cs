using DiceCraft.Core;
using TMPro;
using UnityEngine;
using Image=UnityEngine.UI.Image;

namespace DiceCraft
{
    // v0.73: one page that shows every die finish side by side (reference image 3, top half).
    // Reached from the dice library. The sample dice are throwaway copies; nothing here touches a run.
    public sealed partial class GameApp
    {
        private static readonly string[] StyleNames073={"普通","光滑","粗糙","黄金","玻璃","多种叠加","临时","锁定"};
        public void ShowDiceStyleLegend073()
        {
            if(IsBusy)return;
            var p=Art07Modal("骰子样式",208,96,1504,888,"style-legend16");
            UiKit.Text(p,"LegendHint","拖动任意一颗骰子可以旋转查看。纹理与黄金按面生效，玻璃与临时按整颗骰子生效。",24,84,1456,48,20,LabelOn11(p,UiKit.Muted));
            string[] notes=
            {
                "没有附加属性。",ShortText11("U01"),ShortText11("U02"),ShortText11("U06"),
                ShortText11("U05"),"玻璃骰体上的黄金与光滑面：各项效果逐项叠加。","本关临时骰：本关结束后消失，不占永久骰数量。","本手已锁定：留在提交栏，不能移回骰池。"
            };
            for(int i=0;i<8;i++)
            {
                var die=DiceFactory.CreateStandard(Config,i+1);die.dieId="STYLE"+i;
                if(i==1||i==5)foreach(var f in die.faces)f.texture="smooth";
                if(i==2)foreach(var f in die.faces)f.texture="rough";
                if(i==3||i==5)foreach(var f in die.faces)f.isGold=true;
                if(i==4||i==5)die.isGlass=true;
                if(i==6)die.lifetime="stage_temporary";
                float x=24+i%4*366,y=136+i/4*370;
                var cell=UiKit.Box(p,"StyleCell"+i,x,y,354,358,Color.white);var back=cell.GetComponent<Image>();
                // V1.1 (W20X): a theme's section tint, as on the shop's sections; untinted when it sets none.
                back.sprite=UiKit.Template("ui_section_panel");back.type=back.sprite!=null?Image.Type.Sliced:Image.Type.Simple;back.color=back.sprite!=null?UiKit.Section(Color.white):UiKit.Background;
                DieShadow073(cell,"Shadow",177,Large11?145:204,Large11?135:170);
                if(i==7)
                {
                    var tray=UiKit.Box(cell,"Tray",82,Large11?144:206,190,Large11?24:30,Color.white).GetComponent<Image>();tray.sprite=UiKit.Template("ui_tray_front");tray.type=tray.sprite!=null?Image.Type.Sliced:Image.Type.Simple;
                    UiKit.Text(tray.transform,"Number","1",0,0,190,Large11?24:26,17,LabelOn11(tray,UiKit.Muted),TextAlignmentOptions.Center);
                }
                DieView073(cell,"StyleDie","style-"+i,die,57,Large11?16:20,240,Large11?150:200,1.5f,true,false);
                if(i==6||i==7)
                {
                    var tag=UiKit.Box(cell,"Tag",250,Large11?96:138,Large11?48:64,Large11?48:64,Color.white).GetComponent<Image>();tag.sprite=UiKit.Template(i==6?"ui_tag_clock":"ui_tag_lock");tag.raycastTarget=false;
                }
                var label=UiKit.Box(cell,"NameChip",Large11?97:112,Large11?172:240,Large11?160:130,Large11?44:40,Color.white).GetComponent<Image>();label.sprite=UiKit.Template("ui_chip_label");label.type=label.sprite!=null?Image.Type.Sliced:Image.Type.Simple;
                var name=UiKit.Text(label.transform,"Name",StyleNames073[i],0,0,Large11?160:130,Large11?44:40,22,LabelOn11(label,UiKit.Ink),TextAlignmentOptions.Center);name.fontStyle=FontStyles.Bold;
                var note=UiKit.Text(cell,"Note",notes[i],16,Large11?222:288,322,Large11?130:58,19.5f,LabelOn11(back,UiKit.Muted),TextAlignmentOptions.Top);
                note.lineSpacing=-6;
            }
        }
    }
}
