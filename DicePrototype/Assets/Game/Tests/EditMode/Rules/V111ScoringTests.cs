using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Core.Tests
{
    // p1.1.1 rules-layer examples of docs/specs/v111/RULES_DELTA.md sections 2-7 and 10, worked out by hand from the
    // rule text. Every expected value is derived in the comment next to it; none is copied from the scorer's output.
    //
    // Fixture conventions. Dice are written "4Rg,1Bx": pip, effective colour (R red, B blue), then g = gold face up,
    // x = glass die. Unless a test says otherwise: level 0 everywhere (pair x2, two pair x3, trips x4, quads x8,
    // straight x10; all blue +0.25, all red +0.5), no rolls left (no unused-roll bonus), capacity 5 (or the number
    // of dice when larger), 5 ordinary slots, 5 permanent dice, 3 rolls used, 3 submissions left, empty wallet.
    // Red group: 0-2 dice x1, 3 x2, 4 x3, 5 x4.5, 6 x6, 7 x8, 8 and more x11.
    public sealed class V111ScoringTests
    {
        private GameConfig cfg;
        private static string DataPath(string name) => Path.Combine(Application.dataPath, "Game", "Data", name);
        [SetUp]
        public void Setup()
        {
            string json = File.ReadAllText(DataPath("P0Config.json")); GameConfig.ValidateJson(json);
            cfg = JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(json)); cfg.Validate();
        }

        private static ControllerInstance Card(string id, int serial, string enchant = "none")
            => new ControllerInstance { definitionId = id, instanceId = "c" + serial, acquiredSerial = serial, enchantmentId = enchant };
        private static ControllerInstance[] Cards(params string[] ids) => ids.Select((id, i) => Card(id, i + 1)).ToArray();

        /// <summary>A standard die (faces F01-F06 = 1-6) lying on the face of the given pip, with that face's colour
        /// and gold flag set from the token.</summary>
        private SubmissionDie Die(int index, string token)
        {
            int pip = token[0] - '0'; string color = token[1] == 'R' ? "red" : "blue"; string flags = token.Substring(2);
            var faces = DiceFactory.CreateStandard(cfg, 1).faces; var up = faces.Single(f => f.permanentPip == pip);
            up.baseColor = color; up.isGold = flags.Contains("g");
            return new SubmissionDie { dieId = "d" + index, faceId = up.faceId, currentPip = pip, currentColor = color, isGold = up.isGold, isGlass = flags.Contains("x"), permanentSixFaceSum = 21, permanentFacesCopy = faces, lifetime = "permanent" };
        }
        /// <summary>extra is "key=value;key=value": wallet, pool (dice joined by +), rolls (left), rollsUsed, ordinal,
        /// slots, perm, left (submissions left before this one), cap, stage, boss, inv, prev, score, target.</summary>
        private SubmissionSnapshot Snap(string dice, string extra, params ControllerInstance[] cards)
        {
            var tokens = dice.Split(',');
            var s = new SubmissionSnapshot
            {
                configVersion = cfg.configVersion, capacity = Math.Max(5, tokens.Length), rollsRemaining = 0, rollsUsed = 3, submissionsRemainingBefore = 3,
                permanentDiceCount = 5, ordinaryControllerSlots = 5, stageKind = "normal", modifierId = "", equippedControllers = cards,
                dice = tokens.Select((t, i) => Die(i, t)).ToArray()
            };
            foreach (string pair in (extra ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string key = pair.Substring(0, pair.IndexOf('=')), value = pair.Substring(pair.IndexOf('=') + 1);
                switch (key)
                {
                    case "wallet": s.wallet = long.Parse(value); break;
                    case "pool": s.poolDice = value.Split('+').Select((t, i) => new PoolDie { dieId = "p" + i, faceId = "F0" + t[0], currentPip = t[0] - '0', currentColor = t[1] == 'R' ? "red" : "blue" }).ToArray(); break;
                    case "rolls": s.rollsRemaining = int.Parse(value); break;
                    case "rollsUsed": s.rollsUsed = int.Parse(value); break;
                    case "ordinal": s.submissionOrdinalBefore = int.Parse(value); break;
                    case "slots": s.ordinaryControllerSlots = int.Parse(value); break;
                    case "perm": s.permanentDiceCount = int.Parse(value); break;
                    case "left": s.submissionsRemainingBefore = int.Parse(value); break;
                    case "cap": s.capacity = int.Parse(value); break;
                    case "stage": s.stageKind = value; break;
                    case "boss": s.modifierId = value; break;
                    case "inv": s.inventoryCount = int.Parse(value); break;
                    case "prev": s.previousPrimaryHandId = value; break;
                    case "score": s.stageScoreBefore = value; break;
                    case "target": s.stageTarget = value; break;
                    default: throw new ArgumentException("Unknown fixture key " + key);
                }
            }
            return s;
        }
        private ScoreBreakdown Score(SubmissionSnapshot s)
        {
            string before = JsonUtility.ToJson(s);
            var result = ScoreCalculator.Evaluate(s, cfg);
            Assert.That(JsonUtility.ToJson(s), Is.EqualTo(before), "scoring must not change its input");
            // The explanation rebuilds every number and throws on any mismatch.
            Assert.That(ScoreExplanation.Build(s, result, cfg).global.Last().after, Is.EqualTo(result.score));
            return result;
        }
        private static string[] Entries(ScoreBreakdown b) => b.additiveEntries.Select(e => e.kind + ":" + e.abilityId + ":" + (e.emitterId ?? "-") + ":" + e.dieIndex + ":" + e.amount100).ToArray();
        private static string[] Factors(ScoreBreakdown b) => b.multiplierFactors.Select(f => f.abilityId + "=" + f.numerator + "/" + f.denominator).ToArray();

        // ---- Section 2: the ten steps and the additive zone ----

        [Test]
        public void V111_AdditiveZone_SpecExampleScores1200AndARetriggerOnlyChangesTheBase()
        {
            // 4,4,6,6 is two pair (x3) with base 20. Three red dice merged by C25 form a red group of 3 (x2); three red
            // and one blue is mixed, so no colour bonus. A = gold face 0.5 + C46 2 + C105 3 (reds before blues) +
            // score enchantment on C46 1.5 = 7. C09 x2.5, boss FirstHalf x1/2: 20 x 3 x 2 x 8 x 2.5 x 0.5 = 1200.
            var cards = new[] { Card("C25", 1), Card("C46", 2, "mult2"), Card("C105", 3), Card("C09", 4) };
            var b = Score(Snap("4Rg,4R,6R,6B", "boss=B26", cards));
            Assert.That(b.baseTotal, Is.EqualTo("20")); Assert.That(b.totalHandMult100, Is.EqualTo(300)); Assert.That(b.redMult100, Is.EqualTo(200));
            Assert.That(b.colorId, Is.Null); Assert.That(b.colorAdd100, Is.Zero);
            Assert.That(Entries(b), Is.EqualTo(new[] { "gold:U06:-:0:50", "controller:C46:c2:-1:200", "controller:C105:c3:-1:300", "enchantment:mult2:c2:-1:150" }));
            Assert.That(b.additiveTotal100, Is.EqualTo(700)); Assert.That(b.enchantAdditiveCount, Is.EqualTo(1));
            Assert.That(Factors(b), Is.EqualTo(new[] { "C09=5/2" }));
            Assert.That(b.globalFactors.Select(f => f.numerator + "/" + f.denominator), Is.EqualTo(new[] { "1/2" }));
            Assert.That(b.score, Is.EqualTo("1200"));
            // C64 counts the first die (the gold one, contribution 4) once more: base 24, A still 7:
            // 24 x 3 x 2 x 8 x 2.5 x 0.5 = 1440.
            var again = Score(Snap("4Rg,4R,6R,6B", "boss=B26", cards.Concat(new[] { Card("C64", 5) }).ToArray()));
            Assert.That(again.baseTotal, Is.EqualTo("24")); Assert.That(again.additiveTotal100, Is.EqualTo(700)); Assert.That(again.score, Is.EqualTo("1440"));
        }

        [Test]
        public void V111_HandHalf_FloorsTheHundredthsOfTheHandMultiplier()
        {
            // The hand table cannot produce the x2.25 of the specification example, so the same rule is shown with a
            // level 1 pair: 2 + 0.75 = x2.75, halved in hundredths floor(275 / 2) = 137 = x1.37 (not 1.375).
            // Base = 6 + 6 + C44 (94 gold x 2) = 200; mixed colours, one red die: 200 x 1.37 = 274 (exact halving would give 275).
            var s = Snap("6B,6R", "wallet=94;boss=B19", Card("C44", 1)); s.handLevels = new[] { new HandLevel { handId = "pair", level = 1 } };
            var b = Score(s);
            Assert.That(b.baseTotal, Is.EqualTo("200")); Assert.That(b.handMultBeforeModifier100, Is.EqualTo(275)); Assert.That(b.totalHandMult100, Is.EqualTo(137));
            Assert.That(b.score, Is.EqualTo("274"));
        }

        [Test]
        public void V111_GoldFace_AddsHalfPerSubmittedFaceAndNoLongerDoublesTheDie()
        {
            // Section 7.2: two gold faces give +1; with C89 the score is also x1.3 per gold face = x1.69.
            // Base 2 + 3 = 5 (not doubled), scatter, all blue level 0 +0.25: A = 1.25.
            // 5 x 2.25 x 1.69 = 19.0125 -> 19; as a reduced fraction 190125 / 10000 = 1521 / 80.
            var b = Score(Snap("2Bg,3Bg", "", Card("C89", 1)));
            Assert.That(b.pipContributions, Is.EqualTo(new long[] { 2, 3 }));
            Assert.That(Entries(b), Is.EqualTo(new[] { "color:all_blue:-:-1:25", "gold:U06:-:0:50", "gold:U06:-:1:50" }));
            Assert.That(Factors(b), Is.EqualTo(new[] { "C89=169/100" }));
            Assert.That(b.scoreNumerator, Is.EqualTo("1521")); Assert.That(b.scoreDenominator, Is.EqualTo("80")); Assert.That(b.score, Is.EqualTo("19"));
        }

        [Test]
        public void V111_GoldFace_StaysInTheAdditiveZoneUnderPipsOnlyAndOnASealedDie()
        {
            // PipsOnly (B39): base is the bare pip 2; A = all blue 0.25 + gold 0.5: 2 x 1.75 = 3.5 -> 3.
            var pipsOnly = Score(Snap("2Bg", "boss=B39"));
            Assert.That(pipsOnly.additiveTotal100, Is.EqualTo(75)); Assert.That(pipsOnly.score, Is.EqualTo("3"));
            // SealFirst (B05): the die contributes 0, the gold bonus is still listed; 0 x 1.75 = 0.
            var sealedDie = Score(Snap("2Bg", "boss=B05"));
            Assert.That(sealedDie.pipContributions, Is.EqualTo(new long[] { 0 })); Assert.That(sealedDie.additiveTotal100, Is.EqualTo(75)); Assert.That(sealedDie.score, Is.EqualTo("0"));
        }

        [Test]
        public void V111_Enchantments_BaseAdds15AndScoreAdds150UnlessSilenced()
        {
            // C50 adds 30 to the base: 2 + 3 + 30 = 35; mixed colours. Score enchantment +1.5: 35 x 2.5 = 87.5 -> 87.
            var scoreEnchant = Score(Snap("2B,3R", "", Card("C50", 1, "mult2")));
            Assert.That(Entries(scoreEnchant), Is.EqualTo(new[] { "enchantment:mult2:c1:-1:150" })); Assert.That(scoreEnchant.enchantAdditiveCount, Is.EqualTo(1)); Assert.That(scoreEnchant.score, Is.EqualTo("87"));
            // EnchantSilence (B06): the card still triggers, its enchantment gives nothing: 35.
            var silenced = Score(Snap("2B,3R", "boss=B06", Card("C50", 1, "mult2")));
            Assert.That(silenced.triggeredSourceIds, Is.EqualTo(new[] { "c1" })); Assert.That(silenced.additiveEntries, Is.Empty); Assert.That(silenced.enchantAdditiveCount, Is.Zero); Assert.That(silenced.score, Is.EqualTo("35"));
            // Base enchantment: 35 + 15 = 50.
            var baseEnchant = Score(Snap("2B,3R", "", Card("C50", 1, "base10")));
            Assert.That(baseEnchant.enchantBaseAdd, Is.EqualTo(15)); Assert.That(baseEnchant.enchantBaseCount, Is.EqualTo(1)); Assert.That(baseEnchant.score, Is.EqualTo("50"));
            // A card that does not trigger does not fire its enchantment: C149 needs exactly one die.
            var idle = Score(Snap("2B,3R", "", Card("C149", 1, "mult2")));
            Assert.That(idle.triggeredSourceIds, Is.Empty); Assert.That(idle.additiveEntries, Is.Empty); Assert.That(idle.score, Is.EqualTo("5"));
        }

        [Test]
        public void V111_C49_TwoCopiesAddPerFiveOrHalveTwiceWithoutAFive()
        {
            // Two 5s, two cards: each adds 2 x 1.5 = 3. Pair x2, base 10, all blue 0.25: A = 6.25: 10 x 2 x 7.25 = 145.
            var fives = Score(Snap("5B,5B", "", Cards("C49", "C49")));
            Assert.That(Entries(fives), Is.EqualTo(new[] { "color:all_blue:-:-1:25", "controller:C49:c1:-1:300", "controller:C49:c2:-1:300" }));
            Assert.That(fives.multiplierFactors, Is.Empty); Assert.That(fives.score, Is.EqualTo("145"));
            // No 5: both cards still trigger, each halves: 5 x 1.25 x 1/4 = 1.5625 -> 1.
            var none = Score(Snap("2B,3B", "", Cards("C49", "C49")));
            Assert.That(none.triggeredSourceIds, Is.EqualTo(new[] { "c1", "c2" })); Assert.That(Factors(none), Is.EqualTo(new[] { "C49=1/2", "C49=1/2" }));
            Assert.That(none.controllerMultiplierNumerator + "/" + none.controllerMultiplierDenominator, Is.EqualTo("1/4")); Assert.That(none.score, Is.EqualTo("1"));
        }

        [Test]
        public void V111_Score_SpecSection14WorkedExample()
        {
            // 4,4,4,4,1 all red: quads x8, red group of four 4s x3, base 17. A = all red level 0 0.5 + two gold faces 1 = 1.5.
            // C155 (four 4s, one 1 only) x2; C149 needs a single die and does not trigger; C53 ticket 3 = x1.5.
            // 17 x 8 x 3 x 2.5 x 2 x 1.5 = 3060; with the boss whole-hand half 1530.
            var s = Snap("4Rg,4Rg,4R,4R,1R", "", Cards("C155", "C149", "C53"));
            s.resolvedRandomChoices = new[] { new RandomMultiplierChoice { ownerInstanceId = "c3", units2 = 3 } };
            var b = Score(s);
            Assert.That(b.baseTotal, Is.EqualTo("17")); Assert.That(b.totalHandMult100, Is.EqualTo(800)); Assert.That(b.redMult100, Is.EqualTo(300)); Assert.That(b.additiveTotal100, Is.EqualTo(150));
            Assert.That(b.triggeredSourceIds, Is.EqualTo(new[] { "c1", "c3" })); Assert.That(Factors(b), Is.EqualTo(new[] { "C155=2/1", "C53=3/2" }));
            Assert.That(b.score, Is.EqualTo("3060"));
            s.modifierId = "B26"; Assert.That(Score(s).score, Is.EqualTo("1530"));
        }

        // ---- Section 3: colours, levels and the red group ----

        [TestCase("2B", "all_blue", 3, 175, "5")]   // 0.25 + 3 x 0.5 = 1.75: 2 x 2.75 = 5.5 -> 5
        [TestCase("2R", "all_red", 12, 650, "15")]  // 0.5 + 12 x 0.5 = 6.5: 2 x 7.5 = 15
        [TestCase("2B", "all_blue", 0, 25, "2")]    // 2 x 1.25 = 2.5 -> 2
        [TestCase("2R", "all_red", 0, 50, "3")]     // 2 x 1.5 = 3
        public void V111_Colour_IsAnAdditiveBonusByLevel(string dice, string colorId, int level, int expectedAdd100, string expectedScore)
        {
            var s = Snap(dice, ""); s.colorLevels = new[] { new HandLevel { handId = colorId, level = level } };
            var b = Score(s);
            Assert.That(b.colorId, Is.EqualTo(colorId)); Assert.That(b.colorAdd100, Is.EqualTo(expectedAdd100)); Assert.That(b.additiveTotal100, Is.EqualTo(expectedAdd100)); Assert.That(b.score, Is.EqualTo(expectedScore));
        }

        [Test]
        public void V111_Colour_ZeroLevelsKeepsLevel0AndNoRedClearsOnlyTheColourBonus()
        {
            // ZeroLevels (B28): a level 3 all red counts as level 0 = +0.5: 2 x 1.5 = 3.
            var zero = Snap("2R", "boss=B28"); zero.colorLevels = new[] { new HandLevel { handId = "all_red", level = 3 } };
            var z = Score(zero); Assert.That(z.colorAdd100, Is.EqualTo(50)); Assert.That(z.score, Is.EqualTo("3"));
            // NoRed (B22): the red bonus is cleared and the red group is x1; the colour id and the gold face stay: 2 x 1.5 = 3.
            var noRed = Score(Snap("2Rg", "boss=B22"));
            Assert.That(noRed.colorId, Is.EqualTo("all_red")); Assert.That(noRed.colorAdd100, Is.Zero); Assert.That(Entries(noRed), Is.EqualTo(new[] { "gold:U06:-:0:50" })); Assert.That(noRed.redMult100, Is.EqualTo(100)); Assert.That(noRed.score, Is.EqualTo("3"));
            // Mixed colours: no entry at all.
            var mixed = Score(Snap("2R,3B", "")); Assert.That(mixed.colorId, Is.Null); Assert.That(mixed.additiveEntries, Is.Empty); Assert.That(mixed.score, Is.EqualTo("5"));
        }

        [TestCase(1, 100)]
        [TestCase(2, 100)]
        [TestCase(3, 200)]
        [TestCase(4, 300)]
        [TestCase(5, 450)]
        [TestCase(6, 600)]
        [TestCase(7, 800)]
        [TestCase(8, 1100)]
        public void V111_RedGroup_TableByCount(int redDice, int expectedMult100)
        {
            var b = Score(Snap(string.Join(",", Enumerable.Repeat("6R", redDice)), "cap=8"));
            Assert.That(b.redCount, Is.EqualTo(redDice)); Assert.That(b.redValue, Is.EqualTo(6)); Assert.That(b.redMult100, Is.EqualTo(expectedMult100));
        }

        [Test]
        public void V111_RedGroup_SpecExampleWithTheFourRedGroupCards()
        {
            // Submitted red 1, red 4, blue 6; a red 2 stays in the pool. Mixed colours, scatter, base 11.
            // No cards: groups {1} and {4}, the tie takes the larger key 4, count 1, x1: 11.
            var none = Score(Snap("1R,4R,6B", "pool=2R"));
            Assert.That(none.redValue, Is.EqualTo(4)); Assert.That(none.redCount, Is.EqualTo(1)); Assert.That(none.redMult100, Is.EqualTo(100)); Assert.That(none.score, Is.EqualTo("11"));
            // C24 maps key 1 to 4 (group of 2), C73 adds the red pool die (3), C146 one virtual unit (4): x3 -> 33.
            var three = Score(Snap("1R,4R,6B", "pool=2R", Cards("C24", "C73", "C146")));
            Assert.That(three.redValue, Is.EqualTo(4)); Assert.That(three.redCount, Is.EqualTo(4)); Assert.That(three.redPoolCount, Is.EqualTo(1)); Assert.That(three.redBonusCount, Is.EqualTo(1));
            Assert.That(three.redMult100, Is.EqualTo(300)); Assert.That(three.colorId, Is.Null); Assert.That(three.score, Is.EqualTo("33"));
            // With C25 every red die has key 0; the count is still 4.
            var merged = Score(Snap("1R,4R,6B", "pool=2R", Cards("C24", "C73", "C146", "C25")));
            Assert.That(merged.redValue, Is.Zero); Assert.That(merged.redCount, Is.EqualTo(4)); Assert.That(merged.score, Is.EqualTo("33"));
            // C69 counts only the submitted members once more: 1 x 2 + 4 x 2 + 6 = 16: 16 x 3 = 48.
            var again = Score(Snap("1R,4R,6B", "pool=2R", Cards("C24", "C73", "C146", "C69")));
            Assert.That(again.pipRetriggers, Is.EqualTo(new[] { 1, 1, 0 })); Assert.That(again.pipSum, Is.EqualTo("16")); Assert.That(again.score, Is.EqualTo("48"));
            // Two red 4s and nothing else: count 2 keeps x1. Pair x2, base 8, all red +0.5: 8 x 2 x 1.5 = 24.
            var two = Score(Snap("4R,4R", "")); Assert.That(two.redCount, Is.EqualTo(2)); Assert.That(two.redMult100, Is.EqualTo(100)); Assert.That(two.score, Is.EqualTo("24"));
        }

        [Test]
        public void V111_C43_ClampsAtLevel12ByTheRealLevel()
        {
            // Section 3.1: level 11 with two C43 requests rises by 1 only.
            var s = Snap("2B,2B", "prev=pair", Cards("C43", "C43")); s.handLevels = new[] { new HandLevel { handId = "pair", level = 11 } };
            var plan = Score(s).afterScorePlan; Assert.That(plan.upgradeHandId, Is.EqualTo("pair")); Assert.That(plan.upgradeRequested, Is.EqualTo(2)); Assert.That(plan.upgradeApplied, Is.EqualTo(1));
            // Really at 12 but scored at level 0 (ZeroLevels, B28): no upgrade.
            var capped = Snap("2B,2B", "prev=pair;boss=B28", Cards("C43")); capped.handLevels = new[] { new HandLevel { handId = "pair", level = 12 } };
            var b = Score(capped); Assert.That(b.totalHandMult100, Is.EqualTo(200)); Assert.That(b.afterScorePlan.upgradeRequested, Is.EqualTo(1)); Assert.That(b.afterScorePlan.upgradeApplied, Is.Zero);
            // Level 13 is not a legal normal level any more.
            capped.modifierId = ""; capped.handLevels[0].level = 13; Assert.Throws<ArgumentException>(() => ScoreCalculator.Evaluate(capped, cfg));
        }

        // ---- Section 4: cards ----

        // The bonus of one card in hundredths, straight from the tables of sections 4.2 and 4.4; 0 = the card adds nothing.
        [TestCase("C06", "1R,2B,3B", "", 300)]
        [TestCase("C06", "1R,1B,2B", "", 0)]
        [TestCase("C08", "2B,3R", "", 200)]
        [TestCase("C08", "1R,2B,3B,4R,6B", "", 0)]
        [TestCase("C11", "2B,2B,3B,3B,6R", "", 800)]
        [TestCase("C14", "1R,3B", "", 400)]
        [TestCase("C18", "2R,3B,4R,5B", "", 500)]
        [TestCase("C28", "2B,2B,3R,3R", "", 400)]
        [TestCase("C28", "2B,2B,2B,2B", "", 0)]
        [TestCase("C30", "4B,4B,4B,4R", "", 250)]
        [TestCase("C32", "4R", "", 300)]
        [TestCase("C40", "1R,3B,5B,2B", "", 450)]
        [TestCase("C46", "2B", "", 200)]
        [TestCase("C74", "2B,3R", "pool=1R+6B", 400)]
        [TestCase("C74", "2B,3R", "pool=1R", 0)]
        [TestCase("C75", "2B", "rollsUsed=1", 450)]
        [TestCase("C75", "2B", "rollsUsed=2", 0)]
        [TestCase("C77", "2B", "rolls=0", 250)]
        [TestCase("C77", "2B", "rolls=1", 0)]
        [TestCase("C81", "2R,5R", "", 800)]
        [TestCase("C82", "2B,2B,2B,5R,5R", "", 450)]
        [TestCase("C83", "1R,2B,3B,4R,5B", "", 600)]
        [TestCase("C83", "1R,2B,3B,4R,6B", "", 0)]
        [TestCase("C86", "1R,1R,2B,2B,3B,3B", "", 800)]
        [TestCase("C88", "1R,2B,5B,6B", "", 600)]
        [TestCase("C102", "2B", "left=1", 450)]
        [TestCase("C102", "2B", "left=2", 0)]
        [TestCase("C104", "2R,3B,4R", "", 450)]
        [TestCase("C105", "2R,3R,4B", "", 300)]
        [TestCase("C106", "2B,6R,2B", "", 250)]
        [TestCase("C144", "2B,4R,6B", "", 400)]
        [TestCase("C149", "3B", "", 800)]
        [TestCase("C153", "2B", "ordinal=0", 450)]
        [TestCase("C153", "2B", "ordinal=1", 0)]
        [TestCase("C63", "2B", "perm=5", 250)]     // 5 permanent dice x 0.5
        [TestCase("C139", "2B", "wallet=69", 300)] // floor(69 / 10) x 0.5
        [TestCase("C139", "2B", "wallet=9", 0)]
        [TestCase("C147", "2B", "pool=1R+2B+3B", 180)] // 3 pool dice x 0.6
        [TestCase("C148", "2B", "rolls=2", 200)]   // 2 unused rolls x 1
        [TestCase("C150", "2B", "ordinal=3", 600)] // fourth hand: 2 x 3
        [TestCase("C150", "2B", "ordinal=0", 0)]
        public void V111_AdditiveCard_AddsItsTableValue(string id, string dice, string extra, int expected100)
        {
            var b = Score(Snap(dice, extra, Card(id, 1)));
            var mine = b.additiveEntries.Where(e => e.kind == "controller").ToArray();
            if (expected100 == 0) { Assert.That(mine, Is.Empty); Assert.That(b.triggeredSourceIds, Is.Empty); Assert.That(b.multiplierFactors, Is.Empty); return; }
            Assert.That(mine.Select(e => e.abilityId + ":" + e.emitterId + ":" + e.stateOwnerId + ":" + e.amount100), Is.EqualTo(new[] { id + ":c1:c1:" + expected100 }));
            Assert.That(b.triggeredSourceIds, Is.EqualTo(new[] { "c1" })); Assert.That(b.multiplierFactors, Is.Empty, "an additive card is not a multiplier any more");
        }

        [Test]
        public void V111_AdditiveCards_ThatCountCardsAndSlots()
        {
            // C47: 5 live cards (itself, two capacity cards, a disabled card) x 0.6 = +3.
            var c47 = Snap("2B,3R", "boss=B25", Cards("C01", "C03", "C47", "C21", "C22")); c47.disabledControllerIds = new[] { "c1", "c2" };
            Assert.That(Entries(Score(c47)), Is.EqualTo(new[] { "controller:C47:c3:-1:300" }));
            // C151: five ordinary slots, three in use: 2 x 1.25 = +2.5.
            Assert.That(Entries(Score(Snap("2B,3R", "slots=5", Cards("C151", "C01", "C50")))), Is.EqualTo(new[] { "controller:C151:c1:-1:250" }));
            // With a cultivation card (C38) one of the three rides for free: 3 empty slots = +3.75.
            Assert.That(Entries(Score(Snap("2B,3R", "slots=5", Cards("C151", "C01", "C38")))), Is.EqualTo(new[] { "controller:C151:c1:-1:375" }));
            // C152: two common cards (C01, C50) x 0.6 = +1.2; C152 itself is uncommon.
            Assert.That(Entries(Score(Snap("2B,3R", "", Cards("C152", "C01", "C50")))), Is.EqualTo(new[] { "controller:C152:c1:-1:120" }));
        }

        [TestCase("C02", "2B", "", "3/1")]
        [TestCase("C02", "2B,3R", "", "")]
        [TestCase("C09", "2B", "", "5/2")]
        [TestCase("C31", "3B,3B,3B,3B,3B", "", "3/1")]
        [TestCase("C31", "3B,3B,3B", "", "")]
        [TestCase("C33", "5B,2R", "", "3/1")]
        [TestCase("C33", "2B,5R", "", "")]
        [TestCase("C42", "2B", "", "4/1")]
        [TestCase("C101", "2B", "", "3/1")]
        [TestCase("C103", "2B", "perm=3", "3/1")]
        [TestCase("C103", "2B", "perm=4", "2/1")]
        [TestCase("C103", "2B", "perm=5", "")]
        [TestCase("C130", "2B", "stage=boss", "3/1")]
        [TestCase("C130", "2B", "stage=pressure", "")]
        [TestCase("C71", "2B", "pool=6R+6B", "9/4")]
        [TestCase("C71", "2B", "pool=5R", "")]
        [TestCase("C87", "3B,3B,3B,3B,3B", "", "9/4")]  // five of a kind: (3/2)^(5-3)
        [TestCase("C87", "3B,3B,3B,3B", "", "3/2")]
        [TestCase("C87", "3B,3B,3B", "", "")]
        [TestCase("C155", "4B,4B,4B,4B,1B,1B,6B", "", "4/1")]
        [TestCase("C155", "4B,4B,4B,4R", "", "2/1")]
        [TestCase("C155", "4B,4B,4B,1B,1B", "", "")]
        public void V111_MultiplierCard_HasItsTableFactor(string id, string dice, string extra, string expectedFactor)
        {
            var b = Score(Snap(dice, extra, Card(id, 1)));
            Assert.That(Factors(b), Is.EqualTo(expectedFactor == "" ? new string[0] : new[] { id + "=" + expectedFactor }));
            Assert.That(b.additiveEntries.Where(e => e.kind == "controller"), Is.Empty);
        }

        [Test]
        public void V111_C155_SevenDiceScoreAndC138BudgetAndC53Tickets()
        {
            // 4,4,4,4,1,1,6 all blue: quads x8 beats three-with-a-pair x6; base 24; A = 0.25; C155 x4: 24 x 8 x 1.25 x 4 = 960.
            Assert.That(Score(Snap("4B,4B,4B,4B,1B,1B,6B", "", Card("C155", 1))).score, Is.EqualTo("960"));
            // C138: 3 gold pays the left source only (2 gold, x2.5); the second finds 1 gold and does not trigger.
            var spend = Score(Snap("2B", "wallet=3", Cards("C138", "C138")));
            Assert.That(spend.triggeredSourceIds, Is.EqualTo(new[] { "c1" })); Assert.That(Factors(spend), Is.EqualTo(new[] { "C138=5/2" })); Assert.That(spend.afterScorePlan.goldSpent, Is.EqualTo(2));
            // 2 x 1.25 x 2.5 = 6.25 -> 6.
            Assert.That(spend.score, Is.EqualTo("6"));
            // C53: ticket 0 is x0.5 (unit 1), ticket 8 is x4 (unit 8); the table is [0.5,1,1,1.5,2,2,3,3,4].
            Assert.That(cfg.Controller("C53").parameters.factorUnits2, Is.EqualTo(new[] { 1, 2, 2, 3, 4, 4, 6, 6, 8 }));
            foreach (var pair in new[] { new[] { 1, 1, 2 }, new[] { 8, 4, 1 } })
            {
                var s = Snap("2B", "", Card("C53", 1)); s.resolvedRandomChoices = new[] { new RandomMultiplierChoice { ownerInstanceId = "c1", units2 = pair[0] } };
                Assert.That(Factors(Score(s)), Is.EqualTo(new[] { "C53=" + pair[1] + "/" + pair[2] }));
            }
        }

        // Whole base score of one card, sections 4.3: dice, context, expected base total.
        [TestCase("C10", "1R,1B", "", "32")]               // each 1 contributes 1 + 15
        [TestCase("C13", "1R,2B,3B", "", "22")]            // 1 + 8, 2, 3 + 8
        [TestCase("C17", "3B,4R", "", "16")]               // 3 + 9, 4
        [TestCase("C20", "1R,2B,3B,4R,6B", "", "40")]      // full, last die 6 + 24 = 30
        [TestCase("C27", "2B,2B,2B,2B", "", "28")]         // 8 + one paired pip x 20
        [TestCase("C27", "2B,2B,2B,3R,3R,3R", "", "55")]   // 15 + two paired pips x 20
        [TestCase("C39", "2B,3B,4R", "", "27")]            // 2 + 9, 3 + 9, 4
        [TestCase("C44", "2B", "wallet=4", "10")]          // 2 + 4 gold x 2
        [TestCase("C48", "1R,2B,3B", "", "36")]            // 6 + two empty slots x 15
        [TestCase("C50", "2B", "", "32")]                  // 2 + 30
        [TestCase("C70", "2B", "pool=1R+2B", "20")]        // 2 + two pool dice x 9
        [TestCase("C72", "2B", "pool=2R+6B", "26")]        // 2 + (2 + 6) x 3
        [TestCase("C76", "2B", "rollsUsed=3", "32")]       // 2 + two extra rolls x 15
        [TestCase("C80", "1R,1B,6B", "", "38")]            // 8 + one pair summing to 7 x 30
        [TestCase("C107", "2B,3B,4R", "", "49")]           // 2, 3 + 20, 4 + 20
        [TestCase("C108", "4R,2B", "", "30")]              // 6 + first die 4 x 6
        [TestCase("C135", "2B", "inv=1", "17")]            // 2 + one item x 15
        public void V111_BaseCard_AddsItsTableValue(string id, string dice, string extra, string expectedBase)
            => Assert.That(Score(Snap(dice, extra, Card(id, 1))).baseTotal, Is.EqualTo(expectedBase));

        [Test]
        public void V111_BaseCards_ThatReadTheSixPermanentFacesAndHandOrRetriggerCards()
        {
            // C92: a die whose six permanent faces are red adds 15 even when a blue wave covers it: 2 + 15 = 17.
            var mono = Snap("2R", "", Card("C92", 1)); foreach (var f in mono.dice[0].permanentFacesCopy) f.baseColor = "red"; mono.blueWave = true;
            var m = Score(mono); Assert.That(m.effectiveColors, Is.EqualTo(new[] { "blue" })); Assert.That(m.pipContributions, Is.EqualTo(new long[] { 17 }));
            // C93: six faces of pip 5: the contribution 5 becomes 15.
            var same = Snap("5B", "", Card("C93", 1)); foreach (var f in same.dice[0].permanentFacesCopy) f.permanentPip = 5; same.dice[0].permanentSixFaceSum = 30;
            Assert.That(Score(same).pipContributions, Is.EqualTo(new long[] { 15 }));
            // C84: three of a kind also has a pair: trips x4 + 2 = x6. C85: quads x8 + 4 = x12.
            Assert.That(Score(Snap("3B,3B,3B", "", Card("C84", 1))).totalHandMult100, Is.EqualTo(600));
            Assert.That(Score(Snap("3B,3B,3B,3B", "", Card("C85", 1))).totalHandMult100, Is.EqualTo(1200));
            // C143: the two 2s count three times each, the three 3s once.
            Assert.That(Score(Snap("2B,2B,3R,3R,3R", "", Card("C143", 1))).pipRetriggers, Is.EqualTo(new[] { 2, 2, 0, 0, 0 }));
            // C29: four consecutive pips make a straight (x10) and C83 then adds 6.
            var straight = Score(Snap("2B,3B,4R,5B", "", Cards("C29", "C83")));
            Assert.That(straight.primaryHandId, Is.EqualTo("straight")); Assert.That(Entries(straight), Is.EqualTo(new[] { "controller:C83:c2:-1:600" }));
        }

        [Test]
        public void V111_C19_SmallestPipOneOrTwo()
        {
            // 1,2,5 mixed: smallest pip 1 gives +2.5: base 8 x 3.5 = 28.
            var one = Score(Snap("1R,2B,5B", "", Card("C19", 1))); Assert.That(Entries(one), Is.EqualTo(new[] { "controller:C19:c1:-1:250" })); Assert.That(one.score, Is.EqualTo("28"));
            // With C12 the odd pips rise by 1: 2,2,6. Smallest pip 2 gives +1.5; pair x2, base 10: 10 x 2 x 2.5 = 50.
            var two = Score(Snap("1R,2B,5B", "", Cards("C12", "C19")));
            Assert.That(two.effectivePips, Is.EqualTo(new[] { 2, 2, 6 })); Assert.That(Entries(two), Is.EqualTo(new[] { "controller:C19:c2:-1:150" })); Assert.That(two.score, Is.EqualTo("50"));
            // Smallest pip 3: nothing.
            var none = Score(Snap("3B,4R", "", Card("C19", 1))); Assert.That(none.triggeredSourceIds, Is.Empty); Assert.That(none.score, Is.EqualTo("7"));
        }

        [TestCase("6B,2R,5B,1R,4B", true)]
        [TestCase("2B,6R,1B", true)]
        [TestCase("1B,6R,2B,5R", true)]
        [TestCase("2B,2R,1B", false)]   // a neighbour is equal
        [TestCase("1B,2R,3B", false)]   // the direction does not flip
        [TestCase("6B,2R,5B,5R", false)]
        [TestCase("2B,6R", false)]      // fewer than three dice
        public void V111_C91_AlternatingHighLowWithAtLeastThreeDice(string dice, bool expected)
        {
            var b = Score(Snap(dice, "", Card("C91", 1)));
            Assert.That(Entries(b).Where(e => e.StartsWith("controller")), Is.EqualTo(expected ? new[] { "controller:C91:c1:-1:250" } : new string[0]));
            Assert.That(b.triggeredSourceIds.Length, Is.EqualTo(expected ? 1 : 0));
        }

        [Test]
        public void V111_C137_StarPipPerRealCardSharedByItsMimic()
        {
            // Star 4, dice 4,4,1: +1 per matching die = +2. Pair x2, base 9, mixed: 9 x 2 x 3 = 54.
            var one = Card("C137", 1); one.state.starPip = 4;
            var b = Score(Snap("4B,4R,1B", "", one)); Assert.That(Entries(b), Is.EqualTo(new[] { "controller:C137:c1:-1:200" })); Assert.That(b.score, Is.EqualTo("54"));
            // Two real cards with stars 1 and 6, dice 1,1,6: +2 and +1. Pair x2, base 8, mixed: 8 x 2 x 4 = 64.
            ControllerInstance a = Card("C137", 2), c = Card("C137", 3); a.state.starPip = 1; c.state.starPip = 6;
            var two = Score(Snap("1R,1B,6B", "", a, c)); Assert.That(Entries(two), Is.EqualTo(new[] { "controller:C137:c2:-1:200", "controller:C137:c3:-1:100" })); Assert.That(two.score, Is.EqualTo("64"));
            // A mimic of the first adds another +2 with the target's star: A = 5: 8 x 2 x 6 = 96.
            var mimic = Snap("1R,1B,6B", "", Card("C41", 1), a, c);
            var three = Score(mimic); Assert.That(Entries(three), Is.EqualTo(new[] { "controller:C137:c1:-1:200", "controller:C137:c2:-1:200", "controller:C137:c3:-1:100" }));
            Assert.That(three.additiveEntries[0].stateOwnerId, Is.EqualTo("c2")); Assert.That(three.score, Is.EqualTo("96"));
            // The first real card disabled: neither it nor its mimic adds anything: A = 1: 8 x 2 x 2 = 32.
            mimic.modifierId = "B25"; mimic.disabledControllerIds = new[] { "c2" };
            var disabled = Score(mimic); Assert.That(Entries(disabled), Is.EqualTo(new[] { "controller:C137:c3:-1:100" })); Assert.That(disabled.score, Is.EqualTo("32"));
            // No star drawn yet (0): the card does not trigger. A star on any other card is invalid.
            Assert.That(Score(Snap("4B,4R,1B", "", Card("C137", 1))).triggeredSourceIds, Is.Empty);
            var wrong = Card("C46", 1); wrong.state.starPip = 4; Assert.Throws<ArgumentException>(() => ScoreCalculator.Evaluate(Snap("2B", "", wrong), cfg));
        }

        [TestCase("321", "160", true)]   // 2 x 160 = 320 < 321
        [TestCase("321", "161", false)]  // 322 is not below 321
        [TestCase("320", "160", false)]  // exactly half does not count
        [TestCase("2430", "1214", true)] // stage 6 behind a 150% wall
        [TestCase("2430", "1215", false)]
        public void V111_C156_TriggersStrictlyBelowHalfTheFrozenTarget(string target, string before, bool expected)
        {
            // 2 + 3 = 5, mixed colours: with the bonus 5 x 6 = 30, without it 5.
            var b = Score(Snap("2B,3R", "target=" + target + ";score=" + before, Card("C156", 1)));
            Assert.That(Entries(b), Is.EqualTo(expected ? new[] { "controller:C156:c1:-1:500" } : new string[0])); Assert.That(b.score, Is.EqualTo(expected ? "30" : "5"));
        }

        [Test]
        public void V111_C156_NeedsTheFrozenStageScoreAndTarget()
        {
            Assert.Throws<ArgumentException>(() => ScoreCalculator.Evaluate(Snap("2B,3R", "", Card("C156", 1)), cfg));
            Assert.Throws<ArgumentException>(() => ScoreCalculator.Evaluate(Snap("2B,3R", "target=321", Card("C156", 1)), cfg));
            Assert.Throws<ArgumentException>(() => ScoreCalculator.Evaluate(Snap("2B,3R", "target=0;score=0"), cfg));
            // Without a C156 source the two fields may be left out.
            Assert.That(Score(Snap("2B,3R", "")).score, Is.EqualTo("5"));
            // A disabled C156 is not an effective source.
            var disabled = Snap("2B,3R", "boss=B25", Card("C156", 1)); disabled.disabledControllerIds = new[] { "c1" };
            Assert.That(Score(disabled).score, Is.EqualTo("5"));
        }

        // ---- Section 5: growth ----

        [Test]
        public void V111_Growth_ValueIsStartPlusCatchUpPlusGrownPart()
        {
            // C56 offered in cycle 5: catch-up 4 x 0.5 = 2, value +3. Blue 2: A = 0.25 + 3: 2 x 4.25 = 8.5 -> 8.
            var deep = Card("C56", 1); deep.state.growthCatchup = 200;
            var first = Score(Snap("2B", "", deep));
            Assert.That(Entries(first), Is.EqualTo(new[] { "color:all_blue:-:-1:25", "controller:C56:c1:-1:300" })); Assert.That(first.score, Is.EqualTo("8"));
            // No red face: the plan grows it by 0.4 after scoring; this hand still used +3.
            Assert.That(first.afterScorePlan.growthChanges.Select(g => g.ownerInstanceId + ":" + g.abilityId + ":" + g.delta + ":" + g.halve + ":" + g.reset), Is.EqualTo(new[] { "c1:C56:40:False:False" }));
            // Next hand reads +3.4: 2 x 4.65 = 9.3 -> 9.
            deep.state.growth = 40; Assert.That(Score(Snap("2B", "", deep)).score, Is.EqualTo("9"));
            // A red face: no growth planned.
            Assert.That(Score(Snap("2R", "", deep)).afterScorePlan.growthChanges, Is.Empty);
            // C55 starts at +12 and plans +3: 2 + 3 + 12 = 17, then 20.
            var pile = Card("C55", 1); var p0 = Score(Snap("2B,3R", "", pile));
            Assert.That(p0.controllerBaseAdds.Select(a => a.abilityId + ":" + a.amount), Is.EqualTo(new[] { "C55:12" })); Assert.That(p0.score, Is.EqualTo("17"));
            Assert.That(p0.afterScorePlan.growthChanges.Select(g => g.delta), Is.EqualTo(new long[] { 3 }));
            pile.state.growth = 3; Assert.That(Score(Snap("2B,3R", "", pile)).score, Is.EqualTo("20"));
            // C57 grows by 0.8 when the final red count is at least 3: two members plus the C146 unit.
            var fire = Score(Snap("4R,4R", "", Cards("C57", "C146")));
            Assert.That(fire.redCount, Is.EqualTo(3)); Assert.That(fire.afterScorePlan.growthChanges.Select(g => g.abilityId + ":" + g.delta), Is.EqualTo(new[] { "C57:80" }));
            Assert.That(Score(Snap("4R,4R", "", Cards("C57"))).afterScorePlan.growthChanges, Is.Empty);
            // A catch-up on a card that does not grow is invalid.
            var wrong = Card("C46", 1); wrong.state.growthCatchup = 50; Assert.Throws<ArgumentException>(() => ScoreCalculator.Evaluate(Snap("2B", "", wrong), cfg));
        }

        [Test]
        public void V111_Growth_C54ReadsItsMultiplierAndPlansGrowthOrHalving()
        {
            // Bought in cycle 3 (catch-up 0.2, baseline x1.45), grown 0.3: x1.75 = 7/4.
            // Pair of blue 2s: 4 x 2 x 1.25 x 1.75 = 17.5 -> 17.
            var streak = Card("C54", 1); streak.state.growthCatchup = 20; streak.state.growth = 30;
            var same = Score(Snap("2B,2B", "prev=pair", streak));
            Assert.That(Factors(same), Is.EqualTo(new[] { "C54=7/4" })); Assert.That(same.score, Is.EqualTo("17"));
            Assert.That(same.afterScorePlan.growthChanges.Select(g => g.delta + ":" + g.halve + ":" + g.reset), Is.EqualTo(new[] { "10:False:False" }));
            // Another hand before: the plan halves the grown part and adds nothing.
            Assert.That(Score(Snap("2B,2B", "prev=trips", streak)).afterScorePlan.growthChanges.Select(g => g.delta + ":" + g.halve + ":" + g.reset), Is.EqualTo(new[] { "0:True:False" }));
            // Scatter is a hand like any other.
            Assert.That(Score(Snap("2B,3B", "prev=scatter", streak)).afterScorePlan.growthChanges.Select(g => g.delta + ":" + g.halve), Is.EqualTo(new[] { "10:False" }));
            // The first submission of a run has no previous hand: no entry.
            Assert.That(Score(Snap("2B,2B", "", streak)).afterScorePlan.growthChanges, Is.Empty);
            // After the halvings the card reads x1.60 (0.15 grown) and then x1.52 (0.07 grown).
            streak.state.growth = 15; Assert.That(ControllerRules.GrowthValue(cfg, "C54", streak.state), Is.EqualTo(160));
            streak.state.growth = 7; Assert.That(ControllerRules.GrowthValue(cfg, "C54", streak.state), Is.EqualTo(152));
            // A disabled C54 plans nothing.
            var off = Snap("2B,2B", "prev=pair;boss=B25", streak); off.disabledControllerIds = new[] { "c1" };
            Assert.That(Score(off).afterScorePlan.growthChanges, Is.Empty);
        }

        [Test]
        public void V111_GrowthHalf_HalvesOnlyTheGrownPartWhileScoring()
        {
            // B35 = ZeroLevels + GrowthHalf. C55 +12 grown by 3 reads 12 + floor(3 / 2) = 13: 2 + 3 + 13 = 18.
            var pile = Card("C55", 1); pile.state.growth = 3;
            var halved = Score(Snap("2B,3R", "boss=B35", pile)); Assert.That(halved.controllerBaseAdds.Single().amount, Is.EqualTo("13")); Assert.That(halved.score, Is.EqualTo("18"));
            Assert.That(pile.state.growth, Is.EqualTo(3), "the stored value is untouched");
            // Growth still continues: the plan is the normal +3.
            Assert.That(halved.afterScorePlan.growthChanges.Select(g => g.delta), Is.EqualTo(new long[] { 3 }));
            // C56 grown by 0.45 reads 1 + floor(45 / 2) / 100 = 1.22. Blue 2 at level 0: A = 0.25 + 1.22: 2 x 2.47 = 4.94 -> 4.
            var deep = Card("C56", 1); deep.state.growth = 45;
            var d = Score(Snap("2B", "boss=B35", deep)); Assert.That(Entries(d), Is.EqualTo(new[] { "color:all_blue:-:-1:25", "controller:C56:c1:-1:122" })); Assert.That(d.score, Is.EqualTo("4"));
            // The catch-up is not halved: C54 with catch-up 0.2 and 0.3 grown reads 1.25 + 0.2 + 0.15 = 1.60 = 8/5.
            var streak = Card("C54", 1); streak.state.growthCatchup = 20; streak.state.growth = 30;
            Assert.That(Factors(Score(Snap("2B,3R", "boss=B35", streak))), Is.EqualTo(new[] { "C54=8/5" }));
            // C141 counts cleared stages in the same field; the half does not touch its countdown: +4 - 3 = +1: 5 x 2 = 10.
            var fade = Card("C141", 1); fade.state.growth = 3;
            Assert.That(Entries(Score(Snap("2B,3R", "boss=B35", fade))), Is.EqualTo(new[] { "controller:C141:c1:-1:100" }));
            fade.state.growth = 0; Assert.That(Score(Snap("2B,3R", "", fade)).score, Is.EqualTo("25")); // +4: 5 x 5
            fade.state.growth = 4; Assert.Throws<ArgumentException>(() => ScoreCalculator.Evaluate(Snap("2B,3R", "", fade), cfg));
        }

        [Test]
        public void V111_Growth_ClassificationMatchesTheConfig()
        {
            foreach (var c in cfg.controllers)
            {
                string kind = c.parameters.growthKind ?? "";
                Assert.That(ControllerRules.IsGrowthAdditive(c.id), Is.EqualTo(kind == "additive"), c.id);
                Assert.That(ControllerRules.IsGrowthMultiplier(c.id), Is.EqualTo(kind == "multiplier"), c.id);
                Assert.That(ControllerRules.IsGrowthBase(c.id), Is.EqualTo(kind == "base"), c.id);
            }
            // Start values of section 5: additive +1, base +12, multiplier x1.25.
            foreach (string id in new[] { "C51", "C56", "C57", "C60", "C61", "C129", "C132", "C134" }) Assert.That(ControllerRules.GrowthValue(cfg, id, new ControllerDynamicState()), Is.EqualTo(100), id);
            foreach (string id in new[] { "C55", "C59", "C62" }) Assert.That(ControllerRules.GrowthValue(cfg, id, new ControllerDynamicState()), Is.EqualTo(12), id);
            foreach (string id in new[] { "C54", "C58", "C100", "C125" }) Assert.That(ControllerRules.GrowthValue(cfg, id, new ControllerDynamicState()), Is.EqualTo(125), id);
            Assert.Throws<ArgumentException>(() => ControllerRules.GrowthValue(cfg, "C46", new ControllerDynamicState()));
            // Event sizes of section 5 in the unit of each card.
            var events = new Dictionary<string, int> { { "C51", 80 }, { "C54", 10 }, { "C55", 3 }, { "C56", 40 }, { "C57", 80 }, { "C58", 15 }, { "C59", 6 }, { "C60", 50 }, { "C61", 50 }, { "C62", 1 }, { "C100", 15 }, { "C125", 20 }, { "C129", 250 }, { "C132", 250 }, { "C134", 40 } };
            foreach (var pair in events) Assert.That(cfg.Controller(pair.Key).parameters.growthEvent, Is.EqualTo(pair.Value), pair.Key);
            // Catch-up per later cycle: additive 0.5, base 6 points, multiplier 0.1.
            Assert.That(cfg.growthCatchupPerCycle.additive, Is.EqualTo(50)); Assert.That(cfg.growthCatchupPerCycle.@base, Is.EqualTo(6)); Assert.That(cfg.growthCatchupPerCycle.multiplier, Is.EqualTo(10));
        }

        // ---- Section 6: cultivation cards ----

        [Test]
        public void V111_Cultivation_TheLeftmostCardWithoutSlotlessRidesForFree()
        {
            // A ordinary card, B cultivation + slotless, C cultivation, D cultivation: B is free by its enchantment,
            // C by the cultivation place, D takes a slot: A and D = 2 ordinary slots.
            ControllerInstance a = Card("C46", 1), b = Card("C26", 2, "slotless"), c = Card("C38", 3), d = Card("C122", 4);
            Assert.That(ControllerRules.FreeCultivationInstanceId(new[] { a, b, c, d }, cfg), Is.EqualTo("c3"));
            Assert.That(ControllerRules.OrdinaryUsed(new[] { a, b, c, d }, cfg), Is.EqualTo(2));
            // D dragged to the left of C: now D rides for free; the count stays 2.
            Assert.That(ControllerRules.FreeCultivationInstanceId(new[] { a, b, d, c }, cfg), Is.EqualTo("c4"));
            Assert.That(ControllerRules.OrdinaryUsed(new[] { a, b, d, c }, cfg), Is.EqualTo(2));
            // C sold: D is the leftmost again. No cultivation card without slotless: nobody rides for free.
            Assert.That(ControllerRules.FreeCultivationInstanceId(new[] { a, b, d }, cfg), Is.EqualTo("c4")); Assert.That(ControllerRules.OrdinaryUsed(new[] { a, b, d }, cfg), Is.EqualTo(1));
            Assert.That(ControllerRules.FreeCultivationInstanceId(new[] { a, b }, cfg), Is.Null); Assert.That(ControllerRules.OrdinaryUsed(new[] { a, b }, cfg), Is.EqualTo(1));
            // A destroyed card no longer counts; C125 left the cultivation list.
            c.alive = false; Assert.That(ControllerRules.FreeCultivationInstanceId(new[] { a, c, d }, cfg), Is.EqualTo("c4")); c.alive = true;
            Assert.That(ControllerRules.OrdinaryUsed(new[] { Card("C125", 1), Card("C46", 2) }, cfg), Is.EqualTo(2));
            Assert.That(cfg.controllers.Where(x => x.isCultivation).Select(x => x.id), Is.EqualTo(new[] { "C07", "C15", "C26", "C36", "C37", "C38", "C115", "C117", "C118", "C119", "C120", "C121", "C122", "C123", "C124", "C126", "C127" }));
            // Equipment limits use the same count: five ordinary cards plus one cultivation card fit five slots, a second one does not.
            var full = new[] { Card("C46", 1), Card("C50", 2), Card("C01", 3), Card("C03", 4), Card("C02", 5), Card("C38", 6) };
            Assert.DoesNotThrow(() => ControllerRules.ValidateEquipment(full, cfg));
            Assert.Throws<ArgumentException>(() => ControllerRules.ValidateEquipment(full.Concat(new[] { Card("C122", 7) }), cfg));
        }

        [TestCase(0, 1)]
        [TestCase(3, 1)]   // a third rewritten face is not yet worth anything
        [TestCase(4, 2)]
        [TestCase(23, 6)]
        [TestCase(24, 7)]  // the cap: +6
        public void V111_Cultivation_SalePriceGainsOnePerFourRewrittenFaces(int faces, int expectedPrice)
        {
            // C26 costs 3: the p1.1.0 sale price is max(1, floor(3 / 2)) = 1.
            var card = Card("C26", 1); card.state.cultivationFacesChanged = faces;
            Assert.That(ControllerRules.SalePrice(card, cfg), Is.EqualTo(expectedPrice));
            // Full price (C140): 3 + the same bonus.
            Assert.That(ControllerRules.SalePrice(card, cfg, true), Is.EqualTo(3 + expectedPrice - 1));
        }

        [Test]
        public void V111_Cultivation_CountIsBoundedAndOnlyForCultivationCards()
        {
            var over = Card("C26", 1); over.state.cultivationFacesChanged = 25; Assert.Throws<ArgumentException>(() => ControllerRules.ValidateEquipment(new[] { over }, cfg));
            var wrong = Card("C46", 1); wrong.state.cultivationFacesChanged = 4; Assert.Throws<ArgumentException>(() => ControllerRules.ValidateEquipment(new[] { wrong }, cfg));
            // A card that is not a cultivation card sells as before: C46 costs 5 -> 2.
            Assert.That(ControllerRules.SalePrice(Card("C46", 1), cfg), Is.EqualTo(2));
        }

        [Test]
        public void V111_FacePlans_C36AndC37TouchTheFirstThreeDiceAndNameEverySource()
        {
            // Capacity 5, four dice: both C36 trigger, indices 0-2, net +2, one binding per source.
            var four = Score(Snap("5B,2B,6B,1R", "", Cards("C36", "C36"))).afterScorePlan;
            Assert.That(four.growthIndices, Is.EqualTo(new[] { 0, 1, 2 })); Assert.That(four.growthNetDelta, Is.EqualTo(2));
            Assert.That(four.cultivationSourceBindings.Select(b => b.abilityId + ":" + b.emitterId + ":" + b.stateOwnerId), Is.EqualTo(new[] { "C36:c1:c1", "C36:c2:c2" }));
            // Two dice: the third index is ignored.
            Assert.That(Score(Snap("5B,2B", "", Cards("C36"))).afterScorePlan.growthIndices, Is.EqualTo(new[] { 0, 1 }));
            // C36 and C37 cancel: net 0, both sources are still named.
            var net = Score(Snap("5B,2B,6B", "", Cards("C37", "C36"))).afterScorePlan;
            Assert.That(net.growthNetDelta, Is.Zero); Assert.That(net.cultivationSourceBindings.Select(b => b.abilityId), Is.EqualTo(new[] { "C37", "C36" }));
            // A full row does not trigger them.
            Assert.That(Score(Snap("5B,2B,6B,1R,3B", "", Cards("C36"))).afterScorePlan.growthIndices, Is.Empty);
            // Order of the older rewrites: C07, C26, C36/C37 in card order, C15, C38; one entry for C38 however many copies.
            var order = Score(Snap("2B,5B", "", Cards("C38", "C37", "C36", "C07", "C38"))).afterScorePlan;
            Assert.That(order.cultivationSourceBindings.Select(b => b.abilityId + ":" + b.emitterId), Is.EqualTo(new[] { "C07:c4", "C37:c2", "C36:c3", "C38:c1" }));
            Assert.That(order.copyFirstToSecond, Is.True); Assert.That(order.blueSpreadIndices, Is.EqualTo(new[] { 0, 1 }));
            // A mimic of C36 is its own source and credits the copied card.
            var mimic = Score(Snap("5B,2B,6B", "", Cards("C41", "C36"))).afterScorePlan;
            Assert.That(mimic.growthNetDelta, Is.EqualTo(2)); Assert.That(mimic.cultivationSourceBindings.Select(b => b.emitterId + ">" + b.stateOwnerId), Is.EqualTo(new[] { "c1>c2", "c2>c2" }));
        }

        private static string[] Changes(ScoreBreakdown b) => b.afterScorePlan.faceChanges.Select(f => f.sourceId + ":" + f.stateOwnerId + ":" + f.dieId + ":" + f.faceId + ":" + f.kind + ":" + f.amount + ":" + (f.text ?? "")).ToArray();

        [Test]
        public void V111_FacePlans_OfTheSubmitCultivationCards()
        {
            // C117: the up faces of the first and the last submitted die turn gold; a single die only once.
            Assert.That(Changes(Score(Snap("2B,3B,4B", "", Card("C117", 1)))), Is.EqualTo(new[] { "C117:c1:d0:F02:gold:1:", "C117:c1:d2:F04:gold:1:" }));
            Assert.That(Changes(Score(Snap("2B", "", Card("C117", 1)))), Is.EqualTo(new[] { "C117:c1:d0:F02:gold:1:" }));
            // C119: first die shows 6, the first two other faces by id (F01, F02) become 6; last die shows 3, F01 and F02 become 3.
            Assert.That(Changes(Score(Snap("6B,5B,3B", "", Card("C119", 1)))), Is.EqualTo(new[] { "C119:c1:d0:F01:pipSet:6:", "C119:c1:d0:F02:pipSet:6:", "C119:c1:d2:F01:pipSet:3:", "C119:c1:d2:F02:pipSet:3:" }));
            // Fewer than two faces differ: only those.
            var few = Snap("6B", "", Card("C119", 1)); foreach (var f in few.dice[0].permanentFacesCopy.Where(f => f.faceId != "F03")) f.permanentPip = 6; few.dice[0].permanentSixFaceSum = 33;
            Assert.That(Changes(Score(few)), Is.EqualTo(new[] { "C119:c1:d0:F03:pipSet:6:" }));
            // C122: every submitted up face is set to 5.
            Assert.That(Changes(Score(Snap("1R,5B", "", Card("C122", 1)))), Is.EqualTo(new[] { "C122:c1:d0:F01:pipSet:5:", "C122:c1:d1:F05:pipSet:5:" }));
            // C123 by the frozen physical pip: 1 plans +1, 2 and 3 nothing, 4 to 6 plan -2.
            Assert.That(Changes(Score(Snap("1R,2B,3B,4R,6B", "", Card("C123", 1)))), Is.EqualTo(new[] { "C123:c1:d0:F01:pipDelta:1:", "C123:c1:d3:F04:pipDelta:-2:", "C123:c1:d4:F06:pipDelta:-2:" }));
            // C124: weight +2 on every submitted up face. C126: +2 pips on every pool die's up face.
            Assert.That(Changes(Score(Snap("2B,3B", "", Card("C124", 1)))), Is.EqualTo(new[] { "C124:c1:d0:F02:weightDelta:2:", "C124:c1:d1:F03:weightDelta:2:" }));
            Assert.That(Changes(Score(Snap("2B", "pool=4R+6B", Card("C126", 1)))), Is.EqualTo(new[] { "C126:c1:p0:F04:pipDelta:2:", "C126:c1:p1:F06:pipDelta:2:" }));
            // C127: every hand of at least two dice (here the second of the stage) copies pip, colour, texture and gold
            // of the first up face to the last one; the weight is not part of the plan.
            Assert.That(Changes(Score(Snap("4Rg,2B", "ordinal=1", Card("C127", 1)))), Is.EqualTo(new[] { "C127:c1:d1:F02:pipSet:4:", "C127:c1:d1:F02:color:0:red", "C127:c1:d1:F02:texture:0:none", "C127:c1:d1:F02:gold:1:" }));
            Assert.That(Changes(Score(Snap("4R", "", Card("C127", 1)))), Is.Empty);
            // C125 is a growth multiplier now and plans no face change. A mimic's change credits the copied card.
            Assert.That(Changes(Score(Snap("2B,3B", "", Card("C125", 1)))), Is.Empty);
            Assert.That(Changes(Score(Snap("2B", "", Cards("C41", "C122")))), Is.EqualTo(new[] { "C122:c2:d0:F02:pipSet:5:", "C122:c2:d0:F02:pipSet:5:" }));
        }

        // ---- Section 7.3: glass ----

        [Test]
        public void V111_Glass_TriplesTheDieAndListsTheSubmittedGlassDiceForTheBreakDraw()
        {
            var b = Score(Snap("2Bx,3B,4Bx", "pool=6R"));
            Assert.That(b.pipContributions, Is.EqualTo(new long[] { 6, 3, 12 })); Assert.That(b.afterScorePlan.glassRiskDieIds, Is.EqualTo(new[] { "d0", "d2" }));
            // A sealed glass die is still at risk.
            Assert.That(Score(Snap("2Bx,3B", "boss=B05")).afterScorePlan.glassRiskDieIds, Is.EqualTo(new[] { "d0" }));
            Assert.That(Score(Snap("2B,3B", "")).afterScorePlan.glassRiskDieIds, Is.Empty);
            Assert.That(cfg.glassBreakPercent, Is.EqualTo(15)); Assert.That(cfg.glassContributionMultiplier, Is.EqualTo(3));
        }

        // ---- Section 10: boss modifiers that disable cards ----

        [Test]
        public void V111_Modifiers_B29DisablesOnlyTheLeftmostRareCardAndB25TheLeftmostTwo()
        {
            // Uncommon A, rare B, rare C: only B.
            Assert.That(ControllerRules.DisabledBy(Cards("C06", "C02", "C09"), cfg, "B29"), Is.EqualTo(new[] { "c2" }));
            // A mimic counts by its own rarity (C41 is rare), whatever it copies.
            Assert.That(ControllerRules.DisabledBy(Cards("C06", "C41", "C46", "C02"), cfg, "B29"), Is.EqualTo(new[] { "c2" }));
            Assert.That(ControllerRules.DisabledBy(Cards("C06", "C46"), cfg, "B29"), Is.Empty);
            // A destroyed rare card is skipped.
            var cards = Cards("C02", "C09"); cards[0].alive = false; Assert.That(ControllerRules.DisabledBy(cards, cfg, "B29"), Is.EqualTo(new[] { "c2" }));
            // B25 and B37 count positions, slotless and cultivation cards included.
            var row = new[] { Card("C38", 1), Card("C46", 2, "slotless"), Card("C02", 3) };
            Assert.That(ControllerRules.DisabledBy(row, cfg, "B25"), Is.EqualTo(new[] { "c1", "c2" })); Assert.That(ControllerRules.DisabledBy(row, cfg, "B37"), Is.EqualTo(new[] { "c1", "c2" }));
            // A disabled cultivation card still rides for free.
            Assert.That(ControllerRules.OrdinaryUsed(row, cfg), Is.EqualTo(1));
        }

        // ---- Section 13: the explanation ----

        [Test]
        public void V111_Explanation_OneAdditiveLineThatExpandsIntoItsEntries()
        {
            // +0.5 (gold), +2 (C46), +1.5 (its score enchantment): the line is x5. Base 5, scatter, mixed: 25.
            var s = Snap("2Rg,3B", "", Card("C46", 1, "mult2")); var b = Score(s); var trace = ScoreExplanation.Build(s, b, cfg);
            Assert.That(trace.global.Select(x => x.kind), Is.EqualTo(new[] { "unused_rolls", "hand", "red_group", "additive", "floor" }));
            var line = trace.global.Single(x => x.kind == "additive");
            Assert.That(line.operation, Is.EqualTo("×")); Assert.That(line.before, Is.EqualTo("5")); Assert.That(line.operand, Is.EqualTo("5")); Assert.That(line.after, Is.EqualTo("25"));
            Assert.That(line.entries.Select(e => e.kind + ":" + e.amount100), Is.EqualTo(new[] { "gold:50", "controller:200", "enchantment:150" }));
            Assert.That(trace.global.Where(x => x.kind != "additive").All(x => x.entries == null), Is.True);
            // No gold step on the die any more.
            Assert.That(trace.dice[0].steps, Is.Empty); Assert.That(b.score, Is.EqualTo("25"));
            // Nothing to add: the line is still shown, as x1.
            var plain = Snap("2R,3B", ""); var empty = ScoreExplanation.Build(plain, Score(plain), cfg).global.Single(x => x.kind == "additive");
            Assert.That(empty.operand, Is.EqualTo("1")); Assert.That(empty.entries, Is.Empty);
            // Controller factors follow the additive line; item and boss factors come last before the floor.
            var full = Snap("2Rg,3B", "boss=B26", Card("C46", 1, "mult2"), Card("C09", 2)); full.scoreItemMultiplier = 2;
            Assert.That(ScoreExplanation.Build(full, Score(full), cfg).global.Select(x => x.kind), Is.EqualTo(new[] { "unused_rolls", "hand", "red_group", "additive", "controller", "item", "boss", "floor" }));
        }

        [TestCase("total")]
        [TestCase("amount")]
        [TestCase("order")]
        [TestCase("missing")]
        [TestCase("colour")]
        public void V111_Explanation_RejectsAnInconsistentAdditiveBreakdown(string tamper)
        {
            var s = Snap("2Bg,3B", "", Card("C46", 1, "mult2")); var b = ScoreCalculator.Evaluate(s, cfg);
            Assert.That(b.additiveEntries.Select(e => e.kind), Is.EqualTo(new[] { "color", "gold", "controller", "enchantment" }));
            switch (tamper)
            {
                case "total": b.additiveTotal100++; break;
                case "amount": b.additiveEntries[1].amount100 = 60; b.additiveTotal100 += 10; break;
                case "order": Array.Reverse(b.additiveEntries); break;
                case "missing": b.additiveTotal100 -= b.additiveEntries[3].amount100; b.additiveEntries = b.additiveEntries.Take(3).ToArray(); break;
                case "colour": b.colorAdd100 = 75; break;
            }
            Assert.Throws<ArgumentException>(() => ScoreExplanation.Build(s, b, cfg));
        }

        // ---- Section 12.3: the configuration contract ----

        [Test]
        public void V111_Config_RejectsOldKeysMissingContentAndChangedLists()
        {
            string json = File.ReadAllText(DataPath("P0Config.json"));
            // A leftover p1.1.0 key is an unknown field.
            string old = json.Replace("\"goldAdd100\": 50,", "\"goldContributionMultiplier\": 2, \"goldAdd100\": 50,"); Assert.That(old, Is.Not.EqualTo(json));
            Assert.Throws<ArgumentException>(() => GameConfig.ValidateJson(old));
            string oldParameter = json.Replace("\"additivePerController100\": 60", "\"perControllerUnits2\": 1"); Assert.That(oldParameter, Is.Not.EqualTo(json));
            Assert.Throws<ArgumentException>(() => GameConfig.ValidateJson(oldParameter));
            Func<GameConfig> read = () => JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(json));
            var c = read(); c.consumables = c.consumables.Where(i => i.id != "I13").ToArray(); Assert.Throws<InvalidOperationException>(() => c.Validate());
            c = read(); c.Controller("C125").isCultivation = true; Assert.Throws<InvalidOperationException>(() => c.Validate());
            c = read(); var table = c.Controller("C53").parameters.factorUnits2; int swap = table[0]; table[0] = table[1]; table[1] = swap; Assert.Throws<InvalidOperationException>(() => c.Validate());
            c = read(); c.redRewards.Single(r => r.count == 2).factor100 = 175; Assert.Throws<InvalidOperationException>(() => c.Validate());
            c = read(); c.Modifier("B29").amount = 2; Assert.Throws<InvalidOperationException>(() => c.Validate());
            c = read(); c.stages[1].riskTarget = "481"; Assert.Throws<InvalidOperationException>(() => c.Validate());
            c = read(); c.stages[0].normalTarget = "161"; Assert.Throws<InvalidOperationException>(() => c.Validate());
            c = read(); c.shop.uniqueControllerIds = c.shop.uniqueControllerIds.Take(18).ToArray(); Assert.Throws<InvalidOperationException>(() => c.Validate());
            c = read(); c.Controller("C56").parameters.growthKind = "multiplier"; Assert.Throws<InvalidOperationException>(() => c.Validate());
            Assert.DoesNotThrow(() => read().Validate());
        }

        [Test]
        public void V111_Config_VersionContentAndCardIdentity()
        {
            Assert.That(cfg.configVersion, Is.EqualTo("p1.1.1")); Assert.That(cfg.saveSchemaVersion, Is.EqualTo(8));
            Assert.That(cfg.controllers.Length, Is.EqualTo(150)); Assert.That(cfg.consumables.Length, Is.EqualTo(13)); Assert.That(cfg.activeContentCount, Is.EqualTo(185));
            Assert.That(cfg.maxHandLevel, Is.EqualTo(12)); Assert.That(cfg.maxColorLevel, Is.EqualTo(12)); Assert.That(cfg.goldAdd100, Is.EqualTo(50));
            // The three removed cards are not valid instances; the three new ones are.
            foreach (string id in new[] { "C90", "C128", "C145" }) Assert.Throws<ArgumentException>(() => ControllerRules.ValidateEquipment(new[] { Card(id, 1) }, cfg, true), id);
            foreach (string id in new[] { "C154", "C155", "C156" }) Assert.DoesNotThrow(() => ControllerRules.ValidateEquipment(new[] { Card(id, 1) }, cfg), id);
            Assert.Throws<ArgumentException>(() => ControllerRules.ValidateEquipment(new[] { Card("C157", 1) }, cfg, true));
            // C154 is not a scoring card: it never triggers and cannot carry a scoring enchantment.
            Assert.That(Score(Snap("2B,3R", "", Card("C154", 1))).triggeredSourceIds, Is.Empty);
            Assert.That(ControllerRules.IsEnchantmentLegal("C154", "mult2", cfg), Is.False); Assert.That(ControllerRules.IsEnchantmentLegal("C155", "mult2", cfg), Is.True); Assert.That(ControllerRules.IsEnchantmentLegal("C156", "base10", cfg), Is.True);
            // Section 9.3: the 19 multiplier cards that are not offered again while owned.
            Assert.That(cfg.controllers.Where(c => ControllerRules.IsUniqueController(c.id, cfg)).Select(c => c.id), Is.EqualTo(new[] { "C02", "C09", "C31", "C33", "C41", "C42", "C53", "C54", "C58", "C71", "C87", "C89", "C100", "C101", "C103", "C125", "C130", "C138", "C155" }));
            var owned = Cards("C53", "C49");
            Assert.That(ControllerRules.OwnsUniqueController(owned, "C53", cfg), Is.True); Assert.That(ControllerRules.OwnsUniqueController(owned, "C49", cfg), Is.False); Assert.That(ControllerRules.OwnsUniqueController(owned, "C02", cfg), Is.False);
            owned[0].alive = false; Assert.That(ControllerRules.OwnsUniqueController(owned, "C53", cfg), Is.False);
        }
    }
}
