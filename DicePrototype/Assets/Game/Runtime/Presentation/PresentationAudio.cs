using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace DiceCraft.Presentation
{
    /// <summary>Configure with generated clips and the project's mixer; silent missing clips do not block rules.</summary>
    public sealed class PresentationAudioPlayer : MonoBehaviour
    {
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioMixerGroup> groups = new Dictionary<string, AudioMixerGroup>(StringComparer.Ordinal);
        private readonly AudioVoiceBudget budget = new AudioVoiceBudget();
        private readonly AudioSource[] voices = new AudioSource[12];
        private readonly string[] voiceBuses = new string[12];
        private readonly float[] voiceGains = new float[12];
        private AudioSource music;
        private AudioMixer mixer;
        private int variantSerial;
        private float master = .7f, musicVolume = .35f, sfx = .7f, ui = .7f, duck = 1f;
        private double duckUntil;
        public bool HoverEnabled { get; set; } = true;
        public void Configure(AudioClip[] assets, AudioMixer audioMixer = null, AudioMixerGroup[] buses = null)
        {
            clips.Clear(); foreach (var clip in assets ?? Array.Empty<AudioClip>()) if (clip != null) clips[clip.name] = clip;
            groups.Clear(); foreach (var group in buses ?? Array.Empty<AudioMixerGroup>()) if (group != null) groups[group.name] = group;
            mixer = audioMixer; EnsureSources(); ApplyVolumes();
        }
        private void EnsureSources()
        {
            // Separate native emitters make each voice inspectable. Pool identities stay
            // fixed: twelve short emitters, of which the budget admits at most eight.
            for (int i = 0; i < voices.Length; i++) if (voices[i] == null) voices[i] = CreateSource("Art07Voice_" + i.ToString("D2"), false);
            if (music == null) music = CreateSource("Art07Music", true);
            if (groups.TryGetValue("Music", out var group)) music.outputAudioMixerGroup = group;
        }
        private AudioSource CreateSource(string sourceName, bool loop)
        {
            var node = new GameObject(sourceName); node.transform.SetParent(transform, false);
            var source = node.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 0; source.loop = loop; return source;
        }
        public void SetVolumes(float masterVolume, float musicBus, float sfxBus, float uiBus)
        { master = Mathf.Clamp01(masterVolume); musicVolume = Mathf.Clamp01(musicBus); sfx = Mathf.Clamp01(sfxBus); ui = Mathf.Clamp01(uiBus); ApplyVolumes(); }
        public bool PlayCue(string cue, string sourceKey = null, string receiptId = null, int scoreStage = 0, int tier = 0, string semanticVariant = null)
        {
            if (cue == "SFX_UI_HOVER" && !HoverEnabled) return false;
            string asset = AudioCueMap.AssetFor(cue, variantSerial++, tier, semanticVariant);
            if (asset == null || !clips.TryGetValue(asset, out var clip)) return false;
            EnsureSources(); float pitch = cue == "SFX_SCORE_STEP" ? AudioCueMap.ScorePitch(scoreStage) : 1f;
            double now = Time.realtimeSinceStartupAsDouble;
            int slot = budget.Admit(cue, sourceKey, receiptId, now, clip.length / pitch); if (slot < 0) return false;
            var def = AudioCueMap.Find(cue); var source = voices[slot]; source.Stop(); source.clip = clip; source.pitch = pitch;
            string bus = cue == "SFX_BREAK" && (semanticVariant == "card" || semanticVariant == "ControllerDestroyed") ? "Score" : def.Bus;
            source.outputAudioMixerGroup = groups.TryGetValue(bus, out var group) ? group : groups.TryGetValue(bus == "UI" ? "UI" : "SFX", out var parent) ? parent : null;
            voiceBuses[slot] = bus; voiceGains[slot] = 1f / Mathf.Sqrt(Math.Max(1, budget.ActiveCount(now)));
            source.volume = VoiceVolume(slot); source.Play();
            if (cue == "SFX_SCORE_BURST") duckUntil = now + .35;
            return true;
        }
        public bool PlayMusic(string assetId = "music_table_loop")
        { if (!clips.TryGetValue(assetId, out var clip)) return false; EnsureSources(); music.clip = clip; ApplyVolumes(); music.Play(); return true; }
        public void StopReceipt(string receiptId) { foreach (int slot in budget.ReleaseReceipt(receiptId)) if (voices[slot] != null) voices[slot].Stop(); }
        private float VoiceVolume(int slot) => voiceGains[slot] * (mixer != null && voices[slot]?.outputAudioMixerGroup != null ? 1f : master * (voiceBuses[slot] == "UI" ? ui : sfx));
        private void ApplyVolumes()
        {
            if (mixer != null) { SetDb("MasterVolume", master); SetDb("MusicVolume", musicVolume); SetDb("SFXVolume", sfx); SetDb("UIVolume", ui); }
            for (int i = 0; i < voices.Length; i++) if (voices[i] != null) voices[i].volume = VoiceVolume(i);
            if (music != null) music.volume = duck * (mixer != null && music.outputAudioMixerGroup != null ? 1f : master * musicVolume);
        }
        private void SetDb(string key, float volume) => mixer.SetFloat(key, volume <= .0001f ? -80f : 20f * Mathf.Log10(volume));
        private void Update()
        {
            float target = Time.realtimeSinceStartupAsDouble < duckUntil ? .708f : 1f;
            float seconds = target < duck ? .12f : .25f;
            duck = Mathf.MoveTowards(duck, target, Time.unscaledDeltaTime * .292f / seconds);
            if (music != null) music.volume = duck * (mixer != null && music.outputAudioMixerGroup != null ? 1f : master * musicVolume);
        }
    }
}
