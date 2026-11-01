using System;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Presentation;
using UnityEngine.UI;

namespace DiceCraft.Verification
{
    public enum Art07FixtureKind { G147, GoldenSample, MaximumTable, MaximumPool, Debug32, ZeroMultiplier, SharedHalf, PermanentPost, Qualified }

    public sealed class Art07FixtureReport
    {
        public string fixture, runId, stateHash, previewScore;
        public int dice, controllers, inventory, capacity, locked;
        public bool debugWorkspace, expectedValuesMatched;
    }

    /// <summary>Reproducible presentation scenes submitted through the real debug command API.</summary>
    public static class Art07Fixtures
    {
        public static DebugScene Create(GameConfig cfg, Art07FixtureKind kind, int firstItemSerial = 1, int firstDieSerial = 1)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (firstItemSerial < 1 || firstDieSerial < 1) throw new ArgumentOutOfRangeException();
            var scene = new DebugScene {
                configVersion = cfg.configVersion, sourceRunId = "Art07Fixture", note = "art0.7.0 " + kind,
                seed = 20261031, diceRngState = 20261031, shopRngState = 20261032, effectRngState = 20261033,
                stageIndex = 0, gold = 30, target = "1800000", score = "0", capacityOverride = 5,
                remainingSubmissions = 3, rollsRemaining = 2, consumableSlots = 5,
                handLevels = cfg.handDefinitions.Select(h => new HandLevel { handId = h.id, level = kind == Art07FixtureKind.G147 ? 4 : 0 }).ToList(),
                colorLevels = cfg.colorDefinitions.Select(h => new HandLevel { handId = h.id, level = kind == Art07FixtureKind.G147 ? 4 : 0 }).ToList()
            };
            int dieCount = kind == Art07FixtureKind.Debug32 ? 32 : kind == Art07FixtureKind.MaximumTable || kind == Art07FixtureKind.MaximumPool ? 10 : 5;
            scene.capacityOverride = kind == Art07FixtureKind.Debug32 ? 32 : dieCount == 10 ? 8 : 5;
            for (int i = 0; i < dieCount; i++) scene.dice.Add(DiceFactory.CreateStandard(cfg, firstDieSerial + i));
            string[] definitions;
            switch (kind)
            {
                case Art07FixtureKind.G147: definitions = new[] { "C02", "C30", "C31", "C32" }; break;
                case Art07FixtureKind.GoldenSample: definitions = new[] { "C02", "C41", "C45", "C52" }; break;
                case Art07FixtureKind.MaximumTable: case Art07FixtureKind.MaximumPool: definitions = new[] { "C02", "C41", "C45", "C52", "C30", "C31", "C32", "C39", "C48", "C50" }; break;
                case Art07FixtureKind.Debug32: definitions = Enumerable.Repeat("C46", 32).ToArray(); break;
                case Art07FixtureKind.ZeroMultiplier: definitions = new[] { "C49", "C01", "C43", "C38" }; scene.previousPrimaryHandId = "five_kind"; break;
                case Art07FixtureKind.SharedHalf: definitions = new[] { "C41", "C53" }; break;
                case Art07FixtureKind.PermanentPost: definitions = new[] { "C36", "C42", "C38", "C01", "C15" }; scene.capacityOverride=6; break;
                case Art07FixtureKind.Qualified: definitions = new[] { "C02", "C30", "C31", "C32" }; scene.target="1"; break;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
            string[] enchants = { "none", "slotless", "base10", "mult2", "roll1", "submit1" };
            for (int i = 0; i < definitions.Length; i++)
            {
                int serial = firstItemSerial + i;
                string enchant = kind == Art07FixtureKind.G147 || kind == Art07FixtureKind.ZeroMultiplier || kind == Art07FixtureKind.SharedHalf || kind == Art07FixtureKind.PermanentPost || kind == Art07FixtureKind.Qualified ? "none" : enchants[i % enchants.Length];
                if (kind == Art07FixtureKind.MaximumTable || kind == Art07FixtureKind.MaximumPool) enchant = i < 4 ? new[] { "base10", "mult2", "roll1", "submit1" }[i] : "slotless";
                scene.controllers.Add(new ControllerInstance { instanceId = "ITEM_" + serial.ToString("D5"), definitionId = definitions[i], acquiredSerial = serial,
                    enchantmentId = enchant,
                    state = new ControllerDynamicState { activeColor = definitions[i] == "C45" ? "blue" : null, fundAccrual = definitions[i] == "C52" ? 6 : 0, turnoverUnits2 = 3 } });
            }
            if (kind == Art07FixtureKind.SharedHalf)
                scene.randomOverrides.Add(new DebugRandomOverride { kind = "C53", sourceId = scene.controllers[1].instanceId, value = "1" });
            int itemCount = kind == Art07FixtureKind.G147 || kind == Art07FixtureKind.SharedHalf || kind == Art07FixtureKind.ZeroMultiplier ? 0 : 5;
            for (int i = 0; i < itemCount; i++) scene.inventory.Add(new OwnedItem { instanceId = "ITEM_" + (firstItemSerial + definitions.Length + i).ToString("D5"), contentId = "I" + (i + 1).ToString("D2") });
            scene.currentResults = new System.Collections.Generic.List<DieHandState>();
            int lockedCount = kind == Art07FixtureKind.MaximumPool ? 0 : kind == Art07FixtureKind.Debug32 ? 16 : kind == Art07FixtureKind.MaximumTable ? 8 : kind == Art07FixtureKind.GoldenSample ? 3 : 5;
            for (int i = 0; i < scene.dice.Count; i++)
            {
                var die = scene.dice[i]; int pip = kind == Art07FixtureKind.G147 || kind == Art07FixtureKind.Debug32 ? 6 : kind == Art07FixtureKind.ZeroMultiplier ? 2 : i % 6 + 1;
                var face = die.faces.Single(f => f.permanentPip == pip); face.baseColor = "blue";
                if (kind == Art07FixtureKind.GoldenSample || kind == Art07FixtureKind.MaximumTable || kind == Art07FixtureKind.MaximumPool)
                {
                    if (i == 1) die.isGlass = true;
                    if (i == 2) { face.isGold = true; face.texture = "smooth"; }
                    if (i == 3) { face.texture = "rough"; face.baseColor = "red"; }
                    if (i == 4 || i == 9) { die.lifetime = "stage_temporary"; die.createdStageId = cfg.stages[0].stageId; }
                }
                bool locked = i < lockedCount;
                scene.currentResults.Add(new DieHandState { dieId = die.dieId, currentFaceId = face.faceId, currentPip = pip, currentColor = face.baseColor, hasRolled = true, isLocked = locked });
                if (locked) scene.orderedLockedIds.Add(die.dieId);
            }
            if(kind==Art07FixtureKind.PermanentPost)
            {
                // Two 3→4 faces plus a last-slot red→blue dye produce three
                // distinct net-changed targets, alongside a forced C42 loss.
                for(int i=0;i<scene.dice.Count;i++)
                {
                    var face=scene.dice[i].faces.Single(f=>f.faceId=="F06");
                    face.permanentPip=i<2?3:6;face.baseColor=i==1?"blue":"red";
                    var current=scene.currentResults[i];current.currentFaceId=face.faceId;
                    current.currentPip=i<2?1:face.permanentPip;current.currentColor=face.baseColor;current.remoteOverride=i<2;
                }
                scene.randomOverrides.Add(new DebugRandomOverride{kind="C42",sourceId=scene.controllers[1].instanceId,value="break"});
            }
            return scene;
        }

