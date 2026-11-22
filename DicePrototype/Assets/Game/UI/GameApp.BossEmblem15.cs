using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        [SerializeField] public Sprite[] defaultBossEmblems15;

        public static string BossEmblemFamily15(string modifierId, int tier)
        {
            if (tier == 4) return "final";
            switch (modifierId)
            {
                case "B05": case "B13": case "B14": case "B15": case "B16": case "B17": case "B18": return "seal";
                case "B01": case "B02": case "B09": case "B10": case "B27": return "constraint";
                case "B08": case "B19": case "B20": case "B21": case "B22": case "B23": case "B24": case "B28": return "multiplier";
                case "B06": case "B07": case "B25": case "B29": return "cards";
                case "B03": case "B04": case "B12": return "economy";
                case "B11": case "B26": case "B30": return "target";
                default: return null;
            }
        }

        private RectTransform BossEmblem15(Transform parent, string name, string modifierId,
            float x, float y, float size, bool withFrame = true, bool lifted = false)
        {
            var modifier = Config.Modifier(modifierId);
            string spriteName = "emblem_" + BossEmblemFamily15(modifierId, modifier.tier);
            var sprite = ThemeRegistry.Current?.ContentArt(spriteName)
                ?? defaultBossEmblems15?.FirstOrDefault(icon => icon != null && icon.name == spriteName);
            var root = UiKit.Rect(parent,name,x,y,size,size);
            var tierColor = lifted ? UiKit.PanelLight : modifier.tier >= 4 ? UiKit.Boss
                : modifier.tier == 3 ? UiKit.RarityRare : modifier.tier == 2 ? UiKit.RarityAdvanced : UiKit.RarityCommon;
            var frame = withFrame ? UiKit.Template("ui_emblem_tier" + modifier.tier) : null;
            if (frame != null)
            {
                var border = UiKit.Box(root,"TierFrame15",0,0,size,size,tierColor).GetComponent<Image>();
                border.sprite=frame;
                border.type=Image.Type.Simple;
                border.preserveAspect=true;
                border.raycastTarget=false;
            }
            float inset = withFrame ? size * (16f/176) : 0;
            var face = UiKit.Box(root,"BossFamily15",inset,inset,size-2*inset,size-2*inset,
                sprite != null ? Color.white : tierColor).GetComponent<Image>();
            face.sprite=sprite;
            face.type=Image.Type.Simple;
            face.preserveAspect=true;
            face.raycastTarget=false;
            if (sprite == null)
                for (int bar=0; bar<modifier.tier; bar++)
                    UiKit.Box(face.transform,"MissingFamilyTier"+bar,size*.2f,size*(.15f+bar*.16f),size*.44f,size*.07f,UiKit.Ink);
            return root;
        }
    }
}
