#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiceCraft.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [Serializable] private sealed class LoadingUiCapture07
        {
            public string screen,unity,runtimeMvid,testMvid,phase,savePath;
            public int width,height,frame,workerThreadId;
            public bool ready,hintShown;
            public double readMilliseconds,firstHintMilliseconds;
            public float fontScale;
        }
        private IEnumerator LoadingPendingScene07()
        {
            var load=SceneManager.LoadSceneAsync(ScenePath,LoadSceneMode.Single);Assert.That(load,Is.Not.Null);yield return load;
            app=UnityEngine.Object.FindAnyObjectByType<GameApp>();Assert.That(app,Is.Not.Null);
        }
        private IEnumerator LoadingWaitReady07()
        {
            double deadline=Time.realtimeSinceStartupAsDouble+30;
            while(!app.StartupReady07&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(app.StartupReady07,Is.True,"Actual startup did not finish within the original Player30s readiness limit.");
        }
        private IEnumerator LoadingScreen07(string id)
        {
            // Ready is a storage boundary, not the end of the existing finite screen
            // entrances. Let them finish naturally before recording their final layout.
            // Pending captures deliberately retain their original >200ms timing.
            if(app.StartupReady07)
            {
                double deadline=Time.realtimeSinceStartupAsDouble+5;
                while(LoadingEntrancePlaying07()&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                Assert.That(LoadingEntrancePlaying07(),Is.False,"The actual recovery entrance did not finish.");
            }
            yield return new WaitForEndOfFrame();
            var texture=ScreenCapture.CaptureScreenshotAsTexture();Assert.That(texture,Is.Not.Null);
            string output=Path.Combine(saveDirectory,"art07-loading-ui");Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output,id+".png"),texture.EncodeToPNG());UnityEngine.Object.Destroy(texture);
            File.WriteAllText(Path.Combine(output,id+".json"),JsonUtility.ToJson(new LoadingUiCapture07{
                screen=id,unity=Application.unityVersion,runtimeMvid=typeof(GameApp).Module.ModuleVersionId.ToString(),testMvid=GetType().Module.ModuleVersionId.ToString(),
                width=Screen.width,height=Screen.height,frame=Time.frameCount,phase=app.Session?.State?.phase,savePath=app.SaveLocation,
                ready=app.StartupReady07,hintShown=app.StartupLoadingHintEverShown07,readMilliseconds=app.StartupLoadElapsedMilliseconds07,
                firstHintMilliseconds=app.StartupHintFirstMilliseconds07,workerThreadId=app.StartupLoadWorkerThreadId07,fontScale=UiKit.FontScale},true));
        }
        private bool LoadingEntrancePlaying07()
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            return UnityEngine.Object.FindObjectsByType<Art07MenuReveal>().Any(x=>x.isActiveAndEnabled&&(bool)typeof(Art07MenuReveal).GetField("playing",flags).GetValue(x))
                ||UnityEngine.Object.FindObjectsByType<Art07PanelMotion>().Any(x=>x.isActiveAndEnabled&&x.IsPlaying)
                ||UnityEngine.Object.FindObjectsByType<Art07ScreenReveal>().Any(x=>x.isActiveAndEnabled&&x.IsPlaying)
                ||HasActive("ContinueVeil07");
        }
        [UnityTest] public IEnumerator PLAY_ART07_LOADING_UI_SlowReadThresholdRendersFramesAndRejectsCommandsUntilReady()
        {
            StartRun("70952");string file=app.SaveLocation,state=StateJson();byte[] original=File.ReadAllBytes(file);
            PlayerPrefs.SetInt("art07.textScale",150);
            var entered=new TaskCompletionSource<bool>();var release=new TaskCompletionSource<bool>();int reads=0;
            GameApp.StartupReadBarrierForTests07=path=>{Interlocked.Increment(ref reads);entered.TrySetResult(true);if(!release.Task.Wait(TimeSpan.FromSeconds(20)))throw new TimeoutException("Isolated startup read barrier was not released.");};
            try
            {
                yield return LoadingPendingScene07();int beganFrame=Time.frameCount;
                Assert.That(UiKit.FontScale,Is.EqualTo(1.5f));
                double deadline=Time.realtimeSinceStartupAsDouble+5;
                while(!entered.Task.IsCompleted&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                Assert.That(entered.Task.IsCompleted,Is.True);Assert.That(app.StartupReady07,Is.False);Assert.That(app.Session,Is.Null);
                int earlyFrames=0;
                while(app.StartupLoadElapsedMilliseconds07<=240&&Time.realtimeSinceStartupAsDouble<deadline)
                {
                    if(app.StartupLoadElapsedMilliseconds07<=200){earlyFrames++;Assert.That(HasActive("StartupReadHint07"),Is.False);}
                    yield return null;
                }
                yield return null;
                Assert.That(app.StartupHintFirstMilliseconds07,Is.GreaterThan(200));
                Assert.That(HasActive("StartupReadHint07"),Is.True);Assert.That(Time.frameCount-beganFrame,Is.GreaterThanOrEqualTo(2),"Main/render frames must advance while the real worker remains pending.");
                Assert.That(InformationText07("StartupReadHint07"),Is.EqualTo("正在读取存档…"));
                Assert.That(HasActive("NewRun")||HasActive("ContinueRun"),Is.False);
                var denied=app.Send(new SessionCommand{kind=CommandKind.NewRun,seed=99});Assert.That(denied.accepted,Is.False);
                app.StartNew("99");Assert.That(app.Session,Is.Null);
                app.UnlockDebug("XD is god");Assert.That(app.EnterDebugWorkspace()||app.ResumeDebugWorkspace(),Is.False);app.ReturnNormalWorkspace();
                CollectionAssert.AreEqual(original,File.ReadAllBytes(file));Assert.That(Directory.GetFiles(saveDirectory,"debug-run*"),Is.Empty);
                yield return app.StartCoroutine(LoadingScreen07("slow-pending"));
                TestContext.WriteLine("Real under200ms frame samples: "+earlyFrames+"; first visible hint at "+app.StartupHintFirstMilliseconds07+"ms.");
                release.TrySetResult(true);yield return LoadingWaitReady07();
                Assert.That(app.StartupLoadWorkerThreadId07,Is.Not.EqualTo(Thread.CurrentThread.ManagedThreadId));
                Assert.That(HasActive("StartupReadHint07"),Is.False);Assert.That(StateJson(),Is.EqualTo(state));CollectionAssert.AreEqual(original,File.ReadAllBytes(file));
                int before=Volatile.Read(ref reads);Click("ContinueRun");Assert.That(Volatile.Read(ref reads),Is.EqualTo(before),"Continue must use the preloaded state, not start a second load.");
                Assert.That(StateJson(),Is.EqualTo(state));CollectionAssert.AreEqual(original,File.ReadAllBytes(file));
                yield return app.StartCoroutine(LoadingScreen07("slow-ready-continued"));
            }
            finally{release.TrySetResult(true);GameApp.StartupReadBarrierForTests07=null;}
        }
        [UnityTest] public IEnumerator PLAY_ART07_LOADING_UI_FastActiveBackupLegacyAndWorkerErrorKeepStoragePolicy()
        {
            StartRun("70953");string file=app.SaveLocation,state=StateJson();byte[] valid=File.ReadAllBytes(file);
            PlayerPrefs.SetInt("art07.textScale",150);
            try
            {
                yield return LoadBootstrap();
                Assert.That(app.StartupLoadElapsedMilliseconds07,Is.LessThan(200),"This fixture must actually exercise the fast path; a slower host is not a fast-path pass.");
                Assert.That(app.StartupLoadingHintEverShown07,Is.False);Assert.That(StateJson(),Is.EqualTo(state));
                yield return app.StartCoroutine(LoadingScreen07("fast-ready"));
                byte[] corrupt=System.Text.Encoding.UTF8.GetBytes("{ corrupt active");File.WriteAllBytes(file,corrupt);File.WriteAllBytes(file+".backup",valid);
                yield return LoadBootstrap();Assert.That(StateJson(),Is.EqualTo(state));Assert.That(HasActive("LoadIssueDetails07"),Is.True);
                Assert.That(InformationText07("RecoveryStateBody"),Does.Contain("备份检查点"));
                CollectionAssert.AreEqual(corrupt,File.ReadAllBytes(file));CollectionAssert.AreEqual(valid,File.ReadAllBytes(file+".backup"));
                yield return app.StartCoroutine(LoadingScreen07("backup-ready-notice"));
                byte[] legacy=System.Text.Encoding.UTF8.GetBytes("{\"schema\":3,\"config\":\"p0.5.0\",\"payload\":\"{}\",\"sha256\":\"legacy-fixture\"}");
                File.WriteAllBytes(file,legacy);File.WriteAllBytes(file+".backup",legacy);
                yield return LoadBootstrap();Assert.That(app.Session.State,Is.Null);
                Assert.That(InformationText07("LegacyVersions07Body"),Does.Contain("p0.5.0").And.Contain("schema 3"));
                var archives=Directory.GetFiles(saveDirectory,"active-run.json*.legacy-schema3-*");Assert.That(archives.Length,Is.EqualTo(2));
                foreach(string archive in archives)CollectionAssert.AreEqual(legacy,File.ReadAllBytes(archive));
                CollectionAssert.AreEqual(legacy,File.ReadAllBytes(file));CollectionAssert.AreEqual(legacy,File.ReadAllBytes(file+".backup"));
                yield return app.StartCoroutine(LoadingScreen07("legacy-ready-notice"));
                GameApp.StartupReadBarrierForTests07=path=>throw new IOException("isolated-worker-read-failure");
                yield return LoadBootstrap();Assert.That(app.Session.State,Is.Null);Assert.That(HasActive("LoadIssueDetails07"),Is.True);
                Assert.That(InformationText07("RecoveryDetailBody"),Does.Contain("isolated-worker-read-failure"));
                CollectionAssert.AreEqual(legacy,File.ReadAllBytes(file));CollectionAssert.AreEqual(legacy,File.ReadAllBytes(file+".backup"));
                yield return app.StartCoroutine(LoadingScreen07("worker-error-ready-notice"));
            }
            finally{GameApp.StartupReadBarrierForTests07=null;}
        }
        [UnityTest] public IEnumerator PLAY_ART07_LOADING_UI_DestroyAndReloadSerializesSamePathWithoutAdoptingOldResult()
        {
            StartRun("70954");string file=app.SaveLocation,state=StateJson();byte[] original=File.ReadAllBytes(file);
            PlayerPrefs.SetInt("art07.textScale",150);
            var firstEntered=new TaskCompletionSource<bool>();var secondEntered=new TaskCompletionSource<bool>();
            var firstRelease=new TaskCompletionSource<bool>();var secondRelease=new TaskCompletionSource<bool>();int calls=0;
            GameApp.StartupReadBarrierForTests07=path=>
            {
                int index=Interlocked.Increment(ref calls);var signal=index==1?firstEntered:secondEntered;var release=index==1?firstRelease:secondRelease;
                signal.TrySetResult(true);if(!release.Task.Wait(TimeSpan.FromSeconds(20)))throw new TimeoutException("Isolated reload barrier timed out.");
            };
            try
            {
                yield return LoadingPendingScene07();double deadline=Time.realtimeSinceStartupAsDouble+5;
                while(!firstEntered.Task.IsCompleted&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                Assert.That(firstEntered.Task.IsCompleted,Is.True);var destroyed=app;
                yield return LoadingPendingScene07();Assert.That(destroyed==null,Is.True);Assert.That(app.StartupReady07,Is.False);
                int frame=Time.frameCount;for(int i=0;i<5;i++)yield return null;
                deadline=Time.realtimeSinceStartupAsDouble+5;
                while(app.StartupLoadElapsedMilliseconds07<=240&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                yield return null;
                Assert.That(secondEntered.Task.IsCompleted,Is.False,"A new scene must wait for the prior owner of the same save path.");
                Assert.That(calls,Is.EqualTo(1));Assert.That(Time.frameCount-frame,Is.GreaterThanOrEqualTo(5));
                Assert.That(HasActive("StartupReadHint07"),Is.True,"The new scene's real wait for the same save path also needs the delayed loading hint.");
                Assert.That(app.StartupHintFirstMilliseconds07,Is.GreaterThan(200));
                yield return app.StartCoroutine(LoadingScreen07("destroy-reload-waiting-for-path"));
                CollectionAssert.AreEqual(original,File.ReadAllBytes(file));
                firstRelease.TrySetResult(true);deadline=Time.realtimeSinceStartupAsDouble+5;
                while(!secondEntered.Task.IsCompleted&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                Assert.That(secondEntered.Task.IsCompleted,Is.True);Assert.That(app.StartupReady07,Is.False);Assert.That(app.Session,Is.Null);
                secondRelease.TrySetResult(true);yield return LoadingWaitReady07();
                Assert.That(UnityEngine.Object.FindObjectsByType<GameApp>().Length,Is.EqualTo(1));Assert.That(calls,Is.EqualTo(2));
                Assert.That(StateJson(),Is.EqualTo(state));CollectionAssert.AreEqual(original,File.ReadAllBytes(file));
                Assert.That(HasActive("LoadIssueDetails07"),Is.False);
                yield return app.StartCoroutine(LoadingScreen07("destroy-reload-ready"));
            }
            finally{firstRelease.TrySetResult(true);secondRelease.TrySetResult(true);GameApp.StartupReadBarrierForTests07=null;}
        }
    }
}
#endif
