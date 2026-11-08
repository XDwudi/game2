using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace DiceCraft.Editor
{
    /// <summary>Reproducible real Bold face for the existing Chinese TMP family.</summary>
    public static class V071FontSetup
    {
        public const string RegularPath="Assets/Game/Fonts/DiceChinese.asset";
        public const string SourcePath="Assets/Game/Fonts/NotoSansSC-Bold.otf";
        public const string BoldPath="Assets/Game/Fonts/DiceChineseBold.asset";
        private const string SourceSha256="d1961be1161ea1be08496c920862d06ea5c23a757628f4fd69368de1d9f51bed";
        private const int PointSize=48,Padding=7,AtlasSize=2048;

        [MenuItem("DiceCraft/Art071/Build and bind real Chinese Bold font")]
        public static void BuildBold()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Run font setup after compilation and outside Play Mode.");
            if(!File.Exists(SourcePath) || FileSha(SourcePath)!=SourceSha256)
                throw new InvalidOperationException("Bold source missing or changed; check ArtSource/Art071/Fonts/SOURCE.json.");
            var regular=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RegularPath);
            if(regular==null)throw new InvalidOperationException("Missing existing Regular font: "+RegularPath);
            if(regular.faceInfo.pointSize!=PointSize || regular.atlasPadding!=Padding || regular.atlasWidth!=AtlasSize || regular.atlasHeight!=AtlasSize || regular.atlasRenderMode!=GlyphRenderMode.SDFAA)
                throw new InvalidOperationException("Regular settings changed; review the font recipe before regenerating.");
            uint[] required=regular.characterTable.Select(c=>c.unicode).Distinct().OrderBy(c=>c).ToArray();
            if(required.Length==0 || !required.Contains(0x2026u))
                throw new InvalidOperationException("Regular character table must include U+2026.");
            var priorCharacters=regular.characterTable.ToArray();
            var priorGlyphs=regular.glyphTable.ToArray();
            var priorAtlases=regular.atlasTextures.ToArray();
            string[] priorAtlasHashes=priorAtlases.Select(AtlasSha).ToArray();
            var priorWeights=regular.fontWeightTable.ToArray();
            if(priorWeights.Length!=10)throw new InvalidOperationException("Unexpected Regular font weight table.");

            var source=AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
            if(source==null)
            {
                AssetDatabase.ImportAsset(SourcePath,ImportAssetOptions.ForceSynchronousImport);
                source=AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
            }
            if(source==null)throw new InvalidOperationException("Import the Bold source with Include Font Data enabled.");
            var bold=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldPath);
            bool created=false;
            if(bold==null)
            {
                if(File.Exists(BoldPath) || AssetDatabase.LoadMainAssetAtPath(BoldPath)!=null)
                    throw new InvalidOperationException("Existing non-font or unreadable Bold asset is preserved; review it manually.");
                bold=CreateBold(source,required);
                created=true;
            }
            // An existing good font is reused byte-for-byte. An incomplete or stale
            // one fails validation; this tool never deletes/rebuilds an existing asset.
            ValidateBold(bold,required);
            if(priorWeights[7].regularTypeface!=null && priorWeights[7].regularTypeface!=bold)
                throw new InvalidOperationException("Another Bold typeface is already configured; it has not been replaced.");

            bool changed=regular.fontWeightTable[7].regularTypeface!=bold;
            try
            {
                regular.fontWeightTable[7].regularTypeface=bold;
                bool alternative;
                var ellipsis=TMP_FontAssetUtilities.GetCharacterFromFontAsset(0x2026,regular,true,FontStyles.Bold,FontWeight.Bold,out alternative);
                if(ellipsis==null || ellipsis.textAsset!=bold || !alternative)
                    throw new InvalidOperationException("Real Bold ellipsis lookup did not resolve to the new face.");
                var regularEllipsis=TMP_FontAssetUtilities.GetCharacterFromFontAsset(0x2026,regular,false,FontStyles.Normal,FontWeight.Regular,out alternative);
                if(regularEllipsis==null || regularEllipsis.textAsset!=regular || alternative)
                    throw new InvalidOperationException("Regular ellipsis resolution changed unexpectedly.");
                if(!priorCharacters.SequenceEqual(regular.characterTable) || !priorGlyphs.SequenceEqual(regular.glyphTable) || !priorAtlases.SequenceEqual(regular.atlasTextures) || !priorAtlasHashes.SequenceEqual(regular.atlasTextures.Select(AtlasSha)))
                    throw new InvalidOperationException("Regular glyphs or atlas bytes changed unexpectedly.");
                for(int i=0;i<priorWeights.Length;i++)
                    if(regular.fontWeightTable[i].italicTypeface!=priorWeights[i].italicTypeface || (i!=7 && regular.fontWeightTable[i].regularTypeface!=priorWeights[i].regularTypeface))
                        throw new InvalidOperationException("An unrelated weight mapping changed.");
            }
            catch
            {
                regular.fontWeightTable[7]=priorWeights[7];
                throw;
            }
            if(changed)
            {
                EditorUtility.SetDirty(regular);
                AssetDatabase.SaveAssetIfDirty(regular);
            }
            Debug.Log("V071_FONT_READY created="+created+" bindingChanged="+changed+" required="+required.Length+" boldCharacters="+bold.characterTable.Count+" atlasCount="+bold.atlasTextures.Length+" boldEllipsis=True regularAtlasUnchanged=True sourceSha256="+SourceSha256);
        }

        private static TMP_FontAsset CreateBold(Font source,uint[] required)
        {
            var bold=TMP_FontAsset.CreateFontAsset(source,PointSize,Padding,GlyphRenderMode.SDFAA,AtlasSize,AtlasSize,AtlasPopulationMode.Dynamic,true);
            if(bold==null)throw new InvalidOperationException("FontEngine could not load the verified Bold source.");
            try
            {
                bold.name="DiceChineseBold";
                if(!bold.TryAddCharacters(required,out uint[] missing,true) || (missing!=null && missing.Length>0))
                    throw new InvalidOperationException("Bold coverage incomplete: "+string.Join(",",(missing??Array.Empty<uint>()).Select(c=>"U+"+c.ToString("X4"))));
                bold.atlasPopulationMode=AtlasPopulationMode.Static;
                bold.material.name="DiceChineseBold Material";
                for(int i=0;i<bold.atlasTextures.Length;i++)bold.atlasTextures[i].name="DiceChineseBold Atlas "+i;
                ValidateBold(bold,required);
                AssetDatabase.CreateAsset(bold,BoldPath);
                AssetDatabase.AddObjectToAsset(bold.material,bold);
                foreach(var atlas in bold.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,bold);
                EditorUtility.SetDirty(bold);
                AssetDatabase.SaveAssetIfDirty(bold);
                return bold;
            }
            catch
            {
                // Only release objects that never became assets. Persisted partial
                // output is retained for diagnosis, never silently overwritten.
                if(!AssetDatabase.Contains(bold))
                {
                    foreach(var atlas in bold.atlasTextures)if(atlas!=null && !AssetDatabase.Contains(atlas))UnityEngine.Object.DestroyImmediate(atlas);
                    if(bold.material!=null && !AssetDatabase.Contains(bold.material))UnityEngine.Object.DestroyImmediate(bold.material);
                    UnityEngine.Object.DestroyImmediate(bold);
                }
                throw;
            }
        }

        private static void ValidateBold(TMP_FontAsset bold,uint[] required)
        {
            if(bold.atlasPopulationMode!=AtlasPopulationMode.Static || bold.faceInfo.pointSize!=PointSize || bold.atlasPadding!=Padding || bold.atlasWidth!=AtlasSize || bold.atlasHeight!=AtlasSize || bold.atlasRenderMode!=GlyphRenderMode.SDFAA)
                throw new InvalidOperationException("Bold asset settings differ from the fixed recipe; existing asset preserved.");
            var sourceGuid=new SerializedObject(bold).FindProperty("m_SourceFontFileGUID");
            if(sourceGuid==null || sourceGuid.stringValue!=AssetDatabase.AssetPathToGUID(SourcePath) || !string.Equals(bold.faceInfo.styleName,"Bold",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Bold asset source identity does not match the verified font.");
            if(bold.material==null || bold.atlasTextures.Length==0 || bold.atlasTextures.Any(a=>a==null || a.width!=AtlasSize || a.height!=AtlasSize))
                throw new InvalidOperationException("Bold material or atlas is incomplete.");
            foreach(uint unicode in required)
                if(!bold.characterLookupTable.TryGetValue(unicode,out var character) || character.textAsset!=bold || character.glyph==null || character.glyph.atlasIndex>=bold.atlasTextures.Length)
                    throw new InvalidOperationException("Bold glyph missing or invalid: U+"+unicode.ToString("X4"));
        }

        private static string AtlasSha(Texture2D texture)
        {
            if(texture==null || !texture.isReadable)throw new InvalidOperationException("Readable Regular atlas required to verify unchanged bytes.");
            using(var digest=SHA256.Create())return Hex(digest.ComputeHash(texture.GetRawTextureData()));
        }
        private static string FileSha(string path)
        {using(var stream=File.OpenRead(path))using(var digest=SHA256.Create())return Hex(digest.ComputeHash(stream));}
        private static string Hex(byte[] hash)=>BitConverter.ToString(hash).Replace("-","").ToLowerInvariant();
    }
}
