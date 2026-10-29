# Pure C# validation host

This optional .NET 10 host compiles the exact `Rules` and `Session` source files without Unity. It executes the 52 frozen score examples (all 18 output fields), five probability vectors, all 2916 texture/buff combinations, the RNG sequence and config schema checks. It does not replace Unity EditMode, PlayMode, build or human playtest evidence.

From the repository root:

```powershell
dotnet run --project 'DicePrototype/Assets/Game/Core/Rules/Validation~/CoreRulesValidation.csproj' -c Release -- 'DicePrototype/Assets/Game/Data'
```

No external packages are required. `report.json` records the real runtime, UTC time, checks and failures. Unity ignores directories ending in `~`.

`UnityTestsCompilation.csproj` is a separate compile-only API check for the production runtime and this agent's EditMode/PlayMode tests. It references the installed Unity 6000.6.7f1 libraries and this project's imported package assemblies, so the Unity project must have been imported first. It neither discovers tests nor runs Unity native functions; a successful compile cannot substitute for a nonzero Unity test run.

```powershell
dotnet build 'DicePrototype/Assets/Game/Core/Rules/Validation~/UnityTestsCompilation.csproj' -c Release
```
