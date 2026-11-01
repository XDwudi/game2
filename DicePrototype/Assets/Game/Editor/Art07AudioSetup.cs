using System;
using System.IO;
using System.Linq;
using DiceCraft.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace DiceCraft.Editor
{
    /// <summary>Imports authored WAV files and binds an existing, editor-created Mixer. No private APIs or YAML authoring.</summary>
    public static class Art07AudioSetup
    {
        public const string AudioRoot = "Assets/Art/Art07/Audio";
        public const string MixerPath = AudioRoot + "/mixer_art07.mixer";
        public static readonly string[] RequiredGroups = { "Master", "Music", "SFX", "UI", "Dice", "Score", "Ambience" };
        public static readonly string[] RequiredParameters = { "MasterVolume", "MusicVolume", "SFXVolume", "UIVolume" };

        public static void ImportClips()
        {
            if (!Directory.Exists(AudioRoot)) throw new InvalidOperationException("Art07 WAV exports are missing.");
            AssetDatabase.Refresh();
            foreach (string source in Directory.GetFiles(AudioRoot, "*.wav", SearchOption.TopDirectoryOnly))
            {
                string path = source.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) throw new InvalidOperationException("Missing AudioImporter: " + path);
                bool music = Path.GetFileNameWithoutExtension(path) == "music_table_loop";
                importer.forceToMono = !music;
                importer.loadInBackground = false;
                importer.ambisonic = false;
                var settings = importer.defaultSampleSettings;
                settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
                settings.quality = music ? .7f : 1f;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                settings.preloadAudioData = !music;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }

        public static void Integrate(GameApp app)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before changing scene asset references.");
            ImportClips();
            var clips = Directory.GetFiles(AudioRoot, "*.wav", SearchOption.TopDirectoryOnly)
                .Select(p => AssetDatabase.LoadAssetAtPath<AudioClip>(p.Replace('\\', '/'))).OrderBy(c => c.name, StringComparer.Ordinal).ToArray();
            var expected = AudioCueMap.Cues.SelectMany(c => c.AssetIds).Concat(new[] { "music_table_loop" }).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            if (clips.Any(c => c == null) || !clips.Select(c => c.name).SequenceEqual(expected)) throw new InvalidOperationException("Art07 must contain exactly the 25 mapped SFX and one table music loop.");
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null) throw new InvalidOperationException("Create " + MixerPath + " in Unity Audio Mixer, with Master→Music/SFX/UI, SFX→Dice/Score/Ambience, and expose MasterVolume/MusicVolume/SFXVolume/UIVolume. WAV import completed; scene routing has not been altered.");
            var groups = mixer.FindMatchingGroups("");
            foreach (string name in RequiredGroups)
                if (groups.Count(g => g.name == name) != 1) throw new InvalidOperationException("Mixer requires exactly one group named " + name);
            foreach (string parameter in RequiredParameters)
                if (!mixer.GetFloat(parameter, out _)) throw new InvalidOperationException("Mixer volume parameter is not exposed: " + parameter);
            Undo.RecordObject(app, "Integrate Art07 audio references");
            var serialized = new SerializedObject(app);
            AssignObject(serialized, "art07Mixer", mixer);
            AssignArray(serialized, "art07Buses", RequiredGroups.Select(name => groups.Single(g => g.name == name)).Cast<UnityEngine.Object>().ToArray());
            AssignArray(serialized, "art07Sounds", clips.Cast<UnityEngine.Object>().ToArray());
            serialized.ApplyModifiedProperties(); EditorUtility.SetDirty(app);
            AssetDatabase.SaveAssets();
        }
        private static void AssignObject(SerializedObject target, string name, UnityEngine.Object value)
        { var property = target.FindProperty(name); if (property == null) throw new InvalidOperationException("GameApp is missing serialized field " + name); property.objectReferenceValue = value; }
        private static void AssignArray(SerializedObject target, string name, UnityEngine.Object[] values)
        {
            var property = target.FindProperty(name); if (property == null || !property.isArray) throw new InvalidOperationException("GameApp is missing serialized array " + name);
            property.arraySize = values.Length; for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
