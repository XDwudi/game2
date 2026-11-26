#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Verification;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [Serializable] private sealed class ReleaseSmokeField26
        {
            public string path, text, handId;
        }

        [Serializable] private sealed class ReleaseSmokeFrame26
        {
            public string image;
            public ReleaseSmokeField26[] fields;
            public ReleaseSmokeField26 scoreFocus;
        }

        [Serializable] private sealed class ReleaseSmokeEvidence26
        {
            public string schema, status, theme, rulesVersion;
            public bool isEditor;
            public int stepsSent, traceSteps, screenWidth, screenHeight;
            public string[] images;
            public ReleaseSmokeFrame26[] textFrames;
        }

        // Seed 5000, the first nine actual p1.1.0 release commands. Core independently
        // returns H0001, multiplier 3 and score 198 at sequence nine. No cached trace is required.
        // p1.1.1 (W9a): the same nine commands open evidence/v111/balance/trace_p111_5000.json; the expected hand and score
        // now come from replaying them on a fresh Core session (CoreReplay26) instead of p1.1.0's H0001, x3, 198.
        private const string ReleasePrefix26 = @"{""configVersion"":""p1.1.1"",""seed"":5000,""steps"":[
            {""kind"":""NewRun"",""seed"":5000},{""kind"":""StartStage""},{""kind"":""Roll""},{""kind"":""Roll""},
            {""kind"":""ConfirmLocks"",""dieIds"":[""D002"",""D003"",""D005""]},{""kind"":""Roll""},
            {""kind"":""ConfirmLocks"",""dieIds"":[""D001"",""D004""]},
            {""kind"":""Reorder"",""dieIds"":[""D001"",""D002"",""D003"",""D004"",""D005""]},{""kind"":""Submit""}]}";

        [UnityTest, Timeout(180000)]
        public IEnumerator PLAY_V26_ReleaseNavigationSmokeToFirstCommittedMultiplier()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","artifacts","cache","v11",
                "w26","replay-smoke", Screen.width+"x"+Screen.height,
                IdentityHead20X().Substring(0,8)+"-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ")));
            var host = new GameObject("InactiveReleaseNavigationSmokeDriver");
            host.SetActive(false);
            try
            {
                // Establish a different real opening before asking the shared release route to
                // capture default. Reading the globally active theme alone missed stale menu art.
                app.ApplyTheme11("swiss");
                yield return W11Settle();
                var firstSkin = FindActive<RectTransform>("MenuCardBack0").GetComponent<Image>().sprite;
                Assert.That(firstSkin,Is.SameAs(UiKit.Template("ui_card_controller")));
                var tour = host.AddComponent<Art073CaptureTour>();
                yield return tour.CaptureReleaseNavigationSmoke(app,ReleasePrefix26,root);
                var evidence = JsonUtility.FromJson<ReleaseSmokeEvidence26>(File.ReadAllText(Path.Combine(root,"smoke.json")));
                Assert.That(evidence.schema,Is.EqualTo("v11-editor-navigation-smoke-1"));
                Assert.That(evidence.status,Is.EqualTo("editor-smoke-completed"));
                Assert.That(evidence.isEditor,Is.True);
                Assert.That((evidence.theme,evidence.rulesVersion),Is.EqualTo(("default","p1.1.1")));
                Assert.That((evidence.stepsSent,evidence.traceSteps),Is.EqualTo((8,8)));
                Assert.That((evidence.screenWidth,evidence.screenHeight),Is.EqualTo((Screen.width,Screen.height)));
                Assert.That(firstSkin,Is.Not.SameAs(UiKit.Template("ui_card_controller")));
                var state=app.Session.State;
                var core=CoreReplay26();
                Assert.That((state.seed,state.phase,app.Session.ActionSequence),Is.EqualTo((5000u,core.phase,9L)));
                Assert.That((state.lastSubmission.snapshot.handId,state.lastSubmission.breakdown.score),Is.EqualTo((core.lastSubmission.snapshot.handId,core.lastSubmission.breakdown.score)));
                Assert.That(File.Exists(app.SaveLocation),Is.True);

                string[] pages={"menu","handbook","settings","new-run","stage-preview","HandReady-normal","pause",
                    "Selecting-normal","play-pending","play-locked","play-scoring","score-details","score-steps"};
                var images=pages.Select((page,i)=>i.ToString("00")+"-"+page+".png").ToArray();
                CollectionAssert.AreEqual(images,evidence.images);
                CollectionAssert.AreEqual(images,evidence.textFrames.Select(f=>f.image));
                for(int i=0;i<images.Length;i++)
                {
                    byte[] bytes=File.ReadAllBytes(Path.Combine(root,images[i]));
                    Assert.That(bytes.Length,Is.GreaterThan(24),images[i]);
                    CollectionAssert.AreEqual(new byte[]{137,80,78,71,13,10,26,10},bytes.Take(8),images[i]);
                    int Dimension(int offset)=>bytes[offset]<<24|bytes[offset+1]<<16|bytes[offset+2]<<8|bytes[offset+3];
                    Assert.That((Dimension(16),Dimension(20)),Is.EqualTo((Screen.width,Screen.height)),images[i]);
                    Assert.That(evidence.textFrames[i].fields.Any(f=>!string.IsNullOrWhiteSpace(f.text)),Is.True,images[i]);
                }
                var formula=evidence.textFrames[11];
                var factor=evidence.textFrames[12];
                CheckReleaseScoreFocus26(formula,"ScoreDrawerTable/Viewport/Content/Formula");
                CheckReleaseScoreFocus26(factor,"ScoreDetail/Viewport/Content/GlobalStep1");
                Assert.That(formula.scoreFocus.text,Does.Contain("×").And.Contain("=").And.Contain(PlayerNumberFormat.Exact(core.lastSubmission.breakdown.score)));
                Assert.That(factor.scoreFocus.text,Does.Contain(" × ").And.Contain("→"),"The second step multiplies (p1.1.0 pinned its x3).");
                Assert.That(HasActive("PreviousSubmission"),Is.True,"Real drawer closed before returning to play.");
                Assert.That(HasActive("ScoreDrawerTable")||HasActive("ScoreDetail"),Is.False);
                TestContext.Out.WriteLine("Real UI smoke "+root);
            }
            finally { UnityEngine.Object.Destroy(host); }
        }

        /// <summary>The nine commands of ReleasePrefix26 on a fresh Core session (no UI, no save file): the state the smoke must reach.</summary>
        private RunState CoreReplay26()
        {
            var core=new GameSession(app.Config,new MemorySaveStore());
            void Step(CommandKind kind,params string[] dice)
            {
                if(kind==CommandKind.ConfirmLocks)foreach(string id in dice)Assert.That(core.TogglePending(id),Is.True,id);
                long sequence=core.State?.actionSequence??0;
                var result=core.Execute(new SessionCommand{kind=kind,commandId="smoke-core-"+(sequence+1),expectedActionSequence=sequence,seed=kind==CommandKind.NewRun?5000u:0u,
                    dieIds=kind==CommandKind.Reorder?dice:new string[0]});
                Assert.That(result.accepted,Is.True,kind+": "+result.error);
            }
            Step(CommandKind.NewRun);Step(CommandKind.StartStage);Step(CommandKind.Roll);Step(CommandKind.Roll);
            Step(CommandKind.ConfirmLocks,"D002","D003","D005");Step(CommandKind.Roll);Step(CommandKind.ConfirmLocks,"D001","D004");
            Step(CommandKind.Reorder,"D001","D002","D003","D004","D005");Step(CommandKind.Submit);
            return core.State;
        }

        private static void CheckReleaseScoreFocus26(ReleaseSmokeFrame26 frame,string suffix)
        {
            Assert.That(frame.scoreFocus,Is.Not.Null,frame.image);
            Assert.That(frame.scoreFocus.handId,Is.EqualTo("H0001"),frame.image);
            Assert.That(frame.scoreFocus.path,Does.EndWith(suffix),frame.image);
            var field=frame.fields.Where(f=>f.path==frame.scoreFocus.path).ToArray();
            Assert.That(field.Length,Is.EqualTo(1),frame.image);
            Assert.That(field[0].text,Is.EqualTo(frame.scoreFocus.text),frame.image);
        }
    }
}
#endif
