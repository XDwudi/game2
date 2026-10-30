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
            {"C01", new[]{"extraInterestPerTen"}},
            {"C02", new[]{"scoreMultiplier"}},
            {"C03", new[]{"occupiedIndex"}},
            {"C04", new[]{"submissionBonus","rollBonus"}},
            {"C05", new[]{"rollGrant","perHandLimit","minUnlocked","maxUnlocked","checkOnlyAfterRoll"}},
            {"C06", new[]{"minDistinct","scoreMultiplier"}},
            {"C07", new[]{"onlyFirstSubmission","requiredDice","sourceIndex","targetIndex"}},
            {"C08", new[]{"scoreMultiplier"}},
            {"C09", new[]{"capacityDelta","scoreMultiplier"}},
            {"C10", new[]{"pip","contributionAdd"}},
            {"C11", new[]{"pipA","countA","pipB","countB","scoreMultiplier"}},
            {"C12", new[]{"oddPipAdd"}},
            {"C13", new[]{"oddContributionAdd"}},
            {"C14", new[]{"scoreMultiplier"}},
            {"C15", new[]{"pattern","targetIndex"}},
            {"C16", new[]{"rollBonus"}},
            {"C17", new[]{"maxPip","contributionAdd"}},
            {"C18", new[]{"scoreMultiplier"}},
            {"C19", new[]{"maxPermanentFaceSum","contributionAdd"}},
            {"C20", new[]{"lastSlotPip","dieContributionAdd","requireFullCapacity"}},
            {"C21", new[]{"capacityDelta"}},
            {"C22", new[]{"capacityDelta"}},
            {"C23", new[]{"rollBonus"}},
            {"C24", new[]{"redGroupPipFrom","redGroupPipTo"}},
            {"C25", new[]{"mergeAllRed"}},
            {"C26", new[]{"requiredDice","allFacesColor"}},
            {"C27", new[]{"minSamePip","baseAdd","perSubmitLimit"}},
            {"C28", new[]{"minDifferentPairs","scoreMultiplier"}},
            {"C29", new[]{"minimumStraightLength"}},
            {"C30", new[]{"minSamePip","scoreMultiplier"}},
            {"C31", new[]{"minSamePip","scoreMultiplier"}},
            {"C32", new[]{"requiredDistinct","scoreMultiplier"}},
            {"C33", new[]{"minDice","firstMinusSecondAtLeast","scoreMultiplier"}},
            {"C34", new[]{"targetPips","maxTransferUnits"}},
            {"C35", new[]{"targetPips","maxTransferUnits"}},
            {"C36", new[]{"requireEmptySlot","minDice","indices","permanentFaceDelta"}},
            {"C37", new[]{"requireEmptySlot","minDice","indices","permanentFaceDelta"}},
            {"C38", new[]{"index","currentPhysicalFaceColor"}},
            {"C39", new[]{"effectiveColor","contributionAdd"}},
            {"C40", new[]{"indices","positiveCommonDifference","scoreMultiplier"}}
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
            if (type == typeof(string)) { Require(node.kind == 's', path + " requires string."); return; }
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
            var names = new HashSet<string>(fields.Select(f => f.Name), StringComparer.Ordinal);
            Require(node.fields.Count == names.Count && names.SetEquals(node.fields.Keys), path + " has missing or unknown fields.");
            string controllerId = type == typeof(ControllerDefinition) ? node.fields["id"].text : null;
            foreach (var field in fields)
                ValidateNode(node.fields[field.Name], field.FieldType, path + "." + field.Name, field.Name == "parameters" ? controllerId : null);
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
                        case 'b': result.Append('\b'); break; case 'f': result.Append('\f'); break;
                        case 'n': result.Append('\n'); break; case 'r': result.Append('\r'); break; case 't': result.Append('\t'); break;
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
