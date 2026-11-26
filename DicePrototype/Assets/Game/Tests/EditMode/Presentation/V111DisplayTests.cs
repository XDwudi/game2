using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DiceCraft.Core;
using DiceCraft.Core.Tests;
using DiceCraft.Presentation;
using NUnit.Framework;

namespace DiceCraft.Presentation.Tests
{
    // V1.1.1 task W7a (RULES_DELTA sections 2, 12.2 and 13): what the score readouts show multiplies to the real score.
    // The data models under test are the ones the interface draws from - ScoreFactors111 (formula chips, the formula
    // lines of the breakdown, the high-score celebration, the run statistics), ScoreExplanation's global steps (the
    // rows of the breakdown, with the bonus line and its entries) and the presentation receipt (the total multiplier
    // and the bonus events). No pixels: every case of GoldenScoresV111.json is recomputed and compared exactly.
    public sealed class V111DisplayTests
    {
        private GameConfig cfg;
        [SetUp] public void Setup() { cfg = V111Golden.ReadConfig(); }

        public static IEnumerable Cases()
        {
            foreach (var item in V111Golden.Cases) yield return new TestCaseData(item.id).SetName("DISPLAY111-" + item.id);
        }
        private static ExactFraction Score(ScoreBreakdown b) => new ExactFraction(ExactScoreMath.Parse(b.scoreNumerator), ExactScoreMath.Parse(b.scoreDenominator));
        private static void AssertSame(ExactFraction actual, ExactFraction expected, string what)
            => Assert.That(actual.ToString(), Is.EqualTo(expected.ToString()), what);

