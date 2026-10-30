using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using DiceCraft.Core;
using DiceCraft.Tests;
using NUnit.Framework;

namespace DiceCraft.IndependentReview
{
    public sealed class IndependentV05Tests : SessionTestSupport
    {
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };
        private static readonly JsonDocument Vectors = JsonDocument.Parse(File.ReadAllText("evidence/v05/review/independent-vectors.json"));
        private static void Same(JsonElement expected, JsonElement actual, string path)
        {
            Assert.That(actual.ValueKind, Is.EqualTo(expected.ValueKind), path + " kind");
            if (expected.ValueKind == JsonValueKind.Object)
                foreach (var field in expected.EnumerateObject()) { Assert.That(actual.TryGetProperty(field.Name, out var got), Is.True, path + "." + field.Name + " exists"); Same(field.Value, got, path + "." + field.Name); }
            else if (expected.ValueKind == JsonValueKind.Array)
            {
                Assert.That(actual.GetArrayLength(), Is.EqualTo(expected.GetArrayLength()), path + " length");
                for (int i = 0; i < expected.GetArrayLength(); i++) Same(expected[i], actual[i], path + "[" + i + "]");
            }
            else Assert.That(actual.ToString(), Is.EqualTo(expected.ToString()), path);
        }

        [Test] public void Reviewer_1024GeneratedScoresMatchAll26ReferenceFieldsAndArePure()
        {
            foreach (var item in Vectors.RootElement.GetProperty("scores").EnumerateArray())
            {
                int serial = 0;
                var s = new SubmissionSnapshot {
                    capacity = item.GetProperty("capacity").GetInt32(), rollsRemaining = item.GetProperty("rollsRemaining").GetInt32(),
                    submissionOrdinalBefore = item.GetProperty("submissionOrdinal").GetInt32(), redWave = item.GetProperty("redWave").GetBoolean(),
                    handLevels = item.GetProperty("levels").EnumerateObject().Select(v => new HandLevel { handId = v.Name, level = v.Value.GetInt32() }).ToArray(),
                    equippedControllers = item.GetProperty("controllerIds").EnumerateArray().Select((cid, i) => new ControllerInstance { definitionId = cid.GetString(), instanceId = "REVIEW_" + i, acquiredSerial = i + 1, enchantmentId = item.GetProperty("enchants").GetProperty(cid.GetString()).GetString() }).ToArray(),
                    dice = item.GetProperty("dice").EnumerateArray().Select(d => new SubmissionDie { dieId = "D" + (++serial), faceId = "F01", currentPip = d.GetProperty("p").GetInt32(), currentColor = d.GetProperty("c").GetString(), permanentSixFaceSum = d.GetProperty("sum6").GetInt32(), isGold = d.GetProperty("gold").GetBoolean(), isGlass = d.GetProperty("glass").GetBoolean() }).ToArray()
                };
                string before = JsonSerializer.Serialize(s, Json);
                var result = ScoreCalculator.Evaluate(s, cfg);
                Same(item.GetProperty("expected"), JsonSerializer.SerializeToElement(result, Json), item.GetProperty("id").GetString());
                Assert.That(JsonSerializer.Serialize(s, Json), Is.EqualTo(before), "pure score");
                var explain = ScoreExplanation.Build(s, result, cfg);
                Assert.That(explain.global.Last().after, Is.EqualTo(result.score));
                Assert.That(explain.global.Count(step => step.operation == "floor"), Is.EqualTo(1));
            }
        }

        [Test] public void Reviewer_512ExtremeWeightPermutedProbabilitiesMatchIndependentFractions()
        {
            foreach (var item in Vectors.RootElement.GetProperty("probabilities").EnumerateArray())
            {
                var faces = JsonSerializer.Deserialize<FaceState[]>(item.GetProperty("faces").GetRawText(), Json);
                string before = JsonSerializer.Serialize(faces, Json);
                var actual = ProbabilityCalculator.BuildUnits(faces, item.GetProperty("large").GetBoolean(), item.GetProperty("small").GetBoolean(), cfg, item.GetProperty("high").GetBoolean(), item.GetProperty("low").GetBoolean());
                CollectionAssert.AreEqual(item.GetProperty("expected").EnumerateArray().Select(x => x.GetInt32()), actual, item.GetProperty("id").GetString());
                Assert.That(JsonSerializer.Serialize(faces, Json), Is.EqualTo(before));
                Assert.That(actual.Sum(), Is.EqualTo(1000000)); Assert.That(actual.Min(), Is.GreaterThanOrEqualTo(0));
            }
        }

