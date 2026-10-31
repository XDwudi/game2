using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using DiceCraft.Core;

/// <summary>Integration trajectory for every stage/settlement/shop transition; explicitly experimental, never balance evidence.</summary>
public static class DebugRunTrace
{
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
    private static GameConfig cfg;
    private static GameSession session;
    private static MemorySaveStore store;
    private static int sequence;
    private static readonly List<object> steps = new List<object>();

    public static int Main()
    {
        cfg = JsonSerializer.Deserialize<GameConfig>(GameConfig.PrepareJsonForUnity(File.ReadAllText("DicePrototype/Assets/Game/Data/P0Config.json")), Json); cfg.Validate();
        store = new MemorySaveStore(); session = GameSession.CreateDebugWorkspace(cfg, store);
        var initialWorkspace = session.State;
        var scene = session.ExportDebugScene();
        foreach (var d in scene.dice) foreach (var f in d.faces) { f.permanentPip = 4; f.baseColor = "red"; f.isGold = true; }
        string[] cards = { "C25", "C30", "C31", "C32" };
        for (int i = 0; i < cards.Length; i++) scene.controllers.Add(new ControllerInstance {
            definitionId = cards[i], enchantmentId = "mult2", acquiredSerial = i + 1, instanceId = "ITEM_" + (i + 1).ToString("D5")
        });
        foreach (var h in scene.handLevels) h.level = 8;
        foreach (var h in scene.colorLevels) h.level = 8;
        scene.remainingSubmissions = -1; scene.rollsRemaining = -1;
        Act(CommandKind.ApplyDebugScene, c => c.debugScene = scene);
        int shops = 0, risks = 0;
        for (int stage = 0; stage < cfg.stages.Length; stage++)
        {
            if (stage != 0)
            {
                if (cfg.stages[stage].riskEnabled) { Act(CommandKind.ChooseChallenge, c => c.risk = true); risks++; }
                Act(CommandKind.StartStage);
            }
            Act(CommandKind.Roll);
            Act(CommandKind.ConfirmLocks, c => c.dieIds = session.State.dice.Select(d => d.dieId).ToArray());
            Act(CommandKind.Submit);
            if (!session.State.stage.qualified) throw new Exception("Debug transition fixture did not qualify stage " + stage);
            Act(CommandKind.EndStage); Act(CommandKind.Continue);
            if (stage < cfg.stages.Length - 1)
            {
                if (session.State.phase != "Shop") throw new Exception("Missing shop"); shops++;
                Act(CommandKind.LeaveShop);
            }
        }
        if (session.State.phase != "RunWon" || !session.State.isDebug || shops != 23 || risks != 8) throw new Exception("Debug trajectory incomplete");
        Directory.CreateDirectory("evidence/v06/session");
        File.WriteAllText("evidence/v06/session/debug-24-trace.json", JsonSerializer.Serialize(new {
            configVersion = cfg.configVersion, debug = true, purpose = "24 stage / 23 shop transition integration, not balance evidence", initialWorkspace,
            expectedFinalPhase = session.State.phase, stages = 24, shops, risks, steps
        }, Json));
        File.WriteAllText("evidence/v06/session/debug-24-final-state.json", JsonSerializer.Serialize(session.State, Json));
        Console.WriteLine("DEBUG_24_PASSED stages=24 shops=" + shops + " risks=" + risks + " commands=" + steps.Count + " restores=" + steps.Count + " duplicateChecks=" + steps.Count);
        return 0;
    }

    private static void Act(CommandKind kind, Action<SessionCommand> configure = null)
    {
        var cmd = new SessionCommand { commandId = "debug_" + (++sequence), kind = kind, expectedActionSequence = session.ActionSequence };
        configure?.Invoke(cmd);
        if (kind == CommandKind.ConfirmLocks) foreach (string id in cmd.dieIds) if (!session.TogglePending(id)) throw new Exception("Cannot select die " + id);
        var result = session.Execute(cmd); if (!result.accepted) throw new Exception(kind + ": " + result.error);
        var state = session.State;
        string serialized = JsonSerializer.Serialize(state, Json);
        session = new GameSession(cfg, store, JsonSerializer.Deserialize<RunState>(serialized, Json));
        if (JsonSerializer.Serialize(session.State, Json) != serialized) throw new Exception("Restore changed snapshot");
        if (!session.Execute(cmd).duplicate || JsonSerializer.Serialize(session.State, Json) != serialized) throw new Exception("Duplicate changed snapshot");
        steps.Add(new { command = cmd, actionSequence = state.actionSequence, state.phase, state.gold, stageIndex = state.stage.stageIndex,
            stageScore = state.stage.score, target = state.stage.target, submissionScore = result.receipt.score,
            state.diceRngState, state.shopRngState, state.effectRngState, stateHash = GameSession.StateHash(state) });
    }
}