        [TestCaseSource(nameof(Cases))]
        public void DisplayV111_ShownFactorsMultiplyToTheScoreBeforeTheFloor(string id)
        {
            var example = V111Golden.Case(id); var snap = V111Golden.Snapshot(example, cfg); var b = ScoreCalculator.Evaluate(snap, cfg);
            Assert.That(b.score, Is.EqualTo(V111Golden.Str(example.expected, "score")), "The case itself must still be the golden one.");
            var expected = Score(b);
            Assert.That(ExactScoreMath.SaveDecimal(expected.Numerator / expected.Denominator), Is.EqualTo(b.score), "floor(numerator / denominator) is the score");

            // 1. The factor list: all of it, and with the x1 factors left out (chips and one-line formulas).
            var all = ScoreFactors111.List(b); var shown = ScoreFactors111.List(b, true);
            AssertSame(ScoreFactors111.Product(all), expected, "every factor");
            AssertSame(ScoreFactors111.Product(shown), expected, "the factors that are not x1");
            Assert.That(all.Select(f => f.Key).Take(5), Is.EqualTo(new[] { "base", "hand", "red", "additive", "controller" }), "order of the score");
            Assert.That(all.Skip(5).All(f => f.Key == "item" || f.Key == "boss"), Is.True);
            Assert.That(all.Count(f => f.Key == "additive"), Is.EqualTo(1), "one bonus line, also when A is 0");
            Assert.That(shown.Any(f => f.Key != "base" && f.Numerator == f.Denominator), Is.False, "no x1 among the shown factors");
            Assert.That(shown[0].Key, Is.EqualTo("base"), "the base is always shown");
            var bonus = all.Single(f => f.Key == "additive");
            AssertSame(new ExactFraction(bonus.Numerator, bonus.Denominator), new ExactFraction(100 + (BigInteger)b.additiveTotal100, 100), "the bonus line is 1 + A");
            Assert.That(bonus.Entries.Aggregate(BigInteger.Zero, (sum, e) => sum + e.amount100), Is.EqualTo((BigInteger)b.additiveTotal100), "the entries of the bonus line add up to A");
            Assert.That(bonus.Entries.Length, Is.EqualTo(b.additiveEntries.Length));
            AssertSame(ScoreFactors111.Product(new[] { all[0] }) * ScoreFactors111.Multiplier(b, true), expected, "base x total multiplier");

            // 2. The one-line formula: the product of the numbers it writes.
            var written = new List<ExactFraction>();
            string formula = HighScore073.Formula(b, n => Term(written, n, 100), (n, d) => Term(written, ExactScoreMath.Parse(n), ExactScoreMath.Parse(d)), n => Term(written, ExactScoreMath.Parse(n), 1),
                (key, value) => key + " " + value, f => Term(written, ExactScoreMath.Parse(f.numerator), ExactScoreMath.Parse(f.denominator)));
            AssertSame(written.Aggregate(new ExactFraction(1, 1), (a, f) => a * f), expected, "formula " + formula);
            Assert.That(formula.Split(new[] { "  ×  " }, StringSplitOptions.None).Length, Is.EqualTo(shown.Count), "one term per shown factor");
            Assert.That(formula.Contains("color") || formula.Contains("enchant"), Is.False, "no colour or enchantment factor of its own");

            // 3. The rows of the breakdown: a chain from the base to the score, with one bonus row that opens into its entries.
            var trace = ScoreExplanation.Build(snap, b, cfg); var rows = trace.global;
            for (int i = 1; i < rows.Length; i++) Assert.That(rows[i].before, Is.EqualTo(rows[i - 1].after), "row " + i + " continues the row before");
            Assert.That(rows[rows.Length - 1].kind, Is.EqualTo("floor")); Assert.That(rows[rows.Length - 1].after, Is.EqualTo(b.score));
            AssertSame(ExactFraction.Parse(rows[rows.Length - 1].before), expected, "the value before the floor");
            var line = rows.Single(r => r.kind == "additive");
            AssertSame(ExactFraction.Parse(line.operand), new ExactFraction(100 + (BigInteger)b.additiveTotal100, 100), "the bonus row multiplies by 1 + A");
            Assert.That(line.entries.Aggregate(BigInteger.Zero, (sum, e) => sum + e.amount100), Is.EqualTo((BigInteger)b.additiveTotal100), "the bonus row's entries add up to A");
            Assert.That(rows.Any(r => r.kind == "color" || r.kind == "gold" || r.kind == "enchantment_multiplier"), Is.False, "no colour, gold or enchantment row of its own");
            var product = new ExactFraction(ExactScoreMath.Parse(b.baseTotal), 1);
            foreach (var row in rows.SkipWhile(r => r.operation == "+").Where(r => r.operation == "×")) product = product * ExactFraction.Parse(row.operand);
            AssertSame(product, expected, "base x every multiplying row");

            // 4. The presentation receipt: its total multiplier, and the bonus events it plays.
            var receipt = PresentationReceiptAdapter.Build(new CommandReceipt { commandId = "receipt", score = b.score, submission = new SubmissionRecord { snapshot = snap, breakdown = b }, effects = new List<EffectRecord>() },
                new RunState { runId = "run", stage = new StageState { score = "0", target = "1800000" } },
                new RunState { runId = "run", phase = "HandReady", stage = new StageState { score = b.score, target = "1800000" } }, cfg);
            var multiplier = new ExactFraction(ExactScoreMath.Parse(receipt.MultiplierNumeratorDecimal), ExactScoreMath.Parse(receipt.MultiplierDenominatorDecimal));
            AssertSame(new ExactFraction(ExactScoreMath.Parse(b.baseTotal), 1) * multiplier, expected, "base x the receipt's total multiplier");
            var entries = receipt.Events.Where(e => e.IsScoring && (e.EffectFamily == "color" || e.EffectFamily == "gold" || e.EffectFamily == "controller_additive" || e.EffectFamily == "enchantment_additive")).ToArray();
            Assert.That(entries.Length, Is.EqualTo(b.additiveEntries.Length), "one event per bonus");
            Assert.That(entries.All(e => e.Operation == "+"), Is.True);
            var total = entries.Aggregate(new ExactFraction(0, 1), (sum, e) => sum + ExactFraction.Parse(e.Operand));
            AssertSame(total, ScoreFactors111.Bonus(b.additiveTotal100), "the bonus events add up to A");
            if (entries.Length > 0) AssertSame(ExactFraction.Parse(entries[entries.Length - 1].AfterValue), ScoreFactors111.Bonus(b.additiveTotal100), "the running bonus total ends at A");
            var lineEvent = receipt.Events.Single(e => e.EffectFamily == "additive");
            AssertSame(ExactFraction.Parse(lineEvent.Operand), new ExactFraction(100 + (BigInteger)b.additiveTotal100, 100), "the bonus line event multiplies by 1 + A");
            Assert.That(lineEvent.EventOrdinal, Is.GreaterThan(entries.Select(e => e.EventOrdinal).DefaultIfEmpty(-1).Max()), "the line comes after its entries");
            bool fromCards = b.additiveEntries.Any(e => e.emitterId != null);
            Assert.That(lineEvent.Phase, Is.EqualTo(fromCards ? PresentationPhase.ControllerAndEnchant : PresentationPhase.BaseAndStructure), "the line is shown with the cards only when a card added to it");
            foreach (var e in entries.Where(e => e.EffectFamily == "controller_additive" || e.EffectFamily == "enchantment_additive"))
            {
                Assert.That(e.EmitterId, Is.Not.Null); Assert.That(e.CountsForChain, Is.True, "a card that adds a bonus took part in the score");
                Assert.That(receipt.ScoringEmitterIds, Does.Contain(e.EmitterId));
            }
        }
        private static string Term(List<ExactFraction> written, BigInteger numerator, BigInteger denominator)
        { var value = new ExactFraction(numerator, denominator); written.Add(value); return value.ToString(); }

