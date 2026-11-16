using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DiceCraft.Core;
using DiceCraft.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UObj = UnityEngine.Object;

namespace DiceCraft.Tests
{
    /// <summary>
    /// V1.1 task W11 (docs/specs/v11/PLAY_LAYOUT.md): the new play screen. Scenes are built through the real debug-scene
    /// command, then shown by the production renderer; captures hide only the debug-workspace markers
    /// (GameApp.CleanCapture11), so they show what a player sees in an ordinary run.
    /// </summary>
    public sealed partial class GameplayPlayModeTests
    {
        private static readonly string[] W11Cards3={"C39","C139","C34"};
        private static readonly string[] W11Cards7={"C39","C139","C34","C56","C08","C46","C50"};
        private string W11Output
        {
            get
            {
                string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","artifacts","cache","v11","w11",Screen.width+"x"+Screen.height));
                Directory.CreateDirectory(dir);return dir;
            }
        }
        /// <summary>A play-screen scene: pips/colours per die, how many are locked, rolled or not, stage and modifier.</summary>
        private DebugScene W11Scene(string[] cards,int[] pips,string colors,int locked,bool rolled,int stageIndex=3,string modifier="",bool risk=false,string target="4000",string score="0",int items=1,int capacity=5)
        {
            Assert.That(app.UnlockDebug("XD is god"),Is.True);Assert.That(app.EnterDebugWorkspace(),Is.True);
            var s=app.Session.State;var cfg=app.Config;
            var scene=new DebugScene{configVersion=cfg.configVersion,sourceRunId=s.sourceRunId,note="W11 play screen",seed=20261116,diceRngState=20261116,shopRngState=20261117,effectRngState=20261118,
                stageIndex=stageIndex,gold=28,target=target,score=score,capacityOverride=capacity,remainingSubmissions=3,rollsRemaining=rolled?2:3,consumableSlots=3,modifierId=modifier,risk=risk,
                handLevels=cfg.handDefinitions.Select(h=>new HandLevel{handId=h.id,level=0}).ToList(),colorLevels=cfg.colorDefinitions.Select(h=>new HandLevel{handId=h.id,level=0}).ToList()};
            for(int i=0;i<pips.Length;i++)scene.dice.Add(DiceFactory.CreateStandard(cfg,s.nextDieSerial+i));
            for(int i=0;i<cards.Length;i++){int serial=s.nextItemSerial+i;scene.controllers.Add(new ControllerInstance{instanceId="ITEM_"+serial.ToString("D5"),definitionId=cards[i],acquiredSerial=serial,enchantmentId="none",state=new ControllerDynamicState()});}
            string[] itemIds={"I04","I06","I01"};
            for(int i=0;i<items;i++)scene.inventory.Add(new OwnedItem{instanceId="ITEM_"+(s.nextItemSerial+cards.Length+i).ToString("D5"),contentId=itemIds[i%itemIds.Length]});
            if(rolled)
            {
                scene.currentResults=new List<DieHandState>();
                for(int i=0;i<pips.Length;i++)
                {
                    var die=scene.dice[i];var face=die.faces.First(f=>f.permanentPip==pips[i]);face.baseColor=colors[i%colors.Length]=='r'?"red":"blue";
                    bool isLocked=i<locked;
                    scene.currentResults.Add(new DieHandState{dieId=die.dieId,currentFaceId=face.faceId,currentPip=pips[i],currentColor=face.baseColor,hasRolled=true,isLocked=isLocked});
                    if(isLocked)scene.orderedLockedIds.Add(die.dieId);
                }
            }
            else for(int i=0;i<pips.Length;i++){var die=scene.dice[i];foreach(var face in die.faces)face.baseColor=colors[i%colors.Length]=='r'&&face.permanentPip%2==1?"red":face.baseColor;}
            return scene;
        }
        private void W11Apply(DebugScene scene)
        {
            var result=app.Send(new SessionCommand{kind=CommandKind.ApplyDebugScene,debugScene=scene});
            Assert.That(result.accepted,Is.True,result.error);app.SkipVisual();
            if(HasActive("RunDebugScene"))Click("RunDebugScene");
            app.Refresh();Canvas.ForceUpdateCanvases();
        }
        private IEnumerator W11Settle()
        {
            float deadline=Time.realtimeSinceStartup+6;
            while((app.IsBusy||app.PresentationPlaying07||app.table.IsTransitioning)&&Time.realtimeSinceStartup<deadline)yield return null;
            app.table.FinishLayoutTransitions();
            yield return new WaitForSecondsRealtime(.35f);
            Canvas.ForceUpdateCanvases();
        }
        private IEnumerator W11Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                Assert.That(texture.width,Is.EqualTo(Screen.width));
                File.WriteAllBytes(Path.Combine(W11Output,name+".png"),texture.EncodeToPNG());
            }
            finally{UObj.Destroy(texture);}
        }
        private PresentationPlayer W11Player()=>(PresentationPlayer)typeof(GameApp).GetField("presentationPlayer07",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(app);
        private void W11Invoke(string method,params object[] args)=>typeof(GameApp).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(app,args);
        /// <summary>Advances a held presentation (app disabled) to a fraction of the first or last beat of a phase, then draws its frame.</summary>
        private void W11HoldAt(PresentationPhase phase,float fraction,bool lastBeat=false)
        {
            var player=W11Player();Assert.That(player.IsPlaying,Is.True,"Presentation must still be playing.");
            var beats=player.Sequence.Beats.Where(b=>b.Phase==phase).ToArray();Assert.That(beats,Is.Not.Empty,"No "+phase+" beat.");
            var beat=lastBeat?beats.Last():beats.First();double target=beat.StartMs+Math.Max(1,beat.DurationMs*fraction);
            double step=target-player.Clock.ElapsedMilliseconds;if(step>0)player.Tick(step/1000.0);
            W11Invoke("TickScore11");W11Invoke("TickLibraryEffects07",0f);
            Canvas.ForceUpdateCanvases();
        }
        private void W11TextScale(int percent)
        {
            typeof(GameApp).GetField("textScale07",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(app,percent);
            UiKit.FontScale=percent/100f;app.Refresh();Canvas.ForceUpdateCanvases();
        }

        [UnityTest] public IEnumerator PLAY_V11_W11_CheckpointScreensDefaultAndSwiss()
        {
            Assert.That(Screen.width==1920&&Screen.height==1080||Screen.width==1280&&Screen.height==720,Is.True,"Capture at actual 1920x1080 or 1280x720.");
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);int previewValue=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            string savedTheme=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            try
            {
                PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);GameApp.CleanCapture11=true;
                StartRun("71116");
                foreach(string theme in new[]{"default","swiss"})
                {
                    app.ApplyTheme11(theme);Assert.That(app.ThemeId11,Is.EqualTo(theme));
                    foreach(int percent in Screen.width==1280?new[]{100,150}:new[]{100})
                    {
                        W11TextScale(percent);string tag=theme+(percent==100?"":"-text"+percent);
                        // (a) selecting: five dice, three cards; two locked, one selected.
                        W11Apply(W11Scene(W11Cards3,new[]{4,4,6,2,4},"bbbrb",2,true));
                        app.Session.TogglePending(app.Session.State.dice[4].dieId);app.Refresh();
                        yield return W11Settle();yield return W11Capture(tag+"-a-selecting");
                        // (f) ten dice, seven cards; three locked.
                        W11Apply(W11Scene(W11Cards7,new[]{6,6,5,3,1,4,2,6,5,1},"bbrbrbbrbb",3,true,items:3,capacity:8));
                        yield return W11Settle();yield return W11Capture(tag+"-f-ten-dice-seven-cards");
                        if(percent!=100)continue;
                        // (c) boss stage at the start of a hand (stage 9, 徒手).
                        W11Apply(W11Scene(new[]{"C39","C139","C34","C56","C08"},new[]{1,2,3,4,5},"bbrbb",0,false,stageIndex:8,modifier:"B02",target:"13500",score:"0",items:2));
                        yield return W11Settle();yield return W11Capture(tag+"-c-boss-hand-start");
                        // (d) pressure stage, the pressure challenge accepted, selecting.
                        W11Apply(W11Scene(new[]{"C39","C46","C50","C08"},new[]{5,5,3,5,1},"bbrbr",2,true,stageIndex:4,risk:true,target:"3000",score:"1240"));
                        yield return W11Settle();yield return W11Capture(tag+"-d-pressure");
                        // (e) a new stage before any hand is scored.
                        W11Apply(W11Scene(W11Cards3,new[]{1,2,3,4,5},"bbbbb",0,false,stageIndex:6,target:"6000",score:"0"));
                        yield return W11Settle();yield return W11Capture(tag+"-e-new-stage");
                        // (b) mid-scoring: four blue fours submitted; held at the dice, card and formula beats and on the total.
                        W11Apply(W11Scene(new[]{"C39","C139","C34","C08","C46"},new[]{4,4,4,4,2},"bbbbr",4,true,target:"3000",score:"0"));
                        yield return W11Settle();
                        var result=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(result.accepted,Is.True,result.error);
                        bool enabled=app.enabled;app.enabled=false;
                        try
                        {
                            W11HoldAt(PresentationPhase.DiceContributions,.8f);yield return null;yield return W11Capture(tag+"-b1-scoring-dice");
                            W11HoldAt(PresentationPhase.ControllerAndEnchant,.7f,true);yield return null;yield return W11Capture(tag+"-b2-scoring-formula");
                            W11HoldAt(PresentationPhase.FinalScore,.95f);W11Invoke("TickHighScore073",.3f);yield return null;yield return W11Capture(tag+"-b3-scoring-total");
                        }
                        finally{app.enabled=enabled;}
                        app.SkipVisual();yield return W11Settle();yield return W11Capture(tag+"-b4-after-scoring");
                    }
                    W11TextScale(100);
                }
            }
            finally
            {
                GameApp.CleanCapture11=false;app.ApplyTheme11(GameTheme.DefaultId);
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,previewValue);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,savedTheme);PlayerPrefs.Save();
            }
        }
    }
}
