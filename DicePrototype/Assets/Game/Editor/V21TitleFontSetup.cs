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
        private const string SancaiSource = FontRoot + "/Themes/sancai/MaShanZheng-Regular.ttf";
        public const string SancaiTitle = "Assets/Resources/Themes/sancai/Fonts/title.asset";
        private const string SettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string SourceMissing = "²·‹›←↑→↓≈≤≥◆◇●✓";
        private const int PointSize = 56;
        private const int AtlasSize = 2048;

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
        public static void BuildSancai()
        {
            var record = JsonUtility.FromJson<SourceRecord>(
                File.ReadAllText(FontRoot + "/Themes/sancai/SOURCE.json"));
            string expectedHash = record.local_files.Single(f => f.name == Path.GetFileName(SancaiSource)).sha256;
            using (var stream = File.OpenRead(SancaiSource))
            using (var hash = SHA256.Create())
                if (Hex(hash.ComputeHash(stream)) != expectedHash)
                    throw new InvalidDataException("Sancai source differs from SOURCE.json");

            V071FontSetup.BuildBold();
            var regular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(V071FontSetup.RegularPath);
            var bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(V071FontSetup.BoldPath);
            var source = AssetDatabase.LoadAssetAtPath<Font>(SancaiSource);
            if (!source || !regular || !bold) throw new InvalidDataException("Missing TMP input");
            var required = RequiredCharacters("title");
            var font = Bake(source, regular, bold, required);
            try
            {
                // A second independent bake must match glyph IDs, atlas rectangles and pixels.
                var repeat = Bake(source, regular, bold, required);
                try
                {
                    string digest = Fingerprint(font);
                    if (digest != Fingerprint(repeat))
                        throw new InvalidDataException("Non-reproducible Sancai atlas");
                    StoreFont(font);
                    Debug.Log($"W21_SANCAI chars={font.characterTable.Count} atlas={AtlasSize}x{AtlasSize} " +
                              $"atlasAndGlyphsSha256={digest} sourceSha256={expectedHash}");
                }
                finally { ReleaseTemporary(repeat); }
            }
            finally { ReleaseTemporary(font); }

            var settings = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(SettingsPath));
            var defaultFont = settings.FindProperty("m_defaultFontAsset");
            if (defaultFont.objectReferenceValue != regular)
            {
                defaultFont.objectReferenceValue = regular;
                settings.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(settings.targetObject);
            }
        }

        private static TMP_FontAsset Bake(Font source, TMP_FontAsset regular, TMP_FontAsset bold, uint[] required)
        {
            var font = TMP_FontAsset.CreateFontAsset(source, PointSize, 8, GlyphRenderMode.SDFAA,
                AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);
            if (!font) throw new InvalidDataException("FontEngine could not read " + SancaiSource);
            try
            {
                font.name = "title";
                font.TryAddCharacters(required, out uint[] missing, true);
                var fallback = missing ?? Array.Empty<uint>();
                var expected = required.Where(c => c <= char.MaxValue && SourceMissing.Contains((char)c));
                if (!fallback.OrderBy(c => c).SequenceEqual(expected) ||
                    fallback.Any(c => !bold.characterLookupTable.ContainsKey(c)))
                    throw new InvalidDataException("Unexpected fallback set: " + string.Join(",", fallback));
                if (font.atlasTextureCount != 1)
                    throw new InvalidDataException("Sancai title exceeded one 2048² atlas");
                font.atlasPopulationMode = AtlasPopulationMode.Static;
                font.fallbackFontAssetTable = new List<TMP_FontAsset> { bold, regular };
                var face = font.faceInfo;
                face.scale = 0.96f;
                // TMP multiplies face metrics by face.scale when creating line boxes.
                float units = face.pointSize / face.scale;
                face.lineHeight = 1.448f * units;
                face.ascentLine = 1.16f * units;
                face.descentLine = -0.288f * units;
                // TMP multiplies baselineOffset by the face scale a second time.
                face.baseline = 0.041f * units / face.scale;
                font.faceInfo = face;
                font.material.name = "title Material";
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

        private static void StoreFont(TMP_FontAsset font)
        {
            var saved = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SancaiTitle);
            if (!saved)
            {
                if (File.Exists(SancaiTitle)) throw new InvalidDataException("Unexpected asset at " + SancaiTitle);
                string parent = Path.GetDirectoryName(SancaiTitle).Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(parent))
                    AssetDatabase.CreateFolder(Path.GetDirectoryName(parent).Replace('\\', '/'), "Fonts");
                AssetDatabase.CreateAsset(font, SancaiTitle);
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
