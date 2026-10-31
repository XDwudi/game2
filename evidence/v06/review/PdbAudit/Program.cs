using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

string root=Path.GetFullPath(args[0]),output=Path.GetFullPath(args[1]);
string directory=args.Length>2?Path.GetFullPath(args[2]):Path.Combine(root,"DicePrototype/Library/ScriptAssemblies");
if(File.Exists(output))throw new InvalidOperationException("Retain previous evidence; choose a new output path.");
var docs=new List<object>();var assemblies=new List<object>();int mismatches=0;
string Hash(string p)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))).ToLowerInvariant();
foreach(var name in new[]{"DiceCraft.Core","DiceCraft.Runtime","DiceCraft.EditMode","DiceCraft.PlayModeTests","DiceCraft.Editor"}) {
 string dll=Path.Combine(directory,name+".dll"),pdb=Path.ChangeExtension(dll,".pdb");
 if(!File.Exists(dll))continue;
 using var ds=File.OpenRead(dll);using var pe=new PEReader(ds);var dm=pe.GetMetadataReader();
 var mvid=dm.GetGuid(dm.GetModuleDefinition().Mvid).ToString();
 using var ps=File.OpenRead(pdb);using var provider=MetadataReaderProvider.FromPortablePdbStream(ps);var reader=provider.GetMetadataReader();
 var cv=pe.ReadDebugDirectory().Where(e=>e.Type==DebugDirectoryEntryType.CodeView).Select(pe.ReadCodeViewDebugDirectoryData).Single();
 var pdbGuid=new Guid(reader.DebugMetadataHeader.Id.Take(16).ToArray());
 bool pdbMatchesDll=cv.Guid==pdbGuid;if(!pdbMatchesDll)mismatches++;
 int projectDocs=0;
 foreach(var h in reader.Documents) {
  var doc=reader.GetDocument(h);string source=reader.GetString(doc.Name);
  if(!source.Replace('\\','/').Contains("Assets/Game/"))continue;
  string path=Path.IsPathRooted(source)?source:Path.Combine(root,"DicePrototype",source);
  byte[] expected=reader.GetBlobBytes(doc.Hash);string expectedHex=Convert.ToHexString(expected).ToLowerInvariant();
  string actual=File.Exists(path)?Convert.ToHexString(expected.Length==20?SHA1.HashData(File.ReadAllBytes(path)):SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant():null;
  bool matches=expectedHex==actual;if(!matches)mismatches++;
  docs.Add(new{assembly=name,source,expectedChecksum=expectedHex,currentChecksum=actual,matches,sourceLastWriteUtc=File.Exists(path)?File.GetLastWriteTimeUtc(path):(DateTime?)null});projectDocs++;
 }
 assemblies.Add(new{name,mvid,dllSha256=Hash(dll),pdbSha256=Hash(pdb),dllLastWriteUtc=File.GetLastWriteTimeUtc(dll),pdbMatchesDll,projectDocuments=projectDocs});
}
File.WriteAllText(output,JsonSerializer.Serialize(new{utc=DateTime.UtcNow,reviewerRanUnity=false,directory,mismatches,assemblies,documents=docs},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PDB audit: {assemblies.Count} assemblies, {docs.Count} project documents, {mismatches} mismatches. {output}");
