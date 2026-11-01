using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiceCraft.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [Serializable] private sealed class LoadingEnvelope07 { public int schema;public string config,sha256,payload; }
        [Serializable] private sealed class LoadingThreadCase07
        {
            public string name,path,canonicalState,notice,error;
            public int workerThreadId,incompatibleSchema;
            public string incompatibleVersion;
            public bool legacyPreserved;
            public double elapsedMilliseconds;
            public string[] legacyFiles;
        }
        [Serializable] private sealed class LoadingThreadReport07
        {
            public string unity,runtimeMvid,testMvid,configVersion,expectedState;
            public int mainThreadId,renderFramesWhileWaiting;
            public bool allOriginalBytesPreserved;
            public LoadingThreadCase07[] cases;
        }

        // Preparatory engine experiment only. Does not change GameApp startup or storage policy.
        [UnityTest] public IEnumerator PLAY_ART07_LOADING_SPIKE_RealWorkerReadsDtoAndAtomicActiveBackupLegacy()
        {
            StartRun("70951");
            string folder=Path.Combine(saveDirectory,"art07-loading-thread-spike");Directory.CreateDirectory(folder);
            string configJson=JsonUtility.ToJson(app.Config);
            byte[] ordinarySave=File.ReadAllBytes(app.SaveLocation);
            string ordinaryState=StateJson();long sequence=app.Session.ActionSequence;
            string reference=Path.Combine(folder,"main-reference.json");File.WriteAllBytes(reference,ordinarySave);
            string expected=JsonUtility.ToJson(new AtomicSaveStore(reference,app.Config,SaveSlotPolicy.Normal).Load());
            Assert.That(expected,Is.Not.EqualTo("null"));
            string active=Path.Combine(folder,"active.json");File.WriteAllBytes(active,ordinarySave);
            string backup=Path.Combine(folder,"backup.json");File.WriteAllText(backup,"{ corrupt active");File.WriteAllBytes(backup+".backup",ordinarySave);
            string legacy=Path.Combine(folder,"legacy.json");
            var envelope=JsonUtility.FromJson<LoadingEnvelope07>(System.Text.Encoding.UTF8.GetString(ordinarySave));
            envelope.schema=3;envelope.config="p0.5.0";File.WriteAllText(legacy,JsonUtility.ToJson(envelope));
            File.WriteAllText(legacy+".backup",JsonUtility.ToJson(envelope));
            string[] originals={reference,active,backup,backup+".backup",legacy,legacy+".backup"};
            byte[][] originalBytes=originals.Select(File.ReadAllBytes).ToArray();
            var report=new LoadingThreadReport07{unity=Application.unityVersion,runtimeMvid=typeof(GameApp).Module.ModuleVersionId.ToString(),testMvid=GetType().Module.ModuleVersionId.ToString(),configVersion=app.Config.configVersion,mainThreadId=Thread.CurrentThread.ManagedThreadId,expectedState=expected,cases=new LoadingThreadCase07[3]};
            string reportPath=Path.Combine(folder,"thread-spike-report.json");File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));
            string[] paths={active,backup,legacy},names={"valid-active","invalid-active-valid-backup","legacy-active-and-backup"};
            for(int i=0;i<paths.Length;i++)
            {
                // All UnityEngine.Object/TextAsset/PlayerPrefs/scene access ends above this boundary.
                // Only detached strings, plain DTOs and a worker-owned store enter the Task.
                string path=paths[i],name=names[i],immutableConfig=configJson;
                var task=Task.Run(()=>LoadingThreadRead07(path,name,immutableConfig));
                double deadline=Time.realtimeSinceStartupAsDouble+20;
                while(!task.IsCompleted&&Time.realtimeSinceStartupAsDouble<deadline){report.renderFramesWhileWaiting++;yield return null;}
                File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));
                Assert.That(task.IsCompleted,Is.True,"Isolated Atomic Load worker exceeded20s; no synthetic completion used. Evidence directory is retained.");
                var result=task.GetAwaiter().GetResult();report.cases[i]=result;
                File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));
                Assert.That(result.error,Is.Null.Or.Empty,result.error);
                Assert.That(result.workerThreadId,Is.Not.EqualTo(report.mainThreadId),"Must execute the actual serialization and Atomic Load on a background worker.");
                if(i<2)
                {
                    Assert.That(result.canonicalState,Is.EqualTo(expected),"Worker DTO must exactly match the main-thread production Load result.");
                    Assert.That(result.incompatibleSchema,Is.Zero);Assert.That(result.legacyFiles,Is.Empty);
                    if(i==0)Assert.That(result.notice,Is.Null.Or.Empty);
                    else Assert.That(result.notice,Is.EqualTo("活动存档不可读，已恢复上一份有效备份。"));
                }
                else
                {
                    Assert.That(result.canonicalState,Is.Null);Assert.That(result.incompatibleSchema,Is.EqualTo(3));
                    Assert.That(result.incompatibleVersion,Is.EqualTo("p0.5.0"));Assert.That(result.legacyPreserved,Is.True);
                    Assert.That(result.legacyFiles.Length,Is.EqualTo(2),"Existing Atomic policy preserves both legacy active and legacy backup.");
                    foreach(var archived in result.legacyFiles)CollectionAssert.AreEqual(originalBytes[4],File.ReadAllBytes(archived));
                }
                Assert.That(Thread.CurrentThread.ManagedThreadId,Is.EqualTo(report.mainThreadId),"Assertions and Unity state stay on the main thread.");
                Assert.That(StateJson(),Is.EqualTo(ordinaryState));Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));
                CollectionAssert.AreEqual(ordinarySave,File.ReadAllBytes(app.SaveLocation));
            }
            for(int i=0;i<originals.Length;i++)CollectionAssert.AreEqual(originalBytes[i],File.ReadAllBytes(originals[i]),originals[i]);
            report.allOriginalBytesPreserved=true;
            File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));
        }
        private static LoadingThreadCase07 LoadingThreadRead07(string path,string name,string configJson)
        {
            var result=new LoadingThreadCase07{name=name,path=path,workerThreadId=Thread.CurrentThread.ManagedThreadId};
            var watch=System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var config=JsonUtility.FromJson<GameConfig>(configJson);config.Validate();
                var store=new AtomicSaveStore(path,config,SaveSlotPolicy.Normal);
                var state=store.Load();result.canonicalState=state==null?null:JsonUtility.ToJson(state);
                result.notice=store.LastLoadNotice;var incompatible=store.LastIncompatibleSave;
                if(incompatible!=null){result.incompatibleSchema=incompatible.Schema;result.incompatibleVersion=incompatible.ConfigVersion;result.legacyPreserved=incompatible.IsLegacyPreserved;}
                result.legacyFiles=Directory.GetFiles(Path.GetDirectoryName(path),Path.GetFileName(path)+"*.legacy-schema*").OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            }
            catch(Exception error){result.error=error.ToString();}
            finally{watch.Stop();result.elapsedMilliseconds=watch.Elapsed.TotalMilliseconds;}
            return result;
        }
    }
}
