using System;
using System.Collections.Generic;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Presentation;
using UnityEngine;

namespace DiceCraft
{
    // V1.1.1 task W7b (RULES_DELTA 7.3): glass dice break after the submission that carried them, never on a roll. The
    // v0.7 shatter (BreakDie07, the outline of the further dice, twelve fragments at most) plays on the post-score beat
    // whose events are the committed "DieBroken" records. Presentation only: the dice are already gone from the
    // committed state; the frozen pre-submission view still shows them until this beat.
    public sealed partial class GameApp
    {
        /// <summary>The first dice of a receipt that shatter into fragments; every further one leaves its outline.</summary>
        public const int ShatteredDiceLimit111=2;
        /// <summary>Fragments one shattering die may use (two dice: twelve at most).</summary>
        public const int FragmentsPerDie111=6;
        // Dice of the receipt on screen that have broken already: a redraw during the rest of the presentation must
        // not put them back into their slots.
        private readonly HashSet<string> shatteredDice111=new HashSet<string>(StringComparer.Ordinal);
        /// <summary>The dice the presentation on screen has taken off the table as broken, in no particular order.</summary>
        public string[] ShatteredDice111=>shatteredDice111.ToArray();
        private bool DieShattered111(string dieId)=>IsPresenting07&&shatteredDice111.Contains(dieId);
        /// <summary>The broken dice of a presentation receipt in the order the rules judged them (the submission order).</summary>
        private static string[] BrokenDice111(PresentationReceipt receipt)
            =>receipt==null?Array.Empty<string>():receipt.Events.Where(e=>e.EffectFamily=="DieBroken"&&e.TargetDieIds.Count>0).Select(e=>e.TargetDieIds[0]).Distinct(StringComparer.Ordinal).ToArray();

        /// <summary>
        /// The shatter of the glass dice this beat reports. Each die is read where it stands in its slot (position,
        /// rotation and size of the real die), leaves the table, and either shatters there (the first two of the
        /// receipt, six fragments each) or leaves its outline (the others; all of them with reduced motion or low
        /// quality, as on the v0.7 roll). The sound is the event's own cue.
        /// </summary>
        private void PresentSubmissionBreaks111(PresentationBeat beat)
        {
            var here=beat.Events.Where(e=>e.EffectFamily=="DieBroken"&&e.TargetDieIds.Count>0).ToArray();
            if(here.Length==0)return;
            var order=BrokenDice111(receipt07).ToList();
            var poses=new Dictionary<string,DiePose07>(StringComparer.Ordinal);
            foreach(var e in here)
            {
                string id=e.TargetDieIds[0];var source=table.GetFaceOrientation(id);
                if(source!=null&&source.gameObject.activeInHierarchy&&!shatteredDice111.Contains(id))poses[id]=new DiePose07{Position=source.position,Rotation=source.rotation,Scale=source.lossyScale};
            }
            if(!instant07&&EffectGroup07("risk"))
            {
                int outlineFrom=effectOptions07.ReducedMotion||effectOptions07.Quality==PresentationQuality.Low?0:ShatteredDiceLimit111;
                foreach(var e in here)
                {
                    string id=e.TargetDieIds[0];int index=order.IndexOf(id);
                    if(!poses.TryGetValue(id,out var pose))continue;
                    if(index>=outlineFrom)ShowBrokenOutline07(id,pose);
                    if(index<ShatteredDiceLimit111)BreakDie07(id,e.EventId,pose);
                }
            }
            foreach(var e in here){string id=e.TargetDieIds[0];shatteredDice111.Add(id);table.HideDie111(id);}
        }
    }
}
