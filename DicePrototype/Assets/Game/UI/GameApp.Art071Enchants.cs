using System;
using System.Collections.Generic;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Presentation;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private readonly List<Art071EnchantCue> enchantCues071=new List<Art071EnchantCue>(10);
        private readonly Dictionary<Art071EnchantCue,RectTransform> enchantTargets071=new Dictionary<Art071EnchantCue,RectTransform>();
        private readonly Vector3[] enchantCorners071=new Vector3[4];
        public string[] ActiveEnchantmentSources071=>enchantCues071.Where(c=>c.IsBound).Select(c=>c.BoundInstanceId+":"+c.BoundEnchantmentId).ToArray();

        private void InitializeEnchantCues071()
        {
            for(int i=0;i<10;i++)
            {
                var root=UiKit.Rect(libraryEffects07,"EnchantCue071_"+i,0,0,160,224);
                root.pivot=new Vector2(.5f,.5f);
                var cue=root.gameObject.AddComponent<Art071EnchantCue>();
                cue.Configure(new Vector2(160,224),enchantIcons.First(s=>s.name=="badge_enchant_submit1"),UiKit.Template("ui_icon_roll"),UiKit.Template("fx_arc_short"));
                enchantCues071.Add(cue);
            }
        }
        private Rect EnchantBounds071(RectTransform target)
        {
            target.GetWorldCorners(enchantCorners071);
            var a=ScreenToDesign(RectTransformUtility.WorldToScreenPoint(null,enchantCorners071[1]));
            var b=ScreenToDesign(RectTransformUtility.WorldToScreenPoint(null,enchantCorners071[3]));
            return Rect.MinMaxRect(Math.Min(a.x,b.x),Math.Min(a.y,b.y),Math.Max(a.x,b.x),Math.Max(a.y,b.y));
        }
        private void EnchantCue071(string kind,string instance,Rect area,Vector2 from,Vector2 to,RectTransform follow=null)
        {
            if(instant07||enchantCues071.Any(c=>c.IsBound&&c.BoundInstanceId==instance&&c.BoundEnchantmentId==kind))return;
            var cue=enchantCues071.FirstOrDefault(c=>!c.IsBound);if(cue==null)return;
            var rect=(RectTransform)cue.transform;rect.anchoredPosition=new Vector2(area.center.x,-area.center.y);
            cue.Configure(area.size,enchantIcons.First(s=>s.name=="badge_enchant_submit1"),UiKit.Template("ui_icon_roll"),UiKit.Template("fx_arc_short"));
            bool quiet=reduced||effectOptions07?.ReducedMotion==true||effectOptions07?.Quality==PresentationQuality.Low;
            int papers=kind=="base10"&&!quiet?EffectReserve07("main",EffectPoolKind.Particle,4):0;
            cue.Bind(kind,instance,quiet,from,to,papers);rect.SetAsLastSibling();
            if(follow!=null)enchantTargets071[cue]=follow;
        }
        private void PresentScoringEnchants071(PresentationBeat beat)
        {
            foreach(var e in beat.Events.Where(e=>e.IsScoring&&(e.EffectFamily=="enchantment_base"||e.EffectFamily=="enchantment_multiplier")))
            {
                if(!TryCardBounds07(e.EmitterId,out var card))continue;
                // Stay by the actual top-right enchant badge, clear of the main illustration and exact score.
                var area=new Rect(card.xMax-65,card.yMin+5,60,48);
                if(e.EffectFamily=="enchantment_base")EnchantCue071("base10",e.EmitterId,area,Vector2.zero,Vector2.zero);
                else
                {
                    var factors=FindTransactionRect07("ReceiptFactors");if(factors==null)continue;
                    var destination=EnchantBounds071(factors);var full=new Rect(0,0,1920,1080);
                    Vector2 from=new Vector2(card.center.x-full.center.x,full.center.y-card.yMax);
                    Vector2 to=new Vector2(destination.xMin+24-full.center.x,full.center.y-destination.yMin+5);
                    EnchantCue071("mult2",e.EmitterId,full,from,to);
                }
            }
            // Replay and live presentation consume the same immutable event sources.
            // StartStage uses its separate committed transaction path below.
            foreach(var e in beat.Events.Where(e=>e.EffectFamily=="hand_roll_bonus"))
                foreach(string id in e.SourceEmitterIds)BudgetCue071("roll1",id,"RollBudgetIcon");
        }
        private void PresentBudgetEnchants071(RunState before,RunState after)
        {
            if(after==null)return;
            if(after.stage?.started==true&&(before?.stage?.started!=true||before.stage.stageIndex!=after.stage.stageIndex))
                foreach(string id in after.stage.submissionBonusSourceIds)BudgetCue071("submit1",id,"SubmitBudgetIcon");
            if(after.hand!=null&&after.hand.handId!=before?.hand?.handId)
                foreach(string id in after.hand.rollBonusSourceIds)BudgetCue071("roll1",id,"RollBudgetIcon");
        }
        private void BudgetCue071(string kind,string id,string targetName)
        {
            var target=FindTransactionRect07(targetName);if(target==null)return;
            var bounds=EnchantBounds071(target);
            EnchantCue071(kind,id,new Rect(bounds.center-new Vector2(30,24),new Vector2(60,48)),new Vector2(18,8),Vector2.zero);
        }
        private void PresentAcquiredEnchant071(RunState before,RunState after)
        {
            foreach(var card in after.controllers.Where(c=>c.enchantmentId=="slotless"&&!before.controllers.Any(p=>p.instanceId==c.instanceId)))
            {
                var target=FindTransactionRect07("Controller_"+card.instanceId);if(target==null)continue;
                EnchantCue071("slotless",card.instanceId,EnchantBounds071(target),Vector2.zero,Vector2.zero,target);
            }
        }
        private void TickEnchantCues071(float t)
        {
            foreach(var cue in enchantCues071)if(cue.IsBound)
            {
                if(enchantTargets071.TryGetValue(cue,out var target)&&target!=null)
                {
                    var bounds=EnchantBounds071(target);var rect=(RectTransform)cue.transform;
                    rect.anchoredPosition=new Vector2(bounds.center.x,-bounds.center.y);cue.ResizeExtent(bounds.size);
                }
                cue.SetReducedMotion(reduced||effectOptions07?.ReducedMotion==true||effectOptions07?.Quality==PresentationQuality.Low);cue.Tick(t);
            }
        }
    }
}
