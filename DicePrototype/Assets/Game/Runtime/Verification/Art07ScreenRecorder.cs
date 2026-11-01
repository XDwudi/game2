using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DiceCraft.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DiceCraft.Verification
{
    public enum Art07ScreenPreset { Normal1080, Small720Font150, Normal1440, Aspect16x10, Ultrawide, Laptop768 }
    [Serializable] public sealed class Art07ScreenOptions
    {
        public string name = "Normal1080";
        public int width = 1920, height = 1080, fontPercent = 100;
        public uint seed = 20261031;
        public float settleSeconds = .35f;
        public static Art07ScreenOptions For(Art07ScreenPreset preset)
        {
            var o = new Art07ScreenOptions { name = preset.ToString() };
            switch(preset)
            {
                case Art07ScreenPreset.Normal1080: break;
                case Art07ScreenPreset.Small720Font150: o.width=1280;o.height=720;o.fontPercent=150;break;
                case Art07ScreenPreset.Normal1440: o.width=2560;o.height=1440;break;
                case Art07ScreenPreset.Aspect16x10: o.width=2560;o.height=1600;break;
                case Art07ScreenPreset.Ultrawide: o.width=3440;o.height=1440;break;
                case Art07ScreenPreset.Laptop768: o.width=1366;o.height=768;break;
                default: throw new ArgumentOutOfRangeException(nameof(preset));
            }
            return o;
        }
    }

    /// <summary>Explicit isolated screenshot capture; public actions only, never automatic installation or acceptance.</summary>
    public sealed class Art07ScreenRecorder : MonoBehaviour
    {
        [Serializable] private sealed class Preference { public string key; public bool existed; public int value; }
        [Serializable] private sealed class ActionRow
        {
            public string action,beforeState,afterState,beforeSave,afterSave;
            public bool allowsRuleMutation,unchanged;
        }
        [Serializable] private sealed class ControlRow
        {
            public string path,name,kind,text;
            public Rect bounds,visibleBounds;
            public bool interactable,centerRaycastHits,visible,hit40Physical,textOverflows;
            public float textHeight,preferredTextHeight,textWidth,preferredTextWidth;
        }
        [Serializable] private sealed class ScreenRow
        {
            public string id,specScreen,scope,file,pngSha256,stateHashBefore,stateHashAfter,saveShaBefore,saveShaAfter,normalSaveSha;
            public string phase,runId,savePath,unityVersion,artVersion,rulesVersion,screenMode;
            public int unityFrame,width,height,dice,capacity,controllers,inventory,controllerQuotes,consumableQuotes,pendingDice;
            public float fontScale,canvasScale;
            public bool reducedMotion;
            public int qualityLevel;
            public bool debugWorkspace,stateAndSaveUnchanged,contentValid;
            public double nonBlackRatio,brightRatio;
            public int quantizedColors;
            public ControlRow[] controls;
            public TextRow[] textRows;
            public string[] debugCandidateIndicators;
        }
        [Serializable] private sealed class TextRow
        {
            public string path,name,text;
            public Rect bounds,visibleBounds;
            public int layoutCharacters,nonWhitespaceCharacters,renderedGlyphs,fullyVisibleGlyphs;
            public bool overflow,truncated,rendererCulled;
        }
        [Serializable] private sealed class Coverage { public string screen,status,scope; }
        [Serializable] private sealed class Report
        {
            public string schema="art07-screen-recording-1",status="running",error,startedUtc,endedUtc,outputDirectory;
            public string artVersion,rulesVersion,unityVersion,buildGuid,buildIdentity,runtimeMvid,coreMvid,recorderSourceSha256,configSha256;
            public string platform,os,gpu,graphicsApi,saveDirectory,normalSaveBeforeDebug,normalSaveAfterDebug;
            public string method="Public GameApp/debug commands and raycast-checked UI actions; composited screenshot after WaitForEndOfFrame";
            public string acceptance="Capture evidence only; no visual, performance, platform or complete-screen acceptance inferred";
            public bool editor,batchMode,preferencesRestored,ordinaryCheckpointPreserved;
            public Art07ScreenOptions requested;
            public Preference[] priorPreferences;
            public List<ScreenRow> screens=new List<ScreenRow>();
            public List<ActionRow> actions=new List<ActionRow>();
            public List<Coverage> coverage=new List<Coverage>();
        }
        private static Art07ScreenRecorder active;
        private static readonly string[] PreferenceKeys={"art07.textScale","shopTab","art07.guideSeen",GameApp.ZeroRollHintKey07,GameApp.RandomHintKey07,GameApp.QualifiedHintKey07};
        private GameApp app;
        private Report report;
        private Coroutine routine;
        private string normalSavePath;
        private float originalFontScale;
        private double lastProgress;
        private readonly List<RaycastResult> raycasts=new List<RaycastResult>();
        public bool IsRecording=>report!=null&&report.status=="running";
        public string Status=>report?.status??"not_started";
        public string Error=>report?.error;
        public string OutputDirectory=>report?.outputDirectory;

        public static Art07ScreenRecorder Begin(GameApp owner,string absoluteOutputDirectory,Art07ScreenPreset preset=Art07ScreenPreset.Normal1080)
            =>Begin(owner,absoluteOutputDirectory,Art07ScreenOptions.For(preset));
        public static Art07ScreenRecorder Begin(GameApp owner,string absoluteOutputDirectory,Art07ScreenOptions options)
        {
            if(!Application.isPlaying||owner==null||!owner.StartupReady07||owner.Session==null||owner.canvas==null)throw new InvalidOperationException("Use a running GameApp.");
            if(active!=null&&active.IsRecording||FindObjectsByType<Art07VisualRecorder>().Any(x=>x.IsRecording))throw new InvalidOperationException("Serialize screenshot and video recorders.");
            if(owner.IsBusy||owner.Session.HasPendingSave||owner.Session.State!=null||owner.DebugUnlocked)throw new InvalidOperationException("Begin at a fresh main menu with an empty isolated Session and locked debug entry, before NewRun.");
            if(options==null||options.width<640||options.height<480||!new[]{100,125,150}.Contains(options.fontPercent)||options.settleSeconds<.2f||options.settleSeconds>2f)throw new ArgumentException("Invalid dimensions/font/settling time.");
            if(Screen.width!=options.width||Screen.height!=options.height)throw new InvalidOperationException("Set actual Game view/Player dimensions before Begin: "+options.width+"x"+options.height+"; observed "+Screen.width+"x"+Screen.height);
            if(string.IsNullOrWhiteSpace(GameApp.SaveDirectoryOverride)||!Path.IsPathRooted(GameApp.SaveDirectoryOverride))throw new InvalidOperationException("Set unique absolute SaveDirectoryOverride before GameApp.Awake.");
            string saveRoot=Path.GetFullPath(GameApp.SaveDirectoryOverride);
            if(!Within(owner.SaveLocation,saveRoot)||Within(saveRoot,Application.persistentDataPath))throw new InvalidOperationException("App must use isolated override outside user storage.");
            if(File.Exists(Path.Combine(saveRoot,"active-run.json"))||File.Exists(Path.Combine(saveRoot,"debug-run.json")))throw new InvalidOperationException("Use fresh isolated saves; existing saves are not overwritten.");
            if(string.IsNullOrWhiteSpace(absoluteOutputDirectory)||!Path.IsPathRooted(absoluteOutputDirectory))throw new ArgumentException("Use absolute output path.");
            string output=Path.GetFullPath(absoluteOutputDirectory);
            if(Within(output,Application.dataPath)||Within(output,saveRoot)||Within(saveRoot,output))throw new ArgumentException("Keep evidence outside Assets and separate from saves.");
            if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())throw new IOException("Output must be new or empty.");
            Directory.CreateDirectory(output);Directory.CreateDirectory(Path.Combine(output,"screens"));
            var recorder=new GameObject("Art07ScreenRecorder_Explicit").AddComponent<Art07ScreenRecorder>();
            recorder.app=owner;recorder.originalFontScale=UiKit.FontScale;recorder.normalSavePath=Path.Combine(saveRoot,"active-run.json");recorder.lastProgress=Time.realtimeSinceStartupAsDouble;
            recorder.report=new Report{outputDirectory=output,requested=JsonUtility.FromJson<Art07ScreenOptions>(JsonUtility.ToJson(options)),startedUtc=DateTime.UtcNow.ToString("O"),
                artVersion=GameApp.ArtVersion,rulesVersion=owner.Config.configVersion,unityVersion=Application.unityVersion,buildGuid=Application.buildGUID,buildIdentity=Resources.Load<TextAsset>("BuildIdentity")?.text,
                runtimeMvid=typeof(GameApp).Module.ModuleVersionId.ToString(),coreMvid=typeof(GameSession).Module.ModuleVersionId.ToString(),recorderSourceSha256=HashFile(Path.Combine(Application.dataPath,"Game/Runtime/Verification/Art07ScreenRecorder.cs")),configSha256=Hash(Encoding.UTF8.GetBytes(owner.configAsset.text)),
                platform=Application.platform.ToString(),os=SystemInfo.operatingSystem,gpu=SystemInfo.graphicsDeviceName,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),editor=Application.isEditor,batchMode=Application.isBatchMode,saveDirectory=saveRoot,
                priorPreferences=PreferenceKeys.Select(k=>new Preference{key=k,existed=PlayerPrefs.HasKey(k),value=PlayerPrefs.GetInt(k,0)}).ToArray()};
            for(int i=1;i<=22;i++)recorder.report.coverage.Add(new Coverage{screen="S"+i.ToString("D2"),status="not_captured",scope="No evidence yet; not a waiver."});
            active=recorder;recorder.WriteReport();recorder.routine=recorder.StartCoroutine(recorder.Guarded(recorder.Record()));return recorder;
        }

        private IEnumerator Record()
        {
            yield return Press("MenuSettings");yield return Press("SettingsTab2");
            for(int i=0;Mathf.RoundToInt(UiKit.FontScale*100)!=report.requested.fontPercent&&i<3;i++)yield return Press("TextScale");
            if(Mathf.RoundToInt(UiKit.FontScale*100)!=report.requested.fontPercent)throw new InvalidOperationException("Requested text scale not applied.");
            yield return Press("SettingsDone");yield return Capture("menu-empty","S01","Fresh ordinary menu; no previous run.");
            yield return Press("NewRun");yield return Capture("new-run-seed","S02","Actual seed dialog; no run created yet.");
            yield return Press("RandomSeed");
            if(!GameRng.TryParseSeed(Find<TMP_InputField>("SeedInput").text,out _))throw new InvalidOperationException("Random seed control did not fill a valid uint seed.");
            yield return Capture("new-run-random-seed","S02","Actual random-seed UI; ordinary state and save unchanged.");
            var seed=Find<TMP_InputField>("SeedInput");seed.text=report.requested.seed.ToString();seed.DeactivateInputField();EventSystem.current.SetSelectedGameObject(null);
            yield return Press("StartNew",true);yield return Capture("stage-preview","S03","Actual ordinary stage-one preview; risk variant untested.");
            yield return Press("StartStage",true);yield return Capture("before-first-roll","S04","Actual ordinary five-die hand.");
            if(TryFind<Button>("DismissGuide")!=null){yield return Capture("first-run-guide","S19","Actual first-step overlay only, not full tutorial.");yield return Press("DismissGuide");}
            yield return Press("Roll",true);yield return Capture("rolled-unselected","S05","Actual RNG result before selection.");
            string firstDie=app.Session.State.dice[0].dieId;
            yield return ClickDie(firstDie,false);yield return Capture("pending-die","S05","One pending die; committed checkpoint unchanged.");
            yield return Press("Confirm",true);yield return Capture("confirmed-lock","S06","Actual lock; drag/exhausted-roll branches untested.");
            yield return Press("Pause");yield return Capture("pause","S16","Stable ordinary pause, not mid-animation.");yield return Press("Settings");
            for(int i=0;i<3;i++)
            {
                yield return Press("SettingsTab"+i);yield return Capture("settings-tab-"+i+"-top","S17","Settings page "+i+"; no native display transition.");
                if(i==2)
                {
                    yield return SettingsBottom();yield return Capture("settings-assist-bottom","S17","Bottom of real auxiliary settings form.");
                    var password=Find<TMP_InputField>("DebugPassword");password.text="XD is god";password.DeactivateInputField();EventSystem.current.SetSelectedGameObject(null);
                    yield return Capture("debug-password","S20","Actual password field before unlocking; ordinary run unchanged.");
                    yield return Press("UnlockDebug");
                    if(!app.DebugUnlocked||app.IsDebugWorkspace)throw new InvalidOperationException("Password entry must unlock UI without moving the ordinary run into debug.");
                    yield return Capture("debug-entry","S20","Actual password handler opened isolated-workspace choices; no copy applied yet.");
                    yield return Press("CloseDialog");
                }
            }
            yield return Press("Handbook");yield return Capture("handbook-hands","S14","Actual structure handbook.");
            yield return Press("HandbookColors");yield return Capture("handbook-colors","S14","Actual independent colors/red groups.");yield return Press("CloseDialog");
            yield return ClickDie(firstDie,true);
            foreach(string face in new[]{"F01","F02","F03","F04","F05","F06"}){yield return Press("Face_"+face);yield return Capture("dice-face-"+face,"S12","Real physical face selected; permanent/current/effective detail.");}
            yield return Press("ProbabilityGroups");yield return Capture("dice-probability-expanded","S12","Alternate real probability grouping, no edits.");yield return Press("CloseDialog");
            report.normalSaveBeforeDebug=HashFile(normalSavePath);
            yield return ApplyFixture(Art07FixtureKind.MaximumPool,false,false);yield return Capture("maximum-pool","S04","Explicit debug fixture: 10 dice/8 slots/10 controllers/5 items; not normal progression.");
            yield return ApplyFixture(Art07FixtureKind.MaximumTable,true,false);yield return Capture("maximum-locked-table","S06","Explicit fixture: eight locked/two pool; debug target 1, initial score 0.");
            yield return Press("Factor0");yield return Capture("score-preview-detail","S07","Actual calculator preview.");
            string clipboard=GUIUtility.systemCopyBuffer;
            try
            {
                string exact=Find<TMP_Text>("Formula").text;yield return Press("CopyExactScore");
                if(GUIUtility.systemCopyBuffer!=exact)throw new InvalidOperationException("Copied score/formula differs from the complete precise readout.");
                yield return Capture("score-preview-copied","S07","Actual copy button; complete exact formula copied, previous clipboard restored afterward.");
            }
            finally{GUIUtility.systemCopyBuffer=clipboard;}
            yield return Press("CloseDialog");
            var state=app.Session.State;
            yield return Press("Controller_"+state.controllers[0].instanceId);yield return Capture("controller-detail","S15","Actual first equipped controller.");yield return Press("CloseDialog");
            yield return Press("Item_"+state.inventory[0].instanceId);yield return Capture("item-detail","S15","Actual held item; not used.");yield return Press("CloseDialog");
            yield return Press("Submit",true);yield return Capture("after-real-submit","S08","Committed fixture result only; continuous animation belongs to video recorder.");
            if(app.Session.State.lastSubmission==null||app.Session.State.lastSubmission.breakdown.score=="0")throw new InvalidOperationException("No real positive submission for history/shop.");
            yield return Press("PreviousSubmission");yield return Capture("history-detail","S07","Historical detail from preceding real Submit receipt.");yield return Press("CloseDialog");
            if(app.Session.State.phase!="StageSettlement")yield return Press("EndStage",true);
            yield return Capture("stage-settlement","S09","Real settlement after debug target met by Submit; not normal balance evidence.");yield return Press("SettlementContinue",true);
            state=app.Session.State;
            if(state.phase!="Shop"||state.shop.offers.Count(o=>o.category=="controller")!=6||state.shop.offers.Count(o=>o.category=="consumable")!=5)throw new InvalidOperationException("Expanded shop did not generate six/five quotes.");
            for(int i=0;i<5;i++){yield return Press("Tab"+i);yield return Capture("shop-tab-"+i,"S10","Actual generated shop; fixed six controller quotes, lower tab "+i+".");}
            yield return ApplyFixture(Art07FixtureKind.Debug32,false,true);yield return Capture("debug32-table","S21","Explicit 32 dice/controllers/items/capacity; real table pagination.");
            yield return Press("DebugWorkspace");yield return Press("ReloadDebugDraft");
            foreach(string tab in new[]{"DebugTab0","DebugTab3","DebugTab1","DebugNav3","DebugTab2","DebugNav5","DebugTab4"})
            {yield return Press(tab);ValidateDebug32Draft();yield return Capture("debug32-"+tab,"S21","Actual editor after re-reading applied 32-member scene; page top/selected anchor, not all 32 rows.");}
            yield return Press("ReturnNormal",true);report.normalSaveAfterDebug=HashFile(normalSavePath);report.ordinaryCheckpointPreserved=report.normalSaveBeforeDebug==report.normalSaveAfterDebug;
            if(!report.ordinaryCheckpointPreserved)throw new InvalidOperationException("Debug capture changed ordinary checkpoint.");
            yield return Press("Pause");yield return Press("MainMenu");yield return Capture("menu-continue","S01","Ordinary checkpoint retained after debug return.");
            yield return Press("NewRun");yield return Capture("new-run-overwrite-summary","S02","Existing ordinary progress listed before replacement; cancel preserves checkpoint.");yield return Press("CloseDialog");
        }
        private IEnumerator ApplyFixture(Art07FixtureKind kind,bool expandedShop,bool all32)
        {
            yield return Idle();string before=StateHash(),save=HashFile(app.SaveLocation);
            if(!app.DebugUnlocked||!app.EnterDebugWorkspace())throw new InvalidOperationException("Actual password UI must unlock debug before applying fixtures.");
            var state=app.Session.State;var scene=Art07Fixtures.Create(app.Config,kind,state.nextItemSerial,state.nextDieSerial);scene.sourceRunId=state.sourceRunId;
            if(expandedShop){scene.target="1";scene.score="0";scene.controllerOfferPurchases=3;scene.consumableOfferPurchases=3;scene.note+="; expanded-shop capture; debug target=1, initial score=0";}
            if(all32){scene.consumableSlots=32;for(int i=scene.inventory.Count;i<32;i++)scene.inventory.Add(new OwnedItem{instanceId="ITEM_"+(state.nextItemSerial+64+i).ToString("D5"),contentId="I"+(i%7+1).ToString("D2")});}
            File.WriteAllText(Path.Combine(report.outputDirectory,"fixture-"+kind+".json"),JsonUtility.ToJson(scene,true));
            var result=app.Send(new SessionCommand{kind=CommandKind.ApplyDebugScene,debugScene=scene});
            if(!result.accepted||result.pendingSave)throw new InvalidOperationException("Fixture rejected: "+result.error);
            yield return Idle();yield return Press("ReloadDebugDraft");yield return Press("RunDebugScene");Art07Fixtures.Verify(kind,app.Session.State,app.Session.PreviewScoreRange());
            AddAction("Public debug fixture "+kind,before,save,true);
            File.WriteAllText(Path.Combine(report.outputDirectory,"fixture-"+kind+"-applied.json"),JsonUtility.ToJson(app.Session.State,true));
        }
        private IEnumerator Press(string name,bool mutation=false)
        {
            yield return Idle();var button=Find<Button>(name);yield return Reveal(button.transform as RectTransform);button=Find<Button>(name);
            if(!button.IsInteractable()||!Hit(button.gameObject,Bounds(button.transform as RectTransform).center))throw new InvalidOperationException("Disabled/covered UI action: "+name);
            string before=StateHash(),save=HashFile(app.SaveLocation);button.onClick.Invoke();yield return Idle();AddAction("Button "+name,before,save,mutation);
        }
        private void ValidateDebug32Draft()
        {
            var state=app.Session.State;
            if(state.dice.Count!=32||state.controllers.Count!=32||state.inventory.Count!=32)throw new InvalidOperationException("Applied Debug32 counts changed.");
            var controllers=TryFind<TextMeshProUGUI>("ControllerCount");
            if(controllers!=null&&!controllers.text.StartsWith("控制器 32 / 32",StringComparison.Ordinal))throw new InvalidOperationException("Stale controller candidate form: "+controllers.text);
            var dice=TryFind<TextMeshProUGUI>("DiceCount");if(dice!=null&&dice.text!="32 / 32")throw new InvalidOperationException("Stale dice candidate form: "+dice.text);
            var items=TryFind<TextMeshProUGUI>("ItemsDebug");if(items!=null&&!items.text.Contains("32 / 32"))throw new InvalidOperationException("Stale item candidate form: "+items.text);
            var face=TryFind<Button>("CurrentFace");var pip=TryFind<Button>("CurrentPip");
            var first=state.hand.currentResults.Single(r=>r.dieId==state.dice[0].dieId);
            if(face!=null&&face.GetComponentInChildren<TextMeshProUGUI>().text!="当前面："+first.currentFaceId)throw new InvalidOperationException("Stale current-face candidate.");
            if(pip!=null&&pip.GetComponentInChildren<TextMeshProUGUI>().text!="本次点数："+first.currentPip)throw new InvalidOperationException("Stale current-pip candidate.");
        }
        private IEnumerator ClickDie(string id,bool right)
        {
            yield return Idle();var pointer=Find<DiePointer>("Die_"+id);var center=Bounds(pointer.transform as RectTransform).center;
            if(!Hit(pointer.gameObject,center))throw new InvalidOperationException("Covered die target: "+id);
            string before=StateHash(),save=HashFile(app.SaveLocation);
            ExecuteEvents.Execute(pointer.gameObject,new PointerEventData(EventSystem.current){position=center,button=right?PointerEventData.InputButton.Right:PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
            yield return Idle();AddAction("Pointer "+(right?"right ":"left ")+id,before,save,false);
        }
        private void AddAction(string action,string before,string save,bool mutation)
        {
            var row=new ActionRow{action=action,beforeState=before,afterState=StateHash(),beforeSave=save,afterSave=HashFile(app.SaveLocation),allowsRuleMutation=mutation};
            row.unchanged=row.beforeState==row.afterState&&row.beforeSave==row.afterSave;report.actions.Add(row);WriteReport();lastProgress=Time.realtimeSinceStartupAsDouble;
            if(!mutation&&!row.unchanged)throw new InvalidOperationException("Read-only UI changed rule state/save: "+action);
        }
        private IEnumerator SettingsBottom()
        {
            var parent=Find<Button>("SettingsDone").transform.parent;
            var scrolls=app.canvas.GetComponentsInChildren<ScrollRect>(false).Where(s=>s.transform.IsChildOf(parent)).ToArray();
            if(scrolls.Length!=1)throw new InvalidOperationException("Expected one active settings scroll.");scrolls[0].verticalNormalizedPosition=0;yield return Idle();
        }
        private IEnumerator Reveal(RectTransform target)
        {
            foreach(var scroll in target.GetComponentsInParent<ScrollRect>(false))
            {
                Canvas.ForceUpdateCanvases();var viewport=scroll.viewport!=null?scroll.viewport:scroll.transform as RectTransform;
                Rect v=Bounds(viewport),t=Bounds(target),content=Bounds(scroll.content);
                if(scroll.vertical&&content.height>v.height&&(t.yMin<v.yMin||t.yMax>v.yMax))scroll.verticalNormalizedPosition=Mathf.Clamp01(scroll.verticalNormalizedPosition+(t.center.y-v.center.y)/(content.height-v.height));
                if(scroll.horizontal&&content.width>v.width&&(t.xMin<v.xMin||t.xMax>v.xMax))scroll.horizontalNormalizedPosition=Mathf.Clamp01(scroll.horizontalNormalizedPosition+(t.center.x-v.center.x)/(content.width-v.width));
                yield return null;
            }
            Canvas.ForceUpdateCanvases();yield return null;
        }
        private IEnumerator Idle()
        {
            double start=Time.realtimeSinceStartupAsDouble;
            while(app.IsBusy||app.PresentationPlaying07){if(Time.realtimeSinceStartupAsDouble-start>40)throw new TimeoutException("Presentation did not become idle.");yield return null;}
            if(app.Session.HasPendingSave)throw new IOException("Actual save failed; no fabricated recovery screen is generated.");
            Canvas.ForceUpdateCanvases();double until=Time.realtimeSinceStartupAsDouble+report.requested.settleSeconds;
            while(Time.realtimeSinceStartupAsDouble<until)yield return null;
            if(Screen.width!=report.requested.width||Screen.height!=report.requested.height)throw new InvalidOperationException("Resolution changed during this recording.");
        }
        private IEnumerator Capture(string id,string specScreen,string scope)
        {
            yield return Idle();string before=StateHash(),save=HashFile(app.SaveLocation);yield return new WaitForEndOfFrame();var state=app.Session.State;
            var row=new ScreenRow{id=id,specScreen=specScreen,scope=scope,file="screens/"+report.screens.Count.ToString("D3")+"-"+id+".png",
                unityFrame=Time.frameCount,width=Screen.width,height=Screen.height,fontScale=UiKit.FontScale,canvasScale=app.canvas.scaleFactor,
                unityVersion=Application.unityVersion,artVersion=GameApp.ArtVersion,rulesVersion=app.Config.configVersion,screenMode=Screen.fullScreenMode.ToString(),reducedMotion=UiKit.ReducedMotion,qualityLevel=QualitySettings.GetQualityLevel(),stateHashBefore=before,saveShaBefore=save,savePath=app.SaveLocation,normalSaveSha=HashFile(normalSavePath),phase=state?.phase??"MainMenu",runId=state?.runId,
                dice=state?.dice.Count??0,controllers=state?.controllers.Count??0,inventory=state?.inventory.Count??0,capacity=state?.stage.capacity??0,pendingDice=app.Session.PendingIds.Length,
                controllerQuotes=state?.shop?.offers.Count(o=>o.category=="controller")??0,consumableQuotes=state?.shop?.offers.Count(o=>o.category=="consumable")??0,debugWorkspace=state?.debugWorkspace==true,controls=Controls(),textRows=TextRows(),
                debugCandidateIndicators=new[]{"ControllerCount","DiceCount","ItemsDebug","OrderedLocks"}.Select(name=>TryFind<TextMeshProUGUI>(name)).Where(label=>label!=null).Select(label=>label.name+"="+label.text)
                    .Concat(new[]{"CurrentFace","CurrentPip"}.Select(name=>TryFind<Button>(name)).Where(button=>button!=null).Select(button=>button.name+"="+button.GetComponentInChildren<TextMeshProUGUI>().text)).ToArray()};
            Texture2D texture=null;
            try
            {
                texture=ScreenCapture.CaptureScreenshotAsTexture();if(texture==null||texture.width!=row.width||texture.height!=row.height)throw new IOException("No full-size screenshot.");
                var pixels=texture.GetPixels32();var colors=new HashSet<int>();int sampled=0,lit=0,bright=0;
                for(int i=0;i<pixels.Length;i+=Math.Max(1,pixels.Length/16384)){var p=pixels[i];sampled++;int maximum=Math.Max(p.r,Math.Max(p.g,p.b));if(maximum>12)lit++;if(maximum>96)bright++;colors.Add((p.r>>4)<<8|(p.g>>4)<<4|p.b>>4);}
                row.nonBlackRatio=lit/(double)sampled;row.brightRatio=bright/(double)sampled;row.quantizedColors=colors.Count;row.contentValid=row.nonBlackRatio>=.10&&row.brightRatio>=.005&&colors.Count>=8;
                byte[] png=texture.EncodeToPNG();if(png==null||png.Length<64)throw new IOException("Empty screenshot PNG.");
                string path=Path.Combine(report.outputDirectory,row.file);File.WriteAllBytes(path,png);row.pngSha256=Hash(png);
                row.stateHashAfter=StateHash();row.saveShaAfter=HashFile(app.SaveLocation);row.stateAndSaveUnchanged=before==row.stateHashAfter&&save==row.saveShaAfter;
                report.screens.Add(row);var coverage=report.coverage.Single(c=>c.screen==specScreen);coverage.status="partial_capture_pending_review";coverage.scope=string.Join(" | ",report.screens.Where(s=>s.specScreen==specScreen).Select(s=>s.id));
                File.WriteAllText(Path.Combine(report.outputDirectory,row.file+".json"),JsonUtility.ToJson(row,true));WriteReport();lastProgress=Time.realtimeSinceStartupAsDouble;
                if(!row.contentValid||!row.stateAndSaveUnchanged||HashFile(path)!=row.pngSha256)throw new InvalidOperationException("Capture integrity failed; partial evidence retained: "+id);
            }
            finally{if(texture!=null)Destroy(texture);}
        }
        private ControlRow[] Controls()
        {
            return app.canvas.GetComponentsInChildren<Selectable>(false).Select(control=>
            {
                var rect=control.transform as RectTransform;Rect bounds=Bounds(rect),visible=Intersection(bounds,new Rect(0,0,Screen.width,Screen.height));
                foreach(var mask in control.GetComponentsInParent<RectMask2D>(false))if(mask.isActiveAndEnabled)visible=Intersection(visible,Bounds(mask.transform as RectTransform));
                foreach(var mask in control.GetComponentsInParent<Mask>(false))if(mask.isActiveAndEnabled)visible=Intersection(visible,Bounds(mask.transform as RectTransform));
                var text=control.GetComponentInChildren<TextMeshProUGUI>(false);if(text!=null)text.ForceMeshUpdate();
                return new ControlRow{path=Hierarchy(control.transform),name=control.name,kind=control.GetType().Name,text=text?.text,bounds=bounds,visibleBounds=visible,
                    interactable=control.IsInteractable(),visible=visible.width>0&&visible.height>0,centerRaycastHits=Hit(control.gameObject,bounds.center),hit40Physical=visible.width>=39.99f&&visible.height>=39.99f,textOverflows=text!=null&&text.isTextOverflowing,
                    textHeight=text?.rectTransform.rect.height??0,preferredTextHeight=text?.preferredHeight??0,textWidth=text?.rectTransform.rect.width??0,preferredTextWidth=text?.preferredWidth??0};
            }).ToArray();
        }
        private TextRow[] TextRows()
        {
            Camera camera=app.canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:app.canvas.worldCamera;
            return app.canvas.GetComponentsInChildren<TextMeshProUGUI>(false).Where(t=>!string.IsNullOrWhiteSpace(t.text)).Select(t=>
            {
                t.ForceMeshUpdate();Rect visible=Intersection(Bounds(t.rectTransform),new Rect(0,0,Screen.width,Screen.height));
                foreach(var mask in t.GetComponentsInParent<RectMask2D>(false))if(mask.isActiveAndEnabled)visible=Intersection(visible,Bounds((RectTransform)mask.transform));
                foreach(var mask in t.GetComponentsInParent<Mask>(false))if(mask.isActiveAndEnabled)visible=Intersection(visible,Bounds((RectTransform)mask.transform));
                var row=new TextRow{path=Hierarchy(t.transform),name=t.name,text=t.text,bounds=Bounds(t.rectTransform),visibleBounds=visible,
                    layoutCharacters=t.textInfo.characterCount,overflow=t.isTextOverflowing,truncated=t.isTextTruncated,rendererCulled=t.canvasRenderer.cull};
                for(int i=0;i<t.textInfo.characterCount;i++)
                {
                    var ch=t.textInfo.characterInfo[i];if(char.IsWhiteSpace(ch.character))continue;row.nonWhitespaceCharacters++;
                    if(!ch.isVisible)continue;row.renderedGlyphs++;
                    var a=RectTransformUtility.WorldToScreenPoint(camera,t.transform.TransformPoint(ch.bottomLeft));
                    var b=RectTransformUtility.WorldToScreenPoint(camera,t.transform.TransformPoint(ch.topRight));
                    if(a.x>=visible.xMin-.1f&&b.x<=visible.xMax+.1f&&a.y>=visible.yMin-.1f&&b.y<=visible.yMax+.1f)row.fullyVisibleGlyphs++;
                }
                return row;
            }).ToArray();
        }
        private bool Hit(GameObject target,Vector2 point)
        {
            if(EventSystem.current==null)return false;raycasts.Clear();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},raycasts);
            var first=raycasts.FirstOrDefault(r=>r.gameObject!=null).gameObject;return first!=null&&(first==target||first.transform.IsChildOf(target.transform));
        }
        private Rect Bounds(RectTransform target)
        {
            if(target==null)return Rect.zero;var corners=new Vector3[4];target.GetWorldCorners(corners);Camera camera=app.canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:app.canvas.worldCamera;
            var points=corners.Select(p=>RectTransformUtility.WorldToScreenPoint(camera,p)).ToArray();return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
        }
        private static Rect Intersection(Rect a,Rect b)=>Rect.MinMaxRect(Math.Max(a.xMin,b.xMin),Math.Max(a.yMin,b.yMin),Math.Max(Math.Max(a.xMin,b.xMin),Math.Min(a.xMax,b.xMax)),Math.Max(Math.Max(a.yMin,b.yMin),Math.Min(a.yMax,b.yMax)));
        private T TryFind<T>(string name)where T:Component=>app.canvas.GetComponentsInChildren<T>(false).FirstOrDefault(x=>x.name==name);
        private T Find<T>(string name)where T:Component
        {
            var found=app.canvas.GetComponentsInChildren<T>(false).Where(x=>x.name==name).ToArray();if(found.Length!=1)throw new InvalidOperationException("Expected one active "+typeof(T).Name+" "+name+", found "+found.Length);return found[0];
        }
        private string StateHash()=>app.Session.State==null?"no-run":GameSession.StateHash(app.Session.State);
        private static string Hierarchy(Transform target){var names=new List<string>();for(var t=target;t!=null;t=t.parent)names.Add(t.name);names.Reverse();return string.Join("/",names);}
        private static bool Within(string path,string root){string p=Path.GetFullPath(path),r=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);return p.Equals(r,StringComparison.OrdinalIgnoreCase)||p.StartsWith(r+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
        private static string HashFile(string path)=>File.Exists(path)?Hash(File.ReadAllBytes(path)):"missing";
        private static string Hash(byte[] bytes){using(var sha=SHA256.Create())return string.Concat(sha.ComputeHash(bytes).Select(x=>x.ToString("x2")));}
        private void WriteReport()=>File.WriteAllText(Path.Combine(report.outputDirectory,"screen-report.json"),JsonUtility.ToJson(report,true));
        private IEnumerator Guarded(IEnumerator body)
        {
            var stack=new Stack<IEnumerator>();stack.Push(body);
            while(IsRecording&&stack.Count>0)
            {
                object current=null;bool more;
                try{more=stack.Peek().MoveNext();if(more)current=stack.Peek().Current;else stack.Pop();}
                catch(Exception ex){Finish("failed",ex.ToString());yield break;}
                if(!more)continue;if(current is IEnumerator nested)stack.Push(nested);else yield return current;
            }
            if(IsRecording)Finish("captured_pending_visual_review",null);
        }
        public void Cancel(){if(IsRecording){if(routine!=null)StopCoroutine(routine);Finish("cancelled","Explicit cancellation; partial evidence retained.");}}
        private void Update(){if(IsRecording&&Time.realtimeSinceStartupAsDouble-lastProgress>90){if(routine!=null)StopCoroutine(routine);Finish("failed","No progress for 90 seconds. Check Game view rendering/UI transitions.");}}
        private void Finish(string status,string error)
        {
            if(!IsRecording)return;report.status=status;report.error=error;report.endedUtc=DateTime.UtcNow.ToString("O");
            foreach(var pref in report.priorPreferences){if(pref.existed)PlayerPrefs.SetInt(pref.key,pref.value);else PlayerPrefs.DeleteKey(pref.key);}PlayerPrefs.Save();UiKit.FontScale=originalFontScale;report.preferencesRestored=true;
            report.normalSaveAfterDebug=HashFile(normalSavePath);report.ordinaryCheckpointPreserved=report.normalSaveBeforeDebug!=null&&report.normalSaveBeforeDebug==report.normalSaveAfterDebug;
            WriteReport();if(active==this)active=null;Debug.Log("ART07_SCREEN_CAPTURE "+status+" "+report.outputDirectory+(error==null?"":" "+error));
        }
        private void OnDestroy(){if(IsRecording)Finish("interrupted","Recorder destroyed; partial evidence retained.");}
    }
}
