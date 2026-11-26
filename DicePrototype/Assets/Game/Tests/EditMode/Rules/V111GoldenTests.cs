using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Core.Tests
{
    // GoldenScoresV111.json is produced by tools/v111_golden.py, an independent reference scorer written from
    // docs/specs/v111/RULES_DELTA.md without reading this C# code (evidence/v111/rules/golden-provenance.json).
    // Field mapping, the null/empty normalisations and every disagreement found are in docs/specs/v111/W4B_NOTES.md.

    /// <summary>Strict reader for the golden file. The file has open-ended maps (input.bindings) and meaningful
    /// nulls, which JsonUtility cannot represent, so the cases are read into a plain tree instead: objects are
    /// Dictionary&lt;string, object&gt;, arrays List&lt;object&gt;, and scalars string, long, bool or null. Only
    /// integers are accepted as numbers; every amount in the file is an integer or a decimal string.</summary>
    public static class V111Json
    {
        public static object Parse(string text)
        {
            int at = 0; object value = Value(text, ref at); Skip(text, ref at);
            if (at != text.Length) throw new FormatException("Trailing characters after the JSON value at " + at);
            return value;
        }
        private static void Skip(string t, ref int at) { while (at < t.Length && (t[at] == ' ' || t[at] == '\n' || t[at] == '\r' || t[at] == '\t')) at++; }
        private static char Next(string t, ref int at) { if (at >= t.Length) throw new FormatException("Unexpected end of JSON."); return t[at++]; }
        private static bool Word(string t, ref int at, string word)
        {
            if (string.CompareOrdinal(t, at, word, 0, word.Length) != 0) return false;
            at += word.Length; return true;
        }
        private static object Value(string t, ref int at)
        {
            Skip(t, ref at); if (at >= t.Length) throw new FormatException("Unexpected end of JSON.");
            char c = t[at];
            if (c == '{')
            {
                at++; var map = new Dictionary<string, object>(StringComparer.Ordinal); Skip(t, ref at);
                if (at < t.Length && t[at] == '}') { at++; return map; }
                while (true)
                {
                    Skip(t, ref at); string key = Text(t, ref at); Skip(t, ref at);
                    if (Next(t, ref at) != ':') throw new FormatException("Expected ':' at " + at);
                    if (map.ContainsKey(key)) throw new FormatException("Duplicate key " + key);
                    map.Add(key, Value(t, ref at)); Skip(t, ref at);
                    char d = Next(t, ref at); if (d == '}') return map;
                    if (d != ',') throw new FormatException("Expected ',' or '}' at " + at);
                }
            }
            if (c == '[')
            {
                at++; var list = new List<object>(); Skip(t, ref at);
                if (at < t.Length && t[at] == ']') { at++; return list; }
                while (true)
                {
                    list.Add(Value(t, ref at)); Skip(t, ref at);
                    char d = Next(t, ref at); if (d == ']') return list;
                    if (d != ',') throw new FormatException("Expected ',' or ']' at " + at);
                }
            }
            if (c == '"') return Text(t, ref at);
            if (Word(t, ref at, "true")) return true;
            if (Word(t, ref at, "false")) return false;
            if (Word(t, ref at, "null")) return null;
            int start = at; if (t[at] == '-') at++;
            while (at < t.Length && t[at] >= '0' && t[at] <= '9') at++;
            if (at == start || (at < t.Length && (t[at] == '.' || t[at] == 'e' || t[at] == 'E'))) throw new FormatException("Only integers are allowed at " + start);
            return long.Parse(t.Substring(start, at - start), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        }
        private static string Text(string t, ref int at)
        {
            if (Next(t, ref at) != '"') throw new FormatException("Expected a string at " + at);
            var sb = new StringBuilder();
            while (true)
            {
                char c = Next(t, ref at);
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                char e = Next(t, ref at);
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (at + 4 > t.Length) throw new FormatException("Short \\u escape.");
                        sb.Append((char)int.Parse(t.Substring(at, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)); at += 4; break;
                    default: throw new FormatException("Unknown escape \\" + e);
                }
            }
        }
    }

    public sealed class V111GoldenCase
    {
        public string id, description, source, cardId, family, derivedFile, derivedId;
        public bool handChecked;
        public Dictionary<string, object> input, expected, handChecks;
        public override string ToString() => id;
    }

    /// <summary>The p1.1.1 golden file, the snapshot it describes and the field-by-field comparison. Shared by the
    /// new comparison tests and by the older golden test classes, which now run on their own slice of this file.</summary>
    public static class V111Golden
    {
        public const string FileName = "GoldenScoresV111.json", Schema = "v111-golden-1", ConfigVersion = "p1.1.1";
        private static V111GoldenCase[] cached; private static Dictionary<string, object> header; private static Dictionary<string, V111GoldenCase> byId;

        public static string DataPath(string name) => Path.Combine(Application.dataPath, "Game", "Data", name);
        public static GameConfig ReadConfig()
        {
            string json = File.ReadAllText(DataPath("P0Config.json")); GameConfig.ValidateJson(json);
            var cfg = JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(json)); cfg.Validate(); return cfg;
        }
        private static void Load()
        {
            if (cached != null) return;
            var root = Obj(V111Json.Parse(File.ReadAllText(DataPath(FileName))));
            var list = new List<V111GoldenCase>();
            foreach (var raw in List(root["cases"]))
            {
                var c = Obj(raw);
                var item = new V111GoldenCase
                {
                    id = Str(c, "id"), description = Str(c, "description"), source = Str(c, "source"), handChecked = (bool)c["handChecked"],
                    cardId = c.ContainsKey("cardId") ? Str(c, "cardId") : null, family = c.ContainsKey("family") ? Str(c, "family") : null,
                    input = Obj(c["input"]), expected = Obj(c["expected"]), handChecks = c.ContainsKey("handChecks") ? Obj(c["handChecks"]) : null
                };
                if (c.ContainsKey("derivedFrom")) { var from = Obj(c["derivedFrom"]); item.derivedFile = Str(from, "file"); item.derivedId = Str(from, "id"); }
                list.Add(item);
            }
            byId = list.ToDictionary(c => c.id, StringComparer.Ordinal); // throws on a duplicate id
            root.Remove("cases"); header = root; cached = list.ToArray();
        }
        public static V111GoldenCase[] Cases { get { Load(); return cached; } }
        public static Dictionary<string, object> Header { get { Load(); return header; } }
        public static V111GoldenCase Case(string id) { Load(); return byId[id]; }
        /// <summary>The cases recomputed from one of the four older files (id prefix L08-, L09-, L10- or L11-).</summary>
        public static V111GoldenCase[] Slice(string prefix) => Cases.Where(c => c.id.StartsWith(prefix, StringComparison.Ordinal)).ToArray();

        // ---- tree access -------------------------------------------------------------------------------------
        public static Dictionary<string, object> Obj(object value) => (Dictionary<string, object>)value;
        public static List<object> List(object value) => (List<object>)value;
        public static string Str(Dictionary<string, object> map, string key) => (string)map[key];
        public static long Num(Dictionary<string, object> map, string key) => (long)map[key];
        public static int Int(Dictionary<string, object> map, string key) => checked((int)(long)map[key]);
        public static bool Flag(Dictionary<string, object> map, string key) => (bool)map[key];
        public static string[] Strings(object value) => List(value).Select(v => (string)v).ToArray();

        // ---- input -> SubmissionSnapshot ---------------------------------------------------------------------
        // Normalisations (W4B_NOTES.md section 1): previousPrimary "" -> null; controllers[].activeColor "" -> null;
        // bindings null -> mimicBindings null (a mimic copies its right neighbour), an object -> explicit bindings
        // with a null value kept as "no target"; a synthetic die without six-face data (faces [], faceId "") gets
        // face id F01, no face copy and a six-face sum of 21, and reads colour and gold from dice[].color / .gold.
        public static SubmissionSnapshot Snapshot(V111GoldenCase example, GameConfig cfg)
        {
            var input = example.input;
            MimicBinding[] bindings = null;
            if (input["bindings"] != null)
                bindings = Obj(input["bindings"]).Select(b => new MimicBinding { sourceInstanceId = b.Key, targetInstanceId = (string)b.Value }).ToArray();
            string previous = Str(input, "previousPrimary");
            return new SubmissionSnapshot
            {
                configVersion = cfg.configVersion, capacity = Int(input, "capacity"), rollsRemaining = Int(input, "rollsRemaining"), rollsUsed = Int(input, "rollsUsed"),
                submissionsRemainingBefore = Int(input, "submissionsRemainingBefore"), permanentDiceCount = Int(input, "permanentDiceCount"),
                submissionOrdinalBefore = Int(input, "submissionOrdinal"), wallet = Num(input, "wallet"), redWave = Flag(input, "redWave"), blueWave = Flag(input, "blueWave"),
                debug = Flag(input, "debug"), previousPrimaryHandId = string.IsNullOrEmpty(previous) ? null : previous,
                stageKind = Str(input, "stageKind"), modifierId = Str(input, "modifierId"), inventoryCount = Int(input, "inventoryCount"),
                ordinaryControllerSlots = Int(input, "ordinaryControllerSlots"), scoreItemMultiplier = Int(input, "scoreItemMultiplier"),
                disabledControllerIds = Strings(input["disabledControllerIds"]), stageHandsUsed = Strings(input["stageHandsUsed"]),
                stageScoreBefore = Str(input, "stageScoreBefore"), stageTarget = Str(input, "stageTarget"),
                mimicBindings = bindings,
                handLevels = List(input["handLevels"]).Select(Obj).Select(h => new HandLevel { handId = Str(h, "handId"), level = Int(h, "level") }).ToArray(),
                colorLevels = List(input["colorLevels"]).Select(Obj).Select(h => new HandLevel { handId = Str(h, "handId"), level = Int(h, "level") }).ToArray(),
                resolvedRandomChoices = List(input["randomChoices"]).Select(Obj).Select(c => new RandomMultiplierChoice { ownerInstanceId = Str(c, "ownerInstanceId"), units2 = Int(c, "units2") }).ToArray(),
                equippedControllers = List(input["controllers"]).Select(Obj).Select((c, i) => new ControllerInstance
                {
                    instanceId = Str(c, "instanceId"), definitionId = Str(c, "definitionId"), enchantmentId = Str(c, "enchantmentId"), acquiredSerial = i + 1,
                    state = new ControllerDynamicState
                    {
                        growth = Num(c, "growth"), growthCatchup = Num(c, "growthCatchup"), turnoverUnits2 = Int(c, "turnoverUnits2"), starPip = Int(c, "starPip"),
                        activeColor = string.IsNullOrEmpty(Str(c, "activeColor")) ? null : Str(c, "activeColor")
                    }
                }).ToArray(),
                dice = List(input["dice"]).Select(Obj).Select(d =>
                {
                    var faces = List(d["faces"]).Select(Obj).Select(f => new FaceState { faceId = Str(f, "faceId"), permanentPip = Int(f, "pip"), baseColor = Str(f, "color"), texture = Str(f, "texture"), isGold = Flag(f, "gold"), baseWeight = 1 }).ToArray();
                    if (faces.Length == 0)
                        return new SubmissionDie { dieId = Str(d, "dieId"), faceId = "F01", currentPip = Int(d, "pip"), currentColor = Str(d, "color"), isGold = Flag(d, "gold"), isGlass = Flag(d, "glass"), permanentSixFaceSum = 21, lifetime = Str(d, "lifetime") };
                    var up = faces.Single(f => f.faceId == Str(d, "faceId"));
                    return new SubmissionDie { dieId = Str(d, "dieId"), faceId = Str(d, "faceId"), currentPip = Int(d, "pip"), currentColor = up.baseColor, isGold = up.isGold, isGlass = Flag(d, "glass"),
                        permanentSixFaceSum = faces.Sum(f => f.permanentPip), permanentFacesCopy = faces, lifetime = Str(d, "lifetime") };
                }).ToArray(),
                poolDice = List(input["pool"]).Select(Obj).Select(p => new PoolDie { dieId = Str(p, "dieId"), faceId = Str(p, "faceId"), currentPip = Int(p, "pip"), currentColor = Str(p, "color") }).ToArray()
            };
        }

        // ---- expected -> canonical text ----------------------------------------------------------------------
        // Every field is compared as canonical text. E(...) is the only place where a null and an empty string are
        // treated as the same value; it is used for the optional ids listed in W4B_NOTES.md section 1 and nowhere else.
        private static string E(object value) => value == null ? "" : (string)value;
        private static string S(object value) => value == null ? "<null>" : value is bool b ? (b ? "true" : "false") : Convert.ToString(value, CultureInfo.InvariantCulture);
        private static string Seq(IEnumerable values) => "[" + string.Join(",", values.Cast<object>().Select(S)) + "]";
        private static string Rows(object expected, params string[] keys) => Rows(expected, keys, Array.Empty<string>());
        private static string Rows(object expected, string[] keys, string[] optional)
            => "[" + string.Join(" ", List(expected).Select(Obj).Select(row =>
            {
                var unknown = row.Keys.Where(k => !keys.Contains(k)).ToArray();
                if (unknown.Length > 0 || row.Count != keys.Length) throw new FormatException("Unexpected row shape: " + string.Join(",", row.Keys));
                return string.Join("/", keys.Select(k => optional.Contains(k) ? E(row[k]) : S(row[k])));
            })) + "]";
        private static string Rows<T>(IEnumerable<T> actual, params Func<T, object>[] columns) => "[" + string.Join(" ", actual.Select(row => string.Join("/", columns.Select(c => S(c(row)))))) + "]";

        private sealed class Field
        {
            public Func<object, string> expected; public Func<ScoreBreakdown, string> actual;
            public Field(Func<object, string> e, Func<ScoreBreakdown, string> a) { expected = e; actual = a; }
        }
        private static readonly string[] FactorKeys = { "emitterId", "abilityId", "numerator", "denominator" };
        private static string Factors(IEnumerable<MultiplierFactor> factors) => Rows(factors, f => f.emitterId, f => f.abilityId, f => f.numerator, f => f.denominator);
        /// <summary>The submitted members of the chosen red group, rebuilt from fields of the breakdown: the game
        /// reports the key and the grouping mode but no member list (W4B_NOTES.md section 1).</summary>
        public static int[] RedMembers(ScoreBreakdown a, GameConfig cfg)
        {
            int submitted = a.redCount - a.redPoolCount - a.redBonusCount; if (submitted <= 0) return Array.Empty<int>();
            var map = cfg.Controller("C24").parameters;
            Func<int, int> key = i => a.redGroupingMode == "all_red" ? 0 : a.redGroupingMode == "one_as_four" && a.effectivePips[i] == map.redGroupKeyFrom ? map.redGroupKeyTo : a.effectivePips[i];
            return Enumerable.Range(0, a.effectivePips.Length).Where(i => a.effectiveColors[i] == "red" && key(i) == a.redValue).ToArray();
        }
        private static Dictionary<string, Field> BreakdownFields(GameConfig cfg) => new Dictionary<string, Field>(StringComparer.Ordinal)
        {
            ["score"] = new Field(S, a => S(a.score)),
            ["scoreNumerator"] = new Field(S, a => S(a.scoreNumerator)),
            ["scoreDenominator"] = new Field(S, a => S(a.scoreDenominator)),
            ["baseTotal"] = new Field(S, a => S(a.baseTotal)),
            ["pipSum"] = new Field(S, a => S(a.pipSum)),
            ["pipContributions"] = new Field(e => Seq(List(e)), a => Seq(a.pipContributions)),
            ["pipRetriggers"] = new Field(e => Seq(List(e)), a => Seq(a.pipRetriggers)),
            ["sealedDice"] = new Field(e => Seq(List(e)), a => Seq(a.sealedDice)),
            ["controllerBaseAdds"] = new Field(e => Rows(e, "emitterId", "abilityId", "amount"), a => Rows(a.controllerBaseAdds, x => x.emitterId, x => x.abilityId, x => x.amount)),
            ["enchantBaseAdd"] = new Field(S, a => S(a.enchantBaseAdd)),
            ["primaryHandId"] = new Field(S, a => S(a.primaryHandId)),
            ["chosenGroupId"] = new Field(E, a => E(a.chosenGroupId)),
            ["handMultAdd100"] = new Field(S, a => S(a.handMultAdd100)),
            ["handMultBeforeModifier100"] = new Field(S, a => S(a.handMultBeforeModifier100)),
            ["totalHandMult100"] = new Field(S, a => S(a.totalHandMult100)),
            ["colorId"] = new Field(E, a => E(a.colorId)),
            ["colorAdd100"] = new Field(S, a => S(a.colorAdd100)),
            ["redGroupKey"] = new Field(S, a => S(a.redValue)),
            ["redMemberIndices"] = new Field(e => Seq(List(e)), a => Seq(RedMembers(a, cfg))),
            ["redCount"] = new Field(S, a => S(a.redCount)),
            ["redPoolCount"] = new Field(S, a => S(a.redPoolCount)),
            ["redBonusCount"] = new Field(S, a => S(a.redBonusCount)),
            ["redMult100"] = new Field(S, a => S(a.redMult100)),
            ["additiveEntries"] = new Field(e => Rows(e, new[] { "kind", "abilityId", "emitterId", "stateOwnerId", "dieIndex", "amount100" }, new[] { "emitterId", "stateOwnerId" }),
                a => Rows(a.additiveEntries, x => x.kind, x => x.abilityId, x => E(x.emitterId), x => E(x.stateOwnerId), x => x.dieIndex, x => x.amount100)),
            ["additiveTotal100"] = new Field(S, a => S(a.additiveTotal100)),
            ["enchantAdditiveCount"] = new Field(S, a => S(a.enchantAdditiveCount)),
            ["multiplierFactors"] = new Field(e => Rows(e, FactorKeys), a => Factors(a.multiplierFactors)),
            // The reference lists the emitter instance of every factor; the game's field of the same name lists
            // their ability ids. The emitters are compared here, the ability ids through multiplierFactors.
            ["triggeredMultiplierIds"] = new Field(e => Seq(List(e)), a => Seq(a.multiplierFactors.Select(f => f.emitterId))),
            ["controllerMultiplierNumerator"] = new Field(S, a => S(a.controllerMultiplierNumerator)),
            ["controllerMultiplierDenominator"] = new Field(S, a => S(a.controllerMultiplierDenominator)),
            ["globalFactors"] = new Field(e => Rows(e, FactorKeys), a => Factors(a.globalFactors)),
            ["triggeredSourceIds"] = new Field(e => Seq(List(e)), a => Seq(a.triggeredSourceIds)),
            ["triggeredControllerIds"] = new Field(e => Seq(List(e)), a => Seq(a.triggeredControllerIds)),
        };
        private static readonly Dictionary<string, Field> PlanFields = new Dictionary<string, Field>(StringComparer.Ordinal)
        {
            ["earnedGold"] = new Field(S, a => S(a.afterScorePlan.earnedGold)),
            ["goldSpent"] = new Field(S, a => S(a.afterScorePlan.goldSpent)),
            ["copyFirstToSecond"] = new Field(S, a => S(a.afterScorePlan.copyFirstToSecond)),
            ["dyeAllFacesRed"] = new Field(S, a => S(a.afterScorePlan.dyeAllFacesRed)),
            ["growthNetDelta"] = new Field(S, a => S(a.afterScorePlan.growthNetDelta)),
            ["growthIndices"] = new Field(e => Seq(List(e)), a => Seq(a.afterScorePlan.growthIndices)),
            ["redSpreadIndices"] = new Field(e => Seq(List(e)), a => Seq(a.afterScorePlan.redSpreadIndices)),
            ["blueSpreadIndices"] = new Field(e => Seq(List(e)), a => Seq(a.afterScorePlan.blueSpreadIndices)),
            ["cultivationSourceBindings"] = new Field(e => Rows(e, "abilityId", "emitterId", "stateOwnerId"), a => Rows(a.afterScorePlan.cultivationSourceBindings, x => x.abilityId, x => x.emitterId, x => x.stateOwnerId)),
            ["faceChanges"] = new Field(e => Rows(e, new[] { "sourceId", "emitterId", "stateOwnerId", "dieId", "faceId", "kind", "amount", "text" }, new[] { "text" }),
                a => Rows(a.afterScorePlan.faceChanges, x => x.sourceId, x => x.emitterId, x => x.stateOwnerId, x => x.dieId, x => x.faceId, x => x.kind, x => x.amount, x => E(x.text))),
            ["growthChanges"] = new Field(e => Rows(e, "ownerInstanceId", "abilityId", "delta", "reset", "halve"), a => Rows(a.afterScorePlan.growthChanges, x => x.ownerInstanceId, x => x.abilityId, x => x.delta, x => x.reset, x => x.halve)),
            ["upgradeHandId"] = new Field(E, a => E(a.afterScorePlan.upgradeHandId)),
            ["upgradeRequested"] = new Field(S, a => S(a.afterScorePlan.upgradeRequested)),
            ["upgradeApplied"] = new Field(S, a => S(a.afterScorePlan.upgradeApplied)),
            ["nextPreviousPrimary"] = new Field(S, a => S(a.afterScorePlan.nextPreviousPrimary)),
            ["nextColorOwnerIds"] = new Field(e => Seq(List(e)), a => Seq(a.afterScorePlan.nextColorOwnerIds)),
            ["randomFactorOwnerIds"] = new Field(e => Seq(List(e)), a => Seq(a.afterScorePlan.randomFactorOwnerIds)),
            ["cannonRiskEmitterIds"] = new Field(e => Seq(List(e)), a => Seq(a.afterScorePlan.cannonRiskEmitterIds)),
            ["glassRiskDieIds"] = new Field(e => Seq(List(e)), a => Seq(a.afterScorePlan.glassRiskDieIds)),
        };
        public static readonly string[] ExpectedFieldNames =
        {
            "score", "scoreNumerator", "scoreDenominator", "baseTotal", "pipSum", "pipContributions", "pipRetriggers", "sealedDice", "controllerBaseAdds", "enchantBaseAdd",
            "primaryHandId", "chosenGroupId", "handMultAdd100", "handMultBeforeModifier100", "totalHandMult100", "colorId", "colorAdd100", "redGroupKey", "redMemberIndices",
            "redCount", "redPoolCount", "redBonusCount", "redMult100", "additiveEntries", "additiveTotal100", "enchantAdditiveCount", "multiplierFactors", "triggeredMultiplierIds",
            "controllerMultiplierNumerator", "controllerMultiplierDenominator", "globalFactors", "triggeredSourceIds", "triggeredControllerIds", "afterScorePlan"
        };
        public static readonly string[] ExpectedPlanFieldNames =
        {
            "earnedGold", "goldSpent", "copyFirstToSecond", "dyeAllFacesRed", "growthNetDelta", "growthIndices", "redSpreadIndices", "blueSpreadIndices", "cultivationSourceBindings",
            "faceChanges", "growthChanges", "upgradeHandId", "upgradeRequested", "upgradeApplied", "nextPreviousPrimary", "nextColorOwnerIds", "randomFactorOwnerIds",
            "cannonRiskEmitterIds", "glassRiskDieIds"
        };
        private static string Short(string text) => text.Length <= 400 ? text : text.Substring(0, 400) + "...(" + text.Length + " chars)";
        private static void Diff(Dictionary<string, Field> fields, string prefix, Dictionary<string, object> expected, ScoreBreakdown actual, bool complete, List<string> differences)
        {
            foreach (var pair in expected)
            {
                if (prefix == "" && pair.Key == "afterScorePlan") continue;
                if (!fields.TryGetValue(pair.Key, out Field field)) { differences.Add(prefix + pair.Key + ": the file has a field this test does not compare"); continue; }
                string want, got;
                try { want = field.expected(pair.Value); } catch (Exception ex) { differences.Add(prefix + pair.Key + ": unreadable expected value (" + ex.Message + ")"); continue; }
                got = field.actual(actual);
                if (!string.Equals(want, got, StringComparison.Ordinal)) differences.Add(prefix + pair.Key + ": expected " + Short(want) + " actual " + Short(got));
            }
            if (complete) foreach (string name in fields.Keys) if (!expected.ContainsKey(name)) differences.Add(prefix + name + ": missing from the file");
        }
        /// <summary>Every difference between one expectation block and the game's breakdown, as "field: expected X
        /// actual Y". `complete` requires the full field set (case.expected); hand checks are a subset.</summary>
        public static List<string> Differences(Dictionary<string, object> expected, Dictionary<string, object> plan, ScoreBreakdown actual, GameConfig cfg, bool complete)
        {
            var differences = new List<string>();
            if (expected != null) Diff(BreakdownFields(cfg), "", expected, actual, complete, differences);
            if (plan != null) Diff(PlanFields, "afterScorePlan.", plan, actual, complete, differences);
            else if (complete) differences.Add("afterScorePlan: missing from the file");
            return differences;
        }
        /// <summary>All differences for one case: the full expected block plus, where present, the hand-written checks.</summary>
        public static List<string> Differences(V111GoldenCase example, ScoreBreakdown actual, GameConfig cfg)
        {
            var differences = Differences(example.expected, example.expected.ContainsKey("afterScorePlan") ? Obj(example.expected["afterScorePlan"]) : null, actual, cfg, true);
            if (example.handChecks != null)
            {
                var unknown = example.handChecks.Keys.Where(k => k != "expected" && k != "afterScorePlan").ToArray();
                if (unknown.Length > 0) differences.Add("handChecks: unknown block " + string.Join(",", unknown));
                var hand = Differences(example.handChecks.ContainsKey("expected") ? Obj(example.handChecks["expected"]) : null,
                    example.handChecks.ContainsKey("afterScorePlan") ? Obj(example.handChecks["afterScorePlan"]) : null, actual, cfg, false);
                differences.AddRange(hand.Select(d => "handChecks." + d));
            }
            return differences;
        }
        /// <summary>Scores one case and fails with every differing field. Returns the snapshot and the breakdown so
        /// that a caller can go on to check the explanation.</summary>
        public static ScoreBreakdown AssertCase(V111GoldenCase example, GameConfig cfg, out SubmissionSnapshot snapshot)
        {
            snapshot = Snapshot(example, cfg); string before = JsonUtility.ToJson(snapshot);
            var actual = ScoreCalculator.Evaluate(snapshot, cfg);
            var differences = Differences(example, actual, cfg);
            Assert.That(differences, Is.Empty, example.id + " " + example.description + "\n  " + string.Join("\n  ", differences));
            Assert.That(JsonUtility.ToJson(snapshot), Is.EqualTo(before), example.id + ": scoring mutated its input.");
            return actual;
        }
        /// <summary>Instance ids of `card` in the case, and whether each of them is a triggered source.</summary>
        public static bool AllInstances(V111GoldenCase example, string card, int copies, bool triggered, bool allowDisabled, ScoreBreakdown actual)
        {
            var instances = List(example.input["controllers"]).Select(Obj).Where(c => Str(c, "definitionId") == card).Select(c => Str(c, "instanceId")).ToArray();
            if (instances.Length != copies) return false;
            if (!allowDisabled && Strings(example.input["disabledControllerIds"]).Any(id => instances.Contains(id))) return false;
            var expected = new HashSet<string>(Strings(example.expected["triggeredSourceIds"]), StringComparer.Ordinal);
            var got = new HashSet<string>(actual.triggeredSourceIds, StringComparer.Ordinal);
            return instances.All(id => expected.Contains(id) == triggered && got.Contains(id) == triggered);
        }
    }

    public class V111GoldenTests
    {
        // Cards whose numbers or mechanism change in p1.1.1 (RULES_DELTA 4.1-4.5, 5 and 6.1), scoring side only.
        // Kept here so that the file cannot silently drop one. C41 (the mimic) is covered by every mimic_hit case.
        private static readonly string[] ChangedCards =
            ("C02,C06,C08,C09,C10,C11,C13,C14,C17,C18,C19,C20,C27,C28,C29,C30,C31,C32,C33,C36,C37,C39,C40,C42,C43,C44,C46,C47,C48,C49,C50,C51,C53,C54,C55,C56,C57,C58,C59,"
            + "C60,C61,C62,C63,C70,C71,C72,C74,C75,C76,C77,C80,C81,C82,C83,C84,C85,C86,C87,C88,C89,C91,C92,C93,C100,C101,C102,C103,C104,C105,C106,C107,C108,C117,C119,C122,"
            + "C123,C124,C125,C126,C127,C129,C130,C132,C134,C135,C137,C138,C139,C141,C143,C144,C147,C148,C149,C150,C151,C152,C153,C155,C156").Split(',');
        // Scoring cards that trigger on every valid submission: no natural miss exists, only the disabled case.
        private static readonly HashSet<string> AlwaysTriggered = new HashSet<string>(
            "C01,C03,C09,C38,C42,C45,C46,C47,C49,C50,C53,C63,C64,C94,C101,C108,C117,C118,C119,C122,C123,C124,C141,C51,C54,C55,C56,C57,C58,C59,C60,C61,C62,C100,C125,C129,C132,C134".Split(','));
        private const int MatrixCards = 122;
        // Floors per kind of case. The recomputed older inputs are recognised by their id prefix; their `source`
        // label is the name of the file they came from and may follow that file when it is renamed.
        private static readonly Dictionary<string, int> PrefixFloors = new Dictionary<string, int> { ["L08-"] = 160, ["L09-"] = 257, ["L10-"] = 445, ["L11-"] = 455 };
        private static readonly Dictionary<string, int> SourceFloors = new Dictionary<string, int> { ["new"] = 263, ["matrix"] = 572 };
        private GameConfig config;
        [SetUp] public void Setup() { config = V111Golden.ReadConfig(); }

        public static IEnumerable Cases()
        {
            foreach (var item in V111Golden.Cases) yield return new TestCaseData(item.id).SetName("GOLD111-" + item.id);
        }

        [TestCaseSource(nameof(Cases))]
        public void GoldenV111_EveryExpectedFieldMatchesTheIndependentReference(string id)
        {
            V111Golden.AssertCase(V111Golden.Case(id), config, out _);
        }

        [Test]
        public void GoldenV111_FileIsTheP111SetWithAtLeast2152UniqueCases()
        {
            var header = V111Golden.Header; var cases = V111Golden.Cases;
            Assert.That(V111Golden.Str(header, "schema"), Is.EqualTo(V111Golden.Schema));
            Assert.That(V111Golden.Str(header, "configVersion"), Is.EqualTo(V111Golden.ConfigVersion));
            Assert.That(config.configVersion, Is.EqualTo(V111Golden.ConfigVersion));
            Assert.That(cases.Length, Is.GreaterThanOrEqualTo(2152));
            Assert.That(V111Golden.Int(header, "caseCount"), Is.EqualTo(cases.Length));
            Assert.That(cases.Select(c => c.id).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(cases.Length), "Golden case ids must be unique.");
            var declared = V111Golden.Obj(header["casesBySource"]);
            foreach (var floor in SourceFloors) Assert.That(cases.Count(c => c.source == floor.Key), Is.GreaterThanOrEqualTo(floor.Value), floor.Key + " cases");
            foreach (var floor in PrefixFloors)
            {
                var slice = V111Golden.Slice(floor.Key);
                Assert.That(slice.Length, Is.GreaterThanOrEqualTo(floor.Value), floor.Key + " cases");
                Assert.That(slice.All(c => c.derivedId != null && c.id == floor.Key + c.derivedId && !SourceFloors.ContainsKey(c.source)), Is.True, floor.Key + " cases name the older input they recompute");
                Assert.That(slice.Select(c => c.source).Distinct().Count(), Is.EqualTo(1), floor.Key + " cases come from one file");
            }
            Assert.That(cases.Count(c => SourceFloors.ContainsKey(c.source)) + PrefixFloors.Keys.Sum(p => V111Golden.Slice(p).Length), Is.EqualTo(cases.Length), "A case that is neither new, matrix nor a recomputed older input.");
            foreach (var group in cases.GroupBy(c => c.source)) Assert.That(V111Golden.Int(declared, group.Key), Is.EqualTo(group.Count()), group.Key + " declared count");
            Assert.That(declared.Count, Is.EqualTo(cases.Select(c => c.source).Distinct().Count()), "casesBySource names a source without cases.");
            foreach (var example in cases)
            {
                var ids = V111Golden.List(example.input["controllers"]).Select(V111Golden.Obj).Select(c => V111Golden.Str(c, "instanceId")).ToArray();
                Assert.That(ids.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(ids.Length), example.id + " reuses an instance id");
            }
        }

        [Test]
        public void GoldenV111_EveryCaseCarriesTheFullExpectedFieldSet()
        {
            foreach (var example in V111Golden.Cases)
            {
                CollectionAssert.AreEquivalent(V111Golden.ExpectedFieldNames, example.expected.Keys, example.id + " expected fields");
                CollectionAssert.AreEquivalent(V111Golden.ExpectedPlanFieldNames, V111Golden.Obj(example.expected["afterScorePlan"]).Keys, example.id + " afterScorePlan fields");
            }
        }

        // The matrix part of the file classifies its own cases by cardId and family: hit, miss (a natural miss with
        // nothing disabled), disabled, two_hit and mimic_hit.
        [Test]
        public void GoldenV111_EveryChangedCardHasTriggeredAndUntriggeredCases()
        {
            var matrix = V111Golden.Cases.Where(c => c.source == "matrix").ToArray();
            var cards = matrix.Select(c => c.cardId).Distinct(StringComparer.Ordinal).ToArray();
            Assert.That(cards.Length, Is.GreaterThanOrEqualTo(MatrixCards));
            CollectionAssert.IsSubsetOf(ChangedCards, cards, "Every card changed by p1.1.1 needs matrix cases.");
            var results = matrix.ToDictionary(c => c.id, c => ScoreCalculator.Evaluate(V111Golden.Snapshot(c, config), config), StringComparer.Ordinal);
            Func<string, string, V111GoldenCase[]> family = (card, name) => matrix.Where(c => c.cardId == card && c.family == name).ToArray();
            foreach (string card in cards)
            {
                Assert.That(family(card, "hit").Any(c => V111Golden.AllInstances(c, card, 1, true, false, results[c.id])), Is.True, card + " needs a case where one real copy triggers in the reference and in the game.");
                Assert.That(family(card, "disabled").Any(c => V111Golden.AllInstances(c, card, 1, false, true, results[c.id])
                    && V111Golden.Strings(c.input["disabledControllerIds"]).Length > 0), Is.True, card + " needs a case where the disabled copy does not trigger.");
                Assert.That(family(card, "two_hit").Any(c => V111Golden.AllInstances(c, card, 2, true, false, results[c.id])), Is.True, card + " needs two real copies that both trigger.");
                var mixed = family(card, "mimic_hit");
                Assert.That(mixed.Any(c =>
                {
                    var equipped = V111Golden.List(c.input["controllers"]).Select(V111Golden.Obj).ToArray();
                    var expected = V111Golden.Strings(c.expected["triggeredSourceIds"]); var got = results[c.id].triggeredSourceIds;
                    return equipped.Any(x => V111Golden.Str(x, "definitionId") == "C41" && expected.Contains(V111Golden.Str(x, "instanceId")) && got.Contains(V111Golden.Str(x, "instanceId")))
                        && equipped.Any(x => V111Golden.Str(x, "definitionId") == card && expected.Contains(V111Golden.Str(x, "instanceId")) && got.Contains(V111Golden.Str(x, "instanceId")));
                }), Is.True, card + " needs a case where a mimic and the real card both trigger.");
                if (AlwaysTriggered.Contains(card))
                    Assert.That(family(card, "miss"), Is.Empty, card + " triggers on every submission; a miss case would contradict the rules.");
                else
                    Assert.That(family(card, "miss").Any(c => V111Golden.AllInstances(c, card, 1, false, false, results[c.id])), Is.True, card + " needs a natural miss with nothing disabled.");
            }
        }

        [Test]
        public void GoldenV111_HandCheckedCasesAndEveryBossModifierArePresent()
        {
            var cases = V111Golden.Cases;
            Assert.That(cases.Count(c => c.handChecked && c.handChecks != null), Is.GreaterThanOrEqualTo(263), "Hand-checked cases.");
            Assert.That(cases.Where(c => c.handChecked).All(c => c.handChecks != null), Is.True);
            var modifiers = new HashSet<string>(cases.Select(c => V111Golden.Str(c.input, "modifierId")), StringComparer.Ordinal);
            foreach (var modifier in config.bossModifiers) Assert.That(modifiers.Contains(modifier.id), Is.True, modifier.id + " never appears in a golden case");
        }

        [Test]
        public void GoldenV111_ReaderKeepsNullEmptyAndNestedValuesApart()
        {
            var root = V111Golden.Obj(V111Json.Parse("{\"a\":null,\"b\":\"\",\"c\":{\"k\":null},\"d\":[1,-2,true,false,\"x\\u0041\\n\"],\"e\":{}}"));
            Assert.That(root["a"], Is.Null); Assert.That(root["b"], Is.EqualTo(""));
            Assert.That(V111Golden.Obj(root["c"]).ContainsKey("k"), Is.True); Assert.That(V111Golden.Obj(root["c"])["k"], Is.Null);
            CollectionAssert.AreEqual(new object[] { 1L, -2L, true, false, "xA\n" }, V111Golden.List(root["d"]));
            Assert.That(V111Golden.Obj(root["e"]), Is.Empty);
            Assert.Throws<FormatException>(() => V111Json.Parse("{\"a\":1.5}"));
            Assert.Throws<FormatException>(() => V111Json.Parse("{\"a\":1,\"a\":2}"));
            Assert.Throws<FormatException>(() => V111Json.Parse("[1,]"));
            Assert.Throws<FormatException>(() => V111Json.Parse("{} x"));
        }

        // The comparison itself must notice a wrong value, a missing field and a field it does not know.
        [Test]
        public void GoldenV111_ComparisonNoticesAWrongAMissingAndAnUnknownField()
        {
            var example = V111Golden.Cases.First(c => c.source == "new");
            var actual = ScoreCalculator.Evaluate(V111Golden.Snapshot(example, config), config);
            var plan = V111Golden.Obj(example.expected["afterScorePlan"]);
            int baseline = V111Golden.Differences(example.expected, plan, actual, config, true).Count;
            var wrong = new Dictionary<string, object>(example.expected, StringComparer.Ordinal); wrong["score"] = V111Golden.Str(example.expected, "score") + "0";
            Assert.That(V111Golden.Differences(wrong, plan, actual, config, true).Count, Is.EqualTo(baseline + 1));
            var missing = new Dictionary<string, object>(example.expected, StringComparer.Ordinal); missing.Remove("redMult100");
            Assert.That(V111Golden.Differences(missing, plan, actual, config, true).Any(d => d.StartsWith("redMult100: missing", StringComparison.Ordinal)), Is.True);
            var unknown = new Dictionary<string, object>(example.expected, StringComparer.Ordinal); unknown["newField"] = 1L;
            Assert.That(V111Golden.Differences(unknown, plan, actual, config, true).Any(d => d.StartsWith("newField:", StringComparison.Ordinal)), Is.True);
            var wrongPlan = new Dictionary<string, object>(plan, StringComparer.Ordinal); wrongPlan["goldSpent"] = V111Golden.Num(plan, "goldSpent") + 1;
            Assert.That(V111Golden.Differences(example.expected, wrongPlan, actual, config, true).Count, Is.EqualTo(baseline + 1));
        }

        // No field may be compared vacuously: changing any single expected value must produce exactly one more
        // difference. Checked on a few cases of different shapes (plain, with face changes, with a red group).
        [Test]
        public void GoldenV111_ChangingAnySingleExpectedFieldIsNoticed()
        {
            var samples = new[]
            {
                V111Golden.Cases.First(c => c.source == "new"),
                V111Golden.Cases.First(c => V111Golden.List(V111Golden.Obj(c.expected["afterScorePlan"])["faceChanges"]).Count > 0),
                V111Golden.Cases.First(c => V111Golden.List(c.expected["redMemberIndices"]).Count > 1 && V111Golden.List(c.expected["additiveEntries"]).Count > 1),
                V111Golden.Cases.First(c => V111Golden.List(c.expected["multiplierFactors"]).Count > 0 && V111Golden.List(c.expected["controllerBaseAdds"]).Count > 0)
            };
            foreach (var example in samples)
            {
                var actual = ScoreCalculator.Evaluate(V111Golden.Snapshot(example, config), config);
                var plan = V111Golden.Obj(example.expected["afterScorePlan"]);
                Assert.That(V111Golden.Differences(example.expected, plan, actual, config, true), Is.Empty, example.id);
                int checkedFields = 0;
                foreach (string name in example.expected.Keys.Where(k => k != "afterScorePlan").ToArray())
                {
                    var changed = new Dictionary<string, object>(example.expected, StringComparer.Ordinal); changed[name] = Perturb(example.expected[name]);
                    Assert.That(V111Golden.Differences(changed, plan, actual, config, true).Count, Is.EqualTo(1), example.id + " " + name); checkedFields++;
                }
                foreach (string name in plan.Keys.ToArray())
                {
                    var changed = new Dictionary<string, object>(plan, StringComparer.Ordinal); changed[name] = Perturb(plan[name]);
                    Assert.That(V111Golden.Differences(example.expected, changed, actual, config, true).Count, Is.EqualTo(1), example.id + " afterScorePlan." + name); checkedFields++;
                }
                Assert.That(checkedFields, Is.EqualTo(V111Golden.ExpectedFieldNames.Length - 1 + V111Golden.ExpectedPlanFieldNames.Length));
            }
        }
        private static object Perturb(object value)
        {
            if (value is string text) return text + "#";
            if (value is long number) return number + 1;
            if (value is bool flag) return !flag;
            // A list gains one element: a copy of its first one, or a stray marker when it is empty.
            var list = new List<object>((List<object>)value); list.Add(list.Count > 0 ? list[0] : "#"); return list;
        }
    }
}
