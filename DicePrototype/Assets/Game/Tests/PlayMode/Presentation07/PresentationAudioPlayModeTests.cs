using System;
using System.Collections;
using System.Linq;
using DiceCraft.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UObj = UnityEngine.Object;

namespace DiceCraft.Tests
{
    public sealed class PresentationAudioPlayModeTests
    {
        private GameObject host;
        private AudioClip[] clips;
        private AudioListener[] previouslyActiveListeners;
        [UnitySetUp] public IEnumerator Setup()
        {
            // A native audio integration fixture must contain a listener, just as the
            // production camera does. The Test Runner's empty scene has none; in this
            // Unity version uninitialized no-listener sources report playing even
            // before a clip is assigned, which is not a valid playback measurement.
            previouslyActiveListeners = UObj.FindObjectsByType<AudioListener>().Where(l => l.isActiveAndEnabled).ToArray();
            foreach (var listener in previouslyActiveListeners) listener.enabled = false;
            host = new GameObject("Art07AudioPoolTest");
            host.AddComponent<AudioListener>();
            clips = AudioCueMap.Cues.SelectMany(c => c.AssetIds).Concat(new[] { "music_table_loop" })
                .Select(id => AudioClip.Create(id, 48000, 1, 48000, false)).ToArray();
            yield return null;
        }
        [UnityTearDown] public IEnumerator Teardown()
        {
            if (host != null) UObj.Destroy(host);
            foreach (var clip in clips ?? Array.Empty<AudioClip>()) if (clip != null) UObj.Destroy(clip);
            yield return null;
            foreach (var listener in previouslyActiveListeners ?? Array.Empty<AudioListener>()) if (listener != null) listener.enabled = true;
        }
        [UnityTest] public IEnumerator PLAY_ART07_AudioPrewarmsTwelveShortVoicesOneMusicAndNoGrowthAcross200Receipts()
        {
            var player = host.AddComponent<PresentationAudioPlayer>(); player.Configure(clips); player.SetVolumes(0, 0, 0, 0);
            Assert.That(UObj.FindObjectsByType<AudioListener>().Count(l=>l.isActiveAndEnabled), Is.EqualTo(1), "Native audio must have exactly one active listener.");
            var initial = host.GetComponentsInChildren<AudioSource>(true); Assert.That(initial.Length, Is.EqualTo(13)); Assert.That(initial.Count(s => s.loop), Is.EqualTo(1)); Assert.That(initial.Count(s => !s.loop), Is.EqualTo(12));
            int initialPlaying = initial.Count(s => s.isPlaying);
            Assert.That(initial.All(s=>s.gameObject.GetComponents<AudioSource>().Length==1),Is.True,"Each prewarmed voice owns an independent native emitter.");
            Assert.That(player.PlayMusic(), Is.True); string[] ids = initial.Select(x => x.GetEntityId().ToString()).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            int materialCount = Resources.FindObjectsOfTypeAll<Material>().Length;
            for (int iteration = 0; iteration < 200; iteration++)
            {
                string receipt = "audio-receipt-" + iteration;
                for (int i = 0; i < 32; i++) player.PlayCue("SFX_CARD_TRIGGER", receipt + "/" + i, receipt);
                var current = host.GetComponentsInChildren<AudioSource>(true);
                string diagnostic = "Iteration " + iteration + " frame " + Time.frameCount + " DSP " + AudioSettings.dspTime + " activeListeners=" + UObj.FindObjectsByType<AudioListener>().Count(l=>l.isActiveAndEnabled) + " prewarmPlaying=" + initialPlaying + "\n" + string.Join("\n",current.Select(s=>s.name+" id="+s.GetEntityId()+" loop="+s.loop+" playing="+s.isPlaying+" clip="+(s.clip==null?"<none>":s.clip.name)+" samples="+s.timeSamples));
                Assert.That(current.Count(s => !s.loop && s.isPlaying), Is.LessThanOrEqualTo(8), diagnostic);
                Assert.That(host.GetComponentsInChildren<AudioSource>(true).Where(s=>s.clip==null).All(s=>!s.isPlaying),Is.True,"Unassigned spare voices must never start playing.");
                player.StopReceipt(receipt);
                if (iteration % 20 == 0) { player.Configure(clips); yield return null; }
            }
            yield return null;
            CollectionAssert.AreEqual(ids, host.GetComponentsInChildren<AudioSource>(true).Select(x => x.GetEntityId().ToString()).OrderBy(x => x, StringComparer.Ordinal).ToArray(), "Reconfiguration and replay must reuse prewarmed sources.");
            Assert.That(Resources.FindObjectsOfTypeAll<Material>().Length, Is.EqualTo(materialCount), "Audio must not create Materials.");
        }
        [UnityTest] public IEnumerator PLAY_ART07_AudioStopsOnlyOwningReceiptAndMuteAppliesImmediately()
        {
            var player = host.AddComponent<PresentationAudioPlayer>(); player.Configure(clips); player.SetVolumes(.5f, .5f, .5f, .5f);
            Assert.That(player.PlayCue("SFX_CARD_TRIGGER", "a", "old"), Is.True); Assert.That(player.PlayCue("SFX_COIN", "b", "next"), Is.True);
            yield return null;
            var next = host.GetComponentsInChildren<AudioSource>(true).Single(s => s.clip != null && s.clip.name == "sfx_coin");
            player.StopReceipt("old"); Assert.That(next.clip.name, Is.EqualTo("sfx_coin")); Assert.That(next.isPlaying, Is.True);
            player.SetVolumes(0, .5f, .5f, .5f); Assert.That(host.GetComponentsInChildren<AudioSource>(true).All(s => s.volume == 0), Is.True);
            Assert.That(player.PlayCue("MISSING_CUE"), Is.False); player.HoverEnabled = false; Assert.That(player.PlayCue("SFX_UI_HOVER"), Is.False);
        }
    }
}
