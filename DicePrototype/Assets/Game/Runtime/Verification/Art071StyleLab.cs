using System;
using System.Collections;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using UnityEngine;

namespace DiceCraft
{
    /// <summary>Explicit preview scene only. Uses real commands and a unique private save root.</summary>
    [DefaultExecutionOrder(-20000)]
    public sealed class Art071StyleLab:MonoBehaviour
    {
        private string previousSaveOverride;
        public string SaveRoot {get;private set;}
        public bool Ready {get;private set;}
        private void Awake()
        {
            previousSaveOverride=GameApp.SaveDirectoryOverride;
            SaveRoot=Path.GetFullPath(Path.Combine(Application.dataPath,"../../cache/v071/lab-saves/",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")));
            GameApp.SaveDirectoryOverride=SaveRoot;
        }
        private IEnumerator Start()
        {
            var app=FindAnyObjectByType<GameApp>();
            while(app.Session==null)yield return null;
            app.StartNew("71071");
            if(!app.UnlockDebug("XD is god")||!app.EnterDebugWorkspace())throw new InvalidOperationException("Lab debug clone failed.");
            var state=app.Session.State;
            var scene=Verification.Art07Fixtures.Create(app.Config,Verification.Art07FixtureKind.GoldenSample,state.nextItemSerial,state.nextDieSerial);
            scene.note="v0.71 art lab; isolated demonstration; original gameplay";scene.sourceRunId=state.sourceRunId;
            scene.controllers=scene.controllers.Take(3).ToList();
            string[] ids={"C48","C29","C03"};
            for(int i=0;i<3;i++){scene.controllers[i].definitionId=ids[i];scene.controllers[i].enchantmentId=i==1?"mult2":"none";scene.controllers[i].state=new ControllerDynamicState();}
            scene.inventory=scene.inventory.Take(1).ToList();scene.inventory[0].contentId="I07";
            scene.orderedLockedIds=scene.orderedLockedIds.Take(2).ToList();
            for(int i=0;i<scene.dice.Count;i++){scene.dice[i].isGlass=false;scene.dice[i].lifetime="permanent";scene.dice[i].createdStageId=null;foreach(var f in scene.dice[i].faces){f.isGold=false;f.texture="none";f.baseColor=f.permanentPip==1||f.permanentPip==4?"red":"blue";}var r=scene.currentResults[i];r.isLocked=i<2;r.currentColor=scene.dice[i].faces.First(f=>f.faceId==r.currentFaceId).baseColor;}
            var result=app.Send(new SessionCommand{kind=CommandKind.ApplyDebugScene,debugScene=scene});
            if(!result.accepted||result.pendingSave)throw new InvalidOperationException("Lab scene not committed: "+result.error);
            var close=app.canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true).FirstOrDefault(b=>b.gameObject.activeInHierarchy&&b.name=="RunDebugScene");
            if(close!=null)close.onClick.Invoke();
            app.Session.TogglePending(app.Session.State.dice[2].dieId);app.Refresh();Ready=true;
            Debug.Log("ART071_LAB_READY "+SaveRoot);
        }
        private void OnDestroy(){if(GameApp.SaveDirectoryOverride==SaveRoot)GameApp.SaveDirectoryOverride=previousSaveOverride;}
    }
}

