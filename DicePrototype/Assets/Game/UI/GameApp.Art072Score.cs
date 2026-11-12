using TMPro;
using UnityEngine;

namespace DiceCraft
{
    // v0.72 rc.2 score rail dressing. Presentation only: no score, factor or layout rectangle of an
    // existing element is changed here; the well and chip faces sit behind the existing text.
    public sealed partial class GameApp
    {
        private static readonly Color[] FactorTints072={UiKit.Blue,UiKit.Red,UiKit.Red,UiKit.Red,UiKit.Gold,UiKit.Violet};

        /// <summary>Rent the display window before the number so it is painted underneath it.</summary>
        private RectTransform ScoreWell072(Transform panel)=>UiKit.Inset(panel,"ScoreWell072",0,0,10,10);

        /// <summary>Hug the right-aligned score with the display window (the number's right edge is panelWidth-30).</summary>
        private void FitScoreWell072(RectTransform well,TextMeshProUGUI number,float panelWidth,float center,float half,float maxWidth=640)
        {
            // The rail number keeps its regular cut: a bold style here pulls in a second font atlas whose
            // fallback material is created and released on every replay (caught by the 200-replay test).
            float text=number.GetPreferredValues(number.text).x;
            float width=Mathf.Clamp(text+44,Mathf.Min(maxWidth,Compact07?150:190),maxWidth);
            well.anchoredPosition=new Vector2(panelWidth-8-width,-(center-half));well.sizeDelta=new Vector2(width,half*2);
        }

        /// <summary>In the shallow (104-unit) rail a 60-unit ghost button would poke out of the slab: trim the paint, keep the hit rectangle.</summary>
        private static void TrimRailButton072(UnityEngine.UI.Button button,bool thin)
        {
            if(!thin)return;
            var face=((UnityEngine.UI.Image)button.targetGraphic).rectTransform;
            face.anchoredPosition=new Vector2(0,-2);face.sizeDelta=new Vector2(face.sizeDelta.x,face.sizeDelta.y-12);
            var label=(RectTransform)button.transform.Find("Label");label.anchoredPosition+=new Vector2(0,4);
        }

        /// <summary>
        /// A factor that changes the score is a solid colour chip (base blue, multipliers red, controllers gold,
        /// enchantments violet); a neutral one stays a quiet outline. Same button, same click target.
        /// </summary>
        private static void StyleFactorChip072(UnityEngine.UI.Button button,int index,bool neutral)
        {
            var chip=UiKit.Template("ui_button_bevel");
            if(chip==null||neutral||!button.interactable)return;
            var face=(UnityEngine.UI.Image)button.targetGraphic;
            face.sprite=chip;face.type=UnityEngine.UI.Image.Type.Sliced;face.color=FactorTints072[index];
            button.GetComponentInChildren<TextMeshProUGUI>().color=UiKit.Ink;
        }
    }
}
