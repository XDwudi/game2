using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Core.Tests
{
    // p1.1.1 (W4b): the 160 vectors of the first golden file are now the L08- slice of GoldenScoresV111.json, read and
    // compared by V111Golden (V111GoldenTests.cs). The old file and its JsonUtility DTOs are gone from the game data.

    public class RulesTests
    {
        private GameConfig config;
        private static string DataPath(string name) => Path.Combine(Application.dataPath, "Game", "Data", name);
        private static GameConfig ReadConfig()
        {
            string json = File.ReadAllText(DataPath("P0Config.json"));
            GameConfig.ValidateJson(json);
            var result = JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(json));
            result.Validate();
            return result;
        }
        [SetUp] public void Setup() { config = ReadConfig(); }
        public static IEnumerable Goldens()
        {
            // p1.1.1 (W4b): the same 160 inputs, recomputed under p1.1.1 by the independent reference (ids L08-G001...).
            foreach (var item in V111Golden.Slice("L08-")) yield return new TestCaseData(item.id).SetName("GOLD-" + item.derivedId);
        }

        // The name is historical: the comparison now covers every expected field of the p1.1.1 file (33 fields and
        // the 19 fields of the post-score plan), see V111Golden.ExpectedFieldNames.
        [TestCaseSource(nameof(Goldens))]
        public void Golden_All21Fields_Plan14_AndNoMutation(string id)
        {
            V111Golden.AssertCase(V111Golden.Case(id), config, out _); // also asserts that scoring did not mutate the snapshot
        }

        [Test]
        public void GoldenFile_HasExactly160UniqueCases()
        {
            var cases = V111Golden.Slice("L08-");
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 160).Select(i => "G" + i.ToString("D3")), cases.Select(c => c.derivedId));
            Assert.That(cases.All(c => c.id == "L08-" + c.derivedId && c.derivedFile.Contains("GoldenScores")), Is.True);
        }
        [Test]
        public void GoldenJson_PreservesNullEmptyAndNonemptyGroupIdsExactly()
        {
            var cases = V111Golden.List(V111Golden.Obj(V111Json.Parse("{\"cases\":[{\"expected\":{\"chosenGroupId\":null}},{\"expected\":{\"chosenGroupId\":\"\"}},{\"expected\":{\"chosenGroupId\":\"pair\"}}]}"))["cases"])
                .Select(c => V111Golden.Obj(V111Golden.Obj(c)["expected"])).ToArray();
            Assert.That(cases[0]["chosenGroupId"], Is.Null);
            Assert.That(cases[1]["chosenGroupId"], Is.EqualTo(""));
            Assert.That(cases[2]["chosenGroupId"], Is.EqualTo("pair"));
        }
        // p1.1.1 file: a missing optional id is an empty string; null is written only for "bindings" (a mimic copies
        // its right neighbour), for a binding without a target, and for the card ids of an additive entry that has no
        // card behind it (colour, gold face). Every null token of the raw text must be one of those.
        [Test]
        public void GoldenJson_AllOriginalNullTokensAreAccountedFor()
        {
            string json = File.ReadAllText(DataPath(V111Golden.FileName));
            int allNulls = Regex.Matches(json, ":\\s*null(?=\\s*[,}])").Count;
            int adjacency = 0, unbound = 0, cardless = 0;
            Action<object> entries = block =>
            {
                if (block == null || !V111Golden.Obj(block).ContainsKey("additiveEntries")) return;
                foreach (var entry in V111Golden.List(V111Golden.Obj(block)["additiveEntries"]).Select(V111Golden.Obj))
                {
                    string kind = V111Golden.Str(entry, "kind"); bool noCard = kind == "color" || kind == "gold";
                    Assert.That(entry["emitterId"] == null, Is.EqualTo(noCard), kind + " emitterId"); Assert.That(entry["stateOwnerId"] == null, Is.EqualTo(noCard), kind + " stateOwnerId");
                    if (noCard) cardless += 2;
                }
            };
            foreach (var example in V111Golden.Cases)
            {
                if (example.input["bindings"] == null) adjacency++; else unbound += V111Golden.Obj(example.input["bindings"]).Values.Count(v => v == null);
                entries(example.expected);
                if (example.handChecks != null && example.handChecks.ContainsKey("expected")) entries(example.handChecks["expected"]);
                Assert.That(example.expected["chosenGroupId"], Is.Not.Null, example.id); Assert.That(example.expected["colorId"], Is.Not.Null, example.id);
                Assert.That(V111Golden.Obj(example.expected["afterScorePlan"])["upgradeHandId"], Is.Not.Null, example.id);
                Assert.That(example.input["previousPrimary"], Is.Not.Null, example.id);
                Assert.That(example.expected["primaryHandId"], Is.Not.Null.And.Not.Empty, example.id); Assert.That(example.expected["score"], Is.Not.Null.And.Not.Empty, example.id);
            }
            Assert.That(adjacency, Is.GreaterThan(0)); Assert.That(unbound, Is.GreaterThan(0)); Assert.That(cardless, Is.GreaterThan(0));
            Assert.That(adjacency + unbound + cardless, Is.EqualTo(allNulls), "A null token outside the three documented places.");
        }
        [Test]
        public void Configuration_ContainsExactly71SupportedItemsAnd13Hands2Colors()
        {
            Assert.That(config.controllers.Length + config.consumables.Length + config.services.Length + config.diceUpgrades.Length, Is.EqualTo(185)); // p1.1.1: 150 controllers, 13 consumables, 11 services, 11 die upgrades
            Assert.That(config.handDefinitions.Length, Is.EqualTo(13));
        }
        [Test]
        public void Configuration_RejectsMissingZeroOrFalseFields()
        {
            string json = File.ReadAllText(DataPath("P0Config.json"));
            Assert.Throws<ArgumentException>(() => GameConfig.ValidateJson(json.Replace("\"index\": 0", "\"wrongIndex\": 0")));
            Assert.Throws<ArgumentException>(() => GameConfig.ValidateJson(json.Replace("\"purchasedDiceChangeCapacity\": false,", "")));
        }
        [Test]
        public void Configuration_RejectsUnknownMissingDuplicateIds()
        {
            config.controllers[0].id = "C99";
            Assert.Throws<InvalidOperationException>(() => config.Validate());
            config = ReadConfig(); config.controllers[0].id = "C02";
            Assert.Throws<InvalidOperationException>(() => config.Validate());
            config = ReadConfig(); config.consumables = config.consumables.Take(6).ToArray();
            Assert.Throws<InvalidOperationException>(() => config.Validate());
        }
        [TestCase("{}")]
        [TestCase("{\"configVersion\":null}")]
        [TestCase("{\"a\":1,\"a\":2}")]
        [TestCase("{\"a\":1,}")]
        [TestCase("[]")]
        public void Configuration_RejectsMalformedOrIncompleteJson(string json)
            => Assert.Throws<ArgumentException>(() => GameConfig.ValidateJson(json));
        [Test]
        public void Configuration_RejectsInvalidWeightsAndFaces()
        {
            config.smoothOtherWeight100 = 0;
            Assert.Throws<InvalidOperationException>(() => config.Validate());
            config = ReadConfig(); config.diceDefinition.faces[0].pipValue = 7;
            Assert.Throws<InvalidOperationException>(() => config.Validate());
        }

        [TestCase("none", false, false, new[] { 166667, 166667, 166667, 166667, 166666, 166666 })]
        [TestCase("smooth", false, false, new[] { 173334, 173334, 173333, 173333, 173333, 133333 })]
        [TestCase("rough", false, false, new[] { 160000, 160000, 160000, 160000, 160000, 200000 })]
        [TestCase("none", true, false, new[] { 153846, 153846, 153846, 153846, 153846, 230770 })]
        [TestCase("none", true, true, new[] { 214286, 142857, 142857, 142857, 142857, 214286 })]
        public void Probability_FiveContractVectors(string texture, bool large, bool small, int[] expected)
        {
            var faces = DiceFactory.CreateStandard(config, 1).faces;
            faces[0].texture = texture;
            CollectionAssert.AreEqual(expected, ProbabilityCalculator.BuildUnits(faces, large, small, config));
        }
        [Test]
        public void Probability_All2916TextureAndBuffCombinations()
        {
            var faces = DiceFactory.CreateStandard(config, 1).faces;
            string[] textures = { "none", "smooth", "rough" };
            int tested = 0, minimum = int.MaxValue;
            for (int pattern = 0; pattern < 729; pattern++)
            {
                int rest = pattern;
                for (int face = 0; face < 6; face++) { faces[face].texture = textures[rest % 3]; rest /= 3; }
                for (int buffs = 0; buffs < 4; buffs++)
                {
                    int[] units = ProbabilityCalculator.BuildUnits(faces, (buffs & 1) != 0, (buffs & 2) != 0, config);
                    Assert.That(units.Sum(), Is.EqualTo(1000000), "Pattern " + pattern + " buffs " + buffs);
                    Assert.That(units.Min(), Is.GreaterThanOrEqualTo(1));
                    minimum = Math.Min(minimum, units.Min()); tested++;
                }
            }
            Assert.That(tested, Is.EqualTo(2916));
#if UNITY_5_3_OR_NEWER
            TestContext.WriteLine("2916 combinations; smallest face allocation = " + minimum);
#else
            Console.WriteLine("2916 combinations; smallest face allocation = " + minimum);
#endif
        }
        [Test]
        public void Probability_TieBreakUsesPhysicalFaceId_NotArrayOrder()
        {
            var faces = DiceFactory.CreateStandard(config, 1).faces.Reverse().ToArray();
            CollectionAssert.AreEqual(new[] { 166666, 166666, 166667, 166667, 166667, 166667 }, ProbabilityCalculator.BuildUnits(faces, false, false, config));
        }
        [Test]
        public void Probability_RepeatedPreviewDoesNotCompoundOrMutate()
        {
            var die = DiceFactory.CreateStandard(config, 1);
            for (int i = 0; i < 6; i++) die.faces[i].texture = i % 2 == 0 ? "smooth" : "rough";
            string before = JsonUtility.ToJson(die);
            int[] expected = ProbabilityCalculator.BuildUnits(die.faces, true, true, config);
            for (int i = 0; i < 20; i++) CollectionAssert.AreEqual(expected, ProbabilityCalculator.BuildUnits(die.faces, true, true, config));
            Assert.That(JsonUtility.ToJson(die), Is.EqualTo(before));
        }
        [Test]
        public void Probability_UsesPermanentPips()
        {
            var die = DiceFactory.CreateStandard(config, 1);
            foreach (var face in die.faces) face.permanentPip = 1;
            // A remote display of 6 is deliberately absent from physical probability inputs.
            CollectionAssert.AreEqual(ProbabilityCalculator.BuildUnits(die.faces, false, false, config), ProbabilityCalculator.BuildUnits(die.faces, true, false, config));
        }
        [Test]
        public void Probability_RejectsIllegalPhysicalData()
        {
            var die = DiceFactory.CreateStandard(config, 1);
            die.faces[1].faceId = "F01";
            Assert.Throws<InvalidOperationException>(() => ProbabilityCalculator.BuildUnits(die.faces, false, false, config));
        }
        [Test]
        public void Rng_ExactFirstFiveOutputs()
        {
            var rng = new GameRng(1);
            CollectionAssert.AreEqual(new uint[] { 270369, 67634689, 2647435461, 307599695, 2398689233 }, Enumerable.Range(0, 5).Select(_ => rng.NextNonZeroUInt()).ToArray());
        }
        [Test]
        public void Rng_RejectionSamplingContractAndClone()
        {
            var rng = new GameRng(1);
            var clone = rng.Clone();
            for (int i = 0; i < 500; i++)
            {
                uint limit = (uint.MaxValue / 1000000u) * 1000000u, u;
                do { u = clone.NextNonZeroUInt() - 1; } while (u >= limit);
                Assert.That(rng.NextIndex(1000000), Is.EqualTo((int)(u % 1000000u)));
                Assert.That(rng.State, Is.EqualTo(clone.State));
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextIndex(0));
            Assert.That(new GameRng(0).State, Is.EqualTo(0x9E3779B9u));
        }
        [Test]
        public void Rng_SeedOnlyAcceptsUintDecimal()
        {
            Assert.That(GameRng.TryParseSeed("4294967295", out uint value), Is.True); Assert.That(value, Is.EqualTo(uint.MaxValue));
            foreach (string invalid in new[] { "", "-1", "+1", "1.0", "word", "4294967296", " 1" }) Assert.That(GameRng.TryParseSeed(invalid, out _), Is.False, invalid);
        }
        [Test]
        public void FactoryAndClone_HaveNoSharedMutableFaces()
        {
            var first = DiceFactory.CreateStandard(config, 1);
            var second = DiceFactory.CreateStandard(config, 2);
            var clone = first.Clone();
            first.faces[0].permanentPip = 6;
            Assert.That(second.faces[0].permanentPip, Is.EqualTo(1));
            Assert.That(clone.faces[0].permanentPip, Is.EqualTo(1));
            Assert.That(config.diceDefinition.faces[0].pipValue, Is.EqualTo(1));
        }
        [Test]
        public void Score_RejectsEmptyDuplicateOverCapacityAndInvalidLevels()
        {
            var example = V111Golden.Case("L08-G001"); // p1.1.1 (W4b): the same first vector, read from the new file
            var snapshot = V111Golden.Snapshot(example, config);
            Assert.DoesNotThrow(() => ScoreCalculator.Evaluate(snapshot, config), "The template itself must be legal.");
            snapshot.dice = Array.Empty<SubmissionDie>();
            Assert.Throws<ArgumentException>(() => ScoreCalculator.Evaluate(snapshot, config));
            snapshot = V111Golden.Snapshot(example, config); snapshot.dice = new[] { snapshot.dice[0], snapshot.dice[0] };
            Assert.Throws<ArgumentException>(() => ScoreCalculator.Evaluate(snapshot, config));
            snapshot = V111Golden.Snapshot(example, config); snapshot.capacity = 0;
            Assert.Throws<ArgumentException>(() => ScoreCalculator.Evaluate(snapshot, config));
            snapshot = V111Golden.Snapshot(example, config); snapshot.handLevels = new[] { new HandLevel { handId = "pair", level = 13 } }; // p1.1.1: the normal cap is 12
            Assert.Throws<ArgumentException>(() => ScoreCalculator.Evaluate(snapshot, config));
        }
        [Test]
        public void Score_EqualUpgradedMultipliersPreferHigherStructuralRank()
        {
            var snapshot = new SubmissionSnapshot
            {
                capacity = 6,
                handLevels = new[] { new HandLevel { handId = "full_house", level = 1 } },
                dice = new[] { 4, 4, 4, 4, 2, 2 }.Select((p, i) => new SubmissionDie
                {
                    dieId = "D" + (i + 1).ToString("D3"),
                    faceId = "F01",
                    currentPip = p,
                    currentColor = "blue",
                    permanentSixFaceSum = 21
                }).ToArray()
            };
            var actual = ScoreCalculator.Evaluate(snapshot, config);
            Assert.That(actual.chosenGroupId, Is.EqualTo("quads"));
            Assert.That(actual.groupMult100, Is.EqualTo(800));
            Assert.That(actual.score, Is.EqualTo("200")); // p1.1.1: 20 x 8 x 1.25 (all blue adds 0.25)
        }
    }
}
