using System;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Presentation.Tests
{
    public sealed class IndependentA1ReadOnlyTests
    {
        [Test]
        public void IndependentlyRunRealSessionThenTwoHundredReplaysInEveryModeNeverMutatesStateOrSave()
        {
            var cfg=JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(File.ReadAllText("DicePrototype/Assets/Game/Data/P0Config.json")));
            var store=new MemorySaveStore(); var session=new GameSession(cfg,store);
            Action<CommandKind> execute=kind=>{
                var c=new SessionCommand{kind=kind,seed=20261031,commandId="independent-"+session.ActionSequence,expectedActionSequence=session.ActionSequence};
                if(kind==CommandKind.ConfirmLocks)c.dieIds=session.PendingIds;
                var r=session.Execute(c);Assert.That(r.accepted,Is.True,r.error);
            };
            execute(CommandKind.NewRun);execute(CommandKind.StartStage);execute(CommandKind.Roll);
            foreach(var die in session.State.dice)Assert.That(session.TogglePending(die.dieId),Is.True);
            execute(CommandKind.ConfirmLocks);
            var before=session.State;
            var result=session.Execute(new SessionCommand{kind=CommandKind.Submit,commandId="independent-submit",expectedActionSequence=session.ActionSequence});
            Assert.That(result.accepted,Is.True,result.error);
            var after=session.State;
            string serializedBefore=JsonUtility.ToJson(before),serializedAfter=JsonUtility.ToJson(after),serializedReceipt=JsonUtility.ToJson(result.receipt),saveBefore=JsonUtility.ToJson(store.Saved);
            var receipt=PresentationReceiptAdapter.BuildCommitted(result,before,after,cfg);
            foreach(PresentationSpeed speed in Enum.GetValues(typeof(PresentationSpeed)))foreach(bool reduced in new[]{false,true})
            {
                var sequence=PresentationSequence.Create(receipt,new PresentationOptions(speed,reduced));
                var player=new PresentationPlayer();int finals=0;
                player.FinalStateRequested+=r=>{Assert.That(r.ScoreDecimal,Is.EqualTo(result.receipt.score));finals++;};
                for(int i=0;i<200;i++)
                {
                    Assert.That(player.Play(sequence,true),Is.True);
                    player.Clock.Paused=true;player.Tick(20);player.Clock.Paused=false;
                    if(i%3==0)player.Skip();else if(i%3==1)player.Restore(receipt);else player.Tick(20);
                    Assert.That(player.IsPlaying,Is.False);
                }
                Assert.That(finals,Is.GreaterThanOrEqualTo(200));
            }
            Assert.That(JsonUtility.ToJson(before),Is.EqualTo(serializedBefore));
            Assert.That(JsonUtility.ToJson(after),Is.EqualTo(serializedAfter));
            Assert.That(JsonUtility.ToJson(session.State),Is.EqualTo(serializedAfter));
            Assert.That(JsonUtility.ToJson(result.receipt),Is.EqualTo(serializedReceipt));
            Assert.That(JsonUtility.ToJson(store.Saved),Is.EqualTo(saveBefore));
        }
    }
}
