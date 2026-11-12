using System;
using DiceCraft.Core;
using UnityEngine;

namespace DiceCraft
{
    // v0.73 presentation only: real dice inside interface panels (shop counter, owned strip, dice library).
    public sealed partial class GameApp
    {
        /// <summary>Premultiplied material of the interface dice image. Bound by V073Setup so the shader ships with the player.</summary>
        public Material uiDiceImageMaterial073;
        private Art073DiceLayer uiDiceLayer073;
        /// <summary>The table that draws interface dice. Never rolls; never holds a hand.</summary>
        public DiceTable UiDice073 => uiDiceLayer073 != null ? uiDiceLayer073.Dice : null;
        public Art073DiceLayer UiDiceLayer073 => uiDiceLayer073;

        private void InitializeUiDice073()
        {
            if (uiDiceLayer073 != null || table == null || table.viewCamera == null) return;
            uiDiceLayer073 = gameObject.AddComponent<Art073DiceLayer>();
            uiDiceLayer073.Configure(table, canvas, main, overlay, uiDiceImageMaterial073, () => modal);
        }

        /// <summary>Three-quarter view with physical face 1 toward the viewer.</summary>
        private Quaternion DieRest073(int face = 0) => UiDice073.FaceToCamera073(face, -27f, 33f);

        /// <summary>
        /// A draggable real die centred in a rectangle of the given parent. <paramref name="inModal"/> says
        /// which layer owns it: screen dice hide while a modal is open, modal dice hide when it closes.
        /// </summary>
        private Art073DieView DieView073(Transform parent, string name, string slot, DieInstance die, float x, float y, float w, float h, float size,
            bool inModal = false, bool spin = false, Action click = null, Action<int> faceClick = null, RectTransform clip = null, int restFace = 0)
        {
            var rect = UiKit.Rect(parent, name, x, y, w, h);
            var view = UiKit.Component<Art073DieView>(rect.gameObject);
            view.Bind(UiDice073, slot, die, size, DieRest073(restFace), () => inModal ? modal : !modal && !inMenu, clip, true, spin);
            view.clicked = click; view.faceClicked = faceClick; view.turned = null;
            return view;
        }

        /// <summary>Soft floor shadow under an interface die (the dice texture itself carries no shadow).</summary>
        private void DieShadow073(Transform parent, string name, float centreX, float baseY, float width)
        {
            var sprite = UiKit.Template("ui_die_shadow"); if (sprite == null) return;
            var shadow = UiKit.Box(parent, name, centreX - width * .5f, baseY - width * .25f, width, width * .5f, Color.white).GetComponent<UnityEngine.UI.Image>();
            shadow.sprite = sprite; shadow.type = UnityEngine.UI.Image.Type.Simple; shadow.raycastTarget = false;
        }
    }
}
