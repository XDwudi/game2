using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Core.Tests
{
    public sealed class ControllerScoreTests
    {
        private GameConfig config;
        [SetUp] public void Setup()
        {
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "Game", "Data", "P0Config.json"));
            GameConfig.ValidateJson(json);
            config = JsonUtility.FromJson<GameConfig>(json);
            config.Validate();
        }
        private static TestCaseData Case(string id, string label, string pips, string colors, long score,
            int capacity = 5, int permanentSum = 21, bool equipped = true, string expectedPips = null, string expectedColors = null)
            => new TestCaseData(id, pips, colors, score, capacity, permanentSum, equipped, expectedPips, expectedColors).SetName("CONTENT-" + id + "-" + label);
        public static IEnumerable Cases()
        {
            yield return Case("C02", "hit", "2,3", "BB", 10);
            yield return Case("C02", "miss-red", "2,3", "BR", 5);
            yield return Case("C02", "boundary-single", "2", "B", 4);
            yield return Case("C03", "hit", "4,4", "BB", 16, expectedColors: "RB");
            yield return Case("C03", "not-equipped", "4,4", "BB", 16, equipped: false, expectedColors: "BB");
            yield return Case("C03", "boundary-single", "2", "B", 2, expectedColors: "R");
            yield return Case("C06", "hit", "1,2,3,4", "RBBR", 30);
            yield return Case("C06", "miss-three-distinct", "1,2,3", "RBB", 6);
            yield return Case("C06", "boundary-duplicate-with-four-distinct", "1,2,3,3,4", "RBBBR", 78);
            yield return Case("C08", "hit", "3", "B", 6);
            yield return Case("C08", "miss-full", "1,2,3,4,6", "RBBRB", 16);
            yield return Case("C08", "boundary-one-empty-slot", "2,2,3,3", "BBBB", 60);
            yield return Case("C09", "hit", "2", "B", 6, capacity: 4);
            yield return Case("C09", "not-equipped", "2", "B", 2, equipped: false);
            yield return Case("C09", "boundary-full-reduced-capacity", "1,2,3,4", "RBBR", 30, capacity: 4);
            yield return Case("C10", "hit", "1", "R", 5);
            yield return Case("C10", "miss-pip-two", "2", "B", 2);
            yield return Case("C10", "boundary-contribution-does-not-change-pair", "1,1", "RB", 20);
            yield return Case("C11", "hit", "2,2,3,3", "BBBB", 150);
            yield return Case("C11", "miss-one-three", "2,2,3,4", "BBBR", 22);
            yield return Case("C11", "boundary-extra-die-allowed", "2,2,3,3,1", "BBBBR", 165);
            yield return Case("C12", "hit", "1,2", "RB", 8, expectedPips: "2,2");
            yield return Case("C12", "miss-even-unchanged", "2,4", "BB", 6, expectedPips: "2,4");
            yield return Case("C12", "boundary-five-to-six", "5,6", "BB", 24, expectedPips: "6,6");
            yield return Case("C13", "hit", "3", "B", 5);
            yield return Case("C13", "miss-even", "4", "B", 4);
            yield return Case("C13", "boundary-red-group-uses-original-one", "1,1", "RR", 15);
            yield return Case("C14", "hit", "3", "B", 6);
            yield return Case("C14", "miss-mixed-parity", "3,2", "BB", 5);
            yield return Case("C14", "boundary-single-one", "1", "R", 2);
            yield return Case("C17", "hit", "3", "B", 6);
            yield return Case("C17", "miss-four", "4", "B", 4);
            yield return Case("C17", "boundary-all-three-small-values", "1,2,3", "RBB", 15);
            yield return Case("C18", "hit", "2,2", "RB", 16);
            yield return Case("C18", "miss-unequal", "2,2,2", "RRB", 30);
            yield return Case("C18", "boundary-two-different-pips", "3,6", "RB", 18);
            yield return Case("C19", "hit-threshold", "1", "R", 6, permanentSum: 13);
            yield return Case("C19", "miss-above-threshold", "1", "R", 1, permanentSum: 14);
            yield return Case("C19", "boundary-remote-current-six-permanent-ones", "6", "B", 11, permanentSum: 6);
            yield return Case("C20", "hit", "1,2,3,4,6", "RBBRB", 22);
            yield return Case("C20", "miss-last-five", "1,2,3,4,5", "RBBRB", 90);
            yield return Case("C20", "boundary-last-occupied-not-capacity-slot", "6", "B", 6);
        }

        [TestCaseSource(nameof(Cases))]
        public void ExplicitControllerOutcomes(string id, string pipText, string colors, long expectedScore,
            int capacity, int permanentSum, bool equipped, string expectedPips, string expectedColors)
        {
            int[] pips = pipText.Split(',').Select(int.Parse).ToArray();
            var snapshot = new SubmissionSnapshot
            {
                configVersion = config.configVersion, capacity = capacity,
                equippedControllerIds = equipped ? new[] {id} : Array.Empty<string>(),
                dice = pips.Select((p, i) => new SubmissionDie
                {
                    dieId = "D" + (i + 1).ToString("D3"), faceId = "F01", currentPip = p,
                    currentColor = colors[i] == 'R' ? "red" : "blue", permanentSixFaceSum = permanentSum
                }).ToArray()
            };
            string before = JsonUtility.ToJson(snapshot);
            ScoreBreakdown actual = ScoreCalculator.Evaluate(snapshot, config);
            Assert.That(actual.score, Is.EqualTo(expectedScore));
            if (expectedPips != null) CollectionAssert.AreEqual(expectedPips.Split(',').Select(int.Parse), actual.effectivePips);
            if (expectedColors != null) CollectionAssert.AreEqual(expectedColors.Select(c => c == 'R' ? "red" : "blue"), actual.effectiveColors);
            Assert.That(JsonUtility.ToJson(snapshot), Is.EqualTo(before));
        }
    }
}
