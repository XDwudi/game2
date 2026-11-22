using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

namespace DiceCraft.Editor
{
    public static class V21TitleFontSetup
    {
        private const string FontRoot = "Assets/Game/Fonts";
        private const string SettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string MissingSymbols = "²·‹›←↑→↓≈≤≥◆◇●✓";
        private const int AtlasSize = 2048;

        private sealed class TitleSpec
        {
            public readonly string theme, missing;
            public readonly int pointSize, padding;
            public readonly float scale, baseline;
            public string folder => FontRoot + "/Themes/" + theme + "/";
            public string destination => "Assets/Resources/Themes/" + theme + "/Fonts/title.asset";

            public TitleSpec(string theme, int pointSize, int padding, float scale, float baseline, string missing)
            {
                this.theme = theme; this.pointSize = pointSize; this.padding = padding;
                this.scale = scale; this.baseline = baseline; this.missing = missing;
            }
        }
        private static readonly TitleSpec[] Titles = {
            new TitleSpec("sancai", 56, 8, .96f, .041f, MissingSymbols),
            new TitleSpec("comic", 56, 8, 1.05f, .003f, MissingSymbols.Replace("·", "")),
            new TitleSpec("deco", 56, 8, 1f, .021f, MissingSymbols),
            new TitleSpec("glass", 48, 7, .98f, .008f, "")
        };
        [Serializable] private class FileRecord { public string name, sha256; }
        [Serializable] private class SourceRecord { public FileRecord[] local_files; }

        internal static uint[] RequiredCharacters(string name)
        {
            string path = FontRoot + "/Themes/" + name + "_chars.txt";
            string text = File.ReadAllText(path).TrimEnd('\r', '\n');
            var characters = new SortedSet<uint>();
            for (int i = 0; i < text.Length; i++)
            {
                characters.Add((uint)char.ConvertToUtf32(text, i));
                if (char.IsHighSurrogate(text[i])) i++;
            }
            if (characters.Count == 0) throw new InvalidDataException("Empty font character set: " + path);
            return characters.ToArray();
        }

        [MenuItem("DiceCraft/V11/Build Sancai title font")]
        public static void BuildSancai() => Build(new[] { Titles[0] });

        [MenuItem("DiceCraft/V11/Build all approved title fonts")]
        public static void BuildAll() => Build(Titles);

        private static void Build(IEnumerable<TitleSpec> specs)
        {
            V071FontSetup.BuildBold();
            var regular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(V071FontSetup.RegularPath);
            var bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(V071FontSetup.BoldPath);
            if (!regular || !bold) throw new InvalidDataException("Missing TMP fallback");
            var required = RequiredCharacters("title");
            foreach (var spec in specs) BuildTitle(spec, regular, bold, required);

            var settings = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(SettingsPath));
            var defaultFont = settings.FindProperty("m_defaultFontAsset");
            if (defaultFont.objectReferenceValue != regular)
            {
                defaultFont.objectReferenceValue = regular;
                settings.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(settings.targetObject);
            }
        }

        private static void BuildTitle(TitleSpec spec, TMP_FontAsset regular, TMP_FontAsset bold, uint[] required)
        {
            var record = JsonUtility.FromJson<SourceRecord>(File.ReadAllText(spec.folder + "SOURCE.json"));
            foreach (var input in record.local_files)
                using (var stream = File.OpenRead(spec.folder + input.name))
                using (var hash = SHA256.Create())
                    if (Hex(hash.ComputeHash(stream)) != input.sha256)
                        throw new InvalidDataException("Bundled input differs from SOURCE.json: " + input.name);
            var fontFile = record.local_files.Single(f => f.name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
                                                     || f.name.EndsWith(".otf", StringComparison.OrdinalIgnoreCase));
            var source = AssetDatabase.LoadAssetAtPath<Font>(spec.folder + fontFile.name);
            if (!source) throw new InvalidDataException("Missing title source: " + fontFile.name);
            var font = Bake(spec, source, regular, bold, required);
            try
            {
                // A second independent bake must match glyph IDs, atlas rectangles and pixels.
                var repeat = Bake(spec, source, regular, bold, required);
                try
                {
                    string digest = Fingerprint(font);
                    if (digest != Fingerprint(repeat))
                        throw new InvalidDataException("Non-reproducible title atlas: " + spec.theme);
                    StoreFont(font, spec.destination);
                    Debug.Log($"W21_{spec.theme.ToUpperInvariant()} chars={font.characterTable.Count} atlas={AtlasSize}x{AtlasSize} " +
                              $"atlasAndGlyphsSha256={digest} sourceSha256={fontFile.sha256}");
                }
                finally { ReleaseTemporary(repeat); }
            }
            finally { ReleaseTemporary(font); }

        }

