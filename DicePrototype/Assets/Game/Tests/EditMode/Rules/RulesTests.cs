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
    [Serializable]
    public sealed class GoldenCase
    {
        public string id, description;
        public GoldenInput input;
        public ScoreBreakdown expected;
        public SubmissionSnapshot Snapshot(GameConfig cfg)
        {
            var cards = input.controllers ?? Array.Empty<ControllerInstance>();
            foreach (var card in cards) { if (card.state == null) card.state = new ControllerDynamicState(); if (card.state.turnoverUnits2 == 0) card.state.turnoverUnits2 = 3; }
            return new SubmissionSnapshot { configVersion = cfg.configVersion, capacity = input.capacity, rollsRemaining = input.rolls_remaining, redWave = input.red_wave, submissionOrdinalBefore = input.submission_ordinal, wallet = input.wallet, previousPrimaryHandId = input.previous_primary, debug = input.debug, equippedControllers = cards.Select(c => c.Clone()).ToArray(), handLevels = input.levels.ToArray(false), colorLevels = input.color_levels.ToArray(true), mimicBindings = input.bindings.ToBindings(), resolvedRandomChoices = input.random_units2.ToChoices(), dice = input.dice.Select((d, i) => new SubmissionDie { dieId = "D" + i, faceId = "F01", currentPip = d.p, currentColor = d.c, permanentSixFaceSum = d.sum6, isGlass = d.glass, isGold = d.gold }).ToArray() };
        }
    }
    [Serializable]
    public sealed class GoldenInput
    {
        public GoldenDie[] dice; public ControllerInstance[] controllers; public int rolls_remaining, capacity, submission_ordinal; public bool red_wave, debug; public long wallet; public string previous_primary; public GoldenLevels levels, color_levels; public GoldenBindings bindings; public GoldenRandom random_units2;
    }
    [Serializable] public sealed class GoldenDie { public int p, sum6; public string c; public bool gold, glass; }
    [Serializable]
    public sealed class GoldenLevels
    {
        public int pair, two_pair, trips, full_house, three_pair, quads, two_trips, five_kind, four_pair, six_kind, seven_kind, eight_kind, straight, all_blue, all_red;
        public HandLevel[] ToArray(bool colors) => GetType().GetFields().Where(f => (f.Name == "all_blue" || f.Name == "all_red") == colors).Select(f => new HandLevel { handId = f.Name, level = (int)f.GetValue(this) }).ToArray();
    }
    [Serializable]
    public sealed class GoldenBindings
    {
        public string ctl_01, ctl_02, ctl_03, m;
        public MimicBinding[] ToBindings() => GetType().GetFields().Where(f => !string.IsNullOrEmpty((string)f.GetValue(this))).Select(f => new MimicBinding { sourceInstanceId = f.Name, targetInstanceId = (string)f.GetValue(this) == GoldenJsonLoader.NullMarker ? null : (string)f.GetValue(this) }).ToArray();
    }
    [Serializable]
    public sealed class GoldenRandom
    {
        public int ctl_01, ctl_02, ctl_04;
        public RandomMultiplierChoice[] ToChoices() => GetType().GetFields().Where(f => (int)f.GetValue(this) != 0).Select(f => new RandomMultiplierChoice { ownerInstanceId = f.Name, units2 = (int)f.GetValue(this) }).ToArray();
    }
    public static class GoldenJsonLoader
    {
        public const string NullMarker = "__DICECRAFT_GOLDEN_NULL__";
        public static GoldenFile Read(string json)
        {
            if (json.Contains(NullMarker)) throw new ArgumentException("Reserved golden JSON marker.");
            string encoded = Regex.Replace(json, ":\\s*null(?=\\s*[,}])", ":\"" + NullMarker + "\"");
            if (encoded.TrimStart().StartsWith("[", StringComparison.Ordinal)) encoded = "{\"cases\":" + encoded + "}";
            var file = JsonUtility.FromJson<GoldenFile>(encoded);
            foreach (var item in file.cases)
            {
                if (item.expected != null) { if (item.expected.chosenGroupId == NullMarker) item.expected.chosenGroupId = null; if (item.expected.colorId == NullMarker) item.expected.colorId = null; if (item.expected.afterScorePlan != null && item.expected.afterScorePlan.upgradeHandId == NullMarker) item.expected.afterScorePlan.upgradeHandId = null; }
                if (item.input != null && item.input.previous_primary == NullMarker) item.input.previous_primary = null;
            }
            return file;
        }
        public static readonly string[] ExpectedFields = { "score", "baseTotal", "pipContributions", "pairKinds", "pairBaseAdd", "walletBaseAdd", "emptySlotBaseAdd", "steadyBaseAdd", "enchantBaseAdd", "chosenGroupId", "primaryHandId", "totalHandMult100", "colorId", "colorMult100", "redCount", "redMult100", "controllerMultiplierNumerator", "controllerMultiplierDenominator", "triggeredSourceIds", "enchantMultiplierCount", "afterScorePlan" };
        public static void AssertExpected(ScoreBreakdown expected, ScoreBreakdown actual, string id)
        {
            foreach (string name in ExpectedFields)
            {
                var f = typeof(ScoreBreakdown).GetField(name); object a = f.GetValue(expected), b = f.GetValue(actual);
                if (name == "afterScorePlan")
                {
                    foreach (var p in typeof(AfterScorePlan).GetFields())
                    { if (p.FieldType.IsArray) CollectionAssert.AreEqual((IEnumerable)p.GetValue(a), (IEnumerable)p.GetValue(b), id + "." + p.Name); else Assert.That(p.GetValue(b), Is.EqualTo(p.GetValue(a)), id + "." + p.Name); }
                }
                else if (f.FieldType.IsArray) CollectionAssert.AreEqual((IEnumerable)a, (IEnumerable)b, id + "." + name); else Assert.That(b, Is.EqualTo(a), id + "." + name);
            }
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
            var result = JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(json));
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
        public void Golden_All21Fields_Plan14_AndNoMutation(GoldenCase example)
        {
            var snapshot = example.Snapshot(config);
            string before = JsonUtility.ToJson(snapshot);
            var actual = ScoreCalculator.Evaluate(snapshot, config);
            GoldenJsonLoader.AssertExpected(example.expected, actual, example.id);
            Assert.That(JsonUtility.ToJson(snapshot), Is.EqualTo(before), "Pure preview mutated input.");
        }

        [Test]
        public void GoldenFile_HasExactly160UniqueCases()
        {
            var cases = GoldenJsonLoader.Read(File.ReadAllText(DataPath("GoldenScores.json"))).cases;
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 160).Select(i => "G" + i.ToString("D3")), cases.Select(c => c.id));
        }
        [Test]
        public void GoldenJson_PreservesNullEmptyAndNonemptyGroupIdsExactly()
        {
            var file = GoldenJsonLoader.Read("{\"cases\":[{\"expected\":{\"chosenGroupId\":null}},{\"expected\":{\"chosenGroupId\":\"\"}},{\"expected\":{\"chosenGroupId\":\"pair\"}}]}");
            Assert.That(file.cases[0].expected.chosenGroupId, Is.Null);
            Assert.That(file.cases[1].expected.chosenGroupId, Is.EqualTo(""));
            Assert.That(file.cases[2].expected.chosenGroupId, Is.EqualTo("pair"));
        }
        [Test]
        public void GoldenJson_AllOriginalNullTokensAreAccountedFor()
        {
            string json = File.ReadAllText(DataPath("GoldenScores.json"));
            int allNulls = Regex.Matches(json, ":\\s*null(?=\\s*[,}])").Count;
            int groupNulls = Regex.Matches(json, "\"chosenGroupId\"\\s*:\\s*null(?=\\s*[,}])").Count;
            Assert.That(groupNulls, Is.GreaterThan(0));
            Assert.That(allNulls, Is.GreaterThan(groupNulls), "v0.6 also has nullable color, previous primary and upgrade IDs.");
            Assert.That(GoldenJsonLoader.Read(json).cases.Count(c => c.expected.chosenGroupId == null), Is.EqualTo(groupNulls));
            Assert.That(GoldenJsonLoader.Read(json).cases.All(c => c.expected.primaryHandId != null && c.expected.score != null), Is.True);
        }
        [Test]
        public void Configuration_ContainsExactly71SupportedItemsAnd13Hands2Colors()
        {
            Assert.That(config.controllers.Length + config.consumables.Length + config.services.Length + config.diceUpgrades.Length, Is.EqualTo(71));
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
            var example = GoldenJsonLoader.Read(File.ReadAllText(DataPath("GoldenScores.json"))).cases[0];
            var snapshot = example.Snapshot(config);
            snapshot.dice = Array.Empty<SubmissionDie>();
            Assert.Throws<ArgumentException>(() => ScoreCalculator.Evaluate(snapshot, config));
            snapshot = example.Snapshot(config); snapshot.dice = new[] { snapshot.dice[0], snapshot.dice[0] };
            Assert.Throws<ArgumentException>(() => ScoreCalculator.Evaluate(snapshot, config));
            snapshot = example.Snapshot(config); snapshot.capacity = 0;
            Assert.Throws<ArgumentException>(() => ScoreCalculator.Evaluate(snapshot, config));
            snapshot = example.Snapshot(config); snapshot.handLevels = new[] { new HandLevel { handId = "pair", level = 9 } };
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
            Assert.That(actual.score, Is.EqualTo("480"));
        }
    }
}
