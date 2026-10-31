from pathlib import Path
p=Path('DicePrototype/Assets/Game/Tests/EditMode/Rules/V05RulesTests.cs');s=p.read_text(encoding='utf-8').replace('V05RulesTests','V06RulesTests').replace('V05-TRIGGER','V06-TRIGGER').replace('base5','base10').replace('mult120','mult2')
s=s.replace('JsonUtility.FromJson<GameConfig>(json)','JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(json))')
s=s.replace('expected ? 5 : 0','expected ? 10 : 0').replace('result.enchantBaseAdd, Is.EqualTo(10)','result.enchantBaseAdd, Is.EqualTo(20)').replace('contribution.enchantBaseAdd, Is.EqualTo(5)','contribution.enchantBaseAdd, Is.EqualTo(10)')
s=s.replace('b.pipSum, Is.EqualTo(30)','b.pipSum, Is.EqualTo("66")').replace('b.pairBaseAdd, Is.EqualTo(5)','b.pairBaseAdd, Is.EqualTo(10)').replace('b.enchantBaseAdd, Is.EqualTo(10)','b.enchantBaseAdd, Is.EqualTo(20)').replace('b.baseTotal, Is.EqualTo(45)','b.baseTotal, Is.EqualTo("96")').replace('b.score, Is.EqualTo(180)','b.score, Is.EqualTo("1152")')
s=s.replace('b.baseTotal, Is.EqualTo(7)','b.baseTotal, Is.EqualTo("17")').replace('Is.EqualTo("2520000")','Is.EqualTo("204000000")').replace('Is.EqualTo("250000")','Is.EqualTo("1000000")').replace('b.score, Is.EqualTo(10)','b.score, Is.EqualTo("204")')
a=s.index('        [TestCase(79,');b=s.index('        [Test] public void EquipmentSeparates',a)
s=s[:a]+'''        [TestCase(49,"none")][TestCase(50,"slotless")][TestCase(67,"slotless")][TestCase(68,"base10")][TestCase(77,"base10")][TestCase(78,"mult2")][TestCase(81,"mult2")][TestCase(82,"submit1")][TestCase(90,"submit1")][TestCase(91,"roll1")][TestCase(99,"roll1")]
        public void EnchantmentBoundariesAndDedicatedPoolConsumeExactlyOneDraw(int draw,string expected)
        {
            uint seed=1;for(;seed<100000;seed++)if(new GameRng(seed).NextIndex(100)==draw)break;Assert.That(seed,Is.LessThan(100000u));
            var baseline=new GameRng(seed);baseline.NextIndex(100);var scoreRng=new GameRng(seed);Assert.That(ControllerRules.DrawEnchantment("C20",null,scoreRng,cfg),Is.EqualTo(expected));Assert.That(scoreRng.State,Is.EqualTo(baseline.State));
            var nonBaseline=new GameRng(seed);int ticket=nonBaseline.NextIndex(200);string nonExpected=ticket<100?"none":ticket<150?"slotless":ticket<175?"submit1":"roll1";
            var nonRng=new GameRng(seed);Assert.That(ControllerRules.DrawEnchantment("C05",null,nonRng,cfg),Is.EqualTo(nonExpected));Assert.That(nonRng.State,Is.EqualTo(nonBaseline.State));
        }
        [Test] public void ReplacementPoolsExcludeNoneAndCurrentAndAdvanceRng()
        {
            CollectionAssert.AreEqual(new[]{"slotless","base10","mult2","submit1","roll1"},ControllerRules.ReplacementEnchantments("C20","none",cfg));
            CollectionAssert.AreEqual(new[]{"base10","mult2","submit1","roll1"},ControllerRules.ReplacementEnchantments("C20","slotless",cfg));
            CollectionAssert.AreEqual(new[]{"slotless","mult2","submit1","roll1"},ControllerRules.ReplacementEnchantments("C20","base10",cfg));
            CollectionAssert.AreEqual(new[]{"slotless","base10","submit1","roll1"},ControllerRules.ReplacementEnchantments("C20","mult2",cfg));
            CollectionAssert.AreEqual(new[]{"submit1","roll1"},ControllerRules.ReplacementEnchantments("C05","slotless",cfg));
            var rng=new GameRng(1);Assert.That(ControllerRules.DrawEnchantment("C05","none",rng,cfg),Is.AnyOf("slotless","submit1","roll1"));Assert.That(rng.State,Is.Not.EqualTo(1u));
            Assert.Throws<ArgumentException>(()=>ControllerRules.DrawEnchantment("C05","base10",rng,cfg));
        }
'''+s[b:]
s=s.replace('Enumerable.Range(1,10).Select(i=>Card("C"+i.ToString("D2"),i<=4?"none":"slotless",i))','cfg.controllers.Take(10).Select((c,i)=>Card(c.id,i<4?"none":"slotless",i+1))')
s=s.replace('var all=new[]{Card("C04"),Card("C16","none",2),Card("C23","none",3)};','var all=new[]{Card("C01","submit1"),Card("C21","roll1",2),Card("C41","roll1",3)};')
s=s.replace('ControllerRules.SubmissionBudget(all,cfg),Is.EqualTo(5)','ControllerRules.SubmissionBudget(all,cfg),Is.EqualTo(4)').replace('ControllerRules.Capacity(all,cfg),Is.EqualTo(5)','ControllerRules.Capacity(all,cfg),Is.EqualTo(6)')
s=s.replace('HandbookAlwaysContainsThirteenRows','HandbookAlwaysContainsFourteenHandAndTwoColorRows').replace('rows.Length,Is.EqualTo(13)','rows.Length,Is.EqualTo(16)')
p.write_text(s,encoding='utf-8',newline='\n')
