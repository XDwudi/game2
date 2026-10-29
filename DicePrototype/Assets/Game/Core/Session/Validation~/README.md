# Session validation evidence

This .NET 10 host invokes the Session NUnit test methods using the NUnit framework already installed in the Unity project package cache. It is an additional rules test host, **not** a Unity EditMode/PlayMode result. The Unity Editor must discover and run the same tests separately.

From repository root (PowerShell):

```powershell
$env:DOTNET_CLI_HOME = "$PWD/DicePrototype/Assets/Game/Core/Session/Validation~/.dotnet"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
dotnet run --project 'DicePrototype/Assets/Game/Core/Session/Validation~/SessionValidation.csproj' -c Release
```

The project references the resolved local `com.unity.ext.nunit` cache path; if the package lock changes, update the reference only after verifying the installed package. No NuGet packages are downloaded. `report.json` contains each executed case and `run.log` contains raw output. `source_manifest.json` binds the report to the source/test inputs. Transient `bin`, `obj` and `.dotnet` folders are not deliverables.

Tests construct explicit Debug fixtures for otherwise rare boundary states. The six-stage automated scenario is a Debug fixture, not a normal gameplay/balance claim. Concrete file atomic replacement, recovery from a damaged primary save, the Unity presentation busy overlay, and real builds are integration-layer acceptance tasks.
