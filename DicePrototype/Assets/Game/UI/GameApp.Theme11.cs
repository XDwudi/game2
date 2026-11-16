using System.Linq;
using UnityEngine;

namespace DiceCraft
{
    // V1.1 visual themes (docs/specs/v11/DEV_PLAN.md G7, §4.6). Appearance only: switching never touches the
    // session, the run state or the save file. The choice is a setting (PlayerPrefs), not part of a save.
    public sealed partial class GameApp
    {
        public string ThemeId11=>ThemeRegistry.Current?.Id??GameTheme.DefaultId;

        private void InitializeTheme11()
        {
            string saved=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            var theme=ThemeRegistry.Find(saved);
            // A theme that is no longer offered (removed, or a preview without previews enabled) falls back to the default.
            if(theme==null||(theme.Preview&&!ThemeRegistry.ShowPreviewThemes))saved=GameTheme.DefaultId;
            ApplyTheme11(saved,false);
        }

        /// <summary>Activates a theme and redraws. Public so captures and tests can switch without the settings page.</summary>
        public void ApplyTheme11(string id,bool redraw=true)
        {
            var theme=ThemeRegistry.Activate(id);
            ApplyThemeToTable11(theme);
            if(redraw&&StartupReady07)Refresh();
        }

        private Texture feltBaseMap11,feltTableMap11;
        private Color? feltColor11;
        private void ApplyThemeToTable11(GameTheme theme)
        {
            var felt=GameObject.Find("Art07Felt");if(felt==null)return;
            var r=felt.GetComponent<Renderer>();if(r==null||r.sharedMaterial==null)return;
            var shared=r.sharedMaterial;
            // Remember the authored default textures once; a property block always carries an explicit value.
            if(feltColor11==null){feltBaseMap11=shared.GetTexture("_BaseMap");feltTableMap11=shared.GetTexture("_TableMap");feltColor11=shared.GetColor("_BaseColor");}
            var props=new MaterialPropertyBlock();r.GetPropertyBlock(props);
            var baseMap=theme.FeltTile!=null?theme.FeltTile:feltBaseMap11;
            var tableMap=theme.TableScene!=null?theme.TableScene:feltTableMap11;
            if(baseMap!=null)props.SetTexture("_BaseMap",baseMap);
            if(tableMap!=null)props.SetTexture("_TableMap",tableMap);
            props.SetColor("_BaseColor",theme.IsDefault?feltColor11.Value:theme.Palette.felt);
            r.SetPropertyBlock(props);
        }

        private void ThemeSettingsChoice11(Transform list,float y)
        {
            var options=ThemeRegistry.Selectable(ThemeRegistry.ShowPreviewThemes);
            var current=ThemeRegistry.Current??ThemeRegistry.Find(GameTheme.DefaultId);
            SettingsChoice07(list,"Theme","画面风格",current.DisplayName,y,()=>
            {
                int at=options.ToList().FindIndex(t=>t.Id==current.Id);
                var next=options[(at+1)%options.Count];
                PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,next.Id);PlayerPrefs.Save();
                ApplyTheme11(next.Id);
                settingsNotice07="已切换为“"+next.DisplayName+"”风格。只改变外观，不影响对局与存档。";
                ShowSettings07();
            });
        }
    }
}
