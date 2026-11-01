using System;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Tests
{
    public sealed class AtomicSaveMetadata07Tests
    {
        [Serializable] private sealed class Envelope { public int schema;public string config,sha256,payload; }
        private GameConfig config;
        private string folder,path;
        private RunState state;
        [SetUp] public void Setup()
        {
            config=JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(File.ReadAllText(Path.Combine(Application.dataPath,"Game/Data/P0Config.json"))));
            folder=Path.Combine(Application.temporaryCachePath,"DiceCraftLegacyMetadata_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);path=Path.Combine(folder,"active-run.json");
            var session=new GameSession(config,new MemorySaveStore());var result=session.Execute(new SessionCommand{kind=CommandKind.NewRun,commandId="legacy-metadata-seed",seed=71});Assert.That(result.accepted,Is.True);state=session.State;
        }
        private void Version(string file,int schema,string version)
        {var e=JsonUtility.FromJson<Envelope>(File.ReadAllText(file));e.schema=schema;e.config=version;File.WriteAllText(file,JsonUtility.ToJson(e));}
        [TearDown] public void Evidence()=>TestContext.WriteLine("Legacy metadata evidence (isolated): "+folder);

        [Test] public void SAVE_ART07_MetadataReportsFirstRealVersionAndPreservesBothLegacyFiles()
        {
            var store=new AtomicSaveStore(path,config);store.Save(state);store.Save(state);
            Version(path,2,"p0.4.0");Version(path+".backup",3,"p0.5.0");var a=File.ReadAllBytes(path);var b=File.ReadAllBytes(path+".backup");
            Assert.That(store.Load(),Is.Null);var info=store.LastIncompatibleSave;Assert.That(info,Is.Not.Null);
            Assert.That(info.Schema,Is.EqualTo(2));Assert.That(info.ConfigVersion,Is.EqualTo("p0.4.0"));Assert.That(info.IsLegacyPreserved,Is.True);
            CollectionAssert.AreEqual(a,File.ReadAllBytes(path));CollectionAssert.AreEqual(b,File.ReadAllBytes(path+".backup"));
            var archived=Directory.GetFiles(folder,"*.legacy-schema*-*");Assert.That(archived.Length,Is.EqualTo(2));
            Assert.That(archived.Any(p=>File.ReadAllBytes(p).SequenceEqual(a)),Is.True);Assert.That(archived.Any(p=>File.ReadAllBytes(p).SequenceEqual(b)),Is.True);
            store.Load();Assert.That(Directory.GetFiles(folder,"*.legacy-schema*-*").Length,Is.EqualTo(2));
            store.Save(state);store.Save(state);Assert.That(store.Load(),Is.Not.Null);Assert.That(store.LastIncompatibleSave,Is.Null);
            Assert.That(archived.Any(p=>File.ReadAllBytes(p).SequenceEqual(a)),Is.True);Assert.That(archived.Any(p=>File.ReadAllBytes(p).SequenceEqual(b)),Is.True);
        }
        [Test] public void SAVE_ART07_SuccessfulBackupNeverCarriesRejectedActiveVersionIntoUi()
        {
            var store=new AtomicSaveStore(path,config);store.Save(state);store.Save(state);var backup=File.ReadAllBytes(path+".backup");Version(path,3,"p0.5.0");
            Assert.That(store.Load(),Is.Not.Null);Assert.That(store.LastLoadNotice,Does.Contain("有效备份"));Assert.That(store.LastIncompatibleSave,Is.Null);
            CollectionAssert.AreEqual(backup,File.ReadAllBytes(path+".backup"));
        }
        [Test] public void SAVE_ART07_ValidActiveIgnoresIncompatibleBackupAndSaveProbeDoesNotSetLoadMetadata()
        {
            var store=new AtomicSaveStore(path,config);store.Save(state);store.Save(state);Version(path+".backup",2,"p0.4.0");var backup=File.ReadAllBytes(path+".backup");
            Assert.That(store.Load(),Is.Not.Null);Assert.That(store.LastIncompatibleSave,Is.Null);CollectionAssert.AreEqual(backup,File.ReadAllBytes(path+".backup"));
            Version(path,3,"p0.5.0");store.Save(state);Assert.That(store.LastIncompatibleSave,Is.Null,"Write validation must not masquerade as a failed Load.");
        }
    }
}
