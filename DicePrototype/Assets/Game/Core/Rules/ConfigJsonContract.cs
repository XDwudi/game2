using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace DiceCraft.Core
{
    /// <summary>
    /// Structural validation only: prevents JsonUtility silently accepting missing/unknown fields.
    /// Actual rule data remains the explicit Serializable DTO contract in GameConfig.cs.
    /// </summary>
    internal static class ConfigJsonContract
    {
        private static readonly Dictionary<string, string[]> Parameters = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            {"C01", new[]{"goldPerValidSubmission","extraInterestPerTen"}},
            {"C02", new[]{"scoreMultiplier"}},
            {"C03", new[]{"index"}},
            {"C05", new[]{"rollGrant","minUnlocked","maxUnlocked","perSourcePerHandLimit","snapshotBeforeAnyGrant"}},
            {"C06", new[]{"minDistinct","scoreMultiplier"}},
            {"C07", new[]{"submissionOrdinal","requiredDice","sourceIndex","targetIndex"}},
            {"C08", new[]{"scoreMultiplier"}},
            {"C09", new[]{"capacityDelta","scoreMultiplier"}},
            {"C10", new[]{"pip","perDieAdd"}},
            {"C11", new[]{"requiredCounts","scoreMultiplier"}},
            {"C12", new[]{"oddToEvenAdd","idempotent"}},
            {"C13", new[]{"oddPerDieAdd"}},
            {"C14", new[]{"scoreMultiplier"}},
            {"C15", new[]{"scanAllInteriorIndices","pattern","freezeAllTargets"}},
            {"C17", new[]{"pips","perDieAdd"}},
            {"C18", new[]{"scoreMultiplier"}},
            {"C19", new[]{"maxPermanentSixFaceSum","perDieAdd"}},
            {"C20", new[]{"lastActualCapacitySlotPip","perDieAdd","requireFullCapacity"}},
            {"C21", new[]{"capacityDelta"}},
            {"C22", new[]{"capacityDelta"}},
            {"C24", new[]{"redGroupKeyFrom","redGroupKeyTo","idempotent"}},
            {"C25", new[]{"mergeAllEffectiveRed","idempotent"}},
            {"C26", new[]{"requiredDice","allPhysicalFacesColor"}},
            {"C27", new[]{"baseAddPerDistinctPairedPip","minCountPerGroup"}},
            {"C28", new[]{"minDistinctPairs","scoreMultiplier"}},
            {"C29", new[]{"minimumStraightLength","idempotent"}},
            {"C30", new[]{"minSamePip","scoreMultiplier"}},
            {"C31", new[]{"minSamePip","scoreMultiplier"}},
            {"C32", new[]{"distinctPips","scoreMultiplier"}},
            {"C33", new[]{"minDice","firstMinusSecondAtLeast","scoreMultiplier"}},
            {"C34", new[]{"targetPips","probabilityPointsPerSource"}},
            {"C35", new[]{"targetPips","probabilityPointsPerSource"}},
            {"C36", new[]{"minDice","requireEmptySlot","indices","delta"}},
            {"C37", new[]{"minDice","requireEmptySlot","indices","delta"}},
            {"C38", new[]{"targetIndices","color"}},
            {"C39", new[]{"effectiveColor","perDieAdd"}},
            {"C40", new[]{"indices","strictIncreasingArithmetic","scoreMultiplier"}},
            {"C41", new[]{"target","copyBaseAbility","copyTargetEnchantment","copyExistingFundAccrual","shareDynamicTargetState","retargetOnLossInStage"}},
            {"C42", new[]{"scoreMultiplier","destroyChancePercentAfterValidSubmission","riskBearer"}},
            {"C43", new[]{"samePrimaryHandAsPreviousValidSubmit","levelAddPerSource","crossStageHistory","scatterCanUpgrade"}},
            {"C44", new[]{"baseAddPerFrozenGold"}},
            {"C45", new[]{"matchingDieContributionMultiplier","colors","initialDrawOnAcquisition","nextDrawEveryValidSubmit","sharedStateAdvancesOnce"}},
            {"C46", new[]{"scoreMultiplier"}},
            {"C47", new[]{"factor"}},
            {"C48", new[]{"baseAddPerEmptySubmitSlot"}},
            {"C49", new[]{"factor","allowZero"}},
            {"C50", new[]{"baseAdd"}},
            {"C51", new[]{"initialMultiplierUnits2","addUnits2PerRealControllerSale","mimicsDoNotDuplicateStateGrowth","saleRecipients"}},
            {"C52", new[]{"baseBuyPrice","baseSellPrice","sellPriceAddPerSuccessfulStageFinalize"}},
            {"C53", new[]{"factorUnits2","uniform","drawOncePerRealOwnerPerSubmit","mimicReusesOwnerOutcome"}}
        };

        public static void Validate(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 4 * 1024 * 1024)
                throw new ArgumentException("Configuration JSON is empty or too large.");
            var reader = new Reader(json);
            Node root = reader.Value(0);
            reader.End();
            ValidateNode(root, typeof(GameConfig), "$", null);
        }

        private static void ValidateNode(Node node, Type type, string path, string parameterId)
        {
            if (type == typeof(string)) { Require(node.kind == 's' || (path.EndsWith(".riskTarget", StringComparison.Ordinal) && node.kind == '0'), path + " requires string."); return; }
            if (type == typeof(bool)) { Require(node.kind == 'b', path + " requires boolean."); return; }
            if (type == typeof(int) || type == typeof(uint) || type == typeof(float))
            {
                Require(node.kind == 'n', path + " requires number.");
                if (type == typeof(int)) Require(int.TryParse(node.text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _), path + " requires int32.");
                else if (type == typeof(uint)) Require(uint.TryParse(node.text, NumberStyles.None, CultureInfo.InvariantCulture, out _), path + " requires uint32.");
                else Require(float.TryParse(node.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) && !float.IsNaN(f) && !float.IsInfinity(f), path + " requires finite float.");
                return;
            }
            if (type.IsArray)
            {
                Require(node.kind == 'a', path + " requires array.");
                for (int i = 0; i < node.items.Count; i++) ValidateNode(node.items[i], type.GetElementType(), path + "[" + i + "]", null);
                return;
            }
            Require(node.kind == 'o', path + " requires object.");
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            if (type == typeof(ControllerParameters))
            {
                Require(parameterId != null && Parameters.ContainsKey(parameterId), path + " has unknown controller ID.");
                var required = new HashSet<string>(Parameters[parameterId], StringComparer.Ordinal);
                fields = fields.Where(f => required.Contains(f.Name)).ToArray();
            }
            Func<FieldInfo, string> jsonName = f => type == typeof(RequiredPipCounts) ? f.Name.Substring(3) : f.Name;
            var names = new HashSet<string>(fields.Select(jsonName), StringComparer.Ordinal);
            Require(node.fields.Count == names.Count && names.SetEquals(node.fields.Keys), path + " has missing or unknown fields.");
            string controllerId = type == typeof(ControllerDefinition) ? node.fields["id"].text : null;
            foreach (var field in fields)
                ValidateNode(node.fields[jsonName(field)], field.FieldType, path + "." + jsonName(field), field.Name == "parameters" ? controllerId : null);
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new ArgumentException("Configuration JSON: " + message); }
        private sealed class Node
        {
            public char kind;
            public string text;
            public Dictionary<string, Node> fields;
            public List<Node> items;
        }
        private sealed class Reader
        {
            private readonly string input;
            private int index;
            private static readonly Regex Number = new Regex(@"^-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?$", RegexOptions.CultureInvariant);
            public Reader(string input) { this.input = input; }
            private void Whitespace() { while (index < input.Length && (input[index] == ' ' || input[index] == '\r' || input[index] == '\n' || input[index] == '\t')) index++; }
            public void End() { Whitespace(); Require(index == input.Length, "Trailing input."); }
            private char Peek() { Whitespace(); Require(index < input.Length, "Unexpected end."); return input[index]; }
            private void Take(char expected) { Require(Peek() == expected, "Expected '" + expected + "'."); index++; }
            public Node Value(int depth)
            {
                Require(depth <= 32, "Excessive nesting.");
                char ch = Peek();
                if (ch == '{')
                {
                    index++;
                    var n = new Node { kind = 'o', fields = new Dictionary<string, Node>(StringComparer.Ordinal) };
                    if (Peek() == '}') { index++; return n; }
                    while (true)
                    {
                        string key = String(); Take(':');
                        Require(!n.fields.ContainsKey(key), "Duplicate field '" + key + "'.");
                        n.fields.Add(key, Value(depth + 1));
                        char next = Peek(); index++;
                        if (next == '}') return n;
                        Require(next == ',', "Expected comma/object end.");
                    }
                }
                if (ch == '[')
                {
                    index++;
                    var n = new Node { kind = 'a', items = new List<Node>() };
                    if (Peek() == ']') { index++; return n; }
                    while (true)
                    {
                        n.items.Add(Value(depth + 1));
                        char next = Peek(); index++;
                        if (next == ']') return n;
                        Require(next == ',', "Expected comma/array end.");
                    }
                }
                if (ch == '"') return new Node { kind = 's', text = String() };
                int begin = index;
                while (index < input.Length && input[index] != ',' && input[index] != ']' && input[index] != '}' && !char.IsWhiteSpace(input[index])) index++;
                string token = input.Substring(begin, index - begin);
                if (token == "true" || token == "false") return new Node { kind = 'b', text = token };
                if (token == "null") return new Node { kind = '0' };
                Require(Number.IsMatch(token), "Invalid JSON token.");
                return new Node { kind = 'n', text = token };
            }
            private string String()
            {
                Take('"');
                var result = new StringBuilder();
                while (index < input.Length)
                {
                    char ch = input[index++];
                    if (ch == '"') return result.ToString();
                    Require(ch >= ' ', "Unescaped control in string.");
                    if (ch != '\\') { result.Append(ch); continue; }
                    Require(index < input.Length, "Incomplete string escape.");
                    char escaped = input[index++];
                    switch (escaped)
                    {
                        case '"': case '\\': case '/': result.Append(escaped); break;
                        case 'b': result.Append('\b'); break;
                        case 'f': result.Append('\f'); break;
                        case 'n': result.Append('\n'); break;
                        case 'r': result.Append('\r'); break;
                        case 't': result.Append('\t'); break;
                        case 'u':
                            Require(index + 4 <= input.Length, "Incomplete Unicode escape.");
                            Require(ushort.TryParse(input.Substring(index, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort code), "Invalid Unicode escape.");
                            result.Append((char)code); index += 4; break;
                        default: throw new ArgumentException("Configuration JSON: Unknown string escape.");
                    }
                }
                throw new ArgumentException("Configuration JSON: Unterminated string.");
            }
        }
    }
}
