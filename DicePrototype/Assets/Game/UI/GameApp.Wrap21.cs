using System;
using TMPro;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        [Serializable]
        private sealed class BreakWords21
        {
            public int version;
            public string[] words;
        }

        private static LineBreak21 lineBreaker21;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadBreakWords21()
        {
            var asset = Resources.Load<TextAsset>("Data/LineBreakWords");
            var data = asset == null ? null : JsonUtility.FromJson<BreakWords21>(asset.text);
            if (data == null || data.version != 1 || data.words == null || data.words.Length == 0)
                throw new InvalidOperationException("Missing or invalid Data/LineBreakWords.");
            lineBreaker21 = new LineBreak21(data.words);
        }

        private static string WrapLines21(TextMeshProUGUI label, string text, float width, int maxLines)
            => WrapMeasured21(label, text, width, maxLines, label.fontSize, false);

        // These paragraphs grow or fit their font to the measured height. Keep the balanced two-line
        // layout when it fits, and give longer texts explicit whole-word lines before TMP measures them.
        private static string WrapParagraph21(TextMeshProUGUI label,string text,float width)
            => WrapMeasured21(label,text,width,2,label.fontSize,true) ?? WrapLines21(label,text,width,int.MaxValue) ?? text;

        private static string WrapBalanced21(TextMeshProUGUI label, string text, float width)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('\n') >= 0 || text.Length < 6) return text;
            float size = label.enableAutoSizing ? label.fontSizeMax : label.fontSize;
            string wrapped = WrapMeasured21(label, text, width, 2, size, true);
            if (wrapped == null && label.enableAutoSizing && label.fontSizeMin < size)
            {
                float low = label.fontSizeMin, high = size;
                wrapped = WrapMeasured21(label, text, width, 2, low, true);
                while (wrapped != null && high - low > .05f)
                {
                    float mid = (low + high) * .5f;
                    string trial = WrapMeasured21(label, text, width, 2, mid, true);
                    if (trial == null) high = mid;
                    else { low = mid; wrapped = trial; }
                }
            }
            return wrapped ?? text;
        }

        private static string WrapMeasured21(TextMeshProUGUI label, string text, float width, int maxLines, float size, bool balanced)
        {
            if (lineBreaker21 == null) LoadBreakWords21();
            var font = label.font ?? UiKit.Font;
            return lineBreaker21.Wrap(text, width, maxLines, ch =>
            {
                float advance = GlyphAdvance11(font, ch, size, 0);
                return advance >= 0 ? advance : size * (ch >= 0x2E80 ? 1 : ch == ' ' ? .3f : .62f);
            }, balanced);
        }
    }
}
