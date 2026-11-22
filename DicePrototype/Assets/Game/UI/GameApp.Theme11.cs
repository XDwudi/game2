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
            RetainTitleFallback21(theme.TitleFont);
            // V1.1 (W11c): the measured sprite fills and frames belong to the sprites of the theme that drew them.
            ClearSurfaceCaches11();ClearCardCaches13();
            ApplyThemeToTable11(theme);
            if(redraw&&StartupReady07)Refresh();
        }

        private Texture feltBaseMap11,feltTableMap11,feltPicture11;
        private Color? feltColor11;
        private Renderer feltRenderer11;
        private bool galleryBackdrop11;
        private Renderer FeltRenderer11
        {
            get{if(feltRenderer11==null){var felt=GameObject.Find("Art07Felt");feltRenderer11=felt!=null?felt.GetComponent<Renderer>():null;}return feltRenderer11;}
        }
        /// <summary>
        /// V1.1 (W20X, THEME_SPEC §3): the play screen and the screen that draws the play layout (the stage settlement) show
        /// the table composition, table_scene; every other screen (the menu, stage select, the shop, the run end, the card
        /// gallery, the loading screen) shows the theme's page_scene when it ships one. A modal follows the screen under it,
        /// so card details or the handbook over the play screen keep the play table.
        /// </summary>
        public bool PlayBackdrop11=>!inMenu&&ViewState!=null&&!galleryBackdrop11&&(PlayPhase11||ViewState.phase=="StageSettlement");
        private Texture TablePicture11(GameTheme theme)
        {
            var table=theme.TableScene!=null?theme.TableScene:feltTableMap11;
            return !PlayBackdrop11&&theme.PageScene!=null?theme.PageScene:table;
        }
        private void ApplyThemeToTable11(GameTheme theme)
        {
            var r=FeltRenderer11;if(r==null||r.sharedMaterial==null)return;
            var shared=r.sharedMaterial;
            // Remember the authored default textures once; a property block always carries an explicit value.
            if(feltColor11==null){feltBaseMap11=shared.GetTexture("_BaseMap");feltTableMap11=shared.GetTexture("_TableMap");feltColor11=shared.GetColor("_BaseColor");}
            var props=new MaterialPropertyBlock();r.GetPropertyBlock(props);
            var baseMap=theme.FeltTile!=null?theme.FeltTile:feltBaseMap11;
            var tableMap=ChapterPicture18(theme,TablePicture11(theme),out var chapterShift);
            if(baseMap!=null)props.SetTexture("_BaseMap",baseMap);
            if(tableMap!=null)props.SetTexture("_TableMap",tableMap);
            feltPicture11=tableMap;
            props.SetVector(ChapterShiftId18,chapterShift);
            shownChapterShift18=chapterShift;
            props.SetColor("_BaseColor",theme.IsDefault?feltColor11.Value:theme.Palette.felt);
            r.SetPropertyBlock(props);
        }
        /// <summary>V1.1 (W20X): runs with every redraw (ApplyTableMood11) and when the card gallery opens or closes. It swaps
        /// the felt's table picture when the screen moves between the play layout and the other screens, in the frame that
        /// draws the new screen. A theme without page_scene never changes the picture here.</summary>
        private void ApplyTablePicture11()
        {
            var theme=ThemeRegistry.Current;if(theme==null||feltColor11==null)return;
            var picture=ChapterPicture18(theme,TablePicture11(theme),out var chapterShift);
            if(picture==null||ReferenceEquals(picture,feltPicture11)&&shownChapterShift18==chapterShift)return;
            var r=FeltRenderer11;if(r==null)return;
            var props=new MaterialPropertyBlock();r.GetPropertyBlock(props);
            props.SetTexture("_TableMap",picture);props.SetVector(ChapterShiftId18,chapterShift);
            r.SetPropertyBlock(props);
            feltPicture11=picture;
            shownChapterShift18=chapterShift;
        }
        /// <summary>The card gallery (a debug page drawn over the whole screen) is not the play layout.</summary>
        private void SetGalleryBackdrop11(bool shown){galleryBackdrop11=shown;ApplyTablePicture11();}

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
