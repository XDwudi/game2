using System.Text.Json;
namespace UnityEngine {
 public static class JsonUtility {public static T FromJson<T>(string s)=>JsonSerializer.Deserialize<T>(s,new JsonSerializerOptions{IncludeFields=true});public static string ToJson(object v,bool pretty=false)=>JsonSerializer.Serialize(v,new JsonSerializerOptions{IncludeFields=true});}
 public static class Application {public static string unityVersion=>"6000.6.7f1";}
 public class TextAsset {public string text;}
 public static class Debug {public static void Log(object x) {}}
}
namespace UnityEditor {
 [AttributeUsage(AttributeTargets.Method)]public class MenuItem:Attribute {public MenuItem(string s) {}}
 public enum BuildTarget {StandaloneOSX,StandaloneWindows64}
 public enum BuildOptions {StrictMode}
 public static class AssetDatabase {public static string GetAssetPath(object v)=>throw new NotSupportedException();public static T LoadAssetAtPath<T>(string p)=>throw new NotSupportedException();}
 public static class EditorUserBuildSettings {public static BuildTarget activeBuildTarget;}
 public static class BuildPipeline {public static Build.Reporting.BuildReport BuildPlayer(Build.Profile.BuildPlayerWithProfileOptions o)=>throw new NotSupportedException("BuildPlayer forbidden in reviewer probe");}
}
namespace UnityEditor.Build.Profile {
 public class BuildProfile {public static BuildProfile GetActiveBuildProfile()=>throw new NotSupportedException();}
 public class BuildPlayerWithProfileOptions {public BuildProfile buildProfile;public string locationPathName;public UnityEditor.BuildOptions options;}
}
namespace UnityEditor.Build.Reporting {
 public enum BuildResult {Succeeded,Failed}
 public class BuildReport {public Summary summary;}
 public class Summary {public BuildResult result;public int totalErrors,totalWarnings;public ulong totalSize;public TimeSpan totalTime;public Guid guid;}
}
namespace DiceCraft.Core {
 public class GameConfig {public int[] controllers=new int[50],stages=new int[24],handDefinitions=new int[13];public static string PrepareJsonForUnity(string s)=>s;public void Validate()=>throw new NotSupportedException();}
}
