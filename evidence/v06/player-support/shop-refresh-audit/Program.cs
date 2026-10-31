using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using DiceCraft.Core;
class Program {
 static void Main(){
 var opts=new JsonSerializerOptions{IncludeFields=true,WriteIndented=true};
 string path="evidence/v06/player/player-20261031-152132-2388cb4a/debug-refresh-shop-save/debug-run.json";
 var env=JsonDocument.Parse(File.ReadAllText(path));
 var cfg=JsonSerializer.Deserialize<GameConfig>(GameConfig.PrepareJsonForUnity(File.ReadAllText("DicePrototype/Assets/Game/Data/P0Config.json")),opts);cfg.Validate();
 var s=JsonSerializer.Deserialize<RunState>(env.RootElement.GetProperty("payload").GetString(),opts);
 string receipt=s.processedReceipts.Last().stateHash;
 void Show(string label){Console.WriteLine(label+" "+GameSession.StateHash(s)+" receiptMatch="+(GameSession.StateHash(s)==receipt));}
 Console.WriteLine("expectedReceipt "+receipt);Show("rawPayload");
 s.lastSubmission=null;Show("actualLoadLastSubmissionNull");
 try{new GameSession(cfg,new MemorySaveStore(),s);Console.WriteLine("ActualLoadShapeValid=true");}catch(Exception ex){Console.WriteLine(ex);}
 foreach(var o in s.shop.offers)if(o.category!="controller")o.controller=null;
 Show("onlyNoncontrollerOfferControllerNull");
 foreach(var o in s.shop.offers)if(o.category!="new_die")o.die=null;
 Show("alsoNonNewDieOfferDieNull");
 File.WriteAllText("evidence/v06/player-support/shop-refresh-audit/semantic-after.json",JsonSerializer.Serialize(s,opts));
 var oldEnvelope=JsonDocument.Parse(File.ReadAllText(path+".backup"));
 var before=JsonSerializer.Deserialize<RunState>(oldEnvelope.RootElement.GetProperty("payload").GetString(),opts);
 before.lastSubmission=null;
 foreach(var o in before.shop.offers){if(o.category!="controller")o.controller=null;if(o.category!="new_die")o.die=null;}
 var session=new GameSession(cfg,new MemorySaveStore(),before);
 var command=s.processedReceipts.Last().command.Clone();
 var result=session.Execute(command);
 Console.WriteLine("ReplayActualPersistedCommand accepted="+result.accepted+" duplicate="+result.duplicate+" error="+result.error);
 Console.WriteLine("ReplayStateHash "+GameSession.StateHash(session.State)+" matchesActualReceipt="+(GameSession.StateHash(session.State)==receipt));
 if(!result.accepted||GameSession.StateHash(session.State)!=receipt)throw new Exception("Actual saved prestate/command replay did not match the persisted receipt.");
 var replay=session.State;
 Console.WriteLine("ReplayWallet="+replay.gold+" sequence="+replay.actionSequence+" refreshCount="+replay.shop.controllerRefreshCount+" generation="+replay.shop.controllerGeneration);
 foreach(var o in replay.shop.offers)Console.WriteLine("Quote "+o.offerId+" "+o.category+" "+o.contentId+" price="+session.GetOfferPrice(o)+" net="+session.GetOfferNetPrice(o));
 }
}
