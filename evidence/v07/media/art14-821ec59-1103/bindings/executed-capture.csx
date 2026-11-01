// Cache-only eval body. Run by the root agent after fresh, isolated Bootstrap startup.
// No TMP, rules commands, PlayerPrefs writes, asset imports, packing or persistent scenes.
string input=@"E:\agent_workspace\gpt\game2\artifacts\cache\v07-art14-resource-check";
string output=@"E:\agent_workspace\gpt\game2\artifacts\cache\v07-art14-gpu-1103";
var app=UnityEngine.Object.FindAnyObjectByType<DiceCraft.GameApp>();
if(!UnityEngine.Application.isPlaying||UnityEditor.EditorApplication.isCompiling||app==null||app.Session==null||!app.StartupReady07||app.canvas==null)throw new System.InvalidOperationException("Require ready isolated Play Bootstrap; this eval does not start it.");
if(app.Session.State!=null||app.DebugUnlocked||app.IsBusy||app.Session.HasPendingSave)throw new System.InvalidOperationException("Require fresh empty isolated main menu.");
if(UnityEngine.Screen.width!=1920||UnityEngine.Screen.height!=1080)throw new System.InvalidOperationException("Actual GPU surface must be 1920x1080; no rescaling of evidence.");
if(UnityEngine.Object.FindObjectsByType<DiceCraft.Verification.Art07ScreenRecorder>().Any(x=>x.IsRecording)||UnityEngine.Object.FindObjectsByType<DiceCraft.Verification.Art07VisualRecorder>().Any(x=>x.IsRecording))throw new System.InvalidOperationException("Another recorder is active.");
string saves=System.IO.Path.GetFullPath(DiceCraft.GameApp.SaveDirectoryOverride??throw new System.InvalidOperationException("Isolated override absent."));
if(Inside(saves,UnityEngine.Application.persistentDataPath)||!Inside(app.SaveLocation,saves)||System.IO.File.Exists(app.SaveLocation)||System.IO.File.Exists(System.IO.Path.Combine(saves,"debug-run.json")))throw new System.InvalidOperationException("Override must be fresh and outside ordinary storage, set before Bootstrap Awake.");
if(!Inside(output,System.IO.Path.Combine(System.IO.Directory.GetParent(UnityEngine.Application.dataPath).Parent.FullName,"artifacts","cache"))||Inside(output,input)||Inside(output,saves)||Inside(saves,output)||System.IO.Directory.Exists(output))throw new System.InvalidOperationException("Output must be a NEW cache directory separate from input/saves.");
if(HashFile(System.IO.Path.Combine(input,"assets.tsv"))!="b4efc0b480d227dcdd78212b00936dfdeea477bdd1c369ab2b2fc465a683c99c"||HashFile(System.IO.Path.Combine(input,"samples.tsv"))!="54d6b702cbdd6e3681107884c87ad8ac23a17372ea232bb88a0b6dd1bca15e16")throw new System.InvalidOperationException("Frozen target list changed.");
var assets=System.IO.File.ReadAllLines(System.IO.Path.Combine(input,"assets.tsv")).Select(l=>l.Split('\t')).ToDictionary(a=>a[0]);
var samples=System.IO.File.ReadAllLines(System.IO.Path.Combine(input,"samples.tsv")).Select(l=>l.Split('\t')).ToArray();
if(assets.Count!=146||samples.Length!=567||samples.Select(s=>s[0]).Distinct().Count()!=43)throw new System.InvalidOperationException("Unexpected target coverage.");
string project=System.IO.Directory.GetParent(UnityEngine.Application.dataPath).FullName;
foreach(var a in assets.Values)if(HashFile(System.IO.Path.Combine(project,a[1]))!=a[2]||HashFile(System.IO.Path.Combine(project,a[1]+".meta"))!=a[3])throw new System.InvalidOperationException("Asset/import source differs: "+a[0]);
string ordinary=System.IO.Path.Combine(UnityEngine.Application.persistentDataPath,"active-run.json"),ordinaryBefore=HashFile(ordinary),saveBefore=HashFile(app.SaveLocation);
long sequence=app.Session.ActionSequence;
var prefKeys=new[]{"art07.textScale","shopTab","art07.guideSeen",DiceCraft.GameApp.ZeroRollHintKey07,DiceCraft.GameApp.RandomHintKey07,DiceCraft.GameApp.QualifiedHintKey07};
var prefs=prefKeys.Select(k=>new{key=k,existed=UnityEngine.PlayerPrefs.HasKey(k),value=UnityEngine.PlayerPrefs.GetInt(k,0)}).ToArray();
float fontBefore=DiceCraft.UiKit.FontScale;
var jsonType=System.AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Newtonsoft.Json.JsonConvert",false)).FirstOrDefault(t=>t!=null);
var serializer=jsonType?.GetMethod("SerializeObject",new[]{typeof(object)})??throw new System.InvalidOperationException("Existing JSON serializer unavailable.");
System.IO.Directory.CreateDirectory(output);
UnityEngine.GameObject root=null;
var captures=new System.Collections.Generic.List<object>();
var report=new System.Collections.Generic.Dictionary<string,object>{{"schema","art14-gpu-capture-1"},{"status","running"},{"unity",UnityEngine.Application.unityVersion},{"runtimeMvid",typeof(DiceCraft.GameApp).Module.ModuleVersionId.ToString()},{"coreMvid",typeof(DiceCraft.Core.GameSession).Module.ModuleVersionId.ToString()},{"gpu",UnityEngine.SystemInfo.graphicsDeviceName},{"graphicsApi",UnityEngine.SystemInfo.graphicsDeviceType.ToString()},{"colorSpace",UnityEngine.QualitySettings.activeColorSpace.ToString()},{"screens",captures},{"sourcePlanSha256",HashFile(System.IO.Path.Combine(input,"target-plan.json"))},{"sourceIdentitySha256",HashFile(System.IO.Path.Combine(UnityEngine.Application.dataPath,"Resources/BuildIdentity.json"))},{"ordinaryBefore",ordinaryBefore},{"saveBefore",saveBefore},{"saveOverride",saves},{"preferencesBefore",prefs},{"fontScaleBefore",fontBefore},{"startedUtc",System.DateTime.UtcNow.ToString("O")},{"noVisualPassFromCounts",true}};
Write();app.StartCoroutine(Guarded());
return new{status="started",report=System.IO.Path.Combine(output,"capture-report.json"),pages=43,instances=567};

