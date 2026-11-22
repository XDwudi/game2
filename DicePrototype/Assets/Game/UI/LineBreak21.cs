using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DiceCraft
{
    /// <summary>W21 Chinese card and hint line breaks. Widths use the units of the supplied glyph advances.</summary>
    public sealed class LineBreak21
    {
        [Flags]
        private enum Boundary : byte { Ordinary = 0, Forbidden = 1, InWord = 2, Style = 4, Punctuation = 8, Separator = 16 }

        private const string Open = "（“《【「‘([{";
        private const string Close = "，。；：、！？）”》】」’…,.!?;:)]}";
        private const string Operators = "+-−×÷→~≤≥≈=";
        private const string Numeral = @"(?:Lv\.\s*|[+\-−×÷≤≥≈~=]\s*)?[0-9]+(?:[.,][0-9]+)*%?";
        private static readonly Regex Numbers = new Regex(@"(?:第\s*)?" + Numeral + @"(?:\s*[~\-−→]\s*" + Numeral
            + @")*(?:\s*(?:金币|[点颗张件次格关级层个面手种倍家项%]))?", RegexOptions.CultureInvariant);
        private static readonly Regex ChineseMeasures = new Regex(@"第?[一二三四五六七八九十两几]+(?:金币|选[一二三四五六七八九十两]+|[点个颗张件次格关级层面手种倍家项对条])", RegexOptions.CultureInvariant);
        private static readonly Regex Parentheses = new Regex(@"（[^（），；\n]{1,8}）", RegexOptions.CultureInvariant);
        private readonly Dictionary<char, List<string>> wordsByFirst = new Dictionary<char, List<string>>();
        private readonly Dictionary<string, Boundary[]> boundaries = new Dictionary<string, Boundary[]>(StringComparer.Ordinal);

        public LineBreak21(IEnumerable<string> words)
        {
            foreach (string word in words)
            {
                if (string.IsNullOrEmpty(word) || word.Length < 2) throw new ArgumentException("Invalid line-break word.");
                if (!wordsByFirst.TryGetValue(word[0], out var entries))
                    wordsByFirst.Add(word[0], entries = new List<string>());
                entries.Add(word);
            }
        }

        private static void Protect(Boundary[] cuts, int start, int end, Boundary rule)
        {
            for (int i = start + 1; i < end; i++) cuts[i] |= rule;
        }

        private Boundary[] GetBoundaries(string text)
        {
            if (boundaries.TryGetValue(text, out var cached)) return cached;
            var cuts = new Boundary[text.Length + 1];
            foreach (Match match in Numbers.Matches(text))
            {
                int end = match.Index + match.Length;
                Protect(cuts, match.Index, end, Boundary.Forbidden);
                if (match.Index > 0 && "从按比向为由到约".IndexOf(text[match.Index - 1]) >= 0)
                    cuts[match.Index] |= Boundary.Style;
                if (end < text.Length && text[end] == '起') cuts[end] |= Boundary.Style;
            }
            foreach (Match match in ChineseMeasures.Matches(text))
                Protect(cuts, match.Index, match.Index + match.Length, Boundary.Forbidden);
            foreach (Match match in Parentheses.Matches(text))
                Protect(cuts, match.Index, match.Index + match.Length, Boundary.Style);

            for (int i = 0; i < text.Length; i++)
            {
                if (wordsByFirst.TryGetValue(text[i], out var candidates))
                    foreach (string word in candidates)
                        if (word.Length <= text.Length - i && string.CompareOrdinal(text, i, word, 0, word.Length) == 0)
                            Protect(cuts, i, i + word.Length, Boundary.InWord);
                if (i == 0) continue;
                char before = text[i - 1], after = text[i];
                bool separator = i + 3 < text.Length && after == ' ' && text[i + 1] == '·' && text[i + 2] == ' ';
                if (separator)
                {
                    cuts[i] |= Boundary.Separator | Boundary.Punctuation;
                    after = text[i + 3];
                }
                else if (char.IsWhiteSpace(before) || char.IsWhiteSpace(after))
                    cuts[i] |= Boundary.Forbidden;
                if (Close.IndexOf(after) >= 0 || Open.IndexOf(before) >= 0 || Operators.IndexOf(before) >= 0
                    || "每第这那各".IndexOf(before) >= 0 || char.IsLowSurrogate(after)
                    || char.GetUnicodeCategory(after) == UnicodeCategory.NonSpacingMark)
                    cuts[i] |= Boundary.Forbidden;
                if (Open.IndexOf(after) >= 0 || Close.IndexOf(before) >= 0)
                    cuts[i] |= Boundary.Punctuation;
                if (after == '时') cuts[i] |= Boundary.Style;
            }
            // Dynamic prompts include scores / prices. Retain recent word analysis without growing across a run.
            if (boundaries.Count >= 512) boundaries.Clear();
            boundaries.Add(text, cuts);
            return cuts;
        }

        private struct Choice
        {
            public int lines, inWord, shortLast, style, ordinary, end;
            public float widest, last;

            public bool BetterThan(Choice other, bool minimizeWidest)
            {
                if (other.lines == 0 || lines != other.lines) return other.lines == 0 || lines < other.lines;
                if (inWord != other.inWord) return inWord < other.inWord;
                if (shortLast != other.shortLast) return shortLast < other.shortLast;
                if (style != other.style) return style < other.style;
                if (ordinary != other.ordinary) return ordinary < other.ordinary;
                if (minimizeWidest && Math.Abs(widest - other.widest) > 0.0001f) return widest < other.widest;
                return last > other.last + 0.0001f;
            }
        }

        /// <summary>Returns null if the cap cannot be met. Words may split only for a finite cap and after exhausting
        /// whole-word layouts. Numbers with units, opening punctuation, and closing punctuation are never relaxed.</summary>
        public string Wrap(string text, float width, int maxLines, Func<char, float> advance, bool balanced = false)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (maxLines < 1 || width <= 0) return null;
            // Explicit author breaks take priority. New wrapping must still honour the caller's total line budget.
            if (text.IndexOf('\n') >= 0)
            {
                string[] paragraphs = text.Split('\n');
                if (paragraphs.Length > maxLines) return null;
                int remaining = maxLines;
                for (int i = 0; i < paragraphs.Length; i++)
                {
                    string result = Wrap(paragraphs[i], width, remaining - (paragraphs.Length - i - 1), advance, balanced);
                    if (result == null) return null;
                    remaining--;
                    foreach (char ch in result) if (ch == '\n') remaining--;
                    paragraphs[i] = result;
                }
                return string.Join("\n", paragraphs);
            }

            int n = text.Length;
            var size = new float[n + 1];
            var visible = new int[n + 1];
            for (int i = 0; i < n; i++)
            {
                char ch = text[i];
                size[i + 1] = size[i] + advance(ch);
                visible[i + 1] = visible[i] + (char.IsWhiteSpace(ch) || Open.IndexOf(ch) >= 0
                    || Close.IndexOf(ch) >= 0 || ch == '·' ? 0 : 1);
            }
            if (size[n] <= width + 0.0001f) return text;
            if (maxLines == 1) return null;
            Boundary[] cuts = GetBoundaries(text);
            var paths = new Choice[n + 1];
            int minChars = balanced ? 3 : 2;
            // A second pass permits a word break only when the actual height cap requires one.
            bool relaxable = !balanced && maxLines != int.MaxValue;
            for (int pass = 0; pass < (relaxable ? 2 : 1); pass++)
            {
                Solve(width, pass == 1, true);
                if (paths[0].lines == 0) continue;
                // A wide first line can mask differences between suffixes. With the minimum possible longest
                // line fixed for the whole paragraph, break ties by maximizing the final line globally.
                Solve(paths[0].widest, pass == 1, false);
                var output = new StringBuilder(n + paths[0].lines - 1);
                for (int start = 0; start < n;)
                {
                    int from = start + ((cuts[start] & Boundary.Separator) != 0 ? 3 : 0);
                    int end = paths[start].end;
                    output.Append(text, from, end - from);
                    if (end < n) output.Append('\n');
                    start = end;
                }
                return output.ToString();
            }
            return null;

            void Solve(float limit, bool allowWordBreak, bool minimizeWidest)
            {
                Array.Clear(paths, 0, paths.Length);
                for (int start = n - 1; start >= 0; start--)
                {
                    if ((cuts[start] & Boundary.Forbidden) != 0) continue;
                    int from = start + ((cuts[start] & Boundary.Separator) != 0 ? 3 : 0);
                    for (int end = from + 1; end <= n; end++)
                    {
                        float span = size[end] - size[from];
                        if (span > limit + 0.0001f) break;
                        int count = visible[end] - visible[from];
                        if (end == n)
                        {
                            if (count < minChars) continue;
                            var last = new Choice { lines = 1, shortLast = count < 3 ? 1 : 0, widest = span, last = span, end = n };
                            if (last.BetterThan(paths[start], minimizeWidest)) paths[start] = last;
                            continue;
                        }
                        if (balanced && count < minChars) continue;
                        Boundary cut = cuts[end];
                        if ((cut & Boundary.Forbidden) != 0 || (!allowWordBreak && (cut & Boundary.InWord) != 0)) continue;
                        Choice option = paths[end];
                        if (option.lines == 0 || option.lines >= maxLines) continue;
                        option.lines++;
                        if ((cut & Boundary.InWord) != 0) option.inWord++;
                        if ((cut & Boundary.Style) != 0) option.style++;
                        if ((cut & Boundary.Punctuation) == 0) option.ordinary++;
                        option.widest = Math.Max(span, option.widest);
                        option.end = end;
                        if (option.BetterThan(paths[start], minimizeWidest)) paths[start] = option;
                    }
                }
            }
        }
    }
}