        [Test]
        public void DisplayV111_SpecExampleOpensTheBonusLineIntoItsEntries()
        {
            // RULES_DELTA 13: "+2, +1.5, +0.5 expanded, total line x5"; section 2: A = 7 gives x8, 0 still shows x1.
            var b = new ScoreBreakdown { baseTotal = "20", totalHandMult100 = 300, redMult100 = 200, controllerMultiplierNumerator = "5", controllerMultiplierDenominator = "2", additiveTotal100 = 400,
                additiveEntries = new[] { new AdditiveEntry { kind = "controller", abilityId = "C06", emitterId = "c1", stateOwnerId = "c1", amount100 = 200 }, new AdditiveEntry { kind = "enchantment", abilityId = "mult2", emitterId = "c1", stateOwnerId = "c1", amount100 = 150 }, new AdditiveEntry { kind = "gold", abilityId = "U06", dieIndex = 0, amount100 = 50 } },
                globalFactors = new[] { new MultiplierFactor { emitterId = "BOSS", abilityId = "B01", numerator = "1", denominator = "2" } } };
            var all = ScoreFactors111.List(b); var line = all.Single(f => f.Key == "additive");
            Assert.That(line.Numerator, Is.EqualTo((BigInteger)5)); Assert.That(line.Denominator, Is.EqualTo(BigInteger.One)); Assert.That(line.Entries.Select(e => e.amount100), Is.EqualTo(new long[] { 200, 150, 50 }));
            Assert.That(ScoreFactors111.Product(all).ToString(), Is.EqualTo("750"), "20 x 3 x 2 x 5 x 2.5 x 0.5");
            Assert.That(all.Last().Key, Is.EqualTo("boss")); Assert.That(all.Last().SourceId, Is.EqualTo("B01"));
            Assert.That(ScoreFactors111.Multiplier(b, false).ToString(), Is.EqualTo("75")); Assert.That(ScoreFactors111.Multiplier(b, true).ToString(), Is.EqualTo("75/2"));
            b.additiveTotal100 = 0; b.additiveEntries = Array.Empty<AdditiveEntry>();
            Assert.That(ScoreFactors111.List(b).Single(f => f.Key == "additive").IsIdentity, Is.True, "A = 0 is still a line of x1");
            Assert.That(ScoreFactors111.List(b, true).Any(f => f.Key == "additive"), Is.False, "and is left out where only changing factors are written");
            Assert.That(ScoreFactors111.List(null), Is.Empty);
        }
    }
}
