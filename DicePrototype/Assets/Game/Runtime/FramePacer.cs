using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using UnityEngine.LowLevel;

namespace DiceCraft
{
    public interface IFramePacerClock
    {
        long Frequency { get; }
        long Timestamp { get; }
        void SleepMilliseconds(int milliseconds);
        void SpinOnce();
    }

    /// <summary>
    /// Maximum cadence, not a fixed-phase clock: every actual release anchors
    /// the next full period. Late releases remain late; there are no shortened catch-up frames.
    /// No Unity time, game state, file I/O, allocation or measurement-probe input is used here.
    /// </summary>
    public sealed class DeadlineFrameScheduler
    {
        private readonly IFramePacerClock clock;
        private readonly long frequency, periodTicks, spinTailTicks;
        private long observed = -1;
        private bool paused, focused = true;
        public long? NextDeadline { get; private set; }
        public long? LastRelease { get; private set; }
        public bool IsSuspended => paused || !focused;

        public DeadlineFrameScheduler(IFramePacerClock clock, int framesPerSecond = 60)
        {
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            frequency = clock.Frequency;
            if (frequency <= 0) throw new ArgumentOutOfRangeException(nameof(clock));
            if (framesPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
            // Ceiling avoids a fraction-of-a-clock-tick catch-up interval. At 10 MHz,
            // the rounding is below 0.1 microsecond per frame.
            periodTicks = frequency / framesPerSecond + (frequency % framesPerSecond == 0 ? 0 : 1);
            spinTailTicks = Math.Min(periodTicks, (long)(frequency * .0015m));
        }
        public void SetPaused(bool value) { if (paused != value) { paused = value; Reset(); } }
        public void SetFocused(bool value) { if (focused != value) { focused = value; Reset(); } }
        public void Reset() { NextDeadline = null; LastRelease = null; observed = -1; }

        /// <summary>Sleep-only background cap: a player running unfocused must not become uncapped.
        /// Keep the deadline reset; resume starts a fresh foreground period without catching up.</summary>
        public void WaitForFrameOrBackground()
        {
            if (IsSuspended) { clock.SleepMilliseconds(17); return; }
            WaitForNextFrame();
        }

        public void WaitForNextFrame()
        {
            if (IsSuspended || !Read(out long now)) return;
            if (NextDeadline.HasValue)
            {
                long deadline = NextDeadline.Value;
                while (now < deadline)
                {
                    long remaining = deadline - now;
                    if (remaining > spinTailTicks)
                        clock.SleepMilliseconds(Math.Max(1, (int)((remaining - spinTailTicks) * 1000m / frequency)));
                    else clock.SpinOnce(); // Enter busy waiting only within the 1.5 ms tail.
                    if (!Read(out now)) return;
                    if (!NextDeadline.HasValue) break; // Clock moved backwards: discard the old deadline.
                }
            }
            if (now > long.MaxValue - periodTicks) { Reset(); return; }
            LastRelease = now;
            NextDeadline = now + periodTicks;
        }
        private bool Read(out long now)
        {
            now = clock.Timestamp;
            if (now < 0) { Reset(); return false; }
            if (observed >= 0 && now < observed) Reset();
            observed = now;
            return true;
        }
    }

    /// <summary>Pure PlayerLoop tree edits, testable without changing the Editor's loop.</summary>
    public static class FramePacerLoop
    {
        private struct ReleaseFrame { }
        public static bool Contains(PlayerLoopSystem loop)
        {
            if (loop.type == typeof(ReleaseFrame)) return true;
            if (loop.subSystemList != null) foreach (var child in loop.subSystemList) if (Contains(child)) return true;
            return false;
        }
        public static bool TryInstall(ref PlayerLoopSystem loop, PlayerLoopSystem.UpdateFunction callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (Contains(loop)) return true;
            return Insert(ref loop, callback);
        }
        private static bool Insert(ref PlayerLoopSystem loop, PlayerLoopSystem.UpdateFunction callback)
        {
            if (loop.type == typeof(UnityEngine.PlayerLoop.PreLateUpdate))
            {
                var existing = loop.subSystemList ?? Array.Empty<PlayerLoopSystem>();
                var children = new PlayerLoopSystem[existing.Length + 1];
                children[0] = new PlayerLoopSystem {type = typeof(ReleaseFrame), updateDelegate = callback};
                Array.Copy(existing, 0, children, 1, existing.Length); loop.subSystemList = children; return true;
            }
            if (loop.subSystemList == null) return false;
            for (int i = 0; i < loop.subSystemList.Length; i++)
            {
                var child = loop.subSystemList[i];
                if (!Insert(ref child, callback)) continue;
                var children = (PlayerLoopSystem[])loop.subSystemList.Clone();
                children[i] = child; loop.subSystemList = children; return true;
            }
            return false;
        }
        public static bool Remove(ref PlayerLoopSystem loop)
        {
            if (loop.subSystemList == null) return false;
            var kept = new List<PlayerLoopSystem>(loop.subSystemList.Length); bool changed = false;
            foreach (var original in loop.subSystemList)
            {
                if (original.type == typeof(ReleaseFrame)) { changed = true; continue; }
                var child = original; changed |= Remove(ref child); kept.Add(child);
            }
            if (changed) loop.subSystemList = kept.ToArray();
            return changed;
        }
    }