        public static Art07FixtureReport Apply(GameApp app, Art07FixtureKind kind)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));
            if (app.IsBusy || app.Session.HasPendingSave) throw new InvalidOperationException("Finish the current presentation/save before applying an art fixture.");
            if (!app.UnlockDebug("XD is god") || !app.EnterDebugWorkspace()) throw new InvalidOperationException("Could not enter the isolated debug workspace.");
            var before = app.Session.State;
            var scene = Create(app.Config, kind, before.nextItemSerial, before.nextDieSerial);
            scene.sourceRunId = before.sourceRunId;
            var result = app.Send(new SessionCommand { kind = CommandKind.ApplyDebugScene, debugScene = scene });
            if (!result.accepted || result.pendingSave) throw new InvalidOperationException("Art fixture rejected: " + result.error);
            // This invokes the existing public button action, never modifies modal/rule internals.
            var close = app.canvas.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.gameObject.activeInHierarchy && b.name == "RunDebugScene");
            if (close != null) close.onClick.Invoke();
            var state = app.Session.State;
            var preview = app.Session.PreviewScoreRange();
            Verify(kind, state, preview);
            return new Art07FixtureReport { fixture = kind.ToString(), runId = state.runId, stateHash = GameSession.StateHash(state), previewScore = preview?.minimum?.score,
                dice = state.dice.Count, controllers = state.controllers.Count, inventory = state.inventory.Count, capacity = state.stage.capacity, locked = state.hand.orderedLockedIds.Count,
                debugWorkspace = state.debugWorkspace, expectedValuesMatched = true };
        }

        public static PresentationReceipt Submit(GameApp app, Art07FixtureKind expectedKind)
        {
            if (app == null) throw new ArgumentNullException(nameof(app)); var before = app.Session.State;
            var result = app.Send(new SessionCommand { kind = CommandKind.Submit });
            if (!result.accepted || result.pendingSave) throw new InvalidOperationException("Art fixture submission rejected: " + result.error);
            var receipt = PresentationReceiptAdapter.BuildCommitted(result, before, app.Session.State, app.Config);
            if (expectedKind == Art07FixtureKind.G147 && (receipt.ScoreDecimal != "967680" || receipt.MultiplierNumeratorDecimal != "26880" || receipt.MultiplierDenominatorDecimal != "1" || receipt.ChainCount != 4 || receipt.ComboTier != 5 || receipt.QualifiedAfter))
                throw new InvalidOperationException("G147 final presentation does not match the frozen example.");
            if (expectedKind == Art07FixtureKind.ZeroMultiplier && (receipt.ScoreDecimal != "0" || receipt.ComboTier != 0 || !receipt.Events.Any(e => e.EffectFamily == "SubmissionIncome") || !receipt.Events.Any(e => e.EffectFamily == "HandGrown")))
                throw new InvalidOperationException("Zero multiplier fixture lost its legal post-score effects.");
            if(expectedKind==Art07FixtureKind.PermanentPost&&(!result.receipt.effects.Any(e=>e.kind=="FaceValueChanged"&&e.oldValue==3&&e.newValue==4)||!result.receipt.effects.Any(e=>e.kind=="ControllerDestroyed")))
                throw new InvalidOperationException("Permanent-post fixture did not actually change its faces and destroy its risk source.");
            if(expectedKind==Art07FixtureKind.PermanentPost)
            {
                var after=app.Session.State;
                var changed=before.dice.Where(d=>after.dice.Any(n=>n.dieId==d.dieId&&n.faces.Where((f,i)=>f.permanentPip!=d.faces[i].permanentPip||f.baseColor!=d.faces[i].baseColor||f.texture!=d.faces[i].texture||f.isGold!=d.faces[i].isGold).Any())).Select(d=>d.dieId).ToArray();
                if(changed.Length!=3)throw new InvalidOperationException("Permanent-post fixture must visibly change exactly three distinct dice.");
            }
            if(expectedKind==Art07FixtureKind.Qualified&&!receipt.QualifiedAfter)
                throw new InvalidOperationException("Qualified fixture did not actually reach its declared target.");
            return receipt;
        }

        public static void Verify(Art07FixtureKind kind, RunState state, ScorePreview preview)
        {
            if (state == null || !state.debugWorkspace || !state.isDebug || state.phase != "Selecting") throw new InvalidOperationException("Fixture is not an applied independent debug scene.");
            if (kind == Art07FixtureKind.G147)
            {
                if (preview?.minimum?.score != "967680" || preview.maximum.score != "967680" || preview.isRandom || state.stage.target != "1800000" || state.stage.score != "0" || state.stage.qualified || state.hand.rollsRemaining != 2 || state.hand.orderedLockedIds.Count != 5)
                    throw new InvalidOperationException("G147 fixture differs from the authoritative example.");
            }
            if ((kind == Art07FixtureKind.MaximumTable || kind == Art07FixtureKind.MaximumPool) && (state.dice.Count != 10 || state.controllers.Count != 10 || state.stage.capacity != 8 || state.inventory.Count != 5)) throw new InvalidOperationException("Maximum ordinary-sized fixture is incomplete.");
            if (kind == Art07FixtureKind.Debug32 && (state.dice.Count != 32 || state.controllers.Count != 32 || state.stage.capacity != 32)) throw new InvalidOperationException("Debug32 fixture is incomplete.");
            if (kind == Art07FixtureKind.ZeroMultiplier && preview?.minimum?.score != "0") throw new InvalidOperationException("Zero-multiplier fixture contains an unexpected score.");
            if (kind == Art07FixtureKind.GoldenSample && !state.controllers.Select(c => c.definitionId).SequenceEqual(new[] { "C02", "C41", "C45", "C52" })) throw new InvalidOperationException("Golden sample controller identities/order changed.");
        }
    }
}