bool Inside(string p,string r){p=System.IO.Path.GetFullPath(p);r=System.IO.Path.GetFullPath(r).TrimEnd('\\','/');return p.Equals(r,System.StringComparison.OrdinalIgnoreCase)||p.StartsWith(r+System.IO.Path.DirectorySeparatorChar,System.StringComparison.OrdinalIgnoreCase);}
string Hash(byte[] b){using(var h=System.Security.Cryptography.SHA256.Create())return string.Concat(h.ComputeHash(b).Select(x=>x.ToString("x2")));}
string HashFile(string p){return System.IO.File.Exists(p)?Hash(System.IO.File.ReadAllBytes(p)):"missing";}
float Number(string x){return float.Parse(x,System.Globalization.CultureInfo.InvariantCulture);}
void Write(){System.IO.File.WriteAllText(System.IO.Path.Combine(output,"capture-report.json"),(string)serializer.Invoke(null,new[]{(object)report}));}
UnityEngine.Color Background(string name){return name=="white"?UnityEngine.Color.white:name=="deep_teal"?new UnityEngine.Color32(16,42,45,255):name=="red"?new UnityEngine.Color32(236,101,93,255):throw new System.InvalidOperationException("Unknown background.");}
UnityEngine.RectTransform Rect(string name,UnityEngine.Transform parent,float x,float y,float w,float h){var go=new UnityEngine.GameObject(name,typeof(UnityEngine.RectTransform));go.hideFlags=UnityEngine.HideFlags.DontSave;var r=(UnityEngine.RectTransform)go.transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new UnityEngine.Vector2(0,1);r.anchoredPosition=new UnityEngine.Vector2(x,-y);r.sizeDelta=new UnityEngine.Vector2(w,h);return r;}
object Bounds(UnityEngine.RectTransform r){var corners=new UnityEngine.Vector3[4];r.GetWorldCorners(corners);var v=corners.Select(c=>UnityEngine.RectTransformUtility.WorldToScreenPoint(null,c)).ToArray();return new{x=v.Min(c=>c.x),yTop=1080-v.Max(c=>c.y),width=v.Max(c=>c.x)-v.Min(c=>c.x),height=v.Max(c=>c.y)-v.Min(c=>c.y)};}
bool Preserved(){return app!=null&&app.Session.State==null&&app.Session.ActionSequence==sequence&&!app.Session.HasPendingSave&&HashFile(app.SaveLocation)==saveBefore&&HashFile(ordinary)==ordinaryBefore&&DiceCraft.UiKit.FontScale==fontBefore&&prefs.All(p=>UnityEngine.PlayerPrefs.HasKey(p.key)==p.existed&&UnityEngine.PlayerPrefs.GetInt(p.key,0)==p.value);}
System.Collections.IEnumerator Capture(){
 root=new UnityEngine.GameObject("ART14_CacheOnlyCapture",typeof(UnityEngine.RectTransform),typeof(UnityEngine.Canvas));root.hideFlags=UnityEngine.HideFlags.DontSave;
 var canvas=root.GetComponent<UnityEngine.Canvas>();canvas.renderMode=UnityEngine.RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32760;canvas.pixelPerfect=true;canvas.scaleFactor=1;canvas.referencePixelsPerUnit=100;
 foreach(var page in samples.GroupBy(s=>s[0])){
  var stage=Rect(page.Key,root.transform,0,0,1920,1080);
  string[] backgrounds={"white","deep_teal","red"};bool single=page.Count()==1;
  for(int i=0;i<(single?1:3);i++){string bg=single?page.First()[3]:backgrounds[i];var box=Rect("Background_"+bg,stage,single?0:i*640,0,single?1920:640,1080);var img=box.gameObject.AddComponent<UnityEngine.UI.Image>();img.color=Background(bg);img.raycastTarget=false;}
  var rows=new System.Collections.Generic.List<object>();var graphics=new System.Collections.Generic.List<UnityEngine.UI.Graphic>();
  foreach(var s in page){
   var a=assets[s[2]];var texture=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(a[1]);if(texture==null)throw new System.InvalidOperationException("Missing imported texture "+s[2]);
   var sprite=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(a[1]);var r=Rect(s[1],stage,Number(s[4]),Number(s[5]),Number(s[6]),Number(s[7]));
   UnityEngine.UI.Graphic g;
   if(sprite!=null){var im=r.gameObject.AddComponent<UnityEngine.UI.Image>();im.sprite=sprite;im.type=s[9]=="sliced"?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;im.preserveAspect=false;im.pixelsPerUnitMultiplier=1;im.useSpriteMesh=false;g=im;if(s[9]=="sliced"&&(!sprite.border.Equals(new UnityEngine.Vector4(Number(a[6]),Number(a[7]),Number(a[8]),Number(a[9])))||!im.hasBorder))throw new System.InvalidOperationException("Actual nine-slice borders missing/differ: "+s[2]);}
   else{if(s[9]!="auto")throw new System.InvalidOperationException("Required imported Sprite absent: "+s[2]);var im=r.gameObject.AddComponent<UnityEngine.UI.RawImage>();im.texture=texture;g=im;}
   g.raycastTarget=false;g.color=UnityEngine.Color.white;graphics.Add(g);
   rows.Add(new{instanceId=s[1],assetId=s[2],assetPath=a[1],assetGuid=UnityEditor.AssetDatabase.AssetPathToGUID(a[1]),pngSha256=a[2],metaSha256=a[3],background=s[3],backgroundRgba=(object)new{r=Background(s[3]).r,g=Background(s[3]).g,b=Background(s[3]).b,a=1},requestedTopLeft=new{x=Number(s[4]),y=Number(s[5]),width=Number(s[6]),height=Number(s[7])},variant=s[8],renderer=g.GetType().FullName,nativeWidth=texture.width,nativeHeight=texture.height,textureFilter=texture.filterMode.ToString(),diagnosticOnly=a[10]=="true",spriteName=sprite==null?null:sprite.name,spritePacked=sprite!=null&&sprite.packed,spriteRotation=sprite!=null&&sprite.packed?sprite.packingRotation.ToString():"not-packed",actualTexture=g.mainTexture==null?null:g.mainTexture.name});
  }
  UnityEngine.Canvas.ForceUpdateCanvases();yield return null;yield return new UnityEngine.WaitForEndOfFrame();
  if(UnityEngine.Screen.width!=1920||UnityEngine.Screen.height!=1080||!Preserved())throw new System.InvalidOperationException("Capture altered dimensions or read-only session boundary.");
  var geometry=graphics.Select(g=>{var mesh=g.canvasRenderer.GetMesh();if(mesh==null||mesh.vertexCount==0||g.canvasRenderer.cull||g.canvasRenderer.GetAlpha()<=0||g.materialForRendering==null||!g.materialForRendering.shader.isSupported)throw new System.InvalidOperationException("Non-rendering specimen: "+g.name);return (object)new{instanceId=g.name,actualBounds=Bounds(g.rectTransform),vertices=mesh.vertexCount,triangles=mesh.triangles.Length/3,culled=g.canvasRenderer.cull,alpha=g.canvasRenderer.GetAlpha(),shader=g.materialForRendering.shader.name};}).ToArray();
  UnityEngine.Texture2D frame=null;try{frame=UnityEngine.ScreenCapture.CaptureScreenshotAsTexture();if(frame==null||frame.width!=1920||frame.height!=1080)throw new System.InvalidOperationException("GPU readback dimensions invalid.");var bytes=UnityEngine.ImageConversion.EncodeToPNG(frame);string file=page.Key+".png";System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,file),bytes);captures.Add(new{id=page.Key,file,sha256=Hash(bytes),width=frame.width,height=frame.height,frame=UnityEngine.Time.frameCount,canvasScale=canvas.scaleFactor,instances=rows,geometry,stateAndSavePreserved=Preserved()});Write();}finally{if(frame!=null)UnityEngine.Object.Destroy(frame);}
  UnityEngine.Object.Destroy(stage.gameObject);yield return null;
 }
}
System.Collections.IEnumerator Guarded(){var stack=new System.Collections.Generic.Stack<System.Collections.IEnumerator>();stack.Push(Capture());string error=null;try{while(stack.Count>0){object current=null;bool more=false;try{more=stack.Peek().MoveNext();if(more)current=stack.Peek().Current;else stack.Pop();}catch(System.Exception e){error=e.ToString();break;}if(!more)continue;if(current is System.Collections.IEnumerator nested)stack.Push(nested);else yield return current;}}finally{if(root!=null){root.SetActive(false);UnityEngine.Object.Destroy(root);}bool intact=Preserved();report["readOnlySessionPreserved"]=intact;report["ordinaryAfter"]=HashFile(ordinary);report["saveAfter"]=HashFile(app.SaveLocation);report["error"]=error;report["endedUtc"]=System.DateTime.UtcNow.ToString("O");report["status"]=error==null&&intact&&captures.Count==43?"captured_pending_independent_visual_review":"failed";Write();}}

