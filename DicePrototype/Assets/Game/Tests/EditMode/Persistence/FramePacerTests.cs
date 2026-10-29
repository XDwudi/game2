using System;
using System.Collections.Generic;
using System.Reflection;
using DiceCraft;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.LowLevel;

namespace DiceCraft.Tests
{
    public sealed class FramePacerTests
    {
        private sealed class FakeClock : IFramePacerClock
        {
            public long Frequency { get; set; } = 60000;
            public long Now, SpinTicks = 1, ExtraSleepTicks;
            public long Timestamp => Now;
            public readonly List<int> Sleeps = new List<int>();
            public readonly List<long> Spins = new List<long>();
            public Action OnSleep;
            public void SleepMilliseconds(int milliseconds)
            {
                Sleeps.Add(milliseconds);
                Now += Math.Max(1, Frequency * milliseconds / 1000) + ExtraSleepTicks;
                OnSleep?.Invoke();
            }
            public void SpinOnce() { Spins.Add(Now); Now += SpinTicks; }
        }
        private sealed class Before { }
        private sealed class After { }
        private sealed class OtherOwner { }

        [Test] public void PACER_FirstReleaseIsImmediateThenUsesCoarseSleepAndBoundedTail()
        {
            var clock = new FakeClock {Now = 100}; var pacer = new DeadlineFrameScheduler(clock);
            pacer.WaitForNextFrame();
            Assert.That(pacer.LastRelease, Is.EqualTo(100)); Assert.That(pacer.NextDeadline, Is.EqualTo(1100));
            Assert.That(clock.Sleeps, Is.Empty); Assert.That(clock.Spins, Is.Empty);
            clock.Now = 600; pacer.WaitForNextFrame();
            Assert.That(pacer.LastRelease, Is.EqualTo(1100)); Assert.That(pacer.NextDeadline, Is.EqualTo(2100));
            CollectionAssert.AreEqual(new[] {6, 1}, clock.Sleeps);
            Assert.That(clock.Spins, Is.Not.Empty);
            foreach (long start in clock.Spins) Assert.That(1100 - start, Is.LessThanOrEqualTo(90), "Busy waiting must start within 1.5 ms.");
        }
        [Test] public void PACER_LateWorkReanchorsNextFullPeriodInsteadOfCatchingUp()
        {
            var clock = new FakeClock(); var pacer = new DeadlineFrameScheduler(clock);
            pacer.WaitForNextFrame(); clock.Now = 1300; pacer.WaitForNextFrame();
            Assert.That(clock.Sleeps, Is.Empty); Assert.That(pacer.LastRelease, Is.EqualTo(1300));
            Assert.That(pacer.NextDeadline, Is.EqualTo(2300), "A fixed epoch would incorrectly leave only 700 ticks to the next release.");
            pacer.WaitForNextFrame(); Assert.That(clock.Now, Is.EqualTo(2300));
            clock.Now = 100000; pacer.WaitForNextFrame(); Assert.That(pacer.NextDeadline, Is.EqualTo(101000));
        }
        [Test] public void PACER_SleepOvershootIsPreservedAndNeverFollowedByAShortInterval()
        {
            var clock = new FakeClock {ExtraSleepTicks = 300}; var pacer = new DeadlineFrameScheduler(clock);
            pacer.WaitForNextFrame(); pacer.WaitForNextFrame();
            Assert.That(pacer.LastRelease, Is.EqualTo(1200)); Assert.That(pacer.NextDeadline, Is.EqualTo(2200));
            clock.ExtraSleepTicks = 0; pacer.WaitForNextFrame(); Assert.That(pacer.LastRelease, Is.EqualTo(2200));
        }
        [Test] public void PACER_SpinOvershootAlsoReanchorsAtActualRelease()
        {
            var clock = new FakeClock {SpinTicks = 7}; var pacer = new DeadlineFrameScheduler(clock);
            pacer.WaitForNextFrame(); clock.Now = 990; pacer.WaitForNextFrame();
            Assert.That(clock.Now, Is.EqualTo(1004)); Assert.That(pacer.NextDeadline, Is.EqualTo(2004));
        }
        [Test] public void PACER_PauseAndFocusMustBothRecoverBeforeSchedulingRestarts()
        {
            var clock = new FakeClock(); var pacer = new DeadlineFrameScheduler(clock);
            pacer.WaitForNextFrame(); pacer.SetPaused(true); pacer.SetFocused(false);
            clock.Now = 100000; pacer.WaitForNextFrame(); Assert.That(pacer.NextDeadline, Is.Null);
            pacer.SetPaused(false); pacer.WaitForNextFrame(); Assert.That(pacer.IsSuspended, Is.True);
            pacer.SetFocused(true); pacer.WaitForNextFrame();
            Assert.That(clock.Sleeps, Is.Empty); Assert.That(pacer.NextDeadline, Is.EqualTo(101000));
            pacer.SetFocused(true); Assert.That(pacer.NextDeadline, Is.EqualTo(101000), "Repeated unchanged focus callbacks must not postpone a frame.");
            pacer.Reset(); Assert.That(pacer.NextDeadline, Is.Null);
        }
        [Test] public void PACER_NegativeBackwardAndOverflowingClockValuesResetWithoutWaiting()
        {
            var clock = new FakeClock {Now = -1}; var pacer = new DeadlineFrameScheduler(clock);
            pacer.WaitForNextFrame(); Assert.That(pacer.NextDeadline, Is.Null);
            clock.Now = 500; pacer.WaitForNextFrame(); Assert.That(pacer.NextDeadline, Is.EqualTo(1500));
            clock.Now = 100; pacer.WaitForNextFrame(); Assert.That(pacer.NextDeadline, Is.EqualTo(1100));
            Assert.That(clock.Sleeps, Is.Empty);
            clock.Now = long.MaxValue - 500; pacer.WaitForNextFrame(); Assert.That(pacer.NextDeadline, Is.Null);
            Assert.That(pacer.LastRelease, Is.Null);
        }
        [Test] public void PACER_BackgroundWrapperSleepsWithoutSpinningOrAnchoringThenResumesFresh()
        {
            var clock = new FakeClock(); var pacer = new DeadlineFrameScheduler(clock);
            pacer.WaitForFrameOrBackground(); pacer.SetFocused(false);
            pacer.WaitForFrameOrBackground(); pacer.WaitForFrameOrBackground();
            CollectionAssert.AreEqual(new[] {17, 17}, clock.Sleeps);
            Assert.That(clock.Spins, Is.Empty); Assert.That(pacer.NextDeadline, Is.Null);
            Assert.That(pacer.LastRelease, Is.Null);
            pacer.SetFocused(true); pacer.WaitForFrameOrBackground();
            Assert.That(pacer.LastRelease, Is.EqualTo(2040)); Assert.That(pacer.NextDeadline, Is.EqualTo(3040));
            Assert.That(clock.Sleeps.Count, Is.EqualTo(2));
            pacer.SetPaused(true); pacer.WaitForFrameOrBackground();
            CollectionAssert.AreEqual(new[] {17, 17, 17}, clock.Sleeps);
            Assert.That(pacer.NextDeadline, Is.Null); Assert.That(clock.Spins, Is.Empty);
        }
        [Test] public void PACER_ClockResetDuringWaitStopsTheOldDeadlineImmediately()
        {
            var clock = new FakeClock(); var pacer = new DeadlineFrameScheduler(clock);
            pacer.WaitForNextFrame(); clock.OnSleep = () => clock.Now = -1;
            pacer.WaitForNextFrame(); Assert.That(clock.Sleeps.Count, Is.EqualTo(1));
            Assert.That(clock.Spins, Is.Empty); Assert.That(pacer.NextDeadline, Is.Null);
        }
        [Test] public void PACER_FractionalClockPeriodRoundsUpByLessThanOneTick()
        {
            var clock = new FakeClock {Frequency = 10000000}; var pacer = new DeadlineFrameScheduler(clock);
            pacer.WaitForNextFrame(); Assert.That(pacer.NextDeadline, Is.EqualTo(166667));
        }
        [Test] public void PACER_InvalidClockAndRateAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new DeadlineFrameScheduler(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DeadlineFrameScheduler(new FakeClock {Frequency = 0}));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DeadlineFrameScheduler(new FakeClock(), 0));
        }
        [Test] public void PACER_LoopInstallationIsIdempotentAndPrecedesAnimationAndLateUpdate()
        {
            int calls = 0; var loop = Tree();
            Assert.That(FramePacerLoop.TryInstall(ref loop, () => calls++), Is.True);
            var preLate = loop.subSystemList[1].subSystemList;
            Assert.That(preLate.Length, Is.EqualTo(3));
            Assert.That(preLate[1].type, Is.EqualTo(typeof(UnityEngine.PlayerLoop.PreLateUpdate.DirectorUpdateAnimationBegin)));
            Assert.That(preLate[2].type, Is.EqualTo(typeof(UnityEngine.PlayerLoop.PreLateUpdate.ScriptRunBehaviourLateUpdate)));
            preLate[0].updateDelegate(); Assert.That(calls, Is.EqualTo(1));
            Assert.That(FramePacerLoop.TryInstall(ref loop, () => calls += 100), Is.True);
            Assert.That(loop.subSystemList[1].subSystemList, Is.SameAs(preLate));
            preLate[0].updateDelegate(); Assert.That(calls, Is.EqualTo(2));
        }
        [Test] public void PACER_UninstallPreservesNodesAddedByOtherOwnersAfterInstallation()
        {
            int otherCalls = 0; var loop = Tree(); FramePacerLoop.TryInstall(ref loop, () => { });
            var preLate = loop.subSystemList[1]; var nodes = new List<PlayerLoopSystem>(preLate.subSystemList);
            nodes.Insert(1, new PlayerLoopSystem {type = typeof(OtherOwner), updateDelegate = () => otherCalls++});
            preLate.subSystemList = nodes.ToArray(); loop.subSystemList[1] = preLate;
            Assert.That(FramePacerLoop.Remove(ref loop), Is.True); Assert.That(FramePacerLoop.Contains(loop), Is.False);
            Assert.That(loop.subSystemList[0].type, Is.EqualTo(typeof(Before))); Assert.That(loop.subSystemList[2].type, Is.EqualTo(typeof(After)));
            var kept = loop.subSystemList[1].subSystemList;
            Assert.That(kept.Length, Is.EqualTo(3)); Assert.That(kept[0].type, Is.EqualTo(typeof(OtherOwner)));
            kept[0].updateDelegate(); Assert.That(otherCalls, Is.EqualTo(1));
            Assert.That(FramePacerLoop.Remove(ref loop), Is.False);
            Assert.That(loop.subSystemList[1].subSystemList, Is.SameAs(kept));
        }
        [Test] public void PACER_MissingAnchorLeavesTheProvidedLoopUntouched()
        {
            var children = new[] {new PlayerLoopSystem {type = typeof(OtherOwner)}};
            var loop = new PlayerLoopSystem {subSystemList = children};
            Assert.That(FramePacerLoop.TryInstall(ref loop, () => { }), Is.False);
            Assert.That(loop.subSystemList, Is.SameAs(children));
        }
        [Test] public void PACER_EditorInstallIsExplicitlyInertAndKeepsNativeSettings()
        {
            int target = Application.targetFrameRate, vsync = QualitySettings.vSyncCount;
            float scale = Time.timeScale;
            Assert.That(FramePacer.IsInstalled, Is.False); Assert.That(FramePacer.Install(), Is.False);
            FramePacer.Stop(); FramePacer.Stop();
            Assert.That(FramePacer.IsInstalled, Is.False);
            Assert.That(Application.targetFrameRate, Is.EqualTo(target)); Assert.That(QualitySettings.vSyncCount, Is.EqualTo(vsync));
            Assert.That(Time.timeScale, Is.EqualTo(scale));
        }
        [TestCase(0, 60)]
        [TestCase(2, 37)]
        public void PACER_NativeCapOwnershipRestoresOriginalOnceAndCapturesEachNewLifetime(int vsync, int target)
        {
            // The platform-gated Install remains inert in the Editor. Exercise its actual
            // native-setting helpers without installing a callback into the Editor loop.
            int savedTarget = Application.targetFrameRate, savedVSync = QualitySettings.vSyncCount;
            try
            {
                Assert.That(FramePacer.IsInstalled, Is.False);
                QualitySettings.vSyncCount = vsync; Application.targetFrameRate = target;
                NativeCapMethod("TakeNativeCap");
                Assert.That(QualitySettings.vSyncCount, Is.Zero); Assert.That(Application.targetFrameRate, Is.EqualTo(-1));
                QualitySettings.vSyncCount = 1; Application.targetFrameRate = 120;
                NativeCapMethod("TakeNativeCap"); // Reinstall must retain the first pre-install snapshot.
                Assert.That(QualitySettings.vSyncCount, Is.Zero); Assert.That(Application.targetFrameRate, Is.EqualTo(-1));
                NativeCapMethod("RestoreNativeCap");
                Assert.That(QualitySettings.vSyncCount, Is.EqualTo(vsync)); Assert.That(Application.targetFrameRate, Is.EqualTo(target));
                QualitySettings.vSyncCount = 2; Application.targetFrameRate = 45;
                NativeCapMethod("RestoreNativeCap"); // Repeated Stop must not overwrite settings changed later.
                Assert.That(QualitySettings.vSyncCount, Is.EqualTo(2)); Assert.That(Application.targetFrameRate, Is.EqualTo(45));
                NativeCapMethod("TakeNativeCap"); NativeCapMethod("RestoreNativeCap");
                Assert.That(QualitySettings.vSyncCount, Is.EqualTo(2)); Assert.That(Application.targetFrameRate, Is.EqualTo(45));
                Assert.That(FramePacer.IsInstalled, Is.False);
            }
            finally
            {
                NativeCapMethod("RestoreNativeCap");
                QualitySettings.vSyncCount = savedVSync; Application.targetFrameRate = savedTarget;
            }
        }
        private static void NativeCapMethod(string name)
        {
            var method = typeof(FramePacer).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "Native cap ownership must be exercised through the production helper.");
            method.Invoke(null, null);
        }
        private static PlayerLoopSystem Tree() => new PlayerLoopSystem {subSystemList = new[]
        {
            new PlayerLoopSystem {type = typeof(Before)},
            new PlayerLoopSystem {type = typeof(UnityEngine.PlayerLoop.PreLateUpdate), subSystemList = new[]
            {
                new PlayerLoopSystem {type = typeof(UnityEngine.PlayerLoop.PreLateUpdate.DirectorUpdateAnimationBegin)},
                new PlayerLoopSystem {type = typeof(UnityEngine.PlayerLoop.PreLateUpdate.ScriptRunBehaviourLateUpdate)}
            }},
            new PlayerLoopSystem {type = typeof(After)}
        }};
    }
}
