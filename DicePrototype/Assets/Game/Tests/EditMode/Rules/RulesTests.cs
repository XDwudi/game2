using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Core.Tests
{
    [Serializable] public sealed class GoldenFile { public GoldenCase[] cases; }
    [Serializable] public sealed class GoldenCase
    {
        public string id, label;
        public GoldenDie[] dice;
        public int rollsRemaining, capacity, submissionOrdinal;
        public string[] controllerIds;
        public GoldenLevels levels;
        public GoldenEnchants enchants;
        public bool redWave, requiresDebugSlots;
        public ScoreBreakdown expected;
        public SubmissionSnapshot Snapshot(GameConfig config)
        {
            return new SubmissionSnapshot
            {
                runId = "golden", stageId = "STAGE_01", handId = id, configVersion = config.configVersion,
                dice = dice.Select((d, i) => new SubmissionDie
                {
                    dieId = "D" + (i + 1).ToString("D3"), faceId = "F01", currentPip = d.p,
                    currentColor = d.c, isGold = d.gold, isGlass = d.glass,
                    permanentSixFaceSum = d.sum6 == 0 ? 21 : d.sum6, lifetime = "permanent"
                }).ToArray(),
                capacity = capacity, rollsRemaining = rollsRemaining, redWave = redWave,
                equippedControllers = controllerIds.Select((cid, i) => new ControllerInstance { instanceId = "G-" + i, definitionId = cid, acquiredSerial = i + 1, enchantmentId = enchants.Get(cid) }).ToArray(),
                submissionOrdinalBefore = submissionOrdinal, handLevels = levels.ToArray()
            };
        }
    }
    [Serializable] public sealed class GoldenDie { public int p, sum6; public string c; public bool gold, glass; }
    [Serializable] public sealed class GoldenLevels
    {
        public int pair, two_pair, trips, full_house, quads, five_kind, six_kind, seven_kind, eight_kind, straight, all_blue, all_red;
        public HandLevel[] ToArray() => GetType().GetFields().Select(f => new HandLevel { handId = f.Name, level = (int)f.GetValue(this) }).ToArray();
    }

    [Serializable] public sealed class GoldenEnchants
    {
        public string C01,C02,C03,C04,C05,C06,C07,C08,C09,C10,C11,C12,C13,C14,C15,C16,C17,C18,C19,C20,C21,C22,C23,C24,C25,C26,C27,C28,C29,C30,C31,C32,C33,C34,C35,C36,C37,C38,C39,C40;
        public string Get(string id) => string.IsNullOrEmpty((string)GetType().GetField(id).GetValue(this)) ? "none" : (string)GetType().GetField(id).GetValue(this);
    }

    public static class GoldenJsonLoader
    {
        // Unity JsonUtility maps a JSON null string to "". Preserve the source token before
        // deserialization so a genuinely empty expected string is never treated as null.
        public static GoldenFile Read(string json)
        {
            const string marker = "__DICECRAFT_GOLDEN_NULL_GROUP__";
            if (json.Contains(marker)) throw new ArgumentException("Reserved golden JSON marker.");
            string encoded = Regex.Replace(json, "(\"chosenGroupId\"\\s*:\\s*)null(?=\\s*[,}])", "$1\"" + marker + "\"");
            if (encoded.TrimStart().StartsWith("[", StringComparison.Ordinal)) encoded = "{\"cases\":" + encoded + "}";
            var file = JsonUtility.FromJson<GoldenFile>(encoded);
            foreach (var item in file.cases)
                if (item.expected.chosenGroupId == marker) item.expected.chosenGroupId = null;
            return file;
        }
    }

    public class RulesTests
    {
        private GameConfig config;
        private static string DataPath(string name) => Path.Combine(Application.dataPath, "Game", "Data", name);
        private static GameConfig ReadConfig()
        {
            string json = File.ReadAllText(DataPath("P0Config.json"));
            GameConfig.ValidateJson(json);
            var result = JsonUtility.FromJson<GameConfig>(json);
            result.Validate();
            return result;
        }
        [SetUp] public void Setup() { config = ReadConfig(); }
        public static IEnumerable Goldens()
        {
            var cases = GoldenJsonLoader.Read(File.ReadAllText(DataPath("GoldenScores.json"))).cases;
            foreach (var item in cases) yield return new TestCaseData(item).SetName("GOLD-" + item.id);

        }

        [TestCaseSource(nameof(Goldens))]
        public void Golden_All26Fields_AndNoMutation(GoldenCase example)
        {
            var snapshot = example.Snapshot(config);
            string before = JsonUtility.ToJson(snapshot);
            var actual = ScoreCalculator.Evaluate(snapshot, config);
            string[] fields = typeof(ScoreBreakdown).GetFields().Select(f => f.Name).ToArray();
            foreach (string name in fields)
            {
                var field = typeof(ScoreBreakdown).GetField(name);
                object expected = field.GetValue(example.expected), value = field.GetValue(actual);
                if (field.FieldType.IsArray) CollectionAssert.AreEqual((IEnumerable)expected, (IEnumerable)value, example.id + "." + name);
                else Assert.That(value, Is.EqualTo(expected), example.id + "." + name);
            }
            Assert.That(JsonUtility.ToJson(snapshot), Is.EqualTo(before), "Pure preview mutated input.");
        }

        [Test] public void GoldenFile_HasExactly100UniqueCases()
        {
            var cases = GoldenJsonLoader.Read(File.ReadAllText(DataPath("GoldenScores.json"))).cases;
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 100).Select(i => "S" + i.ToString("D3")), cases.Select(c => c.id));
        }
        [Test] public void GoldenJson_PreservesNullEmptyAndNonemptyGroupIdsExactly()
        {
            var file = GoldenJsonLoader.Read("{\"cases\":[{\"expected\":{\"chosenGroupId\":null}},{\"expected\":{\"chosenGroupId\":\"\"}},{\"expected\":{\"chosenGroupId\":\"pair\"}}]}");
            Assert.That(file.cases[0].expected.chosenGroupId, Is.Null);
            Assert.That(file.cases[1].expected.chosenGroupId, Is.EqualTo(""));
            Assert.That(file.cases[2].expected.chosenGroupId, Is.EqualTo("pair"));
        }
        [Test] public void GoldenJson_AllOriginalNullTokensAreAccountedFor()
        {
            string json = File.ReadAllText(DataPath("GoldenScores.json"));
            int allNulls = Regex.Matches(json, ":\\s*null(?=\\s*[,}])").Count;
            int groupNulls = Regex.Matches(json, "\"chosenGroupId\"\\s*:\\s*null(?=\\s*[,}])").Count;
            Assert.That(groupNulls, Is.GreaterThan(0));
            Assert.That(allNulls, Is.EqualTo(groupNulls), "A newly nullable source field needs an explicit loading adapter.");
            Assert.That(GoldenJsonLoader.Read(json).cases.Count(c => c.expected.chosenGroupId == null), Is.EqualTo(groupNulls));
            Assert.That(GoldenJsonLoader.Read(json).cases.All(c => c.expected.redGroupingMode != null && c.expected.scoreNumerator != null && c.expected.scoreDenominator != null), Is.True);
        }
        [Test] public void Configuration_ContainsExactly58SupportedItemsAnd12Hands()
        {
            Assert.That(config.controllers.Length + config.consumables.Length + config.services.Length + config.diceUpgrades.Length, Is.EqualTo(58));
            Assert.That(config.handDefinitions.Length, Is.EqualTo(12));
        }
        [Test] public void Configuration_RejectsMissingZeroOrFalseFields()
        {
            string json = File.ReadAllText(DataPath("P0Config.json"));
            Assert.Throws<ArgumentException>(() => GameConfig.ValidateJson(json.Replace("\"occupiedIndex\": 0", "\"wrongIndex\": 0")));
            Assert.Throws<ArgumentException>(() => GameConfig.ValidateJson(json.Replace("\"purchasedDiceChangeCapacity\": false,", "")));
        }
        [Test] public void Configuration_RejectsUnknownMissingDuplicateIds()
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
        [Test] public void Configuration_RejectsInvalidWeightsAndFaces()
        {
            config.smoothOtherWeight100 = 0;
            Assert.Throws<InvalidOperationException>(() => config.Validate());
            config = ReadConfig(); config.diceDefinition.faces[0].pipValue = 7;
            Assert.Throws<InvalidOperationException>(() => config.Validate());
        }

        [TestCase("none", false, false, new[] {166667,166667,166667,166667,166666,166666})]
        [TestCase("smooth", false, false, new[] {173334,173334,173333,173333,173333,133333})]
        [TestCase("rough", false, false, new[] {160000,160000,160000,160000,160000,200000})]
        [TestCase("none", true, false, new[] {153846,153846,153846,153846,153846,230770})]
        [TestCase("none", true, true, new[] {214286,142857,142857,142857,142857,214286})]
        public void Probability_FiveContractVectors(string texture, bool large, bool small, int[] expected)
        {
            var faces = DiceFactory.CreateStandard(config, 1).faces;
            faces[0].texture = texture;
            CollectionAssert.AreEqual(expected, ProbabilityCalculator.BuildUnits(faces, large, small, config));
        }
        [Test] public void Probability_All2916TextureAndBuffCombinations()
        {
            var faces = DiceFactory.CreateStandard(config, 1).faces;
            string[] textures = {"none","smooth","rough"};
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
        [Test] public void Probability_TieBreakUsesPhysicalFaceId_NotArrayOrder()
        {
            var faces = DiceFactory.CreateStandard(config, 1).faces.Reverse().ToArray();
            CollectionAssert.AreEqual(new[] {166666,166666,166667,166667,166667,166667}, ProbabilityCalculator.BuildUnits(faces, false, false, config));
        }
        [Test] public void Probability_RepeatedPreviewDoesNotCompoundOrMutate()
        {
            var die = DiceFactory.CreateStandard(config, 1);
            for (int i = 0; i < 6; i++) die.faces[i].texture = i % 2 == 0 ? "smooth" : "rough";
            string before = JsonUtility.ToJson(die);
            int[] expected = ProbabilityCalculator.BuildUnits(die.faces, true, true, config);
            for (int i = 0; i < 20; i++) CollectionAssert.AreEqual(expected, ProbabilityCalculator.BuildUnits(die.faces, true, true, config));
            Assert.That(JsonUtility.ToJson(die), Is.EqualTo(before));
        }
        [Test] public void Probability_UsesPermanentPips()
        {
            var die = DiceFactory.CreateStandard(config, 1);
            foreach (var face in die.faces) face.permanentPip = 1;
            // A remote display of 6 is deliberately absent from physical probability inputs.
            CollectionAssert.AreEqual(ProbabilityCalculator.BuildUnits(die.faces, false, false, config), ProbabilityCalculator.BuildUnits(die.faces, true, false, config));
        }
        [Test] public void Probability_RejectsIllegalPhysicalData()
        {
            var die = DiceFactory.CreateStandard(config, 1);
            die.faces[1].faceId = "F01";
            Assert.Throws<InvalidOperationException>(() => ProbabilityCalculator.BuildUnits(die.faces, false, false, config));
        }
        [Test] public void Rng_ExactFirstFiveOutputs()
        {
            var rng = new GameRng(1);
            CollectionAssert.AreEqual(new uint[] {270369,67634689,2647435461,307599695,2398689233}, Enumerable.Range(0, 5).Select(_ => rng.NextNonZeroUInt()).ToArray());
        }
        [Test] public void Rng_RejectionSamplingContractAndClone()
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
        [Test] public void Rng_SeedOnlyAcceptsUintDecimal()
        {
            Assert.That(GameRng.TryParseSeed("4294967295", out uint value), Is.True); Assert.That(value, Is.EqualTo(uint.MaxValue));
            foreach (string invalid in new[] {"", "-1", "+1", "1.0", "word", "4294967296", " 1"}) Assert.That(GameRng.TryParseSeed(invalid, out _), Is.False, invalid);
        }
        [Test] public void FactoryAndClone_HaveNoSharedMutableFaces()
        {
            var first = DiceFactory.CreateStandard(config, 1);
            var second = DiceFactory.CreateStandard(config, 2);
            var clone = first.Clone();
            first.faces[0].permanentPip = 6;
            Assert.That(second.faces[0].permanentPip, Is.EqualTo(1));
            Assert.That(clone.faces[0].permanentPip, Is.EqualTo(1));
            Assert.That(config.diceDefinition.faces[0].pipValue, Is.EqualTo(1));
        }
        [Test] public void Score_RejectsEmptyDuplicateOverCapacityAndInvalidLevels()
        {
            var example = GoldenJsonLoader.Read(File.ReadAllText(DataPath("GoldenScores.json"))).cases[0];
            var snapshot = example.Snapshot(config);
            snapshot.dice = Array.Empty<SubmissionDie>();
            Assert.Throws<ArgumentException>(() => ScoreCalculator.Evaluate(snapshot, config));
            snapshot = example.Snapshot(config); snapshot.dice = new[] {snapshot.dice[0], snapshot.dice[0]};
            Assert.Throws<ArgumentException>(() => ScoreCalculator.Evaluate(snapshot, config));
            snapshot = example.Snapshot(config); snapshot.capacity = 0;
            Assert.Throws<ArgumentException>(() => ScoreCalculator.Evaluate(snapshot, config));
            snapshot = example.Snapshot(config); snapshot.handLevels = new[] {new HandLevel {handId="pair", level=9}};
            Assert.Throws<ArgumentException>(() => ScoreCalculator.Evaluate(snapshot, config));
        }
        [Test] public void Score_EqualUpgradedMultipliersPreferHigherStructuralRank()
        {
            var snapshot = new SubmissionSnapshot
            {
                capacity = 6, handLevels = new[] {new HandLevel {handId = "full_house", level = 1}},
                dice = new[] {4,4,4,4,2,2}.Select((p, i) => new SubmissionDie
                {
                    dieId = "D" + (i + 1).ToString("D3"), faceId = "F01", currentPip = p,
                    currentColor = "blue", permanentSixFaceSum = 21
                }).ToArray()
            };
            var actual = ScoreCalculator.Evaluate(snapshot, config);
            Assert.That(actual.chosenGroupId, Is.EqualTo("quads"));
            Assert.That(actual.groupMult100, Is.EqualTo(800));
            Assert.That(actual.score, Is.EqualTo(220));
        }
    }
}
