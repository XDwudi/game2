using System;
using UnityEngine;

namespace DiceCraft
{
    /// <summary>
    /// V1.1 (docs/specs/v11/DEV_PLAN.md G7): the colour tokens of one visual theme. Every UiKit colour
    /// reads the active palette, so a theme recolours every page that is drawn through UiKit. The
    /// defaults are the v0.73 colours: the default theme looks exactly as before.
    /// </summary>
    [Serializable]
    public sealed class ThemePalette
    {
        // v0.73 tokens (same names as the UiKit properties they back).
        public Color ink=Hex("162629"),panel=Hex("203F43"),panelLight=Hex("315358"),paper=Hex("F4E8CB"),muted=Hex("B8C9C5"),
            gold=Hex("F3BA58"),teal=Hex("79C99C"),red=Hex("EC655D"),blue=Hex("55B6D9"),background=Hex("102A2D"),felt=Hex("173E40"),
            textColor=Hex("F7EED9"),violet=Hex("AD92DB"),danger=Hex("9E3440"),shadow=Hex("07191C"),ghost=Hex("6A9296");
        // v1.1 semantic tokens for the layout and feedback work (art proposal §3 principles 2 and 4, A2 rarity frames).
        public Color primary=Hex("F3BA58"),reward=Hex("F3BA58"),boss=Hex("C8463F"),pressure=Hex("E0703A"),
            rarityCommon=Hex("B8C9C5"),rarityAdvanced=Hex("55B6D9"),rarityRare=Hex("AD92DB");
        /// <summary>V1.1 (W11b): marks drawn straight on the table felt (slot numbers, empty-slot dots, the locked-die grip).
        /// Unset (alpha 0) by default: each mark then keeps its v0.73 colour; a theme with a light felt sets one dark value.</summary>
        public Color onFelt=new Color(0,0,0,0);
        /// <summary>V1.1 (W13): the fill of a card's value badge (×1.5, +12, 本手可用). Unset (alpha 0) by default: the badge
        /// then takes the theme's gold, so a theme sets it only when its gold is too close to its danger colour.</summary>
        public Color valueBadge=new Color(0,0,0,0);
        /// <summary>V1.1 (W20X): the step under a UiKit.Button, secondary and primary. The defaults are the two constants
        /// UiKit drew before the step became a palette colour.</summary>
        public Color buttonStep=Hex("8F453A"),buttonStepPrimary=Hex("B56B29");
        /// <summary>V1.1 (W20X): tint of the shop's section panels (ui_section_panel); when a theme sets it, the dice-style
        /// page's eight sample cells take it too. Unset (alpha 0) by default: the shop tints its panels with panel and the
        /// cells keep their sprite's own colour (W14).</summary>
        public Color section=new Color(0,0,0,0);
        /// <summary>V1.1 (W20X): tint of the caption strips: the shop's section header (ui_section_header) and the dice pool's
        /// tab. Unset (alpha 0) by default: the header takes panelLight, the pool tab panel. The labels on them still take
        /// their colour from the fill as drawn (LabelOn11).</summary>
        public Color caption=new Color(0,0,0,0);
        /// <summary>V1.1 (W20Y): the face of a second-level button (GameApp.SolidButton11: the action row's secondary action,
        /// 跳过演出, 查看骰子, 上一手明细, the score row's detail chips, the shop's buttons and the price buttons on cards and goods
        /// rows). Unset (alpha 0) by default: the face is lerp(background, panel, .55) as before. A disabled button is the face
        /// a further 25 % toward panelLight either way, and its labels still take their colour from the face as drawn.</summary>
        public Color buttonSecondary=new Color(0,0,0,0);

        public ThemePalette Clone()=>(ThemePalette)MemberwiseClone();

        /// <summary>Hex strings as written in a theme file; a missing or invalid token keeps the default.</summary>
        [Serializable]
        public sealed class Hexes
        {
            public string ink,panel,panelLight,paper,muted,gold,teal,red,blue,background,felt,textColor,violet,danger,shadow,ghost,
                primary,reward,boss,pressure,rarityCommon,rarityAdvanced,rarityRare,onFelt,valueBadge,
                buttonStep,buttonStepPrimary,section,caption,buttonSecondary;
        }

        public static ThemePalette From(Hexes source)
        {
            var p=new ThemePalette();if(source==null)return p;
            void Set(ref Color target,string value){if(TryHex(value,out Color c))target=c;}
            Set(ref p.ink,source.ink);Set(ref p.panel,source.panel);Set(ref p.panelLight,source.panelLight);Set(ref p.paper,source.paper);
            Set(ref p.muted,source.muted);Set(ref p.gold,source.gold);Set(ref p.teal,source.teal);Set(ref p.red,source.red);Set(ref p.blue,source.blue);
            Set(ref p.background,source.background);Set(ref p.felt,source.felt);Set(ref p.textColor,source.textColor);Set(ref p.violet,source.violet);
            Set(ref p.danger,source.danger);Set(ref p.shadow,source.shadow);Set(ref p.ghost,source.ghost);
            Set(ref p.primary,source.primary);Set(ref p.reward,source.reward);Set(ref p.boss,source.boss);Set(ref p.pressure,source.pressure);
            Set(ref p.rarityCommon,source.rarityCommon);Set(ref p.rarityAdvanced,source.rarityAdvanced);Set(ref p.rarityRare,source.rarityRare);
            Set(ref p.onFelt,source.onFelt);Set(ref p.valueBadge,source.valueBadge);
            Set(ref p.buttonStep,source.buttonStep);Set(ref p.buttonStepPrimary,source.buttonStepPrimary);
            Set(ref p.section,source.section);Set(ref p.caption,source.caption);Set(ref p.buttonSecondary,source.buttonSecondary);
            return p;
        }

        // Own pure parser: field initialisers must not depend on UiKit's type initialisation or on native calls.
        private static Color Hex(string s){TryHex(s,out Color c);return c;}

        /// <summary>RRGGBB or RRGGBBAA, with or without a leading '#'.</summary>
        public static bool TryHex(string value,out Color color)
        {
            color=Color.magenta;
            if(string.IsNullOrEmpty(value))return false;
            string s=value.Trim().TrimStart('#');
            if(s.Length!=6&&s.Length!=8)return false;
            byte[] parts=new byte[4];parts[3]=255;
            for(int i=0;i<s.Length/2;i++)
                if(!byte.TryParse(s.Substring(i*2,2),System.Globalization.NumberStyles.HexNumber,System.Globalization.CultureInfo.InvariantCulture,out parts[i]))return false;
            color=new Color32(parts[0],parts[1],parts[2],parts[3]);
            return true;
        }
    }
}
