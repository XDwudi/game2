using TMPro;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private void DrawShopWallet07()
        {
            // v0.73: the wallet block keeps its v0.72 geometry; next stage, dice and capacity rows follow it.
            var panel=UiKit.Frame(main,"Stats07",24,24,320,504,UiKit.Background,UiKit.PanelLight);UiKit.Surface(panel,"ui_panel_hud");
            ShopStatus073(panel);
            UiKit.Inset(panel,"MoneyInset072",10,86,300,84);
            var brand=UiKit.Text(panel,"Brand","骰子构筑",18,18,284,72,32);
            brand.textWrappingMode=TextWrappingModes.NoWrap;
            // The currency is a separate caption: it cannot wrap below the amount into interest.
            var unit=UiKit.Text(panel,"MoneyUnit","金币",230,34,72,48,21,UiKit.Gold);
            unit.textWrappingMode=TextWrappingModes.NoWrap;
            string amount=ViewState.gold.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var money=UiKit.Text(panel,"Money",amount,18,90,284,84,38,UiKit.Gold);
            // Regular cut on purpose: a ten-digit wallet at 150% only fits with the regular metrics.
            money.enableAutoSizing=false;money.characterSpacing=0;
            UiKit.Component<Art072ValuePop>(money.gameObject).Configure(ViewState.runId+"|wallet",amount);
            money.textWrappingMode=TextWrappingModes.NoWrap;money.overflowMode=TextOverflowModes.Overflow;
            // At 150%, a ten-digit wallet needs slightly more width than the ordinary inner column.
            // Preserve size and every digit; use the wider number rail and only necessary tracking.
            if(money.GetPreferredValues(amount).x>284)
            {
                money.rectTransform.anchoredPosition=new Vector2(8,-90);
                money.rectTransform.sizeDelta=new Vector2(304,84);
                while(money.GetPreferredValues(amount).x>304&&money.characterSpacing>-4)
                    money.characterSpacing-=.25f;
            }
            UiKit.Text(panel,"Interest",ShopInterestText07(),18,182,284,104,21,UiKit.Muted);        }
    }
}
