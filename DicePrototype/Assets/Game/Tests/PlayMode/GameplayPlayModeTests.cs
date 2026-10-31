using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UObj = UnityEngine.Object;

namespace DiceCraft.Tests
{
    /// <summary>
    /// Real Bootstrap scene, production UI handlers and production save adapter.
    /// Each case uses a unique temporary save/log directory and restores user preferences.
    /// No test edits the player's normal save. Fault fixtures are explicitly marked debug.
    /// </summary>
    public sealed partial class GameplayPlayModeTests
    {
        private const string ScenePath = "Assets/Game/Scenes/Bootstrap.unity";
        private GameApp app;
        private string saveDirectory;
        private readonly Dictionary<string, int?> intPreferences = new Dictionary<string, int?>();
        private float? originalVolume;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            saveDirectory = Path.Combine(Application.temporaryCachePath, "DiceCraftTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(saveDirectory);
            GameApp.SaveDirectoryOverride = saveDirectory; GameApp.SuppressStartupDisplay=true;
            foreach (string key in new[] {"fast", "reduced", "shopTab"})
            {
                intPreferences[key] = PlayerPrefs.HasKey(key) ? (int?)PlayerPrefs.GetInt(key) : null;
                PlayerPrefs.SetInt(key, 0);
            }
            originalVolume = PlayerPrefs.HasKey("volume") ? (float?)PlayerPrefs.GetFloat("volume") : null;
            PlayerPrefs.SetFloat("volume", 0);
            yield return LoadBootstrap();
            Assert.That(app.SaveLocation, Is.EqualTo(Path.Combine(saveDirectory, "active-run.json")), "Test isolation hook was not applied before Awake.");
            Assert.That(app.Session.State, Is.Null, "A unique temporary save directory must start empty.");
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (app != null && app.IsBusy) app.SkipVisual();
            var bootstrap = SceneManager.GetSceneByPath(ScenePath);
            if (bootstrap.IsValid() && bootstrap.isLoaded)
            {
                Scene empty = SceneManager.CreateScene("DiceCraftTestCleanup_" + Guid.NewGuid().ToString("N"));
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(bootstrap);
            }
            GameApp.SaveDirectoryOverride = null; GameApp.SuppressStartupDisplay=false;
            foreach (var preference in intPreferences)
                if (preference.Value.HasValue) PlayerPrefs.SetInt(preference.Key, preference.Value.Value);
                else PlayerPrefs.DeleteKey(preference.Key);
            if (originalVolume.HasValue) PlayerPrefs.SetFloat("volume", originalVolume.Value);
            else PlayerPrefs.DeleteKey("volume");
            PlayerPrefs.Save();
            TestContext.WriteLine("Isolated save/log evidence: " + saveDirectory);
            app = null;
        }

