from pathlib import Path
p=Path('DicePrototype/Assets/Game/Tests/EditMode/Rules/ScoreExplanationTests.cs');s=p.read_text(encoding='utf-8')
s=s.replace('JsonUtility.FromJson<GameConfig>(json)','JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(json))').replace('Trace_All100GoldenOutputs','Trace_All160GoldenOutputs')
a=s.index('            string[] fields = typeof(ScoreBreakdown)');b=s.index('            Assert.That(trace.dice.Length',a)
s=s[:a]+'            GoldenJsonLoader.AssertExpected(golden.expected, breakdown, golden.id);\n'+s[b:]
s=s.replace('decimal value = breakdown.effectivePips[i];','string value = breakdown.effectivePips[i].ToString();').replace('Is.EqualTo((decimal)trace.dice[i].contribution)','Is.EqualTo(trace.dice[i].contribution.ToString())').replace('decimal running = golden.expected.pipSum;','string running = breakdown.pipSum;').replace('Is.EqualTo((decimal)golden.expected.score)','Is.EqualTo(golden.expected.score)').replace('golden.expected.triggeredMultiplierIds','breakdown.triggeredMultiplierIds')
s=s.replace('new decimal[] {1, 5, 7, 10, 15, 30}','new string[] {"1", "11", "14", "20", "25", "50"}').replace('new decimal[] {4, 2, 3, 5, 2, 2}','new string[] {"10", "3", "6", "5", "2", "2"}').replace('new decimal[] {5, 7, 10, 15, 30, 60}','new string[] {"11", "14", "20", "25", "50", "100"}')
s=s.replace('die.contribution, Is.EqualTo(60)','die.contribution, Is.EqualTo(100)').replace('breakdown.score, Is.EqualTo(60)','breakdown.score, Is.EqualTo("300")').replace('Is.EqualTo(2m)','Is.EqualTo("2")').replace('Is.EqualTo(5m)','Is.EqualTo("8")')
s=s.replace('new[] {"unused_rolls", "hand", "red_group", "controller", "controller", "floor"}','new[] {"unused_rolls", "hand", "color", "red_group", "controller", "controller", "floor"}')
s=s.replace('new decimal[] {17, 17, 46.75m, 70.125m, 210.375m, 420.75m}','new string[] {"17", "17", "187/4", "187/4", "1309/16", "1309/4", "3927/4"}').replace('new decimal[] {17, 46.75m, 70.125m, 210.375m, 420.75m, 420}','new string[] {"17", "187/4", "187/4", "1309/16", "1309/4", "3927/4", "981"}')
s=s.replace('steps[3].sourceId','steps[4].sourceId').replace('steps[4].sourceId, Is.EqualTo("C18")','steps[5].sourceId, Is.EqualTo("C18")').replace('breakdown.score, Is.EqualTo(420)','breakdown.score, Is.EqualTo("981")')
s=s.replace('1.5m) * 3 * 2','1.75m) * 4 * 3').replace('Is.EqualTo(414)','Is.EqualTo(960)').replace('Is.Not.EqualTo(prematurelyRounded)','Is.Not.EqualTo(prematurelyRounded.ToString())').replace('else breakdown.score++;','else breakdown.score = ExactScoreMath.Add(breakdown.score,"1");')
a=s.index('        private static decimal AssertStep');s=s[:a]+'''        private static string AssertStep(ScoreStep step,string previous)
        {
            Assert.That(step.before,Is.EqualTo(previous),step.kind+" links to previous exact value.");
            var value=ExactFraction.Parse(previous);var operand=ExactFraction.Parse(step.operand);
            string expected=step.operation=="+"?(value+operand).ToString():step.operation=="×"?(value*operand).ToString():ExactScoreMath.SaveDecimal(value.Numerator/value.Denominator);
            Assert.That(step.after,Is.EqualTo(expected),step.kind);return step.after;
        }
    }
}
'''
p.write_text(s,encoding='utf-8',newline='\n')