        [Test] public void Reviewer_128FullShopTracesMatchIndependentOracleIncludingReplacementAndRng()
        {
            foreach (var item in Vectors.RootElement.GetProperty("shops").EnumerateArray())
            {
                var s = Selecting(new[] {4,4,4,4,4}, new[] {"red","red","red","red","red"});
                Do(s, CommandKind.Submit); Do(s, CommandKind.EndStage);
                s = Edit(s, state => {
                    state.shop = null; state.shopRngState = item.GetProperty("rng").GetUInt32();
                    state.controllers.Clear(); state.controllerSlots = 4;
                    foreach (var c in item.GetProperty("owned").EnumerateArray()) {
                        int serial = state.nextItemSerial++;
                        state.controllers.Add(new ControllerInstance { instanceId = "ITEM_" + serial.ToString("D5"), acquiredSerial = serial, definitionId = c.GetProperty("id").GetString(), enchantmentId = c.GetProperty("enchant").GetString() });
                    }
                    state.dice.Clear(); state.nextDieSerial = 1;
                    foreach (var die in item.GetProperty("dice").EnumerateArray()) {
                        var d = DiceFactory.CreateStandard(cfg, state.nextDieSerial++); d.isGlass = die.GetProperty("glass").GetBoolean();
                        d.faces = JsonSerializer.Deserialize<FaceState[]>(die.GetProperty("faces").GetRawText(), Json); state.dice.Add(d);
                    }
                    foreach (var h in state.handLevels) h.level = item.GetProperty("levels").GetProperty(h.handId).GetInt32();
                    state.consumableSlots = item.GetProperty("slots").GetInt32(); state.itemDiscount = item.GetProperty("discount").GetInt32();
                });
                var before = s.State;
                Do(s, CommandKind.Continue);
                var after = s.State;
                var offers = new List<Dictionary<string, object>>();
                foreach (var o in after.shop.offers) {
                    var row = new Dictionary<string, object> { ["category"] = o.category, ["id"] = o.contentId, ["basePrice"] = o.basePrice, ["enchant"] = o.controller?.enchantmentId };
                    if (o.die != null) row["die"] = o.die.faces.Select(f => new { pip = f.permanentPip, color = f.baseColor }).ToArray();
                    offers.Add(row);
                }
                Same(item.GetProperty("expected"), JsonSerializer.SerializeToElement(new { offers, rng = after.shopRngState }, Json), item.GetProperty("id").GetString());
                Assert.That(after.diceRngState, Is.EqualTo(before.diceRngState)); Assert.That(after.effectRngState, Is.EqualTo(before.effectRngState));
                Assert.That(after.gold, Is.EqualTo(before.gold));
                string persisted = JsonSerializer.Serialize(after, Json);
                for (int i=0;i<3;i++) s = new GameSession(cfg, new MemorySaveStore(), s.State);
                Assert.That(JsonSerializer.Serialize(s.State, Json), Is.EqualTo(persisted));
            }
        }

        [Test] public void Reviewer_C03MakesAllBlueImpossibleButDoesNotBlockBuyingItsUpgrade()
        {
            var cards = new[] { new ControllerInstance { definitionId="C03", enchantmentId="none" } };
            var row = HandbookRules.Build(cfg, Array.Empty<HandLevel>(), cards, 5, 5, 5).Single(x => x.handId == "all_blue");
            Assert.That(row.canForm, Is.False, "C03 always makes the first submitted die effectively red");
            Assert.That(row.upgradeEligible, Is.True, "upgrade purchase uses the specified quantity/capacity conditions");
        }