    /// <summary>
    /// Windows desktop 60 Hz frame scheduling. Explicit main-thread Install/Stop only;
    /// never auto-enabled. Formal performance acceptance is separate from installation.
    /// Inserts at the start of PreLateUpdate, after Update/ordinary delayed coroutines and before
    /// native animation and LateUpdate camera work. EndOfFrame coroutines and presentation are
    /// later: passing this release interval does not establish FrameStart/Present/Complete pacing.
    /// Successful installation owns the native cap until Stop, then restores its prior settings.
    /// Does not change timeScale or deltaTime. Sleep can overshoot; spinning costs up to
    /// ~1.5 ms CPU/frame.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class FramePacer : MonoBehaviour
    {
        public const string StrategyDescription = "60Hz maximum cadence; absolute QPC deadline reanchored to every actual release; ceil(QPC frequency/60); coarse Sleep + <=1.5ms spin tail; first PreLateUpdate child; paused/unfocused Sleep(17ms), no spin or catch-up";
        private sealed class MonotonicClock : IFramePacerClock
        {
            public long Frequency => Stopwatch.Frequency;
            public long Timestamp => Stopwatch.GetTimestamp();
            public void SleepMilliseconds(int milliseconds) => Thread.Sleep(milliseconds);
            public void SpinOnce() => Thread.SpinWait(32);
        }
        private static DeadlineFrameScheduler scheduler;
        private static FramePacer lifetime;
        private static bool ownsNativeCap;
        private static int previousVSync, previousTarget;
        public static bool IsInstalled { get; private set; }

        public static bool Install()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            if (IsInstalled && FramePacerLoop.Contains(loop)) return true;
            if (!FramePacerLoop.TryInstall(ref loop, Tick)) return false;
            scheduler = new DeadlineFrameScheduler(new MonotonicClock());
            scheduler.SetFocused(Application.isFocused);
            PlayerLoop.SetPlayerLoop(loop);
            IsInstalled = true;
            if (lifetime == null)
            {
                var host = new GameObject("FramePacer") {hideFlags = HideFlags.HideAndDontSave};
                UnityEngine.Object.DontDestroyOnLoad(host);
                lifetime = host.AddComponent<FramePacer>();
            }
            TakeNativeCap();
            return true;
#else
            return false; // Editor and every other platform retain their native cap unchanged.
#endif
        }
        public static void Stop()
        {
            if (!IsInstalled) return;
            var old = lifetime; lifetime = null;
            try
            {
                // Read the CURRENT loop: do not restore a cached copy that could erase later users.
                var loop = PlayerLoop.GetCurrentPlayerLoop();
                if (FramePacerLoop.Remove(ref loop)) PlayerLoop.SetPlayerLoop(loop);
            }
            finally
            {
                IsInstalled = false; scheduler?.Reset(); scheduler = null;
                RestoreNativeCap();
                if (old != null) UnityEngine.Object.Destroy(old.gameObject);
            }
        }
        private static void TakeNativeCap()
        {
            // Reinstalling a lost loop node must not replace the original settings with 0/-1.
            if (!ownsNativeCap)
            {
                previousVSync = QualitySettings.vSyncCount;
                previousTarget = Application.targetFrameRate;
                ownsNativeCap = true;
            }
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
        }
        private static void RestoreNativeCap()
        {
            if (!ownsNativeCap) return;
            QualitySettings.vSyncCount = previousVSync;
            Application.targetFrameRate = previousTarget;
            ownsNativeCap = false;
        }
        private static void Tick() => scheduler?.WaitForFrameOrBackground();
        private void OnApplicationPause(bool paused) { if (lifetime == this) scheduler?.SetPaused(paused); }
        private void OnApplicationFocus(bool focused) { if (lifetime == this) scheduler?.SetFocused(focused); }
        private void OnApplicationQuit() { if (lifetime == this) Stop(); }
        private void OnDestroy() { if (lifetime == this) { lifetime = null; Stop(); } }
    }
}
