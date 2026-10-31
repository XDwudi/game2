from pathlib import Path
import re
root=Path(__file__).resolve().parents[1]/'DicePrototype/Assets/Game'
paths=list((root/'Tests/PlayMode').glob('*.cs'))+list((root/'Tests/EditMode/Persistence').glob('*.cs'))
for p in paths:
    s=p.read_text(encoding='utf-8-sig')
    s=s.replace('JsonUtility.FromJson<GameConfig>(json)','JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(json))')
    s=s.replace('long expected = app.Session.PreviewScore().score','string expected = app.Session.PreviewScore().score')
    s=re.sub(r'\bint (gold(?:Before)?)\s*=\s*app.Session.State.gold',r'long \1 = app.Session.State.gold',s)
    s=s.replace('int net=app.Session.GetOfferNetPrice','long net=app.Session.GetOfferNetPrice')
    s=s.replace('fixture.isDebug = true;', 'fixture.isDebug = true; fixture.debugWorkspace = true;')
    s=s.replace('broken.lastSubmission.breakdown.score++;','broken.lastSubmission.breakdown.score=ExactScoreMath.Add(broken.lastSubmission.breakdown.score,"1");')
    s=s.replace('expectedScore.score.ToString("N0")','System.Numerics.BigInteger.Parse(expectedScore.score).ToString("N0",System.Globalization.CultureInfo.InvariantCulture)')
    s=s.replace('Assert.That(expectedScore.score, Is.LessThan(app.Session.State.stage.target)', 'Assert.That(ExactScoreMath.Compare(expectedScore.score,app.Session.State.stage.target), Is.LessThan(0)')
    s=s.replace('base5','base10').replace('mult120','mult2').replace('基础分 +5','基础分 +10')
    if p.name=='GameplayPlayModeTests.cs':
        s=s.replace('RealReferences58Icons','RealReferences71Icons').replace('EqualTo(58)','EqualTo(71)')
        s=s.replace('GameApp.SaveDirectoryOverride = saveDirectory;','GameApp.SaveDirectoryOverride = saveDirectory; GameApp.SuppressStartupDisplay=true;')
        s=s.replace('GameApp.SaveDirectoryOverride = null;','GameApp.SaveDirectoryOverride = null; GameApp.SuppressStartupDisplay=false;')
        s=s.replace('stage.target, Is.EqualTo(50)','stage.target, Is.EqualTo("50")')
        s=s.replace('practiceScore.pipSum, Is.EqualTo(19)','practiceScore.pipSum, Is.EqualTo("19")')
        s=s.replace('practiceScore.redMult100, Is.EqualTo(150)','practiceScore.redMult100, Is.EqualTo(175)')
        s=s.replace('practiceScore.score, Is.EqualTo(75), "v0.5: (19 pips + 6 unused-roll bonus) × pair 2 × two-red reward 1.5."','practiceScore.score, Is.EqualTo("87"), "v0.6: floor((19 + 6) × pair 2 × mixed color 1 × red 1.75)."')
    if p.name=='V05InterfacePlayModeTests.cs':
        s=s.replace('rows.Length,Is.EqualTo(13)','rows.Length,Is.EqualTo(14)')
        s=s.replace('Does.Contain("1/8")','Does.Contain("1/100")') # fixture explicitly debug, handbook honours debug limit.
    p.write_text(s,encoding='utf-8')
p=root/'Runtime/Verification/PlayerVerification.cs';s=p.read_text(encoding='utf-8-sig')
s=s.replace('if (flag < 0) return;','if (flag < 0) return;\n            GameApp.SuppressStartupDisplay=true;')
s=s.replace('state.isDebug = true;', 'state.isDebug = true; state.debugWorkspace = true;')
s=s.replace('s.isDebug = true;', 's.isDebug = true; s.debugWorkspace = true;')
p.write_text(s,encoding='utf-8')
p=root/'UI/GameApp.Modals.cs';s=p.read_text(encoding='utf-8');s=s.replace('state.isDebug=true;','state.isDebug=true;state.debugWorkspace=true;');p.write_text(s,encoding='utf-8')
p=root/'Runtime/GameApp.cs';s=p.read_text(encoding='utf-8');s=s.replace('public string SaveLocation => saves.SavePath;','public string SaveLocation => ViewState?.debugWorkspace==true&&!tutorial?(debugSaves?.SavePath??saves.SavePath):saves.SavePath;')
s=s.replace('UiKit.Button(main,"Pause","设置 / 暂停"','if(DebugUnlocked&&IsDebugWorkspace)UiKit.Button(main,"DebugWorkspace","调试面板",36,1020,180,44,ShowDebugPanel);\n            UiKit.Button(main,"Pause","设置 / 暂停"');p.write_text(s,encoding='utf-8')
print('Runtime/test representation migration applied; semantic execution required.')
