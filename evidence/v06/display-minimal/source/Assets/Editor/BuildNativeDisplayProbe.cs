using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildNativeDisplayProbe
{
    [Serializable] private sealed class Source { public string path, sha256; }
    [Serializable] private sealed class BuildRecord
    {
        public string schema = "p06-native-minimal-build-1", scope = "Disposable isolation experiment, not a release or acceptance build";
        public string utc, status, unityVersion, outputPath, buildGuid, exception;
        public string pipeline = "Built-in", options = "Development", graphicsApi = "Direct3D11";
        public int errors, warnings; public ulong bytes; public double seconds;
        public Source[] inputs; public string[] messages;
    }
    public static void BuildWindows() { Build(BuildOptions.Development, "native-minimal-build", "development"); }
    public static void BuildNonDevelopmentWindows() { Build(BuildOptions.None, "NativeMinimalRelease", "non-development"); }
    private static void Build(BuildOptions options, string directoryName, string variant)
    {
        string project = Directory.GetParent(Application.dataPath).FullName;
        if (Path.GetFileName(project) != "native-minimal-project" || Application.unityVersion != "6000.6.7f1")
            throw new InvalidOperationException("This build method only accepts the disposable native-minimal-project under Unity6000.6.7f1.");
        string workspace = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(project).FullName).FullName).FullName).FullName;
        if (!File.Exists(Path.Combine(workspace, "AGENTS.md"))) throw new DirectoryNotFoundException("Workspace root not found.");
        string buildDirectory = Path.Combine(workspace, "artifacts/cache/v06", directoryName);
        string evidence = Path.Combine(workspace, "evidence/v06/display-minimal");
        Directory.CreateDirectory(buildDirectory); Directory.CreateDirectory(evidence);
        var record = new BuildRecord { utc = DateTime.UtcNow.ToString("O"), status = "build_started", unityVersion = Application.unityVersion, options = options.ToString(),
            outputPath = Path.Combine(buildDirectory, "NativeMinimal.exe"), inputs = new[] { "Assets/NativeDisplayProbe.cs", "Assets/Editor/BuildNativeDisplayProbe.cs", "Packages/manifest.json", "ProjectSettings/ProjectVersion.txt" }
                .Select(p => new Source { path = p, sha256 = Hash(Path.Combine(project, p)) }).ToArray() };
        string reportPath = Path.Combine(evidence, "build-" + variant + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json");
        try
        {
            GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
            QualitySettings.vSyncCount = 0;
            PlayerSettings.companyName = "NativeDisplayDiagnostic"; PlayerSettings.productName = "NativeMinimal";
            PlayerSettings.bundleVersion = "0.0.1-diagnostic";
            PlayerSettings.runInBackground = true; PlayerSettings.defaultScreenWidth = 1920; PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.resizableWindow = true;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            const string scenePath = "Assets/NativeMinimal.unity";
            if (!EditorSceneManager.SaveScene(scene, scenePath)) throw new IOException("Could not save isolated empty scene.");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
            AssetDatabase.SaveAssets();
            var built = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { scenePath }, locationPathName = record.outputPath,
                target = BuildTarget.StandaloneWindows64, options = options });
            record.status = built.summary.result == BuildResult.Succeeded ? "diagnostic_build_succeeded" : "diagnostic_build_failed";
            record.buildGuid = built.summary.guid.ToString(); record.errors = built.summary.totalErrors;
            record.warnings = built.summary.totalWarnings; record.bytes = built.summary.totalSize; record.seconds = built.summary.totalTime.TotalSeconds;
            record.messages = built.steps.SelectMany(s => s.messages).Where(m => m.type != LogType.Log).Select(m => m.type + ": " + m.content).ToArray();
            if (built.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Disposable native control build failed.");
        }
        catch (Exception ex) { record.exception = ex.ToString(); record.status = "diagnostic_build_failed"; throw; }
        finally { File.WriteAllText(reportPath, JsonUtility.ToJson(record, true), new UTF8Encoding(false)); }
    }
    private static string Hash(string path)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
    }
}
