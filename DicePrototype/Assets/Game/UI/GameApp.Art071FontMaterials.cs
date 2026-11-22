using TMPro;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private Material boldUiFallback071;
        private TMP_FontAsset retainedTitle21;
        private Material titleFallback21;

        private void RetainTitleFallback21(TMP_FontAsset font)
        {
            if(retainedTitle21==font)return;
            if(titleFallback21!=null)TMP_MaterialManager.ReleaseFallbackMaterial(titleFallback21);
            retainedTitle21=font;titleFallback21=null;
            if(font==null||!TMP_Settings.matchMaterialPreset||font.fallbackFontAssetTable==null||font.fallbackFontAssetTable.Count==0)return;
            var bold=font.fallbackFontAssetTable[0];
            if(bold==null||bold.material==null||font.material==null)return;
            titleFallback21=TMP_MaterialManager.GetFallbackMaterial(font.material,bold.material);
            TMP_MaterialManager.AddFallbackMaterialReference(titleFallback21);
        }

        private void InitializeUiFontMaterials071()
        {
            if(boldUiFallback071!=null||uiFont==null||!TMP_Settings.matchMaterialPreset)return;
            var weights=uiFont.fontWeightTable;
            var bold=weights!=null&&weights.Length>7?weights[7].regularTypeface:null;
            if(bold==null||bold==uiFont||uiFont.material==null||bold.material==null)return;

            // A redraw temporarily disables every pooled label. Retain one reference
            // for this known font pair so TMP does not destroy/recreate its shared
            // preset material when the last visible bold submesh is disabled.
            boldUiFallback071=TMP_MaterialManager.GetFallbackMaterial(uiFont.material,bold.material);
            TMP_MaterialManager.AddFallbackMaterialReference(boldUiFallback071);
        }

        private void ReleaseUiFontMaterials071()
        {
            RetainTitleFallback21(null);
            var retained=boldUiFallback071;boldUiFallback071=null;
            if(retained!=null)TMP_MaterialManager.ReleaseFallbackMaterial(retained);
        }
    }
}
