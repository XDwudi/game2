"""Run the v1.1 text contract against the actual C# rules without Unity.

All generated files, build outputs, caches, and temporary files stay below
tools/v11_text/.validation/contract-probe. No existing test host is modified.
"""

import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
from xml.sax.saxutils import escape


ROOT = Path(__file__).resolve().parents[2]
WORK = Path(__file__).resolve().parent / ".validation/contract-probe"
RULES = ROOT / "DicePrototype/Assets/Game/Core/Rules"
CONFIG = ROOT / "DicePrototype/Assets/Game/Data/P0Config.json"

PROGRAM = r'''using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using DiceCraft.Core;

internal static class Program
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };
    private static readonly List<string> Passed = new List<string>();
    private static readonly List<object> Failed = new List<object>();
    private static string original;

    private static int Main(string[] args)
    {
        original = File.ReadAllText(args[0]);
        Run("config.p1.1.0.accepted", () =>
        {
            GameConfig config = Load();
            if (config.configVersion != "p1.1.0") throw new Exception("Live config is not p1.1.0.");
            config.Validate();
        });
        Run("config.p1.0.0.rejected", () =>
        {
            GameConfig config = Load();
            config.configVersion = "p1.0.0";
            Reject<InvalidOperationException>(() => config.Validate(), "Unsupported config/save version.");
        });

        foreach (string category in new[] { "controllers", "consumables", "services", "diceUpgrades", "bossModifiers", "lootKinds" })
        {
            foreach (string field in new[] { "shortText", "details" })
            {
                Run(category + "." + field + ".missing", () =>
                {
                    JsonNode node = JsonNode.Parse(original);
                    node[category][0].AsObject().Remove(field);
                    Reject<ArgumentException>(() => GameConfig.ValidateJson(node.ToJsonString()), "$." + category + "[0] has missing or unknown fields.");
                });
                Run(category + "." + field + ".json-null", () =>
                {
                    JsonNode node = JsonNode.Parse(original);
                    node[category][0][field] = null;
                    Reject<ArgumentException>(() => GameConfig.ValidateJson(node.ToJsonString()), "$." + category + "[0]." + field + " requires string.");
                });
                Run(category + "." + field + ".non-string", () =>
                {
                    JsonNode node = JsonNode.Parse(original);
                    node[category][0][field] = 42;
                    Reject<ArgumentException>(() => GameConfig.ValidateJson(node.ToJsonString()), "$." + category + "[0]." + field + " requires string.");
                });
                foreach (var fixture in new[] { (name: "null", text: (string)null), (name: "empty", text: ""), (name: "whitespace", text: " \t\r\n") })
                {
                    Run(category + "." + field + ".validation-" + fixture.name, () =>
                    {
                        GameConfig config = Load();
                        Set(config, category, field, fixture.text);
                        // v1.1: empty details are allowed (the short text says it all); blank-only details are not.
                        if (field == "details" && fixture.name == "empty") config.Validate();
                        else Reject<InvalidOperationException>(() => config.Validate(),
                            field == "details" && fixture.name == "whitespace" ? "Blank details: " : "Missing " + field + ": ");
                    });
                }
            }
            Run(category + ".unknown-key", () =>
            {
                JsonNode node = JsonNode.Parse(original);
                node[category][0]["unexpectedTextKey"] = "text";
                Reject<ArgumentException>(() => GameConfig.ValidateJson(node.ToJsonString()), "$." + category + "[0] has missing or unknown fields.");
            });
            Run(category + ".shortText.28-characters", () => WithText(category, "shortText", new string('\u5361', 28)).Validate());
            Run(category + ".shortText.29-characters", () =>
                Reject<InvalidOperationException>(() => WithText(category, "shortText", new string('\u5361', 29)).Validate(), "shortText exceeds 28 characters: "));
            Run(category + ".shortText.28-supplementary-characters", () => WithText(category, "shortText", Repeat("\U0001F3B2", 28)).Validate());
            Run(category + ".shortText.29-supplementary-characters", () =>
                Reject<InvalidOperationException>(() => WithText(category, "shortText", Repeat("\U0001F3B2", 29)).Validate(), "shortText exceeds 28 characters: "));
            Run(category + ".details.long-text", () => WithText(category, "details", new string('\u5219', 256)).Validate());
        }

        string result = JsonSerializer.Serialize(new { runtime = Environment.Version.ToString(), passed = Passed.Count, failed = Failed.Count, checks = Passed, failures = Failed }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(args[1], result + "\n");
        Console.WriteLine("Contract checks: " + Passed.Count + " passed, " + Failed.Count + " failed.");
        foreach (var failure in Failed) Console.WriteLine(JsonSerializer.Serialize(failure));
        return Failed.Count == 0 ? 0 : 1;
    }

    private static GameConfig Load()
    {
        // Match the Unity loading path, including C11's numeric requiredCounts keys.
        return JsonSerializer.Deserialize<GameConfig>(GameConfig.PrepareJsonForUnity(original), Options);
    }

    private static GameConfig WithText(string category, string field, string value)
    {
        GameConfig config = Load();
        Set(config, category, field, value);
        return config;
    }

    private static void Set(GameConfig config, string category, string field, string value)
    {
        Array entries = (Array)typeof(GameConfig).GetField(category, BindingFlags.Instance | BindingFlags.Public).GetValue(config);
        object entry = entries.GetValue(0);
        entry.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public).SetValue(entry, value);
    }

    private static string Repeat(string text, int count)
    {
        var result = new System.Text.StringBuilder();
        for (int i = 0; i < count; i++) result.Append(text);
        return result.ToString();
    }

    private static void Reject<T>(Action action, string expectedMessage) where T : Exception
    {
        try { action(); }
        catch (T ex)
        {
            if (!ex.Message.Contains(expectedMessage, StringComparison.Ordinal))
                throw new Exception("Rejected for the wrong reason: " + ex.Message);
            return;
        }
        throw new Exception("Expected " + typeof(T).Name + " containing: " + expectedMessage);
    }

    private static void Run(string name, Action action)
    {
        try { action(); Passed.Add(name); }
        catch (Exception ex) { Failed.Add(new { name, error = ex.ToString() }); }
    }
}
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default=shutil.which("dotnet"), help="dotnet executable (requires .NET 10 SDK)")
    args = parser.parse_args()
    if not args.dotnet:
        parser.error("dotnet was not found; supply --dotnet with the SDK executable")

    WORK.mkdir(parents=True, exist_ok=True)
    env = os.environ.copy()
    for key, folder in {
        "DOTNET_CLI_HOME": "dotnet-home",
        "NUGET_PACKAGES": "nuget-packages",
        "NUGET_HTTP_CACHE_PATH": "nuget-http",
        "NUGET_PLUGINS_CACHE_PATH": "nuget-plugins",
        "DOTNET_BUNDLE_EXTRACT_BASE_DIR": "dotnet-bundle",
        "MSBuildUserExtensionsPath": "msbuild-user",
        "TEMP": "temp",
        "TMP": "temp",
    }.items():
        target = WORK / folder
        target.mkdir(exist_ok=True)
        env[key] = str(target)
    env.update({
        "DOTNET_GENERATE_ASPNET_CERTIFICATE": "false",
        "DOTNET_ADD_GLOBAL_TOOLS_TO_PATH": "false",
        "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
        "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
        "DOTNET_NOLOGO": "1",
    })

    (WORK / "Program.cs").write_text(PROGRAM, encoding="utf-8", newline="\n")
    (WORK / "NuGet.Config").write_text(
        '<configuration><packageSources><clear /></packageSources></configuration>\n',
        encoding="utf-8", newline="\n",
    )
    project = WORK / "ContractProbe.csproj"
    project.write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <Nullable>disable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <NuGetAudit>false</NuGetAudit>
    <RestoreConfigFile>{escape(str(WORK / "NuGet.Config"))}</RestoreConfigFile>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="{escape(str(RULES / '*.cs'))}" />
    <Compile Include="Program.cs" />
  </ItemGroup>
</Project>
''', encoding="utf-8", newline="\n")
    results_path = WORK / "results.json"
    # Remove only the previous generated result so a failed build cannot look successful.
    results_path.unlink(missing_ok=True)
    command = [args.dotnet, "run", "--project", str(project), "--configuration", "Release", "--verbosity", "quiet", "--", str(CONFIG), str(results_path)]
    completed = subprocess.run(command, cwd=WORK, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace")
    (WORK / "dotnet.log").write_text(completed.stdout, encoding="utf-8", newline="\n")
    binding = {
        "python": sys.version,
        "command": command,
        "exit_code": completed.returncode,
        "sources_sha256": {str(path.relative_to(ROOT)).replace("\\", "/"): hashlib.sha256(path.read_bytes()).hexdigest() for path in [CONFIG, *sorted(RULES.glob("*.cs")), Path(__file__).resolve()]},
        "results": json.loads(results_path.read_text(encoding="utf-8")) if results_path.exists() else None,
    }
    (WORK / "run.json").write_text(json.dumps(binding, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(completed.stdout, end="" if completed.stdout.endswith("\n") else "\n")
    print("Evidence:", (WORK / "run.json").relative_to(ROOT))
    return completed.returncode or (0 if binding["results"] is not None else 1)


if __name__ == "__main__":
    raise SystemExit(main())
