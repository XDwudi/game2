using System.Reflection;
using System.Text.Json;
var fixture=Path.GetFullPath(args[0]);Directory.SetCurrentDirectory(Path.Combine(fixture,"DicePrototype"));
bool accepted=false;string error=null;
try{typeof(DiceCraft.Editor.PrototypeBuild).GetMethod("ValidateGate",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);accepted=true;}
catch(Exception e){error=(e.InnerException??e).ToString();}
Console.WriteLine(JsonSerializer.Serialize(new{accepted,error,scope="Actual C# ValidateGate with API stubs; never invokes BuildPlayer or Unity"}));