        [Test] public void Reviewer_RestoreRejectsNullFrozenTriggerCollectionAtValidationBoundary()
        {
            var s = RolledHistory();
            var corrupt = s.State; corrupt.lastSubmission.breakdown.triggeredControllerIds = null;
            Assert.That(() => s.ValidateState(corrupt), Throws.Exception,
                "AtomicSaveStore relies on ValidateState, before GameSession.Clone can throw and bypass backup recovery");
        }

        [Test] public void Reviewer_RestoreRejectsContradictoryFrozenScoreBeforeHistoryCanCrashUi()
        {
            var s = RolledHistory();
            var corrupt = s.State; corrupt.lastSubmission.breakdown.score++;
            Assert.That(() => s.ValidateState(corrupt), Throws.Exception,
                "Frozen history must not pass validation and then fail ScoreExplanation.Build during display");
        }

        [TestCase("null-trigger-array")] [TestCase("wrong-frozen-score")]
        public void Reviewer_RealIoBadFrozenHistoryMustRecoverBackupBeforeClone(string corruption)
        {
            var s = Ready("C07","C36"); Do(s,CommandKind.Roll); Lock(s,"D001","D002");
            string directory=Path.Combine("evidence/v05/review/io",corruption+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            string path=Path.GetFullPath(Path.Combine(directory,"active-run.json"));
            var store=new global::DiceCraft.AtomicSaveStore(path,cfg);
            store.Save(s.State); var backup=s.State;
            Do(s,CommandKind.Submit);store.Save(s.State);
            var envelope=JsonNode.Parse(File.ReadAllText(path));
            var payload=JsonNode.Parse(envelope["payload"].GetValue<string>());
            var breakdown=payload["lastSubmission"]["breakdown"];
            if(corruption=="null-trigger-array")breakdown["triggeredControllerIds"]=null;
            else breakdown["score"]=breakdown["score"].GetValue<long>()+1;
            string serialized=payload.ToJsonString();envelope["payload"]=serialized;
            envelope["sha256"]=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serialized))).ToLowerInvariant();
            File.WriteAllText(path,envelope.ToJsonString());
            var loaded=store.Load();
            Assert.That(loaded,Is.Not.Null,store.LastLoadNotice);
            Assert.That(loaded.actionSequence,Is.EqualTo(backup.actionSequence),"Invalid frozen history must not shadow the valid backup");
            Assert.That(store.LastLoadNotice,Does.Contain("备份"));
            Assert.DoesNotThrow(()=>new GameSession(cfg,new MemorySaveStore(),loaded));
        }

        private GameSession RolledHistory()
        {
            var s=Ready("C07","C36");Do(s,CommandKind.Roll);Lock(s,"D001","D002");Do(s,CommandKind.Submit);return s;
        }

        [Test] public void Reviewer_SaveCacheCannotTrustSameLengthAndTimestampAfterExternalCorruption()
        {
            var s=Ready();string directory=Path.GetFullPath(Path.Combine("evidence/v05/review/io","cache-same-metadata-"+Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);string path=Path.Combine(directory,"active-run.json");var store=new global::DiceCraft.AtomicSaveStore(path,cfg);
            store.Save(s.State);Do(s,CommandKind.Roll);store.Save(s.State);
            byte[] backup=File.ReadAllBytes(path+".backup");var stamp=File.GetLastWriteTimeUtc(path);byte[] valid=File.ReadAllBytes(path);
            string text=Encoding.UTF8.GetString(valid);var node=JsonNode.Parse(text);string digest=node["sha256"].GetValue<string>();
            string wrong=(digest[0]=='0'?"1":"0")+digest.Substring(1);File.WriteAllBytes(path,Encoding.UTF8.GetBytes(text.Replace(digest,wrong)));File.SetLastWriteTimeUtc(path,stamp);
            Assert.That(new FileInfo(path).Length,Is.EqualTo(valid.Length));Assert.That(File.GetLastWriteTimeUtc(path),Is.EqualTo(stamp));
            store.Save(s.State);CollectionAssert.AreEqual(backup,File.ReadAllBytes(path+".backup"));Assert.That(Directory.GetFiles(directory,"*.rejected-*").Length,Is.EqualTo(1));
            Assert.That(store.Load().actionSequence,Is.EqualTo(s.State.actionSequence));
        }

        [Test] public void Reviewer_SaveCacheRejectsInvalidFrozenHistoryBeforeAnyDiskWrite()
        {
            var s=RolledHistory();string directory=Path.GetFullPath(Path.Combine("evidence/v05/review/io","cache-invalid-candidate-"+Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);string path=Path.Combine(directory,"active-run.json");var store=new global::DiceCraft.AtomicSaveStore(path,cfg);
            store.Save(s.State);byte[] active=File.ReadAllBytes(path);var invalid=s.State;invalid.lastSubmission.breakdown.score++;
            Assert.That(()=>store.Save(invalid),Throws.Exception);CollectionAssert.AreEqual(active,File.ReadAllBytes(path));Assert.That(File.Exists(path+".tmp"),Is.False);Assert.That(File.Exists(path+".backup"),Is.False);
            Do(s,CommandKind.Roll);store.Save(s.State);CollectionAssert.AreEqual(active,File.ReadAllBytes(path+".backup"));
        }

        [Test] public void Reviewer_SaveCacheReplacementFailureRetainsOldActiveAndAllowsRetry()
        {
            var s=Ready();string directory=Path.GetFullPath(Path.Combine("evidence/v05/review/io","cache-replace-failure-"+Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);string path=Path.Combine(directory,"active-run.json");var store=new global::DiceCraft.AtomicSaveStore(path,cfg);
            store.Save(s.State);byte[] active=File.ReadAllBytes(path);Do(s,CommandKind.Roll);
            using(var held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))Assert.That(()=>store.Save(s.State),Throws.Exception);
            CollectionAssert.AreEqual(active,File.ReadAllBytes(path));Assert.That(File.Exists(path+".backup"),Is.False);
            store.Save(s.State);CollectionAssert.AreEqual(active,File.ReadAllBytes(path+".backup"));Assert.That(store.Load().actionSequence,Is.EqualTo(s.State.actionSequence));
        }

        [Test] public void Reviewer_ActualNativeSaveFailureCandidateRetriesExactlyWithoutReroll()
        {
            string source=Path.GetFullPath("evidence/v05/delivery/windows-new-process/player-20261030-055802-1685d517/normal-save");
            string[] names={"active-run.json","active-run.json.backup","active-run.json.tmp"};
            var original=names.ToDictionary(n=>n,n=>File.ReadAllBytes(Path.Combine(source,n)));
            string directory=Path.GetFullPath(Path.Combine("evidence/v05/review/io","actual-save-failure-retry-"+Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);string path=Path.Combine(directory,"active-run.json");
            File.WriteAllBytes(path,original[names[0]]);File.WriteAllBytes(path+".backup",original[names[1]]);
            var envelope=JsonNode.Parse(Encoding.UTF8.GetString(original[names[2]]));
            var expected=JsonSerializer.Deserialize<RunState>(envelope["payload"].GetValue<string>(),Json);
            var command=expected.processedReceipts.Last().command.Clone();
            var store=new global::DiceCraft.AtomicSaveStore(path,cfg);var loaded=store.Load();
            var session=new GameSession(cfg,store,loaded);string before=JsonSerializer.Serialize(session.State,Json);CommandResult failed;
            using(var held=new FileStream(path+".backup",FileMode.Open,FileAccess.Read,FileShare.Read))failed=session.Execute(command);
            Assert.That(failed.accepted,Is.False);Assert.That(failed.pendingSave,Is.True);Assert.That(session.HasPendingSave,Is.True);
            Assert.That(JsonSerializer.Serialize(session.State,Json),Is.EqualTo(before));CollectionAssert.AreEqual(original[names[0]],File.ReadAllBytes(path));CollectionAssert.AreEqual(original[names[1]],File.ReadAllBytes(path+".backup"));
            var retried=session.RetrySave();Assert.That(retried.accepted,Is.True);Assert.That(session.HasPendingSave,Is.False);
            Assert.That(JsonSerializer.Serialize(session.State,Json),Is.EqualTo(JsonSerializer.Serialize(expected,Json)),"Actual failed native candidate should commit identically after controlled file-lock release");
            string after=JsonSerializer.Serialize(session.State,Json);Assert.That(session.Execute(command).duplicate,Is.True);Assert.That(JsonSerializer.Serialize(session.State,Json),Is.EqualTo(after));
            Assert.That(JsonSerializer.Serialize(store.Load(),Json),Is.EqualTo(after));
            foreach(string n in names)CollectionAssert.AreEqual(original[n],File.ReadAllBytes(Path.Combine(source,n)),"Original native evidence must remain untouched");
            File.WriteAllText(Path.Combine(directory,"review-result.json"),JsonSerializer.Serialize(new{source,scope="System.Text.Json managed adapter, real Windows IO, immutable native failure snapshots copied to reviewer directory",controlledFailure=failed.error,pendingBlockedStateStayedAt=loaded.actionSequence,retriedSequence=session.State.actionSequence,expectedCandidateMatched=true,duplicateDidNotReroll=true,sourceFileHashes=original.ToDictionary(kv=>kv.Key,kv=>Convert.ToHexString(SHA256.HashData(kv.Value)).ToLowerInvariant())},new JsonSerializerOptions{WriteIndented=true}));
        }

        [TestCase(13,true)] [TestCase(12,false)]
        public void Reviewer_ExactNetReplacementBoundaryE36E37(int gold, bool success)
        {
            string offer=null; var s=Edit(Shop(), state=> {
                state.gold=gold; Equip(state,"C28"); state.isDebug=false;
                offer=PutOffer(state,"controller","C28"); state.shop.offers.Single(o=>o.offerId==offer).controller.enchantmentId="slotless";
            });
            string before=JsonSerializer.Serialize(s.State,Json);
            var command=Command(s,CommandKind.Buy); command.offerId=offer;
            var result=s.Execute(command); Assert.That(result.accepted,Is.EqualTo(success));
            if(success) { Assert.That(s.State.gold,Is.Zero); Assert.That(s.State.controllers.Single().enchantmentId,Is.EqualTo("slotless")); Assert.That(s.Execute(command).duplicate,Is.True); }
            else Assert.That(JsonSerializer.Serialize(s.State,Json),Is.EqualTo(before));
        }

        [Test] public void Reviewer_E34TenItemsReplacingOrdinaryWithSlotlessFreesOnlyItsSlot()
        {
            string offer=null; var s=Edit(Shop(), state=> {
                state.gold=100; Equip(state,"C01","C02","C03","C04","C05","C06","C07","C08","C09","C10");
                state.controllerSlots=4; state.isDebug=false;
                foreach(var c in state.controllers.Skip(4)) c.enchantmentId="slotless";
                offer=PutOffer(state,"controller","C02"); state.shop.offers.Single(o=>o.offerId==offer).controller.enchantmentId="slotless";
            });
            Do(s,CommandKind.Buy,c=>c.offerId=offer);
            Assert.That(s.State.controllers.Count,Is.EqualTo(10)); Assert.That(s.OrdinaryControllerSlotsUsed,Is.EqualTo(3)); Assert.That(s.State.controllerSlots,Is.EqualTo(4));
            Do(s,CommandKind.Sell,c=>c.itemInstanceId=s.State.controllers.Single(v=>v.definitionId=="C02").instanceId);
            Assert.That(s.State.controllers.Count,Is.EqualTo(9)); Assert.That(s.OrdinaryControllerSlotsUsed,Is.EqualTo(3)); Assert.That(s.State.controllerSlots,Is.EqualTo(4));
        }

        [TestCase("none")] [TestCase("base5")]
        public void Reviewer_E35SameOrEmptyEnchantCannotReplaceAndDoesNotCharge(string proposed)
        {
            string offer=null; var s=Edit(Shop(),state=> {
                state.gold=100; Equip(state,"C28");state.controllers[0].enchantmentId="base5";state.isDebug=false;
                offer=PutOffer(state,"controller","C28");state.shop.offers.Single(o=>o.offerId==offer).controller.enchantmentId=proposed;
            });
            string before=JsonSerializer.Serialize(s.State,Json);
            var command=Command(s,CommandKind.Buy);command.offerId=offer;
            Assert.That(s.Execute(command).accepted,Is.False);
            Assert.That(JsonSerializer.Serialize(s.State,Json),Is.EqualTo(before));
        }
    }
}
