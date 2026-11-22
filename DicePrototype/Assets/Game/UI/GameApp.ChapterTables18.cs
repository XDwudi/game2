using System;
using System.Collections.Generic;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        [Serializable] private sealed class ChapterToneFile18 { public ChapterToneRow18[] themes; }
        [Serializable] private sealed class ChapterToneRow18 { public string id; public string[] colors; }
        private static readonly int ChapterShiftId18 = Shader.PropertyToID("_ChapterShift18");
        private static Dictionary<string,Color[]> chapterTones18;
        private static Texture2D defaultPlaySurface18;
        private Vector4 shownChapterShift18;

        private static Dictionary<string,Color[]> LoadChapterTones18()
        {
            var tones = new Dictionary<string,Color[]>(StringComparer.Ordinal);
            var asset = Resources.Load<TextAsset>("ChapterTables18/tones");
            if (asset == null) return tones;
            foreach (var row in JsonUtility.FromJson<ChapterToneFile18>(asset.text).themes)
            {
                var colors = new Color[row.colors.Length];
                for (int i = 0; i < colors.Length; i++)
                {
                    var srgb = UiKit.Hex(row.colors[i]);
                    colors[i] = QualitySettings.activeColorSpace == ColorSpace.Linear ? srgb.linear : srgb;
                }
                tones.Add(row.id, colors);
            }
            return tones;
        }

        // Compose the play surface and chapter colour before W11's existing stage mood and lighting.
        // Preview pages, shops and galleries keep their own background, even if a run is loaded.
        private Texture ChapterPicture18(GameTheme theme, Texture page, out Vector4 shift)
        {
            shift = Vector4.zero;
            if (!PlayBackdrop11 || ViewState?.stage == null) return page;
            chapterTones18 ??= LoadChapterTones18();
            if (!chapterTones18.TryGetValue(theme.Id, out var tones)) return page;
            int chapter = Config.stages[ViewState.stage.stageIndex].chapter - 1;
            if (chapter < 0 || chapter >= tones.Length) return page;
            if (chapter > 0)
            {
                Color delta = tones[chapter] - tones[0];
                shift = new Vector4(delta.r, delta.g, delta.b, theme.IsDefault ? 1 : 2);
            }
            if (!theme.IsDefault) return page;
            if (defaultPlaySurface18 == null)
                defaultPlaySurface18 = Resources.Load<Texture2D>("ChapterTables18/default_play");
            return defaultPlaySurface18 != null ? defaultPlaySurface18 : page;
        }
    }
}
