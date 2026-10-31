using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using DiceCraft.Core;

internal static class TraceChecks
{
    private static string Dump(object x)=>JsonSerializer.Serialize(x,ReviewProgram.Json);
    private static T Read<T>(string path)=>JsonSerializer.Deserialize<T>(File.ReadAllText(path),ReviewProgram.Json);
    private static string Text(JsonElement e,string key)=>e.GetProperty(key).ValueKind==JsonValueKind.Null?null:e.GetProperty(key).GetString();
    public static void Run(GameConfig cfg,Action<string,Action> test,Action<bool,string> check,string detailsPath)
    {
        var results=new List<object>();
        test("TRACE-NORMAL-145-COMMANDS-RESTORE-AND-DUPLICATE-FULL-STATE",()=>{
            using var file=JsonDocument.Parse(File.ReadAllText("evidence/v06/session/normal-run-trace.json"));var input=file.RootElement;
            var expected=Read<RunState>("evidence/v06/session/normal-run-final-state.json");
            var store=new MemorySaveStore();var session=new GameSession(cfg,store);int count=0;
            foreach(var step in input.GetProperty("steps").EnumerateArray()) {
                ++count;
                var command=new SessionCommand {kind=Enum.Parse<CommandKind>(Text(step,"kind")),commandId="normal_"+count,expectedActionSequence=session.ActionSequence,
                    seed=step.GetProperty("seed").GetUInt32(),risk=step.GetProperty("risk").GetBoolean(),offerId=Text(step,"offerId"),itemInstanceId=Text(step,"itemInstanceId"),
                    targetDieId=Text(step,"targetDieId"),targetFaceId=Text(step,"targetFaceId"),handId=Text(step,"handId"),
                    dieIds=step.GetProperty("dieIds").EnumerateArray().Select(x=>x.GetString()).ToArray(),targetPips=step.GetProperty("targetPips").EnumerateArray().Select(x=>x.GetInt32()).ToArray()};
                if(command.kind==CommandKind.ConfirmLocks)foreach(var die in command.dieIds)check(session.TogglePending(die),"Normal cannot select "+die);
                var action=session.Execute(command);check(action.accepted,"Normal step "+count+": "+action.error);
                var s=session.State;
                check(!s.isDebug&&!s.debugWorkspace,"Normal replay entered debug");
                Compare(step,s,action,check);
                var frozen=Dump(s);check(Dump(store.Saved)==frozen,"Committed memory store differs at "+count);
                session=new GameSession(cfg,store,JsonSerializer.Deserialize<RunState>(frozen,ReviewProgram.Json));
                check(Dump(session.State)==frozen,"Restore changed normal state at "+count);
                var duplicate=session.Execute(command);check(duplicate.accepted&&duplicate.duplicate&&Dump(session.State)==frozen&&Dump(duplicate.receipt)==Dump(action.receipt),"Normal duplicate mismatch at "+count);
                var originalReceipt=expected.processedReceipts[count-1];
                check(Dump(action.receipt)==Dump(originalReceipt),"Independent replay differs from retained normal receipt at "+count);
                results.Add(new {trajectory="normal",count,command=command.kind.ToString(),s.phase,stage=s.stage.stageIndex+1,s.gold,score=s.stage.score,s.diceRngState,s.shopRngState,s.effectRngState,stateHash=GameSession.StateHash(s),restore=true,duplicate=true});
            }
            check(count==145&&session.State.stage.stageIndex+1==input.GetProperty("expectedFinalStage").GetInt32()&&session.State.phase==Text(input,"expectedFinalPhase"),"Normal terminal declaration differs");
            check(Dump(session.State)==Dump(expected),"Normal full final state differs");
        });
        test("TRACE-DEBUG-175-COMMANDS-24-STAGES-23-SHOPS-8-RISKS",()=>{
            using var file=JsonDocument.Parse(File.ReadAllText("evidence/v06/session/debug-24-trace.json"));var input=file.RootElement;
            var initial=JsonSerializer.Deserialize<RunState>(input.GetProperty("initialWorkspace").GetRawText(),ReviewProgram.Json);
            check(initial.debugWorkspace&&!initial.isDebug,"Debug trajectory did not preserve entry-only mark");
            var store=new MemorySaveStore();var session=new GameSession(cfg,store,initial);int count=0,shops=0,risks=0,completed=0;
            foreach(var step in input.GetProperty("steps").EnumerateArray()) {
                ++count;var command=JsonSerializer.Deserialize<SessionCommand>(step.GetProperty("command").GetRawText(),ReviewProgram.Json);
                if(command.kind==CommandKind.ConfirmLocks)foreach(var die in command.dieIds)check(session.TogglePending(die),"Debug cannot select "+die);
                var action=session.Execute(command);check(action.accepted,"Debug step "+count+": "+action.error);var s=session.State;
                check(s.isDebug&&s.debugWorkspace,"Debug marker absent");Compare(step,s,action,check);
                check(s.diceRngState==step.GetProperty("diceRngState").GetUInt32()&&s.shopRngState==step.GetProperty("shopRngState").GetUInt32()&&s.effectRngState==step.GetProperty("effectRngState").GetUInt32(),"Debug RNG stream differs at "+count);
                check(GameSession.StateHash(s)==Text(step,"stateHash"),"Debug state hash differs at "+count);
                var frozen=Dump(s);check(Dump(store.Saved)==frozen,"Committed debug store differs");
                session=new GameSession(cfg,store,JsonSerializer.Deserialize<RunState>(frozen,ReviewProgram.Json));
                var duplicate=session.Execute(command);check(duplicate.accepted&&duplicate.duplicate&&Dump(session.State)==frozen&&Dump(duplicate.receipt)==Dump(action.receipt),"Debug restore/repeat differs at "+count);
                if(command.kind==CommandKind.Continue&&s.phase=="Shop")shops++;
                if(command.kind==CommandKind.ChooseChallenge&&command.risk)risks++;
                if(command.kind==CommandKind.EndStage)completed++;
                results.Add(new {trajectory="debug",count,command=command.kind.ToString(),s.phase,stage=s.stage.stageIndex+1,s.gold,score=s.stage.score,s.diceRngState,s.shopRngState,s.effectRngState,stateHash=GameSession.StateHash(s),restore=true,duplicate=true});
            }
            check(count==175&&shops==23&&risks==8&&completed==24&&session.State.phase=="RunWon","Debug stage coverage differs");
            check(Dump(session.State)==Dump(Read<RunState>("evidence/v06/session/debug-24-final-state.json")),"Debug full final state differs");
        });
        test("SESSION-NULL-DTO-HASH-MATCHES-UNITY-DEFAULT-PLACEHOLDERS",()=>{
            var store=new MemorySaveStore();var s=new GameSession(cfg,store);
            var start=s.Execute(new SessionCommand {kind=CommandKind.NewRun,commandId="NULL-DTO",expectedActionSequence=0,seed=55});check(start.accepted,"Fixture failed");
            var native=s.State;var materialized=native.Clone();
            materialized.shop=new ShopState();materialized.hand=new HandState();materialized.lastStageSettlement=new StageSettlement();
            s.ValidateState(materialized);check(GameSession.StateHash(native)==GameSession.StateHash(materialized),"Semantically absent Unity default DTOs alter canonical hash");
            var a=new GameSession(cfg,new MemorySaveStore(),native);var b=new GameSession(cfg,new MemorySaveStore(),materialized);
            var cmd=new SessionCommand {kind=CommandKind.StartStage,commandId="NEXT-START",expectedActionSequence=1};
            var ra=a.Execute(cmd);var rb=b.Execute(cmd);check(ra.accepted&&rb.accepted&&ra.receipt.stateHash==rb.receipt.stateHash,"Next accepted receipt differs after placeholder restore");
        });
        File.WriteAllText(detailsPath,JsonSerializer.Serialize(new {scope="Independent .NET trajectory replay; not Unity or platform run",steps=results},new JsonSerializerOptions{WriteIndented=true}));
    }
    private static void Compare(JsonElement step,RunState s,CommandResult action,Action<bool,string> check)
    {
        check(s.actionSequence==step.GetProperty("actionSequence").GetInt64()&&s.gold==step.GetProperty("gold").GetInt64()&&s.phase==Text(step,"phase"),"Sequence/wallet/phase mismatch");
        check(s.stage.stageIndex==step.GetProperty("stageIndex").GetInt32()&&s.stage.score==Text(step,"stageScore")&&s.stage.target==Text(step,"target")&&action.receipt.score==Text(step,"submissionScore"),"Stage/score/target mismatch");
        if(step.TryGetProperty("rollsRemaining",out var rolls))check((s.hand?.rollsRemaining??0)==rolls.GetInt32(),"Roll budget mismatch");
        if(step.TryGetProperty("remainingSubmissions",out var submits))check(s.stage.remainingSubmissions==submits.GetInt32(),"Submit budget mismatch");
    }
}
