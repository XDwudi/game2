// Simulator-only stand-in for Core/Rules/ConfigValidation.cs.
// The shipped Validate() pins p0.6.0 values (prices, red table, interest ...). A balance candidate changes
// exactly those values, so here only structural checks remain. Accessors are copied unchanged.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DiceCraft.Core
{
    public sealed partial class GameConfig
    {
        public EnchantmentDefinition Enchantment(string id) => Required(enchantments, d => d.id, id);
        public ControllerDefinition Controller(string id) => Required(controllers, d => d.id, id);
        public HandDefinition Hand(string id) => Required(handDefinitions, d => d.id, id);
        public ColorDefinition Color(string id) => Required(colorDefinitions, d => d.id, id);
        public ConsumableDefinition Consumable(string id) => Required(consumables, d => d.id, id);
        public ServiceDefinition Service(string id) => Required(services, d => d.id, id);
        public DieUpgradeDefinition Upgrade(string id) => Required(diceUpgrades, d => d.id, id);
        private static T Required<T>(T[] entries, Func<T, string> key, string id)
        {
            if (entries == null) throw new InvalidOperationException("Configuration is not loaded.");
            foreach (T entry in entries) if (key(entry) == id) return entry;
            throw new ArgumentException("Unknown configured ID: " + id, nameof(id));
        }
        public static void ValidateJson(string json) => ConfigJsonContract.Validate(json);
        public static string PrepareJsonForUnity(string json)
        {
            ValidateJson(json);
            return Regex.Replace(json, "(\"requiredCounts\"\\s*:\\s*\\{)([^{}]*)(\\})", m => m.Groups[1].Value + Regex.Replace(m.Groups[2].Value, "\"([23])\"(?=\\s*:)", x => "\"pip" + x.Groups[1].Value + "\"") + m.Groups[3].Value);
        }
        public void Validate()
        {
            Check(controllers != null && handDefinitions != null && handDefinitions.Length == 13 && stages != null && stages.Length > 0, "Structure.");
            Check(redRewards != null && redRewards.Length == 9, "Red rewards.");
            ValidateFaces(DiceFactory.CreateStandard(this, 1).faces);
        }
        public string Opposite(string id)
        {
            switch (id) { case "F01": return faceOpposites.F01; case "F02": return faceOpposites.F02; case "F03": return faceOpposites.F03; case "F04": return faceOpposites.F04; case "F05": return faceOpposites.F05; case "F06": return faceOpposites.F06; default: throw new ArgumentException("Unknown face ID: " + id); }
        }
        public void ValidateFaces(FaceState[] faces)
        {
            Check(faces != null && faces.Length == 6 && faces.Select(f => f.faceId).Distinct().Count() == 6, "Faces.");
            foreach (var f in faces) Check(f.permanentPip >= minPipValue && f.permanentPip <= maxPipValue && (f.baseColor == "red" || f.baseColor == "blue") && f.baseWeight > 0 && (f.texture == "none" || f.texture == "smooth" || f.texture == "rough"), "Invalid face: " + f.faceId);
        }
        internal static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
