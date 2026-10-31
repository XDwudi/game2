from pathlib import Path
p=Path('DicePrototype/Assets/Game/Tests/EditMode/Rules/RulesTests.cs')
s=p.read_text(encoding='utf-8');a=s.index('    public class RulesTests');tail=s[a:]
prefix='''using System;
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
        public string id, description;
        public GoldenInput input;
        public ScoreBreakdown expected;
        public SubmissionSnapshot Snapshot(GameConfig cfg)
        {
            var cards=input.controllers ?? Array.Empty<ControllerInstance>();
            foreach(var card in cards) { if(card.state==null)card.state=new ControllerDynamicState(); if(card.state.turnoverUnits2==0)card.state.turnoverUnits2=3; }
            return new SubmissionSnapshot {configVersion=cfg.configVersion,capacity=input.capacity,rollsRemaining=input.rolls_remaining,redWave=input.red_wave,submissionOrdinalBefore=input.submission_ordinal,wallet=input.wallet,previousPrimaryHandId=input.previous_primary,debug=input.debug,equippedControllers=cards.Select(c=>c.Clone()).ToArray(),handLevels=input.levels.ToArray(false),colorLevels=input.color_levels.ToArray(true),mimicBindings=input.bindings.ToBindings(),resolvedRandomChoices=input.random_units2.ToChoices(),dice=input.dice.Select((d,i)=>new SubmissionDie {dieId="D"+i,faceId="F01",currentPip=d.p,currentColor=d.c,permanentSixFaceSum=d.sum6,isGlass=d.glass,isGold=d.gold}).ToArray()};
        }
    }
    [Serializable] public sealed class GoldenInput
    {
        public GoldenDie[] dice; public ControllerInstance[] controllers;public int rolls_remaining,capacity,submission_ordinal;public bool red_wave,debug;public long wallet;public string previous_primary;public GoldenLevels levels,color_levels;public GoldenBindings bindings;public GoldenRandom random_units2;
    }
    [Serializable] public sealed class GoldenDie { public int p,sum6;public string c;public bool gold,glass; }
    [Serializable] public sealed class GoldenLevels
    {
        public int pair,two_pair,trips,full_house,three_pair,quads,two_trips,five_kind,four_pair,six_kind,seven_kind,eight_kind,straight,all_blue,all_red;
        public HandLevel[] ToArray(bool colors)=>GetType().GetFields().Where(f=>(f.Name=="all_blue"||f.Name=="all_red")==colors).Select(f=>new HandLevel{handId=f.Name,level=(int)f.GetValue(this)}).ToArray();
    }
    [Serializable] public sealed class GoldenBindings
    {
        public string ctl_01,ctl_02,ctl_03,m;
        public MimicBinding[] ToBindings()=>GetType().GetFields().Where(f=>!string.IsNullOrEmpty((string)f.GetValue(this))).Select(f=>new MimicBinding{sourceInstanceId=f.Name,targetInstanceId=(string)f.GetValue(this)==GoldenJsonLoader.NullMarker?null:(string)f.GetValue(this)}).ToArray();
    }
    [Serializable] public sealed class GoldenRandom
    {
        public int ctl_01,ctl_02,ctl_04;
        public RandomMultiplierChoice[] ToChoices()=>GetType().GetFields().Where(f=>(int)f.GetValue(this)!=0).Select(f=>new RandomMultiplierChoice{ownerInstanceId=f.Name,units2=(int)f.GetValue(this)}).ToArray();
    }
    public static class GoldenJsonLoader
    {
        public const string NullMarker="__DICECRAFT_GOLDEN_NULL__";
        public static GoldenFile Read(string json)
        {
            if(json.Contains(NullMarker))throw new ArgumentException("Reserved golden JSON marker.");
            string encoded=Regex.Replace(json,":\\\\s*null(?=\\\\s*[,}])",":\\\""+NullMarker+"\\\"");
            if(encoded.TrimStart().StartsWith("[",StringComparison.Ordinal))encoded="{\\\"cases\\\":"+encoded+"}";
            var file=JsonUtility.FromJson<GoldenFile>(encoded);
            foreach(var item in file.cases)
            {
                if(item.expected!=null){if(item.expected.chosenGroupId==NullMarker)item.expected.chosenGroupId=null;if(item.expected.colorId==NullMarker)item.expected.colorId=null;if(item.expected.afterScorePlan!=null&&item.expected.afterScorePlan.upgradeHandId==NullMarker)item.expected.afterScorePlan.upgradeHandId=null;}
                if(item.input!=null&&item.input.previous_primary==NullMarker)item.input.previous_primary=null;
            }
            return file;
        }
        public static readonly string[] ExpectedFields={"score","baseTotal","pipContributions","pairKinds","pairBaseAdd","walletBaseAdd","emptySlotBaseAdd","steadyBaseAdd","enchantBaseAdd","chosenGroupId","primaryHandId","totalHandMult100","colorId","colorMult100","redCount","redMult100","controllerMultiplierNumerator","controllerMultiplierDenominator","triggeredSourceIds","enchantMultiplierCount","afterScorePlan"};
        public static void AssertExpected(ScoreBreakdown expected,ScoreBreakdown actual,string id)
        {
            foreach(string name in ExpectedFields)
            {
                var f=typeof(ScoreBreakdown).GetField(name);object a=f.GetValue(expected),b=f.GetValue(actual);
                if(name=="afterScorePlan")
                {
                    foreach(var p in typeof(AfterScorePlan).GetFields())
                    {if(p.FieldType.IsArray)CollectionAssert.AreEqual((IEnumerable)p.GetValue(a),(IEnumerable)p.GetValue(b),id+"."+p.Name);else Assert.That(p.GetValue(b),Is.EqualTo(p.GetValue(a)),id+"."+p.Name);}
                }
                else if(f.FieldType.IsArray)CollectionAssert.AreEqual((IEnumerable)a,(IEnumerable)b,id+"."+name);else Assert.That(b,Is.EqualTo(a),id+"."+name);
            }
        }
    }

'''
tail=tail.replace('JsonUtility.FromJson<GameConfig>(json)','JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(json))')
a=tail.index('            string[] fields = typeof(ScoreBreakdown)');b=tail.index('            Assert.That(JsonUtility.ToJson(snapshot)',a)
tail=tail[:a]+'            GoldenJsonLoader.AssertExpected(example.expected, actual, example.id);\n'+tail[b:]
tail=tail.replace('Golden_All26Fields_AndNoMutation','Golden_All21Fields_Plan14_AndNoMutation').replace('GoldenFile_HasExactly100UniqueCases','GoldenFile_HasExactly160UniqueCases').replace('Enumerable.Range(1, 100).Select(i => "S"','Enumerable.Range(1, 160).Select(i => "G"')
a=tail.index('            Assert.That(allNulls, Is.EqualTo(groupNulls)');b=tail.index('\n        }',a)
tail=tail[:a]+'''            Assert.That(allNulls, Is.GreaterThan(groupNulls), "v0.6 also has nullable color, previous primary and upgrade IDs.");
            Assert.That(GoldenJsonLoader.Read(json).cases.Count(c => c.expected.chosenGroupId == null), Is.EqualTo(groupNulls));
            Assert.That(GoldenJsonLoader.Read(json).cases.All(c => c.expected.primaryHandId != null && c.expected.score != null), Is.True);'''+tail[b:]
tail=tail.replace('Configuration_ContainsExactly58SupportedItemsAnd12Hands','Configuration_ContainsExactly71SupportedItemsAnd13Hands2Colors').replace('Is.EqualTo(58)','Is.EqualTo(71)').replace('config.handDefinitions.Length, Is.EqualTo(12)','config.handDefinitions.Length, Is.EqualTo(13)')
tail=tail.replace('\\"occupiedIndex\\": 0','\\"index\\": 0')
tail=tail.replace('actual.score, Is.EqualTo(220)','actual.score, Is.EqualTo("480")')
p.write_text(prefix+tail,encoding='utf-8',newline='\n')