        [UnityTest]
        public IEnumerator PLAY_Bootstrap_RealReferences71IconsFontAndSingleInputSystem()
        {
            Assert.That(UObj.FindObjectsByType<GameApp>().Length, Is.EqualTo(1));
            var systems = UObj.FindObjectsByType<EventSystem>();
            Assert.That(systems.Length, Is.EqualTo(1));
            Assert.That(systems[0].GetComponent<InputSystemUIInputModule>(), Is.Not.Null);
            Assert.That(systems[0].GetComponents<BaseInputModule>().Length, Is.EqualTo(1));
            Assert.That(app.canvas, Is.Not.Null);
            Assert.That(app.canvas.GetComponent<CanvasScaler>(), Is.Not.Null);
            Assert.That(app.table, Is.Not.Null);
            Assert.That(app.table.viewCamera, Is.Not.Null);
            Assert.That(app.table.viewCamera.orthographic, Is.True);
            Assert.That(app.table.bodyMesh, Is.Not.Null);
            long bodyIndices = Enumerable.Range(0, app.table.bodyMesh.subMeshCount).Sum(i => (long)app.table.bodyMesh.GetIndexCount(i));
            Assert.That(bodyIndices / 3, Is.LessThanOrEqualTo(2000));
            foreach (Material material in new[] {app.table.bodyMaterial, app.table.faceMaterial, app.table.glassMaterial})
            {
                Assert.That(material, Is.Not.Null);
                Assert.That(material.shader, Is.Not.Null);
                Assert.That(material.shader.name, Does.Not.Contain("InternalErrorShader"));
            }
            string[] ids = app.Config.controllers.Select(c => c.id).Concat(app.Config.consumables.Select(c => c.id))
                .Concat(app.Config.services.Select(c => c.id)).Concat(app.Config.diceUpgrades.Select(c => c.id)).ToArray();
            Assert.That(ids.Length, Is.EqualTo(71));
            Assert.That(app.contentIcons, Has.Length.EqualTo(71));
            Assert.That(app.contentIcons.All(sprite => sprite != null), Is.True);
            CollectionAssert.AreEquivalent(ids, app.contentIcons.Select(sprite => sprite.name));
            Assert.That(app.uiFont, Is.Not.Null);
            string requiredText = "骰子构筑投掷确认锁定提交金币红蓝" + string.Concat(app.Config.controllers.Select(c => c.displayName))
                + string.Concat(app.Config.consumables.Select(c => c.displayName)) + string.Concat(app.Config.services.Select(c => c.displayName))
                + string.Concat(app.Config.diceUpgrades.Select(c => c.displayName));
            Assert.That(app.uiFont.HasCharacters(requiredText, out uint[] missing, true, true), Is.True,
                "Missing required Chinese glyphs: " + string.Join(",", missing ?? Array.Empty<uint>()));
            foreach (var label in app.canvas.GetComponentsInChildren<TextMeshProUGUI>())
                Assert.That(label.font, Is.Not.Null, label.name);
            Assert.That(Button("ContinueRun").interactable, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PLAY_CoreLoop_ClickLockReorderAndSubmitMatchesPreview()
        {
            StartRun("20261028");
            Assert.That(app.Session.State.phase, Is.EqualTo("StagePreview"));
            Assert.That(app.Session.State.gold, Is.EqualTo(0));
            Assert.That(app.Session.State.dice.Count, Is.EqualTo(5));
            Click("StartStage");
            Assert.That(app.Session.State.phase, Is.EqualTo("HandReady"));
            Assert.That(app.Session.State.hand.hasRolled, Is.False);
            Assert.That(Button("Submit").interactable, Is.False);
            Click("Roll");
            Assert.That(app.IsBusy, Is.True);
            Assert.That(app.Session.State.hand.rollsRemaining, Is.EqualTo(2));
            yield return WaitForIdle();

            long beforePending = app.Session.State.actionSequence;
            ClickDie("D002"); ClickDie("D001");
            CollectionAssert.AreEqual(new[] {"D002", "D001"}, app.Session.PendingIds);
            Assert.That(app.Session.State.actionSequence, Is.EqualTo(beforePending));
            Assert.That(Button("Roll").interactable, Is.False);
            Assert.That(Button("Submit").interactable, Is.False);
            Click("Clear");
            Assert.That(app.Session.PendingIds, Is.Empty);
            Assert.That(app.Session.State.hand.orderedLockedIds, Is.Empty);
            ClickDie("D002"); ClickDie("D001"); Click("Confirm");
            CollectionAssert.AreEqual(new[] {"D002", "D001"}, app.Session.State.hand.orderedLockedIds);
            Assert.That(app.Session.TogglePending("D001"), Is.False, "An ordinary click must not unlock confirmed dice.");
            string lockedState = StateJson();
            ClickDie("D001");
            Assert.That(FindActive<Transform>("Dialog"), Is.Not.Null, "Clicking a locked die should open details.");
            Assert.That(StateJson(), Is.EqualTo(lockedState));
            Click("CloseDialog");

            DragLockedDieOnto("D002", "D001");
            CollectionAssert.AreEqual(new[] {"D001", "D002"}, app.Session.State.hand.orderedLockedIds);
            var loaded = new AtomicSaveStore(app.SaveLocation, app.Config).Load();
            CollectionAssert.AreEqual(new[] {"D001", "D002"}, loaded.hand.orderedLockedIds);
            Assert.That(loaded.hand.currentResults.Count(r => r.isLocked), Is.EqualTo(2));
            string expected = app.Session.PreviewScore().score;
            Assert.That(FindActive<TextMeshProUGUI>("FinalScore").text, Is.EqualTo(System.Numerics.BigInteger.Parse(expected).ToString("N0",System.Globalization.CultureInfo.InvariantCulture)));
            Click("Submit");
            Assert.That(app.Session.State.lastSubmission.breakdown.score, Is.EqualTo(expected));
            Assert.That(app.Session.State.stage.score, Is.EqualTo(expected));
            Assert.That(app.Session.State.stage.remainingSubmissions, Is.EqualTo(2));
            Assert.That(app.Session.State.phase, Is.EqualTo("HandReady"));
            yield return WaitForIdle();
            Assert.That(app.Session.State.hand.orderedLockedIds, Is.Empty);
            Assert.That(app.Session.State.hand.hasRolled, Is.False);
        }

        [UnityTest]
        public IEnumerator PLAY_BusyRejectsBypassedUiAndSkipPreservesCommittedRandomness()
        {
            StartRun("444"); Click("StartStage"); Click("Roll");
            Assert.That(app.IsBusy, Is.True);
            string accepted = StateJson();
            Assert.That(Button("Roll").interactable, Is.False);
            // Deliberately bypass interactability: the command layer must still reject it.
            Button("Roll").onClick.Invoke();
            Assert.That(app.Session.TogglePending("D001"), Is.False);
            var blocked = app.Send(new SessionCommand {kind = CommandKind.Submit});
            Assert.That(blocked.accepted, Is.False);
            Assert.That(StateJson(), Is.EqualTo(accepted));
            Assert.That(JsonUtility.ToJson(new AtomicSaveStore(app.SaveLocation, app.Config).Load()), Is.EqualTo(accepted), "Roll must be durably saved before presentation finishes.");
            Click("Skip");
            Assert.That(app.IsBusy, Is.False);
            Assert.That(StateJson(), Is.EqualTo(accepted));
            AssertAllFaceOrientations();
            yield return new WaitForSecondsRealtime(1.05f);
            Assert.That(StateJson(), Is.EqualTo(accepted), "The cancelled animation callback must not execute a second result.");
            Assert.That(app.IsBusy, Is.False);
        }

        [UnityTest]
        public IEnumerator PLAY_FastAnimationUsesSameSeedResultsAndDoesNotConsumeExtraRolls()
        {
            StartRun("71234"); Click("StartStage"); Click("Roll");
            yield return WaitForIdle();
            var normal = app.Session.State;
            Click("Pause"); Click("MainMenu");
            StartRun("71234"); Click("StartStage");
            Click("Pause"); Click("Fast"); Click("Resume");
            Assert.That(PlayerPrefs.GetInt("fast"), Is.EqualTo(1));
            Click("Roll");
            var accepted = app.Session.State;
            Assert.That(accepted.diceRngState, Is.EqualTo(normal.diceRngState));
            Assert.That(accepted.effectRngState, Is.EqualTo(normal.effectRngState));
            Assert.That(accepted.hand.rollsRemaining, Is.EqualTo(normal.hand.rollsRemaining));
            CollectionAssert.AreEqual(normal.hand.currentResults.Select(ResultKey), accepted.hand.currentResults.Select(ResultKey));
            string before = StateJson();
            yield return WaitForIdle();
            Assert.That(StateJson(), Is.EqualTo(before));
            AssertAllFaceOrientations();
        }

        [UnityTest]
        public IEnumerator PLAY_ReloadDuringRollRestoresLocksOrderAndAcceptedFaceResults()
        {
            StartRun("9281"); Click("StartStage"); Click("Roll"); Click("Skip");
            ClickDie("D003"); ClickDie("D001"); Click("Confirm");
            DragLockedDieOnto("D003", "D001");
            Click("Roll");
            Assert.That(app.IsBusy, Is.True);
            string expected = StateJson();
            yield return LoadBootstrap();
            Assert.That(app.IsBusy, Is.False);
            Assert.That(StateJson(), Is.EqualTo(expected));
            Assert.That(app.Session.PendingIds, Is.Empty);
            Assert.That(Button("ContinueRun").interactable, Is.True);
            Click("ContinueRun");
            CollectionAssert.AreEqual(new[] {"D001", "D003"}, app.Session.State.hand.orderedLockedIds);
            Assert.That(app.Session.State.hand.rollsRemaining, Is.EqualTo(1));
            AssertAllFaceOrientations();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PLAY_SceneUnloadReloadResetsStaticUiAndDoesNotLeakSession()
        {
            StartRun("123"); Click("StartStage"); Click("Roll"); Click("Skip"); ClickDie("D001");
            var previous = app;
            Scene oldScene = app.gameObject.scene;
            Scene empty = SceneManager.CreateScene("DiceCraftReloadBoundary_" + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(oldScene);
            Assert.That(previous == null, Is.True);
            Assert.That(UiKit.Font == null, Is.True, "Static font reference must be released on teardown.");
            string secondDirectory = Path.Combine(saveDirectory, "second-session");
            Directory.CreateDirectory(secondDirectory); GameApp.SaveDirectoryOverride = secondDirectory;
            yield return LoadBootstrap();
            Assert.That(app.Session.State, Is.Null, "A different save path must not inherit an in-memory Run.");
            Assert.That(app.Session.PendingIds, Is.Empty);
            Assert.That(app.IsBusy, Is.False);
            Assert.That(UiKit.Font, Is.SameAs(app.uiFont));
            Assert.That(UObj.FindObjectsByType<GameApp>().Length, Is.EqualTo(1));
            Assert.That(UObj.FindObjectsByType<EventSystem>().Length, Is.EqualTo(1));
            StartRun("123"); Click("StartStage");
            Assert.That(app.Session.State.hand.rollsRemaining, Is.EqualTo(3));
            Assert.That(app.Session.State.hand.orderedLockedIds, Is.Empty);
            Assert.That(app.Session.State.actionSequence, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator PLAY_SixPhysicalFacesAndRemotePipsHaveCorrectOrientationAndDotCount()
        {
            var die = DiceFactory.CreateStandard(app.Config, 987);
            for (int i = 0; i < 6; i++)
            {
                var hand = new DieHandState {dieId = die.dieId, currentFaceId = die.faces[i].faceId, currentPip = die.faces[i].permanentPip, currentColor = die.faces[i].baseColor, hasRolled = true};
                app.table.Display(die, hand, new Vector2(960, 540)); app.table.FinishRoll();
                var orientation = app.table.GetFaceOrientation(die.dieId);
                Assert.That(orientation, Is.Not.Null, die.dieId);
                Assert.That(Vector3.Dot(orientation.TransformDirection(DiceTable.Normals[i]).normalized, Vector3.up), Is.GreaterThan(.999f), hand.currentFaceId);
                var face = orientation.Find("Face_" + hand.currentFaceId).GetComponent<MeshFilter>().sharedMesh;
                Assert.That(ConnectedMeshComponents(face), Is.EqualTo(i + 1), "The face must render the exact pip count, independently of tessellation.");
                yield return null;
            }
            var remote = new DieHandState {dieId = die.dieId, currentFaceId = "F01", currentPip = 6, currentColor = "red", hasRolled = true, remoteOverride = true};
            app.table.Display(die, remote, new Vector2(960, 540)); app.table.FinishRoll();
            var physical = app.table.GetFaceOrientation(die.dieId);
            Assert.That(physical, Is.Not.Null, die.dieId);
            Assert.That(Vector3.Dot(physical.TransformDirection(DiceTable.Normals[0]), Vector3.up), Is.GreaterThan(.999f));
            Assert.That(ConnectedMeshComponents(physical.Find("Face_F01").GetComponent<MeshFilter>().sharedMesh), Is.EqualTo(6));
            Assert.That(die.faces[0].permanentPip, Is.EqualTo(1), "Remote display must not rewrite the permanent face.");
            Assert.That(remote.currentFaceId, Is.EqualTo("F01"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PLAY_IndependentTutorialCompletesRealPurchaseAndPreservesFormalRun()
        {
            StartRun("121212"); Click("StartStage");
            string formal = StateJson();
            byte[] formalSave = File.ReadAllBytes(app.SaveLocation);
            Click("Pause"); Click("MainMenu"); Click("Tutorial");
            Assert.That(app.Session.State.isDebug, Is.True);
            Assert.That(app.Session.State.stage.target, Is.EqualTo("50"));
            Assert.That(Button("Normal").GetComponentInChildren<TextMeshProUGUI>().text, Does.Contain("50 分"), "Tutorial challenge card must show its actual target.");
            Click("StartStage"); Click("Roll"); Click("Skip");
            foreach (string id in app.Session.State.dice.Select(d => d.dieId).ToArray()) ClickDie(id);
            Click("Confirm");
            var practiceScore = app.Session.PreviewScore();
            Assert.That(practiceScore.pipSum, Is.EqualTo("19"));
            Assert.That(practiceScore.unusedRollBonus, Is.EqualTo(6));
            Assert.That(practiceScore.totalHandMult100, Is.EqualTo(200));
            Assert.That(practiceScore.redMult100, Is.EqualTo(175));
            Assert.That(practiceScore.score, Is.EqualTo("87"), "v0.6: floor((19 + 6) × pair 2 × mixed color 1 × red 1.75).");
            Click("Submit");
            yield return WaitForIdle();
            Assert.That(app.Session.State.phase, Is.EqualTo("HandReady"));
            Assert.That(app.Session.State.stage.qualified, Is.True);
            Assert.That(app.Session.CanEndStage, Is.True);
            Click("EndStage");
            Assert.That(app.Session.State.phase, Is.EqualTo("StageSettlement"));
            Click("SettlementContinue");
            Assert.That(app.Session.State.phase, Is.EqualTo("Shop"));
            var offer = app.Session.State.shop.offers.First(o => o.category == "controller");
            long goldBefore = app.Session.State.gold, price = app.Session.GetOfferPrice(offer);
            var card = FindActive<Transform>("Offer_" + offer.offerId);
            var inspect = card.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.name == "Inspect");
            Assert.That(inspect.interactable, Is.True); inspect.onClick.Invoke();
            Click("ConfirmPurchase");
            Assert.That(app.Session.State.gold, Is.EqualTo(goldBefore - price));
            Assert.That(app.Session.State.controllers.Any(c => c.contentId == offer.contentId), Is.True);
            Assert.That(app.Session.State.shop.offers.Single(o => o.offerId == offer.offerId).sold, Is.True);
            CollectionAssert.AreEqual(formalSave, File.ReadAllBytes(app.SaveLocation));
            Click("Pause"); Click("MainMenu");
            Assert.That(StateJson(), Is.EqualTo(formal));
            Assert.That(app.Session.State.isDebug, Is.False);
            Click("ContinueRun");
            Assert.That(app.Session.State.phase, Is.EqualTo("HandReady"));
            yield return null;
        }

        private IEnumerator LoadBootstrap()
        {
            AsyncOperation loading = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            Assert.That(loading, Is.Not.Null, "Bootstrap must be present in Build Settings.");
            yield return loading;
            yield return null;
            app = UObj.FindAnyObjectByType<GameApp>();
            Assert.That(app, Is.Not.Null, "Bootstrap did not instantiate GameApp.");
            Canvas.ForceUpdateCanvases();
        }
        private void StartRun(string seed)
        {
            FindActive<TMP_InputField>("SeedInput").text = seed;
            Click("NewRun");
            Assert.That(app.Session.State, Is.Not.Null);
            Assert.That(app.Session.State.seed, Is.EqualTo(uint.Parse(seed)));
        }
        private IEnumerator WaitForIdle()
        {
            float deadline = Time.realtimeSinceStartup + 4f;
            while (app.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(app.IsBusy, Is.False, "Presentation did not finish within four seconds.");
            yield return null;
        }
        private T FindActive<T>(string name) where T : Component
        {
            var matches = app.canvas.GetComponentsInChildren<T>(false).Where(c => c.name == name && c.gameObject.activeInHierarchy).ToArray();
            Assert.That(matches.Length, Is.EqualTo(1), "Expected exactly one active " + typeof(T).Name + " named " + name);
            return matches[0];
        }
        private UnityEngine.UI.Button Button(string name) => FindActive<UnityEngine.UI.Button>(name);
        private void Click(string name)
        {
            var button = Button(name);
            Assert.That(button.interactable, Is.True, "UI button is disabled: " + name);
            button.onClick.Invoke();
            Canvas.ForceUpdateCanvases();
        }
        private void ClickDie(string id)
        {
            var pointer = FindActive<DiePointer>("Die_" + id);
            var data = new PointerEventData(EventSystem.current) {button = PointerEventData.InputButton.Left};
            ExecuteEvents.Execute(pointer.gameObject, data, ExecuteEvents.pointerClickHandler);
            Canvas.ForceUpdateCanvases();
        }
        private void DragLockedDieOnto(string fromId, string toId)
        {
            var from = FindActive<DiePointer>("Die_" + fromId);
            var target = FindActive<DiePointer>("Die_" + toId).GetComponent<RectTransform>();
            Vector2 position = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center));
            var data = new PointerEventData(EventSystem.current) {position = position, button = PointerEventData.InputButton.Left, dragging = true};
            ExecuteEvents.Execute(from.gameObject, data, ExecuteEvents.beginDragHandler);
            ExecuteEvents.Execute(from.gameObject, data, ExecuteEvents.endDragHandler);
            Canvas.ForceUpdateCanvases();
        }
        private string StateJson() => JsonUtility.ToJson(app.Session.State);
        private static string ResultKey(DieHandState r) => r.dieId + "/" + r.currentFaceId + "/" + r.currentPip + "/" + r.currentColor + "/" + r.isLocked;
        private void AssertAllFaceOrientations()
        {
            foreach (var result in app.Session.State.hand.currentResults.Where(r => r.hasRolled))
            {
                var orientation = app.table.GetFaceOrientation(result.dieId);
                Assert.That(orientation, Is.Not.Null, result.dieId);
                Assert.That(Vector3.Dot(orientation.TransformDirection(DiceTable.Normals[DiceTable.FaceIndex(result.currentFaceId)]).normalized, Vector3.up), Is.GreaterThan(.999f), result.dieId + "/" + result.currentFaceId);
            }
        }
        private static int ConnectedMeshComponents(Mesh mesh)
        {
            int[] parent = Enumerable.Range(0, mesh.vertexCount).ToArray();
            Func<int, int> find = null;
            find = n => parent[n] == n ? n : (parent[n] = find(parent[n]));
            int[] triangles = mesh.triangles;
            var used = new HashSet<int>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                parent[find(b)] = find(a); parent[find(c)] = find(a);
                used.Add(a); used.Add(b); used.Add(c);
            }
            return used.Select(find).Distinct().Count();
        }
    }
}
