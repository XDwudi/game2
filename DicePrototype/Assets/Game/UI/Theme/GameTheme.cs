using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace DiceCraft
{
    /// <summary>
    /// V1.1 visual theme (docs/specs/v11/DEV_PLAN.md §4.6). A theme changes appearance only: palette, the
    /// UiKit surface sprites, content illustrations, the table textures and a settings preview. Layout,
    /// motion, rules and saves are shared by all themes. Anything a theme does not provide falls back to the
    /// default theme's asset of the same name.
    ///
    /// Files (all optional except theme.json), under Assets/Resources/Themes/&lt;id&gt;/:
    ///   theme.json            id, displayName, description, order, preview flag, palette hexes
    ///   UI/*.png              sprites named like the default UiKit templates (ui_paper_panel, ...)
    ///   Art/*.png             illustrations named illustration_&lt;contentId&gt;
    ///   Table/felt_tile.png   felt tile (_BaseMap); Table/table_scene.png table composition (_TableMap)
    ///   Table/page_scene.png  V1.1 (W20X): the table picture of the screens without the play layout (shop, stage select,
    ///                         menu, run end and what opens over them); without it they draw table_scene
    ///   preview.png           settings preview
    /// </summary>
    public sealed class GameTheme
    {
        public const string DefaultId="default";

        [Serializable]
        private sealed class ThemeFile
        {
            public string id,displayName,description;
            public int order;
            public bool preview;
            public ThemePalette.Hexes palette;
            public FontsFile fonts;
        }

        [Serializable]
        private sealed class FontsFile
        {
            public string title;
            public string[] roles;
        }

        private FontsFile fonts;
        public TMP_FontAsset TitleFont { get; private set; }
        public TextRole TitleRoles { get; private set; }
        private const TextRole DefaultTitleRoles = TextRole.ScreenTitle | TextRole.StageNumber
            | TextRole.BigNumber | TextRole.BossName | TextRole.PrimaryButton;

        public string Id{get;private set;}
        public string DisplayName{get;private set;}
        public string Description{get;private set;}
        public int Order{get;private set;}
        /// <summary>Unfinished theme: listed only while theme previews are enabled.</summary>
        public bool Preview{get;private set;}
        public ThemePalette Palette{get;private set;}
        public bool IsDefault=>Id==DefaultId;

        private bool loaded;
        private Dictionary<string,Sprite> templates;
        // V1.1 (W22i): a style ships 184 card illustrations and 7 boss emblems (about 200 MiB of textures), of which a screen
        // draws a handful. Loading them all on the first switch took up to 0.55 s and kept every style's set once visited, so
        // content art is loaded one picture at a time on its first use (Resources.Load) and released when another style
        // becomes current (ThemeRegistry.Activate). A name the style has no picture for is remembered as null.
        private Dictionary<string,Sprite> contentArt;
        private string artFolder;
        /// <summary>Tests only (CreateForTests): the theme that lends this one its content art.</summary>
        private GameTheme artFrom;
        /// <summary>The theme whose content art this one draws: itself, or the theme a test theme borrows it from.</summary>
        internal GameTheme ArtSource=>artFrom??this;
        private Texture2D feltTile,tableScene,pageScene;
        private Sprite previewImage;

        public static GameTheme CreateDefault()=>new GameTheme{Id=DefaultId,DisplayName="默认",Description="复古印刷骰桌",Order=0,Palette=new ThemePalette(),loaded=true};

        /// <summary>Tests only (W20X-02): a theme built in memory, never from a file, so no test picture ships under
        /// Resources. It is a preview theme with the given palette and table pictures; <paramref name="spritesFrom"/> lends it
        /// another theme's sprites (a copy of a real theme with other tints).</summary>
        public static GameTheme CreateForTests(string id,ThemePalette palette,Texture2D table=null,Texture2D page=null,GameTheme spritesFrom=null,
            TMP_FontAsset titleFont=null,TextRole titleRoles=DefaultTitleRoles)
        {
            if(string.IsNullOrEmpty(id)||id==DefaultId)throw new ArgumentException("A test theme needs its own id.",nameof(id));
            spritesFrom?.EnsureLoaded();
            return new GameTheme{Id=id,DisplayName=id,Description="",Order=1000,Preview=true,Palette=palette??new ThemePalette(),loaded=true,
                templates=spritesFrom?.templates,artFrom=spritesFrom,feltTile=spritesFrom?.feltTile,tableScene=table,pageScene=page,
                TitleFont=titleFont,TitleRoles=titleFont!=null?titleRoles:TextRole.None};
        }

        internal static GameTheme FromFile(TextAsset file)
        {
            if(file==null)return null;
            ThemeFile data;
            try{data=JsonUtility.FromJson<ThemeFile>(file.text);}
            catch(Exception e){Debug.LogWarning("V11 theme file ignored: "+e.Message);return null;}
            if(data==null||string.IsNullOrEmpty(data.id)||data.id==DefaultId)return null;
            return new GameTheme{Id=data.id,DisplayName=string.IsNullOrEmpty(data.displayName)?data.id:data.displayName,
                Description=data.description??"",Order=data.order,Preview=data.preview,Palette=ThemePalette.From(data.palette),fonts=data.fonts};
        }

        /// <summary>Loads this theme's sprites and textures on first use, so unused themes cost no memory. Content art is not
        /// loaded here: ContentArt loads each picture when it is first asked for (W22i).</summary>
        public void EnsureLoaded()
        {
            if(loaded)return;loaded=true;
            string root="Themes/"+Id+"/";
            templates=Index(Resources.LoadAll<Sprite>(root+"UI"));
            artFolder=root+"Art/";
            feltTile=Resources.Load<Texture2D>(root+"Table/felt_tile");
            tableScene=Resources.Load<Texture2D>(root+"Table/table_scene");
            pageScene=Resources.Load<Texture2D>(root+"Table/page_scene");
            previewImage=Resources.Load<Sprite>(root+"preview");
            if(!string.IsNullOrEmpty(fonts?.title))
            {
                TitleFont=Resources.Load<TMP_FontAsset>(root+fonts.title);
                if(TitleFont==null)Debug.LogWarning("V11 missing theme title font: "+root+fonts.title);
                else if(fonts.roles==null)TitleRoles=DefaultTitleRoles;
                else foreach(string name in fonts.roles)
                    if(Enum.TryParse(name,true,out TextRole role))TitleRoles|=role;
            }
        }

        private static Dictionary<string,Sprite> Index(IEnumerable<Sprite> sprites)
        {
            var map=new Dictionary<string,Sprite>(StringComparer.OrdinalIgnoreCase);
            foreach(var s in sprites??Enumerable.Empty<Sprite>())if(s!=null&&!map.ContainsKey(s.name))map.Add(s.name,s);
            return map.Count==0?null:map;
        }

        /// <summary>Theme override for a UiKit template, or null to use the default sprite.</summary>
        public Sprite Template(string name)=>name!=null&&templates!=null&&templates.TryGetValue(name,out var s)?s:null;
        /// <summary>Theme override for a content sprite (for example illustration_C01 or emblem_seal), or null. Loaded from
        /// Resources/Themes/&lt;id&gt;/Art on the first ask and kept until another theme becomes current (W22i).</summary>
        public Sprite ContentArt(string name)
        {
            if(string.IsNullOrEmpty(name))return null;
            if(artFrom!=null)return artFrom.ContentArt(name);
            if(artFolder==null)return null;
            if(contentArt==null)contentArt=new Dictionary<string,Sprite>(StringComparer.OrdinalIgnoreCase);
            if(contentArt.TryGetValue(name,out var sprite))return sprite;
            sprite=Resources.Load<Sprite>(artFolder+name);
            contentArt[name]=sprite;
            return sprite;
        }
        /// <summary>V1.1 (W22i): unloads the content art this theme loaded (ThemeRegistry.Activate calls it for the theme that
        /// stops being current). Each texture is unloaded; a sprite still held somewhere is loaded again on its next use.
        /// Returns how many pictures were released.</summary>
        internal int ReleaseContentArt()
        {
            if(contentArt==null)return 0;
            int released=0;
            foreach(var sprite in contentArt.Values)
            {
                if(sprite==null)continue;
                var texture=sprite.texture;
                if(texture!=null)Resources.UnloadAsset(texture);
                released++;
            }
            contentArt.Clear();
            return released;
        }
        public Texture2D FeltTile=>feltTile;
        public Texture2D TableScene=>tableScene;
        /// <summary>V1.1 (W20X): the optional table picture of the non-play screens, or null (they then draw TableScene).</summary>
        public Texture2D PageScene=>pageScene;
        public Sprite PreviewImage=>previewImage;
        public int TemplateCount=>templates?.Count??0;
        /// <summary>V1.1 (W22i): the content art names asked for since this theme became current (loaded or found missing).</summary>
        public int ContentArtCount=>contentArt?.Count??0;
        public IEnumerable<string> TemplateNames=>templates?.Keys??Enumerable.Empty<string>();
    }

    /// <summary>The themes found in Resources/Themes plus the built-in default, and the active one.</summary>
    public static class ThemeRegistry
    {
        public const string PreferenceKey="v11.theme",PreviewKey="v11.themePreview";
        private static List<GameTheme> themes;
        public static GameTheme Current{get;private set;}
        /// <summary>V1.1 (W22i): how many content pictures the last theme switch released.</summary>
        public static int LastReleasedArt{get;private set;}
        public static event Action<GameTheme> Changed;

        public static IReadOnlyList<GameTheme> All{get{Ensure();return themes;}}

        private static void Ensure()
        {
            if(themes!=null)return;
            themes=new List<GameTheme>{GameTheme.CreateDefault()};
            foreach(var file in Resources.LoadAll<TextAsset>("Themes").Where(t=>t!=null&&t.name=="theme"))
            {
                var theme=GameTheme.FromFile(file);
                if(theme!=null&&themes.All(t=>t.Id!=theme.Id))themes.Add(theme);
            }
            themes=themes.OrderBy(t=>t.Order).ThenBy(t=>t.Id,StringComparer.Ordinal).ToList();
        }

        /// <summary>Unfinished themes are listed only when previews are enabled (development captures and tests).</summary>
        public static bool ShowPreviewThemes=>PlayerPrefs.GetInt(PreviewKey,0)==1;

        public static IReadOnlyList<GameTheme> Selectable(bool includePreview)=>All.Where(t=>includePreview||!t.Preview).ToList();

        public static GameTheme Find(string id)=>All.FirstOrDefault(t=>t.Id==id);

        /// <summary>Makes a theme current: UiKit colours and sprites follow it from the next redraw on.</summary>
        public static GameTheme Activate(string id)
        {
            var theme=Find(id)??Find(GameTheme.DefaultId);
            theme.EnsureLoaded();
            // V1.1 (W22i): the theme that stops being current gives back its content art; its UI sprites, tables and preview
            // (a few MiB) stay, as before.
            if(ReferenceEquals(pendingRelease,theme))pendingRelease=null;
            if(Current!=null&&!ReferenceEquals(Current,theme))ScheduleRelease(Current);
            Current=theme;
            UiKit.Palette=theme.Palette;
            UiKit.TitleFont=theme.TitleFont;
            UiKit.TitleRoles=theme.TitleRoles;
            UiKit.ThemeTemplate=theme.IsDefault?null:(Func<string,Sprite>)theme.Template;
            Changed?.Invoke(theme);
            return theme;
        }

        private static GameTheme pendingRelease;
        private static bool releaseHooked;
        /// <summary>
        /// V1.1 (W22i): the previous theme's pictures are released when the canvases next render, after the redraw that
        /// follows the switch has given every visible image its new picture. Released at once, the pictures still on screen
        /// came back: UGUI's Image.sprite setter compares the old sprite's texture with the new one, which loads a released
        /// texture again, and nothing would release it after that.
        /// </summary>
        private static void ScheduleRelease(GameTheme previous)
        {
            // A second switch before the first was drawn: the theme waiting since then goes now (nothing shows it any more).
            if(pendingRelease!=null&&!ReferenceEquals(pendingRelease,previous))Release(pendingRelease);
            pendingRelease=previous;
            if(!releaseHooked){Canvas.willRenderCanvases+=ReleasePending;releaseHooked=true;}
        }
        private static void ReleasePending()
        {
            Canvas.willRenderCanvases-=ReleasePending;releaseHooked=false;
            var theme=pendingRelease;pendingRelease=null;
            if(theme!=null)Release(theme);
        }
        /// <summary>Releases a theme's content art unless the current theme draws it (itself, or a test theme borrowing it).</summary>
        private static void Release(GameTheme theme)
        {
            if(ReferenceEquals(theme,Current)||Current!=null&&ReferenceEquals(theme,Current.ArtSource))return;
            LastReleasedArt=theme.ReleaseContentArt();
        }

        /// <summary>Tests only: forget discovered themes and return UiKit to the default palette and sprites.</summary>
        public static void ResetForTests()
        {
            if(releaseHooked){Canvas.willRenderCanvases-=ReleasePending;releaseHooked=false;}
            pendingRelease=null;
            themes=null;testThemes.Clear();Current=null;UiKit.Palette=null;UiKit.ThemeTemplate=null;
            UiKit.TitleFont=null;UiKit.TitleRoles=TextRole.None;
        }

        private static readonly HashSet<string> testThemes=new HashSet<string>(StringComparer.Ordinal);
        /// <summary>Tests only (W20X-02): offers a theme built in memory (GameTheme.CreateForTests) until it is unregistered.
        /// It cannot replace the default or a theme read from Resources.</summary>
        public static void RegisterForTests(GameTheme theme)
        {
            if(theme==null||theme.IsDefault)throw new ArgumentException("Register a test theme with its own id.",nameof(theme));
            Ensure();
            if(themes.Any(t=>t.Id==theme.Id&&!testThemes.Contains(t.Id)))throw new ArgumentException("A theme read from Resources has the id "+theme.Id+".",nameof(theme));
            themes.RemoveAll(t=>t.Id==theme.Id);themes.Add(theme);testThemes.Add(theme.Id);
        }
        /// <summary>Tests only (W20X-02): removes a theme registered by RegisterForTests. Activate another theme afterwards
        /// when it was the current one.</summary>
        public static void UnregisterForTests(string id)
        {
            if(themes==null||!testThemes.Remove(id))return;
            themes.RemoveAll(t=>t.Id==id);
        }
    }
}
