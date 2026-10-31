using System;
using System.IO;
using System.Linq;
using DiceCraft;
using DiceCraft.Core;

internal static class PersistenceChecks
{
    public static void Run(GameConfig cfg,Action<string,Action> test,Action<bool,string> check)
    {
        string dir=Path.Combine("evidence/v06/review/disk-io",DateTime.UtcNow.ToString("yyyyMMddTHHmmss")+"-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var normal=new GameSession(cfg,new MemorySaveStore());
        var start=normal.Execute(new SessionCommand{kind=CommandKind.NewRun,commandId="REVIEW-NORMAL-NEW",seed=42,expectedActionSequence=0});
        check(start.accepted,"Normal fixture failed: "+start.error);
        var debug=GameSession.CreateDebugWorkspace(cfg,new MemorySaveStore(),normal.State);
        RunState n=normal.State,d=debug.State;
        string nPath=Path.Combine(dir,"normal.json"),dPath=Path.Combine(dir,"debug.json");
        test("PERSIST-NORMAL-AND-READONLY-DEBUG-ROUNDTRIP",()=>{
            new AtomicSaveStore(nPath,cfg,SaveSlotPolicy.Normal).Save(n);
            new AtomicSaveStore(dPath,cfg,SaveSlotPolicy.Debug).Save(d);
            var nl=new AtomicSaveStore(nPath,cfg,SaveSlotPolicy.Normal).Load();
            var dl=new AtomicSaveStore(dPath,cfg,SaveSlotPolicy.Debug).Load();
            check(nl!=null&&nl.runId==n.runId&&!nl.debugWorkspace&&!nl.isDebug,"Normal identity changed.");
            check(dl!=null&&dl.runId==d.runId&&dl.sourceRunId==n.runId&&dl.debugWorkspace&&!dl.isDebug,"Read-only debug isolation failed.");
        });
        test("PERSIST-POLICY-REJECT-BOTH-DIRECTIONS-WITHOUT-WRITING",()=>{
            foreach(var row in new[]{(path:nPath,policy:SaveSlotPolicy.Normal,rejected:d),(path:dPath,policy:SaveSlotPolicy.Debug,rejected:n)}) {
                var bytes=File.ReadAllBytes(row.path);bool threw=false;
                try{new AtomicSaveStore(row.path,cfg,row.policy).Save(row.rejected);}catch(InvalidDataException){threw=true;}
                check(threw,"Foreign workspace save allowed.");check(bytes.SequenceEqual(File.ReadAllBytes(row.path)),"Foreign save altered file.");
            }
        });
        test("PERSIST-VALID-CHECKSUM-FOREIGN-SLOT-LOAD-REJECTS",()=>{
            var normalReader=new AtomicSaveStore(dPath,cfg,SaveSlotPolicy.Normal);var debugReader=new AtomicSaveStore(nPath,cfg,SaveSlotPolicy.Debug);
            check(normalReader.Load()==null&&normalReader.LastLoadNotice.Contains("调试"),"Normal accepted checksum-valid debug workspace.");
            check(debugReader.Load()==null&&debugReader.LastLoadNotice.Contains("普通"),"Debug accepted checksum-valid normal run.");
        });
        test("PERSIST-FOREIGN-ACTIVE-RESTORES-VALID-NORMAL-BACKUP",()=>{
            string active=Path.Combine(dir,"recovery.json");File.Copy(dPath,active);File.Copy(nPath,active+".backup");
            var loader=new AtomicSaveStore(active,cfg,SaveSlotPolicy.Normal);var loaded=loader.Load();
            check(loaded!=null&&loaded.runId==n.runId&&!loaded.debugWorkspace,"Did not recover normal backup.");
            check(loader.LastLoadNotice.Contains("备份"),"Recovery was not surfaced.");
        });
        test("PERSIST-FOREIGN-ACTIVE-NOT-ROTATED-INTO-GOOD-BACKUP",()=>{
            string active=Path.Combine(dir,"quarantine.json");File.Copy(dPath,active);File.Copy(nPath,active+".backup");
            byte[] backup=File.ReadAllBytes(active+".backup"),foreign=File.ReadAllBytes(active);
            new AtomicSaveStore(active,cfg,SaveSlotPolicy.Normal).Save(n);
            check(File.ReadAllBytes(active+".backup").SequenceEqual(backup),"Debug file replaced known normal backup.");
            var rejected=Directory.GetFiles(dir,"quarantine.json.rejected-*");
            check(rejected.Length==1&&File.ReadAllBytes(rejected[0]).SequenceEqual(foreign),"Foreign bytes were not retained exactly.");
        });
        test("PERSIST-EXTERNAL-CORRUPTION-INVALIDATES-BYTE-CACHE",()=>{
            string active=Path.Combine(dir,"cached.json");var store=new AtomicSaveStore(active,cfg,SaveSlotPolicy.Normal);store.Save(n);store.Save(n);
            var bytes=File.ReadAllBytes(active);bytes[bytes.Length/2]^=1;File.WriteAllBytes(active,bytes);
            store.Save(n);check(new AtomicSaveStore(active,cfg,SaveSlotPolicy.Normal).Load()!=null,"Cache bypass preserved corrupt active.");
            var rejected=Directory.GetFiles(dir,"cached.json.rejected-*");
            check(rejected.Length==1&&File.ReadAllBytes(rejected[0]).SequenceEqual(bytes),"External corruption not detected/preserved.");
        });
        test("PERSIST-IO-FAILURE-LEAVES-COMMITTED-BYTES-RECOVERABLE",()=>{
            string active=Path.Combine(dir,"blocked.json");var store=new AtomicSaveStore(active,cfg,SaveSlotPolicy.Normal);store.Save(n);
            var bytes=File.ReadAllBytes(active);Directory.CreateDirectory(active+".tmp");bool threw=false;
            try{store.Save(n);}catch(IOException){threw=true;}catch(UnauthorizedAccessException){threw=true;}
            check(threw,"Deliberately blocked temp path did not fail.");check(File.ReadAllBytes(active).SequenceEqual(bytes),"Failed temp write altered committed file.");
            check(new AtomicSaveStore(active,cfg,SaveSlotPolicy.Normal).Load()!=null,"Failed temp write broke recovery.");
        });
    }
}
