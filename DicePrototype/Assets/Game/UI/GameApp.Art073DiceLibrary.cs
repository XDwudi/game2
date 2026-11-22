using System;
using System.Linq;
using DiceCraft.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DiceCraft
{
    // v0.73 dice library: the large die of the dice sheet keeps its pose, turns to a chosen face and
    // reports the face that looks at the player. Viewing never changes dice order, results or RNG.
    public sealed partial class GameApp
    {
        private static readonly Quaternion SheetRest073=Quaternion.Euler(20,25,15);
        private DieInstance sheetDie073;
        private string sheetKey073;
        private Vector2 sheetCenter073;
        private Quaternion sheetRotation073=SheetRest073,sheetTurnFrom073,sheetTurnTo073;
        private float sheetTurn073=-1;
        private int[] sheetUnits073;
        private TextMeshProUGUI sheetFacing073;
        private int sheetFacingShown073=-2;
        /// <summary>Physical face (0..5) of the large dice-sheet die that currently looks at the player, or -1.</summary>
        public int SheetFacingFace073=>sheetDie073==null?-1:FacingOf073(sheetRotation073);
        public bool SheetTurning073=>sheetTurn073>=0;
        public Quaternion SheetRotation073=>sheetRotation073;

        private void BeginSheetDie073(DieInstance die,string key,Vector2 center,int[] probabilityUnits)
        {
            // Redrawing the same die (choosing a face, toggling the table) keeps the pose the player left.
            if(sheetKey073!=key){sheetRotation073=SheetRest073;sheetTurn073=-1;}
            sheetDie073=die;sheetKey073=key;sheetCenter073=center;sheetUnits073=probabilityUnits;sheetFacingShown073=-2;
        }
        private void EndSheetDie073(){sheetDie073=null;sheetKey073=null;sheetTurn073=-1;sheetFacing073=null;}
        private int FacingOf073(Quaternion rotation)
        {
            Vector3 toward=-table.viewCamera.transform.forward;int best=0;float bestDot=float.NegativeInfinity;
            for(int f=0;f<6;f++){float dot=Vector3.Dot(rotation*DiceTable.Normals[f],toward);if(dot>bestDot){bestDot=dot;best=f;}}
            return best;
        }
        private void TurnSheetToFace073(string faceId)
        {
            sheetTurnTo073=table.FaceToCamera073(DiceTable.FaceIndex(faceId));
            if(reduced||UiKit.ReducedMotion||instant07){sheetRotation073=sheetTurnTo073;sheetTurn073=-1;}
            else{sheetTurnFrom073=sheetRotation073;sheetTurn073=0;}
        }
        private void DragSheetDie073(PointerEventData e)
        {
            if(sheetDie073==null)return;
            var eye=table.viewCamera.transform;float k=Art073DieView.DragDegreesPerPixel*1080f/Mathf.Max(1,Screen.height);
            sheetRotation073=Quaternion.AngleAxis(-e.delta.x*k,eye.up)*Quaternion.AngleAxis(e.delta.y*k,eye.right)*sheetRotation073;sheetTurn073=-1;
            table.ShowPreview(sheetDie073,sheetCenter073,sheetRotation073,2.15f);UpdateSheetFacing073();
        }
        private void TickDiceSheet073()
        {
            if(!modal||!diceSheet07||sheetDie073==null||sheetTurn073<0)return;
            sheetTurn073+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(sheetTurn073/Art073DieView.TurnSeconds);
            sheetRotation073=Quaternion.Slerp(sheetTurnFrom073,sheetTurnTo073,t*t*(3-2*t));
            if(t>=1){sheetRotation073=sheetTurnTo073;sheetTurn073=-1;}
            table.ShowPreview(sheetDie073,sheetCenter073,sheetRotation073,2.15f);UpdateSheetFacing073();
        }
        private void UpdateSheetFacing073()
        {
            if(sheetFacing073==null||sheetDie073==null)return;
            int facing=FacingOf073(sheetRotation073);if(facing==sheetFacingShown073)return;sheetFacingShown073=facing;
            var face=sheetDie073.faces.First(f=>DiceTable.FaceIndex(f.faceId)==facing);int at=Array.IndexOf(sheetDie073.faces,face);
            // v1.1 (W09, text proposal 3): one decimal, the precision of the probability table.
            string chance=sheetUnits073!=null&&at>=0&&at<sheetUnits073.Length?"\n掷出概率 "+PlayerNumberFormat.Percent(sheetUnits073[at],10000):"";
            sheetFacing073.text="正对你的是第 "+(facing+1)+" 面\n"+FaceSummary07(face)+(sheetDie073.isGlass?" · 玻璃骰":"")+chance;
            var scroll=sheetFacing073.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            float height=FitHeight(sheetFacing073,scroll.viewport.rect.height);
            scroll.content.sizeDelta=new Vector2(scroll.content.sizeDelta.x,height);
            scroll.verticalNormalizedPosition=1;
        }
        /// <summary>
        /// Six "face" buttons in place of the flat face net. A press turns the real die to that face and,
        /// where a face can be chosen, chooses it. <paramref name="readOnly"/> is whole-die targeting.
        /// </summary>
        private void DrawFaceChips073(Transform parent,DieInstance die,string currentFace,string selectedFace,Action<string> select,bool readOnly)
        {
            UiKit.Text(parent,"FaceLegend",readOnly?"改造整颗骰子，不用选面":"点“面 N”把骰子转过来 · 白点＝本手朝上的面",848,112,600,40,18,UiKit.Muted);
            for(int i=0;i<6;i++)
            {
                string id="F0"+(i+1);var face=die.faces.First(f=>f.faceId==id);bool chosen=id==selectedFace;
                Action click=readOnly?(Action)null:select!=null?()=>select(id):()=>{TurnSheetToFace073(id);if(reduced||UiKit.ReducedMotion||instant07){table.ShowPreview(sheetDie073,sheetCenter073,sheetRotation073,2.15f);UpdateSheetFacing073();}};
                var chip=UiKit.FlatButton(parent,"Face_"+id,"面 "+(i+1),848+i*101,160,92,64,click,!readOnly,chosen);
                // Whole-die targeting keeps the same pooled button but leaves it inert.
                chip.enabled=!readOnly;
                if(face.isGold)UiKit.Box(chip.transform,"GoldRim",10,52,72,4,UiKit.Gold);
                if(id==currentFace)
                {
                    var dot=UiKit.Box(chip.transform,"CurrentFace",72,8,12,12,UiKit.Paper).GetComponent<UnityEngine.UI.Image>();
                    dot.sprite=UiKit.Template("ui_pip_dot");dot.raycastTarget=false;
                }
                if(chosen)UiKit.Ring(chip.transform,"SelectedH0",-5,-5,102,74,UiKit.Gold);
            }
            float insetHeight=Large11?174:150;
            UiKit.Inset(parent,"FacingInset073",848,230,600,insetHeight);
            var content=UiKit.Scroll(parent,"FacingSummary073",864,234,576,insetHeight-8,insetHeight-8);
            sheetFacing073=UiKit.Text(content,"FacingFace073","",4,0,550,insetHeight-8,22,UiKit.TextColor);sheetFacing073.lineSpacing=6;
            sheetFacingShown073=-2;UpdateSheetFacing073();
            UiKit.Text(parent,"FacingHint073","相对的面：1 和 6、2 和 5、3 和 4",848,Large11?399:386,600,Large11?45:40,19.5f,UiKit.Muted);
            var legend=UiKit.FlatButton(parent,"StyleLegend073","骰子样式说明",Large11?1160:1248,Large11?442:432,Large11?288:200,60,ShowDiceStyleLegend073,!IsBusy);
            NoWrap11(legend.transform.Find("Label").GetComponent<TextMeshProUGUI>());
        }
        /// <summary>Stage screen: opens the dice library on the first die. Sixth cell of the item grid.</summary>
        private void DrawViewDiceButton073(float x,float y,float size)
        {
            bool any=ViewState.dice.Count>0;
            var button=UiKit.Button(main,"ViewDice","",x,y,size,size,()=>
            {
                var first=ViewState.dice.OrderBy(d=>d.createdSerial).FirstOrDefault();if(first!=null)ShowDie(first.dieId);
            },any&&!IsBusy);
            button.targetGraphic.color=any&&!IsBusy?UiKit.Blue:UiKit.PanelLight;
            var icon=UiKit.Box(button.transform,"ViewDiceIcon",(size-46)*.5f,4,46,46,any&&!IsBusy?Color.white:new Color(1,1,1,.5f)).GetComponent<UnityEngine.UI.Image>();
            icon.sprite=UiKit.Template("ui_icon_dice_view");icon.raycastTarget=false;icon.gameObject.SetActive(icon.sprite!=null);
            var label=button.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            label.text="查看骰子";label.fontSize=15*UiKit.FontScale;label.textWrappingMode=TextWrappingModes.NoWrap;
            label.rectTransform.anchoredPosition=new Vector2(2,-(size-36));label.rectTransform.sizeDelta=new Vector2(size-4,26);
            Art07AttachTooltip(button,"view-dice","查看骰子","查看你每颗骰子的六个面。","",null);
        }
    }
}
