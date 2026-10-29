using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using DiceCraft.Core;

public static class NormalRunReplay
{
    public static int Main()
    {
        var json = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
        var cfg = JsonSerializer.Deserialize<GameConfig>(File.ReadAllText("DicePrototype/Assets/Game/Data/P0Config.json"), json); cfg.Validate();
        var source = JsonDocument.Parse(File.ReadAllText("evidence/runs/normal-win.json")).RootElement;
        var store = new MemorySaveStore(); var session = new GameSession(cfg,store);
        var traces = new List<object>(); int count=0;
        foreach(var step in source.GetProperty("steps").EnumerateArray())
        {
            count++;
            string Text(string key) => step.GetProperty(key).ValueKind == JsonValueKind.Null ? null : step.GetProperty(key).GetString();
            var command = new SessionCommand {
                kind=Enum.Parse<CommandKind>(Text("kind")), commandId="normal_"+count,expectedActionSequence=session.State==null?0:session.State.actionSequence,
                seed=step.GetProperty("seed").GetUInt32(),risk=step.GetProperty("risk").GetBoolean(),offerId=Text("offerId"),itemInstanceId=Text("itemInstanceId"),
                targetDieId=Text("targetDieId"),targetFaceId=Text("targetFaceId"),handId=Text("handId"),
                dieIds=step.GetProperty("dieIds").EnumerateArray().Select(x=>x.GetString()).ToArray(),targetPips=step.GetProperty("targetPips").EnumerateArray().Select(x=>x.GetInt32()).ToArray()
            };
            if(command.kind==CommandKind.ConfirmLocks)foreach(var id in command.dieIds)Require(session.TogglePending(id),"Pending rejected");
            var result=session.Execute(command);Require(result.accepted,"Command rejected "+command.kind+": "+result.error);
            var state=session.State;
            Require(!state.isDebug,"Unexpected Debug state");
            Require(state.actionSequence==step.GetProperty("actionSequence").GetInt64(),"Sequence differs");
            Require(state.gold==step.GetProperty("gold").GetInt32(),"Gold differs");
            Require(state.phase==Text("phase"),"Phase differs");
            Require(state.stage.stageIndex==step.GetProperty("stageIndex").GetInt32(),"Stage differs");
            Require(state.stage.score==step.GetProperty("stageScore").GetInt64(),"Cumulative score differs");
            Require(state.stage.target==step.GetProperty("target").GetInt64(),"Target differs");
            Require(result.receipt.score==step.GetProperty("submissionScore").GetInt64(),"Submission score differs");
            string committed=JsonSerializer.Serialize(store.Saved,json);
            session=new GameSession(cfg,store,JsonSerializer.Deserialize<RunState>(committed,json));
            Require(JsonSerializer.Serialize(session.State,json)==committed,"State changed during serialization restore");
            var duplicate=session.Execute(command);Require(duplicate.accepted&&duplicate.duplicate,"Duplicate after restore did not return original receipt");
            Require(JsonSerializer.Serialize(session.State,json)==committed,"Duplicate command changed restored state");
            traces.Add(new{sequence=count,kind=command.kind.ToString(),result="Passed",state.phase,state.gold,score=state.stage.score});
        }
        var end=session.State;
        Require(end.phase=="RunWon"&&!end.isDebug,"Trajectory did not finish normal victory");
        var expected=JsonSerializer.Deserialize<RunState>(File.ReadAllText("evidence/runs/normal-win-final-state.json"),json);
        Require(JsonSerializer.Serialize(end,json)==JsonSerializer.Serialize(expected,json),"Final state differs from search result");
        File.WriteAllText("evidence/runs/normal-replay-report.json",JsonSerializer.Serialize(new{
            runner=".NET 10.0.403; real Core commands, not a Unity player",seed=source.GetProperty("seed").GetUInt32(),
            steps=count,restores=count,duplicateChecks=count,allPassed=true,debug=false,finalPhase=end.phase,finalGold=end.gold,
            finalStageScore=end.stage.score,traces},json));
        Console.WriteLine($"NORMAL_REPLAY_PASSED steps={count} restores={count} duplicateChecks={count} seed={end.seed} phase={end.phase} debug={end.isDebug}");return 0;
    }
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
