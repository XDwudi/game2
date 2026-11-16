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
            {"C47", new[]{"factor","perControllerUnits2"}},
            {"C48", new[]{"baseAddPerEmptySubmitSlot"}},
            {"C49", new[]{"factor","allowZero"}},
            {"C50", new[]{"baseAdd"}},
            {"C51", new[]{"initialMultiplierUnits2","addUnits2PerRealControllerSale","maxGrowthPerShop","mimicsDoNotDuplicateStateGrowth","saleRecipients"}},
            {"C52", new[]{"baseBuyPrice","baseSellPrice","sellPriceAddPerSuccessfulStageFinalize"}},
            {"C53", new[]{"factorUnits2","uniform","drawOncePerRealOwnerPerSubmit","mimicReusesOwnerOutcome"}},
            // v0.9
            {"C54", new[]{"growthPerEvent"}},
            {"C55", new[]{"growthPerEvent"}},
            {"C56", new[]{"growthPerEvent"}},
            {"C57", new[]{"growthPerEvent","minRedGroup"}},
            {"C58", new[]{"growthPerEvent"}},
            {"C59", new[]{"growthPerEvent"}},
            {"C60", new[]{"growthPerEvent"}},
            {"C61", new[]{"growthPerEvent"}},
            {"C62", new[]{"growthPerEvent"}},
            {"C63", new[]{"perDieUnits10"}},
            {"C64", new[]{"retriggers","index"}},
            {"C65", new[]{"retriggers","requireFullCapacity"}},
            {"C66", new[]{"retriggers","pips"}},
            {"C67", new[]{"retriggers","pips"}},
            {"C68", new[]{"retriggers"}},
            {"C69", new[]{"retriggers"}},
            {"C70", new[]{"baseAddPerPoolDie"}},
            {"C71", new[]{"pip","stepNumerator","stepDenominator"}},
            {"C72", new[]{"poolPipSumMultiplier"}},
            {"C73", new[]{"idempotent"}},
            {"C74", new[]{"scoreMultiplier"}},
            {"C75", new[]{"scoreMultiplier","maxRollsUsed"}},
            {"C76", new[]{"baseAddPerExtraRoll"}},
            {"C77", new[]{"scoreMultiplier"}},
            {"C78", new[]{"rollsPerHandDelta"}},
            {"C79", new[]{"submissionsDelta"}},
            {"C80", new[]{"pairSum","baseAddPerPair"}},
            {"C81", new[]{"scoreMultiplier"}},
            {"C82", new[]{"scoreMultiplier","handIds"}},
            {"C83", new[]{"scoreMultiplier"}},
            {"C84", new[]{"handMultAdd100","minSamePip"}},
            {"C85", new[]{"handMultAdd100","minSamePip"}},
            {"C86", new[]{"scoreMultiplier","handIds"}},
            {"C87", new[]{"minSamePip","stepNumerator","stepDenominator"}},
            {"C88", new[]{"scoreMultiplier","minDice"}},
            {"C89", new[]{"stepNumerator","stepDenominator"}},
            {"C90", new[]{"growthPerEvent"}},
            {"C91", new[]{"perDieAdd","textures"}},
            {"C92", new[]{"perDieAdd"}},
            {"C93", new[]{"dieContributionMultiplier"}},
            {"C94", new string[0]},
            {"C95", new[]{"goldPerStage"}},
            {"C96", new[]{"interestPrincipalAdd"}},
            {"C97", new[]{"goldPerTrigger","maxTriggersPerStage","targetDivisor"}},
            {"C98", new[]{"priceDelta"}},
            {"C99", new[]{"saleBonusPerStage"}},
            {"C100", new[]{"growthPerBuyPrice"}},
            {"C101", new[]{"scoreMultiplier","submissionsDelta"}},
            {"C102", new[]{"scoreMultiplier"}},
            {"C103", new[]{"scoreMultiplier","maxOwnedPermanentDice","strictScoreMultiplier","strictMaxOwnedPermanentDice"}},
            // v1.0
            {"C104", new[]{"scoreMultiplier","minDice"}},
            {"C105", new[]{"scoreMultiplier"}},
            {"C106", new[]{"scoreMultiplier","minDice"}},
            {"C107", new[]{"perDieAdd"}},
            {"C108", new[]{"baseAddPerPip","index"}},
            {"C109", new[]{"retriggers","minDice"}},
            {"C110", new[]{"itemsPerStage"}},
            {"C111", new[]{"giftChoices","giftPicks"}},
            {"C112", new[]{"rarity"}},
            {"C113", new[]{"giftChoices","giftPicks"}},
            {"C114", new[]{"enchantsPerBoss"}},
            {"C115", new[]{"facesPerStage","upgradeIds"}},
            {"C116", new[]{"dicePerBoss"}},
            {"C117", new[]{"targetIndices"}},
            {"C118", new[]{"targetIndices"}},
            {"C119", new[]{"index"}},
            {"C120", new[]{"delta"}},
            {"C121", new[]{"delta"}},
            {"C122", new[]{"pip"}},
            {"C123", new[]{"pips"}},
            {"C124", new[]{"delta"}},
            {"C125", new[]{"targetIndices","textures"}},
            {"C126", new[]{"delta"}},
            {"C127", new[]{"submissionOrdinal","minDice","sourceIndex"}},
            {"C128", new[]{"dicePerStage"}},
            {"C129", new[]{"growthPerEvent"}},
            {"C130", new[]{"scoreMultiplier"}},
            {"C131", new string[0]},
            {"C132", new[]{"growthPerEvent"}},
            {"C133", new[]{"goldPerBoss","goldPerPressure"}},
            {"C134", new[]{"growthPerEvent"}},
            {"C135", new[]{"baseAddPerItem"}},
            {"C136", new[]{"dicePerStage"}},
            {"C137", new[]{"perDieAdd"}},
            {"C138", new[]{"scoreMultiplier","goldCost"}},
            {"C139", new[]{"goldPerStep","stepUnits4","maxSteps"}},
            {"C140", new string[0]},
            {"C141", new[]{"initialMultiplierUnits2","expireGrowth"}},
            {"C142", new[]{"handMultAdd100"}},
            {"C143", new[]{"retriggers","requiredDice"}},
            {"C144", new[]{"scoreMultiplier"}},
            {"C145", new[]{"glassBreakPercent"}},
            {"C146", new[]{"idempotent"}},
            {"C147", new[]{"stepUnits4"}},
            {"C148", new[]{"perRollUnits2"}},
            {"C149", new[]{"scoreMultiplier","requiredDice"}},
            {"C150", new string[0]},
            {"C151", new[]{"perSlotUnits2"}},
            {"C152", new[]{"rarity","stepUnits4"}},
            {"C153", new[]{"scoreMultiplier","submissionOrdinal"}}
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
            // DTO fields include the required v1.1 shortText/details keys on all six content categories.
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
