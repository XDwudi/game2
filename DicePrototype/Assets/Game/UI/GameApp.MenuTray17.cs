using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private static readonly int MenuTrayTexture17 = Shader.PropertyToID("_MenuTray17");
        private static readonly int MenuTrayVisible17 = Shader.PropertyToID("_ShowMenuTray17");
        private Texture2D menuTray17;
        private string menuTrayTheme17;
        private bool trayVisible17;

        // Render into the felt before real-time shadows. All live card art and dice stay in their original layers.
        public void ShowMenuTray17(bool show)
        {
            if (show && menuTrayTheme17 != ThemeId11)
            {
                menuTrayTheme17 = ThemeId11;
                menuTray17 = Resources.Load<Texture2D>("MenuArt17/" + menuTrayTheme17);
            }
            bool visible = show && menuTray17 != null;
            if (!visible && !trayVisible17) return;
            var felt = FeltRenderer11;
            if (felt == null) return;
            var props = new MaterialPropertyBlock();
            felt.GetPropertyBlock(props);
            if (visible) props.SetTexture(MenuTrayTexture17,menuTray17);
            props.SetFloat(MenuTrayVisible17,visible ? 1f : 0f);
            felt.SetPropertyBlock(props);
            trayVisible17 = visible;
        }
    }
}
