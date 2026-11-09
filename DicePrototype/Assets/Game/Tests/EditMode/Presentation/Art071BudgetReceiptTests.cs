using System;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Presentation.Tests
{
    public sealed class Art071BudgetReceiptTests
    {
        private GameConfig cfg;
        [SetUp] public void Setup()
        {
            cfg=JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(File.ReadAllText(Path.Combine(Application.dataPath,"Game/Data/P0Config.json"))));cfg.Validate();
        }
        [TestCase(false,false,true,2,true)]
        [TestCase(false,true,true,2,true)]
        [TestCase(true,true,true,2,true)]
        [TestCase(false,false,false,2,false)]
        [TestCase(false,false,true,0,false)]
        public void ART071_Receipt_NewHandGrantIsExplicitNonScoringAndNotQualificationDependent(bool wasQualified,bool qualified,bool newHand,int sourceCount,bool expected)
        {
            var before=new RunState{runId="run",phase="Selecting",stage=new StageState{score="0",target="1000",qualified=wasQualified},hand=new HandState{handId="H1",rollsRemaining=1}};
            var after=before.Clone();after.phase="HandReady";after.stage.qualified=qualified;
            after.hand.handId=newHand?"H2":"H1";after.hand.rollsRemaining=cfg.totalRollsPerHand+sourceCount*cfg.enchantRules.rollBonus;
            after.hand.rollBonusSourceIds=Enumerable.Range(1,sourceCount).Select(i=>"roll-owner-"+i).ToArray();
            var result=new CommandResult{accepted=true,receipt=new CommandReceipt{commandId="actual-saved-command",phase=after.phase}};
            string beforeBytes=JsonUtility.ToJson(before),afterBytes=JsonUtility.ToJson(after);
            var receipt=PresentationReceiptAdapter.BuildCommitted(result,before,after,cfg);
            var grants=receipt.Events.Where(e=>e.EffectFamily=="hand_roll_bonus").ToArray();
            Assert.That(grants.Length,Is.EqualTo(expected?1:0));Assert.That(receipt.ChainCount,Is.Zero);Assert.That(receipt.ScoreDecimal,Is.EqualTo("0"));
            if(expected)
            {
                var grant=grants.Single();Assert.That(grant.IsScoring||grant.CountsForChain,Is.False);
                CollectionAssert.AreEqual(after.hand.rollBonusSourceIds,grant.SourceEmitterIds);
                Assert.That(grant.Operand,Is.EqualTo((sourceCount*cfg.enchantRules.rollBonus).ToString()));
                Assert.That(grant.AfterValue,Is.EqualTo(after.hand.rollsRemaining.ToString()));Assert.That(grant.Detail,Is.EqualTo("H2"));
                foreach(var speed in new[]{PresentationSpeed.Normal,PresentationSpeed.Fast})
                {
                    var sequence=PresentationSequence.Create(receipt,new PresentationOptions(speed));
                    Assert.That(sequence.Beats.Single(b=>b.Phase==PresentationPhase.TargetState).Events.Count(e=>e.EventId==grant.EventId),Is.EqualTo(1));
                    var player=new PresentationPlayer();int calls=0;player.BeatStarted+=b=>calls+=b.Events.Count(e=>e.EventId==grant.EventId);
                    player.Play(sequence);player.Tick(4);Assert.That(calls,Is.EqualTo(1));
                    Assert.That(player.Play(sequence),Is.False);Assert.That(calls,Is.EqualTo(1));
                    Assert.That(player.Play(sequence,true),Is.True);player.Tick(4);Assert.That(calls,Is.EqualTo(2));
                    Assert.That(sequence.TotalDurationMs,Is.LessThanOrEqualTo(speed==PresentationSpeed.Normal?3200:800));
                }
                var instant=new PresentationPlayer();int instantBeats=0;instant.BeatStarted+=b=>instantBeats++;
                instant.Play(PresentationSequence.Create(receipt,new PresentationOptions(PresentationSpeed.Instant)));
                Assert.That(instantBeats,Is.Zero);Assert.That(instant.IsPlaying,Is.False);
            }
            Assert.That(JsonUtility.ToJson(before),Is.EqualTo(beforeBytes));Assert.That(JsonUtility.ToJson(after),Is.EqualTo(afterBytes));
            if(expected){after.hand.rollBonusSourceIds[0]="later-mutation";Assert.That(grants[0].SourceEmitterIds[0],Is.EqualTo("roll-owner-1"),"Receipt must detach committed source IDs.");}
        }
    }
}