        private static TMP_FontAsset Bake(TitleSpec spec, Font source, TMP_FontAsset regular, TMP_FontAsset bold, uint[] required)
        {
            var font = TMP_FontAsset.CreateFontAsset(source, spec.pointSize, spec.padding, GlyphRenderMode.SDFAA,
                AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);
            if (!font) throw new InvalidDataException("FontEngine could not read " + source.name);
            try
            {
                font.name = "title";
                font.TryAddCharacters(required, out uint[] missing, true);
                var fallback = missing ?? Array.Empty<uint>();
                var expected = required.Where(c => c <= char.MaxValue && spec.missing.Contains((char)c));
                if (!fallback.OrderBy(c => c).SequenceEqual(expected) ||
                    fallback.Any(c => !bold.characterLookupTable.ContainsKey(c)))
                    throw new InvalidDataException("Unexpected fallback set: " + string.Join(",", fallback));
                if (font.atlasTextureCount != 1)
                    throw new InvalidDataException(spec.theme + " title exceeded one 2048² atlas");
                font.atlasPopulationMode = AtlasPopulationMode.Static;
                font.fallbackFontAssetTable = new List<TMP_FontAsset> { bold, regular };
                var face = font.faceInfo;
                face.scale = spec.scale;
                // TMP multiplies face metrics by face.scale when creating line boxes.
                float units = face.pointSize / face.scale;
                face.lineHeight = 1.448f * units;
                face.ascentLine = 1.16f * units;
                face.descentLine = -0.288f * units;
                // TMP multiplies baselineOffset by the face scale a second time.
                face.baseline = spec.baseline * units / face.scale;
                font.faceInfo = face;
                font.material.name = "title Material";
                ShaderUtilities.UpdateShaderRatios(font.material);
                font.atlasTexture.name = "title Atlas";
                return font;
            }
            catch
            {
                ReleaseTemporary(font);
                throw;
            }
        }

        private static string Fingerprint(TMP_FontAsset font)
        {
            using (var hash = SHA256.Create())
            using (var data = new MemoryStream())
            using (var writer = new BinaryWriter(data))
            {
                foreach (var c in font.characterTable.OrderBy(c => c.unicode))
                {
                    writer.Write(c.unicode);
                    writer.Write(c.glyphIndex);
                }
                foreach (var glyph in font.glyphTable.OrderBy(g => g.index))
                {
                    writer.Write(glyph.index);
                    writer.Write(glyph.glyphRect.x); writer.Write(glyph.glyphRect.y);
                    writer.Write(glyph.glyphRect.width); writer.Write(glyph.glyphRect.height);
                    writer.Write(glyph.metrics.width); writer.Write(glyph.metrics.height);
                    writer.Write(glyph.metrics.horizontalBearingX); writer.Write(glyph.metrics.horizontalBearingY);
                    writer.Write(glyph.metrics.horizontalAdvance);
                }
                writer.Write(font.atlasTexture.GetRawTextureData());
                data.Position = 0;
                return Hex(hash.ComputeHash(data));
            }
        }

        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();

        private static void StoreFont(TMP_FontAsset font, string destination)
        {
            var saved = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(destination);
            if (!saved)
            {
                if (File.Exists(destination)) throw new InvalidDataException("Unexpected asset at " + destination);
                string parent = Path.GetDirectoryName(destination).Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(parent))
                    AssetDatabase.CreateFolder(Path.GetDirectoryName(parent).Replace('\\', '/'), "Fonts");
                AssetDatabase.CreateAsset(font, destination);
                AssetDatabase.AddObjectToAsset(font.material, font);
                AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
                saved = font;
            }
            else
            {
                var atlas = saved.atlasTexture;
                var material = saved.material;
                if (!atlas || !material || saved.atlasTextureCount != 1)
                    throw new InvalidDataException("Unexpected existing title sub-assets");
                // Keep GUID and both local file IDs on regeneration, including when W16/W15 add text.
                EditorUtility.CopySerialized(font.atlasTexture, atlas);
                EditorUtility.CopySerialized(font.material, material);
                material.mainTexture = atlas;
                EditorUtility.CopySerialized(font, saved);
                saved.atlasTextures = new[] { atlas };
                saved.material = material;
                EditorUtility.SetDirty(material);
            }
            // Toggle the serialized read-access flag without discarding the pixels before
            // the editor writes the texture sub-asset to disk.
            var texture = new SerializedObject(saved.atlasTextures[0]);
            texture.FindProperty("m_IsReadable").boolValue = false;
            texture.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(saved.atlasTextures[0]);
            EditorUtility.SetDirty(saved);
            AssetDatabase.SaveAssetIfDirty(saved);
        }

        private static void ReleaseTemporary(TMP_FontAsset font)
        {
            if (!font || AssetDatabase.Contains(font)) return;
            foreach (var atlas in font.atlasTextures)
                if (atlas && !AssetDatabase.Contains(atlas)) Object.DestroyImmediate(atlas);
            if (font.material && !AssetDatabase.Contains(font.material)) Object.DestroyImmediate(font.material);
            Object.DestroyImmediate(font);
        }
    }
}
