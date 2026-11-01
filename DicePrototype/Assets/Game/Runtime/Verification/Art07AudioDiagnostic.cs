using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Presentation;
using UnityEngine;

namespace DiceCraft.Verification
{
    /// <summary>Explicit, disposable native-audio probe. Does not access GameSession or user saves.</summary>
    public sealed class Art07AudioDiagnostic : MonoBehaviour
    {
        [Serializable] private sealed class SourceState
        {
            public string name, entity, clip, clipLoadState;
            public bool playing, loop, enabled, active, playOnAwake, virtualized;
            public float volume, pitch, time;
            public int timeSamples;
        }
        [Serializable] private sealed class Sample
        {
            public string phase;
            public int frame, admitted;
            public double realtime, dspTime;
            public SourceState[] sources;
        }
        [Serializable] private sealed class Report
        {
            public string schema = "art07-native-audio-diagnostic-1", status = "running", error, unity, platform, gpu, output, startedUtc;
            public string[] arguments;
            public bool batch, listenerPaused;
            public float listenerVolume;
            public int sampleRate, dspBufferSize, realVoices, virtualVoices, listeners;
            public List<Sample> samples = new List<Sample>();
        }
        private Report report;
        private AudioClip clip;
        private AudioClip[] reproductionClips;
        private GameObject probe;
        private double deadline;
        public static Art07AudioDiagnostic Begin(string absoluteOutputJson)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run the native diagnostic in Play Mode or a Player.");
            if (!Path.IsPathRooted(absoluteOutputJson)) throw new ArgumentException("A complete absolute output path is required.");
            if (File.Exists(absoluteOutputJson)) throw new IOException("Do not overwrite earlier diagnostic evidence.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(absoluteOutputJson)));
            var host = new GameObject("Art07AudioDiagnostic");
            var driver = host.AddComponent<Art07AudioDiagnostic>();
            var config = AudioSettings.GetConfiguration();
            driver.report = new Report { output = Path.GetFullPath(absoluteOutputJson), unity = Application.unityVersion, platform = Application.platform.ToString(),
                gpu = SystemInfo.graphicsDeviceType.ToString(), startedUtc = DateTime.UtcNow.ToString("O"), arguments = Environment.GetCommandLineArgs().Where(a => new[] { "-batchmode", "-nographics", "-noaudio", "-force-d3d11", "-force-d3d12", "-force-vulkan", "-force-glcore" }.Contains(a, StringComparer.OrdinalIgnoreCase)).ToArray(), batch = Application.isBatchMode,
                listenerPaused = AudioListener.pause, listenerVolume = AudioListener.volume, sampleRate = config.sampleRate, dspBufferSize = config.dspBufferSize,
                realVoices = config.numRealVoices, virtualVoices = config.numVirtualVoices, listeners = FindObjectsByType<AudioListener>().Length };
            driver.deadline = Time.realtimeSinceStartupAsDouble + 15;
            driver.Write(); driver.StartCoroutine(driver.Guarded(driver.Execute())); return driver;
        }
        private IEnumerator Guarded(IEnumerator body)
        {
            while (true)
            {
                object current;
                try { if (!body.MoveNext()) break; current = body.Current; }
                catch (Exception ex) { Finish("failed", ex.ToString()); yield break; }
                yield return current;
            }
            Finish("completed_observations_only", null);
        }
        private IEnumerator Execute()
        {
            probe = new GameObject("Art07NativeProbeSources"); probe.transform.SetParent(transform, false);
            var plain = probe.AddComponent<AudioSource>();
            Capture("plain_default_no_clip", new[] { plain });
            plain.playOnAwake = false;
            Capture("plain_playOnAwake_false_no_clip", new[] { plain });
            yield return null; Capture("plain_next_frame_no_clip", new[] { plain });
            plain.Stop(); Capture("plain_after_stop_no_clip", new[] { plain });
            plain.enabled = false; Capture("plain_disabled_no_clip", new[] { plain });
            yield return null; Capture("plain_disabled_next_frame_no_clip", new[] { plain });
            plain.enabled = true;
            clip = AudioClip.Create("sfx_card_trigger", 48000, 1, 48000, false);
            float[] signal = new float[48000];
            for (int i = 0; i < signal.Length; i++) signal[i] = (float)Math.Sin(i * Math.PI * 880 / 48000) * .01f;
            clip.SetData(signal, 0); plain.clip = clip; plain.volume = 0; plain.Play();
            Capture("plain_after_play_clip", new[] { plain });
            yield return null; Capture("plain_play_next_frame", new[] { plain });
            plain.Stop(); Capture("plain_after_stop_clip", new[] { plain });
            yield return null; Capture("plain_stopped_next_frame", new[] { plain });
            var poolHost = new GameObject("Art07PoolProbe"); poolHost.transform.SetParent(transform, false);
            var player = poolHost.AddComponent<PresentationAudioPlayer>(); player.Configure(new[] { clip }); player.SetVolumes(0, 0, 0, 0);
            var sources = poolHost.GetComponentsInChildren<AudioSource>(true);
            Capture("pool_prewarmed_no_play", sources);
            yield return null; Capture("pool_prewarmed_next_frame", sources);
            int admitted = 0;
            for (int i = 0; i < 32; i++) if (player.PlayCue("SFX_CARD_TRIGGER", "source-" + i, "diagnostic")) admitted++;
            Capture("pool_32_calls_same_frame", sources, admitted);
            yield return null; Capture("pool_32_calls_next_frame", sources, admitted);
            player.StopReceipt("diagnostic"); Capture("pool_receipt_stopped", sources);
            yield return null; Capture("pool_receipt_stopped_next_frame", sources);

            // Reproduce the failing NUnit sequence without adding a yield after Music.Play.
            // Keep per-iteration native snapshots in memory so diagnostic disk IO cannot
            // turn one immediate trigger burst into a delayed sequence.
            var reproductionHost = new GameObject("Art07AudioPoolTest_ExactReproduction"); reproductionHost.transform.SetParent(transform, false);
            reproductionClips = AudioCueMap.Cues.SelectMany(c => c.AssetIds).Concat(new[] { "music_table_loop" })
                .Select(id => AudioClip.Create(id, 48000, 1, 48000, false)).ToArray();
            yield return null;
            var reproduction = reproductionHost.AddComponent<PresentationAudioPlayer>(); reproduction.Configure(reproductionClips); reproduction.SetVolumes(0, 0, 0, 0);
            sources = reproductionHost.GetComponentsInChildren<AudioSource>(true);
            Capture("exact_pool_before_music", sources);
            bool musicAccepted = reproduction.PlayMusic(); Capture("exact_pool_after_music", sources, musicAccepted ? 1 : 0, false);
            for (int iteration = 0; iteration < 200; iteration++)
            {
                string receipt = "audio-receipt-" + iteration; admitted = 0;
                for (int i = 0; i < 32; i++) if (reproduction.PlayCue("SFX_CARD_TRIGGER", receipt + "/" + i, receipt)) admitted++;
                sources = reproductionHost.GetComponentsInChildren<AudioSource>(true);
                Capture("exact_iteration_" + iteration + "_triggered", sources, admitted, false);
                if (sources.Count(s => !s.loop && s.isPlaying) > 8 || sources.Any(s => s.clip == null && s.isPlaying))
                    throw new InvalidOperationException("Reproduced native short-voice limit failure in exact test iteration " + iteration + ". All source observations retained.");
                reproduction.StopReceipt(receipt); Capture("exact_iteration_" + iteration + "_stopped", sources, 0, false);
                if (iteration % 20 == 0)
                {
                    reproduction.Configure(reproductionClips); Capture("exact_iteration_" + iteration + "_reconfigured", sources, 0, false);
                    yield return null; Capture("exact_iteration_" + iteration + "_next_frame", sources, 0, false);
                }
            }
            Write();
        }
        private void Capture(string phase, IEnumerable<AudioSource> sources, int admitted = 0, bool flush = true)
        {
            report.samples.Add(new Sample { phase = phase, frame = Time.frameCount, admitted = admitted, realtime = Time.realtimeSinceStartupAsDouble, dspTime = AudioSettings.dspTime,
                sources = sources.Select(s => new SourceState { name = s.name, entity = s.GetEntityId().ToString(), clip = s.clip == null ? null : s.clip.name,
                    clipLoadState = s.clip == null ? null : s.clip.loadState.ToString(), playing = s.isPlaying, loop = s.loop, enabled = s.enabled, active = s.gameObject.activeInHierarchy,
                    playOnAwake = s.playOnAwake, virtualized = s.isVirtual, volume = s.volume, pitch = s.pitch, time = s.time, timeSamples = s.timeSamples }).ToArray() });
            if (flush) Write();
        }
        private void Update() { if (report != null && report.status == "running" && Time.realtimeSinceStartupAsDouble > deadline) Finish("failed", "Diagnostic frame progression exceeded 15 seconds."); }
        private void Write() => File.WriteAllText(report.output, JsonUtility.ToJson(report, true));
        private void Finish(string status, string error)
        {
            if (report == null || report.status != "running") return;
            report.status = status; report.error = error; StopAllCoroutines();
            try { Write(); Debug.Log("ART07_AUDIO_DIAGNOSTIC " + status + " " + report.output); }
            finally { ReleaseClips(); Destroy(gameObject); }
        }
        private void OnDestroy()
        {
            if (report == null || report.status != "running") return;
            report.status = "failed"; report.error = "Diagnostic host destroyed before completion."; Write();
            ReleaseClips();
        }
        private void ReleaseClips()
        {
            if (clip != null) Destroy(clip);
            foreach (var asset in reproductionClips ?? Array.Empty<AudioClip>()) if (asset != null) Destroy(asset);
        }
    }
}
