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

        // v1.1 rewrite (docs/specs/v11/RULES_DELTA.md): no old-version metadata is reported any more and nothing is
        // preserved; both old files are deleted and later saves of this version work as usual.
        [Test] public void SAVE_V11_OldVersionFilesAreDeletedAndNeitherMetadataNorNoticeIsReported()
        {
            var store=new AtomicSaveStore(path,config);store.Save(state);store.Save(state);
            Version(path,2,"p0.4.0");Version(path+".backup",3,"p0.5.0");
            Assert.That(store.Load(),Is.Null);Assert.That(store.LastIncompatibleSave,Is.Null);Assert.That(store.LastLoadNotice,Is.Null);
            Assert.That(File.Exists(path),Is.False);Assert.That(File.Exists(path+".backup"),Is.False);Assert.That(store.LastDeletedSaveFiles.Length,Is.EqualTo(2));
            Assert.That(Directory.GetFiles(folder,"*.legacy-schema*-*"),Is.Empty);
            store.Load();Assert.That(store.LastIncompatibleSave,Is.Null);Assert.That(store.LastDeletedSaveFiles,Is.Empty);
            store.Save(state);store.Save(state);Assert.That(store.Load(),Is.Not.Null);Assert.That(store.LastIncompatibleSave,Is.Null);Assert.That(store.LastLoadNotice,Is.Null);
        }
        [Test] public void SAVE_ART07_SuccessfulBackupNeverCarriesRejectedActiveVersionIntoUi()
        {
            var store=new AtomicSaveStore(path,config);store.Save(state);store.Save(state);var backup=File.ReadAllBytes(path+".backup");Version(path,3,"p0.5.0");
            Assert.That(store.Load(),Is.Not.Null);Assert.That(store.LastLoadNotice,Does.Contain("有效备份"));Assert.That(store.LastIncompatibleSave,Is.Null);
            CollectionAssert.AreEqual(backup,File.ReadAllBytes(path+".backup"));
        }
        // v1.1 rewrite (RULES_DELTA): the valid active file still loads, but the old-version backup next to it is deleted
        // instead of being left alone; a save over an old-version active file sets no metadata and quarantines nothing.
        [Test] public void SAVE_ART07_ValidActiveLoadsAnOldBackupIsDeletedAndSaveProbeDoesNotSetLoadMetadata()
        {
            var store=new AtomicSaveStore(path,config);store.Save(state);store.Save(state);Version(path+".backup",2,"p0.4.0");var active=File.ReadAllBytes(path);
            Assert.That(store.Load(),Is.Not.Null);Assert.That(store.LastIncompatibleSave,Is.Null);Assert.That(store.LastLoadNotice,Is.Null);
            Assert.That(File.Exists(path+".backup"),Is.False,"the old-version backup is deleted");CollectionAssert.AreEqual(active,File.ReadAllBytes(path),"the valid active file is untouched");
            Version(path,3,"p0.5.0");store.Save(state);Assert.That(store.LastIncompatibleSave,Is.Null,"Write validation must not masquerade as a failed Load.");
            Assert.That(Directory.GetFiles(folder,"*.rejected-*"),Is.Empty);Assert.That(File.Exists(path+".backup"),Is.False,"an old-version file never becomes the backup");
            Assert.That(store.Load(),Is.Not.Null);
        }
    }
}
